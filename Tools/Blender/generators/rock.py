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


# __H8_CONTINUE__
