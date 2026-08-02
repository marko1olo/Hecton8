using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Input;
using Hecton8.World;

namespace Hecton8.UI
{
    /// <summary>
    /// Unified owner for user settings (graphics, audio, video).
    /// Persists via UserOptionsPersistence (options.h8cfg backend).
    /// Bootstrap-owned persistent runtime settings service.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-30990)]
    public sealed class SettingsManager : MonoBehaviour, IGlobalRegistryHotSwapListener
    {
        // ----------------------------------------------------------
        // CONSTANTS
        // ----------------------------------------------------------

        private const string QualityLevelKey = "Hecton_QualityLevel";
        private const string MasterVolumeKey = "Hecton_MasterVolume";
        private const string MusicVolumeKey = "Hecton_MusicVolume";
        private const string SfxVolumeKey = "Hecton_SfxVolume";
        private const string AmbientVolumeKey = "Hecton_AmbientVolume";
        private const string VsyncKey = "Hecton_Vsync";
        private const string FullscreenKey = "Hecton_Fullscreen";
        private const string ResolutionWidthKey = "Hecton_ResolutionWidth";
        private const string ResolutionHeightKey = "Hecton_ResolutionHeight";
        private const string FieldOfViewKey = "Hecton_FieldOfView";
        private const string ShadowQualityKey = "Hecton_ShadowQuality";
        private const string ShadowDistanceKey = "Hecton_ShadowDistance";
        private const string AntiAliasingKey = "Hecton_AntiAliasing";
        private const string AmbientOcclusionKey = "Hecton_AmbientOcclusion";
        private const string BloomKey = "Hecton_Bloom";
        private const string MotionBlurKey = "Hecton_MotionBlur";
        private const string TextureQualityKey = "Hecton_TextureQuality";
        private const string GraphicsPresetKey = "Hecton_GraphicsPreset";
        private const string QualityScaleVersionKey = "Hecton_QualityScaleVersion";
        private const string MenuVisualStyleKey = "Hecton_MenuVisualStyle";
        private const string MenuVisualConceptKey = "Hecton_MenuVisualConcept";
        private const string TextScaleKey = "Hecton_TextScale";
        private const string UiMotionScaleKey = "Hecton_UiMotionScale";
        private const string VrComfortModeKey = "Hecton_VRComfortMode";
        private const string VrSnapTurnKey = "Hecton_VRSnapTurn";
        private const string VrHorizonLockKey = "Hecton_VRHorizonLock";
        private const string VrComfortVignetteKey = "Hecton_VRComfortVignette";
        private const string VrHeadRelativeSwimBiasKey = "Hecton_VRHeadRelativeSwimBias";

        private const float DefaultVolume = 0.8f;
        private const int DefaultQualityLevel = 4; // High user weight; Unity profile remains platform-owned.
        private const int DefaultGraphicsPreset = 2; // High
        private const int CurrentQualityScaleVersion = 2;
        private const int DefaultMenuVisualStyleIndex = (int)MenuVisualStyle.PressureVesselNoir;
        private const int DefaultMenuVisualConceptIndex = (int)MenuVisualConcept.ModuleWindowOverlay;
        private const float DefaultFOV = 75f;
        private const float DefaultVrHeadRelativeSwimBias = 0.55f;
        public const int MaxContinuousQualityLevel = 6;
        public const int MaxGraphicsPreset = 3;

        // ----------------------------------------------------------
        // REGISTRY CACHE
        // ----------------------------------------------------------

        private static bool _isShuttingDown;
        private static SettingsManager s_runtimeInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _isShuttingDown = false;
            s_runtimeInstance = null;
        }

        /// <summary>
        /// Resolve-or-create the sole GlobalRegistry.Settings / options runtime owner.
        /// Construction previously lived only in GameBootstrapper.EnsureSettingsRuntimeRegistered.
        /// Hot consumers that call EnsureRuntimeInstance (or re-enter after scene unload clears
        /// s_runtimeInstance) permanently saw null when bootstrap had not yet re-registered.
        /// </summary>
        public static SettingsManager EnsureRuntimeInstance()
        {
            if (_isShuttingDown || !Application.isPlaying)
                return null;

            if (s_runtimeInstance != null)
                return s_runtimeInstance;

            SettingsManager registered = GlobalRegistry.Settings;
            if (registered != null)
            {
                s_runtimeInstance = registered;
                return registered;
            }

            SettingsManager existing =
                UnityEngine.Object.FindFirstObjectByType<SettingsManager>(FindObjectsInactive.Include);
            if (existing != null)
            {
                s_runtimeInstance = existing;
                return existing;
            }

            // Player-build construction path: no authored/bootstrap instance reachable.
            GameObject settingsRoot = new GameObject("[SettingsManager]"); // COLD ALLOC
            SettingsManager created = settingsRoot.AddComponent<SettingsManager>();
            return created;
        }


        public static bool TryGetInstance(out SettingsManager instance)
        {
            instance = s_runtimeInstance;
            return instance != null;
        }

        // ----------------------------------------------------------
        // FIELDS
        // ----------------------------------------------------------

        [Header("=== AUDIO MIXER ===")]
        [SerializeField] private AudioMixer audioMixer;

        [Header("=== GRAPHICS ===")]
        [SerializeField, Tooltip("Main camera for FOV application")]
        [UnityEngine.Serialization.FormerlySerializedAs("mainCamera")]
        private Camera _mainCamera;

        [SerializeField, Tooltip("URP Volume for post-processing overrides with a live scene owner (Bloom/Motion Blur). AO preference is cached separately.")]
        private Volume urpVolume;

        private UserOptionsPersistence _persistence;
        private bool _serviceRegistered;
        private bool _hotSwapListenerRegistered;
        private Camera _cachedMainCamera; // Cache resolved gameplay camera
        private VolumeProfile _cachedVolumeProfile; // Cache Volume profile lookup
        private IPlayerRuntimeContext _playerRuntimeContext;
        private bool _mainCameraResolvedFromPlayer;
        private bool _graphicsBindingStandby;
        private bool _audioBindingStandby;
        private bool _pendingFieldOfViewApply;
        private int _cachedQualityLevel = -1;
        private float _cachedMasterVolume = -1f;
        private float _cachedMusicVolume = -1f;
        private float _cachedSfxVolume = -1f;
        private float _cachedAmbientVolume = -1f;
        private bool _cachedVsync;
        private bool _cachedFullscreen;
        private int _cachedResolutionWidth = -1;
        private int _cachedResolutionHeight = -1;
        private float _cachedFieldOfView = -1f;
        private int _cachedShadowQuality = -1;
        private float _cachedShadowDistance = -1f;
        private int _cachedAntiAliasing = -1;
        private bool _cachedAmbientOcclusion;
        private bool _cachedBloom;
        private bool _cachedMotionBlur;
        private int _cachedTextureQuality = -1;
        private int _cachedGraphicsPreset = DefaultGraphicsPreset;
        private int _cachedMenuVisualStyleIndex = DefaultMenuVisualStyleIndex;
        private int _cachedMenuVisualConceptIndex = DefaultMenuVisualConceptIndex;
        private float _cachedTextScale = AccessibilitySettings.DefaultTextScale;
        private float _cachedUiMotionScale = AccessibilitySettings.DefaultUiMotionScale;
        private bool _cachedVrComfortMode = true;
        private bool _cachedVrSnapTurn = true;
        private bool _cachedVrHorizonLock = true;
        private bool _cachedVrComfortVignette = true;
        private float _cachedVrHeadRelativeSwimBias = DefaultVrHeadRelativeSwimBias;
        private int _persistenceBatchDepth;
        private bool _persistenceDirty;
        private bool _persistenceNeedsFullStage;

        public event Action<MenuVisualStyle> MenuVisualStyleChanged;
        public event Action<MenuVisualConcept> MenuVisualConceptChanged;

        // ----------------------------------------------------------
        // LIFECYCLE
        // ----------------------------------------------------------

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            _isShuttingDown = false;
            s_runtimeInstance = this;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            if (Application.isPlaying)
                GameBootstrapper.PersistRuntimeService(this);

