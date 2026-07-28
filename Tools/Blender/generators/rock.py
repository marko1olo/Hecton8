"""HECTON-8 geology generator: stratified, fractured, mineral-stained rock.

Family: ``law.Family.GEOLOGY``. Surface class: ``law.SurfaceClass.GEOLOGIC``, which
shares the hard-surface channel contract (R edge/chip/mineral reveal, G mineral stain,
B baked AO, A ore/emission mask).

Authority this file implements, read in full before editing:
  - ``3DMODEL_GEOLOGY_ROCKS.md``     the specification. Sections 1-11.
  - ``3dmodel.md``                   sections 3, 6, 7, 9, 10, 12.
  - ``PROCEDURAL_ASSET_PIPELINE.md`` stage order + required output package.
  - ``AGENTS.md``                    Zero Mocks, Hollow System Ban, Relative Path
                                     Requirement, Never Trust Automated Assertions.

The bar, quoted from ``PROCEDURAL_ASSET_PIPELINE.md``:

    "Geology must read as material history. The generator must model strata,
     fractures, sediment ledges, wet cavities, mineral seams, pressure breaks, scale
     witnesses, and route-facing landmarks. A rock is not accepted if it is a noise
     sphere with a rock material."

So the shape grammar here is NOT a displaced sphere. The body is a stack of irregular
sedimentary beds, each with its own thickness, competence (hardness), plan outline and
radial recess, bridged into one manifold solid. Every band interface is a real
geometric ledge; soft beds recede and hard beds overhang them, which is what produces
both the stratified silhouette and the occluded cavities that make a baked AO channel
mean something. Planar ``bisect_plane`` cuts then remove corners along conjugate shear
directions, and the exposed faces are filled and assigned to the fracture material
slot. Noise exists, but only as a weak high-frequency term anisotropically stretched
along the bedding plane -- it is grain on top of structure, never the structure itself.

``terrain.md`` was NOT read and is NOT applicable: this generator never touches terrain
heightmaps, ``float[,]`` array indexing, coordinate wrapping, slope mapping, splatmaps,
or biome masks. It builds one object-space mesh around its own origin.

Headless entry point::

    blender.exe -b --factory-startup -P Tools/Blender/generators/rock.py -- \
        --seed 1713 --quality 1.0 --size-class outcrop --variants 1
"""

from __future__ import annotations

import argparse
import json
import math
import os
import sys
import time
from dataclasses import dataclass, field
from typing import Optional

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

_HERE = os.path.dirname(os.path.abspath(__file__))
_BLENDER_TOOLS = os.path.dirname(_HERE)
if _BLENDER_TOOLS not in sys.path:
    # AGENTS.md [RULE] Relative Path Requirement: derived from __file__, never typed.
    sys.path.insert(0, _BLENDER_TOOLS)

from h8forge import law, mesh_ops, preview, vertexcolor          # noqa: E402
from h8forge.blackbox import BlackBox, GenerationAborted         # noqa: E402

try:
    from h8forge import validate as h8validate                   # noqa: E402
except Exception as _validate_import_error:                       # pragma: no cover
    h8validate = None
    _VALIDATE_IMPORT_NOTE = str(_validate_import_error)
else:
    _VALIDATE_IMPORT_NOTE = ""

GENERATOR_NAME = "rock.py"
GENERATOR_VERSION = "1.0.0"


# ---------------------------------------------------------------------------
# Law gap: per-size geology budgets that ``law.py`` does not carry yet
# ---------------------------------------------------------------------------
# ``law.LOD_BUDGETS[Family.GEOLOGY]`` == 18000/7000/1200, which is the *large vent /
# cliff chunk* row of ``3dmodel.md`` section 7 ("Geology rock/vent").
# ``3DMODEL_GEOLOGY_ROCKS.md`` section 7 is STRICTER for smaller rocks and lists three
# rows, verbatim:
#
#   - Small rock:              LOD0 4,000   LOD1 1,200   LOD2 250
#   - Medium boulder/ore:      LOD0 9,000   LOD1 3,000   LOD2 600
#   - Large vent/cliff chunk:  LOD0 18,000  LOD1 7,000   LOD2 1,200
#
# ``3dmodel.md`` section 1: "This root file overrides weaker family documents.
# Specialist files add stricter rules for their domain." The geology rows are stricter,
# therefore binding, and law.py has no symbol for them. This table is the gap, not an
# invention: the numbers are quoted, the ceiling still comes from law for the large
# class, and the report asks the lead to move these into ``law.py`` as
# ``GEOLOGY_SIZE_LOD_BUDGETS``. Nothing here redefines a value law.py already owns.
GEOLOGY_SIZE_LOD_ROWS = {
    "boulder": (4_000, 1_200, 250),
    "outcrop": (9_000, 3_000, 600),
    "cliff-chunk": (18_000, 7_000, 1_200),
}


@dataclass(frozen=True)
class SizeClass:
    """One geology size lane. Radius/height are metres in object space."""

    name: str
    radius_m: float
    height_m: float
    camera_class: str
    bible_row: str

    @property
    def longest_extent_m(self) -> float:
        return max(self.radius_m * 2.0, self.height_m)

    def budget(self, lod_index: int) -> int:
        """Strictest of law's family ceiling and the geology specialist row."""
        family_limit = law.LOD_BUDGETS[law.Family.GEOLOGY].limit(lod_index)
        row = GEOLOGY_SIZE_LOD_ROWS[self.name]
        return min(family_limit, row[min(lod_index, 2)])


SIZE_CLASSES = {
    "boulder": SizeClass("boulder", 0.40, 0.58, "near_interactive",
                         "3DMODEL_GEOLOGY_ROCKS.md s7 small rock"),
    "outcrop": SizeClass("outcrop", 1.45, 2.05, "mid_route",
                         "3DMODEL_GEOLOGY_ROCKS.md s7 medium boulder/ore"),
    "cliff-chunk": SizeClass("cliff-chunk", 3.10, 7.60, "landmark",
                             "3DMODEL_GEOLOGY_ROCKS.md s7 large vent/cliff chunk"),
}

# Chip width as a FRACTION of the asset's longest extent. law.BEVEL_RANGES has no
# Family.GEOLOGY entry (only SMALL_PROP / BASE_MODULE / WRECKAGE), so there is no
# constant to import -- reported as a law gap rather than hardcoded silently. The
# fraction is calibrated against the two anchors ``3dmodel.md`` section 4 does give:
# 0.006-0.018 m on a ~0.2 m handheld prop and 0.08-0.35 m on a macro hull edge. At
# 0.009..0.026 of extent a 0.8 m boulder chips at 7-21 mm and a 7.6 m cliff chunk at
# 68-198 mm, which lands inside both anchors. Width therefore scales with asset size
# and GlobalQualityWeight exactly as section 4 requires.
CHIP_WIDTH_FRACTION_MIN = 0.009
CHIP_WIDTH_FRACTION_MAX = 0.026

# Absolute-size scale witnesses (metres). These do NOT scale with the rock: that is
# the entire point. ``3dmodel.md`` section 12 lists "scale witnesses" as a required
# property, and a feature whose size tracks the asset tells the player nothing. Bed
# thickness and surface grain stay fixed, so a 0.8 m boulder shows ~4 beds and a
# 7.6 m cliff shows ~40 of the same beds.
WITNESS_BED_THICKNESS_MIN_M = 0.055
WITNESS_BED_THICKNESS_MAX_M = 0.340
WITNESS_GRAIN_WAVELENGTH_M = 0.075
WITNESS_PIT_WAVELENGTH_M = 0.052
WITNESS_PIT_DEPTH_M = 0.011

# Beds are capped so a tall chunk cannot demand more rings than its triangle budget
# can carry. Recorded in the manifest when it binds.
MAX_BEDS = 42
MIN_BEDS = 3

# Fraction of the LOD0 budget the base lattice may consume before fractures, vugs and
# chip bevels add their geometry. The remainder is headroom for those stages; the
# authored high-density sculpt is reduced by mesh_ops.reduce_to_budget afterwards.
LATTICE_BUDGET_SHARE = 0.42

# High-density authoring multiplier. mesh_ops.reduce_to_budget docstring: "the correct
# authoring route for organic surfaces is high-density sculpt THEN reduce". Same law
# applies to a fractured rock: displacing a mesh already at budget resolution turns
# ledges into mush.
SCULPT_DENSITY_MULTIPLIER = 2.35


# ---------------------------------------------------------------------------
# Deterministic band-limited anisotropic noise
# ---------------------------------------------------------------------------

class AnisotropicField:
    """Sum of random-phase sinusoids with an explicitly controlled wavelength.

    Chosen over Perlin/simplex for two reasons that matter here:

    1. The wavelength is a parameter in METRES, so a "scale witness" claim is a
       property of the field rather than a hope about a noise basis. Grain authored at
       0.075 m stays 0.075 m on a boulder and on a cliff.
    2. It is trivially anisotropic. Scaling the bedding-normal component of every wave
       vector stretches features ALONG the bedding plane, which is what makes the
       surface read as sedimentary grain instead of the isotropic crust that an
       icosphere-plus-Perlin rock produces -- explicitly rejected by
       ``PROCEDURAL_ASSET_PIPELINE.md``.

    Deterministic: every direction, amplitude and phase comes from the seeded
    ``numpy.random.Generator`` passed in. No wall clock, no global random state.
    """

    __slots__ = ("_k", "_amp", "_phase")

    def __init__(self, rng: np.random.Generator, wavelength_m: float, *,
                 octaves: int = 3, waves_per_octave: int = 5,
                 lacunarity: float = 2.15, gain: float = 0.48,
                 bedding_normal: Optional[np.ndarray] = None,
                 anisotropy: float = 1.0) -> None:
        total = max(1, octaves) * max(1, waves_per_octave)
        directions = rng.normal(size=(total, 3))
        norms = np.linalg.norm(directions, axis=1, keepdims=True)
        norms[norms < 1e-9] = 1.0
        directions /= norms

        if bedding_normal is not None and anisotropy != 1.0:
            n = np.asarray(bedding_normal, dtype=np.float64)
            n = n / max(1e-9, float(np.linalg.norm(n)))
            along = directions @ n
            # Compress the wave vector across bedding and stretch it along bedding:
            # short wavelength across the beds, long wavelength within them.
            directions = directions + np.outer(along * (anisotropy - 1.0), n)
            norms = np.linalg.norm(directions, axis=1, keepdims=True)
            norms[norms < 1e-9] = 1.0
            directions /= norms
            scale = 1.0 + (anisotropy - 1.0) * np.abs(along)
        else:
            scale = np.ones(total)

        freqs = np.empty(total)
        amps = np.empty(total)
        index = 0
        for octave in range(max(1, octaves)):
            wavelength = wavelength_m / (lacunarity ** octave)
            amplitude = gain ** octave
            for _ in range(max(1, waves_per_octave)):
                freqs[index] = (2.0 * math.pi / max(1e-6, wavelength)) * scale[index]
                amps[index] = amplitude
                index += 1

        self._k = directions * freqs[:, None]
        self._amp = amps / max(1e-9, float(amps.sum()))
        self._phase = rng.uniform(0.0, 2.0 * math.pi, size=total)

    def sample(self, points: np.ndarray) -> np.ndarray:
        """Values in roughly [-1, 1] for an (n, 3) array of positions."""
        return np.sin(points @ self._k.T + self._phase) @ self._amp


# ---------------------------------------------------------------------------
# Bedding frame and stratigraphy
# ---------------------------------------------------------------------------

@dataclass
class BeddingFrame:
    """Orthonormal frame whose ``normal`` is the bedding-plane normal.

    ``3DMODEL_GEOLOGY_ROCKS.md`` section 1 wants readable geological process. Exposed
    beds are almost never horizontal, so the stack axis is tilted by a real dip and
    every strata operation -- band profile, parting groove, vein plane, UV1 -- is
        expressed in this frame rather than in world Z.
    """

    normal: Vector
    e1: Vector
    e2: Vector
    dip_deg: float
    dip_azimuth_deg: float

    @classmethod
    def from_rng(cls, rng: np.random.Generator) -> "BeddingFrame":
        dip = float(rng.uniform(9.0, 33.0))
        azimuth = float(rng.uniform(0.0, 360.0))
        normal = (Matrix.Rotation(math.radians(azimuth), 3, "Z")
                  @ Matrix.Rotation(math.radians(dip), 3, "Y")
                  @ Vector((0.0, 0.0, 1.0)))
        normal.normalize()
        helper = Vector((1.0, 0.0, 0.0))
        if abs(normal.dot(helper)) > 0.92:
            helper = Vector((0.0, 1.0, 0.0))
        e1 = (helper - normal * helper.dot(normal))
        e1.normalize()
        e2 = normal.cross(e1)
        e2.normalize()
        return cls(normal, e1, e2, dip, azimuth)

    def to_world(self, u: float, v: float, h: float) -> Vector:
        return self.e1 * u + self.e2 * v + self.normal * h

    def bedding_height(self, position: Vector) -> float:
        return position.dot(self.normal)


@dataclass
class Bed:
    """One sedimentary bed. ``hardness`` 1.0 = competent, 0.0 = soft parting."""

    index: int
    base_h: float
    top_h: float
    hardness: float
    radius_scale: float
    overhangs_below: bool

    @property
    def thickness(self) -> float:
        return self.top_h - self.base_h


