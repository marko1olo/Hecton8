# RENDER_VFX_POST Log

Status: PENDING VERIFICATION

## Session Start

What was wrong: Prompt reports visor post-processing as a messy multi-pass Volume/grab-pass stack with fill-rate risk on MX350.
What was done: Extracted `RENDER_VFX_POST` prompt, loaded relevant mandates, created status and rationale files.
Cinematic Cheats used: Single presentation fake path planned; no physical simulation.
Exact Microseconds saved: Not measured. Estimated 300-900 us target versus separate passes; STATUS: PENDING VERIFICATION.

## Uber Pass Implementation

What was wrong: Visor damage, water distortion, heat haze, dirty lens, hypoxia, pressure warp, and blood were distributed across legacy fullscreen/transparent paths. `SuitVisor.shader` still samples `_CameraOpaqueTexture` twice. Default Volume CA/LensDistortion could double-process the same signal.

What was done:
- Added `Assets/_Project/Art/Shaders/HectonVisorUberPost.shader`.
- Added `Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs`.
- Updated `Assets/_Project/Editor/HectonRenderPipelineValidator.cs` to require the Uber feature, disable old retina/visor-fluid features, zero duplicate shaft lens/haze settings, and deactivate Volume Chromatic Aberration/Lens Distortion.
- Added `Docs/AgentLogs/RECON_RENDER_VFX_POST.md` with the `GrabPass`/`_CameraOpaqueTexture` scan results.
- Updated `Docs/Tasks/Status_RENDER_VFX_POST.md` and `Docs/AgentLogs/Rationale_RENDER_VFX_POST.md`.

Cinematic Cheats used:
- Single-sample CA fake: one `_BlitTexture` sample plus channel bias instead of RGB re-taps.
- Heat haze: direct sine UV displacement from `_Time.y` and `_LocalTemperature`; no thermal field.
- Pressure warp: cheap barrel distortion from `dot(centered, centered)`.
- Cracks: packed RG normal plus alpha reveal threshold from `HealthFraction`.
- Lens dirt: blue-noise/IGN dithered multiply; no alpha-blended overlay.
- Hypoxia: grayscale lerp from `_HypoxiaSignal` or oxygen fallback.
- Blood: `_StatusMask` bit 0 edge tint; no blood texture.
- AUP safety: no temporal buffers; `HectonFloatingOrigin.CurrentShiftSequence` bound as reset salt.

Exact Microseconds saved:
- Unified pass vs 3-5 independent fullscreen/overlay paths: estimated 300-900 us on i3/MX350.
- One scene-color sample vs 3-tap CA: estimated 120-260 us.
- Removed nested heat-haze sine polish: estimated 3-8 us in active haze branch.
- Dithered dirt vs transparent overlay: estimated 70-160 us.
- Disabling legacy retina/fluid/Volume/shaft duplicates when validator can run: estimated 250-700 us.
- STATUS: PENDING VERIFICATION. `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:normal -clp:Summary` is blocked by unrelated `HectonSurvivalSystem.cs(298,29): CS0246 SurvivalPhysiologyScalarResult`. Unity MCP validation timed out/disconnected after refresh.

## R&D Continuation: Transparent Opaque Texture Purge

What was wrong: `SuitVisor.shader` still carried the legacy transparent visor refraction path. It declared `_CameraOpaqueTexture`, sampled it once for refracted scene color, then sampled it again for a high refraction tap. That left the old MX350 fill-rate debt alive outside the new Uber pass.

What was done:
- Removed `_CameraOpaqueTexture` declaration and samples from `Assets/_Project/Art/Shaders/SuitVisor.shader`.
- Replaced scene refraction with a procedural visor scene surrogate using base color, fresnel, HUD tint, glare, radial edge, runoff, and hash dither.
- Kept chroma/hazard/glare response as color remap ALU instead of scene re-sampling.
- Moved the visor toward depth/stencil cutout behavior with `ZWrite On`, `AlphaToMask On`, dithered `clip`, and close-depth alpha fade.
- Re-ran `rg -n "CameraOpaqueTexture|_CameraOpaqueTexture|GrabPass" Assets/_Project/Art/Shaders -g "*.shader"`; no matches remain.
- Re-ran `_BlitTexture` scan on `HectonVisorUberPost.shader`; the Uber pass still has one scene sample at line 170.

