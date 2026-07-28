"""Branching coral generator.

Specification: ``3DMODEL_FLORA_CORAL.md`` section 3 "Coral" -> "Branching coral:
welded trunk, branch hierarchy, knuckles, tip clusters, asymmetry", with section 3's
absolute constraint:

    "Branch intersections must be blended, welded, or explicitly hidden by knuckles.
     Intersecting tubes with z-fighting are rejected."

WHY A SKELETON PLUS SKIN, NOT SWEPT TUBES
-----------------------------------------
The obvious implementation -- generate a tube per branch and place them -- cannot
satisfy that constraint. Two tubes meeting at an angle interpenetrate, and the surfaces
inside each other z-fight exactly as the bible describes. Welding them afterwards means
solving a boolean union on organic geometry, which produces the degenerate slivers the
validator then rejects.

So the branch structure is authored as a VERTEX/EDGE SKELETON with a per-vertex radius,
and Blender's Skin modifier builds the surface. That inverts the problem: the surface is
generated as a single manifold hull around the skeleton, so branch unions are welded by
construction and the modifier itself thickens the joints into knuckles. There is no
intersection to fix because none is ever created.

That part of the previous design was sound and is kept. The GRAMMAR that fed it was not,
and was replaced wholesale after four consecutive silhouette rejections -- see below.

WHY THE PREVIOUS GRAMMAR WAS REPLACED, NOT TUNED
------------------------------------------------
``AGENTS.md`` ``[RULE] Universal route invalidation`` and ``TASTE.md`` "Visual Failure
Firewall" both require replacing the route owner after a repeated visual failure rather
than adjusting it. The rejection was "reads as a femur in a lump" four times. Measured on
the previous skeleton at its own default spec (seed 1712, quality 1.0), all four numbers
from ``build_skeleton`` itself, not inferred:

  height                 0.2667 m   for a declared ``height_m`` of 0.850 m
  canopy width           0.7140 m   -> the colony was 2.68x WIDER than it was TALL
  base node diameter     0.2153 m   -> 81% of the asset's own height
  radius min/max        10.92 : 1

Four independent causes, in descending order of how much of the rejected silhouette each
one owned. The mistake worth recording is that the most-cited cause -- the trunk:tip
ratio -- was only the fourth largest.

1. THE BASE BLOB WAS A BUG, NOT A TUNING VALUE. ``grow()`` was called with
   ``parent_index=0`` while node 1 already existed as a child of node 0, so node 0 ended
   up with TWO children: the first trunk segment and a stub. ``child_counts[0] == 2``
   then satisfied the fork test, and ``knuckle_swell`` (1.45) was applied ON TOP of the
   deliberate ``trunk_radius_m * 1.35`` base widening -- an effective 1.9575x, measured
   0.2153 m across. Node 1 meanwhile had zero children: a dead-end nub buried in the
   blob. Diagnosing that blob as "the 1.35 multiplier" understated it by 45%.

2. ``height_m`` WAS NOT THE HEIGHT. It was consumed once, as ``height_m * 0.20``, the
   length of the first internode. Everything after that was emergent, so a colony
   declared 0.85 m measured 0.2667 m -- 31% of its stated size. Every proportion judged
   against "a 0.85 m coral" was judged on a 0.27 m one, and the preview's scale witness
   was silently 3x out. This generator now GROWS IN UNIT SPACE AND NORMALISES, so
   ``height_m`` is an enforced output, checked in ``silhouette_report``.

3. THE CURRENT BIAS FLATTENED THE COLONY INTO A PLATE. ``current_bias`` 0.34 was added to
   a unit direction at every fork AND again inside every one of 8 segments per internode,
   at 2x the strength of the upward phototropism term. Nothing grew up. 0.714 m wide by
   0.267 m tall with the widest band at 60-70% height is not "branches clustered at the
   top" -- it is a horizontal splat of twigs, which is precisely the flared distal end of
   the femur that was being reported.

4. THE RATIOS WERE A TREE'S. 0.055 m trunk to 0.008 m tip is 6.88:1 declared and 10.92:1
   measured, compounded by ``radius_decay`` 0.62 and ``length_decay`` 0.68 per
   generation. By generation 4 an internode was 0.054 m long at 0.013 m radius -- a 4:1
   stub -- hanging off a 0.17 m x 0.055 m first internode, a 1.5:1 shaft. A tapering
   shaft with twig stubs is a tree; ``3DMODEL_FLORA_CORAL.md`` asks for a colony.

WHAT THE REFERENCE IMAGES ACTUALLY SHOW
--------------------------------------
Read directly from the mandatory folder (``beauty.webp``, ``shallows.webp``,
``nice_biome.webp``, ``Bioluminescent_ecology_concept``). Branching coral there is a
CLUSTER OF NEAR-CONSTANT-DIAMETER FINGERS rising from a small, low, spreading foot. The
identity comes from branch COUNT, the repeated fork, the outward splay and the blunt
swollen tips -- not from a hierarchy of thicknesses. The base is never the widest part of
the silhouette and there is no shaft anywhere.

So the grammar is now:

  encrusting foot: one launch lobe PER STEM, spread across the substrate and staggered in
  height, each with an outward crust tongue
    -> 4-5 primary stems, one from each lobe, so the colony branches AT the substrate
       instead of above a shared origin
    -> repeated DICHOTOMOUS forking, near-equal radius, in a fork plane that turns ~88
       degrees per generation (the orthogonal-alternating pattern of Acropora)
    -> blunt tip digit clusters, each digit at ~parent radius

The per-lobe launch replaced an earlier two-crown arrangement, which was itself an attempt
to break the candelabrum symmetry and did not go far enough: rendered, stems leaving from
two nearby central points still sat within a branch diameter of each other near the base,
and a skinned hull reads that as one trunk. Measured, the lowest 10% band went from
0.088 m to 0.237 m wide when each stem got its own anchor. Low valence at every base node
is a second benefit, since a high-valence skin node is where the modifier generates the
interior sheets that later have to be welded away.

``radius_decay`` is no longer a free parameter. It is DERIVED from the declared
stem:tip ratio and the generation count, so the ratio the bible cares about is a named
invariant instead of an emergent product of four multipliers.

The rest follows ``PROCEDURAL_ASSET_PIPELINE.md`` "Generation Order" in the stated
sequence. AO is baked BEFORE the vertex-colour compose, because the bake writes all four
channels and would otherwise erase the sway gradient -- see ``h8forge.vertexcolor``.
"""

from __future__ import annotations

import argparse
import math
import os
import sys
from dataclasses import dataclass, field
from typing import List, Optional

# Blender runs this file directly, so the package root is not on sys.path yet.
_HERE = os.path.dirname(os.path.abspath(__file__))
_BLENDER_TOOLS = os.path.dirname(_HERE)
if _BLENDER_TOOLS not in sys.path:
    sys.path.insert(0, _BLENDER_TOOLS)

import bmesh  # noqa: E402
import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

from h8forge import export_unity, law, mesh_ops, preview, validate, vertexcolor  # noqa: E402
from h8forge.blackbox import BlackBox, GenerationAborted  # noqa: E402


GENERATOR_NAME = "coral_branching"
# 2.0.0: skeleton grammar replaced (single-trunk recursion -> multi-stem dichotomous
# colony). Not a compatible tuning of 1.x -- the same seed produces a different asset.
GENERATOR_VERSION = "2.0.0"


# ---------------------------------------------------------------------------
# Growth parameters
# ---------------------------------------------------------------------------

