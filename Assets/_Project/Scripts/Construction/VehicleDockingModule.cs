using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.Power;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Trigger-driven moonpool dock for a single transport. It reuses existing transport charge ownership and
    /// temporarily injects docked cargo crates into the base logistics network without inventing a new cargo system.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(PowerNode))]
    [AddComponentMenu("Hecton8/Construction/Vehicle Docking Module")]
    public sealed class VehicleDockingModule : MonoBehaviour, ITickable, IFixedTickable, IUpdatable, IPowerComponent, IPoolable
    {
        [Header("── Docking ──────────────────")]
        [Tooltip("Optional snap anchor applied when a rigidbody transport is docked. Falls back to this transform.")]
        [SerializeField] private Transform dockAnchor;

        [Tooltip("Deterministic docking travel duration in seconds from trigger capture to hard-lock.")]
        [SerializeField, Range(0.25f, 8f)] private float dockingDurationSeconds = 2f;

        [Tooltip("PD position spring gain used to pull the transport toward the moonpool anchor.")]
        [SerializeField, Min(0f)] private float dockingPositionSpring = 20f;

        [Tooltip("PD position damping gain used to suppress overshoot during magnetic capture.")]
        [SerializeField, Min(0f)] private float dockingPositionDamping = 8f;

        [Tooltip("Maximum total PD force applied by magnetic capture before dividing by docked-body mass.")]
        [SerializeField, Min(1f)] private float maxDockingForce = 65000f;

        [Tooltip("PD rotation spring gain used to align the transport to the moonpool anchor.")]
        [SerializeField, Min(0f)] private float dockingRotationSpring = 18f;

        [Tooltip("PD rotation damping gain used to suppress angular overshoot during magnetic capture.")]
        [SerializeField, Min(0f)] private float dockingRotationDamping = 7f;

        [Tooltip("Position error below which the dock can hard-lock before the duration cap.")]
        [SerializeField, Min(0.001f)] private float dockingCaptureDistanceEpsilon = 0.025f;

        [Tooltip("Rotation error in degrees below which the dock can hard-lock before the duration cap.")]
        [SerializeField, Min(0.01f)] private float dockingCaptureAngleEpsilonDegrees = 1f;

        [Tooltip("Normalized transport charge restored per second while the dock is powered.")]
        [SerializeField, Range(0f, 1f)] private float chargeRatePerSecond = 0.2f;

        [Tooltip("When enabled, child cargo crates found on the docked transport become part of the base logistics grid.")]
        [SerializeField] private bool connectDockedCargoToLogistics = true;

        [Header("── Power ───────────────────")]
        [Tooltip("Power draw while the dock is actively charging a transport.")]
        [SerializeField, Range(0f, 400f)] private float chargingPowerDraw = 120f;

        [Tooltip("Grid shedding priority used by this dock.")]
        [SerializeField, Range(0, 100)] private int powerPriority = 35;

        [Header("── Diagnostics ─────────────")]
        [SerializeField] private bool _debugHasPower = true;
        [SerializeField] private bool _debugDockOccupied;
        [SerializeField] private string _debugDockedTransportName;

        // COLD ALLOC: List<StorageCrate>[4] — temporary cargo storage bridge for the currently docked transport — owner: VehicleDockingModule
        private readonly List<StorageCrate> _connectedCargoCrates = new List<StorageCrate>(4);
        // COLD ALLOC: List<StorageCrate>[4] — component query buffer for docked transport cargo discovery — owner: VehicleDockingModule
        private readonly List<StorageCrate> _cargoDiscoveryBuffer = new List<StorageCrate>(4);

        private Transform _cachedTransform;
        private Collider _triggerCollider;
        private PowerNode _powerNode;
        private bool _registered;
        private bool _hasPower = true;
        private bool _activelyCharging;
        private bool _dockingInProgress;
        private bool _isDocked;
        private IPlayerTransportLifecycleOwner _dockedTransport;
        private MonoBehaviour _dockedBehaviour;
        private Transform _dockedTransform;
        private Rigidbody _dockedBody;
        private bool _cachedBodyWasKinematic;
        private bool _cachedBodyUseGravity;
        private RigidbodyConstraints _cachedBodyConstraints;
        private Vector3 _dockingStartPosition;
        private Quaternion _dockingStartRotation = Quaternion.identity;
        private Vector3 _dockingLinearVelocity;
        private Vector3 _dockingAngularVelocityRadians;
        private float _dockingElapsedSeconds;
        private MountablePlayerTransport _mountedTransportLockOwner;
        private int _lastRejectedDockColliderId;

        /// <summary>Continuous draw while charge is actually transferred to a docked transport.</summary>
        public float PowerRating => _activelyCharging ? -chargingPowerDraw : 0f;

        /// <summary>Dock load shedding priority.</summary>
        public int PowerPriority => powerPriority;

        /// <summary>Cached base-grid power state for this dock.</summary>
        public bool HasPower => _hasPower;
        internal bool DebugDockOccupied => _debugDockOccupied;

        private void Awake()
        {
            _cachedTransform = transform;
            _triggerCollider = GetComponent<Collider>();
            _triggerCollider.isTrigger = true;
            _powerNode = GetComponent<PowerNode>();
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            ReleaseDockedTransport();
            TryUnregister();
        }

        private void OnDestroy()
        {
            ReleaseDockedTransport();
            TryUnregister();
        }

        public void OnSpawn()
        {
            _hasPower = true;
            _debugHasPower = true;
            _activelyCharging = false;
            _dockingInProgress = false;
            _isDocked = false;
            _dockingElapsedSeconds = 0f;
            _dockingLinearVelocity = Vector3.zero;
            _dockingAngularVelocityRadians = Vector3.zero;
            _lastRejectedDockColliderId = 0;
            _debugDockOccupied = false;
            _debugDockedTransportName = string.Empty;
            TryRegister();
        }

        public void OnDespawn()
        {
            ReleaseDockedTransport();
            _hasPower = true;
            _debugHasPower = true;
            _activelyCharging = false;
            _dockingInProgress = false;
            _isDocked = false;
            _dockingElapsedSeconds = 0f;
            _dockingLinearVelocity = Vector3.zero;
            _dockingAngularVelocityRadians = Vector3.zero;
            _lastRejectedDockColliderId = 0;
            _debugDockOccupied = false;
            _debugDockedTransportName = string.Empty;
            TryUnregister();
        }

        public void Tick(float deltaTime)
        {
            if (_dockedTransport == null || _dockedBehaviour == null || !_isDocked)
            {
                if (_activelyCharging)
                    _activelyCharging = false;

                return;
            }

            bool nextChargingState = false;
            if (_hasPower && chargeRatePerSecond > 0f && _dockedTransport.CanReceiveTransportCharge)
            {
                float chargeBefore = _dockedTransport.TransportChargeNormalized;
                _dockedTransport.RechargeTransport(chargeRatePerSecond * deltaTime);
                nextChargingState = _dockedTransport.TransportChargeNormalized > chargeBefore + 0.0001f;
            }

            if (_activelyCharging != nextChargingState)
                _activelyCharging = nextChargingState;
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (!_dockingInProgress || _dockedBehaviour == null)
                return;

            AdvanceDockingPose(fixedDeltaTime);
        }

        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            _debugHasPower = hasPower;

            if (!hasPower)
                _activelyCharging = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null)
                return;

            _lastRejectedDockColliderId = 0;
            TryDockFromCollider(other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (other == null || _dockedTransport != null)
                return;

            int colliderId = ResolveColliderRuntimeId(other);
            if (colliderId != 0 && colliderId == _lastRejectedDockColliderId)
                return;

            if (!TryDockFromCollider(other) && colliderId != 0)
                _lastRejectedDockColliderId = colliderId;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null || _dockedBehaviour == null)
                return;

            int colliderId = ResolveColliderRuntimeId(other);
            if (colliderId != 0 && colliderId == _lastRejectedDockColliderId)
                _lastRejectedDockColliderId = 0;

            MonoBehaviour ownerBehaviour;
            IPlayerTransportLifecycleOwner owner;
            if (!TryResolveTransportLifecycleOwner(other, out owner, out ownerBehaviour))
                return;

            if (!ReferenceEquals(ownerBehaviour, _dockedBehaviour) && !ReferenceEquals(owner, _dockedTransport))
                return;

            ReleaseDockedTransport();
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private bool TryDockFromCollider(Collider other)
        {
            if (_dockedTransport != null)
                return false;

            MonoBehaviour ownerBehaviour;
            IPlayerTransportLifecycleOwner owner;
            if (!TryResolveTransportLifecycleOwner(other, out owner, out ownerBehaviour))
                return false;

            DockTransport(owner, ownerBehaviour);
            return true;
        }

        private void DockTransport(IPlayerTransportLifecycleOwner transportOwner, MonoBehaviour transportBehaviour)
        {
            if (transportOwner == null || transportBehaviour == null)
                return;

            _dockedTransport = transportOwner;
            _dockedBehaviour = transportBehaviour;
            _dockedTransform = transportBehaviour.transform;
            _debugDockOccupied = true;
            _debugDockedTransportName = transportBehaviour.name;

            ResolveDockedBody(transportBehaviour);
            BeginDockingControlLock(transportBehaviour);
            _dockingElapsedSeconds = 0f;
            if (_dockedBody == null)
            {
                _dockingLinearVelocity = Vector3.zero;
                _dockingAngularVelocityRadians = Vector3.zero;
            }
            _dockingInProgress = true;
            _isDocked = false;
        }

        private void ReleaseDockedTransport()
        {
            DisconnectDockedCargoCrates();

            if (_dockedBody != null)
            {
                _dockedBody.linearVelocity = Vector3.zero;
                _dockedBody.angularVelocity = Vector3.zero;
                _dockedBody.isKinematic = _cachedBodyWasKinematic;
                _dockedBody.useGravity = _cachedBodyUseGravity;
                _dockedBody.constraints = _cachedBodyConstraints;
            }

            GlobalPhysicsStateManager.UnregisterDockConnection(this);
            EndDockingControlLock();
            _dockedBody = null;
            _dockedTransport = null;
            _dockedBehaviour = null;
            _dockedTransform = null;
            _activelyCharging = false;
            _dockingInProgress = false;
            _isDocked = false;
            _dockingElapsedSeconds = 0f;
            _dockingLinearVelocity = Vector3.zero;
            _dockingAngularVelocityRadians = Vector3.zero;
            _lastRejectedDockColliderId = 0;
            _debugDockOccupied = false;
            _debugDockedTransportName = string.Empty;
        }

        private void ResolveDockedBody(MonoBehaviour transportBehaviour)
        {
            _dockedBody = null;
            if (transportBehaviour == null)
                return;

            if (!transportBehaviour.TryGetComponent(out _dockedBody))
                _dockedBody = transportBehaviour.GetComponentInParent<Rigidbody>();

            if (_dockedBody == null)
                return;

            _cachedBodyWasKinematic = _dockedBody.isKinematic;
            _cachedBodyUseGravity = _dockedBody.useGravity;
            _cachedBodyConstraints = _dockedBody.constraints;
            _dockingStartPosition = _dockedBody.position;
            _dockingStartRotation = _dockedBody.rotation;
            _dockingLinearVelocity = HectonPlayerMotor.SafeVelocity(_dockedBody.linearVelocity);
            _dockingAngularVelocityRadians = HectonPlayerMotor.SafeVelocity(_dockedBody.angularVelocity);
            _dockedBody.linearVelocity = Vector3.zero;
            _dockedBody.angularVelocity = Vector3.zero;
            _dockedBody.useGravity = false;
        }

        private void SnapDockedBodyToAnchor()
        {
            if (_dockedBehaviour == null || _dockedTransform == null)
                return;

            Transform anchor = dockAnchor != null ? dockAnchor : _cachedTransform;
            if (_dockedBody != null)
            {
                _dockedBody.MovePosition(anchor.position);
                _dockedBody.MoveRotation(anchor.rotation);
                return;
            }

            _dockedTransform.SetPositionAndRotation(anchor.position, anchor.rotation);
        }

        private void AdvanceDockingPose(float fixedDeltaTime)
        {
            float duration = Mathf.Max(0.25f, dockingDurationSeconds);
            float safeDeltaTime = Mathf.Max(0.0001f, fixedDeltaTime);
            _dockingElapsedSeconds = Mathf.Min(duration, _dockingElapsedSeconds + safeDeltaTime);
            Transform anchor = dockAnchor != null ? dockAnchor : _cachedTransform;
            Vector3 anchorPosition = anchor.position;
            Quaternion anchorRotation = anchor.rotation;
            Vector3 evaluatedPosition = anchorPosition;
            Quaternion evaluatedRotation = anchorRotation;
            bool hasEvaluatedPose = false;

            if (_dockedBody != null)
            {
                Vector3 currentPosition = _dockedBody.position;
                Quaternion currentRotation = _dockedBody.rotation;
                Vector3 positionError = anchorPosition - currentPosition;
                float bodyMass = Mathf.Max(1f, _dockedBody.mass);
                Vector3 positionForce = ((positionError * dockingPositionSpring) - (_dockingLinearVelocity * dockingPositionDamping)) * bodyMass;
                Vector3 positionAcceleration = ClampVectorMagnitude(positionForce, maxDockingForce) / bodyMass;
                _dockingLinearVelocity = HectonPlayerMotor.SafeVelocity(_dockingLinearVelocity + (positionAcceleration * safeDeltaTime));
                Vector3 nextPosition = currentPosition + (_dockingLinearVelocity * safeDeltaTime);

                Vector3 rotationErrorRadians = ResolveRotationErrorRadians(currentRotation, anchorRotation);
                Vector3 angularAcceleration = (rotationErrorRadians * dockingRotationSpring) - (_dockingAngularVelocityRadians * dockingRotationDamping);
                _dockingAngularVelocityRadians = HectonPlayerMotor.SafeVelocity(_dockingAngularVelocityRadians + (angularAcceleration * safeDeltaTime));
                Quaternion nextRotation = IntegrateAngularVelocity(currentRotation, _dockingAngularVelocityRadians, safeDeltaTime);

                _dockedBody.linearVelocity = _dockingLinearVelocity;
                _dockedBody.angularVelocity = _dockingAngularVelocityRadians;
                _dockedBody.MovePosition(nextPosition);
                _dockedBody.MoveRotation(nextRotation);
                evaluatedPosition = nextPosition;
                evaluatedRotation = nextRotation;
                hasEvaluatedPose = true;
            }
            else if (_dockedTransform != null)
            {
                Vector3 currentPosition = _dockedTransform.position;
                Quaternion currentRotation = _dockedTransform.rotation;
                Vector3 positionError = anchorPosition - currentPosition;
                Vector3 positionForce = (positionError * dockingPositionSpring) - (_dockingLinearVelocity * dockingPositionDamping);
                Vector3 positionAcceleration = ClampVectorMagnitude(positionForce, maxDockingForce);
                _dockingLinearVelocity = HectonPlayerMotor.SafeVelocity(_dockingLinearVelocity + (positionAcceleration * safeDeltaTime));
                Vector3 rotationErrorRadians = ResolveRotationErrorRadians(currentRotation, anchorRotation);
                Vector3 angularAcceleration = (rotationErrorRadians * dockingRotationSpring) - (_dockingAngularVelocityRadians * dockingRotationDamping);
                _dockingAngularVelocityRadians = HectonPlayerMotor.SafeVelocity(_dockingAngularVelocityRadians + (angularAcceleration * safeDeltaTime));
                Vector3 nextPosition = currentPosition + (_dockingLinearVelocity * safeDeltaTime);
                Quaternion nextRotation = IntegrateAngularVelocity(currentRotation, _dockingAngularVelocityRadians, safeDeltaTime);
                _dockedTransform.SetPositionAndRotation(
                    nextPosition,
                    nextRotation);
                evaluatedPosition = nextPosition;
                evaluatedRotation = nextRotation;
                hasEvaluatedPose = true;
            }

            bool durationElapsed = _dockingElapsedSeconds >= duration - 0.0001f;
            bool positionCaptured = hasEvaluatedPose &&
                                    Vector3.SqrMagnitude(anchorPosition - evaluatedPosition) <=
                                    dockingCaptureDistanceEpsilon * dockingCaptureDistanceEpsilon;
            bool rotationCaptured = hasEvaluatedPose &&
                                    Quaternion.Angle(evaluatedRotation, anchorRotation) <= dockingCaptureAngleEpsilonDegrees;
            if (!durationElapsed && (!positionCaptured || !rotationCaptured))
                return;

            FinalizeDockedTransport();
        }

        private void FinalizeDockedTransport()
        {
            SnapDockedBodyToAnchor();
            if (_dockedBody != null)
            {
                _dockedBody.linearVelocity = Vector3.zero;
                _dockedBody.angularVelocity = Vector3.zero;
                _dockedBody.isKinematic = true;
                _dockedBody.useGravity = false;
                GlobalPhysicsStateManager.RegisterDockConnection(this, _dockedBody);
            }

            ConnectDockedCargoCrates();
            _dockingInProgress = false;
            _isDocked = true;
            _dockingLinearVelocity = Vector3.zero;
            _dockingAngularVelocityRadians = Vector3.zero;
        }

        private static Vector3 ClampVectorMagnitude(Vector3 vector, float maxMagnitude)
        {
            float safeMaxMagnitude = Mathf.Max(0f, maxMagnitude);
            if (safeMaxMagnitude <= 0f)
                return Vector3.zero;

            float sqrMagnitude = vector.sqrMagnitude;
            float maxSqrMagnitude = safeMaxMagnitude * safeMaxMagnitude;
            if (sqrMagnitude <= maxSqrMagnitude || sqrMagnitude <= 0.000001f)
                return vector;

            return vector * (safeMaxMagnitude / Mathf.Sqrt(sqrMagnitude));
        }

        private static Vector3 ResolveRotationErrorRadians(Quaternion currentRotation, Quaternion targetRotation)
        {
            Quaternion error = targetRotation * Quaternion.Inverse(currentRotation);
            error.ToAngleAxis(out float angleDegrees, out Vector3 axis);
            if (angleDegrees > 180f)
                angleDegrees -= 360f;

            if (!IsFiniteVector(axis) || axis.sqrMagnitude <= 0.000001f || !float.IsFinite(angleDegrees))
                return Vector3.zero;

            return axis.normalized * (angleDegrees * Mathf.Deg2Rad);
        }

        private static Quaternion IntegrateAngularVelocity(Quaternion currentRotation, Vector3 angularVelocityRadians, float deltaTime)
        {
            float angularSpeed = angularVelocityRadians.magnitude;
            if (angularSpeed <= 0.0001f || !float.IsFinite(angularSpeed))
                return currentRotation;

            Vector3 axis = angularVelocityRadians / angularSpeed;
            return Quaternion.AngleAxis(angularSpeed * Mathf.Rad2Deg * Mathf.Max(0f, deltaTime), axis) * currentRotation;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                     float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
        }

        private void BeginDockingControlLock(MonoBehaviour transportBehaviour)
        {
            _mountedTransportLockOwner = null;
            if (transportBehaviour == null)
                return;

            if (!transportBehaviour.TryGetComponent(out _mountedTransportLockOwner))
                _mountedTransportLockOwner = transportBehaviour.GetComponentInParent<MountablePlayerTransport>();

            if (_mountedTransportLockOwner != null)
                _mountedTransportLockOwner.BeginDockControlLock();
        }

        private void EndDockingControlLock()
        {
            if (_mountedTransportLockOwner == null)
                return;

            _mountedTransportLockOwner.EndDockControlLock();
            _mountedTransportLockOwner = null;
        }

        private void ConnectDockedCargoCrates()
        {
            DisconnectDockedCargoCrates();

            if (!connectDockedCargoToLogistics || _dockedBehaviour == null || _powerNode == null)
                return;

            _cargoDiscoveryBuffer.Clear();
            _dockedBehaviour.GetComponentsInChildren(true, _cargoDiscoveryBuffer);

            for (int i = 0; i < _cargoDiscoveryBuffer.Count; i++)
            {
                StorageCrate crate = _cargoDiscoveryBuffer[i];
                if (crate == null)
                    continue;

                PowerNode crateNode = crate.GetComponent<PowerNode>() ?? crate.GetComponentInParent<PowerNode>();
                if (crateNode != null && crateNode.Grid != null)
                    continue;

                BaseLogisticsNetwork.RegisterStorage(crate, _powerNode);
                _connectedCargoCrates.Add(crate);
            }
        }

        private void DisconnectDockedCargoCrates()
        {
            for (int i = 0; i < _connectedCargoCrates.Count; i++)
            {
                StorageCrate crate = _connectedCargoCrates[i];
                if (crate != null)
                    BaseLogisticsNetwork.UnregisterStorage(crate);
            }

            _connectedCargoCrates.Clear();
            _cargoDiscoveryBuffer.Clear();
        }

        private static bool TryResolveTransportLifecycleOwner(
            Collider other,
            out IPlayerTransportLifecycleOwner lifecycleOwner,
            out MonoBehaviour lifecycleBehaviour)
        {
            lifecycleOwner = other.GetComponentInParent<IPlayerTransportLifecycleOwner>();
            lifecycleBehaviour = lifecycleOwner as MonoBehaviour;
            if (lifecycleOwner != null && lifecycleBehaviour != null)
                return true;

            PlayerTransportCoordinator transportCoordinator = other.GetComponentInParent<PlayerTransportCoordinator>();
            if (transportCoordinator != null && transportCoordinator.TryResolveTransportLifecycleOwner(out lifecycleOwner))
            {
                lifecycleBehaviour = lifecycleOwner as MonoBehaviour;
                return lifecycleBehaviour != null;
            }

            lifecycleOwner = null;
            lifecycleBehaviour = null;
            return false;
        }

        private static int ResolveColliderRuntimeId(Collider collider)
        {
            return collider != null
                ? unchecked((int)EntityId.ToULong(collider.GetEntityId()))
                : 0;
        }
    }
}
