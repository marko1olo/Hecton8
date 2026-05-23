# Rationale_SHINOBU_355

## Decision 01: Reuse Existing KCC Runtime Surface

Problem: The repository already contains `HydrodynamicKccRuntime`, KCC DTOs, KCC Vault handles, rollback bytes, environment SDF buffers, and an editor-only `HeadlessKccSmokeTests.cs` prototype. Creating a new standalone runtime owner would duplicate KCC authority.
Solution: Integrate smoke-test Burst DTOs/jobs through a partial `HydrodynamicKccRuntime` file and keep editor facade code as a caller/reporting shell only.
Rejected Alternatives: A new `HectonKccSmokeTester` MonoBehaviour or runtime manager would add a second KCC ownership path and widen assembly dependencies.
Scalability potential: Low uses flat 32/64-byte DTO lanes and bounded history; Middle/High/Ultra keep the same authority math and increase only diagnostic retention/report richness.
Hardware Impact: i3/MX350 avoids scene load, Transform sync, and Unity Physics scene stepping; expected saving versus GameObject/PlayMode movement sweep is >100 us per 10,000-frame smoke pass frame equivalent on editor hardware, not claimed as profiler proof.

## Decision 02: Keep Hot Loop in Native/Burst, Reports in Cold Editor IO

Problem: The fuzzer needs JSON/CSV/binary artifacts, but managed IO and strings cannot enter the 10,000-frame loop.
Solution: Burst jobs write NativeArray DTOs and 300-frame telemetry; cold editor/test runner serializes reports after job completion.
Rejected Alternatives: Formatting strings or writing files from inside simulation code is not Burst-compatible and would allocate.
Scalability potential: Low keeps one compact binary dump; Ultra can render richer editor graph/gizmo from the same fixed telemetry without bloating gameplay DTOs.
Hardware Impact: Hot-path heap stays 0 B by construction; editor IO cost is outside simulation and CI can isolate it.

## Decision 03: Floating-Origin Stress Must Not Mutate AUP Truth

Problem: Task 11 asks for a 500-frame floating-origin shift, but project AUP doctrine says canonical `double3` AUP is the truth and consumers localize by subtracting an origin before float casts.
Solution: The smoke job shifts `localOriginAup` every 500 frames and verifies `ResolveLocalFloat3(state.AUP_Position, localOriginAup)` stays finite. Authoritative `state.AUP_Position` remains absolute.
Rejected Alternatives: Subtracting 5000m from every Vault KCC position would fake an origin shift by changing gameplay truth and invalidate rollback/history hashes.
Scalability potential: Low/Middle/High/Ultra all use the same double AUP truth; only downstream diagnostic graph richness scales.
Hardware Impact: One `double3` add per 500 frames; on i3/MX350 the cost is below measurement noise while protecting against catastrophic float truncation near 99km.

## Decision 04: Rollback Probe Splits Original Replay and Mutated Replay

Problem: Literal comparison of a replay with deliberately modified historical input against the original trajectory would always differ and create a false desync on every valid correction.
Solution: The fuzzer does two checks: unmodified twin replay must match original bit-for-bit, and mutated twin replay A/B must match each other bit-for-bit after the same modified input. Any mismatch writes `DesyncDetectedSignal`.
Rejected Alternatives: Distance epsilon checks hide bit drift; comparing modified replay directly to original confuses expected correction with nondeterminism.
Scalability potential: Low uses one entity rollback probe per frame; higher tiers can increase probe count without changing DTO route or authority.
Hardware Impact: Cost is isolated to offline QA; it avoids shipping a nondeterministic rollback bug that would be far more expensive than the probe.

## Decision 05: Smoke-Only Vault IDs Stay Local Instead of Editing H8Memory

