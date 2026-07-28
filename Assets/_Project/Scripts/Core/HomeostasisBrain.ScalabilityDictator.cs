using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Scripting;

namespace Hecton8.Core
{
    /// <summary>
    /// 16-byte cache-line friendly health DTO written directly into GlobalDataVault memory.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct SystemHealthDTO
    {
        [FieldOffset(0)] public float FrameTimeMs;
        [FieldOffset(4)] public float VramPressure;
        [FieldOffset(8)] public float ThermalIndex;
        [FieldOffset(12)] public uint ActiveThrottlesMask;
    }

    /// <summary>
    /// 16-byte scalability state DTO consumed by editor tooling and cross-domain readers.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct ScalabilityStateDTO
    {
        [FieldOffset(0)] public float GlobalQualityWeight;
        [FieldOffset(4)] public float FractionalTimeSlice;
        [FieldOffset(8)] public float VramPressure;
        [FieldOffset(12)] public float ThermalIndex;
    }

    /// <summary>
    /// Synthetic heavy-load input used to prove throttling without a renderer or AI dependency.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public partial struct MockHeavyLoadSignal
    {
        public const uint FlagEnabled = 1u << 0;

        [FieldOffset(0)] public float FrameSpikeMs;
        [FieldOffset(4)] public float VramPressure01;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint _pad0;
    }

    /// <summary>
    /// Mock terrain-sampler output that proves continuous trilinear throttling without a terrain dependency.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public partial struct MockTerrainSamplerStatus
    {
        [FieldOffset(0)] public float GlobalQualityWeight;
        [FieldOffset(4)] public float TrilinearSampleProbability01;
        [FieldOffset(8)] public float SkippedTrilinearPercent01;
        [FieldOffset(12)] public uint Frame;
    }

    /// <summary>
    /// 64-byte dictator-local telemetry row. One cache line, explicit alignment, no managed fields.
    /// </summary>

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
    public struct ScalabilityTelemetryEntry
    {
        [System.Runtime.InteropServices.FieldOffset(0)]
        public ulong Timestamp;
        [System.Runtime.InteropServices.FieldOffset(8)]
        public float RawFrameMs;
        [System.Runtime.InteropServices.FieldOffset(12)]
        public float SmoothedFrameMs;
        [System.Runtime.InteropServices.FieldOffset(16)]
        public float GlobalQualityWeight;
        [System.Runtime.InteropServices.FieldOffset(20)]
        public float VramPressure;
        [System.Runtime.InteropServices.FieldOffset(24)]
        public uint Flags;
        [System.Runtime.InteropServices.FieldOffset(28)]
        public uint _pad0;
        [System.Runtime.InteropServices.FieldOffset(32)]
        private byte _pad1;
        [System.Runtime.InteropServices.FieldOffset(33)]
        private byte _pad2;
        [System.Runtime.InteropServices.FieldOffset(34)]
        private byte _pad3;
        [System.Runtime.InteropServices.FieldOffset(35)]
        private byte _pad4;
        [System.Runtime.InteropServices.FieldOffset(36)]
        private byte _pad5;
        [System.Runtime.InteropServices.FieldOffset(37)]
        private byte _pad6;
        [System.Runtime.InteropServices.FieldOffset(38)]
        private byte _pad7;
        [System.Runtime.InteropServices.FieldOffset(39)]
        private byte _pad8;
        [System.Runtime.InteropServices.FieldOffset(40)]
        private byte _pad9;
        [System.Runtime.InteropServices.FieldOffset(41)]
        private byte _pad10;
        [System.Runtime.InteropServices.FieldOffset(42)]
        private byte _pad11;
        [System.Runtime.InteropServices.FieldOffset(43)]
        private byte _pad12;
        [System.Runtime.InteropServices.FieldOffset(44)]
        private byte _pad13;
        [System.Runtime.InteropServices.FieldOffset(45)]
        private byte _pad14;
        [System.Runtime.InteropServices.FieldOffset(46)]
        private byte _pad15;
        [System.Runtime.InteropServices.FieldOffset(47)]
        private byte _pad16;
        [System.Runtime.InteropServices.FieldOffset(48)]
        private byte _pad17;
        [System.Runtime.InteropServices.FieldOffset(49)]
        private byte _pad18;
        [System.Runtime.InteropServices.FieldOffset(50)]
        private byte _pad19;
        [System.Runtime.InteropServices.FieldOffset(51)]
        private byte _pad20;
        [System.Runtime.InteropServices.FieldOffset(52)]
        private byte _pad21;
        [System.Runtime.InteropServices.FieldOffset(53)]
        private byte _pad22;
        [System.Runtime.InteropServices.FieldOffset(54)]
        private byte _pad23;
        [System.Runtime.InteropServices.FieldOffset(55)]
        private byte _pad24;
        [System.Runtime.InteropServices.FieldOffset(56)]
        private byte _pad25;
        [System.Runtime.InteropServices.FieldOffset(57)]
        private byte _pad26;
        [System.Runtime.InteropServices.FieldOffset(58)]
        private byte _pad27;
        [System.Runtime.InteropServices.FieldOffset(59)]
        private byte _pad28;
        [System.Runtime.InteropServices.FieldOffset(60)]
        private byte _pad29;
        [System.Runtime.InteropServices.FieldOffset(61)]
        private byte _pad30;
        [System.Runtime.InteropServices.FieldOffset(62)]
        private byte _pad31;
        [System.Runtime.InteropServices.FieldOffset(63)]
        private byte _pad32;
    }

    /// <summary>
    /// 16-byte editor/CSV tuning DTO. Written into GlobalDataVault, mirrored into hot scalar fields.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct ScalabilityTuningDTO
    {
        [FieldOffset(0)] public float TargetFrameMs;
        [FieldOffset(4)] public float EmergencyThreshold;
        [FieldOffset(8)] public int HysteresisReleaseFrames;
        [FieldOffset(12)] public uint Flags;
    }

    public static partial class HomeostasisBrain
    {
        private const int DictatorSingletonLength = 1;
        private const int ScalabilityTelemetryCapacity = 300;
        private const int ScalabilityCsvScratchBytes = 4096;
        private const int DefaultHysteresisReleaseFrames = 300;
        private const int DefaultVisualOverkillFrames = 600;
        private const int CsvPollCadenceFrames = 60;
        private const int MockTerrainSamplerCadenceFrames = 8;
        private const float DefaultMockFrameSpikeMs = 20f;
        private const float DefaultEmergencyThreshold = 0.9f;
        private const float EmergencyReleaseThreshold = 0.6f;
        private const float MathLodLowThreshold = 0.8f;
        private const float MathLodFullQualityFloor = 0.3f;
        private const float MathLodHealthSoftStart = 0.55f;
        private const float MathLodSurvivalStep = 0.1001f;
        private const float VisualOverkillEnableThreshold = 0.3f;
        private const float VisualOverkillRevokeThreshold = 0.5f;
        private const float CriticalFrameDumpThresholdMs = 33.0f;
        private const float VramSpikeThreshold = 0.8f;
        private const float VramOomThreshold = 0.85f;
        /// <summary>VRAM pressure at which load shedding arms. Strictly above, never at.</summary>
        public const float VramShedArmPressure01 = VramOomThreshold;
        /// <summary>VRAM pressure shedding must fall back to before it may release.</summary>
        public const float VramShedReleasePressure01 = VramSpikeThreshold;
        /// <summary>Minimum real unscaled seconds shedding holds once armed (AGENTS.md:239 band).</summary>
        public const float VramShedMinimumHoldSeconds = 2.5f;
        private const float VramShedMaxBilledDeltaSeconds = 0.5f;
        private const float ScalabilityHardFailFrameMs = 20f;
        private const float SurvivalHardwareShiFloor = 0.4f;
        private const float SurvivalHardwareMaxQualityWeight = 0.6f;
        private const float HardwareConstraintFlagThreshold01 = 0.65f;
        private const float HardwareConstraintHardLockThreshold01 = 0.95f;
        private const float DefaultLowCullingMultiplier = 0.6f;
        private const float MinimumRenderScale01 = 0.5f;
        private const float MinimumFractionalTimeSlice = 0.1f;
        private const float DefaultQualityRecoveryPerSecond = 0.01f;
        private const float QualityPidIntegralGain = 0.30f;
        private const float QualityPidDerivativeGain = 0.15f;
        private const float QualityPidProportionalGain = 0.55f;
        private const float ForcedQualityWeightDisabled = -1f;
        private const float QualityShaderEpsilon = 0.0005f;
        private const float MaxGcFreezePulseSeconds = 5f;
        private const uint DictatorReasonHash = 0x53484933u; // SHI3
        private const uint ScalabilityTelemetryFlagSanitized = 1u << 31;
        private const uint ScalabilityShaderDirtyMathLodLow = 1u << 0;
        private const uint ScalabilityShaderDirtyCullingMultiplier = 1u << 1;
        private const uint ScalabilityShaderDirtyQualityWeight = 1u << 2;
        private const string ScalabilityDumpFileName = "Dump_SCALABILITY_DICTATOR.bin";
        private const string ScalabilityH8DumpFileName = "Dump_SCALABILITY_DICTATOR.h8dump";
        private const string ScalabilityDumpRelativePath = "Docs/AgentLogs/Dump_SCALABILITY_DICTATOR.bin";
        private const string ScalabilityH8DumpRelativePath = "Docs/AgentLogs/Dump_SCALABILITY_DICTATOR.h8dump";
        private const int ScalabilityDumpHeaderBytes = 20;
        private const int ScalabilityDumpEntryBytes = 64;
        private const string ScalabilityCsvFileName = "scalability_curves.csv";

        private static readonly int _mathLodLowScalarId = Shader.PropertyToID("_HectonMathLodLowWeight");
        private static readonly int _cullingMultiplierId = Shader.PropertyToID("_H8CullingMultiplier");
        private static readonly int _globalQualityWeightId = Shader.PropertyToID("_GlobalQualityWeight");
        private static readonly int _h8GlobalQualityWeightId = Shader.PropertyToID("_H8GlobalQualityWeight");

        private static VaultGenerationHandle<SystemHealthDTO> _systemHealthDtoHandle;
        private static VaultGenerationHandle<ScalabilityStateDTO> _scalabilityStateHandle;
        private static VaultGenerationHandle<MockHeavyLoadSignal> _mockHeavyLoadHandle;
        private static VaultGenerationHandle<MockTerrainSamplerStatus> _mockTerrainSamplerStatusHandle;
        private static VaultGenerationHandle<ScalabilityTelemetryEntry> _scalabilityTelemetryHandle;
        private static VaultGenerationHandle<ScalabilityTuningDTO> _scalabilityTuningHandle;

        private static IDynamicResolutionRuntime _dynamicResolutionRuntime;
        private static long _lastStopwatchTimestamp;
        private static bool _stopwatchSeeded;
        private static long _graphicsMemoryBudgetBytes;
        private static uint _deviceModelHash;
        private static float _hardwareShiFloor;
        private static float _hardwareMaxQualityWeight;
        private static float _hardwareConstraintPressure01;
        private static bool _mockProfilesGenerated;
        private static bool _survivalEmergencyActive;
        private static int _emergencyReleaseCounter;
        private static bool _visualOverkillActive;
        private static int _visualOverkillCounter;
        private static bool _forceVisualOverkillOverride;
        private static float _mathLodLowPressure01;
        private static bool _mathLodLowScalarWritten;
        private static float _lastMathLodLowScalar;
        private static bool _mockHeavyLoadActive;
        private static bool _vramSheddingLatched;
        private static float _vramShedHoldSeconds;
        private static double _vramShedLastUnscaledTimeSeconds;
        private static bool _vramShedClockSeeded;
        private static float _cullingMultiplier = 1f;
        private static float _lowCullingMultiplier = DefaultLowCullingMultiplier;
        private static float _targetFrameMsOverride = ScalabilityContract.TargetFrameMilliseconds;
        private static float _emergencyThresholdOverride = DefaultEmergencyThreshold;
        private static int _hysteresisReleaseFrames = DefaultHysteresisReleaseFrames;
        private static int _csvPollCountdown;
        private static DateTime _csvLastWriteUtc;
        private static string _csvProfilePath;
        private static int _lastGen0CollectionCount;
        private static long _lastMonoUsedBytes;
        private static bool _gcFrozenByDictator;
#pragma warning disable CS0414
        private static int _gcFreezeFramesRemaining;
#pragma warning restore CS0414
        private static bool _gcSafeBaseMenuArmed;
        private static uint _lastRuntimeKillBits;
        private static bool _scalabilityDumped;
        private static int _lastMockTerrainScheduleFrame;
        private static int _lastMockTerrainQualityBucket;
        private static uint _lastMockTerrainFlags;
        private static float _globalQualityWeight = 1f;
        private static float _fractionalTimeSlice = 1f;
        private static float _targetRenderScale01 = 1f;
        private static float _lastPublishedGlobalQualityWeight = ForcedQualityWeightDisabled;
        private static uint _pendingScalabilityShaderDirtyFlags;
        private static float _pendingMathLodLowScalar;
        private static float _pendingCullingMultiplier = 1f;
        private static float _pendingGlobalQualityWeight = 1f;
        private static float _lastAppliedRenderScale01 = ForcedQualityWeightDisabled;
        private static byte _lastAppliedRenderPressureLevel;
        private static byte _lastAppliedRenderFlags;
        private static float _forcedGlobalQualityWeight = ForcedQualityWeightDisabled;
        private static bool _forceGlobalQualityWeightOverride;
        private static bool _globalQualityWeightSeeded;
        private static float _qualityPidIntegral;
        private static float _qualityPidPreviousError;
        private static int _scalabilityTelemetryCursor;
        private static int _scalabilityTelemetrySampleCount;
        /// <summary>Current culling multiplier written by the dictator.</summary>
        public static float CullingMultiplier => SanitizeCullingMultiplier(_cullingMultiplier);

