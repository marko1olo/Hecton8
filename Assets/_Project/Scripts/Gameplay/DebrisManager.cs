using Hecton8.Core;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using BurstRandom = Unity.Mathematics.Random;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Runtime GPU debris owner for pre-baked organic fracture chunks.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8920)]
    public sealed class DebrisManager : MonoBehaviour, IUpdatable, ILateFrameTickable, IDebrisService, IOriginShiftListener
    {
        private const int MaxActiveChunks = 192;
        private const int MaxPendingBursts = 24;
        private const int MatrixStrideBytes = sizeof(float) * 16;
        private const float PhysicsPhaseDuration = 3f;
        private const float SinkPhaseDuration = 2f;
        private const float PoolReturnDelay = PhysicsPhaseDuration + SinkPhaseDuration;
        private const float SinkDepthMeters = 0.25f;
        private const float UnderwaterGravity = 2.9f;
        private const float NoiseStrength = 0.42f;
        private const float MinimumPower = 0.05f;
        private const float WorldCullY = -5000f;
        private const float MaximumChunkLifetime = 60f;
        private const int DebrisPoolTelemetryId = unchecked((int)0x00DEB815u);
        private const uint PendingBurstQueueFullReason = 0x44504251u;
        private const uint ActiveSlotPoolExhaustedReason = 0x4450534Cu;

        private static DebrisManager _instance;

        // COLD ALLOC: Mesh[192] - active chunk mesh slots - owner: DebrisManager
        private readonly Mesh[] _slotMeshes = new Mesh[MaxActiveChunks];
        // COLD ALLOC: Material[192] - active chunk material slots - owner: DebrisManager
        private readonly Material[] _slotMaterials = new Material[MaxActiveChunks];
        // COLD ALLOC: ShadowCastingMode[192] - active chunk shadow-state slots - owner: DebrisManager
        private readonly ShadowCastingMode[] _slotShadowModes = new ShadowCastingMode[MaxActiveChunks];
        // COLD ALLOC: bool[192] - active chunk receive-shadow slots - owner: DebrisManager
        private readonly bool[] _slotReceiveShadows = new bool[MaxActiveChunks];
        // COLD ALLOC: uint[192] - active chunk rendering-layer slots - owner: DebrisManager
        private readonly uint[] _slotLayerMasks = new uint[MaxActiveChunks];
        // COLD ALLOC: Mesh[192] - per-frame mesh batch keys - owner: DebrisManager
        private readonly Mesh[] _batchMeshes = new Mesh[MaxActiveChunks];
        // COLD ALLOC: Material[192] - per-frame material batch keys - owner: DebrisManager
        private readonly Material[] _batchMaterials = new Material[MaxActiveChunks];
        // COLD ALLOC: ShadowCastingMode[192] - per-frame shadow batch keys - owner: DebrisManager
        private readonly ShadowCastingMode[] _batchShadowModes = new ShadowCastingMode[MaxActiveChunks];
        // COLD ALLOC: bool[192] - per-frame receive-shadow batch keys - owner: DebrisManager
        private readonly bool[] _batchReceiveShadows = new bool[MaxActiveChunks];
        // COLD ALLOC: uint[192] - per-frame rendering-layer batch keys - owner: DebrisManager
        private readonly uint[] _batchLayerMasks = new uint[MaxActiveChunks];
        // COLD ALLOC: int[192] - per-frame batch counts - owner: DebrisManager
        private readonly int[] _batchCounts = new int[MaxActiveChunks];
        // COLD ALLOC: int[36864] - flat batch-to-slot lookup table - owner: DebrisManager
        private readonly int[] _batchSlotIndices = new int[MaxActiveChunks * MaxActiveChunks];
        // COLD ALLOC: Matrix4x4[192] - per-batch GPU matrix upload staging - owner: DebrisManager
        private readonly Matrix4x4[] _batchMatrices = new Matrix4x4[MaxActiveChunks];
        // COLD ALLOC: DebrisInstanceData[192] - per-batch instanced draw staging - owner: DebrisManager
        private readonly DebrisInstanceData[] _batchInstanceData = new DebrisInstanceData[MaxActiveChunks];
        // COLD ALLOC: PendingBurstRequest[24] - deferred burst queue for post-job insertion - owner: DebrisManager
        private readonly PendingBurstRequest[] _pendingBursts = new PendingBurstRequest[MaxPendingBursts];

        private NativeArray<DebrisChunkState> _frontStates;
        private NativeArray<DebrisChunkState> _backStates;
        private JobHandle _simulationHandle;
        private GraphicsBuffer _matrixBuffer;
        private Vector3 _pendingShiftOffset;
        private int _pendingBurstCount;
        private bool _simulationScheduled;
        private bool _dispatcherRegistered;
        private bool _lateFrameRegistered;
        private bool _originShiftRegistered;
        private bool _clearRequested;
        private bool _isInitialized;

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Ensures a live runtime debris owner exists.
        /// </summary>
        /// <returns>Runtime debris owner.</returns>
        public static DebrisManager EnsureRuntimeInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject runtimeRoot = new GameObject("[DebrisManager]");
            DebrisManager manager = runtimeRoot.AddComponent<DebrisManager>();
            return manager;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        /// <summary>
        /// Registers the service into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
            if (_isInitialized)
                return;

            GlobalRegistry.RegisterDebrisService(this);
            _isInitialized = ReferenceEquals(GlobalRegistry.Debris, this);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            if (!_frontStates.IsCreated)
            {
                // COLD ALLOC: NativeArray<DebrisChunkState>[192] - front debris simulation state buffer - owner: DebrisManager
                _frontStates = new NativeArray<DebrisChunkState>(MaxActiveChunks, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    _frontStates,
                    nameof(DebrisManager),
                    nameof(_frontStates),
                    NativeAllocationLifetime.Session);
            }

            if (!_backStates.IsCreated)
            {
                // COLD ALLOC: NativeArray<DebrisChunkState>[192] - back debris simulation state buffer - owner: DebrisManager
                _backStates = new NativeArray<DebrisChunkState>(MaxActiveChunks, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    _backStates,
                    nameof(DebrisManager),
                    nameof(_backStates),
                    NativeAllocationLifetime.Session);
            }

            if (_matrixBuffer == null)
            {
                // COLD ALLOC: GraphicsBuffer[192] - runtime chunk transform upload buffer - owner: DebrisManager
                _matrixBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MaxActiveChunks, MatrixStrideBytes);
            }

        }

        private void OnEnable()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_dispatcherRegistered)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _dispatcherRegistered = GlobalRegistry.Updatables.Contains(this);
            }

            if (!_lateFrameRegistered)
            {
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = SystemDispatcher.GetLateFrameLane(PriorityLayer.Environment).Contains(this);
            }

            if (!_originShiftRegistered)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _originShiftRegistered = HectonFloatingOrigin.IsListenerRegistered(this);
            }
        }

        private void OnDisable()
        {
            if (_dispatcherRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _dispatcherRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }

            if (_originShiftRegistered)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _originShiftRegistered = false;
            }

            _clearRequested = true;
            _pendingBurstCount = 0;
            if (!_simulationScheduled)
                ResetActiveState();
        }

        private void OnDestroy()
        {
            if (_isInitialized && ReferenceEquals(GlobalRegistry.Debris, this))
            {
                GlobalRegistry.UnregisterDebrisService(this);
                _isInitialized = false;
            }
            else
            {
                _isInitialized = false;
            }

            if (_originShiftRegistered)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _originShiftRegistered = false;
            }

            if (_dispatcherRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _dispatcherRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }

            ReleaseNativeState();
            ReleaseBuffer(ref _matrixBuffer);

            if (_instance == this)
                _instance = null;
        }

        /// <inheritdoc />
        public bool SpawnBurst(
            IDebrisDefinition definition,
            Vector3 runtimeOrigin,
            Quaternion runtimeRotation,
            Vector3 runtimeHitPoint,
            Vector3 runtimeHitNormal,
            float power01,
            uint seed)
        {
            if (definition == null || !definition.IsValid)
                return false;

            if (_pendingBurstCount >= _pendingBursts.Length)
            {
                PublishDebrisPoolExhausted(PendingBurstQueueFullReason);
                return false;
            }

            _pendingBursts[_pendingBurstCount++] = new PendingBurstRequest
            {
                Definition = definition,
                RuntimeOrigin = runtimeOrigin,
                RuntimeRotation = runtimeRotation,
                RuntimeHitPoint = runtimeHitPoint,
                RuntimeHitNormal = runtimeHitNormal,
                Power01 = math.saturate(power01),
                Seed = seed != 0u ? seed : 1u
            };
            return true;
        }

        /// <inheritdoc />
        public void ClearActiveDebris()
        {
            _clearRequested = true;
            _pendingBurstCount = 0;

            if (!_simulationScheduled)
                ResetActiveState();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (_clearRequested && !_simulationScheduled)
            {
                ResetActiveState();
                _clearRequested = false;
            }

            if (_pendingShiftOffset.sqrMagnitude > 0.000001f && !_simulationScheduled)
            {
                ApplyShiftToBuffer(_frontStates, _pendingShiftOffset);
                _pendingShiftOffset = Vector3.zero;
            }

            FlushPendingBursts();
            RenderActiveChunks();

            if (!_simulationScheduled && HasActiveChunks())
            {
                DebrisSimulationJob job = new DebrisSimulationJob
                {
                    ReadStates = _frontStates,
                    WriteStates = _backStates,
                    DeltaTime = math.max(0.0001f, deltaTime),
                    PhysicsPhaseDuration = PhysicsPhaseDuration,
                    PoolReturnDelay = PoolReturnDelay,
                    SinkDepthMeters = SinkDepthMeters,
                    Gravity = UnderwaterGravity,
                    NoiseStrength = NoiseStrength,
                    MaximumLifetime = MaximumChunkLifetime,
                    WorldCullY = WorldCullY,
                    RandomSeed = ResolveJobSeed()
                };
                _simulationHandle = job.Schedule();
                _simulationScheduled = true;
            }
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (!_simulationScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _simulationHandle, forceComplete: false))
                return;

            _simulationScheduled = false;
            SwapStateBuffers();
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            if (shiftOffset.sqrMagnitude <= 0.000001f)
                return;

            ApplyShiftToBuffer(_frontStates, shiftOffset);
            if (_simulationScheduled)
                _pendingShiftOffset += shiftOffset;
            else
                ApplyShiftToBuffer(_backStates, shiftOffset);
        }

        private void FlushPendingBursts()
        {
            if (_pendingBurstCount <= 0)
                return;

            for (int requestIndex = 0; requestIndex < _pendingBurstCount; requestIndex++)
            {
                PendingBurstRequest request = _pendingBursts[requestIndex];
                if (request.Definition == null || !request.Definition.IsValid)
                    continue;

                int requiredSlots = CountValidChunks(request.Definition);
                if (requiredSlots <= 0)
                    continue;

                if (CountFreeSlots() < requiredSlots)
                {
                    PublishDebrisPoolExhausted(ActiveSlotPoolExhaustedReason);
                    continue;
                }

                BurstRandom rng = new BurstRandom(request.Seed != 0u ? request.Seed : 1u);
                float power = math.max(MinimumPower, request.Power01);
                float3 hitNormal = math.normalizesafe(
                    new float3(request.RuntimeHitNormal.x, request.RuntimeHitNormal.y, request.RuntimeHitNormal.z),
                    new float3(0f, 1f, 0f));

                for (int chunkIndex = 0; chunkIndex < request.Definition.ChunkCount; chunkIndex++)
                {
                    Mesh mesh = request.Definition.GetChunkMesh(chunkIndex);
                    if (mesh == null)
                        continue;

                    int slotIndex = FindFreeSlot();
                    if (slotIndex < 0)
                    {
                        PublishDebrisPoolExhausted(ActiveSlotPoolExhaustedReason);
                        break;
                    }

                    Matrix4x4 worldMatrix = Matrix4x4.TRS(request.RuntimeOrigin, request.RuntimeRotation, Vector3.one) *
                                            request.Definition.GetLocalMatrix(chunkIndex);
                    Vector3 runtimePosition = worldMatrix.GetColumn(3);
                    float3 direction = math.normalizesafe(
                        new float3(
                            runtimePosition.x - request.RuntimeHitPoint.x,
                            runtimePosition.y - request.RuntimeHitPoint.y,
                            runtimePosition.z - request.RuntimeHitPoint.z) +
                        hitNormal * 0.45f +
                        rng.NextFloat3Direction() * 0.22f,
                        hitNormal);
                    float massScale = math.max(0.2f, request.Definition.GetMassScale(chunkIndex));
                    float impulse = request.Definition.BaseImpulse *
                                    (0.45f + power) *
                                    math.lerp(0.85f, 1.25f, rng.NextFloat()) /
                                    massScale;
                    float3 velocity = direction * impulse;
                    velocity.y += 0.35f + power * 0.8f;
                    float3 angularVelocity = rng.NextFloat3Direction() * (0.95f + power * 4.5f) / massScale;

                    DebrisChunkState state = new DebrisChunkState
                    {
                        Position = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                        Rotation = ToQuaternion(worldMatrix.rotation),
                        Scale = ToFloat3(ExtractScale(worldMatrix)),
                        Velocity = velocity,
                        AngularVelocity = angularVelocity,
                        Age = 0f,
                        GroundY = request.RuntimeOrigin.y - request.Definition.GroundPlaneOffset,
                        SinkStartY = runtimePosition.y,
                        SinkTargetY = runtimePosition.y - SinkDepthMeters,
                        SinkDuration = SinkPhaseDuration,
                        LinearDamping = math.max(0.05f, request.Definition.LinearDamping),
                        AngularDamping = math.max(0.05f, request.Definition.AngularDamping),
                        BounceDamping = math.clamp(request.Definition.BounceDamping, 0f, 1f),
                        MassScale = massScale,
                        Active = 1,
                        CollisionEnabled = 1,
                        Kinematic = 0
                    };

                    _frontStates[slotIndex] = state;
                    if (!_simulationScheduled)
                        _backStates[slotIndex] = state;

                    _slotMeshes[slotIndex] = mesh;
                    _slotMaterials[slotIndex] = request.Definition.SharedMaterial;
                    _slotShadowModes[slotIndex] = request.Definition.ShadowCastingMode;
                    _slotReceiveShadows[slotIndex] = request.Definition.ReceiveShadows;
                    _slotLayerMasks[slotIndex] = request.Definition.RenderingLayerMask;
                }
            }

            _pendingBurstCount = 0;
        }

        private void RenderActiveChunks()
        {
            if (_matrixBuffer == null || !_frontStates.IsCreated)
                return;

            int batchCount = 0;
            System.Array.Clear(_batchCounts, 0, _batchCounts.Length);

            for (int slotIndex = 0; slotIndex < _frontStates.Length; slotIndex++)
            {
                DebrisChunkState state = _frontStates[slotIndex];
                if (state.Active == 0)
                    continue;

                Mesh mesh = _slotMeshes[slotIndex];
                Material material = _slotMaterials[slotIndex];
                if (mesh == null || material == null)
                    continue;

                int batchIndex = FindOrCreateBatch(
                    mesh,
                    material,
                    _slotShadowModes[slotIndex],
                    _slotReceiveShadows[slotIndex],
                    _slotLayerMasks[slotIndex],
                    ref batchCount);
                if (batchIndex < 0)
                    continue;

                int localIndex = _batchCounts[batchIndex];
                if (localIndex >= MaxActiveChunks)
                    continue;

                _batchSlotIndices[(batchIndex * MaxActiveChunks) + localIndex] = slotIndex;
                _batchCounts[batchIndex] = localIndex + 1;
            }

            for (int batchIndex = 0; batchIndex < batchCount; batchIndex++)
            {
                int instanceCount = _batchCounts[batchIndex];
                if (instanceCount <= 0)
                    continue;

                Bounds worldBounds = default;
                bool boundsInitialized = false;
                for (int localIndex = 0; localIndex < instanceCount; localIndex++)
                {
                    int slotIndex = _batchSlotIndices[(batchIndex * MaxActiveChunks) + localIndex];
                    DebrisChunkState state = _frontStates[slotIndex];
                    Matrix4x4 matrix = Matrix4x4.TRS(
                        new Vector3(state.Position.x, state.Position.y, state.Position.z),
                        ToUnityQuaternion(state.Rotation),
                        new Vector3(state.Scale.x, state.Scale.y, state.Scale.z));

                    _batchMatrices[localIndex] = matrix;
                    _batchInstanceData[localIndex] = new DebrisInstanceData
                    {
                        objectToWorld = matrix,
                        renderingLayerMask = _slotLayerMasks[slotIndex]
                    };

                    Vector3 extents = Vector3.Max(new Vector3(0.25f, 0.25f, 0.25f), new Vector3(state.Scale.x, state.Scale.y, state.Scale.z));
                    if (!boundsInitialized)
                    {
                        worldBounds = new Bounds(matrix.GetColumn(3), extents * 2f);
                        boundsInitialized = true;
                    }
                    else
                    {
                        worldBounds.Encapsulate(new Bounds(matrix.GetColumn(3), extents * 2f));
                    }
                }

                _matrixBuffer.SetData(_batchMatrices, 0, 0, instanceCount);

                RenderParams renderParams = new RenderParams(_batchMaterials[batchIndex])
                {
                    worldBounds = worldBounds,
                    shadowCastingMode = _batchShadowModes[batchIndex],
                    receiveShadows = _batchReceiveShadows[batchIndex],
                    renderingLayerMask = _batchLayerMasks[batchIndex]
                };
                Graphics.RenderMeshInstanced(
                    renderParams,
                    _batchMeshes[batchIndex],
                    0,
                    _batchInstanceData,
                    instanceCount,
                    0);
            }
        }

        private bool HasActiveChunks()
        {
            for (int i = 0; i < _frontStates.Length; i++)
            {
                if (_frontStates[i].Active != 0)
                    return true;
            }

            return false;
        }

        private int FindOrCreateBatch(
            Mesh mesh,
            Material material,
            ShadowCastingMode shadowMode,
            bool receiveShadows,
            uint layerMask,
            ref int batchCount)
        {
            for (int i = 0; i < batchCount; i++)
            {
                if (!ReferenceEquals(_batchMeshes[i], mesh) ||
                    !ReferenceEquals(_batchMaterials[i], material) ||
                    _batchShadowModes[i] != shadowMode ||
                    _batchReceiveShadows[i] != receiveShadows ||
                    _batchLayerMasks[i] != layerMask)
                {
                    continue;
                }

                return i;
            }

            if (batchCount >= MaxActiveChunks)
                return -1;

            int newIndex = batchCount++;
            _batchMeshes[newIndex] = mesh;
            _batchMaterials[newIndex] = material;
            _batchShadowModes[newIndex] = shadowMode;
            _batchReceiveShadows[newIndex] = receiveShadows;
            _batchLayerMasks[newIndex] = layerMask;
            return newIndex;
        }

        private int CountValidChunks(IDebrisDefinition definition)
        {
            int count = 0;
            for (int i = 0; i < definition.ChunkCount; i++)
            {
                if (definition.GetChunkMesh(i) != null)
                    count++;
            }

            return count;
        }

        private int CountFreeSlots()
        {
            int freeCount = 0;
            for (int i = 0; i < _frontStates.Length; i++)
            {
                if (_frontStates[i].Active == 0)
                    freeCount++;
            }

            return freeCount;
        }

        private int FindFreeSlot()
        {
            for (int i = 0; i < _frontStates.Length; i++)
            {
                if (_frontStates[i].Active == 0)
                    return i;
            }

            return -1;
        }

        private static void PublishDebrisPoolExhausted(uint reasonCode)
        {
            GlobalTelemetryBus.PublishPoolExhausted(DebrisPoolTelemetryId, reasonCode);
        }

        private void ResetActiveState()
        {
            if (_frontStates.IsCreated)
            {
                for (int i = 0; i < _frontStates.Length; i++)
                    _frontStates[i] = default;
            }

            if (_backStates.IsCreated)
            {
                for (int i = 0; i < _backStates.Length; i++)
                    _backStates[i] = default;
            }

            System.Array.Clear(_slotMeshes, 0, _slotMeshes.Length);
            System.Array.Clear(_slotMaterials, 0, _slotMaterials.Length);
            System.Array.Clear(_slotShadowModes, 0, _slotShadowModes.Length);
            System.Array.Clear(_slotReceiveShadows, 0, _slotReceiveShadows.Length);
            System.Array.Clear(_slotLayerMasks, 0, _slotLayerMasks.Length);
        }

        private void SwapStateBuffers()
        {
            NativeArray<DebrisChunkState> oldFront = _frontStates;
            _frontStates = _backStates;
            _backStates = oldFront;
        }

        private void ApplyShiftToBuffer(NativeArray<DebrisChunkState> buffer, Vector3 shiftOffset)
        {
            if (!buffer.IsCreated)
                return;

            float3 offset = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
            for (int i = 0; i < buffer.Length; i++)
            {
                DebrisChunkState state = buffer[i];
                if (state.Active == 0)
                    continue;

                state.Position -= offset;
                buffer[i] = state;
            }
        }

        private uint ResolveJobSeed()
        {
            uint seed = (uint)(Time.frameCount + 1);
            return seed == 0u ? 1u : seed;
        }

        private void ReleaseNativeState()
        {
            if (_frontStates.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_frontStates);
                if (_simulationScheduled)
                    _frontStates.Dispose(_simulationHandle);
                else
                    _frontStates.Dispose();

                _frontStates = default;
            }

            if (_backStates.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_backStates);
                if (_simulationScheduled)
                    _backStates.Dispose(_simulationHandle);
                else
                    _backStates.Dispose();

                _backStates = default;
            }

            _simulationHandle = default;
            _simulationScheduled = false;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static Vector3 ExtractScale(Matrix4x4 matrix)
        {
            Vector3 xAxis = matrix.GetColumn(0);
            Vector3 yAxis = matrix.GetColumn(1);
            Vector3 zAxis = matrix.GetColumn(2);
            return new Vector3(xAxis.magnitude, yAxis.magnitude, zAxis.magnitude);
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private static quaternion ToQuaternion(Quaternion value)
        {
            return new quaternion(value.x, value.y, value.z, value.w);
        }

        private static Quaternion ToUnityQuaternion(quaternion value)
        {
            return new Quaternion(value.value.x, value.value.y, value.value.z, value.value.w);
        }

        private struct PendingBurstRequest
        {
            public IDebrisDefinition Definition;
            public Vector3 RuntimeOrigin;
            public Quaternion RuntimeRotation;
            public Vector3 RuntimeHitPoint;
            public Vector3 RuntimeHitNormal;
            public float Power01;
            public uint Seed;
        }

        private struct DebrisInstanceData
        {
            public Matrix4x4 objectToWorld;
            public uint renderingLayerMask;
        }

        private struct DebrisChunkState
        {
            public float3 Position;
            public quaternion Rotation;
            public float3 Scale;
            public float3 Velocity;
            public float3 AngularVelocity;
            public float Age;
            public float GroundY;
            public float SinkStartY;
            public float SinkTargetY;
            public float SinkDuration;
            public float LinearDamping;
            public float AngularDamping;
            public float BounceDamping;
            public float MassScale;
            public byte Active;
            public byte CollisionEnabled;
            public byte Kinematic;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct DebrisSimulationJob : IJob
        {
            [ReadOnly] public NativeArray<DebrisChunkState> ReadStates;
            public NativeArray<DebrisChunkState> WriteStates;
            public float DeltaTime;
            public float PhysicsPhaseDuration;
            public float PoolReturnDelay;
            public float SinkDepthMeters;
            public float Gravity;
            public float NoiseStrength;
            public float MaximumLifetime;
            public float WorldCullY;
            public uint RandomSeed;

            public void Execute()
            {
                BurstRandom rng = new BurstRandom(RandomSeed != 0u ? RandomSeed : 1u);
                float dt = math.max(0.0001f, DeltaTime);

                for (int i = 0; i < ReadStates.Length; i++)
                {
                    DebrisChunkState state = ReadStates[i];
                    if (state.Active == 0)
                    {
                        WriteStates[i] = state;
                        continue;
                    }

                    state.Age += dt;
                    if (state.Age > MaximumLifetime || state.Position.y < WorldCullY)
                    {
                        WriteStates[i] = default;
                        continue;
                    }

                    float inverseMass = 1f / math.max(0.2f, state.MassScale);
                    float3 randomDrift = rng.NextFloat3Direction() * (NoiseStrength * dt * inverseMass);

                    if (state.CollisionEnabled != 0)
                    {
                        state.Velocity += new float3(0f, -Gravity, 0f) * dt;
                        state.Velocity += randomDrift;
                        state.Velocity *= math.saturate(1f - (state.LinearDamping * dt));
                        state.Position += state.Velocity * dt;

                        if (state.Position.y < state.GroundY)
                        {
                            state.Position.y = state.GroundY;
                            if (state.Velocity.y < 0f)
                                state.Velocity.y = -state.Velocity.y * state.BounceDamping;

                            float lateralDamping = math.saturate(1f - (state.BounceDamping * 0.25f));
                            state.Velocity.x *= lateralDamping;
                            state.Velocity.z *= lateralDamping;
                        }

                        if (state.Age >= PhysicsPhaseDuration)
                        {
                            state.CollisionEnabled = 0;
                            state.Kinematic = 1;
                            state.SinkStartY = state.Position.y;
                            state.SinkTargetY = state.SinkStartY - SinkDepthMeters;
                            state.Velocity = float3.zero;
                            state.AngularVelocity = float3.zero;
                        }
                    }
                    else
                    {
                        float sink01 = math.saturate((state.Age - PhysicsPhaseDuration) / math.max(0.0001f, PoolReturnDelay - PhysicsPhaseDuration));
                        float sinkSmooth = sink01 * sink01 * (3f - (2f * sink01));
                        state.Position.y = math.lerp(state.SinkStartY, state.SinkTargetY, sinkSmooth);
                        if (state.Age >= PoolReturnDelay)
                            state.Active = 0;
                    }

                    if (state.Kinematic == 0)
                    {
                        state.AngularVelocity += randomDrift * 0.45f;
                        state.AngularVelocity *= math.saturate(1f - (state.AngularDamping * dt));
                        quaternion deltaRotation = quaternion.Euler(state.AngularVelocity * dt);
                        state.Rotation = math.normalize(math.mul(state.Rotation, deltaRotation));
                    }
                    else
                    {
                        state.AngularVelocity = float3.zero;
                    }

                    WriteStates[i] = state;
                }
            }
        }
    }

    /// <summary>
    /// Author-time cache of pre-baked Voronoi chunk meshes for one destructible object family.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OrganicDebrisProfile : MonoBehaviour, IDebrisDefinition
    {
        [Header("Chunk Source")]
        [SerializeField]
        [Tooltip("Root that contains the authored Voronoi chunk meshes.")]
        private Transform chunkRoot;

        [SerializeField]
        [Tooltip("Shared material used for all runtime chunk draws.")]
        private Material sharedMaterial;

        [Header("Burst Tuning")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Base outward impulse applied when the burst starts.")]
        private float baseImpulse = 3.8f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Linear damping applied while chunks still collide with the simple ground plane.")]
        private float linearDamping = 0.95f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Angular damping applied during debris simulation.")]
        private float angularDamping = 0.85f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Distance chunks sink after the collision phase ends.")]
        private float sinkDistance = 1.2f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Duration of the sink phase after collision is disabled.")]
        private float sinkDuration = 2.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Simple collision plane offset below the intact object origin.")]
        private float groundPlaneOffset = 0.1f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Bounce attenuation used by the simple collision response.")]
        private float bounceDamping = 0.28f;

        [Header("Rendering")]
        [SerializeField]
        [Tooltip("Shadow casting mode for runtime chunk draws.")]
        private ShadowCastingMode shadowCastingMode = ShadowCastingMode.On;

        [SerializeField]
        [Tooltip("True when runtime chunks should receive shadows.")]
        private bool receiveShadows = true;

        [SerializeField, Range(1, 32)]
        [Tooltip("Rendering layer mask index baked into the runtime draw data.")]
        private int renderingLayerMask = 1;

        [SerializeField]
        [Tooltip("Hide the authored chunk root during play so only GPU-driven debris is visible.")]
        private bool hideChunkRootAtRuntime = true;

        [SerializeField]
        [Tooltip("Disable authored chunk colliders at runtime to keep the debris path purely data-driven.")]
        private bool disableChunkCollidersAtRuntime = true;

        [SerializeField, HideInInspector] private Mesh[] cachedChunkMeshes;
        [SerializeField, HideInInspector] private Matrix4x4[] cachedLocalMatrices;
        [SerializeField, HideInInspector] private float[] cachedMassScales;
        [SerializeField, HideInInspector] private Collider[] cachedRuntimeColliders;

        /// <inheritdoc />
        public bool IsValid => sharedMaterial != null &&
                               cachedChunkMeshes != null &&
                               cachedLocalMatrices != null &&
                               cachedMassScales != null &&
                               cachedChunkMeshes.Length > 0 &&
                               cachedChunkMeshes.Length == cachedLocalMatrices.Length &&
                               cachedChunkMeshes.Length == cachedMassScales.Length &&
                               (!disableChunkCollidersAtRuntime || cachedRuntimeColliders != null);

        /// <inheritdoc />
        public int ChunkCount => cachedChunkMeshes != null ? cachedChunkMeshes.Length : 0;

        /// <inheritdoc />
        public Material SharedMaterial => sharedMaterial;

        /// <inheritdoc />
        public ShadowCastingMode ShadowCastingMode => shadowCastingMode;

        /// <inheritdoc />
        public bool ReceiveShadows => receiveShadows;

        /// <inheritdoc />
        public uint RenderingLayerMask => 1u << (math.clamp(renderingLayerMask, 1, 32) - 1);

        /// <inheritdoc />
        public float BaseImpulse => baseImpulse;

        /// <inheritdoc />
        public float LinearDamping => linearDamping;

        /// <inheritdoc />
        public float AngularDamping => angularDamping;

        /// <inheritdoc />
        public float SinkDistance => sinkDistance;

        /// <inheritdoc />
        public float SinkDuration => sinkDuration;

        /// <inheritdoc />
        public float GroundPlaneOffset => groundPlaneOffset;

        /// <inheritdoc />
        public float BounceDamping => bounceDamping;

        private void Awake()
        {
            if (!IsValid)
                RebuildCache();

            ApplyRuntimeAuthoringVisibility();
        }

        private void OnEnable()
        {
            ApplyRuntimeAuthoringVisibility();
        }

        /// <inheritdoc />
        public Mesh GetChunkMesh(int index)
        {
            return cachedChunkMeshes != null && (uint)index < (uint)cachedChunkMeshes.Length
                ? cachedChunkMeshes[index]
                : null;
        }

        /// <inheritdoc />
        public Matrix4x4 GetLocalMatrix(int index)
        {
            return cachedLocalMatrices != null && (uint)index < (uint)cachedLocalMatrices.Length
                ? cachedLocalMatrices[index]
                : Matrix4x4.identity;
        }

        /// <inheritdoc />
        public float GetMassScale(int index)
        {
            return cachedMassScales != null && (uint)index < (uint)cachedMassScales.Length
                ? cachedMassScales[index]
                : 1f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            RebuildCache();
        }
#endif

        private void RebuildCache()
        {
            Transform root = chunkRoot != null ? chunkRoot : transform;
            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            int validCount = 0;

            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;

                if (meshFilter.GetComponent<Renderer>() == null)
                    continue;

                validCount++;
            }

            cachedChunkMeshes = new Mesh[validCount];
            cachedLocalMatrices = new Matrix4x4[validCount];
            cachedMassScales = new float[validCount];
            cachedRuntimeColliders = root.GetComponentsInChildren<Collider>(true); // COLD ALLOC: Collider[][chunk collider count] - runtime collider disable cache - owner: OrganicDebrisProfile

            Matrix4x4 rootWorldToLocal = transform.worldToLocalMatrix;
            int writeIndex = 0;
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;

                Renderer renderer = meshFilter.GetComponent<Renderer>();
                if (renderer == null)
                    continue;

                cachedChunkMeshes[writeIndex] = meshFilter.sharedMesh;
                cachedLocalMatrices[writeIndex] = rootWorldToLocal * meshFilter.transform.localToWorldMatrix;
                cachedMassScales[writeIndex] = math.max(0.25f, meshFilter.sharedMesh.bounds.extents.magnitude);
                writeIndex++;
            }
        }

        private void ApplyRuntimeAuthoringVisibility()
        {
            if (!Application.isPlaying || chunkRoot == null)
                return;

            if (hideChunkRootAtRuntime && chunkRoot != transform)
                chunkRoot.gameObject.SetActive(false);

            if (!disableChunkCollidersAtRuntime)
                return;

            if (cachedRuntimeColliders == null)
                return;

            for (int i = 0; i < cachedRuntimeColliders.Length; i++)
            {
                Collider cachedCollider = cachedRuntimeColliders[i];
                if (cachedCollider != null)
                    cachedCollider.enabled = false;
            }
        }
    }
}
