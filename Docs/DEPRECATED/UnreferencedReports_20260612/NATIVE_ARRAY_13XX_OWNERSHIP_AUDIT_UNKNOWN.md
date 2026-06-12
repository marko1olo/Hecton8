# 13XX NativeArray / Native Ownership Audit

Date: 2026-05-25
Agent: UNKNOWN
Evidence class: STATIC_SOURCE_ONLY
Scope: 13XX agent work related to `NativeArray`, persistent native aliases, `GlobalDataVault`, raw pointer lifetime, and black-box dump routes.

## Verdict

The 13XX work is not cosmetic. Several scoped native ownership defects were actually fixed.

The project is still not clean. The latest comparable full Roslyn ledger now reports:

- `forbiddenPersistentCandidates`: 1770
- `forbiddenMonoBehaviourCandidates`: 358
- `scannedFiles`: 2421
- `parseFailures`: 0

Earlier comparable 1304 full ledger reported:

- `forbiddenPersistentCandidates`: 1947
- `forbiddenMonoBehaviourCandidates`: 432

So the count moved down by:

- persistent candidates: 177 fewer
- MonoBehaviour candidates: 74 fewer

This is directionally correct, but it is not release-clean native memory architecture.

## Current Recheck - 2026-05-26 00:55

Fresh audit run by UNKNOWN:

- `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_UNKNOWN_CURRENT_20260526_0052.json`
  - `scannedFiles = 2421`
  - `forbiddenPersistentCandidates = 1770`
  - `forbiddenMonoBehaviourCandidates = 358`
  - `parseFailures = 0`
  - `auditHashSha256 = 68217d9f155aeb5233cbb3cc004518df4a1eb2c1d0d222bd810ca241008bbe31`

Excluded:

- `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_X_000.json`, modified `2026-05-26 00:47:20`, reports `2138/581`, but is not accepted as current proof because direct source line checks contradict it. It reports old `TerrainChunkPagerRuntime.cs:118-136` raw pointers and old `DroneFleetManager.cs:802-906` native fields that are not present in current source.

New deltas:

- From `2026-05-25 23:16:10`: persistent `1784 -> 1770`, MonoBehaviour `364 -> 358`.
- From `2026-05-25 23:46:40`: persistent `1778 -> 1770`, MonoBehaviour `358 -> 358`.

Source-visible status:

- `WorldChunkResidencyManager.cs = 0` current forbidden persistent candidates.
- `TerrainChunkPagerRuntime.cs = 0` current forbidden persistent candidates, but lifetime Vault lock / unsafe alias strategy still needs owner/fence proof.
- `DroneFleetManager.cs = 0` current forbidden persistent candidates.
- `FluidPipeGraphRuntime.cs = 0` current forbidden persistent candidates.
- `VoxelDeltaProcessor.cs = 0` current forbidden persistent candidates.
- `HabitatGraphManager.cs = 13` current forbidden persistent candidates, all inside private view-bundle structs `HabitatGraphWriteViews` and `HabitatFloodGraphJobViews`; these are not physical owner fields but still need stack-only/ref-struct classification or scanner rule proof.

## Superseded Recheck - 2026-05-25 23:24

This section is historical. It is superseded by the `2026-05-26 00:55` fresh audit above.

New proof after the previous report:

- `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_X_000.json`, timestamp `2026-05-25 23:16:10`
  - `scannedFiles = 2420`
  - `forbiddenPersistentCandidates = 1784`
  - `forbiddenMonoBehaviourCandidates = 364`
  - `parseFailures = 0`
- Delta from `VAULT_NATIVE_ALIAS_LEDGER_1304_FULL.json`:
  - persistent candidates: `1947 -> 1784`, 163 fewer
  - MonoBehaviour candidates: `432 -> 364`, 68 fewer
- Delta from `VAULT_NATIVE_ALIAS_LEDGER_1304_APEX_LOOP13_FULL.json`:
  - persistent candidates: `1853 -> 1784`, 69 fewer
  - MonoBehaviour candidates: `403 -> 364`, 39 fewer
- Burndown/rate details are now recorded in `Docs/Reports/FORBIDDEN_NATIVE_BURNDOWN_RATE_QUALITY_UNKNOWN.md`.
- `1305` changed materially:
  - `TerrainChunkPagerRuntime` raw pointer fields are removed.
  - `TerrainChunkPagerRuntime` still uses lifetime `TryLockBuffer` around DataVault buffers.
  - `TerrainChunkPagerRuntime` still has background worker and blackbox `FileStream` routes.
