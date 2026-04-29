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
    /// Static zero-instance gameplay event bus for deferred physics-impact feedback.
    /// </summary>
    public static class PhysicsEvents
    {
        /// <summary>
        /// Raised once per queued impact during the late-frame flush.
        /// </summary>
        public static event Action<PhysicsImpactSignal> OnImpact;

        internal static void RaiseImpact(in PhysicsImpactSignal impactSignal)
        {
            Action<PhysicsImpactSignal> handler = OnImpact;
            if (handler == null)
                return;

            handler(impactSignal);
        }
    }

    /// <summary>
    /// Authoritative runtime registry for active rigidbodies, mass-ratio guards, and queued impact feedback.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8995)]
    public sealed class GlobalPhysicsStateManager : MonoBehaviour, IFixedTickable, IOriginShiftListener
    {
        private struct RigidbodyState
        {
            public ulong EntityId;
            public int CompensationRefCount;
            public float MaxAngularVelocityClamp;
            public bool AllowDistanceKinematicSleep;
            public bool DistanceKinematicSleepActive;
            public bool HasLastValidPosition;
            public bool HasOriginShiftSnapshot;
            public bool WasSleepingBeforeOriginShift;
            public bool InterpolationSuspendedForOriginShift;
            public bool KinematicModeBeforeDistanceSleep;
            public bool DetectCollisionsBeforeDistanceSleep;
            public RigidbodyInterpolation InterpolationModeBeforeOriginShift;
            public Vector3 SnapshotPositionBeforeOriginShift;
            public Quaternion SnapshotRotationBeforeOriginShift;
            public Vector3 LastValidLinearVelocity;
            public Vector3 LastValidAngularVelocity;
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
        private const float MinMass = 0.0001f;
        private const float MassRatioThreshold = 100f;
        private const float MinImpactForce = 0.01f;
        private const float HeavyImpactIntensity = 0.95f;
        private const float MediumImpactIntensity = 0.45f;
        private const float FarKinematicSleepDistanceMeters = 500f;
        private const double FarKinematicSleepDistanceSq = FarKinematicSleepDistanceMeters * FarKinematicSleepDistanceMeters;

        // COLD ALLOC: Rigidbody[512 initial] â€” authoritative tracked rigidbody registry â€” owner: GlobalPhysicsStateManager
        private Rigidbody[] _trackedBodies = new Rigidbody[MaxTrackedBodies];
        // COLD ALLOC: RigidbodyState[512 initial] â€” per-body runtime state and compensation flags â€” owner: GlobalPhysicsStateManager
        private RigidbodyState[] _bodyStates = new RigidbodyState[MaxTrackedBodies];
        // COLD ALLOC: PhysicsConnection[128] â€” tracked tether/dock connection registry â€” owner: GlobalPhysicsStateManager
        private readonly PhysicsConnection[] _connections = new PhysicsConnection[MaxTrackedConnections];
        // COLD ALLOC: Dictionary<ulong,int>[512 initial] â€” rigidbody entity-id to tracked-index map for O(1) lookups during origin shifts â€” owner: GlobalPhysicsStateManager
        private readonly Dictionary<ulong, int> _trackedBodyIndexByEntityId = new Dictionary<ulong, int>(MaxTrackedBodies);

        private NativeArray<float3> _lastValidPositions;
        private NativeQueue<PhysicsImpactEventData> _impactQueue;
        private int _trackedBodyCount;
        private int _connectionCount;
        private int _queuedImpactCount;
        private bool _registeredFixedTick;
        private bool _registeredOriginShift;
        private bool _sceneEventsSubscribed;
        private Transform _playerTransform;

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

        internal static void UnregisterTrackedBody(Rigidbody body)
        {
            if (body == null || !TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return;

            manager.UnregisterTrackedBodyInternal(body);
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
            GlobalPhysicsStateManager registeredManager = GlobalRegistry.PhysicsStateManager;
            if (registeredManager != null && !ReferenceEquals(registeredManager, this))
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterPhysicsStateManager(this);

            if (!_lastValidPositions.IsCreated)
            {
                // COLD ALLOC: NativeArray<float3>[512 initial] â€” authoritative last-valid runtime-space body positions for origin-shift-safe recovery â€” owner: GlobalPhysicsStateManager
                _lastValidPositions = new NativeArray<float3>(_trackedBodies.Length, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            if (!_impactQueue.IsCreated)
            {
                // COLD ALLOC: NativeQueue<PhysicsImpactEventData>(Persistent) â€” deferred gameplay physics impact bus â€” owner: GlobalPhysicsStateManager
                _impactQueue = new NativeQueue<PhysicsImpactEventData>(Allocator.Persistent);
            }

            if (Application.isPlaying)
            {
                if (transform.parent != null)
                    transform.SetParent(null, true);

                DontDestroyOnLoad(gameObject);
            }

            SubscribeSceneEvents();
            ScanLoadedScenesForRigidbodies();
        }

        private void OnEnable()
        {
            if (!_registeredFixedTick && Application.isPlaying && GlobalRegistry.Dispatcher != null)
            {
                GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Core);
                _registeredFixedTick = true;
            }

            if (!_registeredOriginShift)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginShift = true;
            }
        }

        private void LateUpdate()
        {
            FlushImpactEvents();
        }

        private void OnDisable()
        {
            if (_registeredFixedTick)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Core);
                _registeredFixedTick = false;
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
                _impactQueue.Dispose();

            if (_lastValidPositions.IsCreated)
                _lastValidPositions.Dispose();

            GlobalRegistry.UnregisterPhysicsStateManager(this);
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            RefreshTrackedBodies();
            SweepNaNPhysicsState();
            EvaluateConnections();
            ApplyDistanceKinematicSleepInternal();
        }

        private static bool TryGetRuntimeManager(out GlobalPhysicsStateManager manager)
        {
            manager = GlobalRegistry.PhysicsStateManager;
            return manager != null;
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
            RegisterActiveRigidbodiesForOriginShift();

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
                bodyState.WasSleepingBeforeOriginShift = body.IsSleeping();
                bodyState.InterpolationModeBeforeOriginShift = body.interpolation;
                bodyState.InterpolationSuspendedForOriginShift = body.interpolation != RigidbodyInterpolation.None;
                if (bodyState.InterpolationSuspendedForOriginShift)
                    body.interpolation = RigidbodyInterpolation.None;
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
                    continue;

                body.interpolation = bodyState.InterpolationModeBeforeOriginShift;
                bodyState.InterpolationSuspendedForOriginShift = false;
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

            int bodyIndex = _trackedBodyCount++;
            _trackedBodies[bodyIndex] = body;
            _bodyStates[bodyIndex] = new RigidbodyState
            {
                EntityId = bodyEntityId,
                CompensationRefCount = 0,
                MaxAngularVelocityClamp = ResolveMaxAngularVelocityClamp(body),
                AllowDistanceKinematicSleep = ShouldAllowDistanceKinematicSleep(body),
                DistanceKinematicSleepActive = false,
                HasLastValidPosition = IsFinite(body.position),
                KinematicModeBeforeDistanceSleep = body.isKinematic,
                DetectCollisionsBeforeDistanceSleep = body.detectCollisions,
                LastValidLinearVelocity = IsFinite(body.linearVelocity) ? body.linearVelocity : Vector3.zero,
                LastValidAngularVelocity = IsFinite(body.angularVelocity) ? body.angularVelocity : Vector3.zero
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

        private void QueueImpactInternal(Rigidbody primaryBody, Rigidbody secondaryBody, Collision collision)
        {
            if (!_impactQueue.IsCreated || _queuedImpactCount >= MaxQueuedImpactEvents)
                return;

            float fixedDelta = math.max(Time.fixedDeltaTime, 0.0001f);
            float impactForce = collision.impulse.magnitude / fixedDelta;
            if (!(impactForce > MinImpactForce))
                return;

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
                    impactEvent.WeightClass,
                    impactEvent.PrimaryAudioMaterialId,
                    impactEvent.SecondaryAudioMaterialId));
            }
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

        private void RefreshTrackedBodies()
        {
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
                bodyState.HasLastValidPosition = true;
                bodyState.LastValidLinearVelocity = IsFinite(body.linearVelocity) ? body.linearVelocity : Vector3.zero;
                bodyState.LastValidAngularVelocity = IsFinite(body.angularVelocity) ? body.angularVelocity : Vector3.zero;
                _bodyStates[i] = bodyState;
                _lastValidPositions[i] = new float3(bodyPosition.x, bodyPosition.y, bodyPosition.z);
            }
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
                float3 lastValidPosition = bodyState.HasLastValidPosition
                    ? _lastValidPositions[i]
                    : float3.zero;

                body.position = new Vector3(lastValidPosition.x, lastValidPosition.y, lastValidPosition.z);
                if (math.any(rotationNaNMask))
                    body.rotation = Quaternion.identity;
                body.linearVelocity = IsFinite(bodyState.LastValidLinearVelocity)
                    ? bodyState.LastValidLinearVelocity
                    : Vector3.zero;
                body.angularVelocity = IsFinite(bodyState.LastValidAngularVelocity)
                    ? bodyState.LastValidAngularVelocity
                    : Vector3.zero;
                body.Sleep();

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

        private void ApplyDistanceKinematicSleep(Rigidbody body, ref RigidbodyState bodyState)
        {
            if (bodyState.DistanceKinematicSleepActive)
                return;

            bodyState.KinematicModeBeforeDistanceSleep = body.isKinematic;
            bodyState.DetectCollisionsBeforeDistanceSleep = body.detectCollisions;
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
            if (!body.isKinematic)
                body.WakeUp();

            bodyState.DistanceKinematicSleepActive = false;
        }

        private void RemoveTrackedBodyAt(int bodyIndex)
        {
            int lastIndex = _trackedBodyCount - 1;
            if (bodyIndex < 0 || bodyIndex > lastIndex)
                return;

            Rigidbody removedBody = _trackedBodies[bodyIndex];
            if (removedBody != null)
                _trackedBodyIndexByEntityId.Remove(EntityId.ToULong(removedBody.GetEntityId()));

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

            _lastValidPositions.Dispose();
            _lastValidPositions = resizedPositions;
        }

        private void RegisterActiveRigidbodiesForOriginShift()
        {
            // COLD ALLOC: Rigidbody[][active scene body count] â€” one-shot pre-shift sweep so every live dynamic body gets interpolation suspension before AUP teleport â€” owner: GlobalPhysicsStateManager
            Rigidbody[] bodies = FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude);
            EnsureTrackedBodyCapacity(_trackedBodyCount + bodies.Length);
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody body = bodies[i];
                if (body == null || body.isKinematic)
                    continue;

                RegisterTrackedBodyInternal(body);
            }
        }

        private void ScanLoadedScenesForRigidbodies()
        {
            Rigidbody[] bodies = FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude);
            for (int i = 0; i < bodies.Length; i++)
                RegisterTrackedBodyInternal(bodies[i]);
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
            ScanLoadedScenesForRigidbodies();
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
