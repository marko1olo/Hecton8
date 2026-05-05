# TERRAIN AND BIOME REALITY MAP

Status: PENDING VERIFICATION
Date: 2026-05-04
Scope: MapMagic terrain bridge, 108 biome matrix, scatter influence grid, biome transition runtime hooks.

## 2026-05-05 Volumetric Biome Continuation

Status: PENDING VERIFICATION

This section supersedes older verification text below for the current volumetric biome pass.

### Implemented

- `WorldProceduralFieldSampler` now treats biome influence as a 3D sample, not only a 2D MapMagic read. The sampled Y/depth selects volumetric roles before packing the influence cell.
- `BiomeInfluenceCell` remains a packed uint lane:
  `primary | (secondary << 8) | (blend << 16) | (flags << 24)`.
- Depth override preserves the previous 2D MapMagic biome as transition secondary data when a volumetric biome wins.
- `HectonBiomeMatrixProfile` exposes `GravityMultiplier` through authoring fields `gravityMultiplier` and `buoyancyMultiplier`.
- `PhysicsApplySystem` applies the active biome gravity multiplier only to upward `ForceMode.Acceleration` buoyancy packets marked with `BiomeBuoyancy`.
- `MapMagicBridge.TryGetTerrainSplatColorAUP` samples terrain alphamaps and terrain-layer color approximations at an AUP.
- `VoxelSeamDirector.BuildCaveEntrance` stores terrain splat color and blend weight in `CaveEntrance`.
- `HectonVoxelEngine.VoxelColorJob` blends cave-mouth voxel vertex color toward sampled terrain splat color near entrance radius.
- `BiomeMatrixDirector` now has 15 m transition hysteresis before publishing biome/fog/audio changes.
- `WorldProceduralScatterDirector` projects abyssal-silt plant/kelp/coral candidates to the `Y=-200m` false ceiling when the sampled influence flags/family match.
- `ResourceDistributionDirector.TrySpawnDeepMantleGeodeAtAup` provides the volcanic-hadal forced geode spawn endpoint used by the geology bridge.
- `Fabricator` rejects biome-locked recipes unless the host base module is anchored in the requested matrix ID or biome family.
- `VolumetricBiomeSmokeTester` and `WorldVolumetricBiomeClassificationJobs` provide a Burst-backed headless smoke path for depth-band biome override.

### Cave-Mouth Color Blending Surgery Log

```csharp
if (absoluteTerrainContactPosition.sqrMagnitude > 0.0001f &&
    MapMagicBridge.ActiveRuntimeInstance != null &&
    MapMagicBridge.ActiveRuntimeInstance.TryGetTerrainSplatColorAUP(
        absoluteTerrainContactPosition,
        out Color sampledColor,
        out float sampledBlend))
{
    entrance.terrainSplatColor = new float4(
        sampledColor.r,
        sampledColor.g,
        sampledColor.b,
        sampledColor.a);
    entrance.terrainSplatBlend = math.saturate(sampledBlend);
}
```

```csharp
float blend = math.saturate(math.max(entrance.terrainSplatBlend, entrance.terrainSplatColor.w));
float localWeight = (1f - math.smoothstep(radius * 0.35f, radius * 1.85f, distance)) * blend;
colorPayload.xyz = math.lerp(colorPayload.xyz, terrainSplatColor.xyz, localWeight);
colorPayload.w = math.max(colorPayload.w, localWeight);
```

### Verification Evidence

- Direct Unity Bee C# compile passed for `Hecton8.Core`:
  `dotnet exec ... csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.rsp`.
- Direct Unity Bee C# compile passed for `Hecton8.Editor`:
  `dotnet exec ... csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Editor.rsp`.
- Unity batchmode compile reached `*** Tundra build success` in `CodexArtifacts/unity-volumetric-biome-smoke-2026-05-05.log`.
- `VolumetricBiomeSmokeTester.RunBatchmode` was added, but repeated external batchmode automation took the Unity project lock before a stable executeMethod run could be completed.
- MCP console proof is unavailable in this pass: MCP HTTP transport on `127.0.0.1:8088` returned connection failure/no Unity session.

### Current Blockers

- Batchmode Unity is being repeatedly occupied by other automation smoke processes. This blocks a clean MCP/executeMethod console proof for this specific smoke tester.
- Licensing log lines contain transient Unity access-token errors, but licensing later resolves entitlement and compile continues.
- No production status beyond `PENDING VERIFICATION` is claimed for this pass.

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

## 2026-05-05 OMEGA-AUTONOMY HARDENING PASS

