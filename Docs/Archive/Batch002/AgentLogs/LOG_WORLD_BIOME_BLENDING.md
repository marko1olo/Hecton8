# LOG_WORLD_BIOME_BLENDING

## 2026-05-12 00:42:38 +04:00 - WORLD_BIOME_BLENDING

Status: PENDING VERIFICATION.

What was wrong:
- Terrain biome transitions depended on old sand/rock blending and lacked a Data Monolith heatmap GPU path.
- Micro-scatter was not biome-aware and did not have AUP-stable generation hashing.
- There was no `CurrentBiomeColor` global for post/water fog consumers.
- Terrain/material recon evidence was missing.

What was done:
- `GPUScatterDirector.cs`: added persistent 256x256 R8 heatmap upload from Data Monolith, global `_HectonBiomeHeatmapTex`, `_HectonBiomeGroundArray`, `_HectonBiomeTextureParams`, `_HectonScatterBiomeParams`, `_HectonScatterAupGridOffset`, and `_CurrentBiomeColor`.
- `TerrainMaster.shader`: added four-neighbor heatmap ID lookup and IGN selection into one `Texture2DArray` slice. Fallback keeps legacy sand/rock path when no biome array is bound.
- `Hecton_GpuScatter.compute`: added biome heatmap sampling, biome-driven scatter density/species/scale, and AUP-stable snapped cell hashing.
- `Hecton_ScatterIndirectLit.shader`: added procedural rock displacement from an AUP-stable hash.
- `RECON_WORLD_BIOME_BLENDING.md`: logged terrain material scan; no >4 splat-map material was found.
- `Rationale_WORLD_BIOME_BLENDING.md`: logged decisions and Omega polish.
- `Status_WORLD_BIOME_BLENDING.md`: updated all 15 tasks, with Task 15 blocked by unrelated compile dependencies.

Cinematic cheats used:
- IGN dither plus TAA illusion instead of honest four-way texture blending.
- One generic mesh with AUP-hash displacement instead of many rock meshes.
- GPU height-payload normal derivative for slope rejection instead of CPU normal reads.
- Biome-ID hash scatter variation instead of biome-specific object graphs.

Exact microseconds saved:
- Terrain biome path: estimated 35 us saved per 100k pixels on MX350-class fill by avoiding four-way splat sampling.
- Low-tier scatter cull: estimated 90 us saved per scatter pass by clamping at 15m.
- Movement updates: estimated 300 us saved by avoiding movement-triggered `GraphicsBuffer` recreation.
- AUP shift: estimated 15 us saved per shift by rebasing grid hash instead of rebuilding scatter buffers.
- Omega polish: estimated sub-5 us saved in dense ID conversion paths by replacing `round()` with `+0.5` casts.

Verification:
- `validate_script` reported 0 diagnostics for `GPUScatterDirector.cs` after Omega polish.
- Unity console currently reports unrelated compile errors outside `WORLD_BIOME_BLENDING` files. No current console error names `GPUScatterDirector`, `TerrainMaster`, `Hecton_GpuScatter`, or `Hecton_ScatterIndirectLit`.

## 2026-05-12 00:52:10 +04:00 - HONEST R&D UPGRADE 1

Status: PENDING VERIFICATION.

What was wrong:
- Heatmap byte IDs used a folded `BiomeHash`. Stable, but not authored truth.
- `CurrentBiomeColor` used synthetic hash color instead of Data Monolith `H8BiomeRecord.LightScatterRGB`.

What was done:
- `GPUScatterDirector.cs`: added unsafe cold-path lookup into the Data Monolith `Biomes` section.
- This pass moved heatmap byte encoding toward authored records; R&D Upgrade 3 supersedes the temporary `HeatmapId`/`SurfaceId` mapping because those fields are hashes, not texture-array slices.
- Current biome fog color now uses `LightScatterR/G/B` and `FogDensity` from `H8BiomeRecord`.
- Published both `_CurrentBiomeColor` and `CurrentBiomeColor`.

Cinematic Cheats used:
- Kept R8 heatmap ID texture instead of uploading full biome structs to GPU.
- Kept pointer scan cold-path instead of a managed dictionary.

Exact Microseconds saved:
- Runtime hot path unchanged: 0 us added per frame.
- Avoided managed dictionary allocation/lookup path: estimated 5-20 us saved during monolith upload setup and 0 B GC.

Verification:
- `validate_script Assets/_Project/Scripts/World/GPUScatterDirector.cs`: 0 errors, 0 warnings.

## 2026-05-12 00:53:29 +04:00 - HONEST R&D UPGRADE 2

Status: PENDING VERIFICATION.

What was wrong:
- Authored biome record lookup used a linear scan even though the monolith compiler sorts biome records by `BiomeHash`.

What was done:
- `TryResolveBiomeRecord` now uses binary search over `H8BiomeRecord` data.

Cinematic Cheats used:
- None. This is pure cold-path data lookup hygiene.

Exact Microseconds saved:
- For a 64-biome table, lookup comparisons drop from up to 64 to about 6 per heatmap cell. This is cold boot/reload work only; runtime frame cost remains 0 us.

Verification:
- `validate_script Assets/_Project/Scripts/World/GPUScatterDirector.cs`: 0 errors, 0 warnings.

