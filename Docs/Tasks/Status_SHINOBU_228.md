# SHINOBU_228 Status - BUILDER_TOOL_HOLOGRAPHY_SYNC

Date: 2026-05-20
Domain: Habitat Builder Tool visual representation and placement validation
Assignment Source: `Docs/Tasks/CURRENT_BATCH.md` / `<AGENT_PROMPT id="SHINOBU_228">`
Task Count: 20
Status: POLISH ITERATION 13 STATIC SOURCE GATES / RUNTIME UNITY PROOF PENDING / BUILD HELD BY CPU GUARD AFTER EXTERNAL WALL

## Mandates Read

- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `MATH_AUP_Determinism_Sync.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `DATA_Runtime_Struct_Layout_ARM64.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `REND_GPU_Sovereignty.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt`
- `ARCH_Execution_Phases.txt`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md`

## Pre-Code Analysis

[ANALYSIS]
Target: Replace habitat builder ghost-prefab preview authority with data DTO, Burst math validation, and GPU buffer backed hologram rendering.
Affected systems: `PlayerBuilder`, deleted legacy `PlacementGhost`, `Construction/ShinobuSocketConstructionData.cs`, `Construction/ShinobuSocketConstructionJobs.cs`, `Construction/HectonBlueprintPreviewBatch.cs`, `Hecton_ConstructionDearLieHologram.shader`, project compile include hygiene, editor/static scanners.
Zero GC static source evidence: hot path does not allocate managed arrays, strings, materials, GameObjects, MaterialPropertyBlock, or physics result arrays. DTOs are unmanaged and buffers are Vault/GraphicsBuffer backed. Unity profiler/GCMonitor proof is still pending.
State check: prompt path `Assets/_Project/Scripts/Tools/Builder/` is absent. Actual owner files are root `BuilderTool.cs`, `PlayerBuilder.cs`, and `Construction/` systems. Legacy root `PlacementGhost.cs` is now deleted with its project-file compile include removed.
Rule quote: "VISUAL_SYNC consumes stable simulation snapshots and updates presentation"; "AUP is the only simulation-scale spatial authority"; "MaterialPropertyBlock on standard geometry forbidden; use GraphicsBuffer for GPU Instanced/BRG geometry"; "NativeArray initializations use UninitializedMemory when fully written before read."
[/ANALYSIS]

## Loop 1 - Tasks 01-05

- [x] Task 01 PREFAB_INSTANTIATION_INQUISITION | DOD: `PlayerBuilder.SpawnGhost()` now records preview as data-only pose/flags and contains no retained preview object state; no runtime ghost release route remains. Final placed module spawn is untouched. Rejected: pooled ghost prefab as "cheap enough"; it still mutates transforms/colliders. Estimate: 120-180 us equip spike removed plus GC-risk removal.
- [x] Task 02 PHYSICS_OVERLAP_ERADICATION | DOD: legacy `PlacementGhost` source/prefab route is deleted; preview validity is supplied by Burst SDF/AABB flags. Rejected: `OverlapBoxNonAlloc`; it is non-GC but still broadphase work. Estimate: 60-180 us/frame saved during active preview.
- [x] Task 03 CS1612_HOT_PATH_PROPERTY_ANNIHILATION | DOD: `BuilderGhostStateDTO`, `BuilderGhostVisualDTO`, and telemetry expose raw fields, no DTO properties. Rejected: getter/setter convenience DTOs. Estimate: 5-20 us per job batch avoided from defensive struct copies.
- [x] Task 04 ARM64_GHOST_ALIGNMENT_ASSERTION | DOD: explicit 128B `BuilderGhostStateDTO`, `AUP_TargetPosition` at offset 64, layout asserted in runtime/editor manifests. Rejected: sequential layout and 96B envelope. Estimate: correctness gate; prevents ARM64 unaligned double3 trap.
- [x] Task 05 EMERGENCY_MOCK_VALIDATION_GENERATOR | DOD: `GenerateMockBuilderGhostValidationJob` emits 10,000 deterministic matrices and validation states. Rejected: waiting for scene-only player repro. Estimate: profiling isolation; no claimed runtime saving.

## Loop 2 - Tasks 06-10

- [x] Task 06 BURST_AUP_SNAPPING_KERNEL | DOD: `BuildBuilderGhostStateJob` snaps `double3` AUP before local float matrix creation and stamps deterministic hash. Rejected: snapping runtime `Vector3`. Estimate: 20-45 us/frame target for single preview.
- [x] Task 07 SDF_BASED_VALIDATION_MATH | DOD: `ValidateBuilderGhostPlacementJob` checks all 8 OBB corners against Vault SDF sample lane and existing module AABBs; `GlobalQualityWeight` no longer reduces placement truth. Rejected: trigger/collider truth and quality-throttled legality. Estimate: 40-120 us/frame saved depending module count.
- [x] Task 08 THE_DEAR_LIE_HOLOGRAPHIC_PROJECTION | DOD: preview batch consumes DTOs and renders via `Graphics.DrawProceduralIndirect`; shader reads `StructuredBuffer<BuilderGhostStateRaw>`. Rejected: authored preview renderer hierarchy. Estimate: CPU draw prep under 0.1 ms target.
- [x] Task 09 ASYNCHRONOUS_GPU_UPLOAD_DISPATCHER | DOD: double-buffered `GraphicsBuffer` upload uses `LockBufferForWrite`/`GraphicsBufferUploadUtility.UploadNativeArray`; static scan found no `.SetData(` in target batch. Rejected: `GraphicsBuffer.SetData`. Estimate: 10-60 us stall avoided on MX350.
- [x] Task 10 CONTINUOUS_SCALABILITY_HOLO_MATH | DOD: `GlobalQualityWeight` drives shader scanline/rim/chromatic cost continuously. Rejected: low/high binary shader branch. Estimate: fragment ALU scales down without changing placement truth.

## Loop 3 - Tasks 11-15

- [x] Task 11 SOCKET_MAGNETISM_OVERRIDE | DOD: existing Shinobu socket catalog path remains the magnetism owner; data-only preview carries snapped socket flags into DTO/signal. Rejected: new scene socket scan. Estimate: 20-80 us/frame vs transform search.
- [x] Task 12 AUP_PRECISION_DELTA_MATH | DOD: validation computes AUP center from runtime pose, snaps in `double3`, subtracts runtime origin before float matrix, and AABB overlap uses double delta first. Rejected: absolute world float distance. Estimate: correctness gate at 50 km+.
- [x] Task 13 ROLLBACK_NETCODE_EXCLUSION_FENCE | DOD: flags include `PresentationOnly` and `RollbackExcluded`; docs state no Merkle leaf registration. Rejected: hashing animation phase/preview matrix. Estimate: prevents false desync.
- [x] Task 14 ZERO_INIT_OVERHEAD_BYPASS | DOD: Vault lanes use existing `ResolveHandle` path and unmanaged arrays; jobs fully write active row before read. Rejected: clearing transient preview lanes. Estimate: 1-10 us init/update saved.
- [x] Task 15 TELEMETRY_HOLOGRAPHY_RECORDER | DOD: 300-entry `HolographyTelemetryEntry` ring writes frame/hash/flags/us/SDF count and dumps once to `Dump_SHINOBU_228.bin` on fault. Rejected: chat-only crash explanation. Estimate: forensic coverage, dump threshold >0.5 ms.

## Loop 4 - Tasks 16-20

- [x] Task 16 HOLOGRAPHY_TUNER_WINDOW | DOD: `Builder Tool X-Ray` UI Toolkit window reads telemetry and mutates Vault tuning through `UnsafeUtility.AsRef`. Rejected: runtime UI/string hot path. Estimate: editor-only.
- [x] Task 17 CSV_BUILDER_PROFILES_INGESTOR | DOD: `BuilderHolographyProfileCsv.TryIngest(ReadOnlySpan<byte>, NativeArray<ConstructionSocketTuningDTO>)` uses FNV-1a and manual float parser. Rejected: `string.Split` and `float.Parse`. Estimate: cold boot only.
- [x] Task 18 LIVE_BOUNDS_DEBUG_GIZMO | DOD: `HectonBlueprintPreviewBatch.OnDrawGizmos` draws DTO OBB and corner markers from Vault state. Rejected: collider visualization as proof. Estimate: editor-only.
- [x] Task 19 ARCHITECTURAL_METRIC_VALIDATOR | DOD: `MEMORY_OPTIMIZATION_REPORT.json` has `SHINOBU_228` evidence; editor audit preserves existing report sections. Rejected: overwriting SHINOBU_207 data. Estimate: static proof artifact.
- [x] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | DOD: static scans passed for target files: no `_currentGhostObj`, no runtime ghost-proxy acquire, no `OverlapBoxNonAlloc`, no `DrawMeshInstanced`, no `.SetData(`; guarded `dotnet build Assembly-CSharp.csproj --no-restore /p:UseSharedCompilation=false` reached an existing `Hecton8.Core.csproj` compile wall. Rejected: claiming compile green while sibling-domain symbols and generated project inclusion are broken. Estimate: acceptance gate.

## Iteration Log

- [x] Iteration 1 | Archaeology and local sanitation completed. Prompt re-extracted with strict XML block. Tools/Builder path absent, actual owner files mapped.
- [x] Iteration 2 | Core DTO/jobs/GPU upload reviewed. Added SDF sample Vault lane and per-state sample indexing.
- [x] Iteration 3 | Socket/SDF/AUP precision and telemetry reviewed. Added one-shot holography dump guard to prevent repeated disk writes after first fault.
- [x] Iteration 4 | Editor facade/scanner/docs completed. Static audit menu now avoids destructive overwrite of shared report.
- [x] Iteration 5 | Compile/static audit/self-review completed. Dotnet build intentionally skipped because CPU counter reported 100% and project rule forbids build above 50% CPU.
- [x] Iteration 6 | Ultra polish pass completed. Removed dead runtime ghost-proxy branch, preview shader material scalar mutation, `PlayerToolManager` ghost warmup, and editor bootstrap generation of `PFB_Ghost_*` assets. `BuildableData.ghostPrefab` retained only as legacy serialized compatibility field and authoring now writes it to null.
- [x] Iteration 7 | Subagent audit patch integrated. Removed `PlayerBuilder`'s last ghost object state and null-success validation bypass, added pose-based integrity validation in `HabitatConstructionManager`, moved indirect args into Vault buffer 70945, generated args through Burst, added dynamic draw bounds/frustum guard, cached buffer bindings, rebuilt signal matrices through `BuildBuilderGhostStateJob`, and limited runtime material fallback to editor-only creation. The temporary quality-scaled SDF budget from this iteration was superseded by Iteration 13.
- [x] Iteration 8 | Visual-sync fence polish completed. `HectonBlueprintPreviewBatch` no longer calls direct `Complete()` in `LateFrameTick`, `SetPreview`, signal consumption, or indirect args upload. State/visual/args jobs now publish through a pending `JobHandle`; upload occurs only after `DispatcherJobFence.TryFinalizeCompleted`, with forced completion restricted to disable/destroy teardown.
- [x] Iteration 9 | VR pipe blueprint preview folded into the same data-only holography route. `VRPipeBlueprintPreview` no longer keeps a `Matrix4x4[]` instance cache or calls `DrawMeshInstanced`; `BuildPipeBlueprintPreviewJob` writes cuboid pipe segment matrices, visuals, and indirect args into Vault lanes 70946-70948, then uploads through double-buffered `GraphicsBuffer.LockBufferForWrite` and draws with `Graphics.DrawProceduralIndirect`.
- [x] Iteration 10 | Legacy object-route socket alignment overloads removed from `HabitatConstructionManager`. Static scan now finds no `TryResolveSocketAlignment(`, no `candidateGhost`, and only the pose-based `ScheduleIntegrityValidation(...)` route remains, with `PlayerBuilder` as the sole source caller.
- [x] Iteration 11 | Static audit evidence refreshed. `BuilderHolographyStaticAudit` now checks `VRPipeBlueprintPreview`, legacy object-route deletion, no `.SetData(` across both holography presenters, and replaces the existing `SHINOBU_228` section instead of leaving stale report data. `MEMORY_OPTIMIZATION_REPORT.json` now includes buffer IDs 70945-70948 and VR pipe residue booleans.
- [x] Iteration 12 | Audit-tool false-positive cleanup completed. Forbidden runtime tokens inside `BuilderHolographyStaticAudit` are now composed from split literals, so the SHINOBU_228 `rg` gate reports actual residue only instead of matching the scanner's own search strings.
- [x] Iteration 13 | Source-residue polish completed. SDF validation now always hydrates/evaluates all 8 corners, legacy `PlacementGhost` script and `PFB_Ghost_*` prefabs plus `.meta` files were deleted, `BuildableData.ghostPrefab` references were nulled, stale `Hecton8.Core.csproj` compile include was removed, runtime presenters reject `GlobalDataVault.TryGetLatestCreated` fallback authority, and unchanged preview signal batches skip redundant GPU uploads.

## Verification

- `git diff --check` target files: PASS; output only CRLF normalization warnings.
- Forbidden hot-path scan: PASS; no `TryAcquireGhostProxy`, `TryCreateGhostProxy`, `TryGetGhostProjectionResources`, `H8_RuntimeGhostProxy`, `ReleaseGhostProxy`, `OverlapBoxNonAlloc`, `DrawMeshInstanced`, `.SetData(`, preview ghost pool spawn, runtime ghost warmup, or new `MaterialPropertyBlock` in target files.
- Preview material mutation scan: PASS; no `SetFloat`, `SetColor`, `SetInt`, `_H8BuilderGhostCount`, `_BaseColor`, `_H8SnapDampen`, or `_H8GlobalQualityWeight` in `HectonBlueprintPreviewBatch` or `Hecton_ConstructionDearLieHologram.shader`.
- Editor bootstrap scan: PASS; no `CreateGhostPrefab`, `PFB_Ghost_*`, `Mat_BuildGhost_*`, or `AddComponent<PlacementGhost>` generation path remains. Existing `BuildableData.ghostPrefab` field is legacy schema only.
- Indirect GPU scan: PASS; `Graphics.DrawProceduralIndirect` and `StructuredBuffer<BuilderGhostStateRaw>` present.
- Layout evidence: PASS by source assertions. `BuilderGhostStateDTO`: LocalToWorld 0..63, AUP_TargetPosition 64..87, PrefabHashID 88..91, ValidationFlags 92..95, AnimationPhase 96..99, ValidationStateHash 100..103, `_pad0.._pad5` 104..127; final size 128B.
- Data-only validation scan: PASS; no `_currentGhostObj`, `_currentGhost`, `_ghostSocketBuffer`, `_snappedGhostSocket`, `ReleaseLegacyGhostObject`, `CacheGhostSockets`, `IsCurrentGhostCollider`, or `TryResolveSocketAlignment(` remains in `PlayerBuilder.cs`.
- Indirect args route: PASS; `BuilderGhostIndirectArgsDTO` is Vault-backed under buffer 70945 and written by `BuildBuilderGhostIndirectArgsJob`.
- Visual-sync fence scan: PASS; no direct `.Complete(`, `JobHandle.Complete`, `UploadArgs(`, or `_buffersDirty` remains in `HectonBlueprintPreviewBatch.cs`.
- VR pipe preview scan: PASS; `VRPipeBlueprintPreview.cs` has no `DrawMeshInstanced`, `Matrix4x4[]`, `_matrices`, `.SetData(`, direct `.Complete(`, `JobHandle.Complete`, or `new MaterialPropertyBlock`. It uses Vault buffers 70946 `BuilderGhostStateDTO[64]`, 70947 `BuilderGhostVisualDTO[64]`, and 70948 `BuilderGhostIndirectArgsDTO[1]`.
- Legacy object route scan: PASS; no `TryResolveSocketAlignment(`, no `candidateGhost`, and no `ResolveSocketYawRotation(` remains in `HabitatConstructionManager.cs` or `PlayerBuilder.cs`.
- Static audit report: PASS; `Docs/Reports/MEMORY_OPTIMIZATION_REPORT.json` records no VR pipe mesh instancing/matrix array, no object alignment route, and Vault IDs 70940-70948.
- Final forbidden-token scan: PASS; no `DrawMeshInstanced`, `Matrix4x4[]`, `_matrices`, `.SetData(`, direct `.Complete(`, `JobHandle.Complete`, `TryResolveSocketAlignment(`, `candidateGhost`, or `ResolveSocketYawRotation(` remains in SHINOBU_228 target files after audit-tool string split cleanup.
- Legacy ghost source/asset/project scan: PASS; no `PlacementGhost`, no `PFB_Ghost_*`, no non-zero `ghostPrefab` references under `Assets/_Project` source/prefab/asset/meta scope, and no `PlacementGhost.cs` compile include remains in `Hecton8.Core.csproj`.
- SDF truth scan: PASS; no `qualitySampleLimit`, no 2-corner `ResolveCandidateBudget(quality, 2, BuilderGhostSdfCornerCount)`, and no runtime hydration call to `ResolveBuilderGhostSdfSampleCount` remain.
- Vault authority scan: PASS for runtime presenters; no `TryGetLatestCreated` remains in `HectonBlueprintPreviewBatch.cs` or `VRPipeBlueprintPreview.cs`.
- Orphan meta scan: PASS for deleted `PlacementGhost.cs` and `PFB_Ghost_*` assets; no orphan `.meta` files found in the touched script/ghost-prefab directories.
- Build: HELD. One earlier guarded build was launched only after CPU 5.5% and no dotnet/csc processes. It failed in `Hecton8.Core.csproj` with pre-existing/generated-project dependency errors: missing `Hecton8.Equipment`, missing `Hecton8.Logistics.Grid`, missing `SoundEmissionSignal`/audio interface members, missing `MethodImplAttribute`, missing service bridge types, and `SocketDefinitionDTO` unresolved because `BaseModuleCatalogRuntime.cs` is not included in `Hecton8.Core.csproj` while `HabitatGraphManager.cs` already references it. Latest guard after Iteration 13 sampled CPU 54% with dotnet/csc count 0, so no second build was launched.
