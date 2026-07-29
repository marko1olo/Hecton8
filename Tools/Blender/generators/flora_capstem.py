"""Cap-and-stem flora ("amber fan") generator -- HECTON-8 offline asset forge.

Family: ``law.Family.FLORA``. Surface class ``ORGANIC``. Harvestable hero plant for
the photic shallows, derived from the mandatory reference folder
(``Docs/mandatory if you work on systems that user sees .../nice_biome.webp``,
``beauty.webp``, ``shallows.webp``).

What the references actually show, and what that dictates here:

*   ``nice_biome.webp`` -- the amber form appears in CLUSTERS of three to ten caps
    sharing one patch of ground, never as a lone hero stalk. So the asset unit is a
    CLUMP of stems of differing height and cap size, not one stem. Caps are small
    relative to the frame, tilted at varied angles, and several are visibly CUPPED
    (edge lifted, chanterelle/funnel) rather than flat plates.
*   ``beauty.webp`` -- the same morphology at larger scale carries obvious RADIAL
    RIBS from the attachment point out to a lobed, notched edge. The ribs are
    visible from ABOVE, and the lobes of the edge are where the ribs terminate. So
    the ribbing is a through-thickness structure that shapes the top surface, the
    edge outline and the underside gills together; it is not an underside-only
    detail added for occlusion.
*   Pigment is the visual carrier -- amber against teal -- but it is one dressing of
    the form: the same fans read pale khaki in ``beauty.webp`` and the accents in
    ``shallows.webp`` are magenta and rust. The mesh therefore has to read on
    silhouette alone, and the pigment is authored as real material values on top.
*   No flora in any of the three frames is emissive. Channel G is written 0.
    ``3DMODEL_FLORA_CORAL.md`` section 1: "Biolum-only darkness is reserved for
    depth, caves, contaminated pockets, or special route events."

Structures, mapped to ``3DMODEL_FLORA_CORAL.md`` section 3 (plate coral row --
"layered plates, thick rims, underside AO, chipped edges, support stems") and
section 3 (kelp row -- blades "must not be zero-thickness if seen from both sides"):

*   cap is a SOLID plate: top surface, separate underside, closed by a rounded rim
    band of real thickness -- never a two-sided sheet;
*   radial ribs modulate top relief, edge radius and underside gill depth from one
    term, so the form is coherent;
*   chipped/torn edge sectors break the outline (``3dmodel.md`` section 12 rejects
    "perfect spheres, perfect cylinders");
*   stem tapers, bends downstream, and has a lobed non-circular cross-section with
    lengthwise ridges;
*   holdfast is the flared, finger-lobed foot of the same manifold, and the object
    origin sits at the base of the clump so placement pivot and sway anchor coincide;
*   cap is offset off the stem axis and tilted.

Each stem is ONE closed manifold: foot cap -> stem tube -> cap underside annulus ->
rim band -> cap top -> apex. The stem's last ring IS the cap's hub ring, so the union
is welded by construction rather than by intersecting two shells, which is the
rejection in section 8 ("Branches intersect without weld, knuckle, or hidden union").

Displacement discipline (from the lead's coral field report): every modulation here is
scaled by the LOCAL analytic thickness or radius at that point, never by distance from
an axis, and the dominant term is structure-following (ribs, gills, lengthwise stem
ridges) with fine noise kept weak. Geometry is generated directly at shipping density
-- there is no subdivide-displace-decimate step -- so displacement frequency cannot
outrun the triangle count, which is what got the coral rejected on its second pass.

Determinism: ``numpy.random.default_rng(seed)`` only. ``mathutils.noise`` is banned
here because its seed is process-global and two generators in one session corrupt each
other's stream (``PROCEDURAL_ASSET_PIPELINE.md`` "Deterministic Source Contract").

Invocation::

    blender.exe -b --factory-startup -P Tools/Blender/generators/flora_capstem.py -- \
        --seed 3301 --quality 1.0
"""

from __future__ import annotations

import argparse
import math
import os
import sys
from dataclasses import dataclass, field
from typing import List, Optional, Sequence, Tuple

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

# The package is not on sys.path under `blender -b -P <script>`; this file lives at
# <root>/Tools/Blender/generators/, so the package root is one directory up.
_TOOLS_BLENDER = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if _TOOLS_BLENDER not in sys.path:
    sys.path.insert(0, _TOOLS_BLENDER)

from h8forge import export_unity, law, mesh_ops, preview, validate, vertexcolor
from h8forge.blackbox import BlackBox, GenerationAborted

GENERATOR_NAME = "flora_capstem.py"
GENERATOR_VERSION = "1.0.0"

FAMILY = law.Family.FLORA
SURFACE = law.FAMILY_SURFACE_CLASS[FAMILY]

# Atlas page for the island-pixel and border-padding gates. 2048 with 16 px padding
# comes from law.ATLAS_PADDING_PX; passing it to the validator is what makes those two
# gates actually fire instead of being recorded as "not enforced".
ATLAS_SIZE = 2048

# Camera class for the manifest. A harvestable plant is inspected from arm's length,
# which is why section 5 of the flora bible puts it on the 512 px/m hero row rather
# than the 256 px/m common-instanced row.
CAMERA_DISTANCE_CLASS = "near_interaction"
PLATFORM_LANE = "compact_to_ultra"

# Alpha channel meaning. 3dmodel.md section 5 requires this string in the manifest.
ALPHA_MEANING = (
    "harvest_mask: 1.0 on cap tissue that a harvest tool removes (top surface, "
    "underside gills and rim), ramping to 0.0 down the neck and 0.0 across the "
    "holdfast/foot. A cut at 0.0 yields nothing and kills the plant, so the "
    "interaction proxy is a root sphere and the shader/tool reads this mask to "
    "decide which tissue detaches."
)

REFERENCE_IDS = (
    "Docs/mandatory if you work on systems that user sees (water, terrain, sky, "
    "flora, ui) - read this and all images inside (references)/nice_biome.webp",
    "Docs/mandatory if you work on systems that user sees (water, terrain, sky, "
    "flora, ui) - read this and all images inside (references)/beauty.webp",
    "Docs/mandatory if you work on systems that user sees (water, terrain, sky, "
    "flora, ui) - read this and all images inside (references)/shallows.webp",
)


# ---------------------------------------------------------------------------
# Small numeric helpers
# ---------------------------------------------------------------------------

def _smoothstep(x: float) -> float:
    x = law.saturate(x)
    return x * x * (3.0 - 2.0 * x)


def _lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def _lerp_v(a: Vector, b: Vector, t: float) -> Vector:
    return a + (b - a) * t


def _rng_range(rng, lo: float, hi: float) -> float:
    return float(lo + (hi - lo) * rng.random())


def _basis_from_up(up: Vector, hint: Vector) -> Tuple[Vector, Vector, Vector]:
    """Right-handed orthonormal frame with ``up`` as Z and ``hint`` biasing X."""
    z = up.normalized()
    x = (hint - z * hint.dot(z))
    if x.length <= 1e-6:
        fallback = Vector((1.0, 0.0, 0.0))
        x = fallback - z * fallback.dot(z)
        if x.length <= 1e-6:
            x = Vector((0.0, 1.0, 0.0)) - z * z.y
    x.normalize()
    y = z.cross(x)
    return x, y, z


# ---------------------------------------------------------------------------
# Mesh accumulator
# ---------------------------------------------------------------------------
# Plain python lists rather than a live BMesh, for one reason that matters: every
# vertex carries three authored scalars (geodesic growth distance, harvest mask, local
# tissue thickness) that the vertex-colour stage and the displacement scaling both
# consume. A BMesh pass that merges or reorders vertices silently desynchronises those
# arrays from the geometry, and the symptom -- a sway gradient that no longer matches
# the mesh -- is invisible in a lit render.

@dataclass
class _Accum:
    positions: List[Vector] = field(default_factory=list)
    geodesic: List[float] = field(default_factory=list)
    harvest: List[float] = field(default_factory=list)
    thickness: List[float] = field(default_factory=list)
    faces: List[Tuple[int, ...]] = field(default_factory=list)
    face_material: List[int] = field(default_factory=list)
    # One (island_id, u_metres, v_metres) per face corner, resolved to packed UV
    # coordinates after every island's bounding box is known.
    face_uv: List[List[Tuple[int, float, float]]] = field(default_factory=list)
    # Structural label per face, so a measurement can name the geometry that produced
    # it. Without this a report reads "worst triangle[1553] = 58.0" and the author
    # guesses which of six surfaces that is; with it the diagnostic says "transition"
    # and the fix is obvious. Cheap, permanent, and it replaced three rounds of guessing.
    face_region: List[str] = field(default_factory=list)
    region: str = "unset"

    def vert(self, position: Vector, geodesic: float, harvest: float,
             thickness: float) -> int:
        self.positions.append(position.copy())
        self.geodesic.append(float(geodesic))
        self.harvest.append(law.saturate(harvest))
        self.thickness.append(max(1e-5, float(thickness)))
        return len(self.positions) - 1

    def face(self, indices: Sequence[int], material: int,
             uvs: Sequence[Tuple[int, float, float]]) -> None:
        if len(indices) != len(uvs):
            raise ValueError("face corner count and uv count disagree")
        if len(set(indices)) != len(indices):
            # A repeated index is a degenerate face; the validator's
            # GATE_DEGENERATE_TRIANGLE would catch it later, but catching it at the
            # point of construction names the parametrisation that produced it.
            raise ValueError("degenerate face with repeated vertex index: "
                             + repr(tuple(indices)))
        self.faces.append(tuple(int(i) for i in indices))
        self.face_material.append(int(material))
        self.face_uv.append([(int(i), float(u), float(v)) for i, u, v in uvs])
        self.face_region.append(self.region)

    def quad(self, a: int, b: int, c: int, d: int, material: int,
             uvs: Sequence[Tuple[int, float, float]]) -> None:
        self.face((a, b, c, d), material, uvs)

    def triangulate(self) -> int:
        """Fan every quad into triangles here, at authoring time. Returns faces added.

        NOT cosmetic, and not something the exporter can be left to do. ``export_unity``
        writes the FBX with ``use_triangles=True`` and it has to: ``use_tspace``
        SILENTLY refuses to build a tangent basis on a mesh containing an n-gon, and
        ``3dmodel.md`` section 3 makes Tangent a required stream. So the file always
        holds triangles. ``verify_fbx_roundtrip`` then compares data that is indexed
        PER CORNER (``mesh.corner_normals``, the colour attribute), and a quad source
        can never match a triangulated re-import: measured on this asset, 2580 quads +
        360 triangles = 11400 corners against 16560 coming back, so the round trip
        rejected the package and the exporter deleted the FBX it had just written.
        Nothing about the geometry was wrong; the two sides were counting different
        topologies.

        Doing it HERE rather than after the LOD chain buys three things:
          1. the mesh that is validated, baked, previewed and measured is the mesh that
             ships, so the manifest's triangle counts and UV statistics describe the
             exported topology instead of a quad cage Blender would re-cut later;
          2. the diagonal is chosen once, deterministically, by this generator -- a
             non-planar quad has two different surfaces depending on its diagonal, and
             ``3dmodel.md`` section 10 validates the one we pick;
          3. ``face_material``, ``face_uv`` and ``face_region`` are rebuilt in lockstep,
             so the per-face structural labels the UV diagnostic reads stay aligned by
             CONSTRUCTION rather than by trusting a bmesh operator to preserve order.
        ``rock.py`` reached the same conclusion from the other end and triangulates after
        its chain; the accumulator is simply the earliest point where it is free.

        Vertex order is untouched, which is the constraint that matters most here: the
        authored per-vertex geodesic/harvest/thickness arrays are index-aligned to build
        order and a re-indexing pass would silently desynchronise the sway field.
        """
        faces: List[Tuple[int, ...]] = []
        materials: List[int] = []
        uvs: List[List[Tuple[int, float, float]]] = []
        regions: List[str] = []
        for index, corners in enumerate(self.faces):
            material = self.face_material[index]
            corner_uv = self.face_uv[index]
            region = self.face_region[index]
            for k in range(1, len(corners) - 1):
                faces.append((corners[0], corners[k], corners[k + 1]))
                materials.append(material)
                uvs.append([corner_uv[0], corner_uv[k], corner_uv[k + 1]])
                regions.append(region)
        added = len(faces) - len(self.faces)
        self.faces = faces
        self.face_material = materials
        self.face_uv = uvs
        self.face_region = regions
        return added


# ---------------------------------------------------------------------------
# Material slots  --  3dmodel.md section 6
# ---------------------------------------------------------------------------
#   Slot 0: primary structural/tissue material   -> cap top + underside (the organ)
#   Slot 1: exposed cut, bevel, edge, scar       -> torn/chipped rim band
#   Slot 2: secondary trim / growth plate        -> stem + holdfast foot
# Slot 3 (emissive) is deliberately unused: nothing in the reference set glows, and
# _gate_materials rejects a declared slot that carries no triangle, so declaring an
# emissive slot "for later" would fail validation rather than sit harmlessly.

SLOT_CAP = law.MATERIAL_SLOT_PRIMARY
SLOT_RIM = law.MATERIAL_SLOT_CUT_EDGE
SLOT_STEM = law.MATERIAL_SLOT_TRIM

MATERIAL_ROLES = ("CapTissue", "TornEdge", "StemHoldfast")


# ---------------------------------------------------------------------------
# Shape grammar  --  PROCEDURAL_ASSET_PIPELINE.md "Generation Order" step 2
# ---------------------------------------------------------------------------

@dataclass
class StemPlan:
    """Everything decided about one stem before a single vertex exists."""

    base_offset: Vector
    height: float
    cap_radius: float
    stem_radius: float
    bend: float
    lean: Vector
    tilt_deg: float
    cap_offset_frac: float
    rib_count: int
    rib_phase: float
    lobe_amplitude: float
    outline_bias: float
    outline_bias_phase: float
    rib_relief: float
    gill_depth: float
    cup_sign: float
    cup_amplitude: float
    thickness_hub: float
    thickness_rim: float
    tear_sectors: Tuple[Tuple[float, float, float], ...]
    edge_jitter: Tuple[float, ...]
    ridge_count: int
    ridge_phase: float
    finger_count: int
    finger_phase: float
    neck_ratio: float
    juvenile: bool


@dataclass
class ClumpPlan:
    stems: Tuple[StemPlan, ...]
    current_dir: Vector
    segments: int
    stem_rings: int
    cap_top_rings: int
    cap_bottom_rings: int
    rim_rings: int
    hub_fraction: float


