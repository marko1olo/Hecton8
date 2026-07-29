"""Self-test for ``h8forge.validate``. Every gate must fire, and clean data must not.

Run:

    blender.exe -b --factory-startup -P Tools/Blender/h8forge/_test_validate.py

``AGENTS.md`` ``[RULE] Test-Driven Logic Verification (No Dead Variables)``: a
computed value has to be proven consumed. A validation gate that never fires is
the same defect, so each gate below gets a mesh built to violate it plus the
clean-case assertion that the gate stays silent on good geometry.

Each case declares the gates it must raise and the collateral gates it is allowed
to raise. Anything outside both sets fails the test, so a gate that over-triggers
is caught as loudly as one that under-triggers.

Two routes are exercised on purpose:

*   Blender datablocks, for everything Blender's mesh data model can express.
*   ``MeshData`` snapshots, for the vertex/index buffer states Blender structurally
    cannot express but the Unity writer can. Blender re-normalises its normal
    cache, always hands back unit tangents with a +-1 sign, saturates infinities
    to FLT_MAX on assignment, and hands out loop triangles that are triples by
    construction. Those gates are verified on the snapshot, which is the buffer
    the exporter actually writes.
"""

import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
_PACKAGE_PARENT = os.path.dirname(_HERE)
if _PACKAGE_PARENT not in sys.path:
    sys.path.insert(0, _PACKAGE_PARENT)

import bpy  # noqa: E402  (Blender supplies this at runtime)

from h8forge import law  # noqa: E402
from h8forge import validate as V  # noqa: E402
from h8forge.blackbox import BlackBox, GenerationAborted  # noqa: E402

NAN = float("nan")
ATLAS = 1024
HARD = law.SurfaceClass.HARD_SURFACE
PROP = law.Family.SMALL_PROP

# One padded square UV region per cube face, laid out 3 x 2. Square regions keep
# aspect distortion at zero for a square face; the 0.05 outer margin clears the
# 12 px border reserve law.atlas_padding_for(1024) demands.
_UV_CELL = 0.26
_UV_ORIGIN = 0.07
_UV_STEP_U = 0.30
_UV_STEP_V = 0.45


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


def gates_of(failures):
    return set(f.gate for f in failures)


def expect(label, failures, must, allow=()):
    got = gates_of(failures)
    missing = sorted(set(must) - got)
    extra = sorted(got - set(must) - set(allow))
    H.check(not missing, "{0}: gate(s) did not fire: {1}".format(label, missing))
    H.check(not extra, "{0}: unexpected gate(s): {1}".format(label, extra))
    for failure in failures:
        if failure.gate in must:
            H.check(bool(failure.detail.strip()),
                    "{0}: gate {1} has an empty detail".format(label,
                                                               failure.gate))
    return got


def detail_of(failures, gate):
    for failure in failures:
        if failure.gate == gate:
            return failure
    return None


# ---------------------------------------------------------------------------
# Mesh builders
# ---------------------------------------------------------------------------

_CUBE_FACES = (
    # (+X, -X, +Y, -Y, +Z, -Z), each wound counter-clockwise seen from outside.
    ((0.5, -0.5, -0.5), (0.5, 0.5, -0.5), (0.5, 0.5, 0.5), (0.5, -0.5, 0.5)),
    ((-0.5, -0.5, 0.5), (-0.5, 0.5, 0.5), (-0.5, 0.5, -0.5), (-0.5, -0.5, -0.5)),
    ((-0.5, 0.5, 0.5), (0.5, 0.5, 0.5), (0.5, 0.5, -0.5), (-0.5, 0.5, -0.5)),
    ((-0.5, -0.5, -0.5), (0.5, -0.5, -0.5), (0.5, -0.5, 0.5), (-0.5, -0.5, 0.5)),
    ((-0.5, -0.5, 0.5), (0.5, -0.5, 0.5), (0.5, 0.5, 0.5), (-0.5, 0.5, 0.5)),
    ((-0.5, 0.5, -0.5), (0.5, 0.5, -0.5), (0.5, -0.5, -0.5), (-0.5, -0.5, -0.5)),
)


def face_uv_region(face_index):
    col = face_index % 3
    row = face_index // 3
    u0 = _UV_ORIGIN + col * _UV_STEP_U
    v0 = _UV_ORIGIN + row * _UV_STEP_V
    return u0, v0, u0 + _UV_CELL, v0 + _UV_CELL


def new_mesh(name, verts, faces):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    return mesh


def add_uv(mesh, per_loop):
    layer = mesh.uv_layers.new(name="UVMap")
    for i in range(len(layer.data)):
        layer.data[i].uv = per_loop[i]
    return layer


def add_color(mesh, name, per_vertex):
    layer = mesh.color_attributes.new(name=name, type="FLOAT_COLOR",
                                      domain="POINT")
    for i in range(len(layer.data)):
        layer.data[i].color = per_vertex[i]
    return layer


def clean_cube(name="MESH_SmallProp_Clean_LOD0", *, color_name="edge_wear",
               color_values=None, material_slots=1, uv=True, color=True):
    """A cube that satisfies every gate: 24 split verts, 12 triangles.

    Split vertices give per-face unit normals and independent UV islands, which
    is what a bevelled hard-surface generator produces after smoothing-group
    splitting.
    """
    verts = []
    faces = []
    uvs = []
    for face_index in range(6):
        corners = _CUBE_FACES[face_index]
        base = len(verts)
        for corner in corners:
            verts.append(corner)
        faces.append((base, base + 1, base + 2, base + 3))
        u0, v0, u1, v1 = face_uv_region(face_index)
        uvs.extend([(u0, v0), (u1, v0), (u1, v1), (u0, v1)])
    mesh = new_mesh(name, verts, faces)
    if uv:
        add_uv(mesh, uvs)
    if color:
        if color_values is None:
            values = []
            for i in range(len(verts)):
                # Plausible wear data: finite, inside the UNorm8 range, with a
                # real anchor value in the red channel.
                values.append((i / 32.0, 0.25, 0.5 + (i % 3) * 0.1, 1.0))
        else:
            values = color_values
        add_color(mesh, color_name, values)
    for _ in range(material_slots):
        mesh.materials.append(None)
    return mesh


def folded_plate(name, faces):
    """Two triangles sharing edge 1-2, folded slightly so bounds are 3D.

    Shared vertex indices are the point: the directed-edge winding test can only
    see a flipped face where two faces share an edge, and a 24-vertex split cube
    shares none.
    """
    verts = [(0.0, 0.0, 0.0), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (1.0, 1.0, 0.01)]
    mesh = new_mesh(name, verts, faces)
    # Planar xy projection scaled uniformly: a uniform scale has zero aspect
    # distortion, so the UV gates stay quiet and only winding is under test.
    per_loop = []
    for polygon in mesh.polygons:
        for loop_index in polygon.loop_indices:
            vertex = mesh.loops[loop_index].vertex_index
            x, y = verts[vertex][0], verts[vertex][1]
            per_loop.append((_UV_ORIGIN + x * _UV_CELL, _UV_ORIGIN + y * _UV_CELL))
    add_uv(mesh, per_loop)
    add_color(mesh, "edge_wear", [(0.0, 0.2, 0.6, 1.0)] * len(verts))
    mesh.materials.append(None)
    return mesh


