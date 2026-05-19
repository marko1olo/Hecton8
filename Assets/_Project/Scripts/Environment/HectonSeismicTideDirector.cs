using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Environment
{
    /// <summary>
    /// Vault-owned seismic event slot. Size: 40 bytes, default platform packing, no Pack=1.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 40)]
    public struct SeismicEventDTO
    {
        public double3 EpicenterAUP;
        public float Magnitude;
        public float Frequency;
        public float DecayRate;
        public uint EventTypeHash;
    }

    /// <summary>
    /// Raw render/VR pipeline shake output. Size: 32 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct ShakeOffsetDTO
    {
        public float3 TranslationOffset;
        public float3 RotationEuler;
        public ulong _pad0;
    }

    /// <summary>
    /// Human-editable seismic tuning stored in unmanaged vault memory.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct SeismicTuningDTO
    {
        public const uint FlagVrComfortMode = 1u << 0;
        public const uint FlagSineOnly = 1u << 1;

        public float MaxTranslationMeters;
        public float NoiseFrequency;
        public float DecayRate;
        public float SiltMultiplier;
        public float MaxRotationRadians;
        public float SystemHealthIndex;
        public float DamageThreshold;
        public float MaxTurbiditySpike;
        public float ShockwaveRadiusPerMagnitude;
        public float MockTriggerProbability;
        public float MinimumMagnitude;
        public float Reserved0;
        public uint Flags;
        public uint Seed;
        public uint Reserved1;
        public uint Reserved2;
    }

    /// <summary>
    /// One black-box seismic frame. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SeismicDirectorTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ActiveQuakeCount;
        [FieldOffset(8)] public float MaxMagnitudeGenerated;
        [FieldOffset(12)] public float OscillatorComputeTimeMs;
        [FieldOffset(16)] public float3 TranslationOffset;
        [FieldOffset(28)] public float TurbiditySpike;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint Sequence;
        [FieldOffset(40)] public uint EventHash;
        [FieldOffset(44)] public uint Padding0;
        [FieldOffset(48)] public ulong PositionHash;
        [FieldOffset(56)] public ulong Padding1;
    }

    /// <summary>
    /// Isolation camera packet used when the real player/camera pipeline is absent.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct MockCameraPosition
    {
        public double3 AUP;
        public float3 Forward;
        public float3 Up;
        public uint Frame;
        public uint Flags;
    }

    /// <summary>
    /// Isolation silt packet proving turbidity math without the real VFX system.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct MockSiltSignal
    {
        public float TurbiditySpike;
        public float3 UpwardVelocity;
        public uint Frame;
        public uint Flags;
        public uint Reserved;
    }

    /// <summary>
    /// Mock WFC base module row used when the real structural hash is not visible.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct SeismicBaseModuleMock
    {
        public double3 AUP;
        public uint ModuleHash;
        public float DamageThreshold;
        public float LastShockwave;
        public uint Flags;
        public uint Reserved;
    }

    /// <summary>
    /// Constants shared by runtime and editor seismic tooling.
    /// </summary>
    public static class SeismicDirectorConstants
    {
        public const int MaxQuakeSlots = 16;
        public const int TelemetryFrames = 300;
        public const int MockBaseModuleSlots = 8;
        public const int CsvBufferBytes = 4096;
        public const float VrComfortTranslationMeters = 0.05f;
        public const float SevereMagnitude = 8f;
        public const uint EmergencyFaultHash = 0x51464B45u;
        public const uint NarrativeMockHash = 0x4E415252u;
        public const uint TectonicDebrisHash = 0x54454344u;
        public const uint AcousticShockHash = 0x53484F43u;
        public const uint PanicShockHash = 0x50414E43u;
        public const string DumpPath = "Docs/AgentLogs/Dump_SEISMIC_DIRECTOR.bin";
        public const SystemID SeismicSystemId = (SystemID)74;
        public const BufferID TideTelemetryBuffer = (BufferID)70099;
        public const BufferID EventSlotsBuffer = (BufferID)70100;
        public const BufferID ShakeOffsetBuffer = (BufferID)70101;
        public const BufferID TurbiditySpikeBuffer = (BufferID)70102;
        public const BufferID TelemetryRingBuffer = (BufferID)70103;
        public const BufferID TuningBuffer = (BufferID)70104;
        public const BufferID MockNarrativeTriggerBuffer = (BufferID)70105;
        public const BufferID MockCameraPositionBuffer = (BufferID)70106;
        public const BufferID MockSiltSignalBuffer = (BufferID)70107;
        public const BufferID MockBaseModulesBuffer = (BufferID)70108;
    }

    /// <summary>
    /// Allocation-free parser for seismic profile override bytes.
    /// </summary>
    public static class SeismicCsvProfileParser
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const uint MaxTranslationHash = 0x604BC398u;
        private const uint NoiseFrequencyHash = 0x02E3357Du;
        private const uint DecayRateHash = 0x14416B1Fu;
        private const uint SiltMultiplierHash = 0x352B8DCAu;

        public static bool TryApply(byte[] bytes, int length, ref SeismicTuningDTO tuning)
        {
            if (bytes == null || length <= 0 || length > bytes.Length)
                return false;

            bool applied = false;
            int index = 0;
            while (index < length)
            {
                SkipLineTerminators(bytes, length, ref index);
                int keyStart = index;
                while (index < length && bytes[index] != (byte)',' && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                    index++;

                int keyEnd = index;
                if (index >= length || bytes[index] != (byte)',')
                {
                    SkipLine(bytes, length, ref index);
                    continue;
                }

                index++;
                if (!TryParseFloat(bytes, length, ref index, out float value))
                {
                    SkipLine(bytes, length, ref index);
                    continue;
                }

                uint hash = HashKey(bytes, keyStart, keyEnd - keyStart);
                if (hash == MaxTranslationHash && math.isfinite(value))
                {
                    tuning.MaxTranslationMeters = math.clamp(value, 0f, 5f);
                    applied = true;
                }
                else if (hash == NoiseFrequencyHash && math.isfinite(value))
                {
                    tuning.NoiseFrequency = math.clamp(value, 0.1f, 64f);
                    applied = true;
                }
                else if (hash == DecayRateHash && math.isfinite(value))
                {
                    tuning.DecayRate = math.clamp(value, 0.001f, 5f);
                    applied = true;
                }
                else if (hash == SiltMultiplierHash && math.isfinite(value))
                {
                    tuning.SiltMultiplier = math.clamp(value, 0f, 16f);
                    applied = true;
                }

                SkipLine(bytes, length, ref index);
            }

            return applied;
        }

        private static uint HashKey(byte[] bytes, int start, int count)
        {
            uint hash = FnvOffset;
            int end = start + count;
            for (int i = start; i < end; i++)
            {
                byte value = bytes[i];
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);
                if (value == (byte)'_' || value == (byte)' ' || value == (byte)'\t')
                    continue;

                hash = (hash ^ value) * FnvPrime;
            }

            return hash;
        }

        private static bool TryParseFloat(byte[] bytes, int length, ref int index, out float value)
        {
            value = 0f;
            SkipSpaces(bytes, length, ref index);
            bool negative = false;
            if (index < length && bytes[index] == (byte)'-')
            {
                negative = true;
                index++;
            }

            float integer = 0f;
            bool hasDigit = false;
            while (index < length && bytes[index] >= (byte)'0' && bytes[index] <= (byte)'9')
            {
                integer = integer * 10f + (bytes[index] - (byte)'0');
                index++;
                hasDigit = true;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (index < length && bytes[index] == (byte)'.')
            {
                index++;
                while (index < length && bytes[index] >= (byte)'0' && bytes[index] <= (byte)'9')
                {
                    fraction = fraction * 10f + (bytes[index] - (byte)'0');
                    divisor *= 10f;
                    index++;
                    hasDigit = true;
                }
            }

            if (!hasDigit)
                return false;

            value = integer + fraction / math.max(1f, divisor);
            if (negative)
                value = -value;
            return true;
        }

        private static void SkipSpaces(byte[] bytes, int length, ref int index)
        {
            while (index < length && (bytes[index] == (byte)' ' || bytes[index] == (byte)'\t'))
                index++;
        }

        private static void SkipLineTerminators(byte[] bytes, int length, ref int index)
        {
            while (index < length && (bytes[index] == (byte)'\n' || bytes[index] == (byte)'\r'))
                index++;
        }

        private static void SkipLine(byte[] bytes, int length, ref int index)
        {
            while (index < length && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                index++;
            SkipLineTerminators(bytes, length, ref index);
        }
    }

    /// <summary>
    /// Deterministic macro-world tide and seismic director. Physical outcomes are emitted as presentation signals.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Environment/Seismic Tide Director")]
    public sealed class HectonSeismicTideDirector : MonoBehaviour, ISeismicDirector, IUpdatable, ISlowTickable, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown
    {
        private const int TelemetryCapacity = 300;
        private const int SeismicTuningSlots = 1;
        private const int SeismicOutputSlots = 1;
        private const int SeismicMockSignalSlots = 1;
        private const float TidePeriod11Hours = 11f * 3600f;
        private const float TidePeriod17Hours = 17f * 3600f;
        private const float TidePeriod23Hours = 23f * 3600f;
        private const double TidePeriod11HoursRcp = 1d / TidePeriod11Hours;
        private const double TidePeriod17HoursRcp = 1d / TidePeriod17Hours;
        private const double TidePeriod23HoursRcp = 1d / TidePeriod23Hours;
        private const double HourSecondsRcp = 1d / 3600d;
        private const float TwoPi = 6.28318530718f;
        private const float VectorNormalizeEpsilonSq = 0.000001f;
        private const float Hash24ToUnit = 1f / 16777216f;
        private const float HighTremorThreshold = 0.8f;
        private const float AbyssDepthY = -500f;
        private const double ShaderShakeLodHysteresisSeconds = 2.5d;
        private const uint DefaultWorldSeed = 0x8E1571D5u;
        private const uint RockfallSpeciesHash = 0x5246434Cu;
        private const uint SubLowRumbleHash = 0x5355424Cu;
        private const uint SeismicDirectorSourceHash = 0x53454953u;
        private const string TelemetryDumpPath = "Docs/AgentLogs/Dump_WORLD_SEISMIC_GENERATOR.bin";

        private static readonly int _HectonWorldShakeId = Shader.PropertyToID("_HectonWorldShake");

        [Header("Tide")]
        [SerializeField, Min(0f), Tooltip("Peak deterministic tide displacement in meters before harmonic weighting.")]
        private float tideAmplitudeMeters = 3.5f;

        [Header("Seismic Presentation")]
        [SerializeField, Range(0f, 1f), Tooltip("Low-amplitude deterministic tremor floor used between hour-bucket quake events.")]
        private float microTremorIntensity = 0.08f;

        [SerializeField, Range(0f, 1f), Tooltip("Per-hour deterministic chance that the current world-seed bucket produces a visible quake.")]
        private float tremorEventProbability = 0.28f;

        [SerializeField, Min(0f), Tooltip("Maximum CoreLit world-space vertex offset in meters for non-low tiers.")]
        private float shaderShakeMaxMeters = 0.08f;

        [SerializeField, Min(0f), Tooltip("Camera micro-jitter scalar published through SeismicSignal.")]
        private float cameraJitterScale = 0.24f;

        [SerializeField, Min(0f), Tooltip("Audio rumble scalar published through ImpactSignal.")]
        private float audioRumbleScale = 0.9f;

        private IDataVault _dataVault;
        private VaultBufferHandle<SeismicTideTelemetryEntry> _tideTelemetryHandle;
        private VaultBufferHandle<SeismicEventDTO> _seismicEventsHandle;
        private VaultBufferHandle<ShakeOffsetDTO> _shakeOffsetHandle;
        private VaultBufferHandle<float> _turbiditySpikeHandle;
        private VaultBufferHandle<SeismicDirectorTelemetryEntry> _seismicTelemetryHandle;
        private VaultBufferHandle<SeismicTuningDTO> _seismicTuningHandle;
        private VaultBufferHandle<MockNarrativeTriggerSignal> _mockNarrativeTriggerHandle;
        private VaultBufferHandle<MockCameraPosition> _mockCameraHandle;
        private VaultBufferHandle<MockSiltSignal> _mockSiltHandle;
        private VaultBufferHandle<SeismicBaseModuleMock> _mockBaseModuleHandle;
        private ITickDispatcher _tickDispatcher;
        private IWorldSeedProvider _worldSeedProvider;
        private IPlayerRuntimeContext _playerRuntime;
        private CelestialRuntimeSnapshot _celestialSnapshot;
        private HectonQualityTier _scalabilityTier = HectonQualityTier.Unknown;
        private MathPrecisionLevel _mathPrecision = MathPrecisionLevel.Low;
        private double _fallbackAbsoluteUniverseTime;
        private uint _cachedWorldSeed = DefaultWorldSeed;
        private bool _isInitialized;
        private bool _registeredUpdatable;
        private bool _registeredSlowTickable;
        private bool _registeredLateFrameTickable;
        private bool _registeredService;
        private bool _seismicVaultReady;
        private bool _seismicSignalLanesPrewarmed;
        private bool _legacyFaultBinaryScanned;
        private bool _emergencyFaultsGenerated;
        private bool _oscillatorJobScheduled;
        private bool _dumpedSeismicDirectorTelemetry;
        private bool _dumpedInvalidTelemetry;
        private bool _lowMemoryProfile = true;
        private bool _shaderShakeDisabled = true;
        private bool _hasShaderShakeState;
        private bool _hasPendingShaderShakeState;
        private bool _pendingShaderShakeDisabled;
        private int _telemetryWriteIndex;
        private int _seismicTelemetryWriteIndex;
        private int _lastScheduledTelemetryIndex = -1;
        private int _tickCount;
        private int _lastCollapseHourBucket = int.MinValue;
        private double _nextCsvPollTime;
        private double _shaderShakeLodSwitchTime;
        private DateTime _lastCsvWriteUtc;
        private JobHandle _oscillatorJob;
        private uint _sequence;
        private uint _seismicEventSequence;
        private SeismicRuntimeSnapshot _snapshot;
        private TideSolveResult _cachedTide;
        private Vector4 _lastWorldShake;
        private bool _hasCachedTide;
        private readonly byte[] _csvReadBuffer = new byte[SeismicDirectorConstants.CsvBufferBytes]; // COLD ALLOC: byte[4096] - editor seismic CSV override buffer - owner: HectonSeismicTideDirector

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public float SeismicIntensity01 => _snapshot.SeismicIntensity01;

        /// <inheritdoc />
        public float3 SeismicDirection => _snapshot.SeismicDirection;

        /// <inheritdoc />
        public float TideHeightMeters => _snapshot.TideHeightMeters;

        /// <inheritdoc />
        public float TideHigh01 => _snapshot.TideHigh01;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _isInitialized;

        /// <inheritdoc />
        public int TickCount => _tickCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticShaderState()
        {
            Shader.SetGlobalVector(_HectonWorldShakeId, Vector4.zero);
        }

        /// <summary>
        /// Ensures the bootstrap-owned runtime component exists without scene-wide object discovery.
        /// </summary>
        public static HectonSeismicTideDirector EnsureRuntimeInstance()
        {
            HectonSeismicTideDirector registered = GlobalRegistry.SeismicDirector as HectonSeismicTideDirector;
            if (registered != null)
                return registered;

            GameObject runtimeRoot = new GameObject("[HectonSeismicTideDirector]"); // COLD ALLOC: GameObject[1] - bootstrap-owned seismic tide runtime root - owner: HectonSeismicTideDirector
            return runtimeRoot.AddComponent<HectonSeismicTideDirector>();
        }

        /// <summary>
        /// Explicit bootstrap entry point.
        /// </summary>
        public void InitializeService()
        {
            ISeismicDirector registered = GlobalRegistry.SeismicDirector;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                enabled = false;
                return;
            }

            RefreshCachedRuntimeState();
            EnsureTelemetryRing();
            EnsureSeismicVaultBuffers();
            PrewarmSeismicSignalLanes();
            if (!_registeredService)
            {
                GlobalRegistry.RegisterSeismicDirector(this);
                _registeredService = ReferenceEquals(GlobalRegistry.SeismicDirector, this);
            }

            _isInitialized = _registeredService;
            TryRegisterTickLanes();
            EvaluateAndPublish(refreshTide: true, publishSignals: false, publishCelestial: true);
        }

        /// <inheritdoc />
        public SeismicRuntimeSnapshot GetRuntimeSnapshot()
        {
            return _snapshot;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!_isInitialized)
                return;

            _ = deltaTime;
            _tickCount++;
            EvaluateAndPublish(refreshTide: false, publishSignals: false, publishCelestial: false);
            ScheduleSeismicOscillator(deltaTime);
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            if (!_isInitialized)
                return;

            RefreshCachedRuntimeState();
            EnsureTelemetryRing();
            EnsureSeismicVaultBuffers();
            ExecuteMockNarrativeTrigger();
#if UNITY_EDITOR
            TryPollCsvProfileOverrides();
#endif
            EvaluateAndPublish(refreshTide: true, publishSignals: true, publishCelestial: true);
            WriteTelemetryEntry();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            CompleteSeismicOscillatorJob();
        }

        private void OnEnable()
        {
            if (_isInitialized)
            {
                RefreshCachedRuntimeState();
                TryRegisterTickLanes();
            }
        }

        private void OnDisable()
        {
            CompleteSeismicOscillatorJob();
            TryUnregisterTickLanes();
            if (_registeredService)
            {
                GlobalRegistry.UnregisterSeismicDirector(this);
                _registeredService = false;
            }

            _isInitialized = false;
            PushWorldShake(Vector4.zero);
            ClearCachedRuntimeState();
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        /// <inheritdoc />
        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            CompleteSeismicOscillatorJob();
            TryUnregisterTickLanes();
            if (_registeredService)
            {
                GlobalRegistry.UnregisterSeismicDirector(this);
                _registeredService = false;
            }

            _isInitialized = false;
            PushWorldShake(Vector4.zero);
            DisposeTelemetryRing();
            ClearCachedRuntimeState();
        }

        private void TryRegisterTickLanes()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredUpdatable)
                _registeredUpdatable = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);

            if (!_registeredSlowTickable)
                _registeredSlowTickable = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);

            if (!_registeredLateFrameTickable)
                _registeredLateFrameTickable = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterTickLanes()
        {
            if (_registeredUpdatable)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredUpdatable = false;
            }

            if (_registeredSlowTickable)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTickable = false;
            }

            if (_registeredLateFrameTickable)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTickable = false;
            }
        }

        private void EvaluateAndPublish(bool refreshTide, bool publishSignals, bool publishCelestial)
        {
            double h8Time = ResolveH8TimeSeconds();
            int hourBucket = ResolveHourBucket(h8Time);
            uint seed = LCG_Hash(ResolveWorldSeed() + unchecked((uint)hourBucket));
            TideSolveResult tide = ResolveTideSolve(h8Time, seed, refreshTide);
            SeismicSolveResult seismic = EvaluateSeismicStateBurst(h8Time, seed, microTremorIntensity, tremorEventProbability);

            bool hasPlayerAup = TryResolvePlayerAup(out AbsoluteUniversePosition playerAup);
            bool abyssDepth = false;
            if (hasPlayerAup)
            {
                double3 playerAbsolute = playerAup.ToAbsoluteDouble3();
                abyssDepth = math.isfinite(playerAbsolute.y) && playerAbsolute.y < AbyssDepthY;
            }

            bool lowTier = IsLowTierShaderShakeDisabled();
            float cameraJitter = math.saturate(seismic.Intensity01 * cameraJitterScale * (abyssDepth ? 0.5f : 1f));
            float audioRumble = math.saturate(seismic.Intensity01 * audioRumbleScale * (abyssDepth ? 1.5f : 1f));
            float thermalScalar = seismic.Intensity01 > HighTremorThreshold ? 2f : 1f;
            uint flags = (uint)(SeismicRuntimeFlags.Valid);
            if (lowTier)
                flags |= (uint)SeismicRuntimeFlags.LowTierShaderShakeDisabled;
            if (abyssDepth)
                flags |= (uint)SeismicRuntimeFlags.AbyssDepthAttenuation;
            if (seismic.Intensity01 > HighTremorThreshold)
                flags |= (uint)SeismicRuntimeFlags.HighTremor;

            _sequence++;
            _snapshot = new SeismicRuntimeSnapshot
            {
                AbsoluteUniverseTime = h8Time,
                SeismicDirection = seismic.Direction,
                SeismicIntensity01 = seismic.Intensity01,
                TideHeightMeters = tide.HeightMeters,
                TideHigh01 = tide.High01,
                CameraJitter01 = cameraJitter,
                AudioRumble01 = audioRumble,
                ThermalEruptionProbabilityScalar = thermalScalar,
                Flags = flags,
                Sequence = _sequence
            };

            if (!IsSnapshotFinite(in _snapshot))
            {
                DumpTelemetryRingOnce();
                _snapshot = default;
                _hasCachedTide = false;
                PushWorldShake(Vector4.zero);
                return;
            }

            if (publishCelestial)
                PublishCelestialTideSnapshot(h8Time, in tide);

            PublishShaderWorldShake(in seismic, lowTier);

            if (!publishSignals)
                return;

            PublishSeismicSignal(cameraJitter, audioRumble, thermalScalar, abyssDepth, lowTier);
            PublishRumbleSignal(audioRumble, hasPlayerAup, in playerAup);

            if (seismic.Intensity01 > HighTremorThreshold && _lastCollapseHourBucket != hourBucket)
            {
                _lastCollapseHourBucket = hourBucket;
                _snapshot.Flags |= (uint)SeismicRuntimeFlags.CollapseDebrisQueued;
                PublishRockfallDebris(seed, hasPlayerAup, in playerAup);
            }
        }

        private TideSolveResult ResolveTideSolve(double h8Time, uint seed, bool refreshTide)
        {
            if (refreshTide || !_hasCachedTide)
            {
                _cachedTide = EvaluateTideHarmonicsBurst(h8Time, seed, math.max(0f, tideAmplitudeMeters));
                _hasCachedTide = true;
            }

            return _cachedTide;
        }

        private void PublishCelestialTideSnapshot(double h8Time, in TideSolveResult tide)
        {
            CelestialRuntimeSnapshot celestial = _celestialSnapshot;
            celestial.AbsoluteUniverseTime = h8Time;
            celestial.TideHeightMeters = tide.HeightMeters;
            celestial.TideHigh01 = tide.High01;
            celestial.TidePullVector = tide.PullDirection;
            celestial.Flags |= (uint)CelestialRuntimeFlags.Valid;
            if (tide.High01 >= 0.66f)
                celestial.Flags |= (uint)CelestialRuntimeFlags.HighTide;
            else
                celestial.Flags &= ~(uint)CelestialRuntimeFlags.HighTide;

            celestial.Sequence = unchecked(celestial.Sequence + 1u);
            _celestialSnapshot = celestial;
            GlobalRegistry.PublishCelestialRuntimeSnapshot(in celestial);
        }

        private void PublishShaderWorldShake(in SeismicSolveResult seismic, bool lowTier)
        {
            if (lowTier || seismic.Intensity01 <= 0.0001f || shaderShakeMaxMeters <= 0f)
            {
                PushWorldShake(Vector4.zero);
                return;
            }

            float displacement = math.saturate(seismic.Intensity01) * math.max(0f, shaderShakeMaxMeters);
            float3 shake = seismic.Direction * displacement;
            PushWorldShake(new Vector4(shake.x, shake.y, shake.z, seismic.Intensity01));
        }

        private void PushWorldShake(Vector4 value)
        {
            if (ApproximatelyEqual(_lastWorldShake, value))
                return;

            Shader.SetGlobalVector(_HectonWorldShakeId, value);
            _lastWorldShake = value;
        }

        private void PublishSeismicSignal(float cameraJitter, float audioRumble, float thermalScalar, bool abyssDepth, bool lowTier)
        {
            byte depthFlags = abyssDepth ? (byte)1 : (byte)0;
            byte flags = lowTier ? (byte)2 : (byte)0;
            SeismicSignal signal = new SeismicSignal
            {
                Direction = _snapshot.SeismicDirection,
                Intensity01 = _snapshot.SeismicIntensity01,
                CameraJitter01 = cameraJitter,
                AudioIntensity01 = audioRumble,
                ThermalEruptionProbabilityScalar = thermalScalar,
                Sequence = unchecked((ushort)_sequence),
                DepthFlags = depthFlags,
                Flags = flags
            };
            GlobalSignals.Publish(in signal);
        }

        private void PublishRumbleSignal(float audioRumble, bool hasPlayerAup, in AbsoluteUniversePosition playerAup)
        {
            if (audioRumble <= 0.001f)
                return;

            AbsoluteUniversePosition pointAup = hasPlayerAup
                ? playerAup
                : AbsoluteUniversePosition.FromAbsolutePosition(new double3(0d, -250d, 0d));

            ImpactSignal signal = new ImpactSignal
            {
                PointAup = pointAup,
                Force = audioRumble * 8000f,
                Intensity = audioRumble,
                MaterialHash = SubLowRumbleHash,
                WeightClass = 3,
                PrimaryMaterialId = 0,
                SecondaryMaterialId = 0,
                Flags = 1
            };
            GlobalSignals.Publish(in signal);
        }

        private void PublishRockfallDebris(uint seed, bool hasPlayerAup, in AbsoluteUniversePosition playerAup)
        {
            AbsoluteUniversePosition originAup = hasPlayerAup
                ? playerAup
                : AbsoluteUniversePosition.FromAbsolutePosition(new double3(0d, -250d, 0d));
            double3 origin = originAup.ToAbsoluteDouble3();
            float intensity = _snapshot.SeismicIntensity01;
            if (!math.isfinite(intensity) || intensity <= 0.001f)
                return;

            for (int i = 0; i < 3; i++)
            {
                uint debrisSeed = LCG_Hash(seed ^ unchecked((uint)(0x9E3779B9u + i * 0x45D9F3Bu)));
                float angle = Hash01(debrisSeed) * TwoPi;
                math.sincos(angle, out float angleSin, out float angleCos);
                float radius = math.lerp(18f, 54f, Hash01(debrisSeed ^ 0xB5297A4Du));
                float vertical = math.lerp(-5f, 11f, Hash01(debrisSeed ^ 0x68E31DA4u));
                double3 offset = new double3(angleCos * radius, vertical, angleSin * radius);
                DebrisSpawnSignal debris = new DebrisSpawnSignal
                {
                    PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(origin + offset),
                    SpeciesHash = RockfallSpeciesHash,
                    SourceEntityId = debrisSeed,
                    Intensity01 = intensity,
                    DebrisKind = DebrisSpawnSignal.DebrisKindRockShard,
                    Flags = DebrisSpawnSignal.FlagComputeShard
                };
                SignalBus<DebrisSpawnSignal>.Push(in debris);
            }
        }

        private bool EnsureSeismicVaultBuffers()
        {
            if (!ValidateSeismicLayouts())
            {
                _seismicVaultReady = false;
                DumpSeismicDirectorTelemetryOnce();
                return false;
            }

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                _seismicVaultReady = false;
                return false;
            }

            NativeArray<SeismicEventDTO> events = vault.GetBuffer<SeismicEventDTO>(
                SeismicDirectorConstants.EventSlotsBuffer,
                SeismicDirectorConstants.MaxQuakeSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
            _seismicEventsHandle = vault.GetBufferHandle<SeismicEventDTO>(
                SeismicDirectorConstants.EventSlotsBuffer,
                SeismicDirectorConstants.MaxQuakeSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
            _shakeOffsetHandle = vault.GetBufferHandle<ShakeOffsetDTO>(
                SeismicDirectorConstants.ShakeOffsetBuffer,
                SeismicOutputSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
            _turbiditySpikeHandle = vault.GetBufferHandle<float>(
                SeismicDirectorConstants.TurbiditySpikeBuffer,
                SeismicOutputSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
            _seismicTelemetryHandle = vault.GetBufferHandle<SeismicDirectorTelemetryEntry>(
                SeismicDirectorConstants.TelemetryRingBuffer,
                SeismicDirectorConstants.TelemetryFrames,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
            _seismicTuningHandle = vault.GetBufferHandle<SeismicTuningDTO>(
                SeismicDirectorConstants.TuningBuffer,
                SeismicTuningSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
            _mockNarrativeTriggerHandle = vault.GetBufferHandle<MockNarrativeTriggerSignal>(
                SeismicDirectorConstants.MockNarrativeTriggerBuffer,
                SeismicMockSignalSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
            _mockCameraHandle = vault.GetBufferHandle<MockCameraPosition>(
                SeismicDirectorConstants.MockCameraPositionBuffer,
                SeismicMockSignalSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
            _mockSiltHandle = vault.GetBufferHandle<MockSiltSignal>(
                SeismicDirectorConstants.MockSiltSignalBuffer,
                SeismicMockSignalSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
            _mockBaseModuleHandle = vault.GetBufferHandle<SeismicBaseModuleMock>(
                SeismicDirectorConstants.MockBaseModulesBuffer,
                SeismicDirectorConstants.MockBaseModuleSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);

            _seismicVaultReady =
                events.IsCreated &&
                _seismicEventsHandle.IsCreated &&
                _shakeOffsetHandle.IsCreated &&
                _turbiditySpikeHandle.IsCreated &&
                _seismicTelemetryHandle.IsCreated &&
                _seismicTuningHandle.IsCreated &&
                _mockNarrativeTriggerHandle.IsCreated &&
                _mockCameraHandle.IsCreated &&
                _mockSiltHandle.IsCreated &&
                _mockBaseModuleHandle.IsCreated;

            if (!_seismicVaultReady)
                return false;

            SeedDefaultSeismicTuning();
            SeedMockCameraAndBaseModules();
            if (!_legacyFaultBinaryScanned)
                LoadLegacyFaultsOrGenerateEmergency(events);
            return true;
        }

        private void PrewarmSeismicSignalLanes()
        {
            if (_seismicSignalLanesPrewarmed)
                return;

            SignalBus<MockNarrativeTriggerSignal>.Configure(4, maxFrameSignals: 8, lowTierFrameSignals: 2, laneHash: SeismicDirectorConstants.NarrativeMockHash);
            SignalBus<DebrisAvalancheSignal>.Configure(8, maxFrameSignals: 16, lowTierFrameSignals: 4, laneHash: SeismicDirectorConstants.TectonicDebrisHash);
            SignalBus<AcousticShockwaveSignal>.Configure(8, maxFrameSignals: 16, lowTierFrameSignals: 4, laneHash: SeismicDirectorConstants.AcousticShockHash);
            SignalBus<GlobalPanicSignal>.Configure(8, maxFrameSignals: 16, lowTierFrameSignals: 4, laneHash: SeismicDirectorConstants.PanicShockHash);
            SignalBus<MockNarrativeTriggerSignal>.EnsureInitialized();
            SignalBus<DebrisAvalancheSignal>.EnsureInitialized();
            SignalBus<AcousticShockwaveSignal>.EnsureInitialized();
            SignalBus<GlobalPanicSignal>.EnsureInitialized();
            _seismicSignalLanesPrewarmed = true;
        }

        private static bool ValidateSeismicLayouts()
        {
            return UnsafeUtility.SizeOf<SeismicEventDTO>() == 40 &&
                   UnsafeUtility.SizeOf<ShakeOffsetDTO>() == 32 &&
                   UnsafeUtility.SizeOf<SeismicDirectorTelemetryEntry>() == 64 &&
                   UnsafeUtility.SizeOf<SeismicTuningDTO>() == 64;
        }

        private void SeedDefaultSeismicTuning()
        {
            NativeArray<SeismicTuningDTO> tuningBuffer = _seismicTuningHandle.Resolve(_dataVault);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                return;

            SeismicTuningDTO tuning = tuningBuffer[0];
            if (tuning.MaxTranslationMeters > 0f && tuning.NoiseFrequency > 0f && tuning.DecayRate > 0f)
                return;

            tuning.MaxTranslationMeters = 0.35f;
            tuning.NoiseFrequency = 7.5f;
            tuning.DecayRate = 0.18f;
            tuning.SiltMultiplier = 1.75f;
            tuning.MaxRotationRadians = 0.035f;
            tuning.SystemHealthIndex = _lowMemoryProfile || _scalabilityTier <= HectonQualityTier.Mx350 ? 0.9f : 0.25f;
            tuning.DamageThreshold = 0.42f;
            tuning.MaxTurbiditySpike = 1.25f;
            tuning.ShockwaveRadiusPerMagnitude = 125f;
            tuning.MockTriggerProbability = 0.35f;
            tuning.MinimumMagnitude = 6f;
            tuning.Flags = HectonXRRuntimeState.IsXRActive ? SeismicTuningDTO.FlagVrComfortMode : 0u;
            tuning.Seed = _cachedWorldSeed != 0u ? _cachedWorldSeed : DefaultWorldSeed;
            tuningBuffer[0] = tuning;
        }

        private void SeedMockCameraAndBaseModules()
        {
            NativeArray<MockCameraPosition> camera = _mockCameraHandle.Resolve(_dataVault);
            if (camera.IsCreated && camera.Length > 0)
            {
                MockCameraPosition mock = camera[0];
                if (!math.all(math.isfinite(mock.AUP)))
                    mock.AUP = new double3(0d, -2000d, 0d);
                if (!math.all(math.isfinite(mock.Forward)) || math.lengthsq(mock.Forward) < 0.0001f)
                    mock.Forward = new float3(0f, 0f, 1f);
                if (!math.all(math.isfinite(mock.Up)) || math.lengthsq(mock.Up) < 0.0001f)
                    mock.Up = new float3(0f, 1f, 0f);
                mock.Frame = (uint)Time.frameCount;
                mock.Flags = 1u;
                camera[0] = mock;
            }

            NativeArray<SeismicBaseModuleMock> modules = _mockBaseModuleHandle.Resolve(_dataVault);
            if (!modules.IsCreated)
                return;

            int count = math.min(modules.Length, SeismicDirectorConstants.MockBaseModuleSlots);
            for (int i = 0; i < count; i++)
            {
                SeismicBaseModuleMock module = modules[i];
                if (module.ModuleHash != 0u)
                    continue;

                module.AUP = new double3((i - 3) * 18d, -1990d, (i & 1) == 0 ? 24d : -24d);
                module.ModuleHash = LCG_Hash(SeismicDirectorSourceHash ^ unchecked((uint)i));
                module.DamageThreshold = 0.35f + (i * 0.025f);
                module.LastShockwave = 0f;
                module.Flags = 1u;
                modules[i] = module;
            }
        }

        private void LoadLegacyFaultsOrGenerateEmergency(NativeArray<SeismicEventDTO> events)
        {
            _legacyFaultBinaryScanned = true;
            try
            {
                if (!TryLoadLegacyFaultBinary(events))
                    GenerateEmergencyMockFaults(events);
            }
            catch (IOException)
            {
                GenerateEmergencyMockFaults(events);
            }
            catch (UnauthorizedAccessException)
            {
                GenerateEmergencyMockFaults(events);
            }
        }

        private static bool TryLoadLegacyFaultBinary(NativeArray<SeismicEventDTO> events)
        {
            if (!events.IsCreated || !BitConverter.IsLittleEndian)
                return false;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string streamingAssets = Path.Combine(Application.dataPath, "StreamingAssets");
            if (TryLoadLegacyFaultBinaryAt(Path.Combine(streamingAssets, "tectonic_fault_lines.h8bin"), events))
                return true;
            if (TryLoadLegacyFaultBinaryAt(Path.Combine(streamingAssets, "quake_magnitudes.bin"), events))
                return true;
            if (TryLoadLegacyFaultBinaryAt(Path.Combine(projectRoot, "Docs", "Archive", "tectonic_fault_lines.h8bin"), events))
                return true;
            if (TryLoadLegacyFaultBinaryAt(Path.Combine(projectRoot, "Docs", "Archive", "quake_magnitudes.bin"), events))
                return true;

            return false;
        }

        private static bool TryLoadLegacyFaultBinaryAt(string path, NativeArray<SeismicEventDTO> events)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            const int RecordBytes = 40;
            const int HeaderBytes = 16;
            const uint FaultMagic = 0x4B514838u; // H8QK little-endian legacy quake header.

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (stream.Length < RecordBytes)
                    return false;

                byte[] header = new byte[HeaderBytes]; // COLD ALLOC: byte[16] - legacy seismic binary header staging - owner: HectonSeismicTideDirector
                int headerRead = stream.Read(header, 0, header.Length);
                if (headerRead < HeaderBytes)
                    return false;

                uint magic = ReadUInt32Le(header, 0);
                int count;
                long recordOffset;
                if (magic == FaultMagic)
                {
                    count = math.max(0, ReadInt32Le(header, 4));
                    recordOffset = HeaderBytes;
                }
                else
                {
                    long availableRecords = stream.Length / RecordBytes;
                    count = availableRecords > events.Length ? events.Length : (int)availableRecords;
                    recordOffset = 0L;
                }

                if (count <= 0)
                    return false;

                int writeCount = math.min(events.Length, count);
                byte[] record = new byte[RecordBytes]; // COLD ALLOC: byte[40] - legacy seismic fault record staging - owner: HectonSeismicTideDirector
                for (int i = 0; i < writeCount; i++)
                {
                    stream.Position = recordOffset + (long)i * RecordBytes;
                    int read = stream.Read(record, 0, RecordBytes);
                    if (read != RecordBytes)
                        break;

                    double3 epicenter = new double3(
                        ReadDoubleLe(record, 0),
                        ReadDoubleLe(record, 8),
                        ReadDoubleLe(record, 16));
                    if (!math.all(math.isfinite(epicenter)))
                        continue;

                    SeismicEventDTO fault = default;
                    fault.EpicenterAUP = epicenter;
                    fault.Magnitude = math.max(0f, ReadFloatLe(record, 24));
                    fault.Frequency = math.max(0.1f, ReadFloatLe(record, 28));
                    fault.DecayRate = math.max(0.001f, ReadFloatLe(record, 32));
                    fault.EventTypeHash = ReadUInt32Le(record, 36);
                    events[i] = fault;
                }

                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReadUInt32Le(byte[] bytes, int offset)
        {
            return (uint)(bytes[offset] |
                          (bytes[offset + 1] << 8) |
                          (bytes[offset + 2] << 16) |
                          (bytes[offset + 3] << 24));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ReadInt32Le(byte[] bytes, int offset)
        {
            return unchecked((int)ReadUInt32Le(bytes, offset));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ReadFloatLe(byte[] bytes, int offset)
        {
            return BitConverter.ToSingle(bytes, offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ReadDoubleLe(byte[] bytes, int offset)
        {
            return BitConverter.ToDouble(bytes, offset);
        }

        private void GenerateEmergencyMockFaults(NativeArray<SeismicEventDTO> events)
        {
            if (!events.IsCreated)
                return;

            int count = math.min(events.Length, 4);
            for (int i = 0; i < count; i++)
            {
                SeismicEventDTO fault = default;
                fault.EpicenterAUP = new double3(i * 64d, -2000d - i * 120d, -i * 48d);
                fault.Magnitude = 0f;
                fault.Frequency = 5.5f + i * 0.75f;
                fault.DecayRate = 0.16f + i * 0.02f;
                fault.EventTypeHash = SeismicDirectorConstants.EmergencyFaultHash ^ unchecked((uint)i);
                events[i] = fault;
            }

            _emergencyFaultsGenerated = true;
        }

        private unsafe void ExecuteMockNarrativeTrigger()
        {
            if (!_seismicVaultReady || _dataVault == null)
                return;

            MockNarrativeTriggerSignal* signalPtr = (MockNarrativeTriggerSignal*)_mockNarrativeTriggerHandle.ResolvePointer(_dataVault);
            if (signalPtr == null)
                return;

            SeismicTuningDTO tuning = ReadSeismicTuning();
            MockNarrativeTriggerJob job = new MockNarrativeTriggerJob
            {
                Output = signalPtr,
                TimeSeconds = ResolveH8TimeSeconds(),
                Seed = LCG_Hash(_cachedWorldSeed ^ _sequence ^ 0x4E415252u),
                Probability = math.saturate(tuning.MockTriggerProbability),
                MinimumMagnitude = math.max(0f, tuning.MinimumMagnitude),
                Frame = (uint)Time.frameCount
            };
            job.Run();

            MockNarrativeTriggerSignal signal = *signalPtr;
            if (signal.Fire == 0u)
                return;

            SignalBus<MockNarrativeTriggerSignal>.Push(in signal);
            TrySpawnSeismicEvent(signal.EpicenterAUP, signal.Magnitude, tuning.NoiseFrequency, tuning.DecayRate, SeismicDirectorConstants.NarrativeMockHash);
        }

        private unsafe bool TrySpawnSeismicEvent(double3 epicenterAup, float magnitude, float frequency, float decayRate, uint eventTypeHash)
        {
            if (!_seismicVaultReady || _dataVault == null || !math.all(math.isfinite(epicenterAup)) || !math.isfinite(magnitude))
                return false;

            float safeMagnitude = math.max(0f, magnitude);
            if (safeMagnitude <= 0f)
                return false;

            for (int i = 0; i < SeismicDirectorConstants.MaxQuakeSlots; i++)
            {
                ref SeismicEventDTO slot = ref _seismicEventsHandle.GetElementAsRef(_dataVault, i);
                if (slot.Magnitude > 0.01f)
                    continue;

                slot.EpicenterAUP = epicenterAup;
                slot.Magnitude = safeMagnitude;
                slot.Frequency = math.max(0.1f, frequency);
                slot.DecayRate = math.max(0.001f, decayRate);
                slot.EventTypeHash = eventTypeHash != 0u ? eventTypeHash : SeismicDirectorConstants.EmergencyFaultHash;
                _seismicEventSequence++;
                PublishSeismicSpawnSignals(in slot, safeMagnitude);
                return true;
            }

            int replaceIndex = 0;
            float weakestMagnitude = float.MaxValue;
            for (int i = 0; i < SeismicDirectorConstants.MaxQuakeSlots; i++)
            {
                ref SeismicEventDTO slot = ref _seismicEventsHandle.GetElementAsRef(_dataVault, i);
                if (slot.Magnitude < weakestMagnitude)
                {
                    weakestMagnitude = slot.Magnitude;
                    replaceIndex = i;
                }
            }

            ref SeismicEventDTO replacement = ref _seismicEventsHandle.GetElementAsRef(_dataVault, replaceIndex);
            replacement.EpicenterAUP = epicenterAup;
            replacement.Magnitude = safeMagnitude;
            replacement.Frequency = math.max(0.1f, frequency);
            replacement.DecayRate = math.max(0.001f, decayRate);
            replacement.EventTypeHash = eventTypeHash != 0u ? eventTypeHash : SeismicDirectorConstants.EmergencyFaultHash;
            _seismicEventSequence++;
            PublishSeismicSpawnSignals(in replacement, safeMagnitude);
            return true;
        }

        private void PublishSeismicSpawnSignals(in SeismicEventDTO seismicEvent, float magnitude)
        {
            AbsoluteUniversePosition epicenter = AbsoluteUniversePosition.FromAbsolutePosition(seismicEvent.EpicenterAUP);
            float intensity01 = math.saturate(magnitude * 0.1f);
            float radius = math.max(1f, magnitude * ReadSeismicTuning().ShockwaveRadiusPerMagnitude);

            GlobalPanicSignal panic = default;
            panic.EpicenterAup = epicenter;
            panic.RadiusMeters = radius;
            panic.Intensity01 = intensity01;
            panic.SourceHash = SeismicDirectorSourceHash;
            panic.Frame = (uint)Time.frameCount;
            panic.Flags = 1u;
            SignalBus<GlobalPanicSignal>.Push(in panic);

            if (magnitude < SeismicDirectorConstants.SevereMagnitude)
                return;

            PublishDebrisAvalanche(epicenter, intensity01, radius);
            PublishAcousticShockwave(epicenter, intensity01, radius);
            PublishKineticImpactRoute(in seismicEvent, intensity01, radius);
        }

        private void PublishDebrisAvalanche(AbsoluteUniversePosition epicenter, float intensity01, float radius)
        {
            DebrisAvalancheSignal avalanche = default;
            avalanche.CenterAup = epicenter;
            avalanche.RadiusMeters = radius;
            avalanche.Intensity01 = intensity01;
            avalanche.SourceHash = SeismicDirectorSourceHash;
            avalanche.Frame = (uint)Time.frameCount;
            avalanche.Flags = 1u;
            SignalBus<DebrisAvalancheSignal>.Push(in avalanche);

            double3 origin = epicenter.ToAbsoluteDouble3();
            for (int i = 0; i < 8; i++)
            {
                uint debrisSeed = LCG_Hash(_cachedWorldSeed ^ unchecked((uint)(i * 0x45D9F3Bu)) ^ _seismicEventSequence);
                float angle = Hash01(debrisSeed) * TwoPi;
                math.sincos(angle, out float angleSin, out float angleCos);
                float ring = math.lerp(10f, math.min(radius, 70f), Hash01(debrisSeed ^ 0xB5297A4Du));
                double3 offset = new double3(angleCos * ring, math.lerp(6f, 18f, Hash01(debrisSeed ^ 0x68E31DA4u)), angleSin * ring);
                DebrisSpawnSignal debris = default;
                debris.PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(origin + offset);
                debris.SpeciesHash = RockfallSpeciesHash;
                debris.SourceEntityId = debrisSeed;
                debris.Intensity01 = intensity01;
                debris.DebrisKind = DebrisSpawnSignal.DebrisKindRockShard;
                debris.Flags = DebrisSpawnSignal.FlagComputeShard;
                debris.Quantity = 16;
                SignalBus<DebrisSpawnSignal>.Push(in debris);
            }
        }

        private void PublishAcousticShockwave(AbsoluteUniversePosition epicenter, float intensity01, float radius)
        {
            AcousticShockwaveSignal shockwave = default;
            shockwave.CenterAup = epicenter;
            shockwave.RadiusMeters = radius;
            shockwave.Intensity01 = intensity01;
            shockwave.LowPass01 = math.saturate(intensity01 * 1.25f);
            shockwave.SourceHash = SeismicDirectorSourceHash;
            shockwave.Frame = (uint)Time.frameCount;
            shockwave.Flags = 1u;
            SignalBus<AcousticShockwaveSignal>.Push(in shockwave);

            AcousticPingSignal ping = default;
            ping.PositionAup = epicenter;
            ping.RadiusMeters = radius;
            ping.Intensity01 = intensity01;
            ping.SourceId = SeismicDirectorSourceHash;
            ping.Channel = AcousticPingSignal.ChannelMetalStress;
            ping.Flags = AcousticPingSignal.FlagActiveSonar;
            SignalBus<AcousticPingSignal>.Push(in ping);

            ImpactSignal impact = default;
            impact.PointAup = epicenter;
            impact.Force = intensity01 * 12000f;
            impact.Intensity = intensity01;
            impact.MaterialHash = SubLowRumbleHash;
            impact.WeightClass = 3;
            impact.Flags = 1;
            SignalBus<ImpactSignal>.Push(in impact);
        }

        private void PublishKineticImpactRoute(in SeismicEventDTO seismicEvent, float intensity01, float radius)
        {
            NativeArray<SeismicBaseModuleMock> modules = _mockBaseModuleHandle.Resolve(_dataVault);
            if (!modules.IsCreated)
                return;

            int count = math.min(modules.Length, SeismicDirectorConstants.MockBaseModuleSlots);
            for (int i = 0; i < count; i++)
            {
                SeismicBaseModuleMock module = modules[i];
                if (module.ModuleHash == 0u)
                    continue;

                double3 deltaD = module.AUP - seismicEvent.EpicenterAUP;
                if (!math.all(math.isfinite(deltaD)))
                    continue;

                float3 delta = (float3)deltaD;
                float distSq = math.max(1f, math.lengthsq(delta));
                float radiusSq = math.max(1f, radius * radius);
                float shockwave = intensity01 * math.saturate(1f - (distSq / radiusSq));
                module.LastShockwave = shockwave;
                modules[i] = module;
                if (shockwave <= module.DamageThreshold)
                    continue;

                CombatDamageSignal damage = default;
                damage.ImpactAup = module.AUP;
                damage.Direction = math.normalizesafe(delta, new float3(0f, -1f, 0f));
                damage.Magnitude = shockwave;
                damage.DamageType = SeismicDirectorSourceHash;
                damage.TargetHash = module.ModuleHash;
                damage.SourceHash = SeismicDirectorSourceHash;
                damage.Frame = (uint)Time.frameCount;
                damage.Channel = 1;
                damage.Flags = CombatDamageSignal.DirectRuntimeFlag;
                damage.IntegrityDelta = (byte)math.clamp((int)math.round(shockwave * 255f), 1, 255);
                SignalBus<CombatDamageSignal>.Push(in damage);
            }
        }

        private unsafe void ScheduleSeismicOscillator(float deltaTime)
        {
            if (!_seismicVaultReady || _dataVault == null || _oscillatorJobScheduled)
                return;

            SeismicEventDTO* events = (SeismicEventDTO*)_seismicEventsHandle.ResolvePointer(_dataVault);
            ShakeOffsetDTO* shake = (ShakeOffsetDTO*)_shakeOffsetHandle.ResolvePointer(_dataVault);
            float* turbidity = (float*)_turbiditySpikeHandle.ResolvePointer(_dataVault);
            SeismicDirectorTelemetryEntry* telemetry = (SeismicDirectorTelemetryEntry*)_seismicTelemetryHandle.ResolvePointer(_dataVault);
            MockSiltSignal* mockSilt = (MockSiltSignal*)_mockSiltHandle.ResolvePointer(_dataVault);
            if (events == null || shake == null || turbidity == null || telemetry == null || mockSilt == null)
                return;

            if (!TryResolveSeismicCameraAup(out double3 cameraAup))
                cameraAup = new double3(0d, -2000d, 0d);

            SeismicTuningDTO tuning = ReadSeismicTuning();
            if (HectonXRRuntimeState.IsXRActive)
                tuning.Flags |= SeismicTuningDTO.FlagVrComfortMode;
            if (_lowMemoryProfile || _scalabilityTier <= HectonQualityTier.Mx350)
                tuning.SystemHealthIndex = math.max(tuning.SystemHealthIndex, 0.9f);

            int telemetryIndex = _seismicTelemetryWriteIndex;
            _seismicTelemetryWriteIndex++;
            if (_seismicTelemetryWriteIndex >= SeismicDirectorConstants.TelemetryFrames)
                _seismicTelemetryWriteIndex = 0;

            SeismicOscillatorJob job = new SeismicOscillatorJob
            {
                Events = events,
                Shake = shake,
                TurbiditySpike = turbidity,
                Telemetry = telemetry,
                MockSilt = mockSilt,
                EventCapacity = SeismicDirectorConstants.MaxQuakeSlots,
                TelemetryIndex = telemetryIndex,
                CameraAUP = cameraAup,
                DeltaTime = math.max(0f, deltaTime),
                H8TimeSeconds = ResolveH8TimeSeconds(),
                Frame = (uint)Time.frameCount,
                Sequence = _seismicEventSequence,
                Tuning = tuning
            };

            _lastScheduledTelemetryIndex = telemetryIndex;
            _oscillatorJob = job.Schedule();
            _oscillatorJobScheduled = true;
        }

        private void CompleteSeismicOscillatorJob()
        {
            if (!_oscillatorJobScheduled)
                return;

            long start = Stopwatch.GetTimestamp();
            _oscillatorJob.Complete();
            long end = Stopwatch.GetTimestamp();
            _oscillatorJobScheduled = false;

            float computeMs = (float)((end - start) * 1000d / Stopwatch.Frequency);

            UpdateCompletedSeismicTelemetry(computeMs);
            PublishSeismicOutputSignal();
        }

        private void UpdateCompletedSeismicTelemetry(float computeMs)
        {
            if (_lastScheduledTelemetryIndex < 0 || _dataVault == null)
                return;

            NativeArray<SeismicDirectorTelemetryEntry> telemetry = _seismicTelemetryHandle.Resolve(_dataVault);
            if (!telemetry.IsCreated || _lastScheduledTelemetryIndex >= telemetry.Length)
                return;

            SeismicDirectorTelemetryEntry entry = telemetry[_lastScheduledTelemetryIndex];
            entry.OscillatorComputeTimeMs = computeMs;
            if (computeMs > 0.1f)
                entry.Flags |= 1u << 0;
            if (math.lengthsq(entry.TranslationOffset) > 25f)
                entry.Flags |= 1u << 1;
            telemetry[_lastScheduledTelemetryIndex] = entry;

            if ((entry.Flags & 0x3u) != 0u)
                DumpSeismicDirectorTelemetryOnce();
        }

        private void PublishSeismicOutputSignal()
        {
            if (_dataVault == null)
                return;

            NativeArray<ShakeOffsetDTO> shakeBuffer = _shakeOffsetHandle.Resolve(_dataVault);
            NativeArray<float> turbidityBuffer = _turbiditySpikeHandle.Resolve(_dataVault);
            if (!shakeBuffer.IsCreated || shakeBuffer.Length <= 0 || !turbidityBuffer.IsCreated || turbidityBuffer.Length <= 0)
                return;

            ShakeOffsetDTO shake = shakeBuffer[0];
            float translationIntensity = math.saturate(math.length(shake.TranslationOffset) * 2f);
            float turbidity = math.saturate(turbidityBuffer[0]);
            if (translationIntensity <= 0.0001f && turbidity <= 0.0001f)
                return;

            SeismicSignal signal = default;
            signal.Direction = math.normalizesafe(shake.TranslationOffset, new float3(1f, 0f, 0f));
            signal.Intensity01 = math.max(translationIntensity, turbidity);
            bool vrComfort = HectonXRRuntimeState.IsXRActive || (ReadSeismicTuning().Flags & SeismicTuningDTO.FlagVrComfortMode) != 0u;
            signal.CameraJitter01 = vrComfort ? 0f : translationIntensity;
            signal.AudioIntensity01 = math.saturate(signal.Intensity01 * 1.25f);
            signal.ThermalEruptionProbabilityScalar = signal.Intensity01 > 0.8f ? 2f : 1f;
            signal.Sequence = unchecked((ushort)_seismicEventSequence);
            signal.DepthFlags = 1;
            signal.Flags = 4;
            GlobalSignals.Publish(in signal);
        }

        private SeismicTuningDTO ReadSeismicTuning()
        {
            NativeArray<SeismicTuningDTO> tuningBuffer = _seismicTuningHandle.Resolve(_dataVault);
            if (tuningBuffer.IsCreated && tuningBuffer.Length > 0)
                return tuningBuffer[0];

            SeismicTuningDTO tuning = default;
            tuning.MaxTranslationMeters = 0.35f;
            tuning.NoiseFrequency = 7.5f;
            tuning.DecayRate = 0.18f;
            tuning.SiltMultiplier = 1.75f;
            tuning.MaxRotationRadians = 0.035f;
            tuning.SystemHealthIndex = 0.9f;
            tuning.DamageThreshold = 0.42f;
            tuning.MaxTurbiditySpike = 1.25f;
            tuning.ShockwaveRadiusPerMagnitude = 125f;
            tuning.MockTriggerProbability = 0.35f;
            tuning.MinimumMagnitude = 6f;
            tuning.Seed = DefaultWorldSeed;
            return tuning;
        }

        private bool TryResolveSeismicCameraAup(out double3 cameraAup)
        {
            IPlayerRuntimeContext player = _playerRuntime;
            if (player != null && player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                cameraAup = snapshot.Aup.ToAbsoluteDouble3();
                if (math.all(math.isfinite(cameraAup)))
                    return true;
            }

            NativeArray<MockCameraPosition> mockCamera = _mockCameraHandle.Resolve(_dataVault);
            if (mockCamera.IsCreated && mockCamera.Length > 0)
            {
                cameraAup = mockCamera[0].AUP;
                return math.all(math.isfinite(cameraAup));
            }

            cameraAup = default;
            return false;
        }

        private void DumpSeismicDirectorTelemetryOnce()
        {
            if (_dumpedSeismicDirectorTelemetry)
                return;

            _dumpedSeismicDirectorTelemetry = true;
            DumpSeismicDirectorTelemetry();
        }

        private void DumpSeismicDirectorTelemetry()
        {
            if (_dataVault == null)
                return;

            NativeArray<SeismicDirectorTelemetryEntry> telemetry = _seismicTelemetryHandle.Resolve(_dataVault);
            if (!telemetry.IsCreated)
                return;

            try
            {
                Directory.CreateDirectory("Docs/AgentLogs");
                using (FileStream stream = new FileStream(SeismicDirectorConstants.DumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(SeismicDirectorConstants.TelemetryFrames);
                    writer.Write(_seismicTelemetryWriteIndex);
                    for (int i = 0; i < SeismicDirectorConstants.TelemetryFrames; i++)
                    {
                        int index = (_seismicTelemetryWriteIndex + i) % SeismicDirectorConstants.TelemetryFrames;
                        SeismicDirectorTelemetryEntry entry = telemetry[index];
                        writer.Write(entry.Frame);
                        writer.Write(entry.ActiveQuakeCount);
                        writer.Write(entry.MaxMagnitudeGenerated);
                        writer.Write(entry.OscillatorComputeTimeMs);
                        writer.Write(entry.TranslationOffset.x);
                        writer.Write(entry.TranslationOffset.y);
                        writer.Write(entry.TranslationOffset.z);
                        writer.Write(entry.TurbiditySpike);
                        writer.Write(entry.Flags);
                        writer.Write(entry.Sequence);
                        writer.Write(entry.EventHash);
                        writer.Write(entry.PositionHash);
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

#if UNITY_EDITOR
        private void TryPollCsvProfileOverrides()
        {
            double now = ResolveH8TimeSeconds();
            if (now < _nextCsvPollTime || _dataVault == null)
                return;

            _nextCsvPollTime = now + 0.5d;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "seismic_profiles.csv"));
            if (!File.Exists(path))
                return;

            DateTime lastWrite = File.GetLastWriteTimeUtc(path);
            if (lastWrite.Ticks <= 0 || lastWrite == _lastCsvWriteUtc)
                return;

            _lastCsvWriteUtc = lastWrite;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int bytesRead = stream.Read(_csvReadBuffer, 0, _csvReadBuffer.Length);
                    NativeArray<SeismicTuningDTO> tuningBuffer = _seismicTuningHandle.Resolve(_dataVault);
                    if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                        return;

                    SeismicTuningDTO tuning = tuningBuffer[0];
                    if (SeismicCsvProfileParser.TryApply(_csvReadBuffer, bytesRead, ref tuning))
                        tuningBuffer[0] = tuning;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
#endif

        private void EnsureTelemetryRing()
        {
            if (_tideTelemetryHandle.IsCreated)
                return;

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return;

            _tideTelemetryHandle = vault.GetBufferHandle<SeismicTideTelemetryEntry>(
                SeismicDirectorConstants.TideTelemetryBuffer,
                TelemetryCapacity,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
        }

        private void DisposeTelemetryRing()
        {
            _tideTelemetryHandle = default;
            _telemetryWriteIndex = 0;
        }

        private void WriteTelemetryEntry()
        {
            NativeArray<SeismicTideTelemetryEntry> telemetry = _tideTelemetryHandle.Resolve(_dataVault);
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            telemetry[_telemetryWriteIndex] = new SeismicTideTelemetryEntry
            {
                TimeSeconds = _snapshot.AbsoluteUniverseTime,
                TideLevel = _snapshot.TideHeightMeters,
                LastTremorIntensity = _snapshot.SeismicIntensity01,
                Direction = _snapshot.SeismicDirection,
                Flags = _snapshot.Flags,
                Sequence = _snapshot.Sequence
            };
            _telemetryWriteIndex++;
            if (_telemetryWriteIndex >= TelemetryCapacity)
                _telemetryWriteIndex = 0;
        }

        private void DumpTelemetryRingOnce()
        {
            if (_dumpedInvalidTelemetry)
                return;

            _dumpedInvalidTelemetry = true;
            DumpTelemetryRing();
        }

        private void DumpTelemetryRing()
        {
            NativeArray<SeismicTideTelemetryEntry> telemetry = _tideTelemetryHandle.Resolve(_dataVault);
            if (!telemetry.IsCreated)
                return;

            try
            {
                Directory.CreateDirectory("Docs/AgentLogs");
                using (FileStream stream = new FileStream(TelemetryDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(TelemetryCapacity);
                    writer.Write(_telemetryWriteIndex);
                    for (int i = 0; i < TelemetryCapacity; i++)
                    {
                        int index = (_telemetryWriteIndex + i) % TelemetryCapacity;
                        SeismicTideTelemetryEntry entry = telemetry[index];
                        writer.Write(entry.TimeSeconds);
                        writer.Write(entry.TideLevel);
                        writer.Write(entry.LastTremorIntensity);
                        writer.Write(entry.Direction.x);
                        writer.Write(entry.Direction.y);
                        writer.Write(entry.Direction.z);
                        writer.Write(entry.Flags);
                        writer.Write(entry.Sequence);
                    }
                }
            }
            catch (Exception)
            {
#if UNITY_EDITOR
                Debug.LogError("[HectonSeismicTideDirector] telemetry dump failed.");
#endif
            }
        }

        private void RefreshCachedRuntimeState()
        {
            _tickDispatcher = GlobalRegistry.TickDispatcher;
            _dataVault = GlobalRegistry.DataVault;
            _worldSeedProvider = GlobalRegistry.WorldSeedProvider;
            _playerRuntime = GlobalRegistry.Player;
            _fallbackAbsoluteUniverseTime = GlobalRegistry.AbsoluteUniverseTime;
            _celestialSnapshot = GlobalRegistry.CelestialRuntimeSnapshot;

            _cachedWorldSeed = _worldSeedProvider != null && _worldSeedProvider.IsInitialized
                ? unchecked((uint)_worldSeedProvider.RuntimeWorldSeed)
                : DefaultWorldSeed;

            _scalabilityTier = GlobalRegistry.ScalabilityTier;
            _mathPrecision = GlobalRegistry.MathPrecision;
            _lowMemoryProfile = GlobalRegistry.H8_LOW_MEMORY_PROFILE;
            bool requestedShaderShakeDisabled = _lowMemoryProfile ||
                                                _mathPrecision == MathPrecisionLevel.Low ||
                                                _scalabilityTier == HectonQualityTier.Low ||
                                                _scalabilityTier == HectonQualityTier.Mx350 ||
                                                _scalabilityTier == HectonQualityTier.Unknown;
            UpdateShaderShakeLodState(requestedShaderShakeDisabled);
        }

        private void UpdateShaderShakeLodState(bool requestedDisabled)
        {
            double now = ResolveH8TimeSeconds();
            if (!_hasShaderShakeState)
            {
                _shaderShakeDisabled = requestedDisabled;
                _hasShaderShakeState = true;
                _hasPendingShaderShakeState = false;
                return;
            }

            if (requestedDisabled == _shaderShakeDisabled)
            {
                _hasPendingShaderShakeState = false;
                return;
            }

            if (!_hasPendingShaderShakeState || _pendingShaderShakeDisabled != requestedDisabled)
            {
                _pendingShaderShakeDisabled = requestedDisabled;
                _shaderShakeLodSwitchTime = now + ShaderShakeLodHysteresisSeconds;
                _hasPendingShaderShakeState = true;
                return;
            }

            if (now < _shaderShakeLodSwitchTime)
                return;

            _shaderShakeDisabled = requestedDisabled;
            _hasPendingShaderShakeState = false;
        }

        private void ClearCachedRuntimeState()
        {
            _tickDispatcher = null;
            _dataVault = null;
            _worldSeedProvider = null;
            _playerRuntime = null;
            _tideTelemetryHandle = default;
            _seismicEventsHandle = default;
            _shakeOffsetHandle = default;
            _turbiditySpikeHandle = default;
            _seismicTelemetryHandle = default;
            _seismicTuningHandle = default;
            _mockNarrativeTriggerHandle = default;
            _mockCameraHandle = default;
            _mockSiltHandle = default;
            _mockBaseModuleHandle = default;
            _celestialSnapshot = default;
            _fallbackAbsoluteUniverseTime = 0d;
            _cachedWorldSeed = DefaultWorldSeed;
            _cachedTide = default;
            _hasCachedTide = false;
            _scalabilityTier = HectonQualityTier.Unknown;
            _mathPrecision = MathPrecisionLevel.Low;
            _lowMemoryProfile = true;
            _shaderShakeDisabled = true;
            _hasShaderShakeState = false;
            _hasPendingShaderShakeState = false;
            _pendingShaderShakeDisabled = false;
            _shaderShakeLodSwitchTime = 0d;
            _seismicVaultReady = false;
            _seismicSignalLanesPrewarmed = false;
            _oscillatorJobScheduled = false;
            _lastScheduledTelemetryIndex = -1;
        }

        private double ResolveH8TimeSeconds()
        {
            ITickDispatcher dispatcher = _tickDispatcher;
            if (dispatcher != null && math.isfinite(dispatcher.DilatedTimeSeconds))
                return dispatcher.DilatedTimeSeconds;

            return math.isfinite(_fallbackAbsoluteUniverseTime) ? _fallbackAbsoluteUniverseTime : 0d;
        }

        private static int ResolveHourBucket(double h8Time)
        {
            if (!math.isfinite(h8Time))
                return 0;

            double hour = math.floor(h8Time * HourSecondsRcp);
            if (hour > int.MaxValue)
                return int.MaxValue;
            if (hour < int.MinValue)
                return int.MinValue;

            return (int)hour;
        }

        private uint ResolveWorldSeed()
        {
            return _cachedWorldSeed;
        }

        private bool IsLowTierShaderShakeDisabled()
        {
            return _shaderShakeDisabled;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition aup)
        {
            IPlayerRuntimeContext player = _playerRuntime;
            Transform transform = player != null ? player.PlayerTransform : null;
            if (transform == null)
            {
                aup = default;
                return false;
            }

            Vector3 position = transform.position;
            if (!math.all(math.isfinite((float3)position)))
            {
                aup = default;
                return false;
            }

            aup = AbsoluteUniversePosition.FromRuntimePosition(position);
            return true;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static TideSolveResult EvaluateTideHarmonicsBurst(double h8Time, uint seed, float amplitudeMeters)
        {
            float phase0 = Hash01(seed ^ 0xA511E9B3u) * TwoPi;
            float phase1 = Hash01(seed ^ 0x63D83595u) * TwoPi;
            float phase2 = Hash01(seed ^ 0x9D2C5680u) * TwoPi;
            HarmonicSinCos(h8Time, TidePeriod11HoursRcp, phase0, out float h0, out float c0);
            HarmonicSinCos(h8Time, TidePeriod17HoursRcp, phase1, out float h1, out float c1);
            HarmonicSinCos(h8Time, TidePeriod23HoursRcp, phase2, out float h2, out _);
            float combined = (h0 * 0.52f) + (h1 * 0.31f) + (h2 * 0.17f);
            float height = combined * math.max(0f, amplitudeMeters);
            float high01 = math.saturate((combined * 0.5f) + 0.5f);
            float3 pull = NormalizeSafe(new float3(
                c0,
                0.05f + high01 * 0.08f,
                h1 * 0.72f + c1 * 0.28f),
                new float3(1f, 0f, 0f));

            return new TideSolveResult
            {
                HeightMeters = height,
                High01 = high01,
                PullDirection = pull
            };
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static SeismicSolveResult EvaluateSeismicStateBurst(double h8Time, uint seed, float microIntensity, float eventProbability)
        {
            float hourPhase = (float)(h8Time * HourSecondsRcp);
            float eventRoll = Hash01(seed ^ 0xBADC0DEu);
            float eventGate = eventRoll <= math.saturate(eventProbability) ? math.lerp(0.55f, 1f, Hash01(seed ^ 0xC001D00Du)) : 0f;
            float eventEnvelope = TriangleWave01(hourPhase + Hash01(seed ^ 0x51ED270Bu));
            float micro = TriangleWave01((float)(h8Time * 0.071d) + Hash01(seed ^ 0x72E4A13Bu)) * math.saturate(microIntensity);
            float intensity = math.saturate(eventEnvelope * eventGate + micro);
            float yaw = Hash01(seed ^ 0xA2F2D13Fu) * TwoPi;
            math.sincos(yaw, out float yawSin, out float yawCos);
            float tilt = (Hash01(seed ^ 0x9E3779B9u) - 0.5f) * 0.12f;
            float3 direction = NormalizeSafe(new float3(yawCos, tilt, yawSin), new float3(1f, 0f, 0f));

            return new SeismicSolveResult
            {
                Intensity01 = intensity,
                Direction = direction
            };
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void HarmonicSinCos(double h8Time, double inversePeriodSeconds, float phase, out float sine, out float cosine)
        {
            double cycle = h8Time * inversePeriodSeconds;
            double wrapped = cycle - math.floor(cycle);
            math.sincos((float)wrapped * TwoPi + phase, out sine, out cosine);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float TriangleWave01(float phase)
        {
            float wrapped = phase - math.floor(phase);
            return 1f - math.abs(wrapped * 2f - 1f);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint LCG_Hash(uint value)
        {
            value = unchecked(value * 1664525u + 1013904223u);
            value ^= value >> 16;
            value = unchecked(value * 2246822519u + 3266489917u);
            value ^= value >> 13;
            value = unchecked(value * 3266489917u + 668265263u);
            return value ^ (value >> 16);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Hash01(uint value)
        {
            return (LCG_Hash(value) & 0x00FFFFFFu) * Hash24ToUnit;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return math.all(math.isfinite(value)) && math.isfinite(lengthSq) && lengthSq > VectorNormalizeEpsilonSq
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct MockNarrativeTriggerJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public MockNarrativeTriggerSignal* Output;
            public double TimeSeconds;
            public uint Seed;
            public float Probability;
            public float MinimumMagnitude;
            public uint Frame;

            public void Execute()
            {
                ref MockNarrativeTriggerSignal signal = ref UnsafeUtility.AsRef<MockNarrativeTriggerSignal>(Output);
                signal = default;

                uint bucket = (uint)math.max(0d, math.floor(TimeSeconds * 0.5d));
                uint hash = LCG_Hash(Seed ^ bucket ^ Frame);
                if (Hash01(hash) > math.saturate(Probability))
                    return;

                float x = math.lerp(-220f, 220f, Hash01(hash ^ 0xB5297A4Du));
                float z = math.lerp(-220f, 220f, Hash01(hash ^ 0x68E31DA4u));
                float y = math.lerp(-2350f, -1850f, Hash01(hash ^ 0x1B56C4E9u));
                float magnitude = math.max(MinimumMagnitude, math.lerp(6f, 9.25f, Hash01(hash ^ 0xA511E9B3u)));
                signal.EpicenterAUP = new double3(x, y, z);
                signal.Magnitude = magnitude;
                signal.Intensity01 = math.saturate(magnitude * 0.1f);
                signal.TriggerHash = SeismicDirectorConstants.NarrativeMockHash;
                signal.Frame = Frame;
                signal.Fire = 1u;
                signal.Flags = 1u;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct SeismicOscillatorJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public SeismicEventDTO* Events;
            [NativeDisableUnsafePtrRestriction] public ShakeOffsetDTO* Shake;
            [NativeDisableUnsafePtrRestriction] public float* TurbiditySpike;
            [NativeDisableUnsafePtrRestriction] public SeismicDirectorTelemetryEntry* Telemetry;
            [NativeDisableUnsafePtrRestriction] public MockSiltSignal* MockSilt;
            public int EventCapacity;
            public int TelemetryIndex;
            public double3 CameraAUP;
            public float DeltaTime;
            public double H8TimeSeconds;
            public uint Frame;
            public uint Sequence;
            public SeismicTuningDTO Tuning;

            public void Execute()
            {
                float3 translation = float3.zero;
                float3 rotation = float3.zero;
                float turbidity = 0f;
                float maxMagnitude = 0f;
                uint activeCount = 0u;
                uint eventHash = 0u;
                int capacity = math.min(EventCapacity, SeismicDirectorConstants.MaxQuakeSlots);
                float dt = math.max(0f, DeltaTime);
                float radiusPerMagnitude = math.max(1f, Tuning.ShockwaveRadiusPerMagnitude);
                bool sineOnly = Tuning.SystemHealthIndex > 0.85f || (Tuning.Flags & SeismicTuningDTO.FlagSineOnly) != 0u;

                for (int i = 0; i < capacity; i++)
                {
                    ref SeismicEventDTO seismicEvent = ref UnsafeUtility.AsRef<SeismicEventDTO>(Events + i);
                    float magnitude = seismicEvent.Magnitude;
                    if (!math.isfinite(magnitude) || magnitude <= 0.01f)
                    {
                        seismicEvent.Magnitude = 0f;
                        continue;
                    }

                    double3 deltaD = CameraAUP - seismicEvent.EpicenterAUP;
                    if (!math.all(math.isfinite(deltaD)))
                    {
                        seismicEvent.Magnitude = 0f;
                        continue;
                    }

                    activeCount++;
                    maxMagnitude = math.max(maxMagnitude, magnitude);
                    eventHash = seismicEvent.EventTypeHash;

                    float radius = math.max(1f, magnitude * radiusPerMagnitude);
                    float radiusSq = math.max(1f, radius * radius);
                    float3 delta = (float3)deltaD;
                    float distSq = math.max(1f, math.lengthsq(delta));
                    if (distSq <= radiusSq)
                    {
                        float normalizedDistSq = distSq / math.max(1f, radiusSq);
                        float inverseSquare = 1f / math.max(0.0001f, 1f + normalizedDistSq * 16f);
                        float edge = math.saturate(1f - normalizedDistSq);
                        float falloff = math.saturate(inverseSquare * 4f * edge);
                        float3 direction = NormalizeSafe(delta, new float3(1f, 0f, 0f));
                        float phase = (float)(H8TimeSeconds * math.max(0.1f, seismicEvent.Frequency)) + i * 1.6180339f;
                        math.sincos(phase * TwoPi, out float sine, out float cosine);
                        float noiseValue = 0f;
                        if (!sineOnly)
                        {
                            float nf = math.max(0.1f, Tuning.NoiseFrequency);
                            noiseValue = noise.snoise(new float3(direction.x + phase, direction.y + i * 0.37f, direction.z - phase) * nf);
                        }

                        float magnitude01 = math.saturate(magnitude * 0.1f);
                        float amplitude = Tuning.MaxTranslationMeters * magnitude01 * falloff;
                        float3 lateral = NormalizeSafe(new float3(-direction.z, direction.y * 0.25f, direction.x), new float3(0f, 1f, 0f));
                        translation += (direction * sine + lateral * noiseValue * 0.35f) * amplitude;
                        rotation += new float3(cosine * 0.55f, noiseValue, sine * 0.35f) * (Tuning.MaxRotationRadians * magnitude01 * falloff);
                        turbidity = math.max(turbidity, magnitude01 * falloff * math.max(0f, Tuning.SiltMultiplier));
                    }

                    float decayRate = math.max(0.001f, seismicEvent.DecayRate);
                    float decayed = magnitude * math.exp(-decayRate * dt);
                    seismicEvent.Magnitude = math.isfinite(decayed) && decayed >= 0.01f ? decayed : 0f;
                }

                bool rawTranslationExceeded = math.lengthsq(translation) > 25f;
                float maxTranslation = math.max(0f, Tuning.MaxTranslationMeters);
                bool vrComfort = (Tuning.Flags & SeismicTuningDTO.FlagVrComfortMode) != 0u;
                if (vrComfort)
                {
                    rotation = float3.zero;
                    translation = ClampLength(translation, SeismicDirectorConstants.VrComfortTranslationMeters);
                }
                else
                {
                    translation = ClampLength(translation, maxTranslation);
                    rotation = ClampLength(rotation, math.max(0f, Tuning.MaxRotationRadians));
                }

                float turbidityMax = math.max(0f, Tuning.MaxTurbiditySpike);
                if (Tuning.SystemHealthIndex > 0.85f)
                    turbidityMax = math.min(turbidityMax, 0.45f);
                turbidity = math.clamp(turbidity, 0f, turbidityMax);

                if (!math.all(math.isfinite(translation)))
                    translation = float3.zero;
                if (!math.all(math.isfinite(rotation)))
                    rotation = float3.zero;
                if (!math.isfinite(turbidity))
                    turbidity = 0f;

                ref ShakeOffsetDTO shake = ref UnsafeUtility.AsRef<ShakeOffsetDTO>(Shake);
                shake.TranslationOffset = translation;
                shake.RotationEuler = rotation;
                shake._pad0 = 0UL;
                *TurbiditySpike = turbidity;

                ref MockSiltSignal silt = ref UnsafeUtility.AsRef<MockSiltSignal>(MockSilt);
                silt.TurbiditySpike = turbidity;
                silt.UpwardVelocity = new float3(0f, math.saturate(turbidity) * 2f, 0f);
                silt.Frame = Frame;
                silt.Flags = turbidity > 0.0001f ? 1u : 0u;
                silt.Reserved = 0u;

                if ((uint)TelemetryIndex < SeismicDirectorConstants.TelemetryFrames)
                {
                    ref SeismicDirectorTelemetryEntry telemetry = ref UnsafeUtility.AsRef<SeismicDirectorTelemetryEntry>(Telemetry + TelemetryIndex);
                    telemetry = default;
                    telemetry.Frame = Frame;
                    telemetry.ActiveQuakeCount = activeCount;
                    telemetry.MaxMagnitudeGenerated = maxMagnitude;
                    telemetry.TranslationOffset = translation;
                    telemetry.TurbiditySpike = turbidity;
                    telemetry.Flags = vrComfort ? SeismicTuningDTO.FlagVrComfortMode : 0u;
                    if (rawTranslationExceeded)
                        telemetry.Flags |= 1u << 1;
                    telemetry.Sequence = Sequence;
                    telemetry.EventHash = eventHash;
                    telemetry.PositionHash = HashDouble3ToUlong(CameraAUP);
                    if (!math.all(math.isfinite(CameraAUP)) || !math.all(math.isfinite(translation)))
                        telemetry.Flags |= 1u << 8;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float3 ClampLength(float3 value, float maxLength)
            {
                float maxSafe = math.max(0f, maxLength);
                float lengthSq = math.lengthsq(value);
                if (!math.isfinite(lengthSq) || lengthSq <= 0.0000001f || lengthSq <= maxSafe * maxSafe)
                    return math.all(math.isfinite(value)) ? value : float3.zero;

                return value * math.rsqrt(math.max(lengthSq, 0.0000001f)) * maxSafe;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static ulong HashDouble3ToUlong(double3 value)
            {
                long x = (long)math.round(value.x * 0.125d);
                long y = (long)math.round(value.y * 0.125d);
                long z = (long)math.round(value.z * 0.125d);
                uint h0 = LCG_Hash((uint)x ^ (uint)(x >> 32) ^ ((uint)y * 397u));
                uint h1 = LCG_Hash((uint)z ^ (uint)(z >> 32) ^ ((uint)y * 16777619u));
                return ((ulong)h0 << 32) | h1;
            }
        }

        private static bool IsSnapshotFinite(in SeismicRuntimeSnapshot snapshot)
        {
            return math.isfinite(snapshot.AbsoluteUniverseTime) &&
                   math.all(math.isfinite(snapshot.SeismicDirection)) &&
                   math.isfinite(snapshot.SeismicIntensity01) &&
                   math.isfinite(snapshot.TideHeightMeters) &&
                   math.isfinite(snapshot.TideHigh01) &&
                   math.isfinite(snapshot.CameraJitter01) &&
                   math.isfinite(snapshot.AudioRumble01) &&
                   math.isfinite(snapshot.ThermalEruptionProbabilityScalar);
        }

        private static bool ApproximatelyEqual(Vector4 a, Vector4 b)
        {
            return math.abs(a.x - b.x) <= 0.000001f &&
                   math.abs(a.y - b.y) <= 0.000001f &&
                   math.abs(a.z - b.z) <= 0.000001f &&
                   math.abs(a.w - b.w) <= 0.000001f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                return;

            TectonicEventTunerWindow.DrawShockwaveGizmos();
        }
#endif

        [StructLayout(LayoutKind.Sequential)]
        private struct TideSolveResult
        {
            public float HeightMeters;
            public float High01;
            public float3 PullDirection;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SeismicSolveResult
        {
            public float Intensity01;
            public float3 Direction;
        }

        [StructLayout(LayoutKind.Sequential, Size = 40)]
        private struct SeismicTideTelemetryEntry
        {
            public double TimeSeconds;
            public float TideLevel;
            public float LastTremorIntensity;
            public float3 Direction;
            public uint Flags;
            public uint Sequence;
        }
    }

#if UNITY_EDITOR
    public sealed class TectonicEventTunerWindow : EditorWindow
    {
        private const float MinTranslation = 0f;
        private const float MaxTranslation = 5f;
        private const float MinNoise = 0.1f;
        private const float MaxNoise = 64f;
        private const float MinDecay = 0.001f;
        private const float MaxDecay = 5f;
        private const float MinSilt = 0f;
        private const float MaxSilt = 16f;

        [MenuItem("Hecton/Environment/Tectonic Event Tuner")]
        public static void Open()
        {
            GetWindow<TectonicEventTunerWindow>("Tectonic Event Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
        }

        private void OnGUI()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!Application.isPlaying || vault == null)
            {
                EditorGUILayout.HelpBox("Play Mode and GlobalDataVault are required.", MessageType.Info);
                return;
            }

            VaultBufferHandle<SeismicTuningDTO> handle = vault.GetBufferHandle<SeismicTuningDTO>(
                SeismicDirectorConstants.TuningBuffer,
                1,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);
            NativeArray<SeismicTuningDTO> tuningArray = handle.Resolve(vault);
            if (!tuningArray.IsCreated || tuningArray.Length <= 0)
            {
                EditorGUILayout.HelpBox("Seismic tuning buffer is unavailable.", MessageType.Warning);
                return;
            }

            SeismicTuningDTO tuning = tuningArray[0];
            EditorGUI.BeginChangeCheck();
            float maxTranslation = EditorGUILayout.Slider("Max Translation", tuning.MaxTranslationMeters, MinTranslation, MaxTranslation);
            float noiseFrequency = EditorGUILayout.Slider("Noise Frequency", tuning.NoiseFrequency, MinNoise, MaxNoise);
            float decayRate = EditorGUILayout.Slider("Decay Rate", tuning.DecayRate, MinDecay, MaxDecay);
            float siltMultiplier = EditorGUILayout.Slider("Silt Multiplier", tuning.SiltMultiplier, MinSilt, MaxSilt);
            bool vrComfort = EditorGUILayout.Toggle("VR Comfort Mode", (tuning.Flags & SeismicTuningDTO.FlagVrComfortMode) != 0u);
            bool sineOnly = EditorGUILayout.Toggle("Sine Only", (tuning.Flags & SeismicTuningDTO.FlagSineOnly) != 0u);
            if (EditorGUI.EndChangeCheck())
            {
                ref SeismicTuningDTO target = ref handle.GetElementAsRef(vault, 0);
                target.MaxTranslationMeters = maxTranslation;
                target.NoiseFrequency = noiseFrequency;
                target.DecayRate = decayRate;
                target.SiltMultiplier = siltMultiplier;
                if (vrComfort)
                    target.Flags |= SeismicTuningDTO.FlagVrComfortMode;
                else
                    target.Flags &= ~SeismicTuningDTO.FlagVrComfortMode;
                if (sineOnly)
                    target.Flags |= SeismicTuningDTO.FlagSineOnly;
                else
                    target.Flags &= ~SeismicTuningDTO.FlagSineOnly;
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Inject M8.6 Test Event"))
                InjectTestEvent(vault, in tuning);
        }

        private void OnSceneGui(SceneView sceneView)
        {
            _ = sceneView;
            DrawShockwaveGizmos();
        }

        internal static void DrawShockwaveGizmos()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!Application.isPlaying || vault == null)
                return;

            if (!vault.TryGetBufferHandle(SeismicDirectorConstants.EventSlotsBuffer, out VaultBufferHandle<SeismicEventDTO> eventsHandle))
                return;

            NativeArray<SeismicEventDTO> events = eventsHandle.Resolve(vault);
            if (!events.IsCreated)
                return;

            float radiusPerMagnitude = 125f;
            if (vault.TryGetBufferHandle(SeismicDirectorConstants.TuningBuffer, out VaultBufferHandle<SeismicTuningDTO> tuningHandle))
            {
                NativeArray<SeismicTuningDTO> tuning = tuningHandle.Resolve(vault);
                if (tuning.IsCreated && tuning.Length > 0)
                    radiusPerMagnitude = math.max(1f, tuning[0].ShockwaveRadiusPerMagnitude);
            }

            int count = math.min(events.Length, SeismicDirectorConstants.MaxQuakeSlots);
            Handles.color = new Color(1f, 0.12f, 0.08f, 0.85f);
            for (int i = 0; i < count; i++)
            {
                SeismicEventDTO seismicEvent = events[i];
                if (seismicEvent.Magnitude <= 0.01f || !math.all(math.isfinite(seismicEvent.EpicenterAUP)))
                    continue;

                Vector3 center = AbsoluteUniversePosition.FromAbsolutePosition(seismicEvent.EpicenterAUP).ToRuntimeFloat3();
                float radius = math.max(1f, seismicEvent.Magnitude * radiusPerMagnitude);
                Handles.DrawWireDisc(center, Vector3.up, radius);
                Handles.DrawWireDisc(center, Vector3.right, radius);
                Handles.DrawWireDisc(center, Vector3.forward, radius);
            }
        }

        private static void InjectTestEvent(IDataVault vault, in SeismicTuningDTO tuning)
        {
            VaultBufferHandle<SeismicEventDTO> handle = vault.GetBufferHandle<SeismicEventDTO>(
                SeismicDirectorConstants.EventSlotsBuffer,
                SeismicDirectorConstants.MaxQuakeSlots,
                SeismicDirectorConstants.SeismicSystemId,
                NativeArrayOptions.ClearMemory);

            int index = 0;
            for (int i = 0; i < SeismicDirectorConstants.MaxQuakeSlots; i++)
            {
                ref SeismicEventDTO slot = ref handle.GetElementAsRef(vault, i);
                if (slot.Magnitude <= 0.01f)
                {
                    index = i;
                    break;
                }
            }

            ref SeismicEventDTO target = ref handle.GetElementAsRef(vault, index);
            target.EpicenterAUP = new double3(0d, -2000d, 0d);
            target.Magnitude = 8.6f;
            target.Frequency = math.max(0.1f, tuning.NoiseFrequency);
            target.DecayRate = math.max(0.001f, tuning.DecayRate);
            target.EventTypeHash = SeismicDirectorConstants.EmergencyFaultHash;
            SceneView.RepaintAll();
        }
    }
#endif
}

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>
    /// Local narrative isolation signal for story-driven quake tests. Size: 64 bytes, AUP double3 first.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct MockNarrativeTriggerSignal : ISignal
    {
        [FieldOffset(0)] public double3 EpicenterAUP;
        [FieldOffset(24)] public float Magnitude;
        [FieldOffset(28)] public float Intensity01;
        [FieldOffset(32)] public uint TriggerHash;
        [FieldOffset(36)] public uint Frame;
        [FieldOffset(40)] public uint Fire;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong Padding0;
        [FieldOffset(56)] public ulong Padding1;
    }

    /// <summary>
    /// Seismic-to-debris avalanche request. Size: 72 bytes, no Pack=1.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 72)]
    public partial struct DebrisAvalancheSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition CenterAup;
        [FieldOffset(48)] public float RadiusMeters;
        [FieldOffset(52)] public float Intensity01;
        [FieldOffset(56)] public uint SourceHash;
        [FieldOffset(60)] public uint Frame;
        [FieldOffset(64)] public uint Flags;
        [FieldOffset(68)] public uint Reserved;
    }

    /// <summary>
    /// Seismic-to-audio low-pass shockwave request. Size: 72 bytes, no Pack=1.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 72)]
    public partial struct AcousticShockwaveSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition CenterAup;
        [FieldOffset(48)] public float RadiusMeters;
        [FieldOffset(52)] public float Intensity01;
        [FieldOffset(56)] public float LowPass01;
        [FieldOffset(60)] public uint SourceHash;
        [FieldOffset(64)] public uint Frame;
        [FieldOffset(68)] public uint Flags;
    }

    /// <summary>
    /// Seismic-to-ecosystem panic broadcast. Size: 72 bytes, no Pack=1.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 72)]
    public partial struct GlobalPanicSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition EpicenterAup;
        [FieldOffset(48)] public float RadiusMeters;
        [FieldOffset(52)] public float Intensity01;
        [FieldOffset(56)] public uint SourceHash;
        [FieldOffset(60)] public uint Frame;
        [FieldOffset(64)] public uint Flags;
        [FieldOffset(68)] public uint Reserved;
    }
}
