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

# The outlier ceiling must ALSO ignore slivers, or it reintroduces the very bug the
# area-weighted population test was written to remove -- judged on a single triangle with
# no regard for whether that triangle covers any visible surface.
#
# Measured on a kelp LOD: a failing triangle had world edge lengths
# [0.0529, 0.0443, 0.0972] where 0.0529 + 0.0443 ~= 0.0972. That is a collinear sliver --
# 9.7 cm long, 1.8 mm altitude, roughly 0.9 cm2 of surface. On a plant with ~0.5 m2 of
# surface it is 0.017% of what the player sees, and sigma_max/sigma_min is numerically
# ill-conditioned on a near-degenerate triangle, so the metric amplifies rounding error
# there rather than measuring stretch. Ten documented attempts to remove such slivers
# geometrically all converged just under whatever threshold was set, then produced a
# DIFFERENT outlier -- the signature of chasing a numerical artefact, not a defect.
#
# Expressed relative to the MEAN triangle area so it is scale-independent: a triangle far
# smaller than its mesh's typical triangle is a sliver whatever the asset's size.
UV_STRETCH_OUTLIER_MIN_AREA_RATIO = 0.10

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
# Texture roles and channel layouts
# ---------------------------------------------------------------------------
# ``3DMODEL_TEXTURES_MATERIALS.md`` section 2 (AMENDED 2026-07-29 on measurement) and
# ``3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`` section 3 (AMENDED the same day).
#
# The suffix set below is the SHIPPED one, not the pre-amendment one. Census, run again
# from this file's own tree on 2026-07-29 and reproducing the lead's numbers exactly:
#
#   suffix                    files under Assets/_Project/Art/TEXTURES
#   _BaseColor                138
#   _NormalGL                 138
#   _MaskMap_UnityURP         138
#   _ARM_AO_Rough_Metal       138
#   _Height                   138
#   _MRAO                       0
#   _Albedo                     0
#
# So the five roles below are one consistent family set repeated 138 times, and the
# superseded ``_Albedo``/``_Normal``/``_MRAO`` spellings never existed on disk.
# ``_Normal`` (18) and ``_Detail`` (18) do exist as a smaller, separate set.

TEXTURE_ROLE_BASECOLOR = "BaseColor"
TEXTURE_ROLE_NORMAL = "NormalGL"
TEXTURE_ROLE_MASK_URP = "MaskMap_UnityURP"
TEXTURE_ROLE_ARM = "ARM_AO_Rough_Metal"
TEXTURE_ROLE_HEIGHT = "Height"
TEXTURE_ROLE_EMISSION = "Emission"
TEXTURE_ROLE_DETAIL = "Detail"

# The default shipped stack. Emission and Detail are conditional roles, not defaults:
# the playbook restricts emission to "bioluminescence, instrument glow, hot venting,
# energized equipment, or emergency markings", so a family that has none of those must
# OMIT the map rather than ship a black one.
SHIPPED_TEXTURE_ROLES = (
    TEXTURE_ROLE_BASECOLOR,
    TEXTURE_ROLE_NORMAL,
    TEXTURE_ROLE_MASK_URP,
    TEXTURE_ROLE_ARM,
    TEXTURE_ROLE_HEIGHT,
)

# TWO PACKED LAYOUTS SHIP AND THEY ARE NOT INTERCHANGEABLE. Binding one where the other
# is expected puts ambient occlusion in the metallic slot, which is silent and wrong.
# Every manifest must record which layout each map uses; pick by suffix, never by
# assumption.
#
# ``_MaskMap_UnityURP`` is bit-exact against the live master shader
# ``Assets/_Project/Art/Shaders/Hecton_ModuleHardSurfaceLit.shader``: the property is
# labelled "Packed Mask (R Metallic G Occlusion A Smoothness)" at :71 and decoded at
# :349-353 as ``metallic = packedMask.r``, ``smoothness = packedMask.a``,
# ``occlusionMap = packedMask.g``. B is never read.
MASKMAP_URP_CHANNELS = ("metallic", "occlusion", "unused", "smoothness")
ARM_CHANNELS = ("ambient_occlusion", "roughness", "metallic", "unused")

TEXTURE_CHANNEL_LAYOUTS = {
    TEXTURE_ROLE_MASK_URP: MASKMAP_URP_CHANNELS,
    TEXTURE_ROLE_ARM: ARM_CHANNELS,
}

