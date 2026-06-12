# Terrain Runtime Authority Route

Status: repair-pass route card, 2026-05-26; chain section added 2026-06-12 (full code audit).
Evidence class: STATIC_DOC
Owner domain: Echelon 2 World Generation & Terrain.
Review disposition: YELLOW / STATIC_DOC_ONLY until compile/import/runtime/profiler/player proof exists.

---

## Полная цепочка процедурной генерации (VERIFIED 2026-06-12, все файлы прочитаны целиком)

### Источник формы рельефа

**`WorldMacroGeologyFields`** (`Assets/_Project/Scripts/World/WorldMacroGeologyFields.cs`) — единственный первоисточник. Никакого `.h8bin` для рельефа нет. Это статическая аналитическая функция `ArtifactVersion = 6`.

Алгоритм `EvaluateHeightMeters()` в порядке слоёв:
1. Три уровня domain warp (low/mid/high, 980/520/240 м)
2. Синусоидальная линия шельфа с warp-смещением
3. 5 процедурных каньонов
4. Первичный разлом с tectonic trench (направление 0.72/-0.69)
5. Вторичный разлом (-0.38/-0.925)
6. 3 хребта параллельно разломам
7. 3 эллиптических бассейна (фиксированные координаты в AUP)
8. 4 хребтовых цепи (ridge chain hills)
9. Cellular Voronoi тектоническая сеть + uplift waves
10. Региональный breakup (macro + meso шум)
11. Hard clamp: depth ∈ [−620, HadalDepth], нелинейный зажим при depth < −260

Возвращает `WorldMacroGeologySample` (20+ полей): height, depth, shelf/shelfBreak/ridge/trench/basin/fault masks, slope01, curvature01, erosionFlow01, terrace/slump/tributary/nodule/reef/hardRock/voxelSeam masks, PrimaryZone.

**Параметры по умолчанию** (`WorldMacroGeologyParams.CreateDefault`):
- ShelfDepth 90 м, AbyssDepth 2950 м, HadalDepth 4600 м
- ShelfBreakWidth 5200 м, RidgeHeight 1550 м / Width 2350 м
- TrenchDepth 900 м / Width 2200 м, BasinDepth 620 м
- Authoring seed: `880031`, MinimumWorldExtent: 30 000 м, DefaultChunkSize: 512 м

**Version gate:** если `MacroGeologyArtifactVersion` в Burst-джобе не совпадает с `ArtifactVersion = 6` — активируется Voronoi fallback в `HectonSandboxAbyssalShelfMath`. Сейчас версии совпадают.

---

### Полная цепочка генерации (рантайм)

```text
WorldMacroGeologyFields.EvaluateHeightMeters()   ← ЕДИНСТВЕННЫЙ ПЕРВОИСТОЧНИК ФОРМЫ
│  AUP XZ float, детерминированный, без аллокаций
│  seed = CombineWorldSeed(authoringSeed=880031, runtimeWorldSeed)
│
├─► HectonSandboxAbyssalShelfMapMagicNode         [MapMagic граф, нода высоты]
│     Burst IJob, NativeArray<float>, Allocator.TempJob
│     → HectonSandboxAbyssalShelfBaseJob (первичный путь при ArtifactVersion совпадении)
│     → HectonSandboxAbyssalShelfMath (Voronoi fallback при version mismatch)
│     → записывает в MapMagic MatrixWorld → TerrainData (SetHeightsDelayLOD)
│
├─► HectonTerrainSplatmapMapMagicNode             [MapMagic граф, сплатмап]
│     → WorldProceduralTerrainSlopeCavitySplatmapJob [Burst IJobParallelFor]
│       → WorldMacroGeologyFields.Evaluate() → WorldTerrainSurfaceMaterialResolver
│       → WorldTerrainMesoDetailFields.Evaluate() (террасы/обвалы/каналы ±70 м)
│       Выходы: sand / rock / silt / cavity / slopeWeight (MatrixWorld)
│
├─► HectonTerrainSurfaceMaterialMapMagicNode      [MapMagic граф, 8 материальных масок]
│     → WorldTerrainSurfaceMaterialMaskJob [Burst IJobParallelFor]
│       → WorldMacroGeologyFields.Evaluate() → WorldTerrainSurfaceMaterialResolver
│       Выходы: shellSand / limestoneShelf / claySilt / hardRock /
│               brineSaltCrust / manganeseNodulePlain / reefRubble / seepCrust
│               + packed RGBA (R=rock, G=sand, B=silt, A=deposition)
│
├─► WorldGenerativeGeologyTerrainSeamApplier      [MonoBehaviour, запись в Terrain]
│     SetHeightsDelayLOD, _HectonVoxelBlendMask texture
│
├─► WorldGenerativeGeologyVoxelBridgeDirector     [жизненный цикл Voxel volumes]
│     → HectonVoxelEngine (Marching Cubes, Mesh.AllocateWritableMeshData)
│     → VoxelSeamDirector (seam math в AUP, без Transform-space)
│
└─► WorldProceduralFieldSampler                   [центральный рантайм-сэмплер]
      SeafloorSource enum:
        MapMagicHeight (первичный) → MapMagicBridge
        MacroGeologyFallback       → WorldMacroGeologyFields.Evaluate() напрямую
        TerrainProviderHeight / SceneProbeLegacy / FallbackSynthetic
      CellSamplingJob [Burst IJobParallelFor, 4096 биом-ячеек]:
        → зона, биом, паттерн, плотности scatter, cave proximity
      FieldSample.macroGeologySample содержит полный WorldMacroGeologySample
      → (spawn, scatter, cave, fauna, loot, PDA, resource distribution)
```

