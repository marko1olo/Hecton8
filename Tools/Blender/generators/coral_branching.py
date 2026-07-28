"""Branching coral generator.

Specification: ``3DMODEL_FLORA_CORAL.md`` section 3 "Coral" -> "Branching coral:
welded trunk, branch hierarchy, knuckles, tip clusters, asymmetry", with section 3's
absolute constraint:

    "Branch intersections must be blended, welded, or explicitly hidden by knuckles.
     Intersecting tubes with z-fighting are rejected."

WHY A SKELETON PLUS SKIN, NOT SWEPT TUBES
-----------------------------------------
The obvious implementation -- generate a tube per branch and place them -- cannot
satisfy that constraint. Two tubes meeting at an angle interpenetrate, and the surfaces
inside each other z-fight exactly as the bible describes. Welding them afterwards means
solving a boolean union on organic geometry, which produces the degenerate slivers the
validator then rejects.

So the branch structure is authored as a VERTEX/EDGE SKELETON with a per-vertex radius,
and Blender's Skin modifier builds the surface. That inverts the problem: the surface is
generated as a single manifold hull around the skeleton, so branch unions are welded by
construction and the modifier itself thickens the joints into knuckles. There is no
intersection to fix because none is ever created.

The rest follows ``PROCEDURAL_ASSET_PIPELINE.md`` "Generation Order" in the stated
sequence. AO is baked BEFORE the vertex-colour compose, because the bake writes all four
channels and would otherwise erase the sway gradient -- see ``h8forge.vertexcolor``.
"""

from __future__ import annotations

import argparse
import math
import os
import sys
from dataclasses import dataclass, field
from typing import List, Optional

# Blender runs this file directly, so the package root is not on sys.path yet.
_HERE = os.path.dirname(os.path.abspath(__file__))
_BLENDER_TOOLS = os.path.dirname(_HERE)
if _BLENDER_TOOLS not in sys.path:
    sys.path.insert(0, _BLENDER_TOOLS)

import bmesh  # noqa: E402
import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

from h8forge import law, mesh_ops, preview, vertexcolor  # noqa: E402
from h8forge.blackbox import BlackBox, GenerationAborted  # noqa: E402


GENERATOR_NAME = "coral_branching"
GENERATOR_VERSION = "1.0.0"


# ---------------------------------------------------------------------------
# Growth parameters
# ---------------------------------------------------------------------------

@dataclass
class CoralSpec:
    """Deterministic growth description.

    Every field is a named parameter, never hidden chance:
    ``PROCEDURAL_ASSET_PIPELINE.md`` -- "If artist variation is needed, variation is a
    named seed, not hidden chance."
    """

    seed: int = 1712
    quality: float = 1.0

    height_m: float = 0.85
    trunk_radius_m: float = 0.055
    tip_radius_m: float = 0.008

    # Branch hierarchy. Depth is capped rather than quality-scaled because depth changes
    # the SILHOUETTE, and the pipeline bible forbids quality changing gameplay-visible
    # identity: "Raising quality may add visual detail but must not ... create a
    # different gameplay route." Quality scales density and surface detail instead.
    max_depth: int = 4
    children_min: int = 2
    children_max: int = 3

    branch_angle_deg: float = 38.0
    angle_jitter_deg: float = 17.0
    length_decay: float = 0.68
    radius_decay: float = 0.62

    # Asymmetry: coral grows into current and toward light. A symmetric candelabra
    # reads as procedural, which section 3 rejects.
    current_direction: tuple = (0.62, 0.28, 0.0)
    current_bias: float = 0.34
    phototropism: float = 0.22

    # Mineralised skeleton: rigid, so sway is capped into the bible's 0..32/255 band.
    mineralised: bool = True

    # Surface character. Fine-pore weight is deliberately low: the first attempt used a
    # 0.30 fine octave and the tips read as cauliflower rather than polyp clusters.
    pore_strength: float = 0.55
    ring_frequency: float = 38.0
    ring_strength: float = 0.34
    fine_pore_weight: float = 0.12
    knuckle_swell: float = 1.45
    tip_cluster_count: int = 3

    large_enough_to_block_path: bool = False

    def branch_segments(self) -> int:
        """Skeleton subdivisions per branch. Continuous in GlobalQualityWeight."""
        return int(round(3 + 5 * law.saturate(self.quality)))

    def skin_subdivisions(self) -> int:
        """Surface refinement level. Continuous, and never zero -- an unsubdivided skin
        hull is a faceted tube, which is the primitive look the bible rejects."""
        return int(round(1 + 2 * law.saturate(self.quality)))


