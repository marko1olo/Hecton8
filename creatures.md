# HECTON-8 Creatures Bible

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Scope: creature behavior, encounter design, ecology, sensory inputs, animation presentation, telegraphing, AI taste, and fauna-system integration.

## 0. Prime Creature Law

A creature is a pressure on behavior, not a model with patrol points.

Every creature must change at least one player decision:

- light use;
- sound discipline;
- route choice;
- oxygen timing;
- scan trust;
- salvage greed;
- repair urgency;
- hiding, retreat, or baiting.

If a creature can be removed without changing player behavior, it is decoration.

## 1. Sensory Contract

Creatures must declare what they sense:

- sound;
- light;
- electrical power;
- blood/chemistry;
- motion;
- hull stress;
- territory intrusion;
- sonar ping.

Detection must be legible through audio, UI, animation, route state, or environmental response. Random aggro is rejected.

## 2. Encounter Shape

Good encounters have phases:

1. Absence with evidence.
2. Audio or sensor anomaly.
3. Partial silhouette or route consequence.
4. Player decision.
5. Close threat or escape.
6. Evidence after the event.

Full reveal first is weak. Jump scare without system context is rejected.

## 3. Ecology

Creatures should belong to the world:

- feeding grounds;
- avoidance zones;
- nests;
- migration routes;
- light/noise reactions;
- corpse/scar evidence;
- relationship to vents, wrecks, coral, current, or Atlas systems.

Zoo placement is rejected.

## 4. AI Runtime Discipline

Behavior implementation must obey:

- no scene search in hot paths;
- no managed allocation in behavior ticks;
- sensory data through owned snapshots or typed signal lanes;
- continuous `GlobalQualityWeight` for perception cadence, animation richness, and presentation detail;
- low-tier math LOD that preserves behavior truth;
- black-box telemetry for last known state and NaN/fault conditions.

Do not solve presentation with expensive per-creature physics unless gameplay truth requires it.

## 5. Animation And Presentation

Creature motion must communicate:

- intent;
- mass;
- water resistance;
- injury;
- attention;
- feeding/territorial behavior;
- sensory reaction.

Use VAT, baked masks, IK only where needed, spline fakes, shader deformation, and pooled VFX before expensive runtime simulation. Animation must align with mesh topology and vertex color semantics from `3dmodel.md`.

## 6. Creature Audio

Audio is often the first reveal:

- distant pressure call;
- hull scrape;
- water displacement;
- sonar distortion;
- breath-like low frequency;
- silence before strike;
- territory pulse.

Generic roars are rejected.

## 7. Creature QA Gates

Reject if:

- creature does not change player behavior;
- aggro has no sensory cause;
- encounter starts with full visual reveal;
- behavior ignores oxygen, light, sound, route, or pressure;
- animation floats without mass;
- audio is generic;
- AI tick allocates;
- no black-box state exists for fault review;
- model looks good but gameplay role is empty.
