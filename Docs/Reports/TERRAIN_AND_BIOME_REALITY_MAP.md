# TERRAIN AND BIOME REALITY MAP

Status: PENDING VERIFICATION
Date: 2026-05-04
Scope: MapMagic terrain bridge, 108 biome matrix, scatter influence grid, biome transition runtime hooks.

## Mandates Followed

- AGENTS.md: zero fake verification; Unity/MCP proof must be reported separately from code/build proof.
- OPT_Native_Memory_Collections_JobSystem_Protocol: one NativeCollection owner; Burst jobs carry unmanaged fields only.
- GPU_Compute_Kernels_Kernels_Optimization_MX350: C# GPU resources use GraphicsBuffer; packed biome data is a uint stream.
- REND_Instanced_Flora_Physics: flora stream cap is hard-bounded at 4096 instances per stream cell per biome.
- CORE_Weather_Abyssal_FlowField_Currents: biome current overrides are transition inputs; weather/flow runtime verification remains required.

## Data Reality

- The requested `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md` did not exist before this pass.
- `Docs/Legacy_World_Reference/TERRAIN_108_BIOMES_VISION.md` is the legacy narrative source for the 108 matrix.
- `Assets/_Project/Scripts/HectonBiomeMatrixCatalog.cs` declares `HectonBiomeMatrixProfile[108]` and validates IDs 1..108.
- `Assets/_Project/Data/Biomes/MatrixProfiles/Biome_###_*.asset` contains matrix profile assets with stable `matrixIndex` fields.
- Reserve placeholders exist in the data naming pattern, for example `Tier##_North/South/East/West_Reserve`.

## Runtime Integration Map

- `MapMagicBridge` is the terrain authority. It exposes cached tile lookup and matrix biome readout through `TryGetMatrixBiomeId`.
- `WorldProceduralFieldSampler` now publishes `BiomeInfluenceCell` with byte lanes:
  `primary | (secondary << 8) | (blend << 16) | (flags << 24)`.
- `WorldProceduralScatterDirectorSamplingPipeline` packs influence cells with a Burst `IJobParallelFor`, uploads the packed grid, and uses `Blend255` to mix primary/secondary scatter context.
- `WorldProceduralScatterDirector` rejects flora overflow above 4096 instances per stream cell per biome.
- `SoundscapeSystem` and `HectonMusicDirector` are wired to biome matrix changes for profile-driven audio transitions.
- `AmbientWaterMotionManager` interpolates biome current overrides over five seconds on matrix biome changes.
- `BiomeMatrixDirector` triggers seismic dust on IDs 7, 9, 11 or profile opt-in via `AbyssalFluidDecalManager.RegisterSeismicDust`.
- `HectonUnderwaterVisuals` now owns the runtime biome fog transition buffers, schedules `BiomeTransitionFogBlendJob` from slow tick, and completes the result only through `ILateFrameTickable` / `DispatcherJobSwap`.

## Terrain/Voxel Seam

- MapMagic height and normals are sampled only through `MapMagicBridge`.
- `WorldGenerativeGeologyVoxelBridgeDirector.BuildEntrances` resolves a terrain normal and passes it into `VoxelSeamDirector.BuildCaveEntrance`.
- `VoxelSeamDirector.BuildCaveEntrance` stores normal-derived perturbation into cave entrance SDF fields so the cave mouth can blend into the terrain surface instead of reading as a raw boolean cut.

## Surgery Log

Burst biome influence pack job:

```csharp
[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
private struct BiomeInfluencePackJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<WorldProceduralFieldSampler.BiomeInfluenceCell> Source;
    [WriteOnly] public NativeArray<uint> Destination;
    public int CellCount;

    public void Execute(int index)
    {
        if ((uint)index >= (uint)CellCount)
            return;

        Destination[index] = Source[index].Packed;
    }
}
```

Burst biome fog blend job:

```csharp
public void Execute(int index)
{
    BiomeTransitionSample sample = Samples[index];
    BiomeTransitionFogSource from = ResolveSource(sample.FromBiomeId);
    BiomeTransitionFogSource to = ResolveSource(sample.ToBiomeId);
    float blend = ResolveAupBlend(index, sample.Blend255 * (1f / 255f));
    float smoothBlend = blend * blend * (3f - 2f * blend);

    Results[index] = new BiomeTransitionFogResult
    {
        Sample = new BiomeTransitionSample
        {
            FromBiomeId = sample.FromBiomeId,
            ToBiomeId = sample.ToBiomeId,
            Blend255 = (byte)math.round(math.saturate(smoothBlend) * 255f),
            Flags = sample.Flags
        },
        FogColor = math.lerp(from.FogColor, to.FogColor, smoothBlend),
        Density = math.lerp(from.Density, to.Density, smoothBlend),
        Turbidity = math.lerp(from.Turbidity, to.Turbidity, smoothBlend),
        Absorption = math.lerp(from.Absorption, to.Absorption, smoothBlend)
    };
}
```

Smoke tester:

- `Assets/_Project/Scripts/World/BiomeTransitionSmokeTester.cs`
- `Assets/_Project/Scripts/Editor/BiomeTransitionSmokeTesterMenu.cs`
- Batch/editor method: `Hecton8.Editor.BiomeTransitionSmokeTesterMenu.RunBiomeTransitionSmokeTest`

## Verification

- `dotnet build Hecton8.Core.csproj /m:1`: 0 errors, 0 warnings.
- `dotnet build Hecton8.Editor.csproj /m:1`: 0 errors, 0 warnings.
- MCP `execute_code` smoke: `BiomeTransitionSmokeTester.RunHeadlessSmokeTest` returned `passed=True`, `density=0.0400`, `absorption=0.5000`, `packed=0x05802B2A`.
- MCP `read_console` after smoke execution: 0 error entries and 0 warning entries.
- MCP editor state after refresh: `ready_for_tools=true`, `is_compiling=false`, `is_domain_reload_pending=false`.
- `git diff --check` on touched biome fog files: clean except CRLF normalization warnings.
- `rg` sweep for `Terrain.activeTerrain`, `activeTerrains`, and terrain scene search patterns in `Assets/_Project/Scripts`: no matches.

## Regression Model

- CPU: extra scatter work is one Burst pack pass over resolved cells; no managed iteration added to per-instance hot path.
- GC: measured proof absent. Code review shows no LINQ, coroutine, scene search, or managed allocation in the new sampling hot path.
- Memory: one owner for the biome influence GPU buffer is `WorldProceduralFieldSampler`; duplicate scatter working-memory ownership was removed.
- GPU: upload is a packed `uint` GraphicsBuffer stream; MX350 risk is bounded by single uint per scatter cell plus existing flora 4096 cap.
- Correctness risk: full playmode traversal through an authored biome border is still required before any production verification claim.
