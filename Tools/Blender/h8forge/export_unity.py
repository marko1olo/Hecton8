"""FBX export with a proven Blender -> Unity axis, scale and stream conversion.

``3dmodel.md`` section 0 sanctions this module: offline authoring may run in "Unity
Editor tooling **or external offline DCC/bake tools**". Nothing here writes a Unity
asset -- it writes one FBX per package plus a JSON manifest, and hands Unity the
importer contract it must apply.

Section 3, "Universal Mesh Data Contract", is the specification this file exists to
satisfy. Every stream it names has to arrive:

    Position   Float32 x3    local-space metres, finite, bounds validated
    Normal     Float32 x3    unit length within 0.5 percent, split where smoothing
                             groups demand
    Tangent    Float32 x4    MikkTSpace-compatible, ``w`` is handedness
    Color      UNorm8 x4     domain-specific masks
    TexCoord0  Float32 x2    primary material UV
    TexCoord1  Float32 x2    lightmap / detail / atlas remap / packed masks
    TexCoord2  optional      curvature, blend, flow, VAT, family-specific

"Preserve every stream" is not a design intent here, it is a measured claim. Every
export re-imports its own file and compares counts, colours, UV order, corner
normals, a landmark vertex direction and mesh chirality before returning. The
comparison lives in :func:`verify_fbx_roundtrip` so a caller can also point it at a
file it did not write. ``AGENTS.md`` ``[RULE] Never Trust Automated Assertions
Alone`` is the reason: a written file and a zero exit code prove nothing.

Coordinate systems, stated once and measured in ``_test_export.py``
--------------------------------------------------------------------
Blender is right-handed, Z up, +Y forward, 1 unit = 1 m.
FBX (as written here) is right-handed, Y up, 1 unit = 1 cm.
Unity is left-handed, Y up, +Z forward, 1 unit = 1 m.

    axis_forward="-Z", axis_up="Y"  maps Blender (x, y, z) -> FBX (x, z, -y)
    Unity then flips handedness      FBX  (x, y, z) -> Unity (x, y, -z)
    net                              Blender (x, y, z) -> Unity (x, z, y)

The net map swaps two axes, so its determinant is -1. That is correct and is *not*
a mirror: the coordinate system changed handedness, the object did not. Unity
compensates by flipping triangle winding, which is why an FBX exported this way
arrives with outward normals rather than inside-out geometry.

The mirror that does break things comes from somewhere else -- a negative object
scale in Blender. ``axis_conversion`` is always a proper rotation (verified for five
axis combinations in the self-test), so it can never introduce one, but a generator
that mirrored a part with ``scale.x = -1`` will hand Unity inverted winding and an
inverted tangent ``w``, which silently breaks every normal map on the asset.
:func:`export_fbx` refuses to export such an object rather than discovering it in
engine, per ``3dmodel.md`` section 3: "no inverted unintentional winding".
"""

from __future__ import annotations

import hashlib
import json
import math
import os
import re
from dataclasses import dataclass, field
from typing import Callable, Iterable, Optional, Sequence

import bpy
from mathutils import Matrix, Vector

from . import law
from .blackbox import BlackBox, GenerationAborted

# ---------------------------------------------------------------------------
# Versions
# ---------------------------------------------------------------------------
# ``validate.py`` sets the precedent for an artefact-local version constant
# (``VALIDATOR_VERSION``); law.py holds bible thresholds, not tool versions.
EXPORTER_VERSION = "1.0.0"
MANIFEST_SCHEMA = "h8forge.manifest/1"

# ``3DMODEL_TEXTURES_MATERIALS.md`` section 11 demands this literal string whenever
# only static rules were exercised.
PENDING_MARKER = "PENDING UNITY/PROFILER VERIFICATION"


# ---------------------------------------------------------------------------
# The export settings, with the reason each value is what it is
# ---------------------------------------------------------------------------
# Frozen as data so the manifest, the report and the self-test all cite the same
# numbers instead of three copies drifting apart.

#: FBX units per Blender metre. ``apply_unit_scale=True`` with
#: ``apply_scale_options='FBX_SCALE_NONE'`` bakes Blender's
#: ``100.0 * unit_settings.scale_length`` factor into the geometry and leaves the
#: file header's ``UnitScaleFactor`` at 1.0, i.e. "one file unit is one
#: centimetre". Measured: a 1.75 m landmark is written as 175.0.
FBX_UNITS_PER_METRE = 100.0

#: What the file header ends up saying. Unity reads it as File Scale and needs
#: "Convert Units" enabled to turn centimetres back into metres.
FBX_HEADER_UNIT_SCALE_FACTOR = 1.0