Problem: SHINOBU_355 needs temporary buffers for history, rollback, telemetry, failure records, profiles, and mock desync. `H8Memory.cs` is open/dirty in the workspace and is a high-conflict core memory file.
Solution: Use existing production KCC `BufferID` lanes for state/input/SDF/faults and cast numeric smoke-only `BufferID` values `71810..71818` for editor/offline temporary lanes, documented in the route card.
Rejected Alternatives: Editing `H8Memory.cs` for enum entries would create a needless merge conflict and widen the blast radius.
Scalability potential: Low keeps 100 phantoms and 1,000,000 history slots; High/Ultra can reserve a formal BufferID range later after integrator consolidation.
Hardware Impact: i3/MX350 avoids extra global enum churn and uses one Vault allocation pass; no runtime memory layout changes.

## Decision 06: Legacy Standalone Runner Removed From Editor Test File

Problem: `HeadlessKccSmokeTests.cs` contained a previous standalone runner with private `new NativeArray` allocations and duplicate jobs. Leaving it would undermine the partial-class integration mandate.
Solution: Replace the file with a facade-only NUnit/window/gizmo layer that calls `Shinobu355KccSmokeRunner`, which stages simulation buffers through `GlobalDataVault` and uses the partial `HydrodynamicKccRuntime` jobs.
Rejected Alternatives: Keeping the old runner as "unused" would still compile duplicate QA logic and preserve invalid source patterns.
Scalability potential: All device tiers run the same fuzzer kernel; editor presentation can scale independently from QA math.
Hardware Impact: Headless CI avoids legacy NativeArray runner setup and any scene/object path; expected saving is dominated by deleting Unity scene/physics bootstrap, not by the facade itself.

## Decision 07: Critical Fault Fail-Fast With Deterministic History Fill

Problem: NaN or rollback desync must halt the fuzzer, but `NativeArrayOptions.UninitializedMemory` means unfinished history slots cannot be left unwritten.
Solution: After `KccSmokeFailureNonFinite` or `KccSmokeFailureRollbackDesync`, the job writes current AUP values into all remaining history slots, then breaks the physical simulation loop.
Rejected Alternatives: Continuing corrupted physics would contaminate forensic data; breaking immediately would leave uninitialized history bytes for validation jobs.
Scalability potential: Low devices stop work early on fatal math; High/Ultra keep identical behavior and can use the saved time for richer editor forensic display.
Hardware Impact: On i3/MX350 fatal failures avoid running the remaining expensive SDF/rollback steps while still keeping memory deterministic for postmortem scans.

## Decision 08: Treat Invalid SDF as a Breach, Not Safe Space

Problem: The first smoke SDF sampler returned a positive distance for out-of-volume positions. That could hide a phantom leaving the voxel cave and still report safe open space.
Solution: Add `KccSmokeInvalidSdfMeters`, validate SDF dimensions/backing length before trilinear reads, and map invalid/out-of-volume samples to `KccSmokeFailureEscape | KccSmokeFailureSdfInvalid`.
Rejected Alternatives: Trusting bootstrap-generated dimensions was too brittle; a bad future test profile could still drive the Burst job into an unsafe read or false pass.
Scalability potential: Low/Middle/High/Ultra use the same sentinel path. Higher quality can increase SDF resolution later without changing failure semantics.
Hardware Impact: i3/MX350 pays a small scalar bounds check per sample in offline QA; the trade removes undefined memory reads and false green reports.

## Decision 09: Rollback Proof Must Include Velocity and Flags

Problem: AUP-only replay comparison could miss velocity or collision flag divergence while the position happened to quantize to the same millimeter.
Solution: Apply replay collision flags during rollback resimulation and compare AUP bits, velocity bits, and flags through `ReplayStateMatches`.
Rejected Alternatives: Distance epsilon or AUP-only comparison hides deterministic drift in the rollback state ring.
Scalability potential: Probe cadence remains low and can scale by frequency later; DTO layout and authority route are unchanged.
Hardware Impact: Six extra scalar bit comparisons per sampled rollback frame; negligible on i3/MX350 compared with preventing multiplayer desync regressions.

## Decision 10: Editor Telemetry Must Remain Vault-Owned

