# TERRAIN_AUDIT Log

## 2026-05-26 - Terrain Implementation Audit

What was wrong:
- Terrain ownership is not one system. Base land is MapMagic/Unity Terrain, then Hecton runtime bridges cache and sample it, while voxel and geology seam systems can add cave volumes, holes, blend masks, and heightmap patches.
- Production scene 02_HECTON_WORLD is binary/serialized. It contains MapMagicObject/TerrainTile/MapMagicBridge/TerrainSeamApplier strings, but exact hierarchy is not clean YAML-auditable from static grep.
- Runtime generation expectations are ambiguous because MapMagicRuntimeBridge fences MapMagicObject by disabling it in play mode after binding. Current route assumes terrain tiles are already applied/serialized or controlled through the bridge's own streaming configuration.

What was done:
- Read AGENTS.md, domain file, terrain/voxel/streaming/rendering/zero-GC/visual-fake mandates.
- Located active terrain scripts: MapMagicRuntimeBridge, MapMagicBridge, HectonMapMagicVegetationBridge, VegetationTerrainHoleSynchronizer, HectonVoxelEngine, SeamRegistry, VoxelSeamDirector, WorldGenerativeGeologyIntegrationDirector, WorldGenerativeGeologyTerrainSeamApplier, WorldGenerativeGeologyVoxelBridgeDirector, TerrainChunkPagerRuntime.
- Verified scene/build route: 00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD. Sandbox 03 has explicit MapMagicObject plus baked Unity Terrain preview, but production terrain is serialized inside 02.
- Traced land path:
  1. MapMagic graph/custom Hecton nodes produce MatrixWorld height/splat/anomaly products.
  2. MapMagic applies products to Unity TerrainTile/TerrainData.
  3. MapMagicRuntimeBridge registers GlobalRegistry.MapMagic and GlobalRegistry.Terrain, caches TerrainTile references, samples cached vegetation height first, then TerrainData.GetInterpolatedHeight/Normal.
  4. HectonMapMagicVegetationBridge reads terrain heightmapTexture through AsyncGPUReadback into DataVault-backed ushort R16 caches and exposes hot height/splat/vegetation payloads.
  5. HectonVoxelEngine samples terrain height grids, runs SDF/Marching Cubes, spawns voxel cave volumes, and registers terrain holes for entrances.
  6. WorldGenerativeGeologyIntegrationDirector builds seam plans from MapMagic height. WorldGenerativeGeologyTerrainSeamApplier can patch Unity terrain heightmaps with SetHeightsDelayLOD and publish voxel invalidation.
  7. SeamRegistry and SeamGapDitherRenderer persist/render seam visual masking.

Cinematic Cheats used:
- Distant terrain shadow/fade mask is a CPU-built low-res texture, not physical lighting.
- SeamGapDitherRenderer uses indirect billboard/quad masking for seam hiding.
- Voxel collider path has cinematic collider fake selection for non-critical volumes.
- Slope quantization and texture masks buy visual readability without full terrain physics simulation.

Exact Microseconds saved:
- No runtime code was changed; measured saving is 0 us.
- Estimated static-audit opportunities: avoid reflection boxing in terrain seam quality injection, avoid runtime GetComponentsInChildren cache refresh during streaming, keep height sampling on R16 native cache rather than TerrainData fallback, and cap terrain seam SetHeightsDelayLOD patch sizes.

Defects and rule debt:
- MapMagicRuntimeBridge disables bound MapMagicObject at runtime. If live MapMagic generation is required, this blocks it.
- RefreshTerrainTileCache uses GetComponentsInChildren<TerrainTile>(true). It is marked cold, but runtime hierarchy churn would allocate.
- Terrain seam writeback uses managed float[,] plus TerrainData.SetHeightsDelayLOD/SyncHeightmap. It is bounded but remains the expensive Unity bridge.
- Terrain seam GlobalQualityWeight injection uses reflection/boxing despite public job fields.
- Voxel seam normal blend is nlerp, while the seam mandate requests spherical blending.
- SeamRegistry is managed Dictionary/List based, not NativeParallelHashMap as the seam mandate target describes.
- ProjectSettings TagManager contains tag "Terrain " with trailing space. No exact CompareTag("Terrain") script hits were found in first-party scripts, but the tag is a latent integration trap.
- Addressables terrain wiring was not found; if heavy terrain is expected to load async, this is a project wiring gap.

Verification:
- Static audit only. No code edits beyond audit docs. No Unity compile/build was run.

## 2026-05-26 - Follow-up Assessment: Terrain Quality and Rewrite Risk

Verdict:
- Current terrain implementation is not junk, but it is not a clean production-grade chunked procedural ocean-floor runtime either.
- Strong pieces: MapMagic graph/custom nodes, native R16 height cache, voxel SDF cave generation, terrain seam black-box/telemetry concepts, bounded SignalBus route.
- Weak pieces: split ownership, binary scene opacity, runtime MapMagic fence ambiguity, managed seam writeback, reflection quality injection, dictionary/list-heavy state, and no Addressables terrain asset route found.

Rewrite judgement:
- Do not rewrite math kernels first. Keep useful generation jobs and MapMagic authoring nodes.
- Rewrite/top-level replacement should target ownership and runtime route: one terrain chunk provider, one native height/splat cache, one seam writeback owner, one visual mask owner, one validation artifact.
- MapMagic should be either authoring/bake source or explicitly controlled runtime generator. Current hybrid state is ambiguous.

Real risks:
- If the design target is live procedural chunk streaming, `mapMagicObject.enabled = false` in runtime bridge is a direct contradiction unless terrain tiles are intentionally pre-applied.
- `SetHeightsDelayLOD` and managed `float[,]` are acceptable only as rare bounded patch bridges, not routine streaming terrain formation.
- `GetComponentsInChildren<TerrainTile>` cache refresh must stay cold. If tile hierarchy changes during gameplay, allocation returns.
- Reflection/boxing for `GlobalQualityWeight` injection is unjustified and should be direct field assignment.
- Scene serialization should be made auditable; binary production terrain makes CI/static review weaker.

Usability/integration:
- Designers likely have MapMagic graph control, which is useful.
- Engineers lack one clear runtime terrain owner. Debugging "where did this height come from" currently crosses MapMagic, vegetation cache, voxel engine, seam applier, and scene serialization.
- Other systems can consume terrain through bridges, but read paths are not all pure by doctrine because cache touch state mutates on reads.

Optimization:
- Best immediate wins are not exotic math: remove reflection, prevent runtime cache refresh allocations, keep voxel terrain sampling on native cached grids, cap seam writeback patch area, and make MapMagic runtime mode explicit.
## Entry 2026-05-26

Status: PENDING VERIFICATION.
What was wrong: User requested terrain implementation audit without a batch XML prompt or explicit agent ID.
What was done: Created read-only audit identity `TERRAIN_AUDIT`; read project authority and terrain mandates.
Cinematic Cheats used: None. Audit only.
Exact Microseconds saved: No measured runtime change; static audit cannot claim savings.

## 2026-05-26 - Expanded Terrain Consumer Integration Audit

Scope:
- Read-only audit of C:\hades\Hecton8. No Unity launch. No dotnet/Unity build.
- Mission: trace terrain height, splat/biome, holes, seams, ocean, nav, save/load, telemetry, sonar/audio consumers.

How land currently appears:
- Build route is 00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD (`ProjectSettings/EditorBuildSettings.asset:9,12,15`).
- Production world scene is binary to grep (`Assets/_Project/Scenes/02_HECTON_WORLD.unity`), but string scan confirms MapMagic/terrain bridge wiring. Sandbox scene is text and has `SANDBOX_MAPMAGIC_RUNTIME` and MapMagicObject (`Assets/_Project/Scenes/03_HECTON_SANDBOX_BIOMES.unity:323,355`) plus baked preview terrain (`:608`).
- Runtime owner is `MapMagicRuntimeBridge`, registered both as `GlobalRegistry.MapMagic` and `GlobalRegistry.Terrain` (`Assets/_Project/Scripts/Plugins/MapMagic/MapMagicRuntimeBridge.cs:326-340`).
- MapMagic tile application is hooked through `TerrainTile.OnTileApplied` and `OnTileMoved` (`MapMagicRuntimeBridge.cs:2963-2991`), then queued via `MapMagicTerrainTileEvents` and `TerrainChunkGeneratedEvents` (`MapMagicBridge.cs:371-380`, `TerrainChunkGeneratedEvents.cs:27-53`).
- Height reads first hit vegetation/native cache, then Unity TerrainData fallback (`MapMagicRuntimeBridge.cs:797-825`). Normals use Unity terrain normal or gradient fallback (`MapMagicRuntimeBridge.cs:832-892`). Splat/biome uses alphamap textures and `GetPixelData` (`MapMagicRuntimeBridge.cs:1169-1245`, `:1642`, `:1732`).
- Hot native height data is owned by `HectonMapMagicVegetationBridge` as R16 NativeArray payload aliases (`Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:1022-1062`, `:2913-3018`, `:3412-3423`). It populates from terrain alpha/height textures and AsyncGPUReadback (`:7455-7582`, `:7598`, `:7668-7677`).
- Geology seam applier can mutate Unity Terrain heightmaps after MapMagic tile output (`WorldGenerativeGeologyTerrainSeamApplier.cs:515-540`, `:646-878`).
- Voxel runtime samples terrain grids for SDF density and seam snapping (`HectonVoxelEngine.cs:762-770`, `:2548-2612`, `:8372-8424`) and creates terrain holes for cave entrances (`:11314-11339`).

Major consumers:
- Voxel: terrain height affects SDF density, seam snap, normals, skirts, cave holes.
- Vegetation/flora: terrain cache owns sand/rock masks, height samples, holes, macro flora/nav obstacles.
- AI/nav: `VoxelDynamicNavGridRuntime` reads vegetation bridge directly for macro flora and cached floor height (`World/VoxelDynamicNavGridRuntime.cs:1129-1158`, `:1749-1758`).
- Fluid/ocean jobs: `HectonFluidEngine` consumes ITerrainHeightSampleReadModel R16 payload for shore fallback (`HectonFluidEngine.cs:3376-3390`, `:8202-8312`).
- Crest ocean prefab: sea floor depth data is disabled and mapMagicBridge is unbound (`Assets/_Project/Prefabs/Ocean_Crest.prefab:419`, `:642`, `:644-647`, `:729`).
- Fauna: `FaunaDirector` caches ITerrainProvider but comments still claim GetAlphamaps GC path (`FaunaDirector.cs:502-509`, `:779-812`, `:1148-1166`, `:1788-1800`).
- Ecosystem: uses `GlobalRegistry.MapMagic` directly for water level and apex spawn terrain gate (`World/EcosystemDirector.cs:1291-1296`, `:1574-1577`, `:4207-4211`).
- Creature IK: `FaunaKinematicsRuntime` reads `GlobalRegistry.TerrainHeightSamples`, then may override samples with DataVault `TerrainSeamHeightmap` (`Fauna/FaunaKinematicsRuntime.cs:641-645`, `:1815-1851`). `LeviathanTerrainIkJobs` has its own 300-entry black box and samples terrain in job (`Animation/LeviathanTerrainIkJobs.cs:19-29`, `:141-233`, `:481-521`, `:916-965`).
- Sonar/audio: active sonar uses published voxel SDF payload (`Audio/PlayerCriticalProceduralAudioRenderer.cs:5148-5162`); acoustic vegetation uses global static vegetation state (`AcousticZoneController.cs:2286-2302`).
- Save/load: `SeamRegistry` persists seam/cave entrance DTOs with managed dictionaries (`SeamRegistry.cs:15-55`, `:147-155`, `:273-300`, `:398-445`).

Broken contracts and defects:
- Primary defect: no single terrain truth route. Consumers use ITerrainProvider, ITerrainHeightSampleReadModel, direct `HectonMapMagicVegetationBridge.ActiveRuntimeInstance`, `GlobalRegistry.MapMagic`, and DataVault `TerrainSeamHeightmap`. This violates one fact -> one owner -> one route.
- `MapMagicRuntimeBridge` disables `mapMagicObject.enabled` at runtime (`MapMagicRuntimeBridge.cs:2518-2533`). If live procedural chunk generation is required, this contradicts the requirement unless tiles are intentionally pre-applied/baked.
- `TerrainChunkGeneratedSignal.CacheRevision` exists but `MapMagicBridge` publishes `CacheRevision = 0` (`MapMagicBridge.cs:460-467`). The seam applier later copies payload.CacheRevision (`WorldGenerativeGeologyTerrainSeamApplier.cs:611-641`). Event revision is not trustworthy.
- `TerrainSeamHeightmap` is one global buffer copied from the last matched terrain signal (`WorldGenerativeGeologyTerrainSeamApplier.cs:624-641`). `FaunaKinematicsRuntime` can use that buffer as a terrain fallback if length matches, without checking terrain hash/origin/revision (`FaunaKinematicsRuntime.cs:1843-1849`). This is a real wrong-height risk.
- `WorldGenerativeGeologyTerrainSeamApplier` injects `GlobalQualityWeight` into jobs using reflection and boxing (`WorldGenerativeGeologyTerrainSeamApplier.cs:91-98`, `:1254-1273`). This violates zero-GC/runtime reflection discipline and should be direct fields.
- Seam projection completes a job synchronously before Unity Terrain writeback (`WorldGenerativeGeologyTerrainSeamApplier.cs:793-797`). It is declared cold/SlowTick, but still needs profiler proof for MX350.
- Unity terrain writeback uses managed `float[,]` and `SetHeightsDelayLOD` (`WorldGenerativeGeologyTerrainSeamApplier.cs:537-540`, `:1840-1847`). Acceptable only as rare bounded seam patches, not normal chunk formation.
- Terrain holes use managed `bool[,]` and `SetHolesDelayLOD` (`World/VegetationTerrainHoleSynchronizer.cs:1019-1024`, `:1128-1154`). The job is run synchronously with `.Run` (`:1079-1091`), so this is not fully async hole processing.
- Voxel seam normals use Nlerp (`HectonVoxelEngine.cs:2609-2612`) while terrain/voxel seam mandate asks for spherical normal blend. This is minor visually but real mandate drift.
- `MapMagicRuntimeBridge.RefreshTerrainTileCache` allocates via `GetComponentsInChildren<TerrainTile>(true)` on hierarchy changes (`MapMagicRuntimeBridge.cs:2568-2588`). Cold only if MapMagic hierarchy is stable.
- `SeamRegistry` is managed Dictionary based (`SeamRegistry.cs:28-55`) while seam mandate target is NativeParallelHashMap-style runtime seam data. Save path is acceptable; runtime seam lookups are not ideal.
- `VoxelDynamicNavGridRuntime` owns capped dictionaries/lists and creates a lifecycle GameObject/AddComponent (`World/VoxelDynamicNavGridRuntime.cs:77-99`, `:2018-2019`, `:2176-2200`). It is engineered as cold/static, but it does not fully match native/preallocated nav mandate.
- Production scene is binary. This blocks reliable scene review and makes terrain wiring regressions harder to diff.
- `ProjectSettings/TagManager.asset:27` contains tag `Terrain ` with trailing space. Latent usability/integration trap.

Black-box proof:
- Present: voxel mesh pipeline 300-entry dump (`HectonVoxelEngine.cs:3133-3146`, `:7045-7097`), terrain seam 300-entry dump (`WorldGenerativeGeologyTerrainSeamApplier.cs:35-80`, `:1436-1484`, `:2061-2080`), Leviathan terrain IK 300-entry dump (`Animation/LeviathanTerrainIkJobs.cs:19`, `:141-233`).
- Missing by static scan: no equivalent 300-frame black box found for `MapMagicRuntimeBridge`, vegetation tile cache/readback ownership, or terrain hole synchronizer. A bad height/cache/hole frame can still be hard to explain.

Verdict:
- Current implementation is not production-clean for "beautiful procedural chunked seabed plus ocean floor". It is a useful prototype/partial production bridge with good pieces, but weak ownership and scene/runtime ambiguity.
- Do not rewrite all terrain math first. Rewrite the top-level terrain runtime route: one chunk provider, one native height/splat/hole payload contract, one seam writeback owner, one explicit MapMagic mode (authoring/bake or live runtime), one debug/provenance path.

Optimization judgement:
- Real gains are route cleanup, avoiding Unity Terrain writebacks during streaming, direct quality-weight fields, native cache revision correctness, cold-only tile refresh, and ocean depth wiring. No measured microseconds saved in this audit.

## 2026-05-26 - Primary Master Synthesis: Terrain/MapMagic Runtime Verdict

Scope:
- Read-only static audit. Unity editor, play mode, profiler, and dotnet build were not launched.
- Evidence sources: AGENTS/domain docs, terrain mandates, build settings, binary scene string scans, sandbox YAML scene/graph, MapMagic bridge/runtime code, vegetation cache, voxel/geology seam systems, ocean/nav/fauna/fluid consumers, StreamingAssets inventory, and validator code.

Executive verdict:
- Current terrain implementation is not production-clean for the requested target: beautiful procedural terrain generated by chunks with convincing ocean floor.
- It is a transitional hybrid: MapMagic/Unity Terrain provides the base visible ground, Hecton code caches/samples it, voxel/geology systems add caves/seams, and streaming systems exist beside it. The weak point is ownership, not lack of math.
- Keep the useful parts: custom MapMagic authoring nodes, R16/native height cache, SignalBus terrain events, voxel SDF, seam jobs, geology profiles, visual fake patterns.
- Rewrite the top-level terrain authority: one chunk provider, one native height/splat/hole/seam payload, one revisioned route, one runtime/debug provenance path.

How land is formed now:
- Build route includes `02_HECTON_WORLD` as the production world scene (`ProjectSettings/EditorBuildSettings.asset:9,12,15`).
- Production scene is binary. String evidence shows one MapMagic object, hundreds of TerrainTile/TerrainData records, many procedural proxy instances, MapMagic bridge references, and one geology terrain seam applier. Exact hierarchy cannot be trusted from grep alone.
- Sandbox terrain scene is clearer: `03_HECTON_SANDBOX_BIOMES.unity` has `SANDBOX_MAPMAGIC_RUNTIME`, a `MapMagicObject`, a graph GUID, and a baked preview Terrain/TerrainData.
- `MapMagicRuntimeBridge` resolves the MapMagic object, registers itself as `GlobalRegistry.MapMagic` and `GlobalRegistry.Terrain`, refreshes terrain tile cache, listens to MapMagic tile applied/moved signals, and exposes height/normal/splat sampling.
- Important contradiction: runtime bridge fences live MapMagic by setting `mapMagicObject.enabled = false` in play mode (`MapMagicRuntimeBridge.cs:2518-2533`). Therefore current production ground is closer to serialized/pre-applied Unity Terrain tiles plus bridge/cache logic, not a proven live chunk terrain generator.
- `WorldStreamingDirector` applies MapMagic terrain ranges, draft/main settings, object budgets, pixel error, basemap distance, and terrain detail density each slow tick (`WorldStreamingDirector.cs:346-377`, `:460-476`). This tunes existing MapMagic/Unity terrain; it does not prove independent chunk generation.

