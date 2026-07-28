"""Geometry operations that enforce HECTON-8 mesh law.

Everything here delegates the actual numerical work to Blender's battle-tested
operators rather than reimplementing bevel/decimation math, which is the entire
reason an external DCC is sanctioned by ``3dmodel.md`` section 0:

    "All mesh generation, texture synthesis, UV unwrapping, tangent construction,
     normal baking, atlas packing, collider fitting, LOD decimation, and prefab
     assembly MUST occur only in Unity Editor tooling or external offline DCC/bake
     tools."

Runs inside Blender 4.5 LTS (Python 3.11). Note the 4.1+ API break: ``use_auto_smooth``
no longer exists on ``Mesh``; angle-based shading is a modifier applied through
``bpy.ops.object.shade_auto_smooth``. Code written against 3.x docs silently does
nothing here, which is exactly the kind of quiet degeneracy this project's rule
files warn about.
"""

from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Iterable, Optional

import bmesh
import bpy
from mathutils import Vector

from . import law
from .blackbox import BlackBox


# ---------------------------------------------------------------------------
# bmesh lifecycle
# ---------------------------------------------------------------------------

def _make_sole_active(obj: bpy.types.Object) -> None:
    """Make ``obj`` the only selected object and the active one.

    ``bpy.ops.object.modifier_apply`` and ``shade_auto_smooth`` are operators, and
    operators read the selection, not just the active pointer. Setting
    ``view_layer.objects.active`` alone leaves the object unselected, at which point
    the operator returns ``{'CANCELLED'}`` and does nothing -- with no exception. That
    is how a decimation loop "runs" six times and leaves the triangle count untouched.
    """
    view_layer = bpy.context.view_layer
    for other in view_layer.objects:
        if other.select_get():
            other.select_set(False)
    obj.select_set(True)
    view_layer.objects.active = obj


