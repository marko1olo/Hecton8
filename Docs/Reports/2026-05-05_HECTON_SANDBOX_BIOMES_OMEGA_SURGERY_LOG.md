# HECTON-8 Sandbox Biomes Macro Shelf Surgery Log

Date: 2026-05-07
Domain: `03_HECTON_SANDBOX_BIOMES`
Status: PENDING VERIFICATION

## Mandates Loaded

- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `STRM_World_Streaming_Residency_Chunk_Management.txt`
- `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## Scope

- Scene target: `03_HECTON_SANDBOX_BIOMES`.
- System target: test sandbox procedural terrain, `HectonSandboxAbyssalShelf*`.
- Macro-only pass: no rivers, sand, ecology, or small sediment detailing claimed here.

## Macro Shelf Implementation

The shelf profile now drops from `HighWorldY = 2000m` to `LowWorldY = -5000m` over `DescentRadiusMeters = 17500m` with an exponential curve:

```csharp
t = saturate(radius / descentRadiusMeters);
macro01 = (1 - exp(-falloff * t * t)) / max(0.000001, 1 - exp(-falloff));
baseY = lerp(HighWorldY, LowWorldY, macro01);
```

Runtime parameter:

- `MacroExponentialFalloff = 3.1f`

This replaces the previous direct radial falloff behavior with a continental shelf descent across the required 15-20 km macro radius.

## Burst Y-Branch Domain Warp

The ridge lookup is no longer a clean polygonal Voronoi lookup. The input AUP XZ coordinate is domain-warped before plate lookup:

```csharp
sample = (float2)aupXZ * DomainWarpFrequency;
low = float2(
    FractalPerlinNoise(sample, seedA) * 2 - 1,
    FractalPerlinNoise(sample + offsetA, seedB) * 2 - 1);
high = float2(
    FractalPerlinNoise(sample * 2.37 + offsetB, seedC) * 2 - 1,
    FractalPerlinNoise(sample * 2.11 + offsetC, seedD) * 2 - 1);
