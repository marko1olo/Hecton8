using System;
using System.IO;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Logistics;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Construction/Fluid Pipe Graph Runtime")]
    public sealed class FluidPipeGraphRuntime : MonoBehaviour, IFluidPipeGraphService, ISlowTickable, ILateFrameTickable, IServiceShutdown
    {
        private const string NativeMemoryOwner = nameof(FluidPipeGraphRuntime);
        private const float SlowTickStepSeconds = 0.1f;
        private const uint PipeImpactMaterialHash = 0x50495045u;

        [Header("Graph")]
        [SerializeField, Min(16)] private int nodeCapacity = 512;
        [SerializeField, Min(16)] private int connectionCapacity = 1024;
        [SerializeField, Min(0.001f)] private float defaultPipeFlowRate = FluidPipeGraphConstants.DefaultFlowRate;

        [Header("Integration")]
        [SerializeField] private SubmarineAtmosphereSystem atmosphereSystem;

        [Header("Diagnostics")]
        [SerializeField] private int _debugNodeCount;
        [SerializeField] private int _debugLastRuptureCount;
        [SerializeField] private float _debugLastMaxPressureKPa;

        private NativeArray<float> _pipePressure;
        private NativeArray<float> _pipeContents;
        private NativeArray<byte> _pipeFlags;
        private NativeArray<byte> _pipeContentKinds;
        private NativeArray<int> _pipeNetworkIds;
        private NativeArray<int> _pipeRoomIndices;
        private NativeArray<float> _pipeCapacities;
        private NativeArray<float> _pipeMaxPressure;
        private NativeArray<float> _pipeFlowRates;
        private NativeArray<float> _pipeSourceRates;
        private NativeArray<float> _pipeDemandRates;
        private NativeArray<float3> _pipeFlowVectors;
        private NativeArray<float> _pipeRoomExchangeContents;
        private NativeArray<float> _pipeLastVisualFlow01;
        private NativeArray<AbsoluteUniversePosition> _pipeAups;
        private NativeArray<FluidPipeTelemetryEntry> _telemetryRing;
        private NativeArray<FluidPipeRuptureRecord> _ruptureTelemetryRing;
        private NativeParallelMultiHashMap<int, int> _pipeConnections;
        private NativeQueue<FluidPipeRuptureRecord> _ruptureQueue;

        private JobHandle _solveHandle;
        private bool _solveScheduled;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private bool _registeredService;
        private bool _initialized;
        private bool _atmosphereResolveAttempted;
        private bool _blackBoxDumped;
        private int _nodeCount;
        private int _connectionCount;
        private int _frameIndex;
        private int _telemetryCursor;
        private float _solveAccumulator;

        public bool IsInitialized => _initialized;
        public int PipeNodeCount => _nodeCount;

        private void Awake()
        {
            ResolveAtmosphereSystem(force: true);
            EnsureNativeState();
        }

        private void OnEnable()
        {
            ResolveAtmosphereSystem(force: true);
            EnsureNativeState();
            RegisterRuntime();
        }

        private void OnDisable()
        {
            UnregisterRuntime();
            CompleteSolve(force: true);
        }

        private void OnDestroy()
        {
            DisposeNativeState();
        }

        public void OnServiceShutdown()
        {
            UnregisterRuntime();
            DisposeNativeState();
        }

        public void SlowTick()
        {
            if (!_initialized)
                return;

            _solveAccumulator += SlowTickStepSeconds;
            float cadence = FluidPipeGraphConstants.ResolveCadenceSeconds(ResolveMathLod());
            if (_solveAccumulator + 0.0001f < cadence)
                return;

            if (_solveScheduled)
                return;

            float deltaTime = _solveAccumulator;
            _solveAccumulator = 0f;
            ApplyPumpInputs(deltaTime);
            ApplyElectrolysisInputs(deltaTime);
            ScheduleSolve(deltaTime);
        }

        public void LateFrameTick()
        {
            if (!CompleteSolve(force: false))
                return;

            ProcessSolvedOutputs();
        }

        public bool TryReadPipeNode(int nodeIndex, out float pressureKPa, out float contents, out byte flags)
        {
            if (!_initialized || _solveScheduled || nodeIndex < 0 || nodeIndex >= _nodeCount)
            {
                pressureKPa = 0f;
                contents = 0f;
                flags = 0;
                return false;
            }

            pressureKPa = _pipePressure[nodeIndex];
            contents = _pipeContents[nodeIndex];
            flags = _pipeFlags[nodeIndex];
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
            if (_solveScheduled || _nodeCount >= nodeCapacity)
            {
                nodeIndex = -1;
                return false;
            }

            nodeIndex = _nodeCount++;
            _debugNodeCount = _nodeCount;
            _pipePressure[nodeIndex] = 0f;
            _pipeContents[nodeIndex] = 0f;
            _pipeFlags[nodeIndex] = (byte)FluidPipeFlags.Active;
            _pipeContentKinds[nodeIndex] = contentKind;
            _pipeNetworkIds[nodeIndex] = networkId;
            _pipeRoomIndices[nodeIndex] = roomIndex;
            _pipeCapacities[nodeIndex] = math.max(FluidPipeGraphConstants.MinCapacity, capacity);
            _pipeMaxPressure[nodeIndex] = math.max(FluidPipeGraphConstants.MinMaxPressureKPa, maxPressureKPa);
            _pipeFlowRates[nodeIndex] = math.max(0f, defaultPipeFlowRate);
            _pipeSourceRates[nodeIndex] = 0f;
            _pipeDemandRates[nodeIndex] = 0f;
            _pipeFlowVectors[nodeIndex] = default;
            _pipeRoomExchangeContents[nodeIndex] = 0f;
            _pipeLastVisualFlow01[nodeIndex] = 0f;
            _pipeAups[nodeIndex] = nodeAup;
            return true;
        }

        public bool TryConnectPipeNodes(int sourceNodeIndex, int destinationNodeIndex)
        {
            if (!_initialized ||
                _solveScheduled ||
                !IsValidNode(sourceNodeIndex) ||
                !IsValidNode(destinationNodeIndex) ||
                sourceNodeIndex == destinationNodeIndex ||
                _pipeNetworkIds[sourceNodeIndex] != _pipeNetworkIds[destinationNodeIndex] ||
                _pipeContentKinds[sourceNodeIndex] != _pipeContentKinds[destinationNodeIndex])
            {
                return false;
            }

            int neededConnections = 0;
            bool hasForward = HasConnection(sourceNodeIndex, destinationNodeIndex);
            bool hasReverse = HasConnection(destinationNodeIndex, sourceNodeIndex);
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
                _pipeConnections.Add(sourceNodeIndex, destinationNodeIndex);
                _connectionCount++;
            }

            if (!hasReverse)
            {
                _pipeConnections.Add(destinationNodeIndex, sourceNodeIndex);
                _connectionCount++;
            }

            return true;
        }

        public bool TryInjectPipeContents(int nodeIndex, float contents)
        {
            if (!_initialized || _solveScheduled || !IsValidNode(nodeIndex) || !math.isfinite(contents) || contents <= 0f)
                return false;

            float nextContents = _pipeContents[nodeIndex] + contents;
            if (!math.isfinite(nextContents))
                return false;

            _pipeContents[nodeIndex] = nextContents;
            _pipePressure[nodeIndex] = ResolvePressureForNode(nodeIndex, _pipeContents[nodeIndex]);
            return true;
        }

        public bool TrySetPipeSourceRate(int nodeIndex, float contentsPerSecond)
        {
            if (!_initialized || _solveScheduled || !IsValidNode(nodeIndex) || !math.isfinite(contentsPerSecond))
                return false;

            _pipeSourceRates[nodeIndex] = math.max(0f, contentsPerSecond);
            return true;
        }

        public bool TrySetPipeDemandRate(int nodeIndex, float contentsPerSecond)
        {
            if (!_initialized || _solveScheduled || !IsValidNode(nodeIndex) || !math.isfinite(contentsPerSecond))
                return false;

            _pipeDemandRates[nodeIndex] = math.max(0f, contentsPerSecond);
            return true;
        }

        public bool TrySetPipeNodeFlags(int nodeIndex, byte setMask, byte clearMask)
        {
            if (!_initialized || _solveScheduled || !IsValidNode(nodeIndex))
                return false;

            byte flags = _pipeFlags[nodeIndex];
            flags = (byte)((flags | setMask) & ~clearMask);
            _pipeFlags[nodeIndex] = flags;
            return true;
        }

        private void EnsureNativeState()
        {
            if (_initialized)
                return;

            nodeCapacity = math.max(16, nodeCapacity);
            connectionCapacity = math.max(16, connectionCapacity);
            _pipePressure = new NativeArray<float>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _pipeContents = new NativeArray<float>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _pipeFlags = new NativeArray<byte>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _pipeContentKinds = new NativeArray<byte>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _pipeNetworkIds = new NativeArray<int>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _pipeRoomIndices = new NativeArray<int>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _pipeCapacities = new NativeArray<float>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _pipeMaxPressure = new NativeArray<float>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _pipeFlowRates = new NativeArray<float>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _pipeSourceRates = new NativeArray<float>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _pipeDemandRates = new NativeArray<float>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _pipeFlowVectors = new NativeArray<float3>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _pipeRoomExchangeContents = new NativeArray<float>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _pipeLastVisualFlow01 = new NativeArray<float>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _pipeAups = new NativeArray<AbsoluteUniversePosition>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _telemetryRing = new NativeArray<FluidPipeTelemetryEntry>(FluidPipeGraphConstants.BlackBoxFrameCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _ruptureTelemetryRing = new NativeArray<FluidPipeRuptureRecord>(FluidPipeGraphConstants.BlackBoxFrameCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _pipeConnections = new NativeParallelMultiHashMap<int, int>(connectionCapacity, Allocator.Persistent);
            _ruptureQueue = new NativeQueue<FluidPipeRuptureRecord>(Allocator.Persistent);

            RegisterNativeMemory();
            _initialized = true;
        }

        private void RegisterNativeMemory()
        {
            NativeMemorySentinel.RegisterNativeArray(_pipePressure, NativeMemoryOwner, nameof(_pipePressure), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_pipeContents, NativeMemoryOwner, nameof(_pipeContents), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_pipeFlags, NativeMemoryOwner, nameof(_pipeFlags), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_pipeContentKinds, NativeMemoryOwner, nameof(_pipeContentKinds), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_pipeNetworkIds, NativeMemoryOwner, nameof(_pipeNetworkIds), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_pipeRoomIndices, NativeMemoryOwner, nameof(_pipeRoomIndices), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_pipeCapacities, NativeMemoryOwner, nameof(_pipeCapacities), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_pipeMaxPressure, NativeMemoryOwner, nameof(_pipeMaxPressure), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_pipeFlowRates, NativeMemoryOwner, nameof(_pipeFlowRates), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_pipeSourceRates, NativeMemoryOwner, nameof(_pipeSourceRates), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_pipeDemandRates, NativeMemoryOwner, nameof(_pipeDemandRates), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_pipeFlowVectors, NativeMemoryOwner, nameof(_pipeFlowVectors), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_pipeRoomExchangeContents, NativeMemoryOwner, nameof(_pipeRoomExchangeContents), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_pipeLastVisualFlow01, NativeMemoryOwner, nameof(_pipeLastVisualFlow01), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_pipeAups, NativeMemoryOwner, nameof(_pipeAups), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_telemetryRing, NativeMemoryOwner, nameof(_telemetryRing), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_ruptureTelemetryRing, NativeMemoryOwner, nameof(_ruptureTelemetryRing), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeParallelMultiHashMap(_pipeConnections, NativeMemoryOwner, nameof(_pipeConnections), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeQueue(_ruptureQueue, nodeCapacity, NativeMemoryOwner, nameof(_ruptureQueue), NativeAllocationLifetime.Scene);
        }

        private void DisposeNativeState()
        {
            CompleteSolve(force: true);
            UnregisterRuntime();

            DisposeArray(ref _pipePressure);
            DisposeArray(ref _pipeContents);
            DisposeArray(ref _pipeFlags);
            DisposeArray(ref _pipeContentKinds);
            DisposeArray(ref _pipeNetworkIds);
            DisposeArray(ref _pipeRoomIndices);
            DisposeArray(ref _pipeCapacities);
            DisposeArray(ref _pipeMaxPressure);
            DisposeArray(ref _pipeFlowRates);
            DisposeArray(ref _pipeSourceRates);
            DisposeArray(ref _pipeDemandRates);
            DisposeArray(ref _pipeFlowVectors);
            DisposeArray(ref _pipeRoomExchangeContents);
            DisposeArray(ref _pipeLastVisualFlow01);
            DisposeArray(ref _pipeAups);
            DisposeArray(ref _telemetryRing);
            DisposeArray(ref _ruptureTelemetryRing);

            if (_pipeConnections.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelMultiHashMap(NativeMemoryOwner, nameof(_pipeConnections));
                _pipeConnections.Dispose();
                _pipeConnections = default;
            }

            if (_ruptureQueue.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(NativeMemoryOwner, nameof(_ruptureQueue));
                _ruptureQueue.Dispose();
                _ruptureQueue = default;
            }

            _initialized = false;
            _nodeCount = 0;
            _connectionCount = 0;
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

        private void UnregisterRuntime()
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

            if (_registeredService)
            {
                SubmarineElectrolysisModule.ClearPipeGraphFromActiveModules(this);
                GlobalRegistry.UnregisterFluidPipeGraphService(this);
                _registeredService = false;
            }
        }

        private void ScheduleSolve(float deltaTime)
        {
            if (_ruptureQueue.IsCreated)
                _ruptureQueue.Clear();

            FluidPipePressureSolveJob job = new FluidPipePressureSolveJob
            {
                NodeCount = _nodeCount,
                FrameIndex = _frameIndex++,
                TelemetryIndex = _telemetryCursor++,
                DeltaTime = deltaTime,
                DefaultFlowRate = math.max(0f, defaultPipeFlowRate),
                Connections = _pipeConnections,
                PipeContentKinds = _pipeContentKinds,
                PipeNetworkIds = _pipeNetworkIds,
                PipeRoomIndices = _pipeRoomIndices,
                PipeCapacities = _pipeCapacities,
                PipeMaxPressure = _pipeMaxPressure,
                PipeFlowRates = _pipeFlowRates,
                PipeSourceRates = _pipeSourceRates,
                PipeDemandRates = _pipeDemandRates,
                PipePressure = _pipePressure,
                PipeContents = _pipeContents,
                PipeFlags = _pipeFlags,
                PipeFlowVectors = _pipeFlowVectors,
                PipeRoomExchangeContents = _pipeRoomExchangeContents,
                TelemetryRing = _telemetryRing,
                RuptureTelemetryRing = _ruptureTelemetryRing,
                Ruptures = _ruptureQueue.AsParallelWriter()
            };

            _solveHandle = job.Schedule();
            _solveScheduled = true;
        }

        private bool CompleteSolve(bool force)
        {
            if (!_solveScheduled)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _solveHandle, force))
                return false;

            _solveScheduled = false;
            return true;
        }

        private void ProcessSolvedOutputs()
        {
            if (!_initialized)
                return;

            int ruptureCount = 0;
            while (_ruptureQueue.IsCreated && _ruptureQueue.TryDequeue(out FluidPipeRuptureRecord rupture))
            {
                ruptureCount++;
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
            int pumpCount = WaterPumpModule.ActivePumpCount;
            for (int i = 0; i < pumpCount; i++)
            {
                WaterPumpModule pump = WaterPumpModule.GetActivePump(i);
                BaseModule host = pump != null ? pump.HostModule : null;
                if (pump == null || host == null || !pump.CanPump || host.WaterVolumeM3 <= 0f)
                    continue;

                if (!pump.TryEnsureWaterPipeNode(this, out int waterNode))
                    continue;

                float budget = pump.ResolveDrainBudgetM3(deltaTime);
                if (budget <= 0f)
                    continue;

                float drained = host.DrainWaterVolumeM3(budget);
                if (drained > 0f)
                    TryInjectPipeContents(waterNode, drained);
            }
        }

        private void ApplyElectrolysisInputs(float deltaTime)
        {
            ClearOxygenSourceDemandRates();

            int sourceCount = SubmarineElectrolysisModule.ActiveElectrolysisCount;
            if (sourceCount <= 0)
                return;

            float demandScale = math.rcp(math.max(0.001f, deltaTime));
            for (int i = 0; i < sourceCount; i++)
            {
                SubmarineElectrolysisModule source = SubmarineElectrolysisModule.GetActiveElectrolysis(i);
                if (atmosphereSystem == null)
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

        private void ClearOxygenSourceDemandRates()
        {
            for (int i = 0; i < _nodeCount; i++)
            {
                byte flags = _pipeFlags[i];
                if ((flags & (byte)FluidPipeFlags.OxygenSource) == 0)
                    continue;

                _pipeDemandRates[i] = 0f;
            }
        }

        private void ApplyRoomExchangeOutputs()
        {
            for (int i = 0; i < _nodeCount; i++)
            {
                float exchange = _pipeRoomExchangeContents[i];
                if (exchange <= 0f)
                    continue;

                byte kind = _pipeContentKinds[i];
                if (kind == (byte)FluidPipeContentKind.Oxygen)
                {
                    int roomIndex = _pipeRoomIndices[i];
                    if (atmosphereSystem != null && roomIndex >= 0)
                        atmosphereSystem.InjectOxygenUnits(roomIndex, exchange);
                }
                else if (kind == (byte)FluidPipeContentKind.Water)
                {
                    PublishFluidIncursion(i, exchange);
                }
            }
        }

        private void PublishRuptureSignals(in FluidPipeRuptureRecord rupture)
        {
            AbsoluteUniversePosition aup = IsValidNode(rupture.NodeIndex) ? _pipeAups[rupture.NodeIndex] : default;
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
            GlobalSignals.Publish(in pipeSignal);

            ImpactSignal impactSignal = new ImpactSignal
            {
                PointAup = aup,
                Force = rupture.PressureKPa,
                Intensity = rupture.Flow01,
                MaterialHash = PipeImpactMaterialHash,
                WeightClass = 1,
                Flags = 1
            };
            GlobalSignals.Publish(in impactSignal);
            ConnectionSplineBatchRenderer.SetPipeNodeRuptured((uint)math.max(0, rupture.NodeIndex), true);

            if (rupture.ContentKind == (byte)FluidPipeContentKind.Water)
                PublishFluidIncursion(rupture.NodeIndex, rupture.Contents);
        }

        private void PublishFlowVisuals()
        {
            for (int i = 0; i < _nodeCount; i++)
            {
                float capacity = math.max(FluidPipeGraphConstants.MinCapacity, _pipeCapacities[i]);
                float flow01 = math.saturate(_pipeFlowVectors[i].y * math.rcp(capacity));
                float previous = _pipeLastVisualFlow01[i];
                if (flow01 <= 0.001f && previous <= 0.001f)
                    continue;
                if (math.abs(flow01 - previous) <= 0.01f)
                    continue;

                _pipeLastVisualFlow01[i] = flow01;
                ConnectionSplineBatchRenderer.SetPipeNodeFlow((uint)i, flow01);
            }
        }

        private void PublishFluidIncursion(int nodeIndex, float amount)
        {
            if (!IsValidNode(nodeIndex))
                return;

            int roomIndex = _pipeRoomIndices[nodeIndex];
            if (roomIndex < 0)
                return;

            FluidIncursionSignal incursionSignal = new FluidIncursionSignal
            {
                LeakAup = _pipeAups[nodeIndex],
                CompartmentId = (uint)math.max(0, roomIndex),
                FloodLevel01 = 0f,
                FlowRate01 = math.saturate(amount),
                Flags = 1
            };
            GlobalSignals.Publish(in incursionSignal);
        }

        private FluidPipeTelemetryEntry ReadLatestTelemetry()
        {
            if (!_telemetryRing.IsCreated || _telemetryRing.Length <= 0)
                return default;

            int index = (_telemetryCursor - 1) % _telemetryRing.Length;
            if (index < 0)
                index += _telemetryRing.Length;
            return _telemetryRing[index];
        }

        private void DumpBlackBox()
        {
            if (_blackBoxDumped || !_telemetryRing.IsCreated)
                return;

            _blackBoxDumped = true;
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string logDirectory = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(logDirectory);
                string dumpPath = Path.Combine(logDirectory, "Dump_PIPE_LOGISTICS_ARCHITECT.bin");
                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(0x48385049u);
                    writer.Write(_telemetryRing.Length);
                    writer.Write(_ruptureTelemetryRing.IsCreated ? _ruptureTelemetryRing.Length : 0);
                    for (int i = 0; i < _telemetryRing.Length; i++)
                    {
                        FluidPipeTelemetryEntry entry = _telemetryRing[i];
                        writer.Write(entry.FrameIndex);
                        writer.Write(entry.NodeCount);
                        writer.Write(entry.RuptureCount);
                        writer.Write(entry.NanCount);
                        writer.Write(entry.TotalWater);
                        writer.Write(entry.TotalOxygen);
                        writer.Write(entry.MaxPressureKPa);
                        writer.Write(entry.StateHash);
                    }

                    if (_ruptureTelemetryRing.IsCreated)
                    {
                        for (int i = 0; i < _ruptureTelemetryRing.Length; i++)
                        {
                            FluidPipeRuptureRecord rupture = _ruptureTelemetryRing[i];
                            writer.Write(rupture.NodeIndex);
                            writer.Write(rupture.NetworkId);
                            writer.Write(rupture.RoomIndex);
                            writer.Write(rupture.FrameIndex);
                            writer.Write(rupture.PressureKPa);
                            writer.Write(rupture.Contents);
                            writer.Write(rupture.Flow01);
                            writer.Write(rupture.NodeHash);
                            writer.Write(rupture.ContentKind);
                            writer.Write(rupture.Flags);
                            writer.Write(rupture.Reserved);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[FluidPipeGraphRuntime] Failed to dump pipe black box: " + exception.Message);
#endif
            }
        }

        private bool HasConnection(int sourceNodeIndex, int destinationNodeIndex)
        {
            NativeParallelMultiHashMapIterator<int> iterator;
            int candidate;
            if (!_pipeConnections.TryGetFirstValue(sourceNodeIndex, out candidate, out iterator))
                return false;

            do
            {
                if (candidate == destinationNodeIndex)
                    return true;
            }
            while (_pipeConnections.TryGetNextValue(out candidate, ref iterator));

            return false;
        }

        private float ResolvePressureForNode(int nodeIndex, float contents)
        {
            float capacity = math.max(FluidPipeGraphConstants.MinCapacity, _pipeCapacities[nodeIndex]);
            float maxPressure = math.max(FluidPipeGraphConstants.MinMaxPressureKPa, _pipeMaxPressure[nodeIndex]);
            return math.max(0f, contents) * math.rcp(capacity) * maxPressure;
        }

        private FluidPipeMathLod ResolveMathLod()
        {
            switch (GlobalRegistry.ScalabilityTier)
            {
                case HectonQualityTier.Ultra:
                    return FluidPipeMathLod.Ultra;
                case HectonQualityTier.High:
                    return FluidPipeMathLod.High;
                case HectonQualityTier.Mid:
                    return FluidPipeMathLod.Middle;
                default:
                    return FluidPipeMathLod.Low;
            }
        }

        private bool IsValidNode(int nodeIndex)
        {
            return nodeIndex >= 0 && nodeIndex < _nodeCount;
        }

        private void ResolveAtmosphereSystem(bool force)
        {
            if (!force && _atmosphereResolveAttempted)
                return;

            if (atmosphereSystem != null && atmosphereSystem.isActiveAndEnabled)
            {
                _atmosphereResolveAttempted = true;
                return;
            }

            _atmosphereResolveAttempted = true;
            atmosphereSystem = GetComponentInParent<SubmarineAtmosphereSystem>();
            if (atmosphereSystem == null)
                TryGetComponent(out atmosphereSystem);
        }

        private static short ClampToShort(int value)
        {
            if (value < short.MinValue)
                return short.MinValue;
            if (value > short.MaxValue)
                return short.MaxValue;
            return (short)value;
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
