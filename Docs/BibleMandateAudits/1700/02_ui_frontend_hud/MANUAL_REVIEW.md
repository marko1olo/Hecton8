# UI / Menus / HUD Manual Review

Status: YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING - NO UNITY/PROFILER PROOF
Date: 2026-06-02

## Reviewed Files

- `Assets/_Project/Scripts/UI/FontAssetRecovery.cs`
- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`
- `Assets/_Project/Scripts/UI/SettingsPanel.cs`
- `Assets/_Project/Scripts/UI/SettingsManager.cs`
- `Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs`
- `Assets/_Project/Scripts/UI/ShaderCompassRibbon.cs`
- `Assets/_Project/Scripts/UI/SonarHoloCompass.cs`
- `Assets/_Project/Scripts/UI/SubmarineSonarHoloMapRenderer.cs`
- `Assets/_Project/Scripts/UI/SubtitleManager.cs`
- `Assets/_Project/Scripts/UI/UIParticleEffect.cs`
- `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs`
- `Assets/_Project/Scripts/UI/WorldSpaceTMPSharpnessController.cs`
- `Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs`
- `Assets/_Project/Scripts/Core/UIStateStore.cs`
- `Assets/_Project/Scripts/Core/SceneRuntimeService.cs`

## What Exists

- HUD runtime uses dispatcher-facing interfaces (`ISlowTickable`, `ILateFrameTickable`) instead of raw `Update`.
- `H8Debug` logging is compile-stripped outside editor/development builds.
- Settings UI logs are mostly diagnostic/user-action paths, not per-frame gameplay logs.
- `FontAssetRecovery` editor material repair is guarded by `#if UNITY_EDITOR`; active runtime bootstrap does not call the private recovery scan.
- `SuitHUDV4CanvasOverlay` owns its UI materials and uses cached/dirty material writes for acoustic radar and HUD effects.
- `SubtitleManager` uses fixed char buffers and span copy paths for many subtitle flows.
- Several HUD/cockpit/wrist systems use retained buffers/material property blocks rather than obvious per-frame managed strings in the reviewed snippets.

## What Is Missing / Not Proven

- UI profiler proof: no 0 B/frame capture for HUD/menu interactions was run.
- `SuitHUDV4CanvasOverlay.BuildThreatChevronMesh()` still creates a Mesh in player bootstrap; release should prefer a serialized mesh asset or prove this is a single cold fallback.
- Runtime-created UI materials need prefab/setup proof showing no repeated creation during scene reload, language swap, or HUD re-enable loops.
- Font production needs proof that all release prefabs use assigned static font atlases/materials and do not rely on recovery code.
- `TopographicalSonarSynthesizer` uses setup-time persistent job buffers and graphics buffers, then schedules ping/fade jobs. It still needs ping spam proof, upload budget proof, and proof that mock SDF is not release truth.
- `ShaderCompassRibbon`, `SonarHoloCompass`, `SubtitleManager`, `UIParticleEffect`, and `SuitHUDV4CanvasOverlay` create runtime UI GameObjects/children. This can be acceptable bootstrap assembly only if no post-bootstrap hierarchy growth occurs.
- `SubmarineSonarHoloMapRenderer`, `SuitHUDV4CanvasOverlay`, `VehicleSubOsCockpitRuntime`, and `WristHologramHudRuntime` create runtime meshes/materials. Release UI needs prefab/material proof or a bounded one-time construction proof.
- `WorldSpaceTMPSharpnessController` creates per-label TMP material instances; this needs count/batching proof.
- `VehicleSubOsCockpitRuntime` has fallback damage proxy/glyph routes. Those need capture proof that fallback display is readable and not hiding missing authored damage proxy data.

## Current Classification

