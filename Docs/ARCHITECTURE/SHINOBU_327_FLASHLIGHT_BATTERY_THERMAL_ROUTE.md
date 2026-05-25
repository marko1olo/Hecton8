# SHINOBU_327 Flashlight Battery Thermal Route

## Owner

`ModularEquipmentEngine` owns handheld illumination battery and diode thermal truth.

- `PlayerFlashlight` is a presentation bridge only.
- Owned presentation inputs: input, audio, beam mode, cached transform/color authoring.
- It does not drain battery, heat diodes, register with update dispatcher, run CPU flicker noise, or drive runtime `Light.intensity`.

## Route

- 1. Boot: `ModularEquipmentEngine` requests unmanaged Vault buffers for equipment state, published state, AUP samples, grid requests, wear, telemetry, tuning, specs, counters.
- 2. Simulation phase: `EquipmentStateIntegrationJob` integrates battery discharge, cold penalty, dry/wet thermal exchange, durability wear, depletion, and catastrophic overheat in Burst over unmanaged arrays.
- 3. Signal route:
  - depletion and overheat use typed `SignalBus<T>` lanes;
  - `FlashlightEvents` bridges over `SignalBus<FlashlightEventPayload>`;
  - no private native queue ownership;
  - per-generation cursor resumes after late-frame budget exhaustion without replaying snapshots;
  - catastrophic overheat sets `Broken | Depleted`, clears active state, zeros battery, emits non-visual severity.
- 4. Visual sync: `ModularEquipmentEngine` steps active/enabled `PlayerFlashlight` presentation shell from owner `LateFrameTick`.
- It publishes active beam globals and `_HectonFlashlightFailureState = (battery01, thermal01, failure01, flags)` once from owner phase.
- Flashlight shaders use those vectors for beam contribution and procedural flicker.
- 5. Proof: `FlashlightTelemetryEntry[300]` and `EquipmentTelemetryEntry[300]` remain in Vault.
- Flashlight ring records frame, tool hash, battery, thermal, depth, ambient Celsius, drain, peak heat, Burst micros, quality, tick interval, flags, hash, signals, wear.
- Fault dumps write `Docs/AgentLogs/Dump_SHINOBU_327.bin`.

## Dear Lie

Beam failure effect is not simulated on CPU and instantiates no GameObject lights.

HLSL procedural hash/triangle carriers modulate cone silt, volumetric raymarch color, and screen-space shaft contribution from owner-published scalar state.

`HectonFlashlightVoxelShadowProvider` is now an inert legacy facade only.

It does not register with dispatcher, allocate private native buffers, run physics overlap voxelization, or upload voxel textures at runtime.

Scene references fail closed while owner-phase shader path remains active.

## Scaling

`GlobalQualityWeight` controls equipment cadence and thermal-grid sampling continuously.

Low values collapse ambient sampling toward nearest-cell and slower cadence. High values allow trilinear ambient sampling and tighter cadence.

DTO layout, authority route, save identity, and signal ownership do not change.
