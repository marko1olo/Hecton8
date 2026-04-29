# Scanner Data Mining

## Owners
- `PDAMapTab.cs`: PDA sonar viewport owner. Uploads the published cave SDF, drives the raymarch material, and stages status text through `CharBufferPool`.
- `Hecton_PDA_SonarMap.shader`: local-space sonar projection. Raymarches the SDF inside a bounded hologram box and renders cyan wireframe occupancy plus threat pings.
- `LoreDatabaseManager.cs`: fixed industrial lore bank and runtime unlock word-mask owner.
- `PDADataLogTab.cs`: archive presentation owner. Reads lore unlock words to decide which records are visible.
- `ScannerTool.cs`: scientific scan owner. Samples cave density, hazards, and chemical trails.
- `ResearchDataTemplate.cs`: per-fragment authored scan contract.

## Zero-GC Rules
- Runtime lore unlock state is stored in `NativeArray<uint>[2]` for the 50-record industrial bank.
- Unlocking a lore record resolves one fixed index and applies one word-level bitwise OR.
- PDA HUD text uses `CharBufferPool` and `FixedCharBuffer`. No runtime string formatting is required for the scanner operational summary.
- No runtime string dictionary lookups are used for lore state. `LoreDatabaseManager` resolves by stable FNV-1a hash.

## PDA Sonar Map
- `HectonVoxelVolume` publishes:
  - `gridDimensions`
  - `volumeOrigin`
  - `voxelCellSize`
  - `encodedSdf`
  - `sdfRange`
- `PDAMapTab` uploads `encodedSdf` into a `Texture3D`.
- The local hologram box is scaled from the world-space voxel extents:
  - `worldHalfExtent = (gridDimensions - 1) * voxelCellSize * 0.5`
  - `localScale = 0.55 / max(worldHalfExtent)`
  - `localHalfExtent = worldHalfExtent * localScale`

## SDF Raymarch Math
- The fragment shader builds a view ray from PDA UV space into a local 3D box.
- It intersects that ray against the local volume AABB.
- Marching starts at `tEnter` and ends at `tExit`, not through a hardcoded cube.
- Local sample position is converted to volume UVW:
  - `uvw = saturate((position - volumeMin) / (volumeMax - volumeMin))`
- Encoded SDF decode:
  - `sdf = ((encoded * 2) - 1) * sdfRange`
- Near-surface shell:
  - `surfaceBand = 1 - saturate(abs(sdf) / shellThickness)`
- Wireframe cell mask:
  - `gridPos = uvw * (gridDimensions - 1)`
  - `cellFrac = abs(frac(gridPos) - 0.5)`
  - `wire = 1 - smoothstep(thin, thick, min(cellFrac.x, cellFrac.y, cellFrac.z))`
- Final cyan response:
  - `wireStrength = wire + surfaceBand + fresnel`
- Result: occupied cave mass renders as cyan wireframe shells instead of a flat screen-space fill.

## Threat Ping Overlay
- `PDAMapTab` pulls the acoustic radar grid from `GlobalRegistry.Audio`.
- The eight strongest bins are converted into local offsets and uploaded as `_ThreatPings[8]`.
- The shader adds a pulsing red threat halo with radial falloff and `_TimePhase`.

## Lore Unlock Storage
- Bank size: 50 records.
- Runtime layout:
  - word `0`: bits `0..31`
  - word `1`: bits `32..49`
- Save layout:
  - one packed `ulong`
- Unlock operation:
  - `wordIndex = index >> 5`
  - `bitMask = 1u << (index & 31)`
  - `_unlockedWords[wordIndex] |= bitMask`
- `PDADataLogTab` reads those bits through the packed-word API and never asks `AudioLogSystem` for discovery ownership.

## Molecular Scanner Bridge
- `ScannerTool` samples the published `ChemicalInfluenceGrid` at each scientific hit point.
- Combined channel layout:
  - `x`: blood
  - `y`: exhaust
  - `z`: fear
- Generic chemical load:
  - `chemicalLoad01 = saturate(max(abs(channels)))`
- Organic blood trace:
  - `organicBlood01 = saturate(channels.x)`
- When `organicBlood01 > 0.1`, the operational summary appends:
  - `TRACES OF ORGANIC BLOOD DETECTED`
- That message stays allocation-free because `HUDQuickBar` already stages tool summaries through `CharBufferPool`.

## Research Data Templates
- `ResearchDataTemplate` remains the per-target authored contract:
  - `ScanDuration`
  - staged lore unlock masks
  - reward/hash link for hologram proxy lookup
- One template maps to one abyss research subject. The runtime path already supports ten or more authored items without a second owner.

## GC Sweep
- No live `GC.Collect()` call exists under `Assets/_Project/Scripts`.
- Archive and documentation references are not runtime hot-path owners and were not copied into gameplay code.
