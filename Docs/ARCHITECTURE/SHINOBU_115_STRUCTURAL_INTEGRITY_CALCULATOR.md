# SHINOBU_115 Structural Integrity Calculator

Owner: `SHINOBU_115`

Domain: Echelon 6 Habitat & Vehicles

Runtime: `Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorRuntime.cs`

Evidence class: STATIC_SOURCE / FILESYSTEM. Unity import, Play Mode, profiler, GCMonitor, player-build, and shader visual proof absent.

Current-source note, 2026-05-20:

- SHINOBU_218 owns active depth-based structural integrity solver identity and audit trail.
- Current source truth: `Docs/ARCHITECTURE/SHINOBU_218_DEPTH_BASED_INTEGRITY_SOLVER.md`, `Docs/Tasks/Status_SHINOBU_218.md`, `Docs/AgentLogs/LOG_SHINOBU_218.md`.
- This SHINOBU_115 document is historical except matching current source.

## Source Anchors

Evidence: STATIC_SOURCE / FILESYSTEM.

Scope: cited local paths exist at capture time. No compile/import/Play/profiler/GC/player/save/platform/visual proof.

- `Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorRuntime.cs`

- `Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorTypes.cs`

- `Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs`

- `Assets/_Project/Scripts/Habitat/Deformation/Contracts/HabitatDeformationContracts.cs`

- `Assets/_Project/Scripts/Habitat/Deformation/Editor/StructuralIntegrityTunerWindow.cs`

- `Assets/_Project/Scripts/Habitat/Deformation/Editor/HullIntegrityTunerWindow.cs`

## Authority

Structural collapse truth is `IntegrityStateDTO` in `GlobalDataVault`, not Unity joints, GameObject recursion, or rigidbody mass. The solver writes scalar stress, pressure, flags, and buckling. Presentation consumes `_HectonStructuralIntegrityStateBuffer` for shader vertex deformation.

- `StructuralTuningDTO.GlobalQualityWeight` is rollback-visible quality truth.
- It controls structural cadence, SDF tap blending, telemetry, and signal profile bridging.
- Local `HomeostasisBrain.GlobalQualityWeight` is visual-only for shader presentation.
- It is not written back into structural truth.

## Vault Buffers

Historical IDs below were SHINOBU_115 capture state. Current SHINOBU_218 source moved active structural buffers to `70488-70497` to avoid collision with Environment/Celestial raw constants at `70110-70116`; see `Docs/ARCHITECTURE/SHINOBU_218_DEPTH_BASED_INTEGRITY_ROUTE_CARD.md`.

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

- `StructuralIntegrityMaterialStrengths` is a fixed 32-slot open-addressed hash table.
- Storage: Vault `NativeArray<StructuralMaterialStrengthEntry>`.
- Preserves Task 18 hash-map intent.
- Does not create persistent `NativeHashMap` owner outside `GlobalDataVault` allocation contract.

## Phases

- Simulation: dispatcher `Tick(float)` schedules Burst pressure, SDF anchor, CSR stress, collapse-signal, edge-sever, and telemetry jobs.

- Visual sync: `LateFrameTick()` completes the scheduled fence, keeps Vault locks through GPU upload and fault dump, then releases solver locks in `finally`.

- Cold: historical SHINOBU_115 player/editor route reloaded material CSV from `ColdTick()`.
- Current SHINOBU_218 source keeps boot CSV ingestion and editor-only cold polling.
- Player `ColdTick` is not registered and compiles to a no-op.

## Signals

- `BaseIntegrityEventPayload`: 64-byte unmanaged stress warning/collapse lane.

- `FluidIncursionSignal`: emitted at stress >= 0.95 once per node.

- `BaseModuleCompromisedSignal`: emitted with the same breach threshold for downstream habitat response.

`BaseModuleCompromisedSignal.QualityTier` is a Core-owned legacy profile byte.

