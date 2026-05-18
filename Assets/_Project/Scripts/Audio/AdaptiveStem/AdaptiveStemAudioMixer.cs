using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.Audio
{
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct AudioStemStateDTO
    {
        public float TensionIndex;
        public float DepthFilter;
        public uint ActiveStemHash;
        public uint _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct StemCommandDTO
    {
        public uint StemHash_A;
        public float Volume_A;
        public uint StemHash_B;
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
        [FieldOffset(28)] public uint _pad0;
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
        [FieldOffset(0)] public float AttackSeconds;
        [FieldOffset(4)] public float ReleaseSeconds;
        [FieldOffset(8)] public float CrossfadeSeconds;
        [FieldOffset(12)] public float BeatBpm;
        [FieldOffset(16)] public float BeatWindowSeconds;
        [FieldOffset(20)] public float DepthMinMeters;
        [FieldOffset(24)] public float DepthMaxMeters;
        [FieldOffset(28)] public float DepthFilterMinHz;
        [FieldOffset(32)] public float DepthFilterMaxHz;
        [FieldOffset(36)] public float CombatEnterThreshold;
        [FieldOffset(40)] public float CombatExitThreshold;
        [FieldOffset(44)] public float NarrativeOverrideWeight;
        [FieldOffset(48)] public float GlobalQualityWeight;
        [FieldOffset(52)] public float SystemHealth01;
        [FieldOffset(56)] public float IoPressure01;
        [FieldOffset(60)] public float Damage01;
        [FieldOffset(64)] public float OxygenDanger01;
        [FieldOffset(68)] public float MockPredatorMeters;
        [FieldOffset(72)] public float MockDepthMeters;
        [FieldOffset(76)] public float BiomeFadeSeconds;
        [FieldOffset(80)] public uint CurrentBiomeHash;
        [FieldOffset(84)] public uint TargetBiomeHash;
        [FieldOffset(88)] public ulong NarrativeStateMask;
        [FieldOffset(96)] public float MockPhaseSeconds;
        [FieldOffset(100)] public float KernelCadenceSeconds;
        [FieldOffset(104)] public uint StemBaseHash;
        [FieldOffset(108)] public uint StemActionHash;
        [FieldOffset(112)] public uint StemDepthHash;
        [FieldOffset(116)] public uint StemBossHash;
        [FieldOffset(120)] public float GroupBlend01;
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
        [FieldOffset(60)] public uint _pad0;
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
    public sealed unsafe class AdaptiveStemAudioMixer : MonoBehaviour, IUpdatable, ILateFrameTickable, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private const int TelemetryCapacity = 300;
        private const int CsvScratchBytes = 4096;
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
        private const ulong DefaultBossNarrativeMask = 1ul << 7;
        private const SystemID VaultOwner = SystemID.AudioStemMixer;
        private const int CsvPollSlowTickInterval = 2;
        private const string CsvDefaultRelativePath = "Docs/Audio/audio_stem_rules.csv";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_STEM_MIXER.bin";

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

        [Header("Cold Tuning")]
        [SerializeField] private string _csvRelativePath = CsvDefaultRelativePath;
        [SerializeField, Range(0f, 1f)] private float _mockDamage01;
        [SerializeField, Range(0f, 1f)] private float _mockOxygenDanger01;
        [SerializeField, Range(0f, 1f)] private float _mockQualityBias01 = 1f;
        [SerializeField, Min(0f)] private float _mockDepthAmplitudeMeters = 1200f;
        [SerializeField, Min(1f)] private float _mockPredatorCycleSeconds = 18f;
        [SerializeField] private ulong _bossNarrativeMask = DefaultBossNarrativeMask;
        [SerializeField] private bool _autoStartSources = true;

        private NativeArray<AudioStemStateDTO> _stemState; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<StemCommandDTO> _stemCommands; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<StemMixFrameDTO> _mixFrame; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<AudioStemRuleDTO> _rules; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<MockPredatorProximitySignal> _mockPredator; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<MockDepthSignal> _mockDepth; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<MockTensionSignal> _mockTension; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<AudioStemTelemetryEntry> _telemetryRing; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<int> _telemetryCursor; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<byte> _csvScratch; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<ScalabilityStateDTO> _scalabilityState; // Vault alias; HardwareHomeostasis owns backing memory.
        private IDataVault _dataVault;
        private VaultBufferHandle<AudioStemStateDTO> _stemStateHandle;
        private VaultBufferHandle<StemCommandDTO> _stemCommandsHandle;
        private VaultBufferHandle<StemMixFrameDTO> _mixFrameHandle;
        private VaultBufferHandle<AudioStemRuleDTO> _rulesHandle;
        private VaultBufferHandle<MockPredatorProximitySignal> _mockPredatorHandle;
        private VaultBufferHandle<MockDepthSignal> _mockDepthHandle;
        private VaultBufferHandle<MockTensionSignal> _mockTensionHandle;
        private VaultBufferHandle<AudioStemTelemetryEntry> _telemetryRingHandle;
        private VaultBufferHandle<int> _telemetryCursorHandle;
        private VaultBufferHandle<byte> _csvScratchHandle;
        private VaultBufferHandle<ScalabilityStateDTO> _scalabilityStateHandle;
        private string _resolvedCsvPath;
        private string _lastResolvedCsvRelativePath;
        private DateTime _lastCsvWriteUtc;
        private float _beatTimerSeconds;
        private float _kernelAccumulatorSeconds;
        private float _biomeBlend01;
        private float _ioDelaySeconds;
        private float _lastSystemHealth01 = 1f;
        private float _lastIoPressure01;
        private float _lastDamage01;
        private float _lastOxygenDanger01;
        private float _cachedGlobalQualityWeight = 1f;
        private uint _currentBiomeHash = DefaultBiomeHash;
        private uint _targetBiomeHash = DefaultBiomeHash;
        private ulong _narrativeStateMask;
        private int _registeredUpdate;
        private int _registeredLateFrame;
        private int _registeredSlowTick;
        private int _registeredHotSwap;
        private int _nativeAllocated;
        private int _telemetryDumped;
        private int _audioJobsPending;
        private int _csvPollCountdown;
        private uint _simulationFrameCounter;
        private uint _streamingFaultFlags;
        private JobHandle _audioJobHandle;

        public static bool TryGetActive(out AdaptiveStemAudioMixer mixer)
        {
            mixer = _activeInstance;
            return mixer != null && mixer._nativeAllocated != 0;
        }

        private void Awake()
        {
            EnsureVaultStorage();
            RefreshCsvPathCold();
            ConfigureSourcesCold();
            if (!ScanLegacyBinaryProfilesCold())
                GenerateEmergencyMockAudioProfiles();
        }

        private void OnEnable()
        {
            EnsureVaultStorage();
            RefreshCsvPathCold();
            ConfigureSourcesCold();
            _activeInstance = this;
            if (GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment))
                _registeredUpdate = 1;
            if (GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment))
                _registeredLateFrame = 1;
            if (GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment))
                _registeredSlowTick = 1;
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
            {
                EnsureVaultStorage();
                if (Volatile.Read(ref _nativeAllocated) == 0)
                    return;
            }

            if (!TryFlushCompletedAudioJobs())
                return;

            float safeDelta = math.clamp(deltaTime, MinAudioDeltaSeconds, MaxAudioDeltaSeconds);
            unchecked
            {
                _simulationFrameCounter++;
                if (_simulationFrameCounter == 0u)
                    _simulationFrameCounter = 1u;
            }

            DrainSignalInputs();
            UpdateBeatAndBiomeState(safeDelta);
            UpdateVaultRulesFromManagedState(safeDelta);
            ScheduleAudioKernels();
        }

        public void LateFrameTick()
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return;

            TryFlushCompletedAudioJobs();
        }

        public void SlowTick()
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return;

            TryRefreshScalabilityStateAliasCold();
            RefreshGlobalQualitySnapshotCold();
            PollCsvRulesCold();
            ValidateStreamingClipsCold();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Unknown)
                return;

            EnsureVaultStorage();
            TryRefreshScalabilityStateAliasCold();
            RefreshGlobalQualitySnapshotCold();
            _ = previousService;
            _ = currentService;
        }

        public bool TryGetEditorRule(out AudioStemRuleDTO rule)
        {
            if (Volatile.Read(ref _nativeAllocated) == 0 || !_rules.IsCreated)
            {
                rule = default;
                return false;
            }

            if (!TryFlushCompletedAudioJobs())
            {
                rule = default;
                return false;
            }

            rule = _rules[0];
            return true;
        }

        public bool TryWriteEditorRule(in AudioStemRuleDTO rule)
        {
            if (Volatile.Read(ref _nativeAllocated) == 0 || !_rules.IsCreated)
                return false;

            if (!TryFlushCompletedAudioJobs())
                return false;

            AudioStemRuleDTO sanitized = SanitizeRule(rule);
            _rules[0] = sanitized;
            return true;
        }

        public bool TryGetEditorMixFrame(out StemMixFrameDTO frame)
        {
            if (Volatile.Read(ref _nativeAllocated) == 0 || !_mixFrame.IsCreated)
            {
                frame = default;
                return false;
            }

            if (!TryFlushCompletedAudioJobs())
            {
                frame = default;
                return false;
            }

            frame = _mixFrame[0];
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
            if (Volatile.Read(ref _nativeAllocated) == 0 || !_telemetryRing.IsCreated || !_telemetryCursor.IsCreated)
                return false;

            if (!TryFlushCompletedAudioJobs())
                return false;

            int cursor = math.max(0, _telemetryCursor[0]);
            int offset = math.clamp(offsetFromNewest, 0, TelemetryCapacity - 1);
            int index = cursor - 1 - offset;
            while (index < 0)
                index += TelemetryCapacity;

            entry = _telemetryRing[index % TelemetryCapacity];
            return true;
        }

        private void UnregisterRuntime()
        {
            if (ReferenceEquals(_activeInstance, this))
                _activeInstance = null;
            if (Interlocked.Exchange(ref _registeredHotSwap, 0) != 0)
                GlobalRegistry.UnregisterHotSwapListener(this);
            if (Interlocked.Exchange(ref _registeredSlowTick, 0) != 0)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            if (Interlocked.Exchange(ref _registeredLateFrame, 0) != 0)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            if (Interlocked.Exchange(ref _registeredUpdate, 0) != 0)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            ForceFlushAudioJobsForShutdown();
        }

        private void EnsureVaultStorage()
        {
            if (Volatile.Read(ref _nativeAllocated) != 0 &&
                _stemState.IsCreated &&
                _stemCommands.IsCreated &&
                _mixFrame.IsCreated &&
                _rules.IsCreated &&
                _mockPredator.IsCreated &&
                _mockDepth.IsCreated &&
                _mockTension.IsCreated &&
                _telemetryRing.IsCreated &&
                _telemetryCursor.IsCreated &&
                _csvScratch.IsCreated)
            {
                return;
            }

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            _dataVault = vault;
            _stemStateHandle = vault.GetBufferHandle<AudioStemStateDTO>(
                BufferID.AudioStemState,
                1,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _stemCommandsHandle = vault.GetBufferHandle<StemCommandDTO>(
                BufferID.AudioStemCommands,
                2,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _mixFrameHandle = vault.GetBufferHandle<StemMixFrameDTO>(
                BufferID.AudioStemMixFrame,
                1,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _rulesHandle = vault.GetBufferHandle<AudioStemRuleDTO>(
                BufferID.AudioStemRules,
                1,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _mockPredatorHandle = vault.GetBufferHandle<MockPredatorProximitySignal>(
                BufferID.AudioStemMockPredator,
                1,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _mockDepthHandle = vault.GetBufferHandle<MockDepthSignal>(
                BufferID.AudioStemMockDepth,
                1,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _mockTensionHandle = vault.GetBufferHandle<MockTensionSignal>(
                BufferID.AudioStemMockTension,
                1,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _telemetryRingHandle = vault.GetBufferHandle<AudioStemTelemetryEntry>(
                BufferID.AudioStemTelemetry,
                TelemetryCapacity,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _telemetryCursorHandle = vault.GetBufferHandle<int>(
                BufferID.AudioStemTelemetryCursor,
                1,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _csvScratchHandle = vault.GetBufferHandle<byte>(
                BufferID.AudioStemCsvScratch,
                CsvScratchBytes,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);

            _stemState = _stemStateHandle.Resolve(vault);
            _stemCommands = _stemCommandsHandle.Resolve(vault);
            _mixFrame = _mixFrameHandle.Resolve(vault);
            _rules = _rulesHandle.Resolve(vault);
            _mockPredator = _mockPredatorHandle.Resolve(vault);
            _mockDepth = _mockDepthHandle.Resolve(vault);
            _mockTension = _mockTensionHandle.Resolve(vault);
            _telemetryRing = _telemetryRingHandle.Resolve(vault);
            _telemetryCursor = _telemetryCursorHandle.Resolve(vault);
            _csvScratch = _csvScratchHandle.Resolve(vault);

            if (!_stemState.IsCreated ||
                !_stemCommands.IsCreated ||
                !_mixFrame.IsCreated ||
                !_rules.IsCreated ||
                !_mockPredator.IsCreated ||
                !_mockDepth.IsCreated ||
                !_mockTension.IsCreated ||
                !_telemetryRing.IsCreated ||
                !_telemetryCursor.IsCreated ||
                !_csvScratch.IsCreated)
            {
                DisposeVaultStorage();
                return;
            }

            MemClearArray(_stemState);
            MemClearArray(_stemCommands);
            MemClearArray(_mixFrame);
            MemClearArray(_rules);
            MemClearArray(_mockPredator);
            MemClearArray(_mockDepth);
            MemClearArray(_mockTension);
            MemClearArray(_telemetryRing);
            MemClearArray(_telemetryCursor);
            MemClearArray(_csvScratch);
            TryRefreshScalabilityStateAliasCold();
            RefreshGlobalQualitySnapshotCold();
            Volatile.Write(ref _nativeAllocated, 1);
        }

        private void DisposeVaultStorage()
        {
            IDataVault vault = _dataVault;
            if (vault != null)
                vault.ReleaseOwnerBuffers(VaultOwner, out _);

            _stemState = default;
            _stemCommands = default;
            _mixFrame = default;
            _rules = default;
            _mockPredator = default;
            _mockDepth = default;
            _mockTension = default;
            _telemetryRing = default;
            _telemetryCursor = default;
            _csvScratch = default;
            _scalabilityState = default;
            _stemStateHandle = default;
            _stemCommandsHandle = default;
            _mixFrameHandle = default;
            _rulesHandle = default;
            _mockPredatorHandle = default;
            _mockDepthHandle = default;
            _mockTensionHandle = default;
            _telemetryRingHandle = default;
            _telemetryCursorHandle = default;
            _csvScratchHandle = default;
            _scalabilityStateHandle = default;
            _resolvedCsvPath = null;
            _lastResolvedCsvRelativePath = null;
            _dataVault = null;
            Volatile.Write(ref _nativeAllocated, 0);
        }

        private void ConfigureSourcesCold()
        {
            ConfigureSourceCold(_baseStemSource);
            ConfigureSourceCold(_actionStemSource);
            ConfigureSourceCold(_depthStemSource);
            ConfigureSourceCold(_bossStemSource);
            ValidateStreamingClipsCold();
        }

        private void TryRefreshScalabilityStateAliasCold()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                _scalabilityState = default;
                _scalabilityStateHandle = default;
                return;
            }

            if (!vault.TryGetBufferHandle<ScalabilityStateDTO>(BufferID.ShinobuScalabilityState, out _scalabilityStateHandle))
            {
                _scalabilityState = default;
                _scalabilityStateHandle = default;
                return;
            }

            _scalabilityState = _scalabilityStateHandle.Resolve(vault);
        }

        private void RefreshGlobalQualitySnapshotCold()
        {
            if (!_scalabilityState.IsCreated || _scalabilityState.Length == 0)
                return;

            ScalabilityStateDTO state = _scalabilityState[0];
            if (math.isfinite(state.GlobalQualityWeight))
                _cachedGlobalQualityWeight = math.saturate(state.GlobalQualityWeight);
        }

        private void ConfigureSourceCold(AudioSource source)
        {
            if (source == null)
                return;

            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = 0f;
        }

        private void ValidateStreamingClipsCold()
        {
            _streamingFaultFlags = 0u;
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
            try
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
            catch (Exception ex)
            {
                Debug.LogWarning("[SHINOBU_46] Legacy audio binary scan failed; emergency mock profiles are active. " + ex.Message);
                return false;
            }
        }

        private void GenerateEmergencyMockAudioProfiles()
        {
            if (Volatile.Read(ref _nativeAllocated) == 0 || !_rules.IsCreated)
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
            _rules[0] = rule;

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
            _stemCommands[0] = primary;
            _stemCommands[1] = secondary;
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

            ReadOnlySpan<SurvivalVitalsChangedSignal> vitalSignals = SignalBus<SurvivalVitalsChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < vitalSignals.Length; i++)
            {
                float oxygenDanger01 = 1f - math.saturate(vitalSignals[i].Oxygen01);
                _lastOxygenDanger01 = math.max(_lastOxygenDanger01, oxygenDanger01);
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

            ReadOnlySpan<ScalabilityChangedEvent> scalabilitySignals = SignalBus<ScalabilityChangedEvent>.GetFrameSnapshot();
            for (int i = 0; i < scalabilitySignals.Length; i++)
                _cachedGlobalQualityWeight = ResolveTierFallbackQualityWeight(scalabilitySignals[i].CurrentTier);
        }

        private void UpdateBeatAndBiomeState(float deltaTime)
        {
            AudioStemRuleDTO rule = _rules[0];
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
            _rules[0] = rule;

            StemMixFrameDTO frame = _mixFrame[0];
            frame.BeatPhase01 = beatPhase01;
            frame.Flags = flags;
            frame.GroupBlend01 = _biomeBlend01;
            _mixFrame[0] = frame;
        }

        private void UpdateVaultRulesFromManagedState(float deltaTime)
        {
            AudioStemRuleDTO rule = _rules[0];
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
            _rules[0] = SanitizeRule(rule);
        }

        private void ScheduleAudioKernels()
        {
            AudioStemRuleDTO rule = _rules[0];
            float cadence = math.max(MinAudioDeltaSeconds, rule.KernelCadenceSeconds);
            if (_kernelAccumulatorSeconds + KernelCadenceEpsilonSeconds < cadence)
            {
                WriteTelemetry(0f);
                return;
            }

            float kernelDeltaSeconds = math.clamp(_kernelAccumulatorSeconds, MinAudioDeltaSeconds, MaxAudioDeltaSeconds);
            _kernelAccumulatorSeconds = 0f;
            MockAudioStimulusJob mockJob = new MockAudioStimulusJob
            {
                Rules = (AudioStemRuleDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_rules),
                Predator = (MockPredatorProximitySignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_mockPredator),
                Depth = (MockDepthSignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_mockDepth),
                Tension = (MockTensionSignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_mockTension),
                FrameIndex = _simulationFrameCounter,
                DeltaSeconds = kernelDeltaSeconds,
                MockDepthAmplitudeMeters = _mockDepthAmplitudeMeters,
                MockPredatorCycleSeconds = _mockPredatorCycleSeconds
            };
            JobHandle mockHandle = mockJob.Schedule();
            JobHandle dependency = mockHandle;
            AudioStemTensionKernelJob tensionJob = new AudioStemTensionKernelJob
            {
                State = (AudioStemStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_stemState),
                Rules = (AudioStemRuleDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_rules),
                Predator = (MockPredatorProximitySignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_mockPredator),
                Depth = (MockDepthSignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_mockDepth),
                MockTension = (MockTensionSignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_mockTension),
                DeltaSeconds = kernelDeltaSeconds
            };
            JobHandle tensionHandle = tensionJob.Schedule(dependency);
            dependency = JobHandle.CombineDependencies(mockHandle, tensionHandle);

            StemCrossfadeSolverJob solverJob = new StemCrossfadeSolverJob
            {
                State = (AudioStemStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_stemState),
                Rules = (AudioStemRuleDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_rules),
                Commands = (StemCommandDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_stemCommands),
                MixFrame = (StemMixFrameDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_mixFrame),
                FrameIndex = _simulationFrameCounter,
                DeltaSeconds = kernelDeltaSeconds
            };
            _audioJobHandle = solverJob.Schedule(dependency);
            Volatile.Write(ref _audioJobsPending, 1);
        }

        private bool TryFlushCompletedAudioJobs()
        {
            if (Volatile.Read(ref _audioJobsPending) == 0)
                return true;

            if (!_audioJobHandle.IsCompleted)
                return false;

            long startTicks = Stopwatch.GetTimestamp();
            _audioJobHandle.Complete();
            Volatile.Write(ref _audioJobsPending, 0);
            ApplyMixFrameToUnityAudio();
            float elapsedMicroseconds = ResolveElapsedMicroseconds(startTicks);
            WriteTelemetry(elapsedMicroseconds);
            if (elapsedMicroseconds > MixerDumpThresholdMicroseconds || HasNonFiniteMixFrame())
                DumpTelemetryOnce();
            return true;
        }

        private void ForceFlushAudioJobsForShutdown()
        {
            if (Volatile.Read(ref _audioJobsPending) == 0)
                return;

            long startTicks = Stopwatch.GetTimestamp();
            _audioJobHandle.Complete();
            Volatile.Write(ref _audioJobsPending, 0);
            if (_mixFrame.IsCreated && _telemetryRing.IsCreated && _telemetryCursor.IsCreated)
            {
                float elapsedMicroseconds = ResolveElapsedMicroseconds(startTicks);
                WriteTelemetry(elapsedMicroseconds);
            }
        }

        private void ApplyMixFrameToUnityAudio()
        {
            StemMixFrameDTO frame = _mixFrame[0];
            ApplyVolume(_baseStemSource, frame.BaseVolume);
            ApplyVolume(_actionStemSource, frame.ActionVolume);
            ApplyVolume(_depthStemSource, frame.DepthVolume);
            ApplyVolume(_bossStemSource, frame.BossVolume);
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

        private void WriteTelemetry(float elapsedMicroseconds)
        {
            if (!_telemetryRing.IsCreated || !_telemetryCursor.IsCreated)
                return;

            StemMixFrameDTO frame = _mixFrame[0];
            AudioStemRuleDTO rule = _rules[0];
            int cursor = _telemetryCursor[0];
            int index = cursor % TelemetryCapacity;
            AudioStemTelemetryEntry entry = default;
            entry.Frame = frame.Frame != 0u ? frame.Frame : _simulationFrameCounter;
            entry.ActiveStemHash = frame.ActiveStemHash;
            entry.BiomeHash = frame.BiomeHash;
            entry.Flags = frame.Flags;
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
            _telemetryRing[index] = entry;
            _telemetryCursor[0] = (cursor + 1) % TelemetryCapacity;
        }

        private bool HasNonFiniteMixFrame()
        {
            StemMixFrameDTO frame = _mixFrame[0];
            if (!math.isfinite(frame.TensionIndex) ||
                !math.isfinite(frame.DepthFilter) ||
                !math.isfinite(frame.CutoffHz) ||
                !math.isfinite(frame.BaseVolume) ||
                !math.isfinite(frame.ActionVolume) ||
                !math.isfinite(frame.DepthVolume) ||
                !math.isfinite(frame.BossVolume))
            {
                frame.Flags |= FlagNonFinite;
                _mixFrame[0] = frame;
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
                string repoRoot = ResolveRepoRootPath();
                string dumpPath = Path.Combine(repoRoot, DumpRelativePath);
                string directory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                void* source = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_telemetryRing);
                int byteCount = TelemetryCapacity * UnsafeUtility.SizeOf<AudioStemTelemetryEntry>();
                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    stream.Write(new ReadOnlySpan<byte>(source, byteCount));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SHINOBU_46] Failed to dump adaptive stem telemetry. " + ex.Message);
            }
        }

        private void PollCsvRulesCold()
        {
            if (!_csvScratch.IsCreated)
                return;

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

            _lastCsvWriteUtc = writeTime;
            try
            {
                int bytesRead;
                byte* scratch = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_csvScratch);
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int safeLength = (int)math.min(stream.Length, CsvScratchBytes);
                    Span<byte> scratchSpan = new Span<byte>(scratch, safeLength);
                    bytesRead = stream.Read(scratchSpan);
                }

                ParseCsvRules(bytesRead);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SHINOBU_46] audio_stem_rules.csv parse failed. " + ex.Message);
            }
        }

        private void ParseCsvRules(int byteCount)
        {
            if (byteCount <= 0)
                return;

            AudioStemRuleDTO rule = _rules[0];
            int index = 0;
            while (index < byteCount)
            {
                while (index < byteCount && IsLineBreakOrSpace(_csvScratch[index]))
                    index++;
                if (index >= byteCount)
                    break;

                if (_csvScratch[index] == (byte)'#')
                {
                    while (index < byteCount && !IsLineBreak(_csvScratch[index]))
                        index++;
                    continue;
                }

                int keyStart = index;
                while (index < byteCount && _csvScratch[index] != (byte)',' && !IsLineBreak(_csvScratch[index]))
                    index++;

                int keyEnd = index;
                if (index >= byteCount || _csvScratch[index] != (byte)',')
                {
                    while (index < byteCount && !IsLineBreak(_csvScratch[index]))
                        index++;
                    continue;
                }

                index++;
                int valueStart = index;
                while (index < byteCount && !IsLineBreak(_csvScratch[index]))
                    index++;

                if (TryParseFloat(_csvScratch, valueStart, index, out float value))
                {
                    uint hash = HashCsvKey(_csvScratch, keyStart, keyEnd);
                    ApplyCsvRule(ref rule, hash, value);
                }
            }

            _rules[0] = SanitizeRule(rule);
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

        private static bool TryParseFloat(NativeArray<byte> bytes, int start, int end, out float value)
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

        private static uint HashCsvKey(NativeArray<byte> bytes, int start, int end)
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

        private ulong ResolveBossMask()
        {
            return _bossNarrativeMask == 0ul ? DefaultBossNarrativeMask : _bossNarrativeMask;
        }

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
            if (_scalabilityState.IsCreated && _scalabilityState.Length > 0)
            {
                ScalabilityStateDTO state = _scalabilityState[0];
                if (math.isfinite(state.GlobalQualityWeight))
                {
                    _cachedGlobalQualityWeight = math.saturate(state.GlobalQualityWeight);
                    return _cachedGlobalQualityWeight;
                }
            }

            return math.saturate(_cachedGlobalQualityWeight);
        }

        private static float ResolveTierFallbackQualityWeight(byte tierProfile)
        {
            return ScalabilityTierProfiles.Normalize(tierProfile) == ScalabilityTierProfiles.LowMx350 ? 0.1f : 1f;
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
                float depth01 = 0.5f + 0.5f * math.sin(depthPhase * math.PI * 2f);
                float depthMeters = depth01 * math.max(0f, FiniteOrFallback(MockDepthAmplitudeMeters, DefaultDepthMaxMeters));

                predator.ProximityMeters = proximityMeters;
                predator.Proximity01 = math.saturate(proximity01);
                predator.OscillationPhase = phase;
                predator.DamageSpike01 = math.saturate(rule.Damage01);
                predator.Frame = FrameIndex;
                predator.SourceHash = StemActionHash;
                predator.Flags = 0u;
                predator._pad0 = 0u;

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
                state._pad0 = 0u;
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
    }
}