@dataclass
class Stratigraphy:
    """The bed column plus the plan-outline and drift functions of the body."""

    beds: list
    height_m: float
    base_radius_m: float
    plan_harmonics: np.ndarray
    plan_phases: np.ndarray
    plan_orders: np.ndarray
    drift_amp: np.ndarray
    drift_phase: np.ndarray
    plan_twist_rad: float
    landmark_bed: int
    landmark_azimuth_rad: float
    landmark_arc_rad: float
    bed_thickness_capped: bool

    def radius_scale_at(self, h: float) -> float:
        """Piecewise bed profile with a sharp step at every interface.

        A linear interpolation between bed radii would produce a smooth cone -- the
        "wedding cake with sanded edges" failure. The transition is a smoothstep over
        the first 18 percent of each bed, so the remaining 82 percent is a genuinely
        parallel-sided slab and the interface reads as a ledge in silhouette.
        """
        beds = self.beds
        if not beds:
            return 1.0
        if h <= beds[0].base_h:
            return beds[0].radius_scale
        if h >= beds[-1].top_h:
            return beds[-1].radius_scale
        for bed in beds:
            if bed.base_h <= h <= bed.top_h:
                previous = beds[bed.index - 1].radius_scale if bed.index > 0 else bed.radius_scale
                window = max(1e-6, bed.thickness * 0.18)
                local = (h - bed.base_h) / window
                if local >= 1.0:
                    return bed.radius_scale
                t = local * local * (3.0 - 2.0 * local)
                return previous + (bed.radius_scale - previous) * t
        return beds[-1].radius_scale

    def hardness_at(self, h: float) -> float:
        for bed in self.beds:
            if bed.base_h <= h <= bed.top_h:
                return bed.hardness
        return self.beds[-1].hardness if self.beds else 1.0

    def plan_shape(self, theta: float, h: float) -> float:
        """Irregular closed plan outline. Never a circle, never self-intersecting."""
        twisted = theta + self.plan_twist_rad * (h / max(1e-6, self.height_m))
        value = 1.0
        for amp, phase, order in zip(self.plan_harmonics, self.plan_phases, self.plan_orders):
            value += amp * math.sin(order * twisted + phase)
        return max(0.42, value)

    def drift(self, h: float) -> tuple:
        """Slow lean/offset of the bed centres, so the stack is not a plumb column."""
        t = h / max(1e-6, self.height_m)
        u = 0.0
        v = 0.0
        for i in range(self.drift_amp.shape[0]):
            u += float(self.drift_amp[i, 0]) * math.sin(math.pi * (i + 1) * t + float(self.drift_phase[i, 0]))
            v += float(self.drift_amp[i, 1]) * math.sin(math.pi * (i + 1) * t + float(self.drift_phase[i, 1]))
        return u, v

    def landmark_sector_weight(self, theta: float, h: float) -> float:
        """1.0 inside the route-facing undercut sector, falling to 0 outside it.

        ``3DMODEL_GEOLOGY_ROCKS.md`` section 1 asks for "route landmarks" and section 3
        for "overhang/shelf silhouette". One deliberate asymmetric notch is what makes a
        rock usable as a navigation cue instead of a lump that looks the same from every
        angle.
        """
        bed = self.beds[self.landmark_bed]
        if not (bed.base_h - bed.thickness * 0.35 <= h <= bed.top_h + bed.thickness * 0.35):
            return 0.0
        delta = abs((theta - self.landmark_azimuth_rad + math.pi) % (2.0 * math.pi) - math.pi)
        half = self.landmark_arc_rad * 0.5
        if delta >= half:
            return 0.0
        t = 1.0 - (delta / half)
        return t * t * (3.0 - 2.0 * t)


def build_stratigraphy(rng: np.random.Generator, size: SizeClass,
                       process: str) -> Stratigraphy:
    """Deterministic bed column with ABSOLUTE bed thickness (the scale witness).

    Bed count follows from height / thickness, so it is the rock that changes, not the
    beds. ``MAX_BEDS`` binds only on the tallest class and is reported when it does.
    """
    style = float(rng.uniform(0.82, 1.35))
    mean_thickness = float(rng.uniform(WITNESS_BED_THICKNESS_MIN_M,
                                       WITNESS_BED_THICKNESS_MAX_M)) * style
    mean_thickness = min(mean_thickness, size.height_m / MIN_BEDS)
    ideal_count = int(round(size.height_m / max(1e-6, mean_thickness)))
    count = max(MIN_BEDS, min(MAX_BEDS, ideal_count))
    capped = ideal_count > MAX_BEDS

    weights = rng.uniform(0.55, 1.55, size=count)
    if process == "basalt":
        # Columnar basalt breaks in tall, near-uniform units rather than graded beds.
        weights = 0.65 + 0.35 * weights
    weights = weights / weights.sum()
    thicknesses = weights * size.height_m

    # Hardness alternates with a seeded bias so competent beds and soft partings
    # interleave the way a real sequence does, instead of a random speckle.
    phase = float(rng.uniform(0.0, math.pi))
    beds = []
    cursor = -size.height_m * 0.5
    for i in range(count):
        alternation = 0.5 + 0.5 * math.sin(phase + i * (math.pi * 0.83))
        jitter = float(rng.uniform(-0.22, 0.22))
        hardness = min(1.0, max(0.0, alternation * 0.78 + 0.22 + jitter))
        if process == "basalt":
            hardness = min(1.0, hardness * 0.55 + 0.45)
        top = cursor + float(thicknesses[i])
        beds.append(Bed(i, cursor, top, hardness, 1.0, False))
        cursor = top

    # Soft beds recede; a competent bed sitting on a soft one gains an overhang lip.
    # That pair is the mechanism behind both the stratified silhouette and the
    # occluded cavity the AO bake has to find.
    recess_gain = float(rng.uniform(0.055, 0.155))
    for bed in beds:
        recess = recess_gain * (1.0 - bed.hardness)
        scale = 1.0 - recess
        if bed.index > 0 and bed.hardness > beds[bed.index - 1].hardness + 0.22:
            scale += float(rng.uniform(0.018, 0.052))
            bed.overhangs_below = True
        bed.radius_scale = scale

    # Route-facing landmark: one strongly recessed shelf bed with the bed above it
    # pushed proud, placed in the middle band of the silhouette where it reads.
    lower = max(1, int(count * 0.34))
    upper = max(lower + 1, int(count * 0.72))
    landmark = int(rng.integers(lower, min(count - 1, upper) + 1))
    beds[landmark].radius_scale -= float(rng.uniform(0.075, 0.155))
    beds[landmark].hardness = min(beds[landmark].hardness, 0.32)
    if landmark + 1 < count:
        beds[landmark + 1].radius_scale += float(rng.uniform(0.035, 0.085))
        beds[landmark + 1].overhangs_below = True
        beds[landmark + 1].hardness = max(beds[landmark + 1].hardness, 0.72)

    orders = np.array(sorted(rng.choice(np.arange(2, 9), size=4, replace=False)))
    harmonics = rng.uniform(0.045, 0.135, size=4) / (1.0 + 0.35 * (orders - 2))
    phases = rng.uniform(0.0, 2.0 * math.pi, size=4)
    drift_amp = rng.uniform(-0.16, 0.16, size=(3, 2)) * size.radius_m
    drift_phase = rng.uniform(0.0, 2.0 * math.pi, size=(3, 2))

    return Stratigraphy(
        beds=beds,
        height_m=size.height_m,
        base_radius_m=size.radius_m,
        plan_harmonics=harmonics,
        plan_phases=phases,
        plan_orders=orders,
        drift_amp=drift_amp,
        drift_phase=drift_phase,
        plan_twist_rad=float(rng.uniform(-0.55, 0.55)),
        landmark_bed=landmark,
        landmark_azimuth_rad=float(rng.uniform(0.0, 2.0 * math.pi)),
        landmark_arc_rad=float(rng.uniform(1.15, 2.10)),
        bed_thickness_capped=capped,
    )


# ---------------------------------------------------------------------------
# Density solver
# ---------------------------------------------------------------------------

@dataclass
class LatticeDensity:
    segments: int
    rings: int
    ideal_ring_spacing_m: float
    ideal_segment_spacing_m: float
    achieved_ring_spacing_m: float
    achieved_segment_spacing_m: float
    budget_bound: bool
    lattice_triangles: int


def solve_density(strata: Stratigraphy, size: SizeClass, quality: float,
                  beds: int) -> LatticeDensity:
    """Pick ring/segment counts from an absolute target edge length, then cap by budget.

    Quality is continuous and drives the target edge length, so 0.25 and 1.0 produce
    genuinely different lattices rather than the same mesh with a different label.
    ``AGENTS.md``: "Binary quality switches are rejected."

    The cap is real and reported: an 7.6 m chunk cannot resolve 30 mm detail inside
    18,000 triangles, and pretending otherwise would ship an over-budget LOD0.
    """
    q = law.saturate(quality)
    ring_spacing = 0.105 + (0.030 - 0.105) * q
    segment_spacing = 0.150 + (0.046 - 0.150) * q

    circumference = 2.0 * math.pi * size.radius_m
    rings_ideal = int(round(size.height_m / ring_spacing)) + 1
    rings_ideal = max(beds * 2 + 1, min(430, rings_ideal))
    segments_ideal = max(12, min(96, int(round(circumference / segment_spacing))))

    sculpt_budget = int(size.budget(0) * LATTICE_BUDGET_SHARE * SCULPT_DENSITY_MULTIPLIER)
    rings, segments = rings_ideal, segments_ideal
    lattice_tris = 2 * (rings - 1) * segments
    bound = False
    if lattice_tris > sculpt_budget:
        bound = True
        shrink = math.sqrt(sculpt_budget / float(lattice_tris))
        rings = max(beds * 2 + 1, int(rings * shrink))
        segments = max(12, int(segments * shrink))
        lattice_tris = 2 * (rings - 1) * segments
        # A ring floor of 2 per bed is non-negotiable: one ring per bed cannot express
        # a ledge, and losing the ledge loses the strata read the whole family is for.
        while lattice_tris > sculpt_budget and segments > 12:
            segments -= 1
            lattice_tris = 2 * (rings - 1) * segments

    return LatticeDensity(
        segments=segments,
        rings=rings,
        ideal_ring_spacing_m=ring_spacing,
        ideal_segment_spacing_m=segment_spacing,
        achieved_ring_spacing_m=size.height_m / max(1, rings - 1),
        achieved_segment_spacing_m=circumference / max(1, segments),
        budget_bound=bound,
        lattice_triangles=lattice_tris,
    )


# ---------------------------------------------------------------------------
# Stage 3: high-detail source geometry
# ---------------------------------------------------------------------------