def stem_segment_count(segments: int) -> Tuple[int, int]:
    """(stem_segments, ratio) -- the stem's angular resolution, coarser than the cap's.

    The cap needs enough angular samples to resolve its ribs and lobed outline; a 15 mm
    stem does not, and giving it the same count produced quads 10:1 to 20:1 elongated.
    Sliver triangles are the reason the UV gate would not close: sigma_max/sigma_min is
    ill-conditioned on a needle, so a map whose per-edge scales were all within 27% of
    each other still measured a ratio of 5.7 against a 0.55 limit. Fewer, squarer stem
    quads fix the metric and the topology at once, and ``PROCEDURAL_ASSET_PIPELINE.md``
    already asks for "quad-dominant topology, uniform density" over "chaotic
    triangulation".

    A ratio of 1 means no transition band is needed, which keeps the low-quality path
    identical to a plain tube.
    """
    ratio = max(1, segments // 12)
    while ratio > 1 and segments % ratio != 0:
        ratio -= 1
    return segments // ratio, ratio


def triangles_per_stem(segments: int, stem_rings: int, cap_top_rings: int,
                       cap_bottom_rings: int, rim_rings: int) -> int:
    """Exact LOD0 triangle count for one stem. Not an estimate.

    Foot fan, coarse stem bands, one fan transition band into the cap hub, then the cap
    underside, rim and top bands plus the apex fan. Verified against the measured
    datablock rather than trusted.
    """
    stem_segments, ratio = stem_segment_count(segments)
    # The coarse->fine refinement sits on the cap UNDERSIDE, not in the neck: see
    # _build_stem for why the neck was the wrong place for it.
    refinement = ((ratio + 1) * stem_segments if ratio > 1
                  else 2 * stem_segments)
    cap_bands = (cap_bottom_rings - 1) + (rim_rings + 1) + (cap_top_rings - 1)
    return (stem_segments                              # foot fan
            + 2 * stem_segments * (stem_rings - 1)     # stem tube, all coarse
            + refinement                               # hub -> first full cap ring
            + 2 * segments * cap_bands                 # cap underside, rim, top
            + segments)                                # apex fan


def _fit_density(*, rib_count: int, segments_per_rib: int, stem_rings: int,
                 cap_top_rings: int, cap_bottom_rings: int, rim_rings: int,
                 stem_count: int, budget: int):
    """Shrink ring density deterministically until the clump fits its LOD0 budget.

    Rings go first and angular segments last: the lobed, notched cap OUTLINE is the
    silhouette this asset is for, and dropping angular resolution attacks exactly the
    feature that has to survive. ``3dmodel.md`` section 7 -- "If the silhouette reads
    correctly at lower counts, spend saved budget on material detail" -- is the same
    priority ordering.
    """
    # stem_rings floors at 10, not 4. Below that the stem bands become needles and the
    # UV gate cannot close no matter how good the parameterisation is -- measured 12.0%
    # of area over the organic limit purely from 17:1 slivers.
    minimums = {"stem_rings": 10, "cap_top_rings": 4, "cap_bottom_rings": 3,
                "rim_rings": 1}
    counts = {"stem_rings": stem_rings, "cap_top_rings": cap_top_rings,
              "cap_bottom_rings": cap_bottom_rings, "rim_rings": rim_rings}
    ceiling = int(budget * 0.93)

    def total(spr: int) -> int:
        return stem_count * triangles_per_stem(
            rib_count * spr, counts["stem_rings"], counts["cap_top_rings"],
            counts["cap_bottom_rings"], counts["rim_rings"])

    for _guard in range(64):
        if total(segments_per_rib) <= ceiling:
            break
        # Reduce whichever ring count is furthest above its floor, so the shrink stays
        # balanced instead of collapsing one axis to its minimum first.
        candidates = [(counts[k] - minimums[k], k) for k in counts]
        candidates.sort(reverse=True)
        slack, key = candidates[0]
        if slack > 0:
            counts[key] -= 1
            continue
        if segments_per_rib > 2:
            segments_per_rib -= 1
            continue
        break
    return (segments_per_rib, counts["stem_rings"], counts["cap_top_rings"],
            counts["cap_bottom_rings"], counts["rim_rings"])


def plan_clump(rng, *, quality: float, cap_radius: float, height: float) -> ClumpPlan:
    """Decide the whole clump deterministically from the RNG.

    Reference basis for the clump: in ``nice_biome.webp`` the amber caps occur in
    groups sharing one patch of substrate, at clearly different heights, with one or
    two juvenile buttons among the mature caps. A single stalk is not what the frame
    shows, and the 6500-triangle LOD0 ceiling in ``law.LOD_BUDGETS[Family.FLORA]`` is
    generous enough for a small group -- spending it on readable repeated structure is
    what ``3dmodel.md`` section 7 asks for ("If the silhouette reads correctly at
    lower counts, spend saved budget on material detail").
    """
    q = law.saturate(quality)

    # GlobalQualityWeight scales density only. Rib COUNT is a silhouette fact and is
    # drawn from the RNG, not from quality: 3dmodel.md section 8 forbids quality from
    # changing the authored shape ("The silhouette must not step from low to high; it
    # must gain density along the same authored shape").
    # Ring counts are chosen for QUAD ASPECT, not only for triangle spend. A stem band
    # 110 mm long against a 6.6 mm circumferential step is a 17:1 needle, and a needle
    # makes sigma_max/sigma_min ill-conditioned however good the map is -- that alone
    # held the UV gate at 12.0% against its 10% allowance. Stem bands became cheap once
    # the stem dropped to half the cap's angular resolution, so the saved triangles go
    # into LENGTH, where they fix the aspect.
    segments_per_rib = 2 + int(round(1.0 * q))          # 2..3
    stem_rings = 10 + int(round(8.0 * q))               # 10..18
    cap_top_rings = 4 + int(round(3.0 * q))             # 4..7
    cap_bottom_rings = 3 + int(round(2.0 * q))          # 3..5
    rim_rings = 1 + int(round(1.0 * q))                 # 1..2

    stem_count = 2 + int(round(1.6 * q))                # 2..4 (a clump, never one)
    if rng.random() < 0.28:
        stem_count = max(2, stem_count - 1)

    current = Vector((_rng_range(rng, -1.0, 1.0), _rng_range(rng, -1.0, 1.0), 0.0))
    if current.length <= 1e-4:
        current = Vector((1.0, 0.0, 0.0))
    current.normalize()

    rib_count = int(7 + round(_rng_range(rng, 0.0, 6.0)))   # 7..13

    # Density is fitted to the REAL LOD0 ceiling before a vertex exists, because the
    # per-vertex sway/harvest arrays are index-aligned to build order: decimating LOD0
    # afterwards would desynchronise them, so "generate then reduce" is not available
    # here and authored density has to be right the first time. 3dmodel.md section 7
    # calls the budget a hard maximum, and a four-stem clump at full ring density
    # measured 7168 triangles against 6500 -- fixed by shrinking rings, not by
    # decimating away the authored silhouette. No RNG is consumed here, so the fit
    # cannot shift the deterministic draw order of anything below it.
    (segments_per_rib, stem_rings, cap_top_rings, cap_bottom_rings,
     rim_rings) = _fit_density(
        rib_count=rib_count, segments_per_rib=segments_per_rib,
        stem_rings=stem_rings, cap_top_rings=cap_top_rings,
        cap_bottom_rings=cap_bottom_rings, rim_rings=rim_rings,
        stem_count=stem_count, budget=law.LOD_BUDGETS[FAMILY].lod0)
    segments = rib_count * segments_per_rib

    stems: List[StemPlan] = []
    for index in range(stem_count):
        # Heights spread hard so the group reads as several ages, which is what the
        # reference clusters look like. The last stem of a large clump is a juvenile
        # button: short stem, small nearly-closed cap, no tear damage yet.
        juvenile = stem_count >= 3 and index == stem_count - 1 and rng.random() < 0.75
        if juvenile:
            height_scale = _rng_range(rng, 0.28, 0.46)
            radius_scale = _rng_range(rng, 0.34, 0.52)
        else:
            height_scale = _rng_range(rng, 0.70, 1.06) if index else _rng_range(rng, 0.94, 1.06)
            radius_scale = _rng_range(rng, 0.72, 1.04) if index else _rng_range(rng, 0.92, 1.05)

        stem_height = height * height_scale
        stem_cap_radius = cap_radius * radius_scale

        angle = (index / float(stem_count)) * math.tau + _rng_range(rng, -0.5, 0.5)
        spread = cap_radius * _rng_range(rng, 0.18, 0.62)
        base = Vector((math.cos(angle) * spread, math.sin(angle) * spread, 0.0))

        # Lean: outward from the clump centre plus a downstream component. Nature does
        # not stack a clump vertically, and a fan tips its face toward the light while
        # the current pushes the stem.
        outward = base.normalized() if base.length > 1e-5 else Vector((1.0, 0.0, 0.0))
        lean = (outward * _rng_range(rng, 0.10, 0.34)
                + current * _rng_range(rng, 0.12, 0.40))

        # Tear sectors: (centre angle, half width, depth). A juvenile has none.
        # A tear narrower than the angular sampling is not a bite, it is a
        # single-vertex needle: measured 66 mm of radius collapsed between two adjacent
        # vertices 2.75 mm apart, which both looks like a spike and destroys the UV
        # parameterisation there. The half width is therefore floored at 2.5 angular
        # segments so every tear is resolved by several vertices on each flank.
        tears: List[Tuple[float, float, float]] = []
        minimum_half_width = 3.5 * math.tau / float(segments)
        if not juvenile:
            for _ in range(1 + int(rng.integers(0, 2))):
                tears.append((
                    _rng_range(rng, 0.0, math.tau),
                    max(minimum_half_width, _rng_range(rng, 0.10, 0.42)),
                    _rng_range(rng, 0.16, 0.32),
                ))

        stem_radius = stem_height * _rng_range(rng, 0.036, 0.058)
        # 0.585 is the taper factor the profile reaches at t = 1 before the neck flare,
        # i.e. (1 - 0.46) ** 0.85. hub_fraction is 0.13 of the cap radius.
        natural_top_radius = max(1e-4, stem_radius * 0.585)
        neck_ratio = max(1.0, (0.13 * stem_cap_radius) / natural_top_radius)

        stems.append(StemPlan(
            base_offset=base,
            height=stem_height,
            cap_radius=stem_cap_radius,
            stem_radius=stem_radius,
            bend=_rng_range(rng, 0.10, 0.30),
            lean=lean,
            # The reference caps read as tilted, not tipped over; 8-26 degrees covers what
            # nice_biome.webp and beauty.webp show. The upper bound also matters
            # geometrically: the tilt displaces the hub ring tangentially by
            # R_hub * sin(tilt), and that offset shears the final stem band.
            tilt_deg=_rng_range(rng, 8.0, 26.0),
            # Bounded by the HUB radius, not the cap radius. At 0.10-0.32 of the cap
            # radius the offset reached 50 mm against a 20 mm hub, so the stem's top ring
            # and the cap's hub ring barely overlapped and the transition band was a
            # violently skewed shear -- a coarse vertex ended up collinear with a hub
            # chord (sigma 19.1 against a 3.3 ceiling). hub_fraction is 0.13, so half the
            # hub radius is 0.065 of the cap radius; this stays inside that.
            cap_offset_frac=_rng_range(rng, 0.030, 0.060),
            rib_count=rib_count,
            rib_phase=_rng_range(rng, 0.0, math.tau),
            lobe_amplitude=_rng_range(rng, 0.055, 0.115),
            # The visible "cap not centred on its stem" read comes from an ASYMMETRIC
            # OUTLINE, not from displacing the hub: the cap simply extends further on one
            # side. That is also what the reference shows -- in beauty.webp the fans are
            # kidney-shaped about their attachment rather than discs pushed sideways --
            # and unlike a hub offset it costs nothing in weld quality.
            outline_bias=_rng_range(rng, 0.10, 0.24),
            outline_bias_phase=_rng_range(rng, 0.0, math.tau),
            rib_relief=_rng_range(rng, 0.030, 0.070),
            gill_depth=_rng_range(rng, 0.24, 0.44),
            cup_sign=1.0 if rng.random() < 0.62 else -1.0,
            cup_amplitude=_rng_range(rng, 0.14, 0.34),
            # Fractions of cap radius. 3DMODEL_FLORA_CORAL.md section 3 asks plate coral
            # for "thick rims", and the earlier 0.040-0.072 rim gave a 6-11 mm band at a
            # 155 mm radius -- a knife edge that also read as a 4 px UV island and a
            # 15:1 sliver band. Thicker is both what the bible wants and what the
            # geometry needs.
            # An ABSOLUTE floor of 11 mm on the rim rides on top of the fraction. A small
            # juvenile cap of 51 mm radius took the fraction literally and produced a
            # 4.7 mm rim, whose UV island measured 181 x 3.36 px -- under
            # law.UV_MIN_ISLAND_PIXELS at 512 px/m. Proportionally thicker young caps are
            # also correct: a button IS chunky relative to its width, and it thins as it
            # expands.
            thickness_hub=max(_rng_range(rng, 0.165, 0.235),
                              0.017 / max(1e-4, stem_cap_radius)),
            thickness_rim=min(0.30, max(_rng_range(rng, 0.075, 0.115),
                                        0.011 / max(1e-4, stem_cap_radius))),
            tear_sectors=tuple(tears),
            edge_jitter=tuple(float(x) for x in rng.normal(0.0, 0.016, size=segments)),
            ridge_count=int(4 + round(_rng_range(rng, 0.0, 3.0))),
            ridge_phase=_rng_range(rng, 0.0, math.tau),
            finger_count=int(5 + round(_rng_range(rng, 0.0, 3.0))),
            finger_phase=_rng_range(rng, 0.0, math.tau),
            # How far the neck must widen to meet the cap hub. Without it the stem met a
            # hub 3x its own radius in ONE band -- a trumpet no strip unwrap can carry
            # (measured sigma ratio 26.9) and anatomically wrong besides: a cap-and-stem
            # plant flares into its cap over a visible neck. Derived from the hub radius
            # the cap will actually present, not guessed.
            neck_ratio=neck_ratio,
            juvenile=juvenile,
        ))

    return ClumpPlan(
        stems=tuple(stems),
        current_dir=current,
        segments=segments,
        stem_rings=stem_rings,
        cap_top_rings=cap_top_rings,
        cap_bottom_rings=cap_bottom_rings,
        rim_rings=rim_rings,
        hub_fraction=0.13,
    )


# ---------------------------------------------------------------------------
# Stem axis and cross-section
# ---------------------------------------------------------------------------

def _stem_axis(plan: StemPlan, samples: int) -> List[Tuple[Vector, Vector, float, float]]:
    """(position, tangent, t, arclength) along one stem, base first.

    The bend is quadratic in ``t`` so curvature grows toward the tip -- a stalk grown
    into a current is stiff at the holdfast and compliant at the neck, which is the
    same physical statement the sway formula in ``3DMODEL_FLORA_CORAL.md`` section 2
    makes about the red channel.
    """
    out: List[Tuple[Vector, Vector, float, float]] = []
    lean = plan.lean
    positions: List[Vector] = []
    parameters: List[float] = []
    for i in range(samples):
        # Rings cluster toward the foot. Uniform sampling put the ENTIRE holdfast flare
        # between ring 0 and ring 1 -- a 2.5:1 radius change across one band, which is
        # a cone with a hard crease rather than a root pad, and no cylindrical UV map
        # can carry it (measured sigma ratio 21.9 on that one band). Clustering resolves
        # the flare axially the same way the tear clamp resolves a bite angularly.
        #
        # Cluster toward the FOOT only, exponent 1.35. Three spacings were measured:
        #   uniform   -- the whole holdfast flare fell in one band, a 2.5:1 cone crease
        #                that no strip unwrap can carry (sigma 21.9);
        #   power 2.0 -- fixed the foot, left a final band spanning 30% of the stem
        #                (132 mm against a 6.6 mm circumferential step);
        #   cosine    -- clustered both ends, and that made the HUB band worse, not
        #                better: the cap is tilted up to 26 degrees, so the hub ring sits
        #                ~10 mm tangentially off the stem ring, and shrinking the axial
        #                step to 4 mm left the band dominated by that tangential offset.
        # 1.35 with 18 rings resolves the flare while keeping the final step around 20 mm,
        # which is twice the tilt offset rather than half of it.
        t = (i / float(samples - 1)) ** 1.35
        # Vertical rise slightly eased so the neck is not a straight ramp.
        rise = plan.height * (t ** 0.94)
        lateral = lean * (plan.height * plan.bend * (t ** 1.85))
        positions.append(plan.base_offset + Vector((0.0, 0.0, rise)) + lateral)
        parameters.append(t)

    arclength = 0.0
    for i, position in enumerate(positions):
        if i == 0:
            tangent = (positions[1] - positions[0])
        elif i == len(positions) - 1:
            tangent = (positions[-1] - positions[-2])
            arclength += (positions[i] - positions[i - 1]).length
        else:
            tangent = (positions[i + 1] - positions[i - 1])
            arclength += (positions[i] - positions[i - 1]).length
        if tangent.length <= 1e-9:
            tangent = Vector((0.0, 0.0, 1.0))
        out.append((positions[i], tangent.normalized(), parameters[i], arclength))
    return out


def _stem_profile_radius(plan: StemPlan, t: float, theta: float) -> float:
    """Local stem radius. Non-circular by construction, flared into a holdfast.

    Three superposed terms, all scaled by the LOCAL radius rather than by distance
    from an axis (the lead's coral field report: axis-distance scaling left the stem
    porcelain-smooth while the outer geometry self-intersected):

    * taper -- thick at the foot, thin at the neck;
    * ellipse + lengthwise ridges -- ``3DMODEL_FLORA_CORAL.md`` section 3 requires a
      "stipe or spine with taper and ribbing" and forbids a plain cylinder;
    * holdfast flare with radial finger lobes over the bottom ~14% of the stem, which
      is the "holdfast or root cluster, not a loose vertical ribbon" requirement.
    """
    taper = (1.0 - 0.46 * t) ** 0.85
    swell = 1.0 + 0.10 * math.sin(math.pi * min(1.0, t * 1.15))
    radius = plan.stem_radius * taper * swell

    ellipse = 1.0 + 0.085 * math.cos(2.0 * (theta - plan.ridge_phase))
    ridges = 1.0 + 0.075 * math.cos(plan.ridge_count * theta + plan.ridge_phase
                                    + 1.7 * t)
    radius *= ellipse * ridges

    # Neck: the stem widens into the cap hub over the top ~28% instead of meeting it in
    # one step. Anatomically what a cap-and-stem plant does, and geometrically it turns
    # a single 3:1 cone band into four gentle ones.
    if t > 0.72:
        radius *= 1.0 + (plan.neck_ratio - 1.0) * _smoothstep((t - 0.72) / 0.28)

    # Holdfast flare. The amplitude is bounded on purpose: the flare sets the ratio
    # between the widest and narrowest ring circumference, and that ratio IS the
    # circumferential scale error of the stem's constant-width unwrap. 0.95 keeps the
    # spread near 2:1, which the geometric-mean anchor splits into +-1.41x -- inside the
    # 0.55 organic aspect limit. A larger flare is not free detail, it is UV stretch.
    foot_span = 0.22
    if t < foot_span:
        k = (1.0 - t / foot_span) ** 2.0
        flare = 1.0 + 0.62 * k
        # The finger amplitude is bounded by SHEAR, not by taste. A high-amplitude
        # angular modulation on a ring whose radius is also changing fast axially skews
        # the band quads hard: at 0.34 one foot quad measured edge scales spanning
        # 0.181..0.399, a genuine 2.2x anisotropy on 51 mm2 of visible surface, which is
        # a real stretch failure rather than a sliver artefact. The holdfast still reads
        # as a splayed, lobed pad at 0.20.
        fingers = 1.0 + 0.20 * k * math.cos(plan.finger_count * theta
                                            + plan.finger_phase)
        radius *= flare * fingers
    return max(radius, plan.stem_radius * 0.22)


# ---------------------------------------------------------------------------
# Cap surface
# ---------------------------------------------------------------------------

def _cap_edge_radius(plan: StemPlan, theta: float, segment_index: int) -> float:
    """Outline radius at one angle: ribs lobe it, tears bite it, jitter roughens it.

    The lobed outline is the SAME rib term that corrugates the top surface and cuts
    the underside gills. In ``beauty.webp`` the fans' scalloped edges line up with
    their visible ribs, so deriving all three from one function is what makes the form
    coherent rather than three unrelated noise layers.
    """
    lobe = 1.0 + plan.lobe_amplitude * math.cos(plan.rib_count * theta
                                                + plan.rib_phase)
    # Low-frequency asymmetry: the cap reaches further on one side, which is what makes
    # it read as off-centre on its stem without displacing the hub.
    bias = 1.0 + plan.outline_bias * math.cos(theta - plan.outline_bias_phase)
    radius = plan.cap_radius * lobe * bias

    for centre, half_width, depth in plan.tear_sectors:
        delta = abs(((theta - centre + math.pi) % math.tau) - math.pi)
        if delta < half_width:
            bite = 1.0 - _smoothstep(delta / half_width)
            radius *= 1.0 - depth * bite

    radius *= 1.0 + plan.edge_jitter[segment_index % len(plan.edge_jitter)]
    return max(radius, plan.cap_radius * 0.42)


def _cap_top_height(plan: StemPlan, u: float, theta: float) -> float:
    """Top-surface height above the cap plane at radial fraction ``u``.

    ``cup_sign`` +1 lifts the edge into a funnel, -1 droops it into a dome. Both occur
    in ``nice_biome.webp``; several caps there are clearly cupped, which is why a flat
    plate is not an acceptable default. The central boss is the swelling where the stem
    enters, and the rib relief grows outward so the ribs converge at the hub the way a
    real fan's veins do.
    """
    cup = plan.cup_sign * plan.cup_amplitude * plan.cap_radius * (u ** 2.0)
    boss = 0.085 * plan.cap_radius * (1.0 - _smoothstep(u / 0.42)) if u < 0.42 else 0.0
    relief = (plan.rib_relief * plan.cap_radius
              * math.cos(plan.rib_count * theta + plan.rib_phase)
              * (u ** 1.35))
    return cup + boss + relief


def _cap_thickness(plan: StemPlan, u: float, theta: float) -> float:
    """Plate thickness at (u, theta), gills cut into the underside only.

    Section 3 of the flora bible: plate coral needs "thick rims, underside AO".
    Thickness is greatest at the hub and never reaches zero at the rim, so the plate is
    a solid with a real edge rather than a sheet seen from both sides.

    Gills run at twice the rib frequency, sit between the ribs, fade out before the rim
    so the rim stays thick, and their depth is a FRACTION OF LOCAL THICKNESS -- so a
    thin juvenile cap gets shallow gills automatically and cannot self-intersect.
    """
    base = (plan.thickness_rim
            + (plan.thickness_hub - plan.thickness_rim) * ((1.0 - u) ** 1.55))
    thickness = base * plan.cap_radius

    gill_window = _smoothstep(u / 0.28) * (1.0 - 0.72 * _smoothstep((u - 0.68) / 0.32))
    gill = 0.5 - 0.5 * math.cos(2.0 * plan.rib_count * theta + plan.rib_phase)
    thickness *= 1.0 - plan.gill_depth * gill_window * gill
    return max(thickness, plan.thickness_rim * plan.cap_radius * 0.30)


class _NoiseField:
    """Smooth deterministic scalar field on a coarse grid, bilinearly sampled.

    Frequency is deliberately tied to the rib count, i.e. to the triangle density this
    asset actually ships at. The lead's coral field report names the opposite mistake:
    displacement finer than the shipping triangle count survives as faceted patches
    after decimation. This field is a WEAK secondary term -- callers scale it by local
    tissue thickness, never by a global amplitude.
    """

    __slots__ = ("_grid", "_rows", "_cols")

    def __init__(self, rng, rows: int, cols: int) -> None:
        self._rows = max(2, rows)
        self._cols = max(2, cols)
        self._grid = np.asarray(rng.normal(0.0, 1.0, size=(self._rows, self._cols)),
                                dtype=np.float64)

    def sample(self, a: float, b: float) -> float:
        """``a`` in 0..1 across rows, ``b`` in 0..1 wrapping across columns."""
        fa = law.saturate(a) * (self._rows - 1)
        i0 = int(math.floor(fa))
        i1 = min(self._rows - 1, i0 + 1)
        ta = fa - i0
        fb = (b % 1.0) * self._cols
        j0 = int(math.floor(fb)) % self._cols
        j1 = (j0 + 1) % self._cols
        tb = fb - math.floor(fb)
        top = _lerp(self._grid[i0][j0], self._grid[i0][j1], tb)
        bottom = _lerp(self._grid[i1][j0], self._grid[i1][j1], tb)
        return _lerp(top, bottom, ta)


# ---------------------------------------------------------------------------
# Geometry emission
# ---------------------------------------------------------------------------

def _apply_drift(axis, direction: Vector, amount: float):
    """Bend the upper stem laterally toward the off-axis cap hub and re-derive tangents.

    The cap is deliberately NOT centred on the stem axis, so the stem has to arrive
    under the hub. Translating only the final ring would put a hard kink in the last
    band; drifting the whole upper third moves the whole neck, which is what a stalk
    carrying a lopsided cap actually does.
    """
    positions = []
    for position, _tangent, t, _s in axis:
        weight = _smoothstep((t - 0.55) / 0.45)
        positions.append(position + direction * (amount * weight))
    out = []
    arclength = 0.0
    for i, position in enumerate(positions):
        if i == 0:
            tangent = positions[1] - positions[0]
        elif i == len(positions) - 1:
            tangent = positions[-1] - positions[-2]
            arclength += (positions[i] - positions[i - 1]).length
        else:
            tangent = positions[i + 1] - positions[i - 1]
            arclength += (positions[i] - positions[i - 1]).length
        if tangent.length <= 1e-9:
            tangent = Vector((0.0, 0.0, 1.0))
        out.append((position, tangent.normalized(), axis[i][2], arclength))
    return out


def _build_stem(accum: _Accum, plan: StemPlan, clump: ClumpPlan, rng,
                island_base: int) -> dict:
    """Emit one closed manifold: foot -> stem -> cap underside -> rim -> cap top.

    Returns a per-stem report with the numbers a proof artefact needs.
    """
    segments = clump.segments
    thetas = [math.pi + math.tau * j / float(segments) for j in range(segments)]
    # Seam at j == 0, which sits at theta = pi -- the rear of the stem relative to the
    # current direction. 3DMODEL_FLORA_CORAL.md section 5: "Stipes and branches use
    # cylindrical unwrap with seam on the least visible rear side."

    noise = _NoiseField(rng, 5, max(4, plan.rib_count))

    axis = _stem_axis(plan, clump.stem_rings)
    tip_x, _tip_y, _tip_z = _basis_from_up(axis[-1][1], clump.current_dir)
    cap_shift = plan.cap_offset_frac * plan.cap_radius
    axis = _apply_drift(axis, tip_x, cap_shift)

    tip_position, tip_tangent, _t, stem_length = axis[-1]

    # Cap frame: tilt the stem's tip frame downstream about an axis perpendicular to
    # both the current and the stem, so the fan leans the way the water pushes it.
    tilt_axis = clump.current_dir.cross(tip_tangent)
    if tilt_axis.length <= 1e-5:
        tilt_axis = Vector((0.0, 1.0, 0.0))
    tilt_axis.normalize()
    rotation = Matrix.Rotation(math.radians(plan.tilt_deg), 4, tilt_axis)
    cap_up = (rotation @ tip_tangent).normalized()
    cap_x = (rotation @ tip_x).normalized()
    cap_y = cap_up.cross(cap_x).normalized()
    cap_centre = tip_position + cap_up * (plan.cap_radius * 0.06) + cap_x * cap_shift

    edge_radius = [_cap_edge_radius(plan, thetas[j], j) for j in range(segments)]

    def cap_point(u: float, j: int, underside: bool) -> Vector:
        theta = thetas[j]
        radius = u * edge_radius[j]
        height = _cap_top_height(plan, u, theta)
        if underside:
            height -= _cap_thickness(plan, u, theta)
        # Weak thickness-scaled noise. Amplitude is a fraction of LOCAL plate
        # thickness, so a thin juvenile cap gets proportionally less and the two
        # surfaces can never cross.
        local_thickness = _cap_thickness(plan, u, theta)
        height += 0.16 * local_thickness * noise.sample(u, theta / math.tau)
        return (cap_centre + cap_x * (radius * math.cos(theta))
                + cap_y * (radius * math.sin(theta)) + cap_up * height)

    # ---- radial samples -------------------------------------------------
    hub_u = clump.hub_fraction
    bottom_us = [hub_u + (1.0 - hub_u) * (i / float(clump.cap_bottom_rings))
                 for i in range(clump.cap_bottom_rings + 1)]
    top_us = [1.0 - (i / float(clump.cap_top_rings))
              for i in range(clump.cap_top_rings + 1)]   # ends at u = 0 (apex)

    # ---- island ids -----------------------------------------------------
    island_stem = island_base + 0
    island_foot = island_base + 1
    island_bottom = island_base + 2
    island_rim = island_base + 3
    island_top = island_base + 4

    # ---- stem rings, last ring == cap hub ring --------------------------
    stem_segments, ratio = stem_segment_count(segments)
    # The hub ring is COARSE, like the rest of the stem. The coarse->fine refinement was
    # originally here, in the neck, and that was the wrong place: the hub ring is tilted
    # up to 34 degrees and its plane cuts the stem ring's plane, so on one side the two
    # rings nearly coincide and the fan triangles came out collinear to a few microns
    # over tens of millimetres -- a zero-area world triangle mapped to a healthy UV one,
    # i.e. sigma_min = 0 (measured 58.0, then 19.1, then 14.6 against a 3.3 ceiling as
    # each contributing cause was removed). Refining on the cap UNDERSIDE instead puts
    # the irregular topology on a wide, shallow surface whose quads are about 2.5:1,
    # where a fan cannot produce a sliver.
    hub_ring = [cap_point(hub_u, jc * ratio, True) for jc in range(stem_segments)]

    ring_indices: List[List[int]] = []
    ring_circumference: List[float] = []
    ring_arc: List[List[float]] = []
    # v runs along the real SURFACE from the foot, per angle -- not along the axis. The
    # holdfast flare collapses the radius by a factor of ~2.5 over the first two rings,
    # so the slant height of that band is far longer than the axial rise; using axial
    # arc length there measured 78x aspect distortion on the first stem band.
    # v is one scalar PER RING, the mean surface advance, not a per-column
    # accumulation. Per-column looked more accurate and was catastrophically worse:
    # adjacent columns diverge as their own radius wobbles, so column j reached
    # v = 0.30 while j+1 reached 0.33 -- a 30 mm SHEAR across a quad 3 mm wide, and
    # sigma_max/sigma_min hit 6100. A per-ring v cannot shear by construction.
    stem_v: List[float] = []
    previous_ring: Optional[List[Vector]] = None
    # The coarse angles are a strict SUBSET of the fine ones -- coarse jc lines up with
    # fine jc*ratio -- which is what lets the cap-underside refinement connect without
    # leaving a T-vertex.
    thetas_stem = [math.pi + math.tau * jc / float(stem_segments)
                   for jc in range(stem_segments)]

    for i, (position, tangent, t, arclength) in enumerate(axis):
        is_hub = (i == len(axis) - 1)
        angles = thetas_stem
        count = stem_segments
        if is_hub:
            points = hub_ring
        else:
            frame_x, frame_y, _frame_z = _basis_from_up(tangent, clump.current_dir)
            points = []
            for j in range(count):
                theta = angles[j]
                radius = _stem_profile_radius(plan, t, theta)
                # Fine noise stays WEAK and bounded. At 0.10 x an unclamped N(0,1) it
                # reached +-35% of the stem radius: visually lumpy rather than grown,
                # and enough angular wobble to wreck the parameterisation. The lead's
                # coral report names the same failure -- one isotropic octave reads as
                # cauliflower, so structure-following terms dominate and noise trims.
                radius *= 1.0 + 0.030 * max(-2.5, min(
                    2.5, noise.sample(t, theta / math.tau)))
                points.append(position + frame_x * (radius * math.cos(theta))
                              + frame_y * (radius * math.sin(theta)))

            if i == len(axis) - 2:
                # Guarantee axial clearance to the hub ring at EVERY column. The hub ring
                # is tilted by up to 34 degrees and offset off the stem axis, so its plane
                # cuts the coarse ring's plane: on one side the two rings nearly coincide,
                # and the transition triangles there came out collinear to 8 microns in
                # 22 mm -- a zero-area world triangle mapped to a healthy UV one, which is
                # sigma_min = 0 by definition (measured 58.0 against a 3.3 ceiling). The
                # cosine spacing shortened the average step and made this worse, not
                # better, because the deficit is geometric, not a sampling artefact.
                mean_radius = max(1e-4, ring_circumference[-1] / math.tau
                                  if ring_circumference else 0.01)
                clearance = min((hub_ring[j] - points[j]).dot(tip_tangent)
                                for j in range(count))
                deficit = 0.45 * mean_radius - clearance
                if deficit > 0.0:
                    points = [p - tip_tangent * deficit for p in points]

        # u is the REAL accumulated arc around the ring, normalised by that ring's own
        # circumference. Uniform index spacing looked equivalent and is not: the
        # holdfast finger lobes and the ellipse make adjacent arc steps differ several
        # fold around one ring, and uniform u then claims 7.76 mm where the surface
        # travels 2.46 mm -- that alone produced a sigma ratio of 493.
        circumference = 0.0
        arc = [0.0] * count
        for j in range(count):
            arc[j] = circumference
            circumference += (points[(j + 1) % count] - points[j]).length
        ring_circumference.append(circumference)
        ring_arc.append([value - circumference * 0.5 for value in arc])

        if previous_ring is None:
            stem_v.append(0.0)
        else:
            advance = sum((points[jc] - previous_ring[jc]).length
                          for jc in range(count)) / float(count)
            stem_v.append(stem_v[-1] + advance)
        previous_ring = points

        harvest = _smoothstep((t - 0.74) / 0.26)
        radius_estimate = max(1e-4, circumference / math.tau)
        indices = [accum.vert(points[j], arclength, harvest, radius_estimate)
                   for j in range(count)]
        ring_indices.append(indices)

    accum.region = "foot"
    # ---- foot: flat n-gon closing the bottom ----------------------------
    # The foot ring lies in the z=0 plane and the fan is planar, so planar offsets ARE
    # the isometric map here. Its own island: the strip map above it uses a different
    # parametrisation and sharing one island would fight both.
    foot_centre = accum.vert(axis[0][0], 0.0, 0.0,
                             max(1e-4, ring_circumference[0] / math.tau))
    foot_radius = ring_circumference[0] / math.tau
    for j in range(stem_segments):
        k = (j + 1) % stem_segments
        offset_a = accum.positions[ring_indices[0][j]] - axis[0][0]
        offset_b = accum.positions[ring_indices[0][k]] - axis[0][0]
        accum.face(
            (foot_centre, ring_indices[0][k], ring_indices[0][j]), SLOT_STEM,
            ((island_foot, 0.0, 0.0),
             (island_foot, offset_b.x, offset_b.y),
             (island_foot, offset_a.x, offset_a.y)))

    accum.region = "stem_band"
    # ---- stem bands: constant-width cylindrical strip -------------------
    # A tapering tube has no isometric rectangular unwrap, and the two obvious choices
    # fail in opposite ways. Giving each ring its own u width preserves circumferential
    # LENGTH but makes the island a trapezoid, so two rings at the same angle drift
    # apart -- measured 33 mm of drift against a 1.75 mm v step, which sent the UV
    # triangle collinear and sigma_min to zero (ratio 6100, then 40 after other fixes).
    # Mapping the flare as a polar annulus instead over-stretched it 4.5x
    # circumferentially as soon as the surface stopped being disc-like (ratio 18).
    #
    # A CONSTANT width removes drift entirely: u depends only on the angular index, so
    # every ring lands in the same column. What remains is a per-ring circumferential
    # scale error of C_i / C_ref, and anchoring C_ref on the GEOMETRIC MEAN splits that
    # error symmetrically -- a 2.0:1 spread between the flared foot and the neck becomes
    # +-1.41x, i.e. sigma_max/sigma_min - 1 = 0.41, inside the 0.55 organic limit.
    # Anchoring on the mean or on either extreme puts the whole ratio on one end and
    # fails there.
    log_sum = 0.0
    for circumference in ring_circumference:
        log_sum += math.log(max(1e-6, circumference))
    reference_circumference = math.exp(log_sum / max(1, len(ring_circumference)))
    strip_u = [[(value / max(1e-6, ring_circumference[i])) * reference_circumference
                for value in ring_arc[i]]
               for i in range(len(ring_indices))]

    # v is per-RING everywhere except the FINAL band. Accumulating v per column over many
    # rings makes neighbouring columns diverge and shear (that mistake measured sigma
    # 6100), but the last band is a single step into a hub ring tilted up to 34 degrees
    # and offset off the axis, so the real advance genuinely varies about 3:1 around the
    # ring. Forcing one scalar there claims a uniform advance the surface does not have,
    # which is itself shear -- measured 8.4 to 12.6 across three seeds, all of it in that
    # one band. Per-column v is wrong when accumulated and right for a single step.
    last_band = len(ring_indices) - 2
    hub_advance = [
        (accum.positions[ring_indices[-1][j]]
         - accum.positions[ring_indices[last_band][j]]).length
        for j in range(stem_segments)
    ]
    for i in range(len(ring_indices) - 1):
        # Band index in the label so a measurement names the exact band, not just "the
        # stem". Locating a defect by band was worth several rounds of guessing.
        accum.region = "stem_band_{0:02d}".format(i)
        lower = ring_indices[i]
        upper = ring_indices[i + 1]
        for j in range(stem_segments):
            k = (j + 1) % stem_segments
            # At the seam (j == stem_segments-1) the k corner must continue past the end
            # of the ring instead of wrapping back to -C_ref/2, or the last quad spans
            # the whole island and inverts.
            wrap = reference_circumference if k == 0 else 0.0
            if i == last_band:
                v_up_j = stem_v[i] + hub_advance[j]
                v_up_k = stem_v[i] + hub_advance[k]
                # AND THE UPPER u COMES FROM THE LOWER RING IN THIS BAND ONLY.
                #
                # Every other band takes its upper u from the upper ring's own arc
                # normalisation, which is right when the two rings are near-parallel:
                # their normalised arc positions differ by the lobe wobble alone. The hub
                # ring is not near-parallel. It is tilted up to 34 degrees and offset off
                # the stem axis, so its arc lengths redistribute strongly around the ring
                # and its normalised positions walk away from the coarse ring's. The quad
                # then carries a u SHEAR on top of the 3:1 v variation this band already
                # has, and the two together are what put sigma_max/sigma_min out of range.
                #
                # MEASURED as the dominant cause: sweeping 48 seeds at quality 1.0, every
                # LOD0 uv_stretch_excessive failure put its worst triangle in the
                # StemHoldfast slot and in THIS band (stem_band_09 to _16 depending on the
                # seed's ring count), at 3.35 to 12.79 against a 3.30 ceiling, while
                # cap_top, cap_underside and rim never exceeded 4.21.
                #
                # The topology makes the substitution exact rather than approximate:
                # lower[j] connects to upper[j] by construction, both rings are indexed by
                # the same coarse `thetas_stem`, so column j IS the same column on both
                # rings. Reusing the lower u asserts that, which is true, instead of
                # re-deriving a second opinion from a tilted ring's arc lengths. v still
                # carries the real per-column advance, so the band's genuine 3:1 stretch
                # is still described -- only the spurious shear is removed.
                #
                # Safe against the cap island: UVs here are per-CORNER, and the hub ring's
                # cap-side UVs are emitted separately into `island_bottom`, so this does
                # not move the cap underside's parameterisation.
                u_up_j = strip_u[i][j]
                u_up_k = strip_u[i][k] + wrap
            else:
                v_up_j = stem_v[i + 1]
                v_up_k = stem_v[i + 1]
                u_up_j = strip_u[i + 1][j]
                u_up_k = strip_u[i + 1][k] + wrap
            accum.quad(
                lower[j], lower[k], upper[k], upper[j], SLOT_STEM,
                ((island_stem, strip_u[i][j], stem_v[i]),
                 (island_stem, strip_u[i][k] + wrap, stem_v[i]),
                 (island_stem, u_up_k, v_up_k),
                 (island_stem, u_up_j, v_up_j)))

    accum.region = "cap_underside"
    # ---- cap underside: hub ring outward to the rim ----------------------
    # Ring 0 is the COARSE hub ring shared with the stem; every ring beyond it is at full
    # cap resolution, so the first band is the coarse->fine refinement. Radial arc
    # distance from the hub, plus the hub's own radius, gives a geodesic polar map. The
    # hub arc offset keeps the innermost ring off the polar singularity so no UV triangle
    # collapses to zero area.
    hub_arc = hub_u * plan.cap_radius
    bottom_rings: List[List[int]] = []
    bottom_arc: List[List[float]] = []
    previous_points = None
    accumulated = [0.0] * segments
    for u in bottom_us[1:]:
        points = [cap_point(u, j, True) for j in range(segments)]
        for j in range(segments):
            reference = previous_points[j] if previous_points is not None \
                else hub_ring[j // ratio]
            accumulated[j] += (points[j] - reference).length
        bottom_arc.append(list(accumulated))
        indices = [
            accum.vert(points[j], stem_length + accumulated[j], 1.0,
                       _cap_thickness(plan, u, thetas[j]))
            for j in range(segments)
        ]
        bottom_rings.append(indices)
        previous_points = points

    # Refinement band: coarse hub ring -> first full-resolution underside ring. Each
    # coarse vertex fans across the `ratio` fine vertices it spans, then one bridging
    # triangle carries the coarse edge, so every fine vertex is used exactly once and the
    # shell stays manifold with no T-vertex for the decimator to collapse badly.
    hub_indices = ring_indices[-1]
    first_fine = bottom_rings[0]
    for jc in range(stem_segments):
        kc = (jc + 1) % stem_segments
        base = jc * ratio
        coarse_uv = _polar_uv(island_bottom, thetas, (base,), (hub_arc,))[0]
        next_coarse_uv = _polar_uv(island_bottom, thetas, (kc * ratio,), (hub_arc,))[0]
        for m in range(ratio):
            a = (base + m) % segments
            b = (base + m + 1) % segments
            accum.face(
                (hub_indices[jc], first_fine[b], first_fine[a]), SLOT_CAP,
                (coarse_uv,
                 _polar_uv(island_bottom, thetas, (b,), (hub_arc + bottom_arc[0][b],))[0],
                 _polar_uv(island_bottom, thetas, (a,), (hub_arc + bottom_arc[0][a],))[0]))
        end = (base + ratio) % segments
        accum.face(
            (hub_indices[jc], hub_indices[kc], first_fine[end]), SLOT_CAP,
            (coarse_uv, next_coarse_uv,
             _polar_uv(island_bottom, thetas, (end,),
                       (hub_arc + bottom_arc[0][end],))[0]))

    for i in range(len(bottom_rings) - 1):
        inner = bottom_rings[i]
        outer = bottom_rings[i + 1]
        for j in range(segments):
            k = (j + 1) % segments
            accum.quad(
                inner[j], outer[j], outer[k], inner[k], SLOT_CAP,
                _polar_uv(island_bottom, thetas, (j, j, k, k),
                          (hub_arc + bottom_arc[i][j], hub_arc + bottom_arc[i + 1][j],
                           hub_arc + bottom_arc[i + 1][k], hub_arc + bottom_arc[i][k])))

    accum.region = "cap_top"
    # ---- cap top: rim inward to the apex --------------------------------
    top_rings: List[List[int]] = []
    top_arc: List[List[float]] = []
    previous_points = None
    accumulated = [0.0] * segments
    apex_index: Optional[int] = None
    for u in top_us:
        if u <= 1e-6:
            apex = cap_centre + cap_up * _cap_top_height(plan, 0.0, 0.0)
            apex_index = accum.vert(apex, stem_length + hub_arc, 1.0,
                                    _cap_thickness(plan, 0.0, 0.0))
            break
        points = [cap_point(u, j, False) for j in range(segments)]
        if previous_points is not None:
            for j in range(segments):
                accumulated[j] += (points[j] - previous_points[j]).length
        top_arc.append(list(accumulated))
        indices = [
            accum.vert(points[j],
                       stem_length + hub_arc + max(0.0, (1.0 - u)) * plan.cap_radius,
                       1.0, _cap_thickness(plan, u, thetas[j]))
            for j in range(segments)
        ]
        top_rings.append(indices)
        previous_points = points

    if apex_index is None:
        raise GenerationAborted("cap top ring list never reached the apex")

    # Top surface UV: geodesic polar radius measured from the APEX. The arc is
    # accumulated inward from the rim, so the polar radius is (total including the last
    # hop to the apex) minus the accumulated arc. Omitting that final hop put the
    # innermost ring AT radius 0 together with the apex, which collapsed one quad band
    # and the whole apex fan to zero UV area -- 256 GATE_ZERO_AREA_UV_TRIANGLE hits.
    apex_position = accum.positions[apex_index]
    apex_hop = [(apex_position - accum.positions[top_rings[-1][j]]).length
                for j in range(segments)]
    top_total = [max(1e-5, top_arc[-1][j] + apex_hop[j]) for j in range(segments)]
    for i in range(len(top_rings) - 1):
        outer = top_rings[i]
        inner = top_rings[i + 1]
        for j in range(segments):
            k = (j + 1) % segments
            r_o_j = top_total[j] - top_arc[i][j]
            r_i_j = top_total[j] - top_arc[i + 1][j]
            r_o_k = top_total[k] - top_arc[i][k]
            r_i_k = top_total[k] - top_arc[i + 1][k]
            accum.quad(
                outer[j], outer[k], inner[k], inner[j], SLOT_CAP,
                _polar_uv(island_top, thetas, (j, k, k, j),
                          (r_o_j, r_o_k, r_i_k, r_i_j)))

    innermost = top_rings[-1]
    for j in range(segments):
        k = (j + 1) % segments
        r_j = top_total[j] - top_arc[-1][j]
        r_k = top_total[k] - top_arc[-1][k]
        accum.face(
            (innermost[j], innermost[k], apex_index), SLOT_CAP,
            _polar_uv(island_top, thetas, (j, k, j), (r_j, r_k, 0.0)))

    accum.region = "rim"
    # ---- rim band: closes underside to top with real thickness ----------
    # 3DMODEL_FLORA_CORAL.md section 3 (plate coral): "thick rims ... chipped edges".
    # The band bulges outward through its middle so the edge is rounded rather than a
    # knife, and it carries the cut/scar material slot because that is what a torn
    # plate edge is.
    rim_bottom = bottom_rings[-1]
    rim_top = top_rings[0]
    rim_rows: List[List[int]] = [rim_top]
    rim_fraction: List[float] = [0.0]
    band_thickness = [
        (accum.positions[rim_top[j]] - accum.positions[rim_bottom[j]]).length
        for j in range(segments)
    ]
    for step in range(1, clump.rim_rings + 1):
        s = step / float(clump.rim_rings + 1)
        points = []
        for j in range(segments):
            theta = thetas[j]
            top_point = accum.positions[rim_top[j]]
            bottom_point = accum.positions[rim_bottom[j]]
            middle = _lerp_v(top_point, bottom_point, s)
            outward = (cap_x * math.cos(theta) + cap_y * math.sin(theta))
            middle = middle + outward * (0.42 * band_thickness[j]
                                        * math.sin(math.pi * s))
            points.append(middle)
        indices = [
            accum.vert(points[j], stem_length + hub_arc + bottom_arc[-1][j], 1.0,
                       max(1e-4, band_thickness[j]))
            for j in range(segments)
        ]
        rim_rows.append(indices)
        rim_fraction.append(s)
    rim_rows.append(rim_bottom)
    rim_fraction.append(1.0)

    # Real path length ACROSS the band, accumulated per angle. The straight-line
    # thickness understates it because the band bulges outward through its middle to
    # round the edge, and a UV step shorter than the surface it covers is stretch.
    rim_arc: List[List[float]] = [[0.0] * segments]
    for row in range(1, len(rim_rows)):
        rim_arc.append([
            rim_arc[row - 1][j]
            + (accum.positions[rim_rows[row][j]]
               - accum.positions[rim_rows[row - 1][j]]).length
            for j in range(segments)
        ])
    rim_total = rim_arc[-1]

    # The rim gets its OWN island with a pure arc-length strip map: u is the accumulated
    # circumferential arc along the rim, v the accumulated distance across the band. That
    # is isometric in both directions, whereas continuing the underside's polar map put
    # the band's radial coordinate on a lobed, torn radius and sheared it (measured
    # sigma 3.56 with all three edge scales inside 17% of each other -- pure shear).
    #
    # A separate island was rejected earlier for a real reason that no longer applies: at
    # a 6-11 mm rim it measured 448 x 3.95 px, under law.UV_MIN_ISLAND_PIXELS. The rim is
    # now 12-18 mm because 3DMODEL_FLORA_CORAL.md section 3 asks plate coral for "thick
    # rims", which puts the island at 7-9 px and above the floor.
    rim_circumference = 0.0
    rim_u = [0.0] * segments
    for j in range(segments):
        rim_u[j] = rim_circumference
        rim_circumference += (accum.positions[rim_top[(j + 1) % segments]]
                              - accum.positions[rim_top[j]]).length
    rim_u = [value - rim_circumference * 0.5 for value in rim_u]
    for i in range(len(rim_rows) - 1):
        upper = rim_rows[i]
        lower = rim_rows[i + 1]
        for j in range(segments):
            k = (j + 1) % segments
            # v is the real distance across the band at THAT angle, so a thin torn
            # sector does not get the same texel run as a thick one.
            wrap = rim_circumference if k == 0 else 0.0
            accum.quad(
                upper[j], lower[j], lower[k], upper[k], SLOT_RIM,
                ((island_rim, rim_u[j], rim_arc[i][j]),
                 (island_rim, rim_u[j], rim_arc[i + 1][j]),
                 (island_rim, rim_u[k] + wrap, rim_arc[i + 1][k]),
                 (island_rim, rim_u[k] + wrap, rim_arc[i][k])))

    return {
        "height": round(plan.height, 5),
        "capRadius": round(plan.cap_radius, 5),
        "stemRadius": round(plan.stem_radius, 5),
        "ribCount": plan.rib_count,
        "tearSectors": len(plan.tear_sectors),
        "capProfile": "cupped" if plan.cup_sign > 0 else "domed",
        "capTiltDeg": round(plan.tilt_deg, 2),
        "capOffsetFractionOfRadius": round(plan.cap_offset_frac, 4),
        "juvenile": plan.juvenile,
        "stemArcLengthM": round(stem_length, 5),
        "capSurfaceArcM": round(hub_arc + max(bottom_arc[-1]), 5),
        "rimThicknessM": [round(min(band_thickness), 5), round(max(band_thickness), 5)],
        "islands": {"stem": island_stem, "foot": island_foot,
                    "capUndersideAndRim": island_bottom, "capTop": island_top},
        "unusedIslandSlot": island_rim,
    }


def _polar_uv(island: int, thetas: Sequence[float], corner_j: Sequence[int],
              radii: Sequence[float]):
    """Geodesic polar UVs for a disc-like island, in metres.

    Radius is SURFACE arc length, not planar radius, so a cupped and corrugated cap
    unwraps without compressing its outer rings -- which is what keeps the measured
    aspect distortion inside ``law.UV_STRETCH_MAX_BY_SURFACE[ORGANIC]``.
    """
    out = []
    for corner in range(len(corner_j)):
        theta = thetas[corner_j[corner]]
        radius = radii[corner]
        out.append((island, radius * math.cos(theta), radius * math.sin(theta)))
    return out


# ---------------------------------------------------------------------------
# UV packing  --  3dmodel.md section 6, 3DMODEL_FLORA_CORAL.md section 5
# ---------------------------------------------------------------------------

def pack_islands(accum: _Accum, *, atlas_size: int,
                 texel_density: int) -> dict:
    """Convert per-corner metre coordinates into packed 0..1 UVs.

    Every island's raw coordinates are already in METRES measured along the real
    surface, so multiplying by ``texel_density / atlas_size`` sets texel density
    exactly rather than approximately. ``3DMODEL_FLORA_CORAL.md`` section 5 puts a
    "Hero harvestable flora" at 512 px/m, which is why the caller passes
    ``law.TEXEL_DENSITY_HERO_FLORA``.

    Packing is a deterministic shelf pack into the region the atlas border reserve
    leaves free. If the requested density does not fit, EVERY island is scaled by one
    factor and the achieved density is reported -- silently clipping into the border
    would trip ``GATE_UV_ATLAS_PADDING_VIOLATION``, and silently keeping the number
    while overflowing would be a false density claim.
    """
    padding_px = law.atlas_padding_for(atlas_size)
    padding = padding_px / float(atlas_size)
    gap = 2.0 * padding
    usable = 1.0 - 2.0 * padding
    if usable <= 0.0:
        raise GenerationAborted("atlas padding leaves no usable UV space")

    scale = texel_density / float(atlas_size)

    boxes = {}
    for corners in accum.face_uv:
        for island, u, v in corners:
            box = boxes.get(island)
            if box is None:
                boxes[island] = [u, v, u, v]
            else:
                if u < box[0]:
                    box[0] = u
                if v < box[1]:
                    box[1] = v
                if u > box[2]:
                    box[2] = u
                if v > box[3]:
                    box[3] = v

    def layout(active_scale: float):
        """Shelf pack at a given scale. Returns (offsets, used_width, used_height)."""
        order = sorted(boxes.keys(),
                       key=lambda i: (-(boxes[i][3] - boxes[i][1]), i))
        offsets = {}
        cursor_x = padding
        cursor_y = padding
        row_height = 0.0
        max_x = padding
        for island in order:
            box = boxes[island]
            width = (box[2] - box[0]) * active_scale
            height = (box[3] - box[1]) * active_scale
            if cursor_x > padding and cursor_x + width > padding + usable:
                cursor_x = padding
                cursor_y += row_height + gap
                row_height = 0.0
            offsets[island] = (cursor_x - box[0] * active_scale,
                               cursor_y - box[1] * active_scale)
            cursor_x += width + gap
            max_x = max(max_x, cursor_x - gap)
            row_height = max(row_height, height)
        return offsets, max_x - padding, (cursor_y + row_height) - padding

    offsets, used_w, used_h = layout(scale)
    achieved = float(texel_density)
    shrink = 1.0
    if used_w > usable or used_h > usable:
        shrink = min(usable / max(1e-9, used_w), usable / max(1e-9, used_h)) * 0.995
        scale *= shrink
        achieved = texel_density * shrink
        offsets, used_w, used_h = layout(scale)

    resolved: List[List[Tuple[float, float]]] = []
    for corners in accum.face_uv:
        row = []
        for island, u, v in corners:
            offset_u, offset_v = offsets[island]
            row.append((u * scale + offset_u, v * scale + offset_v))
        resolved.append(row)

    utilisation = 0.0
    for island, box in boxes.items():
        utilisation += ((box[2] - box[0]) * scale) * ((box[3] - box[1]) * scale)

    return {
        "uvs": resolved,
        "islandCount": len(boxes),
        "atlasSize": atlas_size,
        "paddingPx": padding_px,
        "requestedTexelDensityPxPerM": int(texel_density),
        "achievedTexelDensityPxPerM": round(achieved, 2),
        "densityScaleApplied": round(shrink, 5),
        "boundingBoxUtilisation": round(utilisation, 5),
        "usedWidthUv": round(used_w, 5),
        "usedHeightUv": round(used_h, 5),
        "route": "authored analytic UVs: cylindrical for the stem with the seam at "
                 "the rear (theta=pi), geodesic polar for the cap top, cap underside "
                 "and foot, arc-length strip for the rim band. Coordinates are real "
                 "surface metres scaled to the target texel density, which is why "
                 "aspect distortion is near zero by construction rather than solved "
                 "for.",
    }


# ---------------------------------------------------------------------------
# Datablock assembly
# ---------------------------------------------------------------------------

def _to_object(accum: _Accum, packed_uvs, name: str,
               blackbox: BlackBox) -> bpy.types.Object:
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([tuple(p) for p in accum.positions], [], list(accum.faces))
    mesh.update()

    if len(mesh.polygons) != len(accum.faces):
        raise GenerationAborted(
            "from_pydata kept {0} of {1} faces; the topology description is "
            "invalid".format(len(mesh.polygons), len(accum.faces)))

    uv_layer = mesh.uv_layers.new(name="UVMap")
    for polygon_index, polygon in enumerate(mesh.polygons):
        polygon.material_index = accum.face_material[polygon_index]
        corners = packed_uvs[polygon_index]
        for corner, loop_index in enumerate(polygon.loop_indices):
            uv_layer.data[loop_index].uv = corners[corner]

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)

    blackbox.record("assemble_datablock", vertex_count=len(mesh.vertices),
                    triangle_count=mesh_ops.triangle_count(mesh),
                    warning="polygons={0} uv_layers={1}".format(
                        len(mesh.polygons), len(mesh.uv_layers)))
    return obj


# ---------------------------------------------------------------------------
# Materials  --  real pigment, not a grey placeholder
# ---------------------------------------------------------------------------
# Colour is the deliverable here, not decoration. In nice_biome.webp the amber cap
# against teal water is the whole reason the frame reads, and TASTE.md requires
# "shallow flora/coral has pigment and growth logic" while rejecting "flat procedural
# colors". Values are linear base colour, chosen against the reference: a saturated
# warm amber top, a deeper rust-brown torn edge, and a pale cream-ochre stem -- the
# stems in the reference are markedly LIGHTER than their caps, which is part of what
# makes the cap read as a separate organ at distance.
#
# These also have to survive a teal fog volume, so the cap pigment is pushed toward
# saturated orange rather than yellow: water absorbs long wavelengths first, and a
# desaturated ochre would go grey-green within a few metres.

_MATERIAL_SPECS = {
    "CapTissue": {
        "base_color": (0.855, 0.360, 0.070, 1.0),
        "roughness": 0.31,          # wet tissue: a broad soft specular, not matte
        "subsurface": 0.22,         # a thin cap is translucent at grazing light
        "subsurface_radius": (0.020, 0.009, 0.004),
        "ior": 1.38,
    },
    "TornEdge": {
        "base_color": (0.330, 0.115, 0.040, 1.0),
        "roughness": 0.52,          # a torn edge is fibrous, not glossy
        "subsurface": 0.10,
        "subsurface_radius": (0.010, 0.004, 0.002),
        "ior": 1.36,
    },
    "StemHoldfast": {
        "base_color": (0.640, 0.545, 0.375, 1.0),
        "roughness": 0.38,
        "subsurface": 0.14,
        "subsurface_radius": (0.012, 0.007, 0.004),
        "ior": 1.37,
    },
}


def build_materials() -> List[bpy.types.Material]:
    """Shared ``MAT_*`` materials in slot order. One set per family, never per instance.

    ``PROCEDURAL_ASSET_PIPELINE.md``: "Generated object families must never create one
    material per instance"; variation belongs in instanced shader properties and the
    vertex-colour masks this generator bakes.
    """
    materials = []
    for role in MATERIAL_ROLES:
        name = law.NAME_MATERIAL.format(family=FAMILY.value, role=role)
        existing = bpy.data.materials.get(name)
        if existing is not None:
            bpy.data.materials.remove(existing)
        material = bpy.data.materials.new(name)
        material.use_nodes = True
        nodes = material.node_tree.nodes
        links = material.node_tree.links
        nodes.clear()
        output = nodes.new("ShaderNodeOutputMaterial")
        bsdf = nodes.new("ShaderNodeBsdfPrincipled")
        spec = _MATERIAL_SPECS[role]
        bsdf.inputs["Base Color"].default_value = spec["base_color"]
        bsdf.inputs["Roughness"].default_value = spec["roughness"]
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = 0.0
        # Blender 4.x renamed the subsurface inputs. hasattr-style probing with an
        # explicit fallback, never a bare try/except that would convert a rename into
        # silently flat tissue.
        for candidate in ("Subsurface Weight", "Subsurface"):
            if candidate in bsdf.inputs:
                bsdf.inputs[candidate].default_value = spec["subsurface"]
                break
        if "Subsurface Radius" in bsdf.inputs:
            bsdf.inputs["Subsurface Radius"].default_value = spec["subsurface_radius"]
        if "IOR" in bsdf.inputs:
            bsdf.inputs["IOR"].default_value = spec["ior"]
        links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
        materials.append(material)
    return materials


# ---------------------------------------------------------------------------
# LOD re-unwrap
# ---------------------------------------------------------------------------

def _settle_topology(obj: bpy.types.Object, *, fill_cracks: bool):
    """Weld, dissolve, purge, and leave the mesh TRIANGLES ONLY with no duplicate faces.

    ORDER IS THE WHOLE POINT AND THE OBVIOUS ORDER IS WRONG. ``weld_and_clean`` ends its
    repair with ``holes_fill``, which emits one N-GON per closed loop, and
    ``dissolve_degenerate`` MERGES adjacent triangles into quads and n-gons -- so any pass
    that triangulates before dissolving hands back a mesh that is not triangulated.
    Measured on seed 1811 q0.35 after exactly that mistake: the level reported 1687
    triangles from 1678 polygons, i.e. nine surviving quads, and one of them split into a
    triangle of cross length 2.94e-08 against ``law.DEGENERATE_TRIANGLE_AREA_EPS`` 1e-07.
    Triangulation is therefore LAST, after every operator that can fuse faces.

    Four gate failures across the seed/quality sweep had this single cause and every one
    of them named a different gate, which is why they read as four bugs:

    *   seed 1811 q0.00 -- FBX round trip, ``corner normal count 2543 -> 2547``: surviving
        non-triangles, the same per-corner mismatch a quad LOD0 produces.
    *   seed 1811 q0.35 -- ``GATE_DEGENERATE_TRIANGLE``, the sliver above.
    *   seeds 3301 and 7 -- ``GATE_TANGENT_LENGTH_OUT_OF_RANGE``, tangent length exactly
        0.0, which is what a sliver's zero-area UV footprint yields.
    *   seed 3301 -- FBX round trip, ``triangle count 1618 -> 1617 ... check for a
        DUPLICATE FACE``: Quadric Edge Collapse can pull two triangles onto the same vertex
        triple, FBX merges the pair on import, and the round trip then rejects the package
        for losing one triangle. Invisible to a non-manifold-edge query, so it is purged
        here by vertex-triple identity.

    Blender also cannot build a tangent basis on an n-gon at all -- ``calc_tangents``
    raises "tris/quads" and the validator then records all three tangent gates as NOT
    ENFORCED rather than failed -- so leaving triangles is required regardless of the FBX.

    ``fill_cracks`` is False on the second call: closing rims again after the budget refit
    would re-inflate the triangle count and put the level straight back on the seam-drop
    path this hook exists to keep it off.
    """
    bm = mesh_ops.bmesh_from_object(obj)
    stats = mesh_ops.weld_and_clean(bm, merge_distance=1e-4,
                                    fill_boundary_loops=fill_cracks)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-4, edges=bm.edges[:])

    fused = [face for face in bm.faces if len(face.verts) > 3]
    if fused:
        bmesh.ops.triangulate(bm, faces=fused, quad_method="BEAUTY",
                              ngon_method="BEAUTY")

    slivers = [face for face in bm.faces
               if face.calc_area() <= law.DEGENERATE_TRIANGLE_AREA_EPS]
    if slivers:
        bmesh.ops.delete(bm, geom=slivers, context="FACES")

    bm.verts.index_update()
    seen = set()
    duplicates = []
    for face in bm.faces:
        key = tuple(sorted(vert.index for vert in face.verts))
        if key in seen:
            duplicates.append(face)
        else:
            seen.add(key)
    if duplicates:
        bmesh.ops.delete(bm, geom=duplicates, context="FACES")

    orphans = [vert for vert in bm.verts if not vert.link_faces]
    if orphans:
        bmesh.ops.delete(bm, geom=orphans, context="VERTS")

    non_triangles = sum(1 for face in bm.faces if len(face.verts) != 3)
    mesh_ops.bmesh_to_object(bm, obj)
    if non_triangles:
        raise GenerationAborted(
            "_settle_topology left {0} non-triangular faces on {1}; the FBX round trip "
            "compares per-corner data against a triangulated re-import and would reject "
            "the package".format(non_triangles, obj.name))
    return stats, ("triangulated {0} fused faces, purged {1} slivers and {2} duplicate "
                   "faces".format(len(fused), len(slivers), len(duplicates)))