SHINOBU_115 derives it from continuous `GlobalQualityWeight` only when populating the signal. Solver cadence, SDF anchoring, telemetry, and shader scalars keep continuous quality.

`StructuralCollapseSignalJob` serializes collapse/leak emission by ascending node index. It keeps typed unmanaged SignalBus writers and removes nondeterministic parallel enqueue order.

- Signal payload construction sanitizes non-finite node AUPs.
- It clamps grid conversion.
- It clamps outgoing float vector/depth payloads to finite meters.
- Corrupted coordinate becomes deterministic collapse state plus finite signals.
- It does not become platform-specific cast behavior.

## Black Box

Last 300 structural frames are held in `StructuralIntegrityTelemetryRing`. Non-finite math or mass collapse writes:

- `Docs/AgentLogs/Dump_SHINOBU_115.bin`

- `Docs/AgentLogs/Dump_STRUCTURAL_SURGEON.bin`

Current SHINOBU_218 owner dump path is `Docs/AgentLogs/Dump_SHINOBU_218.bin`; the structural-surgeon mirror remains for integrator crash triage.

`Hull Integrity Tuner` reads this telemetry ring through `TryGetTelemetrySample`; the graph is not a live per-node scan pretending to be forensic history.

Telemetry cursor reads are normalized before ring indexing. The writer rejects negative cursor values, wraps by actual ring capacity, and does not use `math.abs(cursor)`.

Visual-sync fault dump indexes telemetry by actual ring capacity. A corrupted negative cursor resolves to deterministic latest-slot fallback, not negative modulo.

## Determinism

- `IntegrityStateDTO` is explicit 32 bytes and validated in player/runtime builds with `UnsafeUtility.SizeOf`.
- Runtime size validation also covers `StructuralTuningDTO`, `StructuralTelemetryEntry`, `StructuralMaterialStrengthEntry`, `StructuralTelemetryDumpHeader`, `BaseIntegrityEventPayload`, and Core-owned `AbsoluteUniversePosition`.
- Field-offset reflection is editor-only audit code under `#if UNITY_EDITOR`; offset validation covers all SHINOBU DTO fields plus AUP padding, while runtime boot does not use `System.Reflection`.
- Depth pressure uses `double3` AUP deltas before float cast.

Evaluation cadence is continuous:

`framesBetweenUpdates = (int)math.lerp(1f, 30f, 1.0f - StructuralTuningDTO.GlobalQualityWeight)`

The runtime advances `_frame` every simulation tick even when the previous solver fence is alive.

A slow local client does not pause the frame counter or replace Vault-authored quality with local thermal quality.

SDF anchoring is quality-continuous.

- Low: one nearest SDF sample.
- Higher weights: six-neighbor cross taps.
- Blend gate: polynomial quality curve plus `math.smoothstep(0.25f, 0.75f, quality)`.
- Dimension inference: integer cube-volume checks.
- Rejected: float cube-root rounding.
- Terrain anchoring remains O(1); no `Physics.Raycast`.

- Pressure and SDF jobs vaccinate against corrupted AUP deltas.
- Non-finite deltas set `StateFlagNonFinite`, force stress/buckling into collapse-safe scalar values, and stop before pressure or voxel math can spread NaN.
- Pressure also rejects finite-but-impossible depth deltas above 1,000,000 meters before the `double` to `float` cast.
- SDF queries clamp finite-but-huge deltas to a bounded query extent before converting `double3` into `float3`, then reject non-finite float results before voxel indexing.

## Cascade

Collapse is a scalar state transition.

- `StructuralCollapseSignalJob` sets `StateFlagCollapsed`.
- `StructuralEdgeSeverJob` marks owned CSR edges whose source or destination is collapsed.
- Connected graph edges sever without recursion or GameObject destruction.

Collapsed stress and buckling writes sanitize non-finite prior DTO values before `math.max`, so a dead node cannot keep broadcasting NaN through state hash, telemetry, or shader upload.

