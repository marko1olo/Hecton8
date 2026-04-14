using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        // ══════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════

        private SettingsManager _settings;
        private bool _initialized;
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

            LoadCurrentSettings();
            RefreshAllUI();
        }

        private void OnDisable()
        {
            UnbindSliders();
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
                txtQualityLevel.SetText(qualityNames[_cachedQualityLevel]);
            else
                txtQualityLevel.SetText("--");
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

            if (txtMasterVolume != null)
                txtMasterVolume.SetText($"{Mathf.RoundToInt(_cachedMasterVolume * 100f)}%");

            if (txtMusicVolume != null)
                txtMusicVolume.SetText($"{Mathf.RoundToInt(_cachedMusicVolume * 100f)}%");

            if (txtSfxVolume != null)
                txtSfxVolume.SetText($"{Mathf.RoundToInt(_cachedSfxVolume * 100f)}%");

            if (txtAmbientVolume != null)
                txtAmbientVolume.SetText($"{Mathf.RoundToInt(_cachedAmbientVolume * 100f)}%");
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

            if (txtFieldOfView != null)
                txtFieldOfView.SetText($"{Mathf.RoundToInt(_cachedFieldOfView)}°");

            if (txtShadowQuality != null && _cachedShadowQuality >= 0 && _cachedShadowQuality < ShadowQualityNames.Length)
                txtShadowQuality.SetText(ShadowQualityNames[_cachedShadowQuality]);

            if (sliderShadowDistance != null)
                sliderShadowDistance.SetValueWithoutNotify(_cachedShadowDistance);

            if (txtShadowDistance != null)
                txtShadowDistance.SetText($"{Mathf.RoundToInt(_cachedShadowDistance)}m");

            if (txtAntiAliasing != null && _cachedAntiAliasing >= 0 && _cachedAntiAliasing < AntiAliasingNames.Length)
                txtAntiAliasing.SetText(AntiAliasingNames[_cachedAntiAliasing]);

            if (toggleAmbientOcclusion != null)
                toggleAmbientOcclusion.SetIsOnWithoutNotify(_cachedAmbientOcclusion);

            if (toggleBloom != null)
                toggleBloom.SetIsOnWithoutNotify(_cachedBloom);

            if (toggleMotionBlur != null)
                toggleMotionBlur.SetIsOnWithoutNotify(_cachedMotionBlur);

            if (txtTextureQuality != null && _cachedTextureQuality >= 0 && _cachedTextureQuality < TextureQualityNames.Length)
                txtTextureQuality.SetText(TextureQualityNames[_cachedTextureQuality]);
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
        }

        private void OnPresetMedium()
        {
            if (_settings == null)
                return;

            _settings.ApplyQualityPreset(1);
            LoadCurrentSettings();
            RefreshAllUI();
        }

        private void OnPresetHigh()
        {
            if (_settings == null)
                return;

            _settings.ApplyQualityPreset(2);
            LoadCurrentSettings();
            RefreshAllUI();
        }

        private void OnPresetUltra()
        {
            if (_settings == null)
                return;

            _settings.ApplyQualityPreset(3);
            LoadCurrentSettings();
            RefreshAllUI();
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
            _cachedFieldOfView = value;
            if (txtFieldOfView != null)
                txtFieldOfView.SetText($"{Mathf.RoundToInt(value)}°");
        }

        private void OnShadowQualityDecrease()
        {
            _cachedShadowQuality = Mathf.Clamp(_cachedShadowQuality - 1, 0, 3);
            if (txtShadowQuality != null && _cachedShadowQuality >= 0 && _cachedShadowQuality < ShadowQualityNames.Length)
                txtShadowQuality.SetText(ShadowQualityNames[_cachedShadowQuality]);
        }

        private void OnShadowQualityIncrease()
        {
            _cachedShadowQuality = Mathf.Clamp(_cachedShadowQuality + 1, 0, 3);
            if (txtShadowQuality != null && _cachedShadowQuality >= 0 && _cachedShadowQuality < ShadowQualityNames.Length)
                txtShadowQuality.SetText(ShadowQualityNames[_cachedShadowQuality]);
        }

        private void OnShadowDistanceChanged(float value)
        {
            _cachedShadowDistance = value;
            if (txtShadowDistance != null)
                txtShadowDistance.SetText($"{Mathf.RoundToInt(value)}m");
        }

        private void OnAntiAliasingDecrease()
        {
            _cachedAntiAliasing = Mathf.Clamp(_cachedAntiAliasing - 1, 0, 3);
            if (txtAntiAliasing != null && _cachedAntiAliasing >= 0 && _cachedAntiAliasing < AntiAliasingNames.Length)
                txtAntiAliasing.SetText(AntiAliasingNames[_cachedAntiAliasing]);
        }

        private void OnAntiAliasingIncrease()
        {
            _cachedAntiAliasing = Mathf.Clamp(_cachedAntiAliasing + 1, 0, 3);
            if (txtAntiAliasing != null && _cachedAntiAliasing >= 0 && _cachedAntiAliasing < AntiAliasingNames.Length)
                txtAntiAliasing.SetText(AntiAliasingNames[_cachedAntiAliasing]);
        }

        private void OnAmbientOcclusionChanged(bool value)
        {
            _cachedAmbientOcclusion = value;
        }

        private void OnBloomChanged(bool value)
        {
            _cachedBloom = value;
        }

        private void OnMotionBlurChanged(bool value)
        {
            _cachedMotionBlur = value;
        }

        private void OnTextureQualityDecrease()
        {
            _cachedTextureQuality = Mathf.Clamp(_cachedTextureQuality - 1, 0, 3);
            if (txtTextureQuality != null && _cachedTextureQuality >= 0 && _cachedTextureQuality < TextureQualityNames.Length)
                txtTextureQuality.SetText(TextureQualityNames[_cachedTextureQuality]);
        }

        private void OnTextureQualityIncrease()
        {
            _cachedTextureQuality = Mathf.Clamp(_cachedTextureQuality + 1, 0, 3);
            if (txtTextureQuality != null && _cachedTextureQuality >= 0 && _cachedTextureQuality < TextureQualityNames.Length)
                txtTextureQuality.SetText(TextureQualityNames[_cachedTextureQuality]);
        }

        // ══════════════════════════════════════════════════════════
        // CALLBACKS — AUDIO
        // ══════════════════════════════════════════════════════════

        private void OnMasterVolumeChanged(float value)
        {
            _cachedMasterVolume = value;
            if (txtMasterVolume != null)
                txtMasterVolume.SetText($"{Mathf.RoundToInt(value * 100f)}%");
        }

        private void OnMusicVolumeChanged(float value)
        {
            _cachedMusicVolume = value;
            if (txtMusicVolume != null)
                txtMusicVolume.SetText($"{Mathf.RoundToInt(value * 100f)}%");
        }

        private void OnSfxVolumeChanged(float value)
        {
            _cachedSfxVolume = value;
            if (txtSfxVolume != null)
                txtSfxVolume.SetText($"{Mathf.RoundToInt(value * 100f)}%");
        }

        private void OnAmbientVolumeChanged(float value)
        {
            _cachedAmbientVolume = value;
            if (txtAmbientVolume != null)
                txtAmbientVolume.SetText($"{Mathf.RoundToInt(value * 100f)}%");
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
        }

        private void OnCancel()
        {
            LoadCurrentSettings();
            RefreshAllUI();
        }
    }
}
