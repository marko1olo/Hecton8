# Habitat Fluid Incursion

Owner: `SHINOBU_119`

Source anchors: `Assets/_Project/Scripts/Physics/HabitatFluidIncursionDirector.cs`, `Assets/_Project/Scripts/Physics/HabitatFluidIncursionJobs.cs`, `Assets/_Project/Scripts/Physics/HabitatFluidIncursionContracts.cs`.

## Runtime Truth

Flooding is scalar state, not particles and not rising planes. The authoritative room state is `FluidCompartmentDTO`, a 64-byte explicit-layout buffer in `GlobalDataVault`:

- `LocalCenterOfMass`
- `NodeHashID`
- `CurrentWaterVolume`
- `MaxWaterVolume`
- `WaterLevelHeight01`
- `Flags`
- explicit padding through byte 63

Jobs mutate the DTO with raw pointers and `UnsafeUtility.AsRef`. The buffer can be snapshotted by rollback systems with blind memcpy.

Legacy trigger/component water authority is not valid for this route.

Facts:

- `BaseModule` interior triggers are player occupancy only.
- `BaseAirlock` does not cache/call `BuoyancyObject`.
- `BuoyancyObject` no longer owns dry-zone ref-count mutation methods.
- Dry interior consumers must migrate to base-transition signals.
- Component water suppression must not return.

## Solver

- `FluidIngressJob` applies Torricelli ingress from `IntegrityStateDTO` breach area and AUP-local water depth.
- `FluidBfsPressureEqualizationJob` traverses CSR topology and moves conserved scalar volume across unsealed conductive edges using surface-head difference: AUP-local compartment Y delta plus fill-height delta.
- Edge conductance is a scalar Vault lane, sealed edges resolve to zero conductance, and transfer volume is quantized to signed milliliters with a per-edge remainder lane to prevent closed-loop drift.
- `GlobalQualityWeight` drives solver iterations continuously from 1 to 5 and BFS visit budget from 16 to 128 nodes.

Runtime cadence is continuous.

- Director accumulates deterministic fixed delta.
- Solver window lerps from `5Hz` to about `62.5Hz` by `smoothstep(GlobalQualityWeight)`.
- Accumulated delta enters ingress/equalization.
- Minimum quality drops solver calls without deleting water volume.

## Bridges

- Physics mass route:
  - `SubmarineFloodStateSignal` plus unmanaged `PhysicsEventPayload`;
  - enqueue through `PhysicsEventBus.NotifyFloodMassShift`;
  - no flood-specific interface listener array.
- Vehicle dynamics consumes mass into `SubmarineMassProperties`; existing AddedMass tensor job applies roll/heel math.
- Audio: `SignalBus<HabitatFloodAcousticMuffleSignal>` with bounded muffle scalar, cutoff, and transmission byte. `AcousticZoneEvents` remains the audio-domain facade/consumer surface; the flood director does not reference the audio assembly directly.
- Visual: `_H8HabitatFluidWaterlines` StructuredBuffer containing per-room fill/waterline/wobble scalars.
- Debug: 300-entry `FluidIncursionTelemetryEntry` ring plus per-compartment telemetry snapshot; invalid state dumps `Docs/AgentLogs/Dump_SHINOBU_330.bin`.

## Boundaries

The director exposes `InstallCsrTopology` and `ApplyCompartmentVolumeCsv` bridge methods. It does not own habitat construction, integrity deformation, audio DSP, shader authoring, or vehicle hydrodynamics.

Editor/debug facades read `ShinobuFluidCompartmentTelemetry`, not live front/back solver buffers. Editor reads and tuning writes are suppressed while the Vault reports active Burst locks.

Static guard: `Tools/OOP_Water_Trigger_Scanner_SHINOBU_330.py` scans Habitat/Vehicles, HFI runtime, `BaseModule`, `BaseAirlock`, `BuoyancyObject`, and `SubmarineFluidDynamics`; latest report is `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_330.json` with `findingCount=0` across 19 files.

## Route Cards

