using System;
using TMPro;
using Hecton8.Core;
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

        [Header("=== MENU STYLE ===")]
        [SerializeField] private Button btnMenuStyleDecrease;
        [SerializeField] private Button btnMenuStyleIncrease;
        [SerializeField] private TMP_Text txtMenuVisualStyle;
        [SerializeField] private Button btnMenuConceptDecrease;
        [SerializeField] private Button btnMenuConceptIncrease;
        [SerializeField] private TMP_Text txtMenuVisualConcept;
        [SerializeField, Tooltip("Creates cold runtime rows in scene-authored settings panels when menu visual controls are not wired.")]
        private bool autoCreateMenuVisualStyleRow = true;

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

        // ══════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════

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

        private static readonly string[] ShadowQualityNames = { "Off", "Low", "Medium", "High" };
        private static readonly string[] AntiAliasingNames = { "None", "FXAA", "SMAA", "TAA" };
        private static readonly string[] TextureQualityNames = { "Low", "Medium", "High", "Ultra" };

        // ZERO-GC: Cached char buffers for volume/FOV/shadow distance display
        private static readonly CachedTextLabel[] VolumePercentLabels = new CachedTextLabel[101]; // COLD ALLOC: label[101] - volume percentage char buffers - owner: SettingsPanel
        private static readonly CachedTextLabel[] FOVLabels = new CachedTextLabel[51]; // COLD ALLOC: label[51] - FOV char buffers - owner: SettingsPanel
        private static readonly CachedTextLabel[] ShadowDistanceLabels = new CachedTextLabel[251]; // COLD ALLOC: label[251] - shadow distance char buffers - owner: SettingsPanel

        // ZERO-GC: Dirty flags to prevent unnecessary SetText calls
        private int _prevMasterVolumeIndex = -1;
        private int _prevMusicVolumeIndex = -1;
        private int _prevSfxVolumeIndex = -1;
        private int _prevAmbientVolumeIndex = -1;
        private int _prevFOVIndex = -1;
        private int _prevShadowDistanceIndex = -1;
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

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            EnsureListenerActionsCached();
            EnsureMenuStyleControlsCold();
            EnsureMenuConceptControlsCold();
            BindButtons();
        }

        private void OnEnable()
        {
            EnsureListenerActionsCached();
            EnsureMenuStyleControlsCold();
            EnsureMenuConceptControlsCold();
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

        // ══════════════════════════════════════════════════════════
        // INITIALIZATION
        // ══════════════════════════════════════════════════════════

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
        }

        private void EnsureListenerActionsCached()
        {
            if (_applyAction != null &&
                _cancelAction != null &&
                _masterVolumeChangedAction != null &&
                _shadowDistanceChangedAction != null &&
                _menuStyleIncreaseAction != null &&
                _menuConceptIncreaseAction != null)
            {
                return;
            }

            CacheListenerActions();
        }

        private void BindButtons()
        {
            if (btnPresetLow != null)
            {
                btnPresetLow.onClick.RemoveAllListeners();
                btnPresetLow.onClick.AddListener(_presetLowAction);
            }

            if (btnPresetMedium != null)
            {
                btnPresetMedium.onClick.RemoveAllListeners();
                btnPresetMedium.onClick.AddListener(_presetMediumAction);
            }

            if (btnPresetHigh != null)
            {
                btnPresetHigh.onClick.RemoveAllListeners();
                btnPresetHigh.onClick.AddListener(_presetHighAction);
            }

            if (btnPresetUltra != null)
            {
                btnPresetUltra.onClick.RemoveAllListeners();
                btnPresetUltra.onClick.AddListener(_presetUltraAction);
            }

            if (btnQualityDecrease != null)
            {
                btnQualityDecrease.onClick.RemoveAllListeners();
                btnQualityDecrease.onClick.AddListener(_qualityDecreaseAction);
            }

            if (btnQualityIncrease != null)
            {
                btnQualityIncrease.onClick.RemoveAllListeners();
                btnQualityIncrease.onClick.AddListener(_qualityIncreaseAction);
            }

            if (toggleVsync != null)
            {
                toggleVsync.onValueChanged.RemoveAllListeners();
                toggleVsync.onValueChanged.AddListener(_vsyncChangedAction);
            }

            if (toggleFullscreen != null)
            {
                toggleFullscreen.onValueChanged.RemoveAllListeners();
                toggleFullscreen.onValueChanged.AddListener(_fullscreenChangedAction);
            }

            if (btnShadowQualityDecrease != null)
            {
                btnShadowQualityDecrease.onClick.RemoveAllListeners();
                btnShadowQualityDecrease.onClick.AddListener(_shadowQualityDecreaseAction);
            }

            if (btnShadowQualityIncrease != null)
            {
                btnShadowQualityIncrease.onClick.RemoveAllListeners();
                btnShadowQualityIncrease.onClick.AddListener(_shadowQualityIncreaseAction);
            }

            if (btnAntiAliasingDecrease != null)
            {
                btnAntiAliasingDecrease.onClick.RemoveAllListeners();
                btnAntiAliasingDecrease.onClick.AddListener(_antiAliasingDecreaseAction);
            }

            if (btnAntiAliasingIncrease != null)
            {
                btnAntiAliasingIncrease.onClick.RemoveAllListeners();
                btnAntiAliasingIncrease.onClick.AddListener(_antiAliasingIncreaseAction);
            }

            if (btnTextureQualityDecrease != null)
            {
                btnTextureQualityDecrease.onClick.RemoveAllListeners();
                btnTextureQualityDecrease.onClick.AddListener(_textureQualityDecreaseAction);
            }

            if (btnTextureQualityIncrease != null)
            {
                btnTextureQualityIncrease.onClick.RemoveAllListeners();
                btnTextureQualityIncrease.onClick.AddListener(_textureQualityIncreaseAction);
            }

            if (btnMenuStyleDecrease != null)
            {
                btnMenuStyleDecrease.onClick.RemoveAllListeners();
                btnMenuStyleDecrease.onClick.AddListener(_menuStyleDecreaseAction);
            }

            if (btnMenuStyleIncrease != null)
            {
                btnMenuStyleIncrease.onClick.RemoveAllListeners();
                btnMenuStyleIncrease.onClick.AddListener(_menuStyleIncreaseAction);
            }

            if (btnMenuConceptDecrease != null)
            {
                btnMenuConceptDecrease.onClick.RemoveAllListeners();
                btnMenuConceptDecrease.onClick.AddListener(_menuConceptDecreaseAction);
            }

            if (btnMenuConceptIncrease != null)
            {
                btnMenuConceptIncrease.onClick.RemoveAllListeners();
                btnMenuConceptIncrease.onClick.AddListener(_menuConceptIncreaseAction);
            }

            if (toggleAmbientOcclusion != null)
            {
                toggleAmbientOcclusion.onValueChanged.RemoveAllListeners();
                toggleAmbientOcclusion.onValueChanged.AddListener(_ambientOcclusionChangedAction);
            }

            if (toggleBloom != null)
            {
                toggleBloom.onValueChanged.RemoveAllListeners();
                toggleBloom.onValueChanged.AddListener(_bloomChangedAction);
            }

            if (toggleMotionBlur != null)
            {
                toggleMotionBlur.onValueChanged.RemoveAllListeners();
                toggleMotionBlur.onValueChanged.AddListener(_motionBlurChangedAction);
            }

            if (btnResetDefaults != null)
            {
                btnResetDefaults.onClick.RemoveAllListeners();
                btnResetDefaults.onClick.AddListener(_resetDefaultsAction);
            }

            if (btnApply != null)
            {
                btnApply.onClick.RemoveAllListeners();
                btnApply.onClick.AddListener(_applyAction);
            }

            if (btnCancel != null)
            {
                btnCancel.onClick.RemoveAllListeners();
                btnCancel.onClick.AddListener(_cancelAction);
            }
        }

        private void EnsureMenuStyleControlsCold()
        {
            if (!autoCreateMenuVisualStyleRow)
                return;

            if (btnMenuStyleDecrease != null && btnMenuStyleIncrease != null && txtMenuVisualStyle != null)
                return;

            Transform graphicsSection = transform.Find("Container/Section_Graphics");
            if (graphicsSection == null)
                return;

            Transform existingRow = graphicsSection.Find("Row_MenuVisualStyle");
            if (existingRow == null)
                existingRow = CreateMenuStyleRowCold(graphicsSection);

            CacheMenuStyleRowCold(existingRow);
        }

        private Transform CreateMenuStyleRowCold(Transform parent)
        {
            GameObject rowObject = new GameObject("Row_MenuVisualStyle", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement)); // COLD ALLOC: optional main-menu settings style row.
            rowObject.transform.SetParent(parent, false);

            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
            rowRect.localScale = Vector3.one;

            LayoutElement rowLayout = rowObject.GetComponent<LayoutElement>();
            rowLayout.minHeight = 34f;
            rowLayout.preferredHeight = 36f;

            HorizontalLayoutGroup rowGroup = rowObject.GetComponent<HorizontalLayoutGroup>();
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

            Transform graphicsSection = transform.Find("Container/Section_Graphics");
            if (graphicsSection == null)
                return;

            Transform existingRow = graphicsSection.Find("Row_MenuVisualConcept");
            if (existingRow == null)
                existingRow = CreateMenuConceptRowCold(graphicsSection);

            CacheMenuConceptRowCold(existingRow);
        }

        private Transform CreateMenuConceptRowCold(Transform parent)
        {
            GameObject rowObject = new GameObject("Row_MenuVisualConcept", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement)); // COLD ALLOC: optional main-menu settings concept row.
            rowObject.transform.SetParent(parent, false);

            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
            rowRect.localScale = Vector3.one;

            LayoutElement rowLayout = rowObject.GetComponent<LayoutElement>();
            rowLayout.minHeight = 34f;
            rowLayout.preferredHeight = 36f;

            HorizontalLayoutGroup rowGroup = rowObject.GetComponent<HorizontalLayoutGroup>();
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

        private static T FindDirectChildComponentCold<T>(Transform parent, string childName) where T : Component
        {
            Transform child = parent.Find(childName);
            if (child == null || !child.TryGetComponent(out T component))
                return null;

            return component;
        }

        private static Button CreateMenuStyleButtonCold(Transform parent, string name, ReadOnlySpan<char> label)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); // COLD ALLOC: optional settings row button.
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.075f, 0.145f, 0.155f, 0.86f);

            Button button = buttonObject.GetComponent<Button>();
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
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); // COLD ALLOC: optional settings row TMP text.
            textObject.transform.SetParent(parent, false);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
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

            _slidersBound = false;
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
            _cachedGraphicsPreset = _settings.GraphicsPreset;
            _cachedMenuVisualStyleIndex = MenuVisualStyleCatalog.ToIndex(_settings.MenuVisualStyle);
            _cachedMenuVisualConceptIndex = MenuVisualConceptCatalog.ToIndex(_settings.MenuVisualConcept);
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

        // ══════════════════════════════════════════════════════════
        // CALLBACKS — GRAPHICS
        // ══════════════════════════════════════════════════════════

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
            int clampedPreset = Mathf.Clamp(preset, 0, SettingsManager.MaxContinuousQualityLevel);
            _cachedGraphicsPreset = clampedPreset;

            switch (clampedPreset)
            {
                case 0:
                    _cachedQualityLevel = 0;
                    _cachedShadowQuality = 1;
                    _cachedShadowDistance = 50f;
                    _cachedAntiAliasing = 1;
                    _cachedAmbientOcclusion = false;
                    _cachedBloom = false;
                    _cachedMotionBlur = false;
                    _cachedTextureQuality = 0;
                    break;

                case 1:
                    _cachedQualityLevel = 1;
                    _cachedShadowQuality = 2;
                    _cachedShadowDistance = 100f;
                    _cachedAntiAliasing = 2;
                    _cachedAmbientOcclusion = false;
                    _cachedBloom = true;
                    _cachedMotionBlur = false;
                    _cachedTextureQuality = 1;
                    break;

                case 2:
                    _cachedQualityLevel = 2;
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
            ApplyLocalizedLabel("Container/Section_Presets/Label_Presets", LocalizationKeys.SETTINGS_PRESETS, "PRESETS");
            ApplyLocalizedLabel("Container/Section_Graphics/Label_Graphics", LocalizationKeys.SETTINGS_GRAPHICS, "GRAPHICS");
            ApplyLocalizedLabel("Container/Section_Audio/Label_Audio", LocalizationKeys.SETTINGS_AUDIO, "AUDIO");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_QualityLevel/Label_Row_QualityLevel", LocalizationKeys.SETTINGS_QUALITY_LEVEL, "QUALITY LEVEL");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_FOV/Label_Row_FOV", LocalizationKeys.SETTINGS_FOV, "FIELD OF VIEW");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_ShadowDistance/Label_Row_ShadowDistance", LocalizationKeys.SETTINGS_SHADOW_DISTANCE, "SHADOW DISTANCE");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_ShadowQuality/Label_Row_ShadowQuality", LocalizationKeys.SETTINGS_SHADOW_QUALITY, "SHADOW QUALITY");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_AntiAliasing/Label_Row_AntiAliasing", LocalizationKeys.SETTINGS_ANTI_ALIASING, "ANTI-ALIASING");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_TextureQuality/Label_Row_TextureQuality", LocalizationKeys.SETTINGS_TEXTURE_QUALITY, "TEXTURE QUALITY");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_MenuVisualStyle/Label_Row_MenuVisualStyle", "settings.menu_visual_style", "MENU STYLE");
            ApplyLocalizedLabel("Container/Section_Graphics/Row_MenuVisualConcept/Label_Row_MenuVisualConcept", "settings.menu_visual_concept", "MENU CONCEPT");
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
            SetTextIfChanged(label, ResolveLocalizedSpan(key, fallback));
        }

        private static void SetValueTextIfChanged(TMP_Text label, ReadOnlySpan<char> text, ref uint previousHash)
        {
            if (label == null)
                return;

            uint textHash = unchecked((uint)LocHash.Compute(text));
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
            uint textHash = unchecked((uint)LocHash.Compute(text));
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

        private static void SetTextIfChanged(TMP_Text label, ReadOnlySpan<char> text)
        {
            if (label != null)
                TmpTextNoAlloc.Set(label, text);
        }

        private static float ResolvePresentationClockSeconds()
        {
            return (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
        }

        private static ReadOnlySpan<char> ResolveLocalizedSpan(string key, string fallback)
        {
            ILocalizationTextReadModel manager = s_localization;
            if (manager == null)
                return fallback.AsSpan();

            return manager.GetRawSpanOrFallback(LocHash.Compute(key), fallback.AsSpan());
        }

        private static ReadOnlySpan<char> ResolveLocalizedShadowQualityName(int shadowQualityIndex)
        {
            return shadowQualityIndex switch
            {
                0 => ResolveLocalizedSpan(LocalizationKeys.SETTINGS_VALUE_OFF, "OFF"),
                1 => ResolveLocalizedSpan(LocalizationKeys.SETTINGS_PRESET_LOW, "LOW"),
                2 => ResolveLocalizedSpan(LocalizationKeys.SETTINGS_PRESET_MEDIUM, "MEDIUM"),
                3 => ResolveLocalizedSpan(LocalizationKeys.SETTINGS_PRESET_HIGH, "HIGH"),
                _ => ResolveLocalizedSpan(LocalizationKeys.SETTINGS_VALUE_OFF, "OFF")
            };
        }

        private static ReadOnlySpan<char> ResolveLocalizedTextureQualityName(int textureQualityIndex)
        {
            return textureQualityIndex switch
            {
                0 => ResolveLocalizedSpan(LocalizationKeys.SETTINGS_PRESET_LOW, "LOW"),
                1 => ResolveLocalizedSpan(LocalizationKeys.SETTINGS_PRESET_MEDIUM, "MEDIUM"),
                2 => ResolveLocalizedSpan(LocalizationKeys.SETTINGS_PRESET_HIGH, "HIGH"),
                3 => ResolveLocalizedSpan(LocalizationKeys.SETTINGS_PRESET_ULTRA, "ULTRA"),
                _ => ResolveLocalizedSpan(LocalizationKeys.SETTINGS_PRESET_LOW, "LOW")
            };
        }

        private static ReadOnlySpan<char> ResolveLocalizedAntiAliasingName(int antiAliasingIndex)
        {
            if (antiAliasingIndex <= 0)
                return ResolveLocalizedSpan(LocalizationKeys.SETTINGS_VALUE_OFF, "OFF");

            return AntiAliasingNames[Mathf.Clamp(antiAliasingIndex, 0, AntiAliasingNames.Length - 1)].AsSpan();
        }

        private static ReadOnlySpan<char> ResolveLocalizedQualityName(int qualityIndex)
        {
            return qualityIndex switch
            {
                0 => ResolveLocalizedSpan(LocalizationKeys.SETTINGS_PRESET_LOW, "LOW"),
                1 => ResolveLocalizedSpan(LocalizationKeys.SETTINGS_PRESET_MEDIUM, "MEDIUM"),
                2 => ResolveLocalizedSpan(LocalizationKeys.SETTINGS_PRESET_HIGH, "HIGH"),
                3 => ResolveLocalizedSpan(LocalizationKeys.SETTINGS_PRESET_ULTRA, "ULTRA"),
                _ => "--".AsSpan()
            };
        }

        // ══════════════════════════════════════════════════════════
        // CALLBACKS — AUDIO
        // ══════════════════════════════════════════════════════════

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

        // ══════════════════════════════════════════════════════════
        // CALLBACKS — ACTIONS
        // ══════════════════════════════════════════════════════════

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
                    LocalizationKeys.ERROR_SETTINGS_UNAVAILABLE,
                    "Some settings failed to apply. Check console for details.\n\nRetry or revert to defaults?");
                
                Hecton.UI.MainMenu.ModalWindow.ShowWithCustomLabels(
                    "Settings Apply Failed",
                    _modalMessageBuffer,
                    messageLength,
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

            RestoreMenuVisualCancelSnapshot();
            LoadCurrentSettings();
            RefreshAllUI();
        }

        private int CopyLocalizedSpanToModalBuffer(string key, string fallback)
        {
            return CopySpanToBuffer(ResolveLocalizedSpan(key, fallback), _modalMessageBuffer, 0);
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
