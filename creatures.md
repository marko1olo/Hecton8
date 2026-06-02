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

## 8. Truth Ownership

Creature truth is owned by AI, animation, physics, audio, and generated asset domains separately:

- `ai.md` owns cognition, Director pressure, stimulus memory, navigation intent, and flocking cadence.
- `animation.md` owns motion presentation, IK/VAT routes, and silhouette-preserving animation LOD.
- `physics.md` owns collision, hit response, force packets, and contact truth.
- `audio.md` owns sound identity, mix priority, and sonar/audio telegraphs.
- `3dmodel.md` and fauna-specific rules own mesh topology, materials, LODs, and collider proxies.

Creature scripts must not become a monolith that directly owns all of these lanes.

## 9. GlobalQualityWeight Scaling

Compact keeps the same creature role with fewer active entities, lower cognition cadence, simpler animation, stronger audio cues, simpler VFX, and shorter visibility range. Middle restores normal local cognition and presentation. High adds richer memory, secondary motion, and material/lighting detail. Ultra adds encounter density and sensory overkill, not omniscience or unavoidable attacks.

## 10. Proof Artifacts

A creature implementation must provide:

- role statement: what player decision changes;
- sensory contract and stimulus causes;
- Director/token interaction if encounter-managed;
- AI tick cadence and active count;
- animation/mesh proof screenshot;
- audio cue list;
- collision proxy proof;
- low-tier readability capture;
- black-box state fields.

## 11. Acceptance Sentence

A creature is accepted only when it has a gameplay role, sensory cause, readable intent, authored body quality, bounded AI cost, scalable presentation, and evidence that the player can understand why the encounter happened.