- `LINE_LEVEL_CLASSIFICATION.md`: all 241 runtime suspect lines classified; 128 editor/dev guarded, 86 cold/setup/fault/user-action paths, 24 false positives, and 3 registered runtime violations.
- `FontAssetRecovery.cs`: `LEGAL_EDITOR_OR_DEV_GUARDED` for active route; dormant runtime methods must not be revived without editor-only guard.
- `SuitHUDV4CanvasOverlay.cs`: `YELLOW_BOOTSTRAP_REVIEW_REQUIRED`.
- `SettingsPanel.cs` / `SettingsManager.cs`: `LIKELY_LEGAL_DIAGNOSTIC_OR_USER_ACTION_PATH`.
- `TopographicalSonarSynthesizer.cs`: `YELLOW_UI_SENSOR_PROOF_REQUIRED`.
- UI runtime hierarchy assembly: `YELLOW_BOOTSTRAP_ONLY_PROOF_REQUIRED`.
- UI runtime meshes/materials: `YELLOW_UI_RUNTIME_ASSET_PROOF_REQUIRED`.
- Per-label TMP material clones: `YELLOW_UI_BATCHING_PROOF_REQUIRED`.

## Required Next Proof

- Menu/HUD interaction profiler with GC Alloc column visible for at least 300 frames.
- Screenshot/capture for compact and high tiers after localization expansion.
- Prefab audit confirming authored fonts, HUD materials, and no repeated runtime mesh/material creation.
- Sonar ping/fade stress proof with GPU upload bytes, job latency, active point count, and mock/fallback flags recorded.
- 300-frame HUD/cockpit/subtitle/wrist interaction capture showing no post-bootstrap GameObject creation, no repeated material creation, no Canvas rebuild spikes, and 0 B/frame steady-state UI text updates.

## Pass 6 Addendum - Non-Editor Overlay Boundary

- Non-editor scan found several `OnGUI` diagnostic/tuner files outside `/Editor/` paths. Even when they are not normal HUD code, release closure requires asmdef/define proof that IMGUI overlays are absent from player builds.
- UI runtime proof must include build-symbol exclusion for diagnostic overlays in addition to HUD/menu profiler captures.

## Pass 7 Addendum - Menu And Font Runtime Boundary

- `SettingsPanel.CreateMenuStyleTextCold()` and `ConfigureMenuStyleLayoutCold()` read as legal cold menu assembly, not a steady-state UI tick. Closure still needs menu interaction proof that the panel does not rebuild hierarchy or add layout components after construction.
- `FontAssetRecovery` material repair and asset import repair are editor-guarded, but runtime `RefreshTextComponent()` can force a TMP mesh rebuild. Release closure requires callsite proof that this is bootstrap/recovery only and not the normal text update route.

## Pass 8 Addendum - Suit HUD Runtime Materials And Hierarchy

- `SuitHUDV4CanvasOverlay` creates runtime materials for threat chevron, dithered background, saving pulse, and acoustic radar paths. These may be cold owner resources, but release acceptance needs prefab/material assignment proof or lifecycle proof that creation happens once and never repeats under HUD re-enable, language swap, scene reload, or save-state transitions.
- The HUD can add `CanvasGroup`, `RectMask2D`, `CanvasRenderer`, isolated `Canvas`, gauge graphics, quickbar images, TMP labels, and a content root GameObject during bootstrap assembly. This is not automatically a violation, but it is still not a release-green UI route until a 300-frame interaction capture proves no post-bootstrap hierarchy growth, no canvas rebuild spikes, and 0 B/frame steady-state text updates.

## Pass 11 Addendum - Topographical Sonar Buffer And Mock SDF Detail

- `TopographicalSonarSynthesizer` has persistent H8Memory job buffers, DataVault handles, double point buffers, indirect args buffer, shader globals buffer, non-forced job finalization, ping interval gating, and black-box telemetry.
- The release gate remains yellow because `ScheduleSonarScan()` can generate mock SDF when no published SDF snapshot is available. UI can display diagnostic mock data, but production sonar cannot present mock geometry as real terrain.
- Required proof: published SDF/DataMonolith availability in release scenes, ping spam profile, GPU upload budget, no repeated graphics buffer creation after bootstrap, and black-box dump only on fault.

## Pass 15 Addendum - UI State Store And Scene Transition Overlay

