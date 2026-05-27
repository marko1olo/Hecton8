## 2026-05-25 NativeArray / Native Ownership Audit For 13XX Agents

Agent id: UNKNOWN. No `Status_UNKNOWN.md` or `Rationale_UNKNOWN.md` existed on disk at audit start.

Scope: user asked whether 13XX agents fixed the `NativeArray` / native ownership issues, what the old complaints were, and what remains.

Rules used:
- AGENTS.md memory sovereignty rule: persistent `NativeArray<T>`, `NativeList<T>`, `NativeQueue<T>` fields in `MonoBehaviour` or runtime manager classes are forbidden unless core authority / documented bridge.
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`: single native owner, no stale aliases, no mid-Tick `.Complete()`, no shared raw views without contract.
- `.agents-skills/DATA_Runtime_Struct_Layout_ARM64.txt`: NativeArray element DTOs must be unmanaged, finite-safe, 8-byte aligned.
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`: `Get*`/`Resolve*` accessors must not allocate/grow/mutate/publish.

Evidence commands/results:
- `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1304_FULL.json`: full first-party forbidden persistent candidates = 1947, MonoBehaviour candidates = 432.
- `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_X_000.json`: full first-party forbidden persistent candidates = 1853, MonoBehaviour candidates = 403, parse failures = 0.
- Ledger is not perfectly fresh: files newer than ledger exist after `05/25/2026 21:31:29`, including `NativeAudioFrameRingBuffer.cs`, `HectonVoxelVolume.cs`, `GlobalDataVault.cs`, `WorldChunkResidencyManager.cs`, `H8DataMonolithTypes.cs`. Full rescan was not launched because CPU sample was 78.8%.

Old complaint -> current state:

1. 1300 / AI Cognition
- Old complaint: `AIAnxietyTunerWindow.AnxietyTelemetryChartElement` cached a persistent `NativeArray<AnxietyTelemetryEntry>` field; utility/anxiety/apex writers used mutable vault views without explicit write lock/finally; black-box dump path did not include 1300-owned file.
- Current proof: `VAULT_EXORCISM_REPORT_1300.json` says beforePersistentNativeAliasFields=1, after=0, forbiddenMonoBehaviourCandidates=0, verdict STATIC_GREEN.
- Source proof: `AIAnxietyTunerWindow.cs:365-366` stores only `IDataVault` and `VaultGenerationHandle<AnxietyTelemetryEntry>`; `:398` resolves `NativeArray` method-locally inside draw.
- Current state: fixed in scoped domain. Remaining NativeArray fields in AI cognition are vault-view structs / job parameters, not persistent owner aliases.

2. 1301 / AI Ecology spatial grid
- Old complaint: query struct retained `NativeArray<SpatialGridEntryDTO>`, `NativeArray<SpatialGridBucketRangeDTO>`, `NativeArray<AmbientEntityAupDTO>` across possible DataVault relocation; diagnostic dump had unsafe/fault-path edge cases and later a long-lived raw pointer snapshot.
- Current proof: `VAULT_EXORCISM_APEX_REAUDIT_1301.json` reports 0 forbidden persistent candidates in AI/Ecosystem, AI/Ambient, Animation/FaunaProcedural, and Scripts/Ecosystem scoped scans.
- Source proof: `ShinobuSpatialGridSolver.cs:328-330` stores handles, not arrays; `:571-573` uses `TryReadOnlyHandle` method-locally; `:1563-1616` writes dump snapshot under `TryAcquireWriteLock` with finally release; `:1744-1778` worker reads snapshot under `TryLockBuffer`/`TryUnlockBuffer`.
- Caveat: report explicitly says the worker disk output still uses managed `FileStream`, not native plugin IO.
- Current state: scoped native alias problem fixed; native-only crash-export requirement not fully fixed.

3. 1302 / Physics vehicles
- Old complaint: raw scan found one forbidden persistent native alias in physics, `VerletCableNodeBuffer.Nodes`, but it belongs to cable/tether ownership, excluded from 1302 scope. Local vehicle fault dump wrote raw NativeArray bytes through local writers.
- Current proof: `VAULT_EXORCISM_REPORT_1302.json` says rawForbiddenPersistentNativeCollectionCandidates=1, scopedForbiddenPersistentNativeCollectionCandidates=0, excludedForbiddenPersistentCandidates=1.
- Source proof: `VerletCableDTOs.cs:625-627` still has `ref struct VerletCableNodeBuffer { NativeArray<VerletNodeDTO> Nodes; }`, excluded cable/tether lane. `VehicleComponentDamageRuntime.cs:884-913` routes fatal state through `GlobalTelemetryBus.TryDumpBlackboxNow`.
- Residual from report: Core `GlobalTelemetryBus` writer still managed IO internally; broader physics tree still has runtime local dump writers in Cavitation/Buoyancy/KCC/Exosuit/Seaglide/HabitatFluid and excluded Tether/Cable.
- Current state: 1302 did not need to remove a scoped persistent NativeArray; it moved vehicle fault dump away from local raw writer, but physics-wide dump/native ownership is not clean.

4. 1304 / Voxel and SurfaceNets
- Old complaint: `HectonVoxelVolume` had 4 scene-lifetime NativeArray fields; `VoxelDeltaProcessor` had persistent NativeQueue and 11 native fields; `ScheduledCompactionRequest` stored NativeArray scratch views across frames; `VoxelSurfaceNetsVaultBuffers` stored 18 NativeArray view fields; GPU upload dispatcher kept lock-buffer views as fields.
- Current proof: `VAULT_EXORCISM_REPORT_1304.json` says SurfaceNets forbidden persistent candidates=0, filteredTargetForbiddenPersistentCandidates=0; full repo still 1866 in that report. `VAULT_EXORCISM_APEX_REVIEW_1304.md` says blackbox lock fixed.
- Source proof: `VoxelDeltaProcessor.cs:140` route is `Docs/AgentLogs/Dump_1304_Voxel.bin`; `:1048-1085` blackbox buffer is acquired by `TryAcquireWriteLock` and released; `ScheduledCompactionRequest` at `:6057-6065` no longer stores NativeArray fields.
- Current conflict: 1312 report fails because current source is owned by 1304 route, not 1312 route. `VOXEL_PAGING_OPTIMIZATION_REPORT_1312.json` has success=false, `dumpPath1312=false`, `ownerDumpLeak1304Absent=false`, `agent1312LayoutValidatorPresent=false`.
- Current state: 1304 target work is mostly fixed; 1304/1312 ownership conflict remains unresolved.

5. 1305 / World streaming and residency
- Old complaint: `TerrainChunkPagerRuntime.cs` had 19 cached raw pointer fields derived from DataVault arrays and runtime-long locks; `WorldChunkResidencyManager.cs` had 25/26 persistent native aliases plus `_chunkSpatialLookup`.
- Current proof: `VAULT_EXORCISM_REPORT_1305.json` says worldPostScanForbiddenCandidates=445, streamingAdjacentPostScanCandidates=45, status PENDING_PHASE1_REFACTOR. It says patch pass only unified dump route and AUP casts; no Phase 1 native alias migration.
- Source proof: `TerrainChunkPagerRuntime.cs:118-136` still has cached pointer fields; `:675-693` fills them from `NativeArrayUnsafeUtility.GetUnsafePtr`; `:806` still uses `TryLockBuffer`. `WorldChunkResidencyManager.cs:861-886` still has many private NativeArray/NativeList/NativeQueue/NativeParallel fields; `:1686-1701` still allocates persistent native maps/queues/lists.
- Current state: not fixed. This is the clearest remaining native ownership debt from the 13XX group.

6. 1310 / Core memory
- Old complaint: DataVault write-lock / buffer-lock / compaction ordering could race if active bits and block locks were published in wrong order; invalid pointer frees had fatal/unsafe paths.
- Current proof: `MEMORY_SENTRY_OPTIMIZATION_REPORT_1310.json` says status PASS, checkCount=41, failedCount=0.
- Source proof: `GlobalDataVault.cs` contains guarded `TryAcquireWriteLock`, `ReleaseWriteLock`, `TryLockBuffer`, `TryUnlockBuffer`, and compaction checks around `BlockFlagLocked` / active lock masks.
- Current state: static core lock model improved. No Unity/runtime compaction stress proof was run in this audit.

7. 1313 / Data Monolith
- Old complaint: fixed boot/fatal markers used temporary `NativeArray<byte>`, generic string/UTF8/substrings, and Data Monolith ABI/schema was stale.
- Current proof: `DATA_MONOLITH_APEX_RECHECK_1313.json` says touchedFileTokenScan.newNativeArrayHits=0. Corrected defects list names boot marker and fatal boot crash log replacements.
- Source proof: `GameBootstrapper.cs:5048-5060` uses `stackalloc byte[BootStateRecordBytes]`; `:5367-5370` uses stackalloc for fixed fatal message.
- Residual: same report says strictBlockingCandidateCount=262, productionCandidateFindingCount=281, active `static_data.h8bin` is schema stale and `REBAKE_REQUIRED_FAIL_CLOSED`; Unity/import/player/profiler/build not run.
- Current state: specific NativeArray temp residues fixed; Data Monolith release readiness not fixed.

8. 1314 / Audio bridge
- Old complaint: native callback descriptor exported pointers from DataVault-resolved NativeArrays; those pointers could become stale after DataVault relocation. First fix with long-lived DataVault pins was rejected because it could block arena growth. Also WriteIndex slot alignment and managed dump writer issues existed.
- Current proof: `AUDIO_BRIDGE_OPTIMIZATION_REPORT_1314.json` says status PASS_STATIC_APEX_DTO_POINTER_FIRST_LAYOUT_COMPILE_NOT_RUN, failedChecks=0.
- Source proof: `NativeAudioFrameRingBuffer.cs:483-506` allocates frames/shared/dump bytes through `H8Memory.AllocateRaw`; `:522-545` release gates on `H8Memory.IsInitialized`; `:561-575` refuses stale views then creates transient NativeArray views over raw pointers; `:624-635`, `:704-715`, `:730-743` use telemetry `TryAcquireWriteLock` and `finally ReleaseWriteLock`.
- Residual: compile not run; live Unity fuzzer is blocked by editor DataVault context.
- Current state: static architecture of audio bridge native arrays/pointers is substantially fixed; runtime proof absent.

Bottom line:
- The 13XX work is not just cosmetic. Several scoped NativeArray/native ownership problems were actually fixed: 1300, 1301 scoped alias, 1304 target voxel, 1310 core lock semantics, 1313 boot/fatal temp NativeArray, 1314 audio bridge pointer lifetime.
- But the project is not clean. The latest full ledger still had 1853 forbidden persistent candidates and 403 MonoBehaviour candidates. The biggest proven live leftovers from this audit are 1305 world streaming/residency, 1312/1304 voxel ownership conflict, 1302 broader physics dump route/core managed IO, and 1313 Data Monolith release blockers.
- No honest release-ready claim is possible from these artifacts. Static direction is correct; integration is incomplete.

## 2026-05-25 Report Promotion

User requested the same audit to be written directly into docs/reports.

Artifacts written:
- `Docs/Reports/NATIVE_ARRAY_13XX_OWNERSHIP_AUDIT_UNKNOWN.md`
- `Docs/Reports/NATIVE_ARRAY_13XX_OWNERSHIP_AUDIT_UNKNOWN.json`
- `Docs/Tasks/Status_UNKNOWN.md`
- `Docs/AgentLogs/Rationale_UNKNOWN.md`

No runtime/source code was changed in this promotion pass.

## 2026-05-25 Current Recheck And Scripts Map

User requested current state and a full map of the Scripts folder.

What changed since the previous audit:
- New comparable full ledger proof: `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1303_WHOLE_SCRIPTS.json`.
- Latest full ledger counters: `forbiddenPersistentCandidates=1826`, `forbiddenMonoBehaviourCandidates=384`, `parseFailures=0`.
- Delta from `VAULT_NATIVE_ALIAS_LEDGER_X_000.json`: persistent `1849 -> 1826`, MonoBehaviour `403 -> 384`.
- Delta from `VAULT_NATIVE_ALIAS_LEDGER_1304_FULL.json`: persistent `1947 -> 1826`, MonoBehaviour `432 -> 384`.
- `VAULT_NATIVE_ALIAS_LEDGER_X_000.json` was overwritten at `2026-05-25 23:01:30` as a scoped one-file ledger, so its current zero counts are not comparable to full-project ledgers.

1305 correction:
- Old report statement that `TerrainChunkPagerRuntime.cs:118-136` still had raw pointer fields is stale.
- Current source has handle fields at `TerrainChunkPagerRuntime.cs:100-116`.
- Current source validates/resolves arrays at `TerrainChunkPagerRuntime.cs:652-673`.
- Remaining 1305 blockers are lifetime DataVault locks at `TerrainChunkPagerRuntime.cs:749-775`, managed `FileStream` dump at `TerrainChunkPagerRuntime.cs:2292-2305`, and `WorldChunkResidencyManager.cs:865-889` persistent native fields with allocation/use around `WorldChunkResidencyManager.cs:1690-1742`.
- Latest 1305 status: `PENDING_PHASE1_REFACTOR_DEEP_AUDIT_PATCH_PASS12_COMPLETE`, `streamingAdjacentCandidatesAfter=21`, `remainingWorldResidencyPersistentNativeFields=26`.

1313 correction:
- `Docs/Reports/DATA_MONOLITH_APEX_PARANOID_PASS5_1313.json` says `GetSectionDataPointer` hits are 0 and normal reads use `ReadOnlySpan<T>`.
- `Docs/Reports/DATA_MONOLITH_APEX_PARANOID_PASS6_1313.json` says active Windows/Android release forbidden token hits are 0, but strict release validator status is `FAIL` with 8 findings.
- `H8StaticDataArena.cs:595-613` is the normal span read route.
- `H8StaticDataArena.cs:669` keeps a private Burst pointer helper only.
- Active `static_data.h8bin` static-validates current format/schema in the 1313 recheck, but release is still rejected: latest strict validator has 8 findings and older global parser purge still recorded broader production text/config debt.

Scripts folder map written:
- `Docs/Reports/SCRIPTS_FOLDER_MAP_UNKNOWN.md`
- `Docs/Reports/SCRIPTS_FOLDER_MAP_UNKNOWN.json`
- `Docs/Reports/SCRIPTS_FILE_INDEX_UNKNOWN.tsv`

Scripts map counters:
- Directories: 326
- Files: 5579
- C# files: 2420
- C# lines: 1765753
- asmdef files: 162
- Snapshot caveat: map files were written at `2026-05-25 23:01:22`; other agents changed source files again immediately after that.

Cinematic Cheats used:
- None. Documentation/evidence update only.

Exact Microseconds saved:
- Runtime: 0 us claimed.
- Audit/integration saving: prevents repeated stale investigation of removed 1305 raw pointer fields; not a runtime measurement.