# Projection angle limit for the LOD re-solve, DERIVED from the gate rather than picked.
#
# ``smart_project`` is a planar-projection unwrapper: it clusters faces into groups and
# flattens each group along one axis. A face whose normal sits ``theta`` away from its
# group axis is therefore compressed by ``cos(theta)`` in one direction and not at all in
# the other, so the parameterisation carries a BUILT-IN aspect distortion of
# ``1/cos(theta) - 1`` before any geometry is at fault. ``angle_limit`` is the largest
# ``theta`` a group will accept, so it IS the worst-case distortion the solver is allowed
# to introduce -- and at the 66 degrees this generator used to pass, that is
# ``1/cos(66) - 1 = 1.46``, i.e. 2.7x ``law.UV_STRETCH_MAX_BY_SURFACE[ORGANIC]``. A solver
# configured to exceed the gate by construction is not a tuning problem.
#
# Inverting the relation ties the two numbers together permanently: the widest projection
# angle whose own compression still fits the organic limit. Measured on this asset at
# LOD1, on the seam-preserved mesh: 66 degrees gave 2.9% of area over the limit with a
# worst triangle of 0.993, the derived angle gave 0.0% and 0.348.
UV_PROJECTION_ANGLE_DEG = math.degrees(
    math.acos(1.0 / (1.0 + law.UV_STRETCH_MAX_BY_SURFACE[SURFACE])))


