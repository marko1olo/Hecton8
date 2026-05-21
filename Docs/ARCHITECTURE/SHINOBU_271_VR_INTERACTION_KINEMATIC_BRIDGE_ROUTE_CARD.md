# SHINOBU_271 VR Interaction Kinematic Bridge Route Card

Route ID: `SHINOBU_271_VR_INTERACTION_KINEMATIC_BRIDGE`
Date: 2026-05-21
Owner: `SHINOBU_271`
Owner domain: Echelon 4 Player / Kinematics / VR Interaction Bridge
Owning file/system: `PhysicalHandController`, `VRInteractionKinematicBridge`, `VRPhysicsInquisition`
Status: `YELLOW / STATIC DOTNET COMPILE GREEN; PENDING UNITY IMPORT, PLAY MODE, PROFILER, AND DEVICE PROOF`

Problem: VR hands must stop using SpringJoint/ConfigurableJoint/Rigidbody hand truth and resolve controller motion through deterministic AUP, Voxel SDF, and socket math.

Why owner-local data is insufficient: the bridge consumes player/controller pose, immutable Voxel SDF data, interaction sockets, and emits presentation matrices plus combat velocity signals. Keeping those facts inside a MonoBehaviour would create duplicate truth and rollback ambiguity.

Why direct caller/owner interface is insufficient: Voxel SDF, interaction sockets, skinned hand presentation, telemetry, and velocity damage consumers are separate domains. The bridge needs unmanaged DTO lanes and typed signal output without concrete sibling-domain references.

Instrument:
- `GlobalRegistry` cold service/interface for `IDataVault` and `IVoxelSonarSdfReadModel` only during cold bootstrap.
- `GlobalDataVault / IDataVault` for hand states, previous states, controller matrices, sockets, tuning, resolved matrices, telemetry, and telemetry cursor.
- `SignalBus<CombatDamageSignal>` for velocity impact output.
- Black-box telemetry route.

Producer/consumer phase:
- Producer: existing `PhysicalInteractionHandler.FixedTick()` supplies controller runtime pose to `PhysicalHandController.StepFixed()`.
- Bridge phase: controller matrix ingestion in PRE_SIMULATION semantics, SDF/socket/velocity solve in SIMULATION semantics, telemetry/fault state in POST_SIMULATION semantics.
- Consumer: hand presentation reads `ResolvedHandMatrices`; combat consumes `CombatDamageSignal`; editor gizmo/tuner reads Vault views outside runtime hot proof.

Cadence/capacity:
- Two hands per fixed tick.
- `VRHandStateDTO[2]`, `PreviousHandStates[2]`, `VRControllerMatrixDTO[2]`, `float4x4[2]`.
- `VRInteractionSocketDTO[128]`.
- `VRInteractionTelemetryEntry[600]`, representing 300 complete two-hand frames.

Expected max events/reads per frame:
- Up to 2 controller matrix writes, 2 hand state writes, 2 previous-state writes, 2 matrix writes, and 2 telemetry row writes per fixed tick.
- Socket scan reads all active rows up to 128 to keep interaction truth independent of quality.
- At most one `CombatDamageSignal` per hand per Unity frame from this bridge.

GlobalQualityWeight behavior:
- `ResolveQualityIterationHint()` maps `GlobalQualityWeight` continuously from 2 to 8 as a presentation/telemetry hint.
- `ResolveIterationCount()` for authoritative SDF hand truth returns the deterministic 8-step fence so local thermal state cannot fork rollback hand AUPs.
- Low quality consumers may collapse visual hand polish, optional haptic cadence, or telemetry interpretation to the 2-4 step hint while preserving DTO layout, BufferIDs, authority route, socket truth, and rollback state.

Accessor purity:
- `CacheKinematicBridgeCold()` is the only route that can call `GlobalRegistry` and create/grow Vault lanes.
- `RefreshKinematicBridgeExisting()` uses only cached `IDataVault` plus `TryResolveExisting`.
- Runtime read helpers `ReadRightHandAttachment()` and `ReadOpposingHandAttachment()` do not publish signals, sync scene state, allocate/grow buffers, complete jobs, mutate global state, or search the scene.
- Fault dump API is explicitly named `DumpTelemetryFaultOnly`, not a read accessor.

