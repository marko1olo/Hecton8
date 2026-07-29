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
#: Blender's uniquifying suffix. Re-importing an FBX into the session that produced
#: it lands every node next to its source, so the copies come back as ``NAME.001``.
#: Matching on the raw name would report "objects absent from the fbx" for a file
#: that is perfectly correct, so the comparison keys on the stripped base name --
#: and a source object already carrying such a suffix is rejected outright, because
#: it would make that stripping ambiguous and Unity mangles the dot anyway.
_DUPLICATE_SUFFIX = re.compile(r"\.\d{3}$")


def _base_name(name: str) -> str:
    return _DUPLICATE_SUFFIX.sub("", name)


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
        bpy.context.view_layer.update()
        view_layer = bpy.context.view_layer
        self._previous_active = view_layer.active_layer_collection
        self._previous_selection = tuple(
            o for o in view_layer.objects if o is not None and o.select_get())
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
                if obj is None:
                    continue
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
            key = _base_name(obj.name)
            if key in after:
                report.failures.append(
                    "two imported nodes collapse onto the base name " + key)
                continue
            after[key] = _snapshot(obj, apply_modifiers=False)
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
                raw[_base_name(obj.name)] = _snapshot(obj, apply_modifiers=False)
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
            # Name the SHAPE of the loss, not just the delta. polygon_count and
            # max_polygon_sides are already snapshotted on both sides and were never
            # compared, so a triangle-count failure reported a number with no
            # mechanism attached - and three separate hypotheses were measured and
            # refuted against it before anyone read these two fields.
            #
            # The discriminator:
            #   polygons SAME, tris down    -> an n-gon lost a side; a triangulation
            #                                  or n-gon-support problem, not lost
            #                                  geometry.
            #   polygons DOWN, verts SAME   -> a whole FACE was dropped while its
            #                                  vertices survived. That is a face the
            #                                  writer or reader refused, and the
            #                                  first candidate is a DUPLICATE FACE:
            #                                  two faces on the same vertex set are
            #                                  merged on import, and an isolated
            #                                  duplicate PAIR is manifold by the
            #                                  edge test - every edge has exactly
            #                                  two faces - so a non-manifold-edge
            #                                  repair cannot see it and neither can a
            #                                  bowtie-vertex walk.
            #   polygons DOWN, verts DOWN   -> geometry genuinely removed, e.g. a
            #                                  degenerate face collapsed on import.
            shape = ("polygons {0} -> {1}, maxSides {2} -> {3}, verts {4} -> {5}"
                     .format(src.polygon_count, dst.polygon_count,
                             src.max_polygon_sides, dst.max_polygon_sides,
                             src.vertex_count, dst.vertex_count))
            if src.polygon_count == dst.polygon_count:
                mechanism = ("an n-gon lost a side, so this is triangulation or "
                             "n-gon support, NOT lost geometry")
            elif src.vertex_count == dst.vertex_count:
                mechanism = ("a whole FACE was dropped with its vertices intact - "
                             "check for a DUPLICATE FACE, which is invisible to both "
                             "a non-manifold-edge query and a bowtie-vertex walk")
            else:
                mechanism = "geometry removed outright, verts fell too"
            report.failures.append(
                tag + "triangle count {0} -> {1} ({2}; {3})".format(
                    src.triangle_count, dst.triangle_count, shape, mechanism))
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


# ---------------------------------------------------------------------------
# Pre-export guards
# ---------------------------------------------------------------------------

def _as_object(candidate):
    """Accept a bpy object, a mesh_ops.LodLevel, or a mesh_ops.ColliderResult.

    Duck-typed on purpose: importing mesh_ops here would couple the exporter to a
    module another agent owns, for nothing but a type name.
    """
    if candidate is None:
        return None
    if isinstance(candidate, bpy.types.Object):
        return candidate
    inner = getattr(candidate, "obj", None)
    if isinstance(inner, bpy.types.Object):
        return inner
    raise TypeError(
        "expected a bpy Object or an object with an .obj attribute, got "
        + type(candidate).__name__)


def _guard_objects(objects: Sequence[bpy.types.Object],
                   notes: list) -> None:
    """Refuse the export states that produce silently broken Unity assets."""
    failures = []
    for obj in objects:
        if obj.type != "MESH":
            failures.append("{0} is a {1}, not a MESH".format(obj.name, obj.type))
            continue
        if obj.name not in bpy.context.view_layer.objects:
            failures.append(
                "{0} is not in the active view layer, so use_selection cannot "
                "reach it".format(obj.name))
        determinant = obj.matrix_world.to_3x3().determinant()
        if determinant <= 0.0:
            failures.append(
                "{0} has a mirrored or degenerate transform (matrix_world "
                "determinant {1:+.6f}). Unity inherits the flipped winding and the "
                "tangent w sign inverts with it, which breaks every normal map on "
                "the asset. 3dmodel.md section 3 forbids inverted unintentional "
                "winding: bake the mirror into the geometry and recompute normals "
                "instead of shipping a negative scale.".format(obj.name,
                                                               determinant))
        if not _SAFE_NODE_NAME.match(obj.name):
            failures.append(
                "{0!r} contains characters Unity's importer rewrites; the FBX node "
                "name would not match the mesh asset name and the _LOD suffix "
                "convention would break".format(obj.name))
        if _DUPLICATE_SUFFIX.search(obj.name):
            failures.append(
                "{0!r} ends in Blender's duplicate suffix. That name is an "
                "authoring accident, it breaks the _LOD suffix convention, Unity "
                "rewrites the dot, and it makes the round-trip node matching "
                "ambiguous. Rename through law.NAME_MESH / law.NAME_COLLIDER."
                .format(obj.name))
        mesh = obj.data
        if not mesh.vertices:
            failures.append("{0} has no vertices".format(obj.name))
        if not mesh.uv_layers:
            failures.append(
                "{0} has no UV layer. 3dmodel.md section 3 makes TexCoord0 a "
                "required stream and tangent space cannot be computed without "
                "it, so the Tangent stream would be dropped too.".format(obj.name))
        elif len(mesh.uv_layers) < 2:
            notes.append(
                "{0}: only UV0 present. 3dmodel.md section 3 requires TexCoord1 "
                "when a lightmap, detail, atlas remap or packed mask is used; "
                "confirm this asset genuinely needs none.".format(obj.name))
        if not mesh.color_attributes:
            failures.append(
                "{0} has no colour attribute. law.VCOL_CONTRACT is mandatory for "
                "every family (3dmodel.md sections 4 and 5), and a missing "
                "stream a material reads is a validation failure per section "
                "8.".format(obj.name))
        elif len(mesh.color_attributes) > 1:
            extra = [a.name for a in mesh.color_attributes
                     if a.name != getattr(mesh.attributes, "active_color_name", "")]
            notes.append(
                "{0}: {1} colour attributes present ({2}). Unity consumes one; "
                "prioritize_active_color writes {3!r} first, but leftover layers "
                "still bloat the file. vertexcolor.remove_scratch_attributes "
                "clears the AO bake scratch layer.".format(
                    obj.name, len(mesh.color_attributes),
                    [a.name for a in mesh.color_attributes], extra,
                ) if extra else
                "{0}: {1} colour attributes present.".format(
                    obj.name, len(mesh.color_attributes)))
        if not mesh.materials:
            notes.append(
                "{0}: no material slot. 3dmodel.md section 6 declares slot 0 as "
                "the primary structural/tissue material; Unity will assign its "
                "default material to submesh 0.".format(obj.name))
        sides = max((len(p.vertices) for p in mesh.polygons), default=0)
        if sides > 4:
            notes.append(
                "{0}: contains a {1}-sided polygon. use_triangles=True is "
                "mandatory for this mesh -- without it Blender skips tangent "
                "export with only a console warning and the FBX ships with no "
                "Tangent stream.".format(obj.name, sides))
    if failures:
        raise GenerationAborted(
            "fbx export refused: " + "; ".join(failures), failures=failures)