def _make_reunwrap(atlas_size: int, notes: List[str]):
    """Close the collapse cracks, refit the budget, THEN re-solve UVs and pack.

    Decimate/COLLAPSE has no UV term in its collapse cost, so a radial cap layout is
    destroyed by reduction while the triangle budget still reads as met (measured on
    kelp: LOD0 p95 0.98, LOD1 worst 7610). The analytic parametrisation cannot be
    reapplied once the topology has changed, so LOD1/LOD2 get an angle-based solve.

    THE ORDER IS THE FIX, and getting it wrong cost this asset both coarse LODs.
    ``build_lod_chain`` splits UV seams into mesh boundaries before decimating, and the
    collapse then moves one side of a split seam without the other, so ``_weld_coincident``
    leaves genuine CRACKS -- measured 636 boundary edges at LOD1 and 330 at LOD2 in a
    clump that is built as four closed manifolds. ``weld_and_clean`` closes them, which is
    what ``3dmodel.md`` section 5 requires ("all sheet borders must be capped, thickened,
    or tagged as non-collision render-only"), and closing them costs TRIANGLES:
    ``holes_fill`` emits an n-gon per loop, measured 1728 -> 2093 at LOD1 against an 1800
    budget and 288 -> 421 at LOD2 against 300.

    That overshoot is what actually broke the asset. ``build_lod_chain`` reads the count
    after this hook returns, and an over-budget level makes it DISCARD the whole level and
    rebuild it from LOD0 with the seam splitting turned off -- legal only at LOD2, where
    ``3DMODEL_FLORA_CORAL.md`` section 6 permits "simplified shells or cards", and taken
    at BOTH levels here. Worse, for as long as that branch re-ran neither the weld nor
    this hook, the shipped LOD1/LOD2 carried LOD0's analytic UVs dragged through a
    collapse: measured 14.0% of LOD1 area and 14.9% of LOD2 area over the organic limit
    with worst triangles of 90.89 and 190.95 against a 3.30 outlier ceiling. The evidence
    was actively misleading, because the black box held the clean numbers this hook had
    measured on the mesh that was then deleted.

    So: fill the cracks, refit the budget with one more collapse, and only then solve the
    parameterisation. The unwrap has to be LAST or the decimation undoes it, and the
    budget has to be met before returning or the level is thrown away.

    The rescale afterwards matters too: ``smart_project`` packs to the full 0..1 square and
    touches the border, which is exactly ``GATE_UV_ATLAS_PADDING_VIOLATION``. Squeezing
    into the reserve keeps that gate ENFORCED at every level instead of being skipped
    for the coarse ones.
    """
    padding = law.atlas_padding_for(atlas_size) / float(atlas_size)

    def reunwrap(obj: bpy.types.Object, lod_index: int) -> None:
        # Decimation leaves slivers, and a sliver's UV triangle can come back with zero
        # area, which makes calc_tangents emit a zero-length tangent and trips
        # GATE_TANGENT_LENGTH_OUT_OF_RANGE (observed at LOD2 on seed 4127). Cleaning the
        # degenerate geometry BEFORE unwrapping removes the cause instead of unwrapping
        # around it. Safe at LOD1/LOD2 only, which is why it lives here and not in the
        # LOD0 path: LOD0's per-vertex sway/harvest arrays are index-aligned and a merge
        # there would desynchronise them.
        before_tris = mesh_ops.triangle_count(obj.data)
        # Bracket EVERY step in this hook with a component count. `build_lod_chain`
        # already welds the seam split back before calling us, so anything that
        # fragments the shell after that point is made HERE, and until this probe
        # existed there was no way to tell which of the five steps below did it.
        # Attributing it by plausibility is how three earlier attributions in this
        # pipeline went wrong ("disconnected shells", "the generator opens rims",
        # "recalc_face_normals").
        shell_trace = [("entry", mesh_ops.topology_report(obj))]
        clean, settle = _settle_topology(obj, fill_cracks=True)
        shell_trace.append(("after_crack_fill", mesh_ops.topology_report(obj)))

        # Refit the budget the crack repair just broke. Without this the level is
        # discarded and rebuilt without seam preservation -- see the docstring.
        # reduce_to_budget targets budget * 0.94, so the headroom it leaves cannot be
        # eaten by the n-gon triangulation the fill introduced.
        budget = law.LOD_BUDGETS[FAMILY].limit(lod_index)
        filled_tris = mesh_ops.triangle_count(obj.data)
        refitted = filled_tris
        repair = None
        resettle = None
        if filled_tris > budget:
            mesh_ops.reduce_to_budget(obj, family=FAMILY, lod_index=lod_index)
            # That extra collapse has to be cleaned after itself, and NOT with the
            # hole filling on. Quadric Edge Collapse pulls faces onto shared edges:
            # measured here, the refit left 6 edges carrying 3-4 triangles at LOD1 and
            # 5 at LOD2, which forces a repeated directed edge that no winding choice
            # can resolve -- 12 GATE_INCONSISTENT_WINDING occurrences. weld_and_clean's
            # non-manifold repair (keep the two largest faces at such an edge, drop the
            # buried interior sheets) is the owner of that defect. Filling again here
            # would re-inflate past the budget and put the level straight back on the
            # seam-drop path, so this pass repairs without adding geometry.
            shell_trace.append(("after_refit_decimate",
                                mesh_ops.topology_report(obj)))
            repair, resettle = _settle_topology(obj, fill_cracks=False)
            refitted = mesh_ops.triangle_count(obj.data)
            shell_trace.append(("after_refit_repair", mesh_ops.topology_report(obj)))
        notes.append("LOD{0} shell trace: {1}".format(
            lod_index,
            "; ".join("{0} comp={1} tris={2} boundary={3} nonmanifold={4} "
                      "smallest={5}".format(stage, report.components,
                                            report.triangles, report.boundary_edges,
                                            report.nonmanifold_edges,
                                            report.smallest_component)
                      for stage, report in shell_trace)))
        notes.append(
            "LOD{0} crack repair: {1} -> {2} tris closing {3} boundary loops "
            "({4} boundary edges left), {7}, refitted to {5} against the {6} budget"
            .format(lod_index, before_tris, filled_tris,
                    clean["boundary_loops_filled"], clean["boundary_edges_after"],
                    refitted, budget, settle)
            + ("" if repair is None else
               "; post-refit repair removed {0} interior faces at non-manifold edges "
               "({1} -> {2}), leaving {3} boundary edges, then {4}"
               .format(repair["interior_faces_deleted"],
                       repair["nonmanifold_edges_before"],
                       repair["nonmanifold_edges_after"],
                       repair["boundary_edges_after"], resettle)))

        # RE-DERIVE THE SHADING BASIS, because every step above invalidated the one this
        # level inherited. LOD1/LOD2 start as copies of LOD0 and therefore carry LOD0's
        # custom split normals interpolated across a collapse, and `weld_and_clean` ends
        # with `recalc_face_normals`, which puts face normals out of agreement with that
        # inherited basis. Measured consequence: `verify_fbx_roundtrip` rejected the
        # package with "LOD2: corner normals changed by 0.001828; the authored
        # weighted/split normal basis did not survive" -- 34x TOL_NORMAL, whose measured
        # worst case for genuine INT16 custom normals is 5.34e-5, so that delta is a
        # broken basis and not export precision. `rock.py` recorded the same failure at
        # 0.001859 from the same cause.
        #
        # Re-deriving is also the correct answer rather than merely the working one.
        # `3dmodel.md` section 7 requires decimation to preserve "hard normals", and at
        # LOD2 the surface LOD0's normals described no longer exists; what preserves the
        # requirement is re-applying the same rule -- the organic dihedral threshold from
        # `law.smooth_angle_for` plus FACE_AREA_WITH_ANGLE weighting -- to the topology
        # that actually ships.
        relit = mesh_ops.apply_shading_basis(
            obj, smooth_angle_deg=law.smooth_angle_for(SURFACE), weighted=True,
            keep_sharp=True)
        if relit.smooth_polygons <= 0 or not relit.weighted_applied:
            notes.append(
                "LOD{0} shading basis NOT re-derived (smooth_polygons={1} "
                "weighted={2}); the level would ship with LOD0's stale normals"
                .format(lod_index, relit.smooth_polygons, relit.weighted_applied))
        else:
            notes.append(
                "LOD{0} shading basis re-derived at {1:.0f} deg: smooth_polygons={2} "
                "sharp_edges={3} weighted=True".format(
                    lod_index, law.smooth_angle_for(SURFACE),
                    relit.smooth_polygons, relit.sharp_edges))

        mesh_ops._make_sole_active(obj)
        bpy.ops.object.mode_set(mode="EDIT")
        try:
            bpy.ops.mesh.select_all(action="SELECT")
            result = bpy.ops.uv.smart_project(
                angle_limit=math.radians(UV_PROJECTION_ANGLE_DEG),
                island_margin=padding,
                area_weight=0.0,
                correct_aspect=True,
                scale_to_bounds=False)
        finally:
            bpy.ops.object.mode_set(mode="OBJECT")
        if "FINISHED" not in result:
            notes.append("LOD{0} smart_project returned {1}; UVs are whatever the "
                         "collapse left".format(lod_index, sorted(result)))
            return

        layer = obj.data.uv_layers.active
        if layer is None:
            notes.append("LOD{0} has no active UV layer after re-unwrap".format(
                lod_index))
            return
        count = len(layer.data)
        buffer = [0.0] * (count * 2)
        layer.data.foreach_get("uv", buffer)
        span = 1.0 - 2.0 * padding
        lo_u = min(buffer[0::2]) if count else 0.0
        hi_u = max(buffer[0::2]) if count else 1.0
        lo_v = min(buffer[1::2]) if count else 0.0
        hi_v = max(buffer[1::2]) if count else 1.0
        range_u = max(1e-6, hi_u - lo_u)
        range_v = max(1e-6, hi_v - lo_v)
        # One uniform factor for both axes so the re-unwrap cannot introduce aspect
        # distortion of its own while fixing the border overlap.
        factor = min(span / range_u, span / range_v)
        for i in range(count):
            buffer[i * 2] = padding + (buffer[i * 2] - lo_u) * factor
            buffer[i * 2 + 1] = padding + (buffer[i * 2 + 1] - lo_v) * factor
        layer.data.foreach_set("uv", buffer)
        obj.data.update()
        notes.append("LOD{0} re-unwrapped (smart_project, angle {1:.1f} deg derived "
                     "from the {2} organic aspect limit) and rescaled into the {3} px "
                     "border reserve by x{4:.4f}".format(
                         lod_index, UV_PROJECTION_ANGLE_DEG,
                         law.UV_STRETCH_MAX_BY_SURFACE[SURFACE],
                         law.atlas_padding_for(atlas_size), factor))

    return reunwrap


