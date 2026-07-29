"""Hand-held hard-surface tool generator.

Specification route: ``3dmodel.md`` section 4 "Hard-Surface Engineering Law" ->
``3DMODEL_EQUIPMENT_PROPS.md`` (the family bible for "tools, devices, cockpit parts,
lab machinery") -> ``3DMODEL_HARD_SURFACE_MODULES.md`` sections 2/3/5 for the bevel,
smoothing-group and wear-bake law that props inherit.

WHY THIS FILE IS NAMED ``prop_handtool`` AND NOT ``prop_hardsurface``
--------------------------------------------------------------------
``law.FAMILY_SURFACE_CLASS[Family.SMALL_PROP]`` is already ``HARD_SURFACE``, so a
"hardsurface" suffix on a small-prop generator carries no information -- every member of
this family is hard surface. The sibling generators are named for the FORM they build
(``coral_branching``, ``flora_capstem``, ``rock``), and the form is what changes the
shape grammar. ``3DMODEL_EQUIPMENT_PROPS.md`` section 4 makes the same split itself,
budgeting "Handheld hero tools" at 1024 px/m against "Standard equipment" at 512 and
"Background clutter" at 256. A hand tool is the close-camera case with a grip, an
instrument face and a working end; a crate or a canister is a different grammar and
belongs in its own module later.

WHY PARTS PLUS A BEVEL PASS, NOT ONE BEVELLED BOX
-------------------------------------------------
``3DMODEL_EQUIPMENT_PROPS.md`` section 8 rejects a prop with "no visible function", and
section 1 demands it "communicate function: grip, hinge, screw, latch, display, sensor,
vent, cable route, seal, wear, and scale". No amount of chamfering makes one box do that.
So the tool is assembled from named functional parts, each of which is a closed solid
built by a general loft: casing shells split by a real seam groove, a bolted instrument
bezel, a ribbed grip, a stepped bore collar, a helically fluted bit, a stand-off guard
rail, and a cable gland. Function is geometry here, not a texture promise.

THE BEVEL WIDTH TRAP THIS GENERATOR HAS TO SOLVE
------------------------------------------------
``mesh_ops.bevel_hard_edges`` takes ONE offset for the whole selection and clamps it to
20% of the shortest adjacent edge anywhere in that selection -- which is exactly what
``3dmodel.md`` section 4 step 5 requires, and exactly what breaks on a multi-scale
assembly. A 2 mm bolt stud drags the global clamp down to ~0.4 mm, so the 0.25 m casing
would receive an invisible chamfer and the whole point of the bevel law would be lost
with no error anywhere.

The fix is not a new number. Geometry is built into TWO bmeshes by size band and each is
bevelled separately with the SAME ``law.BEVEL_RANGES[Family.SMALL_PROP]``, so the bible's
clamp is evaluated per band against that band's own shortest edge. Both ``BevelResult``
records are reported, including their ``clamped`` flags, so a collapsed width is visible
rather than inferred. The long casing edges additionally carry a machined chamfer BY
CONSTRUCTION through the rounded-rectangle profile, because a real casing is extruded
with a corner radius rather than chamfered afterwards.

Stage order follows ``PROCEDURAL_ASSET_PIPELINE.md`` "Generation Order". AO is baked
BEFORE the vertex-colour compose because the bake overwrites all four channels, and the
LOD0 budget is met before unwrapping because decimating afterwards discards the UV layout
and the colours -- both learned by breaking them, see ``Tools/Blender/README.md``.
"""

from __future__ import annotations

import argparse
import math
import os
import sys
from dataclasses import dataclass, field
from typing import List, Optional, Sequence

# Blender runs this file directly, so the package root is not on sys.path yet.
_HERE = os.path.dirname(os.path.abspath(__file__))
_BLENDER_TOOLS = os.path.dirname(_HERE)
if _BLENDER_TOOLS not in sys.path:
    sys.path.insert(0, _BLENDER_TOOLS)

import bmesh  # noqa: E402
import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

from h8forge import export_unity, law, mesh_ops, preview, validate, vertexcolor  # noqa: E402
from h8forge.blackbox import BlackBox, GenerationAborted  # noqa: E402


GENERATOR_NAME = "prop_handtool"
GENERATOR_VERSION = "1.0.0"

# Orientation contract. ``export_unity.blender_to_unity`` maps Blender (x, y, z) to Unity
# (x, z, y), so building the bore axis along Blender +Y puts it on Unity +Z -- forward,
# which is the held-tool convention -- and the grip on Blender -Z lands on Unity -Y, down.
# 3DMODEL_EQUIPMENT_PROPS.md section 1 requires a stated "Pivot and orientation contract";
# this is it, and it is asserted rather than assumed because a tool exported pointing at
# the player's face is not a defect any triangle count would catch.
BORE_AXIS = Vector((0.0, 1.0, 0.0))
UP_AXIS = Vector((0.0, 0.0, 1.0))
SIDE_AXIS = Vector((1.0, 0.0, 0.0))


# ---------------------------------------------------------------------------
# Specification
# ---------------------------------------------------------------------------

@dataclass
class HandToolSpec:
    """Deterministic tool description. Every field is a named parameter.

    ``PROCEDURAL_ASSET_PIPELINE.md`` "Deterministic Source Contract": "If artist
    variation is needed, variation is a named seed, not hidden chance."
    """

    seed: int = 2611
    quality: float = 1.0

    # Function category, required by 3DMODEL_EQUIPMENT_PROPS.md section 1.
    function: str = "bore"
    verb: str = "drill"

    # Overall scale. A hand tool is measured against the hand that holds it: a 0.26 m
    # body with a 0.10 m grip is a two-hand-span device, which is what the mandatory
    # reference frame `Diver_scanning_salvage_node` shows a diver holding.
    body_length_m: float = 0.255
    body_height_m: float = 0.068
    body_width_m: float = 0.052

    # Casing corner radius as a fraction of the half-height. This is the machined
    # extrusion radius, not a post-hoc chamfer -- see the module docstring. It keeps the
    # long casing edges inside the bible's ban on "ninety-degree mathematical corners"
    # even before the bevel pass runs.
    #
    # MEASURED CALIBRATION: the first render used 0.34 and the casing read as a soap bar.
    # At 0.34 of a 41 mm half-height the corner arc is 14 mm against a 31 mm half-width,
    # so the straight runs almost vanish and the profile is nearly an ellipse -- which is
    # exactly the "clean sci-fi plastic" TASTE.md rejects on sight.
    #
    # The casing now uses a SHARP rectangle and lets the bevel pass cut the chamfer, which
    # is what the bible prescribes and what the width clamp needs; see _rect_profile.
    # These two fields survive for the trigger lug, which is small enough that a
    # construction radius is the cheaper route.
    casing_corner_radius: float = 0.22
    casing_profile_segments: int = 16

    # Edges per side on the casing rectangle. Three is deliberate and load-bearing: it
    # keeps every casing edge above 22 mm so the bevel clamp stays wide, while still
    # giving the decimator interior vertices to collapse at LOD1/LOD2.
    casing_edges_per_side: int = 3

    # The seam that splits the casing into an upper and a lower shell. Section 4 of the
    # hard-surface bible wants an "inset frame" and a "gasket or flange ring"; on a hand
    # tool that reads as a recessed parting line with a gasket lip inside it.
    seam_depth_m: float = 0.0055
    seam_width_m: float = 0.0092

    # Instrument bezel. The single strongest "this is an instrument" signal in the
    # reference frame: a round face set in a raised ring with a bolt circle.
    #
    # MEASURED CALIBRATION: at 26.5 mm radius and 8.8 mm height the first render showed a
    # flat oval plate lying on the shell -- it read as a sticker, not as a housing. The
    # ratio is what carries the read, so the radius came down and the height went up.
    bezel_radius_m: float = 0.0195
    bezel_wall_m: float = 0.0050
    bezel_height_m: float = 0.0135
    bezel_segments: int = 20
    bezel_bolt_count: int = 8

    # Grip. Section 2: "Handles need inner radius and grip ridges."
    grip_length_m: float = 0.101
    grip_radius_m: float = 0.0182
    grip_rake_deg: float = 17.0
    grip_rib_count: int = 7
    grip_rib_amplitude: float = 0.0021
    grip_segments: int = 14
    trigger_guard: bool = True

    # Working end. Section 2: "minimum 12 LOD0 for small parts, 24+ for hero cylinders."
    # MEASURED: at 26.8 mm the collar was as wide as the 30.6 mm tapered nose, so
    # collar -> chuck -> bit read as one smooth bulge instead of three machined
    # steps. 3DMODEL_EQUIPMENT_PROPS.md section 1 wants the prop to communicate
    # function; a stepless nose communicates nothing.
    collar_radius_m: float = 0.0212
    collar_length_m: float = 0.0175
    chuck_radius_m: float = 0.0141
    chuck_length_m: float = 0.0215
    bit_radius_m: float = 0.0092
    bit_length_m: float = 0.0625
    bit_flute_count: int = 3
    bit_flute_depth: float = 0.30
    bit_twist_turns: float = 0.72
    nose_segments: int = 24

    # Cooling / motor rib band on the rear casing.
    rib_band_count: int = 5
    rib_band_height_m: float = 0.0028

    # Stand-off guard rail. The detail that breaks the silhouette hardest, taken straight
    # from the salvage reference frame's hull rails on stubs.
    guard_rail: bool = True
    guard_rail_radius_m: float = 0.0050
    guard_rail_standoff_m: float = 0.0215
    guard_rail_segments: int = 9

    # Cable gland. PROCEDURAL_ASSET_PIPELINE.md "cable glands"; section 2 forbids
    # "unthickened curves", so the lead is a capped tube, never a zero-width strip.
    cable_gland: bool = True
    cable_radius_m: float = 0.0068
    cable_length_m: float = 0.0295

    # Fastener field on the casing seam.
    casing_bolt_count: int = 6
    bolt_radius_m: float = 0.0027
    bolt_height_m: float = 0.0019
    bolt_segments: int = 8

    # ---- wear authoring -------------------------------------------------
    # These are ART constants for THIS generator's material assignment, not bible
    # thresholds, so they live here and not in law.py: law.py holds numbers a bible
    # states, and no bible states how much faster bare metal polishes than paint.
    # 3DMODEL_HARD_SURFACE_MODULES.md section 5 does state the FORM of the equation --
    # "wear = convexity * exposureMask * materialWearCoefficient" -- and this is the
    # coefficient term of exactly that.
    wear_coefficient_structural: float = 0.70
    wear_coefficient_bare_metal: float = 1.00
    wear_coefficient_gasket: float = 0.45
    wear_coefficient_glass: float = 0.15

    # "grime = cavity * downwardBias * wetnessRoute". A submerged tool is wet
    # everywhere, so the route term is 1.0 by default and is named rather than implied.
    wetness_route: float = 1.0
    grime_downward_bias_floor: float = 0.34

    def bevel_hero(self) -> bool:
        """A hand tool is inspected at arm's length, so it takes the hero segment
        ceiling from ``law.bevel_segments_for``: 3-6 segments at high quality rather
        than the 4 cap used for background geometry."""
        return True

    def profile_segments(self) -> int:
        """Casing ring resolution, continuous in GlobalQualityWeight.

        Never below 12: section 2 sets that as the LOD0 floor for small cylindrical
        parts, and the casing's rounded corners are cylindrical sections.
        """
        return max(12, int(round(self.casing_profile_segments *
                                 (0.62 + 0.38 * law.saturate(self.quality)))))

    def nose_ring_segments(self) -> int:
        """Bore-collar and bit resolution. Floor of 24 because section 2 requires
        "24+ for hero cylinders" and the bore collar is the closest-inspected round
        form on the asset."""
        return max(24, int(round(self.nose_segments *
                                 (0.75 + 0.25 * law.saturate(self.quality)))))


