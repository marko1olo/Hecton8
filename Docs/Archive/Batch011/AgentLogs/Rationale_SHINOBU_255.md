# Rationale_SHINOBU_255

## Decision 0: Harness Boundary

Problem: Relaxation solver stability must be tested without Unity scenes, GameObjects, or manual QA graph construction.
Solution: Build a headless NUnit/Burst harness around unmanaged CSR buffers and explicit PRE_SIMULATION/SIMULATION/POST_SIMULATION job phases.
Rejected Alternatives: Scene-authored stress prefabs and MonoBehaviour-driven graph discovery were rejected because they test the editor graph path, not solver math stability, and add uncontrolled managed allocations.
Scalability potential: Low uses deterministic finite checks and capped node counts; Middle expands topology profiles; High retains richer telemetry; Ultra runs maximum iteration/fidelity with full failure export.
Hardware Impact: Expected low-end i3/MX350 gain is deterministic solver stress without editor scene overhead; target is no managed allocation inside the 1,000-frame loop and solver average threshold flag at 200 us.

## Decision 1: Mandatory Registry Read Set

Problem: The fuzzer touches logistics math, native memory, ARM64 layout, AUP spatial values, and post-mortem telemetry.
Solution: Use the following mandates as active constraints: LOGI_Energy_Networks_Power_Grid_Graph_Flow, OPT_Zero_GC_Policy_AllocFree_Mandate, DATA_Runtime_Struct_Layout_ARM64, OPT_Native_Memory_Collections_JobSystem_Protocol, MATH_AUP_Determinism_Sync, DBG_Telemetry_Crash_Reporting_PostMortem.
Rejected Alternatives: Reading unrelated AI/render/audio mandates was rejected because the assignment is headless solver fuzzing and mandate noise increases cross-domain risk.
Scalability potential: Mandate set covers Low/Middle/High/Ultra math scale without widening ownership into rendering or gameplay systems.
Hardware Impact: Source-level design is constrained toward flat native arrays and cache-linear CSR traversal for i3/MX350 viability.

## Decision 2: Reuse Existing PowerVoltageSolverJob

Problem: The prompt demands solver destruction testing, not a new production solver that could drift from the actual power math.
Solution: The fuzzer drives existing `PowerVoltageSolverJob` and `IntegrateBatteryChargeJob` over hostile CSR buffers, using injected `NativeArray` data instead of scene-built graphs.
Rejected Alternatives: Writing a separate Jacobi implementation was rejected because it could pass while production math fails. Refactoring `PowerGridManager` public API was rejected because the existing DataVault-backed job route already decouples math from scene discovery.
Scalability potential: Low can reduce node/profile counts outside the strict CI test; Middle can parse authored topology profiles; High/Ultra keep 5,000+ nodes and 8 iterations at `GlobalQualityWeight=1.0`.
Hardware Impact: Flat CSR and reused Burst jobs keep the validation surface representative of i3/MX350 cache behavior while avoiding MonoBehaviour traversal overhead.

## Decision 3: Explicit Source/Drain Energy Accounting

Problem: The hostile fuzzer injects source nodes and demand rates, so raw potential sums are not expected to be conserved as a closed thermodynamic system.
Solution: The validator records initial/final energy delta but only flags `ThermodynamicFailure` when `ExplicitGenerationDrainPresent == 0`.
Rejected Alternatives: Always failing on potential-sum drift was rejected because source/demand is the point of a power grid. Ignoring energy entirely was rejected because closed-profile fuzzing still needs a leak detector.
Scalability potential: Low/Middle/High/Ultra can reuse the same DTO flag and switch to closed-system profiles without changing result layout.
Hardware Impact: The check is a linear validation sweep already needed for residual/NaN detection; no extra graph pass is added.

## Decision 4: Post-Loop Disk Artifacts Only

Problem: Failure topology and success reports must exist without contaminating the 1,000-frame allocation/performance measurement.
Solution: CSV, binary dump, and JSON report writes execute only after the loop, using NativeArray byte scratch and ASCII append helpers.
Rejected Alternatives: `Debug.Log`, string-per-row formatting, and per-frame report writes were rejected because they allocate and distort solver timing.
Scalability potential: Low gets compact CSV topology reconstruction; Middle/High/Ultra can retain larger graph/failure telemetry without changing hot loop behavior.
Hardware Impact: Zero disk I/O in measured loop; failure I/O cost is outside frame simulation and only paid when math breaks.