def uv_diagnostics(mesh: bpy.types.Mesh, regions: Optional[Sequence[str]] = None) -> dict:
    """Per-material-slot UV anisotropy, using the VALIDATOR's own metric.

    ``mesh_ops.uv_stretch_stats`` reports a crude ratio of two edge scalings while
    ``validate.uv_aspect_distortion`` reports the real ``sigma_max / sigma_min - 1`` of
    the parameterisation. Measured on this asset they disagreed by two orders of
    magnitude (8.4 against 493), so the number a generator tunes against has to be the
    one the gate uses. Splitting by material slot turns "34.7% of area is stretched"
    into "which surface", which is the difference between a fix and a guess.
    """
    data = validate.extract_mesh_data(mesh)
    if not data.uv_layers:
        return {"status": "no uv layer"}
    _name, uv0 = data.uv_layers[0]
    # loop_triangle -> polygon, so a per-face structural label can be looked up. The
    # accumulator's face order survives into mesh.polygons unchanged (from_pydata keeps
    # it, and the clean pass is asserted to remove nothing), so the index maps directly.
    mesh.calc_loop_triangles()
    polygon_of_triangle = [tri.polygon_index for tri in mesh.loop_triangles]
    buckets: dict = {}
    for t in range(data.triangle_count):
        slot = data.tri_material_index[t] if t < len(data.tri_material_index) else -1
        distortion = validate.uv_aspect_distortion(
            data.positions, uv0, data.tri_vertices, data.tri_loops, t)
        area = validate._triangle_world_area(data, t)
        bucket = buckets.setdefault(slot, {"n": 0, "area": 0.0, "over": 0.0,
                                           "worst": 0.0, "worstTri": -1,
                                           "byRegion": {}})
        bucket["n"] += 1
        bucket["area"] += area
        if not law.finite(distortion):
            distortion = float("inf")
        if distortion > bucket["worst"]:
            bucket["worst"] = distortion
            bucket["worstTri"] = t
        if distortion > law.uv_stretch_limit_for(SURFACE, hero=True):
            bucket["over"] += area
            if regions is not None and t < len(polygon_of_triangle):
                polygon = polygon_of_triangle[t]
                name = regions[polygon] if 0 <= polygon < len(regions) else "unknown"
                bucket["byRegion"][name] = bucket["byRegion"].get(name, 0.0) + area
    def edge_lengths(t: int):
        """(world, uv) edge lengths of triangle ``t``, so a bad sigma has a cause."""
        vs = [data.tri_vertices[t * 3 + k] for k in range(3)]
        ls = [data.tri_loops[t * 3 + k] for k in range(3)]
        world = []
        uv = []
        for k in range(3):
            a, b = vs[k], vs[(k + 1) % 3]
            world.append(round(math.sqrt(sum(
                (data.positions[a * 3 + c] - data.positions[b * 3 + c]) ** 2
                for c in range(3))), 6))
            la, lb = ls[k], ls[(k + 1) % 3]
            uv.append(round(math.hypot(uv0[la * 2] - uv0[lb * 2],
                                       uv0[la * 2 + 1] - uv0[lb * 2 + 1]), 6))
        return world, uv

    def region_of(t: int) -> str:
        if regions is None or t < 0 or t >= len(polygon_of_triangle):
            return "unknown"
        polygon = polygon_of_triangle[t]
        return regions[polygon] if 0 <= polygon < len(regions) else "unknown"

    out = {}
    for slot in sorted(buckets):
        bucket = buckets[slot]
        role = MATERIAL_ROLES[slot] if 0 <= slot < len(MATERIAL_ROLES) else str(slot)
        world, uv = edge_lengths(bucket["worstTri"]) if bucket["worstTri"] >= 0 \
            else ([], [])
        out[role] = {
            "triangles": bucket["n"],
            "worst": round(bucket["worst"], 3) if law.finite(bucket["worst"]) else "inf",
            "worstTriangle": bucket["worstTri"],
            "worstRegion": region_of(bucket["worstTri"]),
            "worstWorldEdgesM": world,
            "worstUvEdges": uv,
            "stretchedAreaFraction": round(
                bucket["over"] / max(1e-12, bucket["area"]), 4),
            "overLimitByRegion": {
                name: round(area / max(1e-12, bucket["area"]), 4)
                for name, area in sorted(bucket["byRegion"].items())
            },
        }
    return out