---

### Детальная цепочка WorldTerrainDetailContracts (из WorldMacroGeologySample вниз)

```text
WorldMacroGeologySample
│
├─ WorldTerrainMesoDetailFields.Evaluate()   → WorldTerrainMesoDetailSample
│    (ContractVersion=1, дельта высоты ±70 м, terraces/slump/tributary/talus/rubble/reef)
│
├─ WorldTerrainSurfaceMaterialResolver.Resolve()  → WorldTerrainSurfaceMaterialWeights
│    (ContractVersion=1, 8 нормализованных весов материалов)
│    ApplyMesoDetailBias() → уточнение весов через meso-сэмпл
│    ResolvePackedControlRgba() → float4(R=rock, G=sand, B=silt, A=deposition)
│
└─ WorldTerrainDetailContracts.ResolveEligibilityFlags()  → uint flags
     SandScatter / RockScatter / NoduleScatter / ReefScatter / BrineDeposit /
     SeepDeposit / TalusBoulder / RubblePebble / DetailNormal / DecalOverlay /
     VoxelAnchor / CaveMouthCandidate

Тиры детализации (WorldTerrainDetailContracts):
  NearPlayable:  513 px, 1 м/px, до  768 м,   все control maps
  MidTraversal:  257 px, 2 м/px, до 2048 м,   macro+slope+curvature+erosion+terrace+slump+tributary+material+voxelSeam
  FarSilhouette: 129 px, 4 м/px, до 6144 м,   macro+slope+material+voxelSeam
  DistantHlod:    65 px, 8 м/px, до 24576 м,  macro+material
```

---

### Python-близнец: предпросмотр по сиду

**`Tools/BuildWorldMacroGeologyPreview.py`** (1434 строки) — точная математическая копия `WorldMacroGeologyFields` на Python.

Запуск:
```powershell
python -B Tools/BuildWorldMacroGeologyPreview.py --seed 880031 [--runtime-seed 0] [--resolution 512] [--workers N]
```

Генерирует в `Docs/GeneratedAssets/Terrain/MacroGeology/`:
- `terrain_relief_proof.png` — hillshade + erosion (основной визуал)
- `raw_elevation.png`, `hillshade.png`, `local_relief.png`
- `geology_zones.png` — 8 зон цветом (PhoticShelf/ShelfBreak/FaultRidge/BrineTrench/AbyssalPlain/SedimentFan/ColdSeepField/HadalBasin)
- `depth_strata.png` — Photic/Mesophotic/Bathyal/Abyssal/Hadal
- `waterline_sweep.png` — при WaterY ∈ {-100, 0, +100}
- `material_regions.png` — 8 материальных масок
- `terrain_control_rgba_fold.png` — packed R/G/B/A
- `slope.png`, `curvature.png`, `erosion_flow.png`
- `shelf_mask.png`, `trench_mask.png`, `ridge_fault_mask.png`, `sediment_seep_mask.png`
- `meso_terrain_controls.png`, `scatter_eligibility.png`, `voxel_seam_mask.png`
- `contact_sheet.png`, `terrain_contact_sheet.png`
- `WorldMacroGeologyPreviewManifest.json` + `WorldMacroGeologyChunkManifest.json`