twist = FractalPerlinNoise(sample * 0.73 + offsetD, seedE) * 2 - 1;
angle = twist * 1.0471976f;
warp = rotate(low * 0.72 + high * 0.28, angle) * DomainWarpMeters;
platePosition = (aupXZ + warp) / PlateCellSizeMeters;
```

Ridge mask uses both Voronoi edge and junction deltas:

```csharp
edgeMask = 1 - smoothstep(RidgeWidthMeters * 0.18, RidgeWidthMeters, F2 - F1);
junctionMask = 1 - smoothstep(JunctionWidthMeters * 0.22, JunctionWidthMeters, F3 - F2);
ridgeMask = saturate(edgeMask * 0.82 + junctionMask * 0.72 + forkNoise * 0.10);
```

This produces warped edge ridges plus junction emphasis for Y-shaped branching. Ridge lift is attenuated by macro depth so the far abyss remains at `-5000m` instead of being artificially lifted by ridge energy.

## Slope Quantization

`HectonSandboxSlopeQuantizationJob` now targets a physical gradient instead of generating stair/terrace bands:

```csharp
targetGradient = tan(radians(PlateauTargetAngleDegrees));
targetFactor = targetGradient / gradient;
factor = lerp(1, targetFactor, adjustMask * Strength);
resolved = average + delta * factor;
```

Runtime defaults:

- Flat dead zone: `4 degrees`
- Target slope: `30 degrees`
- Steep source: `40 degrees`
- Steep full clamp band: `58 degrees`
- Strength: `1`

Smoke validation measures active sloped samples separately from flats. Current active average is `30.718 degrees`.

## AUP Continuity

The sandbox shelf base job no longer takes a raw `WorldOriginXZ`. It takes `AbsoluteUniversePosition WorldOriginAup`, and every sample resolves from AUP:

- `HectonSandboxAbyssalShelfMath.BuildAupXZ`
- `HectonSandboxAbyssalShelfMath.ResolveSampleAupXZ`
- `HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(in AbsoluteUniversePosition, ...)`

Standalone smoke proves both chunk classes:

- Origin chunk `(0,0)`: `originChunkInvalid = 0`
- Far chunk `(50000,50000)`: `farChunkInvalid = 0`
- Direct high-coordinate vs AUP high-coordinate delta: `highChunkAupDeltaMeters = 0`
- AUP boundary probe delta: `aupBoundaryDeltaMeters = 0.31`

## Harness Visualization

`ErosionTestHarness.cs` now exports normal maps in addition to grayscale height and mask outputs:

- `CodexArtifacts/ErosionTestHarness_After_Normal.png`
- `CodexArtifacts/ErosionTestHarness_MacroShelf.png`
- `CodexArtifacts/ErosionTestHarness_MacroShelf_Normal.png`

The macro preview path schedules the shelf base job and slope quantization job with AUP origin `(-16000m,-16000m)`, `64m` sample spacing, and the same macro shelf parameters as the sandbox MapMagic node.

Current editor compile errors in unrelated project files block an honest claim that these PNG artifacts were written in this session. The code path validates with Unity MCP.

## Verification Evidence

Standalone release smoke command:

```powershell
dotnet run --project Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke/HectonSandboxAbyssalShelfStandaloneSmoke.csproj --configuration Release -- --output Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke.json
```

Result:

```json
{"status":"MACRO SHELF VERIFIED","tester":"HectonSandboxAbyssalShelfStandaloneSmoke","samples":12,"invalid":0,"cliffSamples":0,"plateauSamples":8,"minHeightMeters":-5000,"maxHeightMeters":2000,"maxSlopeDegrees":39.043,"averageSlopeDegrees":12.141,"averageActiveSlopeDegrees":30.718,"slope30Samples":2,"aupDeltaMeters":0,"aupBoundaryDeltaMeters":0.31,"originChunkInvalid":0,"farChunkInvalid":0,"highChunkAupDeltaMeters":0,"elapsedMs":23.454}
```

## Omega V2 Crucible Audit

Violation found and excised:

- `HectonSandboxAbyssalShelfSmokeTester.RunSmokeTest` marked `sampleHandleScheduled = true` only after scheduling the final summary job.
- Failure mode: if reduction or summary scheduling threw after the first sample job was scheduled, `finally` could unregister and dispose `positions`, `samples`, `reductions`, or `summary` while a live job still referenced one or more arrays.
- Fix: `sampleHandleScheduled = true` now executes immediately after `sampleJob.Schedule(...)`, before dependent jobs are scheduled.

Native memory interrogation:

- `HectonSandboxAbyssalShelfMapMagicNode`: allocates `rawHeights` and `quantizedHeights`; both registered via `RegisterTempJobArray`; `generationHandleScheduled` is set immediately after the base job schedule; `finally` drains the handle with `DispatcherJobSwap.TryComplete` before unregister/dispose.
- `HectonSandboxAbyssalShelfSmokeTester`: allocates `positions`, `samples`, `reductions`, and `summary`; all registered through `NativeMemorySentinel`; `finally` drains `sampleHandle` before unregister/dispose. This is the path fixed by the Crucible pass.
- `ErosionTestHarness.Run`: allocates `before`, `heightA`, `heightB`, `sediment`, `wear`, and `metrics`; all registered in `RegisterTempJobBuffers`; `handleScheduled` is set immediately after the first scheduled job; `finally` drains before `DisposeTracked`.
- `ErosionTestHarness.WriteMacroShelfPreviewArtifacts`: allocates `raw` and `quantized`; both registered; `handleScheduled` is set immediately after the base shelf job schedule; `finally` drains before `DisposeTracked`.
- `HectonSandboxAbyssalShelfJobs`: uses `NativeArray` fields inside Burst jobs only; ownership remains with caller allocation sites above.
- `Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke/Program.cs`: no Unity `NativeArray`, `NativeList`, or `NativeQueue` ownership. Managed arrays are standalone console harness memory, not Unity runtime native memory.

GC purge scan on touched code files:

- `foreach`: 0 matches.
- LINQ calls `.Select`, `.Where`, `.Any`, `.First`, `.FirstOrDefault`, `.ToList`, `.ToArray`: 0 matches.
- `string.Format`: 0 matches.
- string interpolation `$"`: 0 matches.
- `Update`, `Tick`, `SlowTick`: 0 matches in touched code files.

