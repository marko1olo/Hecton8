using System;
using System.Collections.Generic;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Physics
{
    /// <summary>
    /// Connection classes monitored by <see cref="GlobalPhysicsStateManager"/> for mass-ratio instability.
    /// </summary>
    public enum PhysicsConnectionKind : byte
    {
        None = 0,
        Tether = 1,
        Dock = 2
    }

    /// <summary>
    /// Coarse impact weight bucket used by downstream audio/VFX listeners.
    /// </summary>
    public enum PhysicsImpactWeightClass : byte
    {
        Light = 0,
        Medium = 1,
        Heavy = 2
    }

    [Flags]
    internal enum PhysicsStateMask : byte
    {
        None = 0,
        WasAsleep = 1 << 4,
        NanDetected = 1 << 6
    }

    /// <summary>
    /// Optional rigidbody-side metadata provider for procedural impact material synthesis.
    /// </summary>
    public interface IPhysicsImpactMaterialProvider
    {
        /// <summary>
        /// Compact authored impact-audio material family.
        /// </summary>
        byte ImpactAudioMaterialId { get; }
    }

    /// <summary>
    /// Runtime-owned collider LOD participant controlled by the global physics hysteresis gate.
    /// </summary>
    public interface IPhysicsColliderLodHysteresisSink
    {
        /// <summary>
        /// Enables or disables simplified collider LOD based on distance hysteresis.
        /// </summary>
        /// <param name="allowSimplifiedColliderLod">True after the body stays outside the LOD0 radius long enough.</param>
        void SetColliderLodDistanceGate(bool allowSimplifiedColliderLod);
    }

    /// <summary>
    /// Immutable gameplay impact payload flushed in LateUpdate after the fixed-step collision phase.
    /// </summary>
    public readonly struct PhysicsImpactSignal
    {
        /// <summary>
        /// Creates a queued gameplay physics-impact payload.
        /// </summary>
        public PhysicsImpactSignal(
            ulong primaryBodyId,
            ulong secondaryBodyId,
            Vector3 point,
            Vector3 normal,
            float force,
            float intensity,
            float massVelocity,
            PhysicsImpactWeightClass weightClass,
            byte primaryAudioMaterialId,
            byte secondaryAudioMaterialId)
        {
            PrimaryBodyId = primaryBodyId;
            SecondaryBodyId = secondaryBodyId;
            Point = point;
            Normal = normal;
            Force = force;
            Intensity = intensity;
            MassVelocity = massVelocity;
            WeightClass = weightClass;
            PrimaryAudioMaterialId = primaryAudioMaterialId;
            SecondaryAudioMaterialId = secondaryAudioMaterialId;
        }

        /// <summary>Primary tracked rigidbody instance ID.</summary>
        public ulong PrimaryBodyId { get; }

        /// <summary>Secondary tracked rigidbody instance ID, or zero for static geometry.</summary>
        public ulong SecondaryBodyId { get; }

        /// <summary>Resolved world-space impact point.</summary>
        public Vector3 Point { get; }

        /// <summary>Resolved world-space impact normal.</summary>
        public Vector3 Normal { get; }

        /// <summary>Average impact force derived from collision impulse.</summary>
        public float Force { get; }

        /// <summary>Perceived impact intensity computed from the force-domain logarithmic mapping.</summary>
        public float Intensity { get; }

        /// <summary>Strict item impact loudness scalar: impact velocity magnitude multiplied by primary body mass.</summary>
        public float MassVelocity { get; }

        /// <summary>Discrete impact-weight bucket for downstream presentation systems.</summary>
        public PhysicsImpactWeightClass WeightClass { get; }

        /// <summary>Primary collision body's compact authored impact material family.</summary>
        public byte PrimaryAudioMaterialId { get; }

        /// <summary>Secondary collision body's compact authored impact material family.</summary>
        public byte SecondaryAudioMaterialId { get; }

        /// <summary>True when the event falls into the heavy feedback bucket.</summary>
        public bool IsHeavy => WeightClass == PhysicsImpactWeightClass.Heavy;
    }

    /// <summary>
    /// Listener contract for deferred physics-impact feedback.
    /// </summary>
    public interface IPhysicsImpactEventListener
    {
        /// <summary>Called once for each queued impact after the fixed-step collision phase.</summary>
        /// <param name="impactSignal">Impact payload.</param>
        void OnPhysicsImpact(in PhysicsImpactSignal impactSignal);
    }

    /// <summary>
    /// Static zero-instance gameplay event bus for deferred physics-impact feedback.
    /// </summary>
    public static class PhysicsEvents
    {
        private const int ListenerCapacity = 16;

        // COLD ALLOC: RegistryBucket<IPhysicsImpactEventListener>[16] - deferred physics impact listeners - owner: PhysicsEvents
        private static readonly RegistryBucket<IPhysicsImpactEventListener> _impactListeners = new RegistryBucket<IPhysicsImpactEventListener>(ListenerCapacity);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _impactListeners.Clear();
        }

        /// <summary>
        /// Registers one deferred physics-impact listener.
        /// </summary>
        /// <param name="listener">Listener to register.</param>
        public static void Register(IPhysicsImpactEventListener listener)
        {
            if (listener != null && !_impactListeners.Contains(listener))
                _impactListeners.Register(listener);
        }

        /// <summary>
        /// Unregisters one deferred physics-impact listener.
        /// </summary>
        /// <param name="listener">Listener to unregister.</param>
        public static void Unregister(IPhysicsImpactEventListener listener)
        {
            if (listener != null && _impactListeners.Contains(listener))
                _impactListeners.Unregister(listener);
        }

        internal static void RaiseImpact(in PhysicsImpactSignal impactSignal)
        {
            int count = _impactListeners.Count;
            if (count <= 0)
                return;

            IPhysicsImpactEventListener[] rawArray = _impactListeners.RawArray;
            for (int i = count - 1; i >= 0; i--)
                rawArray[i].OnPhysicsImpact(in impactSignal);
        }
    }

    /// <summary>
    /// Authoritative runtime registry for active rigidbodies, mass-ratio guards, and queued impact feedback.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8995)]
    public sealed class GlobalPhysicsStateManager : MonoBehaviour, IFixedTickable, ILateFrameTickable, IOriginShiftListener
    {
        private struct RigidbodyState
        {
            public ulong EntityId;
            public PhysicsStateMask StateMask;
            public int CompensationRefCount;
            public float MaxAngularVelocityClamp;
            public bool AllowDistanceKinematicSleep;
            public bool DistanceKinematicSleepActive;
            public bool HasLastValidPosition;
            public bool HasLastValidAup;
            public bool HasOriginShiftSnapshot;
            public bool WasSleepingBeforeOriginShift;
            public bool WasSleepingBeforeDistanceSleep;
            public bool InterpolationSuspendedForOriginShift;
            public bool CollisionDetectionOverriddenForOriginShift;
            public bool KinematicModeBeforeDistanceSleep;
            public bool DetectCollisionsBeforeDistanceSleep;
            public RigidbodyInterpolation InterpolationModeBeforeOriginShift;
            public CollisionDetectionMode CollisionDetectionModeBeforeOriginShift;
            public Vector3 SnapshotPositionBeforeOriginShift;
            public Quaternion SnapshotRotationBeforeOriginShift;
            public Vector3 LastValidLinearVelocity;
            public Vector3 LastValidAngularVelocity;
            public AbsoluteUniversePosition LastValidAup;
            public IPhysicsColliderLodHysteresisSink ColliderLodSink;
            public Vector3 BaseInertiaTensor;
            public Quaternion BaseInertiaTensorRotation;
            public float BaseAngularDamping;
            public float HydrodynamicSubmersionFactor;
            public float FixedInterpolationAlphaBeforeOriginShift;
            public float ColliderLodOutOfRangeSeconds;
            public bool HasColliderLodSink;
            public bool ColliderLodDistanceGateOpen;
            public bool HasAddedMassBaseline;
            public bool AddedMassTensorApplied;
        }

        private struct PhysicsConnection
        {
            public UnityEngine.Object Owner;
            public Rigidbody BodyA;
            public Rigidbody BodyB;
            public Rigidbody CompensatedBody;
            public PhysicsConnectionKind Kind;
            public bool CompensationActive;
        }

        private struct PhysicsImpactEventData
        {
            public ulong PrimaryBodyId;
            public ulong SecondaryBodyId;
            public float Force;
            public float Intensity;
            public float MassVelocity;
            public float3 Point;
            public float3 Normal;
            public PhysicsImpactWeightClass WeightClass;
            public byte PrimaryAudioMaterialId;
            public byte SecondaryAudioMaterialId;
        }

        private const int MaxTrackedBodies = 512;
        private const int MaxTrackedConnections = 128;
        private const int MaxQueuedImpactEvents = 256;
        private const int MaxImpactFlushIterations = MaxQueuedImpactEvents;
        private const int SceneRootScanCapacity = 128;
        private const int SceneRigidbodyScanCapacity = MaxTrackedBodies;
        private const float MinMass = 0.0001f;
        private const float MassRatioThreshold = 100f;
        private const float MinImpactForce = 0.01f;
        private const float HeavyImpactIntensity = 0.95f;
        private const float MediumImpactIntensity = 0.45f;
        private const float FarKinematicSleepDistanceMeters = 500f;
        private const float ColliderLodCompoundToSimpleDistanceMeters = 80f;
        private const float ColliderLodSimpleToCompoundDistanceMeters = 72f;
        private const float ColliderLodSimplifyHysteresisSeconds = 5f;
        private const float AddedMassAngularDampingScale = 0.35f;
        private const float AddedMassInertiaTensorScale = 0.35f;
        private const float OriginShiftContinuousCcdSpeedMetersPerSecond = 20f;
        private const float KineticAnomalyAccelerationMetersPerSecondSq = 100f;
        private const double FarKinematicSleepDistanceSq = FarKinematicSleepDistanceMeters * FarKinematicSleepDistanceMeters;
        private const double ColliderLodCompoundToSimpleDistanceSq = ColliderLodCompoundToSimpleDistanceMeters * ColliderLodCompoundToSimpleDistanceMeters;
        private const double ColliderLodSimpleToCompoundDistanceSq = ColliderLodSimpleToCompoundDistanceMeters * ColliderLodSimpleToCompoundDistanceMeters;

        // COLD ALLOC: Rigidbody[512 initial] â€” authoritative tracked rigidbody registry â€” owner: GlobalPhysicsStateManager
        private Rigidbody[] _trackedBodies = new Rigidbody[MaxTrackedBodies];
        // COLD ALLOC: RigidbodyState[512 initial] â€” per-body runtime state and compensation flags â€” owner: GlobalPhysicsStateManager
        private RigidbodyState[] _bodyStates = new RigidbodyState[MaxTrackedBodies];
        // COLD ALLOC: PhysicsConnection[128] â€” tracked tether/dock connection registry â€” owner: GlobalPhysicsStateManager
        private readonly PhysicsConnection[] _connections = new PhysicsConnection[MaxTrackedConnections];
        // COLD ALLOC: Dictionary<ulong,int>[512 initial] â€” rigidbody entity-id to tracked-index map for O(1) lookups during origin shifts â€” owner: GlobalPhysicsStateManager
        private readonly Dictionary<ulong, int> _trackedBodyIndexByEntityId = new Dictionary<ulong, int>(MaxTrackedBodies);
        // COLD ALLOC: List<GameObject>[128] - scene-load root scratch for rigidbody registry bootstrap without scene-wide array allocation - owner: GlobalPhysicsStateManager
        private readonly List<GameObject> _sceneRootScratch = new List<GameObject>(SceneRootScanCapacity);
        // COLD ALLOC: List<Rigidbody>[512] - scene-load rigidbody scratch for registry bootstrap without scene-wide array allocation - owner: GlobalPhysicsStateManager
        private readonly List<Rigidbody> _sceneRigidbodyScratch = new List<Rigidbody>(SceneRigidbodyScanCapacity);

        private NativeArray<float3> _lastValidPositions;
        private NativeQueue<PhysicsImpactEventData> _impactQueue;
        private int _trackedBodyCount;
        private int _connectionCount;
        private int _queuedImpactCount;
        private bool _serviceRegistered;
        private bool _isInitialized;
        private bool _registeredFixedTick;
        private bool _registeredLateFrameTick;
        private bool _registeredOriginShift;
        private bool _sceneEventsSubscribed;
        private int _lastKineticAnomalyFrame = -1;
        private Transform _playerTransform;

        internal static GlobalPhysicsStateManager ActiveRuntimeInstance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        internal static void RegisterTrackedBody(Rigidbody body)
        {
            if (!TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return;

            manager.RegisterTrackedBodyInternal(body);
        }

        internal static void PrepareTrackedBodiesForOriginShift()
        {
            if (!TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return;

            manager.PrepareTrackedBodiesForOriginShiftInternal();
        }

        internal static void CommitTrackedBodiesForOriginShift(Vector3 shiftOffset)
        {
            if (!TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return;

            manager.CommitTrackedBodiesForOriginShiftInternal(shiftOffset);
        }

        internal static void FinalizeTrackedBodiesAfterOriginShift()
        {
            if (!TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return;

            manager.FinalizeTrackedBodiesAfterOriginShiftInternal();
        }

        internal static void ResetTrackedBodiesForSafeTeleport()
        {
            if (!TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return;

            manager.ResetTrackedBodiesForSafeTeleportInternal();
        }

        internal static void UnregisterTrackedBody(Rigidbody body)
        {
            if (body == null || !TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return;

            manager.UnregisterTrackedBodyInternal(body);
        }

        internal static void SetHydrodynamicSubmersion(Rigidbody body, float submersionFactor)
        {
            if (body == null || !TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return;

            manager.SetHydrodynamicSubmersionInternal(body, submersionFactor);
        }

        internal static void QueueImpact(Rigidbody primaryBody, Rigidbody secondaryBody, Collision collision)
        {
            if (primaryBody == null || collision == null || !TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return;

            manager.QueueImpactInternal(primaryBody, secondaryBody, collision);
        }

        internal static void QueueKinematicImpact(
            Rigidbody primaryBody,
            Vector3 point,
            Vector3 normal,
            float impactSpeedMetersPerSecond,
            Rigidbody secondaryBody = null)
        {
            if (primaryBody == null || !TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return;

            manager.QueueKinematicImpactInternal(primaryBody, secondaryBody, point, normal, impactSpeedMetersPerSecond);
        }

        internal static void RegisterTetherConnection(UnityEngine.Object owner, Rigidbody anchorBody, Rigidbody payloadBody)
        {
            if (!TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return;

            manager.RegisterOrUpdateConnection(owner, anchorBody, payloadBody, PhysicsConnectionKind.Tether);
        }

        internal static void UnregisterTetherConnection(UnityEngine.Object owner)
        {
            if (owner == null || !TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return;

            manager.UnregisterConnection(owner, PhysicsConnectionKind.Tether);
        }

        internal static void RegisterDockConnection(UnityEngine.Object owner, Rigidbody dockedBody)
        {
            if (!TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return;

            manager.RegisterOrUpdateConnection(owner, dockedBody, null, PhysicsConnectionKind.Dock);
        }

        internal static void UnregisterDockConnection(UnityEngine.Object owner)
        {
            if (owner == null || !TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return;

            manager.UnregisterConnection(owner, PhysicsConnectionKind.Dock);
        }

        internal static bool IsKinematicAnchorCompensationEnabled(UnityEngine.Object owner, PhysicsConnectionKind kind)
        {
            if (owner == null || !TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return false;

            int connectionIndex = manager.FindConnectionIndex(owner, kind);
            if (connectionIndex < 0)
                return false;

            return manager._connections[connectionIndex].CompensationActive;
        }

        /// <summary>
        /// Clears tracked bodies, connections, and queued impacts during a guarded scene transition.
        /// </summary>
        public static void ClearRuntimeStateStatic()
        {
            if (TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                manager.ClearRuntimeState();
        }

        private void Awake()
        {
            if (ActiveRuntimeInstance == null)
                ActiveRuntimeInstance = this;

            if (!_lastValidPositions.IsCreated)
            {
                // COLD ALLOC: NativeArray<float3>[512 initial] â€” authoritative last-valid runtime-space body positions for origin-shift-safe recovery â€” owner: GlobalPhysicsStateManager
                _lastValidPositions = new NativeArray<float3>(_trackedBodies.Length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    _lastValidPositions,
                    nameof(GlobalPhysicsStateManager),
                    nameof(_lastValidPositions),
                    NativeAllocationLifetime.Session);
            }

            if (!_impactQueue.IsCreated)
            {
                // COLD ALLOC: NativeQueue<PhysicsImpactEventData>(Persistent) â€” deferred gameplay physics impact bus â€” owner: GlobalPhysicsStateManager
                _impactQueue = new NativeQueue<PhysicsImpactEventData>(Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeQueue(
                    _impactQueue,
                    MaxQueuedImpactEvents,
                    nameof(GlobalPhysicsStateManager),
                    nameof(_impactQueue),
                    NativeAllocationLifetime.Session);
            }
        }

        /// <summary>
        /// Registers this manager as the authoritative global physics-state owner.
        /// </summary>
        public void InitializeService()
        {
            if (_isInitialized)
            {
                TryRegisterService();
                TryRegisterFixedTick();
                TryRegisterLateFrameTick();
                TryRegisterOriginShift();
                return;
            }

            GlobalPhysicsStateManager registeredManager = GlobalRegistry.PhysicsStateManager;
            if (registeredManager != null && !ReferenceEquals(registeredManager, this))
            {
                Destroy(gameObject);
                return;
            }

            TryRegisterService();

            SubscribeSceneEvents();
            ScanLoadedScenesForRigidbodies();
            _isInitialized = true;
            TryRegisterFixedTick();
            TryRegisterOriginShift();
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;

            if (!_isInitialized)
                return;

            TryRegisterFixedTick();
            TryRegisterLateFrameTick();
            TryRegisterOriginShift();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            FlushImpactEvents();
        }

        private void OnDisable()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            if (_registeredFixedTick)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Core);
                _registeredFixedTick = false;
            }

            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _registeredLateFrameTick = false;
            }

            if (_registeredOriginShift)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShift = false;
            }
        }

        private void OnDestroy()
        {
            OnDisable();
            UnsubscribeSceneEvents();
            ClearRuntimeState();

            if (_impactQueue.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(GlobalPhysicsStateManager), nameof(_impactQueue));
                _impactQueue.Dispose();
            }

            if (_lastValidPositions.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_lastValidPositions);
                _lastValidPositions.Dispose();
            }

            TryUnregisterService();
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            RefreshTrackedBodies(fixedDeltaTime);
            SweepNaNPhysicsState();
            EvaluateConnections();
            ApplyDistanceKinematicSleepInternal();
            ApplyColliderLodHysteresisInternal(fixedDeltaTime);
            ApplyAddedMassTensorState();
        }

        private static bool TryGetRuntimeManager(out GlobalPhysicsStateManager manager)
        {
            manager = GlobalRegistry.PhysicsStateManager;
            return manager != null;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered)
                return;

            GlobalRegistry.RegisterPhysicsStateManager(this);
            _serviceRegistered = true;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.PhysicsStateManager, this))
                GlobalRegistry.UnregisterPhysicsStateManager(this);

            _serviceRegistered = false;
        }

        private void TryRegisterFixedTick()
        {
            if (_registeredFixedTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Core);
            _registeredFixedTick = GlobalRegistry.FixedTickables.Contains(this);
        }

        private void TryRegisterLateFrameTick()
        {
            if (_registeredLateFrameTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Core);
            _registeredLateFrameTick = SystemDispatcher
                .GetLateFrameLane(PriorityLayer.Core)
                .Contains(this);
        }

        private void TryRegisterOriginShift()
        {
            if (_registeredOriginShift)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShift = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            // HectonFloatingOrigin now performs the rigidbody teleport before PhysX sync.
            // This callback remains registered so the runtime manager stays aligned with the
            // origin-shift listener contract, but the tracked-body translation is no longer
            // deferred until after the transform shift has already dirtied physics state.
        }

        private void PrepareTrackedBodiesForOriginShiftInternal()
        {
            for (int i = _trackedBodyCount - 1; i >= 0; i--)
            {
                Rigidbody body = _trackedBodies[i];
                if (body == null)
                {
                    RemoveTrackedBodyAt(i);
                    continue;
                }

                if (body.isKinematic)
                    continue;

                RigidbodyState bodyState = _bodyStates[i];
                Vector3 position = body.position;
                Quaternion rotation = body.rotation;
                if (IsFinite(position))
                {
                    bodyState.HasLastValidPosition = true;
                    _lastValidPositions[i] = new float3(position.x, position.y, position.z);
                    bodyState.LastValidAup = AbsoluteUniversePosition.FromRuntimePosition(position);
                    bodyState.HasLastValidAup = true;
                }
                else
                {
                    position = bodyState.HasLastValidPosition
                        ? new Vector3(_lastValidPositions[i].x, _lastValidPositions[i].y, _lastValidPositions[i].z)
                        : Vector3.zero;
                }

                bodyState.HasOriginShiftSnapshot = true;
                bodyState.SnapshotPositionBeforeOriginShift = position;
                bodyState.SnapshotRotationBeforeOriginShift = IsFinite(rotation) ? rotation : Quaternion.identity;

                bodyState.LastValidLinearVelocity = IsFinite(body.linearVelocity) ? body.linearVelocity : Vector3.zero;
                bodyState.LastValidAngularVelocity = IsFinite(body.angularVelocity) ? body.angularVelocity : Vector3.zero;
                bodyState.FixedInterpolationAlphaBeforeOriginShift = HectonFloatingOrigin.CurrentFixedInterpolationAlpha;
                bodyState.WasSleepingBeforeOriginShift = body.IsSleeping();
                bodyState.InterpolationModeBeforeOriginShift = body.interpolation;
                bodyState.InterpolationSuspendedForOriginShift = body.interpolation != RigidbodyInterpolation.None;
                if (bodyState.InterpolationSuspendedForOriginShift)
                    body.interpolation = RigidbodyInterpolation.None;
                bodyState.CollisionDetectionModeBeforeOriginShift = body.collisionDetectionMode;
                float speedSq = bodyState.LastValidLinearVelocity.sqrMagnitude;
                bodyState.CollisionDetectionOverriddenForOriginShift =
                    speedSq > OriginShiftContinuousCcdSpeedMetersPerSecond * OriginShiftContinuousCcdSpeedMetersPerSecond &&
                    body.collisionDetectionMode != CollisionDetectionMode.Continuous &&
                    body.collisionDetectionMode != CollisionDetectionMode.ContinuousDynamic;
                if (bodyState.CollisionDetectionOverriddenForOriginShift)
                    body.collisionDetectionMode = CollisionDetectionMode.Continuous;
                body.PublishTransform();
                _bodyStates[i] = bodyState;
            }
        }

        private void CommitTrackedBodiesForOriginShiftInternal(Vector3 shiftOffset)
        {
            if (!_lastValidPositions.IsCreated || _trackedBodyCount <= 0 || shiftOffset.sqrMagnitude <= 0.000001f)
                return;

            for (int i = _trackedBodyCount - 1; i >= 0; i--)
            {
                Rigidbody body = _trackedBodies[i];
                if (body == null)
                {
                    RemoveTrackedBodyAt(i);
                    continue;
                }

                if (body.isKinematic)
                    continue;

                RigidbodyState bodyState = _bodyStates[i];
                Vector3 snapshotPosition = bodyState.HasOriginShiftSnapshot
                    ? bodyState.SnapshotPositionBeforeOriginShift
                    : body.position;
                Vector3 targetPosition = snapshotPosition - shiftOffset;

                if (!IsFinite(targetPosition))
                    targetPosition = Vector3.zero;

                Quaternion targetRotation = bodyState.HasOriginShiftSnapshot && IsFinite(bodyState.SnapshotRotationBeforeOriginShift)
                    ? bodyState.SnapshotRotationBeforeOriginShift
                    : Quaternion.identity;

                Vector3 linearVelocity = IsFinite(bodyState.LastValidLinearVelocity)
                    ? bodyState.LastValidLinearVelocity
                    : Vector3.zero;
                Vector3 angularVelocity = IsFinite(bodyState.LastValidAngularVelocity)
                    ? bodyState.LastValidAngularVelocity
                    : Vector3.zero;

                body.position = targetPosition;
                body.rotation = targetRotation;
                body.MovePosition(targetPosition);
                body.linearVelocity = linearVelocity;
                body.angularVelocity = angularVelocity;

                if (bodyState.WasSleepingBeforeOriginShift)
                    body.Sleep();
                else
                    body.WakeUp();

                body.PublishTransform();

                _lastValidPositions[i] = new float3(targetPosition.x, targetPosition.y, targetPosition.z);
                bodyState.HasOriginShiftSnapshot = false;
                bodyState.LastValidLinearVelocity = linearVelocity;
                bodyState.LastValidAngularVelocity = angularVelocity;
                bodyState.FixedInterpolationAlphaBeforeOriginShift = 0f;
                bodyState.WasSleepingBeforeOriginShift = false;
                _bodyStates[i] = bodyState;
            }
        }

        private void FinalizeTrackedBodiesAfterOriginShiftInternal()
        {
            for (int i = _trackedBodyCount - 1; i >= 0; i--)
            {
                Rigidbody body = _trackedBodies[i];
                if (body == null)
                {
                    RemoveTrackedBodyAt(i);
                    continue;
                }

                RigidbodyState bodyState = _bodyStates[i];
                if (!bodyState.InterpolationSuspendedForOriginShift)
                {
                    if (bodyState.CollisionDetectionOverriddenForOriginShift)
                    {
                        body.collisionDetectionMode = bodyState.CollisionDetectionModeBeforeOriginShift;
                        bodyState.CollisionDetectionOverriddenForOriginShift = false;
                        _bodyStates[i] = bodyState;
                    }

                    continue;
                }

                body.interpolation = bodyState.InterpolationModeBeforeOriginShift;
                bodyState.InterpolationSuspendedForOriginShift = false;
                if (bodyState.CollisionDetectionOverriddenForOriginShift)
                {
                    body.collisionDetectionMode = bodyState.CollisionDetectionModeBeforeOriginShift;
                    bodyState.CollisionDetectionOverriddenForOriginShift = false;
                }

                _bodyStates[i] = bodyState;
            }
        }

        private void ResetTrackedBodiesForSafeTeleportInternal()
        {
            for (int i = _trackedBodyCount - 1; i >= 0; i--)
            {
                Rigidbody body = _trackedBodies[i];
                if (body == null)
                {
                    RemoveTrackedBodyAt(i);
                    continue;
                }

                RigidbodyState bodyState = _bodyStates[i];
                bool wasKinematic = body.isKinematic;
                bool wasDetectingCollisions = body.detectCollisions;
                bool wasSleeping = body.IsSleeping();
                Vector3 linearVelocity = IsFinite(body.linearVelocity) ? body.linearVelocity : Vector3.zero;
                Vector3 angularVelocity = IsFinite(body.angularVelocity) ? body.angularVelocity : Vector3.zero;
                Vector3 position = IsFinite(body.position) ? body.position : Vector3.zero;

                body.ResetCenterOfMass();
                body.detectCollisions = false;
                body.isKinematic = true;
                body.PublishTransform();
                body.isKinematic = false;
                body.isKinematic = wasKinematic;
                body.detectCollisions = wasDetectingCollisions;

                if (!wasKinematic)
                {
                    body.linearVelocity = linearVelocity;
                    body.angularVelocity = angularVelocity;
                    if (wasSleeping)
                        body.Sleep();
                    else
                        body.WakeUp();
                }
                else if (wasSleeping)
                {
                    body.Sleep();
                }

                if (IsFinite(position))
                {
                    _lastValidPositions[i] = new float3(position.x, position.y, position.z);
                    bodyState.LastValidAup = AbsoluteUniversePosition.FromRuntimePosition(position);
                    bodyState.HasLastValidPosition = true;
                    bodyState.HasLastValidAup = true;
                }

                bodyState.LastValidLinearVelocity = linearVelocity;
                bodyState.LastValidAngularVelocity = angularVelocity;
                if (wasSleeping)
                    bodyState.StateMask |= PhysicsStateMask.WasAsleep;
                else
                    bodyState.StateMask &= ~PhysicsStateMask.WasAsleep;

                _bodyStates[i] = bodyState;
            }
        }

        private void RegisterTrackedBodyInternal(Rigidbody body)
        {
            if (body == null)
                return;

            ulong bodyEntityId = EntityId.ToULong(body.GetEntityId());
            if (_trackedBodyIndexByEntityId.ContainsKey(bodyEntityId))
                return;

            EnsureTrackedBodyCapacity(_trackedBodyCount + 1);

            EnsureReporter(body);
            IPhysicsColliderLodHysteresisSink colliderLodSink = ResolveColliderLodSink(body);

            int bodyIndex = _trackedBodyCount++;
            _trackedBodies[bodyIndex] = body;
            _bodyStates[bodyIndex] = new RigidbodyState
            {
                EntityId = bodyEntityId,
                StateMask = PhysicsStateMask.None,
                CompensationRefCount = 0,
                MaxAngularVelocityClamp = ResolveMaxAngularVelocityClamp(body),
                AllowDistanceKinematicSleep = ShouldAllowDistanceKinematicSleep(body),
                DistanceKinematicSleepActive = false,
                HasLastValidPosition = IsFinite(body.position),
                HasLastValidAup = IsFinite(body.position),
                LastValidAup = IsFinite(body.position) ? AbsoluteUniversePosition.FromRuntimePosition(body.position) : default,
                KinematicModeBeforeDistanceSleep = body.isKinematic,
                DetectCollisionsBeforeDistanceSleep = body.detectCollisions,
                LastValidLinearVelocity = IsFinite(body.linearVelocity) ? body.linearVelocity : Vector3.zero,
                LastValidAngularVelocity = IsFinite(body.angularVelocity) ? body.angularVelocity : Vector3.zero,
                ColliderLodSink = colliderLodSink,
                HasColliderLodSink = IsColliderLodSinkAlive(colliderLodSink),
                ColliderLodDistanceGateOpen = false,
                ColliderLodOutOfRangeSeconds = 0f,
                BaseInertiaTensor = IsFinite(body.inertiaTensor) ? body.inertiaTensor : Vector3.one,
                BaseInertiaTensorRotation = IsFinite(body.inertiaTensorRotation) ? body.inertiaTensorRotation : Quaternion.identity,
                BaseAngularDamping = math.max(0f, body.angularDamping),
                HydrodynamicSubmersionFactor = 0f,
                HasAddedMassBaseline = true
            };
            ApplyTrackedBodyAngularVelocityClamp(body, _bodyStates[bodyIndex].MaxAngularVelocityClamp);
            _trackedBodyIndexByEntityId[bodyEntityId] = bodyIndex;
            _lastValidPositions[bodyIndex] = new float3(body.position.x, body.position.y, body.position.z);
        }

        private void UnregisterTrackedBodyInternal(Rigidbody body)
        {
            int bodyIndex = FindTrackedBodyIndex(body);
            if (bodyIndex < 0)
                return;

            RemoveTrackedBodyAt(bodyIndex);
        }

        private void SetHydrodynamicSubmersionInternal(Rigidbody body, float submersionFactor)
        {
            if (body == null)
                return;

            RegisterTrackedBodyInternal(body);
            int bodyIndex = FindTrackedBodyIndex(body);
            if (bodyIndex < 0)
                return;

            RigidbodyState bodyState = _bodyStates[bodyIndex];
            CaptureAddedMassBaseline(body, ref bodyState);
            bodyState.HydrodynamicSubmersionFactor = math.saturate(submersionFactor);
            _bodyStates[bodyIndex] = bodyState;
        }

        private void QueueImpactInternal(Rigidbody primaryBody, Rigidbody secondaryBody, Collision collision)
        {
            if (!_impactQueue.IsCreated || _queuedImpactCount >= MaxQueuedImpactEvents)
                return;

            float fixedDelta = math.max(Time.fixedDeltaTime, 0.0001f);
            float impactForce = collision.impulse.magnitude / fixedDelta;
            if (!(impactForce > MinImpactForce))
                return;

            float massVelocity = ResolveImpactMassVelocity(primaryBody, collision.relativeVelocity.magnitude);
            float impactIntensity = ResolveImpactIntensityFromForce(impactForce);
            if (!(impactIntensity > 0f))
                return;

            ContactPoint contact = collision.contactCount > 0 ? collision.GetContact(0) : default;
            Vector3 point = collision.contactCount > 0 ? contact.point : primaryBody.worldCenterOfMass;
            Vector3 normal = collision.contactCount > 0 && contact.normal.sqrMagnitude > 0.000001f ? contact.normal.normalized : Vector3.up;
            PhysicsImpactWeightClass weightClass = ResolveImpactWeightClass(impactIntensity);

            _impactQueue.Enqueue(new PhysicsImpactEventData
            {
                PrimaryBodyId = EntityId.ToULong(primaryBody.GetEntityId()),
                SecondaryBodyId = secondaryBody != null ? EntityId.ToULong(secondaryBody.GetEntityId()) : 0ul,
                Force = impactForce,
                Intensity = impactIntensity,
                MassVelocity = massVelocity,
                Point = new float3(point.x, point.y, point.z),
                Normal = new float3(normal.x, normal.y, normal.z),
                WeightClass = weightClass,
                PrimaryAudioMaterialId = ResolveImpactAudioMaterialId(primaryBody),
                SecondaryAudioMaterialId = ResolveImpactAudioMaterialId(secondaryBody)
            });
            _queuedImpactCount++;
        }

        private void QueueKinematicImpactInternal(
            Rigidbody primaryBody,
            Rigidbody secondaryBody,
            Vector3 point,
            Vector3 normal,
            float impactSpeedMetersPerSecond)
        {
            if (!_impactQueue.IsCreated || _queuedImpactCount >= MaxQueuedImpactEvents)
                return;

            float safeImpactSpeed = math.max(0f, impactSpeedMetersPerSecond);
            if (!(safeImpactSpeed > 0.0001f))
                return;

            float fixedDelta = math.max(Time.fixedDeltaTime, 0.0001f);
            float effectiveMass = math.max(primaryBody != null ? primaryBody.mass : 1f, MinMass);
            float impactForce = (effectiveMass * safeImpactSpeed) / fixedDelta;
            if (!(impactForce > MinImpactForce))
                return;

            float massVelocity = ResolveImpactMassVelocity(primaryBody, safeImpactSpeed);
            float3 point3 = new float3(point.x, point.y, point.z);
            float3 normal3 = new float3(normal.x, normal.y, normal.z);
            if (!math.all(math.isfinite(point3)))
                point3 = primaryBody != null ? (float3)primaryBody.worldCenterOfMass : float3.zero;
            if (!math.all(math.isfinite(normal3)) || math.lengthsq(normal3) <= 0.000001f)
                normal3 = new float3(0f, 1f, 0f);
            else
                normal3 = math.normalize(normal3);

            float impactIntensity = ResolveImpactIntensityFromForce(impactForce);
            if (!(impactIntensity > 0f))
                return;

            _impactQueue.Enqueue(new PhysicsImpactEventData
            {
                PrimaryBodyId = EntityId.ToULong(primaryBody.GetEntityId()),
                SecondaryBodyId = secondaryBody != null ? EntityId.ToULong(secondaryBody.GetEntityId()) : 0ul,
                Force = impactForce,
                Intensity = impactIntensity,
                MassVelocity = massVelocity,
                Point = point3,
                Normal = normal3,
                WeightClass = ResolveImpactWeightClass(impactIntensity),
                PrimaryAudioMaterialId = ResolveImpactAudioMaterialId(primaryBody),
                SecondaryAudioMaterialId = ResolveImpactAudioMaterialId(secondaryBody)
            });
            _queuedImpactCount++;
        }

        private void FlushImpactEvents()
        {
            if (!_impactQueue.IsCreated || _queuedImpactCount <= 0)
                return;

            int processedCount = 0;
            while (_queuedImpactCount > 0 &&
                   processedCount < MaxImpactFlushIterations &&
                   _impactQueue.TryDequeue(out PhysicsImpactEventData impactEvent))
            {
                _queuedImpactCount--;
                processedCount++;
                PhysicsEvents.RaiseImpact(new PhysicsImpactSignal(
                    impactEvent.PrimaryBodyId,
                    impactEvent.SecondaryBodyId,
                    new Vector3(impactEvent.Point.x, impactEvent.Point.y, impactEvent.Point.z),
                    new Vector3(impactEvent.Normal.x, impactEvent.Normal.y, impactEvent.Normal.z),
                    impactEvent.Force,
                    impactEvent.Intensity,
                    impactEvent.MassVelocity,
                    impactEvent.WeightClass,
                    impactEvent.PrimaryAudioMaterialId,
                    impactEvent.SecondaryAudioMaterialId));
            }
        }

        private static float ResolveImpactMassVelocity(Rigidbody primaryBody, float impactVelocityMagnitude)
        {
            float massKg = primaryBody != null ? primaryBody.mass : 1f;
            return math.max(0f, impactVelocityMagnitude) * math.max(massKg, MinMass);
        }

        private static byte ResolveImpactAudioMaterialId(Rigidbody body)
        {
            if (body == null)
                return 0;

            if (body.TryGetComponent(out IPhysicsImpactMaterialProvider directProvider))
                return directProvider.ImpactAudioMaterialId;

            IPhysicsImpactMaterialProvider provider = body.GetComponentInParent<IPhysicsImpactMaterialProvider>();
            return provider != null ? provider.ImpactAudioMaterialId : (byte)0;
        }

        private void RegisterOrUpdateConnection(
            UnityEngine.Object owner,
            Rigidbody bodyA,
            Rigidbody bodyB,
            PhysicsConnectionKind kind)
        {
            if (owner == null || kind == PhysicsConnectionKind.None)
                return;

            if (bodyA != null)
                RegisterTrackedBodyInternal(bodyA);
            if (bodyB != null)
                RegisterTrackedBodyInternal(bodyB);

            int connectionIndex = FindConnectionIndex(owner, kind);
            if (connectionIndex < 0)
            {
                if (_connectionCount >= _connections.Length)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning("[GlobalPhysicsStateManager] Connection registry capacity exceeded.");
#endif
                    return;
                }

                connectionIndex = _connectionCount++;
            }

            _connections[connectionIndex] = new PhysicsConnection
            {
                Owner = owner,
                BodyA = bodyA,
                BodyB = bodyB,
                CompensatedBody = null,
                Kind = kind,
                CompensationActive = false
            };

            EvaluateConnectionAt(connectionIndex);
        }

        private void UnregisterConnection(UnityEngine.Object owner, PhysicsConnectionKind kind)
        {
            int connectionIndex = FindConnectionIndex(owner, kind);
            if (connectionIndex < 0)
                return;

            RemoveConnectionAt(connectionIndex);
        }

        private void EvaluateConnections()
        {
            for (int i = 0; i < _trackedBodyCount; i++)
            {
                RigidbodyState bodyState = _bodyStates[i];
                bodyState.CompensationRefCount = 0;
                _bodyStates[i] = bodyState;
            }

            for (int i = _connectionCount - 1; i >= 0; i--)
            {
                PhysicsConnection connection = _connections[i];
                UnityEngine.Object ownerObject = connection.Owner;
                if (connection.Owner == null || ownerObject == null)
                {
                    RemoveConnectionAt(i);
                    continue;
                }

                EvaluateConnectionAt(i);
                connection = _connections[i];
                if (!connection.CompensationActive || connection.CompensatedBody == null)
                    continue;

                int compensatedIndex = FindTrackedBodyIndex(connection.CompensatedBody);
                if (compensatedIndex < 0)
                    continue;

                RigidbodyState bodyState = _bodyStates[compensatedIndex];
                bodyState.CompensationRefCount++;
                _bodyStates[compensatedIndex] = bodyState;
            }
        }

        private void EvaluateConnectionAt(int connectionIndex)
        {
            PhysicsConnection connection = _connections[connectionIndex];
            Rigidbody bodyA = connection.BodyA;
            Rigidbody bodyB = connection.BodyB;

            connection.CompensationActive = false;
            connection.CompensatedBody = null;

            if (connection.Kind == PhysicsConnectionKind.Dock)
            {
                if (bodyA != null)
                {
                    connection.CompensationActive = true;
                    connection.CompensatedBody = bodyA;
                }

                _connections[connectionIndex] = connection;
                return;
            }

            if (bodyA == null || bodyB == null || bodyA.isKinematic || bodyB.isKinematic)
            {
                _connections[connectionIndex] = connection;
                return;
            }

            float massA = math.max(bodyA.mass, MinMass);
            float massB = math.max(bodyB.mass, MinMass);
            float heavierMass = math.max(massA, massB);
            float lighterMass = math.max(math.min(massA, massB), MinMass);
            float ratio = heavierMass / lighterMass;
            if (!(ratio > MassRatioThreshold))
            {
                _connections[connectionIndex] = connection;
                return;
            }

            connection.CompensationActive = true;
            connection.CompensatedBody = massA >= massB ? bodyA : bodyB;
            _connections[connectionIndex] = connection;
        }

        private void RefreshTrackedBodies(float fixedDeltaTime)
        {
            float safeDeltaTime = math.max(fixedDeltaTime, 0.0001f);
            for (int i = _trackedBodyCount - 1; i >= 0; i--)
            {
                Rigidbody body = _trackedBodies[i];
                if (body == null)
                {
                    RemoveTrackedBodyAt(i);
                    continue;
                }

                Vector3 bodyPosition = body.position;
                if (!IsFinite(bodyPosition))
                    continue;

                RigidbodyState bodyState = _bodyStates[i];
                ApplyTrackedBodyAngularVelocityClamp(body, bodyState.MaxAngularVelocityClamp);
                Vector3 currentLinearVelocity = IsFinite(body.linearVelocity) ? body.linearVelocity : Vector3.zero;
                if (!HectonFloatingOrigin.IsShiftInProgress && IsFinite(bodyState.LastValidLinearVelocity))
                {
                    Vector3 deltaVelocity = currentLinearVelocity - bodyState.LastValidLinearVelocity;
                    float acceleration = deltaVelocity.magnitude / safeDeltaTime;
                    if (acceleration > KineticAnomalyAccelerationMetersPerSecondSq)
                        ReportKineticAnomalyOncePerFrame(bodyPosition, deltaVelocity, acceleration);
                }

                bodyState.HasLastValidPosition = true;
                bodyState.LastValidAup = AbsoluteUniversePosition.FromRuntimePosition(bodyPosition);
                bodyState.HasLastValidAup = true;
                bodyState.LastValidLinearVelocity = currentLinearVelocity;
                bodyState.LastValidAngularVelocity = IsFinite(body.angularVelocity) ? body.angularVelocity : Vector3.zero;
                _bodyStates[i] = bodyState;
                _lastValidPositions[i] = new float3(bodyPosition.x, bodyPosition.y, bodyPosition.z);
            }
        }

        private void ReportKineticAnomalyOncePerFrame(Vector3 bodyPosition, Vector3 deltaVelocity, float acceleration)
        {
            int frame = Time.frameCount;
            if (_lastKineticAnomalyFrame == frame)
                return;

            _lastKineticAnomalyFrame = frame;
            CrashTelemetryBuffer.ReportKineticAnomaly(bodyPosition, deltaVelocity, acceleration);
        }

        private void SweepNaNPhysicsState()
        {
            for (int i = _trackedBodyCount - 1; i >= 0; i--)
            {
                Rigidbody body = _trackedBodies[i];
                if (body == null)
                    continue;

                float3 position = new float3(body.position.x, body.position.y, body.position.z);
                Quaternion bodyRotation = body.rotation;
                float4 rotation = new float4(bodyRotation.x, bodyRotation.y, bodyRotation.z, bodyRotation.w);
                float3 linearVelocity = new float3(body.linearVelocity.x, body.linearVelocity.y, body.linearVelocity.z);
                float3 angularVelocity = new float3(body.angularVelocity.x, body.angularVelocity.y, body.angularVelocity.z);

                bool3 positionNaNMask = math.isnan(position);
                bool4 rotationNaNMask = math.isnan(rotation);
                bool3 linearNaNMask = math.isnan(linearVelocity);
                bool3 angularNaNMask = math.isnan(angularVelocity);
                if (!math.any(positionNaNMask | linearNaNMask | angularNaNMask) && !math.any(rotationNaNMask))
                    continue;

                RigidbodyState bodyState = _bodyStates[i];
                float3 lastValidPosition = bodyState.HasLastValidAup
                    ? bodyState.LastValidAup.ToRuntimeFloat3()
                    : bodyState.HasLastValidPosition
                        ? _lastValidPositions[i]
                        : float3.zero;

                body.position = new Vector3(lastValidPosition.x, lastValidPosition.y, lastValidPosition.z);
                if (math.any(rotationNaNMask))
                    body.rotation = Quaternion.identity;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.Sleep();
                bodyState.LastValidLinearVelocity = Vector3.zero;
                bodyState.LastValidAngularVelocity = Vector3.zero;
                bodyState.HasLastValidPosition = true;
                _lastValidPositions[i] = lastValidPosition;
                _bodyStates[i] = bodyState;

                CrashTelemetryBuffer.ReportNanPhysicsRecovery();
            }
        }

        private void ApplyDistanceKinematicSleepInternal()
        {
            ResolvePlayerTransform();
            if (_playerTransform == null)
                return;

            AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(_playerTransform.position);
            for (int i = _trackedBodyCount - 1; i >= 0; i--)
            {
                Rigidbody body = _trackedBodies[i];
                if (body == null)
                {
                    RemoveTrackedBodyAt(i);
                    continue;
                }

                RigidbodyState bodyState = _bodyStates[i];
                if (!bodyState.AllowDistanceKinematicSleep || bodyState.CompensationRefCount > 0)
                {
                    if (bodyState.DistanceKinematicSleepActive)
                        RestoreDistanceKinematicSleep(body, ref bodyState);

                    _bodyStates[i] = bodyState;
                    continue;
                }

                AbsoluteUniversePosition bodyAup = AbsoluteUniversePosition.FromRuntimePosition(body.position);
                bool shouldSleep = AbsoluteUniversePosition.DistanceSq(in bodyAup, in playerAup) > FarKinematicSleepDistanceSq;
                if (shouldSleep)
                    ApplyDistanceKinematicSleep(body, ref bodyState);
                else if (bodyState.DistanceKinematicSleepActive)
                    RestoreDistanceKinematicSleep(body, ref bodyState);

                _bodyStates[i] = bodyState;
            }
        }

        private void ApplyColliderLodHysteresisInternal(float fixedDeltaTime)
        {
            ResolvePlayerTransform();
            if (_playerTransform == null)
                return;

            AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(_playerTransform.position);
            float safeDeltaTime = Mathf.Max(0f, fixedDeltaTime);
            for (int i = _trackedBodyCount - 1; i >= 0; i--)
            {
                Rigidbody body = _trackedBodies[i];
                if (body == null)
                {
                    RemoveTrackedBodyAt(i);
                    continue;
                }

                RigidbodyState bodyState = _bodyStates[i];
                if (!bodyState.HasColliderLodSink || !IsColliderLodSinkAlive(bodyState.ColliderLodSink))
                {
                    bodyState.ColliderLodOutOfRangeSeconds = 0f;
                    bodyState.ColliderLodDistanceGateOpen = false;
                    _bodyStates[i] = bodyState;
                    continue;
                }

                AbsoluteUniversePosition bodyAup = AbsoluteUniversePosition.FromRuntimePosition(body.position);
                double distanceSq = AbsoluteUniversePosition.DistanceSq(in bodyAup, in playerAup);
                if (bodyState.ColliderLodDistanceGateOpen)
                {
                    if (distanceSq <= ColliderLodSimpleToCompoundDistanceSq)
                    {
                        bodyState.ColliderLodDistanceGateOpen = false;
                        bodyState.ColliderLodOutOfRangeSeconds = 0f;
                        bodyState.ColliderLodSink.SetColliderLodDistanceGate(false);
                    }
                }
                else if (distanceSq > ColliderLodCompoundToSimpleDistanceSq)
                {
                    bodyState.ColliderLodOutOfRangeSeconds += safeDeltaTime;
                    if (bodyState.ColliderLodOutOfRangeSeconds >= ColliderLodSimplifyHysteresisSeconds)
                    {
                        bodyState.ColliderLodDistanceGateOpen = true;
                        bodyState.ColliderLodSink.SetColliderLodDistanceGate(true);
                    }
                }
                else
                {
                    bodyState.ColliderLodOutOfRangeSeconds = 0f;
                }

                _bodyStates[i] = bodyState;
            }
        }

        private void ApplyDistanceKinematicSleep(Rigidbody body, ref RigidbodyState bodyState)
        {
            if (bodyState.DistanceKinematicSleepActive)
                return;

            bodyState.KinematicModeBeforeDistanceSleep = body.isKinematic;
            bodyState.DetectCollisionsBeforeDistanceSleep = body.detectCollisions;
            bodyState.WasSleepingBeforeDistanceSleep = body.IsSleeping();
            if (bodyState.WasSleepingBeforeDistanceSleep)
                bodyState.StateMask |= PhysicsStateMask.WasAsleep;
            else
                bodyState.StateMask &= ~PhysicsStateMask.WasAsleep;

            body.isKinematic = true;
            body.detectCollisions = false;
            body.Sleep();
            bodyState.DistanceKinematicSleepActive = true;
        }

        private void RestoreDistanceKinematicSleep(Rigidbody body, ref RigidbodyState bodyState)
        {
            if (!bodyState.DistanceKinematicSleepActive)
                return;

            body.isKinematic = bodyState.KinematicModeBeforeDistanceSleep;
            body.detectCollisions = bodyState.DetectCollisionsBeforeDistanceSleep;
            bool wasAsleepBeforeEviction = bodyState.WasSleepingBeforeDistanceSleep ||
                                           (bodyState.StateMask & PhysicsStateMask.WasAsleep) != 0;
            if (!body.isKinematic && !wasAsleepBeforeEviction)
                body.WakeUp();
            else if (!body.isKinematic)
                body.Sleep();

            bodyState.WasSleepingBeforeDistanceSleep = false;
            bodyState.StateMask &= ~PhysicsStateMask.WasAsleep;
            bodyState.DistanceKinematicSleepActive = false;
        }

        private void ApplyAddedMassTensorState()
        {
            for (int i = _trackedBodyCount - 1; i >= 0; i--)
            {
                Rigidbody body = _trackedBodies[i];
                if (body == null)
                {
                    RemoveTrackedBodyAt(i);
                    continue;
                }

                RigidbodyState bodyState = _bodyStates[i];
                CaptureAddedMassBaseline(body, ref bodyState);
                float submersionFactor = math.saturate(bodyState.HydrodynamicSubmersionFactor);
                if (submersionFactor <= 0.0001f)
                {
                    if (bodyState.AddedMassTensorApplied)
                        RestoreAddedMassBaseline(body, ref bodyState);

                    _bodyStates[i] = bodyState;
                    continue;
                }

                float multiplier = 1f + (AddedMassAngularDampingScale * submersionFactor);
                float inertiaMultiplier = 1f + (AddedMassInertiaTensorScale * submersionFactor);
                body.angularDamping = bodyState.BaseAngularDamping * multiplier;
                body.inertiaTensor = bodyState.BaseInertiaTensor * inertiaMultiplier;
                body.inertiaTensorRotation = bodyState.BaseInertiaTensorRotation;
                bodyState.AddedMassTensorApplied = true;
                _bodyStates[i] = bodyState;
            }
        }

        private static void CaptureAddedMassBaseline(Rigidbody body, ref RigidbodyState bodyState)
        {
            if (body == null || bodyState.HasAddedMassBaseline)
                return;

            bodyState.BaseInertiaTensor = IsFinite(body.inertiaTensor) ? body.inertiaTensor : Vector3.one;
            bodyState.BaseInertiaTensorRotation = IsFinite(body.inertiaTensorRotation) ? body.inertiaTensorRotation : Quaternion.identity;
            bodyState.BaseAngularDamping = math.max(0f, body.angularDamping);
            bodyState.HasAddedMassBaseline = true;
        }

        private static void RestoreAddedMassBaseline(Rigidbody body, ref RigidbodyState bodyState)
        {
            if (body == null || !bodyState.HasAddedMassBaseline)
                return;

            if (IsFinite(bodyState.BaseInertiaTensor))
                body.inertiaTensor = bodyState.BaseInertiaTensor;
            if (IsFinite(bodyState.BaseInertiaTensorRotation))
                body.inertiaTensorRotation = bodyState.BaseInertiaTensorRotation;
            body.angularDamping = math.max(0f, bodyState.BaseAngularDamping);
            bodyState.AddedMassTensorApplied = false;
        }

        private void RemoveTrackedBodyAt(int bodyIndex)
        {
            int lastIndex = _trackedBodyCount - 1;
            if (bodyIndex < 0 || bodyIndex > lastIndex)
                return;

            Rigidbody removedBody = _trackedBodies[bodyIndex];
            if (removedBody != null)
            {
                RigidbodyState removedState = _bodyStates[bodyIndex];
                RestoreColliderLodGate(ref removedState);
                if (removedState.AddedMassTensorApplied)
                    RestoreAddedMassBaseline(removedBody, ref removedState);
                _trackedBodyIndexByEntityId.Remove(EntityId.ToULong(removedBody.GetEntityId()));
            }

            for (int i = _connectionCount - 1; i >= 0; i--)
            {
                PhysicsConnection connection = _connections[i];
                if (ReferenceEquals(connection.BodyA, removedBody) ||
                    ReferenceEquals(connection.BodyB, removedBody) ||
                    ReferenceEquals(connection.CompensatedBody, removedBody))
                {
                    RemoveConnectionAt(i);
                }
            }

            _trackedBodies[bodyIndex] = _trackedBodies[lastIndex];
            _trackedBodies[lastIndex] = null;
            _bodyStates[bodyIndex] = _bodyStates[lastIndex];
            _bodyStates[lastIndex] = default;
            _lastValidPositions[bodyIndex] = _lastValidPositions[lastIndex];
            _lastValidPositions[lastIndex] = default;
            if (bodyIndex != lastIndex)
            {
                Rigidbody movedBody = _trackedBodies[bodyIndex];
                if (movedBody != null)
                    _trackedBodyIndexByEntityId[EntityId.ToULong(movedBody.GetEntityId())] = bodyIndex;
            }
            _trackedBodyCount--;
        }

        private void ResolvePlayerTransform()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            _playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
        }

        private static bool ShouldAllowDistanceKinematicSleep(Rigidbody body)
        {
            if (body == null)
                return false;

            if (body.CompareTag("Player"))
                return false;

            if (body.TryGetComponent(out HectonPlayerMotor _) ||
                body.TryGetComponent(out MountablePlayerTransport _) ||
                body.TryGetComponent(out SubmarineCoreDirector _))
            {
                return false;
            }

            return true;
        }

        private static IPhysicsColliderLodHysteresisSink ResolveColliderLodSink(Rigidbody body)
        {
            if (body != null && body.TryGetComponent(out IPhysicsColliderLodHysteresisSink sink))
                return sink;

            return null;
        }

        private static bool IsColliderLodSinkAlive(IPhysicsColliderLodHysteresisSink sink)
        {
            if (sink == null)
                return false;

            return !(sink is UnityEngine.Object unityObject) || unityObject != null;
        }

        private static void RestoreColliderLodGate(ref RigidbodyState bodyState)
        {
            if (!bodyState.ColliderLodDistanceGateOpen)
                return;

            if (IsColliderLodSinkAlive(bodyState.ColliderLodSink))
                bodyState.ColliderLodSink.SetColliderLodDistanceGate(false);

            bodyState.ColliderLodDistanceGateOpen = false;
            bodyState.ColliderLodOutOfRangeSeconds = 0f;
        }

        private static float ResolveMaxAngularVelocityClamp(Rigidbody body)
        {
            if (body == null)
                return 0f;

            if (body.TryGetComponent(out MountablePlayerTransport _) ||
                body.TryGetComponent(out VehicleMotor _) ||
                body.TryGetComponent(out SubmarineCoreDirector _))
            {
                return 3f;
            }

            if (body.TryGetComponent(out FaunaBrain _))
                return 4f;

            return 0f;
        }

        private static void ApplyTrackedBodyAngularVelocityClamp(Rigidbody body, float maxAngularVelocityClamp)
        {
            if (body == null || maxAngularVelocityClamp <= 0f)
                return;

            if (math.abs(body.maxAngularVelocity - maxAngularVelocityClamp) > 0.0001f)
                body.maxAngularVelocity = maxAngularVelocityClamp;
        }

        private void RemoveConnectionAt(int connectionIndex)
        {
            int lastIndex = _connectionCount - 1;
            if (connectionIndex < 0 || connectionIndex > lastIndex)
                return;

            _connections[connectionIndex] = _connections[lastIndex];
            _connections[lastIndex] = default;
            _connectionCount--;
        }

        private int FindTrackedBodyIndex(Rigidbody body)
        {
            if (body == null)
                return -1;

            ulong bodyEntityId = EntityId.ToULong(body.GetEntityId());
            if (!_trackedBodyIndexByEntityId.TryGetValue(bodyEntityId, out int index))
                return -1;

            if ((uint)index >= (uint)_trackedBodyCount || !ReferenceEquals(_trackedBodies[index], body))
            {
                _trackedBodyIndexByEntityId.Remove(bodyEntityId);
                return -1;
            }

            return index;
        }

        private int FindConnectionIndex(UnityEngine.Object owner, PhysicsConnectionKind kind)
        {
            for (int i = 0; i < _connectionCount; i++)
            {
                PhysicsConnection connection = _connections[i];
                if (connection.Kind == kind && ReferenceEquals(connection.Owner, owner))
                    return i;
            }

            return -1;
        }

        private void EnsureReporter(Rigidbody body)
        {
            if (body == null)
                return;

            if (body.TryGetComponent(out PhysicsStateReporter reporter))
                return;

            body.gameObject.AddComponent<PhysicsStateReporter>(); // COLD ALLOC: PhysicsStateReporter[1] â€” runtime collision relay added to tracked rigidbodies â€” owner: GlobalPhysicsStateManager
        }

        private void EnsureTrackedBodyCapacity(int requiredCount)
        {
            if (requiredCount <= _trackedBodies.Length)
                return;

            int newCapacity = _trackedBodies.Length;
            int growthWatchdog = 31;
            while (newCapacity < requiredCount && growthWatchdog-- > 0)
                newCapacity <<= 1;

            if (newCapacity < requiredCount)
            {
                Debug.LogError(
                    $"[GlobalPhysicsStateManager] Failed to grow tracked body capacity for requiredCount={requiredCount}.");
                return;
            }

            Array.Resize(ref _trackedBodies, newCapacity);
            Array.Resize(ref _bodyStates, newCapacity);

            if (!_lastValidPositions.IsCreated)
                return;

            NativeArray<float3> resizedPositions = new NativeArray<float3>(newCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < _trackedBodyCount; i++)
                resizedPositions[i] = _lastValidPositions[i];

            NativeMemorySentinel.UnregisterNativeArray(_lastValidPositions);
            _lastValidPositions.Dispose();
            _lastValidPositions = resizedPositions;
            NativeMemorySentinel.RegisterNativeArray(
                _lastValidPositions,
                nameof(GlobalPhysicsStateManager),
                nameof(_lastValidPositions),
                NativeAllocationLifetime.Session);
        }

        private void ScanLoadedScenesForRigidbodies()
        {
            int sceneCount = SceneManager.sceneCount;
            for (int sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
                ScanSceneForRigidbodies(SceneManager.GetSceneAt(sceneIndex));
        }

        private void ScanSceneForRigidbodies(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            _sceneRootScratch.Clear();
            scene.GetRootGameObjects(_sceneRootScratch);

            int rootCount = _sceneRootScratch.Count;
            for (int rootIndex = 0; rootIndex < rootCount; rootIndex++)
            {
                GameObject rootObject = _sceneRootScratch[rootIndex];
                if (rootObject == null || !rootObject.activeInHierarchy)
                    continue;

                _sceneRigidbodyScratch.Clear();
                rootObject.GetComponentsInChildren(false, _sceneRigidbodyScratch);
                int bodyCount = _sceneRigidbodyScratch.Count;
                EnsureTrackedBodyCapacity(_trackedBodyCount + bodyCount);
                for (int bodyIndex = 0; bodyIndex < bodyCount; bodyIndex++)
                    RegisterTrackedBodyInternal(_sceneRigidbodyScratch[bodyIndex]);
            }

            _sceneRigidbodyScratch.Clear();
            _sceneRootScratch.Clear();
        }

        private void ClearRuntimeState()
        {
            if (_impactQueue.IsCreated)
            {
                int drainIterations = 0;
                while (drainIterations < MaxQueuedImpactEvents && _impactQueue.TryDequeue(out _))
                {
                    drainIterations++;
                }
            }

            _queuedImpactCount = 0;

            for (int i = 0; i < _trackedBodyCount; i++)
            {
                Rigidbody body = _trackedBodies[i];
                if (body == null)
                    continue;

                RigidbodyState bodyState = _bodyStates[i];
                RestoreColliderLodGate(ref bodyState);
                if (bodyState.AddedMassTensorApplied)
                    RestoreAddedMassBaseline(body, ref bodyState);
            }

            Array.Clear(_trackedBodies, 0, _trackedBodyCount);
            Array.Clear(_bodyStates, 0, _trackedBodyCount);
            Array.Clear(_connections, 0, _connectionCount);
            _trackedBodyIndexByEntityId.Clear();
            if (_lastValidPositions.IsCreated)
            {
                for (int i = 0; i < _lastValidPositions.Length; i++)
                    _lastValidPositions[i] = default;
            }

            _trackedBodyCount = 0;
            _connectionCount = 0;
            _queuedImpactCount = 0;
        }

        private static PhysicsImpactWeightClass ResolveImpactWeightClass(float impactIntensity)
        {
            if (impactIntensity >= HeavyImpactIntensity)
                return PhysicsImpactWeightClass.Heavy;

            if (impactIntensity >= MediumImpactIntensity)
                return PhysicsImpactWeightClass.Medium;

            return PhysicsImpactWeightClass.Light;
        }

        private static float ResolveImpactIntensityFromForce(float impactForce)
        {
            return math.log10(1f + (math.max(0f, impactForce) / 100f));
        }

        private void SubscribeSceneEvents()
        {
            if (_sceneEventsSubscribed)
                return;

            SceneManager.sceneLoaded += HandleSceneLoaded;
            _sceneEventsSubscribed = true;
        }

        private void UnsubscribeSceneEvents()
        {
            if (!_sceneEventsSubscribed)
                return;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _sceneEventsSubscribed = false;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ScanSceneForRigidbodies(scene);
        }

        private static bool IsFinite(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(value3));
        }

        private static bool IsFinite(Quaternion value)
        {
            float4 value4 = new float4(value.x, value.y, value.z, value.w);
            return math.all(math.isfinite(value4));
        }
    }

    /// <summary>
    /// Lightweight per-rigidbody collision relay that forwards impact data into <see cref="GlobalPhysicsStateManager"/>.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class PhysicsStateReporter : MonoBehaviour
    {
        private Rigidbody _body;
        private ulong _entityId;

        private void Awake()
        {
            TryGetComponent(out _body);
            _entityId = _body != null ? EntityId.ToULong(_body.GetEntityId()) : 0ul;
        }

        private void OnEnable()
        {
            if (_body == null)
                TryGetComponent(out _body);

            if (_body == null)
                return;

            _entityId = EntityId.ToULong(_body.GetEntityId());
            GlobalPhysicsStateManager.RegisterTrackedBody(_body);
        }

        private void OnDisable()
        {
            if (_body != null)
                GlobalPhysicsStateManager.UnregisterTrackedBody(_body);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_body == null || collision == null)
                return;

            Rigidbody otherBody = collision.rigidbody;
            if (otherBody != null && _entityId > EntityId.ToULong(otherBody.GetEntityId()))
                return;

            GlobalPhysicsStateManager.QueueImpact(_body, otherBody, collision);
        }
    }
}