- `WorldChunkResidencyManager` had 6 persistent native containers at this checkpoint; this is superseded by the current fresh audit, where `WorldChunkResidencyManager.cs = 0`.
- `1313` changed materially:
  - private `GetSectionDataPointer` helper was removed.
  - normal Data Monolith section reads now use `ReadOnlySpan<T>`.
  - active `static_data.h8bin` is static-validator clean for current format/schema.
  - release readiness is still rejected pending platform PAL/player/profiler proof.

Scripts folder map added:

- Human report: `Docs/Reports/SCRIPTS_FOLDER_MAP_UNKNOWN.md`
- Machine map: `Docs/Reports/SCRIPTS_FOLDER_MAP_UNKNOWN.json`
- Full file index: `Docs/Reports/SCRIPTS_FILE_INDEX_UNKNOWN.tsv`
- Current totals: 326 directories, 5579 files, 2420 C# files, 1765591 C# lines, 162 asmdefs.
- Latest regenerated Scripts snapshot at `2026-05-25 23:01:22`: 326 directories, 5579 files, 2420 C# files, 1765753 C# lines, 162 asmdefs.
- Source files changed again after that snapshot, so the map is an evidence snapshot, not a live lock.

## Freshness Caveat

The latest full ledger used here is `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_X_000.json`, timestamp `2026-05-25 23:16:10`.

Source files may still change after this ledger because other agents are active. Do not treat this as a lock; treat it as the latest comparable full-project proof artifact currently on disk.

A fresh heavy Roslyn audit was not launched because the latest CPU guard samples stayed above the project limit (`74.2%`, `62.8%`, then `72.3%`), even with no active compiler process.

## Rule Boundary

The problem is not `NativeArray` itself.

Allowed:

- method-local `NativeArray<T>` views resolved from `GlobalDataVault`
- read-only `TryReadOnlyHandle` views
- `TryAcquireWriteLock` views released in `finally`
- Burst/job struct fields used as transient scheduled job parameters
- core memory authority allocations in `GlobalDataVault` / `H8Memory`
- stable native bridge raw pools when the native plugin retains pointers

Rejected:

- persistent `NativeArray<T>`, `NativeList<T>`, `NativeQueue<T>`, `NativeParallel*` fields in runtime managers/MonoBehaviours without a strict owner contract
- raw pointers captured from DataVault-resolved arrays and retained across frames
- long-lived DataVault locks/pins used to keep exported native pointers alive
- black-box dump routes that claim native crash proof while still depending on managed runtime IO
- report claims of `STATIC PASS` without source proof and residual caveats

## Agent 1300 - AI Cognition

Old complaint:

- `AIAnxietyTunerWindow.AnxietyTelemetryChartElement` cached persistent `NativeArray<AnxietyTelemetryEntry>`.
- Utility/anxiety/apex writers used mutable vault views without explicit `TryAcquireWriteLock` and `finally ReleaseWriteLock`.
- Black-box dump path did not include a 1300-owned artifact.

Current proof:

- `Docs/Reports/VAULT_EXORCISM_REPORT_1300.json`
- `beforePersistentNativeAliasFields = 1`
- `afterPersistentNativeAliasFields = 0`
- `forbiddenMonoBehaviourCandidates = 0`
- `verdict = STATIC_GREEN`

Source proof:

- `Assets/_Project/Scripts/AI/Cognition/Editor/AIAnxietyTunerWindow.cs:365-366` stores `IDataVault` and `VaultGenerationHandle<AnxietyTelemetryEntry>`, not a persistent `NativeArray`.
- `Assets/_Project/Scripts/AI/Cognition/Editor/AIAnxietyTunerWindow.cs:398` resolves the `NativeArray` inside `DrawChart`.
- `Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault_AnxietyDecay.cs:297-336` uses write locks and releases.
- Dump routes include `Dump_1300_AICognition.bin`.

Status: fixed in scoped domain.

Residual:

- No runtime Unity/profiler proof from this audit.
- Remaining `NativeArray` fields in the scoped files are transient vault views or job parameter views.

## Agent 1301 - AI Ecology Spatial Grid

Old complaint:

- Spatial query state retained `NativeArray<SpatialGridEntryDTO>`, `NativeArray<SpatialGridBucketRangeDTO>`, and `NativeArray<AmbientEntityAupDTO>` across possible DataVault relocation.
- Fault dump path had unsafe/fault-path edge cases.
- A later pass found a long-lived raw snapshot pointer in the dump route.

Current proof:

- `Docs/Reports/VAULT_EXORCISM_APEX_REAUDIT_1301.json`
- Scoped scans report `forbiddenPersistentCandidates = 0` for AI/Ecosystem, AI/Ambient, Animation/FaunaProcedural, and Scripts/Ecosystem.

