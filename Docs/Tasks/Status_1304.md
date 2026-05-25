# Status 1304 - MEMORY_SOVEREIGN_WORLD_VOXEL_EXORCIST

Date: 2026-05-25
Domain: Assets/Project/Scripts/World/Voxel
Task Count: 20
Batch Source: Docs/Tasks/CURRENT_BATCH.md <AGENT_PROMPT id="1304">
Prompt Re-Extraction: bytes=21853, tasks=20, sha256=e93d536283b5c370d3ce6f26b4728ff8adff4fc45bc007f1072020bd36f728df
Status: PENDING_VERIFICATION_APEX_LOOP29_EXCEPTION_ROUTE_REDUCTION

## Domain Path Resolution

- Requested path `Assets/Project/Scripts/World/Voxel`: missing on disk.
- Effective first-party root per AGENTS.md: `Assets/_Project`.
- Effective voxel scan scope: first-party voxel/SDF files under `Assets/_Project/Scripts`, excluding third-party folders and non-voxel world systems.

## Mandates Selected Before Coding

- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- ARCH_Signal_Lane_Segregation.txt
- ARCH_Execution_Phases.txt
- VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt

## Phase 0 Checklist

- [x] Task 01: EXHAUSTIVE_NATIVE_ALIAS_INQUISITION | Justification: prebuilt Roslyn scanner parsed 2413 first-party files with 0 parse failures and emitted `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1304_FULL.json`; strict `VoxelSurfaceNets` folder emitted `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1304_VOXEL_SURFACE_NETS.json` | DOD practice: AST field scan distinguishes fields from locals/job carriers | Alternative rejected: grep-only alias count | Estimate: 0 runtime us, offline scan only
- [x] Task 02: OWNERSHIP_PROVENANCE_AND_LIFECYCLE_MAPPING | Justification: mapped sonar SDF to `SystemID.WorldStreaming` and `BufferID.VoxelSdfTexture3D/VoxelSdfPayloadDescriptor`, carve pools to `SystemID.TerrainSeams` and `ShinobuDeltaCrusher*`, meshing scratch to `HectonVoxelEngine` cold scratch | DOD practice: owner/allocator/job/consumer trace before deletion | Alternative rejected: deleting arrays without buffer ownership proof | Estimate: expected low-end save 10-80 us/frame after migration in carve/compaction frames, pending profiler proof
- [x] Task 03: DEPENDENCY_GRAPH_IMPACT_ANALYSIS | Justification: identified public SDF read model, GPR consumer, raymarch/sample methods, compaction lease usage, and VoxelSurfaceNets phase-local view aggregate | DOD practice: call graph and public accessor audit | Alternative rejected: assuming consumers are local to voxel volume | Estimate: expected crash-risk reduction, CPU estimate pending migration
- [x] Task 04: DTO_LAYOUT_EXTRACTION_AND_VERIFICATION | Justification: verified `VoxelSdfPayloadDescriptorDTO` expanded to 80B explicit with audio-material lane fields at offsets 64/68/72 and pad at 76; `VoxelModifiedCell` 8B explicit, `VoxelCarveTelemetryEntry` 80B explicit, `VoxelBlackBoxDumpHeader` 32B explicit, and flagged sequential mesh-upload structs for later guard | DOD practice: explicit unmanaged DTO layout ledger | Alternative rejected: trusting Sequential layout for vault DTOs | Estimate: 0 runtime us now; prevents ARM64 trap/fallback risk
- [x] Task 05: TELEMETRY_RING_INTEGRATION_PLANNING | Justification: planned 64B memory-sovereignty entry and reuse/correction path for existing `ShinobuDeltaCrusherVoxelBlackBox`; dump target must become `Docs/AgentLogs/Dump_1304_Voxel.bin` | DOD practice: fixed-size unmanaged ring and cold binary dump | Alternative rejected: managed string logs/exceptions in hot failure branches | Estimate: 0 runtime us in Phase 0; hot write target <= 1 us when implemented

## Phase 1 Checklist

- [x] Task 06: VAULT_DESCRIPTOR_SUBSTITUTION | Justification: removed 3 persistent GPU upload `NativeArray` aliases, removed 4 private `HectonVoxelVolume` sonar snapshot/build `NativeArray<byte>` fields, and replaced `VoxelDeltaProcessor` private `NativeQueue<VoxelCarveEvent>` with vault-owned `BufferID.ShinobuDeltaCrusherCarveEventQueue` ring state | DOD practice: GlobalDataVault owns cross-domain buffers, object stores metadata only | Alternative rejected: reusing save material buffers, dropping audio material ids, or retaining a scene-lifetime queue | Estimate: stale-alias risk removed; CPU delta pending profiler
- [x] Task 07: COLD_BOOT_BUFFER_REGISTRATION | Justification: `OnEnable` and DataVault hot-swap route call `TryEnsurePublishedSonarVaultPayloadCapacity`, which now ensures descriptor, encoded SDF, and audio-material vault buffers | DOD practice: cold owner registration before read access | Alternative rejected: lazy allocation inside read accessors | Estimate: read path 0 allocation; cold registration cost off hot sample path
- [x] Task 08: PHASE_LOCAL_VIEW_RESOLUTION | Justification: GPU locked buffer views are method-local; sonar read methods resolve read-only vault handles per call; long-running compaction source copy takes an explicit `TryLockBuffer(BufferID.VoxelSdfTexture3D, SystemID.TerrainSeams)` lease; queued carve ring and scheduled compaction output are resolved only inside active phase methods | DOD practice: no object-stored mutable native views for newly fixed paths | Alternative rejected: double-buffered private scene arrays | Estimate: relocation-safety gain, CPU estimate pending runtime profiler
- [x] Task 09: IRONCLAD_TRY_FINALLY_LOCKING | Justification: sonar descriptor/audio/SDF writes, queued carve write-locks, and failed/stale compaction read leases now release through `finally` or explicit fail-closed release branches | DOD practice: every new write/read lease path has a visible release route | Alternative rejected: convention-only release after branch logic | Estimate: 0 us normal path, avoids full-frame lock stalls on failure
- [x] Task 10: BURST_JOB_SIGNATURE_RECONCILIATION | Justification: compaction and SurfaceNets jobs still receive direct phase-local `NativeArray<T>` views, but owner objects no longer cache those views; `ChunkDeltaState` resolves pool subarrays at schedule/use phase and `VoxelSurfaceNetsVaultBuffers` resolves from vault handles | DOD practice: handles stay out of jobs, physical views are created only immediately before scheduling or copy | Alternative rejected: passing vault handles into Burst jobs | Estimate: 0 managed GC, added vault resolve cost only at phase boundaries
- [x] Task 11: READ_ACCESSOR_PURIFICATION | Justification: dead throwing SurfaceNets accessors removed; `TryGetPublishedSonarSdfPayload` now reads from vault descriptor and read-only generation handles instead of private snapshots | DOD practice: read accessors do not allocate, publish, or mutate | Alternative rejected: clearing or growing buffers during read | Estimate: 0 managed GC on read path
- [x] Task 12: EXPLICIT_DTO_REFACTORING | Justification: `VoxelSdfPayloadDescriptorDTO` explicit layout is now 80B, multiple of 8, with all added audio fields 4-byte aligned and `_pad0` at offset 76 | DOD practice: manual FieldOffset ledger | Alternative rejected: second unmanaged descriptor type with another cross-domain route | Estimate: 0 runtime us; ABI proof artifact updated
- [x] Task 13: SCALABILITY_WEIGHT_PRESERVATION | Justification: queued carve drain and scheduled commit cadence resolve from continuous `HomeostasisBrain.GlobalQualityWeight`; SurfaceNets tuning stores continuous `GlobalQualityWeight` and clamps cadence without binary device switches | DOD practice: float quality scalar controls budget/cadence, not DTO authority | Alternative rejected: `isLowEnd`/tier branches | Estimate: 0 managed GC; CPU scales 1-4 queued carves/frame and 1-2 SurfaceNets chunks/frame
- [x] Task 14: TELEMETRY_RING_IMPLEMENTATION | Justification: `VoxelDeltaProcessor` owns a 300-entry vault-backed blackbox ring and writes explicit 80B `VoxelCarveTelemetryEntry` structs on invalid carve, queue overflow, pending carve corruption, commit-budget failure, and dump triggers | DOD practice: unmanaged struct copy into fixed ring | Alternative rejected: managed string logs in failure branches | Estimate: hot write target <= 1 us; measured profiler proof unavailable
- [x] Task 15: BLACKBOX_DUMP_ROUTING | Justification: SurfaceNets and `VoxelDeltaProcessor` dump routes now point to `Docs/AgentLogs/Dump_1304_Voxel.bin` | DOD practice: agent-owned fixed binary dump path | Alternative rejected: leaving SHINOBU/1312 dump route drift | Estimate: 0 us normal path, crash-path forensic ownership fixed

