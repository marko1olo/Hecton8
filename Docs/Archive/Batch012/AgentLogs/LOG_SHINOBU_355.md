# LOG_SHINOBU_355

## 2026-05-23 Headless KCC Smoke Tester

What was wrong:
- Existing editor KCC smoke file contained a standalone runner and duplicate jobs instead of routing through the KCC authority class.
- Legacy editor path preserved private native allocations and did not provide rollback/desync mock signal formatting.
- No SHINOBU_355 route card, status checklist, rationale, or static QA optimization artifact existed.

What was done:
- Converted `HydrodynamicKccRuntime` to partial and added `HectonKccRuntime_SmokeTest.cs`.
- Added explicit DTOs:
  - `KccSmokeTestStateDTO`: 32 B, offsets 0/24/28, 8 B alignment.
  - `KccSmokeProfileDTO`: 64 B.
  - `KccSmokeVoxelSdfInfoDTO`: 64 B.
  - `KccSmokeTestResultDTO`: 128 B.
  - `KccSmokeFailureRecordDTO`: 128 B.
  - `KccSmokeTelemetryEntry`: 64 B.
  - `KccSmokeDriftProbeDTO`: 64 B.
- Added Burst jobs:
  - `GenerateMockTestGeometryJob`.
  - `InitializeSmokePhantomsJob`.
  - `EvaluateHeadlessKccFrameLoopJob`.
  - `VerifyCollisionEscapeJob`.
  - `AnalyzePrecisionDriftJob`.
- Added `Shinobu355KccSmokeRunner` NUnit/editor runner using `GlobalDataVault` and `NativeArrayOptions.UninitializedMemory`.
- Replaced legacy `HeadlessKccSmokeTests.cs` with facade-only tests, UI Toolkit window, telemetry graph, and SceneView failure gizmo.
- Added Roslyn `OOP_Test_Scanner` and `KccSmokeLayoutGuard`.
- Added artifacts:
  - `Docs/Tasks/Status_SHINOBU_355.md`
  - `Docs/AgentLogs/Rationale_SHINOBU_355.md`
  - `Docs/ARCHITECTURE/SHINOBU_355_HEADLESS_KCC_SMOKE_ROUTE_CARD.md`
  - `Docs/Reports/QA_OPTIMIZATION_REPORT.json`

Cinematic cheats used:
- Voxel cave stress is a synthetic SDF shell/wedge/crevice/pillar field, not authored cave geometry.
- Hydrodynamic/current stress uses deterministic sine-polynomial hostile currents, not real fluid simulation.
- Editor graph is a direct 300-entry penetration-depth telemetry line, not a sampled scene replay.
- SceneView failure proof draws Handles only: green trajectory segment, red breach marker, yellow velocity arrow, cyan input vector.

Exact microseconds saved / measured state:
- No profiler measurement was produced; compile/import proof is blocked.
- Estimated hot gameplay cost: 0 us because the fuzzer is offline QA only.
- Removed scene/GameObject/Unity Physics smoke route: expected saving is the entire scene/physics bootstrap path on headless CI.
- Avoided zeroing 1,000,000 `double3` history slots: ~24 MB clear skipped per run.
- Telemetry dump footprint: 300 * 64 B = 19,200 B.

Verification:
- Assignment extraction: `CURRENT_BATCH.md` SHINOBU_355 block found; task count = 20.
- Static forbidden-pattern grep over KCC test scope returned 0 hits for:
  - `new GameObject`
  - `GameObject.Instantiate`
  - `Instantiate(`
  - `Physics.Simulate`
  - `List<Vector3>`
  - `float.Parse`
  - `double.Parse`
  - `UnsafeUtility.MemClear`
- Brace balance true for:
  - `Assets/_Project/Scripts/Physics/KCC/HectonKccRuntime_SmokeTest.cs`
  - `Assets/_Project/Tests/Editor/Shinobu355KccSmokeEditTests.cs`
  - `Assets/_Project/Tests/Editor/HeadlessKccSmokeTests.cs`
  - `Assets/_Project/Scripts/Physics/KCC/Editor/KccSmokeArchitectureValidators.cs`