```text

Route ID: SHINOBU_119_FLUID_VAULT_STATE

Date: 2026-05-19

Owner: SHINOBU_119

Owner domain: Habitat & Vehicles / Fluid Incursion

Owning file/system: HabitatFluidIncursionDirector

Problem: Flood truth must be visible to Burst jobs, rollback snapshots, editor telemetry, shader upload, and postmortem dumps.

Why owner-local data is insufficient: CSR topology, breach input, render upload, editor tuning, and rollback need stable native handles across phase boundaries.

Why direct caller/owner interface is insufficient: Jobs need raw native buffers, not managed calls.

Instrument: GlobalDataVault / IDataVault; Black-box/telemetry route.

R48 exact HFI route-card fields:

Producer/consumer phase: FixedTick/POST_FIXED producers write Vault state and bridge packets; PostFixed physics/audio and VISUAL_SYNC render/editor consumers read snapshots/signals in owned phases.

Cadence/capacity: quality-scaled 5Hz..50Hz solver cadence; 256 compartments, 1024 directed edges, 300 telemetry frames, and bounded SignalBus event capacities.

Overflow/failure: clamp invalid water, set fault flags, use bounded event overflow/circuit-breaker telemetry, and dump black-box targets only after timestamped runtime triggers.

Shutdown/disposal: Vault owns native memory; director releases graphics buffers, unlocks handles, and unregisters dispatcher hooks.

Review disposition: `YELLOW / STATIC_SOURCE_ONLY` until compile/import/runtime/profiler proof exists.

Producer phase: FixedTick scheduled jobs; PostFixed telemetry stamp.

Consumer phase: PostFixed bridge publish, VISUAL_SYNC render upload, editor debug, rollback snapshot.

Cadence: quality-scaled 5Hz to fixed-frame solver; telemetry per solved frame.

Expected max events/reads per frame: 256 compartment DTO reads/writes, 1024 CSR edges, 1 summary, 1 telemetry ring write.

GlobalQualityWeight behavior: cadence lerps 5Hz..50Hz by q^2; BFS iterations round(lerp(1,5,q)); shader wobble lerps by q.

Payload/data shape: unmanaged explicit-layout DTOs, 16/32/64 bytes.

Managed fields present: no

UnityEngine.Object fields present: no

Layout proof: FluidCompartmentDTO 64 bytes exact; telemetry and mass DTOs 64 bytes.
Capacity: 256 compartments, 1024 directed edges, 300 telemetry frames.

Overflow/failure: clamp invalid water, set NonFinite flag, dump black box once.

Telemetry fields: frame, state hash, water mass, flooded/breached counts, solver wall microseconds, invalid count.

Black-box fields: same telemetry ring dumped to Dump_SHINOBU_330.bin.
Profiler marker: pending Unity profiler proof.

GC proof required: GCMonitor/profiler 0 B during solver cadence.

Shutdown/disposal: Vault owns native memory; director releases only GraphicsBuffers and unlocks locked handles.

Scene unload behavior: OnDisable completes active handoff if scheduled, unlocks, unregisters from dispatcher.

Stale-handle behavior: every access resolves VaultBufferHandle and checks IsCreated/Length.

Rejected alternatives: owner-local native arrays; concrete graph/integrity references; managed queues; physical particles.

Why this does not increase global monolith risk: one owner owns one narrow DTO set; bridge consumers get snapshots/signals, not mutation authority.

H-Phi impact expected: static DataVault surface grows by one domain-owned buffer family.

Proof required before GREEN: Unity import, Play Mode flood mock, profiler/GC, Frame Debugger shader buffer.

Reviewer: pending integrator

Status: YELLOW / STATIC_SOURCE_ONLY

```

