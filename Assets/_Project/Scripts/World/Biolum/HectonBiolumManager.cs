// â•”â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•—
// â•‘  HADES HECTON-8 | HectonBiolumManager                                       â•‘
// â•‘  Central bioluminescence system (manages all zones globally)                â•‘
// â•šâ•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using Hecton8.AI;
using Hecton8.Bootstrap;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Hecton8.Physics;
using System.Collections.Generic;
using Hecton8.Visor;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Biolum
{
    #pragma warning disable CS0414 // Placeholder serialized tuning kept for future global-light budget wiring.
    /// <summary>
    /// Central manager for all bioluminescence zones in the world.
    /// Tracks active zones, manages global pools, optimizes updates.
    /// Handles:
    /// - Cave zones (CaveBiolumZone)
    /// - Ocean zones (OceanBiolumZone)
    /// - Floor zones (FloorBiolumZone)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HectonBiolumManager : MonoBehaviour, ITickable, IUpdatable, ISonarPulseEventListener, IOriginShiftListener, IDisposable
    {
        private static readonly int _FloraOceanBiolumColorId = Shader.PropertyToID("_HectonOceanBiolumColor");
        private static readonly int _FloraOceanBiolumStrengthId = Shader.PropertyToID("_HectonOceanBiolumStrength");
        private static readonly int _FloraFloorBiolumColorId = Shader.PropertyToID("_HectonFloorBiolumColor");
        private static readonly int _FloraFloorBiolumStrengthId = Shader.PropertyToID("_HectonFloorBiolumStrength");
        private static readonly int _GlobalBiolumPhaseId = Shader.PropertyToID("_GlobalBiolumPhase");
        private static readonly int _BiolumMasterPhaseId = Shader.PropertyToID("_BiolumMasterPhase");
        private static readonly int _BiolumIntensityId = Shader.PropertyToID("_BiolumIntensity");
        private static readonly int _BiolumTouchRipplesId = Shader.PropertyToID("_BiolumTouchRipples");
        private static readonly int _BiolumTouchRippleParamsId = Shader.PropertyToID("_BiolumTouchRippleParams");

        private struct TouchRippleState
        {
            public float3 RuntimePosition;
            public float Radius;
            public float Intensity;
            public float Age;
            public float Lifetime;
            public uint SourceId;
            public byte Active;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BiolumTelemetryEntry
        {
            public uint Frame;
            public float3 CameraPosition;
            public float Intensity;
            public float Phase;
            public float PredatorDim;
            public float DaylightMask;
            public ushort PredatorHits;
            public byte ActiveRipples;
            public byte Flags;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct PredatorBlackoutJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> PredatorPositions;
            public float3 ObserverPosition;
            public float RadiusSq;
            [WriteOnly] public NativeArray<float> Scores;

            public void Execute(int index)
            {
                float3 predatorPosition = PredatorPositions[index];
                bool finite = math.all(math.isfinite(predatorPosition)) && math.all(math.isfinite(ObserverPosition));
                float score = 0f;
                if (finite && RadiusSq > 0.0001f)
                {
                    float3 delta = predatorPosition - ObserverPosition;
                    score = math.saturate(1f - (math.lengthsq(delta) * math.rcp(RadiusSq)));
                }

                Scores[index] = score;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct RippleDistanceJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> RipplePositions;
            public float3 ObserverPosition;
            [WriteOnly] public NativeArray<float> DistanceSq;

            public void Execute(int index)
            {
                float3 ripplePosition = RipplePositions[index];
                if (!math.all(math.isfinite(ripplePosition)) || !math.all(math.isfinite(ObserverPosition)))
                {
                    DistanceSq[index] = float.MaxValue;
                    return;
                }

                float3 delta = ripplePosition - ObserverPosition;
                DistanceSq[index] = math.lengthsq(delta);
            }
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // SINGLETON
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // INSPECTOR SETTINGS
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [Header("â”€â”€ Biolum Manager Settings â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField, Tooltip("Global intensity multiplier")]
        public float _globalIntensityScale = 1.0f;

        [SerializeField, Tooltip("Global range multiplier")]
        public float _globalRangeScale = 1.0f;

        [SerializeField, Range(0f, 1f), Tooltip("Global mood level (0=eerie, 1=vibrant)")]
        private float _globalMoodLevel = 0.5f;

        [SerializeField, Tooltip("Max total lights across all zones")]
        private int _maxTotalLights = 64;

        [SerializeField, Tooltip("Automatically find zones on start")]
        private bool _autoFindZones = true;

        [Header("â”€â”€ Sonar Communication â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField, Range(0f, 1f), Tooltip("Ð¡Ð¸Ð»Ð° ÐºÑ€Ð°Ñ‚ÐºÐ¾Ð³Ð¾ Ð±Ð¸Ð¾Ð»ÑŽÐ¼Ð¸Ð½ÐµÑÑ†ÐµÐ½Ñ‚Ð½Ð¾Ð³Ð¾ Ð¾Ñ‚Ð²ÐµÑ‚Ð° Ð½Ð° Ð°ÐºÑ‚Ð¸Ð²Ð½Ñ‹Ð¹ sonar pulse Ð¸Ð³Ñ€Ð¾ÐºÐ°.")]
        private float _sonarCommunicationBoost = 0.42f;

        [SerializeField, Range(1f, 3f), Tooltip("ÐÐ°ÑÐºÐ¾Ð»ÑŒÐºÐ¾ sonar pulse ÑƒÑÐ¸Ð»Ð¸Ð²Ð°ÐµÑ‚ ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÑŽÑ‰ÑƒÑŽ Ð¾ÐºÐµÐ°Ð½ÑÐºÑƒÑŽ/Ð´Ð¾Ð½Ð½ÑƒÑŽ Ð±Ð¸Ð¾Ð»ÑŽÐ¼Ð¸Ð½ÐµÑÑ†ÐµÐ½Ñ†Ð¸ÑŽ.")]
        private float _sonarStrengthMultiplier = 1.65f;

        [SerializeField, Range(0f, 0.25f), Tooltip("ÐÐ°ÑÐºÐ¾Ð»ÑŒÐºÐ¾ sonar pulse Ð¿Ð¾Ð´Ð½Ð¸Ð¼Ð°ÐµÑ‚ Ñ†Ð²ÐµÑ‚ Ð±Ð¸Ð¾Ð»ÑŽÐ¼Ð° Ðº Ñ…Ð¾Ð»Ð¾Ð´Ð½Ð¾Ð¼Ñƒ Ð¾Ñ‚Ð²ÐµÑ‚Ð½Ð¾Ð¼Ñƒ ÑÐ²ÐµÑ‡ÐµÐ½Ð¸ÑŽ.")]
        private float _sonarColorLift = 0.08f;

        [SerializeField, Tooltip("Ð¡ÐºÐ¾Ñ€Ð¾ÑÑ‚ÑŒ Ð·Ð°Ñ‚ÑƒÑ…Ð°Ð½Ð¸Ñ sonar-Ð¾Ñ‚Ð²ÐµÑ‚Ð° Ñ„Ð»Ð¾Ñ€Ñ‹.")]
        private float _sonarDecayRate = 0.75f;

        [SerializeField, Tooltip("ÐÐ¾Ñ€Ð¼Ð°Ð»Ð¸Ð·ÑƒÑŽÑ‰Ð¸Ð¹ Ñ€Ð°Ð´Ð¸ÑƒÑ sonar pulse Ð´Ð»Ñ Ñ€Ð°ÑÑ‡ÐµÑ‚Ð° ÑÐ¸Ð»Ñ‹ Ð¾Ñ‚Ð²ÐµÑ‚Ð½Ð¾Ð¹ Ð²Ð¾Ð»Ð½Ñ‹.")]
        private float _sonarReferenceRadius = 100f;

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // PRIVATE STATE
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private const int ActiveZoneListCapacity = 32;
        private const float GlobalBiolumPhaseRateHz = 0.58f;
        private const float GlobalBiolumPhasePublishEpsilon = 0.0001f;
        private const float ShaderColorPublishEpsilon = 0.0001f;
        private const int MaxTouchRipples = 16;
        private const int MaxPredatorContacts = 16;
        private const int BiolumTelemetryCapacity = 300;
        private const float PredatorBlackoutRadiusMeters = 50f;
        private const float PredatorBlackoutRadiusSq = PredatorBlackoutRadiusMeters * PredatorBlackoutRadiusMeters;
        private const float PredatorBlackoutMinimumIntensity = 0.1f;
        private const float PredatorBlackoutFadeInvSeconds = 0.5f;
        private const float PredatorBlackoutFadeRate = (1f - PredatorBlackoutMinimumIntensity) * PredatorBlackoutFadeInvSeconds;
        private const float ShallowDaylightCutoffY = -50f;
        private const int MovementSignalMaxDrainPerTick = 32;
        private const int BiolumDumpCooldownFrames = 300;
        private const string BiolumDumpRelativePath = "Docs/AgentLogs/Dump_BIOLUMINESCENCE_DIRECTOR.bin";
        private const uint ActiveBiolumRipplesHash = 0xB105A11Fu;
        private const uint BiolumDirectorContextHash = 0xB101D1ECu;

        // COLD ALLOC: List<HectonBiolumZone>[32] - active cave-zone registry - owner: HectonBiolumManager
        private readonly List<HectonBiolumZone> _activeCaveZones = new List<HectonBiolumZone>(ActiveZoneListCapacity);
        // COLD ALLOC: List<HectonBiolumZone>[32] - active ocean-zone registry - owner: HectonBiolumManager
        private readonly List<HectonBiolumZone> _activeOceanZones = new List<HectonBiolumZone>(ActiveZoneListCapacity);
        // COLD ALLOC: List<HectonBiolumZone>[32] - active floor-zone registry - owner: HectonBiolumManager
        private readonly List<HectonBiolumZone> _activeFloorZones = new List<HectonBiolumZone>(ActiveZoneListCapacity);

        private int _totalActiveLights = 0;
        private bool _initialized = false;
        private Camera _cachedCamera = null;
        private Transform _cachedCameraTransform = null;
        private bool _serviceRegistered = false;
        private bool _tickRegistered = false;
        private float _floraGlobalUpdateTimer = 0f;
        private float _nextCameraResolveTime = 0f;
        private float _sonarPulseBoost = 0f;
        private float _globalBiolumPhase = 0f;
        private float _lastPublishedGlobalBiolumPhase = -1f;
        private AbsoluteUniversePosition _cachedCameraAup;
        private int _cachedCameraAupFrame = -1;

        private const float CameraResolveCooldown = 1f;
        private static readonly Color _SonarResponseColor = new Color(0.62f, 0.94f, 1f, 1f);

        private Color _cachedOceanBiolumColor = Color.black;
        private Color _cachedFloorBiolumColor = Color.black;
        private float _cachedOceanBiolumStrength = 0f;
        private float _cachedFloorBiolumStrength = 0f;
        private Color _lastPublishedOceanBiolumColor = Color.black;
        private Color _lastPublishedFloorBiolumColor = Color.black;
        private float _lastPublishedOceanBiolumStrength = 0f;
        private float _lastPublishedFloorBiolumStrength = 0f;
        private bool _floraShaderGlobalsPublished = false;
        private float _masterPulse01 = 0.5f;
        private float _masterIntensity = 1f;
        private float _daylightMask = 1f;
        private float _eclipseMask = 0f;
        private float _flowFrequencyScale = 1f;
        private float _predatorTargetIntensity = 1f;
        private float _predatorCurrentIntensity = 1f;
        private int _activeTouchRippleCount = 0;
        private int _predatorCandidateCount = 0;
        private int _lastRippleTelemetryCount = -1;
        private int _lastRippleTelemetryFrame = -1;
        private int _telemetryWriteIndex = 0;
        private int _lastBiolumDumpFrame = -BiolumDumpCooldownFrames;
        private uint _telemetrySequence = 0u;
        private Vector4 _lastPublishedMasterPhase = new Vector4(-1f, -1f, -1f, -1f);
        private Vector4 _lastPublishedBiolumIntensity = new Vector4(-1f, -1f, -1f, -1f);

        // COLD ALLOC: TouchRippleState[16] - fixed touch ripple state pool; no per-frame containers - owner: HectonBiolumManager
        private readonly TouchRippleState[] _touchRipples = new TouchRippleState[MaxTouchRipples];
        // COLD ALLOC: Vector4[16] - fixed GPU upload staging for touch ripples - owner: HectonBiolumManager
        private readonly Vector4[] _touchRippleUpload = new Vector4[MaxTouchRipples];
        // COLD ALLOC: SpatialQueryHit[16] - fixed predator blackout spatial query buffer - owner: HectonBiolumManager
        private readonly SpatialQueryHit[] _predatorContacts = new SpatialQueryHit[MaxPredatorContacts];
        // COLD ALLOC: int[16] - compact ripple job source slot map - owner: HectonBiolumManager
        private readonly int[] _rippleJobSlotIndices = new int[MaxTouchRipples];
        // COLD ALLOC: int[16] - nearest-first ripple upload order - owner: HectonBiolumManager
        private readonly int[] _sortedTouchRippleIndices = new int[MaxTouchRipples];
        // COLD ALLOC: float[16] - nearest-first ripple distance scores - owner: HectonBiolumManager
        private readonly float[] _sortedTouchRippleDistanceSq = new float[MaxTouchRipples];

        private GraphicsBuffer _touchRippleBufferA;
        private GraphicsBuffer _touchRippleBufferB;
        private GraphicsBuffer _publishedTouchRippleBuffer;
        private int _lastPublishedTouchRippleCount = -1;
        private HectonQualityTier _lastPublishedTouchRippleTier = HectonQualityTier.Unknown;
        private NativeArray<float3> _predatorJobPositions;
        private NativeArray<float> _predatorJobScores;
        private JobHandle _predatorJobHandle;
        private bool _predatorJobScheduled = false;
        private int _scheduledPredatorCount = 0;
        private NativeArray<float3> _rippleJobPositions;
        private NativeArray<float> _rippleJobDistances;
        private JobHandle _rippleJobHandle;
        private bool _rippleJobScheduled = false;
        private int _scheduledRippleCount = 0;
        private int _sortedTouchRippleCount = 0;
        private bool _rippleSortReady = false;
        private NativeArray<BiolumTelemetryEntry> _telemetryRing;
        private bool _disposed = false;

        #if UNITY_EDITOR
        [SerializeField] private bool _debugLogUpdates = false;
        [SerializeField] private int _debugTickInvocations = 0;
        [SerializeField] private int _debugZoneTickPasses = 0;
        [SerializeField] private int _debugOceanZoneCount = 0;
        [SerializeField] private int _debugFloorZoneCount = 0;
        [SerializeField] private int _debugLastTickFrame = -1;
        [SerializeField] private float _debugLastTickDelta = 0f;
        #endif

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // LIFECYCLE
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void Awake()
        {
            HectonBiolumManager registered = GlobalRegistry.BiolumManager;
            if (Application.isPlaying && registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return;
            }

            EnsureRuntimeResources();
            ResetFloraShaderGlobals();
        }

        private void Start()
        {
            Initialize();
        }

        private void OnEnable()
        {
            TryRegisterService();
            TryRegister();
            HectonFloatingOrigin.RegisterListener(this);
            EnsureRuntimeResources();
            SpectrumEvents.RegisterSonarPulseListener(this);
        }

        private void OnDisable()
        {
            TryUnregisterService();
            TryUnregister();
            HectonFloatingOrigin.UnregisterListener(this);
            CompleteRuntimeJobs(true);
            SpectrumEvents.UnregisterSonarPulseListener(this);
            _sonarPulseBoost = 0f;

            ResetFloraShaderGlobals();
        }

        private void OnDestroy()
        {
            TryUnregisterService();
            TryUnregister();
            HectonFloatingOrigin.UnregisterListener(this);
            CompleteRuntimeJobs(true);
            SpectrumEvents.UnregisterSonarPulseListener(this);
            _sonarPulseBoost = 0f;

            ResetFloraShaderGlobals();
            Dispose();
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // PUBLIC API
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Register a bioluminescence zone (called by zone OnEnable).
        /// </summary>
        public void RegisterZone(HectonBiolumZone zone)
        {
            if (zone == null) return;

            zone.EnsureTickRegistration();

            if (zone is CaveBiolumZone cave)
            {
                if (!_activeCaveZones.Contains(zone))
                    _activeCaveZones.Add(zone);
            }
            else if (zone is OceanBiolumZone ocean)
            {
                if (!_activeOceanZones.Contains(zone))
                    _activeOceanZones.Add(zone);
            }
            else if (zone is FloorBiolumZone floor)
            {
                if (!_activeFloorZones.Contains(zone))
                    _activeFloorZones.Add(zone);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugLogUpdates) Debug.Log("[BiolumManager] Registered zone", this);
#endif
        }

        /// <summary>
        /// Unregister a bioluminescence zone (called by zone OnDisable).
        /// </summary>
        public void UnregisterZone(HectonBiolumZone zone)
        {
            if (zone == null) return;

            _activeCaveZones.Remove(zone);
            _activeOceanZones.Remove(zone);
            _activeFloorZones.Remove(zone);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugLogUpdates) Debug.Log("[BiolumManager] Unregistered zone", this);
#endif
        }

        /// <summary>
        /// Get total active lights across all zones.
        /// </summary>
        public int GetTotalActiveLights() => _totalActiveLights;

        /// <summary>
        /// Get zone count by type.
        /// </summary>
        public int GetCaveZoneCount() => _activeCaveZones.Count;
        public int GetOceanZoneCount() => _activeOceanZones.Count;
        public int GetFloorZoneCount() => _activeFloorZones.Count;

        internal int CopyNearbyZonesNonAlloc(Vector3 referencePosition, float maxDistance, HectonBiolumZone[] destination, float[] weights, bool includeOcean = true, bool includeFloor = true)
        {
            if (destination == null || destination.Length == 0 || weights == null || weights.Length < destination.Length)
                return 0;

            int count = 0;
            float maxDistanceSq = maxDistance > 0f ? maxDistance * maxDistance : float.PositiveInfinity;
            AbsoluteUniversePosition referenceAup = AbsoluteUniversePosition.FromRuntimePosition(referencePosition);

            if (includeOcean)
                count = CollectNearbyZonesNonAlloc(_activeOceanZones, in referenceAup, maxDistanceSq, destination, weights, count);

            if (includeFloor)
                count = CollectNearbyZonesNonAlloc(_activeFloorZones, in referenceAup, maxDistanceSq, destination, weights, count);

            return count;
        }

        /// <summary>
        /// Get camera position for LOD calculations (cached).
        /// </summary>
        public Vector3 GetCameraPosition()
        {
            TryResolveCameraReference(false);
            return _cachedCameraTransform != null ? _cachedCameraTransform.position : Vector3.zero;
        }

        /// <summary>
        /// Returns camera AUP cached for the current frame so zone LOD never does long-range transform subtraction.
        /// </summary>
        public AbsoluteUniversePosition GetCameraAup()
        {
            int frame = Time.frameCount;
            if (_cachedCameraAupFrame != frame)
            {
                _cachedCameraAup = AbsoluteUniversePosition.FromRuntimePosition(GetCameraPosition());
                _cachedCameraAupFrame = frame;
            }

            return _cachedCameraAup;
        }

        /// <summary>
        /// Set global mood level (affects all zones).
        /// </summary>
        public void SetGlobalMoodLevel(float mood)
        {
            _globalMoodLevel = Mathf.Clamp01(mood);
        }

        /// <summary>
        /// Set global intensity scale.
        /// </summary>
        public void SetGlobalIntensityScale(float scale)
        {
            _globalIntensityScale = Mathf.Max(0.1f, scale);
        }

        /// <summary>
        /// Set global range scale.
        /// </summary>
        public void SetGlobalRangeScale(float scale)
        {
            _globalRangeScale = Mathf.Max(0.1f, scale);
        }

        /// <summary>
        /// Update cheap global flora biolum shader inputs from the closest active ocean/floor zones.
        /// </summary>
        public void Tick(float deltaTime)
        {
            float safeDeltaTime = math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
            EnsureRuntimeResources();
            CompleteRuntimeJobs(false);
            DrainMovementAcousticSignals();
            UpdateTouchRipples(safeDeltaTime);
            UpdatePredatorBlackout(safeDeltaTime);
            PublishGlobalBiolumPhase(safeDeltaTime);
            RecordBiolumTelemetry();

#if UNITY_EDITOR
            _debugTickInvocations++;
            _debugLastTickFrame = Time.frameCount;
            _debugLastTickDelta = safeDeltaTime;
            _debugOceanZoneCount = _activeOceanZones.Count;
            _debugFloorZoneCount = _activeFloorZones.Count;
#endif
            if (_sonarPulseBoost > 0f)
            {
                _sonarPulseBoost = Mathf.Max(0f, _sonarPulseBoost - (_sonarDecayRate * safeDeltaTime));
            }

            _floraGlobalUpdateTimer += safeDeltaTime;
            if (_floraGlobalUpdateTimer < 0.18f)
            {
                return;
            }

            _floraGlobalUpdateTimer = 0f;
            UpdateFloraShaderGlobals();
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // PRIVATE: Initialization & Updates
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Initialize manager: find existing zones or wait for registration.
        /// </summary>
        private void Initialize()
        {
            if (_initialized) return;

            if (_autoFindZones)
            {
                FindExistingZones();
            }

            TryResolveCameraReference(true);

            _initialized = true;
            UpdateFloraShaderGlobals();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugLogUpdates) Debug.Log("[BiolumManager] Initialized", this);
#endif
        }

        /// <summary>
        /// Register all active biolum zones without a scene-wide object scan.
        /// </summary>
        private void FindExistingZones()
        {
            List<HectonBiolumZone> zones = HectonBiolumZone.ActiveZones;
            int count = zones.Count;
            for (int i = 0; i < count; i++)
            {
                HectonBiolumZone zone = zones[i];
                if (zone == null)
                    continue;

                zone.EnsureTickRegistration();
                RegisterZone(zone);
            }
        }

        private void UpdateFloraShaderGlobals()
        {
            AbsoluteUniversePosition cameraAup = GetCameraAup();

            Color oceanColor;
            float oceanStrength;
            bool hasOcean = TrySampleDominantZone(_activeOceanZones, in cameraAup, out oceanColor, out oceanStrength);

            Color floorColor;
            float floorStrength;
            bool hasFloor = TrySampleDominantZone(_activeFloorZones, in cameraAup, out floorColor, out floorStrength);

            _cachedOceanBiolumColor = hasOcean ? oceanColor : Color.black;
            _cachedFloorBiolumColor = hasFloor ? floorColor : Color.black;
            float sonarStrengthScale = 1f + ((_sonarStrengthMultiplier - 1f) * _sonarPulseBoost);
            float sonarColorLift = _sonarColorLift * _sonarPulseBoost;
            _cachedOceanBiolumStrength = hasOcean ? Mathf.Clamp01(oceanStrength * 0.28f * sonarStrengthScale) : 0f;
            _cachedFloorBiolumStrength = hasFloor ? Mathf.Clamp01(floorStrength * 0.24f * sonarStrengthScale) : 0f;

            if (hasOcean && sonarColorLift > 0f)
            {
                _cachedOceanBiolumColor = FastLerpColor(_cachedOceanBiolumColor, _SonarResponseColor, sonarColorLift);
            }

            if (hasFloor && sonarColorLift > 0f)
            {
                _cachedFloorBiolumColor = FastLerpColor(_cachedFloorBiolumColor, _SonarResponseColor, sonarColorLift);
            }

            PublishFloraShaderGlobals();
        }

        private void PublishGlobalBiolumPhase(float deltaTime)
        {
            ResolveCelestialBiolumState(out double celestialTime);
            ResolveAbyssalFlowFrequencyScale();

            float phaseStep = math.max(0f, deltaTime) * GlobalBiolumPhaseRateHz * _flowFrequencyScale;
            if (celestialTime > 0d)
            {
                double scaledPhase = celestialTime * GlobalBiolumPhaseRateHz * _flowFrequencyScale;
                _globalBiolumPhase = (float)(scaledPhase - math.floor(scaledPhase));
            }
            else
            {
                _globalBiolumPhase = math.frac(_globalBiolumPhase + phaseStep);
            }

            _masterPulse01 = math.saturate(0.5f + (math.sin(_globalBiolumPhase * math.PI * 2f) * 0.5f));
            float intensityScale = math.isfinite(_globalIntensityScale) ? math.max(0f, _globalIntensityScale) : 1f;
            // CelestialRuntime already owns _HectonCelestialBiolumMultiplier; this vector only carries director dimming.
            _masterIntensity = intensityScale * _daylightMask * _predatorCurrentIntensity;
            if (!math.isfinite(_masterIntensity))
            {
                _masterIntensity = 0f;
                DumpBiolumTelemetry(3);
            }

            Vector4 phaseVector = new Vector4(_globalBiolumPhase, _masterPulse01, _flowFrequencyScale, _eclipseMask);
            Vector4 intensityVector = new Vector4(_masterIntensity, _predatorCurrentIntensity, _daylightMask, _activeTouchRippleCount);

            if (VectorDeltaExceeds(_lastPublishedMasterPhase, phaseVector, GlobalBiolumPhasePublishEpsilon))
            {
                Shader.SetGlobalVector(_BiolumMasterPhaseId, phaseVector);
                Shader.SetGlobalFloat(_GlobalBiolumPhaseId, _globalBiolumPhase);
                _lastPublishedMasterPhase = phaseVector;
                _lastPublishedGlobalBiolumPhase = _globalBiolumPhase;
            }

            if (VectorDeltaExceeds(_lastPublishedBiolumIntensity, intensityVector, GlobalBiolumPhasePublishEpsilon))
            {
                Shader.SetGlobalVector(_BiolumIntensityId, intensityVector);
                _lastPublishedBiolumIntensity = intensityVector;
            }
        }

        private void ResolveCelestialBiolumState(out double celestialTime)
        {
            CelestialRuntimeSnapshot snapshot = GlobalRegistry.CelestialRuntimeSnapshot;
            bool valid = (snapshot.Flags & (uint)CelestialRuntimeFlags.Valid) != 0u;
            valid = valid &&
                !double.IsNaN(snapshot.AbsoluteUniverseTime) &&
                !double.IsInfinity(snapshot.AbsoluteUniverseTime) &&
                math.all(math.isfinite(snapshot.SunDirection)) &&
                math.isfinite(snapshot.EclipseOcclusion01);
            bool eclipse = valid &&
                (((snapshot.Flags & (uint)CelestialRuntimeFlags.EclipseActive) != 0u) ||
                 snapshot.EclipseOcclusion01 > 0.05f);
            Vector3 cameraPosition = GetCameraPosition();
            bool daylightShallow = valid && !eclipse && snapshot.SunDirection.y > 0.05f && cameraPosition.y > ShallowDaylightCutoffY;

            _daylightMask = daylightShallow ? 0f : 1f;
            _eclipseMask = eclipse ? 1f : 0f;
            celestialTime = valid ? snapshot.AbsoluteUniverseTime : Time.timeAsDouble;
        }

        private void ResolveAbyssalFlowFrequencyScale()
        {
            _flowFrequencyScale = 1f;
            HectonFluidEngine fluid = GlobalRegistry.Fluid;
            if (fluid == null)
                return;

            if (!fluid.TrySampleModAbyssalFlow(GetCameraPosition(), out float3 flowVector))
                return;

            float flowEnergy = math.lengthsq(flowVector);
            if (!math.isfinite(flowEnergy))
                return;

            _flowFrequencyScale = 1f + (math.saturate(flowEnergy * 0.04f) * 0.2f);
        }

        private void DrainMovementAcousticSignals()
        {
            int drained = 0;
            while (drained < MovementSignalMaxDrainPerTick && GlobalSignals.TryDequeueMovementAcoustic(out MovementAcousticSignal signal))
            {
                drained++;
                AddOrRefreshTouchRipple(in signal);
            }
        }

        private void AddOrRefreshTouchRipple(in MovementAcousticSignal signal)
        {
            float3 runtimePosition = signal.PositionAup.ToRuntimeFloat3();
            if (!math.all(math.isfinite(runtimePosition)) ||
                !math.isfinite(signal.Volume) ||
                !math.isfinite(signal.VelocitySq))
            {
                DumpBiolumTelemetry(1);
                return;
            }

            float velocity01 = math.saturate(signal.VelocitySq * 0.015f);
            float volume01 = math.saturate(signal.Volume);
            float intensity = math.saturate(0.28f + volume01 * 0.42f + velocity01 * 0.3f);
            if (intensity <= 0.001f)
                return;

            float radius = math.lerp(4f, 18f, velocity01);
            float lifetime = math.lerp(0.65f, 2.4f, velocity01);
            int slot = FindTouchRippleSlot(signal.SourceId);
            _touchRipples[slot].RuntimePosition = runtimePosition;
            _touchRipples[slot].Radius = radius;
            _touchRipples[slot].Intensity = intensity;
            _touchRipples[slot].Age = 0f;
            _touchRipples[slot].Lifetime = lifetime;
            _touchRipples[slot].SourceId = signal.SourceId;
            _touchRipples[slot].Active = 1;
            _rippleSortReady = false;
        }

        private int FindTouchRippleSlot(uint sourceId)
        {
            int firstInactive = -1;
            int weakestIndex = 0;
            float weakestScore = float.MaxValue;

            for (int i = 0; i < MaxTouchRipples; i++)
            {
                ref TouchRippleState ripple = ref _touchRipples[i];
                if (ripple.Active != 0 && ripple.SourceId == sourceId)
                    return i;

                if (ripple.Active == 0)
                {
                    if (firstInactive < 0)
                        firstInactive = i;
                    continue;
                }

                float remaining = math.max(0f, ripple.Lifetime - ripple.Age);
                float score = ripple.Intensity * remaining;
                if (score < weakestScore)
                {
                    weakestScore = score;
                    weakestIndex = i;
                }
            }

            return firstInactive >= 0 ? firstInactive : weakestIndex;
        }

        private void UpdateTouchRipples(float deltaTime)
        {
            float safeDeltaTime = math.max(0f, deltaTime);
            _activeTouchRippleCount = 0;
            for (int i = 0; i < MaxTouchRipples; i++)
            {
                if (_touchRipples[i].Active == 0)
                    continue;

                _touchRipples[i].Age += safeDeltaTime;
                if (_touchRipples[i].Age >= _touchRipples[i].Lifetime)
                {
                    _touchRipples[i] = default;
                    continue;
                }

                _activeTouchRippleCount++;
            }

            if (_activeTouchRippleCount == 0)
                _rippleSortReady = false;

            ScheduleRippleDistanceJob(GetCameraPosition());
            PublishTouchRippleBuffer();
        }

        private void ScheduleRippleDistanceJob(Vector3 observerPosition)
        {
            if (_rippleJobScheduled || !_rippleJobPositions.IsCreated || !_rippleJobDistances.IsCreated)
                return;

            float3 observer = new float3(observerPosition.x, observerPosition.y, observerPosition.z);
            if (!math.all(math.isfinite(observer)))
            {
                _rippleSortReady = false;
                DumpBiolumTelemetry(4);
                return;
            }

            int count = 0;
            for (int i = 0; i < MaxTouchRipples; i++)
            {
                if (_touchRipples[i].Active == 0)
                    continue;

                if (!math.all(math.isfinite(_touchRipples[i].RuntimePosition)))
                {
                    _touchRipples[i] = default;
                    DumpBiolumTelemetry(5);
                    continue;
                }

                _rippleJobSlotIndices[count] = i;
                _rippleJobPositions[count++] = _touchRipples[i].RuntimePosition;
            }

            _scheduledRippleCount = count;
            if (count <= 0)
                return;

            _rippleJobHandle = new RippleDistanceJob
            {
                RipplePositions = _rippleJobPositions,
                ObserverPosition = observer,
                DistanceSq = _rippleJobDistances
            }.Schedule(count, 4);
            _rippleJobScheduled = true;
        }

        private void PublishTouchRippleBuffer()
        {
            if (_touchRippleBufferA == null || _touchRippleBufferB == null)
                return;

            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            bool lowMathLod = !DistanceMath.IsHighQualityTier(tier);
            int writeCount = lowMathLod ? 0 : StageTouchRippleUpload();

            if (!lowMathLod && writeCount > 0)
            {
                GraphicsBuffer writeBuffer = ResolveTouchRippleWriteBuffer();
                if (writeBuffer != null)
                {
                    GraphicsBufferUploadUtility.UploadArray(writeBuffer, _touchRippleUpload, MaxTouchRipples);
                    _publishedTouchRippleBuffer = writeBuffer;
                    Shader.SetGlobalBuffer(_BiolumTouchRipplesId, writeBuffer);
                }
            }

            if (writeCount != _lastPublishedTouchRippleCount || tier != _lastPublishedTouchRippleTier)
            {
                Shader.SetGlobalVector(_BiolumTouchRippleParamsId, new Vector4(writeCount, 0f, 0f, 0f));
                _lastPublishedTouchRippleCount = writeCount;
                _lastPublishedTouchRippleTier = tier;
            }
        }

        private int StageTouchRippleUpload()
        {
            int count = 0;
            if (_rippleSortReady && _sortedTouchRippleCount > 0)
            {
                for (int order = 0; order < _sortedTouchRippleCount && count < MaxTouchRipples; order++)
                {
                    int slot = _sortedTouchRippleIndices[order];
                    StageTouchRippleSlot(slot, ref count);
                }

                for (int i = count; i < MaxTouchRipples; i++)
                    _touchRippleUpload[i] = Vector4.zero;

                return count;
            }

            for (int i = 0; i < MaxTouchRipples; i++)
                StageTouchRippleSlot(i, ref count);

            for (int i = count; i < MaxTouchRipples; i++)
                _touchRippleUpload[i] = Vector4.zero;

            return count;
        }

        private void StageTouchRippleSlot(int slot, ref int count)
        {
            if (slot < 0 || slot >= MaxTouchRipples || count >= MaxTouchRipples)
                return;

            if (_touchRipples[slot].Active == 0)
                return;

            float lifetime = math.max(0.0001f, _touchRipples[slot].Lifetime);
            float life01 = 1f - math.saturate(_touchRipples[slot].Age * math.rcp(lifetime));
            float radius = _touchRipples[slot].Radius * math.saturate(_touchRipples[slot].Intensity * life01);
            float3 position = _touchRipples[slot].RuntimePosition;
            if (!math.isfinite(radius) || radius <= 0.0001f || !math.all(math.isfinite(position)))
            {
                _touchRipples[slot] = default;
                DumpBiolumTelemetry(6);
                return;
            }

            _touchRippleUpload[count++] = new Vector4(position.x, position.y, position.z, radius);
        }

        private void UpdatePredatorBlackout(float deltaTime)
        {
            float step = (math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f) * PredatorBlackoutFadeRate;
            _predatorCurrentIntensity = Mathf.MoveTowards(_predatorCurrentIntensity, _predatorTargetIntensity, step);

            if (_predatorJobScheduled || !_predatorJobPositions.IsCreated || !_predatorJobScores.IsCreated)
                return;

            Vector3 cameraPosition = GetCameraPosition();
            int contactCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                cameraPosition,
                PredatorBlackoutRadiusMeters,
                SpatialTargetKind.Bioform,
                _predatorContacts);
            _predatorCandidateCount = 0;

            for (int i = 0; i < contactCount && _predatorCandidateCount < MaxPredatorContacts; i++)
            {
                SpatialQueryHit hit = _predatorContacts[i];
                if (!(hit.Owner is FaunaBrain brain) || brain.IsDead || !brain.IsApexPredatorRuntime)
                    continue;

                Vector3 predatorPosition = hit.Position;
                if (!math.all(math.isfinite(new float3(predatorPosition.x, predatorPosition.y, predatorPosition.z))))
                {
                    DumpBiolumTelemetry(7);
                    continue;
                }

                _predatorJobPositions[_predatorCandidateCount++] = new float3(predatorPosition.x, predatorPosition.y, predatorPosition.z);
            }

            _scheduledPredatorCount = _predatorCandidateCount;
            if (_scheduledPredatorCount <= 0)
            {
                _predatorTargetIntensity = 1f;
                return;
            }

            _predatorJobHandle = new PredatorBlackoutJob
            {
                PredatorPositions = _predatorJobPositions,
                ObserverPosition = new float3(cameraPosition.x, cameraPosition.y, cameraPosition.z),
                RadiusSq = PredatorBlackoutRadiusSq,
                Scores = _predatorJobScores
            }.Schedule(_scheduledPredatorCount, 4);
            _predatorJobScheduled = true;
        }

        private void CompleteRuntimeJobs(bool forceComplete)
        {
            if (_predatorJobScheduled && TryFinalizeRuntimeJob(ref _predatorJobHandle, forceComplete))
            {
                _predatorJobScheduled = false;
                float maxScore = 0f;
                for (int i = 0; i < _scheduledPredatorCount; i++)
                    maxScore = math.max(maxScore, _predatorJobScores[i]);

                _predatorTargetIntensity = math.lerp(1f, PredatorBlackoutMinimumIntensity, math.saturate(maxScore));
                _scheduledPredatorCount = 0;
            }

            if (_rippleJobScheduled && TryFinalizeRuntimeJob(ref _rippleJobHandle, forceComplete))
            {
                _rippleJobScheduled = false;
                FinalizeRippleDistanceOrder();
                _scheduledRippleCount = 0;
            }
        }

        private static bool TryFinalizeRuntimeJob(ref JobHandle handle, bool forceComplete)
        {
            return forceComplete
                ? DispatcherJobSwap.TryComplete(ref handle, true)
                : DispatcherJobSwap.TryFinalizeCompleted(ref handle);
        }

        private void FinalizeRippleDistanceOrder()
        {
            _sortedTouchRippleCount = 0;
            _rippleSortReady = false;
            for (int i = 0; i < _scheduledRippleCount && i < MaxTouchRipples; i++)
            {
                float distanceSq = _rippleJobDistances[i];
                int slot = _rippleJobSlotIndices[i];
                if (slot < 0 || slot >= MaxTouchRipples || _touchRipples[slot].Active == 0 || !math.isfinite(distanceSq))
                    continue;

                int writeIndex = _sortedTouchRippleCount;
                _sortedTouchRippleIndices[writeIndex] = slot;
                _sortedTouchRippleDistanceSq[writeIndex] = distanceSq;
                _sortedTouchRippleCount++;

                while (writeIndex > 0 && _sortedTouchRippleDistanceSq[writeIndex] < _sortedTouchRippleDistanceSq[writeIndex - 1])
                {
                    int swapIndex = _sortedTouchRippleIndices[writeIndex - 1];
                    _sortedTouchRippleIndices[writeIndex - 1] = _sortedTouchRippleIndices[writeIndex];
                    _sortedTouchRippleIndices[writeIndex] = swapIndex;

                    float swapDistance = _sortedTouchRippleDistanceSq[writeIndex - 1];
                    _sortedTouchRippleDistanceSq[writeIndex - 1] = _sortedTouchRippleDistanceSq[writeIndex];
                    _sortedTouchRippleDistanceSq[writeIndex] = swapDistance;
                    writeIndex--;
                }
            }

            _rippleSortReady = _sortedTouchRippleCount > 0;
        }

        private void EnsureRuntimeResources()
        {
            if (_disposed)
                _disposed = false;

            if (_touchRippleBufferA == null)
                _touchRippleBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(MaxTouchRipples);

            if (_touchRippleBufferB == null)
                _touchRippleBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(MaxTouchRipples);

            if (_publishedTouchRippleBuffer == null && _touchRippleBufferA != null)
            {
                GraphicsBufferUploadUtility.UploadArray(_touchRippleBufferA, _touchRippleUpload, MaxTouchRipples);
                GraphicsBufferUploadUtility.UploadArray(_touchRippleBufferB, _touchRippleUpload, MaxTouchRipples);
                _publishedTouchRippleBuffer = _touchRippleBufferA;
                Shader.SetGlobalBuffer(_BiolumTouchRipplesId, _publishedTouchRippleBuffer);
            }

            if (!_predatorJobPositions.IsCreated)
                _predatorJobPositions = new NativeArray<float3>(MaxPredatorContacts, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            if (!_predatorJobScores.IsCreated)
                _predatorJobScores = new NativeArray<float>(MaxPredatorContacts, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            if (!_rippleJobPositions.IsCreated)
                _rippleJobPositions = new NativeArray<float3>(MaxTouchRipples, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            if (!_rippleJobDistances.IsCreated)
                _rippleJobDistances = new NativeArray<float>(MaxTouchRipples, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            if (!_telemetryRing.IsCreated)
                _telemetryRing = new NativeArray<BiolumTelemetryEntry>(BiolumTelemetryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private void ReleaseRuntimeResources()
        {
            ReleaseGraphicsBuffer(ref _touchRippleBufferA);
            ReleaseGraphicsBuffer(ref _touchRippleBufferB);
            _publishedTouchRippleBuffer = null;

            DisposeNativeArray(ref _predatorJobPositions);
            DisposeNativeArray(ref _predatorJobScores);
            DisposeNativeArray(ref _rippleJobPositions);
            DisposeNativeArray(ref _rippleJobDistances);
            DisposeNativeArray(ref _telemetryRing);
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            array.Dispose();
            array = default;
        }

        private GraphicsBuffer ResolveTouchRippleWriteBuffer()
        {
            if (_publishedTouchRippleBuffer == null)
                return _touchRippleBufferA != null ? _touchRippleBufferA : _touchRippleBufferB;

            return ReferenceEquals(_publishedTouchRippleBuffer, _touchRippleBufferA)
                ? _touchRippleBufferB
                : _touchRippleBufferA;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            CompleteRuntimeJobs(true);
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float3 shift = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
            if (!math.all(math.isfinite(shift)))
                return;

            for (int i = 0; i < MaxTouchRipples; i++)
            {
                if (_touchRipples[i].Active == 0)
                    continue;

                _touchRipples[i].RuntimePosition -= shift;
            }
        }

        private void RecordBiolumTelemetry()
        {
            if (!_telemetryRing.IsCreated)
                return;

            Vector3 cameraPosition = GetCameraPosition();
            float3 cameraPosition3 = new float3(cameraPosition.x, cameraPosition.y, cameraPosition.z);
            byte flags = 0;
            if (_daylightMask <= 0.001f)
                flags |= 1;
            if (_predatorCurrentIntensity < 0.999f)
                flags |= 2;
            if (_eclipseMask > 0.001f)
                flags |= 4;
            if (!math.all(math.isfinite(cameraPosition3)))
            {
                flags |= 8;
                cameraPosition3 = float3.zero;
                DumpBiolumTelemetry(8);
            }

            float safeIntensity = math.isfinite(_masterIntensity) ? _masterIntensity : 0f;
            float safePhase = math.isfinite(_globalBiolumPhase) ? _globalBiolumPhase : 0f;
            float safePredatorDim = math.isfinite(_predatorCurrentIntensity) ? _predatorCurrentIntensity : 1f;
            float safeDaylightMask = math.isfinite(_daylightMask) ? _daylightMask : 1f;

            _telemetryRing[_telemetryWriteIndex] = new BiolumTelemetryEntry
            {
                Frame = (uint)Time.frameCount,
                CameraPosition = cameraPosition3,
                Intensity = safeIntensity,
                Phase = safePhase,
                PredatorDim = safePredatorDim,
                DaylightMask = safeDaylightMask,
                PredatorHits = (ushort)math.min(_predatorCandidateCount, ushort.MaxValue),
                ActiveRipples = (byte)math.min(_activeTouchRippleCount, byte.MaxValue),
                Flags = flags
            };
            _telemetryWriteIndex = (_telemetryWriteIndex + 1) % BiolumTelemetryCapacity;
            _telemetrySequence++;

            if (_activeTouchRippleCount != _lastRippleTelemetryCount || Time.frameCount - _lastRippleTelemetryFrame >= 30)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    ActiveBiolumRipplesHash,
                    BiolumDirectorContextHash,
                    _activeTouchRippleCount);
                _lastRippleTelemetryCount = _activeTouchRippleCount;
                _lastRippleTelemetryFrame = Time.frameCount;
            }

            if (!math.isfinite(_masterIntensity) || !math.isfinite(_globalBiolumPhase))
                DumpBiolumTelemetry(2);
        }

        private void DumpBiolumTelemetry(byte reasonFlags)
        {
            if (!_telemetryRing.IsCreated)
                return;

            int frame = Time.frameCount;
            if (frame - _lastBiolumDumpFrame < BiolumDumpCooldownFrames)
                return;

            _lastBiolumDumpFrame = frame;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dumpPath = Path.Combine(projectRoot, BiolumDumpRelativePath);
            string directory = Path.GetDirectoryName(dumpPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(0x42494F4Cu);
                writer.Write(_telemetrySequence);
                writer.Write(reasonFlags);
                writer.Write(BiolumTelemetryCapacity);
                for (int i = 0; i < BiolumTelemetryCapacity; i++)
                {
                    BiolumTelemetryEntry entry = _telemetryRing[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.CameraPosition.x);
                    writer.Write(entry.CameraPosition.y);
                    writer.Write(entry.CameraPosition.z);
                    writer.Write(entry.Intensity);
                    writer.Write(entry.Phase);
                    writer.Write(entry.PredatorDim);
                    writer.Write(entry.DaylightMask);
                    writer.Write(entry.PredatorHits);
                    writer.Write(entry.ActiveRipples);
                    writer.Write(entry.Flags);
                }
            }
        }

        private static bool VectorDeltaExceeds(Vector4 left, Vector4 right, float epsilon)
        {
            return math.abs(left.x - right.x) > epsilon ||
                   math.abs(left.y - right.y) > epsilon ||
                   math.abs(left.z - right.z) > epsilon ||
                   math.abs(left.w - right.w) > epsilon;
        }

        private static Color FastLerpColor(Color from, Color to, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(
                from.r + ((to.r - from.r) * t),
                from.g + ((to.g - from.g) * t),
                from.b + ((to.b - from.b) * t),
                from.a + ((to.a - from.a) * t));
        }

        private bool TryResolveCameraReference(bool force)
        {
            if (_cachedCameraTransform != null)
                return true;

            float currentTime = Time.unscaledTime;
            if (!force && currentTime < _nextCameraResolveTime)
                return false;

            _nextCameraResolveTime = currentTime + CameraResolveCooldown;

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null)
            {
                IPlayerRuntimeContext playerContext = Hecton8.Core.GlobalRegistry.Player;
                Camera playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
                if (playerCamera == null)
                    playerTransform.TryGetComponent(out playerCamera);

                if (playerCamera != null)
                {
                    _cachedCamera = playerCamera;
                    _cachedCameraTransform = playerCamera.transform;
                    return true;
                }

                _cachedCameraTransform = playerTransform;
                _cachedCamera = null;
                return true;
            }

            return false;
        }

        private bool TrySampleDominantZone(List<HectonBiolumZone> zones, in AbsoluteUniversePosition referenceAup, out Color sampledColor, out float sampledStrength)
        {
            sampledColor = Color.black;
            sampledStrength = 0f;

            int count = zones.Count;
            for (int i = 0; i < count; i++)
            {
                HectonBiolumZone zone = zones[i];
                if (zone == null)
                {
                    continue;
                }

                float zoneRange = zone.SampleZoneRange();
                if (zoneRange <= 0.01f)
                {
                    continue;
                }

                float zoneRangeSq = zoneRange * zoneRange;
                AbsoluteUniversePosition zoneAup = zone.GetZoneAup();
                double distanceSqDouble = AbsoluteUniversePosition.DistanceSq(in zoneAup, in referenceAup);
                if (distanceSqDouble > zoneRangeSq)
                {
                    continue;
                }

                float distanceSq = (float)math.min(distanceSqDouble, float.MaxValue);
                float proximity = 1f - Mathf.Clamp01(distanceSq / zoneRangeSq);
                float weightedStrength = zone.SampleZoneIntensity() * proximity;
                if (weightedStrength <= sampledStrength)
                {
                    continue;
                }

                sampledStrength = weightedStrength;
                sampledColor = zone.SampleZoneColor();
            }

            return sampledStrength > 0f;
        }

        private static int CollectNearbyZonesNonAlloc(List<HectonBiolumZone> zones, in AbsoluteUniversePosition referenceAup, float maxDistanceSq, HectonBiolumZone[] destination, float[] weights, int count)
        {
            int destinationCapacity = destination.Length;
            if (destinationCapacity == 0)
                return 0;

            int zoneCount = zones.Count;
            for (int i = 0; i < zoneCount; i++)
            {
                HectonBiolumZone zone = zones[i];
                if (zone == null)
                    continue;

                float zoneRange = zone.SampleZoneRange();
                if (zoneRange <= 0.01f)
                    continue;

                AbsoluteUniversePosition zoneAup = zone.GetZoneAup();
                double distanceSqDouble = AbsoluteUniversePosition.DistanceSq(in zoneAup, in referenceAup);
                float effectiveRangeSq = zoneRange * zoneRange;
                if (distanceSqDouble > effectiveRangeSq || distanceSqDouble > maxDistanceSq)
                    continue;

                float distanceSq = (float)math.min(distanceSqDouble, float.MaxValue);
                float proximity = 1f - Mathf.Clamp01(distanceSq / effectiveRangeSq);
                float score = zone.SampleZoneIntensity() * proximity;
                if (score <= 0f)
                    continue;

                if (count < destinationCapacity)
                {
                    destination[count] = zone;
                    weights[count] = score;
                    count++;
                    InsertZoneDescending(destination, weights, count - 1);
                    continue;
                }

                int weakestIndex = destinationCapacity - 1;
                if (score <= weights[weakestIndex])
                    continue;

                destination[weakestIndex] = zone;
                weights[weakestIndex] = score;
                InsertZoneDescending(destination, weights, weakestIndex);
            }

            return count;
        }

        private static void InsertZoneDescending(HectonBiolumZone[] destination, float[] weights, int index)
        {
            while (index > 0 && weights[index] > weights[index - 1])
            {
                HectonBiolumZone zone = destination[index - 1];
                destination[index - 1] = destination[index];
                destination[index] = zone;

                float weight = weights[index - 1];
                weights[index - 1] = weights[index];
                weights[index] = weight;
                index--;
            }
        }

        private void ResetFloraShaderGlobals()
        {
            _cachedOceanBiolumColor = Color.black;
            _cachedFloorBiolumColor = Color.black;
            _cachedOceanBiolumStrength = 0f;
            _cachedFloorBiolumStrength = 0f;

            PublishFloraShaderGlobals();
            if (math.abs(_lastPublishedGlobalBiolumPhase) > GlobalBiolumPhasePublishEpsilon)
                Shader.SetGlobalFloat(_GlobalBiolumPhaseId, 0f);
            _globalBiolumPhase = 0f;
            _lastPublishedGlobalBiolumPhase = 0f;
            _masterPulse01 = 0.5f;
            _masterIntensity = 0f;
            _predatorTargetIntensity = 1f;
            _predatorCurrentIntensity = 1f;
            _daylightMask = 1f;
            _eclipseMask = 0f;
            Vector4 resetPhase = new Vector4(0f, 0.5f, 1f, 0f);
            Vector4 resetIntensity = Vector4.zero;
            _lastPublishedMasterPhase = resetPhase;
            _lastPublishedBiolumIntensity = resetIntensity;
            Shader.SetGlobalVector(_BiolumMasterPhaseId, resetPhase);
            Shader.SetGlobalVector(_BiolumIntensityId, resetIntensity);
            Shader.SetGlobalVector(_BiolumTouchRippleParamsId, Vector4.zero);
            _lastPublishedTouchRippleCount = 0;
            _lastPublishedTouchRippleTier = HectonQualityTier.Unknown;
        }

        private void PublishFloraShaderGlobals()
        {
            bool forcePublish = !_floraShaderGlobalsPublished;
            if (forcePublish ||
                !NearlyEqual(_lastPublishedOceanBiolumColor, _cachedOceanBiolumColor, ShaderColorPublishEpsilon))
            {
                Shader.SetGlobalColor(_FloraOceanBiolumColorId, _cachedOceanBiolumColor);
                _lastPublishedOceanBiolumColor = _cachedOceanBiolumColor;
            }

            if (forcePublish ||
                math.abs(_lastPublishedOceanBiolumStrength - _cachedOceanBiolumStrength) > ShaderColorPublishEpsilon)
            {
                Shader.SetGlobalFloat(_FloraOceanBiolumStrengthId, _cachedOceanBiolumStrength);
                _lastPublishedOceanBiolumStrength = _cachedOceanBiolumStrength;
            }

            if (forcePublish ||
                !NearlyEqual(_lastPublishedFloorBiolumColor, _cachedFloorBiolumColor, ShaderColorPublishEpsilon))
            {
                Shader.SetGlobalColor(_FloraFloorBiolumColorId, _cachedFloorBiolumColor);
                _lastPublishedFloorBiolumColor = _cachedFloorBiolumColor;
            }

            if (forcePublish ||
                math.abs(_lastPublishedFloorBiolumStrength - _cachedFloorBiolumStrength) > ShaderColorPublishEpsilon)
            {
                Shader.SetGlobalFloat(_FloraFloorBiolumStrengthId, _cachedFloorBiolumStrength);
                _lastPublishedFloorBiolumStrength = _cachedFloorBiolumStrength;
            }

            _floraShaderGlobalsPublished = true;
        }

        private static bool NearlyEqual(Color left, Color right, float epsilon)
        {
            return math.abs(left.r - right.r) <= epsilon &&
                   math.abs(left.g - right.g) <= epsilon &&
                   math.abs(left.b - right.b) <= epsilon &&
                   math.abs(left.a - right.a) <= epsilon;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            HectonBiolumManager registered = GlobalRegistry.BiolumManager;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return;
            }

            if (!GameBootstrapper.RegisterBiolumDirector(this))
                return;

            _serviceRegistered = ReferenceEquals(GlobalRegistry.BiolumManager, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GameBootstrapper.UnregisterBiolumDirector(this);
            _serviceRegistered = false;
        }

        private void HandleSonarPulse(float radius)
        {
            if (!math.isfinite(radius) || !math.isfinite(_sonarReferenceRadius))
                return;

            float normalizedRadius = Mathf.Clamp01(radius / Mathf.Max(1f, _sonarReferenceRadius));
            if (normalizedRadius <= 0f)
            {
                return;
            }

            _sonarPulseBoost = Mathf.Max(_sonarPulseBoost, _sonarCommunicationBoost * normalizedRadius);

            if (!_initialized)
            {
                return;
            }

            _floraGlobalUpdateTimer = 0f;
            UpdateFloraShaderGlobals();
        }

        void ISonarPulseEventListener.OnSonarPulse(float radius)
        {
            HandleSonarPulse(radius);
        }

        /// <summary>
        /// Releases persistent native and graphics resources owned by the biolum director.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            CompleteRuntimeJobs(true);
            ReleaseRuntimeResources();
            _disposed = true;
        }

        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // EDITOR
        // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void TryRegister()
        {
            if (_tickRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _maxTotalLights = Mathf.Max(1, _maxTotalLights);
            _sonarDecayRate = Mathf.Max(0.01f, _sonarDecayRate);
            _sonarReferenceRadius = Mathf.Max(1f, _sonarReferenceRadius);
        }
#endif
    }
    #pragma warning restore CS0414
}
