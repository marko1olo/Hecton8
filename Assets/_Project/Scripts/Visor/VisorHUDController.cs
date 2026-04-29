using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Optimization;
using Hecton8.Physics;
using Hecton8.World;
using UnityEngine;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NASAPunk.Visor
{
    /// <summary>
    /// Drives the visor HUD projection material and optional runtime render texture.
    /// Runtime refresh runs through <see cref="GameTickManager"/> while edit-mode preview
    /// stays on an editor callback so play mode avoids MonoBehaviour Update polling.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class VisorHUDController : MonoBehaviour, ITickable, IUpdatable
    {
        private static readonly List<VisorHUDController> s_activeControllers = new List<VisorHUDController>(2);

        public enum ProjectionMode
        {
            Disabled,
            SharedRenderTexture,
            RuntimeRenderTexture
        }

        [Header("References")]
        [SerializeField] private Renderer _visorRenderer;
        [SerializeField] private Camera _hudCamera;
        [SerializeField] private Camera _baseStackCamera;
        [SerializeField] private Camera _referenceCamera;
        [SerializeField] private RenderTexture _sharedRenderTexture;

        [Header("Projection")]
        [SerializeField] private ProjectionMode _projectionMode = ProjectionMode.Disabled;

        [Header("Runtime Render Texture Settings")]
        [SerializeField] private bool _matchScreenResolution = true;
        [SerializeField, Range(0.1f, 1f)] private float _renderScale = 0.5f;
        [SerializeField] private int _rtWidth = 1280;
        [SerializeField] private int _rtHeight = 720;
        [SerializeField] private FilterMode _filterMode = FilterMode.Bilinear;
        [SerializeField] private bool _enableAdaptiveRuntimeRTScaling = true;
        [SerializeField, Range(0.25f, 1f)] private float _adaptiveRuntimeRTMinScale = 0.35f;
        [SerializeField, Range(0.5f, 1f)] private float _adaptiveVRAMWarningScale = 0.82f;
        [SerializeField, Range(0.35f, 1f)] private float _adaptiveVRAMCriticalScale = 0.68f;
        [SerializeField, Range(0.01f, 0.25f)] private float _adaptiveScaleQuantizationStep = 0.05f;

        [Header("Runtime Tuning")]
        [SerializeField, Range(0f, 5f)] private float _hudIntensity = 2.5f;
        [SerializeField] private Color _hudTint = new Color(0.82f, 0.96f, 1f, 0.14f);
        [SerializeField, Range(0f, 2f)] private float _scratchBleed = 0.8f;
        [SerializeField, Range(0f, 0.1f)] private float _distortion = 0.02f;
        [SerializeField] private bool _previewInEditMode = true;

        [Header("Water Runoff")]
        [SerializeField, Range(0f, 0.05f)] private float _waterRunoffDistortion = 0.012f;
        [SerializeField, Range(0.5f, 4f)] private float _waterRunoffSpeed = 1.35f;
        [SerializeField, Range(0.5f, 12f)] private float _waterDropletScale = 5f;
        [SerializeField, Range(0f, 2f)] private float _waterDropletDensity = 1f;
        [SerializeField, Range(0f, 1f)] private float _submergeRunoffIntensity = 0.26f;
        [SerializeField, Range(0f, 1f)] private float _surfaceRunoffIntensity = 0.7f;
        [SerializeField, Range(0f, 1f)] private float _submergeRunoffHoldDuration = 0.08f;
        [SerializeField, Range(0f, 1.5f)] private float _surfaceRunoffHoldDuration = 0.24f;
        [SerializeField, Range(0.25f, 10f)] private float _submergeRunoffRecoverySpeed = 1.4f;
        [SerializeField, Range(0.25f, 10f)] private float _surfaceRunoffRecoverySpeed = 1.8f;
        [SerializeField, Range(0.5f, 5f)] private float _surfaceBreakRunoffMinimumLifetime = 3f;

        [Header("Condensation")]
        [SerializeField, Range(0f, 0.05f)] private float _condensationDistortion = 0.008f;
        [SerializeField, Range(0.5f, 6f)] private float _condensationEdgeExponent = 2.35f;
        [SerializeField, Range(0f, 2f)] private float _condensationDriftSpeed = 0.18f;
        [SerializeField, Range(1f, 20f)] private float _temperatureShockThreshold = 6f;
        [SerializeField, Range(0.1f, 4f)] private float _pressureShockThreshold = 0.75f;
        [SerializeField, Range(0.5f, 1.5f)] private float _criticalPressureStartFactor = 0.88f;
        [SerializeField, Range(1f, 2f)] private float _criticalPressureFullFactor = 1.18f;
        [SerializeField, Range(0f, 1f)] private float _criticalPressureCondensationMax = 0.52f;
        [SerializeField, Range(0f, 1f)] private float _condensationShockHoldDuration = 0.22f;
        [SerializeField, Range(0.25f, 8f)] private float _condensationShockRecoverySpeed = 1.3f;
        [SerializeField, Range(0.25f, 8f)] private float _criticalPressureCondensationBlendSpeed = 2.2f;

        [Header("Abyssal Frost")]
        [SerializeField, Range(0f, 1f)] private float _screenFrostMaximum = 0.78f;
        [SerializeField, Range(-40f, 10f)] private float _frostStartTemperature = -6f;
        [SerializeField, Range(-60f, 0f)] private float _frostFullTemperature = -24f;
        [SerializeField, Range(0f, 1f)] private float _abyssalColdFrostBoost = 0.35f;
        [SerializeField, Range(0.25f, 8f)] private float _screenFrostBlendSpeed = 1.9f;

        [Header("Environmental Interference")]
        [SerializeField, Range(0f, 0.05f)] private float _interferenceDistortionMax = 0.016f;

        [Header("Structural Fatigue Glitch")]
        [SerializeField, Range(0f, 0.02f)] private float _structuralFatigueChromaticAberrationMax = 0.007f;
        [SerializeField, Range(0f, 1f)] private float _structuralFatigueStaticNoiseMax = 0.22f;
        [SerializeField, Range(0.25f, 12f)] private float _structuralFatigueBlendSharpness = 4.8f;

        [Header("Hypoxia HUD Failure")]
        [SerializeField, Range(0.01f, 0.5f)] private float _hypoxiaStartThreshold = 0.15f;
        [SerializeField, Range(0.25f, 12f)] private float _hypoxiaBlendSharpness = 5.6f;

        [Header("Pose Lock")]
        [SerializeField] private bool _syncToReferenceCamera = true;
        [SerializeField] private bool _syncPoseInEditMode = false;
        [SerializeField] private Vector3 _visorLocalOffset = new Vector3(0f, 0f, 0.3f);
        [SerializeField] private Vector3 _visorLocalEulerOffset = Vector3.zero;
        [SerializeField] private Vector3 _visorLocalScale = new Vector3(1f, 1f, 0.6f);
        [SerializeField] private Vector3 _hudCameraLocalOffset = Vector3.zero;
        [SerializeField] private Vector3 _hudCameraLocalEulerOffset = Vector3.zero;
        [SerializeField] private float _minimumVisorForwardOffset = 0.02f;
        [SerializeField] private bool _enforceNearClipSafeOffset = false;

        private const float AutoResolveRetryInterval = 1f;

        private RenderTexture _hudRT;
        private MaterialPropertyBlock _mpb;
        private bool _ownsRuntimeTexture;
        private int _cachedRTWidth = -1;
        private int _cachedRTHeight = -1;
        private float _cachedEffectiveRenderScale = -1f;
        private float _nextAutoResolveAt;
        private bool _materialPropertiesDirty = true;
        private UniversalAdditionalCameraData _cachedHudCameraData;
        private UniversalAdditionalCameraData _cachedBaseCameraData;
        private bool _poseApplied;
        private Vector3 _appliedVisorPosition;
        private Quaternion _appliedVisorRotation;
        private Vector3 _appliedVisorScale;
        private bool _hudPoseApplied;
        private Vector3 _appliedHudPosition;
        private Quaternion _appliedHudRotation;
        private Vector3 _cachedVisorEulerOffset;
        private Quaternion _cachedVisorOffsetRotation = Quaternion.identity;
        private Vector3 _cachedHudEulerOffset;
        private Quaternion _cachedHudOffsetRotation = Quaternion.identity;

        private bool _glitchActive;
        private float _glitchTimer;
        private float _glitchDuration;
        private float _glitchOriginalIntensity;
        private float _waterRunoffIntensity;
        private float _waterRunoffHoldTimer;
        private float _waterRunoffRecoverySpeed;
        private float _condensationShockIntensity;
        private float _condensationShockHoldTimer;
        private float _criticalPressureCondensationTarget;
        private float _criticalPressureCondensation;
        private float _screenFrostTarget;
        private float _screenFrostStrength;
        private float _interferenceDistortionIntensity;
        private float _interferenceDistortionHoldTimer;
        private float _interferenceDistortionRecoverySpeed;
        private bool _runtimeTickRegistered;
        private bool _editorPreviewSuspended;
        private HectonSurvivalSystem _survivalSystem;
        private TraumaDispatcher _traumaDispatcher;
        private HectonSurvivalSystem _subscribedSurvivalSystem;
        private ISubmarineRuntimeContext _submarineRuntimeContext;
        private SubmarineStructuralGrid _structuralGrid;
        private bool _hasTemperatureSample;
        private float _lastTemperatureSample;
        private bool _hasPressureSample;
        private float _lastPressureSample;
        private float _structuralFatigueChromaticAberration;
        private float _structuralFatigueStaticNoise;
        private float _hudHypoxiaLevel;
        private float _hazardRadiationLevel;
        private float _hazardThermalLevel;
        private float _hazardToxicLevel;
        private float _hazardGlitchLevel;
        private float _biosRecoveryModeBlend;

        private uint _glitchRngState = 1u;

        private static readonly int ID_HUDTex = Shader.PropertyToID("_HUD_RenderTexture");
        private static readonly int ID_HUDIntensity = Shader.PropertyToID("_HUD_Intensity");
        private static readonly int ID_HUDColor = Shader.PropertyToID("_HUD_Color");
        private static readonly int ID_ScratchBleed = Shader.PropertyToID("_HUD_ScratchBleed");
        private static readonly int ID_Distortion = Shader.PropertyToID("_DistortionStrength");
        private static readonly int ID_WaterRunoffStrength = Shader.PropertyToID("_WaterRunoffStrength");
        private static readonly int ID_WaterRunoffSpeed = Shader.PropertyToID("_WaterRunoffSpeed");
        private static readonly int ID_WaterRunoffDistortion = Shader.PropertyToID("_WaterRunoffDistortion");
        private static readonly int ID_WaterDropletDensity = Shader.PropertyToID("_WaterDropletDensity");
        private static readonly int ID_WaterDropletScale = Shader.PropertyToID("_WaterDropletScale");
        private static readonly int ID_CondensationStrength = Shader.PropertyToID("_CondensationStrength");
        private static readonly int ID_CondensationDistortion = Shader.PropertyToID("_CondensationDistortion");
        private static readonly int ID_CondensationEdgeExponent = Shader.PropertyToID("_CondensationEdgeExponent");
        private static readonly int ID_CondensationDriftSpeed = Shader.PropertyToID("_CondensationDriftSpeed");
        private static readonly int ID_ScreenFrostStrength = Shader.PropertyToID("_ScreenFrostStrength");
        private static readonly int ID_ChromaticAberration = Shader.PropertyToID("_ChromaticAberration");
        private static readonly int ID_StaticNoise = Shader.PropertyToID("_StaticNoise");
        private static readonly int ID_HypoxiaLevel = Shader.PropertyToID("_HypoxiaLevel");
        private static readonly int ID_HazardRadiationLevel = Shader.PropertyToID("_HazardRadiationLevel");
        private static readonly int ID_HazardThermalLevel = Shader.PropertyToID("_HazardThermalLevel");
        private static readonly int ID_HazardToxicLevel = Shader.PropertyToID("_HazardToxicLevel");
        private static readonly int ID_HazardGlitchLevel = Shader.PropertyToID("_HazardGlitchLevel");
        private static readonly int ID_BiosRecoveryMode = Shader.PropertyToID("_BiosRecoveryMode");

        public Camera HudCamera => _hudCamera;
        public RenderTexture SharedRenderTexture => _sharedRenderTexture;
        internal bool CanPresentProjection =>
            isActiveAndEnabled &&
            _hudCamera != null &&
            _hudCamera.isActiveAndEnabled &&
            _visorRenderer != null &&
            _visorRenderer.enabled &&
            !_visorRenderer.forceRenderingOff &&
            _visorRenderer.gameObject.activeInHierarchy;

        public static void CopyActiveControllersTo(List<VisorHUDController> results)
        {
            if (results == null)
                return;

            results.Clear();
            for (int i = 0; i < s_activeControllers.Count; i++)
            {
                VisorHUDController controller = s_activeControllers[i];
                if (controller != null && controller.isActiveAndEnabled)
                    results.Add(controller);
            }
        }

        private void Awake()
        {
            EnsurePropertyBlock();
        }

        private void OnEnable()
        {
            RegisterActiveController();
            EnsurePropertyBlock();
            _materialPropertiesDirty = true;
            AutoResolveReferences(force: true);
            SyncProjectionPose();
            RebuildProjection();
            TryRegisterRuntimeTick();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (!IsEditorPreviewActive())
                    SuspendEditModeProjection();

                EvaluateEditorTickRegistration();
            }
#endif
        }

        private void Start()
        {
            TryRegisterRuntimeTick();
        }

        private void OnDisable()
        {
            UnregisterActiveController();

            if (_glitchActive)
            {
                _hudIntensity = _glitchOriginalIntensity;
                _glitchActive = false;
            }

            if (_waterRunoffIntensity > 0f || _waterRunoffHoldTimer > 0f)
            {
                _waterRunoffIntensity = 0f;
                _waterRunoffHoldTimer = 0f;
                _materialPropertiesDirty = true;
                ApplyMaterialProperties();
            }

            if (_condensationShockIntensity > 0f ||
                _condensationShockHoldTimer > 0f ||
                _criticalPressureCondensation > 0f ||
                _screenFrostStrength > 0f ||
                _interferenceDistortionIntensity > 0f ||
                _interferenceDistortionHoldTimer > 0f)
            {
                _condensationShockIntensity = 0f;
                _condensationShockHoldTimer = 0f;
                _criticalPressureCondensationTarget = 0f;
                _criticalPressureCondensation = 0f;
                _screenFrostTarget = 0f;
                _screenFrostStrength = 0f;
                _interferenceDistortionIntensity = 0f;
                _interferenceDistortionHoldTimer = 0f;
                _materialPropertiesDirty = true;
                ApplyMaterialProperties();
            }

            RefreshSurvivalSubscription(null);
            _survivalSystem = null;
            _traumaDispatcher = null;
            _hasTemperatureSample = false;
            _hasPressureSample = false;
            _hazardRadiationLevel = 0f;
            _hazardThermalLevel = 0f;
            _hazardToxicLevel = 0f;
            _hazardGlitchLevel = 0f;
            _biosRecoveryModeBlend = 0f;
            UnregisterRuntimeTick();
            ReleaseRT();
            InvalidatePoseCache();
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
        }

        private void OnDestroy()
        {
            // Ensure RT is released on component destruction
            ReleaseRT();
        }

#if UNITY_EDITOR
        private void EditorTick()
        {
            if (!IsEditorPreviewActive())
            {
                SuspendEditModeProjection();
                return;
            }

            if (_editorPreviewSuspended)
                ResumeEditModeProjection();

            if (!ShouldTickInEditMode())
            {
                UnregisterEditorTick();
                return;
            }

            RefreshRuntimeState(forceResolve: false);
        }
#endif

        private void OnValidate()
        {
            EnsurePropertyBlock();
            _materialPropertiesDirty = true;
            AutoResolveReferences(force: true);
            SyncProjectionPose();

            if (!isActiveAndEnabled)
                return;

            RebuildProjection();
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EvaluateEditorTickRegistration();
#endif
        }

        public void Tick(float deltaTime)
        {
            AutoResolveReferences(force: false);
            SyncProjectionPose();
            RefreshAdaptiveRuntimeProjection();
            UpdateGlitchState(deltaTime);
            UpdateWaterRunoffState(deltaTime);
            UpdateCondensationState(deltaTime);
            UpdateFrostState(deltaTime);
            UpdateInterferenceState(deltaTime);
            UpdateStructuralFatigueState(deltaTime);
            UpdateHazardTraumaState(deltaTime);
            UpdateHypoxiaState(deltaTime);
            if (_materialPropertiesDirty)
                ApplyMaterialProperties();
        }

        /// <summary>
        /// xorshift32 based zero-GC pseudo-random in [0, 1).
        /// </summary>
        private float XorShift01()
        {
            _glitchRngState ^= _glitchRngState << 13;
            _glitchRngState ^= _glitchRngState >> 17;
            _glitchRngState ^= _glitchRngState << 5;
            return (_glitchRngState & 0x7FFFFF) / (float)0x800000;
        }

        private void TryRegisterRuntimeTick()
        {
            if (!Application.isPlaying || _runtimeTickRegistered)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _runtimeTickRegistered = true;
        }

        private void UnregisterRuntimeTick()
        {
            if (!_runtimeTickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _runtimeTickRegistered = false;
        }

        private void RegisterActiveController()
        {
            if (s_activeControllers.Contains(this))
                return;

            s_activeControllers.Add(this);
        }

        private void UnregisterActiveController()
        {
            s_activeControllers.Remove(this);
        }

        private void AutoResolveReferences(bool force)
        {
            if (!force && !NeedsAutoResolve())
                return;

            float now = GetAutoResolveNow();
            if (!force && now < _nextAutoResolveAt)
                return;

            _nextAutoResolveAt = now + AutoResolveRetryInterval;

            if (_visorRenderer == null)
                _visorRenderer = GetComponent<Renderer>();

            if (_hudCamera == null)
            {
                Transform parent = transform.parent;
                if (parent != null)
                {
                    Transform cameraTransform = parent.Find("HUD_Render_Camera");
                    if (cameraTransform != null)
                        _hudCamera = cameraTransform.GetComponent<Camera>();
                }
            }

            if (_baseStackCamera == null)
            {
                Transform parent = transform.parent;
                if (parent != null)
                {
                    Transform mainCameraTransform = parent.Find("Main Camera");
                    if (mainCameraTransform != null)
                    {
                        Transform spaceCameraTransform = mainCameraTransform.Find("SpaceCamera");
                        if (spaceCameraTransform != null)
                            _baseStackCamera = spaceCameraTransform.GetComponent<Camera>();
                    }
                }
            }

            if (_referenceCamera == null)
            {
                Transform parent = transform.parent;
                if (parent != null)
                {
                    Transform mainCameraTransform = parent.Find("Main Camera");
                    if (mainCameraTransform != null)
                        _referenceCamera = mainCameraTransform.GetComponent<Camera>();
                    else
                        _referenceCamera = parent.GetComponent<Camera>();
                }

                if (_referenceCamera == null && _baseStackCamera != null)
                {
                    Transform baseParent = _baseStackCamera.transform.parent;
                    if (baseParent != null)
                        _referenceCamera = baseParent.GetComponent<Camera>();
                }
            }

            ResolveSurvivalSystemReference();
            ResolveStructuralGridReference();
        }

        private bool NeedsAutoResolve()
        {
            bool needsBaseStackCamera = _projectionMode != ProjectionMode.Disabled && _baseStackCamera == null;
            bool needsReferenceCamera = _syncToReferenceCamera && _referenceCamera == null;
            bool needsHudCamera = _projectionMode != ProjectionMode.Disabled && _hudCamera == null;
            bool needsSurvivalSystem = Application.isPlaying && _survivalSystem == null;
            bool needsTraumaDispatcher = Application.isPlaying && _traumaDispatcher == null;
            bool needsStructuralGrid = Application.isPlaying && _structuralGrid == null;

            return _visorRenderer == null
                || needsHudCamera
                || needsBaseStackCamera
                || needsReferenceCamera
                || needsSurvivalSystem
                || needsTraumaDispatcher
                || needsStructuralGrid;
        }

        private static float GetAutoResolveNow()
        {
            return Application.isPlaying ? Time.unscaledTime : Time.realtimeSinceStartup;
        }

        private void EnsurePropertyBlock()
        {
            if (_mpb != null)
                return;

            // COLD ALLOC: MaterialPropertyBlock[1] â€” visor surface state bridge â€” owner: VisorHUDController
            _mpb = new MaterialPropertyBlock();
        }

        private void RefreshRuntimeState(bool forceResolve)
        {
            AutoResolveReferences(forceResolve);
            SyncProjectionPose();
            ApplyMaterialProperties();
        }

        private void ApplyMaterialProperties()
        {
            if (_visorRenderer == null)
                return;

            EnsurePropertyBlock();

            float condensationStrength = Mathf.Clamp01(_condensationShockIntensity + _criticalPressureCondensation);
            float environmentalDistortion = _interferenceDistortionIntensity * _interferenceDistortionMax;
            float hazardChromaticAberration = (_hazardRadiationLevel * 0.010f) + (_hazardGlitchLevel * 0.006f);
            float hazardStaticNoise = (_hazardGlitchLevel * 0.28f) + (_hazardToxicLevel * 0.18f);

            _visorRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(ID_HUDIntensity, _hudIntensity);
            _mpb.SetColor(ID_HUDColor, _hudTint);
            _mpb.SetFloat(ID_ScratchBleed, _scratchBleed);
            _mpb.SetFloat(ID_Distortion, _distortion + environmentalDistortion);
            _mpb.SetFloat(ID_WaterRunoffStrength, _waterRunoffIntensity);
            _mpb.SetFloat(ID_WaterRunoffSpeed, _waterRunoffSpeed);
            _mpb.SetFloat(ID_WaterRunoffDistortion, _waterRunoffDistortion);
            _mpb.SetFloat(ID_WaterDropletDensity, _waterDropletDensity);
            _mpb.SetFloat(ID_WaterDropletScale, _waterDropletScale);
            _mpb.SetFloat(ID_CondensationStrength, condensationStrength);
            _mpb.SetFloat(ID_CondensationDistortion, _condensationDistortion);
            _mpb.SetFloat(ID_CondensationEdgeExponent, _condensationEdgeExponent);
            _mpb.SetFloat(ID_CondensationDriftSpeed, _condensationDriftSpeed);
            _mpb.SetFloat(ID_ScreenFrostStrength, _screenFrostStrength);
            _mpb.SetFloat(ID_ChromaticAberration, Mathf.Max(_structuralFatigueChromaticAberration, hazardChromaticAberration));
            _mpb.SetFloat(ID_StaticNoise, Mathf.Max(_structuralFatigueStaticNoise, hazardStaticNoise));
            _mpb.SetFloat(ID_HypoxiaLevel, _hudHypoxiaLevel);
            _mpb.SetFloat(ID_HazardRadiationLevel, _hazardRadiationLevel);
            _mpb.SetFloat(ID_HazardThermalLevel, _hazardThermalLevel);
            _mpb.SetFloat(ID_HazardToxicLevel, _hazardToxicLevel);
            _mpb.SetFloat(ID_HazardGlitchLevel, _hazardGlitchLevel);
            _mpb.SetFloat(ID_BiosRecoveryMode, _biosRecoveryModeBlend);
            _visorRenderer.SetPropertyBlock(_mpb);
            _materialPropertiesDirty = false;
        }

        private void UpdateGlitchState(float deltaTime)
        {
            if (!_glitchActive)
                return;

            _glitchTimer += deltaTime;

            if (_glitchTimer >= _glitchDuration)
            {
                _hudIntensity = _glitchOriginalIntensity;
                _glitchActive = false;
                _materialPropertiesDirty = true;
                return;
            }

            float rand01 = XorShift01();
            _hudIntensity = _glitchOriginalIntensity * (0.1f + rand01 * 1.9f);
            _materialPropertiesDirty = true;
        }

        private void UpdateWaterRunoffState(float deltaTime)
        {
            if (_waterRunoffHoldTimer > 0f)
            {
                _waterRunoffHoldTimer -= deltaTime;
                if (_waterRunoffHoldTimer < 0f)
                    _waterRunoffHoldTimer = 0f;

                return;
            }

            if (_waterRunoffIntensity <= 0.001f)
            {
                if (_waterRunoffIntensity != 0f)
                {
                    _waterRunoffIntensity = 0f;
                    _materialPropertiesDirty = true;
                }

                return;
            }

            float t = 1f - Mathf.Exp(-Mathf.Max(0.1f, _waterRunoffRecoverySpeed) * deltaTime);
            float nextIntensity = Mathf.Lerp(_waterRunoffIntensity, 0f, t);
            if (!Mathf.Approximately(nextIntensity, _waterRunoffIntensity))
            {
                _waterRunoffIntensity = nextIntensity;
                _materialPropertiesDirty = true;
            }
        }

        private void ResolveSurvivalSystemReference()
        {
            if (!Application.isPlaying)
                return;

            HectonSurvivalSystem resolvedSystem = _survivalSystem;
            TraumaDispatcher resolvedTraumaDispatcher = _traumaDispatcher;
            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform currentPlayerTransform) &&
                currentPlayerTransform != null)
            {
                if (resolvedSystem == null || resolvedSystem.transform != currentPlayerTransform)
                    currentPlayerTransform.TryGetComponent(out resolvedSystem);

                if (resolvedTraumaDispatcher == null || resolvedTraumaDispatcher.transform != currentPlayerTransform)
                    currentPlayerTransform.TryGetComponent(out resolvedTraumaDispatcher);
            }

            if (_survivalSystem != resolvedSystem)
                _survivalSystem = resolvedSystem;

            if (_traumaDispatcher != resolvedTraumaDispatcher)
                _traumaDispatcher = resolvedTraumaDispatcher;

            RefreshSurvivalSubscription(_survivalSystem);
        }

        private void ResolveStructuralGridReference()
        {
            if (!Application.isPlaying)
                return;

            ISubmarineRuntimeContext submarineRuntimeContext = GlobalRegistry.Submarine;
            _submarineRuntimeContext = submarineRuntimeContext;
            _structuralGrid = submarineRuntimeContext != null ? submarineRuntimeContext.StructuralGrid : null;
        }

        private void RefreshSurvivalSubscription(HectonSurvivalSystem target)
        {
            if (_subscribedSurvivalSystem == target)
                return;

            if (_subscribedSurvivalSystem != null)
            {
                _subscribedSurvivalSystem.OnTemperatureChanged -= HandleTemperatureChanged;
                _subscribedSurvivalSystem.OnPressureChanged -= HandlePressureChanged;
            }

            _subscribedSurvivalSystem = target;

            if (_subscribedSurvivalSystem == null)
                return;

            _subscribedSurvivalSystem.OnTemperatureChanged += HandleTemperatureChanged;
            _subscribedSurvivalSystem.OnPressureChanged += HandlePressureChanged;
            HandleTemperatureChanged(_subscribedSurvivalSystem.EnvironmentTemperature);
            HandlePressureChanged(_subscribedSurvivalSystem.Pressure);
        }

        private void HandleTemperatureChanged(float temperature)
        {
            if (_hasTemperatureSample)
            {
                float delta = Mathf.Abs(temperature - _lastTemperatureSample);
                if (delta >= _temperatureShockThreshold)
                    TriggerCondensationShock(delta / Mathf.Max(0.01f, _temperatureShockThreshold));
            }

            _lastTemperatureSample = temperature;
            _hasTemperatureSample = true;
        }

        private void HandlePressureChanged(float pressure)
        {
            if (_hasPressureSample)
            {
                float delta = Mathf.Abs(pressure - _lastPressureSample);
                if (delta >= _pressureShockThreshold)
                    TriggerCondensationShock(delta / Mathf.Max(0.01f, _pressureShockThreshold));
            }

            _lastPressureSample = pressure;
            _hasPressureSample = true;

            float target = 0f;
            if (_subscribedSurvivalSystem != null && _subscribedSurvivalSystem.Stats != null)
            {
                float safeDepth = Mathf.Max(0.01f, _subscribedSurvivalSystem.Stats.SafeDepth);
                float pressureFactor = pressure / safeDepth;
                float pressureT = Mathf.InverseLerp(
                    _criticalPressureStartFactor,
                    Mathf.Max(_criticalPressureStartFactor + 0.01f, _criticalPressureFullFactor),
                    pressureFactor);
                target = pressureT * _criticalPressureCondensationMax;
            }

            if (!Mathf.Approximately(_criticalPressureCondensationTarget, target))
            {
                _criticalPressureCondensationTarget = target;
                _materialPropertiesDirty = true;
            }
        }

        private void TriggerCondensationShock(float normalizedIntensity)
        {
            float clampedIntensity = Mathf.Clamp01(normalizedIntensity);
            if (clampedIntensity <= 0f)
                return;

            if (_condensationShockIntensity < clampedIntensity)
                _condensationShockIntensity = clampedIntensity;

            _condensationShockHoldTimer = Mathf.Max(_condensationShockHoldTimer, _condensationShockHoldDuration);
            _condensationShockRecoverySpeed = Mathf.Max(0.1f, _condensationShockRecoverySpeed);
            _materialPropertiesDirty = true;
        }

        private void UpdateCondensationState(float deltaTime)
        {
            float pressureBlendT = 1f - Mathf.Exp(-Mathf.Max(0.1f, _criticalPressureCondensationBlendSpeed) * deltaTime);
            float blendedPressureCondensation = Mathf.Lerp(
                _criticalPressureCondensation,
                _criticalPressureCondensationTarget,
                pressureBlendT);
            if (!Mathf.Approximately(blendedPressureCondensation, _criticalPressureCondensation))
            {
                _criticalPressureCondensation = blendedPressureCondensation;
                _materialPropertiesDirty = true;
            }

            if (_condensationShockHoldTimer > 0f)
            {
                _condensationShockHoldTimer -= deltaTime;
                if (_condensationShockHoldTimer < 0f)
                    _condensationShockHoldTimer = 0f;

                return;
            }

            if (_condensationShockIntensity <= 0.001f)
            {
                if (_condensationShockIntensity != 0f)
                {
                    _condensationShockIntensity = 0f;
                    _materialPropertiesDirty = true;
                }

                return;
            }

            float t = 1f - Mathf.Exp(-Mathf.Max(0.1f, _condensationShockRecoverySpeed) * deltaTime);
            float nextIntensity = Mathf.Lerp(_condensationShockIntensity, 0f, t);
            if (!Mathf.Approximately(nextIntensity, _condensationShockIntensity))
            {
                _condensationShockIntensity = nextIntensity;
                _materialPropertiesDirty = true;
            }
        }

        private void UpdateFrostState(float deltaTime)
        {
            float target = 0f;
            if (_subscribedSurvivalSystem != null)
            {
                float temperature = _subscribedSurvivalSystem.EnvironmentTemperature;
                float temperatureT = Mathf.InverseLerp(_frostStartTemperature, _frostFullTemperature, temperature);
                float coldSeverity = Mathf.Clamp01(_subscribedSurvivalSystem.ColdStressSeverity01);
                target = Mathf.Max(temperatureT, coldSeverity * (0.62f + _abyssalColdFrostBoost));
                target *= _screenFrostMaximum;
            }

            if (!Mathf.Approximately(target, _screenFrostTarget))
                _screenFrostTarget = target;

            float blendT = 1f - Mathf.Exp(-Mathf.Max(0.1f, _screenFrostBlendSpeed) * deltaTime);
            float blendedFrost = Mathf.Lerp(_screenFrostStrength, _screenFrostTarget, blendT);
            if (!Mathf.Approximately(blendedFrost, _screenFrostStrength))
            {
                _screenFrostStrength = blendedFrost;
                _materialPropertiesDirty = true;
            }
        }

        private void UpdateInterferenceState(float deltaTime)
        {
            if (_interferenceDistortionHoldTimer > 0f)
            {
                _interferenceDistortionHoldTimer -= deltaTime;
                if (_interferenceDistortionHoldTimer < 0f)
                    _interferenceDistortionHoldTimer = 0f;

                return;
            }

            if (_interferenceDistortionIntensity <= 0.001f)
            {
                if (_interferenceDistortionIntensity != 0f)
                {
                    _interferenceDistortionIntensity = 0f;
                    _materialPropertiesDirty = true;
                }

                return;
            }

            float t = 1f - Mathf.Exp(-Mathf.Max(0.1f, _interferenceDistortionRecoverySpeed) * deltaTime);
            float nextIntensity = Mathf.Lerp(_interferenceDistortionIntensity, 0f, t);
            if (!Mathf.Approximately(nextIntensity, _interferenceDistortionIntensity))
            {
                _interferenceDistortionIntensity = nextIntensity;
                _materialPropertiesDirty = true;
            }
        }

        private void UpdateStructuralFatigueState(float deltaTime)
        {
            float fatigue01 = ResolveStructuralFatigue01();
            float targetChromaticAberration = fatigue01 * Mathf.Max(0f, _structuralFatigueChromaticAberrationMax);
            float targetStaticNoise = fatigue01 * Mathf.Max(0f, _structuralFatigueStaticNoiseMax);
            float blendT = 1f - Mathf.Exp(-Mathf.Max(0.1f, _structuralFatigueBlendSharpness) * deltaTime);

            float nextChromaticAberration = Mathf.Lerp(_structuralFatigueChromaticAberration, targetChromaticAberration, blendT);
            if (!Mathf.Approximately(nextChromaticAberration, _structuralFatigueChromaticAberration))
            {
                _structuralFatigueChromaticAberration = nextChromaticAberration;
                _materialPropertiesDirty = true;
            }

            float nextStaticNoise = Mathf.Lerp(_structuralFatigueStaticNoise, targetStaticNoise, blendT);
            if (!Mathf.Approximately(nextStaticNoise, _structuralFatigueStaticNoise))
            {
                _structuralFatigueStaticNoise = nextStaticNoise;
                _materialPropertiesDirty = true;
            }
        }

        private void UpdateHazardTraumaState(float deltaTime)
        {
            float targetRadiation = 0f;
            float targetThermal = 0f;
            float targetToxic = 0f;
            float targetBiosRecovery = 0f;
            if (_traumaDispatcher != null)
            {
                targetRadiation = Mathf.Clamp01(_traumaDispatcher.HazardRadiationSignal01);
                targetThermal = Mathf.Clamp01(_traumaDispatcher.HazardThermalSignal01);
                targetToxic = Mathf.Clamp01(_traumaDispatcher.HazardToxicSignal01);
                targetBiosRecovery = _traumaDispatcher.BiosRecoveryModeActive ? 1f : 0f;
            }

            float targetGlitch = Mathf.Clamp01(Mathf.Max(
                targetRadiation,
                Mathf.Max(targetThermal * 0.82f, targetToxic * 0.91f)));
            float blendT = 1f - Mathf.Exp(-Mathf.Max(0.1f, _structuralFatigueBlendSharpness) * deltaTime);

            if (UpdateSmoothedVisualChannel(ref _hazardRadiationLevel, targetRadiation, blendT))
                _materialPropertiesDirty = true;

            if (UpdateSmoothedVisualChannel(ref _hazardThermalLevel, targetThermal, blendT))
                _materialPropertiesDirty = true;

            if (UpdateSmoothedVisualChannel(ref _hazardToxicLevel, targetToxic, blendT))
                _materialPropertiesDirty = true;

            if (UpdateSmoothedVisualChannel(ref _hazardGlitchLevel, targetGlitch, blendT))
                _materialPropertiesDirty = true;

            if (UpdateSmoothedVisualChannel(ref _biosRecoveryModeBlend, targetBiosRecovery, blendT))
                _materialPropertiesDirty = true;
        }

        private void UpdateHypoxiaState(float deltaTime)
        {
            float targetHypoxia = 0f;
            if (_subscribedSurvivalSystem != null)
            {
                float oxygenNormalized = Mathf.Clamp01(_subscribedSurvivalSystem.OxygenNormalized);
                float safeThreshold = Mathf.Clamp(_hypoxiaStartThreshold, 0.01f, 1f);
                if (oxygenNormalized < safeThreshold)
                    targetHypoxia = 1f - Mathf.Clamp01(oxygenNormalized / safeThreshold);
            }

            float blendT = 1f - Mathf.Exp(-Mathf.Max(0.1f, _hypoxiaBlendSharpness) * deltaTime);
            float nextHypoxia = Mathf.Lerp(_hudHypoxiaLevel, targetHypoxia, blendT);
            if (!Mathf.Approximately(nextHypoxia, _hudHypoxiaLevel))
            {
                _hudHypoxiaLevel = nextHypoxia;
                _materialPropertiesDirty = true;
            }
        }

        private float ResolveStructuralFatigue01()
        {
            if (_structuralGrid != null && _structuralGrid.isActiveAndEnabled && _structuralGrid.IsReady)
                return Mathf.Clamp01(_structuralGrid.FatiguePeakNormalized);

            return 0f;
        }

        private static bool UpdateSmoothedVisualChannel(ref float current, float target, float blendT)
        {
            float nextValue = Mathf.Lerp(current, target, blendT);
            if (Mathf.Approximately(nextValue, current))
                return false;

            current = nextValue;
            return true;
        }

        private void PrepareProjectionTexture()
        {
            if (_projectionMode == ProjectionMode.Disabled)
            {
                ReleaseOwnedRuntimeTexture();
                _hudRT = null;
                _ownsRuntimeTexture = false;
                _cachedRTWidth = -1;
                _cachedRTHeight = -1;
                _cachedEffectiveRenderScale = -1f;
                return;
            }

            if (_projectionMode == ProjectionMode.SharedRenderTexture && _sharedRenderTexture != null)
            {
                ReleaseOwnedRuntimeTexture();
                _hudRT = _sharedRenderTexture;
                _ownsRuntimeTexture = false;
                _cachedRTWidth = -1;
                _cachedRTHeight = -1;
                _cachedEffectiveRenderScale = -1f;
                return;
            }

            if (!_ownsRuntimeTexture)
                _hudRT = null;

            float effectiveRenderScale = ResolveEffectiveRuntimeRenderScale();
            int targetWidth;
            int targetHeight;
            CalculateTargetRTDimensions(effectiveRenderScale, out targetWidth, out targetHeight);

            // Reuse RT if size matches
            if (_hudRT != null && _hudRT.width == targetWidth && _hudRT.height == targetHeight && _hudRT.format == RenderTextureFormat.ARGB32)
            {
                _hudRT.filterMode = _filterMode;
                if (!_hudRT.IsCreated())
                    _hudRT.Create();
                _ownsRuntimeTexture = true;
                _cachedRTWidth = targetWidth;
                _cachedRTHeight = targetHeight;
                _cachedEffectiveRenderScale = effectiveRenderScale;
                return;
            }

            // Release old RT if size changed
            ReleaseOwnedRuntimeTexture();

            // Rent RT from pool (zero-GC, O(1) lookup)
            _hudRT = RenderTexturePool.Instance.Rent(targetWidth, targetHeight, RenderTextureFormat.ARGB32, this);
            _hudRT.filterMode = _filterMode;
            _hudRT.useMipMap = false;
            _hudRT.name = "VisorHUD_RT_Scaled";
            if (!_hudRT.IsCreated())
                _hudRT.Create();
            _ownsRuntimeTexture = true;
            _cachedRTWidth = targetWidth;
            _cachedRTHeight = targetHeight;
            _cachedEffectiveRenderScale = effectiveRenderScale;
        }

        private void RefreshAdaptiveRuntimeProjection()
        {
            if (!Application.isPlaying ||
                _projectionMode != ProjectionMode.RuntimeRenderTexture ||
                _hudCamera == null)
            {
                return;
            }

            float effectiveRenderScale = ResolveEffectiveRuntimeRenderScale();
            int targetWidth;
            int targetHeight;
            CalculateTargetRTDimensions(effectiveRenderScale, out targetWidth, out targetHeight);

            if (_hudRT != null &&
                _cachedRTWidth == targetWidth &&
                _cachedRTHeight == targetHeight &&
                Mathf.Approximately(_cachedEffectiveRenderScale, effectiveRenderScale))
            {
                return;
            }

            PrepareProjectionTexture();
            BindRT();
        }

        private float ResolveEffectiveRuntimeRenderScale()
        {
            float effectiveScale = Mathf.Clamp01(_renderScale);
            if (!_enableAdaptiveRuntimeRTScaling || !Application.isPlaying)
                return QuantizeAdaptiveScale(Mathf.Clamp(effectiveScale, 0.1f, 1f));

            DynamicResolutionScaler scaler = DynamicResolutionScaler.Instance;
            if (scaler != null && scaler.Enabled)
                effectiveScale *= Mathf.Clamp01(scaler.CurrentRenderScale);

            VRAMMonitor vramMonitor = VRAMMonitor.Instance;
            if (vramMonitor != null)
            {
                switch (vramMonitor.PressureState)
                {
                    case VRAMMonitor.VRAMPressureState.Critical:
                        effectiveScale *= _adaptiveVRAMCriticalScale;
                        break;

                    case VRAMMonitor.VRAMPressureState.Warning:
                        effectiveScale *= _adaptiveVRAMWarningScale;
                        break;
                }
            }

            effectiveScale = Mathf.Clamp(effectiveScale, _adaptiveRuntimeRTMinScale, 1f);
            return QuantizeAdaptiveScale(effectiveScale);
        }

        private void CalculateTargetRTDimensions(float effectiveRenderScale, out int targetWidth, out int targetHeight)
        {
            int baseWidth = _matchScreenResolution ? Screen.width : _rtWidth;
            int baseHeight = _matchScreenResolution ? Screen.height : _rtHeight;
            float clampedScale = Mathf.Clamp(effectiveRenderScale, 0.1f, 1f);
            targetWidth = Mathf.Max(32, Mathf.RoundToInt(baseWidth * clampedScale));
            targetHeight = Mathf.Max(32, Mathf.RoundToInt(baseHeight * clampedScale));
        }

        private float QuantizeAdaptiveScale(float scale)
        {
            float quantizationStep = Mathf.Max(0.01f, _adaptiveScaleQuantizationStep);
            return Mathf.Round(scale / quantizationStep) * quantizationStep;
        }

        private void RebuildProjection()
        {
            InvalidatePoseCache();
            PrepareProjectionTexture();
            SyncCameraRole();
            BindRT();
        }

        private void BindRT()
        {
            EnsurePropertyBlock();

            if (_hudCamera != null)
                _hudCamera.targetTexture = _projectionMode == ProjectionMode.Disabled ? null : _hudRT;

            if (_visorRenderer == null)
                return;

            _visorRenderer.GetPropertyBlock(_mpb);
            Texture hudTexture = _hudRT != null ? (Texture)_hudRT : Texture2D.blackTexture;
            _mpb.SetTexture(ID_HUDTex, hudTexture);
            _visorRenderer.SetPropertyBlock(_mpb);
            _materialPropertiesDirty = true;
        }

        private void ReleaseRT()
        {
            if (_hudCamera != null)
            {
                _hudCamera.targetTexture = null;
                _hudCamera.enabled = true;
            }

            ReleaseOwnedRuntimeTexture();

            _hudRT = null;
            _ownsRuntimeTexture = false;
            _cachedRTWidth = -1;
            _cachedRTHeight = -1;
            _cachedEffectiveRenderScale = -1f;
        }

        private void ReleaseOwnedRuntimeTexture()
        {
            if (!_ownsRuntimeTexture || _hudRT == null)
                return;

            // Register disposal with lifecycle tracker
            if (RenderTextureLifecycleTracker.Instance != null)
                RenderTextureLifecycleTracker.Instance.RegisterDisposal(_hudRT);

            // Return to pool for reuse (zero-GC)
            if (RenderTexturePool.Instance != null)
                RenderTexturePool.Instance.Return(_hudRT);
            else
            {
                // Fallback if pool not available (Editor mode or shutdown)
                _hudRT.Release();
                if (Application.isPlaying)
                    Destroy(_hudRT);
                else
                    DestroyImmediate(_hudRT);
            }
        }

        private void SuspendEditModeProjection()
        {
            if (Application.isPlaying || _editorPreviewSuspended)
                return;

            if (_hudCamera != null)
            {
                _hudCamera.targetTexture = null;
                _hudCamera.enabled = false;
            }

            ReleaseOwnedRuntimeTexture();
            _hudRT = null;
            _ownsRuntimeTexture = false;
            _cachedRTWidth = -1;
            _cachedRTHeight = -1;
            _cachedEffectiveRenderScale = -1f;

            if (_visorRenderer != null)
            {
                EnsurePropertyBlock();
                _visorRenderer.GetPropertyBlock(_mpb);
                _mpb.SetTexture(ID_HUDTex, Texture2D.blackTexture);
                _visorRenderer.SetPropertyBlock(_mpb);
            }

            _editorPreviewSuspended = true;
        }

        private void ResumeEditModeProjection()
        {
            if (Application.isPlaying || !_editorPreviewSuspended)
                return;

            _editorPreviewSuspended = false;
            _materialPropertiesDirty = true;
            RebuildProjection();
        }

        /// <summary>
        /// Configures the HUD camera so projection rendering stays inside the URP pipeline.
        /// </summary>
        private void SyncCameraRole()
        {
            if (_hudCamera == null)
                return;

            UniversalAdditionalCameraData hudCameraData = GetCachedHudCameraData();
            if (hudCameraData == null)
                return;

            Camera stackBaseCamera = ResolveHudStackBaseCamera();
            UniversalAdditionalCameraData baseCameraData = GetCameraData(stackBaseCamera);

            bool projected = _projectionMode != ProjectionMode.Disabled;
            if (projected)
            {
                hudCameraData.renderType = CameraRenderType.Base;
                RemoveHudCameraFromKnownStacks(stackBaseCamera, baseCameraData);

                _hudCamera.clearFlags = CameraClearFlags.SolidColor;
                _hudCamera.backgroundColor = Color.clear;
                _hudCamera.enabled = true;
                return;
            }

            // Overlay fallback renders through the screen-space HUD canvas, so the HUD camera
            // must not stay in any URP stack. Leaving it stacked reintroduces a broken runtime
            // path on renderers that report stacking inconsistently.
            RemoveHudCameraFromKnownStacks(stackBaseCamera, baseCameraData);
            hudCameraData.renderType = CameraRenderType.Base;
            _hudCamera.clearFlags = CameraClearFlags.Depth;
            _hudCamera.enabled = false;
        }

        private bool ShouldUseHudBaseDepthFallback(
            Camera stackBaseCamera,
            UniversalAdditionalCameraData baseCameraData)
        {
            if (_hudCamera == null)
                return true;

            if (stackBaseCamera == null || baseCameraData == null)
                return true;

            ScriptableRenderer baseRenderer = baseCameraData.scriptableRenderer;
            if (baseRenderer == null)
                return true;

            return !baseRenderer.SupportsCameraStackingType(CameraRenderType.Base) ||
                   !baseRenderer.SupportsCameraStackingType(CameraRenderType.Overlay);
        }

        private void ApplyHudBaseDepthFallback(
            UniversalAdditionalCameraData hudCameraData,
            Camera stackBaseCamera,
            UniversalAdditionalCameraData baseCameraData)
        {
            hudCameraData.renderType = CameraRenderType.Base;
            RemoveHudCameraFromKnownStacks(stackBaseCamera, baseCameraData);

            float fallbackDepth = ResolveHudFallbackDepth();
            if (!Mathf.Approximately(_hudCamera.depth, fallbackDepth))
                _hudCamera.depth = fallbackDepth;

            _hudCamera.clearFlags = CameraClearFlags.Depth;
            _hudCamera.enabled = true;
        }

        private void RemoveHudCameraFromKnownStacks(
            Camera stackBaseCamera,
            UniversalAdditionalCameraData baseCameraData)
        {
            if (_hudCamera == null)
                return;

            RemoveHudCameraFromStack(baseCameraData);
            RemoveHudCameraFromStack(GetCameraData(stackBaseCamera));
            RemoveHudCameraFromStack(GetCameraData(_baseStackCamera));
            RemoveHudCameraFromStack(GetCameraData(_referenceCamera));
        }

        private void RemoveHudCameraFromStack(UniversalAdditionalCameraData cameraData)
        {
            if (cameraData == null || cameraData.cameraStack == null)
                return;

            if (cameraData.cameraStack.Contains(_hudCamera))
                cameraData.cameraStack.Remove(_hudCamera);
        }

        private float ResolveHudFallbackDepth()
        {
            float fallbackDepth = _hudCamera != null ? _hudCamera.depth : 0f;

            if (_baseStackCamera != null)
                fallbackDepth = Mathf.Max(fallbackDepth, _baseStackCamera.depth + 2f);

            if (_referenceCamera != null)
                fallbackDepth = Mathf.Max(fallbackDepth, _referenceCamera.depth + 1f);

            return fallbackDepth;
        }

        private Camera ResolveHudStackBaseCamera()
        {
            if (_referenceCamera != null &&
                _referenceCamera != _hudCamera &&
                TryGetBaseCameraData(_referenceCamera, out _))
            {
                return _referenceCamera;
            }

            return EnsureValidBaseStackCamera() ? _baseStackCamera : null;
        }

        private bool EnsureValidBaseStackCamera()
        {
            if (HasValidBaseStackCamera())
                return true;

            Camera resolvedCamera = TryResolveBaseStackCameraFromHierarchy();
            if (resolvedCamera == null)
                return false;

            if (_baseStackCamera != resolvedCamera)
            {
                _baseStackCamera = resolvedCamera;
                _cachedBaseCameraData = null;
            }

            return HasValidBaseStackCamera();
        }

        private bool HasValidBaseStackCamera()
        {
            return TryGetBaseCameraData(_baseStackCamera, out _);
        }

        private Camera TryResolveBaseStackCameraFromHierarchy()
        {
            Camera resolvedCamera = TryResolveBaseStackCameraFromTransform(
                _referenceCamera != null ? _referenceCamera.transform : null);
            if (resolvedCamera != null)
                return resolvedCamera;

            resolvedCamera = TryResolveBaseStackCameraFromTransform(
                _baseStackCamera != null ? _baseStackCamera.transform : null);
            if (resolvedCamera != null)
                return resolvedCamera;

            Transform parent = transform.parent;
            if (parent == null)
                return null;

            Transform mainCameraTransform = parent.Find("Main Camera");
            if (mainCameraTransform == null)
                return null;

            Transform spaceCameraTransform = mainCameraTransform.Find("SpaceCamera");
            return spaceCameraTransform != null ? spaceCameraTransform.GetComponent<Camera>() : null;
        }

        private static Camera TryResolveBaseStackCameraFromTransform(Transform sourceTransform)
        {
            if (sourceTransform == null)
                return null;

            Transform spaceCameraTransform = sourceTransform.Find("SpaceCamera");
            if (spaceCameraTransform != null)
            {
                Camera directCamera = spaceCameraTransform.GetComponent<Camera>();
                if (directCamera != null)
                    return directCamera;
            }

            Transform parent = sourceTransform.parent;
            if (parent == null)
                return null;

            Transform siblingSpaceCameraTransform = parent.Find("SpaceCamera");
            if (siblingSpaceCameraTransform == null)
                return null;

            return siblingSpaceCameraTransform.GetComponent<Camera>();
        }

        private void SyncProjectionPose()
        {
            if (!_syncToReferenceCamera || _referenceCamera == null)
                return;

            if (!Application.isPlaying && !_syncPoseInEditMode)
                return;

            Transform referenceTransform = _referenceCamera.transform;
            Vector3 referencePosition = referenceTransform.position;
            Quaternion referenceRotation = referenceTransform.rotation;
            Vector3 visorOffset = _visorLocalOffset;
            visorOffset.z = Mathf.Max(visorOffset.z, _minimumVisorForwardOffset);

            if (_enforceNearClipSafeOffset)
            {
                float nearClipSafeOffset = _referenceCamera.nearClipPlane + 0.12f;
                visorOffset.z = Mathf.Max(visorOffset.z, nearClipSafeOffset);
            }

            Quaternion visorRotation = referenceRotation * GetCachedVisorOffsetRotation();
            Vector3 visorPosition = referencePosition + referenceRotation * visorOffset;
            if (!_poseApplied || _appliedVisorPosition != visorPosition || _appliedVisorRotation != visorRotation)
            {
                transform.SetPositionAndRotation(visorPosition, visorRotation);
                _appliedVisorPosition = visorPosition;
                _appliedVisorRotation = visorRotation;
                _poseApplied = true;
            }

            if (_appliedVisorScale != _visorLocalScale)
            {
                transform.localScale = _visorLocalScale;
                _appliedVisorScale = _visorLocalScale;
            }

            if (_hudCamera == null)
                return;

            Transform hudTransform = _hudCamera.transform;
            Quaternion hudRotation = referenceRotation * GetCachedHudOffsetRotation();
            Vector3 hudPosition = referencePosition + referenceRotation * _hudCameraLocalOffset;
            if (!_hudPoseApplied || _appliedHudPosition != hudPosition || _appliedHudRotation != hudRotation)
            {
                hudTransform.SetPositionAndRotation(hudPosition, hudRotation);
                _appliedHudPosition = hudPosition;
                _appliedHudRotation = hudRotation;
                _hudPoseApplied = true;
            }
        }

        private UniversalAdditionalCameraData GetCachedHudCameraData()
        {
            if (_hudCamera == null)
                return null;

            if (_cachedHudCameraData == null || _cachedHudCameraData.gameObject != _hudCamera.gameObject)
                _cachedHudCameraData = _hudCamera.GetComponent<UniversalAdditionalCameraData>();

            return _cachedHudCameraData;
        }

        private UniversalAdditionalCameraData GetCachedBaseCameraData()
        {
            if (_baseStackCamera == null)
                return null;

            if (_cachedBaseCameraData == null || _cachedBaseCameraData.gameObject != _baseStackCamera.gameObject)
                _cachedBaseCameraData = _baseStackCamera.GetComponent<UniversalAdditionalCameraData>();

            return _cachedBaseCameraData;
        }

        private static UniversalAdditionalCameraData GetCameraData(Camera camera)
        {
            return camera != null ? camera.GetComponent<UniversalAdditionalCameraData>() : null;
        }

        private static bool TryGetBaseCameraData(Camera camera, out UniversalAdditionalCameraData cameraData)
        {
            cameraData = GetCameraData(camera);
            return cameraData != null && cameraData.renderType == CameraRenderType.Base;
        }

        private void InvalidatePoseCache()
        {
            _cachedHudCameraData = null;
            _cachedBaseCameraData = null;
            _poseApplied = false;
            _appliedVisorPosition = default;
            _appliedVisorRotation = default;
            _appliedVisorScale = default;
            _hudPoseApplied = false;
            _appliedHudPosition = default;
            _appliedHudRotation = default;
            _cachedVisorEulerOffset = default;
            _cachedVisorOffsetRotation = Quaternion.identity;
            _cachedHudEulerOffset = default;
            _cachedHudOffsetRotation = Quaternion.identity;
        }

        private Quaternion GetCachedVisorOffsetRotation()
        {
            if (_cachedVisorEulerOffset != _visorLocalEulerOffset)
            {
                _cachedVisorEulerOffset = _visorLocalEulerOffset;
                _cachedVisorOffsetRotation = Quaternion.Euler(_visorLocalEulerOffset);
            }

            return _cachedVisorOffsetRotation;
        }

        private Quaternion GetCachedHudOffsetRotation()
        {
            if (_cachedHudEulerOffset != _hudCameraLocalEulerOffset)
            {
                _cachedHudEulerOffset = _hudCameraLocalEulerOffset;
                _cachedHudOffsetRotation = Quaternion.Euler(_hudCameraLocalEulerOffset);
            }

            return _cachedHudOffsetRotation;
        }

        public void SetHUDIntensity(float intensity)
        {
            float clampedIntensity = Mathf.Clamp(intensity, 0f, 5f);
            if (Mathf.Approximately(_hudIntensity, clampedIntensity))
                return;

            _hudIntensity = clampedIntensity;
            _materialPropertiesDirty = true;
        }

        public void SetProjectionMode(ProjectionMode projectionMode)
        {
            if (_projectionMode == projectionMode)
                return;

            _projectionMode = projectionMode;
            RebuildProjection();
        }

        public void SetSharedRenderTexture(RenderTexture sharedRenderTexture)
        {
            if (_sharedRenderTexture == sharedRenderTexture)
                return;

            _sharedRenderTexture = sharedRenderTexture;
            InvalidatePoseCache();
            if (_projectionMode == ProjectionMode.SharedRenderTexture)
                RebuildProjection();
        }

        /// <summary>
        /// Starts a deterministic glitch pulse without coroutines or heap allocations.
        /// </summary>
        public void GlitchPulse(float duration = 0.3f)
        {
            if (!_glitchActive)
                _glitchOriginalIntensity = _hudIntensity;

            _glitchActive = true;
            _glitchTimer = 0f;
            _glitchDuration = duration;
            _glitchRngState = (uint)(Time.unscaledTime * 1000f) | 1u;
        }

        /// <summary>
        /// Triggers a short visor runoff pulse when crossing into water.
        /// </summary>
        public void TriggerSubmergeRunoff()
        {
            TriggerWaterRunoff(
                _submergeRunoffIntensity,
                _submergeRunoffHoldDuration,
                _submergeRunoffRecoverySpeed);
        }

        /// <summary>
        /// Triggers a stronger visor runoff pulse when breaking back to the surface.
        /// </summary>
        public void TriggerSurfaceBreakRunoff()
        {
            float holdDuration = _surfaceRunoffHoldDuration;
            float recoverySpeed = _surfaceRunoffRecoverySpeed;
            float desiredLifetime = Mathf.Max(0f, _surfaceBreakRunoffMinimumLifetime);
            float remainingRecoveryWindow = desiredLifetime - holdDuration;
            if (remainingRecoveryWindow > 0.05f)
            {
                // Exponential decay reaches ~1% after ~4.6 / speed seconds.
                float maximumRecoverySpeed = 4.6f / remainingRecoveryWindow;
                recoverySpeed = Mathf.Min(recoverySpeed, Mathf.Max(0.1f, maximumRecoverySpeed));
            }

            TriggerWaterRunoff(
                _surfaceRunoffIntensity,
                holdDuration,
                recoverySpeed);
        }

        internal void TriggerEnvironmentalDistortion(float normalizedIntensity, float holdDuration, float recoverySpeed)
        {
            float clampedIntensity = Mathf.Clamp01(normalizedIntensity);
            if (clampedIntensity <= 0f)
                return;

            if (_interferenceDistortionIntensity < clampedIntensity)
                _interferenceDistortionIntensity = clampedIntensity;

            _interferenceDistortionHoldTimer = Mathf.Max(_interferenceDistortionHoldTimer, holdDuration);
            _interferenceDistortionRecoverySpeed = Mathf.Max(0.1f, recoverySpeed);
            _materialPropertiesDirty = true;
        }

        private void TriggerWaterRunoff(float intensity, float holdDuration, float recoverySpeed)
        {
            float clampedIntensity = Mathf.Clamp01(intensity);
            if (clampedIntensity <= 0f)
                return;

            if (_waterRunoffIntensity < clampedIntensity)
                _waterRunoffIntensity = clampedIntensity;

            _waterRunoffHoldTimer = Mathf.Max(_waterRunoffHoldTimer, holdDuration);
            _waterRunoffRecoverySpeed = Mathf.Max(0.1f, recoverySpeed);
            _materialPropertiesDirty = true;
        }

#if UNITY_EDITOR
        private static bool IsEditorPreviewActive()
        {
            return !EditorApplication.isCompiling &&
                   !EditorApplication.isUpdating;
        }

        private bool ShouldTickInEditMode()
        {
            if (Application.isPlaying || !isActiveAndEnabled || !_previewInEditMode)
                return false;

            if (_materialPropertiesDirty)
                return true;

            if (_syncToReferenceCamera && _syncPoseInEditMode)
                return true;

            return NeedsAutoResolve();
        }

        private void EvaluateEditorTickRegistration()
        {
            if (ShouldTickInEditMode())
            {
                RegisterEditorTick();
                return;
            }

            UnregisterEditorTick();
        }

        private void RegisterEditorTick()
        {
            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
        }

        private void UnregisterEditorTick()
        {
            EditorApplication.update -= EditorTick;
        }
#endif
    }
}
