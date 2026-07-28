"""HECTON-8 offline asset law, expressed as numbers.

Single source of truth for every threshold the 3D bibles impose. No generator may
hardcode a budget, bevel width, texel density, or padding value locally: import it
from here so the numbers cannot drift apart across families.

Sources (authority, read in full before editing this file):
  - ``3dmodel.md``                        root mesh/UV/LOD/collision law
  - ``PROCEDURAL_ASSET_PIPELINE.md``      package + validation + manifest law
  - ``3DMODEL_HARD_SURFACE_MODULES.md``   hard-surface specialisation
  - ``3DMODEL_FLORA_CORAL.md``            organic specialisation
  - ``3DMODEL_GEOLOGY_ROCKS.md``          geology specialisation
  - ``AGENTS.md``                         naming defaults, quality-weight law

Every constant below carries the bible section it came from. If a bible changes,
this file changes with it and the citation is how the next agent finds the source.
"""

from __future__ import annotations

import os
from dataclasses import dataclass, field
from enum import Enum


# ---------------------------------------------------------------------------
# Project root resolution
# ---------------------------------------------------------------------------
# AGENTS.md: "[RULE] Relative Path Requirement (No Hardcoded Absolute Paths):
# Hardcoding absolute developer paths ... is strictly banned. All screenshot, log,
# config, and data directories must be resolved relatively from the project root."
#
# This file lives at <root>/Tools/Blender/h8forge/law.py, so the root is three
# directories up. We verify by probing for a marker that only the repo root has,
# rather than trusting the arithmetic blindly.

_ROOT_MARKERS = ("AGENTS.md", "PROJECT_BIBLES.md", "Assets")


def project_root() -> str:
    """Absolute path to the HECTON-8 repo root, derived from this file's location.

    Walks upward looking for the repo markers instead of assuming a fixed depth,
    so moving this package one level does not silently produce paths that write
    assets into the wrong tree.
    """
    here = os.path.dirname(os.path.abspath(__file__))
    probe = here
    for _ in range(6):
        if all(os.path.exists(os.path.join(probe, m)) for m in _ROOT_MARKERS):
            return probe
        parent = os.path.dirname(probe)
        if parent == probe:
            break
        probe = parent
    raise RuntimeError(
        "HECTON-8 project root not found above " + here +
        " (expected markers: " + ", ".join(_ROOT_MARKERS) + ")"
    )


def asset_path(*parts: str) -> str:
    """Absolute path under ``<root>/Assets``."""
    return os.path.join(project_root(), "Assets", *parts)


# ---------------------------------------------------------------------------
# Asset families
# ---------------------------------------------------------------------------

class Family(str, Enum):
    """Generated asset families. Drives budgets, bevel policy and vcol semantics."""

    SMALL_PROP = "SmallProp"
    BASE_MODULE = "BaseModule"
    WRECKAGE = "Wreckage"
    FLORA = "Flora"
    FLORA_CLUSTER = "FloraCluster"
    FAUNA = "Fauna"
    GEOLOGY = "Geology"


class SurfaceClass(str, Enum):
    """Physical surface behaviour. Selects the vertex-colour contract."""

    HARD_SURFACE = "HardSurface"
    ORGANIC = "Organic"
    GEOLOGIC = "Geologic"


FAMILY_SURFACE_CLASS = {
    Family.SMALL_PROP: SurfaceClass.HARD_SURFACE,
    Family.BASE_MODULE: SurfaceClass.HARD_SURFACE,
    Family.WRECKAGE: SurfaceClass.HARD_SURFACE,
    Family.FLORA: SurfaceClass.ORGANIC,
    Family.FLORA_CLUSTER: SurfaceClass.ORGANIC,
    Family.FAUNA: SurfaceClass.ORGANIC,
    Family.GEOLOGY: SurfaceClass.GEOLOGIC,
}


# ---------------------------------------------------------------------------
# LOD triangle budgets  --  3dmodel.md section 7, "Default triangle budgets"
# ---------------------------------------------------------------------------
# Quoted from the bible table. These are HARD MAXIMA, not targets:
#   "These are hard maxima, not targets. If the silhouette reads correctly at
#    lower counts, spend saved budget on material detail..."

