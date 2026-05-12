# LOG_PHYSICS_FLUIDS

## 2026-05-11 - HYDRO_ENGINEER / PHYSICS_FLUIDS
Status: PENDING VERIFICATION

What was wrong:
- Fluid current direction had cheap dominant-axis behavior without enough organic variation.
- Deep submerged bodies could still pay wave sampling with only a 0.5m margin.
- Water-entry events stayed local instead of publishing acoustic `ImpactSignal` packets.
- Global tide phase used local time instead of AUP/celestial time.
- GPU buoyancy constants were loose globals instead of an explicit constant buffer group.
- Splash variation had no AUP-based deterministic hash lane.
- Final compile is blocked by non-hydro concurrent edits.

What was done:
- Verified and preserved 32x32x32 prebaked curl vector lookup in `HectonAnalyticalFlowField.SamplePrebakedVectorCurrent`.
- Restored exact `math.normalize` only for high-tier hero/player exact-normal buoyancy; debris remains dominant-axis.
- Changed whirlpool tangent to `math.cross(up,toCenter)` with `rsqrt` length.
- Raised CPU and GPU deep-submerged Gerstner early-out to object height + wave envelope + 5m.
- Added AUP-driven tide phase through `ResolveAbsoluteUniverseTideTimeSeconds`.
- Published fluid and submarine splash impacts to `GlobalSignals` as `ImpactSignal`.
- Wrapped GPU buoyancy wave params in `HectonGpuBuoyancyConstants`.
- Set `BuoyancyParams` to explicit 96-byte layout.
- Added AUP/sample-index LCG splash gain and verified no `UnityEngine.Random`.

3D vector noise lookup logic:
```csharp
float3 aupCell = (worldPos + vectorNoiseAupOffset) * vectorNoiseInvCellSize;
int cellMask = math.select(VectorNoiseLowTierMask, VectorNoiseMask, highTier);
int x = FastFloorToInt(aupCell.x) & cellMask;
int y = FastFloorToInt(aupCell.y) & cellMask;
int z = FastFloorToInt(aupCell.z) & cellMask;
int index = x | (y << VectorNoiseSliceShift) | (z << VectorNoisePlaneShift);
float3 highSample = vectorNoiseField[index];
float3 lowSample = DominantAxisOrDefault(highSample, new float3(1f, 0f, 0f));
float3 vectorSample = math.select(lowSample, highSample, highTier);
```

Cinematic cheats used:
- Prebaked vector-noise lookup instead of runtime CPU fluid noise.
- Triangle-wave current modulation instead of sine/noise turbulence.
- Propwash cone force instead of fluid displacement.
- Cross-product whirlpool fake instead of vortex simulation.
- Full-buoyancy deep early-out instead of Gerstner sampling.
- 16-sample viscosity LUT instead of dynamic curve evaluation.
- LCG splash gain instead of random variation.

Exact microseconds saved:
- Vector lookup vs CPU noise: 20-80 us per 100 sampled bodies.
- Math LOD normals and propwash/whirlpool cheats: 17-51 us per active cluster.
- Deep early-out and capped drag: 12-37 us in heavy submerged sets.
- Bounded flood BFS: 15-60 us on compartment-heavy submarines.
- Late-swap nonblocking schedule: 50-200 us stall avoidance during spikes.
- Total estimated worst-spike savings: 120-430 us on i3/MX350. PENDING VERIFICATION until Unity profiler capture.

Verification:
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` succeeded earlier with 0 errors and 0 warnings after hydro namespace fix.
- Later `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` failed outside hydro with 47 warnings and 11 errors in construction/save dependencies.
- No Unity import/shader compiler pass was available in this run; GPU cbuffer syntax remains PENDING VERIFICATION.
