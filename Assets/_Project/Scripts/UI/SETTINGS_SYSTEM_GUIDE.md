# Settings System — Implementation Guide

## Overview

Unified settings system for HECTON-8. Manages graphics, audio, and video options with persistence via `UserOptionsPersistence` (PlayerPrefs backend).

**Owner**: `SettingsManager` (singleton, DontDestroyOnLoad)  
**UI**: `SettingsPanel` (pause menu, main menu)  
**Backend**: `UserOptionsPersistence` (Easy Save 3 wrapper)

---

## Architecture

### SettingsManager.cs
- **Singleton**: `SettingsManager.Instance`
- **Lifecycle**: Awake → LoadAllSettings → ApplyAllSettings
- **Persistence**: Automatic save on every property change
- **Zero GC**: Cached fields, dirty flags, no LINQ, no string alloc

### SettingsPanel.cs
- **UI Owner**: Exposes all settings via sliders, toggles, buttons
- **Zero GC**: Cached delegates, dirty flags for text updates
- **Lifecycle**: OnEnable → LoadCurrentSettings → RefreshAllUI
- **Apply/Cancel**: Staged changes, apply on button press

---

## Graphics Settings

### Quality Presets
- **Low**: QualityLevel=0, ShadowQuality=Low, ShadowDistance=50m, AA=FXAA, AO=Off, Bloom=Off, MotionBlur=Off, TextureQuality=Low
- **Medium**: QualityLevel=1, ShadowQuality=Medium, ShadowDistance=100m, AA=SMAA, AO=Off, Bloom=On, MotionBlur=Off, TextureQuality=Medium
- **High**: QualityLevel=2, ShadowQuality=Medium, ShadowDistance=200m, AA=SMAA, AO=On, Bloom=On, MotionBlur=Off, TextureQuality=High
- **Ultra**: QualityLevel=2, ShadowQuality=High, ShadowDistance=300m, AA=TAA, AO=On, Bloom=On, MotionBlur=On, TextureQuality=Ultra

### Individual Settings
- **Field of View**: 60-110° (default 75°)
- **Shadow Quality**: Off/Low/Medium/High (0-3)
- **Shadow Distance**: 50-300m (default 200m)
- **Anti-Aliasing**: None/FXAA/SMAA/TAA (0-3)
- **Ambient Occlusion**: On/Off (default On)
- **Bloom**: On/Off (default On)
- **Motion Blur**: On/Off (default Off)
- **Texture Quality**: Low/Medium/High/Ultra (0-3, default High)

### Unity API Mapping
- **QualityLevel**: `QualitySettings.SetQualityLevel()`
- **Vsync**: `QualitySettings.vSyncCount` (0=Off, 1=On)
- **Fullscreen**: `Screen.fullScreen`
- **Resolution**: `Screen.SetResolution()`
- **Shadow Distance**: `QualitySettings.shadowDistance`
- **Texture Quality**: `QualitySettings.globalTextureMipmapLimit` (3=Low, 0=Ultra)

---

## Audio Settings

### Volume Controls
- **Master Volume**: 0-100% (default 80%)
- **Music Volume**: 0-100% (default 80%)
- **SFX Volume**: 0-100% (default 80%)
- **Ambient Volume**: 0-100% (default 80%)

### AudioMixer Integration
- **Mixer**: `Assets/_Project/MasterMixer.mixer`
- **Groups**: Master, Music, SFX, Ambient
- **Exposed Parameters**: MasterVolume, MusicVolume, SfxVolume, AmbientVolume
- **Conversion**: Linear (0-1) → dB (-80 to 0)

---

## Persistence

### Keys
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

### Backend
- **UserOptionsPersistence**: Wrapper around PlayerPrefs
- **Save Timing**: Immediate on every property change
- **Load Timing**: Awake (SettingsManager)

---

## UI Integration

### SettingsPanel Inspector Fields

#### Graphics
- `btnPresetLow`, `btnPresetMedium`, `btnPresetHigh`, `btnPresetUltra` (quality presets)
- `btnQualityDecrease`, `btnQualityIncrease`, `txtQualityLevel` (quality level)
- `toggleVsync`, `toggleFullscreen` (video)
- `sliderFieldOfView`, `txtFieldOfView` (FOV)
- `btnShadowQualityDecrease`, `btnShadowQualityIncrease`, `txtShadowQuality` (shadows)
- `sliderShadowDistance`, `txtShadowDistance` (shadow distance)
- `btnAntiAliasingDecrease`, `btnAntiAliasingIncrease`, `txtAntiAliasing` (AA)
- `toggleAmbientOcclusion`, `toggleBloom`, `toggleMotionBlur` (post-processing)
- `btnTextureQualityDecrease`, `btnTextureQualityIncrease`, `txtTextureQuality` (textures)