        /// <summary>Continuous scalar: 1.0 means visual overkill, 0.0 means minimum survival.</summary>
        public static float GlobalQualityWeight => SanitizeQualityWeight01(_globalQualityWeight, 0f);

        /// <summary>Continuous update-budget scalar for time-sliced systems.</summary>
        public static float FractionalTimeSlice => ResolveFractionalTimeSliceFromWeight(GlobalQualityWeight);

        /// <summary>Continuous render-scale scalar derived from the global quality weight.</summary>
        public static float TargetRenderScale01 => ResolveRenderScaleFromWeight(GlobalQualityWeight);

        /// <summary>Probability threshold for deterministic stochastic decimation callers.</summary>
        public static float StochasticDecimationThreshold => SanitizeQualityWeight01(_globalQualityWeight, 0f);

        /// <summary>
        /// Deterministic probability gate for callers that need smooth work decimation.
        /// </summary>
        public static bool ShouldExecuteStochasticUpdate(uint stableHash)
        {
            float weight = SanitizeQualityWeight01(_globalQualityWeight, 0f);
            if (weight <= 0f)
                return false;
            if (weight >= 1f)
                return true;

            float sample01 = (stableHash & 0x00FFFFFFu) * (1f / 16777215f);
            return sample01 < weight;
        }

        private static void InitializeScalabilityDictator(
            NativeArray<float> hardwareMetrics,
            NativeArray<float> frameTimes,
            NativeArray<HomeostasisBlackBoxEntry> blackBox)
        {
            ValidateScalabilityDtoLayouts(blackBox);
            ResolveHardwareConstraintPolicy();
            _graphicsMemoryBudgetBytes = ResolveGraphicsMemoryBudgetBytes();
            _dynamicResolutionRuntime = GlobalRegistry.DynamicResolutionRuntime;
            _lastStopwatchTimestamp = 0L;
            _stopwatchSeeded = false;
            _survivalEmergencyActive = false;
            _emergencyReleaseCounter = 0;
            _visualOverkillActive = false;
            _visualOverkillCounter = 0;
            _forceVisualOverkillOverride = false;
            _mathLodLowPressure01 = 0f;
            _mathLodLowScalarWritten = false;
            _lastMathLodLowScalar = ForcedQualityWeightDisabled;
            _mockHeavyLoadActive = false;
            _vramSheddingLatched = false;
            _vramShedHoldSeconds = 0f;
            _vramShedLastUnscaledTimeSeconds = 0.0;
            _vramShedClockSeeded = false;
            _cullingMultiplier = 1f;
            _lowCullingMultiplier = DefaultLowCullingMultiplier;
            _targetFrameMsOverride = ScalabilityContract.TargetFrameMilliseconds;
            _emergencyThresholdOverride = DefaultEmergencyThreshold;
            _hysteresisReleaseFrames = DefaultHysteresisReleaseFrames;
            WriteCurrentTuningStateToVault(_dataVault);
            _csvPollCountdown = 0;
            _csvLastWriteUtc = default;
            _csvProfilePath = ResolveScalabilityCsvPath();
            _lastGen0CollectionCount = GC.CollectionCount(0);
            _lastMonoUsedBytes = Profiler.GetMonoUsedSizeLong();
            _gcFrozenByDictator = false;
            _gcFreezeFramesRemaining = 0;
            _gcSafeBaseMenuArmed = false;
            _lastRuntimeKillBits = 0u;
            _scalabilityDumped = false;
            _lastMockTerrainScheduleFrame = -MockTerrainSamplerCadenceFrames;
            _lastMockTerrainQualityBucket = -1;
            _lastMockTerrainFlags = 0u;
            _globalQualityWeight = SanitizeQualityWeight01(_hardwareMaxQualityWeight, SurvivalHardwareMaxQualityWeight);
            _fractionalTimeSlice = ResolveFractionalTimeSliceFromWeight(_globalQualityWeight);
            _targetRenderScale01 = ResolveRenderScaleFromWeight(_globalQualityWeight);
            _lastPublishedGlobalQualityWeight = ForcedQualityWeightDisabled;
            _pendingScalabilityShaderDirtyFlags = 0u;
            _pendingMathLodLowScalar = 0f;
            _pendingCullingMultiplier = 1f;
            _pendingGlobalQualityWeight = _globalQualityWeight;
            _lastAppliedRenderScale01 = ForcedQualityWeightDisabled;
            _lastAppliedRenderPressureLevel = 0;
            _lastAppliedRenderFlags = 0;
            _forcedGlobalQualityWeight = ForcedQualityWeightDisabled;
            _forceGlobalQualityWeightOverride = false;
            _globalQualityWeightSeeded = false;
            _qualityPidIntegral = 0f;
            _qualityPidPreviousError = 0f;
            _scalabilityTelemetryCursor = 0;
            _scalabilityTelemetrySampleCount = 0;
            MathLodRuntimeConfig.PublishConfig(
                _dataVault,
                0u,
                _globalQualityWeight,
                _fractionalTimeSlice,
                0f,
                0f,
                0f,
                0u,
                out _);
            hardwareMetrics[(int)HardwareMetricSlot.VramPressure01] = 0f;

            IDataVault vault = _dataVault;
            if (EnsureScalabilityStateHandles(vault))
            {
                if (TryAcquireWriteView(vault, in _systemHealthDtoHandle, DictatorSingletonLength, out NativeArray<SystemHealthDTO> health))
                {
                    try
                    {
                        MemClearIfCreated(health);
                    }
                    finally
                    {
                        vault.ReleaseWriteLock(in _systemHealthDtoHandle, SystemID.HardwareHomeostasis);
                    }
                }

                if (TryAcquireWriteView(vault, in _scalabilityStateHandle, DictatorSingletonLength, out NativeArray<ScalabilityStateDTO> state))
                {
                    try
                    {
                        MemClearIfCreated(state);
                    }
                    finally
                    {
                        vault.ReleaseWriteLock(in _scalabilityStateHandle, SystemID.HardwareHomeostasis);
                    }
                }
            }

            if (EnsureMockHeavyLoadHandle(vault) &&
                TryAcquireWriteView(vault, in _mockHeavyLoadHandle, DictatorSingletonLength, out NativeArray<MockHeavyLoadSignal> heavyLoad))
            {
                try
                {
                    MemClearIfCreated(heavyLoad);
                }
                finally
                {
                    vault.ReleaseWriteLock(in _mockHeavyLoadHandle, SystemID.HardwareHomeostasis);
                }
            }

            if (EnsureMockTerrainSamplerStatusHandle(vault) &&
                TryAcquireWriteView(vault, in _mockTerrainSamplerStatusHandle, DictatorSingletonLength, out NativeArray<MockTerrainSamplerStatus> terrainStatus))
            {
                try
                {
                    MemClearIfCreated(terrainStatus);
                }
                finally
                {
                    vault.ReleaseWriteLock(in _mockTerrainSamplerStatusHandle, SystemID.HardwareHomeostasis);
                }
            }

            if (EnsureScalabilityTelemetryHandle(vault) &&
                TryAcquireWriteView(vault, in _scalabilityTelemetryHandle, ScalabilityTelemetryCapacity, out NativeArray<ScalabilityTelemetryEntry> telemetry))
            {
                try
                {
                    MemClearIfCreated(telemetry);
                }
                finally
                {
                    vault.ReleaseWriteLock(in _scalabilityTelemetryHandle, SystemID.HardwareHomeostasis);
                }
            }

            GenerateEmergencyMockProfiles();
            _pendingCullingMultiplier = 1f;
            _pendingScalabilityShaderDirtyFlags |= ScalabilityShaderDirtyCullingMultiplier;
            PublishQualityShaderGlobals(true);
            SetMathLodLowPressure(ResolveHardwareConstraintPressure01());
            FlushVisualSyncShaderState();
        }

        private static void ShutdownScalabilityDictator()
        {
            if (_gcFrozenByDictator)
            {
                GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
                _gcFrozenByDictator = false;
                _gcFreezeFramesRemaining = 0;
            }

            IDynamicResolutionRuntime drsRuntime = _dynamicResolutionRuntime;
            if (drsRuntime != null && drsRuntime.IsSystemOverrideActive)
                drsRuntime.ClearSystemOverrideRenderScale();

            Shader.SetGlobalFloat(_globalQualityWeightId, 1f);
            Shader.SetGlobalFloat(_h8GlobalQualityWeightId, 1f);
            Shader.SetGlobalFloat(_mathLodLowScalarId, 0f);
            _lastMathLodLowScalar = 0f;
            _mathLodLowScalarWritten = true;
            _pendingScalabilityShaderDirtyFlags = 0u;
            _pendingMathLodLowScalar = 0f;
            _pendingCullingMultiplier = 1f;
            _pendingGlobalQualityWeight = 1f;
            ReleaseScalabilityDictatorVaultHandles(_dataVault);
            _systemHealthDtoHandle = default;
            _scalabilityStateHandle = default;
            _mockHeavyLoadHandle = default;
            _mockTerrainSamplerStatusHandle = default;
            _scalabilityTelemetryHandle = default;
            _scalabilityTuningHandle = default;
            _scalabilityTelemetryCursor = 0;
            _scalabilityTelemetrySampleCount = 0;
            _qualityPidIntegral = 0f;
            _qualityPidPreviousError = 0f;
            _dynamicResolutionRuntime = null;
            _csvProfilePath = null;
            _mockProfilesGenerated = false;
            _hardwareConstraintPressure01 = 0f;
            _hardwareShiFloor = 0f;
            _hardwareMaxQualityWeight = 1f;
            _lastRuntimeKillBits = 0u;
            _vramSheddingLatched = false;
            _vramShedHoldSeconds = 0f;
            _vramShedLastUnscaledTimeSeconds = 0.0;
            _vramShedClockSeeded = false;
        }

        private static void ResetScalabilityDictatorVaultHandles()
        {
            ResetScalabilityDictatorVaultHandles(_dataVault);
        }

        private static void ResetScalabilityDictatorVaultHandles(IDataVault releaseVault)
        {
            ReleaseScalabilityDictatorVaultHandles(releaseVault);
            _systemHealthDtoHandle = default;
            _scalabilityStateHandle = default;
            _mockHeavyLoadHandle = default;
            _mockTerrainSamplerStatusHandle = default;
            _scalabilityTelemetryHandle = default;
            _scalabilityTuningHandle = default;
            _scalabilityTelemetryCursor = 0;
            _scalabilityTelemetrySampleCount = 0;
            _qualityPidIntegral = 0f;
            _qualityPidPreviousError = 0f;
            _mockProfilesGenerated = false;
        }

        private static void ReleaseScalabilityDictatorVaultHandles(IDataVault vault)
        {
            MathLodRuntimeConfig.ReleaseRuntimeBuffers(vault);

            if (vault == null)
                return;

            if (_systemHealthDtoHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _systemHealthDtoHandle);
            if (_scalabilityStateHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _scalabilityStateHandle);
            if (_mockHeavyLoadHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _mockHeavyLoadHandle);
            if (_mockTerrainSamplerStatusHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _mockTerrainSamplerStatusHandle);
            if (_scalabilityTelemetryHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _scalabilityTelemetryHandle);
            if (_scalabilityTuningHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _scalabilityTuningHandle);
        }

        private static float ResolveTargetFrameMs(float targetFps)
        {
            float configured = math.isfinite(_targetFrameMsOverride) && _targetFrameMsOverride > 0f
                ? _targetFrameMsOverride
                : ScalabilityContract.TargetFrameMilliseconds;
            float fpsFrameMs = 1000f * math.rcp(math.max(1f, targetFps));
            return math.isfinite(configured) && configured > 0f ? configured : fpsFrameMs;
        }

        private static float ResolveTargetFrameRate()
        {
            return math.isfinite(_cachedTargetFrameRate) && _cachedTargetFrameRate > 0f
                ? _cachedTargetFrameRate
                : 60f;
        }

        private static float SanitizeTunerTargetFrameMs(float targetFrameMs)
        {
            return math.isfinite(targetFrameMs)
                ? math.clamp(targetFrameMs, 4f, 50f)
                : ScalabilityContract.TargetFrameMilliseconds;
        }

        private static float SanitizeTunerEmergencyThreshold(float emergencyThreshold)
        {
            return math.isfinite(emergencyThreshold)
                ? math.clamp(emergencyThreshold, 0.1f, 1f)
                : DefaultEmergencyThreshold;
        }

        private static int SanitizeTunerHysteresisFrames(int hysteresisFrames)
        {
            return math.clamp(hysteresisFrames, 1, 3600);
        }

        private static bool TrySanitizeForcedQualityWeight(float qualityWeight, out float sanitizedWeight)
        {
            sanitizedWeight = 0f;
            if (!math.isfinite(qualityWeight))
                return false;

            sanitizedWeight = math.saturate(qualityWeight);
            return true;
        }