Problem: The editor graph cached telemetry in a private persistent `NativeArray`, creating a second native owner outside `GlobalDataVault`.
Solution: Retain the editor/test `GlobalDataVault` after a run and expose telemetry through `TryReadOnlyHandle` with the generation handle. Dispose on assembly reload/editor quit or before the next run.
Rejected Alternatives: Copying telemetry into another persistent native array was faster to code but violates one-owner memory routing.
Scalability potential: Low uses one 300-entry ring; Ultra can add richer editor views by reading the same retained Vault handle without duplicating native memory.
Hardware Impact: Saves one 19.2 KB native copy per run and removes a leak-prone native allocation route on low-end editor machines.

## Decision 11: Split OOP Report From Smoke Report

Problem: The OOP scanner and smoke runner both wrote `Docs/Reports/QA_OPTIMIZATION_REPORT.json`, so whichever ran last erased the other proof artifact.
Solution: Keep smoke summary in `QA_OPTIMIZATION_REPORT.json` and move static OOP scanner proof to `QA_OPTIMIZATION_OOP_REPORT.json`, referenced by the smoke report.
Rejected Alternatives: JSON merge parsing in cold editor code would add brittle string handling and more managed churn for no runtime gain.
Scalability potential: Report sections can grow independently as QA coverage increases.
Hardware Impact: No runtime cost; avoids CI artifact races and lost evidence.

## Decision 12: One Heavy NUnit Entry Point

Problem: Two editor tests launched the same 100 phantom / 10,000 frame pass, doubling CI time and report writes.
Solution: Keep the heavy pass in `Shinobu355_KccSmoke_100Phantoms_10000Frames_NoNanEscapeRollbackDesync`; downgrade the legacy facade test to a route-constant assertion.
Rejected Alternatives: Keeping duplicate heavy tests makes failures noisy and wastes the editor job budget.
Scalability potential: Low-tier CI avoids redundant work; higher-tier machines can still run the same single authoritative pass.
Hardware Impact: Saves one full smoke pass per test sweep.

## Decision 13: Keep NoAlias and Prove Same-Typed Buffers Are Distinct

Problem: `States` and `RollbackStateRing` are both `NativeArray<KinematicStateDTO>` and marked `[NoAlias]`; Burst can only trust this if the scheduler never passes the same backing lane twice.
Solution: Add a cold runner assertion that the Vault BufferIDs for states and rollback ring differ before resolving/scheduling the job.
Rejected Alternatives: Removing `[NoAlias]` would protect against a caller bug but reduce the compiler's ability to vectorize known distinct lanes.
Scalability potential: The proof scales as more smoke buffers are formalized; a future route card can promote the local IDs into reserved BufferIDs.
Hardware Impact: One editor assertion, 0 us inside the Burst hot loop; preserves NEON/AVX aliasing proof for low-end CPUs.

## Decision 14: Editor Facade Belongs to KCC Editor Assembly, Not Tests

Problem: The UI Toolkit window, telemetry graph, failure gizmo, runner, and layout assertions lived inside `Hecton8.EditModeTests`, making the human control bridge test-assembly gated and duplicating editor responsibilities.
Solution: Move the cold facade into `Assets/_Project/Scripts/Physics/KCC/Editor/Shinobu355KccSmokeEditorFacade.cs`, keep tests as thin NUnit callers, and let `Hecton8.EditModeTests` reference `Hecton8.Physics.KCC.Editor`.
Rejected Alternatives: Reflection from editor tooling into test classes or adding menu/UI code to the NUnit files would preserve the wrong owner boundary.
Scalability potential: Low-tier CI runs only thin NUnit entrypoints; editor tooling remains available as KCC domain tooling without relying on `UNITY_INCLUDE_TESTS` ownership.
Hardware Impact: 0 us gameplay cost; avoids loading duplicated runner/UI code through the test route and removes a compile-wall trap around test-only define constraints.

## Decision 14b: Scanner Must Prove Tests Do Not Own The Editor Facade

