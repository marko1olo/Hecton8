# SHINOBU_41 Geological Synthesis

Status: STATIC SOURCE ORIENTATION / DATAVAULT SCRATCH+BLACKBOX SOURCE NOTES / REFLECTION ABI BLOCKED BY STALE TERRAIN ASSEMBLY / CORE BUILD BLOCKED OUTSIDE SHINOBU / UNITY RUNTIME PENDING

Owner domain: world / geology synthesis

## Source Anchors

Evidence: STATIC_SOURCE / FILESYSTEM.

Scope: cited local paths exist at capture time. No compile/import/Play/profiler/GC/player/save/platform/visual proof.

- `Assets/_Project/Scripts/World/GlobalWorldSampler.cs`

- `Assets/_Project/Scripts/World/HybridTerrainSeamJobs.cs`

- `Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs`

- Prior terrain asmdef anchor `Assets/_Project/Scripts/World/Terrain/Hecton8.World.Terrain.asmdef` is absent in the current checkout; do not cite terrain-assembly isolation until a current asmdef exists.

## Runtime Contract

- Input positions are `double3` AUP. The sampler subtracts `ActiveChunkOriginAup` before float math.

- Runtime data is injected as `NativeArray` slices through `GlobalWorldSamplerData`.

- Quality is continuous through `GlobalQualityWeight` in the 0..1 range.

- Below `GlobalQualityWeight` 0.3, height/SDF interpolation collapses to nearest lookup and raymarching trends to one step.
- Above 0.3, smoothstep ramps toward bilinear/trilinear; above 0.7, extra micro-detail is allowed.

- Normal estimation follows the same curve: below 0.3 it returns a stable cheap up-normal, then `math.lerp` blends into tetrahedron gradient normals as quality rises.

- Biome and erosion lanes collapse below `0.3`.
- Biome uses nearest hash; erosion uses nearest mask only with active micro-detail; Simplex micro-detail waits for the ramp.

- `ResolveSamplingCadenceDivisor()` maps low quality to divisor 12 (60Hz caller -> 5Hz) and high quality to divisor 1.
- Dispatchers own stale-buffer policy; sampler does not block for immediate reads.

- `FilterGlobalQualityWeight()` accepts deterministic `SimulationTickDelta` so thermal load shedding/recovery can be rate-limited without Unity `Time.deltaTime`.

- `GlobalWorldSamplerQualityState` is a 32-byte aligned scheduler DTO for quality hysteresis. `QualityWeightFilterJob` updates this state through the job graph; callers then apply `CurrentWeight` to `GlobalWorldSamplerData`.

- Missing/unloaded sectors are routed through `ActiveSectorPointers` and return HardFloor.

- Cave authority uses `SdfOverrideMask` before discarding 2D height beneath the macro surface.

- Inactive SDF/sea lanes use bounded finite sentinel `1048576f`, not `float.MaxValue`; `SanitizeResult` clamps result fields before NativeArray and telemetry exposure.

- `SampleDistanceOnly()` sanitizes every exit path, so direct raymarch/vehicle-style consumers do not need to rediscover the full `Sample()` wrapper to get bounded DTOs.

## Job Dependency API

Public scheduling wrappers are the terrain-domain dependency boundary:

- `ScheduleBatchSampler`

- `ScheduleLocalBatchSampler`

- `ScheduleGradientNormals`

- `ScheduleMockTerrainStress`

- `ScheduleMockRaymarch`

- `ScheduleQualityWeightFilter`

Each wrapper consumes a caller `JobHandle inputDeps` and returns the scheduled output handle. No SHINOBU_41 runtime code calls `JobHandle.Complete()`.

## Compile Guard

No current terrain asmdef exists at `Assets/_Project/Scripts/World/Terrain/Hecton8.World.Terrain.asmdef`. Terrain job assembly isolation is therefore not current proof.

`WorldGenerativeGeologyTerrainSeamApplier` and `HybridTerrainSeamJobs.cs` are current source anchors under the broad source tree. This document does not claim a clean terrain-only asmdef boundary.