def _view_layer_objects() -> list:
    """Live view-layer objects, None entries filtered out.

    ``view_layer.objects`` can hand back ``None`` slots while the depsgraph is stale
    -- for example straight after a script removed datablocks. Iterating it blind
    raises ``AttributeError`` on the first hole, which is a crash in the middle of a
    selection restore rather than an honest error.
    """
    return [o for o in bpy.context.view_layer.objects if o is not None]


def _refresh_view_layer() -> None:
    """Flush pending depsgraph work before anything reads the scene.

    Two things break without this. ``view_layer.objects`` does not yet list an
    object a script has just linked, so the reachability guard rejects perfectly
    valid geometry. And ``matrix_world`` is stale after a script assigns
    ``obj.scale``, so the mirrored-transform guard reads the previous determinant
    and waves a negative scale through.
    """
    bpy.context.view_layer.update()


def _select_only(objects: Sequence[bpy.types.Object]) -> None:
    view_layer = bpy.context.view_layer
    for other in _view_layer_objects():
        if other.select_get():
            other.select_set(False)
    for obj in objects:
        obj.select_set(True)
    view_layer.objects.active = objects[0]


# ---------------------------------------------------------------------------
# Export
# ---------------------------------------------------------------------------

def export_fbx(
    objects,
    out_path: str,
    *,
    apply_modifiers: bool = True,
    verify_roundtrip: bool = True,
    blackbox: Optional[BlackBox] = None,
) -> ExportResult:
    """Write one FBX containing ``objects``, then prove it round-trips.

    ``objects`` may be bpy objects, ``mesh_ops.LodLevel`` values or
    ``mesh_ops.ColliderResult`` values -- anything exposing ``.obj``.

    ``out_path`` is required and is never defaulted. law.py holds no sanctioned FBX
    staging directory, and inventing one would breach ``AGENTS.md`` ``Project
    Shape``: "Do not invent new prefixes, folders ... without local source proof and
    justification." Writing under ``Assets/`` would also trigger an import in
    whichever Unity instance is running.

    Raises :class:`~h8forge.blackbox.GenerationAborted` when a guard trips or the
    round trip finds data loss, an axis error or a chirality flip.
    ``PROCEDURAL_ASSET_PIPELINE.md`` "Validation Before Save": "On validation
    failure the save is aborted."
    """
    resolved = []
    for candidate in (objects if isinstance(objects, (list, tuple))
                      else list(objects)):
        obj = _as_object(candidate)
        if obj is not None:
            resolved.append(obj)
    if not resolved:
        raise GenerationAborted("fbx export refused: no objects to export")

    notes = []
    _refresh_view_layer()
    _guard_objects(resolved, notes)

    out_path = os.path.abspath(out_path)
    directory = os.path.dirname(out_path)
    if directory:
        os.makedirs(directory, exist_ok=True)
    if os.path.exists(out_path):
        # AGENTS.md "Atomic File Delete Rule": a stale artefact left in place is
        # how a run reports success against the previous run's output.
        os.remove(out_path)

    triangle_counts = {}
    uv_names = ()
    has_colors = False
    has_custom_normals = False
    max_sides = 0
    for obj in resolved:
        shape = _snapshot(obj, apply_modifiers=apply_modifiers)
        triangle_counts[obj.name] = shape.triangle_count
        uv_names = uv_names or shape.uv_names
        has_colors = has_colors or bool(shape.color_layers)
        has_custom_normals = has_custom_normals or shape.has_custom_normals
        max_sides = max(max_sides, shape.max_polygon_sides)

    previous_selection = tuple(
        o for o in _view_layer_objects() if o.select_get())
    previous_active = bpy.context.view_layer.objects.active
    settings = dict(EXPORT_SETTINGS)
    settings["use_mesh_modifiers"] = bool(apply_modifiers)
    try:
        _select_only(resolved)
        result = bpy.ops.export_scene.fbx(filepath=out_path, **settings)
    finally:
        for obj in _view_layer_objects():
            try:
                obj.select_set(obj in previous_selection)
            except RuntimeError:
                pass
        if previous_active is not None:
            try:
                bpy.context.view_layer.objects.active = previous_active
            except (RuntimeError, ReferenceError):
                pass

    # An operator that returns CANCELLED writes nothing and raises nothing.
    # mesh_ops documents the same trap for modifier_apply; the cost of not
    # checking is a manifest that describes a file which does not exist.
    if "FINISHED" not in result:
        raise GenerationAborted(
            "bpy.ops.export_scene.fbx returned {0} for {1}".format(
                sorted(result), os.path.basename(out_path)))
    if not os.path.isfile(out_path) or os.path.getsize(out_path) <= 0:
        raise GenerationAborted(
            "fbx export reported FINISHED but produced no usable file: "
            + os.path.basename(out_path))

    notes.insert(0, "wrote {0} ({1} bytes) with axis_forward={2} axis_up={3} "
                    "bake_space_transform={4} colors_type={5} use_triangles={6} "
                    "use_tspace={7}".format(
                        os.path.basename(out_path), os.path.getsize(out_path),
                        settings["axis_forward"], settings["axis_up"],
                        settings["bake_space_transform"], settings["colors_type"],
                        settings["use_triangles"], settings["use_tspace"]))

    unit_scale = FBX_UNITS_PER_METRE
    verified = False
    # use_tspace only succeeds once the mesh is triangles/quads, and
    # use_triangles guarantees that; a tangent claim without it would be false
    # for any n-gon mesh.
    has_tangents = bool(settings["use_tspace"]) and bool(uv_names)
    if verify_roundtrip:
        report = verify_fbx_roundtrip(
            resolved, out_path, colors_type=settings["colors_type"],
            apply_modifiers=apply_modifiers)
        notes.extend(report.lines())
        unit_scale = report.measured_unit_scale
        has_colors = has_colors or report.has_vertex_colors
        if report.uv_layer_names:
            uv_names = report.uv_layer_names
        if not report.passed:
            dump = blackbox.dump("fbx_roundtrip_failed") if blackbox else None
            if blackbox is not None:
                blackbox.note_invalid("export_fbx", "FBX_ROUNDTRIP_FAILED",
                                      "; ".join(report.failures)[:400])
            # DELETE THE REJECTED FILE. PROCEDURAL_ASSET_PIPELINE.md "Validation
            # Before Save": "On validation failure the save is aborted" - and an
            # aborted save that leaves the file on disk has not aborted anything.
            #
            # This was harmless while packages landed in Docs/AgentLogs, which is
            # gitignored and outside Assets. It stopped being harmless the moment
            # law.forge_package_dir moved the destination INSIDE Assets on
            # 2026-07-29: a rejected FBX left there is imported by Unity on next
            # focus, and the generator's own abort message is the only thing saying
            # it should not exist. Raising while leaving the artefact behind is the
            # worst of both outcomes, because the caller sees a failure and the
            # project sees an asset.
            try:
                if os.path.exists(out_path):
                    os.remove(out_path)
                    notes.append(
                        "rejected fbx deleted from {0}: a failed round trip must "
                        "not leave an importable file behind".format(
                            _project_relative(out_path, [])))
            except OSError as removal_error:            # pragma: no cover
                notes.append(
                    "REJECTED FBX COULD NOT BE DELETED at {0}: {1}. Remove it by "
                    "hand before Unity imports it.".format(
                        _project_relative(out_path, []), removal_error))
            raise GenerationAborted(
                "fbx round trip failed for {0}: {1}".format(
                    os.path.basename(out_path), "; ".join(report.failures)),
                dump_path=dump, failures=list(report.failures))
        verified = True
        notes.append(
            "roundtrip VERIFIED: axis map confirmed={0}, chirality "
            "preserved={1}".format(report.axis_map_confirmed,
                                   report.chirality_preserved))
    else:
        notes.append(
            "roundtrip verification disabled by caller; stream survival is "
            "UNPROVEN for this file")

    if blackbox is not None:
        blackbox.record(
            "export_fbx",
            triangle_count=sum(triangle_counts.values()),
            vertex_count=-1,
            warning="" if verified else "roundtrip not verified",
        )

    return ExportResult(
        fbx_path=out_path,
        object_names=tuple(o.name for o in resolved),
        triangle_counts=triangle_counts,
        has_vertex_colors=has_colors,
        has_custom_normals=has_custom_normals,
        has_tangents=has_tangents,
        uv_layer_names=tuple(uv_names),
        unit_scale=unit_scale,
        roundtrip_verified=verified,
        roundtrip_notes=tuple(notes),
    )


