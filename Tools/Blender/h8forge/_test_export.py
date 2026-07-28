"""Self-test for ``h8forge.export_unity``. Proves the FBX, not the file's existence.

Run:

    blender.exe -b --factory-startup -P Tools/Blender/h8forge/_test_export.py

``AGENTS.md`` ``[RULE] Never Trust Automated Assertions Alone``: "Exit Code 0 or the
presence of a screenshot file does NOT prove the interface is functional." An
``os.path.isfile`` assertion on an FBX is that same non-proof, so every claim below
is measured against re-imported geometry or against the bytes of the written file.

The test object is asymmetric on purpose. A cube proves nothing: it survives an axis
swap, a mirror and a 90 degree rotation without changing its bounding box or its
vertex set. This mesh is an L-shaped prism with distinct extents on all three axes
plus a single spike vertex that is the unique farthest point from the centroid, so
any permutation or sign flip moves a measurable landmark.

Negative controls are the point of several cases. A verifier that cannot fail is not
a verifier, so the deliberately wrong axis combination, the mirrored transform, the
n-gon tangent drop and the missing-stream guards all get a case that asserts the
failure fires.

The FBX is also read directly with Blender's bundled ``io_scene_fbx.parse_fbx``, which
is how the tangent-layer presence, the header axes and the centimetre unit scale are
proven rather than inferred from a successful re-import.
"""

import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
_PACKAGE_PARENT = os.path.dirname(_HERE)
if _PACKAGE_PARENT not in sys.path:
    sys.path.insert(0, _PACKAGE_PARENT)

import bpy  # noqa: E402  (Blender supplies this at runtime)
import bmesh  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from bpy_extras.io_utils import axis_conversion  # noqa: E402

from io_scene_fbx import parse_fbx  # noqa: E402  (bundled with Blender)

from h8forge import law  # noqa: E402
from h8forge import export_unity as X  # noqa: E402
from h8forge.blackbox import BlackBox, GenerationAborted  # noqa: E402

# preview.py already writes under Docs/AgentLogs; blackbox.py dumps there too.
# AGENTS.md relative-path rule: derived from law.project_root(), never hardcoded.
OUT_DIR = os.path.join(law.project_root(), "Docs", "AgentLogs",
                       "ForgeExportSelfTest")

PROP = law.Family.SMALL_PROP
HARD = law.SurfaceClass.HARD_SURFACE

# The spike. Distinct magnitudes, distinct signs, and further from the centroid
# than any other vertex by a wide margin.
SPIKE = (1.30, -0.30, 1.75)
# L profile in the Blender XY plane. Asymmetric in both axes.
PROFILE = ((0.0, 0.0), (0.9, 0.0), (0.9, 0.3), (0.35, 0.3), (0.35, 0.75),
           (0.0, 0.75))
Z_LO, Z_HI = 0.0, 1.4
CUSTOM_NORMAL_TILT_DEG = 25.0


class Harness(object):
    def __init__(self):
        self.passed = 0
        self.failures = []

    def check(self, condition, label):
        if condition:
            self.passed += 1
        else:
            self.failures.append(label)
            sys.stdout.write("  FAIL  " + label + "\n")
        return bool(condition)

    def case(self, name):
        sys.stdout.write("[case] " + name + "\n")
        sys.stdout.flush()

    def info(self, text):
        sys.stdout.write("        " + text + "\n")
        sys.stdout.flush()

    def report(self):
        sys.stdout.write("\n" + "=" * 72 + "\n")
        sys.stdout.write("checks passed: {0}   failed: {1}\n".format(
            self.passed, len(self.failures)))
        if self.failures:
            for line in self.failures:
                sys.stdout.write("  FAILED: " + line + "\n")
            sys.stdout.write("RESULT: FAIL\n")
            return 1
        sys.stdout.write("RESULT: PASS\n")
        return 0


H = Harness()


# ---------------------------------------------------------------------------
# Builders
# ---------------------------------------------------------------------------

def _clear_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    # Flush the depsgraph: view_layer.objects otherwise keeps None holes where the
    # removed objects were, and the next iteration over it raises.
    bpy.context.view_layer.update()


def build_probe(name, *, triangulate=True, uv_layers=2, color=True,
                custom_normals=True, spike=True):
    """L-prism with a spike, two UV layers, a BYTE_COLOR/CORNER 'Col' attribute.

    ``BYTE_COLOR``/``CORNER`` and the attribute name ``Col`` are not arbitrary: they
    are exactly what ``vertexcolor.ensure_color_attribute`` produces, and its
    docstring explains why ("matches the bible's declared vertex layout
    Color | UNorm8 x4 ... A FLOAT_COLOR/POINT attribute survives Blender but changes
    the exported layout"). Testing a different layout would test nothing.
    """
    verts = [(x, y, Z_LO) for (x, y) in PROFILE]
    verts += [(x, y, Z_HI) for (x, y) in PROFILE]
    n = len(PROFILE)
    faces = [tuple(range(n - 1, -1, -1)), tuple(range(n, 2 * n))]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, j + n, i + n))

    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    if spike:
        mesh.vertices[n + 1].co = SPIKE
        mesh.update()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)

    if triangulate:
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        bm.to_mesh(mesh)
        bm.free()
        mesh.update()

    names = ("UVMap", "UVLightmap")
    for index in range(uv_layers):
        layer = mesh.uv_layers.new(name=names[index])
        for loop_index, loop in enumerate(mesh.loops):
            co = mesh.vertices[loop.vertex_index].co
            if index == 0:
                layer.data[loop_index].uv = (0.10 + 0.30 * co.x,
                                             0.20 + 0.30 * co.y)
            else:
                layer.data[loop_index].uv = (0.55 + 0.20 * co.z,
                                             0.65 + 0.10 * co.x)

    if color:
        attribute = mesh.color_attributes.new(name="Col", type="BYTE_COLOR",
                                              domain="CORNER")
        for loop_index, loop in enumerate(mesh.loops):
            co = mesh.vertices[loop.vertex_index].co
            attribute.data[loop_index].color = _authored_color(co)
        mesh.color_attributes.active_color = attribute
        mesh.attributes.active_color_name = "Col"
        mesh.attributes.default_color_name = "Col"

    mesh.materials.append(None)
    mesh.calc_loop_triangles()
    bpy.context.view_layer.update()

    if custom_normals:
        # Tilt every corner normal by a known angle about world X. The tilt is what
        # makes the survival claim falsifiable: a recalculated normal would come
        # back as the geometric face normal, which differs from this by 25 degrees.
        rotation = Matrix.Rotation(math.radians(CUSTOM_NORMAL_TILT_DEG), 3, "X")
        mesh.normals_split_custom_set(
            [tuple(rotation @ Vector(nrm.vector)) for nrm in mesh.corner_normals])
    return obj


def _authored_color(co):
    """Known, monotonic gradient per channel. A swap or a gamma shift is visible.

    Channel meanings follow law.HARD_SURFACE_VCOL: R edge wear, G oxidation,
    B baked AO, A emission mask. The values here are a deterministic function of
    position so any expected value can be recomputed rather than remembered.
    """
    return (co.z / 1.8, co.x / 1.4, 0.25 + 0.5 * (co.y / 0.8), 1.0)


def geometric_corner_normals(obj):
    """Corner normals the mesh would have with no custom normals, in world space."""
    mesh = obj.data
    matrix = obj.matrix_world.to_3x3().inverted_safe().transposed()
    out = []
    for polygon in mesh.polygons:
        for _ in polygon.loop_indices:
            out.append((matrix @ polygon.normal).normalized())
    return out


def world_corner_normals(obj):
    mesh = obj.data
    matrix = obj.matrix_world.to_3x3().inverted_safe().transposed()
    return [(matrix @ Vector(nrm.vector)).normalized()
            for nrm in mesh.corner_normals]


