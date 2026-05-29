using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Data-only contract describing how an active transport should reshape player feel layers.
    /// </summary>
    /// <remarks>
    /// This owner does not drive locomotion and does not own battery logic.
    /// It only supplies normalized tuning for presentation and audio consumers.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Player Transport Feel Contract")]
    public sealed class PlayerTransportFeelContract : MonoBehaviour, IPoolable
    {
        private static PlayerTransportFeelContract s_lastSpawnedContract;

        [Header("-- Preset ---------------------------")]
        [Tooltip("Optional shared transport preset. When assigned, feel values resolve from the preset instead of local inspector overrides.")]
        [SerializeField] private PlayerTransportPreset preset;

        [Header("-- Transport Normalization -----------")]
        [Tooltip("Propulsion force treated as full transport output for feel normalization.")]
        [SerializeField, Range(50f, 2000f)] private float propulsionForceReference = 800f;

        [Header("-- Swim Presentation ----------------")]
        [Tooltip("Minimum propulsion floor injected while active transport is pulling the player.")]
        [SerializeField, Range(0f, 1f)] private float swimPropulsionFloor = 0.62f;

        [Tooltip("Stroke cadence multiplier while transport is doing part of the work.")]
        [SerializeField, Range(0.2f, 1f)] private float swimCadenceMultiplier = 0.74f;

        [Tooltip("Pose lag multiplier while transport is pulling the player forward.")]
        [SerializeField, Range(0.5f, 1.2f)] private float swimPoseLagMultiplier = 0.9f;

        [Tooltip("Root presentation weight restored by active transport.")]
        [SerializeField, Range(0f, 1f)] private float swimRootPresentationWeight = 0.58f;

        [Tooltip("Support-hand presentation weight restored by active transport.")]
        [SerializeField, Range(0f, 1f)] private float swimSupportHandWeight = 0.84f;

        [Tooltip("Forward root bias applied while transport is active.")]
        [SerializeField, Range(0f, 0.06f)] private float swimRootForwardBias = 0.018f;

        [Tooltip("Forward guide-hand bias applied while transport is active.")]
        [SerializeField, Range(0f, 0.08f)] private float swimGuideForwardBias = 0.028f;

        [Header("-- Occupancy Overrides -------------")]
        [Tooltip("Player enclosure class used by downstream transport consumers.")]
        [SerializeField] private PlayerTransportOccupancyMode occupancyMode = PlayerTransportOccupancyMode.Handheld;

        [Tooltip("How much of the normal swim-body presentation should remain while this transport is owned.")]
        [SerializeField, Range(0f, 1f)] private float swimPresentationScale = 1f;

        [Tooltip("How much of the normal swim thruster loop should remain while this transport is owned.")]
        [SerializeField, Range(0f, 1f)] private float thrusterAudioScale = 1f;

        [Tooltip("How much camera motion this transport should preserve for future camera consumers.")]
        [SerializeField, Range(0f, 1f)] private float cameraMotionScale = 1f;

        [Header("-- Audio ----------------------------")]
        [Tooltip("Minimum speed floor kept alive while transport is active.")]
        [SerializeField, Range(0f, 1f)] private float audioIdleSpeedFloor = 0.42f;

        [Tooltip("Extra audio volume added by active transport thrust.")]
        [SerializeField, Range(0f, 0.6f)] private float audioVolumeBoost = 0.18f;

        [Tooltip("Extra audio pitch added by active transport thrust.")]
        [SerializeField, Range(0f, 0.8f)] private float audioPitchBoost = 0.22f;

        [Tooltip("Minimum swim-mode blend kept alive while transport is active.")]
        [SerializeField, Range(0f, 1f)] private float audioModeBlendFloor = 0.35f;

        /// <summary>Optional shared preset driving this transport feel contract.</summary>
        public PlayerTransportPreset Preset => preset;

        internal static bool TryResolveLastSpawned(GameObject instance, out PlayerTransportFeelContract contract)
        {
            contract = s_lastSpawnedContract;
            return contract != null && ReferenceEquals(contract.gameObject, instance);
        }

        public void OnSpawn()
        {
            s_lastSpawnedContract = this;
        }

        public void OnDespawn()
        {
            if (ReferenceEquals(s_lastSpawnedContract, this))
                s_lastSpawnedContract = null;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(s_lastSpawnedContract, this))
                s_lastSpawnedContract = null;
        }

        /// <summary>Propulsion force treated as full transport output for feel normalization.</summary>
        public float PropulsionForceReference => preset != null ? preset.PropulsionForceReference : propulsionForceReference;

        /// <summary>Minimum propulsion floor injected while active transport is pulling the player.</summary>
        public float SwimPropulsionFloor => preset != null ? preset.SwimPropulsionFloor : swimPropulsionFloor;

        /// <summary>Stroke cadence multiplier while transport is doing part of the work.</summary>
        public float SwimCadenceMultiplier => preset != null ? preset.SwimCadenceMultiplier : swimCadenceMultiplier;

        /// <summary>Pose lag multiplier while transport is pulling the player forward.</summary>
        public float SwimPoseLagMultiplier => preset != null ? preset.SwimPoseLagMultiplier : swimPoseLagMultiplier;

        /// <summary>Root presentation weight restored by active transport.</summary>
        public float SwimRootPresentationWeight => preset != null ? preset.SwimRootPresentationWeight : swimRootPresentationWeight;

        /// <summary>Support-hand presentation weight restored by active transport.</summary>
        public float SwimSupportHandWeight => preset != null ? preset.SwimSupportHandWeight : swimSupportHandWeight;

        /// <summary>Forward root bias applied while transport is active.</summary>
        public float SwimRootForwardBias => preset != null ? preset.SwimRootForwardBias : swimRootForwardBias;

        /// <summary>Forward guide-hand bias applied while transport is active.</summary>
        public float SwimGuideForwardBias => preset != null ? preset.SwimGuideForwardBias : swimGuideForwardBias;

        /// <summary>Player enclosure class used by downstream transport consumers.</summary>
        public PlayerTransportOccupancyMode OccupancyMode => preset != null ? preset.OccupancyMode : occupancyMode;

        /// <summary>How much of the normal swim-body presentation should remain while this transport is owned.</summary>
        public float SwimPresentationScale => preset != null ? preset.SwimPresentationScale : swimPresentationScale;

        /// <summary>How much of the normal swim thruster loop should remain while this transport is owned.</summary>
        public float ThrusterAudioScale => preset != null ? preset.ThrusterAudioScale : thrusterAudioScale;

        /// <summary>How much camera motion this transport should preserve for future camera consumers.</summary>
        public float CameraMotionScale => preset != null ? preset.CameraMotionScale : cameraMotionScale;

        /// <summary>Minimum speed floor kept alive while transport is active.</summary>
        public float AudioIdleSpeedFloor => preset != null ? preset.AudioIdleSpeedFloor : audioIdleSpeedFloor;

        /// <summary>Extra audio volume added by active transport thrust.</summary>
        public float AudioVolumeBoost => preset != null ? preset.AudioVolumeBoost : audioVolumeBoost;

        /// <summary>Extra audio pitch added by active transport thrust.</summary>
        public float AudioPitchBoost => preset != null ? preset.AudioPitchBoost : audioPitchBoost;

        /// <summary>Minimum swim-mode blend kept alive while transport is active.</summary>
        public float AudioModeBlendFloor => preset != null ? preset.AudioModeBlendFloor : audioModeBlendFloor;

        internal void BindPreset(PlayerTransportPreset transportPreset)
        {
            if (transportPreset == null || ReferenceEquals(preset, transportPreset))
                return;

            preset = transportPreset;
        }
    }
}
