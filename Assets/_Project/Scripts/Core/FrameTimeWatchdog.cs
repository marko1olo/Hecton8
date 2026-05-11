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
        private const float CriticalThresholdSeconds = 0.025f;
        private const float WarningThresholdSeconds = 0.01667f;
        private const int DegradationConsecutiveFrames = 180;
        private const float ThermalParticleSpawnScale = 0.5f;
        private const uint DefaultSubsystemHash = 0x46545744u;
        private const uint SustainedFrameWarningHash = 0x4654574Eu; // "FTWN"
        private const uint SustainedFrameCriticalHash = 0x46544352u; // "FTCR"
        private const uint DegradeDisableDistantFloraMask = 1u << 0;
        private const uint DegradeHalfParticleEmissionMask = 1u << 1;
        private const uint DegradeDisableVoxelAoMask = 1u << 2;
        private const uint DegradeCriticalLevelMask = 1u << 31;

        private static int _consecutiveSpikeFrames;
        private static int _consecutiveWarningFrames;
        private static int _lastSpikeReportFrame = -1;
        private static int _lastDegradationReportFrame = -1;
        private static int _reportedSubsystemFrame = -1;
        private static uint _reportedSubsystemHash;
        private static float _reportedSubsystemCostMs;
        private static int _reportedBrgBatchFrame = -1;
        private static int _reportedBrgBatchCount;
        private static float _particleEmissionScale = 1f;
        private static bool _voxelAoEnabled = true;
        private static bool _systemDegradationActive;

        public static bool IsDistantFloraRenderingEnabled => !_systemDegradationActive;
        public static float ParticleEmissionScale => _particleEmissionScale;
        public static bool IsVoxelAmbientOcclusionEnabled => _voxelAoEnabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _consecutiveSpikeFrames = 0;
            _consecutiveWarningFrames = 0;
            _lastSpikeReportFrame = -1;
            _lastDegradationReportFrame = -1;
            _reportedSubsystemFrame = -1;
            _reportedSubsystemHash = 0u;
            _reportedSubsystemCostMs = 0f;
            _reportedBrgBatchFrame = -1;
            _reportedBrgBatchCount = 0;
            _particleEmissionScale = 1f;
            _voxelAoEnabled = true;
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
                _consecutiveWarningFrames = 0;
                return;
            }

            if (deltaTime > SpikeThresholdSeconds)
                _consecutiveSpikeFrames++;
            else
                _consecutiveSpikeFrames = 0;

            if (_consecutiveSpikeFrames >= 3)
                PublishSpike(deltaTime);

            PublishDrawCallEstimateIfPresent();

            if (deltaTime > WarningThresholdSeconds)
                _consecutiveWarningFrames++;
            else
                _consecutiveWarningFrames = 0;

            if (deltaTime >= CriticalThresholdSeconds)
            {
                ActivateSystemDegradation(deltaTime, true);
                return;
            }

            if (_consecutiveWarningFrames >= DegradationConsecutiveFrames)
            {
                PublishSystemDegradationWarning(deltaTime);
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

        private static void PublishSystemDegradationWarning(float deltaTime)
        {
            int frame = Time.frameCount;
            if (_lastDegradationReportFrame == frame)
                return;

            _lastDegradationReportFrame = frame;
            float frameTimeMilliseconds = deltaTime * 1000f;
            PerformanceEvents.RaiseSystemDegradation(
                frameTimeMilliseconds,
                WarningThresholdSeconds * 1000f,
                frame);
            GlobalTelemetryBus.PublishSystemDegradation(SustainedFrameWarningHash, 0u, frameTimeMilliseconds);
        }

        private static void ActivateSystemDegradation(float deltaTime, bool critical)
        {
            if (_systemDegradationActive)
                return;

            _systemDegradationActive = true;
            _particleEmissionScale = ThermalParticleSpawnScale;
            _voxelAoEnabled = false;

            float frameTimeMilliseconds = deltaTime * 1000f;
            const uint actionMask =
                DegradeDisableDistantFloraMask |
                DegradeHalfParticleEmissionMask |
                DegradeDisableVoxelAoMask |
                DegradeCriticalLevelMask;

            PerformanceEvents.RaiseSystemDegradation(
                frameTimeMilliseconds,
                (critical ? CriticalThresholdSeconds : WarningThresholdSeconds) * 1000f,
                Time.frameCount);
            GlobalTelemetryBus.PublishSystemDegradation(SustainedFrameCriticalHash, actionMask, frameTimeMilliseconds);
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