@dataclass
class SkeletonNode:
    position: Vector
    radius: float
    depth: int
    parent: Optional[int]
    distance_from_anchor: float
    is_tip: bool = False


@dataclass
class CoralResult:
    name: str
    lods: list = field(default_factory=list)
    collider: Optional[object] = None
    sway_report: dict = field(default_factory=dict)
    ao_report: Optional[object] = None
    node_count: int = 0
    tip_count: int = 0
    preview_paths: tuple = ()
    channel_stats: tuple = ()


# ---------------------------------------------------------------------------
# Stage 2: shape grammar -- the skeleton
# ---------------------------------------------------------------------------

def build_skeleton(spec: CoralSpec, blackbox: BlackBox) -> List[SkeletonNode]:
    """Recursive branch hierarchy as nodes, anchored at the origin.

    Growth direction blends three influences, which is what separates a coral from a
    fractal tree: the parent direction (structural continuity), the current vector
    (flow-facing asymmetry the bible demands), and upward phototropism. Jitter is drawn
    from the seeded generator so the same seed always yields the same colony.
    """
    rng = np.random.default_rng(spec.seed)
    current = Vector(spec.current_direction)
    if current.length > 1e-6:
        current.normalize()

    nodes: List[SkeletonNode] = [
        SkeletonNode(Vector((0.0, 0.0, 0.0)), spec.trunk_radius_m, 0, None, 0.0)
    ]

    def grow(parent_index: int, direction: Vector, length: float,
             radius: float, depth: int) -> None:
        parent = nodes[parent_index]
        segments = spec.branch_segments()
        segment_length = length / segments
        current_dir = direction.copy()
        cursor = parent_index

        for step in range(segments):
            # Bend along the branch so no segment is a straight cylinder.
            bend = Vector((
                float(rng.normal(0.0, 0.16)),
                float(rng.normal(0.0, 0.16)),
                float(rng.normal(0.0, 0.09)),
            ))
            current_dir = (current_dir
                           + bend * 0.5
                           + current * (spec.current_bias * 0.28)
                           + Vector((0.0, 0.0, spec.phototropism * 0.22)))
            if current_dir.length < 1e-6:
                current_dir = Vector((0.0, 0.0, 1.0))
            current_dir.normalize()

            source = nodes[cursor]
            position = source.position + current_dir * segment_length
            t = (step + 1) / float(segments)
            node_radius = radius * (1.0 - t) + (radius * spec.radius_decay) * t
            nodes.append(SkeletonNode(
                position=position,
                radius=max(spec.tip_radius_m, node_radius),
                depth=depth,
                parent=cursor,
                distance_from_anchor=source.distance_from_anchor + segment_length,
            ))
            cursor = len(nodes) - 1

        if depth >= spec.max_depth:
            nodes[cursor].is_tip = True
            _add_tip_cluster(nodes, cursor, spec, rng)
            return

        child_count = int(rng.integers(spec.children_min, spec.children_max + 1))
        base_dir = current_dir.copy()
        # Distribute children around the parent axis with a seeded phase offset, so
        # sibling branches do not all lean the same way.
        phase = float(rng.uniform(0.0, math.tau))
        for child in range(child_count):
            angle = math.radians(
                spec.branch_angle_deg + float(rng.normal(0.0, spec.angle_jitter_deg)))
            azimuth = phase + (math.tau * child / max(1, child_count))
            axis = _perpendicular(base_dir)
            rotated = base_dir.copy()
            rotated.rotate(Matrix.Rotation(angle, 4, axis))
            rotated.rotate(Matrix.Rotation(azimuth, 4, base_dir))
            rotated += current * spec.current_bias
            if rotated.length < 1e-6:
                continue
            rotated.normalize()
            grow(cursor,
                 rotated,
                 length * spec.length_decay * float(rng.uniform(0.82, 1.12)),
                 nodes[cursor].radius * spec.radius_decay,
                 depth + 1)

    # Short first run so branching starts LOW. The first attempt used 0.42 of total
    # height for the trunk, which produced a long bare stalk with one clump of branches
    # on top - a silhouette that reads as broccoli, not as a coral colony whose branches
    # fan from near the holdfast.
    grow(0, Vector((0.0, 0.0, 1.0)), spec.height_m * 0.20,
         spec.trunk_radius_m, 1)

    tips = sum(1 for n in nodes if n.is_tip)
    blackbox.record("skeleton", seed=spec.seed, family=law.Family.FLORA.value,
                    vertex_count=len(nodes),
                    warning="" if tips else "skeleton produced no tips")
    return nodes


