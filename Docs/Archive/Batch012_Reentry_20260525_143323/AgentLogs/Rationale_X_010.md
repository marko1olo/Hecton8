# Rationale_X_010 - POWER_GRID_AND_CSR_SIMPLIFIER

Status: IMPLEMENTED - STATIC PASS - COMPILE RETRY BLOCKED
Date: 2026-05-23

## Decision 00 - Phase 0 Scope Lock

Problem: The assignment names a stale root `current_batch.md`; the active batch prompt exists at `Docs/Tasks/CURRENT_BATCH.md`.
Solution: Extracted `<AGENT_PROMPT id="X_010"...>` by CLI regex from the active task file and ignored neighboring prompts.
Rejected Alternatives: Reading archived batch prompts was rejected because AGENTS.md forbids previous-batch contamination. Proceeding without extraction was rejected because task count and constraints would be unproven.
Scalability potential: Low tier needs bounded active-compartment traversal; middle/high/ultra can spend saved CPU on visual logistics proxies without changing gameplay truth.
Hardware Impact: Avoiding stale-task drift prevents code churn and compile-wall cost; runtime gain from this decision is 0 us.

## Decision 01 - Mandate Set

Problem: Power/fluid logistics touches graph math, Native memory, ARM64 DTO layout, telemetry, AUP, and global authority.
Solution: Loaded mandates LOGI_Energy_Networks_Power_Grid_Graph_Flow, NET_Logistics_Quantum, DATA_Runtime_Struct_Layout_ARM64, OPT_Zero_GC_Policy_AllocFree_Mandate, OPT_Native_Memory_Collections_JobSystem_Protocol, DBG_Telemetry_Crash_Reporting_PostMortem, MATH_AUP_Determinism_Sync, and ARCH_Global_Registry_ServiceLocator_DI_Init.
Rejected Alternatives: Loading all mandates was rejected as noise. Loading only logistics was rejected because DTO alignment, blackbox, and GlobalRegistry misuse are explicit acceptance risks.
Scalability potential: Low uses bounded 2-pass/fast-path; middle/high/ultra increase cadence and presentation detail through continuous `GlobalQualityWeight`, not binary switches.
Hardware Impact: Mandate alignment targets i3/MX350 cache-linear execution, 0 B GC, and bounded job scheduling. Decision cost/saving is 0 us runtime.

## Decision 02 - Two-Pass Delta Over Jacobi

Problem: The logistics power and pipe paths contained multi-pass relaxation patterns that scaled with iteration count instead of active compartment truth.
Solution: Replaced hot solve loops with fixed two-pass delta propagation in `LogisticsNetworkGraph`, `ShinobuLogisticsRouter`, and `SumpPumpPipeGridRuntime`. The first pass distributes source/frontier potential; the second equalizes local deltas. The algorithm is deterministic and non-recursive.
Rejected Alternatives: Standard Jacobi convergence was rejected because ten sweeps over 2000 nodes/6000 edges burns memory bandwidth for visual smoothness that can be bought elsewhere. Same-frame schedule/readback loops were rejected by mandate.
Scalability potential: Low tier gets exactly two passes and coarser cadence. Middle tier keeps two passes with normal cadence. High tier spends saved CPU on telemetry, VFX flow visualization, and UI overlays. Ultra tier may add visual-only overkill layers, not gameplay-truth iterations.
Hardware Impact: Static estimate is 320 us saved on i3/MX350 for 2000 nodes/6000 edges versus ten relaxation passes. Exact profiler proof is pending compile gate and runtime harness execution.

## Decision 03 - 32-Byte CSR DTOs

Problem: `PipeEdgeDTO` carried unused 64-byte fields in the hot CSR edge stream, doubling cache pressure for 6000-edge drainage sweeps.
Solution: Reduced `PipeEdgeDTO` to explicit 32-byte layout: source, destination, conductance, current flow, flags, edge hash, source hash, destination hash. Validation checks offsets in development/editor builds.
Rejected Alternatives: Keeping 64 bytes for future metadata was rejected because the removed fields were not used by the hot jobs. Adding side-channel managed metadata was rejected because it violates Zero-GC hot path.
Scalability potential: Low tier halves edge payload bandwidth. Middle/high/ultra can use sidecar visual telemetry buffers if needed, while the authority DTO stays compact.
Hardware Impact: Static estimate is 12 us saved per 6000-edge edge sweep on i3/MX350-class memory bandwidth, plus 192 KB less hot edge footprint.

## Decision 04 - Open-Circuit And Unpowered Fast Paths

Problem: Empty/open networks and no-generation networks could still enter scheduling or retain powered source flags while publishing zero potential.
Solution: Added/kept pre-schedule bypasses and corrected `ResetNoEdgeRuntimeState` so all open-circuit nodes are isolated/offline/non-powered with zero potential. Source identity can remain as source metadata, but powered truth is false.
Rejected Alternatives: Scheduling an empty Burst job was rejected because scheduler overhead dominates a dead-grid frame. Leaving source nodes powered in an open circuit was rejected because it creates false telemetry and consumer-facing truth divergence.
Scalability potential: Low tier avoids useless work during outages. Middle/high/ultra can still render outage diagnostics from telemetry without reintroducing solver work.
Hardware Impact: Static estimate is 48 us saved per dead-grid tick from skipped job setup and summary solve chain.

