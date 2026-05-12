using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.Power;
using Hecton8.World;
using Unity.Collections;
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
    public sealed class VehicleDockingModule : MonoBehaviour, ITickable, IFixedTickable, IUpdatable, IPowerComponent, IPoolable, IOriginShiftListener
    {
        private const int TransportLookupCacheCapacity = 16;
        private const float MaxDockingFixedDeltaSeconds = 0.05f;
        private const float DockingAcquireDistanceSqMeters = 2f;
        private const float DockingAcquireAlignmentDot = 0.8f;
        private const float DefaultDockingDurationSeconds = 1.5f;
        private const float DefaultUndockEjectSpeedMetersPerSecond = 4.5f;
        private const float DefaultDockingImpactSpeedMetersPerSecond = 6.5f;
        private const int DockTelemetryCapacity = 300;

        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        private struct DockTelemetryEntry
        {
            public int Frame;
            public byte State;
            public byte HasPower;
            public byte HasRelativeAup;
            public byte Reserved;
            public float DistanceSq;
            public float AlignmentDot;
            public float3 Position;
            public float4 Rotation;
            public long GridX;
            public long GridY;
            public long GridZ;
        }

        [Header("Docking")]
        [Tooltip("Optional snap anchor applied when a rigidbody transport is docked. Falls back to this transform.")]
        [SerializeField] private Transform dockAnchor;

        [Tooltip("Deterministic docking travel duration in seconds from trigger capture to hard-lock.")]
        [SerializeField, Range(0.05f, 8f)] private float dockingDurationSeconds = DefaultDockingDurationSeconds;

        [Tooltip("Velocity injected along the dock forward axis when a transport undocks.")]
        [SerializeField, Min(0f)] private float undockEjectSpeedMetersPerSecond = DefaultUndockEjectSpeedMetersPerSecond;

        [Tooltip("Synthetic impact speed sent to the shared physics/audio impact bus when the dock hard-locks.")]
        [SerializeField, Min(0f)] private float dockingImpactSpeedMetersPerSecond = DefaultDockingImpactSpeedMetersPerSecond;

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

        [Header("Power")]
        [Tooltip("Power draw while the dock is actively charging a transport.")]
        [SerializeField, Range(0f, 400f)] private float chargingPowerDraw = 120f;

        [Tooltip("Grid shedding priority used by this dock.")]
        [SerializeField, Range(0, 100)] private int powerPriority = 35;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugHasPower = true;
        [SerializeField] private bool _debugDockOccupied;
        [SerializeField] private string _debugDockedTransportName;

        // COLD ALLOC: List<StorageCrate>[4] - temporary cargo storage bridge for the currently docked transport - owner: VehicleDockingModule
        private readonly List<StorageCrate> _connectedCargoCrates = new List<StorageCrate>(4);
        // COLD ALLOC: List<StorageCrate>[4] - component query buffer for docked transport cargo discovery - owner: VehicleDockingModule
        private readonly List<StorageCrate> _cargoDiscoveryBuffer = new List<StorageCrate>(4);
        // COLD ALLOC: ulong[16] - trigger collider id cache for transport lifecycle owner discovery - owner: VehicleDockingModule
        private readonly ulong[] _transportLookupColliderIds = new ulong[TransportLookupCacheCapacity];
        // COLD ALLOC: IPlayerTransportLifecycleOwner[16] - resolved transport owner cache for trigger contacts - owner: VehicleDockingModule
        private readonly IPlayerTransportLifecycleOwner[] _transportLookupOwners = new IPlayerTransportLifecycleOwner[TransportLookupCacheCapacity];
        // COLD ALLOC: MonoBehaviour[16] - resolved transport owner component cache for trigger contacts - owner: VehicleDockingModule
        private readonly MonoBehaviour[] _transportLookupBehaviours = new MonoBehaviour[TransportLookupCacheCapacity];

        private Transform _cachedTransform;
        private Collider _triggerCollider;
        private PowerNode _powerNode;
        private BaseModule _owningModule;
        private bool _registered;
        private bool _hasPower = true;
        private bool _activelyCharging;
        private bool _dockingInProgress;
        private bool _isDocked;
        private IPlayerTransportLifecycleOwner _dockedTransport;
        private MonoBehaviour _dockedBehaviour;
        private Transform _dockedTransform;
        private Rigidbody _dockedBody;
        private VehicleMotor _dockedVehicleMotor;
        private SubmarineFluidDynamics _dockedFluidDynamics;
        private bool _cachedBodyWasKinematic;
        private bool _cachedBodyUseGravity;
        private RigidbodyConstraints _cachedBodyConstraints;
        private Vector3 _dockingStartPosition;
        private Quaternion _dockingStartRotation = Quaternion.identity;
        private AbsoluteUniversePosition _dockingStartAup;
        private AbsoluteUniversePosition _dockingTargetAup;
        private AbsoluteUniversePosition _habitatReferenceAup;
        private AbsoluteUniversePosition _dockedRelativeAup;
        private float _dockingElapsedSeconds;
        private float _attachedDroneMassKg;
        private MountablePlayerTransport _mountedTransportLockOwner;
        private ulong _lastRejectedDockColliderId;
        private int _transportLookupCount;
        private int _transportLookupWriteCursor;
        private bool _hasDockedRelativeAup;
        private NativeArray<DockTelemetryEntry> _dockTelemetry;
        private int _dockTelemetryCursor;

        /// <summary>Continuous draw while charge is actually transferred to a docked transport.</summary>
        public float PowerRating => _activelyCharging ? -chargingPowerDraw : 0f;

        /// <summary>Dock load shedding priority.</summary>
        public int PowerPriority => powerPriority;

        /// <summary>Cached base-grid power state for this dock.</summary>
        public bool HasPower => _hasPower;
        internal bool DebugDockOccupied => _debugDockOccupied;
        public bool IsDockingInProgress => _dockingInProgress;
        public bool IsDocked => _isDocked;
        public bool ShouldCullDrivingHud => _dockingInProgress || _isDocked;
        public bool ShouldBlockSubmarineHatchOpening => _isDocked && _owningModule != null && _owningModule.IsFlooded;
        public bool HasDockedRelativeAup => _hasDockedRelativeAup;
        public AbsoluteUniversePosition DockedRelativeAup => _dockedRelativeAup;
        public float TotalDockedMassKg => ResolveDockedBodyMassKg() + _attachedDroneMassKg;

        public void SetAttachedDroneMassKg(float massKg)
        {
            _attachedDroneMassKg = math.isfinite(massKg) ? math.max(0f, massKg) : 0f;
            PushDockedExternalMass();
        }

        public bool TryUndock(bool applyEjectVelocity = true)
        {
            if (_dockedTransport == null && _dockedBehaviour == null && _dockedBody == null)
                return false;

            RecordDockTelemetry();
            ReleaseDockedTransport(applyEjectVelocity);
            return true;
        }

        private void Awake()
        {
            SanitizeDockingSettings();
            _cachedTransform = transform;
            _triggerCollider = GetComponent<Collider>();
            _triggerCollider.isTrigger = true;
            _powerNode = GetComponent<PowerNode>();
            _owningModule = GetComponentInParent<BaseModule>();
            EnsureDockTelemetry();
        }

        private void OnValidate()
        {
            SanitizeDockingSettings();
        }

        private void OnEnable()
        {
            EnsureDockTelemetry();
            ClearTransportLookupCache();
            HectonFloatingOrigin.RegisterListener(this);
            TryRegister();
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            ReleaseDockedTransport();
            ClearTransportLookupCache();
            TryUnregister();
            DisposeDockTelemetry();
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            ReleaseDockedTransport();
            ClearTransportLookupCache();
            TryUnregister();
            DisposeDockTelemetry();
        }

        public void OnSpawn()
        {
            _hasPower = true;
            _debugHasPower = true;
            _activelyCharging = false;
            _dockingInProgress = false;
            _isDocked = false;
            _dockingElapsedSeconds = 0f;
            _attachedDroneMassKg = 0f;
            _hasDockedRelativeAup = false;
            _dockTelemetryCursor = 0;
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
            _attachedDroneMassKg = 0f;
            _hasDockedRelativeAup = false;
            _dockTelemetryCursor = 0;
            _lastRejectedDockColliderId = 0UL;
            ClearTransportLookupCache();
            _debugDockOccupied = false;
            _debugDockedTransportName = string.Empty;
            TryUnregister();
        }

        public void Tick(float deltaTime)
        {
            RecordDockTelemetry();

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
            RecordDockTelemetry();
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

            TryDockFromCollider(other);
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

            ReleaseDockedTransport(true);
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            bool updateRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            bool fixedRegistered = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            if (!updateRegistered || !fixedRegistered)
            {
                if (updateRegistered)
                    GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                if (fixedRegistered)
                    GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);

                _registered = false;
                return;
            }

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

            if (!PassesDockingAcquisitionGate(ownerBehaviour))
                return false;

            DockTransport(owner, ownerBehaviour);
            return true;
        }

        private bool PassesDockingAcquisitionGate(MonoBehaviour transportBehaviour)
        {
            if (transportBehaviour == null || !TryResolveDockAnchor(out Transform anchor))
                return false;

            if (!TryResolveCandidatePose(transportBehaviour, out Vector3 candidatePosition, out Quaternion candidateRotation))
                return false;

            AbsoluteUniversePosition candidateAup = AbsoluteUniversePosition.FromRuntimePosition(candidatePosition);
            AbsoluteUniversePosition anchorAup = AbsoluteUniversePosition.FromRuntimePosition(anchor.position);
            if (AbsoluteUniversePosition.DistanceSq(candidateAup, anchorAup) >= DockingAcquireDistanceSqMeters)
                return false;

            Vector3 candidateForward = candidateRotation * Vector3.forward;
            Vector3 anchorForward = anchor.forward;
            if (!IsFiniteVector(candidateForward) || !IsFiniteVector(anchorForward))
                return false;

            float alignmentDot = Vector3.Dot(candidateForward, anchorForward);
            return math.isfinite(alignmentDot) && alignmentDot > DockingAcquireAlignmentDot;
        }

        private static bool TryResolveCandidatePose(MonoBehaviour transportBehaviour, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (transportBehaviour == null)
                return false;

            Rigidbody candidateBody;
            if (!transportBehaviour.TryGetComponent(out candidateBody))
                candidateBody = transportBehaviour.GetComponentInParent<Rigidbody>();

            if (candidateBody != null)
            {
                position = candidateBody.position;
                rotation = candidateBody.rotation;
            }
            else
            {
                Transform candidateTransform = transportBehaviour.transform;
                position = candidateTransform.position;
                rotation = candidateTransform.rotation;
            }

            return IsFiniteVector(position) && IsFiniteQuaternion(rotation);
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
            ResolveDockedVehicleMotor(transportBehaviour);
            ResolveDockedFluidDynamics(transportBehaviour);
            BeginDockingControlLock(transportBehaviour);
            _dockingElapsedSeconds = 0f;
            CacheDockingTrajectory();
            ResetDockedVehiclePresentationState();
            _dockingInProgress = true;
            _isDocked = false;

            if (ShouldUseInstantDockSnap())
                FinalizeDockedTransport();
        }

        private void ReleaseDockedTransport(bool applyEjectVelocity = false)
        {
            DisconnectDockedCargoCrates();

            if (_dockedBody != null)
            {
                Vector3 ejectVelocity = applyEjectVelocity
                    ? ResolveDockForward() * ResolveSafeUndockEjectSpeed()
                    : Vector3.zero;

                _dockedBody.linearVelocity = Vector3.zero;
                _dockedBody.angularVelocity = Vector3.zero;
                _dockedBody.isKinematic = _cachedBodyWasKinematic;
                _dockedBody.useGravity = _cachedBodyUseGravity;
                _dockedBody.constraints = _cachedBodyConstraints;

                if (applyEjectVelocity)
                    ApplyUndockEjectVelocity(_dockedBody, ejectVelocity);
            }

            GlobalPhysicsStateManager.UnregisterDockConnection(this);
            EndDockingControlLock();
            _dockedBody = null;
            _dockedVehicleMotor = null;
            if (_dockedFluidDynamics != null)
                _dockedFluidDynamics.SetDockedExternalMassKilograms(0f);
            _dockedFluidDynamics = null;
            _attachedDroneMassKg = 0f;
            _dockedTransport = null;
            _dockedBehaviour = null;
            _dockedTransform = null;
            _activelyCharging = false;
            _dockingInProgress = false;
            _isDocked = false;
            _dockingElapsedSeconds = 0f;
            _hasDockedRelativeAup = false;
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
            _dockedBody.isKinematic = true;
            _dockedBody.useGravity = false;
        }

        private void ResolveDockedVehicleMotor(MonoBehaviour transportBehaviour)
        {
            _dockedVehicleMotor = null;
            if (transportBehaviour == null)
                return;

            if (!transportBehaviour.TryGetComponent(out _dockedVehicleMotor))
                _dockedVehicleMotor = transportBehaviour.GetComponentInParent<VehicleMotor>();
        }

        private void ResolveDockedFluidDynamics(MonoBehaviour transportBehaviour)
        {
            _dockedFluidDynamics = null;
            if (transportBehaviour == null)
                return;

            if (!transportBehaviour.TryGetComponent(out _dockedFluidDynamics))
                _dockedFluidDynamics = transportBehaviour.GetComponentInParent<SubmarineFluidDynamics>();

            PushDockedExternalMass();
        }

        private void ResetDockedVehiclePresentationState()
        {
            if (_dockedVehicleMotor == null)
                return;

            _dockedVehicleMotor.ResetHydrodynamicPresentationState();
        }

        private void CacheDockingTrajectory()
        {
            Transform anchor = ResolveDockAnchor();
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
            RefreshDockedRelativeAup(anchor != null ? anchor.position : startPosition);
        }

        private bool SnapDockedBodyToAnchor()
        {
            if (_dockedBehaviour == null || _dockedTransform == null)
                return false;

            Transform anchor = ResolveDockAnchor();
            if (anchor == null || !IsFiniteVector(anchor.position) || !IsFiniteQuaternion(anchor.rotation))
                return false;

            if (_dockedBody != null)
            {
                _dockedBody.MovePosition(anchor.position);
                _dockedBody.MoveRotation(anchor.rotation);
                return true;
            }

            _dockedTransform.SetPositionAndRotation(anchor.position, anchor.rotation);
            return true;
        }

        private void AdvanceDockingPose(float fixedDeltaTime)
        {
            if (ShouldUseInstantDockSnap())
            {
                FinalizeDockedTransport();
                return;
            }

            float duration = ResolveSafeDockingDurationSeconds();
            float safeFixedDeltaTime = SanitizeFixedDeltaSeconds(fixedDeltaTime);
            _dockingElapsedSeconds = math.min(duration, _dockingElapsedSeconds + safeFixedDeltaTime);
            Transform anchor = ResolveDockAnchor();
            if (anchor == null || !IsFiniteVector(anchor.position) || !IsFiniteQuaternion(anchor.rotation))
            {
                AbortDockingForInvalidPose();
                return;
            }

            Vector3 anchorPosition = anchor.position;
            Quaternion anchorRotation = anchor.rotation;
            _dockingTargetAup = AbsoluteUniversePosition.FromRuntimePosition(anchorPosition);
            RefreshDockedRelativeAup(anchorPosition);
            float normalizedTime = math.saturate(_dockingElapsedSeconds * math.rcp(duration));
            float easedTime = normalizedTime * normalizedTime * (3f - (2f * normalizedTime));
            Vector3 evaluatedPosition = ResolveRuntimeAupLerp(_dockingStartAup, _dockingTargetAup, easedTime, anchorPosition);
            Quaternion evaluatedRotation = FastNlerp(_dockingStartRotation, anchorRotation, easedTime);

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
            bool wasDocked = _isDocked;
            if (!SnapDockedBodyToAnchor())
            {
                AbortDockingForInvalidPose();
                return;
            }

            if (_dockedBody != null)
            {
                _dockedBody.linearVelocity = Vector3.zero;
                _dockedBody.angularVelocity = Vector3.zero;
                _dockedBody.isKinematic = true;
                _dockedBody.useGravity = false;
                GlobalPhysicsStateManager.RegisterDockConnection(this, _dockedBody);
            }

            Transform anchor = ResolveDockAnchor();
            if (anchor != null && IsFiniteVector(anchor.position))
                RefreshDockedRelativeAup(anchor.position);

            ConnectDockedCargoCrates();
            _dockingInProgress = false;
            _isDocked = true;

            if (!wasDocked)
                QueueDockingImpactSignal();

            PushDockedExternalMass();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (_dockedTransport == null || _dockedBehaviour == null)
                return;

            if (_dockingInProgress)
            {
                FinalizeDockedTransport();
                return;
            }

            if (!_isDocked)
                return;

            if (!SnapDockedBodyToAnchor())
            {
                AbortDockingForInvalidPose();
                return;
            }

            Transform anchor = ResolveDockAnchor();
            if (anchor != null && IsFiniteVector(anchor.position))
                RefreshDockedRelativeAup(anchor.position);
        }

        void IOriginShiftListener.OnOriginShift(in Hecton8.Core.OriginShiftEventData shiftData)
        {
            OnOriginShift(in shiftData);
        }

        private Transform ResolveDockAnchor()
        {
            return dockAnchor != null ? dockAnchor : _cachedTransform;
        }

        private bool TryResolveDockAnchor(out Transform anchor)
        {
            anchor = ResolveDockAnchor();
            return anchor != null && IsFiniteVector(anchor.position) && IsFiniteQuaternion(anchor.rotation);
        }

        private Vector3 ResolveDockForward()
        {
            Transform anchor = ResolveDockAnchor();
            Vector3 forward = anchor != null ? anchor.forward : Vector3.forward;
            return IsFiniteVector(forward) ? forward : Vector3.forward;
        }

        private void RefreshDockedRelativeAup(Vector3 dockRuntimePosition)
        {
            AbsoluteUniversePosition dockWorldAup = AbsoluteUniversePosition.FromRuntimePosition(dockRuntimePosition);
            _habitatReferenceAup = ResolveHabitatReferenceAup(dockRuntimePosition);
            _dockedRelativeAup = ResolveRelativeToHabitatAup(dockWorldAup, _habitatReferenceAup);
            _hasDockedRelativeAup = true;
        }

        private AbsoluteUniversePosition ResolveHabitatReferenceAup(Vector3 fallbackPosition)
        {
            if (_owningModule != null)
            {
                Transform ownerTransform = _owningModule.transform;
                if (ownerTransform != null && IsFiniteVector(ownerTransform.position))
                    return AbsoluteUniversePosition.FromRuntimePosition(ownerTransform.position);
            }

            return AbsoluteUniversePosition.FromRuntimePosition(fallbackPosition);
        }

        private static AbsoluteUniversePosition ResolveRelativeToHabitatAup(
            AbsoluteUniversePosition worldAup,
            AbsoluteUniversePosition habitatAup)
        {
            double3 relativeMeters = worldAup.ToAbsoluteDouble3() - habitatAup.ToAbsoluteDouble3();
            return AbsoluteUniversePosition.FromAbsolutePosition(relativeMeters);
        }

        private void QueueDockingImpactSignal()
        {
            if (_dockedBody == null)
                return;

            float impactSpeed = ResolveSafeDockingImpactSpeed();
            if (impactSpeed <= 0f)
                return;

            Transform anchor = ResolveDockAnchor();
            Vector3 point = anchor != null && IsFiniteVector(anchor.position)
                ? anchor.position
                : _dockedBody.position;
            Vector3 normal = -ResolveDockForward();
            GlobalPhysicsStateManager.QueueKinematicImpact(_dockedBody, point, normal, impactSpeed);
        }

        private void ApplyUndockEjectVelocity(Rigidbody body, Vector3 ejectVelocity)
        {
            if (body == null || body.isKinematic || !IsFiniteVector(ejectVelocity))
                return;

            body.linearVelocity = ejectVelocity;
            body.angularVelocity = Vector3.zero;
        }

        private void AbortDockingForInvalidPose()
        {
            DumpDockTelemetry();
            ReleaseDockedTransport(false);
        }

        private float ResolveDockedBodyMassKg()
        {
            return _dockedBody != null && math.isfinite(_dockedBody.mass)
                ? math.max(0f, _dockedBody.mass)
                : 0f;
        }

        private void PushDockedExternalMass()
        {
            if (_dockedFluidDynamics == null)
                return;

            _dockedFluidDynamics.SetDockedExternalMassKilograms(_attachedDroneMassKg);
        }

        private void EnsureDockTelemetry()
        {
            if (_dockTelemetry.IsCreated)
                return;

            _dockTelemetry = new NativeArray<DockTelemetryEntry>(
                DockTelemetryCapacity,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
        }

        private void DisposeDockTelemetry()
        {
            if (!_dockTelemetry.IsCreated)
                return;

            _dockTelemetry.Dispose();
            _dockTelemetryCursor = 0;
        }

        private void RecordDockTelemetry()
        {
            if (!_dockTelemetry.IsCreated)
                return;

            if (!_dockingInProgress && !_isDocked)
                return;

            Vector3 position = ResolveTelemetryPosition();
            Quaternion rotation = ResolveTelemetryRotation();
            if (!IsFiniteVector(position) || !IsFiniteQuaternion(rotation))
            {
                DumpDockTelemetry();
                return;
            }

            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(position);
            Transform anchor = ResolveDockAnchor();
            float distanceSq = 0f;
            float alignmentDot = 0f;
            if (anchor != null && IsFiniteVector(anchor.position))
            {
                double resolvedDistanceSq = AbsoluteUniversePosition.DistanceSq(
                    aup,
                    AbsoluteUniversePosition.FromRuntimePosition(anchor.position));
                distanceSq = resolvedDistanceSq < float.MaxValue ? (float)resolvedDistanceSq : float.MaxValue;
                alignmentDot = IsFiniteQuaternion(anchor.rotation)
                    ? Vector3.Dot(rotation * Vector3.forward, anchor.forward)
                    : 0f;
            }

            _dockTelemetry[_dockTelemetryCursor] = new DockTelemetryEntry
            {
                Frame = Time.frameCount,
                State = _dockingInProgress ? (byte)1 : (_isDocked ? (byte)2 : (byte)0),
                HasPower = _hasPower ? (byte)1 : (byte)0,
                HasRelativeAup = _hasDockedRelativeAup ? (byte)1 : (byte)0,
                Reserved = 0,
                DistanceSq = distanceSq,
                AlignmentDot = alignmentDot,
                Position = new float3(position.x, position.y, position.z),
                Rotation = new float4(rotation.x, rotation.y, rotation.z, rotation.w),
                GridX = aup.GridX,
                GridY = aup.GridY,
                GridZ = aup.GridZ
            };
            _dockTelemetryCursor++;
            if (_dockTelemetryCursor >= _dockTelemetry.Length)
                _dockTelemetryCursor = 0;
        }

        private Vector3 ResolveTelemetryPosition()
        {
            if (_dockedBody != null)
                return _dockedBody.position;
            if (_dockedTransform != null)
                return _dockedTransform.position;
            return _cachedTransform != null ? _cachedTransform.position : Vector3.zero;
        }

        private Quaternion ResolveTelemetryRotation()
        {
            if (_dockedBody != null)
                return _dockedBody.rotation;
            if (_dockedTransform != null)
                return _dockedTransform.rotation;
            return _cachedTransform != null ? _cachedTransform.rotation : Quaternion.identity;
        }

        private void DumpDockTelemetry()
        {
            if (!_dockTelemetry.IsCreated)
                return;

            string projectRoot = Application.dataPath;
            if (!string.IsNullOrEmpty(projectRoot))
                projectRoot = Directory.GetParent(projectRoot)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                projectRoot = Directory.GetCurrentDirectory();

            string directory = Path.Combine(projectRoot, "Docs", "AgentLogs");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "Dump_VEHICLE_MECH_DOCKING.bin");
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(DockTelemetryCapacity);
                writer.Write(_dockTelemetryCursor);
                for (int i = 0; i < _dockTelemetry.Length; i++)
                {
                    int index = (_dockTelemetryCursor + i) % _dockTelemetry.Length;
                    DockTelemetryEntry entry = _dockTelemetry[index];
                    writer.Write(entry.Frame);
                    writer.Write(entry.State);
                    writer.Write(entry.HasPower);
                    writer.Write(entry.HasRelativeAup);
                    writer.Write(entry.Reserved);
                    writer.Write(entry.DistanceSq);
                    writer.Write(entry.AlignmentDot);
                    writer.Write(entry.Position.x);
                    writer.Write(entry.Position.y);
                    writer.Write(entry.Position.z);
                    writer.Write(entry.Rotation.x);
                    writer.Write(entry.Rotation.y);
                    writer.Write(entry.Rotation.z);
                    writer.Write(entry.Rotation.w);
                    writer.Write(entry.GridX);
                    writer.Write(entry.GridY);
                    writer.Write(entry.GridZ);
                }
            }
        }

        private float ResolveSafeUndockEjectSpeed()
        {
            return math.isfinite(undockEjectSpeedMetersPerSecond)
                ? math.max(0f, undockEjectSpeedMetersPerSecond)
                : DefaultUndockEjectSpeedMetersPerSecond;
        }

        private float ResolveSafeDockingImpactSpeed()
        {
            return math.isfinite(dockingImpactSpeedMetersPerSecond)
                ? math.max(0f, dockingImpactSpeedMetersPerSecond)
                : DefaultDockingImpactSpeedMetersPerSecond;
        }

        private static bool ShouldUseInstantDockSnap()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            return tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350;
        }

        private void SanitizeDockingSettings()
        {
            dockingDurationSeconds = DefaultDockingDurationSeconds;
            undockEjectSpeedMetersPerSecond = math.isfinite(undockEjectSpeedMetersPerSecond)
                ? math.max(0f, undockEjectSpeedMetersPerSecond)
                : DefaultUndockEjectSpeedMetersPerSecond;
            dockingImpactSpeedMetersPerSecond = math.isfinite(dockingImpactSpeedMetersPerSecond)
                ? math.max(0f, dockingImpactSpeedMetersPerSecond)
                : DefaultDockingImpactSpeedMetersPerSecond;
            dockingPositionSpring = math.isfinite(dockingPositionSpring)
                ? math.max(0f, dockingPositionSpring)
                : 20f;
            dockingPositionDamping = math.isfinite(dockingPositionDamping)
                ? math.max(0f, dockingPositionDamping)
                : 8f;
            maxDockingForce = math.isfinite(maxDockingForce)
                ? math.max(1f, maxDockingForce)
                : 65000f;
            dockingRotationSpring = math.isfinite(dockingRotationSpring)
                ? math.max(0f, dockingRotationSpring)
                : 18f;
            dockingRotationDamping = math.isfinite(dockingRotationDamping)
                ? math.max(0f, dockingRotationDamping)
                : 7f;
            dockingCaptureDistanceEpsilon = math.isfinite(dockingCaptureDistanceEpsilon)
                ? math.max(0.001f, dockingCaptureDistanceEpsilon)
                : 0.025f;
            dockingCaptureAngleEpsilonDegrees = math.isfinite(dockingCaptureAngleEpsilonDegrees)
                ? math.max(0.01f, dockingCaptureAngleEpsilonDegrees)
                : 1f;
        }

        private static Vector3 ResolveRuntimeAupLerp(
            AbsoluteUniversePosition from,
            AbsoluteUniversePosition to,
            float normalizedTime,
            Vector3 fallbackPosition)
        {
            double3 start = from.ToAbsoluteDouble3();
            double3 target = to.ToAbsoluteDouble3();
            double3 resolved = start + ((target - start) * (double)math.saturate(normalizedTime));
            float3 runtime = AbsoluteUniversePosition.FromAbsolutePosition(resolved).ToRuntimeFloat3();
            Vector3 runtimePosition = new Vector3(runtime.x, runtime.y, runtime.z);
            return IsFiniteVector(runtimePosition) ? runtimePosition : fallbackPosition;
        }

        private float ResolveSafeDockingDurationSeconds()
        {
            return math.isfinite(dockingDurationSeconds) && dockingDurationSeconds > 0f
                ? dockingDurationSeconds
                : DefaultDockingDurationSeconds;
        }

        private static Quaternion FastNlerp(Quaternion from, Quaternion to, float normalizedTime)
        {
            if (!IsFiniteQuaternion(from))
                return IsFiniteQuaternion(to) ? to : Quaternion.identity;
            if (!IsFiniteQuaternion(to))
                return from;

            float t = math.saturate(normalizedTime);
            float sign = Quaternion.Dot(from, to) < 0f ? -1f : 1f;
            float4 blended = new float4(
                from.x + (((to.x * sign) - from.x) * t),
                from.y + (((to.y * sign) - from.y) * t),
                from.z + (((to.z * sign) - from.z) * t),
                from.w + (((to.w * sign) - from.w) * t));
            float lengthSq = math.lengthsq(blended);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return to;

            float invLength = math.rsqrt(lengthSq);
            return new Quaternion(
                blended.x * invLength,
                blended.y * invLength,
                blended.z * invLength,
                blended.w * invLength);
        }

        private static float SanitizeFixedDeltaSeconds(float fixedDeltaTime)
        {
            return math.isfinite(fixedDeltaTime)
                ? math.clamp(fixedDeltaTime, 0.0001f, MaxDockingFixedDeltaSeconds)
                : 0.02f;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                     float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            float4 q = new float4(value.x, value.y, value.z, value.w);
            return math.all(math.isfinite(q)) && math.lengthsq(q) > 0.000001f;
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