def _perpendicular(direction: Vector) -> Vector:
    """Any unit vector perpendicular to ``direction``, chosen stably.

    Crossing with a fixed axis fails when the direction is parallel to it, which for a
    vertical trunk is the common case, not the edge case.
    """
    reference = Vector((0.0, 0.0, 1.0))
    if abs(direction.dot(reference)) > 0.94:
        reference = Vector((1.0, 0.0, 0.0))
    out = direction.cross(reference)
    if out.length < 1e-6:
        return Vector((1.0, 0.0, 0.0))
    out.normalize()
    return out


def _add_tip_cluster(nodes: List[SkeletonNode], tip_index: int,
                     spec: CoralSpec, rng) -> None:
    """Short thick nubs at a branch end.

    Section 3 lists "tip clusters" as a required structure for branching coral. A branch
    that simply narrows to a point reads as a stick; real polyp-bearing tips flare.
    """
    tip = nodes[tip_index]
    for _ in range(max(0, spec.tip_cluster_count)):
        direction = Vector((
            float(rng.normal(0.0, 1.0)),
            float(rng.normal(0.0, 1.0)),
            float(rng.uniform(0.15, 1.0)),
        ))
        if direction.length < 1e-6:
            continue
        direction.normalize()
        length = tip.radius * float(rng.uniform(2.2, 4.0))
        nodes.append(SkeletonNode(
            position=tip.position + direction * length,
            radius=tip.radius * float(rng.uniform(0.85, 1.25)),
            depth=tip.depth + 1,
            parent=tip_index,
            distance_from_anchor=tip.distance_from_anchor + length,
            is_tip=True,
        ))


# ---------------------------------------------------------------------------
# Stage 3: skeleton -> welded surface
# ---------------------------------------------------------------------------

