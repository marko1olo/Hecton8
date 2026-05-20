# Abyssal Thermodynamics Solver

Date: 2026-05-19
Owner: SHINOBU_117
Status: pending Unity compile because narrow `Hecton8.Core.csproj` build is blocked by Visor/Somatic missing DTO dependencies outside thermodynamics.
Source anchors: `Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.cs`, `Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsJobs.cs`, `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardTypes.cs`.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R47 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R47 root/architecture authority-spine/runtime-wording/counter-drift correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R46 remains the prior interior-authority/route-field/proof-language correction. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Runtime Contract
- `ThermalCellDTO` is `[StructLayout(LayoutKind.Explicit, Size = 16)]`.
- Offsets: temperature `0`, conductivity `4`, convection Y `8`, flags `12`.
- `ThermalGridTuningDTO` is `[StructLayout(LayoutKind.Explicit, Size = 128)]`; offset `124` is `SimulationTickDeltaSeconds`, reusing the former padding slot without growing the ABI.
- Main buffers live in `GlobalDataVault` under `BufferID.AbyssalThermal*`.
- SHINOBU_203 adds owner-local solver-control Vault buffers `70052` (`ThermalSolverConvergenceStateDTO[1]`), `70053` (`ThermalResidualSlot64[128]`), and `70054` (`int[1]` dump latch). These are convergence telemetry/control lanes, not thermal source truth.
- Each `ThermalResidualSlot64` row is one explicit 64-byte cache line with residual at offset `0` and fault flags at offset `4`; primary jobs write via `[NativeSetThreadIndex]`, and the scalar reduction scans those padded slots instead of the voxel grid.
- `ThermalSolverConvergenceStateDTO` is `[StructLayout(LayoutKind.Explicit, Size = 16)]` with residuals at `0`/`4`, omega at `8`, iteration count at `12`, and fault flags at `14`; `ThermalCellLayoutValidator` validates the layout on cold enable.
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
- Abyssal heat energy uses fixed deterministic simulation deltas, not dispatcher frame delta. `GlobalQualityWeight` resolves cadence continuously from 12 frames at minimum quality to 1 frame at full quality; `SimulationTickDeltaSeconds = cadenceFrames * 1/60`.
- `AbyssalThermodynamicsSolver` ingests/refreshes `ThermalSourceSignal` records every dispatcher tick before cadence gating, so low-tier solver throttling does not drop producer data.
- `HeatDiffusionSolverJob` is scheduled as a dependency chain with `ThermalSolverResidualReductionJob` after each pass. Each scheduled pass performs one double-buffer Jacobi relaxation from Front to Back with Jacobi-safe dynamic damping (`omega` 0.55..1.0); there is no hidden in-job `JacobiIterations` loop and no false `omega > 1` SOR label on the double-buffer path. It sanitizes ambient, max-stable, conductivity, dissipation, and source payload scalars before deriving thermal diffusion or injection math, and cold/editor tuning writes clamp grid resolution, active cell count, quality, cell size, conductivity, convection, and dissipation before they reach the Vault. It preserves Front for audit, rotates writes through Back/ShiftScratch as `GlobalQualityWeight` raises pass count, and terminal convergence makes later ping-pong passes copy forward instead of re-solving.
- Every processed thermal voxel contributes its already-computed residual to 64-byte worker slots for authoritative convergence. `GlobalQualityWeight` still controls pass count, tolerance, cadence, and sample/read presentation quality continuously; sampled-only convergence is forbidden because it can hide divergent unsampled cells.
- `ShiftThermalGridJob` recenters the sliding window asynchronously with `UnsafeUtility.MemMove`.
- `SampleTemperatureJob` is data-provider only; damage owners consume temperature output. External sample handles are chained into the next thermodynamics writer dependency so Front/Back swaps cannot race reader jobs. At low `GlobalQualityWeight` it collapses toward nearest-cell reads, then blends through a polynomial toward trilinear temperature/convection/conductivity sampling for high-tier perception.
- Legacy `ThermodynamicsHazardGridRuntime` is also data-provider/updraft-only; direct heat/radiation damage emission was removed from its simulation chain.
- Thermodynamics code no longer uses Unity `Time.frameCount`/`Time.deltaTime` for its own simulation metadata or abyssal heat integration. The abyssal solver uses its own frame counter and fixed `SimulationTickDeltaSeconds`; the legacy grid uses `_simulationFrame`; thermal source signals use the core arena frame sequence.

## Presentation
- `ConvectionVelocityY` is a scalar fake for heat shimmer.
- VISUAL_SYNC uploads the cell buffer to `_H8AbyssalThermalCells` through double-buffered `GraphicsBuffer` pages using `LockBufferForWrite` and `UnsafeUtility.MemCpy`.
- `OnDrawGizmos` draws a blue/yellow/white slice for designer validation.

## Black Box
- `ThermalTelemetryEntry[300]` records max temperature, source count, actual convergence iteration count, solver time, energy before/after, flags, and NaN/divergent cell evidence.
- Energy audit compares Front+Injection against the final Back/ShiftScratch field and flags non-dissipation drift.
- NaN or divergent solver detection dumps the ring immediately to `Docs/AgentLogs/Dump_THERMO_SURGEON.bin` and the SHINOBU_203 alias `Docs/AgentLogs/Dump_SHINOBU_203.bin`; max-iteration exhaustion dumps after five consecutive capped frames. Vault buffer `70054` latches the last dumped fault key and resets after a clean telemetry frame, preventing repeated disk writes for one continuous fault.
