**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Shell/UX Production System — Wiring Instructions

**STATUS:** Code 100% Complete — Needs Inspector Wiring Only  
**ESTIMATED TIME:** 30-45 minutes manual work in Unity Editor  
**PRIORITY:** HIGH — Required for system functionality

---

## OVERVIEW

All C# code is complete and production-ready. This document contains step-by-step Inspector wiring instructions for the coder to execute in Unity Editor.

**Systems to Wire:**
1. SettingsManager (singleton, DontDestroyOnLoad)
2. SettingsPanel (main settings UI)
3. SettingsPanelAnimator (fade-in animations)
4. SettingsComparisonView (FPS estimates)
5. SettingsLivePreview (real-time preview)
6. SaveSlotHoverPreview (enlarged thumbnails on hover)
7. UIScreenShake (destructive action feedback)
8. UIParticleEffect (button click particles)
9. SaveThumbnailCapture (automatic screenshot on save)
10. UIAudioFeedback (automatic audio for all UI)

---

## SECTION 1: SettingsManager (Singleton)

**Location:** Scene `01_MAIN_MENU` or create new GameObject in DontDestroyOnLoad

### Steps:
1. Find or create GameObject `[SettingsManager]` in hierarchy
2. Add component `SettingsManager` if not present
3. Assign references:

| Field | Value | Notes |
|-------|-------|-------|
| `audioMixer` | `Assets/_Project/Audio/MasterMixer.mixer` | Main audio mixer asset |
| `mainCamera` | `Camera.main` or `HUD_Render_Camera` | Main camera for FOV application |
| `urpVolume` | Find Volume component in scene | URP Volume for post-processing (AO/Bloom/Motion Blur) |

### Verification:
- Play Mode → Check Console for "SettingsManager initialized" (no errors)
- Test: Change FOV slider → verify camera.fieldOfView updates
- Test: Toggle Bloom → verify URP Volume effect changes

---

## SECTION 2: SettingsPanel (Main Settings UI)

**Location:** Scene `01_MAIN_MENU` → GameObject `Panel_Settings`

### Steps:
1. Find GameObject `Panel_Settings` in hierarchy
2. Verify component `SettingsPanel` is attached
3. Assign ALL UI element references:

### Graphics Section:
| Field | GameObject Path | Component Type |
|-------|----------------|----------------|
| `btnPresetLow` | `Panel_Settings/Container/Presets/Btn_Preset_Low` | Button |
| `btnPresetMedium` | `Panel_Settings/Container/Presets/Btn_Preset_Medium` | Button |
| `btnPresetHigh` | `Panel_Settings/Container/Presets/Btn_Preset_High` | Button |
| `btnPresetUltra` | `Panel_Settings/Container/Presets/Btn_Preset_Ultra` | Button |
| `btnQualityDecrease` | `Panel_Settings/Container/Quality/Btn_Decrease` | Button |
| `btnQualityIncrease` | `Panel_Settings/Container/Quality/Btn_Increase` | Button |
| `txtQualityLevel` | `Panel_Settings/Container/Quality/Txt_Value` | TMP_Text |
| `toggleVsync` | `Panel_Settings/Container/Video/Toggle_Vsync` | Toggle |
| `toggleFullscreen` | `Panel_Settings/Container/Video/Toggle_Fullscreen` | Toggle |
| `sliderFieldOfView` | `Panel_Settings/Container/Graphics/Slider_FOV` | Slider |
| `txtFieldOfView` | `Panel_Settings/Container/Graphics/Slider_FOV/Txt_Value` | TMP_Text |
| `btnShadowQualityDecrease` | `Panel_Settings/Container/Graphics/Shadow_Quality/Btn_Decrease` | Button |
| `btnShadowQualityIncrease` | `Panel_Settings/Container/Graphics/Shadow_Quality/Btn_Increase` | Button |
| `txtShadowQuality` | `Panel_Settings/Container/Graphics/Shadow_Quality/Txt_Value` | TMP_Text |
| `sliderShadowDistance` | `Panel_Settings/Container/Graphics/Slider_Shadow_Distance` | Slider |
| `txtShadowDistance` | `Panel_Settings/Container/Graphics/Slider_Shadow_Distance/Txt_Value` | TMP_Text |
| `btnAntiAliasingDecrease` | `Panel_Settings/Container/Graphics/Anti_Aliasing/Btn_Decrease` | Button |
| `btnAntiAliasingIncrease` | `Panel_Settings/Container/Graphics/Anti_Aliasing/Btn_Increase` | Button |
| `txtAntiAliasing` | `Panel_Settings/Container/Graphics/Anti_Aliasing/Txt_Value` | TMP_Text |
| `toggleAmbientOcclusion` | `Panel_Settings/Container/Graphics/Toggle_AO` | Toggle |
| `toggleBloom` | `Panel_Settings/Container/Graphics/Toggle_Bloom` | Toggle |
| `toggleMotionBlur` | `Panel_Settings/Container/Graphics/Toggle_Motion_Blur` | Toggle |
| `btnTextureQualityDecrease` | `Panel_Settings/Container/Graphics/Texture_Quality/Btn_Decrease` | Button |
| `btnTextureQualityIncrease` | `Panel_Settings/Container/Graphics/Texture_Quality/Btn_Increase` | Button |
| `txtTextureQuality` | `Panel_Settings/Container/Graphics/Texture_Quality/Txt_Value` | TMP_Text |