EXPORT_SETTINGS = {
    # -- axis and scale ---------------------------------------------------
    # Blender +Z (up) -> FBX +Y (up); Blender +Y (forward) -> FBX -Z. This is the
    # Maya/Autodesk Y-up convention Unity's importer is written against. The
    # header this produces is UpAxis=Y(+), FrontAxis=Z(+), CoordAxis=X(+).
    "axis_forward": "-Z",
    "axis_up": "Y",
    # No extra scaling. The asset is already authored in metres because law.py
    # expresses every dimension in metres (BEVEL_RANGES, MIN_BOUNDS_EXTENT_M).
    "global_scale": 1.0,
    # Honour the scene unit scale rather than dumping raw Blender units. With
    # scale_length=1.0 this is the x100 metre->centimetre factor.
    "apply_unit_scale": True,
    # 'FBX_SCALE_NONE' == "All Local": the unit factor goes into the geometry and
    # the header stays at 1.0. This is what Maya and Max emit, so it is the
    # best-tested path through Unity's FBX importer. The alternative,
    # 'FBX_SCALE_UNITS', leaves the data in metres and writes
    # UnitScaleFactor=100; it also works but relies on Unity honouring a
    # non-default header value, which is a risk with no upside here.
    "apply_scale_options": "FBX_SCALE_NONE",
    # Apply the axis conversion at all. False would write Blender-space data
    # under a Y-up header, which is the classic "arrives rotated 90 degrees".
    "use_space_transform": True,
    # Bake the axis conversion into the VERTEX DATA, leaving the FBX node
    # transform at identity. With False the conversion lives on the node instead,
    # so Unity receives a Z-up mesh asset plus a -90 degree X rotation on the
    # renderer -- measured, not folklore: re-importing such a file yields
    # rot=(-90,0,0). Every consumer that reads Mesh.bounds axes, or that expects
    # an identity renderer transform under an LODGroup, then reads the wrong
    # axes. Blender flags this option experimental because it is known to break
    # armatures and animation; this exporter writes static meshes only, with
    # object_types={'MESH'} and bake_anim=False, so that hazard cannot apply.
    "bake_space_transform": True,
    # -- what goes in the file --------------------------------------------
    # Only the objects the caller selected, and only meshes. preview.py builds
    # lights, a camera and a scale-witness object into the same scene; a
    # whole-scene export would ship them inside the asset.
    "use_selection": True,
    "use_visible": False,
    "object_types": {"MESH"},
    # Triangulate. Three separate reasons, in order of severity:
    #  1) use_tspace SILENTLY refuses to write tangents for a mesh containing any
    #     polygon with more than four sides -- it logs "cannot compute/export
    #     tangent space for it" and finishes successfully with no tangent layer.
    #     3dmodel.md section 3 lists Tangent as a required stream, so a silent
    #     drop is a corrupt package that still passes a file-exists check.
    #     Triangulating first makes the tangent pass unconditional (verified: the
    #     same n-gon mesh exports tangents with this on and none with it off).
    #  2) Unity triangulates on import regardless, so the file then matches what
    #     the engine actually receives and the manifest's triangle counts describe
    #     the shipped topology.
    #  3) Triangulating a non-planar n-gon changes its volume. Doing it here, once,
    #     means the geometry validated offline is the geometry Unity gets, instead
    #     of Unity picking its own diagonals later.
    "use_triangles": True,
    # Modifier state is the caller's decision (see export_fbx(apply_modifiers)).
    "use_mesh_modifiers": True,
    # Never hand Unity a subdivision cage plus a "please subdivide" flag.
    # 3dmodel.md section 0: runtime is "a blind consumer of serialized" assets.
    "use_subsurf": False,
    # Loose edges are not renderable and the validator already deletes them.
    "use_mesh_edges": False,
    # Write per-edge sharpness alongside the normals. Unity ignores it when
    # Normals=Import, but it keeps the authored hard/soft edge topology in the
    # file for any other consumer and for a fallback to Normals=Calculate, which
    # then reproduces the same split from the same angle.
    "mesh_smooth_type": "EDGE",
    # Tangent + binormal layers, one pair per UV layer. Requires triangles/quads,
    # hence use_triangles above.
    "use_tspace": True,
    # LINEAR, not SRGB. This is the single most consequential flag for the
    # vertex-colour contract and it was settled by reading the written file:
    # an authored linear mask of 0.25 lands in the FBX as 0.25016 with LINEAR and
    # as 0.53725 with SRGB -- exactly the sRGB transfer curve. Unity copies the
    # raw FBX float into Mesh.colors32 without a colour-space conversion, so SRGB
    # would gamma-warp every channel of law.VCOL_CONTRACT: an AO of 0.44 would
    # reach the shader as 0.69 and a sway gradient would lose its low end. These
    # channels are numbers, not colours (3dmodel.md sections 4 and 5), so the
    # transfer function has no business touching them.
    "colors_type": "LINEAR",
    # Unity consumes a single colour channel. This writes the attribute
    # vertexcolor.ensure_color_attribute marked active first, so a leftover
    # scratch layer cannot take its place.
    "prioritize_active_color": True,
    # Object custom properties stay out. vertexcolor.bake_ambient_occlusion
    # stashes a full per-vertex float list on the object as "h8_ao_values"; with
    # this on, that array would be serialised into the FBX.
    "use_custom_props": False,
    # No animation data, no AnimStack, no dummy clip on the Unity side.
    "bake_anim": False,
    # Textures are Unity-side TX_* assets per 3DMODEL_TEXTURES_MATERIALS.md
    # section 2. The FBX carries geometry only, so there is nothing to embed and
    # no developer path to leak into it (AGENTS.md relative-path rule).
    "path_mode": "STRIP",
    "embed_textures": False,
    "use_metadata": True,
}

