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
        private Rigidbody _dockedBody;
        private bool _cachedBodyWasKinematic;
        private bool _cachedBodyUseGravity;
        private RigidbodyConstraints _cachedBodyConstraints;
        private Vector3 _dockingStartPosition;
        private Quaternion _dockingStartRotation = Quaternion.identity;
        private float _dockingElapsedSeconds;
        private MountablePlayerTransport _mountedTransportLockOwner;

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

            TryDockFromCollider(other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (other == null || _dockedTransport != null)
                return;

            TryDockFromCollider(other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null || _dockedBehaviour == null)
                return;

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

        private void TryDockFromCollider(Collider other)
        {
            if (_dockedTransport != null)
                return;

            MonoBehaviour ownerBehaviour;
            IPlayerTransportLifecycleOwner owner;
            if (!TryResolveTransportLifecycleOwner(other, out owner, out ownerBehaviour))
                return;

            DockTransport(owner, ownerBehaviour);
        }

        private void DockTransport(IPlayerTransportLifecycleOwner transportOwner, MonoBehaviour transportBehaviour)
        {
            if (transportOwner == null || transportBehaviour == null)
                return;

            _dockedTransport = transportOwner;
            _dockedBehaviour = transportBehaviour;
            _debugDockOccupied = true;
            _debugDockedTransportName = transportBehaviour.name;

            ResolveDockedBody(transportBehaviour);
            BeginDockingControlLock(transportBehaviour);
            _dockingElapsedSeconds = 0f;
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
            _activelyCharging = false;
            _dockingInProgress = false;
            _isDocked = false;
            _dockingElapsedSeconds = 0f;
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
            _dockedBody.linearVelocity = Vector3.zero;
            _dockedBody.angularVelocity = Vector3.zero;
            _dockedBody.useGravity = false;
        }

        private void SnapDockedBodyToAnchor()
        {
            if (_dockedBehaviour == null)
                return;

            Transform anchor = dockAnchor != null ? dockAnchor : _cachedTransform;
            if (_dockedBody != null)
            {
                _dockedBody.MovePosition(anchor.position);
                _dockedBody.MoveRotation(anchor.rotation);
                return;
            }

            Transform transportTransform = _dockedBehaviour.transform;
            transportTransform.SetPositionAndRotation(anchor.position, anchor.rotation);
        }

        private void AdvanceDockingPose(float fixedDeltaTime)
        {
            float duration = Mathf.Max(0.25f, dockingDurationSeconds);
            _dockingElapsedSeconds = Mathf.Min(duration, _dockingElapsedSeconds + Mathf.Max(0f, fixedDeltaTime));
            Transform anchor = dockAnchor != null ? dockAnchor : _cachedTransform;
            float normalizedTime = Mathf.Clamp01(_dockingElapsedSeconds / duration);
            float smoothTime = normalizedTime * normalizedTime * (3f - 2f * normalizedTime);
            Vector3 targetPosition = Vector3.LerpUnclamped(_dockingStartPosition, anchor.position, smoothTime);
            Quaternion targetRotation = Quaternion.Slerp(_dockingStartRotation, anchor.rotation, smoothTime);

            if (_dockedBody != null)
            {
                _dockedBody.linearVelocity = Vector3.zero;
                _dockedBody.angularVelocity = Vector3.zero;
                _dockedBody.MovePosition(targetPosition);
                _dockedBody.MoveRotation(targetRotation);
            }
            else if (_dockedBehaviour != null)
            {
                _dockedBehaviour.transform.SetPositionAndRotation(targetPosition, targetRotation);
            }

            if (normalizedTime < 0.9999f)
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
    }
}
