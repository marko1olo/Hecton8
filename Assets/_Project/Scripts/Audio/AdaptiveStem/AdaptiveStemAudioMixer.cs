using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.Audio
{
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct AudioStemStateDTO
    {
        [FieldOffset(0)]
        public float TensionIndex;
        [FieldOffset(4)]
        public float DepthFilter;
        [FieldOffset(8)]
        public uint ActiveStemHash;
        [FieldOffset(12)] private byte _pad0;
        [FieldOffset(13)] private byte _pad1;
        [FieldOffset(14)] private byte _pad2;
        [FieldOffset(15)] private byte _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct StemCommandDTO
    {
        [FieldOffset(0)]
        public uint StemHash_A;
        [FieldOffset(4)]
        public float Volume_A;
        [FieldOffset(8)]
        public uint StemHash_B;
        [FieldOffset(12)]
        public float Volume_B;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct MockPredatorProximitySignal
    {
        [FieldOffset(0)] public float ProximityMeters;
        [FieldOffset(4)] public float Proximity01;
        [FieldOffset(8)] public float OscillationPhase;
        [FieldOffset(12)] public float DamageSpike01;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint SourceHash;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] private byte _pad0;
        [FieldOffset(29)] private byte _pad1;
        [FieldOffset(30)] private byte _pad2;
        [FieldOffset(31)] private byte _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct MockDepthSignal
    {
        [FieldOffset(0)] public float DepthMeters;
        [FieldOffset(4)] public float Depth01;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public partial struct MockTensionSignal
    {
        [FieldOffset(0)] public float Tension01;
        [FieldOffset(4)] public float Damage01;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct AudioStemRuleDTO
    {
        [FieldOffset(0)] public ulong NarrativeStateMask;
        [FieldOffset(8)] public float AttackSeconds;
        [FieldOffset(12)] public float ReleaseSeconds;
        [FieldOffset(16)] public float CrossfadeSeconds;
        [FieldOffset(20)] public float BeatBpm;
        [FieldOffset(24)] public float BeatWindowSeconds;
        [FieldOffset(28)] public float DepthMinMeters;
        [FieldOffset(32)] public float DepthMaxMeters;
        [FieldOffset(36)] public float DepthFilterMinHz;
        [FieldOffset(40)] public float DepthFilterMaxHz;
        [FieldOffset(44)] public float CombatEnterThreshold;
        [FieldOffset(48)] public float CombatExitThreshold;
        [FieldOffset(52)] public float NarrativeOverrideWeight;
        [FieldOffset(56)] public float GlobalQualityWeight;
        [FieldOffset(60)] public float SystemHealth01;
        [FieldOffset(64)] public float IoPressure01;
        [FieldOffset(68)] public float Damage01;
        [FieldOffset(72)] public float OxygenDanger01;
        [FieldOffset(76)] public float MockPredatorMeters;
        [FieldOffset(80)] public float MockDepthMeters;
        [FieldOffset(84)] public float BiomeFadeSeconds;
        [FieldOffset(88)] public float MockPhaseSeconds;
        [FieldOffset(92)] public float KernelCadenceSeconds;
        [FieldOffset(96)] public float GroupBlend01;
        [FieldOffset(100)] public uint CurrentBiomeHash;
        [FieldOffset(104)] public uint TargetBiomeHash;
        [FieldOffset(108)] public uint StemBaseHash;
        [FieldOffset(112)] public uint StemActionHash;
        [FieldOffset(116)] public uint StemDepthHash;
        [FieldOffset(120)] public uint StemBossHash;
        [FieldOffset(124)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct StemMixFrameDTO
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ActiveStemHash;
        [FieldOffset(8)] public uint BiomeHash;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float TensionIndex;
        [FieldOffset(20)] public float DepthFilter;
        [FieldOffset(24)] public float CutoffHz;
        [FieldOffset(28)] public float QualityWeight;
        [FieldOffset(32)] public float BaseVolume;
        [FieldOffset(36)] public float ActionVolume;
        [FieldOffset(40)] public float DepthVolume;
        [FieldOffset(44)] public float BossVolume;
        [FieldOffset(48)] public float BeatPhase01;
        [FieldOffset(52)] public float IoPressure01;
        [FieldOffset(56)] public float GroupBlend01;
        [FieldOffset(60)] private byte _pad0;
        [FieldOffset(61)] private byte _pad1;
        [FieldOffset(62)] private byte _pad2;
        [FieldOffset(63)] private byte _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AudioStemTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ActiveStemHash;
        [FieldOffset(8)] public uint BiomeHash;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float TensionIndex;
        [FieldOffset(20)] public float DepthFilter;
        [FieldOffset(24)] public float CutoffHz;
        [FieldOffset(28)] public float MixerUpdateMicroseconds;
        [FieldOffset(32)] public float BaseVolume;
        [FieldOffset(36)] public float ActionVolume;
        [FieldOffset(40)] public float DepthVolume;
        [FieldOffset(44)] public float BossVolume;
        [FieldOffset(48)] public float QualityWeight;
        [FieldOffset(52)] public float BeatPhase01;
        [FieldOffset(56)] public float IoPressure01;
        [FieldOffset(60)] public float UpdateCadenceHz;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Audio/Adaptive Stem Audio Mixer")]
    public sealed unsafe class AdaptiveStemAudioMixer : MonoBehaviour, IColdTickable, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001AdaptiveStemAudioMixerSignalPushDropCount;
        private const int TelemetryCapacity = 300;
#if UNITY_EDITOR
        private const int CsvScratchBytes = 4096;
#endif
        private const float DefaultAttackSeconds = 0.1f;
        private const float DefaultReleaseSeconds = 15f;
        private const float DefaultCrossfadeSeconds = 2f;
        private const float DefaultBeatBpm = 88f;
        private const float DefaultBeatWindowSeconds = 0.05f;
        private const float DefaultDepthMinMeters = 0f;
        private const float DefaultDepthMaxMeters = 1200f;
        private const float DefaultDepthFilterMinHz = 800f;
        private const float DefaultDepthFilterMaxHz = 22000f;
        private const float DefaultBiomeFadeSeconds = 10f;
        private const float MixerDumpThresholdMicroseconds = 1000f;
        private const float MinAudioDeltaSeconds = 0.0001f;
        private const float MaxAudioDeltaSeconds = 0.25f;
        private const float KernelCadenceEpsilonSeconds = 0.001f;
        private const float MinBpm = 24f;
        private const float MaxBpm = 240f;
        private const uint StemBaseHash = 0xB4510A10u;
        private const uint StemActionHash = 0xAC710A10u;
        private const uint StemDepthHash = 0xDE970A10u;
        private const uint StemBossHash = 0xB0550A10u;
        private const uint DefaultBiomeHash = 0x5348494Eu;
        private const uint FlagBeatGateOpen = 1u << 0;
        private const uint FlagNarrativeOverride = 1u << 1;
        private const uint FlagIoTransitionDelay = 1u << 2;
        private const uint FlagClipNotStreaming = 1u << 3;
        private const uint FlagNonFinite = 1u << 4;
        public const uint TelemetryFlagCelestialLightBound = 1u << 5;
        public const uint TelemetryFlagCelestialLightMissing = 1u << 6;
        public const uint TelemetryFlagCelestialLightFallback = 1u << 7;
        public const uint TelemetryFlagCelestialLightAbyssCritical = 1u << 8;
        public const uint TelemetryFlagCelestialLightQualityReduced = 1u << 9;
        public const uint TelemetryFlagCelestialLightTwilight = 1u << 10;
        public const uint TelemetryFlagCelestialLightNight = 1u << 11;
        private const ulong DefaultBossNarrativeMask = 1ul << 7;
        private static readonly bool ProceduralSynthOwnsStemTransport = true;
        private const SystemID VaultOwner = SystemID.AudioStemMixer;
        private static readonly ulong AudioStemRulesMutationGuardMask = AdaptiveStemMutationGuardBit(BufferID.AudioStemRules);
        private static readonly ulong AudioStemFrameMutationGuardMask =
            AdaptiveStemMutationGuardBit(BufferID.AudioStemState) |
            AdaptiveStemMutationGuardBit(BufferID.AudioStemCommands) |
            AdaptiveStemMutationGuardBit(BufferID.AudioStemMixFrame) |
            AudioStemRulesMutationGuardMask |
            AdaptiveStemMutationGuardBit(BufferID.AudioStemMockPredator) |
            AdaptiveStemMutationGuardBit(BufferID.AudioStemMockDepth) |
            AdaptiveStemMutationGuardBit(BufferID.AudioStemMockTension) |
            AdaptiveStemMutationGuardBit(BufferID.AudioStemTelemetry) |
            AdaptiveStemMutationGuardBit(BufferID.AudioStemTelemetryCursor);
#if UNITY_EDITOR
        private const int CsvPollSlowTickInterval = 2;
        private const string CsvDefaultRelativePath = "Docs/Audio/audio_stem_rules.csv";
#endif
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_STEM_MIXER.bin";

#if UNITY_EDITOR
        private const uint CsvAttackSecondsHash = 0x02EDD0B3u;
        private const uint CsvReleaseSecondsHash = 0x55F57658u;
        private const uint CsvCrossfadeSecondsHash = 0x8CA0446Bu;
        private const uint CsvBeatBpmHash = 0x4EE90943u;
        private const uint CsvBeatWindowSecondsHash = 0x8C034044u;
        private const uint CsvDepthMinMetersHash = 0x671D3B50u;
        private const uint CsvDepthMaxMetersHash = 0x5003D7DEu;
        private const uint CsvDepthFilterMinHzHash = 0xE0733819u;
        private const uint CsvDepthFilterMaxHzHash = 0xB249DFC3u;
        private const uint CsvCombatEnterHash = 0xF682A6ACu;
        private const uint CsvCombatExitHash = 0xBA0F98D2u;
        private const uint CsvNarrativeOverrideWeightHash = 0x1634D39Fu;
        private const uint CsvBiomeFadeSecondsHash = 0x578347A4u;
#endif

        private static AdaptiveStemAudioMixer _activeInstance;

        [Header("Streaming Stem Sources")]
        [Tooltip("Looping streaming exploration/base music stem.")]
        [SerializeField] private AudioSource _baseStemSource;
        [Tooltip("Looping streaming action/combat music stem.")]
        [SerializeField] private AudioSource _actionStemSource;
        [Tooltip("Looping streaming depth-dread music stem.")]
        [SerializeField] private AudioSource _depthStemSource;
        [Tooltip("Looping streaming narrative/boss music stem.")]
        [SerializeField] private AudioSource _bossStemSource;

        [Header("Depth Fake Filters")]
        [SerializeField] private AudioLowPassFilter _baseLowPassFilter;
        [SerializeField] private AudioLowPassFilter _actionLowPassFilter;
        [SerializeField] private AudioLowPassFilter _depthLowPassFilter;
        [SerializeField] private AudioLowPassFilter _bossLowPassFilter;

#if UNITY_EDITOR
        [Header("Cold Tuning")]
        [SerializeField] private string _csvRelativePath = CsvDefaultRelativePath;
#endif
        [SerializeField, Range(0f, 1f)] private float _mockDamage01;
        [SerializeField, Range(0f, 1f)] private float _mockOxygenDanger01;
        [SerializeField, Range(0f, 1f)] private float _mockQualityBias01 = 1f;
        [SerializeField, Min(0f)] private float _mockDepthAmplitudeMeters = 1200f;
        [SerializeField, Min(1f)] private float _mockPredatorCycleSeconds = 18f;
        [SerializeField] private ulong _bossNarrativeMask = DefaultBossNarrativeMask;
        [SerializeField] private bool _autoStartSources = true;

        private ref struct AdaptiveStemVaultViews
        {
            public NativeArray<AudioStemStateDTO> StemState;
            public NativeArray<StemCommandDTO> StemCommands;
            public NativeArray<StemMixFrameDTO> MixFrame;
            public NativeArray<AudioStemRuleDTO> Rules;
            public NativeArray<MockPredatorProximitySignal> MockPredator;
            public NativeArray<MockDepthSignal> MockDepth;
            public NativeArray<MockTensionSignal> MockTension;
            public NativeArray<AudioStemTelemetryEntry> TelemetryRing;
            public NativeArray<int> TelemetryCursor;
        }

        private IDataVault _dataVault;
        private ICelestialLightReadabilityReadModel _celestialLightReadModel;
        private VaultGenerationHandle<AudioStemStateDTO> _stemStateHandle;
        private VaultGenerationHandle<StemCommandDTO> _stemCommandsHandle;
        private VaultGenerationHandle<StemMixFrameDTO> _mixFrameHandle;
        private VaultGenerationHandle<AudioStemRuleDTO> _rulesHandle;
        private VaultGenerationHandle<MockPredatorProximitySignal> _mockPredatorHandle;
        private VaultGenerationHandle<MockDepthSignal> _mockDepthHandle;
        private VaultGenerationHandle<MockTensionSignal> _mockTensionHandle;
        private VaultGenerationHandle<AudioStemTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<ScalabilityStateDTO> _scalabilityStateHandle;
#if UNITY_EDITOR
        private string _resolvedCsvPath;
        private string _lastResolvedCsvRelativePath;
        private DateTime _lastCsvWriteUtc;
#endif
        private float _beatTimerSeconds;
        private float _kernelAccumulatorSeconds;
        private float _biomeBlend01;
        private float _ioDelaySeconds;
        private float _lastSystemHealth01 = 1f;
        private float _lastIoPressure01;
        private float _lastDamage01;
        private float _lastOxygenDanger01;
        private float _cachedGlobalQualityWeight = 1f;
        private uint _playerSurvivalVitalsSourceId;
        private uint _currentBiomeHash = DefaultBiomeHash;
        private uint _targetBiomeHash = DefaultBiomeHash;
        private ulong _narrativeStateMask;
        private int _registeredUpdate;
        private int _registeredLateFrame;
        private int _registeredColdTick;
        private int _registeredHotSwap;
        private int _nativeAllocated;
        private int _telemetryDumpRequested;
        private int _telemetryDumped;
#if UNITY_EDITOR
        private int _csvPollCountdown;
#endif
        private uint _simulationFrameCounter;
        private uint _streamingFaultFlags;
        private StemMixFrameDTO _pendingUnityMixFrame;
        private AudioStemRuleDTO _pendingUnityMixRule;
        private int _pendingUnityMixFrameDirty;

        public static bool TryGetActive(out AdaptiveStemAudioMixer mixer)
        {
            mixer = _activeInstance;
            return mixer != null && mixer._nativeAllocated != 0;
        }

        private void Awake()
        {
            CacheDataVaultCold();
            CacheCelestialLightReadabilityCold();
            CachePlayerSurvivalVitalsSourceIdCold();
            EnsureVaultStorage();
#if UNITY_EDITOR
            RefreshCsvPathCold();
#endif
            ConfigureSourcesCold();
            if (!ScanLegacyBinaryProfilesCold())
                GenerateEmergencyMockAudioProfiles();
        }

        private void OnEnable()
        {
            CacheDataVaultCold();
            CacheCelestialLightReadabilityCold();
            CachePlayerSurvivalVitalsSourceIdCold();
            EnsureVaultStorage();
#if UNITY_EDITOR
            RefreshCsvPathCold();
#endif
            ConfigureSourcesCold();
            _activeInstance = this;
            if (GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment))
                _registeredUpdate = 1;
            if (GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment))
                _registeredLateFrame = 1;
            if (GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment))
                _registeredColdTick = 1;
            if (GlobalRegistry.TryRegisterHotSwapListener(this))
                _registeredHotSwap = 1;
        }

        private void OnDisable()
        {
            UnregisterRuntime();
        }

        private void OnDestroy()
        {
            UnregisterRuntime();
            DisposeVaultStorage();
        }

        public void Tick(float deltaTime)
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return;

            float safeDelta = math.clamp(deltaTime, MinAudioDeltaSeconds, MaxAudioDeltaSeconds);
            unchecked
            {
                _simulationFrameCounter++;
                if (_simulationFrameCounter == 0u)
                    _simulationFrameCounter = 1u;
            }

            DrainSignalInputs();
            if (!TryAcquireStemFrameMutationView(out AdaptiveStemVaultViews views, out IDataVault guardVault))
                return;

            try
            {
                UpdateBeatAndBiomeState(ref views, safeDelta);
                UpdateVaultRulesFromManagedState(ref views, safeDelta);
                RunAudioKernels(ref views);
            }
            finally
            {
                ReleaseAdaptiveStemMutationGuard(guardVault, AudioStemFrameMutationGuardMask);
            }

            if (Interlocked.Exchange(ref _telemetryDumpRequested, 0) != 0)
                DumpTelemetryOnce();
        }

        public void LateFrameTick()
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return;

            FlushPendingUnityMixFrame();
        }

        public void ColdTick()
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
                EnsureVaultStorage();

            TryRefreshScalabilityStateHandleCold();
            RefreshGlobalQualitySnapshotCold();
            CachePlayerSurvivalVitalsSourceIdCold();
#if UNITY_EDITOR
            PollCsvRulesCold();
#endif
            ValidateStreamingClipsCold();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.CelestialEngineRuntime)
                CacheCelestialLightReadModel(currentService as ICelestialLightReadabilityReadModel);

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                RefreshPlayerSurvivalVitalsSourceId(currentService as IPlayerRuntimeContext);
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            IDataVault nextVault = currentService is IDataVault vault ? vault : null;
            RebindDataVaultCold(nextVault);

            EnsureVaultStorage();
            TryRefreshScalabilityStateHandleCold();
            RefreshGlobalQualitySnapshotCold();
            _ = previousService;
        }

        public bool TryGetEditorRule(out AudioStemRuleDTO rule)
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
            {
                rule = default;
                return false;
            }

            IDataVault vault = _dataVault;
            if (vault == null ||
                !vault.TryReadOnlyHandle(in _rulesHandle, out NativeArray<AudioStemRuleDTO>.ReadOnly rules) ||
                !rules.IsCreated ||
                rules.Length <= 0)
            {
                rule = default;
                return false;
            }

            rule = rules[0];
            return true;
        }

        public bool TryWriteEditorRule(in AudioStemRuleDTO rule)
        {
            return Volatile.Read(ref _nativeAllocated) != 0 &&
                   TryWriteRuleForOwnerRoute(in rule);
        }

        private bool TryReadRuleForOwnerRoute(out AudioStemRuleDTO rule)
        {
            rule = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                !vault.TryReadOnlyHandle(in _rulesHandle, out NativeArray<AudioStemRuleDTO>.ReadOnly rules) ||
                !rules.IsCreated ||
                rules.Length <= 0)
            {
                return false;
            }

            rule = rules[0];
            return true;
        }

        private bool TryWriteRuleForOwnerRoute(in AudioStemRuleDTO rule)
        {
            if (!TryAcquireRuleMutationView(out NativeArray<AudioStemRuleDTO> rules, out IDataVault guardVault))
            {
                return false;
            }

            try
            {
                if (!rules.IsCreated || rules.Length <= 0)
                    return false;

                rules[0] = SanitizeRule(rule);
                return true;
            }
            finally
            {
                ReleaseAdaptiveStemMutationGuard(guardVault, AudioStemRulesMutationGuardMask);
            }
        }

        public bool TryGetEditorMixFrame(out StemMixFrameDTO frame)
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
            {
                frame = default;
                return false;
            }

            IDataVault vault = _dataVault;
            if (vault == null ||
                !vault.TryReadOnlyHandle(in _mixFrameHandle, out NativeArray<StemMixFrameDTO>.ReadOnly mixFrame) ||
                !mixFrame.IsCreated ||
                mixFrame.Length <= 0)
            {
                frame = default;
                return false;
            }

            frame = mixFrame[0];
            return true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only access to assigned stem clips for import-policy repair.
        /// </summary>
        public bool TryGetEditorStemClip(int index, out AudioClip clip)
        {
            AudioSource source = null;
            if (index == 0)
                source = _baseStemSource;
            else if (index == 1)
                source = _actionStemSource;
            else if (index == 2)
                source = _depthStemSource;
            else if (index == 3)
                source = _bossStemSource;

            clip = source != null ? source.clip : null;
            return clip != null;
        }
