# Scatter Runtime Docs

Date: 2026-05-07
Status: PENDING VERIFICATION

Purpose: canonical active bundle for scatter runtime refactor planning, baseline, and DOTS scope.

Current-state boundary:

- Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using this bundle as current project truth.
- Scatter runtime remains owned by `WorldProceduralScatterDirector` and adjacent scatter backend seams.
- DOTS/Entities scatter work is currently a disabled placeholder seam unless package, define, profiler parity, and runtime validation prove otherwise.
- Current source/package check: `com.unity.entities` is not declared in `Packages/manifest.json`.
- `Assets/_Project/Scripts/World/Dots/Hecton8.World.Dots.asmdef` exists, but is gated by defines and is not auto-referenced.
- No current first-party source path under `Assets/_Project/Scripts` uses `Unity.Entities`, `IComponentData`, `SystemBase`, or `ISystem`.
- This bundle is planning and architecture guidance, not proof of live scatter correctness or performance.

## Files

- `SCATTER_REFACTOR_EXECUTION_PLAN.md` - active scatter cleanup plan.
- `SCATTER_REFACTORING_MANIFESTO_V2.md` - stronger architectural manifesto for the director refactor.
- `SCATTER_PHASE1_BASELINE_CHECKLIST.md` - baseline validation checklist.
- `SCATTER_DOTS_NARROW_SCOPE_SPEC.md` - narrow DOTS scope.
- `ECS_DOTS_ADOPTION_PLAN.md` - broader DOTS adoption planning.

## Rule

Use this bundle as the active scatter planning zone instead of loose root-level `Docs/*.md` scatter files.

## 2026-05-12 DOC_VULCAN Technical Requirements

Status: SOURCE-SCANNED, RUNTIME PENDING VERIFICATION.

[SOURCE] The active scatter rendering path starts in `Assets/_Project/Art/Shaders/Hecton_GpuScatter.compute`. The file declares `HECTON_SCATTER_THREADS 64`, generates candidates in `GenerateScatterInstances`, rejects invalid candidates before instance write, and compacts visible instances through `CompactVisibleScatterInstances`. The CPU must treat this path as the authority for large-area scatter. GameObject scatter is legacy planning language unless a source file proves otherwise.

[REQ] Runtime scatter must run as compute-driven candidate generation, visibility culling, and indirect or batch rendering consumption. The CPU may upload biome, bounds, density, and chunk constants. The CPU must not iterate visible flora or prop instances as individual transforms.

[REQ] Hi-Z culling must sample `_HectonScatterDepthPyramid` through `IsOccludedByScatterDepthPyramid`. The cull path must compare projected bounds against the pyramid with an explicit occlusion bias. A scatter change that bypasses the pyramid must include a profiler note and a rollback path.

[REQ] Foveated updates must preserve the shader-side `ResolveFoveatedUpdateMask` contract: update near or gaze-critical candidates every eligible frame, push peripheral candidates to cadence buckets, and keep `_HectonScatterFrameQuadrant` available for quarter-field refresh. The system must prefer temporal coherence over perfectly fresh distant scatter.

[REQ] CPU uploads must use the `GraphicsBufferUploadUtility` lock-buffer path where the data shape fits it. Source evidence: `SystemDispatcher.cs` creates structured buffers with `GraphicsBuffer.UsageFlags.LockBufferForWrite` and uploads through `GraphicsBuffer.LockBufferForWrite`. Standard `SetData` is allowed only for rare setup data or unsupported buffer shapes.

[REQ] Scatter density must remain data-oriented. `_HectonScatterDensityBins`, `_HectonScatterChunkBounds`, biome heat maps, height fields, and cave SDF data must gate candidates before any visible instance append. Do not allocate managed lists during scatter generation.

[REQ] BRG, GPUI, or indirect draw consumers must read compacted GPU buffers. If BRG flickers, verify buffer lifetime, append counter reset, frame index parity, and bounds inflation before changing scatter math.

### Shader Math LOD Contract

[SOURCE] `Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl` defines `_MATH_LOD_LOW`, `_MATH_LOD_HIGH`, `_HectonMathLodMode`, and `_HectonMathLodDistanceSq`.

[REQ] `_MATH_LOD_LOW` must use the cheap math path in `HectonCoreLitSafeNormalize` and the squared color path in depth crush. It must avoid expensive normalization and `pow` where the source already provides the approximation.

[REQ] Documentation must not claim that `_MATH_LOD_LOW` strips point lights. Current source keeps the CoreLit glow-point path capped by `HECTON_GLOW_POINT_MAX` and runtime counts. Low-end savings come from cheaper normalize/depth math and variant policy, not from a proven point-light removal in the current file.