def export_lod_group(
    lod_objects,
    collider,
    out_path: str,
    *,
    identity=None,
    apply_modifiers: bool = True,
    verify_roundtrip: bool = True,
    blackbox: Optional[BlackBox] = None,
) -> ExportResult:
    """Export a whole LOD chain plus its collision proxy as one FBX.

    One file, several nodes. Unity's automatic LODGroup route keys off child
    GameObjects whose names end in ``_LOD0``, ``_LOD1``, ``_LOD2`` sharing a common
    prefix, and ``law.NAME_MESH`` ("MESH_{family}_{name}_LOD{lod}") already produces
    exactly that, so the two conventions reconcile without renaming anything. The
    reconciliation is checked here rather than assumed: a chain whose names do not
    end in the suffix is rejected, because Unity would then import three sibling
    renderers with no LODGroup and ``HectonFBXPostprocessor.OnPostprocessModel``
    would decimate LOD0 into its own ``__AUTO_LOD1``/``__AUTO_LOD2`` above 2000
    triangles -- shipping five LODs where three were authored.

    The collider rides in the same file as a ``COL_``-prefixed node
    (``law.COLLIDER_PREFIX``). ``3dmodel.md`` section 9 requires the proxy to be a
    separate object from the visual mesh, and the Unity authoring script must bind
    it to a ``MeshCollider`` with ``convex = true`` and never to an LOD mesh.
    """
    levels = []
    for candidate in (lod_objects if isinstance(lod_objects, (list, tuple))
                      else list(lod_objects)):
        obj = _as_object(candidate)
        if obj is not None:
            levels.append(obj)
    if not levels:
        raise GenerationAborted("lod group export refused: no LOD objects")

    notes = []
    seen = {}
    for position, obj in enumerate(levels):
        match = _LOD_SUFFIX.search(obj.name)
        if match is None:
            raise GenerationAborted(
                "lod group export refused: {0!r} does not end in _LOD<n>, so "
                "Unity cannot build an LODGroup from it. Use "
                "law.NAME_MESH.format(family=..., name=..., lod=...).".format(
                    obj.name))
        index = int(match.group(1))
        if index in seen:
            raise GenerationAborted(
                "lod group export refused: LOD{0} claimed by both {1!r} and "
                "{2!r}".format(index, seen[index], obj.name))
        seen[index] = obj.name
        if index != position:
            notes.append(
                "LOD ordering: argument position {0} carries _LOD{1}; the manifest "
                "records the suffix, not the argument order".format(position,
                                                                   index))
    prefixes = set(_LOD_SUFFIX.sub("", obj.name) for obj in levels)
    if len(prefixes) > 1:
        raise GenerationAborted(
            "lod group export refused: LOD names share no common prefix "
            "({0}); Unity groups by the text before _LOD".format(
                sorted(prefixes)))

    missing = [i for i in (0, 1, 2) if i not in seen]
    if missing:
        notes.append(
            "LOD chain is incomplete: missing {0}. 3dmodel.md section 7 requires "
            "LOD0/LOD1/LOD2 unless the asset is an approved impostor/card or an "
            "editor-only debug mesh; record that exemption in the "
            "manifest.".format(["LOD" + str(i) for i in missing]))

    exported = list(levels)
    collider_obj = _as_object(collider)
    if collider_obj is not None:
        if not collider_obj.name.startswith(law.COLLIDER_PREFIX):
            raise GenerationAborted(
                "lod group export refused: collider {0!r} must start with "
                "{1!r} (3dmodel.md section 9, law.COLLIDER_PREFIX)".format(
                    collider_obj.name, law.COLLIDER_PREFIX))
        exported.append(collider_obj)
    else:
        reason = getattr(collider, "reason", "") if collider is not None else ""
        notes.append(
            "no collision proxy in this package"
            + (": " + reason if reason else
               ". 3DMODEL_FLORA_CORAL.md section 7 allows this for flora/coral "
               "only; every other family needs a COL_ proxy."))

    result = export_fbx(exported, out_path, apply_modifiers=apply_modifiers,
                        verify_roundtrip=verify_roundtrip, blackbox=blackbox)

    family = getattr(identity, "family", None)
    if family is not None:
        budgets = law.LOD_BUDGETS.get(
            family if isinstance(family, law.Family) else law.Family(family))
        if budgets is not None:
            previous = None
            for index in sorted(seen):
                name = seen[index]
                tris = result.triangle_counts.get(name, -1)
                budget = budgets.limit(index)
                verdict = "within" if tris <= budget else "OVER"
                notes.append(
                    "LOD{0} {1}: {2} tris vs law budget {3} -> {4}".format(
                        index, name, tris, budget, verdict))
                if previous is not None and tris >= previous:
                    notes.append(
                        "LOD chain is not monotonic at LOD{0} ({1} >= previous "
                        "{2}); 3dmodel.md section 7 requires each level to be a "
                        "reduction of the one before".format(index, tris,
                                                             previous))
                previous = tris
    if collider_obj is not None:
        tris = result.triangle_counts.get(collider_obj.name, -1)
        notes.append(
            "collider {0}: {1} tris vs law.COLLIDER_CONVEX_TRI_MAX {2} -> {3}"
            .format(collider_obj.name, tris, law.COLLIDER_CONVEX_TRI_MAX,
                    "within" if tris <= law.COLLIDER_CONVEX_TRI_MAX else "OVER"))
        notes.append(
            "Unity side must bind {0} to a MeshCollider with convex=true on a "
            "collider child, never to an LOD mesh (3dmodel.md section 9)".format(
                collider_obj.name))
    notes.append(
        "Unity LODGroup: create it explicitly from the _LOD suffixed children "
        "rather than relying on importer auto-detection, then set "
        "screenRelativeTransitionHeight per level with a hysteresis band and "
        "fadeMode=CrossFade with animateCrossFading=false (dithered). "
        "3dmodel.md section 7 bans alpha-blended cross-fade for dense "
        "flora/coral on the compact lane; dither is not alpha blend.")

    return ExportResult(
        fbx_path=result.fbx_path,
        object_names=result.object_names,
        triangle_counts=result.triangle_counts,
        has_vertex_colors=result.has_vertex_colors,
        has_custom_normals=result.has_custom_normals,
        has_tangents=result.has_tangents,
        uv_layer_names=result.uv_layer_names,
        unit_scale=result.unit_scale,
        roundtrip_verified=result.roundtrip_verified,
        roundtrip_notes=tuple(list(result.roundtrip_notes) + notes),
    )