## Decision 5: Compile Gate Deferral

Problem: Batch rule forbids `dotnet build` when CPU is above 50 percent or dotnet/csc is active.
Solution: CPU was sampled through `Get-Counter` and CIM at 92.1-100 percent; no dotnet/csc process was active. Build/test execution is deferred and marked pending verification.
Rejected Alternatives: Running build anyway was rejected because it violates the explicit batch rule and risks starving other agents on the same host.
Scalability potential: No runtime scalability effect; this preserves multi-agent machine stability.
Hardware Impact: Avoided adding compile load to already saturated CPU. Compile proof remains absent.

<SELF_AUDIT>
  <ARRAY_FORMATS>
    <PowerNodeDTO size="32" layout="existing explicit DTO" use="NativeArray + PowerNodeDTO* via UnsafeUtility.AsRef"/>
    <FluidCompartmentDTO size="32" layout="existing explicit DTO" use="editor test layout assertion"/>
    <PowerJacobiStressTopologyProfile size="32" layout="explicit" fields="ProfileHash, NodeCount, EdgeCapacity, LoopRatio01, StarRatio01, IslandRatio01, Flags"/>
    <PowerJacobiStressFrameTelemetry size="64" layout="explicit" capacity="300" fields="FrameIndex, StateHash, FailureFlags, Residual, Energy, FirstBadNodeHash, SolverMicroseconds"/>
    <PowerJacobiStressFuzzerResult size="128" layout="explicit" fields="FailureFlags, FinalResidual, MaxResidual, EnergyDeltaAbs, AverageSolverMicroseconds, ManagedBytesDelta, FirstFailureAup"/>
  </ARRAY_FORMATS>
  <EDITOR_TOOLING>
    <Window menu="Hecton/Power/Solver Fuzzer" button="RUN HOSTILE GRAPH TEST" output="PASS/FAIL, flags, residual, average solver microseconds"/>
    <Gizmo source="PowerJacobiStressFuzzerState" marker="red sphere at failed AUP modulo scene-local view"/>
  </EDITOR_TOOLING>
  <MANUAL_QA_ERADICATION>
    <Claim status="PENDING VERIFICATION">Hostile CSR topology generation, NaN injection, convergence validation, CSV export, and CI NUnit route are automated. Unity/batchmode execution proof is not produced because CPU gate blocked build/test launch.</Claim>
  </MANUAL_QA_ERADICATION>
</SELF_AUDIT>

## Decision 6: QA Headless Assembly Boundary

Problem: The fuzzer was initially staged in the Power source tree, making every fuzzer edit look like runtime-domain churn and increasing compile-wall blast radius.
Solution: Move SHINOBU_255 source into `Assets/_Project/Scripts/QA/Headless/JacobiStressFuzzer` under `Hecton8.QA.Headless.asmdef`; keep the namespace/API compatible with existing `Hecton8.Power` solver DTOs and add only the required `Unity.Jobs` assembly reference.
Rejected Alternatives: Keeping the file under `Scripts/Power` was rejected because the artifact is a CI/headless destructiveness harness, not gameplay authority. Creating a new solver copy was rejected because it would test clone math instead of production `PowerVoltageSolverJob`.
Scalability potential: Low executes the same harness with reduced authored profiles; Middle expands CSV ratios; High/Ultra run the default 5,000-node, 1,000-frame, 8-iteration route at `GlobalQualityWeight=1.0`.
Hardware Impact: Keeps fuzzer edits out of sibling runtime assemblies and confines compile churn to QA Headless plus test assembly. Expected i3/MX350 runtime gain is unchanged; iteration-time risk is lower.

## Decision 7: Phase Fence Completion Instead Of Per-Iteration Fence

Problem: The previous loop completed after every Jacobi iteration, which is harsher than the requested dispatcher phase model and artificially serializes the solver chain.
Solution: Schedule all Jacobi/SOR passes as a dependency chain and complete once at the SIMULATION solver fence; keep explicit `.Complete()` calls only at PRE, SIM, and POST boundaries required by Task 05.
Rejected Alternatives: Fully async dispatcher integration was rejected because the prompt requires no Unity PlayerLoop. Per-iteration `.Complete()` was rejected because it measures fence overhead more than solver math.
Scalability potential: Low can reduce the continuous iteration budget; Ultra still executes all eight chained solver passes while measuring math cost instead of repeated main-thread fences.
Hardware Impact: Removes seven main-thread fences per frame at the default 8-iteration setting. For 1,000 frames this avoids 7,000 avoidable synchronization points on low-end silicon.