Verification:
- JSON parse passed for `SCRIPTS_FOLDER_MAP_UNKNOWN.json`.
- JSON parse passed for `NATIVE_ARRAY_13XX_OWNERSHIP_AUDIT_UNKNOWN.json`.
- Fresh heavy Roslyn/build was not launched because CPU guard stayed above limit (`74.2%`, `62.8%`, then `72.3%`).

## 2026-05-25 Forbidden Native Burndown Rate And Quality

User asked speed, quality, and whether every forbidden persistent / forbidden MonoBehaviour candidate must be removed.

Artifacts written:
- `Docs/Reports/FORBIDDEN_NATIVE_BURNDOWN_RATE_QUALITY_UNKNOWN.md`
- `Docs/Reports/FORBIDDEN_NATIVE_BURNDOWN_RATE_QUALITY_UNKNOWN.json`

Measured full-ledger state:
- Latest full ledger: `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_X_000.json`, modified `2026-05-25 23:16:10`.
- Scanned files: `2420`.
- Parse failures: `0`.
- Forbidden persistent candidates: `1784`.
- Forbidden MonoBehaviour candidates: `364`.
- Allowed transient job fields: `5494`.
- Allowed core memory authority fields: `45`.
- Allowed stack-only ref struct views: `19`.

Measured speed:
- Full day window `07:46:40 -> 23:16:10`: persistent `2042 -> 1784`, rate `16.65/hour`; MonoBehaviour `510 -> 364`, rate `9.42/hour`.
- Active 13XX window `17:04:25 -> 23:16:10`: persistent `1947 -> 1784`, rate `26.31/hour`; MonoBehaviour `432 -> 364`, rate `10.97/hour`.
- Late active window `21:32:49 -> 23:16:10`: persistent `1853 -> 1784`, rate `40.05/hour`; MonoBehaviour `403 -> 364`, rate `22.64/hour`.

Quality finding:
- Direction is correct and source-visible in scoped systems: 1305 world streaming dropped streaming-adjacent candidates `44 -> 6`; 1306 construction moved `DroneFleetManager.cs` `36 -> 4`, `FluidPipeGraphRuntime.cs` `19 -> 0`, `LogisticsPipeTransportScheduler.cs` `10 -> 0`, `LogisticsRouteScratchMemory.cs` `7 -> 0`, and `DroneFleetManager_Transactions.cs` `8 -> 0`.
- Release quality is not reached. Most proof is static-only. Compile/player/profiler/live fuzzer proof is absent in key reports.
- Current top residuals are not cosmetic: `HectonVoxelEngine.cs` 124, `VegetationMemoryPool.cs` 75, `PlayerInventory.cs` 63, `DestructibleOrganicManager.cs` 50, `LogisticsNetworkGraph.cs` 50.

Policy answer:
- Do not remove every `NativeArray` or native container. Remove/migrate or explicitly reclassify every `forbidden_persistent_native_alias_candidate`.
- Keep valid transient job fields, method-local DataVault views released in `finally`, stack-only views, core memory authority, fixed black-box rings with owner proof, and editor-only validators.

Cinematic Cheats used:
- None. Documentation/evidence update only.

Exact Microseconds saved:
- Runtime: 0 us claimed.
- Audit/integration saving only: false rate claims and blind NativeArray deletion are avoided.

## 2026-05-26 Fresh Forbidden Native Recheck

User requested another recheck/update.

Fresh artifact:
- `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_UNKNOWN_CURRENT_20260526_0052.json`
- `Docs/Reports/FORBIDDEN_NATIVE_BURNDOWN_RATE_QUALITY_UNKNOWN.md` updated.
- `Docs/Reports/FORBIDDEN_NATIVE_BURNDOWN_RATE_QUALITY_UNKNOWN.json` updated.
- `Docs/Reports/NATIVE_ARRAY_13XX_OWNERSHIP_AUDIT_UNKNOWN.md` updated.
- `Docs/Reports/NATIVE_ARRAY_13XX_OWNERSHIP_AUDIT_UNKNOWN.json` updated.

Guard before audit:
- CPU sample: `16.5%`.
- No dotnet/csc process was shown by the guard command.
- Ran prebuilt audit executable only; no rebuild/build.

Fresh ledger result:
- Scanned files: `2421`.
- Parse failures: `0`.
- Total native field declarations: `7324`.
- Forbidden persistent candidates: `1770`.
- Forbidden MonoBehaviour candidates: `358`.
- Job transient fields: `5490`.
- Stack-only ref struct views: `19`.
- Core memory allowed fields: `45`.
- Raw pointer fields: `865`.
- Hash: `68217d9f155aeb5233cbb3cc004518df4a1eb2c1d0d222bd810ca241008bbe31`.

Rejected artifact:
- `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_X_000.json`, modified `2026-05-26 00:47:20`, reported `2138/581`.
- It was rejected as current proof because it contradicted current source line checks: it reported old terrain raw pointers and old drone native fields that are not present in the checked files.

New trend:
- From accepted `2026-05-25 23:16:10`: persistent `1784 -> 1770`, MonoBehaviour `364 -> 358`.
- From trusted `2026-05-25 23:46:40`: persistent `1778 -> 1770`, MonoBehaviour `358 -> 358`.
- Current late rate from `23:46:40 -> 00:55:25`: persistent `6.98/hour`; MonoBehaviour `0/hour`.

Quality update:
- `WorldChunkResidencyManager.cs`, `TerrainChunkPagerRuntime.cs`, `DroneFleetManager.cs`, `FluidPipeGraphRuntime.cs`, and `VoxelDeltaProcessor.cs` now have `0` current forbidden persistent candidates in the fresh full ledger.
- `HabitatGraphManager.cs` remains at `13`, all inside private view-bundle structs (`HabitatGraphWriteViews`, `HabitatFloodGraphJobViews`), not physical owner fields. This still needs stack-only/ref-struct classification or scanner rule proof.
- Top residuals remain outside the cleaned 13XX target owners: `HectonVoxelEngine.cs` 124, `VegetationMemoryPool.cs` 75, `PlayerInventory.cs` 63, `DestructibleOrganicManager.cs` 50, `LogisticsNetworkGraph.cs` 50.

Cinematic Cheats used:
- None. Documentation/evidence update only.

Exact Microseconds saved:
- Runtime: 0 us claimed.

## 2026-05-26 Build Repair And Warning Closure

User requested build pass after main work and full fixing of errors/warnings.

Artifacts written:
- `Docs/Reports/BUILD_UNKNOWN_20260526_010802.log`
- `Docs/Reports/BUILD_UNKNOWN_20260526_011653.log`
- `Docs/Reports/BUILD_UNKNOWN_20260526_011946.log`
- `Docs/Reports/BUILD_UNKNOWN_20260526_012406.log`
- `Docs/Reports/BUILD_REPAIR_UNKNOWN_20260526.md`
- `Docs/Reports/BUILD_REPAIR_UNKNOWN_20260526.json`

Guard:
- Builds were launched only after checking CPU and compiler/dotnet process state.
- Final command: `dotnet build .\Hecton8.slnx -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false *> Docs/Reports/BUILD_UNKNOWN_20260526_012406.log`

What was wrong:
- First build failed: `75` errors, `5` warnings.
- Error classes: missing imports, stale MPSC writer API, removed DataVault local, stale drone persistent native array references, invalid C# address/out and `in` property usage, private padding initialization, `uint` span index, unassigned locals/out values, unreachable catch order, unavailable shader tag dependency.
- Warning classes: function pointer comparison, obsolete Unity editor search overload, unused exception locals, stale missing `Hecton8.Input.csproj` reference.

What was done:
- Fixed compile errors in source files, not by suppressing diagnostics.
- Kept drone transactions on current buffer-owner accessors instead of restoring removed persistent native fields.
- Updated warning sources until final build was warning-clean.
- Added conditional project-reference removal in `Directory.Build.targets` only when `Hecton8.Input.csproj` is absent.

Final proof:
- `Docs/Reports/BUILD_UNKNOWN_20260526_012406.log:66` -> `Build succeeded.`
- `Docs/Reports/BUILD_UNKNOWN_20260526_012406.log:67` -> `0 Warning(s)`
- `Docs/Reports/BUILD_UNKNOWN_20260526_012406.log:68` -> `0 Error(s)`

Residual caveats:
- This proves `.slnx` C# build only.
- Unity Editor import, playmode tests, player build, Burst compile, and profiler validation were not executed in this pass.
- Worktree was already heavily dirty from concurrent agents; unrelated dirty files were not normalized.

Cinematic Cheats used:
- None. Compile/warning repair only.

Exact Microseconds saved:
- Runtime: `0 us` claimed.

## 2026-05-26 Current Token And Native Memory Progress Check

What was wrong:
- The previous token snapshot was stale after more agent work.
- Scoped agent reports could be mistaken for global native-memory cleanup proof.
- CPU/compiler guard blocked a new self-run full Roslyn scan and current build.

What was done:
- Regenerated `Docs/TOKEN_USAGE_LEDGER.md` and `Docs/Reports/TOKEN_USAGE_AUDIT_2026-05-26.*`.
- Compared comparable full ledgers: `VAULT_NATIVE_ALIAS_LEDGER_UNKNOWN_CURRENT_20260526_0052.json` and `VAULT_NATIVE_ALIAS_LEDGER_1315_PASS14.json`.
- Reviewed current 1315-1329 status/log evidence.
- Wrote `Docs/Reports/NATIVE_MEMORY_CLEANUP_PROGRESS_UNKNOWN_20260526.md` and `.json`.

Current token stats:
- Total tokens: `100,190,687,073`.
- Input/cached/output/reasoning: `99,842,361,313` / `95,934,146,304` / `347,808,960` / `109,975,170`.
- Sessions/files: `2,707` / `2,832`.

Current native ledger stats:
- Full persistent candidates: `1770 -> 1081`, down `689`.
- Full MonoBehaviour candidates: `358 -> 82`, down `276`.
- Current residuals: `1081` persistent, `82` MonoBehaviour, `861` raw pointer fields.
- Current top residual file: `VegetationMemoryPool.cs` with `65` persistent candidates.

Quality verdict:
- Real progress, not global clean.
- Scoped green reports are mostly credible where they use Roslyn/hot-path/build proof.
- `1316` remains explicitly partial/red in its own logs.
- Current build proof is pending because guard showed CPU `100%` and active `dotnet`.

Cinematic Cheats used:
- None by this audit. Reviewed agents used visual/data-layout cheats where documented.

Exact Microseconds saved:
- Runtime: `0 us` claimed by this audit.

## 2026-05-26 Token And Documentation Stats Refresh

What was wrong:
- Current token totals existed only after a fresh recount, while stable docs still referenced older token/doc boundaries.
- Fresh generated Markdown failed the repository UTF-8-SIG structure gate.

What was done:
- Updated `Tools/CodexTokenUsageAudit_20260525.py` to emit current dated Markdown token reports with UTF-8-SIG.
- Regenerated `Docs/TOKEN_USAGE_LEDGER.md`.
- Created `Docs/Reports/TOKEN_USAGE_AUDIT_2026-05-26.md` and `.json`.
- Updated current root/report/architecture/global-map docs with exact current counters.
- Reran `VerifyDocStructure.py`: `pass=true`, active docs `704`, non-BOM `0`.
- Reran `OOP_Doc_Scanner.py`: `finalPass=true`, active docs `704`, source sync `true`.

Current stats:
- Total local Codex JSONL tokens: `100,190,687,073`.
- Usage sessions: `2,707`; JSONL files: `2,832`.
- First-party `Assets/_Project` C#: `2509` files / `1,810,267` lines.
- Broad source snapshot: `15,421` files / `16,983,272` lines.
- Superseded 2026-05-25 token audit moved to `Docs/_Archive/TokenUsage_2026-05-25/`.

Cinematic Cheats used:
- None.

Exact Microseconds saved:
- Runtime: `0 us` claimed.

## 2026-05-26 Build Recheck And Root Docs Refresh

User challenged the build claim and requested root documentation updated concisely and honestly.

Fresh build proof:
- Guard before build: CPU `2`, compiler process count `0`.
- Command: `dotnet build .\Hecton8.slnx -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false`
- Log: `Docs/Reports/BUILD_UNKNOWN_RECHECK_20260526_013709.log`
- Exit: `0`
- Summary: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)` at lines `66-68`.
- Post-build compiler process check: none shown.

Docs updated:
- `BUILD_PLAYTEST_ISSUES.md`
- `MASTER_RELEASE_WORK_PLAN.md`
- `Docs/ROOT_DOCS_REFERENCE.md`
- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/QUALITY_GATES.md`
- `Docs/SYSTEMS_CONTRACTS.md`
- `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`
- `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`
- `Docs/ARCHITECTURE/README.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- `Docs/Reports/BUILD_REPAIR_UNKNOWN_20260526.md`
- `Docs/Reports/BUILD_REPAIR_UNKNOWN_20260526.json`
- `Docs/Reports/ROOT_DOCS_BUILD_EVIDENCE_REFRESH_UNKNOWN_20260526.md`
- `Docs/Reports/ROOT_DOCS_BUILD_EVIDENCE_REFRESH_UNKNOWN_20260526.json`

What changed:
- Old active-doc statements that current CLI compile was blocked by CPU/compiler contention or `NETSDK1004` were superseded.
- New active boundary is full `.slnx` CLI_COMPILE pass only.
- Runtime proof is still explicitly pending: Unity import, Console, Play Mode, player build, profiler, GCMonitor, save/load, scene wiring, visual quality, and platform readiness.

Cinematic Cheats used:
- None.

Exact Microseconds saved:
- Runtime: `0 us` claimed.

## 2026-05-26 Documentation Gate Closure

After root/stable doc refresh, documentation validators were run.

Initial validator state:
- `python Tools/VerifyDocStructure.py`: `pass=false`; fence issue in `Docs/Reports/DATA_MONOLITH_BLOB_V2_MIGRATION_1313.md`; active non-BOM docs `92`.
- `python Tools/OOP_Doc_Scanner.py`: `finalPass=false`; two over-threshold architecture lines.

Repairs:
- Added `text` language tags to two code fences in `Docs/Reports/DATA_MONOLITH_BLOB_V2_MIGRATION_1313.md`.
- Split the fleet snapshot line in `Docs/ARCHITECTURE/DRONE_FLEET_PROTOCOL.md`.
- Split the loop165 proof/residual line in `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`.
- Converted the `92` active docs listed by `DOC_STRUCTURE_VALIDATION_X_012.json` to UTF-8 BOM without content edits.

Final validator state:
- `python Tools/VerifyDocStructure.py`: `pass=true`, duplicate headers `0`, broken links `0`, fence issues `0`, stale parameter files `0`, non-BOM active docs `0`.
- `python Tools/OOP_Doc_Scanner.py`: `finalPass=true`, `sourceSyncPass=true`, `activeStaleParameterFiles=0`, `wordReductionPercent=35.86017095528346`.

Cinematic Cheats used:
- None.

Exact Microseconds saved:
- Runtime: `0 us` claimed.

## 2026-05-26 Second Build And Root Docs Recheck

User repeated the challenge and requested root documentation recheck again.

Fresh build proof:
- Guard before build: CPU `5`, compiler process count `0`.
- Command: `dotnet build .\Hecton8.slnx -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false`
- Log: `Docs/Reports/BUILD_UNKNOWN_RECHECK_20260526_020504.log`
- Exit: `0`
- Summary: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)` at lines `66-68`.