## Phase 2 Checklist

- [ ] Task 16: MOCK_VOXEL_STRESS_HARNESS | BLOCKED BY DEPENDENCY: `dotnet build Hecton8.Core.csproj --no-restore --nologo` fails outside voxel domain: 2 `CS0122` in `Audio/AcousticPortalPropagation.cs` and 16 `CS0177` in `TetherInstance.cs`
- [x] Task 17: DEFRAGMENTATION_RACE_CONDITION_FUZZER | Justification: added editor-only `VoxelMemorySovereigntyValidator1304.RunDefragRaceFuzzer`, which mutates carve/density vault buffers under write locks while a background reader probes generations and compaction fences | DOD practice: stress stale-handle/lock discipline in editor, not runtime | Alternative rejected: live gameplay thread fuzzer | Estimate: 0 runtime us; execution pending Unity Editor because current shell build guard is blocked by CPU >50%
- [x] Task 18: ARM64_ALIGNMENT_VALIDATOR_INTEGRATION | Justification: added editor validator for public SurfaceNets/GPR DTOs plus `VoxelDeltaProcessor.ValidateAgent1304PrivateLayouts`; checks `UnsafeUtility.SizeOf`, `StructLayoutAttribute`, and field offsets | DOD practice: fail compile/editor load on layout drift | Alternative rejected: markdown-only byte map | Estimate: 0 player runtime us; editor-only cold validation
- [x] Task 19: ZERO_GC_HOT_PATH_VERIFICATION | Justification: targeted production text scans report 0 hits for `string.Format`, `.ToString(`, LINQ query methods, `foreach`, `StringBuilder`, `Enumerable`, and `new string`; one interpolation remains editor-only in `VoxelDeformationSmokeTester.DescribeStatus`; targeted Roslyn filter reports 0 forbidden persistent native alias candidates in touched voxel files; two `new NativeArray<byte>` calls remain as `Allocator.TempJob` transient publish scratch in `PublishSonarSdfSnapshotAsync` and are disposed in `finally` | DOD practice: static text proof plus AST proof, no chat-only claim | Alternative rejected: claiming no native allocations at all | Estimate: 0 managed GC on verified hot paths
- [x] Task 20: AUTOMATED_METRIC_VALIDATOR_REPORT | Justification: emitted `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1304_FULL_FINAL.json` and `Docs/Reports/VAULT_EXORCISM_REPORT_1304.json` | DOD practice: machine-readable proof artifact plus scanner hash | Alternative rejected: chat-only report | Estimate: 0 runtime us

## Current Loop

Loop 10/5: APEX recheck re-extracted prompt id 1304, performed a no-build line review, found that `WriteBlackBoxSample` wrote the vault-backed blackbox ring through a resolved `NativeArray` without `TryAcquireWriteLock`, and fixed it with `TryAcquireBlackBoxBuffer`/`ReleaseBlackBoxBuffer` plus `finally` release in both sample write and dump export. The lock helper now re-ensures the handle only after stale/invalid resolve proof, not after write-lock contention. Project build was deliberately not rerun per user instruction to avoid repeated `dotnet build`; only the existing hotpath AST auditor was rerun.

## Verification

- Roslyn scan: PASS for parse, 0 parse failures.
- Full first-party AST recheck: 2418 files, 7462 native fields, 1866 forbidden candidates, 0 parse failures, hash `3e5f22573f34c97959fb1b089f0ec6c5db9573169bdfae4bf19563a8c76935fe`.
- SurfaceNets AST recheck: 6 files, 29 native fields, 0 forbidden persistent candidates, 0 forbidden MonoBehaviour candidates, hash `8ff30cf64e12439d0c052e090b83b045812f7aa2c1ecec0ae135314c31344baa`.
- Targeted Roslyn filter on `HectonVoxelVolume.cs`, `VoxelDeltaProcessor.cs`, `GroundRadarContracts.cs`, `H8Memory.cs`, `VoxelDeformationSmokeTester.cs`, and `World/VoxelSurfaceNets`: `TARGET_FORBIDDEN_COUNT=0`.
- Remaining target-scope AST candidates: none in the filtered production files. Full first-party still has 1866 forbidden candidates outside agent 1304 domain.
- Runtime hotpath AST audit: files=8, parseFailures=0, objectCreations=289, managedRiskCreations=102, nativeTempJobAllocations=12, nativePersistentAllocations=15, `string.Format=0`, `.ToString()=0`, LINQ calls=0, `foreach=0`, interpolated strings=0, string concat suspects=0, hash `1be6a51baa78e95279ecf78868d210469b0e41b6c8969a5329467007909b8ad4`.
- Target route scan: 0 hits for `Agent1312`, `Dump_1312`, or `Dump_SHINOBU` in modified target files after route correction.
- Blackbox lock scan: `TryResolveBlackBox=0`; blackbox writes use `TryAcquireWriteLock(in _blackBoxHandle, SystemID.TerrainSeams)` and `ReleaseWriteLock` in `finally`.
- Native allocation classification: `HectonVoxelVolume` has two cold `Allocator.TempJob` publish scratch arrays disposed in `finally`; `VoxelDeformationSmokeTester` has TempJob smoke-test arrays; `H8Memory` has core-memory-authority Persistent allocator state; target voxel production owner fields remain 0 by Roslyn filter.
- `git diff --check`: PASS, only CRLF normalization warnings.
- `VoxelDeltaProcessor` preprocessor balance: depth=0, minDepth=0.
- Compile: NOT RERUN IN LOOP 10. Last guarded build remains the known external block: 21 errors outside 1304 domain, 2 `CS0122` in `Assets/_Project/Scripts/Audio/AcousticPortalPropagation.cs`, 16 `CS0177` in `Assets/_Project/Scripts/TetherInstance.cs`, and 3 `CS0246` in `TetherInstance.cs`.
- C# production mutation: `VoxelSurfaceNetsGpuUploadDispatcher.cs`, `VoxelSurfaceNetsVault.cs`, `HectonVoxelVolume.cs`, `GroundRadarContracts.cs`, `H8Memory.cs`, `VoxelDeltaProcessor.cs`, `VoxelDeformationSmokeTester.cs`.

## Loop 11 APEX Addendum

Loop 11/5: reread prompt/status/rationale, found that SurfaceNets job/GPU upload paths still resolved vault-owned `NativeArray` views without a DataVault relocation pin held for the full job/fence window. Added explicit source/job leases, made old unsafe `TrySchedule*` entrypoints fail closed, required callers to release pinned leases after completion, and kept GPU source lease in `_pendingSourceLease` until `TryFinalizeUpload`/release. Same pass found proof-route drift in `VoxelDeltaProcessor`: `VoxelBlackBoxDumpRelativePath` and private layout validator helper names still referenced 1312. Corrected to 1304. No `dotnet build` was launched.

## Loop 11 Verification

- Target route scan after patch: 0 hits for `Agent1312`, `Dump_1312`, or `Dump_SHINOBU` in `VoxelDeltaProcessor.cs`, `HectonVoxelVolume.cs`, and `World/VoxelSurfaceNets`.
- Managed text scan after patch: runtime target hits for `string.Format`, LINQ methods, `foreach`, `new string`, and `StringBuilder` are 0; only `.ToString(...)` hits are editor-only in `World/VoxelSurfaceNets/Editor/VoxelMeshTunerWindow.cs:101-106`.
- Native lock scan after patch: SurfaceNets GPU upload source now uses `TryAcquireGpuUploadSourceLease`/`ReleaseGpuUploadSourceLease`; SurfaceNets scheduled jobs use `TryScheduleMockDensityPinned`, `TryScheduleExtractionPinned`, and `TryScheduleHzbCullPinned` with `VoxelSurfaceNetsJobBufferLease`.
- Blackbox route proof: `VoxelDeltaProcessor.cs:140` now points to `Docs/AgentLogs/Dump_1304_Voxel.bin`; `ValidateAgent1304PrivateLayouts` no longer delegates to `Agent1312` helpers.
- Brace/preprocessor coarse check: `VoxelDeltaProcessor.cs` brace depth=0, minDepth=0.
- `git diff --check` on touched target C# files: PASS, only CRLF normalization warnings.
- Compile: NOT RERUN IN LOOP 11 by user instruction. Existing known build wall remains external Audio/Tether from previous guarded build.

## Loop 12 APEX Addendum