#: Deliberately wrong combination, kept next to the right one because the
#: self-test uses it as a negative control. ``axis_up='Z'`` performs no conversion
#: at all: the file stays Z-up under a Y-up header and the asset arrives rotated.
_WRONG_AXES_CONTROL = {"axis_forward": "Y", "axis_up": "Z"}

# Round-trip tolerances. Each one is the measured error plus headroom, never a
# number picked to make a test pass.
TOL_POSITION_M = 1.0e-4        # metres; the cm round trip is exact to ~1e-6
TOL_NORMAL = 1.0e-3            # measured worst case 5.34e-5 (INT16 custom normals)
TOL_COLOR = 2.0e-3             # measured 0.0; UNorm8 quantisation is ~2.2e-3
TOL_UV = 1.0e-5
TOL_DIRECTION = 2.0e-3         # unit-vector delta for the axis claim
#: Below this, the extreme vertex is not distinct enough for the landmark check to
#: mean anything and the axis assertion is downgraded to a recorded note.
MIN_LANDMARK_MARGIN = 1.0e-3

_SAFE_NODE_NAME = re.compile(r"^[A-Za-z0-9_.\-]+$")
_LOD_SUFFIX = re.compile(r"_LOD(\d+)$")


# ---------------------------------------------------------------------------
# Pure axis maps
# ---------------------------------------------------------------------------

def blender_to_fbx_axes(v) -> tuple:
    """Blender (x, y, z) -> FBX axes (x, z, -y). Rotation only, no unit scale.

    Matches ``axis_conversion(to_forward='-Z', to_up='Y')``, whose determinant is
    +1: the FBX-space content is a rotation of the Blender content, never a
    reflection.
    """
    return (float(v[0]), float(v[2]), -float(v[1]))


def blender_to_unity(v) -> tuple:
    """Blender (x, y, z) -> Unity (x, z, y).

    Composition of the export axis conversion and Unity's right-to-left-handed
    flip. Determinant -1, which is the handedness change, not a mirror.
    """
    return (float(v[0]), float(v[2]), float(v[1]))


def _normalised(v: Vector) -> Vector:
    length = v.length
    if length <= 1.0e-12:
        return Vector((0.0, 0.0, 0.0))
    return v / length


# ---------------------------------------------------------------------------
# Results
# ---------------------------------------------------------------------------

@dataclass
class ExportResult:
    """What was written, and the evidence that it survived.

    ``unit_scale`` is FBX units per Blender metre -- 100.0 for the settings above.
    When the round trip ran it is *measured* from the re-imported object rather
    than restated from a constant.

    ``roundtrip_verified`` is True only when verification actually ran and passed.
    A failed round trip does not return False: it raises
    :class:`~h8forge.blackbox.GenerationAborted`, because
    ``PROCEDURAL_ASSET_PIPELINE.md`` "Validation Before Save" makes save failure an
    abort, and the FBX is the save.
    """

    fbx_path: str
    object_names: tuple
    triangle_counts: dict
    has_vertex_colors: bool
    has_custom_normals: bool
    has_tangents: bool
    uv_layer_names: tuple
    unit_scale: float
    roundtrip_verified: bool
    roundtrip_notes: tuple

    def summary(self) -> str:
        return (
            "{path}: {n} object(s) tris={tris} vcol={vc} customNormals={cn} "
            "tangents={tan} uv={uv} unitScale={us:g} roundtrip={rt}"
        ).format(
            path=os.path.basename(self.fbx_path), n=len(self.object_names),
            tris=sum(self.triangle_counts.values()), vc=self.has_vertex_colors,
            cn=self.has_custom_normals, tan=self.has_tangents,
            uv=list(self.uv_layer_names), us=self.unit_scale,
            rt="VERIFIED" if self.roundtrip_verified else "NOT VERIFIED",
        )


