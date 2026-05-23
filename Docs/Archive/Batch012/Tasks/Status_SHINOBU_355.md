# Status_SHINOBU_355

Agent: SHINOBU_355
Domain: HEADLESS_KCC_SMOKE_TESTER
Task count: 20
Mandates selected: OPT_Zero_GC_Policy_AllocFree_Mandate, OPT_Native_Memory_Collections_JobSystem_Protocol, DATA_Runtime_Struct_Layout_ARM64, MATH_Coordinate_Precision_AUP_FloatingOrigin, MATH_AUP_Determinism_Sync, MATH_Deterministic_RNG_SlotMachine, VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline, DBG_Telemetry_Crash_Reporting_PostMortem.

## Loop 1: Tasks 01-05

- [x] Task 01 MANDATORY_CODEBASE_GREP_SCAN
  - DOD practice: `rg` archaeology across `Assets/_Project/Tests` and `Assets/_Project/Scripts/Physics/KCC`.
  - Evidence: found legacy `HeadlessKccSmokeTests.cs`, `HydrodynamicKccRuntime.cs`, KCC jobs, `CoreDeterminismSignals.cs`, `GlobalSignals.cs`.
  - Rejected alternative: new unrelated smoke manager.
  - Estimate: 0 us hot path; static scan only.
- [x] Task 02 PARTIAL_CLASS_INTEGRATION_MANDATE
  - DOD practice: changed `HydrodynamicKccRuntime` to `partial` and added isolated `HectonKccRuntime_SmokeTest.cs`.
  - Evidence: smoke DTOs/jobs live under the existing KCC authority type.
  - Rejected alternative: standalone `HectonKccSmokeTester` runtime owner.
  - Estimate: 0 us gameplay hot path; offline IJob only.
- [x] Task 03 SIGNALBUS_MATRIX_VERIFICATION
  - DOD practice: checked interconnect docs and `DesyncDetectedSignal` layout, then wrote mock desync signal into a Vault-backed native lane.
  - Evidence: `MockDesyncSignals[0]` records hashes, frame, source id, rollback fence frame, entity offset.
  - Rejected alternative: publishing runtime events from an offline fuzzer.
  - Estimate: ~0.02 us per rollback fault write.
- [x] Task 04 GAME_OBJECT_SPAWN_INQUISITION
  - DOD practice: removed legacy standalone runner from `HeadlessKccSmokeTests.cs`; SceneView debug now uses `Handles`, not a spawned object.
  - Evidence: `rg` found 0 hits for `new GameObject`, `GameObject.Instantiate`, `Instantiate(`, `Physics.Simulate`.
  - Rejected alternative: PlayMode scene/player prefab smoke test.
  - Estimate: saves scene/bootstrap cost; no per-frame Unity scene work.
- [x] Task 05 MANAGED_TEST_LIST_PURGE
  - DOD practice: position history is `NativeArray<double3>` requested from `GlobalDataVault`.
  - Evidence: `SmokePositionHistoryBuffer` capacity = 100 phantoms * 10,000 frames.
  - Rejected alternative: `List<Vector3>` / managed trajectory list.
  - Estimate: avoids managed list growth and scattered heap reads; ~24 MB contiguous history lane.

## Loop 2: Tasks 06-10

- [x] Task 06 EMERGENCY_MOCK_TEST_GEOMETRY
  - DOD practice: implemented `GenerateMockTestGeometryJob` with synthetic SDF shell, wedges, crevices, and pillars.
  - Evidence: writes `BufferID.ShinobuKccEnvironmentSdf` through Vault handle.
  - Rejected alternative: waiting on authored cave assets.
  - Estimate: one cold SDF bake; 0 us per KCC frame after bake.
- [x] Task 07 BURST_HEADLESS_FRAME_LOOP_KERNEL
  - DOD practice: implemented `[BurstCompile(CompileSynchronously=true)] EvaluateHeadlessKccFrameLoopJob`.
  - Evidence: 10,000-frame nested loop over 100 phantoms, hostile deterministic input, swept SDF collision resolve.
  - Rejected alternative: one scheduled job per frame or Unity game-loop coroutine.
  - Estimate: target budget recorded as <=100 us average per simulated frame in report DTO.