def fbx_facts(path):
    """Header axes, unit scale, layer-element presence, read from the file."""
    root, version = parse_fbx.parse(path, use_namedtuple=True)
    facts = {"version": version, "layers": {}, "header": {}}
    wanted_layers = ("LayerElementNormal", "LayerElementTangent",
                     "LayerElementBinormal", "LayerElementColor",
                     "LayerElementUV")

    def walk(elem):
        ident = elem.id.decode("ascii", "replace")
        if ident == "P" and elem.props:
            key = elem.props[0]
            key = key.decode("utf-8", "replace") if isinstance(key, bytes) else key
            if key in ("UnitScaleFactor", "UpAxis", "UpAxisSign", "FrontAxis",
                       "FrontAxisSign", "CoordAxis", "CoordAxisSign"):
                facts["header"][key] = elem.props[-1]
        if ident in wanted_layers:
            names = []
            for child in elem.elems:
                if child.id.decode("ascii", "replace") == "Name":
                    value = child.props[0]
                    names.append(value.decode("utf-8", "replace")
                                 if isinstance(value, bytes) else value)
            facts["layers"].setdefault(ident, []).extend(names or [""])
        if ident == "Vertices" and elem.props:
            facts["maxAbsCoord"] = max(abs(v) for v in elem.props[0])
            facts["vertexFloats"] = len(elem.props[0])
        if ident == "PolygonVertexIndex" and elem.props:
            facts["polygons"] = sum(1 for v in elem.props[0] if v < 0)
        for child in elem.elems:
            walk(child)

    walk(root)
    return facts


def import_raw_axes(path):
    """Import with an identity axis conversion: world space == raw FBX axis space."""
    before = set(o.name for o in bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, use_manual_orientation=True,
                             axis_forward="Y", axis_up="Z",
                             use_custom_normals=True, colors_type="LINEAR",
                             use_image_search=False)
    return [o for o in bpy.data.objects
            if o.name not in before and o.type == "MESH"]


def import_matched(path):
    before = set(o.name for o in bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, use_custom_normals=True,
                             colors_type="LINEAR", use_image_search=False)
    return [o for o in bpy.data.objects
            if o.name not in before and o.type == "MESH"]


def out(name):
    return os.path.join(OUT_DIR, name)


# ---------------------------------------------------------------------------
# Pure maths: the axis claim before any file is written
# ---------------------------------------------------------------------------

def case_axis_conversion_never_mirrors():
    H.case("axis_conversion is always a proper rotation (no combination mirrors)")
    combos = (("-Z", "Y"), ("Z", "Y"), ("Y", "Z"), ("-Y", "Z"), ("Z", "-Y"))
    for forward, up in combos:
        m = axis_conversion(from_forward="Y", from_up="Z", to_forward=forward,
                            to_up=up)
        H.check(abs(m.determinant() - 1.0) < 1e-9,
                "axis_conversion({0},{1}) determinant must be +1, got {2:+.6f}"
                .format(forward, up, m.determinant()))
    chosen = axis_conversion(
        from_forward="Y", from_up="Z",
        to_forward=X.EXPORT_SETTINGS["axis_forward"],
        to_up=X.EXPORT_SETTINGS["axis_up"])
    H.info("chosen matrix maps X->{0} Y->{1} Z->{2}".format(
        tuple(round(v, 3) for v in chosen @ Vector((1, 0, 0))),
        tuple(round(v, 3) for v in chosen @ Vector((0, 1, 0))),
        tuple(round(v, 3) for v in chosen @ Vector((0, 0, 1)))))
    H.check((chosen @ Vector((0, 0, 1)) - Vector((0, 1, 0))).length < 1e-6,
            "Blender +Z (up) must map to FBX +Y (up), got "
            + str(tuple(round(v, 4) for v in chosen @ Vector((0, 0, 1)))))
    H.check((chosen @ Vector((0, 1, 0)) - Vector((0, 0, -1))).length < 1e-6,
            "Blender +Y (forward) must map to FBX -Z, got "
            + str(tuple(round(v, 4) for v in chosen @ Vector((0, 1, 0)))))
    H.check((chosen @ Vector((1, 0, 0)) - Vector((1, 0, 0))).length < 1e-6,
            "Blender +X must map to FBX +X unchanged")
    # The wrong combination is a rotation too, which is exactly why it is
    # dangerous: it produces a 180 degree yaw, not a visible mirror.
    wrong = axis_conversion(from_forward="Z", from_up="Y")
    H.check((wrong @ Vector((1, 0, 0)) - Vector((-1, 0, 0))).length < 1e-6,
            "axis_forward='Z' must flip X, which is the silent 180 degree yaw")


def case_pure_axis_maps():
    H.case("blender_to_fbx_axes / blender_to_unity")
    v = (1.30, -0.30, 1.75)
    H.check(X.blender_to_fbx_axes(v) == (1.30, 1.75, 0.30),
            "blender_to_fbx_axes must give (x, z, -y), got "
            + str(X.blender_to_fbx_axes(v)))
    H.check(X.blender_to_unity(v) == (1.30, 1.75, -0.30),
            "blender_to_unity must give (x, z, y), got " + str(X.blender_to_unity(v)))
    # Determinant of the composed Blender->Unity map must be -1: that is the
    # handedness change, and it is the reason Unity flips winding.
    basis = Matrix((X.blender_to_unity((1, 0, 0)), X.blender_to_unity((0, 1, 0)),
                    X.blender_to_unity((0, 0, 1)))).transposed()
    H.check(abs(basis.determinant() + 1.0) < 1e-9,
            "Blender->Unity map determinant must be -1 (handedness flip), got "
            + str(basis.determinant()))
    H.info("Blender->Unity determinant {0:+.1f}: coordinate handedness changes, "
           "geometry does not mirror".format(basis.determinant()))


# ---------------------------------------------------------------------------
# The main round trip
# ---------------------------------------------------------------------------

_MAIN = {}


def case_export_roundtrip():
    H.case("export + round trip: counts, streams, landmark, chirality")
    _clear_scene()
    obj = build_probe("MESH_SmallProp_Probe_LOD0")
    mesh = obj.data
    mesh.calc_loop_triangles()
    before = {
        "verts": len(mesh.vertices),
        "loops": len(mesh.loops),
        "tris": len(mesh.loop_triangles),
        "uvs": tuple(l.name for l in mesh.uv_layers),
        "colors": [tuple(c for c in mesh.color_attributes["Col"].data[i].color)
                   for i in range(len(mesh.loops))],
        "normals": world_corner_normals(obj),
        "geometric": geometric_corner_normals(obj),
    }
    H.info("source: verts={verts} loops={loops} tris={tris} uv={uvs}".format(**before))

    scene_objects = len(bpy.data.objects)
    scene_meshes = len(bpy.data.meshes)
    scene_materials = len(bpy.data.materials)

    box = BlackBox("export_selftest", "main")
    result = X.export_fbx([obj], out("probe.fbx"), blackbox=box)
    _MAIN["result"] = result
    _MAIN["before"] = before
    _MAIN["path"] = result.fbx_path

    for line in result.roundtrip_notes:
        H.info(line)

    H.check(result.roundtrip_verified,
            "round trip must be verified, notes: " + " | ".join(
                result.roundtrip_notes))
    H.check(result.object_names == ("MESH_SmallProp_Probe_LOD0",),
            "object_names, got " + str(result.object_names))
    H.check(result.triangle_counts.get("MESH_SmallProp_Probe_LOD0") == before["tris"],
            "triangle_counts must match the source {0}, got {1}".format(
                before["tris"], result.triangle_counts))
    H.check(result.has_vertex_colors, "has_vertex_colors must be True")
    H.check(result.has_custom_normals, "has_custom_normals must be True")
    H.check(result.has_tangents, "has_tangents must be True")
    H.check(result.uv_layer_names == ("UVMap", "UVLightmap"),
            "uv_layer_names must keep order, got " + str(result.uv_layer_names))
    # The re-imported object scale is a float32, so 1/0.01 lands a few ULPs off
    # 100. A relative tolerance is the honest bound; demanding exact equality would
    # be a test asserting float precision rather than the unit contract.
    H.check(abs(result.unit_scale - X.FBX_UNITS_PER_METRE)
            / X.FBX_UNITS_PER_METRE < 1e-5,
            "measured unit_scale must be {0:g} fbx units per metre, got {1}".format(
                X.FBX_UNITS_PER_METRE, result.unit_scale))

    # The verification step must not pollute the scene it verified.
    H.check(len(bpy.data.objects) == scene_objects,
            "round trip leaked objects: {0} -> {1}".format(
                scene_objects, len(bpy.data.objects)))
    H.check(len(bpy.data.meshes) == scene_meshes,
            "round trip leaked meshes: {0} -> {1}".format(
                scene_meshes, len(bpy.data.meshes)))
    H.check(len(bpy.data.materials) == scene_materials,
            "round trip leaked materials: {0} -> {1}".format(
                scene_materials, len(bpy.data.materials)))
    H.check(bpy.data.collections.get("H8_ExportRoundTrip") is None,
            "round trip left its sandbox collection behind")
    H.check(box.total_recorded > 0, "black box must have recorded the export")


