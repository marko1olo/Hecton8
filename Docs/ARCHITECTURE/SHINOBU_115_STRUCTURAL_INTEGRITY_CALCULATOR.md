# SHINOBU_115 Structural Integrity Calculator

Owner: `SHINOBU_115`
Domain: Echelon 6 Habitat & Vehicles
Runtime: `Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorRuntime.cs`
Evidence class: STATIC_SOURCE / FILESYSTEM. This document is not Unity import, Play Mode, profiler, GCMonitor, player-build, or shader visual proof.

Current-source note, 2026-05-20: SHINOBU_218 now owns the active depth-based structural integrity solver identity and audit trail. For current source truth, use `Docs/ARCHITECTURE/SHINOBU_218_DEPTH_BASED_INTEGRITY_SOLVER.md`, `Docs/Tasks/Status_SHINOBU_218.md`, and `Docs/AgentLogs/LOG_SHINOBU_218.md`. This SHINOBU_115 document remains historical except where it matches current source.

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM. These anchors prove only that the cited local paths exist at this capture time; they are not compile, Unity import, Play Mode, profiler, GC, player-build, save/load, platform, or visual proof.

- `Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorRuntime.cs`
- `Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorTypes.cs`
- `Assets/_Project/Scripts/Habitat/Deformation/Runtime/HullIntegrityRuntime.cs`
- `Assets/_Project/Scripts/Habitat/Deformation/Contracts/HabitatDeformationContracts.cs`
- `Assets/_Project/Scripts/Habitat/Deformation/Editor/StructuralIntegrityTunerWindow.cs`
- `Assets/_Project/Scripts/Habitat/Deformation/Editor/HullIntegrityTunerWindow.cs`

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R47 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Current DOC_GLOBAL boundary (2026-05-20 R47): `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md` is the latest local static root/architecture authority-spine, runtime-wording, and counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction at `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`; R44 remains the prior internal-residue/exact-route-field/proof-wording correction at `Docs/Reports/2026-05-20_DOCUMENTATION_R44_ROOT_ARCHITECTURE_INTERNAL_RESIDUE_EXACT_ROUTE_FIELDS_LOCAL.md`; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction at `Docs/Reports/2026-05-20_DOCUMENTATION_R43_ROOT_ARCHITECTURE_ROUTE_CARD_AND_COUNTER_RESIDUE_LOCAL.md`; R42 remains the prior counter/route-boundary/proof-label correction at `Docs/Reports/2026-05-20_DOCUMENTATION_R42_ROOT_ARCHITECTURE_COUNTER_AND_ROUTE_BOUNDARY_LOCAL.md`; R41 remains the prior global-authority/internal-residue correction at `Docs/Reports/2026-05-20_DOCUMENTATION_R41_ROOT_ARCHITECTURE_GLOBAL_AUTHORITY_INTERNAL_RESIDUE_LOCAL.md`; R40 remains the prior R38-residue/source-counter correction at `Docs/Reports/2026-05-20_DOCUMENTATION_R40_ROOT_ARCHITECTURE_R38_RESIDUE_AND_COUNTER_REFRESH_LOCAL.md`; R39 remains the prior authority-counter/proof-wording correction at `Docs/Reports/2026-05-20_DOCUMENTATION_R39_ROOT_ARCHITECTURE_AUTHORITY_COUNTER_AND_PROOF_WORDING_LOCAL.md`; R38 remains the prior source-counter drift and boundary correction at `Docs/Reports/2026-05-20_DOCUMENTATION_R38_ROOT_ARCHITECTURE_SOURCE_COUNTER_DRIFT_AND_BOUNDARY_LOCAL.md`; R37 remains the prior artifact-path/proof-wording/source-counter correction at `Docs/Reports/2026-05-20_DOCUMENTATION_R37_ROOT_ARCHITECTURE_ARTIFACT_PATHS_AND_COUNTERS_LOCAL.md`; R36 remains the prior authority-spine/domain-map correction at `Docs/Reports/2026-05-20_DOCUMENTATION_R36_ROOT_ARCHITECTURE_AUTHORITY_SPINE_LOCAL.md`; R35 remains the prior R4/counter-residue correction at `Docs/Reports/2026-05-19_DOCUMENTATION_R35_ROOT_ARCHITECTURE_R4_AND_COUNTER_RESIDUE_LOCAL.md`; R34 remains the older source-counter and physical-line refresh, superseded by R37/R38/R39/R40/R41/R42/R43/R44/R45/R46/R47 where exact counts, route-card fields, AtlasCheck status, or proof wording differ. R33 remains the prior R32-residue/source-anchor correction; R32 remains the prior R4/proof-wording correction; R31 remains the prior current-boundary propagation layer; R30 remains the prior internal-currentness layer; R29 remains the prior stale-gate/global-authority layer; R28 remains the prior interior-boundary layer; and R27 is historical source-counter/index evidence superseded by R34/R37/R38/R39/R40/R41/R42/R43/R44/R45/R46/R47.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Authority