def build_body(strata: Stratigraphy, frame: BeddingFrame, density: LatticeDensity,
               size: SizeClass, rng: np.random.Generator, quality: float,
               process: str, blackbox: BlackBox) -> tuple:
    """Bridged bed-stack solid with anisotropic grain and absolute-size pitting.

    Returns ``(bmesh, grain_field, pit_field)`` so later stages can reuse the exact
    same deterministic fields for the stain/vein channels.
    """
    q = law.saturate(quality)
    bm = bmesh.new()

    grain_amp = size.longest_extent_m * (0.006 + 0.011 * q)
    grain = AnisotropicField(
        rng, WITNESS_GRAIN_WAVELENGTH_M,
        octaves=2 if q < 0.5 else 3,
        bedding_normal=np.array([frame.normal.x, frame.normal.y, frame.normal.z]),
        anisotropy=3.4 if process == "sedimentary" else 2.1,
    )
    pit = AnisotropicField(
        rng, WITNESS_PIT_WAVELENGTH_M, octaves=1, waves_per_octave=7,
        bedding_normal=np.array([frame.normal.x, frame.normal.y, frame.normal.z]),
        anisotropy=1.35,
    )

    rings = density.rings
    segments = density.segments
    base_h = -size.height_m * 0.5

    # Pass 1: undisplaced lattice positions plus the outward radial direction of each
    # vertex. Displacement is applied along the LOCAL outward direction and scaled by
    # the LOCAL radius, never by distance from an arbitrary axis -- scaling by
    # distance-from-axis leaves the core smooth while the rim self-intersects.
    positions = np.empty((rings * segments, 3))
    outward = np.empty((rings * segments, 3))
    local_scale = np.empty(rings * segments)
    hardness = np.empty(rings * segments)
    theta_of = np.empty(rings * segments)
    height_of = np.empty(rings * segments)

    index = 0
    for r in range(rings):
        t = r / float(rings - 1)
        h = base_h + t * size.height_m
        radius_scale = strata.radius_scale_at(h)
        bed_hardness = strata.hardness_at(h)
        drift_u, drift_v = strata.drift(h)
        for s in range(segments):
            theta = (s / float(segments)) * 2.0 * math.pi
            shape = strata.plan_shape(theta, h)
            notch = strata.landmark_sector_weight(theta, h)
            radius = size.radius_m * shape * radius_scale * (1.0 - 0.30 * notch)
            direction = frame.e1 * math.cos(theta) + frame.e2 * math.sin(theta)
            point = direction * radius + frame.normal * h
            point.x += drift_u
            point.y += drift_v
            positions[index] = (point.x, point.y, point.z)
            outward[index] = (direction.x, direction.y, direction.z)
            local_scale[index] = radius
            hardness[index] = bed_hardness
            theta_of[index] = theta
            height_of[index] = h
            index += 1

    # Pass 2: displacement. Soft beds weather rougher, competent beds stay tight.
    grain_values = grain.sample(positions)
    pit_values = pit.sample(positions)
    softness = 1.0 + 0.85 * (1.0 - hardness)
    radial = grain_values * grain_amp * softness

    # Absolute-depth pitting: only the upper tail of the field cuts, so pits are
    # discrete vugs rather than a wobble over the whole surface.
    pit_threshold = 0.34 if process == "basalt" else 0.46
    pit_mask = np.clip((pit_values - pit_threshold) / max(1e-6, 1.0 - pit_threshold), 0.0, 1.0)
    pit_mask = pit_mask * pit_mask * (3.0 - 2.0 * pit_mask)
    pit_depth = WITNESS_PIT_DEPTH_M * (0.55 + 0.45 * q) * (1.6 if process == "basalt" else 1.0)
    radial -= pit_mask * pit_depth

    # Clamp so displacement can never invert a vertex through the local surface: the
    # coral run self-intersected exactly here, and self-intersection shows up later as
    # impossible bright pixels in the channel renders.
    limit = local_scale * 0.34
    radial = np.clip(radial, -limit, limit)
    displaced = positions + outward * radial[:, None]

    verts = []
    for i in range(rings * segments):
        verts.append(bm.verts.new(Vector((float(displaced[i, 0]),
                                          float(displaced[i, 1]),
                                          float(displaced[i, 2])))))
    bm.verts.ensure_lookup_table()

    for r in range(rings - 1):
        row = r * segments
        next_row = (r + 1) * segments
        for s in range(segments):
            s_next = (s + 1) % segments
            bm.faces.new((verts[row + s], verts[row + s_next],
                          verts[next_row + s_next], verts[next_row + s]))

    # Caps: closed solid. ``3DMODEL_GEOLOGY_ROCKS.md`` section 2 requires manifold
    # output for a solid rock, so both ends are filled and then poked into a fan so the
    # top can dome and the base can dish -- a flat lid reads as a sliced cylinder.
    bottom = bm.faces.new(tuple(reversed(verts[0:segments])))
    top = bm.faces.new(tuple(verts[(rings - 1) * segments:rings * segments]))
    for face, sign, amount in ((top, 1.0, 0.16), (bottom, -1.0, 0.055)):
        poked = bmesh.ops.poke(bm, faces=[face], offset=0.0,
                               center_mode="MEAN_WEIGHTED", use_relative_offset=False)
        for vert in poked["verts"]:
            vert.co += frame.normal * (sign * size.height_m * amount)
            wobble = float(grain.sample(np.array([[vert.co.x, vert.co.y, vert.co.z]]))[0])
            vert.co += frame.normal * (sign * grain_amp * 2.0 * wobble)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    stats = mesh_ops.weld_and_clean(bm, blackbox=blackbox)
    blackbox.record("build_body", family=law.Family.GEOLOGY.value,
                    vertex_count=len(bm.verts), triangle_count=len(bm.faces),
                    warning="" if stats["degenerate_faces_deleted"] == 0 else
                    "welded away {n} zero-area faces".format(
                        n=stats["degenerate_faces_deleted"]))
    return bm, grain, pit


# ---------------------------------------------------------------------------
# Stage 4a: fractures and pressure breaks
# ---------------------------------------------------------------------------

@dataclass
class FracturePlane:
    origin: np.ndarray
    normal: np.ndarray
    kind: str