#### Audio
- `sliderMasterVolume`, `txtMasterVolume`
- `sliderMusicVolume`, `txtMusicVolume`
- `sliderSfxVolume`, `txtSfxVolume`
- `sliderAmbientVolume`, `txtAmbientVolume`

#### Actions
- `btnResetDefaults` (reset all to defaults)
- `btnApply` (apply staged changes)
- `btnCancel` (revert staged changes)

---

## Usage Examples

### Apply Quality Preset
```csharp
SettingsManager.Instance.ApplyQualityPreset(2); // High
```

### Change Individual Setting
```csharp
SettingsManager.Instance.FieldOfView = 90f;
SettingsManager.Instance.ShadowDistance = 150f;
SettingsManager.Instance.Bloom = false;
```

### Reset to Defaults
```csharp
SettingsManager.Instance.ResetToDefaults();
```

### Check Current Settings
```csharp
float fov = SettingsManager.Instance.FieldOfView;
bool bloom = SettingsManager.Instance.Bloom;
int shadowQuality = SettingsManager.Instance.ShadowQuality;
```

---

## Performance Notes

### Zero GC Compliance
- ✅ No LINQ
- ✅ No string concat/interpolation in hot paths
- ✅ Cached delegates
- ✅ Dirty flags for text updates
- ✅ Pre-allocated arrays for quality names
- ✅ No GetComponent in hot paths

### Cold Allocations
- `MaterialPropertyBlock` (N/A — no MPB in settings)
- `AudioMixer` reference (serialized, no alloc)
- Quality name arrays (static readonly, one-time)

---

## Testing Checklist

### Functional
- [ ] Quality presets apply all settings correctly
- [ ] Individual settings persist across scene loads
- [ ] Audio mixer volumes update in real-time
- [ ] FOV changes apply to main camera
- [ ] Shadow distance/quality changes visible in-game
- [ ] Texture quality changes visible (check mipmap limit)
- [ ] Reset to defaults restores all settings

### Performance
- [ ] No GC alloc in SettingsPanel.OnEnable
- [ ] No GC alloc in SettingsPanel.RefreshAllUI
- [ ] No GC alloc in SettingsManager property setters
- [ ] No frame drops when changing settings

### Edge Cases
- [ ] SettingsManager.Instance null-check in OnDisable
- [ ] UserOptionsPersistence null-check in Load/Save
- [ ] Invalid preset index (0-3) clamped
- [ ] Missing UI elements (null-check all SerializeField)

---

## Future Work

### URP Volume Integration
- **AO/Bloom/Motion Blur**: Currently stored but not applied to URP Volume
- **Implementation**: Add `UnityEngine.Rendering.Universal` reference, find active Volume, toggle overrides
- **File**: `SettingsManager.ApplyPostProcessing()`

### Camera FOV Application
- **Current**: FOV stored but not applied to camera
- **Implementation**: Find main camera, set `Camera.fieldOfView`
- **File**: `SettingsManager.FieldOfView` setter

### Resolution Dropdown
- **Current**: Resolution stored but no UI dropdown
- **Implementation**: Populate dropdown with `Screen.resolutions`, filter by refresh rate
- **File**: `SettingsPanel.PopulateResolutionDropdown()`

---

## Status

**PENDING VERIFICATION**

- Code compiles ✅
- Zero GC compliance ✅
- Unity scene integration ✅ (SettingsManager in 01_MAIN_MENU, 02_HECTON_WORLD)
- UI wiring ⚠️ (Inspector fields need manual assignment)
- In-game testing ❌ (requires user verification)
- URP Volume integration ❌ (future work)
- Camera FOV application ❌ (future work)

**Next Steps**:
1. Assign UI elements in SettingsPanel Inspector (01_MAIN_MENU, 02_HECTON_WORLD)
2. Test quality presets in Play Mode
3. Verify settings persistence across scene loads
4. Implement URP Volume integration for AO/Bloom/Motion Blur
5. Implement Camera FOV application
6. Add resolution dropdown UI