Problem: Moving source files without extending the scanner would leave only a human claim that KCC/Kinematic tests no longer own UI Toolkit or SceneView tooling.
Solution: Extend `OOP_Test_Scanner` to count `EditorWindow`, `SceneView`, `UIElements`, `VisualElement`, `Button`, and `ProgressBar` identifiers in KCC/Kinematic test files.
Rejected Alternatives: Broad `rg` over all tests is too noisy because unrelated domains still own their own test windows; SHINOBU_355 needs scoped proof for KCC/Kinematic test ownership.
Scalability potential: Future KCC test regressions become scanner-detectable without expanding runtime dependencies.
Hardware Impact: Cold Roslyn scan only; 0 us runtime or fuzzer hot path.

## Decision 15: Editor Runner Must Not Depend on NUnit

Problem: Moving the runner to KCC editor assembly would either require a new NUnit precompiled reference there or removal of `Assert` calls.
Solution: Replace runner/layout `Assert` calls with deterministic `FatalArchitectureException` checks; tests still use NUnit only at their outer boundary.
Rejected Alternatives: Adding `nunit.framework.dll` to the KCC editor asmdef was rejected because the editor facade is production tooling, not a test framework extension.
Scalability potential: The same runner can be called by menu, CI harness, or future command-line editor automation.
Hardware Impact: 0 us hot path; exception checks execute only in cold/editor bootstrap.

## Decision 16: Editor Window Uses Scheduled Job Polling, Not Direct Blocking Run

Problem: The UI Toolkit window called the synchronous NUnit-grade runner directly. That kept the actual KCC math in Burst, but the human facade still blocked the editor main thread until the full 100 phantom / 10,000 frame pass finished.
Solution: Add `StartScheduledRun()` and `ScheduledRun`, allocate/resolve the same Vault lanes, schedule geometry/init/simulation/escape/drift jobs as a dependency chain, and let `HeadlessKccSmokeTesterWindow` poll the final `JobHandle` from `EditorApplication.update`. Finalization calls `Complete()` only after `IsCompleted`; disposal drains only during editor teardown.
Rejected Alternatives: `async Task` was rejected because AGENTS.md forbids managed task allocation for this path. Splitting the 10,000-frame kernel into hundreds of tiny jobs was rejected because it would add scheduler overhead and weaken the amortized batch-work requirement. Leaving the direct button call was rejected because it failed the human-control/background-pipeline intent.
Scalability potential: Low-tier editors keep a responsive facade while worker threads run the long pass. Middle/High/Ultra can add richer telemetry presentation without changing the KCC truth route or DTO layout.
Hardware Impact: Runtime gameplay cost remains 0 us; this is editor-only. Main-thread stall from the button path is removed except for cold allocation/scheduling and final `IsCompleted` cleanup. Unity profiler proof is still pending because project compile/import remains externally blocked.

## Decision 17: Scheduled Editor Reports Must Not Masquerade As CI GC Proof

Problem: The first scheduled editor route finalized reports with `ManagedBytesAllocated = 0`, which could be misread as the same allocation proof as the synchronous CI/NUnit runner. That is false evidence because the scheduled editor facade is a cold human tool and can include editor UI allocations outside the Burst smoke loop.
Solution: Capture current-thread allocated bytes before scheduling and report the delta after the final handle completes. Keep this as an editor evidence metric only and give the scheduled route its own evidence class: `UNITY_EDITOR_SCHEDULED_JOB_PENDING_IMPORT_PROOF`.
Rejected Alternatives: Marking scheduled editor allocations as a failure was rejected because editor UI/report allocations are outside the 10,000-frame Burst hot path. Leaving a hardcoded zero was rejected because it creates a fake proof artifact.
Scalability potential: Low-tier editors get honest diagnostics without changing runtime truth. Higher-tier editors can add richer graphs while the authoritative CI route remains the synchronous measured runner.
Hardware Impact: Two cold `GC.GetAllocatedBytesForCurrentThread()` calls in the editor path; 0 us gameplay hot-path cost.

## Decision 18: Retain Telemetry Snapshot, Not The Full Smoke Vault