def mobius_band(name, segments=8, radius=1.0, width=0.35):
    """A triangulated Moebius band: a 2-manifold that is NON-ORIENTABLE.

    The reject case the winding diagnosis exists for, and the reason it is built
    here rather than asserted about in prose. Every edge carries one or two
    triangles, so a non-manifold census reads a clean zero, yet no assignment of
    per-triangle winding makes every shared edge agree -- which is exactly the
    state ``bmesh.ops.recalc_face_normals`` cannot repair. Measured on coral LOD0
    in this shape: 53 occurrences, ``nonmanifold=0``, and recalc moving it to 39
    rather than to 0.

    The twist lives in the closing quad, whose two far corners are taken in
    swapped order. UVs are per-face cells so the twist cannot leak into the UV
    gates and confuse what the case is testing.
    """
    verts = []
    for i in range(segments):
        angle = 2.0 * math.pi * i / float(segments)
        half = 0.5 * angle
        for side in (-1.0, 1.0):
            offset = side * 0.5 * width
            reach = radius + offset * math.cos(half)
            verts.append((reach * math.cos(angle), reach * math.sin(angle),
                          offset * math.sin(half)))
    faces = []
    for i in range(segments):
        j = (i + 1) % segments
        inner_i, outer_i = 2 * i, 2 * i + 1
        if j == 0:
            # THE TWIST. Joining the last rung to the first with the two sides
            # exchanged is what makes the strip one-sided.
            inner_j, outer_j = 2 * j + 1, 2 * j
        else:
            inner_j, outer_j = 2 * j, 2 * j + 1
        faces.append((inner_i, outer_i, outer_j))
        faces.append((inner_i, outer_j, inner_j))
    mesh = new_mesh(name, verts, faces)

    # One padded UV cell per quad, corners assigned by position in the face
    # rather than by vertex identity. Cell aspect matches the world aspect of a
    # rung, so aspect distortion stays at zero and only winding is under test.
    segment_world = 2.0 * math.pi * radius / float(segments)
    cell_v = 0.03
    cell_u = cell_v * (segment_world / width)
    per_loop = [None] * (len(faces) * 3)
    for face_index in range(len(faces)):
        cell = face_index // 2
        u0 = _UV_ORIGIN + cell * cell_u
        v0 = _UV_ORIGIN
        corners = (((u0, v0), (u0, v0 + cell_v), (u0 + cell_u, v0 + cell_v))
                   if face_index % 2 == 0 else
                   ((u0, v0), (u0 + cell_u, v0 + cell_v), (u0 + cell_u, v0)))
        for k in range(3):
            per_loop[face_index * 3 + k] = corners[k]
    add_uv(mesh, per_loop)
    add_color(mesh, "edge_wear", [(0.0, 0.2, 0.6, 1.0)] * len(verts))
    mesh.materials.append(None)
    return mesh


def three_faces_on_one_edge(name):
    """Three triangles sharing edge 0-1: a non-manifold fan.

    Among three faces on one edge two must traverse it the same way, so the
    winding gate CANNOT stay silent here however the faces are wound. The point
    of the case is that the detail must say so instead of sending the reader to
    ``recalc_face_normals``, which has nothing to fix.
    """
    verts = [(0.0, 0.0, 0.0), (1.0, 0.0, 0.0),
             (0.5, 1.0, 0.0), (0.5, -0.6, 0.4), (0.5, 0.2, -0.7)]
    faces = [(0, 1, 2), (0, 1, 3), (0, 1, 4)]
    mesh = new_mesh(name, verts, faces)
    per_loop = []
    cell = 0.22
    for polygon in mesh.polygons:
        u0 = _UV_ORIGIN + polygon.index * (cell + 0.02)
        per_loop.extend([(u0, _UV_ORIGIN), (u0 + cell, _UV_ORIGIN),
                         (u0 + 0.5 * cell, _UV_ORIGIN + cell)])
    add_uv(mesh, per_loop)
    add_color(mesh, "edge_wear", [(0.0, 0.2, 0.6, 1.0)] * len(verts))
    mesh.materials.append(None)
    return mesh


def dome_grid(name, cells):
    """``cells`` x ``cells`` quads with a shallow dome, UV-mapped in one island.

    Used for the triangle-budget gates: 14 cells is 392 triangles, above the
    350-triangle LOD2 maximum for a small prop in law.LOD_BUDGETS.
    """
    verts = []
    for j in range(cells + 1):
        for i in range(cells + 1):
            x = i / float(cells)
            y = j / float(cells)
            z = 0.01 * math.sin(x * math.pi) * math.sin(y * math.pi)
            verts.append((x, y, z))
    faces = []
    stride = cells + 1
    for j in range(cells):
        for i in range(cells):
            a = j * stride + i
            faces.append((a, a + 1, a + stride + 1, a + stride))
    mesh = new_mesh(name, verts, faces)
    per_loop = []
    for polygon in mesh.polygons:
        for loop_index in polygon.loop_indices:
            vertex = mesh.loops[loop_index].vertex_index
            x, y = verts[vertex][0], verts[vertex][1]
            per_loop.append((_UV_ORIGIN + x * _UV_CELL, _UV_ORIGIN + y * _UV_CELL))
    add_uv(mesh, per_loop)
    add_color(mesh, "edge_wear", [(0.0, 0.1, 0.7, 1.0)] * len(verts))
    mesh.materials.append(None)
    return mesh


def shared_cube(name):
    """Eight-vertex cube, 12 triangles, consistent outward winding."""
    verts = [(-0.5, -0.5, -0.5), (0.5, -0.5, -0.5), (0.5, 0.5, -0.5),
             (-0.5, 0.5, -0.5), (-0.5, -0.5, 0.5), (0.5, -0.5, 0.5),
             (0.5, 0.5, 0.5), (-0.5, 0.5, 0.5)]
    faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5),
             (2, 3, 7, 6), (3, 0, 4, 7)]
    return new_mesh(name, verts, faces)


def validate_clean(mesh, **kwargs):
    options = {"family": PROP, "lod_index": 0, "surface_class": HARD,
               "atlas_size": ATLAS}
    options.update(kwargs)
    return V.validate_mesh(mesh, **options)


# ---------------------------------------------------------------------------
# Blender-route cases
# ---------------------------------------------------------------------------

def case_clean_mesh():
    H.case("clean cube passes every gate")
    mesh = clean_cube()
    report = validate_clean(mesh)
    H.check(report.passed,
            "clean cube must pass, got: " + "; ".join(
                str(f) for f in report.failures))
    H.check(report.vertex_count == 24,
            "clean cube vertex_count=24, got " + str(report.vertex_count))
    H.check(report.triangle_count == 12,
            "clean cube triangle_count=12, got " + str(report.triangle_count))
    H.check(report.submesh_count == 1,
            "clean cube submesh_count=1, got " + str(report.submesh_count))
    H.check(report.uv_layers == ("UVMap",),
            "clean cube uv_layers, got " + str(report.uv_layers))
    H.check(report.color_layers == ("edge_wear",),
            "clean cube color_layers, got " + str(report.color_layers))
    H.check(report.has_tangent_basis, "clean cube must have a tangent basis")
    H.check(abs(report.bounds_min[0] + 0.5) < 1e-6
            and abs(report.bounds_max[2] - 0.5) < 1e-6,
            "clean cube bounds, got {0} {1}".format(report.bounds_min,
                                                    report.bounds_max))
    H.check(len(report.digest) == 32,
            "digest must be a 16-byte hex string, got " + repr(report.digest))
    return mesh