## 2026-05-12 01:34:21 +04:00 - HONEST R&D UPGRADE 3

Status: PENDING VERIFICATION.

What was wrong:
- `HeatmapId` and `SurfaceId` are Data Monolith string hashes. They are not dense `Texture2DArray` indices.
- The temporary hash-as-slice mapping could collapse most authored biomes onto the final texture slice after clamping.

What was done:
- `GPUScatterDirector.cs`: heatmap bytes now encode `H8BiomeRecord.RecordIndex + 1`; `0` remains the missing-biome sentinel.
- Unknown-record fallback is bounded by assigned `Texture2DArray.depth` when a biome array exists.
- `TerrainMaster.shader`: converts selected R8 ID to `encodedBiomeId`, then samples slice `encodedBiomeId - 1` from `_HectonBiomeGroundArray`.

Cinematic Cheats used:
- Kept the one-byte heatmap and one texture-array sample per pixel.
- Used monolith record order as the dense slice contract instead of uploading hash remap tables.

Exact Microseconds saved:
- Runtime hot path unchanged: 0 us added per frame.
- Avoided a GPU hash remap buffer/table: estimated 64 KB to several hundred KB VRAM avoided depending on biome count, and 1 extra dependent lookup avoided per terrain pixel.

Verification:
- `validate_script Assets/_Project/Scripts/World/GPUScatterDirector.cs`: 0 errors, 0 warnings after retry.
- Latest `refresh_unity` compile request timed out after 60s waiting for editor readiness.
- Latest `read_console` retry failed because the Unity session did not answer ping. Status remains PENDING VERIFICATION.

## 2026-05-12 01:42:19 +04:00 - HONEST R&D UPGRADE 4

Status: PENDING VERIFICATION.

What was wrong:
- Heatmap upload invalidation used Data Monolith byte length only.
- A same-size rebake could leave stale biome IDs resident in `_HectonBiomeHeatmapTex`.

What was done:
- `GPUScatterDirector.cs`: added `_biomeHeatmapBlobChecksum` and compares `H8StaticDataArena.Header.Checksum64` with byte length before skipping upload.
- Heatmap resource resets now clear both byte length and checksum sentinels.

Cinematic Cheats used:
- None. This is correctness hygiene on a cold upload gate.

Exact Microseconds saved:
- Runtime hot path unchanged: 0 us added per frame.
- Avoided forced per-frame or per-movement upload: estimated 64 KB texture upload avoided whenever the monolith is unchanged.

Verification:
- First `validate_script` attempt disconnected from Unity MCP.
- Retry `validate_script Assets/_Project/Scripts/World/GPUScatterDirector.cs`: 0 errors, 0 warnings.

## 2026-05-12 01:53:48 +04:00 - HONEST R&D UPGRADE 5

Status: PENDING VERIFICATION.

What was wrong:
- `GPUScatterDirector` had no fixed 300-frame black-box telemetry ring despite owning critical terrain/scatter GPU state.
- A NaN/invalid scatter state would not dump recent high-level state to disk.

What was done:
- Added `NativeArray<ScatterTelemetryEntry>[300]` with frame, flags, center, AUP offset, radius, cell size, grid resolution, candidate count, biome hash, visible count, state hash, origin shift sequence, and monolith checksum low bits.
- Added invalid-state dump to `Docs/AgentLogs/Dump_WORLD_BIOME_BLENDING.bin`.
- Recorded missing dependency, origin shift, and normal scatter frames without per-candidate telemetry.

Cinematic Cheats used:
- One compact high-level ring instead of verbose per-instance forensic logs.
- Binary dump instead of JSON/string diagnostics.

Exact Microseconds saved:
- Avoided managed logging/JSON allocation in runtime: 0 B GC per frame.
- Per-frame telemetry write is estimated below 5 us; dump I/O only happens on invalid state.

Verification:
- First `validate_script` attempt disconnected from Unity MCP.
- Standard `validate_script` retry hit an MCP regex timeout, not compiler diagnostics.
- `validate_script Assets/_Project/Scripts/World/GPUScatterDirector.cs` at `basic` level: 0 errors, 0 warnings.

## 2026-05-12 01:58:28 +04:00 - FINAL R&D VERIFICATION ATTEMPT

Status: PENDING VERIFICATION.

What was wrong:
- Unity MCP became unstable after the file grew: `validate_script` alternated between session disconnects and regex timeout.
- Project-level compile proof remains unavailable from this agent session.

What was done:
- Re-extracted `WORLD_BIOME_BLENDING` prompt from `CURRENT_BATCH.md` after three R&D passes.
- Ran `git diff --check` on touched files: no whitespace errors.
- Ran anti-bloat `rg` for hot-path `round(`, managed string formatting, `foreach`, and managed byte/uint arrays: no matches in touched runtime files.
- Tried `dotnet build Assembly-CSharp.csproj --no-restore`; it timed out after 124s without usable diagnostics.

Cinematic Cheats used:
- None. Verification only.

Exact Microseconds saved:
- 0 us runtime. This pass only preserved evidence integrity.

Verification:
- Status remains PENDING VERIFICATION by prompt requirement and blocked editor/project state.