def case_reimport_streams():
    H.case("re-imported streams: counts, colour values, UV order, normals")
    path = _MAIN.get("path")
    if not H.check(path is not None, "main export must have run first"):
        return
    before = _MAIN["before"]
    imported = import_matched(path)
    if not H.check(len(imported) == 1,
                   "one mesh object expected, got " + str([o.name for o in imported])):
        return
    obj = imported[0]
    mesh = obj.data
    mesh.calc_loop_triangles()

    H.check(len(mesh.vertices) == before["verts"],
            "vertex count {0} -> {1}".format(before["verts"], len(mesh.vertices)))
    H.check(len(mesh.loop_triangles) == before["tris"],
            "triangle count {0} -> {1}".format(before["tris"],
                                               len(mesh.loop_triangles)))
    H.check(len(mesh.loops) == before["loops"],
            "loop count {0} -> {1}".format(before["loops"], len(mesh.loops)))
    H.check(tuple(l.name for l in mesh.uv_layers) == before["uvs"],
            "uv layer names/order {0} -> {1}".format(
                before["uvs"], tuple(l.name for l in mesh.uv_layers)))
    H.check(len(mesh.uv_layers) == 2,
            "both UV layers must arrive (TexCoord0 + TexCoord1)")

    # Colour: sampled indices printed with real numbers, max delta over all loops.
    if H.check(len(mesh.color_attributes) >= 1, "colour attribute must survive"):
        attribute = mesh.color_attributes[0]
        H.check(attribute.name == "Col",
                "colour attribute name must stay 'Col', got " + attribute.name)
        after = [tuple(c for c in attribute.data[i].color)
                 for i in range(len(mesh.loops))]
        worst = 0.0
        for u, v in zip(before["colors"], after):
            for a, b in zip(u, v):
                worst = max(worst, abs(a - b))
        for index in (0, 7, 23, len(after) // 2, len(after) - 1):
            H.info("colour loop[{0}] {1} -> {2}".format(
                index, tuple(round(c, 5) for c in before["colors"][index]),
                tuple(round(c, 5) for c in after[index])))
        H.check(worst <= X.TOL_COLOR,
                "colour max delta {0:.7f} must be within {1:g}".format(
                    worst, X.TOL_COLOR))
        H.info("colour max delta over all {0} loops = {1:.7f}".format(
            len(after), worst))
        # A sanity check that the gradient is real, not a flat fill that would
        # round trip trivially.
        reds = [c[0] for c in after]
        H.check(max(reds) - min(reds) > 0.4,
                "the authored red gradient must span more than 0.4, got {0:.3f}"
                .format(max(reds) - min(reds)))

    # Normals: survived AND are the custom ones, not recalculated geometrics.
    after_normals = world_corner_normals(obj)
    if H.check(len(after_normals) == len(before["normals"]),
               "corner normal count {0} -> {1}".format(
                   len(before["normals"]), len(after_normals))):
        worst = max((a - b).length for a, b in zip(before["normals"],
                                                  after_normals))
        H.check(worst <= X.TOL_NORMAL,
                "corner normal max delta {0:.7f} must be within {1:g}".format(
                    worst, X.TOL_NORMAL))
        H.info("world corner-normal max delta = {0:.7f}".format(worst))
        # The falsifiable half. If Blender had recalculated normals on import, each
        # corner would come back as its geometric face normal. Assert instead that
        # each one equals rotation @ geometricNormal, i.e. exactly the authored
        # custom normal. Note the tilt is about X, so corners whose face normal is
        # parallel to X are legitimately unmoved -- comparing against the rotated
        # geometric normal handles that, where a blanket "must be 25 degrees off"
        # assertion would not.
        rotation = Matrix.Rotation(math.radians(CUSTOM_NORMAL_TILT_DEG), 3, "X")
        worst_expected = 0.0
        angles = []
        for imported, geometric in zip(after_normals, before["geometric"]):
            expected = (rotation @ geometric).normalized()
            worst_expected = max(worst_expected, (imported - expected).length)
            angles.append(math.acos(max(-1.0, min(1.0, imported.dot(geometric)))))
        H.check(worst_expected <= X.TOL_NORMAL,
                "every imported corner normal must equal rotation({0:g} deg, X) @ "
                "geometricNormal, i.e. the authored custom normal; worst deviation "
                "{1:.7f} exceeds {2:g}. A match against the plain geometric normal "
                "would mean the importer recalculated them.".format(
                    CUSTOM_NORMAL_TILT_DEG, worst_expected, X.TOL_NORMAL))
        tilted = [a for a in angles if a > math.radians(20.0)]
        H.check(len(tilted) > len(angles) // 3,
                "the authored tilt must be detectable on most corners, otherwise "
                "this case cannot distinguish custom from recalculated normals; "
                "only {0} of {1} corners are more than 20 degrees off".format(
                    len(tilted), len(angles)))
        H.info("imported normals sit up to {0:.3f} degrees off the geometric face "
               "normal ({1} of {2} corners tilted; corners with an X-parallel face "
               "normal are unaffected by a rotation about X)".format(
                   math.degrees(max(angles)), len(tilted), len(angles)))
        H.check(getattr(mesh, "has_custom_normals", False),
                "the re-imported mesh must report has_custom_normals=True")
    _clear_scene()


def case_axis_and_landmark():
    H.case("axis convention: where a Blender +Z vertex lands in FBX space")
    path = _MAIN.get("path")
    if not H.check(path is not None, "main export must have run first"):
        return
    _clear_scene()
    source = build_probe("MESH_SmallProp_Probe_LOD0")
    src_mesh = source.data
    src_world = [source.matrix_world @ v.co for v in src_mesh.vertices]
    centroid = Vector((0, 0, 0))
    for p in src_world:
        centroid += p
    centroid /= len(src_world)
    ranked = sorted(src_world, key=lambda p: (p - centroid).length, reverse=True)
    landmark = ranked[0]
    margin = (ranked[0] - centroid).length - (ranked[1] - centroid).length
    H.check((landmark - Vector(SPIKE)).length < 1e-6,
            "the farthest-from-centroid vertex must be the spike {0}, got {1}"
            .format(SPIKE, tuple(round(c, 4) for c in landmark)))
    H.check(margin > 0.2,
            "landmark margin over the runner-up must be decisive, got {0:.4f} m"
            .format(margin))
    H.info("landmark {0}, centroid {1}, margin {2:.4f} m".format(
        tuple(round(c, 4) for c in landmark),
        tuple(round(c, 4) for c in centroid), margin))

    # The highest vertex in Blender is the +Z witness. After the conversion it must
    # be the highest along FBX +Y.
    top_blender = max(src_world, key=lambda p: p.z)
    # Every scalar the rest of this case needs must be read before the datablocks
    # go away; holding a Mesh reference across _clear_scene raises ReferenceError.
    src_volume = _signed_volume_world(src_mesh, src_world)
    _clear_scene()

    raw = import_raw_axes(path)
    if not H.check(len(raw) == 1, "raw-axis import must give one object"):
        return
    obj = raw[0]
    raw_world = [obj.matrix_world @ v.co for v in obj.data.vertices]
    raw_centroid = Vector((0, 0, 0))
    for p in raw_world:
        raw_centroid += p
    raw_centroid /= len(raw_world)
    raw_ranked = sorted(raw_world, key=lambda p: (p - raw_centroid).length,
                        reverse=True)
    raw_landmark = raw_ranked[0]

    expected_offset = Vector(X.blender_to_fbx_axes(landmark - centroid))
    actual_offset = raw_landmark - raw_centroid
    # Direction only: the raw import still carries the file's centimetre unit
    # factor on the object scale, so magnitudes are scale-dependent and directions
    # are not.
    delta = (expected_offset.normalized() - actual_offset.normalized()).length
    H.info("fbx-space landmark offset expected direction {0}, measured {1}, "
           "delta {2:.7f}".format(
               tuple(round(c, 5) for c in expected_offset.normalized()),
               tuple(round(c, 5) for c in actual_offset.normalized()), delta))
    H.check(delta < X.TOL_DIRECTION,
            "the landmark must appear in FBX space at the axis-converted "
            "direction; delta {0:.6f} exceeds {1:g}".format(delta,
                                                            X.TOL_DIRECTION))

    # Bounding-box extents are an independent witness of the axis permutation.
    def extents(points):
        xs = [p.x for p in points]
        ys = [p.y for p in points]
        zs = [p.z for p in points]
        return (max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))

    src_extent = extents(src_world)
    raw_extent = extents(raw_world)
    H.info("extents: Blender {0} -> FBX space {1}".format(
        tuple(round(c, 4) for c in src_extent),
        tuple(round(c, 4) for c in raw_extent)))
    H.check(abs(raw_extent[0] - src_extent[0]) < 1e-3,
            "X extent must pass through unchanged")
    H.check(abs(raw_extent[1] - src_extent[2]) < 1e-3,
            "Blender Z extent must become the FBX Y extent (Z-up -> Y-up)")
    H.check(abs(raw_extent[2] - src_extent[1]) < 1e-3,
            "Blender Y extent must become the FBX Z extent")

    # +Z witness: the topmost Blender vertex must be the topmost FBX-Y vertex.
    top_raw = max(raw_world, key=lambda p: p.y)
    expected_top = Vector(X.blender_to_fbx_axes(top_blender))
    H.info("Blender topmost vertex {0} -> expected fbx {1}, measured {2}".format(
        tuple(round(c, 4) for c in top_blender),
        tuple(round(c, 4) for c in expected_top),
        tuple(round(c, 4) for c in top_raw)))
    H.check((expected_top.normalized() - top_raw.normalized()).length < 1e-3,
            "a vertex at Blender +Z must end up at FBX +Y")

    # Chirality: the axis conversion is a rotation, so the signed volume sign is
    # invariant. A negative ratio here would mean the file is mirrored.
    raw_volume = _signed_volume_world(obj.data, raw_world)
    H.info("signed volume: Blender {0:+.7f} -> FBX space {1:+.7f} (sign "
           "{2})".format(src_volume, raw_volume,
                         "preserved" if src_volume * raw_volume > 0 else "FLIPPED"))
    H.check(src_volume * raw_volume > 0.0,
            "chirality must survive: signed volume sign flipped, meaning the FBX "
            "content is mirrored relative to the source")
    H.info("Unity will additionally negate FBX Z and flip winding; net Blender"
           "->Unity map is (x, y, z) -> (x, z, y), determinant -1, so the "
           "coordinate handedness changes and the geometry does not mirror")
    _clear_scene()


