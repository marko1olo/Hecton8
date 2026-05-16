using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.World;
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
    /// Flags consumed by the centralized rigidbody culling overseer.
    /// </summary>
    [Flags]
    public enum PhysicsCullingFlags : byte
    {
        None = 0,
        IgnoreCulling = 1 << 0,
        HeavyCollider = 1 << 1
    }

    /// <summary>
    /// Bootstrap-registered facade for centralized rigidbody culling.
    /// </summary>
    public interface IPhysicsCullingOverseer
    {
        /// <summary>Number of rigidbodies currently tracked by the overseer.</summary>
        int TrackedBodyCount { get; }

        /// <summary>Number of bodies kept in a sleep/kinematic/mesh-stripped state by the last completed pass.</summary>
        int CulledBodyCount { get; }

        /// <summary>
        /// Registers a rigidbody for centralized culling.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <param name="flags">Optional culling flags.</param>
        void RegisterBody(Rigidbody body, PhysicsCullingFlags flags);

        /// <summary>
        /// Unregisters a rigidbody from centralized culling.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        void UnregisterBody(Rigidbody body);

        /// <summary>
        /// Wakes culling-managed bodies near an authoritative AUP event origin.
        /// </summary>
        /// <param name="originAup">Event origin.</param>
        /// <param name="radiusMeters">Wake radius in meters.</param>
        void WakeBodiesNear(in AbsoluteUniversePosition originAup, float radiusMeters);

        /// <summary>
        /// Returns true when the overseer currently owns a culled state for the body.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <returns>True when ambient physics should not touch this body.</returns>
        bool IsBodyCulled(Rigidbody body);
    }

    /// <summary>
    /// Optional cold-path provider for authored rigidbody culling flags.
    /// </summary>
    public interface IPhysicsCullingFlagProvider
    {
        /// <summary>Authored flags consumed by the global physics culling overseer.</summary>
        PhysicsCullingFlags CullingFlags { get; }
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

        // COLD ALLOC: RegistryBucket<IPhysicsImpactEventListener>[16] — deferred physics impact listeners — owner: PhysicsEvents
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
    public sealed class GlobalPhysicsStateManager : MonoBehaviour, IFixedTickable, ILateFrameTickable, IPostFixedTickable, IOriginShiftListener, IAcousticPingEventListener, IPhysicsAcousticImpulseEventListener, IPhysicsImpactEventListener, IPhysicsCullingOverseer, IServiceHeartbeat, IServiceShutdown
    {
        private struct VaultBufferBinding<T>
            where T : struct
        {
            public VaultBufferHandle<T> Handle;
            public BufferID BufferId;
            public int RequiredLength;
            public SystemID OwnerSystemId;

            public VaultBufferBinding(BufferID bufferId, int requiredLength, SystemID ownerSystemId)
            {
                Handle = default;
                BufferId = bufferId;
                RequiredLength = requiredLength;
                OwnerSystemId = ownerSystemId;
            }

            public bool IsCreated
            {
                get
                {
                    NativeArray<T> buffer = ResolveExisting();
                    return buffer.IsCreated;
                }
            }

            public int Length
            {
                get
                {
                    NativeArray<T> buffer = ResolveExisting();
                    return buffer.IsCreated ? buffer.Length : 0;
                }
            }

            public bool Ensure(NativeArrayOptions options = NativeArrayOptions.ClearMemory)
            {
                IDataVault dataVault = GlobalRegistry.DataVault;
                if (dataVault == null || RequiredLength <= 0)
                {
                    Handle = default;
                    return false;
                }

                if (!Handle.IsCreated || Handle.Length < RequiredLength)
                {
                    Handle = dataVault.GetBufferHandle<T>(
                        BufferId,
                        RequiredLength,
                        OwnerSystemId,
                        options);
                }

                NativeArray<T> buffer = ResolveExisting(dataVault);
                return buffer.IsCreated && buffer.Length >= RequiredLength;
            }

            public NativeArray<T> AsNativeArray()
            {
                return ResolveExisting();
            }

            public void ReleaseView()
            {
                Handle = default;
            }

            public T this[int index]
            {
                get
                {
                    NativeArray<T> buffer = ResolveExisting();
                    return buffer[index];
                }
                set
                {
                    NativeArray<T> buffer = ResolveExisting();
                    buffer[index] = value;
                }
            }

            public static implicit operator NativeArray<T>(VaultBufferBinding<T> binding)
            {
                return binding.AsNativeArray();
            }

            NativeArray<T> ResolveExisting()
            {
                return ResolveExisting(GlobalRegistry.DataVault);
            }

            NativeArray<T> ResolveExisting(IDataVault dataVault)
            {
                if (dataVault == null || !Handle.IsCreated)
                    return default;

                return Handle.Resolve(dataVault);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct RigidbodyState
        {
            public ulong EntityId;
            public PhysicsStateMask StateMask;
            public int CompensationRefCount;
            public int CullingLockRefCount;
            public float MaxAngularVelocityClamp;
            public bool AllowDistanceKinematicSleep;
            public bool DistanceSleepActive;
            public bool DistanceKinematicSleepActive;
            public bool MeshColliderStripActive;
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
            public PhysicsCullingFlags CullingFlags;
            public byte MeshColliderCount;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct PhysicsDistanceCullingJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<double3> RigidbodyAUPs;
            [ReadOnly] public NativeArray<byte> CurrentStates;
            [WriteOnly] public NativeArray<byte> AwakeResults;
            [WriteOnly] public NativeArray<byte> CommandResults;
            [WriteOnly] public NativeArray<float> DistanceSqResults;

            public float3 CameraForward;
            public float SleepDistanceMeters;
            public float WakeDistanceMeters;
            public float KinematicSleepDistanceMeters;
            public float KinematicWakeDistanceMeters;
            public float MeshColliderStripDistanceMeters;
            public float MeshColliderRestoreDistanceMeters;
            public byte AbyssalDepthCull;

            public void Execute(int index)
            {
                byte currentState = CurrentStates[index];
                if ((currentState & CullingStateIgnoreCulling) != 0)
                {
                    AwakeResults[index] = 1;
                    CommandResults[index] = CullingCommandAwake;
                    DistanceSqResults[index] = 0f;
                    return;
                }

                double3 playerRelativeAup = RigidbodyAUPs[index];
                if (!math.all(math.isfinite(playerRelativeAup)))
                {
                    AwakeResults[index] = 1;
                    CommandResults[index] = CullingCommandAwake | CullingCommandInvalidInput;
                    DistanceSqResults[index] = 0f;
                    return;
                }

                double distanceSqDouble = math.lengthsq(playerRelativeAup);
                float distanceSq = math.isfinite(distanceSqDouble) && distanceSqDouble < float.MaxValue
                    ? (float)distanceSqDouble
                    : float.MaxValue;
                DistanceSqResults[index] = distanceSq;
                if (!math.isfinite(distanceSqDouble))
                {
                    AwakeResults[index] = 1;
                    CommandResults[index] = CullingCommandAwake | CullingCommandInvalidInput;
                    return;
                }

                float effectiveSleepDistance = math.max(1f, SleepDistanceMeters);
                if (AbyssalDepthCull != 0)
                    effectiveSleepDistance *= AbyssalDepthSleepDistanceScale;

                float3 safeCameraForward = math.normalizesafe(CameraForward, new float3(0f, 0f, 1f));
                double behindDot =
                    playerRelativeAup.x * safeCameraForward.x +
                    playerRelativeAup.y * safeCameraForward.y +
                    playerRelativeAup.z * safeCameraForward.z;
                bool behindCamera = behindDot < 0d;
                if (behindCamera)
                    effectiveSleepDistance *= BehindCameraSleepDistanceScale;

                float effectiveWakeDistance = math.max(1f, math.min(WakeDistanceMeters, effectiveSleepDistance - SleepWakeHysteresisMeters));
                float sleepDistanceSq = effectiveSleepDistance * effectiveSleepDistance;
                float wakeDistanceSq = effectiveWakeDistance * effectiveWakeDistance;

                bool sleepActive = (currentState & CullingStateSleepActive) != 0;
                bool shouldSleep = sleepActive
                    ? distanceSq > wakeDistanceSq
                    : distanceSq > sleepDistanceSq;
                bool kinematicActive = (currentState & CullingStateKinematicActive) != 0;
                bool shouldKinematic = kinematicActive
                    ? distanceSq > KinematicWakeDistanceMeters * KinematicWakeDistanceMeters
                    : distanceSq > KinematicSleepDistanceMeters * KinematicSleepDistanceMeters;
                bool meshStripActive = (currentState & CullingStateMeshColliderStripped) != 0;
                bool hasHeavyCollider = (currentState & CullingStateHeavyCollider) != 0;
                bool shouldStripMeshColliders = hasHeavyCollider && (meshStripActive
                    ? distanceSq > MeshColliderRestoreDistanceMeters * MeshColliderRestoreDistanceMeters
                    : distanceSq > MeshColliderStripDistanceMeters * MeshColliderStripDistanceMeters);

                AwakeResults[index] = shouldSleep ? (byte)0 : (byte)1;

                byte command = shouldSleep ? (byte)0 : CullingCommandAwake;
                if (shouldKinematic)
                    command |= CullingCommandKinematic;
                if (shouldStripMeshColliders)
                    command |= CullingCommandStripMeshColliders;
                CommandResults[index] = command;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct PhysicsConnection
        {
            public UnityEngine.Object Owner;
            public Rigidbody BodyA;
            public Rigidbody BodyB;
            public Rigidbody CompensatedBody;
            public PhysicsConnectionKind Kind;
            public bool CompensationActive;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
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

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct PhysicsCullingTelemetryEntry
        {
            public int FrameIndex;
            public int TrackedBodyCount;
            public int CulledBodyCount;
            public uint BodyId;
            public float DistanceSq;
            public byte Command;
            public byte AwakeResult;
            public byte Flags;
            public byte Reserved;
            public ushort CcdInterventions;
        }

        private const int MaxTrackedBodies = 512;
        private const int MaxTrackedConnections = 128;
        private const int MaxQueuedImpactEvents = 256;
        private const int PhysicsCullingTelemetryCapacity = 300;
        private const int MaxImpactFlushIterations = MaxQueuedImpactEvents;
        private const int MaxMeshCollidersPerBody = 4;
        private const int MaxTrackedMeshColliderRefs = MaxTrackedBodies * MaxMeshCollidersPerBody;
        private const int SceneRootScanCapacity = 128;
        private const int SceneRigidbodyScanCapacity = MaxTrackedBodies;
        private const int MeshColliderScratchCapacity = MaxMeshCollidersPerBody;
        private const float MinMass = 0.0001f;
        private const float MassRatioThreshold = 100f;
        private const float MinImpactForce = 0.01f;
        private const float HeavyImpactIntensity = 0.95f;
        private const float MediumImpactIntensity = 0.45f;
        private const float DefaultSleepDistanceMeters = 50f;
        private const float LowTierSleepDistanceMeters = 40f;
        private const float DefaultWakeDistanceMeters = 45f;
        private const float LowTierWakeDistanceMeters = 36f;
        private const float SleepWakeHysteresisMeters = 5f;
        private const float BehindCameraSleepDistanceScale = 0.5f;
        private const float AbyssalDepthSleepDistanceScale = 0.8f;
        private const float AbyssalDepthThresholdMeters = 500f;
        private const float KinematicCullDistanceMeters = 100f;
        private const float KinematicRestoreDistanceMeters = 90f;
        private const float MeshColliderStripDistanceMeters = 150f;
        private const float MeshColliderRestoreDistanceMeters = 90f;
        private const float AcousticWakeMinimumRadiusMeters = 8f;
        private const float AcousticWakeMaximumRadiusMeters = 180f;
        private const float ImpactWakeMinimumRadiusMeters = 4f;
        private const float ImpactWakeMaximumRadiusMeters = 64f;
        private const float PhysicsCullingSlowTickIntervalSeconds = 0.1f;
        private const float DistanceSleepVelocityDampening = 0.9f;
        private const byte CullingStateSleepActive = 1 << 0;
        private const byte CullingStateKinematicActive = 1 << 1;
        private const byte CullingStateMeshColliderStripped = 1 << 2;
        private const byte CullingStateIgnoreCulling = 1 << 3;
        private const byte CullingStateHeavyCollider = 1 << 4;
        private const byte CullingCommandAwake = 1 << 0;
        private const byte CullingCommandKinematic = 1 << 1;
        private const byte CullingCommandStripMeshColliders = 1 << 2;
        private const byte CullingCommandInvalidInput = 1 << 7;
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
        private const float KinematicHitStopImpactSpeedMetersPerSecond = 20f;
        private const float KinematicHitStopTimeScale = 0.05f;
        private const float KinematicHitStopDurationSeconds = 0.1f;
        private const float SpeculativeHoverTideMinScale = 0.75f;
        private const float SpeculativeHoverTideMaxScale = 1.25f;
        private const double ColliderLodCompoundToSimpleDistanceSq = ColliderLodCompoundToSimpleDistanceMeters * ColliderLodCompoundToSimpleDistanceMeters;
        private const double ColliderLodSimpleToCompoundDistanceSq = ColliderLodSimpleToCompoundDistanceMeters * ColliderLodSimpleToCompoundDistanceMeters;
        private const SystemID OwnerSystemId = SystemID.GlobalPhysicsStateManager;
        private static readonly uint _nanRecoverySystemHash = unchecked((uint)LocHash.Compute(nameof(GlobalPhysicsStateManager)));
        private static readonly uint _kinematicHitStopHash = unchecked((uint)LocHash.Compute("GlobalPhysicsStateManager.KinematicHitStop"));

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
        // COLD ALLOC: MeshCollider[2048] - per-body heavy collider refs for distance stripping - owner: GlobalPhysicsStateManager
        private readonly MeshCollider[] _trackedMeshColliders = new MeshCollider[MaxTrackedMeshColliderRefs];
        // COLD ALLOC: byte[2048] - pre-strip enabled flags for mesh collider restoration - owner: GlobalPhysicsStateManager
        private readonly byte[] _trackedMeshColliderEnabledBeforeStrip = new byte[MaxTrackedMeshColliderRefs];
        // COLD ALLOC: List<MeshCollider>[4] - cold registration scratch; never used by frame loops - owner: GlobalPhysicsStateManager
        private readonly List<MeshCollider> _meshColliderScratch = new List<MeshCollider>(MeshColliderScratchCapacity);

        private VaultBufferBinding<float3> _lastValidPositions = new VaultBufferBinding<float3>(BufferID.RigidbodyLastValidPositions, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<double3> _rigidbodyAUPs = new VaultBufferBinding<double3>(BufferID.RigidbodyAUPs, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<byte> _rigidbodyCullingStateSnapshot = new VaultBufferBinding<byte>(BufferID.RigidbodyCullingState, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<byte> _rigidbodyAwakeResults = new VaultBufferBinding<byte>(BufferID.RigidbodyAwakeResults, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<byte> _rigidbodyCullingCommandResults = new VaultBufferBinding<byte>(BufferID.RigidbodyCullingCommands, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<float> _rigidbodyDistanceSqResults = new VaultBufferBinding<float>(BufferID.RigidbodyDistanceSq, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<PhysicsCullingTelemetryEntry> _physicsCullingTelemetry = new VaultBufferBinding<PhysicsCullingTelemetryEntry>(BufferID.PhysicsCullingTelemetry, PhysicsCullingTelemetryCapacity, OwnerSystemId);
        private VaultBufferBinding<PhysicsImpactEventData> _impactEvents = new VaultBufferBinding<PhysicsImpactEventData>(BufferID.PhysicsImpactEvents, MaxQueuedImpactEvents, OwnerSystemId);
        private JobHandle _physicsCullingJobHandle;
        private int _trackedBodyCount;
        private int _connectionCount;
        private int _queuedImpactCount;
        private int _impactEventReadIndex;
        private int _impactEventWriteIndex;
        private int _physicsCullingJobCount;
        private int _culledBodyCount;
        private int _physicsCullingTelemetryWriteIndex;
        private int _kinematicCcdInterventionsFrame = -1;
        private int _kinematicCcdInterventionsThisFrame;
        private bool _serviceRegistered;
        private bool _isInitialized;
        private bool _registeredFixedTick;
        private bool _registeredLateFrameTick;
        private bool _registeredPostFixedTick;
        private bool _registeredOriginShift;
        private bool _physicsCullingEventBusRegistered;
        private bool _physicsCullingJobScheduled;
        private bool _physicsCullingJobDiscardRequested;
        private bool _sceneEventsSubscribed;
        private bool _connectionCapacityOverflowReported;
        private bool _trackedBodyCapacityOverflowReported;
        private bool _nativeStateAllocationFailureReported;
        private float _lastFixedDeltaTime = PhysicsFixedStepSeconds;
        private float _physicsCullingSlowTickAccumulator;
        private int _aupJitterSentinelCountdown;
        private int _aupJitterSentinelCachedFrame = -1;
        private bool _aupJitterSentinelDueThisFrame;
        private float _hitStopRemainingUnscaledSeconds;
        private float _hitStopRestoreTimeScale = 1f;
        private int _lastKineticAnomalyFrame = -1;
        private bool _hitStopActive;
        private static int _cachedWaterLevelFrame = -1;
        private static float _cachedWaterLevelBaseY;
        private static float _cachedWaterLevelAmplitude;
        private static float _cachedWaterLevelCelestialTideY;
        private static uint _cachedWaterLevelCelestialSequence;
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

        /// <inheritdoc />
        public int TrackedBodyCount => _trackedBodyCount;

        /// <inheritdoc />
        public int CulledBodyCount => _culledBodyCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _cachedWaterLevelFrame = -1;
            _cachedWaterLevelBaseY = 0f;
            _cachedWaterLevelAmplitude = 0f;
            _cachedWaterLevelCelestialTideY = 0f;
            _cachedWaterLevelCelestialSequence = 0u;
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
            CelestialRuntimeSnapshot celestialSnapshot = GlobalRegistry.CelestialRuntimeSnapshot;
            uint celestialSequence = GlobalRegistry.CelestialRuntimeSnapshotSequence;
            float celestialTideY = (celestialSnapshot.Flags & (uint)CelestialRuntimeFlags.Valid) != 0u
                ? celestialSnapshot.TideHeightMeters
                : 0f;
            if (_cachedWaterLevelFrame == frame &&
                math.abs(_cachedWaterLevelBaseY - baseWaterLevelY) <= 0.0001f &&
                math.abs(_cachedWaterLevelAmplitude - safeAmplitude) <= 0.0001f &&
                math.abs(_cachedWaterLevelCelestialTideY - celestialTideY) <= 0.0001f &&
                _cachedWaterLevelCelestialSequence == celestialSequence &&
                _cachedWaterLevelTidesEnabled == tidesEnabled)
            {
                return _cachedCurrentWaterLevelY;
            }

            float resolvedWaterLevelY = baseWaterLevelY;
            if (tidesEnabled && safeAmplitude > 0f)
            {
                float tideTimeSeconds = ResolveAbsoluteUniverseTideTimeSeconds(timeSeconds, in celestialSnapshot);
                float combinedWave = ResolveSignedTriangleWave(tideTimeSeconds) + ResolveSignedTriangleWave(tideTimeSeconds * 0.5f);
                resolvedWaterLevelY += combinedWave * safeAmplitude;
            }

            resolvedWaterLevelY += celestialTideY;
            _cachedWaterLevelFrame = frame;
            _cachedWaterLevelBaseY = baseWaterLevelY;
            _cachedWaterLevelAmplitude = safeAmplitude;
            _cachedWaterLevelCelestialTideY = celestialTideY;
            _cachedWaterLevelCelestialSequence = celestialSequence;
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

        private static float ResolveAbsoluteUniverseTideTimeSeconds(
            float fallbackTimeSeconds,
            in CelestialRuntimeSnapshot celestialSnapshot)
        {
            double universeTime = (celestialSnapshot.Flags & (uint)CelestialRuntimeFlags.Valid) != 0u
                ? celestialSnapshot.AbsoluteUniverseTime
                : GlobalRegistry.AbsoluteUniverseTime;
            if (double.IsNaN(universeTime) || double.IsInfinity(universeTime))
                return fallbackTimeSeconds;

            double wrappedTime = universeTime % 4096d;
            return (float)wrappedTime;
        }

        internal static float ResolveSpeculativeHoverHeightMeters(float baseHeightMeters, float timeSeconds)
        {
            float safeBaseHeight = math.max(0f, baseHeightMeters);
            if (safeBaseHeight <= 0f)
                return 0f;

            CelestialRuntimeSnapshot celestialSnapshot = GlobalRegistry.CelestialRuntimeSnapshot;
            bool hasCelestialTide = (celestialSnapshot.Flags & (uint)CelestialRuntimeFlags.Valid) != 0u;
            float tide01 = hasCelestialTide
                ? math.saturate(celestialSnapshot.TideHigh01)
                : math.saturate(0.5f + (0.5f * ResolveSignedTriangleWave(timeSeconds)));
            return safeBaseHeight * math.lerp(SpeculativeHoverTideMinScale, SpeculativeHoverTideMaxScale, tide01);
        }

        internal static void RegisterTrackedBody(Rigidbody body)
        {
            if (!TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return;

            manager.RegisterTrackedBodyInternal(body, PhysicsCullingFlags.None);
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

        internal static void ReportKinematicCcdIntervention()
        {
            if (!TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return;

            manager.ReportKinematicCcdInterventionInternal();
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

        internal static void RequestKinematicHitStop(float impactSpeedMetersPerSecond)
        {
            if (!TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return;

            manager.RequestKinematicHitStopInternal(impactSpeedMetersPerSecond);
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
                TryRegisterPhysicsCullingEventBus();
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
            TryRegisterPhysicsCullingEventBus();
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
            TryRegisterPhysicsCullingEventBus();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            TryCompletePhysicsCullingJobNonBlocking();
            TickKinematicHitStopGate();
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
            ReleaseUndersizedNativeState();

            if (!_lastValidPositions.IsCreated)
            {
                // COLD VAULT: float3[512] - authoritative last-valid runtime-space body positions for origin-shift-safe recovery.
                _lastValidPositions.Ensure();
            }

            if (!_impactEvents.IsCreated)
            {
                // COLD VAULT: PhysicsImpactEventData[256] - deferred gameplay physics impact ring.
                _impactEvents.Ensure();
            }

            if (!_rigidbodyAUPs.IsCreated)
            {
                // COLD VAULT: double3[512] - player-relative AUP body positions for Burst distance culling.
                _rigidbodyAUPs.Ensure();
            }

            if (!_rigidbodyCullingStateSnapshot.IsCreated)
            {
                // COLD VAULT: byte[512] - culling state snapshot consumed by Burst job.
                _rigidbodyCullingStateSnapshot.Ensure();
            }

            if (!_rigidbodyAwakeResults.IsCreated)
            {
                // COLD VAULT: byte[512] - Burst awake/sleep result lane.
                _rigidbodyAwakeResults.Ensure();
            }

            if (!_rigidbodyCullingCommandResults.IsCreated)
            {
                // COLD VAULT: byte[512] - Burst culling command lane.
                _rigidbodyCullingCommandResults.Ensure();
            }

            if (!_rigidbodyDistanceSqResults.IsCreated)
            {
                // COLD VAULT: float[512] - distance squared diagnostics from culling job.
                _rigidbodyDistanceSqResults.Ensure();
            }

            if (!_physicsCullingTelemetry.IsCreated)
            {
                // COLD VAULT: PhysicsCullingTelemetryEntry[300] - black-box circular telemetry for sleep enforcer.
                _physicsCullingTelemetry.Ensure();
            }
        }

        private void ReleaseUndersizedNativeState()
        {
            bool hasUndersizedLane =
                (_lastValidPositions.IsCreated && _lastValidPositions.Length < MaxTrackedBodies) ||
                (_impactEvents.IsCreated && _impactEvents.Length < MaxQueuedImpactEvents) ||
                (_rigidbodyAUPs.IsCreated && _rigidbodyAUPs.Length < MaxTrackedBodies) ||
                (_rigidbodyCullingStateSnapshot.IsCreated && _rigidbodyCullingStateSnapshot.Length < MaxTrackedBodies) ||
                (_rigidbodyAwakeResults.IsCreated && _rigidbodyAwakeResults.Length < MaxTrackedBodies) ||
                (_rigidbodyCullingCommandResults.IsCreated && _rigidbodyCullingCommandResults.Length < MaxTrackedBodies) ||
                (_rigidbodyDistanceSqResults.IsCreated && _rigidbodyDistanceSqResults.Length < MaxTrackedBodies) ||
                (_physicsCullingTelemetry.IsCreated && _physicsCullingTelemetry.Length < PhysicsCullingTelemetryCapacity);
            if (!hasUndersizedLane)
                return;

            CompletePhysicsCullingJobForStateMutation(discardResults: true);
            ReleaseUndersizedVaultBuffer(ref _lastValidPositions, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _impactEvents, MaxQueuedImpactEvents);
            ReleaseUndersizedVaultBuffer(ref _rigidbodyAUPs, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _rigidbodyCullingStateSnapshot, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _rigidbodyAwakeResults, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _rigidbodyCullingCommandResults, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _rigidbodyDistanceSqResults, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _physicsCullingTelemetry, PhysicsCullingTelemetryCapacity);
        }

        private static void ReleaseUndersizedVaultBuffer<T>(ref VaultBufferBinding<T> buffer, int requiredLength)
            where T : struct
        {
            if (!buffer.IsCreated || buffer.Length >= requiredLength)
                return;

            buffer.ReleaseView();
        }

        private bool HasRequiredNativeState()
        {
            return _lastValidPositions.IsCreated &&
                _lastValidPositions.Length >= MaxTrackedBodies &&
                _impactEvents.IsCreated &&
                _impactEvents.Length >= MaxQueuedImpactEvents &&
                _rigidbodyAUPs.IsCreated &&
                _rigidbodyAUPs.Length >= MaxTrackedBodies &&
                _rigidbodyCullingStateSnapshot.IsCreated &&
                _rigidbodyCullingStateSnapshot.Length >= MaxTrackedBodies &&
                _rigidbodyAwakeResults.IsCreated &&
                _rigidbodyAwakeResults.Length >= MaxTrackedBodies &&
                _rigidbodyCullingCommandResults.IsCreated &&
                _rigidbodyCullingCommandResults.Length >= MaxTrackedBodies &&
                _rigidbodyDistanceSqResults.IsCreated &&
                _rigidbodyDistanceSqResults.Length >= MaxTrackedBodies &&
                _physicsCullingTelemetry.IsCreated &&
                _physicsCullingTelemetry.Length >= PhysicsCullingTelemetryCapacity;
        }

        private void ReportNativeStateUnavailable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_nativeStateAllocationFailureReported)
            {
                Debug.LogError("[GlobalPhysicsStateManager] Required native state unavailable; rigidbody tracking rejected.");
                _nativeStateAllocationFailureReported = true;
            }
#endif
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
            RestoreKinematicHitStopGate(forceRestore: true);
            CompletePhysicsCullingJobForStateMutation(discardResults: true);
            UnregisterRuntimeHooks();
            UnsubscribeSceneEvents();
            ClearRuntimeState();

            _impactEvents.ReleaseView();
            _lastValidPositions.ReleaseView();
            _rigidbodyAUPs.ReleaseView();
            _rigidbodyCullingStateSnapshot.ReleaseView();
            _rigidbodyAwakeResults.ReleaseView();
            _rigidbodyCullingCommandResults.ReleaseView();
            _rigidbodyDistanceSqResults.ReleaseView();
            _physicsCullingTelemetry.ReleaseView();

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

            if (_physicsCullingEventBusRegistered)
            {
                PhysicsEventBus.Unregister((IAcousticPingEventListener)this);
                PhysicsEventBus.Unregister((IPhysicsAcousticImpulseEventListener)this);
                PhysicsEvents.Unregister((IPhysicsImpactEventListener)this);
                _physicsCullingEventBusRegistered = false;
            }
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            _lastFixedDeltaTime = SanitizeFixedStepDelta(fixedDeltaTime);
            RefreshTrackedBodies(_lastFixedDeltaTime);
            SweepNaNPhysicsState();
            EvaluateConnections();
            TickPhysicsCullingSlowCadence(_lastFixedDeltaTime);
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

        /// <inheritdoc />
        public void RegisterBody(Rigidbody body, PhysicsCullingFlags flags)
        {
            RegisterTrackedBodyInternal(body, flags);
        }

        /// <inheritdoc />
        public void UnregisterBody(Rigidbody body)
        {
            UnregisterTrackedBodyInternal(body);
        }

        /// <inheritdoc />
        public void WakeBodiesNear(in AbsoluteUniversePosition originAup, float radiusMeters)
        {
            WakeCulledBodiesNear(in originAup, radiusMeters);
        }

        /// <inheritdoc />
        public bool IsBodyCulled(Rigidbody body)
        {
            int bodyIndex = FindTrackedBodyIndex(body);
            if (bodyIndex < 0)
                return false;

            RigidbodyState bodyState = _bodyStates[bodyIndex];
            return bodyState.DistanceSleepActive ||
                bodyState.DistanceKinematicSleepActive ||
                bodyState.MeshColliderStripActive;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered)
                return;

            GlobalRegistry.RegisterPhysicsStateManager(this);
            GlobalRegistry.RegisterPhysicsCullingOverseer(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.PhysicsStateManager, this) &&
                ReferenceEquals(GlobalRegistry.PhysicsCullingOverseer, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.PhysicsStateManager, this))
                GlobalRegistry.UnregisterPhysicsStateManager(this);
            if (ReferenceEquals(GlobalRegistry.PhysicsCullingOverseer, this))
                GlobalRegistry.UnregisterPhysicsCullingOverseer(this);

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

        private void TryRegisterPhysicsCullingEventBus()
        {
            if (_physicsCullingEventBusRegistered || !Application.isPlaying)
                return;

            PhysicsEventBus.Register((IAcousticPingEventListener)this);
            PhysicsEventBus.Register((IPhysicsAcousticImpulseEventListener)this);
            PhysicsEvents.Register((IPhysicsImpactEventListener)this);
            _physicsCullingEventBusRegistered = true;
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
            CompletePhysicsCullingJobForStateMutation(discardResults: true);
            if (!_lastValidPositions.IsCreated || _trackedBodyCount <= 0)
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
                if (body.isKinematic && !bodyState.DistanceKinematicSleepActive)
                    continue;

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
            CompletePhysicsCullingJobForStateMutation(discardResults: true);
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

                RigidbodyState bodyState = _bodyStates[i];
                if (body.isKinematic && !bodyState.DistanceKinematicSleepActive)
                    continue;

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
                if (_rigidbodyAUPs.IsCreated)
                    _rigidbodyAUPs[i] = new double3(targetPosition.x, targetPosition.y, targetPosition.z);
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
            CompletePhysicsCullingJobForStateMutation(discardResults: true);
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

        private void RequestKinematicHitStopInternal(float impactSpeedMetersPerSecond)
        {
            if (!math.isfinite(impactSpeedMetersPerSecond) ||
                impactSpeedMetersPerSecond < KinematicHitStopImpactSpeedMetersPerSecond)
            {
                return;
            }

            ITickDispatcher dispatcher = GlobalRegistry.TickDispatcher;
            float currentScale = dispatcher != null ? dispatcher.TimeDilationScalar : 1f;
            if (!math.isfinite(currentScale) || currentScale <= 0.0001f)
                return;

            if (!_hitStopActive)
                _hitStopRestoreTimeScale = currentScale;

            _hitStopActive = true;
            _hitStopRemainingUnscaledSeconds = math.max(_hitStopRemainingUnscaledSeconds, KinematicHitStopDurationSeconds);
            if (currentScale > KinematicHitStopTimeScale)
                dispatcher?.RequestTimeDilation(KinematicHitStopTimeScale, _kinematicHitStopHash);
        }

        private void TickKinematicHitStopGate()
        {
            if (!_hitStopActive)
                return;

            _hitStopRemainingUnscaledSeconds -= ResolveDispatcherUnscaledDeltaTime();
            if (_hitStopRemainingUnscaledSeconds > 0f)
                return;

            RestoreKinematicHitStopGate(forceRestore: false);
        }

        private static float ResolveDispatcherUnscaledDeltaTime()
        {
            ITickDispatcher dispatcher = GlobalRegistry.TickDispatcher;
            if (dispatcher != null)
            {
                double dispatcherDelta = dispatcher.TimeSnapshot.UnscaledDeltaTime;
                if (dispatcherDelta > 0d && double.IsFinite(dispatcherDelta))
                    return dispatcherDelta > 1d ? 1f : (float)dispatcherDelta;
            }

            float currentDelta = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            return math.isfinite(currentDelta) && currentDelta > 0f
                ? currentDelta
                : PhysicsFixedStepSeconds;
        }

        private void RestoreKinematicHitStopGate(bool forceRestore)
        {
            if (!_hitStopActive)
                return;

            ITickDispatcher dispatcher = GlobalRegistry.TickDispatcher;
            float currentScale = dispatcher != null ? dispatcher.TimeDilationScalar : 1f;
            bool ownsScale = math.isfinite(currentScale) &&
                currentScale > 0.0001f &&
                (forceRestore || currentScale <= KinematicHitStopTimeScale + 0.0001f);
            if (ownsScale)
                dispatcher?.RequestTimeDilation(math.max(0.0001f, _hitStopRestoreTimeScale), _kinematicHitStopHash);

            _hitStopActive = false;
            _hitStopRemainingUnscaledSeconds = 0f;
            _hitStopRestoreTimeScale = 1f;
        }

        private void RegisterTrackedBodyInternal(Rigidbody body, PhysicsCullingFlags registrationFlags = PhysicsCullingFlags.None)
        {
            if (body == null)
                return;

            if (!HasRequiredNativeState())
            {
                EnsureNativeState();
                if (!HasRequiredNativeState())
                {
                    ReportNativeStateUnavailable();
                    return;
                }
            }

            ulong bodyEntityId = EntityId.ToULong(body.GetEntityId());
            PhysicsCullingFlags cullingFlags = ResolveCullingFlags(body) | registrationFlags;
            if (_trackedBodyIndexByEntityId.TryGetValue(bodyEntityId, out int existingBodyIndex))
            {
                if ((uint)existingBodyIndex < (uint)_trackedBodyCount &&
                    ReferenceEquals(_trackedBodies[existingBodyIndex], body))
                {
                    RigidbodyState existingState = _bodyStates[existingBodyIndex];
                    cullingFlags |= existingState.CullingFlags;
                    byte existingMeshColliderCount = existingState.MeshColliderCount;
                    if ((cullingFlags & PhysicsCullingFlags.HeavyCollider) != 0 && existingMeshColliderCount == 0)
                        existingMeshColliderCount = CacheMeshCollidersForBody(body, existingBodyIndex);
                    if (existingMeshColliderCount > 0)
                        cullingFlags |= PhysicsCullingFlags.HeavyCollider;

                    existingState.CullingFlags = cullingFlags;
                    existingState.MeshColliderCount = existingMeshColliderCount;
                    existingState.AllowDistanceKinematicSleep = ShouldAllowDistanceKinematicSleep(body, cullingFlags);
                    if ((cullingFlags & PhysicsCullingFlags.IgnoreCulling) != 0)
                        RestoreAllPhysicsCullingState(existingBodyIndex, body, ref existingState, forceWake: true);

                    _bodyStates[existingBodyIndex] = existingState;
                    return;
                }

                if ((uint)existingBodyIndex < (uint)_trackedBodyCount)
                    RemoveTrackedBodyAt(existingBodyIndex);
                else
                    _trackedBodyIndexByEntityId.Remove(bodyEntityId);
            }

            if (!EnsureTrackedBodyCapacity(_trackedBodyCount + 1))
                return;

            CompletePhysicsCullingJobForStateMutation(discardResults: true);
            IPhysicsColliderLodHysteresisSink colliderLodSink = ResolveColliderLodSink(body);
            Vector3 bodyPosition = body.position;
            Vector3 bodyLinearVelocity = body.linearVelocity;
            Vector3 bodyAngularVelocity = body.angularVelocity;
            Vector3 bodyInertiaTensor = body.inertiaTensor;
            Quaternion bodyInertiaTensorRotation = body.inertiaTensorRotation;
            bool hasFiniteBodyPosition = IsFinite(bodyPosition);

            int bodyIndex = _trackedBodyCount++;
            byte meshColliderCount = CacheMeshCollidersForBody(body, bodyIndex);
            if (meshColliderCount > 0)
                cullingFlags |= PhysicsCullingFlags.HeavyCollider;

            _trackedBodies[bodyIndex] = body;
            _bodyStates[bodyIndex] = new RigidbodyState
            {
                EntityId = bodyEntityId,
                StateMask = PhysicsStateMask.None,
                CompensationRefCount = 0,
                CullingLockRefCount = 0,
                MaxAngularVelocityClamp = ResolveMaxAngularVelocityClamp(body),
                AllowDistanceKinematicSleep = ShouldAllowDistanceKinematicSleep(body, cullingFlags),
                CullingFlags = cullingFlags,
                MeshColliderCount = meshColliderCount,
                DistanceSleepActive = false,
                DistanceKinematicSleepActive = false,
                MeshColliderStripActive = false,
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
            EnsureReporter(body);
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
                HectonFloatingOrigin.IsShiftInProgress ||
                !_impactEvents.IsCreated ||
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
                normal3 *= math.rsqrt(math.max(normalSq, 0.000001f));
            AbsoluteUniversePosition pointAup = AbsoluteUniversePosition.FromRuntimePosition(new Vector3(point3.x, point3.y, point3.z));
            PhysicsImpactWeightClass weightClass = ResolveImpactWeightClass(impactIntensity);

            EnqueueImpactEvent(new PhysicsImpactEventData
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
        }

        private void QueueKinematicImpactInternal(
            Rigidbody primaryBody,
            Rigidbody secondaryBody,
            Vector3 point,
            Vector3 normal,
            float impactSpeedMetersPerSecond)
        {
            if (primaryBody == null ||
                HectonFloatingOrigin.IsShiftInProgress ||
                !_impactEvents.IsCreated ||
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
                normal3 *= math.rsqrt(math.max(normalSq, 0.000001f));
            AbsoluteUniversePosition pointAup = AbsoluteUniversePosition.FromRuntimePosition(new Vector3(point3.x, point3.y, point3.z));

            float impactIntensity = ResolveImpactIntensityFromForce(impactForce);
            if (!(impactIntensity > 0f))
                return;

            EnqueueImpactEvent(new PhysicsImpactEventData
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
        }

        private bool EnqueueImpactEvent(in PhysicsImpactEventData impactEvent)
        {
            if (!_impactEvents.IsCreated || _queuedImpactCount >= MaxQueuedImpactEvents)
                return false;

            int writeIndex = _impactEventWriteIndex;
            if ((uint)writeIndex >= (uint)MaxQueuedImpactEvents)
                writeIndex = 0;

            _impactEvents[writeIndex] = impactEvent;
            _impactEventWriteIndex = writeIndex + 1 >= MaxQueuedImpactEvents
                ? 0
                : writeIndex + 1;
            _queuedImpactCount++;
            return true;
        }

        private void FlushImpactEvents()
        {
            if (!_impactEvents.IsCreated || _queuedImpactCount <= 0)
                return;

            int processedCount = 0;
            while (_queuedImpactCount > 0 &&
                   processedCount < MaxImpactFlushIterations)
            {
                int readIndex = _impactEventReadIndex;
                if ((uint)readIndex >= (uint)MaxQueuedImpactEvents)
                    readIndex = 0;

                PhysicsImpactEventData impactEvent = _impactEvents[readIndex];
                _impactEvents[readIndex] = default;
                _impactEventReadIndex = readIndex + 1 >= MaxQueuedImpactEvents
                    ? 0
                    : readIndex + 1;
                _queuedImpactCount--;
                processedCount++;
                Vector3 impactPoint = new Vector3(impactEvent.Point.x, impactEvent.Point.y, impactEvent.Point.z);
                Vector3 impactNormal = new Vector3(impactEvent.Normal.x, impactEvent.Normal.y, impactEvent.Normal.z);
                ImpactSignal corridorSignal = new ImpactSignal
                {
                    PointAup = impactEvent.PointAup,
                    Force = impactEvent.Force,
                    Intensity = impactEvent.Intensity,
                    PrimaryBodyId = unchecked((uint)impactEvent.PrimaryBodyId),
                    WeightClass = (byte)impactEvent.WeightClass,
                    PrimaryMaterialId = impactEvent.PrimaryAudioMaterialId,
                    SecondaryMaterialId = impactEvent.SecondaryAudioMaterialId,
                    Flags = 0
                };
                GlobalSignals.Publish(in corridorSignal);
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
            ApplyImmediateConnectionCullingLocks(in _connections[connectionIndex], forceWake: true);
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
                bodyState.CullingLockRefCount = 0;
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
                ApplyImmediateConnectionCullingLocks(in connection, forceWake: false);
                if (!connection.CompensationActive || connection.CompensatedBody == null)
                    continue;

                int compensatedIndex = FindTrackedBodyIndex(connection.CompensatedBody);
                if (compensatedIndex < 0)
                    continue;

                RigidbodyState bodyState = _bodyStates[compensatedIndex];
                bodyState.CompensationRefCount++;
                bodyState.CullingLockRefCount++;
                _bodyStates[compensatedIndex] = bodyState;
            }
        }

        private void ApplyImmediateConnectionCullingLocks(in PhysicsConnection connection, bool forceWake)
        {
            if (connection.Kind == PhysicsConnectionKind.Tether)
            {
                IncrementBodyCullingLock(connection.BodyA, forceWake);
                IncrementBodyCullingLock(connection.BodyB, forceWake);
                return;
            }

            if (connection.Kind == PhysicsConnectionKind.Dock)
                IncrementBodyCullingLock(connection.BodyA, forceWake);
        }

        private void IncrementBodyCullingLock(Rigidbody body, bool forceWake)
        {
            int bodyIndex = FindTrackedBodyIndex(body);
            if (bodyIndex < 0)
                return;

            RigidbodyState bodyState = _bodyStates[bodyIndex];
            bodyState.CullingLockRefCount++;
            if (forceWake &&
                (bodyState.DistanceSleepActive ||
                 bodyState.DistanceKinematicSleepActive ||
                 bodyState.MeshColliderStripActive))
            {
                RestoreAllPhysicsCullingState(bodyIndex, body, ref bodyState, forceWake: true);
            }

            _bodyStates[bodyIndex] = bodyState;
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
            if (!ShouldRunAupJitterSentinelFrame() ||
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

        private bool ShouldRunAupJitterSentinelFrame()
        {
            int frame = Time.frameCount;
            if (_aupJitterSentinelCachedFrame == frame)
                return _aupJitterSentinelDueThisFrame;

            _aupJitterSentinelCachedFrame = frame;
            if (_aupJitterSentinelCountdown <= 0)
            {
                _aupJitterSentinelCountdown = AupJitterSentinelFrameInterval - 1;
                _aupJitterSentinelDueThisFrame = true;
                return true;
            }

            _aupJitterSentinelCountdown--;
            _aupJitterSentinelDueThisFrame = false;
            return false;
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

                DumpPhysicsCullingBlackBox("rigidbody_nan");
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
            body.ResetCenterOfMass();
            body.ResetInertiaTensor();
            body.position = targetPosition;
            body.rotation = targetRotation;
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

        private void TickPhysicsCullingSlowCadence(float fixedDeltaTime)
        {
            TryCompletePhysicsCullingJobNonBlocking();
            _physicsCullingSlowTickAccumulator += math.max(0f, fixedDeltaTime);
            if (_physicsCullingSlowTickAccumulator < PhysicsCullingSlowTickIntervalSeconds)
                return;

            _physicsCullingSlowTickAccumulator -= PhysicsCullingSlowTickIntervalSeconds;
            if (_physicsCullingSlowTickAccumulator > PhysicsCullingSlowTickIntervalSeconds)
                _physicsCullingSlowTickAccumulator = 0f;

            RunPhysicsCullingSlowTick();
        }

        private void RunPhysicsCullingSlowTick()
        {
            if (!_isInitialized || _trackedBodyCount <= 0)
            {
                _culledBodyCount = 0;
                return;
            }

            if (_physicsCullingJobScheduled)
            {
                TryCompletePhysicsCullingJobNonBlocking();
                if (_physicsCullingJobScheduled)
                    return;
            }

            if (HectonFloatingOrigin.IsShiftInProgress ||
                !HasRequiredNativeState() ||
                !TryResolvePhysicsCullingPlayerState(out AbsoluteUniversePosition playerAup, out float3 cameraForward, out float depthMeters))
            {
                return;
            }

            int bodyIndex = 0;
            while (bodyIndex < _trackedBodyCount)
            {
                Rigidbody body = _trackedBodies[bodyIndex];
                if (body == null)
                {
                    RemoveTrackedBodyAt(bodyIndex);
                    continue;
                }

                RigidbodyState bodyState = _bodyStates[bodyIndex];
                if (!TryResolveTrackedBodyAup(body, ref bodyState, out AbsoluteUniversePosition bodyAup))
                {
                    _bodyStates[bodyIndex] = bodyState;
                    _rigidbodyAUPs[bodyIndex] = double3.zero;
                    _rigidbodyCullingStateSnapshot[bodyIndex] = CullingStateIgnoreCulling;
                    bodyIndex++;
                    continue;
                }

                _bodyStates[bodyIndex] = bodyState;
                _rigidbodyAUPs[bodyIndex] = AbsoluteUniversePosition.DeltaMetersClamped(in bodyAup, in playerAup);
                _rigidbodyCullingStateSnapshot[bodyIndex] = BuildPhysicsCullingStateSnapshot(in bodyState);
                bodyIndex++;
            }

            int jobCount = _trackedBodyCount;
            if (jobCount <= 0)
            {
                _culledBodyCount = 0;
                return;
            }

            float sleepDistanceMeters = ResolveSleepDistanceMeters();
            PhysicsDistanceCullingJob job = new PhysicsDistanceCullingJob
            {
                RigidbodyAUPs = _rigidbodyAUPs,
                CurrentStates = _rigidbodyCullingStateSnapshot,
                AwakeResults = _rigidbodyAwakeResults,
                CommandResults = _rigidbodyCullingCommandResults,
                DistanceSqResults = _rigidbodyDistanceSqResults,
                CameraForward = cameraForward,
                SleepDistanceMeters = sleepDistanceMeters,
                WakeDistanceMeters = ResolveWakeDistanceMeters(sleepDistanceMeters),
                KinematicSleepDistanceMeters = KinematicCullDistanceMeters,
                KinematicWakeDistanceMeters = KinematicRestoreDistanceMeters,
                MeshColliderStripDistanceMeters = GlobalPhysicsStateManager.MeshColliderStripDistanceMeters,
                MeshColliderRestoreDistanceMeters = GlobalPhysicsStateManager.MeshColliderRestoreDistanceMeters,
                AbyssalDepthCull = depthMeters >= AbyssalDepthThresholdMeters ? (byte)1 : (byte)0
            };

            _physicsCullingJobCount = jobCount;
            _physicsCullingJobDiscardRequested = false;
            _physicsCullingJobHandle = job.Schedule(jobCount, 32);
            _physicsCullingJobScheduled = true;
            JobHandle.ScheduleBatchedJobs();
        }

        private bool TryCompletePhysicsCullingJobNonBlocking()
        {
            if (!_physicsCullingJobScheduled)
                return true;

            if (!_physicsCullingJobHandle.IsCompleted)
                return false;

            CompletePhysicsCullingJobForStateMutation(discardResults: false);
            return true;
        }

        private void CompletePhysicsCullingJobForStateMutation(bool discardResults)
        {
            if (!_physicsCullingJobScheduled)
                return;

            if (discardResults)
                _physicsCullingJobDiscardRequested = true;

            _physicsCullingJobHandle.Complete();
            int jobCount = math.min(_physicsCullingJobCount, _trackedBodyCount);
            bool shouldDiscard = _physicsCullingJobDiscardRequested;
            _physicsCullingJobScheduled = false;
            _physicsCullingJobDiscardRequested = false;
            _physicsCullingJobCount = 0;
            _physicsCullingJobHandle = default;

            if (shouldDiscard)
                return;

            DispatchPhysicsCullingResults(jobCount);
        }

        private void DispatchPhysicsCullingResults(int jobCount)
        {
            int culledCount = 0;
            for (int i = jobCount - 1; i >= 0; i--)
            {
                Rigidbody body = _trackedBodies[i];
                if (body == null)
                {
                    RemoveTrackedBodyAt(i);
                    continue;
                }

                RigidbodyState bodyState = _bodyStates[i];
                byte awakeResult = _rigidbodyAwakeResults[i];
                byte command = _rigidbodyCullingCommandResults[i];
                float distanceSq = _rigidbodyDistanceSqResults[i];
                if (ApplyPhysicsCullingCommand(i, body, ref bodyState, awakeResult, command, distanceSq))
                    culledCount++;

                _bodyStates[i] = bodyState;
                RecordPhysicsCullingTelemetry(in bodyState, awakeResult, command, distanceSq, culledCount);
            }

            _culledBodyCount = culledCount;
        }

        private bool ApplyPhysicsCullingCommand(
            int bodyIndex,
            Rigidbody body,
            ref RigidbodyState bodyState,
            byte awakeResult,
            byte command,
            float distanceSq)
        {
            if ((command & CullingCommandInvalidInput) != 0)
            {
                RestoreAllPhysicsCullingState(bodyIndex, body, ref bodyState, forceWake: true);
                DumpPhysicsCullingBlackBox("invalid_input");
                return false;
            }

            bool cullingAllowed = bodyState.AllowDistanceKinematicSleep &&
                bodyState.CompensationRefCount <= 0 &&
                bodyState.CullingLockRefCount <= 0 &&
                (bodyState.CullingFlags & PhysicsCullingFlags.IgnoreCulling) == 0;
            if (!cullingAllowed)
            {
                RestoreAllPhysicsCullingState(bodyIndex, body, ref bodyState, forceWake: true);
                return false;
            }

            bool stripMeshColliders = (command & CullingCommandStripMeshColliders) != 0;
            if (stripMeshColliders)
                ApplyMeshColliderStrip(bodyIndex, ref bodyState);
            else
                RestoreMeshColliderStrip(bodyIndex, ref bodyState);

            bool shouldKinematic = (command & CullingCommandKinematic) != 0;
            if (!shouldKinematic)
                RestoreDistanceKinematicCull(body, ref bodyState, forceWake: awakeResult != 0);

            bool shouldSleep = awakeResult == 0;
            if (shouldSleep)
                ApplyDistanceSleep(body, ref bodyState, distanceSq);
            else
                RestoreDistanceSleep(body, ref bodyState, forceWake: true);

            if (shouldKinematic)
                ApplyDistanceKinematicCull(body, ref bodyState);

            return bodyState.DistanceSleepActive ||
                bodyState.DistanceKinematicSleepActive ||
                bodyState.MeshColliderStripActive;
        }

        private void ApplyDistanceSleep(Rigidbody body, ref RigidbodyState bodyState, float distanceSq)
        {
            if (body == null)
                return;

            if (bodyState.DistanceSleepActive)
            {
                if (!body.IsSleeping())
                {
                    DampenBodyVelocityForSleep(body);
                    body.Sleep();
                }

                return;
            }

            bodyState.WasSleepingBeforeDistanceSleep = body.IsSleeping();
            if (bodyState.WasSleepingBeforeDistanceSleep)
                bodyState.StateMask |= PhysicsStateMask.WasAsleep;
            else
                bodyState.StateMask &= ~PhysicsStateMask.WasAsleep;

            DampenBodyVelocityForSleep(body);
            body.Sleep();
            bodyState.DistanceSleepActive = true;
            PublishRigidbodySleepSignal(in bodyState, body != null ? body.position : Vector3.zero, distanceSq, 1);
        }

        private static void DampenBodyVelocityForSleep(Rigidbody body)
        {
            if (body == null || body.isKinematic)
                return;

            Vector3 linearVelocity = body.linearVelocity;
            Vector3 angularVelocity = body.angularVelocity;
            body.linearVelocity = IsFinite(linearVelocity) ? linearVelocity * DistanceSleepVelocityDampening : Vector3.zero;
            body.angularVelocity = IsFinite(angularVelocity) ? angularVelocity * DistanceSleepVelocityDampening : Vector3.zero;
        }

        private void RestoreDistanceSleep(Rigidbody body, ref RigidbodyState bodyState, bool forceWake)
        {
            if (!bodyState.DistanceSleepActive)
                return;

            bool wasAsleepBeforeEviction = bodyState.WasSleepingBeforeDistanceSleep ||
                                           (bodyState.StateMask & PhysicsStateMask.WasAsleep) != 0;
            bodyState.WasSleepingBeforeDistanceSleep = false;
            bodyState.StateMask &= ~PhysicsStateMask.WasAsleep;
            bodyState.DistanceSleepActive = false;

            if (body != null && !body.isKinematic)
            {
                if (forceWake || !wasAsleepBeforeEviction)
                    body.WakeUp();
                else
                    body.Sleep();
            }

            PublishRigidbodySleepSignal(in bodyState, body != null ? body.position : Vector3.zero, 0d, 0);
        }

        private static void ApplyDistanceKinematicCull(Rigidbody body, ref RigidbodyState bodyState)
        {
            if (body == null)
                return;

            if (bodyState.DistanceKinematicSleepActive)
            {
                body.isKinematic = true;
                body.detectCollisions = false;
                if (!body.IsSleeping())
                    body.Sleep();

                return;
            }

            bodyState.KinematicModeBeforeDistanceSleep = body.isKinematic;
            bodyState.DetectCollisionsBeforeDistanceSleep = body.detectCollisions;
            body.isKinematic = true;
            body.detectCollisions = false;
            body.Sleep();
            bodyState.DistanceKinematicSleepActive = true;
        }

        private static void RestoreDistanceKinematicCull(Rigidbody body, ref RigidbodyState bodyState, bool forceWake)
        {
            if (!bodyState.DistanceKinematicSleepActive)
                return;

            if (body != null)
            {
                body.isKinematic = bodyState.KinematicModeBeforeDistanceSleep;
                body.detectCollisions = bodyState.DetectCollisionsBeforeDistanceSleep;
                if (forceWake && !body.isKinematic)
                    body.WakeUp();
            }

            bodyState.DistanceKinematicSleepActive = false;
        }

        private void RestoreAllPhysicsCullingState(int bodyIndex, Rigidbody body, ref RigidbodyState bodyState, bool forceWake)
        {
            if ((uint)bodyIndex < (uint)_trackedBodyCount)
                RestoreMeshColliderStrip(bodyIndex, ref bodyState);

            RestoreDistanceKinematicCull(body, ref bodyState, forceWake);
            RestoreDistanceSleep(body, ref bodyState, forceWake);
        }

        private static byte BuildPhysicsCullingStateSnapshot(in RigidbodyState bodyState)
        {
            byte state = 0;
            if (bodyState.DistanceSleepActive)
                state |= CullingStateSleepActive;
            if (bodyState.DistanceKinematicSleepActive)
                state |= CullingStateKinematicActive;
            if (bodyState.MeshColliderStripActive)
                state |= CullingStateMeshColliderStripped;
            if (!bodyState.AllowDistanceKinematicSleep ||
                bodyState.CompensationRefCount > 0 ||
                bodyState.CullingLockRefCount > 0 ||
                (bodyState.CullingFlags & PhysicsCullingFlags.IgnoreCulling) != 0)
            {
                state |= CullingStateIgnoreCulling;
            }
            if ((bodyState.CullingFlags & PhysicsCullingFlags.HeavyCollider) != 0 && bodyState.MeshColliderCount > 0)
                state |= CullingStateHeavyCollider;

            return state;
        }

        private static float ResolveSleepDistanceMeters()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            return tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350
                ? LowTierSleepDistanceMeters
                : DefaultSleepDistanceMeters;
        }

        private static float ResolveWakeDistanceMeters(float sleepDistanceMeters)
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            float configuredWake = tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350
                ? LowTierWakeDistanceMeters
                : DefaultWakeDistanceMeters;
            return math.min(configuredWake, sleepDistanceMeters - SleepWakeHysteresisMeters);
        }

        private static bool TryResolvePhysicsCullingPlayerState(
            out AbsoluteUniversePosition playerAup,
            out float3 cameraForward,
            out float depthMeters)
        {
            playerAup = default;
            cameraForward = new float3(0f, 0f, 1f);
            depthMeters = 0f;

            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null &&
                runtimeContext.IsBound)
            {
                PlayerMovementRuntimeState movementState = runtimeContext.MovementState;
                if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    playerAup = movementState.PredictedAup;
                    cameraForward = math.normalizesafe(movementState.CameraForward, new float3(0f, 0f, 1f));
                    float rawDepthMeters = movementState.DepthMeters;
                    depthMeters = math.isfinite(rawDepthMeters) ? math.max(0f, rawDepthMeters) : 0f;
                    return IsFinite(in playerAup) && math.all(math.isfinite(cameraForward));
                }
            }

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            if (playerMovement == null)
                return false;

            playerAup = playerMovement.CurrentAup;
            cameraForward = new float3(0f, 0f, 1f);
            float fallbackDepthMeters = playerMovement.CurrentDepth;
            depthMeters = math.isfinite(fallbackDepthMeters) ? math.max(0f, fallbackDepthMeters) : 0f;
            return IsFinite(in playerAup);
        }

        private void ApplyMeshColliderStrip(int bodyIndex, ref RigidbodyState bodyState)
        {
            if (bodyState.MeshColliderCount <= 0)
                return;

            if (bodyState.MeshColliderStripActive)
            {
                EnforceMeshColliderStrip(bodyIndex, bodyState.MeshColliderCount);
                return;
            }

            int baseIndex = bodyIndex * MaxMeshCollidersPerBody;
            int count = math.min((int)bodyState.MeshColliderCount, MaxMeshCollidersPerBody);
            for (int i = 0; i < count; i++)
            {
                int colliderIndex = baseIndex + i;
                MeshCollider meshCollider = _trackedMeshColliders[colliderIndex];
                if (meshCollider == null)
                {
                    _trackedMeshColliderEnabledBeforeStrip[colliderIndex] = 0;
                    continue;
                }

                _trackedMeshColliderEnabledBeforeStrip[colliderIndex] = meshCollider.enabled ? (byte)1 : (byte)0;
                meshCollider.enabled = false;
            }

            bodyState.MeshColliderStripActive = true;
        }

        private void EnforceMeshColliderStrip(int bodyIndex, byte meshColliderCount)
        {
            int baseIndex = bodyIndex * MaxMeshCollidersPerBody;
            int count = math.min((int)meshColliderCount, MaxMeshCollidersPerBody);
            for (int i = 0; i < count; i++)
            {
                MeshCollider meshCollider = _trackedMeshColliders[baseIndex + i];
                if (meshCollider != null && meshCollider.enabled)
                    meshCollider.enabled = false;
            }
        }

        private void RestoreMeshColliderStrip(int bodyIndex, ref RigidbodyState bodyState)
        {
            if (!bodyState.MeshColliderStripActive || bodyState.MeshColliderCount <= 0)
                return;

            int baseIndex = bodyIndex * MaxMeshCollidersPerBody;
            int count = math.min((int)bodyState.MeshColliderCount, MaxMeshCollidersPerBody);
            for (int i = 0; i < count; i++)
            {
                int colliderIndex = baseIndex + i;
                MeshCollider meshCollider = _trackedMeshColliders[colliderIndex];
                if (meshCollider != null)
                    meshCollider.enabled = _trackedMeshColliderEnabledBeforeStrip[colliderIndex] != 0;

                _trackedMeshColliderEnabledBeforeStrip[colliderIndex] = 0;
            }

            bodyState.MeshColliderStripActive = false;
        }

        private byte CacheMeshCollidersForBody(Rigidbody body, int bodyIndex)
        {
            ClearMeshColliderRefs(bodyIndex);
            if (body == null)
                return 0;

            _meshColliderScratch.Clear();
            body.GetComponentsInChildren(false, _meshColliderScratch);
            int count = math.min(_meshColliderScratch.Count, MaxMeshCollidersPerBody);
            int baseIndex = bodyIndex * MaxMeshCollidersPerBody;
            for (int i = 0; i < count; i++)
                _trackedMeshColliders[baseIndex + i] = _meshColliderScratch[i];

            _meshColliderScratch.Clear();
            return (byte)count;
        }

        private void MoveMeshColliderRefs(int sourceBodyIndex, int targetBodyIndex)
        {
            if (sourceBodyIndex == targetBodyIndex)
                return;

            int sourceBaseIndex = sourceBodyIndex * MaxMeshCollidersPerBody;
            int targetBaseIndex = targetBodyIndex * MaxMeshCollidersPerBody;
            for (int i = 0; i < MaxMeshCollidersPerBody; i++)
            {
                _trackedMeshColliders[targetBaseIndex + i] = _trackedMeshColliders[sourceBaseIndex + i];
                _trackedMeshColliderEnabledBeforeStrip[targetBaseIndex + i] = _trackedMeshColliderEnabledBeforeStrip[sourceBaseIndex + i];
            }
        }

        private void ClearMeshColliderRefs(int bodyIndex)
        {
            int baseIndex = bodyIndex * MaxMeshCollidersPerBody;
            for (int i = 0; i < MaxMeshCollidersPerBody; i++)
            {
                _trackedMeshColliders[baseIndex + i] = null;
                _trackedMeshColliderEnabledBeforeStrip[baseIndex + i] = 0;
            }
        }

        private void RecordPhysicsCullingTelemetry(
            in RigidbodyState bodyState,
            byte awakeResult,
            byte command,
            float distanceSq,
            int culledCount)
        {
            if (!_physicsCullingTelemetry.IsCreated)
                return;

            int writeIndex = _physicsCullingTelemetryWriteIndex;
            if ((uint)writeIndex >= PhysicsCullingTelemetryCapacity)
                writeIndex = 0;

            int nextWriteIndex = writeIndex + 1;
            _physicsCullingTelemetryWriteIndex = nextWriteIndex >= PhysicsCullingTelemetryCapacity
                ? 0
                : nextWriteIndex;

            _physicsCullingTelemetry[writeIndex] = new PhysicsCullingTelemetryEntry
            {
                FrameIndex = Time.frameCount,
                TrackedBodyCount = _trackedBodyCount,
                CulledBodyCount = culledCount,
                BodyId = unchecked((uint)bodyState.EntityId),
                DistanceSq = distanceSq,
                Command = command,
                AwakeResult = awakeResult,
                Flags = (byte)bodyState.CullingFlags,
                CcdInterventions = ResolveKinematicCcdInterventionsForFrame(Time.frameCount)
            };
        }

        private void ReportKinematicCcdInterventionInternal()
        {
            int frame = Time.frameCount;
            if (_kinematicCcdInterventionsFrame != frame)
            {
                _kinematicCcdInterventionsFrame = frame;
                _kinematicCcdInterventionsThisFrame = 0;
            }

            if (_kinematicCcdInterventionsThisFrame < ushort.MaxValue)
                _kinematicCcdInterventionsThisFrame++;
        }

        private ushort ResolveKinematicCcdInterventionsForFrame(int frame)
        {
            if (_kinematicCcdInterventionsFrame != frame)
                return 0;

            return (ushort)math.min(_kinematicCcdInterventionsThisFrame, ushort.MaxValue);
        }

        private void DumpPhysicsCullingBlackBox(string reason)
        {
            if (!_physicsCullingTelemetry.IsCreated)
                return;

            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string logDirectory = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(logDirectory);
                string dumpPath = Path.Combine(logDirectory, "Dump_PHYSICS_CULLING_OVERSEER.bin");
                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(reason ?? string.Empty);
                    writer.Write(_physicsCullingTelemetryWriteIndex);
                    writer.Write(PhysicsCullingTelemetryCapacity);
                    for (int i = 0; i < PhysicsCullingTelemetryCapacity; i++)
                    {
                        PhysicsCullingTelemetryEntry entry = _physicsCullingTelemetry[i];
                        writer.Write(entry.FrameIndex);
                        writer.Write(entry.TrackedBodyCount);
                        writer.Write(entry.CulledBodyCount);
                        writer.Write(entry.BodyId);
                        writer.Write(entry.DistanceSq);
                        writer.Write(entry.Command);
                        writer.Write(entry.AwakeResult);
                        writer.Write(entry.Flags);
                        writer.Write(entry.Reserved);
                        writer.Write(entry.CcdInterventions);
                    }
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[GlobalPhysicsStateManager] Failed to dump physics culling black box: " + exception.Message);
#endif
            }
        }

        void IAcousticPingEventListener.OnAcousticPing(in AcousticPingEvent pingEvent)
        {
            if (!IsFinite(pingEvent.RuntimePosition))
                return;

            float radiusMeters = math.clamp(
                pingEvent.RadiusMeters * math.max(pingEvent.Intensity01, 0.25f),
                AcousticWakeMinimumRadiusMeters,
                AcousticWakeMaximumRadiusMeters);
            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(pingEvent.RuntimePosition);
            WakeCulledBodiesNear(in originAup, radiusMeters);
        }

        void IPhysicsAcousticImpulseEventListener.OnAcousticImpulse(in AcousticImpulseEvent impulseEvent)
        {
            if (!IsFinite(impulseEvent.RuntimePosition))
                return;

            float kineticEnergy = math.max(0f, impulseEvent.KineticEnergyJoules);
            float energyRadius = kineticEnergy > 0f
                ? kineticEnergy * math.rsqrt(math.max(kineticEnergy, 0.000001f)) * 0.1f
                : 0f;
            float radiusMeters = math.clamp(
                math.max(impulseEvent.RadiusMeters, energyRadius) * math.max(impulseEvent.Volume01, 0.25f),
                AcousticWakeMinimumRadiusMeters,
                AcousticWakeMaximumRadiusMeters);
            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(impulseEvent.RuntimePosition);
            WakeCulledBodiesNear(in originAup, radiusMeters);
        }

        void IPhysicsImpactEventListener.OnPhysicsImpact(in PhysicsImpactSignal impactSignal)
        {
            float radiusMeters = math.clamp(
                ImpactWakeMinimumRadiusMeters + (math.max(impactSignal.Intensity, 0f) * 12f),
                ImpactWakeMinimumRadiusMeters,
                ImpactWakeMaximumRadiusMeters);
            AbsoluteUniversePosition originAup = impactSignal.ResolvePointAup();
            WakeCulledBodiesNear(in originAup, radiusMeters);
        }

        private void WakeCulledBodiesNear(in AbsoluteUniversePosition originAup, float radiusMeters)
        {
            if (HectonFloatingOrigin.IsShiftInProgress ||
                radiusMeters <= 0f ||
                !math.isfinite(radiusMeters) ||
                !IsFinite(in originAup))
            {
                return;
            }

            CompletePhysicsCullingJobForStateMutation(discardResults: true);
            double radiusSq = (double)radiusMeters * radiusMeters;
            for (int i = _trackedBodyCount - 1; i >= 0; i--)
            {
                Rigidbody body = _trackedBodies[i];
                if (body == null)
                {
                    RemoveTrackedBodyAt(i);
                    continue;
                }

                RigidbodyState bodyState = _bodyStates[i];
                if (!bodyState.DistanceSleepActive &&
                    !bodyState.DistanceKinematicSleepActive &&
                    !bodyState.MeshColliderStripActive)
                {
                    continue;
                }

                if (!TryResolveTrackedBodyAup(body, ref bodyState, out AbsoluteUniversePosition bodyAup))
                {
                    _bodyStates[i] = bodyState;
                    continue;
                }

                if (AbsoluteUniversePosition.DistanceSq(in bodyAup, in originAup) > radiusSq)
                {
                    _bodyStates[i] = bodyState;
                    continue;
                }

                RestoreAllPhysicsCullingState(i, body, ref bodyState, forceWake: true);
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

        private static void PublishRigidbodySleepSignal(
            in RigidbodyState bodyState,
            in AbsoluteUniversePosition bodyAup,
            double distanceSq,
            byte sleepState)
        {
            double safeDistanceSq = math.max(0.0, distanceSq);
            float distanceMeters = safeDistanceSq > 0.0
                ? (float)math.min(1000000.0, safeDistanceSq * math.rsqrt(math.max(safeDistanceSq, 0.000001)))
                : 0f;
            RigidbodySleepSignal signal = new RigidbodySleepSignal
            {
                PositionAup = bodyAup,
                BodyId = unchecked((uint)bodyState.EntityId),
                DistanceMeters = distanceMeters,
                SleepState = sleepState,
                Flags = bodyState.DistanceKinematicSleepActive ? (byte)1 : (byte)0
            };
            GlobalSignals.Publish(in signal);
        }

        private static void PublishRigidbodySleepSignal(
            in RigidbodyState bodyState,
            Vector3 runtimePosition,
            double distanceSq,
            byte sleepState)
        {
            AbsoluteUniversePosition bodyAup = bodyState.HasLastValidAup
                ? bodyState.LastValidAup
                : AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            PublishRigidbodySleepSignal(in bodyState, in bodyAup, distanceSq, sleepState);
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
            CompletePhysicsCullingJobForStateMutation(discardResults: true);
            int lastIndex = _trackedBodyCount - 1;
            if (bodyIndex < 0 || bodyIndex > lastIndex)
                return;

            Rigidbody removedBody = _trackedBodies[bodyIndex];
            if (removedBody != null)
            {
                RigidbodyState removedState = _bodyStates[bodyIndex];
                RestoreColliderLodGate(ref removedState);
                RestoreSafeTeleportSpeculativeCcd(removedBody, ref removedState);
                RestoreAllPhysicsCullingState(bodyIndex, removedBody, ref removedState, forceWake: false);
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
            MoveMeshColliderRefs(lastIndex, bodyIndex);
            ClearMeshColliderRefs(lastIndex);
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
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null &&
                runtimeContext.IsBound)
            {
                PlayerMovementRuntimeState movementState = runtimeContext.MovementState;
                if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                    IsFinite(in movementState.PredictedAup))
                {
                    playerAup = movementState.PredictedAup;
                    return true;
                }
            }

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

        private static PhysicsCullingFlags ResolveCullingFlags(Rigidbody body)
        {
            if (body == null)
                return PhysicsCullingFlags.IgnoreCulling;

            PhysicsCullingFlags flags = PhysicsCullingFlags.None;
            if (body.TryGetComponent(out IPhysicsCullingFlagProvider directProvider))
                flags |= directProvider.CullingFlags;

            IPhysicsCullingFlagProvider parentProvider = body.GetComponentInParent<IPhysicsCullingFlagProvider>();
            if (parentProvider != null && !ReferenceEquals(parentProvider, directProvider))
                flags |= parentProvider.CullingFlags;

            if (body.CompareTag("Player") ||
                body.TryGetComponent(out HectonPlayerMotor _) ||
                body.TryGetComponent(out MountablePlayerTransport _) ||
                body.TryGetComponent(out SubmarineCoreDirector _))
            {
                flags |= PhysicsCullingFlags.IgnoreCulling;
            }

            return flags;
        }

        private static bool ShouldAllowDistanceKinematicSleep(Rigidbody body, PhysicsCullingFlags cullingFlags)
        {
            if (body == null)
                return false;

            if ((cullingFlags & PhysicsCullingFlags.IgnoreCulling) != 0)
                return false;

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
            CompletePhysicsCullingJobForStateMutation(discardResults: true);
            if (_impactEvents.IsCreated)
            {
                int impactClearCount = math.min(_impactEvents.Length, MaxQueuedImpactEvents);
                for (int i = 0; i < impactClearCount; i++)
                    _impactEvents[i] = default;
            }

            _queuedImpactCount = 0;
            _impactEventReadIndex = 0;
            _impactEventWriteIndex = 0;

            for (int i = 0; i < _trackedBodyCount; i++)
            {
                Rigidbody body = _trackedBodies[i];
                if (body == null)
                    continue;

                RigidbodyState bodyState = _bodyStates[i];
                RestoreColliderLodGate(ref bodyState);
                RestoreSafeTeleportSpeculativeCcd(body, ref bodyState);
                RestoreAllPhysicsCullingState(i, body, ref bodyState, forceWake: false);
                if (bodyState.AddedMassTensorApplied)
                    RestoreAddedMassBaseline(body, ref bodyState);
            }

            Array.Clear(_trackedBodies, 0, _trackedBodyCount);
            Array.Clear(_bodyStates, 0, _trackedBodyCount);
            Array.Clear(_trackedMeshColliders, 0, MaxTrackedMeshColliderRefs);
            Array.Clear(_trackedMeshColliderEnabledBeforeStrip, 0, MaxTrackedMeshColliderRefs);
            Array.Clear(_connections, 0, _connectionCount);
            _trackedBodyIndexByEntityId.Clear();
            if (_lastValidPositions.IsCreated)
            {
                for (int i = 0; i < _lastValidPositions.Length; i++)
                    _lastValidPositions[i] = default;
            }

            if (_rigidbodyAUPs.IsCreated)
            {
                int nativeClearCount = math.min(
                    _rigidbodyAUPs.Length,
                    math.min(
                        _rigidbodyCullingStateSnapshot.IsCreated ? _rigidbodyCullingStateSnapshot.Length : 0,
                        math.min(
                            _rigidbodyAwakeResults.IsCreated ? _rigidbodyAwakeResults.Length : 0,
                            math.min(
                                _rigidbodyCullingCommandResults.IsCreated ? _rigidbodyCullingCommandResults.Length : 0,
                                _rigidbodyDistanceSqResults.IsCreated ? _rigidbodyDistanceSqResults.Length : 0))));
                for (int i = 0; i < nativeClearCount; i++)
                {
                    _rigidbodyAUPs[i] = default;
                    _rigidbodyCullingStateSnapshot[i] = default;
                    _rigidbodyAwakeResults[i] = default;
                    _rigidbodyCullingCommandResults[i] = default;
                    _rigidbodyDistanceSqResults[i] = default;
                }
            }

            if (_physicsCullingTelemetry.IsCreated)
            {
                int telemetryClearCount = math.min(_physicsCullingTelemetry.Length, PhysicsCullingTelemetryCapacity);
                for (int i = 0; i < telemetryClearCount; i++)
                    _physicsCullingTelemetry[i] = default;
            }

            _trackedBodyCount = 0;
            _connectionCount = 0;
            _queuedImpactCount = 0;
            _impactEventReadIndex = 0;
            _impactEventWriteIndex = 0;
            _culledBodyCount = 0;
            _physicsCullingJobCount = 0;
            _physicsCullingTelemetryWriteIndex = 0;
            _physicsCullingSlowTickAccumulator = 0f;
            _aupJitterSentinelCountdown = 0;
            _aupJitterSentinelCachedFrame = -1;
            _aupJitterSentinelDueThisFrame = false;
            _connectionCapacityOverflowReported = false;
            _trackedBodyCapacityOverflowReported = false;
            _nativeStateAllocationFailureReported = false;
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

        private static bool IsFinite(in AbsoluteUniversePosition value)
        {
            return math.isfinite(value.LocalX) &&
                math.isfinite(value.LocalY) &&
                math.isfinite(value.LocalZ);
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
