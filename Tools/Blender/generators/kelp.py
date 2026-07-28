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
ATLAS_SIZE = 1024

# Material slots, 3dmodel.md section 6. Slot 3 (emissive) is deliberately absent:
# kelp is photic tissue, so there is no bioluminescent organ to give a slot to,
# and an unused declared slot is a validator failure in its own right.
SLOT_ROLES = ("tissue", "cut_edge", "holdfast")

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
                  material_fn, geo_base, geo_lengths, uv_v_offset=0.0):
    """Sweep a parametric cross-section along ``points`` and cap both ends.

    ``offset_fn(row, u, j, theta) -> (x, y)`` returns the cross-section offset in
    the transported frame, in metres. Every amplitude a caller puts in there is
    expressed as a fraction of the LOCAL cross-section radius, never of a global
    distance from an axis: scaling displacement by distance-from-axis leaves the
    thick base porcelain-smooth while the thin tip self-intersects.

    UVs are written in METRES here, and they are UNROLLED rather than projected:

      U  cumulative chord length around each ring, measured on the ring itself
      V  cumulative surface distance along the sweep, accumulated PER COLUMN and
         corrected for the U the column drifts through:
             dv = sqrt(d3d^2 - du^2)

    That correction is the difference between passing and failing the UV gate. With
    a single shared V per row -- centreline arc length -- every quad on a tapered or
    ribbed tube gets a UV height that does not match the surface distance its own
    column actually travels, and the measured aspect distortion runs 15-30 percent
    across essentially the whole mesh. Unrolling per column makes all four UV edges
    of every quad equal to their 3D counterparts, so what remains is only the
    diagonal error, which is genuine Gaussian curvature and small.

    ``3dmodel.md`` section 6 forbids "Stretched polygons above 15 percent aspect
    distortion for hero/near assets". A later single UNIFORM scale into an atlas
    rect preserves this; a per-island fit would manufacture the distortion instead.

    Returns the (u_min, v_min, u_max, v_max) UV bounds of the island in metres.
    """
    rows = len(points)
    tangents, normals, binormals = _parallel_frames(points)

    # -- ring plan, including a graded cap ring at each end ---------------
    # A single-step pole fan produces sub-millimetre, heavily sheared triangles: the
    # first run of this generator reported UV areas of 9.5e-08 against the 1e-07
    # degeneracy epsilon, and sliver islands 2.7 px wide. One intermediate shrink
    # ring turns each pole into a two-step dome, so the fan triangles stay close to
    # isotropic and comfortably above the epsilon.
    def _ring_radius(index):
        centre = points[index]
        total = 0.0
        u_param = index / float(rows - 1) if rows > 1 else 0.0
        for j in range(segments):
            theta = 2.0 * math.pi * j / float(segments)
            off_x, off_y = offset_fn(index, u_param, j, theta)
            total += math.sqrt(off_x * off_x + off_y * off_y)
        return total / float(segments)

    lead_radius = max(1e-4, _ring_radius(0))
    trail_radius = max(1e-4, _ring_radius(rows - 1))
    # Keep cap rings above ~3 mm so no cap triangle can fall under the degeneracy
    # epsilon once it is scaled into UV space.
    lead_scale = min(0.62, max(0.30, 0.0030 / lead_radius)) if lead_radius > 0.0030 else 0.62
    trail_scale = min(0.62, max(0.30, 0.0030 / trail_radius)) if trail_radius > 0.0030 else 0.62

    plan = []
    plan.append((points[0] - tangents[0] * (lead_radius * 0.45), 0, 0.0,
                 lead_scale, -lead_radius * 0.45))
    for i in range(rows):
        u_param = i / float(rows - 1) if rows > 1 else 0.0
        plan.append((points[i], i, u_param, 1.0, geo_lengths[i]))
    plan.append((points[rows - 1] + tangents[rows - 1] * (trail_radius * 0.45),
                 rows - 1, 1.0, trail_scale,
                 geo_lengths[rows - 1] + trail_radius * 0.45))

    # -- vertices ---------------------------------------------------------
    # One extra column duplicates column 0 so the seam can carry two different U
    # values. weld_and_clean merges the duplicated VERTEX afterwards, but UVs live
    # on loops, so the seam survives as differing corner UVs -- which is what a UV
    # seam is, and what the FBX exporter will re-split on anyway.
    grid = []
    for centre, frame_index, u_param, radius_scale, geo_length in plan:
        normal = normals[frame_index]
        binormal = binormals[frame_index]
        row = []
        for j in range(segments + 1):
            theta = 2.0 * math.pi * (j % segments) / float(segments)
            off_x, off_y = offset_fn(frame_index, u_param, j % segments, theta)
            position = centre + normal * (off_x * radius_scale) + \
                binormal * (off_y * radius_scale)
            vertex = bm.verts.new(position)
            vertex[geo_layer] = geo_base + max(0.0, geo_length)
            vertex[cls_layer] = vertex_class
            row.append(vertex)
        grid.append(row)
    bm.verts.index_update()
    ring_count = len(grid)

    # -- U in metres: cumulative chord length around each ring -------------
    u_coords = []
    for row in grid:
        cumulative = [0.0]
        for j in range(1, segments + 1):
            cumulative.append(cumulative[-1] + (row[j].co - row[j - 1].co).length)
        u_coords.append(cumulative)

    # -- V in metres: per-column unrolled surface distance -----------------
    v_coords = [[uv_v_offset] * (segments + 1)]
    for i in range(1, ring_count):
        previous = v_coords[-1]
        row = []
        for j in range(segments + 1):
            span = (grid[i][j].co - grid[i - 1][j].co).length
            drift = u_coords[i][j] - u_coords[i - 1][j]
            height = span * span - drift * drift
            row.append(previous[j] + (math.sqrt(height) if height > 0.0 else 0.0))
        v_coords.append(row)

    u_min = 0.0
    u_max = max(row[-1] for row in u_coords)
    v_min = uv_v_offset
    v_max = max(max(row) for row in v_coords)

    # -- quads ------------------------------------------------------------
    for i in range(ring_count - 1):
        for j in range(segments):
            a = grid[i][j]
            b = grid[i][j + 1]
            c = grid[i + 1][j + 1]
            d = grid[i + 1][j]
            if len({a, b, c, d}) != 4:
                # A collapsed cross-section would make a degenerate quad, which
                # 3dmodel.md section 10 rejects outright. Skipping is correct: the
                # apex fans below close whatever this leaves open.
                continue
            face = bm.faces.new((a, b, c, d))
            face.material_index = material_fn(i, j, ring_count, segments)
            face[part_layer] = part_id
            corners = ((a, u_coords[i][j], v_coords[i][j]),
                       (b, u_coords[i][j + 1], v_coords[i][j + 1]),
                       (c, u_coords[i + 1][j + 1], v_coords[i + 1][j + 1]),
                       (d, u_coords[i + 1][j], v_coords[i + 1][j]))
            for loop in face.loops:
                for vertex, u_value, v_value in corners:
                    if loop.vert is vertex:
                        loop[uv_layer].uv = (u_value, v_value)
                        break

    # -- apex fans --------------------------------------------------------
    # Closing with a shared apex instead of an n-gon cap keeps the part a single UV
    # island. An n-gon cap would be its own island, and law.UV_MIN_ISLAND_PIXELS = 4
    # rejects islands that small on a 1024 atlas.
    for end in (0, ring_count - 1):
        row = grid[end]
        frame_index = plan[end][1]
        ring_centre = sum((v.co for v in row[:segments]), Vector((0.0, 0.0, 0.0))) \
            / float(segments)
        direction = tangents[frame_index] * (-1.0 if end == 0 else 1.0)
        radius = sum((v.co - ring_centre).length for v in row[:segments]) / float(segments)
        apex_position = ring_centre + direction * max(radius * 0.80, 1e-4)
        apex = bm.verts.new(apex_position)
        apex[geo_layer] = geo_base + max(0.0, plan[end][4] +
                                         (radius if end else -radius))
        apex[cls_layer] = vertex_class
        for j in range(segments):
            first = row[j]
            second = row[j + 1]
            if first is second:
                continue
            # Winding must follow the quad band it closes, or the shell reports an
            # inconsistent-winding failure at exactly two rings out of hundreds.
            triple = (first, second, apex) if end else (second, first, apex)
            try:
                face = bm.faces.new(triple)
            except ValueError:
                continue
            face.material_index = material_fn(frame_index, j, ring_count, segments)
            face[part_layer] = part_id
            u_first = u_coords[end][j]
            u_second = u_coords[end][j + 1]
            # The apex corner's V is the real 3D distance from that corner to the
            # apex, per triangle, so the fan is unrolled on the same basis as the
            # bands rather than guessed.
            reach = 0.5 * ((apex_position - first.co).length +
                           (apex_position - second.co).length)
            v_base = 0.5 * (v_coords[end][j] + v_coords[end][j + 1])
            v_apex = v_base + (reach if end else -reach)
            if v_apex < v_min:
                v_min = v_apex
            if v_apex > v_max:
                v_max = v_apex
            mapping = {first: (u_first, v_coords[end][j]),
                       second: (u_second, v_coords[end][j + 1]),
                       apex: (0.5 * (u_first + u_second), v_apex)}
            for loop in face.loops:
                loop[uv_layer].uv = mapping[loop.vert]

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
        self.stipe_radius_base = float(rng.uniform(0.036, 0.052))
        self.stipe_radius_top = float(rng.uniform(0.011, 0.017))
        self.stipe_ellipse = float(rng.uniform(0.14, 0.24))
        self.stipe_twist = float(rng.uniform(0.7, 2.1))
        self.rib_count = int(rng.integers(5, 9))
        self.rib_amplitude = float(rng.uniform(0.085, 0.155))
        self.growth_ring_frequency = float(rng.uniform(7.0, 13.0))
        self.growth_ring_amplitude = float(rng.uniform(0.045, 0.085))

        # Pneumatocyst swellings: real kelp carries gas bladders on the stipe.
        swelling_count = _qi(1, 3, self.quality)
        self.swellings = tuple(
            (float(rng.uniform(0.22, 0.92)), float(rng.uniform(0.16, 0.34)),
             float(rng.uniform(0.035, 0.070)))
            for _ in range(swelling_count)
        )

        # Holdfast. Fingers splay along the ground plane; the boss is the knuckle
        # that hides the union, which is what section 3 permits instead of a
        # boolean: "Branch intersections must be blended, welded, or explicitly
        # hidden by knuckles."
        self.finger_count = _qi(5, 9, self.quality)
        self.boss_radius = float(rng.uniform(0.062, 0.084))
        self.boss_height = float(rng.uniform(0.085, 0.125))

        # Blades. Canopy cluster plus basal sporophylls, which is the real
        # Macrocystis arrangement and also puts the long fronds where the sway
        # leverage is highest.
        self.canopy_blades = _qi(4, 9, self.quality)
        self.basal_blades = _qi(1, 3, self.quality)
        self.blade_length = float(rng.uniform(0.42, 0.68))
        self.blade_width = float(rng.uniform(0.058, 0.092))
        self.blade_thickness = float(rng.uniform(0.0045, 0.0075))

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
        for centre, width, amplitude in self.swellings:
            radius += amplitude * math.exp(
                -((t - centre) ** 2) / max(1e-5, width * width * 0.5))
        return radius


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

    bounds = _sweep_closed(
        bm, uv_layer, geo_layer, cls_layer, part_layer,
        points=points, segments=segments, offset_fn=offset, part_id=part_id,
        vertex_class=CLS_STIPE,
        material_fn=lambda i, j, r, s: law.MATERIAL_SLOT_PRIMARY,
        geo_base=form.boss_height * 0.55, geo_lengths=lengths)
    return bounds, points, lengths


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

    islands.append(_sweep_closed(
        bm, uv_layer, geo_layer, cls_layer, part_layer,
        points=boss_points, segments=boss_segments, offset_fn=boss_offset,
        part_id=part_id, vertex_class=CLS_BOSS,
        material_fn=lambda i, j, r, s: law.MATERIAL_SLOT_TRIM,
        geo_base=0.0, geo_lengths=boss_lengths))
    part_id += 1

    finger_rows = _qi(5, 8, quality)
    finger_segments = _qi(6, 10, quality)
    for index in range(form.finger_count):
        azimuth = 2.0 * math.pi * index / float(form.finger_count) + \
            float(rng.uniform(-0.34, 0.34))
        direction = Vector((math.cos(azimuth), math.sin(azimuth), 0.0))
        # Upstream fingers reach further: that is where the drag load is resisted.
        upstream = law.saturate(0.5 - 0.5 * direction.dot(form.current))
        reach = float(rng.uniform(0.135, 0.225)) * (0.82 + 0.42 * upstream)
        radius0 = float(rng.uniform(0.015, 0.023))
        knuckle_freq = float(rng.uniform(4.5, 8.5))
        knuckle_amp = float(rng.uniform(0.13, 0.24))
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
            radius = r0 * (1.0 - 0.72 * (u ** 0.9)) + 0.0035
            knuckles = 1.0 + ka * math.sin(kf * math.pi * u + phase)
            # Flattened against the substrate: a gripping root, not a wire.
            flatten = 1.0 - 0.22 * abs(math.sin(theta))
            sample = Vector((math.cos(theta), math.sin(theta), u * 4.0))
            fine = 1.0 + 0.12 * _fine_noise(sample, form.noise_offset, 3.6)
            scaled = radius * knuckles * fine
            return (math.cos(theta) * scaled, math.sin(theta) * scaled * flatten)

        islands.append(_sweep_closed(
            bm, uv_layer, geo_layer, cls_layer, part_layer,
            points=points, segments=finger_segments, offset_fn=finger_offset,
            part_id=part_id, vertex_class=CLS_FINGER,
            material_fn=lambda i, j, r, s: law.MATERIAL_SLOT_TRIM,
            geo_base=0.012, geo_lengths=lengths))
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

    rows = _qi(11, 17, quality)
    nu = _qi(4, 6, quality)
    segments = 2 * (nu - 1)
    # Serration teeth and blister count are the density knobs section 9 names:
    # "GlobalQualityWeight scales flora and coral fidelity through offline branch
    # count, pore density, blade serration density..."
    serration_teeth = _qi(3, 9, quality)
    blister_count = _qi(1, 4, quality)
    tear_count = _qi(1, 3, quality)

    stipe_rows = len(stipe_points)

    # Canopy fronds cluster in the upper stipe, basal sporophylls sit low. That is
    # the real arrangement and it also places the long fronds where the sway
    # leverage is genuinely highest, instead of forcing the sway curve to lie.
    heights = []
    for k in range(form.canopy_blades):
        span = k / float(max(1, form.canopy_blades - 1)) if form.canopy_blades > 1 else 0.5
        heights.append((0.44 + 0.54 * span + float(rng.uniform(-0.022, 0.022)), True))
    for k in range(form.basal_blades):
        span = k / float(max(1, form.basal_blades - 1)) if form.basal_blades > 1 else 0.5
        heights.append((0.10 + 0.12 * span + float(rng.uniform(-0.015, 0.015)), False))

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
        length = form.blade_length * (0.72 + 0.5 * lee) * \
            (1.0 if is_canopy else 0.55) * float(rng.uniform(0.88, 1.14))
        width = form.blade_width * (0.85 + 0.3 * lee) * float(rng.uniform(0.9, 1.12))
        thickness = form.blade_thickness * float(rng.uniform(0.9, 1.1))

        rise = float(rng.uniform(0.22, 0.40))
        droop = float(rng.uniform(0.20, 0.42))
        serr_phase_right = float(detail_rng.uniform(0.0, 1.0))
        serr_phase_left = float(detail_rng.uniform(0.0, 1.0))
        serr_amp = float(detail_rng.uniform(0.10, 0.19))
        fold_k = float(detail_rng.uniform(1.6, 3.4))
        fold_phase = float(detail_rng.uniform(0.0, 6.28))
        fold_amp = float(detail_rng.uniform(0.9, 2.1))
        tears = tuple((float(detail_rng.uniform(0.25, 0.9)),
                       1.0 if detail_rng.random() < 0.5 else -1.0,
                       float(detail_rng.uniform(0.030, 0.065)),
                       float(detail_rng.uniform(0.38, 0.66)))
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
            radial = outward * (-stipe_r * 0.55 + length * 0.34 * (u ** 0.8))
            flow = form.current * (length * 0.80 * form.current_strength *
                                   1.45 * (u ** 1.4))
            vertical = length * (rise * (u ** 0.55) - droop * (u ** 2.1))
            points.append(Vector((attach.x + radial.x + flow.x,
                                  attach.y + radial.y + flow.y,
                                  attach.z + vertical)))
        lengths = _arclengths(points)

        def blade_offset(row_index, u, j, theta,
                         w=width, th=thickness, sa=serr_amp,
                         spr=serr_phase_right, spl=serr_phase_left,
                         fk=fold_k, fp=fold_phase, fa=fold_amp,
                         tear_list=tears, blister_list=blisters,
                         scar_u=scar_at, scar_w=scar_width):
            cx = math.cos(theta)
            sy = math.sin(theta)

            # Sheet plan-form: narrow at the sheath, widest just past mid, tapering
            # to a point. A rectangle is the "flat untextured rectangle" the section
            # 8 gate rejects.
            plan = math.sin(math.pi * (u ** 0.72)) ** 0.55
            half_width = w * (0.16 + 0.94 * plan)

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

            half_thickness = th * (0.55 + 0.45 * plan)

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
            fold = fa * th * math.sin(fk * math.pi * u + fp) * (0.35 + 0.65 * abs(cx))

            return (half_width * cx, half_thickness * sy + fold)

        def blade_material(i, j, r, s, n=nu, seg=segments):
            # Rim columns straddle theta = 0 and theta = pi exactly.
            if j in (0, seg - 1, n - 2, n - 1):
                return law.MATERIAL_SLOT_CUT_EDGE
            return law.MATERIAL_SLOT_PRIMARY

        islands.append(_sweep_closed(
            bm, uv_layer, geo_layer, cls_layer, part_layer,
            points=points, segments=segments, offset_fn=blade_offset,
            part_id=part_id, vertex_class=CLS_BLADE,
            material_fn=blade_material,
            geo_base=form.boss_height * 0.55 + attach_length,
            geo_lengths=lengths))
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
# Stage 5: UV atlas layout  --  3dmodel.md section 6
# ---------------------------------------------------------------------------

def _shelf_pack(rects, region: float, gutter: float):
    """Height-sorted shelf pack. Returns placements or None if it does not fit.

    ``3dmodel.md`` section 6: "Atlas packing must use MaxRects, Skyline, or
    equivalent rectangle packing. Random shelf packing that leaves large holes is
    rejected." Sorting by height descending before shelving is the classic
    Shelf-Next-Fit-Decreasing-Height variant, which is what makes it deterministic
    and tight rather than random.
    """
    order = sorted(range(len(rects)), key=lambda i: (-rects[i][1], -rects[i][0], i))
    placements = [None] * len(rects)
    cursor_x = 0.0
    cursor_y = 0.0
    shelf_height = 0.0
    for i in order:
        width, height = rects[i]
        if cursor_x + width > region:
            cursor_x = 0.0
            cursor_y += shelf_height + gutter
            shelf_height = 0.0
        if cursor_y + height > region:
            return None
        placements[i] = (cursor_x, cursor_y)
        cursor_x += width + gutter
        if height > shelf_height:
            shelf_height = height
    return placements


def _pack_uv_islands(bm, uv_layer, part_layer, island_bounds, atlas_size: int):
    """Fit every part's metre-space UV island into the atlas with one uniform scale.

    The scale is UNIFORM and shared by every island. A per-island fit would make
    each part a different texel density, which section 6 rejects ("Texel density
    mismatch above 20 percent"), and a non-uniform stretch would manufacture the
    aspect distortion the same section bans. Islands keep the arc-length
    proportions the sweep gave them; only their position and their common scale
    change.
    """
    padding_px = law.atlas_padding_for(atlas_size)
    padding_uv = float(padding_px) / float(atlas_size)
    region = 1.0 - 2.0 * padding_uv

    keys = sorted(island_bounds.keys())
    sizes = []
    for key in keys:
        u0, v0, u1, v1 = island_bounds[key]
        sizes.append((max(1e-5, u1 - u0), max(1e-5, v1 - v0)))

    total_area = sum(w * h for w, h in sizes)
    scale = region / max(1e-6, math.sqrt(total_area)) * 0.92
    placements = None
    for _attempt in range(240):
        scaled = [(w * scale, h * scale) for w, h in sizes]
        placements = _shelf_pack(scaled, region, padding_uv)
        if placements is not None:
            break
        scale *= 0.955
    if placements is None:
        raise RuntimeError("UV atlas packing failed to converge for "
                           + str(len(keys)) + " islands")

    offsets = {}
    for position, key in enumerate(keys):
        u0, v0, _u1, _v1 = island_bounds[key]
        place_u, place_v = placements[position]
        offsets[key] = (padding_uv + place_u - u0 * scale,
                        padding_uv + place_v - v0 * scale)

    used_area = 0.0
    for face in bm.faces:
        key = face[part_layer]
        offset = offsets.get(key)
        if offset is None:
            continue
        for loop in face.loops:
            u, v = loop[uv_layer].uv
            loop[uv_layer].uv = (u * scale + offset[0], v * scale + offset[1])
    for position, key in enumerate(keys):
        used_area += sizes[position][0] * scale * sizes[position][1] * scale

    return {
        "atlasSize": atlas_size,
        "atlasFamily": ATLAS_FAMILY,
        "paddingPx": padding_px,
        "islands": len(keys),
        "uniformScaleUvPerMetre": round(scale, 6),
        "texelDensityPxPerMetre": round(scale * atlas_size, 2),
        "utilisationFraction": round(used_area, 5),
    }


def _mark_uv_seams(bm, uv_layer) -> int:
    """Flag edges whose two faces disagree about UV, so decimation preserves them.

    ``3dmodel.md`` section 7 requires decimation to preserve UV seams, and
    ``mesh_ops._split_uv_seams`` implements that by splitting edges flagged
    ``seam``. Nothing else in the pipeline sets that flag, so an unmarked seam is
    silently collapsed at LOD1 and the texture tears across it.
    """
    marked = 0
    for edge in bm.edges:
        faces = edge.link_faces
        if len(faces) != 2:
            continue
        coords = {}
        split = False
        for face in faces:
            for loop in face.loops:
                if loop.vert not in edge.verts:
                    continue
                uv = tuple(round(value, 6) for value in loop[uv_layer].uv)
                previous = coords.get(loop.vert)
                if previous is None:
                    coords[loop.vert] = uv
                elif previous != uv:
                    split = True
        if split:
            edge.seam = True
            marked += 1
    return marked


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
    specs = (
        ("tissue", (0.075, 0.150, 0.075, 1.0), 0.42),
        ("cut_edge", (0.115, 0.095, 0.052, 1.0), 0.30),
        ("holdfast", (0.055, 0.048, 0.040, 1.0), 0.66),
    )
    out = []
    for role, colour, roughness in specs:
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
            if "Metallic" in principled.inputs:
                principled.inputs["Metallic"].default_value = 0.0
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
    # The core now bounds the AO ray itself, so ``distance`` is honoured. It matters:
    # unbounded rays let every blade occlude every other blade across the whole plant,
    # which buries the local cavity detail 3DMODEL_FLORA_CORAL.md section 2 asks for
    # ("low values in crevices, under plates, root clusters, and branch
    # intersections") under a global sky-occlusion term.
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

    report.update({
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
    for attribute_name in (GEO_LAYER, CLS_LAYER, PART_LAYER):
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
                  ao_samples: int, atlas_size: int) -> dict:
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

    island_bounds = {}
    holdfast_islands, next_part = _build_holdfast(bm, layers, form, quality, 0)
    for offset, bounds in enumerate(holdfast_islands):
        island_bounds[offset] = bounds

    stipe_rows = _qi(12, 26, quality)
    stipe_segments = _qi(7, 12, quality)
    stipe_part = next_part
    stipe_bounds, stipe_points, stipe_lengths = _build_stipe(
        bm, layers, form, stipe_rows, stipe_segments, stipe_part)
    island_bounds[stipe_part] = stipe_bounds
    next_part += 1

    blade_islands, next_part, blade_records, blade_stats = _build_blades(
        bm, layers, form, quality, stipe_points, stipe_lengths, next_part)
    for offset, bounds in enumerate(blade_islands):
        island_bounds[stipe_part + 1 + offset] = bounds

    raw_faces = len(bm.faces)
    raw_verts = len(bm.verts)
    bb.record("geometry_built", vertex_count=raw_verts, triangle_count=raw_faces,
              family=law.Family.FLORA.value)

    # -- 5. UVs and material IDs ------------------------------------------
    atlas_report = _pack_uv_islands(bm, uv_layer, part_layer, island_bounds,
                                    atlas_size)
    weld_stats = mesh_ops.weld_and_clean(bm, merge_distance=1e-4, blackbox=bb)
    seams_marked = _mark_uv_seams(bm, uv_layer)

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
        ao_distance=max(0.12, form.boss_radius * 2.4 + 0.14))

    bounds_min, bounds_max = mesh_ops.local_bounds(obj)
    identity.scale_meters = round(max(bounds_max.z - bounds_min.z,
                                      bounds_max.x - bounds_min.x,
                                      bounds_max.y - bounds_min.y), 5)

    # -- 8. LOD chain -----------------------------------------------------
    lods = mesh_ops.build_lod_chain(
        obj, family=law.Family.FLORA, name=name, quality_weight=quality,
        levels=3, preserve_seams=True, blackbox=bb)

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

    validate.assert_or_abort(
        [reports, chain_failures, collider_failures], blackbox=bb,
        reason="kelp pre-save gate seed={s} quality={q}".format(s=seed, q=quality))

    # -- 12. save ---------------------------------------------------------
    os.makedirs(out_dir, exist_ok=True)
    export_objects = [level.obj for level in lods] + [empty for _n, empty in anchors]
    fbx_path = os.path.join(out_dir, "MESH_{f}_{n}.fbx".format(
        f=law.Family.FLORA.value, n=name))
    view_layer = bpy.context.view_layer
    for other in view_layer.objects:
        other.select_set(False)
    for target in export_objects:
        target.select_set(True)
    view_layer.objects.active = lods[0].obj
    bpy.ops.export_scene.fbx(
        filepath=fbx_path, use_selection=True, apply_unit_scale=True,
        global_scale=1.0, apply_scale_options="FBX_SCALE_NONE",
        axis_forward="-Z", axis_up="Y", object_types={"MESH", "EMPTY"},
        use_mesh_modifiers=False, mesh_smooth_type="FACE", use_tspace=True,
        # The exporter defaults to SRGB, which would gamma-encode masks that are
        # DATA, not colour: a sway value of 0.5 would arrive in Unity as 0.74. LINEAR
        # passes the authored 0..1 numbers through untouched.
        colors_type="LINEAR", path_mode="STRIP", use_triangles=True,
        bake_anim=False)

    blend_path = os.path.join(out_dir, "SRC_{f}_{n}.blend".format(
        f=law.Family.FLORA.value, n=name))
    bpy.ops.wm.save_as_mainfile(filepath=blend_path, copy=True)

    # -- 13. proof artefacts ----------------------------------------------
    proof = {}
    if want_preview:
        proof = _render_proof(lods[0].obj, name=run_tag,
                              resolution=preview_resolution)

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
            "trianglesBeforeBudgetReduce": before_reduce,
            "trianglesAfterBudgetReduce": after_reduce,
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
            "achieved": atlas_report["texelDensityPxPerMetre"],
            "meetsHero": atlas_report["texelDensityPxPerMetre"] >=
                         law.TEXEL_DENSITY_HERO_FLORA,
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
            "allPassed": all(r.passed for r in reports) and not chain_failures
                         and not collider_failures,
        },
        "files": {"fbx": os.path.basename(fbx_path),
                  "blend": os.path.basename(blend_path)},
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

    manifest_path = os.path.join(out_dir, law.NAME_MANIFEST.format(
        family=law.Family.FLORA.value, name=name) + ".json")
    with open(manifest_path, "w", encoding="utf-8") as handle:
        json.dump(manifest, handle, indent=1, sort_keys=False)
    manifest["files"]["manifest"] = os.path.basename(manifest_path)
    manifest["outDir"] = out_dir
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
    flat = preview.render_contact_sheet(obj, preview.PreviewSpec(
        name=name + "_flat", resolution=resolution, mode="flat",
        views=("front", "three_quarter", "side", "low"),
        surface_class=law.SurfaceClass.ORGANIC))
    studio = preview.render_contact_sheet(obj, preview.PreviewSpec(
        name=name + "_studio", resolution=resolution, mode="studio",
        views=("front", "three_quarter", "side", "top"),
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
                ao_samples=args.ao_samples, atlas_size=args.atlas_size)
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
    print("  uv atlas         {i} islands, {sz}px atlas, {p}px padding, "
          "{d} px/m (hero target {h}), utilisation {u:.1%}".format(
              i=uv["islands"], sz=uv["atlasSize"], p=uv["paddingPx"],
              d=uv["texelDensityPxPerMetre"],
              h=law.TEXEL_DENSITY_HERO_FLORA, u=uv["utilisationFraction"]))
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
        print("  channel sheet    " + proof["channelSheet"])
    print("  manifest         " + manifest["files"].get("manifest", "?"))
    print("  fbx              " + manifest["files"]["fbx"])
    print("  black box        {n} steps, last accepted '{la}', first invalid "
          "'{fi}'".format(n=manifest["blackBox"]["stepsRecorded"],
                          la=manifest["blackBox"]["lastAcceptedStage"],
                          fi=manifest["blackBox"]["firstInvalidStage"]))


if __name__ == "__main__":
    sys.exit(main())
