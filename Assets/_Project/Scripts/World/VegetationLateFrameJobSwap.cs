using Unity.Jobs;

namespace Hecton8.World
{
    /// <summary>
    /// Dispatcher-owned vegetation job swap helper. Call only from ILateFrameTickable swap windows,
    /// origin-shift barriers, or teardown paths that already own simulation pause.
    /// </summary>
    internal static class VegetationLateFrameJobSwap
    {
        /// <summary>
        /// Completes a vegetation job only when already completed, unless the caller owns a forced teardown or origin-shift barrier.
        /// </summary>
        /// <param name="handle">Caller-owned job fence to complete and clear.</param>
        /// <param name="forceComplete">True only when the caller owns a simulation pause or teardown window.</param>
        /// <returns>True when the handle has been completed and reset.</returns>
        public static bool TryComplete(ref JobHandle handle, bool forceComplete)
        {
            if (!forceComplete && !handle.IsCompleted)
                return false;

            handle.Complete();
            handle = default;
            return true;
        }
    }
}
