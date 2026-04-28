using UnityEngine;
using UnityEngine.Audio;

namespace Hecton8.Audio
{
    /// <summary>
    /// Global authored profile routing for the music director.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MusicDirectorConfig_",
        menuName = "Hecton8/Audio/Music Director Config",
        order = 131)]
    public sealed class HectonMusicDirectorConfig : ScriptableObject
    {
        [Header("Profiles")]
        [Tooltip("Profile used in the main menu scene.")]
        [SerializeField] private HectonMusicBiomeProfile _mainMenuProfile;

        [Tooltip("Profile used in a prologue scene.")]
        [SerializeField] private HectonMusicBiomeProfile _prologueProfile;

        [Tooltip("Profile used for shallow water.")]
        [SerializeField] private HectonMusicBiomeProfile _shallowProfile;

        [Tooltip("Profile used for shelf and mid-depth water.")]
        [SerializeField] private HectonMusicBiomeProfile _shelfProfile;

        [Tooltip("Profile used for abyssal water.")]
        [SerializeField] private HectonMusicBiomeProfile _abyssProfile;

        [Tooltip("Profile used for cave contexts.")]
        [SerializeField] private HectonMusicBiomeProfile _caveProfile;

        [Tooltip("Profile used for thermal and vent contexts.")]
        [SerializeField] private HectonMusicBiomeProfile _thermalProfile;

        [Tooltip("Profile used for interior and safe base contexts.")]
        [SerializeField] private HectonMusicBiomeProfile _baseProfile;

        [Tooltip("Profile used for combat escalation.")]
        [SerializeField] private HectonMusicBiomeProfile _combatProfile;

        [Tooltip("Fallback profile used when no routing context resolves.")]
        [SerializeField] private HectonMusicBiomeProfile _fallbackProfile;

        [Header("Mixer Routing")]
        [Tooltip("Optional dedicated mixer group for bed music.")]
        [SerializeField] private AudioMixerGroup _musicMixerGroup;

        [Tooltip("Optional dedicated mixer group for stingers.")]
        [SerializeField] private AudioMixerGroup _stingerMixerGroup;

        [Header("Runtime Ownership")]
        [Tooltip("Authored runtime director prefab containing HectonMusicDirector, MusicVoicePool, and pre-authored AudioSource children.")]
        [SerializeField] private HectonMusicDirector _runtimeDirectorPrefab;

        /// <summary>
        /// Profile used in the main menu scene.
        /// </summary>
        public HectonMusicBiomeProfile MainMenuProfile => _mainMenuProfile;

        /// <summary>
        /// Profile used in a prologue scene.
        /// </summary>
        public HectonMusicBiomeProfile PrologueProfile => _prologueProfile;

        /// <summary>
        /// Profile used for shallow water.
        /// </summary>
        public HectonMusicBiomeProfile ShallowProfile => _shallowProfile;

        /// <summary>
        /// Profile used for shelf and mid-depth water.
        /// </summary>
        public HectonMusicBiomeProfile ShelfProfile => _shelfProfile;

        /// <summary>
        /// Profile used for abyssal water.
        /// </summary>
        public HectonMusicBiomeProfile AbyssProfile => _abyssProfile;

        /// <summary>
        /// Profile used for cave contexts.
        /// </summary>
        public HectonMusicBiomeProfile CaveProfile => _caveProfile;

        /// <summary>
        /// Profile used for thermal and vent contexts.
        /// </summary>
        public HectonMusicBiomeProfile ThermalProfile => _thermalProfile;

        /// <summary>
        /// Profile used for interior and safe base contexts.
        /// </summary>
        public HectonMusicBiomeProfile BaseProfile => _baseProfile;

        /// <summary>
        /// Profile used for combat escalation.
        /// </summary>
        public HectonMusicBiomeProfile CombatProfile => _combatProfile;

        /// <summary>
        /// Fallback profile used when no routing context resolves.
        /// </summary>
        public HectonMusicBiomeProfile FallbackProfile => _fallbackProfile;

        /// <summary>
        /// Optional dedicated mixer group for bed music.
        /// </summary>
        public AudioMixerGroup MusicMixerGroup => _musicMixerGroup;

        /// <summary>
        /// Optional dedicated mixer group for stingers.
        /// </summary>
        public AudioMixerGroup StingerMixerGroup => _stingerMixerGroup;

        /// <summary>
        /// Authored runtime director prefab used instead of constructing voices at runtime.
        /// </summary>
        public HectonMusicDirector RuntimeDirectorPrefab => _runtimeDirectorPrefab;
    }
}
