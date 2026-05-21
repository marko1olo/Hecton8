# Hydraulic Erosion Baker - SHINOBU_242

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Status: PENDING VERIFICATION

## Ownership
- Owner: SHINOBU_242 / HYDRAULIC_EROSION_SIMULATOR_BAKER.
- Domain: ECHELON 2 WORLD GENERATION & TERRAIN.
- Runtime route: none. The droplet simulator is Editor-only and writes immutable `.h8bin` payloads.

## Data Route
- Authoring input: `Assets/_Project/Data/Terrain/terrain_weathering_profiles.csv`.
- Sector outputs: `Assets/StreamingAssets/Hecton8/TerrainErosion/sector_XXXX_ZZZZ_height.h8bin`.
- Silt outputs: `Assets/StreamingAssets/Hecton8/TerrainErosion/sector_XXXX_ZZZZ_silt.h8bin`.
- Seam transfer outputs: `Assets/StreamingAssets/Hecton8/TerrainErosion/sector_XXXX_ZZZZ_{north|south|east|west}.h8seam`.
- Macro output: `Assets/StreamingAssets/Hecton8/TerrainErosion/macro_erosion.h8bin`.
- Report: `Docs/Reports/EROSION_BAKE_REPORT.json`.

## Binary Contract
- Header: `ErosionHeightmapFileHeaderDTO`, 160 bytes.
- Payload: raw little-endian `float32` values.
- Endian marker: `0x01020304` in every height/silt/macro/seam header.
- Height payload kind: `1`.
- Silt payload kind: `2`.
- Macro payload kind: `3`.
- Seam header: `ErosionSeamTransferFileHeaderDTO`, 160 bytes, magic `HSEM`, payload `ErosionDropletDTO[32]`. Directional sidecars are always rewritten; zero-transfer directions carry a valid zero-count header to prevent stale handoff files.
- `PayloadFlagRollbackExcluded` is mandatory. These files are static world data, not rollback gameplay state.

## Seam Queue Memory
- Directional `NativeQueue<ErosionDropletDTO>` lanes are cold-prewarmed to expected capacity before the Burst erosion job is scheduled.
- `NativeMemorySentinel` registration happens after queue prewarm, so the tracked bytes describe the intended seam-transfer budget rather than an abstract label.
- This keeps allocator growth out of the boundary-transfer phase while preserving chunk-local bake memory.

## Data Monolith / Vault Boundary
- These files are sidecar `StreamingAssets/Hecton8/TerrainErosion` terrain-cache payloads, not `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- The editor baker owns no persistent runtime memory and requests no `GlobalDataVault` handles.
- A future runtime terrain-streaming owner must validate header/checksum/finite payloads, own Vault handles if needed, own generation/disposal, and publish immutable snapshots from its owner phase.
- `GlobalDataVault.TryGetLatestCreated()` is not allowed as a terrain erosion fallback.

## Compile Wall
- Source boundary: `Assets/_Project/Scripts/Editor/HydraulicErosionForge/Shinobu242`.
- Assembly: `Hecton8.World.HydraulicErosionForge.Editor`.
- References: `Hecton8.Core` and Unity Burst/Collections/Jobs/Mathematics packages only. No sibling runtime domain is referenced.

## Netcode Fence
The erosion height and silt arrays are excluded from `StateRingBuffer` and Merkle leaves. Runtime netcode synchronizes dynamic entities over terrain; terrain erosion payloads are immutable environment data loaded from StreamingAssets.

## Failure Proof
- DTO layout proof: `Docs/Reports/SHINOBU_242_SELF_AUDIT.xml`.
- Runtime mutation scan: `Docs/Reports/WORLD_OPTIMIZATION_REPORT.json`.
- Black Box dump path: `Docs/AgentLogs/Dump_SHINOBU_242.bin`.

## Editor Facade
- `Hydraulic Erosion Forge` exposes droplet count, rain, evaporation, sediment capacity, erosion aggressiveness, and `GlobalQualityWeight`.
- The droplet slider range starts at `0` for baseline serializer/scanner diagnostics and defaults to the one-million-droplet production path.
- All technical sliders expose numeric input fields for reproducible profile matching.
- Slider changes coalesce through `EditorApplication.delayCall` and refresh the reduced Burst preview patch unless a full bake is active.
- Preview droplet count is capped by `PreviewDropletCount` while still honoring lower designer-requested counts.

## Scalability
- Low: `GlobalQualityWeight` collapses droplet lifetime, capacity, erosion spread, and sampling toward cheaper nearest-style evaluation.
- Middle: `smoothstep` ramps interpolation, erosion strength, and capacity without binary hardware switches.
- High: bilinear sampling, longer droplet lifetime, and richer silt masks feed better shader blending.
- Ultra: visual overkill is paid in shader/tessellation and downstream material detail, not by rerunning erosion at runtime.