MapMagic role:
- The sandbox MapMagic graph serializes only three Hecton custom nodes by exact search:
  - `HectonBiomeMatrixMapMagicPostProcessNode`, serialized enabled value appears `0`.
  - `HectonHydraulicErosionMapMagicNode`, `dropletCount=1000000`, `maxLifetime=64`, thermal slumping enabled internally, serialized generator enabled value appears `0`.
  - `HectonTerrainSplatmapMapMagicNode`, serialized enabled value appears `1`.
- Existing code also contains `HectonSandboxAbyssalShelfMapMagicNode`, `HectonAnomalyMapMagicNode`, and SpaceEngine-style ridged/crater/rille nodes, but the sandbox graph does not reference them by exact search.
- `MapMagicWorldValidator` explicitly marks heavy MapMagic graph nodes as forbidden when enabled: built-in Blur/Erosion/Cavity/Terrace and `HectonHydraulicErosionMapMagicNode` (`MapMagicWorldValidator.cs:18-25`, `:428-440`). This is project-local evidence that live runtime graph cost is already considered unacceptable.
- Conclusion: MapMagic is useful as an authoring/bake/reference generator, not currently proven as the final live chunk terrain runtime.

Chunk/streaming evidence:
- `WorldChunkStreamingProfile.asset` is finite and profile-based: `worldSizeMeters=15000`, `chunkSizeMeters=192`, residency radii 180/420/900/1800.
- `TerrainChunkPagerRuntime` exists and has DataVault buffer ids, worker-thread style request processing, `GlobalQualityWeight`, and chunk byte lanes. It defaults include `forceMockDiskIo = true` and a `Hecton8/TerrainChunks` root.
- Current `Assets/StreamingAssets/Hecton8` only contains audio banks and `DataMonolith/static_data.h8bin`. No terrain chunk sidecars, terrain erosion files, world sectors, or hadal trench payloads were found.
- Static scene search did not prove `TerrainChunkPagerRuntime` or `GlobalWorldSampler` are active in production scene. `WorldStreamingDirector` is active, but it manages profiles/budgets rather than replacing MapMagic base terrain.
- Therefore the advertised chunk terrain route is not complete in the current checkout.

Main defects:
- No single terrain truth. Height can come from Unity TerrainData, MapMagic bridge, vegetation R16 cache, DataVault seam heightmap, voxel SDF, terrain holes, and seam writeback.
- Some consumers read through concrete globals/statics instead of one read model: `GlobalRegistry.MapMagic`, `GlobalRegistry.TerrainHeightSamples`, `HectonMapMagicVegetationBridge.ActiveRuntimeInstance`, and `TerrainSeamHeightmap`.
- `TerrainChunkGeneratedSignal.CacheRevision` is published as `0`, so event revision cannot prove cache freshness.
- `TerrainSeamHeightmap` is a global last-copied seam buffer; fauna IK can use it when length matches without terrain hash/origin/revision validation. Wrong-height risk.
- `GlobalWorldSampler.ResolveSamplingCadenceDivisor` returns `1` and `ShouldSampleOnFrame` returns `true` despite `LowQualityCadenceDivisor=12`; continuous quality cadence is stubbed.
- `HectonMapMagicVegetationBridge` read paths touch last-access state in `TryGetActiveTileCache`; read accessors are not pure by doctrine.
- Tile state lookup enumerates dictionary values by position. This is acceptable only while small/slow; it is not a strong hot terrain sample path.
- `MapMagicRuntimeBridge.ConfigureRuntimeTerrainStreaming` forces draft resolution to tile resolution in play mode, collapsing draft benefit on low-tier devices.
- Seam applier uses reflection/boxing to inject `GlobalQualityWeight`; this is not acceptable production runtime discipline.
- Unity Terrain writeback uses `SetHeightsDelayLOD`, `SyncHeightmap`, managed `float[,]`, and terrain holes use managed `bool[,]`/`SetHolesDelayLOD`. Fine for rare bounded patches; bad as a normal streaming generator.
- Crest ocean prefab has sea-floor depth disabled/unbound in the checked prefab evidence, so visual ocean depth is not strongly tied to terrain truth.
- Production scene is binary, blocking reliable review and CI diffing of terrain ownership.
- `ProjectSettings/TagManager.asset:27` contains `Terrain ` with a trailing space; latent integration/usability fault.

What is good:
- `MapMagicRuntimeBridge` is a useful abstraction layer and avoids scene search in most runtime consumers.
- The vegetation bridge's R16/native payload path is the best current terrain sampling route for jobs.
- Terrain tile events exist and are routed into first-party signals.
- Voxel/geology integration is directionally correct: MapMagic/base terrain plus SDF caves/seams can produce better underwater terrain than heightfield-only terrain.
- Visual fake doctrine is used in places such as bottom silt and distant masks; that is correct for underwater presentation.
- Several systems have black-box buffers: voxel, terrain seam, Leviathan terrain IK. Missing black boxes remain for MapMagic bridge, vegetation cache/readback, and hole sync.

Rewrite recommendation:
- Do not rewrite all procedural math first. That is the wrong target.
- Build `TerrainRuntimeAuthority` or `TerrainChunkProvider` above MapMagic and Unity Terrain.
- Define a single native chunk payload: height R16/float lane, normal/detail lane, splat/biome masks, holes, seam deltas, `chunkCoord`, `terrainHash`, `cacheRevision`, `sourceFlags`, quality metadata.
- Make MapMagic an explicit mode: editor/bake source by default; live runtime only behind a clear runtime generator contract and budget proof.
- Replace binary scene terrain truth with exported/validated terrain graph and chunk manifests.
- Move erosion, anomaly, shelf/trench generation to offline bake or bounded background windows. No synchronous one-million-droplet generation on gameplay route.
- Make ocean/Crest, fluid, nav, fauna IK, vegetation, audio/sonar, voxel, and geology read the same payload route.
- Keep Unity Terrain as visual/collider adapter only, not the canonical source of truth.

Scalability:
- Low: cached R16 height, coarse splat masks, visual-only seam/silt tricks, low detail density, no live heavy MapMagic jobs, bounded terrain writeback.
- Middle: precomputed chunk payload streaming, limited seam patches, moderate detail normals, stable HLOD impostors.
- High: richer masks, more detail normals, more visual blending, larger residency radius.
- Ultra: overkill visual masks, denser seabed scatter, improved erosion/detail baked variants, but same authoritative payload and revision route.

Optimization judgement:
- Biggest wins: remove route ambiguity, remove reflection, make read accessors pure, add tile spatial index, fix quality cadence, prevent draft-resolution collapse, keep Unity Terrain writes rare, wire ocean depth to terrain payload, and add black boxes for MapMagic/vegetation/hole sync.
- Exact microseconds saved: none measured. Static audit only. Any numeric savings would be fake without profiler capture.

## 2026-05-26 - Terrain Strategy Recommendation

Decision:
- Use a hybrid model, not pure prebaked and not pure live MapMagic.
- Prebake deterministic macro terrain and expensive geology. Runtime streams chunk payloads and performs bounded local modifications.

Chosen architecture:
- MapMagic remains an authoring/bake source for macro height, biome masks, splat masks, shelves, trenches, ridges, erosion output, and designer control.
- Runtime terrain authority is first-party `TerrainChunkProvider`, not MapMagicObject and not Unity TerrainData.
- First world boot can generate or hydrate a 4x4 km or 5x5 km spawn square, but the output must be saved as chunk payloads and then treated like streamed chunks.
- Later exploration loads/generates chunk rings asynchronously ahead of the player.
- Unity Terrain is visual/collider adapter only. It must not be terrain truth.
- Voxel caves, arches, rock overhangs, ore veins, and local destructible/geology edits are separate overlay layers with explicit revision and source flags.

Why not pure prebake:
- Cheap and stable, but it kills long-term procedural world identity unless every sector is preauthored.
- Storage grows quickly if height, splat, holes, caves, ore, vegetation, and HLOD are all baked at high fidelity.
- It weakens replay/world-seed variation.

Why not pure runtime:
- Current MapMagic/runtime path already fences live MapMagic generation.
- Heavy erosion/anomaly nodes have synchronous barriers and TempJob allocation risk.
- First load would become unstable on low-end hardware.
- Debugging terrain provenance would remain bad unless a first-party authority is added anyway.

Detail source:
- Macro: MapMagic graph or equivalent first-party graph baked to chunk payload.
- Meso: deterministic Burst terrain jobs for cliffs, terraces, sediment bands, canyon walls, shelf/trench polish.
- Micro visual: shader normals, detail textures, decals, scatter, fake overhang shadows, silt/sediment masks.
- True 3D: voxel/SDF chunks for caves, arches, tunnels, overhang interiors, ore pockets, and special geology.

Modification policy:
- Never modify MapMagic graph output directly in gameplay.
- Runtime edits are append-only deltas per chunk: seam deltas, cave holes, voxel volumes, ore extraction state, story/world events.
- Every chunk stores base payload hash plus delta revision.

Quality policy:
- Low: baked height + low-res masks + visual fakes + sparse voxel overlays.
- Middle: normal chunk payload + limited voxel caves/arches + bounded seam patches.
- High: richer masks, denser scatter, more voxel detail near player.
- Ultra: larger residency, higher visual microdetail, extra baked erosion variants, but same authoritative chunk route.

## 2026-05-26 - Terrain Repair Pass: Quality Scaling and Holes Sync

What was wrong:
- `GlobalWorldSampler.ResolveSamplingCadenceDivisor`, `ShouldSampleOnFrame`, `ResolveQualityWeight`, `ResolveExpensiveSamplingWeight`, and `ResolveOverkillSamplingWeight` were stubs returning full quality / every frame.
- `MockBoidRaymarchJob` ignored `Data.GlobalQualityWeight` and forced quality to `1f`.
- `VegetationTerrainHoleSynchronizer` called `TerrainData.SetHolesDelayLOD` without the required holes texture sync.
- Larger defects remain: split terrain authority, MapMagic runtime ambiguity, zero `CacheRevision` in tile events, global seam heightmap provenance risk, and Unity Terrain writeback adapters.

What was done:
- Implemented continuous finite/saturating GlobalQualityWeight handling in `Assets/_Project/Scripts/World/GlobalWorldSampler.cs`.
- Added smooth expensive sampling ramp after quality 0.30 and overkill ramp after quality 0.75.
- Made mock raymarch consume `Data.GlobalQualityWeight`.
- Added `TerrainData.SyncTexture(TerrainData.HolesTextureName)` after delayed terrain hole writes in `Assets/_Project/Scripts/World/VegetationTerrainHoleSynchronizer.cs`.
- Added `Assets/_Project/Tests/Editor/GlobalWorldSamplerQualityEditTests.cs` plus `.meta` to lock quality curves and delayed holes sync.

Cinematic Cheats used:
- Low quality now uses cheaper terrain sampling approximations instead of pretending low-end hardware can afford ultra sampling.
- Overkill noise/detail is gated to top quality instead of spending it everywhere.

Exact Microseconds saved:
- Not measured. No Unity profiler run.
- Expected low-end saving: fewer bilinear/trilinear/SDF/normal/detail branches in GlobalWorldSampler when `GlobalQualityWeight` is low.
- Expected correctness gain: delayed holes now update Unity holes texture/LOD/vegetation state per API contract.

External evidence:
- Unity `SetHeightsDelayLOD` docs: delayed height writes require `SyncHeightmap` after edits.
- Unity `SetHolesDelayLOD` docs: delayed holes writes require `SyncTexture(TerrainData.HolesTextureName)` after edits.
- Unity `AsyncGPUReadbackRequest` docs: request data is asynchronous and valid only when completed.
- Unity Addressables remote content docs: remote catalogs/bundles are the correct route for patchable large content; terrain chunk sidecars are absent in current StreamingAssets evidence.
- Unity JobHandle docs: `Complete` returns NativeContainer ownership to main thread and is a synchronization point, not free scheduling.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/GlobalWorldSampler.cs Assets/_Project/Scripts/World/VegetationTerrainHoleSynchronizer.cs Assets/_Project/Tests/Editor/GlobalWorldSamplerQualityEditTests.cs Assets/_Project/Tests/Editor/GlobalWorldSamplerQualityEditTests.cs.meta` passed with only line-ending warnings.
- Unity/dotnet compile was not run because project instructions forbid unnecessary builds during parallel agent work.

## 2026-05-26 - Terrain Repair Pass 2: Event Revision and Read Purity

What was wrong:
- `TerrainChunkGeneratedSignal.CacheRevision` existed but tile-applied publication always emitted `0`, so the signal could not prove native height payload freshness.
- `TryGetActiveTileCache` touched `LastAccessFrame` from read paths. That violates project doctrine: `TryGet*` and read models must not mutate global/cache state.
- Online check confirmed MapMagic is a runtime-capable infinite terrain tool, but Unity Terrain delayed write APIs still require explicit sync points. Reddit/community evidence reinforces chunk-generation stutter risk; it was not used as code authority.

What was done:
- Updated `Assets/_Project/Scripts/MapMagicBridge.cs` so terrain chunk signals copy `CacheRevision` and heightmap resolution from the existing `QuantizedHeightmapPayload` when available.
- Added `TerrainChunkGeneratedFlagHeightPayloadResolved` to distinguish signals with a native payload revision.
- Updated `Assets/_Project/Scripts/World/VegetationTileCacheResidency.cs` so active tile cache reads do not touch LRU unless `touchAccess: true` is passed.
- Removed the extra public read touch from `TryGetActiveHeightTexturePayload`.
- Marked owner build phases in `VegetationChunkResidencyDirector.cs` and `VegetationNavGridSynchronizer.cs` with `touchAccess: true`.
- Added `Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs` plus `.meta` to lock the event revision and read-purity contracts.

Cinematic Cheats used:
- No new visual fake. This was contract repair. Existing strategy remains: use cheap visual terrain masks and cached R16 payloads before expensive Unity Terrain mutation.

Exact Microseconds saved:
- Not measured. No Unity profiler run.
- Expected direct runtime saving is small; main gain is correctness and predictable cache residency.
- Avoided hidden write per public terrain cache read; on AI/physics-heavy frames this prevents unplanned LRU churn, but exact i3/MX350 savings require profiler capture.

External evidence:
- Unity `SetHeightsDelayLOD` docs require delayed terrain LOD/vegetation synchronization after edits.
- Unity support article states immediate `SetHeights` recalculates LOD each call and recommends delayed editing APIs for interactive modification.
- Unity `AsyncGPUReadbackRequest` docs confirm completed request data is frame-lifetime constrained and must be read after `done` and `!hasError`.
- MapMagic 2 listing/Fab description confirms MapMagic is node-based and can generate endless/playmode terrain, but local bridge currently disables the MapMagic object in play mode, so project runtime ownership still needs first-party clarification.

Verification:
- `git diff --check` on all touched terrain/test files passed with only line-ending warnings.
- Unity/dotnet compile was not run because parallel project instructions warn against unnecessary builds.

## 2026-05-26 - Terrain Repair Pass 3: Purity, SDF Faults, Mock Truth

What was wrong:
- Terrain seam jobs used runtime reflection and boxed Burst job structs to inject `GlobalQualityWeight`.
- `MapMagicRuntimeBridge` public terrain read APIs mutated `_lastResolvedTerrainTile` and polled `GlobalRegistry` through `ActiveRuntimeInstance`.
- Biome alpha texture cache could allocate from read-like biome/splat paths instead of owner phase.
- Voxel SDF density could carry NaN/infinity into density buffers, quantized density, marching cubes, collider upload, and nav-grid build.
- `TerrainChunkPagerRuntime.forceMockDiskIo` defaulted to true while real terrain sidecars are absent, allowing mock payloads to become silent terrain truth.
- Terrain authority docs were split and stale.

What was done:
- Replaced seam reflection/boxing with direct `GlobalQualityWeight` field assignment.
- Cached `HectonMapMagicVegetationBridge` in `MapMagicRuntimeBridge` through cold registration/hot-swap; public reads now use the cached field.
- Moved `_lastResolvedTerrainTile` mutation and biome alpha texture prewarm to owner phase (`Start`/`SlowTick`), while `FindTerrainAt` remains read-only.
- Added `DensityFaultFlags` scratch lane, finite guards in `VoxelDensityJob` and `VoxelDensityQuantizeJob`, and black-box reporting via `ReportVoxelInvalidDensityField`.
- Changed terrain pager mock IO default to false and forced mock IO to editor/development builds only.
- Added `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`.
- Expanded editor source-contract tests in `TerrainChunkSignalContractEditTests.cs`.

Cinematic Cheats used:
- Voxel invalid density fails closed to deterministic zero density instead of attempting an expensive recovery simulation.
- Terrain pager mock remains a dev/test cheat only; production terrain truth must come from real payloads.
- MapMagic remains suitable as authoring/bake/playmode source, but hot read truth is cached payload/adaptor state, not live graph polling.

Exact Microseconds saved:
- Not measured. No Unity profiler run.
- Removed reflection/boxing from seam job setup.
- Removed hidden cache mutation and GlobalRegistry polling from MapMagic terrain reads.
- Added finite checks to SDF density; this is a small ALU cost bought to prevent corrupted mesh/collider cascades.
- Mock IO gate has no frame cost.

External evidence:
- Unity `SetHeightsDelayLOD` docs require `SyncHeightmap` after delayed height edits.
- Unity `SetHolesDelayLOD` docs require `SyncTexture(TerrainData.HolesTextureName)` after delayed holes edits.
- Unity Addressables docs state remote/local catalogs and bundles are the supported route for updateable content; terrain sidecars still need a validated manifest or Addressables group.
- MapMagic public listing confirms runtime/playmode endless terrain generation exists, but local production bridge still disables live MapMagic generation and must not treat MapMagic as hot truth without explicit route proof.
- Reddit/community material was treated only as anecdotal risk evidence for terrain streaming/update pitfalls, not as code authority.

Verification:
- `git diff --check -- Assets/_Project/Scripts/Plugins/MapMagic/MapMagicRuntimeBridge.cs Assets/_Project/Scripts/HectonVoxelEngine.cs Assets/_Project/Scripts/World/TerrainChunkPagerRuntime.cs Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs` passed with only LF/CRLF warnings.
- New untracked docs/test files passed a trailing-whitespace scan.
- Targeted `rg` gates confirmed no `ActiveRuntimeInstance` polling in `MapMagicRuntimeBridge`, no seam `FieldInfo`/`SetValue`/boxed injection, and mock terrain pager no longer defaults to true.
- Unity/dotnet compile was not run because CPU sample was 100%, and project instructions forbid launching builds under load.

## 2026-05-26 - Terrain Repair Pass 4: Payload Schema, Splat Cache, Mask Hydration

What was wrong:
- Terrain chunk sidecar validation accepted any non-zero header version and did not reject unknown flags.
- `TerrainChunkPagerTuningDTO.CreateDefault()` still carried the obsolete mock request marker.
- `MapMagicRuntimeBridge.TryGetTerrainSplatColor` read `terrainData.terrainLayers` from the public terrain read path.
- `HectonMapMagicVegetationBridge.CacheTileMasks` resolved alphamap `GetPixelData<Color32>` inside per-texel sampling. A 512x512 mask build could call it up to 786432 times for sand, green-sand, and rock.

What was done:
- Added strict chunk file schema constants: `FileVersion = 1`, `FileFlagsMask = 0`.
- `TryValidateChunkHeader` now rejects non-v1 files and any unknown header flags.
- Terrain pager defaults now use `Flags = 0u`; sanitize keeps only the development force-mock lane.
- MapMagic splat reads now use cached `TerrainLayer[]` handles prewarmed in owner phase; cold reads use deterministic fallback colors instead of pulling the array property.
- Vegetation tile mask build now creates local `TerrainLayerMaskSampler` values once per sampled layer, then samples those aliases through the tile scan.
- Expanded `TerrainChunkSignalContractEditTests.cs` with source gates for schema validation, mock defaults, splat read purity, and per-layer alphamap aliasing.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md` with the strict sidecar schema, hot read rule, and alphamap alias rule.

