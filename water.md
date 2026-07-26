# HECTON-8 Abyssal Water Bible

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Scope: abyssal water presentation, current fields, silt, turbidity, caustics, flow cues, flooding presentation, wetness, buoyancy cues, exterior/interior water state, and water-related proof gates.

## 1. Prime Law

Water in HECTON-8 is not a full fluid simulator. It is a controlled pressure medium that makes route, danger, scale, machinery, and visibility readable.

The project rejects two failures equally:

- fake water that is only blue fog, bloom, and particles;
- expensive water simulation whose result the player cannot read, exploit, or fear.

Any water feature must name its job before implementation:

- route visibility;
- pressure or depth cue;
- current direction;
- flooding state;
- hull or seal failure;
- sonar/noise interference;
- silt disturbance;
- creature concealment or reveal;
- salvage risk;
- cinematic material response.

If the feature cannot name a player-readable job, it is decoration and must not enter runtime.

## 1.1 Surface And Shallows Water

Surface water and photic shallows must be beautiful, bright, and readable. They are not allowed to inherit abyssal darkness as a default grade.

Required surface/shallow traits:

- clear ocean color with believable depth falloff;
- wave normals, specular sparkle, foam, refraction, caustic hints, and waterline wetness where visible;
- terrain readable through shallow water when the player or camera is near the surface;
- quality scaling that reduces density, resolution, or update cadence before it damages the art direction.

Depth can remove light and increase turbidity. The surface cannot be made ugly to save budget. Low hardware keeps the clean ocean read; high hardware buys richer reflection, caustics, spray, foam breakup, and underwater light shafts.

Depth/light lock:

- 0-100 m: mostly clear, bright, beautiful, and readable water.
- Deep caves may be dim or dark even inside shallow bands.
- 200-400 m: increasing turbidity and twilight.
- 400-500 m and below: darkness/murk becomes normal, but route silhouettes, instruments, and return cues must survive.

Subnautica-level surface/shallow water readability is the floor. HECTON-8 should exceed it through material response, shore/waterline detail, alien ecology, and technogenic traces where appropriate.

## 2. Truth Ownership

Water truth is split deliberately:

- `physics.md` owns pressure, hull damage, flooding scalar state, buoyancy force packets, collision consequences, and tether/cable force interaction.
- `vehicles.md` owns submarine/suit motion response, docking/EVA water handoff, cockpit instruments, and vehicle pressure envelope.
- `world.md` owns depth zones, biome turbidity, flow corridors, vents, sediment fields, and authored water landmarks.
- `rendering.md` owns fog, water material, caustic projection, silt particles, wetness shaders, and GPU budget.
- `audio.md` owns muffling, sonar, pressure groans, water ingress sound, and mix-state response.
- `ui.md` owns instrument readout and warning presentation.

`DepthZoneDirector.cs` in `Assets/_Project/Scripts/World/` owns depth zone evaluation and discovery triggers. It executes via `ISlowTickable` at a 2 Hz cadence (0.5s interval) with zero GC allocations. Zone transitions publish blittable 8-byte `DepthZoneEventPayload` structs over fixed-capacity (16 entry) native queues.

No water script may become a hidden global owner for pressure, route, damage, AI, save, or vehicle truth. It must consume snapshots from the named owners and publish only its assigned presentation or authored field data.

## 2.1 Current Static Source Anchor - Ocean Kinematics

Evidence class: STATIC_SOURCE only. `Assets/_Project/Scripts/Plugins/Crest/OceanKinematics/OceanKinematicsVaultRuntime.cs` owns Vault-backed analytical ocean sampling buffers for the Crest/ocean bridge. It is not a Crest material wrapper, not a runtime material clone path, and not permission to instantiate or override Crest materials at runtime.

Static source anchors:

- owner/system: `SystemID.Fluid`; buffers are resolved through `IDataVault` generation handles;
- buffers: `OceanKinematicsBufferIds` `72940` through `72950` for requests, results, Gerstner waves, tuning, macro state, telemetry ring/cursor, GPU cached results, CSV scratch, queue counters, and rollback fence;
- capacity: `RequestCapacity = 50000`, `WaveCapacity = 8`, `TelemetryCapacity = 300`;
- truth route: `OceanKinematicsSampleRequestDTO` to analytical Gerstner evaluation/cache to `FluidSampleResultDTO` and `OceanMacroStateDTO`; water rendering and Crest asset materials remain separate owners;
- black-box route: `OceanKinematicsTelemetryEntry[300]`; source dump target is `Docs/AgentLogs/Dump_SHINOBU_261.bin` on fault;
- proof gap: Unity import, Play Mode, profiler/GC, Frame Debugger/RenderGraph, Crest scene binding, and visual water quality remain `PENDING VERIFICATION`.

## 2.2 Current Static Source Anchor - Water-Level Calibration

Evidence class: STATIC_SOURCE only. The current sea-level calibration route is first-party and split from terrain macro geology. Terrain may produce seafloor height and terrain-provider fallback values; it does not own the calibrated ocean surface.

Static source anchors:

- contract owner: `Assets/_Project/Scripts/World/Contracts/WorldWaterLevelCalibrationContracts.cs`;
- scene/Crest authoring owner: `Assets/_Project/Scripts/Plugins/Crest/WorldWaterLevelCalibrationAuthoring.cs`;
- editor install/validation route: `Assets/_Project/Scripts/Plugins/Crest/Editor/CrestWorldWaterLevelCalibrationInstaller.cs`;
- generated source artifact: `Docs/GeneratedAssets/Terrain/MacroGeology/WorldWaterLevelCalibration_Extent30000m_Res192.json`;
- migrated-away path: `Assets/_Project/Scripts/World/WorldWaterLevelCalibrationAuthoring.cs` is not the current authoring location.

Runtime contract:

- `WorldWaterLevelCalibrationDTO` publishes requested, resolved, and fallback water level Y, calibration travel meters, authoring/runtime seeds, source hash, and flags;
- `WorldWaterLevelCalibrationMath` clamps non-finite or out-of-envelope values through `DefaultWaterLevelY = 14.02`, `MinimumCalibrationTravelMeters = 100`, and `MaximumAbsoluteWaterLevelY = 1000`;
- `WorldWaterLevelCalibrationRuntimeRegistry` holds the active read model, resets at subsystem registration, clears on owner disable, and marks duplicate owners through `DuplicateOwner`;
- `WorldWaterLevelCalibrationAuthoring` is `ExecuteAlways`, applies the resolved Y to the Crest root or override transform, binds a local `Crest.OceanRenderer` when present, and publishes debug flags;
- `CrestWorldWaterLevelCalibrationInstaller` reads the generated JSON lane order `strictCandidateLevels`, then `bestLevels`, then `allLevels`, installs the authoring component on `Assets/_Project/Prefabs/Ocean_Crest.prefab`, can install the prefab into `Assets/_Project/Scenes/02_HECTON_WORLD.unity`, and refuses to mutate assets while Play Mode, compiling, or importing/updating is active.

Consumer route:

- `ShinobuOceanSurfaceAtmosphereRuntime` reads `WorldWaterLevelCalibrationRuntimeRegistry` first when resolving `SeaLevel`, then falls back through weather/serialized sea level;
- `GlobalRegistry.OceanKinematics` is the runtime sea-level read route for buoyancy, resource distribution, biome/depth matrix logic, Sargassum/flora systems, PDA map fallback, storm propagation, Atlas signal depth, bioluminescence darkness, chunk residency, and other systems that need water depth;
- MapMagic/terrain bridges may expose water surface fallback values, but active ocean kinematics is the preferred live source when initialized;
- save/load terrain identity may compare water calibration metadata, but saves must not serialize an entire water field or turn water presentation into save truth.

Failure modeling required before acceptance:

- generated calibration artifact missing, malformed, stale, or without a readable lane;
- requested water level non-finite, outside `MaximumAbsoluteWaterLevelY`, or farther than calibration travel from fallback;
- Crest prefab or scene has no `OceanRenderer`, missing `Crest4KinematicsAdapter`, missing calibration component, or Crest root Y does not match resolved calibration Y;
- duplicate calibration owners register in one domain lifetime;
- registry survives domain reload incorrectly, stale read model remains after scene unload, or owner disables without unregistering;
- installer runs during Play Mode, compile, import/update, or without the target prefab/scene;
- consumers read default `14.02` while assuming a calibrated value was applied;
- terrain, atmosphere, physics, or UI code starts writing a competing sea-level truth instead of consuming ocean kinematics/calibration.

