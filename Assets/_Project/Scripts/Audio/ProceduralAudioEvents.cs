using System;
using UnityEngine;

namespace Hecton8.Audio
{
    /// <summary>
    /// Zero-allocation payload for sample-accurate procedural audio triggers.
    /// </summary>
    public readonly struct AudioPingTriggerInfo
    {
        /// <summary>
        /// Creates a new audio ping trigger payload.
        /// </summary>
        /// <param name="dspTime">Exact audio-system start time for the ping.</param>
        /// <param name="intensity">Normalized ping intensity in the 0..1 range.</param>
        /// <param name="chirpDurationSeconds">Audible chirp duration in seconds.</param>
        public AudioPingTriggerInfo(double dspTime, float intensity, float chirpDurationSeconds)
        {
            DspTime = dspTime;
            Intensity = intensity;
            ChirpDurationSeconds = chirpDurationSeconds;
        }

        /// <summary>Exact audio-system time when the ping started rendering.</summary>
        public double DspTime { get; }

        /// <summary>Normalized ping intensity in the 0..1 range.</summary>
        public float Intensity { get; }

        /// <summary>Primary chirp duration in seconds.</summary>
        public float ChirpDurationSeconds { get; }
    }

    /// <summary>
    /// Main-thread event bridge for sample-accurate procedural audio triggers.
    /// </summary>
    public static class ProceduralAudioEvents
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnAudioPingTriggered = null;
        }

        /// <summary>
        /// Raised on the main thread after the audio renderer starts a sonar ping.
        /// Consumers must use the provided DSP time for synchronization, not <see cref="Time.time"/>.
        /// </summary>
        public static event Action<AudioPingTriggerInfo> OnAudioPingTriggered;

        /// <summary>
        /// Raises the sample-accurate sonar-ping notification on the main thread.
        /// </summary>
        /// <param name="dspTime">Exact audio-system start time for the ping.</param>
        /// <param name="intensity">Normalized ping intensity in the 0..1 range.</param>
        /// <param name="chirpDurationSeconds">Primary chirp duration in seconds.</param>
        public static void RaiseAudioPingTriggered(double dspTime, float intensity, float chirpDurationSeconds)
        {
            OnAudioPingTriggered?.Invoke(new AudioPingTriggerInfo(dspTime, intensity, chirpDurationSeconds));
        }
    }
}
