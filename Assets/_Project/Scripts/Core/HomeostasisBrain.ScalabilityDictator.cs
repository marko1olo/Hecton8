using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Scripting;

namespace Hecton8.Core
{
    /// <summary>
    /// 16-byte cache-line friendly health DTO written directly into GlobalDataVault memory.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct SystemHealthDTO
    {
        public float FrameTimeMs;
        public float VramPressure;
        public float ThermalIndex;
        public uint ActiveThrottlesMask;
    }

    /// <summary>
    /// 16-byte scalability state DTO consumed by editor tooling and cross-domain readers.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct ScalabilityStateDTO
    {
        public float GlobalQualityWeight;
        public float FractionalTimeSlice;
        public float VramPressure;
        public float ThermalIndex;
    }

    /// <summary>
    /// Synthetic heavy-load input used to prove throttling without a renderer or AI dependency.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public partial struct MockHeavyLoadSignal
    {
        public const uint FlagEnabled = 1u << 0;

        public float FrameSpikeMs;
        public float VramPressure01;
        public uint Flags;
        public uint _pad0;
    }

    /// <summary>
    /// Mock terrain-sampler output that proves continuous trilinear throttling without a terrain dependency.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public partial struct MockTerrainSamplerStatus
    {
        public float GlobalQualityWeight;
        public float TrilinearSampleProbability01;
        public float SkippedTrilinearPercent01;
        public uint Frame;
    }

    /// <summary>
    /// 32-byte dictator-local telemetry row. One half cache-line, explicit alignment, no managed fields.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ScalabilityTelemetryEntry
    {
        [FieldOffset(0)] public ulong Timestamp;
        [FieldOffset(8)] public float RawFrameMs;
        [FieldOffset(12)] public float SmoothedFrameMs;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public float VramPressure;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint _pad0;
    }

