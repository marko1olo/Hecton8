"""Kelp / seaweed generator -- HECTON-8 flora family.

Specification: ``3DMODEL_FLORA_CORAL.md`` section 3 "Kelp And Seaweed". Every
structure it lists is mandatory and is built here:

  holdfast/root cluster     splayed haptera fingers welded under a lumpy boss,
                            following the ground plane -- not a vertical ribbon
  stipe with taper+ribbing  swept tube whose cross-section changes along its
                            length: taper, rotating ellipse, angular ribs,
                            pneumatocyst swellings. A constant-radius tube is a
                            stated rejection.
  blade sheets with rim     each blade is a CLOSED thin shell: the cross-section
                            is a flat lens, so the narrow ends of the lens *are*
                            the edge rim. Section 3: "Blade surfaces must not be
                            zero-thickness if seen from both sides at close
                            range. Use a thin shell with edge rim."
  secondary breakup         serration, margin tears, longitudinal folds,
                            pneumatocyst blisters and healed scars, all at LOD0
  anchor socket             object origin sits at the holdfast base, and the sway
                            field is measured from it

Root law: ``3dmodel.md`` sections 3, 5, 6, 7, 9, 10, 12.
Package/stage law: ``PROCEDURAL_ASSET_PIPELINE.md`` "Generation Order" and
"Required Output Package".

Every threshold comes from ``h8forge.law``. Nothing numeric that a bible fixes is
redefined here -- ``generators/__init__.py`` forbids it and a local copy is drift.

Determinism: one ``numpy.random.default_rng`` per named stream, keyed by
``[seed, stream_id]``. No wall clock, no unseeded ``random``, no dependence on
dict order. ``PROCEDURAL_ASSET_PIPELINE.md``: "If artist variation is needed,
variation is a named seed, not hidden chance."

Run headless::

    blender.exe -b --factory-startup -P Tools/Blender/generators/kelp.py -- \
        --seed 4021 --quality 1.0

Blender 4.5 LTS note: ``Mesh.use_auto_smooth`` was removed in 4.1. Angle shading
goes through ``mesh_ops.apply_shading_basis``, which uses the current operator.
"""

from __future__ import annotations

import argparse
import json
import math
import os
import sys
import time

import bmesh
import bpy
import numpy as np
from mathutils import Vector, kdtree, noise

# The package lives beside this file's parent, which is not on sys.path when
# Blender runs a script by path.
_TOOLS_BLENDER = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if _TOOLS_BLENDER not in sys.path:
    sys.path.insert(0, _TOOLS_BLENDER)

from h8forge import law, mesh_ops, preview, validate, vertexcolor  # noqa: E402
from h8forge.blackbox import BlackBox, GenerationAborted  # noqa: E402

GENERATOR_NAME = "kelp.py"
GENERATOR_VERSION = "1.0.0"

# 3DMODEL_FLORA_CORAL.md section 5: "Atlas groups must pack by biome and material
# family: kelp, brittle coral, massive coral, plate coral, root/biofilm."
ATLAS_FAMILY = "kelp"
# 2048, for two measured reasons. (a) Texel density: a 1024 page gave 313 px/m, short
# of the 512 px/m law.TEXEL_DENSITY_HERO_FLORA target for hero harvestable flora, and
# kelp is harvestable. (b) The zero-area UV gate compares UV area against
# law.DEGENERATE_TRIANGLE_AREA_EPS in ABSOLUTE UV units, so UV density decides whether
# a physically fine 1 mm blade-tip triangle reads as degenerate: at 1024 the blade tip
# cap fell to ~6.6e-08 against the 1e-07 epsilon. Doubling the page quadruples UV area.
ATLAS_SIZE = 2048

# Material slots, 3dmodel.md section 6. Slot 3 is declared under that section's
# "emissive/bioluminescent/DETAILS only when needed" clause and carries the
# pneumatocyst bladder pigment -- the amber-orange accent the mandatory reference
# ``forest_kelp.webp`` uses as its colour focal point. It is NOT emissive: kelp is
# photic tissue, so vertex-colour G stays 0 everywhere. Every declared slot must
# carry triangles or validation fails, so the bladder regions are real geometry.
SLOT_ROLES = ("tissue", "basal_collar_scar", "holdfast", "bladder")

# Vertex class tags, carried per-vertex so the sway/harvest fields can tell
# rigid root tissue from flexible frond tissue after welding has renumbered
# everything.
CLS_BOSS = 0.0
CLS_FINGER = 1.0
CLS_STIPE = 2.0
CLS_BLADE = 3.0

GEO_LAYER = "h8_geo"
CLS_LAYER = "h8_cls"
PART_LAYER = "h8_part"

# Named deterministic streams. Adding a stage must not reshuffle earlier ones,
# which is what a single shared generator would do.
STREAM_FORM = 11
STREAM_HOLDFAST = 23
STREAM_STIPE = 37
STREAM_BLADES = 53
STREAM_DETAIL = 71


# ---------------------------------------------------------------------------
# Deterministic helpers
# ---------------------------------------------------------------------------

def _rng(seed: int, stream: int) -> np.random.Generator:
    """Independent named stream. ``default_rng`` accepts a sequence as entropy."""
    return np.random.default_rng([int(seed), int(stream)])


def _lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def _qi(lo: int, hi: int, quality: float) -> int:
    """Continuous count sampled from a quality curve, rounded half-up.

    ``AGENTS.md``: "[RULE] Binary quality switches are rejected. Every scalable
    algorithm must consume continuous GlobalQualityWeight from 0.0 minimum
    survival to 1.0 visual overkill." Python's ``round`` is banker's rounding, so
    0.5 steps would land unevenly; ``int(x + 0.5)`` keeps the curve monotonic.
    """
    return int(lo + (hi - lo) * law.saturate(quality) + 0.5)


def _smoothstep(edge0: float, edge1: float, x: float) -> float:
    if edge1 <= edge0:
        return 0.0 if x < edge0 else 1.0
    t = law.saturate((x - edge0) / (edge1 - edge0))
    return t * t * (3.0 - 2.0 * t)


def _tri_wave(x: float) -> float:
    """Triangle wave in 0..1 with period 1. Serration teeth, not a sine blob."""
    f = x - math.floor(x)
    return 2.0 * f if f < 0.5 else 2.0 * (1.0 - f)


def _fine_noise(point: Vector, offset: Vector, frequency: float) -> float:
    """Weak isotropic octave in -1..1.

    Kept weak on purpose. A single isotropic octave used as the dominant term
    reads as cauliflower rather than grown tissue, so the dominant terms in this
    generator are anisotropic bands ALONG the structure (ribs, folds, growth
    rings) and this is the 0.12-weight garnish on top.

    ``mathutils.noise`` is a deterministic Perlin field with no internal state;
    the seed enters as a domain offset, which is reproducible across runs and
    across machines.
    """
    return noise.noise((point * frequency) + offset)


def _parallel_frames(points):
    """Tangent/normal/binormal per sample, transported to avoid twist flips.

    Recomputing the normal from a fixed world up-vector snaps 180 degrees when the
    curve passes vertical, which tears the swept surface. Parallel transport keeps
    the frame continuous, which is what ``3DMODEL_FLORA_CORAL.md`` section 4 needs
    for "radial normal blended with curve tangent frame".
    """
    count = len(points)
    tangents = []
    for i in range(count):
        if i == 0:
            delta = points[1] - points[0]
        elif i == count - 1:
            delta = points[-1] - points[-2]
        else:
            delta = points[i + 1] - points[i - 1]
        if delta.length <= 1e-9:
            delta = Vector((0.0, 0.0, 1.0))
        tangents.append(delta.normalized())

    reference = Vector((1.0, 0.0, 0.0))
    if abs(tangents[0].dot(reference)) > 0.9:
        reference = Vector((0.0, 1.0, 0.0))
    first = (reference - tangents[0] * reference.dot(tangents[0]))
    if first.length <= 1e-9:
        first = Vector((0.0, 1.0, 0.0))
    normals = [first.normalized()]
    binormals = [tangents[0].cross(normals[0]).normalized()]
    for i in range(1, count):
        projected = normals[-1] - tangents[i] * normals[-1].dot(tangents[i])
        if projected.length <= 1e-7:
            projected = binormals[-1] - tangents[i] * binormals[-1].dot(tangents[i])
        if projected.length <= 1e-7:
            projected = Vector((0.0, 1.0, 0.0))
        normal = projected.normalized()
        normals.append(normal)
        binormals.append(tangents[i].cross(normal).normalized())
    return tangents, normals, binormals


def _arclengths(points):
    """Cumulative centreline arc length per sample, in metres."""
    out = [0.0]
    for i in range(1, len(points)):
        out.append(out[-1] + (points[i] - points[i - 1]).length)
    return out


# ---------------------------------------------------------------------------
# The one geometry primitive: a closed swept ring surface
# ---------------------------------------------------------------------------
# Stipe, holdfast fingers, holdfast boss and blades are all the same object: a
# cross-section swept along a curve and closed with a pole at each end. Using one
# primitive is not tidiness, it is what makes the guarantees uniform -- every part
# comes out closed, manifold, consistently wound, arc-length unwrapped into ONE UV
# island, and free of the intersecting-tube z-fighting that
# ``3DMODEL_FLORA_CORAL.md`` section 3 rejects.
#
# A blade is this primitive with a flat lens cross-section: the narrow ends of the
# lens ARE the edge rim the bible demands, so the rim needs no separate stitch and
# cannot come apart from the sheet.


def _sweep_closed(bm, uv_layer, geo_layer, cls_layer, part_layer, *,
                  points, segments, offset_fn, part_id, vertex_class,
                  material_fn, geo_base, geo_lengths, seam_direction=None):
    """Sweep a parametric cross-section along ``points`` and cap both ends.

    ``offset_fn(row, u, j, theta) -> (x, y)`` returns the cross-section offset in
    the transported frame, in metres. Every amplitude a caller puts in there is
    expressed as a fraction of the LOCAL cross-section radius, never of a global
    distance from an axis: scaling displacement by distance-from-axis leaves the
    thick base porcelain-smooth while the thin tip self-intersects.

    This function does NOT write UVs. It marks the SEAM -- one cut running pole to
    pole down the least visible column -- and the conformal solver in
    :func:`_unwrap_and_pack` does the metric work afterwards.

    That split is deliberate, and it is the second design here: writing UVs
    analytically was tried first and measured. Two hand-rolled parameterisations were
    tested against the real gate. Shared centreline V gave 5037 of ~6100 triangles
    above the 15 percent aspect ceiling; per-column unrolled V with the
    ``dv = sqrt(d3d^2 - du^2)`` correction gave 455 triangles with UV area exactly
    0.0. The second failure is instructive rather than a bug: cumulative U drift
    accumulates all the way around a ring, so on a tapering tube the drift at the far
    columns exceeds the row span and the correction collapses. A tapered tube unrolls
    isometrically to an annulus SECTOR, not to a rectangle, and a leaf-shaped blade
    plan-form is not developable at all -- no rectangular lattice mapping can be
    conformal on it. Gaussian curvature, not sloppy code.

    ``3dmodel.md`` section 6 lists the answer first among its approved routes:
    "Conformal unwrap using LSCM/ABF-style angle preservation for unique surfaces."
    Blender ships both, so the solver gets the metric problem and this function keeps
    what a solver cannot infer: WHERE the seam belongs. Section 5 is specific about
    that -- "seam on the least visible rear side" -- and seam placement is the half
    that carries the art direction.

    Returns the seam column index and edge count, for the caller's records.
    """
    rows = len(points)
    tangents, normals, binormals = _parallel_frames(points)

    # -- ring plan --------------------------------------------------------
    plan = []
    for i in range(rows):
        u_param = i / float(rows - 1) if rows > 1 else 0.0
        plan.append((points[i], i, u_param, 1.0, geo_lengths[i]))

    # -- vertices ---------------------------------------------------------
    # The ring closes on itself: exactly ``segments`` columns, no duplicated seam
    # column. The seam is a marked EDGE CHAIN, which is what the unwrap solver
    # consumes, so a duplicated column would only add welded-away vertices.
    grid = []
    for centre, frame_index, u_param, radius_scale, geo_length in plan:
        normal = normals[frame_index]
        binormal = binormals[frame_index]
        row = []
        for j in range(segments):
            theta = 2.0 * math.pi * j / float(segments)
            off_x, off_y = offset_fn(frame_index, u_param, j, theta)
            position = centre + normal * (off_x * radius_scale) + \
                binormal * (off_y * radius_scale)
            vertex = bm.verts.new(position)
            vertex[geo_layer] = geo_base + max(0.0, geo_length)
            vertex[cls_layer] = vertex_class
            row.append(vertex)
        grid.append(row)
    bm.verts.index_update()
    ring_count = len(grid)

    # -- seam column: the least visible side ------------------------------
    # Section 5: "Stipes and branches use cylindrical unwrap with seam on the least
    # visible rear side." Measured at the mid ring against the caller's chosen hide
    # direction, so the cut lands on the lee/underside rather than on whichever column
    # happens to be index 0.
    mid = ring_count // 2
    mid_frame = plan[mid][1]
    seam_column = 0
    if seam_direction is not None and seam_direction.length > 1e-9:
        hide = seam_direction.normalized()
        best = -2.0
        for j in range(segments):
            theta = 2.0 * math.pi * j / float(segments)
            world = (normals[mid_frame] * math.cos(theta) +
                     binormals[mid_frame] * math.sin(theta))
            score = world.normalized().dot(hide)
            if score > best:
                best = score
                seam_column = j

    # -- quads ------------------------------------------------------------
    for i in range(ring_count - 1):
        for j in range(segments):
            nxt = (j + 1) % segments
            a = grid[i][j]
            b = grid[i][nxt]
            c = grid[i + 1][nxt]
            d = grid[i + 1][j]
            if len({a, b, c, d}) != 4:
                # A collapsed cross-section would make a degenerate quad, which
                # 3dmodel.md section 10 rejects outright. Skipping is correct: the
                # apex fans below close whatever this leaves open.
                continue
            face = bm.faces.new((a, b, c, d))
            face.material_index = material_fn(i, j, ring_count, segments)
            face[part_layer] = part_id

    # -- n-gon caps -------------------------------------------------------
    # A single apex vertex is a CONE POINT, and a conformal solver has a scale
    # singularity there: measured, the fan triangles around one came back at 7.7 aspect
    # distortion against an outlier ceiling of 3.3, and flattening the dome did not help
    # (apex at 0.80 vs 0.26 of the radius changed nothing). An n-gon cap has no interior
    # vertex to be singular at, and cutting it free along its own boundary gives the
    # solver three easy pieces per part -- a rectangle and two near-flat discs -- rather
    # than one sphere with two puncture points.
    #
    # The shell stays closed, as 3dmodel.md section 5 requires ("all sheet borders must
    # be capped"). These are caps, not holes. Winding is left to the recalculation pass
    # in weld_and_clean, which orients each closed component outward.
    caps = []
    for end in (0, ring_count - 1):
        row = grid[end]
        if len(row) < 3:
            continue
        # FLAT centroid fan, not one n-gon. A stadium cross-section has runs of nearly
        # COLLINEAR vertices along its straight sides, and any triangulation of an n-gon
        # spanning them emits near-degenerate triangles -- measured as a single triangle
        # at 8.9 aspect distortion that no area or edge-length filter caught, because its
        # world area cleared the threshold while its UV area collapsed.
        #
        # The centroid sits IN the ring's own plane, so the angle sum around it is about
        # 2*pi and there is no cone deficit: this is not the tall apex dome that caused
        # the original singularity (that one had a large deficit and compressed UV by
        # ~1000x). A flat fan gives well-shaped triangles with no collinear triples.
        centre = Vector((0.0, 0.0, 0.0))
        for vertex in row:
            centre += vertex.co
        centre /= float(len(row))
        hub = bm.verts.new(centre)
        hub[geo_layer] = row[0][geo_layer]
        hub[cls_layer] = vertex_class
        made = []
        for j in range(len(row)):
            first = row[j]
            second = row[(j + 1) % len(row)]
            triple = (first, second, hub) if end else (second, first, hub)
            try:
                face = bm.faces.new(triple)
            except ValueError:
                continue
            face.material_index = material_fn(plan[end][1], j, ring_count, segments)
            face[part_layer] = part_id
            made.append(face)
        caps.append((row, made))

    # -- mark the seam chain ----------------------------------------------
    # Pole to pole down one column. Topologically each part is a sphere, so a single
    # cut of this shape opens it into a disc -- the domain a conformal solver needs.
    # Without the apex spokes the cut stops short of the poles and the surface stays
    # closed, at which point the solver has to invent its own seam.
    seam_edges = 0
    chain = [grid[i][seam_column] for i in range(ring_count)]
    for index in range(len(chain) - 1):
        edge = bm.edges.get((chain[index], chain[index + 1]))
        if edge is not None:
            edge.seam = True
            seam_edges += 1
    # Cut each cap free along its boundary ring, so the part unwraps as a rectangle plus
    # two discs instead of a punctured sphere.
    for row, _faces in caps:
        for j in range(len(row)):
            edge = bm.edges.get((row[j], row[(j + 1) % len(row)]))
            if edge is not None and not edge.seam:
                edge.seam = True
                seam_edges += 1

    return {"seamColumn": seam_column, "seamEdges": seam_edges,
            "rings": ring_count, "segments": segments}

    return (u_min, v_min, u_max, v_max)