def _signed_volume_world(mesh, world_positions):
    mesh.calc_loop_triangles()
    total = 0.0
    for t in mesh.loop_triangles:
        a, b, c = (world_positions[i] for i in t.vertices)
        total += a.dot(b.cross(c))
    return total / 6.0


def case_file_contents():
    H.case("written file: header axes, centimetre unit scale, layer elements")
    path = _MAIN.get("path")
    if not H.check(path is not None, "main export must have run first"):
        return
    facts = fbx_facts(path)
    H.info("header {0}".format(facts["header"]))
    H.info("layers {0}".format({k: v for k, v in facts["layers"].items()}))
    H.info("maxAbsCoord={0} vertexFloats={1} polygons={2}".format(
        facts.get("maxAbsCoord"), facts.get("vertexFloats"),
        facts.get("polygons")))
    header = facts["header"]
    H.check(header.get("UpAxis") == 1 and header.get("UpAxisSign") == 1,
            "header must declare Y up, got UpAxis={0} sign={1}".format(
                header.get("UpAxis"), header.get("UpAxisSign")))
    H.check(header.get("CoordAxis") == 0 and header.get("CoordAxisSign") == 1,
            "header must declare +X right")
    H.check(abs(float(header.get("UnitScaleFactor", -1))
                - X.FBX_HEADER_UNIT_SCALE_FACTOR) < 1e-9,
            "header UnitScaleFactor must be {0:g} (centimetres), got {1}".format(
                X.FBX_HEADER_UNIT_SCALE_FACTOR, header.get("UnitScaleFactor")))
    # The spike sits at Blender z=1.75 m, so the file must hold 175 units.
    H.check(abs(facts.get("maxAbsCoord", 0.0) - SPIKE[2] * X.FBX_UNITS_PER_METRE)
            < 1e-3,
            "geometry must be written in centimetres: expected max coordinate "
            "{0:g}, file holds {1}".format(SPIKE[2] * X.FBX_UNITS_PER_METRE,
                                           facts.get("maxAbsCoord")))
    H.check(len(facts["layers"].get("LayerElementNormal", ())) >= 1,
            "the file must carry a normal layer")
    H.check(facts["layers"].get("LayerElementTangent") == ["UVMap", "UVLightmap"],
            "the file must carry a tangent layer per UV set, got "
            + str(facts["layers"].get("LayerElementTangent")))
    H.check(facts["layers"].get("LayerElementBinormal") == ["UVMap", "UVLightmap"],
            "the file must carry the matching binormal layers")
    H.check(facts["layers"].get("LayerElementColor") == ["Col"],
            "the file must carry the 'Col' colour layer, got "
            + str(facts["layers"].get("LayerElementColor")))
    H.check(facts["layers"].get("LayerElementUV") == ["UVMap", "UVLightmap"],
            "the file must carry both UV layers in order, got "
            + str(facts["layers"].get("LayerElementUV")))


# ---------------------------------------------------------------------------
# The production shading basis
# ---------------------------------------------------------------------------

