using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.Power;
using Hecton8.Vehicles.Automation;
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
        private const float LowTierSplineSampleIntervalSeconds = 0.1f;
        private const float DockingDeviationAbortMeters = 5f;
        private const float DockingWakeSignalIntervalSeconds = 0.1f;
        private const float DockingCompleteSignalProgress01 = 0.95f;
        private const uint DockingWakeSourceHash = 0x44534C4Eu;
        private const uint DockingWakeSourceVehicleFlag = 2u;
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
            public float SplineDeviationError;
            public float FlowSpeed;
            public float3 Position;
            public float3 SplineTargetPosition;
            public float3 CommandVelocity;
            public float3 FlowVelocity;
            public float4 Rotation;
            public long GridX;
            public long GridY;
            public long GridZ;
            public uint OwnerHash;
            public uint RequestId;
            public uint RuntimeFlags;
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
        private HectonFluidEngine _fluidRuntime;
        private bool _cachedBodyWasKinematic;
        private bool _cachedBodyUseGravity;
        private RigidbodyConstraints _cachedBodyConstraints;
        private RigidbodyInterpolation _cachedBodyInterpolation;
        private Vector3 _dockingStartPosition;
        private Quaternion _dockingStartRotation = Quaternion.identity;
        private AbsoluteUniversePosition _dockingStartAup;
        private AbsoluteUniversePosition _dockingTargetAup;
        private AbsoluteUniversePosition _habitatReferenceAup;
        private AbsoluteUniversePosition _dockedRelativeAup;
        private ActiveSplineData _activeDockingSpline;
        private IDockingAutopilotService _dockingAutopilotService;
        private int _activeDockingSplineSlot = -1;
        private float _dockingElapsedSeconds;
        private float _attachedDroneMassKg;
        private uint _dockingSplineOwnerHash;
        private uint _dockingSplineRequestId;
        private MountablePlayerTransport _mountedTransportLockOwner;
        private Vector3 _lowTierSplineFromPosition;
        private Vector3 _lowTierSplineTargetPosition;
        private Quaternion _lowTierSplineTargetRotation = Quaternion.identity;
        private Vector3 _lastDockingSplineTargetPosition;
        private Vector3 _lastDockingCommandVelocity;
        private float3 _lastDockingFlowVelocity;
        private float _lowTierSplineBlendSeconds;
        private float _dockingWakeElapsedSeconds;
        private float _lastSplineDeviationError;
        private bool _hasLowTierSplineSample;
        private bool _dockingCompletionSignalPublished;
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
            _dockingSplineOwnerHash = ResolveDockingSplineOwnerHash();
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
            CacheDockingAutopilotService();
            CacheFluidRuntime();
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
            ResetDockingRuntimeCaches();
            _lastRejectedDockColliderId = 0UL;
            ClearTransportLookupCache();
            CacheDockingAutopilotService();
            CacheFluidRuntime();
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
            ResetDockingRuntimeCaches();
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
            ResetDockingRuntimeCaches();
            CacheDockingTrajectory();
            ResetDockedVehiclePresentationState();
            _dockingInProgress = true;
            _isDocked = false;

            if (ShouldUseInstantDockSnap())
                FinalizeDockedTransport();
        }

        private void ReleaseDockedTransport(bool applyEjectVelocity = false)
        {
            ReleaseActiveDockingSpline(DockingSplineRuntimeState.Aborted);
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
                _dockedBody.interpolation = _cachedBodyInterpolation;

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
            ResetDockingRuntimeCaches();
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
            _cachedBodyInterpolation = _dockedBody.interpolation;
            _dockingStartPosition = _dockedBody.position;
            _dockingStartRotation = _dockedBody.rotation;
            _dockedBody.linearVelocity = Vector3.zero;
            _dockedBody.angularVelocity = Vector3.zero;
            _dockedBody.isKinematic = true;
            _dockedBody.useGravity = false;
            _dockedBody.interpolation = RigidbodyInterpolation.Interpolate;
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
            float duration = ResolveSafeDockingDurationSeconds();
            float3 startForward = ToFloat3(startRotation * Vector3.forward);
            float3 targetForward = anchor != null ? ToFloat3(anchor.forward) : startForward;
            float3 targetUp = anchor != null ? ToFloat3(anchor.up) : new float3(0f, 1f, 0f);
            unchecked
            {
                _dockingSplineRequestId++;
                if (_dockingSplineRequestId == 0u)
                    _dockingSplineRequestId = 1u;
            }

            if (!DockingAutopilotMath.TryBuildActiveSpline(
                    _dockingStartAup.ToAbsoluteDouble3(),
                    startForward,
                    _dockingTargetAup.ToAbsoluteDouble3(),
                    targetForward,
                    targetUp,
                    _dockingSplineOwnerHash,
                    _dockingSplineRequestId,
                    duration,
                    ResolveDockingMathLodByte(),
                    out _activeDockingSpline))
            {
                _activeDockingSpline = default;
                _activeDockingSplineSlot = -1;
                return;
            }

            if (_dockingAutopilotService == null)
                CacheDockingAutopilotService();
            if (_fluidRuntime == null)
                CacheFluidRuntime();

            _activeDockingSplineSlot = -1;
            if (_dockingAutopilotService != null &&
                _dockingAutopilotService.TryAcquireSplineSlot(_dockingSplineOwnerHash, out int splineSlot) &&
                _dockingAutopilotService.TryWriteActiveSpline(splineSlot, in _activeDockingSpline))
            {
                _activeDockingSplineSlot = splineSlot;
            }

            _lowTierSplineFromPosition = startPosition;
            _lowTierSplineTargetPosition = startPosition;
            _lowTierSplineTargetRotation = startRotation;
            _lastDockingSplineTargetPosition = startPosition;
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
            float systemStress01 = ResolveSystemStress01();
            byte mathLod = _activeDockingSpline.MathLod;
            float splineProgress = DockingAutopilotMath.ResolveDockingProgress01(normalizedTime, mathLod, systemStress01);
            Vector3 actualPosition = ResolveTelemetryPosition();
            if (!IsFiniteVector(actualPosition))
            {
                AbortDockingForInvalidPose();
                return;
            }

            if (!TryEvaluateDockingSplinePose(
                    splineProgress,
                    safeFixedDeltaTime,
                    IsLowDockingMathTier(mathLod),
                    anchorPosition,
                    anchorRotation,
                    out Vector3 evaluatedPosition,
                    out Quaternion evaluatedRotation))
            {
                AbortDockingForInvalidPose();
                return;
            }

            if (!TryUpdateSplineDeviation(actualPosition, _lastDockingSplineTargetPosition))
            {
                AbortDockingForInvalidPose();
                return;
            }

            if (_lastSplineDeviationError > DockingDeviationAbortMeters)
            {
                AbortDockingForDeviation(actualPosition, _lastDockingSplineTargetPosition);
                return;
            }

            Vector3 flowVelocity = ResolveDockingFlowVelocity(evaluatedPosition);
            Vector3 commandVelocity = ResolveDockingCommandVelocity(
                actualPosition,
                evaluatedPosition,
                flowVelocity,
                safeFixedDeltaTime);
            _lastDockingFlowVelocity = ToFloat3(flowVelocity);
            _lastDockingCommandVelocity = commandVelocity;
            QueueDockingWakeSignals(evaluatedPosition, commandVelocity, safeFixedDeltaTime);
            TryPublishDockingCompleteSignal(splineProgress, anchorPosition, anchor.forward);

            if (_dockedBody != null)
            {
                _dockedBody.linearVelocity = commandVelocity;
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
            {
                RefreshDockedRelativeAup(anchor.position);
                TryPublishDockingCompleteSignal(1f, anchor.position, anchor.forward);
            }

            ConnectDockedCargoCrates();
            _dockingInProgress = false;
            _isDocked = true;

            if (!wasDocked)
                QueueDockingImpactSignal();

            PushDockedExternalMass();
            ReleaseActiveDockingSpline(DockingSplineRuntimeState.Completed);
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
            double3 relativeMeters = AbsoluteUniversePosition.DeltaMetersClamped(in worldAup, in habitatAup);
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
            PublishDockingFailedSignal(DockingFailureReason.InvalidRequest, ResolveTelemetryPosition(), _lastDockingSplineTargetPosition);
            ReleaseDockedTransport(false);
        }

        private void AbortDockingForDeviation(Vector3 actualPosition, Vector3 splineTargetPosition)
        {
            DumpDockTelemetry();
            PublishDockingFailedSignal(DockingFailureReason.ObstacleBlocked, actualPosition, splineTargetPosition);
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
                SplineDeviationError = _lastSplineDeviationError,
                FlowSpeed = FastMagnitudeFromSq(math.lengthsq(_lastDockingFlowVelocity)),
                Position = new float3(position.x, position.y, position.z),
                SplineTargetPosition = ToFloat3(_lastDockingSplineTargetPosition),
                CommandVelocity = ToFloat3(_lastDockingCommandVelocity),
                FlowVelocity = _lastDockingFlowVelocity,
                Rotation = new float4(rotation.x, rotation.y, rotation.z, rotation.w),
                GridX = aup.GridX,
                GridY = aup.GridY,
                GridZ = aup.GridZ,
                OwnerHash = _dockingSplineOwnerHash,
                RequestId = _dockingSplineRequestId,
                RuntimeFlags = _activeDockingSpline.Flags
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
            string path = Path.Combine(directory, "Dump_DOCKING_AUTOPILOT_SPLINE.bin");
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(0x4453504Cu);
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
                    writer.Write(entry.SplineDeviationError);
                    writer.Write(entry.FlowSpeed);
                    writer.Write(entry.Position.x);
                    writer.Write(entry.Position.y);
                    writer.Write(entry.Position.z);
                    writer.Write(entry.SplineTargetPosition.x);
                    writer.Write(entry.SplineTargetPosition.y);
                    writer.Write(entry.SplineTargetPosition.z);
                    writer.Write(entry.CommandVelocity.x);
                    writer.Write(entry.CommandVelocity.y);
                    writer.Write(entry.CommandVelocity.z);
                    writer.Write(entry.FlowVelocity.x);
                    writer.Write(entry.FlowVelocity.y);
                    writer.Write(entry.FlowVelocity.z);
                    writer.Write(entry.Rotation.x);
                    writer.Write(entry.Rotation.y);
                    writer.Write(entry.Rotation.z);
                    writer.Write(entry.Rotation.w);
                    writer.Write(entry.GridX);
                    writer.Write(entry.GridY);
                    writer.Write(entry.GridZ);
                    writer.Write(entry.OwnerHash);
                    writer.Write(entry.RequestId);
                    writer.Write(entry.RuntimeFlags);
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
            return false;
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

        private float ResolveSafeDockingDurationSeconds()
        {
            return math.isfinite(dockingDurationSeconds) && dockingDurationSeconds > 0f
                ? dockingDurationSeconds
                : DefaultDockingDurationSeconds;
        }

        private void CacheDockingAutopilotService()
        {
            _dockingAutopilotService = GlobalRegistry.TryGet(out IDockingAutopilotService service) ? service : null;
        }

        private void CacheFluidRuntime()
        {
            _fluidRuntime = GlobalRegistry.Fluid;
        }

        private void ResetDockingRuntimeCaches()
        {
            _activeDockingSpline = default;
            _activeDockingSplineSlot = -1;
            _lowTierSplineFromPosition = Vector3.zero;
            _lowTierSplineTargetPosition = Vector3.zero;
            _lowTierSplineTargetRotation = Quaternion.identity;
            _lastDockingSplineTargetPosition = Vector3.zero;
            _lastDockingCommandVelocity = Vector3.zero;
            _lastDockingFlowVelocity = float3.zero;
            _lowTierSplineBlendSeconds = 0f;
            _dockingWakeElapsedSeconds = 0f;
            _lastSplineDeviationError = 0f;
            _hasLowTierSplineSample = false;
            _dockingCompletionSignalPublished = false;
        }

        private static float ResolveSystemStress01()
        {
            float stress01 = HomeostasisBrain.SystemHealthIndex01;
            return math.isfinite(stress01) ? math.saturate(stress01) : 0f;
        }

        private static bool IsLowDockingMathTier(byte mathLod)
        {
            return mathLod == 0;
        }

        private bool TryEvaluateDockingSplinePose(
            float progress01,
            float fixedDeltaTime,
            bool lowTierMath,
            Vector3 fallbackPosition,
            Quaternion fallbackRotation,
            out Vector3 evaluatedPosition,
            out Quaternion evaluatedRotation)
        {
            if (!lowTierMath)
            {
                _hasLowTierSplineSample = false;
                _lowTierSplineBlendSeconds = 0f;
                return TryEvaluateDockingSplinePoseRaw(
                    progress01,
                    fallbackPosition,
                    fallbackRotation,
                    out evaluatedPosition,
                    out evaluatedRotation);
            }

            if (!_hasLowTierSplineSample || _lowTierSplineBlendSeconds >= LowTierSplineSampleIntervalSeconds)
            {
                Vector3 sourcePosition = ResolveTelemetryPosition();
                if (!IsFiniteVector(sourcePosition))
                    sourcePosition = fallbackPosition;

                if (!TryEvaluateDockingSplinePoseRaw(
                        progress01,
                        fallbackPosition,
                        fallbackRotation,
                        out Vector3 targetPosition,
                        out Quaternion targetRotation))
                {
                    evaluatedPosition = fallbackPosition;
                    evaluatedRotation = fallbackRotation;
                    return false;
                }

                _lowTierSplineFromPosition = sourcePosition;
                _lowTierSplineTargetPosition = targetPosition;
                _lowTierSplineTargetRotation = targetRotation;
                _lowTierSplineBlendSeconds = 0f;
                _hasLowTierSplineSample = true;
            }

            float alpha = math.saturate(_lowTierSplineBlendSeconds * math.rcp(LowTierSplineSampleIntervalSeconds));
            evaluatedPosition = LinearInterpolate(_lowTierSplineFromPosition, _lowTierSplineTargetPosition, alpha);
            evaluatedRotation = _lowTierSplineTargetRotation;
            _lowTierSplineBlendSeconds += math.max(0f, fixedDeltaTime);
            return IsFiniteVector(evaluatedPosition) && IsFiniteQuaternion(evaluatedRotation);
        }

        private bool TryEvaluateDockingSplinePoseRaw(
            float progress01,
            Vector3 fallbackPosition,
            Quaternion fallbackRotation,
            out Vector3 evaluatedPosition,
            out Quaternion evaluatedRotation)
        {
            evaluatedPosition = fallbackPosition;
            evaluatedRotation = fallbackRotation;
            if (_activeDockingSpline.OwnerHash == 0u)
                return false;

            _activeDockingSpline.Progress01 = math.saturate(progress01);
            bool evaluated = false;
            DockingSplineSample sample = default;
            if (_dockingAutopilotService != null && _activeDockingSplineSlot >= 0)
            {
                _dockingAutopilotService.TryWriteActiveSpline(_activeDockingSplineSlot, in _activeDockingSpline);
                evaluated = _dockingAutopilotService.TryEvaluateActiveSpline(_activeDockingSplineSlot, _activeDockingSpline.Progress01, out sample);
            }

            if (!evaluated)
                evaluated = DockingAutopilotMath.TryEvaluate(in _activeDockingSpline, _activeDockingSpline.Progress01, out sample);
            if (!evaluated)
                return false;

            evaluatedPosition = DockingAutopilotMath.ResolveRuntimePosition(sample.AbsolutePosition, fallbackPosition);
            evaluatedRotation = ResolveDockingSplineRotation(sample.Tangent, sample.Up, fallbackRotation);
            _lastDockingSplineTargetPosition = evaluatedPosition;
            return IsFiniteVector(evaluatedPosition) && IsFiniteQuaternion(evaluatedRotation);
        }

        private bool TryUpdateSplineDeviation(Vector3 actualPosition, Vector3 splineTargetPosition)
        {
            if (!IsFiniteVector(actualPosition) || !IsFiniteVector(splineTargetPosition))
                return false;

            Vector3 delta = actualPosition - splineTargetPosition;
            float deviationSq = delta.sqrMagnitude;
            if (!math.isfinite(deviationSq) || deviationSq < 0f)
                return false;

            _lastSplineDeviationError = FastMagnitudeFromSq(deviationSq);
            return math.isfinite(_lastSplineDeviationError);
        }

        private Vector3 ResolveDockingFlowVelocity(Vector3 samplePosition)
        {
            HectonFluidEngine fluid = _fluidRuntime;
            if (fluid == null ||
                !IsFiniteVector(samplePosition) ||
                !fluid.TrySampleModAbyssalFlow(samplePosition, out float3 flowVelocity) ||
                !math.all(math.isfinite(flowVelocity)))
            {
                return Vector3.zero;
            }

            return ToVector3(flowVelocity);
        }

        private Vector3 ResolveDockingCommandVelocity(
            Vector3 actualPosition,
            Vector3 evaluatedPosition,
            Vector3 flowVelocity,
            float fixedDeltaTime)
        {
            float safeDelta = math.max(0.0001f, fixedDeltaTime);
            Vector3 pathVelocity = (evaluatedPosition - actualPosition) * math.rcp(safeDelta);
            Vector3 compensatedVelocity = pathVelocity - flowVelocity;
            return IsFiniteVector(compensatedVelocity) ? compensatedVelocity : Vector3.zero;
        }

        private void QueueDockingWakeSignals(Vector3 position, Vector3 commandVelocity, float fixedDeltaTime)
        {
            _dockingWakeElapsedSeconds += math.max(0f, fixedDeltaTime);
            if (_dockingWakeElapsedSeconds < DockingWakeSignalIntervalSeconds)
                return;

            _dockingWakeElapsedSeconds = 0f;
            if (!IsFiniteVector(position) || !IsFiniteVector(commandVelocity))
                return;

            float speedSq = commandVelocity.sqrMagnitude;
            if (!math.isfinite(speedSq) || speedSq < 0.25f)
                return;

            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(position);
            float3 velocity = ToFloat3(commandVelocity);
            WakeGeneratedSignal wakeSignal = new WakeGeneratedSignal
            {
                PositionAup = positionAup,
                Velocity = velocity,
                SourceFlags = DockingWakeSourceVehicleFlag
            };
            GlobalSignals.Publish(in wakeSignal);

            float speed = FastMagnitudeFromSq(speedSq);
            FluidImpulseSignal impulseSignal = new FluidImpulseSignal
            {
                PositionAup = positionAup,
                Vector = velocity,
                Radius = math.clamp(1.5f + (speed * 0.15f), 1.5f, 8f),
                Lifetime = speedSq > 4f ? 1.25f : 0.75f,
                Frame = unchecked((uint)math.max(0, Time.frameCount)),
                SourceHash = DockingWakeSourceHash,
                Flags = DockingWakeSourceVehicleFlag
            };
            GlobalSignals.Publish(in impulseSignal);
        }

        private void TryPublishDockingCompleteSignal(float progress01, Vector3 dockPosition, Vector3 dockForward)
        {
            if (_dockingCompletionSignalPublished ||
                progress01 < DockingCompleteSignalProgress01 ||
                !IsFiniteVector(dockPosition) ||
                !IsFiniteVector(dockForward))
            {
                return;
            }

            AbsoluteUniversePosition dockAup = AbsoluteUniversePosition.FromRuntimePosition(dockPosition);
            DockingCompleteSignal signal = new DockingCompleteSignal
            {
                DroneId = unchecked((int)_dockingSplineOwnerHash),
                HubGridId = ResolveDockingHubGridId(),
                DockAup = AbsoluteUniversePositionBlit.FromAup(in dockAup),
                DockForward = DockingAutopilotMath.NormalizeOrFallback(ToFloat3(dockForward), new float3(0f, 0f, 1f)),
                RequestId = _dockingSplineRequestId,
                Flags = _activeDockingSpline.Flags,
                Reserved0 = 0,
                Reserved1 = 0,
                Reserved2 = 0
            };
            SignalBus<DockingCompleteSignal>.Push(in signal);
            _dockingCompletionSignalPublished = true;
        }

        private void PublishDockingFailedSignal(DockingFailureReason reason, Vector3 actualPosition, Vector3 targetPosition)
        {
            if (!IsFiniteVector(actualPosition))
                actualPosition = ResolveTelemetryPosition();
            if (!IsFiniteVector(actualPosition))
                return;

            Vector3 failureVector = IsFiniteVector(targetPosition)
                ? targetPosition - actualPosition
                : Vector3.zero;
            if (!IsFiniteVector(failureVector))
                failureVector = Vector3.zero;

            AbsoluteUniversePosition lastAup = AbsoluteUniversePosition.FromRuntimePosition(actualPosition);
            DockingFailedSignal signal = new DockingFailedSignal
            {
                DroneId = unchecked((int)_dockingSplineOwnerHash),
                HubGridId = ResolveDockingHubGridId(),
                LastAup = AbsoluteUniversePositionBlit.FromAup(in lastAup),
                FailureVector = ToFloat3(failureVector),
                RequestId = _dockingSplineRequestId,
                Reason = (byte)reason,
                Flags = _activeDockingSpline.Flags,
                Reserved0 = 0,
                Reserved1 = 0
            };
            SignalBus<DockingFailedSignal>.Push(in signal);
        }

        private void ReleaseActiveDockingSpline(DockingSplineRuntimeState finalState)
        {
            if (_activeDockingSplineSlot >= 0 && _dockingAutopilotService != null)
            {
                _activeDockingSpline.State = (byte)finalState;
                _activeDockingSpline.Progress01 = finalState == DockingSplineRuntimeState.Completed
                    ? 1f
                    : _activeDockingSpline.Progress01;
                _dockingAutopilotService.TryWriteActiveSpline(_activeDockingSplineSlot, in _activeDockingSpline);
                _dockingAutopilotService.TryReleaseSplineSlot(_activeDockingSplineSlot, _dockingSplineOwnerHash);
            }

            _activeDockingSpline = default;
            _activeDockingSplineSlot = -1;
        }

        private uint ResolveDockingSplineOwnerHash()
        {
            int instanceId = GetInstanceID();
            uint hash = unchecked((uint)instanceId);
            return hash != 0u ? hash : 1u;
        }

        private static byte ResolveDockingMathLodByte()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            switch (tier)
            {
                case HectonQualityTier.High:
                case HectonQualityTier.Ultra:
                    return 2;
                case HectonQualityTier.Mid:
                    return 1;
                default:
                    return 0;
            }
        }

        private int ResolveDockingHubGridId()
        {
            return _owningModule != null ? _owningModule.GetInstanceID() : GetInstanceID();
        }

        private static bool IsLowDockingMathTier(byte mathLod)
        {
            return mathLod == 0;
        }

        private static float ResolveSystemStress01()
        {
            return math.saturate(SignalBusRegistry.SystemStress01);
        }

        private void ResetDockingRuntimeCaches()
        {
            _hasLowTierSplineSample = false;
            _dockingCompletionSignalPublished = false;
            _lowTierSplineBlendSeconds = 0f;
            _dockingWakeElapsedSeconds = 0f;
            _lastSplineDeviationError = 0f;
            _lastDockingFlowVelocity = float3.zero;
            _lastDockingCommandVelocity = Vector3.zero;
            _lastDockingSplineTargetPosition = Vector3.zero;
            _lowTierSplineFromPosition = Vector3.zero;
            _lowTierSplineTargetPosition = Vector3.zero;
            _lowTierSplineTargetRotation = Quaternion.identity;
        }

        private static Vector3 LinearInterpolate(Vector3 from, Vector3 to, float alpha)
        {
            float t = math.saturate(alpha);
            return from + ((to - from) * t);
        }

        private static float FastMagnitudeFromSq(float magnitudeSq)
        {
            if (!math.isfinite(magnitudeSq) || magnitudeSq <= 0f)
                return 0f;

            return magnitudeSq * math.rsqrt(math.max(magnitudeSq, 0.000001f));
        }

        private static Quaternion ResolveDockingSplineRotation(float3 tangent, float3 up, Quaternion fallbackRotation)
        {
            Vector3 forward = ToVector3(tangent);
            if (!IsFiniteVector(forward) || forward.sqrMagnitude <= 0.000001f)
                return fallbackRotation;

            Vector3 upVector = ToVector3(up);
            if (!IsFiniteVector(upVector) || upVector.sqrMagnitude <= 0.000001f)
                upVector = Vector3.up;

            Quaternion rotation = Quaternion.LookRotation(forward, upVector);
            return IsFiniteQuaternion(rotation) ? rotation : fallbackRotation;
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
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