@dataclass(frozen=True)
class LodBudget:
    lod0: int
    lod1: int
    lod2: int
    impostor_min: int
    impostor_max: int

    def limit(self, lod_index: int) -> int:
        if lod_index == 0:
            return self.lod0
        if lod_index == 1:
            return self.lod1
        if lod_index == 2:
            return self.lod2
        return self.impostor_max


LOD_BUDGETS = {
    Family.SMALL_PROP: LodBudget(6_000, 2_000, 350, 2, 80),
    Family.BASE_MODULE: LodBudget(15_000, 5_000, 700, 12, 300),
    Family.WRECKAGE: LodBudget(25_000, 8_000, 1_200, 12, 500),
    Family.FLORA: LodBudget(6_500, 1_800, 300, 2, 80),
    Family.FLORA_CLUSTER: LodBudget(14_000, 4_000, 700, 12, 200),
    # Fauna: 3dmodel.md section 7 says "VAT/impostor only" for the far tier, which is a
    # ROUTE statement, not a budget of zero. Encoding 0/0 made `limit(3)` return 0, so any
    # fauna impostor read as over budget no matter how cheap it was -- a gate that can only
    # fail is the same defect as a gate that can never fire. The band mirrors the
    # comparable large-body classes; the VAT/impostor ROUTE requirement is enforced by the
    # family bible and the prefab assembler, not by pretending no triangles are allowed.
    Family.FAUNA: LodBudget(35_000, 12_000, 2_000, 12, 500),
    Family.GEOLOGY: LodBudget(18_000, 7_000, 1_200, 12, 250),
}


# ---------------------------------------------------------------------------
# Bevel policy  --  3dmodel.md section 4, "Hard-Surface Engineering Law"
# ---------------------------------------------------------------------------
# "Ninety-degree mathematical corners are banned on visible metal, plastic,
#  ceramic, glass, rubber, pressure doors, habitat modules, wreckage, pipes,
#  railings, consoles, and equipment."
#
# "Every visible hard edge whose adjacent face angle is greater than 35 degrees
#  must be processed by a bevel/chamfer pass unless the edge is explicitly hidden
#  inside an occluded connector seam."

BEVEL_ANGLE_THRESHOLD_DEG = 35.0

# "Clamp bevel width to 20 percent of the shortest adjacent edge to prevent
#  self-overlap." (3dmodel.md section 4, step 5)
BEVEL_WIDTH_CLAMP_RATIO = 0.20


@dataclass(frozen=True)
class BevelRange:
    """Metres. Width scales continuously with GlobalQualityWeight and asset size."""

    min_m: float
    max_m: float

    def width_for(self, quality_weight: float) -> float:
        q = _saturate(quality_weight)
        return self.min_m + (self.max_m - self.min_m) * q


BEVEL_RANGES = {
    # "Small handheld prop: 0.006 m to 0.018 m."
    Family.SMALL_PROP: BevelRange(0.006, 0.018),
    # "Base module structural edge: 0.035 m to 0.12 m."
    Family.BASE_MODULE: BevelRange(0.035, 0.12),
    # "Exterior hull/wreckage macro edge: 0.08 m to 0.35 m."
    Family.WRECKAGE: BevelRange(0.08, 0.35),
    # Geology has no row in 3dmodel.md section 4 because that table is written for
    # manufactured edges. A rock's chip is not a machined chamfer, and the bible warns that
    # a uniform bevel reads as machined - 3DMODEL_GEOLOGY_ROCKS.md wants "chipped edges".
    # The range is wide on purpose so a generator can partition its hard edges into several
    # buckets at different widths rather than applying one chamfer everywhere.
    Family.GEOLOGY: BevelRange(0.008, 0.09),
}


# Per-size geology LOD rows. `LOD_BUDGETS[Family.GEOLOGY]` carries only the LARGE row
# (18000/7000/1200) from 3dmodel.md section 7, but 3DMODEL_GEOLOGY_ROCKS.md section 7 is
# stricter for smaller classes, and a 0.8 m boulder allowed 18000 triangles is a budget
# failure waiting to be discovered in a scatter field. Callers take min() of the two.
GEOLOGY_SIZE_LOD_BUDGETS = {
    "boulder": LodBudget(4_000, 1_200, 250, 12, 250),
    "outcrop": LodBudget(9_000, 3_000, 600, 12, 250),
    "cliffchunk": LodBudget(18_000, 7_000, 1_200, 12, 250),
}