def case_degenerate_triangle():
    H.case("degenerate triangle: collapsed corner")
    mesh = clean_cube("MESH_SmallProp_Degenerate_LOD0")
    mesh.vertices[2].co = mesh.vertices[1].co
    report = validate_clean(mesh)
    expect("degenerate_triangle", report.failures,
           must=(V.GATE_DEGENERATE_TRIANGLE,),
           allow=(V.GATE_UV_STRETCH_EXCESSIVE,))
    found = detail_of(report.failures, V.GATE_DEGENERATE_TRIANGLE)
    H.check(found is not None and "triangle[" in found.detail,
            "degenerate detail must locate the triangle, got "
            + (found.detail if found else "<none>"))


def case_zero_area_uv():
    H.case("zero-area UV triangle: collapsed UV face")
    mesh = clean_cube("MESH_SmallProp_ZeroUV_LOD0")
    u0, v0 = face_uv_region(0)[0], face_uv_region(0)[1]
    layer = mesh.uv_layers[0]
    for i in range(4):
        layer.data[i].uv = (u0, v0)
    report = validate_clean(mesh)
    expect("zero_area_uv_triangle", report.failures,
           must=(V.GATE_ZERO_AREA_UV_TRIANGLE,),
           allow=(V.GATE_UV_ISLAND_BELOW_MIN_PIXELS,))
    found = detail_of(report.failures, V.GATE_ZERO_AREA_UV_TRIANGLE)
    H.check(found is not None and found.count == 2,
            "both triangles of the collapsed face must aggregate, count="
            + str(found.count if found else -1))


def case_non_finite_position():
    H.case("non-finite position: NaN vertex coordinate")
    mesh = clean_cube("MESH_SmallProp_NanPos_LOD0")
    mesh.vertices[5].co = (NAN, 0.5, -0.5)
    report = validate_clean(mesh)
    expect("non_finite_position", report.failures,
           must=(V.GATE_NON_FINITE_POSITION, V.GATE_NON_FINITE_BOUNDS),
           # Measured on Blender 4.5.9: a NaN corner makes the MikkTSpace
           # wrapper emit a zero-length tangent, (0.0, 0.0, 0.0), on the
           # affected loop, so the tangent length gate is reachable through
           # Blender's own data. Correct collateral here, not a false positive.
           allow=(V.GATE_DEGENERATE_TRIANGLE, V.GATE_NON_FINITE_NORMAL,
                  V.GATE_NORMAL_LENGTH_OUT_OF_RANGE,
                  V.GATE_UV_STRETCH_EXCESSIVE, V.GATE_NON_FINITE_TANGENT,
                  V.GATE_TANGENT_LENGTH_OUT_OF_RANGE))
    found = detail_of(report.failures, V.GATE_NON_FINITE_POSITION)
    H.check(found is not None and "vertex[5]" in found.detail,
            "position detail must name vertex[5], got "
            + (found.detail if found else "<none>"))


def case_non_finite_uv():
    H.case("non-finite UV: NaN texture coordinate")
    mesh = clean_cube("MESH_SmallProp_NanUV_LOD0")
    mesh.uv_layers[0].data[0].uv = (NAN, 0.07)
    report = validate_clean(mesh)
    expect("non_finite_uv", report.failures, must=(V.GATE_NON_FINITE_UV,))


def case_non_finite_color():
    H.case("non-finite colour: NaN in a wear channel")
    values = [(0.0, 0.25, 0.5, 1.0)] * 24
    values[7] = (0.1, NAN, 0.5, 1.0)
    mesh = clean_cube("MESH_SmallProp_NanCol_LOD0", color_values=values)
    report = validate_clean(mesh)
    expect("non_finite_color", report.failures, must=(V.GATE_NON_FINITE_COLOR,))
    found = detail_of(report.failures, V.GATE_NON_FINITE_COLOR)
    H.check(found is not None and "element[7]" in found.detail,
            "colour detail must name element[7], got "
            + (found.detail if found else "<none>"))


def case_color_out_of_unorm_range():
    H.case("vertex colour outside the UNorm8 range")
    values = [(0.0, 0.25, 0.5, 1.0)] * 24
    values[3] = (7.0, 0.25, 0.5, 1.0)
    mesh = clean_cube("MESH_SmallProp_ColRange_LOD0", color_values=values)
    report = validate_clean(mesh)
    expect("vertex_color_out_of_unorm_range", report.failures,
           must=(V.GATE_VERTEX_COLOR_OUT_OF_UNORM_RANGE,))


def case_color_layer_missing():
    H.case("vertex colour layer missing")
    mesh = clean_cube("MESH_SmallProp_NoCol_LOD0", color=False)
    report = validate_clean(mesh)
    expect("vertex_color_layer_missing", report.failures,
           must=(V.GATE_VERTEX_COLOR_LAYER_MISSING,))


def case_color_contract_mismatch():
    H.case("vertex colour layer name outside the family contract")
    mesh = clean_cube("MESH_SmallProp_BadColName_LOD0", color_name="wear_map")
    report = validate_clean(mesh)
    expect("vertex_color_contract_mismatch", report.failures,
           must=(V.GATE_VERTEX_COLOR_CONTRACT_MISMATCH,))
    accepted = V.expected_vcol_names(HARD)
    H.check(("edge_wear",) in accepted,
            "hard-surface contract must accept the law.VCOL_CONTRACT red-channel "
            "name, accepted=" + str(accepted))


def case_organic_sway_anchor():
    H.case("organic sway channel with no anchor band")
    organic = law.SurfaceClass.ORGANIC
    bad = clean_cube("MESH_Flora_NoAnchor_LOD0", color_name="sway_amplitude",
                     color_values=[(0.9, 0.1, 0.5, 1.0)] * 24)
    report = V.validate_mesh(bad, family=law.Family.FLORA, lod_index=0,
                             surface_class=organic, atlas_size=ATLAS)
    expect("organic_sway_anchor_missing", report.failures,
           must=(V.GATE_ORGANIC_SWAY_ANCHOR_MISSING,))
    values = []
    for i in range(24):
        values.append((0.0 if i < 8 else 0.95, 0.1, 0.5, 1.0))
    good = clean_cube("MESH_Flora_Anchored_LOD0", color_name="sway_amplitude",
                      color_values=values)
    good_report = V.validate_mesh(good, family=law.Family.FLORA, lod_index=0,
                                  surface_class=organic, atlas_size=ATLAS)
    H.check(good_report.passed,
            "anchored organic mesh must pass, got: " + "; ".join(
                str(f) for f in good_report.failures))


def case_uv0_missing():
    H.case("UV0 missing")
    mesh = clean_cube("MESH_SmallProp_NoUV_LOD0", uv=False)
    report = validate_clean(mesh)
    expect("uv0_missing", report.failures, must=(V.GATE_UV0_MISSING,))
    H.check(not report.has_tangent_basis,
            "a mesh with no UV cannot carry a tangent basis")