Docs updated:
- Current root/stable compile boundary now points to `Docs/Reports/BUILD_UNKNOWN_RECHECK_20260526_020504.log`.
- The older `013709` proof remains historical, not current boundary.

Cinematic Cheats used:
- None.

Exact Microseconds saved:
- Runtime: `0 us` claimed.

## 2026-05-26 Unity 6 NativeArray CopyFromFast Audit

What was wrong:
- A 2018 `CopyFromFast`/`Il2CppSetOption` optimization request could be misapplied to Unity 6 without proving the current `NativeArray<T>.CopyFrom(T[])` implementation.
- Project source had no `Il2CppSetOption`, no `CopyFromFast`, and only one repeated runtime managed-array `CopyFrom` candidate.

What was done:
- Inspected installed Unity `6000.4.1f1` `UnityEngine.CoreModule.dll` assemblies with Mono.Cecil.
- Verified current Unity 6 managed-array `CopyFrom` reaches `GCHandle.Alloc(..., Pinned)` and `UnsafeUtility.MemCpy`, not a per-element managed index loop.
- Searched project source for `CopyFrom`, `CopyTo`, `NativeArray.Copy`, `GCHandle.Alloc`, `Il2CppSetOption`, `UnsafeUtility.MemCpy`, `MemMove`, and unsafe NativeArray pointer APIs.
- Replaced `ProximityColliderSystem` status copy with `NativeArray<byte>.Copy(_prevStatus, 0, prevStatus, 0, _pointCount)` to handle reused DataVault buffers larger than the active logical point count.
- Ran post-patch CLI build. First log `BUILD_UNKNOWN_COPYFROMFAST_PATCH_20260526.log` passed with `7` `CS0420` warnings in `PlayerCriticalProceduralAudioRenderer.cs`.
- Replaced seven `Volatile.Read(ref _targetGranularMaxVoiceCount)` calls with direct reads of the already `volatile int` field.
- Reran CLI build. Final log `BUILD_UNKNOWN_COPYFROMFAST_PATCH_RECHECK_20260526.log` has `Build succeeded.`, `0 Warning(s)`, `0 Error(s)` at lines `66-68`.
- Updated root/stable current-build references to point at `Docs/Reports/BUILD_UNKNOWN_COPYFROMFAST_PATCH_RECHECK_20260526.log`.
- Wrote `Docs/Reports/UNITY6_NATIVEARRAY_COPYFROM_FAST_AUDIT_UNKNOWN_20260526.md` and `.json`.
- Reclosed docs: `VerifyDocStructure.py pass=true activeDocCount=709`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=709`.

Cinematic Cheats used:
- None. This was memory-copy route validation, not presentation simulation.

Exact Microseconds saved:
- `0` claimed. Source change is a correctness fix; verdict is to avoid an unnecessary unsafe optimization until profiler/player proof exists.

## 2026-05-26 Unity API Trap Detector Audit

What was wrong:
- `UnityApiTrapDetector` matched `.material` and `.vertices` by raw substring.
- Local mimic of old behavior reported `82` runtime-script hits: `.material=68`, `.vertices=14`.
- Representative false positives included `materialIds`, `materialReferenceIndex`, `fontAsset.material`, `Graphic/Image.material`, RenderGraph `passData.material`, `mesh.vertices = ...` setters, and documented `COLD ALLOC:` mesh getters.

What was done:
- Patched `Assets/_Project/Scripts/Editor/UnityApiTrapDetector.cs`.
- Patched `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs`.
- Added exact member matching.
- Added simple per-file type indexing for non-renderer material owners: `Graphic`, `MaskableGraphic`, `Image`, `RawImage`, `TMP_Text`, `TextMeshProUGUI`, and `TMP_FontAsset`.
- Excluded known UI/TMP/RenderGraph material lanes from the renderer-material warning.
- Changed `Mesh.vertices` rule to getter-only detection.
- Honored canonical `COLD ALLOC:` annotations for detector rules.
- Replaced cockpit `mesh.vertices` getter with reusable `List<Vector3>` plus `Mesh.GetVertices`.
- Wrote `Docs/Reports/UNITY_API_TRAP_DETECTOR_AUDIT_UNKNOWN_20260526.md` and `.json`.

Current static estimate:
- Patched-detector mimic over runtime scripts: `0` unwaived hits.

Blocked proof:
- CLI build was not launched at the first attempt because CPU guard was `65.2%`, above the project threshold.
- Build was later launched after guard passed: CPU `34.2%`, `dotnet=0`, `csc=0`.
- `Docs/Reports/BUILD_UNKNOWN_UNITY_API_TRAP_DETECTOR_20260526.log` has `Build succeeded.`, `0 Warning(s)`, `0 Error(s)` at lines `66-68`.
- After the cockpit `mesh.vertices` cleanup, final build proof is `Docs/Reports/BUILD_UNKNOWN_UNITY_API_TRAP_DETECTOR_MESH_RECHECK_20260526.log`, also `Build succeeded.`, `0 Warning(s)`, `0 Error(s)` at lines `66-68`.
- Documentation gates reclosed: `VerifyDocStructure.py pass=true activeDocCount=710`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=710 sourceSyncPass=true`.

Cinematic Cheats used:
- None.

Exact Microseconds saved:
- Runtime: `0 us` claimed. This is compliance-tool precision, not measured runtime optimization.
## 2026-05-26 - Unity Runtime API Trap Cleanup

What was wrong:
- Unity `Renderer.sharedMaterials` copied-array getter remained in `ContextualPhysicalIkRig`.
- Generic `GetComponents*<T>` array overloads remained in cold first-party runtime setup paths.
- `UnityApiTrapDetector` did not detect `Renderer.sharedMaterials` getter or generic `GetComponents*<T>` array overloads, and lacked `#if UNITY_EDITOR` awareness.

What was done:
- `ContextualPhysicalIkRig`: moved material-slot read/write to reusable `List<Material>`, `GetSharedMaterials`, and `SetSharedMaterials`.
- `PrologueSequenceRegistryBridge`: replaced `GetComponents<MonoBehaviour>()` array overload with reusable `List<MonoBehaviour>`.
- `HarvestableOutcrop`: replaced child renderer/collider array discovery with retained lists.
- `DebrisManager` / `OrganicDebrisProfile`: replaced temporary `MeshFilter[]` scan with a retained list while preserving serialized profile arrays.
- `UnityApiTrapDetector`: added `Renderer.sharedMaterials` getter rule, generic `GetComponents*<T>` array-overload rule, and preprocessor editor-only skipping.

Cinematic cheats used:
- None. This pass removed hidden managed allocation routes and improved compliance tooling; it did not change simulation or presentation fidelity.

Exact microseconds saved:
- `0` claimed. No profiler or Unity Play Mode proof was run.

Proof:
- Report: `Docs/Reports/UNITY_RUNTIME_API_TRAP_CLEANUP_UNKNOWN_20260526.md`
- Machine report: `Docs/Reports/UNITY_RUNTIME_API_TRAP_CLEANUP_UNKNOWN_20260526.json`
- Static mimic result: `TOTAL=0`
- `git diff --check`: passed with LF/CRLF working-copy warnings only.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=693 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=693 sourceSyncPass=true`.
- CLI build: after guard blocked launches at `100%`, `96%`, `88%`, `77%`, `74%`, `62%`, and `73%`, CPU reached `21%` with compiler count `0`; `Docs/Reports/BUILD_UNKNOWN_RUNTIME_API_TRAP_CLEANUP_20260526.log` has `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.

## 2026-05-26 - Unity Player Shader Reference Fix

What was wrong:
- PDA map hologram, HUD dither/data pulse, and harpoon tracer runtime materials depended on `Shader.Find` or empty prefab shader fields.
- Unity documents that `Shader.Find` can succeed in Editor and fail in player builds when no asset reference keeps the shader included.
- Raw runtime scan still reports `46` `Shader.Find` matches in `33` non-Editor files, so project-wide shader routing is not clean.

What was done:
- Added `pdaHologramMapShader` to `PlayerPDA` and forwarded it to runtime-created PDA spectrum/map tabs.
- Added `dataRecPulseShader` and serialized HUD dither/data pulse shader references on `Suit_HUD_Canvas.prefab`.
- Added `tracerShader` to `HarpoonLauncherTool` and serialized the tether line-strip shader on the held harpoon prefab.
- Restricted touched `Shader.Find` fallbacks to `UNITY_EDITOR || DEVELOPMENT_BUILD`.
- Wrote `Docs/Reports/UNITY_PLAYER_SHADER_REFERENCE_FIX_UNKNOWN_20260526.md` and `.json`.

Cinematic cheats used:
- None. This pass fixed asset ownership; it did not alter visual simulation.

Exact microseconds saved:
- `0` claimed. No profiler/player build was run.

Proof:
- Build: `Docs/Reports/BUILD_UNKNOWN_SHADER_REFERENCE_FIX_20260526.log` -> `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Static: touched `Shader.Find` calls are editor/development only.
- YAML: shader GUID refs added to `Player.prefab`, `Suit_HUD_Canvas.prefab`, and `Tool_HarpoonLauncher_Held.prefab`.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=694`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=694 sourceSyncPass=true`.

## 2026-05-26 - Unity Runtime Trap Deep Pass

What was wrong:
- Remaining runtime trap search surface still had release-reachable `Shader.Find` in clean owner-local material fallback paths.
- GPU readback traps needed classification from source, not keyword panic.

What was done:
- Searched first-party and Crest runtime script roots for `WaitForCompletion`, `GetData`, `SetData`, `ReadPixels`, `EncodeToPNG`, `Camera.main`, and scene object find APIs.
- Found `0` runtime `WaitForCompletion()` hits in searched roots.
- Classified first-party async `GetData<T>()` as legal where guarded by request completion or `SystemDispatcher.IsAsyncReadbackReadyNoWait`.
- Patched `Assets/_Project/Scripts/TetherManager.cs`: added `tetherRenderShader`, editor exact-path auto assignment, and editor/development-only name lookup.
- Patched `Assets/_Project/Scripts/UI/SubmarineSonarHoloMapRenderer.cs`: release route now requires serialized `sonarMapShader`; name lookup is editor/development only.
- Patched `Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs`: added `fallbackShader`, editor exact-path auto assignment, and editor/development-only name lookup.
- Patched `Assets/_Project/Scripts/Visor/HectonDryVolumeFeature.cs`: added editor exact-path auto assignment for the three dry-volume shaders and editor/development-only name lookup.
- Wrote `Docs/Reports/UNITY_RUNTIME_TRAP_DEEP_PASS_UNKNOWN_20260526.md/.json`.

Cinematic cheats used:
- None. This pass is release asset ownership and no-wait readback classification, not a visual simulation replacement.

Exact microseconds saved:
- `0` measured. No profiler/player build was run. Expected benefit is player-build shader correctness and reduced risk of CPU/GPU sync work, not proven frame-time savings.

Validation:
- Touched `Shader.Find` release reachability: `false` for six touched calls.
- Scoped `git diff --check`: pass, line-ending warnings only.
- Build: initially blocked by AGENTS guard at `2026-05-26 15:30:51`, CPU `99%`, active `dotnet` and `csc` processes. Later guard allowed launch; `Docs/Reports/BUILD_UNKNOWN_RUNTIME_TRAP_DEEP_PASS_20260526.log` has `Build succeeded.`, `0 Warning(s)`, `0 Error(s)` at lines `66-68`.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=695 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=695 sourceSyncPass=true`.

Residual:
- Dirty shader lookup surfaces owned by other agents remain, including `DroneFleetManager.cs` and `HectonVoxelEngine.cs`.
- Render-feature shader serialization still needs a separate renderer-data asset pass.
- Vendor Crest `SetData/GetData` remains third-party surface.

## 2026-05-26 - Unity Addressables Lifecycle Trap Pass

What was wrong:
- `ItemCatalog.TryGetLoadedWorldPrefab` checked handle validity before `WorldPrefabLoadState.Queued`.
- Queued world-prefab records intentionally have no Addressables handle until the dispatcher consumes the ticket.
- Failed/null-result Addressables loads marked the runtime record failed but did not release the failed handle.

What was done:
- Patched only clean `Assets/_Project/Scripts/ItemCatalog.cs`.
- Moved queued-state handling before handle validity checks.
- Persisted queued/loading access touch data before returning pending `false`.
- Added `FailWorldPrefabLoad` and `ReleaseFailedWorldPrefabHandle`.
- Routed tracked failed handles through `AssetLifecycleGovernor.ReleaseAddressableAsset`.
- Routed untracked failed handles through `TryReleaseExternalAddressableFault`.
- Kept direct `Addressables.Release` only as a governor-missing fallback.
- Wrote `Docs/Reports/UNITY_ADDRESSABLES_LIFECYCLE_TRAP_UNKNOWN_20260526.md/.json`.

Cinematic cheats used:
- None. This pass fixed asset lifecycle ownership; it did not change simulation or visual fidelity.

Exact microseconds saved:
- `0` claimed. No profiler/player proof was run.