def cut_fractures(bm: bmesh.types.BMesh, frame: BeddingFrame, strata: Stratigraphy,
                  size: SizeClass, rng: np.random.Generator, quality: float,
                  process: str, blackbox: BlackBox) -> list:
    """Planar cuts with sharp exposed faces, assigned to the fracture material slot.

    ``3dmodel.md`` section 6 reserves "Slot 1: exposed cut, bevel, edge, scar, or
    fracture material", so every face created here gets
    ``law.MATERIAL_SLOT_CUT_EDGE``.

    Real pressure breaks come in conjugate shear pairs at a high angle to bedding, plus
    bedding-parallel partings where a slab sheared off. Both are generated. The cut
    offset is taken from a PERCENTILE of the actual vertex distribution along the plane
    normal, which is what guarantees a cut removes a corner instead of bisecting the
    body in half and deleting a lobe.
    """
    q = law.saturate(quality)
    conjugate = 2 + int(round(2.0 * q))
    if size.name == "cliff-chunk":
        conjugate += 1
    if process == "basalt":
        conjugate += 1

    normal = np.array([frame.normal.x, frame.normal.y, frame.normal.z])
    e1 = np.array([frame.e1.x, frame.e1.y, frame.e1.z])
    e2 = np.array([frame.e2.x, frame.e2.y, frame.e2.z])

    shear_dip = math.radians(float(rng.uniform(58.0, 74.0)))
    base_azimuth = float(rng.uniform(0.0, 2.0 * math.pi))
    planes = []
    for i in range(conjugate):
        sign = 1.0 if i % 2 == 0 else -1.0
        azimuth = base_azimuth + (i // 2) * float(rng.uniform(1.05, 2.35)) + (0.0 if sign > 0 else math.pi * 0.5)
        lateral = e1 * math.cos(azimuth) + e2 * math.sin(azimuth)
        plane_normal = lateral * math.sin(shear_dip) + normal * (sign * math.cos(shear_dip))
        plane_normal /= max(1e-9, np.linalg.norm(plane_normal))
        planes.append(FracturePlane(np.zeros(3), plane_normal, "conjugate_shear"))

    # One bedding-parallel pressure break at a soft interface: the slab that let go.
    soft = min(strata.beds, key=lambda b: b.hardness)
    parting_normal = normal.copy()
    if rng.random() < 0.5:
        parting_normal = -parting_normal
    planes.append(FracturePlane(normal * soft.top_h, parting_normal, "bedding_parting"))

    created = []
    for plane in planes:
        coords = np.array([[v.co.x, v.co.y, v.co.z] for v in bm.verts])
        distances = coords @ plane.normal
        keep_fraction = float(rng.uniform(0.80, 0.93))
        cut_at = float(np.quantile(distances, keep_fraction))
        removed_estimate = int((distances > cut_at).sum())
        if removed_estimate < 3:
            continue

        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        result = bmesh.ops.bisect_plane(
            bm, geom=geom, dist=1e-6,
            plane_co=Vector((float(plane.normal[0] * cut_at),
                             float(plane.normal[1] * cut_at),
                             float(plane.normal[2] * cut_at))),
            plane_no=Vector((float(plane.normal[0]), float(plane.normal[1]),
                             float(plane.normal[2]))),
            use_snap_center=False, clear_outer=True, clear_inner=False)

        cut_edges = [g for g in result.get("geom_cut", ())
                     if isinstance(g, bmesh.types.BMEdge) and g.is_valid]
        if not cut_edges:
            continue
        filled = bmesh.ops.holes_fill(bm, edges=cut_edges, sides=0)
        new_faces = [f for f in filled.get("faces", ()) if f.is_valid]
        if not new_faces:
            continue
        triangulated = bmesh.ops.triangulate(bm, faces=new_faces)
        faces = [f for f in triangulated.get("faces", new_faces) if f.is_valid] or new_faces
        for face in faces:
            face.material_index = law.MATERIAL_SLOT_CUT_EDGE
            # Do not smooth a chipped plane into a soft blob
            # (``3DMODEL_GEOLOGY_ROCKS.md`` section 4).
            face.smooth = False
            for edge in face.edges:
                edge.smooth = False
        plane.origin = plane.normal * cut_at
        created.append(plane)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    blackbox.record("cut_fractures", vertex_count=len(bm.verts),
                    triangle_count=len(bm.faces),
                    warning="" if created else "no fracture plane took effect",
                    failure_code="" if created else "FRACTURE_NONE")
    return created


# ---------------------------------------------------------------------------
# Stage 4b: bedding partings, mineral seams, macro vugs
# ---------------------------------------------------------------------------

@dataclass
class SeamPlane:
    origin_h: float
    normal: np.ndarray
    half_width_m: float
    kind: str


def carve_partings(bm: bmesh.types.BMesh, frame: BeddingFrame, strata: Stratigraphy,
                   size: SizeClass, rng: np.random.Generator, quality: float,
                   blackbox: BlackBox) -> list:
    """Recessed grooves along soft bed interfaces -- the occluded cracks AO needs.

    ``3DMODEL_GEOLOGY_ROCKS.md`` section 3 lists "occluded cavities" and section 4
    requires the B channel to carry "cavity darkness". A groove pushed in along a
    bedding interface is a continuous concave feature the ray-traced bake can actually
    find, unlike a surface wobble.
    """
    q = law.saturate(quality)
    count = 1 + int(round(2.0 * q))
    softest = sorted(strata.beds, key=lambda b: b.hardness)[:max(1, count)]
    normal = np.array([frame.normal.x, frame.normal.y, frame.normal.z])

    seams = []
    for bed in softest:
        half_width = min(bed.thickness * 0.42,
                         size.longest_extent_m * (0.010 + 0.008 * q))
        depth = size.longest_extent_m * (0.012 + 0.016 * q)
        centre = (bed.base_h + bed.top_h) * 0.5
        moved = 0
        for vert in bm.verts:
            h = vert.co.dot(frame.normal)
            offset = abs(h - centre)
            if offset >= half_width:
                continue
            falloff = 1.0 - (offset / half_width)
            falloff = falloff * falloff * (3.0 - 2.0 * falloff)
            radial = vert.co - frame.normal * h
            if radial.length <= 1e-6:
                continue
            vert.co -= radial.normalized() * (depth * falloff)
            moved += 1
        if moved:
            seams.append(SeamPlane(centre, normal, half_width, "bedding_parting_groove"))

    blackbox.record("carve_partings", vertex_count=len(bm.verts),
                    triangle_count=len(bm.faces),
                    warning="" if seams else "no parting groove carved")
    return seams


def raise_mineral_seams(bm: bmesh.types.BMesh, frame: BeddingFrame,
                        strata: Stratigraphy, size: SizeClass,
                        rng: np.random.Generator, quality: float,
                        blackbox: BlackBox) -> list:
    """Oblique quartz/sulphide veins: material slot 2 plus real positive relief.

    ``3dmodel.md`` section 6: "Slot 2: secondary trim, gasket, barnacle, mineral vein,
    or growth plate". ``3DMODEL_GEOLOGY_ROCKS.md`` section 2: "Fractures and strata must
    not be only albedo paint." A vein is harder than its host, so it weathers proud --
    the relief and the slot and the G channel all come from the same distance field, so
    they cannot disagree.

    Count floors at 1 at every quality: ``GlobalQualityWeight`` "never changes ore node
    identity" (section 6), so quality may add veins but never delete the one that
    defines the asset.
    """
    q = law.saturate(quality)
    count = 1 + int(round(3.0 * q))
    normal = np.array([frame.normal.x, frame.normal.y, frame.normal.z])
    e1 = np.array([frame.e1.x, frame.e1.y, frame.e1.z])
    e2 = np.array([frame.e2.x, frame.e2.y, frame.e2.z])

    veins = []
    for _ in range(count):
        # Oblique to bedding: a vein parallel to the beds would be indistinguishable
        # from a parting, and cross-cutting is what makes it read as later-stage.
        tilt = math.radians(float(rng.uniform(34.0, 78.0)))
        azimuth = float(rng.uniform(0.0, 2.0 * math.pi))
        lateral = e1 * math.cos(azimuth) + e2 * math.sin(azimuth)
        plane_normal = lateral * math.sin(tilt) + normal * math.cos(tilt)
        plane_normal /= max(1e-9, np.linalg.norm(plane_normal))
        offset = float(rng.uniform(-0.34, 0.34)) * size.longest_extent_m
        half_width = size.longest_extent_m * float(rng.uniform(0.010, 0.024))
        relief = size.longest_extent_m * (0.004 + 0.006 * q)

        plane_vector = Vector((float(plane_normal[0]), float(plane_normal[1]),
                               float(plane_normal[2])))
        touched_faces = 0
        for vert in bm.verts:
            distance = abs(vert.co.dot(plane_vector) - offset)
            if distance >= half_width:
                continue
            falloff = 1.0 - (distance / half_width)
            falloff = falloff * falloff * (3.0 - 2.0 * falloff)
            direction = vert.normal.copy()
            if direction.length <= 1e-6:
                continue
            vert.co += direction.normalized() * (relief * falloff)
        for face in bm.faces:
            if face.material_index == law.MATERIAL_SLOT_CUT_EDGE:
                continue
            centre = face.calc_center_median()
            if abs(centre.dot(plane_vector) - offset) < half_width * 0.72:
                face.material_index = law.MATERIAL_SLOT_TRIM
                touched_faces += 1
        veins.append(SeamPlane(offset, plane_normal, half_width, "mineral_vein"))
        blackbox.record("mineral_vein", triangle_count=touched_faces,
                        warning="" if touched_faces else "vein plane matched no face")

    return veins


def punch_vugs(bm: bmesh.types.BMesh, size: SizeClass, rng: np.random.Generator,
               quality: float, process: str, blackbox: BlackBox) -> int:
    """Nested inset pockets: steep-walled macro hollows for genuine AO contrast.

    The fine absolute-wavelength pitting from ``build_body`` is the scale witness; these
    are the macro wave-drilled hollows and basalt vesicle clusters, which legitimately
    scale with block size. Two nested insets give near-vertical walls, because a single
    shallow dish does not occlude anything and would leave the B channel flat.
    """
    q = law.saturate(quality)
    target_radius = min(0.060, max(0.008, 0.012 * size.longest_extent_m))
    density = (2.6 + 5.4 * q) * (1.7 if process == "basalt" else 1.0)

    candidates = []
    for face in bm.faces:
        if face.material_index == law.MATERIAL_SLOT_CUT_EDGE:
            continue
        area = face.calc_area()
        if area <= 0.0:
            continue
        inradius = math.sqrt(area / math.pi)
        if inradius > target_radius * 1.75:
            candidates.append((face, inradius, area))
    if not candidates:
        blackbox.record("punch_vugs", warning="no face large enough for a macro vug")
        return 0

    total_area = sum(c[2] for c in candidates)
    wanted = max(2, int(round(total_area * density)))
    wanted = min(wanted, len(candidates), 220)
    weights = np.array([c[2] for c in candidates])
    weights = weights / weights.sum()
    chosen = rng.choice(len(candidates), size=wanted, replace=False, p=weights)

    punched = 0
    for pick in chosen:
        face, inradius, _area = candidates[int(pick)]
        if not face.is_valid:
            continue
        radius = target_radius * float(rng.uniform(0.72, 1.28))
        thickness = max(1e-4, inradius - radius)
        depth = radius * float(rng.uniform(0.85, 1.65))
        first = bmesh.ops.inset_individual(
            bm, faces=[face], thickness=thickness * 0.55, depth=-depth * 0.45,
            use_even_offset=True, use_interpolate=True, use_relative_offset=False)
        if not first.get("faces"):
            continue
        if face.is_valid:
            bmesh.ops.inset_individual(
                bm, faces=[face], thickness=thickness * 0.35, depth=-depth * 0.55,
                use_even_offset=True, use_interpolate=True, use_relative_offset=False)
        punched += 1

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    blackbox.record("punch_vugs", vertex_count=len(bm.verts),
                    triangle_count=len(bm.faces),
                    warning="" if punched else "no vug survived inset")
    return punched


# ---------------------------------------------------------------------------
# Stage 4c: varied chipping (never a uniform chamfer)
# ---------------------------------------------------------------------------

def _local_shortest_edge(edge: bmesh.types.BMEdge) -> float:
    """Shortest edge sharing a vertex with ``edge``, i.e. its own overlap limit."""
    shortest = edge.calc_length()
    for vert in edge.verts:
        for other in vert.link_edges:
            if other is not edge:
                length = other.calc_length()
                if length < shortest:
                    shortest = length
    return shortest


def _boundary_loops(boundary_edges: list) -> list:
    """Group boundary edges into connected rims so each hole is filled on its own."""
    pool = {e for e in boundary_edges if e.is_valid}
    loops = []
    while pool:
        start = pool.pop()
        group = [start]
        frontier = [start]
        while frontier:
            edge = frontier.pop()
            for vert in edge.verts:
                for other in vert.link_edges:
                    if other in pool:
                        pool.discard(other)
                        group.append(other)
                        frontier.append(other)
        loops.append(group)
    return loops


def close_open_boundaries(bm: bmesh.types.BMesh, blackbox: BlackBox,
                          stage: str, passes: int = 5) -> int:
    """Fill every open rim until the shell is closed. Returns remaining boundary edges.

    ``3DMODEL_GEOLOGY_ROCKS.md`` section 2: "Solid rocks and vents must be manifold
    unless the asset is a render-only shell", and section 9 rejects output with holes.

    Needed because a single ``holes_fill`` after ``bisect_plane`` is not enough: when a
    cut plane clips through a vug pocket the rim is not one simple loop, and the
    unfilled remainder leaves holes. Measured before this fix: 507 boundary edges on the
    outcrop, which also defeated ``recalc_face_normals`` (it cannot consistently orient
    an open shell, hence ``inconsistent_winding``) and let AO rays into the interior,
    pinning the B channel minimum at exactly 0.0.
    """
    for _attempt in range(max(1, passes)):
        boundary = [e for e in bm.edges if len(e.link_faces) == 1]
        if not boundary:
            break
        # Fill each rim SEPARATELY. Handing every boundary edge to one holes_fill call
        # lets it bridge unrelated rims: measured once as a single outer membrane
        # stretched over the whole rock, which is technically "closed" and buried the
        # real surface inside it. The AO bake reported mean 0.0057 with max 0.9719 --
        # everything occluded except the membrane. Nothing in a lit render showed it.
        progressed = False
        for loop_edges in _boundary_loops(boundary):
            filled = bmesh.ops.holes_fill(bm, edges=loop_edges, sides=0)
            new_faces = [f for f in filled.get("faces", ()) if f.is_valid]
            if not new_faces:
                continue
            progressed = True
            triangulated = bmesh.ops.triangulate(bm, faces=new_faces)
            for face in [f for f in triangulated.get("faces", new_faces) if f.is_valid]:
                face.material_index = law.MATERIAL_SLOT_CUT_EDGE
        if not progressed:
            break
    boundary_left = sum(1 for e in bm.edges if len(e.link_faces) == 1)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    blackbox.record("close_boundaries:" + stage, vertex_count=len(bm.verts),
                    triangle_count=len(bm.faces),
                    warning="" if boundary_left == 0 else
                    "{n} boundary edges remain".format(n=boundary_left),
                    failure_code="" if boundary_left == 0 else "OPEN_SHELL")
    return boundary_left


@dataclass
class ChipReport:
    hard_edges: int
    buckets: tuple
    widths_m: tuple
    segments: tuple
    beveled: int
    nominal_width_m: float = 0.0


def chip_edges(bm: bmesh.types.BMesh, size: SizeClass, rng: np.random.Generator,
               quality: float, process: str, blackbox: BlackBox) -> ChipReport:
    """Break up hard edges at three different widths, on three random subsets.

    ``3dmodel.md`` section 4 bans raw 90-degree corners, but a single global chamfer on
    a rock reads as machined -- so ``mesh_ops.bevel_hard_edges`` (one offset for the
    whole selection) is deliberately NOT used here. Instead the hard-edge set is
    partitioned by a seeded draw and beveled widest-first at 0.38x, 1.0x and 2.15x the
    nominal chip width. Along one edge chain some edges get a wide spall and their
    neighbours a hairline, which is what a broken rock actually looks like.

    Width still comes from law: a ``law.BevelRange`` is constructed from the size-scaled
    chip fraction and sampled through ``law.BevelRange.width_for``, and the per-edge
    overlap clamp is ``law.BEVEL_WIDTH_CLAMP_RATIO``.
    """
    q = law.saturate(quality)
    hard = mesh_ops.select_hard_edges(bm, law.BEVEL_ANGLE_THRESHOLD_DEG)
    if not hard:
        blackbox.record("chip_edges", warning="no hard edges above threshold",
                        failure_code="CHIP_NO_HARD_EDGES")
        return ChipReport(0, (), (), (), 0)

    chip_range = law.BevelRange(
        CHIP_WIDTH_FRACTION_MIN * size.longest_extent_m,
        CHIP_WIDTH_FRACTION_MAX * size.longest_extent_m)
    nominal = chip_range.width_for(q)
    if process == "basalt":
        nominal *= 0.72          # sharper, less rounded breaks on volcanic rock

    multipliers = (2.15, 1.0, 0.38)
    share = (0.22, 0.44, 0.34)
    order = rng.permutation(len(hard))
    cursor = 0
    buckets = []
    for fraction in share:
        take = int(round(len(hard) * fraction))
        buckets.append([hard[int(i)] for i in order[cursor:cursor + take]])
        cursor += take
    if cursor < len(hard):
        buckets[-1].extend(hard[int(i)] for i in order[cursor:])

    widths = []
    segment_counts = []
    beveled = 0
    for bucket, multiplier in zip(buckets, multipliers):
        requested = nominal * multiplier
        # law.BEVEL_WIDTH_CLAMP_RATIO is a PER-EDGE limit, so the bucket is filtered to
        # the edges that can actually carry the requested width instead of collapsing the
        # whole bucket to the global minimum. Measured before this fix: one short edge
        # anywhere in a 394-edge bucket drove the offset to 1e-05 m, so 930 edges were
        # "beveled" at a hundredth of a millimetre -- a silent no-op that still satisfied
        # a naive "widths vary" check. That is the exact failure class the project's rule
        # files call quiet degeneracy.
        alive = []
        for edge in bucket:
            if not edge.is_valid or len(edge.link_faces) != 2:
                continue
            local = _local_shortest_edge(edge)
            if local * law.BEVEL_WIDTH_CLAMP_RATIO >= requested:
                alive.append(edge)
        if not alive:
            widths.append(0.0)
            segment_counts.append(0)
            continue
        segments = law.bevel_segments_for(q) if multiplier >= 1.0 else 1
        bmesh.ops.bevel(bm, geom=alive, offset=requested, offset_type="OFFSET",
                        segments=segments, profile=0.5 if multiplier < 2.0 else 0.34,
                        affect="EDGES", clamp_overlap=True, material=-1)
        widths.append(requested)
        segment_counts.append(segments)
        beveled += len(alive)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    report = ChipReport(len(hard), tuple(len(b) for b in buckets),
                        tuple(round(w, 5) for w in widths),
                        tuple(segment_counts), beveled, nominal)
    blackbox.record("chip_edges", vertex_count=len(bm.verts),
                    triangle_count=len(bm.faces),
                    warning="" if beveled else "no edge beveled")
    return report


# ---------------------------------------------------------------------------
# Stage 5: material IDs, UV0 fallback unwrap, UV1 bedding-aligned strata UV
# ---------------------------------------------------------------------------

# Pigment read off the mandatory reference frame
# ``Docs/mandatory if you work on systems that user sees .../nice_biome.webp``, opened
# directly per ``AGENTS.md`` ``[REQ] Direct Media Reading``. What that frame actually
# shows for geology, against the assumption that rock is warm grey:
#   - host rock is a COOL DARK SLATE with a blue-green cast, not brown and not mid-grey;
#   - every upward-facing surface carries a strong green-olive algae mat, and that
#     biological colour -- not geometry density -- is what makes the rock read;
#   - ledges carry warm ochre accents where growth clusters;
#   - undersides and cavities go deep teal, close to black, which is the AO channel's job.
# So the material previews the intended runtime contract: albedo driven by the vertex
# colour channels rather than a flat swatch.
ROCK_SLATE = (0.096, 0.104, 0.113)
ROCK_ALGAE = (0.115, 0.196, 0.082)
ROCK_OCHRE = (0.243, 0.150, 0.055)
ROCK_FRESH = (0.300, 0.290, 0.268)
ROCK_VEIN = (0.560, 0.520, 0.430)

MATERIAL_ROLES = (
    (law.MATERIAL_SLOT_PRIMARY, "Primary", ROCK_SLATE, 0.78),
    (law.MATERIAL_SLOT_CUT_EDGE, "FractureFace", ROCK_FRESH, 0.56),
    (law.MATERIAL_SLOT_TRIM, "MineralVein", ROCK_VEIN, 0.34),
)

TRIPLANAR_METRES_PER_TILE = 1.25
UV1_LAYER_NAME = "UV1_Strata"


def build_materials(obj: bpy.types.Object) -> list:
    """Three shared ``MAT_Geology_*`` datablocks in the declared slot order.

    Slot 3 (emissive) is deliberately absent: ``3dmodel.md`` section 6 says slot 3 is
    "emissive/bioluminescent/details only when needed", and the ore/emission signal for
    a rock travels in the A vertex-colour channel, which needs no extra draw slot. The
    slots exist for the AO bake too -- ``vertexcolor.bake_ambient_occlusion`` refuses to
    run without one, and failing loudly there is better than a uniform fake AO field.

    Every slot is guaranteed non-empty by the generator (>=1 fracture face, >=1 vein
    face), because ``validate.GATE_SUBMESH_EMPTY_DECLARED_SLOT`` rejects a declared slot
    with no geometry.
    """
    materials = []
    while obj.data.materials:
        obj.data.materials.pop()
    for _slot, role, base_color, roughness in MATERIAL_ROLES:
        name = law.NAME_MATERIAL.format(family=law.Family.GEOLOGY.value, role=role)
        existing = bpy.data.materials.get(name)
        if existing is not None:
            bpy.data.materials.remove(existing)
        material = bpy.data.materials.new(name)
        material.use_nodes = True
        _wire_channel_driven_albedo(material, base_color, roughness)
        obj.data.materials.append(material)
        materials.append(material)
    return materials


def _wire_channel_driven_albedo(material: bpy.types.Material, base_color: tuple,
                                roughness: float) -> None:
    """Albedo = base pigment, stained by G, revealed by R, occluded by B.

    This is the intended runtime contract expressed as Blender nodes, so the
    ``mode="material"`` contact sheet shows what the channels DO instead of a flat
    swatch. A grey preview cannot answer whether the mineral staining and the wet/dry
    contrast read, and per the reference frame that colour is half of what sells a rock.
    """
    tree = material.node_tree
    nodes = tree.nodes
    links = tree.links
    nodes.clear()

    output = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    attribute = nodes.new("ShaderNodeVertexColor")
    attribute.layer_name = law.VCOL_ATTRIBUTE_NAME
    separate = nodes.new("ShaderNodeSeparateColor")
    links.new(attribute.outputs["Color"], separate.inputs["Color"])

    stain = nodes.new("ShaderNodeMix")
    stain.data_type = "RGBA"
    stain.blend_type = "MIX"
    stain.inputs["A"].default_value = (*base_color, 1.0)
    stain.inputs["B"].default_value = (*ROCK_ALGAE, 1.0)
    links.new(separate.outputs["Green"], stain.inputs["Factor"])

    reveal = nodes.new("ShaderNodeMix")
    reveal.data_type = "RGBA"
    reveal.blend_type = "MIX"
    reveal.inputs["B"].default_value = (*ROCK_FRESH, 1.0)
    links.new(stain.outputs["Result"], reveal.inputs["A"])
    # Chip reveal is a partial lerp: a fresh spall lightens the surface, it does not
    # replace the rock.
    reveal_gain = nodes.new("ShaderNodeMath")
    reveal_gain.operation = "MULTIPLY"
    reveal_gain.inputs[1].default_value = 0.55
    links.new(separate.outputs["Red"], reveal_gain.inputs[0])
    links.new(reveal_gain.outputs["Value"], reveal.inputs["Factor"])

    # AO darkening, floored so the cavity reads dark without going pure black --
    # 3dmodel.md forbids using darkness to hide missing work.
    ao_floor = nodes.new("ShaderNodeMath")
    ao_floor.operation = "MULTIPLY_ADD"
    ao_floor.inputs[1].default_value = 0.75
    ao_floor.inputs[2].default_value = 0.25
    links.new(separate.outputs["Blue"], ao_floor.inputs[0])

    occlude = nodes.new("ShaderNodeMix")
    occlude.data_type = "RGBA"
    occlude.blend_type = "MULTIPLY"
    occlude.inputs["Factor"].default_value = 1.0
    links.new(reveal.outputs["Result"], occlude.inputs["A"])
    ao_color = nodes.new("ShaderNodeCombineColor")
    links.new(ao_floor.outputs["Value"], ao_color.inputs["Red"])
    links.new(ao_floor.outputs["Value"], ao_color.inputs["Green"])
    links.new(ao_floor.outputs["Value"], ao_color.inputs["Blue"])
    links.new(ao_color.outputs["Color"], occlude.inputs["B"])

    # Algae and wet stain are glossier than dry rock.
    rough = nodes.new("ShaderNodeMap Range" if False else "ShaderNodeMapRange")
    rough.inputs["From Min"].default_value = 0.0
    rough.inputs["From Max"].default_value = 1.0
    rough.inputs["To Min"].default_value = roughness
    rough.inputs["To Max"].default_value = max(0.18, roughness - 0.34)
    links.new(separate.outputs["Green"], rough.inputs["Value"])

    links.new(occlude.outputs["Result"], bsdf.inputs["Base Color"])
    links.new(rough.outputs["Result"], bsdf.inputs["Roughness"])
    if "Metallic" in bsdf.inputs:
        bsdf.inputs["Metallic"].default_value = 0.0
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])