@dataclass
class CoralSpec:
    """Deterministic growth description.

    Every field is a named parameter, never hidden chance:
    ``PROCEDURAL_ASSET_PIPELINE.md`` -- "If artist variation is needed, variation is a
    named seed, not hidden chance."

    Radii are FRACTIONS OF HEIGHT rather than absolute metres. A 0.3 m colony and a
    0.9 m colony are the same organism at two ages, so their proportions must be
    identical; absolute radii made every proportion a function of ``height_m`` by
    accident, which is how a 0.055 m trunk ended up on a 0.267 m plant.
    """

    seed: int = 1712
    quality: float = 1.0

    # ENFORCED above-ground height in metres. The skeleton is grown in unit space and
    # normalised to this, so it is an output guarantee, not a growth hint.
    #
    # 0.55 m, not 0.85 m, and the reason is a triangle budget rather than taste. A 0.85 m
    # staghorn thicket in nature carries HUNDREDS of ~2 cm branches; at the flora LOD0
    # ceiling of 6500 triangles only ~30-40 branches can be modelled, and 40 branches
    # spread over 0.85 m are necessarily slender. Rendered, that read as bare winter
    # brushwood -- correct topology, wrong organism. Reference colonies in
    # ``beauty.webp`` and ``Bioluminescent_ecology_concept`` are 0.2-0.5 m, where 30-40
    # chunky branches is what the animal actually looks like. Larger colonies are the
    # FLORA_CLUSTER family's problem (14000 tris), not this one's.
    height_m: float = 0.55

    # --- proportion, as fractions of height -------------------------------------
    # Reference read: branch diameter is a small and roughly CONSTANT fraction of a
    # colony's size, and the stem:tip ratio is ~2:1, not ~7:1.
    #
    # The absolute numbers were raised sharply after iteration 1: at 0.0225 the branches
    # were ~15 diameters long between forks and read as twigs. Coral branches run 3-6
    # diameters between forks -- that stubbiness IS the family's silhouette signature,
    # and it is the difference between a coral and a dead bush.
    stem_radius_frac: float = 0.042
    tip_radius_frac: float = 0.027
    # Holdfast lowered and the lobes enlarged after iteration 2 rendered the base as a
    # smooth EGG -- the same "lump" failure as the original generator, just smaller. A
    # single dominant sphere always reads as a bead; the lobes have to be big enough and
    # far enough out to BE the base rather than decorate it.
    holdfast_radius_frac: float = 0.060
    lobe_radius_frac: float = 0.056

    # --- colony structure --------------------------------------------------------
    # Multiple primary stems from a spreading foot. A single trunk cannot produce the
    # reference silhouette: at generation 1 there is exactly one axis, so a shaft is
    # unavoidable no matter how the radii are tuned.
    stem_count_min: int = 4
    stem_count_max: int = 5
    # Each stem launches from its OWN lobe on the encrusting pad, spread horizontally and
    # staggered in height, instead of from one or two central crowns. Iteration 3 rendered
    # the lower 25% as a single fused column because every stem left from nearly the same
    # point and only the tilt separated them -- within a branch diameter of each other, a
    # skinned hull reads that as one trunk. The references branch AT the substrate.
    holdfast_lobe_spread: float = 0.40
    holdfast_stagger: float = 0.085
    crust_lobe_chance: float = 0.7
    # Two tilt values ALTERNATING per stem, so the colony fills its envelope instead of
    # forming a uniform umbrella. Both were lowered after iteration 1 measured a 1.31 m
    # canopy on a 0.85 m colony: tilt plus four generations of fork half-angle accumulate
    # outward, so the crown sprawled into an open fan with gaps instead of the dense mass
    # the references show. They stay low now for a second reason -- the launch lobes are
    # already spread horizontally, so a stem no longer needs tilt in order to separate.
    stem_tilt_low_deg: float = 30.0
    stem_tilt_high_deg: float = 14.0
    stem_tilt_jitter_deg: float = 8.0

    # --- repeated dichotomous forking -------------------------------------------
    fork_generations: int = 4
    # 24, not 21. Thicker branches at a tight fork angle overlap their own sibling before
    # they separate: clearance after a fork is ``2 * L * sin(half_angle)`` against a branch
    # diameter of ``2 * radius``, so raising the radius without raising the angle buys
    # interior sheets. The canopy aspect that the tighter angle was protecting is now held
    # by ``upright_recovery`` instead, which converts sprawl into height rather than
    # forbidding it.
    fork_half_angle_deg: float = 24.0
    fork_angle_jitter_deg: float = 7.0
    # Outer forks are tighter than inner ones on a real colony, which keeps the crown
    # compact instead of letting the angle compound the same amount at every generation.
    fork_angle_taper: float = 0.30
    # Phototropism that GROWS with radial exposure: a branch that has wandered far from
    # the axis is more lit and turns back upward. This converts sprawl into height, which
    # is both the physical behaviour and the cheapest fix for the canopy aspect -- it
    # raises the numerator and lowers the denominator at once.
    upright_recovery: float = 0.32
    # Fork planes turn ~90 degrees per generation. This is the single strongest cue
    # separating branching coral from a whorled conifer: the previous grammar spread
    # children around the parent AXIS (a bottle-brush), coral forks in a PLANE that
    # alternates.
    fork_plane_turn_deg: float = 88.0
    fork_plane_jitter_deg: float = 26.0
    # Sympodial growth: one child continues nearly straight while its sibling turns
    # away. Common in Acropora and the main source of natural asymmetry.
    sympodial_chance: float = 0.40
    sympodial_dominance: float = 0.42

    # Shorter terminal internodes: the outer branches should be stubs, which is what
    # produces the blunt crowded crown rather than a spray of wands.
    length_decay: float = 0.80
    internode_jitter: float = 0.15

    # Knuckle at a fork, not a joint sphere. 1.45 was visible as a bead at every union;
    # a real fork thickens by roughly a tenth.
    knuckle_swell: float = 1.13
    # Persistent per-branch curvature: a fixed turn per segment produces a clean arc,
    # which is what a growing branch does. The previous random walk produced wiggle.
    branch_curve_deg: float = 11.0
    segment_jitter: float = 0.05

    # Asymmetry: coral grows into current and toward light. Both are now WEAK and the
    # flow term is applied once per internode instead of once per segment -- at 0.34
    # per segment it was the dominant growth direction and flattened the colony to a
    # 2.68:1 plate.
    current_direction: tuple = (0.62, 0.28, 0.0)
    current_bias: float = 0.085
    phototropism: float = 0.20

    # Mineralised skeleton: rigid, so sway is capped into the bible's 0..32/255 band.
    mineralised: bool = True

    # Surface character. Displacement is proportional to LOCAL branch radius, so the
    # amplitude fraction below is what actually matters, not an absolute distance.
    # Raised after iteration 2: at 0.34 x radius on a thick branch the relief was ~14% of
    # the radius and the branch outlines rendered as clean cylinders. ``3dmodel.md``
    # section 5 requires "secondary silhouette noise ... nonuniform cross-sections", which
    # is a SILHOUETTE property -- it has to be visible on the outline, not just in shading.
    pore_strength: float = 0.58
    displacement_radius_fraction: float = 0.52
    ring_frequency: float = 44.0
    ring_strength: float = 0.26
    # Corallite cups: high frequency, low amplitude. This is the coral surface
    # signature. The earlier "fine pore" band was low frequency and read as cauliflower.
    corallite_frequency: float = 120.0
    corallite_weight: float = 0.34
    coarse_lobe_weight: float = 0.44
    # 1-2 digits, not 2-3. Iteration 1 produced 112 digit tips on 32 terminal branches;
    # at 6500 triangles that is 52 tris per digit and decimation blurred them into the
    # branch instead of reading as a cluster. Fewer, thicker, blunter digits survive the
    # collapse and are what the reference tips actually look like.
    tip_digits_min: int = 1
    tip_digits_max: int = 2
    tip_swell: float = 1.14

    large_enough_to_block_path: bool = False

    # --- derived, never hand-set -------------------------------------------------

    def radius_decay(self) -> float:
        """Per-fork radius factor DERIVED from the declared stem:tip ratio.

        ``3DMODEL_FLORA_CORAL.md`` section 3 asks for a "branch hierarchy", not a
        thickness cliff. Deriving the decay makes the stem:tip ratio a stated invariant:
        with the defaults it is exactly 0.0225/0.0115 = 1.96:1 whatever the generation
        count. Hand-setting the decay is how the previous version reached 10.92:1
        without any single number looking wrong.
        """
        generations = max(1, self.fork_generations)
        ratio = law.saturate(self.tip_radius_frac / max(1e-9, self.stem_radius_frac))
        return float(pow(max(1e-6, ratio), 1.0 / generations))

    def stem_tip_ratio(self) -> float:
        return self.stem_radius_frac / max(1e-9, self.tip_radius_frac)

    def branch_segments(self, depth: int) -> int:
        """Skeleton subdivisions per internode. Continuous in GlobalQualityWeight.

        Lower generations get more segments because they are longer and their arc is
        what the eye reads; a 4 cm terminal internode gains nothing from 6 segments and
        costs skin geometry that decimation then has to throw away.
        """
        base = 3 if depth <= 1 else 2
        return int(round(base + 1.0 * law.saturate(self.quality)))

    def skin_subdivisions(self) -> int:
        """Surface refinement level. Continuous, and never zero -- an unsubdivided skin
        hull is a faceted tube, which is the primitive look the bible rejects."""
        return int(round(1 + 1 * law.saturate(self.quality)))


@dataclass
class SkeletonNode:
    position: Vector
    radius: float
    depth: int
    parent: Optional[int]
    distance_from_anchor: float
    is_tip: bool = False
    # Base/holdfast nodes must never receive the fork knuckle swell. The previous
    # version had no way to say so, and the base node -- which had two children by
    # accident -- was swollen as if it were a branch fork.
    is_base: bool = False


@dataclass
class CoralResult:
    name: str
    lods: list = field(default_factory=list)
    collider: Optional[object] = None
    sway_report: dict = field(default_factory=dict)
    ao_report: Optional[object] = None
    node_count: int = 0
    tip_count: int = 0
    fork_count: int = 0
    silhouette: dict = field(default_factory=dict)
    topology: list = field(default_factory=list)
    preview_paths: tuple = ()
    channel_stats: tuple = ()
    mesh_reports: list = field(default_factory=list)
    fbx_path: str = ""
    manifest_path: str = ""


# ---------------------------------------------------------------------------
# Stage 2: shape grammar -- the colony skeleton
# ---------------------------------------------------------------------------

def _rotated(vector: Vector, axis: Vector, angle: float) -> Vector:
    out = vector.copy()
    out.rotate(Matrix.Rotation(angle, 4, axis))
    return out