Structural collapse truth is `IntegrityStateDTO` in `GlobalDataVault`, not Unity joints, GameObject recursion, or rigidbody mass. The solver writes scalar stress, pressure, flags, and buckling. Presentation consumes `_HectonStructuralIntegrityStateBuffer` for shader vertex deformation.

`StructuralTuningDTO.GlobalQualityWeight` is the authoritative rollback-visible quality scalar for structural cadence, SDF tap blending, telemetry, and signal profile bridging. Local `HomeostasisBrain.GlobalQualityWeight` is visual-only for shader presentation parameters and is not written back into structural truth.

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

`StructuralIntegrityMaterialStrengths` is a fixed 32-slot open-addressed hash table stored as a Vault `NativeArray<StructuralMaterialStrengthEntry>`. This preserves Task 18's hash-map intent without creating a persistent `NativeHashMap` owner outside the `GlobalDataVault` allocation contract.

## Phases

- Simulation: dispatcher `Tick(float)` schedules Burst pressure, SDF anchor, CSR stress, collapse-signal, edge-sever, and telemetry jobs.
- Visual sync: `LateFrameTick()` completes the scheduled fence, keeps Vault locks through GPU upload and fault dump, then releases solver locks in `finally`.
- Cold: historical SHINOBU_115 player/editor route reloaded material CSV from `ColdTick()`. Current SHINOBU_218 source keeps boot CSV ingestion and editor-only cold polling; player `ColdTick` is not registered and compiles to a no-op.

## Signals

- `BaseIntegrityEventPayload`: 64-byte unmanaged stress warning/collapse lane.
- `FluidIncursionSignal`: emitted at stress >= 0.95 once per node.
- `BaseModuleCompromisedSignal`: emitted with the same breach threshold for downstream habitat response.

`BaseModuleCompromisedSignal.QualityTier` is a Core-owned binary profile byte. SHINOBU_115 maps continuous `GlobalQualityWeight` into `ScalabilityTierProfiles.LowMx350/HighRtx` only at this signal boundary; structural solver cadence, SDF anchoring, telemetry, and shader scalar data keep the continuous quality value.

Collapse/leak signal emission is serialized inside `StructuralCollapseSignalJob` by ascending node index. The job still uses typed unmanaged SignalBus writers, but it no longer relies on nondeterministic parallel enqueue ordering for gameplay-visible events.

Signal payload construction sanitizes non-finite node AUPs, clamps grid conversion, and clamps outgoing float vector/depth payloads to finite meters. A corrupted coordinate becomes deterministic collapse state plus finite signals, not platform-specific cast behavior.

## Black Box

Last 300 structural frames are held in `StructuralIntegrityTelemetryRing`. Non-finite math or mass collapse writes:

- `Docs/AgentLogs/Dump_SHINOBU_115.bin`
- `Docs/AgentLogs/Dump_STRUCTURAL_SURGEON.bin`

Current SHINOBU_218 owner dump path is `Docs/AgentLogs/Dump_SHINOBU_218.bin`; the structural-surgeon mirror remains for integrator crash triage.

`Hull Integrity Tuner` reads this telemetry ring through `TryGetTelemetrySample`; the graph is not a live per-node scan pretending to be forensic history.

Telemetry cursor reads are normalized before ring indexing. The writer rejects negative cursor values, wraps by actual ring capacity, and does not use `math.abs(cursor)`.

The visual-sync fault dump path also indexes telemetry by actual ring capacity rather than nominal capacity. A corrupted negative cursor resolves to a deterministic latest-slot fallback instead of a negative modulo read.

## Determinism

`IntegrityStateDTO` is explicit 32 bytes and validated in player/runtime builds with `UnsafeUtility.SizeOf`. Runtime size validation also covers `StructuralTuningDTO`, `StructuralTelemetryEntry`, `StructuralMaterialStrengthEntry`, `StructuralTelemetryDumpHeader`, `BaseIntegrityEventPayload`, and Core-owned `AbsoluteUniversePosition`. Field-offset reflection is editor-only audit code under `#if UNITY_EDITOR`; offset validation covers all SHINOBU DTO fields plus AUP padding, while runtime boot does not use `System.Reflection`. Depth pressure uses `double3` AUP deltas before float cast.

Evaluation cadence is continuous:

`framesBetweenUpdates = (int)math.lerp(1f, 30f, 1.0f - StructuralTuningDTO.GlobalQualityWeight)`

The runtime advances `_frame` every simulation tick even when the previous solver fence is alive. A slow local client does not pause the frame counter and does not replace Vault-authored quality with local thermal quality.

SDF anchoring is also quality-continuous. Low weight collapses to one nearest SDF sample. Higher weights blend in six-neighbor cross taps by polynomial quality curve and the current-source `math.smoothstep(0.25f, 0.75f, quality)` gate. SDF dimensions are inferred with integer cube-volume checks, not float cube-root rounding. Terrain anchoring remains O(1) and never uses `Physics.Raycast`.