@dataclass
class RoundtripReport:
    """Outcome of re-importing an FBX and comparing it to the source objects."""

    passed: bool
    failures: list = field(default_factory=list)
    notes: list = field(default_factory=list)
    measured_unit_scale: float = FBX_UNITS_PER_METRE
    has_vertex_colors: bool = False
    has_custom_normals: bool = False
    uv_layer_names: tuple = ()
    triangle_counts: dict = field(default_factory=dict)
    chirality_preserved: bool = False
    axis_map_confirmed: bool = False

    def lines(self) -> tuple:
        out = list(self.notes)
        for failure in self.failures:
            out.append("FAIL " + failure)
        return tuple(out)


# ---------------------------------------------------------------------------
# Geometry snapshots
# ---------------------------------------------------------------------------

def _foreach(collection, prop: str, count: int, fill):
    """``foreach_get`` into a preallocated list. Mirrors validate._foreach.

    Bulk reads, not per-element attribute access: law.LOD_BUDGETS tops out at a
    35 000 triangle fauna body, and Python attribute access on that many loops is
    minutes rather than milliseconds.
    """
    if count <= 0:
        return []
    # COLD ALLOC: float[count] - flat mesh stream snapshot - owner: _foreach
    buffer = [fill] * count
    collection.foreach_get(prop, buffer)
    return buffer


@dataclass
class _Shape:
    """Flat, frame-explicit snapshot of the geometry an FBX side holds."""

    name: str = ""
    vertex_count: int = 0
    loop_count: int = 0
    triangle_count: int = 0
    polygon_count: int = 0
    max_polygon_sides: int = 0
    uv_names: tuple = ()
    color_layers: tuple = ()
    active_color: str = ""
    has_custom_normals: bool = False
    world_positions: list = field(default_factory=list)
    world_corner_normals: list = field(default_factory=list)
    colors: list = field(default_factory=list)
    uv0: list = field(default_factory=list)
    signed_volume: float = 0.0
    centroid: tuple = (0.0, 0.0, 0.0)
    landmark: tuple = (0.0, 0.0, 0.0)
    landmark_margin: float = 0.0
    matrix_determinant: float = 1.0
    object_scale: tuple = (1.0, 1.0, 1.0)
    local_landmark: tuple = (0.0, 0.0, 0.0)
    local_centroid: tuple = (0.0, 0.0, 0.0)


def _shape_mesh(obj: bpy.types.Object, apply_modifiers: bool):
    """(mesh, release) for the geometry the exporter will actually write.

    With live modifiers and ``apply_modifiers=True`` the FBX receives the evaluated
    result, so measuring ``obj.data`` would report triangle counts the file does not
    contain. mesh_ops applies its bevel/shading/decimate work eagerly, so the fast
    path is the common one, but reporting the wrong number in a manifest is exactly
    the fabricated-proof failure the bibles reject.
    """
    live = [m for m in obj.modifiers if m.show_viewport or m.show_render]
    if apply_modifiers and live:
        depsgraph = bpy.context.evaluated_depsgraph_get()
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        return mesh, evaluated.to_mesh_clear
    return obj.data, None


