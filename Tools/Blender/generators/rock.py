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
if _HERE not in sys.path:
    # `blender -P script` does NOT put the script's own directory on sys.path, so a
    # sibling module in generators/ is unimportable without this line.
    sys.path.insert(0, _HERE)

from h8forge import export_unity, law, mesh_ops, preview, vertexcolor  # noqa: E402
from h8forge.blackbox import BlackBox, GenerationAborted         # noqa: E402
import silhouette_probe                                          # noqa: E402

try:
    from h8forge import validate as h8validate                   # noqa: E402
except Exception as _validate_import_error:                       # pragma: no cover
    h8validate = None
    _VALIDATE_IMPORT_NOTE = str(_validate_import_error)
else:
    _VALIDATE_IMPORT_NOTE = ""

GENERATOR_NAME = "rock.py"
GENERATOR_VERSION = "1.0.0"


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

    @property
    def law_key(self) -> str:
        """law.GEOLOGY_SIZE_LOD_BUDGETS keys carry no dash ("cliffchunk")."""
        return self.name.replace("-", "")

    def budget(self, lod_index: int) -> int:
        """Per-size geology budget, now owned by law.py.

        ``law.LOD_BUDGETS[Family.GEOLOGY]`` is only the large cliff-chunk row of
        ``3dmodel.md`` section 7; ``3DMODEL_GEOLOGY_ROCKS.md`` section 7 is stricter for
        smaller rocks, and section 1 makes the specialist file binding where it is
        stricter. That table used to live here as a local copy and now lives in law with
        its citation, so this is a lookup rather than a second source of truth.
        """
        return law.geology_budget_for(self.law_key).limit(lod_index)


SIZE_CLASSES = {
    "boulder": SizeClass("boulder", 0.40, 0.58, "near_interactive",
                         "3DMODEL_GEOLOGY_ROCKS.md s7 small rock"),
    "outcrop": SizeClass("outcrop", 1.45, 2.05, "mid_route",
                         "3DMODEL_GEOLOGY_ROCKS.md s7 medium boulder/ore"),
    "cliff-chunk": SizeClass("cliff-chunk", 3.10, 7.60, "landmark",
                             "3DMODEL_GEOLOGY_ROCKS.md s7 large vent/cliff chunk"),
}

# The dashless spelling is accepted for every class, because ``law.py`` keys its geology
# budget rows that way -- ``law.GEOLOGY_SIZE_LOD_BUDGETS["cliffchunk"]``, reached through
# ``SizeClass.law_key`` -- while this CLI spelled the same class ``cliff-chunk``. One name
# for one thing would be better, but the display name is already in manifests, object names
# and file names, so the alias is the non-destructive half of the fix: ``--size cliffchunk``
# and ``--size cliff-chunk`` both resolve, and a genuinely wrong name is still rejected by
# argparse rather than silently falling back to a default class.
SIZE_CLASS_ALIASES = {key.replace("-", ""): key for key in SIZE_CLASSES
                      if key.replace("-", "") != key}


def resolve_size_class(name: str) -> str:
    """Canonical ``SIZE_CLASSES`` key for a user-supplied class name."""
    return SIZE_CLASS_ALIASES.get(name, name)

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

# Radial factors for the concentric cap rings in ``build_body``, rim inward. A pole fan's
# triangle aspect ratio is ``circumference / segments`` REGARDLESS of resolution, so the only
# way to stop a cap reading as a starburst is to stop the spokes spanning the whole radius.
# Four bands land each quad at 2-5:1 on all three size classes and confine the residual pole
# fan to the innermost 12 percent of the radius, where the displacement limit -- which is a
# multiple of the vertex's OWN mean edge length -- is small enough that it cannot pinwheel.
# Cost is 2 * len(CAP_RING_FACTORS) * segments triangles per cap: +6 percent of the sculpt
# mesh on the cliff chunk, all of which the LOD0 decimation to budget absorbs.
CAP_RING_FACTORS = (0.72, 0.48, 0.28, 0.12)

# Deepest bed recession as a fraction of that bed's OWN thickness, for the bed carrying the
# widest relief in the column; every other bed scales below it. Thickness-relative because
# differential erosion is a property of the bed, not of the boulder it sits in -- and because
# a radius-relative amplitude saturated the anti-fold clamp on the cliff chunk and turned the
# stratigraphy into one uniform step. At 0.22 the deepest step on a 0.36 m cliff-chunk bed is
# 79 mm, about 1 percent of the 7.6 m extent and ~8 px in a 768 px silhouette render, so it
# reads in outline while staying under the clamp that stops a quad folding through itself.
BEDDING_RELIEF_OF_THICKNESS = 0.22

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

# The MATERIAL grain stays at the figures above -- they are the triplanar texture's scale
# witness and the manifest declares triplanar as the primary material route. The GEOMETRIC
# grain is a different quantity and it was silently conflated with them.
#
# Measured: a band-limited field can only be carried by a mesh whose sample spacing is at most
# half its wavelength. At the geology triangle budgets the achievable spacing is 0.027 m
# (boulder), 0.064 m (outcrop) and 0.128 m (cliff chunk), so the shortest representable
# wavelengths are 0.054 / 0.129 / 0.257 m. Asking the mesh for a 0.075 m grain is therefore
# impossible on everything except the boulder, at ANY lattice arrangement inside the budget --
# and an unrepresentable field does not simply vanish, it aliases into low-frequency wobble,
# which reads as a soft plastic surface. Sub-Nyquist detail belongs in the normal map.
#
# So the geometric wavelength is derived per asset from the spacing the lattice actually
# achieved, and recorded in the manifest so the scale-witness claim stays truthful about which
# scales live in geometry and which live in the material.
GEOMETRIC_GRAIN_NYQUIST_FACTOR = 2.4

# ...AND THE FACTOR ABOVE MUST GUARD THE FINEST OCTAVE, NOT THE BASE ONE. This is the defect
# that kept the masonry alive after the clamp above was already in place, and it is worth
# stating as a number because the clamp LOOKS like it is doing the job:
#
#   the clamp set the BASE wavelength to 2.4 x spacing, then `AnisotropicField` was built with
#   `octaves=3, lacunarity=2.15`, so the finest octave sat at base / 2.15^2 = base / 4.62.
#   Measured samples-per-wavelength on the finest octave, all three size classes: 0.52.
#   Nyquist needs more than 2. So the two finest octaves were pure per-vertex white noise --
#   3.85x below the sampling floor -- on a regular quad grid.
#
# White noise on a quad grid IS two-axis masonry: every quad becomes an independent plateau,
# rows and columns both regular, which is precisely the dry-stone-wall read. The cascade
# walked straight past the guard standing in front of it.
#
# So the band is now defined from its FINE end, where the lattice constraint actually lives,
# and the octave count is small and the lacunarity gentle so the coarse end stays surface
# texture instead of becoming the form. Sub-lattice grain is not "reduced" here, it is
# ABSENT by construction, and the manifest says so: it belongs in the normal map.
GRAIN_OCTAVES = 2
GRAIN_LACUNARITY = 1.6

# Amplitude as a fraction of wavelength, i.e. a SLOPE. 1/(2*pi) = 0.159 is the point at which a
# sinusoid's own gradient reaches 1 and the surface folds through itself; the previous
# `0.16 + 0.22 * q` reached 0.38 at full quality, 2.4x past that limit, so at q=1 the grain was
# guaranteed to fold and then be rescued by the per-vertex `local_edge * 0.70` clamp -- which is
# itself a lattice-frequency term and so a second masonry source. Staying under the fold limit
# means the clamp stops being load-bearing.
GRAIN_SLOPE_MIN = 0.10
GRAIN_SLOPE_MAX = 0.155

# Beds are capped so a tall chunk cannot demand more rings than its triangle budget
# can carry. Recorded in the manifest when it binds.
MAX_BEDS = 42
MIN_BEDS = 3

# Fraction of the LOD0 budget the base lattice may consume before fractures, vugs and
# chip bevels add their geometry. The remainder is headroom for those stages; the
# authored high-density sculpt is reduced by mesh_ops.reduce_to_budget afterwards.
LATTICE_BUDGET_SHARE = 0.55

# High-density authoring multiplier. mesh_ops.reduce_to_budget docstring: "the correct
# authoring route for organic surfaces is high-density sculpt THEN reduce". Same law
# applies to a fractured rock: displacing a mesh already at budget resolution turns
# ledges into mush.
#
# Held at ~1.0 for geology, against the coral precedent. A rock's defining features are
# thin ledge annuli 25-50 mm tall, and Quadric Edge Collapse removes exactly those first:
# measured, a 17,108-triangle sculpt decimated to 8,032 for LOD0 came out with every bed
# erased and the silhouette back to a smooth faceted loaf. Organic surfaces tolerate
# sculpt-then-reduce because their detail is smooth curvature; stratified stone does not,
# because its detail IS the discontinuity. So the lattice is built at budget and LOD0 is
# barely decimated -- reduction is pushed into LOD1/LOD2 where losing a ledge is correct.
SCULPT_DENSITY_MULTIPLIER = 1.05

# Silhouette acceptance, from `silhouette_probe`. These are MEASURED control values from a
# run of `silhouette_probe.py --controls` on this Blender build, not chosen thresholds:
# a smooth icosphere and a noise-displaced icosphere spread their outline turning evenly and
# score around 0.09-0.14, while a random convex polytope concentrates it into a few arrises
# and scores 0.79. `3DMODEL_GEOLOGY_ROCKS.md` section 9 rejects an asset where "no geological
# process is visible in silhouette", and that is the gate these numbers make executable.
#
# The FLOOR is set above the potato with margin, not at the polytope: a real weathered rock
# is not a gemstone, and demanding 0.79 would push the generator back into the faceted-
# gemstone failure this file has already produced once. A gate that can only be passed by
# the wrong answer is worse than no gate.
SILHOUETTE_CONTROL_SPHERE = 0.094
SILHOUETTE_CONTROL_POTATO = 0.137
SILHOUETTE_CONTROL_POLYTOPE = 0.789
SILHOUETTE_POTATO_FLOOR = 0.20      # at or below this the outline IS a potato: FAIL
SILHOUETTE_TARGET_FLOOR = 0.30      # below this the fractures are weak: WARN


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
# Joint set: the plan outline as a POLYGON, not a sum of sinusoids
# ---------------------------------------------------------------------------

@dataclass
class JointSet:
    """Through-going joint planes, stored as a convex support function in plan.

    THIS is the fix for "the fractures do not read in silhouette", and the reason it is a
    change of shape grammar rather than a parameter tune:

    The previous plan outline was ``1 + sum of four sinusoids``. That function is smooth
    everywhere by construction, so the plan outline was a smooth closed curve and there was
    no flat facet anywhere on the body for a fracture to bound. The fracture stage did make
    real planar cuts -- ``bisect_plane`` plus ``holes_fill``, not displacement -- but every
    cut had been shrunk to a 1-4.5 percent quantile bite after an earlier round produced a
    "faceted gemstone" by cutting too deep. Measured on the silhouette probe, the result was
    turn concentration 0.325 against 0.137 for a displaced icosphere and 0.789 for a convex
    polytope: turning spread over many small rim nicks, which reads as a ragged lump.

    Retuning the cut size cannot fix that, because the two failures are a false dichotomy:
    deep global half-space cuts erase the strata, shallow ones are invisible. The way out is
    to put the planar structure in the BASE FORM, where it coexists with the bedding instead
    of competing with it. Geologically that is also the truthful model -- a rock mass breaks
    along two or three through-going joint SETS, the joints cut every bed, and the beds then
    weather back by different amounts between them. Reference
    ``CLIFFS AND WATER PREVIOUSLY IN DEVELOPMENT.jpg`` shows exactly that: vertical arrises
    running the full height of the mass with bedding ledges stepping across them.

    Support function of the convex polygon whose faces are the lines ``p . n_k = d_k``::

        r(theta) = min over k of  d_k / cos(theta - phi_k),  taken where cos > 0

    Sharp arrises are not sampled, they are CONSTRUCTED: ``ring_angles`` puts a lattice
    vertex exactly on every corner azimuth, so two flat faces meet at a real dihedral edge
    rather than at a rounded chord. A corner sampled by luck is a corner that disappears at
    LOD1.
    """

    azimuths: np.ndarray
    offsets: np.ndarray
    corner_azimuths: np.ndarray
    strike_count: int

    @property
    def face_count(self) -> int:
        return int(self.azimuths.shape[0])

    @classmethod
    def from_rng(cls, rng: np.random.Generator, process: str) -> "JointSet":
        """Two or three conjugate sets plus cross joints, then a bounded-polygon repair."""
        # Basalt columns are polygonal in plan with a strong 5-6 face habit; sedimentary
        # blocks break on fewer, wider joint faces.
        sets = 3 if process == "basalt" else 2
        primary = float(rng.uniform(0.0, math.pi))
        separation = float(rng.uniform(math.radians(52.0), math.radians(84.0)))

        azimuths = []
        for s in range(sets):
            strike = primary + s * separation
            jitter = float(rng.uniform(-0.14, 0.14))
            azimuths.append(strike + jitter)
            azimuths.append(strike + math.pi + float(rng.uniform(-0.14, 0.14)))
        for _ in range(int(rng.integers(1, 3))):
            azimuths.append(float(rng.uniform(0.0, 2.0 * math.pi)))

        azimuths = np.array([a % (2.0 * math.pi) for a in azimuths])
        azimuths = np.array(sorted(set(np.round(azimuths, 6))))

        # Two MEASURED spike sources, both fixed by bounding the azimuth gap. A corner
        # between adjacent faces separated by an angle g sits at radius d / cos(g / 2), so
        # the gap is not a cosmetic parameter -- it is the corner radius:
        #
        #   g = 128 deg (the first attempt's ceiling) -> corner at 2.28 d, a needle
        #   g =  78 deg                               -> corner at 1.27 d, a rock corner
        #   g =  26 deg (the floor below)             -> corner at 1.03 d, a shallow bend
        #
        # Iteration 1's silhouette carried exactly that needle: a 1-2 px wafer protruding
        # several percent of the extent in two of four views. It reads as a mesh error rather
        # than as geology and it aliases, so the ceiling is 78 degrees and adjacent faces
        # closer than 26 degrees are merged away -- two nearly parallel joint faces are one
        # joint, and keeping both only manufactures a sliver.
        keep = [float(azimuths[0])]
        for angle in azimuths[1:]:
            if float(angle) - keep[-1] >= math.radians(26.0):
                keep.append(float(angle))
        if (keep[0] + 2.0 * math.pi) - keep[-1] < math.radians(26.0) and len(keep) > 3:
            keep.pop()
        azimuths = np.array(keep)

        for _repair in range(12):
            gaps = np.diff(np.concatenate([azimuths, [azimuths[0] + 2.0 * math.pi]]))
            worst = int(np.argmax(gaps))
            if gaps[worst] < math.radians(78.0):
                break
            insert = (azimuths[worst] + gaps[worst] * 0.5) % (2.0 * math.pi)
            azimuths = np.array(sorted(set(np.round(
                np.concatenate([azimuths, [insert]]), 6))))

        offsets = rng.uniform(0.76, 1.06, size=azimuths.shape[0])
        joint = cls(azimuths=azimuths, offsets=offsets,
                    corner_azimuths=np.zeros(0), strike_count=sets)
        joint.corner_azimuths = joint._solve_corners()
        return joint

    def _solve_corners(self) -> np.ndarray:
        """Azimuths where two ACTIVE faces meet.

        A face pair that is adjacent in normal-azimuth order is not automatically adjacent
        on the polygon: another face can cut their intersection off entirely. So the
        candidate is solved analytically and then TESTED against the support function --
        keeping an inactive corner would place a lattice vertex outside the solid and produce
        a spike.
        """
        corners = []
        count = self.azimuths.shape[0]
        for i in range(count):
            j = (i + 1) % count
            a1, a2 = float(self.azimuths[i]), float(self.azimuths[j])
            d1, d2 = float(self.offsets[i]), float(self.offsets[j])
            determinant = math.cos(a1) * math.sin(a2) - math.sin(a1) * math.cos(a2)
            if abs(determinant) < 1e-6:
                continue
            x = (d1 * math.sin(a2) - d2 * math.sin(a1)) / determinant
            y = (d2 * math.cos(a1) - d1 * math.cos(a2)) / determinant
            radius = math.hypot(x, y)
            if radius < 1e-6:
                continue
            theta = math.atan2(y, x) % (2.0 * math.pi)
            if abs(radius - self.radius(theta)) > 1e-4 * max(1.0, radius):
                continue                      # cut off by a third face; not a real corner
            corners.append(theta)
        return np.array(sorted(set(np.round(corners, 6))))

    def radius(self, theta: float, face_scales: Optional[np.ndarray] = None) -> float:
        """Support radius at an azimuth, in units of the body's base radius."""
        best = float("inf")
        for k in range(self.azimuths.shape[0]):
            cosine = math.cos(theta - float(self.azimuths[k]))
            if cosine <= 1e-4:
                continue
            offset = float(self.offsets[k])
            if face_scales is not None:
                offset *= float(face_scales[k])
            candidate = offset / cosine
            if candidate < best:
                best = candidate
        # Unbounded is impossible after the gap repair, but a finite floor here is cheaper
        # than trusting that: an inf would propagate into every vertex position silently.
        return best if best < 1e6 else 1.0

    def ring_angles(self, fill_target: int) -> np.ndarray:
        """Corner azimuths plus enough fill angles to resolve grain and grooves.

        Fill vertices inside a face are collinear in plan, so quadric collapse removes them
        cheaply at LOD1/LOD2 while the corner vertices survive as boundary-like features --
        the arris outlives the decimation, which is the whole reason the corners are exact.
        """
        angles = list(self.corner_azimuths)
        wanted = max(len(angles) * 2, int(fill_target))
        for i in range(wanted):
            angles.append((i / float(wanted)) * 2.0 * math.pi)
        angles = sorted(set(np.round(np.array(angles) % (2.0 * math.pi), 5)))
        # Drop a fill angle that lands on top of a corner: two vertices at the same azimuth
        # and the same radius is a zero-area quad, i.e. a degenerate-triangle gate failure
        # manufactured at build time.
        cleaned = []
        for angle in angles:
            if cleaned and angle - cleaned[-1] < 1e-3:
                continue
            cleaned.append(angle)
        if cleaned and (cleaned[0] + 2.0 * math.pi) - cleaned[-1] < 1e-3:
            cleaned.pop()
        return np.array(cleaned)

    def as_dict(self) -> dict:
        return {
            "faceCount": self.face_count,
            "conjugateSets": self.strike_count,
            "faceAzimuthsDeg": [round(math.degrees(a), 2) for a in self.azimuths],
            "faceOffsetsRadiusFraction": [round(float(o), 4) for o in self.offsets],
            "cornerAzimuthsDeg": [round(math.degrees(a), 2)
                                  for a in self.corner_azimuths],
            "note": "plan outline is the support function of this convex polygon; ring "
                    "vertices land exactly on the corner azimuths so the arris is "
                    "constructed, not sampled",
        }


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
    plan_phase: float = 0.0
    # Recession in METRES along the local surface normal, positive into the rock. This is
    # what `erode_bedding_planes` applies and it replaces `radius_scale` as the way bed
    # relief reaches the mesh. `radius_scale` is retained ONLY as the column's own
    # bookkeeping -- the tuned geology that decides which bed is soft, which stands proud
    # and which one is the route landmark is sound and is preserved verbatim; what was
    # wrong was applying it as a radius, which made every bed an axisymmetric contour.
    recession_m: float = 0.0

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
    joints: Optional[JointSet] = None
    # Per-bed, per-joint-face inset. Shape (bedCount, faceCount). This is what stops the
    # joint polygon from reading as an extruded prism: each bed weathers its own faces back
    # by a different amount, so the vertical arris JOGS at every bed contact. The corner
    # AZIMUTH is untouched, so the arris stays geometrically sharp while ceasing to be
    # perfectly straight -- which is what the reference cliff photograph actually shows.
    face_inset: Optional[np.ndarray] = None

    def face_scales_for_bed(self, bed_index: int) -> Optional[np.ndarray]:
        if self.face_inset is None:
            return None
        index = min(max(0, bed_index), self.face_inset.shape[0] - 1)
        return self.face_inset[index]

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
                # Narrow window: the step must read as a LEDGE. At 0.18 the profile was
                # effectively a smooth taper and the beds were invisible in silhouette.
                window = max(1e-6, bed.thickness * 0.06)
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

    def bed_at(self, h: float):
        for bed in self.beds:
            if bed.base_h <= h <= bed.top_h:
                return bed
        return self.beds[-1] if self.beds else None

    def plan_shape(self, theta: float, h: float) -> float:
        """Plan outline: joint-polygon support radius, with a weak roughening term.

        The joint polygon is the structure. The harmonic term that used to BE the outline is
        kept only as a residual at a tenth of its old amplitude, so a face is not
        mathematically perfect -- ``3dmodel.md`` section 12 rejects "perfect cylinders" as
        loudly as it rejects blobs -- while staying flat enough to hold a straight run in
        silhouette. Measured budget for that residual: at 0.9 percent of radius it is 13 mm
        on a 1.45 m outcrop, about 1.5 px in a 768 px silhouette render, so it roughens the
        surface without curving the outline.
        """
        # THE PLAN OUTLINE IS BED-INDEPENDENT, and that is the point.
        #
        # This used to add the containing bed's `plan_phase` to the azimuth and look up that
        # bed's own per-face inset row. Both are step functions of the BED INDEX, i.e. step
        # functions of height, so the section changed discontinuously at every bed contact
        # and the mass became a stack of discs each rotated and inset differently. Rendered,
        # that is a lathe-turned part: horizontal ribbon bands at near-constant spacing whose
        # trace follows the silhouette contour like a topographic map, which is a height-field
        # band function and NOT bedding. Real beds are planes with a dip and a strike; they cut
        # THROUGH the mass and their surface trace varies with the local slope.
        #
        # `plan_twist_rad * (h / height)` stays: a CONTINUOUS twist is a sheared mass, not a
        # stack. Bed relief now arrives once, from `erode_bedding_planes`, along the surface
        # normal. The arris jog that `face_inset` existed to create still happens -- a joint
        # face is bedding-perpendicular, so it takes full recession and steps in and out per
        # bed -- but now as a consequence of the erosion rather than as a second mechanism.
        twisted = theta + self.plan_twist_rad * (h / max(1e-6, self.height_m))

        if self.joints is not None:
            value = self.joints.radius(theta, None)
        else:
            value = 1.0
        residual = 0.0
        for amp, phase, order in zip(self.plan_harmonics, self.plan_phases,
                                     self.plan_orders):
            residual += amp * math.sin(order * twisted + phase)
        return max(0.30, value * (1.0 + residual))

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

    # Bed-thickness HETEROGENEITY scales with bed count, and this is a size-class defect repair.
    # At 0.55-1.55 the thickest bed is under 3x the thinnest, which is fine for the 4-6 beds a
    # boulder gets and wrong for the 42 a cliff chunk gets: the studio sheet read as corrugated
    # cardboard, a stack of near-identical pancakes, which is the "poker chips" failure this
    # file's history already records at this size class. A real sequence has a few dominant
    # competent beds carrying many thin partings, so the spread widens as the count rises.
    spread = min(1.0, count / float(MAX_BEDS))
    low = 0.55 - 0.22 * spread
    high = 1.55 + 1.15 * spread
    weights = rng.uniform(low, high, size=count)
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
        beds.append(Bed(i, cursor, top, hardness, 1.0, False,
                        float(rng.uniform(-0.45, 0.45))))
        cursor = top

    # Soft beds recede; a competent bed sitting on a soft one gains an overhang lip.
    # That pair is the mechanism behind both the stratified silhouette and the
    # occluded cavity the AO bake has to find.
    #
    # These amplitudes are DOMINANT by design. At 0.055-0.155 the beds were invisible:
    # the rendered outcrop read as a chamfered gemstone with no banding at all, which
    # 3DMODEL_GEOLOGY_ROCKS.md section 9 rejects outright. Bed relief is the macro form of
    # a sedimentary rock, so it must be the largest term in the shape -- an order of
    # magnitude above the surface grain, not comparable to it.
    recess_gain = float(rng.uniform(0.055, 0.150))
    for bed in beds:
        recess = recess_gain * (1.0 - bed.hardness)
        scale = 1.0 - recess
        if bed.index > 0 and bed.hardness > beds[bed.index - 1].hardness + 0.18:
            scale += float(rng.uniform(0.050, 0.110))
            bed.overhangs_below = True
        bed.radius_scale = scale

    # Route-facing landmark: one strongly recessed shelf bed with the bed above it
    # pushed proud, placed in the middle band of the silhouette where it reads.
    lower = max(1, int(count * 0.34))
    upper = max(lower + 1, int(count * 0.72))
    landmark = int(rng.integers(lower, min(count - 1, upper) + 1))
    beds[landmark].radius_scale -= float(rng.uniform(0.060, 0.130))
    beds[landmark].hardness = min(beds[landmark].hardness, 0.32)
    if landmark + 1 < count:
        beds[landmark + 1].radius_scale += float(rng.uniform(0.030, 0.075))
        beds[landmark + 1].overhangs_below = True
        beds[landmark + 1].hardness = max(beds[landmark + 1].hardness, 0.72)

    # Only SOME interfaces are true shelves.
    #
    # With every bed stepping the full perimeter the 7.6 m chunk read as a stack of slate
    # tiles or poker chips -- strata legible but the stone mass gone, which is the opposite
    # overshoot from the smooth loaf. In a real sequence most contacts are visible as a
    # colour/texture band and only a few competent beds stand out as a shelf. Non-shelf
    # beds keep 82 percent of the previous radius difference, so the contact still exists
    # for the stain channel and the shading break without cutting a geometric step.
    ledge_draw = rng.random(len(beds))
    for index in range(1, len(beds)):
        if ledge_draw[index] >= 0.34:
            beds[index].radius_scale = (beds[index - 1].radius_scale
                                        + (beds[index].radius_scale
                                           - beds[index - 1].radius_scale) * 0.18)

    # A ledge's TREAD must not out-run its RISER.
    #
    # Isolated with --debug-stage lattice: at recess 0.14-0.34 the raw bed lattice was not
    # a stratified rock but a stack of PANCAKE PLATES with 0.3-0.6 m overhangs that
    # interpenetrated their neighbours. The detail stages then welded those
    # interpenetrations away, and THAT is what erased the strata -- the grammar was
    # producing them all along, absurdly, and the cleanup was removing them. Raising the
    # amplitude, which is what the earlier rounds did, made it strictly worse.
    #
    # So the horizontal step is clamped against the bed's own thickness: a step wider than
    # roughly half the riser height turns a bedding ledge into a near-horizontal shelf that
    # both self-intersects and reads as a mushroom cap.
    # Tightened from 0.55 to 0.30 of the riser height. At 0.55 a 0.34 m bed could legally step
    # 0.19 m of radius, and combined with the extra proud amount an overhanging bed receives
    # that produced thin curled lips -- the iteration-6 studio sheet read them as peeled sheet
    # metal along the flanks, which is the "clean sci-fi plastic" failure wearing a different
    # hat. A bedding ledge whose tread approaches its riser is a mushroom cap, not a shelf.
    for index in range(1, len(beds)):
        max_step = (beds[index].thickness * 0.30) / max(1e-6, size.radius_m)
        difference = beds[index].radius_scale - beds[index - 1].radius_scale
        if abs(difference) > max_step:
            beds[index].radius_scale = (beds[index - 1].radius_scale
                                        + math.copysign(max_step, difference))

    # Convert the column's radial bookkeeping into an absolute recession along the surface
    # normal, which is the only form `erode_bedding_planes` uses. Everything above -- the
    # hardness alternation, the proud competent bed over a soft one, the landmark shelf, the
    # "only some interfaces are true shelves" draw, the tread-vs-riser clamp -- is untouched.
    # A radius_scale under 1 is a bed that weathers back; over 1 is a bed that stands proud,
    # and a negative recession is exactly that.
    # Scaled by the BED'S OWN THICKNESS, not by the rock's radius, and that is the difference
    # between a measurement and a saturated clamp. Measured on the cliff chunk when this was
    # `(1 - radius_scale) * size.radius_m`: the deepest recession came out 0.1450 m, which was
    # EXACTLY `local_edge * 0.85`, i.e. the safety clamp -- so the clamp and not the
    # stratigraphy was choosing the step, 6127 of 6152 vertices moved by an identical
    # saturated amount, and the result was a fresh uniform band plus enough folding to fail
    # the LOD0 round trip at corner-normal delta 0.002118.
    #
    # Thickness is also the geologically correct scale: a thick competent bed weathers into a
    # prominent shelf and a 2 cm parting into a hairline, independent of how big the rock is.
    # Normalising by the widest relief in the column keeps the SHAPE of the tuned profile
    # (which bed is soft, which stands proud, which is the landmark) and only sets its depth.
    widest = max((abs(1.0 - bed.radius_scale) for bed in beds), default=0.0)
    for bed in beds:
        relief = (1.0 - bed.radius_scale) / widest if widest > 1e-9 else 0.0
        bed.recession_m = relief * bed.thickness * BEDDING_RELIEF_OF_THICKNESS

    orders = np.array(sorted(rng.choice(np.arange(3, 11), size=4, replace=False)))
    # Amplitude cut ~10x: this term used to define the outline and now only roughens the
    # joint faces. See Stratigraphy.plan_shape for the measured pixel budget.
    harmonics = rng.uniform(0.004, 0.013, size=4) / (1.0 + 0.20 * (orders - 3))
    phases = rng.uniform(0.0, 2.0 * math.pi, size=4)
    drift_amp = rng.uniform(-0.16, 0.16, size=(3, 2)) * size.radius_m
    drift_phase = rng.uniform(0.0, 2.0 * math.pi, size=(3, 2))

    joints = JointSet.from_rng(rng, process)
    # Per-bed weathering of each joint face. Competent beds hold their face near the joint
    # plane; soft beds recede further. Derived from the bed's own hardness so the inset and
    # the strata story cannot disagree.
    face_inset = np.empty((len(beds), joints.face_count))
    for bed in beds:
        for k in range(joints.face_count):
            soft = 1.0 - bed.hardness
            # The random component is deliberately smaller than the hardness-driven one so
            # the inset reads as a COHERENT per-bed recession rather than as a random fringe.
            # At +-1.6 percent the silhouette grew a sawtooth rim of unrelated few-centimetre
            # nicks, which is the "many small nicks" failure the metric is built to reject.
            face_inset[bed.index, k] = (1.0
                                        - soft * float(rng.uniform(0.024, 0.080))
                                        + float(rng.uniform(-0.009, 0.009)))

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
        joints=joints,
        face_inset=face_inset,
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