Также пишет player-approach proof waypoints: `shelf_approach`, `shelf_break_descent`, `canyon_sediment_fan`, `ridge_flank`, `trench_floor`, `basin_floor`, `voxel_seam_candidate`.

Валидатор: **`Tools/ValidateWorldMacroGeologyPreview.py`** — проверяет manifest, sha256 всех PNG, zone counts, depth strata, waypoints, chunk manifest.

**Смена сида:** меняешь `--seed` или `--runtime-seed` → получаешь другой рельеф с теми же дефолтными параметрами. `combine_world_seed(authoring, runtime)` = `hash32(authoring, runtime, 0x6D2B79F5)`.

---

## Runtime Truth

Runtime terrain reads must prefer first-party immutable payloads:

- Macro-geology identity from `WorldMacroGeologyFields`: authoring seed, macro artifact version, chunk size, chunk range, and chunk range hash.
- Terrain detail/material contracts from `WorldTerrainDetailContracts`: material classes, meso detail fields, proof extents, and packed control channels.
- Quantized height payloads from `HectonMapMagicVegetationBridge`.
- Cache revision carried through `TerrainChunkGeneratedSignal`.
- Voxel/SDF density fields for caves, arches, overhangs, and ore-volume geometry.
- Terrain seam writeback only as a bounded adapter to Unity Terrain.

MapMagic remains an authoring/bake, active provider, or controlled tile-application source over the first-party terrain contract. It must not be treated as the standalone source of macro-geology truth. Unity `TerrainData` is a visual/collider adapter unless a route explicitly marks it as the owner for that phase.

Save/load terrain identity must compare the active runtime provider against the macro-geology contract and water calibration. Mismatched seed, macro artifact version, chunk size/range/hash, runtime generation id, provider flags, or water level/source is a visible compatibility fault, not a silent repair.

## Mock Payload Rule

`TerrainChunkPagerRuntime.forceMockDiskIo` and `TerrainChunkPagerTuningDTO.CreateDefault()` must not default to production mock truth.

Mock sector payloads are editor/development scaffolding only.

Player builds must fail closed to real sidecar/Addressables payload loading when terrain chunk files are absent.

Runtime tuning sanitization strips unsupported request flags and keeps only the development force-mock lane.

Chunk sidecar headers are schema-gated.

Version `1` is the only accepted terrain chunk file version in the current reader. Unknown header flags are rejected.

A future payload manifest must move through a deliberate v2 reader instead of silently accepting old or foreign bytes.

## Hot Read Rules

Terrain read APIs must not:

- Poll `GlobalRegistry`.
- Mutate `_lastResolvedTerrainTile` or any LRU/cache state.
- Allocate texture arrays or terrain tile arrays.
- Search the scene.

MapMagic bridge runtime identity is owner-local.

`MapMagicBridge.Instance` and `WorldRuntimeReferenceUtility.TryResolveMapMagicBridge` read the bridge-owned active pointer. They must not poll `GlobalRegistry.MapMagic`.

`GlobalRegistry.MapMagic` remains a cold registration, duplicate-owner guard, and unregister guard inside `MapMagicRuntimeBridge` owner lifecycle only.

Procedural ore and marauder outpost generation consume terrain/MapMagic through `WorldRuntimeReferenceUtility.TryResolveMapMagicBridge`.

`ProceduralOreSpawner` must not assign `_terrainProvider` from `GlobalRegistry.Terrain`; it should use the resolved MapMagic bridge as the terrain provider unless a hotswap event supplies a provider.

`MarauderOutpostGenerationService` must not assign `_cachedMapMagicBridge` from `GlobalRegistry.MapMagic`.

`BiomeMatrixDirector` consumes terrain through hotswap-injected `ITerrainProvider` or `WorldRuntimeReferenceUtility.TryResolveMapMagicBridge`. It must not poll `GlobalRegistry.Terrain` to evaluate biome depth or seismic dust.

`BiomeMatrixDirector.EvaluateMatrix` must not repair player references while playing.

Player transform and movement rebinding belongs to cold setup or `GlobalRegistryServiceSlot.Player` hotswap.

Runtime evaluation consumes the cached transform.

`EcosystemDirector` terrain/resource cross-domain reads use `MapMagicBridge.Instance` and `ResourceDistributionDirector.ActiveRuntimeInstance`. It must not poll `GlobalRegistry.MapMagic` or `GlobalRegistry.ResourceDistribution` from spawn gates, depth, envelope, or mutation sampling.

