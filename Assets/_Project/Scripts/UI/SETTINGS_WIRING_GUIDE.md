# Settings UI — Manual Wiring Guide

## STATUS: UI Created, Wiring Pending

The settings UI structure has been created in `01_MAIN_MENU.unity` scene, but Inspector field assignment must be done manually in Unity Editor due to MCP limitations.

---

## Step 1: Open Scene

1. Open `Assets/_Project/Scenes/01_MAIN_MENU.unity` in Unity Editor
2. Locate `Canvas/Panel_Settings` GameObject in Hierarchy
3. Select it to view SettingsPanel component in Inspector

---

## Step 2: Assign UI References to SettingsPanel Component

### Graphics Section

**Preset Buttons:**
- `btnPresetLow` → `Canvas/Panel_Settings/Container/Section_Presets/Row_Presets/Btn_PresetLow`
- `btnPresetMedium` → `Canvas/Panel_Settings/Container/Section_Presets/Row_Presets/Btn_PresetMedium`
- `btnPresetHigh` → `Canvas/Panel_Settings/Container/Section_Presets/Row_Presets/Btn_PresetHigh`
- `btnPresetUltra` → `Canvas/Panel_Settings/Container/Section_Presets/Row_Presets/Btn_PresetUltra`

**Field of View:**
- `sliderFieldOfView` → `Canvas/Panel_Settings/Container/Section_Graphics/Row_FOV/Slider_FOV`
- `txtFieldOfView` → `Canvas/Panel_Settings/Container/Section_Graphics/Row_FOV/Txt_FOV`

**Shadow Distance:**
- `sliderShadowDistance` → `Canvas/Panel_Settings/Container/Section_Graphics/Row_ShadowDistance/Slider_ShadowDistance`
- `txtShadowDistance` → `Canvas/Panel_Settings/Container/Section_Graphics/Row_ShadowDistance/Txt_ShadowDistance`

**Toggles:**
- `toggleVsync` → `Canvas/Panel_Settings/Container/Section_Graphics/Row_Toggles/Toggle_Vsync`
- `toggleFullscreen` → `Canvas/Panel_Settings/Container/Section_Graphics/Row_Toggles/Toggle_Fullscreen`
- `toggleAmbientOcclusion` → `Canvas/Panel_Settings/Container/Section_Graphics/Row_Toggles/Toggle_AO`
- `toggleBloom` → `Canvas/Panel_Settings/Container/Section_Graphics/Row_Toggles/Toggle_Bloom`
- `toggleMotionBlur` → `Canvas/Panel_Settings/Container/Section_Graphics/Row_Toggles/Toggle_MotionBlur`

### Audio Section

**Master Volume:**
- `sliderMasterVolume` → `Canvas/Panel_Settings/Container/Section_Audio/Row_MasterVolume/Slider_MasterVolume`
- `txtMasterVolume` → `Canvas/Panel_Settings/Container/Section_Audio/Row_MasterVolume/Txt_MasterVolume`

### Actions

**Buttons:**
- `btnApply` → `Canvas/Panel_Settings/Container/Row_Actions/Btn_Apply`
- `btnCancel` → `Canvas/Panel_Settings/Container/Row_Actions/Btn_Cancel`

---

## Step 3: Assign SettingsManager References

1. Find `[SettingsManager]` GameObject in scene root
2. Select it to view SettingsManager component in Inspector

**Assign:**
- `mainCamera` -> registry-owned player camera (or leave null for GlobalRegistry player camera resolution)
- `urpVolume` → Find Volume GameObject with post-processing profile (search for "Volume" in Hierarchy)
- `audioMixer` → `Assets/_Project/MasterMixer.mixer` (should already be assigned)

---

## Step 4: Configure Slider Ranges

### Slider_FOV
- Min Value: 60
- Max Value: 110
- Whole Numbers: false
- Value: 75

### Slider_ShadowDistance
- Min Value: 50
- Max Value: 300
- Whole Numbers: false
- Value: 200