def case_weighted_normal_basis_survives():
    """The basis mesh_ops actually authors, not an artificial tilt.

    ``case_reimport_streams`` rotates flat face normals by a known angle. That is
    falsifiable, but synthetic. This case builds the basis the way
    ``mesh_ops.apply_shading_basis`` does after its headless fix -- polygons marked
    smooth at data level, edges above ``law.SMOOTH_ANGLE_DEG`` marked sharp, then
    WEIGHTED_NORMAL in FACE_AREA_WITH_ANGLE with keep_sharp -- and proves that basis
    reaches the FBX intact.

    The distinction is the whole point. ``bpy.ops.object.shade_auto_smooth`` returns
    ``{'CANCELLED'}`` under ``-b --factory-startup``, so a test that leaned on the
    operator route would have been round-tripping FLAT shading and calling it proof.
    This case therefore asserts, before exporting anything, that the basis is
    neither flat nor uniformly smooth: some corners must differ from their face
    normal, and some vertices must carry split normals. Either assertion failing
    means the case has degenerated and proves nothing.
    """
    H.case("production shading basis (smooth + sharp edges + WEIGHTED_NORMAL)")
    _clear_scene()
    obj = build_probe("MESH_SmallProp_Weighted_LOD0", custom_normals=False)
    mesh = obj.data

    for polygon in mesh.polygons:
        polygon.use_smooth = True
    threshold = math.radians(law.SMOOTH_ANGLE_DEG)
    edge_faces = {}
    for polygon in mesh.polygons:
        for key in polygon.edge_keys:
            edge_faces.setdefault(key, []).append(polygon)
    sharp = 0
    for edge in mesh.edges:
        faces = edge_faces.get(edge.key, ())
        if len(faces) != 2:
            continue
        dot = max(-1.0, min(1.0, faces[0].normal.dot(faces[1].normal)))
        if math.acos(dot) > threshold:
            edge.use_edge_sharp = True
            sharp += 1
    mesh.update()
    H.check(sharp > 0,
            "the control mesh must have edges above law.SMOOTH_ANGLE_DEG ({0:g} "
            "deg) to mark sharp, marked {1}".format(law.SMOOTH_ANGLE_DEG, sharp))
    H.info("marked {0} of {1} edges sharp above {2:g} degrees".format(
        sharp, len(mesh.edges), law.SMOOTH_ANGLE_DEG))

    modifier = obj.modifiers.new(name="H8_WeightedNormal", type="WEIGHTED_NORMAL")
    modifier.mode = "FACE_AREA_WITH_ANGLE"
    modifier.weight = 50
    modifier.keep_sharp = True
    bpy.context.view_layer.update()

    # Read the EVALUATED result: that is what the exporter writes with
    # apply_modifiers=True, and what export_fbx measures.
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    eval_mesh = evaluated.to_mesh()
    eval_mesh.calc_loop_triangles()
    normal_matrix = obj.matrix_world.to_3x3().inverted_safe().transposed()
    eval_normals = [(normal_matrix @ Vector(n.vector)).normalized()
                    for n in eval_mesh.corner_normals]
    face_normals = []
    for polygon in eval_mesh.polygons:
        for _ in polygon.loop_indices:
            face_normals.append((normal_matrix @ polygon.normal).normalized())
    has_custom = bool(getattr(eval_mesh, "has_custom_normals", False))
    per_vertex = {}
    for loop_index, loop in enumerate(eval_mesh.loops):
        per_vertex.setdefault(loop.vertex_index, []).append(eval_normals[loop_index])
    split_vertices = 0
    for vectors in per_vertex.values():
        if any((vectors[0] - v).length > 1e-3 for v in vectors[1:]):
            split_vertices += 1
    off_face = sum(1 for a, b in zip(eval_normals, face_normals)
                   if (a - b).length > 1e-3)
    evaluated.to_mesh_clear()

    H.info("evaluated basis: has_custom_normals={0}; {1} of {2} corners differ from "
           "their face normal; {3} of {4} vertices carry split normals".format(
               has_custom, off_face, len(eval_normals), split_vertices,
               len(per_vertex)))
    H.check(off_face > 0,
            "the weighted-normal basis must actually smooth something. Every corner "
            "still equals its face normal, which means this case is round-tripping "
            "FLAT shading and proves nothing about custom normals.")
    H.check(split_vertices > 0,
            "keep_sharp must leave at least one vertex with split normals; without "
            "a split this case cannot distinguish an authored basis from a uniform "
            "recalculation")

    result = X.export_fbx([obj], out("weighted_basis.fbx"))
    for line in result.roundtrip_notes:
        if "corner-normal" in line or "roundtrip VERIFIED" in line:
            H.info(line)
    H.check(result.roundtrip_verified,
            "the weighted-normal basis must round-trip: "
            + " | ".join(result.roundtrip_notes))

    # Independent check outside the module: compare the re-imported world-space
    # corner normals against the evaluated source ones.
    imported = import_matched(result.fbx_path)
    if H.check(len(imported) == 1, "one object expected from the re-import"):
        after = world_corner_normals(imported[0])
        if H.check(len(after) == len(eval_normals),
                   "corner count {0} -> {1}".format(len(eval_normals), len(after))):
            worst = max((a - b).length for a, b in zip(eval_normals, after))
            H.check(worst <= X.TOL_NORMAL,
                    "the authored weighted/split basis must survive; worst corner "
                    "deviation {0:.7f} exceeds {1:g}".format(worst, X.TOL_NORMAL))
            H.info("weighted-normal basis max corner deviation after round trip = "
                   "{0:.7f}".format(worst))
            after_face = geometric_corner_normals(imported[0])
            still_off = sum(1 for a, b in zip(after, after_face)
                            if (a - b).length > 1e-3)
            H.check(still_off == off_face,
                    "the same {0} corners must still differ from their face normal "
                    "after the round trip, got {1}. A drop toward zero means the "
                    "importer recalculated flat.".format(off_face, still_off))
            H.info("{0} corners still smoothed after the round trip (source had "
                   "{1})".format(still_off, off_face))
    _clear_scene()


# ---------------------------------------------------------------------------
# Negative controls
# ---------------------------------------------------------------------------

def _export_with(objects, path, **overrides):
    """Raw operator call with EXPORT_SETTINGS plus overrides. Test-only."""
    bpy.context.view_layer.update()
    view_layer = bpy.context.view_layer
    for other in view_layer.objects:
        if other is not None and other.select_get():
            other.select_set(False)
    for obj in objects:
        obj.select_set(True)
    view_layer.objects.active = objects[0]
    settings = dict(X.EXPORT_SETTINGS)
    settings.update(overrides)
    if os.path.exists(path):
        os.remove(path)
    return bpy.ops.export_scene.fbx(filepath=path, **settings)


def case_colors_type_matters():
    H.case("colors_type: LINEAR keeps the mask value, SRGB gamma-warps it")
    _clear_scene()
    obj = build_probe("MESH_SmallProp_ColourProbe_LOD0")
    mesh = obj.data
    authored = tuple(c for c in mesh.color_attributes["Col"].data[0].color)  # noqa

    linear_path = out("colour_linear.fbx")
    srgb_path = out("colour_srgb.fbx")
    _export_with([obj], linear_path, colors_type="LINEAR")
    _export_with([obj], srgb_path, colors_type="SRGB")
    _clear_scene()

    def file_value(path):
        # Import both files as LINEAR: the importer then writes the file's raw
        # numbers into a FLOAT_COLOR attribute, so what is read back IS what Unity
        # will copy into Mesh.colors32.
        imported = import_matched(path)
        value = tuple(c for c in imported[0].data.color_attributes[0].data[0].color)
        _clear_scene()
        return value

    linear_value = file_value(linear_path)
    srgb_value = file_value(srgb_path)
    H.info("authored (scene-linear) loop[0] = {0}".format(
        tuple(round(c, 5) for c in authored)))
    H.info("file value with colors_type=LINEAR = {0}".format(
        tuple(round(c, 5) for c in linear_value)))
    H.info("file value with colors_type=SRGB   = {0}".format(
        tuple(round(c, 5) for c in srgb_value)))

    worst_linear = max(abs(a - b) for a, b in zip(authored, linear_value))
    H.check(worst_linear <= X.TOL_COLOR,
            "LINEAR must preserve the authored mask value; delta {0:.7f} exceeds "
            "{1:g}".format(worst_linear, X.TOL_COLOR))

    def to_srgb(value):
        if value <= 0.0031308:
            return value * 12.92
        return 1.055 * (value ** (1.0 / 2.4)) - 0.055

    worst_curve = 0.0
    biggest_shift = 0.0
    for index in range(3):
        worst_curve = max(worst_curve,
                          abs(to_srgb(linear_value[index]) - srgb_value[index]))
        biggest_shift = max(biggest_shift,
                            abs(srgb_value[index] - linear_value[index]))
    H.check(worst_curve < 1e-3,
            "the SRGB file value must be exactly the sRGB transfer of the LINEAR "
            "one; residual {0:.6f}".format(worst_curve))
    H.check(biggest_shift > 0.05,
            "the two settings must differ materially, otherwise this proves "
            "nothing; largest channel shift was only {0:.5f}".format(biggest_shift))
    H.info("SRGB shifts a channel by up to {0:.5f} -- that is the gamma warp a "
           "sway/AO mask would ship with".format(biggest_shift))