Problem: Retaining the entire editor/test smoke Vault after completion kept a 128 MiB arena cap alive only to let the editor graph read 300 telemetry rows.
Solution: Copy the 300-entry `KccSmokeTelemetryEntry` ring into a dedicated 1 MiB telemetry-only `GlobalDataVault` after the job handle has completed, then dispose the bulk smoke Vault. The editor graph still reads through `TryReadOnlyHandle`; it does not own a private persistent `NativeArray`.
Rejected Alternatives: Keeping the full smoke Vault was memory-wasteful. Copying into a private persistent `NativeArray` was rejected again because it creates a second native owner outside the Vault route.
Scalability potential: Low-tier editors retain only a small proof artifact. Higher-tier tools can render richer graphs by reading the same tiny retained Vault or by explicitly requesting a larger telemetry Vault without touching gameplay DTOs.
Hardware Impact: Replaces a retained 128 MiB cap with a 1 MiB cap. Adds one cold 19.2 KB copy per smoke run; 0 us gameplay or Burst hot-path cost.

## Decision 19: Forensic Dumps Need Versioned Ordered Binary, Not Raw Ring Order

Problem: The black-box dump wrote an ASCII-ish header and then raw telemetry in modulo-array order. After ring wrap, that is not an oldest-to-newest forensic sequence, and the file does not prove entry size or version.
Solution: Write a 32-byte little-endian header with magic `H8KCC355`, version, entry count, struct size, oldest frame, newest frame, and source hash. Emit telemetry rows by rotating the circular ring based on the failure frame or full default frame count.
Rejected Alternatives: Leaving raw array order was faster but makes postmortem tools reconstruct state from implicit modulo behavior. Writing JSON was rejected because the crash artifact must stay compact and unmanaged-row compatible.
Scalability potential: Low keeps a tiny deterministic dump. High/Ultra can append additional fixed-size sections under a new version without invalidating current rows.
Hardware Impact: Cold failure IO only. No added simulation cost; postmortem parsing becomes O(entryCount) with explicit layout.

## Decision 20: Caller Proof Must Precede Unsafe Phantom Scheduling

Problem: The initialization job takes unsafe state pointers, but the editor runner scheduled the default phantom count directly. If future Vault capacity changes undersize a lane, the job could index beyond a resolved array before any managed assertion catches it.
Solution: Add `ValidateSmokeBuffers(...)` after all Vault resolves, assert every native lane length, and pass the validated phantom count into initialization, simulation, and escape verification jobs.
Rejected Alternatives: Adding per-index guards inside the Burst hot loop would protect against caller bugs but would spend branches every phantom frame. Trusting `EnsureGenerationHandle` intent alone was too weak for unsafe pointer code.
Scalability potential: Future profile counts or phantom counts can scale by changing capacity first; the runner fails fast in cold bootstrap if a lane is undersized.
Hardware Impact: Cold assertions only. Preserves branch-free inner simulation loops on i3/MX350 and ARM64.

## Decision 21: CSV Parser Must Fail Closed On Overflow And Hostile Profile Ranges

Problem: The span-based CSV parser could overflow the integer accumulator before converting to double, producing a finite corrupted coordinate.
Solution: Guard `whole * 10 + digit` before mutation, clamp exponent accumulation, reject AUP coordinates outside 250 km, reject speeds above 2000 m/s, reject non-finite or oversized input bias, and clamp speed scale to 0.01..4.
Rejected Alternatives: `double.Parse`/`float.Parse` was rejected for culture sensitivity and managed parser overhead. Silently clamping AUP coordinates was rejected because profile data should fail closed, not move phantoms into a different cave.
Scalability potential: Low-tier CI rejects broken profiles before allocating debug effort. Higher-tier fuzzing can widen constants explicitly if the test envelope changes.
Hardware Impact: Cold CSV ingest only; 0 us in the 10,000-frame Burst loop.

## Decision 22: Scheduled Teardown Must Not Re-Enter A Failed Finalizer