# ---------------------------------------------------------------------------
# Unity import contract
# ---------------------------------------------------------------------------
# Families whose runtime animation comes from an offline-baked Vertex Animation
# Texture. AGENTS.md "Zero-GC Scatter & Animation Protocol": "Kelps, corals, and
# fish must use offline baked Vertex Animation Textures (VAT) and
# BatchRendererGroup (BRG) indirect rendering." A VAT indexes per-vertex data by
# vertex id, so any importer step that reorders or welds vertices desynchronises
# the texture from the mesh.
_VAT_FAMILIES = (law.Family.FLORA, law.Family.FLORA_CLUSTER, law.Family.FAUNA)

#: Real project layers, read from ProjectSettings/TagManager.asset. Not invented:
#: AGENTS.md forbids changing Tags/Layers, so the importer script must select from
#: what exists.
_FAMILY_LAYER = {
    law.Family.SMALL_PROP: "World_Static",
    law.Family.BASE_MODULE: "BaseModule",
    law.Family.WRECKAGE: "World_Static",
    law.Family.GEOLOGY: "World_Static",
    law.Family.FLORA: "Flora_NonColliding",
    law.Family.FLORA_CLUSTER: "Flora_NonColliding",
    law.Family.FAUNA: "Fauna_Hitbox",
}