def case_ngon_drops_tangents_without_triangulation():
    H.case("negative control: use_tspace silently drops tangents on n-gons")
    _clear_scene()
    ngon = build_probe("MESH_SmallProp_Ngon_LOD0", triangulate=False)
    sides = max(len(p.vertices) for p in ngon.data.polygons)
    H.check(sides > 4,
            "the control mesh must actually contain an n-gon, max sides " + str(sides))

    no_tri = out("ngon_no_triangulate.fbx")
    with_tri = out("ngon_triangulated.fbx")
    _export_with([ngon], no_tri, use_triangles=False)
    _export_with([ngon], with_tri, use_triangles=True)

    facts_off = fbx_facts(no_tri)
    facts_on = fbx_facts(with_tri)
    H.info("use_triangles=False -> tangent layers {0}".format(
        facts_off["layers"].get("LayerElementTangent")))
    H.info("use_triangles=True  -> tangent layers {0}".format(
        facts_on["layers"].get("LayerElementTangent")))
    H.check(facts_off["layers"].get("LayerElementTangent") is None,
            "without triangulation the n-gon mesh must ship with NO tangent layer; "
            "if this starts passing tangents, revisit the use_triangles reasoning")
    H.check(facts_on["layers"].get("LayerElementTangent") == ["UVMap", "UVLightmap"],
            "with triangulation the tangent layers must be present, got "
            + str(facts_on["layers"].get("LayerElementTangent")))
    H.check(X.EXPORT_SETTINGS["use_triangles"] is True,
            "EXPORT_SETTINGS must keep use_triangles=True for this reason")
    _clear_scene()


def case_wrong_axes_is_detected():
    H.case("negative control: the wrong axis pair fails verification")
    _clear_scene()
    obj = build_probe("MESH_SmallProp_BadAxes_LOD0")
    path = out("bad_axes.fbx")
    _export_with([obj], path, **X._WRONG_AXES_CONTROL)
    report = X.verify_fbx_roundtrip([obj], path)
    axis_failures = [f for f in report.failures if "axis map wrong" in f]
    for line in report.failures:
        H.info("reported: " + line)
    H.check(not report.passed,
            "verification must reject an FBX exported with axis_up='Z'")
    H.check(axis_failures,
            "the failure must name the axis map, got " + str(report.failures))
    H.check(not report.axis_map_confirmed,
            "axis_map_confirmed must stay False for the wrong pair")
    # And the correct pair on the same mesh must pass, so this is not a verifier
    # that rejects everything.
    good = out("good_axes.fbx")
    _export_with([obj], good)
    good_report = X.verify_fbx_roundtrip([obj], good)
    H.check(good_report.passed,
            "the same mesh with the shipped settings must pass: "
            + "; ".join(good_report.failures))
    H.check(good_report.axis_map_confirmed and good_report.chirality_preserved,
            "the shipped settings must confirm both the axis map and chirality")
    _clear_scene()


def case_mirrored_transform_rejected():
    H.case("negative control: a negative object scale is refused")
    _clear_scene()
    obj = build_probe("MESH_SmallProp_Mirror_LOD0")
    obj.scale = (-1.0, 1.0, 1.0)
    bpy.context.view_layer.update()
    determinant = obj.matrix_world.to_3x3().determinant()
    H.check(determinant < 0.0,
            "the control object must actually have a negative determinant, got "
            + str(determinant))
    raised = None
    try:
        X.export_fbx([obj], out("mirrored.fbx"))
    except GenerationAborted as error:
        raised = error
    H.check(raised is not None,
            "export_fbx must refuse a mirrored transform; tangent handedness "
            "inverts with the winding and every normal map breaks")
    if raised is not None:
        text = " ".join(raised.failures) if raised.failures else str(raised)
        H.info("refused with: " + text[:220])
        H.check("determinant" in text and "normal map" in text,
                "the refusal must explain the mirror and its consequence")
    obj.scale = (1.0, 1.0, 1.0)
    _clear_scene()


def case_missing_stream_guards():
    H.case("negative control: missing UV0 and missing colour attribute are refused")
    _clear_scene()
    no_uv = build_probe("MESH_SmallProp_NoUV_LOD0", uv_layers=0)
    raised = None
    try:
        X.export_fbx([no_uv], out("no_uv.fbx"))
    except GenerationAborted as error:
        raised = error
    H.check(raised is not None, "a mesh with no UV layer must be refused")
    if raised is not None:
        H.check(any("TexCoord0" in f for f in raised.failures),
                "the refusal must cite the TexCoord0 requirement, got "
                + str(raised.failures))
    _clear_scene()

    no_color = build_probe("MESH_SmallProp_NoCol_LOD0", color=False)
    raised = None
    try:
        X.export_fbx([no_color], out("no_col.fbx"))
    except GenerationAborted as error:
        raised = error
    H.check(raised is not None, "a mesh with no colour attribute must be refused")
    if raised is not None:
        H.check(any("VCOL_CONTRACT" in f for f in raised.failures),
                "the refusal must cite law.VCOL_CONTRACT, got "
                + str(raised.failures))
    _clear_scene()


# ---------------------------------------------------------------------------
# LOD group
# ---------------------------------------------------------------------------

_LODS = {}


def case_lod_group():
    H.case("export_lod_group: LOD naming, collider node, budget notes")
    _clear_scene()
    levels = []
    for index in range(3):
        name = law.NAME_MESH.format(family=PROP.value, name="Crate", lod=index)
        obj = build_probe(name)
        if index > 0:
            # A real chain is monotonic; decimate so the notes have real numbers.
            modifier = obj.modifiers.new(name="H8_Decimate", type="DECIMATE")
            modifier.decimate_type = "COLLAPSE"
            modifier.ratio = 0.6 if index == 1 else 0.3
            modifier.use_collapse_triangulate = True
        levels.append(obj)

    collider = build_probe(
        law.COLLIDER_PREFIX + "{0}_Crate".format(PROP.value), custom_normals=False)

    identity = law.GeneratorIdentity(
        generator="_test_export", generator_version="1.0.0", seed=1337,
        quality_weight=0.75, family=PROP, scale_meters=1.75,
        camera_distance_class="near", platform_lane="compact",
        source_references=("TX_SmallProp_Crate_Albedo",))

    result = X.export_lod_group(levels, collider, out("crate_lodgroup.fbx"),
                                identity=identity)
    _LODS["result"] = result
    _LODS["identity"] = identity

    for line in result.roundtrip_notes:
        H.info(line)
    H.check(result.roundtrip_verified, "the LOD package must round-trip")
    H.check(len(result.object_names) == 4,
            "one file must hold LOD0/1/2 plus the collider, got "
            + str(result.object_names))
    H.check(any(n.startswith(law.COLLIDER_PREFIX) for n in result.object_names),
            "the collider node must be present and COL_ prefixed")
    for index in range(3):
        expected = law.NAME_MESH.format(family=PROP.value, name="Crate", lod=index)
        H.check(expected in result.object_names,
                "missing LOD node " + expected)
        H.check(expected.endswith("_LOD" + str(index)),
                "law.NAME_MESH must end in the _LOD<n> suffix Unity's LODGroup "
                "convention needs, got " + expected)
    notes = "\n".join(result.roundtrip_notes)
    H.check("vs law budget" in notes,
            "the notes must state each LOD against its law.LOD_BUDGETS limit")
    H.check("law.COLLIDER_CONVEX_TRI_MAX" in notes,
            "the notes must state the collider against its budget")
    H.check("LODGroup" in notes,
            "the notes must carry the Unity LODGroup authoring instruction")
    counts = [result.triangle_counts[
        law.NAME_MESH.format(family=PROP.value, name="Crate", lod=i)]
        for i in range(3)]
    H.info("LOD triangle counts {0}".format(counts))
    H.check(counts[0] > counts[1] > counts[2],
            "the exported chain must be monotonic, got " + str(counts))