def skeleton_to_object(nodes: List[SkeletonNode], spec: CoralSpec,
                       name: str, collection: bpy.types.Collection,
                       blackbox: BlackBox) -> bpy.types.Object:
    """Skin the skeleton into one manifold surface with welded joints.

    The Skin modifier requires a mesh of verts and EDGES with no faces, one vertex
    marked as root, and a per-vertex radius pair. Joint radius is swollen by
    ``knuckle_swell`` at branch points, which is what turns a Y-junction into the
    knuckle the bible asks for rather than a smooth taper through the fork.
    """
    mesh = bpy.data.meshes.new(name + "_skeleton")
    vertices = [tuple(node.position) for node in nodes]
    edges = [(index, node.parent) for index, node in enumerate(nodes)
             if node.parent is not None]
    mesh.from_pydata(vertices, edges, [])
    mesh.update()

    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)

    modifier = obj.modifiers.new(name="H8_Skin", type="SKIN")
    modifier.use_smooth_shade = True
    modifier.branch_smoothing = 0.28

    # Count children per node so forks can be identified and swollen.
    child_counts = [0] * len(nodes)
    for node in nodes:
        if node.parent is not None:
            child_counts[node.parent] += 1

    skin_layer = mesh.skin_vertices[0].data
    for index, node in enumerate(nodes):
        radius = node.radius
        if child_counts[index] > 1:
            radius *= spec.knuckle_swell
        skin_layer[index].radius = (radius, radius)
        skin_layer[index].use_root = (node.parent is None)

    mesh_ops._make_sole_active(obj)
    bpy.ops.object.modifier_apply(modifier=modifier.name)

    blackbox.record("skin", vertex_count=len(obj.data.vertices),
                    triangle_count=mesh_ops.triangle_count(obj.data))
    return obj


@dataclass
class SkeletonSampler:
    """Nearest-skeleton-node lookup: local branch radius and geodesic distance per point.

    Both quantities are needed and neither can be derived from the vertex position alone:

    - Displacement amplitude must scale with LOCAL BRANCH THICKNESS. An earlier version
      scaled it by horizontal distance from the Z axis, which gave the trunk -- sitting
      on the axis -- almost no displacement while the outer tips got the full amount.
      The visible result was a porcelain-smooth stem with popcorn florets, and the
      amplitude disparity was violent enough to self-intersect the tip geometry.
    - Sway must use GEODESIC distance along the branch, not straight-line distance from
      the anchor. A branch that arcs back over its own base is far along the stem but
      physically near the holdfast, and Euclidean distance would call its tip rigid.
    """

    tree: object
    radii: list
    distances: list

    @classmethod
    def build(cls, nodes: List[SkeletonNode]) -> "SkeletonSampler":
        from mathutils import kdtree

        tree = kdtree.KDTree(len(nodes))
        for index, node in enumerate(nodes):
            tree.insert(node.position, index)
        tree.balance()
        return cls(
            tree=tree,
            radii=[node.radius for node in nodes],
            distances=[node.distance_from_anchor for node in nodes],
        )

    def sample(self, point: Vector) -> tuple:
        """(local_radius, geodesic_distance) at the nearest skeleton node."""
        _co, index, _dist = self.tree.find(point)
        if index is None:
            return (0.01, 0.0)
        return (self.radii[index], self.distances[index])


def refine_surface(obj: bpy.types.Object, spec: CoralSpec,
                   sampler: SkeletonSampler, blackbox: BlackBox) -> None:
    """Subdivide, then displace along normals for pores and secondary silhouette.

    ``3dmodel.md`` section 5: "the saved mesh must contain secondary silhouette noise,
    believable taper, compression, scars, growth rings, cavities, and nonuniform
    cross-sections." The skin hull supplies taper and cross-section variation; this
    stage supplies the surface history.

    Three frequency bands, not one, because a single isotropic noise reads as crust:
      - growth rings, ANISOTROPIC along the branch axis -- the structural signature that
        makes a stem read as grown rather than extruded;
      - coarse lobes, which carve the cavities the AO bake needs to find;
      - fine pores, kept deliberately weak. Fine noise is what turned the first attempt
        into cauliflower.

    All bands are hashed from object-space position plus the seed, so the same seed
    reproduces the same surface. ``mathutils.noise`` is avoided on purpose: its seed is
    process-global, so two generators in one Blender session would perturb each other.
    """
    levels = spec.skin_subdivisions()
    if levels > 0:
        modifier = obj.modifiers.new(name="H8_Subsurf", type="SUBSURF")
        modifier.subdivision_type = "CATMULL_CLARK"
        modifier.levels = levels
        modifier.render_levels = levels
        mesh_ops._make_sole_active(obj)
        bpy.ops.object.modifier_apply(modifier=modifier.name)

    strength = spec.pore_strength * (0.55 + 0.45 * law.saturate(spec.quality))
    if strength <= 0.0:
        return

    mesh = obj.data
    seed_offset = float(spec.seed % 9973) * 0.013

    for vertex in mesh.vertices:
        position = vertex.co.copy()
        local_radius, geodesic = sampler.sample(position)

        # Growth rings: banded along the branch, so the modulation follows the structure
        # instead of sitting on top of it.
        rings = math.sin(geodesic * spec.ring_frequency + seed_offset * 3.1)
        coarse = _value_noise(position * 9.0, seed_offset)
        fine = _value_noise(position * 31.0, seed_offset + 4.77)

        amount = (rings * spec.ring_strength
                  + coarse * 0.62
                  + fine * spec.fine_pore_weight) * strength
        # Amplitude proportional to the LOCAL branch radius: a 5 mm tip and a 55 mm
        # trunk both get proportionate relief, and nothing is displaced past its own
        # thickness into a self-intersection.
        vertex.co = position + vertex.normal * (amount * local_radius * 0.85)

    mesh.update()
    blackbox.record("refine_surface", vertex_count=len(mesh.vertices),
                    triangle_count=mesh_ops.triangle_count(mesh))