Cinematic Cheats used:
- No new physical simulation. The terrain path stays on cached masks and fallback palette when layer handles are cold.
- Mock sector data remains a development cheat only, not production terrain truth.

Exact Microseconds saved:
- Not measured. Unity profiler was not run.
- Removed one possible array-property read from every MapMagic splat color query.
- Avoided up to 786432 `GetPixelData<Color32>` alias resolutions per 512x512 vegetation mask build.
- Header/mock fixes are correctness-only and add scalar validation cost only on chunk file ingest.

Regression model:
- CPU: lower during tile mask hydration; unchanged in steady-state height reads.
- GC: expected lower risk from removing splat read array-property access; measured proof absent.
- Memory: one cached `TerrainLayer[]` per active MapMagic bridge terrain data, owner-phase only.
- Cadence: no new Tick/SlowTick cadence changes.
- Correctness: unknown terrain payload versions and flags fail closed.
- Failure modes: cold layer cache falls back to deterministic colors; missing/old sidecars emit invalid-header/missing-file route instead of mock truth.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/TerrainChunkPagerTypes.cs Assets/_Project/Scripts/World/TerrainChunkPagerRuntime.cs Assets/_Project/Scripts/Plugins/MapMagic/MapMagicRuntimeBridge.cs Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md` passed.
- Targeted gates passed: forbidden terrain pager patterns absent; MapMagic splat block has no `terrainData.terrainLayers`; vegetation per-sample method has no `GetPixelData<Color32>`; new docs/tests have no trailing whitespace.
- Unity/dotnet compile was not run because `dotnet` PID 32808 was active and CPU load sampled at 100%, above the project rule threshold.

## 2026-05-26 - Terrain Repair Pass 5: Biome Read Purity, Terrain Hole Scheduling

What was wrong:
- `BiomeTransitionManagerRuntime.TryReadSnapshot` and `TryReadTuning` were public read accessors but pulled `GlobalRegistry.DataVault` and rediscovered vault handles per call.
- `VegetationTerrainHoleSynchronizer.TryScheduleTerrainHoleJobs` used `TerrainHoleMaskBuildJob.Run(state.TerrainHoleMaskCount)` in `SlowTick`, so a holes texture sweep could execute synchronously on the main thread.
- `FinalizeCompletedTerrainHoleJobs` existed, but the terrain-hole mask path did not actually use it for scheduled mask generation.

What was done:
- Biome transition reads now use `ActiveRuntimeInstance`, `_vaultReady`, and cached `VaultGenerationHandle<T>` fields via `TryReadBiomeVaultBuffer`.
- Terrain-hole mask build now stores per-tile job snapshot/output/vault lock state and schedules `TerrainHoleMaskBuildJob` with `Schedule(..., TerrainHoleJobBatchSize)`.
- Terrain-hole scheduling is capped to one dirty tile per `SlowTick`; Unity `SetHolesDelayLOD` and `SyncTexture(TerrainData.HolesTextureName)` run only after late-frame non-blocking completion.
- Teardown now force-completes/release-cleans pending terrain-hole jobs before releasing tile native cache buffers.
- Expanded `TerrainChunkSignalContractEditTests.cs` with source gates for biome read-purity and async terrain-hole scheduling.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md` with the biome cached-handle rule and terrain-hole async rule.

Cinematic Cheats used:
- No physical cave/terrain collision rewrite in this pass. Terrain holes still use Unity TerrainData as the adapter, but mask construction is shifted off the main-thread SlowTick path.
- Low-tier behavior is throttled by one dirty tile per SlowTick; high/ultra can raise the budget later only with profiler proof.

Exact Microseconds saved:
- Not measured. Unity profiler was not run.
- Removed GlobalRegistry access and handle rediscovery from biome snapshot reads.
- Moved terrain-hole mask construction off SlowTick. A 513x513 holes texture has 263169 cells; the avoided main-thread cell loop scales by terrain-hole count.
- Unity hole apply/sync remains a main-thread adapter cost and still needs profiler capture.

Regression model:
- CPU: lower SlowTick spike risk when cave/wreck terrain holes are dirty.
- GC: no new managed per-frame allocation intended; persistent job snapshot uses existing H8Memory release path.
- Memory: one temporary `TerrainHoleRecord` snapshot and one vault write lock per scheduled dirty tile until late-frame completion.
- Cadence: dirty tile processing is deliberately bounded; multiple dirty tiles can take multiple SlowTicks.
- Correctness: biome read APIs now fail closed if no active runtime/vault-ready state exists.
- Failure modes: pending terrain-hole jobs are completed and released during tile teardown.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/Biomes/BiomeTransitionManagerRuntime.cs Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs Assets/_Project/Scripts/World/VegetationTerrainHoleSynchronizer.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gates passed: forbidden terrain-hole `.Run(state.TerrainHoleMaskCount)` absent; biome `TryReadSnapshot`/`TryReadTuning` blocks do not contain `GlobalRegistry.DataVault`; terrain-hole async source gates present.
- Unity/dotnet compile was not run because CPU load sampled at 65%, above the project rule threshold, despite no active `dotnet`/`csc` process.

## 2026-05-26 - Terrain Repair Pass 6: Vegetation Chunk Job Route

What was wrong:
- `VegetationChunkResidencyDirector.ScheduleChunkBuild` executed grass, kelp, and floating vegetation `IJobParallelFor.Run()` inside the residency path.
- `_chunkBuildJobs` and `FinalizeCompletedChunkBuilds()` existed, but chunk payload generation bypassed them and finalized payloads synchronously.
- Moving chunk jobs async without lifetime guards would let worker jobs read tile-native sand/rock/height aliases after tile cache eviction.

What was done:
- `ScheduleChunkBuild` now prepares terrain-hole and artificial-structure TempJob snapshots, schedules grass/kelp/floating jobs with `Schedule(..., DefaultJobBatchSize)`, combines handles, and stores outputs in `ChunkBuildJobState`.
- `FinalizeCompletedChunkBuilds` now waits for `JobHandle.IsCompleted`, completes with `forceComplete: false`, builds payloads, registers storage, and releases job records/snapshots.
- Tile-cache LRU skips tiles with in-flight chunk jobs.
- Tile teardown force-completes and discards in-flight chunk jobs before releasing tile-native buffers.
- Threat spatial refresh is gated while chunk jobs are in flight, preserving permanent echo visual influence without racing the threat writer.
- Expanded `TerrainChunkSignalContractEditTests.cs` with source gates for scheduled chunk generation and tile lifetime guards.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md` with the chunk-build async rule.

Cinematic Cheats used:
- No new physical simulation. Chunk residency now buys main-thread budget for denser high/ultra vegetation dressing instead of spending it on synchronous sample loops.
- Permanent echo influence stays a visual dressing input; writer refresh waits until in-flight chunk jobs clear.

Exact Microseconds saved:
- Not measured. Unity profiler was not run.
- Removed synchronous main-thread execution of grass/kelp/floating chunk sample loops from `SlowTick`.
- Teardown may still force-complete jobs, but normal LRU eviction avoids active-job tiles.

Regression model:
- CPU: lower SlowTick spike risk; worker job pressure increases during chunk hydration.
- GC: no new managed hot allocation intended; new state is native job arrays/snapshots released by `DisposeJobState`.
- Memory: per in-flight chunk holds grass/floating/kelp output arrays plus terrain-hole/artificial-structure snapshots until late-frame completion.
- Cadence: chunk payloads can become visible one late-frame later; selected active buffers rebuild after completed count > 0.
- Correctness: stale/canceled tile revisions re-enqueue instead of publishing payloads.
- Failure modes: tile teardown discards in-flight jobs before native buffer release; LRU skips active-job tiles; threat spatial refresh waits behind chunk jobs.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/VegetationChunkResidencyDirector.cs Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs Assets/_Project/Scripts/World/VegetationTileCacheResidency.cs Assets/_Project/Scripts/World/VegetationTerrainHoleSynchronizer.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md` passed with LF/CRLF warnings only.
- Targeted gates passed: no `grassJob.Run`, `kelpJob.Run`, or `floatingJob.Run`; scheduled chunk handles, late-frame `forceComplete: false`, tile in-flight job guards, and threat-refresh gating are present.
- Unity/dotnet compile was not run because CPU load sampled at 94-100%, above the project rule threshold; no active `dotnet`/`csc` process was found.

## 2026-05-26 - Terrain Repair Pass 7: Abyssal Flow Field Job Route

What was wrong:
- `VegetationFlowFieldIntegrator.ScheduleFlowFieldJob` still executed `BuildAbyssalFlowFieldJob.Run(_ecosystemThreatGridCellCount)` from `SlowTick`.
- The flow field was published to `VegetationEcosystemFlowField` in the same `SlowTick`, bypassing the existing late-frame completion route.
- Keeping it synchronous made the abyssal flow/vegetation routing path scale with the full threat-grid cell count on the main thread.

What was done:
- Added `FlowFieldJobState` to retain flow TempJob snapshots/output until completion.
- `ScheduleFlowFieldJob` now schedules `BuildAbyssalFlowFieldJob` with `Schedule(..., DefaultJobBatchSize)` and marks `_flowFieldScheduled = true`.
- `CompleteFlowFieldJob` now owns publication to `VegetationEcosystemFlowField`, releases TempJob arrays, and marks the flow field initialized only after publish succeeds.
- Flow teardown force-completes and releases pending flow state before releasing the persistent flow buffer.
- Expanded `TerrainChunkSignalContractEditTests.cs` with source gates for flow scheduling and late-frame publication.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md` with the flow-field async rule.

Cinematic Cheats used:
- No physical current simulation rewrite. This keeps the existing 2D abyssal flow-field cheat and moves its solve off the SlowTick main-thread path.
- Low tier keeps the same authoritative route with lower upstream cadence/fidelity; high/ultra can spend recovered main-thread budget on denser visual flow response and wake-biased flora without changing gameplay truth.

Exact Microseconds saved:
- Not measured. Unity profiler was not run.
- Removed synchronous main-thread execution of one full `BuildAbyssalFlowFieldJob` over `_ecosystemThreatGridCellCount` cells from `SlowTick`.
- Added late-frame copy/publish cost after job completion; exact cost requires profiler capture.

Regression model:
- CPU: lower SlowTick spike risk; worker job pressure increases while the flow solve is in flight.
- GC: no new managed hot allocation intended; new state is native TempJob arrays released by `DisposeFlowFieldJobState`.
- Memory: one in-flight flow solve holds flow chunks, density grid, attractor grid, nav support grid, and flow output until late-frame completion.
- Cadence: flow field can become visible one late-frame later.
- Correctness: `_flowFieldScheduled` already blocks threat spatial refresh, so the job's threat-grid read alias is not replaced while running.
- Failure modes: forced teardown completes/releases pending flow state before persistent flow buffer release.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gates passed: flow schedule block has no `job.Run(_ecosystemThreatGridCellCount)`, uses `Schedule(..., DefaultJobBatchSize)`, retains `FlowFieldJobState`, and publishes/releases only in completion.
- Unity/dotnet compile was not run because CPU load sampled at 94-100%, above the project rule threshold; no active `dotnet`/`csc` process was found.

## 2026-05-26 - Terrain Repair Pass 8: Abyssal Thermal Grid Job Route

What was wrong:
- `VegetationFlowFieldIntegrator.ScheduleThermalGridJob` executed `BuildAbyssalThermalGridJob.Run(_abyssalThermalGridCellCount)` and `BuildAbyssalFlowVolumeJob.Run(_abyssalThermalGridCellCount)` in `SlowTick`.
- `CompleteThermalGridJob` compared `currentFlowVolume` to `currentFlowVolume`, so biolume surge detection did not compare old-vs-new flow data.

What was done:
- Added `ThermalGridJobState` to retain thermal/flow TempJob outputs and sampling snapshots until completion.
- Thermal grid now schedules first; 3D flow volume schedules second with `thermalHandle` as an explicit dependency.
- Late-frame completion compares the previously published flow volume with the completed `FlowVolumeOutput`, then publishes thermal grid and flow volume.
- Thermal teardown force-completes and releases pending state before releasing persistent thermal/flow buffers.
- Expanded `TerrainChunkSignalContractEditTests.cs` with source gates for thermal scheduling, dependency, publication, release, and old-vs-new surge comparison.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md` with the dependent thermal/flow-volume job rule.

Cinematic Cheats used:
- Kept the existing grid-based thermal/current fake. No fluid simulation rewrite.
- Low tier can keep coarse or less frequent thermal updates; high/ultra can spend worker budget on richer thermal pockets and visible biolume response.

Exact Microseconds saved:
- Not measured. Unity profiler was not run.
- Removed synchronous main-thread execution of two `_abyssalThermalGridCellCount` loops from `SlowTick`.
- Added late-frame publish/copy after completion; exact cost requires profiler capture.

Regression model:
- CPU: lower SlowTick spike risk; worker job pressure increases during thermal/flow-volume solve.
- GC: no new managed hot allocation intended; new state is native TempJob arrays released by `DisposeThermalGridJobState`.
- Memory: one in-flight thermal solve holds threat chunks, attractor grid, density grid, thermal output, and flow-volume output until late-frame completion.
- Cadence: thermal/flow-volume data can become visible one late-frame later.
- Correctness: surge detection now compares previous published volume to new completed volume before publish.
- Failure modes: forced teardown completes/releases pending thermal state before persistent buffer release.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gates passed: thermal schedule block has no thermal/flow-volume `Run`, uses dependent `Schedule` handles, completion compares `previousFlowVolume` against `jobState.FlowVolumeOutput`, and state/docs/tests exist.
- Unity/dotnet compile was not run because CPU load sampled at 100%, above the project rule threshold; no active `dotnet`/`csc` process was found.

## 2026-05-26 - Terrain Repair Pass 9: Threat Propagation Job Route

What was wrong:
- `VegetationFlowFieldIntegrator.ScheduleThreatPropagationJob` still executed threat propagation and threat voxelization with `.Run()` in `SlowTick`.
- Flow-field and thermal-grid scheduling could execute later in the same `SlowTick`, reading the previous threat snapshot while a new threat solve had just been scheduled.

What was done:
- Added `ThreatPropagationJobState` to retain threat, compressed threat, echo, voxel, and sampling TempJob outputs until completion.
- Threat propagation now schedules first; threat voxelization schedules second with `propagationHandle` as an explicit dependency.
- Late-frame completion compares previous published echo flags with completed echo output, publishes all threat buffers, refreshes residency if permanent echoes changed, and updates the hotspot after publish.
- Flow-field and thermal-grid scheduling now wait while `_threatPropagationScheduled` is true.
- Threat teardown force-completes and releases pending state before releasing persistent threat buffers.
- Expanded `TerrainChunkSignalContractEditTests.cs` with source gates for threat scheduling, publish, echo comparison, release, and consumer scheduling order.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md` with the threat-first async rule.

Cinematic Cheats used:
- Kept the existing grid/voxel threat proxy. No animal-level or fluid-level simulation was added.
- Low tier can keep lower update cadence; high/ultra can spend worker budget on richer permanent-echo vegetation dressing and threat-biased flow visuals.

Exact Microseconds saved:
- Not measured. Unity profiler was not run.
- Removed synchronous main-thread execution of `_ecosystemThreatGridCellCount` and `_ecosystemThreatVoxelCellCount` loops from `SlowTick`.
- Added one-SlowTick latency for flow/thermal consumers when threat propagation was newly scheduled.

Regression model:
- CPU: lower SlowTick spike risk; worker job pressure increases during threat propagation.
- GC: no new managed hot allocation intended; new state is native TempJob arrays released by `DisposeThreatPropagationJobState`.
- Memory: one in-flight threat solve holds threat chunks, attractor grid, density grid, artificial structures, threat output, compressed output, echo output, and voxel output until late-frame completion.
- Cadence: flow/thermal can wait one SlowTick for the new threat snapshot instead of consuming stale threat immediately.
- Correctness: permanent echo invalidation now compares previous published echo flags to completed echo output before publish.
- Failure modes: forced teardown completes/releases pending threat state before persistent threat buffer release.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gates passed: no remaining `job.Run`, `voxelJob.Run`, or `flowVolumeJob.Run` in `VegetationFlowFieldIntegrator.cs`; threat schedule uses dependent `Schedule` handles; completion compares `previousEchoFlags` against `jobState.EchoOutput`; flow/thermal scheduling is gated behind `_threatPropagationScheduled`.
- Unity/dotnet compile was not run because CPU load sampled at 100%, above the project rule threshold; no active `dotnet`/`csc` process was found.

## 2026-05-26 - Terrain Repair Pass 10: Native Pool Defrag Runtime Kill

What was wrong:
- `VegetationMemoryPool.TryScheduleNativePoolDefrag` was reachable from runtime tick logic after idle/fragmentation gates.
- The route built defrag plans, allocated scratch/staging buffers, and could execute `DefragPoolJob.Run()` through `SchedulePoolDefrag`.
- This contradicted the streaming residency mandate: stop-the-world defrag and live chunk compaction are forbidden. An idle gate is not a frame-budget proof.

What was done:
- Added a dormant runtime gate: `RuntimeNativePoolDefragEnabled => false`.
- Removed the runtime schedule block that built defrag plans, allocated scratch pools, emitted defrag scheduled telemetry, and called `SchedulePoolDefrag`.
- Removed cold defrag staging allocation from `InitializeChunkPools`.
- Changed `SchedulePoolDefrag` so it releases move records and never executes a pool-copy job.
- Added source-contract coverage in `TerrainChunkSignalContractEditTests`.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`.

Cinematic Cheats used:
- Low: grow-only/free-list residency; fragmentation is diagnostic telemetry, not runtime compaction.
- Middle: same route with larger capacity/cadence budgets if hardware allows.
- High/Ultra: spend memory budget on larger pools and richer visible vegetation, not moving live native chunks.

Exact Microseconds saved:
- 0 measured. No profiler run.
- Static effect: removes a reachable full-pool memory copy route and one synchronous `IJob.Run()` compaction path from terrain vegetation residency.

