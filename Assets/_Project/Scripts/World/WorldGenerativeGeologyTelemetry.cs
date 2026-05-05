using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Stateless telemetry gates for world-generation geology. Callers own rate-limit frame state.
    /// </summary>
    internal static class WorldGenerativeGeologyTelemetry
    {
        internal const uint VoxelEngineMissingWarningHash = 0x56425845u;
        internal const uint VoxelVolumeNullWarningHash = 0x5642584Eu;

        private const int VoxelQueueTelemetryFrameInterval = 120;
        private const int VoxelFaultTelemetryFrameInterval = 120;
        private const int TerrainPatchTelemetryFrameInterval = 120;
        private const int QueuedLaunchTelemetryThreshold = 4;
        private const uint QueuePressureWarningHash = 0x56425851u;
        private const uint BuildDataBudgetWarningHash = 0x56425842u;
        private const uint TerrainPatchBridgeWarningHash = 0x54485042u;
        private const uint VoxelBridgeTelemetryContextHash = 0x56425843u;
        private const uint TerrainSeamTelemetryContextHash = 0x54534D43u;
        private const float BuildDataTelemetryThresholdMs = 4f;

        internal static void PublishVoxelQueuePressureIfNeeded(int queuedLaunchCount, ref int nextTelemetryFrame)
        {
            if (!Application.isPlaying || queuedLaunchCount < QueuedLaunchTelemetryThreshold)
                return;

            int frame = Time.frameCount;
            if (frame < nextTelemetryFrame)
                return;

            nextTelemetryFrame = frame + VoxelQueueTelemetryFrameInterval;
            GlobalTelemetryBus.PublishPerformanceWarning(
                QueuePressureWarningHash,
                VoxelBridgeTelemetryContextHash,
                queuedLaunchCount);
        }

        internal static void PublishVoxelBuildDataBudgetIfNeeded(float buildDataMs)
        {
            if (!Application.isPlaying || buildDataMs < BuildDataTelemetryThresholdMs)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                BuildDataBudgetWarningHash,
                VoxelBridgeTelemetryContextHash,
                buildDataMs);
        }

        internal static void PublishVoxelFaultIfNeeded(uint warningHash, float scalarValue, ref int nextTelemetryFrame)
        {
            if (!Application.isPlaying)
                return;

            int frame = Time.frameCount;
            if (frame < nextTelemetryFrame)
                return;

            nextTelemetryFrame = frame + VoxelFaultTelemetryFrameInterval;
            GlobalTelemetryBus.PublishPerformanceWarning(
                warningHash,
                VoxelBridgeTelemetryContextHash,
                scalarValue);
        }

        internal static void PublishTerrainPatchBridgeWarningIfNeeded(
            int patchSampleCount,
            int patchSampleBudget,
            ref int nextTelemetryFrame)
        {
            if (!Application.isPlaying || patchSampleCount <= patchSampleBudget)
                return;

            int frame = Time.frameCount;
            if (frame < nextTelemetryFrame)
                return;

            nextTelemetryFrame = frame + TerrainPatchTelemetryFrameInterval;
            GlobalTelemetryBus.PublishPerformanceWarning(
                TerrainPatchBridgeWarningHash,
                TerrainSeamTelemetryContextHash,
                patchSampleCount);
        }
    }
}