```text

Route ID: SHINOBU_119_FLUID_MASS_SHIFT

Date: 2026-05-19

Owner: SHINOBU_119

Owner domain: Habitat & Vehicles / Fluid Incursion

Owning file/system: PhysicsEventBus bridge

Problem: Flood water mass must reach vehicle physics without direct Rigidbody mutation.

Why owner-local data is insufficient: submarine dynamics owns application of torque/sink response.

Why direct caller/owner interface is insufficient: physics listeners are phase-drained through existing PhysicsEventBus.

Instrument: SignalBus<PhysicsEventPayload> through PhysicsEventBus typed facade.

Producer phase: PostFixed after flood jobs complete.

Consumer phase: physics event drain.

Cadence: mass publish interval, default 0.1s.

Expected max events/reads per frame: 1 flood mass event per director per publish window.

GlobalQualityWeight behavior: lower quality can publish coarser intervals through tuning; payload shape unchanged.

Payload/data shape: unmanaged PhysicsEventPayload with FloodMassShift discriminator.

Managed fields present: no

UnityEngine.Object fields present: no

Layout proof: existing PhysicsEventPayload lane.

Capacity: existing PhysicsEventBus capacity.

Overflow/failure: existing PhysicsEventBus overflow/circuit-breaker telemetry.

Telemetry fields: frame, source body id, fill ratio, mass, math LOD, flags.

Black-box fields: flood telemetry ring records same source frame and mass.

Profiler marker: pending.

GC proof required: PhysicsEventBus/SignalBus profiler proof.

Shutdown/disposal: existing PhysicsEventBus shutdown.

Scene unload behavior: no retained Unity object payload.

Stale-handle behavior: event is value payload only.

Rejected alternatives: direct Rigidbody mass edits; direct submarine class reference.

Why this does not increase global monolith risk: one existing physics route extended with a specific event type, no new registry slot.

H-Phi impact expected: minimal; no new GlobalRegistry surface.

Proof required before GREEN: physics listener smoke and profiler/GC.

Reviewer: pending integrator

Status: YELLOW / STATIC_SOURCE_ONLY

```

```text

Route ID: SHINOBU_119_FLUID_ACOUSTIC_MUFFLE

Date: 2026-05-19

Owner: SHINOBU_119

Owner domain: Habitat & Vehicles / Fluid Incursion

Owning file/system: SignalBus<HabitatFloodAcousticMuffleSignal>

Problem: Flooded compartments must attenuate sound without fluid owning DSP.

Why owner-local data is insufficient: audio propagation owns LPF/reverb/transmission application.

Why direct caller/owner interface is insufficient: flood and audio assemblies must stay decoupled.

Instrument: SignalBus<T> first-party broadcast; AcousticZoneEvents facade configures same lane for audio domain.

Producer phase: PostFixed mass/acoustic publish window.

Consumer phase: audio/acoustic zone snapshot drain.

Cadence: default 0.1s, max 1 signal per director per publish window.

Expected max events/reads per frame: 32 capacity.

Minimum-quality frame cap: 8.

GlobalQualityWeight behavior: lower quality receives the same bounded scalar signal at coarser cadence.

Payload/data shape: 64-byte unmanaged explicit-layout raw AUP grid/local plus scalar muffle DTO.

Managed fields present: no

UnityEngine.Object fields present: no

Layout proof: 8-byte long grid fields first, float3 at 24, uint/float, byte flags, explicit padding to 64.

Capacity: 32 signals, 8 minimum-quality frame signals.
Overflow/failure: SignalBus drops/load-sheds with lane telemetry.

Telemetry fields: pending SignalBus telemetry plus flood black-box summary intensity.

Black-box fields: flood intensity and max fill in frame summary.

Profiler marker: pending.

GC proof required: SignalBus profiler/GC proof.

Shutdown/disposal: SignalBus global lane shutdown; director carries no audio object refs.

Scene unload behavior: value lane only.

Stale-handle behavior: no handle; signal snapshot lifecycle is SignalBus-owned.

Rejected alternatives: direct AudioMixer writes; audio assembly reference from flood director; acoustic ray simulation.

Why this does not increase global monolith risk: typed lane, one payload, no managed data, no registry slot.

H-Phi impact expected: one typed signal lane.

Proof required before GREEN: audio consumer smoke, profiler/GC, overflow counters.

Reviewer: pending integrator

Status: YELLOW / STATIC_SOURCE_ONLY

```
