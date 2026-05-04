# FLOW_FIELD_MATH.md
# HECTON-8 | GPU Abyssal Flow Volume Math | 2026-04-29

## Scope

This document describes the live 3D abyssal flow-volume implementation owned by:

- `Assets/_Project/Scripts/HectonFluidEngine.cs`
- `Assets/_Project/Art/Shaders/AbyssalFlowField.compute`

## 2026-05-04 Current-State Boundary

- Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before treating this document as current runtime truth.
- This is the intended/source-backed flow-field architecture, not proof that the compute shader imports cleanly or that the current scene has no flow/weather console errors.
- Runtime ownership and verification must be reopened in `HectonFluidEngine.cs`, `GlobalWeatherDirector`, and the Unity console before surgery.

This replaces older flat / 2D interpretations that only survived in historical audit notes and prompt dumps.

## Owner Graph

1. `GlobalWeatherDirector` publishes `WeatherRuntimeSnapshot`.
2. `HectonFluidEngine.FixedTick` consumes that snapshot and dispatches the GPU abyssal flow kernels.
3. `AbyssalFlowField.compute` updates a 3D velocity field and performs surge detection on the GPU.
4. `AsyncGPUReadback` returns only the aggregate bitmask, not the full field.
5. `GlobalWeatherDirector.RegisterBiolumeSurge(4f)` latches `WeatherState.BiolumeSurge` when the GPU reports bit 5.

## Data Layout

### Heat Source Payload

`GpuHeatSourceData`

- `float3 PositionWS`
- `float Intensity`
- `float Radius`

Maximum concurrent sources: `8`.

### Flow Field Buffer

`RWStructuredBuffer<float4> _AbyssalFlowFieldResult`

- `xyz`: world-space flow vector
- `w`: turbulence-weighted magnitude snapshot for diagnostics and downstream consumers

### Aggregate Mask

`RWByteAddressBuffer _AbyssalAggregateMask`

- byte offset `0`: packed weather/surge bitmask
- bit `5`: `WeatherState.BiolumeSurge`

## Dispatch Topology

Kernel order per dispatch:

1. `ResetAbyssalFlowAggregate`
2. `UpdateAbyssalFlowField`
3. `DetectBiolumeSurge`
4. `AsyncGPUReadback.Request(_gpuAbyssalAggregateBuffer)`

Readback uses a 3-slot ring in `HectonFluidEngine`. The CPU never blocks on `GetData`; it only consumes requests after `request.done`.

## 3D Flow Volume Math

### Base Weather Bias

Each node starts from the weather-owned current:

```text
flow = weatherCurrent.xyz
thermalIntensity = max(weatherParams.x, 0)
```

This means the abyssal field inherits the same macro weather vector already resolved from the global weather bitmask.

### Surface-to-Abyss Storm Propagation

Storm energy is injected into the same 3D field on the GPU. `HectonFluidEngine` uploads:

- `windVector = WeatherRuntimeSnapshot.GlobalWindVector`
- `windIntensity = length(windVector)`
- `waveHeight = Wave0.Amplitude + Wave1.Amplitude + Wave2.Amplitude`
- `stormBlend = WeatherRuntimeSnapshot.WeatherIntensity` when bit `1` (`WeatherState.Storm`) is present
- `surfaceY = waterLevel`

Per node:

```text
depthBelowSurface = max(0, surfaceY - nodeWorldPos.y)
depthBoost        = depthBelowSurface <= 200 ? 0.4 : 0.15
stormTurbulence   = 1 + stormBlend * depthBoost

phase = dot(nodeWorldPos.xz - flowCenter.xz, windDir.xz) * 0.035
phase += timeSeconds * (0.35 + waveHeight * 0.12)

pressureWave = sin(phase) * ((waveHeight * 0.18) + (windIntensity * 0.035))

flow    += windDir * (pressureWave * stormTurbulence)
flow.xz += windDir.xz * (windIntensity * 0.05 * stormBlend)
```