Proof:
- Unity Addressables package is `2.7.6`.
- First-party runtime `Resources.*` scan: `0` hits in `Assets/_Project/Scripts`.
- First-party runtime `WaitForCompletion()` scan: `0` hits in `Assets/_Project/Scripts`.
- Vendor Crest `Resources.Load<ComputeShader>` hits remain third-party surface.
- Build: `Docs/Reports/BUILD_UNKNOWN_ADDRESSABLES_LIFECYCLE_TRAP_20260526.log` has `Build succeeded.`, `0 Warning(s)`, `0 Error(s)` at lines `66-68`.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=696 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=696 sourceSyncPass=true`.

## 2026-05-26 - Unity Awaitable Task Factory Trap Pass

What was wrong:
- `FloraGenomeVaultRuntime.BeginLoadGenomeBinaryAsync` used `Task.Factory.StartNew(... LongRunning ...)` for a Unity-facing cold binary load.
- The path allocated a `BinaryReadRequest` object and stored `Task<int>` as pending runtime state while a DataVault raw-byte buffer lock was held.
- Unity 6 documentation says `Awaitable` is the Unity async primitive, pooled and allocation-minimal, with explicit background/main-thread switches.

What was done:
- Re-read STRM async, zero-GC, native/jobs, execution phase, frame-budget, and registry mandates.
- Checked Unity 6 official `Awaitable`, async Awaitable manual, `BackgroundThreadAsync`, and `MainThreadAsync` docs.
- Checked Reddit/community discussions only as signal; no code decision was based on Reddit.
- Searched first-party scripts for `Task.Run`, `Task.Factory.StartNew`, `private Task`, and `System.Threading.Tasks`.
- Patched only clean `Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeVaultRuntime.cs` and `Assets/_Project/Scripts/Construction/BaseModuleCatalogRuntime.cs`.
- Removed `System.Threading`, `System.Threading.Tasks`, `BinaryReadRequest`, `_pendingBinaryRead`, `Task.Factory.StartNew`, `CancellationToken.None`, `TaskCreationOptions.LongRunning`, and `TaskScheduler.Default`.
- Added explicit pending/completed/byte-count fields and `RunGenomeBinaryLoadAsync`.
- The binary scan/copy now runs after `Awaitable.BackgroundThreadAsync`; completion state is written only after `Awaitable.MainThreadAsync`.
- Converted `BaseModuleCatalogRuntime.TryStartCatalogByteLoad` from `out Task<int>`/`Task.Run` to `out Awaitable<int>` with `ReadCatalogBytesIntoNativeArrayAsync`.
- Wrote `Docs/Reports/UNITY_AWAITABLE_TASK_FACTORY_TRAP_UNKNOWN_20260526.md/.json`.

Cinematic cheats used:
- None. This pass is async ownership cleanup, not a physical/visual simulation.

Exact microseconds saved:
- `0` claimed. No profiler/player proof was run.

Proof:
- Static: first-party runtime scan for `Task.Run`, `Task.Factory.StartNew`, `private Task`, and `System.Threading.Tasks` now returns editor/scanner/compiler hits only.
- Static: `FloraGenomeVaultRuntime.cs` no longer contains `Task`, `Task.Factory.StartNew`, `BinaryReadRequest`, or `System.Threading.Tasks`.
- Static: `BaseModuleCatalogRuntime.cs` no longer contains `Task.Run` or `System.Threading.Tasks`.
- Static: both patched runtime routes use `Awaitable.BackgroundThreadAsync` and `Awaitable.MainThreadAsync`.
- Scoped `git diff --check`: pass, line-ending warning only.
- Build: initially blocked by AGENTS guard. Earlier samples: CPU `100%` with `8` dotnet processes, then CPU `20%` with `7` dotnet processes.

## 2026-05-26 - Awaitable Build Recheck And Vegetation Threat Handle Wall

What was wrong:
- The Awaitable source cleanup itself removed the runtime `Task.Factory.StartNew` / `Task.Run` surface, but the first legal post-patch build exposed unrelated vegetation compile drift from active native-handle migration.
- `Docs/Reports/BUILD_UNKNOWN_AWAITABLE_VEGETATION_WALL_RECHECK_20260526.log` failed with `2` errors / `0` warnings: `currentFlowVolume` and `previousFlowVolume` were definitely-unassigned locals in `VegetationFlowFieldIntegrator.cs`.
- `Docs/Reports/BUILD_UNKNOWN_AWAITABLE_VEGETATION_WALL_RECHECK2_20260526.log` failed with `65` errors / `0` warnings: consumers still referenced removed `EcosystemThreat*CurrentNative` / `EcosystemThreat*NextNative` fields after `VegetationNativeMemory` moved threat storage to DataVault handles.

What was done:
- Fixed the `VegetationFlowFieldIntegrator` definite-assignment blockers by default-initializing the native views before DataVault acquisition.
- Fixed the remaining `VegetationThreatAndStructureService` stale threat consumers by reading DataVault threat grid / echo views instead of old `VegetationNativeMemory` native array fields.
- Verified old threat aliases no longer appear under `Assets/_Project/Scripts/World`.
- Updated `Docs/Reports/UNITY_AWAITABLE_TASK_FACTORY_TRAP_UNKNOWN_20260526.md/.json` from `build pending` to `compile reclosed`.
- Updated root/stable current compile references to `Docs/Reports/BUILD_UNKNOWN_AWAITABLE_THREAT_HANDLE_RECHECK_20260526.log`.

Cinematic cheats used:
- None. This pass is async ownership cleanup plus compile/ownership repair, not simulation or visual fakery.

Exact microseconds saved:
- Runtime: `0 us` claimed. No profiler/player proof was run.

Proof:
- Static Task scan: first-party runtime `Task.Run`, `Task.Factory.StartNew`, `private Task`, and `System.Threading.Tasks` hits are editor/scanner/compiler only.
- Static threat-alias scan: no old `EcosystemThreat*CurrentNative` / `NextNative` names remain under `Assets/_Project/Scripts/World`.
- Final build: `Docs/Reports/BUILD_UNKNOWN_AWAITABLE_THREAT_HANDLE_RECHECK_20260526.log` exits `0` and reports `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=697 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=697 sourceSyncPass=true`.
- Runtime proof still absent: no Unity import, Console, PlayMode, player build, profiler, GCMonitor, shader variant, scene wiring, visual, or platform gate was run.

## 2026-05-26 - Crest Native PNG Trap And Vegetation Pool Compile Wall

What was wrong:
- `HectonCrestOceanDepthCacheRuntimeBridge` used a development/editor forensic PNG route that converted async GPU readback data into a temporary `Texture2D`, called `Texture2D.EncodeToPNG()`, and wrote a managed `byte[]`.
- The first legal build after that patch exposed unrelated vegetation compile drift: stale direct `NativeChunkPool` field consumers remained after the pool moved to DataVault handles.

What was done:
- Checked official Unity docs for `ImageConversion`, `AsyncGPUReadbackRequest.GetData`, and `NativeArray<T>`.
- Replaced the Crest temporary `Texture2D` route with `ImageConversion.EncodeNativeArrayToPNG`.
- Wrote the encoded native PNG buffer through `FileStream.Write(ReadOnlySpan<byte>)`.
- Disposed the encoded `NativeArray<byte>` in `finally`.
- Repaired vegetation chunk-pool readers through `TryReadChunkPoolView`.
- Repaired vegetation chunk-pool writers through `TryAcquireChunkPoolWriteView` and `ReleaseChunkPoolWriteLocks`.
- Fixed `VegetationChunkResidencyDirector.WriteJobRecordsToPool` so element writes go through local `NativeArray<T>` handles acquired under the write lock.
- Wrote `Docs/Reports/UNITY_CREST_NATIVE_PNG_TRAP_UNKNOWN_20260526.md/.json`.
- Updated root/stable current compile references to `Docs/Reports/BUILD_UNKNOWN_CREST_NATIVE_PNG_POOL_RECHECK3_20260526.log`.

Cinematic cheats used:
- None. This pass removed diagnostic allocation churn and repaired native ownership compile drift; it did not change simulation or visual behavior.

Exact microseconds saved:
- Runtime: `0 us` claimed. No profiler/player proof was run.

Proof:
- Crest target file now has `ImageConversion.EncodeNativeArrayToPNG` and no `Texture2D.EncodeToPNG`.
- Scoped stale vegetation pool scan has no hits for `CountValidJobRecords` or direct `pool.Matrices` / `pool.Metadata` / `pool.SemanticTypes` routes in the repaired world files.
- Final build: `Docs/Reports/BUILD_UNKNOWN_CREST_NATIVE_PNG_POOL_RECHECK3_20260526.log` exits `0` and reports `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=698 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=698 sourceSyncPass=true`.
- Runtime proof still absent: no Unity import, Console, PlayMode, player build, profiler, GCMonitor, shader variant, scene wiring, visual, or platform gate was run.

## 2026-05-26 - Voxel Request Allocation Traps

What was wrong:
- `HectonVoxelStreamingBridge`, `WorldGenerativeGeologyVoxelBridgeDirector`, and `WorldCaveDirector` created a linked CTS for each heavy pending cave/voxel request.
- The linked layer listened only to an owner lifetime CTS, while each owner already cancels pending requests explicitly on stale request and shutdown paths.
- `WorldGenerativeGeologyVoxelBridgeDirector` also created a fallback `CavePresetLibrary.Create(Grotto)` per request when `voxelEngine.defaultPreset` was missing.
- `WorldGenerativeGeologyVoxelBridgeDirector.FlushQueuedLaunches` drained queued launches with `_queuedLaunchOrder.RemoveAt(0)`, which shifts the remaining `List<T>` contents on every front dequeue.

What was done:
- Checked Unity official Awaitable/destroy-token docs and Microsoft linked CTS docs.
- Patched only clean source files:
  - `Assets/_Project/Scripts/World/HectonVoxelStreamingBridge.cs`
  - `Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs`
  - `Assets/_Project/Scripts/WorldCaveDirector.cs`
- Replaced per-request `CreateLinkedTokenSource(lifetime.Token)` with direct per-request `new CancellationTokenSource()`.
- Kept one request CTS and cancellation token handoff to heavy generation.
- Cached one fallback grotto preset in `WorldGenerativeGeologyVoxelBridgeDirector` instead of creating a managed `CavePreset` per missing-defaultPreset request.
- Replaced queued-launch `RemoveAt(0)` with `_queuedLaunchHeadIndex` FIFO draining and stale-key skipping through `_queuedLaunchKeys`.
- Kept bounded `RemoveRange(0, _queuedLaunchHeadIndex)` only in queue compaction outside the per-launch dequeue loop.
- Documented dirty residual linked CTS hits in `GameBootstrapper.cs` and `PrologueSequenceRegistryBridge.cs` without touching them.
- Documented dirty residual fallback preset factory hit in `HectonVoxelEngine.cs` without touching it.
- Wrote `Docs/Reports/UNITY_VOXEL_CTS_UNLINK_UNKNOWN_20260526.md/.json`.
- Updated root/stable current compile references to `Docs/Reports/BUILD_UNKNOWN_VOXEL_QUEUE_DEQUEUE_RECHECK_20260526.log`.

Cinematic cheats used:
- None. This pass is lifetime/allocation hygiene, not simulation or visual fakery.

Exact microseconds saved:
- Runtime: `0 us` claimed. No profiler/player proof was run.

Proof:
- Scoped scan: no `CreateLinkedTokenSource(lifetime.Token)` remains in the three patched owner files.
- Residual scan: linked CTS remains only in dirty `GameBootstrapper.cs` and `PrologueSequenceRegistryBridge.cs`.
- Fallback preset scan: `WorldGenerativeGeologyVoxelBridgeDirector` now has one cached fallback; dirty `HectonVoxelEngine.cs` still has a separate residual factory call.
- Queue scan: no `RemoveAt(0)` remains in `WorldGenerativeGeologyVoxelBridgeDirector`; `_debugQueuedLaunches` and reconcile telemetry use `_queuedLaunchKeys.Count`.
- Build guard: final legal sample CPU `45.5%`, compiler count `0`.
- Final build: `Docs/Reports/BUILD_UNKNOWN_VOXEL_QUEUE_DEQUEUE_RECHECK_20260526.log` exits `0` and reports `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Post-queue documentation gates: `VerifyDocStructure.py pass=true activeDocCount=699 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=699 sourceSyncPass=true`.
- Runtime proof still absent: no Unity import, Console, PlayMode, player build, profiler, GCMonitor, shader variant, scene wiring, visual, or platform gate was run.

## 2026-05-26 - Front-Pop RemoveAt Cleanup

What was wrong:
- Runtime/script scan still had `RemoveAt(0)` in clean owner files after the geology queue patch.
- Microsoft documents `List<T>.RemoveAt` shifts following elements after removal, so front-pop removal is the wrong queue/LRU shape.

What was done:
- Patched `BeaconNetworkSystem` so oldest-beacon cap trimming despawns all excess first and then performs one `RemoveRange(0, excessCount)`.
- Patched `SaveThumbnailSystem` so thumbnail LRU order uses a fixed `string[12]` buffer and explicit count instead of a growable `List<string>` front pop.
- Patched `BioReactor` so fuel depletion counts front-depleted fuel items and removes them once with `RemoveRange(0, depletedCount)`.
- Patched `H8DataBaker` so CSV headers are not removed via front-shift; data rows are built directly after validation.
- Wrote `Docs/Reports/UNITY_FRONTPOP_REMOVEAT_CLEANUP_UNKNOWN_20260526.md/.json`.
- Updated root/stable current compile references to `Docs/Reports/BUILD_UNKNOWN_REMOVEAT_FRONTPOP_RECHECK_20260526.log`.

Cinematic cheats used:
- None. This pass is list/owner data-structure hygiene, not simulation or visual fakery.

Exact microseconds saved:
- Runtime: `0 us` claimed. No profiler/player proof was run.

Proof:
- Project scan for `RemoveAt(0)` now reports only `Assets/_Project/Scripts/Editor/LODStatisticsWindow.cs`.
- Scoped `git diff --check` passed for changed source files with line-ending warnings only.
- Build guard: attempts `1-29` blocked by high CPU and/or active compilers; launched at attempt `30` with CPU `48.6%`, compiler count `0`.
- Final build: `Docs/Reports/BUILD_UNKNOWN_REMOVEAT_FRONTPOP_RECHECK_20260526.log` exits `0` and reports `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=700 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=700 sourceSyncPass=true`.
- Runtime proof still absent: no Unity import, Console, PlayMode, player build, profiler, GCMonitor, scene wiring, visual, or platform gate was run.

## 2026-05-26 - Mesh And Component Cache Trap Pass

What was wrong:
- Clean runtime fallback mesh builders still used `mesh.vertices`, `mesh.uv`, and `mesh.triangles` property setters plus inline managed geometry arrays.
- `MapMagicRuntimeBridge` cached terrain tiles through `GetComponentsInChildren<TerrainTile>(true)`, which returns a new array.
- Project-wide residual mesh setter scans still include dirty files, so claiming global zero would be false.

What was done:
- Checked Unity 6 official docs for `Mesh.vertices`, `Mesh.SetVertices`, and `GameObject.GetComponentsInChildren(bool, List<T>)`.
- Patched clean fallback mesh construction in ambient biota, ArchitectEye diagnostics, AssetLifecycle fallback impostor, PDA spectrogram, wrist HUD, marine snow, visor AR stencil, and marauder outpost service.
- Patched `MapMagicRuntimeBridge` to use an owner `List<TerrainTile>` and Unity supplied-list discovery overload.
- Repaired unrelated dirty-file compile/warning walls exposed by the build in `WreckMaterialRegistry`, `AbyssalThermalManager`, `ProceduralWreckGenerator`, `VRSomaticProvider.Comfort`, `ScatterEvaluator`, and `DestructibleOrganicManager`.
- Wrote `Docs/Reports/UNITY_MESH_COMPONENT_CACHE_TRAP_UNKNOWN_20260526.md/.json`.
- Updated root/stable current compile references to `Docs/Reports/BUILD_UNKNOWN_MESH_COMPONENT_CACHE_TRAP_RECHECK3_20260526.log`.
- Left dirty residual files untouched and documented them.