def case_lod_naming_rejected():
    H.case("negative control: LOD names without the _LOD suffix are refused")
    _clear_scene()
    bad = [build_probe("MESH_SmallProp_Crate_High"),
           build_probe("MESH_SmallProp_Crate_Low")]
    raised = None
    try:
        X.export_lod_group(bad, None, out("bad_lod_names.fbx"))
    except GenerationAborted as error:
        raised = error
    H.check(raised is not None,
            "a chain that Unity cannot group must be refused, not shipped")
    if raised is not None:
        H.check("_LOD" in str(raised) and "law.NAME_MESH" in str(raised),
                "the refusal must point at law.NAME_MESH, got " + str(raised))
    _clear_scene()

    mismatched = [
        build_probe(law.NAME_MESH.format(family=PROP.value, name="A", lod=0)),
        build_probe(law.NAME_MESH.format(family=PROP.value, name="B", lod=1)),
    ]
    raised = None
    try:
        X.export_lod_group(mismatched, None, out("bad_lod_prefix.fbx"))
    except GenerationAborted as error:
        raised = error
    H.check(raised is not None,
            "LOD nodes with different name prefixes must be refused: Unity groups "
            "by the text before _LOD")
    _clear_scene()

    wrong_collider = [build_probe(
        law.NAME_MESH.format(family=PROP.value, name="C", lod=0))]
    proxy = build_probe("PROXY_C", custom_normals=False)
    raised = None
    try:
        X.export_lod_group(wrong_collider, proxy, out("bad_col_name.fbx"))
    except GenerationAborted as error:
        raised = error
    H.check(raised is not None,
            "a collider without the COL_ prefix must be refused (3dmodel.md "
            "section 9)")
    _clear_scene()


# ---------------------------------------------------------------------------
# Unity import contract
# ---------------------------------------------------------------------------

def case_unity_import_notes():
    H.case("unity_import_notes: values, citations and family divergence")
    for family in law.Family:
        notes = unity = X.unity_import_notes(family)
        importer = notes["modelImporter"]
        H.check(set(importer) <= set(notes["why"]),
                "{0}: every modelImporter key needs a citation, missing {1}".format(
                    family.value, sorted(set(importer) - set(notes["why"]))))
        for key, text in notes["why"].items():
            H.check(len(text.strip()) > 40,
                    "{0}: citation for {1} is too thin to be a citation".format(
                        family.value, key))
        H.check(importer["importNormals"] == "Import",
                family.value + ": importNormals must be Import, the generator owns "
                "the weighted split normals")
        H.check(importer["importTangents"] == "CalculateMikk",
                family.value + ": importTangents must be CalculateMikk")
        H.check(importer["meshCompression"] == "Off",
                family.value + ": meshCompression must be Off")
        H.check(importer["isReadable"] is False,
                family.value + ": isReadable must be False")
        H.check(importer["generateSecondaryUV"] is False,
                family.value + ": generateSecondaryUV must be False, it overwrites "
                "authored UV1")
        H.check(importer["addCollider"] is False,
                family.value + ": addCollider must be False")
        H.check(importer["materialImportMode"] == "None",
                family.value + ": materialImportMode must be None")
        H.check(importer["importColors"] is True,
                family.value + ": importColors must be True")
        H.check(abs(importer["globalScale"] - 1.0) < 1e-9
                and importer["useFileScale"] is True,
                family.value + ": scale contract must be globalScale=1 with "
                "useFileScale=True")
        H.check(abs(importer["normalSmoothingAngle"] - law.SMOOTH_ANGLE_DEG) < 1e-9,
                family.value + ": normalSmoothingAngle must be law.SMOOTH_ANGLE_DEG")
        H.check(notes["vertexColorContract"]
                == list(law.VCOL_CONTRACT[law.FAMILY_SURFACE_CLASS[family]]),
                family.value + ": vertex colour contract must come from law.py")
        H.check(notes["collider"]["convexTriangleMax"]
                == law.COLLIDER_CONVEX_TRI_MAX,
                family.value + ": collider budget must come from law.py")
        H.check(notes["lodGroup"]["budgets"]["lod0"] == law.LOD_BUDGETS[family].lod0,
                family.value + ": LOD budgets must come from law.py")
        H.check(notes["proofStatus"] == X.PENDING_MARKER,
                family.value + ": the notes must carry the pending marker")
        H.check(notes["textureImport"]["albedo"]["compression"] == "BC7"
                and notes["textureImport"]["normal"]["compression"] == "BC5",
                family.value + ": texture defaults must be BC7 albedo / BC5 normal")

    vat = X.unity_import_notes(law.Family.FAUNA)["modelImporter"]
    hard = X.unity_import_notes(law.Family.BASE_MODULE)["modelImporter"]
    H.check(vat["optimizeMeshVertices"] is False
            and vat["weldVertices"] is False
            and vat["meshOptimizationFlags"] == "PolygonOrder",
            "VAT families must not reorder or weld vertices: the VAT indexes by "
            "vertex id")
    H.check(hard["optimizeMeshVertices"] is True
            and hard["weldVertices"] is True
            and hard["meshOptimizationFlags"] == "Everything",
            "hard-surface families should take the full optimisation")
    H.info("VAT families {0} take PolygonOrder only".format(
        [f.value for f in X._VAT_FAMILIES]))

    flora = X.unity_import_notes(law.Family.FLORA)["collider"]
    H.check(flora["defaultCollision"] == "none",
            "flora default collision must be none (3DMODEL_FLORA_CORAL.md s7)")
    H.check(flora["physicsLayer"] == "Flora_NonColliding",
            "flora layer must be the real project layer, got "
            + flora["physicsLayer"])
    H.check(X.unity_import_notes(law.Family.BASE_MODULE)["collider"]["physicsLayer"]
            == "BaseModule",
            "base module layer must be the real project layer")
    conflicts = X.unity_import_notes(PROP)["knownProjectConflicts"]
    H.check(any("importNormals" in c and "HectonFBXPostprocessor" in c
                for c in conflicts),
            "the notes must warn about the postprocessor forcing "
            "importNormals=Calculate")
    H.check(any("generateSecondaryUV" in c for c in conflicts),
            "the notes must warn about the UV2 audit overwriting UV1")
    for conflict in conflicts:
        H.info("conflict: " + conflict[:150])


# ---------------------------------------------------------------------------
# Manifest
# ---------------------------------------------------------------------------

