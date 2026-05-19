# Abyssal Thermodynamics Solver

Date: 2026-05-19
Owner: SHINOBU_117
Status: pending Unity compile because narrow `Hecton8.Core.csproj` build is blocked by Visor/Somatic missing DTO dependencies outside thermodynamics.
Source anchors: `Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.cs`, `Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsJobs.cs`, `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardTypes.cs`.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R32 architecture R4/proof-wording correction is the latest artifact-backed local static DOC_GLOBAL boundary for architecture/root documentation. R31 remains the prior current-boundary propagation layer, R30 remains the prior internal-currentness layer, R29 remains the prior stale-gate/global-authority layer, R28 remains the prior interior-boundary layer, and R27 remains the latest source-counter/index snapshot until rerun.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Runtime Contract
- `ThermalCellDTO` is `[StructLayout(LayoutKind.Explicit, Size = 16)]`.
- Offsets: temperature `0`, conductivity `4`, convection Y `8`, flags `12`.
- Main buffers live in `GlobalDataVault` under `BufferID.AbyssalThermal*`.
- Hot solver jobs use raw pointers and `NativeArrayOptions.UninitializedMemory`.
- Missing boot Vault is a fail-fast error; the abyssal solver and legacy thermodynamics hazard grid do not create private fallback Vaults.
- Heat producers route through `IThermodynamicsService.TryInjectTransientHeatSource`; the legacy service facade publishes `ThermalSourceSignal` and `HectonHazardManager` is not the heat authority.
- `ThermalSourceSignal` is `[StructLayout(LayoutKind.Explicit, Size = 64)]`: AUP `0`, radius `48`, intensity `52`, source id `56`, frame `60`.
- `ThermalSourceSignal` is a direct signal registry lane with capacity `128`, low-tier frame cap `32`, deterministic mutation order, and sort keys from `SourceId` or folded AUP/radius/intensity.
- The legacy thermodynamics hazard grid no longer schedules an entity damage job or publishes thermodynamics-owned `CombatDamageSignal`/mock damage. It only publishes updraft signals; damage owners must sample the field.
- Legacy thermodynamics source emission is serial deterministic; updraft extraction is done during the serial telemetry scan, not by interlocked writes from parallel diffusion.
- Legacy resolution now follows a continuous `GlobalQualityWeight` polynomial from 16^3 to 32^3 with smooth health-pressure damping and an optional continuous `qualityCeiling`, not a force-low bool.
- Abyssal active resolution is also quality-derived, but accepts changed resolution targets only after a 3 second hysteresis band to prevent full-grid rebuild flicker under noisy hardware pressure.

## Simulation
- Heat producers write `ThermalSourceSignal`; `AbyssalThermodynamicsSolver` ingests the frame snapshot into Vault `HeatSourceDTO` records before scheduling jobs.
- Transient signal sources expire after 6 solver frames unless refreshed. Direct authored sources set `HeatSourceDTO.FlagPersistent`; mock volcano sources set `HeatSourceDTO.FlagMock` and are removed when real heat arrives.
- `ThermalInjectionJob` maps AUP to wrapped grid cells after subtracting `GridOriginAup`.
- `HeatDiffusionSolverJob` is scheduled as a dependency chain. It preserves Front for audit and rotates writes through Back/ShiftScratch as `GlobalQualityWeight` raises Jacobi pass count.
- `ShiftThermalGridJob` recenters the sliding window asynchronously with `UnsafeUtility.MemMove`.
- `SampleTemperatureJob` is data-provider only; damage owners consume temperature output. At low `GlobalQualityWeight` it collapses to nearest-cell reads, then blends through a polynomial toward trilinear temperature/convection/conductivity sampling for high-tier perception.
- Legacy `ThermodynamicsHazardGridRuntime` is also data-provider/updraft-only; direct heat/radiation damage emission was removed from its simulation chain.
- Thermodynamics code no longer uses Unity `Time.frameCount`/`Time.deltaTime` for its own simulation metadata. The abyssal solver uses its own frame counter; the legacy grid uses `_simulationFrame`; thermal source signals use the core arena frame sequence.

## Presentation
- `ConvectionVelocityY` is a scalar fake for heat shimmer.
- VISUAL_SYNC uploads the cell buffer to `_H8AbyssalThermalCells` through double-buffered `GraphicsBuffer` pages using `LockBufferForWrite` and `UnsafeUtility.MemCpy`.
- `OnDrawGizmos` draws a blue/yellow/white slice for designer validation.

## Black Box
- `ThermalTelemetryEntry[300]` records max temperature, source count, iterations, solver time, energy before/after, flags, and NaN cell.
- Energy audit compares Front+Injection against the final Back/ShiftScratch field and flags non-dissipation drift.
- NaN detection dumps the ring to `Docs/AgentLogs/Dump_THERMO_SURGEON.bin`.