Cinematic Cheats used:
- Scene refraction is now a deterministic color surrogate, not real background refraction.
- Chroma is channel remap/bias, not RGB scene re-taps.
- Visor transparency is dithered cutout/A2C coverage, not alpha-blended full-screen glass.
- Close-object occlusion is depth-faded alpha, not a physical glass intersection solve.

Exact Microseconds saved:
- Deleted two `_CameraOpaqueTexture` taps from `SuitVisor.shader`: estimated 120-300 us at 1080p on MX350, pending profiler proof.
- Reduced transparent blend pressure through cutout/stencil/depth behavior: estimated 40-150 us, pending Frame Debugger proof.
- Build status remains PENDING VERIFICATION. Latest `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:normal -clp:Summary` is blocked by unrelated `VoxelDeltaProcessor.cs(1688,92): CS0246 SaveVoxelDeltaRun8` and `HectonBoidController.cs(73,86): CS0535 IAcousticPingEventListener.OnAcousticPing(in AcousticPingEvent)`.
- Visual status remains PENDING VERIFICATION. Unity MCP `read_console` failed because the Unity session did not answer ping, so shader import, Game View screenshot, Frame Debugger, and GC/profiler numbers were not produced in this pass.

## R&D Continuation: Textureless Uber Richness

What was wrong: The Uber pass had texture slots for crack, dirt, and blue noise, but renderer auto-repair can add the feature before those assets are assigned. That creates a weak fallback path: blank cracks, white dirt, gray dither, and avoidable decorative texture sampling. The low-tier `_QUALITY_MX350` keyword also introduced shader variant risk for a material created at runtime.

What was done:
- Added `_HectonUberTextureFlags` in `HectonVisorUberPostFeature` and `HectonVisorUberPost.shader`.
- Added procedural crack veins with `rsqrt(max(dot, epsilon))` normal approximation when no packed crack texture is bound.
- Added procedural lens grime and procedural IGN/hash noise when no dirt/blue-noise texture is bound.
- Removed `_QUALITY_MX350` shader keyword and C# keyword mutation; MX350 heat haze disable now uses `_HectonUberLowTier` uniform only.
- Added canonical `COLD ALLOC` comments on the renderer feature's persistent allocations.

Cinematic Cheats used:
- Crack texture fallback is deterministic vein ALU, not texture generation or decal geometry.
- Dirt fallback is procedural grime multiply, not alpha-blended glass.
- Blue noise fallback is IGN/hash, not a texture dependency.
- Low-tier LOD is uniform amplitude zeroing, not a separate shader variant.

Exact Microseconds saved:
- Optional texture fetch avoidance when assets are unbound: estimated 8-35 us on MX350, pending shader profiler proof.
- Keyword/variant removal: frame-time neutral, but removes build stripping/variant memory risk.
- Latest useful `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:ErrorsOnly` remains blocked outside rendering with 77 errors, mostly missing cross-domain policy/native bridge symbols such as `HectonPersistentPathPolicy`, `HectonThreadPriorityPolicy`, `HectonNativeBridge`, `HardwareTierDetector`, `SteamDeckInputPal`, plus unrelated combat/save/audio errors. No visor C# error was reported before the external failure list.
- Unity MCP `read_console` later returned 5 latest errors from combat/fauna (`CaptureReceiverManagedRefs`, `PublishCombatTelemetryAnomaly`, `ProceduralCrabLegIKRuntime` access/job fields). No render-domain error was present in those latest entries, but the project is still compile-red.
- STATUS: PENDING VERIFICATION. Unity shader import and visual review still absent.

## R&D Continuation: Exact Blood Gate

What was wrong: Blood tint used shader-side float math to reconstruct bit 0 from `_HectonUberStatusMask`, and the C# path could fall back to global `_StatusMask` when exact context status was zero. That creates stale blood tint and float precision failure modes.