def finest_representable_wavelength(density: "LatticeDensity") -> float:
    """Shortest wavelength this lattice can carry at all, in metres.

    Keyed off the COARSER of the two achieved spacings, because a field is under-sampled as
    soon as either axis is too coarse for it -- averaging the two would let a fine vertical
    spacing hide a coarse circumferential one, which is exactly the state the outcrop was in.
    """
    coarsest = max(density.achieved_ring_spacing_m, density.achieved_segment_spacing_m)
    return coarsest * GEOMETRIC_GRAIN_NYQUIST_FACTOR


def geometric_grain_wavelength(density: "LatticeDensity") -> float:
    """BASE grain wavelength whose FINEST octave is still representable, in metres.

    The octave cascade is part of the field, so the sampling constraint has to be applied
    through it rather than to its coarsest member. ``AnisotropicField`` divides the base by
    ``lacunarity ** octave``, so the finest member is ``base / lacunarity ** (octaves - 1)``
    and THAT is the one that has to clear the lattice.

    The returned wavelength is therefore always at or above the witness value, and on the
    outcrop and cliff chunk it is well above it. That is not a compromise to be tuned away:
    it is the honest statement that those size classes cannot carry 0.075 m relief as
    GEOMETRY inside their triangle budget, and section 2 of ``3DMODEL_GEOLOGY_ROCKS.md``
    already routes that detail correctly -- "Major cracks need mesh relief, bevel chips, or
    baked normal/depth support".
    """
    finest = finest_representable_wavelength(density)
    return max(WITNESS_GRAIN_WAVELENGTH_M,
               finest * (GRAIN_LACUNARITY ** (GRAIN_OCTAVES - 1)))


