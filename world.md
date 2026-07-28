# HECTON-8 World Bible

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Scope: world composition, biome identity, route design, habitat placement, wrecks, caves, geology, ecology scatter, landmarks, navigation, and environmental storytelling.

## 0. Prime World Law

The world is not random underwater decoration. It is flooded geography, failed industry, salvage history, and pressure architecture.

Every location must provide:

- a route decision;
- a pressure or visibility condition;
- a machine, geological, biological, or salvage reason to exist;
- a scale cue;
- an evidence cue.

Pretty emptiness is rejected. Random scatter is rejected. Biomes that differ only by color are rejected.

## 0.1 Surface And Photic Zone Law

The surface, shoreline, exposed rock, ocean skin, sky, Aegir view, moon silhouettes, and photic shallows are the beauty counterweight to the abyss. They must be bright, readable, and materially rich, not gloomy by default.

Darkness starts with depth, caves, storm occlusion, industrial interiors, and temporary route events. Surface world art must show terrain form, water clarity, wet rock, sediment, cloud motion, celestial scale, and approach routes. If a surface area needs tension, use weather, route cost, radiation timing, wave state, sound, and visible engineering risk instead of crushing the image to black.

Compact keeps clean silhouettes, ocean color, wet material breakup, and authored landmarks. Middle adds denser shoreline detail and richer water response. High and Ultra spend saved cycles on terrain microdetail, cloud depth, reflections, foam, caustic hints, and celestial atmosphere.

Depth/light lock:

- 0-100 m: mostly bright, colorful, beautiful, and readable.
- Shallow caves can be dim or dark, but open photic water is not a noir zone.
- 200-400 m: twilight and increasing tension.
- 400-500 m and below: true darkness/murk becomes normal, with route structure preserved.

Surface and shallow zones may be colorful and alien, including coral-like forms and unusual biota. They should also carry technogenic history in places: colony remnants, cables, route hardware, pipes, wreck fragments, salvage cuts, stations, or other signs that people and machines were here.

## 1. World Layers

A strong location has layered logic:

- Geological base: trench, shelf, cliff, vent, cave, sediment basin, fracture, drowned terrain.
- Industrial intrusion: habitat, cable, pump, relay, dock, wreck, pipe, scaffold, hatch, salvage cut.
- Biological response: coral, kelp, fauna route, biofilm, biolum field, carcass, nest, avoidance zone.
- Player route: entry, landmark, risk corridor, return path, shortcut, lockout, extraction point.
- Evidence: scars, bodies, logs, broken machinery, black-box fragment, missing wall, cut panel.

If only one layer exists, the area will feel like a test map.

## 2. Biome Identity

Each biome must define:

- dominant silhouette;
- fog and lighting behavior;
- navigation landmarks;
- acoustic signature;
- hazards and safe pockets;
- material palette;
- resource/evidence logic;
- creature behavior pressure;
- low-tier readability method.

Do not define a biome by tint alone. "Blue cave", "green kelp", or "red danger area" is not enough.

## 3. Route Design

Routes must support tension:

- visible goal with uncertain path;
- return path cost;
- at least one readable landmark;
- at least one risk modifier;
- optional shortcut with tradeoff;
- failure consequences;
- low-end silhouette readability.

Avoid straight corridors with decorative props. Avoid open fields with no route grammar. Avoid maze design that relies on confusion instead of pressure.

## 4. Habitat And Wreck Placement

Habitats and wrecks must obey engineering:

- pressure doors face route logic;
- pumps and conduits connect plausible systems;
- docking geometry has approach space;
- broken modules have failure direction;
- salvage cuts expose structure;
- interiors show how the machine survived or failed;
- exterior silhouettes remain readable in fog.

Do not place modules as random boxes. A module must explain why it is there, what it did, how it failed, and what the player can do with it.

## 5. Geology And Terrain

Geology must guide:

- strata indicate direction;
- cliffs frame routes;
- caves compress risk;
- vents create heat, sound, mineral, fauna, and light logic;
- sediment shows current and disturbance;
- rock shapes support cover, landmarks, and collision proxies.