AUP compliance scan on touched code files:

- `transform.position`: 0 matches.
- `Vector3.Distance`: 0 matches.
- `math.distance`: 0 matches.
- generic `Distance(` call token: 0 matches.

Dependency-cycle scan on touched code files:

- `GlobalRegistry.Get`: 0 matches.
- `GlobalRegistry.` access: 0 matches.
- `Awake`: 0 matches.
- `OnEnable`: 0 matches.

Post-fix standalone verification:

- `dotnet build Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke/HectonSandboxAbyssalShelfStandaloneSmoke.csproj -v:minimal`: exit `0`, `0 Warning(s)`, `0 Error(s)`.
- `dotnet run --project Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke/HectonSandboxAbyssalShelfStandaloneSmoke.csproj --configuration Release -- --output Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke.json`: exit `0`, status `MACRO SHELF VERIFIED`.

Unity MCP status:

- Fresh Unity MCP validation after the Crucible patch failed because the Unity session was unavailable: `no_unity_session`.
- Prior Unity MCP validation before this one-line smoke tester fence patch reported 0 errors and 0 warnings on the same four Unity script files.

## Omega V2 Crucible Audit 2

Second-pass result:

- No new leak or hot-path violation found in touched code files.
- Forced-action optimization executed: editor normal-map pixel generation moved from managed nested loops into Burst `ErosionNormalMapBakeJob : IJobParallelFor`.

Garbage removed:

- `ErosionTestHarness.WriteNormalPng` previously allocated a managed `Color32[]` and filled every normal-map pixel on the main thread.
- Replacement: `NativeArray<Color32> pixels` plus Burst job writes normal pixels in parallel, then `Texture2D.SetPixelData(NativeArray<Color32>, 0)` uploads the finished buffer.

New native memory path:

- Allocation: `NativeArray<Color32> pixels` in `WriteNormalPng`.
- Registration: `NativeMemorySentinel.RegisterNativeArray(pixels, NativeMemoryOwner, NormalPixelsLabel, NativeAllocationLifetime.TempJob)`.
- Fence: `handleScheduled = true` immediately after `ErosionNormalMapBakeJob.Schedule(...)`.
- Completion: `DispatcherJobSwap.TryComplete(ref handle, forceComplete: true)` before PNG write and in `finally` if still scheduled.
- Disposal: `DisposeTracked(ref pixels)` in `finally`, which unregisters from `NativeMemorySentinel`, disposes, and resets to `default`.

Second-pass scans on touched code files:

- `foreach`, LINQ calls, `string.Format`, string interpolation, `Update`, `Tick`, `SlowTick`: 0 matches.
- `transform.position`, `Vector3.Distance`, `math.distance`, `Distance(`: 0 matches.
- `GlobalRegistry.Get`, `GlobalRegistry.`, `Awake`, `OnEnable`: 0 matches.

Second-pass verification:

- `dotnet build Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke/HectonSandboxAbyssalShelfStandaloneSmoke.csproj -v:minimal`: exit `0`, `0 Warning(s)`, `0 Error(s)`.
- `dotnet run --project Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke/HectonSandboxAbyssalShelfStandaloneSmoke.csproj --configuration Release -- --output Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke.json`: exit `0`, status `MACRO SHELF VERIFIED`.
- `git diff --check` on `ErosionTestHarness.cs`, this surgery log, and the standalone JSON: exit `0`.

Second-pass blockers:

- Unity MCP validation still unavailable: `no_unity_session`.
- `dotnet build Hecton8.Editor.csproj` with isolated intermediates fails before script compilation on generated-project circular `ResolveProjectReferences` errors in unrelated generated project references. No `ErosionTestHarness.cs` compiler diagnostic was produced by that build.

Unity MCP script validation:

- `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfJobs.cs`: 0 errors, 0 warnings.
- `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfSmokeTester.cs`: 0 errors, 0 warnings.
- `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfMapMagicNode.cs`: 0 errors, 0 warnings.
- `Assets/_Project/Scripts/Editor/ErosionTestHarness.cs`: 0 errors, 0 warnings.

Target static scan:

- `HectonSandboxAbyssalShelfBaseJob` call sites now use `WorldOriginAup`.
- Remaining `WorldOriginXZ` matches are separate terrain systems, not this sandbox shelf base job.
- Hot shelf Burst jobs contain no managed allocation path.

## Compile State

`dotnet restore Hecton8.Core.csproj` with isolated intermediates returned exit `0`.

`dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false` with isolated intermediates returned exit `1` because the current workspace has unrelated compile errors:

- `Assets/_Project/Scripts/CraftingSystem.cs`
- `Assets/_Project/Scripts/Fabricator.cs`
- `Assets/_Project/Scripts/SaveManager.cs`
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs`

No errors in the build output point at the sandbox shelf files.

Build log artifact: `CodexArtifacts/macro-shelf-h8core-build.log`.

## Diff Scope

- `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfJobs.cs`
- `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfSmokeTester.cs`
- `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfMapMagicNode.cs`
- `Assets/_Project/Scripts/Editor/ErosionTestHarness.cs`
- `Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke/Program.cs`
- `Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke.json`
- `CodexArtifacts/macro-shelf-h8core-build.log`
- `Docs/Reports/2026-05-05_HECTON_SANDBOX_BIOMES_OMEGA_SURGERY_LOG.md`

## Omega V2 Crucible Audit 3

Third-pass result:

- No new runtime leak, GC hot-path, AUP, or dependency-cycle violation was found in the touched sandbox shelf files.
- Forced-action optimization executed because the audit was clean: remaining editor PNG bake loops in `ErosionTestHarness` were moved to Burst jobs.

Garbage removed:

- `ErosionTestHarness.WriteHeightPng` no longer allocates a managed `Color32[]` or fills grayscale pixels on the main thread.
- `ErosionTestHarness.WriteMaskPng` no longer scans mask max or fills mask pixels on the main thread.
- `WritePng(Color32[] pixels, ...)` was removed. PNG upload path now uses `Texture2D.SetPixelData(NativeArray<Color32>, 0)`.

New Burst jobs:

- `ErosionGrayscalePngBakeJob : IJobParallelFor` writes height grayscale pixels.
- `ErosionMaskMaxJob : IJob` computes mask max in Burst.
- `ErosionMaskPngBakeJob : IJobParallelFor` writes normalized mask pixels after the max dependency.

New native memory paths:

- `WriteHeightPng`: allocates `NativeArray<Color32> pixels`, registers label `heightPixels`, sets `handleScheduled = true` immediately after `ErosionGrayscalePngBakeJob.Schedule(...)`, completes through `DispatcherJobSwap.TryComplete`, and disposes through `DisposeTracked(ref pixels)` in `finally`.
- `WriteMaskPng`: allocates `NativeArray<Color32> pixels` and `NativeArray<float> maxValue`, registers labels `maskPixels` and `maskMax`, sets `maxHandleScheduled = true` immediately after `ErosionMaskMaxJob.Schedule()`, sets `bakeHandleScheduled = true` immediately after dependent `ErosionMaskPngBakeJob.Schedule(...)`, completes the live handle in `finally`, then unregisters/disposes both arrays through `DisposeTracked`.

Third-pass scans on touched code files:

- Managed pixel arrays: `Color32[]`, `new Color32[]`, `SetPixels32`: 0 matches.
- LINQ operator calls, `foreach`, `string.Format`, `Update`, `Tick`, `SlowTick`: 0 executable-code matches. The only broad-token `from` hits were XML comments in `HectonSandboxAbyssalShelfJobs.cs`.
- `transform.position`, `Vector3.Distance`, `math.distance`, `GlobalRegistry.Get`, `GlobalRegistry.`: 0 matches.

Third-pass verification:

- `git diff --check -- Assets/_Project/Scripts/Editor/ErosionTestHarness.cs Docs/Reports/2026-05-05_HECTON_SANDBOX_BIOMES_OMEGA_SURGERY_LOG.md Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke.json`: exit `0`; CRLF normalization warnings only.
- `dotnet build Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke/HectonSandboxAbyssalShelfStandaloneSmoke.csproj -v:minimal`: exit `0`, `0 Warning(s)`, `0 Error(s)`.
- `dotnet run --project Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke/HectonSandboxAbyssalShelfStandaloneSmoke.csproj --configuration Release -- --output Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke.json`: exit `0`, status `MACRO SHELF VERIFIED`, invalid `0`, cliff samples `0`, average active slope `30.718`, AUP delta `0`, far-boundary delta `0.31`.
- First Unity console read after terrain compile workers settled returned `0` error entries.
- Final Unity console retry returned one unrelated Burst exception for `Hecton8.World.WorldProceduralTerrainSlopeCavitySplatmapJob` in `Hecton8.Core`; no console diagnostic pointed at `ErosionTestHarness.cs` or the sandbox shelf files.

Third-pass validation blockers:

- `validate_script` through Unity MCP did not produce a semantic script report. First attempt disconnected while awaiting `command_result`; second attempt timed out. Unity console showed MCP regex timeout inside `ManageScript.CheckDuplicateMethodSignatures`, not a script compiler diagnostic.
- Generated Editor project build did not produce a stable full-project compiler verdict. One `dotnet build Hecton8.Editor.csproj` run exited `1` after package/dependency compilation without a surfaced `ErosionTestHarness.cs` error; a quiet retry timed out and left MSBuild workers alive. Those verification workers were stopped; Unity console later reported `0` errors.

## Planetary Shelf Sculpting Pass

Status: `PENDING VERIFICATION`; standalone smoke harness recorded the historical `MACRO SHELF VERIFIED` label only.

Mandates applied:

- `MATH_Coordinate_Precision_AUP_FloatingOrigin`: shelf sampling is AUP-first; `transform.position` is not used for terrain distance or noise phase.
- `OPT_Native_Memory_Collections_JobSystem_Protocol`: new slope-weight `NativeArray` paths are Sentinel-registered and disposed after cold MapMagic/smoke sync jobs complete.
- `OPT_Zero_GC_Policy_AllocFree_Mandate`: no LINQ, hot-path string formatting, `GlobalRegistry.Get`, transform distance calls, or runtime `foreach` were added to the touched shelf/splat jobs.
- `VOX_MapMagic_Voxel_Seam_Alignment_Integration`: MapMagic terrain/splat outputs remain AUP-compatible matrix products; no Unity Terrain API clone path was introduced.
- `STRM_World_Streaming_Residency_Chunk_Management`: shelf generation remains job-backed and uses absolute coordinates, not chunk-local float-only noise.

Implementation:

- `HectonSandboxAbyssalShelfMath` now gates positive height through an island macro-mask. Heights above sea level survive only at the central island cluster or Voronoi junction island gates; all other shelf/ridge terrain below or at sea level remains continuous into the abyss.
- `HectonSandboxSlopeQuantizationJob` now matches the requested slope policy: source slopes below `15` degrees are biased toward `3.5` degrees; source slopes above `45` degrees are biased toward `80` degrees. The operation rescales center-cell deviation from its 4-neighbor average, so it creates slanted broad shelves and cliff bands rather than discrete stair steps.
- `HectonSandboxAbyssalShelfMapMagicNode` resolves `GlobalRegistry.WorldSeedProvider.RuntimeWorldSeed` when available, combines it with the authoring seed, and the Burst sampler derives a deterministic macro seed from AUP `GridX/GridZ` in 100km groups. This avoids float precision loss at distance while keeping 5km AUP cell borders continuous.
- `WorldProceduralTerrainSlopeCavitySplatmapJob` now writes a `SlopeWeights01` `NativeArray<float>` alongside sand/rock/silt/cavity weights.
- `HectonTerrainSplatmapMapMagicNode` now exposes a `Slope Weight` outlet matrix. Sharp slope weight drives the rock/brittle path; flat cells remain sand/silt candidates.
- `PlanetaryCanvasMapMagicGraphIntegrator` maps texture layers containing `brittle` to the rock outlet, matching the requested Brittle Rock cliff texture path.

Great Descent formula:

```text
t = saturate(length(AUP.xz) / DescentRadiusMeters)
descent01 = (1 - exp(-Falloff * t^2)) / (1 - exp(-Falloff))
baseY = lerp(+2000m, -5000m, descent01)
```

With `DescentRadiusMeters = 17500` and `Falloff = 3.1`, the derivative is shallow at island center, steepest through the transition band, and clamped to abyssal plain after the radius.

Ridge Warp formula:

```text
p = AUP.xz * DomainWarpFrequency
twist = fbm_perlin(0.73p + [31.19, -22.7], seed ^ 0x1B56C4E9) * 2 - 1
angle = twist * 60deg
warpRaw =
    0.72 * [fbm_perlin(p, seed ^ 0x5F356495), fbm_perlin(p + [17.317, -41.113], seed ^ 0xC2B2AE35)] * 2 - 1
  + 0.28 * [fbm_perlin(2.37p + [-61.7, 8.31], seed ^ 0xB5297A4D), fbm_perlin(2.11p + [4.89, 73.2], seed ^ 0x68E31DA4)] * 2 - 1
