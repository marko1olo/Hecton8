using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Input;
using Hecton8.Interaction;
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
    [AddComponentMenu("Hecton8/Gameplay/Transport/Mountable Player Transport")]
    public sealed class MountablePlayerTransport : MonoBehaviour, IInteractable, ITickable, IFixedTickable, IPlayerTransportSource, IPlayerTransportLifecycleOwner, ITransportPlatform
    {
        private const string DefaultMountText = "Board Transport";
        private const string DefaultDismountText = "Dismount";

        [Header("-- Preset ---------------------------")]
        [Tooltip("Shared transport preset driving locomotion, prompts, and feel.")]
        [SerializeField] private PlayerTransportPreset preset;

        [Header("-- Anchors --------------------------")]
        [Tooltip("Seat or grip anchor representing the rider pose on this transport. Defaults to this transform.")]
        [SerializeField] private Transform riderAnchor;

        [Tooltip("Optional explicit dismount target. If omitted, a right-side offset is used.")]
        [SerializeField] private Transform dismountAnchor;

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
        private PlayerTransportFeelContract _transportFeelContract;
        private VehicleUpgradeModule _vehicleUpgradeModule;
        private bool _registered;
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

        private InputManager _subscribedInputManager;
        private bool _mounted;
        private bool _transportActive;
        private bool _isBroken;
        private bool _lifecycleInitialized;
        private float _currentThrottle;
        private float _currentChargeNormalized = 1f;
        private float _currentIntegrity = -1f;
        private string _cachedMountText = DefaultMountText;
        private string _cachedDismountText = DefaultDismountText;
        private float _bailoutDriftTimer;
        private float _cachedLinearDamping;
        private float _cachedAngularDamping;
        private bool _hasCachedBodyDamping;
        private bool _cachedBodyWasKinematic = true;
        private bool _platformMotionInitialized;
        private Vector3 _platformLinearVelocity;
        private Vector3 _platformAngularVelocity;
        private Vector3 _previousPlatformPosition;
        private Quaternion _previousPlatformRotation = Quaternion.identity;

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

        /// <summary>Current normalized transport integrity.</summary>
        public float TransportIntegrityNormalized => ResolveIntegrityNormalized();

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
            TryGetComponent(out _transportFeelContract);
            TryGetComponent(out _vehicleUpgradeModule);
            ResolveAnchorCache();
            BindPresetToFeelContract();
            RebuildPromptCache();
            EnsureLifecycleInitialized();
            ResetPlatformMotionCache();
        }

        private void OnEnable()
        {
            TryRegister();
            ResolveAnchorCache();
            BindPresetToFeelContract();
            ResolveVehicleUpgradeModule();
            RebuildPromptCache();
            EnsureLifecycleInitialized();
            ResetPlatformMotionCache();
        }

        private void OnDisable()
        {
            ForceReleaseMountedRider();
            TryUnregister();
        }

        private void OnDestroy()
        {
            ForceReleaseMountedRider();
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

            bool underwaterDriveAllowed = !preset.UnderwaterOnly ||
                (_riderMovement != null && _riderMovement.CurrentLocomotionMode == PlayerLocomotionMode.UnderwaterSwim);
            if (!underwaterDriveAllowed)
            {
                _transportActive = false;
                _currentThrottle = 0f;
                return;
            }

            InputManager inputManager = InputManager.Instance;
            Vector2 moveInput = inputManager != null ? inputManager.MoveInput : Vector2.zero;
            float verticalInput = inputManager != null ? inputManager.VerticalMovementInput : 0f;
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
                AlignTransportToRider(fixedDeltaTime);
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

            _transportBody.linearDamping = bailoutLinearDamping;
            _transportBody.angularDamping = bailoutAngularDamping;
            _transportBody.AddForce(Vector3.down * bailoutSinkAcceleration * fixedDeltaTime, ForceMode.VelocityChange);
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
            return Mathf.Clamp01(GetTransportPropulsionForce() / reference);
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
            if (_currentIntegrity <= 0.0001f)
                BreakTransport();
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
            AlignTransportToRider(0f);
            ResetPlatformMotionCache();
            ZeroRiderVelocity();
            PlayTransportOneShot(mountSound);
        }

        private void DismountRider(bool placeRiderAtExit)
        {
            DismountRiderInternal(placeRiderAtExit, zeroRiderVelocity: true);
        }

        internal void TriggerEmergencyBailoutDrift(Vector3 inheritedVelocity, float severity)
        {
            if (!_mounted)
            {
                BeginEmergencyBailoutDrift(inheritedVelocity, severity);
                return;
            }

            DismountRiderInternal(placeRiderAtExit: false, zeroRiderVelocity: false);
            BeginEmergencyBailoutDrift(inheritedVelocity, severity);
        }

        private void ForceReleaseMountedRider()
        {
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
                float followT = 1f - Mathf.Exp(-preset.OrientationFollowSharpness * fixedDeltaTime);
                targetRotation = Quaternion.Slerp(_cachedTransform.rotation, targetRotation, followT);
            }

            Vector3 riderPosition = _riderTransform.position;
            Vector3 targetPosition = riderPosition - targetRotation * _riderAnchorLocalPosition;

            if (_transportBody != null && _transportBody.isKinematic)
            {
                _transportBody.MoveRotation(targetRotation);
                _transportBody.MovePosition(targetPosition);
            }
            else
            {
                _cachedTransform.SetPositionAndRotation(targetPosition, targetRotation);
            }
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
            float blend = 1f - Mathf.Exp(-sharpness * deltaTime);
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
            if (_riderTransform == null)
                return;

            Vector3 targetPosition;
            Quaternion targetRotation;
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

            if (_riderBody != null)
            {
                _riderBody.position = targetPosition;
                _riderBody.rotation = targetRotation;
            }

            _riderTransform.SetPositionAndRotation(targetPosition, targetRotation);
        }

        private void ZeroRiderVelocity()
        {
            if (_riderBody == null)
                return;

            _riderBody.linearVelocity = Vector3.zero;
            _riderBody.angularVelocity = Vector3.zero;
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

        private void DismountRiderInternal(bool placeRiderAtExit, bool zeroRiderVelocity)
        {
            if (!_mounted)
                return;

            if (placeRiderAtExit)
                MoveRiderToDismountPoint();

            if (_riderTransportCoordinator != null)
                _riderTransportCoordinator.ClearExternalTransportSource(this);

            RestoreRiderInteraction();
            UnsubscribeMountedInput();
            RestoreInteractionCollider();
            if (zeroRiderVelocity)
                ZeroRiderVelocity();

            PlayTransportOneShot(dismountSound);
            ClearRiderReferences();

            _mounted = false;
            _transportActive = false;
            _currentThrottle = 0f;
            ResetPlatformMotionCache();
        }

        private void BeginEmergencyBailoutDrift(Vector3 inheritedVelocity, float severity)
        {
            if (_transportBody == null)
                return;

            if (!_hasCachedBodyDamping)
            {
                _cachedLinearDamping = _transportBody.linearDamping;
                _cachedAngularDamping = _transportBody.angularDamping;
                _cachedBodyWasKinematic = _transportBody.isKinematic;
                _hasCachedBodyDamping = true;
            }

            if (_transportBody.isKinematic)
                _transportBody.isKinematic = false;

            _transportBody.WakeUp();
            _transportBody.linearVelocity = inheritedVelocity * Mathf.Lerp(0.88f, 1.04f, Mathf.Clamp01(severity));
            _transportBody.angularVelocity = new Vector3(0f, Mathf.Lerp(0.6f, 2.2f, Mathf.Clamp01(severity)), 0f);
            _transportBody.linearDamping = bailoutLinearDamping;
            _transportBody.angularDamping = bailoutAngularDamping;
            _bailoutDriftTimer = bailoutDriftDuration;
            ResetPlatformMotionCache();
        }

        private void UpdatePlatformMotionCache(float fixedDeltaTime)
        {
            Transform platformTransform = PlatformTransform;
            if (platformTransform == null || fixedDeltaTime <= 0f)
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
                return;
            }

            _platformLinearVelocity = (currentPosition - _previousPlatformPosition) / fixedDeltaTime;
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

                _platformAngularVelocity = axis.normalized * (angleDegrees * Mathf.Deg2Rad / fixedDeltaTime);
            }

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
        }

        private void TryRestoreBodyFromBailoutDrift()
        {
            if (_transportBody == null || !_hasCachedBodyDamping || _bailoutDriftTimer > 0f)
                return;

            _transportBody.linearDamping = _cachedLinearDamping;
            _transportBody.angularDamping = _cachedAngularDamping;
            if (!_isBroken)
                _transportBody.isKinematic = _cachedBodyWasKinematic;
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

        private void TryRegister()
        {
            if (_registered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register((ITickable)this);
            tickManager.Register((IFixedTickable)this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
            {
                tickManager.Unregister((ITickable)this);
                tickManager.Unregister((IFixedTickable)this);
            }

            _registered = false;
        }

        private void RefreshMountedInputSubscription()
        {
            InputManager currentInputManager = InputManager.Instance;
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

            if (SpatialAudioManager.TryGetInstance(out SpatialAudioManager audio))
                audio.PlayAtPoint(clip, _cachedTransform.position, transportAudioVolume);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField] private bool _debugMounted;
        [SerializeField] private float _debugThrottle;
#endif

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveAnchorCache();
            BindPresetToFeelContract();
            RebuildPromptCache();
        }
#endif
    }
}
