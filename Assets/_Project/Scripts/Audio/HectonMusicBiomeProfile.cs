using System;
using UnityEngine;

namespace Hecton8.Audio
{
    /// <summary>
    /// Weighted foreign-profile bleed configuration used to keep exploration music varied.
    /// </summary>
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct HectonMusicProfileBlend
    {
        [Tooltip("Secondary profile that can inject cues into the current biome.")]
        [SerializeField] private HectonMusicBiomeProfile _profile;

        [Tooltip("Relative selection weight when this bleed source is eligible.")]
        [SerializeField, Min(1)] private int _weight;

        /// <summary>
        /// Secondary profile reference.
        /// </summary>
        public HectonMusicBiomeProfile Profile => _profile;

        /// <summary>
        /// Relative selection weight.
        /// </summary>
        public int Weight => _weight > 0 ? _weight : 1;
    }

    /// <summary>
    /// Music configuration container for one runtime tone profile.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MusicProfile_",
        menuName = "Hecton8/Audio/Music Biome Profile",
        order = 130)]
    public sealed class HectonMusicBiomeProfile : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable runtime id.")]
        [SerializeField] private string _profileId = "music.profile.generic";

        [Tooltip("Inspector-facing label.")]
        [SerializeField] private string _profileLabel = "Generic Music Profile";

        [Header("Playback")]
        [Tooltip("Minimum silence between completed tracks, in seconds.")]
        [SerializeField, Min(0f)] private float _minPauseSeconds = 45f;

        [Tooltip("Maximum silence between completed tracks, in seconds.")]
        [SerializeField, Min(0f)] private float _maxPauseSeconds = 120f;

        [Tooltip("Default fade-in duration for new bed tracks.")]
        [SerializeField, Min(0.01f)] private float _fadeInSeconds = 1.75f;

        [Tooltip("Default fade-out duration when a track ends naturally.")]
        [SerializeField, Min(0.01f)] private float _fadeOutSeconds = 2.5f;

        [Tooltip("Default crossfade duration when the active context changes.")]
        [SerializeField, Min(0.01f)] private float _crossfadeSeconds = 2.25f;

        [Tooltip("Chance that the director uses a short bridge clip instead of a long bed when available.")]
        [SerializeField, Range(0f, 1f)] private float _shortTrackChance = 0.18f;

        [Tooltip("Cooldown before another short-form bridge clip may be selected.")]
        [SerializeField, Min(0f)] private float _shortTrackCooldownSeconds = 75f;

        [Header("Primary Weight")]
        [Tooltip("Relative weight for this profile's own calm pool when calm bleed sources are also eligible.")]
        [SerializeField, Min(1)] private int _localCalmWeight = 60;

        [Tooltip("Relative weight for this profile's own tense pool when tense bleed sources are also eligible.")]
        [SerializeField, Min(1)] private int _localTenseWeight = 60;

        [Header("Cross-Tension Mixing")]
        [Tooltip("Allows this profile to opportunistically borrow from the opposite tension pool even when current runtime tension does not request it.")]
        [SerializeField] private bool _allowCrossTensionMix;

        [Tooltip("Chance to borrow from the opposite tension pool when both sides have valid clips.")]
        [SerializeField, Range(0f, 1f)] private float _crossTensionMixChance;

        [Header("Variety")]
        [Tooltip("How many recently played long-form clips should be excluded from selection when alternatives exist.")]
        [SerializeField, Range(1, 4)] private int _longRepeatHorizon = 1;

        [Tooltip("How many recently played short-form clips should be excluded from selection when alternatives exist.")]
        [SerializeField, Range(1, 3)] private int _shortRepeatHorizon = 1;

        [Header("Primary Pools")]
        [Tooltip("Calm long-form exploration beds.")]
        [SerializeField] private HectonMusicClip[] _calmLongTracks;

        [Tooltip("Tense long-form exploration beds.")]
        [SerializeField] private HectonMusicClip[] _tenseLongTracks;

        [Tooltip("Calm short-form bridge clips.")]
        [SerializeField] private HectonMusicClip[] _calmShortTracks;

        [Tooltip("Tense short-form bridge clips.")]
        [SerializeField] private HectonMusicClip[] _tenseShortTracks;

        [Header("Stingers")]
        [Tooltip("Discovery overlays. These play over the current bed and duck it.")]
        [SerializeField] private HectonMusicClip[] _discoveryStingers;

        [Tooltip("Danger overlays. These can precede combat escalation.")]
        [SerializeField] private HectonMusicClip[] _dangerStingers;

        [Tooltip("Recovery / relief overlays after tension drops.")]
        [SerializeField] private HectonMusicClip[] _recoveryStingers;

        [Header("Bleed Sources")]
        [Tooltip("Foreign calm profiles that may inject long or short calm cues into this biome.")]
        [SerializeField] private HectonMusicProfileBlend[] _calmBleedProfiles;

        [Tooltip("Foreign tense profiles that may inject long or short tense cues into this biome.")]
        [SerializeField] private HectonMusicProfileBlend[] _tenseBleedProfiles;

        /// <summary>
        /// Stable runtime id.
        /// </summary>
        public string ProfileId => _profileId;

        /// <summary>
        /// Inspector-facing label.
        /// </summary>
        public string ProfileLabel => _profileLabel;

        /// <summary>
        /// Minimum silence between tracks, in seconds.
        /// </summary>
        public float MinPauseSeconds => _minPauseSeconds;

        /// <summary>
        /// Maximum silence between tracks, in seconds.
        /// </summary>
        public float MaxPauseSeconds => _maxPauseSeconds >= _minPauseSeconds ? _maxPauseSeconds : _minPauseSeconds;

        /// <summary>
        /// Default fade-in duration.
        /// </summary>
        public float FadeInSeconds => _fadeInSeconds;

        /// <summary>
        /// Default fade-out duration.
        /// </summary>
        public float FadeOutSeconds => _fadeOutSeconds;

        /// <summary>
        /// Default crossfade duration.
        /// </summary>
        public float CrossfadeSeconds => _crossfadeSeconds;

        /// <summary>
        /// Short-form bridge clip chance.
        /// </summary>
        public float ShortTrackChance => _shortTrackChance;

        /// <summary>
        /// Short-form bridge clip cooldown.
        /// </summary>
        public float ShortTrackCooldownSeconds => _shortTrackCooldownSeconds;

        /// <summary>
        /// Local calm-pool source weight.
        /// </summary>
        public int LocalCalmWeight => _localCalmWeight > 0 ? _localCalmWeight : 1;

        /// <summary>
        /// Local tense-pool source weight.
        /// </summary>
        public int LocalTenseWeight => _localTenseWeight > 0 ? _localTenseWeight : 1;

        /// <summary>
        /// True when this profile may opportunistically borrow from the opposite tension pool.
        /// </summary>
        public bool AllowCrossTensionMix => _allowCrossTensionMix;

        /// <summary>
        /// Chance to borrow from the opposite tension pool.
        /// </summary>
        public float CrossTensionMixChance => _crossTensionMixChance;

        /// <summary>
        /// Recent-history exclusion horizon for long-form clips.
        /// </summary>
        public int LongRepeatHorizon => _longRepeatHorizon;

        /// <summary>
        /// Recent-history exclusion horizon for short-form clips.
        /// </summary>
        public int ShortRepeatHorizon => _shortRepeatHorizon;

        /// <summary>
        /// Calm long-form bed pool.
        /// </summary>
        public HectonMusicClip[] CalmLongTracks => _calmLongTracks;

        /// <summary>
        /// Tense long-form bed pool.
        /// </summary>
        public HectonMusicClip[] TenseLongTracks => _tenseLongTracks;

        /// <summary>
        /// Calm short-form bridge pool.
        /// </summary>
        public HectonMusicClip[] CalmShortTracks => _calmShortTracks;

        /// <summary>
        /// Tense short-form bridge pool.
        /// </summary>
        public HectonMusicClip[] TenseShortTracks => _tenseShortTracks;

        /// <summary>
        /// Discovery stinger pool.
        /// </summary>
        public HectonMusicClip[] DiscoveryStingers => _discoveryStingers;

        /// <summary>
        /// Danger stinger pool.
        /// </summary>
        public HectonMusicClip[] DangerStingers => _dangerStingers;

        /// <summary>
        /// Recovery stinger pool.
        /// </summary>
        public HectonMusicClip[] RecoveryStingers => _recoveryStingers;

        /// <summary>
        /// Calm foreign-bleed sources.
        /// </summary>
        public HectonMusicProfileBlend[] CalmBleedProfiles => _calmBleedProfiles;

        /// <summary>
        /// Tense foreign-bleed sources.
        /// </summary>
        public HectonMusicProfileBlend[] TenseBleedProfiles => _tenseBleedProfiles;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_minPauseSeconds < 0f)
                _minPauseSeconds = 0f;

            if (_maxPauseSeconds < _minPauseSeconds)
                _maxPauseSeconds = _minPauseSeconds;

            if (_fadeInSeconds < 0.01f)
                _fadeInSeconds = 0.01f;

            if (_fadeOutSeconds < 0.01f)
                _fadeOutSeconds = 0.01f;

            if (_crossfadeSeconds < 0.01f)
                _crossfadeSeconds = 0.01f;

            if (_shortTrackCooldownSeconds < 0f)
                _shortTrackCooldownSeconds = 0f;

            if (_localCalmWeight < 1)
                _localCalmWeight = 1;

            if (_localTenseWeight < 1)
                _localTenseWeight = 1;

            if (_crossTensionMixChance < 0f)
                _crossTensionMixChance = 0f;
            else if (_crossTensionMixChance > 1f)
                _crossTensionMixChance = 1f;

            if (_longRepeatHorizon < 1)
                _longRepeatHorizon = 1;
            else if (_longRepeatHorizon > 4)
                _longRepeatHorizon = 4;

            if (_shortRepeatHorizon < 1)
                _shortRepeatHorizon = 1;
            else if (_shortRepeatHorizon > 3)
                _shortRepeatHorizon = 3;
        }
#endif
    }
}