## Decision 8: Safe Config Clamp And Layout Fail-Fast

Problem: External profile/config callers could pass zero, negative, or non-finite validation thresholds; layout drift would only be caught by NUnit instead of the fuzzer route itself.
Solution: Normalize residual, energy, and performance thresholds into `safeConfig`; call `ValidateRequiredLayouts()` at the beginning of `Run()` and return `FailureFlagLayout` before any NativeArray allocation if the ABI is wrong.
Rejected Alternatives: Trusting the edit test alone was rejected because CI needs a machine-readable failure result from the harness itself. Silently accepting zero thresholds was rejected because it creates false performance/divergence failures.
Scalability potential: Threshold semantics remain continuous and profile-driven; quality controls iteration count only when callers leave `IterationCount <= 0`, while the default proof still forces Ultra.
Hardware Impact: The fail-fast path is O(1) and prevents allocating roughly the full CSR scratch set when ABI drift invalidates the run.

## Decision 9: H-Phi Scratch Boundary

Problem: The DataVault law forbids persistent private native ownership, but Task 14 explicitly mandates TempJob `NativeArrayOptions.UninitializedMemory` graph buffers for the headless test.
Solution: Keep all native buffers method-local inside `PowerJacobiStressFuzzer.Run`, allocate with `Allocator.TempJob`, dispose in `finally`, and request no new `VaultBufferHandle` IDs. Production Power Vault IDs remain the existing `PowerGridBufferIds` `70850..70864`; SHINOBU_255 does not own or mutate those runtime lanes.
Rejected Alternatives: Adding fuzzer-only Vault BufferIDs was rejected because this is not gameplay truth or rollback state. Persistent manager fields were rejected because they would violate the Vault law and fragment ownership.
Scalability potential: Low/Middle profiles can reduce node/edge counts without layout changes; High/Ultra reuse the same single-run scratch topology.
Hardware Impact: Method-local TempJob memory keeps lifetime short and deterministic, avoiding persistent heap fragmentation on low-end devices and CI hosts.

## Decision 10: Solver Scope Honesty

Problem: The assignment names Power Grid, Fluid Equalization, and Thermal Diffusion, but the repository exposes a directly reusable production CSR relaxation job for Power while fluid/thermal routes are not exposed as public headless CSR kernels without broader cross-domain refactor.
Solution: Drive the existing production `PowerVoltageSolverJob` and `IntegrateBatteryChargeJob` now, document the limitation, and avoid fabricating proof for private/non-public fluid or thermal solver paths.
Rejected Alternatives: Duplicating private fluid/thermal solver math was rejected because clone tests are false proof. Touching broad Physics/Thermal runtime surfaces was rejected because it would violate the domain boundary and compile-wall rule without a route card.
Scalability potential: The fuzzer architecture can host additional public solver kernels later by injecting their DTO arrays into the same PRE/SIM/POST structure.
Hardware Impact: Current proof is accurate for the exposed power relaxation route only; no speculative CPU cost is added to unrelated domains.

## Decision 11: Unsafe Context Correction

Problem: `PowerJacobiStressFuzzer.Run` assigns `PowerNodeDTO*` fields through `NativeArrayUnsafeUtility.GetUnsafePtr`, which requires a C# unsafe context even when the asmdef has `allowUnsafeCode=true`.
Solution: Mark `PowerJacobiStressFuzzer` as `unsafe` while keeping pointer use confined to the headless harness and Burst job payload assignment sites.
Rejected Alternatives: Replacing pointer access with managed/indexed DTO mutation was rejected because Task 03 explicitly requires raw pointer validation through `UnsafeUtility.AsRef`.
Scalability potential: No quality-route change. The same pointer path is used from low profile runs through Ultra default runs.
Hardware Impact: Avoids accessor/copy paths and keeps validation representative of production pointer mutation. Compile proof remains pending because CPU is at 100 percent.

## Decision 12: Cold CSV Profile Load Integration