def bmesh_from_object(obj: bpy.types.Object) -> bmesh.types.BMesh:
    """Fresh BMesh from an object's evaluated-free mesh data."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    return bm


def bmesh_to_object(bm: bmesh.types.BMesh, obj: bpy.types.Object,
                    free: bool = True) -> None:
    bm.to_mesh(obj.data)
    obj.data.update()
    if free:
        bm.free()


def triangle_count(mesh: bpy.types.Mesh) -> int:
    """Triangles after triangulation, which is what Unity will actually receive.

    ``len(mesh.polygons)`` undercounts quads and n-gons, so budget checks against it
    pass while the imported asset blows the budget. Loop-triangles are the truth.
    """
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


# ---------------------------------------------------------------------------
# Cleaning and welding
# ---------------------------------------------------------------------------

def weld_and_clean(
    bm: bmesh.types.BMesh,
    merge_distance: float = 1e-4,
    *,
    blackbox: Optional[BlackBox] = None,
) -> dict:
    """Merge coincident vertices, drop degenerate geometry, recalculate winding.

    ``3DMODEL_FLORA_CORAL.md`` section 3: "Branch intersections must be blended,
    welded, or explicitly hidden by knuckles. Intersecting tubes with z-fighting are
    rejected." Welding is the mechanical half of satisfying that gate.
    """
    before_v = len(bm.verts)
    before_f = len(bm.faces)

    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=merge_distance)
    bmesh.ops.dissolve_degenerate(bm, dist=merge_distance,
                                  edges=bm.edges[:])
    # Delete faces that survived dissolve but still have no area.
    dead = [f for f in bm.faces if f.calc_area() <= law.DEGENERATE_TRIANGLE_AREA_EPS]
    if dead:
        bmesh.ops.delete(bm, geom=dead, context="FACES")
    # Loose verts and edges never reach the GPU but do reach the validator.
    loose_verts = [v for v in bm.verts if not v.link_faces]
    if loose_verts:
        bmesh.ops.delete(bm, geom=loose_verts, context="VERTS")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])

    stats = {
        "verts_removed": before_v - len(bm.verts),
        "faces_removed": before_f - len(bm.faces),
        "degenerate_faces_deleted": len(dead),
        "loose_verts_deleted": len(loose_verts),
    }
    if blackbox is not None:
        blackbox.record(
            "weld_and_clean",
            vertex_count=len(bm.verts),
            triangle_count=len(bm.faces),
            warning="" if not dead else "deleted {n} zero-area faces".format(n=len(dead)),
        )
    return stats


# ---------------------------------------------------------------------------
# Bevel  --  3dmodel.md section 4, "Hard-Surface Engineering Law"
# ---------------------------------------------------------------------------

@dataclass
class BevelResult:
    edges_considered: int
    edges_beveled: int
    width_m: float
    segments: int
    clamped: bool


def _shortest_adjacent_edge_length(edge: bmesh.types.BMEdge) -> float:
    """Length of the shortest edge sharing a vertex with ``edge``.

    Feeds the bible's overlap clamp: "Clamp bevel width to 20 percent of the
    shortest adjacent edge to prevent self-overlap."
    """
    shortest = edge.calc_length()
    for vert in edge.verts:
        for other in vert.link_edges:
            if other is edge:
                continue
            length = other.calc_length()
            if length < shortest:
                shortest = length
    return shortest


def select_hard_edges(
    bm: bmesh.types.BMesh,
    angle_threshold_deg: float = law.BEVEL_ANGLE_THRESHOLD_DEG,
) -> list:
    """Manifold edges whose adjacent face angle exceeds the threshold.

    Implements steps 1-3 of the bible's required algorithm: build the edge/face
    adjacency, reject zero-area faces, and compute
    ``angle = acos(clamp(dot(n0, n1), -1, 1))``.

    Boundary edges are excluded: they have one adjacent face, so there is no
    dihedral angle to bevel, and beveling them produces a rim that the flora bible
    wants authored deliberately rather than as a side effect.
    """
    threshold = math.radians(angle_threshold_deg)
    hard = []
    for edge in bm.edges:
        faces = edge.link_faces
        if len(faces) != 2:
            continue
        f0, f1 = faces[0], faces[1]
        if (f0.calc_area() <= law.DEGENERATE_TRIANGLE_AREA_EPS or
                f1.calc_area() <= law.DEGENERATE_TRIANGLE_AREA_EPS):
            continue
        dot = max(-1.0, min(1.0, f0.normal.dot(f1.normal)))
        if math.acos(dot) > threshold:
            hard.append(edge)
    return hard


def bevel_hard_edges(
    bm: bmesh.types.BMesh,
    *,
    family: law.Family,
    quality_weight: float,
    hero: bool = False,
    bevel_range: Optional[law.BevelRange] = None,
    angle_threshold_deg: float = law.BEVEL_ANGLE_THRESHOLD_DEG,
    blackbox: Optional[BlackBox] = None,
) -> BevelResult:
    """Chamfer every visible hard edge. Never a no-op on hard-surface families.

    ``3dmodel.md`` section 4 bans 90-degree mathematical corners outright: "A perfect
    cube edge is one infinitely sharp normal discontinuity, so it either renders as a
    dead black cut or a razor highlight that exposes the mesh as programmer output."

    Width comes from the family range scaled by ``GlobalQualityWeight``, then clamped
    per-edge to 20% of the shortest adjacent edge. The clamp is global here (one
    offset for the whole bevel op) and uses the most restrictive edge in the
    selection, because a single bmesh bevel call takes one offset; picking the
    minimum is the only choice that cannot self-intersect anywhere.
    """
    if bevel_range is None:
        bevel_range = law.BEVEL_RANGES.get(family)
    if bevel_range is None:
        raise ValueError(
            "no bevel range for family " + str(family) +
            "; pass bevel_range explicitly (e.g. law.BEVEL_RANGE_INTERIOR_TRIM)"
        )

    hard = select_hard_edges(bm, angle_threshold_deg)
    considered = len(bm.edges)
    if not hard:
        if blackbox is not None:
            blackbox.record("bevel_hard_edges", warning="no hard edges found")
        return BevelResult(considered, 0, 0.0, 0, False)

    requested = bevel_range.width_for(quality_weight)
    segments = law.bevel_segments_for(quality_weight, hero=hero)

    limit = min(_shortest_adjacent_edge_length(e) for e in hard)
    max_allowed = limit * law.BEVEL_WIDTH_CLAMP_RATIO
    width = min(requested, max_allowed)
    clamped = width < requested

    # harden_normals requires the result to be shaded smooth; the caller applies
    # angle shading afterwards, so we keep normals soft here and let the weighted
    # normal pass own the final basis. Hardening twice fights itself.
    bmesh.ops.bevel(
        bm,
        geom=hard,
        offset=width,
        offset_type="OFFSET",
        segments=segments,
        profile=0.5,
        affect="EDGES",
        clamp_overlap=True,
        material=-1,
    )

    if blackbox is not None:
        blackbox.record(
            "bevel_hard_edges",
            vertex_count=len(bm.verts),
            triangle_count=len(bm.faces),
            warning=("clamped {w:.4f}m from requested {r:.4f}m".format(w=width, r=requested)
                     if clamped else ""),
        )
    return BevelResult(considered, len(hard), width, segments, clamped)


# ---------------------------------------------------------------------------
# Shading basis  --  3dmodel.md section 4, smoothing groups + weighted normals
# ---------------------------------------------------------------------------

def apply_shading_basis(
    obj: bpy.types.Object,
    *,
    smooth_angle_deg: float = law.SMOOTH_ANGLE_DEG,
    weighted: bool = True,
    keep_sharp: bool = True,
    blackbox: Optional[BlackBox] = None,
) -> None:
    """Angle-based smoothing plus area/angle weighted normals.

    The bible's formula:
        ``weightedNormal(v, group) = normalize(sum(faceNormal[i] * faceArea[i] * cornerAngleWeight[i]))``
    is exactly Blender's ``WEIGHTED_NORMAL`` modifier with ``mode='FACE_AREA_WITH_ANGLE'``,
    so we use it instead of hand-rolling a fold that would need its own proof.

    Blender 4.1 removed ``Mesh.use_auto_smooth``. ``shade_auto_smooth`` adds the
    "Smooth by Angle" modifier, which is the supported route in 4.5. Setting the old
    attribute here would raise, and guarding it with ``hasattr`` would silently skip
    shading -- both worse than using the current API directly.
    """
    view_layer = bpy.context.view_layer
    previous_active = view_layer.objects.active
    _make_sole_active(obj)

    bpy.ops.object.shade_auto_smooth(angle=math.radians(smooth_angle_deg))

    if weighted:
        modifier = obj.modifiers.new(name="H8_WeightedNormal", type="WEIGHTED_NORMAL")
        modifier.mode = "FACE_AREA_WITH_ANGLE"
        modifier.weight = 50
        modifier.keep_sharp = keep_sharp
        bpy.ops.object.modifier_apply(modifier=modifier.name)

    if previous_active is not None:
        view_layer.objects.active = previous_active
    if blackbox is not None:
        blackbox.record(
            "apply_shading_basis",
            vertex_count=len(obj.data.vertices),
            triangle_count=triangle_count(obj.data),
        )


# ---------------------------------------------------------------------------
# LOD chain  --  3dmodel.md section 7
# ---------------------------------------------------------------------------

@dataclass
class LodLevel:
    index: int
    obj: bpy.types.Object
    triangles: int
    budget: int
    ratio_used: float

    @property
    def within_budget(self) -> bool:
        return self.triangles <= self.budget


def reduce_to_budget(
    obj: bpy.types.Object,
    *,
    family: law.Family,
    lod_index: int = 0,
    headroom: float = 0.94,
    blackbox: Optional[BlackBox] = None,
) -> int:
    """Decimate an object down to its LOD budget, returning the final triangle count.

    Needed because the correct authoring route for organic surfaces is high-density
    sculpt THEN reduce: displacing a mesh that is already at budget resolution produces
    mush, while displacing a subdivided mesh and decimating afterwards keeps the
    silhouette the displacement created. ``3dmodel.md`` section 7 names Quadric Edge
    Collapse as the default allowed algorithm for exactly this.

    ``headroom`` leaves a small margin under the ceiling so a later triangulation or
    seam split cannot push a compliant asset back over it.
    """
    budget = law.LOD_BUDGETS[family].limit(lod_index)
    target = max(4, int(budget * max(0.1, min(1.0, headroom))))
    start = triangle_count(obj.data)

    for _attempt in range(8):
        current = triangle_count(obj.data)
        if current <= target:
            break
        modifier = obj.modifiers.new(name="H8_BudgetDecimate", type="DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = max(0.01, min(0.99, (target / float(current)) * 0.96))
        modifier.use_collapse_triangulate = True
        _make_sole_active(obj)
        bpy.ops.object.modifier_apply(modifier=modifier.name)

    final = triangle_count(obj.data)
    if blackbox is not None:
        blackbox.record(
            "reduce_to_budget", family=family.value, triangle_count=final,
            vertex_count=len(obj.data.vertices),
            warning="" if final <= budget else
            "still over budget {f}>{b} from {s}".format(f=final, b=budget, s=start),
            failure_code="" if final <= budget else "BUDGET_UNREACHABLE",
        )
    return final


def _split_uv_seams(obj: bpy.types.Object) -> int:
    """Split the mesh along UV seams and sharp edges so decimation preserves them.

    ``3dmodel.md`` section 7 requires: "Decimation must preserve boundary edges, UV
    seams, hard normals, material borders, sockets, attachment points, silhouette
    curvature, and collision proxy fit."

    Blender's Decimate/COLLAPSE has no seam-preservation flag -- it preserves mesh
    BOUNDARIES. Splitting seam and sharp edges converts them into boundaries, which
    is the mechanism that makes the bible's requirement actually hold instead of
    being asserted in a comment. The duplicated vertices along the seam are not
    waste: any UV-seamed mesh must duplicate them on export anyway, so this matches
    what Unity will receive regardless.

    Returns the number of edges split.
    """
    bm = bmesh_from_object(obj)
    targets = set()
    for edge in bm.edges:
        if edge.seam or not edge.smooth:
            targets.add(edge)
    # Material borders are silhouette-relevant too: a collapse across a slot border
    # drags one material's geometry into another's island.
    for edge in bm.edges:
        faces = edge.link_faces
        if len(faces) == 2 and faces[0].material_index != faces[1].material_index:
            targets.add(edge)
    count = len(targets)
    if count:
        bmesh.ops.split_edges(bm, edges=list(targets))
    bmesh_to_object(bm, obj)
    return count


def build_lod_chain(
    source: bpy.types.Object,
    *,
    family: law.Family,
    name: str,
    quality_weight: float = 1.0,
    levels: int = 3,
    preserve_seams: bool = True,
    blackbox: Optional[BlackBox] = None,
) -> list:
    """Produce LOD0..LOD(levels-1) as separate objects, each inside its budget.

    LOD0 is the source geometry, only verified against budget -- never decimated,
    because LOD0 is the authored silhouette. LOD1+ use Quadric Edge Collapse, which
    ``3dmodel.md`` names as "the default allowed algorithm for arbitrary meshes".

    Blender's decimate ``ratio`` is an approximate target, so each level iterates:
    apply, measure real triangles, tighten the ratio if still over budget. A single
    blind ratio pass is how generators end up shipping over-budget LODs while their
    logs claim compliance.
    """
    budgets = law.LOD_BUDGETS[family]
    out: list = []

    lod0 = source
    lod0.name = law.NAME_MESH.format(family=family.value, name=name, lod=0)
    lod0.data.name = lod0.name
    tris0 = triangle_count(lod0.data)
    out.append(LodLevel(0, lod0, tris0, budgets.lod0, 1.0))
    if blackbox is not None:
        blackbox.record("lod0", family=family.value, triangle_count=tris0,
                        vertex_count=len(lod0.data.vertices),
                        warning="" if tris0 <= budgets.lod0 else
                        "LOD0 over budget {t}>{b}".format(t=tris0, b=budgets.lod0))

    # Each level is derived from the PREVIOUS level's real triangle count, not from
    # its own budget. Targeting the budget alone produces a non-monotonic chain
    # whenever LOD0 already sits far under budget: LOD1 gets aggressively reduced,
    # LOD2 sees itself already under its looser budget and skips decimation
    # entirely, and the far LOD ships heavier than the near one. 3dmodel.md section 7
    # requires LOD1 to be a reduction of LOD0 and LOD2 a reduction of LOD1, so the
    # chain is relative by construction.
    previous_tris = tris0

    for index in range(1, levels):
        budget = budgets.limit(index)
        clone = lod0.copy()
        clone.data = lod0.data.copy()
        clone.name = law.NAME_MESH.format(family=family.value, name=name, lod=index)
        clone.data.name = clone.name
        source.users_collection[0].objects.link(clone)

        if preserve_seams:
            _split_uv_seams(clone)

        # Retention per step, scaled by GlobalQualityWeight. LOD1 keeps silhouette
        # plus most material zones; LOD2 keeps mass and anchor shape only. Quality
        # raises retained density inside the step but can never lift it to 1.0, so
        # monotonicity does not depend on the weight.
        base_retain = 0.55 if index == 1 else 0.35
        retain = base_retain + 0.15 * law.saturate(quality_weight)
        target = int(previous_tris * retain)
        # Strictly below the previous level whenever there is anything left to cut.
        if previous_tris > 8:
            target = min(target, previous_tris - 1)
        target = max(4, min(target, budget))

        ratio_used = 1.0
        for _attempt in range(6):
            current = triangle_count(clone.data)
            if current <= target:
                break
            ratio = max(0.01, min(0.99, (target / float(current)) * 0.96))
            modifier = clone.modifiers.new(name="H8_Decimate", type="DECIMATE")
            modifier.decimate_type = "COLLAPSE"
            modifier.ratio = ratio
            modifier.use_collapse_triangulate = True
            _make_sole_active(clone)
            bpy.ops.object.modifier_apply(modifier=modifier.name)
            ratio_used *= ratio

        final_tris = triangle_count(clone.data)
        out.append(LodLevel(index, clone, final_tris, budget, ratio_used))

        problems = []
        if final_tris > budget:
            problems.append("over budget {t}>{b}".format(t=final_tris, b=budget))
        if final_tris > previous_tris:
            problems.append("non-monotonic: LOD{i}={t} > LOD{p}={pt}".format(
                i=index, t=final_tris, p=index - 1, pt=previous_tris))
        if blackbox is not None:
            blackbox.record(
                "lod{i}".format(i=index),
                family=family.value,
                triangle_count=final_tris,
                vertex_count=len(clone.data.vertices),
                warning="; ".join(problems),
                failure_code="LOD_CHAIN_INVALID" if problems else "",
            )
        previous_tris = final_tris

    return out


# ---------------------------------------------------------------------------
# Collision proxy  --  3dmodel.md section 9
# ---------------------------------------------------------------------------

def _convex_hull_in_place(bm: bmesh.types.BMesh) -> None:
    """Replace the BMesh contents with its triangulated convex hull.

    ``bmesh.ops.convex_hull`` ADDS hull faces without removing the source geometry, and
    its ``geom_interior``/``geom_unused`` reports only cover input the hull did not
    consume. For an already-convex input every vertex IS on the hull, so both lists come
    back empty and the original faces survive underneath the new ones -- giving exactly
    double the triangle count with no error and no leftover to delete.

    Stripping faces and edges down to a bare point cloud first makes the operation
    unconditional: whatever the input topology, the result is the hull of its vertices
    and nothing else.
    """
    if bm.faces:
        bmesh.ops.delete(bm, geom=bm.faces[:], context="FACES_ONLY")
    if bm.edges:
        bmesh.ops.delete(bm, geom=bm.edges[:], context="EDGES_FACES")
    result = bmesh.ops.convex_hull(bm, input=bm.verts[:], use_existing_faces=False)
    leftovers = list(result.get("geom_interior", [])) + list(result.get("geom_unused", []))
    if leftovers:
        bmesh.ops.delete(bm, geom=leftovers, context="VERTS")
    if bm.faces:
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])


@dataclass
class ColliderResult:
    obj: Optional[bpy.types.Object]
    triangles: int
    kind: str
    within_budget: bool
    reason: str = ""


def make_convex_collider(
    source: bpy.types.Object,
    *,
    family: law.Family,
    name: str,
    blackbox: Optional[BlackBox] = None,
) -> ColliderResult:
    """Convex hull proxy under the 200-triangle ceiling, as a separate datablock.

    ``3dmodel.md`` section 9 is absolute: "LOD0 visual meshes must never be assigned
    directly to production MeshCollider components. ... Rocks/coral/complex geology:
    convex hull or convex decomposition under 200 triangles total per asset."

    Flora families return no collider by default -- ``3DMODEL_FLORA_CORAL.md``
    section 7: "Default flora/coral collision is none." Returning an empty result
    with a stated reason is the honest outcome; silently emitting a collider anyway
    would put decorative fronds into the physics scene.
    """
    if family in law.FAMILIES_WITHOUT_DEFAULT_COLLISION:
        if blackbox is not None:
            blackbox.record("collider_skipped", family=family.value,
                            warning="flora default: no collision (bible section 7)")
        return ColliderResult(None, 0, "none", True,
                              "flora/coral default collision is none per "
                              "3DMODEL_FLORA_CORAL.md section 7")

    bm = bmesh_from_object(source)
    _convex_hull_in_place(bm)

    hull_mesh = bpy.data.meshes.new(
        law.NAME_COLLIDER.format(family=family.value, name=name))
    bm.to_mesh(hull_mesh)
    bm.free()

    collider = bpy.data.objects.new(
        law.COLLIDER_PREFIX + "{f}_{n}".format(f=family.value, n=name), hull_mesh)
    source.users_collection[0].objects.link(collider)

    # Read ``collider.data`` on every pass, never a captured mesh reference:
    # ``modifier_apply`` rebinds the object's mesh datablock, so a variable captured
    # before the loop keeps pointing at the pre-decimation mesh. The loop then runs to
    # exhaustion measuring geometry that nobody is modifying, reports FINISHED every
    # time, and leaves the collider at full hull density.
    hull_tris = triangle_count(collider.data)
    attempts = 0
    for _attempt in range(8):
        tris = triangle_count(collider.data)
        if tris <= law.COLLIDER_CONVEX_TRI_MAX:
            break
        attempts += 1
        modifier = collider.modifiers.new(name="H8_HullDecimate", type="DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = max(0.02, min(0.95,
                             (law.COLLIDER_CONVEX_TRI_MAX / float(tris)) * 0.92))
        modifier.use_collapse_triangulate = True
        _make_sole_active(collider)
        bpy.ops.object.modifier_apply(modifier=modifier.name)

    # Decimation breaks convexity: collapsing a hull edge can pull a vertex inside the
    # shell. PhysX needs the proxy convex, so re-hull after reduction and judge the
    # budget only afterwards. Skipping this ships a "convex" collider that is not.
    if attempts:
        bm = bmesh.new()
        bm.from_mesh(collider.data)
        _convex_hull_in_place(bm)
        bm.to_mesh(collider.data)
        bm.free()

    tris = triangle_count(collider.data)
    within = tris <= law.COLLIDER_CONVEX_TRI_MAX
    if blackbox is not None:
        blackbox.record(
            "collider_convex", family=family.value, triangle_count=tris,
            vertex_count=len(collider.data.vertices),
            warning="" if within else
            "collider over budget {t}>{m} (hull was {h}, {a} decimation passes)".format(
                t=tris, m=law.COLLIDER_CONVEX_TRI_MAX, h=hull_tris, a=attempts),
            failure_code="" if within else "COLLIDER_OVER_BUDGET",
        )
    return ColliderResult(
        collider, tris, "convex", within,
        "" if within else
        "hull {h} tris reduced to {t} over {a} passes, still above the {m} ceiling".format(
            h=hull_tris, t=tris, a=attempts, m=law.COLLIDER_CONVEX_TRI_MAX))


# ---------------------------------------------------------------------------
# Bounds
# ---------------------------------------------------------------------------

def local_bounds(obj: bpy.types.Object) -> tuple:
    """(min, max) as Vectors in object local space. Empty mesh yields zero vectors."""
    if not obj.data.vertices:
        return (Vector((0.0, 0.0, 0.0)), Vector((0.0, 0.0, 0.0)))
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]
    return (Vector((min(xs), min(ys), min(zs))), Vector((max(xs), max(ys), max(zs))))


def longest_extent(obj: bpy.types.Object) -> float:
    lo, hi = local_bounds(obj)
    return max(hi.x - lo.x, hi.y - lo.y, hi.z - lo.z)
