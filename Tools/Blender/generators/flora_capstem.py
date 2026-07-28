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

    def quad(self, a: int, b: int, c: int, d: int, material: int,
             uvs: Sequence[Tuple[int, float, float]]) -> None:
        self.face((a, b, c, d), material, uvs)


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
    segments_per_rib = 2 + int(round(2.0 * q))          # 2..4
    stem_rings = 5 + int(round(6.0 * q))                # 5..11
    cap_top_rings = 4 + int(round(4.0 * q))             # 4..8
    cap_bottom_rings = 3 + int(round(3.0 * q))          # 3..6
    rim_rings = 1 + int(round(2.0 * q))                 # 1..3

    stem_count = 2 + int(round(1.6 * q))                # 2..4 (a clump, never one)
    if rng.random() < 0.28:
        stem_count = max(2, stem_count - 1)

    current = Vector((_rng_range(rng, -1.0, 1.0), _rng_range(rng, -1.0, 1.0), 0.0))
    if current.length <= 1e-4:
        current = Vector((1.0, 0.0, 0.0))
    current.normalize()

    rib_count = int(7 + round(_rng_range(rng, 0.0, 6.0)))   # 7..13
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
        tears: List[Tuple[float, float, float]] = []
        if not juvenile:
            for _ in range(1 + int(rng.integers(0, 3))):
                tears.append((
                    _rng_range(rng, 0.0, math.tau),
                    _rng_range(rng, 0.10, 0.42),
                    _rng_range(rng, 0.16, 0.44),
                ))

        stems.append(StemPlan(
            base_offset=base,
            height=stem_height,
            cap_radius=stem_cap_radius,
            stem_radius=stem_height * _rng_range(rng, 0.036, 0.058),
            bend=_rng_range(rng, 0.10, 0.30),
            lean=lean,
            tilt_deg=_rng_range(rng, 9.0, 34.0),
            cap_offset_frac=_rng_range(rng, 0.10, 0.32),
            rib_count=rib_count,
            rib_phase=_rng_range(rng, 0.0, math.tau),
            lobe_amplitude=_rng_range(rng, 0.055, 0.115),
            rib_relief=_rng_range(rng, 0.030, 0.070),
            gill_depth=_rng_range(rng, 0.24, 0.44),
            cup_sign=1.0 if rng.random() < 0.62 else -1.0,
            cup_amplitude=_rng_range(rng, 0.14, 0.34),
            thickness_hub=_rng_range(rng, 0.115, 0.170),
            thickness_rim=_rng_range(rng, 0.040, 0.072),
            tear_sectors=tuple(tears),
            edge_jitter=tuple(float(x) for x in rng.normal(0.0, 0.016, size=segments)),
            ridge_count=int(4 + round(_rng_range(rng, 0.0, 3.0))),
            ridge_phase=_rng_range(rng, 0.0, math.tau),
            finger_count=int(5 + round(_rng_range(rng, 0.0, 3.0))),
            finger_phase=_rng_range(rng, 0.0, math.tau),
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
        hub_fraction=0.17,
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
    for i in range(samples):
        t = i / float(samples - 1)
        # Vertical rise slightly eased so the neck is not a straight ramp.
        rise = plan.height * (t ** 0.94)
        lateral = lean * (plan.height * plan.bend * (t ** 1.85))
        positions.append(plan.base_offset + Vector((0.0, 0.0, rise)) + lateral)

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
        out.append((positions[i], tangent.normalized(), i / float(samples - 1),
                    arclength))
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

    foot_span = 0.14
    if t < foot_span:
        k = (1.0 - t / foot_span) ** 2.0
        flare = 1.0 + 1.55 * k
        fingers = 1.0 + 0.52 * k * math.cos(plan.finger_count * theta
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
    radius = plan.cap_radius * lobe

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
    hub_ring = [cap_point(hub_u, j, True) for j in range(segments)]

    ring_indices: List[List[int]] = []
    ring_circumference: List[float] = []
    for i, (position, tangent, t, arclength) in enumerate(axis):
        frame_x, frame_y, _frame_z = _basis_from_up(tangent, clump.current_dir)
        natural = []
        for j in range(segments):
            theta = thetas[j]
            radius = _stem_profile_radius(plan, t, theta)
            radius *= 1.0 + 0.10 * noise.sample(t, theta / math.tau)
            natural.append(position + frame_x * (radius * math.cos(theta))
                           + frame_y * (radius * math.sin(theta)))
        if i == len(axis) - 1:
            points = hub_ring
        elif i == len(axis) - 2:
            # Blend the penultimate ring toward the hub ring pulled back along the
            # tangent, so the weld is smooth instead of a collar step.
            step = (axis[-1][0] - position).length
            points = [_lerp_v(natural[j], hub_ring[j] - tip_tangent * step, 0.5)
                      for j in range(segments)]
        else:
            points = natural

        circumference = 0.0
        for j in range(segments):
            circumference += (points[(j + 1) % segments] - points[j]).length
        ring_circumference.append(circumference)

        harvest = _smoothstep((t - 0.74) / 0.26)
        radius_estimate = max(1e-4, circumference / math.tau)
        indices = [accum.vert(points[j], arclength, harvest, radius_estimate)
                   for j in range(segments)]
        ring_indices.append(indices)

    # ---- foot: flat n-gon closing the bottom ----------------------------
    foot_centre = accum.vert(axis[0][0], 0.0, 0.0,
                             max(1e-4, ring_circumference[0] / math.tau))
    foot_radius = ring_circumference[0] / math.tau
    for j in range(segments):
        k = (j + 1) % segments
        offset_a = accum.positions[ring_indices[0][j]] - axis[0][0]
        offset_b = accum.positions[ring_indices[0][k]] - axis[0][0]
        accum.face(
            (foot_centre, ring_indices[0][k], ring_indices[0][j]), SLOT_STEM,
            ((island_foot, 0.0, 0.0),
             (island_foot, offset_b.x, offset_b.y),
             (island_foot, offset_a.x, offset_a.y)))

    # ---- stem bands -----------------------------------------------------
    v_offsets = [row[3] for row in axis]
    for i in range(len(ring_indices) - 1):
        lower = ring_indices[i]
        upper = ring_indices[i + 1]
        for j in range(segments):
            k = (j + 1) % segments
            frac_j = j / float(segments) - 0.5
            frac_k = (j + 1) / float(segments) - 0.5
            u_lo_j = frac_j * ring_circumference[i]
            u_lo_k = frac_k * ring_circumference[i]
            u_hi_j = frac_j * ring_circumference[i + 1]
            u_hi_k = frac_k * ring_circumference[i + 1]
            accum.quad(
                lower[j], lower[k], upper[k], upper[j], SLOT_STEM,
                ((island_stem, u_lo_j, v_offsets[i]),
                 (island_stem, u_lo_k, v_offsets[i]),
                 (island_stem, u_hi_k, v_offsets[i + 1]),
                 (island_stem, u_hi_j, v_offsets[i + 1])))

    # ---- cap underside: hub ring outward to the rim ----------------------
    bottom_rings: List[List[int]] = [ring_indices[-1]]
    bottom_arc = [[0.0] * segments]
    previous_points = hub_ring
    accumulated = [0.0] * segments
    for u in bottom_us[1:]:
        points = [cap_point(u, j, True) for j in range(segments)]
        for j in range(segments):
            accumulated[j] += (points[j] - previous_points[j]).length
        bottom_arc.append(list(accumulated))
        indices = [
            accum.vert(points[j], stem_length + accumulated[j], 1.0,
                       _cap_thickness(plan, u, thetas[j]))
            for j in range(segments)
        ]
        bottom_rings.append(indices)
        previous_points = points

    # Radial arc distance from the hub, plus the hub's own radius, gives a geodesic
    # polar map for the underside. Hub arc offset keeps the innermost ring off the
    # polar singularity, so no UV triangle collapses to zero area.
    hub_arc = hub_u * plan.cap_radius
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

    # Top surface UV: geodesic polar map measured OUTWARD from the rim, converted to a
    # radius from the apex so the island is a disc rather than an annulus.
    top_total = [max(1e-5, top_arc[-1][j]) for j in range(segments)]
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

    rim_circumference = 0.0
    for j in range(segments):
        rim_circumference += (accum.positions[rim_top[(j + 1) % segments]]
                              - accum.positions[rim_top[j]]).length
    for i in range(len(rim_rows) - 1):
        upper = rim_rows[i]
        lower = rim_rows[i + 1]
        for j in range(segments):
            k = (j + 1) % segments
            u_j = (j / float(segments) - 0.5) * rim_circumference
            u_k = ((j + 1) / float(segments) - 0.5) * rim_circumference
            # v is the real distance across the band at THAT angle, so a thin torn
            # sector does not get the same texel run as a thick one.
            accum.quad(
                upper[j], lower[j], lower[k], upper[k], SLOT_RIM,
                ((island_rim, u_j, rim_fraction[i] * band_thickness[j]),
                 (island_rim, u_j, rim_fraction[i + 1] * band_thickness[j]),
                 (island_rim, u_k, rim_fraction[i + 1] * band_thickness[k]),
                 (island_rim, u_k, rim_fraction[i] * band_thickness[k])))

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
        "islands": [island_stem, island_foot, island_bottom, island_rim, island_top],
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