def _perpendicular(direction: Vector) -> Vector:
    """Any unit vector perpendicular to ``direction``, chosen stably.

    Crossing with a fixed axis fails when the direction is parallel to it, which for a
    vertical stem is the common case, not the edge case.
    """
    reference = Vector((0.0, 0.0, 1.0))
    if abs(direction.dot(reference)) > 0.94:
        reference = Vector((1.0, 0.0, 0.0))
    out = direction.cross(reference)
    if out.length < 1e-6:
        return Vector((1.0, 0.0, 0.0))
    out.normalize()
    return out


def build_skeleton(spec: CoralSpec, blackbox: BlackBox) -> List[SkeletonNode]:
    """Multi-stem dichotomous colony, grown in unit space then normalised to height.

    Two-pass on purpose. Positions are grown with a first-internode length of 1.0 and
    radii carried as a RELATIVE multiplier chain; only afterwards is the whole skeleton
    scaled so its above-ground extent equals ``spec.height_m`` and the relative radii
    are resolved against that height. That is what makes ``height_m`` and the stem:tip
    ratio enforced outputs rather than growth hints -- the failure mode of the previous
    version, which declared 0.85 m and produced 0.2667 m.
    """
    rng = np.random.default_rng(spec.seed)
    current = Vector(spec.current_direction)
    if current.length > 1e-6:
        current.normalize()
    up = Vector((0.0, 0.0, 1.0))

    decay = spec.radius_decay()
    ratio_stem = 1.0
    ratio_holdfast = spec.holdfast_radius_frac / max(1e-9, spec.stem_radius_frac)
    ratio_lobe = spec.lobe_radius_frac / max(1e-9, spec.stem_radius_frac)

    # Relative radius carried per node; resolved to metres in pass 2.
    nodes: List[SkeletonNode] = []
    rel_radius: List[float] = []

    def add(position: Vector, rel: float, depth: int, parent: Optional[int],
            distance: float, *, is_tip: bool = False, is_base: bool = False) -> int:
        nodes.append(SkeletonNode(position=position, radius=0.0, depth=depth,
                                  parent=parent, distance_from_anchor=distance,
                                  is_tip=is_tip, is_base=is_base))
        rel_radius.append(rel)
        return len(nodes) - 1

    # ------------------------------------------------------------------
    # ENCRUSTING FOOT.
    #
    # Two jobs, and both are anatomy rather than topology. 3DMODEL_FLORA_CORAL.md
    # section 3 requires "anchor geometry", and the Skin modifier leaves its root vertex
    # OPEN -- a visible hole with the stem interior showing through. A node BELOW the
    # substrate plane closes the volume because the modifier skins around it.
    #
    # The previous version solved the hole with a single large sphere and created the
    # "lump". A foot is LOW AND SPREADING: shallow lobes that reach outward at the
    # substrate rather than one ball that reaches upward. Same closed volume, and it is
    # what the reference images actually show under a coral.
    # ------------------------------------------------------------------
    root = add(Vector((0.0, 0.0, -0.055)), ratio_holdfast, 0, None, 0.0, is_base=True)

    # ONE LAUNCH LOBE PER STEM, spread across the pad and staggered in height. The lobe is
    # both the anchor geometry the bible asks for and the stem's origin, so the colony
    # branches at the substrate the way the reference colonies do. Valence stays low --
    # root carries only the lobes, each lobe carries its stem plus at most one crust
    # tongue -- because a high-valence skin node is where the modifier produces the
    # interior sheets that later become black slits.
    stem_count = int(rng.integers(spec.stem_count_min, spec.stem_count_max + 1))
    pad_phase = float(rng.uniform(0.0, math.tau))
    pads: List[tuple] = []
    for index in range(stem_count):
        azimuth = (pad_phase + math.tau * index / stem_count
                   + float(rng.normal(0.0, 0.19)))
        spread = spec.holdfast_lobe_spread * float(rng.uniform(0.78, 1.28))
        height = (-0.014
                  + spec.holdfast_stagger * (index % 2)
                  + float(rng.uniform(-0.010, 0.022)))
        lobe = add(Vector((math.cos(azimuth) * spread,
                           math.sin(azimuth) * spread,
                           height)),
                   ratio_lobe * float(rng.uniform(0.88, 1.12)), 0, root, 0.06,
                   is_base=True)
        pads.append((lobe, azimuth))
        # Crust tongue: a flat outward-and-down lobe under the stem, so the foot reads as
        # a spreading encrustation rather than a ring of beads.
        if float(rng.random()) < spec.crust_lobe_chance:
            tongue = spread * float(rng.uniform(1.30, 1.70))
            add(Vector((math.cos(azimuth) * tongue,
                        math.sin(azimuth) * tongue,
                        -0.060 + float(rng.uniform(-0.008, 0.008)))),
                ratio_lobe * float(rng.uniform(0.62, 0.82)), 0, lobe, 0.10,
                is_base=True)

    fork_count = [0]

    def grow(parent_index: int, direction: Vector, length: float,
             rel: float, depth: int, plane_normal: Vector) -> None:
        """One internode, then either a dichotomous fork or a tip digit cluster."""
        segments = spec.branch_segments(depth)
        segment_length = length / segments
        heading = direction.normalized()

        # Persistent curvature: one fixed turn per segment about a stable axis, so the
        # internode arcs instead of wandering. Direction of the arc is seeded per branch.
        curve_axis = _perpendicular(heading)
        curve_axis = _rotated(curve_axis, heading, float(rng.uniform(0.0, math.tau)))
        curve_step = math.radians(spec.branch_curve_deg) / segments

        cursor = parent_index
        for step in range(segments):
            heading = _rotated(heading, curve_axis, curve_step)
            jitter = Vector((float(rng.normal(0.0, spec.segment_jitter)),
                            float(rng.normal(0.0, spec.segment_jitter)),
                            float(rng.normal(0.0, spec.segment_jitter * 0.6))))
            heading = heading + jitter + up * (spec.phototropism * 0.06)
            if heading.length < 1e-6:
                heading = Vector((0.0, 0.0, 1.0))
            heading.normalize()

            source = nodes[cursor]
            position = source.position + heading * segment_length
            # Radius is CONSTANT along an internode and steps down only at the fork.
            # The previous version interpolated radius inside every internode, which is
            # a tree's continuous taper; a coral branch is a near-cylinder between forks.
            cursor = add(position, rel, depth, cursor,
                         source.distance_from_anchor + segment_length)

        if depth >= spec.fork_generations:
            nodes[cursor].is_tip = True
            _add_tip_digits(nodes, rel_radius, add, cursor, rel, spec, rng, heading)
            return

        fork_count[0] += 1
        child_rel = rel * decay

        # Dichotomous fork in a PLANE. Rotating the heading about the plane normal keeps
        # both children inside the plane spanned by heading and (heading x normal).
        normal = plane_normal - heading * plane_normal.dot(heading)
        if normal.length < 1e-6:
            normal = _perpendicular(heading)
        normal.normalize()

        # Fork half-angle tapers with depth: an outer fork on a real colony is tighter
        # than the first one, so the same nominal angle at every generation compounds the
        # crown outward faster than the organism does.
        taper = 1.0 - spec.fork_angle_taper * (depth - 1) / max(1, spec.fork_generations)
        half = math.radians(spec.fork_half_angle_deg) * max(0.35, taper)
        angle_a = half + math.radians(float(rng.normal(0.0, spec.fork_angle_jitter_deg)))
        angle_b = half + math.radians(float(rng.normal(0.0, spec.fork_angle_jitter_deg)))
        scale_a = 1.0
        scale_b = 1.0
        if float(rng.random()) < spec.sympodial_chance:
            # One child dominates: shallow angle, longer internode. The sibling becomes
            # a short lateral. This is the asymmetry section 3 demands, taken from the
            # organism rather than from noise.
            angle_a *= spec.sympodial_dominance
            scale_a = 1.16
            scale_b = 0.78

        # Radial exposure of the fork point, in unit space where the first internode is
        # 1.0. Drives the upright recovery below.
        fork_position = nodes[cursor].position
        radial = math.hypot(fork_position.x, fork_position.y)

        for sign, angle, length_scale in ((1.0, angle_a, scale_a),
                                          (-1.0, angle_b, scale_b)):
            child_dir = _rotated(heading, normal, sign * angle)
            # Flow and light applied ONCE here, weakly, not per segment. The previous
            # version pushed 0.095 of flow into every one of eight segments per
            # internode, which is why nothing grew upward.
            child_dir = (child_dir
                         + current * (spec.current_bias * (0.5 + 0.5 * depth
                                                           / max(1, spec.fork_generations)))
                         + up * (spec.phototropism * 0.16)
                         + up * (spec.upright_recovery * min(1.6, radial)))
            if child_dir.length < 1e-6:
                continue
            child_dir.normalize()

            # Fork plane turns ~90 degrees about the child so the next fork is
            # orthogonal to this one.
            turn = math.radians(spec.fork_plane_turn_deg
                                + float(rng.normal(0.0, spec.fork_plane_jitter_deg)))
            child_normal = _rotated(normal, child_dir, turn)

            child_length = (length * spec.length_decay * length_scale
                            * float(rng.uniform(1.0 - spec.internode_jitter,
                                                1.0 + spec.internode_jitter)))
            grow(cursor, child_dir, child_length, child_rel, depth + 1, child_normal)

    # One primary stem per launch lobe. Tilt alternates between the wide and the steep
    # value so the colony is not a uniform umbrella; because the lobes are already spread
    # horizontally, the stems no longer need a large tilt to separate, and a smaller tilt
    # is what keeps the canopy aspect near 1.
    for index, (lobe, azimuth) in enumerate(pads):
        tilt_deg = (spec.stem_tilt_low_deg if index % 2 == 0
                    else spec.stem_tilt_high_deg)
        tilt = math.radians(tilt_deg + float(rng.normal(0.0, spec.stem_tilt_jitter_deg)))
        # Lean along the lobe's own azimuth, so a stem continues the direction its anchor
        # already spread in rather than crossing over its neighbours.
        lean = azimuth + float(rng.normal(0.0, 0.30))
        direction = Vector((math.sin(tilt) * math.cos(lean),
                            math.sin(tilt) * math.sin(lean),
                            math.cos(tilt)))
        direction.normalize()
        plane_normal = _perpendicular(direction)
        plane_normal = _rotated(plane_normal, direction,
                                float(rng.uniform(0.0, math.tau)))
        grow(lobe, direction, 1.0 * float(rng.uniform(0.86, 1.14)),
             ratio_stem, 1, plane_normal)

    # ------------------------------------------------------------------
    # PASS 2: normalise to the declared height and resolve radii.
    # ------------------------------------------------------------------
    max_z = max((node.position.z for node in nodes), default=0.0)
    if max_z <= 1e-6:
        blackbox.note_invalid("skeleton", "SKELETON_DEGENERATE",
                              "colony has no positive vertical extent")
        raise GenerationAborted("coral skeleton has no height")
    scale = spec.height_m / max_z
    stem_radius_m = spec.stem_radius_frac * spec.height_m
    for index, node in enumerate(nodes):
        node.position = node.position * scale
        node.distance_from_anchor *= scale
        node.radius = max(1e-4, rel_radius[index] * stem_radius_m)

    tips = sum(1 for n in nodes if n.is_tip)
    blackbox.record("skeleton", seed=spec.seed, family=law.Family.FLORA.value,
                    vertex_count=len(nodes),
                    warning="" if tips else "skeleton produced no tips")
    blackbox.record("skeleton_grammar",
                    warning="stems={s} forks={f} tips={t} decay={d:.4f} "
                            "stem:tip={r:.2f}:1".format(
                                s=stem_count, f=fork_count[0], t=tips,
                                d=decay, r=spec.stem_tip_ratio()))
    return nodes