def case_manifest():
    H.case("write_manifest: required fields, gaps, determinism, relative paths")
    import json

    result = _LODS.get("result")
    identity = _LODS.get("identity")
    if not H.check(result is not None, "the LOD case must have run first"):
        return

    meshes = []
    for index in range(3):
        name = law.NAME_MESH.format(family=PROP.value, name="Crate", lod=index)
        meshes.append({
            "name": name,
            "lod": index,
            "triangles": result.triangle_counts[name],
            "vertices": -1,
            "submeshes": 1,
            "uvLayers": ["UVMap", "UVLightmap"],
            "colorLayers": ["Col"],
            "hasTangentBasis": True,
            "lodBudget": law.LOD_BUDGETS[PROP].limit(index),
            "withinBudget": True,
            "validatorVersion": "1.0.0",
            "validation": {"passed": True, "failures": [], "warnings": []},
        })
    colliders = [{
        "name": law.COLLIDER_PREFIX + "SmallProp_Crate",
        "kind": "convex",
        "triangles": result.triangle_counts[
            law.COLLIDER_PREFIX + "SmallProp_Crate"],
        "triangleBudget": law.COLLIDER_CONVEX_TRI_MAX,
        "withinBudget": True,
    }]
    materials = [{"name": law.NAME_MATERIAL.format(family=PROP.value,
                                                   role="Primary"), "slot": 0}]
    textures = [{"name": law.NAME_TEXTURE.format(family=PROP.value, set="Crate",
                                                 role="Albedo"),
                 "role": "albedo", "compression": "BC7", "sRGB": True},
                {"name": law.NAME_TEXTURE.format(family=PROP.value, set="Crate",
                                                 role="Normal"),
                 "role": "normal", "compression": "BC5", "sRGB": False}]
    proofs = [os.path.join(OUT_DIR, "crate_contactsheet.png")]
    uv_summary = {"texelDensityPixelsPerMetre": 512, "stretchRatioMax": 1.09,
                  "islandCount": 6, "atlasSize": 1024,
                  "atlasPaddingPx": law.atlas_padding_for(1024),
                  "atlasUtilisation": 0.71, "edgeBleed": True}

    path = X.write_manifest(
        os.path.join(OUT_DIR, X.manifest_filename(PROP, "Crate")),
        identity, meshes, materials, textures, colliders, proofs,
        export_result=result, uv_summary=uv_summary, alpha_meaning="emission_mask")
    H.check(os.path.isfile(path), "manifest must be written")
    H.info("manifest: " + os.path.relpath(path, law.project_root()))
    with open(path, "r", encoding="utf-8") as handle:
        payload = json.load(handle)

    # Every field PROCEDURAL_ASSET_PIPELINE.md "Proof Artifacts" and
    # "Deterministic Source Contract" name, checked by name.
    for key in ("schema", "identity", "meshes", "materials", "textures",
                "colliders", "colliderSummary", "lod", "uvSummary", "export",
                "unityImport", "validation", "proof", "validationHash",
                "validationHashAlgorithm", "vertexColorContract", "naming",
                "manifestGaps", "productionReady"):
        H.check(key in payload, "manifest must carry '" + key + "'")
    for key in ("seed", "generator", "generatorVersion", "qualityWeight", "family",
                "scaleMeters", "cameraDistanceClass", "platformLane",
                "sourceReferences"):
        H.check(key in payload["identity"],
                "Deterministic Source Contract field missing: identity." + key)
    H.check(payload["identity"]["seed"] == 1337, "the seed must be recorded")
    H.check(payload["lod"]["triangleCountsPerLod"].get("LOD0") is not None,
            "triangle counts per LOD are mandatory")
    H.check(payload["lod"]["monotonic"] is True,
            "the manifest must report the chain as monotonic, got "
            + str(payload["lod"]))
    H.check(payload["colliderSummary"]["count"] == 1
            and payload["colliderSummary"]["types"] == ["convex"],
            "collider count and type summary are mandatory")
    H.check(payload["vertexColorContract"]["alphaMeaning"] == "emission_mask",
            "the alpha channel meaning must be documented (3dmodel.md s5)")
    H.check(payload["proof"]["status"] == X.PENDING_MARKER,
            "the manifest must carry the pending-verification marker")
    H.check(len(payload["validationHash"]) == 32,
            "validationHash must be a 16-byte hex digest, got "
            + str(payload["validationHash"]))
    H.check(not payload["manifestGaps"],
            "a complete payload must report no gaps, got "
            + str(payload["manifestGaps"]))
    H.check(payload["productionReady"] is True,
            "a complete, passing package must be marked production ready")

    # No absolute developer path anywhere in the file.
    with open(path, "r", encoding="utf-8") as handle:
        raw = handle.read()
    root = law.project_root()
    H.check(root.replace("\\", "/") not in raw.replace("\\", "/"),
            "the manifest must not embed the absolute project root "
            "(AGENTS.md relative-path rule)")
    H.check("C:/Users" not in raw and "C:\\\\Users" not in raw,
            "the manifest must not embed a developer home path")
    H.check(payload["export"]["fbx"].startswith("Docs/"),
            "the fbx path must be project-relative and forward-slashed, got "
            + str(payload["export"]["fbx"]))
    H.check(payload["proof"]["paths"][0].startswith("Docs/"),
            "proof paths must be project-relative, got "
            + str(payload["proof"]["paths"]))

    # Determinism: same inputs, same bytes, same hash. No timestamp inside.
    second = X.write_manifest(
        os.path.join(OUT_DIR, "MANIFEST_repeat.json"),
        identity, meshes, materials, textures, colliders, proofs,
        export_result=result, uv_summary=uv_summary, alpha_meaning="emission_mask")
    with open(second, "r", encoding="utf-8") as handle:
        repeat = json.load(handle)
    H.check(repeat["validationHash"] == payload["validationHash"],
            "the same package must hash identically; a wall-clock field would "
            "break this")
    H.info("validationHash {0} reproduced on a second write".format(
        payload["validationHash"]))

    # A changed input must change the hash, otherwise the hash proves nothing.
    mutated = [dict(m) for m in meshes]
    mutated[0]["triangles"] += 1
    third = X.write_manifest(
        os.path.join(OUT_DIR, "MANIFEST_mutated.json"),
        identity, mutated, materials, textures, colliders, proofs,
        export_result=result, uv_summary=uv_summary, alpha_meaning="emission_mask")
    with open(third, "r", encoding="utf-8") as handle:
        changed = json.load(handle)
    H.check(changed["validationHash"] != payload["validationHash"],
            "a changed triangle count must change the validation hash")


def case_manifest_gaps():
    H.case("write_manifest: a thin payload is written but not production ready")
    import json

    identity = law.GeneratorIdentity(
        generator="_test_export", generator_version="1.0.0", seed=7,
        quality_weight=0.5, family=law.Family.FLORA, scale_meters=1.0,
        camera_distance_class="near", platform_lane="compact")
    path = X.write_manifest(
        os.path.join(OUT_DIR, "MANIFEST_thin.json"), identity,
        [{"name": "MESH_Flora_Kelp_LOD0", "lod": 0, "triangles": 900}],
        [], [], [], [])
    with open(path, "r", encoding="utf-8") as handle:
        payload = json.load(handle)
    gaps = payload["manifestGaps"]
    for line in gaps:
        H.info("gap: " + line[:130])
    H.check(payload["productionReady"] is False,
            "a payload missing bible-required proof must not be production ready")
    H.check(any(g.startswith("materials") for g in gaps),
            "missing materials must be reported")
    H.check(any(g.startswith("textures") for g in gaps),
            "missing textures must be reported")
    H.check(any(g.startswith("proofPaths") for g in gaps),
            "missing proof captures must be reported")
    H.check(any(g.startswith("uvSummary") for g in gaps),
            "a missing UV density/atlas summary must be reported")
    H.check(any(g.startswith("alphaMeaning") for g in gaps),
            "an organic family with no documented alpha meaning must be reported")
    H.check(any(g.startswith("lodChain") for g in gaps),
            "an incomplete LOD chain must be reported")
    H.check(any("sourceReferences" in g for g in gaps),
            "missing source references must be reported")
    # Flora has no default collision, so a missing collider is NOT a gap.
    H.check(not any(g.startswith("colliders") for g in gaps),
            "flora must not be asked for a collider (3DMODEL_FLORA_CORAL.md s7), "
            "gaps were " + str(gaps))

    raised = None
    try:
        X.write_manifest(os.path.join(OUT_DIR, "MANIFEST_empty.json"), identity,
                         [], [], [], [], [])
    except ValueError as error:
        raised = error
    H.check(raised is not None,
            "a manifest with no mesh records must be refused outright")
    raised = None
    try:
        X.write_manifest(os.path.join(OUT_DIR, "MANIFEST_noid.json"), None,
                         [{"name": "x", "lod": 0, "triangles": 1}], [], [], [], [])
    except ValueError as error:
        raised = error
    H.check(raised is not None,
            "a manifest with no GeneratorIdentity must be refused outright")


# ---------------------------------------------------------------------------
# Runner
# ---------------------------------------------------------------------------

CASES = (
    case_axis_conversion_never_mirrors,
    case_pure_axis_maps,
    case_export_roundtrip,
    case_reimport_streams,
    case_axis_and_landmark,
    case_file_contents,
    case_weighted_normal_basis_survives,
    case_colors_type_matters,
    case_ngon_drops_tangents_without_triangulation,
    case_wrong_axes_is_detected,
    case_mirrored_transform_rejected,
    case_missing_stream_guards,
    case_lod_group,
    case_lod_naming_rejected,
    case_unity_import_notes,
    case_manifest,
    case_manifest_gaps,
)


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    # AGENTS.md "Atomic File Delete Rule": clear the previous run's artefacts so a
    # stale file cannot be mistaken for this run's output.
    for entry in os.listdir(OUT_DIR):
        if entry.lower().endswith((".fbx", ".json")):
            os.remove(os.path.join(OUT_DIR, entry))
    sys.stdout.write("h8forge export_unity self-test\n")
    sys.stdout.write("blender {0}   output {1}\n\n".format(
        bpy.app.version_string, os.path.relpath(OUT_DIR, law.project_root())))
    for case in CASES:
        try:
            case()
        except Exception as error:  # noqa: BLE001 -- a crashed case is a failure
            import traceback
            traceback.print_exc()
            H.failures.append("{0} raised {1}: {2}".format(
                case.__name__, type(error).__name__, error))
    return H.report()


if __name__ == "__main__":
    sys.exit(main())
