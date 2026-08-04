using System;
using TMPro;
using Hecton8.Core;
using Hecton8.Input;
using Hecton8.Modding;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Hecton.Localization;

namespace Hecton8.UI
{
    /// <summary>
    /// Settings panel UI for pause menu.
    /// Exposes graphics, audio, and video options via SettingsManager.
    /// Zero-GC: uses dirty flags for text updates, cached delegates.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Settings Panel")]
    public sealed class SettingsPanel : MonoBehaviour, ILocalizationLanguageChangedListener
    {
        private static ILocalizationTextReadModel s_localization;
        private static readonly int SettingsPresetsKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_PRESETS);
        private static readonly int SettingsGraphicsKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_GRAPHICS);
        private static readonly int SettingsAudioKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_AUDIO);
        private static readonly int SettingsQualityLevelKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_QUALITY_LEVEL);
        private static readonly int SettingsFovKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_FOV);
        private static readonly int SettingsShadowDistanceKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_SHADOW_DISTANCE);
        private static readonly int SettingsShadowQualityKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_SHADOW_QUALITY);
        private static readonly int SettingsAntiAliasingKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_ANTI_ALIASING);
        private static readonly int SettingsTextureQualityKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_TEXTURE_QUALITY);
        private static readonly int SettingsMenuVisualStyleKeyHash = LocHash.Compute("settings.menu_visual_style");
        private static readonly int SettingsMenuVisualConceptKeyHash = LocHash.Compute("settings.menu_visual_concept");
        private static readonly int SettingsTextScaleKeyHash = LocHash.Compute("settings.text_scale");
        private static readonly int SettingsUiMotionScaleKeyHash = LocHash.Compute("settings.ui_motion_scale");
        private static readonly int SettingsVsyncKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_VSYNC);
        private static readonly int SettingsFullscreenKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_FULLSCREEN);
        private static readonly int SettingsAoKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_AO);
        private static readonly int SettingsBloomKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_BLOOM);
        private static readonly int SettingsMotionBlurKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_MOTION_BLUR);
        private static readonly int SettingsMasterVolumeKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_MASTER_VOLUME);
        private static readonly int SettingsMusicVolumeKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_MUSIC_VOLUME);
        private static readonly int SettingsSfxVolumeKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_SFX_VOLUME);
        private static readonly int SettingsAmbientVolumeKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_AMBIENT_VOLUME);
        private static readonly int SettingsResetDefaultsKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_RESET_DEFAULTS);
        private static readonly int SettingsApplyKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_APPLY);
        private static readonly int SettingsCancelKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_CANCEL);
        private static readonly int SettingsValueOffKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_VALUE_OFF);
        private static readonly int SettingsPresetLowKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_PRESET_LOW);
        private static readonly int SettingsPresetMediumKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_PRESET_MEDIUM);
        private static readonly int SettingsPresetHighKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_PRESET_HIGH);
        private static readonly int SettingsPresetUltraKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_PRESET_ULTRA);
        private static readonly int ErrorSettingsUnavailableKeyHash = LocHash.Compute(LocalizationKeys.ERROR_SETTINGS_UNAVAILABLE);
        private const uint UiTextHashSeed = 2166136261u;
        private const uint UiTextHashPrime = 16777619u;
        private const int TextScalePercentMin = 78;
        private const int TextScalePercentMax = 135;
        private const int TextScalePercentLabelCount = TextScalePercentMax - TextScalePercentMin + 1;

        // ----------------------------------------------------------
        // INSPECTOR
        // ----------------------------------------------------------

        [Header("=== GRAPHICS ===")]
        [SerializeField] private Transform sectionGraphics;
        [SerializeField] private Button btnPresetLow;
        [SerializeField] private Button btnPresetMedium;
        [SerializeField] private Button btnPresetHigh;
        [SerializeField] private Button btnPresetUltra;
        [SerializeField] private Button btnQualityDecrease;
        [SerializeField] private Button btnQualityIncrease;
        [SerializeField] private TMP_Text txtQualityLevel;
        [SerializeField] private Toggle toggleVsync;
        [SerializeField] private Toggle toggleFullscreen;
        [SerializeField] private Slider sliderFieldOfView;
        [SerializeField] private TMP_Text txtFieldOfView;
        [SerializeField] private Button btnShadowQualityDecrease;
        [SerializeField] private Button btnShadowQualityIncrease;
        [SerializeField] private TMP_Text txtShadowQuality;
        [SerializeField] private Slider sliderShadowDistance;
        [SerializeField] private TMP_Text txtShadowDistance;
        [SerializeField] private Button btnAntiAliasingDecrease;
        [SerializeField] private Button btnAntiAliasingIncrease;
        [SerializeField] private TMP_Text txtAntiAliasing;
        [SerializeField] private Toggle toggleAmbientOcclusion;
        [SerializeField] private Toggle toggleBloom;
        [SerializeField] private Toggle toggleMotionBlur;
        [SerializeField] private Button btnTextureQualityDecrease;
        [SerializeField] private Button btnTextureQualityIncrease;
        [SerializeField] private TMP_Text txtTextureQuality;

        [Header("=== MENU STYLE ===")]
        [SerializeField] private Transform rowMenuVisualStyle;
        [SerializeField] private Transform rowMenuVisualConcept;
        [SerializeField] private Button btnMenuStyleDecrease;
        [SerializeField] private Button btnMenuStyleIncrease;
        [SerializeField] private TMP_Text txtMenuVisualStyle;
        [SerializeField] private Button btnMenuConceptDecrease;
        [SerializeField] private Button btnMenuConceptIncrease;
        [SerializeField] private TMP_Text txtMenuVisualConcept;
        [SerializeField, Tooltip("Creates cold runtime rows in scene-authored settings panels when menu visual controls are not wired.")]
        private bool autoCreateMenuVisualStyleRow = true;

        [Header("=== ACCESSIBILITY ===")]
        [SerializeField] private Transform rowTextScale;
        [SerializeField] private Transform rowUiMotionScale;
        [SerializeField] private Slider sliderTextScale;
        [SerializeField] private TMP_Text txtTextScale;
        [SerializeField] private Slider sliderUiMotionScale;
        [SerializeField] private TMP_Text txtUiMotionScale;
        [SerializeField, Tooltip("Creates a cold runtime text-scale row in scene-authored settings panels when accessibility controls are not wired.")]
        private bool autoCreateAccessibilityTextScaleRow = true;
        [SerializeField, Tooltip("Creates a cold runtime UI motion comfort row in scene-authored settings panels when accessibility controls are not wired.")]
        private bool autoCreateAccessibilityMotionScaleRow = true;

        [Header("=== AUDIO ===")]
        [SerializeField] private Slider sliderMasterVolume;
        [SerializeField] private Slider sliderMusicVolume;
        [SerializeField] private Slider sliderSfxVolume;
        [SerializeField] private Slider sliderAmbientVolume;
        [SerializeField] private TMP_Text txtMasterVolume;
        [SerializeField] private TMP_Text txtMusicVolume;
        [SerializeField] private TMP_Text txtSfxVolume;
        [SerializeField] private TMP_Text txtAmbientVolume;

        [Header("=== ACTIONS ===")]
        [SerializeField] private Button btnResetDefaults;
        [SerializeField] private Button btnApply;
        [SerializeField] private Button btnCancel;

        [Header("=== LIVE PREVIEW ===")]
        [SerializeField] private SettingsLivePreview livePreview;

        [Header("=== ANIMATION ===")]
        [SerializeField] private SettingsPanelAnimator panelAnimator;

        [Header("=== COMPARISON VIEW ===")]
        [SerializeField] private SettingsComparisonView comparisonView;

        [Header("=== MODS ===")]
        [SerializeField] private ModMenuUIController modMenuController;

        [Header("=== PERFORMANCE ===")]
        [SerializeField, Tooltip("Apply button cooldown (seconds)")]
        private float applyButtonCooldown = 0.5f;

        [SerializeField, Tooltip("Slider throttle interval (seconds)")]
        private float sliderThrottleInterval = 0.1f;

        [SerializeField, Tooltip("Toggle debounce interval (seconds)")]
        private float toggleDebounceInterval = 0.05f;

        // ----------------------------------------------------------
        // FIELDS
        // ----------------------------------------------------------

        private SettingsManager _settings;
        private bool _initialized;
        private bool _slidersBound;

        // Performance guards
        private float _nextApplyTime;
        private float _nextSliderUpdateTime;
        private float _nextToggleUpdateTime;
        private int _cachedQualityLevel = -1;
        private float _cachedMasterVolume = -1f;
        private float _cachedMusicVolume = -1f;
        private float _cachedSfxVolume = -1f;
        private float _cachedAmbientVolume = -1f;
        private bool _cachedVsync;
        private bool _cachedFullscreen;
        private float _cachedFieldOfView = -1f;
        private int _cachedShadowQuality = -1;
        private float _cachedShadowDistance = -1f;
        private int _cachedAntiAliasing = -1;
        private bool _cachedAmbientOcclusion;
        private bool _cachedBloom;
        private bool _cachedMotionBlur;
        private int _cachedTextureQuality = -1;
        private int _cachedGraphicsPreset = -1;
        private int _cachedMenuVisualStyleIndex = -1;
        private int _cachedMenuVisualConceptIndex = -1;
        private float _cachedTextScale = AccessibilitySettings.DefaultTextScale;
        private float _cachedUiMotionScale = AccessibilitySettings.DefaultUiMotionScale;
        private int _openedMenuVisualStyleIndex = -1;
        private int _openedMenuVisualConceptIndex = -1;

        private UnityAction _presetLowAction;
        private UnityAction _presetMediumAction;
        private UnityAction _presetHighAction;
        private UnityAction _presetUltraAction;
        private UnityAction _qualityDecreaseAction;
        private UnityAction _qualityIncreaseAction;
        private UnityAction _shadowQualityDecreaseAction;
        private UnityAction _shadowQualityIncreaseAction;
        private UnityAction _antiAliasingDecreaseAction;
        private UnityAction _antiAliasingIncreaseAction;
        private UnityAction _textureQualityDecreaseAction;
        private UnityAction _textureQualityIncreaseAction;
        private UnityAction _menuStyleDecreaseAction;
        private UnityAction _menuStyleIncreaseAction;
        private UnityAction _menuConceptDecreaseAction;
        private UnityAction _menuConceptIncreaseAction;
        private UnityAction _resetDefaultsAction;
        private UnityAction _applyAction;
        private UnityAction _cancelAction;
        private Action _retryApplyModalAction;
        private Action _resetDefaultsModalAction;
        private UnityAction<bool> _vsyncChangedAction;
        private UnityAction<bool> _fullscreenChangedAction;
        private UnityAction<bool> _ambientOcclusionChangedAction;
        private UnityAction<bool> _bloomChangedAction;
        private UnityAction<bool> _motionBlurChangedAction;
        private UnityAction<float> _masterVolumeChangedAction;
        private UnityAction<float> _musicVolumeChangedAction;
        private UnityAction<float> _sfxVolumeChangedAction;
        private UnityAction<float> _ambientVolumeChangedAction;
        private UnityAction<float> _fieldOfViewChangedAction;
        private UnityAction<float> _shadowDistanceChangedAction;
        private UnityAction<float> _textScaleChangedAction;
        private UnityAction<float> _uiMotionScaleChangedAction;

        private static readonly string[] ShadowQualityNames = { "Off", "Low", "Medium", "High" };
        private static readonly string[] AntiAliasingNames = { "None", "FXAA", "SMAA", "TAA" };
        private static readonly string[] TextureQualityNames = { "Low", "Medium", "High", "Ultra" };

        // ZERO-GC: Cached char buffers for volume/FOV/shadow distance display
        private static readonly CachedTextLabel[] VolumePercentLabels = new CachedTextLabel[101]; // COLD ALLOC: label[101] - volume percentage char buffers - owner: SettingsPanel
        private static readonly CachedTextLabel[] FOVLabels = new CachedTextLabel[51]; // COLD ALLOC: label[51] - FOV char buffers - owner: SettingsPanel
        private static readonly CachedTextLabel[] ShadowDistanceLabels = new CachedTextLabel[251]; // COLD ALLOC: label[251] - shadow distance char buffers - owner: SettingsPanel
        private static readonly CachedTextLabel[] TextScalePercentLabels = new CachedTextLabel[TextScalePercentLabelCount]; // COLD ALLOC: label[58] - text scale percentage char buffers - owner: SettingsPanel

        // ZERO-GC: Dirty flags to prevent unnecessary SetText calls
        private int _prevMasterVolumeIndex = -1;
        private int _prevMusicVolumeIndex = -1;
        private int _prevSfxVolumeIndex = -1;
        private int _prevAmbientVolumeIndex = -1;
        private int _prevFOVIndex = -1;
        private int _prevShadowDistanceIndex = -1;
        private int _prevTextScaleIndex = -1;
        private int _prevUiMotionScaleIndex = -1;
        private uint _prevQualityLevelTextHash;
        private uint _prevShadowQualityTextHash;
        private uint _prevAntiAliasingTextHash;
        private uint _prevTextureQualityTextHash;
        private uint _prevMenuVisualStyleTextHash;
        private uint _prevMenuVisualConceptTextHash;
        private readonly char[] _menuVisualStyleDisplayBuffer = new char[160]; // COLD ALLOC: menu style indexed display buffer - owner: SettingsPanel
        private readonly char[] _menuVisualConceptDisplayBuffer = new char[160]; // COLD ALLOC: menu concept indexed display buffer - owner: SettingsPanel
        private readonly char[] _modalMessageBuffer = new char[192]; // COLD ALLOC: settings modal message staging buffer copied directly into TMP - owner: SettingsPanel

        private readonly struct CachedTextLabel
        {
            public readonly char[] Buffer;
            public readonly int Length;

            public CachedTextLabel(char[] buffer, int length)
            {
                Buffer = buffer;
                Length = length;
            }
        }

        // ZERO-GC: Static constructor to pre-generate display buffers.
        static SettingsPanel()
        {
            for (int i = 0; i <= 100; i++)
                VolumePercentLabels[i] = CreateSuffixedNumericLabel(i, '%');

            for (int i = 0; i <= 50; i++)
                FOVLabels[i] = CreateSuffixedNumericLabel(60 + i, '\u00B0');

            for (int i = 0; i <= 250; i++)
                ShadowDistanceLabels[i] = CreateSuffixedNumericLabel(50 + i, 'm');

            for (int i = 0; i < TextScalePercentLabels.Length; i++)
                TextScalePercentLabels[i] = CreateSuffixedNumericLabel(TextScalePercentMin + i, '%');
        }

        private static CachedTextLabel CreateSuffixedNumericLabel(int value, char suffix)
        {
            char[] buffer = new char[CountPositiveDecimalDigits(value) + 1]; // COLD ALLOC: numeric label char cache at type initialization.
            value.TryFormat(buffer.AsSpan(), out int written);
            buffer[written] = suffix;
            return new CachedTextLabel(buffer, written + 1);
        }

        private static int CountPositiveDecimalDigits(int value)
        {
            if (value >= 1000)
                return 4;
            if (value >= 100)
                return 3;
            if (value >= 10)
                return 2;

            return 1;
        }

        // ----------------------------------------------------------
        // LIFECYCLE
        // ----------------------------------------------------------

        private void Awake()
        {
            EnsureListenerActionsCached();
            EnsureMenuStyleControlsCold();
            EnsureMenuConceptControlsCold();
            EnsureAccessibilityTextScaleControlsCold();
            EnsureAccessibilityMotionScaleControlsCold();
            BindButtons();
        }

        private void OnEnable()
        {
            EnsureListenerActionsCached();
            EnsureMenuStyleControlsCold();
            EnsureMenuConceptControlsCold();
            EnsureAccessibilityTextScaleControlsCold();
            EnsureAccessibilityMotionScaleControlsCold();
            BindButtons();

            if (!_initialized)
                Initialize();
            else
                BindSliders();

            LocalizationEvents.RegisterLanguageListener(this);
            LoadCurrentSettings();
            CaptureMenuVisualCancelSnapshot();
            RefreshAllUI();
            RefreshLocalizedLabels();
            modMenuController?.RefreshView();

            // Play fade-in animation
            if (panelAnimator != null)
                panelAnimator.PlayFadeIn();

            // Show comparison view
            if (comparisonView != null)
                comparisonView.Show();
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            UnbindSliders();

            // Fade out animation (if supported)
            if (panelAnimator != null && panelAnimator.IsPlaying())
            {
                panelAnimator.SkipAnimation();
            }

            // Hide comparison view
            if (comparisonView != null)
                comparisonView.Hide();
        }

        // ----------------------------------------------------------
        // INITIALIZATION
        // ----------------------------------------------------------

        private void Initialize()
        {
            _settings = GlobalRegistry.Settings;
            s_localization = GlobalRegistry.LocalizationText;
            if (_settings == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[SettingsPanel] SettingsManager runtime is null. Settings unavailable.");
#endif
                return;
            }

            BindSliders();
            _initialized = true;
        }

        private void CacheListenerActions()
        {
            _presetLowAction = OnPresetLow; // COLD ALLOC: UnityAction[1] - cached low preset listener - owner: SettingsPanel
            _presetMediumAction = OnPresetMedium; // COLD ALLOC: UnityAction[1] - cached medium preset listener - owner: SettingsPanel
            _presetHighAction = OnPresetHigh; // COLD ALLOC: UnityAction[1] - cached high preset listener - owner: SettingsPanel
            _presetUltraAction = OnPresetUltra; // COLD ALLOC: UnityAction[1] - cached ultra preset listener - owner: SettingsPanel
            _qualityDecreaseAction = OnQualityDecrease; // COLD ALLOC: UnityAction[1] - cached quality decrease listener - owner: SettingsPanel
            _qualityIncreaseAction = OnQualityIncrease; // COLD ALLOC: UnityAction[1] - cached quality increase listener - owner: SettingsPanel
            _shadowQualityDecreaseAction = OnShadowQualityDecrease; // COLD ALLOC: UnityAction[1] - cached shadow quality decrease listener - owner: SettingsPanel
            _shadowQualityIncreaseAction = OnShadowQualityIncrease; // COLD ALLOC: UnityAction[1] - cached shadow quality increase listener - owner: SettingsPanel
            _antiAliasingDecreaseAction = OnAntiAliasingDecrease; // COLD ALLOC: UnityAction[1] - cached anti-aliasing decrease listener - owner: SettingsPanel
            _antiAliasingIncreaseAction = OnAntiAliasingIncrease; // COLD ALLOC: UnityAction[1] - cached anti-aliasing increase listener - owner: SettingsPanel
            _textureQualityDecreaseAction = OnTextureQualityDecrease; // COLD ALLOC: UnityAction[1] - cached texture quality decrease listener - owner: SettingsPanel
            _textureQualityIncreaseAction = OnTextureQualityIncrease; // COLD ALLOC: UnityAction[1] - cached texture quality increase listener - owner: SettingsPanel
            _menuStyleDecreaseAction = OnMenuStyleDecrease; // COLD ALLOC: UnityAction[1] - cached menu style decrease listener - owner: SettingsPanel
            _menuStyleIncreaseAction = OnMenuStyleIncrease; // COLD ALLOC: UnityAction[1] - cached menu style increase listener - owner: SettingsPanel
            _menuConceptDecreaseAction = OnMenuConceptDecrease; // COLD ALLOC: UnityAction[1] - cached menu concept decrease listener - owner: SettingsPanel
            _menuConceptIncreaseAction = OnMenuConceptIncrease; // COLD ALLOC: UnityAction[1] - cached menu concept increase listener - owner: SettingsPanel
            _resetDefaultsAction = OnResetDefaults; // COLD ALLOC: UnityAction[1] - cached reset defaults listener - owner: SettingsPanel
            _applyAction = OnApply; // COLD ALLOC: UnityAction[1] - cached apply listener - owner: SettingsPanel
            _cancelAction = OnCancel; // COLD ALLOC: UnityAction[1] - cached cancel listener - owner: SettingsPanel
            _retryApplyModalAction = OnApply; // COLD ALLOC: Action[1] - cached settings apply retry modal listener - owner: SettingsPanel
            _resetDefaultsModalAction = OnResetDefaults; // COLD ALLOC: Action[1] - cached settings reset modal listener - owner: SettingsPanel
            _vsyncChangedAction = OnVsyncChanged; // COLD ALLOC: UnityAction<bool>[1] - cached vsync listener - owner: SettingsPanel
            _fullscreenChangedAction = OnFullscreenChanged; // COLD ALLOC: UnityAction<bool>[1] - cached fullscreen listener - owner: SettingsPanel
            _ambientOcclusionChangedAction = OnAmbientOcclusionChanged; // COLD ALLOC: UnityAction<bool>[1] - cached ambient occlusion listener - owner: SettingsPanel
            _bloomChangedAction = OnBloomChanged; // COLD ALLOC: UnityAction<bool>[1] - cached bloom listener - owner: SettingsPanel
            _motionBlurChangedAction = OnMotionBlurChanged; // COLD ALLOC: UnityAction<bool>[1] - cached motion blur listener - owner: SettingsPanel
            _masterVolumeChangedAction = OnMasterVolumeChanged; // COLD ALLOC: UnityAction<float>[1] - cached master volume listener - owner: SettingsPanel
            _musicVolumeChangedAction = OnMusicVolumeChanged; // COLD ALLOC: UnityAction<float>[1] - cached music volume listener - owner: SettingsPanel
            _sfxVolumeChangedAction = OnSfxVolumeChanged; // COLD ALLOC: UnityAction<float>[1] - cached sfx volume listener - owner: SettingsPanel
            _ambientVolumeChangedAction = OnAmbientVolumeChanged; // COLD ALLOC: UnityAction<float>[1] - cached ambient volume listener - owner: SettingsPanel
            _fieldOfViewChangedAction = OnFieldOfViewChanged; // COLD ALLOC: UnityAction<float>[1] - cached field of view listener - owner: SettingsPanel
            _shadowDistanceChangedAction = OnShadowDistanceChanged; // COLD ALLOC: UnityAction<float>[1] - cached shadow distance listener - owner: SettingsPanel
            _textScaleChangedAction = OnTextScaleChanged; // COLD ALLOC: UnityAction<float>[1] - cached text scale listener - owner: SettingsPanel
            _uiMotionScaleChangedAction = OnUiMotionScaleChanged; // COLD ALLOC: UnityAction<float>[1] - cached UI motion scale listener - owner: SettingsPanel
        }

        private void EnsureListenerActionsCached()
        {
            if (_applyAction != null &&
                _cancelAction != null &&
                _masterVolumeChangedAction != null &&
                _shadowDistanceChangedAction != null &&
                _textScaleChangedAction != null &&
                _uiMotionScaleChangedAction != null &&
                _menuStyleIncreaseAction != null &&
                _menuConceptIncreaseAction != null &&
                _retryApplyModalAction != null &&
                _resetDefaultsModalAction != null)
            {
                return;
            }

            CacheListenerActions();
        }

        private void BindButtons()
        {
            if (btnPresetLow != null)
            {
                btnPresetLow.onClick.RemoveListener(_presetLowAction);
                btnPresetLow.onClick.AddListener(_presetLowAction);
            }

            if (btnPresetMedium != null)
            {
                btnPresetMedium.onClick.RemoveListener(_presetMediumAction);
                btnPresetMedium.onClick.AddListener(_presetMediumAction);
            }

            if (btnPresetHigh != null)
            {
                btnPresetHigh.onClick.RemoveListener(_presetHighAction);
                btnPresetHigh.onClick.AddListener(_presetHighAction);
            }

            if (btnPresetUltra != null)
            {
                btnPresetUltra.onClick.RemoveListener(_presetUltraAction);
                btnPresetUltra.onClick.AddListener(_presetUltraAction);
            }

            if (btnQualityDecrease != null)
            {
                btnQualityDecrease.onClick.RemoveListener(_qualityDecreaseAction);
                btnQualityDecrease.onClick.AddListener(_qualityDecreaseAction);
            }

            if (btnQualityIncrease != null)
            {
                btnQualityIncrease.onClick.RemoveListener(_qualityIncreaseAction);
                btnQualityIncrease.onClick.AddListener(_qualityIncreaseAction);
            }

            if (toggleVsync != null)
            {
                toggleVsync.onValueChanged.RemoveListener(_vsyncChangedAction);
                toggleVsync.onValueChanged.AddListener(_vsyncChangedAction);
            }

            if (toggleFullscreen != null)
            {
                toggleFullscreen.onValueChanged.RemoveListener(_fullscreenChangedAction);
                toggleFullscreen.onValueChanged.AddListener(_fullscreenChangedAction);
            }

            if (btnShadowQualityDecrease != null)
            {
                btnShadowQualityDecrease.onClick.RemoveListener(_shadowQualityDecreaseAction);
                btnShadowQualityDecrease.onClick.AddListener(_shadowQualityDecreaseAction);
            }

            if (btnShadowQualityIncrease != null)
            {
                btnShadowQualityIncrease.onClick.RemoveListener(_shadowQualityIncreaseAction);
                btnShadowQualityIncrease.onClick.AddListener(_shadowQualityIncreaseAction);
            }

            if (btnAntiAliasingDecrease != null)
            {
                btnAntiAliasingDecrease.onClick.RemoveListener(_antiAliasingDecreaseAction);
                btnAntiAliasingDecrease.onClick.AddListener(_antiAliasingDecreaseAction);
            }

            if (btnAntiAliasingIncrease != null)
            {
                btnAntiAliasingIncrease.onClick.RemoveListener(_antiAliasingIncreaseAction);
                btnAntiAliasingIncrease.onClick.AddListener(_antiAliasingIncreaseAction);
            }

            if (btnTextureQualityDecrease != null)
            {
                btnTextureQualityDecrease.onClick.RemoveListener(_textureQualityDecreaseAction);
                btnTextureQualityDecrease.onClick.AddListener(_textureQualityDecreaseAction);
            }

            if (btnTextureQualityIncrease != null)
            {
                btnTextureQualityIncrease.onClick.RemoveListener(_textureQualityIncreaseAction);
                btnTextureQualityIncrease.onClick.AddListener(_textureQualityIncreaseAction);
            }

            if (btnMenuStyleDecrease != null)
            {
                btnMenuStyleDecrease.onClick.RemoveListener(_menuStyleDecreaseAction);
                btnMenuStyleDecrease.onClick.AddListener(_menuStyleDecreaseAction);
            }

            if (btnMenuStyleIncrease != null)
            {
                btnMenuStyleIncrease.onClick.RemoveListener(_menuStyleIncreaseAction);
                btnMenuStyleIncrease.onClick.AddListener(_menuStyleIncreaseAction);
            }

            if (btnMenuConceptDecrease != null)
            {
                btnMenuConceptDecrease.onClick.RemoveListener(_menuConceptDecreaseAction);
                btnMenuConceptDecrease.onClick.AddListener(_menuConceptDecreaseAction);
            }

            if (btnMenuConceptIncrease != null)
            {
                btnMenuConceptIncrease.onClick.RemoveListener(_menuConceptIncreaseAction);
                btnMenuConceptIncrease.onClick.AddListener(_menuConceptIncreaseAction);
            }

            if (toggleAmbientOcclusion != null)
            {
                toggleAmbientOcclusion.onValueChanged.RemoveListener(_ambientOcclusionChangedAction);
                toggleAmbientOcclusion.onValueChanged.AddListener(_ambientOcclusionChangedAction);
            }

            if (toggleBloom != null)
            {
                toggleBloom.onValueChanged.RemoveListener(_bloomChangedAction);
                toggleBloom.onValueChanged.AddListener(_bloomChangedAction);
            }

            if (toggleMotionBlur != null)
            {
                toggleMotionBlur.onValueChanged.RemoveListener(_motionBlurChangedAction);
                toggleMotionBlur.onValueChanged.AddListener(_motionBlurChangedAction);
            }

            if (btnResetDefaults != null)
            {
                btnResetDefaults.onClick.RemoveListener(_resetDefaultsAction);
                btnResetDefaults.onClick.AddListener(_resetDefaultsAction);
            }

            if (btnApply != null)
            {
                btnApply.onClick.RemoveListener(_applyAction);
                btnApply.onClick.AddListener(_applyAction);
            }

            if (btnCancel != null)
            {
                btnCancel.onClick.RemoveListener(_cancelAction);
                btnCancel.onClick.AddListener(_cancelAction);
            }
        }

        private void EnsureMenuStyleControlsCold()
        {
            if (!autoCreateMenuVisualStyleRow)
                return;

            if (btnMenuStyleDecrease != null && btnMenuStyleIncrease != null && txtMenuVisualStyle != null)
                return;

            Transform graphicsSection = sectionGraphics != null ? sectionGraphics : transform.Find("Container/Section_Graphics");
            if (graphicsSection == null)
                return;

            Transform existingRow = rowMenuVisualStyle != null ? rowMenuVisualStyle : graphicsSection.Find("Row_MenuVisualStyle");
            if (existingRow == null)
                existingRow = CreateMenuStyleRowCold(graphicsSection);

            CacheMenuStyleRowCold(existingRow);
        }

        private Transform CreateMenuStyleRowCold(Transform parent)
        {
            GameObject rowObject = new GameObject("Row_MenuVisualStyle", typeof(RectTransform)); // COLD ALLOC: optional main-menu settings style row.
            rowObject.transform.SetParent(parent, false);

            RectTransform rowRect = (RectTransform)rowObject.transform;
            rowRect.localScale = Vector3.one;

            LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
            rowLayout.minHeight = 34f;
            rowLayout.preferredHeight = 36f;

            HorizontalLayoutGroup rowGroup = rowObject.AddComponent<HorizontalLayoutGroup>();
            rowGroup.childAlignment = TextAnchor.MiddleLeft;
            rowGroup.childControlHeight = true;
            rowGroup.childControlWidth = true;
            rowGroup.childForceExpandHeight = false;
            rowGroup.childForceExpandWidth = false;
            rowGroup.spacing = 10f;

            TMP_Text label = CreateMenuStyleTextCold(rowObject.transform, "Label_Row_MenuVisualStyle", "MENU STYLE".AsSpan(), TextAlignmentOptions.Left, 12f);
            ConfigureMenuStyleLayoutCold(label.gameObject, 170f, 0f);

            btnMenuStyleDecrease = CreateMenuStyleButtonCold(rowObject.transform, "Btn_MenuStyleDecrease", "<".AsSpan());
            ConfigureMenuStyleLayoutCold(btnMenuStyleDecrease.gameObject, 42f, 0f);

            txtMenuVisualStyle = CreateMenuStyleTextCold(rowObject.transform, "Txt_MenuVisualStyle", MenuVisualStyleCatalog.GetDisplayName(MenuVisualStyle.PressureVesselNoir), TextAlignmentOptions.Center, 10.5f);
            ConfigureMenuStyleLayoutCold(txtMenuVisualStyle.gameObject, 0f, 1f);

            btnMenuStyleIncrease = CreateMenuStyleButtonCold(rowObject.transform, "Btn_MenuStyleIncrease", ">".AsSpan());
            ConfigureMenuStyleLayoutCold(btnMenuStyleIncrease.gameObject, 42f, 0f);

            return rowObject.transform;
        }

        private void CacheMenuStyleRowCold(Transform row)
        {
            if (row == null)
                return;

            if (btnMenuStyleDecrease == null)
                btnMenuStyleDecrease = FindDirectChildComponentCold<Button>(row, "Btn_MenuStyleDecrease");
            if (btnMenuStyleIncrease == null)
                btnMenuStyleIncrease = FindDirectChildComponentCold<Button>(row, "Btn_MenuStyleIncrease");
            if (txtMenuVisualStyle == null)
                txtMenuVisualStyle = FindDirectChildComponentCold<TMP_Text>(row, "Txt_MenuVisualStyle");
        }

        private void EnsureMenuConceptControlsCold()
        {
            if (!autoCreateMenuVisualStyleRow)
                return;

            if (btnMenuConceptDecrease != null && btnMenuConceptIncrease != null && txtMenuVisualConcept != null)
                return;

            Transform graphicsSection = sectionGraphics != null ? sectionGraphics : transform.Find("Container/Section_Graphics");
            if (graphicsSection == null)
                return;

            Transform existingRow = rowMenuVisualConcept != null ? rowMenuVisualConcept : graphicsSection.Find("Row_MenuVisualConcept");
            if (existingRow == null)
                existingRow = CreateMenuConceptRowCold(graphicsSection);

            CacheMenuConceptRowCold(existingRow);
        }

        private Transform CreateMenuConceptRowCold(Transform parent)
        {
            GameObject rowObject = new GameObject("Row_MenuVisualConcept", typeof(RectTransform)); // COLD ALLOC: optional main-menu settings concept row.
            rowObject.transform.SetParent(parent, false);

            RectTransform rowRect = (RectTransform)rowObject.transform;
            rowRect.localScale = Vector3.one;

            LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
            rowLayout.minHeight = 34f;
            rowLayout.preferredHeight = 36f;

            HorizontalLayoutGroup rowGroup = rowObject.AddComponent<HorizontalLayoutGroup>();
            rowGroup.childAlignment = TextAnchor.MiddleLeft;
            rowGroup.childControlHeight = true;
            rowGroup.childControlWidth = true;
            rowGroup.childForceExpandHeight = false;
            rowGroup.childForceExpandWidth = false;
            rowGroup.spacing = 10f;

            TMP_Text label = CreateMenuStyleTextCold(rowObject.transform, "Label_Row_MenuVisualConcept", "MENU CONCEPT".AsSpan(), TextAlignmentOptions.Left, 12f);
            ConfigureMenuStyleLayoutCold(label.gameObject, 170f, 0f);

            btnMenuConceptDecrease = CreateMenuStyleButtonCold(rowObject.transform, "Btn_MenuConceptDecrease", "<".AsSpan());
            ConfigureMenuStyleLayoutCold(btnMenuConceptDecrease.gameObject, 42f, 0f);

            txtMenuVisualConcept = CreateMenuStyleTextCold(rowObject.transform, "Txt_MenuVisualConcept", MenuVisualConceptCatalog.GetDisplayName(MenuVisualConcept.ModuleWindowOverlay), TextAlignmentOptions.Center, 10.5f);
            ConfigureMenuStyleLayoutCold(txtMenuVisualConcept.gameObject, 0f, 1f);

            btnMenuConceptIncrease = CreateMenuStyleButtonCold(rowObject.transform, "Btn_MenuConceptIncrease", ">".AsSpan());
            ConfigureMenuStyleLayoutCold(btnMenuConceptIncrease.gameObject, 42f, 0f);

            return rowObject.transform;
        }

        private void CacheMenuConceptRowCold(Transform row)
        {
            if (row == null)
                return;

            if (btnMenuConceptDecrease == null)
                btnMenuConceptDecrease = FindDirectChildComponentCold<Button>(row, "Btn_MenuConceptDecrease");
            if (btnMenuConceptIncrease == null)
                btnMenuConceptIncrease = FindDirectChildComponentCold<Button>(row, "Btn_MenuConceptIncrease");
            if (txtMenuVisualConcept == null)
                txtMenuVisualConcept = FindDirectChildComponentCold<TMP_Text>(row, "Txt_MenuVisualConcept");
        }

        private void EnsureAccessibilityTextScaleControlsCold()
        {
            if (!autoCreateAccessibilityTextScaleRow)
                return;

            if (sliderTextScale != null && txtTextScale != null)
                return;

            Transform graphicsSection = sectionGraphics != null ? sectionGraphics : transform.Find("Container/Section_Graphics");
            if (graphicsSection == null)
                return;

            Transform existingRow = rowTextScale != null ? rowTextScale : graphicsSection.Find("Row_TextScale");
            if (existingRow == null)
                existingRow = CreateAccessibilityTextScaleRowCold(graphicsSection);

            CacheAccessibilityTextScaleRowCold(existingRow);
        }

        private Transform CreateAccessibilityTextScaleRowCold(Transform parent)
        {
            GameObject rowObject = new GameObject("Row_TextScale", typeof(RectTransform)); // COLD ALLOC: optional accessibility text-scale row.
            rowObject.transform.SetParent(parent, false);

            RectTransform rowRect = (RectTransform)rowObject.transform;
            rowRect.localScale = Vector3.one;

            LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
            rowLayout.minHeight = 34f;
            rowLayout.preferredHeight = 36f;

            HorizontalLayoutGroup rowGroup = rowObject.AddComponent<HorizontalLayoutGroup>();
            rowGroup.childAlignment = TextAnchor.MiddleLeft;
            rowGroup.childControlHeight = true;
            rowGroup.childControlWidth = true;
            rowGroup.childForceExpandHeight = false;
            rowGroup.childForceExpandWidth = false;
            rowGroup.spacing = 10f;

            TMP_Text label = CreateMenuStyleTextCold(rowObject.transform, "Label_Row_TextScale", "TEXT SCALE".AsSpan(), TextAlignmentOptions.Left, 12f);
            ConfigureMenuStyleLayoutCold(label.gameObject, 170f, 0f);

            sliderTextScale = CreateTextScaleSliderCold(rowObject.transform, "Slider_TextScale");
            ConfigureMenuStyleLayoutCold(sliderTextScale.gameObject, 0f, 1f);

            txtTextScale = CreateMenuStyleTextCold(rowObject.transform, "Txt_TextScale", "100%".AsSpan(), TextAlignmentOptions.Center, 10.5f);
            ConfigureMenuStyleLayoutCold(txtTextScale.gameObject, 62f, 0f);

            return rowObject.transform;
        }

        private void CacheAccessibilityTextScaleRowCold(Transform row)
        {
            if (row == null)
                return;

            if (sliderTextScale == null)
                sliderTextScale = FindDirectChildComponentCold<Slider>(row, "Slider_TextScale");
            if (txtTextScale == null)
                txtTextScale = FindDirectChildComponentCold<TMP_Text>(row, "Txt_TextScale");
        }

        private void EnsureAccessibilityMotionScaleControlsCold()
        {
            if (!autoCreateAccessibilityMotionScaleRow)
                return;

            if (sliderUiMotionScale != null && txtUiMotionScale != null)
                return;

            Transform graphicsSection = sectionGraphics != null ? sectionGraphics : transform.Find("Container/Section_Graphics");
            if (graphicsSection == null)
                return;

            Transform existingRow = rowUiMotionScale != null ? rowUiMotionScale : graphicsSection.Find("Row_UiMotionScale");
            if (existingRow == null)
                existingRow = CreateAccessibilityMotionScaleRowCold(graphicsSection);

            CacheAccessibilityMotionScaleRowCold(existingRow);
        }

        private Transform CreateAccessibilityMotionScaleRowCold(Transform parent)
        {
            GameObject rowObject = new GameObject("Row_UiMotionScale", typeof(RectTransform)); // COLD ALLOC: optional accessibility motion row.
            rowObject.transform.SetParent(parent, false);

            RectTransform rowRect = (RectTransform)rowObject.transform;
            rowRect.localScale = Vector3.one;

            LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
            rowLayout.minHeight = 34f;
            rowLayout.preferredHeight = 36f;

            HorizontalLayoutGroup rowGroup = rowObject.AddComponent<HorizontalLayoutGroup>();
            rowGroup.childAlignment = TextAnchor.MiddleLeft;
            rowGroup.childControlHeight = true;
            rowGroup.childControlWidth = true;
            rowGroup.childForceExpandHeight = false;
            rowGroup.childForceExpandWidth = false;
            rowGroup.spacing = 10f;

            TMP_Text label = CreateMenuStyleTextCold(rowObject.transform, "Label_Row_UiMotionScale", "UI MOTION".AsSpan(), TextAlignmentOptions.Left, 12f);
            ConfigureMenuStyleLayoutCold(label.gameObject, 170f, 0f);

            sliderUiMotionScale = CreateUiMotionScaleSliderCold(rowObject.transform, "Slider_UiMotionScale");
            ConfigureMenuStyleLayoutCold(sliderUiMotionScale.gameObject, 0f, 1f);

            txtUiMotionScale = CreateMenuStyleTextCold(rowObject.transform, "Txt_UiMotionScale", "100%".AsSpan(), TextAlignmentOptions.Center, 10.5f);
            ConfigureMenuStyleLayoutCold(txtUiMotionScale.gameObject, 62f, 0f);

            return rowObject.transform;
        }

        private void CacheAccessibilityMotionScaleRowCold(Transform row)
        {
            if (row == null)
                return;

            if (sliderUiMotionScale == null)
                sliderUiMotionScale = FindDirectChildComponentCold<Slider>(row, "Slider_UiMotionScale");
            if (txtUiMotionScale == null)
                txtUiMotionScale = FindDirectChildComponentCold<TMP_Text>(row, "Txt_UiMotionScale");
        }

        private static Slider CreateTextScaleSliderCold(Transform parent, string name)
        {
            return CreateScalarSliderCold(
                parent,
                name,
                AccessibilitySettings.MinimumTextScale,
                AccessibilitySettings.MaximumTextScale,
                AccessibilitySettings.DefaultTextScale);
        }

        private static Slider CreateUiMotionScaleSliderCold(Transform parent, string name)
        {
            return CreateScalarSliderCold(
                parent,
                name,
                AccessibilitySettings.MinimumUiMotionScale,
                AccessibilitySettings.MaximumUiMotionScale,
                AccessibilitySettings.DefaultUiMotionScale);
        }

        private static Slider CreateScalarSliderCold(Transform parent, string name, float minValue, float maxValue, float defaultValue)
        {
            GameObject sliderObject = new GameObject(name, typeof(RectTransform)); // COLD ALLOC: optional accessibility slider.
            sliderObject.transform.SetParent(parent, false);

            Slider slider = sliderObject.AddComponent<Slider>();
            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.value = defaultValue;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;

            Image background = CreateSliderImageCold(sliderObject.transform, "Background", new Color(0.030f, 0.060f, 0.065f, 0.78f));
            RectTransform backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.sizeDelta = new Vector2(0f, 8f);
            backgroundRect.anchoredPosition = Vector2.zero;

            GameObject fillAreaObject = new GameObject("Fill Area", typeof(RectTransform)); // COLD ALLOC: optional slider fill area.
            fillAreaObject.transform.SetParent(sliderObject.transform, false);
            RectTransform fillAreaRect = (RectTransform)fillAreaObject.transform;
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(9f, 0f);
            fillAreaRect.offsetMax = new Vector2(-9f, 0f);

            Image fill = CreateSliderImageCold(fillAreaObject.transform, "Fill", new Color(0.230f, 0.780f, 0.720f, 0.90f));
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(1f, 0.5f);
            fillRect.pivot = new Vector2(0.5f, 0.5f);
            fillRect.sizeDelta = new Vector2(0f, 8f);
            fillRect.anchoredPosition = Vector2.zero;

            GameObject handleAreaObject = new GameObject("Handle Slide Area", typeof(RectTransform)); // COLD ALLOC: optional slider handle area.
            handleAreaObject.transform.SetParent(sliderObject.transform, false);
            RectTransform handleAreaRect = (RectTransform)handleAreaObject.transform;
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(9f, 0f);
            handleAreaRect.offsetMax = new Vector2(-9f, 0f);

            Image handle = CreateSliderImageCold(handleAreaObject.transform, "Handle", new Color(0.950f, 0.600f, 0.240f, 0.98f));
            RectTransform handleRect = handle.rectTransform;
            handleRect.anchorMin = new Vector2(0f, 0.5f);
            handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(18f, 18f);
            handleRect.anchoredPosition = Vector2.zero;

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;

            return slider;
        }

        private static Image CreateSliderImageCold(Transform parent, string name, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform)); // COLD ALLOC: optional slider image.
            imageObject.transform.SetParent(parent, false);

            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static T FindDirectChildComponentCold<T>(Transform parent, string childName) where T : Component
        {
            Transform child = parent.Find(childName);
            if (child == null || !child.TryGetComponent(out T component))
                return null;

            return component;
        }

        private static Button CreateMenuStyleButtonCold(Transform parent, string name, ReadOnlySpan<char> label)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform)); // COLD ALLOC: optional settings row button.
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.075f, 0.145f, 0.155f, 0.86f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.075f, 0.145f, 0.155f, 0.86f);
            colors.highlightedColor = new Color(0.110f, 0.250f, 0.270f, 0.95f);
            colors.selectedColor = new Color(0.140f, 0.340f, 0.360f, 0.98f);
            colors.pressedColor = new Color(0.980f, 0.520f, 0.180f, 0.98f);
            colors.disabledColor = new Color(0.040f, 0.060f, 0.065f, 0.50f);
            button.colors = colors;

            TMP_Text text = CreateMenuStyleTextCold(buttonObject.transform, "Label", label, TextAlignmentOptions.Center, 13f);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
        }

        private static TMP_Text CreateMenuStyleTextCold(Transform parent, string name, ReadOnlySpan<char> value, TextAlignmentOptions alignment, float fontSize)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform)); // COLD ALLOC: optional settings row TMP text.
            textObject.transform.SetParent(parent, false);

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.color = new Color(0.650f, 0.900f, 0.870f, 0.92f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            ConfigureMenuTextFitCold(text, fontSize * 0.68f, fontSize);
            TmpTextNoAlloc.Set(text, value);
            return text;
        }

        private static void ConfigureMenuTextFitCold(TMP_Text text, float minSize, float maxSize)
        {
            if (text == null)
                return;

            float resolvedMin = math.max(6f, math.min(minSize, maxSize));
            float resolvedMax = math.max(resolvedMin, maxSize);
            text.enableAutoSizing = true;
            text.fontSizeMin = resolvedMin;
            text.fontSizeMax = resolvedMax;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private static void ConfigureMenuStyleLayoutCold(GameObject target, float preferredWidth, float flexibleWidth)
        {
            if (target == null)
                return;

            LayoutElement layout = target.GetComponent<LayoutElement>();
            if (layout == null)
                layout = target.AddComponent<LayoutElement>();

            if (preferredWidth > 0f)
                layout.preferredWidth = preferredWidth;
            layout.flexibleWidth = flexibleWidth;
            layout.minHeight = 30f;
            layout.preferredHeight = 32f;
        }

        private void BindSliders()
        {
            if (_slidersBound)
                return;

            EnsureListenerActionsCached();

            if (sliderMasterVolume != null)
            {
                sliderMasterVolume.onValueChanged.RemoveListener(_masterVolumeChangedAction);
                sliderMasterVolume.onValueChanged.AddListener(_masterVolumeChangedAction);
            }

            if (sliderMusicVolume != null)
            {
                sliderMusicVolume.onValueChanged.RemoveListener(_musicVolumeChangedAction);
                sliderMusicVolume.onValueChanged.AddListener(_musicVolumeChangedAction);
            }

            if (sliderSfxVolume != null)
            {
                sliderSfxVolume.onValueChanged.RemoveListener(_sfxVolumeChangedAction);
                sliderSfxVolume.onValueChanged.AddListener(_sfxVolumeChangedAction);
            }

            if (sliderAmbientVolume != null)
            {
                sliderAmbientVolume.onValueChanged.RemoveListener(_ambientVolumeChangedAction);
                sliderAmbientVolume.onValueChanged.AddListener(_ambientVolumeChangedAction);
            }

            if (sliderFieldOfView != null)
            {
                sliderFieldOfView.onValueChanged.RemoveListener(_fieldOfViewChangedAction);
                sliderFieldOfView.onValueChanged.AddListener(_fieldOfViewChangedAction);
            }

            if (sliderShadowDistance != null)
            {
                sliderShadowDistance.onValueChanged.RemoveListener(_shadowDistanceChangedAction);
                sliderShadowDistance.onValueChanged.AddListener(_shadowDistanceChangedAction);
            }

            if (sliderTextScale != null)
            {
                sliderTextScale.onValueChanged.RemoveListener(_textScaleChangedAction);
                sliderTextScale.onValueChanged.AddListener(_textScaleChangedAction);
            }

            if (sliderUiMotionScale != null)
            {
                sliderUiMotionScale.onValueChanged.RemoveListener(_uiMotionScaleChangedAction);
                sliderUiMotionScale.onValueChanged.AddListener(_uiMotionScaleChangedAction);
            }

            _slidersBound = true;
        }

        private void UnbindSliders()
        {
            if (!_slidersBound)
                return;

            if (sliderMasterVolume != null)
                sliderMasterVolume.onValueChanged.RemoveListener(_masterVolumeChangedAction);

            if (sliderMusicVolume != null)
                sliderMusicVolume.onValueChanged.RemoveListener(_musicVolumeChangedAction);

            if (sliderSfxVolume != null)
                sliderSfxVolume.onValueChanged.RemoveListener(_sfxVolumeChangedAction);

            if (sliderAmbientVolume != null)
                sliderAmbientVolume.onValueChanged.RemoveListener(_ambientVolumeChangedAction);

            if (sliderFieldOfView != null)
                sliderFieldOfView.onValueChanged.RemoveListener(_fieldOfViewChangedAction);

            if (sliderShadowDistance != null)
                sliderShadowDistance.onValueChanged.RemoveListener(_shadowDistanceChangedAction);

            if (sliderTextScale != null)
                sliderTextScale.onValueChanged.RemoveListener(_textScaleChangedAction);

            if (sliderUiMotionScale != null)
                sliderUiMotionScale.onValueChanged.RemoveListener(_uiMotionScaleChangedAction);

            _slidersBound = false;
        }

        // ----------------------------------------------------------
        // LOAD/REFRESH
        // ----------------------------------------------------------

        private void LoadCurrentSettings()
        {
            if (_settings == null)
                return;

            _cachedQualityLevel = _settings.QualityLevel;
            _cachedMasterVolume = _settings.MasterVolume;
            _cachedMusicVolume = _settings.MusicVolume;
            _cachedSfxVolume = _settings.SfxVolume;
            _cachedAmbientVolume = _settings.AmbientVolume;
            _cachedVsync = _settings.Vsync;
            _cachedFullscreen = _settings.Fullscreen;
            _cachedFieldOfView = _settings.FieldOfView;
            _cachedShadowQuality = _settings.ShadowQuality;
            _cachedShadowDistance = _settings.ShadowDistance;
            _cachedAntiAliasing = _settings.AntiAliasing;
            _cachedAmbientOcclusion = _settings.AmbientOcclusion;
            _cachedBloom = _settings.Bloom;
            _cachedMotionBlur = _settings.MotionBlur;
            _cachedTextureQuality = _settings.TextureQuality;
            _cachedGraphicsPreset = _settings.GraphicsPreset;
            _cachedMenuVisualStyleIndex = MenuVisualStyleCatalog.ToIndex(_settings.MenuVisualStyle);
            _cachedMenuVisualConceptIndex = MenuVisualConceptCatalog.ToIndex(_settings.MenuVisualConcept);
            _cachedTextScale = SanitizeTextScale(_settings.TextScale);
            _cachedUiMotionScale = SanitizeUiMotionScale(_settings.UiMotionScale);
        }

        private void CaptureMenuVisualCancelSnapshot()
        {
            _openedMenuVisualStyleIndex = _cachedMenuVisualStyleIndex;
            _openedMenuVisualConceptIndex = _cachedMenuVisualConceptIndex;
        }

        private void RestoreMenuVisualCancelSnapshot()
        {
            if (_settings == null)
                return;

            if (MenuVisualStyleCatalog.IsValidStyleIndex(_openedMenuVisualStyleIndex))
                _settings.PreviewMenuVisualStyle(MenuVisualStyleCatalog.FromIndex(_openedMenuVisualStyleIndex));

            if (MenuVisualConceptCatalog.IsValidConceptIndex(_openedMenuVisualConceptIndex))
                _settings.PreviewMenuVisualConcept(MenuVisualConceptCatalog.FromIndex(_openedMenuVisualConceptIndex));
        }

        private void RefreshAllUI()
        {
            RefreshQualityUI();
            RefreshVolumeUI();
            RefreshVideoUI();
            RefreshAdvancedGraphicsUI();
            RefreshMenuVisualStyleUI();
            RefreshMenuVisualConceptUI();
            RefreshAccessibilityUI();
        }

        private void RefreshQualityUI()
        {
            if (txtQualityLevel == null)
                return;

            if (_cachedQualityLevel >= 0 && _cachedQualityLevel <= SettingsManager.MaxContinuousQualityLevel)
                SetValueTextIfChanged(txtQualityLevel, ResolveLocalizedQualityName(_cachedQualityLevel), ref _prevQualityLevelTextHash);
            else
                SetValueTextIfChanged(txtQualityLevel, "--".AsSpan(), ref _prevQualityLevelTextHash);
        }

        private void RefreshVolumeUI()
        {
            if (sliderMasterVolume != null)
                sliderMasterVolume.SetValueWithoutNotify(_cachedMasterVolume);

            if (sliderMusicVolume != null)
                sliderMusicVolume.SetValueWithoutNotify(_cachedMusicVolume);

            if (sliderSfxVolume != null)
                sliderSfxVolume.SetValueWithoutNotify(_cachedSfxVolume);

            if (sliderAmbientVolume != null)
                sliderAmbientVolume.SetValueWithoutNotify(_cachedAmbientVolume);

            // ZERO-GC: Use cached strings and dirty flags
            if (txtMasterVolume != null)
            {
                int percent = Mathf.RoundToInt(_cachedMasterVolume * 100f);
                int labelIndex = Mathf.Clamp(percent, 0, 100);
                SetCachedLabelIfChanged(txtMasterVolume, VolumePercentLabels, labelIndex, ref _prevMasterVolumeIndex);
            }

            if (txtMusicVolume != null)
            {
                int percent = Mathf.RoundToInt(_cachedMusicVolume * 100f);
                int labelIndex = Mathf.Clamp(percent, 0, 100);
                SetCachedLabelIfChanged(txtMusicVolume, VolumePercentLabels, labelIndex, ref _prevMusicVolumeIndex);
            }

            if (txtSfxVolume != null)
            {
                int percent = Mathf.RoundToInt(_cachedSfxVolume * 100f);
                int labelIndex = Mathf.Clamp(percent, 0, 100);
                SetCachedLabelIfChanged(txtSfxVolume, VolumePercentLabels, labelIndex, ref _prevSfxVolumeIndex);
            }

            if (txtAmbientVolume != null)
            {
                int percent = Mathf.RoundToInt(_cachedAmbientVolume * 100f);
                int labelIndex = Mathf.Clamp(percent, 0, 100);
                SetCachedLabelIfChanged(txtAmbientVolume, VolumePercentLabels, labelIndex, ref _prevAmbientVolumeIndex);
            }
        }

        private void RefreshVideoUI()
        {
            if (toggleVsync != null)
                toggleVsync.SetIsOnWithoutNotify(_cachedVsync);

            if (toggleFullscreen != null)
                toggleFullscreen.SetIsOnWithoutNotify(_cachedFullscreen);
        }

        private void RefreshAdvancedGraphicsUI()
        {
            if (sliderFieldOfView != null)
                sliderFieldOfView.SetValueWithoutNotify(_cachedFieldOfView);

            // ZERO-GC: Use cached strings and dirty flags
            if (txtFieldOfView != null)
            {
                int fov = Mathf.RoundToInt(_cachedFieldOfView);
                int index = Mathf.Clamp(fov - 60, 0, 50);
                SetCachedLabelIfChanged(txtFieldOfView, FOVLabels, index, ref _prevFOVIndex);
            }

            if (txtShadowQuality != null && _cachedShadowQuality >= 0 && _cachedShadowQuality < ShadowQualityNames.Length)
                SetValueTextIfChanged(txtShadowQuality, ResolveLocalizedShadowQualityName(_cachedShadowQuality), ref _prevShadowQualityTextHash);

            if (sliderShadowDistance != null)
                sliderShadowDistance.SetValueWithoutNotify(_cachedShadowDistance);

            // ZERO-GC: Use cached strings and dirty flags
            if (txtShadowDistance != null)
            {
                int distance = Mathf.RoundToInt(_cachedShadowDistance);
                int index = Mathf.Clamp(distance - 50, 0, 250);
                SetCachedLabelIfChanged(txtShadowDistance, ShadowDistanceLabels, index, ref _prevShadowDistanceIndex);
            }

            if (txtAntiAliasing != null && _cachedAntiAliasing >= 0 && _cachedAntiAliasing < AntiAliasingNames.Length)
                SetValueTextIfChanged(txtAntiAliasing, ResolveLocalizedAntiAliasingName(_cachedAntiAliasing), ref _prevAntiAliasingTextHash);

            if (toggleAmbientOcclusion != null)
                toggleAmbientOcclusion.SetIsOnWithoutNotify(_cachedAmbientOcclusion);

            if (toggleBloom != null)
                toggleBloom.SetIsOnWithoutNotify(_cachedBloom);

            if (toggleMotionBlur != null)
                toggleMotionBlur.SetIsOnWithoutNotify(_cachedMotionBlur);

            if (txtTextureQuality != null && _cachedTextureQuality >= 0 && _cachedTextureQuality < TextureQualityNames.Length)
                SetValueTextIfChanged(txtTextureQuality, ResolveLocalizedTextureQualityName(_cachedTextureQuality), ref _prevTextureQualityTextHash);
        }

        private void RefreshMenuVisualStyleUI()
        {
            if (txtMenuVisualStyle == null)
                return;

            if (!MenuVisualStyleCatalog.IsValidStyleIndex(_cachedMenuVisualStyleIndex))
                _cachedMenuVisualStyleIndex = MenuVisualStyleCatalog.ClampStyleIndex(_cachedMenuVisualStyleIndex);

            SetIndexedMenuVisualLabelIfChanged(
                txtMenuVisualStyle,
                _cachedMenuVisualStyleIndex + 1,
                MenuVisualStyleCatalog.StyleCount,
                MenuVisualStyleCatalog.GetDisplayName(MenuVisualStyleCatalog.FromIndex(_cachedMenuVisualStyleIndex)),
                _menuVisualStyleDisplayBuffer,
                ref _prevMenuVisualStyleTextHash);
        }

        private void RefreshMenuVisualConceptUI()
        {
            if (txtMenuVisualConcept == null)
                return;

            if (!MenuVisualConceptCatalog.IsValidConceptIndex(_cachedMenuVisualConceptIndex))
                _cachedMenuVisualConceptIndex = MenuVisualConceptCatalog.ClampConceptIndex(_cachedMenuVisualConceptIndex);

            SetIndexedMenuVisualLabelIfChanged(
                txtMenuVisualConcept,
                _cachedMenuVisualConceptIndex + 1,
                MenuVisualConceptCatalog.ConceptCount,
                MenuVisualConceptCatalog.GetDisplayName(MenuVisualConceptCatalog.FromIndex(_cachedMenuVisualConceptIndex)),
                _menuVisualConceptDisplayBuffer,
                ref _prevMenuVisualConceptTextHash);
        }

        private void RefreshAccessibilityUI()
        {
            _cachedTextScale = SanitizeTextScale(_cachedTextScale);
            _cachedUiMotionScale = SanitizeUiMotionScale(_cachedUiMotionScale);

            if (sliderTextScale != null)
            {
                sliderTextScale.minValue = AccessibilitySettings.MinimumTextScale;
                sliderTextScale.maxValue = AccessibilitySettings.MaximumTextScale;
                sliderTextScale.SetValueWithoutNotify(_cachedTextScale);
            }

            if (sliderUiMotionScale != null)
            {
                sliderUiMotionScale.minValue = AccessibilitySettings.MinimumUiMotionScale;
                sliderUiMotionScale.maxValue = AccessibilitySettings.MaximumUiMotionScale;
                sliderUiMotionScale.SetValueWithoutNotify(_cachedUiMotionScale);
            }

            RefreshTextScaleValueLabel();
            RefreshUiMotionScaleValueLabel();
        }

        private void RefreshTextScaleValueLabel()
        {
            if (txtTextScale == null)
                return;

            int index = ResolveTextScaleLabelIndex(_cachedTextScale);
            SetCachedLabelIfChanged(txtTextScale, TextScalePercentLabels, index, ref _prevTextScaleIndex);
        }

        private void RefreshUiMotionScaleValueLabel()
        {
            if (txtUiMotionScale == null)
                return;

            int index = ResolveUiMotionScaleLabelIndex(_cachedUiMotionScale);
            SetCachedLabelIfChanged(txtUiMotionScale, VolumePercentLabels, index, ref _prevUiMotionScaleIndex);
        }

        // ----------------------------------------------------------
        // CALLBACKS - GRAPHICS
        // ----------------------------------------------------------

        private void OnPresetLow()
        {
            SetCachedQualityPreset(0);
        }

        private void OnPresetMedium()
        {
            SetCachedQualityPreset(1);
        }

        private void OnPresetHigh()
        {
            SetCachedQualityPreset(2);
        }

        private void OnPresetUltra()
        {
            SetCachedQualityPreset(3);
        }

        private void SetCachedQualityPreset(int preset)
        {
            int clampedPreset = Mathf.Clamp(preset, 0, SettingsManager.MaxGraphicsPreset);
            _cachedGraphicsPreset = clampedPreset;

            switch (clampedPreset)
            {
                case 0:
                    _cachedQualityLevel = 1;
                    _cachedShadowQuality = 1;
                    _cachedShadowDistance = 50f;
                    _cachedAntiAliasing = 1;
                    _cachedAmbientOcclusion = false;
                    _cachedBloom = false;
                    _cachedMotionBlur = false;
                    _cachedTextureQuality = 0;
                    break;

                case 1:
                    _cachedQualityLevel = 3;
                    _cachedShadowQuality = 2;
                    _cachedShadowDistance = 100f;
                    _cachedAntiAliasing = 2;
                    _cachedAmbientOcclusion = false;
                    _cachedBloom = true;
                    _cachedMotionBlur = false;
                    _cachedTextureQuality = 1;
                    break;

                case 2:
                    _cachedQualityLevel = 4;
                    _cachedShadowQuality = 2;
                    _cachedShadowDistance = 200f;
                    _cachedAntiAliasing = 2;
                    _cachedAmbientOcclusion = true;
                    _cachedBloom = true;
                    _cachedMotionBlur = false;
                    _cachedTextureQuality = 2;
                    break;

                default:
                    _cachedQualityLevel = SettingsManager.MaxContinuousQualityLevel;
                    _cachedShadowQuality = 3;
                    _cachedShadowDistance = 300f;
                    _cachedAntiAliasing = 3;
                    _cachedAmbientOcclusion = true;
                    _cachedBloom = true;
                    _cachedMotionBlur = true;
                    _cachedTextureQuality = 3;
                    break;
            }

            RefreshAllUI();
            UpdatePostProcessingPreview();

            if (comparisonView != null)
                comparisonView.UpdateComparison(clampedPreset);
        }

        private void OnQualityDecrease()
        {
            _cachedQualityLevel = Mathf.Clamp(_cachedQualityLevel - 1, 0, SettingsManager.MaxContinuousQualityLevel);
            RefreshQualityUI();
        }

        private void OnQualityIncrease()
        {
            _cachedQualityLevel = Mathf.Clamp(_cachedQualityLevel + 1, 0, SettingsManager.MaxContinuousQualityLevel);
            RefreshQualityUI();
        }

        private void OnVsyncChanged(bool value)
        {
            _cachedVsync = value;
        }

        private void OnFullscreenChanged(bool value)
        {
            _cachedFullscreen = value;
        }

        private void OnFieldOfViewChanged(float value)
        {
            // Throttle slider updates
            if (ResolvePresentationClockSeconds() < _nextSliderUpdateTime)
                return;

            _nextSliderUpdateTime = ResolvePresentationClockSeconds() + sliderThrottleInterval;

            _cachedFieldOfView = value;

            // ZERO-GC: Use cached strings and dirty flags
            if (txtFieldOfView != null)
            {
                int fov = Mathf.RoundToInt(value);
                int index = Mathf.Clamp(fov - 60, 0, 50);
                SetCachedLabelIfChanged(txtFieldOfView, FOVLabels, index, ref _prevFOVIndex);
            }

            // Live preview
            if (livePreview != null)
                livePreview.PreviewFOV(value);
        }

        private void OnShadowQualityDecrease()
        {
            _cachedShadowQuality = Mathf.Clamp(_cachedShadowQuality - 1, 0, 3);
            if (txtShadowQuality != null && _cachedShadowQuality >= 0 && _cachedShadowQuality < ShadowQualityNames.Length)
                SetValueTextIfChanged(txtShadowQuality, ResolveLocalizedShadowQualityName(_cachedShadowQuality), ref _prevShadowQualityTextHash);
        }

        private void OnShadowQualityIncrease()
        {
            _cachedShadowQuality = Mathf.Clamp(_cachedShadowQuality + 1, 0, 3);
            if (txtShadowQuality != null && _cachedShadowQuality >= 0 && _cachedShadowQuality < ShadowQualityNames.Length)
                SetValueTextIfChanged(txtShadowQuality, ResolveLocalizedShadowQualityName(_cachedShadowQuality), ref _prevShadowQualityTextHash);
        }

        private void OnShadowDistanceChanged(float value)
        {
            // Throttle slider updates
            if (ResolvePresentationClockSeconds() < _nextSliderUpdateTime)
                return;

            _nextSliderUpdateTime = ResolvePresentationClockSeconds() + sliderThrottleInterval;

            _cachedShadowDistance = value;

            // ZERO-GC: Use cached strings and dirty flags
            if (txtShadowDistance != null)
            {
                int distance = Mathf.RoundToInt(value);
                int index = Mathf.Clamp(distance - 50, 0, 250);
                SetCachedLabelIfChanged(txtShadowDistance, ShadowDistanceLabels, index, ref _prevShadowDistanceIndex);
            }
        }

        private void OnTextScaleChanged(float value)
        {
            if (ResolvePresentationClockSeconds() < _nextSliderUpdateTime)
                return;

            _nextSliderUpdateTime = ResolvePresentationClockSeconds() + sliderThrottleInterval;
            _cachedTextScale = SanitizeTextScale(value);

            if (sliderTextScale != null && math.abs(sliderTextScale.value - _cachedTextScale) > 0.0001f)
                sliderTextScale.SetValueWithoutNotify(_cachedTextScale);

            RefreshTextScaleValueLabel();
        }

        private void OnUiMotionScaleChanged(float value)
        {
            if (ResolvePresentationClockSeconds() < _nextSliderUpdateTime)
                return;

            _nextSliderUpdateTime = ResolvePresentationClockSeconds() + sliderThrottleInterval;
            _cachedUiMotionScale = SanitizeUiMotionScale(value);

            if (sliderUiMotionScale != null && math.abs(sliderUiMotionScale.value - _cachedUiMotionScale) > 0.0001f)
                sliderUiMotionScale.SetValueWithoutNotify(_cachedUiMotionScale);

            RefreshUiMotionScaleValueLabel();
        }

        private void OnAntiAliasingDecrease()
        {
            _cachedAntiAliasing = Mathf.Clamp(_cachedAntiAliasing - 1, 0, 3);
            if (txtAntiAliasing != null && _cachedAntiAliasing >= 0 && _cachedAntiAliasing < AntiAliasingNames.Length)
                SetValueTextIfChanged(txtAntiAliasing, ResolveLocalizedAntiAliasingName(_cachedAntiAliasing), ref _prevAntiAliasingTextHash);
        }

        private void OnAntiAliasingIncrease()
        {
            _cachedAntiAliasing = Mathf.Clamp(_cachedAntiAliasing + 1, 0, 3);
            if (txtAntiAliasing != null && _cachedAntiAliasing >= 0 && _cachedAntiAliasing < AntiAliasingNames.Length)
                SetValueTextIfChanged(txtAntiAliasing, ResolveLocalizedAntiAliasingName(_cachedAntiAliasing), ref _prevAntiAliasingTextHash);
        }

        private void OnAmbientOcclusionChanged(bool value)
        {
            // Debounce toggle changes
            if (ResolvePresentationClockSeconds() < _nextToggleUpdateTime)
                return;

            _nextToggleUpdateTime = ResolvePresentationClockSeconds() + toggleDebounceInterval;

            _cachedAmbientOcclusion = value;
            UpdatePostProcessingPreview();
        }

        private void OnBloomChanged(bool value)
        {
            // Debounce toggle changes
            if (ResolvePresentationClockSeconds() < _nextToggleUpdateTime)
                return;

            _nextToggleUpdateTime = ResolvePresentationClockSeconds() + toggleDebounceInterval;

            _cachedBloom = value;
            UpdatePostProcessingPreview();
        }

        private void OnMotionBlurChanged(bool value)
        {
            // Debounce toggle changes
            if (ResolvePresentationClockSeconds() < _nextToggleUpdateTime)
                return;

            _nextToggleUpdateTime = ResolvePresentationClockSeconds() + toggleDebounceInterval;

            _cachedMotionBlur = value;
            UpdatePostProcessingPreview();
        }

        private void UpdatePostProcessingPreview()
        {
            if (livePreview != null)
                livePreview.PreviewPostProcessing(_cachedAmbientOcclusion, _cachedBloom, _cachedMotionBlur);
        }

        private void OnTextureQualityDecrease()
        {
            _cachedTextureQuality = Mathf.Clamp(_cachedTextureQuality - 1, 0, 3);
            if (txtTextureQuality != null && _cachedTextureQuality >= 0 && _cachedTextureQuality < TextureQualityNames.Length)
                SetValueTextIfChanged(txtTextureQuality, ResolveLocalizedTextureQualityName(_cachedTextureQuality), ref _prevTextureQualityTextHash);
        }

        private void OnTextureQualityIncrease()
        {
            _cachedTextureQuality = Mathf.Clamp(_cachedTextureQuality + 1, 0, 3);
            if (txtTextureQuality != null && _cachedTextureQuality >= 0 && _cachedTextureQuality < TextureQualityNames.Length)
                SetValueTextIfChanged(txtTextureQuality, ResolveLocalizedTextureQualityName(_cachedTextureQuality), ref _prevTextureQualityTextHash);
        }

        private void OnMenuStyleDecrease()
        {
            SetMenuVisualStyleIndex(WrapMenuVisualStyleIndex(_cachedMenuVisualStyleIndex - 1));
        }

        private void OnMenuStyleIncrease()
        {
            SetMenuVisualStyleIndex(WrapMenuVisualStyleIndex(_cachedMenuVisualStyleIndex + 1));
        }

        private void OnMenuConceptDecrease()
        {
            SetMenuVisualConceptIndex(WrapMenuVisualConceptIndex(_cachedMenuVisualConceptIndex - 1));
        }

        private void OnMenuConceptIncrease()
        {
            SetMenuVisualConceptIndex(WrapMenuVisualConceptIndex(_cachedMenuVisualConceptIndex + 1));
        }

        private void SetMenuVisualStyleIndex(int index)
        {
            if (_settings == null)
                return;

            int styleIndex = WrapMenuVisualStyleIndex(index);
            if (_cachedMenuVisualStyleIndex == styleIndex)
                return;

            _cachedMenuVisualStyleIndex = styleIndex;
            _settings.PreviewMenuVisualStyle(MenuVisualStyleCatalog.FromIndex(styleIndex));
            RefreshMenuVisualStyleUI();
        }

        private void SetMenuVisualConceptIndex(int index)
        {
            if (_settings == null)
                return;

            int conceptIndex = WrapMenuVisualConceptIndex(index);
            if (_cachedMenuVisualConceptIndex == conceptIndex)
                return;

            _cachedMenuVisualConceptIndex = conceptIndex;
            _settings.PreviewMenuVisualConcept(MenuVisualConceptCatalog.FromIndex(conceptIndex));
            RefreshMenuVisualConceptUI();
        }

        private static int WrapMenuVisualStyleIndex(int index)
        {
            if (index < 0)
                return MenuVisualStyleCatalog.StyleCount - 1;
            if (index >= MenuVisualStyleCatalog.StyleCount)
                return 0;

            return index;
        }

        private static int WrapMenuVisualConceptIndex(int index)
        {
            if (index < 0)
                return MenuVisualConceptCatalog.ConceptCount - 1;
            if (index >= MenuVisualConceptCatalog.ConceptCount)
                return 0;

            return index;
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RefreshAllUI();
            RefreshLocalizedLabels();
            modMenuController?.RefreshView();
        }

        private void RefreshLocalizedLabels()
        {
            ApplyLocalizedLabel("Container/Section_Presets/Label_Presets", SettingsPresetsKeyHash, "PRESETS");
            ApplyLocalizedLabel("Container/Section_Graphics/Label_Graphics", SettingsGraphicsKeyHash, "GRAPHICS");
            ApplyLocalizedLabel("Container/Section_Audio/Label_Audio", SettingsAudioKeyHash, "AUDIO");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_QualityLevel/Label_Row_QualityLevel", SettingsQualityLevelKeyHash, "QUALITY LEVEL");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_FOV/Label_Row_FOV", SettingsFovKeyHash, "FIELD OF VIEW");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_ShadowDistance/Label_Row_ShadowDistance", SettingsShadowDistanceKeyHash, "SHADOW DISTANCE");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_ShadowQuality/Label_Row_ShadowQuality", SettingsShadowQualityKeyHash, "SHADOW QUALITY");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_AntiAliasing/Label_Row_AntiAliasing", SettingsAntiAliasingKeyHash, "ANTI-ALIASING");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_TextureQuality/Label_Row_TextureQuality", SettingsTextureQualityKeyHash, "TEXTURE QUALITY");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_MenuVisualStyle/Label_Row_MenuVisualStyle", SettingsMenuVisualStyleKeyHash, "MENU STYLE");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_MenuVisualConcept/Label_Row_MenuVisualConcept", SettingsMenuVisualConceptKeyHash, "MENU CONCEPT");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_TextScale/Label_Row_TextScale", SettingsTextScaleKeyHash, "TEXT SCALE");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_UiMotionScale/Label_Row_UiMotionScale", SettingsUiMotionScaleKeyHash, "UI MOTION");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_Toggles/Toggle_Vsync/Label", SettingsVsyncKeyHash, "V-SYNC");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_Toggles/Toggle_Fullscreen/Label", SettingsFullscreenKeyHash, "FULLSCREEN");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_Toggles/Toggle_AO/Label", SettingsAoKeyHash, "AMBIENT OCCLUSION");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_Toggles/Toggle_Bloom/Label", SettingsBloomKeyHash, "BLOOM");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_Toggles/Toggle_MotionBlur/Label", SettingsMotionBlurKeyHash, "MOTION BLUR");
            ApplyLocalizedLabel("Container/Section_Audio/Row_MasterVolume/Label_Row_MasterVolume", SettingsMasterVolumeKeyHash, "MASTER");
            ApplyLocalizedLabel("Container/Section_Audio/Row_MusicVolume/Label_Row_MusicVolume", SettingsMusicVolumeKeyHash, "MUSIC");
            ApplyLocalizedLabel("Container/Section_Audio/Row_SfxVolume/Label_Row_SfxVolume", SettingsSfxVolumeKeyHash, "SFX");
            ApplyLocalizedLabel("Container/Section_Audio/Row_AmbientVolume/Label_Row_AmbientVolume", SettingsAmbientVolumeKeyHash, "AMBIENT");
            ApplyLocalizedLabel("Container/Row_Actions/Btn_ResetDefaults/Text", SettingsResetDefaultsKeyHash, "RESET");
            ApplyLocalizedLabel("Container/Row_Actions/Btn_Apply/Text", SettingsApplyKeyHash, "APPLY");
            ApplyLocalizedLabel("Container/Row_Actions/Btn_Cancel/Text", SettingsCancelKeyHash, "CANCEL");
        }

        private void ApplyLocalizedLabel(string relativePath, int keyHash, string fallback)
        {
            Transform target = transform.Find(relativePath);
            if (target == null)
                return;

            if (!target.TryGetComponent(out TMP_Text label))
                return;

            SetTextIfChanged(label, ResolveLocalizedSpan(keyHash, fallback));
            LocalizedTMPAutoSizer.Configure(label, label.fontSize * 0.72f, label.fontSize, TextOverflowModes.Ellipsis, TextWrappingModes.NoWrap);
        }

        private static void SetValueTextIfChanged(TMP_Text label, ReadOnlySpan<char> text, ref uint previousHash)
        {
            if (label == null)
                return;

            uint textHash = ComputeUiTextHash(text);
            if (previousHash == textHash)
                return;

            TmpTextNoAlloc.Set(label, text);
            previousHash = textHash;
        }

        private static void SetIndexedMenuVisualLabelIfChanged(
            TMP_Text label,
            int oneBasedIndex,
            int totalCount,
            ReadOnlySpan<char> value,
            char[] buffer,
            ref uint previousHash)
        {
            if (label == null || buffer == null || buffer.Length == 0)
                return;

            int cursor = 0;
            cursor += CopyTwoDigitPositiveIntToBuffer(oneBasedIndex, buffer, cursor);
            cursor += CopySpanToBuffer("/".AsSpan(), buffer, cursor);
            cursor += CopyTwoDigitPositiveIntToBuffer(totalCount, buffer, cursor);
            cursor += CopySpanToBuffer(" ".AsSpan(), buffer, cursor);
            cursor += CopySpanToBuffer(value, buffer, cursor);

            ReadOnlySpan<char> text = buffer.AsSpan(0, cursor);
            uint textHash = ComputeUiTextHash(text);
            if (previousHash == textHash)
                return;

            label.SetCharArray(buffer, 0, cursor);
            previousHash = textHash;
        }

        private static void SetCachedLabelIfChanged(TMP_Text label, CachedTextLabel[] labels, int index, ref int previousIndex)
        {
            if (label == null || labels == null || index < 0 || index >= labels.Length || previousIndex == index)
                return;

            CachedTextLabel cached = labels[index];
            if (cached.Buffer == null || cached.Length <= 0)
                return;

            label.SetCharArray(cached.Buffer, 0, cached.Length);
            previousIndex = index;
        }

        private static uint ComputeUiTextHash(ReadOnlySpan<char> text)
        {
            uint hash = UiTextHashSeed;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= UiTextHashPrime;
            }

            return hash;
        }

        private static void SetTextIfChanged(TMP_Text label, ReadOnlySpan<char> text)
        {
            if (label != null)
                TmpTextNoAlloc.Set(label, text);
        }

        private static float ResolvePresentationClockSeconds()
        {
            return (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
        }

        private static float SanitizeTextScale(float scale)
        {
            if (!math.isfinite(scale) || scale <= 0f)
                return AccessibilitySettings.DefaultTextScale;

            return math.clamp(scale, AccessibilitySettings.MinimumTextScale, AccessibilitySettings.MaximumTextScale);
        }

        private static int ResolveTextScaleLabelIndex(float scale)
        {
            float safeScale = SanitizeTextScale(scale);
            int percent = Mathf.RoundToInt(safeScale * 100f);
            return Mathf.Clamp(percent - TextScalePercentMin, 0, TextScalePercentLabelCount - 1);
        }

        private static float SanitizeUiMotionScale(float scale)
        {
            if (!math.isfinite(scale))
                return AccessibilitySettings.DefaultUiMotionScale;

            return math.clamp(scale, AccessibilitySettings.MinimumUiMotionScale, AccessibilitySettings.MaximumUiMotionScale);
        }

        private static int ResolveUiMotionScaleLabelIndex(float scale)
        {
            float safeScale = SanitizeUiMotionScale(scale);
            int percent = Mathf.RoundToInt(safeScale * 100f);
            return Mathf.Clamp(percent, 0, 100);
        }

        private static ReadOnlySpan<char> ResolveLocalizedSpan(int keyHash, string fallback)
        {
            ILocalizationTextReadModel manager = s_localization;
            if (manager == null)
                return fallback.AsSpan();

            return manager.GetRawSpanOrFallback(keyHash, fallback.AsSpan());
        }

        private static ReadOnlySpan<char> ResolveLocalizedShadowQualityName(int shadowQualityIndex)
        {
            return shadowQualityIndex switch
            {
                0 => ResolveLocalizedSpan(SettingsValueOffKeyHash, "OFF"),
                1 => ResolveLocalizedSpan(SettingsPresetLowKeyHash, "LOW"),
                2 => ResolveLocalizedSpan(SettingsPresetMediumKeyHash, "MEDIUM"),
                3 => ResolveLocalizedSpan(SettingsPresetHighKeyHash, "HIGH"),
                _ => ResolveLocalizedSpan(SettingsValueOffKeyHash, "OFF")
            };
        }

        private static ReadOnlySpan<char> ResolveLocalizedTextureQualityName(int textureQualityIndex)
        {
            return textureQualityIndex switch
            {
                0 => ResolveLocalizedSpan(SettingsPresetLowKeyHash, "LOW"),
                1 => ResolveLocalizedSpan(SettingsPresetMediumKeyHash, "MEDIUM"),
                2 => ResolveLocalizedSpan(SettingsPresetHighKeyHash, "HIGH"),
                3 => ResolveLocalizedSpan(SettingsPresetUltraKeyHash, "ULTRA"),
                _ => ResolveLocalizedSpan(SettingsPresetLowKeyHash, "LOW")
            };
        }

        private static ReadOnlySpan<char> ResolveLocalizedAntiAliasingName(int antiAliasingIndex)
        {
            if (antiAliasingIndex <= 0)
                return ResolveLocalizedSpan(SettingsValueOffKeyHash, "OFF");

            return AntiAliasingNames[Mathf.Clamp(antiAliasingIndex, 0, AntiAliasingNames.Length - 1)].AsSpan();
        }

        private static ReadOnlySpan<char> ResolveLocalizedQualityName(int qualityIndex)
        {
            return qualityIndex switch
            {
                0 => "SURVIVAL".AsSpan(),
                1 => ResolveLocalizedSpan(SettingsPresetLowKeyHash, "LOW"),
                2 => "LEAN".AsSpan(),
                3 => ResolveLocalizedSpan(SettingsPresetMediumKeyHash, "MEDIUM"),
                4 => ResolveLocalizedSpan(SettingsPresetHighKeyHash, "HIGH"),
                5 => ResolveLocalizedSpan(SettingsPresetUltraKeyHash, "ULTRA"),
                6 => "OVERKILL".AsSpan(),
                _ => "--".AsSpan()
            };
        }

        // ----------------------------------------------------------
        // CALLBACKS - AUDIO
        // ----------------------------------------------------------

        private void OnMasterVolumeChanged(float value)
        {
            // Throttle slider updates
            if (ResolvePresentationClockSeconds() < _nextSliderUpdateTime)
                return;

            _nextSliderUpdateTime = ResolvePresentationClockSeconds() + sliderThrottleInterval;

            _cachedMasterVolume = value;

            // ZERO-GC: Use cached strings and dirty flags
            if (txtMasterVolume != null)
            {
                int percent = Mathf.RoundToInt(value * 100f);
                int labelIndex = Mathf.Clamp(percent, 0, 100);
                SetCachedLabelIfChanged(txtMasterVolume, VolumePercentLabels, labelIndex, ref _prevMasterVolumeIndex);
            }
        }

        private void OnMusicVolumeChanged(float value)
        {
            // Throttle slider updates
            if (ResolvePresentationClockSeconds() < _nextSliderUpdateTime)
                return;

            _nextSliderUpdateTime = ResolvePresentationClockSeconds() + sliderThrottleInterval;

            _cachedMusicVolume = value;

            // ZERO-GC: Use cached strings and dirty flags
            if (txtMusicVolume != null)
            {
                int percent = Mathf.RoundToInt(value * 100f);
                int labelIndex = Mathf.Clamp(percent, 0, 100);
                SetCachedLabelIfChanged(txtMusicVolume, VolumePercentLabels, labelIndex, ref _prevMusicVolumeIndex);
            }
        }

        private void OnSfxVolumeChanged(float value)
        {
            // Throttle slider updates
            if (ResolvePresentationClockSeconds() < _nextSliderUpdateTime)
                return;

            _nextSliderUpdateTime = ResolvePresentationClockSeconds() + sliderThrottleInterval;

            _cachedSfxVolume = value;

            // ZERO-GC: Use cached strings and dirty flags
            if (txtSfxVolume != null)
            {
                int percent = Mathf.RoundToInt(value * 100f);
                int labelIndex = Mathf.Clamp(percent, 0, 100);
                SetCachedLabelIfChanged(txtSfxVolume, VolumePercentLabels, labelIndex, ref _prevSfxVolumeIndex);
            }
        }

        private void OnAmbientVolumeChanged(float value)
        {
            // Throttle slider updates
            if (ResolvePresentationClockSeconds() < _nextSliderUpdateTime)
                return;

            _nextSliderUpdateTime = ResolvePresentationClockSeconds() + sliderThrottleInterval;

            _cachedAmbientVolume = value;

            // ZERO-GC: Use cached strings and dirty flags
            if (txtAmbientVolume != null)
            {
                int percent = Mathf.RoundToInt(value * 100f);
                int labelIndex = Mathf.Clamp(percent, 0, 100);
                SetCachedLabelIfChanged(txtAmbientVolume, VolumePercentLabels, labelIndex, ref _prevAmbientVolumeIndex);
            }
        }

        // ----------------------------------------------------------
        // CALLBACKS - ACTIONS
        // ----------------------------------------------------------

        private void OnResetDefaults()
        {
            if (_settings == null)
                return;

            _settings.ResetToDefaults();
            LoadCurrentSettings();
            CaptureMenuVisualCancelSnapshot();
            RefreshAllUI();
        }

        public void CancelPendingChanges()
        {
            if (_settings == null)
            {
                if (livePreview != null)
                    livePreview.CancelPending();
                return;
            }

            OnCancel();
        }

        public int ReadableCachedQualityLevel => Mathf.Clamp(_cachedQualityLevel, 0, SettingsManager.MaxContinuousQualityLevel);

        public int ReadableCachedGraphicsPreset => Mathf.Clamp(_cachedGraphicsPreset, 0, SettingsManager.MaxGraphicsPreset);

        public int ReadableCachedMenuVisualStyleIndex => MenuVisualStyleCatalog.ClampStyleIndex(_cachedMenuVisualStyleIndex);

        public int ReadableCachedMenuVisualConceptIndex => MenuVisualConceptCatalog.ClampConceptIndex(_cachedMenuVisualConceptIndex);

        public float ReadableCachedTextScale => SanitizeTextScale(_cachedTextScale);

        public float ReadableCachedUiMotionScale => SanitizeUiMotionScale(_cachedUiMotionScale);

        public void ReadableSelectGraphicsPreset(int preset)
        {
            SetCachedQualityPreset(preset);
        }

        public void ReadableQualityDecrease()
        {
            OnQualityDecrease();
        }

        public void ReadableQualityIncrease()
        {
            OnQualityIncrease();
        }

        public void ReadableMenuStyleDecrease()
        {
            OnMenuStyleDecrease();
        }

        public void ReadableMenuStyleIncrease()
        {
            OnMenuStyleIncrease();
        }

        public void ReadableMenuConceptDecrease()
        {
            OnMenuConceptDecrease();
        }

        public void ReadableMenuConceptIncrease()
        {
            OnMenuConceptIncrease();
        }

        public void ReadableResetDefaults()
        {
            OnResetDefaults();
        }

        public void ReadableApply()
        {
            OnApply();
        }

        public void ReadableCancel()
        {
            OnCancel();
        }

        private void OnApply()
        {
            if (_settings == null)
                return;

            // Apply button cooldown guard
            if (ResolvePresentationClockSeconds() < _nextApplyTime)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[SettingsPanel] Apply button on cooldown. Please wait.");
#endif
                return;
            }

            _nextApplyTime = ResolvePresentationClockSeconds() + applyButtonCooldown;

            bool success;
            _settings.BeginPersistenceBatch();
            try
            {
                // Apply live preview immediately
                if (livePreview != null)
                    livePreview.ApplyImmediately();

                if (_cachedGraphicsPreset >= 0)
                    _settings.ApplyQualityPreset(_cachedGraphicsPreset);

                // Apply all cached settings to SettingsManager
                _settings.QualityLevel = _cachedQualityLevel;
                _settings.MasterVolume = _cachedMasterVolume;
                _settings.MusicVolume = _cachedMusicVolume;
                _settings.SfxVolume = _cachedSfxVolume;
                _settings.AmbientVolume = _cachedAmbientVolume;
                _settings.Vsync = _cachedVsync;
                _settings.Fullscreen = _cachedFullscreen;
                _settings.FieldOfView = _cachedFieldOfView;
                _settings.ShadowQuality = _cachedShadowQuality;
                _settings.ShadowDistance = _cachedShadowDistance;
                _settings.AntiAliasing = _cachedAntiAliasing;
                _settings.AmbientOcclusion = _cachedAmbientOcclusion;
                _settings.Bloom = _cachedBloom;
                _settings.MotionBlur = _cachedMotionBlur;
                _settings.TextureQuality = _cachedTextureQuality;
                _settings.MenuVisualStyle = MenuVisualStyleCatalog.FromIndex(_cachedMenuVisualStyleIndex);
                _settings.MenuVisualConcept = MenuVisualConceptCatalog.FromIndex(_cachedMenuVisualConceptIndex);
                _settings.TextScale = _cachedTextScale;
                _settings.UiMotionScale = _cachedUiMotionScale;
                CaptureMenuVisualCancelSnapshot();

                // Verify all settings applied successfully
                success = _settings.ApplyAllSettings();
            }
            finally
            {
                _settings.EndPersistenceBatch();
            }

            if (!success)
            {
                int messageLength = CopyLocalizedSpanToModalBuffer(
                    ErrorSettingsUnavailableKeyHash,
                    "Some settings failed to apply. Check console for details.\n\nRetry or revert to defaults?");

                Hecton.UI.MainMenu.ModalWindow.ShowWithCustomLabels(
                    "Settings Apply Failed",
                    _modalMessageBuffer,
                    messageLength,
                    _retryApplyModalAction,
                    _resetDefaultsModalAction,
                    "Retry",
                    "Revert to Defaults");
            }
        }

        private void OnCancel()
        {
            // Cancel live preview
            if (livePreview != null)
                livePreview.CancelPending();

            RestoreMenuVisualCancelSnapshot();
            LoadCurrentSettings();
            RefreshAllUI();
        }

        private int CopyLocalizedSpanToModalBuffer(int keyHash, string fallback)
        {
            return CopySpanToBuffer(ResolveLocalizedSpan(keyHash, fallback), _modalMessageBuffer, 0);
        }

        private static int CopySpanToBuffer(ReadOnlySpan<char> value, char[] buffer, int offset)
        {
            if (value.Length == 0 || buffer == null || offset >= buffer.Length)
                return 0;

            int safeLength = math.min(value.Length, buffer.Length - offset);
            value.Slice(0, safeLength).CopyTo(buffer.AsSpan(offset, safeLength));
            return safeLength;
        }

        private static int CopyTwoDigitPositiveIntToBuffer(int value, char[] buffer, int offset)
        {
            if (buffer == null || offset >= buffer.Length)
                return 0;

            int safeValue = math.clamp(value, 0, 99);
            int written = 0;
            if (safeValue < 10 && offset < buffer.Length)
            {
                buffer[offset] = '0';
                offset++;
                written++;
            }

            if (safeValue.TryFormat(buffer.AsSpan(offset), out int digitsWritten))
                return written + digitsWritten;

            return written;
        }
    }
}