def case_uv_stretch():
    H.case("UV stretch above the aspect distortion limit")
    mesh = clean_cube("MESH_SmallProp_Stretch_LOD0")
    u0, v0, u1, _ = face_uv_region(0)
    layer = mesh.uv_layers[0]
    squashed = ((u0, v0), (u1, v0), (u1, v0 + 0.02), (u0, v0 + 0.02))
    for i in range(4):
        layer.data[i].uv = squashed[i]
    report = validate_clean(mesh)
    expect("uv_stretch_excessive", report.failures,
           must=(V.GATE_UV_STRETCH_EXCESSIVE,))
    found = detail_of(report.failures, V.GATE_UV_STRETCH_EXCESSIVE)
    # The gate is now AREA-WEIGHTED, so the detail reports the fraction of surface area
    # over the limit plus the worst triangle's measured distortion. The assertion's intent
    # is unchanged - a failure must be locatable and quantified - so it checks for both
    # numbers rather than for the old per-triangle-only wording.
    H.check(found is not None
            and "aspect distortion" in found.detail
            and "worst triangle[" in found.detail
            and "of surface area" in found.detail,
            "stretch detail must carry the measured area fraction AND the worst "
            "triangle value, got "
            + (found.detail if found else "<none>"))
    H.check(V.uv_aspect_distortion([0.0, 0.0, 0.0, 2.0, 0.0, 0.0, 2.0, 2.0, 0.0],
                                   [0.0, 0.0, 1.0, 0.0, 1.0, 1.0],
                                   [0, 1, 2], [0, 1, 2], 0) < 1e-6,
            "a uniform scale must measure zero aspect distortion")


def case_uv_island_too_small():
    H.case("UV island below the minimum pixel size")
    mesh = clean_cube("MESH_SmallProp_TinyIsland_LOD0")
    u0, v0 = face_uv_region(0)[0], face_uv_region(0)[1]
    tiny = 0.002
    layer = mesh.uv_layers[0]
    corners = ((u0, v0), (u0 + tiny, v0), (u0 + tiny, v0 + tiny), (u0, v0 + tiny))
    for i in range(4):
        layer.data[i].uv = corners[i]
    report = validate_clean(mesh)
    expect("uv_island_below_min_pixels", report.failures,
           must=(V.GATE_UV_ISLAND_BELOW_MIN_PIXELS,))
    H.check(V.law.UV_MIN_ISLAND_PIXELS == 4,
            "island gate must read law.UV_MIN_ISLAND_PIXELS")


def case_uv_atlas_padding():
    H.case("UV island inside the atlas border reserve")
    mesh = clean_cube("MESH_SmallProp_NoPadding_LOD0")
    layer = mesh.uv_layers[0]
    corners = ((0.0, 0.0), (0.26, 0.0), (0.26, 0.26), (0.0, 0.26))
    for i in range(4):
        layer.data[i].uv = corners[i]
    report = validate_clean(mesh)
    expect("uv_atlas_padding_violation", report.failures,
           must=(V.GATE_UV_ATLAS_PADDING_VIOLATION,))
    no_atlas = V.validate_mesh(mesh, family=PROP, lod_index=0,
                               surface_class=HARD)
    H.check(no_atlas.passed,
            "without atlas_size the padding gate must report as unenforced, "
            "not fail: " + "; ".join(str(f) for f in no_atlas.failures))
    skipped = set(w.gate for w in no_atlas.warnings)
    H.check(V.GATE_UV_ATLAS_PADDING_VIOLATION in skipped,
            "an unenforced gate must appear in warnings, got " + str(skipped))


def case_winding():
    H.case("inconsistent winding across a shared edge")
    bad = folded_plate("MESH_SmallProp_BadWind_LOD0", [(0, 1, 2), (1, 2, 3)])
    report = validate_clean(bad)
    expect("inconsistent_winding", report.failures,
           must=(V.GATE_INCONSISTENT_WINDING,),
           allow=(V.GATE_NORMAL_LENGTH_OUT_OF_RANGE,))
    good = folded_plate("MESH_SmallProp_GoodWind_LOD0", [(0, 1, 2), (2, 1, 3)])
    good_report = validate_clean(good)
    H.check(good_report.passed,
            "consistently wound plate must pass, got: " + "; ".join(
                str(f) for f in good_report.failures))
    declared = V.validate_mesh(bad, family=PROP, lod_index=0, surface_class=HARD,
                               atlas_size=ATLAS, double_sided=True)
    H.check(V.GATE_INCONSISTENT_WINDING not in gates_of(declared.failures),
            "double_sided=True must convert the winding gate into a recorded "
            "exemption")
    # The symptom alone is not actionable, and the project paid for that: recalc
    # was added for this gate, measured at 53 -> 39 on coral LOD0, and removed
    # again after it broke the authored normal basis on rock. A repairable case
    # must SAY it is repairable.
    found = detail_of(report.failures, V.GATE_INCONSISTENT_WINDING)
    H.check(found is not None
            and "directed edge (1 -> 2)" in found.detail
            and "IS orientable" in found.detail
            and "1 triangle(s)" in found.detail,
            "a flipped face on an orientable surface must be diagnosed as "
            "orientable and countable, got " + (found.detail if found
                                                else "<none>"))
    repairable = V.orientation_analysis(V.extract_mesh_data(bad))
    H.check(repairable.orientable and repairable.backwards_triangles == 1
            and not repairable.over_shared_edges,
            "orientation_analysis on a single flipped face: expected "
            "orientable/1 backwards/0 over-shared, got "
            + str(repairable))
    clean = V.orientation_analysis(V.extract_mesh_data(good))
    H.check(clean.orientable and clean.backwards_triangles == 0
            and not clean.conflict_edges,
            "orientation_analysis must stay silent on a consistently wound "
            "plate, got " + str(clean))


def case_winding_non_orientable():
    H.case("non-orientable surface: winding fires and recalc cannot fix it")
    mesh = mobius_band("MESH_SmallProp_Mobius_LOD0")
    report = validate_clean(mesh)
    # A Moebius band welds a sheet to itself, so the averaged vertex normal at the
    # seam can collapse; that gate is collateral here exactly as it is in
    # case_winding.
    expect("winding_non_orientable", report.failures,
           must=(V.GATE_INCONSISTENT_WINDING,),
           allow=(V.GATE_NORMAL_LENGTH_OUT_OF_RANGE,
                  V.GATE_UV_STRETCH_EXCESSIVE))
    found = detail_of(report.failures, V.GATE_INCONSISTENT_WINDING)
    H.check(found is not None and "NON-ORIENTABLE" in found.detail
            and "recalc_face_normals cannot repair that" in found.detail
            and "1 of 1 connected face region(s)" in found.detail,
            "a non-orientable surface must be named as such, must warn off "
            "recalc and must quote the invariant region count, got "
            + (found.detail if found else "<none>"))
    diagnosis = V.orientation_analysis(V.extract_mesh_data(mesh))
    H.check(not diagnosis.orientable and len(diagnosis.conflict_edges) >= 1
            and diagnosis.regions == 1 and diagnosis.twisted_regions == 1,
            "orientation_analysis must prove the Moebius band non-orientable, "
            "got " + str(diagnosis))
    # The edge SET is traversal dependent and the docstring says so, so nothing
    # asserts a count for it. The region verdict is the invariant, and a twisted
    # region must not also report a backwards-triangle repair that cannot work.
    H.check(diagnosis.backwards_triangles == 0,
            "a twisted region must not offer a flip count as its repair, got "
            + str(diagnosis.backwards_triangles))
    # THE POINT OF THE WHOLE CASE. Every edge carries at most two triangles, so a
    # non-manifold census reads clean while the winding gate fires -- the exact
    # combination that read as a contradiction on coral (nonmanifold=0 next to
    # inconsistent_winding x53).
    incident = V._triangle_adjacency(V.extract_mesh_data(mesh))
    worst = max(len(users) for users in incident.values())
    H.check(worst == 2 and not diagnosis.over_shared_edges,
            "the Moebius band must be manifold (max 2 triangles per edge), got "
            "max " + str(worst) + " and over_shared="
            + str(diagnosis.over_shared_edges))