- [x] Task 08 THE_DEAR_LIE_NaN_VACCINATION_VERIFIER
  - DOD practice: finite checks after each step; critical NaN/desync flags fail-fast and deterministically fill remaining history slots.
  - Evidence: `KccSmokeFailureNonFinite`, `FillRemainingHistory(...)`, black-box dump on failure.
  - Rejected alternative: continuing simulation over corrupt non-finite state without forensic boundary.
  - Estimate: finite checks are scalar branch checks; sub-0.1 us per phantom frame expected.
- [x] Task 09 PHYSICAL_ESCAPE_DETECTOR_MATH
  - DOD practice: implemented `VerifyCollisionEscapeJob` over the full recorded AUP history.
  - Evidence: samples SDF from `double3` AUP via origin-subtracted local coordinates and flags `KccSmokeFailureEscape`.
  - Rejected alternative: `Physics.Raycast`/Unity Physics overlap checks.
  - Estimate: O(1,000,000) SDF trilinear probes; offline CI only.
- [x] Task 10 CONTINUOUS_PRECISION_DRIFT_ANALYSIS
  - DOD practice: implemented `AnalyzePrecisionDriftJob` and drift probe DTO.
  - Evidence: drift error millimeters written into `KccSmokeTestResultDTO`.
  - Rejected alternative: frame-by-frame managed assert spam.
  - Estimate: one cold scalar comparison after simulation; negligible against fuzzer cost.

## Loop 3: Tasks 11-14

- [x] Task 11 AUP_PRECISION_REBASE_STRESS
  - DOD practice: simulated floating-origin stress every 500 frames by shifting local origin only.
  - Evidence: `localOriginAup += double3(5000,0,5000)` and `HydrodynamicKccMath.ResolveLocalFloat3`.
  - Rejected alternative: mutating authoritative AUP truth, which would violate AUP ownership.
  - Estimate: one double3 add every 500 frames; not measurable per frame.
- [x] Task 12 ROLLBACK_NETCODE_DETERMINISM_VERIFIER
  - DOD practice: implemented 30-frame rollback ring and twin resimulation hash/bitwise AUP comparison.
  - Evidence: `KccSmokeRollbackWindowFrames`, `RollbackStateRing`, `RecordMockDesyncSignal`.
  - Rejected alternative: comparing only rounded/vector-distance outputs.
  - Estimate: one entity rollback probe per frame after warmup; offline budget.
- [x] Task 13 ZERO_INIT_OVERHEAD_BYPASS
  - DOD practice: all Vault smoke buffers use `NativeArrayOptions.UninitializedMemory`; jobs overwrite slots deterministically.
  - Evidence: no `UnsafeUtility.MemClear` hits in SHINOBU_355 scope.
  - Rejected alternative: zero-filling million-slot history before immediate overwrite.
  - Estimate: saves ~24 MB history clear plus auxiliary lane clears per run.
- [x] Task 14 TELEMETRY_SMOKE_TEST_RECORDER
  - DOD practice: 300-entry `KccSmokeTelemetryEntry` ring in Vault; raw span dump path on failure.
  - Evidence: `Docs/AgentLogs/Dump_SHINOBU_355.bin`, `stream.Write(new ReadOnlySpan<byte>(ptr, byteCount))`.
  - Rejected alternative: string logs per frame.
  - Estimate: 64 B * 300 ring = 19.2 KB forensic window.

## Loop 4: Tasks 15-18

- [x] Task 15 SMOKE_TEST_TUNER_EDITOR_WINDOW
  - DOD practice: UI Toolkit window with run button, progress bar, and penetration-depth line graph over last telemetry native cache.
  - Evidence: `HeadlessKccSmokeTesterWindow`, `HeadlessKccSmokeTelemetryGraphElement`, `TryGetLastTelemetry`.
  - Rejected alternative: IMGUI repaint allocations or text-only output.
  - Estimate: editor-only; 0 us gameplay hot path.
- [x] Task 16 CSV_TEST_PROFILES_INGESTOR
  - DOD practice: cold parser slices `ReadOnlySpan<byte>`, uses FNV-1a, and parses numeric fields without `float.Parse`/`double.Parse`.
  - Evidence: `TryLoadProfiles`, `TryReadProfile`, `TryReadDouble`, `HashFnv1A`.
  - Rejected alternative: `CsvHelper`, LINQ, culture-sensitive parsing.
  - Estimate: cold boot only; no simulation-loop cost.