# ``3DMODEL_TEXTURES_MATERIALS.md`` section 8, "Import And Streaming Rules". These are
# DECLARATIONS: the Blender lane cannot run Unity's importer, so a generator states the
# contract and the Unity-side binder enforces it.
TEXTURE_IMPORT_SETTINGS = {
    TEXTURE_ROLE_BASECOLOR: {
        "sRGB": True, "textureType": "Default", "compression": "HighQuality",
        "format": "BC7", "mobileFormat": "ASTC_6x6", "mipmaps": True,
        "wrapMode": "Repeat", "filterMode": "Trilinear",
    },
    TEXTURE_ROLE_NORMAL: {
        "sRGB": False, "textureType": "NormalMap", "compression": "HighQuality",
        "format": "BC5", "mobileFormat": "ASTC_6x6", "mipmaps": True,
        "wrapMode": "Repeat", "filterMode": "Trilinear",
        "convention": "OpenGL (+Y up), which is what Unity samples",
    },
    TEXTURE_ROLE_MASK_URP: {
        "sRGB": False, "textureType": "Default", "compression": "HighQuality",
        "format": "BC7", "mobileFormat": "ASTC_6x6", "mipmaps": True,
        "wrapMode": "Repeat", "filterMode": "Trilinear",
    },
    TEXTURE_ROLE_ARM: {
        "sRGB": False, "textureType": "Default", "compression": "HighQuality",
        "format": "BC7", "mobileFormat": "ASTC_6x6", "mipmaps": True,
        "wrapMode": "Repeat", "filterMode": "Trilinear",
    },
    TEXTURE_ROLE_HEIGHT: {
        "sRGB": False, "textureType": "Default", "compression": "HighQuality",
        "format": "BC4", "mobileFormat": "ASTC_6x6", "mipmaps": True,
        "wrapMode": "Repeat", "filterMode": "Trilinear",
        "note": "offline source for parallax/normal derivation; ships only if the "
                "shader contract and platform budget allow it (playbook section 3)",
    },
}


# ``3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`` section 7, "Continuous Quality Lanes".
# Quoted bands: compact near 0.0 = "512 props, 1024 standard world"; middle ~0.35 =
# "1024 props, 2048 key world materials"; high ~0.7 = "2048 hero surfaces"; ultra near
# 1.0 = "2048/4096 hero-only sources".
#
# Expressed as a continuous ramp with a hero flag, because section 7 opens with
# "Texture generation must scale through continuous GlobalQualityWeight, not binary
# low/high switches" -- the same ban on a binary switch that ``bevel_segments_for``
# obeys.
TEXTURE_SIZE_LADDER = (512, 1024, 2048, 4096)


def texture_size_for(quality_weight: float, hero: bool = False) -> int:
    """Bake resolution for a quality weight, clamped to the family's ceiling.

    Non-hero surfaces stop at 2048: section 7 reserves 4096 for "hero-only sources".
    """
    q = _saturate(quality_weight)
    if hero:
        # 512 -> 1024 -> 2048 -> 4096 across the weight range.
        index = int(round(q * 3.0))
    else:
        # 512 -> 1024 -> 2048, ceiling at 2048 for standard world materials.
        index = int(round(q * 2.0))
    return TEXTURE_SIZE_LADDER[max(0, min(len(TEXTURE_SIZE_LADDER) - 1, index))]


# ---------------------------------------------------------------------------
# Geology material-space scale witnesses
# ---------------------------------------------------------------------------
# These are the numbers that make a geology texture SCALE-CALIBRATED rather than
# decorative. ``3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`` section 4 opens the geology
# recipe with "Geology textures must be scale-calibrated", and section 6 requires a
# declared tile size in metres.
#
# OUTSTANDING DEBT, NOT RESOLVED HERE: ``generators/rock.py`` holds its own copies at
# :175 ``WITNESS_GRAIN_WAVELENGTH_M = 0.075``, :176 ``WITNESS_PIT_WAVELENGTH_M = 0.052``,
# :177 ``WITNESS_PIT_DEPTH_M = 0.011`` and :2879 ``TRIPLANAR_METRES_PER_TILE = 1.25``.
# Verified byte-equal to the rows below on 2026-07-29, so nothing is inconsistent TODAY,
# but two homes for one number is the drift this file exists to prevent. Migrating
# ``rock.py`` to import these is a mesh-generator edit and belongs to that file's owner.
GEOLOGY_TRIPLANAR_METRES_PER_TILE = 1.25
GEOLOGY_GRAIN_WITNESS_M = 0.075
GEOLOGY_PIT_WITNESS_M = 0.052
GEOLOGY_PIT_DEPTH_M = 0.011
GEOLOGY_BED_THICKNESS_RANGE_M = (0.055, 0.34)

