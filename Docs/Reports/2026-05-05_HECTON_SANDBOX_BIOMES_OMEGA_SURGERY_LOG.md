# HECTON-8 Sandbox Biomes Macro Shelf Surgery Log

Date: 2026-05-05
Domain: `03_HECTON_SANDBOX_BIOMES`
Status: MACRO SHELF VERIFIED

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
{"status":"MACRO SHELF VERIFIED","tester":"HectonSandboxAbyssalShelfStandaloneSmoke","samples":12,"invalid":0,"cliffSamples":0,"plateauSamples":8,"minHeightMeters":-5000,"maxHeightMeters":2000,"maxSlopeDegrees":39.043,"averageSlopeDegrees":12.141,"averageActiveSlopeDegrees":30.718,"slope30Samples":2,"aupDeltaMeters":0,"aupBoundaryDeltaMeters":0.31,"originChunkInvalid":0,"farChunkInvalid":0,"highChunkAupDeltaMeters":0,"elapsedMs":11.856}
```

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