- [x] Task 17 LIVE_FUZZ_DEBUG_GIZMO
  - DOD practice: static SceneView `Handles` debug draws trajectory segment, red failure marker, yellow velocity arrow, and cyan input vector.
  - Evidence: `HeadlessKccFailureGizmo.SetFailure(failure.Aup, failure.PreviousAup, failure.Velocity, failure.InputVector)`.
  - Rejected alternative: `ExecuteAlways` MonoBehaviour / debug GameObject.
  - Estimate: editor-only; 0 us gameplay hot path.
- [x] Task 18 ARCHITECTURAL_METRIC_VALIDATOR
  - DOD practice: added Roslyn-based `OOP_Test_Scanner` menu item and static JSON proof artifact.
  - Evidence: `KccSmokeArchitectureValidators.cs`, `Docs/Reports/QA_OPTIMIZATION_REPORT.json`.
  - Rejected alternative: regex-only permanent validator; current shell `rg` is recorded as supplemental proof.
  - Estimate: editor command only; 0 us gameplay hot path.

## Loop 5: Tasks 19-20

- [x] Task 19 UNALIGNED_MEMORY_TRAP_GUARD
  - DOD practice: `InitializeOnLoad` guard validates size, align, and offsets for `KccSmokeTestStateDTO`.
  - Evidence: `KccSmokeLayoutGuard` throws `FatalArchitectureException` if Size != 32, Align != 8, Offsets != 0/24/28.
  - Rejected alternative: relying on NUnit only.
  - Estimate: editor startup reflection only; 0 us runtime hot path.
- [x] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION
  - DOD practice: 5 loops completed; static grep, brace balance, DTO route card, status, rationale, and final log artifacts maintained.
  - Evidence: forbidden-pattern grep returned no hits; brace balance true for 4 touched C# files.
  - Rejected alternative: declaring GREEN without compile/import proof.
  - Estimate: source audit only; no runtime cost.

## Compile State

- Guarded compile attempted once: `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false`.
- Result: blocked before SHINOBU_355 diagnostics by external dependency errors:
  - `Assets/_Project/Scripts/Construction/HatchLockJobs.cs(12,45): CS0234 Hecton8.Habitat missing`
  - `Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime_HatchLocks.cs(15,45): CS0234 Hecton8.Habitat missing`
- Current rebuild guard: CPU sample 15%; existing `dotnet` processes are active, so no new build launched.
- Status: STATIC_SOURCE_WIRED / COMPILE_BLOCKED_BY_EXTERNAL_DEPENDENCY.

## Polish Loop 6: Subagent Audit Fixes

- [x] Runtime SDF and profile bounds hardened.
  - DOD practice: `ResolveProfile` clamps profile count to `Profiles.Length`; `SampleSdfStatic` validates dimensions, finite cell size, overflow-safe required cells, and backing length.
  - Evidence: `TryResolveSdfLayout`, `KccSmokeInvalidSdfMeters`, `KccSmokeFailureSdfInvalid`.
  - Rejected alternative: trusting test bootstrap invariants inside a Burst kernel.
  - Estimate: one branch group per SDF sample; cheaper than corrupt native reads and only active in offline QA.
- [x] Out-of-volume escape detection fixed.
  - DOD practice: invalid/outside SDF no longer returns positive safe space; verifier records `Escape | SdfInvalid`.
  - Evidence: `SampleSdfStatic` returns `-4096f` sentinel, verifier treats it as a breach.
  - Rejected alternative: treating bounds miss as open water.
  - Estimate: no extra allocations; prevents false PASS on tunnel-through-world cases.
- [x] Rollback desync comparator widened.
  - DOD practice: replay now compares AUP bits, velocity bits, and flags against authoritative current state.
  - Evidence: `ReplayStateMatches` and replay collision flag application.
  - Rejected alternative: AUP-only comparison that misses velocity/flag divergence.
  - Estimate: six scalar bit comparisons per sampled rollback probe.
- [x] Editor telemetry Vault ownership fixed.
  - DOD practice: removed private persistent `NativeArray` cache; retained the runner `GlobalDataVault` and exposes `NativeArray<T>.ReadOnly`.
  - Evidence: `RetainTelemetryVault`, `TryReadOnlyHandle`, `DisposeTelemetryVault`.
  - Rejected alternative: copying native telemetry into a separate persistent editor-owned array.
  - Estimate: saves one 19.2 KB native copy per run and removes a second ownership route.