# ---------------------------------------------------------------------------
# Stage 2: shape grammar
# ---------------------------------------------------------------------------
# PROCEDURAL_ASSET_PIPELINE.md "Generation Order" step 2: "Build high-level shape
# grammar: modules, branches, bones, strata, sockets, anchors, and silhouette
# landmarks." Nothing is displaced yet -- this decides the organism.


class KelpForm:
    """Deterministic high-level description of one kelp individual.

    ``PROCEDURAL_ASSET_PIPELINE.md``: organic flora "must read as grown under
    current, pressure, and feeding behavior", so the current direction is a first
    class parameter here rather than a cosmetic wobble added at the end. It bends
    the stipe, sweeps every blade downstream, sizes the lee-side blades larger and
    splays the holdfast wider on the upstream side where the load lands.
    """

    def __init__(self, seed: int, quality: float) -> None:
        rng = _rng(seed, STREAM_FORM)
        self.seed = int(seed)
        self.quality = law.saturate(quality)

        self.height = float(rng.uniform(1.75, 2.65))
        current_angle = float(rng.uniform(0.0, 2.0 * math.pi))
        self.current = Vector((math.cos(current_angle), math.sin(current_angle), 0.0))
        self.cross_current = Vector((-self.current.y, self.current.x, 0.0))
        self.current_strength = float(rng.uniform(0.34, 0.72))
        self.noise_offset = Vector((float(rng.uniform(-64.0, 64.0)),
                                   float(rng.uniform(-64.0, 64.0)),
                                   float(rng.uniform(-64.0, 64.0))))

        # Stipe cross-section. A constant-radius tube is an explicit rejection in
        # section 3, so taper, ellipse eccentricity, a rotating ellipse and an
        # angular rib count are all part of the form, not optional polish.
        self.stipe_radius_base = float(rng.uniform(0.021, 0.031))
        self.stipe_radius_top = float(rng.uniform(0.007, 0.011))
        self.stipe_ellipse = float(rng.uniform(0.14, 0.24))
        self.stipe_twist = float(rng.uniform(0.7, 2.1))
        self.rib_count = int(rng.integers(5, 9))
        self.rib_amplitude = float(rng.uniform(0.055, 0.095))
        self.growth_ring_frequency = float(rng.uniform(6.0, 11.0))
        self.growth_ring_amplitude = float(rng.uniform(0.026, 0.046))

        # Pneumatocyst swellings: real kelp carries gas bladders on the stipe.
        # Narrow bands, and the amplitude is a FRACTION of the local radius. The first
        # version added an absolute 0.035-0.070 m to a 0.036-0.052 m radius, which
        # could triple it -- the render showed a bloated tentacle, not a stipe. Same
        # lesson as scaling any displacement by local radius instead of a global size.
        swelling_count = _qi(2, 4, self.quality)
        self.swellings = tuple(
            (float(rng.uniform(0.18, 0.94)), float(rng.uniform(0.022, 0.045)),
             float(rng.uniform(0.35, 0.85)))
            for _ in range(swelling_count)
        )

        # Holdfast. Fingers splay along the ground plane; the boss is the knuckle
        # that hides the union, which is what section 3 permits instead of a
        # boolean: "Branch intersections must be blended, welded, or explicitly
        # hidden by knuckles."
        self.finger_count = _qi(6, 11, self.quality)
        self.boss_radius = float(rng.uniform(0.080, 0.112))
        self.boss_height = float(rng.uniform(0.105, 0.155))

        # Blades. Grammar taken from the mandatory reference ``forest_kelp.webp``,
        # opened directly: the blades there are SHORT relative to the stipe, dense,
        # curled, and distributed along the WHOLE length, giving a bottle-brush
        # silhouette. An earlier draft here used a canopy cluster of long fronds;
        # against the reference that reads as ribbons on a stick, which is the
        # section 3 "loose vertical ribbon" failure wearing a different hat.
        # Densities converged by looking at the renders next to the reference. 6-13
        # long blades read as a corn stalk; the reference crowds many shorter, ruffled
        # blades along the stipe. Length came down and count went up together, because
        # raising count alone just makes a bigger corn stalk.
        self.canopy_blades = _qi(11, 22, self.quality)
        self.basal_blades = _qi(2, 4, self.quality)
        self.blade_length = float(rng.uniform(0.26, 0.46))
        self.blade_width = float(rng.uniform(0.050, 0.082))
        # Thicker than the first pass. The rim columns of a very flat lens are where the
        # area-weighted stretch concentrates, and 3DMODEL_FLORA_CORAL.md section 3 wants
        # real thickness anyway: "Blade surfaces must not be zero-thickness if seen from
        # both sides at close range."
        self.blade_thickness = float(rng.uniform(0.0058, 0.0088))

    @property
    def blade_count(self) -> int:
        return self.canopy_blades + self.basal_blades

    # -- stipe centreline -------------------------------------------------

    def stipe_point(self, t: float) -> Vector:
        """Centreline at normalised height ``t``.

        The lateral term grows faster than linearly so the bend concentrates in the
        upper stipe, which is how a flexible stem loaded by drag actually deforms:
        the base is stiff because the moment arm is short.
        """
        lateral = self.current * (self.current_strength * self.height * (t ** 1.85))
        sway = self.cross_current * (0.055 * self.height *
                                     math.sin(t * math.pi * 1.35))
        return Vector((lateral.x + sway.x, lateral.y + sway.y,
                       self.boss_height * 0.55 + t * self.height))

    def stipe_radius(self, t: float) -> float:
        radius = self.stipe_radius_top + (self.stipe_radius_base -
                                          self.stipe_radius_top) * ((1.0 - t) ** 0.85)
        swell = 0.0
        for centre, width, fraction in self.swellings:
            swell += fraction * math.exp(
                -((t - centre) ** 2) / max(1e-6, width * width * 2.0))
        return radius * (1.0 + min(1.15, swell))


def _stipe_material_for(form):
    """Stipe faces are tissue, except the pneumatocyst swellings, which are bladder.

    The gas bladders are real geometry (see ``KelpForm.stipe_radius``), so giving them
    their own slot means the amber accent lands on a bulge the silhouette already has,
    rather than being painted onto a smooth tube.
    """
    def material_fn(i, j, rings, segments):
        u = i / float(max(1, rings - 1))
        # Basal collar: the scarred transition where the stipe emerges from the
        # holdfast. This is a genuine section 6 slot-1 surface ("exposed cut, bevel,
        # edge, SCAR, or fracture material"), and unlike a blade rim it is a full ring
        # band with enough area to survive to LOD2 as a compact UV island.
        if u < 0.17:
            return law.MATERIAL_SLOT_CUT_EDGE
        for centre, width, _fraction in form.swellings:
            if abs(u - centre) < width * 1.6:
                return law.MATERIAL_SLOT_EMISSIVE
        return law.MATERIAL_SLOT_PRIMARY
    return material_fn


def _build_stipe(bm, layers, form, rows: int, segments: int, part_id: int):
    """Swept stipe with taper, rotating elliptical section, ribs and growth rings.

    Section 3 requires "Stipe or spine with taper and ribbing". Every displacement
    below is a fraction of the LOCAL radius, so the thick base and the thin apex
    receive proportional detail instead of the base staying porcelain-smooth while
    the tip self-intersects.
    """
    uv_layer, geo_layer, cls_layer, part_layer = layers
    points = [form.stipe_point(i / float(rows - 1)) for i in range(rows)]
    lengths = _arclengths(points)

    def offset(row, u, j, theta):
        radius = form.stipe_radius(u)
        twist = form.stipe_twist * u * math.pi
        local = theta + twist
        # Rotating ellipse: the cross-section is never circular and never the same
        # shape twice along the length.
        ellipse = 1.0 + form.stipe_ellipse * math.cos(2.0 * local)
        # Anisotropic bands ALONG the structure are the dominant detail term.
        ribs = 1.0 + form.rib_amplitude * math.cos(form.rib_count * local)
        rings = 1.0 + form.growth_ring_amplitude * math.sin(
            form.growth_ring_frequency * math.pi * u)
        sample = Vector((math.cos(theta) * 0.5, math.sin(theta) * 0.5, u * 3.4))
        fine = 1.0 + 0.12 * _fine_noise(sample, form.noise_offset, 2.6)
        scaled = radius * ellipse * ribs * rings * fine
        return (math.cos(theta) * scaled, math.sin(theta) * scaled)

    # Section 5: seam on the least visible rear side. For a current-bent stipe the
    # rear is the lee face, which the player sees least because the plant leans away.
    info = _sweep_closed(
        bm, uv_layer, geo_layer, cls_layer, part_layer,
        points=points, segments=segments, offset_fn=offset, part_id=part_id,
        vertex_class=CLS_STIPE,
        material_fn=_stipe_material_for(form),
        geo_base=form.boss_height * 0.55, geo_lengths=lengths,
        seam_direction=form.current.copy())
    return info, points, lengths


def _build_holdfast(bm, layers, form, quality: float, part_start: int):
    """Boss plus splayed haptera fingers that follow the ground plane.

    Section 3: "Holdfast or root cluster, not a loose vertical ribbon." The
    "Roots And Biofilms" clause adds: "Roots must follow surface curvature, include
    anchor pads, and avoid perfectly parallel strands." Hence per-finger reach,
    azimuth jitter, a sideways curl, an upstream splay bias and knobbly haptera
    swellings, so no two strands run parallel.
    """
    uv_layer, geo_layer, cls_layer, part_layer = layers
    rng = _rng(form.seed, STREAM_HOLDFAST)
    islands = []
    part_id = part_start

    boss_rows = _qi(5, 8, quality)
    boss_segments = _qi(8, 13, quality)
    boss_points = [Vector((0.0, 0.0, form.boss_height * (i / float(boss_rows - 1))))
                   for i in range(boss_rows)]
    boss_lengths = _arclengths(boss_points)
    boss_lumps = float(rng.uniform(0.10, 0.19))

    def boss_offset(row, u, j, theta):
        # Widest at the substrate, narrowing where the stipe emerges: an anchor pad,
        # not a ball resting on the floor.
        radius = form.boss_radius * (1.0 - 0.42 * (u ** 1.25))
        lumps = 1.0 + boss_lumps * math.cos(3.0 * theta + u * 2.2)
        sample = Vector((math.cos(theta), math.sin(theta), u * 2.0))
        fine = 1.0 + 0.12 * _fine_noise(sample, form.noise_offset, 3.1)
        scaled = radius * lumps * fine
        return (math.cos(theta) * scaled, math.sin(theta) * scaled)

    # The boss seam faces downstream and low, where the holdfast meets sediment and
    # nothing is visible.
    islands.append(_sweep_closed(
        bm, uv_layer, geo_layer, cls_layer, part_layer,
        points=boss_points, segments=boss_segments, offset_fn=boss_offset,
        part_id=part_id, vertex_class=CLS_BOSS,
        material_fn=lambda i, j, r, s: law.MATERIAL_SLOT_TRIM,
        geo_base=0.0, geo_lengths=boss_lengths,
        seam_direction=form.current.copy()))
    part_id += 1

    finger_rows = _qi(5, 8, quality)
    finger_segments = _qi(6, 10, quality)
    for index in range(form.finger_count):
        azimuth = 2.0 * math.pi * index / float(form.finger_count) + \
            float(rng.uniform(-0.34, 0.34))
        direction = Vector((math.cos(azimuth), math.sin(azimuth), 0.0))
        # Upstream fingers reach further: that is where the drag load is resisted.
        upstream = law.saturate(0.5 - 0.5 * direction.dot(form.current))
        reach = float(rng.uniform(0.170, 0.285)) * (0.82 + 0.42 * upstream)
        radius0 = float(rng.uniform(0.018, 0.028))
        knuckle_freq = float(rng.uniform(4.5, 8.5))
        knuckle_amp = float(rng.uniform(0.09, 0.17))
        knuckle_phase = float(rng.uniform(0.0, 6.28))
        curl = float(rng.uniform(-0.28, 0.28))

        points = []
        for step in range(finger_rows):
            u = step / float(finger_rows - 1)
            horizontal = direction * (reach * (u ** 0.72))
            side = form.cross_current * (curl * reach * (u ** 1.6))
            height = (form.boss_height * 0.52) * ((1.0 - u) ** 1.25) + 0.009
            points.append(Vector((horizontal.x + side.x,
                                  horizontal.y + side.y, height)))
        lengths = _arclengths(points)

        def finger_offset(row, u, j, theta, r0=radius0, kf=knuckle_freq,
                          ka=knuckle_amp, phase=knuckle_phase):
            radius = r0 * (1.0 - 0.58 * (u ** 0.9)) + 0.0035
            knuckles = 1.0 + ka * math.sin(kf * math.pi * u + phase)
            # Flattened against the substrate: a gripping root, not a wire.
            flatten = 1.0 - 0.22 * abs(math.sin(theta))
            sample = Vector((math.cos(theta), math.sin(theta), u * 4.0))
            fine = 1.0 + 0.12 * _fine_noise(sample, form.noise_offset, 3.6)
            scaled = radius * knuckles * fine
            return (math.cos(theta) * scaled, math.sin(theta) * scaled * flatten)

        # A haptera lies on the substrate, so its underside is the least visible
        # surface there is.
        islands.append(_sweep_closed(
            bm, uv_layer, geo_layer, cls_layer, part_layer,
            points=points, segments=finger_segments, offset_fn=finger_offset,
            part_id=part_id, vertex_class=CLS_FINGER,
            material_fn=lambda i, j, r, s: law.MATERIAL_SLOT_TRIM,
            geo_base=0.012, geo_lengths=lengths,
            seam_direction=Vector((0.0, 0.0, -1.0))))
        part_id += 1

    return islands, part_id


# ---------------------------------------------------------------------------
# Blades  --  3DMODEL_FLORA_CORAL.md section 3
# ---------------------------------------------------------------------------
# "Blade/frond sheets with thickness or edge rim." and "Blade surfaces must not be
# zero-thickness if seen from both sides at close range. Use a thin shell with edge
# rim."
#
# A blade here is the same closed sweep as the stipe, with a FLAT LENS
# cross-section: half-width ``a`` across the sheet, half-thickness ``b`` through
# it, with a >> b. The narrow ends of the lens are the edge rim, so the rim is part
# of the same manifold shell and cannot separate from the sheet, cannot z-fight
# against it, and gets the cut/edge material slot.
#
# Ring layout: ``segments = 2 * (nu - 1)``, so j = 0 lands exactly on theta = 0
# (right rim) and j = nu - 1 lands exactly on theta = pi (left rim). j in 1..nu-2
# is the upper face, j in nu..segments-1 the lower face. That exactness is what
# lets the rim be selected for material slot 1 by index rather than by a
# floating-point angle test.