Problem: If scheduled editor finalization throws while writing the report, copying telemetry, or dumping files, the UI catch path calls `Dispose()`. Without a guard, `Dispose()` would call the same finalizer again and could mask the original fault or throw recursively.
Solution: Add `_finalizeAttempted` to `ScheduledRun`. `Poll()` owns the completion-window finalizer; `Dispose()` drains and disposes native memory but does not re-enter finalization once it has been attempted.
Rejected Alternatives: Swallowing finalization exceptions would hide broken proof artifacts. Retrying finalization in `Dispose()` was rejected because the failure is deterministic in most file/asset cases.
Scalability potential: Low-tier editors get predictable teardown under IO failure. Higher-tier tooling can layer retry UI outside this runner without touching the native job graph.
Hardware Impact: One cold bool branch in editor teardown; 0 us gameplay and 0 us Burst hot-path cost.

## Decision 23: Editor Failure Gizmo Must Still Obey AUP Locality

Problem: The SceneView failure gizmo stored `double3` AUP correctly but drew by casting absolute 100 km-scale coordinates directly into `Vector3`. This is editor-only, but it creates a bad precedent against the AUP precision law and can make far-origin debug evidence visually noisy.
Solution: Keep absolute `double3` failure and previous AUP as forensic truth, add `s_gizmoOriginAup`, and render only `failureAup - previousAup` / `previousAup - previousAup` local deltas through `HydrodynamicKccMath.ResolveLocalFloat3`.
Rejected Alternatives: Leaving the direct cast was rejected because "cold editor path" is not a valid excuse for violating the same proof discipline the smoke test is validating. Re-centering to SceneView pivot was rejected because the pivot is presentation state, not a deterministic KCC forensic origin.
Scalability potential: Low-tier editors draw the same simple local marker. Higher-tier editor tools can layer richer absolute-AUP labels or sector overlays without changing the KCC smoke DTOs.
Hardware Impact: Two cold `ResolveLocalFloat3` calls per SceneView repaint only when a failure is visible; 0 us in the 10,000-frame Burst simulation.

## Decision 24: Global Binary Ledger Needs The QA-Only Payload Boundary

Problem: SHINOBU_355 had a route card, status, and log proof, but the global binary payload ledger did not contain the QA-only smoke DTO/BufferID boundary. That leaves future agents no single ledger entry showing that `71810..71818` are disposable smoke lanes and not production truth.
Solution: Add `SHINOBU_355 Headless KCC Smoke Tester Boundary` to `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, including owner, evidence class, DTO sizes, Vault IDs, AUP route, Dear Lie route, and dump format.
Rejected Alternatives: Editing `H8Memory.cs` to formalize enum names was rejected again because the file is a shared compile-wall surface and the task only needs offline temporary lanes.
Scalability potential: Low/Middle/High/Ultra QA configurations can scale phantom/profile pressure using the documented local range while production save/rollback identity stays unchanged.
Hardware Impact: Documentation-only change; no runtime cost and no assembly recompilation caused by core enum churn.

## Decision 25: Do Not Create A KCC Runtime Assembly During QA Polish

Problem: The cold SHINOBU editor facade lives in the existing `Hecton8.Physics.KCC.Editor` assembly and calls runtime KCC DTOs/jobs. A naive response would be to wrap the entire KCC runtime folder in a new runtime asmdef, but `HydrodynamicKccRuntime` is a large production surface with existing editor tooling already consuming it.
Solution: Keep SHINOBU_355 inside the existing KCC editor assembly pattern, add only the missing editor assembly references required by the cold runner (`Core.Contracts`, `Unity.Jobs`, unsafe dump permission), and let the test assembly reference that existing editor facade.
Rejected Alternatives: Creating `Hecton8.Physics.KCC.Runtime.asmdef` was rejected because it would reshape the compile graph for production KCC and could surface unrelated dependency walls. Moving the runner back into NUnit files was rejected because it makes the human editor facade test-owned again.
Scalability potential: The QA/editor route remains isolated from gameplay truth and can scale test pressure without changing runtime assembly ownership.
Hardware Impact: No runtime cost. Compile-wall risk is narrower than creating a new runtime assembly; Unity import proof is still pending behind the external build gate.
