# SHINOBU_140 Rationale

Status: PENDING VERIFICATION

## Decision 00 - Evidence Before Surgery

Problem: The XML assignment spans runtime dispatcher code, editor analyzers, CI fallback tooling, H-Phi reporting, and black-box proof. Editing from chat memory would create false compliance.

Solution: Extract `SHINOBU_140` from `Docs/Tasks/CURRENT_BATCH.md`, read AGENTS/domain docs/active mandates, then patch only dispatcher, editor scanner, and H-Phi tooling surfaces.

Rejected Alternatives: Full dispatcher rewrite and broad domain edits were rejected because 20+ agents are active and owner-local routing is mandatory.

Scalability potential: Low devices benefit from enforced gates against hot registry polling, mid-frame completion, and cache-hostile structs. Middle/high/ultra tiers keep richer observability without player overhead.

Hardware Impact: Static gates are 0 us player cost. Runtime savings depend on later remediation and profiler proof.

## Decision 01 - Static Enforcement Over Cross-Domain Remediation

Problem: Hot registry, vault, Burst, and struct-layout debt crosses many domains not owned by SHINOBU_140.

Solution: Add editor scanners plus `Tools/RunShinobu140StaticScanners.py` CI fallback that emits JSON artifacts and returns non-zero while debt remains.

Rejected Alternatives: Blindly editing Player/UI/AI/World/Networking was rejected as ownership sabotage.

Scalability potential: The gates make low-tier frame risks visible before play mode; high-tier tooling can consume detailed JSON rows.

Hardware Impact: Latest fallback scan cost 23.2 s wall time, 0 us player runtime.

## Decision 02 - DispatcherTimingDTO Layout

Problem: The dispatcher needed a fixed black-box telemetry DTO that is ARM64-safe and resistant to CS1612 property copies.

Solution: Use `[StructLayout(LayoutKind.Explicit, Size = 32)]` with float fields at offsets 0/4/8/12, `FrameId` at 16, and pad bytes 20-31. Validate with `DispatcherTimingLayoutGuard`.

Rejected Alternatives: Sequential DTOs and C# properties were rejected.

Scalability potential: Low/middle/high/ultra all read the same compact telemetry record.

Hardware Impact: 300 entries consume 9600 bytes; alignment avoids unaligned read risk.

## Decision 03 - Continuous Time Slicing

Problem: Binary quality tiers violate HECTON-8 scalability law.

Solution: `TimeSliceScheduler` maps `GlobalQualityWeight` onto smooth 0.10/0.45/1.10/2.00 ms background budgets.

Rejected Alternatives: `if low end` switches and fixed budget constants were rejected.

Scalability potential: Low collapses background work; middle keeps normal service cadence; high/ultra buys visual overkill time.

Hardware Impact: One timestamp/budget path per query; exact profiler proof pending.

## Decision 04 - Rollback Fence Ownership

Problem: Visual sync must be suppressed during rollback/resimulation, but dispatcher cannot own netcode restore/catch-up side effects.

Solution: Dispatcher fences `VISUAL_SYNC` on rollback flags while netcode remains owner of restore/resimulation/audio suppression.

Rejected Alternatives: Recursive dispatcher catch-up was rejected because it could double-run simulation side effects. New particle suppression global route was rejected without owner route-card proof.

Scalability potential: Low devices skip presentation work during rollback pressure; high/ultra preserve richer presentation outside rollback frames.

Hardware Impact: O(1) flag read plus skipped visual sync phase on fenced frames.

## Decision 05 - Rollback Fence Compile-Wall Repair

Problem: The first rollback fence called `Hecton8.Networking.HectonRollbackNetcodeRuntime.TryGetRuntimeState`, creating a direct Core-to-Networking source dependency.

Solution: Replace the concrete call with a DataVault read of `(BufferID)70752` into `MasterRollbackRuntimeStateProbeDTO`, a local explicit 96-byte mirror with `Flags` at offset 52 and ulong fields preserving 8-byte alignment. The flag constants are local numeric mirrors of the stable netcode bit positions.

Rejected Alternatives: Adding `using Hecton8.Networking` or keeping the fully qualified type was rejected as compile-wall breach. Creating a new cross-domain API was rejected because contracts already expose DataVault as the route.

Scalability potential: Low/middle/high/ultra all get the same constant-time fence without sibling assembly recompilation pressure.

Hardware Impact: One `TryGetBuffer` and one `NativeArray` element read before visual sync. No allocations.

## Decision 06 - Compile-Wall Gate Strengthening

Problem: The compile-wall scanner originally checked only asmdef references and could miss fully qualified source references.

Solution: Extend both editor and Python scanners to scan Core runtime source for forbidden sibling namespace tokens. Latest compile-wall report has 124 critical findings and 0 `Hecton8.Networking` findings after the local repair.

Rejected Alternatives: Asmdef-only enforcement was rejected because the defect was source-level.

Scalability potential: Faster iteration by preventing accidental sibling edges from entering Core.

Hardware Impact: Tool-only cost; 0 us runtime.

## Decision 07 - H-Phi Gate Binding

Problem: The canonical `HECTON_PHI_SCORE_FINAL.json` could be refreshed without carrying SHINOBU_140 scanner failure evidence.

Solution: Patch `Tools/CalculateHPhi.py` to embed `master_integration_static_gates` from `Docs/Reports/SHINOBU_140_STATIC_GATE_SUMMARY.json`.

Rejected Alternatives: Sidecar-only reporting was rejected because the CTO-facing artifact could hide red gates.

Scalability potential: Governance visibility, not runtime behavior.

Hardware Impact: Latest H-Phi refresh cost 3.4 s, 0 us player cost.

## Decision 08 - Build Attempt And Blocker

Problem: Task 20 requires verification, and CPU/process gate became legal: 17.05% CPU and no `dotnet/csc` process.

Solution: Run targeted `dotnet build Hecton8.Core.csproj --no-restore -m:1`, not a full rebuild.

Rejected Alternatives: Earlier build attempts were rejected while CPU/process gates were illegal. Fixing 314 errors across PDA/Fauna/UI/World/Gameplay/Physics was rejected as cross-domain scope breach.

Scalability potential: None directly; this protects shared build resources and records real compile state.

Hardware Impact: Build ran 101.2 s and failed with 314 existing errors. No SHINOBU_140 touched file was named in reported failures.

## Active Mandates

- ARCH_Execution_Phases
- ARCH_Global_Registry_ServiceLocator_DI_Init
- ARCH_Signal_Lane_Segregation
- DATA_Runtime_Struct_Layout_ARM64
- OPT_Native_Memory_Collections_JobSystem_Protocol
- OPT_Zero_GC_Policy_AllocFree_Mandate
- MATH_AUP_Determinism_Sync
- DBG_Telemetry_Crash_Reporting_PostMortem