        private static float SanitizeQualityWeight01(float qualityWeight, float fallback)
        {
            if (math.isfinite(qualityWeight))
                return math.saturate(qualityWeight);

            return math.isfinite(fallback) ? math.saturate(fallback) : 0f;
        }

        private static float SanitizePressure01(float pressure, float fallback)
        {
            if (math.isfinite(pressure))
                return math.saturate(pressure);

            return math.isfinite(fallback) ? math.saturate(fallback) : 1f;
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(math.isfinite(value) ? value : 0f);
            return t * t * (3f - 2f * t);
        }

        private static float SanitizePositiveFrameMs(float frameMs)
        {
            return math.isfinite(frameMs) && frameMs > 0f
                ? frameMs
                : ResolveTargetFrameMs(ResolveTargetFrameRate());
        }

        private static float ResolveFractionalTimeSliceFromWeight(float qualityWeight)
        {
            float weight = SanitizeQualityWeight01(qualityWeight, 0f);
            return math.lerp(MinimumFractionalTimeSlice, 1f, weight);
        }

        private static float ResolveRenderScaleFromWeight(float qualityWeight)
        {
            float weight = SanitizeQualityWeight01(qualityWeight, 0f);
            return math.lerp(MinimumRenderScale01, 1f, weight);
        }

        private static float SanitizeCullingMultiplier(float multiplier)
        {
            return math.isfinite(multiplier) ? math.clamp(multiplier, 0.4f, 1f) : 1f;
        }

        private static float SanitizeLowCullingMultiplier(float multiplier)
        {
            return math.isfinite(multiplier) ? math.clamp(multiplier, 0.4f, 1f) : DefaultLowCullingMultiplier;
        }

        private static float SampleStopwatchFrameMilliseconds(float fallbackDeltaTime, float targetFps)
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (!_stopwatchSeeded || _lastStopwatchTimestamp <= 0L)
            {
                _lastStopwatchTimestamp = now;
                _stopwatchSeeded = true;
                return math.max(0.001f, fallbackDeltaTime * 1000f);
            }

            long deltaTicks = now - _lastStopwatchTimestamp;
            _lastStopwatchTimestamp = now;
            if (deltaTicks <= 0L)
                return math.max(0.001f, fallbackDeltaTime * 1000f);

            double frameMs = deltaTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (double.IsNaN(frameMs) || double.IsInfinity(frameMs) || frameMs <= 0.0)
                return 1000f * math.rcp(math.max(1f, targetFps));

            return (float)math.clamp(frameMs, 0.001, 1000.0);
        }

        private static float SampleVramPressure01(NativeArray<float> hardwareMetrics)
        {
            long graphicsBytes = Profiler.GetAllocatedMemoryForGraphicsDriver();
            float pressure = 0f;
            if (graphicsBytes > 0L && _graphicsMemoryBudgetBytes > 0L)
                pressure = math.saturate((float)(graphicsBytes / (double)_graphicsMemoryBudgetBytes));

            if (TryReadMockHeavyLoad(out MockHeavyLoadSignal mock) &&
                (mock.Flags & MockHeavyLoadSignal.FlagEnabled) != 0u)
            {
                pressure = math.max(pressure, SanitizePressure01(mock.VramPressure01, 0f));
            }

            pressure = math.isfinite(pressure) ? math.saturate(pressure) : 1f;
            hardwareMetrics[(int)HardwareMetricSlot.VramPressure01] = pressure;
            return pressure;
        }

        private static float ApplyMockFrameSpikeToFrameMs(float frameMs)
        {
            float safeFrameMs = math.isfinite(frameMs) && frameMs > 0f
                ? frameMs
                : ResolveTargetFrameMs(ResolveTargetFrameRate());
            if (!TryReadMockHeavyLoad(out MockHeavyLoadSignal mock) ||
                (mock.Flags & MockHeavyLoadSignal.FlagEnabled) == 0u)
            {
                return safeFrameMs;
            }

            float spikeMs = math.isfinite(mock.FrameSpikeMs) ? math.max(0f, mock.FrameSpikeMs) : 0f;
            return math.min(1000f, safeFrameMs + spikeMs);
        }

        private static float ComputeDictatorRawShi(
            int frame,
            float baseRawShi,
            float frameMs,
            float targetFrameMs,
            float vramPressure01,
            float cpuTempC,
            float jitterSigmaMs,
            NativeArray<float> hardwareMetrics)
        {
            TryPollCsvOverrides(frame);

            float safeTargetFrameMs = math.isfinite(targetFrameMs) && targetFrameMs > 0f
                ? targetFrameMs
                : ResolveTargetFrameMs(ResolveTargetFrameRate());
            float effectiveFrameMs = math.isfinite(frameMs) && frameMs > 0f ? frameMs : safeTargetFrameMs;
            float effectiveVramPressure01 = SanitizePressure01(vramPressure01, 1f);
            float safeCpuTempC = math.isfinite(cpuTempC) ? cpuTempC : 85f;
            float safeJitterSigmaMs = math.isfinite(jitterSigmaMs) && jitterSigmaMs > 0f ? jitterSigmaMs : 0f;
            _mockHeavyLoadActive = false;
            if (TryReadMockHeavyLoad(out MockHeavyLoadSignal mock) &&
                (mock.Flags & MockHeavyLoadSignal.FlagEnabled) != 0u)
            {
                effectiveVramPressure01 = math.max(effectiveVramPressure01, SanitizePressure01(mock.VramPressure01, 0f));
                _mockHeavyLoadActive = true;
            }

            float frameOverTarget01 = math.saturate((effectiveFrameMs - safeTargetFrameMs) * math.rcp(math.max(0.001f, safeTargetFrameMs)));
            float frameCurve = frameOverTarget01 * frameOverTarget01;
            float vramGuard01 = math.saturate((effectiveVramPressure01 - VramSpikeThreshold) * math.rcp(math.max(0.001f, 1f - VramSpikeThreshold)));
            float vramCurve = vramGuard01 * vramGuard01 * (3f - 2f * vramGuard01);
            float thermal01 = math.saturate((safeCpuTempC - 55f) * math.rcp(30f));
            float jitter01 = math.saturate(safeJitterSigmaMs * 0.5f);
            float polynomial = math.saturate(frameCurve * 0.35f + vramCurve * 0.45f + thermal01 * 0.15f + jitter01 * 0.05f);
            float raw = math.max(math.saturate(baseRawShi), polynomial);

            if (effectiveVramPressure01 > VramOomThreshold)
                raw = math.max(raw, math.saturate(0.86f + (effectiveVramPressure01 - VramOomThreshold) * 0.9f));
            if (effectiveFrameMs > CriticalFrameDumpThresholdMs)
                raw = math.max(raw, 0.92f);
            hardwareMetrics[(int)HardwareMetricSlot.VramPressure01] = effectiveVramPressure01;

            return math.isfinite(raw) ? math.saturate(raw) : 1f;
        }

        private static float ApplyHardwareShiFloor(float shi)
        {
            float clamped = math.isfinite(shi) ? math.saturate(shi) : 1f;
            float hardwareFloor = SanitizePressure01(_hardwareShiFloor, 0f);
            return hardwareFloor > 0f ? math.max(clamped, hardwareFloor) : clamped;
        }

        /// <summary>
        /// Real elapsed unscaled seconds since the previous dictator tick. Sampled from the dispatcher
        /// clock and delta'd rather than counted in ticks, so the band below is measured in wall seconds
        /// and cannot become a function of the player's hardware tier. Capped so a pause, a scene load or
        /// an editor breakpoint is not billed against a hysteresis band.
        /// </summary>
        private static float ResolveDictatorUnscaledDeltaSeconds()
        {
            double now = SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (!(now > 0.0) || double.IsInfinity(now))
                return 0f;

            if (!_vramShedClockSeeded)
            {
                _vramShedClockSeeded = true;
                _vramShedLastUnscaledTimeSeconds = now;
                return 0f;
            }

            double delta = now - _vramShedLastUnscaledTimeSeconds;
            _vramShedLastUnscaledTimeSeconds = now;
            if (!(delta > 0.0) || double.IsInfinity(delta))
                return 0f;

            return (float)math.min(delta, VramShedMaxBilledDeltaSeconds);
        }

        /// <summary>
        /// Advances the owner-local VRAM shed latch from the freshly sampled pressure sample.
        /// The dispatcher clock is delta'd on every tick regardless of latch state so a later arm can
        /// never bill a stale interval against the release band.
        /// </summary>
        private static bool AdvanceVramSheddingLatch(float vramPressure01)
        {
            _vramSheddingLatched = ResolveVramSheddingLatch(
                _vramSheddingLatched,
                vramPressure01,
                ResolveDictatorUnscaledDeltaSeconds(),
                VramShedArmPressure01,
                VramShedReleasePressure01,
                VramShedMinimumHoldSeconds,
                ref _vramShedHoldSeconds);
            return _vramSheddingLatched;
        }

        /// <summary>
        /// Pure VRAM shed latch. Arms strictly above <paramref name="armPressure01"/>, holds for at least
        /// <paramref name="minimumHoldSeconds"/> of real unscaled time, and only releases once pressure has
        /// fallen back to or below <paramref name="releasePressure01"/>.
        /// <para>
        /// A single threshold shared by entry and exit cannot work here: shedding lowers the very VRAM
        /// pressure that triggered it, so the comparison closes on itself and limit-cycles on any machine
        /// sitting near its graphics budget. AGENTS.md:239 requires a minimum 2-3 s band on a scalability
        /// switch, and performance.md:122 requires load shedding to be an authored state machine rather
        /// than a panic button.
        /// </para>
        /// Release pressure is clamped to the arm pressure so a CSV/tuner override can never invert the
        /// band into a latch that arms below the level at which it releases.
        /// </summary>
        public static bool ResolveVramSheddingLatch(
            bool latched,
            float vramPressure01,
            float deltaSeconds,
            float armPressure01,
            float releasePressure01,
            float minimumHoldSeconds,
            ref float holdSeconds)
        {
            float pressure01 = SanitizePressure01(vramPressure01, 1f);
            float safeDeltaSeconds = math.isfinite(deltaSeconds) && deltaSeconds > 0f ? deltaSeconds : 0f;
            float safeHoldSeconds = math.isfinite(holdSeconds) && holdSeconds > 0f ? holdSeconds : 0f;
            float safeArm01 = SanitizePressure01(armPressure01, VramShedArmPressure01);
            float safeRelease01 = math.min(safeArm01, SanitizePressure01(releasePressure01, VramShedReleasePressure01));
            float safeBandSeconds = math.isfinite(minimumHoldSeconds) && minimumHoldSeconds > 0f
                ? minimumHoldSeconds
                : VramShedMinimumHoldSeconds;

            if (!latched)
            {
                holdSeconds = 0f;
                return pressure01 > safeArm01;
            }

            safeHoldSeconds += safeDeltaSeconds;
            if (pressure01 > safeRelease01 || safeHoldSeconds < safeBandSeconds)
            {
                holdSeconds = safeHoldSeconds;
                return true;
            }

            holdSeconds = 0f;
            return false;
        }

