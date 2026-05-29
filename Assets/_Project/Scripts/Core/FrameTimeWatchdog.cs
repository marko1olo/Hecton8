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
        private const float MathLodPrecisionWeightThreshold01 = 0.5f;
        private const float DistantFloraDisableWeightThreshold01 = 0.2f;
        private const float DistantFloraPressureDisableThreshold01 = 0.75f;
        private const float VoxelAoEnableWeightThreshold01 = 0.5f;
        private const float VoxelAoPressureDisableThreshold01 = 0.6f;
        private const float ThermalParticleSpawnScale = 0.5f;
        private const float FullParticleSpawnScale = 1f;
        private const float CriticalMathLodPressureFloor01 = 0.85f;
        private const float FramePressureReleasePerSecond = 0.5f;
        private const uint DefaultSubsystemHash = 0x46545744u;
        private const uint SustainedFrameOptimalHash = 0x46544F50u; // "FTOP"
        private const uint SustainedFrameCriticalHash = 0x46544352u; // "FTCR"
        private const uint DegradeDisableDistantFloraMask = 1u << 0;
        private const uint DegradeHalfParticleEmissionMask = 1u << 1;
        private const uint DegradeDisableVoxelAoMask = 1u << 2;
        private const uint DegradeMathLodHighMask = 1u << 3;
        private const uint DegradeMathLodLowMask = 1u << 4;
        private const uint DegradeCriticalLevelMask = 1u << 31;

        private delegate void MathPrecisionLevelWriter(MathPrecisionLevel precisionLevel);
        private delegate void MathPrecisionDegradationWriter(int frame);
        private delegate void MathPrecisionTransitionTicker(int frame);

        // COLD ALLOC: delegates[3] - boot-bound GlobalRegistry writers; hot code invokes cached routes, not registry lookups - owner: FrameTimeWatchdog
        private static readonly MathPrecisionLevelWriter s_registerMathPrecisionLevel = GlobalRegistry.RegisterMathPrecisionLevel;
        private static readonly MathPrecisionDegradationWriter s_beginMathPrecisionDegradation = GlobalRegistry.BeginMathPrecisionDegradation;
        private static readonly MathPrecisionTransitionTicker s_tickMathPrecisionTransition = GlobalRegistry.TickMathPrecisionTransition;

        private static NativeRingBuffer<float> _frameTimeSamples;

        private static int _frameTimeSampleCount;
        private static int _consecutiveSpikeFrames;
        private static int _lastSpikeReportFrame = -1;
        private static int _reportedSubsystemFrame = -1;
        private static uint _reportedSubsystemHash;
        private static float _reportedSubsystemCostMs;
        private static int _lastFrameSampleFrame = -1;
        private static int _lastScalabilityDispatchFrame = -1;
        private static int _reportedBrgBatchFrame = -1;
        private static int _reportedBrgBatchCount;
        private static float _lastFrameDeltaTimeSeconds;
        private static float _lastAverageFrameTimeSeconds;
        private static float _frameTimeSumSeconds;
        private static float _framePressure01;
        private static float _criticalAverageSeconds;
        private static float _lastScalabilitySwitchTimeSeconds = -ScalabilityCooldownSeconds;
        private static float _particleEmissionScale = 1f;
        private static float _visualQualityWeight01 = 1f;
        private static MathLodMode _mathLodMode = MathLodMode.High;
        private static bool _voxelAoEnabled = true;
        private static bool _systemDegradationActive;
        private static bool _shaderLodPushed;

        public static bool IsDistantFloraRenderingEnabled => !_systemDegradationActive;
        public static float ParticleEmissionScale => _particleEmissionScale;
        public static float CurrentVisualQualityWeight01 => _visualQualityWeight01;
        public static bool IsVoxelAmbientOcclusionEnabled => _voxelAoEnabled;

        internal static void TickMathPrecisionTransition(int frame)
        {
            s_tickMathPrecisionTransition(frame);
        }

        public static void InitializeCold()
        {
            EnsureFrameTimeSamples();
        }

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
            _lastFrameSampleFrame = -1;
            _lastScalabilityDispatchFrame = -1;
            _reportedBrgBatchFrame = -1;
            _reportedBrgBatchCount = 0;
            _lastFrameDeltaTimeSeconds = 0f;
            _lastAverageFrameTimeSeconds = 0f;
            _frameTimeSumSeconds = 0f;
            _framePressure01 = 0f;
            _criticalAverageSeconds = 0f;
            _lastScalabilitySwitchTimeSeconds = -ScalabilityCooldownSeconds;
            _particleEmissionScale = 1f;
            _visualQualityWeight01 = 1f;
            _mathLodMode = MathLodMode.High;
            _voxelAoEnabled = true;
            _systemDegradationActive = false;
            _shaderLodPushed = false;
        }

        /// <summary>Releases persistent frame telemetry buffers during explicit bootstrap teardown.</summary>
        public static void Shutdown()
        {
            ResetStaticState();
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

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
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

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
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
        /// Samples dispatcher unscaled delta and updates frame-pressure state.
        /// </summary>
        public static void Tick()
        {
            if (!_frameTimeSamples.IsCreated)
                return;

            if (SimulationSignalRoute.SimulationPaused)
                return;

            float deltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            if (deltaTime <= 0f)
            {
                _consecutiveSpikeFrames = 0;
                return;
            }

            float averageFrameTimeSeconds = RecordFrameTimeSample(deltaTime);
            UpdateFramePressure(deltaTime, averageFrameTimeSeconds);
            _lastFrameDeltaTimeSeconds = deltaTime;
            _lastAverageFrameTimeSeconds = averageFrameTimeSeconds;
            _lastFrameSampleFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (deltaTime > SpikeThresholdSeconds)
                _consecutiveSpikeFrames++;
            else
                _consecutiveSpikeFrames = 0;

            if (_consecutiveSpikeFrames >= 3)
                PublishSpike(deltaTime);
        }

        public static void LateFrameTick()
        {
            if (!_frameTimeSamples.IsCreated)
                return;

            if (!_shaderLodPushed)
                PushInitialScalabilityFromGlobalQuality();

            RefreshContinuousQualityOutputs();

            PublishDrawCallEstimateIfPresent();

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastFrameSampleFrame != frame ||
                _lastScalabilityDispatchFrame == frame ||
                _frameTimeSampleCount < FrameTimeSampleCount)
            {
                return;
            }

            _lastScalabilityDispatchFrame = frame;
            DispatchScalabilityIfNeeded(_lastFrameDeltaTimeSeconds, _lastAverageFrameTimeSeconds);
        }

        private static void PublishSpike(float deltaTime)
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
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
            _frameTimeSamples.RegisterBackingArray(
                nameof(FrameTimeWatchdog),
                nameof(_frameTimeSamples),
                NativeAllocationLifetime.Session);
        }

        private static void DisposeFrameTimeSamples()
        {
            if (!_frameTimeSamples.IsCreated)
                return;

            _frameTimeSamples.UnregisterBackingArray();
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

            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (_shaderLodPushed && now - _lastScalabilitySwitchTimeSeconds < ScalabilityCooldownSeconds)
                return;

            bool lowMode = targetMode == MathLodMode.Low;
            _mathLodMode = targetMode;
            _shaderLodPushed = true;
            _lastScalabilitySwitchTimeSeconds = now;
            ApplyContinuousQualityState(ResolveGlobalQualityWeight01(), lowMode);
            if (lowMode)
                s_beginMathPrecisionDegradation(Hecton8.Core.SystemDispatcher.CurrentFrameIndex);
            else
                s_registerMathPrecisionLevel(MathPrecisionLevel.High);
            float shaderQualityWeight01 = ResolveShaderQualityWeight01(ResolveGlobalQualityWeight01(), lowMode);
            DistanceMath.PushShaderMathLod(shaderQualityWeight01);
            PerformanceEvents.TryRaiseSystemDegradation(
                frameTimeMilliseconds,
                thresholdMilliseconds,
                Hecton8.Core.SystemDispatcher.CurrentFrameIndex,
                degradationLevel);
            GlobalTelemetryBus.PublishSystemDegradation(reasonHash, actionMask, frameTimeMilliseconds);
        }

        private static void PushInitialScalabilityFromGlobalQuality()
        {
            float qualityWeight01 = ResolveGlobalQualityWeight01();
            MathLodMode targetMode = ResolveQualityMathLodMode(qualityWeight01);
            bool lowMode = targetMode == MathLodMode.Low;
            _mathLodMode = targetMode;
            _shaderLodPushed = true;
            _lastScalabilitySwitchTimeSeconds = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            ApplyContinuousQualityState(qualityWeight01, lowMode);
            s_registerMathPrecisionLevel(lowMode ? MathPrecisionLevel.Low : MathPrecisionLevel.High);
            DistanceMath.PushShaderMathLod(ResolveShaderQualityWeight01(qualityWeight01, lowMode));
        }

        private static void RefreshContinuousQualityOutputs()
        {
            ApplyContinuousQualityState(ResolveGlobalQualityWeight01(), _mathLodMode == MathLodMode.Low);
        }

        private static void ApplyContinuousQualityState(float qualityWeight01, bool forcedLowMathLod)
        {
            float pressure01 = ResolveEffectiveFramePressure01(forcedLowMathLod);
            float safeQuality01 = math.saturate(math.select(1f, qualityWeight01, math.isfinite(qualityWeight01)));
            float pressureFloor01 = math.min(safeQuality01, 0.25f);
            float effectiveQuality01 = math.lerp(safeQuality01, pressureFloor01, pressure01);
            float curvedQuality01 = SmoothStep01(effectiveQuality01);
            _visualQualityWeight01 = curvedQuality01;
            _systemDegradationActive =
                pressure01 >= DistantFloraPressureDisableThreshold01 ||
                curvedQuality01 <= DistantFloraDisableWeightThreshold01;
            _particleEmissionScale = math.lerp(ThermalParticleSpawnScale, FullParticleSpawnScale, curvedQuality01);
            _voxelAoEnabled =
                pressure01 < VoxelAoPressureDisableThreshold01 &&
                curvedQuality01 >= VoxelAoEnableWeightThreshold01;
        }

        private static float ResolveShaderQualityWeight01(float qualityWeight01, bool forcedLowMathLod)
        {
            float safeQuality = math.saturate(math.select(1f, qualityWeight01, math.isfinite(qualityWeight01)));
            float pressure01 = ResolveEffectiveFramePressure01(forcedLowMathLod);
            return math.lerp(safeQuality, math.min(safeQuality, 0.25f), pressure01);
        }

        private static void UpdateFramePressure(float deltaTime, float averageFrameTimeSeconds)
        {
            float targetPressure01 = ResolveFramePressure01(averageFrameTimeSeconds);
            if (targetPressure01 >= _framePressure01)
            {
                _framePressure01 = targetPressure01;
                return;
            }

            float release = math.saturate(deltaTime * FramePressureReleasePerSecond);
            _framePressure01 = math.max(targetPressure01, _framePressure01 - release);
        }

        private static float ResolveEffectiveFramePressure01(bool forcedLowMathLod)
        {
            float pressure01 = math.saturate(_framePressure01);
            return forcedLowMathLod
                ? math.max(pressure01, CriticalMathLodPressureFloor01)
                : pressure01;
        }

        private static float ResolveFramePressure01(float averageFrameTimeSeconds)
        {
            float range = CriticalAverageThresholdSeconds - OptimalAverageThresholdSeconds;
            if (!math.isfinite(averageFrameTimeSeconds) || range <= 0f)
                return 0f;

            return SmoothStep01((averageFrameTimeSeconds - OptimalAverageThresholdSeconds) / range);
        }

        private static MathLodMode ResolveQualityMathLodMode(float qualityWeight01)
        {
            return SmoothStep01(qualityWeight01) >= MathLodPrecisionWeightThreshold01
                ? MathLodMode.High
                : MathLodMode.Low;
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(qualityWeight) ? math.saturate(qualityWeight) : 1.0f;
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3.0f - (2.0f * t));
        }

        private static void PublishDrawCallEstimateIfPresent()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_reportedBrgBatchFrame != frame || _reportedBrgBatchCount <= 0)
                return;

            GlobalTelemetryBus.PublishApproximateDrawCallCount(_reportedBrgBatchCount);
        }
    }
}
