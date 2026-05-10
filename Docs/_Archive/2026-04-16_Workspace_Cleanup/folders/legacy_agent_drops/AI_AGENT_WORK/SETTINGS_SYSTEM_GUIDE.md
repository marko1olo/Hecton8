Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Settings System Guide — HECTON-8

**VERSION:** 1.0  
**DATE:** 2026-04-14  
**STATUS:** Production-Ready

---

## TABLE OF CONTENTS

1. [Architecture Overview](#architecture-overview)
2. [Component Responsibilities](#component-responsibilities)
3. [Adding New Settings](#adding-new-settings)
4. [Localization Integration](#localization-integration)
5. [Performance Considerations](#performance-considerations)
6. [Testing Guidelines](#testing-guidelines)
7. [API Reference](#api-reference)

---

## ARCHITECTURE OVERVIEW

### System Design

The Settings System follows a **singleton ownership pattern** with **zero-GC hot paths** and **persistent storage** via PlayerPrefs backend.

```
┌─────────────────────────────────────────────────────────────┐
│                     SETTINGS SYSTEM                          │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────────┐         ┌──────────────────┐          │
│  │ SettingsManager  │◄────────│  SettingsPanel   │          │
│  │   (Singleton)    │         │   (UI Layer)     │          │
│  └────────┬─────────┘         └──────────────────┘          │
│           │                                                   │
│           ├──► UserOptionsPersistence (PlayerPrefs)          │
│           ├──► Camera (FOV)                                  │
│           ├──► URP Volume (Post-Processing)                  │
│           └──► AudioMixer (Volume)                           │
│                                                               │
│  ┌──────────────────┐         ┌──────────────────┐          │
│  │SettingsLivePreview│        │SettingsComparison│          │
│  │   (Real-time)    │         │   (FPS Estimates)│          │
│  └──────────────────┘         └──────────────────┘          │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

### Key Principles

1. **Singleton Ownership**: SettingsManager is the single source of truth for all settings
2. **Zero-GC Hot Paths**: All UI updates use dirty flags and cached references
3. **Persistent Storage**: Settings persist via UserOptionsPersistence (PlayerPrefs backend)
4. **Error Resilience**: Graceful degradation when components unavailable (Camera, Volume, AudioMixer)
5. **Live Preview**: Real-time feedback for FOV and post-processing changes
6. **Localization**: All UI text uses LocalizationKeys

---

## COMPONENT RESPONSIBILITIES

### SettingsManager (Singleton)

**Location:** `Assets/_Project/Scripts/UI/SettingsManager.cs`  
**Lifecycle:** DontDestroyOnLoad  
**Execution Order:** -30990 (early initialization)

**Responsibilities:**
- Own all user settings (graphics, audio, video)
- Persist settings via UserOptionsPersistence
- Apply settings to Unity systems (Camera, Volume, AudioMixer)
- Provide public API for reading/writing settings
- Handle errors gracefully (missing components)

**Key Methods:**
```csharp
// Graphics
int QualityLevel { get; set; }
bool Vsync { get; set; }
bool Fullscreen { get; set; }
float FieldOfView { get; set; }
int ShadowQuality { get; set; }
float ShadowDistance { get; set; }
int AntiAliasing { get; set; }
bool AmbientOcclusion { get; set; }
bool Bloom { get; set; }
bool MotionBlur { get; set; }
int TextureQuality { get; set; }

// Audio
float MasterVolume { get; set; }
float MusicVolume { get; set; }
float SfxVolume { get; set; }
float AmbientVolume { get; set; }

// Utility
void ResetToDefaults()
void ApplyQualityPreset(int preset) // 0=Low, 1=Medium, 2=High, 3=Ultra
bool ApplyAllSettings() // Returns true if all settings applied successfully
```

**Dependencies:**
- UserOptionsPersistence (PlayerPrefs backend)
- Camera (for FOV)
- URP Volume (for post-processing)
- AudioMixer (for volume)

---

### SettingsPanel (UI Layer)

**Location:** `Assets/_Project/Scripts/UI/SettingsPanel.cs`  
**Lifecycle:** Scene-bound (01_MAIN_MENU)

**Responsibilities:**
- Display current settings in UI
- Handle user input (buttons, sliders, toggles)
- Cache pending changes (not applied until "Apply" clicked)
- Trigger live preview for FOV and post-processing
- Show error modal on apply failure
- Integrate with SettingsPanelAnimator and SettingsComparisonView

**Key Methods:**
```csharp
// Lifecycle
private void OnEnable() // Load settings, refresh UI, play animation
private void OnDisable() // Unbind sliders, hide comparison view

// Callbacks
private void OnPresetLow/Medium/High/Ultra() // Apply quality preset
private void OnApply() // Apply all cached settings
private void OnCancel() // Revert to current settings
private void OnResetDefaults() // Reset to default values
```

**UI Elements:**
- 4 preset buttons (Low, Medium, High, Ultra)
- Quality level buttons (decrease/increase)
- Sliders (FOV, Shadow Distance, Master Volume, Music, SFX, Ambient)
- Toggles (VSync, Fullscreen, AO, Bloom, Motion Blur)
- Buttons (Apply, Cancel, Reset Defaults)

---

### SettingsLivePreview (Real-Time Preview)

**Location:** `Assets/_Project/Scripts/UI/SettingsLivePreview.cs`  
**Lifecycle:** Scene-bound (attached to SettingsPanel)

**Responsibilities:**
- Preview FOV changes in real-time (debounced 0.05s)
- Preview post-processing changes in real-time (AO, Bloom, Motion Blur)
- Apply changes immediately on "Apply" button
- Cancel pending changes on "Cancel" button

**Key Methods:**
```csharp
void PreviewFOV(float fov) // Preview FOV change (debounced)
void PreviewPostProcessing(bool ao, bool bloom, bool motionBlur) // Preview PP changes
void ApplyImmediately() // Apply all pending previews
void CancelPending() // Revert all pending previews
```

**Performance:**
- Zero-GC (ITickable state machine)
- Debouncing (0.05s) to avoid spam
- Cached Camera and Volume references

---

### SettingsComparisonView (FPS Estimates)

**Location:** `Assets/_Project/Scripts/UI/SettingsComparisonView.cs`  
**Lifecycle:** Scene-bound (attached to SettingsPanel)

**Responsibilities:**
- Show current FPS estimate based on quality level
- Show estimated FPS after quality change
- Show performance impact ("+10 FPS (Better)" or "-15 FPS (Worse)")
- Update every 0.5s (throttled)

**Key Methods:**
```csharp
void UpdateComparison(int pendingQualityLevel) // Update FPS estimates
void Show() // Show comparison panel
void Hide() // Hide comparison panel
```

**FPS Estimates:**
- Low: 60 FPS
- Medium: 50 FPS
- High: 40 FPS
- Ultra: 30 FPS

**Performance:**
- Zero-GC (ITickable, dirty flags)
- Throttled updates (0.5s interval)

---

### SettingsPanelAnimator (Fade-In Animations)

**Location:** `Assets/_Project/Scripts/UI/SettingsPanelAnimator.cs`  
**Lifecycle:** Scene-bound (attached to SettingsPanel)

**Responsibilities:**
- Staggered fade-in animation for settings panel elements
- Header → Preset buttons → Settings rows → Action buttons
- Zero-GC (ITickable state machine, CanvasGroup alpha)

**Key Methods:**
```csharp
void PlayFadeIn() // Start fade-in animation
void SkipAnimation() // Skip animation, show all immediately
```

**Timing:**
- Header: 0s delay, 0.15s duration
- Presets: 0.15s delay, 0.2s duration, 0.05s stagger
- Settings: 0.35s delay, 0.25s duration, 0.08s stagger
- Actions: 0.6s delay, 0.3s duration
- **Total:** < 1s

**Performance:**
- Zero-GC (no coroutines, no DOTween)
- No frame drops (CanvasGroup alpha only)

---

## ADDING NEW SETTINGS

### Step 1: Add Property to SettingsManager

```csharp
// In SettingsManager.cs

// 1. Add PlayerPrefs key constant
private const string MySettingKey = "Hecton_MySetting";

// 2. Add cached field
private bool _cachedMySetting;

// 3. Add public property
public bool MySetting
{
    get => _cachedMySetting;
    set
    {
        if (_cachedMySetting == value)
            return;

        _cachedMySetting = value;
        ApplyMySetting(value); // Apply to Unity system
        SaveBool(MySettingKey, value); // Persist
    }
}

// 4. Add apply method
private void ApplyMySetting(bool value)
{
    // Apply to Unity system (e.g., QualitySettings, Camera, etc.)
    // Example: QualitySettings.someSetting = value;
}

// 5. Add to LoadAllSettings()
private void LoadAllSettings()
{
    // ... existing settings ...
    _cachedMySetting = LoadBool(MySettingKey, false); // default value
}

// 6. Add to ApplyAllSettings()
public bool ApplyAllSettings()
{
    // ... existing settings ...
    
    try
    {
        ApplyMySetting(_cachedMySetting);
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"[SettingsManager] Failed to apply MySetting: {ex.Message}");
        success = false;
    }
    
    return success;
}

// 7. Add to ResetToDefaults()
public void ResetToDefaults()
{
    // ... existing settings ...
    MySetting = false; // default value
}
```

### Step 2: Add UI Element to SettingsPanel

```csharp
// In SettingsPanel.cs

// 1. Add Inspector field
[Header("=== MY SETTINGS ===")]
[SerializeField] private Toggle toggleMySetting;

// 2. Add cached field
private bool _cachedMySetting;

// 3. Bind in Awake/BindButtons
private void BindButtons()
{
    // ... existing bindings ...
    
    if (toggleMySetting != null)
    {
        toggleMySetting.onValueChanged.RemoveAllListeners();
        toggleMySetting.onValueChanged.AddListener(OnMySettingChanged);
    }
}

// 4. Add callback
private void OnMySettingChanged(bool value)
{
    _cachedMySetting = value;
}

// 5. Load in LoadCurrentSettings
private void LoadCurrentSettings()
{
    // ... existing settings ...
    _cachedMySetting = _settings.MySetting;
}

// 6. Refresh in RefreshAllUI
private void RefreshAllUI()
{
    // ... existing refreshes ...
    if (toggleMySetting != null)
        toggleMySetting.SetIsOnWithoutNotify(_cachedMySetting);
}

// 7. Apply in OnApply
private void OnApply()
{
    // ... existing applies ...
    _settings.MySetting = _cachedMySetting;
    
    bool success = _settings.ApplyAllSettings();
    // ... error handling ...
}
```

### Step 3: Add Localization Keys

```csharp
// In LocalizationKeys.cs
public const string SETTINGS_MY_SETTING = "settings.my_setting";
```

```json
// In localization data files (English)
{
  "settings.my_setting": "My Setting"
}

// Russian
{
  "settings.my_setting": "Moya Nastroyka"
}
```

### Step 4: Wire in Unity Editor

1. Open scene `01_MAIN_MENU`
2. Find `Panel_Settings` GameObject
3. Add Toggle UI element for new setting
4. Assign Toggle to `toggleMySetting` field in SettingsPanel Inspector
5. Test in Play Mode

---

## LOCALIZATION INTEGRATION

### Using Localization in Settings UI

All settings UI text must use LocalizationKeys:

```csharp
// Get localized text
LocalizationManager loc = LocalizationManager.Instance;
string text = loc != null ? loc.Get(LocalizationKeys.SETTINGS_GRAPHICS) : "GRAPHICS";

// Set TMP_Text with localized key
txtLabel.SetText(loc.Get(LocalizationKeys.SETTINGS_FOV));
```

### Available Settings Keys

```csharp
// Headers
SETTINGS_GRAPHICS = "settings.graphics"
SETTINGS_AUDIO = "settings.audio"
SETTINGS_QUALITY_PRESET = "settings.quality_preset"

// Presets
SETTINGS_PRESET_LOW = "settings.preset.low"
SETTINGS_PRESET_MEDIUM = "settings.preset.medium"
SETTINGS_PRESET_HIGH = "settings.preset.high"
SETTINGS_PRESET_ULTRA = "settings.preset.ultra"

// Graphics
SETTINGS_FOV = "settings.fov"
SETTINGS_SHADOW_DISTANCE = "settings.shadow_distance"
SETTINGS_VSYNC = "settings.vsync"
SETTINGS_FULLSCREEN = "settings.fullscreen"
SETTINGS_AO = "settings.ambient_occlusion"
SETTINGS_BLOOM = "settings.bloom"
SETTINGS_MOTION_BLUR = "settings.motion_blur"

// Audio
SETTINGS_MASTER_VOLUME = "settings.master_volume"

// Actions
SETTINGS_APPLY = "settings.apply"
SETTINGS_CANCEL = "settings.cancel"

// Errors
ERROR_SETTINGS_APPLY_FAILED = "error.settings_apply_failed"
ERROR_SETTINGS_UNAVAILABLE = "error.settings_unavailable"
```

### Language Change Handling

Settings UI automatically updates when language changes:

```csharp
// In SettingsPanel.cs
private void OnEnable()
{
    LocalizationManager.OnLanguageChanged += OnLanguageChanged;
}

private void OnDisable()
{
    LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
}

private void OnLanguageChanged(GameLanguage newLanguage)
{
    RefreshAllUI(); // Re-apply localized text
}
```

---

## PERFORMANCE CONSIDERATIONS

### Zero-GC Hot Paths

**RULE:** No GC allocations in Tick/Update/OnEnable/OnDisable.

**Techniques:**
1. **Dirty Flags**: Only update UI when value changes
2. **Cached References**: Cache all components in Awake
3. **ITickable**: Use GameTickManager instead of Update()
4. **CanvasGroup Alpha**: Use alpha for show/hide (not SetActive)
5. **SetValueWithoutNotify**: Avoid triggering callbacks when setting UI values
6. **String Caching**: Pre-allocate static readonly strings

**Example:**
```csharp
// BAD: Allocates string every frame
txtValue.SetText($"{value}%");

// GOOD: Only update when value changes
if (_cachedValue != value)
{
    _cachedValue = value;
    txtValue.SetText($"{Mathf.RoundToInt(value * 100f)}%");
}
```

### Profiling Checklist

Before shipping, verify:
- [ ] Zero GC allocations on panel open/close (Profiler)
- [ ] Settings apply completes in < 50ms (Profiler)
- [ ] No frame drops during panel transitions (Profiler)
- [ ] No allocation spikes during slider drag (Profiler)
- [ ] Memory stable after 10 minutes idle (Profiler)

### Performance Budgets

| Operation | Budget | Notes |
|-----------|--------|-------|
| Panel open/close | 0 B GC | Use CanvasGroup alpha |
| Settings apply | < 50ms | Batch PlayerPrefs writes |
| Slider drag | 0 B GC | Dirty flags, throttling |
| Toggle change | 0 B GC | Debouncing (0.05s) |
| Live preview | 0 B GC | ITickable state machine |

---

## TESTING GUIDELINES

### Unit Testing

Test each setting property:

```csharp
[Test]
public void SettingsManager_QualityLevel_PersistsCorrectly()
{
    // Arrange
    SettingsManager settings = SettingsManager.Instance;
    int expectedQuality = 2;
    
    // Act
    settings.QualityLevel = expectedQuality;
    
    // Assert
    Assert.AreEqual(expectedQuality, settings.QualityLevel);
    Assert.AreEqual(expectedQuality, QualitySettings.GetQualityLevel());
}
```

### Integration Testing

Test full settings flow:

1. Open Settings panel
2. Change quality preset to Low
3. Verify all settings update (FOV, shadows, etc.)
4. Click Apply
5. Reload scene
6. Verify settings persist

### Error Testing

Test error handling:

1. Disconnect Camera reference
2. Open Settings panel
3. Change FOV
4. Click Apply
5. Verify error modal shows "FOV setting unavailable"
6. Verify other settings still apply correctly

### Performance Testing

Test zero-GC compliance:

1. Open Unity Profiler
2. Open Settings panel
3. Drag FOV slider rapidly
4. Verify zero GC allocations
5. Click Apply
6. Verify < 50ms frame time

---

## API REFERENCE

### SettingsManager

```csharp
// Singleton Access
SettingsManager.Instance // Get singleton instance
SettingsManager.TryGetInstance(out SettingsManager instance) // Safe access

// Graphics Properties
int QualityLevel { get; set; } // 0-N (Unity quality levels)
bool Vsync { get; set; } // VSync on/off
bool Fullscreen { get; set; } // Fullscreen on/off
float FieldOfView { get; set; } // 60-110 degrees
int ShadowQuality { get; set; } // 0=Off, 1=Low, 2=Medium, 3=High
float ShadowDistance { get; set; } // 50-300 meters
int AntiAliasing { get; set; } // 0=None, 1=FXAA, 2=SMAA, 3=TAA
bool AmbientOcclusion { get; set; } // AO on/off
bool Bloom { get; set; } // Bloom on/off
bool MotionBlur { get; set; } // Motion Blur on/off
int TextureQuality { get; set; } // 0=Low, 1=Medium, 2=High, 3=Ultra

// Audio Properties
float MasterVolume { get; set; } // 0.0-1.0
float MusicVolume { get; set; } // 0.0-1.0
float SfxVolume { get; set; } // 0.0-1.0
float AmbientVolume { get; set; } // 0.0-1.0

// Utility Methods
void ResetToDefaults() // Reset all settings to defaults
void ApplyQualityPreset(int preset) // Apply quality preset (0=Low, 1=Medium, 2=High, 3=Ultra)
bool ApplyAllSettings() // Apply all cached settings, returns true if all succeeded
void SetResolution(int width, int height) // Set screen resolution
void GetResolution(out int width, out int height) // Get current resolution
```

### SettingsPanel

```csharp
// Lifecycle (automatic)
private void OnEnable() // Load settings, refresh UI, play animation
private void OnDisable() // Unbind sliders, hide comparison view

// Public API (none - internal only)
```

### SettingsLivePreview

```csharp
// Preview Methods
void PreviewFOV(float fov) // Preview FOV change (debounced 0.05s)
void PreviewPostProcessing(bool ao, bool bloom, bool motionBlur) // Preview PP changes

// Apply/Cancel
void ApplyImmediately() // Apply all pending previews
void CancelPending() // Revert all pending previews
```

### SettingsComparisonView

```csharp
// Update Methods
void UpdateComparison(int pendingQualityLevel) // Update FPS estimates

// Show/Hide
void Show() // Show comparison panel
void Hide() // Hide comparison panel
```

### SettingsPanelAnimator

```csharp
// Animation Control
void PlayFadeIn() // Start fade-in animation
void SkipAnimation() // Skip animation, show all immediately
```

---

## BEST PRACTICES

### DO:
✅ Use SettingsManager.Instance for all settings access  
✅ Cache all component references in Awake  
✅ Use dirty flags for UI updates  
✅ Use ITickable instead of Update()  
✅ Use CanvasGroup alpha for show/hide  
✅ Use SetValueWithoutNotify when setting UI values  
✅ Test with Profiler (zero GC, < 50ms apply)  
✅ Handle errors gracefully (missing components)  
✅ Use LocalizationKeys for all UI text  

### DON'T:
❌ Access settings directly (use SettingsManager.Instance)  
❌ Use SetActive for panel transitions (use CanvasGroup alpha)  
❌ Update UI every frame (use dirty flags)  
❌ Use Update() (use ITickable)  
❌ Allocate strings in hot paths (cache or dirty flag)  
❌ Use LINQ in hot paths (zero-GC rule)  
❌ Hardcode English strings (use LocalizationKeys)  
❌ Ignore error states (handle gracefully)  

---

## TROUBLESHOOTING

See `SETTINGS_TROUBLESHOOTING.md` for common issues and solutions.

---

## VERSION HISTORY

**v1.0 (2026-04-14):**
- Initial release
- Complete settings system (graphics, audio, video)
- Zero-GC compliance
- Live preview
- FPS comparison view
- Error handling
- Localization support

---

**END OF GUIDE**
