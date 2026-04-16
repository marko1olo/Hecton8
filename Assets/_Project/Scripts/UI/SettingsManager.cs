using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Hecton8.Bootstrap;
using Hecton8.Input;

namespace Hecton8.UI
{
    /// <summary>
    /// Unified owner for user settings (graphics, audio, video).
    /// Persists via UserOptionsPersistence (PlayerPrefs backend).
    /// Singleton DontDestroyOnLoad.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-30990)]
    public sealed class SettingsManager : MonoBehaviour
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

        private const float DefaultVolume = 0.8f;
        private const int DefaultQualityLevel = 2; // Medium (Surface)
        private const float DefaultFOV = 75f;

        // ══════════════════════════════════════════════════════════
        // SINGLETON
        // ══════════════════════════════════════════════════════════

        private static SettingsManager _instance;
        private static bool _isShuttingDown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            _isShuttingDown = false;
        }

        public static SettingsManager Instance
        {
            get
            {
                if (_isShuttingDown || !Application.isPlaying)
                    return _instance;

                if (_instance != null)
                    return _instance;

                GameObject go = new GameObject("[SettingsManager]");
                _instance = go.AddComponent<SettingsManager>();
                DontDestroyOnLoad(go);
                return _instance;
            }
        }

        public static bool TryGetInstance(out SettingsManager instance)
        {
            instance = _instance;
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

        [SerializeField, Tooltip("URP Volume for post-processing (AO/Bloom/Motion Blur)")]
        private Volume urpVolume;

        private UserOptionsPersistence _persistence;
        private Camera _cachedMainCamera; // Cache resolved gameplay camera
        private VolumeProfile _cachedVolumeProfile; // Cache Volume profile lookup
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

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _isShuttingDown = false;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            if (Application.isPlaying)
            {
                if (transform.parent != null)
                    transform.SetParent(null, true);

                DontDestroyOnLoad(gameObject);
            }

            _persistence = UserOptionsPersistence.Instance;

            TryResolveMainCameraReference();
            TryResolveVolumeProfileReference();

            LoadAllSettings();
            ApplyAllSettings();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            if (_instance == this)
            {
                _instance = null;
            }

            _isShuttingDown = true;
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
                QualitySettings.shadowDistance = clamped;
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
        /// Clears all PlayerPrefs keys, sets default values, applies, and saves.
        /// </summary>
        public void ResetToDefaults()
        {
            // Clear all Hecton_* PlayerPrefs keys before setting defaults
            if (_persistence != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[SettingsManager] Clearing all settings keys from PlayerPrefs...");
#endif
                PlayerPrefs.DeleteKey(QualityLevelKey);
                PlayerPrefs.DeleteKey(MasterVolumeKey);
                PlayerPrefs.DeleteKey(MusicVolumeKey);
                PlayerPrefs.DeleteKey(SfxVolumeKey);
                PlayerPrefs.DeleteKey(AmbientVolumeKey);
                PlayerPrefs.DeleteKey(VsyncKey);
                PlayerPrefs.DeleteKey(FullscreenKey);
                PlayerPrefs.DeleteKey(ResolutionWidthKey);
                PlayerPrefs.DeleteKey(ResolutionHeightKey);
                PlayerPrefs.DeleteKey(FieldOfViewKey);
                PlayerPrefs.DeleteKey(ShadowQualityKey);
                PlayerPrefs.DeleteKey(ShadowDistanceKey);
                PlayerPrefs.DeleteKey(AntiAliasingKey);
                PlayerPrefs.DeleteKey(AmbientOcclusionKey);
                PlayerPrefs.DeleteKey(BloomKey);
                PlayerPrefs.DeleteKey(MotionBlurKey);
                PlayerPrefs.DeleteKey(TextureQualityKey);
                PlayerPrefs.Save();

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
            switch (preset)
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

            // Shadow Distance
            if (!TryApplyShadowDistance(_cachedShadowDistance))
            {
                success = false;
                failureCount++;
            }

            // Texture Quality
            if (!TryApplyTextureQuality(_cachedTextureQuality))
            {
                success = false;
                failureCount++;
            }

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
            if (!ApplyPostProcessing())
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[SettingsManager] Post-processing unavailable (URP Volume not found)");
#endif
                success = false;
                failureCount++;
            }

            // Audio
            if (!ApplyMixerVolume("MasterVolume", _cachedMasterVolume))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[SettingsManager] Audio settings unavailable (AudioMixer not found)");
#endif
                success = false;
                failureCount++;
            }

            ApplyMixerVolume("MusicVolume", _cachedMusicVolume);
            ApplyMixerVolume("SfxVolume", _cachedSfxVolume);
            ApplyMixerVolume("AmbientVolume", _cachedAmbientVolume);

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
                Debug.LogError($"[SettingsManager] Failed to apply quality level {level}: {ex.Message}");
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
                Debug.LogError($"[SettingsManager] Failed to apply VSync: {ex.Message}");
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
                Debug.LogError($"[SettingsManager] Failed to apply fullscreen: {ex.Message}");
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
                Debug.LogError($"[SettingsManager] Failed to apply resolution {width}x{height}: {ex.Message}");
                return false;
            }
        }

        private static bool TryApplyShadowDistance(float distance)
        {
            try
            {
                QualitySettings.shadowDistance = distance;
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SettingsManager] Failed to apply shadow distance: {ex.Message}");
                return false;
            }
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
                Debug.LogError($"[SettingsManager] Failed to apply texture quality: {ex.Message}");
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

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_pendingFieldOfViewApply)
                return;

            _cachedMainCamera = null;
            if (ApplyCameraFOV(_cachedFieldOfView))
                _pendingFieldOfViewApply = false;
        }

        private bool ApplyPostProcessing()
        {
            if (!TryResolveVolumeProfileReference())
                return false;

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

        private bool TryResolveMainCameraReference()
        {
            if (_cachedMainCamera != null)
                return true;

            if (mainCamera != null)
            {
                _cachedMainCamera = mainCamera;
                return true;
            }

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                playerTransform != null)
            {
                if (playerTransform.TryGetComponent(out Camera playerOwnedCamera))
                {
                    mainCamera = playerOwnedCamera;
                    _cachedMainCamera = playerOwnedCamera;
                    return true;
                }

                Camera playerChildCamera = playerTransform.GetComponentInChildren<Camera>(true);
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

            Camera childCamera = GetComponentInChildren<Camera>(true);
            if (childCamera != null)
            {
                mainCamera = childCamera;
                _cachedMainCamera = childCamera;
                return true;
            }

            Camera parentCamera = GetComponentInParent<Camera>();
            if (parentCamera != null)
            {
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

            if (urpVolume != null && urpVolume.profile != null)
            {
                _cachedVolumeProfile = urpVolume.profile;
                return true;
            }

            Volume[] volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include);
            Volume fallback = null;

            for (int i = 0; i < volumes.Length; i++)
            {
                Volume candidate = volumes[i];
                if (candidate == null ||
                    candidate.profile == null ||
                    !candidate.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (candidate.isGlobal)
                {
                    urpVolume = candidate;
                    _cachedVolumeProfile = candidate.profile;
                    return true;
                }

                if (fallback == null)
                    fallback = candidate;
            }

            if (fallback == null)
                return false;

            urpVolume = fallback;
            _cachedVolumeProfile = fallback.profile;
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
            if (_persistence == null)
                return defaultValue;

            return _persistence.GetInt(key, defaultValue);
        }

        private float LoadFloat(string key, float defaultValue)
        {
            if (_persistence == null)
                return defaultValue;

            return _persistence.GetFloat(key, defaultValue);
        }

        private bool LoadBool(string key, bool defaultValue)
        {
            if (_persistence == null)
                return defaultValue;

            return _persistence.GetBool(key, defaultValue);
        }

        private void SaveInt(string key, int value)
        {
            if (_persistence == null)
                return;

            _persistence.SetInt(key, value);
            _persistence.Save();
        }

        private void SaveFloat(string key, float value)
        {
            if (_persistence == null)
                return;

            _persistence.SetFloat(key, value);
            _persistence.Save();
        }

        private void SaveBool(string key, bool value)
        {
            if (_persistence == null)
                return;

            _persistence.SetBool(key, value);
            _persistence.Save();
        }
    }
}
