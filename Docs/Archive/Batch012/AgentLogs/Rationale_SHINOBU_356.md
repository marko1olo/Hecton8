# Rationale_SHINOBU_356

Status: POLISH PATCHED / LOOP16 UNITY IMPORT BLOCKED BY CORE MEMORY DEPENDENCY / LOOP18 CORE MEMORY ROUTE CARD RECORDED / LOOP19 SCHEDULED PROFILE SOURCE FIX PATCHED / LOOP20 SOURCE GUARDS CLEAN

## 2026-05-23 Initial Architecture Decisions

Problem: The batch prompt demands a Jacobi power fuzzer, but the repo already contains `PowerJacobiStressFuzzer`, `PowerGridJacobiStressFuzzerEditTests`, and the production `PowerVoltageSolverJob`.
Solution: Extend the existing QA fuzzer surface and keep it in `Hecton8.Power`. This follows the partial/integration mandate without inventing a second runtime owner.
Rejected Alternatives: A new `HectonPowerGridFuzzer` MonoBehaviour or standalone manager would duplicate the existing test runner and increase compile-wall risk under parallel agents.
Scalability potential: Low/Middle/High/Ultra all run the same offline 1,000-iteration math; editor visualization and reports can scale separately without changing solver truth.
Hardware Impact: i3/MX350 benefit is from flat CSR/native arrays and one headless Burst job instead of managed graph object walks. No profiler proof yet; microsecond savings are a static model only.

Problem: The SignalBus matrix has a power telemetry lane, but this fuzzer is offline QA and must not mutate live game state.
Solution: Format failure data in fuzzer DTO/report fields compatible with BrownoutSignal facts: node index/hash, AUP coordinate, failing array offset, and flags. Do not publish runtime signals.
Rejected Alternatives: Emitting `SignalBus<BrownoutSignal>` from the fuzzer would make test failure into gameplay traffic and violate the offline runner boundary.
Scalability potential: Low keeps raw dump only; Middle adds JSON/CSV report; High adds editor line graph; Ultra adds scene gizmo failure vectors.
Hardware Impact: No runtime device impact because fuzzer stays editor/CI/cold QA. Hot job remains native and Burst-compatible.

Problem: Prompt requires hostile CSR with infinite resistance and AUP double precision.
Solution: Generate CSR directly into flat arrays, represent infinite resistance as zero conductance for stable solver input, preserve explicit non-finite injection in dedicated fault fields/tests, and subtract `BaseOriginAup` in `double3` before float conversion.
Rejected Alternatives: Passing `float.PositiveInfinity` as conductance into the production solver is an invalid representation because the solver consumes conductance, not resistance; it would test bad input encoding instead of infinite resistance behavior.
Scalability potential: Same graph shape works across all devices; presentation-only editor/gizmo fidelity scales by tier.
Hardware Impact: Avoids NaN propagation and avoids managed allocation; expected low-end gain over managed List/Node fuzzers is cache locality, not claimed without profiler artifact.

Problem: Vault buffer IDs initially used SHINOBU-style values above `100000`, but `GlobalDataVault.TryResolveHandle` resolves through the flat metadata array first.
Solution: Move all fuzzer-owned buffer IDs to 35610-35629, below `MaxGenerationHandleCapacity=100000`, and keep every allocation routed through `EnsureGenerationHandle(..., UninitializedMemory)`.
Rejected Alternatives: Keeping high numeric IDs would let generation handles build through the fallback metadata map while failing during resolution. Local `new NativeArray` persistent buffers were rejected because they violate the Vault ownership mandate.
Scalability potential: Low/Middle/High/Ultra all use the same vault-owned flat arrays; `GlobalQualityWeight` is clamped as metadata but does not reduce QA node, edge, or iteration truth.
Hardware Impact: On i3/MX350 this avoids handle-resolution failure and avoids approximately 20 MB of voltage-history zero-fill. On high-end machines the saved memory bandwidth buys deeper editor visualization without changing solver truth.

Problem: A 1,000-iteration fuzzer implemented as a managed loop around `PowerVoltageSolverJob.Schedule().Complete()` would measure dispatch overhead and potentially GC, not solver stability.
Solution: Implement `EvaluateHeadlessJacobiFuzzJob` as one Burst IJob with an internal 1,000-iteration double-buffered CSR relaxation loop, residual reduction, omega variation, early convergence, rollback replay, and fatal halt.
Rejected Alternatives: 1,000 separate jobs and a managed frame loop were rejected due to scheduler overhead and harder GC proof. A managed graph solver was rejected due to cache misses.
Scalability potential: Low uses identical math and records fewer editor visuals; Middle/High/Ultra can visualize more telemetry without changing the job.
Hardware Impact: Static estimate is roughly 100,000 us saved from avoided dispatch/managed traversal in the offline run on low-end silicon. No profiler artifact yet because compile/run is blocked by CPU guard.

