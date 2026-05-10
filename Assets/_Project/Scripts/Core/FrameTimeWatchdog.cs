using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Literal frame-time guard driven by unscaled render-frame duration.
    /// </summary>
    public static class FrameTimeWatchdog
    {
        private const float SpikeThresholdSeconds = 0.01667f;
        private const float ThermalThresholdSeconds = 0.025f;
        private const float SustainedLowFpsThresholdSeconds = 1f / 30f;
        private const float SustainedLowFpsWindowSeconds = 5f;
        private const float ThermalParticleSpawnScale = 0.5f;
        private const float AggressiveLodBias = 0.5f;
        private const uint DefaultSubsystemHash = 0x46545744u;
        private const uint SustainedLowFpsHash = 0x4C4F4650u; // "LOFP"
        private const uint DegradeDisableDistantFloraMask = 1u << 0;
        private const uint DegradeHalfParticleEmissionMask = 1u << 1;
        private const uint DegradeDisableThermalVolumetricsMask = 1u << 2;
        private const uint DegradeAggressiveLodMask = 1u << 3;

        private static readonly int _globalLodBiasId = Shader.PropertyToID("_GlobalLodBias");
        private static readonly int _thermalVolumetricEnabledId = Shader.PropertyToID("_H8ThermalVolumetricEnabled");
        private static readonly int _thermalParticleSpawnScaleId = Shader.PropertyToID("_H8ThermalParticleSpawnScale");
        private static readonly int _distantFloraEnabledId = Shader.PropertyToID("_H8DistantFloraEnabled");

        private static int _consecutiveSpikeFrames;
        private static int _lastSpikeReportFrame = -1;
        private static int _reportedSubsystemFrame = -1;
        private static uint _reportedSubsystemHash;
        private static float _reportedSubsystemCostMs;
        private static int _reportedBrgBatchFrame = -1;
        private static int _reportedBrgBatchCount;
        private static float _lowFpsAccumulatedSeconds;
        private static float _particleEmissionScale = 1f;
        private static bool _thermalFallbackActive;
        private static bool _systemDegradationActive;

        public static bool IsDistantFloraRenderingEnabled => !_systemDegradationActive;
        public static float ParticleEmissionScale => _particleEmissionScale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _consecutiveSpikeFrames = 0;
            _lastSpikeReportFrame = -1;
            _reportedSubsystemFrame = -1;
            _reportedSubsystemHash = 0u;
            _reportedSubsystemCostMs = 0f;
            _reportedBrgBatchFrame = -1;
            _reportedBrgBatchCount = 0;
            _lowFpsAccumulatedSeconds = 0f;
            _particleEmissionScale = 1f;
            _thermalFallbackActive = false;
            _systemDegradationActive = false;
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
        /// Samples <see cref="Time.unscaledDeltaTime"/> and emits telemetry/load-shed commands.
        /// </summary>
        public static void Tick()
        {
            float deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= 0f)
            {
                _consecutiveSpikeFrames = 0;
                return;
            }

            if (deltaTime > SpikeThresholdSeconds)
                _consecutiveSpikeFrames++;
            else
                _consecutiveSpikeFrames = 0;

            if (_consecutiveSpikeFrames >= 3)
                PublishSpike(deltaTime);

            PublishDrawCallEstimateIfPresent();

            if (deltaTime >= ThermalThresholdSeconds)
                ActivateThermalFallback();

            if (deltaTime >= SustainedLowFpsThresholdSeconds)
            {
                _lowFpsAccumulatedSeconds += deltaTime;
                if (_lowFpsAccumulatedSeconds >= SustainedLowFpsWindowSeconds)
                    ActivateSystemDegradation(deltaTime);
            }
            else
            {
                _lowFpsAccumulatedSeconds = 0f;
            }
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

        private static void ActivateThermalFallback()
        {
            if (_thermalFallbackActive)
                return;

            _thermalFallbackActive = true;
            Shader.SetGlobalInt(_thermalVolumetricEnabledId, 0);
            Shader.SetGlobalFloat(_thermalParticleSpawnScaleId, ThermalParticleSpawnScale);
            Shader.SetGlobalFloat(_globalLodBiasId, AggressiveLodBias);
        }

        private static void ActivateSystemDegradation(float deltaTime)
        {
            if (_systemDegradationActive)
                return;

            _systemDegradationActive = true;
            _particleEmissionScale = ThermalParticleSpawnScale;
            ActivateThermalFallback();
            Shader.SetGlobalInt(_distantFloraEnabledId, 0);

            float frameTimeMilliseconds = deltaTime * 1000f;
            const uint actionMask =
                DegradeDisableDistantFloraMask |
                DegradeHalfParticleEmissionMask |
                DegradeDisableThermalVolumetricsMask |
                DegradeAggressiveLodMask;

            PerformanceEvents.RaiseSystemDegradation(
                frameTimeMilliseconds,
                SustainedLowFpsThresholdSeconds * 1000f,
                Time.frameCount);
            GlobalTelemetryBus.PublishSystemDegradation(SustainedLowFpsHash, actionMask, frameTimeMilliseconds);
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
