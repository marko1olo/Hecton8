// â•”â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•—
// â•‘  HADES HECTON-8 | HectonBiolumManager                                       â•‘
// â•‘  Central bioluminescence system (manages all zones globally)                â•‘
// â•šâ•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using Hecton8.Bootstrap;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Visor;
using Hecton8.World;
using Unity.Collections;
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
    public sealed class HectonBiolumManager : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, ISonarPulseEventListener, IOriginShiftListener, IGlobalRegistryHotSwapListener, IDisposable
    {
        private static readonly int _FloraOceanBiolumColorId = Shader.PropertyToID("_HectonOceanBiolumColor");
        private static readonly int _FloraOceanBiolumStrengthId = Shader.PropertyToID("_HectonOceanBiolumStrength");
        private static readonly int _FloraFloorBiolumColorId = Shader.PropertyToID("_HectonFloorBiolumColor");
        private static readonly int _FloraFloorBiolumStrengthId = Shader.PropertyToID("_HectonFloorBiolumStrength");
        private static readonly int _BiolumIntensityId = Shader.PropertyToID("_BiolumIntensity");
        private static readonly int _GlobalBiolumParamsId = Shader.PropertyToID("_GlobalBiolumParams");
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

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        private struct BiolumTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public uint Frame;
            [System.Runtime.InteropServices.FieldOffset(4)]
            public float CameraPositionX;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public float CameraPositionY;
            [System.Runtime.InteropServices.FieldOffset(12)]
            public float CameraPositionZ;
            [System.Runtime.InteropServices.FieldOffset(16)]
            public float Intensity;
            [System.Runtime.InteropServices.FieldOffset(20)]
            public float Phase;
            [System.Runtime.InteropServices.FieldOffset(24)]
            public float PredatorDim;
            [System.Runtime.InteropServices.FieldOffset(28)]
            public ushort PredatorHits;
            [System.Runtime.InteropServices.FieldOffset(30)]
            public byte ActiveRipples;
            [System.Runtime.InteropServices.FieldOffset(31)]
            public byte Flags;
            [System.Runtime.InteropServices.FieldOffset(32)]
            private byte _pad0;
            [System.Runtime.InteropServices.FieldOffset(33)]
            private byte _pad1;
            [System.Runtime.InteropServices.FieldOffset(34)]
            private byte _pad2;
            [System.Runtime.InteropServices.FieldOffset(35)]
            private byte _pad3;
            [System.Runtime.InteropServices.FieldOffset(36)]
            private byte _pad4;
            [System.Runtime.InteropServices.FieldOffset(37)]
            private byte _pad5;
            [System.Runtime.InteropServices.FieldOffset(38)]
            private byte _pad6;
            [System.Runtime.InteropServices.FieldOffset(39)]
            private byte _pad7;
            [System.Runtime.InteropServices.FieldOffset(40)]
            private byte _pad8;
            [System.Runtime.InteropServices.FieldOffset(41)]
            private byte _pad9;
            [System.Runtime.InteropServices.FieldOffset(42)]
            private byte _pad10;
            [System.Runtime.InteropServices.FieldOffset(43)]
            private byte _pad11;
            [System.Runtime.InteropServices.FieldOffset(44)]
            private byte _pad12;
            [System.Runtime.InteropServices.FieldOffset(45)]
            private byte _pad13;
            [System.Runtime.InteropServices.FieldOffset(46)]
            private byte _pad14;
            [System.Runtime.InteropServices.FieldOffset(47)]
            private byte _pad15;
            [System.Runtime.InteropServices.FieldOffset(48)]
            private byte _pad16;
            [System.Runtime.InteropServices.FieldOffset(49)]
            private byte _pad17;
            [System.Runtime.InteropServices.FieldOffset(50)]
            private byte _pad18;
            [System.Runtime.InteropServices.FieldOffset(51)]
            private byte _pad19;
            [System.Runtime.InteropServices.FieldOffset(52)]
            private byte _pad20;
            [System.Runtime.InteropServices.FieldOffset(53)]
            private byte _pad21;
            [System.Runtime.InteropServices.FieldOffset(54)]
            private byte _pad22;
            [System.Runtime.InteropServices.FieldOffset(55)]
            private byte _pad23;
            [System.Runtime.InteropServices.FieldOffset(56)]
            private byte _pad24;
            [System.Runtime.InteropServices.FieldOffset(57)]
            private byte _pad25;
            [System.Runtime.InteropServices.FieldOffset(58)]
            private byte _pad26;
            [System.Runtime.InteropServices.FieldOffset(59)]
            private byte _pad27;
            [System.Runtime.InteropServices.FieldOffset(60)]
            private byte _pad28;
            [System.Runtime.InteropServices.FieldOffset(61)]
            private byte _pad29;
            [System.Runtime.InteropServices.FieldOffset(62)]
            private byte _pad30;
            [System.Runtime.InteropServices.FieldOffset(63)]
            private byte _pad31;
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
        private const int BiolumDumpHeaderBytes = 13;
        private const int BiolumDumpEntryBytes = 32;
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
        private const SystemID VaultOwnerSystem = SystemID.Vfx;
        private static readonly ulong TelemetryRingMutationGuardMask =
            1UL << ((int)BufferID.BiolumLegacyTelemetryRing & 31);

        // COLD ALLOC: fixed zone registry arrays; bounded, no List growth in runtime route.
        private readonly HectonBiolumZone[] _activeCaveZones = new HectonBiolumZone[ActiveZoneListCapacity];
        private readonly HectonBiolumZone[] _activeOceanZones = new HectonBiolumZone[ActiveZoneListCapacity];
        private readonly HectonBiolumZone[] _activeFloorZones = new HectonBiolumZone[ActiveZoneListCapacity];
        private int _activeCaveZoneCount;
        private int _activeOceanZoneCount;
        private int _activeFloorZoneCount;
        private int _zoneRegistryOverflowCount;

        private int _totalActiveLights = 0;
        private bool _initialized = false;
        private Camera _cachedCamera = null;
        private Transform _cachedCameraTransform = null;
        private Vector3 _cachedCameraPosition = Vector3.zero;
        private bool _serviceRegistered = false;
        private bool _tickRegistered = false;
        private bool _lateFrameRegistered = false;
        private bool _hotSwapRegistered = false;
        private IDataVault _dataVault;
        private ITickDispatcher _cachedTickDispatcher;
        private IAbyssalFlowGpuReadModel _cachedFluid;
        private IPlayerRuntimeContext _cachedPlayerContext;
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
        private bool _biolumIntensitySuppressedByPulseSync = false;
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
        private Vector4 _pendingMasterPhase = new Vector4(-1f, -1f, -1f, -1f);
        private Vector4 _pendingBiolumIntensity = new Vector4(-1f, -1f, -1f, -1f);
        private bool _globalBiolumPhaseDirty = false;
        private bool _floraShaderGlobalsDirty = false;
        private bool _touchRippleVisualDirty = false;
        private int _pendingTouchRippleWriteCount = 0;
        private int _pendingTouchRippleQualityBucket = -1;
        private float _pendingTouchRippleUploadBlend = 0f;
        private float _pendingTouchRippleQualityWeight = 1f;

        // COLD ALLOC: TouchRippleState[16] - fixed touch ripple state pool; no per-frame containers - owner: HectonBiolumManager
        private readonly TouchRippleState[] _touchRipples = new TouchRippleState[MaxTouchRipples];
        // COLD ALLOC: Vector4[16] - fixed GPU upload staging for touch ripples - owner: HectonBiolumManager
        private readonly Vector4[] _touchRippleUpload = new Vector4[MaxTouchRipples];
        // COLD ALLOC: SpatialQueryHit[16] - fixed predator blackout spatial query buffer - owner: HectonBiolumManager
        private readonly SpatialQueryHit[] _predatorContacts = new SpatialQueryHit[MaxPredatorContacts];
        // COLD ALLOC: int[16] - compact ripple source slot map - owner: HectonBiolumManager
        private readonly int[] _rippleSourceSlotIndices = new int[MaxTouchRipples];
        // COLD ALLOC: int[16] - nearest-first ripple upload order - owner: HectonBiolumManager
        private readonly int[] _sortedTouchRippleIndices = new int[MaxTouchRipples];
        // COLD ALLOC: float[16] - nearest-first ripple distance scores - owner: HectonBiolumManager
        private readonly float[] _sortedTouchRippleDistanceSq = new float[MaxTouchRipples];

        private GraphicsBuffer _touchRippleBufferA;
        private GraphicsBuffer _touchRippleBufferB;
        private GraphicsBuffer _publishedTouchRippleBuffer;
        private int _lastPublishedTouchRippleCount = -1;
        private int _lastPublishedTouchRippleQualityBucket = -1;
        private int _scheduledRippleCount = 0;
        private int _sortedTouchRippleCount = 0;
        private bool _rippleSortReady = false;
        // COLD ALLOC: float[16] - direct owner-phase ripple distance scratch replacing tiny jobs - owner: HectonBiolumManager
        private readonly float[] _rippleDistanceSqScratch = new float[MaxTouchRipples];
        private VaultGenerationHandle<BiolumTelemetryEntry> _telemetryRingHandle;
        private bool _disposed = false;
        private double _fallbackCelestialTimeSeconds;
        private ICelestialRuntimeSnapshotReadModel _cachedCelestialSnapshot;

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

            CacheGlobalRegistryServicesCold();
            EnsureRuntimeResources();
            ResetFloraShaderGlobals();
        }

        private void Start()
        {
            Initialize();
        }

        private void OnEnable()
        {
            CacheGlobalRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegisterService();
            TryRegister();
            TryRegisterLateFrameTick();
            HectonFloatingOrigin.RegisterListener(this);
            EnsureRuntimeResources();
            SpectrumEvents.RegisterSonarPulseListener(this);
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            TryUnregister();
            TryUnregisterLateFrameTick();
            HectonFloatingOrigin.UnregisterListener(this);
            ReleaseVaultHandlesOnly();
            SpectrumEvents.UnregisterSonarPulseListener(this);
            _sonarPulseBoost = 0f;

            ResetFloraShaderGlobals();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            TryUnregister();
            TryUnregisterLateFrameTick();
            HectonFloatingOrigin.UnregisterListener(this);
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

            if (zone is CaveBiolumZone)
            {
                if (!TryAddZoneNonAlloc(_activeCaveZones, ref _activeCaveZoneCount, zone))
                    NoteZoneRegistryOverflow();
            }
            else if (zone is OceanBiolumZone)
            {
                if (!TryAddZoneNonAlloc(_activeOceanZones, ref _activeOceanZoneCount, zone))
                    NoteZoneRegistryOverflow();
            }
            else if (zone is FloorBiolumZone)
            {
                if (!TryAddZoneNonAlloc(_activeFloorZones, ref _activeFloorZoneCount, zone))
                    NoteZoneRegistryOverflow();
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugLogUpdates) H8Debug.Log("[BiolumManager] Registered zone");
#endif
        }

        /// <summary>
        /// Unregister a bioluminescence zone (called by zone OnDisable).
        /// </summary>
        public void UnregisterZone(HectonBiolumZone zone)
        {
            if (zone == null) return;

            RemoveZoneNonAlloc(_activeCaveZones, ref _activeCaveZoneCount, zone);
            RemoveZoneNonAlloc(_activeOceanZones, ref _activeOceanZoneCount, zone);
            RemoveZoneNonAlloc(_activeFloorZones, ref _activeFloorZoneCount, zone);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugLogUpdates) H8Debug.Log("[BiolumManager] Unregistered zone");