Perlin hills and smoothed boulders are rejected. Use fracture planes, undercuts, shelves, debris fans, vent chimneys, cave ribs, and waterline/sediment cues.

Current generation contract:

- Macro terrain starts from deterministic `WorldMacroGeologyFields` evaluation over the authored world extent and chunk grid.
- Terrain materials, surface masks, and biome/substrate signals are derived through `WorldTerrainDetailContracts` and runtime terrain jobs, not by hand-waving a biome tint over noise.
- MapMagic is a bridge, bake, provider, and tile-application route. The current world source of truth is not an old MapMagic graph standing alone.
- Generated preview manifests under `Docs/GeneratedAssets/Terrain/MacroGeology` are static artifacts for inspection and queueing. Source code and runtime provider identity remain authoritative.
- World save/load validates terrain identity through seed, macro artifact version, chunk range/hash, provider flags, and water calibration. A route that cannot explain its terrain identity is not production-ready.

## 6. Ecology Scatter

Scatter is authored density, not random decoration:

- flora follows current, light, substrate, depth, and hazard;
- coral clusters have parent/child growth logic;
- fauna zones respond to food, shelter, sound, light, and territory;
- debris follows gravity, current, collapse, and salvage trails;
- empty zones exist only to create dread, route clarity, or performance relief.

Scatter must be instance-friendly, LOD-aware, and tied to `3dmodel.md` asset standards.

## 7. Landmarks

Every major area needs at least one landmark:

- silhouette landmark;
- sound landmark;
- light landmark;
- route landmark;
- industrial landmark;
- geological landmark;
- evidence landmark.

Landmarks must survive low-tier graphics. If the player needs ultra fog or dense assets to navigate, the area fails.

## 8. World Generation Order

1. Define route and player decision.
2. Define geological carrier.
3. Place industrial or biological reason.
4. Place landmark and return path.
5. Add hazards and safe pockets.
6. Add evidence and salvage.
7. Add scatter density by substrate/current/depth.
8. Bake LOD/HLOD, occlusion, colliders, probes, and streaming cells.
9. Capture low-tier and normal screenshots.
10. Reject if the area reads as random scatter.

## 9. World QA Gates

Reject if:

- area has no route decision;
- biome differs only by color;
- no low-tier landmark exists;
- scatter is random or evenly spaced;
- modules ignore engineering;
- terrain is smoothed noise;
- no evidence cue exists;
- player cannot describe why the place exists;
- world composition depends on runtime generation in gameplay.
- the first product-facing route is only a narrow resource proof and not a beautiful semi-open shallow route with unease, oxygen pressure, tool use, hazard, and save/load proof.

## 9.1 2026-06-05 Static Source Anchors

Evidence class: STATIC_SOURCE only. Compile, Unity import, Play Mode, profiler, GC, visual captures, save/load, and player-build proof remain PENDING VERIFICATION.

