# SHINOBU_115 Structural Integrity Calculator

Owner: `SHINOBU_115`
Domain: Echelon 6 Habitat & Vehicles
Runtime: `Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorRuntime.cs`

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

## Authority

Structural collapse truth is `IntegrityStateDTO` in `GlobalDataVault`, not Unity joints, GameObject recursion, or rigidbody mass. The solver writes scalar stress, pressure, flags, and buckling. Presentation consumes `_HectonStructuralIntegrityStateBuffer` for shader deformation.

## Vault Buffers

- `StructuralIntegrityStates = 70110`
- `StructuralIntegrityNodeAups = 70111`
- `StructuralIntegrityCsrOffsets = 70112`
- `StructuralIntegrityCsrDestinations = 70113`
- `StructuralIntegrityEdgeFlags = 70114`
- `StructuralIntegrityTelemetryRing = 70115`
- `StructuralIntegrityTelemetryCursor = 70116`
- `StructuralIntegrityTuning = 70117`
- `StructuralIntegrityMaterialStrengths = 70118`
- `StructuralIntegrityCsvScratch = 70119`

## Phases

- Simulation: dispatcher `Tick(float)` schedules Burst pressure, SDF anchor, CSR stress, collapse, edge-sever, and telemetry jobs.
- Post simulation: `LateFrameTick()` completes the job fence and uploads the double-buffered `GraphicsBuffer`.
- Cold: `ColdTick()` reloads material CSV into a Vault scratch byte buffer and applies strength constants only when no solver fence is alive.

## Signals

- `BaseIntegrityEventPayload`: 64-byte unmanaged stress warning/collapse lane.
- `FluidIncursionSignal`: emitted at stress >= 0.95 once per node.
- `BaseModuleCompromisedSignal`: emitted with the same breach threshold for downstream habitat response.

## Black Box

Last 300 structural frames are held in `StructuralIntegrityTelemetryRing`. Non-finite math or mass collapse writes:

- `Docs/AgentLogs/Dump_SHINOBU_115.bin`
- `Docs/AgentLogs/Dump_STRUCTURAL_SURGEON.bin`

## Determinism

`IntegrityStateDTO` is explicit 32 bytes and validated at runtime using `UnsafeUtility.SizeOf` plus offset checks. Depth pressure uses `double3` AUP deltas before float cast. Evaluation cadence is continuous:

`framesBetweenUpdates = (int)math.lerp(1f, 30f, 1.0f - HomeostasisBrain.GlobalQualityWeight)`

## Alias And Dependencies

All structural Burst jobs use `[NoAlias]` on job-owned `NativeArray` fields and signal writers. The schedule chain is linear:

`DepthPressure -> SdfAnchor -> GraphStress -> CollapseSignals -> EdgeSever -> Telemetry`

The only steady-state completion point is the `LateFrameTick()` visual-sync fence. Cold boot/mock/CSV jobs are synchronous only when no solver fence is alive.

## Assembly Boundary

Runtime asmdef references Core, Core.Contracts, Core.Memory, local Deformation contracts, and Unity packages only. It does not reference sibling Runtime assemblies for flood, construction graph, netcode, audio, or VFX; those owners receive typed signals or Vault snapshots.

## Binary Payload Status

`BINARY_PAYLOAD_INTEGRATION_LEDGER.md` was checked on 2026-05-19. SHINOBU_115 does not claim a generated binary payload. `hull_materials.csv` remains a cold designer tuning input parsed into aligned Vault DTOs.

## Proof Status

Static source hardened. Compile, Unity import, profiler, GCMonitor, and runtime proof remain pending in `Docs/Tasks/Status_SHINOBU_115.md`.
