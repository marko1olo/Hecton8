// ============================================================================
// HECTON-8 — AtlasSignalEvents.cs
// Статическая шина событий сигнала Атлас-6.
//
// Лор: пульс ядра Атлас-6 повторяется каждые 11:23 (683 секунды).
// Ритм — не случайность: время перебора всех вариантов "спасения колонии".
// Чем ближе к ядру — тем яснее "содержание" сигнала.
// ============================================================================

using System;
using UnityEngine;

namespace Hecton8.AtlasSignal
{
    public static class AtlasSignalEvents
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnSignalPulse = null;
            OnSignalDetected = null;
            OnSignalStrengthChanged = null;
            OnSignalDecoded = null;
        }

        /// <summary>
        /// Пульс сигнала. Вызывается каждые 683 секунды.
        /// float: интенсивность [0..1] (зависит от расстояния до ядра).
        /// </summary>
        public static event Action<float> OnSignalPulse;

        /// <summary>
        /// Сигнал впервые обнаружен сканером.
        /// Vector3: мировая позиция источника (приблизительная).
        /// </summary>
        public static event Action<Vector3> OnSignalDetected;

        /// <summary>
        /// Изменилась сила сигнала (при движении игрока).
        /// float: сила [0..1].
        /// </summary>
        public static event Action<float> OnSignalStrengthChanged;

        /// <summary>
        /// Сигнал расшифрован (игрок достиг ядра).
        /// string: decoded message ID.
        /// </summary>
        public static event Action<string> OnSignalDecoded;

        public static void RaisePulse(float intensity)
            => OnSignalPulse?.Invoke(intensity);

        public static void RaiseDetected(Vector3 sourcePos)
            => OnSignalDetected?.Invoke(sourcePos);

        public static void RaiseStrengthChanged(float strength)
            => OnSignalStrengthChanged?.Invoke(strength);

        public static void RaiseDecoded(string messageId)
        {
            if (!string.IsNullOrEmpty(messageId))
                OnSignalDecoded?.Invoke(messageId);
        }
    }
}
