using Hecton8.Core;
using Unity.Jobs;

namespace Hecton8.World
{
    /// <summary>
    /// Dispatcher-owned job swap helper. Call only from ILateFrameTickable or IPostFixedTickable swap windows,
    /// origin-shift barriers, or teardown paths that already own simulation pause.
    /// </summary>
    public static class DispatcherJobSwap
    {
        /// <summary>
        /// Marks the start of the dispatcher-owned pre-simulation swap window.
        /// </summary>
        public static void BeginPreSimulationSwapWindow()
        {
            DispatcherJobFence.BeginPreSimulationSwapWindow();
        }

        /// <summary>
        /// Marks the end of the dispatcher-owned pre-simulation swap window.
        /// </summary>
        public static void EndPreSimulationSwapWindow()
        {
            DispatcherJobFence.EndPreSimulationSwapWindow();
        }

        /// <summary>
        /// Marks the start of the dispatcher-owned late-frame swap window.
        /// </summary>
        public static void BeginLateFrameSwapWindow()
        {
            DispatcherJobFence.BeginLateFrameSwapWindow();
        }

        /// <summary>
        /// Marks the end of the dispatcher-owned late-frame swap window.
        /// </summary>
        public static void EndLateFrameSwapWindow()
        {
            DispatcherJobFence.EndLateFrameSwapWindow();
        }

        /// <summary>
        /// Marks the start of the dispatcher-owned post-fixed swap window.
        /// </summary>
        public static void BeginPostFixedSwapWindow()
        {
            DispatcherJobFence.BeginPostFixedSwapWindow();
        }

        /// <summary>
        /// Marks the end of the dispatcher-owned post-fixed swap window.
        /// </summary>
        public static void EndPostFixedSwapWindow()
        {
            DispatcherJobFence.EndPostFixedSwapWindow();
        }

        /// <summary>
        /// Marks the start of the dispatcher-owned post-simulation swap window.
        /// </summary>
        public static void BeginPostSimulationSwapWindow()
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
        }

        /// <summary>
        /// Marks the end of the dispatcher-owned post-simulation swap window.
        /// </summary>
        public static void EndPostSimulationSwapWindow()
        {
            DispatcherJobFence.EndPostSimulationSwapWindow();
        }

        /// <summary>
        /// Completes a job only when already completed, unless the caller owns a forced teardown or origin-shift barrier.
        /// </summary>
        /// <param name="handle">Caller-owned job fence to complete and clear.</param>
        /// <param name="forceComplete">True only when the caller owns a simulation pause or teardown window.</param>
        /// <returns>True when the handle has been completed and reset.</returns>
        public static bool TryComplete(ref JobHandle handle, bool forceComplete)
        {
            return DispatcherJobFence.TryComplete(ref handle, forceComplete);
        }

        /// <summary>
        /// Clears a handle only after the caller has observed completion. This is a non-blocking finalization path.
        /// </summary>
        /// <param name="handle">Caller-owned job fence to finalize and clear.</param>
        /// <returns>True when the handle was already complete and has been reset.</returns>
        public static bool TryFinalizeCompleted(ref JobHandle handle)
        {
            return DispatcherJobFence.TryFinalizeCompleted(ref handle);
        }
    }
}