- [x] NoAlias caller proof added for same-typed state lanes.
  - DOD practice: cold NUnit runner asserts `statesHandle.BufferID != rollbackHandle.BufferID` before scheduling jobs with `[NoAlias]` on both `NativeArray<KinematicStateDTO>` lanes.
  - Evidence: `Assert.AreNotEqual(statesHandle.BufferID, rollbackHandle.BufferID)`.
  - Rejected alternative: removing `[NoAlias]` from the Burst job and forfeiting vectorization proof for a known distinct Vault route.
  - Estimate: one cold assertion; 0 us job hot path.
- [x] Report and duplicate-test polish applied.
  - DOD practice: OOP scanner writes `QA_OPTIMIZATION_OOP_REPORT.json`; smoke report references it. Duplicate 10,000-frame NUnit route now checks constants only.
  - Evidence: single heavy run remains `Shinobu355_KccSmoke_100Phantoms_10000Frames_NoNanEscapeRollbackDesync`.
  - Rejected alternative: two tests running the same expensive pass and racing the same report file.
  - Estimate: saves one full smoke pass per editor test sweep.
- [x] Static re-verification after polish.
  - DOD practice: brace balance over four touched C# files; forbidden-pattern `rg` returned zero hits in SHINOBU_355 scope.
  - Evidence: no hits for `new GameObject`, `Instantiate(`, `Physics.Simulate`, `List<Vector3>`, parser `Parse`, `Allocator.Persistent`, telemetry cache, `foreach`.
  - Rejected alternative: launching rebuild under known external compile wall without need.
  - Estimate: source-only check; 0 us hot path.

## Polish Loop 7: Editor Assembly Boundary

- [x] Editor facade moved out of test assembly.
  - DOD practice: `Shinobu355KccSmokeRunner`, `Shinobu355KccSmokeSummary`, `HeadlessKccLayoutAssertions`, `HeadlessKccSmokeTesterWindow`, telemetry graph, and failure gizmo now live under `Assets/_Project/Scripts/Physics/KCC/Editor`.
  - Evidence: `Shinobu355KccSmokeEditorFacade.cs`; `HeadlessKccSmokeTests.cs` and `Shinobu355KccSmokeEditTests.cs` are thin NUnit callers only.
  - Rejected alternative: reflection from the editor window into the test assembly; that would be brittle and hide the route.
  - Estimate: 0 us hot path; prevents test-only define constraints from owning the human tooling facade.
- [x] NUnit dependency removed from the editor runner.
  - DOD practice: runner/layout checks throw `FatalArchitectureException` instead of calling `Assert`.
  - Evidence: no `NUnit` or `Assert.` hits in `Assets/_Project/Scripts/Physics/KCC/Editor`.
  - Rejected alternative: adding `nunit.framework.dll` to the KCC editor asmdef.
  - Estimate: editor-only; keeps production editor tooling independent from test framework lifecycle.
- [x] Asmdef route adjusted explicitly.
  - DOD practice: `Hecton8.Physics.KCC.Editor.asmdef` now owns unsafe raw dump code and `Unity.Jobs`; `Hecton8.EditModeTests.asmdef` references the editor facade as a test dependency.
  - Evidence: `allowUnsafeCode=true` in KCC editor asmdef; `Hecton8.Physics.KCC.Editor` added to edit-mode test references.
  - Rejected alternative: leaving unsafe dump path in tests while the window/gizmo lived elsewhere.
  - Estimate: no runtime gameplay cost; narrower test assembly and no duplicated editor tooling.
- [x] OOP scanner extended for test-owned editor tooling.
  - DOD practice: `OOP_Test_Scanner` now counts `EditorWindow`, `SceneView`, and UI Toolkit identifiers in KCC/Kinematic tests.
  - Evidence: `editor_window_hits`, `scene_view_hits`, `ui_elements_hits` fields in `QA_OPTIMIZATION_OOP_REPORT.json`.
  - Rejected alternative: claiming the facade move without a repeatable scanner proof.
  - Estimate: cold Roslyn scan only; 0 us hot path.

## Polish Loop 8: Scheduled Editor Pipeline