def _value_noise(point: Vector, seed_offset: float) -> float:
    """Deterministic smooth noise in -1..1 from a position hash.

    Trigonometric hashing rather than ``mathutils.noise``: that module's seed is global
    process state, so two generators in one Blender session would perturb each other and
    neither would be reproducible from its own seed.
    """
    x = point.x + seed_offset
    y = point.y - seed_offset * 0.5
    z = point.z + seed_offset * 0.25
    value = (math.sin(x * 1.7 + math.cos(y * 2.3) * 1.9 + z * 0.7)
             + math.sin(y * 2.9 + math.cos(z * 1.3) * 1.4 + x * 0.5)
             + math.sin(z * 2.1 + math.cos(x * 1.1) * 2.2 + y * 0.9))
    return max(-1.0, min(1.0, value / 3.0))


# ---------------------------------------------------------------------------
# Stage 5: UVs and material slots
# ---------------------------------------------------------------------------

def unwrap_and_assign_materials(obj: bpy.types.Object, spec: CoralSpec,
                                blackbox: BlackBox) -> dict:
    """Angle-preserving unwrap plus the bible's material slot roles.

    ``3dmodel.md`` section 6 permits "Conformal unwrap using LSCM/ABF-style angle
    preservation for unique surfaces" -- Blender's Smart UV Project is that class of
    solver, so this is the sanctioned route rather than a box projection.

    ``3DMODEL_FLORA_CORAL.md`` section 5 allows triplanar for massive rock-like coral
    but is explicit that "branch tubes still need coherent UVs for detail normal and
    phase masks". A branching colony is tubes, so it gets real UVs.

    Slots follow section 6: 0 primary tissue, 1 exposed cut/scar, 2 growth plate /
    barnacle trim, 3 emissive polyps.
    """
    for role, name in (
        (law.MATERIAL_SLOT_PRIMARY, "Tissue"),
        (law.MATERIAL_SLOT_CUT_EDGE, "BrokenTip"),
        (law.MATERIAL_SLOT_TRIM, "GrowthPlate"),
        (law.MATERIAL_SLOT_EMISSIVE, "Polyp"),
    ):
        material_name = law.NAME_MATERIAL.format(family=law.Family.FLORA.value, role=name)
        material = bpy.data.materials.get(material_name)
        if material is None:
            material = bpy.data.materials.new(material_name)
            material.use_nodes = True
        obj.data.materials.append(material)

    mesh_ops._make_sole_active(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0),
                             island_margin=0.012,
                             correct_aspect=True,
                             scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")

    uv_layer = obj.data.uv_layers.active
    report = {
        "uvRoute": "smart_project (LSCM-class conformal), island_margin=0.012",
        "uvLayer": uv_layer.name if uv_layer else None,
        "materialSlots": [slot.material.name for slot in obj.material_slots],
        "texelDensityTarget": law.TEXEL_DENSITY_COMMON_FLORA,
    }
    blackbox.record("unwrap", vertex_count=len(obj.data.vertices),
                    warning="" if uv_layer else "no active UV layer after unwrap")
    return report