def _add_tip_digits(nodes: List[SkeletonNode], rel_radius: List[float], add,
                    tip_index: int, rel: float, spec: CoralSpec, rng,
                    heading: Vector) -> None:
    """Blunt digit cluster at a branch end.

    Section 3 lists "tip clusters" as a required structure for branching coral. The
    previous implementation scattered 3 nubs in random directions at ``2.2..4.0x`` the
    tip radius in length and ``0.85..1.25x`` in radius -- which read as popcorn, and let
    the radius drift BELOW the declared tip radius (measured minimum 0.0068 m against a
    0.008 m floor, part of how the 10.92:1 spread happened).

    An Acropora branch end is a short cluster of near-parallel digits, each about as
    thick as the branch and slightly swollen. So: a mini-fork forward, tight angles,
    radius at ``tip_swell`` of the parent. Blunt, not pointed, and never thinner than
    the branch it sits on.
    """
    tip = nodes[tip_index]
    count = int(rng.integers(spec.tip_digits_min, spec.tip_digits_max + 1))
    if count <= 0:
        return
    axis = _perpendicular(heading)
    axis = _rotated(axis, heading, float(rng.uniform(0.0, math.tau)))
    for digit in range(count):
        angle = math.radians(float(rng.uniform(9.0, 24.0)))
        spin = math.tau * digit / max(1, count) + float(rng.uniform(-0.3, 0.3))
        direction = _rotated(heading, axis, angle)
        direction = _rotated(direction, heading, spin)
        if direction.length < 1e-6:
            continue
        direction.normalize()
        # Length in the same unit space the skeleton is grown in; pass 2 scales it.
        length = float(rng.uniform(0.055, 0.10))
        add(tip.position + direction * length,
            rel * spec.tip_swell * float(rng.uniform(0.94, 1.04)),
            tip.depth + 1, tip_index,
            tip.distance_from_anchor + length, is_tip=True)


def silhouette_report(nodes: List[SkeletonNode], spec: CoralSpec,
                      blackbox: Optional[BlackBox] = None) -> dict:
    """Measured proportions of the skeleton, with the gates the rejections implied.

    This exists because four silhouette rejections in a row were all describable as
    numbers that nobody had printed. ``AGENTS.md`` forbids adding a checker over a
    failure that is not concrete; this one is the opposite case -- the failure was
    concrete and repeated, and the numbers were available inside ``build_skeleton`` the
    whole time. It does not replace opening the render; it makes the render's verdict
    reproducible.
    """
    bands = 10
    band_width = [0.0] * bands
    max_z = max((n.position.z for n in nodes), default=0.0)
    for node in nodes:
        if node.position.z < 0.0:
            continue
        index = min(bands - 1, int(bands * node.position.z / max(1e-9, max_z)))
        reach = (math.hypot(node.position.x, node.position.y) + node.radius) * 2.0
        band_width[index] = max(band_width[index], reach)

    canopy = max(band_width) if band_width else 0.0
    widest_band = band_width.index(canopy) if canopy > 0.0 else 0
    base_diameter = max((n.radius for n in nodes if n.is_base), default=0.0) * 2.0
    branch_radii = [n.radius for n in nodes if not n.is_base]
    radius_spread = (max(branch_radii) / max(1e-9, min(branch_radii))
                     if branch_radii else 0.0)

    # Fork census straight off the parent links, so "forks repeatedly" is a counted
    # property of the delivered skeleton rather than an intention in the grammar.
    child_counts = [0] * len(nodes)
    for node in nodes:
        if node.parent is not None:
            child_counts[node.parent] += 1
    forks = sum(1 for index, node in enumerate(nodes)
                if child_counts[index] > 1 and not node.is_base)

    report = {
        "forkCount": forks,
        "tipCount": sum(1 for n in nodes if n.is_tip),
        "heightM": round(max_z, 4),
        "heightRequestedM": round(spec.height_m, 4),
        "heightError": round(abs(max_z - spec.height_m), 5),
        "canopyWidthM": round(canopy, 4),
        "canopyAspect": round(canopy / max(1e-9, max_z), 3),
        "widestBandPercent": widest_band * 10,
        "baseDiameterM": round(base_diameter, 4),
        "baseOverCanopy": round(base_diameter / max(1e-9, canopy), 3),
        "baseOverHeight": round(base_diameter / max(1e-9, max_z), 3),
        "branchRadiusSpread": round(radius_spread, 3),
        "declaredStemTipRatio": round(spec.stem_tip_ratio(), 3),
        "bandWidthsM": [round(w, 4) for w in band_width],
    }

    # Gates, each traceable to a specific rejection sentence.
    failures = []
    if report["heightError"] > spec.height_m * 0.02:
        failures.append("height {h} != requested {r}".format(
            h=report["heightM"], r=report["heightRequestedM"]))
    if report["baseOverCanopy"] > 0.42:
        failures.append("base blob: base diameter is {p}% of canopy width".format(
            p=int(report["baseOverCanopy"] * 100)))
    if report["widestBandPercent"] < 30 or report["widestBandPercent"] > 90:
        failures.append("mass distribution: widest band at {b}% height".format(
            b=report["widestBandPercent"]))
    if report["canopyAspect"] > 1.9:
        failures.append("flat splat: canopy is {a}x wider than tall".format(
            a=report["canopyAspect"]))
    if report["branchRadiusSpread"] > 3.2:
        failures.append("thickness cliff: branch radius spread {s}:1".format(
            s=report["branchRadiusSpread"]))
    report["gateFailures"] = failures
    report["gatesPassed"] = not failures

    if blackbox is not None:
        blackbox.record("silhouette",
                        warning="; ".join(failures) if failures else "",
                        failure_code="SILHOUETTE_GATE" if failures else "")
    return report


# ---------------------------------------------------------------------------
# Stage 3: skeleton -> welded surface
# ---------------------------------------------------------------------------

