# SHINOBU_115 Structural Integrity Calculator

Owner: `SHINOBU_115`
Domain: Echelon 6 Habitat & Vehicles
Runtime: `Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorRuntime.cs`
Evidence class: STATIC_SOURCE / FILESYSTEM. This document is not Unity import, Play Mode, profiler, GCMonitor, player-build, or shader visual proof.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Authority

Structural collapse truth is `IntegrityStateDTO` in `GlobalDataVault`, not Unity joints, GameObject recursion, or rigidbody mass. The solver writes scalar stress, pressure, flags, and buckling. Presentation consumes `_HectonStructuralIntegrityStateBuffer` for shader vertex deformation.

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

`StructuralIntegrityMaterialStrengths` is a fixed 32-slot open-addressed hash table stored as a Vault `NativeArray<StructuralMaterialStrengthEntry>`. This preserves Task 18's hash-map intent without creating a persistent `NativeHashMap` owner outside the `GlobalDataVault` allocation contract.

## Phases

- Simulation: dispatcher `Tick(float)` schedules Burst pressure, SDF anchor, CSR stress, collapse-signal, edge-sever, and telemetry jobs.
- Visual sync: `LateFrameTick()` completes the scheduled fence, keeps Vault locks through GPU upload and fault dump, then releases solver locks in `finally`.
- Cold: `ColdTick()` reloads material CSV into Vault scratch only when no solver fence is alive. Cold writers acquire `StructuralMutationGuardMask` before locking scratch/material/state buffers. CSV reads use shared read/write access and sequential scan so editor tooling can write the designer file without turning tuning into a runtime dependency.

## Signals

- `BaseIntegrityEventPayload`: 64-byte unmanaged stress warning/collapse lane.
- `FluidIncursionSignal`: emitted at stress >= 0.95 once per node.
- `BaseModuleCompromisedSignal`: emitted with the same breach threshold for downstream habitat response.

`BaseModuleCompromisedSignal.QualityTier` is a Core-owned binary profile byte. SHINOBU_115 maps continuous `GlobalQualityWeight` into `ScalabilityTierProfiles.LowMx350/HighRtx` only at this signal boundary; structural solver cadence, SDF anchoring, telemetry, and shader scalar data keep the continuous quality value.

## Black Box

Last 300 structural frames are held in `StructuralIntegrityTelemetryRing`. Non-finite math or mass collapse writes:

- `Docs/AgentLogs/Dump_SHINOBU_115.bin`
- `Docs/AgentLogs/Dump_STRUCTURAL_SURGEON.bin`

`Hull Integrity Tuner` reads this telemetry ring through `TryGetTelemetrySample`; the graph is not a live per-node scan pretending to be forensic history.

## Determinism

`IntegrityStateDTO` is explicit 32 bytes and validated in player/runtime builds with `UnsafeUtility.SizeOf`. Field-offset reflection is editor-only audit code under `#if UNITY_EDITOR`; runtime boot does not use `System.Reflection`. Depth pressure uses `double3` AUP deltas before float cast.

Evaluation cadence is continuous:

`framesBetweenUpdates = (int)math.lerp(1f, 30f, 1.0f - HomeostasisBrain.GlobalQualityWeight)`

SDF anchoring is also quality-continuous. Low weight collapses to one nearest SDF sample. Higher weights blend in six-neighbor cross taps by polynomial quality curve and `math.step(0.3f, quality)`. Terrain anchoring remains O(1) and never uses `Physics.Raycast`.

## Cascade

Collapse is a scalar state transition. `StructuralCollapseSignalJob` sets `StateFlagCollapsed`; `StructuralEdgeSeverJob` then marks every owned CSR edge whose source is collapsed or whose destination points at a collapsed node. This severs connected graph edges without recursion or GameObject destruction.

## Editor Facade

Menu paths:

- `Hecton-8/Habitat/Hull Integrity Tuner`
- `Hecton-8/Habitat/Structural Integrity Calculator` legacy alias

The facade edits Vault-backed `StructuralTuningDTO` values only when the solver fence is not alive, and tuning writes lock `StructuralIntegrityTuning`. `RegenerateMockGraph()` does not complete active simulation work; it returns while `_jobScheduled != 0` and reports success/failure to the tuner status line. Runtime includes a literal `OnDrawGizmos` hook for green/yellow/flashing-red module stress cubes, capped to 512 nodes and driven by AUP deltas from sea level.

## Alias And Dependencies

All structural Burst jobs use `[NoAlias]` on job-owned `NativeArray` fields and signal writers. The schedule chain is linear:

`DepthPressure -> SdfAnchor -> GraphStress -> CollapseSignals -> EdgeSever -> Telemetry`

Before scheduling, the runtime locks every Vault buffer captured by the job chain: states, node AUPs, CSR offsets, CSR destinations, edge flags, telemetry ring, telemetry cursor, tuning, and optional SDF. Cold boot/mock/CSV jobs are synchronous only when no solver fence is alive, and they still lock the Vault buffers whose pointers they pass to immediate jobs.

## GPU Upload

Structural state uploads use double-buffered `GraphicsBuffer` instances created with `GraphicsBuffer.UsageFlags.LockBufferForWrite`. `_HectonStructuralIntegrityStateBuffer` is the shader-facing Dear Lie lane; gameplay collapse remains scalar Vault truth.

## Assembly Boundary

Runtime asmdef references Core, Core.Contracts, Core.Memory, local Deformation contracts, and Unity packages only. It does not reference sibling Runtime assemblies for World, flood, construction graph, netcode, audio, or VFX; those owners receive typed signals or Vault snapshots.

`StructuralIntegrityCalculatorTypes.cs` imports `Hecton8.World` only for `AbsoluteUniversePosition`. That type is defined in `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`, which is compiled by the parent `Hecton8.Core.asmdef`, not by a sibling World runtime asmdef.

## Binary Payload Status

`BINARY_PAYLOAD_INTEGRATION_LEDGER.md` was checked on 2026-05-19. SHINOBU_115 does not claim a generated binary payload. `hull_materials.csv` remains a cold designer tuning input parsed into aligned Vault DTOs.

## Proof Status

Static source hardened. Compile, Unity import, profiler, GCMonitor, and runtime proof remain pending in `Docs/Tasks/Status_SHINOBU_115.md`.