### Audio Section:
| Field | GameObject Path | Component Type |
|-------|----------------|----------------|
| `sliderMasterVolume` | `Panel_Settings/Container/Audio/Slider_Master_Volume` | Slider |
| `sliderMusicVolume` | `Panel_Settings/Container/Audio/Slider_Music_Volume` | Slider |
| `sliderSfxVolume` | `Panel_Settings/Container/Audio/Slider_Sfx_Volume` | Slider |
| `sliderAmbientVolume` | `Panel_Settings/Container/Audio/Slider_Ambient_Volume` | Slider |
| `txtMasterVolume` | `Panel_Settings/Container/Audio/Slider_Master_Volume/Txt_Value` | TMP_Text |
| `txtMusicVolume` | `Panel_Settings/Container/Audio/Slider_Music_Volume/Txt_Value` | TMP_Text |
| `txtSfxVolume` | `Panel_Settings/Container/Audio/Slider_Sfx_Volume/Txt_Value` | TMP_Text |
| `txtAmbientVolume` | `Panel_Settings/Container/Audio/Slider_Ambient_Volume/Txt_Value` | TMP_Text |

### Actions Section:
| Field | GameObject Path | Component Type |
|-------|----------------|----------------|
| `btnResetDefaults` | `Panel_Settings/Container/Actions/Btn_Reset_Defaults` | Button |
| `btnApply` | `Panel_Settings/Container/Actions/Btn_Apply` | Button |
| `btnCancel` | `Panel_Settings/Container/Actions/Btn_Cancel` | Button |

### Live Preview:
| Field | GameObject Path | Component Type |
|-------|----------------|----------------|
| `livePreview` | `Panel_Settings` (same GameObject) | SettingsLivePreview |

### Animation:
| Field | GameObject Path | Component Type |
|-------|----------------|----------------|
| `panelAnimator` | `Panel_Settings` (same GameObject) | SettingsPanelAnimator |

### Comparison View:
| Field | GameObject Path | Component Type |
|-------|----------------|----------------|
| `comparisonView` | `Panel_Settings` (same GameObject) | SettingsComparisonView |

### Slider Configuration:
Configure slider ranges in Inspector:

| Slider | Min | Max | Default | Whole Numbers |
|--------|-----|-----|---------|---------------|
| `sliderFieldOfView` | 60 | 110 | 75 | Yes |
| `sliderShadowDistance` | 50 | 300 | 200 | Yes |
| `sliderMasterVolume` | 0 | 1 | 0.8 | No |
| `sliderMusicVolume` | 0 | 1 | 0.8 | No |
| `sliderSfxVolume` | 0 | 1 | 0.8 | No |
| `sliderAmbientVolume` | 0 | 1 | 0.8 | No |

### Verification:
- Play Mode → Open Settings panel
- Test: Click preset buttons → verify all settings update
- Test: Drag sliders → verify text labels update
- Test: Toggle switches → verify state changes
- Test: Click Apply → verify settings persist (reload scene)
- Test: Click Cancel → verify settings revert

---

## SECTION 3: SettingsPanelAnimator (Fade-In Animations)

**Location:** Scene `01_MAIN_MENU` → GameObject `Panel_Settings`

### Steps:
1. Add component `SettingsPanelAnimator` to `Panel_Settings` GameObject
2. Create CanvasGroup components for animation groups:
   - Add CanvasGroup to `Panel_Settings/Container/Header`
   - Add CanvasGroup to each preset button
   - Add CanvasGroup to each settings row
   - Add CanvasGroup to `Panel_Settings/Container/Actions`
3. Assign references:

| Field | Value | Notes |
|-------|-------|-------|
| `headerGroup` | CanvasGroup on `Container/Header` | Header text/title |
| `presetButtonGroups` | Array of 4 CanvasGroups | Low, Medium, High, Ultra buttons |
| `settingsRowGroups` | Array of N CanvasGroups | Each settings row (FOV, Shadows, Audio, etc.) |
| `actionButtonsGroup` | CanvasGroup on `Container/Actions` | Apply/Cancel buttons |

### Timing Configuration (Inspector):
| Field | Default Value | Notes |
|-------|---------------|-------|
| `headerDelay` | 0 | Start immediately |
| `headerDuration` | 0.15 | Fast fade-in |
| `presetDelay` | 0.15 | After header |
| `presetDuration` | 0.2 | Smooth fade |
| `presetStagger` | 0.05 | Delay between each button |
| `settingsDelay` | 0.35 | After presets |
| `settingsDuration` | 0.25 | Smooth fade |
| `settingsStagger` | 0.08 | Delay between each row |
| `actionsDelay` | 0.6 | After settings |
| `actionsDuration` | 0.3 | Final fade |

### Verification:
- Play Mode → Open Settings panel
- Verify: Header fades in first
- Verify: Preset buttons fade in sequentially
- Verify: Settings rows fade in sequentially
- Verify: Action buttons fade in last
- Verify: Total animation time < 1 second
- Verify: No GC allocations (Profiler)

---

## SECTION 4: SettingsComparisonView (FPS Estimates)

**Location:** Scene `01_MAIN_MENU` → GameObject `Panel_Settings`

### Steps:
1. Add component `SettingsComparisonView` to `Panel_Settings` GameObject
2. Create comparison panel UI:
   - Create child GameObject `Comparison_Panel` under `Panel_Settings`
   - Add CanvasGroup component to `Comparison_Panel`
   - Create TMP_Text children: `Txt_Current_FPS`, `Txt_Estimated_FPS`, `Txt_Performance_Impact`
3. Assign references:

| Field | Value | Notes |
|-------|-------|-------|
| `comparisonPanel` | CanvasGroup on `Comparison_Panel` | Container for comparison UI |
| `txtCurrentFPS` | TMP_Text `Txt_Current_FPS` | Shows current FPS estimate |
| `txtEstimatedFPS` | TMP_Text `Txt_Estimated_FPS` | Shows estimated FPS after change |
| `txtPerformanceImpact` | TMP_Text `Txt_Performance_Impact` | Shows "+10 FPS (Better)" or "-15 FPS (Worse)" |
| `updateInterval` | 0.5 | Update frequency in seconds |