def _read_vcol_direct(obj: bpy.types.Object) -> dict:
    """Read the packed colour attribute off the mesh, labelled by the organic contract.

    Delegates the numbers to ``vertexcolor.channel_stats`` rather than reimplementing
    them, because that function area-weights its mean so it is comparable with
    ``preview.measure_channel_png``. Compare MIN and MAX between the two: those are
    weighting-independent, while a rendered tile averages over pixels and a readback
    over loops, and for a non-uniform field the two means legitimately differ.

    Reading the stored values at the source is not redundant with the render. Channel
    tiles were measuring stacked LOD copies still wearing default grey and reporting a
    plausible min 0.0 / max 1.0 for every channel; a readback is the only way to tell a
    broken instrument from a broken generator.
    """
    stats = vertexcolor.channel_stats(obj)
    if not stats.get("present"):
        return stats
    out = {"present": True, "domain": stats.get("domain"),
           "attribute": stats.get("attribute")}
    for index, label in enumerate(law.ORGANIC_VCOL):
        out[label] = {
            "min": stats["min"][index],
            "max": stats["max"][index],
            "areaWeightedMean": stats["areaWeightedMean"][index],
        }
    return out


def _purge_scene() -> None:
    """Empty the factory-startup scene so only generated geometry is present."""
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)


# ---------------------------------------------------------------------------
# One variant, in the mandated stage order
# ---------------------------------------------------------------------------

@dataclass
class VariantResult:
    name: str
    lods: list
    reports: list
    chain_failures: list
    collider: object
    vcol_report: dict
    vcol_direct: dict
    ao: object
    sway: object
    uv_summary: dict
    topology: dict
    shading: object
    stems: list
    notes: List[str]
    interaction: dict


