# Settings System Implementation Guide

Date: 2026-06-09
Status: CURRENT STATIC SOURCE ROUTE / UNITY PROOF PENDING
Evidence class: STATIC_SOURCE / STATIC_DOC

This guide describes the current settings runtime source route. It is not Unity scene wiring proof, Play Mode proof, profiler proof, GC proof, visual proof, or imported-asset proof.

## Source Anchors

- `Assets/_Project/Scripts/UI/SettingsManager.cs`
- `Assets/_Project/Scripts/UI/SettingsPanel.cs`
- `Assets/_Project/Scripts/UI/SettingsLivePreview.cs`
- `Assets/_Project/Scripts/UI/SettingsComparisonView.cs`
- `Assets/_Project/Scripts/UI/SettingsPanelAnimator.cs`
- `Assets/_Project/Scripts/UI/SettingsPanelProfiler.cs`
- `Assets/_Project/Scripts/Input/UserOptionsPersistence.cs`

## Owner Boundary

- `SettingsManager` is the runtime settings owner. It is a `MonoBehaviour` singleton registered through runtime lifecycle, scene-load handling, and `GlobalRegistry` hot-swap callbacks.
- `UserOptionsPersistence` owns the persistent `options.h8cfg` backend under `Application.persistentDataPath`. Settings must not be documented as PlayerPrefs or Easy Save 3.
- `SettingsPanel` owns the menu interaction surface. It stages values locally, applies them to `SettingsManager` only through Apply/reset flows, and can cancel pending preview state.
- `SettingsLivePreview`, `SettingsComparisonView`, `SettingsPanelAnimator`, and `SettingsPanelProfiler` are presentation/support components. They do not own persistent settings truth.
- `HomeostasisBrain` owns the effective runtime quality pressure after user preference, hardware ceilings, DRS, thermal pressure, and health pressure are combined. `SettingsManager.QualityLevel` is a saved user preference, not final runtime quality truth.

## Persistence Route

`SettingsManager` loads all cached values from `UserOptionsPersistence` during startup, validates/migrates them, applies them, and saves changed properties back to `options.h8cfg`.

Current persisted keys include:

- `Hecton_QualityLevel`
- `Hecton_MasterVolume`
- `Hecton_MusicVolume`
- `Hecton_SfxVolume`
- `Hecton_AmbientVolume`
- `Hecton_Vsync`
- `Hecton_Fullscreen`
- `Hecton_ResolutionWidth`
- `Hecton_ResolutionHeight`
- `Hecton_FieldOfView`
- `Hecton_ShadowQuality`
- `Hecton_ShadowDistance`
- `Hecton_AntiAliasing`
- `Hecton_AmbientOcclusion`
- `Hecton_Bloom`
- `Hecton_MotionBlur`
- `Hecton_TextureQuality`
- `Hecton_GraphicsPreset`

The manager also persists menu/accessibility/VR preference keys. Do not remove those paths from docs or UI without checking `SettingsManager.cs` and `SettingsPanel.cs`.

## Graphics Route

- Quality preference is stored as a continuous user index in the `0..6` range and mapped through `HomeostasisBrain.SetUserGlobalQualityWeightPreference`.
- Graphics preset remains a `0..3` UI grouping for Low/Medium/High/Ultra-style user intent. It is separate from final `HomeostasisBrain.GlobalQualityWeight`.
- Unity quality presets are not the runtime authority. Do not document runtime scalability as `QualitySettings.SetQualityLevel`.
- VSync writes `QualitySettings.vSyncCount`.
- Resolution writes `Screen.SetResolution(width, height, fullscreen)`.
- Shadow distance writes `QualitySettings.shadowDistance`.
- Texture quality writes `QualitySettings.globalTextureMipmapLimit`.
- FOV is applied to a resolved camera. The manager tries serialized camera, player-owned camera, local camera, children, and parents, and keeps a pending FOV apply flag when camera resolution is not ready.
- Bloom and Motion Blur are applied through a resolved URP `VolumeProfile`.
- Ambient Occlusion is currently persisted and previewed in UI paths, but Unity 6000 URP SSAO is a renderer feature, not a `VolumeComponent`; this guide must not claim AO is visually applied through `VolumeProfile.TryGet`.

## Audio Route

- Master/Music/SFX/Ambient volumes are persisted as `0..1` floats.
- Runtime application uses `AudioMixer.SetFloat` parameter names `MasterVolume`, `MusicVolume`, `SfxVolume`, and `AmbientVolume`.
- Linear volume is converted to dB with the normal `20 * log10(value)` mapping and a silence floor.
- Missing mixer binding is a degraded state; it is not proof that audio settings work.

## UI Route

- `SettingsPanel` captures values from `SettingsManager` on enable and refreshes TMP/slider/toggle state without applying every edit immediately.
- Apply writes cached panel values to `SettingsManager`, runs live preview apply, applies graphics preset intent, then calls `ApplyAllSettings`.
- Cancel rolls back live preview, restores menu visual snapshot state, and refreshes from the last committed values.
- Reset clears the persisted settings keys through `SettingsManager.ResetToDefaults`, writes defaults, applies them, and refreshes UI.
- `SettingsPanel` uses cached UnityAction delegates, dirty text buffers, localized label hashes, and `SetValueWithoutNotify` / `SetIsOnWithoutNotify` patterns to avoid listener feedback loops.

## Lifecycle And Failure Contract

- `SubsystemRegistration` must reset the runtime singleton so domain reload does not leave a stale `SettingsManager`.
- Scene load must retry camera/volume/mixer binding and apply pending FOV/post-processing/audio state where possible.
- `GlobalRegistryServiceSlot.UserOptions` replacement must rebind persistence and flush pending settings without losing the latest cached values.
- Missing persistence, missing mixer, missing camera, missing volume, invalid stored values, invalid resolution dimensions, and failed `TrySave` are degraded states that must be visible in logs or proof artifacts.
- No active doc may claim compile/import/scene wiring/visual application/profiler success from this static guide alone.

## Current Known Gaps

- AO visual application still needs a renderer-feature owner path and Unity-side proof.
- UI field wiring, Play Mode apply/cancel behavior, visual settings effect, profiler/GC, and player persistence roundtrip remain `PENDING VERIFICATION` until fresh Unity artifacts exist.
- `SettingsPanelProfiler` can capture local apply metrics, but those logs are not a substitute for full runtime profiler/GCMonitor proof.

## Garbage-Collection Rule

Do not move this file or the settings UI support files to `Docs/DEPRECATED` while `SettingsManager`, `SettingsPanel`, and their `.meta` files remain active Unity assets. If a settings guide becomes stale again, replace its body with source-route facts or move it together with its `.meta` only after checking references and scene/prefab GUID usage.