Problem: Remainder drift and watt imbalance can exist even when residual convergence looks stable.
Solution: `VerifyPowerConservationJob` computes generated watts, consumed demand, energy delta, and stamps the drift plus measured Burst microseconds into every fuzz telemetry entry.
Rejected Alternatives: Residual-only pass/fail and unused generated/consumed sums were rejected because they miss closed-network energy leaks.
Scalability potential: Low/Middle/High/Ultra use the same drift truth. Only report density changes by editor tooling.
Hardware Impact: One native linear pass over 5,000 nodes is cache-local and cheaper than managed aggregation; estimated low-end cost is under 5,000 us.

Problem: Failure autopsy originally dumped only one telemetry ring and only for math-corruption flags.
Solution: Dump both the 300-entry frame ring and 300-entry `JacobiFuzzTelemetryEntry` ring to `Docs/AgentLogs/Dump_SHINOBU_356.bin` on NaN, divergence, or rollback desync.
Rejected Alternatives: CSV-only failure reports and exception text were rejected because they lack final omega/residual/hash context.
Scalability potential: Low keeps raw binary only; Middle adds CSV; High and Ultra can build richer editor readers from the same dump.
Hardware Impact: Dump cost is cold failure-only. Low-end runtime impact is zero because the fuzzer is offline QA.

Problem: The shared `Docs/Reports/QA_OPTIMIZATION_REPORT.json` already contained another agent's report section.
Solution: Route the fuzzer success report to `QA_OPTIMIZATION_REPORT_SHINOBU_356.json` and make `OOP_Fuzz_Scanner` merge a `shinobu356JacobiPowerFuzzer` section into the shared JSON.
Rejected Alternatives: Overwriting the shared report would erase SHINOBU_355 evidence and create false integration history.
Scalability potential: Report merging scales by agent section; low-end devices never see this editor-only path.
Hardware Impact: Cold editor file IO only. No player or hot QA job impact.

Problem: The prompt requires profile-driven fuzz input named `jacobi_fuzz_profiles.csv`.
Solution: Add `Assets/_Project/Data/jacobi_fuzz_profiles.csv` with cyclic-loop and disconnected-island profiles, load it first, and retain the legacy `fuzzer_topology_profiles.csv` fallback.
Rejected Alternatives: Hard-coded default-only topology was rejected because CI profiles must vary graph stress without code changes.
Scalability potential: Low uses the same 5,000-node profile; Middle/High/Ultra can add heavier profiles without changing solver code.
Hardware Impact: Cold parse only through `ReadOnlySpan<byte>` and native scratch; hot path remains zero managed allocation.

## 2026-05-23 Polish Pass Decisions

Problem: The fuzzer lived inside `Hecton8.QA.Headless.asmdef` but referenced sibling Power/Physics/Thermal DTO surfaces, making compile-wall isolation impossible to prove without a broad root Power assembly migration.
Solution: Replace foreign DTO dependencies in the SHINOBU_356 fuzzer with local explicit-layout `JacobiFuzzPowerNodeDTO=32` and local AUP helper math. Keep the fuzzer namespace/API stable for the editor/test facade, but do not add direct sibling runtime asmdef references.
Rejected Alternatives: Adding a root `Hecton8.Power.Runtime.asmdef` or moving `PowerGridJacobiContracts.cs` was rejected because it changes a broad domain boundary under parallel agents. Keeping Physics/Thermal layout checks in a Power fuzzer test was rejected because those owners need their own tests.
Scalability potential: Low/Middle/High/Ultra QA behavior remains identical; the saved compile-wall surface buys faster iteration and leaves presentation-only editor visualization scalable.
Hardware Impact: Runtime gameplay impact is zero because this is offline QA. Low-end developer hardware gains from reduced assembly dependency breadth; static gain is fewer sibling assemblies invalidated by SHINOBU_356 edits.

