using Hecton8.Core;
using Unity.Collections;
using Unity.Jobs;

namespace Hecton8.AI.Pathfinding
{
    /// <summary>
    /// Explicit scheduler helpers for PRE_SIMULATION schedule and POST_SIMULATION readback.
    /// </summary>
    public static class PathFunnelSchedule
    {
        /// <summary>
        /// Schedules a funnel job from the pre-simulation admission window.
        /// </summary>
        /// <param name="job">Configured Burst job.</param>
        /// <param name="inputDeps">Dependency chain owned by the caller.</param>
        /// <returns>Tracked job handle.</returns>
        public static JobHandle SchedulePreSimulation(ref FunnelSmoothingJob job, JobHandle inputDeps)
        {
            return job.Schedule(inputDeps);
        }

        /// <summary>
        /// Consumes a completed funnel result during the post-simulation swap window.
        /// </summary>
        /// <param name="handle">Tracked funnel handle.</param>
        /// <param name="resultBuffer">Single-slot result buffer.</param>
        /// <param name="result">Copied result payload.</param>
        /// <returns>True when the result was available this frame.</returns>
        /// <remarks>
        /// This helper refuses to force-complete unfinished jobs. Callers can defer readback to the next
        /// late-frame pass instead of serializing the worker thread.
        /// </remarks>
        public static bool TryConsumeFinalizedPostSimulation(ref JobHandle handle, NativeArray<PathFunnelResult> resultBuffer, out PathFunnelResult result)
        {
            result = default;
            if (!resultBuffer.IsCreated || resultBuffer.Length <= 0 || !handle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref handle))
                return false;

            result = resultBuffer[0];
            return true;
        }
    }
}