def case_winding_non_manifold_forced():
    H.case("edge shared by three triangles forces a repeated directed edge")
    mesh = three_faces_on_one_edge("MESH_SmallProp_ThreeFan_LOD0")
    report = validate_clean(mesh)
    expect("winding_non_manifold_forced", report.failures,
           must=(V.GATE_INCONSISTENT_WINDING,),
           allow=(V.GATE_NORMAL_LENGTH_OUT_OF_RANGE,
                  V.GATE_UV_STRETCH_EXCESSIVE))
    found = detail_of(report.failures, V.GATE_INCONSISTENT_WINDING)
    H.check(found is not None
            and "carry 3 or more triangles" in found.detail
            and "non-manifold defect" in found.detail,
            "a three-face edge must be diagnosed as non-manifold rather than as "
            "an orientation problem, got " + (found.detail if found
                                              else "<none>"))
    diagnosis = V.orientation_analysis(V.extract_mesh_data(mesh))
    H.check(len(diagnosis.over_shared_edges) == 1
            and diagnosis.over_shared_edges[0][1] == 3,
            "orientation_analysis must report exactly one edge with three "
            "triangles, got " + str(diagnosis.over_shared_edges))


def case_material_slots_missing():
    H.case("no material slots declared")
    mesh = clean_cube("MESH_SmallProp_NoSlots_LOD0", material_slots=0)
    report = validate_clean(mesh)
    expect("material_slots_missing", report.failures,
           must=(V.GATE_MATERIAL_SLOTS_MISSING,
                 V.GATE_MATERIAL_INDEX_OUT_OF_SLOT_RANGE))


def case_submesh_empty_slot():
    H.case("declared material slot with no triangles")
    mesh = clean_cube("MESH_SmallProp_EmptySlot_LOD0", material_slots=2)
    report = validate_clean(mesh)
    expect("submesh_empty_declared_slot", report.failures,
           must=(V.GATE_SUBMESH_EMPTY_DECLARED_SLOT,))


def case_material_slot_count():
    H.case("material slot count above law.MATERIAL_SLOT_MAX")
    mesh = clean_cube("MESH_SmallProp_TooManySlots_LOD0", material_slots=5)
    for polygon in mesh.polygons:
        polygon.material_index = min(polygon.index, 4)
    report = validate_clean(mesh)
    expect("material_slot_count_exceeded", report.failures,
           must=(V.GATE_MATERIAL_SLOT_COUNT_EXCEEDED,))


def case_material_index_out_of_range():
    H.case("triangle material index outside the declared slots")
    mesh = clean_cube("MESH_SmallProp_BadMatIndex_LOD0", material_slots=1)
    mesh.polygons[0].material_index = 3
    report = validate_clean(mesh)
    expect("material_index_out_of_slot_range", report.failures,
           must=(V.GATE_MATERIAL_INDEX_OUT_OF_SLOT_RANGE,))


def case_lod_budget():
    H.case("LOD triangle budget")
    mesh = dome_grid("MESH_SmallProp_Dense_LOD2", 14)
    over = V.validate_mesh(mesh, family=PROP, lod_index=2, surface_class=HARD,
                           atlas_size=ATLAS)
    expect("lod_triangle_budget_exceeded", over.failures,
           must=(V.GATE_LOD_TRIANGLE_BUDGET_EXCEEDED,))
    H.check(over.triangle_count == 392,
            "dome grid must be 392 triangles, got " + str(over.triangle_count))
    H.check(str(law.LOD_BUDGETS[PROP].limit(2)) in detail_of(
        over.failures, V.GATE_LOD_TRIANGLE_BUDGET_EXCEEDED).detail,
        "budget detail must quote the law.LOD_BUDGETS maximum")
    under = V.validate_mesh(mesh, family=PROP, lod_index=0, surface_class=HARD,
                            atlas_size=ATLAS)
    H.check(under.passed,
            "the same mesh is legal at LOD0 (6000 max), got: " + "; ".join(
                str(f) for f in under.failures))


def case_bounds_extent():
    H.case("bounds extent below law.MIN_BOUNDS_EXTENT_M")
    mesh = clean_cube("MESH_SmallProp_Tiny_LOD0")
    for vertex in mesh.vertices:
        vertex.co = (vertex.co[0] * 0.0008, vertex.co[1] * 0.0008,
                     vertex.co[2] * 0.0008)
    report = validate_clean(mesh)
    expect("bounds_extent_too_small", report.failures,
           must=(V.GATE_BOUNDS_EXTENT_TOO_SMALL,))
    found = detail_of(report.failures, V.GATE_BOUNDS_EXTENT_TOO_SMALL)
    H.check(found is not None and found.count == 3,
            "all three collapsed axes must aggregate, count="
            + str(found.count if found else -1))


def case_planar_card():
    H.case("flat card: strict by default, passes when declared planar")
    verts = [(0.0, 0.0, 0.0), (1.0, 0.0, 0.0), (1.0, 1.0, 0.0), (0.0, 1.0, 0.0)]
    mesh = new_mesh("MESH_SmallProp_Card_LOD2", verts, [(0, 1, 2, 3)])
    per_loop = []
    for polygon in mesh.polygons:
        for loop_index in polygon.loop_indices:
            vertex = mesh.loops[loop_index].vertex_index
            per_loop.append((_UV_ORIGIN + verts[vertex][0] * _UV_CELL,
                             _UV_ORIGIN + verts[vertex][1] * _UV_CELL))
    add_uv(mesh, per_loop)
    add_color(mesh, "edge_wear", [(0.0, 0.2, 0.6, 1.0)] * 4)
    mesh.materials.append(None)
    strict = validate_clean(mesh)
    expect("planar card strict", strict.failures,
           must=(V.GATE_BOUNDS_EXTENT_TOO_SMALL,))
    declared = V.validate_mesh(mesh, family=PROP, lod_index=0,
                               surface_class=HARD, atlas_size=ATLAS, planar=True)
    H.check(declared.passed,
            "planar=True must accept one collapsed axis, got: " + "; ".join(
                str(f) for f in declared.failures))


# ---------------------------------------------------------------------------
# MeshData-snapshot cases
# ---------------------------------------------------------------------------
# Blender's datablock cannot express these states: it re-normalises its normal
# cache, always returns unit tangents with a +-1 bitangent sign, and saturates an
# assigned infinity to FLT_MAX. The Unity vertex/index writer can, so the gates
# are proven on the snapshot buffer the writer consumes.

def snapshot(source):
    return V.extract_mesh_data(source)


def validate_snapshot(data, **kwargs):
    options = {"family": PROP, "lod_index": 0, "surface_class": HARD,
               "atlas_size": ATLAS}
    options.update(kwargs)
    return V.validate_mesh_data(data, **options)


def case_snapshot_baseline(source):
    H.case("snapshot of the clean cube passes, so mutations are isolated")
    report = validate_snapshot(snapshot(source))
    H.check(report.passed,
            "clean snapshot must pass, got: " + "; ".join(
                str(f) for f in report.failures))