- `UIStateStore` owns fixed native arrays for UI state, numeric slots, PDA rollback snapshots, and event hashes. The state shape is good after initialization.
- The unresolved UI issue is first-use: methods like `SetPDAOpenState`, `SetPDAActiveTab`, `AppendPDALogEventHash`, `WriteValue`, and `Clear` call `EnsureInitialized()`. If bootstrap does not call it first, the first UI interaction can allocate persistent native arrays.
- `SceneRuntimeService` creates a transition overlay hierarchy and a dither material during scene transition. This may be legal cold scene-transition work, but release UI proof must show fixed count, assigned shader/material, no repeated leaks, and no steady-state UI hierarchy growth.
- UI proof now needs `UIStateStore` prewarm evidence in addition to the existing HUD/menu/cockpit/wrist interaction profiler.

## Pass 17 Addendum - UI Fallback Assets, Localization Staging, And Input Remap

- `DiegeticMenuCanvasUtility.ResolveCamera(...)` uses `Camera.main` only in menu setup context after a preferred camera check. `NormalizeReadableText(...)` scans TMP children with a static scratch list and labels itself as main-menu setup only. Classification: `LEGAL_COLD_MENU_SETUP_WITH_INJECTED_CAMERA_PROOF_REQUIRED`.
- `DiegeticPDAController` resolves and caches tablet renderer/collider/canvas visibility lists in `Awake()`, `OnEnable()`, and root-change paths. This is cold/rebind-shaped, not steady-state PDA open/close proof. It remains yellow until repeated enable/language/scene cases show no hierarchy scan or runtime EventSystem/material fallback in normal interaction.
- `AcousticRadarSphereRenderer` late-frame drawing uses fixed matrices and `DrawMeshInstanced`, but missing authored `voxelMesh` creates a runtime cube mesh and missing material setup creates a runtime material. Production HUD/radar cannot rely on that fallback.
- `DiegeticVisorHudMesh` builds a runtime projection mesh/material and DataVault black-box ring in `OnEnable()`. Method review found `RefreshQualityPolicy()` can set `_meshRebuildDirty`, but `LateFrameTick()` clears that flag without calling `RebuildMesh()`. This either intentionally makes mesh quality bootstrap-only or is a functional quality-scaling bug.
- `DiegeticGlitchSurgeonRuntime` owns large persistent H8Memory scratch storage for text/glitch/quads/radar/synth/telemetry. Shape is acceptable only with boot prewarm counters; fault dumps use Temp payloads and must remain fault-only.
- `LocRegistry` span/UTF-8 resolve routes are strong, but `TryBeginBabelDictionaryStage(...)` and `EnsureOverrideCsvScratch()` allocate persistent stage/scratch storage. Language switch and override staging must be explicit bounded transactions, not hidden first-use HUD lookups.
- `ControlRemapper` Temp buffers are user-action IO/settings save-load paths, not HUD hot paths. Acceptance still needs a visible blocking/settings transaction proof so control remap cannot freeze gameplay silently.
- `RelayHUDRuntimeBootstrap` creates marker hierarchy after scene load if the active HUD has none. This is a fail-safe only; authored HUD release scenes must prove the marker exists without runtime repair.

## Pass 21 Addendum - Line-Level UI Runtime Closure

- Added `LINE_LEVEL_CLASSIFICATION.md` and classified all 241 runtime suspect lines in the UI/menu/HUD/terminal/localization/settings group.
- Result: 128 `LEGAL_EDITOR_OR_DEV_GUARDED`, 86 `LEGAL_COLD_PATH`, 24 `FALSE_POSITIVE`, and 3 registered `RUNTIME_VIOLATION` lines.
- Registered violation lines: `AcousticRadarSphereRenderer.cs:531`, `DiegeticVisorHudMesh.cs:438`, and `VehicleSubOsCockpitRuntime.cs:2601`.
- `RB-131` was strengthened to include the `VehicleSubOsCockpitRuntime` fallback damage hologram cube mesh path and to require authored cockpit-damage-proxy proof.
- Static closure is not release proof. The UI group remains yellow until authored fallback assets, boot prewarm, localization/language-switch transactions, input-remap IO, visor quality scaling, and 300-frame player profiler captures are provided.
