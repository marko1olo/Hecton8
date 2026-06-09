# Status 1735 - Wreckage Prefab Factory

PROMPT: 1735
DOMAIN: Editor/Environment/Wreckage Prefab Assembly + Voxel Carve Metadata
TASK_COUNT: 23
HYGIENE: Status file is source of truth for this agent. Neighbor tasks ignored after extracting `<AGENT_PROMPT id="1735">`.

## Mandates Selected
- [x] TOOL_Procedural_Wreckage_Generator.txt - DOD: offline generated-asset gate; Rejected: player-runtime mesh/proxy creation; Estimate: 0 us runtime.
- [x] OPT_Zero_GC_Policy_AllocFree_Mandate.txt - DOD: no hot allocations in runtime additions; Rejected: runtime scene scans; Estimate: 0 us steady state.
- [x] OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt - DOD: combine render debris offline; Rejected: draw-call fanout; Estimate: 80-350 us saved per cluttered wreck on MX350-class GPU/CPU.
- [x] REND_URP_Graphics_HotPath_Optimization_HLOD.txt - DOD: SRP Batcher material proof; Rejected: unproven transparent/material-instance paths; Estimate: 25-120 us saved in render setup variance.
- [x] VOX_Voxel_World_Logic_Carving_Persistence.txt - DOD: serialized carve descriptor; Rejected: runtime raycast/terrain search; Estimate: 20-70 us saved per spawn.
- [x] VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt - DOD: box SDF carve metadata, negative/solid convention respected by subtract instruction; Rejected: mesh-accurate carve; Estimate: 150-800 us saved per spawn.
- [x] ARCH_Global_Registry_ServiceLocator_DI_Init.txt - DOD: no GlobalRegistry polling in new runtime scripts; Rejected: hot dependency lookup; Estimate: 2-15 us saved per enable/tick route.
- [x] DBG_Telemetry_Crash_Reporting_PostMortem.txt - DOD: static report and hashes written; Rejected: fake runtime telemetry without factory execution; Estimate: 0 us runtime.

## Loop 1 - Tasks 01-05
- [x] Task 01 WRECKAGE_SCATTER_STATIC_AUDIT - DOD: confirmed no existing `WreckageScatterManager`; added source scanner/test. Rejected: modifying absent code by assumption. Estimate: 0 us runtime, 240 us editor scan.
- [x] Task 02 ROOT_BIBLE_COMPLIANCE_INSPECTION - DOD: read root bibles for authoring/rendering/voxels/performance. Rejected: runtime generation fallback. Estimate: 0 us runtime.
- [x] Task 03 PREFAB_UTILITY_API_ALIGNMENT_INSPECTION - DOD: matched existing `PrefabAssemblerEngine`/`WreckageForgeWindow` `PrefabUtility.SaveAsPrefabAsset` flow. Rejected: raw YAML prefab writes. Estimate: 0 us runtime, 600-1500 us editor save overhead.
- [x] Task 04 SDF_CARVE_VOLUME_MATHEMATICAL_MODELING - DOD: lowest-20-percent yaw OBB with 1m burial expansion. Rejected: full mesh SDF. Estimate: 20-70 us saved per spawn vs runtime bounds analysis.
- [x] Task 05 GLOBAL_REGISTRY_HOT_POLLING_DETECTION - DOD: runtime scripts contain no `GlobalRegistry`; tests assert this. Rejected: registry hot polling. Estimate: 2-15 us saved per runtime route.

## Loop 2 - Tasks 06-10
- [x] Task 06 COMPACTION_FENCE_VULNERABILITY_SCAN - DOD: factory never writes/reads `GlobalDataVault`; carve metadata is serialized. Rejected: direct vault carve publish from prefab. Estimate: 0 us runtime.
- [x] Task 07 TELEMETRY_AND_REPORTING_ARCHITECTURE - DOD: factory keeps in-memory `FactoryReport`; disk JSON is explicit opt-in (`DefaultWriteReportToDisk=false`) to avoid proof-file I/O spam. Rejected: mandatory JSON report per dry-run. Estimate: 0 us runtime.
- [x] Task 08 WRECKAGE_PREFAB_FACTORY_INITIALIZATION - DOD: added `Assets/_Project/Editor/Assembly/WreckagePrefabFactory.cs`. Rejected: extending old forge window with hidden dependencies. Estimate: 0 us runtime.
- [x] Task 09 HIERARCHY_CONSTRUCTION_AND_MATERIAL_BINDING - DOD: builds `VIS_Hull_*`, `VIS_DebrisScatter`, `COL_*`, `TRIG_SalvageNode`; strict Agent 1727 exact-name material resolution for `MAT_Wreckage_Exterior`/`MAT_Wreckage_Burned*`, with no generic charred fallback. Rejected: token-matching `Blackened/Charred` proof materials. Estimate: 25-120 us saved by fewer material routes.
- [x] Task 10 DEBRIS_SCATTER_MESH_COMBINATION - DOD: uses two-pass `Mesh.CombineMeshes` preserving submesh/material buckets. Rejected: one renderer per debris shard. Estimate: 80-350 us saved per cluttered wreck on weak hardware.