def skeleton_to_object(nodes: List[SkeletonNode], spec: CoralSpec,
                       name: str, collection: bpy.types.Collection,
                       blackbox: BlackBox) -> bpy.types.Object:
    """Skin the skeleton into one manifold surface with welded joints.

    The Skin modifier requires a mesh of verts and EDGES with no faces, one vertex
    marked as root, and a per-vertex radius pair. Joint radius is swollen by
    ``knuckle_swell`` at branch points, which is what turns a Y-junction into the
    knuckle the bible asks for rather than a smooth taper through the fork.

    The swell is withheld from base nodes. Previously it was not, and because the base
    node had two children by accident it was swollen as if it were a fork -- 1.35 x 1.45
    = 1.9575x the trunk radius, measured 0.2153 m across on a 0.2667 m plant. That was
    the "smooth blob wider than the entire branch canopy".
    """
    mesh = bpy.data.meshes.new(name + "_skeleton")
    vertices = [tuple(node.position) for node in nodes]
    edges = [(index, node.parent) for index, node in enumerate(nodes)
             if node.parent is not None]
    mesh.from_pydata(vertices, edges, [])
    mesh.update()

    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)

    modifier = obj.modifiers.new(name="H8_Skin", type="SKIN")
    modifier.use_smooth_shade = True
    # Raised from 0.22: branch_smoothing blends the hull THROUGH a junction instead of
    # letting two limb tubes crease into each other, which is the cheapest reduction in the
    # interior-sheet count at a fork and is the "blended" option section 3 offers alongside
    # welding and knuckles.
    modifier.branch_smoothing = 0.34

    # Count children per node so forks can be identified and swollen.
    child_counts = [0] * len(nodes)
    for node in nodes:
        if node.parent is not None:
            child_counts[node.parent] += 1

    swollen = 0
    skin_layer = mesh.skin_vertices[0].data
    for index, node in enumerate(nodes):
        radius = node.radius
        if child_counts[index] > 1 and not node.is_base:
            radius *= spec.knuckle_swell
            swollen += 1
        skin_layer[index].radius = (radius, radius)
        skin_layer[index].use_root = (node.parent is None)

    mesh_ops._make_sole_active(obj)
    bpy.ops.object.modifier_apply(modifier=modifier.name)

    blackbox.record("skin", vertex_count=len(obj.data.vertices),
                    triangle_count=mesh_ops.triangle_count(obj.data),
                    warning="knuckles swollen={n}".format(n=swollen))
    return obj


@dataclass
class SkeletonSampler:
    """Nearest-skeleton-node lookup: local branch radius and geodesic distance per point.

    Both quantities are needed and neither can be derived from the vertex position alone:

    - Displacement amplitude must scale with LOCAL BRANCH THICKNESS. An earlier version
      scaled it by horizontal distance from the Z axis, which gave the trunk -- sitting
      on the axis -- almost no displacement while the outer tips got the full amount.
      The visible result was a porcelain-smooth stem with popcorn florets, and the
      amplitude disparity was violent enough to self-intersect the tip geometry.
    - Sway must use GEODESIC distance along the branch, not straight-line distance from
      the anchor. A branch that arcs back over its own base is far along the stem but
      physically near the holdfast, and Euclidean distance would call its tip rigid.
    """

    tree: object
    radii: list
    distances: list

    @classmethod
    def build(cls, nodes: List[SkeletonNode]) -> "SkeletonSampler":
        from mathutils import kdtree

        tree = kdtree.KDTree(len(nodes))
        for index, node in enumerate(nodes):
            tree.insert(node.position, index)
        tree.balance()
        return cls(
            tree=tree,
            radii=[node.radius for node in nodes],
            distances=[node.distance_from_anchor for node in nodes],
        )

    def sample(self, point: Vector) -> tuple:
        """(local_radius, geodesic_distance) at the nearest skeleton node."""
        _co, index, _dist = self.tree.find(point)
        if index is None:
            return (0.01, 0.0)
        return (self.radii[index], self.distances[index])


def refine_surface(obj: bpy.types.Object, spec: CoralSpec,
                   sampler: SkeletonSampler, blackbox: BlackBox) -> None:
    """Subdivide, then displace along normals for corallites and secondary silhouette.

    ``3dmodel.md`` section 5: "the saved mesh must contain secondary silhouette noise,
    believable taper, compression, scars, growth rings, cavities, and nonuniform
    cross-sections." The skin hull supplies taper and cross-section variation; this
    stage supplies the surface history.

    Three frequency bands, not one, because a single isotropic noise reads as crust:
      - growth rings, ANISOTROPIC along the branch axis -- the structural signature that
        makes a stem read as grown rather than extruded;
      - coarse lobes, which carve the cavities the AO bake needs to find;
      - CORALLITE cups at high frequency and low amplitude. This band replaces the
        previous "fine pore" term, which was low frequency (31x object space) and read
        as cauliflower rather than as the cup field on a coral branch.

    Total displacement is clamped to ``displacement_radius_fraction`` of the LOCAL branch
    radius. The previous coefficient allowed 0.5x the radius, which on a thin branch is
    enough to pinch it or to swallow a fork.

    All bands are hashed from object-space position plus the seed, so the same seed
    reproduces the same surface. ``mathutils.noise`` is avoided on purpose: its seed is
    process-global, so two generators in one Blender session would perturb each other.
    """
    levels = spec.skin_subdivisions()
    if levels > 0:
        modifier = obj.modifiers.new(name="H8_Subsurf", type="SUBSURF")
        modifier.subdivision_type = "CATMULL_CLARK"
        modifier.levels = levels
        modifier.render_levels = levels
        mesh_ops._make_sole_active(obj)
        bpy.ops.object.modifier_apply(modifier=modifier.name)

    strength = spec.pore_strength * (0.55 + 0.45 * law.saturate(spec.quality))
    if strength <= 0.0:
        return

    mesh = obj.data
    seed_offset = float(spec.seed % 9973) * 0.013
    normaliser = max(1e-6, spec.ring_strength + spec.coarse_lobe_weight
                     + spec.corallite_weight)

    for vertex in mesh.vertices:
        position = vertex.co.copy()
        local_radius, geodesic = sampler.sample(position)

        # Growth rings: banded along the branch, so the modulation follows the structure
        # instead of sitting on top of it.
        rings = math.sin(geodesic * spec.ring_frequency + seed_offset * 3.1)
        coarse = _value_noise(position * 9.0, seed_offset)
        corallite = _value_noise(position * spec.corallite_frequency, seed_offset + 4.77)

        amount = (rings * spec.ring_strength
                  + coarse * spec.coarse_lobe_weight
                  + corallite * spec.corallite_weight) / normaliser
        # Amplitude proportional to the LOCAL branch radius: a 10 mm tip and a 19 mm
        # stem both get proportionate relief, and nothing is displaced past a third of
        # its own thickness into a self-intersection or a pinched fork.
        vertex.co = position + vertex.normal * (
            amount * local_radius * spec.displacement_radius_fraction * strength)

    mesh.update()
    blackbox.record("refine_surface", vertex_count=len(mesh.vertices),
                    triangle_count=mesh_ops.triangle_count(mesh))


def _value_noise(point: Vector, seed_offset: float) -> float:
    """Deterministic smooth noise in -1..1 from a position hash.

    Trigonometric hashing rather than ``mathutils.noise``: that module's seed is global
    process state, so two generators in one Blender session would perturb each other and
    neither would be reproducible from its own seed.
    """
    x = point.x + seed_offset
    y = point.y - seed_offset * 0.5
    z = point.z + seed_offset * 0.25
    value = (math.sin(x * 1.7 + math.cos(y * 2.3) * 1.9 + z * 0.7)
             + math.sin(y * 2.9 + math.cos(z * 1.3) * 1.4 + x * 0.5)
             + math.sin(z * 2.1 + math.cos(x * 1.1) * 2.2 + y * 0.9))
    return max(-1.0, min(1.0, value / 3.0))


# ---------------------------------------------------------------------------
# Stage 5: UVs and material slots
# ---------------------------------------------------------------------------

def unwrap_and_assign_materials(obj: bpy.types.Object, spec: CoralSpec,
                                blackbox: BlackBox) -> dict:
    """Angle-preserving unwrap plus the bible's material slot roles.

    ``3dmodel.md`` section 6 permits "Conformal unwrap using LSCM/ABF-style angle
    preservation for unique surfaces" -- Blender's Smart UV Project is that class of
    solver, so this is the sanctioned route rather than a box projection.

    ``3DMODEL_FLORA_CORAL.md`` section 5 allows triplanar for massive rock-like coral
    but is explicit that "branch tubes still need coherent UVs for detail normal and
    phase masks". A branching colony is tubes, so it gets real UVs.

    Slots follow section 6: 0 primary tissue, 1 exposed cut/scar, 2 growth plate /
    barnacle trim, 3 emissive polyps.
    """
    for role, name in (
        (law.MATERIAL_SLOT_PRIMARY, "Tissue"),
        (law.MATERIAL_SLOT_CUT_EDGE, "BrokenTip"),
        (law.MATERIAL_SLOT_TRIM, "GrowthPlate"),
        (law.MATERIAL_SLOT_EMISSIVE, "Polyp"),
    ):
        material_name = law.NAME_MATERIAL.format(family=law.Family.FLORA.value, role=name)
        material = bpy.data.materials.get(material_name)
        if material is None:
            material = bpy.data.materials.new(material_name)
            material.use_nodes = True
        obj.data.materials.append(material)

    mesh_ops._make_sole_active(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0),
                             island_margin=0.012,
                             correct_aspect=True,
                             scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")

    uv_layer = obj.data.uv_layers.active
    report = {
        "uvRoute": "smart_project (LSCM-class conformal), island_margin=0.012",
        "uvLayer": uv_layer.name if uv_layer else None,
        "materialSlots": [slot.material.name for slot in obj.material_slots],
        "texelDensityTarget": law.TEXEL_DENSITY_COMMON_FLORA,
    }
    blackbox.record("unwrap", vertex_count=len(obj.data.vertices),
                    warning="" if uv_layer else "no active UV layer after unwrap")
    return report


