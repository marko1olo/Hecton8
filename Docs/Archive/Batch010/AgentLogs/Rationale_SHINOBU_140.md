# SHINOBU_140 Rationale

Status: PENDING VERIFICATION

## Decision 00 - Work Sequence

Problem: The assignment spans runtime code, editor scanners, documentation, and verification. Editing before source archaeology would create compile-wall risk and false authority claims.

Solution: Build from evidence: extract XML prompt, read mandate set, scan dispatcher/registry/vault/signal/AUP code, then implement the smallest enforceable integration surfaces and cold editor analyzers.

Rejected Alternatives: Directly rewriting dispatcher from prompt text was rejected because existing contracts may differ. Broad refactor was rejected because interface mutation during a batch is forbidden.

Scalability potential: Low tier receives stable dispatcher phase budgets and scan-backed hot-path bans. Middle tier receives normal cadence without hidden stalls. High tier receives richer timing. Ultra tier spends saved cycles in `VISUAL_SYNC`, not uncontrolled simulation.

Hardware Impact: Static scanners have editor-only cost. Runtime target is removal/prevention of hot registry polls and mid-frame `Complete()` stalls; expected low-end i3/MX350 gain is workload-dependent and remains PENDING VERIFICATION until profiler/GCMonitor evidence exists.

## Decision 01 - Static Enforcement Before Cross-Domain Rewrites

Problem: Subagent scan found 23 direct hot-method `GlobalRegistry.*` candidates, but other agents are actively editing Player/UI/AI/World domains. A blind master rewrite risks merge conflicts and service-contract regression.

Solution: Add `Hot_Registry_Polling_Scanner`, `Mid_Frame_Complete_Scanner`, `AUP_Compliance_Scanner`, `Vault_Sovereignty_Scanner`, `Compile_Wall_Scanner`, and `Signal_Bus_Topology_Scanner` as editor-only gates that write JSON artifacts under `Docs/Reports`.

Rejected Alternatives: Editing all 23 candidates in one pass was rejected because it would cross active domain ownership and duplicate SHINOBU_107 remediation work. Chat-only findings were rejected because the CTO reads disk artifacts.

Scalability potential: Low tier benefits when hot registry reads and mid-frame completion stalls are removed by the scanner backlog. Middle/high/ultra tiers preserve deterministic phase routing and can spend saved CPU on presentation instead of discovery.

Hardware Impact: Scanner runtime cost is 0 us in player. Removing each hot registry read is expected to save roughly 0.2-1.0 us on weak i3/MX350 silicon depending on cache state; exact proof is pending profiler.

## Decision 02 - DispatcherTimingDTO Layout

Problem: Legacy `DispatcherTimingDTO` was sequential 16 bytes and incompatible with the XML-mandated telemetry layout. My first bridge attempted to use bytes 20-31 for bucket/system/flags metadata, which violated the explicit pad-byte requirement.

Solution: Convert `DispatcherTimingDTO` to `[StructLayout(LayoutKind.Explicit, Size = 32)]` with phase milliseconds at offsets 0/4/8/12, `FrameId` at 16, and byte pads at 20-31. Keep legacy frame timing aliases at offsets 0/4/8/12 so existing dispatcher interfaces compile while the telemetry ring uses the mandated phase fields. Add `DispatcherTimingLayoutGuard.ValidateLayout()`.

Rejected Alternatives: A separate telemetry DTO was rejected because Task 16 explicitly requires a `DispatcherTimingDTO` Vault ring. Keeping the 16-byte DTO was rejected as an ARM64/cache-line violation.

Scalability potential: Low/middle tiers get a compact 32-byte timing record for cheap black-box dumps. High/ultra tiers get stable telemetry for heavier X-Ray visibility without changing the phase loop.

Hardware Impact: 300 entries consume 9600 bytes. Alignment removes avoidable unaligned load/store risk; microsecond gain is not claimed without ARM64 counter proof.

## Decision 03 - Dispatcher Kernel Changes

Problem: The dispatcher already had the phase skeleton, Kahn sort, native buffers, and legal post-sim job completion. It lacked the required mock entrypoint name, readable cycle trace, rollback visual fence, time-slice budget, and telemetry DTO ring.

Solution: Add `GenerateMockSubsystems()` over existing deterministic mock topology, route master telemetry through `VaultBufferHandle<DispatcherTimingDTO>`, add readable Kahn cycle trace, start `TimeSliceScheduler` in pre-sim, and skip `VISUAL_SYNC` when rollback/resim/hard-resync flags are active.

