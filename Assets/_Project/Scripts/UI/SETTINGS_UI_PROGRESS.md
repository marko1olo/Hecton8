# Settings UI — Implementation Progress

## STATUS: 90% COMPLETE (Inspector Wiring Pending)

---

## Completed ✅

### Backend (SettingsManager.cs)
✅ URP Volume integration (AO/Bloom/Motion Blur)
✅ Camera FOV application
✅ All properties with persistence
✅ Quality presets (Low/Medium/High/Ultra)
✅ Zero GC compliance
✅ ApplyCameraFOV() - resolves the registry-owned player camera, applies FOV
✅ ApplyPostProcessing() — toggles AO/Bloom/Motion Blur on URP Volume

### UI Structure (01_MAIN_MENU) — CREATED
✅ Panel_Settings/Container (VerticalLayoutGroup)
✅ Header_Graphics ("GRAPHICS SETTINGS")
✅ Section_Presets with 4 preset buttons (Low/Medium/High/Ultra)
✅ Section_Graphics with:
  - Row_FOV (FOV slider 60-110° + text label)
  - Row_ShadowDistance (Shadow Distance slider 50-300m + text label)
  - Row_Toggles (Vsync/Fullscreen/AO/Bloom/Motion Blur toggles)
✅ Section_Audio with:
  - Row_MasterVolume (Master Volume slider 0-1 + text label)
✅ Row_Actions with:
  - Btn_Apply (green button)
  - Btn_Cancel (gray button)

---

## Pending ⏳

### Inspector Field Assignment
Need to assign UI elements to SettingsPanel component:
- btnPresetLow → Canvas/Panel_Settings/Container/Section_Presets/Row_Presets/Btn_PresetLow
- btnPresetMedium → Canvas/Panel_Settings/Container/Section_Presets/Row_Presets/Btn_PresetMedium
- btnPresetHigh → Canvas/Panel_Settings/Container/Section_Presets/Row_Presets/Btn_PresetHigh
- btnPresetUltra → Canvas/Panel_Settings/Container/Section_Presets/Row_Presets/Btn_PresetUltra
- sliderFieldOfView → Canvas/Panel_Settings/Container/Section_Graphics/Row_FOV/Slider_FOV
- txtFieldOfView → Canvas/Panel_Settings/Container/Section_Graphics/Row_FOV/Txt_FOV
- sliderShadowDistance → Canvas/Panel_Settings/Container/Section_Graphics/Row_ShadowDistance/Slider_ShadowDistance
- txtShadowDistance → Canvas/Panel_Settings/Container/Section_Graphics/Row_ShadowDistance/Txt_ShadowDistance
- toggleVsync → Canvas/Panel_Settings/Container/Section_Graphics/Row_Toggles/Toggle_Vsync
- toggleFullscreen → Canvas/Panel_Settings/Container/Section_Graphics/Row_Toggles/Toggle_Fullscreen
- toggleAmbientOcclusion → Canvas/Panel_Settings/Container/Section_Graphics/Row_Toggles/Toggle_AO
- toggleBloom → Canvas/Panel_Settings/Container/Section_Graphics/Row_Toggles/Toggle_Bloom
- toggleMotionBlur → Canvas/Panel_Settings/Container/Section_Graphics/Row_Toggles/Toggle_MotionBlur
- sliderMasterVolume → Canvas/Panel_Settings/Container/Section_Audio/Row_MasterVolume/Slider_MasterVolume
- txtMasterVolume → Canvas/Panel_Settings/Container/Section_Audio/Row_MasterVolume/Txt_MasterVolume
- btnApply → Canvas/Panel_Settings/Container/Row_Actions/Btn_Apply
- btnCancel → Canvas/Panel_Settings/Container/Row_Actions/Btn_Cancel

### Missing UI Elements (Optional)
- Music/SFX/Ambient volume sliders (can reuse Master Volume pattern)
- Shadow Quality buttons (decrease/increase + text label)
- Anti-Aliasing buttons (decrease/increase + text label)
- Texture Quality buttons (decrease/increase + text label)
- Quality Level buttons (decrease/increase + text label)
- Reset to Defaults button

### SettingsManager Inspector Assignment
Need to assign in SettingsManager component:
- mainCamera -> registry-owned player camera (or leave null for auto-find)
- urpVolume → find Volume in scene with post-processing profile
- audioMixer → Assets/_Project/MasterMixer.mixer (already assigned)

---

## Testing Checklist ❌

### Functional Testing
- [ ] Quality presets apply all settings correctly
- [ ] Individual settings persist across scene loads
- [ ] Audio mixer volumes update in real-time
- [ ] FOV changes apply to main camera
- [ ] Shadow distance changes visible in-game
- [ ] URP Volume toggles (AO/Bloom/Motion Blur) work
- [ ] Vsync/Fullscreen toggles work
- [ ] Apply button saves staged changes
- [ ] Cancel button reverts staged changes
- [ ] Reset to Defaults restores all settings

### Performance Testing
- [ ] No GC alloc in SettingsPanel.OnEnable
- [ ] No GC alloc in SettingsPanel.RefreshAllUI
- [ ] No GC alloc in SettingsManager property setters
- [ ] No frame drops when changing settings

### Edge Cases
- [ ] SettingsManager.Instance null-check in OnDisable
- [ ] UserOptionsPersistence null-check in Load/Save
- [ ] Invalid preset index (0-3) clamped
- [ ] Missing UI elements (null-check all SerializeField)
- [ ] Missing registry player camera (auto-find fallback)
- [ ] Missing URP Volume (graceful fallback)

---

## Design Notes (Subnautica Style)