Interpretation:

- top `200 m`: storm turbulence rises by `40%`
- deep abyss: storm turbulence still rises by `15%`
- the wind vector provides a coherent horizontal bias instead of isotropic noise
- wave height and wind magnitude drive the phase amplitude, so surface storms physically seed abyssal pressure-wave motion

### Heat Convection Updraft

For each active heat source:

```text
toSourceVector = heat.PositionWS - nodeWorldPos
distSq         = dot(toSourceVector, toSourceVector)
dist           = sqrt(distSq + EPSILON)

verticalVelocity = min(
    (heat.Intensity * max(1.0, thermalIntensity)) / max(dist * dist, EPSILON),
    8.0)

flow.y += verticalVelocity
```

This is inverse-square convection with an `8.0 m/s` hard cap.

### Helical Vortex Spin

The lateral spiral term is derived from the cross product around the world up axis:

```text
toSource = normalize(toSourceVector)
tangent  = normalize(cross(float3(0,1,0), toSource))
vortexStrength = heat.Intensity * (1 - dist / radius) * 0.4

flow.xz += toSource.xz * (vortexStrength * 0.5)
flow.xz += tangent.xz  * (vortexStrength * 0.5)
```

The first term pulls water toward the column. The second adds tangential swirl, producing a helical vent wake instead of a straight vertical jet.

## Thermocline Barrier

Thermocline depth is explicit. `HectonFluidEngine` uploads:

```text
thermoclineY = waterLevel - 120
```

The compute shader applies the barrier only when `WeatherState.ThermoclineActive` or `WeatherState.HaloclineActive` is set:

```text
thermoclineBand = 1 - saturate(abs(nodeWorldPos.y - thermoclineY) / 8)
flow.y = lerp(flow.y, flow.y * 0.1, thermoclineBand)
```

Interpretation:

- outside the `16 m` band, no attenuation
- at the center of the band, vertical velocity is reduced to `10%`
- horizontal flow remains untouched

This is the layered-ocean barrier that traps rising plumes and pushes motion sideways across the boundary.

## 3x3x3 Pressure-Wave Detection

The surge pass is fully 3D. For every node:

1. Read the center velocity.
2. Visit all `26` neighbors in the surrounding `3x3x3` cluster.
3. Compute delta velocity against the center.
4. If any neighbor exceeds the threshold, atomically OR bit 5 into the aggregate mask.

Live math:

```text
deltaVelocity = neighborVelocity - centerVelocity
if dot(deltaVelocity, deltaVelocity) > 64.0:
    InterlockedOr(bitmask, BIOLUME_SURGE)
```

Why squared distance:

- avoids `sqrt` in the innermost loop
- matches the project mandate to prefer squared comparisons
- keeps the threshold equivalent to `length(deltaVelocity) > 8.0`

## CPU Readback Contract

`HectonFluidEngine` does not read the whole flow buffer back to CPU.

CPU-visible path:

1. GPU writes the aggregate mask.
2. `AsyncGPUReadback.Request(_gpuAbyssalAggregateBuffer)` is queued into a 3-slot ring.
3. On later fixed ticks, completed requests are consumed.
4. If bit 5 is present, `GlobalWeatherDirector.RegisterBiolumeSurge(4f)` is called.

This keeps audio/VFX signaling asynchronous and avoids a main-thread stall.

## Historical Status

Do not use historical references to:

- old `BuildAbyssalFlowFieldJob` notes
- flat 2D ecosystem-flow assumptions
- legacy audit prompts that describe the surge check as non-GPU or non-3D

Those references are archived for provenance only. Runtime truth is the GPU compute path described above.

## Verification Targets

- `AbyssalFlowField.compute` imports with zero compute-shader console errors
- `HectonFluidEngine.cs` validates cleanly
- fresh console filter for `AbyssalFlowField` returns no current errors in this domain

Status: `PENDING VERIFICATION`
