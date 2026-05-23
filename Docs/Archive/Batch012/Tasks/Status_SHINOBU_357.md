PROMPT IDENTIFIED: SHINOBU_357 | DOMAIN: Echelon 9 Meta and Integration / offline QA save persistence integrity | TASK COUNT: 19

Status: POLISH PASS IMPLEMENTED - DOTNET BUILD BLOCKED BY CPU GATE
Owner: SHINOBU_357
Scope: WAL save persistence integrity fuzzer, backup-promotion rollback validation, editor-only QA facade.

Mandates selected before coding:
- DATA_Save_Persistence_Binary_Delta_Checksum.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- MATH_AUP_Determinism_Sync.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Execution_Phases.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt

Loop 0 - Pre-code archaeology:
- [x] Task 01 MANDATORY_CODEBASE_GREP_SCAN | DOD: `rg` scan over `Assets/_Project/Tests` and `Assets/_Project/Scripts/SaveSystem`; found existing `WalIntegrityFuzzerCore`, `SaveStateMerkleTree`, `H8BinaryWorldPager`, and `WalIntegrityCheckerEditTests`. Alternative rejected: new standalone fuzzer duplicating Agent 256 rollback surface. Estimate: 900 us static scan execution, excluding shell startup.
- [x] Task 02 PARTIAL_CLASS_INTEGRATION_MANDATE | DOD: integrate through existing `WalIntegrityFuzzerCore` partial, not a new competing `HectonSaveFuzzer`. Alternative rejected: editing `SaveManager` because it is a large sealed runtime manager and Agent 256 fuzzer already owns the offline WAL QA path. Estimate: 60 us code-review decision.
- [x] Task 03 SIGNALBUS_MATRIX_VERIFICATION | DOD: read `SYSTEM_INTERCONNECT_MATRIX.md`; `SaveEvents` lane is queue-backed and flushed by `SystemDispatcher.LateUpdate`; Submarine OS and PowerGrid telemetry lanes are adjacent consumers, not direct dependencies. Alternative rejected: emitting runtime SaveStatus signal from offline QA. Estimate: 300 us source/doc lookup.
- [x] Task 04 MANAGED_FILE_IO_INQUISITION | DOD: added bounded source scanner over `Assets/_Project/Scripts/SaveSystem` and `Assets/_Project/Tests/Editor/SaveSystem`; reports `FileStream` as cold findings, fatal-gates `StreamWriter`. Alternative rejected: deleting `H8BinaryWorldPager` and Merkle WAL file handles because they are the production persistence route, not disposable test stubs. Estimate: 42 us per source file after directory enumeration.
- [x] Task 05 OBJECT_ORIENTED_SERIALIZATION_PURGE | DOD: scanner fatal-gates `JsonUtility` and `BinaryFormatter`; edit test asserts zero fatal serializer findings without embedding false positive token literals. Alternative rejected: reflection/managed formatter fuzzer path. Estimate: 55 us per scanned source file after enumeration.

Loop 1 - Implementation targets:
- [x] Task 06 MOCK_WAL_CORRUPTION_GENERATOR | DOD: implemented `GenerateMockCorruptWalJob` over unmanaged `NativeArray<byte>` buffers with deterministic truncation/mutation and explicit `NativeArray<WalFuzzStateDTO>[0]` state writes. Alternative rejected: managed byte-array WAL fabrication. Estimate: 820 us for 10 MB sequential overwrite on desktop Burst worker, profile dependent.
- [x] Task 07 BURST_HEADLESS_WAL_FUZZ_KERNEL | DOD: implemented `EvaluateHeadlessWalFuzzJob` with synchronous Burst compilation, 100-iteration cap, deterministic interrupt offsets, telemetry overwrite, and no heap allocations in the job. Alternative rejected: main-thread NUnit loop as the primary correctness proof. Estimate: 290 us for 100 math iterations excluding disk I/O.
- [x] Task 08 THE_DEAR_LIE_CHECKSUM_VERIFIER | DOD: disk loop corrupts primary WAL, calls Agent 256 `SaveStateMerkleTree.TryValidateWalAndRollback`, then validates `.bak` promotion by XXHash3-derived `SaveBinaryStorage.Hash64` equality and byte count equality. Alternative rejected: trusting validator return code without file hash proof. Estimate: 180 us per 64 KB validation read; 10 MB profile scales linearly with disk.
- [x] Task 09 FILE_LOCK_LEAK_DETECTOR_MATH | DOD: added exclusive read/write reopen probe for primary and `.bak` after the loop; flags `WalFuzzFileLockLeak` on sharing failure. Alternative rejected: assuming `using FileStream` disposal is proof. Estimate: 35 us metadata/open-close on SSD cache.
- [x] Task 10 BACKUP_PROMOTION_STABILITY_ANALYSIS | DOD: every interrupted primary is restored from `.bak`, revalidated, hashed, and compared against backup bytes before the next iteration. Alternative rejected: single-shot rollback smoke test. Estimate: 215 us per bounded 64 KB iteration excluding file-system variance.

