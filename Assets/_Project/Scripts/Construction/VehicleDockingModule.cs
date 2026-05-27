using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
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
    public sealed class VehicleDockingModule : MonoBehaviour, ITickable, IFixedTickable, IUpdatable, ILateFrameTickable, IPowerComponent, IPoolable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private static int s_x001VehicleDockingModuleSignalPushDropCount;
        private const float MaxDockingFixedDeltaSeconds = 0.05f;
        private const float DockingAcquireDistanceSqMeters = 2f;
        private const float DockingAcquireAlignmentDot = 0.8f;
        private const float DefaultDockingDurationSeconds = 1.5f;
        private const float DefaultUndockEjectSpeedMetersPerSecond = 4.5f;
        private const float DefaultDockingImpactSpeedMetersPerSecond = 6.5f;
        private const float DockingDeviationAbortMeters = 5f;
        private const float DockingWakeSignalIntervalSeconds = 0.1f;
        private const float DockingCompleteSignalProgress01 = 0.95f;
        private const float MaxDockingAngularVelocityRadians = 8f;
        private const uint DockingWakeSourceHash = 0x44534C4Eu;
        private const uint DockingWakeSourceVehicleFlag = 2u;
        private const int DockTelemetryCapacity = 300;
        private const uint DockTelemetryDumpMagic = 0x4453504Cu;
        private const ushort DockTelemetryDumpVersion = 2;
        private const ushort DockTelemetryEntrySizeBytes = 128;
        private const int DockTelemetryDumpCooldownFrames = 30;
        private const int DockedCargoCrateCapacity = 16;
        private const int DockedCargoTraversalCapacity = 64;

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct DockTelemetryEntry
        {
            [FieldOffset(0)]
            public int Frame;
            [FieldOffset(4)]
            public byte State;
            [FieldOffset(5)]
            public byte HasPower;
            [FieldOffset(6)]
            public byte HasRelativeAup;
            [FieldOffset(7)]
            public byte Reserved;
            [FieldOffset(8)]
            public float DistanceSq;
            [FieldOffset(12)]
            public float AlignmentDot;
            [FieldOffset(16)]
            public float SplineDeviationError;
            [FieldOffset(20)]
            public float FlowSpeed;
            [FieldOffset(24)]
            public float3 Position;
            [FieldOffset(36)]
            public float3 SplineTargetPosition;
            [FieldOffset(48)]
            public float3 CommandVelocity;
            [FieldOffset(60)]
            public float3 FlowVelocity;
            [FieldOffset(72)]
            public float4 Rotation;
            [FieldOffset(88)]
            public long GridX;
            [FieldOffset(96)]
            public long GridY;
            [FieldOffset(104)]
            public long GridZ;
            [FieldOffset(112)]
            public uint OwnerHash;
            [FieldOffset(116)]
            public uint RequestId;
            [FieldOffset(120)]
            public uint RuntimeFlags;
            [FieldOffset(124)]
            public uint ReservedTail;
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

        // COLD ALLOC: fixed cargo bridge array - bounded to avoid managed List growth during docking.
        private readonly StorageCrate[] _connectedCargoCrates = new StorageCrate[DockedCargoCrateCapacity];
        // COLD ALLOC: fixed transform traversal stack - prevents component-query buffer growth.
        private readonly Transform[] _cargoTraversalStack = new Transform[DockedCargoTraversalCapacity];
        private int _connectedCargoCrateCount;
        private Transform _cachedTransform;
        private Collider _triggerCollider;
        private CachedTriggerVolume _triggerVolume;
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
        private IDockedExternalMassSink _dockedExternalMassSink;
        private IAbyssalFlowGpuReadModel _fluidRuntime;
        private IPhysicsService _physicsService;
        private IPhysicsStateEventService _physicsStateEvents;
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
        private ITransportDockControlLock _mountedTransportLockOwner;
        private Vector3 _lastDockingSplineTargetPosition;
        private Quaternion _lastDockingSplineRotation = Quaternion.identity;
        private Vector3 _lastDockingCommandVelocity;
        private float3 _lastDockingFlowVelocity;
        private float _dockingWakeElapsedSeconds;
        private float _lastSplineDeviationError;
        private bool _dockingCompletionSignalPublished;
        private ulong _lastRejectedDockColliderId;
        private bool _hasDockedRelativeAup;
        private IDataVault _dataVault;
        private VaultGenerationHandle<DockTelemetryEntry> _dockTelemetryHandle;
        private VaultGenerationHandle<int> _dockTelemetryCursorHandle;
        private int _lastDockTelemetryDumpFrame = -DockTelemetryDumpCooldownFrames;
        private bool _hotSwapRegistered;
        private bool _registeredLateFrame;
        private Transform _pendingDockedTransform;
        private Vector3 _pendingDockedTransformPosition;
        private Quaternion _pendingDockedTransformRotation = Quaternion.identity;
        private bool _pendingDockedTransformPoseDirty;

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
            _triggerVolume = CachedTriggerVolume.FromCollider(_triggerCollider, 3f);
            _powerNode = GetComponent<PowerNode>();
            _owningModule = GetComponentInParent<BaseModule>();
            _dockingSplineOwnerHash = ResolveDockingSplineOwnerHash();
            CacheDockTelemetryVaultCold();
            EnsureDockTelemetry();
        }

        private void OnValidate()
        {
            SanitizeDockingSettings();
        }

        private void OnEnable()
        {
            CacheDockTelemetryVaultCold();
            EnsureDockTelemetry();
            TryRegisterHotSwapListener();
            CacheDockingAutopilotService();
            CacheFluidRuntime();
            CachePhysicsRoutes();
            HectonFloatingOrigin.RegisterListener(this);
            TryRegister();
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            ReleaseDockedTransport();
            TryUnregister();
            TryUnregisterHotSwapListener();
            DisposeDockTelemetry();
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            ReleaseDockedTransport();
            TryUnregister();
            TryUnregisterHotSwapListener();
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
            ResetDockingRuntimeCaches();
            _lastRejectedDockColliderId = 0UL;
            CacheDockTelemetryVaultCold();
            EnsureDockTelemetry();
            TryRegisterHotSwapListener();
            CacheDockingAutopilotService();
            CacheFluidRuntime();
            CachePhysicsRoutes();
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
            ResetDockingRuntimeCaches();
            _lastRejectedDockColliderId = 0UL;
            _debugDockOccupied = false;
            _debugDockedTransportName = string.Empty;
            TryUnregister();
            TryUnregisterHotSwapListener();
            DisposeDockTelemetry();
        }

        public void Tick(float deltaTime)
        {
            RecordDockTelemetry();
            RefreshDockingCandidatesFromRegistry();

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

        public void LateFrameTick()
        {
            FlushQueuedDockedTransformPose();
        }

        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            _debugHasPower = hasPower;

            if (!hasPower)
                _activelyCharging = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DockingAutopilotRuntime)
            {
                RebindDockingAutopilotService(
                    previousService as IDockingAutopilotService,
                    currentService as IDockingAutopilotService);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.FluidRuntime)
            {
                _fluidRuntime = currentService as IAbyssalFlowGpuReadModel;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Physics)
            {
                _physicsService = currentService as IPhysicsService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.PhysicsStateManager)
            {
                _physicsStateEvents = currentService as IPhysicsStateEventService;
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            if (ReferenceEquals(_dataVault, currentService))
                return;

            ClearDockTelemetryDescriptor();
            _dataVault = currentService as IDataVault;
            EnsureDockTelemetry();
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
            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }
            _registered = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.UnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void RefreshDockingCandidatesFromRegistry()
        {
            if (_dockedTransport != null || _dockingInProgress)
                return;

            for (int i = 0; i < PlayerTransportLifecycleRegistry.SlotCapacity; i++)
            {
                if (!PlayerTransportLifecycleRegistry.TryGetAt(i, out IPlayerTransportLifecycleOwner owner, out MonoBehaviour ownerBehaviour))
                    continue;

                if (!IsTransportInsideDockVolume(ownerBehaviour))
                    continue;

                if (PassesDockingAcquisitionGate(ownerBehaviour))
                {
                    _lastRejectedDockColliderId = 0UL;
                    DockTransport(owner, ownerBehaviour);
                    return;
                }
            }
        }

        private bool IsTransportInsideDockVolume(MonoBehaviour transportBehaviour)
        {
            if (transportBehaviour == null)
                return false;

            return TryResolveCandidatePose(transportBehaviour, out Vector3 candidatePosition, out _) &&
                   _triggerVolume.Contains(_cachedTransform, candidatePosition);
        }

        private bool PassesDockingAcquisitionGate(MonoBehaviour transportBehaviour)
        {
            if (transportBehaviour == null || !TryResolveDockAnchor(out Transform anchor))
                return false;

            if (!TryResolveCandidatePose(transportBehaviour, out Vector3 candidatePosition, out Quaternion candidateRotation))
                return false;

            if (RuntimeDistanceSq(candidatePosition, anchor.position) >= DockingAcquireDistanceSqMeters)
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

            CaptureDockedBody(transportBehaviour);
            CaptureDockedVehicleMotor(transportBehaviour);
            CaptureDockedExternalMassSink(transportBehaviour);
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

                QueueLinearVelocitySet(_dockedBody, Vector3.zero, wake: false);
                QueueAngularVelocitySet(_dockedBody, Vector3.zero, wake: false);
                _dockedBody.isKinematic = _cachedBodyWasKinematic;
                _dockedBody.useGravity = _cachedBodyUseGravity;
                _dockedBody.constraints = _cachedBodyConstraints;
                _dockedBody.interpolation = _cachedBodyInterpolation;

                if (applyEjectVelocity)
                    ApplyUndockEjectVelocity(_dockedBody, ejectVelocity);
            }

            _physicsStateEvents?.UnregisterDockConnectionOwner(this);
            EndDockingControlLock();
            _dockedBody = null;
            _dockedVehicleMotor = null;
            if (_dockedExternalMassSink != null)
                _dockedExternalMassSink.SetDockedExternalMassKilograms(0f);
            _dockedExternalMassSink = null;
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

        private void CaptureDockedBody(MonoBehaviour transportBehaviour)
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
            QueueLinearVelocitySet(_dockedBody, Vector3.zero, wake: false);
            QueueAngularVelocitySet(_dockedBody, Vector3.zero, wake: false);
            _dockedBody.isKinematic = true;
            _dockedBody.useGravity = false;
            _dockedBody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void CaptureDockedVehicleMotor(MonoBehaviour transportBehaviour)
        {
            _dockedVehicleMotor = null;
            if (transportBehaviour == null)
                return;

            if (!transportBehaviour.TryGetComponent(out _dockedVehicleMotor))
                _dockedVehicleMotor = transportBehaviour.GetComponentInParent<VehicleMotor>();
        }

        private void CaptureDockedExternalMassSink(MonoBehaviour transportBehaviour)
        {
            _dockedExternalMassSink = null;
            if (transportBehaviour == null)
                return;

            _dockedExternalMassSink = ComponentReferenceUtility.ResolveParentService<IDockedExternalMassSink>(transportBehaviour);

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
            if (!TryResolveAupFromRuntimeOrigin(startPosition, out _dockingStartAup))
            {
                _activeDockingSpline = default;
                _activeDockingSplineSlot = -1;
                return;
            }

            _dockingTargetAup = anchor != null && IsFiniteVector(anchor.position)
                ? OffsetAupByRuntimeDelta(in _dockingStartAup, startPosition, anchor.position)
                : _dockingStartAup;
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
                    DockingAutopilotMath.AuthoritativeMathLod,
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

            _lastDockingSplineTargetPosition = startPosition;
            _lastDockingSplineRotation = startRotation;
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

            QueueDockedTransformPose(_dockedTransform, anchor.position, anchor.rotation);
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
            _dockingTargetAup = OffsetAupByRuntimeDelta(in _dockingStartAup, _dockingStartPosition, anchorPosition);
            RefreshDockedRelativeAup(anchorPosition);
            float normalizedTime = math.saturate(_dockingElapsedSeconds * math.rcp(duration));
            float splineProgress = DockingAutopilotMath.ResolveDockingProgress01(normalizedTime);
            Vector3 actualPosition = ResolveTelemetryPosition();
            if (!IsFiniteVector(actualPosition))
            {
                AbortDockingForInvalidPose();
                return;
            }

            if (!TryEvaluateDockingSplinePose(
                    splineProgress,
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
            Vector3 commandAngularVelocity = ResolveDockingCommandAngularVelocity(
                evaluatedRotation,
                safeFixedDeltaTime);
            _lastDockingFlowVelocity = ToFloat3(flowVelocity);
            _lastDockingCommandVelocity = commandVelocity;
            QueueDockingWakeSignals(evaluatedPosition, commandVelocity, safeFixedDeltaTime);
            TryPublishDockingCompleteSignal(splineProgress, anchorPosition, anchor.forward);

            if (_dockedBody != null)
            {
                QueueLinearVelocitySet(_dockedBody, commandVelocity);
                QueueAngularVelocitySet(_dockedBody, commandAngularVelocity);
                _dockedBody.MovePosition(evaluatedPosition);
                _dockedBody.MoveRotation(evaluatedRotation);
            }
            else if (_dockedTransform != null)
            {
                QueueDockedTransformPose(_dockedTransform, evaluatedPosition, evaluatedRotation);
            }

            _lastDockingSplineRotation = evaluatedRotation;

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
                QueueLinearVelocitySet(_dockedBody, Vector3.zero, wake: false);
                QueueAngularVelocitySet(_dockedBody, Vector3.zero, wake: false);
                _dockedBody.isKinematic = true;
                _dockedBody.useGravity = false;
                _physicsStateEvents?.RegisterDockConnectionOwner(this, _dockedBody);
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
            if (!TryResolveAupFromRuntimeOrigin(dockRuntimePosition, out AbsoluteUniversePosition dockWorldAup))
            {
                _hasDockedRelativeAup = false;
                return;
            }

            _habitatReferenceAup = ResolveHabitatReferenceAup(in dockWorldAup, dockRuntimePosition);
            _dockedRelativeAup = ResolveRelativeToHabitatAup(dockWorldAup, _habitatReferenceAup);
            _hasDockedRelativeAup = true;
        }

        private void QueueDockedTransformPose(Transform target, Vector3 position, Quaternion rotation)
        {
            if (target == null)
                return;

            _pendingDockedTransform = target;
            _pendingDockedTransformPosition = position;
            _pendingDockedTransformRotation = rotation;
            _pendingDockedTransformPoseDirty = true;
        }

        private void FlushQueuedDockedTransformPose()
        {
            if (!_pendingDockedTransformPoseDirty)
                return;

            _pendingDockedTransformPoseDirty = false;
            if (_pendingDockedTransform != null)
                _pendingDockedTransform.SetPositionAndRotation(_pendingDockedTransformPosition, _pendingDockedTransformRotation);
        }

        private AbsoluteUniversePosition ResolveHabitatReferenceAup(
            in AbsoluteUniversePosition dockWorldAup,
            Vector3 dockRuntimePosition)
        {
            if (_owningModule != null)
            {
                Transform ownerTransform = _owningModule.transform;
                if (ownerTransform != null && IsFiniteVector(ownerTransform.position))
                    return OffsetAupByRuntimeDelta(in dockWorldAup, dockRuntimePosition, ownerTransform.position);
            }

            return dockWorldAup;
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
            _physicsStateEvents?.QueueKinematicImpactEvent(_dockedBody, null, point, normal, impactSpeed);
        }

        private void ApplyUndockEjectVelocity(Rigidbody body, Vector3 ejectVelocity)
        {
            if (body == null || body.isKinematic || !IsFiniteVector(ejectVelocity))
                return;

            QueueLinearVelocitySet(body, ejectVelocity);
            QueueAngularVelocitySet(body, Vector3.zero);
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
            if (_dockedExternalMassSink == null)
                return;

            _dockedExternalMassSink.SetDockedExternalMassKilograms(_attachedDroneMassKg);
        }

        private void EnsureDockTelemetry()
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                ClearDockTelemetryDescriptor();
                return;
            }

            if (TryValidateDockTelemetryHandles(
                    vault,
                    in _dockTelemetryHandle,
                    in _dockTelemetryCursorHandle,
                    out _,
                    out _))
            {
                return;
            }

            if (vault.TryGetGenerationHandle<DockTelemetryEntry>(
                    BufferID.VehicleDockingTelemetryRing,
                    out VaultGenerationHandle<DockTelemetryEntry> borrowedRing) &&
                vault.TryGetGenerationHandle<int>(
                    BufferID.VehicleDockingTelemetryCursor,
                    out VaultGenerationHandle<int> borrowedCursor) &&
                TryValidateDockTelemetryHandles(vault, in borrowedRing, in borrowedCursor, out _, out _))
            {
                _dockTelemetryHandle = borrowedRing;
                _dockTelemetryCursorHandle = borrowedCursor;
                return;
            }

            if (vault.IsAllocationLocked)
            {
                ClearDockTelemetryDescriptor();
                return;
            }

            VaultGenerationHandle<DockTelemetryEntry> acquiredRing = vault.EnsureGenerationHandle<DockTelemetryEntry>(
                BufferID.VehicleDockingTelemetryRing,
                DockTelemetryCapacity,
                SystemID.VehiclesPhysics,
                NativeArrayOptions.ClearMemory);
            VaultGenerationHandle<int> acquiredCursor = vault.EnsureGenerationHandle<int>(
                BufferID.VehicleDockingTelemetryCursor,
                1,
                SystemID.VehiclesPhysics,
                NativeArrayOptions.ClearMemory);

            if (!TryValidateDockTelemetryHandles(vault, in acquiredRing, in acquiredCursor, out _, out _))
            {
                ClearDockTelemetryDescriptor();
                return;
            }

            _dockTelemetryHandle = acquiredRing;
            _dockTelemetryCursorHandle = acquiredCursor;
        }

        private void CacheDockTelemetryVaultCold()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (ReferenceEquals(_dataVault, vault))
                return;

            ClearDockTelemetryDescriptor();
            _dataVault = vault;
        }

        private void DisposeDockTelemetry()
        {
            ClearDockTelemetryDescriptor();
        }

        private void RecordDockTelemetry()
        {
            if (!TryAcquireDockTelemetryWrite(
                    out NativeArray<DockTelemetryEntry> telemetry,
                    out int telemetryLength,
                    out NativeArray<int> cursorBuffer,
                    out IDataVault vault))
            {
                return;
            }

            try
            {
                Vector3 position = ResolveTelemetryPosition();
                Quaternion rotation = ResolveTelemetryRotation();
                if (!IsFiniteVector(position) || !IsFiniteQuaternion(rotation))
                {
                    DumpDockTelemetryLocked(telemetry, telemetryLength, cursorBuffer);
                    return;
                }

                if (!TryResolveAupFromRuntimeOrigin(position, out AbsoluteUniversePosition aup))
                {
                    DumpDockTelemetryLocked(telemetry, telemetryLength, cursorBuffer);
                    return;
                }

                Transform anchor = ResolveDockAnchor();
                float distanceSq = 0f;
                float alignmentDot = 0f;
                if (anchor != null && IsFiniteVector(anchor.position))
                {
                    AbsoluteUniversePosition anchorAup = OffsetAupByRuntimeDelta(in aup, position, anchor.position);
                    double resolvedDistanceSq = AbsoluteUniversePosition.DistanceSq(in aup, in anchorAup);
                    distanceSq = resolvedDistanceSq < float.MaxValue ? (float)resolvedDistanceSq : float.MaxValue;
                    alignmentDot = IsFiniteQuaternion(anchor.rotation)
                        ? Vector3.Dot(rotation * Vector3.forward, anchor.forward)
                        : 0f;
                }

                int cursor = SanitizeDockTelemetryCursor(cursorBuffer[0], telemetryLength);
                telemetry[cursor] = new DockTelemetryEntry
                {
                    Frame = unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId),
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
                    RuntimeFlags = _activeDockingSpline.Flags,
                    ReservedTail = 0u
                };
                cursor++;
                if (cursor >= telemetryLength)
                    cursor = 0;
                cursorBuffer[0] = cursor;
            }
            finally
            {
                ReleaseDockTelemetryWriteLocks(vault);
            }
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
            if (!TryAcquireDockTelemetryWrite(
                    out NativeArray<DockTelemetryEntry> telemetry,
                    out int telemetryLength,
                    out NativeArray<int> cursorBuffer,
                    out IDataVault vault))
            {
                return;
            }

            try
            {
                DumpDockTelemetryLocked(telemetry, telemetryLength, cursorBuffer);
            }
            finally
            {
                ReleaseDockTelemetryWriteLocks(vault);
            }
        }

        private void DumpDockTelemetryLocked(
            NativeArray<DockTelemetryEntry> telemetry,
            int telemetryLength,
            NativeArray<int> cursorBuffer)
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (frame - _lastDockTelemetryDumpFrame < DockTelemetryDumpCooldownFrames)
                return;
            _lastDockTelemetryDumpFrame = frame;

            string projectRoot = Application.dataPath;
            if (!string.IsNullOrEmpty(projectRoot))
                projectRoot = Directory.GetParent(projectRoot)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                projectRoot = Directory.GetCurrentDirectory();

            string directory = Path.Combine(projectRoot, "Docs", "AgentLogs");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "Dump_1306_Construction_DockingAutopilotSpline.bin");
            int cursor = SanitizeDockTelemetryCursor(cursorBuffer[0], telemetryLength);
            cursorBuffer[0] = cursor;
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(DockTelemetryDumpMagic);
                writer.Write(DockTelemetryDumpVersion);
                writer.Write(DockTelemetryEntrySizeBytes);
                writer.Write(telemetryLength);
                writer.Write(cursor);
                int index = cursor;
                for (int i = 0; i < telemetryLength; i++)
                {
                    DockTelemetryEntry entry = telemetry[index];
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
                    writer.Write(entry.ReservedTail);
                    index++;
                    if (index >= telemetryLength)
                        index = 0;
                }
            }
        }

        private bool TryAcquireDockTelemetryWrite(
            out NativeArray<DockTelemetryEntry> telemetry,
            out int telemetryLength,
            out NativeArray<int> cursor,
            out IDataVault vault)
        {
            telemetry = default;
            telemetryLength = 0;
            cursor = default;
            vault = null;
            EnsureDockTelemetry();
            vault = _dataVault;
            if (vault == null ||
                !IsVaultHandleCreated(in _dockTelemetryHandle) ||
                !IsVaultHandleCreated(in _dockTelemetryCursorHandle))
            {
                ClearDockTelemetryDescriptor();
                return false;
            }

            bool ringLocked = false;
            bool cursorLocked = false;
            if (!vault.TryAcquireWriteLock(in _dockTelemetryHandle, SystemID.VehiclesPhysics, out telemetry))
                return false;
            ringLocked = true;
            if (!vault.TryAcquireWriteLock(in _dockTelemetryCursorHandle, SystemID.VehiclesPhysics, out cursor))
            {
                ReleaseDockTelemetryWriteLocks(vault, cursorLocked, ringLocked);
                telemetry = default;
                return false;
            }
            cursorLocked = true;

            if (!telemetry.IsCreated ||
                telemetry.Length < DockTelemetryCapacity ||
                !cursor.IsCreated ||
                cursor.Length < 1)
            {
                ReleaseDockTelemetryWriteLocks(vault, cursorLocked, ringLocked);
                telemetry = default;
                cursor = default;
                ClearDockTelemetryDescriptor();
                return false;
            }

            telemetryLength = telemetry.Length;
            int sanitizedCursor = SanitizeDockTelemetryCursor(cursor[0], telemetryLength);
            if (cursor[0] != sanitizedCursor)
                cursor[0] = sanitizedCursor;
            return true;
        }

        private void ReleaseDockTelemetryWriteLocks(IDataVault vault)
        {
            ReleaseDockTelemetryWriteLocks(vault, true, true);
        }

        private void ReleaseDockTelemetryWriteLocks(IDataVault vault, bool cursorLocked, bool ringLocked)
        {
            if (vault == null)
                return;

            if (cursorLocked)
                vault.ReleaseWriteLock(in _dockTelemetryCursorHandle, SystemID.VehiclesPhysics);
            if (ringLocked)
                vault.ReleaseWriteLock(in _dockTelemetryHandle, SystemID.VehiclesPhysics);
        }

        private static bool TryValidateDockTelemetryHandles(
            IDataVault vault,
            in VaultGenerationHandle<DockTelemetryEntry> ringHandle,
            in VaultGenerationHandle<int> cursorHandle,
            out NativeArray<DockTelemetryEntry> telemetry,
            out NativeArray<int> cursor)
        {
            telemetry = default;
            cursor = default;
            return vault != null &&
                   IsVaultHandleCreated(in ringHandle) &&
                   IsVaultHandleCreated(in cursorHandle) &&
                   vault.TryReadHandle(in ringHandle, out telemetry) &&
                   telemetry.IsCreated &&
                   telemetry.Length >= DockTelemetryCapacity &&
                   vault.TryReadHandle(in cursorHandle, out cursor) &&
                   cursor.IsCreated &&
                   cursor.Length >= 1;
        }

        private void ClearDockTelemetryDescriptor()
        {
            _dockTelemetryHandle = default;
            _dockTelemetryCursorHandle = default;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static int SanitizeDockTelemetryCursor(int cursor, int telemetryLength)
        {
            return telemetryLength > 0 && (uint)cursor < (uint)telemetryLength ? cursor : 0;
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
            dockingDurationSeconds = math.isfinite(dockingDurationSeconds)
                ? math.clamp(dockingDurationSeconds, 0.05f, 8f)
                : DefaultDockingDurationSeconds;
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
            _dockingAutopilotService = GlobalRegistry.DockingAutopilot;
        }

        private void CacheFluidRuntime()
        {
            _fluidRuntime = GlobalRegistry.AbyssalFlowGpu;
        }

        private void CachePhysicsRoutes()
        {
            _physicsService = GlobalRegistry.Physics;
            _physicsStateEvents = GlobalRegistry.PhysicsStateEvents;
        }

        private bool QueueLinearVelocitySet(Rigidbody body, Vector3 linearVelocity, bool wake = true)
        {
            IPhysicsService physicsService = _physicsService;
            return physicsService != null && physicsService.QueueLinearVelocitySet(body, linearVelocity, wake);
        }

        private bool QueueAngularVelocitySet(Rigidbody body, Vector3 angularVelocity, bool wake = true)
        {
            IPhysicsService physicsService = _physicsService;
            return physicsService != null && physicsService.QueueAngularVelocitySet(body, angularVelocity, wake);
        }

        private void RebindDockingAutopilotService(
            IDockingAutopilotService previousService,
            IDockingAutopilotService currentService)
        {
            IDockingAutopilotService releaseService = previousService ?? _dockingAutopilotService;
            int activeSlot = _activeDockingSplineSlot;
            if (activeSlot >= 0 && releaseService != null)
                releaseService.TryReleaseSplineSlot(activeSlot, _dockingSplineOwnerHash);

            _dockingAutopilotService = currentService;
            _activeDockingSplineSlot = -1;

            if (!_dockingInProgress ||
                _activeDockingSpline.OwnerHash == 0u ||
                _dockingAutopilotService == null)
            {
                return;
            }

            if (_dockingAutopilotService.TryAcquireSplineSlot(_dockingSplineOwnerHash, out int splineSlot) &&
                _dockingAutopilotService.TryWriteActiveSpline(splineSlot, in _activeDockingSpline))
            {
                _activeDockingSplineSlot = splineSlot;
            }
        }

        private bool TryEvaluateDockingSplinePose(
            float progress01,
            Vector3 fallbackPosition,
            Quaternion fallbackRotation,
            out Vector3 evaluatedPosition,
            out Quaternion evaluatedRotation)
        {
            return TryEvaluateDockingSplinePoseRaw(
                progress01,
                fallbackPosition,
                fallbackRotation,
                out evaluatedPosition,
                out evaluatedRotation);
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
            IAbyssalFlowGpuReadModel fluid = _fluidRuntime;
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

            if (!TryResolveAupFromRuntimeOrigin(position, out AbsoluteUniversePosition positionAup))
                return;

            float3 velocity = ToFloat3(commandVelocity);
            WakeGeneratedSignal wakeSignal = new WakeGeneratedSignal
            {
                PositionAup = positionAup,
                Velocity = velocity,
                SourceFlags = DockingWakeSourceVehicleFlag
            };
            SignalBus<WakeGeneratedSignal>.TryPushTracked(in wakeSignal, ref s_x001VehicleDockingModuleSignalPushDropCount);

            float speed = FastMagnitudeFromSq(speedSq);
            FluidImpulseSignal impulseSignal = new FluidImpulseSignal
            {
                PositionAup = positionAup,
                Vector = velocity,
                Radius = math.clamp(1.5f + (speed * 0.15f), 1.5f, 8f),
                Lifetime = speedSq > 4f ? 1.25f : 0.75f,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                SourceHash = DockingWakeSourceHash,
                Flags = DockingWakeSourceVehicleFlag
            };
            SignalBus<FluidImpulseSignal>.TryPushTracked(in impulseSignal, ref s_x001VehicleDockingModuleSignalPushDropCount);
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

            if (!TryResolveAupFromRuntimeOrigin(dockPosition, out AbsoluteUniversePosition dockAup))
                return;

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
                Reserved2 = 0,
                ReservedTail = 0u
            };
            SignalBus<DockingCompleteSignal>.TryPushTracked(in signal, ref s_x001VehicleDockingModuleSignalPushDropCount);
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

            if (!TryResolveAupFromRuntimeOrigin(actualPosition, out AbsoluteUniversePosition lastAup))
                return;

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
                Reserved1 = 0,
                ReservedTail = 0u
            };
            SignalBus<DockingFailedSignal>.TryPushTracked(in signal, ref s_x001VehicleDockingModuleSignalPushDropCount);
        }

        private void ReleaseActiveDockingSpline(DockingSplineRuntimeState finalState)
        {
            if (_activeDockingSplineSlot >= 0 && _dockingAutopilotService != null)
            {
                _activeDockingSpline.State = (byte)finalState;
                _activeDockingSpline.Progress01 = finalState == DockingSplineRuntimeState.Completed
                    ? 1f
                    : _activeDockingSpline.Progress01;
                _dockingAutopilotService.TryReleaseSplineSlot(_activeDockingSplineSlot, _dockingSplineOwnerHash);
            }

            _activeDockingSpline = default;
            _activeDockingSplineSlot = -1;
        }

        private uint ResolveDockingSplineOwnerHash()
        {
            int instanceId = GetEntityId().GetHashCode();
            uint hash = unchecked((uint)instanceId);
            return hash != 0u ? hash : 1u;
        }

        private int ResolveDockingHubGridId()
        {
            return _owningModule != null ? _owningModule.GetEntityId().GetHashCode() : GetEntityId().GetHashCode();
        }

        private void ResetDockingRuntimeCaches()
        {
            _dockingCompletionSignalPublished = false;
            _dockingWakeElapsedSeconds = 0f;
            _lastSplineDeviationError = 0f;
            _lastDockingFlowVelocity = float3.zero;
            _lastDockingCommandVelocity = Vector3.zero;
            _lastDockingSplineTargetPosition = Vector3.zero;
            _lastDockingSplineRotation = Quaternion.identity;
        }

        private Vector3 ResolveDockingCommandAngularVelocity(Quaternion evaluatedRotation, float fixedDeltaTime)
        {
            if (!IsFiniteQuaternion(evaluatedRotation))
                return Vector3.zero;

            Quaternion previousRotation = IsFiniteQuaternion(_lastDockingSplineRotation)
                ? _lastDockingSplineRotation
                : evaluatedRotation;
            Quaternion currentRotation = NormalizeQuaternionOrFallback(evaluatedRotation, previousRotation);
            previousRotation = NormalizeQuaternionOrFallback(previousRotation, currentRotation);
            Quaternion deltaRotation = currentRotation * Quaternion.Inverse(previousRotation);
            if (!IsFiniteQuaternion(deltaRotation))
                return Vector3.zero;
            if (deltaRotation.w < 0f)
            {
                deltaRotation.x = -deltaRotation.x;
                deltaRotation.y = -deltaRotation.y;
                deltaRotation.z = -deltaRotation.z;
                deltaRotation.w = -deltaRotation.w;
            }

            deltaRotation.ToAngleAxis(out float angleDegrees, out Vector3 axis);
            if (!math.isfinite(angleDegrees) || !IsFiniteVector(axis))
                return Vector3.zero;
            if (angleDegrees > 180f)
                angleDegrees -= 360f;

            float axisLengthSq = axis.sqrMagnitude;
            if (!math.isfinite(axisLengthSq) || axisLengthSq <= 0.000001f)
                return Vector3.zero;

            float invAxisLength = math.rsqrt(math.max(axisLengthSq, 0.000001f));
            float invDelta = math.rcp(math.max(0.0001f, fixedDeltaTime));
            Vector3 angularVelocity = axis * (invAxisLength * math.radians(angleDegrees) * invDelta);
            float magnitudeSq = angularVelocity.sqrMagnitude;
            if (!math.isfinite(magnitudeSq))
                return Vector3.zero;

            float maxAngularVelocity = ResolveMaxDockingAngularVelocityRadians();
            float maxMagnitudeSq = maxAngularVelocity * maxAngularVelocity;
            if (magnitudeSq > maxMagnitudeSq)
                angularVelocity *= math.rsqrt(math.max(magnitudeSq, 0.000001f)) * maxAngularVelocity;

            return IsFiniteVector(angularVelocity) ? angularVelocity : Vector3.zero;
        }

        private float ResolveMaxDockingAngularVelocityRadians()
        {
            float spring = math.isfinite(dockingRotationSpring) ? math.max(0f, dockingRotationSpring) : 0f;
            float damping = math.isfinite(dockingRotationDamping) ? math.max(0f, dockingRotationDamping) : 0f;
            return math.clamp((spring + damping) * 0.25f, 0.5f, MaxDockingAngularVelocityRadians);
        }

        private static float FastMagnitudeFromSq(float magnitudeSq)
        {
            if (!math.isfinite(magnitudeSq) || magnitudeSq <= 0f)
                return 0f;

            return magnitudeSq * math.rsqrt(math.max(magnitudeSq, 0.000001f));
        }

        private static double RuntimeDistanceSq(Vector3 a, Vector3 b)
        {
            double dx = (double)a.x - b.x;
            double dy = (double)a.y - b.y;
            double dz = (double)a.z - b.z;
            return (dx * dx) + (dy * dy) + (dz * dz);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private static bool TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup)
        {
            originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            return IsFiniteAup(in originAup);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFiniteVector(runtimePosition) ||
                !TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup))
            {
                return false;
            }

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in positionAup);
        }

        private static AbsoluteUniversePosition OffsetAupByRuntimeDelta(
            in AbsoluteUniversePosition referenceAup,
            Vector3 referenceRuntimePosition,
            Vector3 targetRuntimePosition)
        {
            double3 localDelta = new double3(
                (double)targetRuntimePosition.x - referenceRuntimePosition.x,
                (double)targetRuntimePosition.y - referenceRuntimePosition.y,
                (double)targetRuntimePosition.z - referenceRuntimePosition.z);
            return AbsoluteUniversePosition.OffsetMeters(in referenceAup, localDelta);
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

        private static Quaternion NormalizeQuaternionOrFallback(Quaternion value, Quaternion fallback)
        {
            float4 q = new float4(value.x, value.y, value.z, value.w);
            float lengthSq = math.lengthsq(q);
            if (!math.all(math.isfinite(q)) || !math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                q = new float4(fallback.x, fallback.y, fallback.z, fallback.w);

            lengthSq = math.lengthsq(q);
            if (!math.all(math.isfinite(q)) || !math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return Quaternion.identity;

            q *= math.rsqrt(math.max(lengthSq, 0.000001f));
            return new Quaternion(q.x, q.y, q.z, q.w);
        }

        private void BeginDockingControlLock(MonoBehaviour transportBehaviour)
        {
            _mountedTransportLockOwner = null;
            if (transportBehaviour == null)
                return;

            if (!transportBehaviour.TryGetComponent(out _mountedTransportLockOwner))
                _mountedTransportLockOwner = transportBehaviour.GetComponentInParent<ITransportDockControlLock>();

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

            Transform root = _dockedBehaviour.transform;
            if (root == null)
                return;

            int stackCount = 1;
            _cargoTraversalStack[0] = root;

            try
            {
                while (stackCount > 0 && _connectedCargoCrateCount < DockedCargoCrateCapacity)
                {
                    stackCount--;
                    Transform current = _cargoTraversalStack[stackCount];
                    _cargoTraversalStack[stackCount] = null;
                    if (current == null)
                        continue;

                    if (current.TryGetComponent(out StorageCrate crate) &&
                        crate != null &&
                        !IsDockedCargoConnected(crate) &&
                        !CrateHasExternalPowerGrid(crate))
                    {
                        BaseLogisticsNetwork.RegisterStorage(crate, _powerNode);
                        _connectedCargoCrates[_connectedCargoCrateCount] = crate;
                        _connectedCargoCrateCount++;
                    }

                    int childCount = current.childCount;
                    for (int childIndex = childCount - 1; childIndex >= 0; childIndex--)
                    {
                        if (stackCount >= DockedCargoTraversalCapacity)
                            return;

                        _cargoTraversalStack[stackCount] = current.GetChild(childIndex);
                        stackCount++;
                    }
                }
            }
            finally
            {
                ClearCargoTraversalStack(stackCount);
            }
        }

        private bool IsDockedCargoConnected(StorageCrate crate)
        {
            for (int i = 0; i < _connectedCargoCrateCount; i++)
            {
                if (ReferenceEquals(_connectedCargoCrates[i], crate))
                    return true;
            }

            return false;
        }

        private static bool CrateHasExternalPowerGrid(StorageCrate crate)
        {
            if (crate == null)
                return true;

            Transform cursor = crate.transform;
            while (cursor != null)
            {
                if (cursor.TryGetComponent(out PowerNode node) && node != null && node.Grid != null)
                    return true;

                cursor = cursor.parent;
            }

            return false;
        }

        private void ClearCargoTraversalStack(int stackCount)
        {
            int safeCount = math.min(stackCount, _cargoTraversalStack.Length);
            for (int i = 0; i < safeCount; i++)
                _cargoTraversalStack[i] = null;
        }

        private void DisconnectDockedCargoCrates()
        {
            for (int i = 0; i < _connectedCargoCrateCount; i++)
            {
                StorageCrate crate = _connectedCargoCrates[i];
                if (crate != null)
                    BaseLogisticsNetwork.UnregisterStorage(crate);

                _connectedCargoCrates[i] = null;
            }

            _connectedCargoCrateCount = 0;
        }

    }
}
