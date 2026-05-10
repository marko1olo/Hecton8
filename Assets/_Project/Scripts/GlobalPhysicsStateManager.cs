using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton.Localization;
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
            _pointAup = AbsoluteUniversePosition.FromRuntimePosition(point);
            _hasPointAup = 1;
            Normal = normal;
            Force = force;
            Intensity = intensity;
            MassVelocity = massVelocity;
            WeightClass = weightClass;
            PrimaryAudioMaterialId = primaryAudioMaterialId;
            SecondaryAudioMaterialId = secondaryAudioMaterialId;
        }

        /// <summary>
        /// Creates a queued gameplay physics-impact payload with authoritative AUP already resolved.
        /// </summary>
        public PhysicsImpactSignal(
            ulong primaryBodyId,
            ulong secondaryBodyId,
            Vector3 point,
            in AbsoluteUniversePosition pointAup,
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
            _pointAup = pointAup;
            _hasPointAup = 1;
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

        /// <summary>True when the impact point already carries floating-origin-safe AUP.</summary>
        public bool HasPointAup => _hasPointAup != 0;

        /// <summary>Resolved floating-origin-safe impact point.</summary>
        public AbsoluteUniversePosition PointAup => ResolvePointAup();

        /// <summary>Returns the impact point as AUP, falling back only for default/legacy payloads.</summary>
        public AbsoluteUniversePosition ResolvePointAup()
        {
            return _hasPointAup != 0 ? _pointAup : AbsoluteUniversePosition.FromRuntimePosition(Point);
        }

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

        private readonly AbsoluteUniversePosition _pointAup;
        private readonly byte _hasPointAup;
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

        internal static bool HasImpactListeners => _impactListeners.Count > 0;

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
            {
                IPhysicsImpactEventListener listener = rawArray[i];
                if (listener != null)
                    listener.OnPhysicsImpact(in impactSignal);
            }
        }
    }

    /// <summary>
    /// Authoritative runtime registry for active rigidbodies, mass-ratio guards, and queued impact feedback.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8995)]
    public sealed class GlobalPhysicsStateManager : MonoBehaviour, IFixedTickable, ILateFrameTickable, IPostFixedTickable, IOriginShiftListener, IServiceHeartbeat, IServiceShutdown
    {
        [StructLayout(LayoutKind.Sequential)]
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
            public bool SafeTeleportSpeculativeCcdActive;
            public bool KinematicModeBeforeDistanceSleep;
            public bool DetectCollisionsBeforeDistanceSleep;
            public RigidbodyInterpolation InterpolationModeBeforeOriginShift;
            public CollisionDetectionMode CollisionDetectionModeBeforeOriginShift;
            public CollisionDetectionMode CollisionDetectionModeBeforeSafeTeleport;
            public int SafeTeleportSpeculativeFixedTicksRemaining;
            public Vector3 SnapshotPositionBeforeOriginShift;
            public Quaternion SnapshotRotationBeforeOriginShift;
            public Vector3 LastValidLinearVelocity;
            public Vector3 LastValidAngularVelocity;
            public AbsoluteUniversePosition LastValidAup;
            public IPhysicsColliderLodHysteresisSink ColliderLodSink;
            public byte ImpactAudioMaterialId;
            public Vector3 BaseInertiaTensor;
            public Quaternion BaseInertiaTensorRotation;
            public float BaseAngularDamping;
            public float HydrodynamicSubmersionFactor;
            public float LastAppliedAddedMassSubmersionFactor;
            public float FixedInterpolationAlphaBeforeOriginShift;
            public float ColliderLodOutOfRangeSeconds;
            public bool HasColliderLodSink;
            public bool ColliderLodDistanceGateOpen;
            public bool IsFullySubmerged;
            public bool HasAddedMassBaseline;
            public bool AddedMassTensorApplied;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PhysicsConnection
        {
            public UnityEngine.Object Owner;
            public Rigidbody BodyA;
            public Rigidbody BodyB;
            public Rigidbody CompensatedBody;
            public PhysicsConnectionKind Kind;
            public bool CompensationActive;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PhysicsImpactEventData
        {
            public ulong PrimaryBodyId;
            public ulong SecondaryBodyId;
            public float Force;
            public float Intensity;
            public float MassVelocity;
            public float3 Point;
            public AbsoluteUniversePosition PointAup;
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
        private const float AddedMassFullySubmergedThreshold = 0.999f;
        private const float AddedMassFullySubmergedAngularDampingMultiplier = 1f + AddedMassAngularDampingScale;
        private const float AddedMassFullySubmergedInertiaTensorMultiplier = 1f + AddedMassInertiaTensorScale;
        private const float AddedMassSubmersionEpsilon = 0.0001f;
        private const float PhysicsFixedStepSeconds = 0.02f;
        private const float InverseTwoPi = 0.15915494309189535f;
        private const float OriginShiftContinuousCcdSpeedMetersPerSecond = 20f;
        private const float OriginShiftContinuousCcdSpeedMetersPerSecondSq =
            OriginShiftContinuousCcdSpeedMetersPerSecond * OriginShiftContinuousCcdSpeedMetersPerSecond;
        private const float KineticAnomalyAccelerationMetersPerSecondSq = 100f;
        private const float AupJitterThresholdMeters = 0.05f;
        private const float AupJitterThresholdMetersSq = AupJitterThresholdMeters * AupJitterThresholdMeters;
        private const int AupJitterSentinelFrameInterval = 60;
        private const int SafeTeleportSpeculativeFixedTickHold = 3;
        private const double FarKinematicSleepDistanceSq = FarKinematicSleepDistanceMeters * FarKinematicSleepDistanceMeters;
        private const double ColliderLodCompoundToSimpleDistanceSq = ColliderLodCompoundToSimpleDistanceMeters * ColliderLodCompoundToSimpleDistanceMeters;
        private const double ColliderLodSimpleToCompoundDistanceSq = ColliderLodSimpleToCompoundDistanceMeters * ColliderLodSimpleToCompoundDistanceMeters;
        private static readonly uint _nanRecoverySystemHash = unchecked((uint)LocHash.Compute(nameof(GlobalPhysicsStateManager)));

        // COLD ALLOC: Rigidbody[512 initial] â€” authoritative tracked rigidbody registry â€” owner: GlobalPhysicsStateManager
        private Rigidbody[] _trackedBodies = new Rigidbody[MaxTrackedBodies];
        // COLD ALLOC: RigidbodyState[512 initial] â€” per-body runtime state and compensation flags â€” owner: GlobalPhysicsStateManager
        private RigidbodyState[] _bodyStates = new RigidbodyState[MaxTrackedBodies];
        // COLD ALLOC: PhysicsConnection[128] â€” tracked tether/dock connection registry â€” owner: GlobalPhysicsStateManager
        private readonly PhysicsConnection[] _connections = new PhysicsConnection[MaxTrackedConnections];
        // COLD ALLOC: Dictionary<ulong,int>[512 initial] â€” rigidbody entity-id to tracked-index map for O(1) lookups during origin shifts â€” owner: GlobalPhysicsStateManager
        private readonly Dictionary<ulong, int> _trackedBodyIndexByEntityId = new Dictionary<ulong, int>(MaxTrackedBodies);
        // COLD ALLOC: List<GameObject>[128] — scene-load root scratch for rigidbody registry bootstrap without scene-wide array allocation — owner: GlobalPhysicsStateManager
        private readonly List<GameObject> _sceneRootScratch = new List<GameObject>(SceneRootScanCapacity);
        // COLD ALLOC: List<Rigidbody>[512] — scene-load rigidbody scratch for registry bootstrap without scene-wide array allocation — owner: GlobalPhysicsStateManager
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
        private bool _registeredPostFixedTick;
        private bool _registeredOriginShift;
        private bool _sceneEventsSubscribed;
        private bool _connectionCapacityOverflowReported;
        private bool _trackedBodyCapacityOverflowReported;
        private float _lastFixedDeltaTime = PhysicsFixedStepSeconds;
        private int _lastKineticAnomalyFrame = -1;
        private static int _cachedWaterLevelFrame = -1;
        private static float _cachedWaterLevelBaseY;
        private static float _cachedWaterLevelAmplitude;
        private static bool _cachedWaterLevelTidesEnabled;
        private static float _cachedCurrentWaterLevelY;

        /// <summary>
        /// Frame-stable cinematic water level. Consumers read this instead of recomputing tide sine waves.
        /// </summary>
        public static float CachedCurrentWaterLevelY => _cachedCurrentWaterLevelY;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _isInitialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _cachedWaterLevelFrame = -1;
            _cachedWaterLevelBaseY = 0f;
            _cachedWaterLevelAmplitude = 0f;
            _cachedWaterLevelTidesEnabled = false;
            _cachedCurrentWaterLevelY = 0f;
        }

        public static float ResolveFrameCachedCurrentWaterLevelY(
            float baseWaterLevelY,
            bool tidesEnabled,
            float tideAmplitudeMeters,
            float timeSeconds)
        {
            int frame = Time.frameCount;
            float safeAmplitude = math.max(0f, tideAmplitudeMeters);
            if (_cachedWaterLevelFrame == frame &&
                math.abs(_cachedWaterLevelBaseY - baseWaterLevelY) <= 0.0001f &&
                math.abs(_cachedWaterLevelAmplitude - safeAmplitude) <= 0.0001f &&
                _cachedWaterLevelTidesEnabled == tidesEnabled)
            {
                return _cachedCurrentWaterLevelY;
            }

            float resolvedWaterLevelY = baseWaterLevelY;
            if (tidesEnabled && safeAmplitude > 0f)
            {
                float combinedWave = ResolveSignedTriangleWave(timeSeconds) + ResolveSignedTriangleWave(timeSeconds * 0.5f);
                resolvedWaterLevelY += combinedWave * safeAmplitude;
            }

            _cachedWaterLevelFrame = frame;
            _cachedWaterLevelBaseY = baseWaterLevelY;
            _cachedWaterLevelAmplitude = safeAmplitude;
            _cachedWaterLevelTidesEnabled = tidesEnabled;
            _cachedCurrentWaterLevelY = resolvedWaterLevelY;
            return resolvedWaterLevelY;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveSignedTriangleWave(float radians)
        {
            float phase = (radians * InverseTwoPi) - 0.25f;
            phase -= math.floor(phase);
            return (2f * math.abs((2f * phase) - 1f)) - 1f;
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

        internal static void ArmSafeTeleportSpeculativeCcdForSafeTeleport()
        {
            if (!TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return;

            manager.ArmSafeTeleportSpeculativeCcdForSafeTeleportInternal();
        }

        internal static void ArmSpeculativeCcdForImpulse(Rigidbody body)
        {
            if (body == null || !TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return;

            manager.ArmSafeTeleportSpeculativeCcd(body);
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
            EnsureNativeState();
        }

        /// <summary>
        /// Registers this manager as the authoritative global physics-state owner.
        /// </summary>
        public void InitializeService()
        {
            EnsureNativeState();

            if (_isInitialized)
            {
                TryRegisterService();
                TryRegisterFixedTick();
                TryRegisterLateFrameTick();
                TryRegisterPostFixedTick();
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
            TryRegisterLateFrameTick();
            TryRegisterPostFixedTick();
            TryRegisterOriginShift();
        }

        private void OnEnable()
        {
            EnsureNativeState();

            if (!_isInitialized)
                return;

            TryRegisterFixedTick();
            TryRegisterLateFrameTick();
            TryRegisterPostFixedTick();
            TryRegisterOriginShift();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            FlushImpactEvents();
        }

        /// <inheritdoc />
        public void PostFixedTick(float fixedDeltaTime)
        {
            ApplyAupJitterSentinel();
            TickSafeTeleportSpeculativeCcdGuards();
        }

        private void OnDisable()
        {
            UnregisterRuntimeHooks();
        }

        private void EnsureNativeState()
        {
            if (!_lastValidPositions.IsCreated)
            {
                // COLD ALLOC: NativeArray<float3>[512] - authoritative last-valid runtime-space body positions for origin-shift-safe recovery - owner: GlobalPhysicsStateManager
                _lastValidPositions = new NativeArray<float3>(MaxTrackedBodies, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    _lastValidPositions,
                    nameof(GlobalPhysicsStateManager),
                    nameof(_lastValidPositions),
                    NativeAllocationLifetime.Session);
            }

            if (!_impactQueue.IsCreated)
            {
                // COLD ALLOC: NativeQueue<PhysicsImpactEventData>[128] - deferred gameplay physics impact bus - owner: GlobalPhysicsStateManager
                _impactQueue = new NativeQueue<PhysicsImpactEventData>(Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeQueue(
                    _impactQueue,
                    MaxQueuedImpactEvents,
                    nameof(GlobalPhysicsStateManager),
                    nameof(_impactQueue),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _impactQueue, MaxQueuedImpactEvents);
            }
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            UnregisterRuntimeHooks();
            UnsubscribeSceneEvents();
            ClearRuntimeState();

            if (_impactQueue.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(GlobalPhysicsStateManager), nameof(_impactQueue));
                _impactQueue.Dispose();
                _impactQueue = default;
            }

            if (_lastValidPositions.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_lastValidPositions);
                _lastValidPositions.Dispose();
                _lastValidPositions = default;
            }

            TryUnregisterService();
            _isInitialized = false;
        }

        private void UnregisterRuntimeHooks()
        {
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

            if (_registeredPostFixedTick)
            {
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Core);
                _registeredPostFixedTick = false;
            }

            if (_registeredOriginShift)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShift = false;
            }
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            _lastFixedDeltaTime = SanitizeFixedStepDelta(fixedDeltaTime);
            RefreshTrackedBodies(_lastFixedDeltaTime);
            SweepNaNPhysicsState();
            EvaluateConnections();
            ApplyDistanceKinematicSleepInternal();
            ApplyColliderLodHysteresisInternal(_lastFixedDeltaTime);
            ApplyAddedMassTensorState();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeFixedStepDelta(float fixedDeltaTime)
        {
            return fixedDeltaTime > 0f && math.isfinite(fixedDeltaTime)
                ? fixedDeltaTime
                : PhysicsFixedStepSeconds;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveImpactFixedDeltaTime()
        {
            return math.max(_lastFixedDeltaTime, 0.0001f);
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
            _serviceRegistered = ReferenceEquals(GlobalRegistry.PhysicsStateManager, this);
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

        private void TryRegisterPostFixedTick()
        {
            if (_registeredPostFixedTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterPostFixedTickable(this, PriorityLayer.Core);
            _registeredPostFixedTick = SystemDispatcher
                .GetPostFixedLane(PriorityLayer.Core)
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
                Vector3 linearVelocity = body.linearVelocity;
                Vector3 angularVelocity = body.angularVelocity;
                CollisionDetectionMode collisionDetectionMode = body.collisionDetectionMode;
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

                bodyState.LastValidLinearVelocity = IsFinite(linearVelocity) ? linearVelocity : Vector3.zero;
                bodyState.LastValidAngularVelocity = IsFinite(angularVelocity) ? angularVelocity : Vector3.zero;
                bodyState.FixedInterpolationAlphaBeforeOriginShift = HectonFloatingOrigin.CurrentFixedInterpolationAlpha;
                bodyState.WasSleepingBeforeOriginShift = body.IsSleeping();
                bodyState.InterpolationModeBeforeOriginShift = body.interpolation;
                bodyState.InterpolationSuspendedForOriginShift = body.interpolation != RigidbodyInterpolation.None;
                if (bodyState.InterpolationSuspendedForOriginShift)
                    body.interpolation = RigidbodyInterpolation.None;
                bodyState.CollisionDetectionModeBeforeOriginShift = collisionDetectionMode;
                float speedSq = bodyState.LastValidLinearVelocity.sqrMagnitude;
                bodyState.CollisionDetectionOverriddenForOriginShift =
                    speedSq > OriginShiftContinuousCcdSpeedMetersPerSecondSq &&
                    collisionDetectionMode != CollisionDetectionMode.Continuous &&
                    collisionDetectionMode != CollisionDetectionMode.ContinuousDynamic;
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

                TeleportBodyWithoutBroadphaseImpulse(
                    body,
                    targetPosition,
                    targetRotation,
                    linearVelocity,
                    angularVelocity,
                    bodyState.WasSleepingBeforeOriginShift);

                _lastValidPositions[i] = new float3(targetPosition.x, targetPosition.y, targetPosition.z);
                bodyState.HasLastValidPosition = true;
                bodyState.LastValidAup = AbsoluteUniversePosition.FromRuntimePosition(targetPosition);
                bodyState.HasLastValidAup = true;
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
                        if (!bodyState.SafeTeleportSpeculativeCcdActive)
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
                    if (!bodyState.SafeTeleportSpeculativeCcdActive)
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
                bool wasSleeping = body.IsSleeping();
                Vector3 bodyPosition = body.position;
                bool hasFinitePosition = IsFinite(bodyPosition);
                Vector3 position = hasFinitePosition ? bodyPosition : Vector3.zero;
                Vector3 linearVelocity = Vector3.zero;
                Vector3 angularVelocity = Vector3.zero;

                body.ResetCenterOfMass();
                TeleportBodyWithoutBroadphaseImpulse(
                    body,
                    position,
                    IsFinite(body.rotation) ? body.rotation : Quaternion.identity,
                    linearVelocity,
                    angularVelocity,
                    wasSleeping);

                _lastValidPositions[i] = new float3(position.x, position.y, position.z);
                bodyState.LastValidAup = AbsoluteUniversePosition.FromRuntimePosition(position);
                bodyState.HasLastValidPosition = true;
                bodyState.HasLastValidAup = true;

                bodyState.LastValidLinearVelocity = linearVelocity;
                bodyState.LastValidAngularVelocity = angularVelocity;
                bodyState.HasOriginShiftSnapshot = false;
                bodyState.FixedInterpolationAlphaBeforeOriginShift = 0f;
                bodyState.InterpolationSuspendedForOriginShift = false;
                bodyState.CollisionDetectionOverriddenForOriginShift = false;
                if (wasSleeping)
                    bodyState.StateMask |= PhysicsStateMask.WasAsleep;
                else
                    bodyState.StateMask &= ~PhysicsStateMask.WasAsleep;

                _bodyStates[i] = bodyState;
            }
        }

        private void ArmSafeTeleportSpeculativeCcdForSafeTeleportInternal()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Rigidbody playerBody = playerContext != null ? playerContext.PlayerRigidbody : null;
            ArmSafeTeleportSpeculativeCcd(playerBody);

            ISubmarineRuntimeContext submarineContext = GlobalRegistry.Submarine;
            Rigidbody hullBody = submarineContext != null ? submarineContext.HullRigidbody : null;
            if (!ReferenceEquals(hullBody, playerBody))
                ArmSafeTeleportSpeculativeCcd(hullBody);

            ArmFastTrackedBodiesForSafeTeleportSpeculativeCcd(playerBody, hullBody);
        }

        private void ArmFastTrackedBodiesForSafeTeleportSpeculativeCcd(Rigidbody playerBody, Rigidbody hullBody)
        {
            for (int i = _trackedBodyCount - 1; i >= 0; i--)
            {
                Rigidbody body = _trackedBodies[i];
                if (body == null)
                {
                    RemoveTrackedBodyAt(i);
                    continue;
                }

                if (ReferenceEquals(body, playerBody) || ReferenceEquals(body, hullBody))
                    continue;

                RigidbodyState bodyState = _bodyStates[i];
                Vector3 linearVelocity = IsFinite(body.linearVelocity)
                    ? body.linearVelocity
                    : bodyState.LastValidLinearVelocity;
                if (!IsFinite(linearVelocity) || linearVelocity.sqrMagnitude <= OriginShiftContinuousCcdSpeedMetersPerSecondSq)
                    continue;

                ArmSafeTeleportSpeculativeCcd(body);
            }
        }

        private void ArmSafeTeleportSpeculativeCcd(Rigidbody body)
        {
            if (body == null)
                return;

            RegisterTrackedBodyInternal(body);
            int bodyIndex = FindTrackedBodyIndex(body);
            if (bodyIndex < 0)
                return;

            RigidbodyState bodyState = _bodyStates[bodyIndex];
            if (!bodyState.SafeTeleportSpeculativeCcdActive)
            {
                bodyState.CollisionDetectionModeBeforeSafeTeleport = bodyState.CollisionDetectionOverriddenForOriginShift
                    ? bodyState.CollisionDetectionModeBeforeOriginShift
                    : body.collisionDetectionMode;
            }

            bodyState.SafeTeleportSpeculativeCcdActive = true;
            bodyState.CollisionDetectionOverriddenForOriginShift = false;
            bodyState.SafeTeleportSpeculativeFixedTicksRemaining = SafeTeleportSpeculativeFixedTickHold;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.PublishTransform();
            _bodyStates[bodyIndex] = bodyState;
        }

        private void TickSafeTeleportSpeculativeCcdGuards()
        {
            for (int i = _trackedBodyCount - 1; i >= 0; i--)
            {
                RigidbodyState bodyState = _bodyStates[i];
                if (!bodyState.SafeTeleportSpeculativeCcdActive)
                    continue;

                Rigidbody body = _trackedBodies[i];
                if (body == null)
                {
                    RemoveTrackedBodyAt(i);
                    continue;
                }

                if (bodyState.SafeTeleportSpeculativeFixedTicksRemaining > 0)
                {
                    bodyState.SafeTeleportSpeculativeFixedTicksRemaining--;
                    if (bodyState.SafeTeleportSpeculativeFixedTicksRemaining > 0)
                    {
                        _bodyStates[i] = bodyState;
                        continue;
                    }
                }

                RestoreSafeTeleportSpeculativeCcd(body, ref bodyState);
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

            if (!EnsureTrackedBodyCapacity(_trackedBodyCount + 1))
                return;

            EnsureReporter(body);
            IPhysicsColliderLodHysteresisSink colliderLodSink = ResolveColliderLodSink(body);
            Vector3 bodyPosition = body.position;
            Vector3 bodyLinearVelocity = body.linearVelocity;
            Vector3 bodyAngularVelocity = body.angularVelocity;
            Vector3 bodyInertiaTensor = body.inertiaTensor;
            Quaternion bodyInertiaTensorRotation = body.inertiaTensorRotation;
            bool hasFiniteBodyPosition = IsFinite(bodyPosition);

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
                HasLastValidPosition = hasFiniteBodyPosition,
                HasLastValidAup = hasFiniteBodyPosition,
                LastValidAup = hasFiniteBodyPosition ? AbsoluteUniversePosition.FromRuntimePosition(bodyPosition) : default,
                KinematicModeBeforeDistanceSleep = body.isKinematic,
                DetectCollisionsBeforeDistanceSleep = body.detectCollisions,
                LastValidLinearVelocity = IsFinite(bodyLinearVelocity) ? bodyLinearVelocity : Vector3.zero,
                LastValidAngularVelocity = IsFinite(bodyAngularVelocity) ? bodyAngularVelocity : Vector3.zero,
                ColliderLodSink = colliderLodSink,
                ImpactAudioMaterialId = ResolveImpactAudioMaterialIdUncached(body),
                HasColliderLodSink = IsColliderLodSinkAlive(colliderLodSink),
                ColliderLodDistanceGateOpen = false,
                ColliderLodOutOfRangeSeconds = 0f,
                BaseInertiaTensor = IsFinite(bodyInertiaTensor) ? bodyInertiaTensor : Vector3.one,
                BaseInertiaTensorRotation = IsFinite(bodyInertiaTensorRotation) ? bodyInertiaTensorRotation : Quaternion.identity,
                BaseAngularDamping = math.max(0f, body.angularDamping),
                HydrodynamicSubmersionFactor = 0f,
                HasAddedMassBaseline = true
            };
            ApplyTrackedBodyAngularVelocityClamp(body, _bodyStates[bodyIndex].MaxAngularVelocityClamp);
            _trackedBodyIndexByEntityId[bodyEntityId] = bodyIndex;
            _lastValidPositions[bodyIndex] = hasFiniteBodyPosition
                ? new float3(bodyPosition.x, bodyPosition.y, bodyPosition.z)
                : float3.zero;
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
            bodyState.IsFullySubmerged = bodyState.HydrodynamicSubmersionFactor >= AddedMassFullySubmergedThreshold;
            _bodyStates[bodyIndex] = bodyState;
        }

        private void QueueImpactInternal(Rigidbody primaryBody, Rigidbody secondaryBody, Collision collision)
        {
            if (primaryBody == null ||
                !PhysicsEvents.HasImpactListeners ||
                HectonFloatingOrigin.IsShiftInProgress ||
                !_impactQueue.IsCreated ||
                _queuedImpactCount >= MaxQueuedImpactEvents)
            {
                return;
            }

            float fixedDelta = ResolveImpactFixedDeltaTime();
            float minImpactImpulse = MinImpactForce * fixedDelta;
            float impulseSq = collision.impulse.sqrMagnitude;
            if (!(impulseSq > minImpactImpulse * minImpactImpulse))
                return;

            float impactForce = EstimateMagnitudeNoSqrt(impulseSq) / fixedDelta;
            float massVelocity = ResolveImpactMassVelocity(primaryBody, EstimateMagnitudeNoSqrt(collision.relativeVelocity.sqrMagnitude));
            float impactIntensity = ResolveImpactIntensityFromForce(impactForce);
            if (!(impactIntensity > 0f))
                return;

            bool hasContact = collision.contactCount > 0;
            ContactPoint contact = hasContact ? collision.GetContact(0) : default;
            Vector3 fallbackPoint = primaryBody.worldCenterOfMass;
            Vector3 point = hasContact ? contact.point : fallbackPoint;
            Vector3 normal = hasContact && contact.normal.sqrMagnitude > 0.000001f ? contact.normal : Vector3.up;
            float3 point3 = new float3(point.x, point.y, point.z);
            float3 normal3 = new float3(normal.x, normal.y, normal.z);
            if (!math.all(math.isfinite(point3)))
                point3 = new float3(fallbackPoint.x, fallbackPoint.y, fallbackPoint.z);
            float normalSq = math.lengthsq(normal3);
            if (!math.all(math.isfinite(normal3)) || normalSq <= 0.000001f)
                normal3 = new float3(0f, 1f, 0f);
            else
                normal3 *= math.rsqrt(normalSq);
            AbsoluteUniversePosition pointAup = AbsoluteUniversePosition.FromRuntimePosition(new Vector3(point3.x, point3.y, point3.z));
            PhysicsImpactWeightClass weightClass = ResolveImpactWeightClass(impactIntensity);

            _impactQueue.Enqueue(new PhysicsImpactEventData
            {
                PrimaryBodyId = EntityId.ToULong(primaryBody.GetEntityId()),
                SecondaryBodyId = secondaryBody != null ? EntityId.ToULong(secondaryBody.GetEntityId()) : 0ul,
                Force = impactForce,
                Intensity = impactIntensity,
                MassVelocity = massVelocity,
                Point = point3,
                PointAup = pointAup,
                Normal = normal3,
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
            if (primaryBody == null ||
                !PhysicsEvents.HasImpactListeners ||
                HectonFloatingOrigin.IsShiftInProgress ||
                !_impactQueue.IsCreated ||
                _queuedImpactCount >= MaxQueuedImpactEvents)
            {
                return;
            }

            float safeImpactSpeed = math.max(0f, impactSpeedMetersPerSecond);
            if (!(safeImpactSpeed > 0.0001f))
                return;

            float fixedDelta = ResolveImpactFixedDeltaTime();
            float effectiveMass = math.max(primaryBody.mass, MinMass);
            float impactForce = (effectiveMass * safeImpactSpeed) / fixedDelta;
            if (!(impactForce > MinImpactForce))
                return;

            float massVelocity = ResolveImpactMassVelocity(primaryBody, safeImpactSpeed);
            float3 point3 = new float3(point.x, point.y, point.z);
            float3 normal3 = new float3(normal.x, normal.y, normal.z);
            if (!math.all(math.isfinite(point3)))
                point3 = (float3)primaryBody.worldCenterOfMass;
            float normalSq = math.lengthsq(normal3);
            if (!math.all(math.isfinite(normal3)) || normalSq <= 0.000001f)
                normal3 = new float3(0f, 1f, 0f);
            else
                normal3 *= math.rsqrt(normalSq);
            AbsoluteUniversePosition pointAup = AbsoluteUniversePosition.FromRuntimePosition(new Vector3(point3.x, point3.y, point3.z));

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
                PointAup = pointAup,
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
                   processedCount < MaxImpactFlushIterations)
            {
                if (!_impactQueue.TryDequeue(out PhysicsImpactEventData impactEvent))
                {
                    _queuedImpactCount = 0;
                    break;
                }

                _queuedImpactCount--;
                processedCount++;
                Vector3 impactPoint = new Vector3(impactEvent.Point.x, impactEvent.Point.y, impactEvent.Point.z);
                Vector3 impactNormal = new Vector3(impactEvent.Normal.x, impactEvent.Normal.y, impactEvent.Normal.z);
                PhysicsEvents.RaiseImpact(new PhysicsImpactSignal(
                    impactEvent.PrimaryBodyId,
                    impactEvent.SecondaryBodyId,
                    impactPoint,
                    in impactEvent.PointAup,
                    impactNormal,
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

        private byte ResolveImpactAudioMaterialId(Rigidbody body)
        {
            int bodyIndex = FindTrackedBodyIndex(body);
            if (bodyIndex >= 0)
                return _bodyStates[bodyIndex].ImpactAudioMaterialId;

            return 0;
        }

        private static byte ResolveImpactAudioMaterialIdUncached(Rigidbody body)
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
                    if (!_connectionCapacityOverflowReported)
                    {
                        Debug.LogWarning("[GlobalPhysicsStateManager] Connection registry capacity exceeded.");
                        _connectionCapacityOverflowReported = true;
                    }
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
                Vector3 bodyLinearVelocity = body.linearVelocity;
                Vector3 currentLinearVelocity = IsFinite(bodyLinearVelocity) ? bodyLinearVelocity : Vector3.zero;
                if (!HectonFloatingOrigin.IsShiftInProgress && IsFinite(bodyState.LastValidLinearVelocity))
                {
                    Vector3 deltaVelocity = currentLinearVelocity - bodyState.LastValidLinearVelocity;
                    float anomalyDeltaVelocity = KineticAnomalyAccelerationMetersPerSecondSq * safeDeltaTime;
                    float deltaVelocitySq = deltaVelocity.sqrMagnitude;
                    if (deltaVelocitySq > anomalyDeltaVelocity * anomalyDeltaVelocity)
                    {
                        float acceleration = EstimateMagnitudeNoSqrt(deltaVelocitySq) / safeDeltaTime;
                        ReportKineticAnomalyOncePerFrame(bodyPosition, deltaVelocity, acceleration);
                    }
                }

                bodyState.HasLastValidPosition = true;
                bodyState.LastValidAup = AbsoluteUniversePosition.FromRuntimePosition(bodyPosition);
                bodyState.HasLastValidAup = true;
                bodyState.LastValidLinearVelocity = currentLinearVelocity;
                Vector3 bodyAngularVelocity = body.angularVelocity;
                bodyState.LastValidAngularVelocity = IsFinite(bodyAngularVelocity) ? bodyAngularVelocity : Vector3.zero;
                _bodyStates[i] = bodyState;
                _lastValidPositions[i] = new float3(bodyPosition.x, bodyPosition.y, bodyPosition.z);
            }
        }

        private void ApplyAupJitterSentinel()
        {
            if ((Time.frameCount % AupJitterSentinelFrameInterval) != 0 ||
                !_lastValidPositions.IsCreated ||
                _trackedBodyCount <= 0 ||
                HectonFloatingOrigin.IsShiftInProgress)
                return;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Rigidbody playerBody = playerContext != null ? playerContext.PlayerRigidbody : null;
            ApplyAupJitterSentinelForBody(playerBody);

            ISubmarineRuntimeContext submarineContext = GlobalRegistry.Submarine;
            Rigidbody submarineBody = submarineContext != null ? submarineContext.HullRigidbody : null;
            if (submarineBody != null && !ReferenceEquals(submarineBody, playerBody))
                ApplyAupJitterSentinelForBody(submarineBody);
        }

        private void ApplyAupJitterSentinelForBody(Rigidbody body)
        {
            if (body == null || !body.isKinematic)
                return;

            int bodyIndex = FindTrackedBodyIndex(body);
            if (bodyIndex < 0)
                return;

            Rigidbody trackedBody = _trackedBodies[bodyIndex];
            if (trackedBody == null)
            {
                RemoveTrackedBodyAt(bodyIndex);
                return;
            }

            RigidbodyState bodyState = _bodyStates[bodyIndex];
            if (!bodyState.HasLastValidAup)
                return;

            Vector3 bodyPosition = trackedBody.position;
            if (!IsFinite(bodyPosition))
                return;

            float3 aupRuntimePosition3 = bodyState.LastValidAup.ToRuntimeFloat3();
            Vector3 aupRuntimePosition = new Vector3(
                aupRuntimePosition3.x,
                aupRuntimePosition3.y,
                aupRuntimePosition3.z);
            if (!IsFinite(aupRuntimePosition))
                return;

            Vector3 correctionDelta = aupRuntimePosition - bodyPosition;
            float correctionSq = correctionDelta.sqrMagnitude;
            if (correctionSq <= AupJitterThresholdMetersSq)
                return;

            Vector3 trackedLinearVelocity = trackedBody.linearVelocity;
            Vector3 trackedAngularVelocity = trackedBody.angularVelocity;
            Vector3 linearVelocity = IsFinite(trackedLinearVelocity) ? trackedLinearVelocity : Vector3.zero;
            Vector3 angularVelocity = IsFinite(trackedAngularVelocity) ? trackedAngularVelocity : Vector3.zero;
            HectonFloatingOrigin.ResyncBody(trackedBody, in bodyState.LastValidAup);

            _lastValidPositions[bodyIndex] = aupRuntimePosition3;
            bodyState.HasLastValidPosition = true;
            bodyState.LastValidAup = AbsoluteUniversePosition.FromRuntimePosition(aupRuntimePosition);
            bodyState.HasLastValidAup = true;
            bodyState.LastValidLinearVelocity = linearVelocity;
            bodyState.LastValidAngularVelocity = angularVelocity;
            _bodyStates[bodyIndex] = bodyState;

            CrashTelemetryBuffer.ReportAupJitterCorrection(bodyPosition, EstimateMagnitudeNoSqrt(correctionSq));
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

                Vector3 bodyPosition = body.position;
                Quaternion bodyRotation = body.rotation;
                Vector3 bodyLinearVelocity = body.linearVelocity;
                Vector3 bodyAngularVelocity = body.angularVelocity;
                float3 position = new float3(bodyPosition.x, bodyPosition.y, bodyPosition.z);
                float4 rotation = new float4(bodyRotation.x, bodyRotation.y, bodyRotation.z, bodyRotation.w);
                float3 linearVelocity = new float3(bodyLinearVelocity.x, bodyLinearVelocity.y, bodyLinearVelocity.z);
                float3 angularVelocity = new float3(bodyAngularVelocity.x, bodyAngularVelocity.y, bodyAngularVelocity.z);

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
                Vector3 invalidRuntimePosition = new Vector3(position.x, position.y, position.z);
                Vector3 recoveredRuntimePosition = RuntimeWatchdog.ReportRigidbodyNanRecovery(
                    _nanRecoverySystemHash,
                    invalidRuntimePosition,
                    new Vector3(lastValidPosition.x, lastValidPosition.y, lastValidPosition.z));
                lastValidPosition = new float3(
                    recoveredRuntimePosition.x,
                    recoveredRuntimePosition.y,
                    recoveredRuntimePosition.z);

                Quaternion recoveredRotation = math.any(rotationNaNMask) ? Quaternion.identity : body.rotation;
                TeleportBodyWithoutBroadphaseImpulse(
                    body,
                    recoveredRuntimePosition,
                    recoveredRotation,
                    Vector3.zero,
                    Vector3.zero,
                    true);
                bodyState.LastValidLinearVelocity = Vector3.zero;
                bodyState.LastValidAngularVelocity = Vector3.zero;
                bodyState.HasLastValidPosition = true;
                bodyState.LastValidAup = AbsoluteUniversePosition.FromRuntimePosition(recoveredRuntimePosition);
                bodyState.HasLastValidAup = true;
                _lastValidPositions[i] = lastValidPosition;
                _bodyStates[i] = bodyState;
            }
        }

        private static void TeleportBodyWithoutBroadphaseImpulse(
            Rigidbody body,
            Vector3 targetPosition,
            Quaternion targetRotation,
            Vector3 linearVelocity,
            Vector3 angularVelocity,
            bool sleepAfter)
        {
            if (body == null || !IsFinite(targetPosition) || !IsFinite(targetRotation))
                return;

            bool wasKinematic = body.isKinematic;
            bool wasDetectingCollisions = body.detectCollisions;
            body.isKinematic = true;
            body.detectCollisions = false;
            body.transform.SetPositionAndRotation(targetPosition, targetRotation);
            body.PublishTransform();
            body.isKinematic = wasKinematic;
            body.detectCollisions = wasDetectingCollisions;

            if (!wasKinematic)
            {
                body.linearVelocity = IsFinite(linearVelocity) ? linearVelocity : Vector3.zero;
                body.angularVelocity = IsFinite(angularVelocity) ? angularVelocity : Vector3.zero;
            }

            if (!wasKinematic)
            {
                if (sleepAfter)
                    body.Sleep();
                else
                    body.WakeUp();
            }

            body.PublishTransform();
        }

        private void ApplyDistanceKinematicSleepInternal()
        {
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return;

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

                if (!TryResolveTrackedBodyAup(body, ref bodyState, out AbsoluteUniversePosition bodyAup))
                {
                    _bodyStates[i] = bodyState;
                    continue;
                }

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
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            float safeDeltaTime = math.max(0f, fixedDeltaTime);
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

                if (!TryResolveTrackedBodyAup(body, ref bodyState, out AbsoluteUniversePosition bodyAup))
                {
                    _bodyStates[i] = bodyState;
                    continue;
                }

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
                if (submersionFactor <= AddedMassSubmersionEpsilon)
                {
                    if (bodyState.AddedMassTensorApplied)
                        RestoreAddedMassBaseline(body, ref bodyState);

                    _bodyStates[i] = bodyState;
                    continue;
                }

                if (bodyState.AddedMassTensorApplied &&
                    math.abs(bodyState.LastAppliedAddedMassSubmersionFactor - submersionFactor) <= AddedMassSubmersionEpsilon)
                {
                    _bodyStates[i] = bodyState;
                    continue;
                }

                bool isFullySubmerged = bodyState.IsFullySubmerged;
                float multiplier = isFullySubmerged
                    ? AddedMassFullySubmergedAngularDampingMultiplier
                    : 1f + (AddedMassAngularDampingScale * submersionFactor);
                float inertiaMultiplier = isFullySubmerged
                    ? AddedMassFullySubmergedInertiaTensorMultiplier
                    : 1f + (AddedMassInertiaTensorScale * submersionFactor);
                body.angularDamping = bodyState.BaseAngularDamping * multiplier;
                body.inertiaTensor = bodyState.BaseInertiaTensor * inertiaMultiplier;
                body.inertiaTensorRotation = bodyState.BaseInertiaTensorRotation;
                bodyState.AddedMassTensorApplied = true;
                bodyState.LastAppliedAddedMassSubmersionFactor = submersionFactor;
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
            bodyState.LastAppliedAddedMassSubmersionFactor = 0f;
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
                RestoreSafeTeleportSpeculativeCcd(removedBody, ref removedState);
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

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            if (playerMovement != null)
            {
                playerAup = playerMovement.CurrentAup;
                return true;
            }

            playerAup = default;
            return false;
        }

        private static bool TryResolveTrackedBodyAup(Rigidbody body, ref RigidbodyState bodyState, out AbsoluteUniversePosition bodyAup)
        {
            if (bodyState.HasLastValidAup)
            {
                bodyAup = bodyState.LastValidAup;
                return true;
            }

            if (body == null)
            {
                bodyAup = default;
                return false;
            }

            Vector3 position = body.position;
            if (!IsFinite(position))
            {
                bodyAup = default;
                return false;
            }

            bodyAup = AbsoluteUniversePosition.FromRuntimePosition(position);
            bodyState.LastValidAup = bodyAup;
            bodyState.HasLastValidAup = true;
            return true;
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

        private static void RestoreSafeTeleportSpeculativeCcd(Rigidbody body, ref RigidbodyState bodyState)
        {
            if (!bodyState.SafeTeleportSpeculativeCcdActive)
                return;

            if (body != null)
            {
                body.collisionDetectionMode = bodyState.CollisionDetectionModeBeforeSafeTeleport;
                body.PublishTransform();
            }

            bodyState.SafeTeleportSpeculativeCcdActive = false;
            bodyState.SafeTeleportSpeculativeFixedTicksRemaining = 0;
            bodyState.CollisionDetectionModeBeforeSafeTeleport = default;
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

        private bool EnsureTrackedBodyCapacity(int requiredCount)
        {
            if (requiredCount <= MaxTrackedBodies)
                return true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_trackedBodyCapacityOverflowReported)
            {
                Debug.LogError("[GlobalPhysicsStateManager] MaxTrackedBodies capacity exceeded. Increase MaxTrackedBodies; runtime buffer growth is forbidden.");
                _trackedBodyCapacityOverflowReported = true;
            }
#endif
            return false;
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
                if (!EnsureTrackedBodyCapacity(_trackedBodyCount + bodyCount))
                {
                    bodyCount = MaxTrackedBodies - _trackedBodyCount;
                    if (bodyCount <= 0)
                        return;
                }

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
                RestoreSafeTeleportSpeculativeCcd(body, ref bodyState);
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
            _connectionCapacityOverflowReported = false;
            _trackedBodyCapacityOverflowReported = false;
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

        private static float EstimateMagnitudeNoSqrt(float valueSq)
        {
            if (!(valueSq > 0f))
                return 0f;

            float estimate =
                valueSq > 4096f ? valueSq * 0.015625f :
                valueSq > 256f ? valueSq * 0.0625f :
                valueSq > 16f ? valueSq * 0.25f :
                valueSq > 1f ? valueSq :
                valueSq > 0.0625f ? 0.5f :
                0.125f;

            estimate = RefineMagnitudeEstimate(valueSq, estimate);
            estimate = RefineMagnitudeEstimate(valueSq, estimate);
            return estimate;
        }

        private static float RefineMagnitudeEstimate(float valueSq, float estimate)
        {
            return 0.5f * (estimate + (valueSq * math.rcp(math.max(estimate, 0.000001f))));
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