        private static ulong ApplyDictatorPressurePolicy(
            int frame,
            float frameMs,
            ulong targetMask,
            ref byte targetLevel,
            ref ushort flags,
            NativeArray<float> hardwareMetrics)
        {
            float vramPressure01 = SanitizePressure01(hardwareMetrics[(int)HardwareMetricSlot.VramPressure01], 1f);
            float cpuTempC = hardwareMetrics[(int)HardwareMetricSlot.CpuTempC];
            float thermalIndex = math.isfinite(cpuTempC) ? math.saturate((cpuTempC - 55f) * math.rcp(30f)) : 1f;
            float systemHealth = SanitizePressure01(_systemHealthIndex01, 1f);
            float safeFrameMs = SanitizePositiveFrameMs(frameMs);
            float emergencyThreshold = SanitizeTunerEmergencyThreshold(_emergencyThresholdOverride);
            float hardwareConstraint01 = ResolveHardwareConstraintPressure01();

            if (systemHealth >= emergencyThreshold)
            {
                _survivalEmergencyActive = true;
                _emergencyReleaseCounter = 0;
            }
            else if (_survivalEmergencyActive)
            {
                if (systemHealth < EmergencyReleaseThreshold)
                {
                    if (_emergencyReleaseCounter < int.MaxValue)
                        _emergencyReleaseCounter++;
                }
                else
                {
                    _emergencyReleaseCounter = 0;
                }

                int releaseFrames = SanitizeTunerHysteresisFrames(_hysteresisReleaseFrames);
                if (_emergencyReleaseCounter >= releaseFrames)
                {
                    _survivalEmergencyActive = false;
                    _emergencyReleaseCounter = 0;
                }
            }

            bool mathLodLow = systemHealth > MathLodLowThreshold ||
                              vramPressure01 > VramOomThreshold ||
                              _survivalEmergencyActive ||
                              hardwareConstraint01 >= HardwareConstraintFlagThreshold01;
            float healthMathLodPressure01 = SmoothStep01(
                (systemHealth - MathLodHealthSoftStart) *
                math.rcp(math.max(0.0001f, MathLodLowThreshold - MathLodHealthSoftStart)));
            float vramMathLodPressure01 = SmoothStep01(
                (vramPressure01 - VramSpikeThreshold) *
                math.rcp(math.max(0.0001f, 1f - VramSpikeThreshold)));
            float hardwareMathLodPressure01 = SmoothStep01(
                hardwareConstraint01 *
                math.rcp(math.max(0.0001f, HardwareConstraintFlagThreshold01)));
            float emergencyMathLodPressure01 = _survivalEmergencyActive
                ? 1f
                : SmoothStep01(
                    (systemHealth - EmergencyReleaseThreshold) *
                    math.rcp(math.max(0.0001f, emergencyThreshold - EmergencyReleaseThreshold)));
            float mathLodPressure01 = math.max(
                math.max(healthMathLodPressure01, vramMathLodPressure01),
                math.max(hardwareMathLodPressure01, emergencyMathLodPressure01));
            if (_survivalEmergencyActive)
            {
                targetMask |= Level3Mask | (ulong)SystemBit.SurvivalPressureEmergency;
                if (targetLevel < 3)
                    targetLevel = 3;
                flags |= (ushort)(HomeostasisSignalFlags.Emergency | HomeostasisSignalFlags.HudWarning);
                _stableRecoveryFrames = 0;
                _recoveryStepFrameCounter = 0;
                _restorationIndex = 0;
            }
            else
            {
                targetMask &= ~(ulong)SystemBit.SurvivalPressureEmergency;
            }

            if (mathLodLow)
            {
                targetMask |= (ulong)SystemBit.MathLodLow;
                flags |= (ushort)HomeostasisSignalFlags.HudWarning;
            }
            else
            {
                targetMask &= ~(ulong)SystemBit.MathLodLow;
            }

            if (AdvanceVramSheddingLatch(vramPressure01))
            {
                targetMask |= (ulong)(SystemBit.VramShedding | SystemBit.NonCriticalVfx);
                if (targetLevel < 2)
                    targetLevel = 2;
            }
            else
            {
                targetMask &= ~(ulong)SystemBit.VramShedding;
            }

            bool squeezeCulling = systemHealth > MathLodLowThreshold || _survivalEmergencyActive;
            if (squeezeCulling)
                targetMask |= (ulong)SystemBit.CullingDistanceSqueeze;
            else if (systemHealth < EmergencyReleaseThreshold)
                targetMask &= ~(ulong)SystemBit.CullingDistanceSqueeze;

            if (_mockHeavyLoadActive)
                targetMask |= (ulong)SystemBit.MockHeavyLoad;
            else
                targetMask &= ~(ulong)SystemBit.MockHeavyLoad;

            if (systemHealth > 0.95f)
            {
                targetMask |= (ulong)SystemBit.AiOneHz;
                if (targetLevel < 3)
                    targetLevel = 3;
            }

            ApplyVisualOverkillPolicy(systemHealth, ref targetMask);
            ApplyGarbageCollectorPolicy(safeFrameMs, ref targetMask);
            SetMathLodLowPressure(mathLodPressure01);
            UpdateCullingMultiplier(math.lerp(1f, SanitizeLowCullingMultiplier(_lowCullingMultiplier), ResolveMathLodLowWeight()));
            UpdateRegistryKillMask(targetMask);
            WriteDictatorState(frame, safeFrameMs, vramPressure01, thermalIndex, targetMask);
            RefreshMockTerrainSamplerStatus(frame, targetMask);

            bool survivalFailure = safeFrameMs > ScalabilityHardFailFrameMs && GlobalQualityWeight <= 0.0001f;
            bool emergencyFailure = safeFrameMs > CriticalFrameDumpThresholdMs &&
                                    (targetMask & (ulong)SystemBit.SurvivalPressureEmergency) != 0UL;
            if (!_scalabilityDumped && (survivalFailure || emergencyFailure))
            {
                if (TryResolveRuntimeBuffers(
                        out _,
                        out _,
                        out NativeArray<HomeostasisBlackBoxEntry> blackBox))
                {
                    DumpScalabilityDictatorBlackBoxOnce(blackBox);
                }
            }

            return targetMask;
        }

        private static void ApplyVisualOverkillPolicy(float systemHealth, ref ulong targetMask)
        {
            if (ResolveHardwareConstraintPressure01() >= HardwareConstraintHardLockThreshold01)
            {
                _visualOverkillActive = false;
                _visualOverkillCounter = 0;
                targetMask &= ~(ulong)SystemBit.VisualOverkill;
                return;
            }

            if (_forceVisualOverkillOverride)
            {
                _visualOverkillActive = true;
            }
            else if (systemHealth < VisualOverkillEnableThreshold)
            {
                if (_visualOverkillCounter < int.MaxValue)
                    _visualOverkillCounter++;
                if (_visualOverkillCounter >= DefaultVisualOverkillFrames)
                    _visualOverkillActive = true;
            }
            else if (systemHealth > VisualOverkillRevokeThreshold)
            {
                _visualOverkillActive = false;
                _visualOverkillCounter = 0;
            }

            if (_visualOverkillActive)
                targetMask |= (ulong)SystemBit.VisualOverkill;
            else
                targetMask &= ~(ulong)SystemBit.VisualOverkill;
        }

        private static void ApplyGarbageCollectorPolicy(float frameMs, ref ulong targetMask)
        {
            int gen0 = GC.CollectionCount(0);
            long monoUsedBytes = Profiler.GetMonoUsedSizeLong();
            bool gen0Spike = gen0 != _lastGen0CollectionCount;
            bool heapSpike = monoUsedBytes > _lastMonoUsedBytes + (2L * 1024L * 1024L);
            _lastGen0CollectionCount = gen0;
            _lastMonoUsedBytes = monoUsedBytes;

#if UNITY_EDITOR
            targetMask &= ~(ulong)SystemBit.GcFreeze;
            _gcFrozenByDictator = false;
            _gcFreezeFramesRemaining = 0;
            _gcSafeBaseMenuArmed = false;
            return;
#else
            float systemHealth = SanitizePressure01(_systemHealthIndex01, 1f);
            if ((gen0Spike || heapSpike) && systemHealth > MathLodLowThreshold)
            {
                targetMask |= (ulong)SystemBit.GcFreeze;
                if (!_gcFrozenByDictator && GarbageCollector.GCMode != GarbageCollector.Mode.Disabled)
                {
                    GarbageCollector.GCMode = GarbageCollector.Mode.Disabled;
                    _gcFrozenByDictator = true;
                    _gcFreezeFramesRemaining = math.max(1, (int)math.ceil(math.max(1f, ResolveTargetFrameRate()) * MaxGcFreezePulseSeconds));
                }
                return;
            }

            if (_gcFrozenByDictator)
            {
                targetMask |= (ulong)SystemBit.GcFreeze;
                if (_gcFreezeFramesRemaining > 0)
                    _gcFreezeFramesRemaining--;

                float safeFrameMs = SanitizePositiveFrameMs(frameMs);
                bool safeBaseMenu = _gcSafeBaseMenuArmed &&
                                    systemHealth < 0.35f &&
                                    safeFrameMs < ResolveTargetFrameMs(ResolveTargetFrameRate());
                bool pulseExpired = _gcFreezeFramesRemaining <= 0;
                if (safeBaseMenu || pulseExpired)
                {
                    GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
                    _gcFrozenByDictator = false;
                    _gcFreezeFramesRemaining = 0;
                    _gcSafeBaseMenuArmed = false;
                    targetMask &= ~(ulong)SystemBit.GcFreeze;
                }
            }
            else
            {
                targetMask &= ~(ulong)SystemBit.GcFreeze;
            }
#endif
        }

        private static void SetMathLodLowPressure(float pressure01)
        {
            _mathLodLowPressure01 = SanitizePressure01(pressure01, 0f);
            RefreshMathLodLowScalar();
        }

        private static float ResolveMathLodLowWeight()
        {
            float qualityWeight = SanitizeQualityWeight01(_globalQualityWeight, 0f);
            float systemHealth = SanitizePressure01(_systemHealthIndex01, 1f);
            float qualityPressure = math.saturate(
                (MathLodLowThreshold - qualityWeight) *
                math.rcp(math.max(0.0001f, MathLodLowThreshold - MathLodFullQualityFloor)));
            qualityPressure = qualityPressure * qualityPressure * (3f - 2f * qualityPressure);

            float healthPressure = math.saturate(
                (systemHealth - MathLodHealthSoftStart) *
                math.rcp(math.max(0.0001f, MathLodLowThreshold - MathLodHealthSoftStart)));
            healthPressure = healthPressure * healthPressure * (3f - 2f * healthPressure);

            float survivalFloor = SmoothStep01(
                (MathLodSurvivalStep - qualityWeight) *
                math.rcp(math.max(0.0001f, MathLodSurvivalStep)));
            return math.saturate(math.max(math.max(qualityPressure, healthPressure), math.max(survivalFloor, _mathLodLowPressure01)));
        }

        private static void RefreshMathLodLowScalar()
        {
            float lowWeight = ResolveMathLodLowWeight();
            if (_mathLodLowScalarWritten && math.abs(_lastMathLodLowScalar - lowWeight) < QualityShaderEpsilon)
                return;

            _pendingMathLodLowScalar = lowWeight;
            _pendingScalabilityShaderDirtyFlags |= ScalabilityShaderDirtyMathLodLow;
        }

        private static void UpdateCullingMultiplier(float multiplier)
        {
            float safeMultiplier = SanitizeCullingMultiplier(multiplier);
            if (math.abs(_cullingMultiplier - safeMultiplier) < 0.001f)
                return;

            _cullingMultiplier = safeMultiplier;
            _pendingCullingMultiplier = safeMultiplier;
            _pendingScalabilityShaderDirtyFlags |= ScalabilityShaderDirtyCullingMultiplier;
        }

        private static void UpdateGlobalQualityState(float frameMs, float vramPressure01, float thermalIndex)
        {
            float targetFrameMs = ResolveTargetFrameMs(ResolveTargetFrameRate());
            float safeFrameMs = math.isfinite(frameMs) && frameMs > 0f ? frameMs : targetFrameMs;
            float safeSystemHealth = SanitizePressure01(_systemHealthIndex01, 1f);
            float safeVramPressure = SanitizePressure01(vramPressure01, 1f);
            float safeThermalIndex = SanitizePressure01(thermalIndex, 1f);
            float hardwareCeiling = math.isfinite(_hardwareMaxQualityWeight)
                ? math.saturate(_hardwareMaxQualityWeight)
                : SurvivalHardwareMaxQualityWeight;
            float frameSeconds = math.max(0.000001f, safeFrameMs * 0.001f);
            float frameError01 = math.saturate((safeFrameMs - targetFrameMs) * math.rcp(math.max(0.0001f, targetFrameMs)));
            if (frameError01 > 0.0001f)
                _qualityPidIntegral = math.saturate(_qualityPidIntegral + frameError01 * frameSeconds);
            else
                _qualityPidIntegral = math.max(0f, _qualityPidIntegral - frameSeconds * 0.25f);

            float derivative01 = math.max(0f, frameError01 - _qualityPidPreviousError);
            _qualityPidPreviousError = frameError01;
            float pidStress = math.saturate(
                frameError01 * QualityPidProportionalGain +
                _qualityPidIntegral * QualityPidIntegralGain +
                derivative01 * QualityPidDerivativeGain);
            float stress = math.max(safeSystemHealth, math.max(math.max(safeVramPressure, safeThermalIndex), pidStress));
            float desired = math.saturate(1f - stress);
            desired = math.min(desired, hardwareCeiling);
            if (_forceGlobalQualityWeightOverride)
                desired = math.min(SanitizeQualityWeight01(_forcedGlobalQualityWeight, 0f), hardwareCeiling);

            if (!_globalQualityWeightSeeded)
            {
                _globalQualityWeight = desired;
                _globalQualityWeightSeeded = true;
            }
            else if (_forceGlobalQualityWeightOverride || desired < _globalQualityWeight)
            {
                _globalQualityWeight = desired;
            }
            else
            {
                float recoveryStep = DefaultQualityRecoveryPerSecond * frameSeconds;
                _globalQualityWeight = math.min(desired, _globalQualityWeight + recoveryStep);
            }

            _globalQualityWeight = SanitizeQualityWeight01(_globalQualityWeight, desired);
            _fractionalTimeSlice = ResolveFractionalTimeSliceFromWeight(_globalQualityWeight);
            _targetRenderScale01 = ResolveRenderScaleFromWeight(_globalQualityWeight);
        }

        private static void ApplyDictatorRenderScale(float frameMs, float thermalIndex)
        {
            IDynamicResolutionRuntime drsRuntime = _dynamicResolutionRuntime;
            if (drsRuntime == null)
                return;

            byte flags = thermalIndex > 0.5f ? (byte)1 : (byte)0;
            if (math.abs(_lastAppliedRenderScale01 - _targetRenderScale01) < 0.001f &&
                _lastAppliedRenderPressureLevel == _currentPressureLevel &&
                _lastAppliedRenderFlags == flags)
            {
                return;
            }

            drsRuntime.ApplySystemOverrideRenderScale(
                _targetRenderScale01,
                _targetRenderScale01,
                math.isfinite(frameMs) && frameMs > 0f ? frameMs : ResolveTargetFrameMs(ResolveTargetFrameRate()),
                _currentPressureLevel,
                flags);
            _lastAppliedRenderScale01 = _targetRenderScale01;
            _lastAppliedRenderPressureLevel = _currentPressureLevel;
            _lastAppliedRenderFlags = flags;
        }

