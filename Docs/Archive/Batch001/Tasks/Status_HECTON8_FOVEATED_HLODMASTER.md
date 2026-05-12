# HECTON8_FOVEATED_HLODMASTER Status

Assignment: Foveated Rendering & HLODMaster scatter/visibility pass.
Domain: BRG Scatter Director, GPU scatter culling, existing HLOD/vegetation renderer integration.
Status: PENDING VERIFICATION

## Loop 1 - Tasks 1-5

- [x] Task 1 Foveated update mask | DOD: screen-space squared radius and `& 3` cadence in compute. Rejected CPU-side per-instance scans. Estimate: 55-90 us GPU ALU avoided on peripheral scatter fields.
- [x] Task 2 Dithered radius culling | DOD: deterministic hash dither at far edge, no hard cutoff. Rejected alpha blend fade because overdraw violates MX350 mandate. Estimate: 15-30 us saved by avoiding extra transparent pass.
- [x] Task 3 Hi-Z occlusion culling | DOD: scatter driver builds and binds previous-frame depth pyramid to compute; shader rejects occluded projected spheres. Rejected separate HLOD append pipeline because mandate assigns HLOD occlusion to Unity 6 GPU Resident Drawer. Estimate: 80-220 us fragment work avoided in rock/coral occlusion scenes; compute cost still requires Unity profiling.
- [BLOCKED BY ARCHITECTURE] Task 4 Cluster merging HLOD | Existing `HectonHLODRenderer` BRG path and vegetation HLOD registry own far-field HLOD. Runtime coral mesh combining was rejected because it would create an editor/runtime ownership mix without an existing coral chunk provider.
- [BLOCKED BY ARCHITECTURE] Task 5 Quad-tree spatial hash | Current scatter domain owns a dense player-centered grid, not streaming chunk selection. A quad-tree requires a world chunk provider contract; inventing one here would violate cross-domain decoupling.

## Loop 2 - Tasks 6-10

- [x] Task 6 GPU resident drawer sync | DOD: instance payload stays in persistent `GraphicsBuffer`; per-frame CPU upload is scalar uniform state plus depth pyramid binding. Rejected per-instance CPU matrix uploads.
- [x] Task 7 BRG refactor confirmation | DOD: touched scatter path renders through `Graphics.RenderMeshIndirect`; static scan found no `DrawMeshInstanced*` or runtime `Instantiate`.
- [ ] Task 8 Sargassum density feedback
- [ ] Task 9 Wind sway ALU
- [ ] Task 10 VRAM defrag

## Loop 3 - Tasks 11-15

- [ ] Task 11 Shadow-only culling
- [ ] Task 12 Staggered frustum update
- [ ] Task 13 Precomputed bounds LUT
- [ ] Task 14 Compute LOD selection
- [x] Task 15 Depth-derivative edge rejection | DOD: projected pixel radius is computed before frustum/Hi-Z/terrain-normal sampling; sub-pixel scatter is killed before expensive terrain normal reads. Estimate: 25-75 us saved in distant dense fields.

## Loop 4 - Tasks 16-20

- [x] Task 16 Indirect draw arguments cache | DOD: indirect args buffer is reused and rewritten only when mesh identity changes; no per-frame managed args array.
- [ ] Task 17 Native memory barrier
- [x] Task 18 Frustum plane bitmask | DOD: frustum planes are uploaded near/far first and shader returns a packed reject mask with immediate near/far exit.
- [x] Task 19 GPU counter readback | DOD: `AsyncGPUReadback` samples the indirect args buffer every 60 frames and updates diagnostics from GPU-produced instance count. Rejected per-frame readback because it stalls the render pipeline.
- [ ] Task 20 Minimal VRAM allocation

## Loop 5 - Tasks 21-25

- [ ] Task 21 Zero-GC renderer cleanup
- [x] Task 22 Frustum padding | DOD: CPU uploads configurable two-meter default padding into the sphere/plane reject path.
- [ ] Task 23 No-branch culling
- [ ] Task 24 Texture-based density map
- [x] Task 25 Frustum jitter compensation | DOD: frustum padding and force-refresh on camera rotation/movement reduce TAA/frustum-edge stale cache flicker. Unity visual verification still pending.

## Verification

- [x] `dotnet build` green after patch
- [x] Static scan: no `length(` / `distance(` / `normalize(` in scatter compute hot path
- [x] Static scan: no `DrawMeshInstanced*` or runtime `Object.Instantiate` in touched scatter path
- [ ] Unity Console / Play Mode / profiler evidence