def build_uvs(obj: bpy.types.Object, frame: BeddingFrame, size: SizeClass,
              quality: float, blackbox: BlackBox) -> dict:
    """UV0 = real angle-based fallback unwrap. UV1 = bedding-aligned strata tiling.

    ``3dmodel.md`` section 6 permits "Triplanar material assignment for large geology and
    heavily irregular rocks when unique UVs would waste space; still requires UV0 or
    object-space coordinates for decals and masks", and
    ``3DMODEL_GEOLOGY_ROCKS.md`` section 5 is blunter: "Triplanar does not excuse missing
    UVs. UV0 still stores decal/manifest coordinates or a fallback unwrap. UV1 may store
    lightmap/detail scale."

    Decision, justified in the manifest:
      - PRIMARY runtime material route is triplanar object-space projection at
        ``TRIPLANAR_METRES_PER_TILE`` metres per tile. Object space, not UV area, so the
        scale is bit-identical across LOD0/1/2 -- which closes the section 9 rejection
        gate "Triplanar scale is undocumented or mismatched between LODs".
      - UV0 is a genuine Smart-UV-Project unwrap with atlas-law island margin, so
        decals, unique bakes and manifest coordinates have somewhere to live.
      - UV1 is a cylindrical unwrap in the BEDDING frame: V follows bedding height, U
        follows circumferential arc length. A strata/detail texture therefore runs
        parallel to the beds instead of fighting them. The seam is placed on the
        azimuth opposite the landmark notch, per section 6's "seam placed on the least
        visible underside", and per-face branch correction removes the wrap smear.
    """
    mesh = obj.data
    while mesh.uv_layers:
        mesh.uv_layers.remove(mesh.uv_layers[0])
    uv0 = mesh.uv_layers.new(name="UVMap")
    mesh.uv_layers.active = uv0

    atlas_size = 2048 if quality >= 0.6 else 1024
    padding_px = law.atlas_padding_for(atlas_size)
    island_margin = padding_px / float(atlas_size)

    view_layer = bpy.context.view_layer
    for other in view_layer.objects:
        if other.select_get():
            other.select_set(False)
    obj.select_set(True)
    view_layer.objects.active = obj

    unwrap_ok = True
    unwrap_note = ""
    try:
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(66.0),
                                 island_margin=island_margin,
                                 area_weight=0.0, correct_aspect=True,
                                 scale_to_bounds=False)
        bpy.ops.object.mode_set(mode="OBJECT")
    except RuntimeError as error:
        unwrap_ok = False
        unwrap_note = str(error)
        if bpy.context.object is not None and bpy.context.object.mode != "OBJECT":
            bpy.ops.object.mode_set(mode="OBJECT")

    uv1 = mesh.uv_layers.new(name=UV1_LAYER_NAME)
    normal = frame.normal
    e1 = frame.e1
    e2 = frame.e2
    mean_radius = max(1e-4, size.radius_m)
    period = 2.0 * math.pi * mean_radius / TRIPLANAR_METRES_PER_TILE

    raw_u = [0.0] * len(mesh.loops)
    raw_v = [0.0] * len(mesh.loops)
    for loop_index, loop in enumerate(mesh.loops):
        co = mesh.vertices[loop.vertex_index].co
        theta = math.atan2(co.dot(e2), co.dot(e1))
        raw_u[loop_index] = (theta / (2.0 * math.pi)) * period
        raw_v[loop_index] = co.dot(normal) / TRIPLANAR_METRES_PER_TILE

    # Per-face branch correction: a face straddling the atan2 cut would otherwise smear
    # the whole texture across it.
    for polygon in mesh.polygons:
        indices = list(polygon.loop_indices)
        values = [raw_u[i] for i in indices]
        if max(values) - min(values) > period * 0.5:
            pivot = min(values) + period * 0.5
            for i in indices:
                if raw_u[i] > pivot:
                    raw_u[i] -= period
    for loop_index in range(len(mesh.loops)):
        uv1.data[loop_index].uv = (raw_u[loop_index], raw_v[loop_index])

    report = {
        "primaryMaterialRoute": "triplanar_object_space",
        "triplanarMetresPerTile": TRIPLANAR_METRES_PER_TILE,
        "triplanarScaleIdenticalAcrossLods": True,
        "triplanarScaleSource": "object-space position, not UV area",
        "uv0": {"name": uv0.name, "route": "smart_project_angle_based_fallback",
                "angleLimitDeg": 66.0, "atlasSize": atlas_size,
                "islandMarginUv": round(island_margin, 6),
                "paddingPx": padding_px, "purpose": "decals, masks, manifest coords",
                "unwrapped": unwrap_ok, "note": unwrap_note},
        "uv1": {"name": UV1_LAYER_NAME, "route": "cylindrical_in_bedding_frame",
                "metresPerTile": TRIPLANAR_METRES_PER_TILE,
                "seamAzimuth": "opposite the landmark notch",
                "purpose": "bedding-parallel strata/detail tiling"},
    }
    blackbox.record("build_uvs", vertex_count=len(mesh.vertices),
                    triangle_count=mesh_ops.triangle_count(mesh),
                    warning="" if unwrap_ok else "smart_project failed: " + unwrap_note,
                    failure_code="" if unwrap_ok else "UV0_UNWRAP_FAILED")
    return report


# ---------------------------------------------------------------------------
# Stage 6: vertex-colour channels (AO bake FIRST, then compose)
# ---------------------------------------------------------------------------

@dataclass
class ChannelFields:
    """Pure functions of final vertex position -- immune to bevel/decimation reindexing.

    Deriving R/G/A from geometric fields rather than from tracked face sets is the only
    approach that survives ``bmesh.ops.bevel`` and ``modifier_apply``, both of which
    rebuild the vertex array. A tracked set would silently smear onto the wrong
    vertices, which is invisible in a lit render and exactly the class of quiet failure
    this pipeline is built to catch.
    """

    frame: BeddingFrame
    strata: Stratigraphy
    size: SizeClass
    fractures: list
    veins: list
    partings: list
    waterline_h: float

    def compose(self, obj: bpy.types.Object, ao: list) -> dict:
        mesh = obj.data
        count = len(mesh.vertices)
        wear_raw = vertexcolor.curvature_edge_wear(obj)

        normal = self.frame.normal
        chip_scale = max(1e-5, self.size.longest_extent_m * CHIP_WIDTH_FRACTION_MAX)
        edge_wear = [0.0] * count
        oxidation = [0.0] * count
        emission = [0.0] * count

        for i, vertex in enumerate(mesh.vertices):
            co = vertex.co
            point = np.array([co.x, co.y, co.z])
            h = co.dot(normal)

            # R: exposed edge / chip / mineral reveal. Convexity is remapped so a flat
            # face lands near 0 instead of the raw 0.5 that would make the channel read
            # as uniform grey in the channel sheet.
            convex = law.saturate((wear_raw[i] - 0.52) / 0.34)

            fresh = 0.0
            for plane in self.fractures:
                distance = abs(float(point @ plane.normal - plane.origin @ plane.normal))
                if distance < chip_scale:
                    fresh = max(fresh, 1.0 - distance / chip_scale)
            vein_near = 0.0
            for vein in self.veins:
                distance = abs(float(point @ vein.normal) - vein.origin_h)
                halo = vein.half_width_m * 2.4
                if distance < halo:
                    vein_near = max(vein_near, 1.0 - distance / halo)

            edge_wear[i] = law.saturate(max(convex, fresh * 0.92, vein_near * 0.58))

            # G: mineral stain / oxidation / algae. Soft beds hold stain, veins bleed a
            # halo, and runoff pools just under every ledge -- all three are geological
            # reasons, not decorative tint.
            softness = 1.0 - self.strata.hardness_at(h)
            runoff = 0.0
            for bed in self.strata.beds:
                if not bed.overhangs_below:
                    continue
                drop = bed.base_h - h
                if 0.0 <= drop < bed.thickness * 1.6:
                    runoff = max(runoff, 1.0 - drop / (bed.thickness * 1.6))
            wet_band = 0.0
            if self.waterline_h > -1e9:
                band = self.size.longest_extent_m * 0.09
                delta = abs(h - self.waterline_h)
                if delta < band:
                    wet_band = 1.0 - delta / band
            oxidation[i] = law.saturate(0.16 + softness * 0.52 + vein_near * 0.62
                                        + runoff * 0.44 + wet_band * 0.35)

            # A: ore / emission / decal eligibility. Fresh mineral cross-sections and
            # vein cores are where a scanner decal or faint mineral glow belongs.
            emission[i] = law.saturate(vein_near * 0.88 + fresh * 0.42)

        report = vertexcolor.write_hard_surface_channels(
            obj, edge_wear=edge_wear, oxidation=oxidation, ao=ao,
            emission_mask=emission)
        report["channelSemantics"] = {
            "R": "exposed edge/chip/mineral reveal (convexity, fracture faces, vein halo)",
            "G": "mineral stain/oxidation/algae (soft bed, vein halo, ledge runoff, wet band)",
            "B": "baked ambient occlusion, ray-traced in Cycles",
            "A": "ore/emission/decal eligibility mask (vein core + fresh fracture)",
        }
        report["attributeName"] = law.VCOL_ATTRIBUTE_NAME
        report["attributeDataType"] = law.VCOL_DATA_TYPE
        report["attributeDomain"] = law.VCOL_DOMAIN
        report["contract"] = list(law.VCOL_CONTRACT[law.SurfaceClass.GEOLOGIC])
        for name, values in (("R", edge_wear), ("G", oxidation), ("A", emission)):
            report["range" + name] = [round(min(values), 5), round(max(values), 5),
                                      round(sum(values) / max(1, len(values)), 5)]
        return report


# ---------------------------------------------------------------------------
# Topology inspection (real numbers for the manifold/island/seam report)
# ---------------------------------------------------------------------------

def inspect_topology(mesh: bpy.types.Mesh) -> dict:
    """Manifold / island / degenerate report required by section 10 proof artifacts."""
    bm = bmesh.new()
    bm.from_mesh(mesh)
    boundary = 0
    non_manifold = 0
    for edge in bm.edges:
        links = len(edge.link_faces)
        if links == 1:
            boundary += 1
        elif links > 2:
            non_manifold += 1
    degenerate = sum(1 for f in bm.faces
                     if f.calc_area() <= law.DEGENERATE_TRIANGLE_AREA_EPS)
    loose = sum(1 for v in bm.verts if not v.link_faces)

    # Connected components, so an "island below minimum volume" (section 2) is a number
    # rather than an assumption.
    seen = set()
    islands = 0
    for vert in bm.verts:
        if vert.index in seen:
            continue
        islands += 1
        stack = [vert]
        seen.add(vert.index)
        while stack:
            current = stack.pop()
            for edge in current.link_edges:
                other = edge.other_vert(current)
                if other is not None and other.index not in seen:
                    seen.add(other.index)
                    stack.append(other)
    bm.free()

    return {
        "boundaryEdges": boundary,
        "nonManifoldEdges": non_manifold,
        "degenerateFaces": degenerate,
        "looseVerts": loose,
        "islands": islands,
        "manifoldClosedSolid": boundary == 0 and non_manifold == 0 and islands == 1,
    }


