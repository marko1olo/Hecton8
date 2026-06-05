# HECTON-8 Sonar, Scanner, Navigation, And Cartography Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: active sonar, passive hydrophone, scanner confidence, acoustic radar, map/cartography, fog-of-war, route plotting, signal trust, sensor UI, and navigation proof gates.

## Prime Law

Sonar is partial truth under pressure. It is not an omniscient minimap.

The player should earn certainty through risk, power, noise, time, line of acoustic sight, and environmental interpretation. HECTON-8 rejects clean radar, magic objective arrows, always-accurate pips, scanner spam, and maps that erase fear. Navigation tools must give enough information to decide, not enough information to feel safe.

Usability lock: map and sonar may be closer to Subnautica-level usability than to a hostile unreadable instrument sim. They should help the player explore, plan, and enjoy the world. They still must not become an omniscient debug map with perfect hidden truth, exact creature state, or free objective certainty.

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

## First-20 Route Hook

- First-20 moment: swim, resource, tool, and hazard response must prove one scanner/sonar/navigation cue that helps route finding or target confidence without revealing hidden truth for free.
- Route blocker removed: scanner, sonar, compass, or beacon UI cannot be decorative, omniscient, unsaved when persistent, or detached from audio/tool/UI owners.
- Proof class: screenshot, Play Mode/player capture for scan or ping use, Profiler/GCMonitor for sensor runtime, Frame Debugger for sensor visuals when changed, and save/load artifact for map reveal or recovered signal state.

## Live Source Anchors - 2026-06-05

Evidence class: STATIC_SOURCE / STATIC_DOC only. These anchors close stable-doc traceability gaps only; they do not prove Unity import, Play Mode, profiler, GC, Frame Debugger, save/load, or player-build readiness.

- `Assets/_Project/Scripts/Visor/SpectrumSystem.cs` is the current visor spectrum/sonar presentation owner. Static source shows `SpectrumSystem : MonoBehaviour, ILateFrameTickable, IAcousticEchoEventListener, IPingReturnSignalListener, IGlobalRegistryHotSwapListener`; `SpectrumEvents` owns bounded fixed queues for mode changes, sonar pulse radius, active sonar ping, `SpatialSonarSnapshot`, `AcousticEchoEvent`, and `PingReturnSignal`. The source inputs are active acoustic pings through `SignalBus<AcousticPingSignal>`, acoustic echo callbacks, ping-return signals, spatial sonar snapshots, player AUP/runtime origin, spatial audio emitter samples for passive radar, and DataVault-backed active-sonar geo telemetry. This is sensed/presentation truth, not world/AI/objective truth.
- `SpectrumSystem` writes a 300-row active-sonar geo telemetry ring under `SystemID.UI` and dumps `Dump_ACTIVE_SONAR_ILLUMINATION.bin` on non-finite active sonar geo state. It uses `HomeostasisBrain.GlobalQualityWeight` for active-sonar geo quality encoding. Missing proof: current Unity import, active sonar UI screenshot, compact readability capture, GC/profiler run, Frame Debugger/RenderGraph proof for sonar visuals, and duplicate-lane/static compile proof.
- `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs` is the diegetic compass navigation anchor. It consumes `AnomalyProximitySignal`, `CompassCalibratedSignal`, `SurvivalVitalsChangedSignal`, `SystemHealthSignal`, and `AupShiftSignal`; owns compass drift/presentation DataVault lanes and a 300-row compass black-box ring; and presents cardinal text through fixed chars. It must be documented as partial instrument bearing, not omniscient route/objective truth.
- `Assets/_Project/Scripts/AtlasSignal/SignalBeacon.cs` is a beacon/acoustic breadcrumb source. It solves triangulated strength from three AUP points against the player AUP, cave-interference noise, and authored range; publishes dominant cached telemetry through `SignalBeaconRegistry`; and emits `PhysicsEventPayload` acoustic pings for audio/sonar consumers. It may recover encrypted audio-log fragments through `IAudioLogRuntime`, but this source does not prove quest progression, save persistence, or objective routing. Missing proof: save/load of recovered bits, quest-state bridge, PDA/HUD display proof, runtime audio/sonar reaction proof, and compact sensor UI proof.

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