Rejected Alternatives: Full dispatcher rewrite was rejected because existing hot loops are already bucketed and phase fenced. Recursive rollback catch-up was rejected without a netcode-owned synchronous step contract because it can double-run simulation side effects.

Scalability potential: Low tier gets a 0.10 ms survival budget for background work. Middle tier gets 0.45 ms. High tier gets 1.10 ms. Ultra tier gets 2.00 ms and can spend the margin on visual overkill while preserving deterministic simulation barriers.

Hardware Impact: Time-slice check is one stopwatch read when queried; pre-sim begin is one timestamp read per frame. Visual-sync skip can save the full visual sync phase on rollback frames; exact us pending profiler.

## Decision 04 - X-Ray And H-Phi

Problem: Existing X-Ray was IMGUI and had no dependency graph. H-Phi output did not exist for this master integration slice.

Solution: Replace X-Ray with UI Toolkit retained elements for phase bars, bucket heatmap, quality/time-slice display, and dependency graph rows read from `SystemDispatcher.TryGetDependencyGraphSnapshot()`. Add `H_Phi_Metric_Aggregator` that combines scanner critical counts with `DispatcherTimingLayoutGuard` and writes `Docs/Reports/HECTON_PHI_SCORE_FINAL.json` when invoked.

Rejected Alternatives: Keeping IMGUI was rejected because Task 17 requires UI Toolkit. Rendering a static graph was rejected because Task 19 requires live topology data.

Scalability potential: Editor-only visibility lets low-tier problems be caught without player runtime overhead. High/ultra developers can inspect graph and phase timing without adding gameplay allocations.

Hardware Impact: Player runtime cost is limited to snapshot arrays already owned by dispatcher and editor-only reads. X-Ray cost exists only in editor.

## Decision 05 - Generated Project Boundary

Problem: `Hecton8.Core.csproj` is generated and does not include new core files until Unity regenerates projects. A standalone `TimeSliceScheduler.cs` would be invisible to CLI build while `SystemDispatcher.cs` references it.

Solution: Move `TimeSliceScheduler` into already-included `SystemDispatcher.cs` and delete the standalone file.

Rejected Alternatives: Editing generated csproj was rejected because the file states it will be overwritten. Leaving a hidden core file was rejected because it creates false CLI verification failures.

Scalability potential: Same continuous scheduler behavior, no project-generation dependency.

Hardware Impact: No runtime difference from the standalone file; compile boundary is cleaner.

## Decision 06 - Verification Gate

Problem: Build protocol forbids `dotnet build` when CPU is above 50% or another `dotnet`/`csc` is active. CPU measured 57.67%, 73.01%, and 77.35%; the final gate also showed active `dotnet` processes.

Solution: Do not launch build. Record `PENDING VERIFICATION`; keep static checks to `git diff --check`, targeted source scans, and scanner self-audit.

Rejected Alternatives: Running build anyway was rejected as direct protocol violation. Claiming compile success from stale artifacts was rejected as fake evidence.

Scalability potential: None at runtime; this protects the shared 20+ agent environment from build contention.

Hardware Impact: Avoided adding build load to an already busy workstation. Runtime performance remains unverified.

## Decision 07 - Polish Reconciliation Pass

Problem: The first X-Ray graph exposed only the first dependency per system, and visual-sync shed flags did not distinguish health-pressure shedding from rollback fencing. The scanner C# file also had no stable Unity `.meta`, leaving GUID generation to local import.

Solution: Add dispatcher transient flag bits for visual-sync shed, rollback fence, and health-pressure shed; reset those bits at the next `PRE_SIMULATION` entry to avoid stale previous-frame diagnosis; expose flat dependency edges and current job-fence telemetry snapshots; render those surfaces in the UI Toolkit X-Ray; add a stable `.meta` for the scanner asset.

Rejected Alternatives: A dispatcher-owned rollback catch-up loop was rejected because `RollbackFixedPipelineJob` already owns restore/resimulation command/audio suppression, and a second dispatcher loop would double-run side effects. A new particle suppression signal was rejected because it would create a global route without owner route-card proof. Editing generated `.csproj` was rejected; Unity asset visibility is handled with a stable `.meta`.

Scalability potential: Low tier can shed visual sync deterministically when health pressure or rollback flags are active. Middle tier keeps edge visibility for normal graph debugging. High and Ultra tiers get richer editor observability without player allocations or new runtime global routes.

Hardware Impact: Player cost remains boolean flag composition plus existing rollback state read before `VISUAL_SYNC`. X-Ray additions are editor-only. Avoiding a duplicate catch-up loop prevents uncontrolled low-end i3/MX350 frame spikes; exact us remains pending profiler.