Loop 2 - Determinism and telemetry:
- [x] Task 11 ROLLBACK_NETCODE_DETERMINISM_VERIFIER | DOD: Burst kernel computes deterministic rollback probe for current frame and 30-frame rewind path; sets `WalFuzzRollbackDesync` on bit mismatch. Alternative rejected: frame-object rollback simulation with managed state. Estimate: 9 us for 100 hash probes.
- [x] Task 12 ZERO_INIT_OVERHEAD_BYPASS | DOD: SHINOBU_357 payload/corrupt/state/telemetry/hash/file-handle buffers request DataVault lanes `73470..73476` with `NativeArrayOptions.UninitializedMemory`; TempJob fallback is editor/test-only when the vault is absent or locked. Alternative rejected: `MemClear` tax on buffers that are immediately overwritten. Estimate: saves about 450 us on 10 MB payload clear on i3/MX350-class memory bandwidth.
- [x] Task 13 TELEMETRY_WAL_FUZZ_RECORDER | DOD: 300-entry `WalFuzzTelemetryEntry` ring, DataVault buffer IDs `73473`/`73474`, raw 64-byte row dump to `Docs/AgentLogs/Dump_SHINOBU_357.bin` on mismatch. Alternative rejected: JSON-only failure logs. Estimate: 19.2 KB dump, about 90 us cached write.

Loop 3 - Editor and static validation:
- [x] Task 14 WAL_FUZZ_TUNER_EDITOR_WINDOW | DOD: added UI Toolkit window `HECTON-8/Save/WAL Save Fuzzer` with run button, scanner button, progress bar, and telemetry graph. Alternative rejected: IMGUI-only debug panel. Estimate: editor-only, 0 us runtime frame cost.
- [x] Task 15 CSV_FUZZ_PROFILES_INGESTOR | DOD: added SHINOBU_357 CSV wrapper over the existing allocation-free `TryLoadProfilesCsv` parser and routed a profile parser edit test through it. Alternative rejected: duplicate CSV parser. Estimate: same as Agent 256 parser, bounded by file byte count.
- [x] Task 16 LIVE_FUZZ_DEBUG_GIZMO | DOD: SceneView failure gizmo draws green disk-sector line, red failure sphere, and yellow direction arrow from recorded state. Alternative rejected: log-only offset reporting. Estimate: editor repaint only, 0 us runtime.
- [x] Task 17 ARCHITECTURAL_METRIC_VALIDATOR | DOD: scanner writes `Docs/Reports/QA_OPTIMIZATION_REPORT.json` with summary `OOP Fuzzers Eradicated` and fatal finding counts. Alternative rejected: chat-only architecture claim. Estimate: 42-55 us per scanned file after enumeration.
- [x] Task 18 UNALIGNED_MEMORY_TRAP_GUARD | DOD: `InitializeOnLoad` guard checks `WalFuzzStateDTO`, `WalFuzzTelemetryEntry`, and `WalFuzzFileHandleStatusDTO` size, alignment, and offsets, throwing `FatalArchitectureException` on violation. Alternative rejected: relying only on NUnit coverage. Estimate: one editor domain-load check, below 15 us after type load.

Loop 4 - Audit:
- [x] Task 19 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | DOD: added DTO layout edit tests, bounded rollback edit test, scanner edit test, diff whitespace check, scanner false-positive boundary checks, and rationale/log artifacts. Alternative rejected: final chat report without artifacts. Estimate: static checks below 1 ms excluding shell startup.