Loop 12/5: reread prompt/status/rationale and rechecked the exact code paths modified by the previous SurfaceNets pin pass. Found two release-critical defects. First, SurfaceNets pinned schedules locked output buffers only as read pins even when jobs wrote into density, vertices, indices, state, telemetry, priority, and GPU argument buffers. Added a separate write-lock mask to `VoxelSurfaceNetsJobBufferLease` and release order now unlocks write leases before read pins. Second, AUP crater flow still stored crater stamp centers as `float3` and still downcasted some absolute MapMagic bridge coordinates directly. Converted `VoxelCraterStamp.position` to explicit-layout `double3`, moved crater/cluster collapse comparisons to double-space, and isolated remaining absolute `Vector3` downcasts behind `TryDowncastAbsoluteAupForLegacyMapMagicBridge` with finite/range fail-closed checks. Corrected `HectonVoxelEngine` mesh blackbox route to `Docs/AgentLogs/Dump_1304_Voxel.bin`.

## Loop 12 Verification

- Prompt re-extraction: bytes=21853, tasks=20, sha256=`e93d536283b5c370d3ce6f26b4728ff8adff4fc45bc007f1072020bd36f728df`.
- SurfaceNets write-lock map: mock density writes `Density`; extraction reads `Density`, `Tuning`, `SurfaceEdgeMasks` and writes `Vertices`, `Indices`, `CellVertexMap`, `States`, `TelemetryRing`, `TelemetryCursor`, `RawDebugVertices`, `IndirectArgs`; HZB cull reads `HzbTiles` and writes `ChunkAabbs`, `Priorities`.
- Route scan after fixes: no target hits for `Agent1312`, `Dump_1312`, or `Dump_SHINOBU`; target 1304 routes are `VoxelDeltaProcessor.cs:140` and `VoxelSurfaceNetsVault.cs:148`, with mesh pipeline route corrected in `HectonVoxelEngine.cs`.
- Managed text scan: runtime target hits for `string.Format`, LINQ methods, `foreach`, `new string`, `StringBuilder`, and runtime `.ToString(` remain 0. Only `.ToString(...)` hits are editor UI in `World/VoxelSurfaceNets/Editor/VoxelMeshTunerWindow.cs:101-106`.
- Native allocation scan: `HectonVoxelEngine` retains cold authority/scratch native allocations and cave generation collections; `HectonVoxelVolume` retains two cold `Allocator.TempJob` publish scratch arrays disposed in `finally`; no SurfaceNets persistent native owner field was reintroduced.
- Roslyn native alias scan before the final AUP patch: full first-party files=2418, parseFailures=0, totalFields=7457, forbiddenPersistentCandidates=1860, jobTransientFields=5532, coreMemoryAllowedFields=46, hash `0f684a132f703c4945266fc81a706cc8920fae780a28105c12cbf8e156586fff`; SurfaceNets files=6, parseFailures=0, totalFields=29, forbiddenPersistentCandidates=0, hash `8ff30cf64e12439d0c052e090b83b045812f7aa2c1ecec0ae135314c31344baa`.
- Targeted Roslyn filter from that scan: strict target forbidden persistent native alias candidates=0; `HectonVoxelEngine` forbidden persistent native alias candidates=0.
- AUP proof after patch: crater DTO stores `double3`; runtime effects use `HectonFloatingOrigin.ToRuntimePosition(double3)` after double-origin subtraction; cluster bounds/collapse distances compare `double3` values; legacy MapMagic bridge downcasts only through finite/range-checked `TryDowncastAbsoluteAupForLegacyMapMagicBridge`.
- Remaining direct absolute `ToVector3(double3)` route scan hit: `HectonVoxelEngine.cs:7013` inside the explicit legacy MapMagic bridge only.
- DTO map added to editor validator: `VoxelCraterStamp` explicit size 32, `position` offset 0, `radius` offset 24, `blendRadius` offset 28.
- Brace balance: `HectonVoxelVolume.cs`, `HectonVoxelEngine.cs`, `VoxelMemorySovereigntyValidator1304.cs`, `VoxelSurfaceNetsVault.cs`, `VoxelDeltaProcessor.cs`, and `VoxelSurfaceNetsGpuUploadDispatcher.cs` all reported depth=0, minDepth=0.
- `git diff --check` on touched target C# files: PASS, only CRLF normalization warnings.
- Roslyn scanner and `dotnet build` were not rerun after the final AUP patch because `Get-CimInstance Win32_Processor` reported `LoadPercentage=100`; user instruction explicitly forbids build/dotnet while CPU is under load. Last known compile wall remains external Audio/Tether from the previous guarded build.

## Loop 12B Route Regression Correction

- A post-log route grep immediately found that `VoxelDeltaProcessor.cs` still had `Dump_1312_VoxelPaging.bin` at line 140 and `Agent1312` private layout helper names around lines 5837-5912.
- Code was corrected after that failed grep. Current route grep result: 0 hits for `Agent1312`, `Dump_1312`, or `Dump_SHINOBU` in `VoxelDeltaProcessor.cs`, `HectonVoxelVolume.cs`, `HectonVoxelEngine.cs`, and `World/VoxelSurfaceNets`.
- Current proof lines: `VoxelDeltaProcessor.cs:140` is `Docs/AgentLogs/Dump_1304_Voxel.bin`; `VoxelDeltaProcessor.cs:5835` owns `ValidateAgent1304PrivateLayouts`; helper names are `AssertAgent1304ExplicitLayout` and `AssertAgent1304Offset`.
- `VoxelDeltaProcessor.cs` brace check after the correction: depth=0, minDepth=0.

## Loop 13 APEX Verification

- Prompt re-extraction repeated: bytes=21853, tasks=20, sha256=`e93d536283b5c370d3ce6f26b4728ff8adff4fc45bc007f1072020bd36f728df`.
- Source route grep repeated after the actual source patch: 0 hits for `Agent1312`, `Dump_1312`, or `Dump_SHINOBU` in `VoxelDeltaProcessor.cs`, `HectonVoxelVolume.cs`, `HectonVoxelEngine.cs`, and `World/VoxelSurfaceNets`.
- Full Roslyn native alias audit rerun once under CPU guard: files=2418, parseFailures=0, totalFields=7485, forbiddenPersistentCandidates=1853, jobTransientFields=5567, coreMemoryAllowedFields=46, hash `5cd22b5639e7b1ec8f3fce43019a95220ef62ad7e8f4280c4ab7aa49139cf666`; output `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1304_APEX_LOOP13_FULL.json`.
- Strict target filter from Loop 13 audit: `strictTargetForbidden=0` for `VoxelDeltaProcessor.cs`, `HectonVoxelVolume.cs`, `GroundRadarContracts.cs`, `H8Memory.cs`, and `World/VoxelSurfaceNets/*`.
- `HectonVoxelEngine.cs` Loop 13 filter: `HectonVoxelEngineForbidden=0`. Residual native scratch arrays in that file are classified by scanner as non-forbidden/core-authorized/job-transient scratch, not claimed as removed.
- SurfaceNets Roslyn audit rerun: files=6, parseFailures=0, totalFields=29, forbiddenPersistentCandidates=0, jobTransientFields=29, coreMemoryAllowedFields=0, hash `8ff30cf64e12439d0c052e090b83b045812f7aa2c1ecec0ae135314c31344baa`; output `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1304_APEX_LOOP13_SURFACENETS.json`.
- Managed token scan over runtime target sources: runtime `string.Format`, LINQ methods, `foreach`, `new string`, `StringBuilder`, and runtime `.ToString(` hits are 0; remaining `.ToString(...)` hits are editor UI only in `World/VoxelSurfaceNets/Editor/VoxelMeshTunerWindow.cs:101-106`.
- Added-line native allocation scan still shows two cold `Allocator.TempJob` `NativeArray<byte>` publish scratch arrays in `HectonVoxelVolume.cs:2058` and `HectonVoxelVolume.cs:2067`. These are locals disposed in `finally`, not persistent fields; not a hot managed-GC route.
- `git diff --check` on touched target files and reports: PASS, only CRLF normalization warnings.
- `dotnet build` was not run in Loop 13. One AST scanner pass was run under CPU guard; build remains last-known blocked by external Audio/Tether compile wall.

## Loop 14 APEX Managed-New Boundary Scan

- Runtime source route grep repeated: 0 hits for `Agent1312`, `Dump_1312`, or `Dump_SHINOBU` in the target source set.
- Managed token scan repeated with `--glob '!**/Editor/**'`: runtime target hits for `string.Format`, `.ToString(`, `Enumerable.`, `System.Linq`, `StringBuilder`, `new string`, `foreach`, `throw new`, `new Task`, `new Thread`, and `new Exception` are 0.
- Same managed scan found cold managed Unity-reference infrastructure: `HectonVoxelEngine.cs:47` `_initLock = new object()`, `HectonVoxelEngine.cs:3047/3053` static `List<>` deferred PhysX queues, `HectonVoxelEngine.cs:3338/3340/3342` active-volume `List<>` registries, and `HectonVoxelVolume.cs:339` published-volume `List<>` registry.
- Verdict: hotpath managed string/LINQ/boxing tokens remain clean, but absolute "no managed new anywhere in runtime code" is not true while Unity-object registries store managed references. Safe unmanaged correction requires a dedicated bridge from Unity references to stable instance-id/GDV descriptors; not patched in this pass because it changes ownership semantics and would need controlled build/test.
- Native allocation boundary remains unchanged: two local `Allocator.TempJob` sonar publish scratch arrays in `HectonVoxelVolume.cs:2058` and `HectonVoxelVolume.cs:2067`; cold `DataVaultExempt*` scratch/table storage in `HectonVoxelEngine.cs`; SurfaceNets forbidden persistent candidates remain 0 by Loop 13 AST.