# WHY THE TEXTURE OWNS A BAND AND NOT "DETAIL IN GENERAL".
#
# ``rock.py``'s own measurement, recorded per asset in
# ``profileParameters``/``grainBand``: the finest wavelength the sculpt lattice can
# represent is 0.087 m on a boulder, 0.205 m on an outcrop, 0.406 m on a cliffchunk.
# Everything below the size class's own figure is absent from the mesh, and
# ``3DMODEL_GEOLOGY_ROCKS.md`` section 2 routes it to "baked normal/depth support" --
# which is this texture family.
#
# The CEILING is the boulder's figure, the tightest of the three, and that choice is
# load-bearing rather than conservative. One tile is shared by every size class. Detail
# between 0.087 m and 0.406 m is genuinely missing from a cliffchunk mesh but genuinely
# PRESENT in a boulder mesh, so putting it in the shared tile would double it on
# boulders -- the texture fighting the geometry, which is the "does not match the mesh"
# rejection in playbook section 0. Anything strictly under 0.087 m is missing from all
# three, so a shared tile can carry it without ever competing with a real ledge.
GEOLOGY_MESH_FINEST_WAVELENGTH_M = {
    "boulder": 0.087,
    "outcrop": 0.205,
    "cliffchunk": 0.406,
}
GEOLOGY_TEXTURE_BAND_CEILING_M = 0.087


# ---------------------------------------------------------------------------
# Structural EXTENT budget  --  how far a feature runs, not how deep it is
# ---------------------------------------------------------------------------
# A DEPTH BUDGET ALONE IS HALF A CONSTRAINT, and the missing half was found by looking at
# a render rather than at a number.
#
# The geology texture family was rejected once for an inverted relief hierarchy: vugs
# deeper than bedding. Fixing that gave every term a declared depth as a fraction of the
# bedding relief, and the ordering became structural. The tile was then rejected AGAIN,
# for laminae that ran uninterrupted from edge to edge of the 1.25 m tile and read as sawn
# timber or veneered board. Same class of defect one axis over: every term had a stated
# depth and NONE had a stated extent, so a bed traversing the whole tile at constant
# thickness was making exactly the kind of unstated claim that a rank-one vug had made.
#
# Real weathered marine bedding is interrupted. Differential erosion cuts across bed
# packages, flakes detach along partings and terminate at joints, and a bed pinches out or
# is truncated rather than crossing an entire face. So a family that tiles must declare
# how far its dominant structure is allowed to run, and must MEASURE it -- an uninterrupted
# run is also the thing that makes tiling visible, because the eye tracks a continuous line
# across the repeat boundary far more readily than it tracks a broken one.
#
# Expressed as a fraction of the tile so it is resolution- and scale-independent.
# ``3DMODEL_GEOLOGY_ROCKS.md`` section 1 is the authority being served: the surface must
# "contain readable geological process: sediment bands, chipped edges, sheared planes ...
# collapsed fracture faces", and a bed with no truncation shows deposition without any of
# the erosion that followed it.
GEOLOGY_LAMINA_MAX_RUN_FRACTION = 0.55

# Minimum share of the surface that must be interrupted by erosional structure -- spall
# scars and joints -- or the face reads as deposited-and-never-weathered.
GEOLOGY_MIN_EROSIONAL_COVERAGE = 0.18


# ---------------------------------------------------------------------------
# Texture acceptance gate thresholds
# ---------------------------------------------------------------------------
# ``3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`` section 9 lists eleven gates in prose with
# no numbers. These are the numeric forms, and each one is annotated with what the prose
# asked for. A gate with no number cannot fire, which the project treats as the same
# defect as a gate that can only fail.

