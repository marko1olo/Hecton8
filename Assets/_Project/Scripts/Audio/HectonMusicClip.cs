using System;
using UnityEngine;

namespace Hecton8.Audio
{
    /// <summary>
    /// High-level role for a music cue.
    /// </summary>
    public enum HectonMusicClipRole : byte
    {
        ExplorationLong = 0,
        ExplorationShort = 1,
        CombatLong = 2,
        CombatShort = 3,
        DiscoveryStinger = 4,
        DangerStinger = 5,
        RecoveryStinger = 6,
        Menu = 7,
        Prologue = 8,
        Override = 9
    }

    /// <summary>
    /// Serialized music cue entry used by biome profiles and the music director.
    /// </summary>
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct HectonMusicClip
    {
        [Header("Identity")]
        [Tooltip("Stable cue id for diagnostics and inspector readability.")]
        [SerializeField] private string _cueId;

        [Tooltip("Audio clip to play.")]
        [SerializeField] private AudioClip _clip;

        [Header("Mix")]
        [Tooltip("Per-clip output multiplier used to compensate loudness mismatches.")]
        [SerializeField, Range(0f, 1f)] private float _volume;

        [Tooltip("Selection weight inside the owning pool. Higher value means more likely.")]
        [SerializeField, Min(1)] private int _weight;

        [Header("Classification")]
        [Tooltip("Role classification used by tooling and runtime diagnostics.")]
        [SerializeField] private HectonMusicClipRole _role;

        [Tooltip("Designer-side tension hint. 0 = calm, 1 = danger.")]
        [SerializeField, Range(0f, 1f)] private float _tension;

        /// <summary>
        /// Stable cue id for diagnostics.
        /// </summary>
        public string CueId => _cueId;

        /// <summary>
        /// Clip reference.
        /// </summary>
        public AudioClip Clip => _clip;

        /// <summary>
        /// Per-clip volume multiplier.
        /// </summary>
        public float Volume => _volume > 0f ? _volume : 1f;

        /// <summary>
        /// Weighted selection value.
        /// </summary>
        public int Weight => _weight > 0 ? _weight : 1;

        /// <summary>
        /// High-level cue role.
        /// </summary>
        public HectonMusicClipRole Role => _role;

        /// <summary>
        /// Designer-side tension hint.
        /// </summary>
        public float Tension => _tension;

        /// <summary>
        /// True when the entry contains a valid clip.
        /// </summary>
        public bool IsValid => _clip != null;
    }
}