## Loop 15 APEX Managed Container Reduction

- Re-read prompt/status/rationale before edits; prompt hash remains `e93d536283b5c370d3ce6f26b4728ff8adff4fc45bc007f1072020bd36f728df`.
- Removed `HectonVoxelVolume` published SDF `List<HectonVoxelVolume>` container and replaced it with an intrusive linked registry: `s_activePublishedHead`, `_publishedNext`, `_publishedPrev`, `_publishedRegistered`, and `s_activePublishedVolumeCount`.
- Removed unused `System.Collections.Generic` from `HectonVoxelVolume.cs`.
- Removed `MCTables` cold `new object()` lock and replaced it with an `Interlocked.CompareExchange` gate on `_initGate` plus `SpinWait`.
- Post-edit scan: `s_activePublishedVolumes`, `new List<HectonVoxelVolume>`, and `_initLock` are 0 hits in target files.
- Remaining managed/new/native boundary after Loop 15: `HectonVoxelEngine` still has deferred PhysX `List<>`, active-volume `List<>`, `_streamingScratchGate = new object()`, cold `new int[]` Marching Cubes seed arrays, `DataVaultExempt*` NativeCollections; `VoxelDeltaProcessor` still has `_chunkStateFreeStack = new int[DirtyChunkStatePoolCapacity]`; `HectonVoxelVolume` still has `_terrainHoleHandles = new int[MaxTerrainHoleHandleCount]` and leak-sentinel managed arrays.
- `dotnet build` and Roslyn AST were not run in Loop 15: CPU load was 62 (>50), and the operator explicitly forbids build/dotnet while CPU is under work.

## Loop 16 APEX Managed Allocation Reduction

- Re-read prompt/status/rationale before edits; prompt hash remains `e93d536283b5c370d3ce6f26b4728ff8adff4fc45bc007f1072020bd36f728df`.
- Replaced `_streamingScratchGate = new object()` in `HectonVoxelEngine` with an integer gate and `StreamingScratchGateScope`; all 15 `lock (_streamingScratchGate)` sites became `using (EnterStreamingScratchGate())`.
- Replaced `VoxelDeltaProcessor._chunkStateFreeStack` managed `int[]` with `FixedList4096Bytes<int>`.
- Replaced `HectonVoxelVolume._terrainHoleHandles` managed `int[]`/`Array.Empty<int>()` with `FixedList128Bytes<int>`.
- Replaced `VoxelVolumeLeakSentinel` static managed arrays (`HectonVoxelVolume[512]`, `int[512]`, `byte[512]`) with intrusive fields on `HectonVoxelVolume`: `_leakSentinelNext`, `_leakSentinelPrev`, `_leakDestroyRequestedFrame`, `_leakSentinelState`.
- Replaced `FixedChunkRegistry<T>._occupied` managed `byte[]` with `FixedList4096Bytes<byte>`.
- Post-edit targeted allocation grep remaining hits: `HectonVoxelEngine` Marching Cubes seed `new int[256]`/`new int[4096]`, deferred PhysX `List<>`, active-volume `List<>`. No remaining targeted hits for `_streamingScratchGate = new object()`, `_chunkStateFreeStack = new int[]`, terrain-hole `new int[]`, leak-sentinel arrays, or `_occupied = new byte[]`.
- Brace balance: `HectonVoxelVolume.cs`, `VoxelDeltaProcessor.cs`, and `HectonVoxelEngine.cs` all `depth=0 min=0`.
- Route grep: 0 hits for `Agent1312`, `Dump_1312`, or `Dump_SHINOBU` in target source.
- `git diff --check`: PASS, only CRLF warnings.
- `dotnet build` and Roslyn AST were not run in Loop 16: CPU load was 96 (>50), `dotnet/csc` processes were absent, and the operator forbids build/dotnet while CPU is under work.

## Loop 17 APEX Managed Allocation Reduction

- Re-read prompt/status/rationale before edits. Correct current-batch extraction must match the attributed opening tag: `<AGENT_PROMPT id="1304" role=...>`. Latest extraction: bytes=21848, tasks=20, sha256=`e93d536283b5c370d3ce6f26b4728ff8adff4fc45bc007f1072020bd36f728df`.
- Replaced `MCTables` Marching Cubes seed `new int[256]` and `new int[4096]` literals with method-local `ReadOnlySpan<int>` stackalloc tables, then copied into existing unmanaged `_edgeTable`/`_triTable` storage. Proof lines: `HectonVoxelEngine.cs:98`, `HectonVoxelEngine.cs:135`, `HectonVoxelEngine.cs:138`, `HectonVoxelEngine.cs:399`.
- Replaced `VoxelDeltaProcessor.FixedVolumeRegistry` managed `HectonVoxelVolume[]` backing storage with an intrusive value-type registry using per-volume links: `_deltaRegisteredNext/_Prev`, `_deltaPendingRebuildNext/_Prev`, `_deltaRegistered`, `_deltaPendingRebuildRegistered`.
- Post-edit brace balance: `VoxelDeltaProcessor.cs`, `HectonVoxelVolume.cs`, and `HectonVoxelEngine.cs` all `depth=0 min=0`.
- Route grep: 0 hits for `Agent1312`, `Dump_1312`, or `Dump_SHINOBU` in target source.
- `git diff --check`: PASS, only CRLF warnings.
- Current broad managed-boundary grep still reports real residuals, so release-grade absolute Zero-GC is NOT proven:
  - `VoxelDeltaProcessor.cs:162,164` shader `Vector4[]`; `201,203,236` bounded managed request arrays; `2158,2184,2207` save DTO `Array.Empty`; `5313` chunk-state pool array; `6756,6757` generic registry key/value arrays.
  - `HectonVoxelVolume.cs:376-383,1413-1420,1948-2006` cave/runtime snapshot arrays; `406-409` Unity collider/mesh reference pools.
  - `HectonVoxelEngine.cs:3035-3037` air-pocket scalar arrays; `3072,3078` deferred PhysX `List<>`; `3074,3080,3082,3085,3087` Unity mesh/bool pools; `3363,3365,3367` active-volume `List<>`; `8377` streaming scratch slot array.
- `dotnet build` and Roslyn AST were not run in Loop 17: CPU load was 76 (>50), and the operator forbids build/dotnet while CPU is under work. Stackalloc syntax and intrusive registry are therefore text/brace verified only, not compiler verified.

## Loop 18 APEX Managed Allocation Reduction

- Re-read prompt/status/rationale before edits; prompt hash remains `e93d536283b5c370d3ce6f26b4728ff8adff4fc45bc007f1072020bd36f728df`.
- Replaced `HectonVoxelEngine` air-pocket managed arrays (`Vector3[]` centers/extents plus `float[]` refill fractions) with `FixedList4096Bytes<AirPocketEntry>`.
- `AirPocketEntry` byte map: explicit size 32; `Center` offset 0 (`float3`, 12B); `HalfExtents` offset 12 (`float3`, 12B); `OxygenRefillFraction` offset 24 (`float`, 4B); `_pad0` offset 28 (`uint`, 4B). Size is divisible by 8.
- Replaced `HectonVoxelEngine._activeVolumeLocalBounds` managed `List<Bounds>` with `FixedList4096Bytes<ActiveVolumeLocalBoundsEntry>`.
- `ActiveVolumeLocalBoundsEntry` byte map: explicit size 24; `Center` offset 0 (`float3`, 12B); `Size` offset 12 (`float3`, 12B). Size is divisible by 8.
- Extended `VoxelMemorySovereigntyValidator1304` to call `global::HectonVoxelEngine.ValidateAgent1304EnginePrivateLayouts(ref failureFlags)` for the new engine DTO maps and existing mesh-pipeline telemetry DTO.
- Post-edit brace balance: `HectonVoxelEngine.cs` and `VoxelMemorySovereigntyValidator1304.cs` both `depth=0 min=0`.
- Route grep: 0 hits for `Agent1312`, `Dump_1312`, or `Dump_SHINOBU` in target source.
- Removed-pattern grep: 0 hits for `new Vector3[AirPocketRegistryCapacity]`, `new float[AirPocketRegistryCapacity]`, `_airPocketCenters`, `_airPocketHalfExtents`, `_airPocketRefillFractions`, and `new List<Bounds>`.
- Current broad managed-boundary grep still reports real residuals, so release-grade absolute Zero-GC is still NOT proven:
  - `VoxelDeltaProcessor.cs:162,164` shader `Vector4[]`; `201,203,236` bounded managed request arrays with `HectonVoxelVolume` references; `2158,2184,2207` save DTO `Array.Empty`; `5313` chunk-state metadata pool array; `6756,6757` generic registry key/value arrays.
  - `HectonVoxelVolume.cs:376-383,1413-1420,1948-2006` cave/runtime snapshot arrays; `406-409` Unity collider/mesh reference pools.
  - `HectonVoxelEngine.cs:3070,3076` deferred PhysX `List<>`; `3072,3078,3080,3083,3085` Unity mesh/bool/reference pools; `3388,3390` active-volume Unity reference `List<>`; `8402` streaming scratch slot array.