# "2x2 tile seam check for tileable sources." A periodic field has no seam, so the test
# asks whether the step ACROSS the wrap is distinguishable from an ordinary step INSIDE
# the tile.
#
# THE STATISTIC MATTERS MORE THAN THE THRESHOLD, and the first version of this row got it
# wrong. It compared the wrap row-pair's mean gradient against the MEAN of all interior
# row-pairs and allowed a ratio of 1.35. Measured on a provably periodic geology bake, the
# base-colour wrap scored 1.69 and FAILED -- while sitting at the 92nd percentile of the
# interior distribution, whose p95 was 10.33 against the wrap's 9.30. The wrap simply
# landed near a lamina contact. One sample against a population mean is not a test of
# anything: for a field with spatially varying gradient, half the interior row-pairs would
# also have "failed".
#
# The gate is now the wrap value against the 99th PERCENTILE of the per-line interior
# distribution. At or below 1.0 the seam is indistinguishable from the worst ordinary step
# in the tile; a genuine discontinuity sits far above the maximum, not just above the mean.
# Verified to still fire: a deliberately non-periodic control scores well over 3.
TEXTURE_SEAM_EXCESS_MAX = 1.15

# Below this many lines the percentile is computed from too few samples to mean anything,
# so the mip gate stops measuring seams rather than reporting noise as a result.
TEXTURE_SEAM_MIN_LINES = 32

# "Histogram sanity: no crushed full-black/full-white albedo." Fraction of pixels pinned
# at 0 or 255 in any base-colour channel.
TEXTURE_CLIPPED_FRACTION_MAX = 0.010

# "Albedo luminance range compatible with URP lighting; no baked directional
# highlights." Two numbers. The luminance band keeps albedo inside the range URP's
# lighting can work with; the correlation test is the one that actually catches baked
# light -- if albedo encodes a lamp, its luminance correlates with N.L for SOME light
# direction, so the gate is the MAXIMUM absolute correlation over a sampled sphere of
# directions.
TEXTURE_ALBEDO_LUMA_MIN = 0.015
TEXTURE_ALBEDO_LUMA_MAX = 0.780
TEXTURE_ALBEDO_LIGHT_CORRELATION_MAX = 0.35

# "Normal strength in family range; no inverted green channel; no flat accidental
# normal map." Mean slope in degrees: below the floor the map is accidentally flat,
# above the ceiling it is a slope field no tangent-space normal can represent without
# shading artefacts. Rock sits high in the band; polished metal sits low.
TEXTURE_NORMAL_MEAN_SLOPE_MIN_DEG = 2.5
TEXTURE_NORMAL_MEAN_SLOPE_MAX_DEG = 38.0

# "no inverted green channel" is a test of SIGN, and it must be measured as one. The first
# implementation used Pearson correlation between the decoded green channel and the
# height field's -dh/dV, requiring r > 0.95. That measures LINEARITY, which normal encoding
# does not have and is not required to have: green is ``-dh/dV / sqrt(1 + |grad|^2)``, so it
# saturates as slope grows. Measured on one family at two lanes, same code and same
# convention: r = 0.984 at 512 and r = 0.908 at 2048, failing purely because the finer band
# carries steeper slopes. A gate whose result depends on the quality lane is not measuring
# the property it names.
#
# Sign agreement is exact and lane-independent, because ``length`` is strictly positive so
# the signs of green and -dh/dV must match identically. Pixels with a near-zero slope are
# excluded: there the sign is decided by 8-bit quantisation, not by the convention.
TEXTURE_NORMAL_GREEN_SIGN_AGREEMENT_MIN = 0.99
TEXTURE_NORMAL_SIGN_SLOPE_FLOOR = 0.02

# "MRAO channel independence; channels cannot be identical unless manifest proves why."
# Pearson |r| between the DATA-CARRYING channels of one packed map. A channel pair above
# this is one map stored twice.
TEXTURE_CHANNEL_CORRELATION_MAX = 0.85

# "Metallic mask matches only real exposed metal or ore." Coverage ceiling for a
# non-metal family, plus the fraction of metallic signal that must fall inside the
# family's own declared ore/inclusion mask.
TEXTURE_METALLIC_COVERAGE_MAX = 0.12
TEXTURE_METALLIC_INSIDE_ORE_MASK_MIN = 0.90

# "Roughness variation supports material identity." A constant grey roughness field is
# explicitly rejected by section 3 unless the material is uniform.
TEXTURE_ROUGHNESS_STD_MIN = 0.030