Loop 5 - Polish pass:
- [x] Vault ownership reconciliation | DOD: documented local casted DataVault lanes `73470..73476` in `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` and removed stale previous-telemetry-ID SHINOBU_357 claims from status/log artifacts. Alternative rejected: core `BufferID` enum edit under active multi-agent compile-wall risk. Estimate: 140 us source/doc lookup plus patch time.
- [x] Unsafe pointer eradication | DOD: removed raw state pointer handoff from SHINOBU_357 jobs; state mutates through `NativeArray<WalFuzzStateDTO>[0]`, corrupt WAL is `[ReadOnly, NoAlias]`, and rollback probe consumes a corrupted byte. Alternative rejected: `[NativeDisableUnsafePtrRestriction]` on fuzzer state. Estimate: below 1 us per 100-iteration probe addition.
- [x] Platform I/O fallback | DOD: `.partial` WAL promotion now uses `File.Replace` with delete/move fallback for unsupported or metadata-failed replacement paths. Alternative rejected: retrying replacement loops that could hide real lock leaks. Estimate: one delete plus one move on fallback path.
- [x] Cache-line test proof | DOD: edit tests assert `WalFuzzTelemetryEntry` and `WalFuzzFileHandleStatusDTO` explicit 64-byte layout and 8-byte alignment. Alternative rejected: relying on log-only layout math. Estimate: editor import/test reflection only, 0 us runtime.

Loop 6 - Inquisitorial polish:
- [x] Partial-file failure hygiene | DOD: failed `.partial` promotion now deletes the stale partial WAL before returning failure, so the next iteration cannot observe abandoned interrupted bytes. Alternative rejected: leaving cleanup to the outer catch because `TryReplaceOrMoveWal` failure is not an exception path. Estimate: one `File.Exists` + delete on failure only.
- [x] Editor facade cache discipline | DOD: `WalSaveFuzzerWindow` caches `IDataVault` on enable/create, uses a fixed `char[192]` summary scratch with canonical `COLD ALLOC` comment, and formats scanner/telemetry summaries through indexed append helpers. Alternative rejected: repeated string concatenation and repeated `GlobalRegistry.DataVault` reads every graph repaint. Estimate: editor-only; graph repaint avoids two cold registry property reads per draw when cached.

Loop 7 - Evidence correction:
- [x] Current-batch prompt proof | DOD: strict attribute-aware extraction of `Docs/Tasks/CURRENT_BATCH.md` found `PROMPT_FOUND=1`, `PROMPT_BYTES=22513`, `TASK_COUNT=19` for `<AGENT_PROMPT id="SHINOBU_357" role="WAL_SAVE_PERSISTENCE_INTEGRITY_FUZZER" chat_name="SHINOBU_357">`. Alternative rejected: the earlier too-narrow regex that required the tag to end immediately after `id`. Estimate: 1.2 s bounded extraction, no codegen impact.
- [x] Build gate resample | DOD: first gate sampled CPU 49.1% and no dotnet/csc/MSBuild rows, second pre-build resample hit CPU 85.1%, and latest resample hit CPU 99%; project rule blocks `dotnet build` when CPU exceeds 50%. Alternative rejected: launching a compile during an active CPU spike. Estimate: saved one full Unity-generated project compile under load.

Verification:
- Compile: BLOCKED. Latest pre-build resample reported CPU at 99%; no build was launched under the >50% CPU gate.
- Unity import: PENDING. Requires editor compile/import after CPU gate clears.
- Static validation: PASS. `git diff --check` passed for touched files with Git CRLF warnings only; attribute-aware `CURRENT_BATCH.md` extraction returned `PROMPT_FOUND=1`, `TASK_COUNT=19`; local source scan found no SHINOBU_357 `StatePtr`, no active SHINOBU_357 `NativeDisableUnsafePtrRestriction`, no `Pack=1`, and no stale previous telemetry BufferID constants in owned code.
- GCMonitor/profiler: NOT RUN. New hot math is Burst/job over `NativeArray`; disk/editor/report paths are cold QA.