def case_index_count(source):
    H.case("index count not a multiple of 3")
    data = snapshot(source)
    data.tri_vertices = data.tri_vertices[:-1]
    report = validate_snapshot(data)
    expect("index_count_not_triangulated", report.failures,
           must=(V.GATE_INDEX_COUNT_NOT_TRIANGULATED,))


def case_index_range(source):
    H.case("index outside the vertex range")
    data = snapshot(source)
    data.tri_vertices[0] = data.vertex_count + 975
    report = validate_snapshot(data)
    expect("index_out_of_range", report.failures,
           must=(V.GATE_INDEX_OUT_OF_RANGE,))
    found = detail_of(report.failures, V.GATE_INDEX_OUT_OF_RANGE)
    H.check(found is not None and "index_buffer[0]" in found.detail,
            "index detail must locate the slot, got "
            + (found.detail if found else "<none>"))


def case_non_finite_normal(source):
    H.case("non-finite normal: NaN and Inf in the normal streams")
    nan_data = snapshot(source)
    nan_data.vertex_normals[0] = NAN
    nan_report = validate_snapshot(nan_data)
    expect("non_finite_normal (NaN, vertex stream)", nan_report.failures,
           must=(V.GATE_NON_FINITE_NORMAL,))
    inf_data = snapshot(source)
    inf_data.corner_normals[4] = float("inf")
    inf_report = validate_snapshot(inf_data)
    expect("non_finite_normal (Inf, corner stream)", inf_report.failures,
           must=(V.GATE_NON_FINITE_NORMAL,))
    found = detail_of(inf_report.failures, V.GATE_NON_FINITE_NORMAL)
    H.check(found is not None and "corner_normals[1]" in found.detail,
            "normal detail must name the stream and index, got "
            + (found.detail if found else "<none>"))


def case_normal_length(source):
    H.case("normal length outside law.NORMAL_LENGTH_MIN..MAX")
    data = snapshot(source)
    data.vertex_normals[0] = 0.5
    data.vertex_normals[1] = 0.0
    data.vertex_normals[2] = 0.0
    report = validate_snapshot(data)
    expect("normal_length_out_of_range", report.failures,
           must=(V.GATE_NORMAL_LENGTH_OUT_OF_RANGE,))
    found = detail_of(report.failures, V.GATE_NORMAL_LENGTH_OUT_OF_RANGE)
    H.check(found is not None and "length=0.5" in found.detail,
            "normal length detail must carry the measurement, got "
            + (found.detail if found else "<none>"))


def case_non_finite_tangent(source):
    H.case("non-finite tangent")
    data = snapshot(source)
    data.tangents[0] = NAN
    report = validate_snapshot(data)
    expect("non_finite_tangent", report.failures,
           must=(V.GATE_NON_FINITE_TANGENT,))
    sign_data = snapshot(source)
    sign_data.tangent_signs[2] = NAN
    sign_report = validate_snapshot(sign_data)
    expect("non_finite_tangent (bitangent sign)", sign_report.failures,
           must=(V.GATE_NON_FINITE_TANGENT,))


def case_tangent_length(source):
    H.case("tangent length outside law.TANGENT_LENGTH_MIN..MAX")
    data = snapshot(source)
    data.tangents[0] = 2.0
    data.tangents[1] = 0.0
    data.tangents[2] = 0.0
    report = validate_snapshot(data)
    expect("tangent_length_out_of_range", report.failures,
           must=(V.GATE_TANGENT_LENGTH_OUT_OF_RANGE,))


def case_tangent_handedness(source):
    H.case("tangent handedness other than -1 or +1")
    data = snapshot(source)
    data.tangent_signs[0] = 0.0
    report = validate_snapshot(data)
    expect("tangent_handedness_invalid", report.failures,
           must=(V.GATE_TANGENT_HANDEDNESS_INVALID,))
    half = snapshot(source)
    half.tangent_signs[0] = 0.5
    expect("tangent_handedness_invalid (0.5)", validate_snapshot(half).failures,
           must=(V.GATE_TANGENT_HANDEDNESS_INVALID,))


def case_infinite_position(source):
    H.case("infinite position and the bounds it poisons")
    data = snapshot(source)
    data.positions[0] = float("inf")
    report = validate_snapshot(data)
    expect("non_finite_position (Inf)", report.failures,
           must=(V.GATE_NON_FINITE_POSITION, V.GATE_NON_FINITE_BOUNDS),
           allow=(V.GATE_DEGENERATE_TRIANGLE, V.GATE_UV_STRETCH_EXCESSIVE,
                  V.GATE_BOUNDS_EXTENT_TOO_SMALL))


def case_empty_mesh():
    H.case("empty mesh")
    data = V.MeshData(name="MESH_SmallProp_Empty_LOD0")
    report = validate_snapshot(data)
    expect("empty_mesh", report.failures,
           must=(V.GATE_EMPTY_MESH, V.GATE_NO_TRIANGLES),
           allow=(V.GATE_NON_FINITE_BOUNDS, V.GATE_UV0_MISSING,
                  V.GATE_VERTEX_COLOR_LAYER_MISSING))
    H.check(not report.passed, "an empty mesh must never pass")


# ---------------------------------------------------------------------------
# Collider cases
# ---------------------------------------------------------------------------

def case_collider_clean(lod0):
    H.case("clean convex collider passes")
    collider = shared_cube("COL_SmallProp_Clean")
    failures = V.validate_collider(collider, family=PROP, lod0_mesh=lod0)
    H.check(not failures,
            "clean collider must pass, got: " + "; ".join(
                str(f) for f in failures))
    return collider


def case_collider_is_lod0(lod0):
    H.case("collider that IS the LOD0 datablock")
    failures = V.validate_collider(lod0, family=PROP, lod0_mesh=lod0)
    expect("collider_is_visual_mesh (same object)", failures,
           must=(V.GATE_COLLIDER_IS_VISUAL_MESH,),
           allow=(V.GATE_COLLIDER_NAME_NOT_COL_PREFIXED,))

    H.case("collider that is a renamed copy of LOD0 (name check defeated)")
    sneaky = lod0.copy()
    sneaky.name = "COL_SmallProp_Sneaky"
    failures = V.validate_collider(sneaky, family=PROP, lod0_mesh=lod0)
    expect("collider_is_visual_mesh (digest)", failures,
           must=(V.GATE_COLLIDER_IS_VISUAL_MESH,))
    found = detail_of(failures, V.GATE_COLLIDER_IS_VISUAL_MESH)
    H.check(found is not None and "byte-identical" in found.detail,
            "the copy must be caught by vertex-data identity, got "
            + (found.detail if found else "<none>"))
    H.check(sneaky.name != lod0.name,
            "the sneaky proxy must carry a different datablock name, so only "
            "the digest can catch it")


def case_collider_budget(lod0):
    H.case("collider above law.COLLIDER_CONVEX_TRI_MAX")
    dense = dome_grid("COL_SmallProp_Dense", 14)
    failures = V.validate_collider(dense, family=PROP, lod0_mesh=lod0)
    expect("collider_triangle_budget_exceeded", failures,
           must=(V.GATE_COLLIDER_TRIANGLE_BUDGET_EXCEEDED,))
    found = detail_of(failures, V.GATE_COLLIDER_TRIANGLE_BUDGET_EXCEEDED)
    H.check(found is not None
            and str(law.COLLIDER_CONVEX_TRI_MAX) in found.detail,
            "budget detail must quote law.COLLIDER_CONVEX_TRI_MAX")


