using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Scriptable preset for scalable player transport setup.
    /// </summary>
    [CreateAssetMenu(fileName = "TransportPreset_", menuName = "Hecton8/Gameplay/Transport Preset")]
    public sealed class PlayerTransportPreset : ScriptableObject
    {
        private const float MinimumSurvivalShieldingScale = 0.1f;
        private const float MinimumPressureDamageTransferScale = 0.25f;

        [Header("-- Identity -------------------------")]
        [Tooltip("Display label used by transport HUD/prompts when no override text is provided.")]
        [SerializeField] private string transportName = "Transport";

        [Tooltip("Prompt shown when the player can mount this transport.")]
        [SerializeField] private string mountInteractText = "Board Transport";

        [Tooltip("Prompt shown while this transport is already mounted.")]
        [SerializeField] private string dismountInteractText = "Dismount";

        [Header("-- Runtime Rules --------------------")]
        [Tooltip("If enabled, this transport only outputs propulsion while the rider is underwater swimming.")]
        [SerializeField] private bool underwaterOnly = true;

        [Tooltip("If enabled, leaving underwater swim instantly drops this transport out of active drive.")]
        [SerializeField] private bool autoDropDriveOutsideWater = true;

        [Tooltip("If enabled, the rider's currently held tool is holstered on mount.")]
        [SerializeField] private bool holsterToolOnMount = true;

        [Header("-- Locomotion -----------------------")]
        [Tooltip("Maximum propulsion force injected into HectonPlayerMovement at full throttle.")]
#if UNITY_EDITOR
        [MinValue(50d)]
        [ValidateInput(nameof(IsFinitePositive), "Propulsion Force must be finite and greater than zero.")]
#endif
        [SerializeField, Range(50f, 3000f)] private float propulsionForce = 1100f;

        [Tooltip("Maximum swim speed multiplier injected into HectonPlayerMovement at full throttle.")]
        [SerializeField, Range(1f, 6f)] private float speedMultiplier = 2.9f;

        [Tooltip("Minimum rider input required before the transport commits to active drive.")]
        [SerializeField, Range(0f, 1f)] private float activationInputThreshold = 0.12f;

        [Tooltip("Passive cruise throttle kept alive even when the rider releases directional input.")]
        [SerializeField, Range(0f, 1f)] private float idleCruiseFactor = 0f;

        [Tooltip("Suit energy drained per second at full throttle.")]
        [SerializeField, Range(0f, 20f)] private float energyDrainPerSecond = 0f;

        [Header("-- Control Shaping -----------------")]
        [Tooltip("How strongly forward drive follows camera pitch. 0 = planar glide, 1 = full swim-style pitch steering.")]
        [SerializeField, Range(0f, 1f)] private float forwardPitchInfluence = 1f;

        [Tooltip("Horizontal strafe authority applied to rider input while this transport is active.")]
        [SerializeField, Range(0f, 1.5f)] private float strafeInputScale = 1f;

        [Tooltip("Vertical ascend/descend authority applied to rider input while this transport is active.")]
        [SerializeField, Range(0f, 1.5f)] private float verticalInputScale = 1f;

        [Tooltip("Reverse thrust authority applied when the rider pulls backward input.")]
        [SerializeField, Range(0f, 1.5f)] private float reverseThrustScale = 1f;

        [Tooltip("How quickly rider body yaw catches camera yaw while this transport is active.")]
        [SerializeField, Range(0.1f, 1.5f)] private float bodyYawResponsivenessScale = 1f;

        [Tooltip("Multiplier applied to surface dive breakthrough assistance for this transport.")]
        [SerializeField, Range(0f, 2f)] private float surfaceDiveAssistScale = 1f;

        [Tooltip("How strongly ambient water currents can push this transport.")]
        [SerializeField, Range(0f, 1.5f)] private float ambientCurrentInfluenceScale = 1f;

        [Tooltip("How strongly surface-lock buoyancy correction still affects this transport.")]
        [SerializeField, Range(0f, 1.5f)] private float surfaceLockInfluenceScale = 1f;

        [Tooltip("How strongly this transport advertises itself to fauna sensory systems while active.")]
        [SerializeField, Range(0f, 3f)] private float faunaDetectionSignature = 1f;

        [Header("-- Drive Response -------------------")]
        [Tooltip("How quickly drive throttle ramps up after the transport commits to propulsion.")]
#if UNITY_EDITOR
        [MinValue(0.5d)]
        [ValidateInput(nameof(IsFinitePositive), "Throttle Rise Sharpness must be finite and greater than zero.")]
#endif
        [SerializeField, Range(0.5f, 30f)] private float throttleRiseSharpness = 10f;

        [Tooltip("How quickly drive throttle bleeds off after the rider releases propulsion input.")]
#if UNITY_EDITOR
        [MinValue(0.5d)]
        [ValidateInput(nameof(IsFinitePositive), "Throttle Fall Sharpness must be finite and greater than zero.")]
#endif
        [SerializeField, Range(0.5f, 30f)] private float throttleFallSharpness = 8f;

        [Tooltip("Non-linear output curve applied after throttle smoothing. 1 = linear, >1 = heavier motor build.")]
        [SerializeField, Range(0.5f, 2f)] private float throttleOutputExponent = 1f;

        [Header("-- Runtime Ownership --------------")]
        [Tooltip("Normalized transport charge drained per second at full drive output. Mounted transports use this as their local battery budget.")]
        [SerializeField, Range(0f, 1f)] private float driveChargeDrainPerSecond = 0.02f;

        [Tooltip("Multiplier applied when this transport receives charge from a docking station.")]
        [SerializeField, Range(0f, 4f)] private float stationChargeRateScale = 1f;

        [Tooltip("Maximum structural integrity for collision damage and failure state.")]
#if UNITY_EDITOR
        [MinValue(1d)]
        [ValidateInput(nameof(IsFinitePositive), "Max Integrity must be finite and greater than zero.")]
#endif
        [SerializeField, Range(1f, 500f)] private float maxIntegrity = 100f;

        [Tooltip("Impact speed below which transport collision damage is ignored.")]
#if UNITY_EDITOR
        [MinValue(0d)]
        [ValidateInput(nameof(IsFiniteNonNegative), "Collision Damage Start Speed must be finite and non-negative.")]
#endif
        [SerializeField, Range(0f, 40f)] private float collisionDamageStartSpeed = 6f;

        [Tooltip("Impact speed at which collision damage reaches its authored ceiling.")]
#if UNITY_EDITOR
        [MinValue(0.1d)]
        [ValidateInput(nameof(IsFinitePositive), "Collision Damage Max Speed must be finite and greater than zero.")]
#endif
        [SerializeField, Range(0.1f, 60f)] private float collisionDamageMaxSpeed = 14f;

        [Tooltip("Integrity damage applied when collision speed reaches the authored ceiling.")]
        [SerializeField, Range(0f, 100f)] private float collisionDamageAtMaxSpeed = 42f;

        [Header("-- Survival Shielding ---------------")]
        [Tooltip("Multiplier applied to underwater oxygen consumption while this transport protects the rider.")]
        [SerializeField, Range(MinimumSurvivalShieldingScale, 2f)] private float oxygenConsumptionScale = 1f;

        [Tooltip("Multiplier applied to depth pressure damage while this transport protects the rider. Hard-clamped above zero so no transport grants full pressure immunity.")]
        [SerializeField, Range(MinimumPressureDamageTransferScale, 2f)] private float pressureDamageScale = 1f;

        [Tooltip("Multiplier applied to thermal exposure while this transport protects the rider.")]
        [SerializeField, Range(MinimumSurvivalShieldingScale, 2f)] private float thermalExposureScale = 1f;

        [Tooltip("Multiplier applied to radiation exposure while this transport protects the rider.")]
        [SerializeField, Range(MinimumSurvivalShieldingScale, 2f)] private float radiationExposureScale = 1f;

        [Header("-- Mounting -------------------------")]
        [Tooltip("Which player facing source drives the mounted transport hull orientation.")]
        [SerializeField] private PlayerTransportOrientationMode orientationMode = PlayerTransportOrientationMode.CameraYaw;

        [Tooltip("How aggressively the mounted transport hull follows the rider facing.")]
        [SerializeField, Range(0.5f, 30f)] private float orientationFollowSharpness = 9f;

        [Tooltip("Fallback dismount distance when no explicit dismount anchor is authored.")]
        [SerializeField, Range(0.5f, 4f)] private float dismountDistance = 1.35f;

        [Header("-- Occupancy Overrides -------------")]
        [Tooltip("Player enclosure class used by downstream camera, body, and audio consumers.")]
        [SerializeField] private PlayerTransportOccupancyMode occupancyMode = PlayerTransportOccupancyMode.Handheld;

        [Tooltip("How much of the normal swim-body presentation should remain while this transport is owned.")]
        [SerializeField, Range(0f, 1f)] private float swimPresentationScale = 1f;

        [Tooltip("How much of the normal swim thruster loop should remain while this transport is owned.")]
        [SerializeField, Range(0f, 1f)] private float thrusterAudioScale = 1f;

        [Tooltip("How much camera motion this transport should preserve for future camera consumers.")]
        [SerializeField, Range(0f, 1f)] private float cameraMotionScale = 1f;

        [Header("-- Collision Profile ---------------")]
        [Tooltip("Multiplier applied to the rider capsule radius while this transport owns collision volume.")]
        [SerializeField, Range(0.5f, 3f)] private float collisionRadiusScale = 1f;

        [Tooltip("Multiplier applied to the rider capsule height while this transport owns collision volume.")]
        [SerializeField, Range(0.5f, 3f)] private float collisionHeightScale = 1f;

        [Tooltip("Vertical center offset added to the rider capsule while this transport owns collision volume.")]
        [SerializeField, Range(-1f, 1f)] private float collisionCenterYOffset = 0f;

        [Header("-- Cockpit Overlay -----------------")]
        [Tooltip("Additional vignette intensity injected while this transport is occupied.")]
        [SerializeField, Range(0f, 0.6f)] private float cockpitVignetteIntensity = 0f;

        [Tooltip("Target vignette smoothness while this transport is occupied.")]
        [SerializeField, Range(0f, 1f)] private float cockpitVignetteSmoothness = 0.32f;

        [Tooltip("Target vignette roundness while this transport is occupied. Higher values read closer to a porthole aperture.")]
        [SerializeField, Range(0f, 1f)] private float cockpitVignetteRoundness = 1f;

        [Tooltip("Extra chromatic aberration injected by the cockpit overlay while occupied.")]
        [SerializeField, Range(0f, 0.4f)] private float cockpitChromaticAberration = 0f;

        [Header("-- Transport Normalization -----------")]
        [Tooltip("Propulsion force treated as full transport output for feel normalization.")]
        [SerializeField, Range(50f, 3000f)] private float propulsionForceReference = 1100f;

        [Header("-- Swim Presentation ----------------")]
        [Tooltip("Minimum propulsion floor injected while active transport is pulling the player.")]
        [SerializeField, Range(0f, 1f)] private float swimPropulsionFloor = 0.68f;

        [Tooltip("Stroke cadence multiplier while transport is doing part of the work.")]
        [SerializeField, Range(0.2f, 1f)] private float swimCadenceMultiplier = 0.7f;

        [Tooltip("Pose lag multiplier while transport is pulling the player forward.")]
        [SerializeField, Range(0.5f, 1.2f)] private float swimPoseLagMultiplier = 0.88f;

        [Tooltip("Root presentation weight restored by active transport.")]
        [SerializeField, Range(0f, 1f)] private float swimRootPresentationWeight = 0.62f;

        [Tooltip("Support-hand presentation weight restored by active transport.")]
        [SerializeField, Range(0f, 1f)] private float swimSupportHandWeight = 0.9f;

        [Tooltip("Forward root bias applied while transport is active.")]
        [SerializeField, Range(0f, 0.08f)] private float swimRootForwardBias = 0.022f;

        [Tooltip("Forward guide-hand bias applied while transport is active.")]
        [SerializeField, Range(0f, 0.1f)] private float swimGuideForwardBias = 0.034f;

        [Header("-- Audio ----------------------------")]
        [Tooltip("Minimum speed floor kept alive while transport is active.")]
        [SerializeField, Range(0f, 1f)] private float audioIdleSpeedFloor = 0.5f;

        [Tooltip("Extra audio volume added by active transport thrust.")]
        [SerializeField, Range(0f, 0.6f)] private float audioVolumeBoost = 0.22f;

        [Tooltip("Extra audio pitch added by active transport thrust.")]
        [SerializeField, Range(0f, 0.8f)] private float audioPitchBoost = 0.28f;

        [Tooltip("Minimum swim-mode blend kept alive while transport is active.")]
        [SerializeField, Range(0f, 1f)] private float audioModeBlendFloor = 0.42f;

        private void OnValidate()
        {
            oxygenConsumptionScale = ClampSurvivalShieldingScale(oxygenConsumptionScale);
            pressureDamageScale = ClampPressureDamageScale(pressureDamageScale);
            thermalExposureScale = ClampSurvivalShieldingScale(thermalExposureScale);
            radiationExposureScale = ClampSurvivalShieldingScale(radiationExposureScale);
        }

#if UNITY_EDITOR
        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
#endif

        /// <summary>Display label used by transport prompts when no override text is supplied.</summary>
        public string TransportName => transportName;

        /// <summary>Prompt shown when the player can mount this transport.</summary>
        public string MountInteractText => mountInteractText;

        /// <summary>Prompt shown while the player is already mounted.</summary>
        public string DismountInteractText => dismountInteractText;

        /// <summary>True when this transport should only drive underwater locomotion.</summary>
        public bool UnderwaterOnly => underwaterOnly;

        /// <summary>True when the drive should instantly collapse outside underwater swim.</summary>
        public bool AutoDropDriveOutsideWater => autoDropDriveOutsideWater;

        /// <summary>True when the rider's held tool should be holstered on mount.</summary>
        public bool HolsterToolOnMount => holsterToolOnMount;

        /// <summary>Maximum propulsion force injected at full throttle.</summary>
        public float PropulsionForce => propulsionForce;

        /// <summary>Maximum swim speed multiplier injected at full throttle.</summary>
        public float SpeedMultiplier => speedMultiplier;

        /// <summary>Minimum rider input required before the transport commits to drive.</summary>
        public float ActivationInputThreshold => activationInputThreshold;

        /// <summary>Passive cruise throttle kept alive with no input.</summary>
        public float IdleCruiseFactor => idleCruiseFactor;

        /// <summary>Suit energy drained per second at full throttle.</summary>
        public float EnergyDrainPerSecond => energyDrainPerSecond;

        /// <summary>How strongly forward drive follows camera pitch while this transport is active.</summary>
        public float ForwardPitchInfluence => forwardPitchInfluence;

        /// <summary>Horizontal strafe authority applied to rider input while this transport is active.</summary>
        public float StrafeInputScale => strafeInputScale;

        /// <summary>Vertical ascend/descend authority applied to rider input while this transport is active.</summary>
        public float VerticalInputScale => verticalInputScale;

        /// <summary>Reverse thrust authority applied when the rider pulls backward input.</summary>
        public float ReverseThrustScale => reverseThrustScale;

        /// <summary>How quickly rider body yaw catches camera yaw while this transport is active.</summary>
        public float BodyYawResponsivenessScale => bodyYawResponsivenessScale;

        /// <summary>Multiplier applied to surface dive breakthrough assistance for this transport.</summary>
        public float SurfaceDiveAssistScale => surfaceDiveAssistScale;

        /// <summary>How strongly ambient water currents can push this transport.</summary>
        public float AmbientCurrentInfluenceScale => ambientCurrentInfluenceScale;

        /// <summary>How strongly surface-lock buoyancy correction still affects this transport.</summary>
        public float SurfaceLockInfluenceScale => surfaceLockInfluenceScale;

        /// <summary>How strongly this transport advertises itself to fauna sensory systems while active.</summary>
        public float FaunaDetectionSignature => faunaDetectionSignature;

        /// <summary>How quickly drive throttle ramps up after the transport commits to propulsion.</summary>
        public float ThrottleRiseSharpness => throttleRiseSharpness;

        /// <summary>How quickly drive throttle bleeds off after propulsion input is released.</summary>
        public float ThrottleFallSharpness => throttleFallSharpness;

        /// <summary>Non-linear output curve applied after throttle smoothing.</summary>
        public float ThrottleOutputExponent => throttleOutputExponent;

        /// <summary>Normalized transport charge drained per second at full drive output.</summary>
        public float DriveChargeDrainPerSecond => driveChargeDrainPerSecond;

        /// <summary>Multiplier applied when this transport receives charge from a docking station.</summary>
        public float StationChargeRateScale => stationChargeRateScale;

        /// <summary>Maximum structural integrity for collision damage and failure state.</summary>
        public float MaxIntegrity => maxIntegrity;

        /// <summary>Impact speed below which transport collision damage is ignored.</summary>
        public float CollisionDamageStartSpeed => collisionDamageStartSpeed;

        /// <summary>Impact speed at which collision damage reaches its authored ceiling.</summary>
        public float CollisionDamageMaxSpeed => collisionDamageMaxSpeed;

        /// <summary>Integrity damage applied when collision speed reaches the authored ceiling.</summary>
        public float CollisionDamageAtMaxSpeed => collisionDamageAtMaxSpeed;

        /// <summary>Multiplier applied to underwater oxygen consumption while this transport protects the rider.</summary>
        public float OxygenConsumptionScale => ClampSurvivalShieldingScale(oxygenConsumptionScale);

        /// <summary>Multiplier applied to depth pressure damage while this transport protects the rider.</summary>
        public float PressureDamageScale => ClampPressureDamageScale(pressureDamageScale);

        /// <summary>Multiplier applied to thermal exposure while this transport protects the rider.</summary>
        public float ThermalExposureScale => ClampSurvivalShieldingScale(thermalExposureScale);

        /// <summary>Multiplier applied to radiation exposure while this transport protects the rider.</summary>
        public float RadiationExposureScale => ClampSurvivalShieldingScale(radiationExposureScale);

        /// <summary>Rider facing source used by the mounted hull.</summary>
        public PlayerTransportOrientationMode OrientationMode => orientationMode;

        /// <summary>Orientation follow sharpness of the mounted hull.</summary>
        public float OrientationFollowSharpness => orientationFollowSharpness;

        /// <summary>Fallback dismount distance when no authored anchor exists.</summary>
        public float DismountDistance => dismountDistance;

        /// <summary>Player enclosure class used by downstream transport consumers.</summary>
        public PlayerTransportOccupancyMode OccupancyMode => occupancyMode;

        /// <summary>How much of the normal swim-body presentation should remain while this transport is owned.</summary>
        public float SwimPresentationScale => swimPresentationScale;

        /// <summary>How much of the normal swim thruster loop should remain while this transport is owned.</summary>
        public float ThrusterAudioScale => thrusterAudioScale;

        /// <summary>How much camera motion this transport should preserve for future camera consumers.</summary>
        public float CameraMotionScale => cameraMotionScale;

        /// <summary>Multiplier applied to the rider capsule radius while this transport owns collision volume.</summary>
        public float CollisionRadiusScale => collisionRadiusScale;

        /// <summary>Multiplier applied to the rider capsule height while this transport owns collision volume.</summary>
        public float CollisionHeightScale => collisionHeightScale;

        /// <summary>Vertical center offset added to the rider capsule while this transport owns collision volume.</summary>
        public float CollisionCenterYOffset => collisionCenterYOffset;

        /// <summary>Additional vignette intensity injected while this transport is occupied.</summary>
        public float CockpitVignetteIntensity => cockpitVignetteIntensity;

        /// <summary>Target vignette smoothness while this transport is occupied.</summary>
        public float CockpitVignetteSmoothness => cockpitVignetteSmoothness;

        /// <summary>Target vignette roundness while this transport is occupied.</summary>
        public float CockpitVignetteRoundness => cockpitVignetteRoundness;

        /// <summary>Extra chromatic aberration injected by the cockpit overlay while occupied.</summary>
        public float CockpitChromaticAberration => cockpitChromaticAberration;

        /// <summary>Propulsion force treated as full transport output for feel normalization.</summary>
        public float PropulsionForceReference => propulsionForceReference;

        /// <summary>Minimum propulsion floor injected while active transport is pulling the player.</summary>
        public float SwimPropulsionFloor => swimPropulsionFloor;

        /// <summary>Stroke cadence multiplier while transport is doing part of the work.</summary>
        public float SwimCadenceMultiplier => swimCadenceMultiplier;

        /// <summary>Pose lag multiplier while transport is pulling the player forward.</summary>
        public float SwimPoseLagMultiplier => swimPoseLagMultiplier;

        /// <summary>Root presentation weight restored by active transport.</summary>
        public float SwimRootPresentationWeight => swimRootPresentationWeight;

        /// <summary>Support-hand presentation weight restored by active transport.</summary>
        public float SwimSupportHandWeight => swimSupportHandWeight;

        /// <summary>Forward root bias applied while transport is active.</summary>
        public float SwimRootForwardBias => swimRootForwardBias;

        /// <summary>Forward guide-hand bias applied while transport is active.</summary>
        public float SwimGuideForwardBias => swimGuideForwardBias;

        /// <summary>Minimum speed floor kept alive while transport is active.</summary>
        public float AudioIdleSpeedFloor => audioIdleSpeedFloor;

        /// <summary>Extra audio volume added by active transport thrust.</summary>
        public float AudioVolumeBoost => audioVolumeBoost;

        /// <summary>Extra audio pitch added by active transport thrust.</summary>
        public float AudioPitchBoost => audioPitchBoost;

        /// <summary>Minimum swim-mode blend kept alive while transport is active.</summary>
        public float AudioModeBlendFloor => audioModeBlendFloor;

        private static float ClampSurvivalShieldingScale(float value)
        {
            return Mathf.Clamp(value, MinimumSurvivalShieldingScale, 2f);
        }

        private static float ClampPressureDamageScale(float value)
        {
            return Mathf.Clamp(value, MinimumPressureDamageTransferScale, 2f);
        }
    }
}