[REQ] `_MATH_LOD_HIGH` may buy visual overkill with more accurate normalization and fog/light response. Low, Middle, High, and Ultra settings must select math cost by distance and device class, not by a single balanced default.

### Flow Field And Plankton Contract

[SOURCE] `Assets/_Project/Art/Shaders/AbyssalFlowField.compute` runs 64-thread kernels, accumulates weather current, storm pressure waves, heat-source vortices, thermocline attenuation, and submarine-wash events into a `float4(flow, energy)` field.

[REQ] Plankton, detritus, and seaweed must sample vector-field advection. They must not run individual Rigidbody current simulation. Noise must appear as authored abyssal motion, not as per-object physics.

[REQ] Low devices must sample coarser flow and reuse temporal frames. High and Ultra devices may add extra visible particles, stronger bloom trails, and denser seaweed response while preserving the same flow-field authority.

### Compute Thread Alignment

[REQ] Scatter, flow, flora culling, and boid kernels must default to 64 threads per group unless profiling proves a target-specific alternative. Source evidence: `Hecton_GpuScatter.compute` uses `HECTON_SCATTER_THREADS 64`, `AbyssalFlowField.compute` and `FloraCulling.compute` use `HECTON_THREADS_PER_GROUP 64`, and `BoidSimulation.compute` uses `THREAD_GROUP_SIZE 64`.

[REQ] The 64-thread baseline must remain the portable MX350/mobile floor. It avoids universal 256-thread assumptions, keeps groups small enough for low-end occupancy pressure, and still lets high-end devices scale by dispatch count, visible density, and visual overkill rather than by changing correctness-critical group shape.

[REQ] Any compute documentation must name the current source constant or macro. Do not copy thread counts from comments without checking the compiled file.

### Dithered Biome Blend And Micro-Scatter

[SOURCE] `GPUScatterDirector.cs`, `TerrainMaster.shader`, `Hecton_GpuScatter.compute`, and `Hecton_ScatterIndirectLit.shader` define the current biome/scatter presentation path.

[REQ] Biome terrain blending must use the R8 heatmap and `Texture2DArray` path. `GPUScatterDirector.ResolveBiomeHeatmapByte` must encode `H8BiomeRecord.RecordIndex + 1`; byte `0` remains the missing-biome sentinel. Do not encode raw biome hashes as texture-array slices.

[REQ] `TerrainMaster.shader` must sample the four nearest biome IDs, use Interleaved Gradient Noise to choose one biome per pixel, and sample exactly one `_HectonBiomeGroundArray` slice. Do not restore four-way alpha splat blending. TAA/noir grain sells the gradient.

[REQ] Micro-scatter must read the biome heatmap in `Hecton_GpuScatter.compute`, hash cells with `_HectonScatterAupGridOffset`, and render through `Graphics.RenderMeshIndirect`. Low, Mid, and High/Ultra cull radii must remain explicit tier values: 15 m, 22 m, and 30 m unless a source/profiler update changes them.

[REQ] `_CurrentBiomeColor` and `CurrentBiomeColor` must come from source-backed biome records where available. Synthetic hash colors are fallback only.

[REQ] Scatter telemetry must keep the fixed 300-entry `ScatterTelemetryEntry` black-box ring. Invalid scatter state must dump `Docs/AgentLogs/Dump_WORLD_BIOME_BLENDING.bin`.

### Troubleshooting

[FAIL] BRG or indirect scatter flickers: reset append counters before generation, verify compacted visible index buffer lifetime, check per-frame quadrant mask, inflate bounds, then inspect Hi-Z bias.

[FAIL] Scatter disappears near cave seams: validate `_HectonCaveVoxelSdf`, chunk bounds LUT, biome heatmap UVs, and camera-relative projection before touching density rules.

[FAIL] Biome seams show hard bands or wrong textures: verify R8 heatmap bytes are `RecordIndex + 1`, verify `_HectonBiomeGroundArray.depth`, then inspect IGN selection. Do not patch this with extra splat samples.

[FAIL] Scatter pops after AUP shift: verify `_HectonScatterAupGridOffset`, origin shift sequence, foveated visibility cache reset, and black-box telemetry before rebuilding buffers.

[FAIL] GPU stalls after scatter upload: check for a fallback to `SetData`, oversized per-frame upload, or missing `LockBufferForWrite` usage on structured buffers.

[FAIL] Low-end GPU exceeds frame budget: lower foveated update radius, increase peripheral cadence, reduce visible append budget, and preserve the cinematic silhouette with shader color/alpha cheats instead of more mesh instances.