## Decision 05 - Scanner As Proof Artifact

Problem: Manual grep does not prevent a later agent from reintroducing iterative loops or deleting the torture job.
Solution: Added `Tools/OOP_Fluid_Scanner_X_010.py` and routed `Tools/OOP_Fluid_Scanner.py` through it. The scanner fails on hot suspicious loops, recursive hot graph methods, missing two-pass proof, missing open-circuit proof, or missing 2000/6000 torture job proof.
Rejected Alternatives: A prose report was rejected because it cannot run in CI. A broad all-project fail on managed containers was rejected because cold authoring owners legitimately use managed buffers.
Scalability potential: Static proof has no runtime cost. It protects low-tier performance by making hot loops visible before runtime.
Hardware Impact: 0 us runtime. Build/CI time cost is accepted as cold verification.

## Decision 06 - Stress Harness

Problem: The task required a hard 2000-node/6000-edge stress shape instead of a theoretical claim.
Solution: Added `LogisticsGridTortureJob`, an unmanaged Burst `IJob` that materializes nodes, CSR offsets, destinations, conductance, runs two delta passes, and writes a 64-byte result summary.
Rejected Alternatives: A MonoBehaviour scene harness was rejected because it would drag managed setup into the proof. A recursive graph builder was rejected because traversal stack ownership must remain explicit.
Scalability potential: Low tier uses the harness as offline proof. Middle/high/ultra can schedule it in QA or benchmark sessions without shipping-frame cost.
Hardware Impact: Expected saving envelope remains 320 us versus ten-pass relaxation for the same stress dimensions. The harness itself is not a shipped-frame cost unless deliberately scheduled.

## Decision 07 - Compile Gate Honesty

Problem: The project forbids launching dotnet build while CPU is above 50% or `csc.exe`/`dotnet.exe` is running, and the first legal compile failed outside X_010 logistics files.
Solution: Ran one legal single-worker build when CPU was below 50%. It failed with 17 unresolved core route symbols: `CraftingSignalRoute`, `SimulationSignalRoute`, `SurvivalSignalRoute`, and `AupSignalRoute`. A retry was blocked because CPU returned to >50% and another `dotnet.exe`/`csc.exe` pair was active.
Rejected Alternatives: Running another build under load was rejected because it violates the explicit agent rule and risks interfering with other agents. Editing core signal routing was rejected because it is outside the X_010 domain and the failure predates the logistics patch.
Scalability potential: This preserves parallel-agent throughput and avoids false compile-wall noise.
Hardware Impact: 0 runtime us. Static X_010 scanner is PASS over 2379 C# files; full compile verification remains blocked by a non-X_010 dependency/gate state. Latest retry gate sample on 2026-05-23 was CPU 94.60%, 95.37%, 95.18% with two active `dotnet.exe` processes.

## Decision 08 - Short-Circuit Stress Proof

Problem: A static 2000-node torture job was not enough proof for multiple short circuits or oscillation control.
Solution: Added short-circuit injection to `LogisticsGridTortureJob` and added `Tools/LogisticsDeltaStress_X_010.py`. The runner executes 512 frames over a 2000-node/6000-edge CSR graph with 384 moving short circuits per frame. Result: PASS, 0 NaN, minPotential 0.0, maxPotential 1.0, maxFrameDelta 0.964.
Rejected Alternatives: Claiming stability from code inspection alone was rejected. Reintroducing convergence iterations was rejected because the two-pass solver must terminate deterministically.
Scalability potential: Low tier runs bounded two-pass math and skips inactive compartments. Middle/high/ultra can add visual-only diagnostics; authority math remains unchanged.
Hardware Impact: Boundedness is proven by clamp/convex-average math. Full active 2000/6000 solve is not honestly sub-microsecond on weak hardware; sub-us only applies to latched idle zero-state frames with 0 CSR touches.

## Decision 09 - Release Jacobi Boundary

Problem: The demand for zero Jacobi references in release cannot be truthfully applied project-wide because release compile includes still contain thermal/legacy solver code outside X_010.
Solution: Added `Tools/LogisticsReleaseJacobiAudit_X_010.py`. It proves `x010HotLogisticsHeavyJacobiCount=0` while reporting project-wide release findings: 45 heavy numerical thermal/legacy findings and 34 legacy Jacobi-name findings outside X_010 hot logistics.
Rejected Alternatives: Hiding the release findings was rejected. Editing submarine thermal solver was rejected as out-of-domain for X_010 and a compile-wall risk.
Scalability potential: X_010 hot logistics release path stays two-pass and bounded. Remaining thermal findings require their owning domain.
Hardware Impact: 0 runtime us for the audit. Runtime impact of out-of-domain thermal solver is not claimed by X_010.