- Compile attempt:
  - Command: `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false`
  - Result: blocked before SHINOBU_355 diagnostics by `Hecton8.Habitat` missing in Construction files.
  - Current CPU guard sample: 97.3%; no repeat rebuild launched.

<SELF_AUDIT>
  <TASK_CHECK>
    <TASK id="01" status="PASS"/>
    <TASK id="02" status="PASS"/>
    <TASK id="03" status="PASS"/>
    <TASK id="04" status="PASS"/>
    <TASK id="05" status="PASS"/>
    <TASK id="06" status="PASS"/>
    <TASK id="07" status="PASS"/>
    <TASK id="08" status="PASS"/>
    <TASK id="09" status="PASS"/>
    <TASK id="10" status="PASS"/>
    <TASK id="11" status="PASS"/>
    <TASK id="12" status="PASS"/>
    <TASK id="13" status="PASS"/>
    <TASK id="14" status="PASS"/>
    <TASK id="15" status="PASS"/>
    <TASK id="16" status="PASS"/>
    <TASK id="17" status="PASS"/>
    <TASK id="18" status="PASS"/>
    <TASK id="19" status="PASS"/>
    <TASK id="20" status="PASS_STATIC_COMPILE_BLOCKED_EXTERNAL"/>
  </TASK_CHECK>
  <ARM64_CHECK>
    KccSmokeTestStateDTO Size=32 Align=8 FieldOffsets: TestPlayerAUP=0 CurrentFrameCount=24 MismatchFlags=28.
  </ARM64_CHECK>
  <ZERO_GC_CHECK>
    10000-frame simulation, hostile input generation, SDF sampling, rollback probe, telemetry ring, failure recording, and history tracking use NativeArray DTOs. Managed file IO and editor UI are cold paths only.
  </ZERO_GC_CHECK>
  <AUP_CHECK>
    State truth stays double3 AUP. SDF/local float math subtracts double3 origin before float cast. Rebase stress shifts local origin only, not authoritative AUP.
  </AUP_CHECK>
  <VAULT_IDS>
    Existing KCC lanes: ShinobuHydroKccStates, ShinobuHydroKccInputs, ShinobuHydroKccProposedVelocities, ShinobuHydroKccFaultFlags, ShinobuKccEnvironmentSdf. Smoke lanes: 71810..71818.
  </VAULT_IDS>
  <COMPILE_STATE>
    STATIC_SOURCE_WIRED. Full compile blocked by external Construction/Hecton8.Habitat dependency before SHINOBU_355 diagnostics.
  </COMPILE_STATE>
</SELF_AUDIT>

---

## 2026-05-23 Polish Loop 11 - AUP Debug And Ledger Proof

What was wrong:
- The cold SceneView failure gizmo cast absolute `double3` AUP directly into `Vector3`.
- The global binary payload ledger did not yet contain the SHINOBU_355 QA-only payload boundary.