# ---------------------------------------------------------------------------
# Stage 6: bakes and vertex colours
# ---------------------------------------------------------------------------

def author_channels(obj: bpy.types.Object, spec: CoralSpec,
                    sampler: SkeletonSampler, blackbox: BlackBox) -> tuple:
    """Bake AO first, then compose R/G/B/A. Order is not cosmetic -- see module docstring.

    R uses ``STIFFNESS_EXPONENT_BRANCHING_CORAL`` and, for a mineralised colony, the
    rigid cap that keeps values inside the bible's 0..32/255 band while PRESERVING a
    gradient inside it. Clamping to a constant would satisfy the band and then trip the
    "root sways as much as tips" rejection gate.
    """
    ao_result = vertexcolor.bake_ambient_occlusion(
        obj, samples=int(24 + 40 * law.saturate(spec.quality)),
        distance=0.22, blackbox=blackbox)
    ao_values = vertexcolor.consume_baked_ao(obj)

    lo, hi = mesh_ops.local_bounds(obj)
    anchor = Vector((0.0, 0.0, lo.z))
    flexible_length = max(1e-4, (hi.z - lo.z))

    # Geodesic distance along the branch, sampled from the skeleton, rather than
    # straight-line distance from the anchor. See SkeletonSampler for why that matters
    # on a colony whose branches arc back over their own base.
    geodesic = [sampler.sample(v.co)[1] for v in obj.data.vertices]
    max_geodesic = max(geodesic) if geodesic else flexible_length

    sway = vertexcolor.build_sway_field(
        obj.data,
        anchor_position=anchor,
        max_flexible_length=max(1e-4, max_geodesic),
        stiffness_exponent=law.STIFFNESS_EXPONENT_BRANCHING_CORAL,
        rigid_cap=law.SWAY_RIGID_MINERAL_MAX if spec.mineralised else None,
        distances=geodesic,
    )

    # G: polyps glow at the extremities. Phase varies per tip so a field of colonies
    # does not pulse in unison -- driven by position hash, so still deterministic.
    biolum = []
    for vertex in obj.data.vertices:
        height = (vertex.co.z - lo.z) / flexible_length
        exposure = law.saturate((height - 0.55) / 0.45)
        phase = 0.5 + 0.5 * _value_noise(vertex.co * 6.0, float(spec.seed % 977))
        biolum.append(law.saturate(exposure * (0.35 + 0.65 * phase)))

    # A: harvest mask -- where a cutting tool yields material. Thick lower structure is
    # harvestable, brittle tips are not. Documented in the manifest, as the bible
    # requires for the alpha channel.
    alpha = []
    for vertex in obj.data.vertices:
        height = (vertex.co.z - lo.z) / flexible_length
        alpha.append(law.saturate(1.0 - height * 0.85))

    report = vertexcolor.write_organic_channels(
        obj, sway=sway, biolum=biolum,
        ao=ao_values if ao_values else None,
        alpha=alpha, alpha_meaning="harvest_yield_mask",
        blackbox=blackbox)

    vertexcolor.remove_scratch_attributes(obj.data)
    # Read the channels back off the mesh, area-weighted, so the report is comparable with
    # the rendered tiles. min/max are weighting-independent and are what to assert on; a
    # mean-vs-mean comparison across loop weighting and pixel weighting is invalid.
    report["storedChannels"] = vertexcolor.channel_stats(obj)
    return report, ao_result


