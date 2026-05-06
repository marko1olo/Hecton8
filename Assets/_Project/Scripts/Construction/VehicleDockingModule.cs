using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.Power;
using Hecton8.World;
using Unity.Mathematics;
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
        private const int TransportLookupCacheCapacity = 16;

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
        // COLD ALLOC: ulong[16] — trigger collider id cache for transport lifecycle owner discovery — owner: VehicleDockingModule
        private readonly ulong[] _transportLookupColliderIds = new ulong[TransportLookupCacheCapacity];
        // COLD ALLOC: IPlayerTransportLifecycleOwner[16] — resolved transport owner cache for trigger contacts — owner: VehicleDockingModule
        private readonly IPlayerTransportLifecycleOwner[] _transportLookupOwners = new IPlayerTransportLifecycleOwner[TransportLookupCacheCapacity];
        // COLD ALLOC: MonoBehaviour[16] — resolved transport owner component cache for trigger contacts — owner: VehicleDockingModule
        private readonly MonoBehaviour[] _transportLookupBehaviours = new MonoBehaviour[TransportLookupCacheCapacity];

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
        private AbsoluteUniversePosition _dockingStartAup;
        private AbsoluteUniversePosition _dockingTargetAup;
        private float _dockingElapsedSeconds;
        private MountablePlayerTransport _mountedTransportLockOwner;
        private ulong _lastRejectedDockColliderId;
        private int _transportLookupCount;
        private int _transportLookupWriteCursor;

        /// <summary>Continuous draw while charge is actually transferred to a docked transport.</summary>
        public float PowerRating => _activelyCharging ? -chargingPowerDraw : 0f;

        /// <summary>Dock load shedding priority.</summary>
        public int PowerPriority => powerPriority;

        /// <summary>Cached base-grid power state for this dock.</summary>
        public bool HasPower => _hasPower;
        internal bool DebugDockOccupied => _debugDockOccupied;

        private void Awake()
        {
            SanitizeDockingSettings();
            _cachedTransform = transform;
            _triggerCollider = GetComponent<Collider>();
            _triggerCollider.isTrigger = true;
            _powerNode = GetComponent<PowerNode>();
        }

        private void OnValidate()
        {
            SanitizeDockingSettings();
        }

        private void OnEnable()
        {
            ClearTransportLookupCache();
            TryRegister();
        }

        private void OnDisable()
        {
            ReleaseDockedTransport();
            ClearTransportLookupCache();
            TryUnregister();
        }

        private void OnDestroy()
        {
            ReleaseDockedTransport();
            ClearTransportLookupCache();
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
            _lastRejectedDockColliderId = 0UL;
            ClearTransportLookupCache();
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
            _lastRejectedDockColliderId = 0UL;
            ClearTransportLookupCache();
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

            _lastRejectedDockColliderId = 0UL;
            TryDockFromCollider(other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (other == null || _dockedTransport != null)
                return;

            ulong colliderId = ResolveColliderRuntimeId(other);
            if (colliderId != 0UL && colliderId == _lastRejectedDockColliderId)
                return;

            if (!TryDockFromCollider(other) && colliderId != 0UL)
                _lastRejectedDockColliderId = colliderId;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null || _dockedBehaviour == null)
                return;

            ulong colliderId = ResolveColliderRuntimeId(other);
            if (colliderId != 0UL && colliderId == _lastRejectedDockColliderId)
                _lastRejectedDockColliderId = 0UL;

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
            _registered = GlobalRegistry.Updatables.Contains(this) ||
                          GlobalRegistry.FixedTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            if (GlobalRegistry.Updatables.Contains(this))
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);

            if (GlobalRegistry.FixedTickables.Contains(this))
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
            CacheDockingTrajectory();
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
            _lastRejectedDockColliderId = 0UL;
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

        private void CacheDockingTrajectory()
        {
            Transform anchor = dockAnchor != null ? dockAnchor : _cachedTransform;
            Vector3 startPosition = _dockingStartPosition;
            Quaternion startRotation = _dockingStartRotation;
            if (_dockedBody != null)
            {
                startPosition = _dockedBody.position;
                startRotation = _dockedBody.rotation;
            }
            else if (_dockedTransform != null)
            {
                startPosition = _dockedTransform.position;
                startRotation = _dockedTransform.rotation;
            }

            _dockingStartPosition = startPosition;
            _dockingStartRotation = startRotation;
            _dockingStartAup = AbsoluteUniversePosition.FromRuntimePosition(startPosition);
            _dockingTargetAup = AbsoluteUniversePosition.FromRuntimePosition(anchor != null ? anchor.position : startPosition);
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
            _dockingElapsedSeconds = Mathf.Min(duration, _dockingElapsedSeconds + Mathf.Max(0.0001f, fixedDeltaTime));
            Transform anchor = dockAnchor != null ? dockAnchor : _cachedTransform;
            Vector3 anchorPosition = anchor.position;
            Quaternion anchorRotation = anchor.rotation;
            _dockingTargetAup = AbsoluteUniversePosition.FromRuntimePosition(anchorPosition);
            float normalizedTime = Mathf.Clamp01(_dockingElapsedSeconds / duration);
            float easedTime = normalizedTime * normalizedTime * (3f - (2f * normalizedTime));
            Vector3 evaluatedPosition = ResolveRuntimeAupLerp(_dockingStartAup, _dockingTargetAup, easedTime, anchorPosition);
            quaternion startRotationQ = new quaternion(
                _dockingStartRotation.x,
                _dockingStartRotation.y,
                _dockingStartRotation.z,
                _dockingStartRotation.w);
            quaternion anchorRotationQ = new quaternion(
                anchorRotation.x,
                anchorRotation.y,
                anchorRotation.z,
                anchorRotation.w);
            quaternion evaluatedRotationQ = math.slerp(startRotationQ, anchorRotationQ, easedTime);
            Quaternion evaluatedRotation = new Quaternion(
                evaluatedRotationQ.value.x,
                evaluatedRotationQ.value.y,
                evaluatedRotationQ.value.z,
                evaluatedRotationQ.value.w);

            if (_dockedBody != null)
            {
                _dockedBody.linearVelocity = Vector3.zero;
                _dockedBody.angularVelocity = Vector3.zero;
                _dockedBody.MovePosition(evaluatedPosition);
                _dockedBody.MoveRotation(evaluatedRotation);
            }
            else if (_dockedTransform != null)
            {
                _dockedTransform.SetPositionAndRotation(evaluatedPosition, evaluatedRotation);
            }

            bool durationElapsed = _dockingElapsedSeconds >= duration - 0.0001f;
            if (!durationElapsed)
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

        private void SanitizeDockingSettings()
        {
            dockingDurationSeconds = Mathf.Clamp(dockingDurationSeconds, 0.25f, 8f);
            dockingPositionSpring = Mathf.Max(0f, dockingPositionSpring);
            dockingPositionDamping = Mathf.Max(0f, dockingPositionDamping);
            maxDockingForce = Mathf.Max(1f, maxDockingForce);
            dockingRotationSpring = Mathf.Max(0f, dockingRotationSpring);
            dockingRotationDamping = Mathf.Max(0f, dockingRotationDamping);
            dockingCaptureDistanceEpsilon = Mathf.Max(0.001f, dockingCaptureDistanceEpsilon);
            dockingCaptureAngleEpsilonDegrees = Mathf.Max(0.01f, dockingCaptureAngleEpsilonDegrees);
        }

        private static Vector3 ResolveRuntimeAupLerp(
            AbsoluteUniversePosition from,
            AbsoluteUniversePosition to,
            float normalizedTime,
            Vector3 fallbackPosition)
        {
            double3 start = from.ToAbsoluteDouble3();
            double3 target = to.ToAbsoluteDouble3();
            double3 resolved = start + ((target - start) * (double)Mathf.Clamp01(normalizedTime));
            float3 runtime = AbsoluteUniversePosition.FromAbsolutePosition(resolved).ToRuntimeFloat3();
            Vector3 runtimePosition = new Vector3(runtime.x, runtime.y, runtime.z);
            return IsFiniteVector(runtimePosition) ? runtimePosition : fallbackPosition;
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

        private bool TryResolveTransportLifecycleOwner(
            Collider other,
            out IPlayerTransportLifecycleOwner lifecycleOwner,
            out MonoBehaviour lifecycleBehaviour)
        {
            lifecycleOwner = null;
            lifecycleBehaviour = null;
            if (other == null)
                return false;

            ulong colliderId = ResolveColliderRuntimeId(other);
            if (colliderId != 0UL)
            {
                for (int i = 0; i < _transportLookupCount; i++)
                {
                    if (_transportLookupColliderIds[i] != colliderId)
                        continue;

                    lifecycleOwner = _transportLookupOwners[i];
                    lifecycleBehaviour = _transportLookupBehaviours[i];
                    if (lifecycleOwner != null && lifecycleBehaviour != null)
                        return lifecycleBehaviour.gameObject.activeInHierarchy;

                    _transportLookupColliderIds[i] = 0UL;
                    break;
                }
            }

            lifecycleOwner = other.GetComponentInParent<IPlayerTransportLifecycleOwner>();
            lifecycleBehaviour = lifecycleOwner as MonoBehaviour;
            if (lifecycleOwner != null && lifecycleBehaviour != null)
            {
                CacheTransportLifecycleOwner(colliderId, lifecycleOwner, lifecycleBehaviour);
                return true;
            }

            PlayerTransportCoordinator transportCoordinator = other.GetComponentInParent<PlayerTransportCoordinator>();
            if (transportCoordinator != null && transportCoordinator.TryResolveTransportLifecycleOwner(out lifecycleOwner))
            {
                lifecycleBehaviour = lifecycleOwner as MonoBehaviour;
                if (lifecycleBehaviour != null)
                {
                    CacheTransportLifecycleOwner(colliderId, lifecycleOwner, lifecycleBehaviour);
                    return true;
                }
            }

            lifecycleOwner = null;
            lifecycleBehaviour = null;
            return false;
        }

        private void CacheTransportLifecycleOwner(
            ulong colliderId,
            IPlayerTransportLifecycleOwner lifecycleOwner,
            MonoBehaviour lifecycleBehaviour)
        {
            if (colliderId == 0UL || lifecycleOwner == null || lifecycleBehaviour == null)
                return;

            int slot;
            if (_transportLookupCount < _transportLookupColliderIds.Length)
            {
                slot = _transportLookupCount;
                _transportLookupCount++;
            }
            else
            {
                slot = _transportLookupWriteCursor;
            }

            _transportLookupColliderIds[slot] = colliderId;
            _transportLookupOwners[slot] = lifecycleOwner;
            _transportLookupBehaviours[slot] = lifecycleBehaviour;
            _transportLookupWriteCursor = (_transportLookupWriteCursor + 1) % _transportLookupColliderIds.Length;
        }

        private void ClearTransportLookupCache()
        {
            for (int i = 0; i < _transportLookupCount; i++)
            {
                _transportLookupColliderIds[i] = 0UL;
                _transportLookupOwners[i] = null;
                _transportLookupBehaviours[i] = null;
            }

            _transportLookupCount = 0;
            _transportLookupWriteCursor = 0;
        }

        private static ulong ResolveColliderRuntimeId(Collider collider)
        {
            return collider != null
                ? EntityId.ToULong(collider.GetEntityId())
                : 0UL;
        }
    }
}