def _build_blades(bm, layers, form, quality: float, stipe_points, stipe_lengths,
                  part_start: int):
    """Every frond, swept downstream, with the full section-3 detail set."""
    uv_layer, geo_layer, cls_layer, part_layer = layers
    rng = _rng(form.seed, STREAM_BLADES)
    detail_rng = _rng(form.seed, STREAM_DETAIL)
    islands = []
    part_id = part_start
    attachments = []

    rows = _qi(7, 10, quality)
    # More columns across the lens lowers the share of surface AREA sitting in the
    # heavily foreshortened rim columns, which is what the area-weighted stretch gate
    # measures. Measured at nu 5-7: 11.7% of area over the 0.55 organic limit vs a 10%
    # allowance.
    nu = _qi(6, 8, quality)
    segments = 2 * (nu - 1)
    # Serration teeth and blister count are the density knobs section 9 names:
    # "GlobalQualityWeight scales flora and coral fidelity through offline branch
    # count, pore density, blade serration density..."
    serration_teeth = _qi(4, 11, quality)
    blister_count = _qi(1, 4, quality)
    tear_count = _qi(1, 3, quality)

    stipe_rows = len(stipe_points)

    # Blades run the whole stipe, densest in the upper half, as the reference shows.
    # Spacing is a jittered even distribution rather than independent random draws, so
    # no two blades land on one ring and leave a bald patch somewhere else.
    heights = []
    for k in range(form.canopy_blades):
        span = k / float(max(1, form.canopy_blades - 1)) if form.canopy_blades > 1 else 0.5
        base = 0.14 + 0.85 * (span ** 0.82)
        heights.append((base + float(rng.uniform(-0.018, 0.018)), True))
    for k in range(form.basal_blades):
        span = k / float(max(1, form.basal_blades - 1)) if form.basal_blades > 1 else 0.5
        heights.append((0.055 + 0.085 * span + float(rng.uniform(-0.012, 0.012)), False))

    for index, (height_t, is_canopy) in enumerate(heights):
        height_t = min(0.985, max(0.055, height_t))
        row = height_t * (stipe_rows - 1)
        low = int(math.floor(row))
        high = min(stipe_rows - 1, low + 1)
        blend = row - low
        attach = stipe_points[low].lerp(stipe_points[high], blend)
        attach_length = _lerp(stipe_lengths[low], stipe_lengths[high], blend)
        stipe_r = form.stipe_radius(height_t)

        azimuth = float(rng.uniform(0.0, 2.0 * math.pi))
        outward = Vector((math.cos(azimuth), math.sin(azimuth), 0.0))
        # Flow-facing asymmetry: fronds on the lee side grow longer because they
        # are not being scoured, and every frond trails downstream.
        lee = law.saturate(0.5 + 0.5 * outward.dot(form.current))
        length = form.blade_length * (0.78 + 0.42 * lee) * \
            (1.0 if is_canopy else 0.78) * float(rng.uniform(0.82, 1.22))
        width = form.blade_width * (0.85 + 0.3 * lee) * float(rng.uniform(0.9, 1.12))
        thickness = form.blade_thickness * float(rng.uniform(0.9, 1.1))

        # Elevation: blades radiate at varied pitch -- some angled up, some straight
        # out, some slumped. One shared rise/droop profile for every blade is what
        # makes a procedural plant read as a manufactured object.
        elevation = float(rng.uniform(-0.55, 0.95))
        rise = float(rng.uniform(0.10, 0.30)) + max(0.0, elevation) * 0.42
        droop = float(rng.uniform(0.14, 0.36)) + max(0.0, -elevation) * 0.55
        # Curl: reference blades twist and hook rather than lying in one plane.
        curl_side = float(rng.uniform(-0.42, 0.42))
        serr_phase_right = float(detail_rng.uniform(0.0, 1.0))
        serr_phase_left = float(detail_rng.uniform(0.0, 1.0))
        serr_amp = float(detail_rng.uniform(0.20, 0.34))
        fold_k = float(detail_rng.uniform(1.6, 3.4))
        fold_phase = float(detail_rng.uniform(0.0, 6.28))
        fold_amp = float(detail_rng.uniform(1.6, 3.2))
        tears = tuple((float(detail_rng.uniform(0.25, 0.9)),
                       1.0 if detail_rng.random() < 0.5 else -1.0,
                       float(detail_rng.uniform(0.030, 0.065)),
                       float(detail_rng.uniform(0.32, 0.54)))
                      for _ in range(tear_count))
        blisters = tuple((float(detail_rng.uniform(0.10, 0.85)),
                          float(detail_rng.uniform(-0.75, 0.75)),
                          float(detail_rng.uniform(0.045, 0.10)),
                          float(detail_rng.uniform(0.9, 2.3)))
                         for _ in range(blister_count))
        scar_at = float(detail_rng.uniform(0.2, 0.8))
        scar_width = float(detail_rng.uniform(0.020, 0.045))

        # Curve: leaves the stipe, sweeps downstream under drag, rises then droops.
        points = []
        for step in range(rows):
            u = step / float(rows - 1)
            # Start inside the stipe so the junction is a hidden union under the
            # sheath, per the section 3 weld/knuckle/hidden-union clause.
            radial = outward * (-stipe_r * 0.55 + length * 0.90 * (u ** 0.78))
            flow = form.current * (length * 0.52 * form.current_strength *
                                   1.45 * (u ** 1.4))
            # Sideways curl, perpendicular to this blade's own outward direction.
            sideways = Vector((-outward.y, outward.x, 0.0)) * \
                (length * curl_side * (u ** 1.5))
            vertical = length * (rise * (u ** 0.55) - droop * (u ** 2.1))
            points.append(Vector((attach.x + radial.x + flow.x + sideways.x,
                                  attach.y + radial.y + flow.y + sideways.y,
                                  attach.z + vertical)))
        lengths = _arclengths(points)

        def blade_offset(row_index, u, j, theta,
                         w=width, th=thickness, sa=serr_amp,
                         spr=serr_phase_right, spl=serr_phase_left,
                         fk=fold_k, fp=fold_phase, fa=fold_amp,
                         tear_list=tears, blister_list=blisters,
                         scar_u=scar_at, scar_w=scar_width):
            # Sheet plan-form: narrow at the sheath, widest just past mid, tapering
            # to a rounded tip. A rectangle is the "flat untextured rectangle" the
            # section 8 gate rejects.
            plan = math.sin(math.pi * (u ** 0.72)) ** 0.55
            # The 0.30 floor is a real rounded tip, not a needle. A blade that tapers
            # to nothing produces sub-millimetre cap triangles, which read as
            # degenerate to the UV gate and shade badly at any LOD.
            nominal_a = w * (0.30 + 0.80 * plan)
            nominal_b = th * (0.72 + 0.28 * plan)

            # STADIUM cross-section sampled by ARC LENGTH, not by uniform theta.
            # This is the fix for the last failing gate. An ellipse sampled at uniform
            # theta crowds its samples where the curve turns tightest -- at the rim,
            # where x = a*cos(theta) barely moves while y races -- so with a/b around
            # 8-11 the chord spacing across one ring varies by that same factor. Those
            # crowded rim columns are simultaneously the thin triangles that breach the
            # outlier ceiling and the surface area that breaches the area fraction.
            # Equal arc spacing makes every column the same width, so the quads are
            # near-isotropic and the conformal solver has almost nothing left to fix.
            # A stadium also IS the "thin shell with edge rim" section 3 asks for: two
            # flat faces joined by a real half-round rim of radius b.
            straight = max(1e-6, nominal_a - nominal_b)
            perimeter = 4.0 * straight + 2.0 * math.pi * nominal_b
            walk = (theta / (2.0 * math.pi)) * perimeter
            arc = math.pi * nominal_b
            if walk < straight:
                # upper face, centre -> right
                x, y = walk, nominal_b
            elif walk < straight + arc:
                # right rim, +90deg -> -90deg
                phi = math.pi * 0.5 - (walk - straight) / max(1e-9, nominal_b)
                x = straight + nominal_b * math.cos(phi)
                y = nominal_b * math.sin(phi)
            elif walk < 3.0 * straight + arc:
                # lower face, right -> left
                x, y = (2.0 * straight + arc - walk), -nominal_b
            elif walk < 3.0 * straight + 2.0 * arc:
                # left rim, -90deg -> -270deg
                phi = -math.pi * 0.5 - (walk - 3.0 * straight - arc) /                     max(1e-9, nominal_b)
                x = -straight + nominal_b * math.cos(phi)
                y = nominal_b * math.sin(phi)
            else:
                # upper face, left -> centre
                x, y = (walk - 4.0 * straight - 2.0 * arc), nominal_b

            # Normalised position across the sheet, and which face we are on. Every
            # feature below keys off these instead of off cos/sin(theta).
            cx = max(-1.0, min(1.0, x / max(1e-9, nominal_a)))
            sy = y

            half_width = nominal_a

            # Serration: independent tooth phase per margin, so the two edges are
            # not mirror images of each other.
            phase = spr if cx >= 0.0 else spl
            teeth = 1.0 + sa * (_tri_wave(u * serration_teeth + phase) - 0.5) * 2.0
            half_width *= teeth

            # Tears: a deep notch bitten out of one margin. Kept above the
            # 1e-4 weld distance so remove_doubles cannot close the notch.
            for tear_u, tear_side, tear_span, tear_depth in tear_list:
                if (cx >= 0.0) == (tear_side > 0.0):
                    falloff = math.exp(-((u - tear_u) ** 2) /
                                       max(1e-6, tear_span * tear_span))
                    half_width *= (1.0 - tear_depth * falloff)

            # Floor the margin. Serration and a tear can otherwise stack
            # multiplicatively and pinch the sheet to near-zero width, which produces a
            # sliver triangle whose UV blows up -- measured as a single LOD0 triangle at
            # 81.89 aspect distortion, over the outlier ceiling. The floor is a real
            # feature too: a torn kelp blade keeps a ragged web, it does not vanish.
            half_width = max(half_width, w * 0.24)

            half_thickness = nominal_b

            # Blisters: one-sided pneumatocyst bumps on the upper face only, which
            # is where gas bladders actually form.
            if sy > 0.0:
                for b_u, b_cx, b_span, b_amp in blister_list:
                    falloff = math.exp(-(((u - b_u) ** 2) / max(1e-6, b_span * b_span) +
                                         ((cx - b_cx) ** 2) / 0.28))
                    half_thickness *= (1.0 + b_amp * falloff)

            # Healed scar: a shallow groove across the sheet.
            half_thickness *= (1.0 - 0.34 * math.exp(
                -((u - scar_u) ** 2) / max(1e-6, scar_w * scar_w)))

            sample = Vector((cx, u * 5.0, 0.0))
            half_thickness *= (1.0 + 0.12 * _fine_noise(sample, form.noise_offset, 3.2))

            # Longitudinal folds displace the MID-SURFACE, so thickness is
            # preserved and the sheet ruffles instead of getting fatter. Strongest
            # at the margins, which is how a kelp blade ripples.
            fold = fa * w * 0.10 * math.sin(fk * math.pi * u + fp) *                 (0.35 + 0.65 * abs(cx))

            # Scale the arc-length sample by however much the features moved the local
            # radii, rather than re-deriving the position from an angle. Serration and
            # tears are +-30% at most, so equal-arc spacing survives the rescale.
            return (x * (half_width / max(1e-9, nominal_a)),
                    y * (half_thickness / max(1e-9, nominal_b)) + fold)

        def blade_material(i, j, r, s, n=nu, seg=segments,
                           blister_list=blisters):
            # The blade margin is NOT a separate material slot. Putting slot 1 on the rim
            # columns was measured to cause four separate failures at once: a pale streak
            # chalk-outlining every blade, 2.4 px sliver islands (build_lod_chain splits
            # material borders, so a two-column strip becomes its own island), a
            # degenerate LOD1 UV triangle at 181 aspect distortion once that strip
            # decimated to one face, and slot 1 emptying at LOD2. A thin geometric band
            # is the wrong carrier for a material ID; the margin is a texture job.
            # Slot 1 now lives on the basal collar, which has real area -- see
            # _stipe_material_for.
            #
            # Pneumatocyst blisters carry the amber bladder pigment the reference uses
            # as its colour focal point. Upper face only, matching the geometry: the
            # bulge is only raised where sin(theta) > 0.
            return law.MATERIAL_SLOT_PRIMARY

        # A blade's underside faces away from the light and the swimmer above it, so
        # the cut goes there rather than across the lit upper face.
        islands.append(_sweep_closed(
            bm, uv_layer, geo_layer, cls_layer, part_layer,
            points=points, segments=segments, offset_fn=blade_offset,
            part_id=part_id, vertex_class=CLS_BLADE,
            material_fn=blade_material,
            geo_base=form.boss_height * 0.55 + attach_length,
            geo_lengths=lengths,
            seam_direction=Vector((0.0, 0.0, -1.0))))
        attachments.append({
            "index": index,
            "heightT": round(height_t, 5),
            "canopy": bool(is_canopy),
            "lengthM": round(length, 5),
            "widthM": round(width, 5),
            "thicknessM": round(thickness, 6),
            "serrationTeeth": serration_teeth,
            "tears": tear_count,
            "blisters": blister_count,
        })
        part_id += 1

    return islands, part_id, attachments, {
        "rows": rows,
        "crossSectionVerts": segments,
        "faceColumnsPerSide": nu - 1,
        "serrationTeeth": serration_teeth,
        "blistersPerBlade": blister_count,
        "tearsPerBlade": tear_count,
    }


# ---------------------------------------------------------------------------
# Stage 5: UVs  --  3dmodel.md section 6, 3DMODEL_FLORA_CORAL.md section 5
# ---------------------------------------------------------------------------
# Seam placement is authored (see _sweep_closed); the metric is solved. This stage
# runs the solver, forces each island's V to follow the growth direction, equalises
# texel density across islands, packs, and then MEASURES the result rather than
# assuming the operators did what their names suggest.


def _face_islands(mesh):
    """Group polygons into islands by flood fill that refuses to cross a seam.

    The seams were marked structurally, one pole-to-pole cut per part, so this
    returns exactly one island per part without needing the part attribute -- and it
    reports the truth about what the solver actually saw, which is the point.
    """
    seam = {}
    for edge in mesh.edges:
        if edge.use_seam:
            seam[tuple(sorted(edge.vertices))] = True

    # edge key -> polygons sharing it
    shared = {}
    for polygon in mesh.polygons:
        keys = []
        count = len(polygon.vertices)
        for k in range(count):
            a = polygon.vertices[k]
            b = polygon.vertices[(k + 1) % count]
            keys.append(tuple(sorted((a, b))))
        for key in keys:
            shared.setdefault(key, []).append(polygon.index)

    islands = []
    assigned = [-1] * len(mesh.polygons)
    for polygon in mesh.polygons:
        if assigned[polygon.index] >= 0:
            continue
        island_index = len(islands)
        stack = [polygon.index]
        members = []
        assigned[polygon.index] = island_index
        while stack:
            current = stack.pop()
            members.append(current)
            poly = mesh.polygons[current]
            count = len(poly.vertices)
            for k in range(count):
                a = poly.vertices[k]
                b = poly.vertices[(k + 1) % count]
                key = tuple(sorted((a, b)))
                if key in seam:
                    continue
                for neighbour in shared.get(key, ()):
                    if assigned[neighbour] < 0:
                        assigned[neighbour] = island_index
                        stack.append(neighbour)
        islands.append(members)
    return islands