@dataclass
class PartRecord:
    """One functional part, kept so the proof packet can list function, not triangles."""

    name: str
    function: str
    material_slot: int
    band: str
    faces: int


@dataclass
class HandToolResult:
    name: str
    lods: list = field(default_factory=list)
    collider: Optional[object] = None
    parts: List[PartRecord] = field(default_factory=list)
    bevel_reports: dict = field(default_factory=dict)
    shading: Optional[object] = None
    ao_report: Optional[object] = None
    channel_report: dict = field(default_factory=dict)
    uv_report: dict = field(default_factory=dict)
    topology: dict = field(default_factory=dict)
    lod_purged: dict = field(default_factory=dict)
    mesh_reports: list = field(default_factory=list)
    chain_failures: list = field(default_factory=list)
    collider_failures: list = field(default_factory=list)
    preview_paths: tuple = ()
    channel_stats: tuple = ()
    fbx_path: str = ""
    manifest_path: str = ""
    orientation: dict = field(default_factory=dict)


# ---------------------------------------------------------------------------
# Stage 2/3: primitive kit
# ---------------------------------------------------------------------------
# Written locally rather than calling bmesh.ops.create_cone/create_cube for two
# reasons that both cost real rework elsewhere in this pipeline:
#
#   1. Every part this asset needs is a LOFT of rings whose radius varies along the
#      axis AND around the ring -- stepped collars, ribbed grips, helically fluted
#      bits, rounded-rectangle casings. A create_cone call cannot express any of them,
#      so they would each need a modifier stack or a displacement pass afterwards.
#   2. bmesh.ops primitive signatures have been renamed across versions
#      (diameter1 -> radius1). A kwarg probe wrapped in try/except is precisely the
#      silent-degeneracy pattern BLENDER_API_TRAPS.md warns about; owning the vertex
#      rings removes the dependency instead of guarding it.
#
# Winding is fixed by construction so GATE_INCONSISTENT_WINDING cannot fire: every
# side quad is emitted in the same rotational order and caps are wound to face outward
# along their own axis.


def _frame(axis: Vector) -> tuple:
    """Orthonormal (right, up, forward) with ``forward`` along ``axis``."""
    forward = axis.normalized()
    reference = UP_AXIS if abs(forward.dot(UP_AXIS)) < 0.94 else SIDE_AXIS
    right = forward.cross(reference)
    if right.length < 1e-9:
        right = Vector((1.0, 0.0, 0.0))
    right.normalize()
    up = right.cross(forward)
    up.normalize()
    return right, up, forward


