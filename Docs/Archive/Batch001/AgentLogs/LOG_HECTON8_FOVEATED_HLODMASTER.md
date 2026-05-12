# HECTON8_FOVEATED_HLODMASTER Log

## 2026-05-11 - Scatter Foveated Culling / Hi-Z Pass

Status: PENDING VERIFICATION

Mandates followed:
- `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`
- `REND_Foveated_Simulation_LOD.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `REND_Instanced_Flora_Physics.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

What was wrong:
- `GPUScatterDirector` had no driver binding for the foveated visibility cache that the scatter compute now expects.
- Scatter had no previous-frame depth pyramid binding, so corals/rocks could remain visible behind occluders.
- Diagnostics reported candidate count as visible count.
- Frustum plane upload did not prioritize near/far plane rejection.
- Runtime HLOD macro combining would violate the current render mandate because HLOD occlusion is owned by Unity 6 GPU Resident Drawer.

What was done:
- Added scatter visibility cache `GraphicsBuffer` and bound it to `_HectonScatterVisibilityCache`.
- Added optional previous-frame Hi-Z depth pyramid generation/binding in `GPUScatterDirector`.
- Added foveated screen-center update mask constants, force-refresh on camera/grid movement, and 4-frame peripheral cadence.
- Added deterministic dithered far-distance rejection.
- Added projected-pixel-radius rejection before frustum, Hi-Z, cave SDF, and terrain normal sampling.
- Reordered frustum planes near/far first and converted the shader test to a packed reject mask.
- Added 60-frame `AsyncGPUReadback` of indirect args slot 1 for real GPU visible-count diagnostics.
- Preserved `Graphics.RenderMeshIndirect`; no `DrawMeshInstanced*`; no runtime `Instantiate`.

Cinematic cheats used:
- Peripheral stale visibility cache instead of exact every-frame culling.
- Hash-dithered far fade instead of honest alpha transition.
- Projected-pixel kill instead of drawing imperceptible scatter.
- Sphere-vs-Hi-Z proxy instead of per-triangle occlusion truth.
- Frustum padding and forced cache refresh instead of exact TAA jitter compensation math.

Estimated microseconds saved:
- Foveated 4-frame peripheral mask: 55-90 us GPU ALU on dense peripheral fields.
- Dithered far fade instead of alpha fade pass: 15-30 us.
- Hi-Z hidden scatter rejection: 80-220 us fragment/raster work in occluded scenes; compute cost pending profiler.
- Projected-pixel kill before terrain-normal sampling: 25-75 us in distant dense fields.
- Cache invalidation instead of full per-frame cache clear: 5-15 us.
- Async counter readback instead of blocking readback: avoids driver stall; normal hot-path estimate 0 us except queued request cadence.

Final git diff summary:
- `Assets/_Project/Art/Shaders/Hecton_GpuScatter.compute`: +167/-? approximate, foveated mask, Hi-Z test, dither cull, frustum reject mask.
- `Assets/_Project/Scripts/World/GPUScatterDirector.cs`: +384/-? approximate, cache/depth resources, compute bindings, counter readback, editor asset auto-assign.
- `Docs/Tasks/Status_HECTON8_FOVEATED_HLODMASTER.md`: progress/checklist.
- `Docs/AgentLogs/Rationale_HECTON8_FOVEATED_HLODMASTER.md`: decision journal.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore ...`: succeeded, 0 warnings, 0 errors.
- Static scan: no `length(` / `distance(` / `normalize(` / `sqrt(` in touched scatter compute targets.
- Static scan: touched scatter path uses `Graphics.RenderMeshIndirect`; no `DrawMeshInstanced*` or runtime `Instantiate`.
- `git diff --check`: no whitespace errors; only LF/CRLF normalization warnings on touched files.

Pending:
- Unity Editor import and shader compile.
- Unity Console after import.
- Play Mode scene validation.
- GPU profiler/RenderDoc numbers on MX350.
- Visual validation for dither edge, foveated cache staleness, and Hi-Z false positives.