Problem: A parser alone does not satisfy Task 17 if the default fuzzer route never lets `fuzzer_topology_profiles.csv` dictate loop/star/island ratios.
Solution: `RunDefault()` now attempts a cold load from `Assets/_Project/Data/fuzzer_topology_profiles.csv` into a Temp `NativeArray<byte>` scratch, then parses it through `ReadOnlySpan<byte>` before any measured 1,000-frame loop begins.
Rejected Alternatives: Loading CSV inside the simulation loop was rejected because it would contaminate the zero-GC/timing proof. Managed `File.ReadAllText`/`Split`/LINQ parsing was rejected because it creates irrelevant garbage and hides parser cost.
Scalability potential: Low/Middle/High/Ultra QA profiles can change node count, edge capacity, loop ratio, star ratio, and island ratio without C# recompilation.
Hardware Impact: No hot-path cost. Cold 64 KB scratch read replaces manual C# edits and keeps profile authoring decoupled from runtime solver proof.

## Decision 13: Perturb-Then-Converge Injection Model

Problem: Resetting every node's potential to synthetic values on every frame prevents the Jacobi/SOR state from accumulating convergence. That can create a false divergence failure caused by the fuzzer, not by the solver.
Solution: `InjectRandomPotentialsJob` now applies hostile non-finite perturbations only at frame 0. Later PRE phases sanitize the current front/back buffers, keep demand pressure active, and preserve solver state for real convergence/oscillation measurement.
Rejected Alternatives: Per-frame full reset was rejected because it turns the test into repeated cold-start solves. Removing hostile injection entirely was rejected because Task 07 requires impossible inputs.
Scalability potential: Low through Ultra all use the same perturb-then-converge truth; quality changes iteration count/cost, not the mathematical meaning of residual history.
Hardware Impact: Same O(N) PRE pass cost, but fewer false failures and more meaningful 1,000-frame residual telemetry on low-end CI machines.

## Decision 14: Runtime Fluid Layout Fail-Fast

Problem: `FluidCompartmentDTO` layout was asserted only by the edit test, while `PowerJacobiStressFuzzer.Run()` could still execute if fluid layout drifted and tests were not reached.
Solution: `ValidateRequiredLayouts()` now checks `UnsafeUtility.SizeOf<FluidCompartmentDTO>() == 32` and `FluidCompartmentLayoutValidator.ValidateFluidCompartmentLayout()` before allocations.
Rejected Alternatives: Leaving the fluid assertion only in NUnit was rejected because Task 04 requires the fuzzer structures and production DTO assumptions to be layout-safe before profiling.
Scalability potential: No quality-route change; fail-fast layout safety is constant across all graph profiles.
Hardware Impact: O(1) startup check prevents invalid profiling data on ARM64 and avoids allocating the fuzzer scratch set under ABI drift.

## Decision 15: Per-Frame Solver Budget And Full Warm-Up

Problem: The performance threshold in the prompt is 0.2 ms per solver frame, but the previous calculation divided cumulative solver ticks by `frameCount * iterationCount`, measuring one Jacobi/SOR pass instead of the full frame budget. The warm-up also skipped `IntegrateBatteryChargeJob` and `ValidateSolverConvergenceJob`, so their first schedule/Burst setup could contaminate the managed allocation delta.
Solution: Divide solver ticks by frame count for both live telemetry and final result. Warm up `PowerVoltageSolverJob`, `IntegrateBatteryChargeJob`, and `ValidateSolverConvergenceJob` before `GC.GetAllocatedBytesForCurrentThread()`, then call the unmanaged graph/result initialization again so the measured loop starts from the same hostile baseline.
Rejected Alternatives: Keeping per-iteration timing was rejected because it under-reports frame cost by up to 8x at Ultra. Measuring after first frame was rejected because it hides allocation contamination instead of proving the full 1,000-frame loop. Adding thermodynamics runtime layout checks to the QA Headless runtime assembly was rejected because it would create a new direct sibling asmdef dependency; thermal ABI checks remain in the edit-test assembly.
Scalability potential: Low/Middle/High/Ultra still scale iteration count continuously through `GlobalQualityWeight`, but the CI default remains Ultra. The budget now measures the whole solver chain that actually spends frame time.
Hardware Impact: Performance failure detection is stricter and representative for i3/MX350-class CPUs; first-frame Burst/schedule setup is moved outside the zero-GC measured loop without changing the runtime graph topology.