### Slider_MasterVolume
- Min Value: 0
- Max Value: 1
- Whole Numbers: false
- Value: 0.8

---

## Step 5: Configure Text Labels

### Initial Text Values
- `Txt_FOV`: "75°"
- `Txt_ShadowDistance`: "200m"
- `Txt_MasterVolume`: "80%"

### Text Alignment
- All value labels (Txt_*): Right-aligned (TextAlignmentOptions.MidlineRight)
- All section labels (Label_*): Left-aligned (TextAlignmentOptions.MidlineLeft)

---

## Step 6: Configure Toggle Labels

Each toggle needs a Label child GameObject with TextMeshProUGUI:

### Toggle_Vsync
- Create child: `Label` (TextMeshProUGUI)
- Text: "V-Sync"
- Position: Right of checkmark

### Toggle_Fullscreen
- Create child: `Label` (TextMeshProUGUI)
- Text: "Fullscreen"
- Position: Right of checkmark

### Toggle_AO
- Create child: `Label` (TextMeshProUGUI)
- Text: "Ambient Occlusion"
- Position: Right of checkmark

### Toggle_Bloom
- Create child: `Label` (TextMeshProUGUI)
- Text: "Bloom"
- Position: Right of checkmark

### Toggle_MotionBlur
- Create child: `Label` (TextMeshProUGUI)
- Text: "Motion Blur"
- Position: Right of checkmark

---

## Step 7: Configure Button Text

Each button needs a Text child GameObject with TextMeshProUGUI:

### Preset Buttons
- `Btn_PresetLow/Text`: "LOW"
- `Btn_PresetMedium/Text`: "MEDIUM"
- `Btn_PresetHigh/Text`: "HIGH"
- `Btn_PresetUltra/Text`: "ULTRA"

### Action Buttons
- `Btn_Apply/Text`: "APPLY"
- `Btn_Cancel/Text`: "CANCEL"

---

## Step 8: Configure Button Colors

### Preset Buttons (Image component)
- `Btn_PresetLow`: Color (0.3, 0.3, 0.3, 1) — Dark gray
- `Btn_PresetMedium`: Color (0.4, 0.4, 0.4, 1) — Medium gray
- `Btn_PresetHigh`: Color (0.5, 0.5, 0.5, 1) — Light gray
- `Btn_PresetUltra`: Color (0.6, 0.6, 0.6, 1) — Bright gray

### Action Buttons (Image component)
- `Btn_Apply`: Color (0.2, 0.6, 0.2, 1) — Green
- `Btn_Cancel`: Color (0.3, 0.3, 0.3, 1) — Gray

---

## Step 9: Configure Layout

### Section_Presets (VerticalLayoutGroup)
- Padding: Left 20, Right 20, Top 10, Bottom 10
- Spacing: 10
- Child Control Width: true
- Child Control Height: true
- Child Force Expand Width: true
- Child Force Expand Height: false

### Section_Graphics (VerticalLayoutGroup)
- Same as Section_Presets

### Section_Audio (VerticalLayoutGroup)
- Same as Section_Presets

### Row_Presets (HorizontalLayoutGroup)
- Spacing: 10
- Child Control Width: true
- Child Control Height: true
- Child Force Expand Width: false
- Child Force Expand Height: true

### Row_FOV / Row_ShadowDistance / Row_MasterVolume (HorizontalLayoutGroup)
- Spacing: 15
- Child Control Width: true
- Child Control Height: true
- Child Force Expand Width: false
- Child Force Expand Height: true

### Row_Toggles (HorizontalLayoutGroup)
- Spacing: 20
- Child Control Width: false
- Child Control Height: true
- Child Force Expand Width: false
- Child Force Expand Height: true

### Row_Actions (HorizontalLayoutGroup)
- Spacing: 20
- Child Control Width: true
- Child Control Height: true
- Child Force Expand Width: false
- Child Force Expand Height: true

---

## Step 10: Test in Play Mode