    /// <summary>
    /// 16-byte editor/CSV tuning DTO. Written into GlobalDataVault, mirrored into hot scalar fields.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct ScalabilityTuningDTO
    {
        public float TargetFrameMs;
        public float EmergencyThreshold;
        public int HysteresisReleaseFrames;
        public uint Flags;
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
        private const float ScalabilityHardFailFrameMs = 20f;
        private const float LowHardwareShiFloor = 0.4f;
        private const float LowHardwareMaxQualityWeight = 0.6f;
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
        private const string ScalabilityDumpFileName = "Dump_SCALABILITY_DICTATOR.bin";
        private const string ScalabilityH8DumpFileName = "Dump_SCALABILITY_DICTATOR.h8dump";
        private const string ScalabilityCsvFileName = "scalability_curves.csv";

        private static readonly int _mathLodLowScalarId = Shader.PropertyToID("_MATH_LOD_LOW");
        private static readonly int _cullingMultiplierId = Shader.PropertyToID("_H8CullingMultiplier");
        private static readonly int _globalQualityWeightId = Shader.PropertyToID("_GlobalQualityWeight");
        private static readonly int _h8GlobalQualityWeightId = Shader.PropertyToID("_H8GlobalQualityWeight");

        private static VaultBufferHandle<SystemHealthDTO> _systemHealthDtoHandle;
        private static VaultBufferHandle<ScalabilityStateDTO> _scalabilityStateHandle;
        private static VaultBufferHandle<MockHeavyLoadSignal> _mockHeavyLoadHandle;
        private static VaultBufferHandle<MockTerrainSamplerStatus> _mockTerrainSamplerStatusHandle;
        private static VaultBufferHandle<ScalabilityTelemetryEntry> _scalabilityTelemetryHandle;
        private static VaultBufferHandle<ScalabilityTuningDTO> _scalabilityTuningHandle;
        private static VaultBufferHandle<byte> _csvScratchHandle;

        private static JobHandle _mockTerrainSamplerJobHandle;
        private static bool _mockTerrainSamplerJobPending;
        private static IDynamicResolutionRuntime _dynamicResolutionRuntime;
        private static long _lastStopwatchTimestamp;
        private static bool _stopwatchSeeded;
        private static long _graphicsMemoryBudgetBytes;
        private static uint _deviceModelHash;
        private static float _hardwareShiFloor;
        private static float _hardwareMaxQualityWeight;
        private static bool _hardwareLowTierLocked;
        private static bool _mockProfilesGenerated;
        private static bool _lowTierEmergencyActive;
        private static int _emergencyReleaseCounter;
        private static bool _visualOverkillActive;
        private static int _visualOverkillCounter;
        private static bool _forceVisualOverkillOverride;
        private static bool _mathLodLowLeaseActive;
        private static bool _mathLodLowScalarWritten;
        private static float _lastMathLodLowScalar;
        private static bool _mockHeavyLoadActive;
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
        private static int _gcFreezeFramesRemaining;
        private static bool _gcSafeBaseMenuArmed;
        private static uint _lastRegistryKillBits;
        private static bool _scalabilityDumped;
        private static int _lastMockTerrainScheduleFrame;
        private static int _lastMockTerrainQualityBucket;
        private static uint _lastMockTerrainFlags;
        private static float _globalQualityWeight = 1f;
        private static float _fractionalTimeSlice = 1f;
        private static float _targetRenderScale01 = 1f;
        private static float _lastPublishedGlobalQualityWeight = ForcedQualityWeightDisabled;
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
        public static float CullingMultiplier => _cullingMultiplier;

        /// <summary>Continuous scalar: 1.0 means visual overkill, 0.0 means minimum survival.</summary>
        public static float GlobalQualityWeight => _globalQualityWeight;

        /// <summary>Continuous update-budget scalar for time-sliced systems.</summary>
        public static float FractionalTimeSlice => _fractionalTimeSlice;

        /// <summary>Continuous render-scale scalar derived from the global quality weight.</summary>
        public static float TargetRenderScale01 => _targetRenderScale01;

        /// <summary>Probability threshold for deterministic stochastic decimation callers.</summary>
        public static float StochasticDecimationThreshold => math.saturate(_globalQualityWeight);

        /// <summary>
        /// Deterministic probability gate for callers that need smooth work decimation.
        /// </summary>
        public static bool ShouldExecuteStochasticUpdate(uint stableHash)
        {
            float weight = math.saturate(_globalQualityWeight);
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
            CompleteMockTerrainSamplerJobIfReady();
            ValidateScalabilityDtoLayouts(blackBox);
            ResolveHardwareTierLock();
            _graphicsMemoryBudgetBytes = ResolveGraphicsMemoryBudgetBytes();
            _dynamicResolutionRuntime = GlobalRegistry.DynamicResolutionRuntime;
            _lastStopwatchTimestamp = 0L;
            _stopwatchSeeded = false;
            _lowTierEmergencyActive = false;
            _emergencyReleaseCounter = 0;
            _visualOverkillActive = false;
            _visualOverkillCounter = 0;
            _forceVisualOverkillOverride = false;
            _mathLodLowLeaseActive = false;
            _mathLodLowScalarWritten = false;
            _lastMathLodLowScalar = ForcedQualityWeightDisabled;
            _mockHeavyLoadActive = false;
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
            _lastRegistryKillBits = 0u;
            _scalabilityDumped = false;
            _lastMockTerrainScheduleFrame = -MockTerrainSamplerCadenceFrames;
            _lastMockTerrainQualityBucket = -1;
            _lastMockTerrainFlags = 0u;
            _globalQualityWeight = math.clamp(_hardwareMaxQualityWeight, 0f, 1f);
            _fractionalTimeSlice = 1f;
            _targetRenderScale01 = 1f;
            _lastPublishedGlobalQualityWeight = ForcedQualityWeightDisabled;
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
            hardwareMetrics[(int)HardwareMetricSlot.VramPressure01] = 0f;

            TryResolveScalabilityDictatorBuffers(
                out NativeArray<SystemHealthDTO> health,
                out NativeArray<ScalabilityStateDTO> state,
                out NativeArray<MockHeavyLoadSignal> heavyLoad,
                out NativeArray<MockTerrainSamplerStatus> terrainSampler,
                out NativeArray<byte> csvScratch);
            MemClearIfCreated(health);
            MemClearIfCreated(state);
            MemClearIfCreated(heavyLoad);
            MemClearIfCreated(terrainSampler);
            MemClearIfCreated(csvScratch);
            if (TryResolveScalabilityTelemetry(out NativeArray<ScalabilityTelemetryEntry> telemetry))
                MemClearIfCreated(telemetry);
            GenerateEmergencyMockProfiles();
            Shader.SetGlobalFloat(_cullingMultiplierId, 1f);
            PublishQualityShaderGlobals(true);
            SetMathLodLowLease(_hardwareLowTierLocked);
        }

        private static void ShutdownScalabilityDictator()
        {
            if (_mockTerrainSamplerJobPending)
            {
                _mockTerrainSamplerJobHandle.Complete();
                _mockTerrainSamplerJobHandle = default;
                _mockTerrainSamplerJobPending = false;
            }

            if (_gcFrozenByDictator)
            {
                GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
                _gcFrozenByDictator = false;
                _gcFreezeFramesRemaining = 0;
            }

            if (_mathLodLowLeaseActive)
                GlobalRegistry.SetTransientLowScalabilityOverride(GlobalRegistry.TransientScalabilityPlatformPressureMask, false);

            IDynamicResolutionRuntime drsRuntime = _dynamicResolutionRuntime;
            if (drsRuntime != null && drsRuntime.IsSystemOverrideActive)
                drsRuntime.ClearSystemOverrideRenderScale();

            Shader.SetGlobalFloat(_globalQualityWeightId, 1f);
            Shader.SetGlobalFloat(_h8GlobalQualityWeightId, 1f);
            Shader.SetGlobalFloat(_mathLodLowScalarId, 0f);
            _lastMathLodLowScalar = 0f;
            _mathLodLowScalarWritten = true;
            _systemHealthDtoHandle = default;
            _scalabilityStateHandle = default;
            _mockHeavyLoadHandle = default;
            _mockTerrainSamplerStatusHandle = default;
            _scalabilityTelemetryHandle = default;
            _scalabilityTuningHandle = default;
            _csvScratchHandle = default;
            _scalabilityTelemetryCursor = 0;
            _scalabilityTelemetrySampleCount = 0;
            _qualityPidIntegral = 0f;
            _qualityPidPreviousError = 0f;
            _dynamicResolutionRuntime = null;
            _csvProfilePath = null;
            _mockProfilesGenerated = false;
            _lastRegistryKillBits = 0u;
        }

        private static void ResetScalabilityDictatorVaultHandles()
        {
            if (_mockTerrainSamplerJobPending)
            {
                _mockTerrainSamplerJobHandle.Complete();
                _mockTerrainSamplerJobHandle = default;
                _mockTerrainSamplerJobPending = false;
            }

            _systemHealthDtoHandle = default;
            _scalabilityStateHandle = default;
            _mockHeavyLoadHandle = default;
            _mockTerrainSamplerStatusHandle = default;
            _scalabilityTelemetryHandle = default;
            _scalabilityTuningHandle = default;
            _csvScratchHandle = default;
            _scalabilityTelemetryCursor = 0;
            _scalabilityTelemetrySampleCount = 0;
            _qualityPidIntegral = 0f;
            _qualityPidPreviousError = 0f;
            _mockProfilesGenerated = false;
        }

        private static float ResolveTargetFrameMs(float targetFps)
        {
            float configured = math.isfinite(_targetFrameMsOverride) && _targetFrameMsOverride > 0f
                ? _targetFrameMsOverride
                : ScalabilityContract.TargetFrameMilliseconds;
            float fpsFrameMs = 1000f * math.rcp(math.max(1f, targetFps));
            return math.isfinite(configured) && configured > 0f ? configured : fpsFrameMs;
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
                pressure = math.max(pressure, math.saturate(mock.VramPressure01));
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
            bool lowTier,
            NativeArray<float> hardwareMetrics)
        {
            TryPollCsvOverrides(frame);

            float effectiveFrameMs = frameMs;
            float effectiveVramPressure01 = vramPressure01;
            _mockHeavyLoadActive = false;
            if (TryReadMockHeavyLoad(out MockHeavyLoadSignal mock) &&
                (mock.Flags & MockHeavyLoadSignal.FlagEnabled) != 0u)
            {
                effectiveVramPressure01 = math.max(effectiveVramPressure01, math.saturate(mock.VramPressure01));
                _mockHeavyLoadActive = true;
            }

            float frameOverTarget01 = math.saturate((effectiveFrameMs - targetFrameMs) * math.rcp(math.max(0.001f, targetFrameMs)));
            float frameCurve = frameOverTarget01 * frameOverTarget01;
            float vramGuard01 = math.saturate((effectiveVramPressure01 - VramSpikeThreshold) * math.rcp(math.max(0.001f, 1f - VramSpikeThreshold)));
            float vramCurve = vramGuard01 * vramGuard01 * (3f - 2f * vramGuard01);
            float thermal01 = math.saturate((cpuTempC - 55f) * math.rcp(30f));
            float jitter01 = math.saturate(jitterSigmaMs * 0.5f);
            float polynomial = math.saturate(frameCurve * 0.35f + vramCurve * 0.45f + thermal01 * 0.15f + jitter01 * 0.05f);
            float raw = math.max(math.saturate(baseRawShi), polynomial);

            if (effectiveVramPressure01 > VramOomThreshold)
                raw = math.max(raw, math.saturate(0.86f + (effectiveVramPressure01 - VramOomThreshold) * 0.9f));
            if (effectiveFrameMs > CriticalFrameDumpThresholdMs)
                raw = math.max(raw, 0.92f);
            if (lowTier)
                raw = math.max(raw, _hardwareShiFloor);

            hardwareMetrics[(int)HardwareMetricSlot.VramPressure01] = effectiveVramPressure01;

            return math.isfinite(raw) ? math.saturate(raw) : 1f;
        }

        private static float ApplyHardwareShiFloor(float shi)
        {
            float clamped = math.isfinite(shi) ? math.saturate(shi) : 1f;
            return _hardwareShiFloor > 0f ? math.max(clamped, _hardwareShiFloor) : clamped;
        }

        private static ulong ApplyDictatorPressurePolicy(
            int frame,
            float frameMs,
            ulong targetMask,
            ref byte targetLevel,
            ref ushort flags,
            NativeArray<float> hardwareMetrics)
        {
            float vramPressure01 = hardwareMetrics[(int)HardwareMetricSlot.VramPressure01];
            float thermalIndex = math.saturate((hardwareMetrics[(int)HardwareMetricSlot.CpuTempC] - 55f) * math.rcp(30f));
            float emergencyThreshold = math.clamp(_emergencyThresholdOverride, 0.01f, 1f);

            if (_systemHealthIndex01 >= emergencyThreshold)
            {
                _lowTierEmergencyActive = true;
                _emergencyReleaseCounter = 0;
            }
            else if (_lowTierEmergencyActive)
            {
                if (_systemHealthIndex01 < EmergencyReleaseThreshold)
                {
                    if (_emergencyReleaseCounter < int.MaxValue)
                        _emergencyReleaseCounter++;
                }
                else
                {
                    _emergencyReleaseCounter = 0;
                }

                int releaseFrames = math.max(1, _hysteresisReleaseFrames);
                if (_emergencyReleaseCounter >= releaseFrames)
                {
                    _lowTierEmergencyActive = false;
                    _emergencyReleaseCounter = 0;
                }
            }

            bool mathLodLow = _systemHealthIndex01 > MathLodLowThreshold ||
                              vramPressure01 > VramOomThreshold ||
                              _lowTierEmergencyActive ||
                              _hardwareLowTierLocked;
            if (_lowTierEmergencyActive)
            {
                targetMask |= Level3Mask | (ulong)SystemBit.LowTierEmergency;
                if (targetLevel < 3)
                    targetLevel = 3;
                flags |= (ushort)(HomeostasisSignalFlags.Emergency | HomeostasisSignalFlags.HudWarning);
                _stableRecoveryFrames = 0;
                _recoveryStepFrameCounter = 0;
                _restorationIndex = 0;
            }
            else
            {
                targetMask &= ~(ulong)SystemBit.LowTierEmergency;
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

            if (vramPressure01 > VramOomThreshold)
            {
                targetMask |= (ulong)(SystemBit.VramShedding | SystemBit.NonCriticalVfx);
                if (targetLevel < 2)
                    targetLevel = 2;
            }
            else
            {
                targetMask &= ~(ulong)SystemBit.VramShedding;
            }

            bool squeezeCulling = _systemHealthIndex01 > MathLodLowThreshold || _lowTierEmergencyActive;
            if (squeezeCulling)
                targetMask |= (ulong)SystemBit.CullingDistanceSqueeze;
            else if (_systemHealthIndex01 < EmergencyReleaseThreshold)
                targetMask &= ~(ulong)SystemBit.CullingDistanceSqueeze;

            if (_mockHeavyLoadActive)
                targetMask |= (ulong)SystemBit.MockHeavyLoad;
            else
                targetMask &= ~(ulong)SystemBit.MockHeavyLoad;

            if (_systemHealthIndex01 > 0.95f)
            {
                targetMask |= (ulong)SystemBit.AiOneHz;
                if (targetLevel < 3)
                    targetLevel = 3;
            }

            ApplyVisualOverkillPolicy(ref targetMask);
            ApplyGarbageCollectorPolicy(frameMs, ref targetMask);
            SetMathLodLowLease(mathLodLow);
            UpdateCullingMultiplier(math.lerp(1f, _lowCullingMultiplier, ResolveMathLodLowWeight()));
            UpdateRegistryKillMask(targetMask);
            WriteDictatorState(frameMs, vramPressure01, thermalIndex, targetMask);
            ScheduleMockTerrainSamplerJob(frame, targetMask);

            bool survivalFailure = frameMs > ScalabilityHardFailFrameMs && _globalQualityWeight <= 0.0001f;
            bool emergencyFailure = frameMs > CriticalFrameDumpThresholdMs &&
                                    (targetMask & (ulong)SystemBit.LowTierEmergency) != 0UL;
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

        private static void ApplyVisualOverkillPolicy(ref ulong targetMask)
        {
            if (_hardwareLowTierLocked)
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
            else if (_systemHealthIndex01 < VisualOverkillEnableThreshold)
            {
                if (_visualOverkillCounter < int.MaxValue)
                    _visualOverkillCounter++;
                if (_visualOverkillCounter >= DefaultVisualOverkillFrames)
                    _visualOverkillActive = true;
            }
            else if (_systemHealthIndex01 > VisualOverkillRevokeThreshold)
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
            if ((gen0Spike || heapSpike) && _systemHealthIndex01 > MathLodLowThreshold)
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

                bool safeBaseMenu = _gcSafeBaseMenuArmed &&
                                    _systemHealthIndex01 < 0.35f &&
                                    frameMs < ResolveTargetFrameMs(ResolveTargetFrameRate());
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

        private static void SetMathLodLowLease(bool enabled)
        {
            if (_mathLodLowLeaseActive != enabled)
            {
                _mathLodLowLeaseActive = enabled;
                GlobalRegistry.SetTransientLowScalabilityOverride(
                    GlobalRegistry.TransientScalabilityPlatformPressureMask,
                    enabled);
                RefreshMathLodLowScalar();
                return;
            }

            if (!_mathLodLowScalarWritten)
                RefreshMathLodLowScalar();
        }

        private static float ResolveMathLodLowWeight()
        {
            float qualityPressure = math.saturate(
                (MathLodLowThreshold - _globalQualityWeight) *
                math.rcp(math.max(0.0001f, MathLodLowThreshold - MathLodFullQualityFloor)));
            qualityPressure = qualityPressure * qualityPressure * (3f - 2f * qualityPressure);

            float healthPressure = math.saturate(
                (_systemHealthIndex01 - MathLodHealthSoftStart) *
                math.rcp(math.max(0.0001f, MathLodLowThreshold - MathLodHealthSoftStart)));
            healthPressure = healthPressure * healthPressure * (3f - 2f * healthPressure);

            float survivalFloor = 1f - math.step(MathLodSurvivalStep, _globalQualityWeight);
            return math.saturate(math.max(math.max(qualityPressure, healthPressure), survivalFloor));
        }

        private static void RefreshMathLodLowScalar()
        {
            float lowWeight = ResolveMathLodLowWeight();
            if (_mathLodLowScalarWritten && math.abs(_lastMathLodLowScalar - lowWeight) < QualityShaderEpsilon)
                return;

            Shader.SetGlobalFloat(_mathLodLowScalarId, lowWeight);
            _lastMathLodLowScalar = lowWeight;
            _mathLodLowScalarWritten = true;
        }

        private static void UpdateCullingMultiplier(float multiplier)
        {
            float safeMultiplier = math.clamp(multiplier, 0.4f, 1f);
            if (math.abs(_cullingMultiplier - safeMultiplier) < 0.001f)
                return;

            _cullingMultiplier = safeMultiplier;
            Shader.SetGlobalFloat(_cullingMultiplierId, safeMultiplier);
        }

        private static void UpdateGlobalQualityState(float frameMs, float vramPressure01, float thermalIndex)
        {
            float targetFrameMs = ResolveTargetFrameMs(ResolveTargetFrameRate());
            float safeFrameMs = math.isfinite(frameMs) && frameMs > 0f ? frameMs : targetFrameMs;
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
            float stress = math.max(math.saturate(_systemHealthIndex01), math.max(math.max(vramPressure01, thermalIndex), pidStress));
            float desired = math.saturate(1f - stress);
            desired = math.min(desired, math.clamp(_hardwareMaxQualityWeight, 0f, 1f));
            if (_forceGlobalQualityWeightOverride)
                desired = math.min(math.saturate(_forcedGlobalQualityWeight), math.clamp(_hardwareMaxQualityWeight, 0f, 1f));

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

            _globalQualityWeight = math.saturate(_globalQualityWeight);
            _fractionalTimeSlice = math.lerp(MinimumFractionalTimeSlice, 1f, _globalQualityWeight);
            _targetRenderScale01 = math.lerp(MinimumRenderScale01, 1f, _globalQualityWeight);
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
            if (!force && math.abs(_lastPublishedGlobalQualityWeight - _globalQualityWeight) < QualityShaderEpsilon)
                return;

            Shader.SetGlobalFloat(_globalQualityWeightId, _globalQualityWeight);
            Shader.SetGlobalFloat(_h8GlobalQualityWeightId, _globalQualityWeight);
            _lastPublishedGlobalQualityWeight = _globalQualityWeight;
        }

        private static void UpdateRegistryKillMask(ulong targetMask)
        {
            uint currentBits = FoldMaskToUInt(targetMask);
            uint clearBits = _lastRegistryKillBits & ~currentBits;
            uint setBits = currentBits & ~_lastRegistryKillBits;
            if (clearBits != 0u)
                GlobalRegistry.SetSystemKillSwitchBits(clearBits, false);
            if (setBits != 0u)
                GlobalRegistry.SetSystemKillSwitchBits(setBits, true);
            _lastRegistryKillBits = currentBits;
        }

        private static void WriteDictatorState(
            float frameMs,
            float vramPressure01,
            float thermalIndex,
            ulong activeMask)
        {
            IDataVault vault = _dataVault;
            if (!EnsureScalabilityStateHandles(vault))
            {
                return;
            }

            uint foldedMask = FoldMaskToUInt(activeMask);
            ref SystemHealthDTO health = ref _systemHealthDtoHandle.GetElementAsRef(vault, 0);
            health.FrameTimeMs = math.isfinite(frameMs) && frameMs > 0f ? frameMs : ResolveTargetFrameMs(ResolveTargetFrameRate());
            health.VramPressure = math.isfinite(vramPressure01) ? math.saturate(vramPressure01) : 1f;
            health.ThermalIndex = math.isfinite(thermalIndex) ? math.saturate(thermalIndex) : 1f;
            health.ActiveThrottlesMask = foldedMask;

            UpdateGlobalQualityState(frameMs, health.VramPressure, health.ThermalIndex);
            ref ScalabilityStateDTO state = ref _scalabilityStateHandle.GetElementAsRef(vault, 0);
            state.GlobalQualityWeight = _globalQualityWeight;
            state.FractionalTimeSlice = _fractionalTimeSlice;
            state.VramPressure = health.VramPressure;
            state.ThermalIndex = health.ThermalIndex;
            RecordScalabilityTelemetry(health.FrameTimeMs, health.VramPressure, foldedMask);
            RefreshMathLodLowScalar();
            ApplyDictatorRenderScale(frameMs, health.ThermalIndex);
            PublishQualityShaderGlobals(false);
        }

        private static void GenerateEmergencyMockProfiles()
        {
            if (_mockProfilesGenerated)
                return;

            IDataVault vault = _dataVault;
            if (vault == null || !TryResolveScalabilityDictatorBuffers(
                    out _,
                    out _,
                    out _,
                    out _,
                    out _))
            {
                return;
            }

            float weight = math.saturate(_globalQualityWeight);
            ref ScalabilityStateDTO state = ref _scalabilityStateHandle.GetElementAsRef(vault, 0);
            state.GlobalQualityWeight = weight;
            state.FractionalTimeSlice = math.lerp(MinimumFractionalTimeSlice, 1f, weight);
            state.VramPressure = 0f;
            state.ThermalIndex = 0f;

            ref MockHeavyLoadSignal heavy = ref _mockHeavyLoadHandle.GetElementAsRef(vault, 0);
            heavy.FrameSpikeMs = DefaultMockFrameSpikeMs;
            heavy.VramPressure01 = 0f;
            heavy.Flags = 0u;
            heavy._pad0 = 0u;

            ref MockTerrainSamplerStatus terrain = ref _mockTerrainSamplerStatusHandle.GetElementAsRef(vault, 0);
            terrain.GlobalQualityWeight = weight;
            terrain.TrilinearSampleProbability01 = weight;
            terrain.SkippedTrilinearPercent01 = 1f - weight;
            terrain.Frame = 0u;
            _mockProfilesGenerated = true;
        }

        private static bool TryReadMockHeavyLoad(out MockHeavyLoadSignal signal)
        {
            signal = default;
            IDataVault vault = _dataVault;
            if (!EnsureMockHeavyLoadHandle(vault))
                return false;

            signal = _mockHeavyLoadHandle.GetElementAsRef(vault, 0);
            return true;
        }

        private static bool EnsureScalabilityStateHandles(IDataVault vault)
        {
            if (vault == null)
                return false;

            bool healthCreated = false;
            bool stateCreated = false;
            if (!_systemHealthDtoHandle.IsCreated || !vault.ResolveBuffer(ref _systemHealthDtoHandle))
            {
                _systemHealthDtoHandle = vault.GetBufferHandle<SystemHealthDTO>(
                    BufferID.ShinobuScalabilitySystemHealth,
                    DictatorSingletonLength,
                    SystemID.HardwareHomeostasis,
                    NativeArrayOptions.UninitializedMemory);
                healthCreated = true;
            }

            if (!_scalabilityStateHandle.IsCreated || !vault.ResolveBuffer(ref _scalabilityStateHandle))
            {
                _scalabilityStateHandle = vault.GetBufferHandle<ScalabilityStateDTO>(
                    BufferID.ShinobuScalabilityState,
                    DictatorSingletonLength,
                    SystemID.HardwareHomeostasis,
                    NativeArrayOptions.UninitializedMemory);
                stateCreated = true;
            }

            if (healthCreated)
            {
                NativeArray<SystemHealthDTO> health = _systemHealthDtoHandle.Resolve(vault);
                MemClearIfCreated(health);
            }

            if (stateCreated)
            {
                NativeArray<ScalabilityStateDTO> state = _scalabilityStateHandle.Resolve(vault);
                MemClearIfCreated(state);
            }

            return _systemHealthDtoHandle.IsCreated &&
                   _systemHealthDtoHandle.Length >= DictatorSingletonLength &&
                   _scalabilityStateHandle.IsCreated &&
                   _scalabilityStateHandle.Length >= DictatorSingletonLength;
        }

        private static bool EnsureScalabilityTuningHandle(IDataVault vault)
        {
            if (vault == null)
                return false;

            bool created = false;
            if (!_scalabilityTuningHandle.IsCreated || !vault.ResolveBuffer(ref _scalabilityTuningHandle))
            {
                _scalabilityTuningHandle = vault.GetBufferHandle<ScalabilityTuningDTO>(
                    BufferID.ShinobuScalabilityTunerState,
                    DictatorSingletonLength,
                    SystemID.HardwareHomeostasis,
                    NativeArrayOptions.UninitializedMemory);
                created = true;
            }

            if (created)
            {
                NativeArray<ScalabilityTuningDTO> tuningArray = _scalabilityTuningHandle.Resolve(vault);
                MemClearIfCreated(tuningArray);
                ref ScalabilityTuningDTO tuning = ref _scalabilityTuningHandle.GetElementAsRef(vault, 0);
                tuning.TargetFrameMs = math.clamp(_targetFrameMsOverride, 4f, 50f);
                tuning.EmergencyThreshold = math.clamp(_emergencyThresholdOverride, 0.1f, 1f);
                tuning.HysteresisReleaseFrames = math.clamp(_hysteresisReleaseFrames, 1, 3600);
                tuning.Flags = 0u;
            }

            return _scalabilityTuningHandle.IsCreated &&
                   _scalabilityTuningHandle.Length >= DictatorSingletonLength;
        }

        private static void WriteCurrentTuningStateToVault(IDataVault vault)
        {
            if (!EnsureScalabilityTuningHandle(vault))
                return;

            ref ScalabilityTuningDTO tuning = ref _scalabilityTuningHandle.GetElementAsRef(vault, 0);
            tuning.TargetFrameMs = math.clamp(_targetFrameMsOverride, 4f, 50f);
            tuning.EmergencyThreshold = math.clamp(_emergencyThresholdOverride, 0.1f, 1f);
            tuning.HysteresisReleaseFrames = math.clamp(_hysteresisReleaseFrames, 1, 3600);
            tuning.Flags = 0u;
        }

        private static bool EnsureMockHeavyLoadHandle(IDataVault vault)
        {
            if (vault == null)
                return false;

            bool created = false;
            if (!_mockHeavyLoadHandle.IsCreated || !vault.ResolveBuffer(ref _mockHeavyLoadHandle))
            {
                _mockHeavyLoadHandle = vault.GetBufferHandle<MockHeavyLoadSignal>(
                    BufferID.ShinobuScalabilityMockHeavyLoad,
                    DictatorSingletonLength,
                    SystemID.HardwareHomeostasis,
                    NativeArrayOptions.UninitializedMemory);
                created = true;
            }

            if (created)
            {
                NativeArray<MockHeavyLoadSignal> signalArray = _mockHeavyLoadHandle.Resolve(vault);
                MemClearIfCreated(signalArray);
            }

            return _mockHeavyLoadHandle.IsCreated &&
                   _mockHeavyLoadHandle.Length >= DictatorSingletonLength;
        }

        private static bool EnsureMockTerrainSamplerStatusHandle(IDataVault vault)
        {
            if (vault == null)
                return false;

            bool created = false;
            if (!_mockTerrainSamplerStatusHandle.IsCreated || !vault.ResolveBuffer(ref _mockTerrainSamplerStatusHandle))
            {
                _mockTerrainSamplerStatusHandle = vault.GetBufferHandle<MockTerrainSamplerStatus>(
                    BufferID.ShinobuScalabilityMockScatterDensity,
                    DictatorSingletonLength,
                    SystemID.HardwareHomeostasis,
                    NativeArrayOptions.UninitializedMemory);
                created = true;
            }

            if (created)
            {
                NativeArray<MockTerrainSamplerStatus> terrainSampler = _mockTerrainSamplerStatusHandle.Resolve(vault);
                MemClearIfCreated(terrainSampler);
            }

            return _mockTerrainSamplerStatusHandle.IsCreated &&
                   _mockTerrainSamplerStatusHandle.Length >= DictatorSingletonLength;
        }

        private static bool TryResolveMockTerrainSamplerStatus(out NativeArray<MockTerrainSamplerStatus> terrainSampler)
        {
            terrainSampler = default;
            IDataVault vault = _dataVault;
            if (!EnsureMockTerrainSamplerStatusHandle(vault))
                return false;

            terrainSampler = _mockTerrainSamplerStatusHandle.Resolve(vault);
            return terrainSampler.IsCreated && terrainSampler.Length >= DictatorSingletonLength;
        }

        private static bool TryResolveCsvScratch(out NativeArray<byte> csvScratch)
        {
            csvScratch = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            bool created = false;
            if (!_csvScratchHandle.IsCreated || !vault.ResolveBuffer(ref _csvScratchHandle))
            {
                _csvScratchHandle = vault.GetBufferHandle<byte>(
                    BufferID.ShinobuScalabilityCsvScratch,
                    ScalabilityCsvScratchBytes,
                    SystemID.HardwareHomeostasis,
                    NativeArrayOptions.UninitializedMemory);
                created = true;
            }

            csvScratch = _csvScratchHandle.Resolve(vault);
            if (created)
                MemClearIfCreated(csvScratch);

            return csvScratch.IsCreated && csvScratch.Length >= ScalabilityCsvScratchBytes;
        }

        private static bool TryResolveScalabilityDictatorBuffers(
            out NativeArray<SystemHealthDTO> health,
            out NativeArray<ScalabilityStateDTO> state,
            out NativeArray<MockHeavyLoadSignal> heavyLoad,
            out NativeArray<MockTerrainSamplerStatus> terrainSampler,
            out NativeArray<byte> csvScratch)
        {
            health = default;
            state = default;
            heavyLoad = default;
            terrainSampler = default;
            csvScratch = default;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            bool healthCreated = false;
            bool stateCreated = false;
            bool heavyLoadCreated = false;
            bool terrainCreated = false;
            bool csvScratchCreated = false;
            if (!_systemHealthDtoHandle.IsCreated || !vault.ResolveBuffer(ref _systemHealthDtoHandle))
            {
                _systemHealthDtoHandle = vault.GetBufferHandle<SystemHealthDTO>(
                    BufferID.ShinobuScalabilitySystemHealth,
                    DictatorSingletonLength,
                    SystemID.HardwareHomeostasis,
                    NativeArrayOptions.UninitializedMemory);
                healthCreated = true;
            }

            if (!_scalabilityStateHandle.IsCreated || !vault.ResolveBuffer(ref _scalabilityStateHandle))
            {
                _scalabilityStateHandle = vault.GetBufferHandle<ScalabilityStateDTO>(
                    BufferID.ShinobuScalabilityState,
                    DictatorSingletonLength,
                    SystemID.HardwareHomeostasis,
                    NativeArrayOptions.UninitializedMemory);
                stateCreated = true;
            }

            if (!_mockHeavyLoadHandle.IsCreated || !vault.ResolveBuffer(ref _mockHeavyLoadHandle))
            {
                _mockHeavyLoadHandle = vault.GetBufferHandle<MockHeavyLoadSignal>(
                    BufferID.ShinobuScalabilityMockHeavyLoad,
                    DictatorSingletonLength,
                    SystemID.HardwareHomeostasis,
                    NativeArrayOptions.UninitializedMemory);
                heavyLoadCreated = true;
            }

            if (!_mockTerrainSamplerStatusHandle.IsCreated || !vault.ResolveBuffer(ref _mockTerrainSamplerStatusHandle))
            {
                _mockTerrainSamplerStatusHandle = vault.GetBufferHandle<MockTerrainSamplerStatus>(
                    BufferID.ShinobuScalabilityMockScatterDensity,
                    DictatorSingletonLength,
                    SystemID.HardwareHomeostasis,
                    NativeArrayOptions.UninitializedMemory);
                terrainCreated = true;
            }

            if (!_csvScratchHandle.IsCreated || !vault.ResolveBuffer(ref _csvScratchHandle))
            {
                _csvScratchHandle = vault.GetBufferHandle<byte>(
                    BufferID.ShinobuScalabilityCsvScratch,
                    ScalabilityCsvScratchBytes,
                    SystemID.HardwareHomeostasis,
                    NativeArrayOptions.UninitializedMemory);
                csvScratchCreated = true;
            }

            health = _systemHealthDtoHandle.Resolve(vault);
            state = _scalabilityStateHandle.Resolve(vault);
            heavyLoad = _mockHeavyLoadHandle.Resolve(vault);
            terrainSampler = _mockTerrainSamplerStatusHandle.Resolve(vault);
            csvScratch = _csvScratchHandle.Resolve(vault);
            if (healthCreated)
                MemClearIfCreated(health);
            if (stateCreated)
                MemClearIfCreated(state);
            if (heavyLoadCreated)
                MemClearIfCreated(heavyLoad);
            if (terrainCreated)
                MemClearIfCreated(terrainSampler);
            if (csvScratchCreated)
                MemClearIfCreated(csvScratch);
            return health.IsCreated &&
                   health.Length >= DictatorSingletonLength &&
                   state.IsCreated &&
                   state.Length >= DictatorSingletonLength &&
                   heavyLoad.IsCreated &&
                   heavyLoad.Length >= DictatorSingletonLength &&
                   terrainSampler.IsCreated &&
                   terrainSampler.Length >= DictatorSingletonLength &&
                   csvScratch.IsCreated &&
                   csvScratch.Length >= ScalabilityCsvScratchBytes;
        }

        private static bool EnsureScalabilityTelemetryHandle(IDataVault vault)
        {
            if (vault == null)
                return false;

            bool created = false;
            if (!_scalabilityTelemetryHandle.IsCreated || !vault.ResolveBuffer(ref _scalabilityTelemetryHandle))
            {
                _scalabilityTelemetryHandle = vault.GetBufferHandle<ScalabilityTelemetryEntry>(
                    BufferID.ShinobuScalabilityOscilloscope,
                    ScalabilityTelemetryCapacity,
                    SystemID.HardwareHomeostasis,
                    NativeArrayOptions.UninitializedMemory);
                created = true;
            }

            if (created)
            {
                NativeArray<ScalabilityTelemetryEntry> telemetry = _scalabilityTelemetryHandle.Resolve(vault);
                MemClearIfCreated(telemetry);
                _scalabilityTelemetryCursor = 0;
                _scalabilityTelemetrySampleCount = 0;
            }

            return _scalabilityTelemetryHandle.IsCreated &&
                   _scalabilityTelemetryHandle.Length >= ScalabilityTelemetryCapacity;
        }

        private static bool TryResolveScalabilityTelemetry(out NativeArray<ScalabilityTelemetryEntry> telemetry)
        {
            telemetry = default;
            IDataVault vault = _dataVault;
            if (!EnsureScalabilityTelemetryHandle(vault))
                return false;

            telemetry = _scalabilityTelemetryHandle.Resolve(vault);
            return telemetry.IsCreated && telemetry.Length >= ScalabilityTelemetryCapacity;
        }

        private static void RecordScalabilityTelemetry(float rawFrameMs, float vramPressure01, uint flags)
        {
            IDataVault vault = _dataVault;
            if (!EnsureScalabilityTelemetryHandle(vault))
                return;

            int index = _scalabilityTelemetryCursor;
            if ((uint)index >= ScalabilityTelemetryCapacity)
                index = 0;

            float safeRawFrameMs = math.isfinite(rawFrameMs) && rawFrameMs > 0f
                ? rawFrameMs
                : ResolveTargetFrameMs(ResolveTargetFrameRate());
            float smoothedFrameMs = _fpsEwma > 0f
                ? 1000f * math.rcp(math.max(1f, _fpsEwma))
                : safeRawFrameMs;
            ref ScalabilityTelemetryEntry entry = ref _scalabilityTelemetryHandle.GetElementAsRef(vault, index);
            entry.Timestamp = unchecked((ulong)_lastStopwatchTimestamp);
            entry.RawFrameMs = safeRawFrameMs;
            entry.SmoothedFrameMs = math.isfinite(smoothedFrameMs) && smoothedFrameMs > 0f ? smoothedFrameMs : safeRawFrameMs;
            entry.GlobalQualityWeight = math.saturate(_globalQualityWeight);
            entry.VramPressure = math.saturate(vramPressure01);
            entry.Flags = flags;
            entry._pad0 = 0u;

            index++;
            _scalabilityTelemetryCursor = index >= ScalabilityTelemetryCapacity ? 0 : index;
            if (_scalabilityTelemetrySampleCount < ScalabilityTelemetryCapacity)
                _scalabilityTelemetrySampleCount++;
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
                   ScalabilityCsvScratchBytes;
        }

        private static void ScheduleMockTerrainSamplerJob(int frame, ulong targetMask)
        {
            CompleteMockTerrainSamplerJobIfReady();
            if (_mockTerrainSamplerJobPending)
                return;

            uint foldedFlags = FoldMaskToUInt(targetMask);
            int qualityBucket = (int)math.round(math.saturate(_globalQualityWeight) * 100f);
            int framesSinceLast = frame - _lastMockTerrainScheduleFrame;
            bool cadenceDue = framesSinceLast < 0 || framesSinceLast >= MockTerrainSamplerCadenceFrames;
            if (!cadenceDue &&
                foldedFlags == _lastMockTerrainFlags &&
                qualityBucket == _lastMockTerrainQualityBucket)
            {
                return;
            }

            if (!TryResolveMockTerrainSamplerStatus(out NativeArray<MockTerrainSamplerStatus> terrainSampler))
            {
                return;
            }

            MockTerrainSamplerStatusJob job = default;
            job.Signal = terrainSampler;
            job.GlobalQualityWeight = _globalQualityWeight;
            job.Frame = unchecked((uint)frame);
#if UNITY_EDITOR
            job.Run();
            _mockTerrainSamplerJobHandle = default;
            _mockTerrainSamplerJobPending = false;
#else
            _mockTerrainSamplerJobHandle = job.Schedule();
            H8Memory.RegisterActiveJob(SystemID.HardwareHomeostasis, _mockTerrainSamplerJobHandle);
            _mockTerrainSamplerJobPending = true;
#endif
            _lastMockTerrainScheduleFrame = frame;
            _lastMockTerrainQualityBucket = qualityBucket;
            _lastMockTerrainFlags = foldedFlags;
        }

        private static void CompleteMockTerrainSamplerJobIfReady()
        {
            if (!_mockTerrainSamplerJobPending || !_mockTerrainSamplerJobHandle.IsCompleted)
                return;

            _mockTerrainSamplerJobHandle.Complete();
            _mockTerrainSamplerJobHandle = default;
            _mockTerrainSamplerJobPending = false;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct MockTerrainSamplerStatusJob : IJob
        {
            [NoAlias] public NativeArray<MockTerrainSamplerStatus> Signal;
            public float GlobalQualityWeight;
            public uint Frame;

            public void Execute()
            {
                float weight = math.saturate(GlobalQualityWeight);
                MockTerrainSamplerStatus status = default;
                status.GlobalQualityWeight = weight;
                status.TrilinearSampleProbability01 = weight;
                status.SkippedTrilinearPercent01 = 1f - weight;
                status.Frame = Frame;
                Signal[0] = status;
            }
        }

        private static void ResolveHardwareTierLock()
        {
            string model = SystemInfo.deviceModel;
            string gpuName = SystemInfo.graphicsDeviceName;
            _deviceModelHash = HashOrdinalIgnoreCase(model);
            bool knownLowEnd =
                ContainsOrdinalIgnoreCase(model, "quest 2") ||
                ContainsOrdinalIgnoreCase(model, "quest 3") ||
                ContainsOrdinalIgnoreCase(model, "steam deck") ||
                ContainsOrdinalIgnoreCase(gpuName, "mx350");
            bool memoryLowEnd = SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize <= 8192;
            bool vramLowEnd = SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize <= 2048;
            _hardwareLowTierLocked = knownLowEnd || memoryLowEnd || vramLowEnd;
            _hardwareShiFloor = _hardwareLowTierLocked ? LowHardwareShiFloor : 0f;
            _hardwareMaxQualityWeight = _hardwareLowTierLocked ? LowHardwareMaxQualityWeight : 1f;
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
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string rootProfile = Path.Combine(projectRoot, ScalabilityCsvFileName);
            if (File.Exists(rootProfile))
                return rootProfile;

            return Path.Combine(projectRoot, "Assets", "_Project", "Data", ScalabilityCsvFileName);
        }

        private static void TryPollCsvOverrides(int frame)
        {
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
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
            if (!TryResolveCsvScratch(out NativeArray<byte> csvScratch))
            {
                return;
            }

            int bytesRead = TryReadCsvFile(path, csvScratch);
            if (bytesRead > 0)
                ParseScalabilityCsv(csvScratch, bytesRead, frame);
#endif
        }

        private static unsafe int TryReadCsvFile(string path, NativeArray<byte> csvScratch)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int maxBytes = math.min(csvScratch.Length, ScalabilityCsvScratchBytes);
                    void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(csvScratch);
                    Span<byte> span = new Span<byte>(ptr, maxBytes);
                    return stream.Read(span);
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

        private static void ParseScalabilityCsv(NativeArray<byte> bytes, int length, int frame)
        {
            int cursor = 0;
            while (TryReadCsvLine(bytes, length, ref cursor, out int lineStart, out int lineEnd))
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
            NativeArray<byte> bytes,
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
                _lowCullingMultiplier = math.clamp(culling, 0.4f, 1f);
                return;
            }

            if (EqualsAscii(bytes, keyStart, keyEnd, "gc_safe_menu") &&
                TryParseIntAscii(bytes, valueStart, valueEnd, out int safeMenu))
            {
                _gcSafeBaseMenuArmed = safeMenu != 0;
            }
        }

        private static bool TryReadCsvLine(
            NativeArray<byte> bytes,
            int length,
            ref int cursor,
            out int lineStart,
            out int lineEnd)
        {
            lineStart = cursor;
            lineEnd = cursor;
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

        private static void TrimAscii(NativeArray<byte> bytes, ref int start, ref int end)
        {
            while (start < end && IsAsciiWhitespace(bytes[start]))
                start++;
            while (end > start && IsAsciiWhitespace(bytes[end - 1]))
                end--;
        }

        private static int FindSeparator(NativeArray<byte> bytes, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                byte c = bytes[i];
                if (c == (byte)',' || c == (byte)'=' || c == (byte)';')
                    return i;
            }

            return -1;
        }

        private static bool EqualsAscii(NativeArray<byte> bytes, int start, int end, string expected)
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

        private static bool TryParseIntAscii(NativeArray<byte> bytes, int start, int end, out int value)
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

        private static bool TryParseFloatAscii(NativeArray<byte> bytes, int start, int end, out float value)
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

        private static void ValidateScalabilityDtoLayouts(NativeArray<HomeostasisBlackBoxEntry> blackBox)
        {
            if (UnsafeUtility.SizeOf<SystemHealthDTO>() == 16 &&
                UnsafeUtility.SizeOf<ScalabilityStateDTO>() == 16 &&
                UnsafeUtility.SizeOf<MockHeavyLoadSignal>() == 16 &&
                UnsafeUtility.SizeOf<MockTerrainSamplerStatus>() == 16 &&
                UnsafeUtility.SizeOf<ScalabilityTelemetryEntry>() == 32 &&
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
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                string directory = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(directory);
                if (TryResolveScalabilityTelemetry(out NativeArray<ScalabilityTelemetryEntry> telemetry))
                {
                    WriteScalabilityTelemetryFile(Path.Combine(directory, ScalabilityDumpFileName), telemetry);
                    WriteScalabilityTelemetryFile(Path.Combine(directory, ScalabilityH8DumpFileName), telemetry);
                }
                else
                {
                    WriteScalabilityDictatorBlackBoxFile(Path.Combine(directory, ScalabilityDumpFileName), blackBox);
                    WriteScalabilityDictatorBlackBoxFile(Path.Combine(directory, ScalabilityH8DumpFileName), blackBox);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void WriteScalabilityTelemetryFile(
            string path,
            NativeArray<ScalabilityTelemetryEntry> telemetry)
        {
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                Span<byte> header = stackalloc byte[20];
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(0, 4), 0x53434454u);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(4, 4), 2);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(8, 4), ScalabilityTelemetryCapacity);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(12, 4), _scalabilityTelemetryCursor);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(16, 4), 32);
                stream.Write(header);

                Span<byte> entryBytes = stackalloc byte[32];
                for (int i = 0; i < ScalabilityTelemetryCapacity; i++)
                {
                    int index = _scalabilityTelemetryCursor + i;
                    if (index >= ScalabilityTelemetryCapacity)
                        index -= ScalabilityTelemetryCapacity;

                    ScalabilityTelemetryEntry entry = telemetry[index];
                    entryBytes.Clear();
                    BinaryPrimitives.WriteUInt64LittleEndian(entryBytes.Slice(0, 8), entry.Timestamp);
                    WriteFloatLittleEndian(entryBytes.Slice(8, 4), entry.RawFrameMs);
                    WriteFloatLittleEndian(entryBytes.Slice(12, 4), entry.SmoothedFrameMs);
                    WriteFloatLittleEndian(entryBytes.Slice(16, 4), entry.GlobalQualityWeight);
                    WriteFloatLittleEndian(entryBytes.Slice(20, 4), entry.VramPressure);
                    BinaryPrimitives.WriteUInt32LittleEndian(entryBytes.Slice(24, 4), entry.Flags);
                    BinaryPrimitives.WriteUInt32LittleEndian(entryBytes.Slice(28, 4), entry._pad0);
                    stream.Write(entryBytes);
                }
            }
        }

        private static void WriteScalabilityDictatorBlackBoxFile(
            string path,
            NativeArray<HomeostasisBlackBoxEntry> blackBox)
        {
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                Span<byte> header = stackalloc byte[20];
                BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(0, 4), 0x53484944u);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(4, 4), 1);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(8, 4), BlackBoxCapacity);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(12, 4), _blackBoxCursor);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(16, 4), 64);
                stream.Write(header);

                Span<byte> entryBytes = stackalloc byte[64];
                for (int i = 0; i < BlackBoxCapacity; i++)
                {
                    int index = _blackBoxCursor + i;
                    if (index >= BlackBoxCapacity)
                        index -= BlackBoxCapacity;

                    HomeostasisBlackBoxEntry entry = blackBox[index];
                    float qualityWeight = math.asfloat(entry.Reserved2);
                    if (!math.isfinite(qualityWeight))
                        qualityWeight = math.saturate(1f - entry.SystemHealthIndex01);
                    entryBytes.Clear();
                    BinaryPrimitives.WriteUInt32LittleEndian(entryBytes.Slice(0, 4), entry.Frame);
                    WriteFloatLittleEndian(entryBytes.Slice(4, 4), qualityWeight);
                    BinaryPrimitives.WriteUInt64LittleEndian(entryBytes.Slice(8, 8), entry.KillSwitchMask);
                    WriteFloatLittleEndian(entryBytes.Slice(16, 4), entry.FpsEwma);
                    WriteFloatLittleEndian(entryBytes.Slice(20, 4), entry.JitterSigmaMs);
                    WriteFloatLittleEndian(entryBytes.Slice(24, 4), entry.CpuTempC);
                    WriteFloatLittleEndian(entryBytes.Slice(28, 4), entry.GpuUtil01);
                    WriteFloatLittleEndian(entryBytes.Slice(32, 4), entry.BatteryLife01);
                    entryBytes[36] = entry.PressureLevel;
                    entryBytes[37] = entry.FoveatedPressureTier;
                    BinaryPrimitives.WriteUInt16LittleEndian(entryBytes.Slice(38, 2), entry.Flags);
                    WriteFloatLittleEndian(entryBytes.Slice(40, 4), entry.TimeDilationScalar);
                    WriteFloatLittleEndian(entryBytes.Slice(44, 4), entry.PeakSystemHealthIndex01);
                    BinaryPrimitives.WriteUInt32LittleEndian(entryBytes.Slice(48, 4), entry.LastThermalAction);
                    WriteFloatLittleEndian(entryBytes.Slice(52, 4), math.asfloat(entry.Reserved0));
                    WriteFloatLittleEndian(entryBytes.Slice(56, 4), math.asfloat(entry.Reserved1));
                    BinaryPrimitives.WriteUInt32LittleEndian(entryBytes.Slice(60, 4), entry.Reserved2);
                    stream.Write(entryBytes);
                }
            }
        }

        /// <summary>
        /// Editor/test facade for the Hardware Dictator Tuner.
        /// </summary>
        public static void ApplyHardwareDictatorTuner(float targetFrameMs, float emergencyThreshold, int hysteresisFrames)
        {
            _targetFrameMsOverride = math.clamp(targetFrameMs, 4f, 50f);
            _emergencyThresholdOverride = math.clamp(emergencyThreshold, 0.1f, 1f);
            _hysteresisReleaseFrames = math.clamp(hysteresisFrames, 1, 3600);
            WriteCurrentTuningStateToVault(_dataVault);

            float frameMs = 0f;
            float vramPressure01 = 0f;
            float thermalIndex = 0f;
            IDataVault vault = _dataVault;
            if (EnsureScalabilityStateHandles(vault))
            {
                ref SystemHealthDTO health = ref _systemHealthDtoHandle.GetElementAsRef(vault, 0);
                frameMs = health.FrameTimeMs;
                vramPressure01 = health.VramPressure;
                thermalIndex = health.ThermalIndex;
            }

            WriteDictatorState(frameMs, vramPressure01, thermalIndex, _currentKillSwitchMask);
        }

        /// <summary>
        /// Reads the unmanaged tuner state that backs the editor and CSV facade.
        /// </summary>
        public static bool TryGetHardwareDictatorTuning(out ScalabilityTuningDTO tuning)
        {
            tuning = default;
            IDataVault vault = _dataVault;
            if (!EnsureScalabilityTuningHandle(vault))
            {
                return false;
            }

            tuning = _scalabilityTuningHandle.GetElementAsRef(vault, 0);
            return true;
        }

        /// <summary>
        /// Editor/test facade for forced continuous quality. Negative values disable the override.
        /// </summary>
        public static void SetForcedGlobalQualityWeightForTuner(float qualityWeight, bool enabled)
        {
            _forceGlobalQualityWeightOverride = enabled;
            _forcedGlobalQualityWeight = enabled ? math.saturate(qualityWeight) : ForcedQualityWeightDisabled;
            if (enabled)
                _globalQualityWeightSeeded = false;
        }

        /// <summary>
        /// Editor/test facade that arms or clears the synthetic load signal.
        /// </summary>
        public static void SetMockHeavyLoadForTuner(float frameSpikeMs, float vramPressure01, bool enabled)
        {
            IDataVault vault = _dataVault;
            if (!EnsureMockHeavyLoadHandle(vault))
            {
                return;
            }

            ref MockHeavyLoadSignal signal = ref _mockHeavyLoadHandle.GetElementAsRef(vault, 0);
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
            if (!EnsureScalabilityStateHandles(vault))
            {
                return false;
            }

            health = _systemHealthDtoHandle.GetElementAsRef(vault, 0);
            state = _scalabilityStateHandle.GetElementAsRef(vault, 0);
            return true;
        }

        /// <summary>
        /// Copies the dependency-free terrain sampler proof status.
        /// </summary>
        public static bool TryGetMockTerrainSamplerStatus(out MockTerrainSamplerStatus status)
        {
            status = default;
            IDataVault vault = _dataVault;
            if (!EnsureMockTerrainSamplerStatusHandle(vault))
            {
                return false;
            }

            status = _mockTerrainSamplerStatusHandle.GetElementAsRef(vault, 0);
            return true;
        }

        /// <summary>
        /// Copies blackbox samples into editor-owned arrays for the live oscilloscope.
        /// </summary>
        public static int CopyHardwareDictatorOscilloscope(float[] qualityWeightSamples, float[] frameMsSamples, int maxCount)
        {
            if (qualityWeightSamples == null || frameMsSamples == null || maxCount <= 0)
                return 0;

            if (TryResolveScalabilityTelemetry(out NativeArray<ScalabilityTelemetryEntry> telemetry))
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
                        ? math.saturate(entry.GlobalQualityWeight)
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
                    ? math.saturate(qualityWeight)
                    : math.saturate(1f - entry.SystemHealthIndex01);
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