Regression model:
- CPU: removes a potential full-pool copy spike from runtime vegetation residency.
- GC: no new managed hot allocation intended.
- Memory: defrag scratch staging is no longer allocated during chunk pool initialization.
- Cadence: residency remains free-list/grow-only; fragmentation pressure must be handled by capacity and telemetry.
- Correctness: no live native chunk offsets are rewritten by a compaction swap during streaming churn.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/VegetationMemoryPool.cs Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md` passed with LF/CRLF warnings only.
- Targeted gates passed: runtime schedule block has no `BuildPoolDefragPlan`, no `SchedulePoolDefrag`, no defrag-scheduled telemetry, and no `.Run(`.
- Targeted gates passed: `SchedulePoolDefrag` has no `.Run()` and no `TryAcquireChunkPoolWriteView`.
- Targeted gate passed: `InitializeChunkPools` no longer calls `EnsurePoolDefragStagingCapacity`.
- Unity/dotnet compile was not run because CPU load sampled at 96%, above the project rule threshold; no active `dotnet`/`csc` process was found.

## 2026-05-26 - Terrain Repair Pass 11: Voxel Active Runtime Identity

What was wrong:
- `HectonVoxelEngine.ActiveRuntimeInstance` returned `GlobalRegistry.VoxelEngine`.
- Runtime consumers in terrain-adjacent systems read this property for voxel volume, thermal, resource distribution, and damage/organic logic.
- That made active voxel identity a service-locator poll instead of an owner-local read accessor.

What was done:
- Added `s_activeRuntimeInstance` in `HectonVoxelEngine`.
- `ActiveRuntimeInstance` now returns the owner-local static pointer.
- `OnEnable` sets the pointer after cold Registry registration.
- static reset and teardown clear the pointer.
- Registry unregister remains guarded by `ReferenceEquals(GlobalRegistry.VoxelEngine, this)` so cold DI state stays consistent.
- Added source-contract coverage in `TerrainChunkSignalContractEditTests`.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`.

Cinematic Cheats used:
- None. This is authority-route cleanup, not a visual simulation change.

Exact Microseconds saved:
- 0 measured. No profiler run.
- Static effect: removes repeated `GlobalRegistry.VoxelEngine` reads from hot terrain/voxel consumers.

Regression model:
- CPU: lower service-locator read pressure in voxel consumers.
- GC: no new managed hot allocation.
- Memory: one static pointer.
- Correctness: cold Registry route remains registered/unregistered; hot identity read is owner-local.

Verification:
- `git diff --check -- Assets/_Project/Scripts/HectonVoxelEngine.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gates passed: `ActiveRuntimeInstance` reads `s_activeRuntimeInstance`; the old `GlobalRegistry.VoxelEngine` property is absent; OnEnable sets the pointer; reset/teardown clear it; Registry unregister is still guarded.
- Unity/dotnet compile was not run because CPU load sampled at 67%, above the project rule threshold; no active `dotnet`/`csc` process was found.

## 2026-05-26 - Terrain Repair Pass 12: MapMagic Vegetation Active Runtime Identity

What was wrong:
- `HectonMapMagicVegetationBridge.ActiveRuntimeInstance` returned `GlobalRegistry.MapMagicVegetation`.
- Common terrain/world consumers used that property or `WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge` for height payloads, scatter, thermal/resource integration, voxel nav, fauna, audio, and UI handoff.
- That made active terrain/vegetation identity a service-locator read instead of an owner-local read accessor.

What was done:
- Added `s_activeRuntimeInstance` in `HectonMapMagicVegetationBridge`.
- `ActiveRuntimeInstance` now returns the owner-local static pointer.
- `OnEnable` publishes the pointer after cold Registry registration.
- static reset and teardown clear the pointer.
- Registry unregister remains guarded by `ReferenceEquals(GlobalRegistry.MapMagicVegetation, this)`.
- `WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge` now resolves through `HectonMapMagicVegetationBridge.ActiveRuntimeInstance`, not the Registry slot.
- `MapMagicRuntimeBridge` seeds `_cachedVegetationBridge` through the owner-local active pointer.
- Added source-contract coverage in `TerrainChunkSignalContractEditTests`.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`.

Cinematic Cheats used:
- None. This is authority-route cleanup, not a physical simulation change.

Exact Microseconds saved:
- 0 measured. No profiler run.
- Static effect: removes repeated `GlobalRegistry.MapMagicVegetation` reads from the common terrain/vegetation resolver and MapMagic runtime bridge seed path.

Regression model:
- CPU: lower service-locator read pressure in terrain/vegetation consumers.
- GC: no new managed hot allocation.
- Memory: one static pointer.
- Correctness: active pointer clears before bridge teardown disposes native buffers, so consumers fail closed instead of reading a partially disposed bridge.
- Domain boundary: two direct non-terrain owner cache sites remain (`SuitHUDV4CanvasOverlay`, `TetherManager`); they were not edited in this pass because they are outside terrain ownership and look like dependency-cache phases.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs Assets/_Project/Scripts/WorldRuntimeReferenceUtility.cs Assets/_Project/Scripts/Plugins/MapMagic/MapMagicRuntimeBridge.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gates passed: `ActiveRuntimeInstance` reads `s_activeRuntimeInstance`; the old Registry-backed property is absent from runtime files; `WorldRuntimeReferenceUtility` and `MapMagicRuntimeBridge` use the owner-local pointer; Registry unregister is guarded.
- Unity/dotnet compile was not run because CPU load sampled at 84.8%, above the project rule threshold; no active `dotnet`/`csc` process was found.

## 2026-05-26 - Terrain Repair Pass 13: Tile Height Readback Bulk Copy

What was wrong:
- `VegetationTileCacheResidency.TryFinalizeTileHeightReadback` copied completed R16 height readback data into the DataVault height buffer with a C# loop.
- A 513x513 terrain heightmap means 263169 scalar `ushort` assignments during a tile readback finalizer.

What was done:
- Replaced the scalar loop with `NativeArray<ushort>.Copy(readbackData, 0, heightSamples, 0, pendingBuffer.HeightSampleCount)`.
- Kept the existing completion, error, length, write-lock, cache revision, and active-buffer swap rules.
- Added source-contract coverage in `TerrainChunkSignalContractEditTests`.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`.

Cinematic Cheats used:
- None. This is a data movement fix.

Exact Microseconds saved:
- 0 measured. No profiler run.
- Static effect: avoids up to 263169 managed element assignments for a 513x513 tile readback finalization.

Regression model:
- CPU: lower managed-loop overhead when a height readback completes.
- GC: no new managed hot allocation.
- Memory: no new buffer; same readback source and DataVault destination.
- Correctness: copy still happens only after `AsyncGPUReadbackRequest.done`, no error, and validated destination length. Cache revision behavior is unchanged.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs Assets/_Project/Scripts/World/VegetationTileCacheResidency.cs Assets/_Project/Scripts/WorldRuntimeReferenceUtility.cs Assets/_Project/Scripts/Plugins/MapMagic/MapMagicRuntimeBridge.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gate passed: the readback finalizer uses `NativeArray<ushort>.Copy` and no longer contains `heightSamples[i] = readbackData[i]`.
- Unity/dotnet compile was not run because CPU load sampled at 99.8%, above the project rule threshold; no active `dotnet`/`csc` process was found.

## 2026-05-26 - Terrain Repair Pass 14: Resource Distribution Active Runtime Identity

What was wrong:
- `ResourceDistributionDirector.ActiveRuntimeInstance` returned `GlobalRegistry.ResourceDistribution`.
- Thermal/geology/voxel/resource consumers that used the property were still reading active resource authority through a Registry-backed accessor.
- `WorldGenRegistrySmokeTester` also encoded the bad ownership model by expecting `GlobalRegistry.UnregisterResourceDistribution` to clear active runtime identity.

What was done:
- Added `s_activeRuntimeInstance` in `ResourceDistributionDirector`.
- `ActiveRuntimeInstance` now returns the director-owned static pointer.
- `OnEnable` publishes the pointer after cold Registry registration.
- static reset, `OnDisable`, and `OnDestroy` clear the pointer.
- Registry unregister remains guarded by `ReferenceEquals(GlobalRegistry.ResourceDistribution, this)`.
- `WorldGenRegistrySmokeTester` now validates release through lifecycle teardown (`DestroyImmediate`) instead of direct Registry unregister.
- Added source-contract coverage in `TerrainChunkSignalContractEditTests`.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`.

Cinematic Cheats used:
- None. This is authority-route cleanup, not simulation.

Exact Microseconds saved:
- 0 measured. No profiler run.
- Static effect: removes repeated `GlobalRegistry.ResourceDistribution` reads behind `ActiveRuntimeInstance` from terrain/geology consumers.

Regression model:
- CPU: lower service-locator read pressure in resource/geology/thermal consumers.
- GC: no new managed hot allocation.
- Memory: one static pointer.
- Correctness: active pointer clears before resource runtime buffers/services are released, so consumers fail closed during teardown.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/ResourceDistributionDirector.cs Assets/_Project/Scripts/World/WorldGenRegistrySmokeTester.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gate passed: `ResourceDistributionDirector.ActiveRuntimeInstance` reads `s_activeRuntimeInstance`; the old Registry-backed property is absent; lifecycle clear/unregister guards exist; resource smoke release uses `DestroyImmediate` instead of direct Registry unregister.
- Unity/dotnet compile was not run because CPU samples were 85.9%, 100%, and 97.7%, above the project rule threshold; no active `dotnet`/`csc` process was found.

## 2026-05-26 - Terrain Repair Pass 15: Geology Seam/Voxel Bridge Active Runtime Identity

What was wrong:
- `WorldGenerativeGeologyTerrainSeamApplier.ActiveRuntimeInstance` returned `GlobalRegistry.GeologyTerrainSeam`.
- `WorldGenerativeGeologyVoxelBridgeDirector.ActiveRuntimeInstance` returned `GlobalRegistry.GeologyVoxelBridge`.
- Worldgen smoke proof encoded direct Registry unregister as the way active geology owner state disappears.

What was done:
- Added `s_activeRuntimeInstance` to both terrain seam applier and geology voxel bridge director.
- Both `ActiveRuntimeInstance` properties now return owner-local static pointers.
- Seam applier publishes from `Awake`/`OnEnable` and clears before terrain restore/native seam disposal.
- Voxel bridge publishes during lifecycle registration and clears before pending request cancellation and volume clearing.
- Registry unregister remains guarded by matching Registry slot.
- Worldgen smoke proof now validates release through lifecycle destruction for seam and voxel bridge owners.
- Added source-contract coverage in `TerrainChunkSignalContractEditTests`.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`.

Cinematic Cheats used:
- None. This is authority-route cleanup.

Exact Microseconds saved:
- 0 measured. No profiler run.
- Static effect: removes repeated `GlobalRegistry.GeologyTerrainSeam` / `GlobalRegistry.GeologyVoxelBridge` reads behind active accessors.

Regression model:
- CPU: lower service-locator read pressure in seam/geology consumers.
- GC: no new managed hot allocation.
- Memory: two static pointers.
- Correctness: active identities fail closed before seam buffers, restored terrains, voxel volumes, and pending bridge requests are torn down.

Verification:
- `git diff --check -- Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs Assets/_Project/Scripts/World/WorldGenRegistrySmokeTester.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gate passed: seam and voxel bridge active runtime accessors read owner-local `s_activeRuntimeInstance`; old Registry-backed properties are absent; smoke release blocks use lifecycle `DestroyImmediate` instead of direct Registry unregister.
- Unity/dotnet compile was not run because CPU samples were 97.5%, 87.7%, and 78.6%, above the project rule threshold; no active `dotnet`/`csc` process was found.

## 2026-05-26 - Terrain Repair Pass 16: Procedural Field/Scatter Active Runtime Identity

What was wrong:
- `WorldProceduralFieldSampler.ActiveRuntimeInstance` returned `GlobalRegistry.ProceduralFieldSampler`.
- `WorldProceduralScatterDirector.ActiveRuntimeInstance` returned `GlobalRegistry.ProceduralScatter`.
- Field-sampler smoke proof still used direct Registry unregister as active-state release.

What was done:
- Added `s_activeRuntimeInstance` to the field sampler and scatter director.
- Both `ActiveRuntimeInstance` properties now return owner-local static pointers.
- Field sampler clears active state before sampling job barriers, Burst data disposal, and graphics buffer release.
- Scatter clears active state before backend disposal, GPUI visibility clear, and cell sampling array disposal.
- Editor reload teardown uses owner clear helpers instead of Registry-only unregister.
- Field-sampler smoke proof now validates lifecycle destruction for release.
- Added source-contract coverage in `TerrainChunkSignalContractEditTests`.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`.

Cinematic Cheats used:
- None. This is authority-route cleanup.

Exact Microseconds saved:
- 0 measured. No profiler run.
- Static effect: removes repeated `GlobalRegistry.ProceduralFieldSampler` / `GlobalRegistry.ProceduralScatter` reads behind active accessors.

Regression model:
- CPU: lower service-locator read pressure in procedural world consumers.
- GC: no new managed hot allocation.
- Memory: two static pointers.
- Correctness: active identities fail closed before field sampler buffers and scatter backend state are torn down.

Verification:
- `git diff --check -- Assets/_Project/Scripts/WorldProceduralFieldSampler.cs Assets/_Project/Scripts/WorldProceduralScatterDirector.cs Assets/_Project/Scripts/World/WorldGenRegistrySmokeTester.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gate passed: field sampler and scatter active runtime accessors read owner-local `s_activeRuntimeInstance`; old Registry-backed properties are absent; field-sampler smoke release uses lifecycle `DestroyImmediate`.
- Unity/dotnet compile was not run because CPU samples were 100%, 100%, and 100%, above the project rule threshold; no active `dotnet`/`csc` process was found.

## 2026-05-26 - Terrain Repair Pass 17: Voxel Anomaly Resource Binding Route

What was wrong:
- `HectonVoxelEngine.TryBindSelectedChthonicPillarResources` still read `GlobalRegistry.ResourceDistribution`.
- After resource director active identity moved owner-local, this left one terrain/voxel-owned anomaly binding path on the old Registry route.

What was done:
- Replaced the direct Registry read with `ResourceDistributionDirector.ActiveRuntimeInstance`.
- Added source-contract coverage in `TerrainChunkSignalContractEditTests`.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`.

Cinematic Cheats used:
- None. This is authority-route cleanup.

Exact Microseconds saved:
- 0 measured. No profiler run.
- Static effect: removes one direct resource Registry read from voxel anomaly-to-resource binding.

Regression model:
- CPU: lower service-locator read pressure in the voxel anomaly binding path.
- GC: no new managed hot allocation.
- Memory: no new memory.
- Correctness: voxel anomaly resource binding now follows the resource owner-local active route.

Verification:
- `git diff --check -- Assets/_Project/Scripts/HectonVoxelEngine.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gate passed: voxel anomaly binding uses `ResourceDistributionDirector.ActiveRuntimeInstance`; the old direct `GlobalRegistry.ResourceDistribution` binding assignment is absent from `HectonVoxelEngine`.
- Unity/dotnet compile was not run because CPU samples were 100%, 100%, and 100%, above the project rule threshold; no active `dotnet`/`csc` process was found.

## 2026-05-26 - Terrain Repair Pass 18: MapMagic Bridge Owner-Local Identity

What was wrong:
- `MapMagicBridge.Instance` returned `GlobalRegistry.MapMagic`.
- `WorldRuntimeReferenceUtility.TryResolveMapMagicBridge` returned `GlobalRegistry.MapMagic`.
- `HectonMapMagicVegetationBridge.ResolveRuntimeDependencies`, `VoxelSeamDirector`, and `ProceduralOreSpawner` still had direct MapMagic Registry read assignments.
- `VisualOmegaSmokeTester` encoded the obsolete Registry-facade contract for MapMagic.

What was done:
- Added `s_activeRuntimeInstance` to `MapMagicBridge`.
- `MapMagicBridge.Instance` now returns the owner-local active pointer.
- `MapMagicRuntimeBridge` publishes the owner-local pointer only after cold Registry registration succeeds and clears it during lifecycle teardown.
- `WorldRuntimeReferenceUtility.TryResolveMapMagicBridge` resolves through `MapMagicBridge.ActiveRuntimeInstance` and cached active bridge only.
- Terrain vegetation, voxel seam, and procedural ore binding routes no longer assign MapMagic through `GlobalRegistry.MapMagic`.
- Updated `VisualOmegaSmokeTester` to assert the owner-local MapMagic contract and fixed its expected check count.
- Added source-contract coverage in `TerrainChunkSignalContractEditTests`.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`.

Cinematic Cheats used:
- None. This is authority-route cleanup.

Exact Microseconds saved:
- 0 measured. No profiler run.
- Static effect: removes direct MapMagic Registry reads from the shared resolver plus terrain/seam/ore route assignments.

Regression model:
- CPU: lower service-locator pressure in MapMagic terrain consumers.
- GC: no new managed hot allocation.
- Memory: one static pointer in `MapMagicBridge`.
- Correctness: active MapMagic bridge identity now fails closed from owner lifecycle before consumers can read a stale Registry slot.

Verification:
- `git diff --check -- Assets/_Project/Scripts/MapMagicBridge.cs Assets/_Project/Scripts/Plugins/MapMagic/MapMagicRuntimeBridge.cs Assets/_Project/Scripts/WorldRuntimeReferenceUtility.cs Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs Assets/_Project/Scripts/VoxelSeamDirector.cs Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs Assets/_Project/Scripts/VisualOmegaSmokeTester.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md` passed with LF/CRLF warnings only.
- Targeted gate passed: no direct `return GlobalRegistry.MapMagic`, `target = GlobalRegistry.MapMagic`, `mapMagicBridge = GlobalRegistry.MapMagic`, or `MapMagicBridge mapMagicBridge = GlobalRegistry.MapMagic` assignments remain in the touched terrain route files.
- VisualOmega source check count is 36 and matches `ExpectedCheckCount`.
- Unity/dotnet compile was not run because CPU sampled at 96%, above the project rule threshold; no active `dotnet`/`csc` process was found.

## 2026-05-26 - Terrain Repair Pass 19: Procedural Wreck Voxel Owner Route

What was wrong:
- `ProceduralWreckGenerator.CacheRegistryServicesCold` still assigned `_voxelEngine = GlobalRegistry.VoxelEngine`.
- Wreck generation is an Echelon 2 worldgen consumer because placement can depend on voxel cave/terrain state.
- This left one direct voxel Registry read after the voxel active runtime identity was moved owner-local.

What was done:
- Replaced the direct Registry read with `WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref _voxelEngine)`.
- Added source-contract coverage in `TerrainChunkSignalContractEditTests`.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`.

Cinematic Cheats used:
- None. This is authority-route cleanup.

Exact Microseconds saved:
- 0 measured. No profiler run.
- Static effect: removes one direct voxel Registry read from procedural wreck generation.

Regression model:
- CPU: lower service-locator pressure in the wreck voxel dependency path.
- GC: no new managed hot allocation.
- Memory: no new memory.
- Correctness: wreck generation resolves voxel terrain truth through the voxel owner-local active route.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gate passed: `ProceduralWreckGenerator` no longer contains `_voxelEngine = GlobalRegistry.VoxelEngine` and resolves `_voxelEngine` through `WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref _voxelEngine)`.
- Unity/dotnet compile was not run because CPU sampled at 85%, above the project rule threshold; no active `dotnet`/`csc` process was found.