Status: `PENDING VERIFICATION`. `OMEGA VERIFIED` is not claimed because `AGENTS.md` requires user-provided logs for final fix confirmation; local Unity smoke evidence is recorded below.

### Mandates Re-Checked

- `AGENTS.md`: no fake verification; status remains `PENDING VERIFICATION` without user-provided production/runtime confirmation.
- `OPT_Native_Memory_Collections_JobSystem_Protocol`: persistent NativeCollections require single owner and Sentinel tracking.
- `OPT_Zero_GC_Policy_AllocFree_Mandate`: no new hot-path string allocation, LINQ, coroutine, or managed container allocation in the touched biome sampling path.
- `MATH_Coordinate_Precision_AUP_FloatingOrigin`: no transform-space authority added; sampling remains fed by existing AUP/runtime position bridge.
- `ARCH_Global_Registry_ServiceLocator_DI_Init`: removed the stale bootstrap fallback to the deleted `SaveManager.ActiveRuntimeInstance` API; active save authority is `GlobalRegistry.SaveRuntime`.

### Surgery Log

- `WorldProceduralFieldSampler`: registered persistent `_burstZoneData`, `_burstBiomeMatrixData`, `_burstBiomeMatrixIdToDataIndex`, `_burstBiomeFamilyData`, `_burstCaveEntranceHints`, and `_noiseLookupTable` with `NativeMemorySentinel`; unregisters now happen before disposal.
- `WorldProceduralFieldSampler`: added `GlobalTelemetryBus.PublishPerformanceWarning` when biome influence GPU grid capacity grows past the MX350 soft cell budget of 4096.
- `ResourceDistributionDirector`: registered pressure metamorphism and ghost proxy snap persistent NativeArrays with `NativeMemorySentinel`; immediate and deferred disposal paths both unregister first.
- `VolumetricBiomeSmokeTester`: upgraded the cold smoke from 3 samples to a 256-sample Burst stress chain and registered all TempJob smoke arrays with `NativeMemorySentinel`.
- `WorldVolumetricBiomeClassificationJobs`: added `VolumetricBiomeStressAuditJob`, a Burst `IJobParallelFor` that validates primary biome ID, required flag masks, and packed `uint` layout.
- `GameBootstrapper`: removed a compile-breaking reference to `SaveManager.ActiveRuntimeInstance`, which no longer exists after the registry migration.
- `AutomationOmegaSmokeTester`: added `using Hecton8.World;` so its cold smoke can resolve `DispatcherJobSwap`.

### Burst Packing Logic

The stress audit validates the same packed ABI used by the scatter GPU grid:

```csharp
uint expectedPack = (uint)(
    cell.PrimaryBiomeId |
    (cell.SecondaryBiomeId << 8) |
    (cell.Blend255 << 16) |
    (cell.Flags << 24));
```

### Verification

- Direct Core compile: `dotnet exec ... csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.rsp` returned exit code 0 after the final sequential rerun.
- Direct Editor compile: `dotnet exec ... csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Editor.rsp` returned exit code 0 after the final sequential rerun.
- `git diff --check` on the biome/resource touched files returned no whitespace errors; only Git CRLF normalization warnings were emitted.
- `rg` barrier sweep found no raw `.Complete()`, `.Run()`, or `JobHandle.CompleteAll` in the scoped biome/resource files. Remaining synchronization points are `DispatcherJobSwap.TryComplete` windows, including one cold forced smoke-test sync outside runtime `Tick`/`Update`.
- Unity batchmode attempts for `Hecton8.World.VolumetricBiomeSmokeTester.RunBatchmode` initially wrote:
  - `CodexArtifacts/omega-volumetric-biome-smoke-2026-05-05-run2.log`
  - `CodexArtifacts/omega-volumetric-biome-smoke-2026-05-05-run3.log`
- Both Unity batchmode attempts exited with code 1 before script execution; logs stop immediately after project path selection and contain no `VolumetricBiomeSmokeTester` PASS/FAIL line.
- External Unity batch processes repeatedly occupied the project during this window:
  - `Hecton8.EditorTools.DocumentationAuthoritySmokeTester.RunMenuItem`
  - `Hecton8.Editor.HydraulicErosionSmokeTester.RunMenu` (`final`, `final2`, `final3`)
- After the external Unity processes exited, `CodexArtifacts/omega-volumetric-biome-smoke-2026-05-05-run4.log` reached `VolumetricBiomeSmokeTester.RunBatchmode` and emitted:
  - `[VolumetricBiomeSmokeTester] PASS shallow=11 twilight=12 hadal=13 flags=16 stressSamples=256 stressFailures=0 packedChecksum=2952397042`