1. Enter Play Mode
2. Open Settings Panel (via main menu or pause menu)
3. Test each control:
   - Click preset buttons (Low/Medium/High/Ultra) — verify all settings change
   - Drag FOV slider — verify camera FOV changes in real-time
   - Drag Shadow Distance slider — verify QualitySettings.shadowDistance changes
   - Toggle V-Sync — verify QualitySettings.vSyncCount changes
   - Toggle Fullscreen — verify Screen.fullScreen changes
   - Toggle AO/Bloom/Motion Blur — verify URP Volume effects toggle
   - Drag Master Volume slider — verify audio mixer volume changes
   - Click Apply — verify settings persist
   - Click Cancel — verify settings revert
4. Exit Play Mode, reload scene, verify settings persisted

---

## Known Issues

### MCP Unity Limitations
- Scene modifications during Play Mode don't persist
- Batch operations sometimes fail to save properly
- Component property assignment via MCP requires Edit Mode

### Workarounds Applied
- All UI creation done in Edit Mode
- Scene saved after each major batch operation
- Manual Inspector wiring required (documented in this guide)

---

## Optional Enhancements (Not Implemented)

### Additional Volume Sliders
- Music Volume (copy Row_MasterVolume pattern)
- SFX Volume (copy Row_MasterVolume pattern)
- Ambient Volume (copy Row_MasterVolume pattern)

### Additional Quality Controls
- Shadow Quality buttons (decrease/increase + text label)
- Anti-Aliasing buttons (decrease/increase + text label)
- Texture Quality buttons (decrease/increase + text label)
- Quality Level buttons (decrease/increase + text label)

### Reset Button
- Add `Btn_ResetDefaults` to Row_Actions
- Wire to `btnResetDefaults` field in SettingsPanel
- Calls `SettingsManager.Instance.ResetToDefaults()`

---

## Files Modified

- `Assets/_Project/Scripts/UI/SettingsManager.cs` ✅ (URP Volume + Camera FOV integration)
- `Assets/_Project/Scripts/UI/SettingsPanel.cs` ✅ (expanded UI fields)
- `Assets/_Project/Scenes/01_MAIN_MENU.unity` ⏳ (UI created, wiring pending)
- `Assets/_Project/Scripts/UI/SETTINGS_WIRING_GUIDE.md` ✅ (this file)

---

## Summary

**Backend**: 100% complete. SettingsManager has all functionality (URP Volume, Camera FOV, quality presets, persistence).

**UI Structure**: 90% complete. All GameObjects created (presets, FOV, shadow distance, toggles, audio, actions). Missing: toggle labels, button text children.

**Inspector Wiring**: 0% complete. All UI references must be assigned manually in Unity Editor (see Step 2 above).

**Testing**: 0% complete. Requires Inspector wiring first.

**STATUS**: Ready for manual Inspector assignment in Unity Editor. Follow steps 1-10 above.
# CURRENT STATUS OVERRIDE

The previous version of this document was inaccurate and should not be followed literally.

Verified current state in `Assets/_Project/Scenes/01_MAIN_MENU.unity`:
- `Panel_Settings` hierarchy was rebuilt and serialized `SettingsPanel` references are assigned in-scene
- `[SettingsManager]` has `mainCamera` and `urpVolume` assigned
- a dedicated `[SETTINGS_VOLUME]` scene object exists for Bloom/Motion Blur ownership
- `MainMenuController.btnBackFromSettings` is assigned to a dedicated `Btn_BackFromSettings`

Critical correction:
- manual Inspector wiring is no longer the required path for `01_MAIN_MENU`
- the old guide omitted required `SettingsPanel` fields and missed the `btnBackFromSettings` gate in `MainMenuController`
- `AmbientOcclusion` is still not applied to a live renderer feature owner at runtime; only Bloom and Motion Blur have a concrete scene owner
- `UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion` is a `ScriptableRendererFeature` in this Unity 6000 project, so `VolumeProfile.TryGet(...)` is not the correct AO owner path
- `SettingsComparisonView` now reads the persisted graphics preset instead of inferring from `QualityLevel`

Treat the rest of this file as historical notes unless revalidated.