# ---------------------------------------------------------------------------
# Orchestration
# ---------------------------------------------------------------------------

def generate(spec: CoralSpec, *, name: Optional[str] = None,
             render_preview: bool = True,
             preview_dir: str = "") -> CoralResult:
    """Full package: geometry, UVs, bakes, channels, LODs, collider, proof renders."""
    asset_name = name or "Coral_Branching_{s:04d}".format(s=spec.seed % 10000)
    blackbox = BlackBox("CoralBranching", "s{s}q{q:02d}".format(
        s=spec.seed, q=int(round(law.saturate(spec.quality) * 100))))

    bpy.ops.wm.read_factory_settings(use_empty=True)
    collection = bpy.data.collections.new("H8_Coral")
    bpy.context.scene.collection.children.link(collection)

    try:
        nodes = build_skeleton(spec, blackbox)
        obj = skeleton_to_object(nodes, spec, asset_name, collection, blackbox)

        bm = mesh_ops.bmesh_from_object(obj)
        mesh_ops.weld_and_clean(bm, blackbox=blackbox)
        mesh_ops.bmesh_to_object(bm, obj)

        sampler = SkeletonSampler.build(nodes)
        refine_surface(obj, spec, sampler, blackbox)
        shading = mesh_ops.apply_shading_basis(
            obj,
            smooth_angle_deg=law.smooth_angle_for(law.SurfaceClass.ORGANIC),
            blackbox=blackbox)
        # Reduce to the LOD0 budget BEFORE unwrapping and baking. Doing it after would
        # throw away the UV layout and vertex colours the following stages author, and
        # doing it never leaves LOD0 32x over the flora ceiling - which is what the first
        # run produced (206880 tris against a 6500 budget).
        mesh_ops.reduce_to_budget(obj, family=law.Family.FLORA, lod_index=0,
                                  blackbox=blackbox)
        uv_report = unwrap_and_assign_materials(obj, spec, blackbox)
        channel_report, ao_result = author_channels(obj, spec, sampler, blackbox)

        lods = mesh_ops.build_lod_chain(
            obj, family=law.Family.FLORA, name=asset_name,
            quality_weight=spec.quality, blackbox=blackbox)

        # Coral collision: flora defaults to none, but section 7 carves out "Large coral
        # blocking path: convex hull under 200 triangles or compound boxes". Only a
        # colony the caller declares path-blocking gets one.
        collider = None
        if spec.large_enough_to_block_path:
            collider = mesh_ops.make_convex_collider(
                lods[0].obj, family=law.Family.GEOLOGY, name=asset_name,
                blackbox=blackbox)
        else:
            collider = mesh_ops.make_convex_collider(
                lods[0].obj, family=law.Family.FLORA, name=asset_name,
                blackbox=blackbox)

        result = CoralResult(
            name=asset_name, lods=lods, collider=collider,
            sway_report=channel_report, ao_report=ao_result,
            node_count=len(nodes),
            tip_count=sum(1 for n in nodes if n.is_tip),
        )

        if render_preview:
            spec_studio = preview.PreviewSpec(
                name=asset_name, output_dir=preview_dir, resolution=512, samples=12,
                surface_class=law.SurfaceClass.ORGANIC, mode="studio",
                views=("front", "three_quarter", "side", "low"))
            studio = preview.render_contact_sheet(lods[0].obj, spec_studio)

            spec_flat = preview.PreviewSpec(
                name=asset_name, output_dir=preview_dir, resolution=512, samples=8,
                surface_class=law.SurfaceClass.ORGANIC, mode="flat",
                views=("front", "three_quarter", "side", "low"))
            flat = preview.render_contact_sheet(lods[0].obj, spec_flat)

            spec_chan = preview.PreviewSpec(
                name=asset_name, output_dir=preview_dir, resolution=512, samples=8,
                surface_class=law.SurfaceClass.ORGANIC)
            channels = preview.render_channel_sheet(lods[0].obj, spec_chan)

            result.preview_paths = (studio.sheet_path, flat.sheet_path,
                                    channels.sheet_path)
            result.channel_stats = tuple(
                preview.measure_channel_png(path) for path in channels.tile_paths)

        return result
    except GenerationAborted:
        raise
    except Exception as error:
        blackbox.note_invalid("generate", "CORAL_GENERATOR_EXCEPTION", str(error))
        dump = blackbox.dump("coral generator raised: " + str(error))
        raise GenerationAborted(
            "coral generation failed: " + str(error), dump_path=dump) from error