### UI Layout Suggestion:
```
Comparison_Panel (CanvasGroup, alpha=1)
├── Txt_Current_FPS: "Current: 60 FPS"
├── Txt_Estimated_FPS: "Estimated: 50 FPS"
└── Txt_Performance_Impact: "-10 FPS (Worse)"
```

### Verification:
- Play Mode → Open Settings panel
- Test: Change quality preset → verify FPS estimates update
- Test: Low → Ultra → verify "Worse" impact shown
- Test: Ultra → Low → verify "Better" impact shown
- Verify: No GC allocations (Profiler)

---

## SECTION 5: SettingsLivePreview (Real-Time Preview)

**Location:** Scene `01_MAIN_MENU` → GameObject `Panel_Settings`

### Steps:
1. Add component `SettingsLivePreview` to `Panel_Settings` GameObject
2. Assign references:

| Field | Value | Notes |
|-------|-------|-------|
| `mainCamera` | Camera.main or HUD_Render_Camera | Camera for FOV preview |
| `urpVolume` | Volume component in scene | URP Volume for post-processing preview |
| `debounceDelay` | 0.05 | Delay before applying preview (seconds) |

### Verification:
- Play Mode → Open Settings panel
- Test: Drag FOV slider → verify camera FOV updates in real-time
- Test: Toggle AO → verify effect appears/disappears immediately
- Test: Toggle Bloom → verify effect appears/disappears immediately
- Test: Toggle Motion Blur → verify effect appears/disappears immediately
- Test: Click Cancel → verify all previews revert to original values
- Verify: No GC allocations (Profiler)

---

## SECTION 6: SaveSlotHoverPreview (Enlarged Thumbnails)

**Location:** Scene `01_MAIN_MENU` → Each SaveSlotUI GameObject

### Steps:
1. Find all SaveSlotUI GameObjects in hierarchy (typically 3 slots)
2. For each SaveSlotUI:
   - Add component `SaveSlotHoverPreview`
   - Create preview panel UI (or use shared panel)
3. Create shared preview panel:
   - Create GameObject `SaveSlot_Preview_Panel` under Canvas
   - Add CanvasGroup component
   - Add child GameObject `Preview_Container` (RectTransform for layout)
   - Add child GameObject `Preview_Thumbnail` with SaveSlotThumbnail component
4. Assign references for each SaveSlotUI:

| Field | Value | Notes |
|-------|-------|-------|
| `previewPanel` | CanvasGroup on `SaveSlot_Preview_Panel` | Shared preview panel |
| `previewContainer` | RectTransform on `Preview_Container` | Layout container |
| `previewThumbnail` | SaveSlotThumbnail on `Preview_Thumbnail` | Enlarged thumbnail display |
| `hoverDelay` | 0.3 | Delay before showing preview (seconds) |
| `fadeInDuration` | 0.15 | Fade-in animation duration |
| `fadeOutDuration` | 0.1 | Fade-out animation duration |
| `enlargeScale` | 2.0 | Scale multiplier for enlarged thumbnail |

### UI Layout Suggestion:
```
SaveSlot_Preview_Panel (CanvasGroup, alpha=0, initially hidden)
└── Preview_Container (RectTransform, 640x360)
    └── Preview_Thumbnail (SaveSlotThumbnail, 640x360)
```

### Verification:
- Play Mode → Open Load Game menu
- Test: Hover over save slot → verify preview appears after 0.3s
- Test: Move mouse away → verify preview fades out
- Test: Hover over empty slot → verify no preview shown
- Verify: No GC allocations (Profiler)

---

## SECTION 7: UIScreenShake (Destructive Action Feedback)

**Location:** Scene `01_MAIN_MENU` → Canvas GameObject

### Steps:
1. Find Canvas GameObject in hierarchy
2. Add component `UIScreenShake` to Canvas
3. Assign references:

| Field | Value | Notes |
|-------|-------|-------|
| `canvasRectTransform` | RectTransform on Canvas | Canvas to shake |
| `shakeDuration` | 0.3 | Shake duration in seconds |
| `shakeIntensity` | 10.0 | Shake intensity (pixels) |
| `shakeCurve` | AnimationCurve (default) | Shake falloff curve |

### AnimationCurve Configuration:
- Key 0: Time=0, Value=1 (full intensity at start)
- Key 1: Time=1, Value=0 (zero intensity at end)
- Curve: Ease-out (smooth falloff)

### Integration:
Add shake triggers to destructive buttons:
```csharp
// Example: Quit button
btnQuit.onClick.AddListener(() => {
    UIScreenShake.Instance?.Shake();
    // ... rest of quit logic
});
```

### Verification:
- Play Mode → Main Menu
- Test: Click Quit button → verify screen shakes
- Test: Click Reset Settings → verify screen shakes
- Verify: Shake completes in 0.3s
- Verify: No GC allocations (Profiler)

---

## SECTION 8: UIParticleEffect (Button Click Particles)

**Location:** Scene `01_MAIN_MENU` → Button GameObjects

### Steps:
1. Create particle prefab or use default configuration
2. For each button that should have particles:
   - Add component `UIParticleEffect` to button GameObject
3. Assign references:

| Field | Value | Notes |
|-------|-------|-------|
| `particlePrefab` | Particle prefab (optional) | If null, uses default config |
| `spawnOffset` | Vector3(0, 0, 0) | Offset from button center |
| `particleCount` | 10 | Number of particles per burst |
| `particleLifetime` | 0.5 | Particle lifetime in seconds |
| `particleSpeed` | 100.0 | Particle speed |
| `particleSize` | 5.0 | Particle size |
| `particleColor` | Color.white | Particle color |

### Default Particle Configuration:
If `particlePrefab` is null, component creates default ParticleSystem:
- Shape: Sphere, radius 50
- Emission: Burst of 10 particles
- Lifetime: 0.5s
- Speed: 100
- Size: 5
- Color: White

### Buttons to Add Particles:
- New Game button
- Load Game button
- Settings button
- Quit button
- Apply button (Settings panel)

### Verification:
- Play Mode → Main Menu
- Test: Click each button → verify particle burst appears
- Test: Rapid clicks → verify particles pool correctly (no lag)
- Verify: No GC allocations (Profiler)

---

## SECTION 9: SaveThumbnailCapture (Automatic Screenshots)

**Location:** Scene `02_HECTON_WORLD` → Create new GameObject

### Steps:
1. Open scene `02_HECTON_WORLD`
2. Create new GameObject `[SaveThumbnailCapture]` in hierarchy
3. Add component `SaveThumbnailCapture`
4. Assign references:

| Field | Value | Notes |
|-------|-------|-------|
| `captureCamera` | Camera.main or gameplay camera | Camera to capture from |
| `captureWidth` | 320 | Thumbnail width in pixels |
| `captureHeight` | 180 | Thumbnail height in pixels |
| `captureDelay` | 0.1 | Delay after save event (seconds) |

### Verification:
- Play Mode → Load into 02_HECTON_WORLD
- Test: Save game to slot → verify thumbnail PNG created in save folder
- Test: Load game → verify thumbnail displays in load menu
- Verify: Thumbnail file size < 50 KB
- Verify: No GC allocations during capture (Profiler)

---

## SECTION 10: UIAudioFeedback (Automatic Audio)

**Location:** Scene `01_MAIN_MENU` → Canvas GameObject

