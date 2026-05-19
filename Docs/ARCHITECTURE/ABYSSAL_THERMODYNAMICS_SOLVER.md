# Abyssal Thermodynamics Solver

Date: 2026-05-19
Owner: SHINOBU_117
Status: pending Unity compile because CPU gate blocked build at 100 percent.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Runtime Contract
- `ThermalCellDTO` is `[StructLayout(LayoutKind.Explicit, Size = 16)]`.
- Offsets: temperature `0`, conductivity `4`, convection Y `8`, flags `12`.
- Main buffers live in `GlobalDataVault` under `BufferID.AbyssalThermal*`.
- Hot solver jobs use raw pointers and `NativeArrayOptions.UninitializedMemory`.
- Heat producers route through `IThermodynamicsService.TryInjectTransientHeatSource`; `HectonHazardManager` is not the heat authority.

## Simulation
- Heat producers write `HeatSourceDTO` records.
- `ThermalInjectionJob` maps AUP to wrapped grid cells after subtracting `GridOriginAup`.
- `HeatDiffusionSolverJob` is scheduled as a dependency chain. It preserves Front for audit and rotates writes through Back/ShiftScratch as `GlobalQualityWeight` raises Jacobi pass count.
- `ShiftThermalGridJob` recenters the sliding window asynchronously with `UnsafeUtility.MemMove`.
- `SampleTemperatureJob` is data-provider only; damage owners consume temperature output.

## Presentation
- `ConvectionVelocityY` is a scalar fake for heat shimmer.
- VISUAL_SYNC uploads the cell buffer to `_H8AbyssalThermalCells`.
- `OnDrawGizmos` draws a blue/yellow/white slice for designer validation.

## Black Box
- `ThermalTelemetryEntry[300]` records max temperature, source count, iterations, solver time, energy before/after, flags, and NaN cell.
- Energy audit compares Front+Injection against the final Back/ShiftScratch field and flags non-dissipation drift.
- NaN detection dumps the ring to `Docs/AgentLogs/Dump_THERMO_SURGEON.bin`.