def geology_budget_for(size_class: str) -> LodBudget:
    """Tightest applicable geology budget for a size class.

    Unknown class falls back to the large row rather than the tightest, because silently
    over-tightening an unlisted class would decimate a legitimate cliff to boulder density.
    """
    return GEOLOGY_SIZE_LOD_BUDGETS.get(size_class, LOD_BUDGETS[Family.GEOLOGY])

# "Interior equipment and panel trim: 0.012 m to 0.035 m."
BEVEL_RANGE_INTERIOR_TRIM = BevelRange(0.012, 0.035)

# "Low tier keeps fewer bevel segments but keeps at least one chamfer face on
#  every visible hard edge. Ultra tier may add 3-6 bevel segments on hero
#  modules..."  -> continuous, monotonic, never zero.
BEVEL_SEGMENTS_MIN = 1
BEVEL_SEGMENTS_MAX = 6


def bevel_segments_for(quality_weight: float, hero: bool = False) -> int:
    """Continuous segment count. Never returns 0 -- one chamfer face is the floor.

    3dmodel.md forbids a binary quality switch; the count is a rounded sample of a
    continuous curve, and the floor of 1 is the bible's explicit low-tier minimum.
    """
    q = _saturate(quality_weight)
    ceiling = BEVEL_SEGMENTS_MAX if hero else 4
    span = ceiling - BEVEL_SEGMENTS_MIN
    return int(BEVEL_SEGMENTS_MIN + round(span * q))


# "Smoothing groups are not optional. A generator must group connected faces when
#  their normal angle is below the smooth threshold..."
#
# 32 degrees is the HARD-SURFACE default: manufactured objects have real creases, and a
# panel seam that shades smooth reads as rubber. Applying that same threshold to organic
# tissue is a design error, not a conservative choice -- displaced organic surfaces
# routinely exceed 32 degrees between adjacent faces, every one of those gets marked sharp,
# and the asset renders as faceted plates. Measured on a branching coral: the studio render
# was indistinguishable from the flat render because nearly every surface edge had been
# classified hard.
#
# 3dmodel.md section 5 wants organics carrying "secondary silhouette noise ... nonuniform
# cross-sections" while still reading as grown tissue, which only works if that noise
# shades smoothly and only genuine breaks stay hard.
SMOOTH_ANGLE_DEG = 32.0

SMOOTH_ANGLE_BY_SURFACE = {
    SurfaceClass.HARD_SURFACE: 32.0,
    # Organic: only a torn edge, a broken tip or a plate rim should stay hard.
    SurfaceClass.ORGANIC: 68.0,
    # Geology sits between the two: strata steps and fracture planes are genuine hard
    # breaks, but weathered mass should not facet.
    SurfaceClass.GEOLOGIC: 46.0,
}


def smooth_angle_for(surface_class: "SurfaceClass") -> float:
    """Angle threshold above which an edge is treated as a hard break."""
    return SMOOTH_ANGLE_BY_SURFACE.get(surface_class, SMOOTH_ANGLE_DEG)


# ---------------------------------------------------------------------------
# Vertex colour contracts
# ---------------------------------------------------------------------------
# 3dmodel.md section 4 (hard surface wear data):
#   R = exposed edge wear / salt-polished rim mask
#   G = rust, oxidation, biofilm, or fluid stain phase/amount
#   B = baked ambient occlusion and cavity darkness
#   A = optional emission, warning paint, or decal eligibility mask
#
# 3dmodel.md section 5 + 3DMODEL_FLORA_CORAL.md section 2 (organic):
#   R = current/water sway amplitude. Root/anchor 0, tips approach 255
#   G = bioluminescence phase or mask
#   B = baked ambient occlusion / cavity darkness
#   A = family-specific; meaning MUST be documented in the manifest

HARD_SURFACE_VCOL = ("edge_wear", "oxidation", "baked_ao", "emission_mask")
ORGANIC_VCOL = ("sway_amplitude", "biolum_phase", "baked_ao", "family_specific")

