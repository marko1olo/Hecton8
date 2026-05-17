using Hecton8.Core;
using Hecton8.Caves;
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
    public sealed class DebrisManager : MonoBehaviour, IUpdatable, ILateFrameTickable, IDebrisService, IOriginShiftListener, IServiceHeartbeat, IServiceShutdown
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
        private const float ThermalPetrificationStillSeconds = 60f;
        private const float ThermalPetrificationProbeIntervalSeconds = 0.25f;
        private const float ThermalPetrificationVelocitySq = 0.0025f;
        private const float ThermalPetrificationProbeRadius = 2.5f;
        private const float ThermalPetrificationSdfRadius = 0.75f;
        private const double DebrisSolveWarningMs = 0.2d;
        private const int DebrisPoolTelemetryId = unchecked((int)0x00DEB815u);
        private const uint PendingBurstQueueFullReason = 0x44504251u;
        private const uint ActiveSlotPoolExhaustedReason = 0x4450534Cu;
        private static readonly uint _DebrisSolveWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("DebrisManager.SolveBudgetExceeded"));
        private static readonly uint _DebrisTelemetryContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("DebrisManager"));

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
        // COLD ALLOC: float[192] - per-slot stillness timer for thermal petrification - owner: DebrisManager
        private readonly float[] _thermalPetrificationTimers = new float[MaxActiveChunks];
        // COLD ALLOC: float[192] - per-slot sparse thermal probe age gates - owner: DebrisManager
        private readonly float[] _thermalPetrificationNextProbeAges = new float[MaxActiveChunks];
        // COLD ALLOC: byte[192] - cached per-slot thermal petrification eligibility flags - owner: DebrisManager
        private readonly byte[] _thermalPetrificationHotFlags = new byte[MaxActiveChunks];

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
        private bool _debrisSolveWarningArmed;
        private float _lastTickDeltaTime;

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _isInitialized;

        /// <summary>
        /// Ensures a live runtime debris owner exists.
        /// </summary>
        /// <returns>Runtime debris owner.</returns>
        public static DebrisManager EnsureRuntimeInstance()
        {
            if (GlobalRegistry.Debris is DebrisManager registeredManager)
                return registeredManager;

            GameObject runtimeRoot = new GameObject("[DebrisManager]");
            DebrisManager manager = runtimeRoot.AddComponent<DebrisManager>();
            return manager;
        }

        /// <summary>
        /// Registers the service into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
            if (_isInitialized)
                return;

            EnsureRuntimeResources();
            GlobalRegistry.RegisterDebrisService(this);
            _isInitialized = ReferenceEquals(GlobalRegistry.Debris, this);
        }

        private void Awake()
        {
            if (GlobalRegistry.Debris is DebrisManager registeredManager && registeredManager != this)
            {
                Destroy(gameObject);
                return;
            }

            EnsureRuntimeResources();
        }

        private void OnEnable()
        {
            EnsureRuntimeResources();
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
            UnregisterRuntimeHooks();

            _clearRequested = true;
            _pendingBurstCount = 0;
            if (!_simulationScheduled)
                ResetActiveState();
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
            if (_isInitialized && ReferenceEquals(GlobalRegistry.Debris, this))
            {
                GlobalRegistry.UnregisterDebrisService(this);
                _isInitialized = false;
            }
            else
            {
                _isInitialized = false;
            }

            UnregisterRuntimeHooks();
            _clearRequested = true;
            _pendingBurstCount = 0;
            ReleaseNativeState();
            ReleaseBuffer(ref _matrixBuffer);
        }

        private void UnregisterRuntimeHooks()
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
        }

        private void EnsureRuntimeResources()
        {
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
                _matrixBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MaxActiveChunks, MatrixStrideBytes); // COLD ALLOC: GraphicsBuffer[192] - runtime chunk transform upload buffer - owner: DebrisManager
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
            return EnqueueBurst(
                definition,
                runtimeOrigin,
                runtimeRotation,
                runtimeHitPoint,
                runtimeHitNormal,
                power01,
                seed,
                0,
                0f);
        }

        /// <inheritdoc />
        public bool SpawnBurst(
            IDebrisDefinition definition,
            Vector3 runtimeOrigin,
            Quaternion runtimeRotation,
            Vector3 runtimeHitPoint,
            Vector3 runtimeHitNormal,
            float power01,
            uint seed,
            int maxChunkCount,
            float lifetimeSeconds)
        {
            return EnqueueBurst(
                definition,
                runtimeOrigin,
                runtimeRotation,
                runtimeHitPoint,
                runtimeHitNormal,
                power01,
                seed,
                maxChunkCount,
                lifetimeSeconds);
        }

        private bool EnqueueBurst(
            IDebrisDefinition definition,
            Vector3 runtimeOrigin,
            Quaternion runtimeRotation,
            Vector3 runtimeHitPoint,
            Vector3 runtimeHitNormal,
            float power01,
            uint seed,
            int maxChunkCount,
            float lifetimeSeconds)
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
                Seed = seed != 0u ? seed : 1u,
                MaxChunkCount = math.max(0, maxChunkCount),
                LifetimeSeconds = math.max(0f, lifetimeSeconds)
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
            long solveStartTimestamp = global::System.Diagnostics.Stopwatch.GetTimestamp();
            _lastTickDeltaTime = math.max(0f, deltaTime);

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

            if (!_simulationScheduled && HasSimulatedChunks())
            {
                DebrisSimulationJob job = new DebrisSimulationJob
                {
                    ReadStates = _frontStates,
                    WriteStates = _backStates,
                    DeltaTime = math.max(0.0001f, _lastTickDeltaTime),
                    PhysicsPhaseDuration = PhysicsPhaseDuration,
                    PoolReturnDelay = PoolReturnDelay,
                    SinkDepthMeters = SinkDepthMeters,
                    Gravity = UnderwaterGravity,
                    NoiseStrength = NoiseStrength,
                    MaximumLifetime = MaximumChunkLifetime,
                    WorldCullY = WorldCullY,
                    RandomSeed = ResolveJobSeed()
                };
                _simulationHandle = job.Schedule(_frontStates.Length, 32);
                _simulationScheduled = true;
            }

            PublishDebrisSolveWarningIfNeeded(solveStartTimestamp);
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

            long solveStartTimestamp = global::System.Diagnostics.Stopwatch.GetTimestamp();
            ProcessThermalPetrification();
            PublishDebrisSolveWarningIfNeeded(solveStartTimestamp);
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

                int validChunkCount = CountValidChunks(request.Definition);
                if (validChunkCount <= 0)
                    continue;

                int requestedSlots = request.MaxChunkCount > 0
                    ? math.min(validChunkCount, request.MaxChunkCount)
                    : validChunkCount;
                if (requestedSlots <= 0)
                    continue;

                if (CountFreeSlots() < requestedSlots)
                {
                    PublishDebrisPoolExhausted(ActiveSlotPoolExhaustedReason);
                    continue;
                }

                BurstRandom rng = new BurstRandom(request.Seed != 0u ? request.Seed : 1u);
                float power = math.max(MinimumPower, request.Power01);
                float3 hitNormal = NormalizeFastOrDefault(
                    new float3(request.RuntimeHitNormal.x, request.RuntimeHitNormal.y, request.RuntimeHitNormal.z),
                    new float3(0f, 1f, 0f));

                int spawnedChunks = 0;
                int authoredChunkCount = request.Definition.ChunkCount;
                int startChunkIndex = authoredChunkCount > 0 ? rng.NextInt(0, authoredChunkCount) : 0;
                float authoredSinkDuration = math.max(0.1f, request.Definition.SinkDuration);
                float authoredSinkDistance = math.max(0.05f, request.Definition.SinkDistance);
                float poolReturnDelay = request.LifetimeSeconds > 0f
                    ? math.max(0.5f, request.LifetimeSeconds)
                    : PhysicsPhaseDuration + authoredSinkDuration;
                float sinkDuration = math.min(authoredSinkDuration, math.max(0.1f, poolReturnDelay - 0.1f));
                float physicsPhaseDuration = math.min(PhysicsPhaseDuration, math.max(0.1f, poolReturnDelay - sinkDuration));
                for (int chunkOffset = 0; chunkOffset < authoredChunkCount && spawnedChunks < requestedSlots; chunkOffset++)
                {
                    int chunkIndex = (startChunkIndex + chunkOffset) % authoredChunkCount;
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
                    float3 direction = NormalizeFastOrDefault(
                        new float3(
                            runtimePosition.x - request.RuntimeHitPoint.x,
                            runtimePosition.y - request.RuntimeHitPoint.y,
                            runtimePosition.z - request.RuntimeHitPoint.z) +
                        hitNormal * 0.45f +
                        NextCheapSignedVector(ref rng) * 0.22f,
                        hitNormal);
                    float massScale = math.max(0.2f, request.Definition.GetMassScale(chunkIndex));
                    float impulse = request.Definition.BaseImpulse *
                                    (0.45f + power) *
                                    math.lerp(0.85f, 1.25f, rng.NextFloat()) /
                                    massScale;
                    float3 velocity = direction * impulse;
                    velocity.y += 0.35f + power * 0.8f;
                    float3 angularVelocity = NextCheapSignedVector(ref rng) * (0.95f + power * 4.5f) / massScale;

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
                        SinkTargetY = runtimePosition.y - authoredSinkDistance,
                        SinkDuration = sinkDuration,
                        SinkDistance = authoredSinkDistance,
                        LinearDamping = math.max(0.05f, request.Definition.LinearDamping),
                        AngularDamping = math.max(0.05f, request.Definition.AngularDamping),
                        BounceDamping = math.clamp(request.Definition.BounceDamping, 0f, 1f),
                        MassScale = massScale,
                        PhysicsPhaseDuration = physicsPhaseDuration,
                        PoolReturnDelay = poolReturnDelay,
                        Active = 1,
                        CollisionEnabled = 1,
                        Kinematic = 0,
                        SettledStatic = 0
                    };

                    _frontStates[slotIndex] = state;
                    if (!_simulationScheduled)
                        _backStates[slotIndex] = state;

                    _slotMeshes[slotIndex] = mesh;
                    _slotMaterials[slotIndex] = request.Definition.SharedMaterial;
                    _slotShadowModes[slotIndex] = request.Definition.ShadowCastingMode;
                    _slotReceiveShadows[slotIndex] = request.Definition.ReceiveShadows;
                    _slotLayerMasks[slotIndex] = request.Definition.RenderingLayerMask;
                    spawnedChunks++;
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
                UnityEngine.Graphics.RenderMeshInstanced(
                    renderParams,
                    _batchMeshes[batchIndex],
                    0,
                    _batchInstanceData,
                    instanceCount,
                    0);
            }
        }

        private bool HasSimulatedChunks()
        {
            for (int i = 0; i < _frontStates.Length; i++)
            {
                DebrisChunkState state = _frontStates[i];
                if (state.Active != 0 && state.SettledStatic == 0)
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
                DebrisChunkState state = _frontStates[i];
                if (state.Active == 0 || state.SettledStatic != 0)
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

            int bestSettledSlot = -1;
            float bestSettledAge = -1f;
            for (int i = 0; i < _frontStates.Length; i++)
            {
                DebrisChunkState state = _frontStates[i];
                if (state.SettledStatic == 0 || state.Age <= bestSettledAge)
                    continue;

                bestSettledAge = state.Age;
                bestSettledSlot = i;
            }

            return bestSettledSlot;
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
            System.Array.Clear(_thermalPetrificationTimers, 0, _thermalPetrificationTimers.Length);
            System.Array.Clear(_thermalPetrificationNextProbeAges, 0, _thermalPetrificationNextProbeAges.Length);
            System.Array.Clear(_thermalPetrificationHotFlags, 0, _thermalPetrificationHotFlags.Length);
        }

        private void ProcessThermalPetrification()
        {
            if (!_frontStates.IsCreated)
                return;

            AbyssalThermalManager thermalManager = GlobalRegistry.Thermodynamics;
            if (thermalManager == null)
                return;

            float deltaTime = _lastTickDeltaTime;
            for (int slotIndex = 0; slotIndex < _frontStates.Length; slotIndex++)
            {
                DebrisChunkState state = _frontStates[slotIndex];
                if (state.Active == 0 || state.SettledStatic != 0)
                {
                    _thermalPetrificationTimers[slotIndex] = 0f;
                    _thermalPetrificationNextProbeAges[slotIndex] = 0f;
                    _thermalPetrificationHotFlags[slotIndex] = 0;
                    continue;
                }

                Vector3 runtimePosition = new Vector3(state.Position.x, state.Position.y, state.Position.z);
                if (math.lengthsq(state.Velocity) > ThermalPetrificationVelocitySq)
                {
                    _thermalPetrificationTimers[slotIndex] = 0f;
                    _thermalPetrificationNextProbeAges[slotIndex] = 0f;
                    _thermalPetrificationHotFlags[slotIndex] = 0;
                    continue;
                }

                if (state.Age >= _thermalPetrificationNextProbeAges[slotIndex])
                {
                    _thermalPetrificationNextProbeAges[slotIndex] = state.Age + ThermalPetrificationProbeIntervalSeconds;
                    bool hasThermalFlow = thermalManager.SampleThermalFlow(
                        runtimePosition,
                        ThermalPetrificationProbeRadius,
                        out AbyssalThermalManager.ThermalFlowSample sample) &&
                        sample.Heat01 > 0.1f;
                    _thermalPetrificationHotFlags[slotIndex] = hasThermalFlow ? (byte)1 : (byte)0;
                }

                if (_thermalPetrificationHotFlags[slotIndex] == 0)
                {
                    _thermalPetrificationTimers[slotIndex] = 0f;
                    continue;
                }

                _thermalPetrificationTimers[slotIndex] += deltaTime;
                if (_thermalPetrificationTimers[slotIndex] < ThermalPetrificationStillSeconds)
                {
                    state.Age = math.min(state.Age, PoolReturnDelay - 0.05f);
                    _frontStates[slotIndex] = state;
                    continue;
                }

                double3 absolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtimePosition);
                if (HectonVoxelVolume.TryDepositAdditiveSdfSphere(
                        absolutePosition,
                        ThermalPetrificationSdfRadius * math.max(0.5f, state.MassScale),
                        ThermalPetrificationSdfRadius))
                {
                    state.CollisionEnabled = 0;
                    state.Kinematic = 1;
                    state.SettledStatic = 1;
                    state.Velocity = float3.zero;
                    state.AngularVelocity = float3.zero;
                    state.Position.y = math.min(state.Position.y, state.SinkTargetY);
                    _frontStates[slotIndex] = state;
                    _thermalPetrificationTimers[slotIndex] = 0f;
                    _thermalPetrificationNextProbeAges[slotIndex] = 0f;
                    _thermalPetrificationHotFlags[slotIndex] = 0;
                }
            }
        }

        private void PublishDebrisSolveWarningIfNeeded(long startTimestamp)
        {
            long elapsedTicks = global::System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
            double elapsedMs = elapsedTicks * 1000d / global::System.Diagnostics.Stopwatch.Frequency;
            if (elapsedMs <= DebrisSolveWarningMs)
            {
                _debrisSolveWarningArmed = false;
                return;
            }

            if (_debrisSolveWarningArmed)
                return;

            _debrisSolveWarningArmed = true;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _DebrisSolveWarningHash,
                _DebrisTelemetryContextHash,
                (float)elapsedMs);
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
            return new Vector3(
                EstimateMagnitudeNoSqrt(xAxis.sqrMagnitude),
                EstimateMagnitudeNoSqrt(yAxis.sqrMagnitude),
                EstimateMagnitudeNoSqrt(zAxis.sqrMagnitude));
        }

        internal static float EstimateMagnitudeNoSqrt(float valueSq)
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
            estimate = RefineMagnitudeEstimate(valueSq, estimate);
            estimate = RefineMagnitudeEstimate(valueSq, estimate);
            estimate = RefineMagnitudeEstimate(valueSq, estimate);
            return estimate;
        }

        private static float3 NormalizeFastOrDefault(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > 0.000001f ? value * math.rsqrt(lengthSq) : fallback;
        }

        private static float3 NextCheapSignedVector(ref BurstRandom rng)
        {
            return new float3(
                (rng.NextFloat() * 2f) - 1f,
                (rng.NextFloat() * 2f) - 1f,
                (rng.NextFloat() * 2f) - 1f);
        }

        private static float RefineMagnitudeEstimate(float valueSq, float estimate)
        {
            return 0.5f * (estimate + (valueSq * math.rcp(math.max(estimate, 0.000001f))));
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
            public int MaxChunkCount;
            public float LifetimeSeconds;
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
            public float SinkDistance;
            public float LinearDamping;
            public float AngularDamping;
            public float BounceDamping;
            public float MassScale;
            public float PhysicsPhaseDuration;
            public float PoolReturnDelay;
            public byte Active;
            public byte CollisionEnabled;
            public byte Kinematic;
            public byte SettledStatic;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct DebrisSimulationJob : IJobParallelFor
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

            public void Execute(int i)
            {
                float dt = math.max(0.0001f, DeltaTime);

                DebrisChunkState state = ReadStates[i];
                if (state.Active == 0)
                {
                    WriteStates[i] = state;
                    return;
                }

                if (state.SettledStatic != 0)
                {
                    state.CollisionEnabled = 0;
                    state.Kinematic = 1;
                    state.Velocity = float3.zero;
                    state.AngularVelocity = float3.zero;
                    WriteStates[i] = state;
                    return;
                }

                state.Age += dt;
                if (state.Age > MaximumLifetime || state.Position.y < WorldCullY)
                {
                    state.CollisionEnabled = 0;
                    state.Kinematic = 1;
                    state.SettledStatic = 1;
                    state.Velocity = float3.zero;
                    state.AngularVelocity = float3.zero;
                    state.Position.y = math.max(state.Position.y, math.max(state.SinkTargetY, WorldCullY));
                    WriteStates[i] = state;
                    return;
                }

                float3 randomDrift = float3.zero;
                float physicsPhaseDuration = state.PhysicsPhaseDuration > 0f
                    ? state.PhysicsPhaseDuration
                    : PhysicsPhaseDuration;
                float poolReturnDelay = state.PoolReturnDelay > 0f
                    ? state.PoolReturnDelay
                    : PoolReturnDelay;

                if (state.CollisionEnabled != 0)
                {
                    uint seed = math.hash(new uint2(RandomSeed != 0u ? RandomSeed : 1u, (uint)i + 1u));
                    BurstRandom rng = new BurstRandom(seed != 0u ? seed : 1u);
                    float inverseMass = 1f / math.max(0.2f, state.MassScale);
                    randomDrift = DebrisManager.NextCheapSignedVector(ref rng) * (NoiseStrength * dt * inverseMass);
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

                    if (state.Age >= physicsPhaseDuration)
                    {
                        state.CollisionEnabled = 0;
                        state.Kinematic = 1;
                        float sinkDistance = state.SinkDistance > 0f
                            ? state.SinkDistance
                            : SinkDepthMeters;
                        state.SinkStartY = state.Position.y;
                        state.SinkTargetY = state.SinkStartY - sinkDistance;
                        state.Velocity = float3.zero;
                        state.AngularVelocity = float3.zero;
                    }
                }
                else
                {
                    float sinkDuration = state.SinkDuration > 0f
                        ? state.SinkDuration
                        : math.max(0.0001f, poolReturnDelay - physicsPhaseDuration);
                    float sink01 = math.saturate((state.Age - physicsPhaseDuration) / math.max(0.0001f, sinkDuration));
                    float sinkSmooth = sink01 * sink01 * (3f - (2f * sink01));
                    state.Position.y = math.lerp(state.SinkStartY, state.SinkTargetY, sinkSmooth);
                    if (state.Age >= poolReturnDelay)
                    {
                        state.Position.y = state.SinkTargetY;
                        state.SettledStatic = 1;
                        state.Kinematic = 1;
                        state.Velocity = float3.zero;
                        state.AngularVelocity = float3.zero;
                    }
                }

                if (state.Kinematic == 0)
                {
                    state.AngularVelocity += randomDrift * 0.45f;
                    state.AngularVelocity *= math.saturate(1f - (state.AngularDamping * dt));
                    quaternion deltaRotation = quaternion.Euler(state.AngularVelocity * dt);
                    state.Rotation = ApproximateNormalizeRotation(math.mul(state.Rotation, deltaRotation));
                }
                else
                {
                    state.AngularVelocity = float3.zero;
                }

                WriteStates[i] = state;
            }

            private static quaternion ApproximateNormalizeRotation(quaternion value)
            {
                float4 raw = value.value;
                float lengthSq = math.dot(raw, raw);
                if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                    return quaternion.identity;

                return new quaternion(raw * math.rsqrt(lengthSq));
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
                cachedMassScales[writeIndex] = math.max(0.25f, DebrisManager.EstimateMagnitudeNoSqrt(meshFilter.sharedMesh.bounds.extents.sqrMagnitude));
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
