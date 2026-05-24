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
        // ══════════════════════════════════════════════════════════
        // CONSTANTS
        // ══════════════════════════════════════════════════════════

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
        private const string VrComfortModeKey = "Hecton_VRComfortMode";
        private const string VrSnapTurnKey = "Hecton_VRSnapTurn";
        private const string VrHorizonLockKey = "Hecton_VRHorizonLock";
        private const string VrComfortVignetteKey = "Hecton_VRComfortVignette";
        private const string VrHeadRelativeSwimBiasKey = "Hecton_VRHeadRelativeSwimBias";

        private const float DefaultVolume = 0.8f;
        private const int DefaultQualityLevel = 2; // Medium (Surface)
        private const int DefaultGraphicsPreset = 2; // High
        private const float DefaultFOV = 75f;
        private const float DefaultVrHeadRelativeSwimBias = 0.55f;

        // ══════════════════════════════════════════════════════════
        // REGISTRY CACHE
        // ══════════════════════════════════════════════════════════

        private static bool _isShuttingDown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _isShuttingDown = false;
        }

        /// <summary>
        /// Returns the registered settings service. Creation is owned by <see cref="GameBootstrapper"/>.
        /// </summary>
        public static SettingsManager EnsureRuntimeInstance()
        {
            if (_isShuttingDown || !Application.isPlaying)
                return null;

            return GlobalRegistry.Settings;
        }

        public static bool TryGetInstance(out SettingsManager instance)
        {
            instance = GlobalRegistry.Settings;
            return instance != null;
        }

        // ══════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════

        [Header("=== AUDIO MIXER ===")]
        [SerializeField] private AudioMixer audioMixer;

        [Header("=== GRAPHICS ===")]
        [SerializeField, Tooltip("Main camera for FOV application")]
        private Camera mainCamera;

        [SerializeField, Tooltip("URP Volume for post-processing overrides with a live scene owner (Bloom/Motion Blur). AO preference is cached separately.")]
        private Volume urpVolume;

        private UserOptionsPersistence _persistence;
        private bool _serviceRegistered;
        private bool _hotSwapListenerRegistered;
        private Camera _cachedMainCamera; // Cache resolved gameplay camera
        private VolumeProfile _cachedVolumeProfile; // Cache Volume profile lookup
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
        private bool _cachedVrComfortMode = true;
        private bool _cachedVrSnapTurn = true;
        private bool _cachedVrHorizonLock = true;
        private bool _cachedVrComfortVignette = true;
        private float _cachedVrHeadRelativeSwimBias = DefaultVrHeadRelativeSwimBias;

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            SettingsManager registered = GlobalRegistry.Settings;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            _isShuttingDown = false;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            if (Application.isPlaying)
                GameBootstrapper.PersistRuntimeService(this);

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
        }

        private void OnDestroy()
        {
            bool wasRegisteredOwner = ReferenceEquals(GlobalRegistry.Settings, this);
            TryUnregisterHotSwapListener();
            UnregisterFromGlobalRegistry();
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            if (wasRegisteredOwner)
                _isShuttingDown = true;
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

                LoadAllSettings();
                ApplyAllSettings();
                return;
            }

            if (!ShouldRetryStandbyBindings(serviceSlot) || !isActiveAndEnabled)
                return;

            if (!IsEnvironmentRuntimeReady())
                return;

            RetryStandbyBindings();
        }

        // ══════════════════════════════════════════════════════════
        // PUBLIC API — GRAPHICS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Gets or sets the Unity quality level (0-based index into QualitySettings.names).
        /// Automatically clamped to valid range and persisted to PlayerPrefs.
        /// </summary>
        public int QualityLevel
        {
            get => _cachedQualityLevel;
            set
            {
                int clamped = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1);
                if (_cachedQualityLevel == clamped)
                    return;

                _cachedQualityLevel = clamped;
                QualitySettings.SetQualityLevel(clamped, true);
                SaveInt(QualityLevelKey, clamped);
            }
        }

        /// <summary>
        /// Gets the currently persisted graphics preset (0=Low, 1=Medium, 2=High, 3=Ultra).
        /// </summary>
        public int GraphicsPreset => _cachedGraphicsPreset;

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

        // ══════════════════════════════════════════════════════════
        // PUBLIC API — AUDIO
        // ══════════════════════════════════════════════════════════

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

        // ══════════════════════════════════════════════════════════
        // PUBLIC API — RESET
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Reset all settings to defaults.
        /// Clears all options.h8cfg keys, sets default values, applies, and saves.
        /// </summary>
        public void ResetToDefaults()
        {
            // Clear all Hecton_* options.h8cfg keys before setting defaults.
            if (_persistence != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[SettingsManager] Clearing all settings keys from options.h8cfg...");
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
                _persistence.DeleteKey(VrComfortModeKey);
                _persistence.DeleteKey(VrSnapTurnKey);
                _persistence.DeleteKey(VrHorizonLockKey);
                _persistence.DeleteKey(VrComfortVignetteKey);
                _persistence.DeleteKey(VrHeadRelativeSwimBiasKey);
                _persistence.Save();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[SettingsManager] All settings keys cleared. Applying defaults...");
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
            ApplyWorldQualityPreset(_cachedGraphicsPreset);

            Resolution defaultRes = Screen.currentResolution;
            SetResolution(defaultRes.width, defaultRes.height);

            // Save defaults to persistence
            if (_persistence != null)
            {
                _persistence.Save();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[SettingsManager] Default settings applied and saved.");
#endif
            }
        }

        /// <summary>
        /// Apply quality preset: 0=Low, 1=Medium, 2=High, 3=Ultra.
        /// Sets all graphics settings at once.
        /// </summary>
        public void ApplyQualityPreset(int preset)
        {
            int clampedPreset = ValidateGraphicsPreset(preset);
            _cachedGraphicsPreset = clampedPreset;
            SaveInt(GraphicsPresetKey, clampedPreset);

            switch (clampedPreset)
            {
                case 0: // Low
                    QualityLevel = 0;
                    ShadowQuality = 1;
                    ShadowDistance = 50f;
                    AntiAliasing = 1; // FXAA
                    AmbientOcclusion = false;
                    Bloom = false;
                    MotionBlur = false;
                    TextureQuality = 0;
                    break;

                case 1: // Medium
                    QualityLevel = 1;
                    ShadowQuality = 2;
                    ShadowDistance = 100f;
                    AntiAliasing = 2; // SMAA
                    AmbientOcclusion = false;
                    Bloom = true;
                    MotionBlur = false;
                    TextureQuality = 1;
                    break;

                case 2: // High
                    QualityLevel = 2;
                    ShadowQuality = 2;
                    ShadowDistance = 200f;
                    AntiAliasing = 2; // SMAA
                    AmbientOcclusion = true;
                    Bloom = true;
                    MotionBlur = false;
                    TextureQuality = 2;
                    break;

                case 3: // Ultra
                    QualityLevel = 2;
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
                    Debug.LogWarning($"[SettingsManager] Invalid preset: {preset}. Valid range: 0-3.");
#endif
                    break;
            }

            ApplyWorldQualityPreset(clampedPreset);
        }

        // ══════════════════════════════════════════════════════════
        // PRIVATE — LOAD/SAVE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Load all settings from persistence with validation and repair.
        /// Clamps out-of-range values and uses defaults for missing keys.
        /// </summary>
        private void LoadAllSettings()
        {
            // Load with validation
            _cachedQualityLevel = ValidateQualityLevel(LoadInt(QualityLevelKey, DefaultQualityLevel));
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
            _cachedVrComfortMode = LoadBool(VrComfortModeKey, true);
            _cachedVrSnapTurn = LoadBool(VrSnapTurnKey, true);
            _cachedVrHorizonLock = LoadBool(VrHorizonLockKey, true);
            _cachedVrComfortVignette = LoadBool(VrComfortVignetteKey, true);
            _cachedVrHeadRelativeSwimBias = Mathf.Clamp01(LoadFloat(VrHeadRelativeSwimBiasKey, DefaultVrHeadRelativeSwimBias));

        }

        // ══════════════════════════════════════════════════════════
        // VALIDATION HELPERS
        // ══════════════════════════════════════════════════════════

        private static int ValidateQualityLevel(int value)
        {
            int maxLevel = QualitySettings.names.Length - 1;
            if (value < 0 || value > maxLevel)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[SettingsManager] Invalid quality level {value}, clamping to [0, {maxLevel}]");
#endif
                return Mathf.Clamp(value, 0, maxLevel);
            }
            return value;
        }

        private static float ValidateVolume(float value)
        {
            if (value < 0f || value > 1f)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[SettingsManager] Invalid volume {value}, clamping to [0, 1]");
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
                Debug.LogWarning($"[SettingsManager] Invalid resolution dimension {value}, clamping to [{min}, {max}]");
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
                Debug.LogWarning($"[SettingsManager] Invalid FOV {value}, clamping to [60, 110]");
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
                Debug.LogWarning($"[SettingsManager] Invalid shadow quality {value}, clamping to [0, 3]");
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
                Debug.LogWarning($"[SettingsManager] Invalid shadow distance {value}, clamping to [50, 300]");
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
                Debug.LogWarning($"[SettingsManager] Invalid anti-aliasing {value}, clamping to [0, 3]");
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
                Debug.LogWarning($"[SettingsManager] Invalid texture quality {value}, clamping to [0, 3]");
#endif
                return Mathf.Clamp(value, 0, 3);
            }
            return value;
        }

        private static int ValidateGraphicsPreset(int value)
        {
            if (value < 0 || value > 3)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[SettingsManager] Invalid graphics preset {value}, clamping to [0, 3]");
#endif
                return Mathf.Clamp(value, 0, 3);
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
                Debug.LogWarning($"[SettingsManager] Applied settings with {failureCount} failure(s). Check warnings above.");
            }
