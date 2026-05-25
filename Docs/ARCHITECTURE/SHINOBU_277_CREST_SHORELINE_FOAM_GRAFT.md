# SHINOBU_277 Crest Shoreline Foam Graft

Owner: `SHINOBU_277 / CREST_SHORELINE_FOAM_GRAFTER`

Evidence class: STATIC_SOURCE only. Unity import, shader import, RenderGraph Viewer, profiler, and GPU timestamp proof are still required.

## Route

- Producer phase: `OceanSinglePassRuntime.VisualSyncTick`, using its cold-cached `IDataVault`.
- Data bridge: `ShorelineFoamGraftRuntime` owns Vault rows `71940..71946`.
- Render consumer: `HectonSinglePassOceanFeature` imports the active `GraphicsBuffer` into the existing depth-mask RenderGraph pass.
- Shader route: `_GlobalShorelineFoam`, `_GlobalShorelineFoamCount`, `_GlobalShorelineFoamRuntime`, and `Hidden/Hecton8/OceanDepthFoam`.
- Crest boundary: `Assets/Crest/Crest/**` remains vendor-owned and unchanged. Crest foam/depth cameras stay disabled on the active ocean prefab.

## DTO Contract

- `ShorelineFoamParamsDTO` is exactly 32 bytes.
- Offset 0: `float4 FoamIntensityAndFalloff`.
- Offset 16: `float4 QualityAndLimits`.
- No C# properties.
- GPU upload uses double-buffered `GraphicsBuffer.LockBufferForWrite`; no `SetData()` lane exists.

## Dear Lie

Shoreline foam is a screen-space visual fake.

Shader route:

- reconstruct scene position from primary camera depth;
- convert localized water surface Y through camera-local origin lane;
- compare depth height against water height.

No CPU particles, `DecalProjector`, auxiliary camera, or `Camera.Render`.

## Scalability

`GlobalQualityWeight` controls active ring count, shader loop limit, decay rate, intensity, falloff, and normal perturbation.

It does not change DTO layout, save identity, rollback authority, or buffer ownership.

## Rollback Boundary

Buffers `71940..71946` are presentation-only. They are not part of `StateRingBuffer`, Merkle hashing, save identity, or lockstep authority.

On rollback, gameplay truth resimulates; shoreline foam continues as fading visual layer.

## Proof Gaps

CPU guard blocked `dotnet build`: CPU sampled at 100%.

Required later proof: Unity Console compile, shader import, RenderGraph Viewer `_GlobalShorelineFoam`, profiler 0 B/frame, dump test `Docs/AgentLogs/Dump_SHINOBU_277.bin`.