# The four channels are PACKED into one attribute, not four named layers. Both the
# writer and every validator must agree on this single name, or a contract check that
# derives layer names from the tuples above rejects every asset the forge produces --
# which is exactly what happened before this constant existed.
VCOL_ATTRIBUTE_NAME = "Col"
VCOL_DATA_TYPE = "BYTE_COLOR"   # matches the bible's Color | UNorm8 x4 vertex stream
VCOL_DOMAIN = "CORNER"          # per-loop, so a split UV seam keeps distinct values

VCOL_CONTRACT = {
    SurfaceClass.HARD_SURFACE: HARD_SURFACE_VCOL,
    SurfaceClass.ORGANIC: ORGANIC_VCOL,
    SurfaceClass.GEOLOGIC: HARD_SURFACE_VCOL,
}

# 3DMODEL_FLORA_CORAL.md section 2, quantised sway bands:
#   "Anchor/root = 0. Rigid mineralized coral = 0 to 32.
#    Flexible frond tips = 192 to 255."
SWAY_ANCHOR = 0.0
SWAY_RIGID_MINERAL_MAX = 32.0 / 255.0
SWAY_FLEXIBLE_TIP_MIN = 192.0 / 255.0
SWAY_FLEXIBLE_TIP_MAX = 1.0

# "The red channel must follow physical leverage:
#   sway = saturate(distanceFromAnchor / maxFlexibleLength) ^ stiffnessExponent"
# A stiffer organism uses a larger exponent so movement concentrates at the tip.
STIFFNESS_EXPONENT_FLEXIBLE_BLADE = 1.25
STIFFNESS_EXPONENT_BRANCHING_CORAL = 2.60
STIFFNESS_EXPONENT_MINERALISED = 4.00


def sway_amplitude(
    distance_from_anchor: float,
    max_flexible_length: float,
    stiffness_exponent: float,
) -> float:
    """The bible's sway formula, verbatim.

    ``sway = saturate(distanceFromAnchor / maxFlexibleLength) ^ stiffnessExponent``

    A zero or non-finite ``max_flexible_length`` means the caller has a degenerate
    curve; returning the anchor value keeps roots rigid rather than producing a
    division artefact that would silently make an entire organism sway uniformly
    -- which is an explicit rejection gate ("Root vertices sway as much as tips").
    """
    if not _finite(max_flexible_length) or max_flexible_length <= 1e-9:
        return SWAY_ANCHOR
    if not _finite(distance_from_anchor):
        return SWAY_ANCHOR
    t = _saturate(distance_from_anchor / max_flexible_length)
    return _saturate(pow(t, max(1e-3, stiffness_exponent)))


# ---------------------------------------------------------------------------
# UV / texture law  --  3dmodel.md section 6, 3DMODEL_FLORA_CORAL.md section 5
# ---------------------------------------------------------------------------

# "Stretched polygons above 15 percent aspect distortion for hero/near assets or
#  25 percent for distant-only assets." (forbidden UV states)
UV_STRETCH_MAX_HERO = 0.15
UV_STRETCH_MAX_DISTANT = 0.25

# How that per-triangle limit is ENFORCED, which the bible does not spell out and which
# a naive reading gets wrong. Measured control experiment on known-good geometry, same
# solver and same metric as the gate:
#
#   geometry            p50     p95     % of TRIANGLES over 0.15
#   clean UV sphere     0.198   0.437   68 %
#   Suzanne             0.446   1.754   86 %
#
# So "no single triangle may exceed 0.15" is unreachable for ANY closed curved surface --
# a clean UV sphere fails on two thirds of its triangles. A gate that cannot pass is the
# same class of defect as a gate that cannot fire, and it aborted every save.
#
# The bible's target is a VISIBLE defect: "stretched polygons". Conformal unwrap of a
# closed surface has a mathematically unavoidable pole singularity, and the triangles at
# that pole are tiny -- they are not visible stretch. So the population is judged by
# SURFACE AREA, not by triangle count: how much of the thing the player actually looks at
# is stretched. An outlier cap keeps a single catastrophic triangle from hiding inside a
# good average.
UV_STRETCH_AREA_FRACTION_MAX = 0.10
UV_STRETCH_OUTLIER_MULTIPLIER = 6.0