        private static void PublishQualityShaderGlobals(bool force)
        {
            float qualityWeight = GlobalQualityWeight;
            if (!force && math.abs(_lastPublishedGlobalQualityWeight - qualityWeight) < QualityShaderEpsilon)
                return;

            _pendingGlobalQualityWeight = qualityWeight;
            _pendingScalabilityShaderDirtyFlags |= ScalabilityShaderDirtyQualityWeight;
        }

        internal static void FlushVisualSyncShaderState()
        {
            uint flags = _pendingScalabilityShaderDirtyFlags;
            if (flags == 0u)
                return;

            if ((flags & ScalabilityShaderDirtyMathLodLow) != 0u)
            {
                Shader.SetGlobalFloat(_mathLodLowScalarId, _pendingMathLodLowScalar);
                _lastMathLodLowScalar = _pendingMathLodLowScalar;
                _mathLodLowScalarWritten = true;
            }

            if ((flags & ScalabilityShaderDirtyCullingMultiplier) != 0u)
                Shader.SetGlobalFloat(_cullingMultiplierId, _pendingCullingMultiplier);

            if ((flags & ScalabilityShaderDirtyQualityWeight) != 0u)
            {
                Shader.SetGlobalFloat(_globalQualityWeightId, _pendingGlobalQualityWeight);
                Shader.SetGlobalFloat(_h8GlobalQualityWeightId, _pendingGlobalQualityWeight);
                _lastPublishedGlobalQualityWeight = _pendingGlobalQualityWeight;
            }

            _pendingScalabilityShaderDirtyFlags = 0u;
        }

        private static void UpdateRegistryKillMask(ulong targetMask)
        {
            uint currentBits = FoldMaskToUInt(targetMask);
            uint clearBits = _lastRuntimeKillBits & ~currentBits;
            uint setBits = currentBits & ~_lastRuntimeKillBits;
            if (clearBits != 0u)
                SignalBusRegistry.SetSystemKillSwitchBits(clearBits, false, DictatorReasonHash);
            if (setBits != 0u)
                SignalBusRegistry.SetSystemKillSwitchBits(setBits, true, DictatorReasonHash);
            _lastRuntimeKillBits = currentBits;
        }

        private static void WriteDictatorState(
            int frame,
            float frameMs,
            float vramPressure01,
            float thermalIndex,
            ulong activeMask)
        {
            IDataVault vault = _dataVault;
            uint foldedMask = FoldMaskToUInt(activeMask);
            float safeFrameMs = SanitizePositiveFrameMs(frameMs);
            float safeVramPressure01 = SanitizePressure01(vramPressure01, 1f);
            float safeThermalIndex = SanitizePressure01(thermalIndex, 1f);
            if (!TryAcquireWriteView(vault, in _systemHealthDtoHandle, DictatorSingletonLength, out NativeArray<SystemHealthDTO> healthArray))
                return;

            try
            {
                SystemHealthDTO health = healthArray[0];
                health.FrameTimeMs = safeFrameMs;
                health.VramPressure = safeVramPressure01;
                health.ThermalIndex = safeThermalIndex;
                health.ActiveThrottlesMask = foldedMask;
                healthArray[0] = health;
            }
            finally
            {
                vault.ReleaseWriteLock(in _systemHealthDtoHandle, SystemID.HardwareHomeostasis);
            }

            UpdateGlobalQualityState(safeFrameMs, safeVramPressure01, safeThermalIndex);
            if (!TryAcquireWriteView(vault, in _scalabilityStateHandle, DictatorSingletonLength, out NativeArray<ScalabilityStateDTO> stateArray))
                return;

            try
            {
                ScalabilityStateDTO state = stateArray[0];
                state.GlobalQualityWeight = _globalQualityWeight;
                state.FractionalTimeSlice = _fractionalTimeSlice;
                state.VramPressure = safeVramPressure01;
                state.ThermalIndex = safeThermalIndex;
                stateArray[0] = state;
            }
            finally
            {
                vault.ReleaseWriteLock(in _scalabilityStateHandle, SystemID.HardwareHomeostasis);
            }

            if (MathLodRuntimeConfig.PublishConfig(
                    vault,
                    unchecked((uint)frame),
                    _globalQualityWeight,
                    _fractionalTimeSlice,
                    safeFrameMs,
                    safeVramPressure01,
                    safeThermalIndex,
                    foldedMask != 0u ? MathLodRuntimeConfig.ConfigFlagExternalPressure : 0u,
                    out uint mathLodFaultFlags) &&
                mathLodFaultFlags != 0u)
            {
                MathLodRuntimeConfig.TryDumpOnFault(null);
            }

            RecordScalabilityTelemetry(safeFrameMs, safeVramPressure01, foldedMask);
            RefreshMathLodLowScalar();
            ApplyDictatorRenderScale(safeFrameMs, safeThermalIndex);
            PublishQualityShaderGlobals(false);
        }

        private static void GenerateEmergencyMockProfiles()
        {
            if (_mockProfilesGenerated)
                return;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !EnsureScalabilityStateHandles(vault) ||
                !EnsureMockHeavyLoadHandle(vault) ||
                !EnsureMockTerrainSamplerStatusHandle(vault))
            {
                return;
            }

            float weight = SanitizeQualityWeight01(_globalQualityWeight, 0f);
            if (!TryAcquireWriteView(vault, in _scalabilityStateHandle, DictatorSingletonLength, out NativeArray<ScalabilityStateDTO> stateArray))
            {
                return;
            }

            try
            {
                ScalabilityStateDTO state = stateArray[0];
                state.GlobalQualityWeight = weight;
                state.FractionalTimeSlice = ResolveFractionalTimeSliceFromWeight(weight);
                state.VramPressure = 0f;
                state.ThermalIndex = 0f;
                stateArray[0] = state;
            }
            finally
            {
                vault.ReleaseWriteLock(in _scalabilityStateHandle, SystemID.HardwareHomeostasis);
            }

            if (!TryAcquireWriteView(vault, in _mockHeavyLoadHandle, DictatorSingletonLength, out NativeArray<MockHeavyLoadSignal> heavyArray))
                return;

            try
            {
                MockHeavyLoadSignal heavy = heavyArray[0];
                heavy.FrameSpikeMs = DefaultMockFrameSpikeMs;
                heavy.VramPressure01 = 0f;
                heavy.Flags = 0u;
                heavy._pad0 = 0u;
                heavyArray[0] = heavy;
            }
            finally
            {
                vault.ReleaseWriteLock(in _mockHeavyLoadHandle, SystemID.HardwareHomeostasis);
            }

            if (!TryAcquireWriteView(vault, in _mockTerrainSamplerStatusHandle, DictatorSingletonLength, out NativeArray<MockTerrainSamplerStatus> terrainArray))
                return;

            try
            {
                MockTerrainSamplerStatus terrain = terrainArray[0];
                terrain.GlobalQualityWeight = weight;
                terrain.TrilinearSampleProbability01 = weight;
                terrain.SkippedTrilinearPercent01 = 1f - weight;
                terrain.Frame = 0u;
                terrainArray[0] = terrain;
            }
            finally
            {
                vault.ReleaseWriteLock(in _mockTerrainSamplerStatusHandle, SystemID.HardwareHomeostasis);
            }