- [x] Editor window no longer blocks while the long smoke job is running.
  - DOD practice: added `StartScheduledRun()` and `ScheduledRun` to return a chained `JobHandle` pipeline; the UI polls `IsCompleted` from `EditorApplication.update`.
  - Evidence: `GenerateMockTestGeometryJob -> InitializeSmokePhantomsJob -> EvaluateHeadlessKccFrameLoopJob -> VerifyCollisionEscapeJob -> AnalyzePrecisionDriftJob` is chained through `JobHandle.CombineDependencies` and `.Schedule(dependency)`.
  - Rejected alternative: leaving the button as a direct `Run(...).Complete()` call, which freezes the editor window for the whole pass.
  - Estimate: no gameplay hot-path cost; editor main thread is released during the 100 * 10,000 simulation pass.
- [x] Scheduled editor finalization is a completion-window sync, not a mid-run readback.
  - DOD practice: `ScheduledRun.Poll()` calls `Complete()` only after `_finalHandle.IsCompleted`; `Dispose()` drains only on editor teardown.
  - Evidence: `HeadlessKccSmokeTesterWindow.PollSmokeTest()` keeps the button disabled, updates progress, and finalizes reports/gizmo after the handle is complete.
  - Rejected alternative: `async Task`/`await`, which AGENTS.md rejects for this editor path and would allocate managed task state.
  - Estimate: avoids one editor UI freeze; exact frame-time proof remains pending Unity import.

## Polish Loop 9: Evidence Artifact Honesty

- [x] Scheduled editor report no longer writes a false 0-byte GC proof.
  - DOD practice: scheduled runs now capture `GC.GetAllocatedBytesForCurrentThread()` before scheduling and report the delta separately from the CI/NUnit synchronous route.
  - Evidence: `WriteReport(..., "UNITY_EDITOR_SCHEDULED_JOB_PENDING_IMPORT_PROOF")` marks the editor route distinct from `"UNITY_EDITOR_JOB_RUN_PENDING_EXTERNAL_COMPILE_WALL"`.
  - Rejected alternative: leaving `ManagedBytesAllocated = 0` in the scheduled route, which would make the report look like a measured hot-path proof when it is an editor facade run.
  - Estimate: one cold scalar sample before scheduling and one after completion; 0 us gameplay hot-path cost.

## Polish Loop 10: Subagent Audit Closure

- [x] Bulk Vault retention removed from the editor graph path.
  - DOD practice: post-run telemetry is copied into a dedicated 1 MiB telemetry-only `GlobalDataVault`; the 128 MiB smoke Vault is disposed after completion.
  - Evidence: `RetainTelemetrySnapshot(...)` replaces retaining the full runner Vault.
  - Rejected alternative: keeping the whole smoke Vault alive for a 300-entry graph.
  - Estimate: retains 1 MiB cap instead of 128 MiB cap; one cold 19.2 KB copy per run.
- [x] Black-box dump upgraded from ad hoc raw ring to ordered binary forensic payload.
  - DOD practice: dump header now writes magic/version/count/entry-size/oldest/newest/source hash in little-endian fields; telemetry rows are rotated oldest-to-newest.
  - Evidence: `WriteBlackBoxDump(telemetry, result)` uses `FirstFailureFrame` or default frame count to choose the ring window.
  - Rejected alternative: writing ring memory in modulo order with no struct-size/version proof.
  - Estimate: cold failure IO only; 0 us simulation hot path.
- [x] Schedule-count and CSV ingestion guards added.
  - DOD practice: all resolved native lane lengths are asserted before scheduling; CSV numeric parser rejects integer overflow and profile range violations.
  - Evidence: `ValidateSmokeBuffers(...)`, `RequireLength(...)`, overflow guard in `TryReadDouble`, and AUP/speed/bias range checks in `TryReadProfile`.
  - Rejected alternative: relying on Vault allocation intent while jobs take unsafe pointers.
  - Estimate: cold runner checks; 0 us Burst simulation cost.
- [x] Layout and scanner proof tightened.
  - DOD practice: executable offset assertions now cover smoke profile, SDF info, result, failure, telemetry, and drift DTOs; OOP scanner report declares KCC/Kinematic scope and detects unqualified `Instantiate`.
  - Evidence: `HeadlessKccLayoutAssertions.RequireOffset<T>(...)`, `KccSmokeLayoutGuard` invokes full assertions, scanner writes `scope`.
  - Rejected alternative: source-visible `[FieldOffset]` without runtime offset proof.
  - Estimate: editor startup/menu scan only; 0 us gameplay cost.
