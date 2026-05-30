using System;
using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Optimization;
using Hecton8.Tools;
using Hecton8.UI;
using Hecton8.World;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
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
    public class VisorHUDController : MonoBehaviour, ILateFrameTickable, ISlowTickable, ISubmarineOsEventListener, IGlobalRegistryHotSwapListener
    {
        private const float BiosRecoveryClarityThreshold = 0.1f;
        private const float LowPowerBiosThreshold = 0.15f;
        private const float InvGlitchMantissaScale = 1.1920928955078125e-7f;
        private const int MaxBrownoutSignalsPerTick = 4;
        private const int RuntimeHudCompositeMaxWidth = 1280;
        private const int RuntimeHudCompositeMaxHeight = 720;
        private const string HudPhosphorKeyword = "_HUD_PHOSPHOR_MODE";
        private const float MinimumProjectedHudIntensity = 1.25f;
        private const float RadiationFatigueMinimumScale = 0.65f;
        private const float RadiationFatigueScalePerSecond = 0.005f;
        private const float RadiationFatigueCriticalExposureSeconds = (1f - RadiationFatigueMinimumScale) / RadiationFatigueScalePerSecond;
        private const int ActiveControllerCapacity = 8;
        private static VisorHUDController s_activeController0;
        private static VisorHUDController s_activeController1;
        private static VisorHUDController s_activeController2;
        private static VisorHUDController s_activeController3;
        private static VisorHUDController s_activeController4;
        private static VisorHUDController s_activeController5;
        private static VisorHUDController s_activeController6;
        private static VisorHUDController s_activeController7;
        private static int s_activeControllerCount;
        private static int s_hudPhosphorModeUserCount;

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
        [SerializeField] private Light _strongestSceneLightSource;

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
        [SerializeField, Range(-10f, 10f)] private float _coldCondensationStartTemperature = 4f;
        [SerializeField, Range(-20f, 4f)] private float _coldCondensationFullTemperature = -6f;
        [SerializeField, Range(0f, 1f)] private float _coldCondensationMaximum = 0.42f;

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

        [Header("Pressure Flicker")]
        [SerializeField, Range(0f, 1f)] private float _pressureFlickerMaximum = 0.42f;
        [SerializeField, Range(0.25f, 12f)] private float _pressureFlickerBlendSharpness = 4.2f;

        [Header("Pressure Lens Crack")]
        [SerializeField, Range(0f, 8000f)] private float _pressureLensCrackStartDepthMeters = 4000f;
        [SerializeField, Range(1f, 3000f)] private float _pressureLensCrackFullDepthRangeMeters = 900f;
        [SerializeField, Range(0.25f, 12f)] private float _pressureLensCrackBlendSharpness = 2.6f;

        [Header("BIOS Recovery")]
        [SerializeField] private Color _biosRecoveryHudTint = new Color(0.16f, 1f, 0.22f, 0.18f);
        [SerializeField, Range(0f, 5f)] private float _biosRecoveryHudIntensity = 2.2f;
        [SerializeField] private TMP_FontAsset _terminalBiosFont;
        [SerializeField] private bool _enableBiosFontSwap = true;
        [SerializeField, Range(0f, 200f)] private float _thermalShockBiosRecoveryTemperature = 80f;
        [SerializeField, Range(0f, 200f)] private float _thermalShockBiosRecoverySpike = 80f;
        [SerializeField, Range(0f, 10f)] private float _thermalShockBiosRecoveryHoldSeconds = 2f;

        [Header("Pose Lock")]
        [SerializeField] private bool _syncToReferenceCamera = true;
        [SerializeField] private bool _syncPoseInEditMode = false;
        [SerializeField] private Vector3 _visorLocalOffset = new Vector3(0f, 0f, 0.05f);
        [SerializeField] private Vector3 _visorLocalEulerOffset = Vector3.zero;
        [SerializeField] private Vector3 _visorLocalScale = new Vector3(1f, 1f, 0.6f);
        [SerializeField] private Vector3 _hudCameraLocalOffset = Vector3.zero;
        [SerializeField] private Vector3 _hudCameraLocalEulerOffset = Vector3.zero;
        [SerializeField] private float _minimumVisorForwardOffset = 0.05f;
        [SerializeField] private bool _enforceNearClipSafeOffset = false;

        private const float AutoResolveRetryInterval = 1f;
        private const float VisorPropertyFloatWriteEpsilon = 0.0005f;

        private RenderTexture _hudRT;
        private MaterialPropertyBlock _mpb;
        private bool _hudScissorCommandBufferRepairRequested = true;
        private bool _scriptableRenderPipelineActiveCold;
        private bool _ownsRuntimeTexture;
        // COLD ALLOC: LabelSwapScheduler[1] â€” staged BIOS HUD font swap queue â€” owner: VisorHUDController
        private readonly LabelSwapScheduler _biosFontSwapScheduler = new LabelSwapScheduler();
        private int _cachedRTWidth = -1;
        private int _cachedRTHeight = -1;
        private int _cachedScreenWidth = RuntimeHudCompositeMaxWidth;
        private int _cachedScreenHeight = RuntimeHudCompositeMaxHeight;
        private float _cachedEffectiveRenderScale = -1f;
        private float _nextAutoResolveAt;
        private bool _materialPropertiesDirty = true;
        private Renderer _appliedHudTextureRenderer;
        private Texture _appliedHudTexture;
        private Texture _appliedGlobalHudTexture;
        private Renderer _appliedVisorPropertyRenderer;
        private bool _hasAppliedVisorMaterialProperties;
        private bool _hasResolvedDynamicVisorMaterialInputs;
        private Renderer _appliedMotionVectorRenderer;
        private float _appliedHudIntensity;
        private Color _appliedHudColor;
        private float _appliedScratchBleed;
        private float _appliedDistortion;
        private float _appliedWaterRunoffStrength;
        private float _appliedDropletAlpha;
        private float _appliedWaterRunoffSpeed;
        private float _appliedWaterRunoffDistortion;
        private float _appliedWaterDropletDensity;
        private float _appliedWaterDropletScale;
        private float _appliedCondensationStrength;
        private float _appliedCondensationDistortion;
        private float _appliedCondensationEdgeExponent;
        private float _appliedCondensationDriftSpeed;
        private float _appliedScreenFrostStrength;
        private float _appliedChromaticAberration;
        private float _appliedStaticNoise;
        private float _appliedScalableRefractionScale;
        private float _appliedScalableChromaticScale;
        private float _appliedQualityPressureDitherScale;
        private float _cachedQualityPressure01 = -1f;
        private float _cachedScalableRefractionScale;
        private float _cachedScalableChromaticScale;
        private float _cachedQualityPressureDitherScale;
        private float _memoryQualityPressureFloor01 = 0.15f;
        private float _appliedHypoxiaLevel;
        private float _appliedHullStressFlicker;
        private float _appliedHazardRadiationLevel;
        private float _appliedHazardThermalLevel;
        private float _appliedHazardToxicLevel;
        private float _appliedHazardGlitchLevel;
        private float _appliedBiosRecoveryMode;
        private float _appliedPressureLensCrackIntensity;
        private float _appliedToolBatteryNormalized;
        private float _appliedToolHeatNormalized;
        private float _appliedToolAmmoUnits;
        private float _appliedToolDistanceMeters;
        private Vector4 _appliedVisorCameraForward;
        private Vector4 _appliedStrongestLightDirection;
        private float _resolvedToolBatteryNormalized;
        private float _resolvedToolHeatNormalized;
        private float _resolvedToolAmmoUnits;
        private float _resolvedToolDistanceMeters;
        private Vector3 _resolvedVisorCameraForward;
        private Vector4 _resolvedStrongestLightDirection;
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
        private float _dropletAlpha;
        private float _dropletFadeTimer;
        private float _waterRunoffIntensity;
        private float _waterRunoffHoldTimer;
        private float _waterRunoffRecoverySpeed;
        private float _condensationShockIntensity;
        private float _condensationShockHoldTimer;
        private float _criticalPressureCondensationTarget;
        private float _criticalPressureCondensation;
        private float _coldCondensationTarget;
        private float _coldCondensation;
        private float _screenFrostTarget;
        private float _screenFrostStrength;
        private float _interferenceDistortionIntensity;
        private float _interferenceDistortionHoldTimer;
        private float _interferenceDistortionRecoverySpeed;
        private bool _runtimeLateFrameRegistered;
        private bool _runtimeSlowTickRegistered;
        private bool _editorPreviewSuspended;
        private bool _editorReferencePoseCached;
        private Camera _editorLastReferenceCamera;
        private Vector3 _editorLastReferencePosition;
        private Quaternion _editorLastReferenceRotation;
        private float _editorLastReferenceNearClip;
        private HectonSurvivalSystem _survivalSystem;
        private TraumaDispatcher _traumaDispatcher;
        private HectonPlayerHealth _playerHealth;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private IModularEquipmentService _cachedModularEquipment;
        private IVramBudgetReadModel _cachedVramMonitor;
        private IRenderTexturePoolService _cachedRenderTexturePool;
        private IRenderTextureLifecycleService _cachedRenderTextureLifecycle;
        private HectonSurvivalSystem _subscribedSurvivalSystem;
        private uint _survivalVitalsSourceId;
        private uint _lastSurvivalVitalsSignalSequence;
        private ISubmarineRuntimeContext _submarineRuntimeContext;
        private ISubmarineHullBreachReadModel _hullBreachReadModel;
        private bool _hasTemperatureSample;
        private float _lastTemperatureSample;
        private bool _hasPressureSample;
        private float _lastPressureSample;
        private float _structuralFatigueChromaticAberration;
        private float _structuralFatigueStaticNoise;
        private float _hudHypoxiaLevel;
        private float _hudHullStressFlicker;
        private float _hazardRadiationLevel;
        private float _hazardThermalLevel;
        private float _hazardToxicLevel;
        private float _hazardGlitchLevel;
        private float _biosRecoveryModeBlend;
        private float _pressureLensCrackIntensity;
        private float _thermalShockBiosRecoveryTimer;
        private float _submarinePowerNormalized = 1f;
        private bool _hasSubmarinePowerSnapshot;
        private bool _hudPhosphorModeKeywordHeld;
        private TMP_FontAsset _activeTerminalBiosFont;
        private Material _activeTerminalBiosFontMaterial;
        private TMP_FontAsset _primaryHudFont;
        private Material _primaryHudFontMaterial;
        private TMP_FontAsset _queuedHudFont;
        private Material _queuedHudFontMaterial;
        private bool _biosFontModeApplied;
        private float _appliedVRBrownoutIntensity = -1f;
        private bool _hotSwapListenerRegistered;

        private uint _glitchRngState = 1u;

        private static readonly int ID_HUDTex = Shader.PropertyToID("_HUD_RenderTexture");
        private static readonly int ID_HUDIntensity = Shader.PropertyToID("_HUD_Intensity");
        private static readonly int ID_HUDColor = Shader.PropertyToID("_HUD_Color");
        private static readonly int ID_ScratchBleed = Shader.PropertyToID("_HUD_ScratchBleed");
        private static readonly int ID_Distortion = Shader.PropertyToID("_DistortionStrength");
        private static readonly int ID_WaterRunoffStrength = Shader.PropertyToID("_WaterRunoffStrength");
        private static readonly int ID_DropletAlpha = Shader.PropertyToID("_DropletAlpha");
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
        private static readonly int ID_ScalableRefractionScale = Shader.PropertyToID("_HectonVisorRefractionScale");
        private static readonly int ID_ScalableChromaticScale = Shader.PropertyToID("_HectonVisorChromaticScale");
        private static readonly int ID_QualityPressureDitherScale = Shader.PropertyToID("_HectonVisorQualityPressureDither");
        private static readonly int ID_HypoxiaLevel = Shader.PropertyToID("_HypoxiaLevel");
        private static readonly int ID_HullStressFlicker = Shader.PropertyToID("_HullStressFlicker");
        private static readonly int ID_HazardRadiationLevel = Shader.PropertyToID("_HazardRadiationLevel");
        private static readonly int ID_HazardThermalLevel = Shader.PropertyToID("_HazardThermalLevel");
        private static readonly int ID_HazardToxicLevel = Shader.PropertyToID("_HazardToxicLevel");
        private static readonly int ID_HazardGlitchLevel = Shader.PropertyToID("_HazardGlitchLevel");
        private static readonly int ID_BiosRecoveryMode = Shader.PropertyToID("_BiosRecoveryMode");
        private static readonly int ID_PressureLensCrackIntensity = Shader.PropertyToID("_PressureLensCrackIntensity");
        private static readonly int ID_ToolBatteryNormalized = Shader.PropertyToID("_ToolBatteryNormalized");
        private static readonly int ID_ToolHeat01 = Shader.PropertyToID("_ToolHeat01");
        private static readonly int ID_ToolAmmoUnits = Shader.PropertyToID("_ToolAmmoUnits");
        private static readonly int ID_ToolDistanceMeters = Shader.PropertyToID("_ToolDistanceMeters");
        private static readonly int ID_VisorCameraForwardWS = Shader.PropertyToID("_VisorCameraForwardWS");
        private static readonly int ID_VisorStrongestLightDirectionWS = Shader.PropertyToID("_VisorStrongestLightDirectionWS");
        private static readonly int ID_HectonVRBrownoutIntensity = Shader.PropertyToID("_HectonVRBrownoutIntensity");

        public Camera HudCamera => _hudCamera;
        public RenderTexture SharedRenderTexture => _sharedRenderTexture;
        internal static Texture ActiveHudRenderTexture { get; private set; }
        internal bool CanPresentProjection =>
            isActiveAndEnabled &&
            _hudCamera != null &&
            _hudCamera.isActiveAndEnabled &&
            _visorRenderer != null &&
            _visorRenderer.enabled &&
            !_visorRenderer.forceRenderingOff &&
            _visorRenderer.gameObject.activeInHierarchy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticRuntimeState()
        {
            ClearActiveControllerSlots();
            s_hudPhosphorModeUserCount = 0;
            Shader.DisableKeyword(HudPhosphorKeyword);
            Shader.SetGlobalFloat(ID_HectonVRBrownoutIntensity, 0f);
        }

        public static void CopyActiveControllersTo(List<VisorHUDController> results)
        {
            if (results == null)
                return;

            results.Clear();
            for (int i = 0; i < s_activeControllerCount; i++)
            {
                if (results.Count >= results.Capacity)
                    break;

                VisorHUDController controller = GetActiveControllerSlot(i);
                if (controller != null && controller.isActiveAndEnabled)
                    results.Add(controller);
            }
        }

        internal static int ActiveControllerCount => s_activeControllerCount;

        internal static VisorHUDController GetActiveController(int index)
        {
            return GetActiveControllerSlot(index);
        }

        public static void PulseActiveControllers(float intensity, int maxCount)
        {
            if (maxCount <= 0)
                return;

            int emitted = 0;
            for (int i = 0; i < s_activeControllerCount; i++)
            {
                VisorHUDController controller = GetActiveControllerSlot(i);
                if (controller == null || !controller.isActiveAndEnabled)
                    continue;

                controller.GlitchPulse(intensity);
                emitted++;
                if (emitted >= maxCount)
                    break;
            }
        }

        private static void ClearActiveControllerSlots()
        {
            s_activeController0 = null;
            s_activeController1 = null;
            s_activeController2 = null;
            s_activeController3 = null;
            s_activeController4 = null;
            s_activeController5 = null;
            s_activeController6 = null;
            s_activeController7 = null;
            s_activeControllerCount = 0;
        }

        private static VisorHUDController GetActiveControllerSlot(int index)
        {
            switch (index)
            {
                case 0:
                    return s_activeController0;
                case 1:
                    return s_activeController1;
                case 2:
                    return s_activeController2;
                case 3:
                    return s_activeController3;
                case 4:
                    return s_activeController4;
                case 5:
                    return s_activeController5;
                case 6:
                    return s_activeController6;
                case 7:
                    return s_activeController7;
                default:
                    return null;
            }
        }

        private static void SetActiveControllerSlot(int index, VisorHUDController controller)
        {
            switch (index)
            {
                case 0:
                    s_activeController0 = controller;
                    break;
                case 1:
                    s_activeController1 = controller;
                    break;
                case 2:
                    s_activeController2 = controller;
                    break;
                case 3:
                    s_activeController3 = controller;
                    break;
                case 4:
                    s_activeController4 = controller;
                    break;
                case 5:
                    s_activeController5 = controller;
                    break;
                case 6:
                    s_activeController6 = controller;
                    break;
                case 7:
                    s_activeController7 = controller;
                    break;
            }
        }

        private static void CompactActiveControllerSlots()
        {
            int writeIndex = 0;
            int readCount = s_activeControllerCount;

            for (int readIndex = 0; readIndex < readCount; readIndex++)
            {
                VisorHUDController controller = GetActiveControllerSlot(readIndex);
                if (controller == null)
                    continue;

                if (writeIndex != readIndex)
                    SetActiveControllerSlot(writeIndex, controller);

                writeIndex++;
            }

            for (int clearIndex = writeIndex; clearIndex < readCount; clearIndex++)
                SetActiveControllerSlot(clearIndex, null);

            s_activeControllerCount = writeIndex;
        }

        private void Awake()
        {
            CacheGraphicsCapabilitiesCold();
            CacheScreenSurfaceCold();
            EnsurePropertyBlock();
            RefreshBiosFontCachesSlow();
            EnsureHudScissorCommandBuffersCold();
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (EditorApplication.isCompiling || !Application.isPlaying)
            {
                SuspendEditModeProjection();
                UnregisterEditorTick();
                return;
            }
#endif

            RegisterActiveController();
            CacheGraphicsCapabilitiesCold();
            CacheScreenSurfaceCold();
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            EnsurePropertyBlock();
            RefreshBiosFontCachesSlow();
            EnsureHudScissorCommandBuffersCold();
            _materialPropertiesDirty = true;
            AutoResolveReferences(force: true);
            SyncProjectionPose();
            RebuildProjection();
            TryRegisterRuntimeTick();
            HectonSubmarineOsEvents.Register(this);
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnregisterEditorTick();
#endif
        }

        private void Start()
        {
            TryRegisterRuntimeTick();
        }

        private void OnDisable()
        {
            UnregisterActiveController();
            TryUnregisterHotSwapListener();

            if (_glitchActive)
            {
                _hudIntensity = _glitchOriginalIntensity;
                _glitchActive = false;
            }

            if (_waterRunoffIntensity > 0f || _waterRunoffHoldTimer > 0f || _dropletAlpha > 0f || _dropletFadeTimer > 0f)
            {
                _waterRunoffIntensity = 0f;
                _waterRunoffHoldTimer = 0f;
                _dropletAlpha = 0f;
                _dropletFadeTimer = 0f;
                _materialPropertiesDirty = true;
                ApplyMaterialProperties();
            }

            if (_condensationShockIntensity > 0f ||
                _condensationShockHoldTimer > 0f ||
                _criticalPressureCondensation > 0f ||
                _coldCondensation > 0f ||
                _screenFrostStrength > 0f ||
                _interferenceDistortionIntensity > 0f ||
                _interferenceDistortionHoldTimer > 0f)
            {
                _condensationShockIntensity = 0f;
                _condensationShockHoldTimer = 0f;
                _criticalPressureCondensationTarget = 0f;
                _criticalPressureCondensation = 0f;
                _coldCondensationTarget = 0f;
                _coldCondensation = 0f;
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
            _thermalShockBiosRecoveryTimer = 0f;
            _submarinePowerNormalized = 1f;
            _hasSubmarinePowerSnapshot = false;
            _appliedVRBrownoutIntensity = -1f;
            Shader.SetGlobalFloat(ID_HectonVRBrownoutIntensity, 0f);
            _biosFontSwapScheduler.Clear();
            _queuedHudFont = null;
            _queuedHudFontMaterial = null;
            _biosFontModeApplied = false;
            ReleaseHudPhosphorKeyword();
            HectonSubmarineOsEvents.Unregister(this);
            UnregisterRuntimeTick();
            ReleaseRT();
            InvalidatePoseCache();
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            HectonSubmarineOsEvents.Unregister(this);
            ReleaseHudPhosphorKeyword();
            DisposeHudScissorCommandBuffers();
            // Ensure RT is released on component destruction
            ReleaseRT();
        }

        public void OnSubmarineOsEvent(in SubmarineOsEventPayload payload)
        {
            if (!HectonSubmarineOsEvents.TryBuildSnapshot(in payload, out HectonSubmarineOsSnapshot snapshot))
                return;

            _submarinePowerNormalized = Mathf.Clamp01(snapshot.PowerNormalized);
            _hasSubmarinePowerSnapshot = true;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                ApplyCachedPlayerContext();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.ModularEquipment)
            {
                _cachedModularEquipment = currentService as IModularEquipmentService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Submarine)
            {
                _submarineRuntimeContext = currentService as ISubmarineRuntimeContext;
                ApplyCachedSubmarineContext();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.VRAMMonitorRuntime)
            {
                _cachedVramMonitor = currentService as IVramBudgetReadModel;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.RenderTexturePoolRuntime)
            {
                _cachedRenderTexturePool = currentService as IRenderTexturePoolService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.RenderTextureLifecycleRuntime)
            {
                _cachedRenderTextureLifecycle = currentService as IRenderTextureLifecycleService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                UnregisterRuntimeTick();
                if (currentService != null)
                    TryRegisterRuntimeTick();
            }
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

        private void CacheRegistryServicesCold()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
            _cachedModularEquipment = GlobalRegistry.ModularEquipment;
            _submarineRuntimeContext = GlobalRegistry.Submarine;
            _cachedVramMonitor = GlobalRegistry.VRAMBudgetReadModel;
            _cachedRenderTexturePool = GlobalRegistry.RenderTexturePoolService;
            _cachedRenderTextureLifecycle = GlobalRegistry.RenderTextureLifecycleService;
            ApplyCachedPlayerContext();
            ApplyCachedSubmarineContext();
        }

        private void ApplyCachedPlayerContext()
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext == null)
            {
                RefreshSurvivalSubscription(null);
                _survivalSystem = null;
                _traumaDispatcher = null;
                _playerHealth = null;
                return;
            }

            _survivalSystem = playerContext.SurvivalSystem;
            _traumaDispatcher = playerContext.TraumaDispatcher;
            _playerHealth = playerContext.PlayerHealth;
            RefreshSurvivalSubscription(_survivalSystem);
        }

        private void ApplyCachedSubmarineContext()
        {
            ISubmarineRuntimeContext submarineRuntimeContext = _submarineRuntimeContext;
            _hullBreachReadModel = submarineRuntimeContext != null ? submarineRuntimeContext.StructuralGrid : null;
        }