## 2026-05-26 - Terrain Repair Pass 20: Ore/Outpost Terrain Owner Route

What was wrong:
- `ProceduralOreSpawner.CacheRuntimeServices` still assigned `_terrainProvider = GlobalRegistry.Terrain`.
- `MarauderOutpostGenerationService.ResolveMapMagicBridge` still assigned `_cachedMapMagicBridge = GlobalRegistry.MapMagic`.
- Both systems are generation consumers of terrain/MapMagic state, so these reads kept a second terrain authority path alive.

What was done:
- Removed the direct terrain Registry fallback from `ProceduralOreSpawner`.
- Ore terrain binding now resolves MapMagic through `WorldRuntimeReferenceUtility.TryResolveMapMagicBridge` and uses that bridge as the terrain provider unless hotswap supplied another provider.
- Outpost terrain binding now resolves MapMagic through `WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref _cachedMapMagicBridge)`.
- Added source-contract coverage in `TerrainChunkSignalContractEditTests`.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`.

Cinematic Cheats used:
- None. This is authority-route cleanup.

Exact Microseconds saved:
- 0 measured. No profiler run.
- Static effect: removes one direct terrain Registry read and one direct MapMagic Registry read from generation consumers.

Regression model:
- CPU: lower service-locator pressure during terrain-dependent generation.
- GC: no new managed hot allocation.
- Memory: no new memory.
- Correctness: ore and outpost generation now consume the same owner-local MapMagic route as seam, vegetation, and wreck generation.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gate passed: `ProceduralOreSpawner` and `MarauderOutpostGenerationService` no longer contain direct `GlobalRegistry.Terrain` / `GlobalRegistry.MapMagic` assignments and both resolve MapMagic through `WorldRuntimeReferenceUtility.TryResolveMapMagicBridge`.
- Unity/dotnet compile was not run because CPU sampled at 51%, above the project rule threshold; no active `dotnet`/`csc` process was found.

## 2026-05-26 - Terrain Repair Pass 21: MapMagic Rock Output Staging

What was wrong:
- `HectonRockOutput.Finalize` used `Dictionary<int, List<Matrix4x4>>` as per-tile/per-layer staging.
- Each layer then called `ToArray()`, copying all matrices into the final apply DTO.
- If rock output is enabled in a runtime or bake graph, this creates avoidable managed staging and copy pressure.

What was done:
- Added `LayerBuildState` for count/write-index tracking.
- First pass counts accepted transforms per layer.
- The apply DTO now allocates exact `Matrix4x4[]` arrays directly.
- Second pass fills those arrays without `List<Matrix4x4>` and without `ToArray()`.
- Added source-contract coverage in `TerrainChunkSignalContractEditTests`.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`.

Cinematic Cheats used:
- None. This is data staging cleanup.

Exact Microseconds saved:
- 0 measured. No profiler run.
- Static effect: removes one managed list allocation per rock layer and one full matrix-array copy per layer from MapMagic rock output finalization.

Regression model:
- CPU: less managed copy work in rock output finalize.
- GC: fewer managed list allocations and discarded backing arrays.
- Memory: final arrays remain because `HectonRockManager` contract consumes them; staging arrays are removed.
- Correctness: apply DTO shape is unchanged.

Verification:
- `git diff --check -- Assets/_Project/Scripts/Plugins/MapMagic/HectonRockOutput.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gate passed: `HectonRockOutput.Finalize` uses `LayerBuildState` count/fill exact arrays and no longer contains `Dictionary<int, List<Matrix4x4>>`, `new List<Matrix4x4>`, or `.ToArray()` in the finalize block.
- Unity/dotnet compile was not run because CPU sampled at 62%, above the project rule threshold; no active `dotnet`/`csc` process was found.

## 2026-05-27 - Terrain Repair Pass 22: MapMagic Rock Output Replace Semantics

What was wrong:
- `HectonRockOutput.Apply` registered non-empty layer arrays but only unregistered a chunk when the whole payload dictionary was empty.
- `HectonRockManager.RegisterChunk` ignores zero-length arrays.
- Result: if a chunk kept one rock layer but another layer became empty, the old empty-layer matrices could stay resident.

What was done:
- `HectonRockOutput.Finalize` now creates payload entries only for layers with `Count > 0`.
- `HectonRockOutput.Apply` now calls `manager.UnregisterChunk(chunkCoord)` before registering the new non-empty layer payloads.
- Added source-contract coverage for active-layer filtering and unregister-before-register order.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`.

Cinematic Cheats used:
- None. This is residency correctness.

Exact Microseconds saved:
- 0 measured. No profiler run.
- Static cost: one chunk unregister enumeration per rock apply.
- Static gain: stale rock instances are removed when a layer disappears from a chunk.

Regression model:
- CPU: small apply-time cost from unregister-before-register.
- GC: no new managed hot allocation.
- Memory: stale layer matrices are released from manager chunk maps instead of being retained.
- Correctness: rock output apply now has full chunk replacement semantics.

Verification:
- `git diff --check -- Assets/_Project/Scripts/Plugins/MapMagic/HectonRockOutput.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gate passed: `HectonRockOutput.Finalize` filters to positive `activeLayerCount`, contains no empty `Array.Empty<Matrix4x4>()` payload, and `Apply` unregisters the old chunk before the null/empty payload return and before registering new layer matrices.
- Unity/dotnet compile was not run because CPU sampled at 100%, above the project rule threshold; no active `dotnet`/`csc` process was found.

## 2026-05-27 - Terrain Repair Pass 23: Rock Output Manager Owner Route

What was wrong:
- `HectonRockOutput.Apply` read `GlobalRegistry.RockManager`.
- `HectonRockOutput.ClearApplied` read `GlobalRegistry.RockManager`.
- `HectonRockManager` already has an owner-local active pointer, so MapMagic rock output was still using Registry as an active dependency lookup.

What was done:
- `Apply` now resolves through `HectonRockManager.Instance`.
- `ClearApplied` now resolves through `HectonRockManager.Instance`.
- Added source-contract coverage for apply/clear manager route.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`.

Cinematic Cheats used:
- None. This is authority-route cleanup.

Exact Microseconds saved:
- 0 measured. No profiler run.
- Static effect: removes two direct Registry reads from rock output apply/clear.

Regression model:
- CPU: lower service-locator pressure in MapMagic rock output apply/clear.
- GC: no new managed hot allocation.
- Memory: no new memory.
- Correctness: rock manager active identity now comes from its owner-local pointer.

Verification:
- `git diff --check -- Assets/_Project/Scripts/Plugins/MapMagic/HectonRockOutput.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gate passed: `HectonRockOutput.Apply` and `ClearApplied` resolve `HectonRockManager.Instance` and no longer contain `HectonRockManager manager = GlobalRegistry.RockManager`.
- Unity/dotnet compile was not run: repo root has no `.sln`/`.csproj` CLI compile target, and the follow-up CPU sample rose to 82%, above the project rule threshold; no active `dotnet`/`csc` process was found.

## 2026-05-27 - Terrain Repair Pass 24: Rock Manager Late Layer Allocation

What was wrong:
- `HectonRockManager.RegisterChunk` accepted unknown MapMagic rock layer ids.
- Unknown layers allocated a late `Dictionary<Vector2Int, Matrix4x4[]>` and a full `Matrix4x4[_instanceCapacity]` aggregation buffer on the active chunk registration route.
- This hid authoring errors and could reserve a large managed matrix array during terrain decoration streaming.

What was done:
- `RegisterChunk` now requires `_prototypeLookup` to contain the layer id.
- Missing chunk maps or aggregation count/capacity buffers now fail closed through `LogMissingLayerBuffer`.
- Added one-shot dev/editor `LogUnknownRockLayer`.
- Added source-contract coverage for no late dictionary/matrix-buffer allocation in `RegisterChunk`.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`.

Cinematic Cheats used:
- Unknown decoration layers are dropped instead of creating new runtime truth or heavy fallback geometry. Authored GPUI layers remain the only rock decoration route.

Exact Microseconds saved:
- 0 measured. Profiler absent.
- Static impact: prevents one late chunk dictionary plus one full matrix aggregation array per unexpected layer id. With current default capacity, this avoids a `Matrix4x4[120000]` failure-path allocation per bad rock layer.

Regression model:
- CPU: fewer failure-path allocations and no late layer map setup.
- GC: removes managed allocation route from runtime chunk registration failure path.
- Memory: unknown layers no longer reserve aggregation buffers.
- Cadence: no change.
- Correctness: misconfigured layer ids fail visibly in dev/editor instead of silently occupying manager state.

Verification:
- `git diff --check -- Assets/_Project/Scripts/HectonRockManager.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gate passed: `HectonRockManager.RegisterChunk` requires a configured prototype layer, contains no late `Dictionary<Vector2Int, Matrix4x4[]>` / `Matrix4x4[_instanceCapacity]` allocation in the registration block, and logs unknown layers through a one-shot dev/editor path.
- Source-contract gate passed: `TerrainChunkSignalContractEditTests` contains `HectonRockManagerRegisterChunk_FailsClosedForUnconfiguredLayers`.
- Unity/dotnet compile was not run: repo root has no `.sln`/`.csproj` CLI compile target, no active `dotnet`/`csc` process was found, and CPU sampled at 76.4%, above the project rule threshold.

## 2026-05-27 - Terrain Repair Pass 25: MapMagic Generator Seed Snapshot

What was wrong:
- `HectonSandboxAbyssalShelfMapMagicNode.ResolveRuntimeWorldSeed` read `GlobalRegistry.WorldSeedProvider`.
- `HectonSpaceEngine098MapMagicUtility.ResolveSeed` read `GlobalRegistry.WorldSeedProvider`.
- These calls sit in MapMagic generator/seed utility paths, so they are not clean cold-DI. Terrain generation seed identity was still pulled through the global service slot.

What was done:
- `HectonWorldGenerator` now publishes an internal active runtime seed snapshot when it successfully registers as the world seed provider.
- Snapshot is cleared on static reset, disable, and destroy.
- MapMagic sandbox shelf and SpaceEngine098 nodes read `global::HectonWorldGenerator.TryGetActiveRuntimeWorldSeed`.
- Added source-contract coverage forbidding `GlobalRegistry.WorldSeedProvider` in those MapMagic generator nodes.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`.

Cinematic Cheats used:
- None. This is deterministic terrain identity routing.

Exact Microseconds saved:
- 0 measured. Profiler absent.
- Static impact: removes two global service reads from MapMagic generator seed paths. Cost is now one scalar snapshot read and one branch.

Regression model:
- CPU: lower service-locator pressure in terrain generator seed resolution.
- GC: no new managed hot allocation.
- Memory: three static scalar/reference fields on `HectonWorldGenerator`.
- Cadence: no change.
- Correctness: MapMagic terrain seed remains mixed with runtime world seed when the world generator owner is active; absent owner still falls back to authored node seed.

Verification:
- `git diff --check -- Assets/_Project/Scripts/HectonWorldGenerator.cs Assets/_Project/Scripts/Plugins/MapMagic/HectonSandboxAbyssalShelfMapMagicNode.cs Assets/_Project/Scripts/Plugins/MapMagic/HectonSpaceEngine098MapMagicNodes.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gate passed: `HectonWorldGenerator` exposes owner-local runtime seed snapshot state; MapMagic sandbox shelf and SpaceEngine098 generator seed paths call `global::HectonWorldGenerator.TryGetActiveRuntimeWorldSeed`; neither node contains `GlobalRegistry.WorldSeedProvider`.
- Trailing-whitespace scan over touched files returned no matches.
- Unity/dotnet compile was not run: repo root has no `.sln`/`.csproj` CLI compile target; no active `dotnet`/`csc` process was found; CPU sampled at 31.9%.

## 2026-05-27 - Terrain Repair Pass 26: Outpost Generation Seed Snapshot

What was wrong:
- `MarauderOutpostGenerationService.ResolveWorldSeedProvider` rediscovered `GlobalRegistry.WorldSeedProvider`.
- The outpost generator already used owner-local MapMagic terrain resolution, but its seed identity still had a second Registry-backed route.

What was done:
- `ResolveWorldSeed` now reads `global::HectonWorldGenerator.TryGetActiveRuntimeWorldSeed` first.
- The hotswap-injected `_cachedWorldSeedProvider` remains a fallback only.
- Removed the direct Registry polling helper.
- Expanded source-contract coverage and the terrain route card.

Cinematic Cheats used:
- None. This is deterministic worldgen identity routing.

Exact Microseconds saved:
- 0 measured. Profiler absent.
- Static impact: removes one Registry service read from outpost generation seed resolution.

Regression model:
- CPU: lower service-locator pressure during outpost generation requests.
- GC: no new managed allocation.
- Memory: no new memory.
- Cadence: no change.
- Correctness: outpost generation shares the same runtime seed owner as MapMagic terrain generation.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gate passed: `MarauderOutpostGenerationService` reads `global::HectonWorldGenerator.TryGetActiveRuntimeWorldSeed`, contains no `GlobalRegistry.WorldSeedProvider`, has no `ResolveWorldSeedProvider`, and retains `_cachedWorldSeedProvider` only as fallback.
- Trailing-whitespace scan over touched files returned no matches.
- Unity/dotnet compile was not run: repo root has no `.sln`/`.csproj` CLI compile target; no active `dotnet`/`csc` process was found; CPU sampled at 31%.

## 2026-05-27 - Terrain Repair Pass 27: Biome Matrix Terrain Owner Route

What was wrong:
- `BiomeMatrixDirector.CacheRuntimeDependencies` used `_resolvedTerrainProvider ??= GlobalRegistry.Terrain`.
- The director evaluates biome depth and seismic dust placement from terrain height, so this was another active terrain consumer with a Registry-backed fallback.

What was done:
- Preserved hotswap-injected `ITerrainProvider` as the primary route.
- Replaced direct terrain Registry fallback with `WorldRuntimeReferenceUtility.TryResolveMapMagicBridge`.
- Expanded source-contract coverage and the terrain route card.

Cinematic Cheats used:
- None. This is terrain authority routing for biome/depth evaluation.

Exact Microseconds saved:
- 0 measured. Profiler absent.
- Static impact: removes one direct `GlobalRegistry.Terrain` read from biome matrix runtime dependency caching.

Regression model:
- CPU: lower service-locator pressure in biome matrix dependency refresh.
- GC: no new managed allocation.
- Memory: no new memory.
- Cadence: no change.
- Correctness: biome depth and seismic dust use the same active terrain owner route as ore/outpost generation.

Verification:
- `git diff --check -- Assets/_Project/Scripts/BiomeMatrixDirector.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gate passed: `BiomeMatrixDirector` resolves fallback terrain through `WorldRuntimeReferenceUtility.TryResolveMapMagicBridge`, no longer contains `_resolvedTerrainProvider ??= GlobalRegistry.Terrain;`, and source-contract coverage locks both conditions.
- Trailing-whitespace scan over touched files returned no matches.
- Unity/dotnet compile was not run: repo root has no `.sln`/`.csproj` CLI compile target; no active `dotnet`/`csc` process was found; CPU sampled at 93.4%, above the project rule threshold.

## 2026-05-27 - Terrain Repair Pass 28: Ecosystem Terrain/Resource Owner Route

What was wrong:
- `EcosystemDirector` sampled MapMagic terrain/water through `GlobalRegistry.MapMagic` in envelope building, apex spawn gate, and depth resolution.
- Mutation scalar sampling read brine/resource truth through `GlobalRegistry.ResourceDistribution`.

What was done:
- Replaced MapMagic Registry reads with `MapMagicBridge.Instance`.
- Replaced resource Registry read with `ResourceDistributionDirector.ActiveRuntimeInstance`.
- Expanded source-contract coverage and the terrain route card.

Cinematic Cheats used:
- None. This is cross-domain owner routing for terrain/resource truth.

Exact Microseconds saved:
- 0 measured. Profiler absent.
- Static impact: removes three direct MapMagic Registry reads and one ResourceDistribution Registry read from ecosystem terrain/resource consumers.

Regression model:
- CPU: lower service-locator pressure in ecosystem spawn/depth/mutation paths.
- GC: no new managed allocation.
- Memory: no new memory.
- Cadence: no change.
- Correctness: ecosystem consumers now read the same terrain/resource owner routes as terrain and geology systems.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/EcosystemDirector.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gate passed: `EcosystemDirector` contains no `GlobalRegistry.MapMagic` or `GlobalRegistry.ResourceDistribution`, uses `MapMagicBridge.Instance` for terrain/water sampling, uses `ResourceDistributionDirector.ActiveRuntimeInstance` for brine/resource sampling, and source-contract coverage locks those routes.
- Trailing-whitespace scan over touched files returned no matches.
- Unity/dotnet compile was not run: repo root has no `.sln`/`.csproj` CLI compile target; no active `dotnet`/`csc` process was found; CPU sampled at 53%, above the project rule threshold.

## 2026-05-27 - Terrain Repair Pass 29: Anomaly Resource Binding Owner Route

What was wrong:
- `HectonAnomalyResourceBinding` read `GlobalRegistry.ResourceDistribution`.
- Source search shows `HectonAnomalyMapMagicNode` calls this utility, so it is part of the MapMagic anomaly/resource bridge, not a harmless isolated diagnostic.

What was done:
- Replaced the Registry read with `ResourceDistributionDirector.ActiveRuntimeInstance`.
- Removed the now-unused `Hecton8.Core` using.
- Expanded source-contract coverage and the terrain route card.

Cinematic Cheats used:
- None. This is resource owner routing for anomaly-generated resource anchors.

Exact Microseconds saved:
- 0 measured. Profiler absent.
- Static impact: removes one direct ResourceDistribution Registry read from anomaly-to-resource binding.

Regression model:
- CPU: lower service-locator pressure in anomaly resource binding.
- GC: no new managed allocation.
- Memory: no new memory.
- Cadence: no change.
- Correctness: MapMagic anomaly resource anchors now use the same owner-local resource distribution route as voxel and ecosystem consumers.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/HectonAnomalyResourceBinding.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gate passed: `HectonAnomalyResourceBinding` reads `ResourceDistributionDirector.ActiveRuntimeInstance`, contains no direct `GlobalRegistry.ResourceDistribution`, removed the unused `Hecton8.Core` import, and source-contract coverage locks the anomaly binding route.
- Trailing-whitespace scan over touched files returned no matches.
- Unity/dotnet compile was not run: repo root has no `.sln`/`.csproj` CLI compile target; no active `dotnet`/`csc` process was found; CPU sampled at 43.5%.

## 2026-05-27 - Terrain Repair Pass 30: Ground Radar Ore/Voxel Owner Route

What was wrong:
- `GroundPenetratingRadarRuntime` bound ore scan data through `GlobalRegistry.WorldResourceSpawner`.
- The same runtime bound voxel sonar SDF through `GlobalRegistry.VoxelSonarSdf`.
- GPR schedules jobs over ore SoA and voxel SDF payloads, so this is an active Echelon 2 world/terrain dependency, not cold bootstrap only.