# "AO is cavity-biased, not random dirt across exposed planes." This is the gate that
# separates a real occlusion integral from a noise field: the channel must track measured
# concavity, and it must anti-track height.
#
# SIGN CORRECTED 2026-07-29 on measurement, and the first spelling of these two rows was
# backwards. They originally read ``..._CONCAVITY_CORRELATION_MIN = 0.30`` and
# ``..._HEIGHT_CORRELATION_MAX = -0.10``, which is the correct test for an occlusion
# STRENGTH field where 1.0 means fully occluded. The channel that actually ships is not
# that. ``Hecton_ModuleHardSurfaceLit`` :349-353 decodes G as
# ``occlusionMap = lerp(1.0, packedMask.g, weight)``, i.e. an occlusion MULTIPLIER where
# 1.0 means fully OPEN and 0.0 means fully dark. So in a cavity the stored value is LOW,
# the correlation with concavity is NEGATIVE, and a generator that satisfied the old rows
# would have shipped an inverted AO map that brightened every crevice.
#
# Measured on the first geology bake: concavity -0.896, height +0.601. Both correct for a
# multiplier, both would have FAILED the old rows. This is the same class of confusion the
# bible amendment warns about in ``3DMODEL_TEXTURES_MATERIALS.md`` section 3 -- knowing
# which slot AO lives in is not the same as knowing which direction it runs.
TEXTURE_AO_CONCAVITY_CORRELATION_MAX = -0.30
TEXTURE_AO_HEIGHT_CORRELATION_MIN = 0.10

# "Emission mask is sparse and semantically placed." Coverage ceiling when the family
# has emission at all.
TEXTURE_EMISSION_COVERAGE_MAX = 0.06

# "Compression preview does not destroy key details on compact lane." Peak
# signal-to-noise ratio, in dB, of a simulated block-compressed round trip at the
# compact-lane size. 30 dB is the conventional floor for "visually equivalent" on
# texture data.
TEXTURE_COMPRESSION_PSNR_MIN_DB = 30.0

# "Mip preview does not create dark seams, ringing, or unreadable hazard/detail
# decals." Two numbers: mean-luminance drift between adjacent mip levels (a dark seam
# shows up as a level that loses energy), and the seam ratio must hold at every level,
# not only at mip 0.
TEXTURE_MIP_LUMA_DRIFT_MAX = 0.06


# ---------------------------------------------------------------------------
# Where a finished package is allowed to land
# ---------------------------------------------------------------------------
# WHY THIS EXISTS, and it is not a convenience. Every FBX the forge has ever
# produced landed under ``Docs/AgentLogs/Forge*``. ``.gitignore:201`` ignores
# ``Docs/AgentLogs/`` wholesale, ``git ls-files`` finds zero tracked FBX there, and
# none of it is inside ``Assets/``. So the output was outside Unity, outside git,
# and one ``git clean`` from gone. Generators were reporting measured LOD chains
# and channel statistics for assets that existed nowhere the game could reach.
#
# ``export_fbx`` was RIGHT to refuse to default a path: ``AGENTS.md`` ``Project
# Shape`` forbids inventing folders "without local source proof and
# justification". The path did not need inventing, only finding. Proof, all local:
#
#   * ``HectonFBXPostprocessor.cs:16`` declares
#     ``ProjectArtRoot = "Assets/_Project/Art"``, first in ``ManagedFbxRoots``.
#   * ``Assets/_Project/Art/Generated/`` is the established home for generated
#     content -- ``Generated/Flora/BioForge`` and ``Generated/ProductFace/Tools``
#     both already exist on disk.
#   * "Forge" is not a new prefix. It is this pipeline's name in the tool tree
#     (``Tools/Blender/h8forge``) and in the postprocessor's own constants
#     ``ForgeManifestSchema``, ``ForgeMeshFilePrefix``, ``ForgeFbxExtension``.
#
# THE IMPORT CARVE-OUT IS ALREADY BUILT AND HAS NEVER FIRED.
# ``HectonFBXPostprocessor`` recognises ``h8forge.manifest/1`` (``:43``) and the
# ``MESH_`` prefix (``:50``), and at ``:401-429`` sets ``importNormals =
# ModelImporterNormals.Import`` for a forge asset -- preserving the weighted split
# normals ``mesh_ops.apply_shading_basis`` bakes, which ``Calculate`` would
# re-derive from a single angle and throw away. At ``:181-196`` a forge asset
# arriving without an LODGroup is LOGGED against its manifest path instead of being
# silently decimated into extra LODs. Both banks of the bridge were finished and
# nothing ever crossed it.
#
# ``export_unity.py``'s own ``knownProjectConflicts`` still warns that the
# postprocessor "forces importNormals=Calculate for every FBX under
# Assets/_Project/Art". True before the carve-out, false now -- and the most
# plausible reason nobody moved the path.
#
# TWO HARD REQUIREMENTS from ``TryResolveForgeManifestPath`` (``:702-736``), both
# already satisfied by ``NAME_MESH``/``NAME_MANIFEST``, which is why the directory
# was the only thing wrong:
#   1. the file name starts with ``MESH_``, ordinal and CASE-SENSITIVE;
#   2. the manifest is a SIBLING in the same directory, named ``MANIFEST_<stem>``.
# ``Assets/ScifiFacility`` is excluded by that same function at ``:715``:
# third-party quarantine can never earn the carve-out, and a manifest dropped
# beside a vendor FBX must not be able to weaken it.
UNITY_ASSET_ROOT = "Assets/_Project"
FORGE_PACKAGE_ROOT = UNITY_ASSET_ROOT + "/Art/Generated/Forge"