Source proof:

- `Assets/_Project/Scripts/AI/Ecosystem/ShinobuSpatialGridSolver.cs:328-330` stores handles, not native arrays.
- `Assets/_Project/Scripts/AI/Ecosystem/ShinobuSpatialGridSolver.cs:571-573` resolves read-only handles method-locally.
- `Assets/_Project/Scripts/AI/Ecosystem/ShinobuSpatialGridSolver.cs:1563-1616` writes the dump snapshot under `TryAcquireWriteLock` with `finally ReleaseWriteLock`.
- `Assets/_Project/Scripts/AI/Ecosystem/ShinobuSpatialGridSolver.cs:1744-1778` worker reads under `TryLockBuffer` / `TryUnlockBuffer`.

Status: scoped native alias problem fixed.

Residual:

- The report explicitly keeps the caveat: worker disk output uses managed `FileStream`, not a native plugin IO path.
- Therefore native-only crash export is not fully fixed.

## Agent 1302 - Physics Vehicles

Old complaint:

- Raw physics scan found one forbidden persistent native alias: `VerletCableNodeBuffer.Nodes`.
- That finding belongs to cable/tether ownership and was excluded from 1302 scope.
- Local vehicle dump writers wrote fault telemetry through local dump paths.

Current proof:

- `Docs/Reports/VAULT_EXORCISM_REPORT_1302.json`
- `rawForbiddenPersistentNativeCollectionCandidates = 1`
- `scopedForbiddenPersistentNativeCollectionCandidates = 0`
- `excludedForbiddenPersistentCandidates = 1`

Source proof:

- `Assets/_Project/Scripts/Physics/VerletCableDTOs.cs:625-627` still has `VerletCableNodeBuffer.Nodes`, but it is a `ref struct` cable/tether lane and was not 1302's ownership.
- `Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageRuntime.cs:884-913` now routes fatal state through `GlobalTelemetryBus.TryDumpBlackboxNow`.

Status: 1302 scoped persistent alias count was already zero; vehicle local fault writer was moved.

Residual:

- Core `GlobalTelemetryBus` writer is still managed IO internally.
- Broader physics tree still contains runtime local dump writers in Cavitation, Buoyancy, KCC, Exosuit, Seaglide, HabitatFluid, and excluded Tether/Cable lanes.

## Agent 1304 - Voxel / SurfaceNets

Old complaint:

- `HectonVoxelVolume` had four scene-lifetime `NativeArray<byte>` fields.
- `VoxelDeltaProcessor` had persistent native queue/state and persistent native fields.
- `ScheduledCompactionRequest` stored `NativeArray` scratch views across frames.
- `VoxelSurfaceNetsVaultBuffers` stored many `NativeArray` view fields.
- GPU upload dispatcher retained lock-buffer views as fields.

Current proof:

- `Docs/Reports/VAULT_EXORCISM_REPORT_1304.json`
- SurfaceNets `forbiddenPersistentCandidates = 0`
- SurfaceNets `forbiddenMonoBehaviourCandidates = 0`
- `filteredTargetForbiddenPersistentCandidates = 0`
- `Docs/Reports/VAULT_EXORCISM_APEX_REVIEW_1304.md` confirms black-box lock repair.

Source proof:

- `Assets/_Project/Scripts/VoxelDeltaProcessor.cs:140` uses `Docs/AgentLogs/Dump_1304_Voxel.bin`.
- `Assets/_Project/Scripts/VoxelDeltaProcessor.cs:1048-1085` acquires black-box buffer through `TryAcquireWriteLock` and releases.
- `Assets/_Project/Scripts/VoxelDeltaProcessor.cs:6057-6065` `ScheduledCompactionRequest` no longer stores `NativeArray` fields.

Status: 1304 target route mostly fixed.

Residual:

- `Docs/Reports/VOXEL_PAGING_OPTIMIZATION_REPORT_1312.json` reports `success = false`.
- 1312 checks fail because current source is owned by 1304 route:
  - `dumpPath1312 = false`
  - `ownerDumpLeak1304Absent = false`
  - `agent1312LayoutValidatorPresent = false`
- This is a real owner conflict: 1304 currently won on disk; 1312 is not closed.

## Agent 1305 - World Streaming / Residency

Old complaint:

- `TerrainChunkPagerRuntime.cs` had 19 cached raw pointer fields derived from DataVault arrays and runtime-long buffer locks.
- `WorldChunkResidencyManager.cs` had 25/26 persistent native aliases plus `_chunkSpatialLookup`.

Current proof:

- `Docs/Reports/VAULT_EXORCISM_REPORT_1305.json`
- `status = PENDING_PHASE1_REFACTOR_DEEP_AUDIT_PATCH_PASS13_COMPLETE`
- `worldPostScanForbiddenCandidates = 445`
- `streamingAdjacentPostScanCandidatesBefore = 44`
- `streamingAdjacentPostScanCandidatesAfter = 6`
- `remainingTerrainPagerPointerFields = 0`
- `remainingWorldResidencyPersistentNativeFields = 6` at this checkpoint.
- Current fresh audit supersedes this: `WorldChunkResidencyManager.cs = 0` and `TerrainChunkPagerRuntime.cs = 0` forbidden persistent candidates.
- Patch pass 13 did not close the broader world streaming proof debt because terrain pager lifetime locks / unsafe alias strategy still need owner/fence proof.

Source proof:

- `Assets/_Project/Scripts/World/TerrainChunkPagerRuntime.cs:100-116` now stores `VaultGenerationHandle<T>` descriptors, not raw pointer fields.
- `Assets/_Project/Scripts/World/TerrainChunkPagerRuntime.cs:652-680` resolves DataVault arrays to validate/cache lengths and cache unsafe aliases.
- `Assets/_Project/Scripts/World/TerrainChunkPagerRuntime.cs:749-775` still locks many DataVault buffers for runtime lifetime.
- `Assets/_Project/Scripts/World/TerrainChunkPagerRuntime.cs:2305` still writes the blackbox dump through managed `FileStream`.
- Current fresh audit reports no field-level forbidden persistent candidates in `WorldChunkResidencyManager.cs`.

Status: partially fixed.

Residual:

- Terrain pager raw pointer cache is fixed.
- World residency field-level persistent native ownership is currently clean in the fresh ledger.
- Lifetime DataVault buffer locks and managed dump IO remain release blockers.
- Any claim that 13XX fixed NativeArray architecture globally is still false while this remains.

## Agent 1310 - Core Memory

Old complaint:

- `GlobalDataVault` write-lock / buffer-lock / compaction ordering could race if active bits and block locks were published in the wrong order.
- Invalid pointer free/reallocate paths could fail unsafely.

Current proof:

- `Docs/Reports/MEMORY_SENTRY_OPTIMIZATION_REPORT_1310.json`
- `status = PASS`
- `checkCount = 50`
- `failedCount = 0`

Source proof:

- `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` contains guarded `TryAcquireWriteLock`, `ReleaseWriteLock`, `TryLockBuffer`, `TryUnlockBuffer`, `BlockFlagLocked`, and compaction lock checks.

Status: static core lock model improved.

Residual:

- No Unity/player/runtime compaction stress proof was run in this audit.

## Agent 1312 - Voxel Paging

Old complaint:

- Voxel paging/delta compression route needed its own dump path, layout validator, AUP/directory correctness, and dense fallback math.

Current proof:

- `Docs/Reports/VOXEL_PAGING_OPTIMIZATION_REPORT_1312.json`
- `success = false`
- Good checks:
  - `aupDoubleEvidence = true`
  - `compactionScratchVaultBacked = true`
  - `denseFallbackFlagPresent = true`
  - `fuzzerAllSlotsReachableAt10000 = true`
- Failed checks:
  - `agent1312LayoutValidatorPresent = false`
  - `dumpPath1312 = false`
  - `ownerDumpLeak1304Absent = false`

Status: partially useful math/fuzzer work, not integrated.

Residual:

- Current disk source routes voxel black box to 1304, not 1312.
- Needs explicit owner arbitration before more edits.

## Agent 1313 - Data Monolith

Old complaint:

- Boot/fatal markers used temporary `NativeArray<byte>`.
- Fatal boot path used generic string/UTF8/substrings.
- Data Monolith ABI/schema and active binary were stale.

Current proof:

- `Docs/Reports/DATA_MONOLITH_APEX_RECHECK_1313.json`
- `Docs/Reports/DATA_MONOLITH_APEX_PARANOID_PASS5_1313.json`
- `Docs/Reports/DATA_MONOLITH_APEX_PARANOID_PASS6_1313.json`
- `touchedFileTokenScan.newNativeArrayHits = 0`
- Corrected defects include boot marker and fatal boot crash log replacements.
- PASS5 removed `GetSectionDataPointer` and normalized normal section reads to `ReadOnlySpan<T>`.
- PASS6 removed runtime `Encoding.UTF8` calls from active release accessors and reports `activeReleaseScan.windowsRelease.forbiddenHits = 0` and `activeReleaseScan.androidRelease.forbiddenHits = 0`.

