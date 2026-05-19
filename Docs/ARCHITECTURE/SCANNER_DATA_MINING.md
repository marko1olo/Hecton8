# Scanner Data Mining
Date: 2026-05-07

Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Verification: PENDING VERIFICATION

## 2026-05-18 SHINOBU_24 Scanner Router Boundary

- Current scanner router implementation: `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs`.
- Persistent scanner data lives in `GlobalDataVault` under `BufferID.ShinobuScannerEntities` through `BufferID.ShinobuScannerSettings` (`70640..70651`).
- Runtime spatial lookup is a flat bucket hash: `BucketHeads[int]` plus `BucketNext[int]`; no runtime-owned `NativeParallelMultiHashMap`.
- Gameplay truth uses bounding-sphere ray intersection and midpoint SDF occlusion. Unity `Physics.Raycast`, collider queries, and component lookup are outside this scanner path.
- `ScannerVfxDTO` is the 32-byte visual bridge; VFX consumers must read the DTO/buffer and must not query scanner internals.
- Designer tuning path is `DataMiningTunerWindow` -> `ScannerSettingsDTO` in the vault during Play Mode, with static fallback only when the vault is unavailable.
- Black-box dump path writes both `Docs/AgentLogs/Dump_SHINOBU_24.bin` and `Docs/AgentLogs/Dump_SHINOBU_24.h8dump` on NaN/budget breach.
- Continuous CSV disk monitoring is not a player hot-path responsibility; the scanner exposes a zero-GC metadata line parser and expects file IO to arrive through a cold/background bridge.

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical data boundary snapshot: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Current actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json`.
- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Visual-realistic-fake doctrine snapshot: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`; re-check `.agents-skills` for newer mandates before implementation.
- Historical May 14/R43 CLI compile wording is stale report text, not current proof. Current R31 static/tool boundary: R31 is the latest DOC_GLOBAL root/architecture current-boundary propagation layer; R30 remains the prior internal-currentness layer; AtlasCheck fails `57` RealtimeCSG refs; Mod API static validation now passes (`Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. Unity import, Console, Play Mode, profiler, GCMonitor, player build, scene wiring, save/load, and visual proof remain PENDING VERIFICATION.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
## Historical 2026-05-04 Boundary

- Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using this scanner map as current runtime truth.
- This document is a PDA/scanner/data-mining contract, not proof that sonar SDF, lore unlocks, scan fragments, or UI text paths are runtime-validated.
- Re-open PDA, scanner, lore, shader, and save owners before surgery.

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