            _mockProfilesGenerated = true;
        }

        private static bool TryReadMockHeavyLoad(out MockHeavyLoadSignal signal)
        {
            signal = default;
            IDataVault vault = _dataVault;
            if (!TryReadMockHeavyLoadView(vault, out NativeArray<MockHeavyLoadSignal>.ReadOnly signals))
                return false;

            signal = signals[0];
            return true;
        }

        private static bool TryReadScalabilityStateViews(
            IDataVault vault,
            out NativeArray<SystemHealthDTO>.ReadOnly health,
            out NativeArray<ScalabilityStateDTO>.ReadOnly state)
        {
            health = default;
            state = default;
            return vault != null &&
                   vault.TryReadOnlyHandle(in _systemHealthDtoHandle, out health) &&
                   vault.TryReadOnlyHandle(in _scalabilityStateHandle, out state) &&
                   health.Length >= DictatorSingletonLength &&
                   state.Length >= DictatorSingletonLength;
        }

        private static bool TryReadScalabilityTuningView(IDataVault vault, out NativeArray<ScalabilityTuningDTO>.ReadOnly tuning)
        {
            tuning = default;
            return vault != null &&
                   vault.TryReadOnlyHandle(in _scalabilityTuningHandle, out tuning) &&
                   tuning.Length >= DictatorSingletonLength;
        }

        private static bool TryReadMockHeavyLoadView(IDataVault vault, out NativeArray<MockHeavyLoadSignal>.ReadOnly signal)
        {
            signal = default;
            return vault != null &&
                   vault.TryReadOnlyHandle(in _mockHeavyLoadHandle, out signal) &&
                   signal.Length >= DictatorSingletonLength;
        }

        private static bool TryReadMockTerrainSamplerStatusView(IDataVault vault, out NativeArray<MockTerrainSamplerStatus>.ReadOnly terrainSampler)
        {
            terrainSampler = default;
            return vault != null &&
                   vault.TryReadOnlyHandle(in _mockTerrainSamplerStatusHandle, out terrainSampler) &&
                   terrainSampler.Length >= DictatorSingletonLength;
        }

        private static bool EnsureScalabilityStateHandles(IDataVault vault)
        {
            if (!OpenOrAcquireVaultBufferForOwnerRoute(
                    vault,
                    ref _systemHealthDtoHandle,
                    BufferID.ShinobuScalabilitySystemHealth,
                    DictatorSingletonLength,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<SystemHealthDTO> health,
                    out bool healthCreated))
            {
                return false;
            }

            if (!OpenOrAcquireVaultBufferForOwnerRoute(
                    vault,
                    ref _scalabilityStateHandle,
                    BufferID.ShinobuScalabilityState,
                    DictatorSingletonLength,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<ScalabilityStateDTO> state,
                    out bool stateCreated))
            {
                return false;
            }

            if (healthCreated)
                MemClearIfCreated(health);
            if (stateCreated)
                MemClearIfCreated(state);
            return true;
        }

        private static bool EnsureScalabilityTuningHandle(IDataVault vault)
        {
            if (!OpenOrAcquireVaultBufferForOwnerRoute(
                    vault,
                    ref _scalabilityTuningHandle,
                    BufferID.ShinobuScalabilityTunerState,
                    DictatorSingletonLength,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<ScalabilityTuningDTO> tuningArray,
                    out bool created))
            {
                return false;
            }

            if (created)
            {
                MemClearIfCreated(tuningArray);
                ScalabilityTuningDTO tuning = tuningArray[0];
                tuning.TargetFrameMs = SanitizeTunerTargetFrameMs(_targetFrameMsOverride);
                tuning.EmergencyThreshold = SanitizeTunerEmergencyThreshold(_emergencyThresholdOverride);
                tuning.HysteresisReleaseFrames = SanitizeTunerHysteresisFrames(_hysteresisReleaseFrames);
                tuning.Flags = 0u;
                tuningArray[0] = tuning;
            }

            return true;
        }

        private static void WriteCurrentTuningStateToVault(IDataVault vault)
        {
            if (!EnsureScalabilityTuningHandle(vault))
                return;

            if (!TryAcquireWriteView(vault, in _scalabilityTuningHandle, DictatorSingletonLength, out NativeArray<ScalabilityTuningDTO> tuningArray))
                return;

            try
            {
                ScalabilityTuningDTO tuning = tuningArray[0];
                tuning.TargetFrameMs = SanitizeTunerTargetFrameMs(_targetFrameMsOverride);
                tuning.EmergencyThreshold = SanitizeTunerEmergencyThreshold(_emergencyThresholdOverride);
                tuning.HysteresisReleaseFrames = SanitizeTunerHysteresisFrames(_hysteresisReleaseFrames);
                tuning.Flags = 0u;
                tuningArray[0] = tuning;
            }
            finally
            {
                vault.ReleaseWriteLock(in _scalabilityTuningHandle, SystemID.HardwareHomeostasis);
            }
        }

        private static bool EnsureMockHeavyLoadHandle(IDataVault vault)
        {
            if (!OpenOrAcquireVaultBufferForOwnerRoute(
                    vault,
                    ref _mockHeavyLoadHandle,
                    BufferID.ShinobuScalabilityMockHeavyLoad,
                    DictatorSingletonLength,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<MockHeavyLoadSignal> signalArray,
                    out bool created))
            {
                return false;
            }

            if (created)
                MemClearIfCreated(signalArray);
            return true;
        }

        private static bool EnsureMockTerrainSamplerStatusHandle(IDataVault vault)
        {
            if (!OpenOrAcquireVaultBufferForOwnerRoute(
                    vault,
                    ref _mockTerrainSamplerStatusHandle,
                    BufferID.ShinobuScalabilityMockScatterDensity,
                    DictatorSingletonLength,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<MockTerrainSamplerStatus> terrainSampler,
                    out bool created))
            {
                return false;
            }

            if (created)
                MemClearIfCreated(terrainSampler);
            return true;
        }

        private static bool EnsureScalabilityTelemetryHandle(IDataVault vault)
        {
            if (!OpenOrAcquireVaultBufferForOwnerRoute(
                    vault,
                    ref _scalabilityTelemetryHandle,
                    BufferID.ShinobuScalabilityOscilloscope,
                    ScalabilityTelemetryCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out NativeArray<ScalabilityTelemetryEntry> telemetry,
                    out bool created))
            {
                return false;
            }

            if (created)
            {
                MemClearIfCreated(telemetry);
                _scalabilityTelemetryCursor = 0;
                _scalabilityTelemetrySampleCount = 0;
            }

            return true;
        }

        private static bool TryReadScalabilityTelemetry(out NativeArray<ScalabilityTelemetryEntry>.ReadOnly telemetry)
        {
            telemetry = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                _scalabilityTelemetryHandle.BufferID == 0u ||
                _scalabilityTelemetryHandle.Generation == 0u)
                return false;

            return vault.TryReadOnlyHandle(in _scalabilityTelemetryHandle, out telemetry) &&
                   telemetry.Length >= ScalabilityTelemetryCapacity;
        }

        private static void RecordScalabilityTelemetry(float rawFrameMs, float vramPressure01, uint flags)
        {
            IDataVault vault = _dataVault;
            if (!TryAcquireWriteView(vault, in _scalabilityTelemetryHandle, ScalabilityTelemetryCapacity, out NativeArray<ScalabilityTelemetryEntry> telemetry))
            {
                return;
            }

            try
            {
                int index = _scalabilityTelemetryCursor;
                if ((uint)index >= ScalabilityTelemetryCapacity)
                    index = 0;

                float safeRawFrameMs = SanitizePositiveFrameMs(rawFrameMs);
                float smoothedFrameMs = _fpsEwma > 0f
                    ? 1000f * math.rcp(math.max(1f, _fpsEwma))
                    : safeRawFrameMs;

                ScalabilityTelemetryEntry entry = telemetry[index];
                entry.Timestamp = unchecked((ulong)_lastStopwatchTimestamp);
                entry.RawFrameMs = safeRawFrameMs;
                entry.SmoothedFrameMs = math.isfinite(smoothedFrameMs) && smoothedFrameMs > 0f ? smoothedFrameMs : safeRawFrameMs;
                entry.GlobalQualityWeight = SanitizeQualityWeight01(_globalQualityWeight, 0f);
                entry.VramPressure = SanitizePressure01(vramPressure01, 1f);
                entry.Flags = flags;
                entry._pad0 = 0u;
                telemetry[index] = entry;

                index++;
                _scalabilityTelemetryCursor = index >= ScalabilityTelemetryCapacity ? 0 : index;
                if (_scalabilityTelemetrySampleCount < ScalabilityTelemetryCapacity)
                    _scalabilityTelemetrySampleCount++;
            }
            finally
            {
                vault.ReleaseWriteLock(in _scalabilityTelemetryHandle, SystemID.HardwareHomeostasis);
            }
        }

        private static bool TryAcquireWriteView<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> view)
            where T : struct
        {
            view = default;
            bool handedOff = false;
            if (vault == null ||
                handle.BufferID == 0u ||
                handle.Generation == 0u ||
                !vault.TryAcquireWriteLock(in handle, SystemID.HardwareHomeostasis, out view))
            {
                return false;
            }

            try
            {
                if (view.IsCreated && view.Length >= requiredLength)
                {
                    handedOff = true;
                    return true;
                }

                view = default;
                return false;
            }
            finally
            {
                if (!handedOff)
                    vault.ReleaseWriteLock(in handle, SystemID.HardwareHomeostasis);
            }
        }

        private static unsafe void MemClearIfCreated<T>(NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated || array.Length <= 0)
                return;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            UnsafeUtility.MemClear(ptr, (long)array.Length * UnsafeUtility.SizeOf<T>());
        }

        private static long ResolveScalabilityDictatorRequestedVaultBytes()
        {
            return UnsafeUtility.SizeOf<SystemHealthDTO>() +
                   UnsafeUtility.SizeOf<ScalabilityStateDTO>() +
                   UnsafeUtility.SizeOf<MockHeavyLoadSignal>() +
                   UnsafeUtility.SizeOf<MockTerrainSamplerStatus>() +
                   UnsafeUtility.SizeOf<ScalabilityTuningDTO>() +
                   ((long)ScalabilityTelemetryCapacity * UnsafeUtility.SizeOf<ScalabilityTelemetryEntry>()) +
                   MathLodRuntimeConfig.ResolveRequestedBytes();
        }

        private static void RefreshMockTerrainSamplerStatus(int frame, ulong targetMask)
        {
            uint foldedFlags = FoldMaskToUInt(targetMask);
            float safeGlobalQualityWeight = SanitizeQualityWeight01(_globalQualityWeight, 0f);
            int qualityBucket = (int)math.round(safeGlobalQualityWeight * 100f);
            int framesSinceLast = frame - _lastMockTerrainScheduleFrame;
            bool cadenceDue = framesSinceLast < 0 || framesSinceLast >= MockTerrainSamplerCadenceFrames;
            if (!cadenceDue &&
                foldedFlags == _lastMockTerrainFlags &&
                qualityBucket == _lastMockTerrainQualityBucket)
            {
                return;
            }

            IDataVault vault = _dataVault;
            if (!TryAcquireWriteView(
                    vault,
                    in _mockTerrainSamplerStatusHandle,
                    DictatorSingletonLength,
                    out NativeArray<MockTerrainSamplerStatus> terrainSampler))
            {
                return;
            }

            try
            {
                WriteMockTerrainSamplerStatus(terrainSampler, safeGlobalQualityWeight, unchecked((uint)frame));
            }
            finally
            {
                vault.ReleaseWriteLock(in _mockTerrainSamplerStatusHandle, SystemID.HardwareHomeostasis);
            }

            _lastMockTerrainScheduleFrame = frame;
            _lastMockTerrainQualityBucket = qualityBucket;
            _lastMockTerrainFlags = foldedFlags;
        }

        private static void WriteMockTerrainSamplerStatus(
            NativeArray<MockTerrainSamplerStatus> terrainSampler,
            float globalQualityWeight,
            uint frame)
        {
            if (!terrainSampler.IsCreated || terrainSampler.Length < DictatorSingletonLength)
            {
                return;
            }

            float weight = SanitizeQualityWeight01(globalQualityWeight, 0f);
            MockTerrainSamplerStatus status = default;
            status.GlobalQualityWeight = weight;
            status.TrilinearSampleProbability01 = weight;
            status.SkippedTrilinearPercent01 = 1f - weight;
            status.Frame = frame;
            terrainSampler[0] = status;
        }

        private static void ResolveHardwareConstraintPolicy()
        {
            string model = SystemInfo.deviceModel;
            HardwareTierDetector.EnsureInitialized();
            _deviceModelHash = HashOrdinalIgnoreCase(model);
            float modelConstraint01 = ResolveKnownHardwareConstraint01(model);
            float memoryConstraint01 = ResolveMemoryConstraint01(SystemInfo.systemMemorySize, 12288f, 8192f);
            float vramConstraint01 = ResolveMemoryConstraint01(SystemInfo.graphicsMemorySize, 4096f, 3072f);
            _hardwareConstraintPressure01 = math.saturate(math.max(modelConstraint01, math.max(memoryConstraint01, vramConstraint01)));
            float curvedConstraint01 = SmoothStep01(_hardwareConstraintPressure01);
            _hardwareShiFloor = SurvivalHardwareShiFloor * curvedConstraint01;
            _hardwareMaxQualityWeight = math.lerp(1f, SurvivalHardwareMaxQualityWeight, curvedConstraint01);
        }

        private static float ResolveHardwareConstraintPressure01()
        {
            return math.saturate(math.isfinite(_hardwareConstraintPressure01) ? _hardwareConstraintPressure01 : 0f);
        }

        private static float ResolveKnownHardwareConstraint01(string model)
        {
            float constraint01 = 0f;
            if (ContainsOrdinalIgnoreCase(model, "quest 2"))
            {
                constraint01 = 1f;
            }
            else if (HardwareTierDetector.SharedMemoryModeActive)
            {
                constraint01 = HardwareTierDetector.RecommendedVramBudgetMegabytes <= 1024 ? 0.85f : 0.65f;
            }
            else if (ContainsOrdinalIgnoreCase(model, "quest 3") ||
                     ContainsOrdinalIgnoreCase(model, "steam deck"))
            {
                constraint01 = 0.65f;
            }

            return constraint01;
        }

        private static float ResolveMemoryConstraint01(int megabytes, float comfortableMegabytes, float falloffMegabytes)
        {
            if (megabytes <= 0 || !math.isfinite(comfortableMegabytes) || !math.isfinite(falloffMegabytes))
                return 0f;

            float denominator = math.max(1f, falloffMegabytes);
            return math.saturate((comfortableMegabytes - megabytes) * math.rcp(denominator));
        }

        private static long ResolveGraphicsMemoryBudgetBytes()
        {
            int graphicsMemoryMb = SystemInfo.graphicsMemorySize;
            if (graphicsMemoryMb <= 0)
                return 0L;

            return (long)graphicsMemoryMb * 1024L * 1024L;
        }

        private static uint HashOrdinalIgnoreCase(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0u;

            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);
                hash ^= c;
                hash *= 16777619u;
            }

            return hash;
        }

        private static bool ContainsOrdinalIgnoreCase(string value, string token)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(token) || token.Length > value.Length)
                return false;

            int maxStart = value.Length - token.Length;
            for (int start = 0; start <= maxStart; start++)
            {
                bool match = true;
                for (int i = 0; i < token.Length; i++)
                {
                    char a = value[start + i];
                    char b = token[i];
                    if (a >= 'A' && a <= 'Z')
                        a = (char)(a + 32);
                    if (b >= 'A' && b <= 'Z')
                        b = (char)(b + 32);
                    if (a != b)
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return true;
            }

            return false;
        }

        private static string ResolveScalabilityCsvPath()
        {
#if UNITY_EDITOR
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string rootProfile = Path.Combine(projectRoot, ScalabilityCsvFileName);
            if (File.Exists(rootProfile))
                return rootProfile;

            return Path.Combine(projectRoot, "Assets", "_Project", "Data", ScalabilityCsvFileName);
#else
            return null;
#endif
        }

        private static void TryPollCsvOverrides(int frame)
        {
#if !UNITY_EDITOR
            return;
#else
            if (_csvPollCountdown > 0)
            {
                _csvPollCountdown--;
                return;
            }

            _csvPollCountdown = CsvPollCadenceFrames;
            string path = _csvProfilePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            DateTime lastWriteUtc = File.GetLastWriteTimeUtc(path);
            if (lastWriteUtc == _csvLastWriteUtc)
                return;

            _csvLastWriteUtc = lastWriteUtc;
            Span<byte> csvScratch = stackalloc byte[ScalabilityCsvScratchBytes];
            int bytesRead = TryReadCsvFile(path, csvScratch);
            if (bytesRead > 0)
                ParseScalabilityCsv(csvScratch.Slice(0, bytesRead), frame);
#endif
        }