`HectonAnomalyResourceBinding` consumes resource distribution through `ResourceDistributionDirector.ActiveRuntimeInstance`. MapMagic anomaly nodes and voxel anomaly binding must not rediscover resource distribution through `GlobalRegistry.ResourceDistribution`.

`GroundPenetratingRadarRuntime` consumes ore SoA through `WorldRuntimeReferenceUtility.TryResolveWorldResourceSpawnerReadModel` and voxel sonar SDF through `WorldRuntimeReferenceUtility.TryResolveVoxelEngine`.

GPR startup and scan scheduling must not rediscover ore or voxel SDF through `GlobalRegistry.WorldResourceSpawner` or `GlobalRegistry.VoxelSonarSdf`.

Owner phases such as `SlowTick`, tile-applied callbacks, or cache refresh may update cached terrain tile and biome texture state.

Splat color reads use cached terrain layer handles.

`TerrainData.terrainLayers` is allowed only from the owner refresh phase.

Unity exposes it as an array property. It must not sit in the hot terrain read route.

Vegetation tile mask builds resolve alphamap `GetPixelData<Color32>` aliases once per sampled layer, then scan the tile through local samplers. Per-texel Unity texture alias resolution is rejected.

MapMagic rock output finalization uses a count-pass plus exact per-layer `Matrix4x4[]` payloads.

Per-layer `List<Matrix4x4>` staging and `ToArray()` copies are rejected in `HectonRockOutput.Finalize`.

Rock output apply has replace semantics: unregister the old chunk entry before registering the new non-empty layer payloads.

Empty layer payloads must not keep stale rock instances resident.

`HectonRockOutput.Apply` and `ClearApplied` use `HectonRockManager.Instance`, not `GlobalRegistry.RockManager`.

`WorldProceduralScatterDirector` also borrows the rock GPUI manager through `HectonRockManager.Instance`. Scatter decoration refresh must not poll `GlobalRegistry.RockManager`.

Rock manager Registry registration remains its cold lifecycle publication only.

`HectonRockManager.RegisterChunk` accepts only preconfigured rock layers with prototype and aggregation buffers built during manager initialization.

Runtime registration of unknown MapMagic rock layer ids fails closed. Late `Dictionary<Vector2Int, Matrix4x4[]>` / `Matrix4x4[]` allocation for unconfigured layers is rejected.

MapMagic generator seed mixing reads the owner-published `HectonWorldGenerator` runtime seed snapshot.

MapMagic generator nodes must not poll `GlobalRegistry.WorldSeedProvider` from `Generate` or seed utility paths.

Marauder outpost generation resolves world seed from the same owner-published runtime seed snapshot. Its hotswap-injected seed provider cache is fallback only; generation reads must not poll `GlobalRegistry.WorldSeedProvider`.

Biome transition read accessors use cached `GlobalDataVault` generation handles.

They must not poll `GlobalRegistry.DataVault`.

They must not rediscover vault handles in `TryReadSnapshot` / `TryReadTuning`.

Terrain hole mask construction is a bounded Burst `IJobParallelFor` from `SlowTick`.

The late-frame swap window finalizes before Unity `SetHolesDelayLOD` / `SyncTexture`.

Synchronous `Run()` over the full holes resolution in `SlowTick` is rejected.

Vegetation chunk payload generation is also a scheduled Burst route.

`SlowTick` may enqueue grass, kelp, and floating vegetation jobs, but it must not execute `IJobParallelFor.Run()` over chunk samples.

Chunk payloads become resident only after the late-frame finalizer observes `JobHandle.IsCompleted` and completes with `forceComplete: false`.

Tile cache eviction skips tiles with in-flight chunk jobs; teardown may force-complete and discard those jobs before releasing tile-native buffers.

Threat spatial snapshot refresh waits while chunk jobs are in flight, so optional permanent-echo vegetation dressing does not race chunk generation reads.

Abyssal vegetation flow-field generation is scheduled from `SlowTick`.

`SlowTick` may create the TempJob snapshots and enqueue `BuildAbyssalFlowFieldJob`, but it must not execute `IJobParallelFor.Run()` or publish the flow buffer.

The late-frame swap window owns completion, publication to `VegetationEcosystemFlowField`, and TempJob release.

Threat spatial snapshot refresh waits while the flow-field job is in flight, so the job cannot read a threat grid while the owner replaces it.

Abyssal thermal grid and 3D flow-volume generation are scheduled as a dependent job chain.