Source proof:

- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:5048-5060` uses `stackalloc byte[BootStateRecordBytes]`.
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:5367-5370` uses stackalloc for fixed fatal message.
- `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:595-613` resolves typed section spans without allocating or copying.
- `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs:669` keeps only a private Burst pointer helper for item binary search.

Status: specific NativeArray temp residues fixed.

Residual:

- `strictBlockingCandidateCount = 262`
- `productionCandidateFindingCount = 281`
- Active `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` now matches current format/schema in static validator proof.
- Latest strict release validator still fails: `DATA_MONOLITH_H8BIN_VALIDATOR_RELEASE_BLOCKERS_1313.json`, status `FAIL`, findings `8`.
- Remaining strict release blockers include unbaked StreamingAssets CSV artifacts and runtime text StreamingAssets loads in haptics, nutrient drift, carrion drift, and camera juice.
- Android/Quest/non-Windows release readiness is still rejected.
- Unity import, monolith bake, Play Mode, player boot, profiler, GCMonitor, and dotnet build were not run.

## Agent 1314 - Audio Bridge

Old complaint:

- Native callback descriptor exported pointers from DataVault-resolved `NativeArray` views.
- DataVault relocation could invalidate those native callback pointers.
- First repair using long-lived DataVault pins was rejected because it could block arena relocation/growth.
- WriteIndex slot alignment and managed runtime dump route also needed correction.

Current proof:

- `Docs/Reports/AUDIO_BRIDGE_OPTIMIZATION_REPORT_1314.json`
- `status = PASS_STATIC_APEX_HOT_TELEMETRY_RAW_RING_COMPILE_NOT_RUN`
- `failedChecks = 0`

Source proof:

- `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:490-528` allocates frames/shared/telemetry/dump bytes through `H8Memory.AllocateRaw`.
- `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:539-575` release path gates on `H8Memory.IsInitialized`.
- `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:587-615` refuses stale views and creates transient `NativeArray` views over raw pointers.
- `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:731-756` writes fixed dump bytes and calls native bridge dump.
- `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs:758-785` mirrors telemetry to DataVault under write lock and `finally` release.

Status: static architecture substantially fixed.

Residual:

- Compile was not run after this report.
- Live fuzzer proof is blocked by Unity Editor DataVault context.

## Current Priority Residuals

1. `1305` streaming/residency native ownership debt.

Evidence:

- `TerrainChunkPagerRuntime.cs:749-775`
- `TerrainChunkPagerRuntime.cs:2292-2305`
- `WorldChunkResidencyManager.cs:865-889`
- `WorldChunkResidencyManager.cs:1690-1742`

Required next work:

- Replace `WorldChunkResidencyManager` persistent native fields with descriptors/handles and phase-local views, or document a strict raw-pool owner if stable native memory is truly required.
- Remove runtime-long DataVault pins.
- Move direct persistent native maps/queues/lists into owner-local or DataVault-owned route with explicit owner, phase, failure mode, and proof.

2. `1304` / `1312` voxel owner conflict.

Evidence:

- Current source: `VoxelDeltaProcessor.cs:140` uses `Dump_1304_Voxel.bin`.
- 1312 report: `success = false`, `dumpPath1312 = false`, `ownerDumpLeak1304Absent = false`.

Required next work:

- Pick one owner route for voxel black-box/paging proof.
- Add compatibility wrappers only if needed, but one fact must have one owner and one proof artifact.

3. `1302` broader physics dump route debt.

Evidence:

- `VAULT_EXORCISM_REPORT_1302.json` says broader physics tree still has local dump writers and Core `GlobalTelemetryBus` writer still uses managed IO internally.

Required next work:

- Separate scoped vehicle route from global physics dump architecture.
- Decide whether Core blackbox dump needs a native writer bridge.

4. `1313` Data Monolith release blockers.

Evidence:

- `DATA_MONOLITH_APEX_RECHECK_1313.json`
- `DATA_MONOLITH_APEX_PARANOID_PASS5_1313.json`
- `DATA_MONOLITH_APEX_PARANOID_PASS6_1313.json`
- `strictBlockingCandidateCount = 262`
- latest strict release validator status `FAIL`, findings `8`.

Required next work:

- Burn down release parser/text loader candidates.
- Remove/bake the remaining strict release validator blockers.
- Run Unity/player boot proof before any readiness claim.

## Final Judgment

Direction: correct.

Completeness: incomplete.

The fixes are real where they are scoped. The global native ownership problem remains large. The next honest work should target `1305` first, then resolve `1304/1312`, then clean physics dump/native crash export and Data Monolith release blockers.