def case_collider_not_convex(lod0):
    H.case("non-convex collision proxy")
    dented = shared_cube("COL_SmallProp_Dented")
    dented.vertices[6].co = (0.0, 0.0, 0.0)
    failures = V.validate_collider(dented, family=PROP, lod0_mesh=lod0)
    expect("collider_not_convex", failures, must=(V.GATE_COLLIDER_NOT_CONVEX,))
    found = detail_of(failures, V.GATE_COLLIDER_NOT_CONVEX)
    H.check(found is not None and "vertex[" in found.detail,
            "convexity detail must name the offending vertex, got "
            + (found.detail if found else "<none>"))


def case_collider_name(lod0):
    H.case("collider named as a visual mesh")
    wrong = shared_cube("LOD_SmallProp_Thing")
    failures = V.validate_collider(wrong, family=PROP, lod0_mesh=lod0)
    expect("collider_name_not_col_prefixed", failures,
           must=(V.GATE_COLLIDER_NAME_NOT_COL_PREFIXED,))
    found = detail_of(failures, V.GATE_COLLIDER_NAME_NOT_COL_PREFIXED)
    H.check(found is not None and found.count == 2,
            "missing COL_ and carrying LOD_ are two occurrences, count="
            + str(found.count if found else -1))


def case_collider_crosscheck_missing():
    H.case("collider validated without any visual mesh to compare against")
    collider = shared_cube("COL_SmallProp_Lonely")
    failures = V.validate_collider(collider, family=PROP)
    expect("collider_crosscheck_unavailable", failures,
           must=(V.GATE_COLLIDER_CROSSCHECK_UNAVAILABLE,))


def case_collider_empty(lod0):
    H.case("empty collision proxy")
    empty = bpy.data.meshes.new("COL_SmallProp_Empty")
    failures = V.validate_collider(empty, family=PROP, lod0_mesh=lod0)
    expect("collider_empty", failures, must=(V.GATE_COLLIDER_EMPTY,))


# ---------------------------------------------------------------------------
# LOD chain cases
# ---------------------------------------------------------------------------

def chain_report(name, triangles, lod_index):
    """A real MeshReport carrying real counts. validate_lod_chain reads these."""
    return V.MeshReport(
        name=name, vertex_count=triangles * 3, triangle_count=triangles,
        submesh_count=1, bounds_min=(0.0, 0.0, 0.0), bounds_max=(1.0, 1.0, 1.0),
        uv_layers=("UVMap",), color_layers=("edge_wear",),
        has_tangent_basis=True, failures=[], warnings=[], lod_index=lod_index,
        family=PROP.value, surface_class=HARD.value)


def case_lod_chain():
    H.case("complete, monotonically decreasing LOD chain passes")
    chain = [chain_report("MESH_SmallProp_A_LOD0", 6000, 0),
             chain_report("MESH_SmallProp_A_LOD1", 2000, 1),
             chain_report("MESH_SmallProp_A_LOD2", 350, 2)]
    H.check(not V.validate_lod_chain(chain, family=PROP),
            "a legal chain must produce no failures")

    H.case("LOD chain missing LOD2")
    expect("lod_chain_incomplete", V.validate_lod_chain(chain[:2], family=PROP),
           must=(V.GATE_LOD_CHAIN_INCOMPLETE,))

    H.case("LOD chain that does not reduce triangles")
    flat = [chain_report("MESH_SmallProp_B_LOD0", 900, 0),
            chain_report("MESH_SmallProp_B_LOD1", 900, 1),
            chain_report("MESH_SmallProp_B_LOD2", 200, 2)]
    failures = V.validate_lod_chain(flat, family=PROP)
    expect("lod_chain_not_monotonic", failures,
           must=(V.GATE_LOD_CHAIN_NOT_MONOTONIC,))
    found = detail_of(failures, V.GATE_LOD_CHAIN_NOT_MONOTONIC)
    H.check(found is not None and "900" in found.detail,
            "monotonic detail must quote the counts, got "
            + (found.detail if found else "<none>"))

    H.case("two meshes claiming the same LOD index")
    duplicated = [chain_report("MESH_SmallProp_C_LOD0", 900, 0),
                  chain_report("MESH_SmallProp_C_LOD1", 500, 1),
                  chain_report("MESH_SmallProp_C_LOD1b", 400, 1),
                  chain_report("MESH_SmallProp_C_LOD2", 200, 2)]
    expect("lod_chain_duplicate_index",
           V.validate_lod_chain(duplicated, family=PROP),
           must=(V.GATE_LOD_CHAIN_DUPLICATE_INDEX,))

    H.case("declared impostor exemption skips the chain-completeness gate")
    single = [chain_report("MESH_SmallProp_D_LOD0", 40, 0)]
    H.check(not V.validate_lod_chain(single, family=PROP, exempt=True),
            "exempt=True must not fail an intentionally single-LOD asset")
    expect("lod_chain_incomplete (no exemption)",
           V.validate_lod_chain(single, family=PROP),
           must=(V.GATE_LOD_CHAIN_INCOMPLETE,))


# ---------------------------------------------------------------------------
# Abort and black box
# ---------------------------------------------------------------------------

def case_assert_or_abort_pass():
    H.case("assert_or_abort accepts a clean package")
    box = BlackBox("h8forge_validate_selftest", "clean")
    clean = [chain_report("MESH_SmallProp_E_LOD0", 900, 0),
             chain_report("MESH_SmallProp_E_LOD1", 400, 1),
             chain_report("MESH_SmallProp_E_LOD2", 100, 2)]
    raised = None
    try:
        V.assert_or_abort(clean, blackbox=box, reason="selftest_clean_package")
    except BaseException as exc:  # noqa: BLE001 - the test needs the type
        raised = exc
    H.check(raised is None,
            "a clean package must not abort, raised " + repr(raised))
    stages = [entry["stage"] for entry in box.ordered_entries()]
    H.check("assert_or_abort:pass" in stages,
            "the accepted case must still be recorded, stages=" + str(stages))


def case_assert_or_abort_raises():
    H.case("assert_or_abort aborts the save and dumps the black box")
    box = BlackBox("h8forge_validate_selftest", "abort")
    broken = clean_cube("MESH_SmallProp_Abort_LOD0", color=False)
    report = validate_clean(broken)
    chain_failures = V.validate_lod_chain([chain_report(report.name, 12, 0)],
                                          family=PROP)
    raised = None
    try:
        V.assert_or_abort([report, chain_failures], blackbox=box,
                          reason="selftest_abort")
    except GenerationAborted as exc:
        raised = exc
    if not H.check(raised is not None,
                   "a failing package must raise GenerationAborted"):
        return
    H.check(raised.dump_path is not None and os.path.isfile(raised.dump_path),
            "the exception must carry a real dump path, got "
            + repr(raised.dump_path))
    H.check(V.GATE_VERTEX_COLOR_LAYER_MISSING in str(raised),
            "the abort message must name the failed gates, got " + str(raised))
    H.check(len(raised.failures) >= 2,
            "the abort must carry both the mesh and chain failures, got "
            + str(len(raised.failures)))
    H.check(box.first_invalid_stage() is not None,
            "the black box must know the first invalid stage")
    if raised.dump_path and os.path.isfile(raised.dump_path):
        os.remove(raised.dump_path)
        H.check(not os.path.isfile(raised.dump_path),
                "the self-test must clean up its own dump artifact")

    H.case("assert_or_abort still raises when no black box is supplied")
    raised = None
    try:
        V.assert_or_abort([report], blackbox=None, reason="selftest_no_box")
    except GenerationAborted as exc:
        raised = exc
    H.check(raised is not None and raised.dump_path is None,
            "without a black box the abort must still raise, with no dump path")