Proof gap: Unity import, installer execution, prefab diff, world-scene binding, Play Mode registry replacement, save/load identity mismatch, profiler/GC, and player traversal remain `PENDING VERIFICATION`.

## 3. Cinematic Fake First

Before adding any dynamic water simulation, prove that these cheaper routes are insufficient:

- scalar room fill ratio for flooding;
- 1D or 2D flowfield texture for currents;
- signed distance or volume mask for local wetness/flood boundary;
- vertex color wetness masks baked into generated assets;
- shader-space normal flow for surface shimmer;
- screen-space distortion limited to local glass/water interfaces;
- particle impostors for silt bursts and leak jets;
- audio/UI/haptic warnings for pressure and ingress;
- authored animation or VAT for cables, flora, and debris.

Continuous fluid simulation is allowed only for a local, inspectable, gameplay-critical event where the player can react to the result. Background compartments, distant ocean volume, ambient currents, decorative leaks, and noninteractive water turbulence use fakes.

## 4. Current And Flow Field Law

Currents must be data fields, not random forces.

Each current zone must declare:

- field ID;
- owner chunk or biome;
- vector source: baked texture, spline lane, analytic function, or DataVault payload;
- update cadence;
- affected systems: particles, flora sway, debris drift, AI navigation cost, vehicle assist/resist, audio;
- maximum force or presentation displacement;
- quality scaling;
- fallback when the field is missing.

Runtime current sampling must be bounded, cache-friendly, and allocation-free. Do not search scene objects to find water zones. Do not allocate lists of affected objects. Do not publish per-object managed events for every current sample.

Compact lane may sample coarse flow cells or a baked 2D field. High and Ultra may add richer local turbulence, eddy masks, secondary particle response, and finer shader detail, but the gameplay-affecting current vector must remain stable and deterministic.

## 5. Visibility, Turbidity, And Fog

Underwater visibility is a gameplay resource. Fog must expose route and danger, not hide weak art.

Every water volume must define:

- near clarity;
- far extinction;
- color absorption;
- silt density;
- particulate response;
- route cue visibility distance;
- emergency readability distance;
- cockpit/visor readability override.

Pure black void is rejected. Generic blue fog is rejected. The correct look is black water with structure: suspended matter, weak lights, silhouettes, sonar hits, route glints, pressure haze, and dirty glass.

Fog density may scale with depth, biome, silt, damage, and current, but it must never remove the only readable return path. At Compact quality, route cues and hazard silhouettes must survive even when secondary particles, caustics, and volumetric layers are reduced.

## 6. Silt, Particles, And Debris

Silt is evidence. It tells the player that something moved, leaked, collapsed, or disturbed the floor.

Silt systems must obey:

- pooled particles only;
- no per-frame managed allocation;
- bounded emission counts;
- no collision-heavy particles without proof;
- texture atlas use for particle sprites;
- deterministic trigger source where gameplay needs replayability;
- visible decay curve;
- no global always-on cloud that flattens the scene.

Silt bursts should come from physical events:

- door pressure equalization;
- tool cut;
- landing impact;
- creature movement;
- rock collapse;
- water ingress;
- current shear;
- salvage removal.

If silt appears everywhere at the same density, it becomes noise and is rejected.

## 7. Caustics And Light Interaction

Caustics are not a decoration pass. In deep water, strong caustics need a believable light source, shallow volume, artificial projector, glass tank, floodlight, or local optical reason.

Allowed caustic routes:

- baked or flipbook caustic texture projected near lamps, glass, pools, or shallow flooded interiors;
- low-frequency shader caustics on close wet surfaces;
- local RenderGraph pass only with GPU proof;
- static decal caustics in authored interiors;
- material response tied to wetness mask.

Rejected:

