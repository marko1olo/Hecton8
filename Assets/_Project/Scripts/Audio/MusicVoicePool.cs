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

        // COLD ALLOC: bool[voiceCount] - runtime availability flags for authored music voices - owner: MusicVoicePool
        private bool[] _voiceAvailable;
        private bool _stingerAvailable = true;

        /// <summary>
        /// Number of authored bed voices available for runtime binding.
        /// </summary>
        internal int VoiceCount => _musicVoices != null ? _musicVoices.Length : 0;

        /// <summary>
        /// Authored stinger voice.
        /// </summary>
        internal AudioSource StingerSource => _stingerSource;

        private void Awake()
        {
            ResetRuntimeAvailability();
        }

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
        /// Resets runtime availability bookkeeping for authored voices.
        /// </summary>
        internal void ResetRuntimeAvailability()
        {
            int voiceCount = _musicVoices != null ? _musicVoices.Length : 0;
            if (_voiceAvailable == null || _voiceAvailable.Length != voiceCount)
                _voiceAvailable = new bool[voiceCount]; // COLD ALLOC: bool[voiceCount] - runtime availability flags for authored music voices - owner: MusicVoicePool

            for (int i = 0; i < voiceCount; i++)
                _voiceAvailable[i] = _musicVoices[i] != null;

            _stingerAvailable = _stingerSource != null;
        }

        /// <summary>
        /// Marks one authored voice as currently in use by the music director.
        /// </summary>
        internal void MarkVoiceInUse(int index)
        {
            if (_voiceAvailable == null || index < 0 || index >= _voiceAvailable.Length)
                return;

            _voiceAvailable[index] = false;
        }

        /// <summary>
        /// Stops, clears, and returns one authored voice to the available pool.
        /// </summary>
        internal void ReleaseMusicVoice(int index)
        {
            if (_musicVoices == null || index < 0 || index >= _musicVoices.Length)
                return;

            AudioSource source = _musicVoices[index];
            if (source != null)
            {
                source.Stop();
                source.clip = null;
                source.volume = 0f;
                source.loop = false;
            }

            if (_voiceAvailable != null && index < _voiceAvailable.Length)
                _voiceAvailable[index] = source != null;
        }

        /// <summary>
        /// Stops, clears, and returns the stinger voice to the available state.
        /// </summary>
        internal void ReleaseStingerVoice()
        {
            if (_stingerSource != null)
            {
                _stingerSource.Stop();
                _stingerSource.clip = null;
                _stingerSource.volume = 0f;
                _stingerSource.loop = false;
            }

            _stingerAvailable = _stingerSource != null;
        }

        /// <summary>
        /// Marks the authored stinger voice as currently in use.
        /// </summary>
        internal void MarkStingerInUse()
        {
            _stingerAvailable = false;
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

            ResetRuntimeAvailability();
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
