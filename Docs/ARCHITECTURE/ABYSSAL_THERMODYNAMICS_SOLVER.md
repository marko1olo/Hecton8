# Abyssal Thermodynamics Solver

Date: 2026-05-19

Owner: SHINOBU_117

Status: pending Unity compile because narrow `Hecton8.Core.csproj` build is blocked by Visor/Somatic missing DTO dependencies outside thermodynamics.

Source anchors: `Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.cs`, `Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsJobs.cs`, `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardTypes.cs`.

## Runtime Contract

- `ThermalCellDTO` is `[StructLayout(LayoutKind.Explicit, Size = 16)]`.

- Offsets: temperature `0`, conductivity `4`, convection Y `8`, flags `12`.

- `ThermalGridTuningDTO` is `[StructLayout(LayoutKind.Explicit, Size = 128)]`; offset `124` is `SimulationTickDeltaSeconds`, reusing the former padding slot without growing the ABI.

- Main buffers live in `GlobalDataVault` under `BufferID.AbyssalThermal*`.

- SHINOBU_203 adds owner-local solver-control Vault buffers `70052` (`ThermalSolverConvergenceStateDTO[1]`), `70053` (`ThermalResidualSlot64[128]`), and `70054` (`int[1]` dump latch). These are convergence telemetry/control lanes, not thermal source truth.

- Each `ThermalResidualSlot64` row is one explicit 64-byte cache line.
- Residual offset: `0`; fault flags offset: `4`.
- Primary jobs write via `[NativeSetThreadIndex]`.
- Scalar reduction scans padded slots, not the voxel grid.

- `ThermalSolverConvergenceStateDTO` is `[StructLayout(LayoutKind.Explicit, Size = 16)]` with residuals at `0`/`4`, omega at `8`, iteration count at `12`, and fault flags at `14`; `ThermalCellLayoutValidator` validates the layout on cold enable.

- Hot solver jobs use raw pointers and `NativeArrayOptions.UninitializedMemory`.

- Missing boot Vault is a fail-fast error; the abyssal solver and legacy thermodynamics hazard grid do not create private fallback Vaults.

- Heat producers route through `IThermodynamicsService.TryInjectTransientHeatSource`; the legacy service facade publishes `ThermalSourceSignal` and `HectonHazardManager` is not the heat authority.

- `ThermalSourceSignal` is `[StructLayout(LayoutKind.Explicit, Size = 64)]`: AUP `0`, radius `48`, intensity `52`, source id `56`, frame `60`.

- `ThermalSourceSignal` is a direct signal registry lane with capacity `128`, minimum-quality frame cap `32`, deterministic mutation order, and sort keys from `SourceId` or folded AUP/radius/intensity.
- The legacy thermodynamics hazard grid no longer schedules an entity damage job or publishes thermodynamics-owned `CombatDamageSignal`/mock damage. It only publishes updraft signals; damage owners must sample the field.

- Legacy thermodynamics source emission is serial deterministic; updraft extraction is done during the serial telemetry scan, not by interlocked writes from parallel diffusion.

- Legacy resolution now follows a continuous `GlobalQualityWeight` polynomial from 16^3 to 32^3 with smooth health-pressure damping and an optional continuous `qualityCeiling`, not a force-low bool.

- Abyssal active resolution is also quality-derived, but accepts changed resolution targets only after a 3 second hysteresis band to prevent full-grid rebuild flicker under noisy hardware pressure.

## Simulation

- Heat producers write `ThermalSourceSignal`; `AbyssalThermodynamicsSolver` ingests the frame snapshot into Vault `HeatSourceDTO` records before scheduling jobs.

- Transient signal sources expire after 6 solver frames unless refreshed. Direct authored sources set `HeatSourceDTO.FlagPersistent`; mock volcano sources set `HeatSourceDTO.FlagMock` and are removed when real heat arrives.

- `ThermalInjectionJob` maps AUP to wrapped grid cells after subtracting `GridOriginAup`.

- Abyssal heat energy uses fixed deterministic simulation deltas.
- It does not use dispatcher frame delta.
- `GlobalQualityWeight` resolves cadence from 12 frames to 1 frame.
- `SimulationTickDeltaSeconds = cadenceFrames * 1/60`.

- `AbyssalThermodynamicsSolver` ingests/refreshes `ThermalSourceSignal` records every dispatcher tick before cadence gating, so quality-driven solver throttling does not drop producer data.
- `HeatDiffusionSolverJob` is scheduled as a dependency chain with `ThermalSolverResidualReductionJob` after each pass.
- Each scheduled pass performs one Front-to-Back double-buffer Jacobi relaxation.
- Dynamic damping: Jacobi-safe `omega` `0.55..1.0`.
- No hidden in-job `JacobiIterations` loop.
- No false `omega > 1` SOR label on the double-buffer path.
- Sanitized before diffusion/injection math: ambient, max-stable, conductivity, dissipation, source payload scalars.
- Cold/editor tuning clamps before Vault write: grid resolution, active cell count, quality, cell size, conductivity, convection, dissipation.
- It preserves Front for audit, rotates writes through Back/ShiftScratch as `GlobalQualityWeight` raises pass count, and terminal convergence makes later ping-pong passes copy forward instead of re-solving.

- Every processed thermal voxel contributes its residual to 64-byte worker slots.
- `GlobalQualityWeight` controls pass count, tolerance, cadence, and sample/read presentation quality.
- Sampled-only convergence is forbidden; it can hide divergent unsampled cells.

- `ShiftThermalGridJob` recenters the sliding window asynchronously with `UnsafeUtility.MemMove`.

- `SampleTemperatureJob` is data-provider only; damage owners consume temperature output.
- External sample handles chain into the next thermodynamics writer dependency so Front/Back swaps cannot race reader jobs.
- Low `GlobalQualityWeight` collapses toward nearest-cell reads; high weights blend through polynomial trilinear temperature/convection/conductivity sampling.
- Legacy `ThermodynamicsHazardGridRuntime` is also data-provider/updraft-only; direct heat/radiation damage emission was removed from its simulation chain.

- Thermodynamics code does not use Unity `Time.frameCount` or `Time.deltaTime` for simulation metadata or abyssal heat integration.
- Abyssal solver cadence: owner frame counter plus fixed `SimulationTickDeltaSeconds`.
- Legacy grid cadence: `_simulationFrame`.
- Thermal source signal cadence: core arena frame sequence.

## Presentation

- `ConvectionVelocityY` is a scalar fake for heat shimmer.

- VISUAL_SYNC uploads the cell buffer to `_H8AbyssalThermalCells` through double-buffered `GraphicsBuffer` pages using `LockBufferForWrite` and `UnsafeUtility.MemCpy`.

- `OnDrawGizmos` draws a blue/yellow/white slice for designer validation.

## Black Box

- `ThermalTelemetryEntry[300]` records max temperature, source count, actual convergence iteration count, solver time, energy before/after, flags, and NaN/divergent cell evidence.

- Energy audit compares Front+Injection against the final Back/ShiftScratch field and flags non-dissipation drift.

- NaN or divergent solver detection dumps immediately to `Docs/AgentLogs/Dump_THERMO_SURGEON.bin` and alias `Docs/AgentLogs/Dump_SHINOBU_203.bin`.
- Max-iteration exhaustion dumps after five consecutive capped frames.
- Vault buffer `70054` latches the last dumped fault key and resets after one clean telemetry frame to prevent repeated writes for one continuous fault.
