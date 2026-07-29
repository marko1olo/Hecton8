"""Small schooling fish -- HECTON-8 offline asset forge, FAUNA family.

Family: ``law.Family.FAUNA``. Surface class ``ORGANIC`` (``law.FAMILY_SURFACE_CLASS``).
Route: ``3dmodel.md`` -> ``PROCEDURAL_ASSET_PIPELINE.md`` -> ``3DMODEL_FAUNA.md``.

WHY THIS FILE EXISTS. Before it there was NO fauna geometry in the project -- verified
independently, not taken on report: ``Assets/_Project/Art/Fauna`` does not exist, and
every ``.fbx``/``.obj``/``.blend`` under ``Assets/_Project`` is flora, geology, rock,
small-prop or prologue architecture. The files matching *creature* are ``Archetype_*.asset``
ScriptableObjects, which are AI data and not meshes. Meanwhile the entire swarm chain
downstream is already wired: ``MAT_SargassumMicroFaunaBoids`` is bound as
``Ocean_Crest.prefab``'s boid material, its shader declares ``_VatPositionTex`` /
``_VatNormalTex``, and ``FaunaHeadlessBake1610.BakeFromCommandLine`` will bake a VAT from
a mesh. What sat in ``Ocean_Crest.prefab:615`` as the boid mesh was a Unity BUILT-IN
PRIMITIVE (``fileID 10209``), and a VAT cannot be baked from a built-in primitive. So the
chain was dead for want of one mesh.

============================================================================
THE CONSUMER CONTRACT. Read this before changing any dimension.
============================================================================

This mesh is not free-standing art; it feeds a VAT bake and an instanced indirect draw,
and three of those constraints fail SILENTLY. They are recorded here because nothing in
the pipeline validates them.

*   **Body axis MUST be strictly longest along local Z.**
    ``AbyssalAnatomyStudio1610.AnalyzeAxis`` picks the longest bounds axis with priority
    X -> Y -> Z and ``>=`` comparisons, so an X/Z tie awards X. If Z is not strictly the
    longest, the bake shears the fish sideways or vertically and NOTHING errors.
    ``_assert_consumer_contract`` below turns that into a hard abort.

*   **Nose is +Z, tail is -Z, up is +Y, lateral is X.**
    ``BoidFishInstanced.shader:12-18`` states this as the model convention, and
    ``BuildLookRotation`` aligns local +Z to velocity. A fish authored nose-down or
    nose-along-X swims backwards or sideways with every null check passing.

*   **Local extent is calibrated to the shader's ``saturate()`` terms, so the mesh is
    authored 2 units long, NOT 0.28 m.** Three live shader terms pin this:
    ``tailFactor = saturate(-localPos.z)`` reaches 1.0 only at ``z == -1``;
    the fin mask saturates near ``|x| ~ 0.556``; ``colorBlend = saturate(-positionOS.y
    - _BellyBlend)`` is full only at ``y <= -1``. ``_FishScale`` (material value 0.28)
    is the metre conversion, so a 2-unit mesh renders about 0.56 m. The VAT bake agrees:
    ``amplitudeMeters = max(0.01, axis.Length * 0.035)`` is proportional only while body
    length exceeds 0.2857 units, and a true-metre 0.28-unit fish sits exactly on that
    floor where the wave becomes a fixed absolute displacement. AUTHORING IN METRES HERE
    WOULD BE THE WRONG CHOICE FOR THIS CONSUMER, and the lead owns ``_FishScale`` if a
    smaller fish is wanted -- it is a material value, not geometry.

*   **ONE material slot, and that is a requirement rather than a simplification.**
    ``SargassumMicroFaunaBoids.cs:8844-8847`` builds the indirect args from submesh 0
    only and issues ``commandCount: 1``, so submeshes 1+ are silently never drawn. A
    second slot would ship an invisible eye. ``3DMODEL_FAUNA.md`` section 5 authorises
    this directly -- "Instancing and SRP Batcher compatibility matter more than arbitrary
    material variety" -- and section 5's eye/organ zones are carried by geometry plus the
    vertex-colour masks instead.

*   **Vertex ORDER is the VAT contract.** The VAT column index IS the mesh vertex index;
    the bake is a verbatim ``Object.Instantiate`` of this mesh. Nothing hashes vertex
    order, so a reorder is silent corruption of every baked page. Setting
    ``identity.family = FAUNA`` is what protects this: ``export_unity`` treats FAUNA as a
    VAT family and emits ``optimizeMeshVertices=False``, ``weldVertices=False``,
    ``meshOptimizationFlags="PolygonOrder"`` in the importer block. Do not "tidy" the
    build order.

*   **UV0 only, and normals are mandatory in practice.** The shader's ``Attributes``
    struct declares POSITION, NORMAL, TEXCOORD0, COLOR and nothing else -- the vertex id
    comes from ``SV_VertexID``, so no channel carries it. A missing normal stream is only
    a LogWarning in the bake, which then writes every normal texel as 0.5 (a zero-length
    vector). That is the silent-degeneracy class this project treats as a defect.

============================================================================
TRIANGLE BUDGET -- derived, with the derivation recorded
============================================================================

``law.LOD_BUDGETS[Family.FAUNA] = LodBudget(35_000, 12_000, 2_000, 12, 500)`` and
``3dmodel.md`` section 7 mirrors it. But ``3dmodel.md:212`` says those are "hard maxima,
not targets", and 35 000 is a HERO CREATURE BODY. A swarm fish is the opposite case:

*   ``BoidFishInstanced.shader:32`` records the author's own working figure --
    "2000 fish x 200 tris = 400K tris, 1 draw call".
*   ``REND_GPU_Driven_Animation_VAT.txt:138`` caps TIER_LOW (MX350) at 2048 boids.
*   ``Ocean_Crest.prefab:635`` currently authors ``boidCount: 128``.
*   The VAT itself bounds vertices, not triangles: width == vertexCount <=
    ``SystemInfo.maxTextureSize``, and ``vertexCount * frameCount <= 2^20`` from the
    32 MB compact guard. At the default 30 frames that is ~16 384 vertices -- three
    orders of magnitude above anything sane here, so the VAT is NOT the binding limit.
*   ``law.LOD_BUDGETS[FAUNA].impostor_max == 500`` is the only law number in the right
    order of magnitude for a mass-instanced body.

So the binding number is the shader's 200 tris/fish, and this generator targets LOD0 at
roughly 2x that to carry a readable LOD0 silhouette, letting the chain land LOD1 near the
200-tri swarm figure. LOD1 is therefore the swarm-cost-matched level and LOD0 the near
pass. Every level is far under the law maxima, which is the correct relationship: the
maxima are a ceiling this asset class should never approach.

============================================================================
VERTEX COLOUR -- the ORGANIC contract, unchanged
============================================================================

``law.ORGANIC_VCOL = ("sway_amplitude", "biolum_phase", "baked_ao", "family_specific")``
and ``3DMODEL_FAUNA.md`` section 4 agree channel for channel. No new packing is invented:

*   R = deformation amplitude. 0.0 at the snout (the rigid anchor -- ``validate.py``'s
    ``organic_sway_anchor_missing`` gate fires on ORGANIC surfaces and needs the anchor
    value to actually occur), rising aft, and HIGH on every fin membrane. Section 4 says
    "Rigid shell = low. Fins/tentacles = high", and ``BoidFishInstanced.shader:548``
    reads ``saturate(input.color.r)`` as its fin-stretch mask -- the two agree once R is
    authored as deformation amplitude rather than as an axial ramp.
*   G = 0.0 across the whole fish. Sargassum micro-fauna in the photic shallows is not
    emissive, and section 4 reserves G for a biolum mask/phase. Painting a glow no
    reference shows would be decoration, which section 9 rejects outright.
*   B = ray-traced baked AO, from ``vertexcolor.bake_ambient_occlusion``.
*   A = counter-shading blend, 0.0 on the dorsal ridge to 1.0 on the ventral belly.
    Section 4 permits "shader blend mask" in A, and counter-shading is the real anatomy
    of a schooling fish rather than an invented channel.

Determinism: ``numpy.random.default_rng(seed)`` only. ``mathutils.noise`` is banned --
its seed is process-global and two generators in one session corrupt each other's stream.

    blender.exe -b --factory-startup -P Tools/Blender/generators/fauna_fish.py -- \
        --seed 2207 --quality 1.0
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
from mathutils import Vector

# The package is not on sys.path under `blender -b -P <script>`; this file lives at
# <root>/Tools/Blender/generators/, so the package root is one directory up.
_TOOLS_BLENDER = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if _TOOLS_BLENDER not in sys.path:
    sys.path.insert(0, _TOOLS_BLENDER)

from h8forge import export_unity, law, mesh_ops, preview, validate, vertexcolor
from h8forge.blackbox import BlackBox, GenerationAborted

GENERATOR_NAME = "fauna_fish.py"
GENERATOR_VERSION = "1.0.0"

FAMILY = law.Family.FAUNA
SURFACE = law.FAMILY_SURFACE_CLASS[FAMILY]

# Atlas page for the island-pixel and border-padding gates. Passing a real atlas size to
# the validator is what makes those two gates FIRE instead of recording themselves as
# "not enforced"; 1024 is chosen over 2048 because this body is a 200-tri swarm unit and
# a 2048 page would be mostly empty reserve.
ATLAS_SIZE = 1024

# TEXEL DENSITY -- and a recorded law.py GAP rather than a local hardcode.
#
# `law.py` carries TEXEL_DENSITY_HERO_FLORA (512), TEXEL_DENSITY_COMMON_FLORA (256) and
# the field-HLOD band, ALL of them flora-named and cited to 3DMODEL_FLORA_CORAL.md
# section 5. `3DMODEL_FAUNA.md` states no px/m figure at all, and there is no fauna row
# in law.py. The pipeline rule is that every threshold lives once in law.py with its
# bible citation and that a local copy is drift -- so this generator does NOT invent a
# number. It uses the existing COMMON-INSTANCED row, which is the class a swarm fish
# actually belongs to, and the missing fauna row is reported to the lead as an h8forge
# diff instead of being papered over here.
#
# Density barely matters for this consumer in any case: MAT_SargassumMicroFaunaBoids
# ships `_BaseMap: {fileID: 0}` (null, so the shader's "white" default applies), the
# material type is Unlit, and there is no PBR texture set anywhere in the chain. UV0
# exists to satisfy 3dmodel.md section 3 and to keep the atlas gates enforceable.
TEXEL_DENSITY = law.TEXEL_DENSITY_COMMON_FLORA

# A swarm body is seen from metres away in a shoal, not inspected in the hand.
CAMERA_DISTANCE_CLASS = "mid_field_instanced"
PLATFORM_LANE = "compact_to_ultra"

# 3dmodel.md section 5 requires the alpha meaning as a manifest string, and
# export_unity.write_manifest records a manifestGap (forcing productionReady=False) if an
# ORGANIC asset leaves it blank.
ALPHA_MEANING = (
    "counter_shading_blend: 0.0 along the dorsal ridge rising to 1.0 across the ventral "
    "belly, following the real counter-shading of a schooling fish. 3DMODEL_FAUNA.md "
    "section 4 permits a shader blend mask in A. A shader may lerp a dark dorsal colour "
    "to a pale belly with it; BoidFishInstanced.shader currently derives the same effect "
    "from local Y instead and does not read A, so this channel is authored to the bible "
    "contract and is not yet consumed."
)

REFERENCE_IDS = (
    "3DMODEL_FAUNA.md sections 1-11 (body plan, deformation topology, channel "
    "contract, LOD and hitbox law)",
    "Assets/_Project/Scripts/BoidFishInstanced.shader:12-18 (model axis convention) "
    "and :517/:547/:586 (local-extent calibration)",
    "Assets/_Project/Editor/Generators/Fauna/AbyssalAnatomyStudio1610.cs:1672-1698 "
    "(AnalyzeAxis longest-bounds-axis selection) and :1039 (VAT width == vertexCount)",
)

# ---------------------------------------------------------------------------
# Material slots -- 3dmodel.md section 6, 3DMODEL_FAUNA.md section 5
# ---------------------------------------------------------------------------
# ONE slot. See the consumer contract in the module docstring: the indirect draw path
# renders submesh 0 only, so any further slot is invisible geometry. `_gate_materials`
# also rejects a DECLARED slot that carries no triangle, so reserving slots "for later"
# would fail validation rather than sit harmlessly.
SLOT_BODY = law.MATERIAL_SLOT_PRIMARY
MATERIAL_ROLES = ("FishTissue",)


# ---------------------------------------------------------------------------
# Small maths helpers
# ---------------------------------------------------------------------------

def _smoothstep(x: float) -> float:
    x = min(1.0, max(0.0, x))
    return x * x * (3.0 - 2.0 * x)


def _lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def _rng_range(rng, lo: float, hi: float) -> float:
    return float(lo + (hi - lo) * rng.random())


def _signed_pow(value: float, exponent: float) -> float:
    """``sign(v) * |v| ** e`` -- the superellipse term, safe at v == 0."""
    if value >= 0.0:
        return math.pow(value, exponent)
    return -math.pow(-value, exponent)


# ---------------------------------------------------------------------------
# Accumulator
# ---------------------------------------------------------------------------

@dataclass
class _Accum:
    """Plain lists, not a live BMesh.

    Every vertex carries two authored scalars (deformation amplitude and the
    counter-shading blend) that are INDEX-ALIGNED to build order. A BMesh merge would
    silently desynchronise them from the geometry, and for this family the build order is
    additionally the VAT column order, so it is doubly load-bearing.
    """

    positions: List[Vector] = field(default_factory=list)
    flex: List[float] = field(default_factory=list)
    belly: List[float] = field(default_factory=list)
    faces: List[Tuple[int, ...]] = field(default_factory=list)
    face_material: List[int] = field(default_factory=list)
    face_uv: List[List[Tuple[int, float, float]]] = field(default_factory=list)
    face_region: List[str] = field(default_factory=list)
    region: str = "unset"

    def vert(self, position: Vector, flex: float, belly: float) -> int:
        index = len(self.positions)
        self.positions.append(position.copy())
        self.flex.append(law.saturate(flex))
        self.belly.append(law.saturate(belly))
        return index

    def face(self, indices: Sequence[int], material: int,
             uvs: Sequence[Tuple[int, float, float]]) -> None:
        if len(indices) != len(uvs):
            raise GenerationAborted(
                "face has {0} corners and {1} uv corners in region {2}".format(
                    len(indices), len(uvs), self.region))
        if len(set(indices)) != len(indices):
            # Catch the degenerate face AT the parametrisation that produced it rather
            # than hundreds of lines later as GATE_DEGENERATE_TRIANGLE with no provenance.
            raise GenerationAborted(
                "face repeats a vertex index {0} in region {1}".format(
                    tuple(indices), self.region))
        self.faces.append(tuple(indices))
        self.face_material.append(material)
        self.face_uv.append([tuple(uv) for uv in uvs])
        self.face_region.append(self.region)

    def quad(self, a: int, b: int, c: int, d: int, material: int,
             uvs: Sequence[Tuple[int, float, float]]) -> None:
        self.face((a, b, c, d), material, uvs)

    def triangulate(self) -> int:
        """Fan every polygon. Vertex order is NEVER touched -- see the VAT contract.

        Triangulating at authoring time rather than at export is required, not tidy:
        ``export_unity`` must write ``use_triangles=True`` because ``use_tspace``
        silently refuses to build a tangent basis on a mesh containing an n-gon, and
        ``verify_fbx_roundtrip`` then compares PER-CORNER data. A quad source can never
        match its own triangulated re-import, and a failed round trip DELETES the package.
        """
        faces: List[Tuple[int, ...]] = []
        material: List[int] = []
        uvs: List[List[Tuple[int, float, float]]] = []
        regions: List[str] = []
        added = 0
        for index, corners in enumerate(self.faces):
            corner_uv = self.face_uv[index]
            if len(corners) == 3:
                faces.append(corners)
                material.append(self.face_material[index])
                uvs.append(corner_uv)
                regions.append(self.face_region[index])
                continue
            for k in range(1, len(corners) - 1):
                faces.append((corners[0], corners[k], corners[k + 1]))
                material.append(self.face_material[index])
                uvs.append([corner_uv[0], corner_uv[k], corner_uv[k + 1]])
                regions.append(self.face_region[index])
                added += 1
        self.faces = faces
        self.face_material = material
        self.face_uv = uvs
        self.face_region = regions
        return added


# ---------------------------------------------------------------------------
# Body plan  --  3DMODEL_FAUNA.md section 2 (declared), section 3 (deformation topology)
# ---------------------------------------------------------------------------
#
# Landmarks in local Z, nose at +1.0 and caudal tips at -1.0. These are the numbers the
# silhouette is judged on, so they are named rather than buried in expressions:
#
#   +1.00  snout tip                      -0.60  caudal peduncle / fin root (NARROWEST)
#   +0.86  jaw line                       -0.80  caudal fork notch
#   +0.52  operculum (gill cover) step    -1.00  caudal lobe tips
#   +0.28  maximum body depth  <-- FORWARD OF CENTRE, which is what makes it fusiform
#
# The "widest point forward of centre" requirement is met by putting maximum depth at
# z = +0.28 on a body whose mid-point is z = +0.20 ((1.0 + -0.60)/2): the peak sits
# forward of the body's own centre AND forward of the overall mesh centre at z = 0.

Z_SNOUT = 1.00
Z_JAW = 0.86
Z_OPERCULUM = 0.52
Z_MAX_DEPTH = 0.28
Z_PEDUNCLE = -0.60
Z_FORK_NOTCH = -0.80
Z_LOBE_TIP = -1.00


@dataclass
class FishPlan:
    """Every authored dimension for one fish, resolved from seed and quality."""

    body_depth: float          # max half-height of the body, local units
    compression_head: float    # half-width / half-height at the head (rounder)
    compression_tail: float    # ... at the peduncle (flatter)
    peduncle_depth: float      # half-height at the caudal root
    snout_depth: float         # half-height where the nose cap closes
    section_fullness: float    # superellipse exponent driver; 2.0 == ellipse
    dorsal_bias: float         # dorsal half-height scale (< 1 -> sharper ridge)
    ventral_bias: float        # ventral half-height scale (> 1 -> rounder belly)
    belly_drop: float          # how far the section centre sinks at mid-body
    lobe_span: float           # caudal lobe tip half-height
    fork_depth: float          # notch inset, drives how FORKED the tail reads
    caudal_thickness: float    # membrane half-width at the trailing edge
    ring_segments: int         # vertices around one ring (even, >= 8)
    body_rings: int
    tail_rings: int
    dorsal_span: Tuple[float, float]
    dorsal_height: float
    anal_span: Tuple[float, float]
    anal_height: float
    pectoral_z: float
    pectoral_length: float
    eye_z: float
    eye_y: float
    eye_radius: float
    asymmetry: float           # deterministic left/right break, bible section 2


def plan_fish(rng, *, quality: float) -> FishPlan:
    """Resolve the body plan. Quality scales DENSITY, never identity.

    ``3DMODEL_FAUNA.md`` section 6: GlobalQualityWeight may scale LOD0 segment density and
    membrane subdivision but "must not change hitbox truth, attack reach, weak spot
    identity, locomotion authority". So every dimension below is seeded, and only the ring
    and segment counts read ``quality``.
    """
    q = law.saturate(quality)

    # Ring segments must stay EVEN so the dorsal ridge and ventral seam both land on a
    # vertex, which is what keeps the UV seam on the belly midline and the sharp dorsal
    # crease on a real edge instead of across a face.
    ring_segments = int(round(_lerp(8, 12, q) / 2.0)) * 2
    body_rings = int(round(_lerp(9, 13, q)))
    tail_rings = int(round(_lerp(3, 4, q)))

    return FishPlan(
        # depth:length of 0.30 on a 2.0 body reads as a schooling fish -- herring sit
        # near 0.22 and jacks near 0.35.
        body_depth=_rng_range(rng, 0.275, 0.315),
        # LATERALLY COMPRESSED, which the brief calls out explicitly: schooling fish are
        # not round in cross-section. Width/depth stays well under 1 everywhere.
        compression_head=_rng_range(rng, 0.54, 0.60),
        compression_tail=_rng_range(rng, 0.27, 0.32),
        peduncle_depth=_rng_range(rng, 0.070, 0.082),
        snout_depth=_rng_range(rng, 0.028, 0.040),
        section_fullness=_rng_range(rng, 2.15, 2.35),
        dorsal_bias=_rng_range(rng, 0.90, 0.95),
        ventral_bias=_rng_range(rng, 1.06, 1.12),
        belly_drop=_rng_range(rng, 0.028, 0.042),
        lobe_span=_rng_range(rng, 0.31, 0.36),
        fork_depth=_rng_range(rng, 0.185, 0.215),
        caudal_thickness=_rng_range(rng, 0.005, 0.008),
        ring_segments=ring_segments,
        body_rings=body_rings,
        tail_rings=tail_rings,
        dorsal_span=(_rng_range(rng, 0.30, 0.38), _rng_range(rng, -0.06, 0.02)),
        dorsal_height=_rng_range(rng, 0.15, 0.19),
        anal_span=(_rng_range(rng, -0.04, -0.10), _rng_range(rng, -0.30, -0.38)),
        anal_height=_rng_range(rng, 0.10, 0.13),
        pectoral_z=_rng_range(rng, 0.34, 0.42),
        pectoral_length=_rng_range(rng, 0.15, 0.19),
        eye_z=_rng_range(rng, 0.70, 0.76),
        eye_y=_rng_range(rng, 0.055, 0.075),
        eye_radius=_rng_range(rng, 0.042, 0.052),
        asymmetry=_rng_range(rng, 0.006, 0.014),
    )


def _body_half_height(plan: FishPlan, t: float) -> float:
    """Fusiform depth profile. ``t`` is 0 at the caudal root and 1 at the snout.

    Two arcs meeting at the depth peak, and the EXPONENTS are the fish-ness:

    *   aft of the peak the rise from the peduncle is gradual (exponent < 1 fills the
        mid-body out rather than letting it taper linearly into a cone);
    *   forward of the peak the fall to the snout is CONVEX, which is what makes a head
        instead of a spike -- a linear taper there is the "scaled capsule" that
        ``3DMODEL_FAUNA.md`` section 1 rejects outright.
    """
    t_peak = (Z_MAX_DEPTH - Z_PEDUNCLE) / (Z_SNOUT - Z_PEDUNCLE)
    if t <= t_peak:
        u = t / max(1e-6, t_peak)
        return plan.peduncle_depth + (plan.body_depth - plan.peduncle_depth) * math.pow(
            u, 0.62)
    u = (t - t_peak) / max(1e-6, 1.0 - t_peak)
    return plan.snout_depth + (plan.body_depth - plan.snout_depth) * math.pow(
        1.0 - u, 0.70)


def _body_compression(plan: FishPlan, t: float) -> float:
    """Half-width / half-height. Head rounder, caudal region flatter."""
    return _lerp(plan.compression_tail, plan.compression_head, math.pow(t, 0.80))


def _section_centre_y(plan: FishPlan, t: float) -> float:
    """Vertical offset of the cross-section centre.

    The spine of a fish sits ABOVE the mid-line of its cross-section, so the section
    centre sinks toward mid-body. This is also half of the deterministic asymmetry
    ``3DMODEL_FAUNA.md`` section 2 requires: "Symmetry may be used for base construction,
    but final mesh must add deterministic asymmetry."
    """
    return -plan.belly_drop * math.sin(math.pi * math.pow(law.saturate(t), 0.90))


def _operculum_step(plan: FishPlan, z: float) -> float:
    """A small radial swell just aft of the gill cover.

    ``3DMODEL_FAUNA.md`` section 2 requires "silhouette contrast: thick mass, thin
    appendage, fin/membrane, mouth/jaw or sensing organ, and material breaks" and section
    3 requires "Mouths, gills, eyes, vents ... require separate loops or material
    borders". With one material slot the border has to be GEOMETRY, so the operculum is a
    real step in the surface rather than a texture seam.
    """
    width = 0.085
    delta = (z - Z_OPERCULUM) / width
    if abs(delta) > 1.6:
        return 1.0
    return 1.0 + 0.055 * math.exp(-delta * delta) * (1.0 if delta > 0.0 else 0.55)


# ---------------------------------------------------------------------------
# Geometry
# ---------------------------------------------------------------------------
#
# ISLAND IDS. One per UV island; pack_islands resolves them to packed 0..1 UVs.
ISLAND_BODY = 0
ISLAND_DORSAL = 1
ISLAND_ANAL = 2
ISLAND_PECTORAL_L = 3
ISLAND_PECTORAL_R = 4
ISLAND_EYE_L = 5
ISLAND_EYE_R = 6
# The caudal fin is its OWN island, and that is a parameterisation decision rather than a
# packing one. Its cross-sections are flat blades, not tube rings: continuing the body's
# cylindrical unwrap through them means one band has to bridge the peduncle ring's small
# circumference and the trailing edge's much longer outline, and that ratio IS aspect
# distortion. Measured on the continuous version: worst 202.50 against a 3.30 ceiling with
# 3.33% of total mesh area over the limit from the 24 trailing-edge triangles alone.
# Projected flat onto its own plane the same surface is near-isometric.
ISLAND_CAUDAL = 7
ISLAND_COUNT = 8


def _flex_from_z(z: float) -> float:
    """Deformation amplitude from axial position.

    Zero forward of maximum body depth -- the head and snout are the rigid part a swim
    cycle pivots AROUND, and ``validate.py``'s ``organic_sway_anchor_missing`` gate needs
    the anchor value 0.0 to actually occur on an ORGANIC surface. Rises monotonically aft
    to 1.0 at the caudal tips, which is where a carangiform swimmer's amplitude lives.
    """
    return law.saturate((Z_MAX_DEPTH - z) / (Z_MAX_DEPTH - Z_LOBE_TIP))


def _ring_angles(segments: int) -> List[float]:
    """Ring angles with the VENTRAL midline at index 0 and the DORSAL ridge at N/2.

    Both poles land exactly on a vertex, which is what lets the UV seam sit on the belly
    (least visible, the analogue of the flora bible's "seam on the least visible rear
    side") and lets the dorsal ridge be a real edge rather than a crease across a face.
    Requires an EVEN segment count, enforced in ``plan_fish``.
    """
    return [-0.5 * math.pi + 2.0 * math.pi * j / float(segments)
            for j in range(segments)]


def _body_ring(plan: FishPlan, t: float, angles: Sequence[float]) -> List[Vector]:
    """One superellipse cross-section of the body at parameter ``t``."""
    z = _lerp(Z_PEDUNCLE, Z_SNOUT - 0.04, t)
    half_h = _body_half_height(plan, t) * _operculum_step(plan, z)
    half_w = half_h * _body_compression(plan, t)
    centre = _section_centre_y(plan, t)
    exponent = 2.0 / plan.section_fullness
    points = []
    for phi in angles:
        cx = _signed_pow(math.cos(phi), exponent)
        cy = _signed_pow(math.sin(phi), exponent)
        bias = plan.dorsal_bias if math.sin(phi) > 0.0 else plan.ventral_bias
        # Deterministic left/right break -- 3DMODEL_FAUNA.md section 2 requires the final
        # mesh to add asymmetry even when construction is symmetric.
        skew = plan.asymmetry * math.sin(phi) if math.cos(phi) > 0.0 else 0.0
        points.append(Vector((half_w * cx + skew, centre + half_h * bias * cy, z)))
    return points


def _caudal_ring(plan: FishPlan, tau: float, angles: Sequence[float],
                 root: Sequence[Vector]) -> List[Vector]:
    """A caudal ring: the peduncle section morphing toward the forked trailing edge.

    THE FORK IS BUILT HERE, and it is built by letting Z vary WITH THE RING ANGLE rather
    than holding each ring at one Z. Every ring vertex migrates toward its own trailing-
    edge Z, deepest at the lobe tips and shallowest at the notch. A constant-Z ring cannot
    produce a forked tail at all -- it produces a paddle.
    """
    z_notch = Z_LOBE_TIP + plan.fork_depth
    exponent = 2.0 / plan.section_fullness
    points = []
    for index, phi in enumerate(angles):
        v = math.sin(phi)
        z_trail = z_notch + (Z_LOBE_TIP - z_notch) * math.pow(abs(v), 1.10)
        y_trail = plan.lobe_span * v
        x_trail = plan.caudal_thickness * _signed_pow(math.cos(phi), exponent)
        base = root[index]
        points.append(Vector((_lerp(base.x, x_trail, tau),
                              _lerp(base.y, y_trail, tau),
                              _lerp(base.z, z_trail, tau))))
    return points


def _belly_of(phi: float) -> float:
    """Counter-shading blend from ring angle: 1.0 ventral, 0.0 dorsal."""
    return law.saturate(0.5 - 0.5 * math.sin(phi))


def _build_body(accum: _Accum, plan: FishPlan) -> dict:
    """Nose cap, body tube, caudal fin, and the closed forked trailing edge.

    ONE closed manifold from snout to tail rim. Ring topology is held through the caudal
    transition on purpose: ``3DMODEL_FAUNA.md`` section 3 requires "ring sections with
    consistent vertex order for VAT/bend shader compatibility" and "even longitudinal
    segments along bendable parts", and holding the same angular index from nose to
    trailing edge makes that literally true.
    """
    segments = plan.ring_segments
    half = segments // 2
    angles = _ring_angles(segments)

    accum.region = "body"
    body_rows: List[List[int]] = []
    ring_arc: List[List[float]] = []
    row_v: List[float] = []
    previous: Optional[List[Vector]] = None
    running_v = 0.0

    for i in range(plan.body_rings + 1):
        # Slightly denser toward the head, where curvature is highest.
        t = math.pow(i / float(plan.body_rings), 0.90)
        points = _body_ring(plan, t, angles)
        # u is REAL accumulated arc around the ring, so a flattened caudal section and a
        # round head section both get honest texel spacing instead of uniform index
        # spacing claiming a distance the surface does not travel.
        arc = [0.0] * segments
        total = 0.0
        for j in range(segments):
            arc[j] = total
            total += (points[(j + 1) % segments] - points[j]).length
        if previous is not None:
            running_v += sum((points[j] - previous[j]).length
                             for j in range(segments)) / float(segments)
        row_v.append(running_v)
        ring_arc.append([value - total * 0.5 for value in arc])
        indices = [accum.vert(points[j], _flex_from_z(points[j].z), _belly_of(angles[j]))
                   for j in range(segments)]
        body_rows.append(indices)
        previous = points

    body_root = [accum.positions[idx].copy() for idx in body_rows[0]]

    accum.region = "caudal_fin"
    caudal_rows: List[List[int]] = []
    caudal_arc: List[List[float]] = []
    caudal_v: List[float] = []
    for k in range(1, plan.tail_rings):
        tau = k / float(plan.tail_rings)
        points = _caudal_ring(plan, tau, angles, body_root)
        arc = [0.0] * segments
        total = 0.0
        for j in range(segments):
            arc[j] = total
            total += (points[(j + 1) % segments] - points[j]).length
        caudal_arc.append([value - total * 0.5 for value in arc])
        caudal_v.append(-tau * abs(Z_PEDUNCLE - Z_LOBE_TIP))
        indices = [accum.vert(points[j], _flex_from_z(points[j].z), _belly_of(angles[j]))
                   for j in range(segments)]
        caudal_rows.append(indices)

    # ---- trailing edge: a HALF ring at x == 0, SHARED by both sides --------
    # Closing the fin this way keeps the shell manifold with no boundary loop and no
    # coincident duplicate pair. Collapsing a full ring to x == 0 instead would put two
    # vertices at identical positions, which a weld would merge -- silently changing the
    # vertex COUNT and therefore the VAT width, the one number the binder refuses on.
    trailing = _caudal_ring(plan, 1.0, angles, body_root)
    edge_indices: List[int] = []
    edge_points: List[Vector] = []
    for m in range(half + 1):
        point = trailing[m % segments]
        flat = Vector((0.0, point.y, point.z))
        edge_points.append(flat)
        edge_indices.append(accum.vert(flat, _flex_from_z(flat.z),
                                       _belly_of(angles[m % segments])))

    # The trailing edge gets its OWN arc parameterisation. Reusing the last caudal ring's
    # u measured LOD0 worst aspect distortion 62.42 with 17.1% of area over the organic
    # limit: the blade's ventral-to-dorsal outline is far longer than the peduncle ring is
    # round, so the ring's u compressed it by that ratio. The edge is traversed once per
    # side of the blade, so in the tube's u parameterisation it covers HALF the loop --
    # hence the +/-E split applied in _stitch_body.
    edge_arc = [0.0]
    for m in range(half):
        edge_arc.append(edge_arc[-1] + (edge_points[m + 1] - edge_points[m]).length)

    return {
        "angles": angles,
        "segments": segments,
        "half": half,
        "body_rows": body_rows,
        "caudal_rows": caudal_rows,
        "edge_indices": edge_indices,
        "edge_points": edge_points,
        "edge_arc": edge_arc,
        "ring_arc": ring_arc,
        "row_v": row_v,
        "caudal_arc": caudal_arc,
        "caudal_v": caudal_v,
        "spine_length": running_v,
    }


def _stitch_body(accum: _Accum, plan: FishPlan, shell: dict) -> None:
    """Faces for the body tube, the nose cap, the caudal bands and the trailing edge."""
    segments = shell["segments"]
    half = shell["half"]
    angles = shell["angles"]
    body_rows = shell["body_rows"]
    caudal_rows = shell["caudal_rows"]
    edge_indices = shell["edge_indices"]
    ring_arc = shell["ring_arc"]
    row_v = shell["row_v"]
    caudal_arc = shell["caudal_arc"]
    caudal_v = shell["caudal_v"]

    def uv(row_arc, v, j):
        return (ISLAND_BODY, row_arc[j % segments], v)

    # ---- body bands --------------------------------------------------------
    accum.region = "body"
    for i in range(len(body_rows) - 1):
        lower = body_rows[i]
        upper = body_rows[i + 1]
        for j in range(segments):
            k = (j + 1) % segments
            # AT THE SEAM the k corner must continue PAST the end of the ring instead of
            # wrapping back to -C/2, or the last quad spans the whole island and INVERTS.
            # ring_arc is stored centred, arc - C/2, so ring_arc[i][0] == -C/2 and the
            # continuation is simply its negation. Getting this wrong measured as LOD0 uv
            # edge-ratio worst 50.36 with 32.5% of surface area over the organic limit --
            # one wrong seam column per band, on every band.
            u_lo_k = ring_arc[i][k] if k != 0 else -ring_arc[i][0]
            u_up_k = ring_arc[i + 1][k] if k != 0 else -ring_arc[i + 1][0]
            accum.quad(
                lower[j], lower[k], upper[k], upper[j], SLOT_BODY,
                ((ISLAND_BODY, ring_arc[i][j], row_v[i]),
                 (ISLAND_BODY, u_lo_k, row_v[i]),
                 (ISLAND_BODY, u_up_k, row_v[i + 1]),
                 (ISLAND_BODY, ring_arc[i + 1][j], row_v[i + 1])))

    # ---- nose cap ----------------------------------------------------------
    # A fan to one apex. The last body ring is already small (snout_depth), so the fan
    # triangles are compact rather than the long slivers a fan from a wide ring produces.
    accum.region = "snout"
    last = body_rows[-1]
    apex_position = Vector((0.0, _section_centre_y(plan, 1.0), Z_SNOUT))
    apex = accum.vert(apex_position, _flex_from_z(Z_SNOUT), 0.5)
    apex_u = 0.0
    apex_v = row_v[-1] + 0.04
    for j in range(segments):
        k = (j + 1) % segments
        u_k = ring_arc[-1][k] if k != 0 else -ring_arc[-1][0]
        accum.face(
            (last[j], last[k], apex), SLOT_BODY,
            ((ISLAND_BODY, ring_arc[-1][j], row_v[-1]),
             (ISLAND_BODY, u_k, row_v[-1]),
             (ISLAND_BODY, apex_u, apex_v)))

    # ---- caudal bands ------------------------------------------------------
    # ---- caudal bands, on the FLAT caudal island -------------------------
    # u is axial distance aft of the peduncle and v is the vertical span, both in real
    # units, so the blade's own plane is mapped near-isometrically. The two faces of the
    # blade become two side-by-side rectangles, offset by `caudal_shift`; the shared
    # ventral and dorsal pole vertices carry a different u on each side, which is the fin
    # rim's seam.
    caudal_shift = (Z_PEDUNCLE - Z_LOBE_TIP) * 1.20

    def caudal_uv(vertex_index: int, side: float):
        point = accum.positions[vertex_index]
        u = -(point.z - Z_PEDUNCLE)
        if side < 0.0:
            u += caudal_shift
        return (ISLAND_CAUDAL, u, point.y)

    accum.region = "caudal_fin"
    chain = [body_rows[0]] + caudal_rows
    for i in range(len(chain) - 1):
        lower = chain[i]
        upper = chain[i + 1]
        for j in range(segments):
            k = (j + 1) % segments
            side = 1.0 if j < half else -1.0
            accum.quad(
                upper[j], upper[k], lower[k], lower[j], SLOT_BODY,
                (caudal_uv(upper[j], side), caudal_uv(upper[k], side),
                 caudal_uv(lower[k], side), caudal_uv(lower[j], side)))

    # ---- trailing edge -----------------------------------------------------
    # The +x half maps ring index j directly to edge index j. The -x half mirrors:
    # ring[half + p] sits at the angle whose +x twin is edge[half - p]. Per-CORNER UVs let
    # the shared edge vertices carry a different u on each side, which is the fin rim's own
    # seam and is invisible.
    accum.region = "caudal_edge"
    last_ring = chain[-1]
    # WINDING: the tail-ward side comes FIRST, matching the caudal bands above
    # (upper -> lower). Emitting this band ring-first instead cost two COUPLED defects,
    # both found with a directed-edge census rather than guessed at:
    #   * 48 repeated DIRECTED edges shared with region caudal_fin -- two faces traversing
    #     one edge in the same direction cannot both be outward, which is the
    #     inconsistent_winding signature; and
    #   * a 4-FACE non-manifold edge at the ventral pole, because the reversed corner order
    #     made the +x and -x pole quads fan on the SAME triangulation diagonal.
    # One ordering fix removes both, and a non-manifold edge is worth finding early: it
    # pins decimation outright, since Quadric Edge Collapse will not collapse across one.
    for j in range(half):
        accum.quad(
            edge_indices[j], edge_indices[j + 1], last_ring[j + 1], last_ring[j],
            SLOT_BODY,
            (caudal_uv(edge_indices[j], 1.0), caudal_uv(edge_indices[j + 1], 1.0),
             caudal_uv(last_ring[j + 1], 1.0), caudal_uv(last_ring[j], 1.0)))
    for p in range(half):
        a = (half + p) % segments
        b = (half + p + 1) % segments
        m_a = half - p
        m_b = half - p - 1
        accum.quad(
            edge_indices[m_a], edge_indices[m_b], last_ring[b],
            last_ring[a], SLOT_BODY,
            (caudal_uv(edge_indices[m_a], -1.0), caudal_uv(edge_indices[m_b], -1.0),
             caudal_uv(last_ring[b], -1.0), caudal_uv(last_ring[a], -1.0)))


def _thick_blade(accum: _Accum, island: int, outline: Sequence[Vector],
                 thickness_axis: Vector, half_thickness: float,
                 flex: Sequence[float], belly: Sequence[float]) -> None:
    """A four-corner fin membrane with real thickness, as a closed shell.

    ``3DMODEL_FAUNA.md`` section 2 requires "thin appendage, fin/membrane" as silhouette
    contrast, and a ZERO-thickness sheet is not an option: it leaves boundary edges, it
    disappears edge-on, and ``validate.py`` has no double-sided declaration on this asset.
    Twelve triangles buys a fin that reads from both sides and closes cleanly.

    ``outline`` runs base-front, base-rear, tip-rear, tip-front. The tip edge is SHORTER
    and shifted aft by the caller, which is what makes a swept fin rather than the glued-on
    rectangle section 1 rejects.
    """
    axis = thickness_axis.normalized() * half_thickness
    plus = [accum.vert(point + axis, flex[i], belly[i])
            for i, point in enumerate(outline)]
    minus = [accum.vert(point - axis, flex[i], belly[i])
             for i, point in enumerate(outline)]

    # ISLAND-LOCAL UVs: a real 2D flattening of the membrane into its OWN plane, then three
    # side-by-side regions -- +x sheet, -x sheet, and a narrow rim strip whose width is the
    # membrane's actual thickness.
    #
    # The first version parameterised by "distance from outline[0]" along one axis and a
    # chord length along the other, which gave corners 0 and 3 the same u AND made
    # uv_plus[0] identical to uv_minus[0]. Result, measured: 16 zero_area_uv_triangle
    # occurrences (a UV triangle with two coincident corners has exactly zero area, which
    # also makes calc_tangents emit a zero-length tangent) plus part of a 119.25 worst
    # aspect distortion. A UV layout has to be a real flattening, not two lengths.
    origin = outline[0]
    axis_span = (outline[3] - outline[0])
    axis_chord = (outline[1] - outline[0])
    if axis_span.length <= 1e-9 or axis_chord.length <= 1e-9:
        raise GenerationAborted("fin blade outline is degenerate in island {0}".format(
            island))
    axis_span = axis_span.normalized()
    # Orthogonalise so the flattening is not sheared by a swept outline.
    axis_chord = (axis_chord - axis_span * axis_chord.dot(axis_span))
    if axis_chord.length <= 1e-9:
        raise GenerationAborted("fin blade outline is collinear in island {0}".format(
            island))
    axis_chord = axis_chord.normalized()
    flat = [((point - origin).dot(axis_chord), (point - origin).dot(axis_span))
            for point in outline]
    lo_u = min(value[0] for value in flat)
    hi_u = max(value[0] for value in flat)
    width = max(1e-4, hi_u - lo_u)
    gap = 2.0 * half_thickness + 1e-4
    uv_plus = [(island, value[0] - lo_u, value[1]) for value in flat]
    uv_minus = [(island, (value[0] - lo_u) + width + gap, value[1]) for value in flat]
    # RIM: a thin band hugging the +x sheet's OUTLINE, not its own strip.
    #
    # An isometric rim strip of its own is geometrically honest and fails the gate anyway:
    # the membrane is 0.012 local units thick, so at the common-instanced texel density the
    # strip measured 3.009 x 321.046 px and tripped uv_island_below_min_pixels (minimum 4)
    # four times. That gate is about ISLANDS, so the fix is not to widen the rim -- which
    # would fake its proportions -- but to stop it being a separate island. Offsetting it
    # outward from the sheet outline keeps it contiguous, keeps its true thickness, and
    # leaves one large island per sheet.
    signed_area = 0.0
    for i in range(4):
        k = (i + 1) % 4
        signed_area += flat[i][0] * flat[k][1] - flat[k][0] * flat[i][1]
    orient = 1.0 if signed_area >= 0.0 else -1.0
    rim_offset = []
    for i in range(4):
        k = (i + 1) % 4
        du = flat[k][0] - flat[i][0]
        dv = flat[k][1] - flat[i][1]
        length = max(1e-9, math.hypot(du, dv))
        # Outward normal of this outline segment in the flattened frame.
        normal = (orient * dv / length, -orient * du / length)
        rim_offset.append((normal[0] * 2.0 * half_thickness,
                           normal[1] * 2.0 * half_thickness))

    accum.face((plus[0], plus[1], plus[2], plus[3]), SLOT_BODY,
               (uv_plus[0], uv_plus[1], uv_plus[2], uv_plus[3]))
    accum.face((minus[3], minus[2], minus[1], minus[0]), SLOT_BODY,
               (uv_minus[3], uv_minus[2], uv_minus[1], uv_minus[0]))
    # RIM WINDING is reversed relative to the +axis face on purpose. Emitting
    # (plus[i], plus[k], ...) makes the rim traverse the top ring in the SAME direction as
    # the top face, so both faces claim to be outward across their shared edge -- measured
    # as 32 repeated directed edges, all inside the fin regions, which is exactly the
    # inconsistent_winding gate's input.
    for i in range(4):
        k = (i + 1) % 4
        off = rim_offset[i]
        accum.quad(plus[k], plus[i], minus[i], minus[k], SLOT_BODY,
                   ((island, uv_plus[k][1], uv_plus[k][2]),
                    (island, uv_plus[i][1], uv_plus[i][2]),
                    (island, uv_plus[i][1] + off[0], uv_plus[i][2] + off[1]),
                    (island, uv_plus[k][1] + off[0], uv_plus[k][2] + off[1])))


def _section_t(z: float) -> float:
    return law.saturate((z - Z_PEDUNCLE) / (Z_SNOUT - 0.04 - Z_PEDUNCLE))


def _surface_half_width(plan: FishPlan, z: float) -> float:
    t = _section_t(z)
    return (_body_half_height(plan, t) * _operculum_step(plan, z)
            * _body_compression(plan, t))


def _surface_half_height(plan: FishPlan, z: float) -> float:
    t = _section_t(z)
    return _body_half_height(plan, t) * _operculum_step(plan, z)


def _hull_y(plan: FishPlan, z: float, upper: bool) -> float:
    """The dorsal or ventral SURFACE height at an axial position.

    This includes ``_section_centre_y``, and omitting it is what opened a visible gap under
    the dorsal fin: the section centre sinks by up to ``belly_drop`` toward mid-body, so a
    fin seated at ``half_height * bias`` alone floats that far above the actual back. In the
    flat sheet it read as a dark sliver beside the fin, which is indistinguishable from a
    hole at a glance.
    """
    t = _section_t(z)
    h = _body_half_height(plan, t) * _operculum_step(plan, z)
    centre = _section_centre_y(plan, t)
    bias = plan.dorsal_bias if upper else plan.ventral_bias
    return centre + (h * bias if upper else -h * bias)


def _hull_x(plan: FishPlan, y: float, z: float) -> float:
    """The lateral SURFACE offset at a point on the flank.

    Solves the section's own superellipse for x at the given height instead of taking a
    fraction of the half-width. A constant x along a fin's base edge cannot follow a hull
    that curves away between the edge's two ends, and that mismatch is what showed the
    pectoral fin's underside through a gap at the flank.
    """
    t = _section_t(z)
    h = _body_half_height(plan, t) * _operculum_step(plan, z)
    centre = _section_centre_y(plan, t)
    width = h * _body_compression(plan, t)
    bias = plan.dorsal_bias if (y - centre) > 0.0 else plan.ventral_bias
    exponent = plan.section_fullness
    y_norm = min(0.96, abs(y - centre) / max(1e-6, h * bias))
    return width * math.pow(max(0.0, 1.0 - math.pow(y_norm, exponent)),
                            1.0 / exponent)


def _build_fins(accum: _Accum, plan: FishPlan) -> dict:
    """Dorsal, anal and paired pectoral membranes.

    All four are seated with a BITE into the hull rather than tangent to it. A fin placed
    at exactly the surface coordinate shows a hairline gap wherever the hull curves away
    between its two base corners, and a fin placed at a fraction of the half-extent
    disappears inside the body -- the measured prop_handtool failure where seated details
    ended up 5.7 mm under the surface and every gate stayed silent.
    """
    bite = 0.020
    report = {}

    # ---- dorsal ------------------------------------------------------------
    accum.region = "dorsal_fin"
    z_front, z_rear = plan.dorsal_span
    y_front = _hull_y(plan, z_front, True) - bite
    y_rear = _hull_y(plan, z_rear, True) - bite
    # THE TIP EDGE MUST BE SHORTER THAN THE BASE. The first version put the tip edge at
    # z_front-0.05 and z_rear-0.10, which is LONGER than the base chord -- so the membrane
    # widened as it left the body and rendered as a flat paddle, the "capsule with fins
    # glued on" failure. A fin tapers and sweeps aft.
    tip_front = z_front - 0.12
    tip_rear = z_rear + 0.11
    outline = [
        Vector((0.0, y_front, z_front)),
        Vector((0.0, y_rear, z_rear)),
        Vector((0.0, y_rear + plan.dorsal_height * 0.55, tip_rear)),
        Vector((0.0, y_front + plan.dorsal_height, tip_front)),
    ]
    _thick_blade(accum, ISLAND_DORSAL, outline, Vector((1.0, 0.0, 0.0)),
                 plan.caudal_thickness * 1.1,
                 (_flex_from_z(z_front) * 0.9 + 0.10,
                  _flex_from_z(z_rear) * 0.9 + 0.10, 0.96, 0.88),
                 (0.0, 0.0, 0.0, 0.0))
    report["dorsal"] = {"spanZ": [round(z_front, 4), round(z_rear, 4)],
                        "heightLocal": round(plan.dorsal_height, 4)}

    # ---- anal --------------------------------------------------------------
    accum.region = "anal_fin"
    z_front, z_rear = plan.anal_span
    y_front = _hull_y(plan, z_front, False) + bite
    y_rear = _hull_y(plan, z_rear, False) + bite
    outline = [
        Vector((0.0, y_front, z_front)),
        Vector((0.0, y_rear, z_rear)),
        Vector((0.0, y_rear - plan.anal_height * 0.50, z_rear + 0.09)),
        Vector((0.0, y_front - plan.anal_height, z_front - 0.07)),
    ]
    _thick_blade(accum, ISLAND_ANAL, outline, Vector((1.0, 0.0, 0.0)),
                 plan.caudal_thickness,
                 (_flex_from_z(z_front) * 0.9 + 0.10,
                  _flex_from_z(z_rear) * 0.9 + 0.10, 0.94, 0.86),
                 (1.0, 1.0, 1.0, 1.0))
    report["anal"] = {"spanZ": [round(z_front, 4), round(z_rear, 4)],
                      "heightLocal": round(plan.anal_height, 4)}

    # ---- pectorals ---------------------------------------------------------
    # Behind the operculum step, angled down and aft. These are the "thin appendage" that
    # breaks the body's smooth flank, and the RIGHT one is shorter by the asymmetry term.
    for side, island in ((1.0, ISLAND_PECTORAL_R), (-1.0, ISLAND_PECTORAL_L)):
        accum.region = "pectoral_fin"
        z_base = plan.pectoral_z
        length = plan.pectoral_length * (1.0 if side > 0.0 else 0.94)
        y_base = _hull_y(plan, z_base, False) * 0.30
        # PER-CORNER hull solve: each base corner sits at its own height, where the
        # flank's x differs. One shared x_base is what opened the flank gap.
        y_front_c = y_base + 0.030
        y_rear_c = y_base - 0.030
        z_front_c = z_base + 0.035
        z_rear_c = z_base - 0.055
        x_front = _hull_x(plan, y_front_c, z_front_c) - bite * 0.5
        x_rear = _hull_x(plan, y_rear_c, z_rear_c) - bite * 0.5
        x_base = max(x_front, x_rear)
        # Same taper rule as the dorsal: the tip chord is about 40% of the base chord, and
        # the whole membrane sweeps aft and DOWN, which is how a pectoral fin sits against
        # the flank. The first version's tip chord was longer than its base and read as a
        # rectangular paddle bolted to the side.
        outline = [
            Vector((side * x_front, y_front_c, z_front_c)),
            Vector((side * x_rear, y_rear_c, z_rear_c)),
            Vector((side * (x_base + length * 0.50), y_base - 0.052,
                    z_base - 0.055 - length * 0.62)),
            Vector((side * (x_base + length * 0.68), y_base - 0.036,
                    z_base - 0.030 - length * 0.55)),
        ]
        _thick_blade(accum, island, outline, Vector((0.0, 1.0, 0.0)),
                     plan.caudal_thickness * 0.9,
                     (0.30, 0.30, 0.92, 0.88), (0.62, 0.72, 0.72, 0.66))
    report["pectoral"] = {"baseZ": round(plan.pectoral_z, 4),
                          "lengthLocal": round(plan.pectoral_length, 4),
                          "rightShorterBy": 0.06}
    return report


def _build_eyes(accum: _Accum, plan: FishPlan) -> dict:
    """A lens per side: rim ring plus an outer and an inner apex.

    ``3DMODEL_FAUNA.md`` section 5 lists "Eye/lens/wet organ when present" as a material
    zone, but this asset ships ONE slot because the indirect draw renders submesh 0 only
    (see the module docstring). So the eye is carried by GEOMETRY -- a lens that bulges
    proudly enough to break the head silhouette and catch a distinct normal -- rather than
    by a material border that would never be drawn.

    Twelve triangles each, as a closed shell. It interpenetrates the skull rather than
    being welded into it, which is correct for an eye and costs nothing: two disjoint
    closed shells are each manifold, so no non-manifold edge is created and
    ``weld_and_clean`` finds nothing to repair.
    """
    segments = 8
    report = {}
    for side, island in ((1.0, ISLAND_EYE_R), (-1.0, ISLAND_EYE_L)):
        accum.region = "eye"
        z = plan.eye_z
        radius = plan.eye_radius
        # SEAT THE RIM ON THE REAL HULL, not at a fraction of the half-width.
        #
        # Taking x = half_width * 0.80 put the whole rim circle INSIDE the head, because the
        # eye sits above the section centre where the superellipse has already pulled the
        # hull inboard. Only the outer apex emerged, so the eye rendered as a sharp dark
        # cone tip poking out of the skull -- visible in the flat sheet as a black diamond
        # and read on first inspection as a hole. Solving the superellipse for x at the
        # eye's own height puts the rim where the surface actually is.
        hull_x = _hull_x(plan, plan.eye_y, z)
        # Rim slightly embedded so no gap can open between lens and hull; apex proud enough
        # to catch its own normal and break the head silhouette.
        x_surface = hull_x - radius * 0.18
        centre = Vector((side * x_surface, plan.eye_y, z))
        rim = []
        for j in range(segments):
            angle = 2.0 * math.pi * j / float(segments)
            rim.append(accum.vert(
                centre + Vector((0.0, radius * math.sin(angle),
                                 radius * math.cos(angle))),
                0.0, 0.30))
        outer = accum.vert(centre + Vector((side * radius * 0.62, 0.0, 0.0)), 0.0, 0.30)
        inner = accum.vert(centre - Vector((side * radius * 0.85, 0.0, 0.0)), 0.0, 0.30)
        for j in range(segments):
            k = (j + 1) % segments
            a = 2.0 * math.pi * j / float(segments)
            b = 2.0 * math.pi * k / float(segments)
            uv_a = (island, radius * math.cos(a), radius * math.sin(a))
            uv_b = (island, radius * math.cos(b), radius * math.sin(b))
            uv_c = (island, 0.0, 0.0)
            if side > 0.0:
                accum.face((rim[j], rim[k], outer), SLOT_BODY, (uv_a, uv_b, uv_c))
                accum.face((rim[k], rim[j], inner), SLOT_BODY, (uv_b, uv_a, uv_c))
            else:
                accum.face((rim[k], rim[j], outer), SLOT_BODY, (uv_b, uv_a, uv_c))
                accum.face((rim[j], rim[k], inner), SLOT_BODY, (uv_a, uv_b, uv_c))
        report["radiusLocal"] = round(radius, 4)
        report["positionZ"] = round(z, 4)
        report["positionY"] = round(plan.eye_y, 4)
    return report


# ---------------------------------------------------------------------------
# UV packing  --  3dmodel.md section 6
# ---------------------------------------------------------------------------

def pack_islands(accum: _Accum, *, atlas_size: int, texel_density: int) -> dict:
    """Per-corner island coordinates in real units -> packed 0..1 UVs.

    Every island's raw coordinates are already distances measured along the real surface,
    so multiplying by ``texel_density / atlas_size`` sets texel density exactly rather than
    approximately. The border reserve comes from ``law.atlas_padding_for``, which is what
    keeps ``uv_atlas_padding_violation`` enforceable instead of skipped.
    """
    padding_px = law.atlas_padding_for(atlas_size)
    padding = padding_px / float(atlas_size)
    usable = 1.0 - 2.0 * padding
    if usable <= 0.0:
        raise GenerationAborted("atlas padding leaves no usable UV space")
    gap = 2.0 * padding
    scale = texel_density / float(atlas_size)

    bounds = {}
    for corners in accum.face_uv:
        for island, u, v in corners:
            box = bounds.get(island)
            if box is None:
                bounds[island] = [u, v, u, v]
            else:
                box[0] = min(box[0], u)
                box[1] = min(box[1], v)
                box[2] = max(box[2], u)
                box[3] = max(box[3], v)

    def layout(active_scale: float):
        # Deterministic shelf pack: tallest island first, ties broken by island id so the
        # layout is reproducible from the seed alone.
        order = sorted(bounds.keys(),
                       key=lambda i: (-(bounds[i][3] - bounds[i][1]) * active_scale, i))
        offsets = {}
        pen_u = padding
        pen_v = padding
        shelf = 0.0
        used_w = 0.0
        for island in order:
            box = bounds[island]
            width = max(1e-6, (box[2] - box[0]) * active_scale)
            height = max(1e-6, (box[3] - box[1]) * active_scale)
            if pen_u + width > 1.0 - padding and shelf > 0.0:
                pen_u = padding
                pen_v += shelf + gap
                shelf = 0.0
            offsets[island] = (pen_u - box[0] * active_scale,
                               pen_v - box[1] * active_scale)
            pen_u += width + gap
            shelf = max(shelf, height)
            used_w = max(used_w, pen_u - padding)
        return offsets, used_w, (pen_v + shelf) - padding

    offsets, used_w, used_h = layout(scale)
    applied = 1.0
    if used_w > usable or used_h > usable:
        shrink = min(usable / max(1e-9, used_w), usable / max(1e-9, used_h)) * 0.995
        applied = shrink
        offsets, used_w, used_h = layout(scale * shrink)

    uvs = []
    active = scale * applied
    for corners in accum.face_uv:
        packed = []
        for island, u, v in corners:
            off_u, off_v = offsets[island]
            packed.append((u * active + off_u, v * active + off_v))
        uvs.append(packed)

    return {
        "uvs": uvs,
        "islandCount": len(bounds),
        "atlasSize": atlas_size,
        "paddingPx": padding_px,
        "requestedTexelDensityPxPerM": texel_density,
        "achievedTexelDensityPxPerM": round(texel_density * applied, 3),
        "densityScaleApplied": round(applied, 5),
        "usedWidthUv": round(used_w, 5),
        "usedHeightUv": round(used_h, 5),
        "route": (
            "authored analytic UVs: cylindrical body+caudal sheet with the seam on the "
            "VENTRAL midline (least visible), u = real accumulated arc around each ring "
            "and v = mean surface advance along the spine; separate flat islands per fin "
            "membrane and per eye lens. No mirroring anywhere -- 3DMODEL_FAUNA.md section "
            "4 forbids mirrored UVs on eyes and jaws, so the head is uniquely mapped."),
    }


# ---------------------------------------------------------------------------
# Datablock, materials, consumer contract
# ---------------------------------------------------------------------------

def _to_object(accum: _Accum, packed_uvs, name: str,
               blackbox: BlackBox) -> bpy.types.Object:
    """Build the Mesh from the accumulator. Vertex order is preserved exactly."""
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([tuple(p) for p in accum.positions], [], list(accum.faces))
    mesh.update()

    # from_pydata drops invalid faces SILENTLY. Comparing the counts is the only way that
    # becomes an error instead of a quietly smaller fish.
    if len(mesh.polygons) != len(accum.faces):
        raise GenerationAborted(
            "from_pydata kept {0} of {1} faces; the topology description is invalid".format(
                len(mesh.polygons), len(accum.faces)))
    if len(mesh.vertices) != len(accum.positions):
        raise GenerationAborted(
            "from_pydata kept {0} of {1} vertices; VAT column order would be wrong".format(
                len(mesh.vertices), len(accum.positions)))

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


_MATERIAL_SPEC = {
    # Sargassum micro-fauna reads as a pale olive-amber flank over a bright belly. Values
    # are LINEAR. The swarm shader is Unlit and drives its own _BaseColor/_BellyColor, so
    # this material exists for the forge's own studio/material proof renders and for any
    # non-swarm use of the mesh -- it is not the swarm's runtime appearance.
    "FishTissue": {
        "base_color": (0.352, 0.318, 0.196, 1.0),
        "roughness": 0.34,
        "subsurface": 0.22,
        "subsurface_radius": (0.020, 0.014, 0.009),
        "ior": 1.36,
    },
}


def build_materials() -> List[bpy.types.Material]:
    """One material per declared slot, named through ``law.NAME_MATERIAL``."""
    out = []
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
        spec = _MATERIAL_SPEC[role]
        bsdf.inputs["Base Color"].default_value = spec["base_color"]
        bsdf.inputs["Roughness"].default_value = spec["roughness"]
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = 0.0
        # Blender 4.x renamed these. Probe by MEMBERSHIP, never a bare try/except
        # AttributeError, which converts an API rename into silently flat tissue.
        for candidate in ("Subsurface Weight", "Subsurface"):
            if candidate in bsdf.inputs:
                bsdf.inputs[candidate].default_value = spec["subsurface"]
                break
        if "Subsurface Radius" in bsdf.inputs:
            bsdf.inputs["Subsurface Radius"].default_value = spec["subsurface_radius"]
        if "IOR" in bsdf.inputs:
            bsdf.inputs["IOR"].default_value = spec["ior"]
        links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
        out.append(material)
    return out


def _assert_consumer_contract(obj: bpy.types.Object, plan: FishPlan,
                              notes: List[str]) -> dict:
    """Turn the three SILENT VAT/shader failures into loud aborts.

    None of these is checked anywhere in the existing pipeline. ``AnalyzeAxis`` picking the
    wrong body axis, a fish authored nose-aft, and a local extent outside the shader's
    ``saturate()`` calibration all produce a plausible asset and a garbage swarm. This is
    the "if a stage can collapse quietly, add a probe that fails loudly" rule applied to a
    contract that lives in another language in another directory.
    """
    lo, hi = mesh_ops.local_bounds(obj)
    size = (hi.x - lo.x, hi.y - lo.y, hi.z - lo.z)

    # 1. Z strictly longest, because AnalyzeAxis uses >= and awards ties to X then Y.
    if not (size[2] > size[0] and size[2] > size[1]):
        raise GenerationAborted(
            "body axis contract: local size ({0:.4f}, {1:.4f}, {2:.4f}) does not have Z "
            "strictly longest. AbyssalAnatomyStudio1610.AnalyzeAxis would pick "
            "{3} and the VAT would shear the fish across the wrong axis with no error"
            .format(size[0], size[1], size[2],
                    "X" if size[0] >= size[1] and size[0] >= size[2] else "Y"))
    margin_x = size[2] / max(1e-9, size[0])
    margin_y = size[2] / max(1e-9, size[1])

    # 2. Nose forward: the single most +Z vertex must be the snout apex on the midline.
    nose = max(obj.data.vertices, key=lambda v: v.co.z)
    if abs(nose.co.x) > 0.02 or nose.co.z < Z_SNOUT - 1e-4:
        raise GenerationAborted(
            "nose contract: the most +Z vertex is at ({0:.4f}, {1:.4f}, {2:.4f}); "
            "BoidFishInstanced.shader requires the snout on +Z at the midline".format(
                nose.co.x, nose.co.y, nose.co.z))

    # 3. Extent inside the shader's saturate() calibration envelope.
    if hi.z > 1.02 or lo.z < -1.02:
        raise GenerationAborted(
            "extent contract: z spans [{0:.4f}, {1:.4f}]; the tail mask "
            "saturate(-localPos.z) is calibrated to [-1, +1]".format(lo.z, hi.z))
    if max(abs(lo.x), abs(hi.x)) > 0.55:
        raise GenerationAborted(
            "extent contract: |x| reaches {0:.4f}; the fin mask saturates near 0.556 and "
            "a schooling fish must be laterally compressed".format(
                max(abs(lo.x), abs(hi.x))))

    compression = size[0] / max(1e-9, size[1])
    if compression >= 0.90:
        raise GenerationAborted(
            "lateral compression contract: width/depth is {0:.3f}; schooling fish are "
            "laterally compressed, not round in cross-section".format(compression))

    notes.append(
        "consumer contract PASS: local size ({0:.4f}, {1:.4f}, {2:.4f}), Z longest by "
        "{3:.2f}x over X and {4:.2f}x over Y, width/depth {5:.3f}, snout apex at z={6:.4f} "
        "on the midline, |x|max {7:.4f} inside the 0.556 fin-mask knee".format(
            size[0], size[1], size[2], margin_x, margin_y, compression, hi.z,
            max(abs(lo.x), abs(hi.x))))
    return {
        "localSize": [round(v, 5) for v in size],
        "zLongestMarginOverX": round(margin_x, 4),
        "zLongestMarginOverY": round(margin_y, 4),
        "widthOverDepth": round(compression, 4),
        "noseZ": round(hi.z, 5),
        "tailZ": round(lo.z, 5),
        "maxAbsX": round(max(abs(lo.x), abs(hi.x)), 5),
        "fishScaleAtMaterial": 0.28,
        "renderedLengthMetresAtThatScale": round(size[2] * 0.28, 4),
    }


# Projection angle for the LOD re-solve, DERIVED from the gate rather than picked.
# smart_project is a planar-projection unwrapper: a face whose normal sits theta from its
# group axis is compressed by cos(theta) in one direction only, so angle_limit IS the
# worst-case aspect distortion the solver may introduce -- 1/cos(theta) - 1. Inverting that
# against the organic limit gives the widest angle whose own compression still fits.
UV_PROJECTION_ANGLE_DEG = math.degrees(
    math.acos(1.0 / (1.0 + law.UV_STRETCH_MAX_BY_SURFACE[SURFACE])))


def _settle_topology(obj: bpy.types.Object) -> Tuple[dict, str]:
    """Purge what Decimate leaves behind, without adding geometry.

    Decimate/COLLAPSE emits slivers whose UV triangle can come back with zero area (which
    makes calc_tangents emit a zero-length tangent and trips
    ``tangent_length_out_of_range``), fused n-gons (Blender cannot build a tangent basis on
    an n-gon at all), and DUPLICATE FACES on the same vertex triple -- FBX merges that pair
    on import, so the round trip rejects the package for losing exactly one triangle.
    """
    bm = mesh_ops.bmesh_from_object(obj)
    stats = mesh_ops.weld_and_clean(bm, merge_distance=1e-4, fill_boundary_loops=False)
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
    doomed = []
    for face in bm.faces:
        key = tuple(sorted(vert.index for vert in face.verts))
        if key in seen:
            doomed.append(face)
        else:
            seen.add(key)
    if doomed:
        bmesh.ops.delete(bm, geom=doomed, context="FACES")
    orphans = [vert for vert in bm.verts if not vert.link_faces]
    if orphans:
        bmesh.ops.delete(bm, geom=orphans, context="VERTS")
    non_triangles = sum(1 for face in bm.faces if len(face.verts) != 3)
    mesh_ops.bmesh_to_object(bm, obj)
    if non_triangles:
        raise GenerationAborted(
            "_settle_topology left {0} non-triangular faces on {1}; the FBX round trip "
            "compares per-corner data against a triangulated re-import".format(
                non_triangles, obj.name))
    return stats, "triangulated {0} fused, purged {1} slivers and {2} duplicate faces".format(
        len(fused), len(slivers), len(doomed))


def _make_reunwrap(atlas_size: int, notes: List[str]):
    """Re-solve UVs and the shading basis for each coarse level.

    Decimate/COLLAPSE has no UV term in its collapse cost, so the analytic body sheet is
    destroyed by reduction while the triangle budget still reads as met. The analytic
    parametrisation cannot be reapplied once the topology has changed, so LOD1/LOD2 get an
    angle-based solve -- and because this hook exists, ``build_lod_chain`` is called with
    ``preserve_seams=False``. That pairing was measured on cap-stem: seam splitting protects
    a parameterisation this generator discards, and at LOD2 it left the shell in 37
    disconnected components with shards, while dropping it gave 4 closed components. A
    generator that passes ``reunwrap=None`` must keep ``preserve_seams=True``.
    """
    padding = law.atlas_padding_for(atlas_size) / float(atlas_size)

    def reunwrap(obj: bpy.types.Object, lod_index: int) -> None:
        entry = mesh_ops.topology_report(obj)
        _clean, settle = _settle_topology(obj)
        budget = law.LOD_BUDGETS[FAMILY].limit(lod_index)
        refitted = mesh_ops.triangle_count(obj.data)
        if refitted > budget:
            mesh_ops.reduce_to_budget(obj, family=FAMILY, lod_index=lod_index)
            _repair, settle = _settle_topology(obj)
            refitted = mesh_ops.triangle_count(obj.data)

        # RE-DERIVE THE SHADING BASIS. LOD1/LOD2 start as copies of LOD0 and carry LOD0's
        # custom split normals interpolated across a collapse, while weld_and_clean ends
        # with recalc_face_normals -- putting face normals out of agreement with that
        # inherited basis. Measured elsewhere in this pipeline as a round-trip rejection at
        # "corner normals changed by 0.001828", 34x the tolerance.
        relit = mesh_ops.apply_shading_basis(
            obj, smooth_angle_deg=law.smooth_angle_for(SURFACE), weighted=True,
            keep_sharp=True)
        if relit.smooth_polygons <= 0 or not relit.weighted_applied:
            notes.append("LOD{0} shading basis NOT re-derived (smooth_polygons={1} "
                         "weighted={2})".format(lod_index, relit.smooth_polygons,
                                                relit.weighted_applied))

        mesh_ops._make_sole_active(obj)
        bpy.ops.object.mode_set(mode="EDIT")
        try:
            bpy.ops.mesh.select_all(action="SELECT")
            result = bpy.ops.uv.smart_project(
                angle_limit=math.radians(UV_PROJECTION_ANGLE_DEG),
                island_margin=padding, area_weight=0.0, correct_aspect=True,
                scale_to_bounds=False)
        finally:
            bpy.ops.object.mode_set(mode="OBJECT")
        after = mesh_ops.topology_report(obj)
        notes.append(
            "LOD{0} reunwrap: entry comp={1} tris={2} boundary={3} -> exit comp={4} "
            "tris={5} boundary={6}; {7}; refitted {8} against the {9} budget".format(
                lod_index, entry.components, entry.triangles, entry.boundary_edges,
                after.components, after.triangles, after.boundary_edges, settle,
                refitted, budget))
        if "FINISHED" not in result:
            notes.append("LOD{0} smart_project returned {1}; UVs are whatever the collapse "
                         "left".format(lod_index, sorted(result)))
            return

        layer = obj.data.uv_layers.active
        if layer is None:
            notes.append("LOD{0} has no active UV layer after re-unwrap".format(lod_index))
            return
        # smart_project packs to the full 0..1 square and TOUCHES the border, which is
        # exactly uv_atlas_padding_violation. One uniform factor for both axes so the
        # rescale cannot introduce aspect distortion of its own while fixing the overlap.
        count = len(layer.data)
        buffer = [0.0] * (count * 2)
        layer.data.foreach_get("uv", buffer)
        span = 1.0 - 2.0 * padding
        lo_u, hi_u = min(buffer[0::2]), max(buffer[0::2])
        lo_v, hi_v = min(buffer[1::2]), max(buffer[1::2])
        factor = min(span / max(1e-6, hi_u - lo_u), span / max(1e-6, hi_v - lo_v))
        for i in range(count):
            buffer[i * 2] = padding + (buffer[i * 2] - lo_u) * factor
            buffer[i * 2 + 1] = padding + (buffer[i * 2 + 1] - lo_v) * factor
        layer.data.foreach_set("uv", buffer)
        obj.data.update()

    return reunwrap


def _read_vcol_direct(obj: bpy.types.Object) -> dict:
    """Read the packed attribute back off the mesh, per channel.

    A rendered channel sheet proves what the SHADER sees; this proves what the DATA holds.
    Both are needed -- the measured lesson in this pipeline is that a channel can be
    correct in the attribute and wrong in the render, and vice versa.
    """
    mesh = obj.data
    layer = mesh.color_attributes.get(law.VCOL_ATTRIBUTE_NAME)
    if layer is None:
        return {"status": "attribute '{0}' absent".format(law.VCOL_ATTRIBUTE_NAME)}
    count = len(layer.data)
    buffer = [0.0] * (count * 4)
    layer.data.foreach_get("color", buffer)
    out = {}
    for index, label in enumerate(law.ORGANIC_VCOL):
        values = buffer[index::4]
        if not values:
            continue
        out[label] = {"min": round(min(values), 5), "max": round(max(values), 5),
                      "mean": round(sum(values) / float(len(values)), 5)}
    return out


def _purge_scene() -> None:
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh, do_unlink=True)


# ---------------------------------------------------------------------------
# Variant
# ---------------------------------------------------------------------------

@dataclass
class VariantResult:
    name: str
    lods: list
    reports: list
    chain_failures: list
    collider_failures: list
    collider: object
    vcol_report: dict
    vcol_direct: dict
    ao: object
    sway: object
    uv_summary: dict
    topology: dict
    shading: object
    plan: dict
    contract: dict
    fins: dict
    eyes: dict
    hitboxes: dict
    notes: List[str]


def generate_variant(*, seed: int, quality: float, variant_index: int,
                     ao_samples: int, atlas_size: int,
                     blackbox: BlackBox) -> VariantResult:
    notes: List[str] = []
    rng = np.random.default_rng(seed + variant_index * 7919)
    name = "Fish_{0}_{1:02d}".format(seed, variant_index)

    # --- 1/2. body plan ----------------------------------------------------
    plan = plan_fish(rng, quality=quality)
    blackbox.record("shape_grammar", seed=seed, family=FAMILY.value,
                    warning="ring_segments={0} body_rings={1} tail_rings={2}".format(
                        plan.ring_segments, plan.body_rings, plan.tail_rings))

    # --- 3/4. geometry -----------------------------------------------------
    accum = _Accum()
    shell = _build_body(accum, plan)
    _stitch_body(accum, plan, shell)
    fins = _build_fins(accum, plan)
    eyes = _build_eyes(accum, plan)
    added = accum.triangulate()
    notes.append(
        "authored {0} polygons fanned into {1} triangles BEFORE UV packing, so the "
        "validated topology is the exported topology: export_unity writes "
        "use_triangles=True (mandatory -- use_tspace drops tangents silently on n-gons) "
        "and verify_fbx_roundtrip compares PER-CORNER data, which a quad source can never "
        "match".format(len(accum.faces) - added, len(accum.faces)))
    blackbox.record("geometry", vertex_count=len(accum.positions),
                    triangle_count=len(accum.faces),
                    warning="ring_segments={0}".format(plan.ring_segments))

    # --- 5. UVs ------------------------------------------------------------
    uv_summary = pack_islands(accum, atlas_size=atlas_size,
                              texel_density=TEXEL_DENSITY)
    if uv_summary["densityScaleApplied"] < 1.0:
        notes.append("texel density reduced from {0} to {1} px/m to fit the {2} px "
                     "border reserve".format(uv_summary["requestedTexelDensityPxPerM"],
                                             uv_summary["achievedTexelDensityPxPerM"],
                                             uv_summary["paddingPx"]))

    obj = _to_object(accum, uv_summary.pop("uvs"),
                     law.NAME_MESH.format(family=FAMILY.value, name=name, lod=0),
                     blackbox)

    # --- 5. materials, and the documented stage-order deviation -------------
    # bpy.ops.object.bake REFUSES an object with no material slot, and the AO bake below
    # needs it. No other stage moved.
    materials = build_materials()
    for material in materials:
        obj.data.materials.append(material)
    notes.append(
        "stage order deviation: the shared MAT_* material is built before the AO bake "
        "because bpy.ops.object.bake refuses an object with no material slot. No other "
        "stage moved.")

    # --- topology probe ----------------------------------------------------
    # The authored per-vertex arrays are index-aligned to build order, and for this family
    # build order is ALSO the VAT column order. So a weld doing any work at all is a
    # construction defect, not a repair -- it would silently change the vertex count the
    # binder refuses on.
    bm = mesh_ops.bmesh_from_object(obj)
    clean = mesh_ops.weld_and_clean(bm, merge_distance=1e-5, fill_boundary_loops=False,
                                    blackbox=blackbox)
    mesh_ops.bmesh_to_object(bm, obj)
    if clean.get("merged_vertices") or clean.get("interior_faces_deleted"):
        raise GenerationAborted(
            "weld_and_clean altered authored geometry ({0}); the per-vertex arrays and the "
            "VAT column order are index-aligned to build order".format(clean))
    topology = mesh_ops.topology_report(obj)
    blackbox.record("topology_report", vertex_count=len(obj.data.vertices),
                    triangle_count=topology.triangles,
                    warning="components={0} boundary={1} nonmanifold={2}".format(
                        topology.components, topology.boundary_edges,
                        topology.nonmanifold_edges))
    notes.append(
        "shell: {0} triangles in {1} components (body+caudal as ONE closed manifold, plus "
        "dorsal, anal, two pectoral membranes and two eye lenses as intentional separate "
        "shells), {2} boundary edges, {3} non-manifold edges. The component count is a "
        "BODY PLAN, not fragmentation -- compare it against the per-LOD shell trace below "
        "before reading a coarse level as damaged.".format(
            topology.triangles, topology.components, topology.boundary_edges,
            topology.nonmanifold_edges))

    # --- consumer contract, before anything can hide it ---------------------
    contract = _assert_consumer_contract(obj, plan, notes)

    # --- shading basis -----------------------------------------------------
    shading = mesh_ops.apply_shading_basis(
        obj, smooth_angle_deg=law.smooth_angle_for(SURFACE), weighted=True,
        keep_sharp=True, blackbox=blackbox)
    if shading.smooth_polygons <= 0:
        raise GenerationAborted(
            "apply_shading_basis smoothed no polygons; the fish would ship flat-shaded")

    # --- 6. AO bake --------------------------------------------------------
    # Ray length DERIVED from the asset. An unbounded distance turns cavity contrast into a
    # global sky term -- measured elsewhere in this pipeline as an AO mean crushed to 0.078.
    ao_distance = max(0.02, mesh_ops.longest_extent(obj) * 0.18)
    ao = vertexcolor.bake_ambient_occlusion(obj, samples=ao_samples,
                                            distance=ao_distance, blackbox=blackbox)
    ao_values = vertexcolor.consume_baked_ao(obj)
    notes.append("AO bake distance {0:.4f} local units, {1} samples".format(
        ao_distance, ao_samples))

    # --- 6. deformation field ----------------------------------------------
    # distances= is the authored per-vertex amplitude, so the field is shaped by ANATOMY
    # (fins high, snout rigid) rather than by Euclidean distance from a point, which would
    # call a pectoral fin at the head rigid.
    sway = vertexcolor.build_sway_field(
        obj.data, anchor_position=Vector((0.0, 0.0, Z_SNOUT)),
        max_flexible_length=1.0,
        stiffness_exponent=law.STIFFNESS_EXPONENT_FLEXIBLE_BLADE,
        rigid_cap=None, distances=accum.flex)

    biolum = [0.0] * len(obj.data.vertices)
    vcol_report = vertexcolor.write_organic_channels(
        obj, sway=sway, biolum=biolum, ao=ao_values if ao_values else None,
        alpha=accum.belly, alpha_meaning=ALPHA_MEANING, blackbox=blackbox)
    vertexcolor.remove_scratch_attributes(obj.data)
    vcol_direct = _read_vcol_direct(obj)

    # --- 7/8. LOD chain ----------------------------------------------------
    slot_anchors = mesh_ops.material_slot_anchors(obj)
    lod_notes: List[str] = []
    lods = mesh_ops.build_lod_chain(
        obj, family=FAMILY, name=name, quality_weight=quality, levels=3,
        preserve_seams=False, reunwrap=_make_reunwrap(atlas_size, lod_notes),
        blackbox=blackbox)
    notes.extend(lod_notes)
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

    # --- 9. collision ------------------------------------------------------
    # Hull the COARSEST level, not LOD0: make_convex_collider decimates then re-hulls, and
    # hulling LOD0 measured 184 triangles elsewhere in this pipeline -- inside
    # law.COLLIDER_CONVEX_TRI_MAX but wasteful, and it failed the convexity gate by 0.1 mm.
    collider = mesh_ops.make_convex_collider(lods[-1].obj, family=FAMILY, name=name,
                                             blackbox=blackbox)
    # 3DMODEL_FAUNA.md section 8: "Fauna collision uses primitives" and "LOD0 render mesh is
    # never used as MeshCollider". The convex hull satisfies the manifest/collider gates;
    # the PRIMITIVE set below is the gameplay truth the Unity assembler instantiates, and
    # section 8 requires hitboxes to align with visible attack and weak zones.
    body_half = _surface_half_height(plan, Z_MAX_DEPTH)
    hitboxes = {
        "route": "primitives, per 3DMODEL_FAUNA.md section 8; the convex hull in this "
                 "package is a validation/backstop proxy and is not the gameplay hitbox",
        "bodyCapsule": {
            "axis": "Z",
            "centerLocal": [0.0, round(_section_centre_y(plan, 0.55), 5), 0.10],
            "radius": round(body_half * 0.80, 5),
            "height": round(Z_SNOUT - Z_PEDUNCLE, 5),
            "zone": "spine/body mass",
        },
        "headSphere": {
            "centerLocal": [0.0, round(plan.eye_y * 0.35, 5), round(plan.eye_z - 0.04, 5)],
            "radius": round(_surface_half_height(plan, plan.eye_z) * 1.05, 5),
            "zone": "head, and the readable weak spot -- aligned to the eye lenses",
        },
        "caudalCapsule": {
            "axis": "Z",
            "centerLocal": [0.0, 0.0, round((Z_PEDUNCLE + Z_LOBE_TIP) * 0.5, 5)],
            "radius": round(plan.lobe_span * 0.55, 5),
            "height": round(abs(Z_LOBE_TIP - Z_PEDUNCLE), 5),
            "zone": "caudal peduncle and fin, the highest-amplitude region",
        },
        "layer": "Fauna_Hitbox",
        "lodAlignmentNote": "section 7 requires hitbox alignment across LODs; these "
                            "primitives are authored in LOD0 local space and every LOD "
                            "shares that space, so no level can visually shrink away from "
                            "its hit truth.",
    }

    # --- 10. validation ----------------------------------------------------
    reports = []
    for level in lods:
        reports.append(validate.validate_mesh(
            level.obj.data, family=FAMILY, lod_index=level.index,
            surface_class=SURFACE, blackbox=blackbox, hero=(level.index == 0),
            triplanar=False, double_sided=False, planar=False,
            atlas_size=atlas_size if level.index == 0 else None))
        stats = mesh_ops.uv_stretch_stats(level.obj)
        notes.append("LOD{0} uv edge-ratio: worst={1:.4f} p95={2:.4f} mean={3:.4f} over "
                     "{4} triangles".format(level.index, stats["worst"], stats["p95"],
                                            stats["mean"], stats["triangles"]))
    chain_failures = validate.validate_lod_chain(reports, family=FAMILY,
                                                 blackbox=blackbox)
    collider_failures = validate.validate_collider(
        collider.obj.data, family=FAMILY, blackbox=blackbox,
        lod0_mesh=lods[0].obj.data) if collider.obj is not None else []

    return VariantResult(
        name=name, lods=lods, reports=reports, chain_failures=chain_failures,
        collider_failures=list(collider_failures), collider=collider,
        vcol_report=vcol_report, vcol_direct=vcol_direct, ao=ao, sway=sway,
        uv_summary=uv_summary,
        topology={"triangles": topology.triangles, "components": topology.components,
                  "boundaryEdges": topology.boundary_edges,
                  "nonManifoldEdges": topology.nonmanifold_edges,
                  "irreducibleFloor": topology.irreducible_floor},
        shading=shading,
        plan={"bodyDepthLocal": round(plan.body_depth, 5),
              "compressionHead": round(plan.compression_head, 4),
              "compressionTail": round(plan.compression_tail, 4),
              "peduncleDepthLocal": round(plan.peduncle_depth, 5),
              "lobeSpanLocal": round(plan.lobe_span, 5),
              "forkDepthLocal": round(plan.fork_depth, 5),
              "ringSegments": plan.ring_segments, "bodyRings": plan.body_rings,
              "tailRings": plan.tail_rings,
              "sectionFullness": round(plan.section_fullness, 4),
              "asymmetryLocal": round(plan.asymmetry, 5),
              "landmarksZ": {"snout": Z_SNOUT, "jaw": Z_JAW, "operculum": Z_OPERCULUM,
                             "maxDepth": Z_MAX_DEPTH, "peduncle": Z_PEDUNCLE,
                             "forkNotch": round(Z_LOBE_TIP + plan.fork_depth, 4),
                             "lobeTip": Z_LOBE_TIP}},
        contract=contract, fins=fins, eyes=eyes, hitboxes=hitboxes, notes=notes)


# ---------------------------------------------------------------------------
# Proof
# ---------------------------------------------------------------------------

def render_proof(variant: VariantResult, *, out_dir: str, resolution: int) -> dict:
    """Flat, studio and material sheets at LOD0, FLAT sheets at every coarse level.

    The coarse-LOD sheets are here FROM THE START rather than added after a failure,
    because this pipeline has already paid for their absence once: with only
    ``lods[0]`` ever rendered, a LOD2 shipped in 37 disconnected components with shards off
    the silhouette while passing the triangle budget, uv_stretch_excessive, winding,
    tangent and FBX round-trip gates. The LOD number is in ``PreviewSpec.name`` so it lands
    in every tile and sheet FILENAME -- a coarse sheet mistaken for LOD0 is worse than no
    sheet.
    """
    subject = variant.lods[0].obj
    sheets = {}
    for mode in ("flat", "studio", "material"):
        spec = preview.PreviewSpec(
            name=variant.name, output_dir=out_dir, resolution=resolution,
            views=("front", "three_quarter", "side", "low"), mode=mode,
            surface_class=SURFACE)
        sheets[mode] = preview.render_contact_sheet(subject, spec).sheet_path

    lod_sheets = {}
    lod_topology = {}
    for level in variant.lods[1:]:
        key = "LOD{0}".format(level.index)
        spec = preview.PreviewSpec(
            name="{0}_{1}".format(variant.name, key), output_dir=out_dir,
            resolution=resolution, views=("three_quarter", "side"), mode="flat",
            surface_class=SURFACE)
        lod_sheets[key] = preview.render_contact_sheet(level.obj, spec).sheet_path
        report = mesh_ops.topology_report(level.obj)
        lod_topology[key] = {
            "triangles": report.triangles, "components": report.components,
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
            "channel": law.ORGANIC_VCOL[index], "tile": os.path.basename(tile),
            "min": round(stats.min_value, 5), "max": round(stats.max_value, 5),
            "mean": round(stats.mean_value, 5),
            "coverage": round(stats.coverage_fraction, 5),
            "hasGradient": stats.has_gradient, "subjectVisible": stats.subject_visible,
        })
    return {"sheets": sheets, "lodSheets": lod_sheets, "lodTopology": lod_topology,
            "channelSheet": channels.sheet_path,
            "channelTiles": list(channels.tile_paths), "measurements": measurements}


def _print_report(variant: VariantResult, proof: Optional[dict], export_result,
                  manifest_path: str) -> None:
    print("")
    print("=" * 78)
    print("ASSET {0}   family={1}   surface={2}".format(
        variant.name, FAMILY.value, SURFACE.value))
    print("=" * 78)
    budgets = law.LOD_BUDGETS[FAMILY]
    for level, report in zip(variant.lods, variant.reports):
        print("  LOD{0}  {1:>6} tris / {2:>6} law max   verts={3:<6} submeshes={4}  "
              "{5}".format(level.index, level.triangles, budgets.limit(level.index),
                           report.vertex_count, report.submesh_count,
                           "PASS" if report.passed else "FAIL"))
        for failure in report.failures:
            print("         ! " + str(failure))
    print("  lod chain: {0}".format(
        "PASS" if not variant.chain_failures else
        "; ".join(str(f) for f in variant.chain_failures)))
    print("  collider : {0} tris kind={1} {2}".format(
        variant.collider.triangles, variant.collider.kind,
        "PASS" if not variant.collider_failures else
        "; ".join(str(f) for f in variant.collider_failures)))
    print("  topology : {0}".format(variant.topology))
    print("  contract : {0}".format(variant.contract))
    print("  shading  : smooth_polygons={0} sharp_edges={1} weighted={2}".format(
        variant.shading.smooth_polygons, variant.shading.sharp_edges,
        variant.shading.weighted_applied))
    print("  AO bake  : baked={0} min={1:.4f} max={2:.4f} mean={3:.4f} "
          "has_contrast={4}".format(variant.ao.baked, variant.ao.min_value,
                                    variant.ao.max_value, variant.ao.mean_value,
                                    variant.ao.has_contrast))
    print("  deform R : min={0:.4f} max={1:.4f} exponent={2} spread={3:.3f} "
          "uniform={4}".format(variant.sway.min_value, variant.sway.max_value,
                               variant.sway.stiffness_exponent,
                               variant.sway.relative_spread, variant.sway.is_uniform))
    for label in law.ORGANIC_VCOL:
        entry = variant.vcol_direct.get(label)
        if entry:
            print("         {0:<16} min={1:<8} max={2:<8} mean={3}".format(
                label, entry["min"], entry["max"], entry["mean"]))
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
    if export_result is not None:
        print("  fbx      : {0}".format(export_result.fbx_path))
        print("  roundtrip: verified={0} unit_scale={1}".format(
            export_result.roundtrip_verified, export_result.unit_scale))
    if manifest_path:
        print("  manifest : {0}".format(manifest_path))
    for note in variant.notes:
        print("  note     : {0}".format(note))


def parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        prog="fauna_fish.py",
        description="Generate HECTON-8 schooling-fish (Sargassum micro-fauna) packages.")
    parser.add_argument("--seed", type=int, default=2207,
                        help="deterministic seed; variation is a named seed, never hidden "
                             "chance")
    parser.add_argument("--quality", type=float, default=1.0,
                        help="GlobalQualityWeight, continuous 0..1; scales ring and "
                             "segment DENSITY only, never body identity")
    parser.add_argument("--variants", type=int, default=1)
    parser.add_argument("--out", default="",
                        help="preview directory; defaults to Docs/AgentLogs/ForgePreviews")
    parser.add_argument("--ao-samples", type=int, default=64)
    parser.add_argument("--atlas", type=int, default=ATLAS_SIZE)
    parser.add_argument("--preview-resolution", type=int, default=640)
    parser.add_argument("--preview", dest="preview", action="store_true", default=True)
    parser.add_argument("--no-preview", dest="preview", action="store_false")
    parser.add_argument("--no-export", dest="export", action="store_false", default=True,
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
        run_tag = "fish_s{0}_q{1:.2f}_v{2}".format(args.seed, quality, variant_index)
        blackbox = BlackBox("fauna_fish", run_tag)
        _purge_scene()
        try:
            variant = generate_variant(
                seed=args.seed, quality=quality, variant_index=variant_index,
                ao_samples=args.ao_samples, atlas_size=args.atlas, blackbox=blackbox)
        except GenerationAborted as error:
            print("[fauna_fish] ABORTED: {0}".format(error))
            return 2

        proof = None
        if args.preview:
            proof = render_proof(variant, out_dir=out_dir,
                                 resolution=args.preview_resolution)

        # 3dmodel.md section 10: validation failure ABORTS the save. Previews render first
        # on purpose -- a rejected asset still has to be LOOKED at, and the sheets are the
        # evidence for why it was rejected.
        try:
            validate.assert_or_abort(
                [variant.reports, variant.chain_failures, variant.collider_failures],
                blackbox=blackbox, reason="fauna_fish " + variant.name)
        except GenerationAborted as error:
            print("[fauna_fish] VALIDATION REJECTED THE SAVE: {0}".format(error))
            _print_report(variant, proof, None, "")
            exit_code = 3
            continue

        export_result = None
        manifest_path = ""
        if args.export:
            package_dir = os.path.join(law.project_root(),
                                       *law.forge_package_dir(FAMILY).split("/"))
            os.makedirs(package_dir, exist_ok=True)
            # The MESH_ prefix is ORDINAL and CASE-SENSITIVE for
            # HectonFBXPostprocessor.TryResolveForgeManifestPath, and without that lookup
            # Unity re-derives normals from a single angle and discards the authored
            # weighted split basis. A wrong directory is visible; a wrong prefix would look
            # like a working export forever.
            fbx_path = os.path.join(package_dir, "MESH_{0}_{1}.fbx".format(
                FAMILY.value, variant.name))
            collider_arg = (variant.collider
                            if getattr(variant.collider, "obj", None) is not None
                            else None)

            identity = law.GeneratorIdentity(
                generator=GENERATOR_NAME, generator_version=GENERATOR_VERSION,
                seed=args.seed + variant_index * 7919, quality_weight=quality,
                family=FAMILY,
                scale_meters=round(mesh_ops.longest_extent(variant.lods[0].obj), 5),
                camera_distance_class=CAMERA_DISTANCE_CLASS,
                platform_lane=PLATFORM_LANE, source_references=REFERENCE_IDS)

            # identity= is passed so provenance is embedded in the FBX, and because FAMILY
            # is FAUNA the manifest's unityImport.modelImporter block comes back with
            # optimizeMeshVertices=False, weldVertices=False and
            # meshOptimizationFlags=PolygonOrder -- the three settings that keep the VAT
            # column order intact. That block is produced by export_unity.write_manifest and
            # by nothing else; a second manifest producer is how geology shipped for months
            # with its normals destroyed on import.
            export_result = export_unity.export_lod_group(
                variant.lods, collider_arg, fbx_path, identity=identity,
                blackbox=blackbox)

            proof_paths = []
            if proof is not None:
                proof_paths = (list(proof["sheets"].values())
                               + list(proof.get("lodSheets", {}).values())
                               + [proof["channelSheet"]])

            manifest_path = export_unity.write_manifest(
                os.path.join(package_dir,
                             export_unity.manifest_filename(FAMILY, variant.name)),
                identity, variant.reports,
                [law.NAME_MATERIAL.format(family=FAMILY.value, role=role)
                 for role in MATERIAL_ROLES],
                # No texture set: MAT_SargassumMicroFaunaBoids ships _BaseMap null, the
                # shader is Unlit, and there is no PBR set anywhere in the chain. Naming a
                # TX_* file that does not exist would be a false reference, so the manifest
                # records the gap honestly instead.
                [],
                [variant.collider] if variant.collider.obj is not None else [],
                proof_paths, export_result=export_result,
                uv_summary=variant.uv_summary, alpha_meaning=ALPHA_MEANING,
                extra={
                    "bodyPlan": {
                        "locomotion": "swimmer, carangiform -- amplitude concentrated aft "
                                      "of maximum body depth, snout rigid",
                        "deformationRoute": "offline baked Vertex Animation Texture (VAT) "
                                            "plus BatchRendererGroup indirect rendering. "
                                            "No skeleton, no blendshapes, no runtime mesh "
                                            "generation.",
                        "materialZones": "ONE slot (flesh/tissue). The indirect draw path "
                                         "renders submesh 0 only, so eye and organ zones "
                                         "are carried by geometry and vertex-colour masks "
                                         "instead of extra slots.",
                        "contactZones": "head (weak spot, aligned to the eye lenses), "
                                        "body mass, caudal peduncle and fin",
                        "silhouetteContrast": "thick mid-body mass, narrow caudal "
                                              "peduncle, four thin membranes, forked "
                                              "caudal blade, operculum step, eye lenses",
                        "asymmetry": "section centre sinks toward mid-body, flank skew on "
                                     "the +X side, right pectoral 6% shorter",
                    },
                    "consumerContract": variant.contract,
                    "axisConvention": "nose +Z, tail -Z, up +Y, lateral X; "
                                      "BoidFishInstanced.shader:12-18. Z is strictly the "
                                      "longest bounds axis so AnalyzeAxis cannot pick "
                                      "another.",
                    "vatReadiness": {
                        "vertexCountLOD0": len(variant.lods[0].obj.data.vertices),
                        "vatWidthWillEqual": len(variant.lods[0].obj.data.vertices),
                        "note": "FaunaSwarmVatPrefabBinder REFUSES a bake whose page width "
                                "!= boidMesh.vertexCount. Vertex ORDER is equally load-"
                                "bearing and is NOT checked anywhere, which is why the "
                                "importer block disables vertex optimisation and welding.",
                        "submeshCount": 1,
                        "uvSets": 1,
                        "normalsPresent": True,
                    },
                    "triangleBudgetDerivation":
                        "law.LOD_BUDGETS[Fauna] = (35000, 12000, 2000, impostor 12-500) "
                        "are HARD MAXIMA for a hero creature body (3dmodel.md:212). The "
                        "binding figure for a mass-instanced swarm unit is "
                        "BoidFishInstanced.shader:32 '2000 fish x 200 tris', with "
                        "REND_GPU_Driven_Animation_VAT.txt:138 capping TIER_LOW at 2048 "
                        "boids. LOD0 targets ~2x the 200-tri figure to carry a readable "
                        "near silhouette; LOD1 is the swarm-cost-matched level.",
                    "hitboxes": variant.hitboxes,
                    "animationFallback": {
                        "compact": "VAT page at reduced frame count; procedural tail wag "
                                   "in BoidFishInstanced.shader:513-545 is the fallback "
                                   "when no VAT is bound",
                        "middle": "VAT, 30 frames (the bake default)",
                        "high": "VAT, higher frame count within the 32 MB compact guard "
                                "(vertexCount x frameCount <= 2^20)",
                        "ultra": "same VAT route; no runtime mesh generation at any tier",
                    },
                    "fins": variant.fins,
                    "eyes": variant.eyes,
                    "topology": variant.topology,
                    "shading": {
                        "smoothAngleDeg": law.smooth_angle_for(SURFACE),
                        "smoothPolygons": variant.shading.smooth_polygons,
                        "sharpEdges": variant.shading.sharp_edges,
                        "weightedNormalsApplied": variant.shading.weighted_applied,
                    },
                    "vertexColorChannels": variant.vcol_report,
                    "vertexColorDirectReadback": variant.vcol_direct,
                    "lodShellTopology":
                        proof["lodTopology"] if proof is not None else {},
                    "aoBake": {
                        "baked": variant.ao.baked, "samples": variant.ao.samples,
                        "min": round(variant.ao.min_value, 5),
                        "max": round(variant.ao.max_value, 5),
                        "mean": round(variant.ao.mean_value, 5),
                        "hasContrast": variant.ao.has_contrast,
                    },
                    "deformationAmplitude": {
                        "min": round(variant.sway.min_value, 5),
                        "max": round(variant.sway.max_value, 5),
                        "stiffnessExponent": variant.sway.stiffness_exponent,
                        "relativeSpread": round(variant.sway.relative_spread, 5),
                        "uniform": variant.sway.is_uniform,
                        "metric": "authored per-vertex anatomy weight: 0.0 forward of "
                                  "maximum body depth, rising to 1.0 at the caudal tips, "
                                  "and high on every fin membrane regardless of axial "
                                  "position",
                    },
                    "biolum": "channel G is 0 across the whole fish: Sargassum micro-fauna "
                              "in the photic shallows is not emissive, and 3DMODEL_FAUNA.md "
                              "section 4 reserves G for a biolum mask/phase.",
                    "lawGapReported": "law.py has no fauna texel-density row; all four "
                                      "TEXEL_DENSITY_* constants are flora-named and cited "
                                      "to 3DMODEL_FLORA_CORAL.md section 5. This generator "
                                      "uses the COMMON-INSTANCED row rather than inventing "
                                      "a local number, and the missing row is reported as "
                                      "an h8forge diff.",
                    "channelMeasurements":
                        proof["measurements"] if proof is not None else [],
                    "generatorNotes": variant.notes,
                })

        _print_report(variant, proof, export_result, manifest_path)
    return exit_code


if __name__ == "__main__":
    sys.exit(main(sys.argv))
