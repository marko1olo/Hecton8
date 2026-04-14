using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
            DontDestroyOnLoad(gameObject);

            _persistence = UserOptionsPersistence.Instance;
            LoadAllSettings();
            ApplyAllSettings();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            _isShuttingDown = true;
        }

        // ══════════════════════════════════════════════════════════
        // PUBLIC API — GRAPHICS
        // ══════════════════════════════════════════════════════════

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

        public void GetResolution(out int width, out int height)
        {
            width = _cachedResolutionWidth;
            height = _cachedResolutionHeight;
        }

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

        public void ResetToDefaults()
        {
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

        private void LoadAllSettings()
        {
            _cachedQualityLevel = LoadInt(QualityLevelKey, DefaultQualityLevel);
            _cachedMasterVolume = LoadFloat(MasterVolumeKey, DefaultVolume);
            _cachedMusicVolume = LoadFloat(MusicVolumeKey, DefaultVolume);
            _cachedSfxVolume = LoadFloat(SfxVolumeKey, DefaultVolume);
            _cachedAmbientVolume = LoadFloat(AmbientVolumeKey, DefaultVolume);
            _cachedVsync = LoadBool(VsyncKey, true);
            _cachedFullscreen = LoadBool(FullscreenKey, true);

            Resolution currentRes = Screen.currentResolution;
            _cachedResolutionWidth = LoadInt(ResolutionWidthKey, currentRes.width);
            _cachedResolutionHeight = LoadInt(ResolutionHeightKey, currentRes.height);

            _cachedFieldOfView = LoadFloat(FieldOfViewKey, DefaultFOV);
            _cachedShadowQuality = LoadInt(ShadowQualityKey, 2);
            _cachedShadowDistance = LoadFloat(ShadowDistanceKey, 200f);
            _cachedAntiAliasing = LoadInt(AntiAliasingKey, 2);
            _cachedAmbientOcclusion = LoadBool(AmbientOcclusionKey, true);
            _cachedBloom = LoadBool(BloomKey, true);
            _cachedMotionBlur = LoadBool(MotionBlurKey, false);
            _cachedTextureQuality = LoadInt(TextureQualityKey, 2);
        }

        private void ApplyAllSettings()
        {
            QualitySettings.SetQualityLevel(_cachedQualityLevel, true);
            QualitySettings.vSyncCount = _cachedVsync ? 1 : 0;
            Screen.fullScreen = _cachedFullscreen;
            Screen.SetResolution(_cachedResolutionWidth, _cachedResolutionHeight, _cachedFullscreen);

            QualitySettings.shadowDistance = _cachedShadowDistance;
            QualitySettings.globalTextureMipmapLimit = 3 - _cachedTextureQuality;

            ApplyCameraFOV(_cachedFieldOfView);
            ApplyPostProcessing();

            ApplyMixerVolume("MasterVolume", _cachedMasterVolume);
            ApplyMixerVolume("MusicVolume", _cachedMusicVolume);
            ApplyMixerVolume("SfxVolume", _cachedSfxVolume);
            ApplyMixerVolume("AmbientVolume", _cachedAmbientVolume);
        }

        private void ApplyCameraFOV(float fov)
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                    return;
            }

            mainCamera.fieldOfView = fov;
        }

        private void ApplyPostProcessing()
        {
            if (urpVolume == null || urpVolume.profile == null)
                return;

            VolumeProfile profile = urpVolume.profile;

            // Bloom
            if (profile.TryGet(out UnityEngine.Rendering.Universal.Bloom bloom))
            {
                bloom.active = _cachedBloom;
            }

            // Motion Blur
            if (profile.TryGet(out UnityEngine.Rendering.Universal.MotionBlur motionBlur))
            {
                motionBlur.active = _cachedMotionBlur;
            }
        }

        private void ApplyMixerVolume(string parameterName, float normalizedVolume)
        {
            if (audioMixer == null)
                return;

            float db = normalizedVolume > 0.0001f
                ? Mathf.Log10(normalizedVolume) * 20f
                : -80f;

            audioMixer.SetFloat(parameterName, db);
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
