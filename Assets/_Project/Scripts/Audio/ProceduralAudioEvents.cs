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
        /// <param name="startSampleFrame">Exact output-sample frame where the ping starts.</param>
        /// <param name="sampleRate">Audio output sample rate used to resolve the frame timestamp.</param>
        /// <param name="intensity">Normalized ping intensity in the 0..1 range.</param>
        /// <param name="chirpDurationSeconds">Audible chirp duration in seconds.</param>
        public AudioPingTriggerInfo(long startSampleFrame, int sampleRate, float intensity, float chirpDurationSeconds)
        {
            StartSampleFrame = startSampleFrame;
            SampleRate = sampleRate > 0 ? sampleRate : 1;
            Intensity = intensity;
            ChirpDurationSeconds = chirpDurationSeconds;
        }

        /// <summary>Exact output-sample frame where the ping started rendering.</summary>
        public long StartSampleFrame { get; }

        /// <summary>Audio output sample rate used to resolve the frame timestamp.</summary>
        public int SampleRate { get; }

        /// <summary>Exact start time in seconds derived from the sample-frame clock.</summary>
        public double StartTimeSeconds => StartSampleFrame / (double)SampleRate;

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
        /// Consumers must use the provided sample-frame timing for synchronization, not <see cref="Time.time"/>.
        /// </summary>
        public static event Action<AudioPingTriggerInfo> OnAudioPingTriggered;

        /// <summary>
        /// Raises the sample-accurate sonar-ping notification on the main thread.
        /// </summary>
        /// <param name="startSampleFrame">Exact output-sample frame where the ping starts.</param>
        /// <param name="sampleRate">Audio output sample rate used to resolve the frame timestamp.</param>
        /// <param name="intensity">Normalized ping intensity in the 0..1 range.</param>
        /// <param name="chirpDurationSeconds">Primary chirp duration in seconds.</param>
        public static void RaiseAudioPingTriggered(long startSampleFrame, int sampleRate, float intensity, float chirpDurationSeconds)
        {
            OnAudioPingTriggered?.Invoke(new AudioPingTriggerInfo(startSampleFrame, sampleRate, intensity, chirpDurationSeconds));
        }
    }
}