- Runtime string/LINQ scan remains 0 hits for `string.Format`, `.ToString(`, `System.Linq`, `Enumerable.`, `foreach`, `StringBuilder`, `new string`, `throw new`, `new Task`, `new Thread`, and `new Exception` in non-editor target files.
- `git diff --check`: PASS, only CRLF warnings.
- `dotnet build` and Roslyn AST were not run in Loop 18: CPU load was 100 (>50), and the operator forbids build/dotnet while CPU is under work. New FixedList/validator syntax is text/brace verified only, not compiler verified.

## Loop 19 APEX Managed Allocation Reduction

- Re-read prompt/status/rationale before edits. Prompt extraction: bytes=21853, tasks=20, sha256=`e93d536283b5c370d3ce6f26b4728ff8adff4fc45bc007f1072020bd36f728df`.
- Replaced `HectonVoxelEngine` mesh-pool occupancy managed arrays (`bool[VoxelSurfaceMeshPoolSize]` and `bool[VoxelPhysicsBakeMeshPoolSize]`) with `FixedList4096Bytes<byte>` lanes.
- Added `EnsureVoxelMeshPoolOccupancyFlags()` with `_voxelMeshPoolOccupancyInitGate` and `_voxelMeshPoolOccupancyInitialized`; initialization uses `Interlocked.CompareExchange`, `SpinWait`, and `Volatile`, not a managed lock object.
- Runtime access now uses byte flags (`0/1`) at acquire/release/reset/destroy paths. Removed-pattern scan reports 0 hits for `new bool[`, `bool[256]`, direct bool `if (_voxel*InUse[i])`, and `= false` writes on those lanes.
- Post-edit brace balance: `HectonVoxelEngine.cs depth=0 min=0`.
- Runtime string/LINQ/exception token scan remains 0 hits for `string.Format`, `.ToString(`, `System.Linq`, `Enumerable.`, `foreach`, `StringBuilder`, `new string`, `throw new`, `new Task`, `new Thread`, and `new Exception` in non-editor target files.
- Route grep remains 0 hits for `Agent1312`, `Dump_1312`, or `Dump_SHINOBU` in target source.
- `git diff --check`: PASS, only CRLF warnings.
- Broad managed-boundary grep after Loop 19 still reports real residuals, so release-grade absolute Zero-GC is still NOT proven:
  - `VoxelDeltaProcessor.cs:162,164` shader `Vector4[]`; `201,203,236` bounded managed request arrays with `HectonVoxelVolume` references; `2158,2184,2207` save DTO `Array.Empty`; `5313` chunk-state metadata pool array; `6756,6757` generic registry key/value arrays.
  - `HectonVoxelVolume.cs:376-383,1413-1420,1948-2006` cave/runtime snapshot arrays; `406-409` Unity collider/mesh/Rigidbody reference pools.
  - `HectonVoxelEngine.cs:3070,3076` deferred PhysX `List<>`; `3072,3078,3082` Unity reference arrays; `3388,3390` active-volume Unity reference `List<>`; `8441` streaming scratch slot array.
- One controlled compile was run because CPU guard was clear (`cpu=42`, `dotnet_csc_count=0`): `dotnet build Hecton8.Core.csproj --no-restore --nologo`.
- Compile result: FAIL, 126 errors outside the 1304 voxel files in the returned stream. Main external walls: `MpscSignalRingBuffer<T>.ParallelWriter` vs `NativeQueue<T>.ParallelWriter` mismatches across Equipment/Combat/Scavenging/UI/Environment/Inventory/etc.; `Audio/AcousticPortalPropagation.cs` private pad access; `Core/Signals/SignalBusRuntime.cs` missing legacy fields; `Physics/TetherBlackBoxDumpWriter.cs` duplicate catch; `TetherInstance.cs` unassigned out parameters and missing exception type usings; `Visor/PlayerStressVFX.cs` ref-return error.
- Build verdict: not green, not attributable to this loop's voxel files from the returned error stream. No further build reruns in Loop 19 per operator instruction.

## Loop 20 APEX Managed Allocation Reduction

- Re-read status/rationale and re-extracted prompt before edits. Prompt extraction: bytes=21853, tasks=20, sha256=`e93d536283b5c370d3ce6f26b4728ff8adff4fc45bc007f1072020bd36f728df`.
- Replaced `HectonVoxelVolume` crater registries `_craterStamps` and `_resourceCraterClusterStamps` from managed `VoxelCraterStamp[]`/`Array.Empty<VoxelCraterStamp>()` to `FixedList4096Bytes<VoxelCraterStamp>`.
- Removed public `VoxelCraterStamp[] CraterStamps` array exposure and added `TryGetCraterStamp(int index, out VoxelCraterStamp stamp)` so rebuild code reads one explicit DTO at a time without receiving a managed array.
- Updated `HectonVoxelEngine` rebuild path to clamp crater count against `StreamingCraterStampScratchCapacity`, fetch crater stamps via `TryGetCraterStamp`, and fail closed if the fixed registry count is inconsistent.
- Added fail-closed guards: `_craterStampCount` or `_resourceCraterClusterCount` outside fixed-list length clears the affected local registry and returns without out-of-range indexing or managed exceptions.
- Byte map reused and still valid: `VoxelCraterStamp` explicit size 32; `position@0 double3` (24B); `radius@24 float`; `blendRadius@28 float`; size divisible by 8.
- AUP rule remains preserved in rebuild copy: `crater.position -= committedTotalOffsetDouble` is performed in `double3`; the local crater position is then consumed by the downstream density job. No absolute AUP-to-float cast was introduced.
- Removed-pattern scan: 0 hits for `VoxelCraterStamp[]`, `CraterStamps =>`, `craterSnapshot`, `new VoxelCraterStamp[`, and `Array.Empty<VoxelCraterStamp>` in target voxel files.
- Post-edit brace balance: `HectonVoxelVolume.cs depth=0 min=0`; `HectonVoxelEngine.cs depth=0 min=0`.
- Runtime string/LINQ/exception token scan remains 0 hits for `string.Format`, `.ToString(`, `System.Linq`, `Enumerable.`, `foreach`, `StringBuilder`, `new string`, `throw new`, `new Task`, `new Thread`, and `new Exception` in non-editor target files.
- Route grep remains 0 hits for `Agent1312`, `Dump_1312`, or `Dump_SHINOBU` in target source.
- `git diff --check`: PASS, only CRLF warnings.
- Broad managed-boundary grep after Loop 20 still reports real residuals, so release-grade absolute Zero-GC is still NOT proven:
  - `VoxelDeltaProcessor.cs:162,164` shader `Vector4[]`; `201,203,236` bounded managed request arrays with `HectonVoxelVolume` references; `2158,2184,2207` save DTO `Array.Empty`; `5313` chunk-state metadata pool array; `6756,6757` generic registry key/value arrays.
  - `HectonVoxelVolume.cs:376-379,1423-1426,1958-1986` cave graph snapshot arrays; `382-383,1429-1430,2001-2007` collapse contact/body arrays containing Unity reference routes; `406-409` Unity collider/mesh reference pools.
  - `HectonVoxelEngine.cs:3070,3076` deferred PhysX `List<>`; `3072,3078,3082` Unity reference arrays; `3388,3390` active-volume Unity reference `List<>`; `8445` streaming scratch slot array.
- `dotnet build` and Roslyn AST were not rerun in Loop 20: CPU guard reported `cpu=100`, `dotnet_csc_count=0`, and the operator forbids build/dotnet while CPU is under work. Last compile wall remains the Loop 19 external 126-error wall.

## Loop 21 APEX ARM64 DTO Layout Correction