def _orient_islands_to_growth(mesh, islands) -> int:
    """Rotate each island so +V runs root-to-tip along the organism.

    ``3DMODEL_FLORA_CORAL.md`` section 5: "Kelp blades use lengthwise UVs: V from
    root to tip, U from left edge to right edge." A conformal solver has no idea
    which way is "root"; it returns an arbitrarily rotated island. Left alone, every
    blade would carry its texture at a random angle, and a lengthwise-anisotropic
    kelp texture would run across the blade on roughly half of them.

    The growth parameter is already known per vertex: it is the geodesic distance
    stored during the sweep. Least-squares fitting geodesic against (u, v) gives the
    UV-space direction of growth, and one rotation per island puts it on +V. This is
    exact, cheap, and needs no guessing about blade orientation in world space.

    Returns the number of islands rotated.
    """
    geodesic = mesh.attributes.get(GEO_LAYER)
    if geodesic is None or geodesic.domain != "POINT":
        return 0
    values = [0.0] * len(mesh.vertices)
    geodesic.data.foreach_get("value", values)

    uv_layer = mesh.uv_layers.active
    if uv_layer is None:
        return 0
    rotated = 0

    for members in islands:
        loops = []
        for polygon_index in members:
            polygon = mesh.polygons[polygon_index]
            for loop_index in polygon.loop_indices:
                loops.append(loop_index)
        if len(loops) < 3:
            continue

        # Least squares for geodesic ~ a*u + b*v + c. The gradient (a, b) is the
        # UV direction in which growth increases fastest.
        sum_u = sum_v = sum_g = 0.0
        for loop_index in loops:
            u, v = uv_layer.data[loop_index].uv
            sum_u += u
            sum_v += v
            sum_g += values[mesh.loops[loop_index].vertex_index]
        count = float(len(loops))
        mean_u = sum_u / count
        mean_v = sum_v / count
        mean_g = sum_g / count

        suu = svv = suv = sug = svg = 0.0
        for loop_index in loops:
            u, v = uv_layer.data[loop_index].uv
            du = u - mean_u
            dv = v - mean_v
            dg = values[mesh.loops[loop_index].vertex_index] - mean_g
            suu += du * du
            svv += dv * dv
            suv += du * dv
            sug += du * dg
            svg += dv * dg
        determinant = suu * svv - suv * suv
        if abs(determinant) <= 1e-18:
            continue
        # Cramer's rule on the normal equations
        # [suu suv; suv svv] [grad_u grad_v]^T = [sug svg]^T
        grad_u = (sug * svv - svg * suv) / determinant
        grad_v = (svg * suu - sug * suv) / determinant
        magnitude = math.hypot(grad_u, grad_v)
        if magnitude <= 1e-12:
            continue

        # Rotate so the growth gradient points along +V.
        angle = math.pi * 0.5 - math.atan2(grad_v, grad_u)
        cos_a = math.cos(angle)
        sin_a = math.sin(angle)
        for loop_index in loops:
            u, v = uv_layer.data[loop_index].uv
            du = u - mean_u
            dv = v - mean_v
            uv_layer.data[loop_index].uv = (mean_u + du * cos_a - dv * sin_a,
                                            mean_v + du * sin_a + dv * cos_a)
        rotated += 1
    return rotated


def _uv_metrics(mesh, atlas_size: int):
    """Measured UV facts: texel density per island, worst aspect distortion, areas.

    ``AGENTS.md`` ``[RULE] Never Trust Automated Assertions Alone``. ``uv.unwrap``,
    ``average_islands_scale`` and ``pack_islands`` all return ``{'FINISHED'}`` on
    meshes they did nothing useful to, so the numbers come from the mesh afterwards.
    """
    data = validate.extract_mesh_data(mesh)
    if not data.uv_layers:
        return None
    uv0 = data.uv_layers[0][1]

    worst = 0.0
    over_hero = 0
    over_distant = 0
    zero_area = 0
    zero_uv = 0
    degenerate_3d = 0
    worst_small = []
    distortions = []
    per_slot = {}
    worst_detail = (-1.0, {})
    organic_limit = law.UV_STRETCH_MAX_BY_SURFACE[law.SurfaceClass.ORGANIC]
    for t in range(data.triangle_count):
        l0 = data.tri_loops[t * 3]
        l1 = data.tri_loops[t * 3 + 1]
        l2 = data.tri_loops[t * 3 + 2]
        s0, t0 = uv0[l0 * 2], uv0[l0 * 2 + 1]
        s1, t1 = uv0[l1 * 2], uv0[l1 * 2 + 1]
        s2, t2 = uv0[l2 * 2], uv0[l2 * 2 + 1]
        area2 = abs((s1 - s0) * (t2 - t0) - (s2 - s0) * (t1 - t0))
        if area2 <= law.DEGENERATE_TRIANGLE_AREA_EPS:
            zero_area += 1
            zero_uv += 1
            world2 = validate.triangle_area_times_two(
                data.positions, data.tri_vertices[t * 3],
                data.tri_vertices[t * 3 + 1], data.tri_vertices[t * 3 + 2])
            worst_small.append((area2, world2, t))
            continue
        distortion = validate.uv_aspect_distortion(
            data.positions, uv0, data.tri_vertices, data.tri_loops, t)
        if distortion == float("inf"):
            zero_area += 1
            degenerate_3d += 1
            world2 = validate.triangle_area_times_two(
                data.positions, data.tri_vertices[t * 3],
                data.tri_vertices[t * 3 + 1], data.tri_vertices[t * 3 + 2])
            worst_small.append((area2, world2, t))
            continue
        distortions.append(distortion)
        if distortion > worst_detail[0]:
            i0 = data.tri_vertices[t * 3]
            i1 = data.tri_vertices[t * 3 + 1]
            i2 = data.tri_vertices[t * 3 + 2]
            w2 = validate.triangle_area_times_two(data.positions, i0, i1, i2)
            legs = []
            for a, b in ((i0, i1), (i1, i2), (i2, i0)):
                legs.append(round(math.dist(
                    data.positions[a * 3:a * 3 + 3],
                    data.positions[b * 3:b * 3 + 3]), 6))
            uvlegs = []
            l0 = data.tri_loops[t * 3]
            l1 = data.tri_loops[t * 3 + 1]
            l2 = data.tri_loops[t * 3 + 2]
            for a, b in ((l0, l1), (l1, l2), (l2, l0)):
                uvlegs.append(round(math.dist(
                    uv0[a * 2:a * 2 + 2], uv0[b * 2:b * 2 + 2]), 6))
            worst_detail = (distortion, {
                "tri": t, "slot": data.tri_material_index[t]
                if t < len(data.tri_material_index) else -1,
                "worldArea2": "%.3e" % w2, "uvArea2": "%.3e" % area2,
                "worldEdges": legs, "uvEdges": uvlegs,
                "z": [round(data.positions[i * 3 + 2], 4) for i in (i0, i1, i2)]})
        world2 = validate.triangle_area_times_two(
            data.positions, data.tri_vertices[t * 3],
            data.tri_vertices[t * 3 + 1], data.tri_vertices[t * 3 + 2])
        slot = data.tri_material_index[t] if t < len(data.tri_material_index) else 0
        entry = per_slot.setdefault(slot, [0.0, 0.0, 0.0])
        entry[0] += world2
        if distortion > organic_limit:
            entry[1] += world2
        entry[2] = max(entry[2], distortion)
        if distortion > worst:
            worst = distortion
        if distortion > law.UV_STRETCH_MAX_HERO:
            over_hero += 1
        if distortion > law.UV_STRETCH_MAX_DISTANT:
            over_distant += 1

    distortions.sort()
    total = len(distortions)

    def percentile(fraction):
        if not distortions:
            return 0.0
        index = min(total - 1, max(0, int(fraction * (total - 1))))
        return distortions[index]

    # Texel density per island, so the section 6 "mismatch above 20 percent" rule is
    # a measured number and not an aspiration.
    islands = _face_islands(mesh)
    densities = []
    uv_layer = mesh.uv_layers.active
    for members in islands:
        uv_area = 0.0
        world_area = 0.0
        for polygon_index in members:
            polygon = mesh.polygons[polygon_index]
            world_area += polygon.area
            corners = [tuple(uv_layer.data[i].uv) for i in polygon.loop_indices]
            for k in range(1, len(corners) - 1):
                a, b, c = corners[0], corners[k], corners[k + 1]
                uv_area += abs((b[0] - a[0]) * (c[1] - a[1]) -
                               (c[0] - a[0]) * (b[1] - a[1])) * 0.5
        if world_area > 1e-9 and uv_area > 1e-12:
            densities.append(math.sqrt(uv_area / world_area) * atlas_size)

    # Island pixel extents, measured with the VALIDATOR's island definition
    # (UV-coordinate connectivity) rather than the seam flood fill, so these numbers
    # are directly comparable to the gate that judges them. The two definitions differ:
    # a seam makes one part into two UV-connected pieces.
    gate_islands = validate._uv_islands(data, uv0)
    island_px = []
    for root in sorted(gate_islands.keys()):
        box = gate_islands[root]
        island_px.append((round((box[2] - box[0]) * atlas_size, 3),
                          round((box[3] - box[1]) * atlas_size, 3)))
    island_px.sort(key=lambda wh: min(wh))
    below_min = sum(1 for w, h in island_px if min(w, h) < law.UV_MIN_ISLAND_PIXELS)

    density_min = min(densities) if densities else 0.0
    density_max = max(densities) if densities else 0.0
    density_mean = (sum(densities) / len(densities)) if densities else 0.0
    mismatch = ((density_max - density_min) / density_max) if density_max > 0 else 0.0

    return {
        "atlasSize": atlas_size,
        "atlasFamily": ATLAS_FAMILY,
        "paddingPx": law.atlas_padding_for(atlas_size),
        "islands": len(islands),
        "texelDensityPxPerMetreMin": round(density_min, 2),
        "texelDensityPxPerMetreMax": round(density_max, 2),
        "texelDensityPxPerMetreMean": round(density_mean, 2),
        "texelDensityMismatchFraction": round(mismatch, 5),
        "texelDensityMismatchLimit": law.UV_TEXEL_MISMATCH_MAX,
        "aspectDistortionMax": round(worst, 5),
        "aspectDistortionP50": round(percentile(0.50), 5),
        "aspectDistortionP95": round(percentile(0.95), 5),
        "aspectDistortionP99": round(percentile(0.99), 5),
        "trianglesOverHeroLimit": over_hero,
        "trianglesOverDistantLimit": over_distant,
        "trianglesMeasured": total,
        "zeroAreaUvTriangles": zero_area,
        "zeroAreaUvBelowEpsilon": zero_uv,
        "degenerate3dTriangles": degenerate_3d,
        "smallestOffenders": [
            {"uvArea2": "%.3e" % a, "worldArea2": "%.3e" % w, "tri": t}
            for a, w, t in sorted(worst_small)[:6]],
        "areaOverLimitBySlot": {
            str(slot): {
                "role": SLOT_ROLES[slot] if slot < len(SLOT_ROLES) else "?",
                "areaShareOfMesh": round(v[0] / max(1e-12, sum(
                    e[0] for e in per_slot.values())), 4),
                "overLimitFractionOfOwnArea": round(v[1] / max(1e-12, v[0]), 4),
                "overLimitShareOfMesh": round(v[1] / max(1e-12, sum(
                    e[0] for e in per_slot.values())), 4),
                "worst": round(v[2], 3),
            } for slot, v in sorted(per_slot.items())},
        "worstTriangle": worst_detail[1],
        "gateIslands": len(gate_islands),
        "gateIslandsBelowMinPixels": below_min,
        "smallestGateIslandsPx": island_px[:6],
        "heroLimit": law.UV_STRETCH_MAX_HERO,
        "distantLimit": law.UV_STRETCH_MAX_DISTANT,
    }


def _heal_degenerate(obj, dist: float = 3.0e-4) -> dict:
    """Collapse sliver geometry that Quadric Edge Collapse leaves behind.

    NOT weld_and_clean: that calls remove_doubles, and mesh_ops._split_uv_seams has
    deliberately duplicated coincident vertices along every seam and material border to
    turn them into decimation boundaries. Welding here would merge those straight back
    and silently undo the seam preservation the LOD chain just paid for.

    What this fixes: a collapse can leave a triangle with ~1e-6 m2 of area, which is
    above law.DEGENERATE_TRIANGLE_AREA_EPS (1e-7) so nothing deletes it, but whose
    smaller singular value is ~0 -- so ANY parameterisation of it reports an enormous
    aspect distortion. Measured: one LOD1 triangle at 181.46 against an outlier ceiling
    of 3.3, while mesh_ops.uv_stretch_stats reported worst 0.561 because it skips
    zero-world-area triangles. The two disagreeing was the tell.
    """
    bm = mesh_ops.bmesh_from_object(obj)
    before_faces = len(bm.faces)
    if bm.faces:
        bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY",
                              ngon_method="BEAUTY")
    bmesh.ops.dissolve_degenerate(bm, dist=dist, edges=bm.edges[:])

    # Aspect, not area. A collapse can leave a LONG thin triangle: one vertex almost on
    # the opposite edge, so no edge is short (dissolve_degenerate sees nothing) and the
    # area still clears an absolute threshold, yet the smaller singular value is ~0 and
    # any parameterisation of it reports a huge aspect distortion. Measured: a LOD1
    # triangle at 264.5 survived both an area filter and a degenerate-edge dissolve.
    # Collapsing its shortest edge heals the neighbourhood instead of punching a hole.
    # Collapse the sliver's shortest edge, but only where the collapse is topologically
    # legal. Two earlier variants failed for opposite reasons: a plain collapse at a
    # threshold aggressive enough to clear the outlier ceiling folded the surface into
    # NON-MANIFOLD configurations (three faces on an edge, which recalc_face_normals
    # cannot orient, reported as inconsistent_winding), and dissolving the middle vertex
    # instead produced n-gons whose re-triangulation created fresh slivers (worst
    # triangle went to 116). The fix is the standard edge-collapse LINK CONDITION: an
    # edge (u,v) may collapse only if the vertices adjacent to both u and v are exactly
    # the vertices opposite that edge. That is precisely the test for "this collapse does
    # not create a non-manifold join", and it lets the threshold go low enough to work.
    collapsed = 0
    skipped_illegal = 0
    for _pass in range(8):
        candidates = []
        for face in bm.faces:
            area = face.calc_area()
            if area <= 1e-12:
                continue
            longest = max(face.edges, key=lambda e: e.calc_length())
            length = longest.calc_length()
            # length^2 / 2A is the ratio of the longest edge to the altitude onto it.
            # Tuned against measurement: 60 missed a 53.3 sliver entirely, and at 42 the
            # pass converged leaving a 41.8 sliver that still mapped to 4.03 UV
            # distortion. The pass converges just under whatever threshold is set, so it
            # must sit below the aspect that produces a breach.
            if (length * length) / (2.0 * area) > 30.0:
                candidates.append(min(face.edges, key=lambda e: e.calc_length()))
        if not candidates:
            break

        claimed = set()
        chosen = []
        for edge in candidates:
            # Boundary edges count. mesh_ops._split_uv_seams deliberately splits every
            # seam and material border before decimating, so a decimated LOD is covered
            # in boundary edges -- and the one triangle that survived every earlier pass
            # at 7.99 distortion was a sliver sitting on exactly such a boundary. For a
            # boundary edge the link condition still applies, with one opposite vertex
            # instead of two.
            if len(edge.link_faces) not in (1, 2):
                continue
            u, v = edge.verts
            ring_u = set()
            for other in u.link_edges:
                ring_u.add(other.other_vert(u).index)
            ring_v = set()
            for other in v.link_edges:
                ring_v.add(other.other_vert(v).index)
            opposite = set()
            for face in edge.link_faces:
                for vertex in face.verts:
                    if vertex is not u and vertex is not v:
                        opposite.add(vertex.index)
            if (ring_u & ring_v) != opposite:
                skipped_illegal += 1
                continue
            # Independent set as well: simultaneous collapses in one neighbourhood can
            # still interact even when each is individually legal.
            ring = {u.index, v.index} | ring_u | ring_v
            if ring & claimed:
                continue
            claimed |= ring
            chosen.append(edge)
        if not chosen:
            break
        bmesh.ops.collapse(bm, edges=chosen, uvs=True)
        collapsed += len(chosen)
        bmesh.ops.dissolve_degenerate(bm, dist=dist, edges=bm.edges[:])
        bm.verts.index_update()
        bm.edges.index_update()
        bm.faces.index_update()

    dead = [f for f in bm.faces if f.calc_area() <= 2.0e-6]
    if dead:
        bmesh.ops.delete(bm, geom=dead, context="FACES")
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh_ops.bmesh_to_object(bm, obj)
    return {"facesBefore": before_faces, "faces": len(obj.data.polygons),
            "sliverEdgesCollapsed": collapsed,
            "collapsesSkippedIllegal": skipped_illegal, "zeroAreaDeleted": len(dead),
            "looseDeleted": len(loose)}