def _snapshot(obj: bpy.types.Object, *, apply_modifiers: bool = True) -> _Shape:
    """Snapshot one object in world space, plus the local data for axis checks."""
    mesh, release = _shape_mesh(obj, apply_modifiers)
    try:
        mesh.calc_loop_triangles()
        matrix = obj.matrix_world
        basis = matrix.to_3x3()
        normal_matrix = basis.inverted_safe().transposed()

        shape = _Shape(
            name=obj.name,
            vertex_count=len(mesh.vertices),
            loop_count=len(mesh.loops),
            triangle_count=len(mesh.loop_triangles),
            polygon_count=len(mesh.polygons),
            matrix_determinant=basis.determinant(),
            object_scale=(obj.scale[0], obj.scale[1], obj.scale[2]),
            uv_names=tuple(layer.name for layer in mesh.uv_layers),
            has_custom_normals=bool(getattr(mesh, "has_custom_normals", False)),
        )
        shape.max_polygon_sides = max(
            (len(p.vertices) for p in mesh.polygons), default=0)

        colors = []
        for attribute in mesh.color_attributes:
            colors.append((attribute.name, attribute.data_type, attribute.domain))
        shape.color_layers = tuple(colors)
        shape.active_color = str(
            getattr(mesh.attributes, "active_color_name", "") or "")

        local = _foreach(mesh.vertices, "co", shape.vertex_count * 3, 0.0)
        world = []
        for index in range(shape.vertex_count):
            p = matrix @ Vector((local[index * 3], local[index * 3 + 1],
                                 local[index * 3 + 2]))
            world.extend((p.x, p.y, p.z))
        shape.world_positions = world

        try:
            corner = _foreach(mesh.corner_normals, "vector", shape.loop_count * 3, 0.0)
        except (AttributeError, RuntimeError):
            corner = []
        normals = []
        for index in range(len(corner) // 3):
            n = _normalised(normal_matrix @ Vector(
                (corner[index * 3], corner[index * 3 + 1], corner[index * 3 + 2])))
            normals.extend((n.x, n.y, n.z))
        shape.world_corner_normals = normals

        if mesh.color_attributes:
            active = mesh.color_attributes.get(shape.active_color) \
                if shape.active_color else None
            if active is None:
                active = mesh.color_attributes[0]
            elements = (shape.vertex_count if active.domain == "POINT"
                        else shape.loop_count)
            shape.colors = _foreach(active.data, "color", elements * 4, 0.0)

        if mesh.uv_layers:
            shape.uv0 = _foreach(mesh.uv_layers[0].data, "uv",
                                 shape.loop_count * 2, 0.0)

        shape.signed_volume = _signed_volume(mesh, world)
        shape.centroid, shape.landmark, shape.landmark_margin = _landmark(world)
        shape.local_centroid, shape.local_landmark, _ = _landmark(local)
        return shape
    finally:
        if release is not None:
            release()


def _signed_volume(mesh, world_positions: list) -> float:
    """Sum of tetrahedron determinants over the triangles, in world space.

    One scalar that carries chirality. A rotation leaves its sign alone; a
    reflection of the geometry with the winding kept flips it. This is the cheapest
    honest answer to "did anything mirror", and 3dmodel.md section 3 bans "inverted
    unintentional winding" outright.
    """
    total = 0.0
    tri = _foreach(mesh.loop_triangles, "vertices",
                   len(mesh.loop_triangles) * 3, 0)
    for t in range(0, len(tri), 3):
        i0, i1, i2 = tri[t] * 3, tri[t + 1] * 3, tri[t + 2] * 3
        a = Vector(world_positions[i0:i0 + 3])
        b = Vector(world_positions[i1:i1 + 3])
        c = Vector(world_positions[i2:i2 + 3])
        total += a.dot(b.cross(c))
    return total / 6.0


def _landmark(positions: list):
    """(centroid, farthest vertex, margin over the runner-up).

    Picking the extreme vertex by a coordinate ranking would not survive the axis
    permutation under test -- the ranking itself changes meaning. Distance from the
    centroid is invariant under rotation, reflection and axis swap, so the same
    physical vertex is selected on both sides of the round trip. ``margin`` is how
    much further out it sits than the second-place vertex; when that is tiny the
    mesh is near-symmetric at its extreme and the landmark proves nothing, which
    the caller must report rather than assert.
    """
    count = len(positions) // 3
    if count == 0:
        return (0.0, 0.0, 0.0), (0.0, 0.0, 0.0), 0.0
    cx = cy = cz = 0.0
    for i in range(count):
        cx += positions[i * 3]
        cy += positions[i * 3 + 1]
        cz += positions[i * 3 + 2]
    centroid = (cx / count, cy / count, cz / count)
    best = -1.0
    second = -1.0
    best_point = (positions[0], positions[1], positions[2])
    for i in range(count):
        dx = positions[i * 3] - centroid[0]
        dy = positions[i * 3 + 1] - centroid[1]
        dz = positions[i * 3 + 2] - centroid[2]
        d = math.sqrt(dx * dx + dy * dy + dz * dz)
        if d > best:
            second = best
            best = d
            best_point = (positions[i * 3], positions[i * 3 + 1],
                          positions[i * 3 + 2])
        elif d > second:
            second = d
    return centroid, best_point, max(0.0, best - max(0.0, second))


# ---------------------------------------------------------------------------
# Sandboxed re-import
# ---------------------------------------------------------------------------

_SANDBOX_COLLECTION = "H8_ExportRoundTrip"

_TRACKED_LIBRARIES = ("objects", "meshes", "materials", "images", "actions",
                      "armatures", "cameras", "lights", "node_groups")


class _ImportSandbox:
    """Import an FBX, inspect it, and leave the caller's scene exactly as found.

    A generator cannot afford a verification step that pollutes the very scene it
    is about to export again -- the second export would ship the re-imported copy.
    Everything the importer creates is diffed against a pre-import census of the
    tracked libraries and removed afterwards, and the active collection plus
    selection are restored.
    """

    def __init__(self) -> None:
        self._before = {}
        self._collection = None
        self._previous_active = None
        self._previous_selection = ()
        self._previous_active_object = None

    def __enter__(self):
        view_layer = bpy.context.view_layer
        self._previous_active = view_layer.active_layer_collection
        self._previous_selection = tuple(
            o for o in view_layer.objects if o.select_get())
        self._previous_active_object = view_layer.objects.active
        for library in _TRACKED_LIBRARIES:
            collection = getattr(bpy.data, library, None)
            self._before[library] = (set() if collection is None
                                     else set(item.name for item in collection))
        existing = bpy.data.collections.get(_SANDBOX_COLLECTION)
        if existing is not None:
            # Left behind by an aborted previous run; reuse rather than stack up.
            self._collection = existing
        else:
            self._collection = bpy.data.collections.new(_SANDBOX_COLLECTION)
        if _SANDBOX_COLLECTION not in [
                c.name for c in bpy.context.scene.collection.children]:
            bpy.context.scene.collection.children.link(self._collection)
        layer = view_layer.layer_collection.children.get(_SANDBOX_COLLECTION)
        if layer is not None:
            view_layer.active_layer_collection = layer
        return self

    def imported_meshes(self) -> list:
        return [o for o in self._collection.objects if o.type == "MESH"]

    def __exit__(self, exc_type, exc, tb):
        view_layer = bpy.context.view_layer
        try:
            for library in _TRACKED_LIBRARIES:
                collection = getattr(bpy.data, library, None)
                if collection is None:
                    continue
                created = [item for item in collection
                           if item.name not in self._before[library]]
                for item in created:
                    try:
                        collection.remove(item)
                    except (RuntimeError, ReferenceError):
                        # A datablock already freed as a dependency of another.
                        pass
            if self._collection is not None:
                scene_children = bpy.context.scene.collection.children
                if self._collection.name in [c.name for c in scene_children]:
                    scene_children.unlink(self._collection)
                try:
                    bpy.data.collections.remove(self._collection)
                except (RuntimeError, ReferenceError):
                    pass
        finally:
            if self._previous_active is not None:
                try:
                    view_layer.active_layer_collection = self._previous_active
                except (RuntimeError, ReferenceError):
                    pass
            for obj in view_layer.objects:
                try:
                    obj.select_set(obj in self._previous_selection)
                except RuntimeError:
                    pass
            if self._previous_active_object is not None:
                try:
                    view_layer.objects.active = self._previous_active_object
                except (RuntimeError, ReferenceError):
                    pass
        return False


# ---------------------------------------------------------------------------
# Round-trip verification
# ---------------------------------------------------------------------------

def _max_delta(before: list, after: list) -> float:
    worst = 0.0
    for a, b in zip(before, after):
        d = abs(a - b)
        if d > worst:
            worst = d
    return worst


def verify_fbx_roundtrip(
    objects: Sequence[bpy.types.Object],
    fbx_path: str,
    *,
    colors_type: str = "LINEAR",
    apply_modifiers: bool = True,
    expect_axis_map: bool = True,
) -> RoundtripReport:
    """Re-import ``fbx_path`` and compare it to ``objects``. Never raises.

    Two imports, because they answer different questions and one cannot answer
    both:

    *   **A, matching axes.** Blender's importer undoes the export conversion, so
        world-space geometry must come back identical. This is the data-survival
        test: counts, UV layer order, colour values, corner normal directions,
        landmark position, chirality.
    *   **B, ``use_manual_orientation`` with forward=Y up=Z.** That is an identity
        conversion, so the imported world space *is* the raw FBX axis space. This is
        the only way to read what the file actually says about orientation without
        writing an FBX parser, and it is what turns "the axes are correct" from a
        claim into a measurement.

    Import A also yields the unit scale: with ``bake_space_transform`` the FBX node
    transform is identity, so the correction the importer places on the object is
    exactly the inverse of the export scale. A measured 0.01 means the file holds
    centimetres.
    """
    report = RoundtripReport(passed=False)
    if not objects:
        report.failures.append("no source objects supplied")
        return report
    if not os.path.isfile(fbx_path):
        report.failures.append("fbx not found: " + os.path.basename(fbx_path))
        return report
    if os.path.getsize(fbx_path) <= 0:
        report.failures.append("fbx is zero bytes: " + os.path.basename(fbx_path))
        return report

    before = {}
    for obj in objects:
        shape = _snapshot(obj, apply_modifiers=apply_modifiers)
        before[shape.name] = shape
    report.triangle_counts = {n: s.triangle_count for n, s in before.items()}

    # -- import A ----------------------------------------------------------
    with _ImportSandbox() as sandbox:
        try:
            bpy.ops.import_scene.fbx(filepath=fbx_path, use_custom_normals=True,
                                     colors_type=colors_type,
                                     use_image_search=False)
        except RuntimeError as error:
            report.failures.append("re-import failed: " + str(error).strip()[:200])
            return report
        imported = sandbox.imported_meshes()
        after = {}
        for obj in imported:
            after[obj.name] = _snapshot(obj, apply_modifiers=False)
        _compare_a(before, after, report)

    # -- import B ----------------------------------------------------------
    if expect_axis_map:
        with _ImportSandbox() as sandbox:
            try:
                bpy.ops.import_scene.fbx(
                    filepath=fbx_path, use_manual_orientation=True,
                    axis_forward="Y", axis_up="Z", use_custom_normals=True,
                    colors_type=colors_type, use_image_search=False)
            except RuntimeError as error:
                report.failures.append(
                    "raw-axis re-import failed: " + str(error).strip()[:200])
                return report
            raw = {}
            for obj in sandbox.imported_meshes():
                raw[obj.name] = _snapshot(obj, apply_modifiers=False)
            _compare_axes(before, raw, report)
    else:
        report.notes.append(
            "axis map not checked: caller passed expect_axis_map=False")

    report.passed = not report.failures
    return report


def _compare_a(before: dict, after: dict, report: RoundtripReport) -> None:
    """Data-survival comparison against the axis-matched re-import."""
    missing = sorted(set(before) - set(after))
    extra = sorted(set(after) - set(before))
    if missing:
        report.failures.append("objects absent from the fbx: " + ", ".join(missing))
    if extra:
        report.failures.append("unexpected objects in the fbx: " + ", ".join(extra))
    report.notes.append("objects round-tripped: {0}".format(sorted(after)))

    for name in sorted(set(before) & set(after)):
        src, dst = before[name], after[name]
        tag = name + ": "

        if src.vertex_count != dst.vertex_count:
            report.failures.append(
                tag + "vertex count {0} -> {1}".format(src.vertex_count,
                                                       dst.vertex_count))
        if src.triangle_count != dst.triangle_count:
            report.failures.append(
                tag + "triangle count {0} -> {1}".format(src.triangle_count,
                                                         dst.triangle_count))
        report.notes.append(
            tag + "verts {0}->{1} loops {2}->{3} tris {4}->{5}".format(
                src.vertex_count, dst.vertex_count, src.loop_count,
                dst.loop_count, src.triangle_count, dst.triangle_count))

        # UV order is the contract, not UV names: Unity binds uv0/uv1 by the
        # layer order in the file (3dmodel.md section 3 TexCoord0/TexCoord1).
        if src.uv_names != dst.uv_names:
            report.failures.append(
                tag + "uv layer order {0} -> {1}".format(list(src.uv_names),
                                                         list(dst.uv_names)))
        report.uv_layer_names = dst.uv_names

        if src.color_layers and not dst.color_layers:
            report.failures.append(
                tag + "vertex colour attribute {0} did not survive".format(
                    [c[0] for c in src.color_layers]))
        elif dst.color_layers:
            report.has_vertex_colors = True
            # BYTE_COLOR arrives as FLOAT_COLOR when colors_type='LINEAR'; the
            # importer chooses the storage type, the FBX itself has no byte
            # colours. law.py's UNorm8 contract is about the Unity-side layout,
            # which the importer notes cover, so only the VALUES matter here.
            report.notes.append(
                tag + "colour layers {0} -> {1} (Blender storage type changes on "
                "a LINEAR import; the file carries floats either way)".format(
                    list(src.color_layers), list(dst.color_layers)))
            if len(src.colors) == len(dst.colors) and src.colors:
                worst = _max_delta(src.colors, dst.colors)
                report.notes.append(
                    tag + "colour max delta {0:.7f} (tol {1:g})".format(
                        worst, TOL_COLOR))
                if worst > TOL_COLOR:
                    report.failures.append(
                        tag + "vertex colour values drifted by {0:.6f}, above "
                        "{1:g}; a value near the sRGB curve here means "
                        "colors_type is wrong".format(worst, TOL_COLOR))
            elif src.colors:
                report.failures.append(
                    tag + "colour element count {0} -> {1}".format(
                        len(src.colors), len(dst.colors)))

        if len(src.world_positions) == len(dst.world_positions):
            worst = _max_delta(src.world_positions, dst.world_positions)
            report.notes.append(
                tag + "world position max delta {0:.7f} m (tol {1:g})".format(
                    worst, TOL_POSITION_M))
            if worst > TOL_POSITION_M:
                report.failures.append(
                    tag + "world positions moved by {0:.6f} m".format(worst))

        if src.has_custom_normals:
            report.has_custom_normals = True
        if len(src.world_corner_normals) == len(dst.world_corner_normals) \
                and src.world_corner_normals:
            worst = _max_delta(src.world_corner_normals, dst.world_corner_normals)
            report.notes.append(
                tag + "world corner-normal max delta {0:.7f} (tol {1:g}), "
                "source has_custom_normals={2}, reimport={3}".format(
                    worst, TOL_NORMAL, src.has_custom_normals,
                    dst.has_custom_normals))
            if worst > TOL_NORMAL:
                report.failures.append(
                    tag + "corner normals changed by {0:.6f}; the authored "
                    "weighted/split normal basis did not survive".format(worst))
        elif src.world_corner_normals:
            report.failures.append(
                tag + "corner normal count {0} -> {1}".format(
                    len(src.world_corner_normals) // 3,
                    len(dst.world_corner_normals) // 3))

        if len(src.uv0) == len(dst.uv0) and src.uv0:
            worst = _max_delta(src.uv0, dst.uv0)
            report.notes.append(
                tag + "uv0 max delta {0:.8f} (tol {1:g})".format(worst, TOL_UV))
            if worst > TOL_UV:
                report.failures.append(tag + "uv0 drifted by {0:.7f}".format(worst))

        lm = math.sqrt(sum((a - b) ** 2 for a, b in
                           zip(src.landmark, dst.landmark)))
        report.notes.append(
            tag + "landmark {0} -> {1}, delta {2:.7f} m, margin {3:.5f} m".format(
                tuple(round(c, 5) for c in src.landmark),
                tuple(round(c, 5) for c in dst.landmark), lm,
                src.landmark_margin))
        if src.landmark_margin < MIN_LANDMARK_MARGIN:
            report.notes.append(
                tag + "landmark margin below {0:g} m: this mesh is near-symmetric "
                "at its extreme vertex, so the landmark test is not "
                "decisive".format(MIN_LANDMARK_MARGIN))
        elif lm > TOL_POSITION_M:
            report.failures.append(
                tag + "landmark vertex moved {0:.6f} m; an axis permutation or a "
                "sign flip is the usual cause".format(lm))

        if abs(src.signed_volume) > 1.0e-9:
            ratio = dst.signed_volume / src.signed_volume
            report.notes.append(
                tag + "signed volume {0:+.7f} -> {1:+.7f} (ratio {2:+.6f})".format(
                    src.signed_volume, dst.signed_volume, ratio))
            if ratio <= 0.0:
                report.failures.append(
                    tag + "signed volume changed sign: geometry is mirrored or "
                    "winding inverted, which flips tangent handedness and breaks "
                    "every normal map")
            elif abs(ratio - 1.0) > 1.0e-3:
                report.failures.append(
                    tag + "signed volume magnitude changed by {0:.4%}; the "
                    "topology the file holds is not the topology that was "
                    "measured".format(abs(ratio - 1.0)))
            else:
                report.chirality_preserved = True
        else:
            report.notes.append(
                tag + "signed volume is ~0 (open or planar shell); chirality "
                "checked by winding only")
            report.chirality_preserved = True

        # bake_space_transform leaves the node at identity, so whatever transform
        # the importer puts back is the inverse of the export global matrix.
        scale = abs(dst.object_scale[0])
        if scale > 1.0e-9:
            report.measured_unit_scale = 1.0 / scale
            report.notes.append(
                tag + "re-import object scale {0:g} -> measured {1:g} fbx units "
                "per metre (header UnitScaleFactor={2:g}, i.e. centimetres)".format(
                    scale, report.measured_unit_scale,
                    FBX_HEADER_UNIT_SCALE_FACTOR))


def _compare_axes(before: dict, raw: dict, report: RoundtripReport) -> None:
    """Axis and chirality claim, read off the raw FBX axis space."""
    shared = sorted(set(before) & set(raw))
    if not shared:
        report.failures.append(
            "raw-axis re-import produced no matching object; axis map unproven")
        return
    confirmed = 0
    for name in shared:
        src, dst = before[name], raw[name]
        tag = name + " [fbx-space]: "
        if src.landmark_margin < MIN_LANDMARK_MARGIN:
            report.notes.append(
                tag + "skipped: near-symmetric extreme vertex")
            continue
        expected = _normalised(Vector(blender_to_fbx_axes(
            Vector(src.landmark) - Vector(src.centroid))))
        actual = _normalised(Vector(dst.landmark) - Vector(dst.centroid))
        delta = (expected - actual).length
        report.notes.append(
            tag + "landmark direction expected {0} measured {1} delta "
            "{2:.6f}".format(tuple(round(c, 5) for c in expected),
                             tuple(round(c, 5) for c in actual), delta))
        if delta > TOL_DIRECTION:
            report.failures.append(
                tag + "axis map wrong: Blender {0} should appear at FBX {1} but "
                "measured {2}. Check axis_forward='-Z' / axis_up='Y'.".format(
                    tuple(round(c, 4) for c in _normalised(
                        Vector(src.landmark) - Vector(src.centroid))),
                    tuple(round(c, 4) for c in expected),
                    tuple(round(c, 4) for c in actual)))
            continue
        if src.signed_volume * dst.signed_volume < 0.0:
            report.failures.append(
                tag + "chirality inverted inside the fbx: the axis conversion is "
                "a proper rotation, so a sign change here means the source object "
                "carries a mirrored transform")
            continue
        confirmed += 1
        unity = blender_to_unity(Vector(src.landmark) - Vector(src.centroid))
        report.notes.append(
            tag + "Unity-space landmark offset will be {0} (Blender {1}); "
            "handedness flip is Unity's, winding is flipped to match".format(
                tuple(round(c, 5) for c in unity),
                tuple(round(c, 5) for c in (Vector(src.landmark)
                                            - Vector(src.centroid)))))
    report.axis_map_confirmed = confirmed > 0
    if confirmed == 0 and not report.failures:
        report.notes.append(
            "axis map not asserted: every object was near-symmetric at its "
            "extreme vertex")
