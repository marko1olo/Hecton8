using System;
using Hecton8.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
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
            PhysicsImpactWeightClass weightClass)
        {
            PrimaryBodyId = primaryBodyId;
            SecondaryBodyId = secondaryBodyId;
            Point = point;
            Normal = normal;
            Force = force;
            Intensity = intensity;
            WeightClass = weightClass;
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
        [BurstCompile]
        private struct TranslateLastValidPositionsJob : IJobParallelFor
        {
            public float3 ShiftOffset;
            public NativeArray<float3> LastValidPositions;

            public void Execute(int index)
            {
                LastValidPositions[index] -= ShiftOffset;
            }
        }

        private struct BodyState
        {
            public ulong EntityId;
            public int CompensationRefCount;
            public bool HasLastValidPosition;
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
        }

        private const int MaxTrackedBodies = 512;
        private const int MaxTrackedConnections = 128;
        private const int MaxQueuedImpactEvents = 256;
        private const float MinMass = 0.0001f;
        private const float MassRatioThreshold = 100f;
        private const float MinImpactForce = 0.01f;
        private const float HeavyImpactIntensity = 0.95f;
        private const float MediumImpactIntensity = 0.45f;

        private static GlobalPhysicsStateManager _instance;

        // COLD ALLOC: Rigidbody[512] — authoritative tracked rigidbody registry — owner: GlobalPhysicsStateManager
        private readonly Rigidbody[] _trackedBodies = new Rigidbody[MaxTrackedBodies];
        // COLD ALLOC: BodyState[512] — per-body runtime state and compensation flags — owner: GlobalPhysicsStateManager
        private readonly BodyState[] _bodyStates = new BodyState[MaxTrackedBodies];
        // COLD ALLOC: PhysicsConnection[128] — tracked tether/dock connection registry — owner: GlobalPhysicsStateManager
        private readonly PhysicsConnection[] _connections = new PhysicsConnection[MaxTrackedConnections];

        private NativeArray<float3> _lastValidPositions;
        private NativeQueue<PhysicsImpactEventData> _impactQueue;
        private int _trackedBodyCount;
        private int _connectionCount;
        private int _queuedImpactCount;
        private bool _registeredFixedTick;
        private bool _registeredOriginShift;
        private bool _sceneEventsSubscribed;

        /// <summary>Current live runtime instance when one exists.</summary>
        public static GlobalPhysicsStateManager Instance => _instance;

        /// <summary>
        /// Ensures a runtime physics-state manager exists.
        /// </summary>
        /// <returns>Live runtime manager.</returns>
        public static GlobalPhysicsStateManager EnsureRuntimeInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject runtimeRoot = new GameObject("[GlobalPhysicsStateManager]");
            return runtimeRoot.AddComponent<GlobalPhysicsStateManager>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        internal static void RegisterTrackedBody(Rigidbody body)
        {
            EnsureRuntimeInstance().RegisterTrackedBodyInternal(body);
        }

        internal static void UnregisterTrackedBody(Rigidbody body)
        {
            if (_instance == null || body == null)
                return;

            _instance.UnregisterTrackedBodyInternal(body);
        }

        internal static void QueueImpact(Rigidbody primaryBody, Rigidbody secondaryBody, Collision collision)
        {
            if (_instance == null || primaryBody == null || collision == null)
                return;

            _instance.QueueImpactInternal(primaryBody, secondaryBody, collision);
        }

        internal static void RegisterTetherConnection(UnityEngine.Object owner, Rigidbody anchorBody, Rigidbody payloadBody)
        {
            EnsureRuntimeInstance().RegisterOrUpdateConnection(owner, anchorBody, payloadBody, PhysicsConnectionKind.Tether);
        }

        internal static void UnregisterTetherConnection(UnityEngine.Object owner)
        {
            if (_instance == null || owner == null)
                return;

            _instance.UnregisterConnection(owner, PhysicsConnectionKind.Tether);
        }

        internal static void RegisterDockConnection(UnityEngine.Object owner, Rigidbody dockedBody)
        {
            EnsureRuntimeInstance().RegisterOrUpdateConnection(owner, dockedBody, null, PhysicsConnectionKind.Dock);
        }

        internal static void UnregisterDockConnection(UnityEngine.Object owner)
        {
            if (_instance == null || owner == null)
                return;

            _instance.UnregisterConnection(owner, PhysicsConnectionKind.Dock);
        }

        internal static bool IsKinematicAnchorCompensationEnabled(UnityEngine.Object owner, PhysicsConnectionKind kind)
        {
            if (_instance == null || owner == null)
                return false;

            int connectionIndex = _instance.FindConnectionIndex(owner, kind);
            if (connectionIndex < 0)
                return false;

            return _instance._connections[connectionIndex].CompensationActive;
        }

        /// <summary>
        /// Clears tracked bodies, connections, and queued impacts during a guarded scene transition.
        /// </summary>
        public static void ClearRuntimeStateStatic()
        {
            if (_instance != null)
                _instance.ClearRuntimeState();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            if (!_lastValidPositions.IsCreated)
            {
                // COLD ALLOC: NativeArray<float3>[512] — authoritative last-valid runtime-space body positions for origin-shift-safe recovery — owner: GlobalPhysicsStateManager
                _lastValidPositions = new NativeArray<float3>(MaxTrackedBodies, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            if (!_impactQueue.IsCreated)
            {
                // COLD ALLOC: NativeQueue<PhysicsImpactEventData>(Persistent) — deferred gameplay physics impact bus — owner: GlobalPhysicsStateManager
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
            if (!_registeredFixedTick && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register((IFixedTickable)this);
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
            if (_registeredFixedTick && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister((IFixedTickable)this);
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

            if (_instance == this)
                _instance = null;
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            RefreshTrackedBodies();
            EvaluateConnections();
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            if (!_lastValidPositions.IsCreated || _trackedBodyCount <= 0 || shiftOffset.sqrMagnitude <= 0.000001f)
                return;

            TranslateLastValidPositionsJob translateJob = new TranslateLastValidPositionsJob
            {
                ShiftOffset = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z),
                LastValidPositions = _lastValidPositions
            };

            JobHandle handle = translateJob.Schedule(_trackedBodyCount, 32);
            handle.Complete();
        }

        private void RegisterTrackedBodyInternal(Rigidbody body)
        {
            if (body == null)
                return;

            if (FindTrackedBodyIndex(body) >= 0)
                return;

            if (_trackedBodyCount >= _trackedBodies.Length)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[GlobalPhysicsStateManager] Tracked rigidbody capacity exceeded.");
#endif
                return;
            }

            EnsureReporter(body);

            int bodyIndex = _trackedBodyCount++;
            _trackedBodies[bodyIndex] = body;
            _bodyStates[bodyIndex] = new BodyState
            {
                EntityId = EntityId.ToULong(body.GetEntityId()),
                CompensationRefCount = 0,
                HasLastValidPosition = IsFinite(body.position),
                LastValidLinearVelocity = IsFinite(body.linearVelocity) ? body.linearVelocity : Vector3.zero,
                LastValidAngularVelocity = IsFinite(body.angularVelocity) ? body.angularVelocity : Vector3.zero
            };

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

            float impactIntensity = math.log10(1f + (impactForce / 100f));
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
                WeightClass = weightClass
            });
            _queuedImpactCount++;
        }

        private void FlushImpactEvents()
        {
            if (!_impactQueue.IsCreated || _queuedImpactCount <= 0)
                return;

            while (_queuedImpactCount > 0 && _impactQueue.TryDequeue(out PhysicsImpactEventData impactEvent))
            {
                _queuedImpactCount--;
                PhysicsEvents.RaiseImpact(new PhysicsImpactSignal(
                    impactEvent.PrimaryBodyId,
                    impactEvent.SecondaryBodyId,
                    new Vector3(impactEvent.Point.x, impactEvent.Point.y, impactEvent.Point.z),
                    new Vector3(impactEvent.Normal.x, impactEvent.Normal.y, impactEvent.Normal.z),
                    impactEvent.Force,
                    impactEvent.Intensity,
                    impactEvent.WeightClass));
            }
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
                BodyState bodyState = _bodyStates[i];
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

                BodyState bodyState = _bodyStates[compensatedIndex];
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

                BodyState bodyState = _bodyStates[i];
                bodyState.HasLastValidPosition = true;
                bodyState.LastValidLinearVelocity = IsFinite(body.linearVelocity) ? body.linearVelocity : Vector3.zero;
                bodyState.LastValidAngularVelocity = IsFinite(body.angularVelocity) ? body.angularVelocity : Vector3.zero;
                _bodyStates[i] = bodyState;
                _lastValidPositions[i] = new float3(bodyPosition.x, bodyPosition.y, bodyPosition.z);
            }
        }

        private void RemoveTrackedBodyAt(int bodyIndex)
        {
            int lastIndex = _trackedBodyCount - 1;
            if (bodyIndex < 0 || bodyIndex > lastIndex)
                return;

            Rigidbody removedBody = _trackedBodies[bodyIndex];
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
            _trackedBodyCount--;
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

            for (int i = 0; i < _trackedBodyCount; i++)
            {
                if (ReferenceEquals(_trackedBodies[i], body))
                    return i;
            }

            return -1;
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

            body.gameObject.AddComponent<PhysicsStateReporter>(); // COLD ALLOC: PhysicsStateReporter[1] — runtime collision relay added to tracked rigidbodies — owner: GlobalPhysicsStateManager
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
                while (_impactQueue.TryDequeue(out _))
                {
                }
            }

            Array.Clear(_trackedBodies, 0, _trackedBodyCount);
            Array.Clear(_bodyStates, 0, _trackedBodyCount);
            Array.Clear(_connections, 0, _connectionCount);
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