- Re-read status/rationale and re-extracted prompt before edits. Prompt extraction: bytes=21853, tasks=20, sha256=`e93d536283b5c370d3ce6f26b4728ff8adff4fc45bc007f1072020bd36f728df`.
- Read active mandates: native memory/job protocol, zero-GC hot path, ARM64 runtime struct layout, crash telemetry, voxel SDF pipeline.
- CPU guard before verification reported `cpu=82`, `dotnet_csc_count=0`; no dotnet/build/Roslyn compile was launched.
- Converted Burst/NativeArray cave DTOs in `CaveTypes.cs` from sequential layout to explicit layout: `CaveNode`, `CaveTunnel`, `CaveEntrance`, `CaveStructure`, `CaveGenerationParams`, and `CaveSpawnData`.
- Byte maps:
  - `CaveNode`: size 40; `position@0 float3`; `radii@12 float3`; `blendRadius@24 float`; `noiseScale@28 float`; `noiseAmplitude@32 float`; `roomType@36 byte`; `_pad0@37`; `_pad1@38`; `_pad2@39`.
  - `CaveTunnel`: size 56; `pointA@0 float3`; `pointB@12 float3`; `radiusA@24 float`; `radiusB@28 float`; `blendRadius@32 float`; `heightScale@36 float`; `widthScale@40 float`; `warpAmount@44 float`; `tunnelType@48 byte`; `_pad0@49`; `_pad1@50`; `_pad2@51`; `_pad3@52 uint`.
  - `CaveEntrance`: size 72; `surfacePosition@0 float3`; `inwardDirection@12 float3`; `radius@24 float`; `funnelLength@28 float`; `innerRadius@32 float`; `terrainNormal@36 float3`; `terrainNormalBlend@48 float`; `terrainSplatColor@52 float4`; `terrainSplatBlend@68 float`.
  - `CaveStructure`: size 48; `position@0 float3`; `size@12 float3`; `pointB@24 float3`; `blendRadius@36 float`; `noiseAmount@40 float`; `structureType@44 byte`; `_pad0@45`; `_pad1@46`; `_pad2@47`.
  - `CaveGenerationParams`: size 80; 4-byte lanes from offsets 0-68; `structureOnlyMode@72 byte`; `spawnContext@73 byte`; `_pad0.._pad5@74..79`.
  - `CaveSpawnData`: size 16; `position@0 float3`; `hashId@12 int`.
- Extended `VoxelMemorySovereigntyValidator1304` to assert these cave DTO sizes and offsets with `UnsafeUtility.SizeOf<T>()` and `UnsafeUtility.GetFieldOffset`.
- Verification: no remaining stale sequential-layout/comment-size hits for the old cave sizes; validator cave assertions present; brace balance `CaveTypes.cs depth=0 min=0` and validator `depth=0 min=0`; route grep remains 0 hits for `Agent1312`, `Dump_1312`, or `Dump_SHINOBU`; `git diff --check` PASS with CRLF warnings only.
- Build/AST status: not rerun due CPU guard. Current proof class is static source plus editor validator code; Unity/editor execution proof is pending.

## Loop 22 APEX Managed LUT Reduction

- Re-read residual scan after Loop 21 and found one removable scalar managed allocation in `HectonVoxelEngine`: `_chthonicPillarColliderUnitCircle` as a static `float2[24]` LUT.
- Replaced the managed array with `GetChthonicPillarColliderUnitCircle(int index)`, a switch-backed value LUT returning `float2` structs. This preserves the 24-point visual approximation and does not introduce trigonometric runtime work.
- Updated the smooth chthonic pillar collider mesh path to call the value LUT.
- Verification: removed-pattern scan has 0 hits for `_chthonicPillarColliderUnitCircle`, `float2[24]`, and `COLD ALLOC: float2[24]`; one expected hit remains for `GetChthonicPillarColliderUnitCircle` definition and one for its call site.
- Runtime string/LINQ/exception token scan over modified runtime target files remains 0 hits.
- Brace balance: `HectonVoxelEngine.cs depth=0 min=0`.
- `git diff --check`: PASS with CRLF warnings only.
- CPU guard still reports `cpu=82`, `dotnet_csc_count=0`; build/Roslyn compile not launched.

## Loop 23 APEX Fail-Closed Registry Guards

- Re-read status/rationale and re-extracted prompt before edits. Prompt extraction: bytes=21853, sha256=`e93d536283b5c370d3ce6f26b4728ff8adff4fc45bc007f1072020bd36f728df`; task count remains 20 by prompt `Task 01..20` labels.
- CPU guard after patch reported `cpu=83`, `dotnet_csc_count=0`; build/Roslyn compile was not launched.
- Patched `HectonVoxelEngine` air-pocket FixedList registry:
  - corruption guard at `HectonVoxelEngine.cs:3216` detects negative count, count/length drift, or length above `AirPocketRegistryCapacity`;
  - corruption guard at `HectonVoxelEngine.cs:3249` protects unregister;
  - read sampling clamps iteration to `min(_airPocketEntries.Length, AirPocketRegistryCapacity)` without mutating the read accessor;
  - corruption path writes `VoxelMeshPipelineRegistryCorruptionFlag` into the existing 300-frame blackbox via `RecordVoxelRegistryCorruptionForAgent1304`.
- Patched `HectonVoxelVolume` terrain-hole FixedList registry:
  - `TrackTerrainHoleHandle` guard at `HectonVoxelVolume.cs:3347` clears corrupt count state and records blackbox telemetry before any indexed read;
  - `UnregisterTerrainHoles` guard at `HectonVoxelVolume.cs:3550` clears corrupt count state and records blackbox telemetry before bridge unregister iteration;
  - vegetation-bridge-null branch now clears the FixedList, not just the counter.
- Patched active-volume bounds registration at `HectonVoxelEngine.cs:4711` to fail closed before adding managed object references if the inline bounds FixedList is unexpectedly full, preventing index-lane desync.
- Verification: brace balance `HectonVoxelEngine.cs depth=0 min=0`, `HectonVoxelVolume.cs depth=0 min=0`; runtime string/LINQ/exception token scan over modified runtime target files remains 0 hits; removed old managed-registry pattern scan remains 0 hits; `git diff --check` PASS with CRLF warnings only.
- Residual truth unchanged: absolute release-grade Zero-GC is still not proven while cave graph arrays, Unity reference pools/lists, shader upload arrays, request arrays, save DTO empty arrays, and generic registry arrays remain.

## Loop 24 APEX DTO/AUP/Boxing Recheck

- Re-read status/rationale and re-extracted prompt before edits. Prompt extraction: bytes=21853, tasks=20, sha256=`e93d536283b5c370d3ce6f26b4728ff8adff4fc45bc007f1072020bd36f728df`.
- CPU guard reported `cpu=88` at loop start and `cpu=70` after verification, `dotnet_csc_count=0`; build/Roslyn compile was not launched.
- Removed `ChunkAddress.Equals(object)` from `VoxelDeltaProcessor.cs`; registry lookup remains typed through `IEquatable<ChunkAddress>.Equals(ChunkAddress)` at the two call sites. Post-scan: 0 hits for `Equals(object` and `object obj` in `VoxelDeltaProcessor.cs`.
- Converted `ThermalMeltEvent` from implicit sequential layout to explicit 48B layout:
  - `AbsoluteUniversePositionDouble@0 double3` (24B)
  - `AbsoluteUniversePosition@24 Vector3` (12B legacy/fallback lane)
  - `RadiusMeters@36 float`
  - `Heat01@40 float`
  - `_pad0@44 uint`
- Extended `ValidateAgent1304PrivateLayouts` with size/offset assertions for `ThermalMeltEvent`.
- AUP recheck: `AcceptThermalMeltEvent` resolves the double coordinate through `ResolveThermalMeltPositionDouble`; volume selection uses `double3 delta = volume.GenerationAbsoluteUniversePositionDouble - absoluteCenter`; runtime float conversion remains routed through `HectonFloatingOrigin.ToRuntimePosition(double3)`.
- Sequential-layout residual: `HectonVoxelEngine.VoxelSurfaceVertex` and `VoxelColliderVertex` remain sequential because Unity `MeshData.SetVertexBufferParams` defines tight GPU strides of 76B and 12B. Padding to 80B/16B would corrupt Unity vertex stream interpretation; this is a Unity mesh API boundary, not a vault DTO.
- Verification: brace balance `VoxelDeltaProcessor.cs depth=0 min=0`, `HectonVoxelEngine.cs depth=0 min=0`, `HectonVoxelVolume.cs depth=0 min=0`; runtime string/LINQ/exception/boxing token scan over modified runtime target files remains 0 hits; source route scan remains 0 hits for `Agent1312`, `Dump_1312`, `Dump_SHINOBU`; `git diff --check` PASS with CRLF warnings only.
- Residual truth unchanged: fixed generic registries and Unity-reference queues/lists still require a descriptor bridge or wider route card before absolute managed-new eradication can be claimed.