# ---------------------------------------------------------------------------
# Stage 6: bakes and vertex colours
# ---------------------------------------------------------------------------

def author_channels(obj: bpy.types.Object, spec: CoralSpec,
                    sampler: SkeletonSampler, blackbox: BlackBox) -> tuple:
    """Bake AO first, then compose R/G/B/A. Order is not cosmetic -- see module docstring.

    R uses ``STIFFNESS_EXPONENT_BRANCHING_CORAL`` and, for a mineralised colony, the
    rigid cap that keeps values inside the bible's 0..32/255 band while PRESERVING a
    gradient inside it. Clamping to a constant would satisfy the band and then trip the
    "root sways as much as tips" rejection gate.
    """
    # AO ray length DERIVED from branch thickness, not a fixed 0.22 m.
    #
    # 0.22 m was hardcoded, and on a 0.55 m colony that is 40% of the whole organism: every
    # branch occludes every other one across the entire crown and the local cavity term the
    # bible actually asks for ("low values in crevices, under plates, root clusters, and
    # branch intersections") is buried under a global sky term. Measured: AO mean 0.792 on a
    # sparse 0.85 m colony, then 0.023 on the denser 0.55 m one -- the same distance
    # behaving as bounded on one and effectively unbounded on the other. A near-black B
    # channel is not occlusion data, and ``3dmodel.md`` forbids shipping darkness in place
    # of information.
    #
    # Cavity scale on a branching colony is a small multiple of the branch radius, so that
    # is what the distance is expressed in. It now scales correctly with ``height_m``.
    ao_distance = max(0.02, spec.stem_radius_frac * spec.height_m * 2.6)
    ao_result = vertexcolor.bake_ambient_occlusion(
        obj, samples=int(24 + 40 * law.saturate(spec.quality)),
        distance=ao_distance, blackbox=blackbox)
    ao_values = vertexcolor.consume_baked_ao(obj)

    lo, hi = mesh_ops.local_bounds(obj)
    anchor = Vector((0.0, 0.0, lo.z))
    flexible_length = max(1e-4, (hi.z - lo.z))

    # Geodesic distance along the branch, sampled from the skeleton, rather than
    # straight-line distance from the anchor. See SkeletonSampler for why that matters
    # on a colony whose branches arc back over their own base.
    geodesic = [sampler.sample(v.co)[1] for v in obj.data.vertices]
    max_geodesic = max(geodesic) if geodesic else flexible_length

    sway = vertexcolor.build_sway_field(
        obj.data,
        anchor_position=anchor,
        max_flexible_length=max(1e-4, max_geodesic),
        stiffness_exponent=law.STIFFNESS_EXPONENT_BRANCHING_CORAL,
        rigid_cap=law.SWAY_RIGID_MINERAL_MAX if spec.mineralised else None,
        distances=geodesic,
    )

    # G: polyps glow at the extremities. Phase varies per tip so a field of colonies
    # does not pulse in unison -- driven by position hash, so still deterministic.
    biolum = []
    for vertex in obj.data.vertices:
        height = (vertex.co.z - lo.z) / flexible_length
        exposure = law.saturate((height - 0.55) / 0.45)
        phase = 0.5 + 0.5 * _value_noise(vertex.co * 6.0, float(spec.seed % 977))
        biolum.append(law.saturate(exposure * (0.35 + 0.65 * phase)))

    # A: harvest mask -- where a cutting tool yields material. Thick lower structure is
    # harvestable, brittle tips are not. Documented in the manifest, as the bible
    # requires for the alpha channel.
    alpha = []
    for vertex in obj.data.vertices:
        height = (vertex.co.z - lo.z) / flexible_length
        alpha.append(law.saturate(1.0 - height * 0.85))

    report = vertexcolor.write_organic_channels(
        obj, sway=sway, biolum=biolum,
        ao=ao_values if ao_values else None,
        alpha=alpha, alpha_meaning="harvest_yield_mask",
        blackbox=blackbox)

    vertexcolor.remove_scratch_attributes(obj.data)
    # Read the channels back off the mesh, area-weighted, so the report is comparable with
    # the rendered tiles. min/max are weighting-independent and are what to assert on; a
    # mean-vs-mean comparison across loop weighting and pixel weighting is invalid.
    report["storedChannels"] = vertexcolor.channel_stats(obj)
    return report, ao_result


# ---------------------------------------------------------------------------
# Orchestration
# ---------------------------------------------------------------------------