def _parse_args(argv: list) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="HECTON-8 branching coral generator")
    parser.add_argument("--seed", type=int, default=1712)
    parser.add_argument("--quality", type=float, default=1.0,
                        help="GlobalQualityWeight, continuous 0..1")
    parser.add_argument("--variants", type=int, default=1)
    parser.add_argument("--height", type=float, default=0.85, help="metres")
    parser.add_argument("--blocking", action="store_true",
                        help="colony is large enough to block a path; emits a convex collider")
    parser.add_argument("--out", type=str, default="")
    parser.add_argument("--no-preview", dest="preview", action="store_false")
    parser.set_defaults(preview=True)
    return parser.parse_args(argv)


def main() -> None:
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = _parse_args(argv)

    for variant in range(max(1, args.variants)):
        spec = CoralSpec(
            seed=args.seed + variant * 7919,
            quality=args.quality,
            height_m=args.height,
            large_enough_to_block_path=args.blocking,
        )
        result = generate(spec, render_preview=args.preview, preview_dir=args.out)

        print("=" * 78)
        print("CORAL {n}  seed={s} quality={q:.2f}".format(
            n=result.name, s=spec.seed, q=spec.quality))
        print("  skeleton nodes={n} tips={t}".format(n=result.node_count,
                                                    t=result.tip_count))
        for level in result.lods:
            print("  LOD{i} tris={t} budget={b} within={w}".format(
                i=level.index, t=level.triangles, b=level.budget,
                w=level.within_budget))
        if result.collider is not None:
            print("  collider kind={k} tris={t} within={w} reason={r}".format(
                k=result.collider.kind, t=result.collider.triangles,
                w=result.collider.within_budget, r=result.collider.reason or "-"))
        ao = result.ao_report
        if ao is not None:
            print("  AO baked={b} min={lo:.3f} max={hi:.3f} mean={m:.3f} contrast={c}".format(
                b=ao.baked, lo=ao.min_value, hi=ao.max_value, m=ao.mean_value,
                c=ao.has_contrast))
            if not ao.baked:
                print("  AO FAILURE: " + ao.reason)
        print("  sway min={lo:.4f} max={hi:.4f} uniform={u} (rigid band cap={c:.4f})".format(
            lo=result.sway_report.get("swayMin", -1),
            hi=result.sway_report.get("swayMax", -1),
            u=result.sway_report.get("swayUniform"),
            c=law.SWAY_RIGID_MINERAL_MAX if spec.mineralised else 1.0))
        stored = result.sway_report.get("storedChannels", {})
        if stored.get("present"):
            print("  STORED  areaWeightedMean=%s" % stored["areaWeightedMean"])
            print("  STORED  min=%s max=%s" % (stored["min"], stored["max"]))
        for stats in result.channel_stats:
            print("  CHAN {c:<44} min={lo:.3f} max={hi:.3f} mean={m:.3f} "
                  "cover={cv:.3f} gradient={g} visible={v}".format(
                      c=stats.channel, lo=stats.min_value, hi=stats.max_value,
                      m=stats.mean_value, cv=stats.coverage_fraction,
                      g=stats.has_gradient, v=stats.subject_visible))
        for path in result.preview_paths:
            print("  PREVIEW " + path)
    print("CORAL_GENERATOR_DONE")


if __name__ == "__main__":
    main()
