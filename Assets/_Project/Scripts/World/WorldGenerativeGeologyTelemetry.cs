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
        private const uint TerrainSeamsBlendedHash = 0x5453424Cu;
        private const uint VoxelBridgeTelemetryContextHash = 0x56425843u;
        private const uint TerrainSeamTelemetryContextHash = 0x54534D43u;
        private const float BuildDataTelemetryThresholdMs = 4f;

        [System.Obsolete("Use TryPublishVoxelQueuePressureIfNeeded(int,ref int) so telemetry suppression is visible.", true)]
        internal static void PublishVoxelQueuePressureIfNeeded(int queuedLaunchCount, ref int nextTelemetryFrame)
        {
            TryPublishVoxelQueuePressureIfNeeded(queuedLaunchCount, ref nextTelemetryFrame);
        }

        internal static bool TryPublishVoxelQueuePressureIfNeeded(int queuedLaunchCount, ref int nextTelemetryFrame)
        {
            if (!Application.isPlaying || queuedLaunchCount < QueuedLaunchTelemetryThreshold)
                return false;

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (frame < nextTelemetryFrame)
                return false;

            nextTelemetryFrame = frame + VoxelQueueTelemetryFrameInterval;
            GlobalTelemetryBus.PublishPerformanceWarning(
                QueuePressureWarningHash,
                VoxelBridgeTelemetryContextHash,
                queuedLaunchCount);
            return true;
        }

        [System.Obsolete("Use TryPublishVoxelBuildDataBudgetIfNeeded(float) so telemetry suppression is visible.", true)]
        internal static void PublishVoxelBuildDataBudgetIfNeeded(float buildDataMs)
        {
            TryPublishVoxelBuildDataBudgetIfNeeded(buildDataMs);
        }

        internal static bool TryPublishVoxelBuildDataBudgetIfNeeded(float buildDataMs)
        {
            if (!Application.isPlaying || buildDataMs < BuildDataTelemetryThresholdMs)
                return false;

            GlobalTelemetryBus.PublishPerformanceWarning(
                BuildDataBudgetWarningHash,
                VoxelBridgeTelemetryContextHash,
                buildDataMs);
            return true;
        }

        [System.Obsolete("Use TryPublishVoxelFaultIfNeeded(uint,float,ref int) so telemetry suppression is visible.", true)]
        internal static void PublishVoxelFaultIfNeeded(uint warningHash, float scalarValue, ref int nextTelemetryFrame)
        {
            TryPublishVoxelFaultIfNeeded(warningHash, scalarValue, ref nextTelemetryFrame);
        }

        internal static bool TryPublishVoxelFaultIfNeeded(uint warningHash, float scalarValue, ref int nextTelemetryFrame)
        {
            if (!Application.isPlaying)
                return false;

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (frame < nextTelemetryFrame)
                return false;

            nextTelemetryFrame = frame + VoxelFaultTelemetryFrameInterval;
            GlobalTelemetryBus.PublishPerformanceWarning(
                warningHash,
                VoxelBridgeTelemetryContextHash,
                scalarValue);
            return true;
        }

        [System.Obsolete("Use TryPublishTerrainPatchBridgeWarningIfNeeded(int,int,ref int) so telemetry suppression is visible.", true)]
        internal static void PublishTerrainPatchBridgeWarningIfNeeded(
            int patchSampleCount,
            int patchSampleBudget,
            ref int nextTelemetryFrame)
        {
            TryPublishTerrainPatchBridgeWarningIfNeeded(patchSampleCount, patchSampleBudget, ref nextTelemetryFrame);
        }

        internal static bool TryPublishTerrainPatchBridgeWarningIfNeeded(
            int patchSampleCount,
            int patchSampleBudget,
            ref int nextTelemetryFrame)
        {
            if (!Application.isPlaying || patchSampleCount <= patchSampleBudget)
                return false;

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (frame < nextTelemetryFrame)
                return false;

            nextTelemetryFrame = frame + TerrainPatchTelemetryFrameInterval;
            GlobalTelemetryBus.PublishPerformanceWarning(
                TerrainPatchBridgeWarningHash,
                TerrainSeamTelemetryContextHash,
                patchSampleCount);
            return true;
        }

        [System.Obsolete("Use TryPublishTerrainSeamsBlended(int,int,float) so telemetry suppression is visible.", true)]
        internal static void PublishTerrainSeamsBlended(
            int patchSampleCount,
            int planCount,
            bool visualSamplingSuppressed)
        {
            TryPublishTerrainSeamsBlended(patchSampleCount, planCount, visualSamplingSuppressed);
        }

        internal static bool TryPublishTerrainSeamsBlended(
            int patchSampleCount,
            int planCount,
            bool visualSamplingSuppressed)
        {
            return TryPublishTerrainSeamsBlended(
                patchSampleCount,
                planCount,
                visualSamplingSuppressed ? 0f : 1f);
        }

        internal static bool TryPublishTerrainSeamsBlended(
            int patchSampleCount,
            int planCount,
            float seamExpensiveWeight)
        {
            if (!Application.isPlaying)
                return false;

            float packed = patchSampleCount + planCount * 0.001f + Mathf.Clamp01(seamExpensiveWeight) * 0.0005f;
            GlobalTelemetryBus.PublishPerformanceWarning(
                TerrainSeamsBlendedHash,
                TerrainSeamTelemetryContextHash,
                packed);
            return true;
        }
    }
}