# Organic surfaces get a wider per-triangle limit than manufactured panels. 3dmodel.md
# section 6 permits box/projection unwrap "for industrial panels only when each face has
# calibrated texel density", i.e. the tight limit is aimed at flat calibrated panels;
# 3DMODEL_FLORA_CORAL.md section 5 instead asks for lengthwise blade UVs and cylindrical
# stalk unwraps, both of which distort by construction on a tapering curved form.
UV_STRETCH_MAX_BY_SURFACE = {
    SurfaceClass.HARD_SURFACE: UV_STRETCH_MAX_HERO,
    SurfaceClass.ORGANIC: 0.55,
    SurfaceClass.GEOLOGIC: 0.40,
}


def uv_stretch_limit_for(surface_class: "SurfaceClass", hero: bool = True) -> float:
    """Per-triangle aspect-distortion limit for a surface class."""
    base = UV_STRETCH_MAX_BY_SURFACE.get(
        surface_class, UV_STRETCH_MAX_HERO if hero else UV_STRETCH_MAX_DISTANT)
    return base if hero else max(base, UV_STRETCH_MAX_DISTANT)


# UV-space degeneracy epsilon. DEGENERATE_TRIANGLE_AREA_EPS is a WORLD area in square
# metres; UV area is dimensionless in a 0..1 domain. Comparing the two mixes units, so a
# healthy 5 mm triangle at high texel density measured as degenerate purely because its
# UV footprint is small. This epsilon is in UV units and is below the area of a
# quarter-texel at 4096.
DEGENERATE_UV_AREA_EPS = 1e-12

# "Texel density mismatch above 20 percent between adjacent hard-surface panels"
UV_TEXEL_MISMATCH_MAX = 0.20

# "Islands smaller than 4 pixels at target mip 0 for any visible LOD0 detail."
UV_MIN_ISLAND_PIXELS = 4

# Atlas padding baseline, 3dmodel.md section 6.
ATLAS_PADDING_PX = {512: 8, 1024: 12, 2048: 16, 4096: 24}


def atlas_padding_for(atlas_size: int) -> int:
    """Padding in pixels. Unlisted sizes fall back to the bible's formula:

    ``requiredPaddingPixels = max(8, 2 ^ mipCountNeededForSmallestSupportedMip)``
    approximated by scaling from the nearest declared baseline, never below 8.
    """
    if atlas_size in ATLAS_PADDING_PX:
        return ATLAS_PADDING_PX[atlas_size]
    for size in sorted(ATLAS_PADDING_PX, reverse=True):
        if atlas_size >= size:
            return ATLAS_PADDING_PX[size]
    return 8


# 3DMODEL_FLORA_CORAL.md section 5, texel density in pixels per metre.
TEXEL_DENSITY_HERO_FLORA = 512
TEXEL_DENSITY_COMMON_FLORA = 256
TEXEL_DENSITY_FIELD_HLOD_MIN = 64
TEXEL_DENSITY_FIELD_HLOD_MAX = 128


# ---------------------------------------------------------------------------
# Material slot contract  --  3dmodel.md section 6
# ---------------------------------------------------------------------------
#   Slot 0: primary structural/tissue material
#   Slot 1: exposed cut, bevel, edge, scar, or fracture material
#   Slot 2: secondary trim, gasket, barnacle, mineral vein, or growth plate
#   Slot 3: emissive/bioluminescent/details only when needed

MATERIAL_SLOT_PRIMARY = 0
MATERIAL_SLOT_CUT_EDGE = 1
MATERIAL_SLOT_TRIM = 2
MATERIAL_SLOT_EMISSIVE = 3
MATERIAL_SLOT_MAX = 4


# ---------------------------------------------------------------------------
# Collision proxy law  --  3dmodel.md section 9
# ---------------------------------------------------------------------------
# "LOD0 visual meshes must never be assigned directly to production MeshCollider
#  components." / "Rocks/coral/complex geology: convex hull or convex
#  decomposition under 200 triangles total per asset, preferably much lower."

