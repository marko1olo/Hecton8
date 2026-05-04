// ══════════════════════════════════════════════════════════════════
// HectonAtmosphereManager.cs  v2.1 (OPTIMIZATION PASS)
// Орбитальная модель солнца + время суток + затмения + _SunDirection
//
// ═══════════════════════════════════════════════════════════════
// v2.1 CHANGES (OPTIMIZATION):
// ═══════════════════════════════════════════════════════════════
//
//   [OPT] Shader property dirty-write batching in RotateSun()
//     • Caches _cachedShaderSunDirection (float3, stack)
//     • Only calls Shader.SetGlobalVector() if changed
//     • Impact: eliminates redundant GPU command buffer writes
//
//   [OPT] Dictionary<int, AtmosphereProfile> for biome lookup
//     • HandleBiomeChanged() from O(N) linear search → O(1) TryGetValue
//     • Built once in Awake() from _biomeOverrides[]
//     • Impact: O(N) one-time cost, O(1) per biome change
//
// ═══════════════════════════════════════════════════════════════
// v4.3 BASELINE (PRESERVED):
// ═══════════════════════════════════════════════════════════════
//
//   [FIX] DefaultExecutionOrder(-6000):
//     Guarantees AtmosphereManager.OnEnable() fires BEFORE
//     UnderwaterVisuals(-4000) and CelestialEngine(-3000).
//     This means AtmosphereManager registers with GameTickManager FIRST
//     → ticks FIRST → ProfileSunIntensity and ComputedHorizonFade
//     are FRESH when UnderwaterVisuals reads them.
//
//     EXECUTION CHAIN (deterministic):
//       1. AtmosphereManager.Tick()  → compute profile, horizon, _SunDirection
//       2. UnderwaterVisuals.Tick()  → write sunLight.intensity (sole authority)
//       3. CelestialEngine.Tick()    → multiply sunLight.intensity by visibility
//
// ═══════════════════════════════════════════════════════════════
// v4.2 DETAILS:
//   ✓ [ExecuteAlways] for Scene View preview
//   ✓ sunLight.intensity NEVER WRITTEN (read-only)
//   ✓ ProfileSunIntensity = profile × transition
//   ✓ ComputedHorizonFade = smoothstep by SunElevation
//   ✓ Biome atmosphere overrides
//   ✓ Eclipse timer + state machine
// ══════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Hecton8.Celestial;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.World;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Atmosphere
{
    /// <summary>
    /// Single write facade for global atmosphere render settings owned by runtime visual systems.
    /// </summary>
    public static class AtmosphereDirector
    {
        public static Material Skybox => RenderSettings.skybox;

        public static bool IsSkybox(Material material)
        {
            return ReferenceEquals(RenderSettings.skybox, material);
        }

        public static bool SetSkybox(Material material)
        {
            if (ReferenceEquals(RenderSettings.skybox, material))
                return false;

            RenderSettings.skybox = material;
            return true;
        }
    }

    /// <summary>
    /// Main-thread listener for deferred atmosphere state changes.
    /// </summary>
    public interface IAtmosphereStateEventListener
    {
        /// <summary>Called when the atmosphere state changes.</summary>
        void OnAtmosphereStateChanged(EnvironmentState state);
    }

    /// <summary>
    /// Queue-backed atmosphere state event lane.
    /// </summary>
    public static class AtmosphereEvents
    {
        private const int ExpectedPendingStateEventCapacity = 8;
        private const int ListenerCapacity = 8;

        private static readonly RegistryBucket<IAtmosphereStateEventListener> _listeners = new RegistryBucket<IAtmosphereStateEventListener>(ListenerCapacity);
        private static NativeQueue<EnvironmentState> _pendingStates;
        private static NativeQueue<EnvironmentState> _nextFrameStates;
        private static int _pendingStateCount;
        private static int _nextFrameStateCount;
        private static bool _isDispatching;

        /// <summary>
        /// Number of queued atmosphere state changes awaiting dispatch.
        /// </summary>
        public static int PendingCount => _pendingStateCount + _nextFrameStateCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingStates.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(AtmosphereEvents), nameof(_pendingStates));
                _pendingStates.Dispose();
                _pendingStates = default;
            }

            if (_nextFrameStates.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(AtmosphereEvents), nameof(_nextFrameStates));
                _nextFrameStates.Dispose();
                _nextFrameStates = default;
            }

            _pendingStateCount = 0;
            _nextFrameStateCount = 0;
            _isDispatching = false;
            _listeners.Clear();
        }

        /// <summary>
        /// Registers a main-thread atmosphere listener.
        /// </summary>
        public static void Register(IAtmosphereStateEventListener listener)
        {
            if (listener != null && !_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        /// <summary>
        /// Unregisters a main-thread atmosphere listener.
        /// </summary>
        public static void Unregister(IAtmosphereStateEventListener listener)
        {
            if (listener != null && _listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        /// <summary>
        /// Queues a new atmosphere state change.
        /// </summary>
        public static void RaiseStateChanged(EnvironmentState state)
        {
            EnsureInitialized();
            if (_pendingStateCount + _nextFrameStateCount >= ExpectedPendingStateEventCapacity)
                return;

            if (_isDispatching)
            {
                _nextFrameStates.Enqueue(state);
                _nextFrameStateCount++;
                return;
            }

            _pendingStates.Enqueue(state);
            _pendingStateCount++;
        }

        /// <summary>
        /// Flushes queued atmosphere state changes on the main thread.
        /// </summary>
        public static void FlushPending()
        {
            if (!_pendingStates.IsCreated)
                return;

            PromoteNextFrameStatesIfFrontEmpty();
            int scanBudget = _pendingStateCount > 0 ? _pendingStateCount : ExpectedPendingStateEventCapacity;
            while (scanBudget > 0 && !_pendingStates.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingStates.TryDequeue(out EnvironmentState state))
                    return;

                if (_pendingStateCount > 0)
                    _pendingStateCount--;
                scanBudget--;
                IAtmosphereStateEventListener[] rawListeners = _listeners.RawArray;
                int listenerCount = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = listenerCount - 1; i >= 0; i--)
                    {
                        IAtmosphereStateEventListener listener = rawListeners[i];
                        if (listener != null)
                            listener.OnAtmosphereStateChanged(state);
                    }
                }
                finally
                {
                    _isDispatching = false;
                }
            }

            if (_pendingStates.IsEmpty())
            {
                _pendingStateCount = 0;
                PromoteNextFrameStatesIfFrontEmpty();
            }
        }

        private static void EnsureInitialized()
        {
            if (!_pendingStates.IsCreated)
            {
                _pendingStates = new NativeQueue<EnvironmentState>(Allocator.Persistent); // COLD ALLOC: NativeQueue<EnvironmentState>[8] - deferred atmosphere state lane flushed by SystemDispatcher - owner: AtmosphereEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingStates,
                    ExpectedPendingStateEventCapacity,
                    nameof(AtmosphereEvents),
                    nameof(_pendingStates),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameStates.IsCreated)
            {
                _nextFrameStates = new NativeQueue<EnvironmentState>(Allocator.Persistent); // COLD ALLOC: NativeQueue<EnvironmentState>[8] - next-frame atmosphere state lane prevents same-frame reentrant dispatch - owner: AtmosphereEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameStates,
                    ExpectedPendingStateEventCapacity,
                    nameof(AtmosphereEvents),
                    nameof(_nextFrameStates),
                    NativeAllocationLifetime.Session);
            }
        }

        private static void PromoteNextFrameStatesIfFrontEmpty()
        {
            if (!_pendingStates.IsCreated ||
                !_nextFrameStates.IsCreated ||
                !_pendingStates.IsEmpty() ||
                _nextFrameStateCount <= 0)
            {
                return;
            }

            NativeQueue<EnvironmentState> swap = _pendingStates;
            _pendingStates = _nextFrameStates;
            _nextFrameStates = swap;
            _pendingStateCount = _nextFrameStateCount;
            _nextFrameStateCount = 0;
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Atmosphere Manager")]
    [DefaultExecutionOrder(-6000)]  // v4.3: MUST tick before UnderwaterVisuals(-4000)
    [ExecuteAlways]
    public class HectonAtmosphereManager : MonoBehaviour, ITickable, IUpdatable, IBiomeMatrixEventListener, IMapMagicBiomeEventListener
    {
        private const float VisualEnterUnderwaterDepth = 0.01f;
        private const float VisualExitUnderwaterDepth = 0.005f;

        #region ══════════ AtmosphereSnapshot ══════════

        private struct AtmosphereSnapshot
        {
            public Color  fogColor;
            public float  fogDensity;
            public float  fogAttenuationDistance;
            public float  skyExposure;
            public Color  ambientColor;
            public float  sunIntensity;
            public float  temperature;
            public float  radiation;

            public static AtmosphereSnapshot Default => new AtmosphereSnapshot
            {
                fogColor      = new Color(0.75f, 0.78f, 0.85f, 1f),
                fogDensity    = 0.008f,
                fogAttenuationDistance = 100f,
                skyExposure  = 1f,
                ambientColor = new Color(0.45f, 0.45f, 0.55f, 1f),
                sunIntensity = 1f,
                temperature  = 20f,
                radiation    = 0f
            };

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static AtmosphereSnapshot FromProfile(AtmosphereProfile p)
            {
                return new AtmosphereSnapshot
                {
                    fogColor      = p.fogColor,
                    fogDensity    = p.fogDensity,
                    fogAttenuationDistance = math.max(0.001f, p.fogAttenuationDistanceMeters),
                    skyExposure  = p.skyExposure,
                    ambientColor = p.ambientColor,
                    sunIntensity = p.sunIntensity,
                    temperature  = p.temperature,
                    radiation    = p.radiation
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static AtmosphereSnapshot Lerp(
                in AtmosphereSnapshot from,
                in AtmosphereSnapshot to,
                float t)
            {
                return new AtmosphereSnapshot
                {
                    fogColor      = Color.Lerp(from.fogColor, to.fogColor, t),
                    fogDensity    = math.lerp(from.fogDensity, to.fogDensity, t),
                    fogAttenuationDistance = math.lerp(from.fogAttenuationDistance, to.fogAttenuationDistance, t),
                    skyExposure  = math.lerp(from.skyExposure,  to.skyExposure,  t),
                    ambientColor = Color.Lerp(from.ambientColor, to.ambientColor, t),
                    sunIntensity = math.lerp(from.sunIntensity, to.sunIntensity, t),
                    temperature  = math.lerp(from.temperature,  to.temperature,  t),
                    radiation    = math.lerp(from.radiation,    to.radiation,    t)
                };
            }
        }

        #endregion

        #region ══════════ Singleton ══════════

        private static HectonAtmosphereManager _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        public static HectonAtmosphereManager Instance
        {
            get { return _instance; }
        }

        #endregion

        #region ══════════ Events ══════════

        #endregion

        #region ══════════ Shader IDs ══════════

        private static readonly int _shaderID_SunDirection =
            Shader.PropertyToID("_SunDirection");
        private static readonly int _shaderID_HectonTimeOfDay01 =
            Shader.PropertyToID("_HectonTimeOfDay01");
        private static readonly int _shaderID_HectonNightFactor =
            Shader.PropertyToID("_HectonNightFactor");
        private static readonly int _shaderID_SargassumBiolumPhaseMultiplier =
            Shader.PropertyToID("_SargassumBiolumPhaseMultiplier");
        private static readonly int _shaderID_FinalGiantAbyssLight =
            Shader.PropertyToID("_FinalGiantAbyssLight");
        private static readonly int _shaderID_AegirDirection =
            Shader.PropertyToID("_AegirDirection");

        #endregion

        #region ══════════ Inspector ══════════

        [Header("═══ Sun & Time Cycle ═══")]
        [Tooltip("Directional sun controlled by the atmosphere cycle. Falls back to RenderSettings.sun during play-mode validation.")]
        [SerializeField] private Light _sunLight;

        [SerializeField, Min(1f)]
        private float _cycleDuration = 3600f;

        [SerializeField, Range(0f, 1f)]
        private float _initialTimeOfDay = 0.25f;

        [SerializeField, Range(0f, 360f)]
        private float _sunOrbitalYAngle = 170f;

        [SerializeField, Range(0f, 90f)]
        private float _orbitalInclination = 23.5f;

        [SerializeField, Range(1f, 30f)]
        private float _nightThresholdAngle = 10f;

        [Tooltip("Angular zone below horizon for smooth sun intensity fade.\n" +
                 "At dot ∈ [0, -sin(fadeAngle)] intensity smoothly → 0.")]
        [SerializeField, Range(1f, 30f)]
        private float _sunHorizonFadeAngle = 10f;

        [Header("═══ Atmosphere Profiles ═══")]
        [SerializeField] private AtmosphereProfile _profileDay;
        [SerializeField] private AtmosphereProfile _profileNight;
        [SerializeField] private AtmosphereProfile _profileUnderwater;
        [SerializeField] private AtmosphereProfile _profileEclipse;

        [Header("═══ Transition ═══")]
        [SerializeField, Range(0.1f, 5f)]
        private float _transitionSpeed = 1.5f;

        [Header("═══ Underwater Detection ═══")]
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private float _waterSurfaceY = 0f;
        [SerializeField] private bool _useAutoUnderwaterDetection = true;

        [Header("Giant Abyss Light")]
        [Tooltip("Linearized surface-color source used before depth absorption attenuates Aegir's planet-shine in water.")]
        [SerializeField, ColorUsage(false, true)] private Color _giantAbyssSurfaceLightColor = new Color(0.48f, 0.72f, 1f, 1f);
        [Tooltip("RGB absorption coefficients per meter for exp(-depthMeters * sigmaRgbPerMeter).")]
        [SerializeField] private Vector3 _giantAbyssSigmaRgbPerMeter = new Vector3(0.0035f, 0.0012f, 0.00055f);
        [Tooltip("Scalar applied to Aegir's phase-lit planet-shine before it is published to shaders.")]
        [SerializeField, Range(0f, 2f)] private float _giantAbyssLightIntensity = 0.35f;
        [Tooltip("Abyssal biolume tint added as a low-strength fill so deep water never collapses to pure black.")]
        [SerializeField, ColorUsage(false, true)] private Color _giantAbyssBiolumeColor = new Color(0f, 0.38f, 0.55f, 1f);
        [Tooltip("Maximum biolume fill added to the final giant abyss light at depth.")]
        [SerializeField, Range(0f, 0.25f)] private float _giantAbyssBiolumeIntensity = 0.055f;

        [Header("═══ Vertical Runtime ═══")]
        [SerializeField] private BiomeMatrixDirector _biomeMatrixDirector;
        [SerializeField] private WorldProceduralFieldSampler _proceduralFieldSampler;
        [SerializeField, Min(0.05f)] private float _biomeInfluenceRefreshInterval = 0.35f;

        [Header("═══ Biome Overrides ═══")]
        [SerializeField] private BiomeAtmosphereOverride[] _biomeOverrides;

        #endregion

        #region ══════════ Runtime State ══════════

        private EnvironmentState _currentState = EnvironmentState.SURFACE_DAY;

        private float _cycleTimer;
        private double _elapsedCycleTimeSeconds;
        private float _sunAngleDegrees;
        private float _sunElevationDot;

        private bool  _eclipseActive;
        private float _eclipseRemainingTime;

        private bool _underwaterExternalFlag;
        private bool _autoUnderwaterState;
        private HectonPlayerMovement _playerMovement;
        private Transform _playerCameraTransform;

        private AtmosphereSnapshot _transitionOrigin;
        private AtmosphereSnapshot _currentValues;
        private float              _transitionProgress;

        private bool _registeredToTickManager;
        private bool _registeredAtmosphereRuntime;

        private AtmosphereProfile _activeBiomeProfile;
        private AtmosphereProfile _activeMatrixProfile;
        private WorldProceduralFieldSampler.BiomeInfluenceCell _currentBiomeInfluence;
        private HectonBiomeMatrixProfile _biomeInfluencePrimaryProfile;
        private HectonBiomeMatrixProfile _biomeInfluenceSecondaryProfile;
        private float _nextBiomeInfluenceRefreshTime = float.NegativeInfinity;
        private bool _hasBiomeInfluenceAtmosphere;
        private int _currentBiomeID = -1;
        private bool _editorInitialized;
        private bool _editorPreviewDirty;

        private float _computedHorizonFade;
        private float _computedSunIntensity;

        /// <summary>Cached shader property values (for dirty-write batching).</summary>
        private float3 _cachedShaderSunDirection = new float3(0f, -1f, 0f);
        private float _cachedShaderTimeOfDay01 = -1f;
        private float _cachedShaderNightFactor = -1f;
        private float _cachedShaderSargassumBiolumPhaseMultiplier = float.NaN;
        private float4 _cachedFinalGiantAbyssLight = new float4(-1f, -1f, -1f, -1f);
        private float4 _cachedAegirDirection = new float4(0f, 0f, 0f, -999f);

        /// <summary>Dictionary for O(1) biome profile lookup (instead of linear search).</summary>
        private Dictionary<int, AtmosphereProfile> _biomeProfileDict;

        #endregion

        #region ══════════ Biome Override Struct ══════════

        [Serializable]
        public struct BiomeAtmosphereOverride
        {
            public int biomeID;
            public AtmosphereProfile profile;
        }

        #endregion

        #region ══════════ Public Properties ══════════

        public EnvironmentState CurrentState   => _currentState;
        public float TimeOfDay                 => _cycleTimer / _cycleDuration;
        public float SunAngle                  => _sunAngleDegrees;
        public float SunElevation              => _sunElevationDot;
        public double ElapsedCycleTimeSeconds  => _elapsedCycleTimeSeconds;
        public Color CurrentFogColor           => _currentValues.fogColor;
        public float CurrentFogDensity         => _currentValues.fogDensity;
        public float CurrentFogAttenuationDistance => _currentValues.fogAttenuationDistance;
        public float CurrentSkyExposure        => _currentValues.skyExposure;
        public Color CurrentAmbientColor       => _currentValues.ambientColor;
        public bool  IsEclipseActive           => _eclipseActive;
        public float EclipseRemainingTime      => _eclipseRemainingTime;
        public float CycleDuration             => _cycleDuration;
        public float OrbitalInclination        => _orbitalInclination;

        /// <summary>
        /// v4.3 CLARIFICATION:
        /// Raw profile sun intensity AFTER transition interpolation.
        /// This is the "what the sun WANTS to be" value.
        /// UnderwaterVisuals multiplies this by horizon × depth.
        /// CelestialEngine then multiplies by eclipse visibility.
        ///
        /// NEVER written to sunLight.intensity by this script.
        /// </summary>
        public float ProfileSunIntensity => _currentValues.sunIntensity;

        /// <summary>
        /// v4.3 CLARIFICATION:
        /// Horizon fade factor [0..1].
        /// Computed from sun elevation angle via smoothstep.
        /// 0 = sun fully below horizon, 1 = sun fully above.
        ///
        /// Read by UnderwaterVisuals to compute:
        ///   sunLight.intensity = ProfileSunIntensity × ComputedHorizonFade × depthFactor
        /// </summary>
        public float ComputedHorizonFade => _computedHorizonFade;

        public float CurrentSunIntensity => _computedSunIntensity;

        public float CurrentTemperature  => _currentValues.temperature;
        public float CurrentRadiation    => _currentValues.radiation;
        public float SeaLevelY           => ResolveSeaLevelY();

        #endregion

        #region ══════════ Lifecycle ══════════

        private void Awake()
        {
            if (Application.isPlaying)
            {
                if (_instance != null && _instance != this)
                {
                    Destroy(this);
                    return;
                }
                _instance = this;
            }
            else
            {
                _instance = this;
            }

            _registeredToTickManager = false;

            // Build biome profile dictionary (ONE-TIME, O(n) initialization)
            _biomeProfileDict = new Dictionary<int, AtmosphereProfile>(16);
            if (_biomeOverrides != null)
            {
                for (int i = 0; i < _biomeOverrides.Length; i++)
                {
                    _biomeProfileDict[_biomeOverrides[i].biomeID] = _biomeOverrides[i].profile;
                }
            }

            InitializeCycleTimer();
            InitializeAtmosphereValues();
            CachePlayerMovement();
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (EditorApplication.isCompiling || !Application.isPlaying)
                return;
#endif

            if (Application.isPlaying)
            {
                TryRegisterService();
                TryRegister();

                MapMagicBiomeEvents.Register(this);
                BiomeMatrixEvents.Register(this);
                ResolveBiomeMatrixDirector();
                ApplyCurrentMatrixAtmosphereOverride();
            }
#if UNITY_EDITOR
            else
            {
                _editorPreviewDirty = true;
                EditorApplication.update -= EditorTick;
                EditorApplication.update += EditorTick;
            }
#endif
        }

        private void Start()
        {
            if (!Application.isPlaying) return;

            if (!_registeredToTickManager)
            {
                TryRegister();
                if (!_registeredToTickManager)
                {
                    Debug.LogError(
                        "[HectonAtmosphere] SystemDispatcher registration failed in Start(). " +
                        "Atmosphere will NOT update.", this);
                }
            }

            if (_biomeMatrixDirector == null)
                ResolveBiomeMatrixDirector();

            ApplyCurrentMatrixAtmosphereOverride();
        }

        private void OnDisable()
        {
            _autoUnderwaterState = false;

            if (Application.isPlaying)
            {
                TryUnregister();
                TryUnregisterService();

                MapMagicBiomeEvents.Unregister(this);
                BiomeMatrixEvents.Unregister(this);
            }
#if UNITY_EDITOR
            else
            {
                EditorApplication.update -= EditorTick;
            }
#endif

            if (_instance == this)
                ResetCycleShaderGlobals();
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                MapMagicBiomeEvents.Unregister(this);
                BiomeMatrixEvents.Unregister(this);
                TryUnregister();
                TryUnregisterService();
            }

            if (_instance != this) return;
            _instance = null;

            ResetCycleShaderGlobals();

        }

        private void TryRegister()
        {
            if (_registeredToTickManager || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registeredToTickManager = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registeredToTickManager = false;
        }

        private void TryRegisterService()
        {
            if (_registeredAtmosphereRuntime || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterAtmosphereRuntime(this);
            _registeredAtmosphereRuntime = ReferenceEquals(GlobalRegistry.Atmosphere, this);
        }

        private void TryUnregisterService()
        {
            if (!_registeredAtmosphereRuntime)
                return;

            if (ReferenceEquals(GlobalRegistry.Atmosphere, this))
                GlobalRegistry.UnregisterAtmosphereRuntime(this);

            _registeredAtmosphereRuntime = false;
        }

#if UNITY_EDITOR
        private void EditorTick()
        {
            if (EditorApplication.isCompiling || !Application.isPlaying)
            {
                EditorApplication.update -= EditorTick;
                return;
            }

            if (Application.isPlaying || this == null)
                return;

            if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                return;

            if (!_editorInitialized)
            {
                InitializeCycleTimer();
                InitializeAtmosphereValues();
                _editorInitialized = true;
                _editorPreviewDirty = true;
            }

            bool sunMoved = SyncEditorPreviewFromSunTransform();
            if (!_editorPreviewDirty && !sunMoved)
                return;

            Tick(0f);
            _editorPreviewDirty = false;
        }
#endif

#if UNITY_EDITOR
        /// <summary>
        /// Synchronizes edit-mode atmosphere state from the live directional-light rotation.
        /// </summary>
        /// <remarks>
        /// Scene View time-of-day authoring must treat the sun transform as the source of truth
        /// while the editor is not playing; otherwise preview visuals drift away from runtime.
        /// </remarks>
        public bool SyncEditorPreviewFromSunTransform()
        {
            if (Application.isPlaying || _sunLight == null)
                return false;

            Transform sunTransform = _sunLight.transform;
            if (sunTransform == null || !sunTransform.hasChanged)
                return false;

            _editorInitialized = true;
            SyncCycleFromEditorSunTransform();
            sunTransform.hasChanged = false;
            _editorPreviewDirty = true;
            return true;
        }

        /// <summary>
        /// Consumes the edit-mode preview dirty flag set by atmosphere authoring changes.
        /// </summary>
        public bool ConsumeEditorPreviewDirty()
        {
            if (Application.isPlaying)
                return false;

            bool wasDirty = _editorPreviewDirty;
            _editorPreviewDirty = false;
            return wasDirty;
        }

        private void SyncCycleFromEditorSunTransform()
        {
            float3 sunForward = math.normalizesafe(
                (float3)_sunLight.transform.forward,
                new float3(0f, 0f, 1f));

            quaternion qInclination = quaternion.RotateZ(math.radians(_orbitalInclination));
            quaternion qAzimuth = quaternion.RotateY(math.radians(_sunOrbitalYAngle));
            quaternion orbitFrame = math.mul(qAzimuth, qInclination);
            float3 localForward = math.mul(math.inverse(orbitFrame), sunForward);
            localForward = math.normalizesafe(localForward, new float3(0f, 0f, 1f));

            float resolvedSunAngle = math.degrees(
                math.atan2(-localForward.y, localForward.z));
            if (resolvedSunAngle < 0f)
                resolvedSunAngle += 360f;

            _sunAngleDegrees = resolvedSunAngle;
            _cycleTimer = (_sunAngleDegrees / 360f) * _cycleDuration;

            double completedCycles = _cycleDuration > 0f
                ? math.floor(_elapsedCycleTimeSeconds / _cycleDuration)
                : 0d;
            _elapsedCycleTimeSeconds = completedCycles * _cycleDuration + _cycleTimer;

            _sunElevationDot = math.dot(-sunForward, new float3(0f, 1f, 0f));

            if (!sunForward.Equals(_cachedShaderSunDirection))
            {
                _cachedShaderSunDirection = sunForward;
                Shader.SetGlobalVector(
                    _shaderID_SunDirection,
                    new Vector4(sunForward.x, sunForward.y, sunForward.z, 0f));
            }

            PublishCycleShaderGlobals(_cycleTimer / _cycleDuration);

            SyncWaterSurfaceFromPlayerMovement();

            EnvironmentState resolvedState = ResolveState();
            AtmosphereProfile resolvedProfile = ResolveProfile(resolvedState);

            _currentState = resolvedState;
            _currentValues = resolvedProfile != null
                ? AtmosphereSnapshot.FromProfile(resolvedProfile)
                : AtmosphereSnapshot.Default;
            _transitionOrigin = _currentValues;
            _transitionProgress = 1f;

            ComputeSunValues();
            PublishGiantAbyssLight();
        }

        private void OnValidate()
        {
            if (EditorApplication.isCompiling || !Application.isPlaying)
                return;

            _editorInitialized = false;
            _editorPreviewDirty = true;
            _cycleDuration = math.max(_cycleDuration, 1f);

            if (_sunLight == null)
            {
                Light renderSettingsSun = RenderSettings.sun;
                if (renderSettingsSun != null && renderSettingsSun.type == LightType.Directional)
                    _sunLight = renderSettingsSun;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.1f, 0.4f, 0.9f, 0.25f);
            Vector3 center = new Vector3(
                transform.position.x, _waterSurfaceY, transform.position.z);
            Gizmos.DrawCube(center, new Vector3(200f, 0.05f, 200f));

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            const int segments = 64;
            const float orbitRadius = 50f;

            float incRad = math.radians(_orbitalInclination);
            float azRad  = math.radians(_sunOrbitalYAngle);

            quaternion qAzimuth     = quaternion.RotateY(azRad);
            quaternion qInclination = quaternion.RotateZ(incRad);
            quaternion orbitFrame   = math.mul(qAzimuth, qInclination);

            Vector3 prevPoint = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * math.PI * 2f;
                float3 localPoint = new float3(
                    math.cos(angle) * orbitRadius,
                    math.sin(angle) * orbitRadius, 0f);
                float3 worldPoint = math.mul(orbitFrame, localPoint);
                Vector3 wp = transform.position + (Vector3)worldPoint;

                if (i > 0) Gizmos.DrawLine(prevPoint, wp);
                prevPoint = wp;
            }

            if (_sunLight != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(
                    transform.position -
                    (Vector3)(_sunLight.transform.forward * orbitRadius), 2f);
            }
        }
#endif

        #endregion

        #region ══════════ ITickable ══════════

        public void Tick(float deltaTime)
        {
            SyncWaterSurfaceFromPlayerMovement();
            AdvanceCycleTimer(deltaTime);
            RotateSun();
            TickEclipseTimer(deltaTime);

            EnvironmentState resolved = ResolveState();
            ProcessStateTransition(resolved);

            InterpolateAtmosphere(deltaTime);
            ApplyProceduralBiomeInfluenceAtmosphere();

            ComputeSunValues();
        }

        #endregion

        #region ══════════ Cycle & Sun ══════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeCycleTimer()
        {
            _cycleTimer = _initialTimeOfDay * _cycleDuration;
            _elapsedCycleTimeSeconds = _cycleTimer;
        }

        private void InitializeAtmosphereValues()
        {
            AtmosphereProfile profile = ResolveProfile(_currentState);
            _currentValues = profile != null
                ? AtmosphereSnapshot.FromProfile(profile)
                : AtmosphereSnapshot.Default;
            _transitionOrigin   = _currentValues;
            _transitionProgress = 1f;

            _computedHorizonFade = 1f;
            _computedSunIntensity = _currentValues.sunIntensity;

            RotateSun();
            ComputeSunValues();
            PublishGiantAbyssLight();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdvanceCycleTimer(float deltaTime)
        {
            _elapsedCycleTimeSeconds += deltaTime;
            _cycleTimer += deltaTime;
            _cycleTimer  = math.fmod(_cycleTimer, _cycleDuration);
        }

        private void RotateSun()
        {
            if (_sunLight == null) return;

            float normalized = _cycleTimer / _cycleDuration;
            _sunAngleDegrees = normalized * 360f;

            float dailyRad       = math.radians(_sunAngleDegrees);
            float inclinationRad = math.radians(_orbitalInclination);
            float azimuthRad     = math.radians(_sunOrbitalYAngle);

            quaternion qDaily       = quaternion.RotateX(dailyRad);
            quaternion qInclination = quaternion.RotateZ(inclinationRad);
            quaternion qAzimuth     = quaternion.RotateY(azimuthRad);

            quaternion finalRotation = math.mul(
                qAzimuth, math.mul(qInclination, qDaily));

            _sunLight.transform.rotation = finalRotation;

            float3 sunForward = math.mul(finalRotation, new float3(0f, 0f, 1f));
            _sunElevationDot = math.dot(-sunForward, new float3(0f, 1f, 0f));

            // v2.1 OPT: Dirty-write batching — only write to shader if changed
            if (!sunForward.Equals(_cachedShaderSunDirection))
            {
                _cachedShaderSunDirection = sunForward;
                Shader.SetGlobalVector(
                    _shaderID_SunDirection,
                    new Vector4(sunForward.x, sunForward.y, sunForward.z, 0f));
            }

            PublishCycleShaderGlobals(normalized);
        }

        private void PublishCycleShaderGlobals(float normalizedTime)
        {
            float timeOfDay01 = math.saturate(normalizedTime);
            float thresholdSin = math.sin(math.radians(_nightThresholdAngle));
            float thresholdSpan = math.max(thresholdSin * 2f, 0.001f);
            float daytimeLerp = math.saturate((_sunElevationDot + thresholdSin) / thresholdSpan);
            float smoothedDaytime = daytimeLerp * daytimeLerp * (3f - 2f * daytimeLerp);
            float nightFactor = 1f - smoothedDaytime;
            float biolumPhaseMultiplier = HectonVegetationConstants.SargassumBiolumPhaseMultiplier;

            if (math.abs(timeOfDay01 - _cachedShaderTimeOfDay01) > 0.0001f)
            {
                _cachedShaderTimeOfDay01 = timeOfDay01;
                Shader.SetGlobalFloat(_shaderID_HectonTimeOfDay01, timeOfDay01);
            }

            if (math.abs(nightFactor - _cachedShaderNightFactor) > 0.0001f)
            {
                _cachedShaderNightFactor = nightFactor;
                Shader.SetGlobalFloat(_shaderID_HectonNightFactor, nightFactor);
            }

            if (math.abs(biolumPhaseMultiplier - _cachedShaderSargassumBiolumPhaseMultiplier) > 0.0001f || float.IsNaN(_cachedShaderSargassumBiolumPhaseMultiplier))
            {
                _cachedShaderSargassumBiolumPhaseMultiplier = biolumPhaseMultiplier;
                Shader.SetGlobalFloat(_shaderID_SargassumBiolumPhaseMultiplier, biolumPhaseMultiplier);
            }
        }

        private void ResetCycleShaderGlobals()
        {
            _cachedShaderTimeOfDay01 = -1f;
            _cachedShaderNightFactor = -1f;
            _cachedShaderSargassumBiolumPhaseMultiplier = float.NaN;
            _cachedFinalGiantAbyssLight = new float4(-1f, -1f, -1f, -1f);
            _cachedAegirDirection = new float4(0f, 0f, 0f, -999f);
            Shader.SetGlobalFloat(_shaderID_HectonTimeOfDay01, 0f);
            Shader.SetGlobalFloat(_shaderID_HectonNightFactor, 0f);
            Shader.SetGlobalFloat(_shaderID_SargassumBiolumPhaseMultiplier, HectonVegetationConstants.SargassumBiolumPhaseMultiplier);
            Shader.SetGlobalVector(_shaderID_FinalGiantAbyssLight, Vector4.zero);
            Shader.SetGlobalVector(_shaderID_AegirDirection, new Vector4(0f, 0f, 1f, 0f));
        }

        #endregion

        #region ══════════ Eclipse ══════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TickEclipseTimer(float deltaTime)
        {
            if (!_eclipseActive) return;
            _eclipseRemainingTime -= deltaTime;
            if (_eclipseRemainingTime <= 0f)
            {
                _eclipseRemainingTime = 0f;
                _eclipseActive        = false;
            }
        }

        #endregion

        #region ══════════ State Machine ══════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private EnvironmentState ResolveState()
        {
            if (_eclipseActive)       return EnvironmentState.ECLIPSE;
            if (EvaluateUnderwater()) return EnvironmentState.UNDERWATER;
            return EvaluateDaytime()
                ? EnvironmentState.SURFACE_DAY
                : EnvironmentState.SURFACE_NIGHT;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool EvaluateUnderwater()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                _autoUnderwaterState = false;
                return false;
            }
#endif

            if (_underwaterExternalFlag)
            {
                _autoUnderwaterState = true;
                return true;
            }

            if (!_useAutoUnderwaterDetection)
            {
                _autoUnderwaterState = false;
                return false;
            }

            if (_playerMovement != null)
            {
                _autoUnderwaterState = ResolveMovementUnderwaterState();
                return _autoUnderwaterState;
            }

            float depth = ResolvePlayerDepth();
            _autoUnderwaterState =
                SurfaceStateUtility.ResolveUnderwaterFromDepth(
                    depth,
                    _autoUnderwaterState,
                    VisualEnterUnderwaterDepth,
                    VisualExitUnderwaterDepth);

            return _autoUnderwaterState;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ResolveMovementUnderwaterState()
        {
            switch (_playerMovement.CurrentLocomotionMode)
            {
                case PlayerLocomotionMode.UnderwaterSwim:
                    return true;

                case PlayerLocomotionMode.ExosuitLocomotion:
                    return _playerMovement.CurrentDepth > 0.01f || _playerMovement.IsPlayerSubmerged;

                case PlayerLocomotionMode.SurfaceSwim:
                    return _playerMovement.IsPlayerSubmerged;

                default:
                    return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool EvaluateDaytime()
        {
            float thresholdSin = math.sin(math.radians(_nightThresholdAngle));
            return _sunElevationDot > thresholdSin;
        }

        private void ProcessStateTransition(EnvironmentState newState)
        {
            if (newState == _currentState) return;

            _transitionOrigin   = _currentValues;
            _transitionProgress = 0f;

            _currentState = newState;

            if (Application.isPlaying)
                AtmosphereEvents.RaiseStateChanged(_currentState);
        }

        #endregion

        #region ══════════ Interpolation ══════════

        private void InterpolateAtmosphere(float deltaTime)
        {
            if (_transitionProgress >= 1f) return;

            _transitionProgress = math.saturate(
                _transitionProgress + deltaTime * _transitionSpeed);

            float t = _transitionProgress;
            float smoothT = t * t * (3f - 2f * t);

            AtmosphereProfile target = ResolveProfile(_currentState);
            if (target == null) return;

            AtmosphereSnapshot targetSnap = AtmosphereSnapshot.FromProfile(target);
            _currentValues = AtmosphereSnapshot.Lerp(
                in _transitionOrigin, in targetSnap, smoothT);
        }

        #endregion

        #region ══════════ Sun Values (COMPUTE ONLY, NO WRITE) ══════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ComputeSunValues()
        {
            float fadeThreshold = math.sin(math.radians(_sunHorizonFadeAngle));

            if (_sunElevationDot <= 0f)
            {
                _computedHorizonFade = 0f;
            }
            else if (_sunElevationDot >= fadeThreshold)
            {
                _computedHorizonFade = 1f;
            }
            else
            {
                float st = _sunElevationDot / fadeThreshold;
                _computedHorizonFade = st * st * (3f - 2f * st);
            }

            _computedSunIntensity = _currentValues.sunIntensity * _computedHorizonFade;
        }

        private void PublishGiantAbyssLight()
        {
            HectonCelestialEngine celestial = HectonCelestialEngine.ActiveRuntimeInstance;
            float3 aegirDirection = new float3(0f, 0f, 1f);
            float planetPhase = 0f;
            float eclipseBacklit = 0f;

            if (celestial != null)
            {
                planetPhase = celestial.PlanetPhase;
                eclipseBacklit = math.saturate(celestial.EclipseBacklitFactor);
                if (celestial.TryGetAegirSkyDirection(out Vector3 direction))
                    aegirDirection = math.normalizesafe(new float3(direction.x, direction.y, direction.z), new float3(0f, 0f, 1f));
            }

            float depthMeters = math.max(0f, ResolvePlayerDepth());
            float3 sigmaRgbPerMeter = math.max(
                new float3(
                    _giantAbyssSigmaRgbPerMeter.x,
                    _giantAbyssSigmaRgbPerMeter.y,
                    _giantAbyssSigmaRgbPerMeter.z),
                float3.zero);
            float3 waterTransmittance = math.exp(-depthMeters * sigmaRgbPerMeter);

            Color surfaceLinearColor = _giantAbyssSurfaceLightColor.linear;
            float3 giantSurfaceColor = new float3(surfaceLinearColor.r, surfaceLinearColor.g, surfaceLinearColor.b);
            float phase01 = math.saturate((planetPhase * 0.5f) + 0.5f);
            float planetShineIntensity = math.max(0f, _giantAbyssLightIntensity) *
                                          math.saturate((phase01 * phase01) + (eclipseBacklit * 0.35f));

            Color biolumeLinearColor = _giantAbyssBiolumeColor.linear;
            float3 biolumeColor = new float3(biolumeLinearColor.r, biolumeLinearColor.g, biolumeLinearColor.b);
            float biolumeDepthMask = math.saturate(depthMeters * 0.0025f) * math.saturate(_cachedShaderNightFactor);

            float3 finalGiantAbyssLight = (giantSurfaceColor * waterTransmittance * planetShineIntensity) +
                                           (biolumeColor * math.max(0f, _giantAbyssBiolumeIntensity) * biolumeDepthMask);
            float4 finalPayload = new float4(finalGiantAbyssLight, planetShineIntensity);
            if (math.all(math.isfinite(finalPayload)) &&
                math.any(math.abs(finalPayload - _cachedFinalGiantAbyssLight) > 0.0001f))
            {
                _cachedFinalGiantAbyssLight = finalPayload;
                Shader.SetGlobalVector(_shaderID_FinalGiantAbyssLight, new Vector4(finalPayload.x, finalPayload.y, finalPayload.z, finalPayload.w));
            }

            float4 directionPayload = new float4(aegirDirection, planetPhase);
            if (math.all(math.isfinite(directionPayload)) &&
                math.any(math.abs(directionPayload - _cachedAegirDirection) > 0.0001f))
            {
                _cachedAegirDirection = directionPayload;
                Shader.SetGlobalVector(_shaderID_AegirDirection, new Vector4(directionPayload.x, directionPayload.y, directionPayload.z, directionPayload.w));
            }
        }

        #endregion

        #region ══════════ Profile Resolution ══════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AtmosphereProfile ResolveProfile(EnvironmentState state)
        {
            if (state == EnvironmentState.ECLIPSE)
                return _profileEclipse != null ? _profileEclipse : _profileDay;

            if (state == EnvironmentState.UNDERWATER)
                return _profileUnderwater != null ? _profileUnderwater : _profileDay;

            if (_activeMatrixProfile != null)
                return _activeMatrixProfile;

            if (_activeBiomeProfile != null)
                return _activeBiomeProfile;

            AtmosphereProfile profile = state switch
            {
                EnvironmentState.SURFACE_DAY   => _profileDay,
                EnvironmentState.SURFACE_NIGHT => _profileNight,
                _                              => _profileDay
            };

            return profile != null ? profile : _profileDay;
        }

        #endregion

        #region ══════════ Biome Handler ══════════

        private void ApplyProceduralBiomeInfluenceAtmosphere()
        {
            if (!Application.isPlaying || _currentState == EnvironmentState.ECLIPSE)
                return;

            RefreshProceduralBiomeInfluenceSnapshotIfNeeded();
            if (!_hasBiomeInfluenceAtmosphere)
                return;

            AtmosphereProfile primary = ResolveMatrixAtmosphereProfile(_biomeInfluencePrimaryProfile);
            if (primary == null)
                return;

            AtmosphereProfile secondary = ResolveMatrixAtmosphereProfile(_biomeInfluenceSecondaryProfile);
            if (secondary == null || _currentBiomeInfluence.SecondaryBiomeId == 0)
            {
                _currentValues.fogColor = primary.fogColor;
                _currentValues.fogDensity = primary.fogDensity;
                _currentValues.fogAttenuationDistance = math.max(0.001f, primary.fogAttenuationDistanceMeters);
                return;
            }

            float blend = _currentBiomeInfluence.Blend255 * (1f / 255f);
            _currentValues.fogColor = Color.Lerp(primary.fogColor, secondary.fogColor, blend);
            _currentValues.fogDensity = math.lerp(primary.fogDensity, secondary.fogDensity, blend);
            _currentValues.fogAttenuationDistance = math.max(
                0.001f,
                math.lerp(primary.fogAttenuationDistanceMeters, secondary.fogAttenuationDistanceMeters, blend));
        }

        private void RefreshProceduralBiomeInfluenceSnapshotIfNeeded()
        {
            float now = Time.unscaledTime;
            if (now < _nextBiomeInfluenceRefreshTime)
                return;

            _nextBiomeInfluenceRefreshTime = now + math.max(0.05f, _biomeInfluenceRefreshInterval);

            if (_proceduralFieldSampler == null)
                WorldRuntimeReferenceUtility.TryResolveWorldProceduralFieldSampler(ref _proceduralFieldSampler);

            Transform sampleTransform = _playerCameraTransform != null ? _playerCameraTransform : _playerTransform;
            if (_proceduralFieldSampler == null || sampleTransform == null)
            {
                _hasBiomeInfluenceAtmosphere = false;
                return;
            }

            if (_proceduralFieldSampler.TrySampleBiomeInfluence(
                    sampleTransform.position,
                    out WorldProceduralFieldSampler.BiomeInfluenceCell influence,
                    out HectonBiomeMatrixProfile primary,
                    out HectonBiomeMatrixProfile secondary))
            {
                _currentBiomeInfluence = influence;
                _biomeInfluencePrimaryProfile = primary;
                _biomeInfluenceSecondaryProfile = secondary;
                _hasBiomeInfluenceAtmosphere = primary != null;
                return;
            }

            _hasBiomeInfluenceAtmosphere = false;
            _currentBiomeInfluence = default;
            _biomeInfluencePrimaryProfile = null;
            _biomeInfluenceSecondaryProfile = null;
        }

        private static AtmosphereProfile ResolveMatrixAtmosphereProfile(HectonBiomeMatrixProfile profile)
        {
            return profile != null && profile.familyProfile != null
                ? profile.familyProfile.atmosphereProfile
                : null;
        }

        private void HandleBiomeChanged(int biomeID)
        {
            _currentBiomeID = biomeID;

            // v2.1 OPT: O(1) dictionary lookup instead of O(n) linear search
            AtmosphereProfile biomeProfile = null;
            if (_biomeProfileDict != null && _biomeProfileDict.TryGetValue(biomeID, out var profile))
            {
                biomeProfile = profile;
            }

            _activeBiomeProfile = biomeProfile;
            _transitionOrigin   = _currentValues;
            _transitionProgress = 0f;
        }

        private void HandleMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
            AtmosphereProfile nextProfile = profile != null && profile.familyProfile != null
                ? profile.familyProfile.atmosphereProfile
                : null;

            if (_activeMatrixProfile == nextProfile)
                return;

            _activeMatrixProfile = nextProfile;
            if (_currentState == EnvironmentState.UNDERWATER)
                return;

            _transitionOrigin = _currentValues;
            _transitionProgress = 0f;
        }

        void IBiomeMatrixEventListener.OnMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
            HandleMatrixBiomeChanged(profile);
        }

        void IBiomeMatrixEventListener.OnDepthTierChanged(int depthTier, float depthMeters)
        {
        }

        void IMapMagicBiomeEventListener.OnMapMagicBiomeChanged(int biomeId)
        {
            HandleBiomeChanged(biomeId);
        }

        #endregion

        #region ══════════ Public API ══════════

        public void TriggerEclipse(float duration)
        {
            if (duration <= 0f) return;
            _eclipseActive        = true;
            _eclipseRemainingTime = duration;
        }

        public void EndEclipse()
        {
            _eclipseActive        = false;
            _eclipseRemainingTime = 0f;
        }

        public void SetUnderwater(bool isUnderwater)
        {
            _underwaterExternalFlag = isUnderwater;
        }

        public void SetTimeOfDay(float normalized)
        {
            _cycleTimer = math.saturate(normalized) * _cycleDuration;
            double completedCycles = math.floor(_elapsedCycleTimeSeconds / _cycleDuration);
            _elapsedCycleTimeSeconds = completedCycles * _cycleDuration + _cycleTimer;
        }

        public void SetWaterSurfaceLevel(float worldY)
        {
            _waterSurfaceY = worldY;
        }

        public void SetPlayerTransform(Transform player)
        {
            _playerTransform = player;
            CachePlayerMovement();
        }

        public void SetCycleDuration(float seconds)
        {
            float normalized = _cycleDuration > 0f ? _cycleTimer / _cycleDuration : 0f;
            double completedCycles = _cycleDuration > 0f
                ? math.floor(_elapsedCycleTimeSeconds / _cycleDuration)
                : 0d;

            _cycleDuration = math.max(seconds, 1f);
            _cycleTimer = normalized * _cycleDuration;
            _elapsedCycleTimeSeconds = completedCycles * _cycleDuration + _cycleTimer;
        }

        public void SetTransitionSpeed(float speed)
        {
            _transitionSpeed = math.clamp(speed, 0.1f, 10f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SyncWaterSurfaceFromPlayerMovement()
        {
            if (_playerMovement == null)
                return;

            _waterSurfaceY = _playerMovement.CurrentWaterSurfaceY;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveSeaLevelY()
        {
            if (_playerMovement != null)
                return _playerMovement.CurrentWaterSurfaceY;

            return _waterSurfaceY;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolvePlayerDepth()
        {
            if (_playerMovement != null)
                return _playerMovement.CurrentDepth;

            if (_playerCameraTransform != null)
                return math.max(0f, _waterSurfaceY - _playerCameraTransform.position.y);

            if (_playerTransform != null)
                return math.max(0f, _waterSurfaceY - _playerTransform.position.y);

            return 0f;
        }

        private void CachePlayerMovement()
        {
            _playerMovement = null;
            _playerCameraTransform = null;

            if (_playerTransform != null)
            {
                _playerTransform.TryGetComponent(out _playerMovement);
                Camera playerOwnedCamera = Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null
                    ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera
                    : Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Camera>(_playerTransform);
                if (playerOwnedCamera != null)
                    _playerCameraTransform = playerOwnedCamera.transform;
            }
        }

        private void ResolveBiomeMatrixDirector()
        {
            if (!Application.isPlaying)
                return;

            WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref _biomeMatrixDirector);
        }

        private void ApplyCurrentMatrixAtmosphereOverride()
        {
            if (!Application.isPlaying)
                return;

            if (_biomeMatrixDirector == null)
                return;

            HandleMatrixBiomeChanged(_biomeMatrixDirector.CurrentProfile);
        }

        public void SetOrbitalInclination(float degrees)
        {
            _orbitalInclination = math.clamp(degrees, 0f, 90f);
        }

        #endregion
    }
}