| Runtime | Owner / boundary | Static route | GlobalQualityWeight consequence | Missing proof |
|---|---|---|---|---|
| `Assets/_Project/Scripts/World/WorldMacroGeologyFields.cs` plus `WorldTerrainDetailContracts.cs` | `Hecton8.World`; deterministic macro geology, material class, terrain mask, and chunk identity contract. It owns terrain-world source data, not water truth, Unity scene search, runtime streaming, or voxel persistence. | Macro fields feed terrain jobs, provider identity, preview manifests, save/load terrain validation, scatter eligibility, and world route composition. MapMagic reads/bridges this route where active; it is not the standalone canonical graph. | Quality tiers may change density, resolution, cadence, HLOD, and presentation richness. They must not change route grammar, macro field identity, material semantics, save identity, biome ownership, or evidence placement. | No Unity import, terrain capture, player traversal, artifact load, save/load mismatch, provider replacement, duplicate owner, profiler, or player-build proof was provided by this static audit. |
| `Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs` | `Hecton8.World.SeedShipAnomaly`, `SystemID.EndgameAnomaly`; owns seed-ship anomaly field presentation/control signals, not generic world generation, save authority, AI cognition, or player UI ownership. | Registers cold, updatable, slow, and late-frame tick routes. Owns DataVault buffers `ShinobuSeedShipAnomaly*`, mock HUD/leviathan/AUP/thermo buffers, and a 300-entry telemetry ring with dump target `Docs/AgentLogs/Dump_SEED_SHIP_ANOMALY.bin`. Publishes/consumes typed lanes including `RadarJamSignal`, `CoreHackedSignal`, `MockHudSignal`, `MockAupRebaseSignal`, `AnomalyProximitySignal`, `SystemGlitchSignal`, `TelemetryAnomalySignal`, `RadiationSourceSignal`, and `RadiationDoseSignal`. | Reads borrowed graphics scalability state or fallback `global_quality_weight`; scales entity budget through anomaly math. It must not change anomaly authority, save identity, or signal DTO layout. | No scene wiring, runtime field capture, profiler, GCMonitor, telemetry dump artifact, radiation/radar gameplay proof, or save/load proof was provided by this static audit. |
| `Assets/_Project/Scripts/World/SargassumCutManager.cs` | `Hecton8.World`, `SystemID.WorldSargassum`; owns global sargassum cut-mask service and terrain damage-volume presentation. It does not own flora mesh generation, terrain authority, inventory, or tool truth. | Registers `ITickable`, `ISlowTickable`, `ILateFrameTickable`, hot-swap listener, and `GlobalRegistry.SargassumCut`. Owns DataVault stamp command buffers `SargassumCutStampCommands` and `SargassumCutDamageVolumeStampCommands`. Uses ping-pong `RenderTexture` masks, double-buffered `GraphicsBuffer` stamp uploads, compute shaders `Hecton_SargassumCutMask.compute` and `Hecton_TerrainDamageVolume.compute`, and shader globals `_SargassumCutMaskRT` and `_HectonDamageVolumeTex`. Registers recent cut heat as transient world spatial hash events. | Reads `HomeostasisBrain.GlobalQualityWeight`; scales cut-mask resolution from 512 up to authored max and damage-volume resolution/depth with hysteresis. Cut truth, tool success, and terrain authority must not depend on high-tier texture resolution. | No compute-support matrix, visual cut-mask capture, Frame Debugger/GPU profiler, GCMonitor, Unity import, shader compatibility, or runtime cut interaction proof was provided by this static audit. |

## 10. Truth Ownership

World composition owns placement logic, route grammar, biome identity, landmarks, and environmental evidence. It does not own runtime streaming, voxel rebuilds, generated mesh authoring, AI cognition, or rendering passes.

World work must route to:

- `streaming.md` for chunk residency and HLOD.
- `voxels.md` for SDF caves, carving, and seams.
- `3dmodel.md` for generated asset quality.
- `rendering.md` and `presentation.md` for fog, lighting, and capture quality.
- `ai.md` and `creatures.md` for ecology pressure.

## 11. GlobalQualityWeight Scaling

Compact keeps route silhouettes, landmarks, low-cost fog, simple scatter, HLOD, proxy collision, and readable evidence. Middle adds density and local biome detail. High adds richer lighting, material response, and ecology. Ultra adds sensory density and longer sightline richness without making navigation depend on expensive effects.

## First-20 Route Hook

- First-20 moment: boot to world load, first exit, swim, resource, tool, hazard, and save/load through one beautiful semi-open shallow route.
- Route blocker removed: world composition cannot collapse into a narrow Copper Wire proof, random scatter field, or unproven beauty shot without route decisions and state restoration.
- Proof class: screenshot, Play Mode/player capture for the full route, Profiler/GCMonitor for route runtime, save/load artifact for restored route state, and static-only route sketch for authoring-only changes.

## 12. Proof Artifacts

World work must provide:

- route sketch or description;
- biome identity statement;
- landmark list;
- evidence cue list;
- streaming/HLOD note;
- collider/navigation note;
- normal-tier screenshot;
- compact-tier screenshot;
- rejection check for random scatter.

## 13. Acceptance Sentence

A world area is accepted only when it has route decisions, physical reason, readable landmarks, believable geology/industry/ecology, evidence, streaming discipline, and compact-tier readability.