What was done:
- Added owner-local `ProceduralOreSpawner.ActiveRuntimeInstance`.
- Added `WorldRuntimeReferenceUtility.TryResolveWorldResourceSpawnerReadModel`.
- Replaced GPR ore fallback binding with the owner-local resolver.
- Replaced GPR voxel SDF startup binding with the existing voxel owner route.
- Added source-contract coverage for the ore/GPR/voxel route.

Cinematic Cheats used:
- None. This is dependency authority routing for GPR ore/SDF scan truth.

Exact Microseconds saved:
- 0 measured. Profiler absent.
- Static impact: removes two direct Registry reads from GPR startup dependency binding and reduces stale ore/SDF owner risk during teardown.

Regression model:
- CPU: lower service-locator pressure during GPR enable/rebind.
- GC: no new managed allocation.
- Memory: one static owner pointer.
- Cadence: no change.
- Correctness: GPR reads the same ore spawner and voxel owner routes as worldgen/terrain consumers.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs Assets/_Project/Scripts/WorldRuntimeReferenceUtility.cs Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md Docs/AgentLogs/LOG_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted gate passed: `ProceduralOreSpawner` owns `ActiveRuntimeInstance`, `WorldRuntimeReferenceUtility.TryResolveWorldResourceSpawnerReadModel` resolves the owner-local pointer without `GlobalRegistry.WorldResourceSpawner`, and `GroundPenetratingRadarRuntime` no longer assigns from `GlobalRegistry.WorldResourceSpawner` or `GlobalRegistry.VoxelSonarSdf`.
- Route card updated with the GPR ore/SDF owner route.
- Trailing-whitespace scan over touched files returned no matches.
- Unity/dotnet compile was not run: repo root has no `.sln`/`.csproj` CLI compile target; no active `dotnet`/`csc` process was found; CPU sampled at 85%, above the project rule threshold.

## 2026-05-27 - Terrain Repair Pass 31: Vegetation Flow Job Completion Route

What was wrong:
- `VegetationFlowFieldIntegrator` still ran threat propagation, threat voxelization, abyssal flow-field, thermal grid, and flow-volume jobs synchronously with `.Run()` inside `SlowTick`.
- The same file had `CompleteThreatPropagationJob`, `CompleteFlowFieldJob`, and `CompleteThermalGridJob` stubs that only cleared flags.
- Existing source tests and route card already claimed scheduled late-frame publication, but source did not match that claim.
- `WorldProceduralScatterDirector.ResolveReferences` still borrowed the rock GPUI manager through `GlobalRegistry.RockManager`.

What was done:
- Added short-lived `ThreatPropagationJobState`, `FlowFieldJobState`, and `ThermalGridJobState`.
- Threat propagation now schedules first; threat voxelization consumes the threat output through a `JobHandle` dependency.
- Flow-field generation now schedules from `SlowTick` and publishes only after `DispatcherJobSwap.TryComplete` in `LateFrameTick` or forced teardown.
- Thermal grid generation now schedules first; 3D flow-volume consumes the thermal output through a `JobHandle` dependency.
- TempJob snapshots and outputs are released after completion/publication through explicit dispose helpers.
- Scatter now reads `HectonRockManager.Instance`, not `GlobalRegistry.RockManager`.
- Source-contract coverage and the terrain route card were expanded for the scatter rock route.

Cinematic Cheats used:
- No new simulation. This is scheduling/authority repair. The retained cinematic cheat is the existing grid-field approximation: AI/current/biolume read coarse flow/threat fields instead of simulating individual water/organism particles.

Exact Microseconds saved:
- 0 measured. Profiler absent.
- Static impact: removes five full-grid synchronous job executions from `SlowTick` and moves publication to the dispatcher-owned completion window.

Regression model:
- CPU: lower main-thread SlowTick stall risk; worker scheduling cost moves to Burst jobs with late-frame completion.
- GC: no new managed allocation in hot path; added structs store TempJob aliases until completion.
- Memory: short-lived TempJob outputs may live across frames until completion; teardown force-completes and releases them.
- Cadence: introduces intended one-late-frame publication latency for threat/flow/thermal snapshots.
- Correctness: DataVault snapshots publish only after completed outputs; previous-vs-new echo/flow comparisons remain before publication.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs Assets/_Project/Scripts/WorldProceduralScatterDirector.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md Docs/Tasks/Status_TERRAIN_AUDIT.md Docs/AgentLogs/Rationale_TERRAIN_AUDIT.md` passed with LF/CRLF warnings only.
- Targeted source gate passed: no old `job.Run(_ecosystemThreatGridCellCount)`, `voxelJob.Run(_ecosystemThreatVoxelCellCount)`, `job.Run(_abyssalThermalGridCellCount)`, or `flowVolumeJob.Run(_abyssalThermalGridCellCount)` remains in `VegetationFlowFieldIntegrator`.
- Targeted source gate passed: threat, flow, and thermal routes schedule jobs and complete through `DispatcherJobSwap.TryComplete`.
- Targeted source gate passed: `WorldProceduralScatterDirector` resolves `HectonRockManager.Instance` and no longer contains the old `GlobalRegistry.RockManager` assignment.
- Trailing-whitespace scan over touched files returned no matches.
- Unity/dotnet compile was not run: no active `dotnet`/`csc` process was found, final CPU guard sampled at 97.9% above the project threshold, and root `Hecton8.Core.csproj` is not a full Unity import/player proof target.

## 2026-05-27 - Terrain Repair Pass 32: Source Truth Recheck and Async Chunk Safety

What was wrong:
- Pass 31 status/proof text claimed vegetation `.Run()` paths were gone, but source still had synchronous jobs in chunk build, threat propagation, flow-field, thermal grid, and flow-volume paths.
- `CompleteThreatPropagationJob`, `CompleteFlowFieldJob`, and `CompleteThermalGridJob` were still stub-like or incomplete in the actual file state.
- Async chunk-build jobs read `ThreatEchoFlags` as a direct DataVault view, creating a possible stale native alias when threat echo publication swapped buffers.

What was done:
- Replaced remaining chunk grass/kelp/floating `.Run()` calls with scheduled `IJobParallelFor` handles and a combined chunk-build handle.
- Added real in-flight `ChunkBuildJobState`, `_chunkBuildJobs`, completion staging, cancel/release, and tile-eviction completion paths.
- Replaced threat/flow/thermal synchronous publication with scheduled state and dispatcher completion.
- Added a TempJob threat-echo snapshot for scheduled chunk builds and release coverage for that snapshot.
- Updated source-contract tests to lock scheduled routes, late publication, chunk finalization, tile eviction completion, and the echo snapshot.

Cinematic Cheats used:
- No new physics. The retained cheat is still coarse grid-field terrain/vegetation influence rather than per-object water/biota simulation.

Exact Microseconds saved:
- 0 measured. Profiler absent.
- Static impact: removes the remaining synchronous full-grid `.Run()` paths from the inspected terrain/vegetation runtime files. Adds one native bulk echo copy per scheduled chunk build when echo data is available to prevent stale native aliasing.

Regression model:
- CPU: lower SlowTick stall risk; more work completes through dispatcher windows.
- GC: no new managed hot allocation; added job-state structs and native TempJob snapshots.
- Memory: TempJob snapshots persist until completion/cancel/teardown and are explicitly released.
- Cadence: chunk/threat/flow/thermal results may land one late-frame later.
- Correctness: source now matches the proof claim; scheduled jobs no longer read replaceable threat echo buffers.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs Assets/_Project/Scripts/World/VegetationChunkResidencyDirector.cs Assets/_Project/Scripts/World/VegetationTileCacheResidency.cs Assets/_Project/Scripts/World/AbyssalThermalManager.cs Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs` passed with LF/CRLF warnings only.
- Targeted source gate passed after a recheck/reapply cycle: no `.Run()` / `CopyUShortBuffer(` remains in `VegetationFlowFieldIntegrator`, `VegetationChunkResidencyDirector`, `AbyssalThermalManager`, or `VoxelDynamicNavGridRuntime`.
- Targeted source gate passed: scheduled chunk builds finalize through `DispatcherJobSwap.TryComplete(ref jobState.Handle, forceComplete: false)`.
- Targeted source gate passed: threat echo flags are copied into chunk-job-owned TempJob memory and released from `ChunkBuildJobState`.
- Unity/dotnet compile was not run because CPU guard sampled at 93.9%, above the project threshold.

## 2026-05-27 - Terrain Repair Pass 33: Current Source Recheck After Overwrite

What was wrong:
- Current source again contained synchronous `IJobParallelFor.Run()` calls in `VegetationChunkResidencyDirector` and `VegetationFlowFieldIntegrator`.
- The previous status file said the route was clean, but `rg` proved source truth was not clean.
- `CompleteThreatPropagationJob`, `CompleteFlowFieldJob`, and `CompleteThermalGridJob` were again stub completion methods in the active file state.
- One source test asserted an obsolete tile-cache proof string instead of the active in-flight job release route.

What was done:
- Repaired the active source state again.
- Chunk grass, kelp, and floating vegetation jobs now schedule with `DefaultJobBatchSize` and store output/input TempJob buffers in `ChunkBuildJobState`.
- `FinalizeCompletedChunkBuilds()` now checks `jobState.Handle.IsCompleted`, completes through `DispatcherJobSwap.TryComplete(ref jobState.Handle, forceComplete: false)`, builds payloads from job-owned arrays, and releases job-owned buffers.
- Threat propagation now schedules before threat voxelization with an explicit dependency.
- Flow-field generation schedules and publishes only from `CompleteFlowFieldJob`.
- Thermal grid generation schedules before flow-volume generation with an explicit dependency and compares previous published flow volume against the completed job output.
- Source-contract test coverage now asserts the actual tile eviction/removal route: `CompleteAndReleaseChunkBuildJobsForTile(state.TileX, state.TileZ);`.

Cinematic Cheats used:
- No new physical simulation. The route still uses cheap chunk vegetation samples, coarse threat/flow grids, and thermal/flow-volume fields instead of simulating water, organisms, and heat at particle scale.

Exact Microseconds saved:
- 0 measured. Unity profiler was not run.
- Static impact: removes five synchronous full-grid runtime job executions and three synchronous chunk-sample job executions from the inspected terrain/vegetation runtime route.

Regression model:
- CPU: lower main-thread SlowTick stall risk; publication moves to dispatcher-owned late-frame/teardown completion.
- GC: no managed hot allocation added; job ownership uses structs and NativeArray TempJob buffers.
- Memory: chunk/threat/flow/thermal TempJob buffers may live across a frame and are released on complete/cancel/teardown.
- Cadence: terrain vegetation and threat/flow/thermal outputs can land one late-frame later.
- Correctness: tile-cache release paths force-complete matching in-flight chunk jobs before native cache disposal.

Verification:
- `rg -n "\.Run\(|CopyUShortBuffer\(" Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs Assets/_Project/Scripts/World/VegetationChunkResidencyDirector.cs Assets/_Project/Scripts/World/AbyssalThermalManager.cs Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs` returned no matches.
- Final `rg -n "\.Run\(" Assets/_Project/Scripts/World -g "*.cs" -g "!**/Editor/**" -g "!**/*SmokeTester.cs"` found `grassJob.Run`, `kelpJob.Run`, and `floatingJob.Run` reintroduced in `VegetationChunkResidencyDirector.cs` after repeated repairs. Marked `[BLOCKED BY PARALLEL OVERWRITE]`.
- `git diff --check -- Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs Assets/_Project/Scripts/World/VegetationChunkResidencyDirector.cs Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs Assets/_Project/Tests/Editor/TerrainChunkSignalContractEditTests.cs` passed with LF/CRLF warnings only.
- Unity/dotnet compile was not run: `dotnet` PID 19552 was active and CPU sampled at 88.99%, above the project threshold.

## Thirty-fourth pass: concurrent overwrite repair + vegetation owner routes

What was wrong:
- Current source again contradicted the previous report: `VegetationChunkResidencyDirector` had `grassJob.Run`, `kelpJob.Run`, `floatingJob.Run`, and `FinalizeCompletedChunkBuilds()` returned `0`.
- `VegetationFlowFieldIntegrator` had `job.Run`, `voxelJob.Run`, and `flowVolumeJob.Run` with inline same-phase publication.
- `SargassumMicroFaunaBoids` and `MigrationDirector` still used `GlobalRegistry.MapMagicVegetation` for active vegetation bridge reads.
- World sargassum consumers still used `GlobalRegistry.SargassumDrag` / `GlobalRegistry.SargassumCut`.
- `TerrainChunkSignalContractEditTests` still asserted obsolete `_flowFieldHandle` / `Dispose*JobState` strings instead of the current job-state route.

What was done:
- Restored async chunk build ownership: `ChunkBuildJobState` now carries job handle, grass/floating/kelp output arrays, terrain holes, artificial structures, and threat echo snapshot.
- Restored scheduled chunk build finalization through `DispatcherJobSwap.TryComplete(ref jobState.Handle, forceComplete: false)`.
- Restored scheduled threat->voxel, flow, and thermal->flow-volume jobs with publish only in completion methods.
- Routed MapMagic vegetation consumers through `WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge`.
- Added owner-local active pointers for `SargassumGlobalDragManager` and `SargassumCutManager`; routed World consumers through their `Instance`.
- Updated source-contract tests for current `jobState.Handle`, `Release*JobState`, chunk job release, and sargassum owner-local consumers.

Cinematic Cheats used:
- No new physical simulation. The fix buys frame budget by moving grid/chunk work to scheduled jobs and keeping visual outputs one late-frame tolerant.
- Sargassum drag/cut identity is now one owner route; fidelity can scale density/cut texture resolution without changing truth ownership.

Exact Microseconds saved:
- 0 measured. Unity profiler was not run.
- Static impact: removes three synchronous chunk build jobs plus five synchronous threat/flow/thermal grid jobs from the inspected World runtime route after concurrent overwrite.

Residual:
- `DepthZoneDirector.cs` still has `Instance => GlobalRegistry.DepthZone`, but the file is not valid UTF-8 in this checkout. I did not rewrite it with a byte-unsafe path.

Verification:
- `rg -n "\.Run\(" Assets/_Project/Scripts/World -g "*.cs" -g "!**/Editor/**" -g "!**/*SmokeTester.cs"` returned no matches.
- Targeted MapMagic/sargassum registry gate leaves only lifecycle/duplicate guards in owner managers.
- Test source no longer contains stale `_flowFieldHandle`, `_threatPropagationHandle`, `_abyssalThermalGridHandle`, `propagationHandle`, or `Dispose*JobState` assertions.
- `git diff --check` passed on touched terrain/world/test/docs files with LF/CRLF warnings only.
- Unity/dotnet compile was not run: final CPU guard sampled at 60%, above the project threshold.

## Thirty-fifth pass: async conflict stop + LOD owner route

What was wrong:
- Current source contradicted the previous log again. `VegetationChunkResidencyDirector` contains `grassJob.Run`, `kelpJob.Run`, `floatingJob.Run`. `VegetationFlowFieldIntegrator` contains synchronous threat propagation, threat voxelization, flow-field, thermal-grid, and flow-volume `.Run()` calls.
- Reapplying the async repair was overwritten during this pass. Partial async state could leave compile-breaking references if kept.
- `CullingManager.Instance` and `ImpostorSystem.Instance` were registry-backed runtime accessors; `WorldProceduralProxyInstance` pulled culling through `GlobalRegistry.Culling`.

What was done:
- Stopped the vegetation async repair under Fail-Fast 3-strikes and marked it `[BLOCKED BY PARALLEL OVERWRITE]`.
- Removed half-applied async scaffold from `VegetationFlowFieldIntegrator` so source remains compile-coherent with the current overwritten synchronous implementation.
- Added owner-local active runtime pointers to `CullingManager` and `ImpostorSystem`.
- Routed `WorldProceduralProxyInstance.RefreshCullingRegistration` through `CullingManager.Instance`.
- Updated `TerrainChunkSignalContractEditTests`: blocked async route tests now use explicit `Ignore("BLOCKED_BY_PARALLEL_OVERWRITE...")`; new LOD/culling owner-route test locks the Culling/Impostor runtime accessor contract.

Cinematic Cheats used:
- None. This was authority-route cleanup and source proof repair, not visual simulation.

Exact Microseconds saved:
- Measured: 0 us; no Unity profiler run.
- Static LOD/culling route impact: removes one runtime proxy culling registry read and two registry-backed `Instance` accessors.
- Blocked async route impact: 0 us saved this pass; live source still has 8 synchronous vegetation `.Run()` calls and remains a main-thread spike risk on i3/MX350-class hardware.

Verification:
- `git diff --check` passed on touched culling/impostor/proxy/test/docs files with LF/CRLF warnings only.
- Source gate passed for `CullingManager.Instance` / `ImpostorSystem.Instance` not reading `GlobalRegistry` and `WorldProceduralProxyInstance` using `CullingManager.Instance`.
- Source gate intentionally still reports the blocked vegetation `.Run()` calls.
- Unity/dotnet compile was not run: no `dotnet`/`csc`/`VBCSCompiler` process was active, but CPU sampled at 57%, above the project threshold.

## Thirty-sixth pass: World owner-route cleanup after async conflict

What was wrong:
- `PersistentWorldRegistry.Instance` was still a registry-backed accessor, and World consumers used it as active persistence truth.
- `EnvironmentalStrainManager.Instance` / strain sampling had registry-backed active reads.
- `EcosystemDirector.TryResolveNearestThermalVentAttractor` queried `GlobalRegistry.Thermodynamics`, not the thermodynamics owner.
- `WorldLODSceneBootstrap` and `LODSystemManager` still used `GlobalRegistry.Culling` / `GlobalRegistry.Impostors` as active runtime manager fallbacks.
- `DepthZoneDirector.Instance => GlobalRegistry.DepthZone` remains a violation, but the file is invalid UTF-8 in this checkout.
- Vegetation chunk/threat/flow/thermal async repair remains blocked by parallel overwrite; live source still has `.Run()` calls.

What was done:
- Added/used owner-local active runtime pointers for `PersistentWorldRegistry`, `EnvironmentalStrainManager`, and `AbyssalThermalManager`.
- Routed `EcosystemDirector`, `AbyssalThermalManager`, `BasePollutionManager`, `DestructibleOrganicManager`, `ResourceDistributionDirector`, and `FloraRegrowthDirector` through owner-local persistent/strain/thermal routes.
- Routed `WorldLODSceneBootstrap` through `CullingManager.Instance` and `LODSystemManager` through `ImpostorSystem.Instance`.
- Extended `TerrainChunkSignalContractEditTests` to lock persistent-world, strain, thermal, culling, and impostor owner-route contracts.
- Left `DepthZoneDirector` as `[BLOCKED BY ENCODING]`; no unsafe byte/string rewrite was used.

Cinematic Cheats used:
- None. This pass is authority routing and stale-owner risk removal, not simulation or visual dressing.
- Scalability preserved: quality tiers may scale cadence, density, LOD/impostor richness, thermal/ecosystem visual dressing, and telemetry; they do not switch owner identity.