The thermal grid job writes the temperature field first; the flow-volume job consumes that TempJob output through an explicit `JobHandle` dependency.

Late-frame swap window:

- Compare previous published flow volume against newly completed output.
- Publish only after old-vs-new biolume surge detection.
- Reject self-comparison against the just-published buffer.

Ecosystem threat propagation and threat voxelization are scheduled before flow/thermal work.

The propagation job writes threat, compressed threat, and echo TempJob outputs; the voxelization job consumes the new threat output through an explicit dependency.

Flow-field and thermal scheduling wait until no threat propagation job is in flight, so consumers never read stale/half-published threat data in the same `SlowTick`.

Source contract: `VegetationFlowFieldIntegrator` must not contain `job.Run(_ecosystemThreatGridCellCount)`, `voxelJob.Run(_ecosystemThreatVoxelCellCount)`, `job.Run(_abyssalThermalGridCellCount)`, or `flowVolumeJob.Run(_abyssalThermalGridCellCount)` in these routes. Publication belongs to the `Complete*Job` late-frame completion methods.

Permanent echo invalidation compares previous published echo flags with the completed echo output before publishing the new echo snapshot.

Vegetation native chunk-pool defragmentation is runtime-dormant.

The active route is grow-only/free-list residency plus fragmentation telemetry.

Runtime pool compaction, scratch-pool swaps, and `IJob.Run()` pool copies are rejected because they create a stop-the-world streaming path and can invalidate native ownership during chunk residency churn.

Voxel runtime identity is owner-local.

`HectonVoxelEngine.ActiveRuntimeInstance` reads the engine-owned static pointer. It must not poll `GlobalRegistry.VoxelEngine` from hot terrain, resource, thermal, or damage consumers.

Registry registration remains a cold dependency-injection route only.

Voxel cave volume pooling is cached in the voxel owner.

`HectonVoxelEngine` caches `IObjectPoolService`, `IPhysicsService`, and `IVramPressureReadModel` during cold owner setup.

It rebinds them through hotswap slots.

Cave spawn/despawn, proxy dampening, and collider-fake pressure gates must not poll `GlobalRegistry` service properties.

Voxel rebuild emergency LOD response uses `LODSystemManager.Instance`; it must not read `GlobalRegistry.LODSystem` from the rebuild-budget path.

Abyssal thermal room infiltration caches the gas dynamics dependency.

`AbyssalThermalManager.ApplyThermalInfiltrationToBaseModules` uses `_gasDynamics`, populated during cold owner setup and rebound through `GasDynamicsRuntime`.

It must not poll `GlobalRegistry.GasDynamics` while vent heat is applied to habitat rooms.

Abyssal thermal dependency resolution is cold/hotswap only.

`AbyssalThermalManager.Tick`, `SlowTick`, and `FixedTick` consume cached owner references.

They must not call `ResolveDependencies`, `TryGetComponent`, `AddComponent`, or `BootstrapState.TryGetCurrentPlayerTransform`.

Player runtime rebinding belongs to the `GlobalRegistryServiceSlot.Player` hotswap route through `RebindPlayerRuntimeContext`.

Abyssal thermal damage fallback receivers are cached.

`FixedTick`, `ProcessThermalGameplayTarget`, `QueueBoilingDamage`, and `EmitThermalShock` must not search components to find `IDamageReceiver`.

Player and submarine fallback damage receivers are refreshed during cold dependency resolution or runtime-context hotswap.

The primary route remains `CombatDamageRuntime` registered target ids; cached `IDamageReceiver` fallback is legacy owner-local compatibility only.

Chemical influence snapshot reads are pure.

`ChemicalInfluenceGrid.TryGetPublishedSnapshot`, `TryGetActivePublishedSnapshot`, `TryGetPublishedBreadcrumbs`, `TrySampleNormalizedChannels`, `TrySampleScentGrid01`, `TryFindNearestScentWaypoint`, and `TryGetTuningSnapshot` read only the active buffer-ready runtime instance.

They must not call `EnsureRuntimeInstance`, `PublishFrame`, or `InitializeRuntime`.

Chemical publication belongs to owner/write phases such as `BeginAiFrame`, `SlowTick`, and queued emitter routes.

Procedural wreck generation is an Echelon 2 terrain/worldgen consumer of voxel cave state.

`ProceduralWreckGenerator` must resolve `_voxelEngine` through `WorldRuntimeReferenceUtility.TryResolveVoxelEngine`, not through `GlobalRegistry.VoxelEngine`.

