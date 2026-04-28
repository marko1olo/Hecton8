using UnityEngine;
using UnityEngine.Audio;

namespace Hecton8.Audio
{
    /// <summary>
    /// Authored runtime voice owner for Hecton music playback.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MusicVoicePool : MonoBehaviour
    {
        [Header("Voices")]
        [Tooltip("Authored bed voices used by HectonMusicDirector. Order is stable and must match the director's fixed voice slots.")]
        [SerializeField] private AudioSource[] _musicVoices;

        [Tooltip("Authored one-shot stinger voice used by HectonMusicDirector.")]
        [SerializeField] private AudioSource _stingerSource;

        /// <summary>
        /// Number of authored bed voices available for runtime binding.
        /// </summary>
        internal int VoiceCount => _musicVoices != null ? _musicVoices.Length : 0;

        /// <summary>
        /// Authored stinger voice.
        /// </summary>
        internal AudioSource StingerSource => _stingerSource;

        /// <summary>
        /// Tries to resolve one authored bed voice by stable slot index.
        /// </summary>
        internal bool TryGetMusicVoice(int index, out AudioSource source)
        {
            source = null;
            if (_musicVoices == null || index < 0 || index >= _musicVoices.Length)
                return false;

            source = _musicVoices[index];
            return source != null;
        }

        /// <summary>
        /// Applies mixer routing and stable runtime defaults to all authored voices.
        /// </summary>
        internal void ApplyRuntimeRouting(AudioMixerGroup musicMixerGroup, AudioMixerGroup stingerMixerGroup)
        {
            if (_musicVoices != null)
            {
                for (int i = 0; i < _musicVoices.Length; i++)
                {
                    AudioSource voice = _musicVoices[i];
                    if (voice == null)
                        continue;

                    ConfigureVoiceSource(voice, musicMixerGroup, 48);
                }
            }

            if (_stingerSource != null)
                ConfigureVoiceSource(_stingerSource, stingerMixerGroup, 32);
        }

        private static void ConfigureVoiceSource(AudioSource source, AudioMixerGroup mixerGroup, int priority)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.spread = 0f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.priority = priority;
            source.outputAudioMixerGroup = mixerGroup;
        }
    }
}