Cinematic cheats used:
- None. This pass is Unity API/cache hygiene, not simulation or visual fakery.

Exact microseconds saved:
- Runtime: `0 us` claimed. No profiler/player proof was run.

Proof:
- Scoped mesh setter scan over touched files has `0` hits.
- `GetComponentsInChildren<TerrainTile>(true)` array-returning route is gone from `MapMagicRuntimeBridge`.
- `git diff --check` passed for touched source files with LF/CRLF warnings only.
- Build guard launched final build at attempt `9` with CPU `33.3%`, compiler count `0`.
- Final build: `Docs/Reports/BUILD_UNKNOWN_MESH_COMPONENT_CACHE_TRAP_RECHECK3_20260526.log` exits `0` and reports `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=700 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=700 sourceSyncPass=true`.
- Runtime proof still absent: no Unity import, Console, PlayMode, player build, profiler, GCMonitor, scene wiring, visual, or platform gate was run.

## 2026-05-26 - Render Feature Shader.Find Trap

What was wrong:
- Four clean URP render features still had release-reachable `Shader.Find` fallbacks after their serialized shader fields.
- Unity documents that `Shader.Find` can work in Editor but fail in player if a shader is not included through a tracked reference/build route.
- Hidden Always Included shader buckets were rejected as architecture; the owner asset must carry the dependency.

What was done:
- Patched `HectonSinglePassOceanFeature` depth foam shader route.
- Patched `HectonDeferredCausticsFeature` caustics shader route.
- Patched `HectonBiolumSSGIFeature` composite shader route and added editor asset loading.
- Patched `HectonSonarPointCloudFeature` overlay shader route and added editor asset loading.
- Wrote `Docs/Reports/UNITY_RENDER_FEATURE_SHADER_FIND_UNKNOWN_20260526.md/.json`.
- Wrote build guard block artifact `Docs/Reports/BUILD_UNKNOWN_RENDER_FEATURE_SHADER_FIND_RECHECK_20260526.log`.

Cinematic cheats used:
- None. This pass is player shader dependency hygiene, not simulation or visual fakery.

Exact microseconds saved:
- Runtime: `0 us` claimed. No profiler/player proof was run.

Proof:
- Scoped scan shows all four remaining `Shader.Find` calls under `UNITY_EDITOR || DEVELOPMENT_BUILD`; releaseReachable=false.
- Shader assets exist at `Assets/_Project/Art/Shaders/Hidden_Hecton_OceanDepthFoam.shader`, `Hecton_DeferredCaustics.shader`, `Hecton_BiolumSSGIComposite.shader`, and `SonarGridOverlay.shader`.
- `git diff --check` passed for four source files with LF/CRLF warnings only.
- Build recheck was guard-blocked after 60 attempts; latest clean compile remains pre-change `BUILD_UNKNOWN_MESH_COMPONENT_CACHE_TRAP_RECHECK3_20260526.log`.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=702 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=702 sourceSyncPass=true`.
- Runtime proof still absent: no Unity import, Console, PlayMode, player build, profiler, GCMonitor, shader variant, scene wiring, visual, or platform gate was run.

## 2026-05-26 - Runtime Shader Reference Catalog

What was wrong:
- Runtime-created materials still depended on release-reachable `Shader.Find` in clean first-party Core, World, VFX, Visor, UI, Construction, and resource-distribution owners.
- Unity documents that `Shader.Find` can succeed in Editor while a player build misses the shader if no tracked reference includes it.
- Always Included Shaders would hide dependency ownership in a global bucket.

What was done:
- Added `RuntimeShaderReferenceCatalog` under Core.
- Added `Assets/_Project/Resources/RuntimeShaderReferenceCatalog.asset` with 13 explicit shader references.
- Added `Hecton8/Runtime/FlatColor` as first-party ghost/fallback material shader.
- Patched 14 clean runtime/URP/VFX/UI source files to resolve catalog/serialized shader references first.
- Kept `Shader.Find` only under `UNITY_EDITOR || DEVELOPMENT_BUILD` in touched files.
- Left dirty runtime residual files untouched and listed them in the report.
- Wrote `Docs/Reports/UNITY_RUNTIME_SHADER_REFERENCE_CATALOG_UNKNOWN_20260526.md/.json`.
- Wrote build guard block artifacts `Docs/Reports/BUILD_UNKNOWN_RUNTIME_SHADER_REFERENCE_CATALOG_20260526.log` and `Docs/Reports/BUILD_UNKNOWN_RUNTIME_SHADER_REFERENCE_CATALOG_RECHECK2_20260526.log`.

Cinematic cheats used:
- None. This pass is player shader dependency hygiene, not simulation or visual fakery.

Exact microseconds saved:
- Runtime: `0 us` claimed. No profiler/player proof was run.

Proof:
- Scoped scan shows all 18 touched `Shader.Find` calls under `UNITY_EDITOR || DEVELOPMENT_BUILD`; releaseReachable=false.
- Catalog asset contains 13 shader GUID references and references the new flat-color shader.
- `git diff --check` passed for touched source/assets with LF/CRLF warnings only.
- Build recheck retry was guard-blocked after 60 attempts in `BUILD_UNKNOWN_RUNTIME_SHADER_REFERENCE_CATALOG_RECHECK2_20260526.log`; latest clean compile remains pre-change `BUILD_UNKNOWN_MESH_COMPONENT_CACHE_TRAP_RECHECK3_20260526.log`.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=687 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=687 sourceSyncPass=true`.
- Dirty runtime residuals remain in `DroneFleetManager`, `HectonVoxelEngine`, `AssetLifecycleGovernor`, `CarveDebrisComputeRenderer`, `MarauderOutpostGenerationService`, and `WreckMaterialRegistry`.
- Runtime proof still absent: no Unity import, Console, PlayMode, player build, profiler, GCMonitor, shader variant, scene wiring, visual, or platform gate was run.

## 2026-05-26 - Shader Mesh CTS Recheck

What was wrong:
- Two linked cancellation sources were redundant one-token wrappers.
- `RuntimeShaderReferenceCatalog.TryGet*` hid a `Resources.Load` and static mutation inside read accessors.
- Dirty runtime files still had release-reachable `Shader.Find` fallbacks.
- Cold fallback mesh builders still used legacy mesh property setters.

What was done:
- Removed redundant linked CTS routes in prologue and bootstrap state-machine entry.
- Kept the real scene activation linked CTS because it combines owner, destroy, and timeout cancellation.
- Moved catalog `Resources.Load` to cold `BeforeSceneLoad`; `TryGet*` methods now only read cached serialized references.
- Expanded catalog to `19` shader references and added `Hecton8/Runtime/CheckerboardUnlit`.
- Cleared first-party runtime release `Shader.Find` static scan to `0`.
- Cleared project-owned runtime mesh property setter static scan to `0`.
- Wrote `Docs/Reports/UNITY_SHADER_MESH_CTS_RECHECK_UNKNOWN_20260526.md/.json`.

Cinematic cheats used:
- None. This pass is dependency/API ownership hygiene, not simulation fakery.

Exact microseconds saved:
- Runtime: `0 us` claimed. No profiler/player proof was run.

Proof:
- `release_runtime_shader_find_hits=0`.
- Runtime mesh property setter hits: `0`.
- Remaining linked CTS: one justified scene activation owner/destroy/timeout route.
- Guarded build launched legally at attempt `19`, CPU `36.2%`, compiler count `0`.
- Build failed before C# compile with `62` missing generated `.csproj` MSB3202 errors from `Hecton8.slnx`; no current compile-green claim.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=691 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=691 sourceSyncPass=true`.
- Runtime proof still absent: no Unity import, Console, PlayMode, player build, profiler, GCMonitor, shader import, visual, or platform gate was run.

## 2026-05-26 - Unity Runtime Trap Deeper Pass

What was wrong:
- `OrganicDebrisProfile.RebuildCache` still had one Unity array-returning collider discovery route.
- `UnityApiTrapDetector` still allowed cold waivers for copied-array APIs.
- The detector inspected string/tooltip text enough to risk false positives and false clean edges.
- `RuntimeShaderReferenceCatalog.cs:45` still has runtime `Resources.Load`; this violates STRM.

What was done:
- Patched `DebrisManager.cs` so collider discovery uses owner `List<Collider>` scratch.
- Patched `UnityApiTrapDetector.cs` to hard-detect runtime `Resources.Load`.
- Removed cold-waiver bypass for `Renderer.sharedMaterials`, `Mesh.vertices`, and generic `GetComponents*<T>` arrays.
- Changed detector order to strip string literals before line comments.
- Wrote `Docs/Reports/UNITY_RUNTIME_TRAP_DEEPER_PASS_UNKNOWN_20260526.md/.json`.
- Updated reports index, status, rationale, and architecture actuality ledger.

Cinematic cheats used:
- None. This pass is API route/tooling hygiene, not visual simulation.

Exact microseconds saved:
- Runtime: `0 us` claimed. No profiler/player proof was run.

Proof:
- `GetComponentsInChildren<Collider>(true)` in `DebrisManager.cs`: `0` hits.
- Runtime `Resources.Load` outside Editor folders: `1` hit, `RuntimeShaderReferenceCatalog.cs:45`.
- Runtime mesh property setters: `0` hits.
- `RemoveAt(0)` scan: only `Assets/_Project/Scripts/Editor/LODStatisticsWindow.cs`.
- Exact release runtime `Shader.Find(...)` scan: `0` hits.
- `git diff --check` on touched source passed with LF/CRLF warnings only.
- Build guard blocked launch: CPU stayed above `50%` for `30` retry attempts plus `10` final attempts; compiler count `0`.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=682`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=682`.
- OOP scanner now skips files that vanish during inventory under concurrent deletes.

Residual:
- The shader catalog must move to Addressables or a proven serialized bootstrap owner route.
- No Unity import, Console, PlayMode, player build, profiler, GCMonitor, shader import, scene wiring, visual, or platform proof exists for this pass.

## 2026-05-26 - UNKNOWN Runtime Trap Deeper Pass - Doc Gate Race Follow-up

What was wrong:
- `OOP_Doc_Scanner.py` still crashed if a file vanished after inventory but before architecture checks or word-count reads.
- The concrete second race was `Docs/ARCHITECTURE/BASE_ATMOSPHERE_LOGISTICS_SHINOBU_221.md`.

What was done:
- Added `try_decode` and used it in secondary scanner reads.
- Split the remaining strict terrain paragraph without changing route facts.
- Updated the runtime trap report JSON/Markdown to the current active doc count.

Exact microseconds saved:
- Runtime: `0 us` claimed. Tooling/doc validation only.

Proof:
- `VerifyDocStructure.py pass=true activeDocCount=668`.
- `OOP_Doc_Scanner.py finalPass=true activeFileCount=668 sourceSyncPass=true`.

## 2026-05-26 - UNKNOWN Runtime Trap Deeper Pass - Build Boundary Follow-up

What was wrong:
- Earlier build attempts were guard-blocked, so the pass still lacked current compile proof.

What was done:
- Ran a guarded post-scanner build attempt when CPU was `48` and compiler process count was `0`.
- Wrote `Docs/Reports/BUILD_UNKNOWN_RUNTIME_TRAP_DEEPER_PASS_POST_SCANNER_RECHECK_20260526.log`.
- Updated the runtime trap report and architecture actuality ledger with the current boundary.

Exact microseconds saved:
- Runtime: `0 us` claimed. Build validation only.

Proof:
- `dotnet build Hecton8.slnx /m:1 /nr:false /p:UseSharedCompilation=false` launched legally.
- Exit code `1`.
- Failure: `62` `MSB3202` errors for missing Unity-generated `.csproj` files.
- Build did not reach C# compilation.

## 2026-05-26 - UNKNOWN Runtime Trap Deeper Pass - STRM Residual Recheck

What was wrong:
- The report still listed a runtime `Resources.Load` residual from an earlier source snapshot.

What was done:
- Reran the runtime `Resources.Load` scan.
- Verified `GameBootstrapper` owns catalog registration.
- Verified `00_BOOTSTRAP.unity:598` binds catalog GUID `66443d0a1f184aef87c6fd729fd8f401`.
- Updated the runtime trap report and architecture actuality ledger.

Exact microseconds saved:
- Runtime: `0 us` claimed. Static route reclassification only.

Proof:
- Runtime `Resources.Load` outside Editor folders: `0` hits.
- `RuntimeShaderReferenceCatalog.Register(runtimeShaderReferenceCatalog)` exists in `GameBootstrapper`.
- Catalog scene binding exists in `Assets/_Project/Scenes/00_BOOTSTRAP.unity`.

## 2026-05-26 - UNKNOWN Runtime Trap Deeper Pass - Final Doc Gate Reclose

What was wrong:
- `VerifyDocStructure.py` dropped to `pass=false` after concurrent edits removed UTF-8 BOM from two active marketing/data docs.

What was done:
- Restored UTF-8 BOM on `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md`.
- Restored UTF-8 BOM on `Docs/Marketing/Data/SOURCE_LEDGER.md`.
- Updated the runtime trap report counts.

Exact microseconds saved:
- Runtime: `0 us` claimed. Documentation encoding validation only.

Proof:
- `VerifyDocStructure.py pass=true activeDocCount=666 encodingWithoutUtf8Sig=0`.
- `OOP_Doc_Scanner.py finalPass=true activeFileCount=666 sourceSyncPass=true`.

## 2026-05-27 - UNKNOWN Runtime Alloc Route Pass

What was wrong:
- `WorldFidelityRoot` used `List<T>.ToArray()` for cold component cache rebuilds.
- `InteractionHighlighter` claimed zero-GC ownership but used `List<Renderer>.ToArray()` in auto-collect.
- `H8PrefabRegistryVramEstimator` used array `GetComponentsInChildren` and `Renderer.sharedMaterials`.

What was done:
- Reused owner arrays in `WorldFidelityRoot` and made `OnEnable` null-safe.
- Reused the renderer cache array in `InteractionHighlighter`.
- Switched `H8PrefabRegistryVramEstimator` to supplied-list renderer/material APIs.
- Wrote `Docs/Reports/UNITY_RUNTIME_ALLOC_ROUTE_PASS_UNKNOWN_20260527.md/.json`.

Cinematic cheats used:
- None. This was allocation-route hygiene, not simulation or visual approximation.

Exact microseconds saved:
- Runtime: `0 us` claimed. No profiler/player proof was run.