#if UNITY_EDITOR
        private static int TryReadCsvFile(string path, Span<byte> csvScratch)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long streamLength = stream.Length;
                    if (streamLength <= 0L || streamLength > csvScratch.Length)
                        return 0;

                    int expectedBytes = (int)streamLength;
                    int bytesRead = 0;
                    while (bytesRead < expectedBytes)
                    {
                        int read = stream.Read(csvScratch.Slice(bytesRead, expectedBytes - bytesRead));
                        if (read <= 0)
                            return 0;

                        bytesRead += read;
                    }

                    return bytesRead;
                }
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
        }

        private static void ParseScalabilityCsv(ReadOnlySpan<byte> bytes, int frame)
        {
            int cursor = 0;
            while (TryReadCsvLine(bytes, ref cursor, out int lineStart, out int lineEnd))
            {
                TrimAscii(bytes, ref lineStart, ref lineEnd);
                if (lineStart >= lineEnd || bytes[lineStart] == (byte)'#')
                    continue;

                int separator = FindSeparator(bytes, lineStart, lineEnd);
                if (separator <= lineStart)
                    continue;

                int keyStart = lineStart;
                int keyEnd = separator;
                int valueStart = separator + 1;
                int valueEnd = lineEnd;
                TrimAscii(bytes, ref keyStart, ref keyEnd);
                TrimAscii(bytes, ref valueStart, ref valueEnd);
                ApplyCsvOverride(bytes, keyStart, keyEnd, valueStart, valueEnd, frame);
            }
        }

        private static void ApplyCsvOverride(
            ReadOnlySpan<byte> bytes,
            int keyStart,
            int keyEnd,
            int valueStart,
            int valueEnd,
            int frame)
        {
            if (EqualsAscii(bytes, keyStart, keyEnd, "target_frame_ms") &&
                TryParseFloatAscii(bytes, valueStart, valueEnd, out float targetFrameMs))
            {
                ApplyHardwareDictatorTuner(targetFrameMs, _emergencyThresholdOverride, _hysteresisReleaseFrames);
                return;
            }

            if (EqualsAscii(bytes, keyStart, keyEnd, "emergency_threshold") &&
                TryParseFloatAscii(bytes, valueStart, valueEnd, out float threshold))
            {
                ApplyHardwareDictatorTuner(_targetFrameMsOverride, threshold, _hysteresisReleaseFrames);
                return;
            }

            if (EqualsAscii(bytes, keyStart, keyEnd, "hysteresis_frames") &&
                TryParseIntAscii(bytes, valueStart, valueEnd, out int frames))
            {
                ApplyHardwareDictatorTuner(_targetFrameMsOverride, _emergencyThresholdOverride, frames);
                return;
            }

            if (EqualsAscii(bytes, keyStart, keyEnd, "force_visual_overkill") &&
                TryParseIntAscii(bytes, valueStart, valueEnd, out int overkill))
            {
                _forceVisualOverkillOverride = overkill != 0;
                return;
            }

            if ((EqualsAscii(bytes, keyStart, keyEnd, "forced_global_quality_weight") ||
                 EqualsAscii(bytes, keyStart, keyEnd, "force_quality_weight")) &&
                TryParseFloatAscii(bytes, valueStart, valueEnd, out float qualityWeight))
            {
                SetForcedGlobalQualityWeightForTuner(qualityWeight, qualityWeight >= 0f);
                return;
            }

            if (EqualsAscii(bytes, keyStart, keyEnd, "mock_frame_spike_ms") &&
                TryParseFloatAscii(bytes, valueStart, valueEnd, out float spikeMs))
            {
                SetMockHeavyLoadForTuner(spikeMs, -1f, spikeMs > 0f);
                return;
            }

            if (EqualsAscii(bytes, keyStart, keyEnd, "mock_vram_pressure") &&
                TryParseFloatAscii(bytes, valueStart, valueEnd, out float mockVram))
            {
                SetMockHeavyLoadForTuner(-1f, mockVram, mockVram > 0f);
                return;
            }

            if (EqualsAscii(bytes, keyStart, keyEnd, "culling_multiplier_low") &&
                TryParseFloatAscii(bytes, valueStart, valueEnd, out float culling))
            {
                _lowCullingMultiplier = SanitizeLowCullingMultiplier(culling);
                return;
            }

            if (EqualsAscii(bytes, keyStart, keyEnd, "gc_safe_menu") &&
                TryParseIntAscii(bytes, valueStart, valueEnd, out int safeMenu))
            {
                _gcSafeBaseMenuArmed = safeMenu != 0;
            }
        }

        private static bool TryReadCsvLine(
            ReadOnlySpan<byte> bytes,
            ref int cursor,
            out int lineStart,
            out int lineEnd)
        {
            lineStart = cursor;
            lineEnd = cursor;
            int length = bytes.Length;
            if (cursor >= length)
                return false;

            while (cursor < length)
            {
                byte c = bytes[cursor];
                if (c == (byte)'\n' || c == (byte)'\r')
                    break;
                cursor++;
            }

            lineEnd = cursor;
            while (cursor < length && (bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r'))
                cursor++;
            return true;
        }

        private static void TrimAscii(ReadOnlySpan<byte> bytes, ref int start, ref int end)
        {
            while (start < end && IsAsciiWhitespace(bytes[start]))
                start++;
            while (end > start && IsAsciiWhitespace(bytes[end - 1]))
                end--;
        }

        private static int FindSeparator(ReadOnlySpan<byte> bytes, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                byte c = bytes[i];
                if (c == (byte)',' || c == (byte)'=' || c == (byte)';')
                    return i;
            }

            return -1;
        }

        private static bool EqualsAscii(ReadOnlySpan<byte> bytes, int start, int end, string expected)
        {
            int length = end - start;
            if (expected == null || length != expected.Length)
                return false;

            for (int i = 0; i < length; i++)
            {
                byte a = bytes[start + i];
                char b = expected[i];
                if (a >= (byte)'A' && a <= (byte)'Z')
                    a = (byte)(a + 32);
                if (b >= 'A' && b <= 'Z')
                    b = (char)(b + 32);
                if (a != (byte)b)
                    return false;
            }

            return true;
        }

        private static bool TryParseIntAscii(ReadOnlySpan<byte> bytes, int start, int end, out int value)
        {
            value = 0;
            bool negative = false;
            if (start < end && bytes[start] == (byte)'-')
            {
                negative = true;
                start++;
            }

            bool any = false;
            int result = 0;
            for (int i = start; i < end; i++)
            {
                byte c = bytes[i];
                if (c < (byte)'0' || c > (byte)'9')
                    return false;
                result = result * 10 + (c - (byte)'0');
                any = true;
            }

            if (!any)
                return false;

            value = negative ? -result : result;
            return true;
        }

        private static bool TryParseFloatAscii(ReadOnlySpan<byte> bytes, int start, int end, out float value)
        {
            value = 0f;
            bool negative = false;
            if (start < end && bytes[start] == (byte)'-')
            {
                negative = true;
                start++;
            }

            bool any = false;
            float result = 0f;
            while (start < end)
            {
                byte c = bytes[start];
                if (c == (byte)'.')
                    break;
                if (c < (byte)'0' || c > (byte)'9')
                    return false;
                result = result * 10f + (c - (byte)'0');
                start++;
                any = true;
            }

            if (start < end && bytes[start] == (byte)'.')
            {
                start++;
                float scale = 0.1f;
                while (start < end)
                {
                    byte c = bytes[start];
                    if (c < (byte)'0' || c > (byte)'9')
                        return false;
                    result += (c - (byte)'0') * scale;
                    scale *= 0.1f;
                    start++;
                    any = true;
                }
            }

            if (!any)
                return false;

            value = negative ? -result : result;
            return math.isfinite(value);
        }

        private static bool IsAsciiWhitespace(byte c)
        {
            return c == (byte)' ' || c == (byte)'\t';
        }
#endif

        private static void ValidateScalabilityDtoLayouts(NativeArray<HomeostasisBlackBoxEntry> blackBox)
        {
            if (UnsafeUtility.SizeOf<SystemHealthDTO>() == 16 &&
                UnsafeUtility.SizeOf<ScalabilityStateDTO>() == 16 &&
                UnsafeUtility.SizeOf<MockHeavyLoadSignal>() == 16 &&
                UnsafeUtility.SizeOf<MockTerrainSamplerStatus>() == 16 &&
                UnsafeUtility.SizeOf<ScalabilityTelemetryEntry>() == 64 &&
                UnsafeUtility.SizeOf<ScalabilityTuningDTO>() == 16)
            {
                return;
            }

            DumpScalabilityDictatorBlackBoxOnce(blackBox);
        }

        private static void DumpScalabilityDictatorBlackBoxOnce(NativeArray<HomeostasisBlackBoxEntry> blackBox)
        {
            if (_scalabilityDumped || !blackBox.IsCreated)
                return;

            _scalabilityDumped = true;
            try
            {
                if (TryReadScalabilityTelemetry(out NativeArray<ScalabilityTelemetryEntry>.ReadOnly telemetry))
                {
                    WriteScalabilityTelemetryFile(ScalabilityDumpRelativePath, telemetry);
                    WriteScalabilityTelemetryFile(ScalabilityH8DumpRelativePath, telemetry);
                }
                else
                {
                    WriteScalabilityDictatorBlackBoxFile(ScalabilityDumpRelativePath, blackBox);
                    WriteScalabilityDictatorBlackBoxFile(ScalabilityH8DumpRelativePath, blackBox);
                }
            }
            catch (Exception)
            {
            }
        }

        private static unsafe void WriteScalabilityTelemetryFile(
            string path,
            NativeArray<ScalabilityTelemetryEntry>.ReadOnly telemetry)
        {
            if (!telemetry.IsCreated || telemetry.Length < ScalabilityTelemetryCapacity)
                return;

            int byteCount = ScalabilityDumpHeaderBytes + (ScalabilityTelemetryCapacity * ScalabilityDumpEntryBytes);
            NativeArray<byte> payload = default;
            const string dumpPayloadLabel = "scalabilityTelemetryDumpPayload";
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(HomeostasisBrain),
                    dumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                WriteUInt32LittleEndian(target, 0, 0x53434454u);
                WriteInt32LittleEndian(target, 4, 2);
                WriteInt32LittleEndian(target, 8, ScalabilityTelemetryCapacity);
                WriteInt32LittleEndian(target, 12, _scalabilityTelemetryCursor);
                WriteInt32LittleEndian(target, 16, ScalabilityDumpEntryBytes);

                int cursor = ScalabilityDumpHeaderBytes;
                float fallbackFrameMs = ResolveTargetFrameMs(ResolveTargetFrameRate());
                for (int i = 0; i < ScalabilityTelemetryCapacity; i++)
                {
                    int index = _scalabilityTelemetryCursor + i;
                    if (index >= ScalabilityTelemetryCapacity)
                        index -= ScalabilityTelemetryCapacity;

                    ScalabilityTelemetryEntry entry = SanitizeTelemetryEntryForDump(telemetry[index], fallbackFrameMs);
                    UnsafeUtility.MemClear(target + cursor, ScalabilityDumpEntryBytes);
                    WriteUInt64LittleEndian(target + cursor, 0, entry.Timestamp);
                    WriteFloatLittleEndian(target + cursor, 8, entry.RawFrameMs);
                    WriteFloatLittleEndian(target + cursor, 12, entry.SmoothedFrameMs);
                    WriteFloatLittleEndian(target + cursor, 16, entry.GlobalQualityWeight);
                    WriteFloatLittleEndian(target + cursor, 20, entry.VramPressure);
                    WriteUInt32LittleEndian(target + cursor, 24, entry.Flags);
                    WriteUInt32LittleEndian(target + cursor, 28, entry._pad0);
                    cursor += ScalabilityDumpEntryBytes;
                }

                NativeFaultDumpWriter.TryWriteAll(path, payload, cursor);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(ref payload, nameof(HomeostasisBrain), dumpPayloadLabel);
            }
        }

        private static ScalabilityTelemetryEntry SanitizeTelemetryEntryForDump(
            ScalabilityTelemetryEntry entry,
            float fallbackFrameMs)
        {
            float safeFallbackFrameMs = math.isfinite(fallbackFrameMs) && fallbackFrameMs > 0f
                ? fallbackFrameMs
                : ScalabilityContract.TargetFrameMilliseconds;
            bool sanitized = false;
            if (!math.isfinite(entry.RawFrameMs) || entry.RawFrameMs <= 0f)
            {
                entry.RawFrameMs = safeFallbackFrameMs;
                sanitized = true;
            }

            if (!math.isfinite(entry.SmoothedFrameMs) || entry.SmoothedFrameMs <= 0f)
            {
                entry.SmoothedFrameMs = entry.RawFrameMs;
                sanitized = true;
            }

            if (!math.isfinite(entry.GlobalQualityWeight))
            {
                entry.GlobalQualityWeight = 0f;
                sanitized = true;
            }
            else
            {
                entry.GlobalQualityWeight = math.saturate(entry.GlobalQualityWeight);
            }

            if (!math.isfinite(entry.VramPressure))
            {
                entry.VramPressure = 1f;
                sanitized = true;
            }
            else
            {
                entry.VramPressure = math.saturate(entry.VramPressure);
            }

            if (sanitized)
                entry.Flags |= ScalabilityTelemetryFlagSanitized;
            entry._pad0 = 0u;
            return entry;
        }

        private static unsafe void WriteFloatLittleEndian(byte* destination, int offset, float value)
        {
            WriteUInt32LittleEndian(destination, offset, math.asuint(value));
        }

        private static unsafe void WriteScalabilityDictatorBlackBoxFile(
            string path,
            NativeArray<HomeostasisBlackBoxEntry> blackBox)
        {
            if (!blackBox.IsCreated || blackBox.Length < BlackBoxCapacity)
                return;

            int byteCount = ScalabilityDumpHeaderBytes + (BlackBoxCapacity * ScalabilityDumpEntryBytes);
            NativeArray<byte> payload = default;
            const string dumpPayloadLabel = "scalabilityDictatorBlackBoxDumpPayload";
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(HomeostasisBrain),
                    dumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                WriteUInt32LittleEndian(target, 0, 0x53484944u);
                WriteInt32LittleEndian(target, 4, 1);
                WriteInt32LittleEndian(target, 8, BlackBoxCapacity);
                WriteInt32LittleEndian(target, 12, _blackBoxCursor);
                WriteInt32LittleEndian(target, 16, ScalabilityDumpEntryBytes);

                int cursor = ScalabilityDumpHeaderBytes;
                for (int i = 0; i < BlackBoxCapacity; i++)
                {
                    int index = _blackBoxCursor + i;
                    if (index >= BlackBoxCapacity)
                        index -= BlackBoxCapacity;

                    HomeostasisBlackBoxEntry entry = blackBox[index];
                    float qualityWeight = math.asfloat(entry.Reserved2);
                    if (!math.isfinite(qualityWeight))
                        qualityWeight = SanitizeQualityWeight01(1f - entry.SystemHealthIndex01, 0f);
                    else
                        qualityWeight = SanitizeQualityWeight01(qualityWeight, 0f);
                    UnsafeUtility.MemClear(target + cursor, ScalabilityDumpEntryBytes);
                    WriteUInt32LittleEndian(target + cursor, 0, entry.Frame);
                    WriteFloatLittleEndian(target + cursor, 4, qualityWeight);
                    WriteUInt64LittleEndian(target + cursor, 8, entry.KillSwitchMask);
                    WriteFloatLittleEndian(target + cursor, 16, entry.FpsEwma);
                    WriteFloatLittleEndian(target + cursor, 20, entry.JitterSigmaMs);
                    WriteFloatLittleEndian(target + cursor, 24, entry.CpuTempC);
                    WriteFloatLittleEndian(target + cursor, 28, entry.GpuUtil01);
                    WriteFloatLittleEndian(target + cursor, 32, entry.BatteryLife01);
                    target[cursor + 36] = entry.PressureLevel;
                    target[cursor + 37] = entry.FoveatedPressureTier;
                    WriteUInt16LittleEndian(target + cursor, 38, entry.Flags);
                    WriteFloatLittleEndian(target + cursor, 40, entry.TimeDilationScalar);
                    WriteFloatLittleEndian(target + cursor, 44, entry.PeakSystemHealthIndex01);
                    WriteUInt32LittleEndian(target + cursor, 48, entry.LastThermalAction);
                    WriteFloatLittleEndian(target + cursor, 52, math.asfloat(entry.Reserved0));
                    WriteFloatLittleEndian(target + cursor, 56, math.asfloat(entry.Reserved1));
                    WriteUInt32LittleEndian(target + cursor, 60, entry.Reserved2);
                    cursor += ScalabilityDumpEntryBytes;
                }

                NativeFaultDumpWriter.TryWriteAll(path, payload, cursor);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(ref payload, nameof(HomeostasisBrain), dumpPayloadLabel);
            }
        }

        private static unsafe void WriteInt32LittleEndian(byte* target, int offset, int value)
        {
            WriteUInt32LittleEndian(target, offset, unchecked((uint)value));
        }

        private static unsafe void WriteUInt16LittleEndian(byte* target, int offset, ushort value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
        }

        private static unsafe void WriteUInt32LittleEndian(byte* target, int offset, uint value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }

        private static unsafe void WriteUInt64LittleEndian(byte* target, int offset, ulong value)
        {
            WriteUInt32LittleEndian(target, offset, unchecked((uint)value));
            WriteUInt32LittleEndian(target, offset + 4, unchecked((uint)(value >> 32)));
        }

        /// <summary>
        /// Editor/test facade for the Hardware Dictator Tuner.
        /// </summary>
        public static void ApplyHardwareDictatorTuner(float targetFrameMs, float emergencyThreshold, int hysteresisFrames)
        {
            _targetFrameMsOverride = SanitizeTunerTargetFrameMs(targetFrameMs);
            _emergencyThresholdOverride = SanitizeTunerEmergencyThreshold(emergencyThreshold);
            _hysteresisReleaseFrames = SanitizeTunerHysteresisFrames(hysteresisFrames);
            WriteCurrentTuningStateToVault(_dataVault);

            float frameMs = 0f;
            float vramPressure01 = 0f;
            float thermalIndex = 0f;
            IDataVault vault = _dataVault;
            if (EnsureScalabilityStateHandles(vault) &&
                TryReadScalabilityStateViews(
                    vault,
                    out NativeArray<SystemHealthDTO>.ReadOnly healthArray,
                    out _))
            {
                SystemHealthDTO health = healthArray[0];
                frameMs = health.FrameTimeMs;
                vramPressure01 = health.VramPressure;
                thermalIndex = health.ThermalIndex;
            }

            WriteDictatorState(SystemDispatcher.CurrentFrameIndex, frameMs, vramPressure01, thermalIndex, _currentKillSwitchMask);
        }

        /// <summary>
        /// Reads the unmanaged tuner state that backs the editor and CSV facade.
        /// </summary>
        public static bool TryGetHardwareDictatorTuning(out ScalabilityTuningDTO tuning)
        {
            tuning = default;
            IDataVault vault = _dataVault;
            if (!TryReadScalabilityTuningView(vault, out NativeArray<ScalabilityTuningDTO>.ReadOnly tuningArray))
            {
                return false;
            }

            ScalabilityTuningDTO vaultTuning = tuningArray[0];
            tuning.TargetFrameMs = SanitizeTunerTargetFrameMs(vaultTuning.TargetFrameMs);
            tuning.EmergencyThreshold = SanitizeTunerEmergencyThreshold(vaultTuning.EmergencyThreshold);
            tuning.HysteresisReleaseFrames = SanitizeTunerHysteresisFrames(vaultTuning.HysteresisReleaseFrames);
            tuning.Flags = 0u;
            return true;
        }

        /// <summary>
        /// Runtime user preference facade for continuous quality. Hardware ceilings still clamp the effective value.
        /// </summary>
        public static void SetUserGlobalQualityWeightPreference(float qualityWeight, bool enabled)
        {
            SetForcedGlobalQualityWeightOverride(qualityWeight, enabled);
        }

        /// <summary>
        /// Editor/test facade for forced continuous quality. Negative values disable the override.
        /// </summary>
        public static void SetForcedGlobalQualityWeightForTuner(float qualityWeight, bool enabled)
        {
            SetForcedGlobalQualityWeightOverride(qualityWeight, enabled);
        }

        private static void SetForcedGlobalQualityWeightOverride(float qualityWeight, bool enabled)
        {
            float sanitizedWeight = ForcedQualityWeightDisabled;
            bool validOverride = enabled && TrySanitizeForcedQualityWeight(qualityWeight, out sanitizedWeight);
            _forceGlobalQualityWeightOverride = validOverride;
            _forcedGlobalQualityWeight = validOverride ? sanitizedWeight : ForcedQualityWeightDisabled;
            if (validOverride)
                _globalQualityWeightSeeded = false;
        }

        /// <summary>
        /// Editor/test facade that arms or clears the synthetic load signal.
        /// </summary>
        public static void SetMockHeavyLoadForTuner(float frameSpikeMs, float vramPressure01, bool enabled)
        {
            IDataVault vault = _dataVault;
            if (!EnsureMockHeavyLoadHandle(vault) ||
                !TryAcquireWriteView(vault, in _mockHeavyLoadHandle, DictatorSingletonLength, out NativeArray<MockHeavyLoadSignal> signalArray))
            {
                return;
            }

            try
            {
                MockHeavyLoadSignal signal = signalArray[0];
                uint previousFlags = signal.Flags;
                bool wasEnabled = (previousFlags & MockHeavyLoadSignal.FlagEnabled) != 0u;
                if (!wasEnabled)
                {
                    if (frameSpikeMs < 0f && vramPressure01 >= 0f)
                        signal.FrameSpikeMs = 0f;
                    if (vramPressure01 < 0f && frameSpikeMs >= 0f)
                        signal.VramPressure01 = 0f;
                }

                if (frameSpikeMs >= 0f)
                    signal.FrameSpikeMs = math.isfinite(frameSpikeMs) ? math.max(0f, frameSpikeMs) : 0f;
                if (vramPressure01 >= 0f)
                    signal.VramPressure01 = math.isfinite(vramPressure01) ? math.saturate(vramPressure01) : 0f;
                if (!math.isfinite(signal.FrameSpikeMs) || signal.FrameSpikeMs < 0f)
                    signal.FrameSpikeMs = 0f;
                if (!math.isfinite(signal.VramPressure01) || signal.VramPressure01 < 0f)
                    signal.VramPressure01 = 0f;
                else
                    signal.VramPressure01 = math.saturate(signal.VramPressure01);

                bool explicitFullUpdate = frameSpikeMs >= 0f && vramPressure01 >= 0f;
                if (enabled &&
                    signal.FrameSpikeMs <= 0.0001f &&
                    signal.VramPressure01 <= 0.0001f)
                {
                    signal.FrameSpikeMs = DefaultMockFrameSpikeMs;
                }

                bool hasSyntheticPressure = signal.FrameSpikeMs > 0.0001f || signal.VramPressure01 > 0.0001f;
                if (enabled)
                    signal.Flags = MockHeavyLoadSignal.FlagEnabled;
                else if (explicitFullUpdate || !hasSyntheticPressure)
                    signal.Flags = 0u;
                signalArray[0] = signal;
            }
            finally
            {
                vault.ReleaseWriteLock(in _mockHeavyLoadHandle, SystemID.HardwareHomeostasis);
            }
        }

        /// <summary>
        /// Editor/test facade that marks the current context safe for re-enabling the GC.
        /// </summary>
        public static void SetHardwareDictatorGcSafeBaseMenu(bool enabled)
        {
            _gcSafeBaseMenuArmed = enabled;
        }

        /// <summary>
        /// Copies the latest health/state DTOs without exposing mutable NativeArray wrappers.
        /// </summary>
        public static bool TryGetHardwareDictatorSnapshot(out SystemHealthDTO health, out ScalabilityStateDTO state)
        {
            health = default;
            state = default;
            IDataVault vault = _dataVault;
            if (!TryReadScalabilityStateViews(
                    vault,
                    out NativeArray<SystemHealthDTO>.ReadOnly healthArray,
                    out NativeArray<ScalabilityStateDTO>.ReadOnly stateArray))
            {
                return false;
            }

            SystemHealthDTO vaultHealth = healthArray[0];
            ScalabilityStateDTO vaultState = stateArray[0];
            health.FrameTimeMs = SanitizePositiveFrameMs(vaultHealth.FrameTimeMs);
            health.VramPressure = SanitizePressure01(vaultHealth.VramPressure, 1f);
            health.ThermalIndex = SanitizePressure01(vaultHealth.ThermalIndex, 1f);
            health.ActiveThrottlesMask = vaultHealth.ActiveThrottlesMask;
            state.GlobalQualityWeight = SanitizeQualityWeight01(vaultState.GlobalQualityWeight, GlobalQualityWeight);
            state.FractionalTimeSlice = ResolveFractionalTimeSliceFromWeight(state.GlobalQualityWeight);
            state.VramPressure = health.VramPressure;
            state.ThermalIndex = health.ThermalIndex;
            return true;
        }

        /// <summary>
        /// Copies the dependency-free terrain sampler proof status.
        /// </summary>
        public static bool TryGetMockTerrainSamplerStatus(out MockTerrainSamplerStatus status)
        {
            status = default;
            IDataVault vault = _dataVault;
            if (!TryReadMockTerrainSamplerStatusView(vault, out NativeArray<MockTerrainSamplerStatus>.ReadOnly statusArray))
            {
                return false;
            }

            MockTerrainSamplerStatus vaultStatus = statusArray[0];
            float weight = SanitizeQualityWeight01(vaultStatus.GlobalQualityWeight, GlobalQualityWeight);
            status.GlobalQualityWeight = weight;
            status.TrilinearSampleProbability01 = weight;
            status.SkippedTrilinearPercent01 = 1f - weight;
            status.Frame = vaultStatus.Frame;
            return true;
        }

        /// <summary>
        /// Copies blackbox samples into editor-owned arrays for the live oscilloscope.
        /// </summary>
        public static int CopyHardwareDictatorOscilloscope(float[] qualityWeightSamples, float[] frameMsSamples, int maxCount)
        {
            if (qualityWeightSamples == null || frameMsSamples == null || maxCount <= 0)
                return 0;

            if (TryReadScalabilityTelemetry(out NativeArray<ScalabilityTelemetryEntry>.ReadOnly telemetry))
            {
                float fallbackFrameMs = ResolveTargetFrameMs(ResolveTargetFrameRate());
                int telemetryCount = math.min(maxCount, math.min(qualityWeightSamples.Length, math.min(frameMsSamples.Length, _scalabilityTelemetrySampleCount)));
                int telemetryStart = _scalabilityTelemetryCursor - telemetryCount;
                if (telemetryStart < 0)
                    telemetryStart += ScalabilityTelemetryCapacity;

                for (int i = 0; i < telemetryCount; i++)
                {
                    int index = telemetryStart + i;
                    if (index >= ScalabilityTelemetryCapacity)
                        index -= ScalabilityTelemetryCapacity;

                    ScalabilityTelemetryEntry entry = telemetry[index];
                    qualityWeightSamples[i] = math.isfinite(entry.GlobalQualityWeight)
                        ? SanitizeQualityWeight01(entry.GlobalQualityWeight, 0f)
                        : 0f;
                    float frameMs = math.isfinite(entry.RawFrameMs) && entry.RawFrameMs > 0f
                        ? entry.RawFrameMs
                        : entry.SmoothedFrameMs;
                    frameMsSamples[i] = math.isfinite(frameMs) && frameMs > 0f ? frameMs : fallbackFrameMs;
                }

                return telemetryCount;
            }

            if (!TryResolveRuntimeBuffers(
                    out _,
                    out _,
                    out NativeArray<HomeostasisBlackBoxEntry> blackBox) ||
                !blackBox.IsCreated)
            {
                return 0;
            }

            float fallbackBlackBoxFrameMs = ResolveTargetFrameMs(ResolveTargetFrameRate());
            int count = math.min(maxCount, math.min(qualityWeightSamples.Length, math.min(frameMsSamples.Length, BlackBoxCapacity)));
            int start = _blackBoxCursor - count;
            if (start < 0)
                start += BlackBoxCapacity;

            for (int i = 0; i < count; i++)
            {
                int index = start + i;
                if (index >= BlackBoxCapacity)
                    index -= BlackBoxCapacity;

                HomeostasisBlackBoxEntry entry = blackBox[index];
                float qualityWeight = math.asfloat(entry.Reserved2);
                qualityWeightSamples[i] = math.isfinite(qualityWeight)
                    ? SanitizeQualityWeight01(qualityWeight, 0f)
                    : SanitizeQualityWeight01(1f - entry.SystemHealthIndex01, 0f);
                float rawFrameMs = math.asfloat(entry.Reserved0);
                float frameMs = math.isfinite(rawFrameMs) && rawFrameMs > 0f
                    ? rawFrameMs
                    : (entry.FpsEwma > 0f ? 1000f * math.rcp(math.max(1f, entry.FpsEwma)) : 0f);
                frameMsSamples[i] = math.isfinite(frameMs) && frameMs > 0f ? frameMs : fallbackBlackBoxFrameMs;
            }

            return count;
        }
    }
}