def pit_field_is_representable(density: "LatticeDensity") -> bool:
    """Can this lattice carry the ABSOLUTE pit wavelength the scale witness declares?

    The pit field was reading ``WITNESS_PIT_WAVELENGTH_M`` (0.052 m) RAW -- no clamp of any
    kind -- against achieved spacings of 0.036/0.085/0.169 m, i.e. 1.4/0.6/0.3 samples per
    wavelength. So it was a second independent white-noise source feeding the same masonry,
    and the grain clamp never covered it because it is a different field.

    It is NOT clamped up to a representable wavelength, and that is the point. Stretching a
    0.052 m vug to 0.41 m so the lattice can sample it does not preserve the feature, it
    replaces it with a different one: a broad shallow dish is not a pit, and the manifest
    would still be claiming a 0.052 m witness. A scale witness is an ABSOLUTE claim, so the
    honest options are "in the mesh at its declared size" or "not in the mesh" -- and
    ``3DMODEL_GEOLOGY_ROCKS.md`` section 2 provides the second route explicitly, with
    ``punch_vugs`` still supplying the mesh-scale cavities section 3 asks for in silhouette.
    """
    return WITNESS_PIT_WAVELENGTH_M >= finest_representable_wavelength(density)


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
    circumference = 2.0 * math.pi * size.radius_m
    sculpt_budget = int(size.budget(0) * LATTICE_BUDGET_SHARE * SCULPT_DENSITY_MULTIPLIER)

    # BALANCED spacing, derived rather than guessed, and this is a measured defect repair.
    #
    # The two spacings used to be picked independently from quality (0.105->0.030 vertical and
    # 0.150->0.046 circumferential) and then shrunk together by a sqrt factor when the budget
    # bound. On the outcrop that produced 0.0661 m vertical against 0.2071 m circumferential --
    # a 3.1x anisotropy -- and the circumferential figure is the one that matters, because a
    # 0.075 m grain field on 0.207 m samples is 2.8x UNDER-SAMPLED. The grain has therefore
    # never been representable: it aliased into a low-frequency wobble, which is a large part of
    # why the flanks render as smooth plastic rather than as stone.
    #
    # For a uniform lattice, quads = height * circumference / s^2, so the achievable spacing at
    # a given triangle budget is a closed form. Measured against the geology rows:
    #   boulder     circumference  2.51 m -> 0.0270 m spacing, Nyquist wavelength 0.0540 m
    #   outcrop     circumference  9.11 m -> 0.0644 m spacing, Nyquist wavelength 0.1288 m
    #   cliff chunk circumference 19.48 m -> 0.1282 m spacing, Nyquist wavelength 0.2565 m
    # Balancing alone buys a 3x finer circumferential resolution at the same triangle count.
    balanced = math.sqrt(size.height_m * circumference / max(1.0, sculpt_budget * 0.5))
    coarse = balanced * 2.4
    spacing = coarse + (balanced - coarse) * q

    rings_ideal = int(round(size.height_m / spacing)) + 1
    rings_ideal = max(beds * 2 + 1, min(430, rings_ideal))
    # Ceiling raised from 96: at 96 the outcrop was capped well short of the balanced spacing,
    # so the cap and not the budget was deciding the circumferential resolution.
    segments_ideal = max(12, min(220, int(round(circumference / spacing))))
    ring_spacing = spacing
    segment_spacing = spacing

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

    # Grain amplitude is tied to the grain WAVELENGTH, not to the asset extent. A
    # band-limited field of wavelength L can only carry amplitude of roughly L/(2*pi)
    # before its own gradient exceeds 1 and the surface folds through itself. At
    # 0.006-0.011 of a 2.9 m extent the requested amplitude was 5-8x that limit, so it was
    # either clamped away to nothing or self-intersecting. Keeping grain small and
    # absolute is also what the geology bible wants: strata are the dominant term and fine
    # noise stays weak, or the asset becomes the "noise sphere" the pipeline bible rejects.
    # Amplitude raised ~2.7x once the base form became planar, and the reason is a real
    # rejection, not a preference. At 0.05-0.14 of the grain wavelength the displacement is
    # 1.05 cm on a 2.9 m body -- 0.36 percent of extent. That was invisible against the old
    # smooth-lobed form and it is WORSE than invisible against flat joint faces: a mirror-flat
    # facet with a crisp arris reads as a machined hull panel, which `TASTE.md` rejects on
    # sight as "clean sci-fi plastic" and `3dmodel.md` section 12 rejects as "clean sterile
    # sci-fi". Measured on the iteration-3 studio sheet: the summit facet rendered as polished
    # plastic and the bedding grooves as milled slots.
    #
    # Real fracture facets are flat at the metre scale and rough at the centimetre scale --
    # that is the scale hierarchy, not a contradiction of it. 0.16-0.38 of the wavelength is
    # 2.9 cm here, 1.0 percent of extent, still an order of magnitude under the bed relief so
    # the strata stay dominant, and still far under the `local_edge * 0.70` fold limit.
    grain_wavelength = geometric_grain_wavelength(density)
    grain_amp = grain_wavelength * (GRAIN_SLOPE_MIN
                                    + (GRAIN_SLOPE_MAX - GRAIN_SLOPE_MIN) * q)
    # More waves per octave now that there are fewer octaves: richness has to come from
    # DIRECTIONS and PHASES inside the representable band instead of from octaves below it.
    # The octave count no longer switches on quality -- quality already drives the lattice
    # spacing, which drives the wavelength, so the field still varies continuously with q and
    # `AGENTS.md`'s ban on binary quality switches is satisfied by the spacing, not by a
    # branch that pushed the field under Nyquist at exactly q >= 0.5.
    grain = AnisotropicField(
        rng, grain_wavelength,
        octaves=GRAIN_OCTAVES, waves_per_octave=11, lacunarity=GRAIN_LACUNARITY,
        bedding_normal=np.array([frame.normal.x, frame.normal.y, frame.normal.z]),
        anisotropy=3.4 if process == "sedimentary" else 2.1,
    )
    pit_representable = pit_field_is_representable(density)
    pit = AnisotropicField(
        rng, WITNESS_PIT_WAVELENGTH_M, octaves=1, waves_per_octave=7,
        bedding_normal=np.array([frame.normal.x, frame.normal.y, frame.normal.z]),
        anisotropy=1.35,
    )
    finest_grain = grain_wavelength / (GRAIN_LACUNARITY ** (GRAIN_OCTAVES - 1))
    coarsest_spacing = max(density.achieved_ring_spacing_m,
                           density.achieved_segment_spacing_m)
    grain_report = {
        "witnessWavelengthM": WITNESS_GRAIN_WAVELENGTH_M,
        "coarsestLatticeSpacingM": round(coarsest_spacing, 5),
        "baseWavelengthM": round(grain_wavelength, 5),
        "finestOctaveWavelengthM": round(finest_grain, 5),
        "finestOctaveSamplesPerWavelength": round(finest_grain / coarsest_spacing, 3),
        "pitWavelengthM": round(pit_wavelength, 5),
        "pitSamplesPerWavelength": round(pit_wavelength / coarsest_spacing, 3),
        "amplitudeM": round(grain_amp, 5),
        "amplitudeOverWavelength": round(grain_amp / max(1e-9, grain_wavelength), 4),
        "foldLimitSlope": round(1.0 / (2.0 * math.pi), 4),
        "subLatticeGrainRoute":
            "Detail below {f:.3f} m cannot be sampled by this lattice and is NOT in the "
            "mesh. 3DMODEL_GEOLOGY_ROCKS.md section 2 routes it to baked normal/depth "
            "support; the witness {w} m is a MATERIAL-space scale on this size class."
            .format(f=finest_grain, w=WITNESS_GRAIN_WAVELENGTH_M),
    }

    # Azimuths are NOT uniform any more: the joint-polygon corner azimuths are in the list
    # exactly, and the rest is fill. Every ring shares the list, which is what keeps the
    # bridge loop regular and puts the arris on a genuine edge chain running the full height.
    if strata.joints is not None:
        angle_list = strata.joints.ring_angles(density.segments)
    else:
        angle_list = np.array([(s / float(density.segments)) * 2.0 * math.pi
                               for s in range(density.segments)])
    segments = int(angle_list.shape[0])
    base_h = -size.height_m * 0.5

    # Ring heights are driven by the BED STRUCTURE, not by uniform height spacing.
    #
    # This is the fix that finally made strata read. With evenly spaced rings, a bed step
    # can only ever be as sharp as one ring gap: measured on the outcrop, the step window
    # was 0.02 m while the ring spacing was 0.037 m, so every ledge collapsed into a
    # single-segment chamfer and the rendered rock was a smooth loaf with no banding at
    # all -- twice in a row, which AGENTS.md [RULE] Same-failure escalation says to solve
    # by changing the route rather than retuning it.
    #
    # A ledge needs TWO rings at nearly the same height with DIFFERENT radii. That pair
    # forms a near-horizontal annulus: the sediment shelf in silhouette, and the overhang
    # that gives the AO bake a real cavity to find.
    beds = strata.beds
    step_rings = 2 * (len(beds) - 1) + 2
    body_budget = max(len(beds), density.rings - step_rings)
    # Few body rings, many segments. Triangles buy far more here as plan-outline detail
    # and ledge annuli than as extra rings inside a parallel-sided slab.
    per_bed = max(1, min(3, body_budget // max(1, len(beds))))

    # EVERY RING SHARES ONE RADIUS FACTOR. The ring HEIGHTS still cluster at bed contacts,
    # because that is where `erode_bedding_planes` needs resolution to put a crisp step, but
    # the radius no longer depends on which bed the ring sits in. That single change is what
    # stops the lattice from being a stack of discs -- a ring pair straddling a contact is now
    # two rings of the SAME radius, so the contact carries no built-in axisymmetric lip, and
    # the step appears only where the erosion says the bed is actually exposed.
    UNIFORM = 1.0
    ring_specs = []
    for i, bed in enumerate(beds):
        if i == 0:
            ring_specs.append((bed.base_h, UNIFORM))
        else:
            # Taller rise than the first attempt: a 25 mm annulus is below what quadric
            # collapse and the weighted-normal pass will preserve, so the ledge has to be
            # a feature the rest of the pipeline can see.
            ledge_rise = min(0.055, max(0.020, bed.thickness * 0.16))
            ring_specs.append((bed.base_h, UNIFORM))
            ring_specs.append((bed.base_h + ledge_rise, UNIFORM))
        for k in range(1, per_bed):
            ring_specs.append((bed.base_h + (k / float(per_bed)) * bed.thickness,
                               UNIFORM))
    ring_specs.append((beds[-1].top_h, UNIFORM))

    # Strictly increasing height, or the bridge loop builds inverted/zero-area quads.
    cleaned = []
    for h, scale in sorted(ring_specs, key=lambda item: item[0]):
        if cleaned and h <= cleaned[-1][0] + 1e-5:
            continue
        cleaned.append((h, scale))
    ring_specs = cleaned
    rings = len(ring_specs)

    # Pass 1: undisplaced lattice positions plus the outward radial direction of each
    # vertex. Displacement is applied along the LOCAL outward direction and scaled by
    # the LOCAL radius, never by distance from an arbitrary axis -- scaling by
    # distance-from-axis leaves the core smooth while the rim self-intersects.
    positions = np.empty((rings * segments, 3))

    index = 0
    for h, radius_scale in ring_specs:
        t = (h - base_h) / max(1e-6, size.height_m)
        drift_u, drift_v = strata.drift(h)
        bed_phase = 0.0
        for candidate in strata.beds:
            if candidate.base_h <= h <= candidate.top_h:
                bed_phase = candidate.plan_phase
                break
        # Only a token taper now. The summit used to shrink 70 percent over the last 14
        # percent of the height, which domed the top and is a large part of why the profile
        # silhouette read as a loaf: a dome has no straight run and no rim. The top is now
        # defined by `truncate_summit`, an oblique planar cut, so all this has to do is
        # avoid a knife-edge rim in the event that the cut declines to fire.
        taper = 1.0
        if t > 0.92:
            local = (t - 0.92) / 0.08
            taper = 1.0 - 0.14 * (local * local * (3.0 - 2.0 * local))
        for s in range(segments):
            theta = float(angle_list[s])
            shape = strata.plan_shape(theta, h)
            notch = strata.landmark_sector_weight(theta, h)
            # The old circumferential `asymmetry` term (16 percent + 9 percent of radius,
            # i.e. 23 cm on this outcrop) is DELETED. It existed to break the lens symmetry
            # of a bed that recedes uniformly, and the joint polygon plus per-bed per-face
            # inset does that far better -- while the sinusoid actively re-curved every flat
            # face it was applied to, which is the defect under repair.
            radius = (size.radius_m * shape * radius_scale * taper
                      * (1.0 - 0.44 * notch))
            direction = frame.e1 * math.cos(theta) + frame.e2 * math.sin(theta)
            point = direction * radius + frame.normal * h
            point.x += drift_u
            point.y += drift_v
            positions[index] = (point.x, point.y, point.z)
            index += 1

    verts = []
    for i in range(rings * segments):
        verts.append(bm.verts.new(Vector((float(positions[i, 0]),
                                          float(positions[i, 1]),
                                          float(positions[i, 2])))))
    bm.verts.ensure_lookup_table()

    for r in range(rings - 1):
        row = r * segments
        next_row = (r + 1) * segments
        for s in range(segments):
            s_next = (s + 1) % segments
            bm.faces.new((verts[row + s], verts[row + s_next],
                          verts[next_row + s_next], verts[next_row + s]))

    # Caps: closed solid. ``3DMODEL_GEOLOGY_ROCKS.md`` section 2 requires manifold
    # output for a solid rock, so both ends are filled -- but NOT by poking the rim n-gon
    # straight to a single pole, which is what this did for its whole history.
    #
    # MEASURED on the shipped s1713 q1.0 FBX, 2026-07-29, before this change. Every one of
    # the twelve longest edges in boulder LOD0 and outcrop LOD0, and ten of the twelve in
    # cliff-chunk LOD0, was a SPOKE of a cap pole:
    #
    #   boulder    pole valence 68 / 64, spokes up to 0.461 m, median edge 0.035 m  (13x)
    #   outcrop    pole valence 95 / 79, spokes up to 1.562 m, median edge 0.079 m  (20x)
    #   cliffchunk pole valence 72 / 70, spokes up to 3.788 m, median edge 0.160 m  (24x)
    #
    # That is the whole geometry of the defect. A pole fan over a rim of ``segments``
    # vertices makes ``segments`` triangles whose length is the cap RADIUS and whose base is
    # the segment spacing, so the aspect ratio is fixed at ``circumference / segments`` --
    # 18:1 on the cliff chunk -- and it does not improve with more triangles, because
    # refining the lattice refines the base and the spoke together. Two visible symptoms,
    # one cause:
    #
    #   * the summit rendered as a radial STARBURST, because each 3 m sliver takes its own
    #     normal from the grain displacement and the fan shares one high-valence centre;
    #   * the base grew a NEEDLE FRINGE, because the fracture cuts and the bedding inset
    #     chew the rim those 3 m spokes hang from, and a chewed 24x-median edge leaves a
    #     1-2 px wafer in the silhouette.
    #
    # `truncate_summit`'s ceiling clamp cannot fix this and neither can `_plane_clamp`:
    # clamping PROJECTS the cap onto the summit plane instead of removing it, so the pole
    # survives the cut lying flat inside the facet -- measured at height fraction 0.83-0.94
    # on all three sizes, i.e. below the top, inside the summit facet, exactly where the
    # starburst renders.
    #
    # So the cap is built as CONCENTRIC RINGS of the rim polygon scaled toward its own
    # centre: a dartboard, not a pinwheel. Each band is quads at 2-5:1, the pole fan that
    # remains is confined to the innermost 12 percent of the radius, and the dome the poke
    # used to provide is now a parabola over the whole cap, which is a better dome anyway.
    # Scaling a star-shaped polygon about its centre cannot self-intersect, which is why
    # this is analytic and not `inset_region`: the plan outline carries 44 percent landmark
    # notches, and an even inset of 0.8 m into a 1.4 m notch collapses it.
    def _cap_ring(h: float, radius_scale: float, factor: float, lift: float,
                  count: int) -> list:
        """One concentric cap ring: the body's own plan outline at ``factor`` of radius.

        ``count`` may be below ``segments`` -- the cap rings thin as they close in. The
        azimuths are still SAMPLED FROM ``angle_list`` by stride rather than spread evenly,
        so an inner ring keeps the joint polygon's corner directions and the cap does not
        slowly rotate away from the plan outline it is closing.
        """
        drift_u, drift_v = strata.drift(h)
        t = (h - base_h) / max(1e-6, size.height_m)
        taper = 1.0
        if t > 0.92:
            local = (t - 0.92) / 0.08
            taper = 1.0 - 0.14 * (local * local * (3.0 - 2.0 * local))
        row = []
        for s in range(count):
            theta = float(angle_list[int(s * segments / count) % segments])
            shape = strata.plan_shape(theta, h)
            notch = strata.landmark_sector_weight(theta, h)
            radius = (size.radius_m * shape * radius_scale * taper
                      * (1.0 - 0.44 * notch) * factor)
            direction = frame.e1 * math.cos(theta) + frame.e2 * math.sin(theta)
            point = direction * radius + frame.normal * (h + lift)
            point.x += drift_u
            point.y += drift_v
            row.append(bm.verts.new(point))
        return row

    # Dome amounts kept at 0.012/0.022. The top dome is the summit truncation's job; leaving
    # a 5.5 percent dome there would either survive the cut as a rounded cap or be thrown
    # away, and in the first case it is the loaf silhouette again.
    # The base dish is CONCAVE (sign +1, i.e. lifted INTO the body), not convex. It used to
    # push the base centre downward, which on a pole cap was one hidden vertex but on a
    # concentric cap is a shallow cone the rock balances on -- measured as a gathered radial
    # pucker in the low and underside views of all three sizes. A ground-standing rock rests
    # on its rim, so dishing upward both removes the cone from the low silhouette and puts
    # the whole underside into self-shadow where the AO bake wants it.
    # EACH CAP RING ALSO HALVES ITS SEGMENT COUNT -- a polar reduction, not just concentric
    # rings. Concentric rings alone fixed the long spokes but left every ring carrying the
    # full 115 segments, so the innermost ring was a 115-gon of 17 mm edges around a 0.37 m
    # radius and the closing poke put 115 slivers back at the centre. A pole fan's aspect
    # ratio is `circumference / segments`, which is scale-INVARIANT, so shrinking the fan
    # without thinning it just makes a smaller pinch: measured as the umbrella pucker still
    # visible on the underside of all three sizes after the first cap fix. Halving the count
    # each band keeps every quad near 1:1 and leaves a 6-8 gon to close, whose poke triangles
    # are as wide as they are long.
    ring_counts = []
    running = segments
    for _ in CAP_RING_FACTORS:
        running = max(6, running // 2)
        ring_counts.append(running)

    for end_ring, sign, amount in ((0, 1.0, 0.022), (rings - 1, 1.0, 0.012)):
        cap_h, cap_scale = ring_specs[end_ring]
        previous = verts[end_ring * segments:(end_ring + 1) * segments]
        for factor, count in zip(CAP_RING_FACTORS, ring_counts):
            # Parabolic in the radial factor: zero at the rim so the band meets the flank
            # flush, full ``amount`` at the centre so the apex lands where the single poke
            # used to put it.
            lift = sign * size.height_m * amount * (1.0 - factor * factor)
            row = _cap_ring(cap_h, cap_scale, factor, lift, count)
            outer = len(previous)
            # Fan the outer ring onto the inner one by nearest-index mapping. Where two
            # adjacent outer vertices map to the same inner vertex the face is a TRIANGLE;
            # where they straddle an inner step it is a quad. That is what absorbs the
            # halving without leaving a hole or a T-junction.
            for s in range(outer):
                s_next = (s + 1) % outer
                j = int(s * count / outer) % count
                j_next = int(s_next * count / outer) % count
                if j == j_next:
                    bm.faces.new((previous[s], previous[s_next], row[j]))
                else:
                    bm.faces.new((previous[s], previous[s_next], row[j_next], row[j]))
            previous = row
        centre = bm.faces.new(tuple(previous))
        poked = bmesh.ops.poke(bm, faces=[centre], offset=0.0,
                               center_mode="MEAN_WEIGHTED", use_relative_offset=False)
        for vert in poked["verts"]:
            # The innermost ring already carries ``amount * (1 - f^2)``; this adds the
            # remaining ``amount * f^2`` so the apex sits at exactly the same dome height
            # the single-poke version gave it, rather than at twice it.
            vert.co += frame.normal * (sign * size.height_m * amount
                                       * CAP_RING_FACTORS[-1] ** 2)
    bm.verts.ensure_lookup_table()
    bm.faces.ensure_lookup_table()

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.normal_update()

    # Displacement runs AFTER the caps exist, over every vertex, along each vertex's own
    # normal. Displacing only the lattice left the caps perfectly flat, which is half of
    # why the top read as a machined fan. Using the vertex normal also means the cap
    # vertices push along the bedding normal while the flank vertices push outward,
    # without any special-casing.
    #
    # Amplitude is scaled by a LOCAL size measure -- the mean length of the vertex's own
    # linked edges -- NOT by distance from an axis. Scaling by distance-from-axis leaves
    # the core smooth while the rim takes full amplitude and self-intersects.
    all_verts = bm.verts[:]
    coords = np.array([[v.co.x, v.co.y, v.co.z] for v in all_verts])
    grain_values = grain.sample(coords)
    pit_values = pit.sample(coords)

    heights = coords @ np.array([frame.normal.x, frame.normal.y, frame.normal.z])
    softness = np.array([1.0 + 0.85 * (1.0 - strata.hardness_at(float(h))) for h in heights])

    offsets = grain_values * grain_amp * softness

    # Absolute-depth pitting: only the upper tail of the field cuts, so pits are
    # discrete vugs rather than a wobble over the whole surface.
    pit_threshold = 0.34 if process == "basalt" else 0.46
    pit_mask = np.clip((pit_values - pit_threshold) / max(1e-6, 1.0 - pit_threshold), 0.0, 1.0)
    pit_mask = pit_mask * pit_mask * (3.0 - 2.0 * pit_mask)
    pit_depth = WITNESS_PIT_DEPTH_M * (0.55 + 0.45 * q) * (1.6 if process == "basalt" else 1.0)
    offsets = offsets - pit_mask * pit_depth

    for i, vert in enumerate(all_verts):
        linked = vert.link_edges
        if not linked:
            continue
        local_edge = sum(e.calc_length() for e in linked) / len(linked)
        limit = local_edge * 0.70
        amount = max(-limit, min(limit, float(offsets[i])))
        normal = vert.normal
        if normal.length <= 1e-9:
            continue
        vert.co += normal.normalized() * amount

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    stats = mesh_ops.weld_and_clean(bm, blackbox=blackbox)
    blackbox.record("build_body", family=law.Family.GEOLOGY.value,
                    vertex_count=len(bm.verts), triangle_count=len(bm.faces),
                    warning="" if stats["degenerate_faces_deleted"] == 0 else
                    "welded away {n} zero-area faces".format(
                        n=stats["degenerate_faces_deleted"]))
    return bm, grain, pit, grain_report


# ---------------------------------------------------------------------------
# Stage 4a: fractures and pressure breaks
# ---------------------------------------------------------------------------

@dataclass
class FracturePlane:
    origin: np.ndarray
    normal: np.ndarray
    kind: str
    volume_spent: float = 0.0
    volume_start: float = 0.0


# One shared ceiling for EVERY half-space removal on the body, summit truncation included.
# Iteration 1 gave the summit cut and the shear cuts a 30 percent budget each, so the body
# could legally lose 60 percent of its volume to planes -- and it did: the side silhouette
# came out a wide thin wedge with an aspect ratio near 2.6 where the size class asks for
# about 1.4. A rock that has lost most of itself is a flake, and the references
# (``UNDERWATER PREVIOUSLY IN DEVELOPMENT.jpg``) show blocky non-equant boulders, not flakes.
CUT_VOLUME_BUDGET_FRACTION = 0.34

# A cut is a GRAZE if the volume it removes, divided by the facet it creates, is shallower
# than this fraction of the asset's longest extent. Grazing cuts are the feather-edge source:
# they leave a wafer wedge with a near-zero dihedral, which shades black, aliases, and reads
# as a mesh defect. Depth = removed_volume / facet_area is the honest measure of "did this
# cut take a corner off or did it skim the surface", and it is scale-free.
CUT_MIN_MEAN_DEPTH_FRACTION = 0.022


def _facet_area(faces: list) -> float:
    return float(sum(f.calc_area() for f in faces if f.is_valid))


def _plane_clamp(bm: bmesh.types.BMesh, normal: np.ndarray, offset: float,
                 roughness: Optional[AnisotropicField] = None,
                 roughness_amp: float = 0.0) -> list:
    """Fold the half-space outside a plane ONTO the plane. Returns the new facet faces.

    This replaces the bisect/holes_fill/poke route, and the reason is a rejection that was
    already on record in this file before I repeated it. Measured, in order:

      iteration 4: `holes_fill` returns an n-gon that `triangulate` ear-clips into a fan whose
        vertices are ALL on the rim. Zero interior vertices, so the roughening pass had nothing
        to move and every large facet stayed a mirror-flat plane -- `TASTE.md` "clean sci-fi
        plastic", rejected on sight.
      iteration 5: poking the facet to create interior vertices rendered as a RADIAL STARBURST
        pinwheel on the summit, because every triangle in a poke fan shares one high-valence
        centre and the roughening gives each a different normal. `build_body`'s own comments
        record that exact artifact being rejected once already, so retuning it would be the
        same-failure escalation `AGENTS.md` forbids.

    Clamping has none of those failure modes because it adds NO topology. Every vertex outside
    the half-space is projected along the plane normal onto it; the lattice's own regular quad
    grid becomes the facet's tessellation, complete with interior vertices. Consequences that
    make this correct rather than merely convenient:

      - no `holes_fill`, so no chance of bridging unrelated rims into a membrane;
      - no new boundary edges, so the shell stays closed by construction;
      - no fan, so no starburst and no ear-clipped slivers;
      - the roughening is applied DURING the projection, so the facet is never flat at any
        point in the pipeline;
      - a clamp is a convex operation, so it cannot produce an overhanging spall. The concavity
        in this asset comes from the landmark notch and the per-bed insets instead, which is
        where it was already coming from.

    The cost is paid honestly: rings that were far outside land close together on the plane, so
    the facet carries some elongated quads. Those are valid geometry, the weld tolerance is
    orders of magnitude below their spacing, and quadric collapse removes them at LOD1.
    """
    plane_normal = Vector((float(normal[0]), float(normal[1]), float(normal[2])))
    plane_normal.normalize()

    outside = []
    for vert in bm.verts:
        distance = vert.co.dot(plane_normal) - offset
        if distance > 0.0:
            outside.append((vert, distance))
    if not outside:
        return []

    points = np.array([[v.co.x, v.co.y, v.co.z] for v, _d in outside])
    if roughness is not None and roughness_amp > 0.0:
        # Sample the field at the PROJECTED position, not the original one, or two vertices
        # that land on the same spot get different offsets and the facet tears.
        projected = points - np.outer(
            np.array([d for _v, d in outside]),
            np.array([plane_normal.x, plane_normal.y, plane_normal.z]))
        offsets = roughness.sample(projected) * roughness_amp
    else:
        offsets = np.zeros(len(outside))

    clamped = set()
    for index, (vert, distance) in enumerate(outside):
        vert.co -= plane_normal * distance
        vert.co += plane_normal * float(offsets[index])
        clamped.add(vert.index)

    facet = []
    for face in bm.faces:
        if all(v.index in clamped for v in face.verts):
            face.material_index = law.MATERIAL_SLOT_CUT_EDGE
            # 3DMODEL_GEOLOGY_ROCKS.md section 4: do not smooth a chipped plane into a blob.
            face.smooth = False
            for edge in face.edges:
                edge.smooth = False
            facet.append(face)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return facet


def truncate_summit(bm: bmesh.types.BMesh, frame: BeddingFrame, strata: Stratigraphy,
                    size: SizeClass, rng: np.random.Generator, quality: float,
                    blackbox: BlackBox) -> Optional[FracturePlane]:
    """Remove the top of the mass with ONE oblique plane, leaving a sloping summit facet.

    The front and side silhouettes of a ground-standing rock are dominated by its PROFILE,
    and a profile that tapers smoothly to a domed apex is the loaf read no amount of plan
    detail can rescue. Every geology reference frame in
    ``Docs/mandatory if you work on systems that user sees ...`` shows the opposite: cliff
    tops are planar shelves or sharp ridges, and the seafloor boulders in
    ``UNDERWATER PREVIOUSLY IN DEVELOPMENT.jpg`` are angular blocks with flat top faces.

    Deliberately a big cut, unlike the shear fractures: one plane taking 14-30 percent of
    the height cannot produce the "faceted gemstone" failure, which needed FIVE half-spaces
    intersecting near the centroid. The bedding-relief cost is bounded and paid knowingly --
    the beds below the cut are untouched, and the cut face is where the bedding TRACE gets
    re-imprinted by `imprint_bedding_on_cuts`.

    The tilt is bounded away from both the bedding normal (a cut parallel to the beds would
    read as a sawn slab) and from vertical (which would be a flank cut, not a summit).
    """
    q = law.saturate(quality)
    normal = np.array([frame.normal.x, frame.normal.y, frame.normal.z])
    e1 = np.array([frame.e1.x, frame.e1.y, frame.e1.z])
    e2 = np.array([frame.e2.x, frame.e2.y, frame.e2.z])

    tilt = math.radians(float(rng.uniform(20.0, 52.0)))
    azimuth = float(rng.uniform(0.0, 2.0 * math.pi))
    lateral = e1 * math.cos(azimuth) + e2 * math.sin(azimuth)
    plane_normal = normal * math.cos(tilt) + lateral * math.sin(tilt)
    plane_normal /= max(1e-9, np.linalg.norm(plane_normal))

    coords = np.array([[v.co.x, v.co.y, v.co.z] for v in bm.verts])
    distances = coords @ plane_normal
    volume_before = abs(bm.calc_volume(signed=True))

    # Quantile, not an absolute height: the body is tilted by the bedding dip and drifted
    # laterally, so a fixed height would clip a different fraction on every seed. Lightened
    # from 0.70-0.86 to 0.80-0.91 once the summit and the shear cuts started sharing one
    # volume budget -- at the old value the summit alone could eat a third of the rock.
    keep = float(rng.uniform(0.80 + 0.02 * q, 0.91))
    # A FRACTION OF THE BODY'S EXTENT ALONG THIS NORMAL, not a quantile of the vertex
    # distribution. The quantile version coupled the cut depth to the STATISTICS of the
    # surface noise, and that coupling fired the moment the grain field was band-limited:
    # the old aliased grain scattered vertices into a long tail, so the 0.80-0.91 quantile
    # sat high and removed little, and with a smooth grain the same quantile sits much
    # closer to the mean and ate the rock. Measured on the cliff chunk with
    # `--debug-stage lattice` versus `--debug-stage fracture`: the lattice was an upright
    # blocky mass and the post-cut body was a low flat wedge -- the "flake" failure
    # `CUT_VOLUME_BUDGET_FRACTION` above is written to prevent, arriving through a stage
    # that never checked itself against that budget.
    #
    # An extent fraction keeps the author's original intent -- the reason the quantile was
    # chosen was dip and drift invariance, and measuring ALONG THE CUT NORMAL is invariant
    # to both for the same reason -- while being independent of how rough the surface is.
    # Robust percentiles rather than min/max so one spike cannot define the span.
    low = float(np.percentile(distances, 2.0))
    high = float(np.percentile(distances, 98.0))
    offset = low + keep * (high - low)
    # The plane must ALWAYS sit below the highest point by a real margin, whatever the quantile
    # happened to select. Measured on the 7.6 m cliff chunk: a quantile-only offset left the
    # poked top cap intact above the plane, so the summit rendered as the radial starburst fan
    # that this file's history already records being rejected once. A quantile is a statement
    # about the vertex POPULATION and the cap is a handful of vertices, so on a tall body with
    # thousands of flank vertices the quantile can land under it.
    ceiling = float(distances.max()) - size.longest_extent_m * 0.045
    offset = min(offset, ceiling)
    ripple = AnisotropicField(
        rng, WITNESS_GRAIN_WAVELENGTH_M * 4.0, octaves=2, waves_per_octave=5,
        bedding_normal=np.array([frame.normal.x, frame.normal.y, frame.normal.z]),
        anisotropy=2.2)
    faces = _plane_clamp(bm, plane_normal, offset, ripple,
                         WITNESS_GRAIN_WAVELENGTH_M * (0.22 + 0.26 * q))
    if not faces:
        blackbox.record("truncate_summit", warning="summit plane produced no cut face",
                        failure_code="SUMMIT_CUT_NONE")
        return None

    volume_after = abs(bm.calc_volume(signed=True))
    spent = volume_before - volume_after
    area = _facet_area(faces)
    depth = spent / max(1e-9, area)
    blackbox.record("truncate_summit", vertex_count=len(bm.verts),
                    triangle_count=len(bm.faces),
                    warning="tilt {t:.1f} deg, keep {k:.3f}, {r:.1%} of volume, "
                            "facet {a:.4f} m2, mean depth {d:.4f} m, {f} tris".format(
                                t=math.degrees(tilt), k=keep,
                                r=spent / max(1e-9, volume_before), a=area, d=depth,
                                f=len(faces)))
    plane = FracturePlane(plane_normal * offset, plane_normal, "summit_truncation")
    plane.volume_spent = spent
    plane.volume_start = volume_before
    return plane


def imprint_bedding_on_cuts(bm: bmesh.types.BMesh, frame: BeddingFrame,
                            strata: Stratigraphy, size: SizeClass,
                            blackbox: BlackBox) -> int:
    """Step the fracture faces at every bed contact. Returns vertices moved.

    A planar cut through bedded stone erases the bedding relief on the face it creates,
    which is the real cost of using cuts at all, and it is why the previous author shrank
    them to invisibility. Nature does not pay that cost: a joint face in a bedded sequence
    steps in and out by a few millimetres to a centimetre as it crosses each bed, because
    each bed's fracture toughness differs. That step is what makes a fracture face read as
    broken bedded stone instead of as a saw cut.

    Implemented as a displacement ALONG THE FACE NORMAL of slot-1 geometry only, keyed to
    the bed the vertex sits in. Amplitude is 6-14 mm -- large enough to catch a grazing
    highlight and to put a visible notch on the outline where the cut face meets the
    silhouette, small enough that it cannot re-round the facet or self-intersect.
    """
    if not strata.beds:
        return 0
    step_m = min(0.014, max(0.004, size.longest_extent_m * 0.0045))

    # Accumulate PER VERTEX, apply ONCE. Iterating faces and moving each vertex inside the
    # loop moved every shared vertex once per incident cut face: a vertex on the arris between
    # three fracture facets was displaced 3x the intended step, up to 42 mm, which both
    # over-shot the authored amplitude and is a self-intersection risk. Averaging the incident
    # face normals is also the geometrically right direction on an arris -- a single face's
    # normal would drag the shared edge sideways out of both planes.
    accumulated = {}
    for face in bm.faces:
        if face.material_index != law.MATERIAL_SLOT_CUT_EDGE:
            continue
        face_normal = face.normal.copy()
        if face_normal.length <= 1e-9:
            continue
        face_normal.normalize()
        for vert in face.verts:
            entry = accumulated.get(vert.index)
            if entry is None:
                accumulated[vert.index] = [vert, face_normal.copy(), 1]
            else:
                entry[1] += face_normal
                entry[2] += 1

    moved = 0
    for vert, normal_sum, _count in accumulated.values():
        if normal_sum.length <= 1e-9:
            continue
        bed = strata.bed_at(vert.co.dot(frame.normal))
        if bed is None:
            continue
        # Hardness drives the sign: a competent bed stands proud of the joint face, a soft
        # parting is recessed into it. Same field the stain channel reads, so the geometry and
        # the colour agree by construction.
        amount = (bed.hardness - 0.5) * 2.0 * step_m
        vert.co += normal_sum.normalized() * amount
        moved += 1
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    blackbox.record("imprint_bedding_on_cuts", vertex_count=len(bm.verts),
                    triangle_count=len(bm.faces),
                    warning="{n} fracture-face vertices stepped at +-{s:.4f} m".format(
                        n=moved, s=step_m),
                    failure_code="" if moved else "CUT_FACE_IMPRINT_NONE")
    return moved


def collapse_thin_wedges(bm: bmesh.types.BMesh, blackbox: BlackBox, stage: str,
                         min_inradius_m: float = 6e-4, passes: int = 3) -> int:
    """Collapse wafer-thin triangles that are NOT small enough for the degenerate gate.

    ``collapse_slivers`` keys on AREA against ``law.DEGENERATE_TRIANGLE_AREA_EPS`` (1e-7 m2),
    so it cannot see a triangle 3 cm long and 0.5 mm wide -- area 7.5e-6 m2, seventy times the
    epsilon, and geometrically a wafer. Those are the feather edges a plane cut leaves wherever
    it grazes the surface tangentially, and they showed in the iteration-1 and iteration-2
    silhouettes as 1-2 px needles protruding several percent of the extent. A needle aliases,
    shades black, and reads as a mesh error.

    Thinness is measured as the inradius ``area / semiperimeter``, which is a LENGTH and so
    directly comparable to a physical tolerance, unlike an aspect ratio. Collapsing (rather
    than deleting) is the same discipline as ``collapse_slivers``: deleting opens a hole,
    filling the hole makes another sliver, and the two chase each other.
    """
    removed = 0
    for _attempt in range(max(1, passes)):
        targets = []
        seen = set()
        for face in bm.faces:
            if not face.is_valid or len(face.verts) < 3:
                continue
            edges = [e for e in face.edges if e.is_valid]
            if len(edges) < 3:
                continue
            perimeter = sum(e.calc_length() for e in edges)
            if perimeter <= 1e-9:
                continue
            if face.calc_area() / (perimeter * 0.5) >= min_inradius_m:
                continue
            shortest = min(edges, key=lambda e: e.calc_length())
            if shortest.index in seen:
                continue
            seen.add(shortest.index)
            targets.append(shortest)
        if not targets:
            break
        bmesh.ops.collapse(bm, edges=targets, uvs=True)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        removed += len(targets)
    left = 0
    for face in bm.faces:
        edges = [e for e in face.edges if e.is_valid]
        perimeter = sum(e.calc_length() for e in edges)
        if perimeter > 1e-9 and face.calc_area() / (perimeter * 0.5) < min_inradius_m:
            left += 1
    blackbox.record("collapse_thin_wedges:" + stage, triangle_count=len(bm.faces),
                    vertex_count=len(bm.verts),
                    warning="{r} wedges collapsed, {l} remain under {m:.5f} m "
                            "inradius".format(r=removed, l=left, m=min_inradius_m))
    return left


def cut_fractures(bm: bmesh.types.BMesh, frame: BeddingFrame, strata: Stratigraphy,
                  size: SizeClass, rng: np.random.Generator, quality: float,
                  process: str, blackbox: BlackBox,
                  volume_already_spent: float = 0.0,
                  volume_reference: float = 0.0) -> list:
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
    # FEW cuts, each one big enough to see. Two failures bracket this parameter and both are
    # recorded here so neither gets re-tried:
    #   five half-spaces at keep 0.80-0.93, all passing near the centroid -> a faceted
    #   gemstone with every bed gone;
    #   five half-spaces at keep 0.955-0.990 -> a 1-4.5 percent quantile shave per cut,
    #   invisible in silhouette (measured: turn concentration 0.325, against 0.137 for a
    #   displaced icosphere and 0.789 for a convex polytope, with the turning spread over
    #   many small rim nicks rather than concentrated in a few arrises).
    # The escape is neither amplitude: it is COUNT plus PLACEMENT. Two or three cuts, each
    # taking a real corner, on a body that is already polygonal in plan.
    conjugate = 2 if size.name == "boulder" else 2 + int(round(q))
    if process == "basalt":
        conjugate += 1

    normal = np.array([frame.normal.x, frame.normal.y, frame.normal.z])
    e1 = np.array([frame.e1.x, frame.e1.y, frame.e1.z])
    e2 = np.array([frame.e2.x, frame.e2.y, frame.e2.z])

    # Dip band 34-62 degrees from the BEDDING PLANE, i.e. the plane normal keeps a real
    # component both along and across bedding. The old 58-74 band produced near-vertical
    # planes whose facets only showed in plan view; a spall scar has to slope, or the front
    # and side silhouettes -- the two views that decide whether a rock reads -- never see it.
    planes = []
    base_azimuth = float(rng.uniform(0.0, 2.0 * math.pi))
    for i in range(conjugate):
        sign = 1.0 if i % 2 == 0 else -1.0
        shear_dip = math.radians(float(rng.uniform(34.0, 62.0)))
        azimuth = (base_azimuth + i * float(rng.uniform(1.55, 2.60))
                   + (0.0 if sign > 0 else math.pi * 0.35))
        lateral = e1 * math.cos(azimuth) + e2 * math.sin(azimuth)
        plane_normal = lateral * math.cos(shear_dip) + normal * (sign * math.sin(shear_dip))
        plane_normal /= max(1e-9, np.linalg.norm(plane_normal))
        planes.append(FracturePlane(np.zeros(3), plane_normal, "conjugate_shear"))

    # One bedding-parallel pressure break at a soft interface: the slab that let go.
    soft = min(strata.beds, key=lambda b: b.hardness)
    parting_normal = normal.copy()
    if rng.random() < 0.5:
        parting_normal = -parting_normal
    planes.append(FracturePlane(normal * soft.top_h, parting_normal, "bedding_parting"))

    # Budget in VOLUME, not in vertex count. Vertex count is a proxy for nothing: the
    # lattice is denser near the ledges, so an identical geometric bite spent a different
    # share of the old 14-percent vertex budget on every seed, which is why the cut depth
    # had to be shrunk until it always fitted. Volume is the quantity the eye actually
    # judges, it is scale-free, and `bm.calc_volume` measures it directly.
    # Reference volume is the body BEFORE the summit truncation, passed in, so the summit
    # and the shear cuts draw on one shared ceiling. Measuring it here instead would reset
    # the denominator after the summit had already spent part of the rock.
    volume_start = volume_reference if volume_reference > 0.0 \
        else abs(bm.calc_volume(signed=True))
    volume_budget = volume_start * CUT_VOLUME_BUDGET_FRACTION
    volume_removed = max(0.0, volume_already_spent)
    min_depth = size.longest_extent_m * CUT_MIN_MEAN_DEPTH_FRACTION

    created = []
    for index, plane in enumerate(planes):
        if volume_removed >= volume_budget:
            blackbox.record("fracture_budget_reached",
                            warning="stopped after {r:.1%} of volume removed".format(
                                r=volume_removed / max(1e-9, volume_start)))
            break
        coords = np.array([[v.co.x, v.co.y, v.co.z] for v in bm.verts])
        distances = coords @ plane.normal
        # First cut is the deep spall; later ones are progressively lighter, so the body
        # cannot converge on the intersection of many equal half-spaces.
        if plane.kind == "bedding_parting":
            keep_fraction = float(rng.uniform(0.91, 0.96))
        elif index == 0:
            keep_fraction = float(rng.uniform(0.80, 0.88))
        else:
            keep_fraction = float(rng.uniform(0.87, 0.94))
        cut_at = float(np.quantile(distances, keep_fraction))
        if int((distances > cut_at).sum()) < 3:
            continue

        before = abs(bm.calc_volume(signed=True))
        snapshot = bm.copy()
        facet_ripple = AnisotropicField(
            rng, WITNESS_GRAIN_WAVELENGTH_M * 4.0, octaves=2, waves_per_octave=5,
            bedding_normal=normal, anisotropy=2.2)
        faces = _plane_clamp(bm, plane.normal, cut_at, facet_ripple,
                             WITNESS_GRAIN_WAVELENGTH_M * (0.20 + 0.24 * q))
        after = abs(bm.calc_volume(signed=True))
        spent = before - after
        area = _facet_area(faces)
        depth = spent / max(1e-9, area)

        reject = ""
        if not faces:
            reject = "no facet"
        elif volume_removed + spent > volume_budget:
            reject = "over the {b:.0%} shared volume budget".format(
                b=CUT_VOLUME_BUDGET_FRACTION)
        elif depth < min_depth:
            reject = ("grazing cut: mean depth {d:.4f} m under the {m:.4f} m floor, "
                      "which is the feather-edge source".format(d=depth, m=min_depth))
        if reject:
            # ROLL BACK rather than accept a bad cut. Skipping a cut on an ESTIMATE -- which
            # the vertex-count version did -- means the check never sees what the cut
            # actually did; that estimate was wrong by enough that the cuts ended up at a
            # 1 percent quantile. Measure, then keep or revert.
            bm.clear()
            bm.from_mesh(_bmesh_to_temp_mesh(snapshot))
            snapshot.free()
            blackbox.record("fracture_rejected:" + plane.kind, warning=reject)
            continue
        snapshot.free()
        volume_removed += spent
        plane.origin = plane.normal * cut_at
        plane.volume_spent = spent
        plane.volume_start = volume_start
        created.append(plane)
        blackbox.record("fracture_cut:" + plane.kind,
                        vertex_count=len(bm.verts), triangle_count=len(bm.faces),
                        warning="keep {k:.3f}, {s:.1%} of volume, facet {a:.4f} m2, "
                                "mean depth {d:.4f} m, {f} tris".format(
                                    k=keep_fraction, s=spent / max(1e-9, volume_start),
                                    a=area, d=depth, f=len(faces)))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    blackbox.record("cut_fractures", vertex_count=len(bm.verts),
                    triangle_count=len(bm.faces),
                    warning="{n} planes cut, {r:.1%} of volume removed".format(
                        n=len(created), r=volume_removed / max(1e-9, volume_start))
                    if created else "no fracture plane took effect",
                    failure_code="" if created else "FRACTURE_NONE")
    return created


def _bmesh_to_temp_mesh(bm: bmesh.types.BMesh) -> bpy.types.Mesh:
    """Materialise a bmesh into a throwaway datablock so it can be read back in.

    ``BMesh`` has no assignment operator, and ``bm.from_mesh`` is the only route back, so a
    rollback has to go through a temporary datablock. The block is removed on the next call,
    keeping exactly one alive -- leaking one datablock per rejected cut would inflate the
    .blend and, worse, leave orphan meshes that `purge_scene` counts as users.
    """
    existing = bpy.data.meshes.get("H8_RockRollback")
    if existing is not None:
        bpy.data.meshes.remove(existing)
    mesh = bpy.data.meshes.new("H8_RockRollback")
    bm.to_mesh(mesh)
    return mesh


# ---------------------------------------------------------------------------
# Stage 4b: bedding partings, mineral seams, macro vugs
# ---------------------------------------------------------------------------

@dataclass
class SeamPlane:
    origin_h: float
    normal: np.ndarray
    half_width_m: float
    kind: str


def _bedding_recession_at(strata: Stratigraphy, d: float) -> float:
    """Recession in metres for bedding coordinate ``d``, smoothed across each contact.

    A hard switch at the contact would put two rings of very different offset on adjacent
    lattice rows and shear the quad between them into a wafer. The window is a small
    fraction of the THINNER of the two beds, so a thick competent bed still presents an
    almost-square shoulder while a 2 cm parting cannot produce a discontinuity wider
    than itself.
    """
    beds = strata.beds
    if not beds:
        return 0.0
    if d <= beds[0].base_h:
        return beds[0].recession_m
    if d >= beds[-1].top_h:
        return beds[-1].recession_m
    for bed in beds:
        if not (bed.base_h <= d <= bed.top_h):
            continue
        if bed.index == 0:
            return bed.recession_m
        previous = beds[bed.index - 1]
        window = max(1e-6, min(bed.thickness, previous.thickness) * 0.22)
        local = (d - bed.base_h) / window
        if local >= 1.0:
            return bed.recession_m
        t = local * local * (3.0 - 2.0 * local)
        return previous.recession_m + (bed.recession_m - previous.recession_m) * t
    return beds[-1].recession_m


def erode_bedding_planes(bm: bmesh.types.BMesh, frame: BeddingFrame,
                         strata: Stratigraphy, size: SizeClass, quality: float,
                         blackbox: BlackBox) -> dict:
    """Differential erosion between hard and soft beds. THE bedding mechanism.

    ``3DMODEL_GEOLOGY_ROCKS.md`` section 1 requires a shape that "contains readable
    geological process ... sediment bands ... erosion shelves", and section 9 rejects an
    asset where "no geological process is visible in silhouette". This is where that
    process now happens, and it replaces three earlier mechanisms rather than joining them:
    the per-bed ring radius, the per-bed plan phase and the per-bed joint-face inset.

    WHY THE OLD ROUTE COULD NOT BE TUNED INTO CORRECTNESS. Bed relief used to be a radius
    as a function of bedding height. The body is a lat-long lattice about the bedding
    normal, so ANY function of height is axisymmetric by construction: every bed became a
    closed ribbon wrapping the silhouette at constant height, which is a contour line on a
    topographic map, not a bed. That is a grammar error and this file's own history is four
    rounds of trying to tune out of it -- "pancake plates", "poker chips", "stack of slate
    tiles", "peeled sheet metal" -- each one changing an amplitude, a gate or a clamp.
    ``AGENTS.md`` ``[RULE] Universal route invalidation`` and ``[RULE] Same-failure
    escalation`` both say to replace the route instead, so the route is replaced.

    THE TWO PROPERTIES THAT MAKE THIS BEDDING RATHER THAN BANDING:

    1.  Membership is the PLANAR coordinate ``d = p . n`` and nothing else, so a bed is a
        slab of space with a dip and a strike that cuts THROUGH the mass. Its trace on the
        surface is the intersection curve of a plane with an irregular surface, which
        wanders with the surface and cannot be a silhouette contour.
    2.  Displacement is along the vertex's OWN surface normal, gated by
        ``exposure = 1 - |n_surface . n_bedding|``. This is the term that carries the
        physics and the one the old route had no equivalent of. A bed only weathers back
        where its CUT EDGE is exposed: on a bedding-perpendicular face, exposure is 1 and
        the bed recedes fully; on a bedding-parallel surface -- a bench tread, the top of a
        competent bed -- exposure is 0 and nothing moves, because there you are looking at
        the bed's own face, not at its edge. So the bands are strong on steep faces, widen
        and fade on shallow ones, and vanish on benches. A function of height alone can
        never do that, which is precisely why the old output read as contour lines.

    Consequence worth naming: the arris jog that the per-bed ``face_inset`` was invented to
    produce still appears, because a joint face is bedding-perpendicular and therefore takes
    full recession that differs bed by bed. One mechanism, two features, and the jog is now
    a consequence of the geology instead of a second decorative pass.
    """
    q = law.saturate(quality)
    normal = frame.normal
    bm.normal_update()

    # Offsets are computed from the ORIGINAL normals for every vertex before any vertex
    # moves. Applying in-place would make each vertex's recession depend on how many of its
    # neighbours had already moved, i.e. on iteration order, which is both wrong and a
    # determinism hazard.
    plan = []
    for vert in bm.verts:
        surface = vert.normal
        if surface.length <= 1e-9:
            continue
        surface = surface.normalized()

        # THE EXPOSURE GATE READS A SMOOTHED NORMAL; THE DISPLACEMENT USES THE REAL ONE.
        #
        # Exposure asks a LOW-FREQUENCY question -- "is this part of the rock a steep face or a
        # bench?" -- so feeding it the raw per-vertex normal made the answer jump from facet to
        # facet across the fracture facets and chip chamfers. Adjacent vertices then took very
        # different recessions and every facet border became a crease. Measured on the cliff
        # chunk: LOD0 round trip failed at corner-normal delta 0.001013 against a 0.001
        # tolerance, while the identical build with the relief turned down to 0.02 sat at
        # 0.000535 -- the pipeline's own noise floor. So the amplitude was never the defect,
        # the per-facet jitter in the gate was, and turning the relief down would have traded
        # away the feature to hide the cause.
        #
        # Averaging with the one-ring neighbours answers the same question about the same
        # neighbourhood without the jitter, and the recession still travels along the vertex's
        # OWN true normal, so a facet keeps its orientation and no geometry is smoothed away.
        smoothed = surface.copy()
        for edge in vert.link_edges:
            other = edge.other_vert(vert).normal
            if other.length > 1e-9:
                smoothed += other.normalized()
        smoothed = smoothed.normalized() if smoothed.length > 1e-9 else surface

        exposure = 1.0 - abs(smoothed.dot(normal))
        if exposure <= 1e-4:
            continue
        recession = _bedding_recession_at(strata, vert.co.dot(normal))
        if abs(recession) <= 1e-6:
            continue
        # Exposure is squared so a bench tread stays genuinely flat instead of taking a
        # weak smeared version of the band, which is what re-introduces the wrapped read.
        plan.append((vert, surface, recession * exposure * exposure))

    # One shared ceiling, as a fraction of the LOCAL edge length, for the same reason the
    # grain displacement has one: a recession deeper than the spacing between the rings that
    # carry it folds the quad through itself and the weld then deletes the bed entirely.
    # That is the "the grammar was producing them all along and the cleanup was removing
    # them" failure recorded in `build_stratigraphy`.
    moved = 0
    deepest = 0.0
    proudest = 0.0
    for vert, surface, offset in plan:
        linked = vert.link_edges
        if not linked:
            continue
        local_edge = sum(e.calc_length() for e in linked) / len(linked)
        # 0.70, the same ceiling the grain displacement uses, and for the same reason. It is a
        # SAFETY NET: if it is ever the operative value the amplitude above is wrong, so the
        # report records the deepest achieved offset for exactly that check.
        limit = local_edge * 0.70
        amount = max(-limit, min(limit, offset))
        vert.co -= surface * amount
        moved += 1
        deepest = max(deepest, amount)
        proudest = min(proudest, amount)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.normal_update()
    report = {
        "mechanism": "planar bedding slabs, recession along the surface normal, gated by "
                     "exposure = (1 - |n_surface . n_bedding|)^2",
        "bedCount": len(strata.beds),
        "verticesMoved": moved,
        "verticesConsidered": len(bm.verts),
        "deepestRecessionM": round(float(deepest), 5),
        "proudestReliefM": round(float(-proudest), 5),
        "recessionRangeM": [round(float(min(b.recession_m for b in strata.beds)), 5),
                            round(float(max(b.recession_m for b in strata.beds)), 5)],
        "dipDeg": round(frame.dip_deg, 3),
        "dipAzimuthDeg": round(frame.dip_azimuth_deg, 3),
    }
    blackbox.record("erode_bedding_planes", vertex_count=len(bm.verts),
                    triangle_count=len(bm.faces),
                    warning="moved {m} of {t} verts, deepest {d:.4f} m, proudest "
                            "{p:.4f} m".format(m=moved, t=len(bm.verts), d=deepest,
                                               p=-proudest))
    return report


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
        # Depth cut ~2.8x. At 0.012-0.028 of extent this was 8 cm on the outcrop, and once the
        # body became blocky that stopped reading as a weathered parting and started reading as
        # a milled trench: the iteration-6 studio sheet showed a wide straight black slot with
        # parallel walls running the length of the mass in three of four views. A parting is a
        # groove, not a channel; the AO channel is what is supposed to make it read dark, not
        # the depth.
        # Halved once `erode_bedding_planes` landed, because the two passes now target the
        # SAME soft beds and their offsets ADD along the same surface normal. Measured on the
        # cliff chunk: erosion 0.145 m plus parting 0.076 m at the same vertices is a 0.22 m
        # gouge on a 0.17 m lattice spacing, which folds. The recession is the erosion's job
        # now; this pass exists only for the narrow occluded crack section 4 wants for the AO
        # channel, so it is a crack on top of a shelf rather than a second shelf.
        depth = size.longest_extent_m * (0.002 + 0.003 * q)
        centre = (bed.base_h + bed.top_h) * 0.5

        # A CONSTANT-depth, constant-width groove cut all the way round a body whose faces
        # are now flat is a MILLED SLOT, and it rendered as one: the iteration-3 studio sheet
        # showed straight parallel channels with square shoulders, indistinguishable from
        # panel gaps or heat-sink fins. `TASTE.md` rejects "clean sci-fi plastic" and
        # "decorative sci-fi panels" on sight. The operation was acceptable on the old lumpy
        # body only because the body hid it.
        #
        # A weathered parting is exposed WHERE IT IS EXPOSED: deep in one sector, pinched out
        # in another, and interrupted where a competent lens bridges it. So depth, width and
        # continuity are all modulated around the azimuth by a seeded harmonic set, and the
        # groove is gated off entirely below a threshold.
        orders = rng.choice(np.arange(2, 7), size=3, replace=False)
        phases = rng.uniform(0.0, 2.0 * math.pi, size=3)
        weights = rng.uniform(0.45, 1.0, size=3)
        weights = weights / weights.sum()
        gate_bias = float(rng.uniform(-0.18, 0.22))

        def exposure(theta: float) -> float:
            value = 0.0
            for order, phase, weight in zip(orders, phases, weights):
                value += weight * math.sin(order * theta + phase)
            # Map roughly [-1, 1] to [0, 1] and then gate: below the threshold the parting is
            # simply not developed at that azimuth.
            level = law.saturate(0.5 + 0.5 * value + gate_bias)
            if level < 0.30:
                return 0.0
            level = (level - 0.30) / 0.70
            return level * level * (3.0 - 2.0 * level)

        moved = 0
        exposed_arc = 0
        bm.normal_update()
        # Same two-phase shape as `erode_bedding_planes`, and for the same reason: read every
        # normal before moving anything.
        groove = []
        for vert in bm.verts:
            h = vert.co.dot(frame.normal)
            radial = vert.co - frame.normal * h
            if radial.length <= 1e-6:
                continue
            theta = math.atan2(radial.dot(frame.e2), radial.dot(frame.e1))
            gate = exposure(theta)
            if gate <= 0.0:
                continue
            exposed_arc += 1
            # Width tracks exposure too: a pinching-out parting narrows as it shallows, which
            # is what removes the constant-section machined read.
            local_width = half_width * (0.45 + 0.55 * gate)
            offset = abs(h - centre)
            if offset >= local_width:
                continue
            falloff = 1.0 - (offset / local_width)
            falloff = falloff * falloff * (3.0 - 2.0 * falloff)
            # ALONG THE SURFACE NORMAL, NOT RADIALLY OUT FROM THE BEDDING AXIS.
            #
            # The membership test above is already planar -- `|h - centre| < width` is a slab
            # with the bedding dip -- so this function had the right idea and the wrong
            # delivery: `radial.normalized()` is a CYLINDRICAL push, so the groove wrapped the
            # form at constant height exactly like the bed ribbons did, and on an overhang it
            # cut sideways through the rock instead of into the exposed face. The surface
            # normal plus the same bedding-exposure gate the erosion pass uses makes the
            # groove follow the outcrop: deep on a face that presents the parting's edge,
            # absent on a bench where the parting is not exposed at all.
            surface = vert.normal
            if surface.length <= 1e-9:
                continue
            surface = surface.normalized()
            bedding_exposure = 1.0 - abs(surface.dot(frame.normal))
            if bedding_exposure <= 1e-4:
                continue
            groove.append((vert, surface,
                           depth * gate * falloff * bedding_exposure * bedding_exposure))
        for vert, surface, amount in groove:
            vert.co -= surface * amount
            moved += 1
        if moved:
            seams.append(SeamPlane(centre, normal, half_width, "bedding_parting_groove"))
            blackbox.record("parting_groove",
                            warning="bed {b}: depth {d:.4f} m, half width {w:.4f} m, "
                                    "{m} verts moved, exposed on {e} of {t} sampled "
                                    "azimuth positions".format(
                                        b=bed.index, d=depth, w=half_width, m=moved,
                                        e=exposed_arc, t=len(bm.verts)))

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
    # Density cut hard after inspecting the render: at (2.6 + 5.4q) the outcrop got 142
    # nested inset pockets and they read as torn dark holes and spikes, not cavities --
    # too many, too deep, and overlapping each other. Vugs are an accent on the bedded
    # form; the ledges are the primary cavity source.
    density = (0.7 + 1.5 * q) * (1.8 if process == "basalt" else 1.0)

    candidates = []
    for face in bm.faces:
        if face.material_index == law.MATERIAL_SLOT_CUT_EDGE:
            continue
        area = face.calc_area()
        if area <= 0.0:
            continue
        inradius = math.sqrt(area / math.pi)
        # 2.5x margin, not 1.75x. A nested inset on a face only marginally larger than the
        # pocket leaves a hairline rim, and those slivers are the source of the
        # non-manifold junctions that the rim-repair pass then cannot fix cleanly
        # (measured: 3-31 non-manifold edges, aborting 4 of 12 matrix configs).
        if inradius > target_radius * 2.5:
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
        depth = radius * float(rng.uniform(0.45, 0.85))
        rim_verts = {v.index for v in face.verts}
        first = bmesh.ops.inset_individual(
            bm, faces=[face], thickness=thickness * 0.45, depth=-depth,
            use_even_offset=True, use_interpolate=True, use_relative_offset=False)
        pocket_faces = [f for f in first.get("faces", ()) if f.is_valid]
        if not pocket_faces:
            continue

        # `inset_individual` on a quad produces a RECTANGULAR pocket with parallel sides.
        # On the old lumpy surface that was hidden; on flat joint faces the iteration-3
        # studio sheet showed them as recessed service panels -- exactly the "decorative
        # sci-fi panels" `TASTE.md` rejects. A wave-drilled vug or a basalt vesicle is
        # irregular, so the new pocket vertices are jittered in all three axes by up to a
        # third of the pocket radius. Bounded by the pocket radius rather than by an absolute
        # figure so it cannot punch through the far wall on a small vug.
        jitter = radius * 0.34
        for pocket in pocket_faces:
            for vert in pocket.verts:
                if vert.index in rim_verts:
                    continue        # keep the surrounding surface where the body put it
                vert.co += Vector((float(rng.uniform(-jitter, jitter)),
                                   float(rng.uniform(-jitter, jitter)),
                                   float(rng.uniform(-jitter, jitter))))
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
                          stage: str, passes: int = 5,
                          tiny_perimeter_m: float = 0.012,
                          stubborn_perimeter_fraction: float = 0.06) -> int:
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
    # The stubborn-rim bound is a FRACTION of the body's own bounding diagonal, not an absolute
    # length. An absolute 0.30 m closed the 0.4 m boulder and the 2.9 m outcrop and left the
    # 7.6 m cliff chunk failing at 8 boundary edges, because its repair-artefact rims are
    # proportionally larger. A tolerance that only holds for one size class is the same defect
    # as a threshold copied from another family.
    if bm.verts:
        xs = [v.co.x for v in bm.verts]
        ys = [v.co.y for v in bm.verts]
        zs = [v.co.z for v in bm.verts]
        diagonal = math.sqrt((max(xs) - min(xs)) ** 2 + (max(ys) - min(ys)) ** 2
                             + (max(zs) - min(zs)) ** 2)
    else:
        diagonal = 1.0
    stubborn_perimeter_m = max(tiny_perimeter_m * 2.0,
                               diagonal * stubborn_perimeter_fraction)

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
            alive = [e for e in loop_edges if e.is_valid]
            if not alive:
                continue
            perimeter = sum(e.calc_length() for e in alive)
            # A rim left behind by deleting a sliver is itself a sliver, so FILLING it
            # produces another sub-epsilon triangle and the degenerate gate fires again.
            # COLLAPSING a tiny rim to a point removes it outright. Targeted per-rim, so
            # unlike a global 1 mm weld -- which produced 11 non-manifold edges by merging
            # across the thin ledge annuli -- authored features are untouched.
            if perimeter < tiny_perimeter_m:
                bmesh.ops.collapse(bm, edges=alive, uvs=True)
                progressed = True
                continue
            filled = bmesh.ops.holes_fill(bm, edges=alive, sides=0)
            new_faces = [f for f in filled.get("faces", ()) if f.is_valid]
            if not new_faces:
                # `holes_fill` REFUSES a non-simple rim -- one that touches itself at a vertex,
                # which is what scattered sliver removal leaves behind. Measured: the basalt
                # outcrop and the cliff chunk each froze at 19 and 8 boundary edges through six
                # repair passes, because every pass handed the same unfillable loop to the same
                # operator. Retrying that is the same-failure escalation `AGENTS.md` forbids;
                # changing the operator is the strategy change it demands.
                #
                # Collapsing the rim to a point always closes it. It costs a small patch of
                # surface, so it is bounded by rim perimeter against the asset extent: a rim
                # this small is a repair artefact, and a rim larger than the bound is a real
                # hole that must reach the gate rather than be quietly pinched shut.
                if perimeter < stubborn_perimeter_m:
                    bmesh.ops.collapse(bm, edges=alive, uvs=True)
                    progressed = True
                    if blackbox is not None:
                        blackbox.record(
                            "stubborn_rim_collapsed:" + stage,
                            warning="holes_fill refused a {n}-edge rim of {p:.4f} m "
                                    "perimeter; collapsed instead".format(
                                        n=len(alive), p=perimeter))
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

    # law.BEVEL_RANGES[Family.GEOLOGY] now exists (0.008-0.09 m), deliberately wide so the
    # three-bucket treatment below keeps its spread instead of collapsing to one chamfer.
    # The local extent-fraction table this replaced is deleted.
    chip_range = law.BEVEL_RANGES[law.Family.GEOLOGY]
    requested_by_size = chip_range.width_for(q)

    # Bevel width is bounded by MESH RESOLUTION, not just by asset size.
    # law.BEVEL_WIDTH_CLAMP_RATIO caps every bevel at 20 percent of the shortest adjacent
    # edge, so a mesh whose edges average 37 mm cannot carry a chamfer wider than ~7 mm no
    # matter what the size-based table asks for. Measured: the extent-based nominal for a
    # 2.9 m outcrop is 75 mm, 10x what the geometry supports, so two of the three buckets
    # found zero eligible edges and the chip pass silently did almost nothing.
    #
    # So the nominal is the smaller of the two, and the honest consequence is recorded:
    # within a 9,000-triangle budget, chamfers are millimetre-scale and MACRO spalls come
    # from the fracture planes as real facets, which is the correct division anyway --
    # 3dmodel.md section 4 wants a chamfer on every hard edge, not a 75 mm round-over that
    # would swallow a 25 mm sediment ledge whole.
    lengths = sorted(_local_shortest_edge(e) for e in hard)
    median_local = lengths[len(lengths) // 2] if lengths else 0.0
    resolution_cap = median_local * law.BEVEL_WIDTH_CLAMP_RATIO
    nominal = min(requested_by_size, resolution_cap) if resolution_cap > 0.0 \
        else requested_by_size
    if process == "basalt":
        nominal *= 0.72          # sharper, less rounded breaks on volcanic rock

    # Relative to the cap, so the widest bucket sits exactly at what the mesh can carry.
    multipliers = (1.0, 0.55, 0.25)
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

    # ShaderNodeMix carries one A/B pair PER DATA TYPE and they all share the names "A"
    # and "B", so inputs["A"] silently resolves to the FLOAT socket. Assigning a colour
    # there leaves the Color sockets at their defaults and the whole graph renders white --
    # which is exactly what the first material contact sheet showed. Colour sockets are
    # index 6 and 7, colour Result is output index 2, Factor(Float) is input 0.
    MIX_FACTOR, MIX_A, MIX_B, MIX_RESULT = 0, 6, 7, 2

    stain = nodes.new("ShaderNodeMix")
    stain.data_type = "RGBA"
    stain.blend_type = "MIX"
    stain.inputs[MIX_A].default_value = (*base_color, 1.0)
    stain.inputs[MIX_B].default_value = (*ROCK_ALGAE, 1.0)
    links.new(separate.outputs["Green"], stain.inputs[MIX_FACTOR])

    reveal = nodes.new("ShaderNodeMix")
    reveal.data_type = "RGBA"
    reveal.blend_type = "MIX"
    reveal.inputs[MIX_B].default_value = (*ROCK_FRESH, 1.0)
    links.new(stain.outputs[MIX_RESULT], reveal.inputs[MIX_A])
    # Chip reveal is a partial lerp: a fresh spall lightens the surface, it does not
    # replace the rock.
    reveal_gain = nodes.new("ShaderNodeMath")
    reveal_gain.operation = "MULTIPLY"
    reveal_gain.inputs[1].default_value = 0.55
    links.new(separate.outputs["Red"], reveal_gain.inputs[0])
    links.new(reveal_gain.outputs["Value"], reveal.inputs[MIX_FACTOR])

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
    occlude.inputs[MIX_FACTOR].default_value = 1.0
    links.new(reveal.outputs[MIX_RESULT], occlude.inputs[MIX_A])
    ao_color = nodes.new("ShaderNodeCombineColor")
    links.new(ao_floor.outputs["Value"], ao_color.inputs["Red"])
    links.new(ao_floor.outputs["Value"], ao_color.inputs["Green"])
    links.new(ao_floor.outputs["Value"], ao_color.inputs["Blue"])
    links.new(ao_color.outputs["Color"], occlude.inputs[MIX_B])

    # Algae and wet stain are glossier than dry rock.
    rough = nodes.new("ShaderNodeMapRange")
    rough.inputs["From Min"].default_value = 0.0
    rough.inputs["From Max"].default_value = 1.0
    rough.inputs["To Min"].default_value = roughness
    rough.inputs["To Max"].default_value = max(0.18, roughness - 0.34)
    links.new(separate.outputs["Green"], rough.inputs["Value"])

    links.new(occlude.outputs[MIX_RESULT], bsdf.inputs["Base Color"])
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
    # Capture the NAME now, as a Python string. `mesh.uv_layers.new` below reallocates the
    # CustomData layer array, and a `bpy_prop` wrapper obtained before that points into the
    # old allocation. Reading `uv0.name` after UV1 exists is a dangling read: it returned
    # garbage bytes and raised `UnicodeDecodeError: 'utf-8' codec can't decode byte 0xb3 in
    # position 2` from the manifest builder, which is undefined behaviour that depends on
    # the allocator, so it fires at random rather than every run.
    uv0_name = str(uv0.name)
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
        "uv0": {"name": uv0_name, "route": "smart_project_angle_based_fallback",
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

def signed_volume(mesh: bpy.types.Mesh) -> float:
    """Signed volume of a closed mesh. NEGATIVE means the shell is inside-out.

    A loud probe for the one failure that silently ruins everything downstream: with
    inverted winding the AO bake samples the interior, so occlusion comes back near zero
    everywhere and the B channel is garbage while every count-based check still passes.
    Measured symptom that sent me here: AO mean 0.0058 on a closed manifold rock whose
    sphere/cube control baked to a correct 1.0.
    """
    mesh.calc_loop_triangles()
    total = 0.0
    for tri in mesh.loop_triangles:
        a = mesh.vertices[tri.vertices[0]].co
        b = mesh.vertices[tri.vertices[1]].co
        c = mesh.vertices[tri.vertices[2]].co
        total += a.dot(b.cross(c))
    return total / 6.0


def ensure_outward_winding(obj: bpy.types.Object, blackbox: BlackBox,
                           stage: str) -> float:
    """Flip the shell outward if it is inside-out, measured by signed volume.

    Must run AFTER the last ``recalc_face_normals`` in the pipeline, which is the whole
    point. Verified sequence that defeated an earlier placement: the fix ran inside the
    bmesh stage, then ``weld_and_clean`` -> ``recalc_face_normals`` during post-decimation
    cleanup flipped the body straight back to inward, reproducing signed volume
    -9.397259 m3 to the digit.

    ``recalc_face_normals`` only guarantees CONSISTENT normals. Its choice of which side
    is "outside" is a heuristic, and on this body -- deep bedding-parting grooves plus
    dozens of nested inset vugs -- it picks the inside. Verified against a control: a 2 m
    cube measures +8.0 by both ``bmesh.calc_volume(signed=True)`` and the loop-triangle
    formula, and -8.0 after a deliberate ``reverse_faces``, so the measurement is sound
    and the geometry really was inverted.
    """
    volume = signed_volume(obj.data)
    if volume >= 0.0:
        blackbox.record("winding_ok:" + stage,
                        triangle_count=mesh_ops.triangle_count(obj.data))
        return volume
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    bm.to_mesh(obj.data)
    obj.data.update()
    bm.free()
    corrected = signed_volume(obj.data)
    blackbox.record(
        "winding_flipped:" + stage, triangle_count=mesh_ops.triangle_count(obj.data),
        warning="signed volume {v:.5f} -> {c:.5f} m3".format(v=volume, c=corrected),
        failure_code="" if corrected > 0.0 else "WINDING_STILL_INVERTED")
    return corrected


def read_back_channels(mesh: bpy.types.Mesh) -> dict:
    """Read the packed attribute back OFF the mesh, per channel.

    Not a debug print: this is the gate that catches a vertex-colour set that was authored
    correctly and then lost or renamed before export. A ``ShaderNodeVertexColor`` pointing
    at a missing layer returns 1.0 for every channel, which renders as a uniform white
    subject and measures as four near-identical tiles -- indistinguishable from a
    legitimately saturated channel unless the stored data is read back directly.
    """
    attribute = mesh.color_attributes.get(law.VCOL_ATTRIBUTE_NAME)
    if attribute is None:
        return {"present": False,
                "layers": [a.name for a in mesh.color_attributes],
                "activeColorName": getattr(mesh.attributes, "active_color_name", ""),
                }
    out = {"present": True, "elements": len(attribute.data),
           "domain": attribute.domain, "dataType": attribute.data_type,
           "layers": [a.name for a in mesh.color_attributes],
           "activeColorName": getattr(mesh.attributes, "active_color_name", "")}
    for channel, key in enumerate("RGBA"):
        values = [attribute.data[i].color[channel] for i in range(len(attribute.data))]
        if not values:
            continue
        out["stored" + key] = [round(min(values), 5), round(max(values), 5),
                               round(sum(values) / len(values), 5)]
    return out


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

    # Degenerates are counted on LOOP TRIANGLES with the validator's own formula, not on bmesh
    # faces with `calc_area`. Two reasons, and the first is a measured miss:
    #
    #   A quad with one near-collinear corner has a perfectly healthy AREA, so a per-face check
    #   passes it, while its loop-triangle split produces one healthy triangle and one sliver.
    #   `h8forge.validate` reads loop triangles and reported exactly that -- cross length
    #   9.73e-08 against the 1e-07 epsilon -- while this gate reported zero degenerates on the
    #   same mesh. Two checks over the same asset disagreeing because one of them measured a
    #   representation Unity never sees.
    #
    #   `3dmodel.md` section 10 states the test as `length(cross(b - a, c - a)) > epsilon`,
    #   which is TWICE the triangle area, so comparing `calc_area()` to the same epsilon is also
    #   a factor-of-two mismatch against the bible's own wording.
    mesh.calc_loop_triangles()
    degenerate = 0
    for tri in mesh.loop_triangles:
        a = mesh.vertices[tri.vertices[0]].co
        b = mesh.vertices[tri.vertices[1]].co
        c = mesh.vertices[tri.vertices[2]].co
        if (b - a).cross(c - a).length <= law.DEGENERATE_TRIANGLE_AREA_EPS:
            degenerate += 1

    volume = signed_volume(mesh)
    return {
        "boundaryEdges": boundary,
        "nonManifoldEdges": non_manifold,
        # Not derivable from the three counts above, and that is the whole point: a
        # duplicate-face pair keeps every edge at exactly two faces, so it is manifold by
        # the edge test, has real area, and has no boundary. Only the FBX round trip saw it,
        # and only as lost geometry after the file was written.
        "duplicateFaces": duplicate_face_count(mesh),
        "degenerateFaces": degenerate,
        "looseVerts": loose,
        "islands": islands,
        "manifoldClosedSolid": boundary == 0 and non_manifold == 0 and islands == 1,
        "signedVolumeM3": round(volume, 6),
        "outwardWinding": volume > 0.0,
    }


def dedupe_faces_bm(bm: bmesh.types.BMesh) -> int:
    """Delete every face after the first on any given vertex set. Returns how many went.

    A duplicate face is the one topology defect in this generator that NOTHING local can
    see, and the FBX exporter is the only stage that reports it -- as lost geometry, after
    the fact. ``inspect_topology`` reads zero non-manifold edges, because both faces of the
    pair contribute to the same edges and every edge still has exactly two of them;
    ``collapse_slivers`` passes, because both faces have real area; there is no bowtie vertex
    and no boundary edge. FBX cannot express the pair at all, so the importer merges it and
    the file comes back with fewer faces, fewer corner normals and fewer colour elements than
    the mesh that was measured. Measured on the cliff chunk: LOD0 16210 -> 16206 triangles,
    colour elements 194496 -> 194472, corner normals 48624 -> 48618, and the same at LOD1 and
    LOD2 -- a whole package rejected with no local symptom to chase.

    Sources here are the repair passes themselves: ``holes_fill`` can bridge a rim that
    already carried a face, and ``bmesh.ops.collapse`` can pull two triangles onto the same
    triple. Both are legitimate operations with this as a side effect, so the answer is to
    measure and remove, not to stop repairing.

    Keyed on the SORTED vertex-index tuple, so winding cannot hide a duplicate: two faces on
    the same three vertices are the same face whichever way they are wound, and a
    back-to-back pair is exactly the 180-degree fold that also ruins the normal fan.

    DELETE IS NOT ALWAYS THE RIGHT REPAIR, and assuming it was cost a run. Two different
    topologies produce a duplicate key:

      - A genuine duplicate riding on top of shell geometry. Every edge of the doomed face
        then has THREE or more faces, so deleting it drops each back to two and the shell
        stays closed. This is the case ``mesh_ops._weld_coincident`` was written for.
      - A FLAP: ``holes_fill`` bridged a rim with a quad whose triangulation reproduces a
        triangle that already exists. Measured on the cliff chunk LOD0, seed 1713: quads
        [7349, 7350, 803, 827] and [333, 7796, 7795, 334], each splitting into one triangle
        that collides with an existing vertex set -- 16206 distinct loop-triangle vertex sets
        out of 16208. Here the flap's outer edges carry only the flap and one neighbour, so
        deleting it leaves those edges with a single face: the repair trades an invisible
        duplicate for a visible hole, and the closed-shell gate then fails instead.

    So the rule is measured per face rather than assumed: delete when every edge can afford
    to lose it, otherwise COLLAPSE the shortest edge, which removes the face without ever
    opening a boundary. That is the same reasoning ``collapse_slivers`` documents for
    degenerate faces, applied to a defect that has real area and is therefore invisible to
    it.
    """
    seen = set()
    doomed = []
    for face in bm.faces:
        key = tuple(sorted(vert.index for vert in face.verts))
        if key in seen:
            doomed.append(face)
        else:
            seen.add(key)
    if not doomed:
        return 0
    deletable = [f for f in doomed
                 if all(len(e.link_faces) >= 3 for e in f.edges)]
    folds = [f for f in doomed if f not in deletable]
    removed = len(doomed)
    if deletable:
        bmesh.ops.delete(bm, geom=deletable, context="FACES_ONLY")
    if folds:
        # Identity, not ``edge.index``. bmesh does not renumber after a topology change, so
        # every index here can be stale or -1 once the deletions above have run -- and an
        # index-keyed set would then treat unrelated edges as the same one and silently drop
        # all but the first collapse. ``bmesh.ops.delete`` also rejects a list that names one
        # element twice, so the de-duplication has to be real.
        edges = []
        for face in folds:
            if not face.is_valid:
                continue
            candidates = [e for e in face.edges if e.is_valid]
            if not candidates:
                continue
            shortest = min(candidates, key=lambda e: e.calc_length())
            if any(shortest is existing for existing in edges):
                continue
            edges.append(shortest)
        if edges:
            bmesh.ops.collapse(bm, edges=edges, uvs=True)
    return removed


def remove_duplicate_faces(obj: bpy.types.Object, blackbox: BlackBox,
                           stage: str) -> int:
    """``dedupe_faces_bm`` on an object, recorded in the black box."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    try:
        removed = dedupe_faces_bm(bm)
        if removed:
            bm.to_mesh(obj.data)
            obj.data.update()
    finally:
        bm.free()
    blackbox.record("dedupe_faces:" + stage,
                    triangle_count=mesh_ops.triangle_count(obj.data),
                    vertex_count=len(obj.data.vertices),
                    warning="" if not removed else
                    "removed {n} duplicate faces".format(n=removed))
    return removed


def duplicate_face_count(mesh: bpy.types.Mesh) -> int:
    """Faces sharing a vertex set with an earlier face. Zero is the only passing value."""
    seen = set()
    duplicates = 0
    for polygon in mesh.polygons:
        key = tuple(sorted(polygon.vertices))
        if key in seen:
            duplicates += 1
        else:
            seen.add(key)
    return duplicates


def stale_smooth_census(obj: bpy.types.Object, threshold_deg: float) -> dict:
    """Edges above the split threshold that are still flagged SMOOTH, plus the widest fan.

    A probe that fails loudly, for a stage that used to fail silently. Decimation carries
    the previous level's sharp-edge flags forward unchanged, so a far LOD can be shaded for
    geometry that no longer exists: measured 304 such edges at LOD1 and 98 at LOD2 on the
    boulder before the per-level basis was re-derived, the widest at 178-180 degrees.

    Two separate things go wrong when this number is above zero, and neither raises:
      - ``3DMODEL_GEOLOGY_ROCKS.md`` section 4 is violated outright. Every one of those
        edges is a fracture plane above 45 degrees being smoothed into a blob.
      - the custom split normals become unencodable. Blender stores them per fan in a polar
        frame whose quantisation step scales with the fan's angular spread, so a smooth fan
        spanning 180 degrees loses ~1e-3 on a unit normal -- which is the exporter's whole
        round-trip tolerance, spent on shading that is wrong anyway.
    """
    limit = math.radians(threshold_deg)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    try:
        stale = 0
        widest = 0.0
        sharp = 0
        for edge in bm.edges:
            if not edge.smooth:
                sharp += 1
            if len(edge.link_faces) != 2:
                continue
            angle = edge.calc_face_angle()
            if angle > widest:
                widest = angle
            if angle > limit and edge.smooth:
                stale += 1
        return {"thresholdDeg": round(threshold_deg, 3),
                "edgesAboveThresholdStillSmooth": stale,
                "sharpEdges": sharp,
                "widestDihedralDeg": round(math.degrees(widest), 2)}
    finally:
        bm.free()


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
    channel_readback: dict = field(default_factory=dict)
    channel_area_stats: dict = field(default_factory=dict)
    silhouette: list = field(default_factory=list)
    silhouette_summary: dict = field(default_factory=dict)
    roundtrip_notes: list = field(default_factory=list)
    roundtrip_verified: bool = False
    export_summary: str = ""
    lod_shading: list = field(default_factory=list)
    stale_smooth_edges: dict = field(default_factory=dict)
    lod_silhouette: dict = field(default_factory=dict)
    # Both of these used to be computed and THROWN AWAY, which is why this generator ended
    # up with a second manifest producer. `export_unity.write_manifest` needs the per-LOD
    # `validate.MeshReport` objects and the `ExportResult`, not the flattened strings this
    # file kept, so keeping only the strings made the shared producer look unusable.
    mesh_reports: list = field(default_factory=list)
    export_result: object = None
    bedding_erosion: dict = field(default_factory=dict)
    grain_band: dict = field(default_factory=dict)


def generate_variant(*, seed: int, quality: float, size: SizeClass, process: str,
                     package_dir: str, proof_dir: str, want_preview: bool,
                     want_fbx: bool, preview_resolution: int,
                     debug_stage: str = "") -> VariantResult:
    """Full stage order from ``PROCEDURAL_ASSET_PIPELINE.md`` "Generation Order".

    TWO destinations, not one, and the split is not cosmetic. ``package_dir`` holds the
    FBX and its sibling manifest and lives under ``Assets``, so Unity imports it.
    ``proof_dir`` holds contact sheets, silhouette masks and channel tiles and lives
    under gitignored ``Docs/AgentLogs``, because Unity has no business importing a
    diagnostic picture as a texture with its own ``.meta``, GUID and VRAM cost. One
    directory for both put 27 PNGs into the asset database for every 2 package files.
    """
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
    bm, _grain, _pit, grain_report = build_body(
        strata, frame, density, size, rng, q, process, blackbox)
    result.grain_band = grain_report

    # --debug-stage: stop after a named stage and render the raw result.
    # The strata measure 0.17-1.12 m of radius step in the PARAMETERS yet do not appear in
    # the silhouette. Three rounds of guessing which stage flattens them was already spent;
    # this bisects it instead.
    if debug_stage == "lattice":
        return _debug_render(bm, name, size, proof_dir, preview_resolution,
                             "lattice", result)

    # Stage 4: family topology rules. Summit truncation FIRST, because it removes the
    # domed apex and the shear cuts should place their facets on the mass that survives --
    # cutting a corner and then deleting it with the summit plane wastes the bite.
    volume_before_cuts = abs(bm.calc_volume(signed=True))
    summit = truncate_summit(bm, frame, strata, size, rng, q, blackbox)
    close_open_boundaries(bm, blackbox, "post_summit")
    fractures = cut_fractures(
        bm, frame, strata, size, rng, q, process, blackbox,
        volume_already_spent=summit.volume_spent if summit is not None else 0.0,
        volume_reference=volume_before_cuts)
    if summit is not None:
        fractures.append(summit)
    close_open_boundaries(bm, blackbox, "post_fracture")
    # Re-imprint the bedding onto every cut face. Without this a planar cut is the one
    # stage that DESTROYS strata, which is what made the previous author shrink the cuts
    # until they were invisible; with it, a cut face carries the bed contacts as steps and
    # the two features stop competing.
    imprint_bedding_on_cuts(bm, frame, strata, size, blackbox)
    # Bedding relief arrives HERE, after the cuts, and that ordering is deliberate. Running it
    # on the raw lattice would let the summit and shear planes slice the relief back off;
    # running it after means a fracture facet -- which is bedding-perpendicular wherever the
    # cut is steep -- receives the bed steps for free, which is the effect
    # `imprint_bedding_on_cuts` was hand-rolling for the cut faces alone.
    result.bedding_erosion = erode_bedding_planes(bm, frame, strata, size, q, blackbox)
    # The separate facet-roughening pass that used to run here is DELETED, not disabled:
    # `_plane_clamp` applies the conchoidal ripple during the projection itself, so a second
    # pass would double the amplitude and start eating the flatness that is the entire point of
    # a fracture facet.
    #
    # Every clamped facet still feathers toward zero thickness where the plane grazes the
    # surface. Those wafer wedges are above the degenerate-area epsilon and below any usable
    # thickness, and they showed as 1-2 px needles in the iteration-1 and iteration-2
    # silhouettes.
    collapse_thin_wedges(bm, blackbox, "post_fracture")
    if debug_stage == "fracture":
        return _debug_render(bm, name, size, proof_dir, preview_resolution,
                             "fracture", result)
    partings = carve_partings(bm, frame, strata, size, rng, q, blackbox)
    if debug_stage == "parting":
        return _debug_render(bm, name, size, proof_dir, preview_resolution,
                             "parting", result)
    veins = raise_mineral_seams(bm, frame, strata, size, rng, q, blackbox)
    vugs = punch_vugs(bm, size, rng, q, process, blackbox)
    mesh_ops.weld_and_clean(bm, blackbox=blackbox)
    open_edges = close_open_boundaries(bm, blackbox, "post_detail")
    if debug_stage == "vug":
        return _debug_render(bm, name, size, proof_dir, preview_resolution,
                             "vug", result)
    chips = chip_edges(bm, size, rng, q, process, blackbox)
    if debug_stage == "chip":
        return _debug_render(bm, name, size, proof_dir, preview_resolution,
                             "chip", result)
    # COLLAPSE the chip slivers BEFORE the cleaner gets to delete them. Measured on the basalt
    # outcrop, which uses a 0.72x narrower nominal chip on a fine lattice: `weld_and_clean`
    # reported "deleted 448 zero-area faces" at this point, and deleting 448 faces opens 448
    # worth of rim. `close_open_boundaries` then froze at 40 unclosable boundary edges through
    # six repair passes, because the rims left by scattered deletions are pinched, non-simple
    # loops that `holes_fill` refuses outright.
    #
    # `collapse_slivers` exists precisely for this and its own docstring says so -- "Deleting a
    # degenerate face, which is what a cleaner does, opens a hole" -- but it was running AFTER
    # the weld, so the holes already existed by the time it could have prevented them.
    # Collapsing merges the sliver's two nearest vertices and the surrounding fan stays closed.
    collapse_slivers(bm, blackbox, "post_chip_pre_weld")
    mesh_ops.weld_and_clean(bm, blackbox=blackbox)
    # ...and that cleanup DELETES those slivers, which opens fresh holes: measured 3
    # boundary edges surviving to LOD0 after the chip pass. Closure has to be the last
    # topology operation, not an earlier one.
    open_edges = close_open_boundaries(bm, blackbox, "post_chip")
    # Triangulate once, globally. Bevel corner fans and holes_fill leave n-gons, and
    # Blender aborts tangent-space computation on anything that is not a tri or quad
    # ("Tangent space can only be computed for tris/quads") which shows up as
    # tangent length 0.0 in the validator. Unity triangulates on import anyway, so this
    # makes the authored topology identical to what the engine receives.
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    collapse_slivers(bm, blackbox, "post_triangulate")
    # `collapse_thin_wedges` deliberately does NOT run here, and this is a measured deletion
    # rather than an omission. It ran here once and collapsed **2590** faces, because after the
    # chip pass the thinnest triangles in the mesh are the CHAMFER STRIPS -- a 2.4 mm chip with
    # bevel segments has triangles well under the 0.6 mm inradius floor. It ate the bevels, and
    # deleting them opened 43 boundary edges of which 3 survived to LOD0 and failed the
    # closed-shell gate.
    #
    # This is the same defect class as a merge distance set above the thinnest feature, which
    # the forge rule file names as the trap that erased the strata: a cleanup threshold below an
    # AUTHORED feature deletes the feature. The wedges this pass exists to remove are created by
    # the plane clamps, so the correct and only place for it is `post_fracture`, where it
    # collapsed exactly one face -- surgical, which is what a cleanup should look like.
    # Closure must still be the last topology operation.
    open_edges = close_open_boundaries(bm, blackbox, "post_triangulate")

    # Force OUTWARD winding by measurement, not by trusting recalc_face_normals.
    # Measured: after the cut/fill/inset stages this body came out at signed volume
    # -9.397 m3, i.e. globally inside-out, even though recalc_face_normals had run and
    # every manifold/winding/island check passed. The consequence was invisible in the
    # geometry and catastrophic downstream -- the Cycles AO bake sampled the interior and
    # returned mean 0.0058, so the whole B channel was garbage while the sphere/cube
    # control baked correctly. recalc_face_normals guarantees CONSISTENT normals; it does
    # not guarantee the sign a closed solid needs.
    volume = bm.calc_volume(signed=True)
    if volume < 0.0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
        blackbox.record("flip_winding", triangle_count=len(bm.faces),
                        warning="signed volume was {v:.5f} m3; faces reversed".format(
                            v=volume))
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
        "bedRadiusScales": [round(b.radius_scale, 4) for b in strata.beds],
        "bedHardness": [round(b.hardness, 3) for b in strata.beds],
        "bedThicknessM": [round(b.thickness, 4) for b in strata.beds],
        "bedRadiusStepM": [round(abs(strata.beds[i].radius_scale
                                     - strata.beds[i - 1].radius_scale)
                                 * size.radius_m, 4)
                           for i in range(1, len(strata.beds))],
        "beddingDipDeg": round(frame.dip_deg, 3),
        "beddingAzimuthDeg": round(frame.dip_azimuth_deg, 3),
        "landmarkBed": strata.landmark_bed,
        "jointSet": strata.joints.as_dict() if strata.joints is not None else {},
        "summitTruncated": any(p.kind == "summit_truncation" for p in fractures),
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
                        n=census.components, f=census.irreducible_floor))

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
    # law.smooth_angle_for(GEOLOGIC) is 46 degrees, not the 32-degree hard-surface
    # default. At 32 nearly every bed edge and weathered facet classified as a hard break,
    # so the shading faceted the whole mass and the ledges had no crisper read than the
    # noise around them. 46 sits above the weathered-mass angles and below the strata-step
    # and fracture-plane angles, which is exactly the discrimination the bible asks for:
    # "Split normals at sharp fracture edges above 45 degrees" while not smoothing a
    # chipped plane into a blob.
    result.shading = mesh_ops.apply_shading_basis(
        obj, smooth_angle_deg=law.smooth_angle_for(law.SurfaceClass.GEOLOGIC),
        weighted=True, keep_sharp=True, blackbox=blackbox)

    # Stage 5: material IDs + UVs.
    build_materials(obj)
    result.uv = build_uvs(obj, frame, size, q, blackbox)

    # Stage 6: bake AO into a scratch attribute FIRST -- bpy.ops.object.bake writes ALL
    # channels of its target, so composing R/G/A before the bake destroys them.
    # AO ray length is a CAVITY-SCALE choice, not an asset-scale one.
    # `3DMODEL_GEOLOGY_ROCKS.md` section 4 asks channel B for "cavity darkness", and a ray
    # length much longer than the cavity measures sky visibility instead: a 5 cm parting
    # groove blocks only a small solid angle of a 25 cm hemisphere, so it barely darkens.
    # Measured on the iteration-3 bake at 0.247 m: AO mean 0.880 with the grooves and vugs
    # rendering as faint grey rather than as dark. The features that must read are the
    # 3-6 cm parting grooves, the 2-5 cm vugs and the bed overhangs, so the ray length is
    # sized to those and only loosely to the body.
    ao_distance = min(0.35, max(0.08, 0.045 * size.longest_extent_m))
    ao_samples = int(round(24 + 104 * q))
    # The REAL AO ray bound. `scene.render.bake.distance` does not exist on 4.5 and
    # `max_ray_distance` was measured to change AO statistics not at all (its RNA scope is
    # selected-to-active cage matching). `world.light_settings.distance` is the knob, and
    # nothing in h8forge sets it -- so unbounded rays turn local cavity contrast into a
    # global sky-occlusion term. Set here until the core owns it.
    world = bpy.context.scene.world
    if world is None:
        world = bpy.data.worlds.new("H8_RockWorld")
        bpy.context.scene.world = world
    world.light_settings.distance = ao_distance
    # Last chance before the bake: an inverted shell makes AO sample the interior and
    # return near-zero everywhere, which no count-based check can detect.
    ensure_outward_winding(obj, blackbox, "pre_ao_bake")
    result.ao = vertexcolor.bake_ambient_occlusion(
        obj, samples=ao_samples, distance=ao_distance, blackbox=blackbox)
    ao_values = vertexcolor.consume_baked_ao(obj)

    waterline = -1e9
    if process == "sedimentary" and size.name != "boulder":
        waterline = float(rng.uniform(0.28, 0.62)) * size.height_m
    fields = ChannelFields(frame, strata, size, fractures, veins, partings, waterline)
    result.channels = fields.compose(obj, ao_values)
    vertexcolor.remove_scratch_attributes(obj.data)

    # Stage 8: LOD chain.
    #
    # preserve_seams=FALSE FOR GEOLOGY, and this is the measured fix for the FBX round
    # trip, not a shortcut. Everything below is a number off this generator.
    #
    # WHAT THE SPLIT DOES HERE. `mesh_ops._split_uv_seams` converts every UV seam, every
    # MATERIAL border and every SHARP edge into a mesh boundary so Decimate/COLLAPSE will
    # not collapse across it. LOD0 is a closed manifold solid with 811 sharp edges, a
    # smart-project unwrap and three material slots, so the split cuts the shell into
    # ribbons. Measured on the boulder, seed 1713: LOD0 components 1 / boundary 0 /
    # non-manifold 0 becomes LOD1 boundary 12-19 with 2-3 non-manifold, and LOD2
    # **7-8 components, 137-156 boundary edges, 5-9 non-manifold edges**. The decimator
    # then moves the duplicated seam vertices apart, so `_weld_coincident` -- which only
    # rejoins vertices that are still coincident -- can no longer put the shell back
    # together. `3dmodel.md` section 7 opens that same sentence with "Decimation must
    # preserve BOUNDARY EDGES": a solid with zero boundary edges decimated into one with
    # 147 has violated the requirement the split exists to satisfy.
    #
    # WHY IT BREAKS THE NORMALS, measured rather than reasoned. Blender encodes custom
    # split normals per fan in a polar (alpha, beta) frame around the fan's own averaged
    # normal, and the quantisation step scales with the fan's angular spread. A vertex on
    # an open rim has an open fan spanning the whole gap, so the step is coarse there.
    # Isolated with no FBX involved at all -- re-applying a mesh's OWN corner normals back
    # onto itself with `normals_split_custom_set` -- the loss was LOD0 4.7e-4, LOD1 4.4e-4,
    # LOD2 **9.4e-4** against the exporter's 1.0e-3 tolerance, and the worst loop was at a
    # boundary vertex with a 179.8-degree fold in its fan every single time. So the FBX was
    # never the problem, the tolerance is not being misapplied to a healthy mesh, and LOD2
    # was sitting inside 6% of the ceiling by construction. Which side of it a given run
    # landed on was decided by decimation noise: three identical invocations of the same
    # seed measured 5.9e-5, 3.4e-4 and 1.816e-3 -- the third aborted.
    #
    # WHAT IS GIVEN UP, honestly. UV0 precision and material-border precision at LOD1/LOD2.
    # Both were already nominal rather than real: `tighten_to_target(allow_weld=True)` welds
    # the split seam vertices back at exactly these levels, so the preservation was undone a
    # few lines later, and the manifest's "preservesUvSeams: true" was describing an
    # intention. `3DMODEL_GEOLOGY_ROCKS.md` section 5 makes triplanar object-space
    # projection the primary material route for irregular geology with UV0 as the
    # "decal/manifest coordinates or a fallback unwrap", and section 7 accepts a "proxy
    # shell" at the coarse level, so far-LOD UV0 precision is explicitly secondary. The
    # authored seams that a unique bake would use live on LOD0, which is never decimated.
    #
    # Retrying the split-and-weld route with different distances would be the same-failure
    # escalation `AGENTS.md` forbids. Removing the constraint that shatters the shell is the
    # strategy change it demands.
    lods = mesh_ops.build_lod_chain(obj, family=law.Family.GEOLOGY, name=name,
                                    quality_weight=q, levels=3, preserve_seams=False,
                                    blackbox=blackbox)
    for level in lods:
        target = int(size.budget(level.index))
        if level.index > 0:
            # CLEAN FIRST, TIGHTEN LAST. The reverse order was a real budget failure, not a
            # style preference: measured on seed 1713, LOD1 came out of `tighten_to_target`
            # at or under 3000 and was then reported at 3042, LOD2 at 715 against 600. The
            # cleanup runs `weld_and_clean`, whose `fill_boundary_loops` defaults to True, so
            # it ADDS fill triangles after the decimator has finished and nothing measures
            # the result again. Decimation has to be the last operation that touches the
            # count, or the count in the manifest is not the count that was decimated.
            #
            # No hole-closing on the far LODs: 3dmodel.md section 7 accepts "coarse
            # silhouette or proxy shell" at LOD2, so chasing manifoldness there would push a
            # decimated proxy back over a hard budget. LOD0 remains the strict manifold solid.
            clean_object(level.obj, blackbox, "post_lod{i}".format(i=level.index),
                         merge_distance=2e-3, close=False)
            if mesh_ops.triangle_count(level.obj.data) > target:
                tighten_to_target(level.obj, target, blackbox,
                                  "lod{i}_size_row".format(i=level.index))
            # Duplicate faces, LAST thing after the decimation that can create them.
            # `mesh_ops._weld_coincident` used to do this, but it only runs when seams were
            # split, so turning seam splitting off for geology took the duplicate-face
            # removal with it -- and the cliff chunk then failed the round trip at all three
            # levels. Removing a face cannot raise the triangle count, so this cannot
            # breach the budget that was just met.
            remove_duplicate_faces(level.obj, blackbox,
                                   "lod{i}".format(i=level.index))
            # TRIANGULATE THE FAR LODS TOO. This is triangle-count-NEUTRAL, so the budget
            # reason the LOD0 block gives for excluding LOD1/LOD2 does not actually apply to
            # triangulation: `mesh_ops.triangle_count` is `len(mesh.loop_triangles)`, which
            # already counts an n-gon as its n-2 triangles. That comment's real subject is
            # `weld_and_clean`'s boundary FILL, which does add geometry.
            #
            # Leaving them untriangulated is a live round-trip hazard rather than a cosmetic
            # one, because the exporter writes triangles (`use_triangles=True`) while the
            # source keeps the n-gon, so the verifier compares 20158 source corners against
            # 20160 reimported ones and ABORTS -- which deletes the package. Measured exactly
            # that on the cliff chunk after the bedding change shifted the decimation:
            # "LOD1: colour element count 80632 -> 80640; corner normal count 20158 -> 20160",
            # i.e. one surviving quad out of 6718 triangles. The previous build passed only
            # because the collapse happened to leave no n-gon behind, so this was always
            # luck, not correctness.
            level_bm = bmesh.new()
            level_bm.from_mesh(level.obj.data)
            ngons = [f for f in level_bm.faces if len(f.verts) > 3]
            if ngons:
                bmesh.ops.triangulate(level_bm, faces=ngons)
                level_bm.to_mesh(level.obj.data)
                level.obj.data.update()
                blackbox.record("triangulate_lod{i}".format(i=level.index),
                                vertex_count=len(level.obj.data.vertices),
                                triangle_count=mesh_ops.triangle_count(level.obj.data),
                                warning="{n} n-gon(s) split so the authored topology "
                                        "matches the exported triangles".format(
                                            n=len(ngons)))
            level_bm.free()
        # Every LOD passes through weld_and_clean/_split_uv_seams, each of which calls
        # recalc_face_normals, so each level needs its own winding check.
        ensure_outward_winding(level.obj, blackbox,
                               "lod{i}".format(i=level.index))
        if level.index > 0:
            # RE-DERIVE THE SHADING BASIS FROM THIS LEVEL'S OWN GEOMETRY.
            #
            # Quadric Edge Collapse has no normal term. It drags LOD0's per-loop custom
            # normals and LOD0's sharp-edge flags through the collapse unchanged, so a far
            # LOD ends up shaded for a mesh that no longer exists. Measured on the boulder:
            # after decimation **304 edges at LOD1 and 98 at LOD2 had a dihedral angle above
            # the 46-degree split threshold while still flagged SMOOTH**, with the widest at
            # 178-180 degrees. `3DMODEL_GEOLOGY_ROCKS.md` section 4 is explicit -- "Split
            # normals at sharp fracture edges above 45 degrees. Do not smooth a chipped
            # plane into a soft blob" -- and every one of those 402 edges was a chipped
            # plane smoothed into a blob. That is a visual defect at LOD1, which is a
            # mid-distance level, not just a round-trip statistic.
            #
            # It is also what makes the corner normals unencodable. A smooth fan spanning
            # 180 degrees gives the clnor polar encoding a range that wide to quantise
            # into a short, and re-deriving the sharp set narrows every fan back down.
            # Measured re-encode loss: LOD1 4.447e-4 -> 3.302e-4, LOD2 4.043e-4 -> 2.440e-4.
            #
            # `3dmodel.md` section 7's "Decimation must preserve ... hard normals" is
            # satisfied by re-deriving them at the SAME threshold on the decimated faces,
            # which is strictly stronger than carrying flags that measurably no longer
            # describe the surface. Runs after `ensure_outward_winding` on purpose: that
            # function reverses faces when the signed volume is negative, which inverts
            # every corner normal, so a basis applied before it would be thrown away.
            result.lod_shading.append(mesh_ops.apply_shading_basis(
                level.obj, smooth_angle_deg=law.smooth_angle_for(
                    law.SurfaceClass.GEOLOGIC),
                weighted=True, keep_sharp=True, blackbox=blackbox))
        # Per-LOD census, so a far-LOD validator failure reports a CAUSE instead of a symptom.
        # `h8forge.validate` reports `inconsistent_winding` on LOD1/LOD2 and
        # `recalc_face_normals` cannot fix it, which means the geometry is locally
        # non-orientable -- a duplicate face or a fin, both of which show up as an edge with
        # more than two faces. Without this census the failure is an unexplained triangle
        # index; the forge rule file's standing lesson is that reasoning from a plausible
        # mechanism instead of reading the number survived two commits once already.
        census_lod = mesh_ops.topology_report(level.obj)
        shading_census = stale_smooth_census(
            level.obj, law.smooth_angle_for(law.SurfaceClass.GEOLOGIC))
        result.stale_smooth_edges["LOD{i}".format(i=level.index)] = shading_census
        result.lods.append({
            "index": level.index,
            "object": level.obj.name,
            "triangles": mesh_ops.triangle_count(level.obj.data),
            "components": census_lod.components,
            "boundaryEdges": census_lod.boundary_edges,
            "nonManifoldEdges": census_lod.nonmanifold_edges,
            "duplicateFaces": duplicate_face_count(level.obj.data),
            "shadingBasis": shading_census,
            "lawFamilyBudget": law.LOD_BUDGETS[law.Family.GEOLOGY].limit(level.index),
            "geologySizeRowBudget": law.geology_budget_for(size.law_key).limit(level.index),
            "effectiveBudget": target,
        })

    # LOD0 leaves `build_lod_chain` with n-gons, because `weld_and_clean` fills boundary loops
    # and `holes_fill` emits an n-gon. An n-gon is not a defect by itself, but its loop-triangle
    # split can be, and the exported FBX plus Unity's importer both see triangles. Triangulating
    # here makes the authored topology identical to what the engine receives, and the sliver
    # collapse then has something to measure. LOD1/LOD2 are deliberately excluded: they are at
    # their budget ceiling and added fill geometry would push them over.
    #
    # The three cleanups ALTERNATE to a fixed point rather than running once each, and that is a
    # measured requirement, not defensive coding. Collapsing a sliver can strand an open rim;
    # filling that rim can produce another sliver; welding a rim can leave an edge with three
    # faces. Running each once left 3 boundary edges on the boulder, 9 plus 4 non-manifold on the
    # cliff chunk and 36 on the basalt outcrop -- three of four matrix configurations aborting on
    # the closed-shell gate while the outcrop that had been iterated on passed.
    #
    # This is NOT the same-failure escalation `AGENTS.md` forbids: that rule bans retrying an
    # operation under unchanged constraints. Here each pass runs against the OUTPUT of the other
    # two, so the state genuinely changes between passes, and the loop exits on a measured
    # condition rather than on a pass count.
    lod0_bm = bmesh.new()
    lod0_bm.from_mesh(lods[0].obj.data)
    bmesh.ops.recalc_face_normals(lod0_bm, faces=lod0_bm.faces[:])
    for attempt in range(6):
        # TRIANGULATION IS PART OF THE FIXED POINT, not a one-shot before it. It used to run
        # once above this loop, and then `weld_and_clean`'s `holes_fill` put n-gons straight
        # back -- so the mesh that got measured, validated and manifested was NOT the mesh
        # the FBX carried. Measured on the cliff chunk: LOD0 ended with three QUADS, and the
        # round trip reported 16208 -> 16205 triangles with the polygon count unchanged at
        # 16205 and maxSides 4 -> 3, i.e. each quad came back as a triangle: 3 triangles and
        # exactly 3 loops lost (colour elements 194472 -> 194460). The exporter's own
        # discriminator called it -- "an n-gon lost a side, so this is triangulation or n-gon
        # support, NOT lost geometry" -- and it was right.
        #
        # Triangulating inside the loop also lets `collapse_slivers` see what the
        # triangulation exposes: a quad with one near-collinear corner has a healthy area but
        # splits into one healthy triangle and one sliver, which is the degenerate the
        # exporter drops.
        ngons = [f for f in lod0_bm.faces if len(f.verts) > 3]
        if ngons:
            bmesh.ops.triangulate(lod0_bm, faces=ngons)
        # Tiny merge distance: this is a topology repair, not a decimation, and the authored
        # features here are millimetre-scale chamfers.
        mesh_ops.weld_and_clean(lod0_bm, merge_distance=1e-5, blackbox=blackbox)
        slivers_left = collapse_slivers(lod0_bm, blackbox,
                                        "lod0_final_{a}".format(a=attempt))
        boundary_left = close_open_boundaries(lod0_bm, blackbox,
                                             "lod0_final_{a}".format(a=attempt))
        nonmanifold_left = sum(1 for e in lod0_bm.edges if len(e.link_faces) > 2)
        # FOURTH symptom of the same cycle, and the one that was missing. `holes_fill` can
        # bridge a rim that already had a face and `bmesh.ops.collapse` can pull two
        # triangles onto one triple, so the sliver/rim/non-manifold fixed point could
        # converge while leaving a duplicate face behind -- invisible to all three of the
        # conditions below and fatal to the FBX round trip. Measured on the cliff chunk:
        # LOD0 lost 4 triangles and 6 corner normals on re-import with every one of those
        # three counts already at zero.
        duplicates_left = dedupe_faces_bm(lod0_bm)
        ngons_left = sum(1 for f in lod0_bm.faces if len(f.verts) > 3)
        if (slivers_left == 0 and boundary_left == 0 and nonmanifold_left == 0
                and duplicates_left == 0 and ngons_left == 0):
            break
    # TERMINAL PASS, and it deliberately does NOT fill. Hole filling is what produces both
    # the n-gons and the flaps, so as long as it is the last thing to run the loop can exit
    # on its attempt cap with three quads still present -- which is exactly what the cliff
    # chunk did, and the export then triangulated them into duplicate triangles that the FBX
    # merged away. Measured before this pass: LOD0 sides={3: 16202, 4: 3} with 16206 distinct
    # loop-triangle vertex sets out of 16208.
    #
    # Order matters. Triangulate first, because a flap is only visible as a duplicate once
    # the quad is split. Collapse slivers next, because the split can expose one. Repair
    # folds last, so nothing after it can create another.
    terminal_ngons = [f for f in lod0_bm.faces if len(f.verts) > 3]
    if terminal_ngons:
        bmesh.ops.triangulate(lod0_bm, faces=terminal_ngons)
    collapse_slivers(lod0_bm, blackbox, "lod0_terminal")
    terminal_folds = dedupe_faces_bm(lod0_bm)
    bmesh.ops.recalc_face_normals(lod0_bm, faces=lod0_bm.faces[:])
    blackbox.record("lod0_terminal", triangle_count=len(lod0_bm.faces),
                    vertex_count=len(lod0_bm.verts),
                    warning="triangulated {n} n-gons, repaired {f} coincident "
                            "faces".format(n=len(terminal_ngons), f=terminal_folds))
    lod0_bm.to_mesh(lods[0].obj.data)
    lods[0].obj.data.update()
    lod0_bm.free()
    ensure_outward_winding(lods[0].obj, blackbox, "lod0_final")
    # LOD0's SHADING BASIS IS RE-DERIVED HERE TOO, for the same reason the far LODs get one:
    # it must describe the mesh that is exported, and the repair loop above runs after the
    # stage-4 basis. Caught by the new gate rather than by inspection -- 2 edges at 179.98
    # degrees were left flagged smooth on the cliff chunk, i.e. two folds shaded as if they
    # were continuous surface. The stage-4 pass still has to happen where it is, because the
    # Cycles AO bake and the channel composition read the shading; this is the second half of
    # the same requirement, applied to the final topology.
    result.shading = mesh_ops.apply_shading_basis(
        lods[0].obj, smooth_angle_deg=law.smooth_angle_for(law.SurfaceClass.GEOLOGIC),
        weighted=True, keep_sharp=True, blackbox=blackbox)
    # REFRESH THE WHOLE LOD0 ROW, not just the triangle count. The per-level census above
    # ran before this repair loop, so every other field in it -- components, boundary edges,
    # non-manifold edges, duplicate faces, shading basis -- described the mesh as it was
    # before the fixed point converged. One refreshed field beside five stale ones is worse
    # than none, because the row reads as current.
    lod0_census = mesh_ops.topology_report(lods[0].obj)
    lod0_shading = stale_smooth_census(
        lods[0].obj, law.smooth_angle_for(law.SurfaceClass.GEOLOGIC))
    result.stale_smooth_edges["LOD0"] = lod0_shading
    result.lods[0].update({
        "triangles": mesh_ops.triangle_count(lods[0].obj.data),
        "components": lod0_census.components,
        "boundaryEdges": lod0_census.boundary_edges,
        "nonManifoldEdges": lod0_census.nonmanifold_edges,
        "duplicateFaces": duplicate_face_count(lods[0].obj.data),
        "shadingBasis": lod0_shading,
    })

    # Topology census AFTER the LOD chain, measured on the LOD0 object that will actually be
    # exported. It used to run before `build_lod_chain`, and that is a stale measurement: the
    # chain rebuilds LOD0's datablock through `_split_uv_seams` and `weld_and_clean`, so my own
    # "zero degenerate faces" gate was passing on a mesh that no longer existed while
    # `h8forge.validate` -- which reads the real one -- reported a degenerate triangle at
    # 8.1e-08 against the 1e-07 epsilon. Two checks over two different states of the same asset,
    # one of them reporting on geometry that was never saved.
    result.topology = inspect_topology(lods[0].obj.data)

    # Stage 9: collision proxy, independent of the visual LODs. The prehull duplicate that
    # used to sit here worked around the concave-input crash in
    # mesh_ops._convex_hull_in_place; that is fixed in the core with a dict.fromkeys
    # dedupe, so the workaround is deleted and the core owns the whole hull route again.
    collider = mesh_ops.make_convex_collider(lods[0].obj, family=law.Family.GEOLOGY,
                                             name=name, blackbox=blackbox)
    result.collider_triangles = collider.triangles
    result.collider_within_budget = collider.within_budget
    result.collider_kind = collider.kind

    # Read the authored channels back off the mesh that will actually be rendered and
    # exported, not off the lists that were handed to the writer.
    result.channel_readback = read_back_channels(lods[0].obj.data)
    # Area-weighted stats, comparable with the rendered tiles. A rendered tile averages
    # over PIXELS and a naive readback averages over LOOPS, so for a non-uniform field the
    # two MEANS legitimately differ -- min and max are weighting-independent and are the
    # values to assert on.
    try:
        result.channel_area_stats = vertexcolor.channel_stats(lods[0].obj)
    except Exception as error:                                   # pragma: no cover
        result.channel_area_stats = {"error": str(error)}

    # The DECISIVE proof shot for this family, rendered BEFORE the gates on purpose so a
    # gate can read it. Every other preview mode is LIT, and a lit render lets shading imply
    # facets the outline does not have -- the same way a normal map fakes relief -- so it
    # cannot answer `3DMODEL_GEOLOGY_ROCKS.md` section 9's "no geological process is visible
    # in silhouette". This is the alpha coverage mask plus outline statistics, and it runs on
    # the LOD0 object that will actually be exported.
    #
    # Calibrated in-process against controls (`silhouette_probe --controls`):
    #   smooth icosphere        turn concentration 0.094
    #   displaced icosphere     0.137   <- the procedural-rock potato
    #   random convex polytope  0.789   <- pure flat facets and sharp arrises
    if want_preview:
        silhouette = silhouette_probe.render_silhouette(
            lods[0].obj, name=name, output_dir=proof_dir,
            resolution=preview_resolution,
            views=("front", "side", "three_quarter", "low"))
        result.sheets["silhouette"] = silhouette.sheet_path
        result.silhouette = [m.as_dict() for m in silhouette.metrics]
        result.silhouette_summary = {
            "meanTurnTop10Fraction": round(silhouette.mean_top10, 4),
            "meanCornerCount": round(silhouette.mean_corners, 2),
            "meanConvexity": round(silhouette.mean_convexity, 4),
            "controlSphereTop10": SILHOUETTE_CONTROL_SPHERE,
            "controlPotatoTop10": SILHOUETTE_CONTROL_POTATO,
            "controlPolytopeTop10": SILHOUETTE_CONTROL_POLYTOPE,
            "potatoFloor": SILHOUETTE_POTATO_FLOOR,
            "targetFloor": SILHOUETTE_TARGET_FLOOR,
        }

        # THE FAR LODS GET THE SAME INSTRUMENT, because section 9's rejection gate
        # "LOD1/LOD2 destroys ore/vent gameplay readability" and section 7's forbidden
        # "Smoothing away all fracture planes" are about the DECIMATED levels, and nothing
        # here had ever measured or looked at them -- every proof render in this generator
        # was LOD0. That was tolerable while the far LODs were an afterthought and is not
        # now that the decimation route has changed: the whole point of dropping seam
        # splitting is that the quadric metric, not a boundary lock, keeps the silhouette.
        # Reported as measurements rather than as a pass/fail, because the bible sets no
        # numeric floor for a 236-triangle proxy and inventing one would be a fabricated
        # threshold. The number triages; the image decides.
        for level in lods[1:]:
            far = silhouette_probe.render_silhouette(
                level.obj, name="{n}_LOD{i}".format(n=name, i=level.index),
                output_dir=proof_dir, resolution=preview_resolution,
                views=("front", "side", "three_quarter", "low"))
            result.sheets["silhouette_lod{i}".format(i=level.index)] = far.sheet_path
            result.lod_silhouette["LOD{i}".format(i=level.index)] = {
                "meanTurnTop10Fraction": round(far.mean_top10, 4),
                "meanCornerCount": round(far.mean_corners, 2),
                "meanConvexity": round(far.mean_convexity, 4),
                "retainedFractionOfLod0": round(
                    far.mean_top10 / max(1e-6, silhouette.mean_top10), 4),
            }

    # Stage 11: validation BEFORE save.
    result.gates = hard_gates(result, size)
    if h8validate is not None:
        try:
            # Labelled PER LOD. The flattened list hid which level failed, and
            # "inconsistent_winding on triangle 1735" is unactionable without knowing
            # whether it came from the authored solid or from a decimated proxy -- the two
            # have completely different fixes and only one of them is a defect.
            result.validator_failures = []
            result.mesh_reports = []
            for level in lods:
                report = h8validate.validate_mesh(
                    level.obj.data, family=law.Family.GEOLOGY, lod_index=level.index,
                    surface_class=law.SurfaceClass.GEOLOGIC, blackbox=blackbox,
                    hero=False, triplanar=True)
                result.mesh_reports.append(report)
                result.validator_failures.extend(
                    "LOD{i} {g}: {d}".format(i=level.index, g=f.gate, d=f.detail)
                    for f in h8validate._collect_failures([report]))
            if collider.obj is not None:
                collider_report = h8validate.validate_collider(
                    collider.obj.data, family=law.Family.GEOLOGY, blackbox=blackbox,
                    lod0_mesh=lods[0].obj.data)
                result.validator_failures.extend(
                    "COLLIDER {g}: {d}".format(g=f.gate, d=f.detail)
                    for f in h8validate._collect_failures([collider_report]))
        except Exception as error:                      # pragma: no cover
            result.validator_failures = ["validator raised: " + str(error)]

    blocking = [g for g in result.gates if g.startswith("FAIL")]
    if blocking:
        dump = blackbox.dump("hard gate failure: " + "; ".join(blocking))
        raise GenerationAborted("rock gates failed for " + name, dump, blocking)

    # Stage 12/13: save + proof.
    os.makedirs(package_dir, exist_ok=True)
    if want_fbx:
        result.fbx_path = export_package(lods, collider, package_dir, name,
                                         result, blackbox)
    if want_preview:
        render_proof(lods[0].obj, name, proof_dir, preview_resolution, result)
    # The manifest is a SIBLING of the FBX and cannot move.
    # `HectonFBXPostprocessor.TryResolveForgeManifestPath` derives the manifest path from
    # the imported mesh path, and that lookup is what gates the import carve-out
    # preserving these authored normals. A manifest in the proof directory would be a
    # manifest Unity never finds.
    result.manifest_path = write_manifest(result, size, frame, strata, package_dir)
    return result


def _debug_render(bm: bmesh.types.BMesh, name: str, size: SizeClass, proof_dir: str,
                  resolution: int, stage: str, result: VariantResult) -> VariantResult:
    """Commit a bmesh to a throwaway object and render it flat. Isolation instrument only.

    Deliberately skips UVs, bakes, LODs, colliders, validation and the manifest: the only
    question it answers is what the silhouette looks like at this exact point in the stage
    order, so anything that could itself alter the shape is left out.
    """
    mesh = bpy.data.meshes.new("DEBUG_{n}_{s}".format(n=name, s=stage))
    obj = bpy.data.objects.new(mesh.name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.update()
    bm.to_mesh(mesh)
    bm.free()

    lo, _hi = mesh_ops.local_bounds(obj)
    for vertex in obj.data.vertices:
        vertex.co.z -= lo.z

    # Shade it the way the real pipeline will, or the render answers a different question.
    mesh_ops.apply_shading_basis(
        obj, smooth_angle_deg=law.smooth_angle_for(law.SurfaceClass.GEOLOGIC),
        weighted=True, keep_sharp=True)

    spec = preview.PreviewSpec(
        name="{n}_DEBUG_{s}".format(n=name, s=stage), output_dir=proof_dir,
        resolution=resolution, mode="flat",
        surface_class=law.SurfaceClass.GEOLOGIC,
        views=("three_quarter", "front", "side", "low"))
    sheet = preview.render_contact_sheet(obj, spec)
    result.sheets["debug_" + stage] = sheet.sheet_path

    # The silhouette test belongs in the isolation instrument too, and cheaply: this path
    # skips the AO bake, the unwrap, the LOD chain and validation, so it answers "did this
    # stage change the OUTLINE" in seconds instead of minutes.
    silhouette = silhouette_probe.render_silhouette(
        obj, name="{n}_DEBUG_{s}".format(n=name, s=stage), output_dir=proof_dir,
        resolution=resolution, views=("front", "side", "three_quarter", "low"))
    result.sheets["debug_silhouette_" + stage] = silhouette.sheet_path
    result.silhouette = [m.as_dict() for m in silhouette.metrics]
    result.silhouette_summary = {
        "meanTurnTop10Fraction": round(silhouette.mean_top10, 4),
        "meanCornerCount": round(silhouette.mean_corners, 2),
        "meanConvexity": round(silhouette.mean_convexity, 4),
        "controlSphereTop10": 0.094, "controlPotatoTop10": 0.137,
        "controlPolytopeTop10": 0.789,
    }
    print("[rock] DEBUG silhouette {s}: ".format(s=stage)
          + json.dumps(result.silhouette_summary))
    for entry in result.silhouette:
        print("[rock] DEBUG SIL {v}: top10={t} corners={c} convexity={x} fuzz={f}".format(
            v=entry["view"], t=entry["turnTop10Fraction"], c=entry["cornerCount"],
            x=entry["convexity"], f=entry["fuzzFraction"]))
    result.lods.append({"index": 0, "object": obj.name,
                        "triangles": mesh_ops.triangle_count(obj.data),
                        "lawFamilyBudget": law.LOD_BUDGETS[law.Family.GEOLOGY].limit(0),
                        "geologySizeRowBudget": law.geology_budget_for(size.law_key).limit(0),
                        "effectiveBudget": size.budget(0)})
    print("[rock] DEBUG stage={s} tris={t} sheet={p}".format(
        s=stage, t=mesh_ops.triangle_count(obj.data), p=sheet.sheet_path))
    return result


def collapse_slivers(bm: bmesh.types.BMesh, blackbox: BlackBox, stage: str,
                     passes: int = 4) -> int:
    """Remove sub-epsilon triangles by COLLAPSING them. Returns how many remain.

    This is the fix for the abort class that killed 7 of 18 matrix configs. Deleting a
    degenerate face -- which is what a cleaner does -- opens a hole; filling that hole
    produces another degenerate face; collapsing THAT rim can strand a non-manifold
    junction. All three symptoms (degenerate faces, boundary edges, non-manifold edges)
    were one cause chasing its own tail.

    Collapsing the sliver's shortest edge removes the triangle without ever creating a
    boundary: the two vertices merge, the zero-area face vanishes with them, and the
    surrounding fan stays closed. ``3dmodel.md`` section 10 demands zero degenerate
    triangles, and this reaches zero without opening the shell.

    The threshold is deliberately 4x ``law.DEGENERATE_TRIANGLE_AREA_EPS``: clearing only to
    exactly the gate value leaves faces a hair above it that the next triangulation or
    decimation pass pushes back under.
    """
    threshold = law.DEGENERATE_TRIANGLE_AREA_EPS * 4.0
    remaining = 0
    for _attempt in range(max(1, passes)):
        slivers = [f for f in bm.faces if f.is_valid and f.calc_area() <= threshold]
        remaining = len(slivers)
        if not slivers:
            break
        targets = []
        seen = set()
        for face in slivers:
            edges = [e for e in face.edges if e.is_valid]
            if not edges:
                continue
            shortest = min(edges, key=lambda e: e.calc_length())
            key = shortest.index
            if key in seen:
                continue
            seen.add(key)
            targets.append(shortest)
        if not targets:
            break
        bmesh.ops.collapse(bm, edges=targets, uvs=True)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    left = sum(1 for f in bm.faces if f.calc_area() <= law.DEGENERATE_TRIANGLE_AREA_EPS)
    if blackbox is not None:
        blackbox.record("collapse_slivers:" + stage, triangle_count=len(bm.faces),
                        vertex_count=len(bm.verts),
                        warning="" if left == 0 else
                        "{n} degenerate faces remain".format(n=left),
                        failure_code="" if left == 0 else "DEGENERATE_SURVIVED")
    return left


def clean_object(obj: bpy.types.Object, blackbox: BlackBox, stage: str,
                 merge_distance: float = 1e-4, close: bool = True) -> dict:
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
    # WELD the micro-features away rather than delete-then-refill them.
    #
    # Two consecutive failures came from the other order: cleaning deletes a sliver, that
    # opens a hole, holes_fill closes the hole with another sliver, and the next clean
    # deletes that -- clean and close chasing each other, with LOD1/LOD2 going over budget
    # from the added fill geometry. AGENTS.md [RULE] Same-failure escalation calls for a
    # different mechanism, not another pass. At the merge distance the two sides of a sliver
    # hole become one vertex, so the hole ceases to exist instead of being patched.
    #
    # Thinnest features to clear: the 20 mm minimum ledge rise, the ~7 mm resolution-capped
    # chip width, and the 4 mm minimum bedding imprint step on a fracture face. The 0.1 mm
    # default is 40x below the smallest of those; the far-LOD call sites pass 2 mm, which is
    # still half the imprint step and is applied only to already-decimated geometry. This is
    # the check the forge rule file demands -- "check your merge distance against your
    # thinnest feature" -- because a merge above the thinnest feature is what DELETED the
    # interpenetrating bed plates and erased the strata in an earlier round.
    # The `merge_distance` ARGUMENT used to be accepted and then ignored -- the call below
    # was hardcoded to 1e-4 while callers passed 2e-3 for the far LODs and reasonably assumed
    # it took effect. A parameter accepted and dropped is the same defect class as a gate that
    # cannot fire, and it is why the far-LOD weld never did what its call site claimed.
    #
    # `fill_boundary_loops` is tied to `close`, because it is the SAME decision expressed
    # in the core's cleaner. It defaults to True there, so a call site that passed
    # `close=False` -- meaning "do not close holes at this level, the budget is a hard
    # ceiling" -- still got holes closed, by the weld, one line before its own closer was
    # skipped. That is how a far LOD gained fill triangles after the decimator had finished
    # and after the last measurement, which is the failure the call site's own comment
    # describes. It also makes the level NON-DETERMINISTIC: the core discovers boundary
    # loops by popping an arbitrary element off a Python set of BMesh edges, whose iteration
    # order is address-derived and therefore different in every process, so which rims get
    # filled and in what order changes run to run.
    stats = mesh_ops.weld_and_clean(bm, merge_distance=merge_distance,
                                    fill_boundary_loops=close, blackbox=blackbox)
    stats["degenerate_left"] = collapse_slivers(bm, blackbox, stage)
    if close:
        stats["boundary_edges_left"] = close_open_boundaries(
            bm, blackbox, "clean:" + stage)
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
    # FBX merges coincident faces on import, so a duplicate pair is silent data loss that
    # only the round trip reports -- and it reports it as an aborted save.
    for level in result.lods:
        duplicates = level.get("duplicateFaces", 0)
        lines.append("{v} LOD{i} zero duplicate faces (got {n})".format(
            v="PASS" if duplicates == 0 else "FAIL", i=level["index"], n=duplicates))
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
    # check happily passes. The width must be a real fraction of the intended nominal,
    # where the nominal is already resolution-bounded.
    nominal = result.nominal_chip_width_m
    biggest = max(widths) if widths else 0.0
    lines.append(("PASS" if nominal > 0.0 and biggest >= nominal * 0.9 else "FAIL")
                 + " widest chip {b:.5f} m is a real chamfer vs nominal {n:.5f} m"
                 .format(b=biggest, n=nominal))
    applied = counts.get("edgesChipped", 0)
    found = max(1, counts.get("hardEdgesFound", 1))
    lines.append(("PASS" if applied >= found * 0.25 else "FAIL")
                 + " chamfer reached {p:.0f} percent of the {f} hard edges".format(
                     p=100.0 * applied / found, f=found))
    if not result.uv.get("uv0", {}).get("unwrapped", False):
        lines.append("FAIL UV0 unwrap failed")
    else:
        lines.append("PASS UV0 fallback unwrap present alongside triplanar route")

    lines.append(("PASS" if topology.get("outwardWinding", False) else "FAIL")
                 + " outward winding, signed volume {v} m3".format(
                     v=topology.get("signedVolumeM3")))

    readback = result.channel_readback
    if not readback.get("present", False):
        lines.append("FAIL packed vcol attribute '{a}' missing from the exported mesh "
                     "(layers present: {l})".format(a=law.VCOL_ATTRIBUTE_NAME,
                                                    l=readback.get("layers")))
    else:
        flat = []
        for key in "RGBA":
            stored = readback.get("stored" + key)
            if stored is not None and (stored[1] - stored[0]) < 0.02:
                flat.append("{k}(min={lo} max={hi})".format(k=key, lo=stored[0],
                                                            hi=stored[1]))
        lines.append(("PASS" if not flat else "FAIL")
                     + " every stored channel varies; flat channels: {f}".format(
                         f=flat or "none"))

    # 3DMODEL_GEOLOGY_ROCKS.md section 9: "No geological process is visible in silhouette."
    # This is that rejection gate, made executable against measured controls rather than
    # against an opinion. It stays silent when previews are disabled instead of failing --
    # a gate that cannot be evaluated must not masquerade as a pass OR as a failure.
    summary = result.silhouette_summary
    if not summary:
        lines.append("SKIP silhouette metric not measured (previews disabled)")
    else:
        measured = float(summary.get("meanTurnTop10Fraction", 0.0))
        corners = float(summary.get("meanCornerCount", 0.0))
        if measured <= SILHOUETTE_POTATO_FLOOR:
            verdict = "FAIL"
        elif measured < SILHOUETTE_TARGET_FLOOR:
            verdict = "WARN"
        else:
            verdict = "PASS"
        lines.append("{v} silhouette turn concentration {m:.4f} (potato control "
                     "{p}, polytope control {q}, floor {f}); mean {c:.1f} arrises "
                     "per view".format(v=verdict, m=measured,
                                       p=SILHOUETTE_CONTROL_POTATO,
                                       q=SILHOUETTE_CONTROL_POLYTOPE,
                                       f=SILHOUETTE_POTATO_FLOOR, c=corners))
        lines.append(("PASS" if corners >= 3.0 else "FAIL")
                     + " at least 3 outline arrises per view on average ({c:.1f})".format(
                         c=corners))

    # Every LOD must be shaded for ITS OWN geometry. Decimation carries the previous
    # level's sharp flags forward, so this is zero only because the basis is re-derived per
    # level; it read 304 at LOD1 and 98 at LOD2 before that, in violation of
    # 3DMODEL_GEOLOGY_ROCKS.md section 4, with nothing raising.
    for key in sorted(result.stale_smooth_edges):
        census = result.stale_smooth_edges[key]
        stale = census["edgesAboveThresholdStillSmooth"]
        lines.append("{v} {k} shading basis matches its own geometry: {n} edges above "
                     "{t} deg still smooth (widest dihedral {w} deg, {s} sharp)".format(
                         v="PASS" if stale == 0 else "FAIL", k=key, n=stale,
                         t=census["thresholdDeg"], w=census["widestDihedralDeg"],
                         s=census["sharpEdges"]))

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

def project_relative(path: str) -> str:
    """Forward-slashed path relative to the repo root, for anything durable.

    ``AGENTS.md`` ``[RULE] Relative Path Requirement`` bans hardcoded absolute developer
    paths, and ``law.forge_package_dir``'s own docstring names this artefact as the reason:
    "never absolute ... a manifest records this path". The manifest was recording
    ``C:\\hades\\Hecton8\\Assets\\...`` for the FBX and for every proof sheet, which leaks a
    developer layout into a file that is meant to be portable evidence. Console output stays
    absolute on purpose -- that one is for a human to paste into a viewer, not to keep.
    """
    if not path:
        return path
    try:
        return os.path.relpath(path, law.project_root()).replace("\\", "/")
    except ValueError:
        # Different drive: no relative path exists. Keep the basename rather than the
        # absolute path, so the artefact still names the file without the developer tree.
        return os.path.basename(path)


def write_manifest(result: VariantResult, size: SizeClass, frame: BeddingFrame,
                   strata: Stratigraphy, package_dir: str) -> str:
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
        "postFractureCensus": result.post_fracture_topology,
        "shadingBasis": {
            "smoothPolygons": result.shading.smooth_polygons if result.shading else 0,
            "sharpEdges": result.shading.sharp_edges if result.shading else 0,
            "weightedNormalsApplied": bool(result.shading and result.shading.weighted_applied),
            "smoothAngleDeg": law.SMOOTH_ANGLE_DEG,
            "bibleRequirement": "3DMODEL_GEOLOGY_ROCKS.md s4: split normals above 45 deg; "
                                "law.SMOOTH_ANGLE_DEG=32 is stricter, so all fracture "
                                "edges split",
        },
        "vertexColorReport": result.channels,
        "vertexColorAreaWeighted": result.channel_area_stats,
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
            "preservesUvSeams": False,
            "preservesSharpNormals": True,
            "preservesMaterialBorders": False,
            "mechanism": "seam splitting is OFF for geology (preserve_seams=False). "
                         "mesh_ops._split_uv_seams turns every UV seam, material border "
                         "and sharp edge into a mesh boundary, and on a rock with 811 "
                         "sharp edges that shattered the shell: measured LOD2 at 7-8 "
                         "components / 137-156 boundary edges / 5-9 non-manifold edges "
                         "from a LOD0 with 1 / 0 / 0. Decimating a closed solid into an "
                         "open one breaks the same section 7 sentence's 'preserve "
                         "boundary edges' clause, and the open fans made the custom "
                         "split normals unencodable, which failed the FBX round trip. "
                         "Hard normals are instead RE-DERIVED per level from that "
                         "level's own faces at the same threshold, which is stronger "
                         "than carrying flags the decimated surface has outgrown.",
            "seamPreservationTradeoff": "UV0 and material-border precision at LOD1/LOD2. "
                                        "3DMODEL_GEOLOGY_ROCKS.md section 5 makes "
                                        "triplanar object-space projection the primary "
                                        "material route and UV0 the decal/manifest "
                                        "fallback, and section 7 accepts a proxy shell at "
                                        "the coarse level. The authored seams a unique "
                                        "bake would use live on LOD0, which is never "
                                        "decimated.",
            "perLevelShadingBasisReDerived": True,
            "uniformVertexSkipping": False,
        },
        "shadingBasisPerLod": result.stale_smooth_edges,
        # The bedding mechanism, named in the manifest so a reviewer can tell WHICH grammar
        # produced the strata without reading the generator. Section 10 requires the "SDF,
        # voxel, fracture, erosion, or profile parameters used to generate the mesh".
        "beddingErosion": result.bedding_erosion,
        # Which SCALES are in the mesh and which are not. Section 10 wants the "erosion, or
        # profile parameters used to generate the mesh"; this is also the honest half of the
        # scale-witness claim, because a witness the lattice cannot sample is a material
        # parameter wearing a geometry label.
        "grainBand": result.grain_band,
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
        "proofArtifacts": {k: project_relative(v) for k, v in result.sheets.items()},
        "channelMeasurements": result.channel_stats,
        "silhouetteMetrics": result.silhouette,
        "silhouetteSummary": result.silhouette_summary,
        "silhouettePerFarLod": result.lod_silhouette,
        "fbx": project_relative(result.fbx_path),
        "fbxRoundTrip": {
            "verified": result.roundtrip_verified,
            "summary": result.export_summary,
            "measurements": result.roundtrip_notes,
            "meaning": "re-imported the written FBX and compared counts, uv layer order, "
                       "vertex colour values, world positions, corner normals, landmark "
                       "vertex, signed volume and raw FBX axis space against the scene. A "
                       "failure DELETES the fbx and aborts the variant, so a manifest that "
                       "exists always describes a file that passed.",
        },
        "unityPrefabAssembly": "NOT PERFORMED. .prefab/.mat/.asset creation is Unity-only "
                               "per AGENTS.md Evidence Law; this generator emits mesh + "
                               "manifest for a Unity-side assembler.",
    }
    # `identity` is dropped from the geology block because the shared producer emits it as a
    # top-level key from the same `law.GeneratorIdentity`. Nothing else is dropped: every
    # other key here is geology proof `3DMODEL_GEOLOGY_ROCKS.md` section 10 enumerates by
    # name, and the no-loss protocol in `Docs/AGENT_AUTHORITY_ROUTING.md` says move a rule's
    # full text to its destination rather than summarise it away.
    payload.pop("identity", None)

    # THE MANIFEST IS WRITTEN BY `export_unity.write_manifest`, NOT HERE.
    #
    # This function used to json.dump its own payload, and that made it a SECOND manifest
    # producer competing with the shared one. The cost was not cosmetic and it was not
    # theoretical -- it silently destroyed this family's authored normals at Unity import:
    #
    #   HectonFBXPostprocessor.cs:774 honours a package's importer contract only when the
    #   sibling manifest declares `"schema": "h8forge.manifest/1"`; :776-786 requires a
    #   `unityImport.modelImporter` block behind it; :789-793 requires
    #   `modelImporter.importNormals == "Import"` AND `export.hasCustomNormals == true`.
    #   Miss any one and :438-440 falls back to `importNormals = Calculate`.
    #
    # This payload had NO `schema` key and NO `unityImport` block -- it could not have,
    # because both are owned by `export_unity` -- so the carve-out could never fire for
    # geology. Measured on the shipped package: the boulder's `.fbx.meta` carried
    # `normalImportMode: 1` (Calculate) while flora's coral, which routes through the shared
    # producer, carried 0 (Import). Every weighted split normal `mesh_ops.apply_shading_basis`
    # authored, and the whole FBX round-trip corner-normal gate that guards them at 0.001
    # tolerance, was being re-derived from one angle and discarded downstream.
    #
    # Hand-adding `schema` and `unityImport` here would have been the wrong repair: it makes
    # two producers of one format, and the next field the postprocessor learns to read gets
    # added to one of them. Delegating means geology inherits that contract by construction,
    # and the geology-specific proof rides in `extra` where it cannot collide with it.
    meshes = result.mesh_reports or [
        # `h8forge.validate` is optional at import (see `_VALIDATE_IMPORT_NOTE`), and the
        # shared producer REFUSES a manifest with no mesh records. Without this fallback a
        # missing validator would turn a working run into a hard failure at the last step,
        # so the LOD census stands in -- fewer fields, same identities and counts.
        {"name": level["object"], "lod": level["index"],
         "triangles": level["triangles"], "lodBudget": level["effectiveBudget"],
         "withinBudget": level["triangles"] <= level["effectiveBudget"]}
        for level in result.lods]
    return export_unity.write_manifest(
        os.path.join(package_dir, export_unity.manifest_filename(
            law.Family.GEOLOGY, result.name)),
        identity, meshes,
        # No MAT_* or TX_* file is authored by this generator: the rock's colour lives in the
        # material base colour and every mask lives in a vertex-colour channel, so naming
        # files that do not exist would be a false reference. The shared producer records
        # both as `manifestGaps` instead, which is the honest record and the same thing
        # coral_branching.py does.
        [], [],
        # A plain dict, which `_collider_entry` accepts verbatim. The ColliderResult object
        # itself is not kept on VariantResult, and inventing a shim class to carry four
        # numbers it already has would be the more fragile of the two options.
        [{"name": law.NAME_COLLIDER.format(family=law.Family.GEOLOGY.value,
                                          name=result.name),
          "kind": result.collider_kind,
          "triangles": result.collider_triangles,
          "triangleBudget": law.COLLIDER_CONVEX_TRI_MAX,
          "withinBudget": result.collider_within_budget,
          "reason": "3dmodel.md section 9 convex proxy; LOD0 MeshCollider is banned"}]
        if result.collider_triangles else [],
        sorted(result.sheets.values()),
        export_result=result.export_result,
        uv_summary=result.uv or None,
        alpha_meaning="material blend / ore-emission mask "
                      "(3DMODEL_GEOLOGY_ROCKS.md section 4 vertex colour contract)",
        extra=payload)


def export_package(lods: list, collider, package_dir: str, name: str,
                   result: VariantResult, blackbox: BlackBox) -> str:
    """Delegate the FBX to ``h8forge.export_unity``, which now exists.

    The local ``bpy.ops.export_scene.fbx`` shim that used to live here was written because
    ``h8forge/__init__.py`` advertised an ``export_unity`` module that was absent from disk.
    It has landed, so the shim is deleted: ``export_lod_group`` owns the Unity axis
    conversion, the tangent basis, the ``_LOD0``/``_LOD1``/``_LOD2`` naming Unity keys its
    automatic LODGroup off, the ``COL_`` collider node, and a round-trip verification the
    shim never did.

    THREE THINGS THIS FUNCTION USED TO GET WRONG, all of the same class -- evidence
    produced and then discarded:

    1.  ``except Exception: return "EXPORT_FAILED: " + str(error)`` turned an aborted save
        into a STRING, which then travelled into the manifest's ``fbx`` field while
        ``main`` printed it and returned exit code 0. A round-trip rejection deletes the
        FBX (``export_unity`` does that deliberately), so the run left a manifest
        describing a file that does not exist and reported success. ``3dmodel.md``
        section 10 makes validation failure abort the save, and
        ``AGENTS.md`` ``[FORBID] Paper-success loops`` covers the rest: the abort is now
        re-raised so ``main`` counts it and exits non-zero, and the manifest is never
        written for a package that has no mesh.
    2.  No ``blackbox`` was passed, so ``export_fbx``'s ``blackbox.dump`` on round-trip
        failure could not fire. The measured per-object deltas -- position, colour, uv,
        corner normal, each printed with its tolerance -- existed inside
        ``RoundtripReport.notes`` and were dropped on the floor. A single-line failure
        message with no numbers behind it is how "corner normals changed by 0.001443"
        arrived with no way to tell whether that was 1.4x the tolerance or 27x the
        measured noise floor.
    3.  ``getattr(result, "path", path)``: ``ExportResult`` has no ``path`` attribute, so
        the default was ALWAYS taken and the exporter's own absolute, normalised path was
        never read back.
    """
    path = os.path.join(package_dir, "MESH_{f}_{n}.fbx".format(
        f=law.Family.GEOLOGY.value, n=name))
    identity = law.GeneratorIdentity(
        generator=GENERATOR_NAME, generator_version=GENERATOR_VERSION,
        seed=0, quality_weight=0.0, family=law.Family.GEOLOGY,
        scale_meters=0.0, camera_distance_class="", platform_lane="windows_copper_wire")
    export = export_unity.export_lod_group(lods, collider, path, identity=identity,
                                           blackbox=blackbox)
    result.roundtrip_notes = list(export.roundtrip_notes)
    result.roundtrip_verified = bool(export.roundtrip_verified)
    result.export_summary = export.summary()
    # The ExportResult itself, not just three fields lifted off it. `write_manifest` has to
    # emit an `export.hasCustomNormals` that HectonFBXPostprocessor.cs:789-793 reads as half
    # of its import contract, and it cannot reconstruct that from a summary string.
    result.export_result = export
    return export.fbx_path


# ---------------------------------------------------------------------------
# Proof renders
# ---------------------------------------------------------------------------

def render_proof(obj: bpy.types.Object, name: str, proof_dir: str, resolution: int,
                 result: VariantResult) -> None:
    """Studio + flat contact sheets and the four-channel sheet, then MEASURE the pixels.

    ``3DMODEL_GEOLOGY_ROCKS.md`` section 10 requires "screenshots with flat material
    override and final material to prove the silhouette carries geology before texture
    detail". ``AGENTS.md`` ``Never Trust Automated Assertions Alone``: the PNG existing
    proves nothing, so every channel tile is sampled through
    ``preview.measure_channel_png``.
    """
    base = dict(output_dir=proof_dir, resolution=resolution,
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
            "tile": project_relative(tile),
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
                        choices=sorted(set(SIZE_CLASSES) | set(SIZE_CLASS_ALIASES))
                        + ["all"])
    parser.add_argument("--process", default="sedimentary",
                        choices=("sedimentary", "basalt"))
    parser.add_argument("--out", default="")
    parser.add_argument("--preview", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--fbx", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--preview-resolution", dest="preview_resolution", type=int,
                        default=640)
    parser.add_argument("--debug-stage", dest="debug_stage", default="",
                        choices=("", "lattice", "fracture", "parting", "vug", "chip"),
                        help="stop after this stage and render it; isolation only")
    return parser.parse_args(argv)


def resolve_out_dirs(requested: str) -> tuple:
    """``(package_dir, proof_dir)``, both absolute, relative to the repo root.

    AGENTS.md ``[RULE] Relative Path Requirement`` bans hardcoded absolute developer
    paths, so both come from ``law.project_root()`` plus a law-owned relative subpath.

    Two directories because they are two different KINDS of artefact. The package -- FBX
    plus its sibling manifest -- must be under ``Assets`` for Unity to import it
    (``law.forge_package_dir``). The proof -- contact sheets, silhouette masks, channel
    tiles -- must NOT be, because Unity would import every PNG as a texture with a
    ``.meta``, a GUID and VRAM cost, for a diagnostic picture; ``law.forge_proof_dir``
    puts those in gitignored ``Docs/AgentLogs``. Writing both to
    ``law.forge_package_dir`` measured 27 stray PNGs against 2 real package files.

    ``--out`` overrides BOTH, deliberately. Its documented purpose is an iteration loop
    that must not touch the asset database at all, and a switch that redirected only half
    the output would still trigger a Unity import on every run -- which is the exact thing
    the flag exists to avoid. One flag, one scratch directory, nothing under ``Assets``.
    """
    if requested:
        scratch = (requested if os.path.isabs(requested)
                   else os.path.join(law.project_root(), requested))
        return scratch, scratch
    root = law.project_root()
    return (os.path.join(root, *law.forge_package_dir(law.Family.GEOLOGY).split("/")),
            os.path.join(root, *law.forge_proof_dir(law.Family.GEOLOGY).split("/")))


def main(argv: list) -> int:
    args = parse_args(argv)
    package_dir, proof_dir = resolve_out_dirs(args.out)
    os.makedirs(package_dir, exist_ok=True)
    os.makedirs(proof_dir, exist_ok=True)

    classes = (sorted(SIZE_CLASSES) if args.size_class == "all"
               else [resolve_size_class(args.size_class)])
    print("[rock] forge {fv} generator {gv} package={p} proof={r}".format(
        fv=law.FORGE_VERSION, gv=GENERATOR_VERSION, p=package_dir, r=proof_dir))
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
                    package_dir=package_dir, proof_dir=proof_dir,
                    want_preview=args.preview, want_fbx=args.fbx,
                    preview_resolution=args.preview_resolution,
                    debug_stage=args.debug_stage)
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
            if "beds" not in result.counts:
                # --debug-stage returns before the detail census exists. Printing it anyway
                # raised KeyError and lost the silhouette numbers the isolation run was for.
                print("[rock] (debug stage: no detail census)")
                continue
            print("[rock] beds={b} dip={d:.1f}deg fractures={f} veins={v} "
                  "partings={p} vugs={g}".format(
                      b=result.counts["beds"], d=result.counts["beddingDipDeg"],
                      f=result.counts["fracturePlanes"], v=result.counts["mineralVeins"],
                      p=result.counts["beddingPartingGrooves"],
                      g=result.counts["macroVugs"]))
            print("[rock] bed radius scales={s} steps_m={st}".format(
                s=result.counts["bedRadiusScales"],
                st=result.counts["bedRadiusStepM"]))
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
                      "effective {eb} | components {c} boundary {b} nonmanifold {n}".format(
                          i=level["index"], t=level["triangles"],
                          lb=level["lawFamilyBudget"], gb=level["geologySizeRowBudget"],
                          eb=level["effectiveBudget"],
                          c=level.get("components"), b=level.get("boundaryEdges"),
                          n=level.get("nonManifoldEdges")))
            print("[rock] collider: {t} tris / {m} ceiling ({k})".format(
                t=result.collider_triangles, m=law.COLLIDER_CONVEX_TRI_MAX,
                k=result.collider_kind))
            if result.grain_band:
                print("[rock] grain band: " + json.dumps(result.grain_band))
            if result.bedding_erosion:
                print("[rock] bedding erosion: " + json.dumps(result.bedding_erosion))
            print("[rock] post-fracture census: " + json.dumps(result.post_fracture_topology))
            print("[rock] topology: " + json.dumps(result.topology))
            if result.shading is not None:
                print("[rock] shading: smooth_polygons={s} sharp_edges={e} "
                      "weighted={w}".format(s=result.shading.smooth_polygons,
                                            e=result.shading.sharp_edges,
                                            w=result.shading.weighted_applied))
            if result.ao is not None:
                print("[rock] AO bake: samples={s} min={lo:.4f} max={hi:.4f} "
                      "mean={m:.4f} contrast={c}".format(
                          s=result.ao.samples, lo=result.ao.min_value,
                          hi=result.ao.max_value, m=result.ao.mean_value,
                          c=result.ao.has_contrast))
            for name in ("R", "G", "A"):
                key = "range" + name
                if key in result.channels:
                    print("[rock] vcol authored {n}: min/max/mean {v}".format(
                        n=name, v=result.channels[key]))
            print("[rock] vcol readback: " + json.dumps(result.channel_readback))
            print("[rock] vcol area-weighted: " + json.dumps(result.channel_area_stats))
            for entry in result.channel_stats:
                print("[rock] channel {c} ({m}): min={lo} max={hi} mean={me} "
                      "coverage={cv} gradient={g} subject={s}".format(
                          c=entry["channel"], m=entry["meaning"], lo=entry["min"],
                          hi=entry["max"], me=entry["mean"],
                          cv=entry["coverageFraction"], g=entry["hasGradient"],
                          s=entry["subjectVisible"]))
            if result.lod_silhouette:
                print("[rock] far-LOD silhouette: " + json.dumps(result.lod_silhouette))
            if result.silhouette_summary:
                print("[rock] silhouette: " + json.dumps(result.silhouette_summary))
                for entry in result.silhouette:
                    print("[rock] SIL {v}: top10={t} corners={c} convexity={x} "
                          "complexity={p} fuzz={f} hullGap={g}".format(
                              v=entry["view"], t=entry["turnTop10Fraction"],
                              c=entry["cornerCount"], x=entry["convexity"],
                              p=entry["complexity"], f=entry["fuzzFraction"],
                              g=entry["hullGapRms"]))
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
            # The round trip is the only measurement that says the FILE holds what the
            # scene holds, and it was being computed and thrown away. Every line carries a
            # measured delta next to its tolerance, so a near-miss is visible BEFORE it
            # becomes an abort -- which is the difference between "corner normals changed
            # by 0.001443" as a mystery and as a trend.
            for line in result.roundtrip_notes:
                print("[rock] RT " + line)
            if result.export_summary:
                print("[rock] EXPORT " + result.export_summary)
            print("[rock] MANIFEST " + result.manifest_path)
            if result.fbx_path:
                print("[rock] FBX " + result.fbx_path)

    print("")
    print("[rock] done, {f} aborted variant(s)".format(f=failures))
    return 1 if failures else 0


if __name__ == "__main__":
    _argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    sys.exit(main(_argv))