def _fit_uv_into_padding(mesh, padding_uv: float) -> None:
    """Uniformly remap every UV into [padding, 1-padding] squared.

    ``3dmodel.md`` section 6 forbids "UV shells touching atlas border without padding",
    and edge bleed needs that reserve to extrude into. A single uniform scale about the
    lower-left of the padded region keeps every island's shape and every island's
    relative density identical, so no distortion or texel-density gate can regress.
    """
    layer = mesh.uv_layers.active
    if layer is None or not len(layer.data):
        return
    lo_u = lo_v = float("inf")
    hi_u = hi_v = float("-inf")
    for element in layer.data:
        u, v = element.uv
        lo_u = min(lo_u, u)
        hi_u = max(hi_u, u)
        lo_v = min(lo_v, v)
        hi_v = max(hi_v, v)
    span_u = max(1e-9, hi_u - lo_u)
    span_v = max(1e-9, hi_v - lo_v)
    region = max(1e-9, 1.0 - 2.0 * padding_uv)
    scale = min(region / span_u, region / span_v)
    for element in layer.data:
        u, v = element.uv
        element.uv = (padding_uv + (u - lo_u) * scale,
                      padding_uv + (v - lo_v) * scale)


def _collapse_uv_outliers(obj, ceiling: float) -> int:
    """Collapse triangles whose MEASURED UV distortion breaches the outlier ceiling.

    The geometric aspect filter in :func:`_heal_degenerate` is a PROXY for the gate: it
    scores 3D shape, while the gate scores the parameterisation. Measured, the proxy has
    real error in both directions -- slivers at aspect 41.8 that mapped fine, and
    triangles under aspect 30 that still mapped to 8.0. Closing the loop on the gate's
    own formula removes the proxy error, so this uses validate.uv_aspect_distortion
    directly and acts only on what actually breaches.

    Returns the number of edges collapsed. Uses the same edge-collapse link condition as
    _heal_degenerate, so it cannot create the non-manifold joins that produced
    inconsistent_winding failures.
    """
    mesh = obj.data
    data = validate.extract_mesh_data(mesh)
    if not data.uv_layers:
        return 0
    uv0 = data.uv_layers[0][1]

    guilty = set()
    for t in range(data.triangle_count):
        distortion = validate.uv_aspect_distortion(
            data.positions, uv0, data.tri_vertices, data.tri_loops, t)
        if distortion > ceiling:
            guilty.add((data.tri_vertices[t * 3], data.tri_vertices[t * 3 + 1],
                        data.tri_vertices[t * 3 + 2]))
    if not guilty:
        return 0

    bm = mesh_ops.bmesh_from_object(obj)
    bm.verts.ensure_lookup_table()
    claimed = set()
    chosen = []
    for triple in sorted(guilty):
        try:
            verts = [bm.verts[i] for i in triple]
        except (IndexError, ReferenceError):
            continue
        edges = []
        for a in range(3):
            edge = bm.edges.get((verts[a], verts[(a + 1) % 3]))
            if edge is not None:
                edges.append(edge)
        if not edges:
            continue
        edge = min(edges, key=lambda e: e.calc_length())
        if len(edge.link_faces) not in (1, 2):
            continue
        u, v = edge.verts
        ring_u = set(e.other_vert(u).index for e in u.link_edges)
        ring_v = set(e.other_vert(v).index for e in v.link_edges)
        opposite = set()
        for face in edge.link_faces:
            for vertex in face.verts:
                if vertex is not u and vertex is not v:
                    opposite.add(vertex.index)
        if (ring_u & ring_v) != opposite:
            continue
        ring = {u.index, v.index} | ring_u | ring_v
        if ring & claimed:
            continue
        claimed |= ring
        chosen.append(edge)
    if not chosen:
        bm.free()
        return 0
    bmesh.ops.collapse(bm, edges=chosen, uvs=True)
    bmesh.ops.dissolve_degenerate(bm, dist=3.0e-4, edges=bm.edges[:])
    dead = [f for f in bm.faces if f.calc_area() <= 2.0e-6]
    if dead:
        bmesh.ops.delete(bm, geom=dead, context="FACES")
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh_ops.bmesh_to_object(bm, obj)
    return len(chosen)


