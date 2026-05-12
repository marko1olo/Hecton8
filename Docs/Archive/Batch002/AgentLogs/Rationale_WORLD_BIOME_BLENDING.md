# Rationale_WORLD_BIOME_BLENDING

Status: PENDING VERIFICATION.

## Decision 0 - Prompt Isolation And Mandates

Problem: The batch file contains many neighboring agent prompts; terrain biome work must not ingest unrelated architecture instructions as task scope.
Solution: Extracted only `<AGENT_PROMPT id="WORLD_BIOME_BLENDING">` from `Docs/Tasks/CURRENT_BATCH.md` with a raw PowerShell regex, then selected eight task-relevant mandates: `REND_Shader_Noir_Aesthetics_Dithering_Fog`, `REND_Terrain_VirtualTexturing`, `GPU_Compute_Kernels_Kernels_Optimization_MX350`, `REND_GPU_Occlusion_Culling_6000`, `MATH_Coordinate_Precision_AUP_FloatingOrigin`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits`, and `VOX_MapMagic_Voxel_Seam_Alignment_Integration`.
Rejected Alternatives: Reading every mandate would waste context and increase cross-domain bleed; using chat memory would violate strict batch parsing.
Scalability potential: Low uses one-sample dithered terrain array and 15m micro-scatter; Middle extends range with same data path; High adds culling integration and denser scatter; Ultra spends saved CPU/GPU budget on richer visible micro-variation, not more splat samples.
Hardware Impact: Expected low-end impact on i3/MX350 is reduced terrain texture fetch pressure by avoiding 4-way alpha splat blending; measured proof absent.

## Decision 1 - Heatmap Transport

Problem: Terrain and scatter need the Data Monolith biome heatmap on GPU without generating garbage or binding a CPU lookup path into shaders.
Solution: `GPUScatterDirector` creates a persistent 256x256 R8 `Texture2D` and a persistent `NativeArray<byte>` upload staging buffer. The upload happens only when resident monolith bytes change, then `_HectonBiomeHeatmapTex` is global for terrain, scatter, and post-process consumers.
Rejected Alternatives: Per-frame managed `byte[]` uploads create GC; `StructuredBuffer<uint>` would preserve hashes but increase bandwidth and indexing cost for terrain pixels; direct MapMagic sampling would bypass monolith authority.
Scalability potential: Low uses the same R8 LUT with no extra texture state; Middle/High/Ultra can bind denser biome arrays while retaining the same 1-byte ID lookup.
Hardware Impact: MX350-class path pays 64 KB heatmap VRAM and a cold upload; expected frame impact is effectively 0 us after upload.

## Decision 2 - Dithered Biome Surface

Problem: Existing terrain path blends sand and rock. The task requires biome transition without four-way splat sampling.
Solution: `TerrainMaster.shader` samples four nearest heatmap IDs, uses IGN from screen position to select one ID, clamps it to `_HectonBiomeGroundArray` slice count, and samples exactly one array slice. The legacy two-texture path remains only as a uniform fallback when no array is bound.
Rejected Alternatives: Four texture splats were rejected because they burn bandwidth and violate the prompt; smoothstep material blending was rejected because it smears biome identity and costs extra samples.
Scalability potential: Low gets one sampled albedo/smoothness slice. Middle adds normal/flow detail already present. High/Ultra spend saved bandwidth on higher resolution array slices or stronger weather detail, not more biome samples.
Hardware Impact: Expected i3/MX350 gain is fewer terrain albedo fetches in biome regions; exact microseconds remain PENDING VERIFICATION due unrelated compile blockers.

## Decision 3 - Micro-Scatter Ownership

Problem: The prompt asks for 50,000 micro rocks/shells, but a parallel scatter renderer would duplicate culling, buffers, and indirect draw ownership.
Solution: Extended `Hecton_GpuScatter.compute` and `GPUScatterDirector` instead. The existing persistent buffers, append list, Hi-Z depth pyramid, foveated cache, and `Graphics.RenderMeshIndirect` stay authoritative. Biome ID now controls scatter density, species index, and scale.
Rejected Alternatives: A new compute/renderer pair would add hidden dependencies and another frame-budget liability; GameObjects and prefab pools were rejected outright.
Scalability potential: Low/MX350 budgets and culls at 15m; Mid uses 22m; High/Ultra allow 30m and up to 50k candidates. Ultra can raise texture richness through materials while keeping the same indirect path.
Hardware Impact: Low-end gain comes from avoiding CPU object work and far scatter. Expected CPU savings are larger than GPU savings; measured proof blocked by unrelated compile errors.

## Decision 4 - AUP Stable Grid

Problem: Runtime-space scatter hashes can pop when floating origin shifts even if the visible world is supposed to preserve absolute positions.
Solution: `GPUScatterDirector` registers as an origin-shift listener, stores the committed total offset in `_HectonScatterAupGridOffset`, and compute hashes snapped generation cells in absolute space. The foveated center is rebased by `-ShiftOffset`.
Rejected Alternatives: Dequeueing `AupShiftSignal` from `GlobalSignals` would steal a global queue packet from other consumers; doing nothing would keep runtime hashes vulnerable.
Scalability potential: Same math across tiers. Low gets predictable cheap hashes; High/Ultra can push denser rocks without shift artifacts.
Hardware Impact: Shift handling is one listener update and one global vector write; expected cost below 5 us per shift.

## Decision 5 - Slope Rejection

Problem: Micro-rocks must not spawn on slopes above 45 degrees, but Unity reflection reports no `TerrainData.normalmapTexture` member in this project version.
Solution: Use the already-bound MapMagic height payload in compute to derive a terrain normal and reject when normal Y falls below cos(45). This remains GPU-side and deterministic.
Rejected Alternatives: `TerrainData.GetInterpolatedNormal` is CPU-side and violates hot-path budget; inventing a normal texture dependency would be brittle because the bridge does not expose one.
Scalability potential: Low uses the same derivative normal gate. High/Ultra can improve normal fidelity later if a true MapMagic normal texture becomes available through the bridge.
Hardware Impact: The derivative path is four height samples per accepted candidate stage; CPU cost stays 0 us, GPU cost remains within the existing compute pass.

## Decision 6 - Verification Boundary

Problem: Unity compile cannot reach a clean project state because unrelated systems currently fail compilation.
Solution: Validated `GPUScatterDirector.cs` through MCP script validation and checked Unity console after refresh. Current console errors are outside terrain/scatter files. Task 15 is marked blocked by dependency while core status stays PENDING VERIFICATION.
Rejected Alternatives: Editing Save, Submarine, Manta, Vegetation, Thermal, Signals, Visor, or Combat domains would violate assigned domain boundaries and risk sabotaging other agents.
Scalability potential: Verification block does not affect runtime scalability design; it only blocks final proof.
Hardware Impact: No runtime impact. Integration must clear external compile errors before shader timing can be measured.

## OMEGA POLISH CHANGES

Problem: The first implementation used `round()` for byte-to-slice conversion in shader/compute hot paths. That is honest math where a visual ID only needs deterministic nearest-integer conversion.
Solution: Replaced hot-path `round()` calls with `+ 0.5` casts in `TerrainMaster.shader`, `Hecton_GpuScatter.compute`, and `GPUScatterDirector.cs`. Re-ran anti-bloat search across touched runtime files for `round(`, `sqrt(`, `normalize(`, `foreach`, `string.Format`, `$"`, and `.ToString(`. No new C# managed string formatting or managed foreach was found. Existing shader `rsqrt` usage is already cheap.
Rejected Alternatives: Keeping `round()` was rejected as unnecessary ALU ceremony; adding a CPU-side biome blend table was rejected because the shader can select the slice from four R8 IDs directly.
Scalability potential: Low/MX350 gets cheap deterministic ID casts and 15m scatter culling; Mid gets 22m; High/Ultra gets 30m and up to 50k candidates. Ultra overkill is visual density/material richness, not physically simulated pebbles.
Hardware Impact: Expected sub-5 us improvement in dense pixel/candidate paths. Exact timing remains PENDING VERIFICATION due external compile blockers.

Validation: Post-polish `validate_script` on `Assets/_Project/Scripts/World/GPUScatterDirector.cs` reports 0 errors and 0 warnings.

Cinematic cheats used:
- IGN dither + TAA blend illusion instead of honest four-texture splatting.
- AUP-stable hash deformation on a generic mesh instead of authored rock mesh variety.
- Height-payload normal derivative instead of CPU normal sampling.
- Biome-ID hash density/species selection instead of biome-specific object graphs.

Final Git Diff summary:
```text
Assets/_Project/Art/Shaders/Hecton_GpuScatter.compute | biome heatmap sampling, biome density/species, AUP-stable grid hash
Assets/_Project/Art/Shaders/Hecton_ScatterIndirectLit.shader | AUP-hash procedural rock displacement
Assets/_Project/Art/Shaders/TerrainMaster.shader | Texture2DArray biome path with four-ID IGN selection and one ground sample
Assets/_Project/Scripts/World/GPUScatterDirector.cs | persistent heatmap upload, global shader bindings, tier culling, AUP listener, CurrentBiomeColor
Docs/Tasks/Status_WORLD_BIOME_BLENDING.md | task evidence and dependency-blocked compile status
Docs/AgentLogs/Rationale_WORLD_BIOME_BLENDING.md | decision journal and polish audit
Docs/AgentLogs/RECON_WORLD_BIOME_BLENDING.md | terrain material splat reconnaissance
```

## R&D Upgrade 1 - Biome Record Truth Source

Problem: The first heatmap upload converted `BiomeHash` into a byte with a folded hash. That was stable, but it was not honest data binding; texture slices and fog colors could drift from authored biome records.
Solution: `GPUScatterDirector` now resolves `H8BiomeRecord` from the Data Monolith `Biomes` section. This pass moved biome color to authored `LightScatterR/G/B` and `FogDensity`; R&D Upgrade 3 below supersedes the temporary heatmap byte mapping after verifying `HeatmapId` and `SurfaceId` are hashes, not dense texture slices. The director also publishes both `_CurrentBiomeColor` and `CurrentBiomeColor` to satisfy shader and post-process naming conventions without a direct post-process dependency.
Rejected Alternatives: Keeping hash-fold IDs was rejected as fake correctness. Adding a managed dictionary was rejected because the cold-path pointer scan is small and avoids heap churn. Editing the Data Monolith arena was rejected because the terrain domain can consume existing public section pointers without altering the data ownership layer.
Scalability potential: Low/MX350 gets the same 64 KB R8 texture with authored biome color data. High/Ultra can align texture-array slices to dense monolith record order without changing shader code; R&D Upgrade 3 documents the corrected slice contract.
Hardware Impact: Cold upload uses binary search over the sorted biome table per heatmap cell; expected one-time cost remains below a frame during boot/monolith reload. Hot path remains 0 GC and unchanged.

Validation: `validate_script Assets/_Project/Scripts/World/GPUScatterDirector.cs` after this R&D upgrade reports 0 errors and 0 warnings.

## R&D Upgrade 2 - Sorted Biome Lookup

Problem: The first authored-record lookup scanned the biome table linearly. The Data Monolith compiler sorts biome records by `BiomeHash`, so a linear search was unnecessary cold-path waste.
Solution: Replaced the scan in `TryResolveBiomeRecord` with binary search over the sorted `H8BiomeRecord` section.
Rejected Alternatives: Managed hash maps were rejected for allocation and lifetime complexity; changing `H8StaticDataArena` was rejected as a cross-domain data-layer edit.
Scalability potential: Low devices reduce boot/reload spikes. High/Ultra can carry more biome records without changing upload complexity from O(n*m) to O(n log m).
Hardware Impact: Estimated cold upload lookup reduction from up to 64 comparisons per cell to about 6 for a 64-biome table; hot frame impact remains 0 us.

Validation: `validate_script Assets/_Project/Scripts/World/GPUScatterDirector.cs` after binary-search conversion reports 0 errors and 0 warnings.

## R&D Upgrade 3 - Dense Texture-Array Slice IDs

Problem: `H8BiomeRecord.HeatmapId` and `SurfaceId` are authored string hashes from the Data Monolith compiler, not dense `Texture2DArray` slice IDs. Treating those hashes as R8 biome IDs collapses most values to slice 255 after clamping and makes biome visuals dishonest.
Solution: Encode `record.RecordIndex + 1` into the R8 heatmap. `TerrainMaster.shader` now converts the selected byte to `encodedBiomeId`, uses `encodedBiomeId - 1` as the `Texture2DArray` slice, and preserves `0` as the missing-biome sentinel. Missing-record fallback is bounded by the actual biome texture-array capacity when one is assigned.
Rejected Alternatives: Using `HeatmapId`/`SurfaceId` low bytes was rejected because hashes are not authored slice order. Clamping hashes was rejected because it collapses unrelated biomes onto the last texture. A managed dictionary remap was rejected because the existing sorted record table already provides dense `RecordIndex` without GC.
Scalability potential: Low/MX350 keeps the same 64 KB R8 heatmap and one terrain texture fetch. Middle/High/Ultra can expand the `Texture2DArray` content in monolith record order without shader changes. Ultra spends saved bandwidth on higher quality slices and denser micro-scatter, not four-way texture blending.
Hardware Impact: Hot frame cost remains unchanged. Cold upload uses the existing binary search and one byte write per heatmap cell; expected runtime frame delta is 0 us. Correctness gain prevents visual layer collapse on both i3/MX350 and high-end GPUs.

Validation: `validate_script Assets/_Project/Scripts/World/GPUScatterDirector.cs` after dense-slice encoding reports 0 errors and 0 warnings.

## R&D Upgrade 4 - Checksum-Backed Heatmap Invalidation

Problem: `GPUScatterDirector` only invalidated the uploaded biome heatmap when `H8StaticDataArena.ByteLength` changed. A same-size monolith rebake with different biome content would leave stale R8 heatmap bytes on the GPU.
Solution: Track both `H8StaticDataArena.ByteLength` and `H8StaticDataArena.Header.Checksum64`. The heatmap upload is skipped only when both match the previous upload.
Rejected Alternatives: Per-frame upload was rejected as wasteful and garbage-adjacent. Adding a new data-layer generation counter was rejected as a cross-domain edit; the monolith header already exposes a validated checksum.
Scalability potential: Low/MX350 still pays 0 us per frame after upload. High/Ultra can hot-reload denser biome arrays or rebaked monoliths without stale terrain/scatter IDs.
Hardware Impact: One extra 64-bit compare in cold upload gating; expected frame cost 0 us. Correctness gain prevents same-size rebake desync.

Validation: `validate_script Assets/_Project/Scripts/World/GPUScatterDirector.cs` after checksum invalidation reports 0 errors and 0 warnings.

## R&D Upgrade 5 - Scatter Black Box

Problem: `GPUScatterDirector` is a critical terrain/scatter runtime path but did not keep a fixed 300-frame black-box buffer. A NaN or invalid scatter state would have left no authoritative recent state dump.
Solution: Added `NativeArray<ScatterTelemetryEntry>[300]` with frame, flags, center, AUP offset, radius, cell size, grid resolution, candidate count, biome hash, visible count, state hash, origin shift sequence, and monolith checksum low bits. Invalid state writes `Docs/AgentLogs/Dump_WORLD_BIOME_BLENDING.bin` through a binary dump path.
Rejected Alternatives: Managed lists or JSON logs were rejected because they allocate and become failure-path noise. Per-candidate telemetry was rejected because it would turn a black box into a frame-time liability. A cross-domain telemetry service edit was rejected; local fixed ring satisfies the requirement.
Scalability potential: Low/MX350 pays one fixed 300-entry native ring and O(1) sample writes. High/Ultra get the same deterministic failure forensics while keeping micro-scatter visual overkill on the GPU.
Hardware Impact: Per-frame cost is one small struct write and a few hash mixes; expected cost is below 5 us. Dump I/O only occurs after invalid state/NaN detection.

Validation: Standard `validate_script` hit an MCP regex timeout after the file grew; `validate_script` at `basic` level reports 0 errors and 0 warnings.