MapMagic vegetation runtime identity is owner-local.

`HectonMapMagicVegetationBridge.ActiveRuntimeInstance` reads the bridge-owned static pointer. This active-instance route must not poll `GlobalRegistry.MapMagicVegetation`.

`WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge` and `MapMagicRuntimeBridge` resolve the vegetation bridge through that owner-local pointer. `GlobalRegistry.MapMagicVegetation` remains a cold registration/unregistration guard only.

`HectonMapMagicVegetationBridge` dependency repair is cold/hotswap only.

`RefreshColdRuntimeDependencies` may run during cold lifecycle and deferred startup bootstrap.

`SlowTick` and `IMapMagicTerrainTileEventListener` callbacks consume cached `MapMagicBridge` and player references.

Player rebinding belongs to the `GlobalRegistryServiceSlot.Player` hotswap route.

Completed tile height readbacks use bulk native copy into the DataVault height buffer.

Per-ushort managed copy loops over full heightmap payloads are rejected in the readback finalizer.

Resource distribution runtime identity is owner-local.

`ResourceDistributionDirector.ActiveRuntimeInstance` reads the director-owned static pointer. It must not poll `GlobalRegistry.ResourceDistribution` from thermal/geology/voxel consumers.

Registry registration remains the cold brine/resource read-model publication route. Lifecycle teardown clears the active pointer before releasing runtime resource buffers.

`ResourceDistributionDirector` worldgen dependency repair is cold/hotswap only.

`SlowTick`, thermal-diamond voxel-face placement, and meteor-impact crater application consume cached player, MapMagic, vegetation, and voxel references.

Runtime repair through `WorldRuntimeReferenceUtility` belongs to cold lifecycle setup or `GlobalRegistryServiceSlot` hotswap rebinding.

Voxel anomaly-to-resource binding uses the resource owner-local active pointer.

`HectonVoxelEngine` must not bind chthonic pillar resources through `GlobalRegistry.ResourceDistribution`.

Geology terrain seam and voxel bridge runtime identities are owner-local.

`WorldGenerativeGeologyTerrainSeamApplier.ActiveRuntimeInstance` and `WorldGenerativeGeologyVoxelBridgeDirector.ActiveRuntimeInstance` read owner-owned static pointers. They must not poll `GlobalRegistry.GeologyTerrainSeam` or `GlobalRegistry.GeologyVoxelBridge`.

Registry registration remains the cold discovery route. Lifecycle teardown clears active pointers before restoring terrains, disposing seam buffers, clearing voxel volumes, or cancelling pending bridge requests.

Procedural field sampler and scatter runtime identities are owner-local.

`WorldProceduralFieldSampler.ActiveRuntimeInstance` and `WorldProceduralScatterDirector.ActiveRuntimeInstance` read owner-owned static pointers. They must not poll `GlobalRegistry.ProceduralFieldSampler` or `GlobalRegistry.ProceduralScatter`.

Registry slots remain cold publication/discovery routes.

Lifecycle and editor reload teardown clear active pointers first.

Then sampling jobs, burst buffers, graphics buffers, scatter backend state, and GPUI visibility may be released.

## Pending Verification

- Add a real terrain payload manifest: world seed, bake revision, graph hash, chunk hash, and sector hash.
- Choose final delivery path for terrain chunk sidecars: validated Addressables groups or a documented binary manifest under StreamingAssets.
- Add behavioral Unity editor tests for terrain hole collider override, terrain hole async finalization, and payload header validation.

## Runtime Job Publication Rule

Vegetation chunk builds, ecosystem threat propagation, abyssal flow fields, thermal grids, thermal-map diffusion, and voxel dynamic nav-grid updates must not use `IJob.Run()` on the terrain runtime path.

Owner phases schedule Burst jobs and store short-lived job state.

Dispatcher swap windows publish completed DataVault snapshots after `JobHandle.IsCompleted`.

Teardown may call `Complete()` before native buffer release.

Teardown, origin-shift, and cache eviction may force-complete only to protect native ownership before releasing buffers.

Scheduled chunk-build jobs must not hold replaceable DataVault views across frames. Threat echo flags are copied into chunk-job-owned TempJob memory and released with `ChunkBuildPendingJob`.

`ThreatPropagationPendingJob`, `FlowFieldPendingJob`, and `ThermalGridPendingJob` own their TempJob snapshots until the late-frame completion path publishes or discards them.