def unity_import_notes(family) -> dict:
    """The exact `ModelImporter` state the Unity-side authoring script must set.

    Values are flat so a C# reader can consume them directly; ``why`` carries the
    bible line behind each one so the pair cannot drift.
    """
    resolved = family if isinstance(family, law.Family) else law.Family(family)
    surface = law.FAMILY_SURFACE_CLASS[resolved]
    vat = resolved in _VAT_FAMILIES

    importer = {
        "globalScale": 1.0,
        "useFileScale": True,
        "bakeAxisConversion": False,
        "importNormals": "Import",
        "normalSmoothingAngle": law.SMOOTH_ANGLE_DEG,
        "importBlendShapeNormals": "None",
        "importTangents": "CalculateMikk",
        "importColors": True,
        "meshCompression": "Off",
        "isReadable": False,
        "optimizeMeshVertices": not vat,
        "optimizeMeshPolygons": True,
        "meshOptimizationFlags": "PolygonOrder" if vat else "Everything",
        "weldVertices": not vat,
        "indexFormat": "Auto",
        "keepQuads": False,
        "generateSecondaryUV": False,
        "materialImportMode": "None",
        "addCollider": False,
        "importAnimation": False,
        "animationType": "None",
        "importBlendShapes": False,
        "importVisibility": False,
        "importCameras": False,
        "importLights": False,
        "importConstraints": False,
        "preserveHierarchy": True,
        "sortHierarchyByName": False,
    }

    why = {
        "globalScale":
            "The FBX already carries real-world size. law.py expresses every "
            "dimension in metres (BEVEL_RANGES, MIN_BOUNDS_EXTENT_M); any scale "
            "factor other than 1.0 would silently rescale those budgets.",
        "useFileScale":
            "Measured: the exporter writes geometry in centimetres and leaves the "
            "header UnitScaleFactor at {0:g}, so Convert Units is what turns 175.0 "
            "back into 1.75 m. Off by mistake and the asset arrives {1:g}x too "
            "large.".format(FBX_HEADER_UNIT_SCALE_FACTOR, FBX_UNITS_PER_METRE),
        "bakeAxisConversion":
            "bake_space_transform=True already baked the axis conversion into the "
            "vertex data on the Blender side, so the FBX node transform is "
            "identity and Unity has nothing left to bake. Enabling it too would "
            "apply a second conversion.",
        "importNormals":
            "3dmodel.md section 3: 'RecalculateNormals, RecalculateTangents, and "
            "RecalculateBounds are allowed only as editor fallback after a "
            "documented failure, never as the default strategy. A generator owns "
            "normals, tangents, UVs, and bounds because it owns the geometry.' "
            "mesh_ops.apply_shading_basis bakes the section 4 weighted-normal "
            "formula into custom split normals; Calculate throws that away and "
            "re-derives from an angle, which is the bevel shading work lost.",
        "normalSmoothingAngle":
            "law.SMOOTH_ANGLE_DEG, the same value mesh_ops used for "
            "shade_auto_smooth. Inert while importNormals=Import, but it means a "
            "forced fallback to Calculate reproduces the authored split instead "
            "of a different one.",
        "importBlendShapeNormals":
            "There are no blend shapes: the exporter runs with bake_anim=False "
            "and object_types={'MESH'}.",
        "importTangents":
            "3dmodel.md section 3 requires 'Tangent | Float32 x4 ... "
            "MikkTSpace-compatible. w is handedness.' Under MikkTSpace the tangent "
            "is fully determined by position, normal and UV0, and all three are "
            "measured to survive this export exactly, so CalculateMikk reproduces "
            "the same basis expressed in Unity's own left-handed frame. The FBX "
            "does carry tangent and binormal layers (use_tspace=True, verified "
            "present in the file), so 'Import' is available -- but Blender's "
            "importer discards tangents, so no round trip inside Blender can prove "
            "the w sign survives Unity's handedness flip. Switching to 'Import' "
            "needs a Unity-side normal-map A/B capture first. Matches the existing "
            "project policy in HectonFBXPostprocessor.ApplyImporterPolicy.",
        "importColors":
            "The colour stream is data, not decoration: law.VCOL_CONTRACT for "
            "{0} is {1} (3dmodel.md sections 4 and 5). Section 8: 'Missing "
            "tangents, colors, UVs, or masks are validation failures when the "
            "material reads them.' Exported with colors_type='LINEAR' so the "
            "numbers arrive unwarped by an sRGB transfer "
            "curve.".format(surface.value, list(law.VCOL_CONTRACT[surface])),
        "meshCompression":
            "Compression quantises positions, normals, tangents and UVs. "
            "3dmodel.md section 10 gates 'Normals normalized within 0.995 to 1.005 "
            "length' and section 3 fixes a stable Float32 layout; quantised "
            "normals fail the first and quantised vertex colours corrupt the AO "
            "and sway masks. Note the existing project policy sets Medium for "
            "Assets/ScifiFacility third-party models only.",
        "isReadable":
            "AGENTS.md Runtime Hot-Path Law forbids mesh.vertices, mesh.normals "
            "and mesh.triangles in hot paths, and 3dmodel.md section 0A forbids "
            "runtime mutation of vertex buffers, so nothing needs the CPU copy. "
            "Read/Write on doubles mesh memory against the compact-lane 1800 MB "
            "VRAM ceiling. Colliders do not need it: PhysX cooks at import, and "
            "runtime collider cooking is rejected anyway.",
        "optimizeMeshVertices":
            ("Off for {0}: this family renders through a baked VAT, which indexes "
             "per-vertex animation by vertex id. Reordering vertices desynchronises "
             "the texture from the mesh and the asset animates as noise."
             if vat else
             "On: reordering for GPU cache locality is free performance and "
             "nothing in this family indexes the mesh by vertex id.").format(
                resolved.value),
        "optimizeMeshPolygons":
            "Triangle-order optimisation touches no per-vertex identity, so it is "
            "safe even on VAT families.",
        "meshOptimizationFlags":
            ("PolygonOrder only, for the VAT vertex-id reason above."
             if vat else
             "Everything: both vertex and polygon order may be optimised."),
        "weldVertices":
            ("Off for {0}: welding changes the vertex count and therefore the VAT "
             "row mapping.".format(resolved.value) if vat else
             "On: welding only merges vertices whose position, normal and UV all "
             "match, so the deliberate splits from the section 4 smoothing groups "
             "survive it."),
        "indexFormat":
            "Auto picks 16-bit under 65k vertices. law.LOD_BUDGETS tops out at a "
            "35 000 triangle fauna body, so most assets stay 16-bit.",
        "keepQuads":
            "The FBX is already triangulated (use_triangles=True), so this is "
            "moot; leaving it off keeps the imported topology identical to the "
            "measured one.",
        "generateSecondaryUV":
            "MUST stay off. Unity's secondary-UV generator OVERWRITES UV1, and "
            "3dmodel.md section 3 makes TexCoord1 an authored stream: 'Lightmap, "
            "detail, atlas remap, or packed baked masks when required.' Note "
            "HectonBakeryUvAudit.RunAudit() sets generateSecondaryUV=true and "
            "reimports for models under its managed roots -- a generated package "
            "placed there loses its authored UV1.",
        "materialImportMode":
            "3dmodel.md section 0 requires 'Static material references named "
            "MAT_*, never runtime material clones', and "
            "PROCEDURAL_ASSET_PIPELINE.md requires shared "
            "MAT_<Family>_<SurfaceRole> assets. Letting Unity build materials from "
            "the FBX creates per-model materials and breaks the SRP Batcher and "
            "atlas policy. The exporter ships nothing to import: path_mode='STRIP' "
            "and embed_textures=False. Note the existing project policy forces "
            "materialLocation=InPrefab for its managed roots.",
        "addCollider":
            "3dmodel.md section 9: 'LOD0 visual meshes must never be assigned "
            "directly to production MeshCollider components.' Generate Colliders "
            "does exactly that. Collision comes from the COL_ proxy in the same "
            "FBX.",
        "importAnimation":
            "Exported with bake_anim=False; there is no animation data.",
        "animationType":
            "Static geometry. An Animator on a generated prop would also breach "
            "the Zero-GC Scatter protocol.",
        "importBlendShapes":
            "None present; matches the existing project importer policy.",
        "importVisibility":
            "Blender visibility flags are authoring state, not runtime truth; "
            "matches the existing project importer policy.",
        "importCameras":
            "preview.py builds a camera into the same scene. object_types={'MESH'} "
            "already excludes it, and this is the second gate.",
        "importLights":
            "Same reason as cameras: preview.py builds lights.",
        "importConstraints":
            "No rigging in a generated static package.",
        "preserveHierarchy":
            "The _LOD0/_LOD1/_LOD2 and COL_ nodes must stay separate children for "
            "the LODGroup and the collider binding to work.",
        "sortHierarchyByName":
            "Off: the authored node order already reflects LOD order, and "
            "resorting would make the imported hierarchy depend on naming rather "
            "than on the manifest.",
    }

    collider_expectation = {
        "proxyNamePrefix": law.COLLIDER_PREFIX,
        "visualNamePrefixes": [law.VISUAL_PREFIX, law.LOD_PREFIX],
        "convexTriangleMax": law.COLLIDER_CONVEX_TRI_MAX,
        "meshColliderConvex": True,
        "meshColliderOnLod0": False,
        "defaultCollision": (
            "none" if resolved in law.FAMILIES_WITHOUT_DEFAULT_COLLISION
            else "convex proxy or primitive compound"),
        "physicsLayer": _FAMILY_LAYER[resolved],
        "physicsLayerNote":
            "Layer name read from ProjectSettings/TagManager.asset. AGENTS.md "
            "forbids changing Tags/Layers without explicit instruction, so the "
            "authoring script must resolve it by name and fail loudly if absent -- "
            "it must not create one. COMMON_SENSE.md rule 2 additionally requires "
            "every raycast against these colliders to pass an explicit "
            "LayerMask.GetMask(...) plus QueryTriggerInteraction.Ignore.",
        "interactionAnchors": list(law.INTERACTION_ANCHORS),
        "interactionAnchorNote":
            "PROCEDURAL_ASSET_PIPELINE.md 'Collision And Interaction Package': "
            "anchors must be serialised, never discovered by runtime scene search.",
    }

    lod_expectation = {
        "createLodGroupExplicitly": True,
        "childSuffixPattern": "_LOD<n>",
        "nameTemplate": law.NAME_MESH,
        "requiredLevels": [0, 1, 2],
        "budgets": {
            "lod0": law.LOD_BUDGETS[resolved].lod0,
            "lod1": law.LOD_BUDGETS[resolved].lod1,
            "lod2": law.LOD_BUDGETS[resolved].lod2,
            "impostorMin": law.LOD_BUDGETS[resolved].impostor_min,
            "impostorMax": law.LOD_BUDGETS[resolved].impostor_max,
        },
        "fadeMode": "CrossFade",
        "animateCrossFading": False,
        "why":
            "3dmodel.md section 7 requires a complete LOD0/LOD1/LOD2 chain and "
            "'LOD switching must use hysteresis and dithered cross-fade where the "
            "renderer supports it. Alpha-blended cross-fade is forbidden for dense "
            "flora/coral on MX350 because it creates overdraw.' CrossFade with "
            "animateCrossFading=false is the dithered path, not alpha blend. "
            "AGENTS.md requires a 3-5 m or 2-3 s hysteresis band on any LOD "
            "switch. Build the LODGroup explicitly: relying on importer "
            "auto-detection risks HectonFBXPostprocessor generating its own "
            "__AUTO_LOD1/__AUTO_LOD2 above 2000 triangles.",
    }

    texture_expectation = {
        "albedo": {"compression": "BC7", "sRGB": True, "mips": True},
        "normal": {"compression": "BC5", "sRGB": False, "mips": True,
                   "textureType": "NormalMap"},
        "mrao": {"compression": "BC7", "sRGB": False, "mips": True,
                 "channels": "R=Metallic G=Roughness-or-Smoothness B=AO A=Emission"},
        "namePrefix": law.NAME_TEXTURE,
        "why":
            "AGENTS.md Visual And Asset Discipline: 'Textures default to BC7 for "
            "albedo/roughness/AO and BC5 for normals where applicable.' "
            "3DMODEL_TEXTURES_MATERIALS.md section 3 and section 8: albedo sRGB "
            "true, normal NormalMap type with sRGB false, masks sRGB false, mips "
            "enabled for world textures, and the manifest must state whether G is "
            "roughness or smoothness rather than guessing.",
    }

    return {
        "family": resolved.value,
        "surfaceClass": surface.value,
        "modelImporter": importer,
        "why": why,
        "collider": collider_expectation,
        "lodGroup": lod_expectation,
        "textureImport": texture_expectation,
        "vertexColorContract": list(law.VCOL_CONTRACT[surface]),
        "exportSettingsUsed": _serialisable_settings(),
        # RESOLVED 2026-07-29, and the stale entry mattered: this list used to open
        # with "OnPreprocessModel forces importNormals=Calculate for every FBX under
        # Assets/_Project/Art ... loses its authored weighted split normals
        # silently." That was true when it was written and is false now. The
        # postprocessor has since grown a complete forge carve-out
        # (HectonFBXPostprocessor.cs:43 schema, :50 MESH_ prefix, :401-429 sets
        # importNormals=Import for a forge asset, :181-196 logs a missing LODGroup
        # against the manifest instead of decimating). A warning nobody re-checked
        # is the most plausible reason every generator kept writing into
        # Docs/AgentLogs, which .gitignore:201 ignores - so the output reached
        # neither Unity nor git for the whole life of this pipeline.
        "knownProjectConflicts": [
            "RESOLVED: the importNormals=Calculate clobber no longer applies to "
            "forge packages. HectonFBXPostprocessor.cs:401-429 sets "
            "importNormals=Import when the sibling manifest declares "
            "h8forge.manifest/1 with unityImport.modelImporter.importNormals == "
            "'Import'. Requires the manifest to sit in the SAME directory named "
            "MANIFEST_<stem>.json and the mesh file to start with upper-case "
            "'MESH_' (TryResolveForgeManifestPath, :702-736). Both hold by "
            "construction from law.NAME_MESH and law.NAME_MANIFEST.",
            "STILL TRUE, and it does not apply to Assets/ScifiFacility, which "
            "TryResolveForgeManifestPath excludes at :715 so a manifest cannot "
            "weaken third-party quarantine policy.",
            "The postprocessor forces "
            "materialLocation=ModelImporterMaterialLocation.InPrefab, which "
            "conflicts with materialImportMode=None and the shared MAT_* policy.",
            "HectonBakeryUvAudit.RunAudit() enables generateSecondaryUV and "
            "reimports, which overwrites authored UV1. Now sharper than when it "
            "was written: Hecton_KelpMaster reads its height and width masks from "
            "UV1 as of 2026-07-29, so this audit does not merely overwrite a "
            "lightmap set, it would overwrite the sway parameterisation.",
            "HectonFBXPostprocessor.OnPostprocessModel builds a fallback LODGroup "
            "with its own decimation above 2000 triangles when no LODGroup is "
            "present. Mitigated for forge packages by the :181-196 branch, which "
            "logs the manifest path instead - but only when the manifest parses.",
        ],
        "proofStatus": PENDING_MARKER,
        "proofStatusNote":
            "Every value above is derived from the bibles and from measured FBX "
            "content. None of it has been applied in Unity by this module: no "
            "Unity import log, no Console output and no visual capture exists for "
            "it.",
    }