warp = rotate(warpRaw, angle) * DomainWarpMeters
platePos = (AUP.xz + warp) / PlateCellSizeMeters
```

Voronoi ridge extraction:

```text
F1,F2,F3 = nearest three warped Voronoi feature distances
edgeMask = 1 - smoothstep(0.18 * RidgeWidth, RidgeWidth, (F2 - F1) * PlateCellSize)
junctionMask = 1 - smoothstep(0.22 * JunctionWidth, JunctionWidth, (F3 - F2) * PlateCellSize)
ridgeMask = saturate((0.82 * edgeMask + 0.72 * junctionMask + 0.10 * forkNoise) * irregularity)
islandMask = saturate(max(centerIsland, junctionMask * smoothstep(IslandThreshold, IslandThreshold + 0.22, islandNoise)))
height = height > 0 ? height * islandMask : height
```

Verification:

- `dotnet build Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke/HectonSandboxAbyssalShelfStandaloneSmoke.csproj -v:minimal`: exit `0`, `0 Warning(s)`, `0 Error(s)`.
- `dotnet run --project Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke/HectonSandboxAbyssalShelfStandaloneSmoke.csproj --configuration Release -- --output Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke.json`: exit `0`.
- Latest smoke JSON: status `MACRO SHELF VERIFIED`, samples `16`, invalid `0`, cliff samples `2`, plateau samples `10`, min height `-5000`, max height `2000`, max slope `60.393`, average slope `14.759`, average active slope `30.594`, slope-30 samples `3`, AUP delta `0`, AUP boundary delta `0`, far chunk invalid `0`.
- `git diff --check` on touched shelf/splat/smoke files: exit `0`; Git emitted CRLF normalization warnings only.

Validation blockers:

- Unity MCP `validate_script` disconnected while awaiting `command_result`; no semantic MCP validation report was produced.
- Direct Bee `Hecton8.Core` compile is currently blocked by unrelated `Assets/_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs(219,38): CS0117 SignalStrengthSystem.StrengthToBand`. No compiler diagnostic in that run pointed at the shelf/splat files.
