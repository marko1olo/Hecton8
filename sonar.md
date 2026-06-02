# HECTON-8 Sonar, Scanner, Navigation, And Cartography Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: active sonar, passive hydrophone, scanner confidence, acoustic radar, map/cartography, fog-of-war, route plotting, signal trust, sensor UI, and navigation proof gates.

## Prime Law

Sonar is partial truth under pressure. It is not an omniscient minimap.

The player should earn certainty through risk, power, noise, time, line of acoustic sight, and environmental interpretation. HECTON-8 rejects clean radar, magic objective arrows, always-accurate pips, scanner spam, and maps that erase fear. Navigation tools must give enough information to decide, not enough information to feel safe.

## Truth Ownership

Sonar/scanner/navigation owns sensed snapshots, confidence, bearing, stale state, map reveal state, acoustic ping events, and sensor UI payloads. It does not own world placement, AI truth, creature cognition, audio mix, route design, or objective truth.

World, AI, audio, tools, UI, and persistence publish or consume bounded sensor facts through typed routes. Sensor systems must not discover gameplay truth by scene search or use sensors as a backdoor to reveal hidden states.

## Sensor Contract

Every sensor output must define:

- source: active ping, passive hydrophone, scanner beam, black-box replay, map cache, beacon, route marker;
- confidence;
- timestamp/staleness;
- range;
- occlusion or obstruction model;
- energy/noise cost;
- who can hear or react to it;
- UI/audio representation;
- save/reveal behavior if persistent.

If a readout cannot be stale or wrong, it must be a debug tool, not diegetic player equipment.

## Active Sonar

Active sonar creates risk:

- ping emits acoustic energy;
- nearby fauna/AI may react;
- returns classify broad material/silhouette before exact identity;
- echo delay, occlusion, and attenuation matter;
- high-confidence ping costs power, noise, time, or exposure.

Active sonar must never become a free wallhack. If it reveals a creature, wreck, route, or resource, the source and confidence must be visible.

## Passive Hydrophone

Passive listening is safer but weaker:

- bearing before range;
- energy before identity;
- stale tracks;
- false or ambiguous sources;
- occlusion and masking by machinery, current, and pressure.

Passive data should shape dread: the player hears route risk before seeing it.

## Cartography And Fog Of War

Maps are memory, not GPS certainty.

Rules:

- reveal is tied to sensor proof, proximity, beacon, or recovered data;
- fog-of-war stores compact bit/voxel/sector state;
- stale data must be visually distinct from current data;
- route marks use player intent or system evidence, not automatic quest arrows;
- map UI must remain readable at low resolution;
- no runtime string/object lookup for map facts.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` may scale ping ray count, map visual richness, hydrophone sector resolution, scanner decal richness, echo visualization, optional diagnostics, and noncritical update cadence. It must not change sensed truth ownership, saved reveal state, creature reaction rules, or objective authority.

Compact keeps bearing, confidence, stale state, strong audio cues, and low-cost map sectors. Middle adds richer silhouettes and scanner feedback. High adds better echo classification and map material. Ultra adds cinematic sensor visualization without becoming omniscient.

## Production Packet

Any sonar, scanner, navigation, or cartography implementation must declare:

- sensor family and owner system;
- active/passive mode list;
- source positions and update cadence;
- confidence, staleness, range, occlusion, and false-positive rules;
- active ping cost and creature/system reaction route;
- map reveal persistence schema if map state is saved;
- UI/audio/haptic presentation route;
- Compact and High proof captures;
- profiler/GC proof when runtime sensor code changes.

The packet must prove that the player is reading imperfect instrument data, not receiving an omniscient minimap.

## Proof Artifacts

Sonar/navigation work must provide:

- sensor source list;
- confidence/staleness rules;
- active ping cost and reaction route;
- map reveal persistence proof if persistent;
- compact sensor UI screenshot;
- audio/UI owner route;
- no scene-search/hot allocation scan if implemented;
- profiler/GC proof for runtime sensor changes.

## Rejection Gates

Reject:

- clean omniscient minimap;
- sonar that reveals exact hidden truth with no cost;
- scanner outputs with no confidence or stale state;
- map markers with no evidence source;
- active ping with no world/creature consequence;
- sensor UI that is decorative rather than actionable;
- reports that claim navigation proof from static text alone.

## Acceptance Sentence

Sonar and navigation are accepted only when sensor facts are partial, owned, stale/confidence-aware, risk-bearing, readable on compact tier, and proven not to reveal world truth for free.