### Steps:
1. Find Canvas GameObject in hierarchy
2. Add component `UIAudioFeedback` to Canvas
3. Create audio clips (or use placeholders):
   - `SFX_UI_Click_Primary.wav` (main actions)
   - `SFX_UI_Click_Secondary.wav` (navigation)
   - `SFX_UI_Click_Destructive.wav` (quit/reset)
   - `SFX_UI_Hover.wav` (button hover)
   - `SFX_UI_Slider_Tick.wav` (slider value change)
   - `SFX_UI_Toggle_On.wav` (toggle on)
   - `SFX_UI_Toggle_Off.wav` (toggle off)
   - `SFX_UI_Panel_Open.wav` (panel open)
   - `SFX_UI_Panel_Close.wav` (panel close)
4. Assign references:

| Field | Value | Notes |
|-------|-------|-------|
| `clickPrimary` | AudioClip | Main action buttons (New Game, Resume, Save) |
| `clickSecondary` | AudioClip | Navigation buttons (Back, Settings) |
| `clickDestructive` | AudioClip | Destructive buttons (Quit, Reset) |
| `hover` | AudioClip | Button hover sound |
| `sliderTick` | AudioClip | Slider value change sound |
| `toggleOn` | AudioClip | Toggle on sound |
| `toggleOff` | AudioClip | Toggle off sound |
| `panelOpen` | AudioClip | Panel open sound |
| `panelClose` | AudioClip | Panel close sound |
| `hoverThrottle` | 0.1 | Hover sound throttle (seconds) |
| `sliderThrottle` | 0.1 | Slider sound throttle (seconds) |

### Verification:
- Play Mode → Main Menu
- Test: Hover over buttons → verify hover sound plays (throttled)
- Test: Click buttons → verify click sounds play (different for primary/secondary/destructive)
- Test: Drag sliders → verify tick sound plays (throttled)
- Test: Toggle switches → verify on/off sounds play
- Test: Open/close panels → verify panel sounds play
- Verify: No audio spam on rapid interactions
- Verify: No GC allocations (Profiler)

---

## SECTION 11: Integration Testing Checklist

After completing all wiring, perform full integration testing:

### Test 1: Settings Panel Flow
- [ ] Open Settings panel → verify fade-in animation
- [ ] Change quality preset → verify all settings update
- [ ] Verify comparison view shows FPS estimates
- [ ] Verify live preview updates camera FOV
- [ ] Verify live preview updates post-processing effects
- [ ] Click Apply → verify settings persist
- [ ] Reload scene → verify settings retained
- [ ] Click Cancel → verify settings revert

### Test 2: Save/Load Flow
- [ ] Save game → verify thumbnail captured
- [ ] Open Load Game menu → verify thumbnails display
- [ ] Hover over save slot → verify enlarged preview appears
- [ ] Click save slot → verify game loads correctly

### Test 3: Audio Feedback
- [ ] Hover over buttons → verify hover sound
- [ ] Click buttons → verify click sounds (primary/secondary/destructive)
- [ ] Drag sliders → verify tick sounds
- [ ] Toggle switches → verify on/off sounds
- [ ] Open/close panels → verify panel sounds

### Test 4: Visual Feedback
- [ ] Click Quit button → verify screen shake
- [ ] Click Reset Settings → verify screen shake
- [ ] Click buttons → verify particle bursts

### Test 5: Performance
- [ ] Open Profiler → verify zero GC allocations in hot paths
- [ ] Measure frame time during settings apply → verify < 50ms
- [ ] Measure frame time during panel open/close → verify no drops
- [ ] Measure memory usage → verify no leaks

### Test 6: Error Handling
- [ ] Disconnect URP Volume → verify error modal on settings apply
- [ ] Disconnect Camera → verify error modal on FOV change
- [ ] Disconnect AudioMixer → verify error modal on volume change
- [ ] Test settings apply failure → verify Retry/Revert options

---

## SECTION 12: Common Issues and Solutions

### Issue: Settings don't persist after scene reload
**Solution:** Verify SettingsManager is DontDestroyOnLoad and UserOptionsPersistence is saving correctly.