Proof:
- Touched-file trap scan for `.ToArray(`, `GetComponentsInChildren<Renderer>(true)`, and `.sharedMaterials`: `0` hits.
- First-party runtime scan snapshot: `Resources.Load=0`, release-reachable `Shader.Find=0`, object find APIs `0`.
- Residual `.ToArray()` text hits remain: `7`.
- Build guard initially blocked at CPU `51`; retry launched at attempt `4`, CPU `38`, compiler count `0`.
- Build result: exit `1`, `0` warnings, `62` `MSB3202` missing Unity-generated `.csproj` errors.
- Root `.csproj` count: `0`; C# compilation was not reached.
- Documentation validation: `VerifyDocStructure.py pass=true activeDocCount=667 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=667 sourceSyncPass=true`.
- Documentation validation: first OOP rerun failed on one overlong architecture-ledger line; after shortening it, `VerifyDocStructure.py pass=true activeDocCount=666` and `OOP_Doc_Scanner.py finalPass=true activeFileCount=666`.
- Follow-up: removed five additional clean `.ToArray()` residuals. Current residual scan has only dirty `WorldSliceAnchor.cs` and dirty `H8DataBaker.cs`.
- Post-follow-up build: legal launch at CPU `34`, compiler count `0`; exit `1`, `0` warnings, `62` `MSB3202` missing generated `.csproj` errors before C# compile.

## 2026-05-27 - UNKNOWN Native Ring Copy Pass

What was wrong:
- `GlobalTelemetryBus` copied telemetry snapshot events with per-element ring wrap/indexer loops.
- This was a plausible target for unsafe `CopyFromFast`, but the project already has a safer native-container copy primitive route.

What was done:
- Added `NativeRingBuffer.CopyRange(..., destinationStartIndex)`.
- Implemented ring range copy as one or two contiguous `NativeArray<T>.Copy` segments.
- Replaced telemetry snapshot copy loops in `GlobalTelemetryBus`.
- Added `NativeRingBufferEditTests` for wrapped chronological copy and destination-offset copy.
- Wrote `Docs/Reports/UNITY_NATIVE_RING_COPY_PASS_UNKNOWN_20260527.md/.json`.

Cinematic cheats used:
- None. This was black-box telemetry copy hygiene, not simulation or visual approximation.

Exact microseconds saved:
- Runtime: `0 us` claimed. No profiler/player proof was run.

Proof:
- Old telemetry snapshot loop residual scan: no `_snapshotBuffer[]`, `_ringBuffer[]`, `CapacityMask`, or copy-count loop hits in touched files.
- `NativeRingBuffer.cs`, `GlobalTelemetryBus.cs`, and `NativeRingBufferEditTests.cs` brace balance: `0`.
- Scoped `git diff --check`: passed with LF/CRLF warnings only.
- Build guard launched legally at CPU `43`, compiler count `0`.
- Build result: exit `1`, `0` warnings, `62` `MSB3202` missing Unity-generated `.csproj` errors.
- Root `.csproj` count: `0`; C# compilation was not reached.
## 2026-05-27 - UNKNOWN Late-Frame Registry Hot-Path Pass

What was wrong:

- `PhysicalPanelButton.LateFrameTick()` still used the `GlobalRegistry` late-frame unregister wrapper.
- The exact clean production hot-method scanner found this as the only actionable clean row after excluding dirty source, Editor folders, and `*.Editor.cs`.

What was done:

- `PhysicalPanelButton` now registers/unregisters its late-frame lane directly through `SystemDispatcher`.
- `GlobalRegistry.TryRegisterLateFrameTickable`, `GlobalRegistry.UnregisterLateFrameTickable`, and `GlobalRegistry.Dispatcher` are now `0` in the touched file.
- Exact clean hot-method forbidden-token scan returns `0` rows after the patch.

Cinematic cheats used:

- None. This is route/phase hygiene for a diegetic UI interaction path, not a visual simulation.

Exact microseconds saved:

- `0` claimed. No profiler or player capture was run.

Proof:

- `Docs/Reports/UNITY_LATEFRAME_REGISTRY_HOTPATH_PASS_UNKNOWN_20260527.md`
- `Docs/Reports/UNITY_LATEFRAME_REGISTRY_HOTPATH_PASS_UNKNOWN_20260527.json`
- `Docs/Reports/BUILD_UNKNOWN_LATEFRAME_REGISTRY_HOTPATH_RECHECK_20260527.log`
## 2026-05-27 - Read Accessor Purity Pass

What was wrong: three clean runtime APIs hid allocation or creation behind read-shaped names. `PerformanceBudgetController.GetBudgetStatus()` allocated a dictionary per call. `WorldShippingContentFilter.GetSuppressedHierarchyIds(..., createIfMissing:true)` mixed pure lookup and cache creation. `RTLProcessor.GetBuffer()` could lazily allocate thread-local staging storage.

What was done: `GetBudgetStatus()` now reuses fixed-capacity owner snapshot storage and keeps `CopyBudgetStatusNonAlloc()` as the preferred retained/hot path. `WorldShippingContentFilter` now separates pure `TryGetSuppressedHierarchyIds()` from explicit `EnsureSuppressedHierarchyIds()` creation. `RTLProcessor` now uses `EnsureBuffer()` for the lazy staging route.

Cinematic Cheats used: none; this was runtime architecture hygiene, not simulation or presentation fakery.

Exact microseconds saved: `0` claimed. No profiler/player proof. Static proof: touched accessor scanner `0`, touched exact hot scanner `0`, brace balance `0`, guarded build launched and hit missing generated `.csproj` boundary before C# compile.

## 2026-05-27 - Unity Signal Contract Pass

Wrong: `SignalBusContractAuditCli` found two confirmed runtime contract errors: `Gameplay.ToolDepletedSignal` and `Tools.ToolDepletedSignal` shared the same signal name while carrying different payloads. It also found one plugin asmdef contract warning: `Hecton8.Plugins` used signal contracts without direct `Hecton8.Core.Contracts` reference.

Done: renamed the local gameplay event to `PlayerToolDepletedSignal`, updated all source producers/listeners, kept `Tools.ToolDepletedSignal : ISignal` unchanged, and added `Hecton8.Core.Contracts` to `Hecton8.Plugins.asmdef`.

Cinematic cheats used: none. This is route/contract hygiene, not visual simulation.

Exact microseconds saved: `0` claimed. Final contract scan proof: `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_FINALSCAN.json` reports `errors=0`, `confirmedErrors=0`, `asmdefContractBoundaryHits=0`. Build recheck launched legally and failed before C# compile with `62` missing Unity-generated `.csproj` errors. Documentation gates are closed: `VerifyDocStructure.py pass=true`, `OOP_Doc_Scanner.py finalPass=true`.

## 2026-05-27 - Unity Signal Queue Diagnostics Pass

Wrong: the previous contract scan left `8` `POSSIBLE_ORPHANED_SIGNAL_QUEUE` warnings, but source proof showed registered NativeQueue lanes and one declared-only wrapper. The scanner was misclassifying queue helper ownership. The CLI also targeted `net8.0` on a machine with only `.NET 10.0.6`, so a normal build produced a non-runnable artifact. `RuntimeDiagnosticsTrace.FlushSuppressedDuplicates()` also allocated a list on flush.

Done: fixed `SignalBusContractAuditCli` queue ownership detection, added declared-only queue classification, retargeted the CLI to `net10.0`, and removed the diagnostics flush list allocation plus heuristic `foreach` token.

Cinematic cheats used: none. This was evidence-tool and diagnostics-path hygiene.

Exact microseconds saved: `0` claimed. Proof: tool restore/build succeeded under guard with `0` warnings/errors; `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_QUEUE_RECHECK.json` reports `errors=0`, `confirmedErrors=0`, `warnings=521`, `POSSIBLE_ORPHANED_SIGNAL_QUEUE=0`. Full solution build was guard-blocked by CPU above `50%` for `30` attempts.

## 2026-05-27 - Unity Signal Audit Classifier And Seam MPB Pass

What was wrong: the audit mixed real runtime debt with false-positive or lower-severity classes: constructors named `TickList`, `#if UNITY_EDITOR` methods, cached MPB writes, ComputeShader dispatch parameters, and owner-local blackbox rings.

What was done: `SignalBusContractAuditCli` now separates constructor/editor-only/MPB/GPU/owner-local telemetry cases. `SeamGapDitherRenderer` no longer mutates draw material parameters before indirect draw; it uses a cached `MaterialPropertyBlock`.

Cinematic cheats used: none. This was audit precision and render-state ownership hygiene.

Exact microseconds saved: `0` claimed. Static proof: latest verified full scan before the final cold-allocation classifier patch reports `errors=0`, `confirmedErrors=0`, `warnings=365`, `infos=553`; seam dither moved from SRP material warnings to MPB info. Final cold-allocation classifier rebuild is guard-blocked by an external `dotnet build Hecton8.slnx`. Documentation gates are closed: `VerifyDocStructure pass=true`, `OOP_Doc_Scanner finalPass=true`.

## 2026-05-27 - Unity Signal Audit Classifier And Contract Rename Recheck

What was wrong:

- The SignalBus contract scanner still had method/layout classifier blind spots: multiline method declarations and fully-qualified `StructLayout` attributes.
- Clean local payloads reused global-looking names for unrelated owners: deterministic input mocks, core fluid telemetry aliases, acoustic occlusion telemetry, depth-stress synth mocks, deferred eclipse queue payload, and UI base-integrity event payload.
- Broad fluid DTO and combat mock duplicate families remain real review debt, but several owner files are dirty or cross-domain.

What was done:

- `SignalBusContractAuditCli` now tracks pending multiline method declarations and recognizes fully-qualified `StructLayout`.
- Renamed clean local payloads: `Deterministic*`, `CoreFluid*Telemetry*`, `AcousticOcclusionTelemetryEntry`, `DepthStressMockPressureSignal`, `DepthStressMockTensionSignal`, `DeferredEclipseGameplayEventPayload`, and `UiBaseIntegrityEventPayload`.
- Updated direct call sites and layout validator string for the UI payload rename.

Cinematic cheats used:

- None. This was architecture/tooling contract hygiene, not simulation or visual approximation.

Exact microseconds saved:

- Runtime: `0 us` claimed. No profiler/player proof was run.

Proof:

- CLI build: `BUILD_UNKNOWN_SIGNAL_CLI_CONTRACT_RENAME_RECHECK_20260527.log`, exit `0`.
- Full audit: `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_CONTRACT_RENAME_RECHECK.json`, `files=2441`, `shaders=71`, `errors=0`, `confirmedErrors=0`, `warnings=266`, `infos=712`.
- Duplicate-name warnings dropped from `48` to `30`; `ZERO_GC_HOT_PATH_ALLOCATION_REVIEW=0`.
- Full solution build was not launched: `BUILD_UNKNOWN_CONTRACT_RENAME_RECHECK_20260527.log` shows `12` guard-blocked attempts with CPU `100` and compiler process count `8-9`.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=680 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=680 sourceSyncPass=true`.

## 2026-05-27 - Unity Signal Audit Safe Rename And Classifier Pass

What was wrong:

- Duplicate signal-like names still mixed unrelated local telemetry/mock owners after the previous pass.
- The contract audit over-warned `IJob`/`IJobParallelFor` telemetry carriers and `Execute()` carrier structs as missing binary payload layout.
- Pending multiline method opening braces were double-counted, so some black-box dump file I/O inherited the previous method name and stayed in runtime I/O warnings.
- `AcousticSensoryTelemetrySnapshot` was a public telemetry readout DTO without explicit layout.

What was done:

- Renamed clean owner-contained carriers: crash telemetry, H8 binary pager telemetry, Save Merkle telemetry write job, harpoon/cable tether telemetry jobs, physiology/biolum/hull mock payloads.
- Added job-struct and executable-carrier classification to `SignalBusContractAuditCli`.
- Fixed pending multiline method brace accounting.
- Added explicit 64-byte layout and field offsets to `AcousticSensoryTelemetrySnapshot`.
- Left dirty or authoritative routes untouched: ocean/atmosphere/audio/UI/graphics/acoustic/quality duplicate families remain for owner agents.

Cinematic cheats used:

- None. This was contract/tooling/layout hygiene, not simulation or visual approximation.

Exact microseconds saved:

- Runtime: `0 us` claimed. No profiler/player proof was run.

Proof:

- CLI build: `dotnet build Tools/SignalBusContractAuditCli/SignalBusContractAuditCli.csproj --disable-build-servers -p:UseSharedCompilation=false -nologo -clp:ErrorsOnly`, exit `0`, `0` warnings, `0` errors.
- Full audit: `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_EXEC_CARRIER_RECHECK.json`, `files=2441`, `shaders=71`, `errors=0`, `confirmedErrors=0`, `warnings=171`, `infos=826`.
- Duplicate-name warnings dropped from `30` to `14`; `SIGNAL_LAYOUT_REVIEW` dropped to `2`; `JOB_STRUCT_LAYOUT_REVIEW=69 info`; `EXECUTABLE_STRUCT_LAYOUT_REVIEW=2 info`.
- Full solution build: `BUILD_UNKNOWN_EXEC_CARRIER_RECHECK_20260527.log`, exit `1`, `83` warnings, `3` errors: Unity-generated editor project circular dependency plus missing `Temp/CodexBuild/Unity.ShaderGraph.Editor.dll`; not a green source compile proof.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=684 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=684 sourceSyncPass=true`.

## 2026-05-27 - Unity Signal Telemetry Ownership Recheck

What was wrong:

- One clean UI path still resolved `TMP_FontAsset.material` inside `FontStreamingManager.ProcessSwapBatch()`.
- The SignalBus contract audit still could not prove several valid NativeArray ownership routes: GlobalDataVault generation aliases and helper-allocated registered telemetry rings.
- The first fresh recheck proved two more scanner blind spots: bare `AllocateArray<T>()` helpers and `ref _fieldHandle` Vault aliases.

What was done:

- Cached the target TMP material in `BeginSwapQueue()` and reused it during `ProcessSwapBatch()`.
- Extended `SignalBusContractAuditCli` ownership detection for `VaultGenerationHandle` aliases.
- Extended helper-route detection for `Allocate*Array`, `RegisterNativeArray`, `UnregisterNativeArray`, and `Dispose*Array(ref NativeArray<T>)`.
- Rebuilt the CLI twice after classifier changes and reran full SignalBus audits.

Cinematic cheats used:

- None. This is UI hot-route hygiene and audit ownership precision, not simulation.

Exact microseconds saved:

- Runtime: `0 us` claimed. No profiler/player proof has run.

Proof:

- Static source proof: `FontStreamingManager` no longer contains `Material targetMaterial` or `_targetFont.material` in `ProcessSwapBatch()`.
- CLI build proof: `BUILD_UNKNOWN_SIGNAL_CLI_TELEMETRY_OWNERSHIP_RECHECK_20260527.log` and `BUILD_UNKNOWN_SIGNAL_CLI_TELEMETRY_OWNERSHIP_RECHECK2_20260527.log` both exit `0` with `0` warnings and `0` errors.
- Audit proof: `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_TELEMETRY_OWNERSHIP_RECHECK2.json` reports `files=2441`, `shaders=71`, `errors=0`, `confirmedErrors=0`, `warnings=166`, `infos=832`.
- Warning movement: total warnings `171 -> 166`; SRP material warnings `73 -> 72`; declared-only telemetry rings `5 -> 1`.
- Ownership proof: `CombatDamageRuntime._telemetryRing` is now `LOCAL_NATIVE_TELEMETRY_RING_OWNER_LOCAL`; `LocRegistry._telemetryFrames` is now `LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS`.
- Remaining declared-only telemetry warning: dirty `Power/WfcOutpostPowerBootRuntime._blackBox`.
- Full solution build boundary: `BUILD_UNKNOWN_TELEMETRY_OWNERSHIP_FULL_SOLUTION_RECHECK_20260527.log` has `350` error lines, `0` warning lines, and no errors in touched source files. The command hit the `604s` tool timeout and the build process later ended; this is not a green compile proof. Main failing project buckets: `Hecton8.Core.csproj=132`, `MeshBakerCore.csproj=70`, `Assembly-CSharp-Editor.csproj=69`, `MapMagic.csproj=21`, `AstarPathfindingProject.csproj=21`.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=688 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=688 sourceSyncPass=true wordReductionPercent=34.132669128506585`.

## 2026-05-27 20:52 +04:00 - Signal Layout/Alias Classifier Pass

What was wrong:
- `SignalBusContractAuditCli` resolved cache-line-critical payload sizes only from the caller file, so it flagged already-correct 64-byte `ProgressionEventSignal` and `VocalCueSignal`.
- Expression-bodied `ResolveBuffer(in VaultGenerationHandle<T>)` NativeArray accessors could be counted as local persistent owners.
- Private-ref aliases to nested native buffer sets were not modeled, producing either blind spots or false unowned telemetry errors.
- `AcousticPanicCommand` and `VocalWarningTelemetrySnapshot` lacked explicit layout.

What was done:
- Added a global struct-layout index to the scanner.
- Tightened telemetry NativeArray matching and alias ownership classification.
- Classified `GlobalTelemetryBus._snapshotBuffer` as owner-local export staging, not blackbox authority.
- Added explicit layout to the two signal-like DTOs.

Proof:
- `Docs/Reports/BUILD_UNKNOWN_SIGNAL_CLI_LAYOUT_ALIAS_RECHECK10_20260527.log`: `0 Warning(s)`, `0 Error(s)` before final borrowed-view refinement.
- `Docs/Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_LAYOUT_ALIAS_RECHECK2.json`: `errors=0`, `confirmedErrors=0`, `warnings=245`, `SIGNAL_LAYOUT_REVIEW=0`, `CACHELINE_CRITICAL_SIGNAL_STRIDE_DEBT=1`.
- `Docs/Reports/UNITY_SIGNAL_LAYOUT_ALIAS_CLASSIFIER_UNKNOWN_20260527.md/.json` written.

Residual:
- Final borrowed-view classifier refinement is not build/audit proven yet. CPU guard stayed >50% for repeated checks; `dotnet build` was not launched after that last patch.
- Remaining real debt from verified audit: `TetherTensionSignal` 192-byte cache-line-critical lane, `ModEventProjectionBridge._cullTelemetry` non-Vault blackbox without proven dump route, and two graphics/culling telemetry buffers outside core ownership.

Cinematic cheats used: none.
Exact microseconds saved: `0`; no profiler/player run.

Follow-up proof after source-only refinement:
- Scoped `git diff --check` on touched source/docs passed with line-ending warnings only.
- Runtime touched-file brace balance: `FaunaDirector.cs=0`, `VocalWarningSystem.cs=0`.
- CPU recheck after wait sampled `100%`; final CLI build/audit remains blocked by build guard, not declared complete.

Final closure:
- CPU later sampled `42%`, compiler count `0`.
- `BUILD_UNKNOWN_SIGNAL_CLI_LAYOUT_ALIAS_FINAL_20260527.log`: `UnknownCheck` build failed with `CS2012` access denied on existing `obj`; not a source compile error.
- `BUILD_UNKNOWN_SIGNAL_CLI_LAYOUT_ALIAS_FINAL2_20260527.log`: `UnknownFinal` build succeeded with `0` warnings and `0` errors.
- `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_LAYOUT_ALIAS_FINAL.json`: `errors=0`, `confirmedErrors=0`, `warnings=155`, `infos=1171`.
- Important movement: `warnings 245 -> 155`, `LOCAL_NATIVE_TELEMETRY_RING_DECLARED_ONLY 1 -> 0`, `SIGNAL_LAYOUT_REVIEW 0`, `CACHELINE_CRITICAL_SIGNAL_STRIDE_DEBT 1`.

Build and docs closure:
- `VerifyDocStructure.py`: `pass=true`, `activeDocCount=692`, `encodingWithoutUtf8Sig=0`.
- `OOP_Doc_Scanner.py`: `finalPass=true`, `activeFileCount=692`, `sourceSyncPass=true`, `wordReductionPercent=31.353138780085825`.
- `BUILD_UNKNOWN_SIGNAL_LAYOUT_ALIAS_FULL_SOLUTION_20260527.log`: first full build failed before C# compile on root `obj` access denied.
- `BUILD_UNKNOWN_SIGNAL_LAYOUT_ALIAS_FULL_SOLUTION_ESCALATED_20260527.log`: escalated full build failed with `3141` warnings and `365` errors.
- Touched-file build hits: `0` for `SignalBusContractAuditCli`, `FaunaDirector.cs`, and `VocalWarningSystem.cs`.
- Top error buckets: `Hecton8.Core.csproj=133`, `MeshBakerCore.csproj=70`, `Assembly-CSharp-Editor.csproj=69`, `MapMagic.csproj=21`, `AstarPathfindingProject.csproj=21`.

## 2026-05-27 - Signal Residual Contract Cleanup

What was wrong:
- `PersistentWorldRegistry.RunSectorOverrideCommitAsync()` still allocated via `List<T>.ToArray()`.
- `TetherTensionSignal` was incorrectly configured as cache-line-critical while its payload is 192 bytes.
- Clean local mock carriers duplicated signal-like names across sandbox/UI/TBDR and unrelated contracts.

What was done:
- Replaced the sector override commit list snapshot with a fixed `SectorOverrideCommitWork[16]` buffer.
- Changed `TetherTensionSignal` to normal `SignalBus<T>.Configure(...)` with preserved capacity.
- Renamed local carriers: `SandboxMockAcousticSignal`, `GlitchMockDepthSignal`, `TBDRMockQualityWeightSignal`.

Cinematic cheats used:
- None. This was SignalBus/DTO contract cleanup, not simulation.

Exact microseconds saved:
- Runtime: `0 us` claimed. No profiler/player proof has run.

Proof:
- Final static audit: `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_TBDR_UI_SANDBOX_TETHER_SECTOR_RECHECK.json`.
- Audit result: `errors=0`, `confirmedErrors=0`, `warnings=148`, `infos=1169`.
- Closed counts: `CACHELINE_CRITICAL_SIGNAL_STRIDE_DEBT=0`, `ZERO_GC_HOT_PATH_ENUMERATION_REVIEW=0`.
- Duplicate signal-like warnings reduced: `14 -> 8`.
- Full project compile errors were not fixed or chased by explicit user instruction.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=693 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=693 sourceSyncPass=true wordReductionPercent=31.34291167072261`.

## 2026-05-27 - Core Memory Signal Domain Deep Pass

What was wrong:
- `ModEventProjectionBridge._cullTelemetry` was still a local persistent telemetry ring in the production route.
- TBDR runtime and vertex-budget DataVault buffers opened generation handles but did not release them on dispose.
- `SignalBusContractAuditCli` did not recognize `NativeMemoryBridgeLifetime` as a bounded owner-local telemetry lifetime.

What was done:
- Added `BufferID.ShinobuModProjectionCullTelemetryRing = 70921`.
- Moved mod projection cull telemetry to `GlobalDataVault` generation handle ownership, with sentinel local fallback only when the Vault is unavailable.
- Added explicit `ReleaseBuffer` paths for TBDR runtime and vertex-budget Vault handles.
- Patched the SignalBus audit classifier for `NativeMemoryBridgeLifetime`.

Cinematic cheats used:
- None. This was ownership/lifetime/tooling work, not simulation.

Exact microseconds saved:
- Runtime: `0 us` claimed. No profiler/player proof has run.

Proof:
- Before audit: `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_DEEP_DOMAIN_RECHECK.json`, `errors=0`, `confirmedErrors=0`, `warnings=148`, `infos=1169`.
- After source changes with existing audit binary: `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_DEEP_DOMAIN_RECHECK_PREBUILD.json`, `errors=0`, `confirmedErrors=0`, `warnings=146`, `infos=1171`.
- `LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT` moved `3 -> 1`; `LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS` moved `3 -> 5`.
- Final CLI build: `BUILD_UNKNOWN_SIGNAL_CLI_DEEP_DOMAIN_RECHECK_20260527.log`, `0` warnings, `0` errors.
- Final audit: `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_DEEP_DOMAIN_RECHECK_FINAL.json`, `errors=0`, `confirmedErrors=0`, `warnings=145`, `infos=1172`.
- Final ownership movement: `LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT 3 -> 0`, `LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS 3 -> 5`, `LOCAL_NATIVE_TELEMETRY_RING_OWNER_LOCAL 7 -> 8`.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=694 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=694 sourceSyncPass=true wordReductionPercent=31.331236932292995`.
- Full project compile errors were intentionally not fixed by user instruction.

## 2026-05-27 - Global Route Stability Pass

What was wrong:
- `ConnectionSplineBatchRenderer.LateFrameTick()` unregistered from `GlobalRegistry` inside the dispatcher late-frame route.
- `HectonAPI.Input.GetButtonMask()` directly read `GlobalRegistry.Input` from a public mod getter.
- Legacy `ModCommandDispatcher` flow/acoustic execution directly read `GlobalRegistry.AbyssalFlowGpu` and `GlobalRegistry.Audio`.

What was done:
- Replaced late-frame self-unregister with a dormant registered state.
- Added cold/hot-swap cache for `HectonAPI` input service.
- Added cold/hot-swap cache for legacy mod command flow/audio dependencies.

Cinematic cheats used:
- None. This was global-route stability work, not simulation.

Exact microseconds saved:
- Runtime: `0 us` claimed. No profiler/player proof has run.

Proof:
- Scoped hot scanner now reports only two intentional residuals:
  `SignalBusRuntime.FlushPostSimulation` fault kill-switch and
  `SignalCorridorRuntime.FlushPostSimulation` legacy bridge.
- Full static audit: `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_GLOBAL_ROUTE_RECHECK.json`.
- Audit result: `scannedFiles=2442`, `shaderFilesScanned=71`, `errors=0`,
  `confirmedErrors=0`, `warnings=145`, `infos=1172`.
- `git diff --check` on touched files passed with line-ending warnings only.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=695 encodingWithoutUtf8Sig=0`;
  `OOP_Doc_Scanner.py finalPass=true activeFileCount=695 sourceSyncPass=true`.
- Full project compile errors were intentionally not fixed by user instruction.

## 2026-05-27 - Global Route Cache Pass

What was wrong:
- `ConnectionSplineBatchRenderer` still checked dispatcher registry state from live link update helpers.
- `ThreadSafeCommandQueue` could read `GlobalRegistry.ObjectPoolService` during late-frame command drain.
- `SystemDispatcher` read `GlobalRegistry.Physics` during late-frame environment artery count and flush.
- `ModWorldPersistenceManager` read object-pool registry state during mod spawn, despawn, and restore.
- `SceneRuntimeService` used registry wrappers and live registry reads in scene transition presentation routes.

What was done:
- Added cached dispatcher availability and direct `SystemDispatcher` lane registration for connection spline and scene runtime owners.
- Bound `ThreadSafeCommandQueue` object-pool cache from `SystemDispatcher` cold refresh and object-pool hot-swap.
- Added cached `IPhysicsService` route for SystemDispatcher late-frame physics pending-count and flush.
- Added cached object-pool dependency for mod world persistence spawn/despawn/restore.
- Cached scene transition terminal handles, audio bridge, tick dispatcher, and camera-juice service.

Cinematic cheats used:
- None. This was dependency route and dispatcher stability work, not simulation.

Exact microseconds saved:
- Runtime: `0 us` claimed. No profiler/player proof has run.

Proof:
- `git diff --check` passed for touched source files.
- Brace delta is `0` for all touched source files.
- Static audit: `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_GLOBAL_ROUTE_CACHE_RECHECK.json`.
- Audit result: `files=2442`, `shaders=71`, `errors=0`, `confirmedErrors=0`, `warnings=145`, `infos=1172`.
- Full project compile errors were intentionally not fixed by user instruction.

## 2026-05-27 - Mod Registry Cache Pass

What was wrong:
- `ModSettingsRegistry` read `GlobalRegistry.UserOptions` from mod setting register/apply routes.
- `ModItemRegistry.ResolveActiveCatalog()` and `ModBuildableRegistry.ResolveActiveCatalog()` read inventory/logistics services from registry.
- `FutureCommandSandboxValidator.OpenVaultLane()` and rollback checks fell back to `GlobalRegistry.DataVault`.
- `ModMenuSettingSliderView` caused options disk save and registry refresh on every slider value event.

What was done:
- Cached `UserOptionsPersistence`, `IPlayerInventoryService`, `ILogisticsService`, and `IDataVault` through cold binding and hot-swap refresh.
- Stored mod setting storage keys on entries so apply routes do not rebuild them.
- Removed the `OpenVaultLane()` registry fallback.
- Batched slider persistence to commit events while keeping live mod callbacks.
- Added `UNITY_MOD_REGISTRY_CACHE_PASS_UNKNOWN_20260527.md/.json`.

Cinematic cheats used:
- None. This was global-route and mod sandbox ownership work, not simulation.

Exact microseconds saved:
- Runtime: `0 us` claimed. No profiler/player proof has run.