            CachePlayerRuntimeContextCold();
            TryRefreshPersistenceReference(out _);

            TryResolveMainCameraReference();
            TryResolveVolumeProfileReference();

            LoadAllSettings();
            ApplyAllSettings();
        }

        private void OnEnable()
        {
            TryRegisterToGlobalRegistry();
            TryRegisterHotSwapListener();
            CachePlayerRuntimeContextCold();
            TryRefreshPersistenceReference(out _);
        }

        private void Start()
        {
            RefreshPersistenceFromRegistry();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            UnregisterFromGlobalRegistry();
            _playerRuntimeContext = null;
            if (ReferenceEquals(s_runtimeInstance, this))
                s_runtimeInstance = null;
        }

        private void OnDestroy()
        {
            bool wasRegisteredOwner = ReferenceEquals(GlobalRegistry.Settings, this);
            TryUnregisterHotSwapListener();
            UnregisterFromGlobalRegistry();
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            if (wasRegisteredOwner)
                _isShuttingDown = true;
            if (ReferenceEquals(s_runtimeInstance, this))
                s_runtimeInstance = null;
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.UserOptionsRuntime)
            {
                if (!TryAssignPersistence(currentService as UserOptionsPersistence, out bool changed) ||
                    !changed ||
                    !isActiveAndEnabled)
                {
                    return;
                }

                RefreshSettingsAfterPersistenceOwnerChanged();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
                RebindPlayerRuntimeContext(currentService as IPlayerRuntimeContext);

            if (!ShouldRetryStandbyBindings(serviceSlot) || !isActiveAndEnabled)
                return;

            if (!IsEnvironmentRuntimeReady())
                return;

            RetryStandbyBindings();
        }

        // ----------------------------------------------------------
        // PUBLIC API - GRAPHICS
        // ----------------------------------------------------------

        /// <summary>
        /// Gets or sets the user quality preference.
        /// The saved index is mapped to HomeostasisBrain.GlobalQualityWeight instead of Unity quality presets.
        /// </summary>
        public int QualityLevel
        {
            get => _cachedQualityLevel;
            set
            {
                int clamped = Mathf.Clamp(value, 0, MaxContinuousQualityLevel);
                if (_cachedQualityLevel == clamped)
                    return;

                _cachedQualityLevel = clamped;
                TryApplyQualityLevel(clamped);
                SaveInt(QualityLevelKey, clamped);
                SaveInt(QualityScaleVersionKey, CurrentQualityScaleVersion);
            }
        }

        /// <summary>
        /// Gets the currently persisted graphics preset (0=Low, 1=Medium, 2=High, 3=Ultra).
        /// </summary>
        public int GraphicsPreset => _cachedGraphicsPreset;

        /// <summary>
        /// Gets or sets the persisted accessibility text scale for PDA and diegetic UI.
        /// </summary>
        public float TextScale
        {
            get => _cachedTextScale;
            set
            {
                float clamped = ValidateTextScale(value);
                if (Mathf.Approximately(_cachedTextScale, clamped))
                    return;

                _cachedTextScale = clamped;
                TryApplyAccessibilityTextScale(clamped);
                SaveFloat(TextScaleKey, clamped);
            }
        }

        /// <summary>
        /// Gets or sets the persisted UI motion comfort scalar (0=no UI shake, 1=authored motion).
        /// </summary>
        public float UiMotionScale
        {
            get => _cachedUiMotionScale;
            set
            {
                float clamped = ValidateUiMotionScale(value);
                if (Mathf.Approximately(_cachedUiMotionScale, clamped))
                    return;

                _cachedUiMotionScale = clamped;
                TryApplyAccessibilityUiMotionScale(clamped);
                SaveFloat(UiMotionScaleKey, clamped);
            }
        }

        /// <summary>
        /// Gets or sets the presentation-only menu visual style.
        /// </summary>
        public MenuVisualStyle MenuVisualStyle
        {
            get => MenuVisualStyleCatalog.FromIndex(_cachedMenuVisualStyleIndex);
            set
            {
                int styleIndex = MenuVisualStyleCatalog.ToIndex(value);
                if (_cachedMenuVisualStyleIndex == styleIndex)
                    return;

                _cachedMenuVisualStyleIndex = styleIndex;
                SaveInt(MenuVisualStyleKey, styleIndex);
                MenuVisualStyleChanged?.Invoke(MenuVisualStyleCatalog.FromIndex(styleIndex));
            }
        }

        public void PreviewMenuVisualStyle(MenuVisualStyle style)
        {
            int styleIndex = MenuVisualStyleCatalog.ToIndex(style);
            MenuVisualStyleChanged?.Invoke(MenuVisualStyleCatalog.FromIndex(styleIndex));
        }

        /// <summary>
        /// Gets or sets the presentation-only menu layout concept.
        /// </summary>
        public MenuVisualConcept MenuVisualConcept
        {
            get => MenuVisualConceptCatalog.FromIndex(_cachedMenuVisualConceptIndex);
            set
            {
                int conceptIndex = MenuVisualConceptCatalog.ToIndex(value);
                if (_cachedMenuVisualConceptIndex == conceptIndex)
                    return;

                _cachedMenuVisualConceptIndex = conceptIndex;
                SaveInt(MenuVisualConceptKey, conceptIndex);
                MenuVisualConceptChanged?.Invoke(MenuVisualConceptCatalog.FromIndex(conceptIndex));
            }
        }

        public void PreviewMenuVisualConcept(MenuVisualConcept concept)
        {
            int conceptIndex = MenuVisualConceptCatalog.ToIndex(concept);
            MenuVisualConceptChanged?.Invoke(MenuVisualConceptCatalog.FromIndex(conceptIndex));
        }

        /// <summary>
        /// Gets or sets VSync enabled state (0=off, 1=on).
        /// Automatically persisted to PlayerPrefs.
        /// </summary>
        public bool Vsync
        {
            get => _cachedVsync;
            set
            {
                if (_cachedVsync == value)
                    return;

                _cachedVsync = value;
                QualitySettings.vSyncCount = value ? 1 : 0;
                SaveBool(VsyncKey, value);
            }
        }

        /// <summary>
        /// Gets or sets fullscreen mode.
        /// Automatically persisted to PlayerPrefs.
        /// </summary>
        public bool Fullscreen
        {
            get => _cachedFullscreen;
            set
            {
                if (_cachedFullscreen == value)
                    return;

                _cachedFullscreen = value;
                Screen.fullScreen = value;
                SaveBool(FullscreenKey, value);
            }
        }

        /// <summary>
        /// Sets screen resolution (width x height) and applies fullscreen state.
        /// Automatically persisted to PlayerPrefs.
        /// </summary>
        /// <param name="width">Screen width in pixels</param>
        /// <param name="height">Screen height in pixels</param>
        public void SetResolution(int width, int height)
        {
            if (_cachedResolutionWidth == width && _cachedResolutionHeight == height)
                return;

            _cachedResolutionWidth = width;
            _cachedResolutionHeight = height;
            Screen.SetResolution(width, height, _cachedFullscreen);
            SaveInt(ResolutionWidthKey, width);
            SaveInt(ResolutionHeightKey, height);
        }

        /// <summary>
        /// Gets current screen resolution.
        /// </summary>
        /// <param name="width">Output: screen width in pixels</param>
        /// <param name="height">Output: screen height in pixels</param>
        public void GetResolution(out int width, out int height)
        {
            width = _cachedResolutionWidth;
            height = _cachedResolutionHeight;
        }

        /// <summary>
        /// Gets or sets camera field of view in degrees (clamped 60-110).
        /// Automatically persisted to PlayerPrefs.
        /// </summary>
        public float FieldOfView
        {
            get => _cachedFieldOfView;
            set
            {
                float clamped = Mathf.Clamp(value, 60f, 110f);
                if (Mathf.Approximately(_cachedFieldOfView, clamped))
                    return;

                _cachedFieldOfView = clamped;
                ApplyCameraFOV(clamped);
                SaveFloat(FieldOfViewKey, clamped);
            }
        }

        /// <summary>
        /// Gets or sets shadow quality (0=Off, 1=Low, 2=Medium, 3=High).
        /// Automatically persisted to PlayerPrefs.
        /// </summary>
        public int ShadowQuality
        {
            get => _cachedShadowQuality;
            set
            {
                int clamped = Mathf.Clamp(value, 0, 3); // 0=Off, 1=Low, 2=Medium, 3=High
                if (_cachedShadowQuality == clamped)
                    return;

                _cachedShadowQuality = clamped;
                SaveInt(ShadowQualityKey, clamped);
            }
        }

        /// <summary>
        /// Gets or sets shadow draw distance in meters (clamped 50-300).
        /// Automatically persisted to PlayerPrefs.
        /// </summary>
        public float ShadowDistance
        {
            get => _cachedShadowDistance;
            set
            {
                float clamped = Mathf.Clamp(value, 50f, 300f);
                if (Mathf.Approximately(_cachedShadowDistance, clamped))
                    return;

                _cachedShadowDistance = clamped;
                TryApplyShadowDistance(clamped);
                SaveFloat(ShadowDistanceKey, clamped);
            }
        }

        /// <summary>
        /// Gets or sets anti-aliasing mode (0=None, 1=FXAA, 2=SMAA, 3=TAA).
        /// Automatically persisted to PlayerPrefs.
        /// </summary>
        public int AntiAliasing
        {
            get => _cachedAntiAliasing;
            set
            {
                int clamped = Mathf.Clamp(value, 0, 3); // 0=None, 1=FXAA, 2=SMAA, 3=TAA
                if (_cachedAntiAliasing == clamped)
                    return;

                _cachedAntiAliasing = clamped;
                SaveInt(AntiAliasingKey, clamped);
            }
        }

        /// <summary>
        /// Gets or sets ambient occlusion post-processing effect enabled state.
        /// Automatically persisted to PlayerPrefs.
        /// </summary>
        public bool AmbientOcclusion
        {
            get => _cachedAmbientOcclusion;
            set
            {
                if (_cachedAmbientOcclusion == value)
                    return;

                _cachedAmbientOcclusion = value;
                ApplyPostProcessing();
                SaveBool(AmbientOcclusionKey, value);
            }
        }

        /// <summary>
        /// Gets or sets bloom post-processing effect enabled state.
        /// Automatically persisted to PlayerPrefs.
        /// </summary>
        public bool Bloom
        {
            get => _cachedBloom;
            set
            {
                if (_cachedBloom == value)
                    return;

                _cachedBloom = value;
                ApplyPostProcessing();
                SaveBool(BloomKey, value);
            }
        }

        /// <summary>
        /// Gets or sets motion blur post-processing effect enabled state.
        /// Automatically persisted to PlayerPrefs.
        /// </summary>
        public bool MotionBlur
        {
            get => _cachedMotionBlur;
            set
            {
                if (_cachedMotionBlur == value)
                    return;

                _cachedMotionBlur = value;
                ApplyPostProcessing();
                SaveBool(MotionBlurKey, value);
            }
        }

        /// <summary>
        /// Gets or sets texture quality (0=Low, 1=Medium, 2=High, 3=Ultra).
        /// Automatically persisted to PlayerPrefs.
        /// </summary>
        public int TextureQuality
        {
            get => _cachedTextureQuality;
            set
            {
                int clamped = Mathf.Clamp(value, 0, 3); // 0=Low, 1=Medium, 2=High, 3=Ultra
                if (_cachedTextureQuality == clamped)
                    return;

                _cachedTextureQuality = clamped;
                QualitySettings.globalTextureMipmapLimit = 3 - clamped; // 3=Low, 0=Ultra
                SaveInt(TextureQualityKey, clamped);
            }
        }

        // ----------------------------------------------------------
        // PUBLIC API - AUDIO
        // ----------------------------------------------------------

        /// <summary>
        /// Gets or sets whether VR comfort locomotion rules are allowed to engage when an XR runtime is active.
        /// Automatically persisted to PlayerPrefs.
        /// </summary>
        public bool VrComfortModeEnabled
        {
            get => _cachedVrComfortMode;
            set
            {
                if (_cachedVrComfortMode == value)
                    return;

                _cachedVrComfortMode = value;
                SaveBool(VrComfortModeKey, value);
            }
        }

        /// <summary>
        /// Gets or sets whether VR turning uses 30-degree snap turns instead of smooth yaw.
        /// Automatically persisted to PlayerPrefs.
        /// </summary>
        public bool VrSnapTurnEnabled
        {
            get => _cachedVrSnapTurn;
            set
            {
                if (_cachedVrSnapTurn == value)
                    return;

                _cachedVrSnapTurn = value;
                SaveBool(VrSnapTurnKey, value);
            }
        }

        /// <summary>
        /// Gets or sets whether underwater VR roll is horizon-locked unless explicit manual tilt is present.
        /// Automatically persisted to PlayerPrefs.
        /// </summary>
        public bool VrHorizonLockEnabled
        {
            get => _cachedVrHorizonLock;
            set
            {
                if (_cachedVrHorizonLock == value)
                    return;

                _cachedVrHorizonLock = value;
                SaveBool(VrHorizonLockKey, value);
            }
        }

        /// <summary>
        /// Gets or sets whether the visor applies comfort blinders during aggressive VR motion.
        /// Automatically persisted to PlayerPrefs.
        /// </summary>
        public bool VrComfortVignetteEnabled
        {
            get => _cachedVrComfortVignette;
            set
            {
                if (_cachedVrComfortVignette == value)
                    return;

                _cachedVrComfortVignette = value;
                SaveBool(VrComfortVignetteKey, value);
            }
        }

        /// <summary>
        /// Gets or sets swimming reference blend: 0=controller/body forward, 1=head/camera forward.
        /// Automatically persisted to PlayerPrefs.
        /// </summary>
        public float VrHeadRelativeSwimBias
        {
            get => _cachedVrHeadRelativeSwimBias;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(_cachedVrHeadRelativeSwimBias, clamped))
                    return;

                _cachedVrHeadRelativeSwimBias = clamped;
                SaveFloat(VrHeadRelativeSwimBiasKey, clamped);
            }
        }

        /// <summary>
        /// Gets or sets master volume (0-1, converted to dB for AudioMixer).
        /// Automatically persisted to PlayerPrefs.
        /// </summary>
        public float MasterVolume
        {
            get => _cachedMasterVolume;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(_cachedMasterVolume, clamped))
                    return;

                _cachedMasterVolume = clamped;
                ApplyMixerVolume("MasterVolume", clamped);
                SaveFloat(MasterVolumeKey, clamped);
            }
        }

        /// <summary>
        /// Gets or sets music volume (0-1, converted to dB for AudioMixer).
        /// Automatically persisted to PlayerPrefs.
        /// </summary>
        public float MusicVolume
        {
            get => _cachedMusicVolume;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(_cachedMusicVolume, clamped))
                    return;

                _cachedMusicVolume = clamped;
                ApplyMixerVolume("MusicVolume", clamped);
                SaveFloat(MusicVolumeKey, clamped);
            }
        }

        /// <summary>
        /// Gets or sets SFX volume (0-1, converted to dB for AudioMixer).
        /// Automatically persisted to PlayerPrefs.
        /// </summary>
        public float SfxVolume
        {
            get => _cachedSfxVolume;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(_cachedSfxVolume, clamped))
                    return;

                _cachedSfxVolume = clamped;
                ApplyMixerVolume("SfxVolume", clamped);
                SaveFloat(SfxVolumeKey, clamped);
            }
        }

        /// <summary>
        /// Gets or sets ambient volume (0-1, converted to dB for AudioMixer).
        /// Automatically persisted to PlayerPrefs.
        /// </summary>
        public float AmbientVolume
        {
            get => _cachedAmbientVolume;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(_cachedAmbientVolume, clamped))
                    return;

                _cachedAmbientVolume = clamped;
                ApplyMixerVolume("AmbientVolume", clamped);
                SaveFloat(AmbientVolumeKey, clamped);
            }
        }

        // ----------------------------------------------------------
        // PUBLIC API - RESET
        // ----------------------------------------------------------

        public void BeginPersistenceBatch()
        {
            _persistenceBatchDepth++;
        }

        public void EndPersistenceBatch()
        {
            if (_persistenceBatchDepth <= 0)
            {
                _persistenceBatchDepth = 0;
                FlushPendingPersistenceSave();
                return;
            }

            _persistenceBatchDepth--;
            if (_persistenceBatchDepth == 0)
                FlushPendingPersistenceSave();
        }

        /// <summary>
        /// Reset all settings to defaults.
        /// Clears all options.h8cfg keys, sets default values, applies, and saves.
        /// </summary>
        public void ResetToDefaults()
        {
            BeginPersistenceBatch();
            try
            {
            // Clear all Hecton_* options.h8cfg keys before setting defaults.
            if (_persistence != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.Log("[SettingsManager] Clearing all settings keys from options.h8cfg...");
#endif
                _persistence.DeleteKey(QualityLevelKey);
                _persistence.DeleteKey(MasterVolumeKey);
                _persistence.DeleteKey(MusicVolumeKey);
                _persistence.DeleteKey(SfxVolumeKey);
                _persistence.DeleteKey(AmbientVolumeKey);
                _persistence.DeleteKey(VsyncKey);
                _persistence.DeleteKey(FullscreenKey);
                _persistence.DeleteKey(ResolutionWidthKey);
                _persistence.DeleteKey(ResolutionHeightKey);
                _persistence.DeleteKey(FieldOfViewKey);
                _persistence.DeleteKey(ShadowQualityKey);
                _persistence.DeleteKey(ShadowDistanceKey);
                _persistence.DeleteKey(AntiAliasingKey);
                _persistence.DeleteKey(AmbientOcclusionKey);
                _persistence.DeleteKey(BloomKey);
                _persistence.DeleteKey(MotionBlurKey);
                _persistence.DeleteKey(TextureQualityKey);
                _persistence.DeleteKey(GraphicsPresetKey);
                _persistence.DeleteKey(QualityScaleVersionKey);
                _persistence.DeleteKey(MenuVisualStyleKey);
                _persistence.DeleteKey(MenuVisualConceptKey);
                _persistence.DeleteKey(TextScaleKey);
                _persistence.DeleteKey(UiMotionScaleKey);
                _persistence.DeleteKey(VrComfortModeKey);
                _persistence.DeleteKey(VrSnapTurnKey);
                _persistence.DeleteKey(VrHorizonLockKey);
                _persistence.DeleteKey(VrComfortVignetteKey);
                _persistence.DeleteKey(VrHeadRelativeSwimBiasKey);
                MarkPersistenceDirty();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.Log("[SettingsManager] All settings keys cleared. Applying defaults...");
#endif
            }

            // Set default values
            QualityLevel = DefaultQualityLevel;
            MasterVolume = DefaultVolume;
            MusicVolume = DefaultVolume;
            SfxVolume = DefaultVolume;
            AmbientVolume = DefaultVolume;
            Vsync = true;
            Fullscreen = true;
            FieldOfView = DefaultFOV;
            ShadowQuality = 2; // Medium
            ShadowDistance = 200f;
            AntiAliasing = 2; // SMAA
            AmbientOcclusion = true;
            Bloom = true;
            MotionBlur = false;
            TextureQuality = 2; // High
            VrComfortModeEnabled = true;
            VrSnapTurnEnabled = true;
            VrHorizonLockEnabled = true;
            VrComfortVignetteEnabled = true;
            VrHeadRelativeSwimBias = DefaultVrHeadRelativeSwimBias;
            _cachedGraphicsPreset = DefaultGraphicsPreset;
            SaveInt(GraphicsPresetKey, _cachedGraphicsPreset);
            _cachedMenuVisualStyleIndex = DefaultMenuVisualStyleIndex;
            SaveInt(MenuVisualStyleKey, _cachedMenuVisualStyleIndex);
            MenuVisualStyleChanged?.Invoke(MenuVisualStyleCatalog.FromIndex(_cachedMenuVisualStyleIndex));
            _cachedMenuVisualConceptIndex = DefaultMenuVisualConceptIndex;
            SaveInt(MenuVisualConceptKey, _cachedMenuVisualConceptIndex);
            MenuVisualConceptChanged?.Invoke(MenuVisualConceptCatalog.FromIndex(_cachedMenuVisualConceptIndex));
            TextScale = AccessibilitySettings.DefaultTextScale;
            UiMotionScale = AccessibilitySettings.DefaultUiMotionScale;
            ApplyWorldQualityPreset(_cachedGraphicsPreset);

            Resolution defaultRes = Screen.currentResolution;
            SetResolution(defaultRes.width, defaultRes.height);

            // Save defaults to persistence
            if (_persistence != null)
            {
                MarkPersistenceDirty();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.Log("[SettingsManager] Default settings applied and saved.");
#endif
            }
            }
            finally
            {
                EndPersistenceBatch();
            }
        }

        /// <summary>
        /// Apply quality preset: 0=Low, 1=Medium, 2=High, 3=Ultra.
        /// Sets all graphics settings at once.
        /// </summary>
        public void ApplyQualityPreset(int preset)
        {
            int clampedPreset = ValidateGraphicsPreset(preset);
            BeginPersistenceBatch();
            try
            {
            _cachedGraphicsPreset = clampedPreset;
            SaveInt(GraphicsPresetKey, clampedPreset);

            switch (clampedPreset)
            {
                case 0: // Low
                    QualityLevel = 1;
                    ShadowQuality = 1;
                    ShadowDistance = 50f;
                    AntiAliasing = 1; // FXAA
                    AmbientOcclusion = false;
                    Bloom = false;
                    MotionBlur = false;
                    TextureQuality = 0;
                    break;

                case 1: // Medium
                    QualityLevel = 3;
                    ShadowQuality = 2;
                    ShadowDistance = 100f;
                    AntiAliasing = 2; // SMAA
                    AmbientOcclusion = false;
                    Bloom = true;
                    MotionBlur = false;
                    TextureQuality = 1;
                    break;

                case 2: // High
                    QualityLevel = 4;
                    ShadowQuality = 2;
                    ShadowDistance = 200f;
                    AntiAliasing = 2; // SMAA
                    AmbientOcclusion = true;
                    Bloom = true;
                    MotionBlur = false;
                    TextureQuality = 2;
                    break;

                case 3: // Ultra
                    QualityLevel = MaxContinuousQualityLevel;
                    ShadowQuality = 3;
                    ShadowDistance = 300f;
                    AntiAliasing = 3; // TAA
                    AmbientOcclusion = true;
                    Bloom = true;
                    MotionBlur = true;
                    TextureQuality = 3;
                    break;

                default:
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    H8Debug.LogWarning("[SettingsManager] Invalid preset. Valid range: 0-3.");
#endif
                    break;
            }

            ApplyWorldQualityPreset(clampedPreset);
            }
            finally
            {
                EndPersistenceBatch();
            }
        }

        // ----------------------------------------------------------
        // PRIVATE - LOAD/SAVE
        // ----------------------------------------------------------

        /// <summary>
        /// Load all settings from persistence with validation and repair.
        /// Clamps out-of-range values and uses defaults for missing keys.
        /// </summary>
        private void LoadAllSettings()
        {
            // Load with validation
            _cachedQualityLevel = LoadQualityLevelWithMigration();
            _cachedMasterVolume = ValidateVolume(LoadFloat(MasterVolumeKey, DefaultVolume));
            _cachedMusicVolume = ValidateVolume(LoadFloat(MusicVolumeKey, DefaultVolume));
            _cachedSfxVolume = ValidateVolume(LoadFloat(SfxVolumeKey, DefaultVolume));
            _cachedAmbientVolume = ValidateVolume(LoadFloat(AmbientVolumeKey, DefaultVolume));
            _cachedVsync = LoadBool(VsyncKey, true);
            _cachedFullscreen = LoadBool(FullscreenKey, true);

            Resolution currentRes = Screen.currentResolution;
            _cachedResolutionWidth = ValidateResolutionDimension(LoadInt(ResolutionWidthKey, currentRes.width), 640, 7680);
            _cachedResolutionHeight = ValidateResolutionDimension(LoadInt(ResolutionHeightKey, currentRes.height), 480, 4320);

            _cachedFieldOfView = ValidateFOV(LoadFloat(FieldOfViewKey, DefaultFOV));
            _cachedShadowQuality = ValidateShadowQuality(LoadInt(ShadowQualityKey, 2));
            _cachedShadowDistance = ValidateShadowDistance(LoadFloat(ShadowDistanceKey, 200f));
            _cachedAntiAliasing = ValidateAntiAliasing(LoadInt(AntiAliasingKey, 2));
            _cachedAmbientOcclusion = LoadBool(AmbientOcclusionKey, true);
            _cachedBloom = LoadBool(BloomKey, true);
            _cachedMotionBlur = LoadBool(MotionBlurKey, false);
            _cachedTextureQuality = ValidateTextureQuality(LoadInt(TextureQualityKey, 2));
            _cachedGraphicsPreset = ValidateGraphicsPreset(LoadInt(GraphicsPresetKey, DefaultGraphicsPreset));
            _cachedMenuVisualStyleIndex = ValidateMenuVisualStyleIndex(LoadInt(MenuVisualStyleKey, DefaultMenuVisualStyleIndex));
            _cachedMenuVisualConceptIndex = ValidateMenuVisualConceptIndex(LoadInt(MenuVisualConceptKey, DefaultMenuVisualConceptIndex));
            _cachedTextScale = ValidateTextScale(LoadFloat(TextScaleKey, AccessibilitySettings.DefaultTextScale));
            _cachedUiMotionScale = ValidateUiMotionScale(LoadFloat(UiMotionScaleKey, AccessibilitySettings.DefaultUiMotionScale));
            _cachedVrComfortMode = LoadBool(VrComfortModeKey, true);
            _cachedVrSnapTurn = LoadBool(VrSnapTurnKey, true);
            _cachedVrHorizonLock = LoadBool(VrHorizonLockKey, true);
            _cachedVrComfortVignette = LoadBool(VrComfortVignetteKey, true);
            _cachedVrHeadRelativeSwimBias = Mathf.Clamp01(LoadFloat(VrHeadRelativeSwimBiasKey, DefaultVrHeadRelativeSwimBias));

        }

        // ----------------------------------------------------------
        // VALIDATION HELPERS
        // ----------------------------------------------------------

        private int LoadQualityLevelWithMigration()
        {
            if (!TryRefreshPersistenceReference(out _))
                return DefaultQualityLevel;

            bool hasStoredQuality = _persistence.HasKey(QualityLevelKey);
            int storedQuality = _persistence.GetInt(QualityLevelKey, DefaultQualityLevel);
            bool hasScaleVersion = _persistence.HasKey(QualityScaleVersionKey);
            int scaleVersion = hasScaleVersion ? _persistence.GetInt(QualityScaleVersionKey, CurrentQualityScaleVersion) : 1;

            if (hasStoredQuality && scaleVersion < CurrentQualityScaleVersion)
            {
                int migratedQuality = MigrateLegacyQualityLevel(storedQuality);
                _persistence.SetInt(QualityLevelKey, migratedQuality);
                _persistence.SetInt(QualityScaleVersionKey, CurrentQualityScaleVersion);
                MarkPersistenceDirty();
                return ValidateQualityLevel(migratedQuality);
            }

            if (!hasScaleVersion)
            {
                _persistence.SetInt(QualityScaleVersionKey, CurrentQualityScaleVersion);
                MarkPersistenceDirty();
            }

            return ValidateQualityLevel(storedQuality);
        }

        private static int MigrateLegacyQualityLevel(int legacyLevel)
        {
            if (legacyLevel <= 0)
                return 1;

            if (legacyLevel == 1)
                return 3;

            if (legacyLevel == 2)
                return 4;

            return MaxContinuousQualityLevel;
        }

        private static int ValidateQualityLevel(int value)
        {
            if (value < 0 || value > MaxContinuousQualityLevel)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogWarning("[SettingsManager] Invalid quality level; clamping.");
#endif
                return Mathf.Clamp(value, 0, MaxContinuousQualityLevel);
            }
            return value;
        }

        private static float ValidateVolume(float value)
        {
            if (value < 0f || value > 1f)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogWarning("[SettingsManager] Invalid volume; clamping.");
#endif
                return Mathf.Clamp01(value);
            }
            return value;
        }

        private static int ValidateResolutionDimension(int value, int min, int max)
        {
            if (value < min || value > max)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogWarning("[SettingsManager] Invalid resolution dimension; clamping.");
#endif
                return Mathf.Clamp(value, min, max);
            }
            return value;
        }

        private static float ValidateFOV(float value)
        {
            if (value < 60f || value > 110f)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogWarning("[SettingsManager] Invalid FOV; clamping.");
#endif
                return Mathf.Clamp(value, 60f, 110f);
            }
            return value;
        }

        private static int ValidateShadowQuality(int value)
        {
            if (value < 0 || value > 3)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogWarning("[SettingsManager] Invalid shadow quality; clamping.");
#endif
                return Mathf.Clamp(value, 0, 3);
            }
            return value;
        }

        private static float ValidateShadowDistance(float value)
        {
            if (value < 50f || value > 300f)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogWarning("[SettingsManager] Invalid shadow distance; clamping.");
#endif
                return Mathf.Clamp(value, 50f, 300f);
            }
            return value;
        }

        private static int ValidateAntiAliasing(int value)
        {
            if (value < 0 || value > 3)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogWarning("[SettingsManager] Invalid anti-aliasing; clamping.");
#endif
                return Mathf.Clamp(value, 0, 3);
            }
            return value;
        }

        private static int ValidateTextureQuality(int value)
        {
            if (value < 0 || value > 3)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogWarning("[SettingsManager] Invalid texture quality; clamping.");
#endif
                return Mathf.Clamp(value, 0, 3);
            }
            return value;
        }

        private static int ValidateGraphicsPreset(int value)
        {
            if (value < 0 || value > MaxGraphicsPreset)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogWarning("[SettingsManager] Invalid graphics preset; clamping.");
#endif
                return Mathf.Clamp(value, 0, MaxGraphicsPreset);
            }

            return value;
        }

        private static int ValidateMenuVisualStyleIndex(int value)
        {
            if (!MenuVisualStyleCatalog.IsValidStyleIndex(value))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogWarning("[SettingsManager] Invalid menu visual style; clamping.");
#endif
                return MenuVisualStyleCatalog.ClampStyleIndex(value);
            }

            return value;
        }

        private static int ValidateMenuVisualConceptIndex(int value)
        {
            if (!MenuVisualConceptCatalog.IsValidConceptIndex(value))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogWarning("[SettingsManager] Invalid menu visual concept; clamping.");
#endif
                return MenuVisualConceptCatalog.ClampConceptIndex(value);
            }

            return value;
        }

        private static float ValidateTextScale(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                return AccessibilitySettings.DefaultTextScale;

            if (value < AccessibilitySettings.MinimumTextScale || value > AccessibilitySettings.MaximumTextScale)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogWarning("[SettingsManager] Invalid text scale; clamping.");
#endif
                return Mathf.Clamp(value, AccessibilitySettings.MinimumTextScale, AccessibilitySettings.MaximumTextScale);
            }

            return value;
        }

        private static float ValidateUiMotionScale(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return AccessibilitySettings.DefaultUiMotionScale;

            if (value < AccessibilitySettings.MinimumUiMotionScale || value > AccessibilitySettings.MaximumUiMotionScale)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogWarning("[SettingsManager] Invalid UI motion scale; clamping.");
#endif
                return Mathf.Clamp(value, AccessibilitySettings.MinimumUiMotionScale, AccessibilitySettings.MaximumUiMotionScale);
            }

            return value;
        }

        /// <summary>
        /// Apply all cached settings to Unity systems with comprehensive error handling.
        /// Returns true if all settings applied successfully, false if any failed.
        /// Logs detailed errors for each failure and attempts to continue with remaining settings.
        /// </summary>
        public bool ApplyAllSettings()
        {
            bool success = true;
            int failureCount = 0;

            // Quality Level
            if (!TryApplyQualityLevel(_cachedQualityLevel))
            {
                success = false;
                failureCount++;
            }

            // VSync
            if (!TryApplyVSync(_cachedVsync))
            {
                success = false;
                failureCount++;
            }

            // Fullscreen
            if (!TryApplyFullscreen(_cachedFullscreen))
            {
                success = false;
                failureCount++;
            }

            // Resolution
            if (!TryApplyResolution(_cachedResolutionWidth, _cachedResolutionHeight, _cachedFullscreen))
            {
                success = false;
                failureCount++;
            }

            bool environmentRuntimeReady = IsEnvironmentRuntimeReady();

            // Shadow Distance
            if (environmentRuntimeReady && !TryApplyShadowDistance(_cachedShadowDistance))
            {
                success = false;
                failureCount++;
            }
            else if (!environmentRuntimeReady)
            {
                _graphicsBindingStandby = true;
            }

            // Texture Quality
            if (!TryApplyTextureQuality(_cachedTextureQuality))
            {
                success = false;
                failureCount++;
            }

            if (!TryApplyAccessibilityTextScale(_cachedTextScale))
            {
                success = false;
                failureCount++;
            }

            if (!TryApplyAccessibilityUiMotionScale(_cachedUiMotionScale))
            {
                success = false;
                failureCount++;
            }

            ApplyWorldQualityPreset(_cachedGraphicsPreset);

            // Camera FOV
            if (!ApplyCameraFOV(_cachedFieldOfView))
            {
                _pendingFieldOfViewApply = true;
            }
            else
            {
                _pendingFieldOfViewApply = false;
            }

            // Post-Processing
            if (!environmentRuntimeReady || !ApplyPostProcessing())
            {
                _graphicsBindingStandby = true;
            }
            else
            {
                _graphicsBindingStandby = false;
            }

            // Audio
            if (!environmentRuntimeReady || !ApplyMixerVolume("MasterVolume", _cachedMasterVolume))
            {
                _audioBindingStandby = true;
            }
            else
            {
                _audioBindingStandby = false;
                ApplyMixerVolume("MusicVolume", _cachedMusicVolume);
                ApplyMixerVolume("SfxVolume", _cachedSfxVolume);
                ApplyMixerVolume("AmbientVolume", _cachedAmbientVolume);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (failureCount > 0)
            {
                H8Debug.LogWarning("[SettingsManager] Applied settings with failures. Check warnings above.");
            }
#endif

            return success;
        }

        // ----------------------------------------------------------
        // SAFE APPLICATION HELPERS
        // ----------------------------------------------------------

        private static bool TryApplyQualityLevel(int level)
        {
            try
            {
                float qualityWeight01 = ResolveQualityWeightFromLevel(level);
                HomeostasisBrain.SetUserGlobalQualityWeightPreference(qualityWeight01, true);
                return true;
            }
            catch (System.Exception ex)
            {
                LogApplyQualityLevelFailed(level, ex);
                return false;
            }
        }

        private static float ResolveQualityWeightFromLevel(int level)
        {
            switch (ValidateQualityLevel(level))
            {
                case 0: return 0.00f;
                case 1: return 0.16f;
                case 2: return 0.32f;
                case 3: return 0.50f;
                case 4: return 0.68f;
                case 5: return 0.84f;
                default: return 1.00f;
            }
        }

        private static bool TryApplyAccessibilityTextScale(float scale)
        {
            try
            {
                float safeScale = ValidateTextScale(scale);
                AccessibilitySettings accessibilitySettings = null;
                if (AccessibilitySettings.TryResolveActiveRuntime(ref accessibilitySettings))
                {
                    accessibilitySettings.SetTextScale(safeScale);
                    return true;
                }

                return FontStreamingManager.RequestAccessibilityTextScale(safeScale);
            }
            catch (System.Exception ex)
            {
                LogApplyAccessibilityTextScaleFailed(ex);
                return false;
            }
        }

        private static bool TryApplyAccessibilityUiMotionScale(float scale)
        {
            try
            {
                float safeScale = ValidateUiMotionScale(scale);
                AccessibilitySettings accessibilitySettings = null;
                if (AccessibilitySettings.TryResolveActiveRuntime(ref accessibilitySettings))
                {
                    accessibilitySettings.SetUiMotionScale(safeScale);
                    return true;
                }

                UIScreenShake.SetGlobalMotionScale(safeScale);
                return true;
            }
            catch (System.Exception ex)
            {
                LogApplyAccessibilityUiMotionScaleFailed(ex);
                return false;
            }
        }

        private static bool TryApplyVSync(bool enabled)
        {
            try
            {
                QualitySettings.vSyncCount = enabled ? 1 : 0;
                return true;
            }
            catch (System.Exception ex)
            {
                LogApplyVSyncFailed(ex);
                return false;
            }
        }

        private static bool TryApplyFullscreen(bool enabled)
        {
            try
            {
                Screen.fullScreen = enabled;
                return true;
            }
            catch (System.Exception ex)
            {
                LogApplyFullscreenFailed(ex);
                return false;
            }
        }

        private static bool TryApplyResolution(int width, int height, bool fullscreen)
        {
            try
            {
                Screen.SetResolution(width, height, fullscreen);
                return true;
            }
            catch (System.Exception ex)
            {
                LogApplyResolutionFailed(width, height, ex);
                return false;
            }
        }

        private static bool TryApplyShadowDistance(float distance)
        {
            try
            {
                UniversalRenderPipelineAsset urpAsset = ResolveActiveUrpAsset();
                if (urpAsset == null)
                    return false;

                if (!Mathf.Approximately(urpAsset.shadowDistance, distance))
                    urpAsset.shadowDistance = distance;

                return true;
            }
            catch (System.Exception ex)
            {
                LogApplyShadowDistanceFailed(ex);
                return false;
            }
        }

        private static UniversalRenderPipelineAsset ResolveActiveUrpAsset()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset currentUrpAsset)
                return currentUrpAsset;

            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset defaultUrpAsset)
                return defaultUrpAsset;

            return UniversalRenderPipeline.asset;
        }

        private static bool TryApplyTextureQuality(int quality)
        {
            try
            {
                QualitySettings.globalTextureMipmapLimit = 3 - quality;
                return true;
            }
            catch (System.Exception ex)
            {
                LogApplyTextureQualityFailed(ex);
                return false;
            }
        }

        private bool ApplyCameraFOV(float fov)
        {
            if (!TryResolveMainCameraReference())
                return false;

            _cachedMainCamera.fieldOfView = fov;
            return true;
        }

        private void TryRegisterToGlobalRegistry()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            GlobalRegistry.RegisterSettingsRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Settings, this);
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            SettingsManager registered = GlobalRegistry.Settings;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsSettingsRuntimeUsable(registered))
            {
                Destroy(gameObject);
                return true;
            }

            GlobalRegistry.UnregisterSettingsRuntime(registered);
            if (ReferenceEquals(s_runtimeInstance, registered))
                s_runtimeInstance = null;
            return false;
        }

        private static bool IsSettingsRuntimeUsable(SettingsManager settings)
        {
            return settings != null && settings._serviceRegistered && settings.isActiveAndEnabled;
        }

        private void UnregisterFromGlobalRegistry()
        {
            if (!_serviceRegistered && !ReferenceEquals(GlobalRegistry.Settings, this))
                return;

            if (ReferenceEquals(GlobalRegistry.Settings, this))
                GlobalRegistry.UnregisterSettingsRuntime(this);

            _serviceRegistered = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        internal void RefreshPersistenceFromRegistry()
        {
            if (!TryRefreshPersistenceReference(out bool changed) || !changed)
                return;

            RefreshSettingsAfterPersistenceOwnerChanged();
        }

        private bool TryRefreshPersistenceReference(out bool changed)
        {
            UserOptionsPersistence persistence = GlobalRegistry.UserOptions;
            return TryAssignPersistence(persistence, out changed);
        }

        private bool TryAssignPersistence(UserOptionsPersistence persistence, out bool changed)
        {
            if (!IsUserOptionsPersistenceUsable(persistence))
                persistence = null;

            changed = !ReferenceEquals(_persistence, persistence);
            if (changed)
            {
                _persistence = persistence;
                if (_persistenceDirty)
                    _persistenceNeedsFullStage = true;
            }

            return _persistence != null;
        }

        private static bool IsUserOptionsPersistenceUsable(UserOptionsPersistence persistence)
        {
            return persistence != null &&
                   persistence.IsServiceReady &&
                   persistence.isActiveAndEnabled;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogApplyQualityLevelFailed(int level, System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.LogError("[SettingsManager] Failed to apply quality level.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogApplyVSyncFailed(System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.LogError("[SettingsManager] Failed to apply VSync.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogApplyFullscreenFailed(System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.LogError("[SettingsManager] Failed to apply fullscreen.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogApplyResolutionFailed(int width, int height, System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.LogError("[SettingsManager] Failed to apply resolution.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogApplyShadowDistanceFailed(System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.LogError("[SettingsManager] Failed to apply shadow distance.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogApplyTextureQualityFailed(System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.LogError("[SettingsManager] Failed to apply texture quality.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogApplyAccessibilityTextScaleFailed(System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.LogError("[SettingsManager] Failed to apply accessibility text scale.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogApplyAccessibilityUiMotionScaleFailed(System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.LogError("[SettingsManager] Failed to apply accessibility UI motion scale.");
#endif
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CachePlayerRuntimeContextCold();
            _cachedMainCamera = null;
            _cachedVolumeProfile = null;

            if (_pendingFieldOfViewApply && ApplyCameraFOV(_cachedFieldOfView))
                _pendingFieldOfViewApply = false;

            if (IsEnvironmentRuntimeReady())
                RetryStandbyBindings();

            ApplyWorldQualityPreset(_cachedGraphicsPreset);
        }

        private static bool ShouldRetryStandbyBindings(GlobalRegistryServiceSlot serviceSlot)
        {
            return serviceSlot == GlobalRegistryServiceSlot.Environment ||
                serviceSlot == GlobalRegistryServiceSlot.Player ||
                serviceSlot == GlobalRegistryServiceSlot.LODSystemRuntime ||
                serviceSlot == GlobalRegistryServiceSlot.DynamicResolutionRuntime ||
                serviceSlot == GlobalRegistryServiceSlot.Audio ||
                serviceSlot == GlobalRegistryServiceSlot.CullingRuntime;
        }

        private void CachePlayerRuntimeContextCold()
        {
            RebindPlayerRuntimeContext(GlobalRegistry.Player);
        }

        private void RebindPlayerRuntimeContext(IPlayerRuntimeContext playerRuntimeContext)
        {
            if (ReferenceEquals(_playerRuntimeContext, playerRuntimeContext))
                return;

            _playerRuntimeContext = playerRuntimeContext;
            _cachedMainCamera = null;
            _cachedVolumeProfile = null;
            if (_mainCameraResolvedFromPlayer)
            {
                _mainCamera = null;
                _mainCameraResolvedFromPlayer = false;
            }
        }

        private void RetryStandbyBindings()
        {
            if (!IsEnvironmentRuntimeReady())
                return;

            if (_graphicsBindingStandby)
            {
                _cachedVolumeProfile = null;
                _graphicsBindingStandby = !ApplyPostProcessing();
            }

            if (_audioBindingStandby)
            {
                _audioBindingStandby = !ApplyAudioMixerSettings();
            }
        }

        private bool ApplyAudioMixerSettings()
        {
            if (!ApplyMixerVolume("MasterVolume", _cachedMasterVolume))
                return false;

            ApplyMixerVolume("MusicVolume", _cachedMusicVolume);
            ApplyMixerVolume("SfxVolume", _cachedSfxVolume);
            ApplyMixerVolume("AmbientVolume", _cachedAmbientVolume);
            return true;
        }

        private static bool IsEnvironmentRuntimeReady()
        {
            IEnvironmentRuntimeContext environment = GlobalRegistry.Environment;
            return environment != null && environment.IsInitialized;
        }

        private bool ApplyPostProcessing()
        {
            if (!TryResolveVolumeProfileReference())
                return false;

            // Unity 6000 URP exposes SSAO as a renderer feature, not a VolumeComponent.
            // Keep the user preference cached, but do not query the volume profile with the old type.

            // Bloom
            if (_cachedVolumeProfile.TryGet(out UnityEngine.Rendering.Universal.Bloom bloom))
            {
                bloom.active = _cachedBloom;
            }

            // Motion Blur
            if (_cachedVolumeProfile.TryGet(out UnityEngine.Rendering.Universal.MotionBlur motionBlur))
            {
                motionBlur.active = _cachedMotionBlur;
            }

            return true;
        }

        private void ApplyWorldQualityPreset(int graphicsPreset)
        {
            LODQualityPreset worldPreset = ResolveWorldQualityPreset(graphicsPreset);

            LODSystemManager lodSystem = GlobalRegistry.LODSystem;
            if (lodSystem != null)
            {
                lodSystem.SetQualityPreset(worldPreset);
                return;
            }

            DynamicResolutionScaler scaler = GlobalRegistry.DynamicResolution;
            if (scaler != null)
            {
                scaler.SetQualityPreset(worldPreset);
            }
        }

        private static LODQualityPreset ResolveWorldQualityPreset(int graphicsPreset)
        {
            switch (graphicsPreset)
            {
                case 0:
                    return LODQualityPreset.Low;
                case 1:
                    return LODQualityPreset.Medium;
                case 2:
                case 3:
                    return LODQualityPreset.High;
                default:
                    return LODQualityPreset.Medium;
            }
        }

        private bool TryResolveMainCameraReference()
        {
            if (_cachedMainCamera != null)
                return true;

            if (_mainCamera != null)
            {
                _mainCameraResolvedFromPlayer = false;
                _cachedMainCamera = _mainCamera;
                return true;
            }

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                if (playerTransform.TryGetComponent(out Camera playerOwnedCamera))
                {
                    _mainCamera = playerOwnedCamera;
                    _cachedMainCamera = playerOwnedCamera;
                    _mainCameraResolvedFromPlayer = true;
                    return true;
                }

                IPlayerRuntimeContext playerContext = _playerRuntimeContext;
                Camera playerChildCamera = playerContext != null ? playerContext.PlayerCamera : null;
                if (playerChildCamera != null)
                {
                    _mainCamera = playerChildCamera;
                    _cachedMainCamera = playerChildCamera;
                    _mainCameraResolvedFromPlayer = true;
                    return true;
                }
            }

            if (TryGetComponent(out Camera localCamera))
            {
                _mainCamera = localCamera;
                _cachedMainCamera = localCamera;
                _mainCameraResolvedFromPlayer = false;
                return true;
            }

            Camera childCamera = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Camera>(transform);
            if (childCamera != null)
            {
                _mainCamera = childCamera;
                _cachedMainCamera = childCamera;
                _mainCameraResolvedFromPlayer = false;
                return true;
            }

            for (Transform current = transform.parent; current != null; current = current.parent)
            {
                if (!current.TryGetComponent(out Camera parentCamera))
                    continue;

                _mainCamera = parentCamera;
                _cachedMainCamera = parentCamera;
                _mainCameraResolvedFromPlayer = false;
                return true;
            }

            return false;
        }

        private bool TryResolveVolumeProfileReference()
        {
            if (_cachedVolumeProfile != null)
                return true;

            if (TryCacheVolumeProfile(urpVolume))
                return true;

            if (_mainCamera != null &&
                TryCacheVolumeProfile(Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Volume>(_mainCamera.transform)))
            {
                return true;
            }

            if (_cachedMainCamera != null &&
                _cachedMainCamera != _mainCamera &&
                TryCacheVolumeProfile(Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Volume>(_cachedMainCamera.transform)))
            {
                return true;
            }

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null &&
                playerContext.PlayerCamera != null &&
                TryCacheVolumeProfile(Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Volume>(playerContext.PlayerCamera.transform)))
            {
                return true;
            }

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null &&
                TryCacheVolumeProfile(Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Volume>(playerTransform)))
            {
                return true;
            }

            if (TryGetComponent(out Volume localVolume) && TryCacheVolumeProfile(localVolume))
                return true;

            if (TryCacheVolumeProfile(Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Volume>(transform)))
                return true;

            for (Transform current = transform.parent; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out Volume parentVolume))
                    return TryCacheVolumeProfile(parentVolume);
            }

            return false;
        }

        private bool TryCacheVolumeProfile(Volume candidate)
        {
            if (candidate == null || candidate.profile == null)
                return false;

            urpVolume = candidate;
            _cachedVolumeProfile = candidate.profile;
            return true;
        }

        /// <summary>
        /// Applies volume to AudioMixer parameter with validation.
        /// SAFETY: Validates parameter exists before setting value.
        /// </summary>
        /// <param name="parameterName">AudioMixer exposed parameter name</param>
        /// <param name="normalizedVolume">Volume value 0-1</param>
        /// <returns>True if parameter was set successfully, false otherwise</returns>
        private bool ApplyMixerVolume(string parameterName, float normalizedVolume)
        {
            if (audioMixer == null)
                return false;

            float db = normalizedVolume > 0.0001f
                ? Mathf.Log10(normalizedVolume) * 20f
                : -80f;

            // SAFETY: Validate parameter exists before setting
            try
            {
                // Try to set the parameter - SetFloat returns false if parameter doesn't exist
                bool success = audioMixer.SetFloat(parameterName, db);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!success)
                {
                    H8Debug.LogWarning("[SettingsManager] AudioMixer parameter not found or not exposed.");
                }
#endif

                return success;
            }
            catch (System.Exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogError("[SettingsManager] Failed to set AudioMixer parameter.");
#endif
                return false;
            }
        }

        private int LoadInt(string key, int defaultValue)
        {
            if (!TryRefreshPersistenceReference(out _))
                return defaultValue;

            return _persistence.GetInt(key, defaultValue);
        }

        private float LoadFloat(string key, float defaultValue)
        {
            if (!TryRefreshPersistenceReference(out _))
                return defaultValue;

            return _persistence.GetFloat(key, defaultValue);
        }

        private bool LoadBool(string key, bool defaultValue)
        {
            if (!TryRefreshPersistenceReference(out _))
                return defaultValue;

            return _persistence.GetBool(key, defaultValue);
        }

        private void SaveInt(string key, int value)
        {
            if (!TryRefreshPersistenceReference(out _))
            {
                MarkPersistenceDirtyForMissingOwner();
                return;
            }

            _persistence.SetInt(key, value);
            MarkPersistenceDirty();
        }

        private void SaveFloat(string key, float value)
        {
            if (!TryRefreshPersistenceReference(out _))
            {
                MarkPersistenceDirtyForMissingOwner();
                return;
            }

            _persistence.SetFloat(key, value);
            MarkPersistenceDirty();
        }

        private void SaveBool(string key, bool value)
        {
            if (!TryRefreshPersistenceReference(out _))
            {
                MarkPersistenceDirtyForMissingOwner();
                return;
            }

            _persistence.SetBool(key, value);
            MarkPersistenceDirty();
        }

        private void MarkPersistenceDirtyForMissingOwner()
        {
            _persistence = null;
            _persistenceDirty = true;
            _persistenceNeedsFullStage = true;
        }

        private void MarkPersistenceDirty()
        {
            if (_persistenceBatchDepth > 0)
            {
                _persistenceDirty = true;
                return;
            }

            if (!TrySavePersistenceNow())
                _persistenceDirty = true;
        }

        private void FlushPendingPersistenceSave()
        {
            if (!_persistenceDirty)
                return;

            _persistenceDirty = false;
            if (!TryRefreshPersistenceReference(out _) || !TrySavePersistenceNow())
                _persistenceDirty = true;
        }

        private bool TrySavePersistenceNow()
        {
            if (IsUserOptionsPersistenceUsable(_persistence))
            {
                if (_persistenceNeedsFullStage)
                    StageCachedSettingsForPersistence();

                if (_persistence.TrySave())
                {
                    _persistenceNeedsFullStage = false;
                    return true;
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogWarning("[SettingsManager] Failed to persist options.h8cfg.");
#endif
                return false;
            }

            _persistence = null;
            return false;
        }

        private void RefreshSettingsAfterPersistenceOwnerChanged()
        {
            if (_persistenceDirty)
            {
                FlushPendingPersistenceSave();
                if (_persistenceDirty)
                    return;

                ApplyAllSettings();
                return;
            }

            LoadAllSettings();
            ApplyAllSettings();
        }

        private void StageCachedSettingsForPersistence()
        {
            if (!IsUserOptionsPersistenceUsable(_persistence))
                return;

            _persistence.SetInt(QualityLevelKey, _cachedQualityLevel);
            _persistence.SetInt(QualityScaleVersionKey, CurrentQualityScaleVersion);
            _persistence.SetFloat(MasterVolumeKey, _cachedMasterVolume);
            _persistence.SetFloat(MusicVolumeKey, _cachedMusicVolume);
            _persistence.SetFloat(SfxVolumeKey, _cachedSfxVolume);
            _persistence.SetFloat(AmbientVolumeKey, _cachedAmbientVolume);
            _persistence.SetBool(VsyncKey, _cachedVsync);
            _persistence.SetBool(FullscreenKey, _cachedFullscreen);
            _persistence.SetInt(ResolutionWidthKey, _cachedResolutionWidth);
            _persistence.SetInt(ResolutionHeightKey, _cachedResolutionHeight);
            _persistence.SetFloat(FieldOfViewKey, _cachedFieldOfView);
            _persistence.SetInt(ShadowQualityKey, _cachedShadowQuality);
            _persistence.SetFloat(ShadowDistanceKey, _cachedShadowDistance);
            _persistence.SetInt(AntiAliasingKey, _cachedAntiAliasing);
            _persistence.SetBool(AmbientOcclusionKey, _cachedAmbientOcclusion);
            _persistence.SetBool(BloomKey, _cachedBloom);
            _persistence.SetBool(MotionBlurKey, _cachedMotionBlur);
            _persistence.SetInt(TextureQualityKey, _cachedTextureQuality);
            _persistence.SetInt(GraphicsPresetKey, _cachedGraphicsPreset);
            _persistence.SetInt(MenuVisualStyleKey, _cachedMenuVisualStyleIndex);
            _persistence.SetInt(MenuVisualConceptKey, _cachedMenuVisualConceptIndex);
            _persistence.SetFloat(TextScaleKey, _cachedTextScale);
            _persistence.SetFloat(UiMotionScaleKey, _cachedUiMotionScale);
            _persistence.SetBool(VrComfortModeKey, _cachedVrComfortMode);
            _persistence.SetBool(VrSnapTurnKey, _cachedVrSnapTurn);
            _persistence.SetBool(VrHorizonLockKey, _cachedVrHorizonLock);
            _persistence.SetBool(VrComfortVignetteKey, _cachedVrComfortVignette);
            _persistence.SetFloat(VrHeadRelativeSwimBiasKey, _cachedVrHeadRelativeSwimBias);
        }
    }
}