### Issue: URP Volume effects don't apply
**Solution:** Verify urpVolume reference is assigned and Volume component has a VolumeProfile with Bloom/Motion Blur overrides.

### Issue: Camera FOV doesn't update
**Solution:** Verify mainCamera reference is assigned and camera is active in scene.

### Issue: Audio doesn't play
**Solution:** Verify audioMixer reference is assigned and AudioMixer has exposed parameters (MasterVolume, MusicVolume, SfxVolume, AmbientVolume).

### Issue: Thumbnails don't display
**Solution:** Verify SaveThumbnailCapture is in 02_HECTON_WORLD scene and captureCamera is assigned.

### Issue: Animations don't play
**Solution:** Verify CanvasGroup components are added to all animation groups and references are assigned.

### Issue: GC allocations detected
**Solution:** Check for string concatenation in hot paths, LINQ usage, or missing cached references.

---

## SECTION 13: Final Verification

After completing all wiring and testing:

1. **Code Review:**
   - [ ] All Inspector references assigned (no null warnings)
   - [ ] All slider ranges configured correctly
   - [ ] All audio clips assigned (or placeholders)
   - [ ] All CanvasGroups added for animations

2. **Functionality:**
   - [ ] Settings apply correctly
   - [ ] Settings persist across scene reloads
   - [ ] Live preview works for FOV and post-processing
   - [ ] Thumbnails capture and display correctly
   - [ ] Audio feedback plays for all interactions
   - [ ] Visual feedback (shake, particles) works

3. **Performance:**
   - [ ] Zero GC allocations in hot paths (Profiler)
   - [ ] Settings apply completes in < 50ms
   - [ ] No frame drops during panel transitions
   - [ ] No memory leaks (Profiler)

4. **Error Handling:**
   - [ ] Missing references show error modals (not crashes)
   - [ ] Settings apply failures show Retry/Revert options
   - [ ] All error messages are user-friendly

5. **Polish:**
   - [ ] Animations are smooth and fast (< 1s total)
   - [ ] Audio is not spammy (throttled correctly)
   - [ ] Visual effects are subtle and professional
   - [ ] UI layout is clean and readable

---

## COMPLETION CHECKLIST

- [ ] Section 1: SettingsManager wired
- [ ] Section 2: SettingsPanel wired
- [ ] Section 3: SettingsPanelAnimator wired
- [ ] Section 4: SettingsComparisonView wired
- [ ] Section 5: SettingsLivePreview wired
- [ ] Section 6: SaveSlotHoverPreview wired
- [ ] Section 7: UIScreenShake wired
- [ ] Section 8: UIParticleEffect wired
- [ ] Section 9: SaveThumbnailCapture wired
- [ ] Section 10: UIAudioFeedback wired
- [ ] Section 11: Integration testing complete
- [ ] Section 12: Common issues reviewed
- [ ] Section 13: Final verification complete

**ESTIMATED TIME:** 30-45 minutes  
**STATUS:** Ready for coder execution

---

## NOTES FOR CODER

- All C# code is production-ready and follows AGENTS.md rules (zero GC, ITickable, no Update(), etc.)
- This document contains ONLY Inspector wiring instructions — no code changes needed
- If any GameObject paths don't match your scene, adjust paths accordingly
- If any audio clips are missing, use placeholder clips or silence (system will work without audio)
- If any UI elements are missing, create them following the layout suggestions
- Test each section individually before moving to the next
- Use Unity Profiler to verify zero GC allocations after wiring
- Report any issues or missing references immediately

**PRIORITY ORDER:**
1. Section 1 (SettingsManager) — CRITICAL
2. Section 2 (SettingsPanel) — CRITICAL
3. Section 5 (SettingsLivePreview) — HIGH
4. Section 9 (SaveThumbnailCapture) — HIGH
5. Sections 3, 4, 6, 7, 8, 10 — MEDIUM (polish)

Good luck! 🚀
