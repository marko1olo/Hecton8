using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Input;
using Hecton8.Interaction;
using Hecton8.Physics;
using Hecton8.Tools;
using Hecton8.UI;
using Hecton8.World;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Preset-driven external transport that mounts onto the current player and feeds the shared transport pipeline.
    /// </summary>
    /// <remarks>
    /// This owner does not replace HectonPlayerMovement.
    /// It registers as an external transport source so the existing swim locomotion stack stays authoritative.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(PlayerTransportFeelContract))]
    [RequireComponent(typeof(VehicleMotor))]
    [AddComponentMenu("Hecton8/Gameplay/Transport/Mountable Player Transport")]
    public sealed class MountablePlayerTransport : MonoBehaviour, IInteractable, ITickable, IUpdatable, IFixedTickable, IPlayerTransportSource, IKinematicVehicleTransportSource, IPlayerTransportLifecycleOwner, ITransportPlatform, IDamageSignalEmitter, IOriginShiftListener
    {
        private const string DefaultMountText = "Board Transport";
        private const string DefaultDismountText = "Dismount";
        private const float InertialGhostBlend = 0.15f;
        private const float MountedDriveSkinWidth = 0.08f;
        private const int EntanglementDensityProbeCount = 4;
        private const int MaxEntanglingFloraCount = 4;
        private const string EntanglementCriticalNotification = "PROPULSION ENTANGLED // CUT KELP TO RESTORE THRUST";
        private const float EntanglementStallHapticLowMotor = 0.9f;
        private const float EntanglementStallHapticHighMotor = 0.32f;
        private const float EntanglementStallHapticDurationSeconds = 0.4f;
        private const float EntanglementStallHapticDecayRate = 3.25f;
        private const byte EntanglementStallHapticPriority = 3;
        private const byte EntanglementStallHapticMotorMask = 0b0011;
        private const byte EntanglementStallHapticBlendMode = 2;
        private const float EntanglementStressHapticLowMotor = 0.62f;
        private const float EntanglementStressHapticHighMotor = 0.78f;
        private const float EntanglementStressHapticDurationSeconds = 0.22f;
        private const float EntanglementStressHapticDecayRate = 6f;
        private const byte EntanglementStressHapticPriority = 2;
        private const byte EntanglementStressHapticMotorMask = 0b0011;
        private const byte EntanglementStressHapticBlendMode = 2;
        private const float CavitationShockwaveMinRadiusMeters = 15f;

        [Header("-- Preset ---------------------------")]
        [Tooltip("Shared transport preset driving locomotion, prompts, and feel.")]
        [SerializeField] private PlayerTransportPreset preset;

        [Header("-- Anchors --------------------------")]
        [Tooltip("Seat or grip anchor representing the rider pose on this transport. Defaults to this transform.")]
        [SerializeField] private Transform riderAnchor;

        [Tooltip("Optional explicit dismount target. If omitted, a right-side offset is used.")]
        [SerializeField] private Transform dismountAnchor;

        [Header("-- Kinematic Drive -----------------")]
        [Tooltip("Optional explicit sweep capsule used by the vehicle motor. Falls back to a local CapsuleCollider when omitted.")]
        [SerializeField] private CapsuleCollider driveCapsule;

        [Tooltip("Pitch angular acceleration in degrees per second squared while the mounted vehicle is actively steering.")]
        [SerializeField, Range(10f, 360f)] private float mountedPitchAngularAcceleration = 90f;

        [Tooltip("Yaw angular acceleration in degrees per second squared while the mounted vehicle is actively steering.")]
        [SerializeField, Range(10f, 480f)] private float mountedYawAngularAcceleration = 140f;

        [Tooltip("Angular damping used to settle pitch and yaw accumulation when the rider releases steering input.")]
        [SerializeField, Range(0.1f, 24f)] private float mountedAngularDamping = 7f;

        [Tooltip("Linear damping used by the mounted vehicle motor instead of Rigidbody.drag.")]
        [SerializeField, Range(0.1f, 8f)] private float mountedLinearDamping = 1.15f;

        [Tooltip("Layer mask used by the mounted vehicle capsule sweep. Defaults to the project physics layers when omitted.")]
        [SerializeField] private LayerMask mountedSweepMask = Hecton8.Core.HectonLayerMasks.MountedSweepLayerMask;

        [Tooltip("Maximum world-up slope angle the mounted kinematic drive may climb before the sweep result is treated like a wall and flattened.")]
        [SerializeField, Range(5f, 89f)] private float mountedGroundSlopeLimitDegrees = 48f;

        [Header("-- Macro-Flora Entanglement ------")]
        [Tooltip("Minimum vehicle speed required before dense kelp or sargassum can jam the propeller.")]
        [SerializeField, Min(0.1f)] private float entanglementMinimumSpeed = 4.5f;

        [Tooltip("Look-ahead distance sampled along the vehicle velocity vector when evaluating macro-flora density.")]
        [SerializeField, Min(0.5f)] private float entanglementProbeLengthMeters = 5f;

        [Tooltip("Density-speed threshold used to trigger a propeller jam. Score = average density * speed.")]
        [SerializeField, Min(0.1f)] private float entanglementThreshold = 2.4f;

        [Tooltip("Search radius used to capture the actual kelp or sargassum stems currently wrapping the vehicle.")]
        [SerializeField, Min(0.5f)] private float entanglementCaptureRadius = 4f;

        [Tooltip("Current-driven acceleration scale applied while the vehicle is tethered to macro-flora.")]
        [SerializeField, Min(0f)] private float entanglementCurrentAcceleration = 0.6f;

        [Tooltip("Additional damping applied while the vehicle is swinging on an entanglement tether.")]
        [SerializeField, Min(0f)] private float entanglementCurrentDamping = 0.9f;

        [Tooltip("Deterministic tether tension, in newtons, above which max-thrust fighting damages the hull.")]
        [SerializeField, Min(100f)] private float entanglementTetherYieldLimit = 28000f;

        [Tooltip("Normalized throttle output required before tether fighting is treated as deliberate max thrust.")]
        [SerializeField, Range(0.1f, 1f)] private float entanglementStressThrottleThreshold = 0.8f;

        [Tooltip("Hull integrity damage per second at full tether overload while the pilot keeps max thrust applied.")]
        [SerializeField, Min(0f)] private float entanglementShearDamagePerSecond = 7.5f;

        [Tooltip("Micro-fracture load accumulated per second at full tether overload.")]
        [SerializeField, Min(0f)] private float entanglementMicroFracturePerSecond = 18f;

        [Tooltip("Micro-fracture load required to permanently reduce the transport safe-depth rating.")]
        [SerializeField, Min(1f)] private float entanglementMicroFractureLimit = 100f;

        [Tooltip("Permanent safe-depth penalty applied each time micro-fracture load crosses the limit.")]
        [SerializeField, Min(0f)] private float entanglementDepthPenaltyPerMicroFractureMeters = 35f;

        [Tooltip("Minimum interval between shear-stress damage signals, groan one-shots, and haptic pulses.")]
        [SerializeField, Min(0.02f)] private float entanglementStressSignalInterval = 0.2f;

        [Tooltip("One-shot structural groan played while the entangled hull is fighting past tether yield.")]
        [SerializeField] private AudioClip entanglementStressGroanSound;

        [Tooltip("Normalized throttle output required before trapped thrusters can cavitate.")]
        [SerializeField, Range(0.1f, 1f)] private float cavitationThrottleThreshold = 0.88f;

        [Tooltip("Vehicle speed below which high trapped thrust produces cavitation instead of useful flow.")]
        [SerializeField, Min(0.05f)] private float cavitationLowSpeedThreshold = 1.2f;

        [Tooltip("Engine integrity damage per second at full cavitation intensity.")]
        [SerializeField, Min(0f)] private float cavitationEngineDamagePerSecond = 2.5f;

        [Tooltip("Minimum interval between cavitation bubble and shockwave requests.")]
        [SerializeField, Min(0.02f)] private float cavitationEventInterval = 0.14f;

        [Tooltip("Radius of the localized cavitation shockwave emitted at the thrusters.")]
        [SerializeField, Min(0.25f)] private float cavitationShockwaveRadius = 15f;

        [Tooltip("Velocity-change impulse pushed through PhysicsApplySystem to nearby small rigidbodies hit by cavitation collapse.")]
        [SerializeField, Min(0f)] private float cavitationShockwaveAcceleration = 7f;

        [Tooltip("Optional thruster anchors used for cavitation bubble and shockwave origins. Falls back to the transport stern when empty.")]
        [SerializeField] private Transform[] cavitationThrusterAnchors;

        [Header("-- Audio ----------------------------")]
        [Tooltip("One-shot played when the player mounts this transport.")]
        [SerializeField] private AudioClip mountSound;

        [Tooltip("One-shot played when the player dismounts this transport.")]
        [SerializeField] private AudioClip dismountSound;

        [Tooltip("Mount and dismount one-shot volume.")]
        [SerializeField, Range(0f, 1f)] private float transportAudioVolume = 0.8f;

        [Header("-- Bailout Drift --------------------")]
        [Tooltip("How long a violently abandoned transport keeps drifting on inherited inertia before settling into passive sink.")]
        [SerializeField, Range(0.1f, 8f)] private float bailoutDriftDuration = 4.5f;

        [Tooltip("Downward acceleration applied while a broken transport is drifting riderless after emergency bailout.")]
        [SerializeField, Range(0f, 20f)] private float bailoutSinkAcceleration = 2.8f;

        [Tooltip("Linear damping imposed during emergency bailout drift so the transport coasts, yaws, and then starts to sink out.")]
        [SerializeField, Range(0f, 8f)] private float bailoutLinearDamping = 1.35f;

        [Tooltip("Angular damping imposed during emergency bailout drift to avoid endless spin after the rider is thrown clear.")]
        [SerializeField, Range(0f, 12f)] private float bailoutAngularDamping = 2.4f;

        [Header("-- Debug ----------------------------")]
        [SerializeField] private bool debugTransportState;

        private Transform _cachedTransform;
        private Collider _interactionCollider;
        private Rigidbody _transportBody;
        private VehicleMotor _vehicleMotor;
        private PlayerTransportFeelContract _transportFeelContract;
        private VehicleUpgradeModule _vehicleUpgradeModule;
        private CapsuleCollider _driveCapsule;
        private bool _registered;
        private bool _registeredOriginShiftListener;
        private bool _interactionColliderWasEnabled;
        private Vector3 _riderAnchorLocalPosition;
        private Quaternion _riderAnchorLocalRotation = Quaternion.identity;

        private Transform _riderTransform;
        private Rigidbody _riderBody;
        private HectonPlayerMovement _riderMovement;
        private HectonSurvivalSystem _riderSurvival;
        private PlayerTransportCoordinator _riderTransportCoordinator;
        private PlayerToolManager _riderToolManager;
        private PlayerInteraction _riderInteraction;
        private bool _riderInteractionWasEnabled;

        private IInputService _subscribedInputManager;
        private bool _mounted;
        private bool _transportActive;
        private bool _isBroken;
        private bool _lifecycleInitialized;
        private float _currentThrottle;
        private float _currentChargeNormalized = 1f;
        private float _currentIntegrity = -1f;
        private string _cachedMountText = DefaultMountText;
        private string _cachedDismountText = DefaultDismountText;
        private Vector2 _driveMoveInput;
        private float _driveVerticalInput;
        private float _bailoutDriftTimer;
        private bool _hasCachedBodyDamping;
        private bool _cachedBodyWasKinematic = true;
        private bool _dockControlLocked;
        private bool _platformMotionInitialized;
        private Vector3 _platformLinearVelocity;
        private Vector3 _platformAngularVelocity;
        private Vector3 _previousPlatformPosition;
        private Quaternion _previousPlatformRotation = Quaternion.identity;
        private float _presentationTransportBoost01;
        private float _nextMountedImpactFeedbackTime;
        private int _platformVelocityGhostWriteIndex;
        private int _platformVelocityGhostSampleCount;
        private float _entanglementStressSignalTimer;
        private float _cavitationEventTimer;
        private float _pendingEntanglementShearDamage;
        private float _pendingCavitationEngineDamage;
        private float _entangledThrottleOutput;
        private float _microFractureLoad;
        private float _pendingSafeDepthPenaltyMeters;
        private float _permanentSafeDepthPenaltyMeters;
        // COLD ALLOC: List<IDamageSignalReceiver>[1] â€” mounted transport damage listeners (player trauma dispatcher) â€” owner: MountablePlayerTransport
        private readonly List<IDamageSignalReceiver> _damageReceivers = new List<IDamageSignalReceiver>(1);
        // COLD ALLOC: Vector3[4] Ã¢â‚¬â€ inertial ghost history for presentation-only transport boost carry Ã¢â‚¬â€ owner: MountablePlayerTransport
        private readonly Vector3[] _platformVelocityGhostHistory = new Vector3[4];

        // COLD ALLOC: UInt32[4] - tracked kelp or sargassum instance uids holding the propeller lock - owner: MountablePlayerTransport
        private readonly uint[] _entanglementInstanceUids = new uint[MaxEntanglingFloraCount];
        // COLD ALLOC: Vector3[4] - tracked kelp or sargassum anchor positions paired with entanglement instance ids - owner: MountablePlayerTransport
        private readonly Vector3[] _entanglementInstancePositions = new Vector3[MaxEntanglingFloraCount];
        private int _entanglementTrackedCount;

        /// <summary>True while this external transport is actively mounted by the rider.</summary>
        public bool IsMounted => _mounted;

        /// <summary>True while this transport is actively contributing propulsion.</summary>
        public bool IsTransportActive => _mounted && _transportActive;

        /// <summary>True when this transport can currently accept station charge.</summary>
        public bool CanReceiveTransportCharge => !_transportActive && _currentChargeNormalized < 0.999f;

        /// <summary>True when the transport has failed structurally and cannot drive.</summary>
        public bool IsTransportBroken => _isBroken;

        /// <summary>Current normalized local transport charge.</summary>
        public float TransportChargeNormalized => _currentChargeNormalized;

        /// <summary>Current accumulated micro-fracture load from fighting macro-flora entanglement.</summary>
        public float MicroFractureLoad => _microFractureLoad;

        /// <summary>Permanent safe-depth penalty accumulated from micro-fracture threshold crossings.</summary>
        public float PermanentSafeDepthPenaltyMeters => _permanentSafeDepthPenaltyMeters;

        /// <summary>Current normalized transport integrity.</summary>
        public float TransportIntegrityNormalized => ResolveIntegrityNormalized();

        /// <inheritdoc />
        public bool IsVehicleMotionAuthoritative => _mounted && _transportBody != null && _vehicleMotor != null && _driveCapsule != null;

        /// <inheritdoc />
        public bool IsTransportPlatformActive => _mounted && PlatformTransform != null;

        /// <inheritdoc />
        public Transform PlatformTransform => riderAnchor != null ? riderAnchor : _cachedTransform;

        /// <inheritdoc />
        public bool InheritPlatformRotation => false;

        private void Awake()
        {
            _cachedTransform = transform;
            _interactionCollider = GetComponent<Collider>();
            TryGetComponent(out _transportBody);
            TryGetComponent(out _vehicleMotor);
            TryGetComponent(out _transportFeelContract);
            TryGetComponent(out _vehicleUpgradeModule);
            ResolveAnchorCache();
            ResolveVehicleDriveReferences();
            BindPresetToFeelContract();
            RebuildPromptCache();
            EnsureLifecycleInitialized();
            ResetPlatformMotionCache();
        }

        private void OnEnable()
        {
            TryRegister();
            TryRegisterOriginShiftListener();
            ResolveAnchorCache();
            ResolveVehicleDriveReferences();
            BindPresetToFeelContract();
            ResolveVehicleUpgradeModule();
            RebuildPromptCache();
            EnsureLifecycleInitialized();
            ResetPlatformMotionCache();
        }

        private void OnDisable()
        {
            ForceReleaseMountedRider();
            TryUnregisterOriginShiftListener();
            TryUnregister();
            _damageReceivers.Clear();
        }

        private void OnDestroy()
        {
            ForceReleaseMountedRider();
            TryUnregisterOriginShiftListener();
            TryUnregister();
        }

        void IInteractable.OnHoverStart()
        {
        }

        void IInteractable.OnHoverEnd()
        {
        }

        void IInteractable.Interact(Transform interactor)
        {
            if (interactor == null)
                return;

            if (_mounted)
            {
                if (ReferenceEquals(interactor, _riderTransform))
                    DismountRider(true);
                return;
            }

            MountRider(interactor);
        }

        string IInteractable.GetInteractText()
        {
            return _mounted ? _cachedDismountText : _cachedMountText;
        }

        /// <summary>
        /// Tick owner for mount state, input-driven throttle, and suit-energy drain.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_mounted)
                return;

            if (!HasValidMountedRider())
            {
                ForceReleaseMountedRider();
                return;
            }

            RefreshMountedInputSubscription();

            if (preset == null)
            {
                _transportActive = false;
                _currentThrottle = 0f;
                return;
            }

            if (_isBroken || _currentChargeNormalized <= 0.0001f)
            {
                _transportActive = false;
                _currentThrottle = 0f;
                return;
            }

            if (_dockControlLocked)
            {
                _transportActive = false;
                _currentThrottle = 0f;
                _driveMoveInput = Vector2.zero;
                _driveVerticalInput = 0f;
                return;
            }

            bool underwaterDriveAllowed = !preset.UnderwaterOnly ||
                (_riderMovement != null && _riderMovement.CurrentLocomotionMode == PlayerLocomotionMode.UnderwaterSwim);
            if (!underwaterDriveAllowed)
            {
                _transportActive = false;
                _currentThrottle = 0f;
                return;
            }

            IInputService inputService = GlobalRegistry.Input;
            PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                ? inputService.GetState()
                : default;
            Vector2 moveInput = inputState.MoveDelta;
            float verticalInput = inputState.VerticalDelta;
            _driveMoveInput = moveInput;
            _driveVerticalInput = verticalInput;
            if (_vehicleMotor != null && _vehicleMotor.IsEntangled)
            {
                _entangledThrottleOutput = ResolveThrottleOutput(ResolveThrottle(moveInput, verticalInput));
                _currentThrottle = 0f;
                _driveMoveInput = Vector2.zero;
                _driveVerticalInput = 0f;
                _transportActive = true;
                return;
            }

            float throttle = ResolveThrottle(moveInput, verticalInput);
            float configuredSuitEnergyDrain = ResolveConfiguredSuitEnergyDrainPerSecond();
            if (throttle > 0f && _riderSurvival != null && configuredSuitEnergyDrain > 0f)
            {
                if (_riderSurvival.Energy <= 0.01f)
                {
                    throttle = 0f;
                }
            }

            _currentThrottle = AdvanceDriveThrottle(_currentThrottle, throttle, deltaTime);
            float throttleOutput = ResolveThrottleOutput(_currentThrottle);
            float configuredDriveChargeDrain = ResolveConfiguredDriveChargeDrainPerSecond();
            if (throttleOutput > 0f && configuredDriveChargeDrain > 0f)
            {
                _currentChargeNormalized = Mathf.Max(
                    0f,
                    _currentChargeNormalized - configuredDriveChargeDrain * throttleOutput * deltaTime);

                if (_currentChargeNormalized <= 0.0001f)
                {
                    _currentChargeNormalized = 0f;
                    _currentThrottle = 0f;
                    throttleOutput = 0f;
                }
            }

            if (throttleOutput > 0f && _riderSurvival != null && configuredSuitEnergyDrain > 0f)
            {
                _riderSurvival.DrainEnergy(configuredSuitEnergyDrain * throttleOutput * deltaTime);
                if (_riderSurvival.Energy <= 0.01f)
                {
                    _currentThrottle = 0f;
                    throttleOutput = 0f;
                }
            }

            _transportActive = throttleOutput > 0.0001f;

            if (debugTransportState)
            {
                // Intentional editor-only diagnostic. Never emit in release hot path.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _debugThrottle = _currentThrottle;
                _debugMounted = _mounted;
#endif
            }
        }

        /// <summary>
        /// Fixed tick owner for visual hull alignment to the mounted rider.
        /// </summary>
        public void FixedTick(float fixedDeltaTime)
        {
            if (_mounted && _riderTransform != null)
            {
                if (_dockControlLocked)
                {
                    if (_vehicleMotor != null)
                    {
                        _vehicleMotor.TryConsumeScheduledCapsuleSweep(out _, out _, out _);
                        _vehicleMotor.ResetRuntimeState();
                    }

                    UpdatePlatformMotionCache(fixedDeltaTime);
                    return;
                }

                ApplyMountedVehicleKinematics(fixedDeltaTime);
                UpdatePlatformMotionCache(fixedDeltaTime);
                return;
            }

            if (_bailoutDriftTimer <= 0f || _transportBody == null)
            {
                TryRestoreBodyFromBailoutDrift();
                ResetPlatformMotionCache();
                return;
            }

            _bailoutDriftTimer -= fixedDeltaTime;
            if (_bailoutDriftTimer < 0f)
                _bailoutDriftTimer = 0f;

            ApplyBailoutDriftDamping(fixedDeltaTime);
            PhysicsForceRouter.QueueForce(
                _transportBody,
                Vector3.down * bailoutSinkAcceleration * fixedDeltaTime,
                ForceMode.VelocityChange);
            UpdatePlatformMotionCache(fixedDeltaTime);
        }

        /// <inheritdoc />
        public Vector3 GetPlatformPointVelocity(Vector3 worldPoint)
        {
            Transform platformTransform = PlatformTransform;
            if (!_mounted || platformTransform == null)
                return Vector3.zero;

            if (_transportBody != null && !_transportBody.isKinematic)
                return _transportBody.GetPointVelocity(worldPoint);

            Vector3 relativePoint = worldPoint - platformTransform.position;
            return _platformLinearVelocity + Vector3.Cross(_platformAngularVelocity, relativePoint);
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            if (shiftOffset.sqrMagnitude <= 0.000001f)
                return;

            _previousPlatformPosition -= shiftOffset;
        }

        /// <summary>Current propulsion force contributed by this transport.</summary>
        public float GetTransportPropulsionForce()
        {
            return _mounted && preset != null
                ? preset.PropulsionForce * ResolveThrottleOutput(_currentThrottle)
                : 0f;
        }

        /// <summary>Current swim speed multiplier contributed by this transport.</summary>
        public float GetTransportSpeedMultiplier()
        {
            if (!_mounted || preset == null)
                return 1f;

            return Mathf.Lerp(1f, preset.SpeedMultiplier, ResolveThrottleOutput(_currentThrottle));
        }

        /// <summary>Current normalized transport boost used by shared presentation/audio consumers.</summary>
        public float GetTransportBoost01()
        {
            if (!_mounted || preset == null)
                return 0f;

            float reference = Mathf.Max(0.01f, preset.PropulsionForceReference);
            float throttleBoost = Mathf.Clamp01(GetTransportPropulsionForce() / reference);
            return Mathf.Clamp01(Mathf.Max(_presentationTransportBoost01, throttleBoost));
        }

        /// <summary>
        /// Recharges the transport by a normalized amount from a docking station.
        /// </summary>
        public void RechargeTransport(float normalizedChargeDelta)
        {
            if (normalizedChargeDelta <= 0f)
                return;

            EnsureLifecycleInitialized();
            _currentChargeNormalized = Mathf.Clamp01(
                _currentChargeNormalized + normalizedChargeDelta * ResolveStationChargeRateScale());
        }

        /// <summary>
        /// Applies collision impact damage to this transport.
        /// </summary>
        public void ApplyTransportCollisionImpact(float impactSpeed, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (preset == null || _isBroken)
                return;

            float previousIntegrityNormalized = ResolveIntegrityNormalized();
            float startSpeed = Mathf.Max(0f, preset.CollisionDamageStartSpeed);
            if (impactSpeed <= startSpeed)
                return;

            float maxSpeed = Mathf.Max(startSpeed + 0.01f, preset.CollisionDamageMaxSpeed);
            float maxDamage = Mathf.Max(0f, preset.CollisionDamageAtMaxSpeed);
            if (maxDamage <= 0f)
                return;

            float damageT = Mathf.InverseLerp(startSpeed, maxSpeed, impactSpeed);
            float damage = Mathf.Lerp(0f, maxDamage, damageT);
            if (damage <= 0f)
                return;

            EnsureLifecycleInitialized();
            _currentIntegrity = Mathf.Max(0f, _currentIntegrity - damage);
            float nextIntegrityNormalized = ResolveIntegrityNormalized();
            DamageSignal damageSignal = BuildDamageSignal(impactSpeed, hitPoint, (uint)DamageTypeMask.Impact, previousIntegrityNormalized, nextIntegrityNormalized);
            DispatchIntegrityChanged(previousIntegrityNormalized, nextIntegrityNormalized, damageSignal);

            float previousPowerChannel = ResolvePowerChannel(previousIntegrityNormalized);
            float nextPowerChannel = ResolvePowerChannel(nextIntegrityNormalized);
            if (Mathf.Abs(nextPowerChannel - previousPowerChannel) > 0.0001f)
                DispatchPowerChanged(previousPowerChannel, nextPowerChannel, damageSignal);

            DispatchClarityChanged(0f, Mathf.Clamp01(Mathf.Max(damageT, 1f - nextIntegrityNormalized)), damageSignal);
            DispatchTraumaThresholdCrossed(ResolveTraumaLevel(nextIntegrityNormalized, damageT));
            if (_currentIntegrity <= 0.0001f)
                BreakTransport();
        }

        private void ApplyTransportStressDamage(
            float damage,
            float signalMagnitude,
            Vector3 hitPoint,
            uint damageType,
            float trauma01,
            bool dispatchPowerChange)
        {
            if (preset == null || _isBroken || damage <= 0f)
                return;

            EnsureLifecycleInitialized();
            float previousIntegrityNormalized = ResolveIntegrityNormalized();
            _currentIntegrity = Mathf.Max(0f, _currentIntegrity - damage);
            float nextIntegrityNormalized = ResolveIntegrityNormalized();
            DamageSignal damageSignal = BuildDamageSignal(
                signalMagnitude,
                hitPoint,
                damageType,
                previousIntegrityNormalized,
                nextIntegrityNormalized);
            DispatchIntegrityChanged(previousIntegrityNormalized, nextIntegrityNormalized, damageSignal);

            if (dispatchPowerChange)
            {
                float previousPowerChannel = ResolvePowerChannel(previousIntegrityNormalized);
                float nextPowerChannel = ResolvePowerChannel(nextIntegrityNormalized);
                if (Mathf.Abs(nextPowerChannel - previousPowerChannel) > 0.0001f)
                    DispatchPowerChanged(previousPowerChannel, nextPowerChannel, damageSignal);
            }

            float clampedTrauma = Mathf.Clamp01(trauma01);
            DispatchClarityChanged(0f, Mathf.Clamp01(Mathf.Max(clampedTrauma, 1f - nextIntegrityNormalized)), damageSignal);
            DispatchTraumaThresholdCrossed(ResolveTraumaLevel(nextIntegrityNormalized, clampedTrauma));
            if (_currentIntegrity <= 0.0001f)
                BreakTransport();
        }

        /// <summary>
        /// Registers a damage receiver for transport damage signals.
        /// </summary>
        public void RegisterDamageReceiver(IDamageSignalReceiver receiver)
        {
            if (receiver == null)
                return;

            for (int i = 0; i < _damageReceivers.Count; i++)
            {
                if (ReferenceEquals(_damageReceivers[i], receiver))
                    return;
            }

            _damageReceivers.Add(receiver);
        }

        /// <summary>
        /// Unregisters a previously registered transport damage receiver.
        /// </summary>
        public void UnregisterDamageReceiver(IDamageSignalReceiver receiver)
        {
            if (receiver == null)
                return;

            for (int i = _damageReceivers.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_damageReceivers[i], receiver))
                {
                    _damageReceivers.RemoveAt(i);
                    break;
                }
            }
        }

        private void MountRider(Transform interactor)
        {
            if (preset == null || _mounted)
                return;

            TryRestoreBodyFromBailoutDrift();

            if (!ResolveRiderReferences(interactor))
                return;

            if (_riderTransportCoordinator == null || !_riderTransportCoordinator.SetExternalTransportSource(this))
            {
                ClearRiderReferences();
                return;
            }

            _mounted = true;
            _transportActive = false;
            _currentThrottle = 0f;
            _interactionColliderWasEnabled = _interactionCollider != null && _interactionCollider.enabled;
            if (_interactionColliderWasEnabled)
                _interactionCollider.enabled = false;

            if (preset.HolsterToolOnMount && _riderToolManager != null)
                _riderToolManager.Holster();

            if (_riderInteraction != null)
            {
                _riderInteractionWasEnabled = _riderInteraction.enabled;
                if (_riderInteractionWasEnabled)
                    _riderInteraction.enabled = false;
            }

            RefreshMountedInputSubscription();
            BindPresetToFeelContract();
            ResolveVehicleDriveReferences();
            ClearMacroFloraEntanglement();
            PrepareMountedKinematicBody();
            AlignTransportToRider(0f);
            ResetPlatformMotionCache();
            SyncMountedRiderVelocity();
            PlayTransportOneShot(mountSound);
        }

        private void DismountRider(bool placeRiderAtExit)
        {
            DismountRiderInternal(placeRiderAtExit, applyEvaHandoff: true, transferTowToTransport: true);
        }

        internal void TriggerEmergencyBailoutDrift(Vector3 inheritedVelocity, float severity)
        {
            if (!_mounted)
            {
                BeginEmergencyBailoutDrift(inheritedVelocity, severity);
                return;
            }

            HectonPlayerMovement riderMovement = _riderMovement;
            DismountRiderInternal(placeRiderAtExit: false, applyEvaHandoff: true, transferTowToTransport: false);
            BeginEmergencyBailoutDrift(inheritedVelocity, severity);
            TryTransferTowHandoffToTransport(riderMovement);
        }

        private void ForceReleaseMountedRider()
        {
            ClearMacroFloraEntanglement();
            if (!_mounted)
            {
                UnsubscribeMountedInput();
                RestoreInteractionCollider();
                ClearRiderReferences();
                ResetPlatformMotionCache();
                return;
            }

            if (_riderTransportCoordinator != null)
                _riderTransportCoordinator.ClearExternalTransportSource(this);

            RestoreRiderInteraction();
            UnsubscribeMountedInput();
            RestoreInteractionCollider();
            ClearRiderReferences();

            _mounted = false;
            _transportActive = false;
            _currentThrottle = 0f;
            ResetPlatformMotionCache();
        }

        private bool ResolveRiderReferences(Transform interactor)
        {
            ClearRiderReferences();

            _riderTransform = interactor;
            _riderTransform.TryGetComponent(out _riderBody);
            _riderTransform.TryGetComponent(out _riderMovement);
            _riderTransform.TryGetComponent(out _riderSurvival);
            _riderTransform.TryGetComponent(out _riderTransportCoordinator);
            _riderTransform.TryGetComponent(out _riderToolManager);
            _riderTransform.TryGetComponent(out _riderInteraction);

            return _riderTransform != null &&
                   _riderMovement != null &&
                   _riderTransportCoordinator != null;
        }

        private void ClearRiderReferences()
        {
            _riderTransform = null;
            _riderBody = null;
            _riderMovement = null;
            _riderSurvival = null;
            _riderTransportCoordinator = null;
            _riderToolManager = null;
            _riderInteraction = null;
            _riderInteractionWasEnabled = false;
        }

        private bool HasValidMountedRider()
        {
            return _riderTransform != null &&
                   _riderTransportCoordinator != null &&
                   _riderMovement != null;
        }

        private void AlignTransportToRider(float fixedDeltaTime)
        {
            Quaternion desiredRiderRotation = ResolveDesiredRiderRotation();
            Quaternion targetRotation = desiredRiderRotation * Quaternion.Inverse(_riderAnchorLocalRotation);
            if (fixedDeltaTime > 0f && preset != null)
            {
                float followT = ResolveBlendFactor(preset.OrientationFollowSharpness, fixedDeltaTime);
                targetRotation = Quaternion.Slerp(_cachedTransform.rotation, targetRotation, followT);
            }

            Vector3 riderPosition = _riderTransform.position;
            Vector3 targetPosition = riderPosition - targetRotation * _riderAnchorLocalPosition;

            if (_transportBody != null)
            {
                _transportBody.MoveRotation(targetRotation);
                _transportBody.MovePosition(targetPosition);
            }
            else
            {
                _cachedTransform.SetPositionAndRotation(targetPosition, targetRotation);
            }
        }

        private void ApplyMountedVehicleKinematics(float fixedDeltaTime)
        {
            if (_transportBody == null || _vehicleMotor == null || _driveCapsule == null)
            {
                AlignTransportToRider(fixedDeltaTime);
                return;
            }

            if (_vehicleMotor.TryConsumeScheduledCapsuleSweep(out bool wasBlocked, out _, out _))
            {
                if (wasBlocked)
                    HandleMountedSweepImpact();
            }

            if (_vehicleMotor.HasPendingSweep)
                return;

            float throttleOutput = ResolveThrottleOutput(_currentThrottle);
            float safeMass = math.max(1f, _transportBody.mass);
            float thrustAcceleration = (preset != null ? math.max(0f, preset.PropulsionForce) : 0f) / safeMass;
            float maxSpeed = math.max(1f, ResolveMountedDriveMaxSpeed(throttleOutput));
            ResolveVehicleUpgradeModule();
            if (_vehicleUpgradeModule != null)
                thrustAcceleration *= math.max(1f, _vehicleUpgradeModule.ThrustAccelerationMultiplier);
            float hydrodynamicSubmersionFactor = _riderMovement != null
                ? math.saturate(_riderMovement.WaterImmersionRatio)
                : 0f;
            float hydrodynamicDepthMeters = _riderSurvival != null
                ? math.max(0f, _riderSurvival.Depth)
                : (_riderMovement != null ? math.max(0f, _riderMovement.CurrentDepth) : 0f);

            _vehicleMotor.ConfigureHydrodynamicSubmersion(hydrodynamicSubmersionFactor);
            _vehicleMotor.ConfigureHydrodynamicDepth(hydrodynamicDepthMeters);
            if (TryAdvanceMacroFloraEntanglement(fixedDeltaTime, thrustAcceleration, safeMass))
            {
                int entanglementSelfColliderInstanceId = _interactionCollider != null
                    ? unchecked((int)EntityId.ToULong(_interactionCollider.GetEntityId()))
                    : 0;
                int entanglementSweepMask = mountedSweepMask.value != 0 ? mountedSweepMask.value : HectonLayerMasks.DefaultRaycastLayerMask;
                _vehicleMotor.ScheduleCapsuleSweepBatch(
                    entanglementSweepMask,
                    MountedDriveSkinWidth,
                    entanglementSelfColliderInstanceId,
                    fixedDeltaTime);
                return;
            }

            float forwardInput = math.clamp(_driveMoveInput.y, -1f, 1f) * throttleOutput;
            float yawInput = math.clamp(_driveMoveInput.x, -1f, 1f);
            float pitchInput = math.clamp(_driveVerticalInput, -1f, 1f);

            _vehicleMotor.IntegrateDrive(
                forwardInput,
                yawInput,
                pitchInput,
                thrustAcceleration,
                maxSpeed,
                mountedLinearDamping,
                mountedYawAngularAcceleration,
                mountedPitchAngularAcceleration,
                mountedAngularDamping,
                fixedDeltaTime);

            int selfColliderInstanceId = _interactionCollider != null
                ? unchecked((int)EntityId.ToULong(_interactionCollider.GetEntityId()))
                : 0;
            int sweepLayerMask = mountedSweepMask.value != 0 ? mountedSweepMask.value : HectonLayerMasks.DefaultRaycastLayerMask;
            _vehicleMotor.ScheduleCapsuleSweepBatch(
                sweepLayerMask,
                MountedDriveSkinWidth,
                selfColliderInstanceId,
                fixedDeltaTime);
        }

        private bool TryAdvanceMacroFloraEntanglement(float fixedDeltaTime, float thrustAcceleration, float safeMass)
        {
            if (_vehicleMotor == null || _transportBody == null)
                return false;

            HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (_vehicleMotor.IsEntangled)
            {
                DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
                if (organicManager != null && organicManager.AreTrackedFloraDestroyed(_entanglementInstanceUids, _entanglementTrackedCount))
                {
                    ClearMacroFloraEntanglement();
                    return false;
                }

                Vector3 flowVelocity = Vector3.zero;
                if (vegetationBridge != null)
                    vegetationBridge.TrySampleAbyssalFlow(_transportBody.position, out flowVelocity);

                float throttleOutput = _entangledThrottleOutput;
                _driveMoveInput = Vector2.zero;
                _driveVerticalInput = 0f;
                _currentThrottle = 0f;
                _transportActive = true;
                _vehicleMotor.AdvanceEntanglement(flowVelocity, entanglementCurrentAcceleration, entanglementCurrentDamping, fixedDeltaTime);
                ApplyEntanglementStressAndCavitation(fixedDeltaTime, throttleOutput, thrustAcceleration, safeMass);
                return true;
            }

            if (vegetationBridge == null)
                return false;

            Vector3 velocity = _vehicleMotor.LinearVelocity;
            float speed = velocity.magnitude;
            if (speed < entanglementMinimumSpeed)
                return false;

            Vector3 direction = velocity / math.max(speed, 0.0001f);
            float density = SampleMacroFloraDensityAlongVelocity(vegetationBridge, _transportBody.position, direction, speed, fixedDeltaTime);
            float entanglementScore = density * speed;
            if (entanglementScore < entanglementThreshold)
                return false;

            DestructibleOrganicManager destructibleOrganicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            if (destructibleOrganicManager == null)
                return false;

            Vector3 sampleCenter = _transportBody.position + direction * math.max(1f, entanglementProbeLengthMeters * 0.5f);
            int trackedCount = destructibleOrganicManager.CollectNearestConsumableFlora(
                sampleCenter,
                entanglementCaptureRadius,
                _entanglementInstanceUids,
                _entanglementInstancePositions);
            if (trackedCount <= 0)
                return false;

            _entanglementTrackedCount = trackedCount;
            Vector3 anchorPosition = Vector3.zero;
            for (int i = 0; i < trackedCount; i++)
                anchorPosition += _entanglementInstancePositions[i];

            anchorPosition /= trackedCount;
            float tetherLength = Vector3.Distance(_transportBody.position, anchorPosition);
            _vehicleMotor.BeginEntanglement(anchorPosition, tetherLength);
            NotifyEntanglementCritical();

            Vector3 anchorFlowVelocity = Vector3.zero;
            vegetationBridge.TrySampleAbyssalFlow(anchorPosition, out anchorFlowVelocity);
            float initialThrottleDemand = preset != null ? ResolveThrottle(_driveMoveInput, _driveVerticalInput) : 0f;
            float initialThrottleOutput = ResolveThrottleOutput(initialThrottleDemand);
            _entangledThrottleOutput = initialThrottleOutput;
            _driveMoveInput = Vector2.zero;
            _driveVerticalInput = 0f;
            _currentThrottle = 0f;
            _transportActive = true;
            _vehicleMotor.AdvanceEntanglement(anchorFlowVelocity, entanglementCurrentAcceleration, entanglementCurrentDamping, fixedDeltaTime);
            ApplyEntanglementStressAndCavitation(fixedDeltaTime, initialThrottleOutput, thrustAcceleration, safeMass);
            return true;
        }

        private float SampleMacroFloraDensityAlongVelocity(
            HectonMapMagicVegetationBridge vegetationBridge,
            Vector3 origin,
            Vector3 direction,
            float speed,
            float fixedDeltaTime)
        {
            if (vegetationBridge == null || direction.sqrMagnitude <= 0.0001f)
                return 0f;

            float probeDistance = math.max(1f, math.max(entanglementProbeLengthMeters, speed * math.max(fixedDeltaTime, 0.0001f)));
            float accumulatedDensity = 0f;
            for (int i = 0; i < EntanglementDensityProbeCount; i++)
            {
                float sampleT = (i + 1f) / EntanglementDensityProbeCount;
                Vector3 samplePosition = origin + direction * (probeDistance * sampleT);
                accumulatedDensity += vegetationBridge.SampleMacroFloraDensityImmediate(samplePosition);
            }

            return accumulatedDensity / EntanglementDensityProbeCount;
        }

        private void ClearMacroFloraEntanglement()
        {
            if (_vehicleMotor != null && _vehicleMotor.IsEntangled)
                _vehicleMotor.ClearEntanglement();

            _entanglementStressSignalTimer = 0f;
            _cavitationEventTimer = 0f;
            _pendingEntanglementShearDamage = 0f;
            _pendingCavitationEngineDamage = 0f;
            _entangledThrottleOutput = 0f;
            _entanglementTrackedCount = 0;
            for (int i = 0; i < _entanglementInstanceUids.Length; i++)
            {
                _entanglementInstanceUids[i] = 0u;
                _entanglementInstancePositions[i] = Vector3.zero;
            }
        }

        private void ApplyEntanglementStressAndCavitation(
            float fixedDeltaTime,
            float throttleOutput,
            float thrustAcceleration,
            float safeMass)
        {
            if (_vehicleMotor == null || _transportBody == null || fixedDeltaTime <= 0f)
                return;

            float safeDeltaTime = math.max(0.0001f, fixedDeltaTime);
            float clampedThrottleOutput = math.saturate(throttleOutput);
            float commandTension = math.max(0f, safeMass) * math.max(0f, thrustAcceleration) * clampedThrottleOutput;
            float tetherTension = math.max(0f, _vehicleMotor.LastEntanglementTensionNewtons + commandTension);
            bool maxThrust = clampedThrottleOutput >= entanglementStressThrottleThreshold;
            bool overYield = maxThrust && tetherTension > entanglementTetherYieldLimit;

            if (overYield)
            {
                float overload01 = math.saturate((tetherTension - entanglementTetherYieldLimit) /
                                                 math.max(entanglementTetherYieldLimit, 0.0001f));
                _pendingEntanglementShearDamage += entanglementShearDamagePerSecond * overload01 * safeDeltaTime;
                AccumulateMicroFractureLoad(overload01, safeDeltaTime);
                _entanglementStressSignalTimer -= safeDeltaTime;
                if (_entanglementStressSignalTimer <= 0f)
                {
                    ApplyTransportStressDamage(
                        _pendingEntanglementShearDamage,
                        tetherTension,
                        _transportBody.worldCenterOfMass,
                        (uint)(DamageTypeMask.Impact | DamageTypeMask.Pressure | DamageTypeMask.MicroFracture),
                        overload01,
                        dispatchPowerChange: false);
                    _pendingEntanglementShearDamage = 0f;
                    PlayTransportOneShot(entanglementStressGroanSound);
                    NotifyEntanglementStressHaptic();
                    _entanglementStressSignalTimer = entanglementStressSignalInterval;
                }
            }
            else
            {
                FlushPendingEntanglementDamage(tetherTension);
            }

            float speed = _vehicleMotor.LinearVelocity.magnitude;
            bool cavitating = clampedThrottleOutput >= cavitationThrottleThreshold &&
                              speed <= cavitationLowSpeedThreshold &&
                              thrustAcceleration > 0.0001f;
            if (!cavitating)
            {
                FlushPendingCavitationDamage();
                return;
            }

            float speedSuppression01 = 1f - math.saturate(speed / math.max(cavitationLowSpeedThreshold, 0.0001f));
            float cavitationIntensity01 = math.saturate(clampedThrottleOutput * math.max(speedSuppression01, overYield ? 1f : 0.5f));
            _pendingCavitationEngineDamage += cavitationEngineDamagePerSecond * cavitationIntensity01 * safeDeltaTime;
            _cavitationEventTimer -= safeDeltaTime;
            if (_cavitationEventTimer > 0f)
                return;

            QueueCavitationBursts(cavitationIntensity01);
            ApplyTransportStressDamage(
                _pendingCavitationEngineDamage,
                cavitationShockwaveAcceleration * cavitationIntensity01,
                ResolveCavitationFallbackPosition(),
                (uint)(DamageTypeMask.Impact | DamageTypeMask.Pressure),
                cavitationIntensity01,
                dispatchPowerChange: true);
            _pendingCavitationEngineDamage = 0f;
            _cavitationEventTimer = cavitationEventInterval;
        }

        private void FlushPendingEntanglementDamage(float tetherTension)
        {
            if (_pendingEntanglementShearDamage <= 0f || _transportBody == null)
                return;

            ApplyTransportStressDamage(
                _pendingEntanglementShearDamage,
                tetherTension,
                _transportBody.worldCenterOfMass,
                (uint)(DamageTypeMask.Impact | DamageTypeMask.Pressure | DamageTypeMask.MicroFracture),
                0.1f,
                dispatchPowerChange: false);
            _pendingEntanglementShearDamage = 0f;
        }

        private void AccumulateMicroFractureLoad(float overload01, float safeDeltaTime)
        {
            if (entanglementMicroFracturePerSecond <= 0f ||
                entanglementMicroFractureLimit <= 0f ||
                entanglementDepthPenaltyPerMicroFractureMeters <= 0f)
            {
                return;
            }

            float localMicroFractureDamage = entanglementMicroFracturePerSecond * math.saturate(overload01) * math.max(0f, safeDeltaTime);
            if (localMicroFractureDamage <= 0f)
                return;

            _microFractureLoad += localMicroFractureDamage;
            float fractureLimit = math.max(1f, entanglementMicroFractureLimit);
            if (_microFractureLoad < fractureLimit)
                return;

            float fractureEvents = math.floor(_microFractureLoad / fractureLimit);
            _microFractureLoad -= fractureEvents * fractureLimit;
            _pendingSafeDepthPenaltyMeters += fractureEvents * math.max(0f, entanglementDepthPenaltyPerMicroFractureMeters);

            float wholePenaltyMeters = math.floor(_pendingSafeDepthPenaltyMeters);
            if (wholePenaltyMeters < 1f)
                return;

            _pendingSafeDepthPenaltyMeters -= wholePenaltyMeters;
            _permanentSafeDepthPenaltyMeters += wholePenaltyMeters;
            ResolveVehicleUpgradeModule();
            if (_vehicleUpgradeModule != null)
                _vehicleUpgradeModule.ApplyPermanentSafeDepthPenalty(wholePenaltyMeters);
        }

        private void FlushPendingCavitationDamage()
        {
            if (_pendingCavitationEngineDamage <= 0f)
                return;

            ApplyTransportStressDamage(
                _pendingCavitationEngineDamage,
                0f,
                ResolveCavitationFallbackPosition(),
                (uint)(DamageTypeMask.Impact | DamageTypeMask.Pressure),
                0.1f,
                dispatchPowerChange: true);
            _pendingCavitationEngineDamage = 0f;
        }

        private void QueueCavitationBursts(float intensity01)
        {
            float clampedIntensity = math.saturate(intensity01);
            if (clampedIntensity <= 0.0001f)
                return;

            int sourceBodyInstanceId = _transportBody != null ? unchecked((int)EntityId.ToULong(_transportBody.GetEntityId())) : 0;
            Vector3 direction = _cachedTransform != null ? -_cachedTransform.forward : Vector3.back;
            bool queuedAny = false;
            float resolvedShockwaveRadius = math.max(CavitationShockwaveMinRadiusMeters, cavitationShockwaveRadius);
            if (cavitationThrusterAnchors != null)
            {
                for (int i = 0; i < cavitationThrusterAnchors.Length; i++)
                {
                    Transform anchor = cavitationThrusterAnchors[i];
                    if (anchor == null)
                        continue;

                    HectonFluidEngine.QueueCavitationBurst(
                        anchor.position,
                        direction,
                        clampedIntensity,
                        resolvedShockwaveRadius,
                        cavitationShockwaveAcceleration,
                        sourceBodyInstanceId);
                    queuedAny = true;
                }
            }

            if (queuedAny)
                return;

            HectonFluidEngine.QueueCavitationBurst(
                ResolveCavitationFallbackPosition(),
                direction,
                clampedIntensity,
                resolvedShockwaveRadius,
                cavitationShockwaveAcceleration,
                sourceBodyInstanceId);
        }

        private Vector3 ResolveCavitationFallbackPosition()
        {
            if (_cachedTransform == null)
                return _transportBody != null ? _transportBody.position : Vector3.zero;

            return _cachedTransform.position - (_cachedTransform.forward * 1.25f);
        }

        private static void NotifyEntanglementStressHaptic()
        {
            ToolHapticsRuntime.EnqueueCommand(
                EntanglementStressHapticLowMotor,
                EntanglementStressHapticHighMotor,
                EntanglementStressHapticDurationSeconds,
                EntanglementStressHapticDecayRate,
                EntanglementStressHapticPriority,
                EntanglementStressHapticMotorMask,
                EntanglementStressHapticBlendMode);
        }

        private static void NotifyEntanglementCritical()
        {
            NotificationEvents.PushCritical(EntanglementCriticalNotification);
            ToolHapticsRuntime.EnqueueCommand(
                EntanglementStallHapticLowMotor,
                EntanglementStallHapticHighMotor,
                EntanglementStallHapticDurationSeconds,
                EntanglementStallHapticDecayRate,
                EntanglementStallHapticPriority,
                EntanglementStallHapticMotorMask,
                EntanglementStallHapticBlendMode);
        }

        private Quaternion ResolveDesiredRiderRotation()
        {
            if (_riderMovement != null && preset != null)
            {
                float yaw = preset.OrientationMode == PlayerTransportOrientationMode.BodyYaw
                    ? _riderMovement.BodyYaw
                    : _riderMovement.CameraYaw;

                float yawRadians = yaw * Mathf.Deg2Rad;
                Vector3 planarForward = new Vector3(Mathf.Sin(yawRadians), 0f, Mathf.Cos(yawRadians));
                if (planarForward.sqrMagnitude > 0.0001f)
                    return Quaternion.LookRotation(planarForward, Vector3.up);
            }

            Vector3 riderForward = _riderTransform.forward;
            riderForward.y = 0f;
            if (riderForward.sqrMagnitude < 0.0001f)
                riderForward = _cachedTransform.forward;

            riderForward.Normalize();
            return Quaternion.LookRotation(riderForward, Vector3.up);
        }

        private float ResolveThrottle(Vector2 moveInput, float verticalInput)
        {
            float planarMagnitude = Mathf.Clamp01(Mathf.Sqrt(moveInput.x * moveInput.x + moveInput.y * moveInput.y));
            float verticalMagnitude = Mathf.Clamp01(Mathf.Abs(verticalInput));
            float driveInputMagnitude = Mathf.Max(planarMagnitude, verticalMagnitude);
            if (driveInputMagnitude >= preset.ActivationInputThreshold)
                return driveInputMagnitude;

            return preset.IdleCruiseFactor;
        }

        private float AdvanceDriveThrottle(float currentThrottle, float targetThrottle, float deltaTime)
        {
            float clampedCurrent = Mathf.Clamp01(currentThrottle);
            float clampedTarget = Mathf.Clamp01(targetThrottle);
            float sharpness = clampedTarget > clampedCurrent
                ? Mathf.Max(0.5f, preset.ThrottleRiseSharpness)
                : Mathf.Max(0.5f, preset.ThrottleFallSharpness);
            float blend = ResolveBlendFactor(sharpness, deltaTime);
            return Mathf.Lerp(clampedCurrent, clampedTarget, blend);
        }

        private float ResolveThrottleOutput(float rawThrottle)
        {
            float clampedThrottle = Mathf.Clamp01(rawThrottle);
            float exponent = Mathf.Max(0.5f, preset != null ? preset.ThrottleOutputExponent : 1f);
            return Mathf.Pow(clampedThrottle, exponent);
        }

        private void MoveRiderToDismountPoint()
        {
            ResolveDismountPose(out Vector3 targetPosition, out Quaternion targetRotation);
            MoveRiderToDismountPose(targetPosition, targetRotation);
        }

        private void ResolveDismountPose(out Vector3 targetPosition, out Quaternion targetRotation)
        {
            if (_riderTransform == null)
            {
                targetPosition = _cachedTransform.position;
                targetRotation = _cachedTransform.rotation;
                return;
            }

            if (dismountAnchor != null)
            {
                targetPosition = dismountAnchor.position;
                targetRotation = dismountAnchor.rotation;
            }
            else
            {
                float distance = preset != null ? preset.DismountDistance : 1.35f;
                targetPosition = _cachedTransform.position + _cachedTransform.right * distance + Vector3.up * 0.1f;
                targetRotation = _riderTransform.rotation;
            }
        }

        private void MoveRiderToDismountPose(Vector3 targetPosition, Quaternion targetRotation)
        {
            if (_riderBody != null)
            {
                _riderBody.MovePosition(targetPosition);
                _riderBody.MoveRotation(targetRotation);
                return;
            }

            _riderTransform.SetPositionAndRotation(targetPosition, targetRotation);
        }

        private void SyncMountedRiderVelocity()
        {
            if (_riderBody == null)
                return;

            Vector3 riderPosition = _riderTransform != null ? _riderTransform.position : _riderBody.position;
            Vector3 platformVelocity = GetPlatformPointVelocity(riderPosition);
            _riderBody.linearVelocity = HectonPlayerMotor.SafeVelocity(platformVelocity);
            _riderBody.angularVelocity = HectonPlayerMotor.SafeVelocity(Vector3.zero);
        }

        private void ResolveAnchorCache()
        {
            Transform anchor = riderAnchor != null ? riderAnchor : _cachedTransform;
            if (anchor == _cachedTransform)
            {
                _riderAnchorLocalPosition = Vector3.zero;
                _riderAnchorLocalRotation = Quaternion.identity;
                return;
            }

            _riderAnchorLocalPosition = anchor.localPosition;
            _riderAnchorLocalRotation = anchor.localRotation;
        }

        private void ResolveVehicleDriveReferences()
        {
            if (_vehicleMotor == null)
                TryGetComponent(out _vehicleMotor);

            _driveCapsule = driveCapsule;
            if (_driveCapsule == null)
                TryGetComponent(out _driveCapsule);

            if (_vehicleMotor != null)
            {
                _vehicleMotor.Bind(_transportBody, _driveCapsule);
                _vehicleMotor.ConfigureGroundSlopeLimit(mountedGroundSlopeLimitDegrees);
            }
        }

        private void BindPresetToFeelContract()
        {
            if (_transportFeelContract == null)
                TryGetComponent(out _transportFeelContract);

            if (_transportFeelContract != null && preset != null)
                _transportFeelContract.BindPreset(preset);
        }

        private void EnsureLifecycleInitialized()
        {
            if (_lifecycleInitialized)
                return;

            _currentChargeNormalized = 1f;
            _currentIntegrity = ResolveMaxIntegrity();
            _isBroken = false;
            _lifecycleInitialized = true;
        }

        private float ResolveMaxIntegrity()
        {
            ResolveVehicleUpgradeModule();
            float integrityBonus = _vehicleUpgradeModule != null
                ? Mathf.Max(0f, _vehicleUpgradeModule.MaxIntegrityBonus)
                : 0f;

            return preset != null
                ? Mathf.Max(1f, preset.MaxIntegrity + integrityBonus)
                : 100f + integrityBonus;
        }

        private float ResolveIntegrityNormalized()
        {
            EnsureLifecycleInitialized();
            return Mathf.Clamp01(_currentIntegrity / ResolveMaxIntegrity());
        }

        private float ResolveStationChargeRateScale()
        {
            return preset != null
                ? Mathf.Max(0f, preset.StationChargeRateScale)
                : 1f;
        }

        private void ResolveVehicleUpgradeModule()
        {
            if (_vehicleUpgradeModule == null)
                TryGetComponent(out _vehicleUpgradeModule);
        }

        internal void BeginDockControlLock()
        {
            _dockControlLocked = true;
            _transportActive = false;
            _currentThrottle = 0f;
            _driveMoveInput = Vector2.zero;
            _driveVerticalInput = 0f;
            if (_vehicleMotor != null)
                _vehicleMotor.ResetRuntimeState();
        }

        internal void EndDockControlLock()
        {
            _dockControlLocked = false;
            _currentThrottle = 0f;
            _driveMoveInput = Vector2.zero;
            _driveVerticalInput = 0f;
            if (_vehicleMotor != null)
                _vehicleMotor.ResetRuntimeState();
        }

        private void HandleMountedSweepImpact()
        {
            if (_transportBody == null || _vehicleMotor == null)
                return;

            float impactSpeed = _vehicleMotor.LastBlockingImpactSpeedMetersPerSecond;
            float threshold = preset != null ? Mathf.Max(0f, preset.CollisionDamageStartSpeed) : 0f;
            if (impactSpeed <= threshold || Time.time < _nextMountedImpactFeedbackTime)
                return;

            _nextMountedImpactFeedbackTime = Time.time + 0.12f;
            Vector3 impactPoint = _vehicleMotor.LastBlockingImpactPoint;
            Vector3 impactNormal = _vehicleMotor.LastBlockingImpactNormal;
            GlobalPhysicsStateManager.QueueKinematicImpact(_transportBody, impactPoint, impactNormal, impactSpeed);
            ApplyTransportCollisionImpact(impactSpeed, impactPoint, impactNormal);
        }

        private void PrepareMountedKinematicBody()
        {
            if (_transportBody == null)
                return;

            if (!_hasCachedBodyDamping)
            {
                _cachedBodyWasKinematic = _transportBody.isKinematic;
                _hasCachedBodyDamping = true;
            }

            _transportBody.isKinematic = true;
            _transportBody.linearVelocity = Vector3.zero;
            _transportBody.angularVelocity = Vector3.zero;
            if (_vehicleMotor != null)
                _vehicleMotor.ResetRuntimeState();
        }

        private float ResolveConfiguredSuitEnergyDrainPerSecond()
        {
            ResolveVehicleUpgradeModule();

            float baseDrain = preset != null ? Mathf.Max(0f, preset.EnergyDrainPerSecond) : 0f;
            float drainScale = _vehicleUpgradeModule != null
                ? Mathf.Max(0.1f, _vehicleUpgradeModule.EnergyDrainScale)
                : 1f;
            float abyssalOverstrainMultiplier = _riderMovement != null
                ? _riderMovement.CurrentAbyssalCounterDriveEnergyMultiplier
                : 1f;
            return baseDrain * drainScale * abyssalOverstrainMultiplier;
        }

        private float ResolveConfiguredDriveChargeDrainPerSecond()
        {
            ResolveVehicleUpgradeModule();

            float baseDrain = preset != null ? Mathf.Max(0f, preset.DriveChargeDrainPerSecond) : 0f;
            float drainScale = _vehicleUpgradeModule != null
                ? Mathf.Max(0.1f, _vehicleUpgradeModule.ChargeDrainScale)
                : 1f;
            return baseDrain * drainScale;
        }

        private void DispatchIntegrityChanged(float prev, float next, DamageSignal signal)
        {
            for (int i = 0; i < _damageReceivers.Count; i++)
                _damageReceivers[i].OnIntegrityChanged(prev, next, signal);
        }

        private void DispatchPowerChanged(float prev, float next, DamageSignal signal)
        {
            for (int i = 0; i < _damageReceivers.Count; i++)
                _damageReceivers[i].OnPowerChanged(prev, next, signal);
        }

        private void DispatchClarityChanged(float prev, float next, DamageSignal signal)
        {
            for (int i = 0; i < _damageReceivers.Count; i++)
                _damageReceivers[i].OnClarityChanged(prev, next, signal);
        }

        private void DispatchTraumaThresholdCrossed(TraumaLevel level)
        {
            if (level == TraumaLevel.None)
                return;

            for (int i = 0; i < _damageReceivers.Count; i++)
                _damageReceivers[i].OnTraumaThresholdCrossed(level);
        }

        private DamageSignal BuildDamageSignal(
            float impactSpeed,
            Vector3 hitPoint,
            uint damageType,
            float previousIntegrityNormalized,
            float nextIntegrityNormalized)
        {
            DamageSignal signal = default;
            signal.magnitude = Mathf.Max(0f, impactSpeed);
            signal.localPoint = _cachedTransform != null
                ? (float3)_cachedTransform.InverseTransformPoint(hitPoint)
                : float3.zero;
            signal.damageType = damageType;
            signal.integrityDelta = (byte)Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Abs(nextIntegrityNormalized - previousIntegrityNormalized) * byte.MaxValue),
                0,
                byte.MaxValue);
            signal.depth = _riderSurvival != null ? Mathf.Max(0f, _riderSurvival.Depth) : 0f;
            signal.sourceID = DamageSourceIds.MountableTransport;
            return signal;
        }

        private static float ResolvePowerChannel(float integrityNormalized)
        {
            return integrityNormalized >= 0.4f
                ? 1f
                : Mathf.Clamp01(integrityNormalized / 0.4f);
        }

        private static TraumaLevel ResolveTraumaLevel(float integrityNormalized, float damageT)
        {
            if (integrityNormalized <= 0.0001f || damageT >= 0.98f)
                return TraumaLevel.Catastrophic;

            if (integrityNormalized < 0.4f || damageT >= 0.72f)
                return TraumaLevel.Critical;

            if (integrityNormalized < 0.65f || damageT >= 0.42f)
                return TraumaLevel.Significant;

            return damageT > 0.05f
                ? TraumaLevel.Minor
                : TraumaLevel.None;
        }

        private void BreakTransport()
        {
            if (_isBroken)
                return;

            _isBroken = true;
            _transportActive = false;
            _currentThrottle = 0f;
            _currentIntegrity = 0f;

            if (_mounted)
                DismountRider(true);
        }

        private void DismountRiderInternal(bool placeRiderAtExit, bool applyEvaHandoff, bool transferTowToTransport)
        {
            if (!_mounted)
                return;

            ClearMacroFloraEntanglement();

            Vector3 exitPosition = _riderBody != null
                ? _riderBody.position
                : (_riderTransform != null ? _riderTransform.position : _cachedTransform.position);
            Quaternion exitRotation = _riderTransform != null ? _riderTransform.rotation : _cachedTransform.rotation;
            if (placeRiderAtExit)
                ResolveDismountPose(out exitPosition, out exitRotation);

            Vector3 exitVelocity = Vector3.zero;
            bool hasExitVelocity = applyEvaHandoff && TryResolveRiderExitVelocity(exitPosition, out exitVelocity);
            if (placeRiderAtExit)
                MoveRiderToDismountPose(exitPosition, exitRotation);

            if (_riderTransportCoordinator != null)
                _riderTransportCoordinator.ClearExternalTransportSource(this);

            RestoreRiderInteraction();
            UnsubscribeMountedInput();
            RestoreInteractionCollider();
            if (hasExitVelocity)
                ApplyRiderExitVelocity(exitVelocity);
            if (transferTowToTransport)
                TryTransferTowHandoffToTransport(_riderMovement);

            PlayTransportOneShot(dismountSound);
            ClearRiderReferences();
            TryRestoreBodyFromMountedDrive();

            _mounted = false;
            _transportActive = false;
            _currentThrottle = 0f;
            ResetPlatformMotionCache();
        }

        private bool TryResolveRiderExitVelocity(Vector3 exitPosition, out Vector3 exitVelocity)
        {
            exitVelocity = Vector3.zero;
            if (_riderBody == null)
                return false;

            Vector3 riderPosition = _riderTransform != null ? _riderTransform.position : _riderBody.position;
            Vector3 platformVelocityAtRider = GetPlatformPointVelocity(riderPosition);
            Vector3 riderRelativeVelocity = _riderBody.linearVelocity - platformVelocityAtRider;
            Vector3 platformVelocityAtExit = GetPlatformPointVelocity(exitPosition);
            Vector3 candidateVelocity = platformVelocityAtExit + riderRelativeVelocity;
            if (!IsFiniteVector(candidateVelocity))
                return false;

            exitVelocity = candidateVelocity;
            return true;
        }

        private void ApplyRiderExitVelocity(Vector3 exitVelocity)
        {
            if (_riderBody == null || !IsFiniteVector(exitVelocity))
                return;

            _riderBody.linearVelocity = HectonPlayerMotor.SafeVelocity(exitVelocity, _riderBody.linearVelocity);
        }

        private void BeginEmergencyBailoutDrift(Vector3 inheritedVelocity, float severity)
        {
            if (_transportBody == null)
                return;

            if (!_hasCachedBodyDamping)
            {
                _cachedBodyWasKinematic = _transportBody.isKinematic;
                _hasCachedBodyDamping = true;
            }

            if (_transportBody.isKinematic)
                _transportBody.isKinematic = false;

            _transportBody.WakeUp();
            _transportBody.linearVelocity = HectonPlayerMotor.SafeVelocity(
                inheritedVelocity * Mathf.Lerp(0.88f, 1.04f, Mathf.Clamp01(severity)),
                _transportBody.linearVelocity);
            _transportBody.angularVelocity = HectonPlayerMotor.SafeVelocity(
                new Vector3(0f, Mathf.Lerp(0.6f, 2.2f, Mathf.Clamp01(severity)), 0f),
                _transportBody.angularVelocity);
            _bailoutDriftTimer = bailoutDriftDuration;
            ResetPlatformMotionCache();
        }

        private bool TryTransferTowHandoffToTransport(HectonPlayerMovement riderMovement)
        {
            if (riderMovement == null || !riderMovement.HasActiveTowCable || _transportBody == null)
                return false;

            Transform platformTransform = PlatformTransform;
            if (platformTransform == null)
                return false;

            PrepareTowHandoffBody();
            return riderMovement.TryTransferHeavyTowToTransport(_transportBody, platformTransform);
        }

        private void PrepareTowHandoffBody()
        {
            if (_transportBody == null)
                return;

            if (!_hasCachedBodyDamping)
            {
                _cachedBodyWasKinematic = _transportBody.isKinematic;
                _hasCachedBodyDamping = true;
            }

            if (_transportBody.isKinematic)
                _transportBody.isKinematic = false;

            _transportBody.WakeUp();
            _bailoutDriftTimer = math.max(_bailoutDriftTimer, bailoutDriftDuration);
            ResetPlatformMotionCache();
        }

        private void UpdatePlatformMotionCache(float fixedDeltaTime)
        {
            Transform platformTransform = PlatformTransform;
            if (platformTransform == null || !TryResolveSafeReciprocal(fixedDeltaTime, out float inverseFixedDeltaTime))
            {
                ResetPlatformMotionCache();
                return;
            }

            Vector3 currentPosition = platformTransform.position;
            Quaternion currentRotation = platformTransform.rotation;
            if (!_platformMotionInitialized)
            {
                _platformMotionInitialized = true;
                _previousPlatformPosition = currentPosition;
                _previousPlatformRotation = currentRotation;
                _platformLinearVelocity = Vector3.zero;
                _platformAngularVelocity = Vector3.zero;
                UpdatePresentationTransportBoost();
                return;
            }

            Vector3 candidateLinearVelocity = (currentPosition - _previousPlatformPosition) * inverseFixedDeltaTime;
            _platformLinearVelocity = HectonPlayerMotor.SafeVelocity(candidateLinearVelocity, _platformLinearVelocity);
            Quaternion deltaRotation = currentRotation * Quaternion.Inverse(_previousPlatformRotation);
            deltaRotation.ToAngleAxis(out float angleDegrees, out Vector3 axis);
            if (float.IsNaN(axis.x) || axis.sqrMagnitude <= 0.000001f || angleDegrees <= 0.0001f)
            {
                _platformAngularVelocity = Vector3.zero;
            }
            else
            {
                if (angleDegrees > 180f)
                    angleDegrees -= 360f;

                float angularSpeed = angleDegrees * Mathf.Deg2Rad * inverseFixedDeltaTime;
                Vector3 candidateAngularVelocity = axis.normalized * angularSpeed;
                _platformAngularVelocity = HectonPlayerMotor.SafeVelocity(candidateAngularVelocity, _platformAngularVelocity);
            }

            UpdatePresentationTransportBoost();
            _previousPlatformPosition = currentPosition;
            _previousPlatformRotation = currentRotation;
        }

        private void ResetPlatformMotionCache()
        {
            Transform platformTransform = PlatformTransform;
            _platformMotionInitialized = platformTransform != null;
            _previousPlatformPosition = platformTransform != null ? platformTransform.position : Vector3.zero;
            _previousPlatformRotation = platformTransform != null ? platformTransform.rotation : Quaternion.identity;
            _platformLinearVelocity = Vector3.zero;
            _platformAngularVelocity = Vector3.zero;
            ResetInertialGhostHistory();
        }

        private void TryRestoreBodyFromBailoutDrift()
        {
            if (_transportBody == null || !_hasCachedBodyDamping || _bailoutDriftTimer > 0f)
                return;

            if (!_isBroken)
                _transportBody.isKinematic = _cachedBodyWasKinematic;
        }

        private void TryRestoreBodyFromMountedDrive()
        {
            if (_transportBody == null || !_hasCachedBodyDamping || _isBroken || _bailoutDriftTimer > 0f)
                return;

            _transportBody.isKinematic = _cachedBodyWasKinematic;
        }

        private void ApplyBailoutDriftDamping(float fixedDeltaTime)
        {
            if (_transportBody == null || fixedDeltaTime <= 0f)
                return;

            float linearDenominator = ResolvePositiveFiniteDenominator(1f + bailoutLinearDamping * fixedDeltaTime);
            float angularDenominator = ResolvePositiveFiniteDenominator(1f + bailoutAngularDamping * fixedDeltaTime);
            Vector3 dampedLinearVelocity = _transportBody.linearVelocity / linearDenominator;
            Vector3 dampedAngularVelocity = _transportBody.angularVelocity / angularDenominator;
            _transportBody.linearVelocity = HectonPlayerMotor.SafeVelocity(dampedLinearVelocity, _transportBody.linearVelocity);
            _transportBody.angularVelocity = HectonPlayerMotor.SafeVelocity(dampedAngularVelocity, _transportBody.angularVelocity);
        }

        private void RebuildPromptCache()
        {
            if (preset != null)
            {
                _cachedMountText = string.IsNullOrWhiteSpace(preset.MountInteractText)
                    ? DefaultMountText
                    : preset.MountInteractText;
                _cachedDismountText = string.IsNullOrWhiteSpace(preset.DismountInteractText)
                    ? DefaultDismountText
                    : preset.DismountInteractText;
                return;
            }

            _cachedMountText = DefaultMountText;
            _cachedDismountText = DefaultDismountText;
        }

        private float ResolveMountedDriveMaxSpeed(float throttleOutput)
        {
            float speedMultiplier = preset != null ? math.max(1f, preset.SpeedMultiplier) : 1f;
            float throttleSpeed = math.lerp(1.5f, speedMultiplier * 6f, math.saturate(throttleOutput));
            ResolveVehicleUpgradeModule();
            if (_vehicleUpgradeModule != null)
                throttleSpeed *= math.max(1f, _vehicleUpgradeModule.MaxSpeedMultiplier);

            return math.max(1.5f, throttleSpeed);
        }

        private void TryRegister()
        {
            if (_registered)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Player);
            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registered = GlobalRegistry.FixedTickables.Contains(this) ||
                          GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            if (GlobalRegistry.FixedTickables.Contains(this))
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Player);

            if (GlobalRegistry.Updatables.Contains(this))
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);

            _registered = false;
        }

        private void TryRegisterOriginShiftListener()
        {
            if (_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void TryUnregisterOriginShiftListener()
        {
            if (!_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _registeredOriginShiftListener = false;
        }

        private void RefreshMountedInputSubscription()
        {
            IInputService currentInputManager = GlobalRegistry.Input;
            if (ReferenceEquals(_subscribedInputManager, currentInputManager))
                return;

            UnsubscribeMountedInput();
            if (currentInputManager == null)
                return;

            currentInputManager.OnInteract += HandleMountedInteract;
            _subscribedInputManager = currentInputManager;
        }

        private void UnsubscribeMountedInput()
        {
            if (_subscribedInputManager == null)
                return;

            _subscribedInputManager.OnInteract -= HandleMountedInteract;
            _subscribedInputManager = null;
        }

        private void HandleMountedInteract()
        {
            if (_mounted)
                DismountRider(true);
        }

        private void RestoreRiderInteraction()
        {
            if (_riderInteraction != null)
                _riderInteraction.enabled = _riderInteractionWasEnabled;

            _riderInteractionWasEnabled = false;
        }

        private void RestoreInteractionCollider()
        {
            if (_interactionCollider != null)
                _interactionCollider.enabled = _interactionColliderWasEnabled;

            _interactionColliderWasEnabled = false;
        }

        private void PlayTransportOneShot(AudioClip clip)
        {
            if (clip == null)
                return;

            if (Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
                audio.PlayAtPoint(clip, _cachedTransform.position, transportAudioVolume);
        }

        private void UpdatePresentationTransportBoost()
        {
            if (!_mounted || preset == null)
            {
                _presentationTransportBoost01 = 0f;
                return;
            }

            RecordInertialGhostVelocity(_platformLinearVelocity);
            Vector3 ghostVelocity = ResolveGhostVelocity();
            Vector3 perceivedVelocity = Vector3.Lerp(_platformLinearVelocity, ghostVelocity, InertialGhostBlend);
            float speedReference = Mathf.Max(0.1f, preset.PropulsionForceReference * 0.01f);
            float speedBoost = Mathf.Clamp01(perceivedVelocity.magnitude / speedReference);
            float throttleBoost = Mathf.Clamp01(GetTransportPropulsionForce() / Mathf.Max(0.01f, preset.PropulsionForceReference));
            _presentationTransportBoost01 = Mathf.Clamp01(Mathf.Max(throttleBoost, speedBoost));
        }

        private void RecordInertialGhostVelocity(Vector3 velocity)
        {
            _platformVelocityGhostHistory[_platformVelocityGhostWriteIndex] = HectonPlayerMotor.SafeVelocity(velocity);
            _platformVelocityGhostWriteIndex = (_platformVelocityGhostWriteIndex + 1) % _platformVelocityGhostHistory.Length;
            if (_platformVelocityGhostSampleCount < _platformVelocityGhostHistory.Length)
                _platformVelocityGhostSampleCount++;
        }

        private Vector3 ResolveGhostVelocity()
        {
            if (_platformVelocityGhostSampleCount < _platformVelocityGhostHistory.Length)
                return _platformLinearVelocity;

            return _platformVelocityGhostHistory[_platformVelocityGhostWriteIndex];
        }

        private void ResetInertialGhostHistory()
        {
            _presentationTransportBoost01 = 0f;
            _platformVelocityGhostWriteIndex = 0;
            _platformVelocityGhostSampleCount = 0;
            for (int i = 0; i < _platformVelocityGhostHistory.Length; i++)
                _platformVelocityGhostHistory[i] = Vector3.zero;
        }

        private static float ResolveBlendFactor(float sharpness, float deltaTime)
        {
            return Mathf.Clamp01(Mathf.Max(0f, sharpness) * Mathf.Max(0f, deltaTime));
        }

        private static bool TryResolveSafeReciprocal(float value, out float reciprocal)
        {
            if (!float.IsFinite(value) || math.abs(value) <= 0.0001f)
            {
                reciprocal = 0f;
                return false;
            }

            reciprocal = 1f / value;
            return float.IsFinite(reciprocal);
        }

        private static float ResolvePositiveFiniteDenominator(float value)
        {
            return float.IsFinite(value) ? math.max(value, 0.001f) : 0.001f;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                     float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField] private bool _debugMounted;
        [SerializeField] private float _debugThrottle;
#endif

#if UNITY_EDITOR
        private void OnValidate()
        {
            entanglementTetherYieldLimit = Mathf.Max(100f, entanglementTetherYieldLimit);
            entanglementStressThrottleThreshold = Mathf.Clamp(entanglementStressThrottleThreshold, 0.1f, 1f);
            entanglementShearDamagePerSecond = Mathf.Max(0f, entanglementShearDamagePerSecond);
            entanglementMicroFracturePerSecond = Mathf.Max(0f, entanglementMicroFracturePerSecond);
            entanglementMicroFractureLimit = Mathf.Max(1f, entanglementMicroFractureLimit);
            entanglementDepthPenaltyPerMicroFractureMeters = Mathf.Max(0f, entanglementDepthPenaltyPerMicroFractureMeters);
            entanglementStressSignalInterval = Mathf.Max(0.02f, entanglementStressSignalInterval);
            cavitationLowSpeedThreshold = Mathf.Max(0.05f, cavitationLowSpeedThreshold);
            cavitationEngineDamagePerSecond = Mathf.Max(0f, cavitationEngineDamagePerSecond);
            cavitationEventInterval = Mathf.Max(0.02f, cavitationEventInterval);
            cavitationShockwaveRadius = Mathf.Max(CavitationShockwaveMinRadiusMeters, cavitationShockwaveRadius);
            cavitationShockwaveAcceleration = Mathf.Max(0f, cavitationShockwaveAcceleration);
            ResolveAnchorCache();
            BindPresetToFeelContract();
            RebuildPromptCache();
        }
#endif
    }
}