Active node scheduling is bounded by states, node AUPs, and `CsrOffsets.Length - 1`.

Graph and edge jobs guard `index + 1` before CSR offset reads. Partial mock/CI buffers fail closed instead of over-indexing.

## Editor Facade

Menu paths:

- `Hecton-8/Habitat/Hull Integrity Tuner`

- `Hecton-8/Habitat/Structural Integrity Calculator` legacy alias

- The facade edits Vault-backed `StructuralTuningDTO` values only when the solver fence is not alive, and tuning writes lock `StructuralIntegrityTuning`.
- `RegenerateMockGraph()` does not complete active simulation work; it returns while `_jobScheduled != 0` and reports success/failure to the tuner status line.
- Runtime includes a literal `OnDrawGizmos` hook for green/yellow/flashing-red module stress cubes, capped to 512 nodes and driven by AUP deltas from sea level.

The UI exposes `Authoritative Quality Weight`; this edits the rollback-visible tuning DTO, not local hardware quality.

`StructuralIntegrityCalculatorRuntime.ActiveRuntime` is published only after `TryInitialize()` succeeds. Failed boot does not leave editor tooling pointed at a half-initialized facade.

- Editor/facade reads acquire scoped Vault locks before resolving state, AUP, tuning, or telemetry aliases.
- Runtime `OnDrawGizmos` and the SceneView heatmap use `TryBuildEditorRelativePosition()` to subtract the local origin, clamp the relative AUP delta, and skip non-finite post-cast positions.
- The status label is changed-only and throttled; the telemetry graph remains a buffer read path, not a per-update string-formatting path.

## Alias And Dependencies

All structural Burst jobs use `[NoAlias]` on job-owned `NativeArray` fields and signal writers. The schedule chain is linear:

`DepthPressure -> SdfAnchor -> GraphStress -> CollapseSignals(serial ascending node scan) -> EdgeSever -> Telemetry`

- Before scheduling, the runtime locks every Vault buffer captured by the job chain: states, node AUPs, CSR offsets, CSR destinations, edge flags, telemetry ring, telemetry cursor, tuning, and optional SDF.
- Cold boot/mock/CSV jobs are synchronous only when no solver fence is alive, and they still lock the Vault buffers whose pointers they pass to immediate jobs.

## GPU Upload

Structural state uploads use double-buffered `GraphicsBuffer` instances with `GraphicsBuffer.UsageFlags.LockBufferForWrite`.

`_HectonStructuralIntegrityStateBuffer` is the shader-facing Dear Lie lane; gameplay collapse remains scalar Vault truth. `_HectonStructuralIntegrityParams.z` may carry local visual quality from `HomeostasisBrain`, but cannot feed back into stress or collapse.

## Assembly Boundary

Runtime asmdef references only Core, Core.Contracts, Core.Memory, local Deformation contracts, and Unity packages.

It does not reference sibling World, flood, construction graph, netcode, audio, or VFX runtime assemblies. Those owners receive signals or Vault snapshots.

`StructuralIntegrityCalculatorTypes.cs` uses explicit alias `AbsoluteUniversePosition = Hecton8.World.AbsoluteUniversePosition`.

- Scope: Core-owned AUP signal payload only.
- Source: `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`.
- Compiled by parent `Hecton8.Core.asmdef`, not sibling World runtime asmdef.

## Binary Payload Status

`BINARY_PAYLOAD_INTEGRATION_LEDGER.md` was checked during SHINOBU_218 polish on 2026-05-20.

- SHINOBU_115 claims no generated binary payload.
- Cold designer tuning input exists: `Docs/Data/hull_materials.csv`.
- No Unity import, live CSV hot-reload, or binary payload runtime proof claimed here.

## Proof Status

Static source hardened. Compile, Unity import, profiler, GCMonitor, and runtime proof remain pending in `Docs/Tasks/Status_SHINOBU_115.md`.
