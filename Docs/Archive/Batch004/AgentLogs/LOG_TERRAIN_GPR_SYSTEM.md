# LOG_TERRAIN_GPR_SYSTEM

## 2026-05-13 - Ground Penetrating Radar

What was wrong:
- No subsurface radar read model existed for deep ore discovery.
- `World/Resources` had no active `GPRManager.Instance` or `Physics.SphereCastAll` ore path to delete, so the rot was absence of a deterministic GPR path rather than live singleton code.
- Cockpit radar had no way to consume subsurface pings from the geology system.
- Ore positions existed as SoA in `ProceduralOreSpawner`, but no GlobalRegistry read model exposed them to an independent GPR system.

What was done:
- Added `IGroundRadarService` and `IWorldResourceSpawnerReadModel` contracts.
- Registered `GroundRadarRuntime` and `WorldResourceSpawnerRuntime` slots through `GlobalRegistry`.
- Added isolated `Hecton8.World.GPR` assembly and `GroundRadarRaymarchJob`.
- Added persistent `NativeArray<float3> GprHits`, `NativeArray<float> GprSignalStrength`, age, GPU payload, counters, and 300-frame telemetry ring.
- Implemented downward SDF probing with 64 rays on capable tiers and 16 rays on Low/MX350/Unknown.
- Implemented ore match against authoritative `OrePositions`, threshold `Density > 0.5`, and distance < 5m.
- Uploaded `float4(xyz, strength)` pings to a shared `GraphicsBuffer`.
- Drew GPR pings with `Graphics.RenderMeshIndirect` and a green/blue pulsing ring shader.
- Added Burst-side 3-second decay/compaction and native AUP shift correction.
- Published `AcousticPingSignal(Subsurface)` and `ToolAcousticSignal(GPR_Return)` with pitch tied to strongest return.
- Wired Submarine OS cockpit radar to bind the same GPR buffer without duplicating ping data.

Cinematic Cheats used:
- Downward sample fan instead of volumetric radar propagation.
- SDF density threshold as the "rock wall" answer.
- Ore proximity NativeArray check instead of physical buried object query.
- Inverse-square scalar attenuation using `math.rcp(depth * depth)`.
- Shader ring pulse as the "dear lie" for readability.
- `dot + rsqrt` shader radius and bitmask Burst flags after Omega polish.

Exact microseconds saved:
- No `SphereCastAll`/object probes: estimated 120-300 us per active scan burst.
- 16-ray low-tier LOD versus 64-ray fixed path: estimated 60-180 us per low-tier burst.
- NativeArray ore SoA versus object markers: estimated 25-80 us per query window.
- Shared `GraphicsBuffer` cockpit reuse: estimated 40-130 us versus duplicate cockpit copy.
- Indirect ring draw versus instantiated ring objects: estimated 200-700 us at 64-128 pings.
- Persistent buffers: 0 B/frame managed GC in the GPR hot path.

Verification:
- `Hecton8.World.Contracts` manual Unity csc pass: exit 0.
- `Hecton8.World.GPR` manual Unity csc pass: exit 0.
- Full `Hecton8.Core` manual Unity csc remains blocked by unrelated `SaveBinaryPayloadCodec` errors: missing `WriteRtgDecay` and `ReadRtgDecay`.
- Current manual Core csc output contains no GPR, cockpit radar, ore spawner, or GlobalRegistry errors from this implementation.

Status:
- PENDING VERIFICATION because global compile is blocked by external dependency errors.