#endif
        }

        /// <summary>
        /// Get total active lights across all zones.
        /// </summary>
        public int GetTotalActiveLights() => _totalActiveLights;

        /// <summary>
        /// Get zone count by type.
        /// </summary>
        public int GetCaveZoneCount() => _activeCaveZoneCount;
        public int GetOceanZoneCount() => _activeOceanZoneCount;
        public int GetFloorZoneCount() => _activeFloorZoneCount;

        private void NoteZoneRegistryOverflow()
        {
            if (_zoneRegistryOverflowCount < int.MaxValue)
                _zoneRegistryOverflowCount++;
        }

        private static bool TryAddZoneNonAlloc(HectonBiolumZone[] zones, ref int count, HectonBiolumZone zone)
        {
            int safeCount = math.clamp(count, 0, zones.Length);
            for (int i = 0; i < safeCount; i++)
            {
                if (ReferenceEquals(zones[i], zone))
                {
                    count = safeCount;
                    return true;
                }
            }

            if (safeCount >= zones.Length)
            {
                count = safeCount;
                return false;
            }

            zones[safeCount] = zone;
            count = safeCount + 1;
            return true;
        }

        private static bool RemoveZoneNonAlloc(HectonBiolumZone[] zones, ref int count, HectonBiolumZone zone)
        {
            int safeCount = math.clamp(count, 0, zones.Length);
            for (int i = 0; i < safeCount; i++)
            {
                if (!ReferenceEquals(zones[i], zone))
                    continue;

                int last = safeCount - 1;
                for (int move = i; move < last; move++)
                {
                    zones[move] = zones[move + 1];
                }

                zones[last] = null;
                count = last;
                return true;
            }

            count = safeCount;
            return false;
        }

        internal int CopyNearbyZonesNonAlloc(Vector3 referencePosition, float maxDistance, HectonBiolumZone[] destination, float[] weights, bool includeOcean = true, bool includeFloor = true)
        {
            if (destination == null || destination.Length == 0 || weights == null || weights.Length < destination.Length)
                return 0;

            int count = 0;
            float maxDistanceSq = maxDistance > 0f ? maxDistance * maxDistance : float.PositiveInfinity;
            if (!TryBuildAupFromRuntimeOrigin(referencePosition, out AbsoluteUniversePosition referenceAup))
                return 0;

            if (includeOcean)
                count = CollectNearbyZonesNonAlloc(_activeOceanZones, _activeOceanZoneCount, in referenceAup, maxDistanceSq, destination, weights, count);

            if (includeFloor)
                count = CollectNearbyZonesNonAlloc(_activeFloorZones, _activeFloorZoneCount, in referenceAup, maxDistanceSq, destination, weights, count);

            return count;
        }

        /// <summary>
        /// Get camera position for LOD calculations (cached).
        /// </summary>
        public Vector3 GetCameraPosition()
        {
            return _cachedCameraPosition;
        }

        /// <summary>
        /// Returns camera AUP cached for the current frame so zone LOD never does long-range transform subtraction.
        /// </summary>
        public AbsoluteUniversePosition GetCameraAup()
        {
            return _cachedCameraAup;
        }

        private static bool TryBuildAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition absoluteAup)
        {
            absoluteAup = default;
            if (!float.IsFinite(runtimePosition.x) ||
                !float.IsFinite(runtimePosition.y) ||
                !float.IsFinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!AbsoluteUniversePosition.IsFinite(in originAup))
                return false;

            absoluteAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return AbsoluteUniversePosition.IsFinite(in absoluteAup);
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
            RefreshCameraSnapshotHot();
            DrainMovementAcousticSignals();
            UpdateTouchRipples(safeDeltaTime);
            UpdatePredatorBlackout(safeDeltaTime);
            UpdateGlobalBiolumPhase(safeDeltaTime);
            RecordBiolumTelemetry();

#if UNITY_EDITOR
            _debugTickInvocations++;
            _debugLastTickFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            _debugLastTickDelta = safeDeltaTime;
            _debugOceanZoneCount = _activeOceanZoneCount;
            _debugFloorZoneCount = _activeFloorZoneCount;
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

        public void LateFrameTick()
        {
            if (_touchRippleVisualDirty && !AreRuntimeResourcesReady())
                return;

            FlushGlobalBiolumPhase();

            if (_touchRippleVisualDirty)
                FlushTouchRippleBuffer();

            if (_floraShaderGlobalsDirty)
            {
                _floraShaderGlobalsDirty = false;
                PublishFloraShaderGlobals();
            }

            if (!_globalBiolumPhaseDirty &&
                !_floraShaderGlobalsDirty &&
                !_touchRippleVisualDirty)
            {
                return;
            }
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

            RefreshCameraSnapshotCold();

            _initialized = true;
            UpdateFloraShaderGlobals();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugLogUpdates) H8Debug.Log("[BiolumManager] Initialized");
#endif
        }

        /// <summary>
        /// Register all active biolum zones without a scene-wide object scan.
        /// </summary>
        private void FindExistingZones()
        {
            int count = HectonBiolumZone.ActiveZoneCount;
            for (int i = 0; i < count; i++)
            {
                HectonBiolumZone zone = HectonBiolumZone.GetActiveZoneAt(i);
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
            bool hasOcean = TrySampleDominantZone(_activeOceanZones, _activeOceanZoneCount, in cameraAup, out oceanColor, out oceanStrength);

            Color floorColor;
            float floorStrength;
            bool hasFloor = TrySampleDominantZone(_activeFloorZones, _activeFloorZoneCount, in cameraAup, out floorColor, out floorStrength);

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

            _floraShaderGlobalsDirty = true;
        }

        private void UpdateGlobalBiolumPhase(float deltaTime)
        {
            UpdateCelestialBiolumStateFromSnapshot(deltaTime, out double celestialTime);
            UpdateAbyssalFlowFrequencyScale();

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

            _masterPulse01 = math.saturate(0.5f + (MathLodApproximation.ApproxSinBhaskara(_globalBiolumPhase * math.PI * 2f) * 0.5f));
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

            if (VectorDeltaExceeds(_lastPublishedMasterPhase, phaseVector, GlobalBiolumPhasePublishEpsilon) ||
                VectorDeltaExceeds(_lastPublishedBiolumIntensity, intensityVector, GlobalBiolumPhasePublishEpsilon) ||
                _biolumIntensitySuppressedByPulseSync)
            {
                _pendingMasterPhase = phaseVector;
                _pendingBiolumIntensity = intensityVector;
                _globalBiolumPhaseDirty = true;
            }
        }

        private void FlushGlobalBiolumPhase()
        {
            if (!_globalBiolumPhaseDirty)
                return;

            Vector4 phaseVector = _pendingMasterPhase;
            Vector4 intensityVector = _pendingBiolumIntensity;
            bool pulseSyncOwnsLegacyGlobals = IsGlobalPulseSyncOwningLegacyBiolumGlobals();
            if (!pulseSyncOwnsLegacyGlobals &&
                VectorDeltaExceeds(_lastPublishedMasterPhase, phaseVector, GlobalBiolumPhasePublishEpsilon))
            {
                HectonShaderGlobalDataVaultBridge.PublishBiolumMasterPhase(phaseVector);
                _lastPublishedMasterPhase = phaseVector;
                _lastPublishedGlobalBiolumPhase = _globalBiolumPhase;
            }

            if (_biolumIntensitySuppressedByPulseSync ||
                VectorDeltaExceeds(_lastPublishedBiolumIntensity, intensityVector, GlobalBiolumPhasePublishEpsilon))
            {
                if (pulseSyncOwnsLegacyGlobals)
                {
                    _biolumIntensitySuppressedByPulseSync = true;
                    _lastPublishedBiolumIntensity = intensityVector;
                    _globalBiolumPhaseDirty = false;
                    return;
                }

                Shader.SetGlobalVector(_BiolumIntensityId, intensityVector);
                _lastPublishedBiolumIntensity = intensityVector;
                _biolumIntensitySuppressedByPulseSync = false;
            }

            _globalBiolumPhaseDirty = false;
        }

        private static bool IsGlobalPulseSyncOwningLegacyBiolumGlobals()
        {
            Vector4 syncParams = Shader.GetGlobalVector(_GlobalBiolumParamsId);
            return syncParams.x > 0.5f;
        }

        private void UpdateCelestialBiolumStateFromSnapshot(float safeDeltaTime, out double celestialTime)
        {
            ICelestialRuntimeSnapshotReadModel readModel = _cachedCelestialSnapshot;
            CelestialRuntimeSnapshot snapshot = readModel != null ? readModel.RuntimeSnapshot : default;
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
            celestialTime = valid ? snapshot.AbsoluteUniverseTime : AdvanceFallbackCelestialTimeSeconds(safeDeltaTime);
        }

        private double AdvanceFallbackCelestialTimeSeconds(float safeDeltaTime)
        {
            ITickDispatcher dispatcher = _cachedTickDispatcher;
            if (dispatcher != null)
            {
                H8TimeSnapshot timeSnapshot = dispatcher.TimeSnapshot;
                if (timeSnapshot.Time >= 0d && !double.IsNaN(timeSnapshot.Time) && !double.IsInfinity(timeSnapshot.Time))
                {
                    _fallbackCelestialTimeSeconds = timeSnapshot.Time;
                    return _fallbackCelestialTimeSeconds;
                }
            }

            _fallbackCelestialTimeSeconds += math.min(math.max(safeDeltaTime, 0f), 0.25f);
            if (_fallbackCelestialTimeSeconds < 0d ||
                double.IsNaN(_fallbackCelestialTimeSeconds) ||
                double.IsInfinity(_fallbackCelestialTimeSeconds))
            {
                _fallbackCelestialTimeSeconds = 0d;
            }

            return _fallbackCelestialTimeSeconds;
        }

        private void UpdateAbyssalFlowFrequencyScale()
        {
            _flowFrequencyScale = 1f;
            IAbyssalFlowGpuReadModel fluid = _cachedFluid;
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
            ReadOnlySpan<MovementAcousticSignal> signals = SignalBus<MovementAcousticSignal>.GetFrameSnapshot();
            int count = math.min(signals.Length, MovementSignalMaxDrainPerTick);
            for (int i = 0; i < count; i++)
            {
                ref readonly MovementAcousticSignal signal = ref signals[i];
                AddOrRefreshTouchRipple(in signal);
            }
        }

        private void AddOrRefreshTouchRipple(in MovementAcousticSignal signal)
        {
            AbsoluteUniversePosition runtimeOriginAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            float3 runtimePosition = AUPMath.ResolveCameraRelative(in signal.PositionAup, in runtimeOriginAup);
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

            UpdateRippleDistanceOrder(GetCameraPosition());
            QueueTouchRippleBufferPublish();
        }

        private void UpdateRippleDistanceOrder(Vector3 observerPosition)
        {
            float3 observer = new float3(observerPosition.x, observerPosition.y, observerPosition.z);
            if (!math.all(math.isfinite(observer)))
            {
                _rippleSortReady = false;
                _scheduledRippleCount = 0;
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

                _rippleSourceSlotIndices[count] = i;
                float3 delta = _touchRipples[i].RuntimePosition - observer;
                _rippleDistanceSqScratch[count++] = math.lengthsq(delta);
            }

            _scheduledRippleCount = count;
            if (count <= 0)
            {
                _rippleSortReady = false;
                return;
            }

            FinalizeRippleDistanceOrder(_rippleDistanceSqScratch);
            _scheduledRippleCount = 0;
        }

        private void QueueTouchRippleBufferPublish()
        {
            float qualityWeight = SampleLegacyTouchRippleQualityWeight();
            int qualityBucket = (int)math.round(qualityWeight * 255f);
            float uploadBlend = math.smoothstep(0.12f, 0.72f, qualityWeight);
            int maxWriteCount = (int)math.round(math.lerp(0f, MaxTouchRipples, uploadBlend));
            int writeCount = maxWriteCount > 0 ? StageTouchRippleUpload(maxWriteCount) : 0;

            _pendingTouchRippleWriteCount = writeCount;
            _pendingTouchRippleQualityBucket = qualityBucket;
            _pendingTouchRippleUploadBlend = uploadBlend;
            _pendingTouchRippleQualityWeight = qualityWeight;
            _touchRippleVisualDirty = true;
        }

        private void FlushTouchRippleBuffer()
        {
            if (_touchRippleBufferA == null || _touchRippleBufferB == null)
            {
                _touchRippleVisualDirty = false;
                return;
            }

            int writeCount = _pendingTouchRippleWriteCount;

            if (writeCount > 0)
            {
                GraphicsBuffer writeBuffer = SelectTouchRippleWriteBuffer();
                if (writeBuffer != null)
                {
                    GraphicsBufferUploadUtility.UploadArray(writeBuffer, _touchRippleUpload, MaxTouchRipples);
                    _publishedTouchRippleBuffer = writeBuffer;
                    Shader.SetGlobalBuffer(_BiolumTouchRipplesId, writeBuffer);
                }
            }

            if (writeCount != _lastPublishedTouchRippleCount ||
                _pendingTouchRippleQualityBucket != _lastPublishedTouchRippleQualityBucket)
            {
                Shader.SetGlobalVector(
                    _BiolumTouchRippleParamsId,
                    new Vector4(
                        writeCount,
                        _pendingTouchRippleUploadBlend,
                        _pendingTouchRippleQualityWeight,
                        0f));
                _lastPublishedTouchRippleCount = writeCount;
                _lastPublishedTouchRippleQualityBucket = _pendingTouchRippleQualityBucket;
            }

            _touchRippleVisualDirty = false;
        }

        private static float SampleLegacyTouchRippleQualityWeight()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(qualityWeight) ? qualityWeight : 1f);
        }

        private int StageTouchRippleUpload(int maxUploadCount)
        {
            int uploadLimit = math.clamp(maxUploadCount, 0, MaxTouchRipples);
            int count = 0;
            if (_rippleSortReady && _sortedTouchRippleCount > 0)
            {
                for (int order = 0; order < _sortedTouchRippleCount && count < uploadLimit; order++)
                {
                    int slot = _sortedTouchRippleIndices[order];
                    StageTouchRippleSlot(slot, ref count);
                }

                for (int i = count; i < MaxTouchRipples; i++)
                    _touchRippleUpload[i] = Vector4.zero;

                return count;
            }

            for (int i = 0; i < MaxTouchRipples && count < uploadLimit; i++)
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

            Vector3 cameraPosition = GetCameraPosition();
            float3 observer = new float3(cameraPosition.x, cameraPosition.y, cameraPosition.z);
            if (!math.all(math.isfinite(observer)))
            {
                _predatorCandidateCount = 0;
                _predatorTargetIntensity = 1f;
                DumpBiolumTelemetry(7);
                return;
            }

            int contactCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                cameraPosition,
                PredatorBlackoutRadiusMeters,
                SpatialTargetKind.Bioform,
                _predatorContacts);
            _predatorCandidateCount = 0;
            float maxScore = 0f;

            for (int i = 0; i < contactCount && _predatorCandidateCount < MaxPredatorContacts; i++)
            {
                SpatialQueryHit hit = _predatorContacts[i];
                if (!(hit.Owner is IFaunaSpatialContact faunaContact) ||
                    faunaContact.IsDead ||
                    !faunaContact.IsApexPredatorContact)
                    continue;

                Vector3 predatorPosition = hit.Position;
                if (!math.all(math.isfinite(new float3(predatorPosition.x, predatorPosition.y, predatorPosition.z))))
                {
                    DumpBiolumTelemetry(7);
                    continue;
                }

                float3 predatorPosition3 = new float3(predatorPosition.x, predatorPosition.y, predatorPosition.z);
                float3 delta = predatorPosition3 - observer;
                float score = math.saturate(1f - (math.lengthsq(delta) * math.rcp(PredatorBlackoutRadiusSq)));
                _predatorCandidateCount++;
                maxScore = math.max(maxScore, score);
            }

            if (_predatorCandidateCount <= 0)
            {
                _predatorTargetIntensity = 1f;
                return;
            }

            _predatorTargetIntensity = math.lerp(1f, PredatorBlackoutMinimumIntensity, math.saturate(maxScore));
        }

        private void FinalizeRippleDistanceOrder(float[] rippleJobDistances)
        {
            _sortedTouchRippleCount = 0;
            _rippleSortReady = false;
            for (int i = 0; i < _scheduledRippleCount && i < MaxTouchRipples; i++)
            {
                float distanceSq = rippleJobDistances[i];
                int slot = _rippleSourceSlotIndices[i];
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

            EnsureVaultBuffers();
        }

        private bool AreRuntimeResourcesReady()
        {
            return !_disposed &&
                   _touchRippleBufferA != null &&
                   _touchRippleBufferB != null &&
                   _publishedTouchRippleBuffer != null;
        }

        private void ReleaseRuntimeResources()
        {
            ReleaseGraphicsBuffer(ref _touchRippleBufferA);
            ReleaseGraphicsBuffer(ref _touchRippleBufferB);
            _publishedTouchRippleBuffer = null;

            ReleaseVaultHandlesOnly();
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private bool EnsureVaultBuffers()
        {
            return EnsureBiolumVaultBuffer(
                ref _telemetryRingHandle,
                BufferID.BiolumLegacyTelemetryRing,
                BiolumTelemetryCapacity,
                NativeArrayOptions.ClearMemory,
                out _);
        }

        private void ReleaseVaultHandlesOnly()
        {
            IDataVault vault = _dataVault;
            ReleaseBiolumVaultHandle(vault, ref _telemetryRingHandle);
        }

        private bool EnsureBiolumVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (TryResolveBiolumVaultBuffer(ref handle, bufferId, requiredLength, out buffer))
                return true;

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, VaultOwnerSystem, options);
            return TryResolveBiolumVaultBuffer(ref handle, bufferId, requiredLength, out buffer);
        }

        private bool TryResolveBiolumVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (HasBiolumVaultHandle(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (!vault.TryGetGenerationHandle<T>(bufferId, out handle) ||
                !HasBiolumVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                handle = default;
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool HasBiolumVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)VaultOwnerSystem &&
                   handle.Generation != 0u;
        }

        private static void ReleaseBiolumVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && handle.BufferID != 0u && handle.Generation != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private bool TryAcquireTelemetryRingGuard(out IDataVault vault, out NativeArray<BiolumTelemetryEntry> telemetryRing)
        {
            vault = null;
            telemetryRing = default;

            vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(TelemetryRingMutationGuardMask))
            {
                vault = null;
                return false;
            }

            bool keepGuard = false;
            try
            {
                keepGuard =
                    !vault.IsCompactionFenceActive &&
                    TryResolveBiolumVaultBuffer(
                        ref _telemetryRingHandle,
                        BufferID.BiolumLegacyTelemetryRing,
                        BiolumTelemetryCapacity,
                        out telemetryRing) &&
                    telemetryRing.IsCreated;
                return keepGuard;
            }
            finally
            {
                if (!keepGuard)
                {
                    vault.ReleaseMutationGuard(TelemetryRingMutationGuardMask);
                    vault = null;
                    telemetryRing = default;
                }
            }
        }

        private static void ReleaseTelemetryRingGuard(IDataVault vault)
        {
            vault?.ReleaseMutationGuard(TelemetryRingMutationGuardMask);
        }

        private GraphicsBuffer SelectTouchRippleWriteBuffer()
        {
            if (_publishedTouchRippleBuffer == null)
                return _touchRippleBufferA != null ? _touchRippleBufferA : _touchRippleBufferB;

            return ReferenceEquals(_publishedTouchRippleBuffer, _touchRippleBufferA)
                ? _touchRippleBufferB
                : _touchRippleBufferA;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
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
            Vector3 cameraPosition = GetCameraPosition();
            float3 cameraPosition3 = new float3(cameraPosition.x, cameraPosition.y, cameraPosition.z);
            byte flags = 0;
            byte dumpReason = 0;
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
                dumpReason |= 8;
            }
            if (_zoneRegistryOverflowCount > 0)
                flags |= 16;

            float safeIntensity = math.isfinite(_masterIntensity) ? _masterIntensity : 0f;
            float safePhase = math.isfinite(_globalBiolumPhase) ? _globalBiolumPhase : 0f;
            float safePredatorDim = math.isfinite(_predatorCurrentIntensity) ? _predatorCurrentIntensity : 1f;

            if (!TryAcquireTelemetryRingGuard(out IDataVault telemetryVault, out NativeArray<BiolumTelemetryEntry> telemetryRing))
                return;

            try
            {
                telemetryRing[_telemetryWriteIndex] = new BiolumTelemetryEntry
                {
                    Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                    CameraPositionX = cameraPosition3.x,
                    CameraPositionY = cameraPosition3.y,
                    CameraPositionZ = cameraPosition3.z,
                    Intensity = safeIntensity,
                    Phase = safePhase,
                    PredatorDim = safePredatorDim,
                    PredatorHits = (ushort)math.min(_predatorCandidateCount, ushort.MaxValue),
                    ActiveRipples = (byte)math.min(_activeTouchRippleCount, byte.MaxValue),
                    Flags = flags
                };
                _telemetryWriteIndex = (_telemetryWriteIndex + 1) % BiolumTelemetryCapacity;
                _telemetrySequence++;
            }
            finally
            {
                ReleaseTelemetryRingGuard(telemetryVault);
            }

            int currentFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_activeTouchRippleCount != _lastRippleTelemetryCount || currentFrame - _lastRippleTelemetryFrame >= 30)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    ActiveBiolumRipplesHash,
                    BiolumDirectorContextHash,
                    _activeTouchRippleCount);
                _lastRippleTelemetryCount = _activeTouchRippleCount;
                _lastRippleTelemetryFrame = currentFrame;
            }

            if (!math.isfinite(_masterIntensity) || !math.isfinite(_globalBiolumPhase))
                dumpReason |= 2;

            if (dumpReason != 0)
                DumpBiolumTelemetry(dumpReason);
        }

        private void DumpBiolumTelemetry(byte reasonFlags)
        {
            if (!TryAcquireTelemetryRingGuard(out IDataVault telemetryVault, out NativeArray<BiolumTelemetryEntry> telemetryRing))
                return;

            try
            {
                int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
                if (frame - _lastBiolumDumpFrame < BiolumDumpCooldownFrames)
                    return;

                _lastBiolumDumpFrame = frame;
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string dumpPath = Path.Combine(projectRoot, BiolumDumpRelativePath);
                int byteCount = BiolumDumpHeaderBytes + BiolumTelemetryCapacity * BiolumDumpEntryBytes;
                const string PayloadLabel = "biolumTelemetryDumpPayload";
                NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(HectonBiolumManager),
                    PayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                try
                {
                    int cursor = 0;
                    WriteUInt32LittleEndian(payload, ref cursor, 0x42494F4Cu);
                    WriteUInt32LittleEndian(payload, ref cursor, _telemetrySequence);
                    payload[cursor++] = reasonFlags;
                    WriteInt32LittleEndian(payload, ref cursor, BiolumTelemetryCapacity);

                    for (int i = 0; i < BiolumTelemetryCapacity; i++)
                    {
                        BiolumTelemetryEntry entry = telemetryRing[i];
                        WriteUInt32LittleEndian(payload, ref cursor, entry.Frame);
                        WriteFloatLittleEndian(payload, ref cursor, entry.CameraPositionX);
                        WriteFloatLittleEndian(payload, ref cursor, entry.CameraPositionY);
                        WriteFloatLittleEndian(payload, ref cursor, entry.CameraPositionZ);
                        WriteFloatLittleEndian(payload, ref cursor, entry.Intensity);
                        WriteFloatLittleEndian(payload, ref cursor, entry.Phase);
                        WriteFloatLittleEndian(payload, ref cursor, entry.PredatorDim);
                        WriteUInt16LittleEndian(payload, ref cursor, entry.PredatorHits);
                        payload[cursor++] = entry.ActiveRipples;
                        payload[cursor++] = entry.Flags;
                    }

                    if (cursor == byteCount)
                        NativeFaultDumpWriter.TryWriteAll(dumpPath, payload, byteCount);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(HectonBiolumManager),
                        PayloadLabel);
                }
            }
            finally
            {
                ReleaseTelemetryRingGuard(telemetryVault);
            }
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> payload, ref int cursor, int value)
        {
            WriteUInt32LittleEndian(payload, ref cursor, (uint)value);
        }

        private static void WriteUInt16LittleEndian(NativeArray<byte> payload, ref int cursor, ushort value)
        {
            payload[cursor++] = (byte)value;
            payload[cursor++] = (byte)(value >> 8);
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> payload, ref int cursor, uint value)
        {
            payload[cursor++] = (byte)value;
            payload[cursor++] = (byte)(value >> 8);
            payload[cursor++] = (byte)(value >> 16);
            payload[cursor++] = (byte)(value >> 24);
        }

        private static void WriteFloatLittleEndian(NativeArray<byte> payload, ref int cursor, float value)
        {
            WriteUInt32LittleEndian(payload, ref cursor, math.asuint(value));
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

        private void RefreshCameraSnapshotHot()
        {
            TryCacheCameraReferenceCachedOnly();
            WriteCameraSnapshot(force: false);
        }

        private void RefreshCameraSnapshotCold()
        {
            TryCacheCameraReferenceCold();
            WriteCameraSnapshot(force: true);
        }

        private void WriteCameraSnapshot(bool force)
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (!force && _cachedCameraAupFrame == frame)
                return;

            _cachedCameraPosition = _cachedCameraTransform != null ? _cachedCameraTransform.position : Vector3.zero;
            if (!TryBuildAupFromRuntimeOrigin(_cachedCameraPosition, out _cachedCameraAup))
                _cachedCameraAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();

            _cachedCameraAupFrame = frame;
        }

        private bool TryCacheCameraReferenceCachedOnly()
        {
            if (_cachedCameraTransform != null)
                return true;

            return TryCacheCameraReferenceFromPlayerContext();
        }

        private bool TryCacheCameraReferenceCold()
        {
            if (_cachedCameraTransform != null)
                return true;

            if (TryCacheCameraReferenceFromPlayerContext())
                return true;

            float currentTime = SampleCameraCacheClockSeconds();
            if (currentTime < _nextCameraResolveTime)
                return false;

            _nextCameraResolveTime = currentTime + CameraResolveCooldown;

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) && playerTransform != null)
            {
                IPlayerRuntimeContext playerContext = _cachedPlayerContext;
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

        private bool TryCacheCameraReferenceFromPlayerContext()
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext == null)
                return false;

            Camera playerCamera = playerContext.PlayerCamera;
            if (playerCamera != null)
            {
                _cachedCamera = playerCamera;
                _cachedCameraTransform = playerCamera.transform;
                return true;
            }

            Transform playerTransform = playerContext.PlayerTransform;
            if (playerTransform == null)
                return false;

            _cachedCamera = null;
            _cachedCameraTransform = playerTransform;
            return true;
        }

        private float SampleCameraCacheClockSeconds()
        {
            ITickDispatcher dispatcher = _cachedTickDispatcher;
            if (dispatcher != null)
            {
                H8TimeSnapshot snapshot = dispatcher.TimeSnapshot;
                if (snapshot.UnscaledTime >= 0d && !double.IsNaN(snapshot.UnscaledTime) && !double.IsInfinity(snapshot.UnscaledTime))
                    return (float)(snapshot.UnscaledTime % 65536d);
            }

            double unscaledTime = SystemDispatcher.CurrentUnscaledTimeSeconds;
            return unscaledTime >= 0d && !double.IsNaN(unscaledTime) && !double.IsInfinity(unscaledTime)
                ? (float)(unscaledTime % 65536d)
                : 0f;
        }

        private bool TrySampleDominantZone(HectonBiolumZone[] zones, int zoneCount, in AbsoluteUniversePosition referenceAup, out Color sampledColor, out float sampledStrength)
        {
            sampledColor = Color.black;
            sampledStrength = 0f;

            int count = math.clamp(zoneCount, 0, zones.Length);
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

        private static int CollectNearbyZonesNonAlloc(HectonBiolumZone[] zones, int zoneCount, in AbsoluteUniversePosition referenceAup, float maxDistanceSq, HectonBiolumZone[] destination, float[] weights, int count)
        {
            int destinationCapacity = destination.Length;
            if (destinationCapacity == 0)
                return 0;

            int safeZoneCount = math.clamp(zoneCount, 0, zones.Length);
            for (int i = 0; i < safeZoneCount; i++)
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
            _globalBiolumPhaseDirty = false;
            _floraShaderGlobalsDirty = false;
            _touchRippleVisualDirty = false;
            _cachedOceanBiolumColor = Color.black;
            _cachedFloorBiolumColor = Color.black;
            _cachedOceanBiolumStrength = 0f;
            _cachedFloorBiolumStrength = 0f;

            PublishFloraShaderGlobals();
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
            if (IsGlobalPulseSyncOwningLegacyBiolumGlobals())
            {
                _biolumIntensitySuppressedByPulseSync = true;
            }
            else
            {
                HectonShaderGlobalDataVaultBridge.PublishBiolumMasterPhase(resetPhase);
                Shader.SetGlobalVector(_BiolumIntensityId, resetIntensity);
                _biolumIntensitySuppressedByPulseSync = false;
            }
            Shader.SetGlobalVector(_BiolumTouchRippleParamsId, Vector4.zero);
            _lastPublishedTouchRippleCount = 0;
            _lastPublishedTouchRippleQualityBucket = -1;
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

        private void CacheGlobalRegistryServicesCold()
        {
            IDataVault currentVault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_dataVault, currentVault))
                ReleaseVaultHandlesOnly();

            _dataVault = currentVault;
            _cachedTickDispatcher = GlobalRegistry.TickDispatcher;
            _cachedFluid = GlobalRegistry.AbyssalFlowGpu;
            _cachedPlayerContext = GlobalRegistry.Player;
            _cachedCelestialSnapshot = GlobalRegistry.CelestialRuntimeSnapshotReadModel;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    if (ReferenceEquals(_dataVault, currentService))
                        return;

                    ReleaseVaultHandlesOnly();
                    _dataVault = currentService as IDataVault;
                    EnsureVaultBuffers();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    _cachedTickDispatcher = currentService as ITickDispatcher;
                    break;
                case GlobalRegistryServiceSlot.FluidRuntime:
                    _cachedFluid = currentService as IAbyssalFlowGpuReadModel;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                    _cachedCamera = null;
                    _cachedCameraTransform = null;
                    _cachedCameraPosition = Vector3.zero;
                    _cachedCameraAupFrame = -1;
                    break;
                case GlobalRegistryServiceSlot.CelestialEngineRuntime:
                    _cachedCelestialSnapshot = GlobalRegistry.CelestialRuntimeSnapshotReadModel;
                    break;
            }
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

            TryUnregisterHotSwapListener();
            TryUnregisterLateFrameTick();
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

            _tickRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = false;
        }

        private void TryRegisterLateFrameTick()
        {
            if (_lateFrameRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrameTick()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _lateFrameRegistered = false;
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
