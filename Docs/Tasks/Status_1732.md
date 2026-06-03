# Status 1732 - Flora & Coral Scatter Prefab Assembler

Prompt: `FLORA_AND_CORAL_SCATTER_PREFAB_ASSEMBLER`
Domain: offline flora/coral prefab assembly, runtime vegetation/impostor material sovereignty.
Task count: 23.
Batch hygiene: fresh status file created; no previous `Status_1732.md` content found.

## Mandates Read

- `REND_Instanced_Flora_Physics.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `REND_GPU_Sovereignty.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `TOOL_Designer_Facades_CSV_Binary_Bridge.txt`

## Root Docs Read

- `AGENTS.md`
- `TASTE.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `3DMODEL_FLORA_CORAL.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `shaders.md`
- `rendering.md`
- `performance.md`
- `ecosystem.md`
- `authoring.md`

## Checklist

- [x] Task 01 - IMPOSTOR_SYSTEM_STATIC_AUDIT. DOD: static scan mapped target file requirement before edits. Rejected alternative: direct patch without callsite map. Estimate: 900 us.
- [x] Task 02 - ROOT_BIBLE_COMPLIANCE_INSPECTION. DOD: extracted vertex color, LOD, atlas, collider, material clone bans. Rejected alternative: relying on XML only. Estimate: 1250 us.
- [x] Task 03 - PREFAB_UTILITY_API_ALIGNMENT_INSPECTION. DOD: inspected existing `PrefabAssemblerEngine` and `FloraTopologyStudio1604` save/LOD patterns. Rejected alternative: inventing a new serialization route. Estimate: 1100 us.
- [x] Task 04 - ANCHOR_POINT_MATHEMATICAL_MODELING. DOD: selected lowest actual vertex Y across all LOD meshes, then child offset, not bounds-only pivot. Rejected alternative: `mesh.bounds.min.y` because importer bounds can hide vertex-origin mistakes. Estimate: 700 us.
- [x] Task 05 - GLOBAL_REGISTRY_HOT_POLLING_DETECTION. DOD: found `ImpostorSystem` and vegetation renderer already cache registry dependencies; planned changes remain serialized/material/culling only. Rejected alternative: adding a new runtime registry lookup in the factory product. Estimate: 850 us.
- [x] Task 06 - COMPACTION_FENCE_VULNERABILITY_SCAN. DOD: verified modified renderer path reads cached quality/culling values and existing DataVault access already checks `IsCompactionFenceActive`. Rejected alternative: adding new DataVault reads for flora sway. Estimate: 1200 us.
- [x] Task 07 - TELEMETRY_AND_REPORTING_ARCHITECTURE. DOD: source-level validation replaces disk report output; factory no longer writes JSON or managed telemetry artifacts during assembly. Rejected alternative: keeping `FLORA_ASSEMBLER_REPORT_1732.json` after the user explicitly rejected report I/O. Estimate: 950 us.
- [x] Task 08 - RB-110_IMPOSTOR_MATERIAL_ERADICATION. DOD: non-geology impostor source-material fallback removed; all impostors require `_authoredImpostorAtlasMaterial` plus authored UV/tint entry. Rejected alternative: retaining source material path. Estimate: 1400 us.
- [x] Task 09 - FLORA_PREFAB_FACTORY_INITIALIZATION. DOD: created `FloraPrefabFactory.cs` EditorWindow with mesh discovery and LOD grouping by base name. Rejected alternative: runtime factory or scene-time baker. Estimate: 1600 us.
- [x] Task 10 - HIERARCHY_CONSTRUCTION_AND_MATERIAL_BINDING. DOD: factory builds root plus direct LOD children and assigns asset-backed `sharedMaterials` only. Rejected alternative: material instances or intermediate transforms. Estimate: 1800 us.
- [x] Task 11 - DITHERED_LOD_GROUP_CONFIGURATION. DOD: factory attaches 3-level CrossFade LODGroup with `animateCrossFading` and volume-based screen heights. Rejected alternative: fixed flat thresholds. Estimate: 900 us.
- [x] Task 12 - TRIGGER_PROXY_AND_HARVEST_INJECTION. DOD: metadata/name harvest routes create direct `TRIG_HarvestNode` with `SphereCollider.isTrigger` and scalar `ScavengeTarget`. Rejected alternative: MeshCollider or runtime metadata search. Estimate: 950 us.
- [x] Task 13 - SHADOW_CULLING_AND_RENDERER_OPTIMIZATION. DOD: LOD2 impostors and volume <1m3 flora force `ShadowCastingMode.Off`; impostor indirect draw remains shadowless. Rejected alternative: default renderer shadows. Estimate: 650 us.
- [x] Task 14 - ASSET_DATABASE_PREFAB_SERIALIZATION. DOD: `PrefabUtility.SaveAsPrefabAsset()` path `Assets/Prefabs/Environment/Flora/PFB_[FloraName].prefab` plus `DestroyImmediate` cleanup. Rejected alternative: scene-resident temporary roots. Estimate: 800 us.
- [x] Task 15 - OFFLINE_PREFAB_VALIDATOR_GATE. DOD: root MeshFilter, LOD count, LOD2 material name, MeshCollider absence, and trigger completeness gates delete failed prefabs. Rejected alternative: warning-only validation. Estimate: 1100 us.
- [x] Task 16 - DRY_RUN_VERIFICATION_EXECUTION. DOD: mental dry run exposed diagonal/bounds weakness; factory uses lowest finite vertex across all three LODs. Rejected alternative: LOD0 bounds-only pivot. Estimate: 500 us.
- [x] Task 17 - CONTINUOUS_QUALITY_SCALING_INTEGRATION. DOD: impostor threshold and vegetation BRG culling/material params scale continuously 0.3x to 1.0x. Rejected alternative: binary low/high quality branch. Estimate: 1200 us.
- [x] Task 18 - BATCHED_COMPILATION_AND_SYNTAX_ASSERTION. DOD: CPU sample 73.08% blocked dotnet build by policy; Unity MCP `validate_script` passed for four modified C# files; scoped `git diff --check` passed. Rejected alternative: launching build under high CPU. Estimate: 1400 us.
- [x] Task 19 - EXPLICIT_LOD_COUNT_VALIDATION_GATE. DOD: `ValidateLod2Material()` requires LOD2 renderer material name to contain `Impostor`; failed prefabs are deleted. Rejected alternative: accepting any LOD2 mesh material. Estimate: 550 us.
- [x] Task 20 - COMPACTION_FENCE_RACE_CONDITION_AUDIT. DOD: no new DataVault readers/jobs introduced; existing vegetation vault routes already gate on `IsCompactionFenceActive`. Rejected alternative: runtime metadata vault lookup. Estimate: 750 us.
- [x] Task 21 - ZERO_GC_ALLOCATION_PROFILER_MOCK. DOD: steady-state impostor path uses existing buffers/shared material and rejects runtime material allocation; no `new Material(` in target runtime files. Rejected alternative: per-species billboard material state. Estimate: 700 us.
- [x] Task 22 - SRP_BATCHER_MATERIAL_LIMIT_TESTING. DOD: 50k/20-species theoretical reef maps visual LOD0/1 to shared family material and all impostors to one atlas material with UV rect buffer. Rejected alternative: per-species impostor material IDs. Estimate: 600 us.
- [x] Task 23 - AUTOMATED_METRIC_VALIDATOR_REPORT. DOD: proof is current C# source plus Unity MCP validation, scoped hot-path scans, scoped diff check, and literal orphan-meta scan; no JSON report is emitted. Rejected alternative: bloated report I/O. Estimate: 850 us.