## Decision 08 - H-Phi Static Refresh

Problem: Editor H-Phi aggregation exists, but without a fresh disk artifact the report would still rely on stale metric data.

Solution: Run the canonical static Python path with one worker: `python -B Tools/CalculateHPhi.py --workers 1 --json-output Docs/Reports/HECTON_PHI_SCORE_FINAL.json --graph-output Docs/Reports/HECTON_PHI_ARCHITECTURE_GRAPH.png --atlas Docs/PROJECT_ATLAS.md`. It wrote `Docs/Reports/HECTON_PHI_SCORE_FINAL.json` and reported `files=5206`, `DOMAIN_INDEX_COUNT=85`, and `STATUS: PHI CALCULATED`.

Rejected Alternatives: Running Unity editor scanners was rejected because no Unity import/Console proof is available in this turn. Claiming runtime H-Phi readiness from a static Python sweep was rejected; this is static source/doc/tool evidence only.

Scalability potential: H-Phi is a governance metric, not a runtime scalability feature. It makes low/middle/high/ultra regressions visible by counting source-level sovereignty and compile-wall drift before they become frame spikes.

Hardware Impact: The Python pass completed in 1.6 s wall time with `--workers 1`. No player runtime cost. Build remains blocked by active `dotnet` processes.

## Decision 09 - Static Analyzer Inquisition

Problem: The prior scanner surface covered the XML core gates but did not explicitly enforce the renewed polish mandate for runtime struct packing, DTO properties, Burst directive flags, interface-container devirtualization risk, or the unresolved rollback particle-suppression route. Leaving those as prose would let Task 11 and H-Phi drift into false compliance.

Solution: Extend `MasterIntegrationSurgeonScanners.cs` with editor-only gates: `Runtime_Struct_Layout_Scanner`, `Burst_Job_Directive_Scanner`, `Dev_Virtualization_Scanner`, and `Rollback_Fence_Compliance_Scanner`. Wire all four into `H_Phi_Metric_Aggregator` and the final `criticalCounts` JSON surface. First-20-minutes route impact: removes bootstrap/world-scene architecture blockers by making phase, memory, and rollback-presentation debt visible before Play Mode proof.

Rejected Alternatives: Runtime patching every flagged domain was rejected because this agent does not own Player/UI/AI/World/Networking internals and active agents may be editing them. Adding a new global particle suppression signal was rejected because `GLOBAL_AUTHORITY_BOUNDARIES.md` requires route-card ownership and `GREEN` review before new global traffic. A dispatcher-owned rollback catch-up loop remains rejected because netcode owns restore/resimulation command and audio suppression; a duplicate loop would double-run side effects.

Scalability potential: Low tier gains hard gates against cache-hostile DTOs, missing Burst directives, virtual dispatch in hot containers, and rollback presentation leakage. Middle tier gets static JSON artifacts for remediation routing. High and Ultra keep richer editor/H-Phi visibility while player runtime cost remains 0 us.

Hardware Impact: New scanners are Editor-only and add 0 us to player frame time. On i3/MX350, remediation of their findings prevents avoidable ARM64/unified-memory stalls, Burst scalar fallback, and rollback visual/audio spikes; exact savings remain PENDING PROFILER.

## Decision 10 - Build Gate Recheck After Analyzer Pass

Problem: Task 20 still wants compile proof, but project protocol forbids launching `dotnet build` while another `dotnet`/`csc` process is active even if CPU is under 50%.

Solution: Re-run the gate after the analyzer pass. CPU averaged `38.74%`, but an active `dotnet` process with PID `53260` existed, so build remains blocked. The canonical Python H-Phi static tool was safe to run and completed again with `files=5206`, `DOMAIN_INDEX_COUNT=85`, and `STATUS: PHI CALCULATED`.

Rejected Alternatives: Launching build beside another `dotnet` process was rejected as direct protocol violation. Treating Python H-Phi as compile proof was rejected because it is static source/doc/tool evidence only.

Scalability potential: None in player runtime; this protects the shared 20+ agent workstation from compile contention while preserving static governance evidence.

Hardware Impact: Avoided adding another compiler workload to an active dotnet process. Runtime performance and GC proof remain PENDING VERIFICATION.

## Decision 11 - CI Fallback Scanner Proof

Problem: Editor-only scanner menus are not CI proof. Without a Unity-free runner, H-Phi scanner claims depend on Unity import/menu execution, which is unavailable and would leave Task 18 as source-only.

