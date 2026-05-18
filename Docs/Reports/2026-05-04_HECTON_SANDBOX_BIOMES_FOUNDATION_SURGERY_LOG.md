<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# HECTON Sandbox Biomes Foundation Surgery Log

Date: 2026-05-07
Scene: `Assets/_Project/Scenes/03_HECTON_SANDBOX_BIOMES.unity`
Status: PENDING VERIFICATION

## Mandates Loaded

- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `STRM_World_Streaming_Residency_Chunk_Management.txt`

## Files Added

- `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfJobs.cs`
- `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfMapMagicNode.cs`
- `Assets/_Project/Scenes/03_HECTON_SANDBOX_BIOMES.unity`
- `Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_BAKED_PREVIEW.asset`
- `Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset`

`02_HECTON_WORLD` was not modified.

## Great Descent Formula

Input is AUP XZ in meters. Constants:

- `highY = 2000`
- `lowY = -5000`
- `R = 16500`
- `AbsoluteUniversePosition.CellSizeMeters = 5000`

AUP alignment:

```text
gridX = floor(absX / cellSize)
gridZ = floor(absZ / cellSize)
localX = absX - gridX * cellSize
localZ = absZ - gridZ * cellSize
aupX = gridX * cellSize + localX
aupZ = gridZ * cellSize + localZ
```

Macro slope:

```text
r = sqrt(aupX^2 + aupZ^2)
t = saturate(r / R)
s = t^2 * (3 - 2t)
yBase = lerp(2000, -5000, s)
```

This gives a deterministic shelf descent from +2000 m to -5000 m over a 16.5 km radius.

## Voronoi Ridge Formula

Warped plate position:

```text
warp = fractalValueNoise4(aupXZ * 0.00018, seed) * 480
p = (aupXZ + warp) / plateCellSize
plateCellSize = 2200
```

Feature point per plate cell:

```text
F_i = cell_i + lerp((0.5, 0.5), hash2(cell_i, seed), plateUniformity)
plateUniformity = 0.86
```

Nearest cell distances:

```text
d1 <= d2 <= d3
edgeDeltaMeters = (sqrt(d2) - sqrt(d1)) * plateCellSize
junctionDeltaMeters = (sqrt(d3) - sqrt(d2)) * plateCellSize
```

Branching ridge masks:

```text
edge = 1 - smoothstep(0.25 * ridgeWidth, ridgeWidth, edgeDeltaMeters)
junction = 1 - smoothstep(0.35 * junctionWidth, junctionWidth, junctionDeltaMeters)
ridge = saturate((edge + 0.55 * junction) * irregularity)
```

Constants:

```text
ridgeWidth = 190
junctionWidth = 360
ridgeHeight = 1750
ridgeMultiplier = 0.22
```

Height composition:

```text
h01Base = (yBase - lowY) / (highY - lowY)
h01 = saturate(h01Base * (1 + ridgeMultiplier * ridge)
      + (ridgeHeight / (highY - lowY)) * ridge)
y = lowY + h01 * (highY - lowY)
```

Ridge edges are Voronoi cell boundaries. Junctions are areas where three cells compete. Smoothstep prevents perfect triangular extrusion.

## Slope Quantization Formula

Central difference over normalized height converted to meters:

```text
dx = (rightMeters - leftMeters) / (2 * cellSizeMeters)
dz = (forwardMeters - backMeters) / (2 * cellSizeMeters)
gradient = sqrt(dx^2 + dz^2)
angle = atan(gradient)
```

Plateaus:

```text
plateauMask = 1 - smoothstep(2 degrees, 15 degrees, angle)
plateauFactor = tan(2 degrees) / gradient
```

Cliffs:

```text
cliffMask = smoothstep(45 degrees, 80 degrees, angle)
cliffFactor = tan(80 degrees) / gradient
```

Resolution:

```text
factor = lerp(1, plateauFactor, plateauMask * 0.72)
factor = lerp(factor, cliffFactor, cliffMask * 0.72)
yResolved = neighborAverage + (center - neighborAverage) * clamp(factor, 0.02, 8)
```

## MapMagic Injection

Source node:

```text
MapMagic.Nodes.MatrixGenerators.HectonSandboxAbyssalShelfMapMagicNode
Generator menu: Hecton / Sandbox Abyssal Shelf Base
Output: MatrixWorld base height product
```

The node allocates `NativeArray<float>` buffers with `Allocator.TempJob`, schedules Burst jobs, completes before `TileData.StoreProduct`, and disposes buffers in `finally`.

Live graph instantiation is blocked by unrelated project compiler errors, so `HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset` is an empty placeholder and the scene contains `[SANDBOX_MAPMAGIC_BINDING_COMPILE_BLOCKED]` inactive. The custom node source is present and validates cleanly; Unity cannot load the type until the dirty production files compile.

## Verification

Verified:

- `HectonSandboxAbyssalShelfJobs.cs`: Unity `validate_script` returned 0 errors, 0 warnings.
- `HectonSandboxAbyssalShelfMapMagicNode.cs`: Unity `validate_script` returned 0 errors, 0 warnings.
- `03_HECTON_SANDBOX_BIOMES.unity` exists and contains:
  - `[SANDBOX_TECTONIC_SHELF_BAKED_PREVIEW]`
  - `[SANDBOX_MAPMAGIC_BINDING_COMPILE_BLOCKED]`
  - `[AUP_CELL_SIZE_METERS_5000_DETERMINISTIC_XZ]`
  - `[DESCENT_RADIUS_16500M_HEIGHT_-5000_TO_2000]`
- Baked preview terrain asset exists at `Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_BAKED_PREVIEW.asset`.

Compiler blockers outside sandbox:

- `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`
- `Assets/_Project/Scripts/HectonSurvivalSystem.cs`
- `Assets/_Project/Scripts/Gameplay/StorageCrate.cs`
- `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs`

These files were already modified in the worktree. They prevent domain reload and live MapMagic graph population.