- [x] Scheduled-run teardown made fail-closed.
  - DOD practice: `_finalizeAttempted` prevents `Dispose()` from re-running a failed finalization path after `Poll()` throws.
  - Evidence: scheduled runner drains and disposes the Vault without duplicating report/snapshot work.
  - Rejected alternative: letting the catch path recursively call the same failing finalizer.
  - Estimate: one cold bool branch in editor teardown; 0 us Burst simulation cost.

## Polish Loop 11: AUP Debug And Ledger Proof

- [x] AGENTS/domain/authority/ledger reread before further changes.
  - DOD practice: re-read `AGENTS.md`, `Docs/Actual Domains of Project.txt`, `GLOBAL_AUTHORITY_BOUNDARIES.md`, and the binary payload ledger context before touching source.
  - Evidence: SHINOBU_355 remains an Echelon 9 QA/meta lane over KCC; no production KCC authority route was claimed.
  - Rejected alternative: patching from chat memory after context compaction.
  - Estimate: source/docs only; 0 us hot path.
- [x] SceneView failure gizmo AUP cast tightened.
  - DOD practice: cold gizmo now stores absolute `double3` failure truth but subtracts `previousAup` via `HydrodynamicKccMath.ResolveLocalFloat3` before creating debug `Vector3` values.
  - Evidence: `HeadlessKccFailureGizmo` has `s_gizmoOriginAup` and no direct `(float)s_failureAup.x` cast remains.
  - Rejected alternative: leaving editor-only direct float casts because they are not gameplay; that still weakens AUP audit discipline.
  - Estimate: editor-only two localizations per SceneView repaint; 0 us Burst simulation cost.
- [x] Binary payload ledger records SHINOBU_355.
  - DOD practice: added a dated ledger entry with owner, local BufferIDs `71810..71818`, DTO byte sizes, AUP route, Dear Lie route, and black-box dump format.
  - Evidence: `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` now includes `SHINOBU_355 Headless KCC Smoke Tester Boundary`.
  - Rejected alternative: route-card-only documentation, which leaves the global binary ledger unaware of the QA-only DTOs.
  - Estimate: docs only; 0 us hot path.
- [x] Loop 11 static verification.
  - DOD practice: re-extracted the SHINOBU_355 XML block by CLI, reran scoped forbidden-token grep, brace balance, JSON parse, AUP-gizmo direct-cast scan, and `git diff --check`.
  - Evidence: `prompt_chars=23818 task_lines=20`; brace balance 0 for five touched C# files; JSON parse OK; no `(float)s_failureAup` or `(float)s_previousAup` cast remains; `git diff --check` exit 0 with CRLF warnings only.
  - Rejected alternative: launching a rebuild while CPU sampled 63.96%, above the 50% gate.
  - Estimate: source-only checks; 0 us hot path.
- [x] KCC editor asmdef route inspected after context compaction.
  - DOD practice: checked `Hecton8.Physics.KCC.Editor.asmdef`, `Hecton8.EditModeTests.asmdef`, and existing `HydrodynamicKccTunerWindow.cs` ownership before changing assembly routing.
  - Evidence: the pre-existing KCC editor assembly already contains KCC runtime-facing editor tooling; SHINOBU_355 added no new runtime asmdef and no sibling runtime reference.
  - Rejected alternative: introducing a new `Hecton8.Physics.KCC.Runtime.asmdef` around `HydrodynamicKccRuntime`, which would be a broad compile-wall move outside this QA lane.
  - Estimate: docs/static inspection only; 0 us hot path.
- [x] Roslyn syntax proof attempt classified as unavailable, not source failure.
  - DOD practice: attempted a no-build Roslyn syntax parse over five SHINOBU C# files after CPU build gate blocked rebuild.
  - Evidence: local Roslyn DLL loading failed with `ReflectionTypeLoadException` / `Roslyn.Utilities.StringTable` initializer failure before parser diagnostics were produced.
  - Rejected alternative: treating the loader failure as five C# syntax errors; brace balance/diff-check remain the valid source-level checks for this loop.
  - Estimate: failed tool probe only; 0 us hot path.
