<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# HECTON-8 Console And VR Readiness Audit

Date: 2026-05-07
Status: PENDING VERIFICATION
Scope: source/project-settings audit plus narrow runtime code cleanup; no Play Mode, profiler, platform build, TRC/XR certification, or GC capture was executed.

## Mandates Followed

- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_Foveated_Simulation_LOD.txt`
- `.agents-skills/CTRL_Device_Abstraction_Haptics.txt`
- `.agents-skills/PROJECT_LTS_Compatibility_Layer.txt`

## Verdict

Console future: technically plausible, not ready.

VR future: not architecturally ready.

The project already has several console-positive foundations: URP 17.4, Addressables, Input System, bounded haptic queue, dynamic resolution/VRAM managers, foveated simulation throttling, RT lifecycle tracking, and first-party zero-GC scanners. The blocking issue is not absence of systems. The blocking issue is that platform authority is still split across PC-oriented settings, presentation-heavy HUD/camera paths, large Unity-coupled runtime assemblies, and unverified runtime budgets.

## Evidence Snapshot

- `Packages/manifest.json` includes `com.unity.inputsystem`, `com.unity.addressables`, and `com.unity.render-pipelines.universal`.
- `Packages/manifest.json` does not include OpenXR/XR Management packages; only Unity built-in `com.unity.modules.vr` and `com.unity.modules.xr` modules are present.
- `ProjectSettings/XRSettings.asset` contains only `"VR Device Disabled"` and `"VR Device User Alert"`. No project-owned XR loader/runtime target is configured in this source snapshot.
- `Assets/_Project/Prefabs/Player.prefab` has cameras with `m_AllowXRRendering: 0` at lines `262`, `3319`, and `3449`; one camera has `m_AllowXRRendering: 1` at line `2377`.
- `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab:2391` is `m_RenderMode: 0` (`ScreenSpaceOverlay`).
- `Assets/_Project/Scenes/01_MAIN_MENU.unity:19540` has `m_XRTrackingOrigin: {fileID: 0}` on the Input System UI module.
- Runtime search found first-party `RenderMode.ScreenSpaceOverlay` writers in `GameBootstrapper`, `PauseMenuController`, `SuitHUDV4CanvasOverlay`, `SuitHUDScreenCompositor`, `SubnauticaSystemsDebugUI`, and authoring helpers.
- Source inventory in this pass: `1057` first-party `.cs` files under `Assets/_Project/Scripts`; static Unity loop text hits are low and mostly dispatcher/editor/comment contexts, but this is text evidence only.

## Immediate Fixes Applied

1. `Assets/_Project/Scripts/UI/SettingsManager.cs`

   Shadow distance now applies through the active `UniversalRenderPipelineAsset` instead of `QualitySettings.shadowDistance`. This aligns the settings path with Unity 6 URP ownership.

2. `Assets/_Project/Scripts/Core/HectonUrpShadowBudgetGuard.cs`

   Removed the duplicate `QualitySettings.shadowDistance` write from the runtime shadow budget guard. The guard still enforces the active URP asset.

3. `Assets/_Project/Scripts/Core/InputDispatcher.cs`

   Removed hot haptic-path `Gamepad.all` re-resolution from `ApplyGamepadHaptics()`. Device discovery remains in the lifecycle/connect-disconnect path; the per-frame haptic path now uses the cached gamepad reference and clears stale devices.

These changes do not touch ProjectSettings, scenes, prefabs, packages, tags, layers, or public method signatures.

## Console Blockers

### HIGH: PC graphics settings still conflict with runtime URP budget authority

`SettingsManager` presets still expose 50/100/200/300 m shadow distances, while `HectonUrpShadowBudgetGuard` enforces 40 m on the active URP asset. The runtime API misuse is patched, but the authored settings UI/range and the guard policy still disagree.

Failure mode: on console, the settings UI can claim a value that the runtime guard clamps away. Certification/performance QA will see nondeterministic option behavior unless the option is either removed, tiered by platform profile, or explicitly described as a capped budget target.

Next repair: create one graphics-platform policy owner and make `SettingsPanel`, `SettingsManager`, and `HectonUrpShadowBudgetGuard` read the same shadow-distance budget table.

### HIGH: platform package/profile split is not present

The project has PC/Editor-oriented package and define residue in the active manifest/settings surface. Examples include MCP, ProBuilder, Visual Scripting, and heavy editor/third-party define history in `ProjectSettings.asset`. This is not proof those packages ship, but it is enough to block a clean console-readiness claim.

Failure mode: console build pipeline inherits editor or unsupported packages/defines, creating longer import times, package validation failures, or platform-specific strip errors.

Next repair: add a platform build profile audit document and CI gate. Do not remove packages blindly.

### HIGH: runtime assemblies remain Unity-coupled

The LTS compatibility mandate requires pure core/simulation layers and Unity API isolation behind backend adapters. Current runtime source still has large first-party MonoBehaviour/UnityEngine surfaces in core gameplay ownership. Current docs already state core asmdef isolation is not solved.

Failure mode: console/LTS migrations become source-wide surgery instead of adapter replacement. API deprecations, IL2CPP stripping, and package upgrades will hit gameplay files directly.

Next repair: pick one small domain and split pure data/contracts from Unity presentation before touching the whole project.

### MEDIUM: haptics are gamepad-rumble ready, not platform-controller complete

`ToolHapticsRuntime` is bounded and double-buffered; `InputDispatcher` dispatches to cached `Gamepad.SetMotorSpeeds`. This is good for baseline console rumble. There is no source evidence of DualSense adaptive trigger routing, platform-specific haptic device abstraction, or XR controller haptic output.

Failure mode: PlayStation/Xbox/Switch controller features degrade to generic rumble or fail platform feature expectations.

Next repair: add a narrow haptic backend interface owned by the input/platform layer; keep `ToolHapticsRuntime` as payload producer.

## VR Blockers

### CRITICAL: no project-owned XR runtime

OpenXR/XR Management is not present in `Packages/manifest.json`, and `ProjectSettings/XRSettings.asset` has no loader configuration. Crest has XR-aware internals, but the first-party gameplay/presentation layer does not own a VR runtime contract.

Failure mode: enabling XR later will be a project-wide integration effort, not a platform toggle.

Next repair: create a separate VR spike branch/profile. Do not turn on XR in the production project without a camera/HUD/input ownership plan.

### CRITICAL: HUD and menu are ScreenSpaceOverlay-first

Player HUD and menu assets/scripts use `ScreenSpaceOverlay`. VR needs world-space or camera-space canvases with tracked pose and stereo-safe interaction. Current first-party code can forcibly set overlay mode.

Failure mode: HUD is rendered as flat desktop overlay, absent per-eye, wrong depth, or unusable with tracked controllers.

Next repair: introduce a `PresentationMode` policy (`Desktop`, `Console`, `VR`) before changing HUD prefabs. VR mode must select world/camera-space UI and XR interaction modules. Do not blanket-rewrite current HUD prefabs.

### HIGH: player camera prefab is mixed XR-enabled/XR-disabled

`Player.prefab` contains multiple camera components with `m_AllowXRRendering: 0` and one with `1`. This may be valid for auxiliary cameras, but there is no documented camera authority saying which cameras are gameplay, HUD, visor, capture, or post-process in VR.

Failure mode: one eye sees missing HUD/postFX, auxiliary cameras render incorrectly, or stereo pass count doubles unexpectedly.

Next repair: document camera ownership per player prefab camera, then author a VR camera rig variant rather than mutating the desktop player prefab blindly.

### HIGH: Input System UI module has no XR tracking origin in main menu

`01_MAIN_MENU.unity` has `m_XRTrackingOrigin: {fileID: 0}`. This is acceptable for desktop, not enough for VR UI interaction.

Failure mode: tracked pointer/ray UI cannot operate in VR menus without scene-specific wiring.

Next repair: VR menu scene/prefab variant with explicit XR origin and controller/ray action maps.

### MEDIUM: foveated simulation is not VR foveated rendering

`FoveatedSimulationManager` throttles simulation based on camera-relative importance and schedules jobs. That helps performance. It is not OpenXR eye-tracked foveated rendering, fixed foveated rendering, stereo render-scale management, or VR compositor integration.

Failure mode: project discussions can overstate VR readiness because "foveated" exists in gameplay simulation code.

Next repair: keep this as simulation LOD only; add a separate VR render budget owner only after OpenXR profile exists.

## Regression Model

CPU: shadow-distance changes run only on settings/apply/quality paths. Haptic change removes a possible device-list scan from the haptic apply path when no cached gamepad exists. No profiler numbers were captured.

GC: no hot-path heap allocation was added. Haptic path now depends on a cached reference instead of resolving `Gamepad.all` during motor application. Measured `0 B/frame` proof is absent.

Memory: no new native/managed containers were added. No Memory Profiler capture was taken.

Cadence: no dispatcher order, job scheduling, scene loading, or render pipeline event cadence was changed.

Correctness: active URP asset shadow distance is now the settings write target. Remaining correctness risk is the unresolved policy conflict between UI preset ranges and the 40 m runtime guard.

## Verification State

- Play Mode: not launched.
- Unity platform build: not run.
- VR runtime: not configured or launched.
- Console build: not run.
- GC: not measured.
- Profiler/Frame Debugger/RenderDoc: not run.
- Local `Editor.log` available in this session had latest `Tundra build success` and strict post-success error scan count `0`, but it predates these code edits.
- Local compile after the source edits: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false` returned `0 Warning(s)` / `0 Error(s)`.

STATUS: PENDING VERIFICATION