## Loop 25 APEX Raycast DTO And Pending Queue Fail-Closed

- Re-read status/rationale, AGENTS.md, domain map, relevant mandates, and prompt before edits. Prompt hash remains `e93d536283b5c370d3ce6f26b4728ff8adff4fc45bc007f1072020bd36f728df`; task count remains 20 by `Task 01..20` labels.
- Converted `VoxelSdfRaycastHit` to explicit 40B layout because it is written through `NativeArray<VoxelSdfRaycastHit>` by `VoxelSdfRaymarchJob`:
  - `Point@0 Vector3`
  - `Normal@12 Vector3`
  - `Distance@24 float`
  - `Density@28 float`
  - `Hit@32 byte`
  - `_pad0.._pad6@33..39 byte`
- Added `VoxelSdfRaycastHit` size/offset assertions to `VoxelMemorySovereigntyValidator1304`.
- Added fail-closed guards for pending queue mirror counters in `VoxelDeltaProcessor`:
  - thermal melt queue validates `_thermalMeltCount` before merge/advance/remove.
  - pending carve queue validates `_pendingCarveHead` and `_pendingCarveCount` before drop/reserve/enqueue/pop/coalesce/schedule/merge.
  - pending compaction queue validates `_pendingCompactionHead` and `_pendingCompactionCount` before enqueue/pop/queue/schedule/pressure checks.
  - corruption path writes `VoxelBlackBoxPendingQueueCorruptionFlag`, encodes queue/head/count/capacity into the blackbox focus field, clears the affected bounded lane, and returns without managed exception flow.
- Verification: brace balance `HectonVoxelVolume.cs depth=0 min=0`, `VoxelDeltaProcessor.cs depth=0 min=0`, validator `depth=0 min=0`.
- Runtime token scan over modified non-editor target files remains 0 hits for `string.Format`, `.ToString(`, LINQ, `foreach`, `StringBuilder`, `new string`, managed throw/task/thread/exception tokens, `Equals(object`, and `object obj`.
- Source route scan remains 0 hits for `Agent1312`, `Dump_1312`, and `Dump_SHINOBU`.
- `git diff --check`: PASS with CRLF warnings only.
- One controlled build was run after CPU guard reported `cpu=43`, `dotnet_csc_count=0`: `dotnet build Hecton8.Core.csproj --no-restore --nologo`.
- Compile result: FAIL, 48 errors outside returned 1304 files. External walls include `World/GPUScatterDirector.cs`, `World/WorldChunkResidencyManager.cs`, `ModdingAPI/FutureCommandSandboxValidator.cs`, `Data/Monolith/H8StaticDataArena.cs`, `Audio/AcousticPortalPropagation.cs`, `Audio/NativeAudioFrameRingBuffer.cs`, `Physics/TetherBlackBoxDumpWriter.cs`, `TetherInstance.cs`, and `Visor/*`.
- No errors from `CaveTypes.cs`, `HectonVoxelEngine.cs`, `HectonVoxelVolume.cs`, `VoxelDeltaProcessor.cs`, or `World/VoxelSurfaceNets/Editor/VoxelMemorySovereigntyValidator1304.cs` appeared in the returned build stream.
- Residual truth unchanged: absolute release-grade Zero-GC remains unproven while managed shader upload arrays, pending request arrays, chunk registry arrays, save DTO empty arrays, cave graph arrays, Unity reference pools/lists, and mesh API tight-stride sequential structs remain.

## Loop 26 APEX Chunk State Pool Heap Removal

- Re-read status/rationale, re-extracted prompt, and re-read mandates before edits. Prompt hash remains `e93d536283b5c370d3ce6f26b4728ff8adff4fc45bc007f1072020bd36f728df`; task count remains 20.
- CPU guard initially blocked build at `cpu=62`, `dotnet_csc_count=0`. A later guard cleared at `cpu=8`, `dotnet_csc_count=0`, so one compile signal was run because this loop changed C# storage layout.
- Removed the managed `ChunkDeltaState[DirtyChunkStatePoolCapacity]` lease pool from `VoxelDeltaProcessor`.
- Replaced it with three inline `FixedList4096Bytes<ChunkDeltaState>` banks:
  - `_chunkStatePoolBank0` at `VoxelDeltaProcessor.cs:200`
  - `_chunkStatePoolBank1` at `VoxelDeltaProcessor.cs:201`
  - `_chunkStatePoolBank2` at `VoxelDeltaProcessor.cs:202`
- Added fail-closed pool-bank guards:
  - `TryAddChunkStatePoolSlot`, `TryGetChunkStatePoolSlot`, `TrySetChunkStatePoolSlot`.
  - corrupt slot/free-stack state writes `VoxelBlackBoxChunkStatePoolCorruptionFlag` at `VoxelDeltaProcessor.cs:152` through `WriteChunkStatePoolCorruptionSample` at `VoxelDeltaProcessor.cs:5574`, clears the free stack, and returns without managed exception flow.
- Converted dirty/compacted chunk state records to explicit layouts:
  - `CompactedChunkState`: size 24; `ChunkCoord@0 int3`; `VoxelSize@12 float`; `RleSdfValueBits@16 ushort`; `IsRleCompressed@18 byte`; `RleMaterialId@19 byte`; `RleCellFlags@20 byte`; `_pad0@21 byte`; `_pad1@22 ushort`.
  - `ChunkDeltaState`: size 32; `ChunkCoord@0 int3`; `VoxelSize@12 float`; `DirtyCellCount@16 int`; `PoolSlot@20 int`; `VaultBacked@24 byte`; `_pad0@25 byte`; `_pad1@26 ushort`; `_pad2@28 uint`.
- Extended `ValidateAgent1304PrivateLayouts` with explicit layout/offset checks for `CompactedChunkState` and `ChunkDeltaState`.
- Verification:
  - removed-pattern scan: 0 hits for `_chunkStatePool = new`, `new ChunkDeltaState[`, `ChunkDeltaState[DirtyChunkStatePoolCapacity]`, and `COLD ALLOC: ChunkDeltaState[`.
  - brace balance: `VoxelDeltaProcessor.cs depth=0 minDepth=0`.
  - runtime token scan over modified non-editor target files remains 0 hits for `string.Format`, `.ToString(`, LINQ, `foreach`, `StringBuilder`, `new string`, managed throw/task/thread/exception tokens, `Equals(object`, and `object obj`.
  - route scan remains 0 hits for `Agent1312`, `Dump_1312`, and `Dump_SHINOBU`.
  - `git diff --check -- Assets/_Project/Scripts/VoxelDeltaProcessor.cs`: PASS with CRLF warning only.
- Build: `dotnet build Hecton8.Core.csproj --no-restore --nologo` failed with 55 errors in external domains. Returned error stream contains no `CaveTypes.cs`, `HectonVoxelEngine.cs`, `HectonVoxelVolume.cs`, `VoxelDeltaProcessor.cs`, or `VoxelMemorySovereigntyValidator1304.cs` errors. External walls include `World/GPUScatterDirector.cs`, `World/WorldChunkResidencyManager.cs`, `Data/Monolith/H8StaticDataArena.cs`, `Audio/*`, `ModdingAPI/FutureCommandSandboxValidator.cs`, `Physics/TetherBlackBoxDumpWriter.cs`, `TetherInstance.cs`, `Construction/DroneFleetManager_Transactions.cs`, and `Visor/*`.
- Residual truth: absolute release-grade Zero-GC remains unproven. Real remaining managed boundaries: shader `Vector4[]`, pending request arrays with `HectonVoxelVolume` references, generic registry key/value arrays, save DTO empty arrays, cave graph arrays, collapse/Unity reference arrays/lists, and Unity mesh API tight-stride sequential structs.

## Loop 27 APEX Fixed Chunk Registry SoA

- Re-read status/rationale and re-extracted `<AGENT_PROMPT id="1304" ...>` from `Docs/Tasks/CURRENT_BATCH.md` with attribute-tolerant CLI regex. Prompt extraction: bytes=21848, taskLabels=20, sha256=`e93d536283b5c370d3ce6f26b4728ff8adff4fc45bc007f1072020bd36f728df`.
- Re-read mandates before coding: `DATA_Runtime_Struct_Layout_ARM64.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`, `MATH_AUP_Determinism_Sync.txt`, `QA_Evidence_Text_Filter_Audit.txt`, `VOX_Voxel_World_Logic_Carving_Persistence.txt`, `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`.
- Removed the managed `FixedChunkRegistry<T>` class allocation from `VoxelDeltaProcessor`.
- Removed the managed `ChunkAddress[] _keys` and `T[] _values` storage from the dirty, compacted, and write-version chunk registries.
- Replaced registry storage with inline SoA banks:
  - `FixedList4096Bytes<ChunkAddress> _keys`
  - `FixedList4096Bytes<T> _values0.._values3`
  - `FixedList4096Bytes<byte> _occupied`
  - lazy fixed-capacity initialization to `InitialChunkRegistryCapacity` without heap arrays.
