using TMPro;
using UnityEngine;
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
    public sealed class SettingsPanel : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        // INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("=== GRAPHICS ===")]
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

        [Header("=== PERFORMANCE ===")]
        [SerializeField, Tooltip("Apply button cooldown (seconds)")]
        private float applyButtonCooldown = 0.5f;

        [SerializeField, Tooltip("Slider throttle interval (seconds)")]
        private float sliderThrottleInterval = 0.1f;

        [SerializeField, Tooltip("Toggle debounce interval (seconds)")]
        private float toggleDebounceInterval = 0.05f;

        // ══════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════

        private SettingsManager _settings;
        private bool _initialized;

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

        private static readonly string[] ShadowQualityNames = { "Off", "Low", "Medium", "High" };
        private static readonly string[] AntiAliasingNames = { "None", "FXAA", "SMAA", "TAA" };
        private static readonly string[] TextureQualityNames = { "Low", "Medium", "High", "Ultra" };

        // ZERO-GC: Cached strings for volume/FOV/shadow distance display
        private static readonly string[] VolumePercentStrings = new string[101]; // COLD ALLOC: string[101] — volume percentage strings 0-100% — owner: SettingsPanel
        private static readonly string[] FOVStrings = new string[51]; // COLD ALLOC: string[51] — FOV strings 60-110° — owner: SettingsPanel
        private static readonly string[] ShadowDistanceStrings = new string[251]; // COLD ALLOC: string[251] — shadow distance strings 50-300m — owner: SettingsPanel

        // ZERO-GC: Dirty flags to prevent unnecessary SetText calls
        private string _prevMasterVolumeText = null;
        private string _prevMusicVolumeText = null;
        private string _prevSfxVolumeText = null;
        private string _prevAmbientVolumeText = null;
        private string _prevFOVText = null;
        private string _prevShadowDistanceText = null;
        private string _prevQualityLevelText = null;
        private string _prevShadowQualityText = null;
        private string _prevAntiAliasingText = null;
        private string _prevTextureQualityText = null;

        // ZERO-GC: Static constructor to pre-generate all display strings
        static SettingsPanel()
        {
            // Pre-generate volume percentage strings (0-100%)
            for (int i = 0; i <= 100; i++)
            {
                VolumePercentStrings[i] = i.ToString() + "%";
            }

            // Pre-generate FOV strings (60-110°)
            for (int i = 0; i <= 50; i++)
            {
                FOVStrings[i] = (60 + i).ToString() + "°";
            }

            // Pre-generate shadow distance strings (50-300m)
            for (int i = 0; i <= 250; i++)
            {
                ShadowDistanceStrings[i] = (50 + i).ToString() + "m";
            }
        }

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            BindButtons();
        }

        private void OnEnable()
        {
            if (!_initialized)
                Initialize();

            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            LoadCurrentSettings();
            RefreshAllUI();
            RefreshLocalizedLabels();

            // Play fade-in animation
            if (panelAnimator != null)
                panelAnimator.PlayFadeIn();

            // Show comparison view
            if (comparisonView != null)
                comparisonView.Show();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
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

        // ══════════════════════════════════════════════════════════
        // INITIALIZATION
        // ══════════════════════════════════════════════════════════

        private void Initialize()
        {
            _settings = SettingsManager.Instance;
            if (_settings == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[SettingsPanel] SettingsManager.Instance is null. Settings unavailable.");
#endif
                return;
            }

            BindSliders();
            _initialized = true;
        }

        private void BindButtons()
        {
            if (btnPresetLow != null)
            {
                btnPresetLow.onClick.RemoveAllListeners();
                btnPresetLow.onClick.AddListener(OnPresetLow);
            }

            if (btnPresetMedium != null)
            {
                btnPresetMedium.onClick.RemoveAllListeners();
                btnPresetMedium.onClick.AddListener(OnPresetMedium);
            }

            if (btnPresetHigh != null)
            {
                btnPresetHigh.onClick.RemoveAllListeners();
                btnPresetHigh.onClick.AddListener(OnPresetHigh);
            }

            if (btnPresetUltra != null)
            {
                btnPresetUltra.onClick.RemoveAllListeners();
                btnPresetUltra.onClick.AddListener(OnPresetUltra);
            }

            if (btnQualityDecrease != null)
            {
                btnQualityDecrease.onClick.RemoveAllListeners();
                btnQualityDecrease.onClick.AddListener(OnQualityDecrease);
            }

            if (btnQualityIncrease != null)
            {
                btnQualityIncrease.onClick.RemoveAllListeners();
                btnQualityIncrease.onClick.AddListener(OnQualityIncrease);
            }

            if (toggleVsync != null)
            {
                toggleVsync.onValueChanged.RemoveAllListeners();
                toggleVsync.onValueChanged.AddListener(OnVsyncChanged);
            }

            if (toggleFullscreen != null)
            {
                toggleFullscreen.onValueChanged.RemoveAllListeners();
                toggleFullscreen.onValueChanged.AddListener(OnFullscreenChanged);
            }

            if (btnShadowQualityDecrease != null)
            {
                btnShadowQualityDecrease.onClick.RemoveAllListeners();
                btnShadowQualityDecrease.onClick.AddListener(OnShadowQualityDecrease);
            }

            if (btnShadowQualityIncrease != null)
            {
                btnShadowQualityIncrease.onClick.RemoveAllListeners();
                btnShadowQualityIncrease.onClick.AddListener(OnShadowQualityIncrease);
            }

            if (btnAntiAliasingDecrease != null)
            {
                btnAntiAliasingDecrease.onClick.RemoveAllListeners();
                btnAntiAliasingDecrease.onClick.AddListener(OnAntiAliasingDecrease);
            }

            if (btnAntiAliasingIncrease != null)
            {
                btnAntiAliasingIncrease.onClick.RemoveAllListeners();
                btnAntiAliasingIncrease.onClick.AddListener(OnAntiAliasingIncrease);
            }

            if (btnTextureQualityDecrease != null)
            {
                btnTextureQualityDecrease.onClick.RemoveAllListeners();
                btnTextureQualityDecrease.onClick.AddListener(OnTextureQualityDecrease);
            }

            if (btnTextureQualityIncrease != null)
            {
                btnTextureQualityIncrease.onClick.RemoveAllListeners();
                btnTextureQualityIncrease.onClick.AddListener(OnTextureQualityIncrease);
            }

            if (toggleAmbientOcclusion != null)
            {
                toggleAmbientOcclusion.onValueChanged.RemoveAllListeners();
                toggleAmbientOcclusion.onValueChanged.AddListener(OnAmbientOcclusionChanged);
            }

            if (toggleBloom != null)
            {
                toggleBloom.onValueChanged.RemoveAllListeners();
                toggleBloom.onValueChanged.AddListener(OnBloomChanged);
            }

            if (toggleMotionBlur != null)
            {
                toggleMotionBlur.onValueChanged.RemoveAllListeners();
                toggleMotionBlur.onValueChanged.AddListener(OnMotionBlurChanged);
            }

            if (btnResetDefaults != null)
            {
                btnResetDefaults.onClick.RemoveAllListeners();
                btnResetDefaults.onClick.AddListener(OnResetDefaults);
            }

            if (btnApply != null)
            {
                btnApply.onClick.RemoveAllListeners();
                btnApply.onClick.AddListener(OnApply);
            }

            if (btnCancel != null)
            {
                btnCancel.onClick.RemoveAllListeners();
                btnCancel.onClick.AddListener(OnCancel);
            }
        }

        private void BindSliders()
        {
            if (sliderMasterVolume != null)
                sliderMasterVolume.onValueChanged.AddListener(OnMasterVolumeChanged);

            if (sliderMusicVolume != null)
                sliderMusicVolume.onValueChanged.AddListener(OnMusicVolumeChanged);

            if (sliderSfxVolume != null)
                sliderSfxVolume.onValueChanged.AddListener(OnSfxVolumeChanged);

            if (sliderAmbientVolume != null)
                sliderAmbientVolume.onValueChanged.AddListener(OnAmbientVolumeChanged);

            if (sliderFieldOfView != null)
                sliderFieldOfView.onValueChanged.AddListener(OnFieldOfViewChanged);

            if (sliderShadowDistance != null)
                sliderShadowDistance.onValueChanged.AddListener(OnShadowDistanceChanged);
        }

        private void UnbindSliders()
        {
            if (sliderMasterVolume != null)
                sliderMasterVolume.onValueChanged.RemoveListener(OnMasterVolumeChanged);

            if (sliderMusicVolume != null)
                sliderMusicVolume.onValueChanged.RemoveListener(OnMusicVolumeChanged);

            if (sliderSfxVolume != null)
                sliderSfxVolume.onValueChanged.RemoveListener(OnSfxVolumeChanged);

            if (sliderAmbientVolume != null)
                sliderAmbientVolume.onValueChanged.RemoveListener(OnAmbientVolumeChanged);

            if (sliderFieldOfView != null)
                sliderFieldOfView.onValueChanged.RemoveListener(OnFieldOfViewChanged);

            if (sliderShadowDistance != null)
                sliderShadowDistance.onValueChanged.RemoveListener(OnShadowDistanceChanged);
        }

        // ══════════════════════════════════════════════════════════
        // LOAD/REFRESH
        // ══════════════════════════════════════════════════════════

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
        }

        private void RefreshAllUI()
        {
            RefreshQualityUI();
            RefreshVolumeUI();
            RefreshVideoUI();
            RefreshAdvancedGraphicsUI();
        }

        private void RefreshQualityUI()
        {
            if (txtQualityLevel == null)
                return;

            string[] qualityNames = QualitySettings.names;
            if (_cachedQualityLevel >= 0 && _cachedQualityLevel < qualityNames.Length)
                SetValueTextIfChanged(txtQualityLevel, ResolveLocalizedQualityName(qualityNames[_cachedQualityLevel]), ref _prevQualityLevelText);
            else
                SetValueTextIfChanged(txtQualityLevel, "--", ref _prevQualityLevelText);
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
                string text = VolumePercentStrings[Mathf.Clamp(percent, 0, 100)];
                if (_prevMasterVolumeText != text)
                {
                    txtMasterVolume.SetText(text);
                    _prevMasterVolumeText = text;
                }
            }

            if (txtMusicVolume != null)
            {
                int percent = Mathf.RoundToInt(_cachedMusicVolume * 100f);
                string text = VolumePercentStrings[Mathf.Clamp(percent, 0, 100)];
                if (_prevMusicVolumeText != text)
                {
                    txtMusicVolume.SetText(text);
                    _prevMusicVolumeText = text;
                }
            }

            if (txtSfxVolume != null)
            {
                int percent = Mathf.RoundToInt(_cachedSfxVolume * 100f);
                string text = VolumePercentStrings[Mathf.Clamp(percent, 0, 100)];
                if (_prevSfxVolumeText != text)
                {
                    txtSfxVolume.SetText(text);
                    _prevSfxVolumeText = text;
                }
            }

            if (txtAmbientVolume != null)
            {
                int percent = Mathf.RoundToInt(_cachedAmbientVolume * 100f);
                string text = VolumePercentStrings[Mathf.Clamp(percent, 0, 100)];
                if (_prevAmbientVolumeText != text)
                {
                    txtAmbientVolume.SetText(text);
                    _prevAmbientVolumeText = text;
                }
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
                string text = FOVStrings[index];
                if (_prevFOVText != text)
                {
                    txtFieldOfView.SetText(text);
                    _prevFOVText = text;
                }
            }

            if (txtShadowQuality != null && _cachedShadowQuality >= 0 && _cachedShadowQuality < ShadowQualityNames.Length)
                SetValueTextIfChanged(txtShadowQuality, ResolveLocalizedShadowQualityName(_cachedShadowQuality), ref _prevShadowQualityText);

            if (sliderShadowDistance != null)
                sliderShadowDistance.SetValueWithoutNotify(_cachedShadowDistance);

            // ZERO-GC: Use cached strings and dirty flags
            if (txtShadowDistance != null)
            {
                int distance = Mathf.RoundToInt(_cachedShadowDistance);
                int index = Mathf.Clamp(distance - 50, 0, 250);
                string text = ShadowDistanceStrings[index];
                if (_prevShadowDistanceText != text)
                {
                    txtShadowDistance.SetText(text);
                    _prevShadowDistanceText = text;
                }
            }

            if (txtAntiAliasing != null && _cachedAntiAliasing >= 0 && _cachedAntiAliasing < AntiAliasingNames.Length)
                SetValueTextIfChanged(txtAntiAliasing, ResolveLocalizedAntiAliasingName(_cachedAntiAliasing), ref _prevAntiAliasingText);

            if (toggleAmbientOcclusion != null)
                toggleAmbientOcclusion.SetIsOnWithoutNotify(_cachedAmbientOcclusion);

            if (toggleBloom != null)
                toggleBloom.SetIsOnWithoutNotify(_cachedBloom);

            if (toggleMotionBlur != null)
                toggleMotionBlur.SetIsOnWithoutNotify(_cachedMotionBlur);

            if (txtTextureQuality != null && _cachedTextureQuality >= 0 && _cachedTextureQuality < TextureQualityNames.Length)
                SetValueTextIfChanged(txtTextureQuality, ResolveLocalizedTextureQualityName(_cachedTextureQuality), ref _prevTextureQualityText);
        }

        // ══════════════════════════════════════════════════════════
        // CALLBACKS — GRAPHICS
        // ══════════════════════════════════════════════════════════

        private void OnPresetLow()
        {
            if (_settings == null)
                return;

            _settings.ApplyQualityPreset(0);
            LoadCurrentSettings();
            RefreshAllUI();

            // Update comparison view
            if (comparisonView != null)
                comparisonView.UpdateComparison(0);
        }

        private void OnPresetMedium()
        {
            if (_settings == null)
                return;

            _settings.ApplyQualityPreset(1);
            LoadCurrentSettings();
            RefreshAllUI();

            // Update comparison view
            if (comparisonView != null)
                comparisonView.UpdateComparison(1);
        }

        private void OnPresetHigh()
        {
            if (_settings == null)
                return;

            _settings.ApplyQualityPreset(2);
            LoadCurrentSettings();
            RefreshAllUI();

            // Update comparison view
            if (comparisonView != null)
                comparisonView.UpdateComparison(2);
        }

        private void OnPresetUltra()
        {
            if (_settings == null)
                return;

            _settings.ApplyQualityPreset(3);
            LoadCurrentSettings();
            RefreshAllUI();

            // Update comparison view
            if (comparisonView != null)
                comparisonView.UpdateComparison(3);
        }

        private void OnQualityDecrease()
        {
            int maxLevel = QualitySettings.names.Length - 1;
            _cachedQualityLevel = Mathf.Clamp(_cachedQualityLevel - 1, 0, maxLevel);
            RefreshQualityUI();
        }

        private void OnQualityIncrease()
        {
            int maxLevel = QualitySettings.names.Length - 1;
            _cachedQualityLevel = Mathf.Clamp(_cachedQualityLevel + 1, 0, maxLevel);
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
            if (Time.unscaledTime < _nextSliderUpdateTime)
                return;

            _nextSliderUpdateTime = Time.unscaledTime + sliderThrottleInterval;

            _cachedFieldOfView = value;
            
            // ZERO-GC: Use cached strings and dirty flags
            if (txtFieldOfView != null)
            {
                int fov = Mathf.RoundToInt(value);
                int index = Mathf.Clamp(fov - 60, 0, 50);
                string text = FOVStrings[index];
                if (_prevFOVText != text)
                {
                    txtFieldOfView.SetText(text);
                    _prevFOVText = text;
                }
            }

            // Live preview
            if (livePreview != null)
                livePreview.PreviewFOV(value);
        }

        private void OnShadowQualityDecrease()
        {
            _cachedShadowQuality = Mathf.Clamp(_cachedShadowQuality - 1, 0, 3);
            if (txtShadowQuality != null && _cachedShadowQuality >= 0 && _cachedShadowQuality < ShadowQualityNames.Length)
                SetValueTextIfChanged(txtShadowQuality, ResolveLocalizedShadowQualityName(_cachedShadowQuality), ref _prevShadowQualityText);
        }

        private void OnShadowQualityIncrease()
        {
            _cachedShadowQuality = Mathf.Clamp(_cachedShadowQuality + 1, 0, 3);
            if (txtShadowQuality != null && _cachedShadowQuality >= 0 && _cachedShadowQuality < ShadowQualityNames.Length)
                SetValueTextIfChanged(txtShadowQuality, ResolveLocalizedShadowQualityName(_cachedShadowQuality), ref _prevShadowQualityText);
        }

        private void OnShadowDistanceChanged(float value)
        {
            // Throttle slider updates
            if (Time.unscaledTime < _nextSliderUpdateTime)
                return;

            _nextSliderUpdateTime = Time.unscaledTime + sliderThrottleInterval;

            _cachedShadowDistance = value;
            
            // ZERO-GC: Use cached strings and dirty flags
            if (txtShadowDistance != null)
            {
                int distance = Mathf.RoundToInt(value);
                int index = Mathf.Clamp(distance - 50, 0, 250);
                string text = ShadowDistanceStrings[index];
                if (_prevShadowDistanceText != text)
                {
                    txtShadowDistance.SetText(text);
                    _prevShadowDistanceText = text;
                }
            }
        }

        private void OnAntiAliasingDecrease()
        {
            _cachedAntiAliasing = Mathf.Clamp(_cachedAntiAliasing - 1, 0, 3);
            if (txtAntiAliasing != null && _cachedAntiAliasing >= 0 && _cachedAntiAliasing < AntiAliasingNames.Length)
                SetValueTextIfChanged(txtAntiAliasing, ResolveLocalizedAntiAliasingName(_cachedAntiAliasing), ref _prevAntiAliasingText);
        }

        private void OnAntiAliasingIncrease()
        {
            _cachedAntiAliasing = Mathf.Clamp(_cachedAntiAliasing + 1, 0, 3);
            if (txtAntiAliasing != null && _cachedAntiAliasing >= 0 && _cachedAntiAliasing < AntiAliasingNames.Length)
                SetValueTextIfChanged(txtAntiAliasing, ResolveLocalizedAntiAliasingName(_cachedAntiAliasing), ref _prevAntiAliasingText);
        }

        private void OnAmbientOcclusionChanged(bool value)
        {
            // Debounce toggle changes
            if (Time.unscaledTime < _nextToggleUpdateTime)
                return;

            _nextToggleUpdateTime = Time.unscaledTime + toggleDebounceInterval;

            _cachedAmbientOcclusion = value;
            UpdatePostProcessingPreview();
        }

        private void OnBloomChanged(bool value)
        {
            // Debounce toggle changes
            if (Time.unscaledTime < _nextToggleUpdateTime)
                return;

            _nextToggleUpdateTime = Time.unscaledTime + toggleDebounceInterval;

            _cachedBloom = value;
            UpdatePostProcessingPreview();
        }

        private void OnMotionBlurChanged(bool value)
        {
            // Debounce toggle changes
            if (Time.unscaledTime < _nextToggleUpdateTime)
                return;

            _nextToggleUpdateTime = Time.unscaledTime + toggleDebounceInterval;

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
                SetValueTextIfChanged(txtTextureQuality, ResolveLocalizedTextureQualityName(_cachedTextureQuality), ref _prevTextureQualityText);
        }

        private void OnTextureQualityIncrease()
        {
            _cachedTextureQuality = Mathf.Clamp(_cachedTextureQuality + 1, 0, 3);
            if (txtTextureQuality != null && _cachedTextureQuality >= 0 && _cachedTextureQuality < TextureQualityNames.Length)
                SetValueTextIfChanged(txtTextureQuality, ResolveLocalizedTextureQualityName(_cachedTextureQuality), ref _prevTextureQualityText);
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            RefreshAllUI();
            RefreshLocalizedLabels();
        }

        private void RefreshLocalizedLabels()
        {
            ApplyLocalizedLabel("Container/Section_Presets/Label_Presets", LocalizationKeys.SETTINGS_PRESETS, "PRESETS");
            ApplyLocalizedLabel("Container/Section_Graphics/Label_Graphics", LocalizationKeys.SETTINGS_GRAPHICS, "GRAPHICS");
            ApplyLocalizedLabel("Container/Section_Audio/Label_Audio", LocalizationKeys.SETTINGS_AUDIO, "AUDIO");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_QualityLevel/Label_Row_QualityLevel", LocalizationKeys.SETTINGS_QUALITY_LEVEL, "QUALITY LEVEL");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_FOV/Label_Row_FOV", LocalizationKeys.SETTINGS_FOV, "FIELD OF VIEW");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_ShadowDistance/Label_Row_ShadowDistance", LocalizationKeys.SETTINGS_SHADOW_DISTANCE, "SHADOW DISTANCE");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_ShadowQuality/Label_Row_ShadowQuality", LocalizationKeys.SETTINGS_SHADOW_QUALITY, "SHADOW QUALITY");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_AntiAliasing/Label_Row_AntiAliasing", LocalizationKeys.SETTINGS_ANTI_ALIASING, "ANTI-ALIASING");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_TextureQuality/Label_Row_TextureQuality", LocalizationKeys.SETTINGS_TEXTURE_QUALITY, "TEXTURE QUALITY");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_Toggles/Toggle_Vsync/Label", LocalizationKeys.SETTINGS_VSYNC, "V-SYNC");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_Toggles/Toggle_Fullscreen/Label", LocalizationKeys.SETTINGS_FULLSCREEN, "FULLSCREEN");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_Toggles/Toggle_AO/Label", LocalizationKeys.SETTINGS_AO, "AMBIENT OCCLUSION");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_Toggles/Toggle_Bloom/Label", LocalizationKeys.SETTINGS_BLOOM, "BLOOM");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_Toggles/Toggle_MotionBlur/Label", LocalizationKeys.SETTINGS_MOTION_BLUR, "MOTION BLUR");
            ApplyLocalizedLabel("Container/Section_Audio/Row_MasterVolume/Label_Row_MasterVolume", LocalizationKeys.SETTINGS_MASTER_VOLUME, "MASTER");
            ApplyLocalizedLabel("Container/Section_Audio/Row_MusicVolume/Label_Row_MusicVolume", LocalizationKeys.SETTINGS_MUSIC_VOLUME, "MUSIC");
            ApplyLocalizedLabel("Container/Section_Audio/Row_SfxVolume/Label_Row_SfxVolume", LocalizationKeys.SETTINGS_SFX_VOLUME, "SFX");
            ApplyLocalizedLabel("Container/Section_Audio/Row_AmbientVolume/Label_Row_AmbientVolume", LocalizationKeys.SETTINGS_AMBIENT_VOLUME, "AMBIENT");
            ApplyLocalizedLabel("Container/Row_Actions/Btn_ResetDefaults/Text", LocalizationKeys.SETTINGS_RESET_DEFAULTS, "RESET");
            ApplyLocalizedLabel("Container/Row_Actions/Btn_Apply/Text", LocalizationKeys.SETTINGS_APPLY, "APPLY");
            ApplyLocalizedLabel("Container/Row_Actions/Btn_Cancel/Text", LocalizationKeys.SETTINGS_CANCEL, "CANCEL");
        }

        private void ApplyLocalizedLabel(string relativePath, string key, string fallback)
        {
            Transform target = transform.Find(relativePath);
            if (target == null)
                return;

            if (!target.TryGetComponent(out TMP_Text label))
                return;

            LocalizedTMPAutoSizer.Configure(label, label.fontSize * 0.72f, label.fontSize, TextOverflowModes.Ellipsis, TextWrappingModes.NoWrap);
            SetTextIfChanged(label, ResolveLocalized(key, fallback));
        }

        private static void SetValueTextIfChanged(TMP_Text label, string text, ref string previousText)
        {
            if (label == null || previousText == text)
                return;

            label.SetText(text);
            previousText = text;
        }

        private static void SetTextIfChanged(TMP_Text label, string text)
        {
            if (label != null && label.text != text)
                label.SetText(text);
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            if (manager == null)
                return fallback;

            return manager.GetOrFallback(manager.CurrentLanguage, key, fallback);
        }

        private static string ResolveLocalizedShadowQualityName(int shadowQualityIndex)
        {
            return shadowQualityIndex switch
            {
                0 => ResolveLocalized(LocalizationKeys.SETTINGS_VALUE_OFF, "OFF"),
                1 => ResolveLocalized(LocalizationKeys.SETTINGS_PRESET_LOW, "LOW"),
                2 => ResolveLocalized(LocalizationKeys.SETTINGS_PRESET_MEDIUM, "MEDIUM"),
                3 => ResolveLocalized(LocalizationKeys.SETTINGS_PRESET_HIGH, "HIGH"),
                _ => ResolveLocalized(LocalizationKeys.SETTINGS_VALUE_OFF, "OFF")
            };
        }

        private static string ResolveLocalizedTextureQualityName(int textureQualityIndex)
        {
            return textureQualityIndex switch
            {
                0 => ResolveLocalized(LocalizationKeys.SETTINGS_PRESET_LOW, "LOW"),
                1 => ResolveLocalized(LocalizationKeys.SETTINGS_PRESET_MEDIUM, "MEDIUM"),
                2 => ResolveLocalized(LocalizationKeys.SETTINGS_PRESET_HIGH, "HIGH"),
                3 => ResolveLocalized(LocalizationKeys.SETTINGS_PRESET_ULTRA, "ULTRA"),
                _ => ResolveLocalized(LocalizationKeys.SETTINGS_PRESET_LOW, "LOW")
            };
        }

        private static string ResolveLocalizedAntiAliasingName(int antiAliasingIndex)
        {
            if (antiAliasingIndex <= 0)
                return ResolveLocalized(LocalizationKeys.SETTINGS_VALUE_OFF, "OFF");

            return AntiAliasingNames[Mathf.Clamp(antiAliasingIndex, 0, AntiAliasingNames.Length - 1)];
        }

        private static string ResolveLocalizedQualityName(string qualityName)
        {
            if (string.IsNullOrWhiteSpace(qualityName))
                return "--";

            if (qualityName.IndexOf("Low", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return ResolveLocalized(LocalizationKeys.SETTINGS_PRESET_LOW, "LOW");
            if (qualityName.IndexOf("Medium", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return ResolveLocalized(LocalizationKeys.SETTINGS_PRESET_MEDIUM, "MEDIUM");
            if (qualityName.IndexOf("High", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return ResolveLocalized(LocalizationKeys.SETTINGS_PRESET_HIGH, "HIGH");
            if (qualityName.IndexOf("Ultra", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return ResolveLocalized(LocalizationKeys.SETTINGS_PRESET_ULTRA, "ULTRA");

            return qualityName;
        }

        // ══════════════════════════════════════════════════════════
        // CALLBACKS — AUDIO
        // ══════════════════════════════════════════════════════════

        private void OnMasterVolumeChanged(float value)
        {
            // Throttle slider updates
            if (Time.unscaledTime < _nextSliderUpdateTime)
                return;

            _nextSliderUpdateTime = Time.unscaledTime + sliderThrottleInterval;

            _cachedMasterVolume = value;
            
            // ZERO-GC: Use cached strings and dirty flags
            if (txtMasterVolume != null)
            {
                int percent = Mathf.RoundToInt(value * 100f);
                string text = VolumePercentStrings[Mathf.Clamp(percent, 0, 100)];
                if (_prevMasterVolumeText != text)
                {
                    txtMasterVolume.SetText(text);
                    _prevMasterVolumeText = text;
                }
            }
        }

        private void OnMusicVolumeChanged(float value)
        {
            // Throttle slider updates
            if (Time.unscaledTime < _nextSliderUpdateTime)
                return;

            _nextSliderUpdateTime = Time.unscaledTime + sliderThrottleInterval;

            _cachedMusicVolume = value;
            
            // ZERO-GC: Use cached strings and dirty flags
            if (txtMusicVolume != null)
            {
                int percent = Mathf.RoundToInt(value * 100f);
                string text = VolumePercentStrings[Mathf.Clamp(percent, 0, 100)];
                if (_prevMusicVolumeText != text)
                {
                    txtMusicVolume.SetText(text);
                    _prevMusicVolumeText = text;
                }
            }
        }

        private void OnSfxVolumeChanged(float value)
        {
            // Throttle slider updates
            if (Time.unscaledTime < _nextSliderUpdateTime)
                return;

            _nextSliderUpdateTime = Time.unscaledTime + sliderThrottleInterval;

            _cachedSfxVolume = value;
            
            // ZERO-GC: Use cached strings and dirty flags
            if (txtSfxVolume != null)
            {
                int percent = Mathf.RoundToInt(value * 100f);
                string text = VolumePercentStrings[Mathf.Clamp(percent, 0, 100)];
                if (_prevSfxVolumeText != text)
                {
                    txtSfxVolume.SetText(text);
                    _prevSfxVolumeText = text;
                }
            }
        }

        private void OnAmbientVolumeChanged(float value)
        {
            // Throttle slider updates
            if (Time.unscaledTime < _nextSliderUpdateTime)
                return;

            _nextSliderUpdateTime = Time.unscaledTime + sliderThrottleInterval;

            _cachedAmbientVolume = value;
            
            // ZERO-GC: Use cached strings and dirty flags
            if (txtAmbientVolume != null)
            {
                int percent = Mathf.RoundToInt(value * 100f);
                string text = VolumePercentStrings[Mathf.Clamp(percent, 0, 100)];
                if (_prevAmbientVolumeText != text)
                {
                    txtAmbientVolume.SetText(text);
                    _prevAmbientVolumeText = text;
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        // CALLBACKS — ACTIONS
        // ══════════════════════════════════════════════════════════

        private void OnResetDefaults()
        {
            if (_settings == null)
                return;

            _settings.ResetToDefaults();
            LoadCurrentSettings();
            RefreshAllUI();
        }

        private void OnApply()
        {
            if (_settings == null)
                return;

            // Apply button cooldown guard
            if (Time.unscaledTime < _nextApplyTime)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[SettingsPanel] Apply button on cooldown. Please wait.");
#endif
                return;
            }

            _nextApplyTime = Time.unscaledTime + applyButtonCooldown;

            // Apply live preview immediately
            if (livePreview != null)
                livePreview.ApplyImmediately();

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

            // Verify all settings applied successfully
            bool success = _settings.ApplyAllSettings();
            if (!success)
            {
                // Show error modal with retry/revert options (localized)
                LocalizationManager loc = LocalizationManager.Instance;
                string title = loc != null ? loc.Get(LocalizationKeys.ERROR_SETTINGS_APPLY_FAILED) : "Settings Apply Failed";
                string message = loc != null ? loc.Get(LocalizationKeys.ERROR_SETTINGS_UNAVAILABLE) : "Some settings failed to apply. Check console for details.\n\nRetry or revert to defaults?";
                
                Hecton.UI.MainMenu.ModalWindow.ShowWithCustomLabels(
                    title,
                    message,
                    () => OnApply(), // Retry
                    () => OnResetDefaults(), // Revert to defaults
                    "Retry",
                    "Revert to Defaults");
            }
        }

        private void OnCancel()
        {
            // Cancel live preview
            if (livePreview != null)
                livePreview.CancelPending();

            LoadCurrentSettings();
            RefreshAllUI();
        }
    }
}