#if UNITY_EDITOR
        private void EditorTick()
        {
            if (EditorApplication.isCompiling)
            {
                SuspendEditModeProjection();
                UnregisterEditorTick();
                return;
            }

            if (!IsEditorPreviewActive())
            {
                SuspendEditModeProjection();
                return;
            }

            if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                return;

            if (_editorPreviewSuspended)
                ResumeEditModeProjection();

            if (!ShouldTickInEditMode())
            {
                UnregisterEditorTick();
                return;
            }

            RefreshRuntimeState(forceResolve: false);
            CacheEditorReferencePose();
        }
#endif

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (EditorApplication.isCompiling || !Application.isPlaying)
            {
                SuspendEditModeProjection();
                UnregisterEditorTick();
                return;
            }
#endif

            EnsurePropertyBlock();
            _materialPropertiesDirty = true;
            AutoResolveReferences(force: true);
            SyncProjectionPose();

            if (!isActiveAndEnabled)
                return;

            RebuildProjection();
        }

        private void AdvanceVisorHudPresentation(float deltaTime)
        {
            RefreshRuntimeReferenceCache();
            ConsumeSurvivalVitalsSignals();
            DrainBrownoutSignals();
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
            UpdatePressureFlickerState(deltaTime);
            UpdatePressureLensCrackState(deltaTime);
            RefreshDynamicVisorMaterialInputs();
        }

        public void LateFrameTick()
        {
            AdvanceVisorHudPresentation(SystemDispatcher.CurrentFrameUnscaledDeltaTime);
            UpdateBiosFontSwapState();
            DrainBiosFontSwapQueue();
            if (_materialPropertiesDirty)
                ApplyMaterialProperties();
        }

        public void SlowTick()
        {
            CacheGraphicsCapabilitiesCold();
            CacheScreenSurfaceCold();
            RefreshBiosFontCachesSlow();
            FlushHudScissorCommandBufferRepairSlow();
        }

        /// <summary>
        /// xorshift32 based zero-GC pseudo-random in [0, 1).
        /// </summary>
        private float XorShift01()
        {
            _glitchRngState ^= _glitchRngState << 13;
            _glitchRngState ^= _glitchRngState >> 17;
            _glitchRngState ^= _glitchRngState << 5;
            return (_glitchRngState & 0x7FFFFF) * InvGlitchMantissaScale;
        }

        private static float FastDecayBlend(float speed, float deltaTime)
        {
            float x = math.max(0.1f, speed) * math.max(0f, deltaTime);
            if (x >= 3.5f)
                return 1f;

            return math.saturate((12f * x) * math.rcp(12f + (6f * x) + (x * x)));
        }

        private static bool NearlyEqual(float lhs, float rhs)
        {
            return math.abs(lhs - rhs) <= VisorPropertyFloatWriteEpsilon;
        }

        private void TryRegisterRuntimeTick()
        {
            if (!Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_runtimeLateFrameRegistered)
                _runtimeLateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);

            if (!_runtimeSlowTickRegistered)
                _runtimeSlowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.UI);
        }

        private void UnregisterRuntimeTick()
        {
            if (_runtimeLateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _runtimeLateFrameRegistered = false;
            }

            if (_runtimeSlowTickRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
                _runtimeSlowTickRegistered = false;
            }
        }

        private void RegisterActiveController()
        {
            CompactActiveControllerSlots();

            for (int i = 0; i < s_activeControllerCount; i++)
            {
                if (ReferenceEquals(GetActiveControllerSlot(i), this))
                    return;
            }

            if (s_activeControllerCount >= ActiveControllerCapacity)
                return;

            SetActiveControllerSlot(s_activeControllerCount, this);
            s_activeControllerCount++;
        }

        private void UnregisterActiveController()
        {
            for (int i = 0; i < s_activeControllerCount; i++)
            {
                if (!ReferenceEquals(GetActiveControllerSlot(i), this))
                    continue;

                int lastIndex = s_activeControllerCount - 1;
                SetActiveControllerSlot(i, GetActiveControllerSlot(lastIndex));
                SetActiveControllerSlot(lastIndex, null);
                s_activeControllerCount = lastIndex;
                return;
            }
        }

        private void ApplyHudPhosphorKeyword(bool enabled)
        {
            if (_hudPhosphorModeKeywordHeld == enabled)
                return;

            _hudPhosphorModeKeywordHeld = enabled;
            if (enabled)
            {
                if (s_hudPhosphorModeUserCount == 0)
                    Shader.EnableKeyword(HudPhosphorKeyword);
                s_hudPhosphorModeUserCount++;
                return;
            }

            if (s_hudPhosphorModeUserCount > 0)
                s_hudPhosphorModeUserCount--;
            if (s_hudPhosphorModeUserCount == 0)
                Shader.DisableKeyword(HudPhosphorKeyword);
        }

        private void ReleaseHudPhosphorKeyword()
        {
            ApplyHudPhosphorKeyword(false);
        }

        private void AutoResolveReferences(bool force)
        {
            if (!force && !NeedsAutoResolve())
                return;

            float now = GetAutoResolveNow();
            if (!force && now < _nextAutoResolveAt)
                return;

            _nextAutoResolveAt = now + AutoResolveRetryInterval;
            bool allowHierarchySearch = force || !Application.isPlaying;

            if (_visorRenderer == null)
                TryGetComponent(out _visorRenderer);

            if (_hudCamera == null && allowHierarchySearch)
            {
                Transform parent = transform.parent;
                if (parent != null)
                {
                    Transform cameraTransform = parent.Find("HUD_Render_Camera");
                    if (cameraTransform != null)
                        cameraTransform.TryGetComponent(out _hudCamera);
                }
            }

            if (_baseStackCamera == null && allowHierarchySearch)
            {
                Transform parent = transform.parent;
                if (parent != null)
                {
                    Transform mainCameraTransform = parent.Find("Main Camera");
                    if (mainCameraTransform != null)
                    {
                        Transform spaceCameraTransform = mainCameraTransform.Find("SpaceCamera");
                        if (spaceCameraTransform != null)
                            spaceCameraTransform.TryGetComponent(out _baseStackCamera);
                    }
                }
            }

            if (_referenceCamera == null && allowHierarchySearch)
            {
                Transform parent = transform.parent;
                if (parent != null)
                {
                    Transform mainCameraTransform = parent.Find("Main Camera");
                    if (mainCameraTransform != null)
                        mainCameraTransform.TryGetComponent(out _referenceCamera);
                    else
                        parent.TryGetComponent(out _referenceCamera);
                }

                if (_referenceCamera == null && _baseStackCamera != null)
                {
                    Transform baseParent = _baseStackCamera.transform.parent;
                    if (baseParent != null)
                        baseParent.TryGetComponent(out _referenceCamera);
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
            bool needsPlayerHealth = Application.isPlaying && _playerHealth == null;
            bool needsHullBreachReadModel = Application.isPlaying && _hullBreachReadModel == null;

            return _visorRenderer == null
                || needsHudCamera
                || needsBaseStackCamera
                || needsReferenceCamera
                || needsSurvivalSystem
                || needsTraumaDispatcher
                || needsPlayerHealth
                || needsHullBreachReadModel;
        }

        private static float GetAutoResolveNow()
        {
            return Application.isPlaying
                ? (float)SystemDispatcher.CurrentUnscaledTimeSeconds
                : ResolveEditorPreviewClockSeconds();
        }

        private static float ResolveEditorPreviewClockSeconds()
        {
            return (float)(System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency);
        }

        private void EnsurePropertyBlock()
        {
            if (_mpb != null)
                return;

            // COLD ALLOC: MaterialPropertyBlock[1] — visor surface state bridge — owner: VisorHUDController
            _mpb = new MaterialPropertyBlock();
        }

        private void RefreshRuntimeState(bool forceResolve)
        {
            if (forceResolve)
                AutoResolveReferences(forceResolve);
            else
                RefreshRuntimeReferenceCache();
            SyncProjectionPose();
            ApplyMaterialProperties();
        }

        private void RefreshRuntimeReferenceCache()
        {
            if (_cachedPlayerContext != null)
                ApplyCachedPlayerContext();
            if (_submarineRuntimeContext != null)
                ApplyCachedSubmarineContext();
        }

        private void ApplyMaterialProperties()
        {
            if (_visorRenderer == null)
                return;

            EnsurePropertyBlock();
            RefreshDynamicVisorMaterialInputs();
            ApplyHudMotionVectorStabilization();
            ResolveScalabilityMatrixGlassState(
                out float scalableRefractionScale,
                out float scalableChromaticScale,
                out float qualityPressureDitherScale);

            float condensationStrength = Mathf.Clamp01(_condensationShockIntensity + _criticalPressureCondensation + _coldCondensation);
            float environmentalDistortion = _interferenceDistortionIntensity * _interferenceDistortionMax;
            float hazardChromaticAberration = (_hazardRadiationLevel * 0.010f) + (_hazardGlitchLevel * 0.006f);
            float hazardStaticNoise = (_hazardGlitchLevel * 0.28f) + (_hazardToxicLevel * 0.18f);
            float biosRecoverySwitch = _biosRecoveryModeBlend >= 0.5f ? 1f : 0f;
            ApplyHudPhosphorKeyword(biosRecoverySwitch > 0.5f || (_hasSubmarinePowerSnapshot && _submarinePowerNormalized < LowPowerBiosThreshold));
            ApplyVRBrownoutState(biosRecoverySwitch);
            float compositeHudIntensity = ResolveCompositeHudIntensity(biosRecoverySwitch);
            Color compositeHudTint = biosRecoverySwitch > 0.5f ? _biosRecoveryHudTint : _hudTint;
            float compositeChromaticAberration = biosRecoverySwitch > 0.5f
                ? 0f
                : Mathf.Max(_structuralFatigueChromaticAberration, hazardChromaticAberration);
            float compositeStaticNoise = biosRecoverySwitch > 0.5f
                ? hazardStaticNoise * 0.08f
                : Mathf.Max(_structuralFatigueStaticNoise, hazardStaticNoise);

            if (_appliedVisorPropertyRenderer != _visorRenderer)
                InvalidateVisorMaterialPropertyCache();

            _visorRenderer.GetPropertyBlock(_mpb);
            bool propertyBlockChanged = false;
            propertyBlockChanged |= ApplyVisorFloat(ID_HUDIntensity, compositeHudIntensity, ref _appliedHudIntensity);
            propertyBlockChanged |= ApplyVisorColor(ID_HUDColor, compositeHudTint, ref _appliedHudColor);
            propertyBlockChanged |= ApplyVisorFloat(ID_ScratchBleed, _scratchBleed, ref _appliedScratchBleed);
            propertyBlockChanged |= ApplyVisorFloat(ID_Distortion, _distortion + environmentalDistortion, ref _appliedDistortion);
            propertyBlockChanged |= ApplyVisorFloat(ID_WaterRunoffStrength, _waterRunoffIntensity, ref _appliedWaterRunoffStrength);
            propertyBlockChanged |= ApplyVisorFloat(ID_DropletAlpha, _dropletAlpha, ref _appliedDropletAlpha);
            propertyBlockChanged |= ApplyVisorFloat(ID_WaterRunoffSpeed, _waterRunoffSpeed, ref _appliedWaterRunoffSpeed);
            propertyBlockChanged |= ApplyVisorFloat(ID_WaterRunoffDistortion, _waterRunoffDistortion, ref _appliedWaterRunoffDistortion);
            propertyBlockChanged |= ApplyVisorFloat(ID_WaterDropletDensity, _waterDropletDensity, ref _appliedWaterDropletDensity);
            propertyBlockChanged |= ApplyVisorFloat(ID_WaterDropletScale, _waterDropletScale, ref _appliedWaterDropletScale);
            propertyBlockChanged |= ApplyVisorFloat(ID_CondensationStrength, condensationStrength, ref _appliedCondensationStrength);
            propertyBlockChanged |= ApplyVisorFloat(ID_CondensationDistortion, _condensationDistortion, ref _appliedCondensationDistortion);
            propertyBlockChanged |= ApplyVisorFloat(ID_CondensationEdgeExponent, _condensationEdgeExponent, ref _appliedCondensationEdgeExponent);
            propertyBlockChanged |= ApplyVisorFloat(ID_CondensationDriftSpeed, _condensationDriftSpeed, ref _appliedCondensationDriftSpeed);
            propertyBlockChanged |= ApplyVisorFloat(ID_ScreenFrostStrength, _screenFrostStrength, ref _appliedScreenFrostStrength);
            propertyBlockChanged |= ApplyVisorFloat(ID_ChromaticAberration, compositeChromaticAberration, ref _appliedChromaticAberration);
            propertyBlockChanged |= ApplyVisorFloat(ID_StaticNoise, compositeStaticNoise, ref _appliedStaticNoise);
            propertyBlockChanged |= ApplyVisorFloat(ID_ScalableRefractionScale, scalableRefractionScale, ref _appliedScalableRefractionScale);
            propertyBlockChanged |= ApplyVisorFloat(ID_ScalableChromaticScale, scalableChromaticScale, ref _appliedScalableChromaticScale);
            propertyBlockChanged |= ApplyVisorFloat(ID_QualityPressureDitherScale, qualityPressureDitherScale, ref _appliedQualityPressureDitherScale);
            propertyBlockChanged |= ApplyVisorFloat(ID_HypoxiaLevel, _hudHypoxiaLevel, ref _appliedHypoxiaLevel);
            propertyBlockChanged |= ApplyVisorFloat(ID_HullStressFlicker, _hudHullStressFlicker, ref _appliedHullStressFlicker);
            propertyBlockChanged |= ApplyVisorFloat(ID_HazardRadiationLevel, _hazardRadiationLevel, ref _appliedHazardRadiationLevel);
            propertyBlockChanged |= ApplyVisorFloat(ID_HazardThermalLevel, _hazardThermalLevel, ref _appliedHazardThermalLevel);
            propertyBlockChanged |= ApplyVisorFloat(ID_HazardToxicLevel, _hazardToxicLevel, ref _appliedHazardToxicLevel);
            propertyBlockChanged |= ApplyVisorFloat(ID_HazardGlitchLevel, _hazardGlitchLevel, ref _appliedHazardGlitchLevel);
            propertyBlockChanged |= ApplyVisorFloat(ID_BiosRecoveryMode, biosRecoverySwitch, ref _appliedBiosRecoveryMode);
            propertyBlockChanged |= ApplyVisorFloat(ID_PressureLensCrackIntensity, _pressureLensCrackIntensity, ref _appliedPressureLensCrackIntensity);
            propertyBlockChanged |= ApplyVisorFloat(ID_ToolBatteryNormalized, _resolvedToolBatteryNormalized, ref _appliedToolBatteryNormalized);
            propertyBlockChanged |= ApplyVisorFloat(ID_ToolHeat01, _resolvedToolHeatNormalized, ref _appliedToolHeatNormalized);
            propertyBlockChanged |= ApplyVisorFloat(ID_ToolAmmoUnits, _resolvedToolAmmoUnits, ref _appliedToolAmmoUnits);
            propertyBlockChanged |= ApplyVisorFloat(ID_ToolDistanceMeters, _resolvedToolDistanceMeters, ref _appliedToolDistanceMeters);
            propertyBlockChanged |= ApplyVisorVector(
                ID_VisorCameraForwardWS,
                MakeVector4(_resolvedVisorCameraForward.x, _resolvedVisorCameraForward.y, _resolvedVisorCameraForward.z, 1f),
                ref _appliedVisorCameraForward);
            propertyBlockChanged |= ApplyVisorVector(ID_VisorStrongestLightDirectionWS, _resolvedStrongestLightDirection, ref _appliedStrongestLightDirection);

            if (propertyBlockChanged)
            {
                _visorRenderer.SetPropertyBlock(_mpb);
                _appliedVisorPropertyRenderer = _visorRenderer;
                _hasAppliedVisorMaterialProperties = true;
            }

            _materialPropertiesDirty = false;
        }

        private float ResolveCompositeHudIntensity(float biosRecoverySwitch)
        {
            if (biosRecoverySwitch > 0.5f)
                return _biosRecoveryHudIntensity;

            return _projectionMode != ProjectionMode.Disabled
                ? Mathf.Max(_hudIntensity, MinimumProjectedHudIntensity)
                : _hudIntensity;
        }

        private void ApplyHudMotionVectorStabilization()
        {
            if (_visorRenderer == null)
                return;

            if (_appliedMotionVectorRenderer == _visorRenderer &&
                _visorRenderer.motionVectorGenerationMode == MotionVectorGenerationMode.ForceNoMotion)
            {
                return;
            }

            _visorRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            _appliedMotionVectorRenderer = _visorRenderer;
        }

        private void ResolveScalabilityMatrixGlassState(
            out float refractionScale,
            out float chromaticScale,
            out float qualityPressureDitherScale)
        {
            float qualityPressure01 = ResolveVisorQualityPressure01();
            if (math.abs(qualityPressure01 - _cachedQualityPressure01) > VisorPropertyFloatWriteEpsilon)
            {
                ResolveScalabilityMatrixGlassScalars(
                    qualityPressure01,
                    out _cachedScalableRefractionScale,
                    out _cachedScalableChromaticScale,
                    out _cachedQualityPressureDitherScale);
                _cachedQualityPressure01 = qualityPressure01;
            }

            refractionScale = _cachedScalableRefractionScale;
            chromaticScale = _cachedScalableChromaticScale;
            qualityPressureDitherScale = _cachedQualityPressureDitherScale;
        }

        private static void ResolveScalabilityMatrixGlassScalars(
            float qualityPressure01,
            out float refractionScale,
            out float chromaticScale,
            out float qualityPressureDitherScale)
        {
            float pressure = math.saturate(qualityPressure01);
            float visualBudget01 = 1f - pressure;
            float smoothVisualBudget01 = SmoothStep01(visualBudget01);
            refractionScale = smoothVisualBudget01;
            chromaticScale = SmoothStep01(math.saturate(visualBudget01 * 1.08f));
            qualityPressureDitherScale = SmoothStep01(pressure);
        }

        private float ResolveVisorQualityPressure01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            quality = math.isfinite(quality) ? math.saturate(quality) : 1f;
            float pressure = 1f - SmoothStep01(quality);
            return math.saturate(math.max(pressure, _memoryQualityPressureFloor01));
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _memoryQualityPressureFloor01 = ResolveMemoryQualityPressureFloor01Cold();
            _scriptableRenderPipelineActiveCold = SampleScriptableRenderPipelineActiveCold();
        }

        private static float ResolveMemoryQualityPressureFloor01Cold()
        {
            int graphicsMemoryMb = SystemInfo.graphicsMemorySize;
            if (graphicsMemoryMb <= 0)
                return 0.15f;

            float shortage01 = math.saturate((2048f - graphicsMemoryMb) * math.rcp(1536f));
            return 0.65f * SmoothStep01(shortage01);
        }

        private static float SmoothStep01(float value)
        {
            value = math.isfinite(value) ? math.saturate(value) : 0f;
            return value * value * (3f - (2f * value));
        }

        private bool ApplyVisorFloat(int propertyId, float value, ref float cachedValue)
        {
            if (_hasAppliedVisorMaterialProperties && math.abs(cachedValue - value) <= VisorPropertyFloatWriteEpsilon)
                return false;

            _mpb.SetFloat(propertyId, value);
            cachedValue = value;
            return true;
        }

        private bool ApplyVisorColor(int propertyId, Color value, ref Color cachedValue)
        {
            if (_hasAppliedVisorMaterialProperties && cachedValue == value)
                return false;

            _mpb.SetColor(propertyId, value);
            cachedValue = value;
            return true;
        }

        private bool ApplyVisorVector(int propertyId, Vector4 value, ref Vector4 cachedValue)
        {
            if (_hasAppliedVisorMaterialProperties &&
                math.abs(cachedValue.x - value.x) <= VisorPropertyFloatWriteEpsilon &&
                math.abs(cachedValue.y - value.y) <= VisorPropertyFloatWriteEpsilon &&
                math.abs(cachedValue.z - value.z) <= VisorPropertyFloatWriteEpsilon &&
                math.abs(cachedValue.w - value.w) <= VisorPropertyFloatWriteEpsilon)
            {
                return false;
            }

            _mpb.SetVector(propertyId, value);
            cachedValue = value;
            return true;
        }

        private void InvalidateVisorMaterialPropertyCache()
        {
            _appliedVisorPropertyRenderer = null;
            _hasAppliedVisorMaterialProperties = false;
        }

        private void RefreshDynamicVisorMaterialInputs()
        {
            ResolveActiveToolDisplayState(
                out float toolBatteryNormalized,
                out float toolHeatNormalized,
                out float toolAmmoUnits,
                out float toolDistanceMeters);
            Vector3 visorCameraForward = ResolveVisorCameraForward();
            Vector4 strongestLightDirection = ResolveStrongestLightDirectionPayload();

            if (!_hasResolvedDynamicVisorMaterialInputs)
            {
                _resolvedToolBatteryNormalized = toolBatteryNormalized;
                _resolvedToolHeatNormalized = toolHeatNormalized;
                _resolvedToolAmmoUnits = toolAmmoUnits;
                _resolvedToolDistanceMeters = toolDistanceMeters;
                _resolvedVisorCameraForward = visorCameraForward;
                _resolvedStrongestLightDirection = strongestLightDirection;
                _hasResolvedDynamicVisorMaterialInputs = true;
                _materialPropertiesDirty = true;
                return;
            }

            bool dirty = false;
            if (math.abs(_resolvedToolBatteryNormalized - toolBatteryNormalized) > VisorPropertyFloatWriteEpsilon)
            {
                _resolvedToolBatteryNormalized = toolBatteryNormalized;
                dirty = true;
            }

            if (math.abs(_resolvedToolHeatNormalized - toolHeatNormalized) > VisorPropertyFloatWriteEpsilon)
            {
                _resolvedToolHeatNormalized = toolHeatNormalized;
                dirty = true;
            }

            if (math.abs(_resolvedToolAmmoUnits - toolAmmoUnits) > VisorPropertyFloatWriteEpsilon)
            {
                _resolvedToolAmmoUnits = toolAmmoUnits;
                dirty = true;
            }

            if (math.abs(_resolvedToolDistanceMeters - toolDistanceMeters) > VisorPropertyFloatWriteEpsilon)
            {
                _resolvedToolDistanceMeters = toolDistanceMeters;
                dirty = true;
            }

            if (Vector3Changed(_resolvedVisorCameraForward, visorCameraForward))
            {
                _resolvedVisorCameraForward = visorCameraForward;
                dirty = true;
            }

            if (Vector4Changed(_resolvedStrongestLightDirection, strongestLightDirection))
            {
                _resolvedStrongestLightDirection = strongestLightDirection;
                dirty = true;
            }

            if (dirty)
                _materialPropertiesDirty = true;
        }

        private static bool Vector3Changed(Vector3 current, Vector3 next)
        {
            return math.abs(current.x - next.x) > VisorPropertyFloatWriteEpsilon ||
                math.abs(current.y - next.y) > VisorPropertyFloatWriteEpsilon ||
                math.abs(current.z - next.z) > VisorPropertyFloatWriteEpsilon;
        }

        private static bool Vector4Changed(Vector4 current, Vector4 next)
        {
            return math.abs(current.x - next.x) > VisorPropertyFloatWriteEpsilon ||
                math.abs(current.y - next.y) > VisorPropertyFloatWriteEpsilon ||
                math.abs(current.z - next.z) > VisorPropertyFloatWriteEpsilon ||
                math.abs(current.w - next.w) > VisorPropertyFloatWriteEpsilon;
        }

        private static Vector4 MakeVector4(float x, float y, float z, float w)
        {
            Vector4 result = default;
            result.x = x;
            result.y = y;
            result.z = z;
            result.w = w;
            return result;
        }

        private void ApplyVRBrownoutState(float biosRecoverySwitch)
        {
            float powerBrownout = 0f;
            if (_hasSubmarinePowerSnapshot)
            {
                float threshold = Mathf.Max(0.001f, LowPowerBiosThreshold);
                powerBrownout = Mathf.Clamp01((threshold - _submarinePowerNormalized) * math.rcp(threshold));
            }

            float brownoutIntensity = Mathf.Max(Mathf.Clamp01(biosRecoverySwitch), powerBrownout);
            if (NearlyEqual(_appliedVRBrownoutIntensity, brownoutIntensity))
                return;

            _appliedVRBrownoutIntensity = brownoutIntensity;
            Shader.SetGlobalFloat(ID_HectonVRBrownoutIntensity, brownoutIntensity);
        }

        private void DrainBrownoutSignals()
        {
            int drained = 0;
            while (drained < MaxBrownoutSignalsPerTick && SignalBus<BrownoutSignal>.TryConsumeFrame(out BrownoutSignal signal))
            {
                drained++;
                float supplyRatio = math.saturate(signal.SupplyRatio);
                if (signal.Severity01 > 0f)
                    supplyRatio = math.min(supplyRatio, math.saturate(1f - signal.Severity01));

                _submarinePowerNormalized = supplyRatio;
                _hasSubmarinePowerSnapshot = true;
            }
        }

        private Vector3 ResolveVisorCameraForward()
        {
            Camera camera = _referenceCamera != null ? _referenceCamera : _baseStackCamera;
            if (camera != null)
                return camera.transform.forward;

            return transform.forward;
        }

        private Vector4 ResolveStrongestLightDirectionPayload()
        {
            Light lightSource = _strongestSceneLightSource != null ? _strongestSceneLightSource : RenderSettings.sun;
            if (lightSource == null || !lightSource.isActiveAndEnabled)
                return Vector4.zero;

            Vector3 directionToLight = -lightSource.transform.forward;
            float intensity = Mathf.Max(0f, lightSource.intensity);
            return MakeVector4(directionToLight.x, directionToLight.y, directionToLight.z, intensity);
        }

        private void ResolveActiveToolDisplayState(
            out float battery01,
            out float heat01,
            out float ammoUnits,
            out float distanceMeters)
        {
            battery01 = 0f;
            heat01 = 0f;
            ammoUnits = 0f;
            distanceMeters = 0f;

            PlayerTool currentTool = ResolveActivePlayerTool();
            uint currentToolHash = currentTool != null ? currentTool.RuntimeToolId : 0u;
            if (SignalBus<ToolStateChangedSignal>.TryGetLatest(out ToolStateChangedSignal signal, out _) &&
                (signal.Flags & ToolStateChangedSignal.FlagEquipped) != 0 &&
                (currentToolHash == 0u || signal.ToolHash == currentToolHash))
            {
                battery01 = math.saturate(signal.Battery01);
                heat01 = math.saturate(signal.Heat01);
                ammoUnits = signal.AmmoUnits;
                distanceMeters = math.max(0f, signal.DistanceMeters);
                return;
            }

            if (currentTool == null)
                return;

            IModularEquipmentService equipment = _cachedModularEquipment;
            if (equipment != null &&
                currentTool.RuntimeToolId != 0u &&
                equipment.TryGetToolState(currentTool.RuntimeToolId, out ToolState state))
            {
                float capacity = 1f;
                if (equipment.TryGetToolStats(currentTool.RuntimeToolId, out ToolRuntimeStats stats))
                {
                    capacity = math.max(0.1f, stats.BatteryCapacity);
                    distanceMeters = math.max(0f, stats.MaxRange);
                }

                battery01 = math.saturate(state.CurrentBattery * math.rcp(capacity));
                heat01 = math.saturate(state.InternalHeat);
                ammoUnits = math.clamp((int)math.round(battery01 * 100f), 0, 999);
                return;
            }

            battery01 = ResolveActiveToolBatteryNormalized();
            ammoUnits = math.clamp((int)math.round(battery01 * 100f), 0, 999);
        }

        private PlayerTool ResolveActivePlayerTool()
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            PlayerToolManager toolManager = playerContext != null ? playerContext.ToolManager : null;
            return toolManager != null ? toolManager.CurrentTool : null;
        }

        private float ResolveActiveToolBatteryNormalized()
        {
            PlayerTool currentTool = ResolveActivePlayerTool();

            if (!(currentTool is IBatteryTool batteryTool) || !batteryTool.HasBattery)
                return 0f;

            return Mathf.Clamp01(batteryTool.BatteryCharge);
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
            if (_dropletFadeTimer > 0f)
            {
                float lifetime = Mathf.Max(0.1f, _surfaceBreakRunoffMinimumLifetime);
                _dropletFadeTimer = Mathf.Max(0f, _dropletFadeTimer - Mathf.Max(0f, deltaTime));
                float nextDropletAlpha = Mathf.Clamp01(_dropletFadeTimer * math.rcp(lifetime));
                if (!NearlyEqual(nextDropletAlpha, _dropletAlpha))
                {
                    _dropletAlpha = nextDropletAlpha;
                    _materialPropertiesDirty = true;
                }
            }
            else if (_dropletAlpha != 0f)
            {
                _dropletAlpha = 0f;
                _materialPropertiesDirty = true;
            }

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

            float t = FastDecayBlend(_waterRunoffRecoverySpeed, deltaTime);
            float nextIntensity = math.lerp(_waterRunoffIntensity, 0f, t);
            if (!NearlyEqual(nextIntensity, _waterRunoffIntensity))
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
            HectonPlayerHealth resolvedPlayerHealth = _playerHealth;
            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform currentPlayerTransform) &&
                currentPlayerTransform != null)
            {
                if (resolvedSystem == null || resolvedSystem.transform != currentPlayerTransform)
                    currentPlayerTransform.TryGetComponent(out resolvedSystem);

                if (resolvedTraumaDispatcher == null || resolvedTraumaDispatcher.transform != currentPlayerTransform)
                    currentPlayerTransform.TryGetComponent(out resolvedTraumaDispatcher);

                if (resolvedPlayerHealth == null || resolvedPlayerHealth.transform != currentPlayerTransform)
                    currentPlayerTransform.TryGetComponent(out resolvedPlayerHealth);
            }

            if (_survivalSystem != resolvedSystem)
                _survivalSystem = resolvedSystem;

            if (_traumaDispatcher != resolvedTraumaDispatcher)
                _traumaDispatcher = resolvedTraumaDispatcher;

            if (_playerHealth != resolvedPlayerHealth)
                _playerHealth = resolvedPlayerHealth;

            RefreshSurvivalSubscription(_survivalSystem);
        }

        private void ResolveStructuralGridReference()
        {
            if (!Application.isPlaying)
                return;

            ISubmarineRuntimeContext submarineRuntimeContext = _submarineRuntimeContext;
            _hullBreachReadModel = submarineRuntimeContext != null ? submarineRuntimeContext.StructuralGrid : null;
        }

        private void RefreshSurvivalSubscription(HectonSurvivalSystem target)
        {
            if (_subscribedSurvivalSystem == target)
                return;

            _subscribedSurvivalSystem = target;
            _survivalVitalsSourceId = ResolveSurvivalVitalsSourceId(_subscribedSurvivalSystem);
            _lastSurvivalVitalsSignalSequence = 0u;

            if (_subscribedSurvivalSystem == null)
                return;

            HandleTemperatureChanged(_subscribedSurvivalSystem.EnvironmentTemperature);
            HandlePressureChanged(_subscribedSurvivalSystem.Pressure);
        }

        private void ConsumeSurvivalVitalsSignals()
        {
            HectonSurvivalSystem system = _subscribedSurvivalSystem;
            uint sourceId = _survivalVitalsSourceId;
            if (system == null || sourceId == 0u)
                return;

            ReadOnlySpan<SurvivalVitalsChangedSignal> signals = SignalBus<SurvivalVitalsChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ref readonly SurvivalVitalsChangedSignal signal = ref signals[i];
                if (signal.SourceId != sourceId)
                    continue;

                if (signal.Sequence == 0u || signal.Sequence == _lastSurvivalVitalsSignalSequence)
                    continue;

                uint flags = signal.Flags;
                if ((flags & (SurvivalVitalsChangedSignalFlags.Temperature | SurvivalVitalsChangedSignalFlags.Pressure)) == 0u)
                    continue;

                _lastSurvivalVitalsSignalSequence = signal.Sequence;
                if ((flags & SurvivalVitalsChangedSignalFlags.Temperature) != 0u)
                    HandleTemperatureChanged(system.EnvironmentTemperature);
                if ((flags & SurvivalVitalsChangedSignalFlags.Pressure) != 0u)
                    HandlePressureChanged(system.Pressure);
            }
        }

        private static uint ResolveSurvivalVitalsSourceId(HectonSurvivalSystem system)
        {
            return system != null
                ? RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(system.GetEntityId()))
                : 0u;
        }

        private void HandleTemperatureChanged(float temperature)
        {
            if (_hasTemperatureSample)
            {
                float delta = Mathf.Abs(temperature - _lastTemperatureSample);
                if (delta >= _temperatureShockThreshold)
                    TriggerCondensationShock(delta * math.rcp(Mathf.Max(0.01f, _temperatureShockThreshold)));

                if (ShouldTriggerThermalShockBiosRecovery(
                    temperature,
                    _lastTemperatureSample,
                    _thermalShockBiosRecoveryTemperature,
                    _thermalShockBiosRecoverySpike))
                {
                    TriggerThermalShockBiosRecovery();
                }
            }

            _lastTemperatureSample = temperature;
            _hasTemperatureSample = true;
        }

        internal static bool ShouldTriggerThermalShockBiosRecovery(
            float temperature,
            float previousTemperature,
            float thresholdTemperature,
            float spikeThreshold)
        {
            float safeThreshold = Mathf.Max(0f, thresholdTemperature);
            if (temperature < safeThreshold)
                return false;

            if (previousTemperature < safeThreshold)
                return true;

            float positiveSpike = temperature - previousTemperature;
            return positiveSpike >= Mathf.Max(0f, spikeThreshold);
        }

        private void TriggerThermalShockBiosRecovery()
        {
            float holdSeconds = Mathf.Max(0f, _thermalShockBiosRecoveryHoldSeconds);
            if (_thermalShockBiosRecoveryTimer < holdSeconds)
                _thermalShockBiosRecoveryTimer = holdSeconds;

            _materialPropertiesDirty = true;
        }

        private void HandlePressureChanged(float pressure)
        {
            if (_hasPressureSample)
            {
                float delta = Mathf.Abs(pressure - _lastPressureSample);
                if (delta >= _pressureShockThreshold)
                    TriggerCondensationShock(delta * math.rcp(Mathf.Max(0.01f, _pressureShockThreshold)));
            }

            _lastPressureSample = pressure;
            _hasPressureSample = true;

            float target = 0f;
            if (_subscribedSurvivalSystem != null && _subscribedSurvivalSystem.Stats != null)
            {
                float invSafeDepth = math.rcp(math.max(0.01f, _subscribedSurvivalSystem.Stats.SafeDepth));
                float pressureFactor = pressure * invSafeDepth;
                float pressureT = FastInverseLerp01(
                    _criticalPressureStartFactor,
                    Mathf.Max(_criticalPressureStartFactor + 0.01f, _criticalPressureFullFactor),
                    pressureFactor);
                target = pressureT * _criticalPressureCondensationMax;
            }

            if (!NearlyEqual(_criticalPressureCondensationTarget, target))
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
            float pressureBlendT = FastDecayBlend(_criticalPressureCondensationBlendSpeed, deltaTime);
            float blendedPressureCondensation = math.lerp(
                _criticalPressureCondensation,
                _criticalPressureCondensationTarget,
                pressureBlendT);
            if (!NearlyEqual(blendedPressureCondensation, _criticalPressureCondensation))
            {
                _criticalPressureCondensation = blendedPressureCondensation;
                _materialPropertiesDirty = true;
            }

            float targetColdCondensation = 0f;
            if (_subscribedSurvivalSystem != null)
            {
                float coldCondensation01 = ResolveColdCondensation01(_subscribedSurvivalSystem.EnvironmentTemperature);
                targetColdCondensation = coldCondensation01 * Mathf.Clamp01(_coldCondensationMaximum);
            }

            if (!NearlyEqual(targetColdCondensation, _coldCondensationTarget))
                _coldCondensationTarget = targetColdCondensation;

            float blendedColdCondensation = math.lerp(
                _coldCondensation,
                _coldCondensationTarget,
                pressureBlendT);
            if (!NearlyEqual(blendedColdCondensation, _coldCondensation))
            {
                _coldCondensation = blendedColdCondensation;
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

            float t = FastDecayBlend(_condensationShockRecoverySpeed, deltaTime);
            float nextIntensity = math.lerp(_condensationShockIntensity, 0f, t);
            if (!NearlyEqual(nextIntensity, _condensationShockIntensity))
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
                float coldCondensation01 = ResolveColdCondensation01(temperature);
                float temperatureT = FastInverseLerp01(_frostStartTemperature, _frostFullTemperature, temperature);
                float coldSeverity = Mathf.Clamp01(_subscribedSurvivalSystem.ColdStressSeverity01);
                target = Mathf.Max(temperatureT, coldSeverity * (0.62f + _abyssalColdFrostBoost));
                target = Mathf.Max(target, coldCondensation01 * Mathf.Clamp01(_coldCondensationMaximum + 0.12f));
                target *= _screenFrostMaximum;
            }

            float hypothermiaFrost = UIStateStore.ReadValueOrDefault(UIValueSlotId.FrostIntensity01, 0f);
            if (hypothermiaFrost > 0f)
                target = Mathf.Max(target, Mathf.Clamp01(hypothermiaFrost) * _screenFrostMaximum);

            if (!NearlyEqual(target, _screenFrostTarget))
                _screenFrostTarget = target;

            float blendT = FastDecayBlend(_screenFrostBlendSpeed, deltaTime);
            float blendedFrost = math.lerp(_screenFrostStrength, _screenFrostTarget, blendT);
            if (!NearlyEqual(blendedFrost, _screenFrostStrength))
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

            float t = FastDecayBlend(_interferenceDistortionRecoverySpeed, deltaTime);
            float nextIntensity = math.lerp(_interferenceDistortionIntensity, 0f, t);
            if (!NearlyEqual(nextIntensity, _interferenceDistortionIntensity))
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
            float blendT = FastDecayBlend(_structuralFatigueBlendSharpness, deltaTime);

            float nextChromaticAberration = math.lerp(_structuralFatigueChromaticAberration, targetChromaticAberration, blendT);
            if (!NearlyEqual(nextChromaticAberration, _structuralFatigueChromaticAberration))
            {
                _structuralFatigueChromaticAberration = nextChromaticAberration;
                _materialPropertiesDirty = true;
            }

            float nextStaticNoise = math.lerp(_structuralFatigueStaticNoise, targetStaticNoise, blendT);
            if (!NearlyEqual(nextStaticNoise, _structuralFatigueStaticNoise))
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
                float clarityRemaining01 = 1f - Mathf.Clamp01(_traumaDispatcher.ClarityChannel01);
                targetBiosRecovery = clarityRemaining01 < BiosRecoveryClarityThreshold ? 1f : 0f;
            }

            if (_playerHealth != null)
            {
                targetRadiation = Mathf.Max(targetRadiation, Mathf.Clamp01(_playerHealth.RadiationExposure));
                if (_playerHealth.RadiationExposureSeconds >= RadiationFatigueCriticalExposureSeconds)
                {
                    targetRadiation = 1f;
                    targetBiosRecovery = 1f;
                }
            }

            if (_thermalShockBiosRecoveryTimer > 0f)
            {
                _thermalShockBiosRecoveryTimer = Mathf.Max(0f, _thermalShockBiosRecoveryTimer - Mathf.Max(0f, deltaTime));
                targetThermal = 1f;
                targetBiosRecovery = 1f;
            }

            if (_hasSubmarinePowerSnapshot && _submarinePowerNormalized < LowPowerBiosThreshold)
                targetBiosRecovery = 1f;

            float targetGlitch = Mathf.Clamp01(Mathf.Max(
                targetRadiation,
                Mathf.Max(targetThermal * 0.82f, targetToxic * 0.91f)));
            float blendT = FastDecayBlend(_structuralFatigueBlendSharpness, deltaTime);

            if (UpdateSmoothedVisualChannel(ref _hazardRadiationLevel, targetRadiation, blendT))
                _materialPropertiesDirty = true;

            if (UpdateSmoothedVisualChannel(ref _hazardThermalLevel, targetThermal, blendT))
                _materialPropertiesDirty = true;

            if (UpdateSmoothedVisualChannel(ref _hazardToxicLevel, targetToxic, blendT))
                _materialPropertiesDirty = true;

            if (UpdateSmoothedVisualChannel(ref _hazardGlitchLevel, targetGlitch, blendT))
                _materialPropertiesDirty = true;

            if (!NearlyEqual(_biosRecoveryModeBlend, targetBiosRecovery))
            {
                _biosRecoveryModeBlend = targetBiosRecovery;
                _materialPropertiesDirty = true;
            }
        }

        private void UpdateHypoxiaState(float deltaTime)
        {
            float targetHypoxia = 0f;
            if (_subscribedSurvivalSystem != null)
            {
                float oxygenNormalized = Mathf.Clamp01(_subscribedSurvivalSystem.OxygenNormalized);
                float safeThreshold = Mathf.Clamp(_hypoxiaStartThreshold, 0.01f, 1f);
                float nitrogenBlur01 = Mathf.Clamp01(_subscribedSurvivalSystem.NitrogenNarcosisVisionBlur01);
                targetHypoxia = ResolveHypoxiaNarcosisTarget(oxygenNormalized, safeThreshold, nitrogenBlur01);
            }

            float blendT = FastDecayBlend(_hypoxiaBlendSharpness, deltaTime);
            float nextHypoxia = math.lerp(_hudHypoxiaLevel, targetHypoxia, blendT);
            if (!NearlyEqual(nextHypoxia, _hudHypoxiaLevel))
            {
                _hudHypoxiaLevel = nextHypoxia;
                _materialPropertiesDirty = true;
            }
        }

        internal static float ResolveHypoxiaNarcosisTarget(float oxygenNormalized, float safeThreshold, float nitrogenVisionBlur01)
        {
            float safe = Mathf.Clamp(safeThreshold, 0.01f, 1f);
            float hypoxia = oxygenNormalized < safe
                ? 1f - Mathf.Clamp01(oxygenNormalized * math.rcp(safe))
                : 0f;
            return Mathf.Max(hypoxia, Mathf.Clamp01(nitrogenVisionBlur01));
        }

        private void UpdatePressureFlickerState(float deltaTime)
        {
            float targetFlicker = ResolveHullStress01() * Mathf.Clamp01(_pressureFlickerMaximum);
            float blendT = FastDecayBlend(_pressureFlickerBlendSharpness, deltaTime);
            float nextFlicker = math.lerp(_hudHullStressFlicker, targetFlicker, blendT);
            if (!NearlyEqual(nextFlicker, _hudHullStressFlicker))
            {
                _hudHullStressFlicker = nextFlicker;
                _materialPropertiesDirty = true;
            }
        }

        private void UpdatePressureLensCrackState(float deltaTime)
        {
            float depth = ResolvePlayerDepthMeters();
            float startDepth = Mathf.Max(0f, _pressureLensCrackStartDepthMeters);
            float invRange = math.rcp(math.max(1f, _pressureLensCrackFullDepthRangeMeters));
            float targetCrack = Mathf.Clamp01((depth - startDepth) * invRange);
            targetCrack = targetCrack * targetCrack * (3f - 2f * targetCrack);
            float blendT = FastDecayBlend(_pressureLensCrackBlendSharpness, deltaTime);
            if (UpdateSmoothedVisualChannel(ref _pressureLensCrackIntensity, targetCrack, blendT))
                _materialPropertiesDirty = true;
        }

        private float ResolvePlayerDepthMeters()
        {
            if (_subscribedSurvivalSystem != null)
                return Mathf.Max(0f, _subscribedSurvivalSystem.Depth);

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            return playerMovement != null ? Mathf.Max(0f, playerMovement.CurrentDepth) : 0f;
        }

        private float ResolveHullStress01()
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            return playerMovement != null ? Mathf.Clamp01(playerMovement.CurrentHullStress01) : 0f;
        }

        private float ResolveStructuralFatigue01()
        {
            ISubmarineHullBreachReadModel hullBreachReadModel = _hullBreachReadModel;
            UnityEngine.Object readModelObject = hullBreachReadModel as UnityEngine.Object;
            if (readModelObject != null && hullBreachReadModel.IsReady)
                return Mathf.Clamp01(hullBreachReadModel.FatiguePeakNormalized);

            return 0f;
        }

        private static bool UpdateSmoothedVisualChannel(ref float current, float target, float blendT)
        {
            float nextValue = math.lerp(current, target, blendT);
            if (NearlyEqual(nextValue, current))
                return false;

            current = nextValue;
            return true;
        }

        private static float FastInverseLerp01(float from, float to, float value)
        {
            float range = to - from;
            if (math.abs(range) <= 0.00001f)
                return 0f;

            return math.saturate((value - from) * math.rcp(range));
        }

        private float ResolveColdCondensation01(float temperature)
        {
            float fullTemperature = Mathf.Min(_coldCondensationStartTemperature - 0.01f, _coldCondensationFullTemperature);
            return FastInverseLerp01(_coldCondensationStartTemperature, fullTemperature, temperature);
        }

        private void PrewarmBiosTerminalFont()
        {
            if (!_enableBiosFontSwap)
                return;

            TMP_FontAsset activeFont = TMP_TextRegistry.PrewarmTerminalFont(_terminalBiosFont);
            if (activeFont == _activeTerminalBiosFont && _activeTerminalBiosFontMaterial != null)
                return;

            _activeTerminalBiosFont = activeFont;
            _activeTerminalBiosFontMaterial = ResolveFontMaterialCold(activeFont);
        }

        private void RefreshBiosFontCachesSlow()
        {
            if (!_enableBiosFontSwap)
                return;

            if (!LocalizedFontResolver.IsFontReady(_activeTerminalBiosFont) ||
                _activeTerminalBiosFontMaterial == null)
                PrewarmBiosTerminalFont();

            if (!LocalizedFontResolver.IsFontReady(_primaryHudFont) ||
                _primaryHudFontMaterial == null)
                ResolvePrimaryHudFont();
        }

        private void UpdateBiosFontSwapState()
        {
            if (!_enableBiosFontSwap)
                return;

            bool biosActive = _biosRecoveryModeBlend >= 0.5f;
            if (biosActive == _biosFontModeApplied)
                return;

            TMP_FontAsset targetFont = biosActive ? _activeTerminalBiosFont : _primaryHudFont;
            if (!LocalizedFontResolver.IsFontReady(targetFont))
                return;

            Material targetMaterial = biosActive ? _activeTerminalBiosFontMaterial : _primaryHudFontMaterial;
            QueueBiosFontSwap(targetFont, targetMaterial);
            _biosFontModeApplied = biosActive;
        }

        private TMP_FontAsset ResolvePrimaryHudFont()
        {
            if (LocalizedFontResolver.IsFontReady(_primaryHudFont))
            {
                if (_primaryHudFontMaterial == null)
                    _primaryHudFontMaterial = ResolveFontMaterialCold(_primaryHudFont);

                return _primaryHudFont;
            }

            int registeredCount = TMP_TextRegistry.Count;
            for (int i = 0; i < registeredCount; i++)
            {
                TMP_TextEntry entry = TMP_TextRegistry.GetEntryAt(i);
                if (entry.Layer != LocLayer.Core || entry.IsUserInput)
                    continue;

                TMP_Text text = entry.Text;
                if (text == null || !LocalizedFontResolver.IsFontReady(text.font))
                    continue;

                return CachePrimaryHudFont(text.font);
            }

            return CachePrimaryHudFont(TMP_Settings.defaultFontAsset);
        }

        private TMP_FontAsset CachePrimaryHudFont(TMP_FontAsset font)
        {
            _primaryHudFont = font;
            _primaryHudFontMaterial = ResolveFontMaterialCold(font);
            return _primaryHudFont;
        }

        private static Material ResolveFontMaterialCold(TMP_FontAsset font)
        {
            return font != null ? font.material : null;
        }

        private void QueueBiosFontSwap(TMP_FontAsset targetFont, Material targetMaterial)
        {
            _biosFontSwapScheduler.Clear();
            _queuedHudFont = targetFont;
            _queuedHudFontMaterial = targetMaterial;

            int registeredCount = TMP_TextRegistry.Count;
            for (int i = 0; i < registeredCount; i++)
            {
                TMP_TextEntry entry = TMP_TextRegistry.GetEntryAt(i);
                if (entry.Layer != LocLayer.Core || entry.IsUserInput)
                    continue;

                TMP_Text text = entry.Text;
                if (text == null || text.font == targetFont)
                    continue;

                if (!_biosFontSwapScheduler.Enqueue(entry))
                    break;
            }
        }

        private void DrainBiosFontSwapQueue()
        {
            if (_queuedHudFont == null || !_biosFontSwapScheduler.HasPending)
                return;

            _biosFontSwapScheduler.DrainTick(_queuedHudFont, _queuedHudFontMaterial);
            if (!_biosFontSwapScheduler.HasPending)
            {
                _queuedHudFont = null;
                _queuedHudFontMaterial = null;
            }
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
            _hudRT = _cachedRenderTexturePool.Rent(targetWidth, targetHeight, RenderTextureFormat.ARGB32, this);
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
                NearlyEqual(_cachedEffectiveRenderScale, effectiveRenderScale))
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

            IVramBudgetReadModel vramMonitor = _cachedVramMonitor;
            if (vramMonitor != null)
            {
                switch (vramMonitor.PressureStateCode)
                {
                    case VramPressureStateCodes.Critical:
                        effectiveScale *= _adaptiveVRAMCriticalScale;
                        break;

                    case VramPressureStateCodes.Warning:
                        effectiveScale *= _adaptiveVRAMWarningScale;
                        break;
                }
            }

            effectiveScale = Mathf.Clamp(effectiveScale, _adaptiveRuntimeRTMinScale, 1f);
            return QuantizeAdaptiveScale(effectiveScale);
        }

        private void CalculateTargetRTDimensions(float effectiveRenderScale, out int targetWidth, out int targetHeight)
        {
            int baseWidth = _matchScreenResolution ? _cachedScreenWidth : _rtWidth;
            int baseHeight = _matchScreenResolution ? _cachedScreenHeight : _rtHeight;
            float clampedScale = Mathf.Clamp(effectiveRenderScale, 0.1f, 1f);
            targetWidth = Mathf.Max(32, Mathf.RoundToInt(baseWidth * clampedScale));
            targetHeight = Mathf.Max(32, Mathf.RoundToInt(baseHeight * clampedScale));
            targetWidth = Mathf.Min(RuntimeHudCompositeMaxWidth, targetWidth);
            targetHeight = Mathf.Min(RuntimeHudCompositeMaxHeight, targetHeight);
        }

        private void CacheScreenSurfaceCold()
        {
            _cachedScreenWidth = Mathf.Max(1, Screen.width);
            _cachedScreenHeight = Mathf.Max(1, Screen.height);
        }

        private float QuantizeAdaptiveScale(float scale)
        {
            float quantizationStep = Mathf.Max(0.01f, _adaptiveScaleQuantizationStep);
            return Mathf.Round(scale * math.rcp(quantizationStep)) * quantizationStep;
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
            if (_hudCamera != null)
            {
                SetHudCameraTargetTextureIfChanged(_projectionMode == ProjectionMode.Disabled ? null : _hudRT);
                ConfigureHudScissorCommandBuffers();
            }

            Texture hudTexture = _hudRT != null ? (Texture)_hudRT : Texture2D.blackTexture;
            bool hasVisorRenderer = _visorRenderer != null;
            ApplyHudTextureBinding(hudTexture, bindRenderer: hasVisorRenderer);

            if (hasVisorRenderer)
                _materialPropertiesDirty = true;
        }

        private void ReleaseRT()
        {
            ReleaseHudScissorCommandBuffers();

            if (_hudCamera != null)
            {
                SetHudCameraTargetTextureIfChanged(null);
                SetHudCameraEnabledIfChanged(true);
            }

            ReleaseOwnedRuntimeTexture();

            _hudRT = null;
            _ownsRuntimeTexture = false;
            ApplyHudTextureBinding(Texture2D.blackTexture, bindRenderer: _visorRenderer != null);
            _cachedRTWidth = -1;
            _cachedRTHeight = -1;
            _cachedEffectiveRenderScale = -1f;
        }

        private void ApplyHudTextureBinding(Texture texture, bool bindRenderer)
        {
            Texture safeTexture = texture != null ? texture : Texture2D.blackTexture;

            if (bindRenderer && _visorRenderer != null)
            {
                ApplyRendererHudTextureBinding(safeTexture);
            }
            else
            {
                _appliedHudTextureRenderer = null;
                _appliedHudTexture = null;
            }

            if (_appliedGlobalHudTexture != safeTexture)
            {
                Shader.SetGlobalTexture(ID_HUDTex, safeTexture);
                _appliedGlobalHudTexture = safeTexture;
            }

            ActiveHudRenderTexture = safeTexture;
        }

        private void SetHudCameraTargetTextureIfChanged(RenderTexture targetTexture)
        {
            if (_hudCamera != null && _hudCamera.targetTexture != targetTexture)
                _hudCamera.targetTexture = targetTexture;
        }

        private void SetHudCameraEnabledIfChanged(bool enabled)
        {
            if (_hudCamera != null && _hudCamera.enabled != enabled)
                _hudCamera.enabled = enabled;
        }

        private void SetHudCameraClearFlagsIfChanged(CameraClearFlags clearFlags)
        {
            if (_hudCamera != null && _hudCamera.clearFlags != clearFlags)
                _hudCamera.clearFlags = clearFlags;
        }

        private void SetHudCameraBackgroundColorIfChanged(Color color)
        {
            if (_hudCamera != null && _hudCamera.backgroundColor != color)
                _hudCamera.backgroundColor = color;
        }

        private void ApplyRendererHudTextureBinding(Texture texture)
        {
            if (_appliedHudTextureRenderer == _visorRenderer && _appliedHudTexture == texture)
                return;

            EnsurePropertyBlock();
            _visorRenderer.GetPropertyBlock(_mpb);
            _mpb.SetTexture(ID_HUDTex, texture);
            _visorRenderer.SetPropertyBlock(_mpb);
            _appliedHudTextureRenderer = _visorRenderer;
            _appliedHudTexture = texture;
        }

        private void ConfigureHudScissorCommandBuffers()
        {
            ClearHudScissorCommandBufferState();
            _hudScissorCommandBufferRepairRequested = false;
        }

        private void EnsureHudScissorCommandBuffersCold()
        {
            ClearHudScissorCommandBufferState();
            _hudScissorCommandBufferRepairRequested = false;
        }

        private void FlushHudScissorCommandBufferRepairSlow()
        {
            if (!_hudScissorCommandBufferRepairRequested)
                return;

            EnsureHudScissorCommandBuffersCold();
        }

        private static bool SampleScriptableRenderPipelineActiveCold()
        {
            return GraphicsSettings.currentRenderPipeline != null || GraphicsSettings.defaultRenderPipeline != null;
        }

        private void ClearHudScissorCommandBufferState()
        {
            // Legacy camera command-buffer scissor path is intentionally retired; RenderGraph owns visor clipping.
        }

        private void ReleaseHudScissorCommandBuffers()
        {
            ClearHudScissorCommandBufferState();
        }

        private void DisposeHudScissorCommandBuffers()
        {
            ReleaseHudScissorCommandBuffers();
        }

        private void ReleaseOwnedRuntimeTexture()
        {
            if (!_ownsRuntimeTexture || _hudRT == null)
                return;

            // Register disposal with lifecycle tracker
            IRenderTextureLifecycleService lifecycle = _cachedRenderTextureLifecycle;
            if (lifecycle != null)
                lifecycle.RegisterDisposal(_hudRT);

            // Return to pool for reuse (zero-GC)
            IRenderTexturePoolService pool = _cachedRenderTexturePool;
            if (pool != null)
                pool.Return(_hudRT);
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
                SetHudCameraTargetTextureIfChanged(null);
                SetHudCameraEnabledIfChanged(false);
            }

            ReleaseOwnedRuntimeTexture();
            _hudRT = null;
            _ownsRuntimeTexture = false;
            _cachedRTWidth = -1;
            _cachedRTHeight = -1;
            _cachedEffectiveRenderScale = -1f;

            ApplyHudTextureBinding(Texture2D.blackTexture, bindRenderer: _visorRenderer != null);

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

                SetHudCameraClearFlagsIfChanged(CameraClearFlags.SolidColor);
                SetHudCameraBackgroundColorIfChanged(Color.clear);
                SetHudCameraEnabledIfChanged(true);
                return;
            }

            // Overlay fallback renders through the screen-space HUD canvas, so the HUD camera
            // must not stay in any URP stack. Leaving it stacked reintroduces a broken runtime
            // path on renderers that report stacking inconsistently.
            RemoveHudCameraFromKnownStacks(stackBaseCamera, baseCameraData);
            hudCameraData.renderType = CameraRenderType.Base;
            SetHudCameraClearFlagsIfChanged(CameraClearFlags.Depth);
            SetHudCameraEnabledIfChanged(false);
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
            if (!NearlyEqual(_hudCamera.depth, fallbackDepth))
                _hudCamera.depth = fallbackDepth;

            SetHudCameraClearFlagsIfChanged(CameraClearFlags.Depth);
            SetHudCameraEnabledIfChanged(true);
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

            Camera resolvedCamera = TryResolveBaseStackCameraFromHierarchy(!Application.isPlaying);
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

        private Camera TryResolveBaseStackCameraFromHierarchy(bool allowHierarchySearch)
        {
            if (!allowHierarchySearch)
                return null;

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
            if (spaceCameraTransform == null)
                return null;

            spaceCameraTransform.TryGetComponent(out Camera camera);
            return camera;
        }

        private static Camera TryResolveBaseStackCameraFromTransform(Transform sourceTransform)
        {
            if (sourceTransform == null)
                return null;

            Transform spaceCameraTransform = sourceTransform.Find("SpaceCamera");
            if (spaceCameraTransform != null)
            {
                spaceCameraTransform.TryGetComponent(out Camera directCamera);
                if (directCamera != null)
                    return directCamera;
            }

            Transform parent = sourceTransform.parent;
            if (parent == null)
                return null;

            Transform siblingSpaceCameraTransform = parent.Find("SpaceCamera");
            if (siblingSpaceCameraTransform == null)
                return null;

            siblingSpaceCameraTransform.TryGetComponent(out Camera camera);
            return camera;
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
                _hudCamera.TryGetComponent(out _cachedHudCameraData);

            return _cachedHudCameraData;
        }

        private UniversalAdditionalCameraData GetCachedBaseCameraData()
        {
            if (_baseStackCamera == null)
                return null;

            if (_cachedBaseCameraData == null || _cachedBaseCameraData.gameObject != _baseStackCamera.gameObject)
                _baseStackCamera.TryGetComponent(out _cachedBaseCameraData);

            return _cachedBaseCameraData;
        }

        private static UniversalAdditionalCameraData GetCameraData(Camera camera)
        {
            if (camera == null)
                return null;

            camera.TryGetComponent(out UniversalAdditionalCameraData cameraData);
            return cameraData;
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
            _editorReferencePoseCached = false;
            _editorLastReferenceCamera = null;
            _editorLastReferencePosition = default;
            _editorLastReferenceRotation = default;
            _editorLastReferenceNearClip = 0f;
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
            if (NearlyEqual(_hudIntensity, clampedIntensity))
                return;

            _hudIntensity = clampedIntensity;
            _materialPropertiesDirty = true;
        }

        public void SetProjectionMode(ProjectionMode projectionMode)
        {
            if (_projectionMode == projectionMode)
            {
                if (!IsProjectionBindingCurrent())
                    RebuildProjection();
                return;
            }

            _projectionMode = projectionMode;
            RebuildProjection();
        }

        public void SetSharedRenderTexture(RenderTexture sharedRenderTexture)
        {
            if (_sharedRenderTexture == sharedRenderTexture)
            {
                if (_projectionMode == ProjectionMode.SharedRenderTexture && !IsProjectionBindingCurrent())
                    RebuildProjection();
                return;
            }

            _sharedRenderTexture = sharedRenderTexture;
            InvalidatePoseCache();
            if (_projectionMode == ProjectionMode.SharedRenderTexture)
                RebuildProjection();
        }

        private bool IsProjectionBindingCurrent()
        {
            if (_projectionMode == ProjectionMode.Disabled)
                return _hudCamera == null || _hudCamera.targetTexture == null;

            if (_hudRT == null)
                return false;

            return _hudCamera == null || _hudCamera.targetTexture == _hudRT;
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
            _glitchRngState = (uint)((float)SystemDispatcher.CurrentUnscaledTimeSeconds * 1000f) | 1u;
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
            _dropletAlpha = 1f;
            _dropletFadeTimer = Mathf.Max(0.1f, desiredLifetime);
            float remainingRecoveryWindow = desiredLifetime - holdDuration;
            if (remainingRecoveryWindow > 0.05f)
            {
                // Exponential decay reaches ~1% after ~4.6 / speed seconds.
                float maximumRecoverySpeed = 4.6f * math.rcp(remainingRecoveryWindow);
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

            if (HasEditorReferencePoseChanged())
                return true;

            return NeedsAutoResolve();
        }

        private bool HasEditorReferencePoseChanged()
        {
            if (!_syncToReferenceCamera || !_syncPoseInEditMode)
                return false;

            if (_referenceCamera == null)
                return false;

            Transform referenceTransform = _referenceCamera.transform;
            Vector3 referencePosition = referenceTransform.position;
            Quaternion referenceRotation = referenceTransform.rotation;
            float nearClipPlane = _referenceCamera.nearClipPlane;

            if (!_editorReferencePoseCached)
                return true;

            return !ReferenceEquals(_editorLastReferenceCamera, _referenceCamera)
                || _editorLastReferencePosition != referencePosition
                || _editorLastReferenceRotation != referenceRotation
                || !NearlyEqual(_editorLastReferenceNearClip, nearClipPlane);
        }

        private void CacheEditorReferencePose()
        {
            if (!_syncToReferenceCamera || !_syncPoseInEditMode || _referenceCamera == null)
            {
                _editorReferencePoseCached = false;
                _editorLastReferenceCamera = null;
                _editorLastReferencePosition = default;
                _editorLastReferenceRotation = default;
                _editorLastReferenceNearClip = 0f;
                return;
            }

            Transform referenceTransform = _referenceCamera.transform;
            _editorLastReferenceCamera = _referenceCamera;
            _editorLastReferencePosition = referenceTransform.position;
            _editorLastReferenceRotation = referenceTransform.rotation;
            _editorLastReferenceNearClip = _referenceCamera.nearClipPlane;
            _editorReferencePoseCached = true;
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