## Data Lanes

- `TerrainSampleDTO`: 24 bytes, `float3 Normal`, `float Distance`, `uint BiomeHash`, `uint _pad0`.

- `MapMagicCellDTO`: 8 bytes, `float Height`, `short TerrainType`, `byte Wetness`, private byte pad.

- `TerrainSampleResult`: 64 bytes, extended with `BiomeHash` at offset 60.

- `GlobalWorldSamplerCounterBlock`: 64 bytes explicit layout, atomic `Value` at offset 0, 60-byte reserved padding to prevent false sharing.

- `TerrainPayloadHeaderDTO`: 64 bytes, cold OSHINO header mirror with 8-byte magic/payload fields, 32-bit dimensions/flags/scales/checksum/endian tag, and explicit padding at offsets 56 and 60.

- `GlobalWorldSamplerQualityState`: 32 bytes, current/target quality, simulation tick delta, shed/recover rates, frame, and two padding words.

- Bounded inactive-distance sentinel: `1048576f`; this is a scalar contract, not a DTO field, so no struct sizes change.

## Binary Boundary

No authoritative terrain binary is currently wired by the ledger.

- OSHINO terrain payload hydration must call `TryReadTerrainPayloadHeader(ReadOnlySpan<byte>, sourceBigEndian, out header)`.
- NativeArray receives data only after header validation.
- Header path is cold and endian-aware.
- It rejects undersized or malformed headers.
- Runtime sampling consumes DataVault slices only.

## Telemetry

- The sampler writes a 300-entry ring and dumps to `Docs/AgentLogs/Dump_TERRAIN_SPLICER.bin` when the throughput threshold exceeds 800,000 samples.
- `ResetFrameTelemetryCounters()` is the explicit PRE_SIM reset point for sample/OOB/smooth-min frame counters.
- Reserved telemetry fields carry estimated smooth-min ns, out-of-bounds count, quality weight, and biome hash.
- Every telemetry frame/warning row sanitizes a local DTO copy before entering the ring.
- Hot counters should use `CounterBlocks` when available; legacy `SampleCounter` remains fallback only.

- Normal-enabled logical queries do not always cost one terrain sample.
- `ResolveTerrainSampleCost()` charges cost 1 below the expensive-quality ramp and cost 5 above it when tetrahedron normal estimation executes the four extra distance probes.
- Batch jobs accumulate this true cost before the atomic counter write, and `ShouldTripThroughputWarning(previousTotal, total)` detects threshold crossings even when the counter jumps past 800001.

- The Unity Terrain seam bridge no longer owns native seam allocations.
- `WorldGenerativeGeologyTerrainSeamApplier` requests `VaultBufferHandle<TerrainSeamTelemetryEntry>` from `GlobalDataVault` with domain-local `BufferID 0x530421`, length 300, owner `SystemID.TerrainSeams`.
- Hybrid scratch uses vault buffers `0x530422` native plans, `0x530423` patch heights, `0x530424` blend mask, and `0x530425` normals.
- Terrain baseline heights use per-terrain `VaultBufferHandle<float>` at `0x531000 + (terrain instance id & 0x000FFFFF)`.
- Record, dump, baseline, and scratch paths resolve vault aliases; dispose does not unregister/free private native arrays.

## Hybrid Seam Writeback

- `WorldGenerativeGeologyTerrainSeamApplier` reads `HomeostasisBrain.GlobalQualityWeight` and no longer branches on `GlobalRegistry.ScalabilityTier` or `ScalabilityTierProfileByte` for seam quality.

- `HybridSdfHeightmapProjectionJob` has required Burst flags, `[NoAlias]` buffers, and quality lanes for Unity-regenerated source assemblies.

- Hybrid seam projection is terrain-local.
- Applier subtracts terrain absolute AUP from plan/contact/voxel AUP in double space.
- Local deltas cast to float only after subtraction.
- Job distance/raymarch math no longer compares absolute 100km runtime floats.

- Fallback patch deformation, voxel snap, trench deformation, and plan/trench rect selection follow the same local-AUP rule.

