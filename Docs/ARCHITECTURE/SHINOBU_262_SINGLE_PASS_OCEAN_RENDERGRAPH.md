# SHINOBU_262 Single-Pass Ocean RenderGraph Route

Owner: `SHINOBU_262 / CREST_CAMERA_GUILLOTINE_EXECUTIONER`

Evidence class: STATIC_SOURCE until Unity import, Console compile, Frame Debugger, RenderGraph viewer, and profiler capture are attached.

## Route

- Producer phase: `SystemDispatcher.VisualSync` through `OceanSinglePassRuntime`.
- Render consumer: `HectonSinglePassOceanFeature`, one `ScriptableRenderPass` that records the depth-mask raster pass and wake compute pass into URP RenderGraph.
- Renderer installation: `SinglePassOceanRendererFeatureInstaller` adds exactly one `HectonSinglePassOceanFeature` to PC, PC High, Mobile, and Quest renderer assets after Unity import and validates the route before builds.
- Data owner: `GlobalDataVault`, `SystemID.HabitatAtmosphere`.
- Shader route: `HectonOceanVisualOverrides` constant buffer, `_H8OceanDepthFoamMask`, and `_H8OceanWakeDisplacement`.
- Crest route: realtime `OceanDepthCache` and `OceanPlanarReflection` cameras are disabled at source and prefab level. Crest no longer owns ocean depth, foam, wake, or planar reflection truth.
- Assembly route: runtime code is isolated in `Hecton8.Rendering.OceanSinglePass.asmdef`. It references Core/Core.Contracts/Core.Memory and Unity/URP packages only; it does not reference sibling runtime domains.
- Asset identity: SHINOBU_262 Unity assets include committed `.meta` files for stable GUIDs across imports.

## DataVault Buffers

- `71895` `OceanVisualOverridesDTO[1]`, 32 bytes, explicit CBuffer layout.
- `71896` `OceanGuillotineTuningDTO[1]`, 64 bytes.
- `71897` `OceanRenderTelemetryEntry[300]`, 64-byte black-box ring.
- `71898` telemetry cursor.
- `71899` `OceanAestheticProfileDTO[64]`.
- `71900` cold CSV scratch bytes.
- `71901` `OceanMockRenderStateDTO[1]`.
- `71902` reserved self-audit row.

## Dear Lie

- Depth is derived from the primary camera depth texture in screen space instead of rendering terrain from a top-down ocean camera.
- Foam uses Gerstner Jacobian/shoreline scalars and wake texture sampling in the ocean shader.
- Vehicle wakes are accumulated from `PropwashEventDTO` data into one RenderGraph-owned compute texture, not by rendering particles or geometry through a hidden camera.

## Scalability

`GlobalQualityWeight` is continuous.

It resolves wake texture size `256..1024` in 16-pixel quanta, scales foam intensity, and scales wake strength. It does not change DTO layout, truth ownership, save identity, or rollback authority.

## Rollback Boundary

All SHINOBU_262 buffers are presentation-only. They are excluded from `StateRingBuffer`, Merkle hashing, save identity, and lockstep rollback. Physics can resimulate; ocean presentation continues through its own VisualSync route and telemetry proof.

## Verification Status

Static source proofs exist for layout guard, editor tuner, camera scanner, CSV parser, edit tests, asmdef isolation, renderer-feature installer/build guard, and `.meta` identity.

Compile/runtime/profiler proof remains pending Unity project regeneration/import and guarded build.