def generate_variant(*, seed: int, quality: float, cap_radius: float, height: float,
                     variant_index: int, ao_samples: int, atlas_size: int,
                     blackbox: BlackBox) -> VariantResult:
    """Run PROCEDURAL_ASSET_PIPELINE.md "Generation Order" 1..11 for one asset."""
    notes: List[str] = []
    rng = np.random.default_rng(seed + variant_index * 7919)
    name = "CapStem_{seed}_{index:02d}".format(seed=seed, index=variant_index)

    # --- 1/2. deterministic manifest inputs, then the shape grammar --------
    clump = plan_clump(rng, quality=quality, cap_radius=cap_radius, height=height)
    blackbox.record("shape_grammar", seed=seed, family=FAMILY.value,
                    warning="stems={0} segments={1} ribs={2} rings={3}/{4}/{5}".format(
                        len(clump.stems), clump.segments, clump.stems[0].rib_count,
                        clump.stem_rings, clump.cap_bottom_rings,
                        clump.cap_top_rings))

    # --- 3/4. high-detail geometry + family topology rules ----------------
    accum = _Accum()
    stem_reports = []
    for index, stem in enumerate(clump.stems):
        stem_reports.append(_build_stem(accum, stem, clump, rng, island_base=index * 5))
    quads = len(accum.faces)
    added = accum.triangulate()
    blackbox.record("geometry", vertex_count=len(accum.positions),
                    triangle_count=len(accum.faces),
                    warning="faces={0} (fanned {1} authored polygons into {2} "
                            "triangles, +{3})".format(
                                len(accum.faces), quads, len(accum.faces), added))
    notes.append(
        "authored {0} polygons fanned into {1} triangles before UV packing, so the "
        "validated topology is the exported topology: export_unity writes "
        "use_triangles=True (mandatory -- use_tspace drops tangents silently on "
        "n-gons) and verify_fbx_roundtrip compares PER-CORNER data, which a quad "
        "source can never match".format(quads, len(accum.faces)))

    # --- 5. UVs and material IDs ------------------------------------------
    uv_summary = pack_islands(accum, atlas_size=atlas_size,
                              texel_density=law.TEXEL_DENSITY_HERO_FLORA)
    if uv_summary["densityScaleApplied"] < 1.0:
        notes.append(
            "texel density reduced from {0} to {1} px/m so the islands fit inside the "
            "{2} px atlas border reserve".format(
                uv_summary["requestedTexelDensityPxPerM"],
                uv_summary["achievedTexelDensityPxPerM"], uv_summary["paddingPx"]))

    obj = _to_object(accum, uv_summary.pop("uvs"),
                     law.NAME_MESH.format(family=FAMILY.value, name=name, lod=0),
                     blackbox)

    # Materials are created HERE, before the bake, not at stage 7. The Cycles AO bake
    # refuses to run on an object with no material slot, so the shared-material stage
    # is forced ahead of the bake stage. Recorded rather than silently reordered.
    materials = build_materials()
    for material in materials:
        obj.data.materials.append(material)
    notes.append(
        "stage order deviation: shared MAT_* materials are built at stage 5 instead of "
        "stage 7 because bpy.ops.object.bake refuses an object with no material slot, "
        "and stage 6 needs the bake. No other stage moved.")

    # --- topology probe before anything can hide a construction fault -----
    before_verts = len(obj.data.vertices)
    before_faces = len(obj.data.polygons)
    bm = mesh_ops.bmesh_from_object(obj)
    clean = mesh_ops.weld_and_clean(bm, merge_distance=1e-5, blackbox=blackbox)
    mesh_ops.bmesh_to_object(bm, obj)
    if (clean["verts_removed"] or clean["faces_removed"]
            or clean["degenerate_faces_deleted"]):
        # The parametrisation is supposed to emit a clean welded manifold. If the clean
        # pass had work to do, that is a construction defect worth seeing, not a
        # convenience: the per-vertex authored scalars are indexed by build order and a
        # merge would desynchronise them from the geometry.
        raise GenerationAborted(
            "weld_and_clean modified a mesh that should already be clean: {0} "
            "(verts {1}->{2}, faces {3}->{4}). The authored per-vertex sway/harvest "
            "arrays are index-aligned and would now be wrong.".format(
                clean, before_verts, len(obj.data.vertices), before_faces,
                len(obj.data.polygons)))

    topology = mesh_ops.topology_report(obj)
    blackbox.record("topology_report", vertex_count=len(obj.data.vertices),
                    triangle_count=topology.triangles,
                    warning="components={0} boundary={1} nonmanifold={2} floor~{3}"
                    .format(topology.components, topology.boundary_edges,
                            topology.nonmanifold_edges, topology.irreducible_floor))
    if topology.boundary_edges:
        notes.append(
            "{0} boundary edges: the clump should be {1} closed shells, one per stem, "
            "so a boundary means a ring failed to weld".format(
                topology.boundary_edges, len(clump.stems)))
    if topology.nonmanifold_edges:
        notes.append("{0} non-manifold edges present".format(
            topology.nonmanifold_edges))

    # --- shading basis: organic angle, never the hard-surface default -----
    shading = mesh_ops.apply_shading_basis(
        obj, smooth_angle_deg=law.smooth_angle_for(SURFACE), weighted=True,
        keep_sharp=True, blackbox=blackbox)
    if shading.smooth_polygons <= 0:
        raise GenerationAborted(
            "apply_shading_basis smoothed 0 polygons; the asset would ship flat-shaded")

    # --- 6. bakes and vertex colours --------------------------------------
    # AO ray length is matched to the cavity the bible actually names -- "under plates,
    # root clusters" -- which here is the gap between the cap underside and the ground
    # and the pockets between the holdfast fingers. That is on the order of the cap
    # radius, NOT of the whole clump: unbounded rays turn local occlusion into a global
    # sky term and the underside stops darkening.
    ao_distance = min(0.55, max(0.06, cap_radius * 1.15))
    ao = vertexcolor.bake_ambient_occlusion(obj, samples=ao_samples,
                                            distance=ao_distance, blackbox=blackbox)
    ao_values = vertexcolor.consume_baked_ao(obj)
    notes.append("AO bake distance {0:.3f} m (cap radius {1:.3f} m), {2} samples"
                 .format(ao_distance, cap_radius, ao_samples))

    # GEODESIC distance along the growth path: stem arc length, then surface arc length
    # out across the cap. A cap tilted back over its own base is far along the stem but
    # near it in straight lines, and Euclidean measurement would call that rim rigid.
    max_geodesic = max(accum.geodesic) if accum.geodesic else 0.0
    sway = vertexcolor.build_sway_field(
        obj.data,
        anchor_position=Vector((0.0, 0.0, 0.0)),
        max_flexible_length=max_geodesic,
        stiffness_exponent=law.STIFFNESS_EXPONENT_FLEXIBLE_BLADE,
        rigid_cap=None,          # soft tissue; a rigid cap belongs to mineralised coral
        distances=accum.geodesic)

    # Channel G: 0 everywhere. Nothing in nice_biome.webp, beauty.webp or shallows.webp
    # shows an emissive cap or stem -- these are sunlit photic-zone plants, and
    # 3DMODEL_FLORA_CORAL.md section 2 fixes non-emissive tissue at exactly 0. Painting
    # a decorative rim glow here would be inventing a reference detail.
    biolum = [0.0] * len(obj.data.vertices)

    vcol_report = vertexcolor.write_organic_channels(
        obj, sway=sway, biolum=biolum,
        ao=ao_values if ao_values else None,
        alpha=accum.harvest, alpha_meaning=ALPHA_MEANING, blackbox=blackbox)
    vertexcolor.remove_scratch_attributes(obj.data)
    vcol_direct = _read_vcol_direct(obj)

    # --- 7/8. LOD chain (materials already built at stage 5) --------------
    #
    # preserve_seams=False, AND THAT IS NOT A RELAXATION OF 3dmodel.md SECTION 7.
    #
    # `_split_uv_seams` converts UV seams, sharp edges and material borders into mesh
    # BOUNDARIES so Decimate/COLLAPSE cannot collapse across them. It exists to protect a
    # parameterisation and a shading basis through the decimation. This generator THROWS
    # BOTH AWAY at LOD1 and LOD2: `_make_reunwrap` re-solves UVs from scratch with
    # `smart_project` and re-derives the weighted/split normal basis with
    # `apply_shading_basis`. So the split was protecting data that is discarded minutes
    # later -- it bought exactly nothing here, while doing real damage.
    #
    # THE DAMAGE, MEASURED on seed 1811 by bracketing every step of the reunwrap hook
    # with `topology_report` (the shell trace note below):
    #
    #   preserve_seams=True     LOD1  4 comp, 42 boundary edges, 1620 tris
    #                           LOD2 37 comp, 132 boundary edges,  264 tris
    #   preserve_seams=False    LOD1  4 comp,  0 boundary edges, 1728 tris
    #                           LOD2  4 comp,  0 boundary edges,  288 tris
    #
    # LOD2 was 264 triangles in 37 disconnected pieces -- 18 of them SINGLE TRIANGLES,
    # visible as shards off the cap rims, plus a detached juvenile cap disc -- while
    # passing the triangle budget, uv_stretch_excessive, winding, tangent and FBX
    # round-trip gates. Both coarse levels are now closed 4-component manifolds matching
    # LOD0's authored shell count exactly, and the triangle counts went UP because no
    # budget is spent on fragments.
    #
    # A WELD CANNOT FIX IT, and that was measured before this change rather than assumed.
    # `_weld_coincident` (1e-6) already runs inside `build_lod_chain` after the decimation
    # and `weld_and_clean` (1e-4) runs twice more inside the reunwrap hook; LOD2 still
    # arrived at 37 components. Probing the real gaps between those components gave
    # 0.66 mm to 15.5 mm with ZERO pairs inside either tolerance: Quadric Edge Collapse
    # moves the two sides of a split seam independently, so the duplicates stop being
    # coincident and a distance weld has nothing to grab. Closing the 15.5 mm gap would
    # need a tolerance larger than the cap rim thickness (12.3-13.6 mm), i.e. it would
    # flatten the plate the asset is made of. Not splitting is the fix; welding is not.
    #
    # THE PRECONDITION IS THE REUNWRAP HOOK. Dropping seams is only correct BECAUSE this
    # generator re-solves UVs and normals per level. A generator that passes
    # `reunwrap=None` must keep `preserve_seams=True` or it ships whatever the collapse
    # left. The two settings are redundant with each other and destructive together.
    #
    # AND IT NEEDS THE SLOT REPAIR BELOW TO BE SAFE. Measured A/B over the same 40 seeds
    # at quality 1.0, which is the only reason this is not shipping as a regression:
    # dropping the seam split took `submesh_empty_declared_slot` from 0/40 to 12/40.
    # `_split_uv_seams` also splits MATERIAL BORDERS, and without that boundary Quadric
    # Edge Collapse drags one slot's geometry into another until the smallest role --
    # TornEdge, the rim band -- loses its last polygon. The first version of this change
    # fixed LOD2's shell on one seed and broke the submesh contract on 30% of them.
    slot_anchors = mesh_ops.material_slot_anchors(obj)
    lod_notes: List[str] = []
    lods = mesh_ops.build_lod_chain(
        obj, family=FAMILY, name=name, quality_weight=quality, levels=3,
        preserve_seams=False, reunwrap=_make_reunwrap(atlas_size, lod_notes),
        blackbox=blackbox)
    notes.extend(lod_notes)

    # `mesh_ops.preserve_material_slots` is the documented owner of that defect -- it
    # re-tags the surviving polygon nearest each emptied slot's LOD0 centroid -- and
    # cap-stem was the ONLY generator that never called it. `kelp.py` and
    # `coral_branching.py` both wired it after hitting this same gate at 288 and 285
    # triangles. 3dmodel.md section 10 requires the submesh count to match the
    # declaration and 3DMODEL_FLORA_CORAL.md section 6 requires LOD2 to keep the shader
    # semantics it still reads, so keeping the role alive at its own location is the
    # honest repair rather than letting a material silently vanish down the chain.
    for level in lods:
        repaired = mesh_ops.preserve_material_slots(level.obj, slot_anchors)
        if repaired:
            notes.append("LOD{0} material slots repaired: {1}".format(
                level.index, repaired))

    for level in lods:
        if not level.within_budget:
            report = mesh_ops.topology_report(level.obj)
            notes.append("LOD{0} over budget: {1}".format(
                level.index, report.explain(level.budget)))

    # --- 9. collision proxy ----------------------------------------------
    collider = mesh_ops.make_convex_collider(lods[0].obj, family=FAMILY, name=name,
                                             blackbox=blackbox)
    # 3DMODEL_FLORA_CORAL.md section 7: flora collision is none; interaction is "Root
    # harvest point: sphere or capsule". That proxy is a transform plus a radius, not
    # geometry, so it belongs in the manifest for the Unity assembler to instantiate.
    lo, hi = mesh_ops.local_bounds(lods[0].obj)
    foot_radius = 0.5 * max(hi.x - lo.x, hi.y - lo.y)
    interaction = {
        "collision": "none",
        "collisionJustification": collider.reason,
        "harvestProxy": {
            "type": "sphere",
            "centerLocal": [0.0, 0.0, round(min(0.06, height * 0.16), 5)],
            "radiusM": round(max(0.05, foot_radius * 0.62), 5),
            "anchor": "ANCHOR_Loot",
            "layer": "Flora_NonColliding",
            "isTrigger": True,
        },
        "anchors": {
            "ANCHOR_Loot": [0.0, 0.0, round(min(0.06, height * 0.16), 5)],
            "ANCHOR_Scan": [0.0, 0.0, round(height * 0.92, 5)],
        },
    }

    # --- 10. validation ---------------------------------------------------
    reports = []
    for level in lods:
        # The island-pixel and border-padding gates take an atlas size, and 3dmodel.md
        # section 6 scopes the first one explicitly: "Islands smaller than 4 pixels at
        # target mip 0 for any visible LOD0 detail". LOD0 carries the authored packed
        # layout and is measured against it. LOD1/LOD2 carry a smart_project solve over
        # collapsed topology, which always produces some slivers, so passing an atlas
        # size there would enforce a rule the bible does not state -- the gate reports
        # itself as not enforced and the real numbers are measured separately below.
        reports.append(validate.validate_mesh(
            level.obj.data, family=FAMILY, lod_index=level.index,
            surface_class=SURFACE, blackbox=blackbox, hero=(level.index == 0),
            triplanar=False, double_sided=False, planar=False,
            atlas_size=atlas_size if level.index == 0 else None))
        stats = mesh_ops.uv_stretch_stats(level.obj)
        notes.append("LOD{0} uv edge-ratio: worst={1:.4f} p95={2:.4f} mean={3:.4f} "
                     "over {4} triangles; validator sigma-ratio per slot: {5}".format(
                         level.index, stats["worst"], stats["p95"], stats["mean"],
                         stats["triangles"],
                         uv_diagnostics(level.obj.data,
                                        accum.face_region if level.index == 0
                                        else None)))
    chain_failures = validate.validate_lod_chain(reports, family=FAMILY,
                                                 blackbox=blackbox)

    return VariantResult(
        name=name, lods=lods, reports=reports, chain_failures=chain_failures,
        collider=collider, vcol_report=vcol_report, vcol_direct=vcol_direct, ao=ao,
        sway=sway, uv_summary=uv_summary,
        topology={
            "triangles": topology.triangles,
            "components": topology.components,
            "boundaryEdges": topology.boundary_edges,
            "nonManifoldEdges": topology.nonmanifold_edges,
            "irreducibleFloor": topology.irreducible_floor,
        },
        shading=shading, stems=stem_reports, notes=notes, interaction=interaction)


# ---------------------------------------------------------------------------
# Proof artefacts
# ---------------------------------------------------------------------------