#endif

        public bool TryGetEditorTelemetry(int offsetFromNewest, out AudioStemTelemetryEntry entry)
        {
            entry = default;
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !vault.TryReadOnlyHandle(in _telemetryRingHandle, out NativeArray<AudioStemTelemetryEntry>.ReadOnly telemetryRing) ||
                !vault.TryReadOnlyHandle(in _telemetryCursorHandle, out NativeArray<int>.ReadOnly telemetryCursor) ||
                !telemetryRing.IsCreated ||
                !telemetryCursor.IsCreated ||
                telemetryRing.Length <= 0 ||
                telemetryCursor.Length <= 0)
                return false;

            int cursor = math.max(0, telemetryCursor[0]);
            int capacity = math.min(TelemetryCapacity, telemetryRing.Length);
            int offset = math.clamp(offsetFromNewest, 0, capacity - 1);
            int index = cursor - 1 - offset;
            while (index < 0)
                index += capacity;

            entry = telemetryRing[index % capacity];
            return true;
        }

        private void UnregisterRuntime()
        {
            if (ReferenceEquals(_activeInstance, this))
                _activeInstance = null;
            if (Interlocked.Exchange(ref _registeredHotSwap, 0) != 0)
                GlobalRegistry.TryUnregisterHotSwapListener(this);
            if (Interlocked.Exchange(ref _registeredColdTick, 0) != 0)
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
            if (Interlocked.Exchange(ref _registeredLateFrame, 0) != 0)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            if (Interlocked.Exchange(ref _registeredUpdate, 0) != 0)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _celestialLightReadModel = null;
            _playerSurvivalVitalsSourceId = 0u;
        }

        private void CacheDataVaultCold()
        {
            RebindDataVaultCold(GlobalRegistry.DataVault);
        }

        private void CacheCelestialLightReadabilityCold()
        {
            CacheCelestialLightReadModel(GlobalRegistry.CelestialLightReadabilityReadModel);
        }

        private void CacheCelestialLightReadModel(ICelestialLightReadabilityReadModel readModel)
        {
            if (IsCelestialLightReadModelUsable(readModel))
            {
                _celestialLightReadModel = readModel;
                return;
            }

            ICelestialLightReadabilityReadModel fallback = GlobalRegistry.CelestialLightReadabilityReadModel;
            _celestialLightReadModel = IsCelestialLightReadModelUsable(fallback) ? fallback : null;
        }

        private void RebindDataVaultCold(IDataVault nextVault)
        {
            if (ReferenceEquals(_dataVault, nextVault))
                return;

            if (_dataVault != null)
                DisposeVaultStorage();

            _dataVault = nextVault;
        }

        private void EnsureVaultStorage()
        {
            IDataVault vault = _dataVault;
            if (Volatile.Read(ref _nativeAllocated) != 0)
            {
                if (AreStemVaultBuffersCreated(vault))
                    return;

                DisposeVaultStorage(vault);
                _dataVault = vault;
            }

            vault = _dataVault;
            if (vault == null)
                return;

            _dataVault = vault;
            _stemStateHandle = vault.EnsureGenerationHandle<AudioStemStateDTO>(
                BufferID.AudioStemState,
                1,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _stemCommandsHandle = vault.EnsureGenerationHandle<StemCommandDTO>(
                BufferID.AudioStemCommands,
                2,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _mixFrameHandle = vault.EnsureGenerationHandle<StemMixFrameDTO>(
                BufferID.AudioStemMixFrame,
                1,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _rulesHandle = vault.EnsureGenerationHandle<AudioStemRuleDTO>(
                BufferID.AudioStemRules,
                1,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _mockPredatorHandle = vault.EnsureGenerationHandle<MockPredatorProximitySignal>(
                BufferID.AudioStemMockPredator,
                1,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _mockDepthHandle = vault.EnsureGenerationHandle<MockDepthSignal>(
                BufferID.AudioStemMockDepth,
                1,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _mockTensionHandle = vault.EnsureGenerationHandle<MockTensionSignal>(
                BufferID.AudioStemMockTension,
                1,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _telemetryRingHandle = vault.EnsureGenerationHandle<AudioStemTelemetryEntry>(
                BufferID.AudioStemTelemetry,
                TelemetryCapacity,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _telemetryCursorHandle = vault.EnsureGenerationHandle<int>(
                BufferID.AudioStemTelemetryCursor,
                1,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);

            if (!TryAcquireStemFrameMutationView(vault, out AdaptiveStemVaultViews views, out IDataVault guardVault))
            {
                DisposeVaultStorage(vault);
                return;
            }

            try
            {
                MemClearArray(views.StemState);
                MemClearArray(views.StemCommands);
                MemClearArray(views.MixFrame);
                MemClearArray(views.Rules);
                MemClearArray(views.MockPredator);
                MemClearArray(views.MockDepth);
                MemClearArray(views.MockTension);
                MemClearArray(views.TelemetryRing);
                MemClearArray(views.TelemetryCursor);
            }
            finally
            {
                ReleaseAdaptiveStemMutationGuard(guardVault, AudioStemFrameMutationGuardMask);
            }

            TryRefreshScalabilityStateHandleCold();
            RefreshGlobalQualitySnapshotCold();
            Interlocked.Exchange(ref _telemetryDumpRequested, 0);
            Interlocked.Exchange(ref _telemetryDumped, 0);
            Volatile.Write(ref _nativeAllocated, 1);
        }

        private void DisposeVaultStorage()
        {
            DisposeVaultStorage(_dataVault);
        }

        private void DisposeVaultStorage(IDataVault vault)
        {
            ReleaseVaultBuffer(vault, ref _stemStateHandle, BufferID.AudioStemState);
            ReleaseVaultBuffer(vault, ref _stemCommandsHandle, BufferID.AudioStemCommands);
            ReleaseVaultBuffer(vault, ref _mixFrameHandle, BufferID.AudioStemMixFrame);
            ReleaseVaultBuffer(vault, ref _rulesHandle, BufferID.AudioStemRules);
            ReleaseVaultBuffer(vault, ref _mockPredatorHandle, BufferID.AudioStemMockPredator);
            ReleaseVaultBuffer(vault, ref _mockDepthHandle, BufferID.AudioStemMockDepth);
            ReleaseVaultBuffer(vault, ref _mockTensionHandle, BufferID.AudioStemMockTension);
            ReleaseVaultBuffer(vault, ref _telemetryRingHandle, BufferID.AudioStemTelemetry);
            ReleaseVaultBuffer(vault, ref _telemetryCursorHandle, BufferID.AudioStemTelemetryCursor);
            _scalabilityStateHandle = default;
#if UNITY_EDITOR
            _resolvedCsvPath = null;
            _lastResolvedCsvRelativePath = null;
#endif
            _dataVault = null;
            Volatile.Write(ref _nativeAllocated, 0);
        }

        private static void ReleaseVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID expectedBufferId)
            where T : struct
        {
            if (vault != null && IsAdaptiveStemVaultHandle(in handle, expectedBufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsAdaptiveStemVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId)
            where T : struct
        {
            return handle.BufferID == (uint)expectedBufferId &&
                   handle.SystemID == (uint)VaultOwner &&
                   handle.Generation != 0u;
        }

        private bool AreAdaptiveStemVaultHandlesExact()
        {
            return IsAdaptiveStemVaultHandle(in _stemStateHandle, BufferID.AudioStemState) &&
                   IsAdaptiveStemVaultHandle(in _stemCommandsHandle, BufferID.AudioStemCommands) &&
                   IsAdaptiveStemVaultHandle(in _mixFrameHandle, BufferID.AudioStemMixFrame) &&
                   IsAdaptiveStemVaultHandle(in _rulesHandle, BufferID.AudioStemRules) &&
                   IsAdaptiveStemVaultHandle(in _mockPredatorHandle, BufferID.AudioStemMockPredator) &&
                   IsAdaptiveStemVaultHandle(in _mockDepthHandle, BufferID.AudioStemMockDepth) &&
                   IsAdaptiveStemVaultHandle(in _mockTensionHandle, BufferID.AudioStemMockTension) &&
                   IsAdaptiveStemVaultHandle(in _telemetryRingHandle, BufferID.AudioStemTelemetry) &&
                   IsAdaptiveStemVaultHandle(in _telemetryCursorHandle, BufferID.AudioStemTelemetryCursor);
        }

        private bool AreStemVaultBuffersCreated(IDataVault vault)
        {
            if (vault == null || !AreAdaptiveStemVaultHandlesExact())
                return false;

            if (!vault.TryReadOnlyHandle(in _stemStateHandle, out NativeArray<AudioStemStateDTO>.ReadOnly stemState) ||
                !vault.TryReadOnlyHandle(in _stemCommandsHandle, out NativeArray<StemCommandDTO>.ReadOnly stemCommands) ||
                !vault.TryReadOnlyHandle(in _mixFrameHandle, out NativeArray<StemMixFrameDTO>.ReadOnly mixFrame) ||
                !vault.TryReadOnlyHandle(in _rulesHandle, out NativeArray<AudioStemRuleDTO>.ReadOnly rules) ||
                !vault.TryReadOnlyHandle(in _mockPredatorHandle, out NativeArray<MockPredatorProximitySignal>.ReadOnly mockPredator) ||
                !vault.TryReadOnlyHandle(in _mockDepthHandle, out NativeArray<MockDepthSignal>.ReadOnly mockDepth) ||
                !vault.TryReadOnlyHandle(in _mockTensionHandle, out NativeArray<MockTensionSignal>.ReadOnly mockTension) ||
                !vault.TryReadOnlyHandle(in _telemetryRingHandle, out NativeArray<AudioStemTelemetryEntry>.ReadOnly telemetryRing) ||
                !vault.TryReadOnlyHandle(in _telemetryCursorHandle, out NativeArray<int>.ReadOnly telemetryCursor) ||
                !stemState.IsCreated ||
                !stemCommands.IsCreated ||
                !mixFrame.IsCreated ||
                !rules.IsCreated ||
                !mockPredator.IsCreated ||
                !mockDepth.IsCreated ||
                !mockTension.IsCreated ||
                !telemetryRing.IsCreated ||
                !telemetryCursor.IsCreated ||
                stemState.Length <= 0 ||
                stemCommands.Length < 2 ||
                mixFrame.Length <= 0 ||
                rules.Length <= 0 ||
                mockPredator.Length <= 0 ||
                mockDepth.Length <= 0 ||
                mockTension.Length <= 0 ||
                telemetryRing.Length <= 0 ||
                telemetryCursor.Length <= 0)
            {
                return false;
            }

            return true;
        }

        private bool TryResolveStemOwnerViews(out AdaptiveStemVaultViews views)
        {
            return TryResolveStemOwnerViews(_dataVault, out views);
        }

        private bool TryResolveStemOwnerViews(IDataVault vault, out AdaptiveStemVaultViews views)
        {
            views = default;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !AreAdaptiveStemVaultHandlesExact())
                return false;

            if (!vault.TryResolveHandle(in _stemStateHandle, out views.StemState) ||
                !vault.TryResolveHandle(in _stemCommandsHandle, out views.StemCommands) ||
                !vault.TryResolveHandle(in _mixFrameHandle, out views.MixFrame) ||
                !vault.TryResolveHandle(in _rulesHandle, out views.Rules) ||
                !vault.TryResolveHandle(in _mockPredatorHandle, out views.MockPredator) ||
                !vault.TryResolveHandle(in _mockDepthHandle, out views.MockDepth) ||
                !vault.TryResolveHandle(in _mockTensionHandle, out views.MockTension) ||
                !vault.TryResolveHandle(in _telemetryRingHandle, out views.TelemetryRing) ||
                !vault.TryResolveHandle(in _telemetryCursorHandle, out views.TelemetryCursor))
            {
                views = default;
                return false;
            }

            bool success =
                views.StemState.IsCreated &&
                views.StemCommands.IsCreated &&
                views.MixFrame.IsCreated &&
                views.Rules.IsCreated &&
                views.MockPredator.IsCreated &&
                views.MockDepth.IsCreated &&
                views.MockTension.IsCreated &&
                views.TelemetryRing.IsCreated &&
                views.TelemetryCursor.IsCreated &&
                views.StemState.Length > 0 &&
                views.StemCommands.Length >= 2 &&
                views.MixFrame.Length > 0 &&
                views.Rules.Length > 0 &&
                views.MockPredator.Length > 0 &&
                views.MockDepth.Length > 0 &&
                views.MockTension.Length > 0 &&
                views.TelemetryRing.Length > 0 &&
                views.TelemetryCursor.Length > 0;
            if (!success)
            {
                views = default;
                return false;
            }

            return true;
        }

        private bool TryAcquireStemFrameMutationView(out AdaptiveStemVaultViews views, out IDataVault guardVault)
        {
            return TryAcquireStemFrameMutationView(_dataVault, out views, out guardVault);
        }

        private bool TryAcquireStemFrameMutationView(IDataVault vault, out AdaptiveStemVaultViews views, out IDataVault guardVault)
        {
            views = default;
            guardVault = vault;
            if (guardVault == null ||
                !AreAdaptiveStemVaultHandlesExact() ||
                guardVault.IsCompactionFenceActive ||
                !guardVault.TryAcquireMutationGuard(AudioStemFrameMutationGuardMask))
            {
                guardVault = null;
                return false;
            }

            bool acquired = true;
            try
            {
                if (guardVault.IsCompactionFenceActive ||
                    !TryResolveStemOwnerViews(guardVault, out views))
                {
                    return false;
                }

                acquired = false;
                return true;
            }
            finally
            {
                if (acquired)
                {
                    ReleaseAdaptiveStemMutationGuard(guardVault, AudioStemFrameMutationGuardMask);
                    views = default;
                    guardVault = null;
                }
            }
        }

        private bool TryAcquireRuleMutationView(out NativeArray<AudioStemRuleDTO> rules, out IDataVault guardVault)
        {
            rules = default;
            guardVault = _dataVault;
            if (guardVault == null ||
                !IsAdaptiveStemVaultHandle(in _rulesHandle, BufferID.AudioStemRules) ||
                guardVault.IsCompactionFenceActive ||
                !guardVault.TryAcquireMutationGuard(AudioStemRulesMutationGuardMask))
            {
                guardVault = null;
                return false;
            }

            bool acquired = true;
            try
            {
                if (guardVault.IsCompactionFenceActive ||
                    !guardVault.TryResolveHandle(in _rulesHandle, out rules) ||
                    !rules.IsCreated ||
                    rules.Length <= 0)
                {
                    return false;
                }

                acquired = false;
                return true;
            }
            finally
            {
                if (acquired)
                {
                    ReleaseAdaptiveStemMutationGuard(guardVault, AudioStemRulesMutationGuardMask);
                    rules = default;
                    guardVault = null;
                }
            }
        }

        private static void ReleaseAdaptiveStemMutationGuard(IDataVault guardVault, ulong mutationGuardMask)
        {
            guardVault?.ReleaseMutationGuard(mutationGuardMask);
        }

        private static ulong AdaptiveStemMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private void ConfigureSourcesCold()
        {
            if (ProceduralSynthOwnsStemTransport)
                EnsureDynamicMusicSignalLaneCold();

            ConfigureSourceCold(_baseStemSource);
            ConfigureSourceCold(_actionStemSource);
            ConfigureSourceCold(_depthStemSource);
            ConfigureSourceCold(_bossStemSource);
            ValidateStreamingClipsCold();
        }

        private void TryRefreshScalabilityStateHandleCold()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                _scalabilityStateHandle = default;
                return;
            }

            if (!vault.TryGetGenerationHandle<ScalabilityStateDTO>(BufferID.ShinobuScalabilityState, out _scalabilityStateHandle))
            {
                _scalabilityStateHandle = default;
                return;
            }
        }

        private void RefreshGlobalQualitySnapshotCold()
        {
            if (!TryResolveScalabilityState(out NativeArray<ScalabilityStateDTO>.ReadOnly scalabilityState) ||
                scalabilityState.Length == 0)
                return;

            ScalabilityStateDTO state = scalabilityState[0];
            if (math.isfinite(state.GlobalQualityWeight))
                _cachedGlobalQualityWeight = math.saturate(state.GlobalQualityWeight);
        }

        private void CachePlayerSurvivalVitalsSourceIdCold()
        {
            RefreshPlayerSurvivalVitalsSourceId(GlobalRegistry.Player);
        }

        private void RefreshPlayerSurvivalVitalsSourceId(IPlayerRuntimeContext playerContext)
        {
            HectonSurvivalSystem survival = playerContext != null && playerContext.IsInitialized
                ? playerContext.SurvivalSystem
                : null;
            _playerSurvivalVitalsSourceId = survival != null
                ? RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(survival.GetEntityId()))
                : 0u;
        }

        private void ConfigureSourceCold(AudioSource source)
        {
            if (source == null)
                return;

            if (ProceduralSynthOwnsStemTransport)
            {
                source.Stop();
                source.clip = null;
                source.playOnAwake = false;
                source.loop = false;
                source.volume = 0f;
                source.enabled = false;
                return;
            }

            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = 0f;
        }

        private void ValidateStreamingClipsCold()
        {
            _streamingFaultFlags = 0u;
            if (ProceduralSynthOwnsStemTransport)
                return;

            ValidateStreamingClipCold(_baseStemSource);
            ValidateStreamingClipCold(_actionStemSource);
            ValidateStreamingClipCold(_depthStemSource);
            ValidateStreamingClipCold(_bossStemSource);
        }

        private void ValidateStreamingClipCold(AudioSource source)
        {
            if (source == null || source.clip == null)
                return;

            if (source.clip.loadType == AudioClipLoadType.Streaming)
                return;

            _streamingFaultFlags |= FlagClipNotStreaming;
        }

        private bool ScanLegacyBinaryProfilesCold()
        {
            string repoRoot = ResolveRepoRootPath();
            string archiveStem = Path.Combine(repoRoot, "Docs", "Archive", "music_stem_bpm.h8bin");
            string archiveCurve = Path.Combine(repoRoot, "Docs", "Archive", "emotional_curves_007.bin");
            string streamingStem = Path.Combine(repoRoot, "StreamingAssets", "music_stem_bpm.h8bin");
            string unityStreamingStem = Path.Combine(repoRoot, "Assets", "StreamingAssets", "music_stem_bpm.h8bin");
            return File.Exists(archiveStem) ||
                   File.Exists(archiveCurve) ||
                   File.Exists(streamingStem) ||
                   File.Exists(unityStreamingStem);
        }

        private void GenerateEmergencyMockAudioProfiles()
        {
            if (Volatile.Read(ref _nativeAllocated) == 0 ||
                !TryAcquireStemFrameMutationView(out AdaptiveStemVaultViews views, out IDataVault guardVault))
                return;

            try
            {
                if (views.Rules.Length <= 0 || views.StemCommands.Length < 2)
                    return;

                AudioStemRuleDTO rule = default;
                rule.AttackSeconds = DefaultAttackSeconds;
                rule.ReleaseSeconds = DefaultReleaseSeconds;
                rule.CrossfadeSeconds = DefaultCrossfadeSeconds;
                rule.BeatBpm = DefaultBeatBpm;
                rule.BeatWindowSeconds = DefaultBeatWindowSeconds;
                rule.DepthMinMeters = DefaultDepthMinMeters;
                rule.DepthMaxMeters = DefaultDepthMaxMeters;
                rule.DepthFilterMinHz = DefaultDepthFilterMinHz;
                rule.DepthFilterMaxHz = DefaultDepthFilterMaxHz;
                rule.CombatEnterThreshold = 0.5f;
                rule.CombatExitThreshold = 0.35f;
                rule.NarrativeOverrideWeight = 1f;
                rule.GlobalQualityWeight = ResolveGlobalQualityWeightFromSnapshot();
                rule.SystemHealth01 = 1f;
                rule.IoPressure01 = 0f;
                rule.BiomeFadeSeconds = DefaultBiomeFadeSeconds;
                rule.CurrentBiomeHash = DefaultBiomeHash;
                rule.TargetBiomeHash = DefaultBiomeHash;
                rule.StemBaseHash = StemBaseHash;
                rule.StemActionHash = StemActionHash;
                rule.StemDepthHash = StemDepthHash;
                rule.StemBossHash = StemBossHash;
                rule.KernelCadenceSeconds = ResolveKernelCadenceSeconds(rule.GlobalQualityWeight);
                views.Rules[0] = rule;

                StemCommandDTO primary = default;
                primary.StemHash_A = StemBaseHash;
                primary.Volume_A = 1f;
                primary.StemHash_B = StemActionHash;
                primary.Volume_B = 0f;
                StemCommandDTO secondary = default;
                secondary.StemHash_A = StemDepthHash;
                secondary.Volume_A = 0f;
                secondary.StemHash_B = StemBossHash;
                secondary.Volume_B = 0f;
                views.StemCommands[0] = primary;
                views.StemCommands[1] = secondary;
            }
            finally
            {
                ReleaseAdaptiveStemMutationGuard(guardVault, AudioStemFrameMutationGuardMask);
            }
        }

        private void DrainSignalInputs()
        {
            _lastDamage01 = math.saturate(_mockDamage01);
            _lastOxygenDanger01 = math.saturate(_mockOxygenDanger01);

            ReadOnlySpan<SystemHealthIndexSignal> healthSignals = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshot();
            for (int i = 0; i < healthSignals.Length; i++)
            {
                SystemHealthIndexSignal signal = healthSignals[i];
                _lastSystemHealth01 = math.saturate(signal.Health01);
                float pressure = math.saturate(math.max(signal.Pressure01, 1f - signal.Health01));
                if (signal.State >= SystemHealthIndexSignal.StateCritical)
                    pressure = math.saturate(math.max(pressure, 0.85f));
                _lastIoPressure01 = pressure;
            }

            ReadOnlySpan<CombatDamageSignal> damageSignals = SignalBus<CombatDamageSignal>.GetFrameSnapshot();
            for (int i = 0; i < damageSignals.Length; i++)
            {
                float damage01 = math.saturate(damageSignals[i].Magnitude * 0.01f);
                _lastDamage01 = math.max(_lastDamage01, damage01);
            }

            uint playerSurvivalVitalsSourceId = _playerSurvivalVitalsSourceId;
            if (playerSurvivalVitalsSourceId != 0u)
            {
                ReadOnlySpan<SurvivalVitalsChangedSignal> vitalSignals = SignalBus<SurvivalVitalsChangedSignal>.GetFrameSnapshot();
                for (int i = 0; i < vitalSignals.Length; i++)
                {
                    ref readonly SurvivalVitalsChangedSignal signal = ref vitalSignals[i];
                    if (signal.SourceId != playerSurvivalVitalsSourceId ||
                        (signal.Flags & SurvivalVitalsChangedSignalFlags.Oxygen) == 0u ||
                        !math.isfinite(signal.Oxygen01))
                    {
                        continue;
                    }

                    float oxygenDanger01 = 1f - math.saturate(signal.Oxygen01);
                    _lastOxygenDanger01 = math.max(_lastOxygenDanger01, oxygenDanger01);
                }
            }

            ReadOnlySpan<BiomeChangedSignal> biomeSignals = SignalBus<BiomeChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < biomeSignals.Length; i++)
            {
                uint nextHash = biomeSignals[i].CurrentBiomeHash;
                if (nextHash == 0u || nextHash == _targetBiomeHash)
                    continue;

                _currentBiomeHash = _targetBiomeHash == 0u ? DefaultBiomeHash : _targetBiomeHash;
                _targetBiomeHash = nextHash;
                _biomeBlend01 = 0f;
            }

            ReadOnlySpan<NarrativePoiStateSignal> narrativeSignals = SignalBus<NarrativePoiStateSignal>.GetFrameSnapshot();
            for (int i = 0; i < narrativeSignals.Length; i++)
                _narrativeStateMask = narrativeSignals[i].StateMask;

        }

        private void UpdateBeatAndBiomeState(ref AdaptiveStemVaultViews views, float deltaTime)
        {
            AudioStemRuleDTO rule = views.Rules[0];
            float bpm = math.clamp(FiniteOrFallback(rule.BeatBpm, DefaultBeatBpm), MinBpm, MaxBpm);
            float beatInterval = 60f / math.max(MinBpm, bpm);
            _beatTimerSeconds += deltaTime;
            if (_beatTimerSeconds > 4096f)
                _beatTimerSeconds -= 4096f;

            float biomeFade = math.max(0.1f, FiniteOrFallback(rule.BiomeFadeSeconds, DefaultBiomeFadeSeconds));
            _biomeBlend01 = math.saturate(_biomeBlend01 + deltaTime / biomeFade);
            if (_biomeBlend01 >= 1f)
                _currentBiomeHash = _targetBiomeHash;

            float ioPressure = math.saturate(_lastIoPressure01);
            float ioDelayTarget = ioPressure * ioPressure * 3f;
            _ioDelaySeconds = math.max(0f, math.max(_ioDelaySeconds - deltaTime, ioDelayTarget));

            float beatPhase01 = math.frac(_beatTimerSeconds / math.max(0.001f, beatInterval));
            float beatWindow01 = math.saturate(rule.BeatWindowSeconds / math.max(0.001f, beatInterval));
            uint flags = rule.Flags & ~(FlagBeatGateOpen | FlagNarrativeOverride | FlagIoTransitionDelay | FlagClipNotStreaming);
            if (beatPhase01 <= beatWindow01 && _ioDelaySeconds <= 0.0001f)
                flags |= FlagBeatGateOpen;
            if ((_narrativeStateMask & ResolveBossMask()) != 0ul)
                flags |= FlagNarrativeOverride;
            if (_ioDelaySeconds > 0.0001f)
                flags |= FlagIoTransitionDelay;
            flags |= _streamingFaultFlags;

            rule.Flags = flags;
            rule.GroupBlend01 = _biomeBlend01;
            rule.CurrentBiomeHash = _currentBiomeHash;
            rule.TargetBiomeHash = _targetBiomeHash;
            views.Rules[0] = rule;

            StemMixFrameDTO frame = views.MixFrame[0];
            frame.BeatPhase01 = beatPhase01;
            frame.Flags = flags;
            frame.GroupBlend01 = _biomeBlend01;
            views.MixFrame[0] = frame;
        }

        private void UpdateVaultRulesFromManagedState(ref AdaptiveStemVaultViews views, float deltaTime)
        {
            AudioStemRuleDTO rule = views.Rules[0];
            float quality = ResolveGlobalQualityWeightFromSnapshot();
            float pressure01 = math.saturate(math.max(_lastIoPressure01, 1f - _lastSystemHealth01));
            float pressurePenalty = math.lerp(1f, 0.1f, Smooth01(pressure01));
            rule.GlobalQualityWeight = math.saturate(math.min(quality, math.saturate(_mockQualityBias01)) * pressurePenalty);
            rule.SystemHealth01 = math.saturate(_lastSystemHealth01);
            rule.IoPressure01 = math.saturate(_lastIoPressure01);
            rule.Damage01 = math.saturate(_lastDamage01);
            rule.OxygenDanger01 = math.saturate(_lastOxygenDanger01);
            rule.NarrativeStateMask = _narrativeStateMask;
            rule.KernelCadenceSeconds = ResolveKernelCadenceSeconds(rule.GlobalQualityWeight);
            _kernelAccumulatorSeconds += deltaTime;
            views.Rules[0] = SanitizeRule(rule);
        }

        private void RunAudioKernels(ref AdaptiveStemVaultViews views)
        {
            AudioStemRuleDTO rule = views.Rules[0];
            float cadence = math.max(MinAudioDeltaSeconds, rule.KernelCadenceSeconds);
            if (_kernelAccumulatorSeconds + KernelCadenceEpsilonSeconds < cadence)
            {
                WriteTelemetry(ref views, 0f);
                return;
            }

            float kernelDeltaSeconds = math.clamp(_kernelAccumulatorSeconds, MinAudioDeltaSeconds, MaxAudioDeltaSeconds);
            _kernelAccumulatorSeconds = 0f;
            long startTicks = Stopwatch.GetTimestamp();
            MockAudioStimulusJob mockJob = new MockAudioStimulusJob
            {
                Rules = (AudioStemRuleDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Rules),
                Predator = (MockPredatorProximitySignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.MockPredator),
                Depth = (MockDepthSignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.MockDepth),
                Tension = (MockTensionSignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.MockTension),
                FrameIndex = _simulationFrameCounter,
                DeltaSeconds = kernelDeltaSeconds,
                MockDepthAmplitudeMeters = _mockDepthAmplitudeMeters,
                MockPredatorCycleSeconds = _mockPredatorCycleSeconds
            };
            mockJob.Execute();
            AudioStemTensionKernelJob tensionJob = new AudioStemTensionKernelJob
            {
                State = (AudioStemStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.StemState),
                Rules = (AudioStemRuleDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Rules),
                Predator = (MockPredatorProximitySignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.MockPredator),
                Depth = (MockDepthSignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.MockDepth),
                MockTension = (MockTensionSignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.MockTension),
                DeltaSeconds = kernelDeltaSeconds
            };
            tensionJob.Execute();

            StemCrossfadeSolverJob solverJob = new StemCrossfadeSolverJob
            {
                State = (AudioStemStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.StemState),
                Rules = (AudioStemRuleDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Rules),
                Commands = (StemCommandDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.StemCommands),
                MixFrame = (StemMixFrameDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.MixFrame),
                FrameIndex = _simulationFrameCounter,
                DeltaSeconds = kernelDeltaSeconds
            };
            solverJob.Execute();
            QueueMixFrameForVisualSync(ref views);
            float elapsedMicroseconds = ResolveElapsedMicroseconds(startTicks);
            WriteTelemetry(ref views, elapsedMicroseconds);
            if (elapsedMicroseconds > MixerDumpThresholdMicroseconds || HasNonFiniteMixFrame(ref views))
                Interlocked.Exchange(ref _telemetryDumpRequested, 1);
        }

        private void QueueMixFrameForVisualSync(ref AdaptiveStemVaultViews views)
        {
            if (!views.MixFrame.IsCreated || views.MixFrame.Length <= 0)
                return;

            _pendingUnityMixFrame = views.MixFrame[0];
            _pendingUnityMixRule = views.Rules.IsCreated && views.Rules.Length > 0 ? views.Rules[0] : default;
            Volatile.Write(ref _pendingUnityMixFrameDirty, 1);
        }

        private void FlushPendingUnityMixFrame()
        {
            if (Volatile.Read(ref _pendingUnityMixFrameDirty) == 0)
                return;

            Volatile.Write(ref _pendingUnityMixFrameDirty, 0);
            ApplyMixFrameToUnityAudio(in _pendingUnityMixFrame, in _pendingUnityMixRule);
        }

        private void ApplyMixFrameToUnityAudio(in StemMixFrameDTO frame, in AudioStemRuleDTO rule)
        {
            if (ProceduralSynthOwnsStemTransport)
            {
                float depthMeters = math.max(0f, math.isfinite(rule.MockDepthMeters) ? rule.MockDepthMeters : 0f);
                float quality = math.saturate(math.isfinite(frame.QualityWeight) ? frame.QualityWeight : 1f);
                float tension = math.saturate(math.isfinite(frame.TensionIndex) ? frame.TensionIndex : 0f);
                CelestialLightReadabilitySnapshot light = ResolveCelestialLightReadability();
                ApplyCelestialLightToMusicSignal(in light, ref tension, ref depthMeters, ref quality);
                PushDynamicMusicSignal(tension, depthMeters, quality);
                return;
            }

            float baseMeasuredLUFS = -23f + (1f - frame.BaseVolume) * -12f;
            float baseTargetLUFS = -14f;
            float baseGainDB = Hecton8.PureLogic.Systems.LufsNormalizationCalculator.Compute(baseMeasuredLUFS, baseTargetLUFS, 6f, -12f);
            float baseScale = math.pow(10f, baseGainDB / 20f);
            
            float actionMeasuredLUFS = -20f + (1f - frame.ActionVolume) * -15f;
            float actionTargetLUFS = -14f;
            float actionGainDB = Hecton8.PureLogic.Systems.LufsNormalizationCalculator.Compute(actionMeasuredLUFS, actionTargetLUFS, 6f, -12f);
            float actionScale = math.pow(10f, actionGainDB / 20f);

            float depthMeasuredLUFS = -25f + (1f - frame.DepthVolume) * -10f;
            float depthTargetLUFS = -14f;
            float depthGainDB = Hecton8.PureLogic.Systems.LufsNormalizationCalculator.Compute(depthMeasuredLUFS, depthTargetLUFS, 6f, -12f);
            float depthScale = math.pow(10f, depthGainDB / 20f);

            float bossMeasuredLUFS = -18f + (1f - frame.BossVolume) * -18f;
            float bossTargetLUFS = -14f;
            float bossGainDB = Hecton8.PureLogic.Systems.LufsNormalizationCalculator.Compute(bossMeasuredLUFS, bossTargetLUFS, 6f, -12f);
            float bossScale = math.pow(10f, bossGainDB / 20f);

            ApplyVolume(_baseStemSource, frame.BaseVolume * baseScale);
            ApplyVolume(_actionStemSource, frame.ActionVolume * actionScale);
            ApplyVolume(_depthStemSource, frame.DepthVolume * depthScale);
            ApplyVolume(_bossStemSource, frame.BossVolume * bossScale);
            ApplyCutoff(_baseLowPassFilter, frame.CutoffHz);
            ApplyCutoff(_actionLowPassFilter, frame.CutoffHz);
            ApplyCutoff(_depthLowPassFilter, frame.CutoffHz);
            ApplyCutoff(_bossLowPassFilter, frame.CutoffHz);

            if (_autoStartSources)
            {
                EnsurePlaying(_baseStemSource);
                EnsurePlaying(_actionStemSource);
                EnsurePlaying(_depthStemSource);
                EnsurePlaying(_bossStemSource);
            }
        }

        private CelestialLightReadabilitySnapshot ResolveCelestialLightReadability()
        {
            ICelestialLightReadabilityReadModel readModel = _celestialLightReadModel;
            if (!IsCelestialLightReadModelUsable(readModel))
            {
                CacheCelestialLightReadModel(GlobalRegistry.CelestialLightReadabilityReadModel);
                readModel = _celestialLightReadModel;
                if (!IsCelestialLightReadModelUsable(readModel))
                    return default;
            }

            return readModel.LightReadabilitySnapshot;
        }

        private static bool IsCelestialLightReadModelUsable(ICelestialLightReadabilityReadModel readModel)
        {
            if (readModel == null)
                return false;

            if (readModel is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private static void ApplyCelestialLightToMusicSignal(
            in CelestialLightReadabilitySnapshot light,
            ref float tension01,
            ref float depthMeters,
            ref float quality01)
        {
            if ((light.Flags & (uint)CelestialLightReadabilityFlags.Valid) == 0u)
                return;

            if (math.isfinite(light.DepthMeters))
                depthMeters = math.max(depthMeters, light.DepthMeters);

            float darknessPressure = math.saturate(
                light.DeepDarkness01 * 0.62f +
                light.ArtificialLightWeight01 * 0.26f +
                light.BiolumWeight01 * 0.12f);
            if ((light.Flags & (uint)CelestialLightReadabilityFlags.LightPhaseNight) != 0u)
                darknessPressure = math.max(darknessPressure, 0.18f);
            else if ((light.Flags & (uint)CelestialLightReadabilityFlags.LightPhaseTwilight) != 0u)
                darknessPressure = math.max(darknessPressure, 0.08f);

            tension01 = math.max(tension01, darknessPressure);

            if (math.isfinite(light.Quality01))
                quality01 = math.saturate(math.min(quality01, math.lerp(1f, light.Quality01, 0.35f)));
        }

        private static void EnsureDynamicMusicSignalLaneCold()
        {
            SignalBus<DynamicMusicScalarSignal>.Configure(
                expectedCapacity: DynamicMusicScalarSignal.ExpectedCapacity,
                maxFrameSignals: DynamicMusicScalarSignal.MaxFrameSignals,
                lowTierFrameSignals: DynamicMusicScalarSignal.LowTierFrameSignals,
                laneHash: DynamicMusicScalarSignal.LaneHash);
            SignalBus<DynamicMusicScalarSignal>.EnsureInitialized();
        }

        private void PushDynamicMusicSignal(float tension01, float depthMeters, float quality01)
        {
            EnsureDynamicMusicSignalLaneCold();
            DynamicMusicScalarSignal signal = default;
            signal.Frame = _simulationFrameCounter;
            signal.Flags =
                DynamicMusicScalarSignal.FlagExternalScalars |
                DynamicMusicScalarSignal.FlagSuppressReactiveImpulses;
            signal.Tension01 = math.saturate(FiniteOrFallback(tension01, 0f));
            signal.DepthMeters = math.max(0f, FiniteOrFallback(depthMeters, 0f));
            signal.GlobalQualityWeight = math.saturate(FiniteOrFallback(quality01, 1f));
            signal.DamageImpulse01 = 0f;
            signal.MusicActivity01 = 0f;
            signal.SourceHash = DynamicMusicScalarSignal.SourceAdaptiveStemHash;
            SignalBus<DynamicMusicScalarSignal>.TryPushTracked(in signal, ref s_x001AdaptiveStemAudioMixerSignalPushDropCount);
        }

        private static void ApplyVolume(AudioSource source, float volume)
        {
            if (source == null)
                return;

            float sanitized = math.saturate(FiniteOrFallback(volume, 0f));
            if (math.abs(source.volume - sanitized) > 0.0005f)
                source.volume = sanitized;
        }

        private static void ApplyCutoff(AudioLowPassFilter filter, float cutoffHz)
        {
            if (filter == null)
                return;

            float sanitized = math.clamp(FiniteOrFallback(cutoffHz, DefaultDepthFilterMaxHz), 10f, DefaultDepthFilterMaxHz);
            if (math.abs(filter.cutoffFrequency - sanitized) > 2f)
                filter.cutoffFrequency = sanitized;
        }

        private static void EnsurePlaying(AudioSource source)
        {
            if (source == null || source.clip == null || source.isPlaying)
                return;

            source.Play();
        }

        private void WriteTelemetry(ref AdaptiveStemVaultViews views, float elapsedMicroseconds)
        {
            if (!views.TelemetryRing.IsCreated ||
                !views.TelemetryCursor.IsCreated ||
                !views.MixFrame.IsCreated ||
                !views.Rules.IsCreated ||
                views.TelemetryRing.Length <= 0 ||
                views.TelemetryCursor.Length <= 0 ||
                views.MixFrame.Length <= 0 ||
                views.Rules.Length <= 0)
                return;

            StemMixFrameDTO frame = views.MixFrame[0];
            AudioStemRuleDTO rule = views.Rules[0];
            int cursor = views.TelemetryCursor[0];
            int capacity = math.min(TelemetryCapacity, views.TelemetryRing.Length);
            int index = cursor % capacity;
            CelestialLightReadabilitySnapshot light = ResolveCelestialLightReadability();
            AudioStemTelemetryEntry entry = default;
            entry.Frame = frame.Frame != 0u ? frame.Frame : _simulationFrameCounter;
            entry.ActiveStemHash = frame.ActiveStemHash;
            entry.BiomeHash = frame.BiomeHash;
            entry.Flags = frame.Flags | BuildCelestialLightTelemetryFlags(in light);
            entry.TensionIndex = frame.TensionIndex;
            entry.DepthFilter = frame.DepthFilter;
            entry.CutoffHz = frame.CutoffHz;
            entry.MixerUpdateMicroseconds = elapsedMicroseconds;
            entry.BaseVolume = frame.BaseVolume;
            entry.ActionVolume = frame.ActionVolume;
            entry.DepthVolume = frame.DepthVolume;
            entry.BossVolume = frame.BossVolume;
            entry.QualityWeight = frame.QualityWeight;
            entry.BeatPhase01 = frame.BeatPhase01;
            entry.IoPressure01 = frame.IoPressure01;
            entry.UpdateCadenceHz = 1f / math.max(MinAudioDeltaSeconds, rule.KernelCadenceSeconds);
            views.TelemetryRing[index] = entry;
            views.TelemetryCursor[0] = (cursor + 1) % capacity;
        }

        private static uint BuildCelestialLightTelemetryFlags(in CelestialLightReadabilitySnapshot light)
        {
            if ((light.Flags & (uint)CelestialLightReadabilityFlags.Valid) == 0u)
                return TelemetryFlagCelestialLightMissing;

            uint flags = TelemetryFlagCelestialLightBound;
            if ((light.Flags & (uint)CelestialLightReadabilityFlags.Fallback) != 0u)
                flags |= TelemetryFlagCelestialLightFallback;
            if ((light.Flags & (uint)CelestialLightReadabilityFlags.ArtificialLightCritical) != 0u)
                flags |= TelemetryFlagCelestialLightAbyssCritical;
            if ((light.Flags & (uint)CelestialLightReadabilityFlags.QualityReduced) != 0u)
                flags |= TelemetryFlagCelestialLightQualityReduced;
            if ((light.Flags & (uint)CelestialLightReadabilityFlags.LightPhaseTwilight) != 0u)
                flags |= TelemetryFlagCelestialLightTwilight;
            if ((light.Flags & (uint)CelestialLightReadabilityFlags.LightPhaseNight) != 0u)
                flags |= TelemetryFlagCelestialLightNight;
            return flags;
        }

        private bool HasNonFiniteMixFrame(ref AdaptiveStemVaultViews views)
        {
            if (!views.MixFrame.IsCreated || views.MixFrame.Length <= 0)
                return false;

            StemMixFrameDTO frame = views.MixFrame[0];
            if (!math.isfinite(frame.TensionIndex) ||
                !math.isfinite(frame.DepthFilter) ||
                !math.isfinite(frame.CutoffHz) ||
                !math.isfinite(frame.BaseVolume) ||
                !math.isfinite(frame.ActionVolume) ||
                !math.isfinite(frame.DepthVolume) ||
                !math.isfinite(frame.BossVolume))
            {
                frame.Flags |= FlagNonFinite;
                views.MixFrame[0] = frame;
                return true;
            }

            return false;
        }

        private void DumpTelemetryOnce()
        {
            if (Interlocked.Exchange(ref _telemetryDumped, 1) != 0)
                return;

            try
            {
                IDataVault vault = _dataVault;
                if (vault == null ||
                    !vault.TryReadOnlyHandle(in _telemetryRingHandle, out NativeArray<AudioStemTelemetryEntry>.ReadOnly telemetryRing) ||
                    !telemetryRing.IsCreated ||
                    telemetryRing.Length <= 0)
                    return;

                string repoRoot = ResolveRepoRootPath();
                string dumpPath = Path.Combine(repoRoot, DumpRelativePath);
                int count = math.min(TelemetryCapacity, telemetryRing.Length);
                int entryBytes = UnsafeUtility.SizeOf<AudioStemTelemetryEntry>();
                int byteCount = count * entryBytes;
                byte* source = (byte*)telemetryRing.GetUnsafeReadOnlyPtr();
                NativeFaultDumpWriter.TryWriteAll(dumpPath, new ReadOnlySpan<byte>(source, byteCount), byteCount);
            }
            catch (IOException)
            {
                Hecton8.Core.H8Debug.LogWarning("[SHINOBU_46] Failed to dump adaptive stem telemetry.");
            }
            catch (UnauthorizedAccessException)
            {
                Hecton8.Core.H8Debug.LogWarning("[SHINOBU_46] Failed to dump adaptive stem telemetry.");
            }
        }

#if UNITY_EDITOR
        private void PollCsvRulesCold()
        {
            if (_csvPollCountdown > 0)
            {
                _csvPollCountdown--;
                return;
            }

            _csvPollCountdown = CsvPollSlowTickInterval;
            string path = ResolveCachedCsvPathCold();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            DateTime writeTime = File.GetLastWriteTimeUtc(path);
            if (writeTime == _lastCsvWriteUtc)
                return;

            try
            {
                int bytesRead;
                int expectedBytes;
                Span<byte> csvScratch = stackalloc byte[CsvScratchBytes];
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long fileLength = stream.Length;
                    if (fileLength <= 0L || fileLength > CsvScratchBytes)
                        return;

                    expectedBytes = (int)fileLength;
                    bytesRead = 0;
                    while (bytesRead < expectedBytes)
                    {
                        int read = stream.Read(csvScratch.Slice(bytesRead, expectedBytes - bytesRead));
                        if (read <= 0)
                            break;

                        bytesRead += read;
                    }
                }

                if (bytesRead != expectedBytes)
                    return;

                if (ParseCsvRules(csvScratch.Slice(0, bytesRead)))
                    _lastCsvWriteUtc = writeTime;
            }
            catch (IOException)
            {
                Hecton8.Core.H8Debug.LogWarning("[SHINOBU_46] audio_stem_rules.csv parse failed.");
            }
            catch (UnauthorizedAccessException)
            {
                Hecton8.Core.H8Debug.LogWarning("[SHINOBU_46] audio_stem_rules.csv parse failed.");
            }
        }

        private bool ParseCsvRules(ReadOnlySpan<byte> csvBytes)
        {
            if (csvBytes.Length <= 0)
                return false;

            if (!TryReadRuleForOwnerRoute(out AudioStemRuleDTO rule))
                rule = default;

            int safeByteCount = csvBytes.Length;
            int index = 0;
            while (index < safeByteCount)
            {
                while (index < safeByteCount && IsLineBreakOrSpace(csvBytes[index]))
                    index++;
                if (index >= safeByteCount)
                    break;

                if (csvBytes[index] == (byte)'#')
                {
                    while (index < safeByteCount && !IsLineBreak(csvBytes[index]))
                        index++;
                    continue;
                }

                int keyStart = index;
                while (index < safeByteCount && csvBytes[index] != (byte)',' && !IsLineBreak(csvBytes[index]))
                    index++;

                int keyEnd = index;
                if (index >= safeByteCount || csvBytes[index] != (byte)',')
                {
                    while (index < safeByteCount && !IsLineBreak(csvBytes[index]))
                        index++;
                    continue;
                }

                index++;
                int valueStart = index;
                while (index < safeByteCount && !IsLineBreak(csvBytes[index]))
                    index++;

                if (TryParseFloat(csvBytes, valueStart, index, out float value))
                {
                    uint hash = HashCsvKey(csvBytes, keyStart, keyEnd);
                    ApplyCsvRule(ref rule, hash, value);
                }
            }

            return TryWriteRuleForOwnerRoute(in rule);
        }

        private static void ApplyCsvRule(ref AudioStemRuleDTO rule, uint hash, float value)
        {
            if (hash == CsvAttackSecondsHash)
                rule.AttackSeconds = value;
            else if (hash == CsvReleaseSecondsHash)
                rule.ReleaseSeconds = value;
            else if (hash == CsvCrossfadeSecondsHash)
                rule.CrossfadeSeconds = value;
            else if (hash == CsvBeatBpmHash)
                rule.BeatBpm = value;
            else if (hash == CsvBeatWindowSecondsHash)
                rule.BeatWindowSeconds = value;
            else if (hash == CsvDepthMinMetersHash)
                rule.DepthMinMeters = value;
            else if (hash == CsvDepthMaxMetersHash)
                rule.DepthMaxMeters = value;
            else if (hash == CsvDepthFilterMinHzHash)
                rule.DepthFilterMinHz = value;
            else if (hash == CsvDepthFilterMaxHzHash)
                rule.DepthFilterMaxHz = value;
            else if (hash == CsvCombatEnterHash)
                rule.CombatEnterThreshold = value;
            else if (hash == CsvCombatExitHash)
                rule.CombatExitThreshold = value;
            else if (hash == CsvNarrativeOverrideWeightHash)
                rule.NarrativeOverrideWeight = value;
            else if (hash == CsvBiomeFadeSecondsHash)
                rule.BiomeFadeSeconds = value;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> bytes, int start, int end, out float value)
        {
            value = 0f;
            int index = start;
            while (index < end && IsHorizontalSpace(bytes[index]))
                index++;

            float sign = 1f;
            if (index < end && bytes[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (index < end && bytes[index] == (byte)'+')
            {
                index++;
            }

            float integer = 0f;
            int digits = 0;
            while (index < end && bytes[index] >= (byte)'0' && bytes[index] <= (byte)'9')
            {
                integer = integer * 10f + bytes[index] - (byte)'0';
                index++;
                digits++;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (index < end && bytes[index] == (byte)'.')
            {
                index++;
                while (index < end && bytes[index] >= (byte)'0' && bytes[index] <= (byte)'9')
                {
                    fraction = fraction * 10f + bytes[index] - (byte)'0';
                    divisor *= 10f;
                    index++;
                    digits++;
                }
            }

            if (digits == 0)
                return false;

            value = sign * (integer + fraction / math.max(1f, divisor));
            return math.isfinite(value);
        }

        private static uint HashCsvKey(ReadOnlySpan<byte> bytes, int start, int end)
        {
            uint hash = 2166136261u;
            for (int i = start; i < end; i++)
            {
                byte b = bytes[i];
                if (b == (byte)' ' || b == (byte)'\t')
                    continue;

                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);

                hash ^= b;
                hash *= 16777619u;
            }

            return hash;
        }

        private void RefreshCsvPathCold()
        {
            _resolvedCsvPath = null;
            _lastResolvedCsvRelativePath = null;
            _lastCsvWriteUtc = default;
            _csvPollCountdown = 0;
            ResolveCachedCsvPathCold();
        }

        private string ResolveCachedCsvPathCold()
        {
            string relative = string.IsNullOrEmpty(_csvRelativePath) ? CsvDefaultRelativePath : _csvRelativePath;
            if (!string.IsNullOrEmpty(_resolvedCsvPath) &&
                string.Equals(_lastResolvedCsvRelativePath, relative, StringComparison.Ordinal))
            {
                return _resolvedCsvPath;
            }

            _lastCsvWriteUtc = default;
            _lastResolvedCsvRelativePath = relative;
            if (Path.IsPathRooted(relative))
            {
                _resolvedCsvPath = relative;
                return _resolvedCsvPath;
            }

            _resolvedCsvPath = Path.Combine(ResolveRepoRootPath(), relative);
            return _resolvedCsvPath;
        }

#endif
        // Not CSV. This is the narrative-override gate read every mixer tick from
        // EvaluateStemRule, so it must exist in player builds; it had been swept into the
        // editor-only CSV polling block above.
        private ulong ResolveBossMask()
        {
            return _bossNarrativeMask == 0ul ? DefaultBossNarrativeMask : _bossNarrativeMask;
        }
#if UNITY_EDITOR

        private static bool IsLineBreak(byte value)
        {
            return value == (byte)'\r' || value == (byte)'\n';
        }

        private static bool IsHorizontalSpace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }

        private static bool IsLineBreakOrSpace(byte value)
        {
            return IsLineBreak(value) || IsHorizontalSpace(value);
        }
#endif

        private static AudioStemRuleDTO SanitizeRule(AudioStemRuleDTO rule)
        {
            rule.AttackSeconds = math.clamp(FiniteOrFallback(rule.AttackSeconds, DefaultAttackSeconds), 0.01f, 5f);
            rule.ReleaseSeconds = math.clamp(FiniteOrFallback(rule.ReleaseSeconds, DefaultReleaseSeconds), 0.1f, 60f);
            rule.CrossfadeSeconds = math.clamp(FiniteOrFallback(rule.CrossfadeSeconds, DefaultCrossfadeSeconds), 0.05f, 30f);
            rule.BeatBpm = math.clamp(FiniteOrFallback(rule.BeatBpm, DefaultBeatBpm), MinBpm, MaxBpm);
            rule.BeatWindowSeconds = math.clamp(FiniteOrFallback(rule.BeatWindowSeconds, DefaultBeatWindowSeconds), 0.001f, 0.25f);
            rule.DepthMinMeters = math.max(0f, FiniteOrFallback(rule.DepthMinMeters, DefaultDepthMinMeters));
            rule.DepthMaxMeters = math.max(rule.DepthMinMeters + 1f, FiniteOrFallback(rule.DepthMaxMeters, DefaultDepthMaxMeters));
            rule.DepthFilterMinHz = math.clamp(FiniteOrFallback(rule.DepthFilterMinHz, DefaultDepthFilterMinHz), 10f, DefaultDepthFilterMaxHz);
            rule.DepthFilterMaxHz = math.clamp(FiniteOrFallback(rule.DepthFilterMaxHz, DefaultDepthFilterMaxHz), rule.DepthFilterMinHz, DefaultDepthFilterMaxHz);
            rule.CombatEnterThreshold = math.saturate(FiniteOrFallback(rule.CombatEnterThreshold, 0.5f));
            rule.CombatExitThreshold = math.saturate(FiniteOrFallback(rule.CombatExitThreshold, 0.35f));
            rule.NarrativeOverrideWeight = math.saturate(FiniteOrFallback(rule.NarrativeOverrideWeight, 1f));
            rule.GlobalQualityWeight = math.saturate(FiniteOrFallback(rule.GlobalQualityWeight, 1f));
            rule.SystemHealth01 = math.saturate(FiniteOrFallback(rule.SystemHealth01, 1f));
            rule.IoPressure01 = math.saturate(FiniteOrFallback(rule.IoPressure01, 0f));
            rule.Damage01 = math.saturate(FiniteOrFallback(rule.Damage01, 0f));
            rule.OxygenDanger01 = math.saturate(FiniteOrFallback(rule.OxygenDanger01, 0f));
            rule.BiomeFadeSeconds = math.clamp(FiniteOrFallback(rule.BiomeFadeSeconds, DefaultBiomeFadeSeconds), 0.25f, 60f);
            if (rule.CurrentBiomeHash == 0u)
                rule.CurrentBiomeHash = DefaultBiomeHash;
            if (rule.TargetBiomeHash == 0u)
                rule.TargetBiomeHash = rule.CurrentBiomeHash;
            if (rule.StemBaseHash == 0u)
                rule.StemBaseHash = StemBaseHash;
            if (rule.StemActionHash == 0u)
                rule.StemActionHash = StemActionHash;
            if (rule.StemDepthHash == 0u)
                rule.StemDepthHash = StemDepthHash;
            if (rule.StemBossHash == 0u)
                rule.StemBossHash = StemBossHash;
            rule.GroupBlend01 = math.saturate(FiniteOrFallback(rule.GroupBlend01, 0f));
            rule.KernelCadenceSeconds = ResolveKernelCadenceSeconds(rule.GlobalQualityWeight);
            return rule;
        }

        private static float ResolveKernelCadenceSeconds(float qualityWeight)
        {
            float q = Smooth01(math.saturate(qualityWeight));
            return math.lerp(0.2f, 0.016666668f, q);
        }

        private float ResolveGlobalQualityWeightFromSnapshot()
        {
            if (TryResolveScalabilityState(out NativeArray<ScalabilityStateDTO>.ReadOnly scalabilityState) &&
                scalabilityState.Length > 0)
            {
                ScalabilityStateDTO state = scalabilityState[0];
                if (math.isfinite(state.GlobalQualityWeight))
                {
                    _cachedGlobalQualityWeight = math.saturate(state.GlobalQualityWeight);
                    return _cachedGlobalQualityWeight;
                }
            }

            return math.saturate(_cachedGlobalQualityWeight);
        }

        private bool TryResolveScalabilityState(out NativeArray<ScalabilityStateDTO>.ReadOnly scalabilityState)
        {
            scalabilityState = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   _scalabilityStateHandle.BufferID != 0u &&
                   vault.TryReadOnlyHandle(in _scalabilityStateHandle, out scalabilityState) &&
                   scalabilityState.IsCreated;
        }

        private static float ResolveElapsedMicroseconds(long startTicks)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            return elapsedTicks * (1000000f / math.max(1f, (float)Stopwatch.Frequency));
        }

        private static string ResolveRepoRootPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static float FiniteOrFallback(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }

        private static void MemClearArray<T>(NativeArray<T> array)
            where T : unmanaged
        {
            if (!array.IsCreated || array.Length == 0)
                return;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array);
            UnsafeUtility.MemClear(ptr, (long)UnsafeUtility.SizeOf<T>() * array.Length);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct MockAudioStimulusJob : IJob
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public AudioStemRuleDTO* Rules;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public MockPredatorProximitySignal* Predator;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public MockDepthSignal* Depth;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public MockTensionSignal* Tension;
            public uint FrameIndex;
            public float DeltaSeconds;
            public float MockDepthAmplitudeMeters;
            public float MockPredatorCycleSeconds;

            public void Execute()
            {
                ref AudioStemRuleDTO rule = ref UnsafeUtility.AsRef<AudioStemRuleDTO>(Rules);
                ref MockPredatorProximitySignal predator = ref UnsafeUtility.AsRef<MockPredatorProximitySignal>(Predator);
                ref MockDepthSignal depth = ref UnsafeUtility.AsRef<MockDepthSignal>(Depth);
                ref MockTensionSignal tension = ref UnsafeUtility.AsRef<MockTensionSignal>(Tension);

                float cycle = math.max(1f, FiniteOrFallback(MockPredatorCycleSeconds, 18f));
                float phaseSeconds = FiniteOrFallback(rule.MockPhaseSeconds, 0f);
                float phase = math.frac(phaseSeconds / cycle);
                float triangle = 1f - math.abs(phase * 2f - 1f);
                float proximityMeters = math.lerp(100f, 0f, triangle);
                float proximity01 = 1f - math.saturate(proximityMeters * 0.01f);
                float depthPhase = math.frac(phaseSeconds / math.max(4f, cycle * 2.7f));
                float depth01 = 0.5f + 0.5f * MathLodApproximation.ApproxSinBhaskara(depthPhase * math.PI * 2f);
                float depthMeters = depth01 * math.max(0f, FiniteOrFallback(MockDepthAmplitudeMeters, DefaultDepthMaxMeters));

                predator.ProximityMeters = proximityMeters;
                predator.Proximity01 = math.saturate(proximity01);
                predator.OscillationPhase = phase;
                predator.DamageSpike01 = math.saturate(rule.Damage01);
                predator.Frame = FrameIndex;
                predator.SourceHash = StemActionHash;
                predator.Flags = 0u;
                depth.DepthMeters = depthMeters;
                depth.Depth01 = math.saturate(depth01);
                depth.Frame = FrameIndex;
                depth.Flags = 0u;

                float mockTension01 = math.saturate(
                    math.saturate(rule.Damage01) +
                    math.saturate(proximity01) * 0.72f +
                    math.saturate(rule.OxygenDanger01) * 0.42f);
                tension.Tension01 = mockTension01;
                tension.Damage01 = math.saturate(rule.Damage01);
                tension.Frame = FrameIndex;
                tension.Flags = 0u;

                rule.MockPredatorMeters = proximityMeters;
                rule.MockDepthMeters = depthMeters;
                float nextPhaseSeconds = phaseSeconds + math.clamp(DeltaSeconds, MinAudioDeltaSeconds, MaxAudioDeltaSeconds);
                rule.MockPhaseSeconds = math.select(nextPhaseSeconds, nextPhaseSeconds - 4096f, nextPhaseSeconds > 4096f);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct AudioStemTensionKernelJob : IJob
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public AudioStemStateDTO* State;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public AudioStemRuleDTO* Rules;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public MockPredatorProximitySignal* Predator;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public MockDepthSignal* Depth;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public MockTensionSignal* MockTension;
            public float DeltaSeconds;

            public void Execute()
            {
                ref AudioStemStateDTO state = ref UnsafeUtility.AsRef<AudioStemStateDTO>(State);
                ref readonly AudioStemRuleDTO rule = ref UnsafeUtility.AsRef<AudioStemRuleDTO>(Rules);
                ref readonly MockPredatorProximitySignal predator = ref UnsafeUtility.AsRef<MockPredatorProximitySignal>(Predator);
                ref readonly MockDepthSignal depth = ref UnsafeUtility.AsRef<MockDepthSignal>(Depth);
                ref readonly MockTensionSignal mockTension = ref UnsafeUtility.AsRef<MockTensionSignal>(MockTension);

                float predatorDanger = math.saturate(predator.Proximity01);
                float damage = math.saturate(math.max(math.max(rule.Damage01, predator.DamageSpike01), mockTension.Damage01));
                float oxygen = math.saturate(rule.OxygenDanger01);
                float narrative = (rule.Flags & FlagNarrativeOverride) != 0u ? rule.NarrativeOverrideWeight : 0f;
                float target = math.saturate(
                    math.max(mockTension.Tension01, damage * 1.0f + predatorDanger * 0.72f + oxygen * 0.42f) +
                    narrative);
                float current = math.saturate(FiniteOrFallback(state.TensionIndex, 0f));
                float delta = math.max(MinAudioDeltaSeconds, DeltaSeconds);
                float attack = math.max(0.01f, rule.AttackSeconds);
                float release = math.max(0.1f, rule.ReleaseSeconds);
                float response = target > current
                    ? math.saturate(delta / attack)
                    : math.saturate(delta / release);

                float tension = math.lerp(current, target, response);
                float depthDenominator = math.max(0.0001f, rule.DepthMaxMeters - rule.DepthMinMeters);
                float depth01 = math.saturate((depth.DepthMeters - rule.DepthMinMeters) / depthDenominator);
                float cutoff = math.lerp(rule.DepthFilterMaxHz, rule.DepthFilterMinHz, depth01);

                state.TensionIndex = math.saturate(FiniteOrFallback(tension, 0f));
                state.DepthFilter = math.clamp(FiniteOrFallback(cutoff, rule.DepthFilterMaxHz), 10f, rule.DepthFilterMaxHz);
                state.ActiveStemHash = ResolveActiveStemHash(rule, state.TensionIndex);
            }

            private static uint ResolveActiveStemHash(AudioStemRuleDTO rule, float tension)
            {
                uint active = rule.StemBaseHash;
                if (tension >= rule.CombatEnterThreshold)
                    active = rule.StemActionHash;
                if ((rule.Flags & FlagNarrativeOverride) != 0u)
                    active = rule.StemBossHash;

                return active;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct StemCrossfadeSolverJob : IJob
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public AudioStemStateDTO* State;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public AudioStemRuleDTO* Rules;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public StemCommandDTO* Commands;
            [NoAlias] [NativeDisableUnsafePtrRestriction] public StemMixFrameDTO* MixFrame;
            public uint FrameIndex;
            public float DeltaSeconds;

            public void Execute()
            {
                ref readonly AudioStemStateDTO state = ref UnsafeUtility.AsRef<AudioStemStateDTO>(State);
                ref readonly AudioStemRuleDTO rule = ref UnsafeUtility.AsRef<AudioStemRuleDTO>(Rules);
                ref StemMixFrameDTO frame = ref UnsafeUtility.AsRef<StemMixFrameDTO>(MixFrame);
                float tension = math.saturate(FiniteOrFallback(state.TensionIndex, 0f));
                float quality = math.saturate(FiniteOrFallback(rule.GlobalQualityWeight, 1f));
                float decorativeWeight = Smooth01(math.saturate((quality - 0.25f) * 1.4285715f));
                float narrativeActive = (rule.Flags & FlagNarrativeOverride) != 0u ? 1f : 0f;
                float beatGate = (rule.Flags & FlagBeatGateOpen) != 0u ? 1f : 0f;

                float explorationTarget = math.saturate(1f - tension * 2f);
                float actionTarget = math.saturate((tension - rule.CombatExitThreshold) / math.max(0.0001f, 1f - rule.CombatExitThreshold));
                float depthTarget = math.saturate((state.DepthFilter - rule.DepthFilterMaxHz) /
                                                  math.min(-0.0001f, rule.DepthFilterMinHz - rule.DepthFilterMaxHz));
                depthTarget *= decorativeWeight * (1f - narrativeActive * 0.65f);
                float bossTarget = narrativeActive;

                float lockedAction = math.lerp(frame.ActionVolume, actionTarget, beatGate);
                float lockedBase = math.lerp(frame.BaseVolume, explorationTarget, beatGate);
                float lockedDepth = math.lerp(frame.DepthVolume, depthTarget, beatGate);
                float lockedBoss = math.max(bossTarget, math.lerp(frame.BossVolume, bossTarget, beatGate));
                float collapsedDepth = lockedDepth * decorativeWeight;
                float collapsedBoss = math.lerp(lockedBoss * 0.6f, lockedBoss, decorativeWeight);
                float normalizedStep = math.saturate(DeltaSeconds / math.max(0.0001f, rule.CrossfadeSeconds));
                float alpha = normalizedStep * (2f - normalizedStep);

                frame.BaseVolume = math.saturate(FiniteOrFallback(math.lerp(frame.BaseVolume, lockedBase, alpha), 0f));
                frame.ActionVolume = math.saturate(FiniteOrFallback(math.lerp(frame.ActionVolume, lockedAction, alpha), 0f));
                frame.DepthVolume = math.saturate(FiniteOrFallback(math.lerp(frame.DepthVolume, collapsedDepth, alpha), 0f));
                frame.BossVolume = math.saturate(FiniteOrFallback(math.lerp(frame.BossVolume, collapsedBoss, alpha), 0f));
                frame.TensionIndex = tension;
                frame.DepthFilter = state.DepthFilter;
                frame.CutoffHz = math.clamp(FiniteOrFallback(state.DepthFilter, rule.DepthFilterMaxHz), 10f, rule.DepthFilterMaxHz);
                frame.QualityWeight = quality;
                frame.IoPressure01 = math.saturate(rule.IoPressure01);
                frame.ActiveStemHash = ResolveLoudestStemHash(rule, frame);
                frame.BiomeHash = rule.GroupBlend01 < 0.5f ? rule.CurrentBiomeHash : rule.TargetBiomeHash;
                frame.Flags = rule.Flags;
                frame.GroupBlend01 = rule.GroupBlend01;
                frame.Frame = FrameIndex;

                StemCommandDTO primary = default;
                primary.StemHash_A = rule.StemBaseHash;
                primary.Volume_A = frame.BaseVolume;
                primary.StemHash_B = rule.StemActionHash;
                primary.Volume_B = frame.ActionVolume;
                ref StemCommandDTO primaryCommand = ref UnsafeUtility.AsRef<StemCommandDTO>(Commands);
                primaryCommand = primary;

                StemCommandDTO secondary = default;
                secondary.StemHash_A = rule.StemDepthHash;
                secondary.Volume_A = frame.DepthVolume;
                secondary.StemHash_B = rule.StemBossHash;
                secondary.Volume_B = frame.BossVolume;
                ref StemCommandDTO secondaryCommand = ref UnsafeUtility.AsRef<StemCommandDTO>(Commands + 1);
                secondaryCommand = secondary;
            }

            private static uint ResolveLoudestStemHash(AudioStemRuleDTO rule, StemMixFrameDTO frame)
            {
                uint hash = rule.StemBaseHash;
                float loudest = frame.BaseVolume;
                if (frame.ActionVolume > loudest)
                {
                    hash = rule.StemActionHash;
                    loudest = frame.ActionVolume;
                }

                if (frame.DepthVolume > loudest)
                {
                    hash = rule.StemDepthHash;
                    loudest = frame.DepthVolume;
                }

                if (frame.BossVolume > loudest)
                    hash = rule.StemBossHash;

                return hash;
            }
        }
    
        #region JulesLink_ReverbPreDelayCalculator
        private static void JulesLink_ReverbPreDelayCalculator() { _ = typeof(Hecton8.PureLogic.Systems.ReverbPreDelayCalculator); }
        #endregion
}
}