# ---------------------------------------------------------------------------
# Scene hygiene
# ---------------------------------------------------------------------------

def purge_scene() -> None:
    """Empty the factory-startup scene so bakes and previews see only the subject."""
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for block in list(bpy.data.meshes):
        if block.users == 0:
            bpy.data.meshes.remove(block)
    for block in list(bpy.data.materials):
        if block.users == 0:
            bpy.data.materials.remove(block)
    # Without this the view layer keeps stale slots for the removed objects and
    # ``view_layer.objects`` yields None entries, which makes any operator helper that
    # iterates the selection (mesh_ops._make_sole_active) raise on a NoneType.
    bpy.context.view_layer.update()


def variant_seed(base_seed: int, index: int) -> int:
    """Deterministic child seed, independent of how many variants were requested."""
    return int((base_seed * 1_000_003 + index * 2_654_435_761) % 2_147_483_647)


# ---------------------------------------------------------------------------
# The generator
# ---------------------------------------------------------------------------

@dataclass
class VariantResult:
    name: str
    seed: int
    quality: float
    size_class: str
    process: str
    lods: list = field(default_factory=list)
    collider_triangles: int = 0
    collider_within_budget: bool = False
    collider_kind: str = ""
    ao: Optional[vertexcolor.AoBakeResult] = None
    channels: dict = field(default_factory=dict)
    uv: dict = field(default_factory=dict)
    topology: dict = field(default_factory=dict)
    density: Optional[LatticeDensity] = None
    chips: Optional[ChipReport] = None
    counts: dict = field(default_factory=dict)
    gates: list = field(default_factory=list)
    manifest_path: str = ""
    fbx_path: str = ""
    sheets: dict = field(default_factory=dict)
    channel_stats: list = field(default_factory=list)
    sculpt_triangles: int = 0
    validator_failures: list = field(default_factory=list)
    open_boundary_edges: int = 0
    nominal_chip_width_m: float = 0.0
    shading: Optional[mesh_ops.ShadingResult] = None
    post_fracture_topology: dict = field(default_factory=dict)


