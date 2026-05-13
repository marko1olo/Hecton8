using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Literal frame-time guard driven by unscaled render-frame duration.
    /// </summary>
    public static class FrameTimeWatchdog
    {
        private const int FrameTimeSampleCount = 64;
        private const int FrameTimeSampleMask = FrameTimeSampleCount - 1;
        private const float InvFrameTimeSampleCount = 0.015625f;
        private const float SpikeThresholdSeconds = 0.01667f;
        private const float OptimalAverageThresholdSeconds = 0.014f;
        private const float CriticalAverageThresholdSeconds = 0.018f;
        private const float CriticalSustainSeconds = 3f;
        private const float ScalabilityCooldownSeconds = 10f;
        private const float ThermalParticleSpawnScale = 0.5f;
        private const float FullParticleSpawnScale = 1f;
        private const uint DefaultSubsystemHash = 0x46545744u;
        private const uint SustainedFrameOptimalHash = 0x46544F50u; // "FTOP"
        private const uint SustainedFrameCriticalHash = 0x46544352u; // "FTCR"
        private const uint DegradeDisableDistantFloraMask = 1u << 0;
        private const uint DegradeHalfParticleEmissionMask = 1u << 1;
        private const uint DegradeDisableVoxelAoMask = 1u << 2;
        private const uint DegradeMathLodHighMask = 1u << 3;
        private const uint DegradeMathLodLowMask = 1u << 4;
        private const uint DegradeCriticalLevelMask = 1u << 31;

        private static NativeRingBuffer<float> _frameTimeSamples;

        private static int _frameTimeSampleCount;
        private static int _consecutiveSpikeFrames;
        private static int _lastSpikeReportFrame = -1;
        private static int _reportedSubsystemFrame = -1;
        private static uint _reportedSubsystemHash;
        private static float _reportedSubsystemCostMs;
        private static int _reportedBrgBatchFrame = -1;
        private static int _reportedBrgBatchCount;
        private static float _frameTimeSumSeconds;
        private static float _criticalAverageSeconds;
        private static float _lastScalabilitySwitchTimeSeconds = -ScalabilityCooldownSeconds;
        private static float _particleEmissionScale = 1f;
        private static MathLodMode _mathLodMode = MathLodMode.High;
        private static bool _voxelAoEnabled = true;
        private static bool _systemDegradationActive;
        private static bool _shaderLodPushed;

        public static bool IsDistantFloraRenderingEnabled => !_systemDegradationActive;
        public static float ParticleEmissionScale => _particleEmissionScale;
        public static bool IsVoxelAmbientOcclusionEnabled => _voxelAoEnabled;
        public static MathLodMode CurrentMathLodMode => _mathLodMode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            DisposeFrameTimeSamples();
            _frameTimeSampleCount = 0;
            _consecutiveSpikeFrames = 0;
            _lastSpikeReportFrame = -1;
            _reportedSubsystemFrame = -1;
            _reportedSubsystemHash = 0u;
            _reportedSubsystemCostMs = 0f;
            _reportedBrgBatchFrame = -1;
            _reportedBrgBatchCount = 0;
            _frameTimeSumSeconds = 0f;
            _criticalAverageSeconds = 0f;
            _lastScalabilitySwitchTimeSeconds = -ScalabilityCooldownSeconds;
            _particleEmissionScale = 1f;
            _mathLodMode = MathLodMode.High;
            _voxelAoEnabled = true;
            _systemDegradationActive = false;
            _shaderLodPushed = false;
        }

        /// <summary>
        /// Reports a subsystem cost sample for the current frame.
        /// </summary>
        /// <param name="subsystemHash">Stable subsystem hash.</param>
        /// <param name="costMilliseconds">Measured cost in milliseconds.</param>
        public static void ReportSubsystemCost(uint subsystemHash, float costMilliseconds)
        {
            if (subsystemHash == 0u || costMilliseconds <= 0f || !math.isfinite(costMilliseconds))
                return;

            int frame = Time.frameCount;
            if (_reportedSubsystemFrame != frame)
            {
                _reportedSubsystemFrame = frame;
                _reportedSubsystemHash = subsystemHash;
                _reportedSubsystemCostMs = costMilliseconds;
                return;
            }

            if (costMilliseconds <= _reportedSubsystemCostMs)
                return;

            _reportedSubsystemHash = subsystemHash;
            _reportedSubsystemCostMs = costMilliseconds;
        }

        /// <summary>
        /// Adds BRG draw-batch estimates reported by render managers without touching Unity Profiler APIs.
        /// </summary>
        public static void ReportBatchRendererGroupBatchCount(int batchCount)
        {
            if (batchCount <= 0)
                return;

            int frame = Time.frameCount;
            if (_reportedBrgBatchFrame != frame)
            {
                _reportedBrgBatchFrame = frame;
                _reportedBrgBatchCount = batchCount;
                return;
            }

            int next = _reportedBrgBatchCount + batchCount;
            _reportedBrgBatchCount = next < 0 ? int.MaxValue : next;
        }

        /// <summary>
        /// Samples dispatcher unscaled delta and emits telemetry/load-shed commands.
        /// </summary>
        public static void Tick()
        {
            EnsureFrameTimeSamples();

            if (!_shaderLodPushed)
                PushInitialScalabilityFromHardwareTier();

            if (GlobalSignals.SimulationPaused)
                return;

            float deltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            if (deltaTime <= 0f)
            {
                _consecutiveSpikeFrames = 0;
                return;
            }

            float averageFrameTimeSeconds = RecordFrameTimeSample(deltaTime);
            if (deltaTime > SpikeThresholdSeconds)
                _consecutiveSpikeFrames++;
            else
                _consecutiveSpikeFrames = 0;

            if (_consecutiveSpikeFrames >= 3)
                PublishSpike(deltaTime);

            PublishDrawCallEstimateIfPresent();

            if (_frameTimeSampleCount >= FrameTimeSampleCount)
                DispatchScalabilityIfNeeded(deltaTime, averageFrameTimeSeconds);
        }

        private static void PublishSpike(float deltaTime)
        {
            int frame = Time.frameCount;
            if (_lastSpikeReportFrame == frame)
                return;

            _lastSpikeReportFrame = frame;
            float frameTimeMilliseconds = deltaTime * 1000f;
            uint subsystemHash = _reportedSubsystemFrame == frame && _reportedSubsystemHash != 0u
                ? _reportedSubsystemHash
                : DefaultSubsystemHash;

            GlobalTelemetryBus.PublishPerformanceSpike(subsystemHash, frameTimeMilliseconds);
            CrashTelemetryBuffer.ReportCriticalPerformanceSpike(subsystemHash, frameTimeMilliseconds, DefaultSubsystemHash);
        }

        private static float RecordFrameTimeSample(float deltaTime)
        {
            EnsureFrameTimeSamples();

            long writeCursor = _frameTimeSamples.TotalWrites;
            int writeSlot = (int)writeCursor & FrameTimeSampleMask;
            float previous = 0f;
            if (_frameTimeSampleCount >= FrameTimeSampleCount)
            {
                previous = _frameTimeSamples[writeSlot];
            }
            else
            {
                _frameTimeSampleCount++;
            }

            _frameTimeSamples.Write(deltaTime);
            _frameTimeSumSeconds += deltaTime - previous;

            return _frameTimeSumSeconds * InvFrameTimeSampleCount;
        }

        private static void EnsureFrameTimeSamples()
        {
            if (_frameTimeSamples.IsCreated)
                return;

            _frameTimeSamples = new NativeRingBuffer<float>(FrameTimeSampleCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeRingBuffer<float>[64] - fixed frame pacing average, no managed List/array growth - owner: FrameTimeWatchdog
            NativeMemorySentinel.RegisterNativeArray(
                _frameTimeSamples.RawArray,
                nameof(FrameTimeWatchdog),
                nameof(_frameTimeSamples),
                NativeAllocationLifetime.Session);
        }

        private static void DisposeFrameTimeSamples()
        {
            if (!_frameTimeSamples.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(_frameTimeSamples.RawArray);
            _frameTimeSamples.Dispose();
            _frameTimeSamples = default;
        }

        private static void DispatchScalabilityIfNeeded(float deltaTime, float averageFrameTimeSeconds)
        {
            bool criticalAverage = averageFrameTimeSeconds > CriticalAverageThresholdSeconds;
            _criticalAverageSeconds = math.select(0f, _criticalAverageSeconds + deltaTime, criticalAverage);

            if (_criticalAverageSeconds >= CriticalSustainSeconds)
            {
                TrySwitchScalability(
                    MathLodMode.Low,
                    SustainedFrameCriticalHash,
                    DegradeDisableDistantFloraMask |
                    DegradeHalfParticleEmissionMask |
                    DegradeDisableVoxelAoMask |
                    DegradeMathLodLowMask |
                    DegradeCriticalLevelMask,
                    averageFrameTimeSeconds * 1000f,
                    CriticalAverageThresholdSeconds * 1000f,
                    SystemDegradationLevel.Critical);
                return;
            }

            if (averageFrameTimeSeconds < OptimalAverageThresholdSeconds)
            {
                TrySwitchScalability(
                    MathLodMode.High,
                    SustainedFrameOptimalHash,
                    DegradeMathLodHighMask,
                    averageFrameTimeSeconds * 1000f,
                    OptimalAverageThresholdSeconds * 1000f,
                    SystemDegradationLevel.Optimal);
            }
        }

        private static void TrySwitchScalability(
            MathLodMode targetMode,
            uint reasonHash,
            uint actionMask,
            float frameTimeMilliseconds,
            float thresholdMilliseconds,
            SystemDegradationLevel degradationLevel)
        {
            if (_shaderLodPushed && _mathLodMode == targetMode)
                return;

            float now = Time.unscaledTime;
            if (_shaderLodPushed && now - _lastScalabilitySwitchTimeSeconds < ScalabilityCooldownSeconds)
                return;

            bool lowMode = targetMode == MathLodMode.Low;
            _mathLodMode = targetMode;
            _shaderLodPushed = true;
            _lastScalabilitySwitchTimeSeconds = now;
            _systemDegradationActive = lowMode;
            _particleEmissionScale = math.select(FullParticleSpawnScale, ThermalParticleSpawnScale, lowMode);
            _voxelAoEnabled = !lowMode;
            if (lowMode)
                GlobalRegistry.BeginMathPrecisionDegradation(Time.frameCount);
            else
                GlobalRegistry.RegisterMathPrecisionLevel(MathPrecisionLevel.High);
            DistanceMath.PushShaderMathLod(targetMode);
            PerformanceEvents.RaiseSystemDegradation(
                frameTimeMilliseconds,
                thresholdMilliseconds,
                Time.frameCount,
                degradationLevel);
            GlobalTelemetryBus.PublishSystemDegradation(reasonHash, actionMask, frameTimeMilliseconds);
        }

        private static void PushInitialScalabilityFromHardwareTier()
        {
            MathLodMode targetMode = ResolveHardwareMathLodMode();
            bool lowMode = targetMode == MathLodMode.Low;
            _mathLodMode = targetMode;
            _shaderLodPushed = true;
            _lastScalabilitySwitchTimeSeconds = Time.unscaledTime;
            _systemDegradationActive = lowMode;
            _particleEmissionScale = math.select(FullParticleSpawnScale, ThermalParticleSpawnScale, lowMode);
            _voxelAoEnabled = !lowMode;
            GlobalRegistry.RegisterMathPrecisionLevel(lowMode ? MathPrecisionLevel.Low : MathPrecisionLevel.High);
            DistanceMath.PushShaderMathLod(targetMode);
        }

        private static MathLodMode ResolveHardwareMathLodMode()
        {
            return DistanceMath.ResolveMathLodMode(GlobalRegistry.ScalabilityTier);
        }

        private static void PublishDrawCallEstimateIfPresent()
        {
            int frame = Time.frameCount;
            if (_reportedBrgBatchFrame != frame || _reportedBrgBatchCount <= 0)
                return;

            GlobalTelemetryBus.PublishApproximateDrawCallCount(_reportedBrgBatchCount);
        }
    }
}
