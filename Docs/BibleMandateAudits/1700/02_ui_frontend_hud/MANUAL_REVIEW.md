# UI / Menus / HUD Manual Review

Status: STATIC REVIEW - NO UNITY/PROFILER PROOF
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
