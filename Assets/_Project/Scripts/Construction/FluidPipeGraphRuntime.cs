using System;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Logistics;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Hecton8.Construction
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Construction/Fluid Pipe Graph Runtime")]
    public sealed class FluidPipeGraphRuntime : MonoBehaviour, IFluidPipeGraphService, ISlowTickable, ILateFrameTickable, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private static int s_x001FluidPipeGraphRuntimeSignalPushDropCount;
        private const float SlowTickStepSeconds = 0.1f;
        private const uint PipeImpactMaterialHash = 0x50495045u;
        private const SystemID OwnerSystemId = SystemID.Construction;
        private const int RuptureBudgetLength = 3;
        private const BufferID PipePressureBufferId = BufferID.FluidPipeGraphRuntime_PipePressureBufferId;
        private const BufferID PipeContentsBufferId = BufferID.FluidPipeGraphRuntime_PipeContentsBufferId;
        private const BufferID PipeFlagsBufferId = BufferID.FluidPipeGraphRuntime_PipeFlagsBufferId;
        private const BufferID PipeContentKindsBufferId = BufferID.FluidPipeGraphRuntime_PipeContentKindsBufferId;
        private const BufferID PipeNetworkIdsBufferId = BufferID.FluidPipeGraphRuntime_PipeNetworkIdsBufferId;
        private const BufferID PipeRoomIndicesBufferId = BufferID.FluidPipeGraphRuntime_PipeRoomIndicesBufferId;
        private const BufferID PipeCapacitiesBufferId = BufferID.FluidPipeGraphRuntime_PipeCapacitiesBufferId;
        private const BufferID PipeMaxPressureBufferId = BufferID.FluidPipeGraphRuntime_PipeMaxPressureBufferId;
        private const BufferID PipeFlowRatesBufferId = BufferID.FluidPipeGraphRuntime_PipeFlowRatesBufferId;
        private const BufferID PipeSourceRatesBufferId = BufferID.FluidPipeGraphRuntime_PipeSourceRatesBufferId;
        private const BufferID PipeDemandRatesBufferId = BufferID.FluidPipeGraphRuntime_PipeDemandRatesBufferId;
        private const BufferID PipeFlowVectorsBufferId = BufferID.FluidPipeGraphRuntime_PipeFlowVectorsBufferId;
        private const BufferID PipeRoomExchangeContentsBufferId = BufferID.FluidPipeGraphRuntime_PipeRoomExchangeContentsBufferId;
        private const BufferID PipeLastVisualFlowBufferId = BufferID.FluidPipeGraphRuntime_PipeLastVisualFlowBufferId;
        private const BufferID PipeAupsBufferId = BufferID.FluidPipeGraphRuntime_PipeAupsBufferId;
        private const BufferID PipeTelemetryRingBufferId = BufferID.FluidPipeGraphRuntime_PipeTelemetryRingBufferId;
        private const BufferID PipeRuptureTelemetryRingBufferId = BufferID.FluidPipeGraphRuntime_PipeRuptureTelemetryRingBufferId;
        private const BufferID PipeRuptureBudgetBufferId = BufferID.FluidPipeGraphRuntime_PipeRuptureBudgetBufferId;
        private const BufferID PipeConnectionSourcesBufferId = BufferID.FluidPipeGraphRuntime_PipeConnectionSourcesBufferId;
        private const BufferID PipeConnectionDestinationsBufferId = BufferID.FluidPipeGraphRuntime_PipeConnectionDestinationsBufferId;
        private const BufferID PipeRuptureDispatchBufferId = BufferID.FluidPipeGraphRuntime_PipeRuptureDispatchBufferId;
        private const BufferID PipeConnectionOffsetsBufferId = BufferID.FluidPipeGraphRuntime_PipeConnectionOffsetsBufferId;
        private const BufferID PipeConnectionCsrDestinationsBufferId = BufferID.FluidPipeGraphRuntime_PipeConnectionCsrDestinationsBufferId;
        private const BufferID PipeConnectionWriteCursorBufferId = BufferID.FluidPipeGraphRuntime_PipeConnectionWriteCursorBufferId;
        private const uint SolveLockPressure = 1u << 0;
        private const uint SolveLockContents = 1u << 1;
        private const uint SolveLockFlags = 1u << 2;
        private const uint SolveLockContentKinds = 1u << 3;
        private const uint SolveLockNetworkIds = 1u << 4;
        private const uint SolveLockRoomIndices = 1u << 5;
        private const uint SolveLockCapacities = 1u << 6;
        private const uint SolveLockMaxPressure = 1u << 7;
        private const uint SolveLockFlowRates = 1u << 8;
        private const uint SolveLockSourceRates = 1u << 9;
        private const uint SolveLockDemandRates = 1u << 10;
        private const uint SolveLockFlowVectors = 1u << 11;
        private const uint SolveLockRoomExchange = 1u << 12;
        private const uint SolveLockTelemetry = 1u << 13;
        private const uint SolveLockRuptureTelemetry = 1u << 14;
        private const uint SolveLockRuptureBudget = 1u << 15;
        private const uint SolveLockConnectionSources = 1u << 16;
        private const uint SolveLockConnectionDestinations = 1u << 17;
        private const uint SolveLockRuptureDispatch = 1u << 18;
        private const uint SolveLockLastVisualFlow = 1u << 19;
        private const uint SolveLockAups = 1u << 20;
        private const uint SolveLockConnectionOffsets = 1u << 21;
        private const uint SolveLockConnectionCsrDestinations = 1u << 22;
        private const uint SolveLockConnectionWriteCursor = 1u << 23;

        [Header("Graph")]
        [SerializeField, Min(16)] private int nodeCapacity = 512;
        [SerializeField, Min(16)] private int connectionCapacity = 1024;
        [SerializeField, Min(0.001f)] private float defaultPipeFlowRate = FluidPipeGraphConstants.DefaultFlowRate;

        [Header("Integration")]
        [SerializeField, FormerlySerializedAs("atmosphereSystem")] private MonoBehaviour atmosphereSystemSource;

        [Header("Diagnostics")]
        [SerializeField] private int _debugNodeCount;
        [SerializeField] private int _debugLastRuptureCount;
        [SerializeField] private float _debugLastMaxPressureKPa;

        private IDataVault _dataVault;
        private VaultGenerationHandle<float> _pipePressureHandle;
        private VaultGenerationHandle<float> _pipeContentsHandle;
        private VaultGenerationHandle<byte> _pipeFlagsHandle;
        private VaultGenerationHandle<byte> _pipeContentKindsHandle;
        private VaultGenerationHandle<int> _pipeNetworkIdsHandle;
        private VaultGenerationHandle<int> _pipeRoomIndicesHandle;
        private VaultGenerationHandle<float> _pipeCapacitiesHandle;
        private VaultGenerationHandle<float> _pipeMaxPressureHandle;
        private VaultGenerationHandle<float> _pipeFlowRatesHandle;
        private VaultGenerationHandle<float> _pipeSourceRatesHandle;
        private VaultGenerationHandle<float> _pipeDemandRatesHandle;
        private VaultGenerationHandle<float3> _pipeFlowVectorsHandle;
        private VaultGenerationHandle<float> _pipeRoomExchangeContentsHandle;
        private VaultGenerationHandle<float> _pipeLastVisualFlowHandle;
        private VaultGenerationHandle<AbsoluteUniversePosition> _pipeAupsHandle;
        private VaultGenerationHandle<FluidPipeTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<FluidPipeRuptureRecord> _ruptureTelemetryRingHandle;
        private VaultGenerationHandle<int> _ruptureQueueBudgetHandle;
        private VaultGenerationHandle<int> _pipeConnectionSourcesHandle;
        private VaultGenerationHandle<int> _pipeConnectionDestinationsHandle;
        private VaultGenerationHandle<int> _pipeConnectionOffsetsHandle;
        private VaultGenerationHandle<int> _pipeConnectionCsrDestinationsHandle;
        private VaultGenerationHandle<int> _pipeConnectionWriteCursorHandle;
        private VaultGenerationHandle<FluidPipeRuptureRecord> _ruptureDispatchHandle;

        private JobHandle _solveHandle;
        private IDataVault _solveLockVault;
        private uint _solveLockMask;
        private bool _solveScheduled;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private bool _registeredService;
        private bool _registeredHotSwap;
        private bool _initialized;
        private bool _atmosphereResolveAttempted;
        private bool _blackBoxDumped;
        private uint _blackBoxStateHash;
        private uint _blackBoxFlags;
        private int _blackBoxTelemetryCount;
        private int _blackBoxRuptureCount;
        private bool _connectionTopologyDirty = true;
        private ISubmarineAtmosphereRoomMutationSink _atmosphereSystem;
        private int _nodeCount;
        private int _connectionCount;
        private int _frameIndex;
        private int _telemetryCursor;
        private float _solveAccumulator;

        public bool IsInitialized => _initialized;
        public int PipeNodeCount => _nodeCount;

        public bool TryGetLastBlackBoxSummary(out uint stateHash, out uint flags, out int telemetryCount, out int ruptureCount)
        {
            if (_blackBoxDumped)
            {
                stateHash = _blackBoxStateHash;
                flags = _blackBoxFlags;
                telemetryCount = _blackBoxTelemetryCount;
                ruptureCount = _blackBoxRuptureCount;
                return true;
            }

            stateHash = 0u;
            flags = 0u;
            telemetryCount = 0;
            ruptureCount = 0;
            return false;
        }


        /// <summary>
        /// Resolve-or-create the sole FluidPipeGraphRuntime owner for GlobalRegistry.FluidPipeGraph.
        /// Script GUID ffc0ea3d61e66f842999d9cc00327913 has ZERO live scene/prefab hits; without this
        /// path IFluidPipeGraphService consumers (electrolysis modules, physiology) stay permanent null.
        /// </summary>
        public static FluidPipeGraphRuntime EnsureRuntimeInstance()
        {
            IFluidPipeGraphService registered = GlobalRegistry.FluidPipeGraph;
            FluidPipeGraphRuntime registeredRuntime = registered as FluidPipeGraphRuntime;
            if (IsFluidPipeGraphRuntimeUsable(registeredRuntime))
                return registeredRuntime;

            if (!ReferenceEquals(registered, null))
            {
                GlobalRegistry.UnregisterFluidPipeGraphService(registered);
                if (!ReferenceEquals(registeredRuntime, null))
                    registeredRuntime._registeredService = false;
            }

            if (!Application.isPlaying)
                return null;

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Sole FluidPipeGraph owner; must construct when bootstrap reorders.
            GameObject runtimeRoot = new GameObject("[FluidPipeGraphRuntime]"); // COLD ALLOC
            return runtimeRoot.AddComponent<FluidPipeGraphRuntime>();
        }

        private static bool IsFluidPipeGraphRuntimeUsable(FluidPipeGraphRuntime instance)
        {
            return !ReferenceEquals(instance, null) && instance != null && instance.isActiveAndEnabled;
        }

        private void Awake()
        {
            ResolveAtmosphereSystem(force: true);
            CacheDataVaultCold();
            EnsureNativeState();
        }

        private void OnEnable()
        {
            ResolveAtmosphereSystem(force: true);
            CacheDataVaultCold();
            EnsureNativeState();
            TryRegisterHotSwapListener();
            RegisterRuntime();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            UnregisterRuntime();
            CompleteSolve(force: true);
        }

        private void OnDestroy()
        {
            DisposeNativeState();
        }

        public void OnServiceShutdown()
        {
            TryUnregisterHotSwapListener();
            UnregisterRuntime();
            DisposeNativeState();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                CompleteSolve(force: true);
                IDataVault previousVault = previousService is IDataVault oldVault ? oldVault : _dataVault;
                ReleaseFluidPipeVaultBuffers(previousVault);
                _dataVault = currentService is IDataVault currentVault ? currentVault : null;
                _initialized = false;
                if (isActiveAndEnabled)
                    EnsureNativeState();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            UnregisterDispatcherTickLanes();

            if (currentService != null && isActiveAndEnabled && (_registeredService || !_registeredSlowTick || !_registeredLateFrameTick))
                RegisterRuntime();
        }

        public void SlowTick()
        {
            if (!_initialized)
                return;

            _solveAccumulator += SlowTickStepSeconds;
            float quality = ResolveGlobalQualityWeight();
            float cadence = FluidPipeGraphConstants.ResolveCadenceSeconds(quality);
            if (_solveAccumulator + 0.0001f < cadence)
                return;

            if (_solveScheduled)
                return;

            float deltaTime = _solveAccumulator;
            ApplyPumpInputs(deltaTime);
            ApplyElectrolysisInputs(deltaTime);
            if (ScheduleSolve(deltaTime))
                _solveAccumulator = 0f;
        }

        public void LateFrameTick()
        {
            if (!CompleteSolve(force: false))
                return;

            ProcessSolvedOutputs();
        }

        public bool TryReadPipeNode(int nodeIndex, out float pressureKPa, out float contents, out byte flags)
        {
            if (!_initialized || _solveScheduled || nodeIndex < 0 || nodeIndex >= _nodeCount ||
                !TryReadOnlyBuffer(in _pipePressureHandle, PipePressureBufferId, nodeCapacity, out NativeArray<float>.ReadOnly pipePressure) ||
                !TryReadOnlyBuffer(in _pipeContentsHandle, PipeContentsBufferId, nodeCapacity, out NativeArray<float>.ReadOnly pipeContents) ||
                !TryReadOnlyBuffer(in _pipeFlagsHandle, PipeFlagsBufferId, nodeCapacity, out NativeArray<byte>.ReadOnly pipeFlags))
            {
                pressureKPa = 0f;
                contents = 0f;
                flags = 0;
                return false;
            }

            pressureKPa = pipePressure[nodeIndex];
            contents = pipeContents[nodeIndex];
            flags = pipeFlags[nodeIndex];
            return true;
        }

        public bool TryRegisterPipeNode(
            int networkId,
            int roomIndex,
            byte contentKind,
            AbsoluteUniversePosition nodeAup,
            float capacity,
            float maxPressureKPa,
            out int nodeIndex)
        {
            EnsureNativeState();
            nodeIndex = -1;
            if (_solveScheduled || _nodeCount >= nodeCapacity)
            {
                return false;
            }

            IDataVault vault = ResolveDataVault();
            uint lockMask = 0u;
            try
            {
                if (!TryAcquireSolveWriteBuffer(vault, in _pipePressureHandle, nodeCapacity, SolveLockPressure, ref lockMask, out NativeArray<float> pipePressure) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeContentsHandle, nodeCapacity, SolveLockContents, ref lockMask, out NativeArray<float> pipeContents) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeFlagsHandle, nodeCapacity, SolveLockFlags, ref lockMask, out NativeArray<byte> pipeFlags) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeContentKindsHandle, nodeCapacity, SolveLockContentKinds, ref lockMask, out NativeArray<byte> pipeContentKinds) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeNetworkIdsHandle, nodeCapacity, SolveLockNetworkIds, ref lockMask, out NativeArray<int> pipeNetworkIds) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeRoomIndicesHandle, nodeCapacity, SolveLockRoomIndices, ref lockMask, out NativeArray<int> pipeRoomIndices) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeCapacitiesHandle, nodeCapacity, SolveLockCapacities, ref lockMask, out NativeArray<float> pipeCapacities) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeMaxPressureHandle, nodeCapacity, SolveLockMaxPressure, ref lockMask, out NativeArray<float> pipeMaxPressure) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeFlowRatesHandle, nodeCapacity, SolveLockFlowRates, ref lockMask, out NativeArray<float> pipeFlowRates) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeSourceRatesHandle, nodeCapacity, SolveLockSourceRates, ref lockMask, out NativeArray<float> pipeSourceRates) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeDemandRatesHandle, nodeCapacity, SolveLockDemandRates, ref lockMask, out NativeArray<float> pipeDemandRates) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeFlowVectorsHandle, nodeCapacity, SolveLockFlowVectors, ref lockMask, out NativeArray<float3> pipeFlowVectors) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeRoomExchangeContentsHandle, nodeCapacity, SolveLockRoomExchange, ref lockMask, out NativeArray<float> pipeRoomExchangeContents) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeLastVisualFlowHandle, nodeCapacity, SolveLockLastVisualFlow, ref lockMask, out NativeArray<float> pipeLastVisualFlow01) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeAupsHandle, nodeCapacity, SolveLockAups, ref lockMask, out NativeArray<AbsoluteUniversePosition> pipeAups))
                {
                    return false;
                }

                nodeIndex = _nodeCount++;
                _debugNodeCount = _nodeCount;
                pipePressure[nodeIndex] = 0f;
                pipeContents[nodeIndex] = 0f;
                pipeFlags[nodeIndex] = (byte)FluidPipeFlags.Active;
                pipeContentKinds[nodeIndex] = contentKind;
                pipeNetworkIds[nodeIndex] = networkId;
                pipeRoomIndices[nodeIndex] = roomIndex;
                pipeCapacities[nodeIndex] = math.max(FluidPipeGraphConstants.MinCapacity, capacity);
                pipeMaxPressure[nodeIndex] = math.max(FluidPipeGraphConstants.MinMaxPressureKPa, maxPressureKPa);
                pipeFlowRates[nodeIndex] = math.max(0f, defaultPipeFlowRate);
                pipeSourceRates[nodeIndex] = 0f;
                pipeDemandRates[nodeIndex] = 0f;
                pipeFlowVectors[nodeIndex] = default;
                pipeRoomExchangeContents[nodeIndex] = 0f;
                pipeLastVisualFlow01[nodeIndex] = 0f;
                pipeAups[nodeIndex] = nodeAup;
                _connectionTopologyDirty = true;
                return true;
            }
            finally
            {
                ReleaseSolveWriteLocks(vault, lockMask);
            }
        }

        public bool TryConnectPipeNodes(int sourceNodeIndex, int destinationNodeIndex)
        {
            if (!_initialized ||
                _solveScheduled ||
                !IsValidNode(sourceNodeIndex) ||
                !IsValidNode(destinationNodeIndex) ||
                sourceNodeIndex == destinationNodeIndex ||
                !TryReadOnlyBuffer(in _pipeNetworkIdsHandle, PipeNetworkIdsBufferId, nodeCapacity, out NativeArray<int>.ReadOnly pipeNetworkIds) ||
                !TryReadOnlyBuffer(in _pipeContentKindsHandle, PipeContentKindsBufferId, nodeCapacity, out NativeArray<byte>.ReadOnly pipeContentKinds) ||
                pipeNetworkIds[sourceNodeIndex] != pipeNetworkIds[destinationNodeIndex] ||
                pipeContentKinds[sourceNodeIndex] != pipeContentKinds[destinationNodeIndex])
            {
                return false;
            }

            IDataVault vault = ResolveDataVault();
            uint lockMask = 0u;
            try
            {
                if (!TryAcquireSolveWriteBuffer(vault, in _pipeConnectionSourcesHandle, connectionCapacity, SolveLockConnectionSources, ref lockMask, out NativeArray<int> connectionSources) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeConnectionDestinationsHandle, connectionCapacity, SolveLockConnectionDestinations, ref lockMask, out NativeArray<int> connectionDestinations))
                {
                    return false;
                }

                int neededConnections = 0;
                bool hasForward = HasConnection(sourceNodeIndex, destinationNodeIndex, connectionSources, connectionDestinations);
                bool hasReverse = HasConnection(destinationNodeIndex, sourceNodeIndex, connectionSources, connectionDestinations);
                if (!hasForward)
                    neededConnections++;
                if (!hasReverse)
                    neededConnections++;
                if (neededConnections == 0)
                    return true;
                if (_connectionCount + neededConnections > connectionCapacity)
                    return false;

                if (!hasForward)
                {
                    connectionSources[_connectionCount] = sourceNodeIndex;
                    connectionDestinations[_connectionCount] = destinationNodeIndex;
                    _connectionCount++;
                }

                if (!hasReverse)
                {
                    connectionSources[_connectionCount] = destinationNodeIndex;
                    connectionDestinations[_connectionCount] = sourceNodeIndex;
                    _connectionCount++;
                }

                _connectionTopologyDirty = true;
                return true;
            }
            finally
            {
                ReleaseSolveWriteLocks(vault, lockMask);
            }
        }

        public bool TryInjectPipeContents(int nodeIndex, float contents)
        {
            if (!_initialized || _solveScheduled || !IsValidNode(nodeIndex) || !math.isfinite(contents) || contents <= 0f ||
                !TryReadOnlyBuffer(in _pipeCapacitiesHandle, PipeCapacitiesBufferId, nodeCapacity, out NativeArray<float>.ReadOnly pipeCapacities) ||
                !TryReadOnlyBuffer(in _pipeMaxPressureHandle, PipeMaxPressureBufferId, nodeCapacity, out NativeArray<float>.ReadOnly pipeMaxPressure))
                return false;

            IDataVault vault = ResolveDataVault();
            uint lockMask = 0u;
            try
            {
                if (!TryAcquireSolveWriteBuffer(vault, in _pipeContentsHandle, nodeCapacity, SolveLockContents, ref lockMask, out NativeArray<float> pipeContents) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipePressureHandle, nodeCapacity, SolveLockPressure, ref lockMask, out NativeArray<float> pipePressure))
                {
                    return false;
                }

                float nextContents = pipeContents[nodeIndex] + contents;
                if (!math.isfinite(nextContents))
                    return false;

                pipeContents[nodeIndex] = nextContents;
                pipePressure[nodeIndex] = ResolvePressureForNode(nodeIndex, pipeContents[nodeIndex], pipeCapacities, pipeMaxPressure);
                return true;
            }
            finally
            {
                ReleaseSolveWriteLocks(vault, lockMask);
            }
        }

        public bool TrySetPipeSourceRate(int nodeIndex, float contentsPerSecond)
        {
            if (!_initialized || _solveScheduled || !IsValidNode(nodeIndex) || !math.isfinite(contentsPerSecond))
                return false;

            IDataVault vault = ResolveDataVault();
            uint lockMask = 0u;
            try
            {
                if (!TryAcquireSolveWriteBuffer(vault, in _pipeSourceRatesHandle, nodeCapacity, SolveLockSourceRates, ref lockMask, out NativeArray<float> pipeSourceRates))
                    return false;

                pipeSourceRates[nodeIndex] = math.max(0f, contentsPerSecond);
                return true;
            }
            finally
            {
                ReleaseSolveWriteLocks(vault, lockMask);
            }
        }

        public bool TrySetPipeDemandRate(int nodeIndex, float contentsPerSecond)
        {
            if (!_initialized || _solveScheduled || !IsValidNode(nodeIndex) || !math.isfinite(contentsPerSecond))
                return false;

            IDataVault vault = ResolveDataVault();
            uint lockMask = 0u;
            try
            {
                if (!TryAcquireSolveWriteBuffer(vault, in _pipeDemandRatesHandle, nodeCapacity, SolveLockDemandRates, ref lockMask, out NativeArray<float> pipeDemandRates))
                    return false;

                pipeDemandRates[nodeIndex] = math.max(0f, contentsPerSecond);
                return true;
            }
            finally
            {
                ReleaseSolveWriteLocks(vault, lockMask);
            }
        }

        public bool TrySetPipeNodeFlags(int nodeIndex, byte setMask, byte clearMask)
        {
            if (!_initialized || _solveScheduled || !IsValidNode(nodeIndex))
                return false;

            IDataVault vault = ResolveDataVault();
            uint lockMask = 0u;
            try
            {
                if (!TryAcquireSolveWriteBuffer(vault, in _pipeFlagsHandle, nodeCapacity, SolveLockFlags, ref lockMask, out NativeArray<byte> pipeFlags))
                    return false;

                byte flags = pipeFlags[nodeIndex];
                flags = (byte)((flags | setMask) & ~clearMask);
                pipeFlags[nodeIndex] = flags;
                return true;
            }
            finally
            {
                ReleaseSolveWriteLocks(vault, lockMask);
            }
        }

        private void EnsureNativeState()
        {
            if (_initialized)
                return;

            nodeCapacity = math.max(16, nodeCapacity);
            connectionCapacity = math.max(16, connectionCapacity);
            if (!FluidPipeGraphLayoutSentinel.ValidateRuntimeDtos())
            {
                _initialized = false;
                return;
            }

            IDataVault vault = ResolveDataVault();
            if (vault == null || !EnsureFluidPipeVaultBuffers(vault))
            {
                _initialized = false;
                return;
            }

            _initialized = true;
            _connectionTopologyDirty = true;
        }

        private void DisposeNativeState()
        {
            CompleteSolve(force: true);
            UnregisterRuntime();
            ReleaseFluidPipeVaultBuffers(ResolveDataVault());

            _initialized = false;
            _nodeCount = 0;
            _connectionCount = 0;
            _solveLockMask = 0u;
            _connectionTopologyDirty = true;
        }

        private void RegisterRuntime()
        {
            if (!_registeredService)
            {
                GlobalRegistry.RegisterFluidPipeGraphService(this);
                _registeredService = ReferenceEquals(GlobalRegistry.FluidPipeGraph, this);
            }

            if (_registeredService)
                SubmarineElectrolysisModule.BindPipeGraphToActiveModules(this);

            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            if (!_registeredLateFrameTick)
                _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void UnregisterRuntime()
        {
            UnregisterDispatcherTickLanes();

            if (_registeredService)
            {
                SubmarineElectrolysisModule.ClearPipeGraphFromActiveModules(this);
                GlobalRegistry.UnregisterFluidPipeGraphService(this);
                _registeredService = false;
            }
        }

        private void UnregisterDispatcherTickLanes()
        {
            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }
        }

        private bool ScheduleSolve(float deltaTime)
        {
            IDataVault vault = ResolveDataVault();
            if (!TryAcquireSolveWriteBuffers(
                    vault,
                    out uint lockMask,
                    out NativeArray<float> pipePressure,
                    out NativeArray<float> pipeContents,
                    out NativeArray<byte> pipeFlags,
                    out NativeArray<byte> pipeContentKinds,
                    out NativeArray<int> pipeNetworkIds,
                    out NativeArray<int> pipeRoomIndices,
                    out NativeArray<float> pipeCapacities,
                    out NativeArray<float> pipeMaxPressure,
                    out NativeArray<float> pipeFlowRates,
                    out NativeArray<float> pipeSourceRates,
                    out NativeArray<float> pipeDemandRates,
                    out NativeArray<float3> pipeFlowVectors,
                    out NativeArray<float> pipeRoomExchangeContents,
                    out NativeArray<FluidPipeTelemetryEntry> telemetryRing,
                    out NativeArray<FluidPipeRuptureRecord> ruptureTelemetryRing,
                    out NativeArray<int> ruptureBudget,
                    out NativeArray<int> connectionSources,
                    out NativeArray<int> connectionDestinations,
                    out NativeArray<int> connectionOffsets,
                    out NativeArray<int> connectionCsrDestinations,
                    out NativeArray<int> connectionWriteCursor,
                    out NativeArray<FluidPipeRuptureRecord> ruptureDispatch))
            {
                return false;
            }

            ResetRuptureQueueBudget(ruptureBudget);

            JobHandle dependency = default;
            bool topologyDirty = _connectionTopologyDirty;
            int frameIndex = _frameIndex;
            int telemetryIndex = _telemetryCursor;
            FluidPipePressureSolveJob job = new FluidPipePressureSolveJob
            {
                NodeCount = _nodeCount,
                ConnectionCount = _connectionCount,
                FrameIndex = frameIndex,
                TelemetryIndex = telemetryIndex,
                DeltaTime = deltaTime,
                DefaultFlowRate = math.max(0f, defaultPipeFlowRate),
                ConnectionOffsets = connectionOffsets,
                ConnectionCsrDestinations = connectionCsrDestinations,
                PipeContentKinds = pipeContentKinds,
                PipeNetworkIds = pipeNetworkIds,
                PipeRoomIndices = pipeRoomIndices,
                PipeCapacities = pipeCapacities,
                PipeMaxPressure = pipeMaxPressure,
                PipeFlowRates = pipeFlowRates,
                PipeSourceRates = pipeSourceRates,
                PipeDemandRates = pipeDemandRates,
                PipePressure = pipePressure,
                PipeContents = pipeContents,
                PipeFlags = pipeFlags,
                PipeFlowVectors = pipeFlowVectors,
                PipeRoomExchangeContents = pipeRoomExchangeContents,
                TelemetryRing = telemetryRing,
                RuptureTelemetryRing = ruptureTelemetryRing,
                Ruptures = ruptureDispatch,
                RuptureBudget = ruptureBudget
            };

            bool scheduled = false;
            bool buildScheduled = false;
            try
            {
                if (topologyDirty)
                {
                    BuildFluidPipeCsrJob buildJob = new BuildFluidPipeCsrJob
                    {
                        NodeCount = _nodeCount,
                        ConnectionCount = _connectionCount,
                        ConnectionSources = connectionSources,
                        ConnectionDestinations = connectionDestinations,
                        ConnectionOffsets = connectionOffsets,
                        ConnectionCsrDestinations = connectionCsrDestinations,
                        ConnectionWriteCursor = connectionWriteCursor
                    };
                    dependency = buildJob.Schedule();
                    buildScheduled = true;
                }

                _solveHandle = topologyDirty ? job.Schedule(dependency) : job.Schedule();
                _solveLockVault = vault;
                _solveLockMask = lockMask;
                _solveScheduled = true;
                _connectionTopologyDirty = false;
                _frameIndex = frameIndex + 1;
                _telemetryCursor = telemetryIndex + 1;
                scheduled = true;
            }
            finally
            {
                if (!scheduled)
                {
                    if (buildScheduled)
                        DispatcherJobFence.TryComplete(ref dependency, forceComplete: true);

                    ReleaseSolveWriteLocks(vault, lockMask);
                }
            }

            return scheduled;
        }

        private void ResetRuptureQueueBudget(NativeArray<int> ruptureBudget)
        {
            if (!ruptureBudget.IsCreated || ruptureBudget.Length < RuptureBudgetLength)
                return;

            ruptureBudget[0] = math.max(1, math.min(nodeCapacity, math.max(0, _nodeCount)));
            ruptureBudget[1] = 0;
            ruptureBudget[2] = 0;
        }

        private static float ResolveGlobalQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private bool CompleteSolve(bool force)
        {
            if (!_solveScheduled)
                return true;

            uint lockMask = _solveLockMask;
            IDataVault lockVault = _solveLockVault;
            bool completed = false;
            if (!DispatcherJobSwap.TryComplete(ref _solveHandle, force))
                return false;

            try
            {
                _solveScheduled = false;
                completed = true;
                return true;
            }
            finally
            {
                if (completed)
                {
                    ReleaseSolveWriteLocks(lockVault, lockMask);
                    _solveLockVault = null;
                    _solveLockMask = 0u;
                }
            }
        }

        private void ProcessSolvedOutputs()
        {
            if (!_initialized)
                return;

            if (!TryReadOnlyBuffer(in _ruptureDispatchHandle, PipeRuptureDispatchBufferId, nodeCapacity, out NativeArray<FluidPipeRuptureRecord>.ReadOnly ruptureDispatch) ||
                !TryReadOnlyBuffer(in _ruptureQueueBudgetHandle, PipeRuptureBudgetBufferId, RuptureBudgetLength, out NativeArray<int>.ReadOnly ruptureBudget))
            {
                return;
            }

            int ruptureCount = math.clamp(ruptureBudget[1], 0, ruptureDispatch.Length);
            for (int i = 0; i < ruptureCount; i++)
            {
                FluidPipeRuptureRecord rupture = ruptureDispatch[i];
                PublishRuptureSignals(in rupture);
            }

            PublishFlowVisuals();
            ApplyRoomExchangeOutputs();
            FluidPipeTelemetryEntry telemetry = ReadLatestTelemetry();
            _debugLastRuptureCount = ruptureCount;
            _debugLastMaxPressureKPa = telemetry.MaxPressureKPa;
            if (telemetry.NanCount > 0)
                DumpBlackBox();
        }

        private void ApplyPumpInputs(float deltaTime)
        {
            // SHINOBU_340: object/BaseModule pump drainage is retired.
            // SumpPumpPipeGridRuntime drains Fluid Incursion Vault buffers through CSR/Jacobi math.
        }

        private void ApplyElectrolysisInputs(float deltaTime)
        {
            ClearOxygenSourceDemandRates();
            if (HasCriticalOxygenCutoffSignal())
                return;

            int sourceCount = SubmarineElectrolysisModule.ActiveElectrolysisCount;
            if (sourceCount <= 0)
                return;

            float demandScale = math.rcp(math.max(0.001f, deltaTime));
            for (int i = 0; i < sourceCount; i++)
            {
                SubmarineElectrolysisModule source = SubmarineElectrolysisModule.GetActiveElectrolysis(i);
                if (_atmosphereSystem == null || !_atmosphereSystem.IsAtmosphereRuntimeActive)
                {
                    source?.FlushPendingPipeOxygenToAtmosphere();
                    continue;
                }

                if (source == null ||
                    !source.TryConsumePipeOxygenForGraph(this, out int nodeIndex, out float oxygenUnits))
                {
                    continue;
                }

                if (!TryInjectPipeContents(nodeIndex, oxygenUnits))
                {
                    source.RestorePipeOxygen(oxygenUnits);
                    continue;
                }

                TrySetPipeDemandRate(nodeIndex, oxygenUnits * demandScale);
            }
        }

        private static bool HasCriticalOxygenCutoffSignal()
        {
            ReadOnlySpan<OxygenCriticalSignal> signals = SignalBus<OxygenCriticalSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                OxygenCriticalSignal signal = signals[i];
                if (signal.SourceId != OxygenCriticalSignal.SourceBioCablePredatorBite)
                    continue;

                float oxygen01 = math.saturate(math.select(0f, signal.Oxygen01, math.isfinite(signal.Oxygen01)));
                bool criticalCutoff = signal.Severity >= OxygenCriticalSignal.CriticalSeverity &&
                                      (signal.Flags & OxygenCriticalSignal.FlagLifeSupportCutoff) != 0;
                if (criticalCutoff && oxygen01 <= 0.001f)
                    return true;
            }

            return false;
        }

        private void ClearOxygenSourceDemandRates()
        {
            if (!TryReadOnlyBuffer(in _pipeFlagsHandle, PipeFlagsBufferId, nodeCapacity, out NativeArray<byte>.ReadOnly pipeFlags))
            {
                return;
            }

            IDataVault vault = ResolveDataVault();
            uint lockMask = 0u;
            try
            {
                if (!TryAcquireSolveWriteBuffer(vault, in _pipeDemandRatesHandle, nodeCapacity, SolveLockDemandRates, ref lockMask, out NativeArray<float> pipeDemandRates))
                    return;

                for (int i = 0; i < _nodeCount; i++)
                {
                    byte flags = pipeFlags[i];
                    if ((flags & (byte)FluidPipeFlags.OxygenSource) == 0)
                        continue;

                    pipeDemandRates[i] = 0f;
                }
            }
            finally
            {
                ReleaseSolveWriteLocks(vault, lockMask);
            }
        }

        private void ApplyRoomExchangeOutputs()
        {
            if (!TryReadOnlyBuffer(in _pipeRoomExchangeContentsHandle, PipeRoomExchangeContentsBufferId, nodeCapacity, out NativeArray<float>.ReadOnly pipeRoomExchangeContents) ||
                !TryReadOnlyBuffer(in _pipeContentKindsHandle, PipeContentKindsBufferId, nodeCapacity, out NativeArray<byte>.ReadOnly pipeContentKinds) ||
                !TryReadOnlyBuffer(in _pipeRoomIndicesHandle, PipeRoomIndicesBufferId, nodeCapacity, out NativeArray<int>.ReadOnly pipeRoomIndices))
            {
                return;
            }

            for (int i = 0; i < _nodeCount; i++)
            {
                float exchange = pipeRoomExchangeContents[i];
                if (exchange <= 0f)
                    continue;

                byte kind = pipeContentKinds[i];
                if (kind == (byte)FluidPipeContentKind.Oxygen)
                {
                    int roomIndex = pipeRoomIndices[i];
                    if (_atmosphereSystem != null && _atmosphereSystem.IsAtmosphereRuntimeActive && roomIndex >= 0)
                        _atmosphereSystem.InjectOxygenUnits(roomIndex, exchange);
                }
                else if (kind == (byte)FluidPipeContentKind.Water)
                {
                    PublishFluidIncursion(i, exchange);
                }
            }
        }

        private void PublishRuptureSignals(in FluidPipeRuptureRecord rupture)
        {
            AbsoluteUniversePosition aup = default;
            if (IsValidNode(rupture.NodeIndex) &&
                TryReadOnlyBuffer(in _pipeAupsHandle, PipeAupsBufferId, nodeCapacity, out NativeArray<AbsoluteUniversePosition>.ReadOnly pipeAups))
            {
                aup = pipeAups[rupture.NodeIndex];
            }

            short roomIndex = ClampToShort(rupture.RoomIndex);
            PipeRuptureSignal pipeSignal = new PipeRuptureSignal
            {
                RuptureAup = aup,
                NetworkId = (uint)math.max(0, rupture.NetworkId),
                NodeId = (uint)math.max(0, rupture.NodeIndex),
                PressureKPa = rupture.PressureKPa,
                ContentKind = rupture.ContentKind,
                Flags = rupture.Flags,
                RoomIndex = roomIndex
            };
            SignalBus<PipeRuptureSignal>.TryPushTracked(in pipeSignal, ref s_x001FluidPipeGraphRuntimeSignalPushDropCount);

            ImpactSignal impactSignal = new ImpactSignal
            {
                PointAup = aup,
                Force = rupture.PressureKPa,
                Intensity = rupture.Flow01,
                MaterialHash = PipeImpactMaterialHash,
                WeightClass = 1,
                Flags = 1
            };
            SignalBus<ImpactSignal>.TryPushTracked(in impactSignal, ref s_x001FluidPipeGraphRuntimeSignalPushDropCount);
            ConnectionSplineBatchRenderer.SetPipeNodeRuptured((uint)math.max(0, rupture.NodeIndex), true);

            if (rupture.ContentKind == (byte)FluidPipeContentKind.Water)
                PublishFluidIncursion(rupture.NodeIndex, rupture.Contents);
        }

        private void PublishFlowVisuals()
        {
            if (!TryReadOnlyBuffer(in _pipeCapacitiesHandle, PipeCapacitiesBufferId, nodeCapacity, out NativeArray<float>.ReadOnly pipeCapacities) ||
                !TryReadOnlyBuffer(in _pipeFlowVectorsHandle, PipeFlowVectorsBufferId, nodeCapacity, out NativeArray<float3>.ReadOnly pipeFlowVectors))
            {
                return;
            }

            IDataVault vault = ResolveDataVault();
            uint lockMask = 0u;
            try
            {
                if (!TryAcquireSolveWriteBuffer(vault, in _pipeLastVisualFlowHandle, nodeCapacity, SolveLockLastVisualFlow, ref lockMask, out NativeArray<float> pipeLastVisualFlow01))
                    return;

                for (int i = 0; i < _nodeCount; i++)
                {
                    float capacity = math.max(FluidPipeGraphConstants.MinCapacity, pipeCapacities[i]);
                    float flow01 = math.saturate(pipeFlowVectors[i].y * math.rcp(capacity));
                    float previous = pipeLastVisualFlow01[i];
                    if (flow01 <= 0.001f && previous <= 0.001f)
                        continue;
                    if (math.abs(flow01 - previous) <= 0.01f)
                        continue;

                    pipeLastVisualFlow01[i] = flow01;
                    ConnectionSplineBatchRenderer.SetPipeNodeFlow((uint)i, flow01);
                }
            }
            finally
            {
                ReleaseSolveWriteLocks(vault, lockMask);
            }
        }

        private void PublishFluidIncursion(int nodeIndex, float amount)
        {
            if (!IsValidNode(nodeIndex))
                return;

            if (!TryReadOnlyBuffer(in _pipeRoomIndicesHandle, PipeRoomIndicesBufferId, nodeCapacity, out NativeArray<int>.ReadOnly pipeRoomIndices) ||
                !TryReadOnlyBuffer(in _pipeAupsHandle, PipeAupsBufferId, nodeCapacity, out NativeArray<AbsoluteUniversePosition>.ReadOnly pipeAups))
            {
                return;
            }

            int roomIndex = pipeRoomIndices[nodeIndex];
            if (roomIndex < 0)
                return;

            FluidIncursionSignal incursionSignal = new FluidIncursionSignal
            {
                LeakAup = pipeAups[nodeIndex],
                CompartmentId = (uint)math.max(0, roomIndex),
                FloodLevel01 = 0f,
                FlowRate01 = math.saturate(amount),
                Flags = 1
            };
            SignalBus<FluidIncursionSignal>.TryPushTracked(in incursionSignal, ref s_x001FluidPipeGraphRuntimeSignalPushDropCount);
        }

        private FluidPipeTelemetryEntry ReadLatestTelemetry()
        {
            if (!TryReadOnlyBuffer(in _telemetryRingHandle, PipeTelemetryRingBufferId, FluidPipeGraphConstants.BlackBoxFrameCount, out NativeArray<FluidPipeTelemetryEntry>.ReadOnly telemetryRing) ||
                telemetryRing.Length <= 0)
                return default;

            int index = (_telemetryCursor - 1) % telemetryRing.Length;
            if (index < 0)
                index += telemetryRing.Length;
            return telemetryRing[index];
        }

        private void DumpBlackBox()
        {
            if (_blackBoxDumped ||
                !TryReadOnlyBuffer(in _telemetryRingHandle, PipeTelemetryRingBufferId, FluidPipeGraphConstants.BlackBoxFrameCount, out NativeArray<FluidPipeTelemetryEntry>.ReadOnly telemetryRing))
            {
                return;
            }

            bool hasRuptureTelemetry = TryReadOnlyBuffer(
                in _ruptureTelemetryRingHandle,
                PipeRuptureTelemetryRingBufferId,
                FluidPipeGraphConstants.BlackBoxFrameCount,
                out NativeArray<FluidPipeRuptureRecord>.ReadOnly ruptureTelemetryRing);

            int ruptureCount = hasRuptureTelemetry ? ruptureTelemetryRing.Length : 0;
            uint hash = 2166136261u ^ 0x48385049u ^ (uint)telemetryRing.Length ^ ((uint)ruptureCount * 16777619u);
            uint flags = 0u;
            for (int i = 0; i < telemetryRing.Length; i++)
            {
                FluidPipeTelemetryEntry entry = telemetryRing[i];
                if (entry.NanCount > 0)
                    flags |= 1u;
                if (entry.RuptureCount > 0)
                    flags |= 2u;
                hash = MixPipeBlackBoxHash(hash, (uint)entry.FrameIndex);
                hash = MixPipeBlackBoxHash(hash, (uint)entry.NodeCount);
                hash = MixPipeBlackBoxHash(hash, (uint)entry.RuptureCount);
                hash = MixPipeBlackBoxHash(hash, (uint)entry.NanCount);
                hash = MixPipeBlackBoxHash(hash, math.asuint(entry.TotalWater));
                hash = MixPipeBlackBoxHash(hash, math.asuint(entry.TotalOxygen));
                hash = MixPipeBlackBoxHash(hash, math.asuint(entry.MaxPressureKPa));
                hash = MixPipeBlackBoxHash(hash, entry.StateHash);
            }

            if (hasRuptureTelemetry)
            {
                for (int i = 0; i < ruptureTelemetryRing.Length; i++)
                {
                    FluidPipeRuptureRecord rupture = ruptureTelemetryRing[i];
                    hash = MixPipeBlackBoxHash(hash, (uint)rupture.NodeIndex);
                    hash = MixPipeBlackBoxHash(hash, (uint)rupture.NetworkId);
                    hash = MixPipeBlackBoxHash(hash, (uint)rupture.RoomIndex);
                    hash = MixPipeBlackBoxHash(hash, (uint)rupture.FrameIndex);
                    hash = MixPipeBlackBoxHash(hash, math.asuint(rupture.PressureKPa));
                    hash = MixPipeBlackBoxHash(hash, math.asuint(rupture.Contents));
                    hash = MixPipeBlackBoxHash(hash, math.asuint(rupture.Flow01));
                    hash = MixPipeBlackBoxHash(hash, rupture.NodeHash);
                    hash = MixPipeBlackBoxHash(hash, rupture.ContentKind);
                    hash = MixPipeBlackBoxHash(hash, rupture.Flags);
                    hash = MixPipeBlackBoxHash(hash, rupture.Reserved);
                    flags |= rupture.Flags;
                }
            }

            _blackBoxStateHash = hash;
            _blackBoxFlags = flags;
            _blackBoxTelemetryCount = telemetryRing.Length;
            _blackBoxRuptureCount = ruptureCount;
            _blackBoxDumped = true;
        }

        private static uint MixPipeBlackBoxHash(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }

        private bool HasConnection(
            int sourceNodeIndex,
            int destinationNodeIndex,
            NativeArray<int> connectionSources,
            NativeArray<int> connectionDestinations)
        {
            int count = math.max(0, math.min(_connectionCount, math.min(connectionSources.Length, connectionDestinations.Length)));
            for (int i = 0; i < count; i++)
            {
                if (connectionSources[i] == sourceNodeIndex &&
                    connectionDestinations[i] == destinationNodeIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private static float ResolvePressureForNode(
            int nodeIndex,
            float contents,
            NativeArray<float>.ReadOnly pipeCapacities,
            NativeArray<float>.ReadOnly pipeMaxPressure)
        {
            float capacity = math.max(FluidPipeGraphConstants.MinCapacity, pipeCapacities[nodeIndex]);
            float maxPressure = math.max(FluidPipeGraphConstants.MinMaxPressureKPa, pipeMaxPressure[nodeIndex]);
            return math.max(0f, contents) * math.rcp(capacity) * maxPressure;
        }

        private bool IsValidNode(int nodeIndex)
        {
            return nodeIndex >= 0 && nodeIndex < _nodeCount;
        }

        private void ResolveAtmosphereSystem(bool force)
        {
            if (!force && _atmosphereResolveAttempted)
                return;

            if (_atmosphereSystem != null && _atmosphereSystem.IsAtmosphereRuntimeActive)
            {
                _atmosphereResolveAttempted = true;
                return;
            }

            _atmosphereResolveAttempted = true;
            _atmosphereSystem = atmosphereSystemSource as ISubmarineAtmosphereRoomMutationSink;
            if (_atmosphereSystem == null || !_atmosphereSystem.IsAtmosphereRuntimeActive)
                ConstructionParentLookup.TryCaptureSelfOrParent(this, out _atmosphereSystem);
        }

        private static short ClampToShort(int value)
        {
            if (value < short.MinValue)
                return short.MinValue;
            if (value > short.MaxValue)
                return short.MaxValue;
            return (short)value;
        }

        private IDataVault ResolveDataVault()
        {
            return _dataVault;
        }

        private void CacheDataVaultCold()
        {
            IDataVault currentVault = GlobalRegistry.DataVault;
            if (ReferenceEquals(_dataVault, currentVault))
                return;

            if (_dataVault != null)
            {
                CompleteSolve(force: true);
                ReleaseFluidPipeVaultBuffers(_dataVault);
            }
            else
            {
                ReleaseFluidPipeVaultBuffers(null);
            }

            _dataVault = currentVault;
            _initialized = false;
        }

        private bool EnsureFluidPipeVaultBuffers(IDataVault vault)
        {
            return TryEnsureHandle(vault, ref _pipePressureHandle, PipePressureBufferId, nodeCapacity) &&
                   TryEnsureHandle(vault, ref _pipeContentsHandle, PipeContentsBufferId, nodeCapacity) &&
                   TryEnsureHandle(vault, ref _pipeFlagsHandle, PipeFlagsBufferId, nodeCapacity) &&
                   TryEnsureHandle(vault, ref _pipeContentKindsHandle, PipeContentKindsBufferId, nodeCapacity) &&
                   TryEnsureHandle(vault, ref _pipeNetworkIdsHandle, PipeNetworkIdsBufferId, nodeCapacity) &&
                   TryEnsureHandle(vault, ref _pipeRoomIndicesHandle, PipeRoomIndicesBufferId, nodeCapacity) &&
                   TryEnsureHandle(vault, ref _pipeCapacitiesHandle, PipeCapacitiesBufferId, nodeCapacity) &&
                   TryEnsureHandle(vault, ref _pipeMaxPressureHandle, PipeMaxPressureBufferId, nodeCapacity) &&
                   TryEnsureHandle(vault, ref _pipeFlowRatesHandle, PipeFlowRatesBufferId, nodeCapacity) &&
                   TryEnsureHandle(vault, ref _pipeSourceRatesHandle, PipeSourceRatesBufferId, nodeCapacity) &&
                   TryEnsureHandle(vault, ref _pipeDemandRatesHandle, PipeDemandRatesBufferId, nodeCapacity) &&
                   TryEnsureHandle(vault, ref _pipeFlowVectorsHandle, PipeFlowVectorsBufferId, nodeCapacity) &&
                   TryEnsureHandle(vault, ref _pipeRoomExchangeContentsHandle, PipeRoomExchangeContentsBufferId, nodeCapacity) &&
                   TryEnsureHandle(vault, ref _pipeLastVisualFlowHandle, PipeLastVisualFlowBufferId, nodeCapacity) &&
                   TryEnsureHandle(vault, ref _pipeAupsHandle, PipeAupsBufferId, nodeCapacity) &&
                   TryEnsureHandle(vault, ref _telemetryRingHandle, PipeTelemetryRingBufferId, FluidPipeGraphConstants.BlackBoxFrameCount) &&
                   TryEnsureHandle(vault, ref _ruptureTelemetryRingHandle, PipeRuptureTelemetryRingBufferId, FluidPipeGraphConstants.BlackBoxFrameCount) &&
                   TryEnsureHandle(vault, ref _ruptureQueueBudgetHandle, PipeRuptureBudgetBufferId, RuptureBudgetLength) &&
                   TryEnsureHandle(vault, ref _pipeConnectionSourcesHandle, PipeConnectionSourcesBufferId, connectionCapacity) &&
                   TryEnsureHandle(vault, ref _pipeConnectionDestinationsHandle, PipeConnectionDestinationsBufferId, connectionCapacity) &&
                   TryEnsureHandle(vault, ref _pipeConnectionOffsetsHandle, PipeConnectionOffsetsBufferId, nodeCapacity + 1) &&
                   TryEnsureHandle(vault, ref _pipeConnectionCsrDestinationsHandle, PipeConnectionCsrDestinationsBufferId, connectionCapacity) &&
                   TryEnsureHandle(vault, ref _pipeConnectionWriteCursorHandle, PipeConnectionWriteCursorBufferId, nodeCapacity) &&
                   TryEnsureHandle(vault, ref _ruptureDispatchHandle, PipeRuptureDispatchBufferId, nodeCapacity);
        }

        private void ReleaseFluidPipeVaultBuffers(IDataVault vault)
        {
            if (vault != null)
            {
                ReleaseBuffer(vault, ref _pipePressureHandle, PipePressureBufferId);
                ReleaseBuffer(vault, ref _pipeContentsHandle, PipeContentsBufferId);
                ReleaseBuffer(vault, ref _pipeFlagsHandle, PipeFlagsBufferId);
                ReleaseBuffer(vault, ref _pipeContentKindsHandle, PipeContentKindsBufferId);
                ReleaseBuffer(vault, ref _pipeNetworkIdsHandle, PipeNetworkIdsBufferId);
                ReleaseBuffer(vault, ref _pipeRoomIndicesHandle, PipeRoomIndicesBufferId);
                ReleaseBuffer(vault, ref _pipeCapacitiesHandle, PipeCapacitiesBufferId);
                ReleaseBuffer(vault, ref _pipeMaxPressureHandle, PipeMaxPressureBufferId);
                ReleaseBuffer(vault, ref _pipeFlowRatesHandle, PipeFlowRatesBufferId);
                ReleaseBuffer(vault, ref _pipeSourceRatesHandle, PipeSourceRatesBufferId);
                ReleaseBuffer(vault, ref _pipeDemandRatesHandle, PipeDemandRatesBufferId);
                ReleaseBuffer(vault, ref _pipeFlowVectorsHandle, PipeFlowVectorsBufferId);
                ReleaseBuffer(vault, ref _pipeRoomExchangeContentsHandle, PipeRoomExchangeContentsBufferId);
                ReleaseBuffer(vault, ref _pipeLastVisualFlowHandle, PipeLastVisualFlowBufferId);
                ReleaseBuffer(vault, ref _pipeAupsHandle, PipeAupsBufferId);
                ReleaseBuffer(vault, ref _telemetryRingHandle, PipeTelemetryRingBufferId);
                ReleaseBuffer(vault, ref _ruptureTelemetryRingHandle, PipeRuptureTelemetryRingBufferId);
                ReleaseBuffer(vault, ref _ruptureQueueBudgetHandle, PipeRuptureBudgetBufferId);
                ReleaseBuffer(vault, ref _pipeConnectionSourcesHandle, PipeConnectionSourcesBufferId);
                ReleaseBuffer(vault, ref _pipeConnectionDestinationsHandle, PipeConnectionDestinationsBufferId);
                ReleaseBuffer(vault, ref _pipeConnectionOffsetsHandle, PipeConnectionOffsetsBufferId);
                ReleaseBuffer(vault, ref _pipeConnectionCsrDestinationsHandle, PipeConnectionCsrDestinationsBufferId);
                ReleaseBuffer(vault, ref _pipeConnectionWriteCursorHandle, PipeConnectionWriteCursorBufferId);
                ReleaseBuffer(vault, ref _ruptureDispatchHandle, PipeRuptureDispatchBufferId);
                return;
            }

            _pipePressureHandle = default;
            _pipeContentsHandle = default;
            _pipeFlagsHandle = default;
            _pipeContentKindsHandle = default;
            _pipeNetworkIdsHandle = default;
            _pipeRoomIndicesHandle = default;
            _pipeCapacitiesHandle = default;
            _pipeMaxPressureHandle = default;
            _pipeFlowRatesHandle = default;
            _pipeSourceRatesHandle = default;
            _pipeDemandRatesHandle = default;
            _pipeFlowVectorsHandle = default;
            _pipeRoomExchangeContentsHandle = default;
            _pipeLastVisualFlowHandle = default;
            _pipeAupsHandle = default;
            _telemetryRingHandle = default;
            _ruptureTelemetryRingHandle = default;
            _ruptureQueueBudgetHandle = default;
            _pipeConnectionSourcesHandle = default;
            _pipeConnectionDestinationsHandle = default;
            _pipeConnectionOffsetsHandle = default;
            _pipeConnectionCsrDestinationsHandle = default;
            _pipeConnectionWriteCursorHandle = default;
            _ruptureDispatchHandle = default;
        }

        private bool TryAcquireSolveWriteBuffers(
            IDataVault vault,
            out uint lockMask,
            out NativeArray<float> pipePressure,
            out NativeArray<float> pipeContents,
            out NativeArray<byte> pipeFlags,
            out NativeArray<byte> pipeContentKinds,
            out NativeArray<int> pipeNetworkIds,
            out NativeArray<int> pipeRoomIndices,
            out NativeArray<float> pipeCapacities,
            out NativeArray<float> pipeMaxPressure,
            out NativeArray<float> pipeFlowRates,
            out NativeArray<float> pipeSourceRates,
            out NativeArray<float> pipeDemandRates,
            out NativeArray<float3> pipeFlowVectors,
            out NativeArray<float> pipeRoomExchangeContents,
            out NativeArray<FluidPipeTelemetryEntry> telemetryRing,
            out NativeArray<FluidPipeRuptureRecord> ruptureTelemetryRing,
            out NativeArray<int> ruptureBudget,
            out NativeArray<int> connectionSources,
            out NativeArray<int> connectionDestinations,
            out NativeArray<int> connectionOffsets,
            out NativeArray<int> connectionCsrDestinations,
            out NativeArray<int> connectionWriteCursor,
            out NativeArray<FluidPipeRuptureRecord> ruptureDispatch)
        {
            lockMask = 0u;
            pipePressure = default;
            pipeContents = default;
            pipeFlags = default;
            pipeContentKinds = default;
            pipeNetworkIds = default;
            pipeRoomIndices = default;
            pipeCapacities = default;
            pipeMaxPressure = default;
            pipeFlowRates = default;
            pipeSourceRates = default;
            pipeDemandRates = default;
            pipeFlowVectors = default;
            pipeRoomExchangeContents = default;
            telemetryRing = default;
            ruptureTelemetryRing = default;
            ruptureBudget = default;
            connectionSources = default;
            connectionDestinations = default;
            connectionOffsets = default;
            connectionCsrDestinations = default;
            connectionWriteCursor = default;
            ruptureDispatch = default;

            if (!AreFluidPipeVaultBuffersReady(vault))
                return false;

            bool acquired = false;
            try
            {
                if (!TryAcquireSolveWriteBuffer(vault, in _pipePressureHandle, nodeCapacity, SolveLockPressure, ref lockMask, out pipePressure) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeContentsHandle, nodeCapacity, SolveLockContents, ref lockMask, out pipeContents) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeFlagsHandle, nodeCapacity, SolveLockFlags, ref lockMask, out pipeFlags) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeContentKindsHandle, nodeCapacity, SolveLockContentKinds, ref lockMask, out pipeContentKinds) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeNetworkIdsHandle, nodeCapacity, SolveLockNetworkIds, ref lockMask, out pipeNetworkIds) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeRoomIndicesHandle, nodeCapacity, SolveLockRoomIndices, ref lockMask, out pipeRoomIndices) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeCapacitiesHandle, nodeCapacity, SolveLockCapacities, ref lockMask, out pipeCapacities) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeMaxPressureHandle, nodeCapacity, SolveLockMaxPressure, ref lockMask, out pipeMaxPressure) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeFlowRatesHandle, nodeCapacity, SolveLockFlowRates, ref lockMask, out pipeFlowRates) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeSourceRatesHandle, nodeCapacity, SolveLockSourceRates, ref lockMask, out pipeSourceRates) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeDemandRatesHandle, nodeCapacity, SolveLockDemandRates, ref lockMask, out pipeDemandRates) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeFlowVectorsHandle, nodeCapacity, SolveLockFlowVectors, ref lockMask, out pipeFlowVectors) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeRoomExchangeContentsHandle, nodeCapacity, SolveLockRoomExchange, ref lockMask, out pipeRoomExchangeContents) ||
                    !TryAcquireSolveWriteBuffer(vault, in _telemetryRingHandle, FluidPipeGraphConstants.BlackBoxFrameCount, SolveLockTelemetry, ref lockMask, out telemetryRing) ||
                    !TryAcquireSolveWriteBuffer(vault, in _ruptureTelemetryRingHandle, FluidPipeGraphConstants.BlackBoxFrameCount, SolveLockRuptureTelemetry, ref lockMask, out ruptureTelemetryRing) ||
                    !TryAcquireSolveWriteBuffer(vault, in _ruptureQueueBudgetHandle, RuptureBudgetLength, SolveLockRuptureBudget, ref lockMask, out ruptureBudget) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeConnectionSourcesHandle, connectionCapacity, SolveLockConnectionSources, ref lockMask, out connectionSources) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeConnectionDestinationsHandle, connectionCapacity, SolveLockConnectionDestinations, ref lockMask, out connectionDestinations) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeConnectionOffsetsHandle, nodeCapacity + 1, SolveLockConnectionOffsets, ref lockMask, out connectionOffsets) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeConnectionCsrDestinationsHandle, connectionCapacity, SolveLockConnectionCsrDestinations, ref lockMask, out connectionCsrDestinations) ||
                    !TryAcquireSolveWriteBuffer(vault, in _pipeConnectionWriteCursorHandle, nodeCapacity, SolveLockConnectionWriteCursor, ref lockMask, out connectionWriteCursor) ||
                    !TryAcquireSolveWriteBuffer(vault, in _ruptureDispatchHandle, nodeCapacity, SolveLockRuptureDispatch, ref lockMask, out ruptureDispatch))
                {
                    return false;
                }

                acquired = true;
                return true;
            }
            finally
            {
                if (!acquired)
                {
                    ReleaseSolveWriteLocks(vault, lockMask);
                    lockMask = 0u;
                }
            }
        }

        private static bool TryAcquireSolveWriteBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            uint bit,
            ref uint lockMask,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !TryResolveSolveLockBufferId(bit, out BufferID expectedBufferId) ||
                !IsFluidPipeVaultHandle(in handle, expectedBufferId))
            {
                return false;
            }

            if ((lockMask & bit) != 0u ||
                !vault.TryLockBuffer(expectedBufferId, OwnerSystemId))
            {
                return false;
            }

            bool releaseOnFailure = true;
            try
            {
                if (vault.TryResolveHandle(in handle, out buffer) &&
                    buffer.IsCreated &&
                    buffer.Length >= requiredLength)
                {
                    lockMask |= bit;
                    releaseOnFailure = false;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (releaseOnFailure)
                    vault.TryUnlockBuffer(expectedBufferId, OwnerSystemId);
            }
        }

        private void ReleaseSolveWriteLocks(IDataVault vault, uint lockMask)
        {
            if (vault == null || lockMask == 0u)
                return;

            uint remainingLocks = lockMask;
            while (remainingLocks != 0u)
            {
                uint bit = remainingLocks & (~remainingLocks + 1u);
                remainingLocks &= ~bit;
                if (TryResolveSolveLockBufferId(bit, out BufferID bufferId))
                    vault.TryUnlockBuffer(bufferId, OwnerSystemId);
            }
        }

        private bool TryReadOnlyBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = ResolveDataVault();
            return vault != null &&
                   requiredLength > 0 &&
                   IsFluidPipeVaultHandle(in handle, expectedBufferId) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.Length >= requiredLength;
        }

        private bool AreFluidPipeVaultBuffersReady(IDataVault vault)
        {
            return vault != null &&
                   HasFluidPipeBuffer(vault, in _pipePressureHandle, PipePressureBufferId, nodeCapacity) &&
                   HasFluidPipeBuffer(vault, in _pipeContentsHandle, PipeContentsBufferId, nodeCapacity) &&
                   HasFluidPipeBuffer(vault, in _pipeFlagsHandle, PipeFlagsBufferId, nodeCapacity) &&
                   HasFluidPipeBuffer(vault, in _pipeContentKindsHandle, PipeContentKindsBufferId, nodeCapacity) &&
                   HasFluidPipeBuffer(vault, in _pipeNetworkIdsHandle, PipeNetworkIdsBufferId, nodeCapacity) &&
                   HasFluidPipeBuffer(vault, in _pipeRoomIndicesHandle, PipeRoomIndicesBufferId, nodeCapacity) &&
                   HasFluidPipeBuffer(vault, in _pipeCapacitiesHandle, PipeCapacitiesBufferId, nodeCapacity) &&
                   HasFluidPipeBuffer(vault, in _pipeMaxPressureHandle, PipeMaxPressureBufferId, nodeCapacity) &&
                   HasFluidPipeBuffer(vault, in _pipeFlowRatesHandle, PipeFlowRatesBufferId, nodeCapacity) &&
                   HasFluidPipeBuffer(vault, in _pipeSourceRatesHandle, PipeSourceRatesBufferId, nodeCapacity) &&
                   HasFluidPipeBuffer(vault, in _pipeDemandRatesHandle, PipeDemandRatesBufferId, nodeCapacity) &&
                   HasFluidPipeBuffer(vault, in _pipeFlowVectorsHandle, PipeFlowVectorsBufferId, nodeCapacity) &&
                   HasFluidPipeBuffer(vault, in _pipeRoomExchangeContentsHandle, PipeRoomExchangeContentsBufferId, nodeCapacity) &&
                   HasFluidPipeBuffer(vault, in _telemetryRingHandle, PipeTelemetryRingBufferId, FluidPipeGraphConstants.BlackBoxFrameCount) &&
                   HasFluidPipeBuffer(vault, in _ruptureTelemetryRingHandle, PipeRuptureTelemetryRingBufferId, FluidPipeGraphConstants.BlackBoxFrameCount) &&
                   HasFluidPipeBuffer(vault, in _ruptureQueueBudgetHandle, PipeRuptureBudgetBufferId, RuptureBudgetLength) &&
                   HasFluidPipeBuffer(vault, in _pipeConnectionSourcesHandle, PipeConnectionSourcesBufferId, connectionCapacity) &&
                   HasFluidPipeBuffer(vault, in _pipeConnectionDestinationsHandle, PipeConnectionDestinationsBufferId, connectionCapacity) &&
                   HasFluidPipeBuffer(vault, in _pipeConnectionOffsetsHandle, PipeConnectionOffsetsBufferId, nodeCapacity + 1) &&
                   HasFluidPipeBuffer(vault, in _pipeConnectionCsrDestinationsHandle, PipeConnectionCsrDestinationsBufferId, connectionCapacity) &&
                   HasFluidPipeBuffer(vault, in _pipeConnectionWriteCursorHandle, PipeConnectionWriteCursorBufferId, nodeCapacity) &&
                   HasFluidPipeBuffer(vault, in _ruptureDispatchHandle, PipeRuptureDispatchBufferId, nodeCapacity);
        }

        private static bool HasFluidPipeBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            int safeLength = math.max(1, requiredLength);
            return IsFluidPipeVaultHandle(in handle, bufferId) &&
                   vault.TryReadHandle(in handle, out NativeArray<T> existing) &&
                   existing.IsCreated &&
                   existing.Length >= safeLength;
        }

        private static bool TryEnsureHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            int safeLength = math.max(1, requiredLength);
            if (IsFluidPipeVaultHandle(in handle, bufferId) &&
                vault.TryReadHandle(in handle, out NativeArray<T> existing) &&
                existing.IsCreated &&
                existing.Length >= safeLength)
            {
                return true;
            }

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existingHandle) &&
                IsFluidPipeVaultHandle(in existingHandle, bufferId))
            {
                handle = existingHandle;
                if (vault.TryReadHandle(in handle, out existing) &&
                    existing.IsCreated &&
                    existing.Length >= safeLength)
                {
                    return true;
                }
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                safeLength,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            return IsFluidPipeVaultHandle(in handle, bufferId) &&
                   vault.TryReadHandle(in handle, out existing) &&
                   existing.IsCreated &&
                   existing.Length >= safeLength;
        }

        private static void ReleaseBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            if (IsFluidPipeVaultHandle(in handle, expectedBufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsFluidPipeVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == (uint)OwnerSystemId &&
                   handle.Generation != 0u;
        }

        private static bool TryResolveSolveLockBufferId(uint bit, out BufferID bufferId)
        {
            switch (bit)
            {
                case SolveLockPressure: bufferId = PipePressureBufferId; return true;
                case SolveLockContents: bufferId = PipeContentsBufferId; return true;
                case SolveLockFlags: bufferId = PipeFlagsBufferId; return true;
                case SolveLockContentKinds: bufferId = PipeContentKindsBufferId; return true;
                case SolveLockNetworkIds: bufferId = PipeNetworkIdsBufferId; return true;
                case SolveLockRoomIndices: bufferId = PipeRoomIndicesBufferId; return true;
                case SolveLockCapacities: bufferId = PipeCapacitiesBufferId; return true;
                case SolveLockMaxPressure: bufferId = PipeMaxPressureBufferId; return true;
                case SolveLockFlowRates: bufferId = PipeFlowRatesBufferId; return true;
                case SolveLockSourceRates: bufferId = PipeSourceRatesBufferId; return true;
                case SolveLockDemandRates: bufferId = PipeDemandRatesBufferId; return true;
                case SolveLockFlowVectors: bufferId = PipeFlowVectorsBufferId; return true;
                case SolveLockRoomExchange: bufferId = PipeRoomExchangeContentsBufferId; return true;
                case SolveLockTelemetry: bufferId = PipeTelemetryRingBufferId; return true;
                case SolveLockRuptureTelemetry: bufferId = PipeRuptureTelemetryRingBufferId; return true;
                case SolveLockRuptureBudget: bufferId = PipeRuptureBudgetBufferId; return true;
                case SolveLockConnectionSources: bufferId = PipeConnectionSourcesBufferId; return true;
                case SolveLockConnectionDestinations: bufferId = PipeConnectionDestinationsBufferId; return true;
                case SolveLockRuptureDispatch: bufferId = PipeRuptureDispatchBufferId; return true;
                case SolveLockLastVisualFlow: bufferId = PipeLastVisualFlowBufferId; return true;
                case SolveLockAups: bufferId = PipeAupsBufferId; return true;
                case SolveLockConnectionOffsets: bufferId = PipeConnectionOffsetsBufferId; return true;
                case SolveLockConnectionCsrDestinations: bufferId = PipeConnectionCsrDestinationsBufferId; return true;
                case SolveLockConnectionWriteCursor: bufferId = PipeConnectionWriteCursorBufferId; return true;
                default: bufferId = default; return false;
            }
        }
    }
}