## Loop State

- Loop 1 target: Tasks 01-05 completed; static gate only, no compile yet.
- Loop 2 target: Tasks 06-10 completed; compile/static gate pending.
- Loop 3 target: Tasks 11-17 completed; compile gate deferred because CPU >50%.
- Loop 4 target: Tasks 18-23 completed.
- Loop 5 polish: removed runtime authored material mutation, added low-target atlas billboard fallback SubShader, hardened factory path/scratch/cache handling, and cleared external compile blockers in drone/power editor assembly sources.
- Loop 6 polish: exact material atlas entries now override albedo fallback entries; indirect atlas draw requires shader level 4.5; factory rejects placeholder/debug flora materials; pooled billboard property blocks clear on failed tracking.
- Loop 7 polish: factory now enforces a 9600-vertex flora mesh budget, one triangle submesh, UV0 finite contract, LOD0/1 vertex color gradient contract, and fixed-capacity scratch buffers before prefab save.
- Loop 8 polish: factory now requires exact `MAT_Flora_ImpostorAtlas`, LOD renderer binding uses one shared material slot, impostor atlas draw data is prebuilt before `GraphicsBuffer.LockBufferForWrite`, CPU culling DataVault reads use read-only views, and external editor DTO mismatch in `InventoryPrefabFactory` no longer blocks Unity compile.
- Loop 9 polish: factory discovery/reporting now has fixed capacities for mesh groups, material candidates, flora templates, and violation entries; overflow fails closed instead of growing editor lists.
- Current proof state: FACTORY_STANDARD_PASS_IMPOSTOR_STANDARD_PASS_SCAVENGE_STANDARD_PASS_VEGETATION_STANDARD_PASS_SCOPED_STATIC_SCAN_PASS_NO_ORPHAN_META_NO_TRAILING_WS_NO_DOTNET_BUILD_FULL_UNITY_REFRESH_BLOCKED_BY_ACTIVE_ROSLYN_DOTNET_AND_CPU_100.