def forge_package_dir(family) -> str:
    """Project-relative directory a finished package belongs in, per family.

    Forward-slashed and never absolute: ``AGENTS.md`` ``[RULE] Relative Path
    Requirement`` bans hardcoded developer paths in any durable artefact, and a
    manifest records this path.

    Writing here means Unity imports on next focus. That is the point, and it is
    also why a generator must not write here while another owner holds the editor
    for a batch run -- ``AGENTS.md`` ``Unity And Build Gates`` allows one owner at a
    time, and an import storm is the interference it forbids.
    """
    resolved = family if isinstance(family, Family) else Family(family)
    return "{root}/{family}".format(root=FORGE_PACKAGE_ROOT, family=resolved.value)


FORGE_PROOF_ROOT = "Docs/AgentLogs/Forge"


def forge_proof_dir(family) -> str:
    """Project-relative directory for PROOF artefacts, which is NOT the package dir.

    THE PACKAGE AND THE PROOF ARE DIFFERENT THINGS AND THEY GO TO DIFFERENT PLACES.
    A package is the FBX plus its sibling manifest: Unity must import those, so they
    belong under ``Assets`` and ``forge_package_dir`` puts them there. A proof
    artefact is a contact sheet, a silhouette mask, a channel tile - diagnostic
    evidence for a human, which Unity has no business importing.

    Learned by breaking it. When ``forge_package_dir`` landed, ``rock.py`` used one
    directory for both, so a single boulder run dropped 20+ PNGs into the asset
    database - every one of which Unity would import as a texture, with a ``.meta``,
    a GUID and VRAM cost, for a diagnostic picture. Measured: 29 files in the Forge
    tree after two runs, of which 2 were the actual package.

    ``Docs/AgentLogs`` is gitignored (``.gitignore:201``), which is exactly right for
    proof and exactly wrong for a package - the same property that made it the wrong
    home for the FBX makes it the right home for a render.
    """
    resolved = family if isinstance(family, Family) else Family(family)
    return "{root}{family}".format(root=FORGE_PROOF_ROOT, family=resolved.value)


def forge_texture_dir(family) -> str:
    """PRODUCTION directory for a generated ``TX_*`` family. Gated, not default.

    Under ``forge_package_dir``'s tree so one family's mesh, manifest and maps stay
    together and the FBX postprocessor carve-out documented above still applies to the
    mesh half.

    ``3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`` section 9 is explicit about when a
    generator is allowed to write here: "If any gate fails, the texture family must not
    be saved into the production asset route." So a bake writes to
    :func:`forge_texture_proof_dir` FIRST, runs the eleven gates, and only a clean sweep
    earns this path. A caller that writes here before gating has skipped the gate, not
    passed it.
    """
    return "{dir}/Textures".format(dir=forge_package_dir(family))


def forge_texture_proof_dir(family) -> str:
    """Gitignored diagnostic tree for texture bakes, per playbook section 9.

    "The bake may write a diagnostic artifact under ``Docs/AgentLogs`` or an editor-only
    quarantine folder, but it must not become a referenced runtime material."

    Deliberately NOT the same directory as :func:`forge_proof_dir`. That one is the mesh
    lane's proof tree and is in active use by the mesh generators; a texture bake
    dropping 5-10 maps plus a lighting sweep into it would interleave two agents' output
    in one folder and make ``clear_render_dir``'s staleness rule ambiguous across lanes.
    """
    resolved = family if isinstance(family, Family) else Family(family)
    return "{root}{family}Texture".format(root=FORGE_PROOF_ROOT, family=resolved.value)


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