- Source lines after patch:
  - fields at `VoxelDeltaProcessor.cs:194-196`
  - registry declaration at `VoxelDeltaProcessor.cs:7051`
  - SoA banks at `VoxelDeltaProcessor.cs:7053-7058`
  - fail-closed initialization at `VoxelDeltaProcessor.cs:7247-7281`
  - value bank get/set at `VoxelDeltaProcessor.cs:7312-7415`
- Verification:
  - prompt extraction: `bytes=21848 taskLabels=20 sha256=e93d536283b5c370d3ce6f26b4728ff8adff4fc45bc007f1072020bd36f728df`.
  - removed-pattern scan: 0 hits for `new FixedChunkRegistry`, `new ChunkAddress[`, `new T[`, `ChunkAddress[capacity]`, `_values = new`, `_keys = new`, `private readonly ChunkAddress[]`, and `private readonly T[]`.
  - brace balance: `VoxelDeltaProcessor.cs depth=0 minDepth=0`.
  - runtime token scan over modified non-editor target files remains 0 hits for `string.Format`, `.ToString(`, LINQ, `foreach`, `StringBuilder`, `new string`, managed throw/task/thread/exception tokens, `Equals(object`, and `object obj`.
  - `git diff --check -- Assets/_Project/Scripts/VoxelDeltaProcessor.cs`: PASS with CRLF warning only.
- Build was intentionally not launched in Loop 27 per operator instruction to avoid repeated dotnet/build attempts after a static-only storage cleanup. Compile proof remains `PENDING VERIFICATION`.
- Residual truth: absolute release-grade Zero-GC remains unproven. Real remaining managed boundaries: shader `Vector4[]`, pending request arrays with `HectonVoxelVolume` references, save DTO empty arrays, cave graph arrays, collapse/Unity reference arrays/lists, active Unity object registries, mesh pools/proxies, streaming scratch slot array, and Unity mesh API tight-stride sequential structs.

## Loop 28 APEX MC Table Vault Locks And Scratch Guard

- Re-read status/rationale and re-extracted `<AGENT_PROMPT id="1304" ...>` from `Docs/Tasks/CURRENT_BATCH.md` with attribute-tolerant CLI regex. Prompt extraction: `bytes=21853 taskLabels=20 sha256=e93d536283b5c370d3ce6f26b4728ff8adff4fc45bc007f1072020bd36f728df`.
- Added DataVault BufferIDs:
  - `H8Memory.cs:813` `VoxelMarchingCubesEdgeTable = 644`
  - `H8Memory.cs:814` `VoxelMarchingCubesTriTable = 645`
- Replaced MC table static persistent ownership:
  - `HectonVoxelEngine.cs:42-46` declares MC table buffer route and lengths.
  - `HectonVoxelEngine.cs:48-50` stores vault and generation handles, not local `NativeArray<int>` tables.
  - `HectonVoxelEngine.cs:55-87` adds `MCTables.JobTableLease`, releasing tri and edge locks in `Dispose`.
  - `HectonVoxelEngine.cs:184` and `HectonVoxelEngine.cs:456` write edge/tri LUTs through vault write locks.
  - `HectonVoxelEngine.cs:477-505` centralizes writable table acquisition.
  - `HectonVoxelEngine.cs:514-529` releases vault table handles on shutdown/hot-swap.
  - `HectonVoxelEngine.cs:531-565` locks both MC table buffers and resolves read-only handles before jobs.
- Rewired Marching Cubes jobs:
  - `HectonVoxelEngine.cs:8013-8044` count job acquires `JobTableLease`, passes read-only edge/tri tables, and releases in `finally`.
  - `HectonVoxelEngine.cs:8102-8133` extract job repeats the same lease discipline.
- Hardened streaming scratch capacity before growth:
  - `HectonVoxelEngine.cs:3128-3134` adds explicit safety caps.
  - `HectonVoxelEngine.cs:8757-8768` rejects invalid capacity requests before scratch allocation/growth.
  - `HectonVoxelEngine.cs:8771-8805` validates grid, point/cell counts, raw mesh, edge vertex, and spawn-point scratch ceilings.
- Verification:
  - Removed-pattern scan: 0 hits for `MCTables.EdgeTable`, `MCTables.TriTable`, `DataVaultExemptMarchingCubesTableAllocator`, `new NativeArray<int>(256`, and `new NativeArray<int>(4096`.
  - Runtime token scan over modified target files: `string.Format=0`, `.ToString(=0`, `System.Linq=0`, `Enumerable.=0`, `foreach=0`, `StringBuilder=0`, `new string=0`, `Equals(object=0`, `object obj=0`, `LogException(ex)=0`.
  - Residual token scan: `throw new=13` from `H8Memory.cs:2141-2191` core fatal-memory paths and editor validator `VoxelMemorySovereigntyValidator1304.cs:38,389`; `catch (Exception ex)=1` at `HectonVoxelVolume.cs:3538`.
  - Brace balance: `HectonVoxelEngine.cs depth=0 min=0`; `H8Memory.cs depth=0 min=0`.
  - `git diff --check -- Assets/_Project/Scripts/HectonVoxelEngine.cs Assets/_Project/Scripts/Core/Memory/H8Memory.cs`: PASS with CRLF warnings only.
- Build was not launched in Loop 28 per operator instruction to avoid repeated dotnet/build attempts. Compile proof remains `PENDING_VERIFICATION`.
- Residual truth: absolute release-grade Zero-GC remains unproven. Real remaining boundaries include `VoxelDeltaProcessor.cs:175,177,212,214,247`, `HectonVoxelVolume.cs:384-391,414-417,537-546,1431-1438,2085,2094,2809`, `CaveTypes.cs:828`, and `HectonVoxelEngine.cs:2873,3195,3197,3201,3203,3207,3535,3537,3542,9225,9254,9350`.

## Loop 29 APEX Managed Exception Route Reduction

- Re-read status/rationale and re-extracted `<AGENT_PROMPT id="1304" ...>` from `Docs/Tasks/CURRENT_BATCH.md`. Prompt extraction before edits: `bytes=21853 taskLabels=20 sha256=e93d536283b5c370d3ce6f26b4728ff8adff4fc45bc007f1072020bd36f728df`.
- Removed the managed exception/log route in `HectonVoxelVolume.ProcessQueuedRebuildsAsync`:
  - previous `catch (Exception ex)` plus `H8Debug.LogException(ex, this)` is gone.
  - `HectonVoxelVolume.cs:3538-3552` now fail-closes a stuck `Baking` state in `finally`, sets `Pending`, requeues, and records binary blackbox telemetry.
- Added a dedicated mesh pipeline blackbox flag:
  - `HectonVoxelEngine.cs:3016` `VoxelMeshPipelineRebuildFailClosedFlag = 1u << 7`
  - `HectonVoxelEngine.cs:6924-6932` `RecordVoxelRebuildFailClosedForAgent1304`
- Removed the cold mesh pool warmup catch/log route:
  - `HectonVoxelEngine.cs:7342-7365` now checks `ct.IsCancellationRequested`/shutdown state and uses `finally` only.
  - `HectonVoxelEngine.cs:7367-7395` warmup loops use boolean cancellation checks.
  - `HectonVoxelEngine.cs:7424-7461` mesh acquire retry loops return null on cancellation instead of throwing.
- Verification:
  - `catch (Exception ex)=0`
  - `catch (Exception exception)=0`
  - `LogException=0` in modified target files.
  - Runtime token scan over modified target files remains 0 hits for `string.Format`, `.ToString(`, `System.Linq`, `Enumerable.`, `foreach`, `StringBuilder`, `new string`, `Equals(object`, and `object obj`.
  - Brace balance: `HectonVoxelEngine.cs depth=0 min=0`; `HectonVoxelVolume.cs depth=0 min=0`.
  - `git diff --check -- Assets/_Project/Scripts/HectonVoxelEngine.cs Assets/_Project/Scripts/HectonVoxelVolume.cs`: PASS with CRLF warnings only.
- Build was not launched in Loop 29 per operator instruction. Compile proof remains `PENDING_VERIFICATION`.
- Residual truth: `ct.ThrowIfCancellationRequested()` remains in other voxel pipeline async paths (`HectonVoxelEngine.cs:5490,5499,5562,7848,7988,8145,8179,8347,8484,9872,10337,10464,10816,10932`; `VoxelDeltaProcessor.cs:2185`). These are not fixed by Loop 29 and prevent a true "no managed exception path" claim.