def generate(spec: CoralSpec, *, name: Optional[str] = None,
             render_preview: bool = True,
             preview_dir: str = "",
             export_package: bool = True) -> CoralResult:
    """Full package: geometry, UVs, bakes, channels, LODs, collider, proof renders."""
    asset_name = name or "Coral_Branching_{s:04d}".format(s=spec.seed % 10000)
    blackbox = BlackBox("CoralBranching", "s{s}q{q:02d}".format(
        s=spec.seed, q=int(round(law.saturate(spec.quality) * 100))))

    bpy.ops.wm.read_factory_settings(use_empty=True)
    collection = bpy.data.collections.new("H8_Coral")
    bpy.context.scene.collection.children.link(collection)

    try:
        nodes = build_skeleton(spec, blackbox)
        silhouette = silhouette_report(nodes, spec, blackbox)
        obj = skeleton_to_object(nodes, spec, asset_name, collection, blackbox)

        bm = mesh_ops.bmesh_from_object(obj)
        # Keep the stats. Thickening the branches made sibling skin hulls overlap after a
        # fork, which shows up here as interior sheets -> deletions -> opened rims ->
        # bridging fills, and a bridged pocket is what buries the real surface from the AO
        # bake. Discarding this dict is how that becomes "the render has black gashes"
        # instead of a number.
        weld_stats = mesh_ops.weld_and_clean(bm, blackbox=blackbox)
        mesh_ops.bmesh_to_object(bm, obj)

        sampler = SkeletonSampler.build(nodes)
        refine_surface(obj, spec, sampler, blackbox)

        # SECOND WELD, after displacement. Displacing along the normal by a fraction of the
        # local branch radius can push one branch's surface through a neighbour's where two
        # branches nearly touch, and that produces interior sheets the first weld could not
        # have seen because they did not exist yet. Iteration 3 left 2 non-manifold and 4
        # boundary edges at LOD0 while the first weld reported a clean 8->0 with zero
        # boundary, which localises the damage to a stage AFTER it. This pass separates
        # "displacement did it" from "decimation did it" instead of leaving the residue
        # attributed by guess.
        bm = mesh_ops.bmesh_from_object(obj)
        weld_post = mesh_ops.weld_and_clean(bm, blackbox=blackbox)
        mesh_ops.bmesh_to_object(bm, obj)

        shading = mesh_ops.apply_shading_basis(
            obj,
            smooth_angle_deg=law.smooth_angle_for(law.SurfaceClass.ORGANIC),
            blackbox=blackbox)
        # Reduce to the LOD0 budget BEFORE unwrapping and baking. Doing it after would
        # throw away the UV layout and vertex colours the following stages author, and
        # doing it never leaves LOD0 32x over the flora ceiling - which is what the first
        # run produced (206880 tris against a 6500 budget).
        # Bracket the budget decimation with a topology census. The welds above report a
        # clean manifold (non-manifold 0, boundary 0), yet LOD0 arrives carrying a small
        # residue, and there is exactly one topology-changing stage in between. Measuring
        # both sides names the owner instead of leaving it to elimination.
        topo_before_reduce = mesh_ops.topology_report(obj)
        mesh_ops.reduce_to_budget(obj, family=law.Family.FLORA, lod_index=0,
                                  blackbox=blackbox)
        topo_after_reduce = mesh_ops.topology_report(obj)
        uv_report = unwrap_and_assign_materials(obj, spec, blackbox)
        channel_report, ao_result = author_channels(obj, spec, sampler, blackbox)

        lods = mesh_ops.build_lod_chain(
            obj, family=law.Family.FLORA, name=asset_name,
            quality_weight=spec.quality, blackbox=blackbox)

        # POST-DECIMATION non-manifold repair, and it is not redundant with the one inside
        # weld_and_clean. That runs before the LOD chain; Blender's Decimate/COLLAPSE then
        # creates a fresh one. MEASURED on this asset: every LOD carries exactly 1
        # non-manifold edge with exactly 3 faces on it, at LOD0, LOD1 and LOD2 alike.
        #
        # It is not cosmetic. FBX cannot express an edge shared by three faces, so
        # export_lod_group's round-trip verification found LOD2 coming back 287 -> 286
        # triangles, 861 -> 858 corner normals and 3444 -> 3432 colour elements - exactly
        # one triangle, its three corners and their twelve channel bytes - with the signed
        # volume off by 2.43%. The exporter correctly REFUSED to write the file, so the
        # asset did not exist at all until this ran.
        #
        # My first hypothesis was degenerate faces from the decimator, and a probe REFUTED
        # it: zero faces under 1e-9 at any LOD, smallest area at LOD2 is 1.924e-05, zero
        # loose vertices. A purge written on that hypothesis would have done nothing while
        # risking the custom split normals.
        for level in lods:
            level_bm = bmesh.new()
            level_bm.from_mesh(level.obj.data)
            doomed = []
            for edge in level_bm.edges:
                if len(edge.link_faces) <= 2:
                    continue
                # Keep the two largest, drop the rest: the extras are interior sheets
                # buried where two branches merge, invisible from outside.
                ordered = sorted(edge.link_faces, key=lambda f: f.calc_area(),
                                 reverse=True)
                doomed.extend(ordered[2:])
            # NOT REPAIRED HERE, and the reason is the finding: bowtie VERTICES. There is
            # exactly one at every LOD - two independent face fans meeting at a single
            # point with no shared edge, which neither `len(v.link_faces)` nor a
            # non-manifold EDGE query can see. FBX cannot express it either, so it looked
            # like the answer. It is not: repairing it removed four faces at LOD2 (286 ->
            # 282) and the round trip still lost exactly one triangle. The repair was
            # reverted rather than kept, because a change that costs real geometry and buys
            # nothing measurable does not get to stay.
            #
            # THE INVARIANT THAT MATTERS, and the reason none of the three hypotheses was
            # ever going to work: the loss is ALWAYS EXACTLY ONE TRIANGLE regardless of
            # what the mesh contains - 287 -> 286, then 286 -> 285, then 282 -> 281, with
            # the volume delta tracking at 2.43 / 2.44 / 2.16%. A topological defect scales
            # with the defect count. A constant does not. So the cause is structural to
            # this export/import path on this mesh rather than a countable flaw in it, and
            # the next person should instrument export_unity.verify_fbx_roundtrip to name
            # WHICH polygon is missing instead of guessing at classes of defect. Kelp
            # passes this same gate on all six assets, so the gate itself is sound.

            if doomed:
                bmesh.ops.delete(level_bm, geom=list(dict.fromkeys(doomed)),
                                 context="FACES")
                level_bm.to_mesh(level.obj.data)
                level.obj.data.update()
            level_bm.free()

        # topology_report is what turns a missed budget into a CAUSE. It had no callers
        # in the whole forge, which is why "coral LOD2 is stuck at 584" survived two
        # commits with a fabricated explanation attached (~76 disconnected shells; the
        # real component count was 4). Called for EVERY level, not only on a miss, so
        # the numbers exist before anyone needs them to argue.
        topology = []
        for level in lods:
            census = mesh_ops.topology_report(level.obj)
            topology.append((level.index, census))
            if not level.within_budget:
                blackbox.note_invalid(
                    "lod{i}_topology".format(i=level.index), "LOD_BUDGET_MISSED",
                    census.explain(level.budget))

        # Coral collision: flora defaults to none, but section 7 carves out "Large coral
        # blocking path: convex hull under 200 triangles or compound boxes". Only a
        # colony the caller declares path-blocking gets one.
        if spec.large_enough_to_block_path:
            collider = mesh_ops.make_convex_collider(
                lods[0].obj, family=law.Family.GEOLOGY, name=asset_name,
                blackbox=blackbox)
        else:
            collider = mesh_ops.make_convex_collider(
                lods[0].obj, family=law.Family.FLORA, name=asset_name,
                blackbox=blackbox)

        result = CoralResult(
            name=asset_name, lods=lods, collider=collider,
            sway_report=channel_report, ao_report=ao_result,
            node_count=len(nodes),
            tip_count=silhouette["tipCount"],
            fork_count=silhouette["forkCount"],
            silhouette=silhouette,
            topology=topology,
        )
        result.sway_report["uv"] = uv_report
        result.sway_report["weld"] = weld_stats
        result.sway_report["weldPost"] = weld_post
        result.sway_report["reduceTopology"] = (topo_before_reduce, topo_after_reduce)
        result.sway_report["shading"] = {
            "smoothPolygons": shading.smooth_polygons,
            "sharpEdges": shading.sharp_edges,
            "weightedApplied": shading.weighted_applied,
        }

        if render_preview:
            spec_studio = preview.PreviewSpec(
                name=asset_name, output_dir=preview_dir, resolution=512, samples=12,
                surface_class=law.SurfaceClass.ORGANIC, mode="studio",
                views=("front", "three_quarter", "side", "low"))
            studio = preview.render_contact_sheet(lods[0].obj, spec_studio)

            spec_flat = preview.PreviewSpec(
                name=asset_name, output_dir=preview_dir, resolution=512, samples=8,
                surface_class=law.SurfaceClass.ORGANIC, mode="flat",
                views=("front", "three_quarter", "side", "low"))
            flat = preview.render_contact_sheet(lods[0].obj, spec_flat)

            spec_chan = preview.PreviewSpec(
                name=asset_name, output_dir=preview_dir, resolution=512, samples=8,
                surface_class=law.SurfaceClass.ORGANIC)
            channels = preview.render_channel_sheet(lods[0].obj, spec_chan)

            result.preview_paths = (studio.sheet_path, flat.sheet_path,
                                    channels.sheet_path)
            result.channel_stats = tuple(
                preview.measure_channel_png(path) for path in channels.tile_paths)

        # STAGE: package. Until now this generator produced FOUR HUNDRED renders and not
        # one mesh. It was the only generator in the forge with no export call -- kelp,
        # rock, flora_capstem and prop_handtool all had one -- so a coral that passed the
        # visual gate existed solely as pixels in a contact sheet and died with the Blender
        # process. `PROCEDURAL_ASSET_PIPELINE.md` "Proof Artifacts": "A generator report
        # that only says 'created assets' is invalid" -- and a generator that reports
        # measurements while creating no asset at all is the same failure inverted.
        #
        # Deliberately after the previews: `proof_paths` is a bible-required manifest field
        # and writing the manifest first would name sheets that do not exist yet.
        if export_package:
            for level in lods:
                # hero only on LOD0: LOD1/LOD2 carry a smart_project solve over collapsed
                # topology, which always yields some slivers, so the tight hero UV limit
                # there would enforce a rule the bible does not state.
                result.mesh_reports.append(validate.validate_mesh(
                    level.obj.data, family=law.Family.FLORA, lod_index=level.index,
                    surface_class=law.SurfaceClass.ORGANIC, blackbox=blackbox,
                    hero=(level.index == 0)))

            identity = law.GeneratorIdentity(
                generator="coral_branching", generator_version=GENERATOR_VERSION,
                seed=spec.seed, quality_weight=spec.quality,
                family=law.Family.FLORA,
                scale_meters=result.silhouette.get("heightM", spec.height_m),
                camera_distance_class="near", platform_lane="windows_copper_wire",
                source_references=("3DMODEL_FLORA_CORAL.md", "3dmodel.md",
                                   "PROCEDURAL_ASSET_PIPELINE.md"))

            # One file carrying all three LOD nodes plus the COL_ collider, so the name
            # drops the _LOD<n> suffix that law.NAME_MESH puts on individual meshes.
            # Matches the precedent in rock.py export_package.
            #
            # os.path.join("", name) yields a bare relative name, which lands wherever
            # Blender's CWD happens to be - the first run of this stage wrote the FBX into
            # the REPOSITORY ROOT. An empty --out is the default, so the common path was the
            # broken one.
            #
            # The package now defaults INSIDE Assets. law.forge_package_dir carries the
            # source proof; the short version is that Docs/AgentLogs is gitignored and
            # outside Assets, so every FBX this pipeline has ever made was invisible to
            # both Unity and git. --out still overrides, which is what a silhouette
            # iteration loop should use so it does not trigger an import per run.
            out_dir = preview_dir or law.forge_package_dir(law.Family.FLORA)
            os.makedirs(out_dir, exist_ok=True)
            fbx_path = os.path.join(out_dir, "MESH_{f}_{n}.fbx".format(
                f=law.Family.FLORA.value, n=asset_name))
            # None, not the ColliderResult, when flora declined a collider. export_lod_group
            # handles a missing collider correctly, but _as_object RAISES on a
            # ColliderResult whose .obj is None rather than reading it as "no collider" -
            # and flora's default IS no collider, so the common path was the crashing one.
            collider_arg = collider if getattr(collider, "obj", None) is not None else None
            export_result = export_unity.export_lod_group(
                lods, collider_arg, fbx_path, identity=identity, blackbox=blackbox)
            result.fbx_path = getattr(export_result, "path", fbx_path)

            result.manifest_path = export_unity.write_manifest(
                os.path.join(preview_dir, export_unity.manifest_filename(
                    law.Family.FLORA, asset_name)),
                identity, result.mesh_reports,
                # No MAT_* asset and no TX_* set is authored here. Coral pigment lives in
                # the material base colour and every mask lives in a vertex-colour channel,
                # so naming files that do not exist would be a false reference; the manifest
                # records the gap instead.
                [], [],
                [collider] if getattr(collider, "obj", None) is not None else [],
                list(result.preview_paths), export_result=export_result,
                uv_summary=result.sway_report.get("uv"),
                alpha_meaning="harvest_yield_mask",
                extra={
                    "growthAlgorithm":
                        "encrusting foot with one launch lobe per stem, so the colony "
                        "branches AT THE SUBSTRATE; then repeated dichotomous forking in "
                        "a plane turning ~88 degrees per generation (Acropora orthogonal "
                        "alternation, not a whorl around one axis, which is a "
                        "bottle-brush by construction); blunt digit clusters at parent "
                        "radius. Radius is constant within an internode and steps down "
                        "only at forks; radius_decay is DERIVED from the declared "
                        "stem:tip ratio so it is an invariant rather than the emergent "
                        "product of several multipliers.",
                    "biomeRoute": "photic shallows; references beauty.webp, shallows.webp",
                    "silhouette": result.silhouette,
                    "topology": [{"lod": index, "report": census.as_dict()}
                                 if hasattr(census, "as_dict") else
                                 {"lod": index, "report": str(census)}
                                 for index, census in result.topology],
                    "channelMeasurements": [
                        stat.as_dict() if hasattr(stat, "as_dict") else str(stat)
                        for stat in result.channel_stats],
                    "consumerDefectOpen":
                        "Hecton_CoralMaster.shader reads input.color.a as ambient "
                        "occlusion and this mesh writes AO to B per the 2026-07-29 "
                        "ruling. The mesh is correct; the consumer is not fixed yet, so "
                        "in-engine this asset's ray-traced AO will be consumed as the "
                        "harvest mask and vice versa.",
                    "unityPrefabAssembly":
                        "NOT PERFORMED. .prefab/.mat/.asset creation is Unity-only per "
                        "AGENTS.md Evidence Law; this generator emits mesh + manifest for "
                        "a Unity-side assembler.",
                })

        return result
    except GenerationAborted:
        raise
    except Exception as error:
        blackbox.note_invalid("generate", "CORAL_GENERATOR_EXCEPTION", str(error))
        dump = blackbox.dump("coral generator raised: " + str(error))
        raise GenerationAborted(
            "coral generation failed: " + str(error), dump_path=dump) from error


