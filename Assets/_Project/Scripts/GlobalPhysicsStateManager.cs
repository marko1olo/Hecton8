using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Celestial;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Memory.Layout;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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

    [Flags]
    internal enum PhysicsStateMask : byte
    {
        None = 0,
        WasAsleep = 1 << 4,
        NanDetected = 1 << 6
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
        /// Queues a deferred wake pulse for culling-managed bodies near an authoritative AUP event origin.
        /// </summary>
        /// <param name="originAup">Event origin.</param>
        /// <param name="radiusMeters">Wake radius in meters.</param>
        void WakeBodiesNear(in AbsoluteUniversePosition originAup, float radiusMeters);

        /// <summary>
        /// Queues a targeted wake request from raymarched impacts or other non-PhysX gameplay events.
        /// </summary>
        /// <param name="request">Unmanaged wake request payload.</param>
        void QueueWakeRequest(in WakeRequestSignal request);

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
    /// Static zero-instance gameplay event bus for deferred physics-impact feedback.
    /// </summary>
    public static class PhysicsEvents
    {
        private const int ListenerCapacity = 16;

        private static readonly ListenerSlot[] _impactListeners = new ListenerSlot[ListenerCapacity]; // COLD ALLOC: ListenerSlot[16] - deferred physics impact listeners - owner: PhysicsEvents
        private static int _impactListenerCount;

        internal static bool HasImpactListeners => _impactListenerCount > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < _impactListenerCount; i++)
                _impactListeners[i].Clear();
            _impactListenerCount = 0;
        }

        /// <summary>
        /// Registers one deferred physics-impact listener.
        /// </summary>
        /// <param name="listener">Listener to register.</param>
        public static void Register(IPhysicsImpactEventListener listener)
        {
            if (listener != null)
                RegisterImmediate(listener);
        }

        /// <summary>
        /// Unregisters one deferred physics-impact listener.
        /// </summary>
        /// <param name="listener">Listener to unregister.</param>
        public static void Unregister(IPhysicsImpactEventListener listener)
        {
            if (listener != null)
                TryUnregisterImmediate(listener);
        }

        internal static bool TryNotifyImpact(in PhysicsImpactSignal impactSignal)
        {
            int count = _impactListenerCount;
            if (count <= 0)
                return false;

            for (int i = count - 1; i >= 0; i--)
            {
                IPhysicsImpactEventListener listener = _impactListeners[i].Listener;
                if (listener != null)
                    listener.OnPhysicsImpact(in impactSignal);
            }

            return true;
        }

        [Obsolete("Physics impact dispatch must use TryNotifyImpact so listener absence is explicit.", true)]
        internal static void RaiseImpact(in PhysicsImpactSignal impactSignal)
        {
            TryNotifyImpact(in impactSignal);
        }

        private static void RegisterImmediate(IPhysicsImpactEventListener listener)
        {
            if (ContainsImmediate(listener) || _impactListenerCount >= ListenerCapacity)
                return;

            _impactListeners[_impactListenerCount].Listener = listener;
            _impactListenerCount++;
        }

        private static bool TryUnregisterImmediate(IPhysicsImpactEventListener listener)
        {
            for (int i = 0; i < _impactListenerCount; i++)
            {
                if (!ReferenceEquals(_impactListeners[i].Listener, listener))
                    continue;

                int lastIndex = _impactListenerCount - 1;
                _impactListeners[i] = _impactListeners[lastIndex];
                _impactListeners[lastIndex].Clear();
                _impactListenerCount = lastIndex;
                return true;
            }

            return false;
        }

        private static bool ContainsImmediate(IPhysicsImpactEventListener listener)
        {
            for (int i = 0; i < _impactListenerCount; i++)
            {
                if (ReferenceEquals(_impactListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private struct ListenerSlot
        {
            public IPhysicsImpactEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }
    }

    /// <summary>
    /// Authoritative runtime registry for active rigidbodies, mass-ratio guards, and queued impact feedback.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8995)]
    public sealed partial class GlobalPhysicsStateManager : MonoBehaviour, IFixedTickable, ILateFrameTickable, IPostFixedTickable, IOriginShiftListener, IAcousticPingEventListener, IPhysicsAcousticImpulseEventListener, IPhysicsImpactEventListener, IPhysicsCullingOverseer, IPhysicsStateEventService, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private static int s_x001GlobalPhysicsStateManagerSignalPushDropCount;
        private struct VaultBufferBinding<T>
            where T : struct
        {
            public VaultGenerationHandle<T> Handle;
            public BufferID BufferId;
            public int RequiredLength;
            public SystemID OwnerSystemId;
            private IDataVault CachedDataVault;
            private IDataVault WriteLockVault;
            private bool WriteLockHeld;

            public VaultBufferBinding(BufferID bufferId, int requiredLength, SystemID ownerSystemId)
            {
                Handle = default;
                BufferId = bufferId;
                RequiredLength = requiredLength;
                OwnerSystemId = ownerSystemId;
                CachedDataVault = null;
                WriteLockVault = null;
                WriteLockHeld = false;
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
                IDataVault dataVault = ResolveDataVault();
                if (dataVault == null || RequiredLength <= 0)
                {
                    Handle = default;
                    return false;
                }

                NativeArray<T> buffer = ResolveExisting(dataVault);
                if (!buffer.IsCreated || buffer.Length < RequiredLength)
                {
                    if (dataVault.IsAllocationLocked || dataVault.IsCompactionFenceActive)
                        return false;

                    Handle = dataVault.EnsureGenerationHandle<T>(
                        BufferId,
                        RequiredLength,
                        OwnerSystemId,
                        options);
                    buffer = ResolveExisting(dataVault);
                }

                return buffer.IsCreated && buffer.Length >= RequiredLength;
            }

            public void BindDataVault(IDataVault dataVault)
            {
                if (dataVault != null)
                    CachedDataVault = dataVault;
            }

            public NativeArray<T> AsNativeArray()
            {
                return ResolveExisting();
            }

            public void ReleaseView()
            {
                if (WriteLockHeld)
                    ReleaseWriteLock();
                Handle = default;
                WriteLockVault = null;
                WriteLockHeld = false;
            }

            public bool TryAcquireWriteLock(out NativeArray<T> buffer)
            {
                buffer = default;
                if (WriteLockHeld)
                {
                    IDataVault writeLockVault = WriteLockVault;
                    if (writeLockVault == null)
                    {
                        WriteLockHeld = false;
                        return false;
                    }

                    buffer = ResolveExisting(writeLockVault);
                    return buffer.IsCreated;
                }

                IDataVault dataVault = ResolveDataVault();
                if (dataVault == null || !IsHandleValid())
                    return false;

                if (!dataVault.TryAcquireWriteLock(in Handle, OwnerSystemId, out buffer))
                    return false;

                bool ownershipTransferred = false;
                try
                {
                    if (buffer.IsCreated && buffer.Length >= RequiredLength)
                    {
                        WriteLockVault = dataVault;
                        WriteLockHeld = true;
                        ownershipTransferred = true;
                        return true;
                    }

                    buffer = default;
                    return false;
                }
                finally
                {
                    if (!ownershipTransferred)
                        dataVault.ReleaseWriteLock(in Handle, OwnerSystemId);
                }
            }

            public bool ReleaseWriteLock()
            {
                if (!WriteLockHeld)
                    return false;

                IDataVault dataVault = WriteLockVault;
                bool released = dataVault != null &&
                    IsHandleValid() &&
                    dataVault.ReleaseWriteLock(in Handle, OwnerSystemId);
                WriteLockHeld = false;
                WriteLockVault = null;
                if (!released)
                    return false;

                return true;
            }

            public bool TryResolve(out NativeArray<T> buffer)
            {
                buffer = ResolveExisting();
                return buffer.IsCreated && buffer.Length >= RequiredLength;
            }

            public bool HasValidView()
            {
                NativeArray<T> buffer = ResolveExisting();
                return buffer.IsCreated && buffer.Length >= RequiredLength;
            }

            public T this[int index]
            {
                get
                {
                    NativeArray<T> buffer = ResolveExisting();
                    return buffer.IsCreated && (uint)index < (uint)buffer.Length
                        ? buffer[index]
                        : default;
                }
                set
                {
                    if (WriteLockHeld)
                    {
                        NativeArray<T> lockedBuffer = ResolveExisting();
                        if (lockedBuffer.IsCreated && (uint)index < (uint)lockedBuffer.Length)
                            lockedBuffer[index] = value;
                        return;
                    }

                    if (!TryAcquireWriteLock(out NativeArray<T> buffer))
                        return;

                    try
                    {
                        if (buffer.IsCreated && (uint)index < (uint)buffer.Length)
                            buffer[index] = value;
                    }
                    finally
                    {
                        ReleaseWriteLock();
                    }
                }
            }

            public static implicit operator NativeArray<T>(VaultBufferBinding<T> binding)
            {
                return binding.AsNativeArray();
            }

            NativeArray<T> ResolveExisting()
            {
                return ResolveExisting(ResolveDataVault());
            }

            NativeArray<T> ResolveExisting(IDataVault dataVault)
            {
                if (dataVault == null || !IsHandleValid())
                    return default;

                return dataVault.TryResolveHandle(in Handle, out NativeArray<T> buffer)
                    ? buffer
                    : default;
            }

            IDataVault ResolveDataVault()
            {
                return CachedDataVault;
            }

            private bool IsHandleValid()
            {
                return Handle.BufferID == unchecked((uint)(int)BufferId) &&
                       Handle.Generation != 0u;
            }
        }

        private struct RigidbodyState
        {
            public ulong EntityId;
            public int EntityInstanceHash;
            public PhysicsStateMask StateMask;
            public int CompensationRefCount;
            public int CullingLockRefCount;
            public float MaxAngularVelocityClamp;
            public byte AllowDistanceKinematicSleep;
            public byte DistanceSleepActive;
            public byte DistanceKinematicSleepActive;
            public byte MeshColliderStripActive;
            public byte HasLastValidPosition;
            public byte HasLastValidAup;
            public byte HasOriginShiftSnapshot;
            public byte WasSleepingBeforeOriginShift;
            public byte WasSleepingBeforeDistanceSleep;
            public byte InterpolationSuspendedForOriginShift;
            public byte CollisionDetectionOverriddenForOriginShift;
            public byte SafeTeleportSpeculativeCcdActive;
            public byte KinematicModeBeforeDistanceSleep;
            public byte DetectCollisionsBeforeDistanceSleep;
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
            public byte HasColliderLodSink;
            public byte ColliderLodDistanceGateOpen;
            public byte IsFullySubmerged;
            public byte HasAddedMassBaseline;
            public byte AddedMassTensorApplied;
            public byte AddedMassDirty;
            public PhysicsCullingFlags CullingFlags;
            public byte MeshColliderCount;
            public byte SleepColliderCount;
            public byte CollidersDisabledByDistanceSleep;
        }

        private struct PhysicsConnection
        {
            public UnityEngine.Object Owner;
            public Rigidbody BodyA;
            public Rigidbody BodyB;
            public Rigidbody CompensatedBody;
            public PhysicsConnectionKind Kind;
            public byte CompensationActive;
        }

        [StructLayout(LayoutKind.Explicit, Size = 112)]
        private struct PhysicsImpactEventData
        {
            [FieldOffset(0)] public AbsoluteUniversePosition PointAup;
            [FieldOffset(48)] public ulong PrimaryBodyId;
            [FieldOffset(56)] public ulong SecondaryBodyId;
            [FieldOffset(64)] public float3 Point;
            [FieldOffset(76)] public float3 Normal;
            [FieldOffset(88)] public float Force;
            [FieldOffset(92)] public float Intensity;
            [FieldOffset(96)] public float MassVelocity;
            [FieldOffset(100)] public PhysicsImpactWeightClass WeightClass;
            [FieldOffset(101)] public byte PrimaryAudioMaterialId;
            [FieldOffset(102)] public byte SecondaryAudioMaterialId;
            [FieldOffset(103)] private byte _pad0;
            [FieldOffset(104)] private byte _pad1;
            [FieldOffset(105)] private byte _pad2;
            [FieldOffset(106)] private byte _pad3;
            [FieldOffset(107)] private byte _pad4;
            [FieldOffset(108)] private byte _pad5;
            [FieldOffset(109)] private byte _pad6;
            [FieldOffset(110)] private byte _pad7;
            [FieldOffset(111)] private byte _pad8;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct PhysicsCullingTelemetryEntry
        {
            [FieldOffset(0)] public int FrameIndex;
            [FieldOffset(4)] public int TrackedBodyCount;
            [FieldOffset(8)] public int CulledBodyCount;
            [FieldOffset(12)] public int LockContentions;
            [FieldOffset(16)] public uint BodyId;
            [FieldOffset(20)] public uint StateHash;
            [FieldOffset(24)] public float DistanceSq;
            [FieldOffset(28)] public float JobMicroseconds;
            [FieldOffset(32)] public float StateSyncMicroseconds;
            [FieldOffset(36)] public float GlobalQualityWeight;
            [FieldOffset(40)] public uint CullingFlags;
            [FieldOffset(44)] public uint FrameHash;
            [FieldOffset(48)] public uint Reserved0;
            [FieldOffset(52)] public ushort CcdInterventions;
            [FieldOffset(54)] public byte Command;
            [FieldOffset(55)] public byte AwakeResult;
            [FieldOffset(56)] public byte Flags;
            [FieldOffset(57)] public byte Reserved;
            [FieldOffset(58)] private byte _pad0;
            [FieldOffset(59)] private byte _pad1;
            [FieldOffset(60)] private byte _pad2;
            [FieldOffset(61)] private byte _pad3;
            [FieldOffset(62)] private byte _pad4;
            [FieldOffset(63)] private byte _pad5;
        }

        private const int MaxTrackedBodies = 2048;
        private const int MaxTrackedConnections = 128;
        private const int MaxConnectionLockTouchedBodies = MaxTrackedConnections * 3;
        private const int MaxQueuedImpactEvents = 256;
        private const int PhysicsCullingTelemetryCapacity = 300;
        private const uint PhysicsCullingRigidBodyNanHash = 0x50434E41u;
        private const uint PhysicsCullingStateSyncOverBudgetHash = 0x50435359u;
        private const uint PhysicsCullingInvalidInputHash = 0x5043494Eu;
        private const int MaxImpactFlushIterations = MaxQueuedImpactEvents;
        private const int MaxMeshCollidersPerBody = 4;
        private const int MaxTrackedMeshColliderRefs = MaxTrackedBodies * MaxMeshCollidersPerBody;
        private const int MaxSleepCollidersPerBody = 4;
        private const int MaxTrackedSleepColliderRefs = MaxTrackedBodies * MaxSleepCollidersPerBody;
        private const int SceneRootScanCapacity = 128;
        private const int SceneRigidbodyScanCapacity = MaxTrackedBodies;
        private const float MinMass = 0.0001f;
        private const float MassRatioThreshold = 100f;
        private const float MinImpactForce = 0.01f;
        private const float HeavyImpactIntensity = 0.95f;
        private const float MediumImpactIntensity = 0.45f;
        private const float DefaultSleepDistanceMeters = 50f;
        private const float DefaultWakeDistanceMeters = 45f;
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
        private const float QuaternionMagnitudeEpsilonSq = 0.000001f;
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
        private const SystemID OwnerSystemId = SystemID.GlobalPhysicsStateManager;
        private static readonly ulong PhysicsCullingSchedulingMutationGuardMask1337 =
            PhysicsVaultMutationGuardBit(BufferID.RigidbodyAUPs) |
            PhysicsVaultMutationGuardBit(BufferID.RigidbodyCullingState) |
            PhysicsVaultMutationGuardBit(BufferID.RigidbodyAwakeResults) |
            PhysicsVaultMutationGuardBit(BufferID.RigidbodyCullingCommands) |
            PhysicsVaultMutationGuardBit(BufferID.RigidbodyDistanceSq) |
            PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingDtos) |
            PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingStateAges) |
            PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingSpatialCandidates) |
            PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingSpatialCandidateMask) |
            PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingMockSeismicSignals) |
            PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingSpatialBucketHeads) |
            PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingSpatialNext) |
            PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingSpatialCellHashes) |
            PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingChangedIndices) |
            PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingChangedCount) |
            PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingFrameTelemetry);
        private static readonly ulong PhysicsCullingDispatchMutationGuardMask1337 =
            PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingChangedIndices) |
            PhysicsVaultMutationGuardBit(BufferID.ShinobuPhysicsCullingChangedCount) |
            PhysicsVaultMutationGuardBit(BufferID.RigidbodyAwakeResults) |
            PhysicsVaultMutationGuardBit(BufferID.RigidbodyCullingCommands) |
            PhysicsVaultMutationGuardBit(BufferID.RigidbodyDistanceSq);
        private static readonly uint _nanRecoverySystemHash = unchecked((uint)LocHash.Compute(nameof(GlobalPhysicsStateManager)));
        private static readonly uint _kinematicHitStopHash = unchecked((uint)LocHash.Compute("GlobalPhysicsStateManager.KinematicHitStop"));
        private static GlobalPhysicsStateManager s_runtimeManager;

        // COLD ALLOC: Rigidbody[512 initial] â€” authoritative tracked rigidbody registry â€” owner: GlobalPhysicsStateManager
        private Rigidbody[] _trackedBodies = new Rigidbody[MaxTrackedBodies];
        // COLD ALLOC: RigidbodyState[512 initial] â€” per-body runtime state and compensation flags â€” owner: GlobalPhysicsStateManager
        private RigidbodyState[] _bodyStates = new RigidbodyState[MaxTrackedBodies];
        // COLD ALLOC: AupSyncFenceEntry[300] — 300-frame origin shift sync fence — owner: GlobalPhysicsStateManager
        private readonly AupSyncFenceEntry[] _syncFenceRing = new AupSyncFenceEntry[300];
        private int _syncFenceRingCursor;
        // COLD ALLOC: PhysicsConnection[128] â€” tracked tether/dock connection registry â€” owner: GlobalPhysicsStateManager
        private readonly PhysicsConnection[] _connections = new PhysicsConnection[MaxTrackedConnections];
        // COLD ALLOC: int[384] - previous-frame connection lock body indices for O(k) ref clearing - owner: GlobalPhysicsStateManager
        private readonly int[] _connectionLockTouchedBodyIndices = new int[MaxConnectionLockTouchedBodies];
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
        // COLD ALLOC: Collider[8192] - cached collider refs for distance sleep disable/restore - owner: GlobalPhysicsStateManager
        private readonly Collider[] _trackedSleepColliders = new Collider[MaxTrackedSleepColliderRefs];
        // COLD ALLOC: byte[8192] - pre-sleep collider enabled flags for restoration - owner: GlobalPhysicsStateManager
        private readonly byte[] _trackedSleepColliderEnabledBeforeSleep = new byte[MaxTrackedSleepColliderRefs];
        // COLD ALLOC: Dictionary<int,int>[2048] - Unity instance-id to tracked index for O(1) wake signals - owner: GlobalPhysicsStateManager
        private readonly Dictionary<int, int> _trackedBodyIndexByInstanceId = new Dictionary<int, int>(MaxTrackedBodies);
        // COLD ALLOC: int[2048] - bounded dirty body queue for added-mass tensor updates - owner: GlobalPhysicsStateManager
        private readonly int[] _addedMassDirtyBodyIndices = new int[MaxTrackedBodies];

        private VaultBufferBinding<float3> _lastValidPositions = new VaultBufferBinding<float3>(BufferID.RigidbodyLastValidPositions, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<double3> _rigidbodyAUPs = new VaultBufferBinding<double3>(BufferID.RigidbodyAUPs, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<byte> _rigidbodyCullingStateSnapshot = new VaultBufferBinding<byte>(BufferID.RigidbodyCullingState, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<byte> _rigidbodyAwakeResults = new VaultBufferBinding<byte>(BufferID.RigidbodyAwakeResults, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<byte> _rigidbodyCullingCommandResults = new VaultBufferBinding<byte>(BufferID.RigidbodyCullingCommands, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<float> _rigidbodyDistanceSqResults = new VaultBufferBinding<float>(BufferID.RigidbodyDistanceSq, MaxTrackedBodies, OwnerSystemId);
        private VaultBufferBinding<PhysicsCullingTelemetryEntry> _physicsCullingTelemetry = new VaultBufferBinding<PhysicsCullingTelemetryEntry>(BufferID.PhysicsCullingTelemetry, PhysicsCullingTelemetryCapacity, OwnerSystemId);
        private VaultBufferBinding<PhysicsImpactEventData> _impactEvents = new VaultBufferBinding<PhysicsImpactEventData>(BufferID.PhysicsImpactEvents, MaxQueuedImpactEvents, OwnerSystemId);
        // COLD ALLOC: PhysicsImpactEventData[256] - fixed scratch for draining impact events after Vault lock release - owner: GlobalPhysicsStateManager
        private readonly PhysicsImpactEventData[] _impactFlushScratch = new PhysicsImpactEventData[MaxImpactFlushIterations];
        // COLD ALLOC: int[2048] - fixed result indices copied before dispatch Vault locks are released - owner: GlobalPhysicsStateManager
        private readonly int[] _physicsCullingDispatchIndexScratch = new int[MaxTrackedBodies];
        // COLD ALLOC: byte[2048] - fixed awake result snapshot copied before dispatch Vault locks are released - owner: GlobalPhysicsStateManager
        private readonly byte[] _physicsCullingDispatchAwakeScratch = new byte[MaxTrackedBodies];
        // COLD ALLOC: byte[2048] - fixed command result snapshot copied before dispatch Vault locks are released - owner: GlobalPhysicsStateManager
        private readonly byte[] _physicsCullingDispatchCommandScratch = new byte[MaxTrackedBodies];
        // COLD ALLOC: float[2048] - fixed distance result snapshot copied before dispatch Vault locks are released - owner: GlobalPhysicsStateManager
        private readonly float[] _physicsCullingDispatchDistanceSqScratch = new float[MaxTrackedBodies];
        private Rigidbody _submarineHullBody;
        private ITickDispatcher _tickDispatcher;
        private ICelestialSkyDirectionReadModel _celestialEngineRuntime;
        private ICelestialRuntimeSnapshotReadModel _celestialSnapshotReadModel;
        private IDataVault _nativeStateDataVault;
        private JobHandle _physicsCullingJobHandle;
        private int _trackedBodyCount;
        private int _connectionCount;
        private int _connectionLockTouchedBodyCount;
        private int _queuedImpactCount;
        private int _impactEventReadIndex;
        private int _impactEventWriteIndex;
        private int _physicsCullingJobCount;
        private int _physicsCullingLockContentionsThisFrame;
        private int _culledBodyCount;
        private int _physicsCullingTelemetryWriteIndex;
        private long _physicsCullingJobScheduleTicks;
        private float _physicsCullingLastJobMicroseconds;
        private float _physicsCullingInvalidInputDumpScalar;
        private float _colliderLodHysteresisAccumulator;
        private byte _physicsCullingInvalidInputDumpPending;
        private int _addedMassDirtyCount;
        private int _kinematicCcdInterventionsFrame = -1;
        private int _kinematicCcdInterventionsThisFrame;
        private bool _serviceRegistered;
        private bool _isInitialized;
        private bool _registeredHotSwapListener;
        private bool _registeredFixedTick;
        private bool _registeredLateFrameTick;
        private bool _registeredPostFixedTick;
        private bool _registeredOriginShift;
        private bool _physicsCullingEventBusRegistered;
        private bool _physicsCullingJobScheduled;
        private bool _physicsCullingJobDiscardRequested;
        private bool _sceneEventsSubscribed;
        private bool _connectionCapacityOverflowReported;
        private bool _connectionRefsRequireFullClear = true;
        private bool _deferredNullTrackedBodyCleanup;
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
        private static CelestialRuntimeSnapshot _cachedCelestialRuntimeSnapshot;
        private static uint _cachedCelestialRuntimeSnapshotSequence;

        /// <summary>
        /// Frame-stable cinematic water level. Consumers read this instead of recomputing tide sine waves.
        /// </summary>
        public static float CachedCurrentWaterLevelY => _cachedCurrentWaterLevelY;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _isInitialized;

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public void QueueKinematicImpactEvent(
            Rigidbody primaryBody,
            Rigidbody secondaryBody,
            Vector3 point,
            Vector3 normal,
            float impactSpeedMetersPerSecond)
        {
            if (primaryBody == null)
                return;

            QueueKinematicImpactInternal(primaryBody, secondaryBody, point, normal, impactSpeedMetersPerSecond);
        }

        /// <inheritdoc />
        void IPhysicsStateEventService.SetHydrodynamicSubmersion(Rigidbody body, float submersionFactor)
        {
            if (body == null || !_isInitialized || !HasRequiredNativeState())
                return;

            SetHydrodynamicSubmersionInternal(body, submersionFactor);
        }

        /// <inheritdoc />
        void IPhysicsStateEventService.RegisterBodyStateTracking(Rigidbody body)
        {
            if (body == null || !_isInitialized || !HasRequiredNativeState())
                return;

            RegisterTrackedBodyInternal(body, PhysicsCullingFlags.None);
        }

        /// <inheritdoc />
        void IPhysicsStateEventService.UnregisterBodyStateTracking(Rigidbody body)
        {
            if (body == null || !_isInitialized || !HasRequiredNativeState())
                return;

            UnregisterTrackedBodyInternal(body);
        }

        /// <inheritdoc />
        void IPhysicsStateEventService.ArmSpeculativeCcdForImpulse(Rigidbody body)
        {
            if (body == null)
                return;

            ArmSafeTeleportSpeculativeCcd(body);
        }

        /// <inheritdoc />
        float IPhysicsStateEventService.ResolveSpeculativeHoverHeightMeters(float baseHeightMeters, float timeSeconds)
        {
            return ResolveSpeculativeHoverHeightMeters(baseHeightMeters, timeSeconds);
        }

        /// <inheritdoc />
        void IPhysicsStateEventService.QueueKinematicImpact(
            Rigidbody primaryBody,
            Vector3 point,
            Vector3 normal,
            float impactSpeedMetersPerSecond,
            Rigidbody secondaryBody)
        {
            if (primaryBody == null)
                return;

            QueueKinematicImpactInternal(primaryBody, secondaryBody, point, normal, impactSpeedMetersPerSecond);
        }

        /// <inheritdoc />
        bool IPhysicsStateEventService.TryResolveImpactAudioMaterialId(Rigidbody body, out byte materialId)
        {
            materialId = ResolveImpactAudioMaterialId(body);
            return materialId != 0;
        }

        /// <inheritdoc />
        void IPhysicsStateEventService.RegisterImpactListener(IPhysicsImpactEventListener listener)
        {
            PhysicsEvents.Register(listener);
        }

        /// <inheritdoc />
        void IPhysicsStateEventService.UnregisterImpactListener(IPhysicsImpactEventListener listener)
        {
            PhysicsEvents.Unregister(listener);
        }

        /// <inheritdoc />
        public void RegisterDockConnectionOwner(UnityEngine.Object owner, Rigidbody dockedBody)
        {
            if (owner == null || dockedBody == null)
                return;

            RegisterOrUpdateConnection(owner, dockedBody, null, PhysicsConnectionKind.Dock);
        }

        /// <inheritdoc />
        public void UnregisterDockConnectionOwner(UnityEngine.Object owner)
        {
            if (owner == null)
                return;

            UnregisterConnection(owner, PhysicsConnectionKind.Dock);
        }

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
            _cachedCelestialRuntimeSnapshot = default;
            _cachedCelestialRuntimeSnapshotSequence = 0u;
            s_runtimeManager = null;
        }

        public static float UpdateFrameCachedCurrentWaterLevelY(
            float baseWaterLevelY,
            bool tidesEnabled,
            float tideAmplitudeMeters,
            float timeSeconds)
        {
            int frame = ResolveCurrentDispatcherFrameIndex();
            float safeAmplitude = math.max(0f, tideAmplitudeMeters);
            CelestialRuntimeSnapshot celestialSnapshot = _cachedCelestialRuntimeSnapshot;
            uint celestialSequence = _cachedCelestialRuntimeSnapshotSequence;
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
                : fallbackTimeSeconds;
            if (double.IsNaN(universeTime) || double.IsInfinity(universeTime) || universeTime < 0d)
                return fallbackTimeSeconds;

            double wrappedTime = universeTime % 4096d;
            return (float)wrappedTime;
        }

        internal static float ResolveSpeculativeHoverHeightMeters(float baseHeightMeters, float timeSeconds)
        {
            float safeBaseHeight = math.max(0f, baseHeightMeters);
            if (safeBaseHeight <= 0f)
                return 0f;

            CelestialRuntimeSnapshot celestialSnapshot = _cachedCelestialRuntimeSnapshot;
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

        internal static void RegisterTrackedBodyIfMissing(Rigidbody body)
        {
            if (body == null || !TryGetRuntimeManager(out GlobalPhysicsStateManager manager))
                return;

            if (manager.IsTrackedBodyRegisteredInternal(body))
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

            return manager._connections[connectionIndex].CompensationActive != 0;
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
                TryRegisterHotSwapListener();
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
            TryRegisterHotSwapListener();

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

            TryRegisterHotSwapListener();
            TryRegisterFixedTick();
            TryRegisterLateFrameTick();
            TryRegisterPostFixedTick();
            TryRegisterOriginShift();
            TryRegisterPhysicsCullingEventBus();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            RefreshOwnerPhaseCelestialSnapshotCache();
            TryCompletePhysicsCullingJobNonBlocking();
            TickKinematicHitStopGate();
            FlushImpactEvents();
        }

        /// <inheritdoc />
        public void PostFixedTick(float fixedDeltaTime)
        {
            TryCompletePhysicsCullingJobNonBlocking();
            if (_deferredNullTrackedBodyCleanup && !_physicsCullingJobScheduled)
                RemoveNullTrackedBodiesOutsidePhysicsCullingLocks();
            ApplyAupJitterSentinel();
            TickSafeTeleportSpeculativeCcdGuards();
        }

        private void OnDisable()
        {
            UnregisterRuntimeHooks();
        }

        private void EnsureNativeState()
        {
            BindNativeStateDataVault();
            CacheColdRuntimeDependencies();
            RefreshOwnerPhaseCelestialSnapshotCache();
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
                // COLD VAULT: double3[512] - authoritative absolute AUP body positions for rollback/hash consumers.
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

            EnsureShinobu37PhysicsCullingState();
        }

        private void BindNativeStateDataVault()
        {
            IDataVault dataVault = GlobalRegistry.DataVault;
            _nativeStateDataVault = dataVault;
            _lastValidPositions.BindDataVault(dataVault);
            _impactEvents.BindDataVault(dataVault);
            _rigidbodyAUPs.BindDataVault(dataVault);
            _rigidbodyCullingStateSnapshot.BindDataVault(dataVault);
            _rigidbodyAwakeResults.BindDataVault(dataVault);
            _rigidbodyCullingCommandResults.BindDataVault(dataVault);
            _rigidbodyDistanceSqResults.BindDataVault(dataVault);
            _physicsCullingTelemetry.BindDataVault(dataVault);
            BindShinobu37PhysicsCullingDataVault(dataVault);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong PhysicsVaultMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private bool TryAcquirePhysicsMutationGuard(ulong mutationGuardMask)
        {
            IDataVault vault = _nativeStateDataVault;
            return mutationGuardMask != 0UL &&
                vault != null &&
                vault.TryAcquireMutationGuard(mutationGuardMask);
        }

        private void ReleasePhysicsMutationGuard(ulong mutationGuardMask)
        {
            IDataVault vault = _nativeStateDataVault;
            if (mutationGuardMask != 0UL && vault != null)
                vault.ReleaseMutationGuard(mutationGuardMask);
        }

        private void CacheColdRuntimeDependencies()
        {
            if (_submarineHullBody == null)
            {
                var submarineContext = GlobalRegistry.Submarine;
                _submarineHullBody = submarineContext != null ? submarineContext.HullRigidbody : null;
            }

            if (_tickDispatcher == null)
                _tickDispatcher = GlobalRegistry.TickDispatcher;
            if (!IsCelestialSkyDirectionReadModelUsable(_celestialEngineRuntime))
                CacheCelestialSkyDirectionReadModel(GlobalRegistry.CelestialSkyDirection);
            if (!IsCelestialRuntimeSnapshotReadModelUsable(_celestialSnapshotReadModel))
                CacheCelestialRuntimeSnapshotReadModel(GlobalRegistry.CelestialRuntimeSnapshotReadModel);
        }

        private void RefreshOwnerPhaseCelestialSnapshotCache()
        {
            // Owner-phase bridge: public waterline/speculative hover helpers consume this immutable cache
            // instead of polling GlobalRegistry from read-shaped accessors.
            ICelestialSkyDirectionReadModel celestialEngine = ResolveCelestialSkyDirectionReadModel();
            if (celestialEngine == null)
            {
                ClearCachedCelestialRuntimeSnapshot();
                return;
            }

            ICelestialRuntimeSnapshotReadModel snapshotReadModel = ResolveCelestialRuntimeSnapshotReadModel();
            if (snapshotReadModel == null)
            {
                ClearCachedCelestialRuntimeSnapshot();
                return;
            }

            CelestialRuntimeSnapshot snapshot = snapshotReadModel.RuntimeSnapshot;
            _cachedCelestialRuntimeSnapshot = snapshot;
            _cachedCelestialRuntimeSnapshotSequence = snapshot.Sequence;
        }

        private ICelestialSkyDirectionReadModel ResolveCelestialSkyDirectionReadModel()
        {
            if (!IsCelestialSkyDirectionReadModelUsable(_celestialEngineRuntime))
                _celestialEngineRuntime = null;

            return _celestialEngineRuntime;
        }

        private ICelestialRuntimeSnapshotReadModel ResolveCelestialRuntimeSnapshotReadModel()
        {
            if (!IsCelestialRuntimeSnapshotReadModelUsable(_celestialSnapshotReadModel))
                _celestialSnapshotReadModel = null;

            return _celestialSnapshotReadModel;
        }

        private void CacheCelestialSkyDirectionReadModel(ICelestialSkyDirectionReadModel readModel)
        {
            if (IsCelestialSkyDirectionReadModelUsable(readModel))
            {
                _celestialEngineRuntime = readModel;
                return;
            }

            ICelestialSkyDirectionReadModel fallback = GlobalRegistry.CelestialSkyDirection;
            _celestialEngineRuntime = IsCelestialSkyDirectionReadModelUsable(fallback) ? fallback : null;
        }

        private void CacheCelestialRuntimeSnapshotReadModel(ICelestialRuntimeSnapshotReadModel readModel)
        {
            if (IsCelestialRuntimeSnapshotReadModelUsable(readModel))
            {
                _celestialSnapshotReadModel = readModel;
                return;
            }

            ICelestialRuntimeSnapshotReadModel fallback = GlobalRegistry.CelestialRuntimeSnapshotReadModel;
            _celestialSnapshotReadModel = IsCelestialRuntimeSnapshotReadModelUsable(fallback) ? fallback : null;
        }

        private static bool IsCelestialSkyDirectionReadModelUsable(ICelestialSkyDirectionReadModel readModel)
        {
            if (readModel == null)
                return false;

            if (readModel is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private static bool IsCelestialRuntimeSnapshotReadModelUsable(ICelestialRuntimeSnapshotReadModel readModel)
        {
            if (readModel == null)
                return false;

            if (readModel is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private static void ClearCachedCelestialRuntimeSnapshot()
        {
            _cachedCelestialRuntimeSnapshot = default;
            _cachedCelestialRuntimeSnapshotSequence = 0u;
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
                (_physicsCullingTelemetry.IsCreated && _physicsCullingTelemetry.Length < PhysicsCullingTelemetryCapacity) ||
                HasUndersizedShinobu37PhysicsCullingState();
            if (!hasUndersizedLane)
                return;

            CompletePhysicsCullingJobForStateMutationBarrier(discardResults: true);
            ReleaseUndersizedVaultBuffer(ref _lastValidPositions, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _impactEvents, MaxQueuedImpactEvents);
            ReleaseUndersizedVaultBuffer(ref _rigidbodyAUPs, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _rigidbodyCullingStateSnapshot, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _rigidbodyAwakeResults, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _rigidbodyCullingCommandResults, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _rigidbodyDistanceSqResults, MaxTrackedBodies);
            ReleaseUndersizedVaultBuffer(ref _physicsCullingTelemetry, PhysicsCullingTelemetryCapacity);
            ReleaseUndersizedShinobu37PhysicsCullingState();
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
                _physicsCullingTelemetry.Length >= PhysicsCullingTelemetryCapacity &&
                HasRequiredShinobu37PhysicsCullingState();
        }

        private void ReportNativeStateUnavailable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_nativeStateAllocationFailureReported)
            {
                Hecton8.Core.H8Debug.LogError("[GlobalPhysicsStateManager] Required native state unavailable; rigidbody tracking rejected.");
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
            CompletePhysicsCullingJobForStateMutationBarrier(discardResults: true);
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
            ReleaseShinobu37PhysicsCullingState();
            _submarineHullBody = null;
            _tickDispatcher = null;
            _nativeStateDataVault = null;

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

            TryUnregisterHotSwapListener();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.CelestialEngineRuntime:
                    CacheCelestialSkyDirectionReadModel(currentService as ICelestialSkyDirectionReadModel);
                    CacheCelestialRuntimeSnapshotReadModel(currentService as ICelestialRuntimeSnapshotReadModel);
                    RefreshOwnerPhaseCelestialSnapshotCache();
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterDispatcherTicks();
                    _tickDispatcher = currentService as ITickDispatcher;
                    if (_tickDispatcher != null && _isInitialized)
                    {
                        TryRegisterFixedTick();
                        TryRegisterLateFrameTick();
                        TryRegisterPostFixedTick();
                    }
                    break;
            }
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            RefreshOwnerPhaseCelestialSnapshotCache();
            _lastFixedDeltaTime = SanitizeFixedStepDelta(fixedDeltaTime);
            RefreshTrackedBodies(_lastFixedDeltaTime);
            SweepNaNPhysicsState();
            EvaluateConnections();
            TickPhysicsCullingSlowCadence(_lastFixedDeltaTime);
            if (_deferredNullTrackedBodyCleanup && !_physicsCullingJobScheduled)
                RemoveNullTrackedBodiesOutsidePhysicsCullingLocks();
            TickColliderLodHysteresisCadence(_lastFixedDeltaTime);
            DrainAddedMassTensorDirtyQueue();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeFixedStepDelta(float fixedDeltaTime)
        {
            return fixedDeltaTime > 0f && math.isfinite(fixedDeltaTime)
                ? fixedDeltaTime
                : PhysicsFixedStepSeconds;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveCurrentDispatcherFrameIndex()
        {
            uint frame = TimeSliceScheduler.CurrentFrameId;
            return unchecked((int)(frame != 0u ? frame : 1u));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveImpactFixedDeltaTime()
        {
            return math.max(_lastFixedDeltaTime, 0.0001f);
        }

        private static bool TryGetRuntimeManager(out GlobalPhysicsStateManager manager)
        {
            manager = s_runtimeManager;
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
        public void QueueWakeRequest(in WakeRequestSignal request)
        {
            QueuePhysicsWakeRequest(in request);
        }

        /// <inheritdoc />
        public bool IsBodyCulled(Rigidbody body)
        {
            int bodyIndex = FindTrackedBodyIndex(body);
            if (bodyIndex < 0)
                return false;

            RigidbodyState bodyState = _bodyStates[bodyIndex];
            return bodyState.DistanceSleepActive != 0 ||
                bodyState.DistanceKinematicSleepActive != 0 ||
                bodyState.MeshColliderStripActive != 0;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered)
                return;

            GlobalRegistry.RegisterPhysicsStateManager(this);
            GlobalRegistry.RegisterPhysicsCullingOverseer(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.PhysicsStateManager, this) &&
                ReferenceEquals(GlobalRegistry.PhysicsCullingOverseer, this);
            if (_serviceRegistered)
                s_runtimeManager = this;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(GlobalRegistry.PhysicsStateManager, this))
                GlobalRegistry.UnregisterPhysicsStateManager(this);
            if (ReferenceEquals(GlobalRegistry.PhysicsCullingOverseer, this))
                GlobalRegistry.UnregisterPhysicsCullingOverseer(this);

            if (ReferenceEquals(s_runtimeManager, this))
                s_runtimeManager = null;
            _serviceRegistered = false;
        }

        private void TryUnregisterDispatcherTicks()
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
        }

        private void TryRegisterFixedTick()
        {
            if (_registeredFixedTick || !Application.isPlaying || GlobalRegistry.TickDispatcher == null)
                return;

            _registeredFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Core);
        }

        private void TryRegisterLateFrameTick()
        {
            if (_registeredLateFrameTick || !Application.isPlaying || GlobalRegistry.TickDispatcher == null)
                return;

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
        }

        private void TryRegisterPostFixedTick()
        {
            if (_registeredPostFixedTick || !Application.isPlaying || GlobalRegistry.TickDispatcher == null)
                return;

            _registeredPostFixedTick = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Core);
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
            CompletePhysicsCullingJobForStateMutationBarrier(discardResults: true);
            if (!_lastValidPositions.IsCreated || _trackedBodyCount <= 0)
                return;

            bool positionLocksReady = TryAcquireTrackedBodyPositionPublishLocks1337(
                includeRigidbodyAups: false,
                out NativeArray<float3> lastValidPositions,
                out _,
                out bool lastValidPositionsLocked,
                out bool rigidbodyAupsLocked);
            if (!positionLocksReady)
                _physicsCullingLockContentionsThisFrame++;

            try
            {
                for (int i = _trackedBodyCount - 1; i >= 0; i--)
                {
                    Rigidbody body = _trackedBodies[i];
                    if (body == null)
                    {
                        _deferredNullTrackedBodyCleanup = true;
                        continue;
                    }

                    RigidbodyState bodyState = _bodyStates[i];
                    if (body.isKinematic && bodyState.DistanceKinematicSleepActive == 0)
                        continue;

                    Vector3 position = body.position;
                    Quaternion rotation = body.rotation;
                    Vector3 linearVelocity = body.linearVelocity;
                    Vector3 angularVelocity = body.angularVelocity;
                    CollisionDetectionMode collisionDetectionMode = body.collisionDetectionMode;
                    if (IsFinite(position))
                    {
                        bodyState.HasLastValidPosition = 1;
                        if (lastValidPositionsLocked && (uint)i < (uint)lastValidPositions.Length)
                            lastValidPositions[i] = new float3(position.x, position.y, position.z);
                        if (TryResolveAupFromRuntimeOrigin(position, out AbsoluteUniversePosition positionAup))
                        {
                            bodyState.LastValidAup = positionAup;
                            bodyState.HasLastValidAup = 1;
                        }
                    }
                    else
                    {
                        float3 fallbackPosition = lastValidPositionsLocked && (uint)i < (uint)lastValidPositions.Length
                            ? lastValidPositions[i]
                            : float3.zero;
                        position = bodyState.HasLastValidPosition != 0
                            ? new Vector3(fallbackPosition.x, fallbackPosition.y, fallbackPosition.z)
                            : Vector3.zero;
                    }

                    bodyState.HasOriginShiftSnapshot = 1;
                    bodyState.SnapshotPositionBeforeOriginShift = position;
                    bodyState.SnapshotRotationBeforeOriginShift = SanitizeQuaternion(rotation);

                    bodyState.LastValidLinearVelocity = IsFinite(linearVelocity) ? linearVelocity : Vector3.zero;
                    bodyState.LastValidAngularVelocity = IsFinite(angularVelocity) ? angularVelocity : Vector3.zero;
                    bodyState.FixedInterpolationAlphaBeforeOriginShift = HectonFloatingOrigin.CurrentFixedInterpolationAlpha;
                    bodyState.WasSleepingBeforeOriginShift = body.IsSleeping() ? (byte)1 : (byte)0;
                    bodyState.InterpolationModeBeforeOriginShift = body.interpolation;
                    bodyState.InterpolationSuspendedForOriginShift = body.interpolation != RigidbodyInterpolation.None ? (byte)1 : (byte)0;
                    if (bodyState.InterpolationSuspendedForOriginShift != 0)
                        body.interpolation = RigidbodyInterpolation.None;
                    bodyState.CollisionDetectionModeBeforeOriginShift = collisionDetectionMode;
                    float speedSq = bodyState.LastValidLinearVelocity.sqrMagnitude;
                    bodyState.CollisionDetectionOverriddenForOriginShift =
                        (speedSq > OriginShiftContinuousCcdSpeedMetersPerSecondSq &&
                        collisionDetectionMode != CollisionDetectionMode.Continuous &&
                        collisionDetectionMode != CollisionDetectionMode.ContinuousDynamic) ? (byte)1 : (byte)0;
                    if (bodyState.CollisionDetectionOverriddenForOriginShift != 0)
                        body.collisionDetectionMode = CollisionDetectionMode.Continuous;
                    body.PublishTransform();
                    _bodyStates[i] = bodyState;
                }
            }
            finally
            {
                ReleaseTrackedBodyPositionPublishLocks1337(lastValidPositionsLocked, rigidbodyAupsLocked);
            }
        }

        private struct AupSyncFenceEntry
        {
            public uint ShiftFrameId;
            public uint FenceResolutionFrame;
        }

        private void CommitTrackedBodiesForOriginShiftInternal(Vector3 shiftOffset)
        {
            _syncFenceRingCursor = (_syncFenceRingCursor + 1) % 300;
            _syncFenceRing[_syncFenceRingCursor] = new AupSyncFenceEntry
            {
                ShiftFrameId = (uint)SystemDispatcher.CurrentFrameIndex,
                FenceResolutionFrame = (uint)SystemDispatcher.CurrentFrameIndex + 300
            };

            CompletePhysicsCullingJobForStateMutationBarrier(discardResults: true);
            if (!_lastValidPositions.IsCreated || _trackedBodyCount <= 0 || shiftOffset.sqrMagnitude <= 0.000001f)
                return;

            bool positionLocksReady = TryAcquireTrackedBodyPositionPublishLocks1337(
                includeRigidbodyAups: true,
                out NativeArray<float3> lastValidPositions,
                out NativeArray<double3> rigidbodyAups,
                out bool lastValidPositionsLocked,
                out bool rigidbodyAupsLocked);
            if (!positionLocksReady)
                _physicsCullingLockContentionsThisFrame++;

            try
            {
                for (int i = _trackedBodyCount - 1; i >= 0; i--)
                {
                    Rigidbody body = _trackedBodies[i];
                    if (body == null)
                    {
                        _deferredNullTrackedBodyCleanup = true;
                        continue;
                    }

                    RigidbodyState bodyState = _bodyStates[i];
                    if (body.isKinematic && bodyState.DistanceKinematicSleepActive == 0)
                        continue;

                    Vector3 snapshotPosition = bodyState.HasOriginShiftSnapshot != 0
                        ? bodyState.SnapshotPositionBeforeOriginShift
                        : body.position;
                    Vector3 targetPosition = snapshotPosition - shiftOffset;

                    if (!IsFinite(targetPosition))
                        targetPosition = Vector3.zero;

                    Quaternion targetRotation = bodyState.HasOriginShiftSnapshot != 0
                        ? SanitizeQuaternion(bodyState.SnapshotRotationBeforeOriginShift)
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
                        bodyState.WasSleepingBeforeOriginShift != 0);

                    if (lastValidPositionsLocked && (uint)i < (uint)lastValidPositions.Length)
                        lastValidPositions[i] = new float3(targetPosition.x, targetPosition.y, targetPosition.z);
                    bodyState.HasLastValidPosition = 1;
                    if (TryResolveAupFromRuntimeOrigin(targetPosition, out AbsoluteUniversePosition targetAup))
                    {
                        targetAup.LocalX = math.round(targetAup.LocalX * 1000f) / 1000f;
                        targetAup.LocalY = math.round(targetAup.LocalY * 1000f) / 1000f;
                        targetAup.LocalZ = math.round(targetAup.LocalZ * 1000f) / 1000f;
                        bodyState.LastValidAup = targetAup;
                        bodyState.HasLastValidAup = 1;
                        if (rigidbodyAupsLocked && (uint)i < (uint)rigidbodyAups.Length)
                            rigidbodyAups[i] = targetAup.ToAbsoluteDouble3();
                    }
                    else
                    {
                        bodyState.HasLastValidAup = 0;
                        if (rigidbodyAupsLocked && (uint)i < (uint)rigidbodyAups.Length)
                            rigidbodyAups[i] = default;
                    }
                    bodyState.HasOriginShiftSnapshot = 0;
                    bodyState.LastValidLinearVelocity = linearVelocity;
                    bodyState.LastValidAngularVelocity = angularVelocity;
                    bodyState.FixedInterpolationAlphaBeforeOriginShift = 0f;
                    bodyState.WasSleepingBeforeOriginShift = 0;
                    _bodyStates[i] = bodyState;
                }
            }
            finally
            {
                ReleaseTrackedBodyPositionPublishLocks1337(lastValidPositionsLocked, rigidbodyAupsLocked);
            }
        }

        private void FinalizeTrackedBodiesAfterOriginShiftInternal()
        {
            CompletePhysicsCullingJobForStateMutationBarrier(discardResults: true);
            for (int i = _trackedBodyCount - 1; i >= 0; i--)
            {
                Rigidbody body = _trackedBodies[i];
                if (body == null)
                {
                    TryRemoveNullTrackedBodyAt(i);
                    continue;
                }

                RigidbodyState bodyState = _bodyStates[i];
                if (bodyState.InterpolationSuspendedForOriginShift == 0)
                {
                    if (bodyState.CollisionDetectionOverriddenForOriginShift != 0)
                    {
                        if (bodyState.SafeTeleportSpeculativeCcdActive == 0)
                            body.collisionDetectionMode = bodyState.CollisionDetectionModeBeforeOriginShift;

                        bodyState.CollisionDetectionOverriddenForOriginShift = 0;
                        _bodyStates[i] = bodyState;
                    }

                    continue;
                }

                body.interpolation = bodyState.InterpolationModeBeforeOriginShift;
                bodyState.InterpolationSuspendedForOriginShift = 0;
                if (bodyState.CollisionDetectionOverriddenForOriginShift != 0)
                {
                    if (bodyState.SafeTeleportSpeculativeCcdActive == 0)
                        body.collisionDetectionMode = bodyState.CollisionDetectionModeBeforeOriginShift;

                    bodyState.CollisionDetectionOverriddenForOriginShift = 0;
                }

                _bodyStates[i] = bodyState;
            }
        }

        private void ResetTrackedBodiesForSafeTeleportInternal()
        {
            CompletePhysicsCullingJobForStateMutationBarrier(discardResults: true);
            bool positionLocksReady = TryAcquireTrackedBodyPositionPublishLocks1337(
                includeRigidbodyAups: true,
                out NativeArray<float3> lastValidPositions,
                out NativeArray<double3> rigidbodyAups,
                out bool lastValidPositionsLocked,
                out bool rigidbodyAupsLocked);
            if (!positionLocksReady)
                _physicsCullingLockContentionsThisFrame++;

            try
            {
                for (int i = _trackedBodyCount - 1; i >= 0; i--)
                {
                    Rigidbody body = _trackedBodies[i];
                    if (body == null)
                    {
                        _deferredNullTrackedBodyCleanup = true;
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
                        SanitizeQuaternion(body.rotation),
                        linearVelocity,
                        angularVelocity,
                        wasSleeping);

                    if (lastValidPositionsLocked && (uint)i < (uint)lastValidPositions.Length)
                        lastValidPositions[i] = new float3(position.x, position.y, position.z);
                    bodyState.HasLastValidPosition = 1;
                    if (TryResolveAupFromRuntimeOrigin(position, out AbsoluteUniversePosition positionAup))
                    {
                        bodyState.LastValidAup = positionAup;
                        bodyState.HasLastValidAup = 1;
                        if (rigidbodyAupsLocked && (uint)i < (uint)rigidbodyAups.Length)
                            rigidbodyAups[i] = positionAup.ToAbsoluteDouble3();
                    }
                    else
                    {
                        bodyState.HasLastValidAup = 0;
                        if (rigidbodyAupsLocked && (uint)i < (uint)rigidbodyAups.Length)
                            rigidbodyAups[i] = default;
                    }

                    bodyState.LastValidLinearVelocity = linearVelocity;
                    bodyState.LastValidAngularVelocity = angularVelocity;
                    bodyState.HasOriginShiftSnapshot = 0;
                    bodyState.FixedInterpolationAlphaBeforeOriginShift = 0f;
                    bodyState.InterpolationSuspendedForOriginShift = 0;
                    bodyState.CollisionDetectionOverriddenForOriginShift = 0;
                    if (wasSleeping)
                        bodyState.StateMask |= PhysicsStateMask.WasAsleep;
                    else
                        bodyState.StateMask &= ~PhysicsStateMask.WasAsleep;

                    _bodyStates[i] = bodyState;
                }
            }
            finally
            {
                ReleaseTrackedBodyPositionPublishLocks1337(lastValidPositionsLocked, rigidbodyAupsLocked);
            }
        }

        private void ArmSafeTeleportSpeculativeCcdForSafeTeleportInternal()
        {
            IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            Rigidbody playerBody = runtimeContext != null ? runtimeContext.PlayerRigidbody : null;
            ArmSafeTeleportSpeculativeCcd(playerBody);

            Rigidbody hullBody = _submarineHullBody;
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
                    TryRemoveNullTrackedBodyAt(i);
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
            if (bodyState.SafeTeleportSpeculativeCcdActive == 0)
            {
                bodyState.CollisionDetectionModeBeforeSafeTeleport = bodyState.CollisionDetectionOverriddenForOriginShift != 0
                    ? bodyState.CollisionDetectionModeBeforeOriginShift
                    : body.collisionDetectionMode;
            }

            bodyState.SafeTeleportSpeculativeCcdActive = 1;
            bodyState.CollisionDetectionOverriddenForOriginShift = 0;
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
                if (bodyState.SafeTeleportSpeculativeCcdActive == 0)
                    continue;

                Rigidbody body = _trackedBodies[i];
                if (body == null)
                {
                    TryRemoveNullTrackedBodyAt(i);
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

            ITickDispatcher dispatcher = _tickDispatcher;
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

        private float ResolveDispatcherUnscaledDeltaTime()
        {
            ITickDispatcher dispatcher = _tickDispatcher;
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

            ITickDispatcher dispatcher = _tickDispatcher;
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
            int bodyInstanceHash = body.GetEntityId().GetHashCode();
            PhysicsCullingFlags cullingFlags = ScanCullingFlagsFromComponents(body) | registrationFlags;
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
                    byte existingSleepColliderCount = existingState.SleepColliderCount;
                    if (existingSleepColliderCount == 0)
                        existingSleepColliderCount = CacheSleepCollidersForBody(body, existingBodyIndex);
                    if (existingMeshColliderCount > 0)
                        cullingFlags |= PhysicsCullingFlags.HeavyCollider;

                    existingState.CullingFlags = cullingFlags;
                    existingState.EntityInstanceHash = bodyInstanceHash;
                    existingState.MeshColliderCount = existingMeshColliderCount;
                    existingState.SleepColliderCount = existingSleepColliderCount;
                    existingState.AllowDistanceKinematicSleep = ShouldAllowDistanceKinematicSleep(body, cullingFlags) ? (byte)1 : (byte)0;

                    bool hasExistingAup = existingState.HasLastValidAup != 0 && IsFinite(in existingState.LastValidAup);
                    AbsoluteUniversePosition existingAup = hasExistingAup ? existingState.LastValidAup : default;
                    if (!hasExistingAup &&
                        TryResolveAupFromRuntimeOrigin(body.position, out AbsoluteUniversePosition resolvedExistingAup))
                    {
                        existingAup = resolvedExistingAup;
                        hasExistingAup = true;
                        existingState.LastValidAup = existingAup;
                        existingState.HasLastValidAup = 1;
                    }

                    _bodyStates[existingBodyIndex] = existingState;
                    _trackedBodyIndexByInstanceId[bodyInstanceHash] = existingBodyIndex;
                    if (_physicsCullingJobScheduled)
                    {
                        MarkPhysicsCullingSpatialHashDirty();
                        return;
                    }

                    if (!TryAcquirePhysicsTrackedBodyLaneMutationLocks1337())
                    {
                        _physicsCullingLockContentionsThisFrame++;
                        MarkPhysicsCullingSpatialHashDirty();
                        return;
                    }

                    try
                    {
                        if ((cullingFlags & PhysicsCullingFlags.IgnoreCulling) != 0)
                            RestoreAllPhysicsCullingState(existingBodyIndex, body, ref existingState, forceWake: true);

                        _bodyStates[existingBodyIndex] = existingState;
                        _rigidbodyAUPs[existingBodyIndex] = hasExistingAup
                            ? existingAup.ToAbsoluteDouble3()
                            : default;
                        WritePhysicsCullingDto(existingBodyIndex, body, in _bodyStates[existingBodyIndex], in existingAup);
                    }
                    finally
                    {
                        ReleasePhysicsTrackedBodyLaneMutationLocks1337();
                    }

                    MarkPhysicsCullingSpatialHashDirty();
                    return;
                }

                if ((uint)existingBodyIndex < (uint)_trackedBodyCount)
                    RemoveTrackedBodyAt(existingBodyIndex);
                else
                {
                    _trackedBodyIndexByEntityId.Remove(bodyEntityId);
                    _trackedBodyIndexByInstanceId.Remove(bodyInstanceHash);
                }
            }

            if (!EnsureTrackedBodyCapacity(_trackedBodyCount + 1))
                return;

            CompletePhysicsCullingJobForStateMutationBarrier(discardResults: true);
            IPhysicsColliderLodHysteresisSink colliderLodSink = FindColliderLodSinkFromComponent(body);
            Vector3 bodyPosition = body.position;
            Vector3 bodyLinearVelocity = body.linearVelocity;
            Vector3 bodyAngularVelocity = body.angularVelocity;
            Vector3 bodyInertiaTensor = body.inertiaTensor;
            Quaternion bodyInertiaTensorRotation = body.inertiaTensorRotation;
            bool hasFiniteBodyPosition = IsFinite(bodyPosition);
            AbsoluteUniversePosition bodyAup = default;
            bool hasBodyAup = hasFiniteBodyPosition &&
                TryResolveAupFromRuntimeOrigin(bodyPosition, out bodyAup);

            int bodyIndex = _trackedBodyCount++;
            byte meshColliderCount = CacheMeshCollidersForBody(body, bodyIndex);
            byte sleepColliderCount = CacheSleepCollidersForBody(body, bodyIndex);
            if (meshColliderCount > 0)
                cullingFlags |= PhysicsCullingFlags.HeavyCollider;

            _trackedBodies[bodyIndex] = body;
            _bodyStates[bodyIndex] = new RigidbodyState
            {
                EntityId = bodyEntityId,
                EntityInstanceHash = bodyInstanceHash,
                StateMask = PhysicsStateMask.None,
                CompensationRefCount = 0,
                CullingLockRefCount = 0,
                MaxAngularVelocityClamp = ScanMaxAngularVelocityClampFromComponents(body),
                AllowDistanceKinematicSleep = ShouldAllowDistanceKinematicSleep(body, cullingFlags) ? (byte)1 : (byte)0,
                CullingFlags = cullingFlags,
                MeshColliderCount = meshColliderCount,
                SleepColliderCount = sleepColliderCount,
                DistanceSleepActive = 0,
                DistanceKinematicSleepActive = 0,
                MeshColliderStripActive = 0,
                HasLastValidPosition = hasFiniteBodyPosition ? (byte)1 : (byte)0,
                HasLastValidAup = hasBodyAup ? (byte)1 : (byte)0,
                LastValidAup = hasBodyAup ? bodyAup : default,
                KinematicModeBeforeDistanceSleep = body.isKinematic ? (byte)1 : (byte)0,
                DetectCollisionsBeforeDistanceSleep = body.detectCollisions ? (byte)1 : (byte)0,
                LastValidLinearVelocity = IsFinite(bodyLinearVelocity) ? bodyLinearVelocity : Vector3.zero,
                LastValidAngularVelocity = IsFinite(bodyAngularVelocity) ? bodyAngularVelocity : Vector3.zero,
                ColliderLodSink = colliderLodSink,
                ImpactAudioMaterialId = FindImpactAudioMaterialIdFromComponents(body),
                HasColliderLodSink = IsColliderLodSinkAlive(colliderLodSink) ? (byte)1 : (byte)0,
                ColliderLodDistanceGateOpen = 0,
                ColliderLodOutOfRangeSeconds = 0f,
                BaseInertiaTensor = IsFinite(bodyInertiaTensor) ? bodyInertiaTensor : Vector3.one,
                BaseInertiaTensorRotation = SanitizeQuaternion(bodyInertiaTensorRotation),
                BaseAngularDamping = math.max(0f, body.angularDamping),
                HydrodynamicSubmersionFactor = 0f,
                HasAddedMassBaseline = 1
            };
            ApplyTrackedBodyAngularVelocityClamp(body, _bodyStates[bodyIndex].MaxAngularVelocityClamp);
            _trackedBodyIndexByEntityId[bodyEntityId] = bodyIndex;
            _trackedBodyIndexByInstanceId[bodyInstanceHash] = bodyIndex;
            if (!TryAcquirePhysicsTrackedBodyLaneMutationLocks1337())
            {
                _trackedBodyIndexByEntityId.Remove(bodyEntityId);
                _trackedBodyIndexByInstanceId.Remove(bodyInstanceHash);
                _trackedBodies[bodyIndex] = null;
                _bodyStates[bodyIndex] = default;
                ClearMeshColliderRefs(bodyIndex);
                ClearSleepColliderRefs(bodyIndex);
                _trackedBodyCount--;
                _physicsCullingLockContentionsThisFrame++;
                return;
            }

            try
            {
                _lastValidPositions[bodyIndex] = hasFiniteBodyPosition
                    ? new float3(bodyPosition.x, bodyPosition.y, bodyPosition.z)
                    : float3.zero;
                _rigidbodyAUPs[bodyIndex] = hasBodyAup
                    ? bodyAup.ToAbsoluteDouble3()
                    : default;
                InitializePhysicsCullingDtoForBody(bodyIndex, body, in _bodyStates[bodyIndex]);
            }
            finally
            {
                ReleasePhysicsTrackedBodyLaneMutationLocks1337();
            }

            EnsureReporter(body);
        }

        private bool IsTrackedBodyRegisteredInternal(Rigidbody body)
        {
            if (body == null)
                return false;

            ulong bodyEntityId = EntityId.ToULong(body.GetEntityId());
            return _trackedBodyIndexByEntityId.TryGetValue(bodyEntityId, out int bodyIndex) &&
                (uint)bodyIndex < (uint)_trackedBodyCount &&
                ReferenceEquals(_trackedBodies[bodyIndex], body);
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
            float safeSubmersion = math.saturate(math.isfinite(submersionFactor) ? submersionFactor : 0f);
            byte isFullySubmerged = safeSubmersion >= AddedMassFullySubmergedThreshold ? (byte)1 : (byte)0;
            bool changed = math.abs(bodyState.HydrodynamicSubmersionFactor - safeSubmersion) > AddedMassSubmersionEpsilon ||
                bodyState.IsFullySubmerged != isFullySubmerged ||
                (safeSubmersion <= AddedMassSubmersionEpsilon && bodyState.AddedMassTensorApplied != 0);

            bodyState.HydrodynamicSubmersionFactor = safeSubmersion;
            bodyState.IsFullySubmerged = isFullySubmerged;
            if (changed)
                MarkAddedMassTensorDirty(bodyIndex, ref bodyState);

            _bodyStates[bodyIndex] = bodyState;
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
            {
                Vector3 centerOfMass = primaryBody.worldCenterOfMass;
                point3 = new float3(centerOfMass.x, centerOfMass.y, centerOfMass.z);
            }
            float normalSq = math.lengthsq(normal3);
            if (!math.all(math.isfinite(normal3)) || normalSq <= 0.000001f)
                normal3 = new float3(0f, 1f, 0f);
            else
                normal3 *= math.rsqrt(math.max(normalSq, 0.000001f));
            if (!TryResolveAupFromRuntimeOrigin(new Vector3(point3.x, point3.y, point3.z), out AbsoluteUniversePosition pointAup))
                return;

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

            if (!_impactEvents.TryAcquireWriteLock(out NativeArray<PhysicsImpactEventData> impactEvents))
            {
                _physicsCullingLockContentionsThisFrame++;
                return false;
            }

            int writeIndex = _impactEventWriteIndex;
            if ((uint)writeIndex >= (uint)MaxQueuedImpactEvents)
                writeIndex = 0;

            try
            {
                if (!impactEvents.IsCreated || (uint)writeIndex >= (uint)impactEvents.Length)
                    return false;

                impactEvents[writeIndex] = impactEvent;
                _impactEventWriteIndex = writeIndex + 1 >= MaxQueuedImpactEvents
                    ? 0
                    : writeIndex + 1;
                _queuedImpactCount++;
                return true;
            }
            finally
            {
                _impactEvents.ReleaseWriteLock();
            }
        }

        private void FlushImpactEvents()
        {
            if (!_impactEvents.IsCreated || _queuedImpactCount <= 0)
                return;

            int processedCount = 0;
            if (!_impactEvents.TryAcquireWriteLock(out NativeArray<PhysicsImpactEventData> impactEvents))
            {
                _physicsCullingLockContentionsThisFrame++;
                return;
            }

            try
            {
                while (_queuedImpactCount > 0 &&
                       processedCount < MaxImpactFlushIterations)
                {
                    int readIndex = _impactEventReadIndex;
                    if ((uint)readIndex >= (uint)MaxQueuedImpactEvents)
                        readIndex = 0;

                    if (!impactEvents.IsCreated || (uint)readIndex >= (uint)impactEvents.Length)
                        break;

                    _impactFlushScratch[processedCount] = impactEvents[readIndex];
                    impactEvents[readIndex] = default;
                    _impactEventReadIndex = readIndex + 1 >= MaxQueuedImpactEvents
                        ? 0
                        : readIndex + 1;
                    _queuedImpactCount--;
                    processedCount++;
                }
            }
            finally
            {
                _impactEvents.ReleaseWriteLock();
            }

            for (int i = 0; i < processedCount; i++)
            {
                PhysicsImpactEventData impactEvent = _impactFlushScratch[i];
                _impactFlushScratch[i] = default;
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
                SignalBus<ImpactSignal>.TryPushTracked(in corridorSignal, ref s_x001GlobalPhysicsStateManagerSignalPushDropCount);
                PhysicsEvents.TryNotifyImpact(new PhysicsImpactSignal(
                    impactEvent.PrimaryBodyId,
                    impactEvent.SecondaryBodyId,
                    impactPoint,
                    impactEvent.PointAup.ToAbsoluteDouble3(),
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

        private static byte FindImpactAudioMaterialIdFromComponents(Rigidbody body)
        {
            if (body == null)
                return 0;

            if (body.TryGetComponent(out Hecton8.Core.Contracts.IPhysicsImpactMaterialProvider directProvider))
                return directProvider.ImpactAudioMaterialId;

            return TryResolveComponentInParents(body.transform.parent, out Hecton8.Core.Contracts.IPhysicsImpactMaterialProvider provider)
                ? provider.ImpactAudioMaterialId
                : (byte)0;
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
                        Hecton8.Core.H8Debug.LogWarning("[GlobalPhysicsStateManager] Connection registry capacity exceeded.");
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
                CompensationActive = 0
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
            if (_connectionCount <= 0 &&
                _connectionLockTouchedBodyCount <= 0 &&
                !_connectionRefsRequireFullClear)
                return;

            ClearPreviousConnectionLockRefs();
            if (_connectionCount <= 0)
                return;

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
                if (connection.CompensationActive == 0 || connection.CompensatedBody == null)
                    continue;

                int compensatedIndex = FindTrackedBodyIndex(connection.CompensatedBody);
                if (compensatedIndex < 0)
                    continue;

                RigidbodyState bodyState = _bodyStates[compensatedIndex];
                bodyState.CompensationRefCount++;
                bodyState.CullingLockRefCount++;
                _bodyStates[compensatedIndex] = bodyState;
                RecordConnectionLockTouchedBody(compensatedIndex);
            }
        }

        private void ClearPreviousConnectionLockRefs()
        {
            if (_connectionRefsRequireFullClear)
            {
                for (int i = 0; i < _trackedBodyCount; i++)
                    ClearConnectionLockRefsAt(i);

                _connectionLockTouchedBodyCount = 0;
                _connectionRefsRequireFullClear = false;
                return;
            }

            int touchedCount = _connectionLockTouchedBodyCount;
            _connectionLockTouchedBodyCount = 0;
            for (int i = 0; i < touchedCount; i++)
            {
                int bodyIndex = _connectionLockTouchedBodyIndices[i];
                _connectionLockTouchedBodyIndices[i] = 0;
                if ((uint)bodyIndex >= (uint)_trackedBodyCount)
                    continue;

                ClearConnectionLockRefsAt(bodyIndex);
            }
        }

        private void ClearConnectionLockRefsAt(int bodyIndex)
        {
            RigidbodyState bodyState = _bodyStates[bodyIndex];
            bodyState.CompensationRefCount = 0;
            bodyState.CullingLockRefCount = 0;
            _bodyStates[bodyIndex] = bodyState;
        }

        private void RecordConnectionLockTouchedBody(int bodyIndex)
        {
            if ((uint)bodyIndex >= (uint)_trackedBodyCount)
                return;

            if (_connectionLockTouchedBodyCount >= _connectionLockTouchedBodyIndices.Length)
            {
                _connectionRefsRequireFullClear = true;
                _physicsCullingLockContentionsThisFrame++;
                return;
            }

            _connectionLockTouchedBodyIndices[_connectionLockTouchedBodyCount++] = bodyIndex;
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
                (bodyState.DistanceSleepActive != 0 ||
                 bodyState.DistanceKinematicSleepActive != 0 ||
                 bodyState.MeshColliderStripActive != 0))
            {
                RestoreAllPhysicsCullingState(bodyIndex, body, ref bodyState, forceWake: true);
            }

            _bodyStates[bodyIndex] = bodyState;
            RecordConnectionLockTouchedBody(bodyIndex);
        }

        private void EvaluateConnectionAt(int connectionIndex)
        {
            PhysicsConnection connection = _connections[connectionIndex];
            Rigidbody bodyA = connection.BodyA;
            Rigidbody bodyB = connection.BodyB;

            connection.CompensationActive = 0;
            connection.CompensatedBody = null;

            if (connection.Kind == PhysicsConnectionKind.Dock)
            {
                if (bodyA != null)
                {
                    connection.CompensationActive = 1;
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

            connection.CompensationActive = 1;
            connection.CompensatedBody = massA >= massB ? bodyA : bodyB;
            _connections[connectionIndex] = connection;
        }

        private void RefreshTrackedBodies(float fixedDeltaTime)
        {
            float safeDeltaTime = math.max(fixedDeltaTime, 0.0001f);
            NativeArray<float3> lastValidPositions = default;
            bool lastValidPositionsLocked = false;
            if (_lastValidPositions.IsCreated)
            {
                lastValidPositionsLocked = _lastValidPositions.TryAcquireWriteLock(out lastValidPositions);
                if (!lastValidPositionsLocked)
                    _physicsCullingLockContentionsThisFrame++;
            }

            try
            {
                for (int i = _trackedBodyCount - 1; i >= 0; i--)
                {
                    Rigidbody body = _trackedBodies[i];
                    if (body == null)
                    {
                        _deferredNullTrackedBodyCleanup = true;
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

                    if (lastValidPositionsLocked && (uint)i < (uint)lastValidPositions.Length)
                    {
                        bodyState.HasLastValidPosition = 1;
                        lastValidPositions[i] = new float3(bodyPosition.x, bodyPosition.y, bodyPosition.z);
                    }

                    if (TryResolveAupFromRuntimeOrigin(bodyPosition, out AbsoluteUniversePosition bodyAup))
                    {
                        bodyState.LastValidAup = bodyAup;
                        bodyState.HasLastValidAup = 1;
                    }

                    bodyState.LastValidLinearVelocity = currentLinearVelocity;
                    Vector3 bodyAngularVelocity = body.angularVelocity;
                    bodyState.LastValidAngularVelocity = IsFinite(bodyAngularVelocity) ? bodyAngularVelocity : Vector3.zero;
                    _bodyStates[i] = bodyState;
                }
            }
            finally
            {
                if (lastValidPositionsLocked)
                    _lastValidPositions.ReleaseWriteLock();
            }
        }

        private void ApplyAupJitterSentinel()
        {
            if (!ShouldRunAupJitterSentinelFrame() ||
                !_lastValidPositions.IsCreated ||
                _trackedBodyCount <= 0 ||
                HectonFloatingOrigin.IsShiftInProgress)
                return;

            IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            Rigidbody playerBody = runtimeContext != null ? runtimeContext.PlayerRigidbody : null;
            ApplyAupJitterSentinelForBody(playerBody);

            Rigidbody submarineBody = _submarineHullBody;
            if (submarineBody != null && !ReferenceEquals(submarineBody, playerBody))
                ApplyAupJitterSentinelForBody(submarineBody);
        }

        private bool ShouldRunAupJitterSentinelFrame()
        {
            int frame = ResolveCurrentDispatcherFrameIndex();
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
                TryRemoveNullTrackedBodyAt(bodyIndex);
                return;
            }

            RigidbodyState bodyState = _bodyStates[bodyIndex];
            if (bodyState.HasLastValidAup == 0)
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

            if (TryAcquireTrackedBodyPositionPublishLocks1337(
                includeRigidbodyAups: true,
                out NativeArray<float3> lastValidPositions,
                out NativeArray<double3> rigidbodyAups,
                out bool lastValidPositionsLocked,
                out bool rigidbodyAupsLocked))
            {
                try
                {
                    if (lastValidPositionsLocked && (uint)bodyIndex < (uint)lastValidPositions.Length)
                        lastValidPositions[bodyIndex] = aupRuntimePosition3;
                    if (rigidbodyAupsLocked && (uint)bodyIndex < (uint)rigidbodyAups.Length)
                        rigidbodyAups[bodyIndex] = bodyState.LastValidAup.ToAbsoluteDouble3();
                }
                finally
                {
                    ReleaseTrackedBodyPositionPublishLocks1337(lastValidPositionsLocked, rigidbodyAupsLocked);
                }
            }
            else
            {
                _physicsCullingLockContentionsThisFrame++;
            }

            bodyState.HasLastValidPosition = 1;
            bodyState.HasLastValidAup = 1;
            bodyState.LastValidLinearVelocity = linearVelocity;
            bodyState.LastValidAngularVelocity = angularVelocity;
            _bodyStates[bodyIndex] = bodyState;

            CrashTelemetryBuffer.ReportAupJitterCorrection(bodyPosition, EstimateMagnitudeNoSqrt(correctionSq));
        }

        private void ReportKineticAnomalyOncePerFrame(Vector3 bodyPosition, Vector3 deltaVelocity, float acceleration)
        {
            int frame = ResolveCurrentDispatcherFrameIndex();
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

                DumpPhysicsCullingBlackBox(PhysicsCullingRigidBodyNanHash, 1f);
                RigidbodyState bodyState = _bodyStates[i];
                float3 lastValidPosition = bodyState.HasLastValidAup != 0
                    ? bodyState.LastValidAup.ToRuntimeFloat3()
                    : bodyState.HasLastValidPosition != 0
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

                Quaternion recoveredRotation = math.any(rotationNaNMask) ? Quaternion.identity : SanitizeQuaternion(body.rotation);
                TeleportBodyWithoutBroadphaseImpulse(
                    body,
                    recoveredRuntimePosition,
                    recoveredRotation,
                    Vector3.zero,
                    Vector3.zero,
                    true);
                bodyState.LastValidLinearVelocity = Vector3.zero;
                bodyState.LastValidAngularVelocity = Vector3.zero;
                bodyState.HasLastValidPosition = 1;
                AbsoluteUniversePosition recoveredAup = default;
                bool hasRecoveredAup = TryResolveAupFromRuntimeOrigin(recoveredRuntimePosition, out recoveredAup);
                if (hasRecoveredAup)
                {
                    bodyState.LastValidAup = recoveredAup;
                    bodyState.HasLastValidAup = 1;
                }
                else
                {
                    bodyState.HasLastValidAup = 0;
                }
                if (TryAcquireTrackedBodyPositionPublishLocks1337(
                    includeRigidbodyAups: true,
                    out NativeArray<float3> lastValidPositions,
                    out NativeArray<double3> rigidbodyAups,
                    out bool lastValidPositionsLocked,
                    out bool rigidbodyAupsLocked))
                {
                    try
                    {
                        if (lastValidPositionsLocked && (uint)i < (uint)lastValidPositions.Length)
                            lastValidPositions[i] = lastValidPosition;
                        if (rigidbodyAupsLocked && (uint)i < (uint)rigidbodyAups.Length)
                            rigidbodyAups[i] = hasRecoveredAup ? recoveredAup.ToAbsoluteDouble3() : default;
                    }
                    finally
                    {
                        ReleaseTrackedBodyPositionPublishLocks1337(lastValidPositionsLocked, rigidbodyAupsLocked);
                    }
                }
                else
                {
                    _physicsCullingLockContentionsThisFrame++;
                }

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
            if (body == null ||
                !IsFinite(targetPosition) ||
                !TryNormalizeQuaternion(targetRotation, out Quaternion normalizedRotation))
                return;

            bool wasKinematic = body.isKinematic;
            bool wasDetectingCollisions = body.detectCollisions;
            body.isKinematic = true;
            body.detectCollisions = false;
            body.ResetCenterOfMass();
            body.ResetInertiaTensor();
            body.position = targetPosition;
            body.rotation = normalizedRotation;
            body.PublishTransform();
            body.isKinematic = wasKinematic;
            body.detectCollisions = wasDetectingCollisions;

            if (!wasKinematic)
            {
                PhysicsForceRouter.QueueLinearVelocitySet(body, IsFinite(linearVelocity) ? linearVelocity : Vector3.zero);
                PhysicsForceRouter.QueueAngularVelocitySet(body, IsFinite(angularVelocity) ? angularVelocity : Vector3.zero);
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

        private bool TryAcquireTrackedBodyPositionPublishLocks1337(
            bool includeRigidbodyAups,
            out NativeArray<float3> lastValidPositions,
            out NativeArray<double3> rigidbodyAups,
            out bool lastValidPositionsLocked,
            out bool rigidbodyAupsLocked)
        {
            lastValidPositions = default;
            rigidbodyAups = default;
            lastValidPositionsLocked = false;
            rigidbodyAupsLocked = false;
            ulong mutationGuardMask = 0UL;
            if (_lastValidPositions.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.RigidbodyLastValidPositions);
            if (includeRigidbodyAups && _rigidbodyAUPs.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.RigidbodyAUPs);
            if (mutationGuardMask == 0UL)
                return true;

            if (!TryAcquirePhysicsMutationGuard(mutationGuardMask))
                return false;

            bool success = false;
            try
            {
                if (_lastValidPositions.IsCreated)
                {
                    if (!_lastValidPositions.TryResolve(out lastValidPositions))
                        return false;
                    lastValidPositionsLocked = true;
                }

                if (includeRigidbodyAups && _rigidbodyAUPs.IsCreated)
                {
                    if (!_rigidbodyAUPs.TryResolve(out rigidbodyAups))
                        return false;
                    rigidbodyAupsLocked = true;
                }

                success = true;
                return true;
            }
            finally
            {
                if (!success)
                {
                    lastValidPositions = default;
                    rigidbodyAups = default;
                    lastValidPositionsLocked = false;
                    rigidbodyAupsLocked = false;
                    ReleasePhysicsMutationGuard(mutationGuardMask);
                }
            }
        }

        private void ReleaseTrackedBodyPositionPublishLocks1337(bool lastValidPositionsLocked, bool rigidbodyAupsLocked)
        {
            ulong mutationGuardMask = 0UL;
            if (lastValidPositionsLocked)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.RigidbodyLastValidPositions);
            if (rigidbodyAupsLocked)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.RigidbodyAUPs);
            ReleasePhysicsMutationGuard(mutationGuardMask);
        }

        private bool TryAcquirePhysicsCullingSchedulingLocks1337()
        {
            if (!TryAcquirePhysicsMutationGuard(PhysicsCullingSchedulingMutationGuardMask1337))
                return false;

            if (ValidatePhysicsCullingSchedulingViews1337())
                return true;

            ReleasePhysicsMutationGuard(PhysicsCullingSchedulingMutationGuardMask1337);
            return false;
        }

        private void ReleasePhysicsCullingSchedulingLocks1337()
        {
            ReleasePhysicsMutationGuard(PhysicsCullingSchedulingMutationGuardMask1337);
        }

        private bool TryAcquirePhysicsCullingDispatchLocks1337()
        {
            if (!TryAcquirePhysicsMutationGuard(PhysicsCullingDispatchMutationGuardMask1337))
                return false;

            if (ValidatePhysicsCullingDispatchViews1337())
                return true;

            ReleasePhysicsMutationGuard(PhysicsCullingDispatchMutationGuardMask1337);
            return false;
        }

        private void ReleasePhysicsCullingDispatchLocks1337()
        {
            ReleasePhysicsMutationGuard(PhysicsCullingDispatchMutationGuardMask1337);
        }

        private bool ValidatePhysicsCullingSchedulingViews1337()
        {
            return _rigidbodyAUPs.HasValidView() &&
                _rigidbodyCullingStateSnapshot.HasValidView() &&
                _rigidbodyAwakeResults.HasValidView() &&
                _rigidbodyCullingCommandResults.HasValidView() &&
                _rigidbodyDistanceSqResults.HasValidView() &&
                _physicsCullingDtos.HasValidView() &&
                _physicsCullingStateAges.HasValidView() &&
                _physicsCullingSpatialCandidates.HasValidView() &&
                _physicsCullingSpatialCandidateMask.HasValidView() &&
                _physicsMockSeismicSignals.HasValidView() &&
                _physicsSpatialBucketHeads.HasValidView() &&
                _physicsSpatialNext.HasValidView() &&
                _physicsSpatialCellHashes.HasValidView() &&
                _physicsStateChangedIndices.HasValidView() &&
                _physicsStateChangedCount.HasValidView() &&
                _physicsCullingFrameTelemetry.HasValidView();
        }

        private bool ValidatePhysicsCullingDispatchViews1337()
        {
            return _physicsStateChangedIndices.HasValidView() &&
                _physicsStateChangedCount.HasValidView() &&
                _rigidbodyAwakeResults.HasValidView() &&
                _rigidbodyCullingCommandResults.HasValidView() &&
                _rigidbodyDistanceSqResults.HasValidView();
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
            if (!_isInitialized || (_trackedBodyCount <= 0 && _physicsCullingMockBodyCount <= 0))
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

            RemoveNullTrackedBodiesOutsidePhysicsCullingLocks();
            if (_trackedBodyCount <= 0 && _physicsCullingMockBodyCount <= 0)
            {
                _culledBodyCount = 0;
                return;
            }

            FlushPhysicsWakeRequests();
            FlushPhysicsTargetWakeRequests();
            TickPhysicsCullingCsvOverrideMonitor();

            if (HectonFloatingOrigin.IsShiftInProgress ||
                !HasRequiredNativeState() ||
                !TryResolvePhysicsCullingPlayerState(out AbsoluteUniversePosition playerAup, out float3 cameraForward, out float depthMeters))
            {
                return;
            }

            AbsoluteUniversePosition cameraAup = ResolvePhysicsCullingCameraAup(in playerAup, ref cameraForward);
            double3 cameraAbsoluteAup = cameraAup.ToAbsoluteDouble3();
            bool hasFrustumPlanes = TryResolvePhysicsCullingFrustumPlanes(
                in cameraAup,
                out float4 frustumPlane0,
                out float4 frustumPlane1,
                out float4 frustumPlane2,
                out float4 frustumPlane3,
                out float4 frustumPlane4,
                out float4 frustumPlane5);

            if (!TryAcquirePhysicsCullingSchedulingLocks1337())
            {
                _physicsCullingLockContentionsThisFrame++;
                return;
            }

            try
            {
            int bodyIndex = 0;
            while (bodyIndex < _trackedBodyCount)
            {
                Rigidbody body = _trackedBodies[bodyIndex];
                if (body == null)
                {
                    _rigidbodyAUPs[bodyIndex] = double3.zero;
                    _rigidbodyCullingStateSnapshot[bodyIndex] = CullingStateIgnoreCulling;
                    bodyIndex++;
                    continue;
                }

                RigidbodyState bodyState = _bodyStates[bodyIndex];
                if (!TryUpdateTrackedBodyAupCache(body, ref bodyState, out AbsoluteUniversePosition bodyAup))
                {
                    _bodyStates[bodyIndex] = bodyState;
                    _rigidbodyAUPs[bodyIndex] = double3.zero;
                    _rigidbodyCullingStateSnapshot[bodyIndex] = CullingStateIgnoreCulling;
                    bodyIndex++;
                    continue;
                }

                _bodyStates[bodyIndex] = bodyState;
                _rigidbodyAUPs[bodyIndex] = bodyAup.ToAbsoluteDouble3();
                _rigidbodyCullingStateSnapshot[bodyIndex] = BuildPhysicsCullingStateSnapshot(in bodyState);
                WritePhysicsCullingDto(bodyIndex, body, in bodyState, in bodyAup);
                bodyIndex++;
            }

            int jobCount = math.min(MaxTrackedBodies, _trackedBodyCount + _physicsCullingMockBodyCount);
            if (jobCount <= 0)
            {
                _culledBodyCount = 0;
                return;
            }

            if (TrySchedulePendingMockSeismicShockwave(jobCount))
                return;

            int candidateCount = BuildPhysicsCullingSpatialCandidates(in cameraAup, jobCount);
            if (candidateCount <= 0)
            {
                RecordShinobu37PhysicsCullingFrameTelemetry(0f, 0);
                return;
            }

            JobHandle changedIndexClearHandle = SchedulePhysicsChangedIndexClear(jobCount, default);
            float fixedDeltaTime = math.max(_lastFixedDeltaTime, PhysicsFixedStepSeconds);
            PhysicsDistanceCullingJobShinobu37 job = new PhysicsDistanceCullingJobShinobu37
            {
                Dtos = _physicsCullingDtos,
                CurrentStates = _rigidbodyCullingStateSnapshot,
                CandidateIndices = _physicsCullingSpatialCandidates,
                AwakeResults = _rigidbodyAwakeResults,
                CommandResults = _rigidbodyCullingCommandResults,
                DistanceSqResults = _rigidbodyDistanceSqResults,
                StateAges = _physicsCullingStateAges,
                ChangedIndices = _physicsStateChangedIndices,
                CameraAbsoluteAup = cameraAbsoluteAup,
                CameraForward = cameraForward,
                KinematicSleepDistanceMeters = KinematicCullDistanceMeters,
                KinematicWakeDistanceMeters = KinematicRestoreDistanceMeters,
                MeshColliderStripDistanceMeters = GlobalPhysicsStateManager.MeshColliderStripDistanceMeters,
                MeshColliderRestoreDistanceMeters = GlobalPhysicsStateManager.MeshColliderRestoreDistanceMeters,
                FrustumPlane0 = frustumPlane0,
                FrustumPlane1 = frustumPlane1,
                FrustumPlane2 = frustumPlane2,
                FrustumPlane3 = frustumPlane3,
                FrustumPlane4 = frustumPlane4,
                FrustumPlane5 = frustumPlane5,
                FrustumInnerSphereSq = ResolvePhysicsCullingFrustumInnerSphereSq(),
                HardwareRadiusSqScale = ResolvePhysicsCullingHardwareRadiusSqScale(),
                HysteresisSeconds = ResolvePhysicsCullingHysteresisSeconds(),
                DeltaTimeSeconds = fixedDeltaTime,
                AbyssalDepthCull = depthMeters >= AbyssalDepthThresholdMeters ? (byte)1 : (byte)0,
                UseFrustum = hasFrustumPlanes ? (byte)1 : (byte)0
            };

            _physicsCullingJobCount = jobCount;
            _physicsCullingJobDiscardRequested = false;
            _physicsCullingJobScheduleTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            JobHandle cullingHandle = job.Schedule(candidateCount, 32, changedIndexClearHandle);
            _physicsCullingJobHandle = SchedulePhysicsChangedIndexCompaction(jobCount, cullingHandle);
            _physicsCullingJobScheduled = true;
            H8Memory.RegisterActiveJob(OwnerSystemId, _physicsCullingJobHandle);
            JobHandle.ScheduleBatchedJobs();
            }
            finally
            {
                ReleasePhysicsCullingSchedulingLocks1337();
            }
        }

        private bool TryCompletePhysicsCullingJobNonBlocking()
        {
            if (!_physicsCullingJobScheduled)
                return true;

            if (!_physicsCullingJobHandle.IsCompleted)
                return false;

            CompletePhysicsCullingJobForStateMutationBarrier(discardResults: false);
            return true;
        }

        private void CompletePhysicsCullingJobForStateMutationBarrier(bool discardResults)
        {
            if (!_physicsCullingJobScheduled)
                return;

            if (discardResults)
                _physicsCullingJobDiscardRequested = true;

            bool shouldDiscard = _physicsCullingJobDiscardRequested;
            // [BLOCKING_SYNC_POINT] Discard paths are structural mutation/origin-shift/teardown barriers;
            // normal culling result publication uses non-blocking finalization only.
            bool completed = shouldDiscard
                ? TryCompletePhysicsCullingJobForStateMutationBarrier(ref _physicsCullingJobHandle)
                : DispatcherJobSwap.TryFinalizeCompleted(ref _physicsCullingJobHandle);
            if (!completed)
                return;

            long completionTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            _physicsCullingLastJobMicroseconds = _physicsCullingJobScheduleTicks > 0L
                ? (float)((completionTicks - _physicsCullingJobScheduleTicks) * 1000000.0 / System.Diagnostics.Stopwatch.Frequency)
                : 0f;
            _physicsCullingJobScheduleTicks = 0L;
            int jobCount = math.min(
                _physicsCullingJobCount,
                math.max(_trackedBodyCount, _physicsCullingMockBodyCount));
            _physicsCullingJobScheduled = false;
            _physicsCullingJobDiscardRequested = false;
            _physicsCullingJobCount = 0;
            H8Memory.RegisterActiveJob(OwnerSystemId, default);

            if (shouldDiscard)
            {
                _physicsCullingLastJobMicroseconds = 0f;
                return;
            }

            DispatchPhysicsCullingResults(jobCount);
        }

        private static bool TryCompletePhysicsCullingJobForStateMutationBarrier(ref JobHandle handle)
        {
            bool completed;
            DispatcherJobSwap.BeginPostFixedSwapWindow();
            try
            {
                completed = DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
            }
            finally
            {
                DispatcherJobSwap.EndPostFixedSwapWindow();
            }

            return completed;
        }

        private void DispatchPhysicsCullingResults(int jobCount)
        {
            long syncStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            _physicsCullingLockContentionsThisFrame = 0;
            _physicsCullingInvalidInputDumpPending = 0;
            _physicsCullingInvalidInputDumpScalar = 0f;
            bool staleBodyCleanupRequired = false;
            int drainedCount = 0;
            int snapshotCount = 0;
            if (!TryAcquirePhysicsCullingDispatchLocks1337())
            {
                _physicsCullingLockContentionsThisFrame++;
                return;
            }

            try
            {
                if (_physicsStateChangedIndices.IsCreated && _physicsStateChangedCount.IsCreated)
                {
                    int maxDrain = math.max(jobCount, MaxTrackedBodies);
                    int queueCapacity = math.min(_physicsStateChangedIndices.Length, maxDrain);
                    PhysicsCullingCounter64 changedCounter = _physicsStateChangedCount[0];
                    int available = math.clamp(changedCounter.Value, 0, queueCapacity);
                    while (drainedCount < available)
                    {
                        int i = _physicsStateChangedIndices[drainedCount];
                        drainedCount++;
                        if ((uint)i >= (uint)_trackedBodyCount)
                            continue;

                        int scratchIndex = snapshotCount;
                        if ((uint)scratchIndex >= (uint)_physicsCullingDispatchIndexScratch.Length)
                        {
                            _physicsCullingLockContentionsThisFrame++;
                            break;
                        }

                        _physicsCullingDispatchIndexScratch[scratchIndex] = i;
                        _physicsCullingDispatchAwakeScratch[scratchIndex] = _rigidbodyAwakeResults[i];
                        _physicsCullingDispatchCommandScratch[scratchIndex] = _rigidbodyCullingCommandResults[i];
                        _physicsCullingDispatchDistanceSqScratch[scratchIndex] = _rigidbodyDistanceSqResults[i];
                        snapshotCount++;
                    }

                    _physicsStateChangedCount[0] = default;
                }
            }
            finally
            {
                ReleasePhysicsCullingDispatchLocks1337();
            }

            for (int scratchIndex = 0; scratchIndex < snapshotCount; scratchIndex++)
            {
                int i = _physicsCullingDispatchIndexScratch[scratchIndex];
                byte awakeResult = _physicsCullingDispatchAwakeScratch[scratchIndex];
                byte command = _physicsCullingDispatchCommandScratch[scratchIndex];
                float distanceSq = _physicsCullingDispatchDistanceSqScratch[scratchIndex];

                if ((uint)i >= (uint)_trackedBodyCount)
                {
                    _physicsCullingDispatchIndexScratch[scratchIndex] = -1;
                    continue;
                }

                Rigidbody body = _trackedBodies[i];
                if (body == null)
                {
                    staleBodyCleanupRequired = true;
                    _physicsCullingDispatchIndexScratch[scratchIndex] = -1;
                    continue;
                }

                RigidbodyState bodyState = _bodyStates[i];
                ApplyPhysicsCullingCommand(i, body, ref bodyState, awakeResult, command, distanceSq);

                _bodyStates[i] = bodyState;
            }

            int culledCount = CountCulledTrackedBodies(out int activeBodies, out int asleepBodies);
            _culledBodyCount = culledCount;
            FlushPhysicsCullingDispatchTelemetry(snapshotCount, culledCount);
            ClearPhysicsCullingDispatchScratch(snapshotCount);

            long syncEndTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            float syncTimeMs = (float)((syncEndTicks - syncStartTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            RecordShinobu37PhysicsCullingFrameTelemetry(syncTimeMs, drainedCount, activeBodies, asleepBodies);
            bool dumpOverBudget = syncTimeMs > PhysicsCullingStateSyncDumpThresholdMs;

            bool dumpInvalidInput = _physicsCullingInvalidInputDumpPending != 0;
            float dumpInvalidInputScalar = _physicsCullingInvalidInputDumpScalar;
            _physicsCullingInvalidInputDumpPending = 0;
            _physicsCullingInvalidInputDumpScalar = 0f;
            if (staleBodyCleanupRequired)
                RemoveNullTrackedBodiesOutsidePhysicsCullingLocks();

            if (dumpInvalidInput)
                DumpPhysicsCullingBlackBox(PhysicsCullingInvalidInputHash, dumpInvalidInputScalar);

            if (dumpOverBudget)
                DumpPhysicsCullingBlackBox(PhysicsCullingStateSyncOverBudgetHash, syncTimeMs);
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
                _physicsCullingInvalidInputDumpScalar = command;
                _physicsCullingInvalidInputDumpPending = 1;
                return false;
            }

            bool cullingAllowed = bodyState.AllowDistanceKinematicSleep != 0 &&
                bodyState.CompensationRefCount <= 0 &&
                bodyState.CullingLockRefCount <= 0 &&
                (bodyState.CullingFlags & PhysicsCullingFlags.IgnoreCulling) == 0;
            if (!cullingAllowed)
            {
                if (bodyState.CompensationRefCount > 0 || bodyState.CullingLockRefCount > 0)
                    _physicsCullingLockContentionsThisFrame++;

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
                RestoreDistanceKinematicCull(bodyIndex, body, ref bodyState, forceWake: awakeResult != 0);

            bool shouldSleep = awakeResult == 0;
            if (shouldSleep)
                ApplyDistanceSleep(bodyIndex, body, ref bodyState, distanceSq);
            else
                RestoreDistanceSleep(bodyIndex, body, ref bodyState, forceWake: true);

            if (shouldKinematic)
                ApplyDistanceKinematicCull(body, ref bodyState);

            return bodyState.DistanceSleepActive != 0 ||
                bodyState.DistanceKinematicSleepActive != 0 ||
                bodyState.MeshColliderStripActive != 0;
        }

        private void ApplyDistanceSleep(int bodyIndex, Rigidbody body, ref RigidbodyState bodyState, float distanceSq)
        {
            if (body == null)
                return;

            if (bodyState.DistanceSleepActive != 0)
            {
                if (!body.IsSleeping())
                {
                    FreezeBodyVelocityForDistanceSleep(bodyIndex, body);
                    DisableSleepColliders(bodyIndex, ref bodyState);
                    body.Sleep();
                }

                return;
            }

            bodyState.WasSleepingBeforeDistanceSleep = body.IsSleeping() ? (byte)1 : (byte)0;
            if (bodyState.WasSleepingBeforeDistanceSleep != 0)
                bodyState.StateMask |= PhysicsStateMask.WasAsleep;
            else
                bodyState.StateMask &= ~PhysicsStateMask.WasAsleep;

            FreezeBodyVelocityForDistanceSleep(bodyIndex, body);
            DisableSleepColliders(bodyIndex, ref bodyState);
            body.Sleep();
            bodyState.DistanceSleepActive = 1;
            WritePhysicsCullingDtoSleepState(bodyIndex, 1);
            PublishRigidbodySleepSignal(in bodyState, body != null ? body.position : Vector3.zero, distanceSq, 1);
        }

        private void RestoreDistanceSleep(int bodyIndex, Rigidbody body, ref RigidbodyState bodyState, bool forceWake)
        {
            if (bodyState.DistanceSleepActive == 0)
                return;

            bool wasAsleepBeforeEviction = bodyState.WasSleepingBeforeDistanceSleep != 0 ||
                                           (bodyState.StateMask & PhysicsStateMask.WasAsleep) != 0;
            bodyState.WasSleepingBeforeDistanceSleep = 0;
            bodyState.StateMask &= ~PhysicsStateMask.WasAsleep;
            bodyState.DistanceSleepActive = 0;

            if (body != null && !body.isKinematic)
            {
                RestoreSleepColliders(bodyIndex, ref bodyState);
                RestoreFrozenVelocityForDistanceSleep(bodyIndex, body);
                if (forceWake || !wasAsleepBeforeEviction)
                    body.WakeUp();
                else
                    body.Sleep();
            }
            else
            {
                RestoreSleepColliders(bodyIndex, ref bodyState);
            }

            WritePhysicsCullingDtoSleepState(bodyIndex, 0);
            PublishRigidbodySleepSignal(in bodyState, body != null ? body.position : Vector3.zero, 0d, 0);
        }

        private static void ApplyDistanceKinematicCull(Rigidbody body, ref RigidbodyState bodyState)
        {
            if (body == null)
                return;

            if (bodyState.DistanceKinematicSleepActive != 0)
            {
                body.isKinematic = true;
                body.detectCollisions = false;
                if (!body.IsSleeping())
                    body.Sleep();

                return;
            }

            bodyState.KinematicModeBeforeDistanceSleep = body.isKinematic ? (byte)1 : (byte)0;
            bodyState.DetectCollisionsBeforeDistanceSleep = body.detectCollisions ? (byte)1 : (byte)0;
            body.isKinematic = true;
            body.detectCollisions = false;
            body.Sleep();
            bodyState.DistanceKinematicSleepActive = 1;
        }

        private void RestoreDistanceKinematicCull(int bodyIndex, Rigidbody body, ref RigidbodyState bodyState, bool forceWake)
        {
            if (bodyState.DistanceKinematicSleepActive == 0)
                return;

            if (body != null)
            {
                body.isKinematic = bodyState.KinematicModeBeforeDistanceSleep != 0;
                body.detectCollisions = bodyState.DetectCollisionsBeforeDistanceSleep != 0;
                if (!body.isKinematic)
                    RestoreFrozenVelocityForDistanceSleep(bodyIndex, body);
                if (forceWake && !body.isKinematic)
                    body.WakeUp();
            }

            bodyState.DistanceKinematicSleepActive = 0;
        }

        private void RestoreAllPhysicsCullingState(int bodyIndex, Rigidbody body, ref RigidbodyState bodyState, bool forceWake)
        {
            if ((uint)bodyIndex < (uint)_trackedBodyCount)
                RestoreMeshColliderStrip(bodyIndex, ref bodyState);

            RestoreDistanceKinematicCull(bodyIndex, body, ref bodyState, forceWake);
            RestoreDistanceSleep(bodyIndex, body, ref bodyState, forceWake);
        }

        private static byte BuildPhysicsCullingStateSnapshot(in RigidbodyState bodyState)
        {
            byte state = 0;
            if (bodyState.DistanceSleepActive != 0)
                state |= CullingStateSleepActive;
            if (bodyState.DistanceKinematicSleepActive != 0)
                state |= CullingStateKinematicActive;
            if (bodyState.MeshColliderStripActive != 0)
                state |= CullingStateMeshColliderStripped;
            if (bodyState.AllowDistanceKinematicSleep == 0 ||
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
            return DefaultSleepDistanceMeters;
        }

        private static float ResolveWakeDistanceMeters(float sleepDistanceMeters)
        {
            return math.min(DefaultWakeDistanceMeters, sleepDistanceMeters - SleepWakeHysteresisMeters);
        }

        private static bool TryResolvePhysicsCullingPlayerState(
            out AbsoluteUniversePosition playerAup,
            out float3 cameraForward,
            out float depthMeters)
        {
            playerAup = default;
            cameraForward = new float3(0f, 0f, 1f);
            depthMeters = 0f;

            IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            if (runtimeContext == null)
            {
                return false;
            }

            if (!runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) ||
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                !IsFinite(in movementState.PredictedAup))
            {
                return false;
            }

            playerAup = movementState.PredictedAup;
            cameraForward = NormalizeWithRsqrtGuard(movementState.CameraForward, new float3(0f, 0f, 1f));
            float rawDepthMeters = movementState.DepthMeters;
            depthMeters = math.isfinite(rawDepthMeters) ? math.max(0f, rawDepthMeters) : 0f;
            return math.all(math.isfinite(cameraForward));
        }

        private void ApplyMeshColliderStrip(int bodyIndex, ref RigidbodyState bodyState)
        {
            if (bodyState.MeshColliderCount <= 0)
                return;

            // L19 hop2 LIVE: mesh-collider enable/disable rebuilds PhysX mass/inertia mid-simulate.
            if (Application.isBatchMode)
                return;

            if (bodyState.MeshColliderStripActive != 0)
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

                bool wasEnabled = meshCollider.enabled;
                _trackedMeshColliderEnabledBeforeStrip[colliderIndex] = wasEnabled ? (byte)1 : (byte)0;
                if (wasEnabled)
                {
                    meshCollider.enabled = false;
                    RecordPhysicsColliderToggleTransition();
                }
            }

            bodyState.MeshColliderStripActive = 1;
        }

        private void EnforceMeshColliderStrip(int bodyIndex, byte meshColliderCount)
        {
            int baseIndex = bodyIndex * MaxMeshCollidersPerBody;
            int count = math.min((int)meshColliderCount, MaxMeshCollidersPerBody);
            for (int i = 0; i < count; i++)
            {
                MeshCollider meshCollider = _trackedMeshColliders[baseIndex + i];
                if (meshCollider != null && meshCollider.enabled)
                {
                    meshCollider.enabled = false;
                    RecordPhysicsColliderToggleTransition();
                }
            }
        }

        private void RestoreMeshColliderStrip(int bodyIndex, ref RigidbodyState bodyState)
        {
            if (bodyState.MeshColliderStripActive == 0 || bodyState.MeshColliderCount <= 0)
                return;

            // L19 hop2 LIVE: mesh-collider enable/disable rebuilds PhysX mass/inertia mid-simulate.
            if (Application.isBatchMode)
                return;

            int baseIndex = bodyIndex * MaxMeshCollidersPerBody;
            int count = math.min((int)bodyState.MeshColliderCount, MaxMeshCollidersPerBody);
            for (int i = 0; i < count; i++)
            {
                int colliderIndex = baseIndex + i;
                MeshCollider meshCollider = _trackedMeshColliders[colliderIndex];
                if (meshCollider != null)
                {
                    bool shouldEnable = _trackedMeshColliderEnabledBeforeStrip[colliderIndex] != 0;
                    if (meshCollider.enabled != shouldEnable)
                    {
                        meshCollider.enabled = shouldEnable;
                        RecordPhysicsColliderToggleTransition();
                    }
                }

                _trackedMeshColliderEnabledBeforeStrip[colliderIndex] = 0;
            }

            bodyState.MeshColliderStripActive = 0;
        }

        private byte CacheMeshCollidersForBody(Rigidbody body, int bodyIndex)
        {
            ClearMeshColliderRefs(bodyIndex);
            if (body == null)
                return 0;

            if (!TryResolvePhysicsCullingColliderCache(body, out IPhysicsCullingColliderCache colliderCache) ||
                !colliderCache.TryGetPhysicsCullingColliders(out Collider[] colliders, out int colliderCount))
            {
                return 0;
            }

            int readCount = colliders != null ? math.min(colliderCount, colliders.Length) : 0;
            int count = 0;
            int baseIndex = bodyIndex * MaxMeshCollidersPerBody;
            for (int i = 0; i < readCount && count < MaxMeshCollidersPerBody; i++)
            {
                if (colliders[i] is MeshCollider meshCollider)
                    _trackedMeshColliders[baseIndex + count++] = meshCollider;
            }

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

        private void FlushPhysicsCullingDispatchTelemetry(int snapshotCount, int culledCount)
        {
            if (snapshotCount <= 0 || !_physicsCullingTelemetry.IsCreated)
                return;

            int frame = ResolveCurrentDispatcherFrameIndex();
            float qualityWeight = ResolvePhysicsCullingQualityWeight01();
            ushort ccdInterventions = ResolveKinematicCcdInterventionsForFrame(frame);

            if (!_physicsCullingTelemetry.TryAcquireWriteLock(out NativeArray<PhysicsCullingTelemetryEntry> telemetryRing))
            {
                _physicsCullingLockContentionsThisFrame++;
                return;
            }

            try
            {
                if (!telemetryRing.IsCreated)
                    return;

                int capacity = math.min(telemetryRing.Length, PhysicsCullingTelemetryCapacity);
                if (capacity <= 0)
                    return;

                int writeIndex = _physicsCullingTelemetryWriteIndex;
                if ((uint)writeIndex >= (uint)capacity)
                    writeIndex = 0;

                for (int scratchIndex = 0; scratchIndex < snapshotCount; scratchIndex++)
                {
                    int bodyIndex = _physicsCullingDispatchIndexScratch[scratchIndex];
                    if ((uint)bodyIndex >= (uint)_trackedBodyCount)
                        continue;

                    RigidbodyState bodyState = _bodyStates[bodyIndex];
                    byte awakeResult = _physicsCullingDispatchAwakeScratch[scratchIndex];
                    byte command = _physicsCullingDispatchCommandScratch[scratchIndex];
                    float distanceSq = _physicsCullingDispatchDistanceSqScratch[scratchIndex];
                    uint stateHash = ComputePhysicsCullingBodyTelemetryHash(frame, bodyState.EntityId, distanceSq, command, awakeResult);
                    telemetryRing[writeIndex] = new PhysicsCullingTelemetryEntry
                    {
                        FrameIndex = frame,
                        TrackedBodyCount = _trackedBodyCount,
                        CulledBodyCount = culledCount,
                        LockContentions = _physicsCullingLockContentionsThisFrame,
                        BodyId = unchecked((uint)bodyState.EntityId),
                        StateHash = stateHash,
                        DistanceSq = distanceSq,
                        JobMicroseconds = _physicsCullingLastJobMicroseconds,
                        StateSyncMicroseconds = 0f,
                        GlobalQualityWeight = qualityWeight,
                        Command = command,
                        AwakeResult = awakeResult,
                        Flags = (byte)bodyState.CullingFlags,
                        CullingFlags = (uint)bodyState.CullingFlags,
                        FrameHash = stateHash ^ unchecked((uint)frame * 16777619u),
                        CcdInterventions = ccdInterventions
                    };

                    writeIndex++;
                    if (writeIndex >= capacity)
                        writeIndex = 0;
                }

                _physicsCullingTelemetryWriteIndex = writeIndex;
            }
            finally
            {
                _physicsCullingTelemetry.ReleaseWriteLock();
            }
        }

        private void ClearPhysicsCullingDispatchScratch(int snapshotCount)
        {
            int count = math.min(snapshotCount, _physicsCullingDispatchIndexScratch.Length);
            for (int i = 0; i < count; i++)
            {
                _physicsCullingDispatchIndexScratch[i] = 0;
                _physicsCullingDispatchAwakeScratch[i] = 0;
                _physicsCullingDispatchCommandScratch[i] = 0;
                _physicsCullingDispatchDistanceSqScratch[i] = 0f;
            }
        }

        private void ReportKinematicCcdInterventionInternal()
        {
            int frame = ResolveCurrentDispatcherFrameIndex();
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

        private void DumpPhysicsCullingBlackBox(uint reasonHash, float scalarValue)
        {
            if (reasonHash == 0u || GlobalTelemetryBus.BlackboxActiveFrameCount <= 0)
                return;

            float safeScalar = math.isfinite(scalarValue) ? scalarValue : 0f;
            GlobalTelemetryBus.PushEvent(reasonHash, safeScalar);
            TryDumpPhysicsCullingBlackBoxToFile(reasonHash, safeScalar);
        }

        void IAcousticPingEventListener.OnAcousticPing(in AcousticPingEvent pingEvent)
        {
            if (!IsFinite(pingEvent.RuntimePosition))
                return;

            float radiusMeters = math.clamp(
                pingEvent.RadiusMeters * math.max(pingEvent.Intensity01, 0.25f),
                AcousticWakeMinimumRadiusMeters,
                AcousticWakeMaximumRadiusMeters);
            if (!TryResolveAupFromRuntimeOrigin(pingEvent.RuntimePosition, out AbsoluteUniversePosition originAup))
                return;

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
            if (!TryResolveAupFromRuntimeOrigin(impulseEvent.RuntimePosition, out AbsoluteUniversePosition originAup))
                return;

            WakeCulledBodiesNear(in originAup, radiusMeters);
        }

        void IPhysicsImpactEventListener.OnPhysicsImpact(in PhysicsImpactSignal impactSignal)
        {
            float radiusMeters = math.clamp(
                ImpactWakeMinimumRadiusMeters + (math.max(impactSignal.Intensity, 0f) * 12f),
                ImpactWakeMinimumRadiusMeters,
                ImpactWakeMaximumRadiusMeters);
            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromAbsolutePosition(impactSignal.ResolvePointAupMeters());
            WakeCulledBodiesNear(in originAup, radiusMeters);
        }

        private void WakeCulledBodiesNear(in AbsoluteUniversePosition originAup, float radiusMeters)
        {
            TryQueuePhysicsCullingWakeRegion(in originAup, radiusMeters);
        }

        private void TickColliderLodHysteresisCadence(float fixedDeltaTime)
        {
            // L19 hop2 LIVE: collider LOD gate flips rebuild PhysX mass/inertia mid-simulate
            // and have produced native AV in setMassAndUpdateInertia under headless batch probes.
            if (Application.isBatchMode)
                return;

            _colliderLodHysteresisAccumulator += math.max(0f, fixedDeltaTime);
            if (_colliderLodHysteresisAccumulator < PhysicsCullingSlowTickIntervalSeconds)
                return;

            float elapsedSeconds = _colliderLodHysteresisAccumulator;
            _colliderLodHysteresisAccumulator = 0f;
            ApplyColliderLodHysteresisInternal(elapsedSeconds);
        }

        private void ApplyColliderLodHysteresisInternal(float fixedDeltaTime)
        {
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            float safeDeltaTime = math.max(0f, fixedDeltaTime);
            float qualityWeight = ResolvePhysicsCullingQualityWeight01();
            double compoundToSimpleDistanceSq = ResolveColliderLodCompoundToSimpleDistanceSq(qualityWeight);
            double simpleToCompoundDistanceSq = ResolveColliderLodSimpleToCompoundDistanceSq(qualityWeight);
            for (int i = _trackedBodyCount - 1; i >= 0; i--)
            {
                Rigidbody body = _trackedBodies[i];
                if (body == null)
                {
                    TryRemoveNullTrackedBodyAt(i);
                    continue;
                }

                RigidbodyState bodyState = _bodyStates[i];
                if (bodyState.HasColliderLodSink == 0 || !IsColliderLodSinkAlive(bodyState.ColliderLodSink))
                {
                    bodyState.ColliderLodOutOfRangeSeconds = 0f;
                    bodyState.ColliderLodDistanceGateOpen = 0;
                    _bodyStates[i] = bodyState;
                    continue;
                }

                if (!TryUpdateTrackedBodyAupCache(body, ref bodyState, out AbsoluteUniversePosition bodyAup))
                {
                    _bodyStates[i] = bodyState;
                    continue;
                }

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in bodyAup, in playerAup);
                if (bodyState.ColliderLodDistanceGateOpen != 0)
                {
                    if (distanceSq <= simpleToCompoundDistanceSq)
                    {
                        bodyState.ColliderLodDistanceGateOpen = 0;
                        bodyState.ColliderLodOutOfRangeSeconds = 0f;
                        RecordPhysicsColliderToggleTransitions(SetColliderLodDistanceGateAndCountTransitions(bodyState.ColliderLodSink, false));
                    }
                }
                else if (distanceSq > compoundToSimpleDistanceSq)
                {
                    bodyState.ColliderLodOutOfRangeSeconds += safeDeltaTime;
                    if (bodyState.ColliderLodOutOfRangeSeconds >= ColliderLodSimplifyHysteresisSeconds)
                    {
                        bodyState.ColliderLodDistanceGateOpen = 1;
                        RecordPhysicsColliderToggleTransitions(SetColliderLodDistanceGateAndCountTransitions(bodyState.ColliderLodSink, true));
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
                Flags = bodyState.DistanceKinematicSleepActive != 0 ? (byte)1 : (byte)0
            };
            SignalBus<RigidbodySleepSignal>.TryPushTracked(in signal, ref s_x001GlobalPhysicsStateManagerSignalPushDropCount);
        }

        private static void PublishRigidbodySleepSignal(
            in RigidbodyState bodyState,
            Vector3 runtimePosition,
            double distanceSq,
            byte sleepState)
        {
            AbsoluteUniversePosition bodyAup;
            if (bodyState.HasLastValidAup != 0)
            {
                bodyAup = bodyState.LastValidAup;
            }
            else if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out bodyAup))
            {
                return;
            }

            PublishRigidbodySleepSignal(in bodyState, in bodyAup, distanceSq, sleepState);
        }

        private void MarkAddedMassTensorDirty(int bodyIndex, ref RigidbodyState bodyState)
        {
            if ((uint)bodyIndex >= (uint)MaxTrackedBodies || bodyState.AddedMassDirty != 0)
                return;

            if (_addedMassDirtyCount >= _addedMassDirtyBodyIndices.Length)
            {
                _physicsCullingLockContentionsThisFrame++;
                return;
            }

            bodyState.AddedMassDirty = 1;
            _addedMassDirtyBodyIndices[_addedMassDirtyCount++] = bodyIndex;
        }

        private void DrainAddedMassTensorDirtyQueue()
        {
            // L19 hop2 LIVE: inertiaTensor writes rebuild PhysX mass mid-simulate and have
            // produced native AV in setMassAndUpdateInertia under headless batch probes.
            if (Application.isBatchMode)
            {
                _addedMassDirtyCount = 0;
                return;
            }

            int dirtyCount = _addedMassDirtyCount;
            _addedMassDirtyCount = 0;
            for (int cursor = 0; cursor < dirtyCount; cursor++)
            {
                int bodyIndex = _addedMassDirtyBodyIndices[cursor];
                _addedMassDirtyBodyIndices[cursor] = 0;
                if ((uint)bodyIndex >= (uint)_trackedBodyCount)
                    continue;

                Rigidbody body = _trackedBodies[bodyIndex];
                if (body == null)
                    continue;

                RigidbodyState bodyState = _bodyStates[bodyIndex];
                if (bodyState.AddedMassDirty == 0)
                    continue;

                bodyState.AddedMassDirty = 0;
                ApplyAddedMassTensorForBody(body, ref bodyState);
                _bodyStates[bodyIndex] = bodyState;
            }
        }

        private static void ApplyAddedMassTensorForBody(Rigidbody body, ref RigidbodyState bodyState)
        {
            CaptureAddedMassBaseline(body, ref bodyState);
            float submersionFactor = math.saturate(bodyState.HydrodynamicSubmersionFactor);
            bodyState.HydrodynamicSubmersionFactor = submersionFactor;
            bodyState.IsFullySubmerged = submersionFactor >= AddedMassFullySubmergedThreshold ? (byte)1 : (byte)0;
            if (submersionFactor <= AddedMassSubmersionEpsilon)
            {
                if (bodyState.AddedMassTensorApplied != 0)
                    RestoreAddedMassBaseline(body, ref bodyState);

                return;
            }

            if (bodyState.AddedMassTensorApplied != 0 &&
                math.abs(bodyState.LastAppliedAddedMassSubmersionFactor - submersionFactor) <= AddedMassSubmersionEpsilon)
                return;

            bool isFullySubmerged = bodyState.IsFullySubmerged != 0;
            float multiplier = isFullySubmerged
                ? AddedMassFullySubmergedAngularDampingMultiplier
                : 1f + (AddedMassAngularDampingScale * submersionFactor);
            float inertiaMultiplier = isFullySubmerged
                ? AddedMassFullySubmergedInertiaTensorMultiplier
                : 1f + (AddedMassInertiaTensorScale * submersionFactor);
            body.angularDamping = bodyState.BaseAngularDamping * multiplier;
            body.inertiaTensor = bodyState.BaseInertiaTensor * inertiaMultiplier;
            body.inertiaTensorRotation = SanitizeQuaternion(bodyState.BaseInertiaTensorRotation);
            bodyState.AddedMassTensorApplied = 1;
            bodyState.LastAppliedAddedMassSubmersionFactor = submersionFactor;
        }

        private static void CaptureAddedMassBaseline(Rigidbody body, ref RigidbodyState bodyState)
        {
            if (body == null || bodyState.HasAddedMassBaseline != 0)
                return;

            bodyState.BaseInertiaTensor = IsFinite(body.inertiaTensor) ? body.inertiaTensor : Vector3.one;
            bodyState.BaseInertiaTensorRotation = SanitizeQuaternion(body.inertiaTensorRotation);
            bodyState.BaseAngularDamping = math.max(0f, body.angularDamping);
            bodyState.HasAddedMassBaseline = 1;
        }

        private static void RestoreAddedMassBaseline(Rigidbody body, ref RigidbodyState bodyState)
        {
            if (body == null || bodyState.HasAddedMassBaseline == 0)
                return;

            if (IsFinite(bodyState.BaseInertiaTensor))
                body.inertiaTensor = bodyState.BaseInertiaTensor;
            if (TryNormalizeQuaternion(bodyState.BaseInertiaTensorRotation, out Quaternion restoredRotation))
                body.inertiaTensorRotation = restoredRotation;
            body.angularDamping = math.max(0f, bodyState.BaseAngularDamping);
            bodyState.AddedMassTensorApplied = 0;
            bodyState.LastAppliedAddedMassSubmersionFactor = 0f;
        }

        private void RemoveTrackedBodyAt(int bodyIndex)
        {
            CompletePhysicsCullingJobForStateMutationBarrier(discardResults: true);
            int lastIndex = _trackedBodyCount - 1;
            if (bodyIndex < 0 || bodyIndex > lastIndex)
                return;

            if (!TryAcquirePhysicsTrackedBodyLaneMutationLocks1337())
            {
                _physicsCullingLockContentionsThisFrame++;
                return;
            }

            try
            {
                RigidbodyState removedState = _bodyStates[bodyIndex];
                if (removedState.EntityId != 0ul)
                    _trackedBodyIndexByEntityId.Remove(removedState.EntityId);
                if (removedState.EntityInstanceHash != 0)
                    _trackedBodyIndexByInstanceId.Remove(removedState.EntityInstanceHash);

                Rigidbody removedBody = _trackedBodies[bodyIndex];
                if (removedBody != null)
                {
                    RestoreColliderLodGate(ref removedState);
                    RestoreSafeTeleportSpeculativeCcd(removedBody, ref removedState);
                    RestoreAllPhysicsCullingState(bodyIndex, removedBody, ref removedState, forceWake: false);
                    if (removedState.AddedMassTensorApplied != 0)
                        RestoreAddedMassBaseline(removedBody, ref removedState);
                    _trackedBodyIndexByEntityId.Remove(EntityId.ToULong(removedBody.GetEntityId()));
                    _trackedBodyIndexByInstanceId.Remove(removedBody.GetEntityId().GetHashCode());
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
                _rigidbodyAUPs[bodyIndex] = _rigidbodyAUPs[lastIndex];
                _rigidbodyAUPs[lastIndex] = default;
                MovePhysicsCullingDtoLane(lastIndex, bodyIndex);
                MoveMeshColliderRefs(lastIndex, bodyIndex);
                ClearMeshColliderRefs(lastIndex);
                MoveSleepColliderRefs(lastIndex, bodyIndex);
                ClearSleepColliderRefs(lastIndex);
                if (bodyIndex != lastIndex)
                {
                    Rigidbody movedBody = _trackedBodies[bodyIndex];
                    if (movedBody != null)
                    {
                        RigidbodyState movedState = _bodyStates[bodyIndex];
                        movedState.EntityId = EntityId.ToULong(movedBody.GetEntityId());
                        movedState.EntityInstanceHash = movedBody.GetEntityId().GetHashCode();
                        if (movedState.AddedMassDirty != 0)
                        {
                            movedState.AddedMassDirty = 0;
                            MarkAddedMassTensorDirty(bodyIndex, ref movedState);
                        }

                        _bodyStates[bodyIndex] = movedState;
                        _trackedBodyIndexByEntityId[movedState.EntityId] = bodyIndex;
                        _trackedBodyIndexByInstanceId[movedState.EntityInstanceHash] = bodyIndex;
                    }
                }
                _trackedBodyCount--;
                _connectionRefsRequireFullClear = true;
                MarkPhysicsCullingSpatialHashDirty();
            }
            finally
            {
                ReleasePhysicsTrackedBodyLaneMutationLocks1337();
            }
        }

        private void RemoveNullTrackedBodiesOutsidePhysicsCullingLocks()
        {
            if (_physicsCullingJobScheduled)
            {
                _deferredNullTrackedBodyCleanup = true;
                return;
            }

            int bodyIndex = 0;
            while (bodyIndex < _trackedBodyCount)
            {
                if (_trackedBodies[bodyIndex] == null)
                {
                    RemoveTrackedBodyAt(bodyIndex);
                    continue;
                }

                bodyIndex++;
            }

            _deferredNullTrackedBodyCleanup = false;
        }

        private bool TryRemoveNullTrackedBodyAt(int bodyIndex)
        {
            if ((uint)bodyIndex >= (uint)_trackedBodyCount || _trackedBodies[bodyIndex] != null)
                return false;

            if (_physicsCullingJobScheduled)
            {
                _deferredNullTrackedBodyCleanup = true;
                return false;
            }

            RemoveTrackedBodyAt(bodyIndex);
            return true;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            if (runtimeContext == null)
            {
                playerAup = default;
                return false;
            }

            if (runtimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                IsFinite(in snapshot.Aup))
            {
                playerAup = snapshot.Aup;
                return true;
            }

            if (runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                IsFinite(in movementState.PredictedAup))
            {
                playerAup = movementState.PredictedAup;
                return IsFinite(in playerAup);
            }

            playerAup = default;
            return false;
        }

        private static bool TryUpdateTrackedBodyAupCache(Rigidbody body, ref RigidbodyState bodyState, out AbsoluteUniversePosition bodyAup)
        {
            if (body == null)
            {
                bodyAup = default;
                return false;
            }

            Vector3 position = body.position;
            if (!IsFinite(position))
            {
                if (bodyState.HasLastValidAup != 0)
                {
                    bodyAup = bodyState.LastValidAup;
                    return true;
                }

                bodyAup = default;
                return false;
            }

            if (!TryResolveAupFromRuntimeOrigin(position, out bodyAup))
            {
                if (bodyState.HasLastValidAup != 0)
                {
                    bodyAup = bodyState.LastValidAup;
                    return true;
                }

                return false;
            }

            bodyState.LastValidAup = bodyAup;
            bodyState.HasLastValidAup = 1;
            return true;
        }

        private static PhysicsCullingFlags ScanCullingFlagsFromComponents(Rigidbody body)
        {
            if (body == null)
                return PhysicsCullingFlags.IgnoreCulling;

            PhysicsCullingFlags flags = PhysicsCullingFlags.None;
            if (body.TryGetComponent(out IPhysicsCullingFlagProvider directProvider))
                flags |= directProvider.CullingFlags;

            if (TryResolveComponentInParents(body.transform.parent, out IPhysicsCullingFlagProvider parentProvider) &&
                !ReferenceEquals(parentProvider, directProvider))
            {
                flags |= parentProvider.CullingFlags;
            }

            if (body.CompareTag("Player"))
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

        private static IPhysicsColliderLodHysteresisSink FindColliderLodSinkFromComponent(Rigidbody body)
        {
            if (body != null && body.TryGetComponent(out IPhysicsColliderLodHysteresisSink sink))
                return sink;

            return null;
        }

        private static bool TryResolveComponentInParents<T>(Transform current, out T component)
        {
            component = default;

            for (; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out component))
                    return true;
            }

            return false;
        }

        private static bool IsColliderLodSinkAlive(IPhysicsColliderLodHysteresisSink sink)
        {
            if (sink == null)
                return false;

            return !(sink is UnityEngine.Object unityObject) || unityObject != null;
        }

        private static int SetColliderLodDistanceGateAndCountTransitions(
            IPhysicsColliderLodHysteresisSink sink,
            bool allowSimplifiedColliderLod)
        {
            if (!IsColliderLodSinkAlive(sink))
                return 0;

            if (sink is IPhysicsColliderLodTransitionSink transitionSink)
                return transitionSink.SetColliderLodDistanceGateAndCountTransitions(allowSimplifiedColliderLod);

            sink.SetColliderLodDistanceGate(allowSimplifiedColliderLod);
            return 1;
        }

        private void RestoreColliderLodGate(ref RigidbodyState bodyState)
        {
            if (bodyState.ColliderLodDistanceGateOpen == 0)
                return;

            if (IsColliderLodSinkAlive(bodyState.ColliderLodSink))
            {
                RecordPhysicsColliderToggleTransitions(SetColliderLodDistanceGateAndCountTransitions(bodyState.ColliderLodSink, false));
            }

            bodyState.ColliderLodDistanceGateOpen = 0;
            bodyState.ColliderLodOutOfRangeSeconds = 0f;
        }

        private static void RestoreSafeTeleportSpeculativeCcd(Rigidbody body, ref RigidbodyState bodyState)
        {
            if (bodyState.SafeTeleportSpeculativeCcdActive == 0)
                return;

            if (body != null)
            {
                body.collisionDetectionMode = bodyState.CollisionDetectionModeBeforeSafeTeleport;
                body.PublishTransform();
            }

            bodyState.SafeTeleportSpeculativeCcdActive = 0;
            bodyState.SafeTeleportSpeculativeFixedTicksRemaining = 0;
            bodyState.CollisionDetectionModeBeforeSafeTeleport = default;
        }

        private static float ScanMaxAngularVelocityClampFromComponents(Rigidbody body)
        {
            if (body == null)
                return 0f;

            if (body.mass >= 250f ||
                (body.TryGetComponent(out IPhysicsCullingFlagProvider cullingProvider) &&
                (cullingProvider.CullingFlags & PhysicsCullingFlags.HeavyCollider) != 0))
            {
                return 3f;
            }

            if (body.TryGetComponent(out IScannerFaunaScientificContact _))
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
                Hecton8.Core.H8Debug.LogError("[GlobalPhysicsStateManager] MaxTrackedBodies capacity exceeded. Increase MaxTrackedBodies; runtime buffer growth is forbidden.");
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
            CompletePhysicsCullingJobForStateMutationBarrier(discardResults: true);
            ClearPhysicsImpactEventQueue1337();

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
                if (bodyState.AddedMassTensorApplied != 0)
                    RestoreAddedMassBaseline(body, ref bodyState);
            }

            Array.Clear(_trackedBodies, 0, _trackedBodyCount);
            Array.Clear(_bodyStates, 0, _trackedBodyCount);
            Array.Clear(_trackedMeshColliders, 0, MaxTrackedMeshColliderRefs);
            Array.Clear(_trackedMeshColliderEnabledBeforeStrip, 0, MaxTrackedMeshColliderRefs);
            Array.Clear(_trackedSleepColliders, 0, MaxTrackedSleepColliderRefs);
            Array.Clear(_trackedSleepColliderEnabledBeforeSleep, 0, MaxTrackedSleepColliderRefs);
            Array.Clear(_connections, 0, _connectionCount);
            if (_connectionLockTouchedBodyCount > 0)
                Array.Clear(_connectionLockTouchedBodyIndices, 0, math.min(_connectionLockTouchedBodyCount, _connectionLockTouchedBodyIndices.Length));
            if (_addedMassDirtyCount > 0)
                Array.Clear(_addedMassDirtyBodyIndices, 0, math.min(_addedMassDirtyCount, _addedMassDirtyBodyIndices.Length));
            _trackedBodyIndexByEntityId.Clear();
            _trackedBodyIndexByInstanceId.Clear();

            bool runtimeClearLocksAcquired = TryAcquirePhysicsRuntimeClearLocks1337(out ulong runtimeClearLockMask);
            if (!runtimeClearLocksAcquired)
            {
                _physicsCullingLockContentionsThisFrame++;
            }
            else
            {
                try
                {
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
                }
                finally
                {
                    ReleasePhysicsRuntimeClearLocks1337(runtimeClearLockMask);
                }
            }

            ClearShinobu37PhysicsCullingState();

            _trackedBodyCount = 0;
            _connectionCount = 0;
            _connectionLockTouchedBodyCount = 0;
            _queuedImpactCount = 0;
            _impactEventReadIndex = 0;
            _impactEventWriteIndex = 0;
            _culledBodyCount = 0;
            _physicsCullingJobCount = 0;
            _physicsCullingTelemetryWriteIndex = 0;
            _physicsCullingSlowTickAccumulator = 0f;
            _colliderLodHysteresisAccumulator = 0f;
            _addedMassDirtyCount = 0;
            _aupJitterSentinelCountdown = 0;
            _aupJitterSentinelCachedFrame = -1;
            _aupJitterSentinelDueThisFrame = false;
            _connectionCapacityOverflowReported = false;
            _connectionRefsRequireFullClear = true;
            _deferredNullTrackedBodyCleanup = false;
            _trackedBodyCapacityOverflowReported = false;
            _nativeStateAllocationFailureReported = false;
        }

        private void ClearPhysicsImpactEventQueue1337()
        {
            if (!_impactEvents.IsCreated)
                return;

            bool locked = _impactEvents.TryAcquireWriteLock(out _);
            if (!locked)
            {
                _physicsCullingLockContentionsThisFrame++;
                return;
            }

            try
            {
                int impactClearCount = math.min(_impactEvents.Length, MaxQueuedImpactEvents);
                for (int i = 0; i < impactClearCount; i++)
                    _impactEvents[i] = default;
            }
            finally
            {
                if (locked)
                    _impactEvents.ReleaseWriteLock();
            }
        }

        private bool TryAcquirePhysicsRuntimeClearLocks1337(out ulong acquiredLockMask)
        {
            acquiredLockMask = ResolvePhysicsRuntimeClearMutationGuardMask1337();
            if (acquiredLockMask == 0UL)
                return true;

            if (TryAcquirePhysicsMutationGuard(acquiredLockMask))
                return true;

            acquiredLockMask = 0UL;
            return false;
        }

        private void ReleasePhysicsRuntimeClearLocks1337(ulong acquiredLockMask)
        {
            ReleasePhysicsMutationGuard(acquiredLockMask);
        }

        private ulong ResolvePhysicsRuntimeClearMutationGuardMask1337()
        {
            ulong mutationGuardMask = 0UL;
            if (_lastValidPositions.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.RigidbodyLastValidPositions);
            if (_rigidbodyAUPs.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.RigidbodyAUPs);
            if (_rigidbodyCullingStateSnapshot.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.RigidbodyCullingState);
            if (_rigidbodyAwakeResults.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.RigidbodyAwakeResults);
            if (_rigidbodyCullingCommandResults.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.RigidbodyCullingCommands);
            if (_rigidbodyDistanceSqResults.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.RigidbodyDistanceSq);
            if (_physicsCullingTelemetry.IsCreated)
                mutationGuardMask |= PhysicsVaultMutationGuardBit(BufferID.PhysicsCullingTelemetry);
            return mutationGuardMask;
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
            float normalizedForce = math.max(0f, impactForce) * 0.01f;
            float primary = normalizedForce * math.rcp(2.35f + normalizedForce);
            float highTail = normalizedForce * math.rcp(16f + normalizedForce);
            return primary + (highTail * 0.65f);
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

            SceneManager.sceneLoaded -= HandleSceneLoaded;
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
            return TryNormalizeQuaternion(value, out _);
        }

        private static Quaternion SanitizeQuaternion(Quaternion value)
        {
            return TryNormalizeQuaternion(value, out Quaternion normalized)
                ? normalized
                : Quaternion.identity;
        }

        private static bool TryNormalizeQuaternion(Quaternion value, out Quaternion normalized)
        {
            float4 value4 = new float4(value.x, value.y, value.z, value.w);
            float lengthSq = math.lengthsq(value4);
            if (!math.all(math.isfinite(value4)) ||
                !math.isfinite(lengthSq) ||
                lengthSq <= QuaternionMagnitudeEpsilonSq)
            {
                normalized = Quaternion.identity;
                return false;
            }

            value4 *= math.rsqrt(math.max(lengthSq, QuaternionMagnitudeEpsilonSq));
            if (value4.w < 0f)
                value4 = -value4;

            normalized = new Quaternion(value4.x, value4.y, value4.z, value4.w);
            return true;
        }

        private static bool IsFinite(in AbsoluteUniversePosition value)
        {
            return math.isfinite(value.LocalX) &&
                math.isfinite(value.LocalY) &&
                math.isfinite(value.LocalZ);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFinite(runtimePosition))
                return false;

            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!math.all(math.isfinite(origin)))
                return false;

            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromAbsolutePosition(origin);
            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFinite(in positionAup);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeWithRsqrtGuard(float3 value, float3 fallback)
        {
            if (!math.all(math.isfinite(value)))
                return fallback;

            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return fallback;

            return value * math.rsqrt(math.max(lengthSq, 0.0001f));
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
            GlobalPhysicsStateManager.RegisterTrackedBodyIfMissing(_body);
        }

        private void OnDisable()
        {
            if (_body != null)
                GlobalPhysicsStateManager.UnregisterTrackedBody(_body);
        }

    }
}