def _unwrap_and_pack(obj, atlas_size: int, blackbox=None):
    """Conformal unwrap on the authored seams, growth-aligned, density-equalised, packed.

    Route: ``3dmodel.md`` section 6, first approved option -- "Conformal unwrap using
    LSCM/ABF-style angle preservation for unique surfaces". Blender's
    ``MINIMUM_STRETCH`` is the SLIM solver and ``ANGLE_BASED`` is ABF++; the first is
    tried and the second is the fallback, because a solver that fails must not leave
    the mesh with whatever UVs happened to be there.
    """
    mesh = obj.data
    if not mesh.uv_layers:
        mesh.uv_layers.new(name="UVMap")
    mesh.uv_layers.active_index = 0

    mesh_ops._make_sole_active(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    try:
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.select_all(action="SELECT")
        # ANGLE_BASED is ABF++, and the gate measures ANGLE anisotropy
        # (sigma_max/sigma_min of the parameterisation), so an angle-preserving
        # solver is the one that satisfies it. MINIMUM_STRETCH (SLIM) was tried
        # first and measured worse on this metric -- p50 0.307, p95 2.355, max
        # 20.4, with 4339 of 5864 triangles over the 15 percent hero ceiling --
        # because it minimises symmetric-Dirichlet energy, trading angle for area.
        # 3dmodel.md section 6 names the right family explicitly: "Conformal
        # unwrap using LSCM/ABF-style angle preservation".
        method = "ANGLE_BASED"
        result = bpy.ops.uv.unwrap(method=method, margin=0.0,
                                   correct_aspect=True)
        if "FINISHED" not in result:
            method = "CONFORMAL"
            result = bpy.ops.uv.unwrap(method=method, margin=0.0,
                                       correct_aspect=True)
        if "FINISHED" not in result:
            raise RuntimeError("uv.unwrap returned " + str(result) +
                               " for both ANGLE_BASED and CONFORMAL")
        # Equalise texel density across islands BEFORE packing. Section 6 rejects
        # "Texel density mismatch above 20 percent between adjacent hard-surface
        # panels"; the same discipline is what keeps a blade and the stipe it grows
        # from resolving at the same scale.
        bpy.ops.uv.average_islands_scale(scale_uv=False, shear=False)
    finally:
        bpy.ops.object.mode_set(mode="OBJECT")

    islands = _face_islands(mesh)
    rotated = _orient_islands_to_growth(mesh, islands)

    padding_px = law.atlas_padding_for(atlas_size)
    padding_uv = float(padding_px) / float(atlas_size)
    mesh_ops._make_sole_active(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    try:
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.select_all(action="SELECT")
        # rotate=False preserves the growth alignment just applied. margin_method
        # ADD reserves the border in absolute UV units, which is what
        # law.atlas_padding_for expresses.
        bpy.ops.uv.pack_islands(rotate=False, scale=True, merge_overlap=False,
                                margin_method="ADD", margin=padding_uv,
                                shape_method="CONCAVE", pin=False,
                                udim_source="CLOSEST_UDIM")
    finally:
        bpy.ops.object.mode_set(mode="OBJECT")

    # pack_islands(margin_method='ADD') does not guarantee the atlas BORDER reserve --
    # measured, it left islands 0.00658 from the edge against a 0.00781 requirement, and
    # with many islands at LOD1 that produced 50 padding violations. One uniform
    # scale+offset into the padded square is deterministic, preserves conformality and
    # preserves relative texel density, so it fixes the gate without touching quality.
    _fit_uv_into_padding(mesh, padding_uv)

    # Measure with the gate's own formula and act until it is satisfied. Bounded, and
    # each round is a real reduction, so it terminates.
    ceiling = law.UV_STRETCH_MAX_BY_SURFACE[law.SurfaceClass.ORGANIC] *         law.UV_STRETCH_OUTLIER_MULTIPLIER
    outliers_removed = 0
    for _round in range(6):
        removed = _collapse_uv_outliers(obj, ceiling)
        if not removed:
            break
        outliers_removed += removed
        mesh_ops._make_sole_active(obj)
        bpy.ops.object.mode_set(mode="EDIT")
        try:
            bpy.ops.mesh.select_all(action="SELECT")
            bpy.ops.uv.select_all(action="SELECT")
            bpy.ops.uv.unwrap(method=method, margin=0.0, correct_aspect=True)
            bpy.ops.uv.average_islands_scale(scale_uv=False, shear=False)
            bpy.ops.uv.pack_islands(rotate=False, scale=True, merge_overlap=False,
                                    margin_method="ADD", margin=padding_uv,
                                    shape_method="CONCAVE", pin=False,
                                    udim_source="CLOSEST_UDIM")
        finally:
            bpy.ops.object.mode_set(mode="OBJECT")
        _fit_uv_into_padding(mesh, padding_uv)

    # Final passes with NO re-solve. Each re-unwrap above fixes the triangle it was given
    # and can hand back a different one, so the loop chases a moving target and stalls
    # (measured: 7.99 down to 4.56 but never under the ceiling). Collapsing with
    # uvs=True interpolates the existing parameterisation instead of re-deriving it, so
    # the offender is removed without the solver introducing a fresh one.
    for _final in range(4):
        removed = _collapse_uv_outliers(obj, ceiling)
        if not removed:
            break
        outliers_removed += removed
        _fit_uv_into_padding(mesh, padding_uv)

    metrics = _uv_metrics(mesh, atlas_size)
    if metrics is not None:
        metrics["uvOutliersCollapsed"] = outliers_removed
        metrics["solver"] = method
        metrics["islandsOriented"] = rotated
        metrics["seamEdges"] = sum(1 for e in mesh.edges if e.use_seam)
    if blackbox is not None:
        blackbox.record(
            "unwrap_and_pack", vertex_count=len(mesh.vertices),
            triangle_count=mesh_ops.triangle_count(mesh),
            warning="" if metrics is None else
            "solver={s} islands={i} p95={p} max={m}".format(
                s=method, i=metrics["islands"],
                p=metrics["aspectDistortionP95"],
                m=metrics["aspectDistortionMax"]))
    return metrics


# ---------------------------------------------------------------------------
# Stage 7: shared materials  --  3dmodel.md section 6
# ---------------------------------------------------------------------------

def _shared_materials():
    """Three shared SRP-Batcher-friendly materials, one per physical surface role.

    Built before the AO bake rather than after, because Cycles refuses to bake an
    object with no material slot. ``PROCEDURAL_ASSET_PIPELINE.md`` step 7 sits after
    step 6 in the written order, but the dependency is real and the bible's own
    exemption clause covers it: a step may move when the family requires it. The
    material DATA is authored here; nothing about the geometry or the bake changes.

    Slot roles follow section 6 exactly. There is no slot 3 because kelp has no
    emissive organ, and a declared-but-empty slot is itself a validation failure.
    """
    # Pigment comes from the mandatory reference set, opened directly:
    # ``forest_kelp.webp`` reads as dark olive-green stipes against luminous teal
    # water, with saturated AMBER-ORANGE bladder clusters as the colour focal points;
    # ``nice_biome.webp`` shows the same palette logic -- saturated green vegetation
    # plus warm accents against cyan. The albedo here is therefore a healthy mid-tone
    # olive, NOT the near-black the reference silhouettes appear to be: that darkness
    # is backlighting through water, and baking it into base colour would double-darken
    # in engine. 3dmodel.md section 12 forbids exactly that -- darkness must not stand
    # in for material work.
    #
    # Translucency is authored too. Kelp is a thin wet membrane; without transmission a
    # blade reads as painted cardboard, and section 10 asks the final-material shot to
    # prove "wetness, translucency, pigment".
    specs = (
        # role, base colour, roughness, transmission-ish weight, sheen
        ("tissue", (0.052, 0.128, 0.043, 1.0), 0.38, 0.28, 0.35),
        ("basal_collar_scar", (0.058, 0.062, 0.034, 1.0), 0.68, 0.06, 0.04),
        ("holdfast", (0.048, 0.036, 0.026, 1.0), 0.72, 0.0, 0.10),
        ("bladder", (0.402, 0.196, 0.030, 1.0), 0.26, 0.34, 0.45),
    )
    out = []
    for role, colour, roughness, translucency, sheen in specs:
        name = law.NAME_MATERIAL.format(family=law.Family.FLORA.value, role=role)
        existing = bpy.data.materials.get(name)
        if existing is not None:
            bpy.data.materials.remove(existing)
        material = bpy.data.materials.new(name)
        material.use_nodes = True
        principled = material.node_tree.nodes.get("Principled BSDF")
        if principled is not None:
            principled.inputs["Base Color"].default_value = colour
            principled.inputs["Roughness"].default_value = roughness
            for socket, value in (("Metallic", 0.0),
                                  ("Subsurface Weight", translucency),
                                  ("Sheen Weight", sheen),
                                  ("IOR", 1.36)):
                if socket in principled.inputs:
                    principled.inputs[socket].default_value = value
            if "Subsurface Radius" in principled.inputs and translucency > 0.0:
                principled.inputs["Subsurface Radius"].default_value = (
                    0.020, 0.048, 0.016)
        out.append(material)
    return out


# ---------------------------------------------------------------------------
# Stage 6: bakes and vertex colours
# ---------------------------------------------------------------------------

def _read_vertex_floats(mesh, name: str, default: float = 0.0):
    """Per-vertex float attribute, or a default-filled list when it is gone.

    Decimation can drop a custom attribute. Returning a filled list rather than
    raising keeps the failure visible in the manifest instead of crashing the run,
    and the caller reports it.
    """
    attribute = mesh.attributes.get(name)
    count = len(mesh.vertices)
    if attribute is None or attribute.domain != "POINT":
        return [default] * count, False
    values = [default] * count
    attribute.data.foreach_get("value", values)
    return values, True


def _add_ao_substrate(form) -> bpy.types.Object:
    """Temporary seafloor disc so occlusion under the holdfast is real, not implied.

    ``3DMODEL_FLORA_CORAL.md`` section 2 requires low B values "in crevices, under
    plates, root clusters, and branch intersections". A kelp baked in empty space
    has no substrate to occlude against, so its root cluster comes back as bright as
    its canopy -- a technically successful bake carrying no information. The seafloor
    the organism is anchored to is physically present, so including it is accuracy,
    not flattery. It is deleted immediately after the bake and never exported.
    """
    radius = max(0.45, form.boss_radius * 2.0 + 0.30)
    bm = bmesh.new()
    bmesh.ops.create_circle(bm, cap_ends=True, cap_tris=False, segments=24,
                            radius=radius)
    mesh = bpy.data.meshes.new("H8KELP_AOFloorMesh")
    bm.to_mesh(mesh)
    bm.free()
    floor = bpy.data.objects.new("H8KELP_AOFloor", mesh)
    floor.location = Vector((0.0, 0.0, -0.0015))
    bpy.context.scene.collection.objects.link(floor)
    material = bpy.data.materials.get("H8KELP_AOFloorMat")
    if material is None:
        material = bpy.data.materials.new("H8KELP_AOFloorMat")
        material.use_nodes = True
    mesh.materials.append(material)
    return floor


def _harvest_mask(classes, geodesics, cut_distance: float):
    """Channel A: harvest mask.

    ``3DMODEL_FLORA_CORAL.md`` section 2: "A = thickness, damage eligibility,
    harvest mask, or wetness. Meaning must be written into the manifest." Harvest is
    the honest choice for kelp because kelp is a harvestable, and the mask then has a
    gameplay consumer: 1.0 marks tissue that leaves with a cut at ``ANCHOR_Cut``,
    0.0 marks the holdfast and lower stipe that stay rooted.
    """
    out = []
    for index in range(len(classes)):
        tag = classes[index]
        if tag >= CLS_BLADE - 0.5:
            out.append(1.0)
        elif tag >= CLS_STIPE - 0.5:
            out.append(_smoothstep(cut_distance * 0.72, cut_distance * 1.15,
                                   geodesics[index]))
        else:
            out.append(0.0)
    return out


def _author_vertex_colours(obj, form, bb, *, ao_samples: int, ao_distance: float):
    """Compose R/G/B/A per the organic contract, in the one order that works.

    ``vertexcolor``'s module docstring is explicit that the AO bake overwrites ALL
    channels of its target attribute, so occlusion is baked into a scratch layer
    FIRST and composed into B afterwards alongside the analytic R/G/A. Baking last
    would silently erase the sway gradient, and a destroyed gradient is invisible in
    an ordinary render.
    """
    mesh = obj.data

    floor = _add_ao_substrate(form)
    # The core bounds the AO ray itself now (world light settings, confirmed by A/B:
    # mean AO 0.7355 / 0.6624 / 0.6048 at 0.06 / 0.35 / 10 m). Nothing to set here.
    try:
        ao_result = vertexcolor.bake_ambient_occlusion(
            obj, samples=ao_samples, distance=ao_distance, blackbox=bb)
    finally:
        bpy.data.objects.remove(floor, do_unlink=True)
        # Removing an object leaves the view layer's object cache holding a dead
        # slot until it is resynced. The next operator that walks
        # ``view_layer.objects`` -- mesh_ops._make_sole_active does, on every
        # decimation pass -- then hits a None and dies with an AttributeError far
        # from the real cause. Resync here, where the removal happened.
        bpy.context.view_layer.update()
    ao_values = vertexcolor.consume_baked_ao(obj)
    vertexcolor.remove_scratch_attributes(mesh)

    geodesics, geo_ok = _read_vertex_floats(mesh, GEO_LAYER, 0.0)
    classes, cls_ok = _read_vertex_floats(mesh, CLS_LAYER, CLS_BLADE)

    # max_flexible_length is the longest path along the organism, so the farthest
    # frond tip lands at 1.0 and the bible's 192..255 tip band is actually reached.
    max_flexible_length = max(geodesics) if geodesics else 0.0

    # Geodesic, not Euclidean. A frond that droops back toward its own holdfast is
    # far along the stem but physically close to it; straight-line distance would
    # call that tip rigid and the shader would leave it standing still.
    sway = vertexcolor.build_sway_field(
        mesh,
        anchor_position=Vector((0.0, 0.0, 0.0)),
        max_flexible_length=max_flexible_length,
        stiffness_exponent=law.STIFFNESS_EXPONENT_FLEXIBLE_BLADE,
        distances=geodesics,
    )

    # G: kelp is photic tissue. Section 2: "Non-emissive tissue = 0." Written
    # explicitly rather than defaulted so the manifest records an authored zero
    # instead of an omission, and so nobody reads a blank channel as an oversight.
    biolum = [0.0] * len(mesh.vertices)

    cut_distance = form.boss_height * 0.55 + form.height * 0.34
    alpha = _harvest_mask(classes, geodesics, cut_distance)

    report = vertexcolor.write_organic_channels(
        obj, sway=sway, biolum=biolum,
        ao=ao_values if ao_values else None, alpha=alpha,
        alpha_meaning="harvest_mask: 1.0 = frond tissue removed by a cut at "
                      "ANCHOR_Cut, 0.0 = holdfast and lower stipe that stay rooted",
        blackbox=bb)

    tips = [sway.values[i] for i in range(len(classes))
            if classes[i] >= CLS_BLADE - 0.5] or [0.0]
    roots = [sway.values[i] for i in range(len(classes))
             if classes[i] <= CLS_FINGER + 0.5] or [0.0]

    # Read the composed attribute straight off the mesh. The rendered channel tiles are
    # measured through preview.measure_channel_png, and an instrument fault there would
    # be indistinguishable from a generator fault; these numbers come from the vertex
    # data itself, so the two can be compared.
    direct = {}
    attribute = mesh.color_attributes.get(law.VCOL_ATTRIBUTE_NAME)
    if attribute is not None:
        for index, channel_name in enumerate(law.ORGANIC_VCOL):
            values = [element.color[index] for element in attribute.data]
            if values:
                direct[channel_name] = {
                    "min": round(min(values), 5),
                    "max": round(max(values), 5),
                    "mean": round(sum(values) / len(values), 5),
                    "elements": len(values),
                    "domain": attribute.domain,
                }

    report.update({
        "directAttributeRead": direct,
        "geodesicAttributeSurvived": geo_ok,
        "classAttributeSurvived": cls_ok,
        "maxFlexibleLengthM": round(max_flexible_length, 5),
        "swayRelativeSpread": round(sway.relative_spread, 5),
        "swayTipMax255": int(round(max(tips) * 255.0)),
        "swayRootMax255": int(round(max(roots) * 255.0)),
        "swayAnchorMin255": int(round(sway.min_value * 255.0)),
        "aoBaked": ao_result.baked,
        "aoHasContrast": ao_result.has_contrast,
        "aoMin": round(ao_result.min_value, 5),
        "aoMax": round(ao_result.max_value, 5),
        "aoMean": round(ao_result.mean_value, 5),
        "aoSamples": ao_result.samples,
        "aoDistanceM": ao_distance,
        "aoReason": ao_result.reason,
        "biolumPolicy": "authored 0 everywhere; photic-zone kelp has no emissive "
                        "organ (3DMODEL_FLORA_CORAL.md section 2)",
    })
    # CLS and PART have done their work; GEO stays alive because the per-LOD reunwrap
    # hook needs it to orient each island so +V still runs root-to-tip after
    # decimation. It is removed from every LOD mesh just before export.
    for attribute_name in (CLS_LAYER, PART_LAYER):
        attribute = mesh.attributes.get(attribute_name)
        if attribute is not None:
            mesh.attributes.remove(attribute)
    return report, sway, ao_result


# ---------------------------------------------------------------------------
# Interaction anchors  --  PROCEDURAL_ASSET_PIPELINE.md
# ---------------------------------------------------------------------------

def _build_anchors(form, name: str):
    """Serialised interaction anchors as empties, named from ``law``.

    "Runtime searching for interaction anchors is rejected; anchors must be
    serialized". Empties export as FBX transforms, so Unity receives them as child
    objects instead of a runtime lookup. Only the verbs that actually apply to a
    kelp are emitted -- inventing ANCHOR_Weld on a plant would be noise.
    """
    wanted = {
        "ANCHOR_Cut": Vector((0.0, 0.0, form.boss_height * 0.55 + form.height * 0.42)),
        "ANCHOR_Loot": Vector((0.0, 0.0, form.boss_height * 0.55 + form.height * 0.60)),
        "ANCHOR_Scan": Vector((0.0, 0.0, form.boss_height * 0.55 + form.height * 0.72)),
    }
    out = []
    for anchor_name, position in sorted(wanted.items()):
        if anchor_name not in law.INTERACTION_ANCHORS:
            raise ValueError("anchor '" + anchor_name + "' is not in "
                             "law.INTERACTION_ANCHORS")
        empty = bpy.data.objects.new(anchor_name + "_" + name, None)
        empty.empty_display_type = "PLAIN_AXES"
        empty.empty_display_size = 0.06
        # Placement pivot law: the anchor socket sits on the stipe axis above the
        # holdfast base, and the holdfast base is the object origin.
        empty.location = position
        bpy.context.scene.collection.objects.link(empty)
        out.append((anchor_name, empty))
    return out


# ---------------------------------------------------------------------------
# Scene reset
# ---------------------------------------------------------------------------

def _material_slot_anchors(obj):
    """Per-slot centroid at LOD0, so an emptied slot can be re-tagged sensibly later."""
    mesh = obj.data
    sums = {}
    for polygon in mesh.polygons:
        slot = polygon.material_index
        entry = sums.setdefault(slot, [Vector((0.0, 0.0, 0.0)), 0])
        entry[0] += polygon.center
        entry[1] += 1
    return {slot: (total / float(count)) for slot, (total, count) in sums.items()
            if count}


def _preserve_material_slots(obj, anchors) -> dict:
    """Re-tag the nearest surviving face to any slot decimation emptied.

    Quadric collapse has no notion of a submesh contract, so a small slot can lose its
    last triangle at LOD2 -- measured on this asset: slot 1 of 4 empty at 288 triangles
    in one of six runs, which the validator rejects as submesh_empty_declared_slot.
    3dmodel.md section 6 requires LOD2 to "Keep vertex color R/G/B semantics" and the
    material slot declaration to match the submesh count, so the honest repair is to
    keep the slot alive at its own location rather than silently dropping a material
    role partway down the chain. One face per emptied slot, chosen by distance to that
    slot's LOD0 centroid, so the material stays where it belongs on the organism.
    """
    mesh = obj.data
    used = set(polygon.material_index for polygon in mesh.polygons)
    declared = len(mesh.materials)
    repaired = {}
    for slot in range(declared):
        if slot in used:
            continue
        anchor = anchors.get(slot)
        if anchor is None or not mesh.polygons:
            continue
        best = None
        best_distance = None
        for polygon in mesh.polygons:
            # Never cannibalise a slot that is itself down to its last face.
            if sum(1 for q in mesh.polygons
                   if q.material_index == polygon.material_index) <= 1:
                continue
            distance = (polygon.center - anchor).length
            if best_distance is None or distance < best_distance:
                best_distance = distance
                best = polygon
        if best is not None:
            best.material_index = slot
            repaired[slot] = round(best_distance, 5)
    return repaired


def _reset_scene() -> None:
    """Empty the scene between variants so one asset cannot contaminate the next.

    ``--factory-startup`` gives a clean file for the first variant only. Without
    this, variant 2 would be framed, baked and rendered with variant 1 still in the
    scene, and every AO value and every preview would be wrong in a way that still
    looks like a successful run.
    """
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)
    for material in list(bpy.data.materials):
        bpy.data.materials.remove(material)
    for image in list(bpy.data.images):
        if image.users == 0:
            bpy.data.images.remove(image)


# ---------------------------------------------------------------------------
# One asset, all thirteen stages in the order the pipeline bible fixes
# ---------------------------------------------------------------------------

def generate_kelp(*, seed: int, quality: float, out_dir: str,
                  name: str, want_preview: bool, preview_resolution: int,
                  ao_samples: int, atlas_size: int,
                  ao_distance_override: float = 0.0) -> dict:
    """Build, validate and save one kelp asset. Returns its manifest dict."""
    quality = law.saturate(quality)
    run_tag = "{n}_s{s}_q{q}".format(n=name, s=seed, q=("%.2f" % quality).replace(".", ""))
    bb = BlackBox("KelpGenerator", run_tag)
    started = time.time()

    _reset_scene()

    # -- 1. deterministic manifest / source references --------------------
    identity = law.GeneratorIdentity(
        generator=GENERATOR_NAME, generator_version=GENERATOR_VERSION,
        seed=seed, quality_weight=quality, family=law.Family.FLORA,
        scale_meters=0.0, camera_distance_class="near_interactive",
        platform_lane="compact_to_ultra_continuous",
        source_references=("3DMODEL_FLORA_CORAL.md#3", "3dmodel.md#5",
                           "PROCEDURAL_ASSET_PIPELINE.md#generation-order"),
    )
    bb.record("identity", seed=seed, family=law.Family.FLORA.value)

    # -- 2. shape grammar -------------------------------------------------
    form = KelpForm(seed, quality)
    bb.record("shape_grammar", seed=seed, family=law.Family.FLORA.value,
              warning="h={h:.2f}m blades={b} fingers={f}".format(
                  h=form.height, b=form.blade_count, f=form.finger_count))

    # -- 3./4. high-detail geometry + family topology rules ---------------
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new("UVMap")
    geo_layer = bm.verts.layers.float.new(GEO_LAYER)
    cls_layer = bm.verts.layers.float.new(CLS_LAYER)
    part_layer = bm.faces.layers.int.new(PART_LAYER)
    layers = (uv_layer, geo_layer, cls_layer, part_layer)

    holdfast_parts, next_part = _build_holdfast(bm, layers, form, quality, 0)

    stipe_rows = _qi(12, 26, quality)
    stipe_segments = _qi(7, 12, quality)
    stipe_part = next_part
    stipe_info, stipe_points, stipe_lengths = _build_stipe(
        bm, layers, form, stipe_rows, stipe_segments, stipe_part)
    next_part += 1

    blade_parts, next_part, blade_records, blade_stats = _build_blades(
        bm, layers, form, quality, stipe_points, stipe_lengths, next_part)
    # Three islands per swept part now: the tube opened along its lengthwise seam, plus
    # the two caps cut free along their boundary rings.
    expected_islands = 3 * (len(holdfast_parts) + 1 + len(blade_parts))

    raw_faces = len(bm.faces)
    raw_verts = len(bm.verts)
    bb.record("geometry_built", vertex_count=raw_verts, triangle_count=raw_faces,
              family=law.Family.FLORA.value)

    # -- 4. topology rules: weld -------------------------------------------
    weld_stats = mesh_ops.weld_and_clean(bm, merge_distance=1e-4, blackbox=bb)
    seams_marked = sum(1 for edge in bm.edges if edge.seam)

    mesh_name = law.NAME_MESH.format(family=law.Family.FLORA.value, name=name, lod=0)
    mesh = bpy.data.meshes.new(mesh_name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(mesh_name, mesh)
    bpy.context.scene.collection.objects.link(obj)

    materials = _shared_materials()
    for material in materials:
        mesh.materials.append(material)

    # Safety net only. This generator evaluates its analytic surface directly at the
    # density it chose, so displacement never has to fight an already-coarse mesh --
    # the mush case reduce_to_budget exists for. It still runs, before the UV/vcol
    # stages that would otherwise be thrown away, so a high-quality variant that
    # overshoots is trimmed here rather than failing validation later.
    before_reduce = mesh_ops.triangle_count(mesh)
    after_reduce = mesh_ops.reduce_to_budget(
        obj, family=law.Family.FLORA, lod_index=0, blackbox=bb)

    # Quadric collapse can leave zero-area faces behind it. weld_and_clean already ran
    # once, BEFORE the decimation, so a second pass here is not redundant: without it
    # those slivers reach the UV metric as infinities and the validator as
    # degenerate_triangle failures, and they are indistinguishable from an authoring
    # bug in the report.
    post_clean = _heal_degenerate(obj)

    # -- 5. UVs and material IDs ------------------------------------------
    # After reduce_to_budget on purpose: unwrapping first and decimating second would
    # hand the solver's result to a collapse pass that interpolates across it.
    atlas_report = _unwrap_and_pack(obj, atlas_size, blackbox=bb)
    if atlas_report is None:
        raise RuntimeError("UV stage produced no UV layer to measure")
    # Printed BEFORE the validation gate so an abort still shows the UV facts that
    # caused it, rather than only the gate names.
    print("  [uv] solver={s} islands={i}/{e} oriented={o} seams={sm} "
          "density={dmin}..{dmax}px/m mismatch={mm} distortion p50={p50} "
          "p95={p95} max={mx} overHero={oh}/{n} zeroArea={z}".format(
              s=atlas_report["solver"], i=atlas_report["islands"],
              e=expected_islands, o=atlas_report["islandsOriented"],
              sm=atlas_report["seamEdges"],
              dmin=atlas_report["texelDensityPxPerMetreMin"],
              dmax=atlas_report["texelDensityPxPerMetreMax"],
              mm=atlas_report["texelDensityMismatchFraction"],
              p50=atlas_report["aspectDistortionP50"],
              p95=atlas_report["aspectDistortionP95"],
              mx=atlas_report["aspectDistortionMax"],
              oh=atlas_report["trianglesOverHeroLimit"],
              n=atlas_report["trianglesMeasured"],
              z=atlas_report["zeroAreaUvTriangles"]))
    for slot, info in sorted(atlas_report["areaOverLimitBySlot"].items()):
        print("  [uv] slot{s} {r:<20} area={a:.1%} overLimit(ownArea)={o:.1%} "
              "overLimit(mesh)={m:.1%} worst={w}".format(
                  s=slot, r=info["role"], a=info["areaShareOfMesh"],
                  o=info["overLimitFractionOfOwnArea"],
                  m=info["overLimitShareOfMesh"], w=info["worst"]))
    print("  [uv] worst triangle: {w}".format(w=atlas_report["worstTriangle"]))
    print("  [uv] zeroUvBelowEps={a} degenerate3d={b} offenders={c}".format(
        a=atlas_report["zeroAreaUvBelowEpsilon"],
        b=atlas_report["degenerate3dTriangles"],
        c=atlas_report["smallestOffenders"]))
    # Assert on MEASURED COUNTS, not on the operators' return values: uv.unwrap,
    # average_islands_scale and pack_islands all report FINISHED on meshes they did
    # nothing useful to.
    # The guard exists to catch a shell the seam chain FAILED TO OPEN, which shows up as
    # islands far below the expected count -- one closed shell means the solver had to
    # invent its own cut. An exact match is the wrong test: the sliver-collapse pass can
    # legitimately consume a whole cap, and each part then contributes two islands
    # instead of three. A floor of two per part distinguishes those two situations.
    atlas_report["islandsExpected"] = expected_islands
    minimum_islands = 2 * (expected_islands // 3)
    if atlas_report["islands"] < minimum_islands:
        raise RuntimeError(
            "unwrap produced {a} islands against {e} expected and a floor of {m}; a "
            "swept shell was not opened by its seam chain".format(
                a=atlas_report["islands"], e=expected_islands, m=minimum_islands))
    # Recorded, not raised. These are NOT authoring degeneracies: measured, all of them
    # have healthy 3D area (0.3-0.9 cm2) and only their UV collapses, which is the
    # conformal solver's scale singularity at the pole of a cut closed shell -- an
    # angle-preserving map trades AREA, and a sphere cut pole to pole concentrates that
    # trade at the two cut endpoints. degenerate3dTriangles is the counter that would
    # mean a real geometry bug, and it reads 0. Raising here would block the proof
    # renders and the failure report, which are the artefacts the lead needs to judge
    # the asset; the validator still enforces the real gate and still aborts the save.
    if atlas_report["degenerate3dTriangles"] > 0:
        raise RuntimeError(
            "{n} triangles are degenerate in 3D, not just in UV".format(
                n=atlas_report["degenerate3dTriangles"]))

    # -- 6a. shading basis, then bakes ------------------------------------
    # The core owns the shading basis. Its ShadingResult is asserted rather than
    # trusted: a silent no-op here ships a flat-shaded asset, and flat shading
    # destroys the specular response the whole normal/bevel pass exists to create.
    shading = mesh_ops.apply_shading_basis(obj, blackbox=bb)
    if getattr(shading, "smooth_polygons", 0) <= 0:
        raise RuntimeError(
            "apply_shading_basis reported {p} smooth polygons; the asset would ship "
            "flat shaded".format(p=getattr(shading, "smooth_polygons", None)))
    # apply_shading_basis leaves the Smooth-by-Angle modifier live. Applying it makes
    # the datablock the validator inspects identical to what the exporter writes;
    # otherwise validation reads pre-modifier normals and Unity gets post-modifier
    # ones, and the two disagree with nothing to show it.
    for modifier in list(obj.modifiers):
        mesh_ops._make_sole_active(obj)
        bpy.ops.object.modifier_apply(modifier=modifier.name)

    # -- 6b. vertex colour channels ---------------------------------------
    vcol_report, sway, ao_result = _author_vertex_colours(
        obj, form, bb, ao_samples=ao_samples,
        ao_distance=(ao_distance_override if ao_distance_override > 0.0
                     else max(0.12, form.boss_radius * 2.4 + 0.14)))

    bounds_min, bounds_max = mesh_ops.local_bounds(obj)
    identity.scale_meters = round(max(bounds_max.z - bounds_min.z,
                                      bounds_max.x - bounds_min.x,
                                      bounds_max.y - bounds_min.y), 5)

    # -- 8. LOD chain -----------------------------------------------------
    # Re-solve UVs per level. Measured on this asset before the hook existed: LOD0 sat
    # at p95 0.98 while LOD1's worst triangle reached 7610 and LOD2's 262 -- Decimate/
    # COLLAPSE carries no UV term in its collapse cost and exposes no flag to add one,
    # so the parameterisation is destroyed while the triangle budget is still met. The
    # hook is family knowledge on purpose: the seam placement and the root-to-tip V
    # orientation below are kelp rules, not a shared helper's.
    slot_anchors = _material_slot_anchors(obj)
    lod_uv = {}

    def _reunwrap(level_obj, lod_index):
        # Heal BEFORE solving: unwrapping a sliver produces a degenerate island, and no
        # amount of re-solving fixes geometry that has no area to parameterise.
        healed = _heal_degenerate(level_obj)
        lod_uv[lod_index] = _unwrap_and_pack(level_obj, atlas_size, blackbox=bb)
        lod_uv[lod_index]["healed"] = healed
        lod_uv[lod_index]["slotsRepaired"] = _preserve_material_slots(
            level_obj, slot_anchors)

    # preserve_seams stays TRUE. Turning it off was tried and measured worse, not better:
    # without the seam/material-border split, decimation welds across those borders and
    # LOD2 came back with 32.8% of its area over the stretch limit plus 16 zero-length
    # vertex normals, against a clean pass with splitting on. The boundary constraints
    # cost a little UV quality at LOD1 and buy correct geometry at LOD2.
    lods = mesh_ops.build_lod_chain(
        obj, family=law.Family.FLORA, name=name, quality_weight=quality,
        levels=3, preserve_seams=True, blackbox=bb, reunwrap=_reunwrap)
    lod_uv[0] = dict(atlas_report)
    lod_uv[0]["slotsRepaired"] = _preserve_material_slots(obj, slot_anchors)
    for level in lods:
        stats = mesh_ops.uv_stretch_stats(level.obj)
        lod_uv.setdefault(level.index, {})["stretchStats"] = stats
        print("  [uv] LOD{i} area-weighted worst={w:.4f} p95={p:.4f} "
              "mean={m:.4f} tris={t} slotsRepaired={s}".format(
                  i=level.index, w=stats["worst"], p=stats["p95"],
                  m=stats["mean"], t=stats["triangles"],
                  s=lod_uv.get(level.index, {}).get("slotsRepaired", {})))
    # GEO has served the reunwrap orientation; strip it from every LOD before export.
    for level in lods:
        attribute = level.obj.data.attributes.get(GEO_LAYER)
        if attribute is not None:
            level.obj.data.attributes.remove(attribute)

    # -- 9. collision proxies ---------------------------------------------
    collider = mesh_ops.make_convex_collider(
        obj, family=law.Family.FLORA, name=name, blackbox=bb)

    # -- 10. package assembly ---------------------------------------------
    anchors = _build_anchors(form, name)

    # -- 11. validation ---------------------------------------------------
    reports = []
    for level in lods:
        reports.append(validate.validate_mesh(
            level.obj.data, family=law.Family.FLORA, lod_index=level.index,
            surface_class=law.SurfaceClass.ORGANIC, blackbox=bb,
            # LOD0 is the near/interactive silhouette, so it takes the 15% hero
            # stretch ceiling. LOD1/LOD2 exist only at distance, which is exactly
            # the case section 6 allows 25% for.
            hero=(level.index == 0),
            atlas_size=atlas_size))
    chain_failures = validate.validate_lod_chain(
        reports, family=law.Family.FLORA, blackbox=bb)

    collider_failures = []
    if collider.obj is not None:
        collider_failures = validate.validate_collider(
            collider.obj.data, family=law.Family.FLORA, blackbox=bb,
            lod0_mesh=lods[0].obj.data,
            visual_meshes=tuple(level.obj.data for level in lods[1:]))

    # -- 13a. proof artefacts, rendered BEFORE the save gate ---------------
    # PROCEDURAL_ASSET_PIPELINE.md puts proof last, and for a PASSING asset it is.
    # Rendering before the gate as well is what makes a REJECTED asset diagnosable:
    # the save must abort on failure ("On validation failure the save is aborted"),
    # but the same document requires the generator to "write a failure report", and a
    # failure report for a visual asset with no image in it cannot be judged. The
    # renders read the mesh; they never write an asset.
    proof = {}
    if want_preview:
        proof = _render_proof(lods[0].obj, name=run_tag,
                              resolution=preview_resolution)

    gate_failures = validate._collect_failures(
        [reports, chain_failures, collider_failures])
    passed = not gate_failures

    # -- 12. save ---------------------------------------------------------
    os.makedirs(out_dir, exist_ok=True)
    export_objects = [level.obj for level in lods] + [empty for _n, empty in anchors]
    fbx_path = os.path.join(out_dir, "MESH_{f}_{n}.fbx".format(
        f=law.Family.FLORA.value, n=name))
    blend_path = os.path.join(out_dir, "SRC_{f}_{n}.blend".format(
        f=law.Family.FLORA.value, n=name))
    if not passed:
        # 3dmodel.md section 10: "Failure aborts save." No FBX, no .blend.
        fbx_path = ""
        blend_path = ""
    view_layer = bpy.context.view_layer
    for other in view_layer.objects:
        other.select_set(False)
    for target in export_objects:
        target.select_set(True)
    view_layer.objects.active = lods[0].obj
    if passed:
        bpy.ops.export_scene.fbx(
            filepath=fbx_path, use_selection=True, apply_unit_scale=True,
            global_scale=1.0, apply_scale_options="FBX_SCALE_NONE",
            axis_forward="-Z", axis_up="Y", object_types={"MESH", "EMPTY"},
            use_mesh_modifiers=False, mesh_smooth_type="FACE", use_tspace=True,
            # The exporter defaults to SRGB, which would gamma-encode masks that are
            # DATA, not colour: a sway of 0.5 would arrive in Unity as 0.74. LINEAR
            # passes the authored 0..1 numbers through untouched.
            colors_type="LINEAR", path_mode="STRIP", use_triangles=True,
            bake_anim=False)
        bpy.ops.wm.save_as_mainfile(filepath=blend_path, copy=True)

    manifest = {
        "identity": identity.as_dict(),
        "assetName": name,
        "family": law.Family.FLORA.value,
        "surfaceClass": law.SurfaceClass.ORGANIC.value,
        "biomeDepthRoute": "photic shallows / kelp bed, seafloor-anchored",
        "growthAlgorithm": "closed swept ring surfaces along parametric growth "
                           "curves; holdfast boss + haptera fingers, ribbed "
                           "tapering stipe, flat-lens blade shells",
        "materialFamily": ATLAS_FAMILY,
        "structures": {
            "holdfastFingers": form.finger_count,
            "holdfastBossHeightM": round(form.boss_height, 4),
            "stipeRings": stipe_rows,
            "stipeRadialSegments": stipe_segments,
            "stipeRadiusBaseM": round(form.stipe_radius_base, 5),
            "stipeRadiusTopM": round(form.stipe_radius_top, 5),
            "stipeRibCount": form.rib_count,
            "stipePneumatocysts": len(form.swellings),
            "bladeCount": form.blade_count,
            "bladeCanopy": form.canopy_blades,
            "bladeBasal": form.basal_blades,
            "bladeShell": "closed thin shell, edge rim is the narrow end of the "
                          "lens cross-section (no zero-thickness sheet)",
            "bladeDetail": blade_stats,
            "blades": blade_records,
            "anchorSocket": "object origin at holdfast base (0,0,0); sway measured "
                            "from it",
            "flowFacingAsymmetry": {
                "currentDirXY": [round(form.current.x, 4), round(form.current.y, 4)],
                "currentStrength": round(form.current_strength, 4),
                "effects": "stipe bend, per-blade downstream sweep, lee-side blades "
                           "longer/wider, holdfast splayed wider upstream",
            },
        },
        "topology": {
            "rawFacesBeforeWeld": raw_faces,
            "rawVertsBeforeWeld": raw_verts,
            "weld": weld_stats,
            "uvSeamEdgesMarked": seams_marked,
            "stipeSeam": stipe_info,
            "trianglesBeforeBudgetReduce": before_reduce,
            "trianglesAfterBudgetReduce": after_reduce,
            "postDecimationClean": post_clean,
            "budgetReduceFired": after_reduce < before_reduce,
            "branchUnion": "stipe base and blade sheaths are hidden unions beneath "
                           "the holdfast boss and the blade root embed; no "
                           "coplanar intersecting tubes (3DMODEL_FLORA_CORAL.md "
                           "section 3)",
        },
        "uv": atlas_report,
        "texelDensityTargets": {
            "heroHarvestable": law.TEXEL_DENSITY_HERO_FLORA,
            "commonInstanced": law.TEXEL_DENSITY_COMMON_FLORA,
            "achievedMean": atlas_report["texelDensityPxPerMetreMean"],
            "achievedMin": atlas_report["texelDensityPxPerMetreMin"],
            "meetsHero": atlas_report["texelDensityPxPerMetreMin"] >=
                         law.TEXEL_DENSITY_HERO_FLORA,
            "meetsCommon": atlas_report["texelDensityPxPerMetreMin"] >=
                           law.TEXEL_DENSITY_COMMON_FLORA,
        },
        "uvRoutes": {
            "blades": "lengthwise: V from root to tip along the blade curve, U "
                      "across the sheet from one margin to the other, continuing "
                      "around the rim onto the underside (3DMODEL_FLORA_CORAL.md "
                      "section 5)",
            "stipeAndFingers": "cylindrical unwrap, arc-length in both axes, single "
                               "seam column placed on the rear/lee side",
            "overlapNote": "upper and lower blade faces occupy the same island but "
                           "distinct U ranges, so no island overlaps another; no "
                           "unique-baked texture is produced, AO lives in vertex "
                           "colour B",
        },
        "materialSlots": [
            {"slot": law.MATERIAL_SLOT_PRIMARY, "role": SLOT_ROLES[0],
             "material": materials[0].name},
            {"slot": law.MATERIAL_SLOT_CUT_EDGE, "role": SLOT_ROLES[1],
             "material": materials[1].name},
            {"slot": law.MATERIAL_SLOT_TRIM, "role": SLOT_ROLES[2],
             "material": materials[2].name},
            {"slot": law.MATERIAL_SLOT_EMISSIVE, "role": SLOT_ROLES[3],
             "material": materials[3].name,
             "note": "non-emissive amber bladder pigment under the section 6 "
                     "'details' clause; vertex colour G stays 0"},
        ],
        "vertexColour": vcol_report,
        "vertexColourContract": list(law.ORGANIC_VCOL),
        "lods": [
            {"lod": level.index, "mesh": level.obj.data.name,
             "triangles": level.triangles, "budget": level.budget,
             "withinBudget": level.within_budget,
             "vertices": len(level.obj.data.vertices),
             "simplification": "authored analytic surface" if level.index == 0
                               else "quadric edge collapse with seam/material "
                                    "border preservation"}
            for level in lods
        ],
        "lodUv": {str(k): v for k, v in sorted(lod_uv.items())},
        "lodChainFailures": [str(f) for f in chain_failures],
        "collision": {
            "kind": collider.kind,
            "triangles": collider.triangles,
            "justification": collider.reason,
            "interactionProxy": "harvest/cut interaction is represented by the "
                                "serialised ANCHOR_* empties; a trigger capsule at "
                                "the root is the runtime owner's call per "
                                "3DMODEL_FLORA_CORAL.md section 7",
        },
        "anchors": [{"name": anchor_name,
                     "position": [round(v, 5) for v in tuple(empty.location)]}
                    for anchor_name, empty in anchors],
        "bounds": {"min": [round(v, 5) for v in tuple(bounds_min)],
                   "max": [round(v, 5) for v in tuple(bounds_max)]},
        "validation": {
            "validatorVersion": validate.VALIDATOR_VERSION,
            "meshes": [{"mesh": r.name, "lod": r.lod_index, "passed": r.passed,
                        "triangles": r.triangle_count, "vertices": r.vertex_count,
                        "submeshes": r.submesh_count, "digest": r.digest,
                        "failures": [str(f) for f in r.failures],
                        "notEnforced": [str(w) for w in r.warnings]}
                       for r in reports],
            "allPassed": passed,
            "gateFailures": [str(f) for f in gate_failures],
            "colliderFailures": [str(f) for f in collider_failures],
        },
        "files": {"fbx": os.path.basename(fbx_path) if fbx_path else
                         "NOT WRITTEN: validation aborted the save",
                  "blend": os.path.basename(blend_path) if blend_path else
                           "NOT WRITTEN: validation aborted the save"},
        "proof": proof,
        "blackBox": {"stepsRecorded": bb.total_recorded,
                     "lastAcceptedStage": bb.last_accepted_stage(),
                     "firstInvalidStage": bb.first_invalid_stage()},
        "prefabBoundary": "PF_/GEN_ prefab, LODGroup and Unity import are NOT "
                          "produced here: only Unity may author .prefab assets "
                          "(AGENTS.md Evidence Law). This package is the FBX + "
                          "manifest the Unity-side assembler consumes.",
        "generationSeconds": round(time.time() - started, 2),
    }

    # A rejected asset gets a REJECTED_-prefixed failure report, never a manifest
    # that a later stage could mistake for a shippable package.
    manifest_path = os.path.join(out_dir, ("" if passed else "REJECTED_") +
                                 law.NAME_MANIFEST.format(
                                     family=law.Family.FLORA.value, name=name) +
                                 ".json")
    with open(manifest_path, "w", encoding="utf-8") as handle:
        json.dump(manifest, handle, indent=1, sort_keys=False)
    manifest["files"]["manifest"] = os.path.basename(manifest_path)
    manifest["outDir"] = out_dir
    if not passed:
        for failure in gate_failures:
            bb.note_invalid("gate:" + failure.gate, failure.gate,
                            "x{c} {d}".format(c=failure.count, d=failure.detail))
        manifest["blackBox"]["dump"] = bb.dump(
            "kelp pre-save gate rejected seed={s} quality={q}".format(
                s=seed, q=quality))
    return manifest


# ---------------------------------------------------------------------------
# Proof renders  --  3DMODEL_FLORA_CORAL.md section 10
# ---------------------------------------------------------------------------

def _render_proof(obj, *, name: str, resolution: int) -> dict:
    """Flat, studio and per-channel sheets, plus real pixel measurements.

    Section 10 requires a "flat-material screenshot proving the silhouette is
    biological before texture detail", and the channel sheet is the only artefact
    that can disprove the section 8 gate "Root vertices sway as much as tips".
    ``measure_channel_png`` turns each tile into numbers so the claim is pixels, not
    an impression -- ``AGENTS.md``: "the existence of a PNG proves nothing".
    """
    out = {}
    # Hide every other mesh first. The LOD1/LOD2 objects sit at the SAME origin as
    # LOD0, and preview._apply_override_material only swaps slots on the objects it is
    # given -- so the siblings render with their own materials right through the
    # subject. Measured consequence before this fix: the G tile reported max=1.0 and
    # mean=0.219 on a channel authored to 0.0 everywhere, because the amber bladder
    # material on the overlapping LOD1/LOD2 was being sampled. Every channel number was
    # describing the wrong geometry, and all four tiles still looked plausible.
    hidden = []
    for other in bpy.data.objects:
        if other is obj or other.type != "MESH" or other.hide_render:
            continue
        other.hide_render = True
        hidden.append(other)
    try:
        return _render_proof_isolated(obj, name=name, resolution=resolution)
    finally:
        for other in hidden:
            other.hide_render = False


def _render_proof_isolated(obj, *, name: str, resolution: int) -> dict:
    """Render the sheets with the subject already isolated by the caller."""
    out = {}
    flat = preview.render_contact_sheet(obj, preview.PreviewSpec(
        name=name + "_flat", resolution=resolution, mode="flat",
        views=("front", "three_quarter", "side", "low"),
        surface_class=law.SurfaceClass.ORGANIC))
    studio = preview.render_contact_sheet(obj, preview.PreviewSpec(
        name=name + "_studio", resolution=resolution, mode="studio",
        views=("front", "three_quarter", "side", "top"),
        surface_class=law.SurfaceClass.ORGANIC))
    # mode="material" keeps the asset's OWN materials, which is the only shot that can
    # show pigment. Section 10 requires both: a flat shot proving the silhouette is
    # biological before texture detail, and a final-material shot proving "wetness,
    # translucency, pigment or bioluminescence, scars, pores, and biome-correct
    # coloration support the organism". A grey render satisfies the first only.
    material = preview.render_contact_sheet(obj, preview.PreviewSpec(
        name=name + "_material", resolution=resolution, mode="material",
        views=("three_quarter", "front", "side", "low"),
        surface_class=law.SurfaceClass.ORGANIC))
    channels = preview.render_channel_sheet(obj, preview.PreviewSpec(
        name=name + "_chan", resolution=resolution,
        surface_class=law.SurfaceClass.ORGANIC))

    measurements = []
    for index, tile in enumerate(channels.tile_paths):
        stats = preview.measure_channel_png(tile)
        measurements.append({
            "channel": law.ORGANIC_VCOL[index],
            "tile": os.path.basename(tile),
            "min": round(stats.min_value, 5),
            "max": round(stats.max_value, 5),
            "mean": round(stats.mean_value, 5),
            "hasGradient": stats.has_gradient,
            "subjectVisible": stats.subject_visible,
            "coverage": round(stats.coverage_fraction, 5),
        })

    out["flatSheet"] = flat.sheet_path
    out["studioSheet"] = studio.sheet_path
    out["materialSheet"] = material.sheet_path
    out["channelSheet"] = channels.sheet_path
    out["channelTiles"] = list(channels.tile_paths)
    out["channelMeasurements"] = measurements
    return out


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def _default_out_dir() -> str:
    """Relative to the repo root, never an absolute developer path.

    ``AGENTS.md`` ``[RULE] Relative Path Requirement``: "Hardcoding absolute
    developer paths ... is strictly banned. All screenshot, log, config, and data
    directories must be resolved relatively from the project root."

    The package lands under ``Docs/AgentLogs`` rather than ``Assets`` because only
    Unity may author asset-database entries; the Unity-side assembler moves and
    imports the FBX.
    """
    return os.path.join(law.project_root(), "Docs", "AgentLogs", "ForgeKelp")


def _parse_args(argv):
    parser = argparse.ArgumentParser(
        prog="kelp.py",
        description="HECTON-8 kelp/seaweed generator (3DMODEL_FLORA_CORAL.md s3)")
    parser.add_argument("--seed", type=int, default=4021,
                        help="deterministic seed; variation is a named seed, "
                             "never hidden chance")
    parser.add_argument("--quality", type=float, default=1.0,
                        help="GlobalQualityWeight, continuous 0..1")
    parser.add_argument("--variants", type=int, default=1,
                        help="number of assets; variant N uses seed+N")
    parser.add_argument("--out", type=str, default="",
                        help="output directory (default Docs/AgentLogs/ForgeKelp)")
    parser.add_argument("--preview", dest="preview", action="store_true",
                        default=True, help="render proof sheets (default)")
    parser.add_argument("--no-preview", dest="preview", action="store_false",
                        help="skip proof renders")
    parser.add_argument("--preview-resolution", type=int, default=640)
    parser.add_argument("--ao-samples", type=int, default=64)
    parser.add_argument("--ao-distance", type=float, default=0.0,
                        help="AO ray bound in metres; 0 derives it from holdfast "
                             "size. Exposed so the bound can be A/B measured "
                             "instead of assumed.")
    parser.add_argument("--atlas-size", type=int, default=ATLAS_SIZE)
    return parser.parse_args(argv)


def main(argv=None) -> int:
    if argv is None:
        argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = _parse_args(argv)

    if not 0.0 <= args.quality <= 1.0:
        sys.stderr.write("[kelp] --quality must be within 0..1, got "
                         + str(args.quality) + "\n")
        return 2
    if args.variants < 1:
        sys.stderr.write("[kelp] --variants must be >= 1\n")
        return 2

    out_dir = args.out or _default_out_dir()
    quality_tag = ("%.2f" % args.quality).replace(".", "")
    failures = 0

    for variant in range(args.variants):
        seed = args.seed + variant
        name = "Kelp_s{s}_q{q}".format(s=seed, q=quality_tag)
        print("")
        print("=" * 78)
        print("[kelp] seed={s} quality={q:.2f} variant={v}/{n} name={nm}".format(
            s=seed, q=args.quality, v=variant + 1, n=args.variants, nm=name))
        print("=" * 78)
        try:
            manifest = generate_kelp(
                seed=seed, quality=args.quality, out_dir=out_dir, name=name,
                want_preview=args.preview,
                preview_resolution=args.preview_resolution,
                ao_samples=args.ao_samples, atlas_size=args.atlas_size,
                ao_distance_override=args.ao_distance)
        except GenerationAborted as error:
            failures += 1
            sys.stderr.write("[kelp] VALIDATION ABORTED SAVE: "
                             + str(error) + "\n")
            # The exception's message names the gates; the per-gate detail names the
            # first offending index and measured value. Printing only the gate list
            # would leave the next agent guessing which triangle, which is the whole
            # reason validate.Failure carries a detail string.
            for failure in error.failures:
                sys.stderr.write("    " + str(failure) + "\n")
            continue

        _print_report(manifest)
        if not manifest["validation"]["allPassed"]:
            failures += 1

    print("")
    if failures:
        print("[kelp] FAILED: {n} of {t} asset(s) did not pass".format(
            n=failures, t=args.variants))
    else:
        print("[kelp] all {t} asset(s) passed every pre-save gate".format(
            t=args.variants))
    return 1 if failures else 0


def _print_report(manifest: dict) -> None:
    """Console evidence. Numbers, not adjectives."""
    identity = manifest["identity"]
    structures = manifest["structures"]
    print("  scale            {s} m tall, bounds {lo} .. {hi}".format(
        s=identity["scaleMeters"], lo=manifest["bounds"]["min"],
        hi=manifest["bounds"]["max"]))
    print("  structures       holdfast fingers={f} stipe rings={r} ribs={rb} "
          "blades={b} (canopy {c} / basal {ba})".format(
              f=structures["holdfastFingers"], r=structures["stipeRings"],
              rb=structures["stipeRibCount"], b=structures["bladeCount"],
              c=structures["bladeCanopy"], ba=structures["bladeBasal"]))
    detail = structures["bladeDetail"]
    print("  blade detail     rows={r} ring verts={v} serration teeth={s} "
          "blisters={bl} tears={t}".format(
              r=detail["rows"], v=detail["crossSectionVerts"],
              s=detail["serrationTeeth"], bl=detail["blistersPerBlade"],
              t=detail["tearsPerBlade"]))
    topology = manifest["topology"]
    print("  topology         raw faces={rf} welded verts removed={wv} "
          "uv seam edges={se} budget-reduce {b}".format(
              rf=topology["rawFacesBeforeWeld"],
              wv=topology["weld"]["verts_removed"],
              se=topology["uvSeamEdgesMarked"],
              b=("{a}->{c}".format(a=topology["trianglesBeforeBudgetReduce"],
                                  c=topology["trianglesAfterBudgetReduce"])
                 if topology["budgetReduceFired"] else "not needed")))
    uv = manifest["uv"]
    print("  uv atlas         {i} islands ({o} growth-oriented), solver={s}, "
          "{sz}px atlas, {p}px padding".format(
              i=uv["islands"], o=uv["islandsOriented"], s=uv["solver"],
              sz=uv["atlasSize"], p=uv["paddingPx"]))
    print("  uv texel density {mn}..{mx} px/m (mean {me}), mismatch {ms:.1%} "
          "vs limit {ml:.0%}; hero target {h} px/m".format(
              mn=uv["texelDensityPxPerMetreMin"], mx=uv["texelDensityPxPerMetreMax"],
              me=uv["texelDensityPxPerMetreMean"],
              ms=uv["texelDensityMismatchFraction"],
              ml=uv["texelDensityMismatchLimit"],
              h=law.TEXEL_DENSITY_HERO_FLORA))
    print("  uv distortion    p50={p50} p95={p95} p99={p99} max={mx}; over hero "
          "{oh}/{n}, over distant {od}/{n}, zero-area {z}".format(
              p50=uv["aspectDistortionP50"], p95=uv["aspectDistortionP95"],
              p99=uv["aspectDistortionP99"], mx=uv["aspectDistortionMax"],
              oh=uv["trianglesOverHeroLimit"], od=uv["trianglesOverDistantLimit"],
              n=uv["trianglesMeasured"], z=uv["zeroAreaUvTriangles"]))
    for level in manifest["lods"]:
        print("  LOD{i}             {t} tris / budget {b}  {verdict}  "
              "({v} verts)".format(
                  i=level["lod"], t=level["triangles"], b=level["budget"],
                  verdict="OK" if level["withinBudget"] else "OVER BUDGET",
                  v=level["vertices"]))
    vcol = manifest["vertexColour"]
    print("  vcol R sway      min={mn} max={mx} (255: anchor={a} rootMax={r} "
          "tipMax={t}) relSpread={s} uniform={u}".format(
              mn=vcol["swayMin"], mx=vcol["swayMax"],
              a=vcol["swayAnchorMin255"], r=vcol["swayRootMax255"],
              t=vcol["swayTipMax255"], s=vcol["swayRelativeSpread"],
              u=vcol["swayUniform"]))
    print("  vcol G biolum    authored 0 (photic kelp, no emissive organ)")
    print("  vcol B AO        baked={bk} contrast={c} min={mn} max={mx} "
          "mean={me} samples={s} distance={d} m".format(
              bk=vcol["aoBaked"], c=vcol["aoHasContrast"], mn=vcol["aoMin"],
              mx=vcol["aoMax"], me=vcol["aoMean"], s=vcol["aoSamples"],
              d=vcol["aoDistanceM"]))
    print("  vcol A           {m}".format(m=vcol["alphaMeaning"]))
    for channel_name, stats in (vcol.get("directAttributeRead") or {}).items():
        print("  vcol[direct] {c:<16} min={mn:<8} max={mx:<8} mean={me:<8} "
              "n={n} ({d})".format(c=channel_name, mn=stats["min"],
                                   mx=stats["max"], me=stats["mean"],
                                   n=stats["elements"], d=stats["domain"]))
    collision = manifest["collision"]
    print("  collision        {k} -- {j}".format(k=collision["kind"],
                                                 j=collision["justification"]))
    print("  anchors          " + ", ".join(
        a["name"] + str(a["position"]) for a in manifest["anchors"]))
    validation = manifest["validation"]
    for entry in validation["meshes"]:
        verdict = "PASS" if entry["passed"] else "FAIL"
        print("  validate LOD{i}    {v}  tris={t} verts={ve} submeshes={s}".format(
            i=entry["lod"], v=verdict, t=entry["triangles"],
            ve=entry["vertices"], s=entry["submeshes"]))
        for failure in entry["failures"]:
            print("      FAILURE: " + failure)
    if manifest["lodChainFailures"]:
        for failure in manifest["lodChainFailures"]:
            print("      CHAIN FAILURE: " + failure)
    proof = manifest.get("proof") or {}
    for measurement in proof.get("channelMeasurements", []):
        print("  channel {c:<16} min={mn:<8} max={mx:<8} mean={me:<8} "
              "gradient={g} coverage={cv}".format(
                  c=measurement["channel"], mn=measurement["min"],
                  mx=measurement["max"], me=measurement["mean"],
                  g=measurement["hasGradient"], cv=measurement["coverage"]))
    if proof:
        print("  flat sheet       " + proof["flatSheet"])
        print("  studio sheet     " + proof["studioSheet"])
        print("  material sheet   " + proof["materialSheet"])
        print("  channel sheet    " + proof["channelSheet"])
    print("  manifest         " + manifest["files"].get("manifest", "?"))
    print("  fbx              " + manifest["files"]["fbx"])
    print("  black box        {n} steps, last accepted '{la}', first invalid "
          "'{fi}'".format(n=manifest["blackBox"]["stepsRecorded"],
                          la=manifest["blackBox"]["lastAcceptedStage"],
                          fi=manifest["blackBox"]["firstInvalidStage"]))


if __name__ == "__main__":
    sys.exit(main())