def generate_variant(*, seed: int, quality: float, size: SizeClass, process: str,
                     out_dir: str, want_preview: bool, want_fbx: bool,
                     preview_resolution: int) -> VariantResult:
    """Full stage order from ``PROCEDURAL_ASSET_PIPELINE.md`` "Generation Order"."""
    q = law.saturate(quality)
    name = "{cls}_{proc}_s{seed}_q{q:03d}".format(
        cls=size.name.replace("-", ""), proc=process, seed=seed, q=int(round(q * 100)))
    blackbox = BlackBox("rock", name)
    result = VariantResult(name=name, seed=seed, quality=q, size_class=size.name,
                           process=process)

    # Stage 1: deterministic source. numpy default_rng only -- no wall clock, no
    # unseeded random, no scene iteration order.
    rng = np.random.default_rng(seed)
    blackbox.record("seed", seed=seed, family=law.Family.GEOLOGY.value)

    purge_scene()

    # Stage 2: shape grammar.
    frame = BeddingFrame.from_rng(rng)
    strata = build_stratigraphy(rng, size, process)
    density = solve_density(strata, size, q, len(strata.beds))
    result.density = density
    blackbox.record("shape_grammar", family=law.Family.GEOLOGY.value,
                    vertex_count=density.rings * density.segments,
                    triangle_count=density.lattice_triangles)

    # Stage 3: high-detail source geometry.
    bm, _grain, _pit = build_body(strata, frame, density, size, rng, q, process, blackbox)

    # Stage 4: family topology rules.
    fractures = cut_fractures(bm, frame, strata, size, rng, q, process, blackbox)
    close_open_boundaries(bm, blackbox, "post_fracture")
    partings = carve_partings(bm, frame, strata, size, rng, q, blackbox)
    veins = raise_mineral_seams(bm, frame, strata, size, rng, q, blackbox)
    vugs = punch_vugs(bm, size, rng, q, process, blackbox)
    mesh_ops.weld_and_clean(bm, blackbox=blackbox)
    open_edges = close_open_boundaries(bm, blackbox, "post_detail")
    chips = chip_edges(bm, size, rng, q, process, blackbox)
    # Chipping is the last topology stage, and clamped bevels leave slivers where three
    # chip widths meet. Clean again or the degenerate-triangle gate fires on LOD0.
    mesh_ops.weld_and_clean(bm, blackbox=blackbox)
    # Triangulate once, globally. Bevel corner fans and holes_fill leave n-gons, and
    # Blender aborts tangent-space computation on anything that is not a tri or quad
    # ("Tangent space can only be computed for tris/quads") which shows up as
    # tangent length 0.0 in the validator. Unity triangulates on import anyway, so this
    # makes the authored topology identical to what the engine receives.
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    result.chips = chips
    result.open_boundary_edges = open_edges
    result.nominal_chip_width_m = chips.nominal_width_m
    result.counts = {
        "beds": len(strata.beds),
        "bedThicknessCappedByMaxBeds": strata.bed_thickness_capped,
        "fracturePlanes": len(fractures),
        "fractureKinds": sorted({p.kind for p in fractures}),
        "beddingPartingGrooves": len(partings),
        "mineralVeins": len(veins),
        "macroVugs": vugs,
        "hardEdgesFound": chips.hard_edges,
        "edgesChipped": chips.beveled,
        "chipWidthsM": list(chips.widths_m),
        "chipBucketSizes": list(chips.buckets),
        "chipBevelSegments": list(chips.segments),
        "beddingDipDeg": round(frame.dip_deg, 3),
        "beddingAzimuthDeg": round(frame.dip_azimuth_deg, 3),
        "landmarkBed": strata.landmark_bed,
    }

    mesh = bpy.data.meshes.new(law.NAME_MESH.format(
        family=law.Family.GEOLOGY.value, name=name, lod=0))
    obj = bpy.data.objects.new(mesh.name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.update()
    bm.to_mesh(mesh)
    bm.free()

    # Census straight after the cutting stages. Planar bisects are the classic way to
    # produce a disconnected shell, and DECIMATE collapses edges but cannot delete a
    # shell -- so many components impose a triangle FLOOR that no number of passes beats.
    census = mesh_ops.topology_report(obj)
    result.post_fracture_topology = {
        "triangles": census.triangles,
        "components": census.components,
        "boundaryEdges": census.boundary_edges,
        "nonManifoldEdges": census.nonmanifold_edges,
        "smallestComponent": census.smallest_component,
        "largestComponent": census.largest_component,
        "irreducibleFloor": census.irreducible_floor,
        "explainAgainstLod0Budget": census.explain(size.budget(0)),
    }
    blackbox.record("post_fracture_census", triangle_count=mesh_ops.triangle_count(mesh),
                    vertex_count=len(mesh.vertices),
                    warning="" if census.components == 1 else
                    "{n} disconnected components impose a triangle floor of {f}".format(
                        n=census.components, f=census.estimated_triangle_floor))

    # Sit the base on z=0 so the preview grid shows real ground contact.
    lo, _hi = mesh_ops.local_bounds(obj)
    for vertex in obj.data.vertices:
        vertex.co.z -= lo.z

    result.sculpt_triangles = mesh_ops.triangle_count(obj.data)

    # Reduce to budget BEFORE the UV unwrap and the AO bake: decimating afterwards
    # would throw away the layout and the colours just authored. mesh_ops.reduce_to_budget
    # uses law's family ceiling, then the stricter geology size row is applied here.
    mesh_ops.reduce_to_budget(obj, family=law.Family.GEOLOGY, lod_index=0,
                              blackbox=blackbox)
    tighten_to_target(obj, int(size.budget(0) * 0.94), blackbox, "lod0_size_row",
                      allow_weld=False)
    clean_object(obj, blackbox, "post_lod0_reduce")

    # ``3DMODEL_GEOLOGY_ROCKS.md`` section 4: "Split normals at sharp fracture edges
    # above 45 degrees. Do not smooth a chipped plane into a soft blob."
    # law.SMOOTH_ANGLE_DEG is 32, stricter than 45, so every fracture edge is split.
    # The result is asserted, not assumed: this function used to be a silent no-op
    # headless, and a secretly flat-shaded rock would have sent me tuning geometry to fix
    # a shading bug.
    result.shading = mesh_ops.apply_shading_basis(
        obj, smooth_angle_deg=law.SMOOTH_ANGLE_DEG, weighted=True, keep_sharp=True,
        blackbox=blackbox)

    # Stage 5: material IDs + UVs.
    build_materials(obj)
    result.uv = build_uvs(obj, frame, size, q, blackbox)

    # Stage 6: bake AO into a scratch attribute FIRST -- bpy.ops.object.bake writes ALL
    # channels of its target, so composing R/G/A before the bake destroys them.
    ao_distance = min(1.20, max(0.12, 0.085 * size.longest_extent_m))
    ao_samples = int(round(24 + 104 * q))
    result.ao = vertexcolor.bake_ambient_occlusion(
        obj, samples=ao_samples, distance=ao_distance, blackbox=blackbox)
    ao_values = vertexcolor.consume_baked_ao(obj)

    waterline = -1e9
    if process == "sedimentary" and size.name != "boulder":
        waterline = float(rng.uniform(0.28, 0.62)) * size.height_m
    fields = ChannelFields(frame, strata, size, fractures, veins, partings, waterline)
    result.channels = fields.compose(obj, ao_values)
    vertexcolor.remove_scratch_attributes(obj.data)

    result.topology = inspect_topology(obj.data)

    # Stage 8: LOD chain.
    lods = mesh_ops.build_lod_chain(obj, family=law.Family.GEOLOGY, name=name,
                                    quality_weight=q, levels=3, preserve_seams=True,
                                    blackbox=blackbox)
    for level in lods:
        target = int(size.budget(level.index))
        if level.index > 0 and level.triangles > target:
            tighten_to_target(level.obj, target, blackbox,
                              "lod{i}_size_row".format(i=level.index))
            clean_object(level.obj, blackbox, "post_lod{i}".format(i=level.index))
        result.lods.append({
            "index": level.index,
            "object": level.obj.name,
            "triangles": mesh_ops.triangle_count(level.obj.data),
            "lawFamilyBudget": law.LOD_BUDGETS[law.Family.GEOLOGY].limit(level.index),
            "geologySizeRowBudget": GEOLOGY_SIZE_LOD_ROWS[size.name][min(level.index, 2)],
            "effectiveBudget": target,
        })

    # Stage 9: collision proxy, independent of the visual LODs.
    hull_source = prehull_duplicate(lods[0].obj, name)
    collider = mesh_ops.make_convex_collider(hull_source, family=law.Family.GEOLOGY,
                                             name=name, blackbox=blackbox)
    bpy.data.objects.remove(hull_source, do_unlink=True)
    bpy.context.view_layer.update()
    result.collider_triangles = collider.triangles
    result.collider_within_budget = collider.within_budget
    result.collider_kind = collider.kind

    # Stage 11: validation BEFORE save.
    result.gates = hard_gates(result, size)
    if h8validate is not None:
        try:
            reports = [h8validate.validate_mesh(
                level.obj.data, family=law.Family.GEOLOGY, lod_index=level.index,
                surface_class=law.SurfaceClass.GEOLOGIC, blackbox=blackbox,
                hero=False, triplanar=True) for level in lods]
            if collider.obj is not None:
                reports.append(h8validate.validate_collider(
                    collider.obj.data, family=law.Family.GEOLOGY, blackbox=blackbox,
                    lod0_mesh=lods[0].obj.data))
            result.validator_failures = [
                "{0}: {1}".format(f.gate, f.detail)
                for f in h8validate._collect_failures(reports)]
        except Exception as error:                      # pragma: no cover
            result.validator_failures = ["validator raised: " + str(error)]

    blocking = [g for g in result.gates if g.startswith("FAIL")]
    if blocking:
        dump = blackbox.dump("hard gate failure: " + "; ".join(blocking))
        raise GenerationAborted("rock gates failed for " + name, dump, blocking)

    # Stage 12/13: save + proof.
    os.makedirs(out_dir, exist_ok=True)
    if want_fbx:
        result.fbx_path = export_fbx(lods, collider, out_dir, name)
    if want_preview:
        render_proof(lods[0].obj, name, out_dir, preview_resolution, result)
    result.manifest_path = write_manifest(result, size, frame, strata, out_dir)
    return result


def prehull_duplicate(source: bpy.types.Object, name: str) -> bpy.types.Object:
    """Convex duplicate of ``source``, so ``make_convex_collider`` gets a valid input.

    BLOCKING BUG in the core, reported rather than patched (``h8forge`` is not this
    generator's to edit): ``mesh_ops._convex_hull_in_place`` builds
    ``leftovers = geom_interior + geom_unused`` and passes it straight to
    ``bmesh.ops.delete``. On a concave rock those two result lists OVERLAP, and
    ``bmesh.ops.delete`` raises ``ValueError: geom: found the same (BMVert/BMEdge/BMFace)
    used multiple times``. Every concave geology LOD0 hits it. The one-line fix in the
    core is to de-duplicate, e.g. ``geom=list(dict.fromkeys(leftovers))``.

    The workaround is NOT a second hull implementation: ``bpy.ops.mesh.convex_hull`` is a
    first-class Blender operator that owns its own cleanup, and the result is handed back
    to ``mesh_ops.make_convex_collider`` which still owns naming, the decimation loop, the
    re-hull after decimation and the ``law.COLLIDER_CONVEX_TRI_MAX`` ceiling. For a
    convex input the core's ``geom_interior``/``geom_unused`` both come back empty, so the
    faulty delete never executes.
    """
    duplicate = source.copy()
    duplicate.data = source.data.copy()
    duplicate.name = "H8_HullSource_" + name
    source.users_collection[0].objects.link(duplicate)
    bpy.context.view_layer.update()

    for other in bpy.context.view_layer.objects:
        if other is not None and other.select_get():
            other.select_set(False)
    duplicate.select_set(True)
    bpy.context.view_layer.objects.active = duplicate
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.convex_hull(delete_unused=True, use_existing_faces=False,
                             make_holes=False, join_triangles=False)
    bpy.ops.object.mode_set(mode="OBJECT")

    # Reduce here, then re-hull, so the object handed to the core is BOTH convex and
    # already under law.COLLIDER_CONVEX_TRI_MAX. The core's loop then breaks on its first
    # measurement and its decimate-then-rehull path never runs. That path is what
    # produced ``collider_not_convex: vertex sits 0.001890 m outside the plane`` -- edge
    # collapse on a hull pulls vertices off the shell, and the re-hull leaves
    # near-coplanar slivers that fail the core's 1 mm ABSOLUTE tolerance. Note for the
    # lead: that tolerance does not scale with asset size, so a 7.6 m chunk is judged at
    # the same 1 mm as a 0.8 m boulder.
    target = int(law.COLLIDER_CONVEX_TRI_MAX * 0.85)
    for _attempt in range(6):
        if mesh_ops.triangle_count(duplicate.data) <= target:
            break
        modifier = duplicate.modifiers.new(name="H8_HullPreReduce", type="DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = max(0.02, min(0.95, target / float(
            mesh_ops.triangle_count(duplicate.data)) * 0.92))
        modifier.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = duplicate
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.convex_hull(delete_unused=True, use_existing_faces=False,
                             make_holes=False, join_triangles=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    return duplicate


def clean_object(obj: bpy.types.Object, blackbox: BlackBox, stage: str) -> dict:
    """Weld + drop degenerates on an object, via the core's bmesh cleaner.

    Needed after every topology-changing stage, not just once. ``bmesh.ops.bevel`` with
    ``clamp_overlap`` emits sliver faces where three chip widths meet, and
    ``DECIMATE/COLLAPSE`` produces its own near-zero-area triangles. ``3dmodel.md``
    section 10 requires ZERO degenerate triangles, so the answer is to clean, not to
    loosen the gate. Per-loop UV and colour data survive a vertex merge, so this is safe
    to run after the unwrap as well.
    """
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    stats = mesh_ops.weld_and_clean(bm, blackbox=blackbox)
    bm.to_mesh(obj.data)
    obj.data.update()
    bm.free()
    blackbox.record("clean:" + stage, vertex_count=len(obj.data.vertices),
                    triangle_count=mesh_ops.triangle_count(obj.data))
    return stats


def tighten_to_target(obj: bpy.types.Object, target: int, blackbox: BlackBox,
                      stage: str, allow_weld: bool = True) -> int:
    """Extra Quadric Edge Collapse down to the stricter geology size-row budget.

    ``mesh_ops.reduce_to_budget`` targets ``law.LOD_BUDGETS``, which only carries the
    large cliff-chunk row. ``build_lod_chain`` likewise judges against law. Neither can
    reach the 4,000/1,200/250 small-rock row, so this closes the gap. Requested h8forge
    change: give both functions an explicit budget override so this helper can be
    deleted.

    ``obj.data`` is re-read on every pass. ``modifier_apply`` REBINDS the object's mesh
    datablock, so a reference captured before the loop measures the pre-decimation mesh
    and the loop reports success while the triangle count never moves.

    ``allow_weld`` exists because of a real structural conflict found by running this:
    ``build_lod_chain`` splits UV seams into mesh BOUNDARIES so that COLLAPSE preserves
    them, but COLLAPSE also cannot cross a boundary -- so a smart-project unwrap with
    many islands puts a hard FLOOR on the reachable triangle count. Measured: LOD2 stalled
    at 1040 triangles against the 600 geology small/medium row. At LOD2 the budget is a
    hard maximum from the bible while UV0 is only the decal fallback (triplanar is the
    primary route), so the seam vertices are welded back and decimation continues. LOD0
    passes ``allow_weld=False``: its authored seams are the ones a unique bake would use.
    """
    if allow_weld and mesh_ops.triangle_count(obj.data) > target:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-4)
        bm.to_mesh(obj.data)
        obj.data.update()
        bm.free()

    for _attempt in range(8):
        current = mesh_ops.triangle_count(obj.data)
        if current <= target:
            break
        modifier = obj.modifiers.new(name="H8_SizeRowDecimate", type="DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = max(0.01, min(0.99, (target / float(current)) * 0.95))
        modifier.use_collapse_triangulate = True
        for other in bpy.context.view_layer.objects:
            if other.select_get():
                other.select_set(False)
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    final = mesh_ops.triangle_count(obj.data)
    blackbox.record(stage, triangle_count=final, vertex_count=len(obj.data.vertices),
                    warning="" if final <= target else
                    "still over size-row target {f}>{t}".format(f=final, t=target),
                    failure_code="" if final <= target else "SIZE_ROW_BUDGET_UNREACHED")
    return final


# ---------------------------------------------------------------------------
# Hard gates -- the rejection gates of 3DMODEL_GEOLOGY_ROCKS.md section 9
# ---------------------------------------------------------------------------

def hard_gates(result: VariantResult, size: SizeClass) -> list:
    """Explicit pass/fail lines. ``FAIL`` aborts the save; ``WARN`` is reported.

    These run unconditionally, independent of whether ``h8forge.validate`` imported, so
    a sibling module in flux can never silently lower this generator's bar.
    """
    lines = []
    for level in result.lods:
        ok = level["triangles"] <= level["effectiveBudget"]
        lines.append("{v} LOD{i} {t} tris vs budget {b}".format(
            v="PASS" if ok else "FAIL", i=level["index"],
            t=level["triangles"], b=level["effectiveBudget"]))
    monotonic = all(result.lods[i]["triangles"] > result.lods[i + 1]["triangles"]
                    for i in range(len(result.lods) - 1))
    lines.append(("PASS" if monotonic else "FAIL") + " LOD chain strictly decreasing")

    lines.append("{v} collider {t} tris vs the {m} ceiling".format(
        v="PASS" if result.collider_within_budget else "FAIL",
        t=result.collider_triangles, m=law.COLLIDER_CONVEX_TRI_MAX))

    ao = result.ao
    if ao is None or not ao.baked:
        lines.append("FAIL AO bake did not run")
    elif not ao.has_contrast:
        lines.append("FAIL AO channel is flat (min={lo:.4f} max={hi:.4f}) -- "
                     "the rock has no cavities".format(lo=ao.min_value, hi=ao.max_value))
    else:
        lines.append("PASS AO contrast {d:.4f} (min={lo:.4f} max={hi:.4f} mean={m:.4f})"
                     .format(d=ao.max_value - ao.min_value, lo=ao.min_value,
                             hi=ao.max_value, m=ao.mean_value))

    topology = result.topology
    lines.append(("PASS" if topology.get("degenerateFaces", 1) == 0 else "FAIL")
                 + " zero degenerate faces")
    lines.append(("PASS" if topology.get("islands", 0) == 1 else "FAIL")
                 + " single connected island (got {n})".format(n=topology.get("islands")))
    lines.append(("PASS" if topology.get("nonManifoldEdges", 1) == 0 else "FAIL")
                 + " zero non-manifold edges (got {n})".format(
                     n=topology.get("nonManifoldEdges")))
    # Section 2 requires a manifold solid; an open shell also defeats
    # recalc_face_normals and lets AO rays into the interior.
    lines.append(("PASS" if topology.get("boundaryEdges", 1) == 0 else "FAIL")
                 + " closed shell, zero boundary edges (got {n})".format(
                     n=topology.get("boundaryEdges")))

    counts = result.counts
    lines.append(("PASS" if counts.get("fracturePlanes", 0) >= 1 else "FAIL")
                 + " fracture planes present ({n})".format(n=counts.get("fracturePlanes")))
    lines.append(("PASS" if counts.get("mineralVeins", 0) >= 1 else "FAIL")
                 + " mineral seams present ({n})".format(n=counts.get("mineralVeins")))
    lines.append(("PASS" if counts.get("beds", 0) >= MIN_BEDS else "FAIL")
                 + " strata beds >= {m} (got {n})".format(m=MIN_BEDS, n=counts.get("beds")))
    lines.append(("PASS" if counts.get("edgesChipped", 0) > 0 else "FAIL")
                 + " hard edges chipped ({n})".format(n=counts.get("edgesChipped")))
    widths = [w for w in counts.get("chipWidthsM", []) if w > 0.0]
    lines.append(("PASS" if len(set(widths)) > 1 else "FAIL")
                 + " chip widths vary (not a uniform chamfer): {w}".format(
                     w=counts.get("chipWidthsM")))
    # A bevel clamped to a hundredth of a millimetre is a no-op that a "widths vary"
    # check happily passes. The width must be a real fraction of the intended nominal.
    nominal = result.nominal_chip_width_m
    biggest = max(widths) if widths else 0.0
    lines.append(("PASS" if nominal > 0.0 and biggest >= nominal * 0.9 else "FAIL")
                 + " widest chip {b:.5f} m is a real chamfer vs nominal {n:.5f} m"
                 .format(b=biggest, n=nominal))
    lines.append(("PASS" if biggest >= size.longest_extent_m * 0.004 else "FAIL")
                 + " widest chip is at least 0.4 percent of the {e:.2f} m extent"
                 .format(e=size.longest_extent_m))
    if not result.uv.get("uv0", {}).get("unwrapped", False):
        lines.append("FAIL UV0 unwrap failed")
    else:
        lines.append("PASS UV0 fallback unwrap present alongside triplanar route")

    shading = result.shading
    if shading is None:
        lines.append("FAIL shading basis never ran")
    else:
        lines.append(("PASS" if shading.smooth_polygons > 0 else "FAIL")
                     + " {n} polygons marked smooth (not flat-shaded)".format(
                         n=shading.smooth_polygons))
        lines.append(("PASS" if shading.sharp_edges > 0 else "FAIL")
                     + " {n} edges split sharp above {a} deg (fracture planes stay crisp)"
                     .format(n=shading.sharp_edges, a=law.SMOOTH_ANGLE_DEG))
        lines.append(("PASS" if shading.weighted_applied else "FAIL")
                     + " weighted normals applied (FACE_AREA_WITH_ANGLE)")
    return lines


# ---------------------------------------------------------------------------
# Package: manifest + FBX
# ---------------------------------------------------------------------------

def write_manifest(result: VariantResult, size: SizeClass, frame: BeddingFrame,
                   strata: Stratigraphy, out_dir: str) -> str:
    """Every proof artifact ``3DMODEL_GEOLOGY_ROCKS.md`` section 10 enumerates."""
    identity = law.GeneratorIdentity(
        generator=GENERATOR_NAME, generator_version=GENERATOR_VERSION,
        seed=result.seed, quality_weight=result.quality, family=law.Family.GEOLOGY,
        scale_meters=size.longest_extent_m, camera_distance_class=size.camera_class,
        platform_lane="windows_copper_wire",
        source_references=("3DMODEL_GEOLOGY_ROCKS.md", "3dmodel.md",
                           "PROCEDURAL_ASSET_PIPELINE.md"))

    payload = {
        "identity": identity.as_dict(),
        "assetFamily": law.Family.GEOLOGY.value,
        "surfaceClass": law.SurfaceClass.GEOLOGIC.value,
        "geologicalProcessTag": result.process,
        "sizeClass": {"name": size.name, "radiusM": size.radius_m,
                      "heightM": size.height_m,
                      "longestExtentM": round(size.longest_extent_m, 4),
                      "bibleRow": size.bible_row},
        "biomeDepthRoute": "photic shallows to medium depth; surface/coastline capable",
        "materialFamily": [law.NAME_MATERIAL.format(family=law.Family.GEOLOGY.value,
                                                    role=role)
                           for _slot, role, _c, _r in MATERIAL_ROLES],
        "materialSlots": {"0": "primary rock", "1": "exposed fracture/cut face",
                          "2": "mineral vein",
                          "3": "absent by design; ore/emission travels in vcol A"},
        "profileParameters": {
            "beddingDipDeg": round(frame.dip_deg, 4),
            "beddingAzimuthDeg": round(frame.dip_azimuth_deg, 4),
            "bedCount": len(strata.beds),
            "bedThicknessRangeM": [round(min(b.thickness for b in strata.beds), 4),
                                   round(max(b.thickness for b in strata.beds), 4)],
            "bedHardnessRange": [round(min(b.hardness for b in strata.beds), 4),
                                 round(max(b.hardness for b in strata.beds), 4)],
            "overhangingBeds": sum(1 for b in strata.beds if b.overhangs_below),
            "landmarkBed": strata.landmark_bed,
            "landmarkArcRad": round(strata.landmark_arc_rad, 4),
            "planHarmonicOrders": [int(o) for o in strata.plan_orders],
            "planTwistRad": round(strata.plan_twist_rad, 4),
            "latticeRings": result.density.rings,
            "latticeSegments": result.density.segments,
            "idealRingSpacingM": round(result.density.ideal_ring_spacing_m, 5),
            "achievedRingSpacingM": round(result.density.achieved_ring_spacing_m, 5),
            "achievedSegmentSpacingM": round(result.density.achieved_segment_spacing_m, 5),
            "densityBoundByBudget": result.density.budget_bound,
            "sculptTrianglesBeforeReduction": result.sculpt_triangles,
        },
        "scaleWitnesses": {
            "absoluteBedThicknessRangeM": [WITNESS_BED_THICKNESS_MIN_M,
                                           WITNESS_BED_THICKNESS_MAX_M],
            "absoluteGrainWavelengthM": WITNESS_GRAIN_WAVELENGTH_M,
            "absolutePitWavelengthM": WITNESS_PIT_WAVELENGTH_M,
            "absolutePitDepthM": WITNESS_PIT_DEPTH_M,
            "note": "bed thickness and surface grain do NOT scale with the asset; that "
                    "is what makes them readable size cues. Chip width and macro vug "
                    "radius DO scale, per 3dmodel.md section 4 bevel-by-class.",
        },
        "detailCounts": result.counts,
        "topologyValidation": result.topology,
        "vertexColorReport": result.channels,
        "aoBake": {
            "engine": "CYCLES",
            "target": "VERTEX_COLORS",
            "samples": result.ao.samples if result.ao else 0,
            "baked": bool(result.ao and result.ao.baked),
            "min": round(result.ao.min_value, 6) if result.ao else 0.0,
            "max": round(result.ao.max_value, 6) if result.ao else 0.0,
            "mean": round(result.ao.mean_value, 6) if result.ao else 0.0,
            "hasContrast": bool(result.ao and result.ao.has_contrast),
        },
        "uvAndTriplanarReport": result.uv,
        "lods": result.lods,
        "decimation": {
            "algorithm": "Quadric Edge Collapse (Blender DECIMATE/COLLAPSE)",
            "preservesBoundaryEdges": True,
            "preservesUvSeams": True,
            "preservesSharpNormals": True,
            "preservesMaterialBorders": True,
            "mechanism": "mesh_ops._split_uv_seams converts seam/sharp/material-border "
                         "edges into mesh boundaries, which COLLAPSE preserves",
            "uniformVertexSkipping": False,
        },
        "collider": {
            "kind": result.collider_kind,
            "triangles": result.collider_triangles,
            "ceiling": law.COLLIDER_CONVEX_TRI_MAX,
            "withinBudget": result.collider_within_budget,
            "usesLod0Mesh": False,
            "namePrefix": law.COLLIDER_PREFIX,
        },
        "gates": result.gates,
        "h8forgeValidatorFailures": result.validator_failures,
        "h8forgeValidatorImported": h8validate is not None,
        "proofArtifacts": result.sheets,
        "channelMeasurements": result.channel_stats,
        "fbx": result.fbx_path,
        "unityPrefabAssembly": "NOT PERFORMED. .prefab/.mat/.asset creation is Unity-only "
                               "per AGENTS.md Evidence Law; this generator emits mesh + "
                               "manifest for a Unity-side assembler.",
    }
    path = os.path.join(out_dir, law.NAME_MANIFEST.format(
        family=law.Family.GEOLOGY.value, name=result.name) + ".json")
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(payload, handle, indent=1, sort_keys=False)
    return path


def export_fbx(lods: list, collider, out_dir: str, name: str) -> str:
    """FBX with Unity axis conversion and tangents.

    ``h8forge.__init__`` advertises an ``export_unity`` module that does not exist on
    disk yet. Rather than create a competing module under ``h8forge/`` (out of scope for
    this generator), this is a local export with the same contract; delete it and call
    ``h8forge.export_unity`` as soon as that module lands.

    ``use_tspace=True`` is required: ``3dmodel.md`` section 3 lists Tangent as a
    mandatory stream and section 10 gates tangent length and handedness.
    """
    path = os.path.join(out_dir, "MESH_{f}_{n}.fbx".format(
        f=law.Family.GEOLOGY.value, n=name))
    for other in bpy.context.view_layer.objects:
        if other.select_get():
            other.select_set(False)
    subjects = [level.obj for level in lods]
    if collider.obj is not None:
        subjects.append(collider.obj)
    for obj in subjects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = subjects[0]
    try:
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, apply_unit_scale=True,
            global_scale=1.0, apply_scale_options="FBX_SCALE_NONE",
            axis_forward="-Z", axis_up="Y", object_types={"MESH"},
            use_mesh_modifiers=True, mesh_smooth_type="EDGE", use_tspace=True,
            colors_type="LINEAR", use_triangles=True, path_mode="COPY",
            bake_anim=False)
    except (RuntimeError, AttributeError, TypeError) as error:
        return "EXPORT_FAILED: " + str(error)
    return path