- Raymarch steps resolve from 1 to 16 through a polynomial curve.
- Below 0.3 quality: expensive deformation raymarch collapses.
- Above 0.3: raymarch returns smoothly.
- Above 0.7: mask-detail job fades in slope boost.

- Stale generated-csproj fallback:
  - Retained field: old byte quality field.
  - Failed purge reason: generated Core compile lane still resolves stale terrain job metadata.
  - Missing generated metadata: `GlobalQualityWeight`, `GlobalQualityWeightValid`.
  - Temporary route: cold reflection injects continuous quality into newer source jobs.
  - Exit condition: Unity-regenerated assembly or contracts-level facade.
  - Status: explicit integration debt; no clean compile-wall pass claimed.

- `VoxelChunkModifiedEvent.Frame` and `TerrainSeamTelemetryEntry.Frame` now use a local monotonic seam frame counter instead of `Time.frameCount`.

- `TerrainSeamTelemetryEntry` is a natural sequential 64-byte row with explicit `Reserved4` tail padding; no manual `Pack` is used.

- The seam black-box ring, hybrid scratch buffers, and baseline height cache are vault-owned rather than private `NativeArray` allocations.

## Editor Facade

`HECTON-8/World/Math-Terrain Probe` raymarches the math field from the Scene camera, draws hit and normal, and exposes `Force Quality Weight` for Low/Middle/High/Ultra inspection.

Editor hot-reload target `biome_atlas_overrides.csv` is absent in the current checkout. Route remains pending until that file or a replacement artifact exists.

## Verification

- Forbidden-pattern grep scope: `GlobalWorldSampler.cs`, `World/HybridTerrainSeamJobs.cs`.
- Local grep text reported no forbidden Physics, terrain, random, file IO, packing, low-precision Burst, frame-time, or property hot-path patterns.
- Missing proof tuple: command, timestamp, environment, output.
- Rerun before using as current proof.

- Seam quality grep:
  - `WorldGenerativeGeologyTerrainSeamApplier.cs` no longer contains tier resolver methods for seam quality.
  - Removed tokens: `GlobalRegistry.ScalabilityTier`, `ScalabilityTierProfileByte`.
  - Remaining `ForceMathLodLow` is a documented legacy ABI enum bit in `GlobalWorldSamplerConfigFlags`.

- Direct-field reflection purge re-probe failed with CS1061 from stale terrain job metadata lacking `GlobalQualityWeight` / `GlobalQualityWeightValid`.
- The direct-field chunk was reverted under fail-fast rules.

- `dotnet build Hecton8.Core.csproj --no-restore /clp:ErrorsOnly`: latest post-revert local run failed outside SHINOBU_41.
- Blocking files: `HomeostasisBrain.ScalabilityDictator.cs`, `SaveBinaryPayloadCodec.cs`, Visor features, `ShinobuFloraFaunaSymbiosisSolver.cs`.
- Errors: unassigned `sanitizedWeight`, missing `IndustrialLoreBitMask`, missing `HectonDrsRenderFeatureGate`, invalid `math.reversebytes`.
- No post-revert compiler error references SHINOBU_41 terrain files.

- `Assembly-CSharp.csproj`: attempted, timed out after 129.7s; no pass claimed.

Status: STATIC SOURCE ORIENTATION / DATAVAULT SCRATCH+BLACKBOX SOURCE NOTES / REFLECTION ABI BLOCKED BY STALE TERRAIN ASSEMBLY / CORE BUILD BLOCKED OUTSIDE SHINOBU / UNITY RUNTIME PENDING

`Assets/_Project/Scripts/World/GlobalWorldSampler.cs` is the terrain truth path for SHINOBU_41. It fuses MapMagic-style quantized height samples and voxel SDF samples through polynomial smooth-min without Unity Physics.

`Assets/_Project/Scripts/World/HybridTerrainSeamJobs.cs` and `Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs` are the cold Unity Terrain seam writeback path. They now consume `GlobalQualityWeight` for hybrid MapMagic/SDF patch deformation instead of selecting by hardware tier.
