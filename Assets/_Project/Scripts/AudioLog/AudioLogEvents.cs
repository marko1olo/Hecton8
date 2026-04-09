// ============================================================================
// HECTON-8 — AudioLogEvents.cs
// Статическая шина событий для системы аудиодневников.
// Zero GC, main thread only.
// ============================================================================

using System;

namespace Hecton8.Narrative
{
    /// <summary>
    /// Статический event bus для AudioLog системы.
    /// Подписчики: PDA архив, HUD субтитры, SpatialAudioManager.
    /// </summary>
    public static class AudioLogEvents
    {
        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnLogDiscovered = null;
            OnLogPlaybackStarted = null;
            OnLogPlaybackStopped = null;
            OnLogPlaybackCompleted = null;
        }

        /// <summary>
        /// Вызывается при первом обнаружении аудиодневника.
        /// string: logId
        /// </summary>
        public static event Action<string> OnLogDiscovered;

        /// <summary>
        /// Вызывается при начале воспроизведения.
        /// AudioLogData: данные лога.
        /// </summary>
        public static event Action<AudioLogData> OnLogPlaybackStarted;

        /// <summary>
        /// Вызывается при остановке воспроизведения (прерывание).
        /// string: logId
        /// </summary>
        public static event Action<string> OnLogPlaybackStopped;

        /// <summary>
        /// Вызывается при завершении воспроизведения (естественный конец).
        /// string: logId
        /// </summary>
        public static event Action<string> OnLogPlaybackCompleted;

        public static void RaiseLogDiscovered(string logId)
        {
            if (string.IsNullOrEmpty(logId)) return;
            OnLogDiscovered?.Invoke(logId);
        }

        public static void RaisePlaybackStarted(AudioLogData data)
        {
            if (data == null) return;
            OnLogPlaybackStarted?.Invoke(data);
        }

        public static void RaisePlaybackStopped(string logId)
        {
            if (string.IsNullOrEmpty(logId)) return;
            OnLogPlaybackStopped?.Invoke(logId);
        }

        public static void RaisePlaybackCompleted(string logId)
        {
            if (string.IsNullOrEmpty(logId)) return;
            OnLogPlaybackCompleted?.Invoke(logId);
        }
    }
}