What was done:
- Added `s_gizmoOriginAup` and localized failure/previous AUP through `HydrodynamicKccMath.ResolveLocalFloat3` before debug drawing.
- Added the SHINOBU_355 boundary to `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- Updated the SHINOBU_355 route card, status file, and rationale.

Cinematic Cheats used:
- No scene physics route was added. Failure presentation remains `SceneView` handles over local deltas; KCC proof remains synthetic SDF math.

Exact microseconds saved / measured state:
- Runtime/Burst simulation delta: 0 us.
- Editor-only cost: two local AUP conversions per SceneView repaint only when a failure marker is visible.
- Rebuild not launched; previous external `Hecton8.Habitat` compile wall remains the last compiler evidence.

Verification:
- Re-extracted the current SHINOBU_355 prompt block by CLI: `prompt_chars=23818`, `task_lines=20`.
- Scoped forbidden-token grep over SHINOBU-owned runtime/editor/test files returned no hits for GameObject/Instantiate/Unity Physics/List<Vector3>/parser/Random/Allocator.Persistent/async/coroutine/foreach patterns.
- Brace balance is 0 for five touched C# files.
- JSON parse passed for `QA_OPTIMIZATION_REPORT.json` and `QA_OPTIMIZATION_OOP_REPORT.json`.
- AUP gizmo direct-cast scan found no `(float)s_failureAup` or `(float)s_previousAup` route; `s_gizmoOriginAup` and `ResolveLocalFloat3` are present.
- `git diff --check` exited 0 with CRLF warnings only.
- Build guard sampled CPU at `63.96%`; compiler processes were absent, but rebuild remains blocked by the >50% CPU rule.
- KCC editor asmdef route inspected: the existing `HydrodynamicKccTunerWindow` already lives under `Hecton8.Physics.KCC.Editor` and consumes KCC runtime DTOs. SHINOBU_355 did not create a new KCC runtime asmdef or add a sibling runtime reference.
- No-build Roslyn syntax proof attempted and classified unavailable: local Roslyn DLL loading failed with `ReflectionTypeLoadException` / `Roslyn.Utilities.StringTable` initializer failure before parser diagnostics. This is not recorded as a C# source syntax failure. Current valid source checks remain brace balance, grep, JSON parse, direct-cast scan, and `git diff --check`.
- Later build guard sampled CPU at `96.97%`; compiler processes were absent, but rebuild remains blocked by the >50% CPU rule.

## 2026-05-23 Polish Loop 9 - Evidence Artifact Honesty

What was wrong:
- The scheduled editor route reported `ManagedBytesAllocated = 0`, which looked like a measured CI hot-path GC proof but was only a placeholder inside the editor facade path.

What was done:
- Added current-thread allocation sampling around the scheduled editor job window.
- Split report evidence class between the synchronous CI/NUnit route and the scheduled editor route.
- Kept scheduled editor allocation delta informational; only the synchronous CI route still marks allocation deltas as a smoke failure.

Cinematic Cheats used:
- No change to the Dear Lie route: synthetic SDF and direct Burst KCC math remain the replacement for GameObjects, Unity Physics scenes, and raycasts.

Exact microseconds saved / measured state:
- Runtime gameplay delta: 0 us.
- Editor scheduled route adds two cold scalar allocation-counter reads.
- Evidence correctness improved: no false 0 B allocation claim from the UI route.

Verification:
- `Shinobu355KccSmokeEditorFacade.cs` brace balance remains 0.
- `git diff --check` for the facade returned clean.

<SELF_AUDIT>
  <EVIDENCE_ROUTE>
    <SYNC_CI evidence_class="UNITY_EDITOR_JOB_RUN_PENDING_EXTERNAL_COMPILE_WALL">Measures `GC.GetAllocatedBytesForCurrentThread()` around the synchronous smoke run and sets allocation failure flag when nonzero.</SYNC_CI>
    <SCHEDULED_EDITOR evidence_class="UNITY_EDITOR_SCHEDULED_JOB_PENDING_IMPORT_PROOF">Reports current-thread allocation delta as editor-facade information only; does not claim hot-path GC proof.</SCHEDULED_EDITOR>
  </EVIDENCE_ROUTE>
  <HOT_PATH>Status unchanged: Burst jobs own the 10,000-frame loop and remain NativeArray/Vault backed.</HOT_PATH>
</SELF_AUDIT>

---

## 2026-05-23 Polish Loop 8 - Scheduled Editor Pipeline

What was wrong:
- The UI Toolkit run button still called the synchronous runner directly, so the editor facade blocked during the full smoke pass even though the heavy math was Burst-backed.
- The route card described the retained telemetry Vault but did not name the scheduled editor completion window.

What was done:
- Added `Shinobu355KccSmokeRunner.StartScheduledRun()` and `ScheduledRun`.
- Scheduled `GenerateMockTestGeometryJob`, `InitializeSmokePhantomsJob`, `EvaluateHeadlessKccFrameLoopJob`, `VerifyCollisionEscapeJob`, and `AnalyzePrecisionDriftJob` as one dependency graph.
- Changed `HeadlessKccSmokeTesterWindow` to poll the scheduled run from `EditorApplication.update`, disable the run button while active, and finalize reports/gizmo only after the final handle is complete.
- Kept the synchronous NUnit runner for CI and warmup measurement; this is a deliberate blocking test harness route, not the human editor route.
- Updated route card, rationale, and status.

Cinematic Cheats used:
- No GameObject runner, no Unity Physics scene, no coroutine, no `async Task`.
- The editor remains a facade over the same synthetic SDF and deterministic KCC Burst kernel.

Exact microseconds saved / measured state:
- Gameplay hot-path delta: 0 us.
- Editor main-thread stall from the window button is reduced to cold setup/scheduling and finalization; the 100 * 10,000 simulation pass runs behind a polled `JobHandle`.
- Exact profiler proof remains pending Unity import because compile is still blocked by external `Hecton8.Habitat` dependency.

Verification:
- Brace balance: 0 for `Shinobu355KccSmokeEditorFacade.cs`.
- `git diff --check` for the edited facade returned clean.
- Scoped forbidden-pattern scan returned no SHINOBU_355 hits for `new GameObject`, `Instantiate(`, `Physics.Simulate`, `List<Vector3>`, parser `Parse`, `UnsafeUtility.MemClear`, `Allocator.Persistent`, private telemetry cache, or `foreach`.

<SELF_AUDIT>
  <TASK_RECONCILIATION>
    <TASK id="15" status="PASS_PLUS">Editor facade now starts a scheduled Burst pipeline and polls `JobHandle.IsCompleted` instead of blocking the UI for the entire smoke pass.</TASK>
    <TASK id="20" status="PASS_STATIC_COMPILE_BLOCKED_EXTERNAL">Status, rationale, route card, and log updated after polish loop 8. No rebuild launched because the previous compile wall is outside SHINOBU_355.</TASK>
  </TASK_RECONCILIATION>
  <DEPENDENCY_GRAPH>
    SDF generation and phantom initialization are scheduled independently, joined with `JobHandle.CombineDependencies`, then fed into simulation, escape verification, and drift analysis. The editor window finalizes only after the final handle reports complete.
  </DEPENDENCY_GRAPH>
  <H_PHI_VAULT_STATUS>
    Scheduled run uses the same Vault lanes as the CI runner and retains only the telemetry Vault handle for the graph after completion.
  </H_PHI_VAULT_STATUS>
</SELF_AUDIT>

---

## 2026-05-23 Polish Loop 7 - Editor Facade Extracted From Tests

What was wrong:
- `HeadlessKccSmokeTests.cs` still owned UI Toolkit window code, SceneView gizmo code, layout assertions, and the editor telemetry graph.
- `Shinobu355KccSmokeEditTests.cs` owned the cold smoke runner and report/dump code, so production editor tooling was gated by the test assembly.
- The runner used NUnit `Assert` calls internally, which would force a test-framework dependency if moved to the KCC editor assembly.

What was done:
- Added `Assets/_Project/Scripts/Physics/KCC/Editor/Shinobu355KccSmokeEditorFacade.cs`.
- Moved `Shinobu355KccSmokeRunner`, `Shinobu355KccSmokeSummary`, `HeadlessKccLayoutAssertions`, `HeadlessKccSmokeTesterWindow`, `HeadlessKccSmokeTelemetryGraphElement`, and `HeadlessKccFailureGizmo` into `Hecton8.Physics.KCC.Editor`.
- Replaced runner/layout `Assert` calls with `FatalArchitectureException` checks.
- Reduced `Shinobu355KccSmokeEditTests.cs` to the single heavy NUnit caller.
- Reduced `HeadlessKccSmokeTests.cs` to static NUnit checks only.
- Updated `Hecton8.Physics.KCC.Editor.asmdef` to own the unsafe black-box dump path and `Unity.Jobs`.
- Added `Hecton8.Physics.KCC.Editor` as an explicit edit-mode test dependency.

Cinematic Cheats used:
- No new presentation simulation was introduced. The editor facade still draws direct `Handles` from the Vault-backed failure record and a fixed 300-row telemetry graph.

Exact microseconds saved / measured state:
- 0 us gameplay cost; all changes are editor/test assembly routing.
- Removes duplicate test-assembly ownership of editor tooling and prevents `UNITY_INCLUDE_TESTS` from controlling the KCC smoke UI path.
- No profiler proof added; no rebuild launched in this loop.

Verification:
- Brace balance: 0 for `Shinobu355KccSmokeEditorFacade.cs`, `KccSmokeArchitectureValidators.cs`, `HectonKccRuntime_SmokeTest.cs`, and both slim NUnit files.
- Tests now contain no `EditorWindow`, `UIElements`, `SceneView`, `Handles`, telemetry cache, or runner implementation.
- KCC editor assembly contains no `NUnit` or `Assert.` references.
- Forbidden-pattern grep in SHINOBU_355 scope returned 0 hits for `new GameObject`, `Instantiate(`, `Physics.Simulate`, `List<Vector3>`, parser `Parse`, `UnsafeUtility.MemClear`, `Allocator.Persistent`, private telemetry cache, and `foreach`.

<SELF_AUDIT>
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Original XML re-extracted; task count remains 20.</TASK>
    <TASK id="02" status="PASS">Runtime jobs remain in partial `HydrodynamicKccRuntime`; editor facade moved to KCC editor assembly.</TASK>
    <TASK id="03" status="PASS">Mock desync lane remains Vault-backed.</TASK>
    <TASK id="04" status="PASS">No GameObject/Unity Physics actor route was introduced.</TASK>
    <TASK id="05" status="PASS">Position history remains Vault `NativeArray<double3>`.</TASK>
    <TASK id="06" status="PASS">Synthetic SDF generator remains Burst job.</TASK>
    <TASK id="07" status="PASS">10,000-frame Burst job unchanged.</TASK>
    <TASK id="08" status="PASS">NaN/SDF invalid guards unchanged.</TASK>
    <TASK id="09" status="PASS">Escape verifier unchanged.</TASK>
    <TASK id="10" status="PASS">Precision drift verifier unchanged.</TASK>
    <TASK id="11" status="PASS">AUP rebase stress unchanged.</TASK>
    <TASK id="12" status="PASS">Rollback full-state compare unchanged.</TASK>
    <TASK id="13" status="PASS">Uninitialized Vault lanes unchanged.</TASK>
    <TASK id="14" status="PASS">Telemetry ring and raw dump now owned by KCC editor facade.</TASK>
    <TASK id="15" status="PASS">UI Toolkit window is in `Hecton8.Physics.KCC.Editor`, not tests.</TASK>
    <TASK id="16" status="PASS">CSV parser moved with the editor runner; still `ReadOnlySpan<byte>` based.</TASK>
    <TASK id="17" status="PASS">SceneView gizmo is in KCC editor assembly; no GameObject debug actor.</TASK>
    <TASK id="18" status="PASS">OOP scanner remains KCC editor tool and now reports tests as thin callers.</TASK>
    <TASK id="19" status="PASS">Layout assertions are KCC editor fail-fast checks.</TASK>
    <TASK id="20" status="PASS_STATIC_COMPILE_BLOCKED_EXTERNAL">Disk logs updated; compile not rerun due known external wall.</TASK>
  </TASK_RECONCILIATION>
  <ASSEMBLY_ROUTE>
    Runtime: existing core/KCC runtime owner. Editor facade: `Hecton8.Physics.KCC.Editor`. Tests: `Hecton8.EditModeTests` thin callers. No runtime sibling dependency was added.
  </ASSEMBLY_ROUTE>
  <ZERO_GC_CHECK>
    Hot fuzzer loop remains Burst/NativeArray only. New C# strings/exceptions are confined to cold editor/report paths and NUnit outer boundary.
  </ZERO_GC_CHECK>
</SELF_AUDIT>

---

## 2026-05-23 Polish Loop 6 - Subagent Findings Applied

What was wrong:
- `ResolveProfile` trusted `ProfileCount` without clamping to `Profiles.Length`.
- `SampleSdfStatic` trusted DTO dimensions and returned positive safe space for out-of-volume samples.
- Rollback replay compared only AUP against history, so velocity/flag desync could survive.
- Editor telemetry graph owned a private persistent native cache outside `GlobalDataVault`.
- OOP scanner and smoke runner both wrote `QA_OPTIMIZATION_REPORT.json`.
- Two NUnit methods could run the same 100 phantom / 10,000 frame smoke pass.
- CSV numeric parser accepted trailing suffix bytes.

What was done:
- Added `KccSmokeInvalidSdfMeters` and overflow-safe SDF layout validation.
- Invalid/out-of-volume SDF now flags `KccSmokeFailureEscape | KccSmokeFailureSdfInvalid`.
- Added sanitized smoke tuning/SDF info at job entry.
- `GlobalQualityWeight` now continuously lerps hostile drive from 220 to 620; default remains 1.0 for maximum CI stress.
- Rollback replay applies collision flags and compares AUP bits, velocity bits, and flags via `ReplayStateMatches`.
- Replaced private telemetry `NativeArray` cache with retained editor/test `GlobalDataVault` and `TryReadOnlyHandle`.
- Added cold BufferID inequality assertion for the same-typed `States` and `RollbackStateRing` lanes before scheduling `[NoAlias]` jobs.
- Split OOP report to `Docs/Reports/QA_OPTIMIZATION_OOP_REPORT.json`.
- Removed duplicate heavy smoke run from `HeadlessKccSmokeTests`; single heavy run remains in `Shinobu355KccSmokeEditTests`.
- CSV parser now supports exponent notation and rejects trailing bytes.

Cinematic Cheats used:
- Voxel cave is still a synthetic SDF stress field, not authored geometry or Unity Physics.
- Collision verification remains direct SDF math in Burst, not `Physics.Raycast` or `Physics.Simulate`.
- Failure visualization remains SceneView `Handles`, no spawned actors.

Exact microseconds saved / measured state:
- One duplicate smoke pass removed per editor test sweep. Exact runtime depends on Unity/Burst import; source-level saving is one full 100 * 10,000 pass.
- Removed one 19.2 KB telemetry copy per run by retaining Vault read-only handle.
- SDF validation adds scalar bounds checks per sample; accepted cost because it prevents undefined native reads and false PASS.
- Profiler/Burst Inspector measurement still pending because full compile remains blocked by external `Hecton8.Habitat` dependency.

Verification:
- Brace balance: 0 for all four touched C# files.
- Forbidden-pattern grep in SHINOBU_355 scope returned 0 hits for `new GameObject`, `Instantiate(`, `Physics.Simulate`, `List<Vector3>`, parser `Parse`, `UnsafeUtility.MemClear`, `Allocator.Persistent`, private telemetry cache, and `foreach`.
- No dotnet rebuild launched in this polish loop.

<SELF_AUDIT>
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Archaeology done with `rg` and batch XML extraction.</TASK>
    <TASK id="02" status="PASS">KCC smoke jobs live in `HydrodynamicKccRuntime` partial.</TASK>
    <TASK id="03" status="PASS">Mock desync signal uses Vault lane, no hot event bus allocation.</TASK>
    <TASK id="04" status="PASS">No GameObject/Physics scene smoke actor.</TASK>
    <TASK id="05" status="PASS">History is Vault-backed native `double3`, not `List<Vector3>`.</TASK>
    <TASK id="06" status="PASS">Synthetic SDF cave generator exists.</TASK>
    <TASK id="07" status="PASS">10,000-frame Burst `IJob` exists with deterministic float mode.</TASK>
    <TASK id="08" status="PASS">NaN/input/SDF invalid vaccination and fail-fast history fill implemented.</TASK>
    <TASK id="09" status="PASS">SDF escape verifier flags penetration and out-of-volume breach.</TASK>
    <TASK id="10" status="PASS">Precision drift job records millimeter error.</TASK>
    <TASK id="11" status="PASS">AUP rebase stress shifts local origin only.</TASK>
    <TASK id="12" status="PASS">Rollback probe compares AUP, velocity bits, and flags.</TASK>
    <TASK id="13" status="PASS">Uninitialized Vault buffers are deterministically overwritten.</TASK>
    <TASK id="14" status="PASS">300-frame telemetry ring and binary dump route exist.</TASK>
    <TASK id="15" status="PASS">Editor window/gizmo exists; telemetry is now Vault read-only.</TASK>
    <TASK id="16" status="PASS">CSV parser uses `ReadOnlySpan<byte>` and rejects malformed suffixes.</TASK>
    <TASK id="17" status="PASS">Failure gizmo uses SceneView `Handles` only.</TASK>
    <TASK id="18" status="PASS">OOP scanner writes separate proof artifact and scans `List<Vector3>`.</TASK>
    <TASK id="19" status="PASS">Explicit layout guard verifies DTO sizes/offsets.</TASK>
    <TASK id="20" status="PASS_STATIC_COMPILE_BLOCKED_EXTERNAL">Self-audit/log/status/route-card updated; build still blocked before SHINOBU_355 by external Construction dependency.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    `KccSmokeTestStateDTO`: offset 0 `double3 TestPlayerAUP` 24 B; offset 24 `uint CurrentFrameCount` 4 B; offset 28 `uint MismatchFlags` 4 B; total 32 B; align 8; no `Pack=1`.
    `KccSmokeTelemetryEntry`: total 64 B fixed cache-line-sized telemetry row; explicit layout.
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    QA truth is not degraded below the configured default; the tester runs default `GlobalQualityWeight=1.0` to maximize stress. The implemented continuous path clamps finite quality and lerps hostile drive 220..620, so lower quality can reduce ALU/trajectory violence without changing DTO layout, save identity, or authority route.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Persistent smoke lanes are Vault handles only: production KCC buffers plus smoke IDs 71810..71818. Editor graph retains the Vault and reads telemetry through `TryReadOnlyHandle`; no private persistent telemetry `NativeArray` remains.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>
    Jobs consume generated SDF/init dependencies manually in the editor runner, then schedule frame loop, collision verifier, and drift analyzer. `[NoAlias]` remains on non-overlapping NativeArrays. Caller asserts distinct BufferIDs for `States` and `RollbackStateRing` before scheduling.
  </POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    SHINOBU_355 did not add a runtime asmdef dependency. Editor scanner report is separated to avoid artifact overwrite. No rebuild was launched in polish loop because previous build hit external `Hecton8.Habitat` wall.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Heavy scene physics path: GameObjects + Unity Physics scene + raycasts/step simulation. Replaced by synthetic SDF direct sampling and deterministic polynomial currents. Complexity remains O(phantoms * frames * sweepSamples), but removes scene graph sync, collider broadphase, and managed object lifetime from the QA route.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

---

## 2026-05-23 Polish Loop 10 - Subagent Audit Closure

What was wrong:
- Scheduled editor route retained the whole 128 MiB smoke Vault for a 300-entry telemetry graph.
- Black-box dump wrote raw telemetry ring order and omitted version/entry-size proof.
- Scanner report claimed broad test scanning while it intentionally scoped to KCC/Kinematic files.
- Phantom jobs were scheduled with the default count after unsafe pointer setup without executable lane-length proof.
- CSV numeric parser could overflow the integer accumulator before returning a finite double.
- Layout assertions proved smoke DTO sizes but not enough dump/report-critical field offsets.

What was done:
- Replaced full-Vault retention with `RetainTelemetrySnapshot(...)`, a 1 MiB telemetry-only Vault copy.
- Upgraded `Dump_SHINOBU_355.bin` to `H8KCC355` v1: 32-byte little-endian header plus oldest-to-newest rotated telemetry rows.
- Added `ValidateSmokeBuffers(...)` and `RequireLength(...)` before all schedule calls using phantom count.
- Added overflow guards and profile range rejection to the span CSV parser.
- Extended executable offset assertions for profile, SDF info, result, failure, telemetry, and drift DTOs.
- Made OOP scanner evidence explicitly scoped and added unqualified `Instantiate(...)` detection.
- Added `_finalizeAttempted` so scheduled teardown drains/disposes native memory without re-entering a failed report/snapshot finalizer.
- Updated QA reports and route card with evidence class, retained-Vault size, dump format, parser guard, and schedule guard.

Cinematic Cheats used:
- No new physics route. The tester still uses direct SDF sampling and polynomial hostile currents instead of Unity Physics actors, raycasts, or scene stepping.

Exact microseconds saved / measured state:
- Retained memory cap drops from 128 MiB to 1 MiB after a run.
- Adds one cold 19.2 KB telemetry copy per run.
- Adds cold schedule/profile validation; 0 us added to the Burst simulation loop.
- Adds one cold bool branch to editor teardown; 0 us added to the Burst simulation loop.
- No rebuild launched; external compile wall and CPU/dotnet guard remain.

Verification:
- JSON parse passed for `QA_OPTIMIZATION_REPORT.json` and `QA_OPTIMIZATION_OOP_REPORT.json`.
- Brace balance is 0 for `Shinobu355KccSmokeEditorFacade.cs`, `KccSmokeArchitectureValidators.cs`, and `HectonKccRuntime_SmokeTest.cs`.
- Scoped forbidden-pattern grep returned no SHINOBU-owned hits for Unity Physics/GameObject/managed-list/parser/async/coroutine patterns.

<SELF_AUDIT>
  <TASK_RECONCILIATION>
    <TASK id="14" status="PASS_PLUS">Telemetry ring is still 300 frames; retained editor proof now lives in a tiny telemetry-only Vault.</TASK>
    <TASK id="16" status="PASS_PLUS">CSV profile ingest now rejects numeric overflow and out-of-range hostile profiles before DTO write.</TASK>
    <TASK id="18" status="PASS_PLUS">Scanner evidence now states KCC/Kinematic scope and catches unqualified Instantiate calls.</TASK>
    <TASK id="19" status="PASS_PLUS">Layout proof expanded beyond size checks to critical smoke DTO field offsets.</TASK>
    <TASK id="20" status="PASS_STATIC_COMPILE_BLOCKED_EXTERNAL">Subagent findings patched and logged; compile still not rerun because prior wall is external and current CPU/dotnet guard blocks rebuild.</TASK>
  </TASK_RECONCILIATION>
  <H_PHI_VAULT_STATUS>
    Bulk smoke Vault is disposed after completion. Retained proof artifact is `SmokeTelemetryBuffer` in a 1 MiB telemetry-only `GlobalDataVault`; no private persistent `NativeArray`.
  </H_PHI_VAULT_STATUS>
  <BLACKBOX_FORMAT>
    Header: bytes 0..7 magic `H8KCC355`; 8..11 version; 12..15 entry count; 16..19 entry size; 20..23 oldest frame; 24..27 newest frame; 28..31 source hash. Body: fixed 64-byte telemetry rows in oldest-to-newest order.
  </BLACKBOX_FORMAT>
  <DEPENDENCY_AND_ALIASING>
    `ValidateSmokeBuffers` proves lane lengths before scheduling. Inner Burst loop remains branch-light and `[NoAlias]` fields remain valid under cold caller assertions.
  </DEPENDENCY_AND_ALIASING>
  <TEARDOWN_GUARD>
    `_finalizeAttempted` prevents duplicate finalizer execution after an editor report/dump exception; native Vault disposal still runs.
  </TEARDOWN_GUARD>
</SELF_AUDIT>