- The same Unity log also contains non-domain service/runtime noise that prevents a "0 console errors" claim:
  - Licensing handshake/access-token errors at lines 71, 74, and 88, followed by successful license resolution.
  - UnityConnect CDN timeout / Curl callback abort at lines 424 and 426.
  - Unity test protocol memory-leak marker and temp allocator warning at lines 456 and 457.

### Residual Risk

- Runtime GC proof is absent. Code review only: no new hot-path managed allocation was introduced in the modified biome sampling path.
- Unity domain smoke proof exists for `VolumetricBiomeSmokeTester`; whole-console proof is not clean because the batch log contains Unity service errors and a temp allocator leak marker outside the smoke assertion line.

## 2026-05-05 OMEGA-AUTONOMY HARDENING PASS 2

Status: `PENDING VERIFICATION`. `OMEGA VERIFIED` is not claimed because `AGENTS.md` requires user-provided logs before a verified status, and pass-2 Unity smoke attempts did not reach a fresh PASS/FAIL assertion after the new code changes.

### Mandates Re-Checked

- `OPT_Native_Memory_Collections_JobSystem_Protocol`: scoped TempJob and Persistent NativeArrays must be Sentinel-owned.
- `OPT_Zero_GC_Policy_AllocFree_Mandate`: no hot-path `.ToString()`, string interpolation, or `string.Format` introduced in scoped `Tick`/`Update` surfaces.
- `MATH_Coordinate_Precision_AUP_FloatingOrigin`: no transform-space authority added; registry migration only changes lookup ownership.
- `ARCH_Global_Registry_ServiceLocator_DI_Init`: procedural field sampler and resource distribution active ownership moved to `GlobalRegistry`; scatter active lookup now resolves through `GlobalRegistry.WorldGen`.

### Surgery Log

- `GlobalRegistry`: added concrete slots for `ProceduralFieldSampler` and `ResourceDistribution`, with register/unregister APIs and service-slot enum entries.
- `WorldProceduralScatterDirector`: removed private static active-state ownership; `ActiveRuntimeInstance` now resolves from `GlobalRegistry.ProceduralScatter`.
- `WorldProceduralFieldSampler`: removed private static active-state ownership; lifecycle now registers/unregisters through `GlobalRegistry`; TempJob prewarm arrays now register with `NativeMemorySentinel` using `TempJob` lifetime before disposal.
- `ResourceDistributionDirector`: removed private static active-state ownership; runtime lifecycle now registers/unregisters through `GlobalRegistry`.
- `VolumetricBiomeSmokeTester`: moved the 256-sample managed audit reduction into Burst jobs: `VolumetricBiomeStressBlockReduceJob` (`IJobParallelFor`) plus `VolumetricBiomeStressFinalReduceJob`.

### Verification

- Direct Core compile: `dotnet exec ... csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.rsp` returned exit code 0.
- Direct Editor compile: `dotnet exec ... csc.dll @Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Editor.rsp` returned exit code 0.
- `git diff --check` on scoped pass-2 files returned no whitespace errors; only Git CRLF normalization warnings were emitted.
- Static sweeps on scoped pass-2 files:
  - Raw `.Complete()`, `.Run()`, `JobHandle.CompleteAll`: 0 matches.
  - Hot-path string purge regex for `.ToString()`, `string.Format`, `$"` in scoped `Tick`/`Update` surfaces: 0 matches.
  - Static active assignment / private `_instance` / `DontDestroyOnLoad` residue in the three migrated world-gen classes: 0 owner assignments remaining.
  - NativeArray allocation sites scanned: 10; untracked allocation sites after patch: 0 by code review against Sentinel registration calls.
- Pass-2 Unity smoke attempts for `Hecton8.World.VolumetricBiomeSmokeTester.RunBatchmode`:
  - `CodexArtifacts/omega-volumetric-biome-smoke-2026-05-05-run5.log`: reached Unity script compilation, no smoke assertion.
  - `CodexArtifacts/omega-volumetric-biome-smoke-2026-05-05-run6.log`: exited with return code 1 before `executeMethod`.
  - `CodexArtifacts/omega-volumetric-biome-smoke-2026-05-05-run7.log`: no smoke assertion before external Unity batch contention resumed.
- Evidence JSON: `CodexArtifacts/omega-biome-autonomy-pass2-verification-2026-05-05.json`.

### Residual Risk

- Pass-2 smoke execution is blocked by repeated external Unity batch processes occupying the project. Compile proof is clean; fresh post-change Unity smoke proof is absent.
- Whole-console clean status is not claimed.
- The larger worktree contains many unrelated modified and untracked files. They were not reverted or normalized.
