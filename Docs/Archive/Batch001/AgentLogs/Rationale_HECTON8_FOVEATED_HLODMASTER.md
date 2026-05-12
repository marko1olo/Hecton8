# HECTON8_FOVEATED_HLODMASTER Rationale

Status: PENDING VERIFICATION

## Decision 1 - Scope Control

Problem: The assignment requests Hi-Z occlusion and HLOD cluster behavior, but the project mandate states HLOD occlusion is owned by Unity 6 GPU Resident Drawer and forbids a hand-rolled HLOD append pipeline.

Solution: Implement scatter-side culling additions only, while reusing existing `HectonIndirectVegetationRenderer` Hi-Z depth pyramid pattern and existing `HectonHLODRenderer` BRG path. Keep HLOD cluster claims evidence-based.

Rejected Alternatives: A new compute HLOD append/compact pipeline would duplicate renderer ownership and create cross-domain dependency with world streaming.

Scalability potential: Low/MX350 gets foveated stale visibility, dithered cutoff, projected-size rejection, and optional depth occlusion. High/Ultra can keep the same path but lower size thresholds and preserve denser far scatter.

Hardware Impact: Estimated 55-90 us saved on MX350-class peripheral scatter culling when most candidates sit outside the center 40% screen radius.

## Decision 2 - Cinematic Dither Instead Of Honest Far Edge

Problem: Hard far-distance cutoff causes visible popping and encourages keeping more objects alive to hide the artifact.

Solution: Use deterministic hash dither in the final far band. It makes far scatter evaporate into fog without alpha blending or per-object state.

Rejected Alternatives: Alpha fade would increase transparent overdraw. CPU-managed fade state would add hot-path uploads and more cache pressure.

Scalability potential: Low uses wider dither band and stronger projected-size kill. High can narrow the dither band for denser premium silhouettes.

Hardware Impact: Estimated 15-30 us saved versus alpha fade on dense far-field scatter by avoiding a second blended rendering path.

## Decision 3 - Scatter Hi-Z Without HLOD Ownership Drift

Problem: Scatter instances were blind to prior-frame depth, but adding a new HLOD append/compact pipeline would conflict with the renderer mandate.

Solution: Add optional previous-frame depth pyramid build/bind to `GPUScatterDirector` only. The compute shader rejects projected scatter spheres against Hi-Z after cheap distance/foveated/projected-size gates.

Rejected Alternatives: Runtime coral macro mesh combining inside the scatter tick was rejected. It would mix editor baking, chunk streaming, and render submission ownership.

Scalability potential: Low/MX350 can raise `minProjectedPixelRadius` and keep the dither band wide. High/Ultra can lower projected-size rejection and keep richer far scatter while still using Hi-Z for hidden objects.

Hardware Impact: Estimated 80-220 us saved in occluded coral fields by avoiding draw/raster work; actual GPU cost requires Unity/RenderDoc verification.

## Decision 4 - Visibility Cache Safety

Problem: Foveated stale-cache reuse can preserve an old visible bit if a candidate becomes invalid before reaching the foveated mask logic.

Solution: Every early invalidation path touched in the scatter kernel writes `0u` into `_HectonScatterVisibilityCache` before returning.

Rejected Alternatives: Clearing the full cache every frame would destroy the 4-frame foveated cadence benefit and add GPU write bandwidth.

Scalability potential: Low/MX350 benefits most from stable stale-cache reuse. High/Ultra can force full refresh more often through inspector values if visual density matters.

Hardware Impact: Estimated 5-15 us saved versus full per-frame cache clear at 16k candidates, while removing stale one-frame visibility artifacts.

## Decision 5 - Low-Rate GPU Counter Readback

Problem: The scatter director reported candidate count as visible count, which is not telemetry and hides culling regressions.

Solution: Read the indirect args buffer through `AsyncGPUReadback` once every 60 frames. Use uint slot 1 as the GPU-produced instance count.

Rejected Alternatives: `GraphicsBuffer.GetData` or per-frame readback were rejected because both can stall CPU/GPU synchronization.

Scalability potential: Low/MX350 keeps the 60-frame cadence. High/Ultra can expose denser scatter while the same diagnostics reveal whether visibility counts exceed budget.

Hardware Impact: Estimated 0 us in normal render hot path except one queued async request per second at 60 FPS; avoids blocking readback stalls.