def _rounded_rect_profile(segments: int, half_width: float, half_height: float,
                          corner_radius: float) -> list:
    """Unit-ish rounded-rectangle outline as (u, v) pairs, counter-clockwise.

    A superellipse would be smoother to write but its corner curvature never actually
    reaches a constant radius, so the casing would read as a pillow rather than as an
    extruded machined shell. This walks four straight runs joined by four true circular
    arcs, which is what an extruded aluminium casing profile is.
    """
    radius = max(1e-5, min(corner_radius, min(half_width, half_height) * 0.98))
    inner_w = max(1e-6, half_width - radius)
    inner_h = max(1e-6, half_height - radius)

    per_corner = max(2, segments // 4)
    points = []
    corners = (
        (inner_w, inner_h, 0.0),
        (-inner_w, inner_h, math.pi * 0.5),
        (-inner_w, -inner_h, math.pi),
        (inner_w, -inner_h, math.pi * 1.5),
    )
    for cx, cy, start in corners:
        for step in range(per_corner + 1):
            angle = start + (math.pi * 0.5) * (step / float(per_corner))
            points.append((cx + radius * math.cos(angle),
                           cy + radius * math.sin(angle)))
    # Drop near-coincident arc endpoints where consecutive corners meet.
    #
    # The epsilon is DISTANCE-based and generous (0.1 mm), not the 1e-9 exact-equality
    # test the first version used. Two arc endpoints that differ by 1e-12 m are not
    # duplicates by that test, so they survived as a pair of vertices 1 picometre apart --
    # and every quad built between them was a sliver. That is the dominant source of the
    # GATE_DEGENERATE_TRIANGLE and GATE_ZERO_AREA_UV_TRIANGLE failures the first run
    # produced, and law.py's own UV notes record that a near-degenerate triangle also
    # makes the stretch metric numerically ill-conditioned, so it inflates a third gate.
    epsilon = 1.0e-4

    def _close(a, b) -> bool:
        return math.hypot(a[0] - b[0], a[1] - b[1]) < epsilon

    cleaned = []
    for point in points:
        if cleaned and _close(point, cleaned[-1]):
            continue
        cleaned.append(point)
    while len(cleaned) > 3 and _close(cleaned[0], cleaned[-1]):
        cleaned.pop()
    return cleaned


def _rect_profile(per_side: int, half_width: float, half_height: float) -> list:
    """Sharp rectangular outline with ``per_side`` edges per side, counter-clockwise.

    WHY THE CASING USES THIS INSTEAD OF THE ROUNDED PROFILE, measured.
    ``3dmodel.md`` section 4 asks for a 0.006-0.018 m chamfer on a small handheld prop
    AND clamps it at step 5 to "20 percent of the shortest adjacent edge". Those two
    clauses only agree when the shortest adjacent edge is at least 0.030 m. A
    rounded-rectangle casing at 24 profile segments has 1.16 mm corner-arc chords, so the
    clamp collapsed the whole structural bevel to 0.09 mm -- measured, and invisible in
    the render.

    A sharp rectangle with three edges per side gives 22.7 mm edges and a 4.53 mm clamp:
    49x wider, and a chamfer that actually catches a highlight. It also puts the corner
    where the bevel pass can see it, which is the mechanism the bible prescribes rather
    than a construction radius that pre-empts it.
    """
    per = max(1, per_side)
    corners = ((half_width, half_height), (-half_width, half_height),
               (-half_width, -half_height), (half_width, -half_height))
    points = []
    for index in range(4):
        ax, ay = corners[index]
        bx, by = corners[(index + 1) % 4]
        for step in range(per):
            t = step / float(per)
            points.append((ax + (bx - ax) * t, ay + (by - ay) * t))
    return points


def _circle_profile(segments: int, radius: float) -> list:
    return [(radius * math.cos(math.tau * i / segments),
             radius * math.sin(math.tau * i / segments))
            for i in range(segments)]


# ---------------------------------------------------------------------------
# Surface seating
# ---------------------------------------------------------------------------
# THE BUG THIS EXISTS TO PREVENT, because it cost a whole render iteration and produced
# a plausible asset with no error anywhere.
#
# The first pass seated every bolted-on detail at a FRACTION of the casing half-extent --
# the bezel at 0.86 of the half-height, the seam bolts at 0.92 of the half-width, the
# guard-rail anchors at 0.80/0.55, the cable gland at 0.58 of the half-length. Every one
# of those fractions is less than 1.0, so every detail was seated INSIDE the shell.
# Measured on the iteration-1 numbers: the bezel base sat 5.7 mm under the top face and
# its bolts topped out 3.8 mm BELOW the surface. The renders showed a bare pillow with a
# sticker on it, and nothing failed: the geometry existed, the validator saw triangles,
# the triangle budget was consumed by parts no camera could ever see.
#
# A fraction of a half-extent is not a surface. These helpers return the actual surface
# coordinate, and callers subtract a small SEAT_BITE so the detail intersects the shell by
# a fraction of a millimetre instead of floating a hair above it.

SEAT_BITE_M = 0.0004


def _flat_run_limits(spec: "HandToolSpec") -> tuple:
    """(half_width, half_height, inner_width, inner_height) of the casing profile.

    ``inner_*`` bound the straight runs. Outside them the surface curves through the
    corner arc, so a detail seated there needs the arc's coordinate rather than the flat
    face's -- which is why every caller is asserted against these limits.
    """
    half_w = spec.body_width_m * 0.5
    half_h = spec.body_height_m * 0.5
    corner = max(1e-5, min(half_h * spec.casing_corner_radius,
                           min(half_w, half_h) * 0.98))
    return half_w, half_h, max(1e-6, half_w - corner), max(1e-6, half_h - corner)


def _top_face_z(spec: "HandToolSpec", at_x: float = 0.0) -> float:
    """Z of the casing's upper surface above ``at_x``.

    The casing profile is a SHARP rectangle (``_rect_profile``), so the top face is flat at
    the half-height across the whole width and the corner chamfer is cut afterwards by the
    bevel pass. An earlier version of this helper still modelled the retired rounded
    profile and returned a LOWER z near the flanks -- which would have re-buried the outer
    bezel bolts exactly as the original fraction-of-half-extent bug did, one abstraction
    layer further in. Callers outside the width are clamped rather than extrapolated.
    """
    half_w, half_h, _inner_w, _inner_h = _flat_run_limits(spec)
    if abs(at_x) > half_w:
        return half_h
    return half_h


def _flank_face_x(spec: "HandToolSpec", at_z: float = 0.0) -> float:
    """X of the casing's right-hand surface at height ``at_z``. Flat, per _rect_profile."""
    half_w, half_h, _inner_w, _inner_h = _flat_run_limits(spec)
    if abs(at_z) > half_h:
        return half_w
    return half_w


def _rear_face_y(spec: "HandToolSpec") -> float:
    """Y of the casing's rear cap. Mirrors the station table in ``_build_casing``."""
    return -spec.body_length_m * 0.62


def _nose_face_y(spec: "HandToolSpec") -> float:
    return spec.body_length_m * 0.38


def _loft(bm: bmesh.types.BMesh, rings: Sequence[Sequence[Vector]], *,
          material_index: int, cap_start: bool = True,
          cap_end: bool = True) -> list:
    """Bridge consecutive equal-length vertex rings into a closed solid.

    Returns the created faces so the caller can record a part's face count without
    re-deriving it from the whole mesh.
    """
    if len(rings) < 2:
        raise ValueError("a loft needs at least two rings")
    width = len(rings[0])
    for ring in rings:
        if len(ring) != width:
            raise ValueError("all loft rings must have the same vertex count")

    vert_rings = []
    for ring in rings:
        vert_rings.append([bm.verts.new(position) for position in ring])
    bm.verts.ensure_lookup_table()

    faces = []
    for level in range(len(vert_rings) - 1):
        lower = vert_rings[level]
        upper = vert_rings[level + 1]
        for index in range(width):
            nxt = (index + 1) % width
            quad = (lower[index], lower[nxt], upper[nxt], upper[index])
            # Degenerate rings (a radius that collapsed to zero) would produce
            # zero-area faces, which GATE_DEGENERATE_TRIANGLE rejects at save time.
            # Skipping them here is cheaper than repairing them later, and the caller
            # sees the shortfall in the returned face count.
            if (quad[0].co - quad[2].co).length < 1e-9:
                continue
            faces.append(bm.faces.new(quad))
    if cap_start:
        faces.append(bm.faces.new(tuple(reversed(vert_rings[0]))))
    if cap_end:
        faces.append(bm.faces.new(tuple(vert_rings[-1])))

    for face in faces:
        face.material_index = material_index
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    return faces


def _purge_degenerate_faces(bm: bmesh.types.BMesh) -> int:
    """Delete faces below the validator's own area epsilon. Returns the count deleted.

    Deliberately NARROWER than ``mesh_ops.weld_and_clean``, which is the right tool for a
    single shell and the wrong one for a 38-part interpenetrating assembly -- it also
    merges by distance and fills boundary loops, and doing that across this asset erased
    an entire declared material slot (the measurement is in the band loop).

    The threshold is ``law.DEGENERATE_TRIANGLE_AREA_EPS``, the SAME constant
    ``validate._gate_triangles`` tests against, so this cannot pass geometry the gate will
    then reject. A cleanup calibrated to a different epsilon than the gate it feeds is
    exactly the near-miss that produced iteration 3's 1.06e-08-against-1e-07 failure.
    Loose vertices are removed too: a vertex with no face has no normal, and
    GATE_NORMAL_LENGTH_OUT_OF_RANGE reports that as a zero-length normal.
    """
    # TRIANGULATE FIRST, and this ordering is the whole fix.
    #
    # The gate measures TRIANGLES, from mesh.loop_triangles. This purge measured FACES. A
    # quad whose total area clears the epsilon can still split into one healthy triangle
    # and one sliver, so a face-area test passes geometry the triangle gate then rejects --
    # measured in iteration 4 as a survivor at 9.67e-08 against a 1e-07 epsilon, a 3%
    # margin, which is exactly what a quad split produces. Triangulating before testing
    # makes the two measurements the same measurement.
    if bm.faces:
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
    doomed = [face for face in bm.faces
              if face.calc_area() <= law.DEGENERATE_TRIANGLE_AREA_EPS]
    if doomed:
        bmesh.ops.delete(bm, geom=doomed, context="FACES")
    orphans = [vert for vert in bm.verts if not vert.link_faces]
    if orphans:
        bmesh.ops.delete(bm, geom=orphans, context="VERTS")
    return len(doomed)


def purge_object_degenerates(obj: bpy.types.Object) -> int:
    """Run the sliver purge on an object's mesh datablock. Returns faces deleted.

    Needed because Decimate/COLLAPSE creates its OWN slivers and it runs inside
    ``mesh_ops.build_lod_chain``, where this generator has no hook. Measured in iteration
    4: LOD1 carried a zero-length tangent and LOD2 a corner normal of length 0.0415. Both
    are what a collapsed triangle produces -- a zero UV area yields no tangent basis and a
    zero surface area yields no normal -- so cleaning each level after the chain is built
    is the only place a caller can reach them without editing the shared library.
    """
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    try:
        removed = _purge_degenerate_faces(bm)
        bm.to_mesh(obj.data)
    finally:
        bm.free()
    obj.data.update()
    return removed


def _ring_at(profile: Sequence[Sequence[float]], origin: Vector, right: Vector,
             up: Vector, scale_u: float = 1.0, scale_v: float = 1.0,
             radial_modulation=None) -> list:
    """Place a 2D profile into 3D at ``origin``.

    ``radial_modulation(index, u, v) -> float`` multiplies the point's distance from the
    ring centre, which is how grip ribs and helical bit flutes are expressed without a
    second geometry pass.
    """
    out = []
    for index, (u, v) in enumerate(profile):
        du = u * scale_u
        dv = v * scale_v
        if radial_modulation is not None:
            factor = radial_modulation(index, du, dv)
            du *= factor
            dv *= factor
        out.append(origin + right * du + up * dv)
    return out


# ---------------------------------------------------------------------------
# Stage 2/3: functional parts
# ---------------------------------------------------------------------------
# Material slots follow 3dmodel.md section 6 and 3DMODEL_EQUIPMENT_PROPS.md section 3:
#   0 painted structural casing, 1 exposed/bare metal at the working end and bevel
#   trim, 2 rubber gasket and grip, 3 emissive display.
# Every declared slot must carry faces or GATE_SUBMESH_EMPTY_DECLARED_SLOT fires, so the
# assignment is checked against the part list at the end of assembly rather than trusted.

SLOT_CASING = law.MATERIAL_SLOT_PRIMARY
SLOT_BARE_METAL = law.MATERIAL_SLOT_CUT_EDGE
SLOT_GASKET = law.MATERIAL_SLOT_TRIM
SLOT_DISPLAY = law.MATERIAL_SLOT_EMISSIVE

MATERIAL_ROLES = (
    (SLOT_CASING, "PaintedCasing"),
    (SLOT_BARE_METAL, "BareMetalEdge"),
    (SLOT_GASKET, "RubberGasket"),
    (SLOT_DISPLAY, "InstrumentGlass"),
)

WEAR_COEFFICIENT_BY_SLOT_FIELD = {
    SLOT_CASING: "wear_coefficient_structural",
    SLOT_BARE_METAL: "wear_coefficient_bare_metal",
    SLOT_GASKET: "wear_coefficient_gasket",
    SLOT_DISPLAY: "wear_coefficient_glass",
}


def _build_casing(bm: bmesh.types.BMesh, spec: HandToolSpec,
                  parts: List[PartRecord]) -> None:
    """Casing shells split by a recessed parting seam with a gasket lip inside it.

    The seam is the detail that converts one volume into a manufactured assembly. It is
    a genuine geometric recess -- two rings pulled inward -- rather than a texture line,
    because 3DMODEL_HARD_SURFACE_MODULES.md section 10 rejects "any panel ... larger
    than 1.5 m without seams, trim, decals, or material breakup" and section 1 asks for
    meso-form "bevels, inset panels, ribs, braces, pressure rings, trims".
    """
    right, up, forward = _frame(BORE_AXIS)
    half_w = spec.body_width_m * 0.5
    half_h = spec.body_height_m * 0.5
    # Sharp rectangle: the bevel pass owns the corner, not the profile. See _rect_profile
    # for the measured reason -- a construction radius here starved the clamp.
    profile = _rect_profile(spec.casing_edges_per_side, half_w, half_h)

    length = spec.body_length_m
    # Rear of the casing sits behind the origin, nose at the front, so the object's
    # pivot lands near the grip -- which is where a held tool is actually pivoted.
    rear = -length * 0.62
    front = length * 0.38

    # Stations carry NO seam recess any more, and that is a bevel-clamp decision as much
    # as an art one. A 9 mm seam needs ring spacings of ~1.6 mm, and the bevel width clamp
    # takes the minimum over the whole selection, so those two rings alone held the entire
    # casing chamfer at 0.09 mm across two measured iterations. The parting line is now a
    # separate clamp BAND wrapped around the shell -- which is what a real pressure casing
    # has anyway, and which actually reads in a render where the recessed groove did not.
    stations = [
        (rear, 0.88),                        # rear taper
        (rear + length * 0.055, 1.0),
        (front - length * 0.09, 1.0),
        (front, 0.90),                       # front taper into the collar
    ]

    rings = [_ring_at(profile, forward * t, right, up, scale, scale)
             for t, scale in stations]
    faces = _loft(bm, rings, material_index=SLOT_CASING)
    parts.append(PartRecord("Casing", "pressure shell", SLOT_CASING, "casing",
                            len(faces)))

    # Trigger-lug-scale detail and everything finer is emitted by the callers into their
    # own bevel bands; the casing bmesh keeps only long-edged geometry.


def _build_seam_band(bm: bmesh.types.BMesh, spec: HandToolSpec,
                     parts: List[PartRecord]) -> None:
    """Raised gasket clamp band at the casing parting line.

    Replaces the recessed groove the first two iterations used. Two independent reasons,
    both measured rather than preferred: the groove was invisible in every one of the four
    iteration-1 views, and its 1.6 mm ring spacing was the single edge that clamped the
    casing's bevel width to 0.09 mm. A proud band reads at a glance, catches a highlight
    on its own chamfer, and traps grime along its lower lip -- which is exactly where
    3DMODEL_HARD_SURFACE_MODULES.md section 5 says channel G belongs, "high below seams".
    """
    right, up, forward = _frame(BORE_AXIS)
    half_w = spec.body_width_m * 0.5
    half_h = spec.body_height_m * 0.5
    profile = _rect_profile(spec.casing_edges_per_side, half_w, half_h)

    length = spec.body_length_m
    seam_centre = _rear_face_y(spec) + length * 0.46
    half_seam = spec.seam_width_m * 0.5
    proud = 1.0 + spec.seam_depth_m / max(1e-6, half_h)

    rings = [
        _ring_at(profile, forward * (seam_centre - half_seam), right, up),
        _ring_at(profile, forward * (seam_centre - half_seam * 0.55), right, up,
                 proud, proud),
        _ring_at(profile, forward * (seam_centre + half_seam * 0.55), right, up,
                 proud, proud),
        _ring_at(profile, forward * (seam_centre + half_seam), right, up),
    ]
    faces = _loft(bm, rings, material_index=SLOT_GASKET,
                  cap_start=False, cap_end=False)
    parts.append(PartRecord("SeamClampBand", "parting-line seal", SLOT_GASKET,
                            "detail", len(faces)))


def _build_cooling_ribs(bm: bmesh.types.BMesh, spec: HandToolSpec,
                        parts: List[PartRecord]) -> None:
    """Motor cooling rib band on the rear third of the casing.

    Cheapest honest silhouette break available on a motor housing, and the rib valleys
    give the Cycles AO bake real cavities to find rather than a uniform shell.
    """
    right, up, forward = _frame(BORE_AXIS)
    half_w = spec.body_width_m * 0.5
    half_h = spec.body_height_m * 0.5
    profile = _rect_profile(spec.casing_edges_per_side, half_w, half_h)

    length = spec.body_length_m
    rib_span = length * 0.26
    rib_start = _rear_face_y(spec) + length * 0.085
    count = max(0, spec.rib_band_count)
    for index in range(count):
        t = rib_start + (rib_span * index / max(1, count - 1) if count > 1 else 0.0)
        thickness = spec.rib_band_height_m
        outer = 1.0 + spec.rib_band_height_m / max(1e-6, half_h)
        rib_rings = [
            _ring_at(profile, forward * (t - thickness * 0.5), right, up),
            _ring_at(profile, forward * (t - thickness * 0.22), right, up,
                     outer, outer),
            _ring_at(profile, forward * (t + thickness * 0.22), right, up,
                     outer, outer),
            _ring_at(profile, forward * (t + thickness * 0.5), right, up),
        ]
        rib_faces = _loft(bm, rib_rings, material_index=SLOT_CASING,
                          cap_start=False, cap_end=False)
        parts.append(PartRecord("CoolingRib{i}".format(i=index),
                                "motor heat rejection", SLOT_CASING, "detail",
                                len(rib_faces)))


def _build_instrument_bezel(bm: bmesh.types.BMesh, spec: HandToolSpec,
                           parts: List[PartRecord]) -> None:
    """Raised annular bezel with a recessed glass face and a bolt circle.

    Modelled as a ring plus an inset disc rather than as a boolean pocket. A boolean on
    a rounded-rectangle casing produces exactly the degenerate slivers the validator
    rejects, and the reference frame shows the real-world answer anyway: instrument
    faces on submersible hardware are bolted-on bezels standing proud of the shell, not
    holes milled into it.
    """
    # THE PLANE BUG THIS COMMENT EXISTS TO PREVENT A REPEAT OF.
    #
    # A boss standing on the TOP face is a ring in the X-Y plane extruded along +Z. The
    # first three iterations built the ring from ``_frame(BORE_AXIS)``, whose ``right``/
    # ``up`` are X and Z -- so the circle lay in the X-Z plane, edge-on to the top face,
    # and lofting it "upward" produced a sheared tube lying against the shell instead of a
    # bezel standing on it. The render showed no instrument face at all while the part list
    # still counted 41 glass faces and every gate stayed silent about it. The frame is
    # taken from the EXTRUSION axis, and here that axis is UP.
    ring_u, ring_v, extrude = _frame(UP_AXIS)
    seat = BORE_AXIS * (spec.body_length_m * 0.02) \
        + UP_AXIS * (_top_face_z(spec) - SEAT_BITE_M)

    segments = max(12, spec.bezel_segments)
    r_out = spec.bezel_radius_m
    r_in = max(1e-4, r_out - spec.bezel_wall_m)
    height = spec.bezel_height_m

    # Bezel wall: outer wall up, across the rim, back down the inner wall. Walking the
    # annulus in one pass means the tube closes on itself and needs no rim cap.
    outer = _circle_profile(segments, r_out)
    inner = _circle_profile(segments, r_in)
    wall_rings = [
        _ring_at(outer, seat, ring_u, ring_v),
        _ring_at(outer, seat + extrude * height, ring_u, ring_v),
        _ring_at(inner, seat + extrude * height, ring_u, ring_v),
        _ring_at(inner, seat + extrude * (height * 0.28), ring_u, ring_v),
    ]
    wall_faces = _loft(bm, wall_rings, material_index=SLOT_BARE_METAL,
                       cap_start=True, cap_end=False)
    parts.append(PartRecord("InstrumentBezel", "readout housing", SLOT_BARE_METAL,
                            "fine", len(wall_faces)))

    # Recessed glass: a shallow dished disc at the floor of the bezel well.
    glass_rings = [
        _ring_at(_circle_profile(segments, r_in * 0.995),
                 seat + extrude * (height * 0.28), ring_u, ring_v),
        _ring_at(_circle_profile(segments, r_in * 0.62),
                 seat + extrude * (height * 0.20), ring_u, ring_v),
        _ring_at(_circle_profile(segments, r_in * 0.20),
                 seat + extrude * (height * 0.15), ring_u, ring_v),
    ]
    glass_faces = _loft(bm, glass_rings, material_index=SLOT_DISPLAY,
                        cap_start=False, cap_end=True)
    parts.append(PartRecord("InstrumentGlass", "depth/torque readout", SLOT_DISPLAY,
                            "fine", len(glass_faces)))

    # Bolt circle around the bezel. Section 2 of the props bible allows fasteners as
    # geometry at LOD0 and requires them to become mask detail later, which the LOD
    # chain does by collapsing them.
    bolt_ring_radius = r_out + spec.bolt_radius_m * 1.6
    for index in range(max(0, spec.bezel_bolt_count)):
        angle = math.tau * index / max(1, spec.bezel_bolt_count)
        offset = SIDE_AXIS * (math.cos(angle) * bolt_ring_radius) \
            + BORE_AXIS * (math.sin(angle) * bolt_ring_radius)
        # Each bolt sits on the surface ABOVE ITS OWN X, not above the bezel centre --
        # the bolt circle is wide enough to reach the corner arc, where the shell is
        # lower, and a shared height would leave the outermost bolts floating.
        base = Vector((offset.x, offset.y + spec.body_length_m * 0.02,
                       _top_face_z(spec, offset.x) - SEAT_BITE_M))
        _build_bolt(bm, spec, base, UP_AXIS, parts,
                    label="BezelBolt{i}".format(i=index))


def _build_bolt(bm: bmesh.types.BMesh, spec: HandToolSpec, position: Vector,
                axis: Vector, parts: List[PartRecord], *, label: str) -> None:
    """A domed hex-ish boss. Eight segments reads as a fastener at arm's length and
    costs ~24 triangles; a 16-segment bolt costs double for no readable gain, and the
    bible's LOD1 rule removes it entirely."""
    right, up, forward = _frame(axis)
    segments = max(6, spec.bolt_segments)
    radius = spec.bolt_radius_m
    height = spec.bolt_height_m
    profile = _circle_profile(segments, radius)
    rings = [
        _ring_at(profile, position, right, up),
        _ring_at(profile, position + forward * height * 0.72, right, up),
        _ring_at(_circle_profile(segments, radius * 0.78),
                 position + forward * height, right, up),
    ]
    faces = _loft(bm, rings, material_index=SLOT_BARE_METAL)
    parts.append(PartRecord(label, "fastener", SLOT_BARE_METAL, "micro", len(faces)))


def _build_grip(bm: bmesh.types.BMesh, spec: HandToolSpec,
                parts: List[PartRecord]) -> None:
    """Raked handle with grip ridges and a trigger lug.

    Section 2: "Handles need inner radius and grip ridges." The ridges are radius
    modulation on the loft rings, so they are real silhouette -- a normal map cannot
    supply the contact shadow a gloved hand needs to read the grip's scale.
    """
    rake = math.radians(spec.grip_rake_deg)
    # Handle axis: down and slightly back from the casing.
    axis = (UP_AXIS * -1.0) + BORE_AXIS * -math.tan(rake)
    axis.normalize()
    right, up, forward = _frame(axis)

    half_h = spec.body_height_m * 0.5
    root = BORE_AXIS * (-spec.body_length_m * 0.14) + UP_AXIS * (-half_h * 0.72)

    rib_count = max(0, spec.grip_rib_count)
    # Two rings per rib plus the shoulder and the butt: enough to cut a real groove
    # rather than a wobble.
    levels = 4 + rib_count * 2
    rings = []
    for level in range(levels + 1):
        t = level / float(levels)
        along = spec.grip_length_m * t
        # Palm swell: thickest a third of the way down, tapering to the butt, which is
        # what a hand actually grips. A straight cylinder reads as a broom handle.
        swell = 1.0 + 0.16 * math.sin(math.pi * min(1.0, t * 1.18))
        taper = 1.0 - 0.20 * t * t
        base = spec.grip_radius_m * swell * taper
        if rib_count > 0:
            phase = t * rib_count * math.tau
            rib = 1.0 + (spec.grip_rib_amplitude / max(1e-6, base)) \
                * max(0.0, math.cos(phase))
        else:
            rib = 1.0
        radius = base * rib
        rings.append(_ring_at(_circle_profile(max(10, spec.grip_segments), radius),
                              root + forward * along, right, up))
    faces = _loft(bm, rings, material_index=SLOT_GASKET)
    parts.append(PartRecord("Grip", "hand contact zone", SLOT_GASKET, "structural",
                            len(faces)))

    if not spec.trigger_guard:
        return
    # Trigger lug: a short blocky finger rest forward of the grip root. Deliberately
    # boxy so the bevel pass has an unambiguous 90-degree edge set to chamfer -- proof
    # the pass ran, visible in the studio render as a soft highlight roll.
    lug_len = spec.grip_radius_m * 2.5
    lug_w = spec.grip_radius_m * 0.85
    lug_h = spec.grip_radius_m * 0.62
    lug_root = root + BORE_AXIS * (spec.grip_radius_m * 0.55) \
        - UP_AXIS * (spec.grip_length_m * 0.20)
    lug_profile = _rounded_rect_profile(12, lug_w, lug_h, lug_h * 0.22)
    lug_rings = [
        _ring_at(lug_profile, lug_root, SIDE_AXIS, UP_AXIS, 1.0, 1.0),
        _ring_at(lug_profile, lug_root + BORE_AXIS * lug_len * 0.72,
                 SIDE_AXIS, UP_AXIS, 0.94, 0.94),
        _ring_at(lug_profile, lug_root + BORE_AXIS * lug_len,
                 SIDE_AXIS, UP_AXIS, 0.55, 0.62),
    ]
    lug_faces = _loft(bm, lug_rings, material_index=SLOT_CASING)
    parts.append(PartRecord("TriggerLug", "index-finger rest", SLOT_CASING,
                            "structural", len(lug_faces)))


def _build_bore_head(bm: bmesh.types.BMesh, spec: HandToolSpec,
                     parts: List[PartRecord], *,
                     bit_bm: Optional[bmesh.types.BMesh] = None) -> None:
    """Stepped collar, chuck, then a helically fluted bit.

    Three diameters instead of one cone because the step shoulders are what make the
    nose read as an assembly of machined parts. The flutes are a real helical radius
    modulation: a drill bit whose cutting geometry is painted on is the "primitive with
    a noise texture" TASTE.md rejects on sight.
    """
    right, up, forward = _frame(BORE_AXIS)
    segments = spec.nose_ring_segments()
    base = spec.body_length_m * 0.38

    # Collar: flange face into a short barrel. Section 4 of the hard-surface bible
    # wants "gasket or flange ring around airlocks and pipe sockets"; the bore mouth is
    # the same class of opening.
    collar_profile = _circle_profile(segments, spec.collar_radius_m)
    collar_rings = [
        _ring_at(_circle_profile(segments, spec.collar_radius_m * 0.90),
                 forward * base, right, up),
        _ring_at(collar_profile, forward * (base + spec.collar_length_m * 0.22),
                 right, up),
        _ring_at(collar_profile, forward * (base + spec.collar_length_m * 0.78),
                 right, up),
        _ring_at(_circle_profile(segments, spec.collar_radius_m * 0.82),
                 forward * (base + spec.collar_length_m), right, up),
    ]
    collar_faces = _loft(bm, collar_rings, material_index=SLOT_BARE_METAL)
    parts.append(PartRecord("BoreCollar", "bore mouth flange", SLOT_BARE_METAL,
                            "structural", len(collar_faces)))

    chuck_base = base + spec.collar_length_m
    chuck_profile = _circle_profile(segments, spec.chuck_radius_m)
    chuck_rings = [
        _ring_at(chuck_profile, forward * chuck_base, right, up),
        _ring_at(chuck_profile, forward * (chuck_base + spec.chuck_length_m * 0.62),
                 right, up),
        _ring_at(_circle_profile(segments, spec.chuck_radius_m * 0.72),
                 forward * (chuck_base + spec.chuck_length_m), right, up),
    ]
    chuck_faces = _loft(bm, chuck_rings, material_index=SLOT_BARE_METAL)
    parts.append(PartRecord("Chuck", "bit clamp", SLOT_BARE_METAL, "structural",
                            len(chuck_faces)))

    # Fluted bit. Radius modulation is a function of BOTH the ring index (angle) and
    # the position along the bit, which is what makes the groove helical instead of a
    # set of parallel rings.
    bit_base = chuck_base + spec.chuck_length_m
    flutes = max(0, spec.bit_flute_count)
    # Level count kept lean on purpose. At 24 mandated radial segments each extra level
    # costs 48 triangles, and the whole asset has to land under 6000 WITHOUT a
    # reduce_to_budget pass -- because that pass collapsed the entire instrument-glass
    # submesh in iteration 2 and fired submesh_empty_declared_slot on all three LODs.
    levels = max(6, int(round(8 + 5 * law.saturate(spec.quality))))
    bit_rings = []
    for level in range(levels + 1):
        t = level / float(levels)
        along = spec.bit_length_m * t
        # Taper over the last fifth into a CHISEL point, not a needle.
        #
        # The first version tapered to 0.06 of the bit radius: 0.55 mm across 24
        # segments is a 0.14 mm chord, and once the flute modulation cut into that the
        # ring collapsed into slivers. A real bore bit ends in a chisel edge anyway, so
        # stopping the taper at 0.24 and capping is both cheaper and more accurate than
        # the needle it replaced.
        tip = 1.0 if t < 0.80 else max(0.24, 1.0 - (t - 0.80) / 0.20 * 0.76)
        radius = spec.bit_radius_m * tip

        def modulate(index, _du, _dv, _t=t, _seg=segments, _flutes=flutes):
            if _flutes <= 0:
                return 1.0
            theta = math.tau * index / _seg
            twist = math.tau * spec.bit_twist_turns * _t
            groove = math.cos(_flutes * (theta + twist))
            # Only cut inward: a flute is a removed channel, never a raised ridge.
            return 1.0 - spec.bit_flute_depth * max(0.0, groove)

        bit_rings.append(_ring_at(_circle_profile(segments, radius),
                                  forward * (bit_base + along), right, up,
                                  radial_modulation=modulate))
    # The bit goes into its OWN bevel band when the caller supplies one. Its fluted rings
    # carry the shortest edges on the whole asset (0.55 mm at the chisel tip), and leaving
    # it in with the collar and chuck is what held the nose chamfer at a tenth of what its
    # own geometry permits.
    target = bit_bm if bit_bm is not None else bm
    bit_faces = _loft(target, bit_rings, material_index=SLOT_BARE_METAL)
    parts.append(PartRecord("FlutedBit", "cutting geometry", SLOT_BARE_METAL,
                            "fine" if bit_bm is not None else "nose", len(bit_faces)))


def _build_guard_rail(bm: bmesh.types.BMesh, spec: HandToolSpec,
                      parts: List[PartRecord]) -> None:
    """Tubular rail standing off the casing on two stubs.

    Straight from the mandatory salvage reference, where every piece of submersible
    hardware carries stand-off rails. It is the highest-value silhouette detail
    available on a prop this size: it breaks the outline, it casts a contact shadow onto
    the shell, and it tells the player the instrument face is protected -- function, not
    decoration, which is what 3DMODEL_EQUIPMENT_PROPS.md section 8 rejects props for
    lacking.
    """
    _half_w, _half_h, inner_w, _inner_h = _flat_run_limits(spec)
    length = spec.body_length_m
    standoff = spec.guard_rail_standoff_m

    # Rails rise from the FLAT part of the top face, arc over the bezel, and return.
    # Iteration 1 anchored them at 0.80 of the half-width and 0.55 of the half-height,
    # which put both rails entirely inside the casing -- the renders showed no rail at
    # all. The anchor X is clamped into the straight run so the stub emerges from a flat
    # face rather than out of the corner arc at an angle.
    rail_x = min(inner_w * 0.72, _half_w * 0.62)
    rear_y = -length * 0.20
    front_y = length * 0.24
    # Sink the anchor slightly INTO the shell so the stub reads as a welded lug rather
    # than a tube resting on paint.
    anchor_z = _top_face_z(spec, rail_x) - spec.guard_rail_radius_m * 0.55
    top_z = _top_face_z(spec, rail_x) + standoff

    for mirror in (1.0, -1.0):
        x = rail_x * mirror
        path = [
            Vector((x, rear_y, anchor_z)),
            Vector((x, rear_y + length * 0.045, top_z)),
            Vector((x, front_y - length * 0.045, top_z)),
            Vector((x, front_y, anchor_z)),
        ]
        faces = _sweep_tube(bm, path, spec.guard_rail_radius_m,
                            max(6, spec.guard_rail_segments), SLOT_BARE_METAL)
        parts.append(PartRecord(
            "GuardRail{s}".format(s="R" if mirror > 0 else "L"),
            "instrument face protection", SLOT_BARE_METAL, "micro", len(faces)))


def _build_cable_gland(bm: bmesh.types.BMesh, spec: HandToolSpec,
                       parts: List[PartRecord]) -> None:
    """Cable port boss plus a short capped lead.

    Section 2: "Cables use tubes or bevelled ribbons with capped ends; no unthickened
    curves." The lead is short on purpose -- a long dangling cable on a rigid mesh
    cannot follow the hand and would read as broken the moment the tool moves.
    """
    half_h = spec.body_height_m * 0.5
    # Seated on the rear CAP, not at an arbitrary fraction of the length. Iteration 1
    # used -0.58 of the length against a rear face at -0.62, burying the gland 10 mm
    # inside the shell so only the lead emerged -- it read as a hook, not a cable port.
    port = BORE_AXIS * (_rear_face_y(spec) + SEAT_BITE_M) \
        + UP_AXIS * (half_h * 0.34)

    # Gland nut: a stubby stepped boss, which is what a real pressure gland looks like.
    right, up, forward = _frame(BORE_AXIS * -1.0)
    segments = 12
    gland_rings = [
        _ring_at(_circle_profile(segments, spec.cable_radius_m * 1.55), port,
                 right, up),
        _ring_at(_circle_profile(segments, spec.cable_radius_m * 1.55),
                 port + forward * spec.cable_radius_m * 0.9, right, up),
        _ring_at(_circle_profile(segments, spec.cable_radius_m * 1.15),
                 port + forward * spec.cable_radius_m * 1.15, right, up),
    ]
    gland_faces = _loft(bm, gland_rings, material_index=SLOT_BARE_METAL)
    parts.append(PartRecord("CableGland", "pressure cable port", SLOT_BARE_METAL,
                            "micro", len(gland_faces)))

    lead_start = port + forward * spec.cable_radius_m * 1.15
    path = [
        lead_start,
        lead_start + forward * spec.cable_length_m * 0.45
        - UP_AXIS * spec.cable_length_m * 0.18,
        lead_start + forward * spec.cable_length_m * 0.80
        - UP_AXIS * spec.cable_length_m * 0.55,
    ]
    lead_faces = _sweep_tube(bm, path, spec.cable_radius_m, 10, SLOT_GASKET)
    parts.append(PartRecord("CableLead", "power/data tether stub", SLOT_GASKET,
                            "micro", len(lead_faces)))


def _sweep_tube(bm: bmesh.types.BMesh, path: Sequence[Vector], radius: float,
                segments: int, material_index: int) -> list:
    """Sweep a capped circular tube along a polyline.

    Each ring is oriented by the average of the incoming and outgoing segment
    directions, so a corner produces a mitred bend rather than a pinch. Reusing one
    fixed frame for the whole path instead would collapse the ring wherever the path
    turns through the frame's own axis.
    """
    if len(path) < 2:
        raise ValueError("a swept tube needs at least two path points")
    directions = []
    for index in range(len(path)):
        if index == 0:
            direction = path[1] - path[0]
        elif index == len(path) - 1:
            direction = path[-1] - path[-2]
        else:
            direction = (path[index + 1] - path[index - 1])
        if direction.length < 1e-9:
            direction = Vector((0.0, 0.0, 1.0))
        directions.append(direction.normalized())

    profile = _circle_profile(segments, radius)
    rings = []
    for point, direction in zip(path, directions):
        right, up, _forward = _frame(direction)
        rings.append(_ring_at(profile, point, right, up))
    return _loft(bm, rings, material_index=material_index)


# ---------------------------------------------------------------------------
# Assembly
# ---------------------------------------------------------------------------

def _assign_materials(obj: bpy.types.Object) -> list:
    """Shared MAT_* datablocks in slot order.

    Fetch-or-create by name so a batch of variants shares one material set:
    3dmodel.md section 8 forbids "material-per-variant proliferation", and the Cycles
    AO bake refuses to run at all on an object with no material slot.
    """
    names = []
    for _slot, role in MATERIAL_ROLES:
        material_name = law.NAME_MATERIAL.format(
            family=law.Family.SMALL_PROP.value, role=role)
        material = bpy.data.materials.get(material_name)
        if material is None:
            material = bpy.data.materials.new(material_name)
            material.use_nodes = True
        obj.data.materials.append(material)
        names.append(material_name)
    return names


def build_tool_object(spec: HandToolSpec, name: str,
                      collection: bpy.types.Collection,
                      blackbox: BlackBox) -> tuple:
    """Assemble every part, bevel per size band, weld, shade. Returns (obj, parts, reports).

    The two bevel bmeshes are the whole reason this function exists in this shape. See
    the module docstring: one bevel call over the full assembly would clamp the casing's
    chamfer to a bolt stud's 20% and silently produce a razor-edged tool.
    """
    parts: List[PartRecord] = []

    # THREE bevel bands, split by SHORTEST FEATURE EDGE rather than by overall part size.
    #
    # Measured across two iterations: one band gave every part a 0.09 mm chamfer, because
    # mesh_ops.bevel_hard_edges takes the minimum shortest-adjacent-edge over the whole
    # selection and the fluted bit tip has 0.55 mm edges. Splitting by band scopes the
    # bible's clamp to comparable geometry, so the casing gets the wide chamfer its 22.7 mm
    # edges permit while the bit still gets one proportionate to a flute.
    #
    # Predicted clamps from the default spec: casing ~2.8 mm (14 mm min edge), nose
    # ~0.9 mm (4.5 mm chuck chord), fine ~0.1 mm (0.55 mm bit tip chord). The measured
    # widths are printed per band so a regression cannot hide behind the intent.
    casing = bmesh.new()
    nose = bmesh.new()
    fine = bmesh.new()

    _build_casing(casing, spec, parts)

    _build_bore_head(nose, spec, parts, bit_bm=fine)
    _build_grip(nose, spec, parts)

    _build_seam_band(fine, spec, parts)
    _build_cooling_ribs(fine, spec, parts)
    _build_instrument_bezel(fine, spec, parts)
    if spec.guard_rail:
        _build_guard_rail(fine, spec, parts)
    if spec.cable_gland:
        _build_cable_gland(fine, spec, parts)

    # Fastener field along the casing seam, both flanks. Seated on the real flank
    # surface: 0.92 of the half-width put every bolt 2.5 mm inside the shell in
    # iteration 1 and not one of them appeared in any of the four views.
    half_h = spec.body_height_m * 0.5
    seam_centre = -spec.body_length_m * 0.62 + spec.body_length_m * 0.46
    bolt_z = half_h * 0.10
    flank_x = _flank_face_x(spec, bolt_z) - SEAT_BITE_M
    for index in range(max(0, spec.casing_bolt_count)):
        frac = (index + 0.5) / max(1, spec.casing_bolt_count)
        along = seam_centre + (frac - 0.5) * spec.body_length_m * 0.30
        for mirror in (1.0, -1.0):
            position = BORE_AXIS * along + SIDE_AXIS * (flank_x * mirror) \
                + UP_AXIS * bolt_z
            _build_bolt(fine, spec, position, SIDE_AXIS * mirror, parts,
                        label="SeamBolt{i}{s}".format(
                            i=index, s="R" if mirror > 0 else "L"))

    reports = {}
    bands = (("casing", casing, spec.bevel_hero()),
             ("nose", nose, spec.bevel_hero()),
             ("fine", fine, False))
    for band, bm, hero in bands:
        # NO weld_and_clean here, and that omission is the fix for three gates.
        #
        # weld_and_clean is written for ONE shell: it merges by distance, resolves
        # 3+-face edges and fills open boundary loops. This asset is 38 interpenetrating
        # closed solids, and every loft is watertight and outward-wound by construction.
        # Merging by distance across the assembly fused surfaces that were never meant to
        # fuse. Measured in iteration 3: 992 faces deleted as degenerate, 259 boundary
        # loops filled, the instrument-glass submesh erased entirely
        # (GATE_SUBMESH_EMPTY_DECLARED_SLOT on all three LODs), a zero-length vertex
        # normal, and GATE_INCONSISTENT_WINDING from the shell enclosed inside the bezel
        # well being flipped by the global recalc.
        #
        # Interpenetration is not a defect here: 3dmodel.md declares no gate against
        # multiple components, hard-surface assemblies are modelled exactly this way, and
        # the buried junction faces are what the AO bake reads as contact cavities. What
        # DOES need cleaning is the sliver set bmesh.ops.bevel leaves behind, which is a
        # far narrower job than a weld.
        result = mesh_ops.bevel_hard_edges(
            bm, family=law.Family.SMALL_PROP, quality_weight=spec.quality,
            hero=hero, blackbox=blackbox)
        purged = _purge_degenerate_faces(bm)
        reports[band] = {
            "edgesConsidered": result.edges_considered,
            "edgesBeveled": result.edges_beveled,
            "widthM": round(result.width_m, 6),
            "segments": result.segments,
            "clampedByShortestEdgeRule": result.clamped,
            "degeneratePurgedAfterBevel": purged,
        }

    # Merge the bands into one datablock. bmesh has no append, so each extra band is baked
    # to a scratch mesh and read back through from_mesh, which is additive.
    mesh = bpy.data.meshes.new(name)
    casing.to_mesh(mesh)
    casing.free()

    merged = bmesh.new()
    merged.from_mesh(mesh)
    scratches = []
    for band, bm, _hero in bands[1:]:
        scratch = bpy.data.meshes.new("{n}_{b}".format(n=name, b=band))
        bm.to_mesh(scratch)
        bm.free()
        merged.from_mesh(scratch)
        scratches.append(scratch)
    merged.to_mesh(mesh)
    merged.free()
    mesh.update()
    for scratch in scratches:
        bpy.data.meshes.remove(scratch)

    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)

    # Probe, not assumption. GATE_SUBMESH_EMPTY_DECLARED_SLOT fired for three iterations
    # and its cause was guessed twice before it was measured. Counting polygons per
    # material index HERE makes an erased submesh a recorded fact at the stage that erases
    # it, instead of a validator failure hundreds of lines later with no provenance.
    census = {}
    for polygon in mesh.polygons:
        census[polygon.material_index] = census.get(polygon.material_index, 0) + 1
    reports["submeshCensus"] = census
    missing = [slot for slot, _role in MATERIAL_ROLES if census.get(slot, 0) == 0]
    blackbox.record("submesh_census", vertex_count=len(mesh.vertices),
                    triangle_count=mesh_ops.triangle_count(mesh),
                    warning="" if not missing else
                    "declared material slots carrying no faces: {m}".format(m=missing),
                    failure_code="" if not missing else "SUBMESH_SLOT_EMPTY")

    blackbox.record("assembly", family=law.Family.SMALL_PROP.value,
                    vertex_count=len(mesh.vertices),
                    triangle_count=mesh_ops.triangle_count(mesh),
                    warning="" if parts else "no parts were built")
    return obj, parts, reports


