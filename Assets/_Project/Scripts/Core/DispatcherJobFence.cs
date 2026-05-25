using System.Runtime.CompilerServices;
using Unity.Jobs;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
#endif

namespace Hecton8.Core
{
    /// <summary>
    /// Core-owned helper for dispatcher job fence reclamation.
    /// Keep this class inside Core: depending on a sibling runtime swap helper here
    /// reintroduces a sibling-runtime compile wall for every system using
    /// dispatcher-safe job finalization.
    /// </summary>
    public static class DispatcherJobFence
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const float IllegalCompletionWarningIntervalSeconds = 5f;
        private const string IllegalCompletionWarningMessage =
            "[DispatcherJobFence] Non-forced job completion requested outside dispatcher swap window.";
#endif

        private static int _activeSwapWindowDepth;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static float _nextIllegalCompletionWarningTime;
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BeginLateFrameSwapWindow()
        {
            _activeSwapWindowDepth++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndLateFrameSwapWindow()
        {
            if (_activeSwapWindowDepth > 0)
                _activeSwapWindowDepth--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BeginPostFixedSwapWindow()
        {
            _activeSwapWindowDepth++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndPostFixedSwapWindow()
        {
            if (_activeSwapWindowDepth > 0)
                _activeSwapWindowDepth--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BeginPostSimulationSwapWindow()
        {
            _activeSwapWindowDepth++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndPostSimulationSwapWindow()
        {
            if (_activeSwapWindowDepth > 0)
                _activeSwapWindowDepth--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFinalizeCompleted(ref JobHandle handle)
        {
            if (!handle.IsCompleted)
                return false;

            handle.Complete();
            handle = default;
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void WarnIllegalNonForcedCompletion()
        {
            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (now < _nextIllegalCompletionWarningTime)
                return;

            _nextIllegalCompletionWarningTime = now + IllegalCompletionWarningIntervalSeconds;
            Hecton8.Core.H8Debug.LogWarning(IllegalCompletionWarningMessage);
        }
#endif
    }
}