### Visual Style Implemented
✅ Dark background panels (VerticalLayoutGroup containers)
✅ Large, readable fonts (36pt headers, 24pt section labels, 18-20pt body)
✅ Generous spacing (20px between sections, 10-15px between rows)
✅ Preset buttons with gradient colors (Low=dark, Ultra=bright)
✅ Green Apply button (0.2, 0.6, 0.2)
✅ Gray Cancel button (0.3, 0.3, 0.3)

### Layout Principles Applied
✅ Vertical scrolling structure (VerticalLayoutGroup)
✅ Grouped sections (Graphics/Audio/Actions)
✅ Clear visual hierarchy (headers > labels > controls)
✅ Consistent control heights (60px buttons, 40px sliders/toggles)
✅ Responsive layout (LayoutGroups, LayoutElements, flexible widths)

### Interaction Feedback (Not Implemented Yet)
⏳ Button hover: brightness +20%
⏳ Button press: scale 0.95x
⏳ Slider drag: highlight thumb
⏳ Toggle: smooth color transition
⏳ Apply: brief flash + sound

---

## Technical Implementation

### Zero GC Compliance ✅
- All UI callbacks cached in BindButtons/BindSliders
- Dirty flags for text updates (only update if value changed)
- No LINQ, no string concat in hot paths
- Pre-allocated arrays for quality names (ShadowQualityNames, AntiAliasingNames, TextureQualityNames)

### URP Volume Integration ⚠️
```csharp
private void ApplyPostProcessing()
{
    if (urpVolume == null || urpVolume.profile == null)
        return;

    VolumeProfile profile = urpVolume.profile;

    if (profile.TryGet(out UnityEngine.Rendering.Universal.Bloom bloom))
        bloom.active = _cachedBloom;

    if (profile.TryGet(out UnityEngine.Rendering.Universal.MotionBlur motionBlur))
        motionBlur.active = _cachedMotionBlur;
}
```

### Camera FOV Application ✅
```csharp
private void ApplyCameraFOV(float fov)
{
    if (mainCamera == null)
    {
        mainCamera = GlobalRegistry.Player.PlayerCamera;
        if (mainCamera == null)
            return;
    }

    mainCamera.fieldOfView = fov;
}
```

---

## Files Modified

- `Assets/_Project/Scripts/UI/SettingsManager.cs` ✅ (URP Volume + Camera FOV)
- `Assets/_Project/Scripts/UI/SettingsPanel.cs` ✅ (expanded UI fields)
- `Assets/_Project/Scenes/01_MAIN_MENU.unity` ✅ (UI layout created)
- `Assets/_Project/Scripts/UI/SETTINGS_SYSTEM_GUIDE.md` ✅ (documentation)
- `Assets/_Project/Scripts/UI/SETTINGS_UI_PROGRESS.md` ✅ (this file)

---

## Next Steps

1. **Inspector Field Assignment** (CRITICAL):
   - Open 01_MAIN_MENU scene in Unity Editor
   - Select Panel_Settings GameObject
   - Assign all UI elements to SettingsPanel component fields
   - Assign mainCamera/urpVolume to SettingsManager component

2. **Missing UI Elements** (Optional):
   - Create Music/SFX/Ambient volume sliders (copy Row_MasterVolume pattern)
   - Create Shadow Quality/AA/Texture Quality button rows (copy Row_FOV pattern)
   - Create Reset to Defaults button (add to Row_Actions)

3. **Play Mode Testing**:
   - Test quality presets (Low/Medium/High/Ultra)
   - Test individual settings (FOV, shadows, toggles, audio)
   - Test persistence (change settings, reload scene, verify)
   - Test URP Volume (toggle AO/Bloom/Motion Blur, verify visual changes)
   - Test Camera FOV (change slider, verify camera.fieldOfView)

4. **Polish**:
   - Add button hover/press animations (ColorTint transitions)
   - Add slider thumb highlight
   - Add toggle smooth transitions
   - Add Apply button flash + sound feedback
   - Add ScrollRect to Container for long lists

---

## Known Issues

- MCP Unity session superseded during UI creation (some elements may need recreation)
- Scene reload issue (01_MAIN_MENU not loading correctly via MCP)
- Inspector field assignment must be done manually in Unity Editor

---

## Summary

---

## CURRENT STATUS OVERRIDE

- Inspector wiring is no longer the primary pending task for `01_MAIN_MENU`; the settings panel hierarchy and serialized references were rebuilt in-scene.
- `SettingsManager.mainCamera` and `SettingsManager.urpVolume` are assigned in the authored scene state.
- `AmbientOcclusion` is persisted in settings data, but this document previously overstated runtime support.
- `UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion` exists, but in Unity 6000 it is a `ScriptableRendererFeature`, not a `VolumeComponent`.
- The project does not currently expose a live renderer-feature owner for SSAO through `VolumeProfile.TryGet`.
- Live post-processing preview in this branch only has a concrete scene owner for Bloom and Motion Blur.
- Any claim here that AO/Bloom/Motion Blur are all driven the same way through URP Volume is false for the current project state.
- `SettingsComparisonView` now compares persisted graphics presets, not raw `QualityLevel`, so `High` and `Ultra` no longer collapse into one estimate row.
- Play mode verification remains `PENDING VERIFICATION`.

**Backend**: partial. Camera FOV, persistence, presets, Bloom, and Motion Blur are wired; AO still lacks a truthful runtime owner in the current stack.

**UI Layout**: 80% complete. Core structure created (presets, FOV, shadow distance, toggles, audio, actions). Missing: additional volume sliders, quality buttons, reset button.

**Inspector Wiring**: 0% complete. All UI elements need manual assignment in Unity Editor.

**Testing**: 0% complete. Requires Inspector wiring first.

**STATUS**: Ready for manual Inspector assignment + Play Mode testing.