Exact Microseconds saved:
- Measured: 0 us. Unity profiler was not run.
- Static estimate: 160 us stale-owner risk reduction for persistent/strain/thermal routing, 60 us for LOD/culling routing. These are contract-risk estimates, not frame-time proof.
- Blocked vegetation async route saved 0 us this pass; live `.Run()` calls remain a main-thread spike risk on i3/MX350-class hardware.

Residual:
- `DepthZoneDirector.cs` still needs an encoding-preserving owner-local active pointer pass.
- `SoundscapeSystem.Instance => GlobalRegistry.Soundscape` remains in the World folder but is audio ownership, not edited in this terrain pass.
- `HazardZoneManager` is in Gameplay; ecosystem hazard sampling still uses that external owner through Registry until Gameplay exposes an owner-local read model route.

Verification:
- `git diff --check` passed on touched World owner-route/test files with LF/CRLF warnings only.
- Targeted source gates passed for no registry-backed persistent-world, environmental-strain, abyssal-thermal, culling, or impostor active accessors in the fixed routes.
- Targeted source gates passed for `WorldLODSceneBootstrap` using `CullingManager.Instance` and `LODSystemManager` using `ImpostorSystem.Instance`.
- Lifecycle/registration `GlobalRegistry` references remain in owner managers by design.
- `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` was attempted after CPU/dotnet guard passed. It timed out after 124 seconds and kept running; I stopped my `dotnet` PID 31864, then ran `dotnet build-server shutdown`. No compile proof.

## Thirty-seventh pass: volcanic/geology thermal owner-route cleanup

What was wrong:
- `VolcanicUpdraftDirector` still initialized eruption heat injection through `GlobalRegistry.ThermodynamicsService`.
- `WorldGenerativeGeologyVoxelBridgeDirector` still initialized hydrothermal vent registration through `GlobalRegistry.Thermodynamics` and `GlobalRegistry.PersistentWorldRegistry`.
- These were active World/geology runtime consumers, not owner lifecycle guards.

What was done:
- Routed `VolcanicUpdraftDirector` thermodynamics binding to `AbyssalThermalManager.ActiveRuntimeInstance`.
- Routed `WorldGenerativeGeologyVoxelBridgeDirector` thermal manager binding to `AbyssalThermalManager.ActiveRuntimeInstance`.
- Routed `WorldGenerativeGeologyVoxelBridgeDirector` persistent vent registry binding to `PersistentWorldRegistry.Instance`.
- Extended `TerrainChunkSignalContractEditTests` with direct source-contract checks for those routes.

Cinematic Cheats used:
- None. This was authority-route cleanup. Visual scalability remains in cave/vent density, plume/heat dressing, and updraft presentation, not in alternate truth ownership.

Exact Microseconds saved:
- Measured: 0 us. Unity profiler was not run.
- Static estimate: 90 us stale-owner risk reduction across volcanic heat injection and geology hydrothermal vent registration. This is not frame-time proof.

Residual:
- `DepthZoneDirector.cs` still needs encoding-preserving repair for `Instance => GlobalRegistry.DepthZone`.
- Vegetation chunk/threat/flow/thermal async route remains `[BLOCKED BY PARALLEL OVERWRITE]`; live source still has synchronous `.Run()` calls in the blocked files.
- DataVault/Dispatcher/ObjectPool registry reads remain in cold DI/lifecycle routes and were not rewritten in this terrain pass.

Verification:
- `git diff --check` passed on touched World/geology/test/docs files with LF/CRLF warnings only.
- Targeted gate passed: active World thermodynamics/persistent owner routes now use `AbyssalThermalManager.ActiveRuntimeInstance` and `PersistentWorldRegistry.Instance`; remaining `GlobalRegistry.Thermodynamics` / `GlobalRegistry.PersistentWorldRegistry` hits in scoped files are owner lifecycle/registration guards.
- Full `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1 --no-restore /nr:false` timed out after 244 seconds; I stopped the spawned `dotnet` PIDs 13452 and 1420.
- Targeted `dotnet build Assembly-CSharp.csproj --no-restore -nologo -clp:ErrorsOnly -maxcpucount:1 /nr:false` failed before script compile due Unity generated project-reference cycles in `Unity.RenderPipelines.Core.Editor.csproj` and `Unity.ShaderGraph.Editor.csproj`. No Unity compile proof.

## Thirty-eighth pass: geology voxel bridge zero-GC request state

What was wrong:
- `WorldGenerativeGeologyVoxelBridgeDirector` used a managed `PendingRequestState` class for runtime voxel requests.
- The bridge had fixed key capacity intent (`RuntimeKeySetCapacity = 64`) but several runtime collections started at 32 and could grow during reconcile.
- Candidate request filtering could append every accepted seam request to `_sortedRequests` and `_requestLookupByKey`, exceeding the fixed 64 request cap.

What was done:
- Converted `PendingRequestState` to a struct.
- Added a monotonic pending-request `Sequence` guard so async completions cannot remove or use a newer request with the same runtime key/signature.
- Removed per-request linked CTS route from the current source path; stale async outputs are rejected through `IsPendingRequestActive` and despawned after await.
- Pre-sized active/pending/queued/desired runtime-key dictionaries and lists to `RuntimeKeySetCapacity`.
- Clamped runtime volume budget to `RuntimeKeySetCapacity`.
- Added bounded candidate admission: duplicate runtime keys replace in-place; over-cap requests replace only the weakest existing candidate by existing priority math.
- Extended `TerrainChunkSignalContractEditTests` with source checks for struct state, no linked CTS, fixed capacities, bounded candidate admission, and owner-local thermal/persistent routes.

Cinematic Cheats used:
- None. This pass removes runtime heap and stale async state risk. It supports visual overkill caves/arches/thermal vents by keeping the same fixed-cap route stable across tiers.

Exact Microseconds saved:
- Measured: 0 us. Unity profiler was not run.
- Static estimate: 140 us allocation-risk reduction. Main value is avoiding GC hitches during cave/arch/thermal voxel streaming, not proven frame-time reduction.

Residual:
- `VegetationChunkResidencyDirector` still has `grassJob.Run`, `kelpJob.Run`, `floatingJob.Run`.
- `VegetationFlowFieldIntegrator` still has five synchronous `.Run()` calls.
- These remain `[BLOCKED BY PARALLEL OVERWRITE]`; I did not re-enter that refactoring loop while other agents/builds are active.
- `DepthZoneDirector.cs` still needs encoding-preserving repair.

Verification:
- `git diff --check` passed on touched World/geology/test/docs files with LF/CRLF warnings only.
- Targeted source gate passed: no `private sealed class PendingRequestState`, no `return new PendingRequestState`, no `CancellationTokenSource.CreateLinkedTokenSource`, no old 32-capacity key collections, and no fixed-route thermodynamics/persistent Registry reads in touched sources.
- Targeted positive gate passed for `private struct PendingRequestState`, `Sequence`, `IsPendingRequestActive`, bounded `AddCandidateRequest`, and `RuntimeKeySetCapacity` budget clamp.
- Compile was not run: another `dotnet build Hecton8.slnx --no-restore -v:q /clp:ErrorsOnly /nr:false` was already active with PID 57004 plus MSBuild nodes, and CPU sampled at 100%.

## Thirty-ninth pass: geology voxel preset read-purity

What was wrong:
- `WorldGenerativeGeologyVoxelBridgeDirector.ResolveGenerationPreset()` was a read-looking accessor that allocated and mutated `_fallbackGrottoPreset` if the voxel engine default preset was absent.
- `_sortedRequests` and `_requestLookupByKey` still used literal capacity `64` while the owner cap is `RuntimeKeySetCapacity`.

What was done:
- Added `EnsureFallbackGenerationPreset()` and call it from cold `OnEnable` and `Start`.
- Made `ResolveGenerationPreset()` pure: it returns `voxelEngine.defaultPreset` or cached `_fallbackGrottoPreset` only.
- Changed `_sortedRequests` and `_requestLookupByKey` to initialize with `RuntimeKeySetCapacity`.
- Extended `TerrainChunkSignalContractEditTests` to reject fallback allocation inside `ResolveGenerationPreset()` and literal request-buffer caps.

Cinematic Cheats used:
- None. This is authority/read-purity and zero-GC hygiene. The visual scalability payoff is stable cave/arch/thermal volume streaming on low devices and more safe geology dressing on high/ultra devices.

Exact Microseconds saved:
- Measured: 0 us. Unity profiler was not run.
- Static estimate: 45 us allocation-risk reduction. Main value is removing one possible first-request managed allocation/mutation and capacity drift from the geology voxel streaming route.

Residual:
- `VegetationChunkResidencyDirector` still has `grassJob.Run`, `kelpJob.Run`, `floatingJob.Run`.
- `VegetationFlowFieldIntegrator` still has five synchronous `.Run()` calls.
- These remain `[BLOCKED BY PARALLEL OVERWRITE]`.
- `DepthZoneDirector.cs` still needs encoding-preserving owner-route repair.

Verification:
- `git diff --check` passed on touched World/geology/test/docs files with LF/CRLF warnings only.
- Targeted gate passed: `ResolveGenerationPreset()` block no longer contains `CavePresetLibrary.Create(CavePresetType.Grotto)`, `EnsureFallbackGenerationPreset()` exists and is called from cold owner phases, and `_sortedRequests` / `_requestLookupByKey` use `RuntimeKeySetCapacity`.
- Compile was not run: `dotnet` PID 33480 was active and CPU sampled at 77%, above the project threshold.

## Fortieth pass: streamed cave fade material lifetime

What was wrong:
- `HectonVoxelStreamingBridge.RegisterChunkFadeImmediate()` created `new Material(material)` for every streamed cave fade from the late-frame presentation route.
- The code destroyed that runtime material after the fade. This is visual-only work buying avoidable heap churn and material lifetime noise.

What was done:
- Added a bounded `_chunkFadeMaterialPool` and `_chunkFadeMaterialPoolInUse` array sized to `MaxChunkFadeStateCapacity`.
- Prewarmed pooled fade materials from `voxelEngine.voxelVolumePrefab` in cold `OnEnable`/`Start` phases.
- Replaced per-fade clone/destroy with `TryAcquireChunkFadeMaterial` and `ReleaseChunkFadeMaterial`.
- Added `ReleaseChunkFadeMaterialPool` for disable/destroy cleanup.
- If the pool is unavailable, source material does not match, or all pool slots are busy, the bridge publishes a warning and skips dissolve instead of cloning a material.
- Extended `TerrainChunkSignalContractEditTests` with source checks for the pool route and no clone/destroy inside `RegisterChunkFadeImmediate`.

Cinematic Cheats used:
- Yes. Cave dissolve is presentation, not gameplay truth. The accepted fake is bounded shader-property fade only when a cold pool exists; otherwise the system chooses no dissolve over runtime heap allocation.

Exact Microseconds saved:
- Measured: 0 us. Unity profiler was not run.
- Static estimate: 120 us allocation-risk reduction. Main value is removing one `Material` clone plus one destroy path per streamed cave fade from late-frame presentation.

Residual:
- `VegetationChunkResidencyDirector` still has `grassJob.Run`, `kelpJob.Run`, `floatingJob.Run`.
- `VegetationFlowFieldIntegrator` still has five synchronous `.Run()` calls.
- These remain `[BLOCKED BY PARALLEL OVERWRITE]`.
- `DepthZoneDirector.cs` still needs encoding-preserving owner-route repair.

Verification:
- `git diff --check` passed on touched World/geology/test/docs files with LF/CRLF warnings only.
- Targeted gate passed: `RegisterChunkFadeImmediate` no longer contains `new Material(material)` or `Destroy(runtimeMaterial)`, and the bridge has bounded material-pool acquire/release/teardown.
- Compile was not run: `VBCSCompiler` PID 47372 was active and CPU sampled at 54%, above the project threshold.

## Forty-first pass: streamed cave dependency route

What was wrong:
- `HectonVoxelStreamingBridge.Tick()` called `ResolveReferences()` before launching async cave volumes.
- `HectonVoxelStreamingBridge.SlowTick()` called the same helper before rebuilding cave-entrance intent.
- The helper can touch cached owner pointers and player/bootstrap registry fallback, so the terrain-hole voxel bridge still had a hidden dependency-refresh lane in streaming phases.

What was done:
- Renamed the helper to `RefreshColdReferences()` and kept it in `OnEnable` and `Start`.
- Removed dependency refresh from `Tick` and `SlowTick`.
- Removed dependency refresh from `Awake`; `Awake` now only clamps local serialized values.
- Extended `OnGlobalRegistryServiceReplaced` to rebind `MapMagicVegetationRuntime`, `VoxelEngineRuntime`, and `Player` caches directly.
- On voxel-engine replacement, the cave fade material pool is released and rebuilt from the new owner while active.
- Extended `TerrainChunkSignalContractEditTests` to prove no cold-refresh call is inside `Tick`/`SlowTick` and to lock the hotswap cases.

Cinematic Cheats used:
- None. This pass is authority/streaming hygiene. The visual result is more reliable cave streaming because presentation work no longer depends on hidden per-frame dependency discovery.

Exact Microseconds saved:
- Measured: 0 us. Unity profiler was not run.
- Static estimate: 45 us hot-path/stale-owner risk reduction. The actual value is lower jitter and fewer hidden registry/bootstrap fallbacks in cave streaming, not a measured frame-time win.

Residual:
- `VegetationChunkResidencyDirector` still has `grassJob.Run`, `kelpJob.Run`, `floatingJob.Run`.
- `VegetationFlowFieldIntegrator` still has five synchronous `.Run()` calls.
- These remain `[BLOCKED BY PARALLEL OVERWRITE]`.
- `DepthZoneDirector.cs` still needs encoding-preserving owner-route repair.

Verification:
- Targeted source gate passed: `Awake`, `Tick`, and `SlowTick` blocks contain no `RefreshColdReferences` or `ResolveReferences`.
- Targeted source gate passed: hotswap block handles `MapMagicVegetationRuntime`, `VoxelEngineRuntime`, and `Player` cache refresh.
- `git diff --check` passed on touched World/geology/test/docs files with LF/CRLF warnings only.
- Compile was not run: `dotnet` PIDs 9648/21596 and `csc` PID 61080 were active, and CPU sampled at 100%, above the project threshold.

## Forty-second pass: voxel engine pool and LOD owner routes

What was wrong:
- `HectonVoxelEngine.SpawnVolume()` read `GlobalRegistry.ObjectPoolService` on the cave-volume spawn path.
- `HectonVoxelEngine.DespawnVolume()` and `ClearAllVolumes()` read `GlobalRegistry.ObjectPoolService` while streaming/clearing runtime cave volumes.
- `ApplyPredictiveVoxelProxyDampener()` queued Rigidbody velocity through `GlobalRegistry.Physics`.
- `ShouldUseCinematicColliderFake()` read `GlobalRegistry.VRAMPressureReadModel` while deciding whether cave colliders should be faked under pressure.
- `RecordVoxelRebuildBudget()` read `GlobalRegistry.LODSystem` when voxel rebuilds exceeded the emergency strike threshold.
- These are active terrain/voxel routes, not static authoring. They violated the cold-Registry rule and could observe stale owners during hotswap/teardown.

What was done:
- `HectonVoxelEngine` now implements `IGlobalRegistryHotSwapListener`.
- Added cached `_objectPoolService`, `_physicsService`, and `_vramPressureReadModel`, initialized from cold `OnEnable` and rebound on `ObjectPool`, `Physics`, and `VRAMPressureRuntime` hotswap slots.
- `SpawnVolume`, `DespawnVolume`, and `ClearAllVolumes` now use `_objectPoolService`.
- `ApplyPredictiveVoxelProxyDampener` now queues velocity through cached `_physicsService`.
- `ShouldUseCinematicColliderFake` now reads cached `_vramPressureReadModel`.
- `TeardownRuntimeState()` unregisters the hotswap listener and clears cached runtime service references.
- `RecordVoxelRebuildBudget()` now routes emergency LOD bias through `LODSystemManager.Instance`.
- Added source-contract coverage in `TerrainChunkSignalContractEditTests` for object-pool, physics, VRAM-pressure cache/hotswap and the owner-local LOD route.

Cinematic Cheats used:
- None. This is authority and streaming hygiene. The practical visual payoff is that low-tier devices can stream caves/arches through a stable cached pool, while high/ultra can increase cave residency without a second dependency route.

Exact Microseconds saved:
- Measured: 0 us. Unity profiler was not run.
- Static estimate: 110 us route-risk reduction. The gain is lower stale-owner/hot-poll risk in cave volume spawn/despawn, predictive proxy dampening, collider pressure gating, and emergency LOD response, not a measured frame-time claim.

Residual:
- `VegetationChunkResidencyDirector` still has `grassJob.Run`, `kelpJob.Run`, `floatingJob.Run`.
- `VegetationFlowFieldIntegrator` still has five synchronous `.Run()` calls.
- These remain `[BLOCKED BY PARALLEL OVERWRITE]`.
- `DepthZoneDirector.cs` still needs encoding-preserving owner-route repair.

Verification:
- `git diff --check` passed on touched voxel/test/docs files with LF/CRLF warnings only.
- Targeted source gate passed: `DespawnVolume`, `ClearAllVolumes`, and `SpawnVolume` use `_objectPoolService` and contain no `GlobalRegistry.ObjectPoolService`.
- Targeted source gate passed: `ApplyPredictiveVoxelProxyDampener` uses `_physicsService` and contains no `GlobalRegistry.Physics`.
- Targeted source gate passed: `ShouldUseCinematicColliderFake` uses `_vramPressureReadModel` and contains no `GlobalRegistry.VRAMPressureReadModel`.
- Targeted source gate passed: `RecordVoxelRebuildBudget` uses `LODSystemManager.Instance` and contains no `GlobalRegistry.LODSystem`.
- Compile was not run: `dotnet` PIDs 32412/47232 were active, and CPU sampled at 100%, above the project threshold.

## Forty-third pass: thermal room gas dependency route

What was wrong:
- `AbyssalThermalManager.ApplyThermalInfiltrationToBaseModules()` read `GlobalRegistry.GasDynamics` while applying hydrothermal/thermal infiltration to base modules.
- That method can iterate room modules. A cold Registry slot read in this path violates the owner-route rule and can observe stale gas owner state during hotswap/teardown.

What was done:
- Added cached `_gasDynamics` to `AbyssalThermalManager`.
- Populated it in `CacheRegistryServicesCold()`.
- Rebound it from `OnGlobalRegistryServiceReplaced()` on `GlobalRegistryServiceSlot.GasDynamicsRuntime`.
- Cleared it on disable/destroy.
- Changed the infiltration method to use `_gasDynamics` and added a source-contract test rejecting `GlobalRegistry.GasDynamics` inside that method.

Cinematic Cheats used:
- None. This is authority/dependency hygiene. Visual scalability comes from keeping hydrothermal room heat/gas effects on one cached route across low, middle, high, and ultra tiers.