Problem: Several fuzzer Burst jobs used `FloatMode.Fast`, which can reassociate math or change NaN behavior while the fuzzer claims rollback/desync proof.
Solution: Change every owned fuzzer Burst job to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`.
Rejected Alternatives: Keeping Fast math for generation/setup was rejected because hostile graph generation and initialization participate in the state hash and rollback comparison. Tolerance-based rollback compare was rejected because exact bit comparison is the failure target.
Scalability potential: Quality does not scale solver iterations in this fuzzer because XML Task 05 requires identical high-fidelity QA math; deterministic math protects authority proof across low ARM64 and high x86 targets.
Hardware Impact: Deterministic Burst may cost ALU throughput versus Fast mode, but this is CI/editor QA, not per-frame gameplay. The cost buys cross-platform reproducibility and prevents false rollback evidence.

Problem: Failure dump used an ASCII prefix and lengths, not a fixed ABI header, so black-box readers could not prove stride/schema without out-of-band knowledge.
Solution: Add `PowerJacobiStressDumpHeader=64` with magic/version/counts/strides/BufferID range, validate it in the editor layout guard, and write it before both 300-entry telemetry rings.
Rejected Alternatives: CSV-only report and ASCII-only dump prefix were rejected because they do not provide byte-exact forensic structure.
Scalability potential: Low uses the raw dump; Middle/High/Ultra editor tools can parse the same header for richer visualization without changing job truth.
Hardware Impact: Adds exactly 64 bytes to cold failure dumps. No runtime frame impact.

Problem: The OOP scanner claimed eradication while missing the fuzzer runtime root and scanning broad Power namespace files by context string.
Solution: Expand roots to include `Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer`, restrict context to owned fuzzer files/types, strip comments/strings before token counting, write a per-agent scanner report, and downgrade status to `PENDING_VERIFICATION_STATIC_SOURCE_ONLY`.
Rejected Alternatives: Manual shared-report overwrite was rejected because SHINOBU_355 evidence exists. A true Roslyn AST scanner was not added because the current editor assembly has no proven Roslyn dependency and adding one would increase compile-wall risk.
Scalability potential: Scanner is cold editor-only. Low hardware pays no gameplay cost; higher-end editor tooling can add AST parsing later behind its own assembly route.
Hardware Impact: Cold source scan only; manual equivalent scanned 55 files with 0 managed graph / 0 Physics / 0 GameObject hits.

Problem: Subagent audit found broader Power runtime issues (`PowerTelemetryEntry` union semantics, AUP distance precision, `IntegrateBatteryChargeJob` race, DataVault owner IDs, runtime reflection, ring dump order) outside SHINOBU_356's owned QA surface.
Solution: Record them as residual external Power-runtime risk, but do not patch production Power runtime in this pass. SHINOBU_356 removes its direct dependency on those surfaces so fuzzer evidence is not blocked by sibling ownership.
Rejected Alternatives: Fixing those production routes opportunistically was rejected because it violates the assigned domain boundary and needs owner route cards/profiler proof.
Scalability potential: Not applicable to this QA patch; future Power owner work should address those findings with dispatcher/profiler evidence.
Hardware Impact: No direct device change from this pass. The residual risks remain facts for the Power owner, not hidden by SHINOBU_356 reporting.

Problem: Re-reading the XML assignment showed Task 15 requires CSV profile limits in unmanaged Vault storage, while the fuzzer only used a cold temp parser result and value-copy config.
Solution: Add `PowerJacobiStressFuzzerBufferIds.TopologyProfile=35629`, resolve it through the local Vault, write the parsed profile row into that buffer, and pass the Vault row into graph generation.
Rejected Alternatives: Keeping the profile as a managed object or ScriptableObject was rejected. Adding a new core enum entry was rejected because a casted local BufferID preserves compile-wall isolation.
Scalability potential: Low/Middle/High/Ultra can load different profile rows without C# recompilation; quality metadata does not change QA stress truth, gameplay truth, or DTO layout.
Hardware Impact: Adds 32 bytes to the local QA vault arena and removes profile ownership ambiguity. No gameplay device impact.

Problem: Failure dump header had a `Flags` field but did not populate it, and `WriteDump` could silently skip a forensic dump if an unrelated CSV scratch buffer was unavailable.
Solution: Pass `result.FailureFlags` into `PowerJacobiStressBinaryDump.WriteDump`, set `PowerJacobiStressDumpHeader.Flags`, and remove the scratch-buffer precondition from dump writing.
Rejected Alternatives: Inferring flags from telemetry rows was rejected because the header is the first forensic contract. Keeping scratch as a dump dependency was rejected because black-box failure dumps must not depend on CSV formatting storage.
Scalability potential: Same 64-byte header supports low raw dump readers and high-end editor forensic visualizers.
Hardware Impact: Cold failure path only; the change adds no hot-path work.

Problem: Editor facade still contained an unused `MonoBehaviour` gizmo hook, which is unnecessary object-oriented Unity surface because SceneView drawing already provides the visual proof.
Solution: Delete `JacobiStressFuzzerGizmoHook` and keep the existing `SceneView.duringSceneGui` marker path.
Rejected Alternatives: Keeping a dormant component was rejected because it invites GameObject attachment and contradicts the no-runtime-debug-object route.
Scalability potential: Editor visualization remains available without scene objects; hot QA remains unchanged.
Hardware Impact: Removes an editor-only component surface. No runtime device impact.

Problem: The scanner report listed broad Power/Test roots while reporting zero hits, but a broad token-only sanity pass found one non-owned Power managed-node token and five non-owned GameObject test tokens.
Solution: Keep the owned-context scan as the SHINOBU_356 authority, but add `contextFilteredFiles`, `scopeNote`, and `ignoredNonOwned*` counters to the scanner output. The report now proves owned fuzzer/editor/test files are clean while preserving the external noise as facts.
Rejected Alternatives: Claiming all broad roots are clean was rejected because `PowerRelayNode.cs` and unrelated legacy tests exist. Expanding SHINOBU_356 scope to rewrite those files was rejected as cross-domain sabotage.
Scalability potential: Low/Middle/High/Ultra player paths are unaffected; this is cold QA evidence. Higher-end editor tooling can replace token scanning with Roslyn later behind a separate route card.
Hardware Impact: Cold source scan only. No runtime device cost.

Problem: A compile check became structurally desirable after ABI/signature changes, but the project has no `.sln`, no generated `Hecton8.QA.Headless.csproj`, and no `.csproj` includes the SHINOBU_356 fuzzer files.
Solution: Do not run broad `dotnet build`; it would compile unrelated generated projects and still not prove the owned asmdef. Record the target discovery and require Unity project regeneration or Unity test runner for a real compile/import check.
Rejected Alternatives: Building `Assembly-CSharp.csproj` or `Hecton8.Core.csproj` was rejected because neither includes `PowerGridJacobiStressFuzzer.cs`. Running rebuild under CPU 98.8% was rejected by AGENTS guard.
Scalability potential: Not gameplay-facing. Developer hardware avoids a false compile-wall burn.
Hardware Impact: Prevented a broad build on saturated CPU; no player device impact.

Problem: Subagent audit found an undefined `PowerJacobiStressFuzzerBufferIds.NodeDtos` reference in the dump header path.
Solution: Replace it with the declared `PowerJacobiStressFuzzerBufferIds.Nodes` and add layout offset guards for result/config/dump header fields that carry 8-byte AUP or forensic ABI data.
Rejected Alternatives: Leaving this as a compile-blocked unknown was rejected because it was source-visible and not dependent on Unity import.
Scalability potential: No gameplay change; the fix protects low-end CI and high-end editor forensic readers equally.
Hardware Impact: Zero runtime cost; prevents a hard compile failure.

Problem: Task 14 required an asynchronous background pipeline, but the editor facade called the synchronous `RunDefault()` and blocked the editor thread.
Solution: Add a cold `ScheduledRun` wrapper that holds Vault-resolved views while the offline job chain is pending, schedules graph generation -> injection -> init -> Jacobi fuzz -> conservation as a dependency chain, and lets the editor poll `JobHandle.IsCompleted` through `EditorApplication.update`.
Rejected Alternatives: Keeping modal synchronous progress was rejected. Moving the fuzzer into a runtime manager was rejected because this is offline QA and would create authority confusion.
Scalability potential: Low developer hardware keeps the editor responsive; high-end machines still run the full 1,000-iteration math and can visualize richer telemetry.
Hardware Impact: Avoids editor main-thread stalls during the scheduled chain. The wrapper stores views only; memory remains owned by the local Vault and is disposed after completion.

Problem: Task 17 required AST scanning, but the previous scanner was token-only and the report said so.
Solution: Add Roslyn precompiled references to `Hecton8.QA.Headless.Editor.asmdef` and upgrade `OOP_Fuzz_Scanner` to a `CSharpSyntaxTree` AST-primary scanner with token fallback only on parse failure.
Rejected Alternatives: Continuing to mark token evidence as enough was rejected. Adding a runtime Roslyn dependency was rejected; this remains editor-only.
Scalability potential: Cold editor/CI only. No player path cost.
Hardware Impact: Cold scan CPU cost increases modestly, but prevents false source-evidence claims.

Problem: The fuzzer injected hostile non-finite DTO fields but did not explicitly observe those fields in the solver loop.
Solution: `EvaluateHeadlessJacobiFuzzJob` now checks `InternalResistance`, `MaxCapacity`, and `CurrentStorage` for non-finite/overflow/negative corruption and raises `FailureFlagMathCorruption` with first-bad-node capture.
Rejected Alternatives: Removing hostile DTO injection was rejected because the assignment explicitly asks for corrupted topology/impedance stress.
Scalability potential: Same detection on all hardware. No binary quality switch.
Hardware Impact: Adds a few scalar checks per node in offline QA only.

Problem: Task 19 mentioned `NativeMinHeap`, but SHINOBU_356 owns no heap/open-set algorithm.
Solution: Mark the heap check as not applicable in status/log rather than inventing an unused heap type. Existing project heaps belong to Pathfinding/Audio/Construction/Economy owners.
Rejected Alternatives: Adding a dead heap just to satisfy a generic prompt line was rejected because it would add unused code and fake architecture.
Scalability potential: No player path impact.
Hardware Impact: Avoids dead code and compile-wall expansion.

Problem: `FrameCount` was reported as the requested 1,000 budget even when the solver stopped earlier on a fatal hostile input, creating a fake 1,000-frame black-box proof.
Solution: Report the actual executed Jacobi iteration count in `FrameCount` and update the edit test to assert a bounded actual count instead of exactly 1,000.
Rejected Alternatives: Adding an outer fake frame loop was rejected because the assignment stress target is the 1,000-iteration Jacobi loop, not a gameplay frame simulation.
Scalability potential: Same truth on all hardware; reporting no longer changes by quality or editor state.
Hardware Impact: Zero hot cost; removes misleading forensic metadata.

Problem: No QA-specific `SystemID` exists in the current core enum, but the fuzzer must tag Vault buffers with an owner.
Solution: Continue using `SystemID.Power` intentionally because the offline data shape is power-grid authority evidence; keep it local to a disposable Vault so it cannot pollute global runtime ownership.
Rejected Alternatives: Editing `H8Memory.cs` to add a QA owner ID was rejected as a broad core-enum change under parallel agents.
Scalability potential: No gameplay path impact.
Hardware Impact: Avoids a core compile-wall change.

Problem: Loop 12 readback found `ScheduledRun.Complete()` referenced `_allocatedBefore`, an undeclared field, creating a hard source-visible compile stop in the async editor route.
Solution: Use the local `allocatedBefore` sample already taken immediately before `_finalHandle.Complete()`. This measures the completion fence window without claiming editor polling or file IO is part of the zero-GC solver kernel.
Rejected Alternatives: Adding a persistent `_allocatedBefore` field was rejected because it would include arbitrary editor polling allocations between schedule and completion and falsely fail the fuzzer. Removing allocation measurement from scheduled completion was rejected because the completion fence still needs a cheap guard.
Scalability potential: Low developer machines avoid a compile stop and keep the editor responsive. Mid/High/Ultra machines still use the same background job chain; no gameplay truth or DTO layout changes.
Hardware Impact: Zero Burst hot-path cost. It prevents a C# import failure and avoids false managed-allocation flags from editor UI time.

Problem: The editor UI labeled scheduled timing as `solver avg us`, but the async path can only observe elapsed background-chain wall time from schedule to completion; it cannot sample exact CPU time inside an already-running Burst job.
Solution: Keep the result ABI unchanged, document the scheduled route in source, and relabel the UI as `solver/chain us`. The synchronous CI `Run()` path still records isolated solver Complete wall time and owns the performance flag.
Rejected Alternatives: Adding a new result field was rejected because `PowerJacobiStressFuzzerResult=128` is already a documented ABI and changing it would require a broader report/schema update. Faking a pure solver sample from the async wrapper was rejected.
Scalability potential: Low/Middle/High/Ultra editor presentation reports the same truth route; quality metadata is preserved but does not change authority or solver coverage.
Hardware Impact: No runtime cost. Removes misleading performance evidence from the editor facade.

Problem: The AST scanner's GameObject invocation matcher only used `.EndsWith(".Instantiate")` and `.EndsWith(".AddComponent")`, which can miss generic/member forms such as `target.AddComponent<Foo>()` depending on Roslyn expression string shape.
Solution: Match `.Instantiate` and `.AddComponent` anywhere in the invocation expression string after AST classification. Object creation detection remains AST-based through `ObjectCreationExpressionSyntax`.
Rejected Alternatives: Returning to token-only scanning was rejected because Task 17 requires AST evidence. Broadening the scanner to all Unity API calls was rejected because SHINOBU_356 only owns Power fuzzer OOP/Physics/GameObject eradication evidence.
Scalability potential: Cold editor/CI only; no player device path.
Hardware Impact: Negligible cold scan cost, stronger static evidence.

Problem: The scheduled route can only know elapsed chain wall time in `Complete()`, after `VerifyPowerConservationJob` has already stamped `JacobiFuzzTelemetryEntry.SolverMicroseconds = 0`.
Solution: Add a cold post-complete native-loop stamp that copies the final measured solver/chain microseconds and final mismatch flags into the 300-entry fuzz telemetry ring before CSV/dump/report artifacts are written.
Rejected Alternatives: Measuring Stopwatch inside Burst was rejected because it is not available and would contaminate deterministic job math. Leaving telemetry at zero was rejected because the black-box ring would disagree with the result row.
Scalability potential: Same 300-entry forensic ring on all devices; quality metadata does not reduce proof coverage.
Hardware Impact: 300 native row writes in cold editor/CI completion only. No gameplay frame cost.

Problem: Runtime and edit-test layout validation used `Marshal.OffsetOf`, which is cold but still a reflection/metadata route inside the SHINOBU_356 proof surface.
Solution: Replace it with unsafe stack-local field offset arithmetic through `UnsafeUtility.AddressOf`, matching existing core diagnostics patterns.
Rejected Alternatives: Removing offset checks was rejected because Task 18 needs an import/runtime guard. Keeping reflection was rejected because the project mandate asks for reflectionless ABI proof where feasible.
Scalability potential: Low/Middle/High/Ultra QA behavior is identical; the guard no longer depends on managed reflection metadata.
Hardware Impact: Cold validation only. It removes reflection overhead and metadata risk from the offline fuzzer entrypoint.

Problem: Hostile `float.MaxValue`, `NaN`, `-Infinity`, and demand overflow injections could be clamped before the solver recorded which raw input caused the failure.
Solution: `EvaluateHeadlessJacobiFuzzJob` now inspects raw initial potential and demand before `Sanitize01`, sets math/divergence failure flags, captures first-bad-node AUP/hash, and then clamps for containment.
Rejected Alternatives: Relying on DTO corruption checks was rejected because it did not prove every hostile input lane was observed. Letting raw infinities enter relaxation was rejected because it would poison later forensic state.
Scalability potential: The same hostile facts are recorded across weak and high-end devices; quality metadata never removes the hostile-input observation pass.
Hardware Impact: One extra scalar guard pass over 5,000 nodes in offline QA. Cost is negligible relative to the 1,000-iteration solver and prevents false-clean hostile cases.

Problem: Scheduled solver telemetry used `EdgeCapacity` as `EdgeCount`, so black-box frame rows could report capacity instead of generated CSR edge count.
Solution: Add `[ReadOnly, NoAlias] NativeArray<int> GraphCounts` to initialization and solver jobs, read `GraphCounts[1]` after the generation dependency, and use it for result and per-frame telemetry.
Rejected Alternatives: Fixing only the final result in `ScheduledRun.Complete()` was rejected because the 300-frame forensic ring would stay wrong. Main-thread readback before scheduling the solver was rejected because it would force a sync point.
Scalability potential: Same actual edge proof across all devices and editor/CI routes.
Hardware Impact: One NativeArray int read in Burst, no main-thread stall, and cleaner black-box evidence.

Problem: Scanner ownership used broad filename/text matching and could classify unrelated Power/Test files as SHINOBU_356-owned.
Solution: Replace ownership with an explicit allow-list of the runtime fuzzer, editor window, and edit test; add the QA editor scanner root, owned-file emission, fail-closed owned parse failures, and broader AST checks for MonoBehaviour/Component bases plus Unity object declarations.
Rejected Alternatives: Keeping text-token ownership was rejected because it could launder external debt into the SHINOBU_356 report. Removing Roslyn was rejected because the DLL lane exists under `Assets/Plugins/Roslyn`; Unity import proof remains pending.
Scalability potential: Cold editor/CI only. Low developer hardware can still use static guards; high-end editor sessions can run the Roslyn menu scanner for richer evidence.
Hardware Impact: Cold scan cost only. No player or hot solver path impact.

Problem: Loop 14 static guard produced raw `Physics`/`GameObject` hits, but every hit was inside the cold editor scanner's own detector literals, not in solver, graph generation, or runtime visualization code.
Solution: Treat those hits as scanner implementation literals and keep ownership evidence anchored to Roslyn AST plus explicit allow-list outputs. Reconfirmed there are no owned hot-path calls to Physics, RaycastCommand, GameObject instantiation, AddComponent, LINQ, managed graph traversal, or `Marshal.OffsetOf`.
Rejected Alternatives: Rewriting detector literals into unreadable byte arrays was rejected because it would reduce maintainability without changing hot-path behavior. Claiming a broad raw-grep zero was rejected because it would hide the scanner's intentional pattern strings.
Scalability potential: Low hardware pays no gameplay cost; the scanner remains cold editor/CI. High-end editor sessions get stronger AST evidence without affecting solver truth.
Hardware Impact: Zero Burst/runtime cost. The only measurable impact is cold source-scan time; build was still blocked by AGENTS CPU guard at 88%, so no compile claim is made.

Problem: The fuzzer resolver was re-questioned for possible generic constraint mismatch against current `IDataVault`.
Solution: Re-read `GlobalDataVault.cs`; `EnsureGenerationHandle<T>` and `TryResolveHandle<T>` both use `where T : struct`, matching `TryResolveFuzzerVaultBuffer<T>`. No code patch is required.
Rejected Alternatives: Tightening to `where T : unmanaged` locally was rejected because it would be stricter than the current core interface and could create unnecessary source churn without proving a project API problem.
Scalability potential: No device-facing change; this protects compile-wall discipline by avoiding unnecessary edits to core memory API or fuzzer call sites.
Hardware Impact: Zero runtime cost. Prevents a false-positive refactor.

Problem: Default profile execution was conflating two different proofs: 1,000-iteration hostile CSR convergence stress and deliberate raw fault forensics. Because `InjectRandomPotentialsJob` always injected `NaN`, `float.MaxValue`, and corrupt DTO lanes on frame 0, the default run could halt on input vaccination before meaningfully exercising omega damping, rollback replay, and early convergence.
Solution: Make raw non-finite potential/demand and corrupt-node DTO injection profile-gated. Default CSV rows and `CreateDefaultProfile()` use `flags=0` for convergence stress over cyclic/island/zero-conductance CSR topology. Explicit fault profile/test uses `ProfileFlagForensicFaults=3` to keep the black-box failure route covered.
Rejected Alternatives: Removing raw fault injection was rejected because Task 08 requires forensic NaN/divergence detection. Keeping raw faults always on was rejected because it undercut Tasks 07, 10, and 11 by ending the default run before the solver stress window.
Scalability potential: Low/Middle/High/Ultra still run the same QA math for the selected profile; quality weight does not change node count, iteration cap, DTO layout, save identity, or authority route. Designers can choose convergence or forensic profile through CSV/flags without C# recompilation.
Hardware Impact: Stable default runs now spend CPU on the intended Jacobi stress window instead of a cheap immediate halt. That is deliberate QA cost, not gameplay cost. Fault profile still halts early for quick black-box route verification.

Problem: Unity EditMode verification became legally runnable after the build gate opened, but the project import stops in Core/Memory before SHINOBU_356 assemblies compile.
Solution: Launched Unity 6000.4.1f1 batchmode with filter `PowerGridJacobiStressFuzzerEditTests`, captured the log at `Logs/SHINOBU_356_EditMode_20260523_094102.log`, and classified the failure as a dependency wall: `Assets/_Project/Scripts/Core/Memory/H8Memory.cs(2986,13)` and `(3003,17)` reference missing `Hecton8.Core.DispatcherJobFence`.
Rejected Alternatives: Editing `H8Memory.cs` was rejected because it is a massive shared Core/Memory surface with unrelated working-tree changes and no SHINOBU_356 mandate. Running broad `dotnet build` was rejected because no generated QA csproj includes the fuzzer and Unity already proved import is blocked earlier in the graph.
Scalability potential: No player-device behavior changes. Once the Core dependency is repaired by its owner, the same Unity filter can verify low/mid/high/ultra QA math without changing fuzzer DTO layout or quality authority.
Hardware Impact: The Unity attempt consumed editor import time but produced concrete blocker evidence. No SHINOBU_356 runtime or Burst hot-path code changed.

Problem: The scanner reports still stated `UNITY_MENU_RUN_PENDING` after a real Unity import attempt had been made, and a naive brace counter flagged the editor file because it counted JSON braces inside string literals.
Solution: Re-ran source-only audits with comment/string stripping for brace balance, preprocessor depth, forbidden hot-path tokens, and unsafe route scans; then updated both SHINOBU_356 QA scanner JSON sections to `STATIC_SOURCE_ROSLYN_AST_IMPLEMENTED_UNITY_IMPORT_BLOCKED`.
Rejected Alternatives: Treating the failed standalone PowerShell Roslyn parse as proof was rejected because the local Roslyn load fails on missing `System.Memory, Version=4.0.1.2`. Adding a new parser dependency was rejected because Unity already has the authoritative editor import route.
Scalability potential: No gameplay truth or QA math changes. The audit only tightens evidence classification so low-end and high-end verification paths remain identical once Core/Memory import is repaired.
Hardware Impact: Source-only scans are cold and cheap. No Burst hot-path changes or extra frame cost.

Problem: Unity import is blocked by `H8Memory.cs` referencing `Hecton8.Core.DispatcherJobFence` from inside `Hecton8.Core.Memory`, but SHINOBU_356 is an Echelon 9 offline QA fuzzer and does not own Echelon 1 Core/Memory.
Solution: Ran read-only local and subagent asmdef audits, then recorded `Docs/ARCHITECTURE/SHINOBU_356_CORE_MEMORY_DISPATCHER_FENCE_BLOCKER_ROUTE_CARD.md`. The proposed Core-owner fix is to replace the two forced teardown calls with a Core.Memory-local forced `JobHandle` teardown, because both calls already use `forceComplete: true` and ignore the return value.
Rejected Alternatives: Adding `Hecton8.Core` to `Hecton8.Core.Memory.asmdef` was rejected as a cycle because `Hecton8.Core.asmdef` already references `Hecton8.Core.Memory`. Moving `DispatcherJobFence` to Contracts was rejected as too broad for this blocker because it contains mutable runtime state. Patching `H8Memory.cs` under SHINOBU_356 was rejected as cross-domain Core ownership violation.
Scalability potential: No player-device behavior changes. The Core fix only unblocks Unity import and preserves the existing cold teardown semantics; the SHINOBU_356 fuzzer still scales presentation/reporting without changing solver truth.
Hardware Impact: Avoids a broad rebuild and avoids a cyclic asmdef route. No runtime Burst hot-path code changes.

Problem: Source readback found `ScheduledRun.TryAllocateAndSchedule` used `activeProfile.Flags`, but `activeProfile` is scoped only inside the synchronous `Run` method.
Solution: Change the scheduled path to read `ProfileFlags = _profileBuffer[0].Flags` after writing the parsed profile row into the Vault-backed profile buffer.
Rejected Alternatives: Adding a second local `activeProfile` value-copy was rejected because the scheduled path already uses `_profileBuffer[0]` as the profile authority for graph generation. Falling back to `profile.Flags` was rejected because it bypasses the explicit Vault row proof added for Task 15.
Scalability potential: No solver truth changes; async and sync routes now consume equivalent profile flags across low/middle/high/ultra editor/CI runs.
Hardware Impact: Zero hot Burst cost. Prevents a C# import failure in the async editor facade.

Problem: A naive regex brace guard still reported an editor-file imbalance after the source fix, risking another false blocker.
Solution: Re-ran a comment/string-aware brace/preprocessor guard over owned runtime, editor, and edit-test files, then re-ran JSON/asmdef parse, deterministic Burst attribute count, stale-symbol scan, forbidden hot-token scan, and narrow `git diff --check`.
Rejected Alternatives: Treating the naive regex result as a real syntax failure was rejected after a stateful scanner returned zero brace depth. Relaunching Unity was rejected because the Core/Memory dependency is unchanged and would stop before SHINOBU_356 again.
Scalability potential: No solver or presentation behavior changed. This is cold evidence hygiene for low-end and high-end verification routes.
Hardware Impact: Source-only guards are cold and cheap. No player runtime or Burst hot-path change.