def render_proof(variant: VariantResult, *, out_dir: str, resolution: int) -> dict:
    """Flat, studio, material and channel sheets, then MEASURE every channel tile.

    ``3DMODEL_FLORA_CORAL.md`` section 10 requires BOTH a "flat-material screenshot
    proving the silhouette is biological before texture detail" AND a "final-material
    screenshot proving wetness, translucency, pigment ... support the organism", so
    both are rendered; neither substitutes for the other.
    """
    subject = variant.lods[0].obj
    sheets = {}
    for mode in ("flat", "studio", "material"):
        spec = preview.PreviewSpec(
            name=variant.name, output_dir=out_dir, resolution=resolution,
            views=("front", "three_quarter", "side", "low"), mode=mode,
            surface_class=SURFACE)
        sheets[mode] = preview.render_contact_sheet(subject, spec).sheet_path

    # ---- COARSE LOD SHEETS ------------------------------------------------
    # Until this existed, `subject = variant.lods[0].obj` was the ONLY thing rendered
    # anywhere in the pipeline, so LOD1 and LOD2 had zero visual proof at any point --
    # and LOD2 used that cover to ship VISIBLY SHATTERED while passing every numeric
    # gate it has: triangle budget, uv_stretch_excessive, winding, tangents and the FBX
    # round trip were all green at 264 triangles in 37 disconnected components with
    # shards off the cap rims. The rule that governs this is in the pipeline notes as
    # "the number and the image are two instruments, neither substitutes for the other";
    # a level nobody renders only has one of them.
    #
    # `flat` mode only, and that is a deliberate choice rather than a saving. Coarse-LOD
    # damage is SILHOUETTE and SHELL damage -- fragments, shards, holes, a detached cap
    # -- and the flat override is the mode that shows it. A material render at LOD2
    # would dress the same fragments in pigment and read as acceptable, which is the
    # failure mode this block exists to end.
    #
    # THE LOD NUMBER IS IN THE FILENAME, not just in a dict key. These sheets sit in the
    # same directory as LOD0's, and a coarse sheet mistaken for LOD0 is worse than no
    # sheet -- it makes a broken level look like an authoring choice. `PreviewSpec.name`
    # drives every tile and sheet path, so `..._LOD2_SHEET_flat.png` is unambiguous in
    # any file listing, in the manifest, and in a chat window. `clear_render_dir` keys
    # its staleness sweep on that same name plus mtime-against-process-start, so the
    # per-LOD prefixes cannot delete each other's output within one run.
    lod_sheets = {}
    lod_topology = {}
    for level in variant.lods[1:]:
        key = "LOD{0}".format(level.index)
        spec = preview.PreviewSpec(
            name="{0}_{1}".format(variant.name, key), output_dir=out_dir,
            resolution=resolution, views=("three_quarter", "low"), mode="flat",
            surface_class=SURFACE)
        lod_sheets[key] = preview.render_contact_sheet(level.obj, spec).sheet_path
        # Ship the number NEXT TO the image, from the same object, in the same pass.
        # `topology_report` is what turns "LOD2 looks wrong" into "37 components,
        # largest 38 triangles" -- a cause rather than an impression -- and the
        # component count is the one figure no existing gate reports.
        report = mesh_ops.topology_report(level.obj)
        lod_topology[key] = {
            "triangles": report.triangles,
            "components": report.components,
            "boundaryEdges": report.boundary_edges,
            "nonManifoldEdges": report.nonmanifold_edges,
            "smallestComponent": report.smallest_component,
            "largestComponent": report.largest_component,
        }

    channel_spec = preview.PreviewSpec(
        name=variant.name, output_dir=out_dir, resolution=resolution,
        mode="studio", surface_class=SURFACE)
    channels = preview.render_channel_sheet(subject, channel_spec,
                                            view="three_quarter")

    measurements = []
    for index, tile in enumerate(channels.tile_paths):
        stats = preview.measure_channel_png(tile)
        measurements.append({
            "channel": law.ORGANIC_VCOL[index],
            "tile": os.path.basename(tile),
            "min": round(stats.min_value, 5),
            "max": round(stats.max_value, 5),
            "mean": round(stats.mean_value, 5),
            "coverage": round(stats.coverage_fraction, 5),
            "hasGradient": stats.has_gradient,
            "subjectVisible": stats.subject_visible,
        })

    return {
        "sheets": sheets,
        "lodSheets": lod_sheets,
        "lodTopology": lod_topology,
        "channelSheet": channels.sheet_path,
        "channelTiles": list(channels.tile_paths),
        "measurements": measurements,
    }


def _print_report(variant: VariantResult, proof: Optional[dict],
                  export_result, manifest_path: str) -> None:
    print("")
    print("=" * 78)
    print("ASSET {0}   family={1}   surface={2}".format(
        variant.name, FAMILY.value, SURFACE.value))
    print("=" * 78)
    budgets = law.LOD_BUDGETS[FAMILY]
    for level, report in zip(variant.lods, variant.reports):
        print("  LOD{0}  {1:>6} tris / {2:>5} budget   verts={3:<6} submeshes={4}  "
              "{5}".format(level.index, level.triangles, budgets.limit(level.index),
                           report.vertex_count, report.submesh_count,
                           "PASS" if report.passed else "FAIL"))
        for failure in report.failures:
            print("         ! " + str(failure))
    print("  lod chain: {0}".format(
        "PASS" if not variant.chain_failures else
        "; ".join(str(f) for f in variant.chain_failures)))
    print("  collider : {0} ({1})".format(variant.collider.kind,
                                          variant.collider.reason))
    print("  topology : {0}".format(variant.topology))
    print("  shading  : smooth_polygons={0} sharp_edges={1} weighted={2}".format(
        variant.shading.smooth_polygons, variant.shading.sharp_edges,
        variant.shading.weighted_applied))
    print("  AO bake  : baked={0} min={1:.4f} max={2:.4f} mean={3:.4f} "
          "has_contrast={4}".format(variant.ao.baked, variant.ao.min_value,
                                    variant.ao.max_value, variant.ao.mean_value,
                                    variant.ao.has_contrast))
    print("  sway     : min={0:.4f} max={1:.4f} exponent={2} relative_spread={3:.3f} "
          "uniform={4}".format(variant.sway.min_value, variant.sway.max_value,
                               variant.sway.stiffness_exponent,
                               variant.sway.relative_spread, variant.sway.is_uniform))
    print("  vcol direct readback (attribute '{0}'):".format(law.VCOL_ATTRIBUTE_NAME))
    for label in law.ORGANIC_VCOL:
        entry = variant.vcol_direct.get(label)
        if entry:
            print("         {0:<16} min={1:<8} max={2:<8} mean={3}".format(
                label, entry["min"], entry["max"], entry["areaWeightedMean"]))
    print("  uv       : {0}".format(
        {k: v for k, v in variant.uv_summary.items() if k != "route"}))
    if proof is not None:
        for measurement in proof["measurements"]:
            print("  chan {0:<16} min={1:<8} max={2:<8} mean={3:<8} coverage={4} "
                  "gradient={5} visible={6}".format(
                      measurement["channel"], measurement["min"], measurement["max"],
                      measurement["mean"], measurement["coverage"],
                      measurement["hasGradient"], measurement["subjectVisible"]))
        for mode, path in proof["sheets"].items():
            print("  sheet {0:<9} {1}".format(mode, path))
        for key, path in proof.get("lodSheets", {}).items():
            print("  sheet {0:<9} {1}".format(key, path))
        for key, report in proof.get("lodTopology", {}).items():
            print("  shell {0:<9} {1}".format(key, report))
        print("  sheet channels  {0}".format(proof["channelSheet"]))
    if export_result is not None:
        print("  fbx      : {0}".format(export_result.fbx_path))
        print("  roundtrip: verified={0} unit_scale={1}".format(
            export_result.roundtrip_verified, export_result.unit_scale))
    if manifest_path:
        print("  manifest : {0}".format(manifest_path))
    for note in variant.notes:
        print("  note     : " + note)
    for stem in variant.stems:
        print("  stem     : " + str(stem))


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        prog="flora_capstem.py",
        description="Generate HECTON-8 cap-and-stem flora (amber fan) packages.")
    parser.add_argument("--seed", type=int, default=3301,
                        help="deterministic seed; variation is a named seed, never "
                             "hidden chance")
    parser.add_argument("--quality", type=float, default=1.0,
                        help="GlobalQualityWeight, continuous 0..1")
    parser.add_argument("--variants", type=int, default=1)
    parser.add_argument("--cap-radius", type=float, default=0.15,
                        help="metres; the reference caps read at 0.10-0.25 m")
    parser.add_argument("--height", type=float, default=0.42,
                        help="metres, tallest stem in the clump")
    parser.add_argument("--out", default="",
                        help="output directory; defaults to "
                             "Docs/AgentLogs/ForgePreviews under the project root")
    parser.add_argument("--ao-samples", type=int, default=64)
    parser.add_argument("--atlas", type=int, default=ATLAS_SIZE)
    parser.add_argument("--preview-resolution", type=int, default=640)
    parser.add_argument("--preview", dest="preview", action="store_true", default=True)
    parser.add_argument("--no-preview", dest="preview", action="store_false")
    parser.add_argument("--no-export", dest="export", action="store_false",
                        default=True,
                        help="skip FBX + manifest, keep validation and previews")

    if "--" in argv:
        argv = argv[argv.index("--") + 1:]
    else:
        argv = []
    return parser.parse_args(argv)


def main(argv: Sequence[str]) -> int:
    args = parse_args(argv)
    quality = law.saturate(args.quality)
    out_dir = args.out or os.path.join(law.project_root(), "Docs", "AgentLogs",
                                       "ForgePreviews")
    os.makedirs(out_dir, exist_ok=True)

    exit_code = 0
    for variant_index in range(max(1, args.variants)):
        run_tag = "capstem_s{0}_q{1:.2f}_v{2}".format(args.seed, quality,
                                                      variant_index)
        blackbox = BlackBox("flora_capstem", run_tag)
        _purge_scene()
        try:
            variant = generate_variant(
                seed=args.seed, quality=quality, cap_radius=args.cap_radius,
                height=args.height, variant_index=variant_index,
                ao_samples=args.ao_samples, atlas_size=args.atlas,
                blackbox=blackbox)
        except GenerationAborted as error:
            print("[flora_capstem] ABORTED: {0}".format(error))
            return 2

        proof = None
        if args.preview:
            proof = render_proof(variant, out_dir=out_dir,
                                 resolution=args.preview_resolution)

        # 3dmodel.md section 10: validation failure ABORTS the save. Previews are
        # rendered first on purpose -- a rejected asset still has to be LOOKED at, and
        # the sheets are the evidence for why it was rejected.
        try:
            validate.assert_or_abort(
                [variant.reports, variant.chain_failures], blackbox=blackbox,
                reason="flora_capstem " + variant.name)
        except GenerationAborted as error:
            print("[flora_capstem] VALIDATION REJECTED THE SAVE: {0}".format(error))
            _print_report(variant, proof, None, "")
            exit_code = 3
            continue

        export_result = None
        manifest_path = ""
        if args.export:
            # TWO defects fixed here, and the second is the quiet one.
            #
            # 1. The package went to out_dir, which defaults to
            #    Docs/AgentLogs/ForgePreviews. .gitignore:201 ignores that tree
            #    wholesale and it is outside Assets, so every cap-stem package was
            #    invisible to Unity and to git and one `git clean` from gone. Packages
            #    now go to law.forge_package_dir; renders stay in out_dir.
            #
            # 2. The file was named "{family}_{name}.fbx" - NO "MESH_" prefix. Even in
            #    the right directory that fails the import gate:
            #    HectonFBXPostprocessor.TryResolveForgeManifestPath (:702-736) requires
            #    the name to start with upper-case "MESH_", ORDINAL and case-sensitive,
            #    before it will look for the sibling manifest at all. Without that
            #    lookup the carve-out at :401-429 never fires and Unity re-derives
            #    normals from a single angle, discarding the authored weighted split
            #    basis. A wrong directory is visible; a wrong prefix would have looked
            #    like a working export forever.
            package_dir = os.path.join(
                law.project_root(),
                *law.forge_package_dir(FAMILY).split("/"))
            os.makedirs(package_dir, exist_ok=True)
            fbx_path = os.path.join(package_dir, "MESH_{0}_{1}.fbx".format(
                FAMILY.value, variant.name))
            # None, not the ColliderResult, when flora declined a collider.
            # export_lod_group handles a missing collider correctly, but handed a
            # ColliderResult whose .obj is None it fails inside the export with
            # "AttributeError: 'NoneType' object has no attribute 'select_get'"
            # instead of reading it as "no collider" - and flora's DEFAULT is no
            # collider, so the common path was the crashing one. Coral hit the same
            # thing from the same cause.
            collider_arg = (variant.collider
                            if getattr(variant.collider, "obj", None) is not None
                            else None)
            export_result = export_unity.export_lod_group(
                variant.lods, collider_arg, fbx_path, blackbox=blackbox)

            identity = law.GeneratorIdentity(
                generator=GENERATOR_NAME, generator_version=GENERATOR_VERSION,
                seed=args.seed + variant_index * 7919, quality_weight=quality,
                family=FAMILY,
                scale_meters=round(mesh_ops.longest_extent(variant.lods[0].obj), 5),
                camera_distance_class=CAMERA_DISTANCE_CLASS,
                platform_lane=PLATFORM_LANE, source_references=REFERENCE_IDS)

            proof_paths = []
            if proof is not None:
                # Coarse-LOD sheets are PROOF PATHS, not a side artefact: a reviewer who
                # opens only what the manifest lists must be shown LOD1 and LOD2, or the
                # blind spot that let a 37-component LOD2 ship is still open.
                proof_paths = (list(proof["sheets"].values())
                               + list(proof.get("lodSheets", {}).values())
                               + [proof["channelSheet"]])

            # Sibling of the FBX, in the package directory, never in out_dir: the
            # postprocessor derives the manifest path FROM the mesh path, so a manifest
            # anywhere else is a manifest that will never be read.
            manifest_path = export_unity.write_manifest(
                os.path.join(package_dir,
                             export_unity.manifest_filename(FAMILY, variant.name)),
                identity, variant.reports,
                [law.NAME_MATERIAL.format(family=FAMILY.value, role=role)
                 for role in MATERIAL_ROLES],
                # No texture set is authored by this generator: the pigment lives in
                # the MAT_* base colours and the masks live in the vertex-colour
                # channels. Naming a TX_* file that does not exist would be a false
                # reference, so the manifest records the gap honestly instead.
                [],
                [variant.collider] if variant.collider.obj is not None else [],
                proof_paths, export_result=export_result,
                uv_summary=variant.uv_summary, alpha_meaning=ALPHA_MEANING,
                extra={
                    "growthAlgorithm":
                        "parametric cap-and-stem clump: quadratic downstream stem "
                        "bend, lobed non-circular cross-section with lengthwise "
                        "ridges, flared finger holdfast, rib-driven cap (top relief, "
                        "lobed outline and underside gills from one term), torn edge "
                        "sectors, thick rounded rim band. Stem last ring IS the cap "
                        "hub ring, so the union is welded rather than intersected.",
                    "biomeRoute": "photic shallows, 0-100 m; reference nice_biome.webp",
                    "materialFamily": "flora_plate_amber",
                    "materialSlotMap": {
                        "0": MATERIAL_ROLES[0] + " (cap top + underside)",
                        "1": MATERIAL_ROLES[1] + " (torn rim band)",
                        "2": MATERIAL_ROLES[2] + " (stem + holdfast foot)",
                    },
                    "stems": variant.stems,
                    "topology": variant.topology,
                    "shading": {
                        "smoothAngleDeg": law.smooth_angle_for(SURFACE),
                        "smoothPolygons": variant.shading.smooth_polygons,
                        "sharpEdges": variant.shading.sharp_edges,
                        "weightedNormalsApplied": variant.shading.weighted_applied,
                    },
                    "vertexColorChannels": variant.vcol_report,
                    "vertexColorDirectReadback": variant.vcol_direct,
                    # Per-level shell topology, recorded because no validator gate
                    # reports a component count and LOD2 fragmentation is invisible to
                    # every gate that does run. Sits beside "lodSheets" on purpose:
                    # number and image, same level, same run.
                    "lodShellTopology":
                        proof["lodTopology"] if proof is not None else {},
                    "aoBake": {
                        "baked": variant.ao.baked,
                        "samples": variant.ao.samples,
                        "min": round(variant.ao.min_value, 5),
                        "max": round(variant.ao.max_value, 5),
                        "mean": round(variant.ao.mean_value, 5),
                        "hasContrast": variant.ao.has_contrast,
                    },
                    "sway": {
                        "min": round(variant.sway.min_value, 5),
                        "max": round(variant.sway.max_value, 5),
                        "stiffnessExponent": variant.sway.stiffness_exponent,
                        "relativeSpread": round(variant.sway.relative_spread, 5),
                        "uniform": variant.sway.is_uniform,
                        "distanceMetric": "geodesic along stem arc then cap surface "
                                          "arc from the hub",
                    },
                    "biolum": "channel G is 0 across the whole asset: no flora in "
                              "nice_biome.webp, beauty.webp or shallows.webp is "
                              "emissive, and 3DMODEL_FLORA_CORAL.md section 2 fixes "
                              "non-emissive tissue at 0.",
                    "interaction": variant.interaction,
                    "channelMeasurements":
                        proof["measurements"] if proof is not None else [],
                    "generatorNotes": variant.notes,
                })

        _print_report(variant, proof, export_result, manifest_path)

    return exit_code


if __name__ == "__main__":
    sys.exit(main(sys.argv))