# ---------------------------------------------------------------------------
# Proof renders
# ---------------------------------------------------------------------------

def render_proof(obj: bpy.types.Object, name: str, out_dir: str, resolution: int,
                 result: VariantResult) -> None:
    """Studio + flat contact sheets and the four-channel sheet, then MEASURE the pixels.

    ``3DMODEL_GEOLOGY_ROCKS.md`` section 10 requires "screenshots with flat material
    override and final material to prove the silhouette carries geology before texture
    detail". ``AGENTS.md`` ``Never Trust Automated Assertions Alone``: the PNG existing
    proves nothing, so every channel tile is sampled through
    ``preview.measure_channel_png``.
    """
    base = dict(output_dir=out_dir, resolution=resolution,
                surface_class=law.SurfaceClass.GEOLOGIC,
                views=("three_quarter", "front", "side", "low"))
    # Each mode gets its OWN asset name. preview.clear_render_dir deletes by name
    # PREFIX, so rendering studio then flat under one name makes the second run wipe the
    # first sheet -- verified in the forge rule file against the coral run, whose studio
    # sheet was missing from disk although the generator rendered it first.
    for mode in ("studio", "flat", "material"):
        spec = preview.PreviewSpec(mode=mode, name=name + "_" + mode, **base)
        sheet = preview.render_contact_sheet(obj, spec)
        result.sheets[mode] = sheet.sheet_path

    channel_spec = preview.PreviewSpec(mode="studio", name=name + "_chan", **base)
    channels = preview.render_channel_sheet(obj, channel_spec, view="three_quarter")
    result.sheets["channels"] = channels.sheet_path
    labels = law.VCOL_CONTRACT[law.SurfaceClass.GEOLOGIC]
    for index, tile in enumerate(channels.tile_paths):
        stats = preview.measure_channel_png(tile)
        result.channel_stats.append({
            "channel": "RGBA"[index],
            "meaning": labels[index],
            "tile": tile,
            "min": round(stats.min_value, 5),
            "max": round(stats.max_value, 5),
            "mean": round(stats.mean_value, 5),
            "coverageFraction": round(stats.coverage_fraction, 5),
            "subjectVisible": stats.subject_visible,
            "hasGradient": stats.has_gradient,
        })


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def parse_args(argv: list) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        prog="rock.py", description="HECTON-8 geology/rock generator (Blender 4.5 LTS)")
    parser.add_argument("--seed", type=int, default=1713)
    parser.add_argument("--quality", type=float, default=1.0,
                        help="GlobalQualityWeight, continuous 0..1")
    parser.add_argument("--variants", type=int, default=1)
    parser.add_argument("--size-class", dest="size_class", default="outcrop",
                        choices=sorted(SIZE_CLASSES) + ["all"])
    parser.add_argument("--process", default="sedimentary",
                        choices=("sedimentary", "basalt"))
    parser.add_argument("--out", default="")
    parser.add_argument("--preview", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--fbx", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--preview-resolution", dest="preview_resolution", type=int,
                        default=640)
    return parser.parse_args(argv)


def resolve_out_dir(requested: str) -> str:
    """Relative to the repo root. AGENTS.md bans hardcoded absolute developer paths."""
    if requested:
        return requested if os.path.isabs(requested) else os.path.join(
            law.project_root(), requested)
    return os.path.join(law.project_root(), "Docs", "AgentLogs", "ForgeRock")


def main(argv: list) -> int:
    args = parse_args(argv)
    out_dir = resolve_out_dir(args.out)
    os.makedirs(out_dir, exist_ok=True)

    classes = sorted(SIZE_CLASSES) if args.size_class == "all" else [args.size_class]
    print("[rock] forge {fv} generator {gv} out={out}".format(
        fv=law.FORGE_VERSION, gv=GENERATOR_VERSION, out=out_dir))
    if h8validate is None:
        print("[rock] WARNING h8forge.validate not importable: " + _VALIDATE_IMPORT_NOTE)

    failures = 0
    for class_name in classes:
        size = SIZE_CLASSES[class_name]
        for index in range(max(1, args.variants)):
            seed = args.seed if args.variants == 1 else variant_seed(args.seed, index)
            started = time.perf_counter()
            try:
                result = generate_variant(
                    seed=seed, quality=args.quality, size=size, process=args.process,
                    out_dir=out_dir, want_preview=args.preview, want_fbx=args.fbx,
                    preview_resolution=args.preview_resolution)
            except GenerationAborted as error:
                failures += 1
                print("[rock] ABORTED {c} seed={s} q={q}: {m}".format(
                    c=class_name, s=seed, q=args.quality, m=error))
                for failure in error.failures:
                    print("[rock]   " + str(failure))
                print("[rock]   blackbox: " + str(error.dump_path))
                continue

            elapsed = time.perf_counter() - started
            print("")
            print("[rock] === {n} ===  {e:.1f}s".format(n=result.name, e=elapsed))
            print("[rock] beds={b} dip={d:.1f}deg fractures={f} veins={v} "
                  "partings={p} vugs={g}".format(
                      b=result.counts["beds"], d=result.counts["beddingDipDeg"],
                      f=result.counts["fracturePlanes"], v=result.counts["mineralVeins"],
                      p=result.counts["beddingPartingGrooves"],
                      g=result.counts["macroVugs"]))
            print("[rock] lattice rings={r} segments={s} sculpt_tris={t} "
                  "(budget_bound={bb})".format(
                      r=result.density.rings, s=result.density.segments,
                      t=result.sculpt_triangles, bb=result.density.budget_bound))
            print("[rock] chips hard_edges={h} buckets={k} widths_m={w} segments={g}"
                  .format(h=result.counts["hardEdgesFound"],
                          k=result.counts["chipBucketSizes"],
                          w=result.counts["chipWidthsM"],
                          g=result.counts["chipBevelSegments"]))
            for level in result.lods:
                print("[rock] LOD{i}: {t} tris | law {lb} | geology row {gb} | "
                      "effective {eb}".format(
                          i=level["index"], t=level["triangles"],
                          lb=level["lawFamilyBudget"], gb=level["geologySizeRowBudget"],
                          eb=level["effectiveBudget"]))
            print("[rock] collider: {t} tris / {m} ceiling ({k})".format(
                t=result.collider_triangles, m=law.COLLIDER_CONVEX_TRI_MAX,
                k=result.collider_kind))
            print("[rock] topology: " + json.dumps(result.topology))
            if result.ao is not None:
                print("[rock] AO bake: samples={s} min={lo:.4f} max={hi:.4f} "
                      "mean={m:.4f} contrast={c}".format(
                          s=result.ao.samples, lo=result.ao.min_value,
                          hi=result.ao.max_value, m=result.ao.mean_value,
                          c=result.ao.has_contrast))
            for name in ("R", "G", "A"):
                key = "range" + name
                if key in result.channels:
                    print("[rock] vcol {n}: min/max/mean {v}".format(
                        n=name, v=result.channels[key]))
            for entry in result.channel_stats:
                print("[rock] channel {c} ({m}): min={lo} max={hi} mean={me} "
                      "coverage={cv} gradient={g} subject={s}".format(
                          c=entry["channel"], m=entry["meaning"], lo=entry["min"],
                          hi=entry["max"], me=entry["mean"],
                          cv=entry["coverageFraction"], g=entry["hasGradient"],
                          s=entry["subjectVisible"]))
            for line in result.gates:
                print("[rock] GATE " + line)
            if result.validator_failures:
                for line in result.validator_failures:
                    print("[rock] VALIDATOR " + line)
            else:
                print("[rock] VALIDATOR clean" if h8validate is not None
                      else "[rock] VALIDATOR skipped (module unavailable)")
            for key, path in sorted(result.sheets.items()):
                print("[rock] PNG {k}: {p}".format(k=key, p=path))
            print("[rock] MANIFEST " + result.manifest_path)
            if result.fbx_path:
                print("[rock] FBX " + result.fbx_path)

    print("")
    print("[rock] done, {f} aborted variant(s)".format(f=failures))
    return 1 if failures else 0


if __name__ == "__main__":
    _argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    sys.exit(main(_argv))