Payload/data shape:
- Managed fields present: no in runtime DTO payloads.
- UnityEngine.Object fields present: no in runtime DTO payloads.
- Layout proof: `VRHandStateDTO` is explicit 64 bytes: `RawControllerAUP@0` 24B, `ResolvedHandAUP@24` 24B, `Velocity@48` 12B, `InteractionFlags@60` 4B.
- Other explicit payloads: `VRControllerMatrixDTO=128`, `VRInteractionSocketDTO=128`, `VRInteractionTuningDTO=128`, `VRInteractionTelemetryEntry=128`.

Overflow/failure:
- SDF dimensions are validated with 64-bit product math before byte indexing.
- Non-finite origin, state, velocity, SDF values, or telemetry fault dumps fail closed.
- Over-budget frames are telemetry-flagged only; NaN/non-finite state marks a pending fault-only black-box dump, flushed from late-frame/teardown instead of fixed-step.
- Missing Vault/SDF fails closed to transform-only hand target with no hot GlobalRegistry polling.

Telemetry fields:
- frame, state hash, flags, CPU microseconds, raw controller AUP, resolved hand AUP, velocity, max penetration, surface normal, socket ID, solver iterations, hand index, marker.

Black-box fields:
- same fixed telemetry rows, dumped to `Docs/AgentLogs/Dump_SHINOBU_271.bin`.

Profiler marker:
- pending. Static CPU timing uses `Stopwatch.GetTimestamp()` in the controller bridge and writes microseconds into telemetry.

Compile proof:
- `dotnet build Hecton8.slnx --no-restore -nologo -v:minimal -maxcpucount:1 /nr:false /p:UseSharedCompilation=false /p:GenerateFullPaths=true` returned `EXIT_CODE=0` in `Docs/AgentLogs/Build_SHINOBU_271_solution_loop13_08.log` with `7 Warning(s)`, `0 Error(s)`.

GC proof required:
- Unity Profiler / GCMonitor capture in Play Mode. Static source proof only exists now.

Shutdown/disposal:
- Bridge persistent runtime lanes are owned by `GlobalDataVault`.
- `PhysicalHandController` disposes only its pre-existing finger spherecast native buffers; it does not own or dispose Vault lanes.

Scene unload behavior:
- controller clears cached Vault/SDF references on destroy; Vault owner controls lane lifetime.
- transform-only runtime proxy is destroyed by `PhysicalHandController`.

Stale-handle behavior:
- fixed-step bridge uses `TryResolveExisting` on cached Vault only.
- if handles are absent or stale, it writes no new global state and updates only the local transform target.

Rejected alternatives:
- SpringJoint or ConfigurableJoint hand coupling.
- Rigidbody.MovePosition/AddForce hand truth.
- hot `GlobalRegistry` polling.
- scene-search read accessors.
- trigger-collider sockets.
- absolute world float math.
- same-frame tiny job schedule/Complete loop for two hands.

Why this does not increase global monolith risk:
- no new `GlobalRegistry` slot.
- no direct sibling runtime assembly reference was added.
- SDF is consumed through `Hecton8.Core.Contracts.IVoxelSonarSdfReadModel`.
- BufferIDs are documented and route-carded; socket and hand truths remain bounded unmanaged lanes.

H-Phi impact expected:
- lower PhysX hand authority and object hierarchy pressure; local numeric BufferID debt remains documented until central enum migration is authorized.

Proof required before GREEN:
- Unity import and Console clear.
- Play Mode GCMonitor 0 B/frame capture under active VR hand motion.
- profiler proof under low/mid/high/ultra quality.
- SDF contact scene proof at origin and far AUP sectors.

Reviewer: integrator/player-kinematics authority required.
Review disposition: `YELLOW`