def _serialisable_settings() -> dict:
    """EXPORT_SETTINGS with the set() value turned into a sorted list for JSON."""
    out = {}
    for key, value in EXPORT_SETTINGS.items():
        out[key] = sorted(value) if isinstance(value, set) else value
    out["fbxUnitsPerMetre"] = FBX_UNITS_PER_METRE
    out["fbxHeaderUnitScaleFactor"] = FBX_HEADER_UNIT_SCALE_FACTOR
    out["blenderToFbxAxes"] = "(x, y, z) -> (x, z, -y)"
    out["blenderToUnityAxes"] = "(x, y, z) -> (x, z, y)"
    return out


# ---------------------------------------------------------------------------
# Manifest
# ---------------------------------------------------------------------------

def manifest_filename(family, name: str) -> str:
    """``law.NAME_MANIFEST`` plus ``.json``, so no caller hardcodes the template."""
    resolved = family if isinstance(family, law.Family) else law.Family(family)
    return law.NAME_MANIFEST.format(family=resolved.value, name=name) + ".json"


def _project_relative(path: str, outside: list) -> str:
    """Project-relative, forward-slashed path.

    ``AGENTS.md`` ``[RULE] Relative Path Requirement``: "Hardcoding absolute
    developer paths ... is strictly banned. All screenshot, log, config, and data
    directories must be resolved relatively from the project root." A manifest is a
    durable artefact, so an absolute ``C:\\Users\\...`` inside it is the same
    violation as one in source. Anything genuinely outside the repo is reduced to
    its basename and reported, never emitted whole.
    """
    if not path:
        return ""
    try:
        root = law.project_root()
    except RuntimeError:
        outside.append(os.path.basename(path))
        return os.path.basename(path)
    absolute = os.path.abspath(path)
    relative = os.path.relpath(absolute, root)
    if relative.startswith(".."):
        outside.append(os.path.basename(absolute))
        return os.path.basename(absolute)
    return relative.replace("\\", "/")