# ---------------------------------------------------------------------------
# Stage 5: UVs and material IDs
# ---------------------------------------------------------------------------

def unwrap(obj: bpy.types.Object, spec: HandToolSpec, *, island_margin: float,
           blackbox: Optional[BlackBox] = None) -> dict:
    """Conformal unwrap plus the measured stretch statistics.

    3dmodel.md section 6 lists box/projection unwrap as legal "for industrial panels
    only when each face has calibrated texel density"; this asset is mostly revolved
    forms, so it takes the conformal route instead, which the same section permits
    unconditionally.

    The seam angle is ``law.smooth_angle_for(HARD_SURFACE)`` rather than a hand-picked
    number: an edge hard enough to break shading is exactly an edge that should carry a
    UV seam, and reusing the one constant keeps the two decisions from drifting apart.
    """
    mesh_ops._make_sole_active(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(
        angle_limit=math.radians(law.smooth_angle_for(law.SurfaceClass.HARD_SURFACE)),
        island_margin=island_margin,
        correct_aspect=True,
        scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")

    stretch = mesh_ops.uv_stretch_stats(obj)
    layer = obj.data.uv_layers.active
    limit = law.uv_stretch_limit_for(law.SurfaceClass.HARD_SURFACE, hero=True)
    report = {
        "uvRoute": "smart_project conformal, angle_limit={a:.0f}deg, margin={m}".format(
            a=law.smooth_angle_for(law.SurfaceClass.HARD_SURFACE), m=island_margin),
        "uvLayer": layer.name if layer else None,
        "stretchWorst": round(stretch["worst"], 4),
        "stretchP95Area": round(stretch["p95"], 4),
        "stretchMean": round(stretch["mean"], 4),
        "stretchTriangles": stretch["triangles"],
        "perTriangleLimit": limit,
        "areaFractionAllowedOverLimit": law.UV_STRETCH_AREA_FRACTION_MAX,
    }
    if blackbox is not None:
        blackbox.record("unwrap", vertex_count=len(obj.data.vertices),
                        warning="" if layer else "no active UV layer after unwrap")
    return report


# ---------------------------------------------------------------------------
# Stage 6: bakes and vertex colours
# ---------------------------------------------------------------------------

def _vertex_material_weights(obj: bpy.types.Object, spec: HandToolSpec) -> list:
    """Per-vertex wear coefficient, area-weighted across incident faces.

    Face material index is the only place surface identity lives, so the coefficient is
    folded onto vertices rather than looked up per vertex. Area weighting matters at
    material borders: a vertex shared between a large painted panel and one tiny bare
    bevel strip should wear mostly like paint.
    """
    mesh = obj.data
    coefficients = {}
    for slot, field_name in WEAR_COEFFICIENT_BY_SLOT_FIELD.items():
        coefficients[slot] = float(getattr(spec, field_name))
    default = spec.wear_coefficient_structural

    totals = [0.0] * len(mesh.vertices)
    weights = [0.0] * len(mesh.vertices)
    mesh.calc_loop_triangles()
    for tri in mesh.loop_triangles:
        p0 = mesh.vertices[tri.vertices[0]].co
        p1 = mesh.vertices[tri.vertices[1]].co
        p2 = mesh.vertices[tri.vertices[2]].co
        area = (p1 - p0).cross(p2 - p0).length * 0.5
        coefficient = coefficients.get(tri.material_index, default)
        for vertex_index in tri.vertices:
            totals[vertex_index] += coefficient * area
            weights[vertex_index] += area
    return [totals[i] / weights[i] if weights[i] > 0.0 else default
            for i in range(len(mesh.vertices))]


def author_channels(obj: bpy.types.Object, spec: HandToolSpec,
                    blackbox: BlackBox) -> tuple:
    """Bake AO, then compose the hard-surface R/G/B/A contract.

    Order is load-bearing: ``bpy.ops.object.bake(target="VERTEX_COLORS")`` overwrites all
    four channels, so composing first and baking second would erase the wear and
    emission masks with no error anywhere.

    The R and G formulas are 3DMODEL_HARD_SURFACE_MODULES.md section 5 verbatim:

        cavity(face) = occlusionSampleCountBlocked / occlusionSampleCount
        wear  = convexity * exposureMask * materialWearCoefficient
        grime = cavity * downwardBias * wetnessRoute

    ``exposureMask`` and ``cavity`` are not invented here -- the same section defines
    cavity as blocked-sample fraction, which is ``1 - ao``, so exposure is the baked AO
    itself. Using the ray-traced bake for both terms is why this lane exists at all: a
    C# generator can only approximate occlusion from curvature.
    """
    # AO ray length is derived from the asset, not hardcoded. Bounding rays to a quarter
    # of the longest extent keeps occlusion LOCAL -- the trap that crushed one coral's AO
    # mean to 0.078 was an unbounded distance turning cavity contrast into a global sky
    # term. On a 0.26 m tool the cavities that matter (seam groove, bezel well, rib
    # valleys, flutes) are all millimetres deep.
    extent = mesh_ops.longest_extent(obj)
    ao_distance = max(0.01, extent * 0.25)
    ao_result = vertexcolor.bake_ambient_occlusion(
        obj, samples=int(round(24 + 40 * law.saturate(spec.quality))),
        distance=ao_distance, blackbox=blackbox)
    ao_values = vertexcolor.consume_baked_ao(obj)

    mesh = obj.data
    count = len(mesh.vertices)
    have_ao = len(ao_values) == count
    exposure = ao_values if have_ao else [1.0] * count

    convexity = vertexcolor.curvature_edge_wear(obj)
    material_wear = _vertex_material_weights(obj, spec)

    edge_wear = []
    oxidation = []
    for index in range(count):
        # R: convexity * exposure * material coefficient.
        edge_wear.append(law.saturate(
            convexity[index] * exposure[index] * material_wear[index]))

        # G: cavity * downwardBias * wetnessRoute. A downward-facing or shadowed
        # surface is where salt and biofilm settle; the floor keeps upward faces from
        # reading as surgically clean, which TASTE.md rejects as "clean synthetic
        # materials with no age, pressure, wear, or function".
        cavity = 1.0 - exposure[index]
        normal_z = mesh.vertices[index].normal.normalized().z
        downward = spec.grime_downward_bias_floor + \
            (1.0 - spec.grime_downward_bias_floor) * law.saturate(0.5 - 0.5 * normal_z)
        oxidation.append(law.saturate(cavity * downward * spec.wetness_route))

    # A: emission / decal eligibility. The instrument glass is the emissive surface; a
    # patch on the left flank is flagged as decal-eligible for the warning label the
    # props bible requires be an atlas decal rather than a unique material.
    display_vertices = set()
    casing_vertices = set()
    for tri in mesh.loop_triangles:
        if tri.material_index == SLOT_DISPLAY:
            display_vertices.update(tri.vertices)
        elif tri.material_index == SLOT_CASING:
            casing_vertices.update(tri.vertices)
    # A = decal ELIGIBILITY, expressed per PANEL, not as a bounding-box patch.
    #
    # Two measured failures got this here. First, selecting by bounding-box fraction alone
    # flagged the guard rail's left tube (x = -16.1 mm against a -14.6 mm threshold) as
    # label-eligible, and the channel-A tile showed the rail tracing bright. Then the
    # material-aware version over-corrected to nothing: the casing is a 4-station loft with
    # no rings between y = -144 mm and y = +74 mm, so a y-band predicate selected ZERO
    # vertices and the rendered A channel came back min = max = mean = 0.000. A uniform
    # channel is the silent failure this pipeline produces instead of an error.
    #
    # A per-vertex mask cannot express a small patch on a panel with no interior vertices,
    # and it does not need to: 3DMODEL_EQUIPMENT_PROPS.md section 3 defines A as "emissive/
    # display/decal eligibility", and section 5 requires labels to come from "atlas/decal
    # slots, not unique material clones". Eligibility is therefore a property of the
    # SURFACE ROLE -- painted casing panels accept a decal, bare machined metal, rubber and
    # glass do not -- and the decal's placement is the atlas's job at bind time, not this
    # channel's. Density-independent, and it says something true.
    emission = []
    for index in range(count):
        if index in display_vertices:
            emission.append(1.0)          # instrument glass: the emissive surface
        elif index in casing_vertices:
            emission.append(0.5)          # painted panel: decal-eligible
        else:
            emission.append(0.0)          # machined metal, rubber, rail: not eligible

    report = vertexcolor.write_hard_surface_channels(
        obj, edge_wear=edge_wear, oxidation=oxidation,
        ao=ao_values if have_ao else None,
        emission_mask=emission, blackbox=blackbox)
    report["aoDistanceM"] = round(ao_distance, 5)
    report["alphaMeaning"] = "emission_and_decal_eligibility"
    vertexcolor.remove_scratch_attributes(mesh)
    report["storedChannels"] = vertexcolor.channel_stats(obj)
    return report, ao_result


# ---------------------------------------------------------------------------
# Orchestration
# ---------------------------------------------------------------------------

def generate(spec: HandToolSpec, *, name: Optional[str] = None,
             render_preview: bool = True, preview_dir: str = "",
             preview_resolution: int = 640,
             export_package: bool = True) -> HandToolResult:
    """Full package: geometry, UVs, bakes, channels, LODs, collider, validation, proof."""
    asset_name = name or "Tool_SeafloorDrill_{s:04d}".format(s=spec.seed % 10000)
    blackbox = BlackBox("PropHandTool", "s{s}q{q:02d}".format(
        s=spec.seed, q=int(round(law.saturate(spec.quality) * 100))))

    bpy.ops.wm.read_factory_settings(use_empty=True)
    collection = bpy.data.collections.new("H8_HandTool")
    bpy.context.scene.collection.children.link(collection)

    try:
        obj, parts, bevel_reports = build_tool_object(
            spec, asset_name, collection, blackbox)

        materials = _assign_materials(obj)
        shading = mesh_ops.apply_shading_basis(
            obj,
            smooth_angle_deg=law.smooth_angle_for(law.SurfaceClass.HARD_SURFACE),
            blackbox=blackbox)

        # Reduce to the LOD0 ceiling BEFORE unwrapping and baking. Decimating afterwards
        # discards the UV layout and the vertex colours the next two stages author.
        #
        # Only called when actually over budget, and the skip is RECORDED.
        # Iteration 2 measured why: reduce_to_budget runs a bare Decimate/COLLAPSE with no
        # material-border split, and it collapsed the entire 57-face instrument-glass
        # submesh out of existence. GATE_SUBMESH_EMPTY_DECLARED_SLOT then fired on all
        # three LODs -- a declared material slot with no triangles. The source density is
        # now tuned to land under the ceiling so this pass is a no-op on the default spec;
        # if a caller pushes quality or size past the ceiling it still runs, because an
        # over-budget LOD0 is the worse failure.
        budget_lod0 = law.LOD_BUDGETS[law.Family.SMALL_PROP].lod0
        source_tris = mesh_ops.triangle_count(obj.data)
        if source_tris > budget_lod0:
            mesh_ops.reduce_to_budget(obj, family=law.Family.SMALL_PROP, lod_index=0,
                                      blackbox=blackbox)
        else:
            blackbox.record("reduce_to_budget_skipped",
                            family=law.Family.SMALL_PROP.value,
                            triangle_count=source_tris,
                            warning="{t} tris already under the {b} ceiling; skipped so "
                                    "the decimator cannot erase a material slot".format(
                                        t=source_tris, b=budget_lod0))

        uv_report = unwrap(obj, spec, island_margin=0.008, blackbox=blackbox)
        channel_report, ao_result = author_channels(obj, spec, blackbox)

        def reunwrap(target: bpy.types.Object, lod_index: int) -> None:
            # Coarser margin at coarser LODs: fewer, chunkier islands survive a collapse
            # better than many thin ones.
            unwrap(target, spec, island_margin=0.008 + 0.004 * lod_index)

        lods = mesh_ops.build_lod_chain(
            obj, family=law.Family.SMALL_PROP, name=asset_name,
            quality_weight=spec.quality, reunwrap=reunwrap, blackbox=blackbox)

        # Collision proxy is hulled from the COARSEST visual level, not from LOD0.
        #
        # Two measured reasons, both from iteration 2. Hulling LOD0 gave 184 triangles:
        # inside law.py's COLLIDER_CONVEX_TRI_MAX of 200, but well over the 100-triangle
        # ceiling 3DMODEL_EQUIPMENT_PROPS.md section 6 sets for a handheld convex hull, and
        # law.py carries no per-family row to catch that. It also failed
        # GATE_COLLIDER_NOT_CONVEX by 0.1 mm, because make_convex_collider decimates the
        # hull and then re-hulls, and a decimated hull of a dense source keeps producing
        # near-planar vertices at the tolerance boundary.
        #
        # A convex hull is determined by extreme points alone, so hulling LOD2 yields the
        # same shape from far fewer candidates -- fewer hull triangles, no decimation pass,
        # and therefore no decimation-induced concavity. Section 6 wants primitives for a
        # prop anyway, so the coarser proxy is closer to the bible's intent, not further.
        collider_source = lods[-1].obj
        collider = mesh_ops.make_convex_collider(
            collider_source, family=law.Family.SMALL_PROP, name=asset_name,
            blackbox=blackbox)

        # Purge decimation slivers from every level before anything measures them.
        # build_lod_chain owns the Decimate passes and offers no cleanup hook, so this is
        # the first point a caller can reach LOD1/LOD2. Counts are recorded per level, and
        # LodLevel.triangles is refreshed because a stale count would make the budget and
        # monotonicity reports describe geometry that no longer exists.
        lod_purged = {}
        for level in lods:
            removed = purge_object_degenerates(level.obj)
            level.triangles = mesh_ops.triangle_count(level.obj.data)
            lod_purged["LOD{i}".format(i=level.index)] = removed
            blackbox.record(
                "lod{i}_sliver_purge".format(i=level.index),
                triangle_count=level.triangles,
                vertex_count=len(level.obj.data.vertices),
                warning="purged {r} degenerate faces".format(r=removed) if removed else "")

        # Topology census per level. Called unconditionally, not only on a budget miss:
        # "topology_report has no callers" is a recorded gap in Tools/Blender/README.md,
        # and a census that is only taken after a failure cannot show a REGRESSION.
        topology = {}
        for level in lods:
            report = mesh_ops.topology_report(level.obj)
            topology["LOD{i}".format(i=level.index)] = {
                "triangles": report.triangles,
                "components": report.components,
                "boundaryEdges": report.boundary_edges,
                "nonmanifoldEdges": report.nonmanifold_edges,
                "irreducibleFloor": report.irreducible_floor,
                "explain": report.explain(level.budget),
            }

        # Stage 11: validation before save. Failure aborts.
        mesh_reports = [
            validate.validate_mesh(
                level.obj.data, family=law.Family.SMALL_PROP, lod_index=level.index,
                surface_class=law.SurfaceClass.HARD_SURFACE, blackbox=blackbox,
                hero=True)
            for level in lods
        ]
        chain_failures = validate.validate_lod_chain(
            mesh_reports, family=law.Family.SMALL_PROP, blackbox=blackbox)
        collider_failures = []
        if collider.obj is not None:
            collider_failures = validate.validate_collider(
                collider.obj.data, family=law.Family.SMALL_PROP, blackbox=blackbox,
                lod0_mesh=lods[0].obj.data,
                visual_meshes=[level.obj.data for level in lods[1:]])

        result = HandToolResult(
            name=asset_name, lods=lods, collider=collider, parts=parts,
            bevel_reports=bevel_reports, shading=shading, ao_report=ao_result,
            channel_report=channel_report, uv_report=uv_report, topology=topology,
            lod_purged=lod_purged,
            mesh_reports=mesh_reports, chain_failures=chain_failures,
            collider_failures=collider_failures,
            orientation={
                "boreAxisBlender": tuple(BORE_AXIS),
                "boreAxisUnity": "(0, 0, 1) forward via blender_to_unity",
                "gripDownBlender": "(0, 0, -1) -> Unity (0, -1, 0)",
                "materialSlots": materials,
                "functionCategory": spec.function,
                "verb": spec.verb,
            })

        if render_preview:
            views = ("three_quarter", "side", "front", "low")
            studio = preview.render_contact_sheet(lods[0].obj, preview.PreviewSpec(
                name=asset_name, output_dir=preview_dir,
                resolution=preview_resolution, samples=24,
                surface_class=law.SurfaceClass.HARD_SURFACE, mode="studio",
                views=views))
            flat = preview.render_contact_sheet(lods[0].obj, preview.PreviewSpec(
                name=asset_name, output_dir=preview_dir,
                resolution=preview_resolution, samples=12,
                surface_class=law.SurfaceClass.HARD_SURFACE, mode="flat",
                views=views))
            channels = preview.render_channel_sheet(lods[0].obj, preview.PreviewSpec(
                name=asset_name, output_dir=preview_dir,
                resolution=preview_resolution, samples=12,
                surface_class=law.SurfaceClass.HARD_SURFACE))
            result.preview_paths = (studio.sheet_path, flat.sheet_path,
                                    channels.sheet_path)
            result.channel_stats = tuple(
                preview.measure_channel_png(path) for path in channels.tile_paths)

        # STAGE: package. This generator had NO export call at all - only a comment
        # mentioning export_unity - so the one hero prop the first-20 route actually
        # blocks on produced contact sheets and no mesh, and died with the Blender
        # process. Coral had the identical defect and this is the same fix.
        #
        # It matters more here than for a decorative asset:
        # ResourceNodeTemplate_CopperVein.asset:20 demands requiredToolClass 2,
        # ResourceNodeTemplate.cs:36 makes that Drill, and of the 13 held tool prefabs
        # Tool_SeafloorDrill_Held is the ONLY one whose MeshFilters are the Unity
        # built-in Cube - three times, one instance named Detail_RibbedTrimBand.
        # Geometry that never leaves Blender cannot replace it.
        #
        # PACKAGE = FBX plus a SIBLING manifest, inside Assets, and nothing else. The
        # sibling requirement is not stylistic:
        # HectonFBXPostprocessor.TryResolveForgeManifestPath (:702-736) derives the
        # manifest path from the mesh path, and without it the carve-out at :401-429
        # never fires, so Unity re-derives normals from a single angle and discards the
        # weighted split basis three separate bevel bands were authored to produce.
        # PROOF = the render sheets, which stay in preview_dir. They must NOT land in
        # the package directory: rock sent 27 PNGs into the asset tree that way and
        # Unity would import every one as a texture with its own meta and GUID.
        if export_package:
            for level in lods:
                result.mesh_reports.append(validate.validate_mesh(
                    level.obj.data, family=law.Family.SMALL_PROP,
                    lod_index=level.index,
                    surface_class=law.SurfaceClass.HARD_SURFACE,
                    blackbox=blackbox, hero=(level.index == 0)))

            identity = law.GeneratorIdentity(
                generator="prop_handtool", generator_version=GENERATOR_VERSION,
                seed=spec.seed, quality_weight=spec.quality,
                family=law.Family.SMALL_PROP,
                scale_meters=spec.body_length_m,
                camera_distance_class="near",
                platform_lane="windows_copper_wire",
                source_references=("3DMODEL_EQUIPMENT_PROPS.md", "3dmodel.md",
                                   "tools.md", "PROCEDURAL_ASSET_PIPELINE.md"))

            package_dir = os.path.join(
                law.project_root(),
                *law.forge_package_dir(law.Family.SMALL_PROP).split("/"))
            os.makedirs(package_dir, exist_ok=True)
            fbx_path = os.path.join(package_dir, "MESH_{f}_{n}.fbx".format(
                f=law.Family.SMALL_PROP.value, n=asset_name))

            # None, not the ColliderResult, when there is no collider object:
            # export_lod_group handles a missing collider correctly but _as_object
            # RAISES on a ColliderResult whose .obj is None instead of reading it as
            # absent.
            collider_arg = (collider
                            if getattr(collider, "obj", None) is not None else None)
            export_result = export_unity.export_lod_group(
                lods, collider_arg, fbx_path, identity=identity,
                blackbox=blackbox)
            result.fbx_path = getattr(export_result, "path", fbx_path)

            result.manifest_path = export_unity.write_manifest(
                os.path.join(package_dir, export_unity.manifest_filename(
                    law.Family.SMALL_PROP, asset_name)),
                identity, result.mesh_reports,
                # No MAT_* or TX_* is authored here: the wear lives in the four
                # vertex-colour channels and the base tint in the material, so naming
                # texture files that do not exist would be a false reference.
                [], [],
                [collider] if getattr(collider, "obj", None) is not None else [],
                list(result.preview_paths), export_result=export_result,
                uv_summary=result.uv_report,
                alpha_meaning="emission_decal_mask",
                extra={
                    "toolClass": "Drill (ResourceNodeTemplate.cs:36 maps 2 -> Drill)",
                    "routeBlocker":
                        "Tool_SeafloorDrill_Held.prefab carries the Unity built-in "
                        "Cube (fileID 10202) in all three MeshFilters, and the binder "
                        "declines the drill by name at "
                        "ProductFacePrefabBinderAuthoring.cs:729-734. This package is "
                        "the geometry that replaces it.",
                    "parts": [getattr(record, "name", str(record))
                              for record in result.parts],
                    "bevelBands": result.bevel_reports,
                    "topology": result.topology,
                    "unityPrefabAssembly":
                        "NOT PERFORMED. .prefab/.mat/.asset creation is Unity-only "
                        "per AGENTS.md Evidence Law; this generator emits mesh plus "
                        "manifest for a Unity-side assembler.",
                })

        # Report BEFORE the gate, then gate.
        #
        # 3dmodel.md section 10 says validation failure aborts the SAVE. It does not say
        # the measurements disappear -- and on the first run they did: assert_or_abort
        # raised before anything was printed, so a failing run reported gate names with
        # no triangle counts, no channel statistics and no bevel widths to diagnose them
        # with. The numbers are exactly what a failing run needs most.
        _print_report(result, spec)
        validate.assert_or_abort(
            [mesh_reports, chain_failures, collider_failures],
            blackbox=blackbox,
            reason="hand tool package gate before save")
        return result
    except GenerationAborted:
        raise
    except Exception as error:
        blackbox.note_invalid("generate", "HANDTOOL_GENERATOR_EXCEPTION", str(error))
        dump = blackbox.dump("hand tool generator raised: " + str(error))
        raise GenerationAborted(
            "hand tool generation failed: " + str(error), dump_path=dump) from error


def _parse_args(argv: list) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="HECTON-8 hand-held hard-surface tool generator")
    parser.add_argument("--seed", type=int, default=2611)
    parser.add_argument("--quality", type=float, default=1.0,
                        help="GlobalQualityWeight, continuous 0..1")
    parser.add_argument("--variants", type=int, default=1)
    parser.add_argument("--length", type=float, default=0.255,
                        help="casing length in metres")
    parser.add_argument("--name", type=str, default="")
    parser.add_argument("--out", type=str, default="")
    parser.add_argument("--preview-resolution", type=int, default=640)
    parser.add_argument("--no-preview", dest="preview", action="store_false")
    # Export is ON by default. A prop generator whose default run produces no
    # asset is the defect this stage was added to fix, so the default must not be
    # the cheap path. The flag exists only to keep a silhouette loop fast.
    parser.add_argument("--no-export", dest="export", action="store_false",
                        help="skip FBX + manifest; fast iteration only")
    parser.set_defaults(preview=True, export=True)
    return parser.parse_args(argv)


def _print_report(result: HandToolResult, spec: HandToolSpec) -> None:
    print("=" * 78)
    print("HANDTOOL {n}  seed={s} quality={q:.2f} function={f} verb={v}".format(
        n=result.name, s=spec.seed, q=spec.quality,
        f=spec.function, v=spec.verb))

    print("  PARTS {n} functional parts".format(n=len(result.parts)))
    by_slot = {}
    for part in result.parts:
        by_slot.setdefault(part.material_slot, []).append(part)
    for slot, role in MATERIAL_ROLES:
        members = by_slot.get(slot, [])
        print("    slot{s} {r:<18} parts={p:<3} faces={f}".format(
            s=slot, r=role, p=len(members),
            f=sum(m.faces for m in members)))
        if not members:
            print("    slot{s} EMPTY -> submesh_empty_declared_slot will fire".format(
                s=slot))

    for band, report in result.bevel_reports.items():
        if "edgesBeveled" not in report:
            print("  WELD  {b:<11} {s}".format(b=band, s=report.get("weld")))
            continue
        print("  BEVEL {b:<11} beveled={e}/{c} width={w:.5f}m segments={s} "
              "clamped={cl}".format(
                  b=band, e=report["edgesBeveled"], c=report["edgesConsidered"],
                  w=report["widthM"], s=report["segments"],
                  cl=report["clampedByShortestEdgeRule"]))
    print("  BEVEL law range {lo}..{hi} m, threshold {t} deg".format(
        lo=law.BEVEL_RANGES[law.Family.SMALL_PROP].min_m,
        hi=law.BEVEL_RANGES[law.Family.SMALL_PROP].max_m,
        t=law.BEVEL_ANGLE_THRESHOLD_DEG))

    shading = result.shading
    if shading is not None:
        print("  SHADING smoothPolygons={s} sharpEdges={h} weightedApplied={w}".format(
            s=shading.smooth_polygons, h=shading.sharp_edges,
            w=shading.weighted_applied))

    for level in result.lods:
        key = "LOD{i}".format(i=level.index)
        top = result.topology.get(key, {})
        print("  {k} tris={t}/{b} within={w} components={c} boundary={be} "
              "nonmanifold={nm} floor={f}".format(
                  k=key, t=level.triangles, b=level.budget, w=level.within_budget,
                  c=top.get("components"), be=top.get("boundaryEdges"),
                  nm=top.get("nonmanifoldEdges"), f=top.get("irreducibleFloor")))
        if top.get("explain"):
            print("    CAUSE " + top["explain"])
        purged = result.lod_purged.get(key)
        if purged:
            print("    PURGED {p} decimation slivers at this level".format(p=purged))

    collider = result.collider
    if collider is not None:
        print("  COLLIDER kind={k} tris={t} lawMax={m} within={w}".format(
            k=collider.kind, t=collider.triangles,
            m=law.COLLIDER_CONVEX_TRI_MAX, w=collider.within_budget))
        # The family bible is STRICTER than law.py here and law.py has no per-family
        # row, so the tighter number is reported explicitly rather than assumed met.
        print("    3DMODEL_EQUIPMENT_PROPS.md section 6 caps a handheld convex hull at "
              "100 tris: {v}".format(
                  v="within" if collider.triangles <= 100 else
                  "OVER by {n}".format(n=collider.triangles - 100)))
        if collider.reason:
            print("    " + collider.reason)

    uv = result.uv_report
    print("  UV {r}".format(r=uv.get("uvRoute")))
    print("     worst={w} p95Area={p} mean={m} tris={t} perTriangleLimit={l} "
          "areaFractionAllowed={a}".format(
              w=uv.get("stretchWorst"), p=uv.get("stretchP95Area"),
              m=uv.get("stretchMean"), t=uv.get("stretchTriangles"),
              l=uv.get("perTriangleLimit"),
              a=uv.get("areaFractionAllowedOverLimit")))

    ao = result.ao_report
    if ao is not None:
        print("  AO baked={b} distance={d}m samples={s} min={lo:.4f} max={hi:.4f} "
              "mean={m:.4f} contrast={c}".format(
                  b=ao.baked, d=result.channel_report.get("aoDistanceM"),
                  s=ao.samples, lo=ao.min_value, hi=ao.max_value,
                  m=ao.mean_value, c=ao.has_contrast))
        if not ao.baked:
            print("  AO FAILURE: " + ao.reason)

    stored = result.channel_report.get("storedChannels", {})
    if stored.get("present"):
        print("  STORED contract={c}".format(
            c=result.channel_report.get("contract")))
        print("  STORED areaWeightedMean={m}".format(
            m=stored.get("areaWeightedMean")))
        print("  STORED min={lo} max={hi}".format(
            lo=stored.get("min"), hi=stored.get("max")))
        flat = []
        for index in range(4):
            lo = stored.get("min", [0, 0, 0, 0])[index]
            hi = stored.get("max", [0, 0, 0, 0])[index]
            if hi - lo < 1e-4:
                flat.append(law.HARD_SURFACE_VCOL[index])
        print("  STORED flatChannels={f}".format(f=flat or "none"))

    for stats in result.channel_stats:
        print("  CHAN {c:<44} min={lo:.3f} max={hi:.3f} mean={m:.3f} "
              "cover={cv:.3f} gradient={g} visible={v}".format(
                  c=stats.channel, lo=stats.min_value, hi=stats.max_value,
                  m=stats.mean_value, cv=stats.coverage_fraction,
                  g=stats.has_gradient, v=stats.subject_visible))

    total_failures = 0
    for report in result.mesh_reports:
        names = sorted({f.gate for f in report.failures})
        total_failures += len(report.failures)
        print("  VALIDATE {n} lod={l} passed={p} failedGates={g}".format(
            n=report.name, l=report.lod_index, p=report.passed,
            g=names or "none"))
        for failure in report.failures:
            print("    FAIL {g}: {d}".format(g=failure.gate, d=failure.detail))
    print("  VALIDATE lodChain failures={f}".format(
        f=sorted({f.gate for f in result.chain_failures}) or "none"))
    for failure in result.chain_failures:
        print("    FAIL {g}: {d}".format(g=failure.gate, d=failure.detail))
    print("  VALIDATE collider failures={f}".format(
        f=sorted({f.gate for f in result.collider_failures}) or "none"))
    for failure in result.collider_failures:
        print("    FAIL {g}: {d}".format(g=failure.gate, d=failure.detail))
    print("  VALIDATE totalMeshGateFailures={n}".format(n=total_failures))

    print("  ORIENT {o}".format(o=result.orientation))
    for path in result.preview_paths:
        print("  PREVIEW " + path)


def main() -> None:
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = _parse_args(argv)

    for variant in range(max(1, args.variants)):
        spec = HandToolSpec(
            seed=args.seed + variant * 7919,
            quality=args.quality,
            body_length_m=args.length,
        )
        name = args.name or None
        if name and args.variants > 1:
            name = "{n}_{v}".format(n=name, v=variant)
        result = generate(spec, name=name, render_preview=args.preview,
                          preview_dir=args.out,
                          preview_resolution=args.preview_resolution,
                          export_package=args.export)
        print("  FBX      " + (result.fbx_path or "NONE - no mesh artifact written"))
        print("  MANIFEST " + (result.manifest_path or "NONE"))
    print("HANDTOOL_GENERATOR_DONE")


if __name__ == "__main__":
    main()