Solution: Add `Tools/RunShinobu140StaticScanners.py`. It mirrors the SHINOBU_140 static gates, writes per-scanner JSON files plus `Docs/Reports/SHINOBU_140_STATIC_GATE_SUMMARY.json`, and returns a non-zero gate exit when critical debt exists. The first version timed out due to repeated full-source passes; it was corrected to one cached C# pass plus one bounded asmdef pass. It completed in `36189 ms` with `totalCritical=3419`, `totalWarnings=516`, and exit gate `1`.

Rejected Alternatives: Depending on Unity Editor menu execution was rejected because no Unity import/Console proof exists. Keeping a timed-out script was rejected because CI gates must be bounded. Hiding the non-zero exit was rejected; the tool reports `PENDING VERIFICATION` and red debt honestly.

Scalability potential: Low-tier development machines can run the fallback once without Unity import. Middle/high/ultra machines can consume the detailed per-scanner JSON for targeted remediation. Player runtime cost remains 0 us.

Hardware Impact: The fallback scan cost `36189 ms` wall time on this workspace and does not touch gameplay. It creates actionable static debt: Hot Registry `21`, Mid-Frame Complete `0`, Rollback Fence `0 critical / 1 warning`, Compile Wall `8`, Runtime Struct Layout `2020`, Vault Sovereignty `667`, Burst Directives `667`.

## Decision 12 - Final Gate After CI Fallback

Problem: After adding the CI fallback runner and generated report artifacts, the canonical H-Phi artifact needed a fresh static refresh and Task 20 still needed a legal build-gate check.

Solution: Re-run `python -B Tools/CalculateHPhi.py --workers 1 --json-output Docs/Reports/HECTON_PHI_SCORE_FINAL.json --graph-output Docs/Reports/HECTON_PHI_ARCHITECTURE_GRAPH.png --atlas Docs/PROJECT_ATLAS.md`; it reported `files=5206`, `DOMAIN_INDEX_COUNT=85`, and `STATUS: PHI CALCULATED`. Re-sampled build gate afterward: CPU averaged `51.26%`, `dotnet/csc=none`, so build remains blocked by CPU policy.

Rejected Alternatives: Running `dotnet build` at `51.26%` CPU was rejected as protocol violation. Claiming the Python H-Phi pass as compile proof was rejected.

Scalability potential: Static governance proof is refreshed without stealing compile resources from other agents; runtime status remains unchanged.

Hardware Impact: No player runtime cost. Build load avoided while workstation CPU was over the allowed threshold.

## Decision 13 - Canonical H-Phi Gate Binding

Problem: `Docs/Reports/SHINOBU_140_STATIC_GATE_SUMMARY.json` carried the static scanner gate, but `Tools/CalculateHPhi.py` could refresh `Docs/Reports/HECTON_PHI_SCORE_FINAL.json` without carrying that red gate forward. That made the canonical H-Phi artifact weaker than the SHINOBU_140 sidecar evidence.

Solution: Patch `Tools/CalculateHPhi.py` to load `Docs/Reports/SHINOBU_140_STATIC_GATE_SUMMARY.json` when present and embed `master_integration_static_gates` into the final JSON. The embedded block records evidence class, source path, Unity invocation state, `gate_passed`, total critical/warning counts, scanner count, and scanner rows. Re-run H-Phi; the final artifact now records `gate_passed=false`, `total_critical=3419`, `total_warnings=516`, and `scanner_count=10`.

Rejected Alternatives: Leaving the scanner summary as a sidecar was rejected because the CTO-facing H-Phi artifact could be read without the SHINOBU_140 gate. Treating the sidecar's non-zero exit as a failure of the H-Phi writer was rejected; H-Phi is an evidence carrier, while the scanner gate remains a red static debt gate. Running Unity editor menu scanners was rejected because no Unity import/Console proof exists.

Scalability potential: Low-tier review machines get the same red static gate from the compact canonical JSON. Middle/high/ultra review paths keep scanner row detail for targeted cleanup without player runtime cost. This is governance visibility, not gameplay performance.

Hardware Impact: Player/runtime impact is 0 us. Python H-Phi refresh completed in 4.5 s after the patch. A legal compile still could not be launched: gate samples averaged `76.67%` CPU with active `dotnet` PID `32468`, then `60.33%` CPU with no compiler processes.

## Active Mandates

- ARCH_Execution_Phases
- ARCH_Global_Registry_ServiceLocator_DI_Init
- ARCH_Signal_Lane_Segregation
- DATA_Runtime_Struct_Layout_ARM64
- OPT_Native_Memory_Collections_JobSystem_Protocol
- OPT_Zero_GC_Policy_AllocFree_Mandate
- MATH_AUP_Determinism_Sync
- DBG_Telemetry_Crash_Reporting_PostMortem