def _mesh_entry(item, outside: list) -> dict:
    """Normalise one mesh record.

    Accepts a ``validate.MeshReport`` (duck-typed, so validate.py stays
    uncoupled), a ``mesh_ops.LodLevel``, or a plain dict. MeshReport is the
    interesting case: it already carries every field 3dmodel.md section 10 wants in
    the proof artefact.
    """
    if isinstance(item, dict):
        entry = dict(item)
        if "path" in entry:
            entry["path"] = _project_relative(entry["path"], outside)
        return entry

    name = getattr(item, "name", None)
    if name is None:
        obj = getattr(item, "obj", None)
        name = getattr(obj, "name", "") if obj is not None else ""
    lod = getattr(item, "lod_index", None)
    if lod is None:
        lod = getattr(item, "index", -1)
    match = _LOD_SUFFIX.search(str(name))
    if (lod is None or lod < 0) and match is not None:
        lod = int(match.group(1))

    entry = {
        "name": str(name),
        "lod": int(lod) if lod is not None else -1,
        "triangles": int(getattr(item, "triangle_count",
                                 getattr(item, "triangles", -1))),
        "vertices": int(getattr(item, "vertex_count", -1)),
        "submeshes": int(getattr(item, "submesh_count", -1)),
        "uvLayers": list(getattr(item, "uv_layers", ()) or ()),
        "colorLayers": list(getattr(item, "color_layers", ()) or ()),
        "hasTangentBasis": bool(getattr(item, "has_tangent_basis", False)),
        "boundsMin": list(getattr(item, "bounds_min", ()) or ()),
        "boundsMax": list(getattr(item, "bounds_max", ()) or ()),
        "digest": str(getattr(item, "digest", "") or ""),
        "validatorVersion": str(getattr(item, "validator_version", "") or ""),
    }
    budget = getattr(item, "budget", None)
    if budget is not None:
        entry["lodBudget"] = int(budget)
        entry["withinBudget"] = bool(getattr(item, "within_budget",
                                             entry["triangles"] <= int(budget)))
    failures = getattr(item, "failures", None)
    if failures is not None:
        entry["validation"] = {
            "passed": bool(getattr(item, "passed", not failures)),
            "failures": [str(f) for f in failures],
            "warnings": [str(w) for w in (getattr(item, "warnings", ()) or ())],
        }
    return entry


def _collider_entry(item, outside: list) -> dict:
    if isinstance(item, dict):
        entry = dict(item)
        if "path" in entry:
            entry["path"] = _project_relative(entry["path"], outside)
        return entry
    obj = getattr(item, "obj", None)
    triangles = int(getattr(item, "triangles", -1))
    return {
        "name": getattr(obj, "name", "") if obj is not None else "",
        "kind": str(getattr(item, "kind", "unknown")),
        "triangles": triangles,
        "triangleBudget": law.COLLIDER_CONVEX_TRI_MAX,
        "withinBudget": bool(getattr(item, "within_budget",
                                     triangles <= law.COLLIDER_CONVEX_TRI_MAX)),
        "reason": str(getattr(item, "reason", "") or ""),
    }


def _named_entry(item, outside: list, kind: str) -> dict:
    if isinstance(item, dict):
        entry = dict(item)
        if "path" in entry:
            entry["path"] = _project_relative(entry["path"], outside)
        return entry
    if isinstance(item, str):
        return {"name": os.path.basename(item),
                "path": _project_relative(item, outside)} \
            if (os.sep in item or "/" in item) else {"name": item}
    material = getattr(item, "name", None)
    if material is not None:
        return {"name": str(material)}
    raise TypeError("cannot record {0} entry of type {1}".format(
        kind, type(item).__name__))


