# FLOW_FIELD_MATH.md

Date: 2026-05-07

Status: PENDING VERIFICATION

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not GPU dispatch, shader import, Frame Debugger, profiler, or player-build proof.

- `Assets/_Project/Scripts/HectonFluidEngine.cs`

- `Assets/_Project/Scripts/FlowFieldVisualizer.cs`

- `Assets/_Project/Scripts/FlowFieldProfile.cs`

- `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs`

- `Assets/_Project/Art/Shaders/AbyssalFlowField.compute`

- `Assets/_Project/Data/DefaultFlowFieldProfile.asset`

# HECTON-8 | GPU Abyssal Flow Volume Math | 2026-04-29

## Scope

Live 3D abyssal flow-volume owner path:

- `Assets/_Project/Scripts/HectonFluidEngine.cs`

- `Assets/_Project/Art/Shaders/AbyssalFlowField.compute`

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.

- Historical actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json` (historical snapshot only; do not use for current counts or proof).

- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.

- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.

- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.

## Historical 2026-05-04 Boundary

- This is the intended/source-backed flow-field architecture, not proof that the compute shader imports cleanly or that the current scene has no flow/weather console errors.

- Runtime ownership and verification must be reopened in `HectonFluidEngine.cs`, `GlobalWeatherDirector`, and the Unity console before surgery.

This replaces older flat / 2D interpretations that only survived in historical audit notes and prompt dumps.

## Owner Graph

1. `GlobalWeatherDirector` publishes `WeatherRuntimeSnapshot`.

2. `HectonFluidEngine.FixedTick` consumes that snapshot and dispatches the GPU abyssal flow kernels.

3. `AbyssalFlowField.compute` updates the legacy structured diagnostic field and the 32x32x32 3D wake texture.

4. High-fidelity dispatches inject submarine wake, thermal updrafts, and optional bounded vortex impulses into the texture.
5. CPU consumers use analytical current sampling; no abyssal flow texture readback is performed.

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

### 3D Flow Texture

`RWTexture3D<float4> _AbyssalFlowTextureWrite`

- `xyz`: world-space velocity, stored in `GraphicsFormat.R16G16B16A16_SFloat`

- `w`: bounded turbulence magnitude

- resolution: `32x32x32`

- world coverage: `100 m` around the current observer

## Dispatch Topology

Kernel order per fixed-step dispatch:

1. `UpdateAbyssalFlowField` writes the legacy structured buffer for older GPU consumers.

2. `UpdateAbyssalFlowTexture` writes base curl noise and decay into the ping-pong 3D texture.

3. `InjectAbyssalWakeTexture` runs when `GlobalQualityWeight` and budget pressure admit wake injection and a valid submarine wake payload exists.
4. `InjectAbyssalVortexTexture` runs when `GlobalQualityWeight` and budget pressure admit vortex injection for queued large-body impulses.

At minimum budget, wake, geyser, and vortex injection converge to zero contribution; the texture is overwritten with base curl only. Intermediate budgets blend injected contribution by the same continuous weight.

## 3D Flow Volume Math

### Base Weather Bias

Each node starts from the weather-owned current:

```text

flow = weatherCurrent.xyz

thermalIntensity = max(weatherParams.x, 0)

```

- Abyssal field input: the macro weather vector already resolved from the global weather bitmask.

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

## Removed 3x3x3 Pressure-Wave Detection

`DetectBiolumeSurge` and the raw aggregate mask are not live in abyssal flow shader or `HectonFluidEngine`.

If revived, the weather feature needs a separate contract, explicit verification, and no violation of the no-readback abyssal wake contract.

Historical math:

1. Read the center velocity.

2. Visit all `26` neighbors in the surrounding `3x3x3` cluster.

3. Compute delta velocity against the center.

4. If any neighbor exceeds the threshold, atomically OR bit 5 into the aggregate mask.

Why the historical path used squared distance:

- avoids `sqrt` in the innermost loop

- matches the project mandate to prefer squared comparisons

- kept the threshold equivalent to `length(deltaVelocity) > 8.0`

Live replacement:

- visible turbulence is carried by the 3D flow texture through curl, geyser, wake, and vortex passes

- no aggregate mask is allocated

- no aggregate readback is allowed from this abyssal wake path

## CPU Readback Contract

`HectonFluidEngine` does not read the whole flow buffer back to CPU.

CPU-visible path:

1. `HectonFluidEngine.TrySampleModAbyssalFlow` resolves weather, authored current, and giant-wake current analytically.

2. `SubmarineFluidDynamics` feeds that vector into the drag job as fluid-relative velocity.

3. GPU consumers sample `_AbyssalFlowFieldTexture` or the legacy structured buffer directly on the GPU.

There is no CPU readback for the 3D texture or full flow field. Existing non-flow GPU readbacks in other systems are outside this contract.

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
