using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Hecton8.Inventory;
using Hecton8.Physics;
using Hecton8.Physics.Determinism;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]
    internal struct PlayerKinematicsRuntimeTelemetryEntry
    {
        public float3 Position;
        public float3 Velocity;
        public float3 IntendedMovement;
        public float DragCoefficient;
        public float WaterDensity;
        public float SolidDensity;
        public uint Frame;
        public uint Flags;
        public uint SyncFenceHash;
        public uint AuxFlags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]
    internal struct PlayerKinematicsSyncState
    {
        public float3 Position;
        public float3 Velocity;
        public quaternion Rotation;
        public uint Frame;
        public uint Flags;
        public uint StateHash;
    }

    [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct PlayerKinematicsBodyJob : IJob
    {
        public NativeArray<float3> Positions;
        public NativeArray<float3> Velocities;
        public NativeArray<float3> IntendedMovement;
        public NativeArray<float3> FlowVelocity;
        public NativeArray<float3> LastValidPositions;
        public NativeArray<PlayerKinematicsRuntimeTelemetryEntry> Telemetry;
        public NativeArray<int> TelemetryWriteIndex;
        public NativeArray<int> FaultFlags;
        public float DeltaTime;
        public float DragCoefficient;
        public float WaterDensity;
        public float EquipmentDragMultiplier;
        public float LadderSnapRadiusSq;
        public float3 LadderPoint;
        public float SolidDensity;
        public uint Frame;
        public uint RuntimeFlags;

        public void Execute()
        {
            float dt = math.max(0.0f, DeltaTime);
            float3 position = Positions[0];
            float3 velocity = Velocities[0];
            float3 intended = IntendedMovement[0];
            float drag = math.max(0.0f, DragCoefficient) * math.max(0.0f, EquipmentDragMultiplier);
            float density = math.max(0.0f, WaterDensity);

            float dragTerm = drag * density * dt;
            velocity *= math.rcp(1.0f + math.max(0.0f, dragTerm));
            velocity += FlowVelocity[0] * dt;

            if ((RuntimeFlags & PlayerKinematicsRuntime.BodyFlagLadderActive) != 0u)
            {
                float3 delta = position - LadderPoint;
                float xzSq = (delta.x * delta.x) + (delta.z * delta.z);
                if (xzSq <= LadderSnapRadiusSq)
                {
                    position.x = LadderPoint.x;
                    position.z = LadderPoint.z;
                    velocity.x = 0.0f;
                    velocity.z = 0.0f;
                }
            }

            int flags = 0;
            bool finite = math.all(math.isfinite(position)) &&
                          math.all(math.isfinite(velocity)) &&
                          math.all(math.isfinite(intended));
            if (!finite)
            {
                flags = PlayerKinematicsRuntime.FaultNaN;
                position = LastValidPositions[0];
                velocity = float3.zero;
            }
            else if ((RuntimeFlags & PlayerKinematicsRuntime.BodyFlagInSolid) != 0u)
            {
                flags = PlayerKinematicsRuntime.FaultSolidTeleport;
                position = LastValidPositions[0];
                velocity = float3.zero;
            }
            else
            {
                position = SnapMillimeter(position);
                velocity = SnapMillimeter(velocity);
                LastValidPositions[0] = position;
            }

            Positions[0] = position;
            Velocities[0] = velocity;
            FaultFlags[0] = flags;

            int writeIndex = TelemetryWriteIndex[0];
            int telemetryLength = math.max(1, Telemetry.Length);
            int wrappedIndex = writeIndex % telemetryLength;
            Telemetry[wrappedIndex] = new PlayerKinematicsRuntimeTelemetryEntry
            {
                Position = position,
                Velocity = velocity,
                IntendedMovement = intended,
                DragCoefficient = drag,
                WaterDensity = density,
                SolidDensity = SolidDensity,
                Frame = Frame,
                Flags = (uint)flags,
                SyncFenceHash = 0u,
                AuxFlags = RuntimeFlags
            };
            TelemetryWriteIndex[0] = (writeIndex + 1) % telemetryLength;
        }

        private static float3 SnapMillimeter(float3 value)
        {
            return new float3(
                DeterministicPhysicsMath.SnapMillimeter(value.x),
                DeterministicPhysicsMath.SnapMillimeter(value.y),
                DeterministicPhysicsMath.SnapMillimeter(value.z));
        }
    }

    [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct PlayerKinematicsHandPlacementJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<RaycastHit> Hits;
        public NativeArray<PlayerKinematicsHandTarget> Targets;
        public float UpperArmLength;
        public float LowerArmLength;
        public float ContactOffset;
        public float MaxProbeDistance;

        public void Execute(int index)
        {
            RaycastHit hit = Hits[index];
            float3 hitPoint = ToFloat3(hit.point);
            float3 hitNormal = ToFloat3(hit.normal);
            if (!HasHit(in hit, hitPoint, hitNormal))
            {
                Targets[index] = default;
                return;
            }

            float3 normal = SafeNormalize(hitNormal, new float3(0.0f, 1.0f, 0.0f));
            float3 point = hitPoint + normal * math.max(0.0f, ContactOffset);
            if (!math.all(math.isfinite(point)))
            {
                Targets[index] = default;
                return;
            }

            float upper = math.max(0.001f, UpperArmLength);
            float lower = math.max(0.001f, LowerArmLength);
            float maxDistance = math.max(0.001f, MaxProbeDistance);
            float safeHitDistance = math.clamp(hit.distance, 0.0f, maxDistance);
            float hitDistanceSq = math.max(0.000001f, safeHitDistance * safeHitDistance);
            float distance = math.min(hitDistanceSq * math.rsqrt(hitDistanceSq), maxDistance);
            float targetDistanceSq = distance * distance;
            float denominator = math.max(0.0001f, 2.0f * upper * lower);
            float elbowCosine = math.clamp(((upper * upper) + (lower * lower) - targetDistanceSq) * math.rcp(denominator), -1.0f, 1.0f);
            float invRange = math.rcp(maxDistance);
            float blend = math.saturate(1.0f - distance * invRange);

            Targets[index] = new PlayerKinematicsHandTarget
            {
                Position = point,
                Normal = normal,
                Blend = blend,
                ElbowCosine = elbowCosine,
                Hit = 1
            };
        }

        private static bool HasHit(in RaycastHit hit, float3 point, float3 normal)
        {
            if (!math.isfinite(hit.distance) ||
                hit.distance < 0.0f ||
                !math.all(math.isfinite(point)) ||
                !math.all(math.isfinite(normal)))
            {
                return false;
            }

            return hit.distance > 0.0f || math.lengthsq(normal) > 0.0001f;
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > 0.000001f && math.all(math.isfinite(value))
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }
    }

    /// <summary>
    /// Player kinematic SOA bridge for Burst drag, equipment load, hand probes, AUP sync, and failsafe telemetry.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(HectonPlayerMovement))]
    [RequireComponent(typeof(HectonPlayerMotor))]
    [AddComponentMenu("Hecton8/Gameplay/Player/Player Kinematics Runtime")]
    public sealed class PlayerKinematicsRuntime : MonoBehaviour, IFixedTickable, IPostFixedTickable, IFastTickable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        internal const int FaultNaN = 1 << 0;
        internal const int FaultSolidTeleport = 1 << 1;
        internal const int FaultSyncFence = 1 << 2;
        internal const int FaultDesync = 1 << 3;
        internal const int FaultStateCorrection = 1 << 4;
        internal const uint BodyFlagLadderActive = 1u << 0;
        internal const uint BodyFlagInSolid = 1u << 1;
        private const uint SyncStateFlagCorrection = 1u << 24;
        private const uint SyncStateFlagApplyRotation = 1u << 25;
        private const int EntityCount = 1;
        private const int HandProbeCount = 2;
        private const int TelemetryFrameCount = 300;
        private const int SyncFenceFrameInterval = 300;
        private const int StateCorrectionDrainLimit = 8;
        private const int InputSignalDrainLimit = 8;
        private const int MaxWallContactFrameAge = 2;
        private const int MaxLadderFrameAge = 2;
        private const float ReferenceWaterDensity = 1.0f;
        private const float HeavyInventoryMassKg = 55.0f;
        private const float HeavyInventoryMaskMultiplier = 0.45f;
        private const float DragCoefficientBase = 0.18f;
        private const float DragCoefficientLoadScale = 0.35f;
        private const float AdvectionVelocityScale = 0.55f;
        private const float StaminaDrainPerSecond = 0.025f;
        private const float WallImpactRollThreshold = 4.0f;
        private const float WallImpactRollDegrees = 9.0f;
        private const float WallImpactRollDecay = 8.0f;
        private const float HandProbeDistance = 1.15f;
        private const float HandProbeSideOffset = 0.23f;
        private const float HandProbeDownOffset = 0.14f;
        private const float HandContactOffset = 0.025f;
        private const float ArmSegmentLength = 0.36f;
        private const float LadderSnapRadius = 0.52f;
        private const float SolidDensityThreshold = 0.0f;
        private const int LowTierHandProbeFrameMask = 3;
        private const float InvTwoPi = 0.15915494309f;
        private const float RollSignalEpsilonDegrees = 0.01f;
        private const string NativeMemoryOwner = nameof(PlayerKinematicsRuntime);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private static readonly int _PlayerSwimVatSpeedId = Shader.PropertyToID("_HectonSwimVatSpeedScalar");
        private static readonly int _PlayerKinematicRollId = Shader.PropertyToID("_H8PlayerKinematicRoll");

        [SerializeField] private LayerMask handProbeLayerMask = UnityEngine.Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.0f)] private float dragCoefficient = DragCoefficientBase;
        [SerializeField, Min(0.0f)] private float waterDensity = ReferenceWaterDensity;
        [SerializeField, Min(0.0f)] private float noClipSolidDensityThreshold = SolidDensityThreshold;

        private NativeArray<float3> _positions;
        private NativeArray<float3> _velocities;
        private NativeArray<float3> _intendedMovement;
        private NativeArray<float3> _flowVelocity;
        private NativeArray<float3> _lastValidPositions;
        private NativeArray<PlayerKinematicsSyncState> _stateRead;
        private NativeArray<PlayerKinematicsSyncState> _stateWrite;
        private NativeArray<PlayerKinematicsHandTarget> _handTargets;
        private NativeArray<PlayerKinematicsRuntimeTelemetryEntry> _telemetry;
        private NativeArray<int> _telemetryWriteIndex;
        private NativeArray<int> _faultFlags;
        private NativeArray<RaycastCommand> _handProbeCommands;
        private NativeArray<RaycastHit> _handProbeHits;
        private JobHandle _handProbeHandle;
        private JobHandle _handPlacementHandle;
        private bool _handProbePending;
        private bool _handPlacementPending;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredFast;
        private bool _registeredLate;
        private bool _registeredOriginShift;
        private bool _registeredHotSwap;
        private bool _dumpWrittenForFault;
        private bool _desyncDumpWritten;
        private bool _stateWriteReady;
        private Rigidbody _body;
        private HectonPlayerMovement _movement;
        private HectonPlayerMotor _motor;
        private PlayerInventory _inventory;
        private HectonSurvivalSystem _survival;
        private ContextualPhysicalIkRig _ikRig;
        private HectonFluidEngine _fluid;
        private HectonVoxelEngine _voxelEngine;
        private Transform _cachedTransform;
        private Transform _cameraTransform;
        private float _rollDegrees;
        private float _rollVelocityDegrees;
        private float _rollPhaseRadians;
        private float _lastVatSpeedScalar = -1.0f;
        private float _lastPushedRollDegrees = 99999.0f;
        private int _nextColdRebindFrame;
        private int _cadenceSalt;
        private int _fastTickCounter;
        private uint _sourceId;
        private uint _lastSyncFenceHash;
        private uint _lastSyncFenceFrame;
        private uint _lastGpuFlowFrame;
        private InputSignal _lastInputSignal;
        private Vector4 _lastGpuFlowResolution;
        private Vector4 _lastGpuFlowCenter;
        private Vector4 _lastGpuFlowSpacing;

        private void Awake()
        {
            _cachedTransform = transform;
            _body = GetComponent<Rigidbody>();
            _movement = GetComponent<HectonPlayerMovement>();
            _motor = GetComponent<HectonPlayerMotor>();
            _sourceId = unchecked((uint)EntityId.ToULong(GetEntityId()));
            _cadenceSalt = unchecked((int)_sourceId);
            TryGetComponent(out _inventory);
            TryGetComponent(out _survival);
            AllocateNativeState();
            RebindServices(allowHierarchyLookup: true);
        }

        private void OnEnable()
        {
            ResetDeterminismSessionState();
            WarmRuntimeStateOnEnable();
            RegisterRuntime();
        }

        private void OnDisable()
        {
            ClearRollSignal();
            UnregisterRuntime();
            CompleteHandProbe(true);
            CompleteHandPlacement(true);
            ClearHandTargets();
        }

        private void OnDestroy()
        {
            UnregisterRuntime();
            CompleteHandProbe(true);
            CompleteHandPlacement(true);
            ClearHandTargets();
            DisposeNativeState();
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (!_positions.IsCreated || _body == null)
                return;

            if (_cachedTransform == null)
                _cachedTransform = transform;

            RebindColdIfMissing();
            if (MovementOwnsKinematicAuthority())
            {
                TickInertiaRoll(fixedDeltaTime);
                ScheduleHandProbes();
                return;
            }

            SnapshotInputs();
            SnapshotGpuFlow();
            SnapshotVoxelSolid(out byte inSolid, out float solidDensity);
            SnapshotLadder(out byte ladderActive, out float3 ladderPoint);

            _positions[0] = ToFloat3(_body.position);
            _velocities[0] = ToFloat3(_body.linearVelocity);
            _flowVelocity[0] = ResolveCurrentAdvection(_body.position);

            var bodyJob = new PlayerKinematicsBodyJob
            {
                Positions = _positions,
                Velocities = _velocities,
                IntendedMovement = _intendedMovement,
                FlowVelocity = _flowVelocity,
                LastValidPositions = _lastValidPositions,
                Telemetry = _telemetry,
                TelemetryWriteIndex = _telemetryWriteIndex,
                FaultFlags = _faultFlags,
                DeltaTime = fixedDeltaTime,
                DragCoefficient = math.max(0.0f, dragCoefficient),
                WaterDensity = ResolveRuntimeWaterDensityScale(),
                EquipmentDragMultiplier = ResolveEquipmentDragMultiplier(),
                LadderSnapRadiusSq = LadderSnapRadius * LadderSnapRadius,
                LadderPoint = ladderPoint,
                SolidDensity = solidDensity,
                Frame = (uint)Time.frameCount,
                RuntimeFlags = ResolveBodyFlags(ladderActive, inSolid)
            };
            bodyJob.Run();

            float3 resolvedPosition3 = SnapMillimeter(_positions[0]);
            float3 resolvedVelocity3 = SnapMillimeter(_velocities[0]);
            _positions[0] = resolvedPosition3;
            _velocities[0] = resolvedVelocity3;
            Vector3 resolvedPosition = ToVector3(resolvedPosition3);
            Vector3 resolvedVelocity = ToVector3(resolvedVelocity3);
            int faultFlags = _faultFlags[0];
            StageStateWrite(resolvedPosition3, resolvedVelocity3, _body.rotation, (uint)faultFlags);
            if (faultFlags == 0)
                _dumpWrittenForFault = false;

            TickInertiaRoll(fixedDeltaTime);
            PublishMovementAcoustics(resolvedPosition, resolvedVelocity3);
            TickStamina();
            ScheduleHandProbes();
            DumpFaultTelemetryIfNeeded();
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            ApplyPendingStateCorrections();
            CommitStateWrite();
        }

        public void FastTick(float deltaTime)
        {
            _fastTickCounter++;
            if (_fastTickCounter < SyncFenceFrameInterval)
                return;

            _fastTickCounter = 0;
            PublishSyncFence();
        }

        public void LateFrameTick()
        {
            if (CompleteHandProbe(false))
                ScheduleHandPlacement();

            if (CompleteHandPlacement(false))
                ApplyHandTargets();

            if (!MovementOwnsKinematicAuthority())
                PushVatScalar();
            PushRollSignal();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!_positions.IsCreated)
                return;

            float3 offset = ToFloat3(shiftData.ShiftOffset);
            _positions[0] -= offset;
            _lastValidPositions[0] -= offset;
            if (_stateRead.IsCreated)
            {
                PlayerKinematicsSyncState state = _stateRead[0];
                state.Position -= offset;
                state = RehashState(state);
                _stateRead[0] = state;
            }

            if (_stateWrite.IsCreated)
            {
                PlayerKinematicsSyncState state = _stateWrite[0];
                state.Position -= offset;
                state = RehashState(state);
                _stateWrite[0] = state;
            }

            if (_telemetry.IsCreated)
            {
                for (int i = 0; i < _telemetry.Length; i++)
                {
                    PlayerKinematicsRuntimeTelemetryEntry entry = _telemetry[i];
                    entry.Position -= offset;
                    _telemetry[i] = entry;
                }
            }

            for (int i = 0; i < _handTargets.Length; i++)
            {
                PlayerKinematicsHandTarget target = _handTargets[i];
                if (target.Hit != 0)
                {
                    target.Position -= offset;
                    _handTargets[i] = target;
                }
            }
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.FluidRuntime ||
                serviceSlot == GlobalRegistryServiceSlot.VoxelEngineRuntime ||
                serviceSlot == GlobalRegistryServiceSlot.Player ||
                serviceSlot == GlobalRegistryServiceSlot.PlayerMotor)
            {
                RebindServices(allowHierarchyLookup: false);
            }
        }

        internal static void EnsureOnPlayerRoot(GameObject playerRoot)
        {
            if (playerRoot == null)
                return;

            if (!playerRoot.TryGetComponent(out PlayerKinematicsRuntime _))
                playerRoot.AddComponent<PlayerKinematicsRuntime>(); // COLD ALLOC: PlayerKinematicsRuntime[1] - player kinematics bridge install - owner: PlayerRuntimeContextService
        }

        private void AllocateNativeState()
        {
            if (_positions.IsCreated)
                return;

            _positions = new NativeArray<float3>(EntityCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[1] - player SOA positions - owner: PlayerKinematicsRuntime
            _velocities = new NativeArray<float3>(EntityCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[1] - player SOA velocities - owner: PlayerKinematicsRuntime
            _intendedMovement = new NativeArray<float3>(EntityCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[1] - player SOA intended movement - owner: PlayerKinematicsRuntime
            _flowVelocity = new NativeArray<float3>(EntityCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[1] - player current advection cache - owner: PlayerKinematicsRuntime
            _lastValidPositions = new NativeArray<float3>(EntityCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float3>[1] - no-clip valid-position ring head - owner: PlayerKinematicsRuntime
            _stateRead = new NativeArray<PlayerKinematicsSyncState>(EntityCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<PlayerKinematicsSyncState>[1] - KCC committed state buffer - owner: PlayerKinematicsRuntime
            _stateWrite = new NativeArray<PlayerKinematicsSyncState>(EntityCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<PlayerKinematicsSyncState>[1] - KCC post-simulation state buffer - owner: PlayerKinematicsRuntime
            _handTargets = new NativeArray<PlayerKinematicsHandTarget>(HandProbeCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<PlayerKinematicsHandTarget>[2] - procedural hand targets - owner: PlayerKinematicsRuntime
            _telemetry = new NativeArray<PlayerKinematicsRuntimeTelemetryEntry>(TelemetryFrameCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<PlayerKinematicsRuntimeTelemetryEntry>[300] - kinematic black box - owner: PlayerKinematicsRuntime
            _telemetryWriteIndex = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[1] - kinematic telemetry cursor - owner: PlayerKinematicsRuntime
            _faultFlags = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[1] - kinematic fail-fast flags - owner: PlayerKinematicsRuntime
            _handProbeCommands = new NativeArray<RaycastCommand>(HandProbeCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastCommand>[2] - batched hand IK commands - owner: PlayerKinematicsRuntime
            _handProbeHits = new NativeArray<RaycastHit>(HandProbeCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastHit>[2] - batched hand IK results - owner: PlayerKinematicsRuntime

            RegisterArray(_positions, nameof(_positions));
            RegisterArray(_velocities, nameof(_velocities));
            RegisterArray(_intendedMovement, nameof(_intendedMovement));
            RegisterArray(_flowVelocity, nameof(_flowVelocity));
            RegisterArray(_lastValidPositions, nameof(_lastValidPositions));
            RegisterArray(_stateRead, nameof(_stateRead));
            RegisterArray(_stateWrite, nameof(_stateWrite));
            RegisterArray(_handTargets, nameof(_handTargets));
            RegisterArray(_telemetry, nameof(_telemetry));
            RegisterArray(_telemetryWriteIndex, nameof(_telemetryWriteIndex));
            RegisterArray(_faultFlags, nameof(_faultFlags));
            RegisterArray(_handProbeCommands, nameof(_handProbeCommands));
            RegisterArray(_handProbeHits, nameof(_handProbeHits));

            float3 start = _body != null ? ToFloat3(_body.position) : ToFloat3(transform.position);
            start = SnapMillimeter(start);
            _positions[0] = start;
            _lastValidPositions[0] = start;
            quaternion rotation = _body != null
                ? ToQuaternion(_body.rotation)
                : ToQuaternion(transform.rotation);
            StageStateWrite(start, float3.zero, rotation, 0u);
            CommitStateWrite();
        }

        private void DisposeNativeState()
        {
            DisposeArray(ref _positions);
            DisposeArray(ref _velocities);
            DisposeArray(ref _intendedMovement);
            DisposeArray(ref _flowVelocity);
            DisposeArray(ref _lastValidPositions);
            DisposeArray(ref _stateRead);
            DisposeArray(ref _stateWrite);
            DisposeArray(ref _handTargets);
            DisposeArray(ref _telemetry);
            DisposeArray(ref _telemetryWriteIndex);
            DisposeArray(ref _faultFlags);
            DisposeArray(ref _handProbeCommands);
            DisposeArray(ref _handProbeHits);
            ResetDeterminismSessionState();
        }

        private void RegisterRuntime()
        {
            if (!_registeredFixed)
            {
                GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Player);
                _registeredFixed = true;
            }

            if (!_registeredPostFixed)
            {
                GlobalRegistry.RegisterPostFixedTickable(this, PriorityLayer.Player);
                _registeredPostFixed = true;
            }

            if (!_registeredFast)
            {
                GlobalRegistry.RegisterFastTickable(this, PriorityLayer.Player);
                _registeredFast = true;
            }

            if (!_registeredLate)
            {
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLate = true;
            }

            if (!_registeredOriginShift)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginShift = true;
            }

            if (!_registeredHotSwap)
            {
                GlobalRegistry.RegisterHotSwapListener(this);
                _registeredHotSwap = true;
            }
        }

        private void UnregisterRuntime()
        {
            if (_registeredFixed)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Player);
                _registeredFixed = false;
            }

            if (_registeredPostFixed)
            {
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Player);
                _registeredPostFixed = false;
            }

            if (_registeredFast)
            {
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Player);
                _registeredFast = false;
            }

            if (_registeredLate)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLate = false;
            }

            if (_registeredOriginShift)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShift = false;
            }

            if (_registeredHotSwap)
            {
                GlobalRegistry.UnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }
        }

        private void RebindServices(bool allowHierarchyLookup)
        {
            RebindRegistryServices();
            if (_motor == null)
                _motor = GlobalRegistry.PlayerMotor != null ? GlobalRegistry.PlayerMotor : GetComponent<HectonPlayerMotor>();
            if (_inventory == null)
                TryGetComponent(out _inventory);
            if (_survival == null)
                TryGetComponent(out _survival);
            // Cold-only child lookup; runtime hot-swap rebinds pass false.
            if (allowHierarchyLookup && _ikRig == null)
                _ikRig = GetComponentInChildren<ContextualPhysicalIkRig>(true);

        }

        private void WarmRuntimeStateOnEnable()
        {
            if (!_positions.IsCreated || !_lastValidPositions.IsCreated)
                return;

            Vector3 runtimePosition = _body != null
                ? _body.position
                : (_cachedTransform != null ? _cachedTransform.position : transform.position);
            float3 position = SnapMillimeter(ToFloat3(runtimePosition));
            if (!math.all(math.isfinite(position)))
                return;

            float3 velocity = _body != null ? ToFloat3(_body.linearVelocity) : float3.zero;
            velocity = math.all(math.isfinite(velocity)) ? SnapMillimeter(velocity) : float3.zero;
            _positions[0] = position;
            if (_velocities.IsCreated)
                _velocities[0] = velocity;
            _lastValidPositions[0] = position;
            StageStateWrite(position, velocity, _body != null ? ToQuaternion(_body.rotation) : quaternion.identity, 0u);
            CommitStateWrite();

            if (_faultFlags.IsCreated)
                _faultFlags[0] = 0;
            _dumpWrittenForFault = false;
        }

        private void ResetDeterminismSessionState()
        {
            _stateWriteReady = false;
            _fastTickCounter = 0;
            _lastSyncFenceHash = 0u;
            _lastSyncFenceFrame = 0u;
            _lastGpuFlowFrame = 0u;
            _lastInputSignal = default;
            _dumpWrittenForFault = false;
            _desyncDumpWritten = false;
            _rollPhaseRadians = 0.0f;
            _lastGpuFlowResolution = Vector4.zero;
            _lastGpuFlowCenter = Vector4.zero;
            _lastGpuFlowSpacing = Vector4.zero;
            if (_intendedMovement.IsCreated)
                _intendedMovement[0] = float3.zero;
            if (_flowVelocity.IsCreated)
                _flowVelocity[0] = float3.zero;
            if (_faultFlags.IsCreated)
                _faultFlags[0] = 0;
            if (_telemetryWriteIndex.IsCreated)
                _telemetryWriteIndex[0] = 0;
            if (_telemetry.IsCreated)
            {
                for (int i = 0; i < _telemetry.Length; i++)
                    _telemetry[i] = default;
            }
        }

        private void RebindRegistryServices()
        {
            _fluid = GlobalRegistry.Fluid;
            _voxelEngine = GlobalRegistry.VoxelEngine;
            if (_motor == null && GlobalRegistry.PlayerMotor != null)
                _motor = GlobalRegistry.PlayerMotor;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            _cameraTransform = playerContext != null && playerContext.PlayerCamera != null
                ? playerContext.PlayerCamera.transform
                : null;
        }

        private void RebindColdIfMissing()
        {
            if (_fluid != null && _voxelEngine != null && _cameraTransform != null)
                return;

            int frame = Time.frameCount;
            if (frame < _nextColdRebindFrame)
                return;

            _nextColdRebindFrame = frame + 64;
            RebindRegistryServices();
        }

        private void SnapshotInputs()
        {
            InputSignal inputSignal = _lastInputSignal;
            for (int i = 0; i < InputSignalDrainLimit; i++)
            {
                if (!PhysicsDeterminismSignals.TryDequeueInput(out InputSignal drained))
                    break;

                inputSignal = drained;
                _lastInputSignal = drained;
            }

            if (inputSignal.Sequence == 0u &&
                PhysicsDeterminismSignals.TryGetLatestInput(out InputSignal latest))
            {
                inputSignal = latest;
                _lastInputSignal = latest;
            }

            float2 planar = inputSignal.MoveDelta;
            float planarSq = math.lengthsq(planar);
            if (planarSq > 1.0f)
                planar *= math.rsqrt(planarSq);

            float3 forward = _cameraTransform != null ? ToFloat3(_cameraTransform.forward) : ToFloat3(_cachedTransform.forward);
            float3 right = _cameraTransform != null ? ToFloat3(_cameraTransform.right) : ToFloat3(_cachedTransform.right);
            forward.y = 0.0f;
            right.y = 0.0f;
            forward = SafeNormalize(forward, new float3(0.0f, 0.0f, 1.0f));
            right = SafeNormalize(right, new float3(1.0f, 0.0f, 0.0f));
            float vertical = math.clamp(inputSignal.VerticalDelta, -1.0f, 1.0f);
            _intendedMovement[0] = (right * planar.x) + (forward * planar.y) + new float3(0.0f, vertical, 0.0f);
        }

        private void SnapshotGpuFlow()
        {
            int frame = Time.frameCount;
            if (_lastGpuFlowFrame != 0u && (frame & ResolveGpuFlowProbeFrameMask()) != 0)
                return;

            if (_fluid == null ||
                !_fluid.TryGetGpuAbyssalFlowFieldBuffer(
                    out GraphicsBuffer _,
                    out Vector4 gridResolution,
                    out Vector4 flowCenter,
                    out Vector4 flowSpacing))
            {
                _lastGpuFlowFrame = 0u;
                return;
            }

            _lastGpuFlowResolution = gridResolution;
            _lastGpuFlowCenter = flowCenter;
            _lastGpuFlowSpacing = flowSpacing;
            _lastGpuFlowFrame = (uint)frame;
        }

        private void SnapshotVoxelSolid(out byte inSolid, out float density)
        {
            inSolid = 0;
            density = 0.0f;
            if (_voxelEngine == null || _body == null)
                return;

            Vector3 position = _body.position;
            if (!_voxelEngine.TryGetNearestActiveVolume(position, out HectonVoxelVolume volume) || volume == null)
                return;

            if (!IsInsidePublishedVoxelSdfBounds(volume, position))
                return;

            if (volume.TrySampleDensity(position, out density, out float density01) &&
                (density > noClipSolidDensityThreshold || density01 >= 0.5f))
            {
                inSolid = 1;
            }
        }

        private static bool IsInsidePublishedVoxelSdfBounds(HectonVoxelVolume volume, Vector3 runtimePosition)
        {
            if (volume == null ||
                !volume.TryGetPublishedSonarSdfPayload(
                    out NativeArray<byte> _,
                    out Vector3Int gridDimensions,
                    out Vector3 volumeOrigin,
                    out Vector3 voxelCellSize,
                    out float _,
                    out int _) ||
                gridDimensions.x <= 1 ||
                gridDimensions.y <= 1 ||
                gridDimensions.z <= 1)
            {
                return false;
            }

            float3 sample = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            float3 origin = new float3(volumeOrigin.x, volumeOrigin.y, volumeOrigin.z);
            float3 cellSize = new float3(
                math.max(0.0001f, math.abs(voxelCellSize.x)),
                math.max(0.0001f, math.abs(voxelCellSize.y)),
                math.max(0.0001f, math.abs(voxelCellSize.z)));

            if (!math.all(math.isfinite(sample)) ||
                !math.all(math.isfinite(origin)) ||
                !math.all(math.isfinite(cellSize)))
            {
                return false;
            }

            float3 min = origin - cellSize * 0.5f;
            float3 max = origin + cellSize * new float3(
                gridDimensions.x - 0.5f,
                gridDimensions.y - 0.5f,
                gridDimensions.z - 0.5f);

            return sample.x >= min.x && sample.x <= max.x &&
                   sample.y >= min.y && sample.y <= max.y &&
                   sample.z >= min.z && sample.z <= max.z;
        }

        private void SnapshotLadder(out byte ladderActive, out float3 ladderPoint)
        {
            ladderActive = 0;
            ladderPoint = _positions.IsCreated ? _positions[0] : float3.zero;
            if (_motor == null ||
                !_motor.TryGetRecentBatchedLadderHit(MaxLadderFrameAge, out RaycastHit ladderHit))
            {
                return;
            }

            ladderPoint = ToFloat3(ladderHit.point);
            ladderActive = 1;
        }

        private float3 ResolveCurrentAdvection(Vector3 position)
        {
            float immersion01 = ResolveRuntimeWaterImmersion01();
            if (immersion01 <= 0.001f)
                return float3.zero;

            if (_fluid == null || !_fluid.TrySampleModAbyssalFlow(position, out float3 flow))
                return float3.zero;

            if (!math.all(math.isfinite(flow)))
                return float3.zero;

            float gpuBoost = _lastGpuFlowFrame != 0u ? 1.0f : 0.65f;
            float tierScale = IsLowTier(GlobalRegistry.ScalabilityTier) ? 0.75f : 1.0f;
            return flow * (AdvectionVelocityScale * gpuBoost * tierScale * immersion01);
        }

        private float ResolveRuntimeWaterDensityScale()
        {
            return math.max(0.0f, waterDensity) * ResolveRuntimeWaterImmersion01();
        }

        private float ResolveRuntimeWaterImmersion01()
        {
            return _movement != null ? math.saturate(_movement.WaterImmersionRatio) : 1.0f;
        }

        private float ResolveEquipmentDragMultiplier()
        {
            if (_inventory == null)
                return 1.0f;

            ulong mask = _inventory.CurrentInventoryMask;
            float load01 = math.saturate(_inventory.CachedInventoryLoad01);
            float heavy = (mask != 0UL && _inventory.TotalMassKg >= HeavyInventoryMassKg) ? HeavyInventoryMaskMultiplier : 0.0f;
            return 1.0f + heavy + load01 * DragCoefficientLoadScale;
        }

        private void TickInertiaRoll(float dt)
        {
            float targetRoll = 0.0f;
            if (_motor != null &&
                _motor.TryGetRecentWallSlideContact(
                    MaxWallContactFrameAge,
                    out Vector3 normal,
                    out _,
                    out float blockedSpeed,
                    out _,
                    out float velocityReduction01,
                    out _))
            {
                float speed01 = math.saturate((blockedSpeed - WallImpactRollThreshold) * 0.2f);
                float side = math.sign(math.dot(ToFloat3(normal), SafeRight()));
                _rollPhaseRadians = DeterministicPhysicsMath.WrapSignedPi(_rollPhaseRadians + math.max(0.0f, dt) * 28.0f);
                float impactWave = IsHighScalabilityTier() ? DeterministicPhysicsMath.SinApprox(_rollPhaseRadians) : SignedTriangleWave(_rollPhaseRadians);
                targetRoll = -side *
                    WallImpactRollDegrees *
                    speed01 *
                    math.saturate(velocityReduction01 + 0.25f) *
                    impactWave;
            }

            float safeDt = math.max(0.0f, dt);
            float spring = ((targetRoll - _rollDegrees) * 64.0f) - (_rollVelocityDegrees * WallImpactRollDecay);
            _rollVelocityDegrees += spring * safeDt;
            _rollDegrees += _rollVelocityDegrees * safeDt;
            _rollDegrees = math.clamp(_rollDegrees, -WallImpactRollDegrees, WallImpactRollDegrees);
        }

        private void PublishMovementAcoustics(Vector3 position, float3 velocity)
        {
            float velocitySq = math.lengthsq(velocity);
            if (velocitySq <= 0.0025f || !math.isfinite(velocitySq))
                return;

            MovementAcousticSignal signal = default;
            signal.PositionAup = AbsoluteUniversePosition.FromRuntimePosition(position);
            signal.Volume = math.saturate(velocitySq * 0.08f);
            signal.VelocitySq = velocitySq;
            signal.SourceId = _sourceId;
            signal.LocomotionMode = ResolveLocomotionModeCode();
            signal.SurfaceMode = (byte)(_movement != null && _movement.IsPlayerSubmerged ? 1 : 0);
            signal.Flags = 0;
            GlobalSignals.Publish(in signal);
        }

        private void TickStamina()
        {
            float intendedSq = math.lengthsq(_intendedMovement[0]);
            if (_survival == null || intendedSq <= 0.0001f)
                return;

            _survival.SetMovementStaminaBurnInput(intendedSq, StaminaDrainPerSecond);
        }

        private byte ResolveLocomotionModeCode()
        {
            return _movement != null ? (byte)_movement.CurrentLocomotionMode : (byte)0;
        }

        private void ScheduleHandProbes()
        {
            if (!_handProbeCommands.IsCreated || _handProbePending || _handPlacementPending)
                return;

            if (_cachedTransform == null)
            {
                ClearHandTargets();
                return;
            }

            if (!IsHighScalabilityTier() && ((Time.frameCount + _cadenceSalt) & LowTierHandProbeFrameMask) != 0)
                return;

            Transform source = _cameraTransform != null ? _cameraTransform : _cachedTransform;
            float3 sourcePosition = ToFloat3(source.position);
            float3 sourceForward = ToFloat3(source.forward);
            float3 sourceRight = ToFloat3(source.right);
            float3 sourceUp = ToFloat3(source.up);
            if (!math.all(math.isfinite(sourcePosition)) ||
                !IsFiniteNonZero(sourceForward) ||
                !IsFiniteNonZero(sourceRight) ||
                !IsFiniteNonZero(sourceUp))
            {
                ClearHandTargets();
                return;
            }

            sourceForward = SafeNormalize(sourceForward, new float3(0.0f, 0.0f, 1.0f));
            sourceRight = SafeNormalize(sourceRight, new float3(1.0f, 0.0f, 0.0f));
            sourceUp = SafeNormalize(sourceUp, new float3(0.0f, 1.0f, 0.0f));
            float3 origin3 = sourcePosition + sourceForward * 0.18f - sourceUp * HandProbeDownOffset;
            if (!math.all(math.isfinite(origin3)))
            {
                ClearHandTargets();
                return;
            }

            Vector3 origin = ToVector3(origin3);
            Vector3 right = ToVector3(sourceRight);
            Vector3 direction = ToVector3(sourceForward);
            QueryParameters parameters = new QueryParameters
            {
                layerMask = handProbeLayerMask.value,
                hitTriggers = QueryTriggerInteraction.Ignore,
                hitBackfaces = false,
                hitMultipleFaces = false
            };

            _handProbeCommands[0] = new RaycastCommand
            {
                from = origin - right * HandProbeSideOffset,
                direction = direction,
                distance = HandProbeDistance,
                queryParameters = parameters
            };
            _handProbeCommands[1] = new RaycastCommand
            {
                from = origin + right * HandProbeSideOffset,
                direction = direction,
                distance = HandProbeDistance,
                queryParameters = parameters
            };
            _handProbeHandle = RaycastCommand.ScheduleBatch(_handProbeCommands, _handProbeHits, HandProbeCount, default);
            _handProbePending = true;
        }

        private bool CompleteHandProbe(bool forceComplete)
        {
            if (!_handProbePending)
                return false;

            if (!DispatcherJobSwap.TryComplete(ref _handProbeHandle, forceComplete))
                return false;

            _handProbePending = false;
            return true;
        }

        private void ScheduleHandPlacement()
        {
            if (_handPlacementPending || !_handProbeHits.IsCreated || !_handTargets.IsCreated)
                return;

            var placementJob = new PlayerKinematicsHandPlacementJob
            {
                Hits = _handProbeHits,
                Targets = _handTargets,
                UpperArmLength = ArmSegmentLength,
                LowerArmLength = ArmSegmentLength,
                ContactOffset = HandContactOffset,
                MaxProbeDistance = HandProbeDistance
            };
            _handPlacementHandle = placementJob.Schedule(HandProbeCount, HandProbeCount);
            _handPlacementPending = true;
        }

        private bool CompleteHandPlacement(bool forceComplete)
        {
            if (!_handPlacementPending)
                return false;

            if (!DispatcherJobSwap.TryComplete(ref _handPlacementHandle, forceComplete))
                return false;

            _handPlacementPending = false;
            return true;
        }

        private void ApplyHandTargets()
        {
            if (_ikRig == null || !_handTargets.IsCreated)
                return;

            _ikRig.ApplyExternalWallHandTargets(_handTargets[0], _handTargets[1]);
        }

        private void ClearHandTargets()
        {
            if (_handTargets.IsCreated)
            {
                _handTargets[0] = default;
                _handTargets[1] = default;
            }

            if (_ikRig == null)
                return;

            PlayerKinematicsHandTarget empty = default;
            _ikRig.ApplyExternalWallHandTargets(in empty, in empty);
        }

        private void PushVatScalar()
        {
            float speedSq = _velocities.IsCreated ? math.lengthsq(_velocities[0]) : 0.0f;
            float scalar = math.saturate(speedSq * 0.05f);
            if (math.abs(scalar - _lastVatSpeedScalar) <= 0.0025f)
                return;

            Shader.SetGlobalFloat(_PlayerSwimVatSpeedId, scalar);
            _lastVatSpeedScalar = scalar;
        }

        private void PushRollSignal()
        {
            if (math.abs(_rollDegrees - _lastPushedRollDegrees) <= RollSignalEpsilonDegrees)
                return;

            if (_movement != null)
                _movement.RequestKinematicInertiaRoll(_rollDegrees);
            Shader.SetGlobalFloat(_PlayerKinematicRollId, _rollDegrees);
            _lastPushedRollDegrees = _rollDegrees;
        }

        private void ClearRollSignal()
        {
            _rollDegrees = 0.0f;
            _rollVelocityDegrees = 0.0f;
            _lastPushedRollDegrees = 99999.0f;
            PushRollSignal();
        }

        private bool MovementOwnsKinematicAuthority()
        {
            return _movement != null && _movement.isActiveAndEnabled;
        }

        private void StageStateWrite(float3 position, float3 velocity, Quaternion rotation, uint flags)
        {
            StageStateWrite(position, velocity, ToQuaternion(rotation), flags);
        }

        private void StageStateWrite(float3 position, float3 velocity, quaternion rotation, uint flags)
        {
            if (!_stateWrite.IsCreated)
                return;

            float3 snappedPosition = SnapMillimeter(position);
            float3 snappedVelocity = SnapMillimeter(velocity);
            rotation = CanonicalizeRotation(rotation);

            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(ToVector3(snappedPosition));
            uint hash = BuildSyncFenceHash(in aup, snappedVelocity, rotation);
            _stateWrite[0] = new PlayerKinematicsSyncState
            {
                Position = snappedPosition,
                Velocity = snappedVelocity,
                Rotation = rotation,
                Frame = (uint)Time.frameCount,
                Flags = flags,
                StateHash = hash
            };
            _stateWriteReady = true;
        }

        private void CommitStateWrite()
        {
            if (!_stateWriteReady || !_stateWrite.IsCreated || !_stateRead.IsCreated)
                return;

            PlayerKinematicsSyncState state = _stateWrite[0];
            _stateRead[0] = state;
            _positions[0] = state.Position;
            _velocities[0] = state.Velocity;
            if ((state.Flags & (uint)(FaultNaN | FaultSolidTeleport)) == 0u)
                _lastValidPositions[0] = state.Position;

            Vector3 position = ToVector3(state.Position);
            Vector3 velocity = ToVector3(state.Velocity);
            if (_motor != null)
            {
                _motor.MovePosition(position);
                _motor.SetLinearVelocity(velocity);
            }
            else if (_body != null)
            {
                _body.MovePosition(position);
                _body.linearVelocity = velocity;
            }

            if (_body != null && (state.Flags & SyncStateFlagApplyRotation) != 0u)
            {
                Quaternion rotation = ToUnityQuaternion(state.Rotation);
                if (IsFinite(rotation))
                    _body.MoveRotation(rotation);
            }

            _stateWriteReady = false;
        }

        private void ApplyPendingStateCorrections()
        {
            for (int i = 0; i < StateCorrectionDrainLimit; i++)
            {
                if (!PhysicsDeterminismSignals.TryDequeueStateCorrection(out StateCorrectionSignal correction))
                    return;

                if (correction.SourceId != 0u && correction.SourceId != _sourceId)
                    continue;

                uint comparisonHash = correction.ExpectedLocalHash != 0u
                    ? correction.ExpectedLocalHash
                    : correction.AuthoritativeHash;
                uint authoritativeHash = correction.AuthoritativeHash != 0u
                    ? correction.AuthoritativeHash
                    : comparisonHash;
                uint localHash = BuildCurrentSyncFenceHash();
                if (comparisonHash != 0u &&
                    localHash != comparisonHash)
                {
                    EmitDesyncDetected(localHash, authoritativeHash, correction.Frame, correction.Flags);
                }

                float3 correctionPosition = ResolveCorrectionPosition(in correction);
                float3 correctionVelocity = ResolveCorrectionVelocity(in correction);
                quaternion correctionRotation = ResolveCorrectionRotation(in correction);
                bool hasRotationPayload =
                    (correction.Flags & PhysicsDeterminismSignals.StateCorrectionSignalFlagRotationValid) != 0;
                uint flags = SyncStateFlagCorrection | (uint)FaultStateCorrection;
                if (hasRotationPayload && IsFinite(ToUnityQuaternion(correctionRotation)))
                    flags |= SyncStateFlagApplyRotation;

                StageStateWrite(correctionPosition, correctionVelocity, correctionRotation, flags);
            }
        }

        private void PublishSyncFence()
        {
            if (_body == null)
                return;

            float3 position = _stateRead.IsCreated ? _stateRead[0].Position : SnapMillimeter(ToFloat3(_body.position));
            float3 velocity = _stateRead.IsCreated ? _stateRead[0].Velocity : SnapMillimeter(ToFloat3(_body.linearVelocity));
            quaternion rotation = _stateRead.IsCreated ? _stateRead[0].Rotation : CanonicalizeRotation(ToQuaternion(_body.rotation));
            Vector3 runtimePosition = ToVector3(position);
            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            uint hash = BuildSyncFenceHash(in aup, velocity, rotation);
            _lastSyncFenceHash = hash;
            _lastSyncFenceFrame = (uint)Time.frameCount;

            SyncFenceSignal signal = default;
            signal.PositionAup = aup;
            signal.RuntimePosition = position;
            signal.Velocity = velocity;
            signal.Rotation = rotation;
            signal.StateHash = hash;
            signal.Frame = _lastSyncFenceFrame;
            signal.SourceId = _sourceId;
            signal.Flags = 0;
            PhysicsDeterminismSignals.Publish(in signal);
            WriteSyncFenceTelemetry(in signal);
        }

        private void WriteSyncFenceTelemetry(in SyncFenceSignal signal)
        {
            if (!_telemetry.IsCreated || !_telemetryWriteIndex.IsCreated)
                return;

            int writeIndex = _telemetryWriteIndex[0];
            int telemetryLength = math.max(1, _telemetry.Length);
            int wrappedIndex = writeIndex % telemetryLength;
            _telemetry[wrappedIndex] = new PlayerKinematicsRuntimeTelemetryEntry
            {
                Position = signal.RuntimePosition,
                Velocity = signal.Velocity,
                IntendedMovement = _intendedMovement.IsCreated ? _intendedMovement[0] : float3.zero,
                DragCoefficient = math.max(0.0f, dragCoefficient),
                WaterDensity = ResolveRuntimeWaterDensityScale(),
                SolidDensity = 0.0f,
                Frame = signal.Frame,
                Flags = FaultSyncFence,
                SyncFenceHash = signal.StateHash,
                AuxFlags = HectonFloatingOrigin.CurrentShiftSequence
            };
            _telemetryWriteIndex[0] = (writeIndex + 1) % telemetryLength;
        }

        private void EmitDesyncDetected(uint localHash, uint authoritativeHash, uint frame, byte flags)
        {
            DesyncDetectedSignal signal = default;
            signal.LocalHash = localHash;
            signal.AuthoritativeHash = authoritativeHash;
            signal.Frame = frame != 0u ? frame : (uint)Time.frameCount;
            signal.SourceId = _sourceId;
            signal.LastFenceFrame = _lastSyncFenceFrame;
            signal.Flags = flags;
            PhysicsDeterminismSignals.Publish(in signal);
            if (_faultFlags.IsCreated)
                _faultFlags[0] |= FaultDesync;
            DumpFaultTelemetryIfNeeded();
        }

        private uint BuildCurrentSyncFenceHash()
        {
            if (_stateRead.IsCreated)
            {
                PlayerKinematicsSyncState state = _stateRead[0];
                AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(ToVector3(state.Position));
                return BuildSyncFenceHash(in aup, state.Velocity, state.Rotation);
            }

            if (_body == null)
                return 0u;

            AbsoluteUniversePosition bodyAup = AbsoluteUniversePosition.FromRuntimePosition(_body.position);
            return BuildSyncFenceHash(in bodyAup, ToFloat3(_body.linearVelocity), CanonicalizeRotation(ToQuaternion(_body.rotation)));
        }

        private static PlayerKinematicsSyncState RehashState(PlayerKinematicsSyncState state)
        {
            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(ToVector3(state.Position));
            state.StateHash = BuildSyncFenceHash(in aup, state.Velocity, state.Rotation);
            return state;
        }

        private static uint BuildSyncFenceHash(in AbsoluteUniversePosition aup, float3 velocity, quaternion rotation)
        {
            uint hash = DeterministicPhysicsMath.FnvOffsetBasis;
            hash = DeterministicPhysicsMath.Fnv1a(hash, aup.GridX);
            hash = DeterministicPhysicsMath.Fnv1a(hash, aup.GridY);
            hash = DeterministicPhysicsMath.Fnv1a(hash, aup.GridZ);
            hash = DeterministicPhysicsMath.Fnv1aQuantizedMillimeter(hash, aup.LocalX);
            hash = DeterministicPhysicsMath.Fnv1aQuantizedMillimeter(hash, aup.LocalY);
            hash = DeterministicPhysicsMath.Fnv1aQuantizedMillimeter(hash, aup.LocalZ);
            hash = DeterministicPhysicsMath.Fnv1aQuantizedMillimeter(hash, velocity.x);
            hash = DeterministicPhysicsMath.Fnv1aQuantizedMillimeter(hash, velocity.y);
            hash = DeterministicPhysicsMath.Fnv1aQuantizedMillimeter(hash, velocity.z);
            hash = DeterministicPhysicsMath.Fnv1aQuantizedMillimeter(hash, rotation.value.x);
            hash = DeterministicPhysicsMath.Fnv1aQuantizedMillimeter(hash, rotation.value.y);
            hash = DeterministicPhysicsMath.Fnv1aQuantizedMillimeter(hash, rotation.value.z);
            return DeterministicPhysicsMath.Fnv1aQuantizedMillimeter(hash, rotation.value.w);
        }

        private void DumpFaultTelemetryIfNeeded()
        {
            if ((_dumpWrittenForFault && _desyncDumpWritten) || !_faultFlags.IsCreated || !_telemetry.IsCreated || _faultFlags[0] == 0)
                return;

            _dumpWrittenForFault = true;
            if ((_faultFlags[0] & FaultDesync) != 0)
                _desyncDumpWritten = true;
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return;

            string logDirectory = Path.Combine(projectRoot, "Docs", "AgentLogs");
            Directory.CreateDirectory(logDirectory);
            string path = Path.Combine(logDirectory, "Dump_PHYSICS_DETERMINISM_SYNC.bin");
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(0x48503844u);
                writer.Write(_faultFlags[0]);
                writer.Write(_telemetryWriteIndex.IsCreated ? _telemetryWriteIndex[0] : 0);
                writer.Write(_lastSyncFenceHash);
                writer.Write(_lastSyncFenceFrame);
                for (int i = 0; i < _telemetry.Length; i++)
                {
                    PlayerKinematicsRuntimeTelemetryEntry entry = _telemetry[i];
                    writer.Write(entry.Position.x);
                    writer.Write(entry.Position.y);
                    writer.Write(entry.Position.z);
                    writer.Write(entry.Velocity.x);
                    writer.Write(entry.Velocity.y);
                    writer.Write(entry.Velocity.z);
                    writer.Write(entry.IntendedMovement.x);
                    writer.Write(entry.IntendedMovement.y);
                    writer.Write(entry.IntendedMovement.z);
                    writer.Write(entry.DragCoefficient);
                    writer.Write(entry.WaterDensity);
                    writer.Write(entry.SolidDensity);
                    writer.Write(entry.Frame);
                    writer.Write(entry.Flags);
                    writer.Write(entry.SyncFenceHash);
                    writer.Write(entry.AuxFlags);
                }
            }
        }

        private float3 SafeRight()
        {
            Transform source = _cameraTransform != null ? _cameraTransform : _cachedTransform;
            return source != null ? SafeNormalize(ToFloat3(source.right), new float3(1.0f, 0.0f, 0.0f)) : new float3(1.0f, 0.0f, 0.0f);
        }

        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > 0.000001f && math.all(math.isfinite(value))
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }

        private static float3 SnapMillimeter(float3 value)
        {
            return new float3(
                DeterministicPhysicsMath.SnapMillimeter(value.x),
                DeterministicPhysicsMath.SnapMillimeter(value.y),
                DeterministicPhysicsMath.SnapMillimeter(value.z));
        }

        private float3 ResolveCorrectionPosition(in StateCorrectionSignal correction)
        {
            bool runtimePositionFlagged =
                (correction.Flags & PhysicsDeterminismSignals.StateCorrectionSignalFlagRuntimePositionValid) != 0;
            if (runtimePositionFlagged && math.all(math.isfinite(correction.RuntimePosition)))
                return SnapMillimeter(correction.RuntimePosition);

            bool hasAupPayload =
                correction.PositionAup.GridX != 0L ||
                correction.PositionAup.GridY != 0L ||
                correction.PositionAup.GridZ != 0L ||
                correction.PositionAup.LocalX != 0.0f ||
                correction.PositionAup.LocalY != 0.0f ||
                correction.PositionAup.LocalZ != 0.0f;
            if (!hasAupPayload)
                return _stateRead.IsCreated ? _stateRead[0].Position : _positions.IsCreated ? _positions[0] : float3.zero;

            return SnapMillimeter(correction.PositionAup.ToRuntimeFloat3());
        }

        private float3 ResolveCorrectionVelocity(in StateCorrectionSignal correction)
        {
            if ((correction.Flags & PhysicsDeterminismSignals.StateCorrectionSignalFlagVelocityValid) != 0 &&
                math.all(math.isfinite(correction.Velocity)))
            {
                return SnapMillimeter(correction.Velocity);
            }

            return _stateRead.IsCreated ? _stateRead[0].Velocity : _velocities.IsCreated ? _velocities[0] : float3.zero;
        }

        private quaternion ResolveCorrectionRotation(in StateCorrectionSignal correction)
        {
            if ((correction.Flags & PhysicsDeterminismSignals.StateCorrectionSignalFlagRotationValid) != 0)
                return CanonicalizeRotation(correction.Rotation);

            return _body != null ? CanonicalizeRotation(ToQuaternion(_body.rotation)) : quaternion.identity;
        }

        private static bool IsFiniteNonZero(float3 value)
        {
            return math.all(math.isfinite(value)) && math.lengthsq(value) > 0.000001f;
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static quaternion ToQuaternion(Quaternion value)
        {
            return new quaternion(value.x, value.y, value.z, value.w);
        }

        private static Quaternion ToUnityQuaternion(quaternion value)
        {
            return new Quaternion(value.value.x, value.value.y, value.value.z, value.value.w);
        }

        private static quaternion CanonicalizeRotation(quaternion value)
        {
            float4 v = value.value;
            float lengthSq = math.lengthsq(v);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return quaternion.identity;

            v *= math.rsqrt(lengthSq);
            if (v.w < 0.0f)
                v = -v;
            return new quaternion(v);
        }

        private static bool IsFinite(Quaternion value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z) &&
                   math.isfinite(value.w);
        }

        private static uint ResolveBodyFlags(byte ladderActive, byte inSolid)
        {
            uint flags = 0u;
            flags |= math.select(0u, BodyFlagLadderActive, ladderActive != 0);
            flags |= math.select(0u, BodyFlagInSolid, inSolid != 0);
            return flags;
        }

        private static bool IsHighScalabilityTier()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            return tier == HectonQualityTier.High || tier == HectonQualityTier.Ultra;
        }

        private static bool IsLowTier(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.Low ||
                   tier == HectonQualityTier.Mx350 ||
                   tier == HectonQualityTier.Unknown;
        }

        private static int ResolveGpuFlowProbeFrameMask()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            if (IsLowTier(tier))
                return 3;

            return tier == HectonQualityTier.Mid ? 1 : 0;
        }

        private static float SignedTriangleWave(float radians)
        {
            float unit = math.frac(radians * InvTwoPi);
            return 1.0f - math.abs((unit * 4.0f) - 2.0f);
        }

        private static void RegisterArray<T>(NativeArray<T> array, string label) where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
        }

        private static void DisposeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }
    }
}
