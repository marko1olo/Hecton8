using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Caves;
using Hecton8.World;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
    public sealed class DebrisManager : MonoBehaviour, IUpdatable, ILateFrameTickable, IDebrisService, IOriginShiftListener, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private const int MaxActiveChunks = 192;
        private const int MaxPendingBursts = 24;
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
        private const Hecton8.Core.Memory.SystemID VaultOwnerSystemId = Hecton8.Core.Memory.SystemID.GameplayDebris;
        private const Hecton8.Core.Memory.BufferID FrontStatesBufferId = Hecton8.Core.Memory.BufferID.GameplayDebrisFrontStates;
        private const Hecton8.Core.Memory.BufferID BackStatesBufferId = Hecton8.Core.Memory.BufferID.GameplayDebrisBackStates;
        private static readonly ulong StateMutationGuardMask =
            MutationGuardBit(FrontStatesBufferId) |
            MutationGuardBit(BackStatesBufferId);
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

        private VaultGenerationHandle<DebrisChunkState> _frontStatesHandle;
        private VaultGenerationHandle<DebrisChunkState> _backStatesHandle;
        private JobHandle _simulationHandle;
        private IDataVault _dataVault;
        private Vector3 _pendingShiftOffset;
        private IDataVault _simulationJobGuardVault;
        private int _pendingBurstCount;
        private bool _simulationScheduled;
        private bool _simulationJobGuardHeld;
        private bool _dispatcherRegistered;
        private bool _lateFrameRegistered;
        private bool _originShiftRegistered;
        private bool _hotSwapRegistered;
        private bool _clearRequested;
        private bool _serviceRegistered;
        private bool _runtimeOwnerAborted;
        private bool _isInitialized;
        private bool _debrisSolveWarningArmed;
        private float _lastTickDeltaTime;
        private IThermodynamicsService _thermalRuntime;

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
            IDebrisService registeredService = GlobalRegistry.Debris;
            if (IsDebrisRuntimeUsable(registeredService))
            {
                DebrisManager existing = registeredService as DebrisManager;
                // Slot may be registered via Awake without heartbeat ready. Finish init in place.
                if (existing != null && !existing._isInitialized)
                    existing.InitializeService();
                return existing;
            }


            DebrisManager staleManager = registeredService as DebrisManager;
            if (!ReferenceEquals(staleManager, null))
            {
                GlobalRegistry.UnregisterDebrisService(registeredService);
                staleManager._serviceRegistered = false;
                staleManager._isInitialized = false;
            }
            else if (!ReferenceEquals(registeredService, null))
            {
                return null;
            }

            GameObject runtimeRoot = new GameObject("[DebrisManager]");
            DebrisManager manager = runtimeRoot.AddComponent<DebrisManager>();
            // Awake registers the slot but does not flip IServiceHeartbeat.IsServiceReady.
            // Bootstrap WaitForBootstrapDependencyHeartbeatAsync gates on IsServiceReady; call
            // InitializeService here so EnsureRuntimeInstance alone is enough for the node.
            if (manager != null)
                manager.InitializeService();
            return manager;
        }

        /// <summary>
        /// Registers the service into <see cref="GlobalRegistry"/> and marks heartbeat ready.
        /// </summary>
        public void InitializeService()
        {
            if (_isInitialized)
                return;

            if (!TryRegisterService())
                return;

            RefreshColdRegistryReferences();
            // Vault buffers are best-effort at Environment-phase install time. Failing them must
            // not leave IsServiceReady false forever (that timed out bootstrap SceneActivate).
            EnsureRuntimeResources();
            _isInitialized = _serviceRegistered;
        }

        private void Awake()
        {
            if (Application.isPlaying && !TryRegisterService())
                return;

            RefreshColdRegistryReferences();
            EnsureRuntimeResources();
        }

        private void OnEnable()
        {
            if (Application.isPlaying && !TryRegisterService())
                return;

            RefreshColdRegistryReferences();
            EnsureRuntimeResources();
            if (!Application.isPlaying)
                return;

            if (!_dispatcherRegistered)
                _dispatcherRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);

            if (!_lateFrameRegistered)
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);

            if (!_hotSwapRegistered)
                _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);

            if (!_originShiftRegistered)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _originShiftRegistered = HectonFloatingOrigin.IsListenerRegistered(this);
            }
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            UnregisterRuntimeHooks();

            _clearRequested = true;
            _pendingBurstCount = 0;
            if (!_simulationScheduled)
                ResetActiveState();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_serviceRegistered && ReferenceEquals(GlobalRegistry.Debris, this))
            {
                GlobalRegistry.UnregisterDebrisService(this);
                _serviceRegistered = false;
            }

            _isInitialized = false;

            UnregisterRuntimeHooks();
            _clearRequested = true;
            _pendingBurstCount = 0;
            ReleaseNativeState();
        }

        private bool TryRegisterService()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_serviceRegistered)
                return true;

            if (!Application.isPlaying)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            IDebrisService registeredService = GlobalRegistry.Debris;
            if (!ReferenceEquals(registeredService, null) && !ReferenceEquals(registeredService, this))
            {
                DebrisManager staleManager = registeredService as DebrisManager;
                if (ReferenceEquals(staleManager, null))
                {
                    _runtimeOwnerAborted = true;
                    Destroy(gameObject);
                    return false;
                }

                GlobalRegistry.UnregisterDebrisService(registeredService);
                staleManager._serviceRegistered = false;
                staleManager._isInitialized = false;
            }

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterDebrisService(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Debris, this);
            _runtimeOwnerAborted = !_serviceRegistered;
            if (_runtimeOwnerAborted)
                Destroy(gameObject);
            return _serviceRegistered;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            IDebrisService registeredService = GlobalRegistry.Debris;
            if (ReferenceEquals(registeredService, null) || ReferenceEquals(registeredService, this))
                return false;

            if (IsDebrisRuntimeUsable(registeredService))
            {
                _runtimeOwnerAborted = true;
                Destroy(gameObject);
                return true;
            }

            DebrisManager staleManager = registeredService as DebrisManager;
            if (!ReferenceEquals(staleManager, null))
            {
                GlobalRegistry.UnregisterDebrisService(registeredService);
                staleManager._serviceRegistered = false;
                staleManager._isInitialized = false;
            }

            return false;
        }

        private static bool IsDebrisRuntimeUsable(IDebrisService service)
        {
            if (ReferenceEquals(service, null))
                return false;

            DebrisManager manager = service as DebrisManager;
            return ReferenceEquals(manager, null) ||
                   (manager != null &&
                    manager._serviceRegistered &&
                    manager.isActiveAndEnabled &&
                    !manager._runtimeOwnerAborted);
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

            if (_hotSwapRegistered)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _hotSwapRegistered = false;
            }

            if (_originShiftRegistered)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _originShiftRegistered = false;
            }
        }

        private void EnsureRuntimeResources()
        {
            EnsureVaultBuffer(
                ref _frontStatesHandle,
                FrontStatesBufferId,
                MaxActiveChunks,
                NativeArrayOptions.ClearMemory);
            EnsureVaultBuffer(
                ref _backStatesHandle,
                BackStatesBufferId,
                MaxActiveChunks,
                NativeArrayOptions.ClearMemory);

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

            float pendingShiftSqrMagnitude = _pendingShiftOffset.sqrMagnitude;
            if (!MathGuard.IsFinite(_pendingShiftOffset) ||
                !MathGuard.IsFinite(pendingShiftSqrMagnitude))
            {
                _pendingShiftOffset = Vector3.zero;
            }
            else if (pendingShiftSqrMagnitude > 0.000001f && !_simulationScheduled)
            {
                if (TryAcquireVaultBuffer(in _frontStatesHandle, MaxActiveChunks, out NativeArray<DebrisChunkState> shiftedFrontStates, out IDataVault shiftedFrontVault))
                {
                    try
                    {
                        ApplyShiftToBuffer(shiftedFrontStates, _pendingShiftOffset);
                    }
                    finally
                    {
                        ReleaseVaultWrite(shiftedFrontVault, in _frontStatesHandle);
                    }
                }

                _pendingShiftOffset = Vector3.zero;
            }

            if (!_simulationScheduled)
                FlushPendingBursts();

            if (!_simulationScheduled &&
                TryReadVaultBuffer(in _frontStatesHandle, MaxActiveChunks, out NativeArray<DebrisChunkState> frontStatesForScan) &&
                HasSimulatedChunks(frontStatesForScan) &&
                TryAcquireSimulationJobGuard() &&
                TryOpenVaultBuffer(_simulationJobGuardVault, in _frontStatesHandle, MaxActiveChunks, out NativeArray<DebrisChunkState> frontStates) &&
                TryOpenVaultBuffer(_simulationJobGuardVault, in _backStatesHandle, MaxActiveChunks, out NativeArray<DebrisChunkState> backStates))
            {
                DebrisSimulationJob job = new DebrisSimulationJob
                {
                    ReadStates = frontStates,
                    WriteStates = backStates,
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
                _simulationHandle = job.Schedule(frontStates.Length, 32);
                _simulationScheduled = true;
            }
            else if (!_simulationScheduled)
            {
                ReleaseSimulationJobGuard();
            }

            PublishDebrisSolveWarningIfNeeded(solveStartTimestamp);
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            bool completedSimulation = false;

            if (_simulationScheduled && DispatcherJobSwap.TryComplete(ref _simulationHandle, forceComplete: false))
            {
                _simulationScheduled = false;
                ReleaseSimulationJobGuard();
                SwapStateBuffers();
                completedSimulation = true;
            }

            RenderActiveChunks();

            if (!completedSimulation)
                return;

            long solveStartTimestamp = global::System.Diagnostics.Stopwatch.GetTimestamp();
            ProcessThermalPetrification();
            PublishDebrisSolveWarningIfNeeded(solveStartTimestamp);
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!MathGuard.IsFinite(shiftOffset) ||
                !MathGuard.IsFinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.000001f)
            {
                return;
            }

            if (_simulationScheduled)
            {
                _pendingShiftOffset += shiftOffset;
                return;
            }

            if (TryAcquireVaultBuffer(in _frontStatesHandle, MaxActiveChunks, out NativeArray<DebrisChunkState> frontStates, out IDataVault frontVault))
            {
                try
                {
                    ApplyShiftToBuffer(frontStates, shiftOffset);
                }
                finally
                {
                    ReleaseVaultWrite(frontVault, in _frontStatesHandle);
                }
            }

            if (TryAcquireVaultBuffer(in _backStatesHandle, MaxActiveChunks, out NativeArray<DebrisChunkState> backStates, out IDataVault backVault))
            {
                try
                {
                    ApplyShiftToBuffer(backStates, shiftOffset);
                }
                finally
                {
                    ReleaseVaultWrite(backVault, in _backStatesHandle);
                }
            }
        }

        private void FlushPendingBursts()
        {
            if (_pendingBurstCount <= 0)
                return;

            if (!TryAcquireStateMutationGuard(out IDataVault guardVault))
                return;

            bool flushed = false;
            try
            {
                if (TryOpenVaultBuffer(guardVault, in _frontStatesHandle, MaxActiveChunks, out NativeArray<DebrisChunkState> frontStates) &&
                    TryOpenVaultBuffer(guardVault, in _backStatesHandle, MaxActiveChunks, out NativeArray<DebrisChunkState> backStates))
                {
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

                        if (CountFreeSlots(frontStates) < requestedSlots)
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

                            int slotIndex = FindFreeSlot(frontStates);
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
                                Scale = (float3)(ExtractScale(worldMatrix)),
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

                            frontStates[slotIndex] = state;
                            if (!_simulationScheduled)
                                backStates[slotIndex] = state;

                            _slotMeshes[slotIndex] = mesh;
                            _slotMaterials[slotIndex] = request.Definition.SharedMaterial;
                            _slotShadowModes[slotIndex] = request.Definition.ShadowCastingMode;
                            _slotReceiveShadows[slotIndex] = request.Definition.ReceiveShadows;
                            _slotLayerMasks[slotIndex] = request.Definition.RenderingLayerMask;
                            spawnedChunks++;
                        }
                    }

                    flushed = true;
                }
            }
            finally
            {
                guardVault.ReleaseMutationGuard(StateMutationGuardMask);
            }

            if (flushed)
                _pendingBurstCount = 0;
        }

        private void RenderActiveChunks()
        {
            if (!TryReadOnlyVaultBuffer(in _frontStatesHandle, MaxActiveChunks, out NativeArray<DebrisChunkState>.ReadOnly frontStates))
            {
                return;
            }

            int batchCount = 0;
            System.Array.Clear(_batchCounts, 0, _batchCounts.Length);

            for (int slotIndex = 0; slotIndex < frontStates.Length; slotIndex++)
            {
                DebrisChunkState state = frontStates[slotIndex];
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
                    DebrisChunkState state = frontStates[slotIndex];
                    Matrix4x4 matrix = Matrix4x4.TRS(
                        new Vector3(state.Position.x, state.Position.y, state.Position.z),
                        ToUnityQuaternion(state.Rotation),
                        new Vector3(state.Scale.x, state.Scale.y, state.Scale.z));

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

        private static bool HasSimulatedChunks(NativeArray<DebrisChunkState> frontStates)
        {
            if (!frontStates.IsCreated)
                return false;

            for (int i = 0; i < frontStates.Length; i++)
            {
                DebrisChunkState state = frontStates[i];
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

        private static int CountFreeSlots(NativeArray<DebrisChunkState> frontStates)
        {
            int freeCount = 0;
            if (!frontStates.IsCreated)
                return 0;

            for (int i = 0; i < frontStates.Length; i++)
            {
                DebrisChunkState state = frontStates[i];
                if (state.Active == 0 || state.SettledStatic != 0)
                    freeCount++;
            }

            return freeCount;
        }

        private static int FindFreeSlot(NativeArray<DebrisChunkState> frontStates)
        {
            if (!frontStates.IsCreated)
                return -1;

            for (int i = 0; i < frontStates.Length; i++)
            {
                if (frontStates[i].Active == 0)
                    return i;
            }

            int bestSettledSlot = -1;
            float bestSettledAge = -1f;
            for (int i = 0; i < frontStates.Length; i++)
            {
                DebrisChunkState state = frontStates[i];
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
            if (TryAcquireVaultBuffer(in _frontStatesHandle, MaxActiveChunks, out NativeArray<DebrisChunkState> frontStates, out IDataVault frontVault))
            {
                try
                {
                    for (int i = 0; i < frontStates.Length; i++)
                        frontStates[i] = default;
                }
                finally
                {
                    ReleaseVaultWrite(frontVault, in _frontStatesHandle);
                }
            }

            if (TryAcquireVaultBuffer(in _backStatesHandle, MaxActiveChunks, out NativeArray<DebrisChunkState> backStates, out IDataVault backVault))
            {
                try
                {
                    for (int i = 0; i < backStates.Length; i++)
                        backStates[i] = default;
                }
                finally
                {
                    ReleaseVaultWrite(backVault, in _backStatesHandle);
                }
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
            if (!TryAcquireVaultBuffer(in _frontStatesHandle, MaxActiveChunks, out NativeArray<DebrisChunkState> frontStates, out IDataVault frontVault))
                return;

            try
            {
                IThermodynamicsService thermalManager = _thermalRuntime;
                if (thermalManager == null)
                    return;

                float deltaTime = _lastTickDeltaTime;
                for (int slotIndex = 0; slotIndex < frontStates.Length; slotIndex++)
                {
                    DebrisChunkState state = frontStates[slotIndex];
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
                            out ThermodynamicFlowSampleDTO sample) &&
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
                        frontStates[slotIndex] = state;
                        continue;
                    }

                    if (!TryResolveRuntimeAup(runtimePosition, out double3 absolutePosition))
                        continue;

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
                        frontStates[slotIndex] = state;
                        _thermalPetrificationTimers[slotIndex] = 0f;
                        _thermalPetrificationNextProbeAges[slotIndex] = 0f;
                        _thermalPetrificationHotFlags[slotIndex] = 0;
                    }
                }
            }
            finally
            {
                ReleaseVaultWrite(frontVault, in _frontStatesHandle);
            }
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out double3 absolutePosition)
        {
            absolutePosition = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!positionAup.IsFinite())
                return false;

            absolutePosition = positionAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absolutePosition));
        }

        private void RefreshColdRegistryReferences()
        {
            _thermalRuntime = GlobalRegistry.ThermodynamicsService;
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null && !_hotSwapRegistered)
                _dataVault = GlobalRegistry.DataVault;

            return _dataVault;
        }

        private bool EnsureVaultBuffer(
            ref VaultGenerationHandle<DebrisChunkState> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options)
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null || requiredLength <= 0)
                return false;

            if (IsVaultHandleCreated(in handle) &&
                vault.TryReadOnlyHandle(in handle, out NativeArray<DebrisChunkState>.ReadOnly existing) &&
                existing.IsCreated &&
                existing.Length >= requiredLength)
            {
                return true;
            }

            if (IsVaultHandleCreated(in handle))
                vault.ReleaseBuffer(in handle);

            handle = vault.EnsureGenerationHandle<DebrisChunkState>(
                bufferId,
                requiredLength,
                VaultOwnerSystemId,
                options);

            return IsVaultHandleCreated(in handle) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<DebrisChunkState>.ReadOnly resolved) &&
                   resolved.IsCreated &&
                   resolved.Length >= requiredLength;
        }

        private bool TryReadVaultBuffer(
            in VaultGenerationHandle<DebrisChunkState> handle,
            int requiredLength,
            out NativeArray<DebrisChunkState> buffer)
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null || !IsVaultHandleCreated(in handle))
                return false;

            return vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryReadOnlyVaultBuffer(
            in VaultGenerationHandle<DebrisChunkState> handle,
            int requiredLength,
            out NativeArray<DebrisChunkState>.ReadOnly buffer)
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null || !IsVaultHandleCreated(in handle))
                return false;

            return vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryOpenVaultBuffer(
            in VaultGenerationHandle<DebrisChunkState> handle,
            int requiredLength,
            out NativeArray<DebrisChunkState> buffer)
        {
            IDataVault vault = _dataVault;
            return TryOpenVaultBuffer(vault, in handle, requiredLength, out buffer);
        }

        private static bool TryOpenVaultBuffer(
            IDataVault vault,
            in VaultGenerationHandle<DebrisChunkState> handle,
            int requiredLength,
            out NativeArray<DebrisChunkState> buffer)
        {
            buffer = default;
            if (vault == null || !IsVaultHandleCreated(in handle))
                return false;

            return vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryAcquireVaultBuffer(
            in VaultGenerationHandle<DebrisChunkState> handle,
            int requiredLength,
            out NativeArray<DebrisChunkState> buffer,
            out IDataVault writeVault)
        {
            buffer = default;
            writeVault = null;
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsVaultHandleCreated(in handle) ||
                !vault.TryAcquireWriteLock(in handle, VaultOwnerSystemId, out buffer))
            {
                return false;
            }

            bool ownershipTransferred = false;
            try
            {
                if (buffer.IsCreated && buffer.Length >= requiredLength)
                {
                    writeVault = vault;
                    ownershipTransferred = true;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (!ownershipTransferred)
                    vault.ReleaseWriteLock(in handle, VaultOwnerSystemId);
            }
        }

        private static void ReleaseVaultWrite(IDataVault vault, in VaultGenerationHandle<DebrisChunkState> handle)
        {
            vault?.ReleaseWriteLock(in handle, VaultOwnerSystemId);
        }

        private void ReleaseVaultBuffer(ref VaultGenerationHandle<DebrisChunkState> handle)
        {
            IDataVault vault = _dataVault;
            if (vault != null && IsVaultHandleCreated(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private bool TryAcquireSimulationJobGuard()
        {
            if (_simulationJobGuardHeld)
                return true;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsVaultHandleCreated(in _frontStatesHandle) ||
                !IsVaultHandleCreated(in _backStatesHandle))
            {
                return false;
            }

            if (!vault.TryAcquireMutationGuard(StateMutationGuardMask))
                return false;

            _simulationJobGuardVault = vault;
            _simulationJobGuardHeld = true;
            return true;
        }

        private void ReleaseSimulationJobGuard()
        {
            if (!_simulationJobGuardHeld)
                return;

            IDataVault vault = _simulationJobGuardVault;
            _simulationJobGuardVault = null;
            _simulationJobGuardHeld = false;
            if (vault != null)
                vault.ReleaseMutationGuard(StateMutationGuardMask);
        }

        private bool TryAcquireStateMutationGuard(out IDataVault vault)
        {
            vault = _dataVault;
            return vault != null &&
                   IsVaultHandleCreated(in _frontStatesHandle) &&
                   IsVaultHandleCreated(in _backStatesHandle) &&
                   vault.TryAcquireMutationGuard(StateMutationGuardMask);
        }

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private static BufferID ToBufferID(in VaultGenerationHandle<DebrisChunkState> handle)
        {
            return (BufferID)unchecked((int)handle.BufferID);
        }

        private static bool IsVaultHandleCreated(in VaultGenerationHandle<DebrisChunkState> handle)
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.ThermodynamicsRuntime)
            {
                _thermalRuntime = currentService as IThermodynamicsService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                IDataVault nextVault = currentService as IDataVault;
                if (ReferenceEquals(_dataVault, nextVault))
                    return;

                ReleaseNativeState();
                _dataVault = nextVault;
                EnsureRuntimeResources();
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
            VaultGenerationHandle<DebrisChunkState> oldFront = _frontStatesHandle;
            _frontStatesHandle = _backStatesHandle;
            _backStatesHandle = oldFront;
        }

        private void ApplyShiftToBuffer(NativeArray<DebrisChunkState> buffer, Vector3 shiftOffset)
        {
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!buffer.IsCreated ||
                !MathGuard.IsFinite(shiftOffset) ||
                !MathGuard.IsFinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.000001f)
            {
                return;
            }

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
            uint seed = unchecked((uint)(SystemDispatcher.CurrentFrameIndex + 1));
            return seed == 0u ? 1u : seed;
        }

        private void ReleaseNativeState()
        {
            if (_simulationScheduled)
                ForceCompleteSimulationInPostSimulationWindow();

            _simulationScheduled = false;
            ReleaseSimulationJobGuard();
            ReleaseVaultBuffer(ref _frontStatesHandle);
            ReleaseVaultBuffer(ref _backStatesHandle);

            _simulationHandle = default;
        }

        private void ForceCompleteSimulationInPostSimulationWindow()
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                DispatcherJobSwap.TryComplete(ref _simulationHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
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

        [StructLayout(LayoutKind.Explicit, Size = 120)]
        private struct DebrisChunkState
        {
            [FieldOffset(0)]
            public float3 Position;
            [FieldOffset(12)]
            public quaternion Rotation;
            [FieldOffset(28)]
            public float3 Scale;
            [FieldOffset(40)]
            public float3 Velocity;
            [FieldOffset(52)]
            public float3 AngularVelocity;
            [FieldOffset(64)]
            public float Age;
            [FieldOffset(68)]
            public float GroundY;
            [FieldOffset(72)]
            public float SinkStartY;
            [FieldOffset(76)]
            public float SinkTargetY;
            [FieldOffset(80)]
            public float SinkDuration;
            [FieldOffset(84)]
            public float SinkDistance;
            [FieldOffset(88)]
            public float LinearDamping;
            [FieldOffset(92)]
            public float AngularDamping;
            [FieldOffset(96)]
            public float BounceDamping;
            [FieldOffset(100)]
            public float MassScale;
            [FieldOffset(104)]
            public float PhysicsPhaseDuration;
            [FieldOffset(108)]
            public float PoolReturnDelay;
            [FieldOffset(112)]
            public byte Active;
            [FieldOffset(113)]
            public byte CollisionEnabled;
            [FieldOffset(114)]
            public byte Kinematic;
            [FieldOffset(115)]
            public byte SettledStatic;
            [FieldOffset(116)]
            private uint _pad0;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct DebrisSimulationJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<DebrisChunkState> ReadStates;
            [WriteOnly, NoAlias] public NativeArray<DebrisChunkState> WriteStates;
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
        // COLD ALLOC: List<MeshFilter> - reusable chunk authoring scan buffer - owner: OrganicDebrisProfile
        private readonly List<MeshFilter> _meshFilterScratch = new List<MeshFilter>(16);
        // COLD ALLOC: List<Collider> - reusable chunk collider authoring scan buffer - owner: OrganicDebrisProfile
        private readonly List<Collider> _colliderScratch = new List<Collider>(16);

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
            _meshFilterScratch.Clear();
            root.GetComponentsInChildren<MeshFilter>(true, _meshFilterScratch);
            int validCount = 0;

            for (int i = 0; i < _meshFilterScratch.Count; i++)
            {
                MeshFilter meshFilter = _meshFilterScratch[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;

                if (!meshFilter.TryGetComponent(out Renderer renderer))
                    continue;

                validCount++;
            }

            cachedChunkMeshes = new Mesh[validCount];
            cachedLocalMatrices = new Matrix4x4[validCount];
            cachedMassScales = new float[validCount];
            _colliderScratch.Clear();
            root.GetComponentsInChildren<Collider>(true, _colliderScratch);
            cachedRuntimeColliders = new Collider[_colliderScratch.Count]; // COLD ALLOC: Collider[][chunk collider count] - runtime collider disable cache - owner: OrganicDebrisProfile
            for (int i = 0; i < _colliderScratch.Count; i++)
                cachedRuntimeColliders[i] = _colliderScratch[i];
            _colliderScratch.Clear();

            Matrix4x4 rootWorldToLocal = transform.worldToLocalMatrix;
            int writeIndex = 0;
            for (int i = 0; i < _meshFilterScratch.Count; i++)
            {
                MeshFilter meshFilter = _meshFilterScratch[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;

                if (!meshFilter.TryGetComponent(out Renderer renderer))
                    continue;

                cachedChunkMeshes[writeIndex] = meshFilter.sharedMesh;
                cachedLocalMatrices[writeIndex] = rootWorldToLocal * meshFilter.transform.localToWorldMatrix;
                cachedMassScales[writeIndex] = math.max(0.25f, DebrisManager.EstimateMagnitudeNoSqrt(meshFilter.sharedMesh.bounds.extents.sqrMagnitude));
                writeIndex++;
            }

            _meshFilterScratch.Clear();
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