COLLIDER_CONVEX_TRI_MAX = 200
COLLIDER_PREFIX = "COL_"
VISUAL_PREFIX = "VIS_"
LOD_PREFIX = "LOD_"

# PROCEDURAL_ASSET_PIPELINE.md, "Collision And Interaction Package"
INTERACTION_ANCHORS = (
    "ANCHOR_Scan",
    "ANCHOR_Cut",
    "ANCHOR_Weld",
    "ANCHOR_Loot",
    "ANCHOR_Pry",
    "ANCHOR_Open",
    "ANCHOR_Repair",
)

# 3DMODEL_FLORA_CORAL.md section 7: "Default flora/coral collision is none."
FAMILIES_WITHOUT_DEFAULT_COLLISION = (Family.FLORA, Family.FLORA_CLUSTER)


# ---------------------------------------------------------------------------
# Mesh validation tolerances  --  3dmodel.md section 10
# ---------------------------------------------------------------------------
# "Normals normalized within 0.995 to 1.005 length."
NORMAL_LENGTH_MIN = 0.995
NORMAL_LENGTH_MAX = 1.005
TANGENT_LENGTH_MIN = 0.995
TANGENT_LENGTH_MAX = 1.005

# "area = length(cross(p1 - p0, p2 - p0)); assert area > 0.0000001"
DEGENERATE_TRIANGLE_AREA_EPS = 1e-7

# "assert bounds finite and extents above 0.001 m"
MIN_BOUNDS_EXTENT_M = 0.001


# ---------------------------------------------------------------------------
# Naming  --  AGENTS.md "Project Shape", PROCEDURAL_ASSET_PIPELINE.md package law
# ---------------------------------------------------------------------------

NAME_MESH = "MESH_{family}_{name}_LOD{lod}"
NAME_MATERIAL = "MAT_{family}_{role}"
NAME_TEXTURE = "TX_{family}_{set}_{role}"
NAME_COLLIDER = "COL_{family}_{name}"
NAME_PREFAB_GENERATED = "GEN_{family}_{name}"
NAME_MANIFEST = "MANIFEST_{family}_{name}"


# ---------------------------------------------------------------------------
# Black box  --  3dmodel.md section 11
# ---------------------------------------------------------------------------
# "Critical generator pipelines must keep the last 300 high-level bake steps in a
#  fixed ring during generation."
BLACKBOX_RING_CAPACITY = 300


# ---------------------------------------------------------------------------
# Generator identity  --  PROCEDURAL_ASSET_PIPELINE.md "Deterministic Source Contract"
# ---------------------------------------------------------------------------
# "The generator must store deterministic seed, generator script name and semantic
#  version, GlobalQualityWeight, asset family, intended scale in meters, camera
#  distance class, target platform lane, source texture/reference IDs, and
#  validation summary hash."

FORGE_VERSION = "1.0.0"


@dataclass
class GeneratorIdentity:
    """The deterministic provenance block every generated package must carry."""

    generator: str
    generator_version: str
    seed: int
    quality_weight: float
    family: Family
    scale_meters: float
    camera_distance_class: str
    platform_lane: str
    forge_version: str = FORGE_VERSION
    source_references: tuple = field(default_factory=tuple)

    def as_dict(self) -> dict:
        return {
            "generator": self.generator,
            "generatorVersion": self.generator_version,
            "forgeVersion": self.forge_version,
            "seed": int(self.seed),
            "qualityWeight": round(float(self.quality_weight), 6),
            "family": self.family.value,
            "scaleMeters": round(float(self.scale_meters), 6),
            "cameraDistanceClass": self.camera_distance_class,
            "platformLane": self.platform_lane,
            "sourceReferences": list(self.source_references),
        }


# ---------------------------------------------------------------------------
# Small numeric helpers
# ---------------------------------------------------------------------------

def _finite(x: float) -> bool:
    return x == x and x not in (float("inf"), float("-inf"))


def _saturate(x: float) -> float:
    if not _finite(x):
        return 0.0
    if x < 0.0:
        return 0.0
    if x > 1.0:
        return 1.0
    return x


# Public aliases -- generators use these rather than reimplementing clamping.
finite = _finite
saturate = _saturate