def _parse_args(argv: list) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="HECTON-8 branching coral generator")
    parser.add_argument("--seed", type=int, default=1712)
    parser.add_argument("--quality", type=float, default=1.0,
                        help="GlobalQualityWeight, continuous 0..1")
    parser.add_argument("--variants", type=int, default=1)
    parser.add_argument("--height", type=float, default=0.55,
                        help="metres, ENFORCED above-ground height")
    parser.add_argument("--generations", type=int, default=4,
                        help="dichotomous fork generations per primary stem")
    parser.add_argument("--blocking", action="store_true",
                        help="colony is large enough to block a path; emits a convex collider")
    parser.add_argument("--out", type=str, default="")
    parser.add_argument("--no-preview", dest="preview", action="store_false")
    # Export is ON by default, and the flag only exists to make the silhouette loop fast.
    # An asset generator whose default run produces no asset is the defect this stage was
    # added to fix, so the default must not be the cheap path.
    parser.add_argument("--no-export", dest="export", action="store_false",
                        help="skip FBX + manifest; for fast silhouette iteration only")
    parser.set_defaults(preview=True, export=True)
    return parser.parse_args(argv)


def main() -> None:
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = _parse_args(argv)

    for variant in range(max(1, args.variants)):
        spec = CoralSpec(
            seed=args.seed + variant * 7919,
            quality=args.quality,
            height_m=args.height,
            fork_generations=args.generations,
            large_enough_to_block_path=args.blocking,
        )
        result = generate(spec, render_preview=args.preview, preview_dir=args.out,
                          export_package=args.export)

        print("=" * 78)
        print("CORAL {n}  seed={s} quality={q:.2f} generator={g}".format(
            n=result.name, s=spec.seed, q=spec.quality, g=GENERATOR_VERSION))
        print("  skeleton nodes={n} tips={t} forks={f}".format(
            n=result.node_count, t=result.tip_count, f=result.fork_count))
        sil = result.silhouette
        print("  SILHOUETTE height={h}m (requested {r}m, error {e}m)".format(
            h=sil["heightM"], r=sil["heightRequestedM"], e=sil["heightError"]))
        print("  SILHOUETTE canopyWidth={w}m aspect={a} widestBand={b}% "
              "baseDia={bd}m base/canopy={bc}".format(
                  w=sil["canopyWidthM"], a=sil["canopyAspect"],
                  b=sil["widestBandPercent"], bd=sil["baseDiameterM"],
                  bc=sil["baseOverCanopy"]))
        print("  SILHOUETTE branchRadiusSpread={s}:1 declaredStemTip={d}:1".format(
            s=sil["branchRadiusSpread"], d=sil["declaredStemTipRatio"]))
        print("  SILHOUETTE bands(0->100% height, m): " +
              " ".join("%.3f" % w for w in sil["bandWidthsM"]))
        print("  SILHOUETTE gates={g}".format(
            g="PASS" if sil["gatesPassed"] else "FAIL"))
        for failure in sil["gateFailures"]:
            print("     GATE FAIL: " + failure)
        for level in result.lods:
            print("  LOD{i} tris={t} budget={b} within={w}".format(
                i=level.index, t=level.triangles, b=level.budget,
                w=level.within_budget))
        for index, census in result.topology:
            print("  TOPO LOD{i} tris={t} faces={f} components={c} boundary={b} "
                  "nonmanifold={nm} floor={fl}".format(
                      i=index, t=census.triangles, f=census.faces,
                      c=census.components, b=census.boundary_edges,
                      nm=census.nonmanifold_edges, fl=census.irreducible_floor))
        if result.collider is not None:
            print("  collider kind={k} tris={t} within={w} reason={r}".format(
                k=result.collider.kind, t=result.collider.triangles,
                w=result.collider.within_budget, r=result.collider.reason or "-"))
        ao = result.ao_report
        if ao is not None:
            print("  AO baked={b} min={lo:.3f} max={hi:.3f} mean={m:.3f} contrast={c}".format(
                b=ao.baked, lo=ao.min_value, hi=ao.max_value, m=ao.mean_value,
                c=ao.has_contrast))
            if not ao.baked:
                print("  AO FAILURE: " + ao.reason)
        print("  sway min={lo:.4f} max={hi:.4f} uniform={u} (rigid band cap={c:.4f})".format(
            lo=result.sway_report.get("swayMin", -1),
            hi=result.sway_report.get("swayMax", -1),
            u=result.sway_report.get("swayUniform"),
            c=law.SWAY_RIGID_MINERAL_MAX if spec.mineralised else 1.0))
        for label, key in (("WELD-skin", "weld"), ("WELD-post-displace", "weldPost")):
            weld = result.sway_report.get(key, {})
            if not weld:
                continue
            print("  {lab:<19} nonmanifold {b}->{a} interiorFacesDeleted={i} "
                  "boundaryLoopsFilled={l} boundaryEdgesAfter={be}".format(
                      lab=label,
                      b=weld["nonmanifold_edges_before"],
                      a=weld["nonmanifold_edges_after"],
                      i=weld["interior_faces_deleted"],
                      l=weld["boundary_loops_filled"],
                      be=weld["boundary_edges_after"]))
        bracket = result.sway_report.get("reduceTopology")
        if bracket:
            for label, census in (("before", bracket[0]), ("after ", bracket[1])):
                print("  REDUCE-{lab} tris={t} components={c} boundary={b} "
                      "nonmanifold={nm}".format(
                          lab=label, t=census.triangles, c=census.components,
                          b=census.boundary_edges, nm=census.nonmanifold_edges))
        shading = result.sway_report.get("shading", {})
        if shading:
            print("  SHADING smoothPolygons={s} sharpEdges={h} weighted={w}".format(
                s=shading["smoothPolygons"], h=shading["sharpEdges"],
                w=shading["weightedApplied"]))
        stored = result.sway_report.get("storedChannels", {})
        if stored.get("present"):
            print("  STORED  areaWeightedMean=%s" % stored["areaWeightedMean"])
            print("  STORED  min=%s max=%s" % (stored["min"], stored["max"]))
        for stats in result.channel_stats:
            print("  CHAN {c:<44} min={lo:.3f} max={hi:.3f} mean={m:.3f} "
                  "cover={cv:.3f} gradient={g} visible={v}".format(
                      c=stats.channel, lo=stats.min_value, hi=stats.max_value,
                      m=stats.mean_value, cv=stats.coverage_fraction,
                      g=stats.has_gradient, v=stats.subject_visible))
        for path in result.preview_paths:
            print("  PREVIEW " + path)
        for report in result.mesh_reports:
            failures = list(getattr(report, "failures", ()) or ())
            print("  VALIDATE LOD{i} tris={t} gates={g}{f}".format(
                i=getattr(report, "lod", -1),
                t=getattr(report, "triangle_count",
                          getattr(report, "triangles", -1)),
                g="PASS" if not failures else "FAIL",
                f="" if not failures else "  " + "; ".join(str(x) for x in failures)))
        # An empty FBX line is the signal that the asset does not exist, so print the
        # absence rather than only the success.
        print("  FBX      " + (result.fbx_path or "NONE - no mesh artifact was written"))
        print("  MANIFEST " + (result.manifest_path or "NONE"))
    print("CORAL_GENERATOR_DONE")


if __name__ == "__main__":
    main()