def write_manifest(
    path: str,
    identity,
    meshes,
    materials,
    textures,
    colliders,
    proof_paths,
    *,
    export_result: Optional[ExportResult] = None,
    uv_summary: Optional[dict] = None,
    alpha_meaning: str = "",
    lod_exempt: bool = False,
    extra: Optional[dict] = None,
) -> str:
    """Write the package manifest as JSON and return the path.

    ``PROCEDURAL_ASSET_PIPELINE.md`` "Proof Artifacts" opens with "A generator
    report that only says 'created assets' is invalid", so this function does not
    accept a thin payload: a manifest missing a bible-required field is written with
    that field named in ``manifestGaps`` and ``productionReady`` forced to false --
    "If proof is missing, the asset is not production-ready, even if the prefab
    exists."

    The manifest carries no wall-clock timestamp. "Every procedural asset must be
    reproducible" and "No generated mesh may depend on ... wall-clock time"
    (Deterministic Source Contract); a time field would make two byte-identical
    packages produce different manifests and a different validation hash. Run
    timing belongs in the black-box dump and the task log.
    """
    if identity is None:
        raise ValueError(
            "manifest refused: PROCEDURAL_ASSET_PIPELINE.md 'Deterministic Source "
            "Contract' requires a GeneratorIdentity (seed, generator name and "
            "semantic version, GlobalQualityWeight, family, scale in metres, "
            "camera distance class, platform lane)")
    meshes = list(meshes or ())
    if not meshes:
        raise ValueError(
            "manifest refused: no mesh records. 'Required Output Package' lists "
            "MESH_<Family>_<Name>_LOD0/1/2 as mandatory content")

    outside = []
    gaps = []

    identity_block = identity.as_dict() if hasattr(identity, "as_dict") \
        else dict(identity)
    for required in ("seed", "generator", "generatorVersion", "qualityWeight",
                     "family", "scaleMeters", "cameraDistanceClass",
                     "platformLane"):
        value = identity_block.get(required)
        if value is None or (isinstance(value, str) and not value.strip()):
            gaps.append("identity." + required)
    if not identity_block.get("sourceReferences"):
        gaps.append("identity.sourceReferences (source texture/reference IDs)")

    family = identity_block.get("family", law.Family.SMALL_PROP.value)
    try:
        resolved_family = law.Family(family)
    except ValueError:
        raise ValueError("manifest refused: unknown family " + repr(family))
    surface = law.FAMILY_SURFACE_CLASS[resolved_family]

    mesh_entries = [_mesh_entry(m, outside) for m in meshes]
    collider_entries = [_collider_entry(c, outside) for c in (colliders or ())]
    material_entries = [_named_entry(m, outside, "material")
                        for m in (materials or ())]
    texture_entries = [_named_entry(t, outside, "texture")
                       for t in (textures or ())]
    proofs = [_project_relative(p, outside) for p in (proof_paths or ())]

    if not material_entries:
        gaps.append("materials (MAT_<Family>_<SurfaceRole>, 3dmodel.md section 6 "
                    "slot 0 is mandatory)")
    if not texture_entries:
        gaps.append("textures (TX_<Family>_<Set>_<Role>; "
                    "3DMODEL_TEXTURES_MATERIALS.md section 2: 'Missing texture is "
                    "fatal unless the generator is explicitly producing a "
                    "placeholder diagnostic asset')")
    if not proofs:
        gaps.append("proofPaths (screenshot or render capture; "
                    "PROCEDURAL_ASSET_PIPELINE.md 'Proof Artifacts')")
    if uv_summary is None:
        gaps.append("uvSummary (UV density and atlas utilisation summary; "
                    "3DMODEL_TEXTURES_MATERIALS.md section 4 texelDensity and "
                    "stretchRatio)")
    if not collider_entries:
        if resolved_family in law.FAMILIES_WITHOUT_DEFAULT_COLLISION:
            pass  # 3DMODEL_FLORA_CORAL.md section 7: default flora collision is none.
        else:
            gaps.append("colliders (3dmodel.md section 9 requires a COL_ proxy for "
                        "this family)")
    if surface is law.SurfaceClass.ORGANIC and not alpha_meaning.strip():
        gaps.append("alphaMeaning (3dmodel.md section 5: the alpha channel meaning "
                    "'must be documented in the asset manifest')")

    lod_indices = sorted(set(e["lod"] for e in mesh_entries if e["lod"] >= 0))
    budgets = law.LOD_BUDGETS[resolved_family]
    missing_lods = [i for i in (0, 1, 2) if i not in lod_indices]
    if missing_lods and not lod_exempt:
        gaps.append("lodChain (missing LOD{0}; 3dmodel.md section 7)".format(
            ", LOD".join(str(i) for i in missing_lods)))

    monotonic = True
    previous = None
    for index in lod_indices:
        current = min(e["triangles"] for e in mesh_entries if e["lod"] == index)
        if previous is not None and current >= previous:
            monotonic = False
        previous = current

    mesh_failures = 0
    for entry in mesh_entries:
        validation = entry.get("validation")
        if validation is not None and not validation.get("passed", True):
            mesh_failures += len(validation.get("failures", ()))

    payload = {
        "schema": MANIFEST_SCHEMA,
        "exporterVersion": EXPORTER_VERSION,
        "forgeVersion": law.FORGE_VERSION,
        "identity": identity_block,
        "surfaceClass": surface.value,
        "naming": {
            "mesh": law.NAME_MESH,
            "material": law.NAME_MATERIAL,
            "texture": law.NAME_TEXTURE,
            "collider": law.NAME_COLLIDER,
            "prefab": law.NAME_PREFAB_GENERATED,
            "manifest": law.NAME_MANIFEST,
            "namingNote":
                "Templates come from law.py, which follows AGENTS.md 'Project "
                "Shape' (generated prefabs GEN_*, textures TX_*). "
                "PROCEDURAL_ASSET_PIPELINE.md 'Required Output Package' spells the "
                "same two artefacts PF_<Family>_<Name>.prefab and "
                "TEX_<Family>_<AtlasOrUnique>_<Role>.png; root AGENTS.md outranks "
                "it per the authority spine, so GEN_ and TX_ are used. Flagged for "
                "the lead rather than resolved here.",
        },
        "vertexColorContract": {
            "channels": list(law.VCOL_CONTRACT[surface]),
            "alphaMeaning": alpha_meaning,
            "exportedColorSpace": EXPORT_SETTINGS["colors_type"],
            "note":
                "Exported with colors_type='LINEAR'. Measured in the written file: "
                "an authored linear 0.25 is stored as 0.25016 with LINEAR and as "
                "0.53725 with SRGB. Unity copies the raw FBX float into "
                "Mesh.colors32 without a colour conversion, so SRGB would gamma "
                "warp every mask channel.",
        },
        "meshes": mesh_entries,
        "lod": {
            "levels": lod_indices,
            "exempt": bool(lod_exempt),
            "monotonic": monotonic,
            "budgets": {
                "lod0": budgets.lod0, "lod1": budgets.lod1, "lod2": budgets.lod2,
                "impostorMin": budgets.impostor_min,
                "impostorMax": budgets.impostor_max,
            },
            "triangleCountsPerLod": {
                "LOD{0}".format(index): min(
                    e["triangles"] for e in mesh_entries if e["lod"] == index)
                for index in lod_indices
            },
        },
        "materials": material_entries,
        "materialSlotContract": {
            "slot0": "primary structural/tissue",
            "slot1": "exposed cut, bevel, edge, scar, fracture",
            "slot2": "secondary trim, gasket, barnacle, mineral vein, growth plate",
            "slot3": "emissive/bioluminescent/details",
            "maxSlots": law.MATERIAL_SLOT_MAX,
        },
        "textures": texture_entries,
        "colliders": collider_entries,
        "colliderSummary": {
            "count": len(collider_entries),
            "types": sorted(set(c.get("kind", "unknown")
                                for c in collider_entries)),
            "triangleBudget": law.COLLIDER_CONVEX_TRI_MAX,
            "allWithinBudget": all(c.get("withinBudget", False)
                                   for c in collider_entries)
            if collider_entries else None,
        },
        "uvSummary": uv_summary if uv_summary is not None else {
            "status": "NOT_MEASURED",
            "required":
                "texelDensity, stretchRatio, island count, atlas rect utilisation, "
                "padding and edge bleed (3DMODEL_TEXTURES_MATERIALS.md sections 4 "
                "and 5; law.UV_STRETCH_MAX_HERO / UV_TEXEL_MISMATCH_MAX / "
                "UV_MIN_ISLAND_PIXELS / ATLAS_PADDING_PX hold the thresholds)",
        },
        "export": {
            "fbx": _project_relative(
                export_result.fbx_path if export_result else "", outside),
            "objectNames": list(export_result.object_names) if export_result else [],
            "unitScaleFbxUnitsPerMetre":
                export_result.unit_scale if export_result else FBX_UNITS_PER_METRE,
            "hasVertexColors":
                bool(export_result.has_vertex_colors) if export_result else None,
            "hasCustomNormals":
                bool(export_result.has_custom_normals) if export_result else None,
            "hasTangents":
                bool(export_result.has_tangents) if export_result else None,
            "uvLayerNames":
                list(export_result.uv_layer_names) if export_result else [],
            "roundtripVerified":
                bool(export_result.roundtrip_verified) if export_result else False,
            "settings": _serialisable_settings(),
        },
        "unityImport": unity_import_notes(resolved_family),
        "validation": {
            "meshGateFailures": mesh_failures,
            "passed": mesh_failures == 0 and monotonic and not missing_lods,
            "validatorVersion": next(
                (e.get("validatorVersion") for e in mesh_entries
                 if e.get("validatorVersion")), ""),
            "note":
                "Mesh, LOD-chain and collider gates are owned by validate.py and "
                "must have run before this manifest was written "
                "(PROCEDURAL_ASSET_PIPELINE.md 'Validation Before Save'). This "
                "block reports their result; it does not re-run them.",
        },
        "proof": {
            "paths": proofs,
            "roundtripNotes":
                list(export_result.roundtrip_notes) if export_result else [],
            "status": PENDING_MARKER,
            "statusNote":
                "Static and Blender-side evidence only. No Unity import log, "
                "Console output, Frame Debugger capture, profiler capture or "
                "in-engine screenshot exists for this package.",
        },
        "manifestGaps": gaps,
        "productionReady": not gaps and mesh_failures == 0,
    }
    if outside:
        payload["pathsOutsideProjectRoot"] = sorted(set(outside))
    if extra:
        payload["extra"] = extra

    # Deterministic hash over the whole payload minus the hash fields themselves.
    canonical = json.dumps(payload, sort_keys=True, separators=(",", ":"),
                           ensure_ascii=True)
    digest = hashlib.blake2b(canonical.encode("utf-8"), digest_size=16).hexdigest()
    payload["validationHashAlgorithm"] = "blake2b-128 over the canonical JSON of " \
                                         "every other field, sorted keys"
    payload["validationHash"] = digest

    path = os.path.abspath(path)
    directory = os.path.dirname(path)
    if directory:
        os.makedirs(directory, exist_ok=True)
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(payload, handle, indent=1, sort_keys=True)
        handle.write("\n")
    return path