Proof:
- `git diff --check` passed on touched files with LF/CRLF warnings only.
- Brace delta is `0` for all touched source files.
- Targeted scan finds direct `GlobalRegistry.UserOptions/PlayerInventory/Logistics/DataVault` reads only in cold bind/install routes in touched files.
- Static audit: `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_MOD_REGISTRY_CACHE_RECHECK.json`.
- Audit result: `files=2443`, `shaders=71`, `errors=0`, `confirmedErrors=0`, `warnings=145`, `infos=1172`.
- Touched-file audit findings are info-only: declared/registered local queues, Vault alias, cold/fatal dump I/O, manifest file I/O.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=697 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=697 sourceSyncPass=true`.
- Full project compile errors were intentionally not fixed by user instruction.

## 2026-05-27 - Vault Rebind Release Pass

What was wrong:
- `FutureCommandSandboxValidator` cleared DataVault lane descriptors without releasing the underlying Vault handles on shutdown/rebind.
- `ModEventProjectionBridge` did not rebind cull telemetry storage when the DataVault service was replaced.

What was done:
- Added explicit release coverage for all `20/20` sandbox `VaultLane<T>` handles.
- Added projected mod cull telemetry release/reopen flow for DataVault hot-swap and fallback storage.
- Completed any scheduled projection job before changing the cull telemetry storage alias.

Cinematic cheats used:
- None. This is native lifecycle and global authority cleanup, not simulation/presentation fakery.

Exact microseconds saved:
- Runtime: `0 us` claimed. No profiler/player proof has run.

Proof:
- `git diff --check` passed on touched source with LF/CRLF warnings only.
- Brace delta is `0` for `FutureCommandSandboxValidator.cs` and `ModEventProjectionBridge.cs`.
- Static audit: `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_VAULT_REBIND_RELEASE_RECHECK.json`.
- Audit result: `files=2443`, `shaders=71`, `errors=0`, `confirmedErrors=0`, `warnings=145`, `infos=1172`.
- Full project compile errors were intentionally not fixed by user instruction.

## 2026-05-28 - Workstation Orphan Process Sweep

What was wrong:
- `C:\hades\dental-crm` had orphan-root dev-server process trees holding local ports `4100` and `5173`.
- `Tools\HectonPhiStaticAudit.py --no-atlas --no-fail` was running with a missing parent process.
- No non-responsive GUI windows were found.
- CPU load remained high after cleanup because a live `dotnet build Hecton8.slnx` task was still active under Codex-owned PowerShell; this was not killed because it was not orphaned or hung.

What was done:
- Terminated orphan tree rooted at PID `13384`: `npm run dev`, `concurrently`, API watch, Vite `5173`, `tsx`, and `esbuild` children.
- Terminated orphan audit PID `39656`.
- Detected a second orphan `dental-crm` dev-server tree rooted at PID `2892`, then terminated its API, web, Vite, `tsx`, and `esbuild` children.
- Verified ports `4100` and `5173` were closed after the second sweep; only unrelated Python listeners on `8000` and `8080` remained.
- Surveyed storage junk candidates without deleting data.

Cinematic cheats used:
- None. This was workstation process and disk hygiene, not runtime simulation.

Exact microseconds saved:
- Runtime: `0 us` claimed. No profiler/player proof has run.
- Workstation frame/build pressure reduced by removing stale Node/Vite/tsx trees; no deterministic project runtime gain claimed.

Junk inventory:
- `C:\hades\Hecton8\Library`: `3.87 GB`, Unity-generated cache; `2.96 GB` older than 7 days.
- `C:\Users\danat\AppData\Local\Temp`: `2.58 GB`; top large files are Visual Studio installer/runtime artifacts.
- `C:\hades\.tmp`: `0.96 GB`, old Edge debug/profile artifacts.
- `C:\$Recycle.Bin`: `0.97 GB`.
- `C:\Users\danat\AppData\Local\npm-cache`: `0.45 GB`.
- `C:\Users\danat\AppData\Local\NVIDIA\DXCache`: `0.25 GB`.
- `C:\Users\danat\AppData\Local\Unity\cache`: `0.16 GB`.
- `C:\Users\danat\AppData\Local\CrashDumps`: `0.12 GB`.
- `C:\hades\Hecton8\Logs`: `0.13 GB`.

Proof:
- Final orphan list contained only protected/session processes: `csrss.exe`, `winlogon.exe`, `explorer.exe`, `WindowsTerminal.exe`, `Notepad.exe`.
- Final hung-window count: `0`.
- Final watched dev ports: `4100` closed, `5173` closed, unrelated `8000` and `8080` still listening.
- Active non-orphan load left running: `dotnet build Hecton8.slnx`; no `csc.exe` found in the last build-process scan.

## 2026-05-28 - Workstation Junk Cleanup

What was wrong:
- Disk junk remained after the process sweep: user temp, `C:\hades\.tmp`, recycle bin, crash dumps, NVIDIA cache, Unity cache, and Hecton project `Temp/Obj`.
- User explicitly excluded `C:\Users\danat\AppData\Local\npm-cache`, `C:\hades\Hecton8\Library`, and `C:\hades\Hecton8\Logs`.
- During the first cleanup pass, `dotnet build Hecton8.Core.csproj` was active, so `Hecton8\Temp` and `Hecton8\Obj` were deferred until the build ended.
- `C:\hades\dental-crm` dev-server respawned again as orphan-root PID `5576` after the earlier sweep.

What was done:
- Cleaned old/unlocked user temp files, `C:\hades\.tmp`, recycle bin, crash dumps, NVIDIA shader cache, Unity user cache, then cleaned `Hecton8\Temp` and `Hecton8\Obj` after the active Hecton build ended.
- Preserved the three excluded paths exactly: `npm-cache`, `Hecton8\Library`, and `Hecton8\Logs`.
- Killed the respawned orphan `dental-crm` dev tree rooted at PID `5576`, including Vite/API/tsx/esbuild children.

Cinematic cheats used:
- None. This was workstation cleanup, not simulation or rendering work.

Exact microseconds saved:
- Runtime: `0 us` claimed. No game profiler proof.
- Workstation pressure reduced by removing stale temp/cache data and a repeated orphan Node dev tree.

Cleanup proof:
- Freed approximately `5.01 GB`: user temp `2.389 GB`, recycle bin `0.975 GB`, `C:\hades\.tmp` `0.959 GB`, NVIDIA DXCache `0.250 GB`, Unity cache `0.183 GB`, crash dumps `0.117 GB`, Hecton `Temp/Obj` `0.137 GB`.
- Final C: free space: `153.22 GB` / `475.70 GB` (`32.2%` free).
- Final preserved sizes: `npm-cache 0.448 GB`, `Hecton8\Library 3.869 GB`, `Hecton8\Logs 0.125 GB`.
- Final watched dev ports: `4100` closed, `5173` closed; unrelated Python listeners on `8000` and `8080` remained.
- Final orphan list contained only protected/session processes: `csrss.exe`, `winlogon.exe`, `explorer.exe`, `WindowsTerminal.exe`, `Notepad.exe`.

## 2026-05-28 - Workstation Safe Improvement Pass

What was wrong:
- `C:\hades\dental-crm` contained stale `.edge-debug*` Microsoft Edge profile directories totaling `2.760 GB`.
- `dental-crm` dev-server respawned again as orphan-root PID `46256` and reopened the same Vite/API process tree.
- Orphan `HectonPhiStaticAudit.py --no-fail` PID `26420` was running with a missing parent.
- Active CPU load also included live Hecton work: `dotnet build Hecton8.slnx`, `csc.exe`, and later `OOP_Doc_Scanner.py`; these were not killed because they were active task-owned work, not abandoned processes.

What was done:
- Verified live Edge/WebView processes did not use `C:\hades\dental-crm\.edge-debug*` as their `user-data-dir`.
- Deleted `12` stale `.edge-debug*` directories from `C:\hades\dental-crm`.
- Terminated the respawned orphan `dental-crm` dev-server tree rooted at PID `46256`.
- Terminated orphan audit PID `26420`.
- Inspected scheduled tasks and startup entries. No `dental-crm`, `npm`, Hecton, or Python autostart source was found. NVIDIA `NvNodeLauncher` and normal OpenVPN/Bitdefender/Realtek/Edge RunOnce entries were left unchanged.

Cinematic cheats used:
- None. This was workstation hygiene and process containment.

Exact microseconds saved:
- Runtime: `0 us` claimed. No game profiler proof.
- Workstation pressure reduced by deleting `2.760 GB` of stale browser debug profile data and stopping abandoned Node/Python process trees.

Cleanup proof:
- Final C: free space: `158.45 GB` / `475.70 GB` (`33.3%` free).
- `C:\hades\dental-crm` final size: `0.459 GB`.
- Final watched dev ports: `4100` closed, `5173` closed; unrelated Python listeners on `8000` and `8080` remained.
- Final temp/cache sizes: user temp `0.202 GB`, Windows temp `0`, `C:\hades\.tmp` `0`, CrashDumps `0`, NVIDIA DX/GL cache `0`, Unity user caches `0`.
- Preserved by instruction: `npm-cache`, `Hecton8\Library`, `Hecton8\Logs`, and Hecton archive logs.

## 2026-05-28 - Core Vault Release Pass

What was wrong:
- `StaticDataStore` and `BabelDictionaryStore` shared telemetry/BTree DataVault buffer IDs while `EnsureGenerationHandle<T>()` does not create a separate lease for existing buffers.
- StaticData, Babel, SignalWarden tuning/telemetry/scratchpad, and MacroDatabase routes cleared Vault handles without consistently calling `ReleaseBuffer()`.
- Babel mapped dictionary/error-slice acquisition failure branches reset handles without release.

What was done:
- Added Babel-specific telemetry/BTree buffer IDs and moved Babel telemetry off StaticData/BTree shared IDs.
- Added release helpers and wired shutdown/rebind/close/failure paths through cached `IDataVault`.
- `GlobalSignals.DisposeAllQueues()` now releases tuning, telemetry ring, and scratchpad Vault handles.
- `H8MacroDatabaseService.Shutdown()` now releases scratch, blackbox, dirty payload, payload-copy, and sector-coordinate handles before clearing Vault state.
- MacroDatabase create/temp cleanup helpers were renamed with `Cold` suffix to document persistence-phase IO.

Cinematic cheats used:
- None. This was core memory lifecycle work, not simulation/rendering.

Exact microseconds saved:
- Runtime: `0 us` claimed. No profiler/player proof.
- Expected value is lower stale-native/rebind/shutdown risk, not measured frame-time improvement.

Proof:
- `git diff --check` on touched source: exit `0`; line-ending warnings only.
- Brace balance: `0` for all six touched source files.
- StaticData/Babel duplicate buffer ID scan: `0` duplicates across their `EnsureGenerationHandle<T>()` routes.
- Final SignalBus audit: `Docs/Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260528_CORE_VAULT_RELEASE_COLD_RECHECK.json`, `files=2443`, `shaders=71`, `errors=0`, `confirmedErrors=0`, `warnings=73`, `infos=1020`.
- Touched-file non-info audit findings: `0`.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=703 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=703 sourceSyncPass=true`.

Residuals:
- Full solution build was not run by this agent; user assigned overall compile errors to another agent.
- No Unity Editor import, Play Mode, profiler, GC, save/load, or player-build proof.

## 2026-05-28 - Core Sync IO And Accessor Pass

What was wrong:
- `LockstepStateValidator.StageReplayWrite()` could open/create replay file state from the post-fixed simulation route.
- Several cold Core IO helpers were named like normal runtime helpers, so the static contract could not distinguish lifecycle/user-commit IO from hot IO.
- `LockstepStateValidator.GetVaultBuffer<T>()` was not a pure getter; it could call `EnsureGenerationHandle<T>()`.

What was done:
- Moved lockstep replay writer setup to `OnEnable()` via `EnsureReplayWriterCold()`.
- Removed writer setup from `StageReplayWrite()`; it now uses only an already-open writer or skips replay writing.
- Renamed cold IO helpers in `InputDispatcher` and `RebindingManager`; added `HectonPersistentPathPolicy.EnsureParentDirectoryCold()` with compatibility wrapper.
- Renamed mutating lockstep DataVault helper to `OpenOrAcquireVaultBufferView<T>()`.

Cinematic cheats used:
- None. This was core route/lifecycle cleanup.

Exact microseconds saved:
- Runtime: `0 us` claimed. No profiler/player proof.
- Expected value is avoiding a possible first-replay synchronous file setup hitch in post-fixed simulation.

Proof:
- `git diff --check` on touched source: exit `0`; line-ending warnings only.
- Brace balance: `0` for all four touched source files.
- Final SignalBus audit: `Docs/Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260528_CORE_SYNC_IO_ACCESSOR_RECHECK.json`, `files=2443`, `shaders=71`, `errors=0`, `confirmedErrors=0`, `warnings=68`, `infos=1025`.
- Core subtree non-info audit findings: `0`.
- Documentation gates: `VerifyDocStructure.py pass=true activeDocCount=704 encodingWithoutUtf8Sig=0`; `OOP_Doc_Scanner.py finalPass=true activeFileCount=704 sourceSyncPass=true`.

Residuals:
- Full solution build was not run by this agent; user assigned overall compile errors to another agent.
- No Unity Editor import, Play Mode, profiler, GC, save/load, or player-build proof.

## 2026-05-28 - Global Signal Name Pass

What was wrong:
- SignalBus audit still had `DUPLICATE_SIGNAL_LIKE_NAME_REVIEW=8` and `EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW=3`.
- The duplicate names were namespace-safe C#, but unsafe as global telemetry/operator identifiers.

What was done:
- Renamed local/narrow duplicate telemetry DTOs:
  - `OceanSurfaceTelemetryEntry` in `HectonFluidEngine` -> `FluidOceanSurfaceTelemetryEntry`
  - private `StructuralTelemetryEntry` in `SubmarineStructuralGrid` -> `SubmarineStructuralTelemetryEntry`
  - private `ThermalTelemetryEntry` in `AbyssalThermalManager` -> `AbyssalThermalManagerTelemetryEntry`
  - nested `GasDynamicsSolver.AtmosphereTelemetryEntry` -> `GasDynamicsTelemetryEntry`
- Renamed editor-only `TelemetrySnapshotRow` -> `CrashSnapshotRow`.
- Kept layout, offsets, capacities, BufferIDs, SystemIDs, and runtime behavior unchanged.

Cinematic cheats used:
- None. This was telemetry contract hygiene.

Exact microseconds saved:
- Runtime: `0 us` claimed. No profiler/player proof.

Proof:
- Final SignalBus audit: `Docs/Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260528_GLOBAL_SIGNAL_NAME_RECHECK.json`, `files=2443`, `shaders=71`, `errors=0`, `confirmedErrors=0`, `warnings=57`, `infos=1024`.
- Closed categories: `DUPLICATE_SIGNAL_LIKE_NAME_REVIEW=0`, `EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW=0`.
- Touched source brace delta: `0`.
- Scoped `git diff --check`: exit `0`; line-ending warnings only.

Residuals:
- Remaining warnings are `RUNTIME_SYNC_FILE_IO_REVIEW=57`, spread outside the Core subtree.
- Full solution build was not run by this agent; user assigned overall compile errors to another agent.
- No Unity Editor import, Play Mode, profiler, GC, save/load, or player-build proof.