- global dancing caustics across abyssal terrain without light reason;
- high-sample volumetric caustics without proof;
- caustics that hide geometry or confuse interactables;
- baked lighting inside albedo textures pretending to be water response.

## 8. Flooding Presentation

Flooding state is owned by physics or persistence. Water presentation reads it.

Preferred implementation:

1. Room owner publishes scalar fill ratio, breach ID, pressure delta, ingress direction, and confidence.
2. Presentation maps scalar state to water plane, wetness masks, leak jets, sound, UI warning, particles, and material darkening.
3. Gameplay contact uses simplified volumes or physics owner data, not visual mesh triangles.
4. Save/load restores scalar truth first, then presentation rebuilds from it.

Do not simulate free-surface water for every compartment. Do not let the visual water plane become the save truth. Do not let particles decide damage.

## 9. Wetness And Material Response

Wetness must be material truth, not a glossy overlay sprayed everywhere.

Wetness data may come from:

- vertex color G/A in generated meshes;
- packed MRAO or wetness masks;
- local volume masks;
- decal masks;
- scalar flooding state;
- tool or leak contact records.

Wetness response should affect roughness, darkening, normal intensity, drip decals, and local specular behavior. It must preserve material identity: painted metal, rubber, glass, stone, flesh, and algae do not become the same shiny surface.

## 10. GlobalQualityWeight Scaling

Compact:

- coarse flow fields;
- scalar flooding presentation;
- low-count pooled particles;
- static or low-frequency fog layers;
- limited caustics near justified lights;
- strong route silhouettes and UI/audio redundancy.

Middle:

- finer flow zones near gameplay;
- richer silt bursts;
- wetness masks on key assets;
- more local fog variation;
- better cockpit/glass response.

High:

- local turbulence masks;
- denser silt near impacts;
- layered wetness and drip decals;
- improved caustic projection where justified;
- richer underwater light shafts within budget.

Ultra:

- cinematic local water response for hero events;
- higher-resolution flow/turbidity fields;
- richer secondary particle motion;
- better material-specific wetness;
- presentation overkill while gameplay truth, save identity, and owner routes remain unchanged.

No quality tier may change pressure truth, route truth, save data, collider truth, or vehicle authority.

## First-20 Route Hook

- First-20 moment: first exit and swim through bright photic water with readable depth, return route, oxygen pressure, and shallow hazard cues.
- Route blocker removed: water cannot hide weak terrain, missing return cues, or absent pressure/flooding truth behind darkness, fog, or decorative particles.
- Proof class: screenshot, Frame Debugger for water/fog/caustic passes when changed, Profiler/GCMonitor for runtime sampling or particles, Play Mode/player capture for swim readability, and save/load artifact for persistent flooding or wetness.

## 11. Proof Artifacts

Water work must provide:

- named truth owner route;
- current/flooding/turbidity field manifest;
- Compact and High screenshot or capture;
- debug view for flow direction, turbidity, fog volume, or fill ratio when relevant;
- profiler proof for runtime water features;
- GC proof for update/sampling paths;
- GPU proof for caustics, particles, volumetrics, or custom render passes;
- save/load proof for flooding or persistent wetness;
- rejection note for any proposed real simulation.

Static documentation may only claim `STATIC_SOURCE_REVIEWED` or `STATIC_DOC_REVIEWED` with exact anchors. Runtime claims remain `PENDING UNITY/PROFILER VERIFICATION` until measured.

## 12. Rejection Gates

Reject water work if:

- it is generic blue fog;
- it is pure darkness without route structure;
- it simulates fluid that does not change a player-readable decision;
- it allocates in current sampling, flooding updates, UI warning routes, or particle triggers;
- it uses global always-on particles to hide weak art;
- it changes gameplay truth by quality tier;
- it makes caustics appear without a believable light reason;
- it stores save truth in visual water objects;
- it hides interactables, doors, return routes, or hazard silhouettes;
- it claims low-end readiness without Compact capture and profiler proof.

## 13. Acceptance Sentence

Water is accepted only when it makes pressure, route, damage, current, visibility, and material state more readable through controlled premium presentation approximations, named truth owners, continuous quality scaling, zero-GC runtime paths, and measured proof where runtime behavior exists.
