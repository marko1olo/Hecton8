using Unity.Jobs;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Dispatcher-owned job swap helper. Call only from ILateFrameTickable or IPostFixedTickable swap windows,
    /// origin-shift barriers, or teardown paths that already own simulation pause.
    /// </summary>
    public static class DispatcherJobSwap
    {
        private const float IllegalCompletionWarningIntervalSeconds = 5f;
        private const string IllegalCompletionWarningMessage =
            "[DispatcherJobSwap] Non-forced job completion requested outside dispatcher swap window.";

        private static int _activeSwapWindowDepth;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static float _nextIllegalCompletionWarningTime;
#endif

        /// <summary>
        /// Marks the start of the dispatcher-owned late-frame swap window.
        /// </summary>
        public static void BeginLateFrameSwapWindow()
        {
            _activeSwapWindowDepth++;
        }

        /// <summary>
        /// Marks the end of the dispatcher-owned late-frame swap window.
        /// </summary>
        public static void EndLateFrameSwapWindow()
        {
            if (_activeSwapWindowDepth > 0)
                _activeSwapWindowDepth--;
        }

        /// <summary>
        /// Marks the start of the dispatcher-owned post-fixed swap window.
        /// </summary>
        public static void BeginPostFixedSwapWindow()
        {
            _activeSwapWindowDepth++;
        }

        /// <summary>
        /// Marks the end of the dispatcher-owned post-fixed swap window.
        /// </summary>
        public static void EndPostFixedSwapWindow()
        {
            if (_activeSwapWindowDepth > 0)
                _activeSwapWindowDepth--;
        }

        /// <summary>
        /// Completes a job only when already completed, unless the caller owns a forced teardown or origin-shift barrier.
        /// </summary>
        /// <param name="handle">Caller-owned job fence to complete and clear.</param>
        /// <param name="forceComplete">True only when the caller owns a simulation pause or teardown window.</param>
        /// <returns>True when the handle has been completed and reset.</returns>
        public static bool TryComplete(ref JobHandle handle, bool forceComplete)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!forceComplete && _activeSwapWindowDepth <= 0)
                WarnIllegalNonForcedCompletion();
#endif

            if (!forceComplete && !handle.IsCompleted)
                return false;

            handle.Complete();
            handle = default;
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void WarnIllegalNonForcedCompletion()
        {
            float now = Time.unscaledTime;
            if (now < _nextIllegalCompletionWarningTime)
                return;

            _nextIllegalCompletionWarningTime = now + IllegalCompletionWarningIntervalSeconds;
            Debug.LogWarning(IllegalCompletionWarningMessage);
        }
#endif
    }
}