#endif

            return success;
        }

        // ══════════════════════════════════════════════════════════
        // SAFE APPLICATION HELPERS
        // ══════════════════════════════════════════════════════════

        private static bool TryApplyQualityLevel(int level)
        {
            try
            {
                QualitySettings.SetQualityLevel(level, true);
                return true;
            }
            catch (System.Exception ex)
            {
                LogApplyQualityLevelFailed(level, ex);
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

            SettingsManager registered = GlobalRegistry.Settings;
            if (registered != null && registered != this)
                return;

            GlobalRegistry.RegisterSettingsRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Settings, this);
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

            LoadAllSettings();
            ApplyAllSettings();
        }

        private bool TryRefreshPersistenceReference(out bool changed)
        {
            return TryAssignPersistence(GlobalRegistry.UserOptions, out changed);
        }

        private bool TryAssignPersistence(UserOptionsPersistence persistence, out bool changed)
        {
            changed = !ReferenceEquals(_persistence, persistence);
            if (changed)
                _persistence = persistence;

            return _persistence != null;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogApplyQualityLevelFailed(int level, System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[SettingsManager] Failed to apply quality level {level}: {exception.Message}");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogApplyVSyncFailed(System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[SettingsManager] Failed to apply VSync: {exception.Message}");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogApplyFullscreenFailed(System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[SettingsManager] Failed to apply fullscreen: {exception.Message}");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogApplyResolutionFailed(int width, int height, System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[SettingsManager] Failed to apply resolution {width}x{height}: {exception.Message}");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogApplyShadowDistanceFailed(System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[SettingsManager] Failed to apply shadow distance: {exception.Message}");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogApplyTextureQualityFailed(System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[SettingsManager] Failed to apply texture quality: {exception.Message}");
#endif
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
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

            if (mainCamera != null)
            {
                _cachedMainCamera = mainCamera;
                return true;
            }

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                if (playerTransform.TryGetComponent(out Camera playerOwnedCamera))
                {
                    mainCamera = playerOwnedCamera;
                    _cachedMainCamera = playerOwnedCamera;
                    return true;
                }

                IPlayerRuntimeContext playerContext = Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext;
                Camera playerChildCamera = playerContext != null ? playerContext.PlayerCamera : null;
                if (playerChildCamera != null)
                {
                    mainCamera = playerChildCamera;
                    _cachedMainCamera = playerChildCamera;
                    return true;
                }
            }

            if (TryGetComponent(out Camera localCamera))
            {
                mainCamera = localCamera;
                _cachedMainCamera = localCamera;
                return true;
            }

            Camera childCamera = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Camera>(transform);
            if (childCamera != null)
            {
                mainCamera = childCamera;
                _cachedMainCamera = childCamera;
                return true;
            }

            for (Transform current = transform.parent; current != null; current = current.parent)
            {
                if (!current.TryGetComponent(out Camera parentCamera))
                    continue;

                mainCamera = parentCamera;
                _cachedMainCamera = parentCamera;
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

            if (mainCamera != null &&
                TryCacheVolumeProfile(Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Volume>(mainCamera.transform)))
            {
                return true;
            }

            if (_cachedMainCamera != null &&
                _cachedMainCamera != mainCamera &&
                TryCacheVolumeProfile(Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Volume>(_cachedMainCamera.transform)))
            {
                return true;
            }

            if (Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext != null &&
                Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext.PlayerCamera != null &&
                TryCacheVolumeProfile(Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Volume>(Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext.PlayerCamera.transform)))
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
                    Debug.LogWarning($"[SettingsManager] AudioMixer parameter '{parameterName}' not found or not exposed.");
                }
#endif
                
                return success;
            }
            catch (System.Exception ex)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[SettingsManager] Failed to set AudioMixer parameter '{parameterName}': {ex.Message}");
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
                return;

            _persistence.SetInt(key, value);
            _persistence.Save();
        }

        private void SaveFloat(string key, float value)
        {
            if (!TryRefreshPersistenceReference(out _))
                return;

            _persistence.SetFloat(key, value);
            _persistence.Save();
        }

        private void SaveBool(string key, bool value)
        {
            if (!TryRefreshPersistenceReference(out _))
                return;

            _persistence.SetBool(key, value);
            _persistence.Save();
        }
    }
}