## Loop 3 - Tasks 11-15
- [x] Task 11 SDF_CARVE_METADATA_SERIALIZATION - DOD: added `VoxelCarveVolume` and descriptor read API. Rejected: runtime terrain placement sampling. Estimate: 20-70 us saved per spawn.
- [x] Task 12 PRIMITIVE_COLLISION_PROXY_ATTACHMENT - DOD: requires `COL_` proxy; allows Box/Capsule/convex MeshCollider only. Rejected: visual mesh collider and auto fallback collider. Estimate: 100-900 us saved at spawn/import depending on collider complexity.
- [x] Task 13 SALVAGE_NODE_AND_TRIGGER_INJECTION - DOD: attaches `EquipmentMetadata` anchors plus `TRIG_SalvageNode`. Rejected: unmanaged metadata omission. Estimate: 0 us steady state.
- [x] Task 14 ASSET_DATABASE_PREFAB_SERIALIZATION - DOD: `SaveAsPrefabAsset`, generated mesh asset path, output path, failed-save guard. Rejected: direct file writes. Estimate: editor-only.
- [x] Task 15 OFFLINE_PREFAB_VALIDATOR_GATE - DOD: validates renderers, material slots, SRP proof, collision count, carve/scatter/metadata components. Rejected: save-before-validation. Estimate: editor-only.

## Loop 4 - Tasks 16-20
- [!] Task 16 DRY_RUN_VERIFICATION_EXECUTION - BLOCKED BY COMPILER GATE: active `dotnet` and CPU 91.62% on recheck; factory not executed. DOD used: static report and Unity script validator. Rejected: launching competing build/test. Estimate: not measured.
- [x] Task 17 CONTINUOUS_QUALITY_SCALING_INTEGRATION - DOD: `WreckageScatterManager` consumes `HomeostasisBrain.GlobalQualityWeight` with smooth 0..1 weight for cold shadow mode. Rejected: gameplay truth changes by quality. Estimate: 0 us steady state.
- [!] Task 18 BATCHED_COMPILATION_AND_SYNTAX_ASSERTION - PARTIAL: Unity `validate_script` passed 0 errors on Wreckage factory/test/runtime scripts and the adjacent Drone metadata/factory compile blocker. Full dotnet/Unity build blocked by active dotnet + CPU 88%. Rejected: violating build gate. Estimate: validator 0 compile errors.
- [x] Task 19 EXPLICIT_DEBRIS_COUNT_VALIDATION_GATE - DOD: factory fails if debris segment count is zero. Rejected: prefab with loose or missing debris proof. Estimate: 0 us runtime.
- [x] Task 20 COMPACTION_FENCE_RACE_CONDITION_AUDIT - DOD: no vault lock/write path in new runtime code. Rejected: carve publish during compaction. Estimate: 0 us runtime.

## Loop 5 - Tasks 21-23
- [x] Task 21 ZERO_GC_ALLOCATION_PROFILER_MOCK - DOD: runtime source audit forbids `GetComponentsInChildren`, `Mesh.CombineMeshes`, material instantiation, registry polling; hot bodies for `LateFrameTick`/`TryQueueSpawnCarve` scanned clean. Rejected: runtime source discovery. Estimate: 0 GC steady state.
- [x] Task 22 SRP_BATCHER_MATERIAL_LIMIT_TESTING - DOD: static tests require `CBUFFER_START(UnityPerMaterial)` proof route and max 4 material slots. Rejected: unbounded submesh materials. Estimate: 25-120 us render setup saved.
- [x] Task 23 AUTOMATED_METRIC_VALIDATOR_REPORT - DOD: report object includes static metrics, but disk write is opt-in to comply with no-bloated-report directive. Rejected: silent JSON proof generation. Estimate: 0 us runtime.

## Verification
- [x] Codebase audit complete
- [x] Implementation complete
- [!] Compilation checked without violating CPU/compiler gate - Unity script validator: 0 errors/0 warnings on modified 1735 files; full build/test skipped due active dotnet and CPU 82% on latest gate sample.
- [x] Final report appended to Docs/AgentLogs/LOG_1735.md

## Polish Amendment 2026-06-03
- [x] Hot-swap dependency repair - DOD: `VoxelCarveVolume` and `WreckageScatterManager` implement `IGlobalRegistryHotSwapListener`; dispatcher replacement resets late-frame registration flag before retry; voxel runtime replacement re-primes carve bridge. Rejected: `Update` retries or hot `GlobalRegistry.Get<T>()`. Estimate: 0 us steady state, prevents missed spawn carve/presentation when services bind late.
- [x] Editor debris capacity prewarm - DOD: factory constants now prewarm 512 debris segments and 512 combine instances per material. Rejected: dynamic list growth during 500-shard editor stress bake. Estimate: editor-only allocation churn avoided; runtime 0 us.
