# LOG_SHINOBU_277

## 2026-05-21T16:52+04:00 Static Implementation Report

What was wrong:
- Crest 5 has shoreline foam math, but active HECTON route must not restore Crest foam/depth cameras or CPU particle/decal emitters.
- Existing single-pass ocean RenderGraph depth mask already owned the correct place to compare depth-buffer reconstruction against water height, but had no SHINOBU_277 `_GlobalShorelineFoam` graft ABI.
- Prompt contained copied visor/decal wording and a direct contradiction: hard constraint requires `ShorelineFoamParamsDTO` size 32; self-audit asks for size 80. The 32-byte hard constraint was used.

What was done:
- Added `ShorelineFoamParamsDTO` as `[StructLayout(LayoutKind.Explicit, Size = 32)]`, offset 0 `FoamIntensityAndFalloff`, offset 16 `QualityAndLimits`, no properties.
- Added Vault lanes `71940..71946` for params, runtime state, 300-entry telemetry, cursor, profiles, CSV scratch, and self-audit reserve.
- Added Burst jobs: `ProcessFoamParametersJob`, `GenerateMockShorelineFoamDataJob`, `DecayShorelineFoamOpacityJob`, and `CopyShorelineFoamParamsToMappedBufferJob`.
- Wired `OceanSinglePassRuntime.VisualSyncTick` to publish localized water height and double-buffered `GraphicsBuffer` upload through `ShorelineFoamGraftRuntime`.
- Wired `HectonSinglePassOceanFeature` to import/bind `_GlobalShorelineFoam`, `_GlobalShorelineFoamCount`, and `_GlobalShorelineFoamRuntime` in the existing RenderGraph depth pass.
- Updated `Hidden/Hecton8/OceanDepthFoam` to blend shoreline foam rows into the depth-vs-water-height calculation using camera-local water height.
- Updated storm ocean fragment shading to use screen foam for continuous reflection normal perturbation.
- Added editor layout validation, tuner window, CSV profile parser, gizmo component, scanner menu, editor tests, architecture route card, and shared report object.

Cinematic cheats used:
- Screen-space primary-camera depth reconstruction instead of a shoreline/depth camera.
- Ring-buffered 32-byte scalar foam rows instead of decals, particles, or simulation meshes.
- Screen-foam derivative normal perturbation instead of a new normal buffer pass.
- Continuous `GlobalQualityWeight` controls active rows, shader loop limit, decay, intensity, falloff, and perturbation.

Exact microseconds saved or estimated:
- Auxiliary Crest/orthographic camera path avoided: estimated 650-1800 us on i3/MX350 depending on terrain fill and depth resolution.
- CPU particle/decal GameObject route avoided: estimated 200-900 us submission/update cost.
- `GraphicsBuffer.SetData()` sync avoided via `LockBufferForWrite`: estimated 20-80 us at 2048-byte upload.
- Ring insertion: O(1), estimated under 1 us.
- Decay job at 64 rows: estimated under 8 us.
- `ProcessFoamParametersJob` at 64 rows: estimated under 15 us.
- Telemetry write: one 64-byte row, estimated under 1 us.

Verification:
- Focused static scan found no active `Camera.Render`, `DecalProjector`, `ParticleSystem`, `AddComponent<Camera>`, active Crest foam sim, or active Crest depth cache route in SHINOBU_277 scope.
- `git diff --check` passed for touched scope except an existing LF/CRLF warning on `Hecton_StormOceanSurface.shader`.
- `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` validated with PowerShell `ConvertFrom-Json`.
- `dotnet build` was not launched: CPU sampled at 99-100%, and `csc`/`dotnet` were already running. Compile, Unity import, shader import, RenderGraph Viewer, and profiler proof remain pending.
