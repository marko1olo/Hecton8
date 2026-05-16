# LOG_SCREEN_SPACE_REFRACTION

## 2026-05-16 - Session Start

What was wrong: Porthole and mask glass refraction task had no agent status/rationale/log files on disk for this batch.
What was done: Created task status, rationale, and log files for SCREEN_SPACE_REFRACTION.
Cinematic Cheats used: Screen-space fake selected as primary route; physical glass/water raytracing rejected before implementation.
Exact Microseconds saved: Pending measurement; no runtime code changed yet.

## 2026-05-16 - Screen-Space Snell Refraction Core

What was wrong: Visor glass relied on fabricated scene color or the old fullscreen blit path. It did not have a shared low-cost Snell approximation, an IOR LUT, explicit `_CameraOpaqueTexture` refraction, depth rejection, or dirt-gated intensity. Full verification was impossible because the shared project currently fails in unrelated core/fauna/AI/animation/VFX files.

What was done: Added `Assets/_Project/Art/Shaders/Post/Hecton_SnellRefractionCore.hlsl`; updated `SuitVisor.shader` to sample `_CameraOpaqueTexture`, use depth-buffer foreground rejection, clamp UV offsets, apply inverse dirt masks, and fall back to chromatic-only low-tier refraction; updated `Hecton_VisorFluidDistortion.shader` with opaque/depth sampling, droplet-mask Snell offsets, and chromatic fallback; migrated `HectonVisorFluidDistortionFeature.cs` from `AddBlitPass` to `AddRasterRenderPass`, binding `_BlitTexture`, `_CameraDepthTexture`, and `_CameraOpaqueTexture` explicitly.

Cinematic Cheats used: No raytracing, no physical glass simulation, no per-object GrabPass. Low-tier path uses chromatic split only. High path uses bounded normal/droplet UV offsets from `_CameraOpaqueTexture`. Stress/homeostasis degrades to the fake path instead of spending samples during visual chaos.

Exact Microseconds saved: 0 us measured and certified. Profiling is blocked by unrelated compile failures. Static expected saving is removal of per-object grab-style capture and old blit utility dependency; exact numbers require a clean Unity/profiler run.

Build/validation: `dotnet restore Hecton8.Core.csproj` succeeded. `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` failed outside this task: missing `Hecton8.Core.Signals`, missing `Hecton8.AI.Perception`, missing animation fauna IK types, missing `IResolutionScalerService`, and `HectonMarineSnowRenderer` interface drift. Static checks found no `AddBlitPass`, `RenderGraphUtils`, `GrabPass`, `new NativeArray`, `GC.Alloc`, singleton `.Instance`, or Unity object search calls in touched files.