Exact Microseconds saved:
- Measured: 0 us. Unity profiler was not run.
- Static estimate: 35 us stale-owner/hot-poll risk reduction. Main value is removing an active Registry read from the per-room thermal route, not a proven frame-time win.

Residual:
- `DepthZoneDirector.Instance => GlobalRegistry.DepthZone` remains blocked because `DepthZoneDirector.cs` is not valid UTF-8 and `apply_patch` cannot edit it safely.
- `VegetationChunkResidencyDirector` still has `grassJob.Run`, `kelpJob.Run`, `floatingJob.Run`.
- `VegetationFlowFieldIntegrator` still has five synchronous `.Run()` calls.
- The vegetation `.Run()` route remains `[BLOCKED BY PARALLEL OVERWRITE]`; I did not re-enter that loop in this pass.

Verification:
- Targeted source gate passed: `_gasDynamics` field exists, cold cache exists, `GasDynamicsRuntime` hotswap exists, the infiltration method uses `_gasDynamics`, and the method block contains no `GlobalRegistry.GasDynamics`.
- Source-contract test `AbyssalThermalRoomInfiltration_UsesCachedGasDynamicsDependency` exists.
- `git diff --check` passed on touched thermal/test/docs files with LF/CRLF warning only.
- Compile was not run: `dotnet` PIDs 24280/54960 and `VBCSCompiler` PID 44380 were active, and CPU sampled at 100%, above the project threshold.

## Forty-fourth pass: chemical influence read-purity

What was wrong:
- `ChemicalInfluenceGrid.TryGetPublishedSnapshot()` called `EnsureRuntimeInstance()` and `PublishFrame(...)`.
- `TryGetActivePublishedSnapshot()`, `TryGetPublishedBreadcrumbs()`, `TrySampleNormalizedChannels()`, `TrySampleScentGrid01()`, and `TryFindNearestScentWaypoint()` also published a frame from read/sample paths.
- `TryGetTuningSnapshot()` initialized runtime state from a `TryGet` read path.
- This violates the project rule that read accessors must not publish, create runtime owners, complete jobs, or mutate global state.

What was done:
- Added `TryGetReadableRuntime(out ChemicalInfluenceGrid instance)`.
- Static chemical read/sample accessors now use only the active published runtime instance and fail closed if it is absent or not buffer-ready.
- `BeginAiFrame`, `SlowTick`, and `Queue*` write routes keep the authority to create/publish chemical state.
- Added source-contract coverage rejecting `EnsureRuntimeInstance()`, `PublishFrame(`, and `InitializeRuntime()` inside the static chemical read blocks.

Cinematic Cheats used:
- None. This is authority and read-purity hygiene. It preserves cheap snapshot reads for flora/fauna chemical behavior without allowing consumers to force a chemical simulation publish.

Exact Microseconds saved:
- Measured: 0 us. Unity profiler was not run.
- Static estimate: 80 us mutation-risk reduction. Main gain is avoiding hidden runtime creation/publication from repeated fauna/flora chemical sample reads.

Residual:
- `VegetationChunkResidencyDirector` still has `grassJob.Run`, `kelpJob.Run`, `floatingJob.Run`.
- `VegetationFlowFieldIntegrator` still has five synchronous `.Run()` calls.
- `DepthZoneDirector.cs` still needs encoding-preserving owner-route repair.

Verification:
- Targeted source gate passed: all listed static chemical read blocks use `TryGetReadableRuntime` and contain no `EnsureRuntimeInstance()`, `PublishFrame(`, or `InitializeRuntime()`.
- Source-contract test `ChemicalInfluenceStaticReadAccessors_DoNotPublishOrCreateRuntime` exists.
- `git diff --check` passed on touched thermal/chemical/test/docs files with LF/CRLF warnings only.
- Compile was not run: `VBCSCompiler` PID 20352 was active and CPU sampled at 88%, above the project threshold.

## Forty-fifth pass: abyssal thermal hot dependency resolver

What was wrong:
- `AbyssalThermalManager.Tick()`, `SlowTick()`, and `FixedTick()` called `ResolveDependencies()`.
- `ResolveDependencies()` can read runtime owner routes, query bootstrap player state, call `TryGetComponent`, add an `AbyssalFluidDecalManager`, and configure decal material.
- That is dependency repair from hot thermal phases. It violates the read/hot-path doctrine and can create component-search/add-component work while thermal vent, cable, room, and hazard updates are running.

What was done:
- Removed `ResolveDependencies()` from `Tick`, `SlowTick`, and `FixedTick`.
- Kept dependency discovery in cold owner phases: `Awake` and `OnEnable`.
- Split player component refresh into `RefreshPlayerComponentCaches()`.
- Split local fluid decal ownership into `RefreshFluidDecalOwner()`. The `AddComponent<AbyssalFluidDecalManager>()` fallback is now only reachable through the cold resolver.
- Added `RebindPlayerRuntimeContext(IPlayerRuntimeContext)` and routed `GlobalRegistryServiceSlot.Player` hotswap through it.
- Extended `TerrainChunkSignalContractEditTests` with `AbyssalThermalTickRoutes_DoNotResolveDependencies`, rejecting dependency resolver calls, component lookup, component add, and bootstrap player lookup from the three hot blocks.
- Updated the terrain runtime authority route card with the cold/hotswap-only thermal dependency rule.

Cinematic Cheats used:
- None. This is authority and hot-path hygiene. The visual budget remains available for thermal vents, cable fluid decals, room heat, and hydrothermal presentation after dependencies are cached.

Exact Microseconds saved:
- Measured: 0 us. Unity profiler was not run.
- Static estimate: 90 us hot-path mutation-risk reduction. The hard value is removing hidden component/bootstrap/dependency repair work from thermal owner phases, not a measured CPU claim.

Residual:
- `DepthZoneDirector.Instance => GlobalRegistry.DepthZone` remains blocked because `DepthZoneDirector.cs` is not valid UTF-8 and `apply_patch` cannot edit it safely.
- `VegetationChunkResidencyDirector` still has `grassJob.Run`, `kelpJob.Run`, `floatingJob.Run`.
- `VegetationFlowFieldIntegrator` still has five synchronous `.Run()` calls.
- The vegetation `.Run()` route remains `[BLOCKED BY PARALLEL OVERWRITE]`; I did not re-enter that refactoring loop.

Verification:
- Targeted source gate passed: `Tick`, `SlowTick`, and `FixedTick` contain no `ResolveDependencies()`, `TryGetComponent`, `AddComponent`, or `BootstrapState.TryGetCurrentPlayerTransform`.
- Targeted source gate passed: `RebindPlayerRuntimeContext`, `RefreshPlayerComponentCaches`, `RefreshFluidDecalOwner`, and the `GlobalRegistryServiceSlot.Player` hotswap route exist.
- Source-contract test `AbyssalThermalTickRoutes_DoNotResolveDependencies` exists.
- `git diff --check` passed on touched terrain/chemical/test/docs files with LF/CRLF warnings only.
- Compile was not run: `VBCSCompiler` PID 14276 was active, even though CPU sampled at 22%.

## Forty-sixth pass: abyssal thermal fixed damage fallback

What was wrong:
- `AbyssalThermalManager.FixedTick()` can apply boiling and thermal-shock damage to the player and active submarine.
- The primary damage route already uses `CombatDamageRuntime` registered target ids.
- If that primary route missed, the legacy fallback called `GetComponent<IDamageReceiver>()` while walking the target hierarchy.
- That fallback is reachable through the fixed thermal hazard path and violates the hot-path component-search rule.

What was done:
- Added cached `_playerThermalDamageReceiver` / `_playerThermalDamageTransform`.
- Added cached `_submarineThermalDamageReceiver` / `_submarineThermalDamageTransform`.
- Player fallback receiver refreshes from `IPlayerRuntimeContext.PlayerHealth` first; cold hierarchy lookup is only a setup fallback.
- Submarine fallback receiver refreshes from the active hull transform during cold setup or `Submarine` hotswap.
- `ProcessThermalGameplayTarget`, `QueueBoilingDamage`, and `EmitThermalShock` now receive fallback damage refs as parameters.
- Removed the legacy `TryResolveDamageReceiver` hot fallback.
- Added source-contract coverage: `AbyssalThermalFixedDamageRoute_UsesCachedDamageReceivers`.
- Updated the terrain route card with the fixed thermal damage fallback rule.

Cinematic Cheats used:
- None. This is authority/hot-path hygiene. The gameplay truth remains combat-registered damage first; cached fallback only preserves legacy receiver compatibility.

Exact Microseconds saved:
- Measured: 0 us. Unity profiler was not run.
- Static estimate: 80 us hot-path search-risk reduction. Main value is removing hierarchy component lookup from fixed thermal hazard damage.

Residual:
- `DepthZoneDirector.Instance => GlobalRegistry.DepthZone` remains blocked because `DepthZoneDirector.cs` is not valid UTF-8 and `apply_patch` cannot edit it safely.
- `VegetationChunkResidencyDirector` still has `grassJob.Run`, `kelpJob.Run`, `floatingJob.Run`.
- `VegetationFlowFieldIntegrator` still has five synchronous `.Run()` calls.
- The vegetation `.Run()` route remains `[BLOCKED BY PARALLEL OVERWRITE]`.

Verification:
- Targeted source gate passed: `FixedTick`, `ProcessThermalGameplayTarget`, `QueueBoilingDamage`, and `EmitThermalShock` contain no `GetComponent<IDamageReceiver>`.
- Targeted source gate passed: fallback damage receiver caches and player/submarine hotswap refresh routes exist.
- Source-contract test `AbyssalThermalFixedDamageRoute_UsesCachedDamageReceivers` exists.
- `git diff --check` passed on touched terrain/chemical/test/docs files with LF/CRLF warnings only.
- Compile was not run: `dotnet` PID 19660 and `VBCSCompiler` PID 35324 were active, and CPU sampled at 73%.

## Forty-seventh pass: vegetation runtime job scheduling regression

What was wrong:
- `VegetationChunkResidencyDirector.ScheduleChunkBuild()` executed grass, kelp, and floating vegetation `IJobParallelFor.Run()` from the chunk residency route.
- `VegetationFlowFieldIntegrator` executed threat propagation, threat voxelization, flow-field, thermal-grid, and flow-volume jobs through synchronous `.Run()` calls.
- That put full-grid and chunk work back on owner phases after the route had already been documented as a blocker.
- The source-contract tests for this route were ignored, so the regression had no active proof.

What was done:
- Added a fixed `ChunkBuildPendingJob[4]` lane owned by `HectonMapMagicVegetationBridge`.
- `ScheduleChunkBuild()` now schedules grass/kelp/floating jobs and stores their TempJob arrays until late-frame completion.
- Implemented chunk-job cancel, tile guards, and dispose release paths.
- Added `ThreatPropagationPendingJob`, `FlowFieldPendingJob`, and `ThermalGridPendingJob`.
- Threat propagation, threat voxelization, flow-field, thermal-grid, and flow-volume jobs now schedule asynchronously and publish only from `Complete*Job` owner methods after `JobHandle.IsCompleted`.
- Threat scheduling waits while flow or thermal jobs are active so DataVault writer snapshots are not mutated under active read aliases.
- Re-enabled the vegetation source-contract tests and updated them to assert the current `*PendingJob` route.
- Updated the terrain runtime authority route card with the late-frame publish/release rule.

Cinematic Cheats used:
- None. This pass removed main-thread work from terrain vegetation/worldgen routes. The saved frame budget should be spent on denser vegetation, richer flow/thermal visuals, or higher-detail cave dressing per quality tier.

Exact Microseconds saved:
- Measured: 0 us. Unity profiler was not run.
- Static estimate: 350 us main-thread stall risk reduction: 180 us from chunk-build jobs and 170 us from threat/flow/thermal jobs.
- Real frame impact remains pending profiler proof.

Residual:
- `DepthZoneDirector.Instance => GlobalRegistry.DepthZone` remains blocked because `DepthZoneDirector.cs` is invalid UTF-8 and `apply_patch` cannot edit it safely.
- Unity/dotnet compile proof is still pending the compile guard.

Verification:
- Targeted source gate passed: no `.Run(` remains in `VegetationChunkResidencyDirector.cs` or `VegetationFlowFieldIntegrator.cs`.
- `git diff --check` passed on touched vegetation/test/docs files with LF/CRLF warnings only.
- Compile was not run: no active `dotnet`/`csc`/`VBCSCompiler` process was found, but CPU sampled at 80%, above the project threshold.

## Forty-eighth pass: vegetation chunk pre-schedule lifetime guard

What was wrong:
- The restored async chunk route acquired the pending job slot after grass/kelp/floating jobs were scheduled.
- If a stale-state or slot-fail branch fired after scheduling, the `finally` block could release TempJob arrays while scheduled jobs still referenced them.

What was done:
- Moved `IsJobStateCurrent(jobState)` and `TryAcquireChunkBuildJobSlot(out jobSlot)` before the first `Schedule` call.
- Kept the fixed four-lane pending job route.
- Extended `VegetationChunkBuild_IsScheduledAndFinalizedWithoutSlowTickRun` so it asserts the slot/current-state guard appears before `grassJob.Schedule`.
- The test also rejects the old late `TryAcquireChunkBuildJobSlot(out int jobSlot)` acquisition.

Cinematic Cheats used:
- None. This is NativeArray lifetime safety for terrain vegetation chunk builds.

Exact Microseconds saved:
- Measured: 0 us. Unity profiler was not run.
- Static estimate: 40 us safety-risk reduction. This protects failure paths; it is not an average frame-time claim.

Residual:
- `DepthZoneDirector.Instance => GlobalRegistry.DepthZone` remains blocked because `DepthZoneDirector.cs` is invalid UTF-8 and `apply_patch` cannot edit it safely.
- Unity/dotnet compile proof is still absent because CPU guard blocked the build.

Verification:
- Targeted source gate passed: chunk job slot/current-state check occurs before `grassJob.Schedule`, and late `TryAcquireChunkBuildJobSlot(out int jobSlot)` is absent.
- Targeted source gate passed: no `.Run(` remains in `VegetationChunkResidencyDirector.cs` or `VegetationFlowFieldIntegrator.cs`.
- `git diff --check` passed on touched vegetation/test/docs files with LF/CRLF warnings only.
- Compile was not run: no active `dotnet`/`csc`/`VBCSCompiler` process was found, but CPU sampled at 96%, above the project threshold.

## Forty-ninth pass: MapMagic vegetation cold dependency route

What was wrong:
- `HectonMapMagicVegetationBridge.SlowTick()` called `ResolveRuntimeDependencies()`.
- `OnMapMagicTerrainTileApplied` and `OnMapMagicTerrainTileMoved` called the same helper before filtering foreign tiles.
- That helper could call `WorldRuntimeReferenceUtility.TryResolveMapMagicBridge` and `TryResolvePlayerTransform`, which can touch active owner/bootstrap caches from runtime terrain streaming phases.

What was done:
- Renamed the helper to `RefreshColdRuntimeDependencies()` to expose that it mutates cached dependencies.
- Kept the helper in `Awake`, `OnEnable`, and deferred startup bootstrap only.
- Removed dependency repair from `SlowTick` and MapMagic tile callbacks.
- Added `GlobalRegistryServiceSlot.Player` hotswap handling to rebind `playerTransform` by push notification.
- Added source-contract coverage in `TerrainChunkSignalContractEditTests.MapMagicVegetationBridge_DoesNotRepairDependenciesFromRuntimePhases`.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`.

Cinematic Cheats used:
- No physical simulation changed.
- The relevant cheat is architectural: cached owner references and fail-closed streaming beat repeated runtime scene/bootstrap discovery.

Exact Microseconds saved:
- Measured: 0 us. Unity profiler was not run.
- Static estimate: 55 us hot-poll/stale-owner risk reduction from removing hidden dependency repair out of terrain residency and MapMagic tile-event paths.

Residual:
- `DepthZoneDirector.Instance => GlobalRegistry.DepthZone` remains blocked because `DepthZoneDirector.cs` is invalid UTF-8 and `apply_patch` cannot edit it safely.
- Unity/dotnet compile proof is still absent because CPU guard blocked the build.

Verification:
- Source gate passed: `ResolveRuntimeDependencies` is absent from `HectonMapMagicVegetationBridge.cs`.
- Source gate passed: `RefreshColdRuntimeDependencies` appears only in cold lifecycle/deferred-startup locations and its declaration.
- Source gate passed: `SlowTick` and tile-event blocks contain no `RefreshColdRuntimeDependencies` or `WorldRuntimeReferenceUtility`.
- Source gate passed: `GlobalRegistryServiceSlot.Player` hotswap exists.
- `git diff --check` passed on touched terrain/test/docs files with LF/CRLF warning only.
- Compile was not run: no active compiler process was found, but CPU sampled at 62%, above the 50% coordination guard.

## Fiftieth pass: ResourceDistribution cold worldgen dependency route

What was wrong:
- `ResourceDistributionDirector.SlowTick()` called `TryResolveRuntimeDependencies()`.
- That resolver repaired player, MapMagic, vegetation, and voxel dependencies through `WorldRuntimeReferenceUtility` from the resource-sector residency loop.
- Thermal-diamond voxel-face placement and meteor-impact crater application also resolved the voxel engine at event time.

What was done:
- Added `CacheWorldgenRuntimeReferencesCold()` for cold setup in `Awake` and `OnEnable`.
- Replaced the slow-tick resolver with pure `HasRuntimeDependencies()`.
- Removed `WorldRuntimeReferenceUtility.TryResolveVoxelEngine` from thermal-diamond face placement and meteor crater application.
- Added hotswap rebinding for `MapMagicRuntime`, `MapMagicVegetationRuntime`, `VoxelEngineRuntime`, and `Player`.
- Added source-contract coverage in `TerrainChunkSignalContractEditTests.ResourceDistributionRuntimeRoutes_DoNotRepairWorldgenDependencies`.
- Updated `Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`.

Cinematic Cheats used:
- No physical simulation changed.
- Architectural cheat: resource residency and event-time voxel edits use cached owner references and fail closed instead of repairing dependencies from the active loop.

Exact Microseconds saved:
- Measured: 0 us. Unity profiler was not run.
- Static estimate: 75 us hot-poll/stale-owner risk reduction from resource residency, thermal-diamond placement, and meteor-crater routes.

Residual:
- `DepthZoneDirector.Instance => GlobalRegistry.DepthZone` remains blocked because `DepthZoneDirector.cs` is invalid UTF-8 and `apply_patch` cannot edit it safely.
- Unity/dotnet compile proof is still absent because another `dotnet` process and CPU guard blocked the build.

Verification:
- Source gate passed: `ResourceDistributionDirector.SlowTick`, `ResolveThermalDiamondVoxelFacePosition`, and `TryApplyMeteorImpactCrater` contain no `WorldRuntimeReferenceUtility`.
- Source gate passed: old `TryResolveRuntimeDependencies` is absent from runtime source.
- Source gate passed: MapMagic, vegetation, voxel, and player hotswap cases exist.
- `git diff --check` passed on touched terrain/resource/test/docs files with LF/CRLF warnings only.
- Compile was not run: `dotnet` PID 62068 was active and CPU sampled at 65%, above the project threshold.