Pressure and SDF jobs vaccinate against corrupted AUP deltas. Non-finite deltas set `StateFlagNonFinite`, force stress/buckling into collapse-safe scalar values, and stop before pressure or voxel math can spread NaN. Pressure also rejects finite-but-impossible depth deltas above 1,000,000 meters before the `double` to `float` cast. SDF queries clamp finite-but-huge deltas to a bounded query extent before converting `double3` into `float3`, then reject non-finite float results before voxel indexing.

## Cascade

Collapse is a scalar state transition. `StructuralCollapseSignalJob` sets `StateFlagCollapsed`; `StructuralEdgeSeverJob` then marks every owned CSR edge whose source is collapsed or whose destination points at a collapsed node. This severs connected graph edges without recursion or GameObject destruction.

Collapsed stress and buckling writes sanitize non-finite prior DTO values before `math.max`, so a dead node cannot keep broadcasting NaN through state hash, telemetry, or shader upload.

Active node scheduling is bounded by states, node AUPs, and `CsrOffsets.Length - 1`. Graph and edge jobs guard `index + 1` before reading CSR offsets, so partial mock/CI buffers fail closed instead of indexing past the Vault-owned graph.

## Editor Facade

Menu paths:

- `Hecton-8/Habitat/Hull Integrity Tuner`
- `Hecton-8/Habitat/Structural Integrity Calculator` legacy alias

The facade edits Vault-backed `StructuralTuningDTO` values only when the solver fence is not alive, and tuning writes lock `StructuralIntegrityTuning`. `RegenerateMockGraph()` does not complete active simulation work; it returns while `_jobScheduled != 0` and reports success/failure to the tuner status line. Runtime includes a literal `OnDrawGizmos` hook for green/yellow/flashing-red module stress cubes, capped to 512 nodes and driven by AUP deltas from sea level.

The UI exposes `Authoritative Quality Weight`; this edits the rollback-visible tuning DTO, not local hardware quality.

`StructuralIntegrityCalculatorRuntime.ActiveRuntime` is published only after `TryInitialize()` succeeds. Failed boot does not leave editor tooling pointed at a half-initialized facade.

Editor/facade reads acquire scoped Vault locks before resolving state, AUP, tuning, or telemetry aliases. Runtime `OnDrawGizmos` and the SceneView heatmap use `TryBuildEditorRelativePosition()` to subtract the local origin, clamp the relative AUP delta, and skip non-finite post-cast positions. The status label is changed-only and throttled; the telemetry graph remains a buffer read path, not a per-update string-formatting path.

## Alias And Dependencies

All structural Burst jobs use `[NoAlias]` on job-owned `NativeArray` fields and signal writers. The schedule chain is linear:

`DepthPressure -> SdfAnchor -> GraphStress -> CollapseSignals(serial ascending node scan) -> EdgeSever -> Telemetry`

Before scheduling, the runtime locks every Vault buffer captured by the job chain: states, node AUPs, CSR offsets, CSR destinations, edge flags, telemetry ring, telemetry cursor, tuning, and optional SDF. Cold boot/mock/CSV jobs are synchronous only when no solver fence is alive, and they still lock the Vault buffers whose pointers they pass to immediate jobs.

## GPU Upload

Structural state uploads use double-buffered `GraphicsBuffer` instances created with `GraphicsBuffer.UsageFlags.LockBufferForWrite`. `_HectonStructuralIntegrityStateBuffer` is the shader-facing Dear Lie lane; gameplay collapse remains scalar Vault truth. `_HectonStructuralIntegrityParams.z` may carry local visual quality from `HomeostasisBrain`, but it is presentation-only and cannot feed back into structural stress or collapse state.

## Assembly Boundary

Runtime asmdef references Core, Core.Contracts, Core.Memory, local Deformation contracts, and Unity packages only. It does not reference sibling Runtime assemblies for World, flood, construction graph, netcode, audio, or VFX; those owners receive typed signals or Vault snapshots.

`StructuralIntegrityCalculatorTypes.cs` uses an explicit `AbsoluteUniversePosition = Hecton8.World.AbsoluteUniversePosition` alias only for the Core-owned AUP signal payload. That type is defined in `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`, which is compiled by the parent `Hecton8.Core.asmdef`, not by a sibling World runtime asmdef.

## Binary Payload Status

`BINARY_PAYLOAD_INTEGRATION_LEDGER.md` was checked again during SHINOBU_218 polish on 2026-05-20. SHINOBU_115 does not claim a generated binary payload. Cold designer tuning input now exists at `Docs/Data/hull_materials.csv`; no Unity import, live CSV hot-reload, or binary payload runtime proof is claimed here.

## Proof Status

Static source hardened. Compile, Unity import, profiler, GCMonitor, and runtime proof remain pending in `Docs/Tasks/Status_SHINOBU_115.md`.