def case_blackbox_records_every_gate():
    H.case("every gate records into the black box")
    box = BlackBox("h8forge_validate_selftest", "gates")
    mesh = clean_cube("MESH_SmallProp_BlackBox_LOD0")
    V.validate_mesh(mesh, family=PROP, lod_index=0, surface_class=HARD,
                    atlas_size=ATLAS, blackbox=box)
    stages = set(entry["stage"] for entry in box.ordered_entries())
    missing = [gate for gate in V.MESH_GATES
               if "validate_mesh:" + gate not in stages]
    H.check(not missing, "mesh gates absent from the black box: " + str(missing))

    box2 = BlackBox("h8forge_validate_selftest", "gatefail")
    broken = clean_cube("MESH_SmallProp_BlackBoxFail_LOD0", color=False)
    V.validate_mesh(broken, family=PROP, lod_index=0, surface_class=HARD,
                    atlas_size=ATLAS, blackbox=box2)
    codes = {}
    for entry in box2.ordered_entries():
        if entry["failure"]:
            codes[entry["failure"]] = entry["stage"]
    H.check(V.GATE_VERTEX_COLOR_LAYER_MISSING in codes,
            "a failed gate must record a failure code, got " + str(codes))
    H.check(box2.last_accepted_stage() is not None,
            "the ring must still name the last accepted stage")

    box3 = BlackBox("h8forge_validate_selftest", "collider")
    V.validate_collider(shared_cube("COL_SmallProp_Recorded"), family=PROP,
                        blackbox=box3, lod0_mesh=clean_cube("MESH_Ref_LOD0"))
    stages3 = set(entry["stage"] for entry in box3.ordered_entries())
    missing3 = [gate for gate in V.COLLIDER_GATES
                if "validate_collider:" + gate not in stages3]
    H.check(not missing3, "collider gates absent: " + str(missing3))

    box4 = BlackBox("h8forge_validate_selftest", "chain")
    V.validate_lod_chain([chain_report("MESH_SmallProp_F_LOD0", 100, 0)],
                         family=PROP, blackbox=box4)
    stages4 = set(entry["stage"] for entry in box4.ordered_entries())
    missing4 = [gate for gate in V.LOD_CHAIN_GATES
                if "validate_lod_chain:" + gate not in stages4]
    H.check(not missing4, "LOD chain gates absent: " + str(missing4))



def case_generator_vcol_shape():
    """The shape vertexcolor.py actually writes must pass the contract gate.

    ``vertexcolor.FINAL_ATTRIBUTE`` is a single packed ``BYTE_COLOR``/``CORNER``
    attribute named "Col", because that is what Unity's FBX importer reads as
    ``Mesh.colors32``. A contract gate that only accepted the channel-semantic
    names would reject every asset the forge produces, so the real shape is
    tested rather than assumed.
    """
    H.case("generator colour shape (packed Col, BYTE_COLOR/CORNER) passes")
    mesh = clean_cube("MESH_SmallProp_PackedCol_LOD0", color=False)
    packed = V.packed_vcol_attribute_name()
    layer = mesh.color_attributes.new(name=packed, type="BYTE_COLOR",
                                      domain="CORNER")
    for i in range(len(layer.data)):
        layer.data[i].color = (0.0, 0.25, 0.5, 1.0)
    report = validate_clean(mesh)
    H.check(report.passed,
            "the packed '{0}' attribute must satisfy the contract, got: {1}"
            .format(packed, "; ".join(str(f) for f in report.failures)))
    H.check(report.color_layers == (packed,),
            "report must record the packed attribute name, got "
            + str(report.color_layers))
    H.check((packed,) in V.expected_vcol_names(HARD)
            and (packed,) in V.expected_vcol_names(law.SurfaceClass.ORGANIC),
            "the packed name must be accepted for every surface class")

    H.case("corner-domain organic sway anchor is still measured")
    organic = clean_cube("MESH_Flora_PackedNoAnchor_LOD0", color=False)
    organic_layer = organic.color_attributes.new(name=packed, type="BYTE_COLOR",
                                                domain="CORNER")
    for i in range(len(organic_layer.data)):
        organic_layer.data[i].color = (0.9, 0.1, 0.5, 1.0)
    organic_report = V.validate_mesh(organic, family=law.Family.FLORA,
                                     lod_index=0,
                                     surface_class=law.SurfaceClass.ORGANIC,
                                     atlas_size=ATLAS)
    expect("organic_sway_anchor_missing (corner domain)",
           organic_report.failures,
           must=(V.GATE_ORGANIC_SWAY_ANCHOR_MISSING,))

# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

def main():
    sys.stdout.write("h8forge.validate self-test, validator version "
                     + V.VALIDATOR_VERSION + ", Blender "
                     + bpy.app.version_string + "\n")
    sys.stdout.write("mesh gates: {0}, collider gates: {1}, chain gates: {2}\n\n"
                     .format(len(V.MESH_GATES), len(V.COLLIDER_GATES),
                             len(V.LOD_CHAIN_GATES)))
    lod0 = case_clean_mesh()
    case_degenerate_triangle()
    case_zero_area_uv()
    case_non_finite_position()
    case_non_finite_uv()
    case_non_finite_color()
    case_color_out_of_unorm_range()
    case_color_layer_missing()
    case_color_contract_mismatch()
    case_generator_vcol_shape()
    case_organic_sway_anchor()
    case_uv0_missing()
    case_uv_stretch()
    case_uv_island_too_small()
    case_uv_atlas_padding()
    case_winding()
    case_winding_non_orientable()
    case_winding_non_manifold_forced()
    case_material_slots_missing()
    case_submesh_empty_slot()
    case_material_slot_count()
    case_material_index_out_of_range()
    case_lod_budget()
    case_bounds_extent()
    case_planar_card()

    base = clean_cube("MESH_SmallProp_Snapshot_LOD0")
    case_snapshot_baseline(base)
    case_index_count(base)
    case_index_range(base)
    case_non_finite_normal(base)
    case_normal_length(base)
    case_non_finite_tangent(base)
    case_tangent_length(base)
    case_tangent_handedness(base)
    case_infinite_position(base)
    case_empty_mesh()

    case_collider_clean(lod0)
    case_collider_is_lod0(lod0)
    case_collider_budget(lod0)
    case_collider_not_convex(lod0)
    case_collider_name(lod0)
    case_collider_crosscheck_missing()
    case_collider_empty(lod0)

    case_lod_chain()
    case_assert_or_abort_pass()
    case_assert_or_abort_raises()
    case_blackbox_records_every_gate()

    code = H.report()
    covered = set()
    for gate in V.MESH_GATES + V.COLLIDER_GATES + V.LOD_CHAIN_GATES:
        covered.add(gate)
    sys.stdout.write("gates declared: {0}\n".format(len(covered)))
    sys.exit(code)


main()
