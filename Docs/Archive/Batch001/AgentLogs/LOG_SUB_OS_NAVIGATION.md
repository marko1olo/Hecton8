# SUB_OS_NAVIGATION Log

## 2026-05-11 - Submarine OS, Navigation & Sonar pass

Status: PENDING VERIFICATION

What was wrong:
- Existing acoustic radar voxel shader was transparent/additive, so it could stack fill-rate behind cockpit glass.
- Submarine OS sonar refresh was a single cadence and did not expose tiered interpolation state.
- VWS warning flag dispatch scanned a fixed warning list instead of processing active bits.
- Internal atmosphere display lacked CO2 in the fixed payload/display path.
- Auto-level had fixed-step station keeping support but no explicit Awaitable control-release entry point.

What was done:
- Added submarine stencil shader contract:
  - `Assets/_Project/Art/Shaders/Hecton_SubmarineCockpitGlassStencil.shader`
  - `Assets/_Project/Art/Shaders/Hecton_SubmarineMonitorOpaqueStencil.shader`
  - `Assets/_Project/Art/Shaders/Hecton_SubmarineSonarHoloMapStencil.shader`
- Converted `Hecton_AcousticRadarVoxel.shader` to cutout/stencil/opaque blend behavior.
- Added `Assets/_Project/Scripts/UI/SubmarineSonarHoloMapRenderer.cs`, sampling voxel/hybrid navigation data and drawing via `Graphics.DrawMesh`.
- Added sonar LOD globals and tiered refresh intervals in `HectonSubmarineOS`.
- Added `_SubInteriorLightingState` global write.
- Changed VWS dispatch to `math.tzcnt` bit walking.
- Added CO2 to submarine OS snapshot/payload and displayed it through fixed char buffers and `SetCharArray`.
- Added Awaitable auto-level entry and no-alloc arming method to `SubmarineStationKeepingController`.
- Generated `.meta` files for all new submarine assets.

Cinematic cheats used:
- Stencil rejection instead of rendering hidden holograms through layered transparent glass.
- Cutout/opaque monitor backgrounds instead of alpha-blended glass panels.
- Voxel/hybrid map sampling instead of raycasts.
- Low-tier 10 Hz retro sonar tick instead of smooth interpolation.
- Global shader vectors instead of per-material updates.

Exact microseconds saved:
- Not exact. Compile/static checks cannot measure frame time.
- Budget estimates recorded in `Docs/Tasks/Status_SUB_OS_NAVIGATION.md`: stencil 35-120 us GPU, low-tier sonar LOD 40-160 us CPU, offscreen culling 20-90 us CPU per hidden monitor, voxel sampling 60-250 us CPU against raycasts, VWS bit processing 2-8 us CPU.
- Required proof still missing: Unity Profiler allocation capture and Frame Debugger/RenderDoc overdraw/stencil validation.

Hard blockers:
- VWS is not a pure `NativeQueue<AudioEvent>` clip path. Existing warning data is authored as `AudioClip`; no stable clip-id mapping contract was available. Current path preserves existing `SpatialAudioManager`/caption behavior and removes the flag-scan waste.
- Engine heat cannot honestly read live thruster usage because only max thrust and Rigidbody velocity are exposed. Current heat remains a speed/acceleration proxy.
- Power grid heatmap cannot read per-module Jacobi drain from the aggregate telemetry snapshot currently exposed.
- Quest landmark distance was not implemented because no active quest landmark AUP contract was found in the owned domain.
- Blip occlusion fade from `EcosystemDirector` distance data was not wired because no stable distance-data contract was exposed for this UI slice.

Verification:
- Final compile used sequential restore/build and passed with 0 errors:
  - `dotnet restore Hecton8.Core.csproj -nr:false -v:minimal`
  - `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`
- Scoped static scan passed for touched files: no `Canvas.ForceUpdateCanvases`, `string.Format`, interpolated strings, `Mathf.Tan`, `RenderTexture`, `Debug.Log`, `.ToString(`, or Cyrillic matches.
- Runtime status remains PENDING VERIFICATION.

## 2026-05-11 - Final anti-bloat update

Status: PENDING VERIFICATION

What was wrong:
- The touched submarine OS file still had development-only `Debug.LogWarning` overflow branches in brownout cache binding.

What was done:
- Removed those log calls and the guard fields. Overflow still returns safely when the fixed cache is full.

Cinematic cheats used:
- Fixed cache truncation instead of runtime logging or dynamic resize.

Exact microseconds saved:
- No normal-frame claim. This removes a rare managed logging path under authored overflow conditions.

Verification:
- Sequential restore/build passed with 0 errors.
- Scoped static scan found no `Debug.Log` in touched files.