What was done:
- Removed `_HectonUberStatusMask` from the shader.
- Removed `_StatusMask` global fallback from `HectonVisorUberPostFeature`.
- Read exact `PlayerRuntimeContext.SurvivalState.StatusMask` / `HectonSurvivalSystem.StatusMask`.
- Converted `StatusMask & 1u` to `_HectonUberBleeding01` in C#.
- Shader now consumes `_HectonUberBleeding01` directly for edge blood tint.

Cinematic Cheats used:
- Blood remains scalar edge tint only; no decal, texture, particle, or extra pass.
- Status decoding is CPU-side scalar transport, not GPU bitfield emulation.

Exact Microseconds saved:
- Removed one material float upload and shader float modulo/floor path: estimated <1 us. Correctness and stale-state removal are the meaningful wins.
- Build retry after this C# change timed out after 124 seconds under concurrent dotnet/MSBuild load. Unity Console still reports external errors from editor tests/save Burst paths and no render-domain error in the latest entries.
- STATUS: PENDING VERIFICATION. Visual review and Frame Debugger proof remain blocked by compile state.

## R&D Continuation: Low-Tier Cache and Hidden Pass Classification

What was wrong: `HectonVisorUberPostFeature` queried `SystemInfo.graphicsMemorySize` from the render-camera setup path every time it built runtime state. The value is effectively session-static, so this was hot-path native-call noise. `HectonVisorUberPost.shader` also used Transparent tags even though it is a hidden fullscreen RenderGraph pass, which makes audits harder.

What was done:
- Added a per-feature low-tier cache keyed by `lowTierVideoMemoryMb`.
- Changed `TryBuildRuntimeState` to receive the cached low-tier result instead of resolving hardware state internally.
- Changed hidden shader tags to `RenderType=Opaque` and `Queue=Geometry`; the pass still uses RenderGraph color/depth resources and `ZTest Always`.
- Ran Unity MCP `validate_script` on `HectonVisorUberPostFeature.cs`: 0 diagnostics.
- Ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:ErrorsOnly`: still blocked outside rendering by `UserOptionsPersistence.cs` missing `HectonPersistentPathPolicy`.

Cinematic Cheats used:
- Low-tier heat haze remains a uniform amplitude zero, not a pass swap or shader keyword.
- Fullscreen pass remains a single scene sample; no new texture/scene taps were added.

Exact Microseconds saved:
- Removed per-camera hardware-memory native query from the active render setup path: estimated <1-3 us per active render camera, pending profiler proof.
- Tag correction is maintenance/audit hygiene; no measured frame-time claim.
- STATUS: PENDING VERIFICATION. Unity visual import, Frame Debugger, and profiler proof remain blocked by external compile errors.

## R&D Continuation: Shader Portability Tightening

What was wrong: `HectonVisorUberPost.shader` used scalar swizzle shorthand (`frameSalt.xx`, `shiftSalt.xx`, `luma.xxx`). This may compile on permissive HLSL backends, but it is unnecessary risk while Unity shader import cannot be trusted due external project compile errors.

What was done:
- Replaced scalar salt swizzles with explicit `float2` constructors.
- Replaced scalar luminance swizzle with explicit `half3` constructor.
- Re-ran `rg` for those risky patterns: no matches.
- Re-ran single scene-sample scan: still one `_BlitTexture` sample, now at line 233.
- Re-ran Unity MCP `validate_script` on `HectonVisorUberPostFeature.cs`: 0 diagnostics.
- Unity Console still reports external `UserOptionsPersistence.cs` / `SaveBinaryStorage.cs` errors plus an MCP regex timeout; no render-domain compile error was returned.

Cinematic Cheats used:
- No new effect. This is source hardening for the existing dither/hypoxia fake.

Exact Microseconds saved:
- 0 us claimed. This is shader compile resilience, not runtime optimization.
- STATUS: PENDING VERIFICATION. Unity shader import/Frame Debugger proof is still absent.
