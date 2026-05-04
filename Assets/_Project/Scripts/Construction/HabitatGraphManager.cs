using System;
using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Audio;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Power;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    [Flags]
    internal enum HabitatSiegeTargetFlags : byte
    {
        None = 0,
        Vulnerable = 1 << 0,
        EmergencyAirlock = 1 << 1,
        Flooded = 1 << 2,
        Ruptured = 1 << 3,
        Brownout = 1 << 4,
        Isolated = 1 << 5,
        CascadeFailure = 1 << 6
    }

    internal struct HabitatSiegeTargetSnapshot
    {
        public float3 ModuleCenter;
        public float3 WeakPoint;
        public float Integrity01;
        public float Vulnerability01;
        public uint NodeId;
        public byte Flags;
        public byte Reserved0;
        public byte Reserved1;
        public byte Reserved2;
    }

    /// <summary>
    /// Rebuilds the placed habitat into a CSR adjacency graph for downstream power and atmosphere solvers.
    /// Owns only base-module topology. Point-to-point crate pipes remain under LogisticsPipeNode.
    /// </summary>
    internal sealed class HabitatGraphManager : IDisposable
    {
        private const float DefaultSocketQuantization = 0.05f;
        private const float OppositeDirectionDotThreshold = -0.85f;
        private const float EdgeResistancePerMeter = 0.05f;
        private const float MinimumEdgeResistance = 0.1f;
        private const float DefaultWaterDensityKilogramsPerCubicMeter = 1025f;
        private const float GravityAccelerationMetersPerSecondSquared = 9.81f;
        private const float DefaultHydrodynamicDamagePerSecondAtFullOverload = 1.5f;
        private const float DefaultHydroShearThresholdKilograms = 18000f;
        private const float PressureBucklingCompressionDeltaThreshold = 0.15f;
        private const float RuptureCascadeNeighborStressMultiplier = 0.5f;
        private const float StructuralGroanStressThreshold01 = 0.8f;
        private const float StructuralGroanPitchRange = 0.32f;
        private const float MinimumHydrodynamicFlowSpeedMetersPerSecond = 0.1f;
        private const float CondensationInteriorTemperatureCelsius = 40f;
        private const float CondensationExternalTemperatureCelsius = 4f;
        private const float SupportCaptureRadiusMeters = 3f;
        private const float SupportCaptureRadiusSq = SupportCaptureRadiusMeters * SupportCaptureRadiusMeters;
        private const int InitialSocketCapacity = 32;
        private const int InitialNodeCapacity = 64;
        private const int InitialEdgeCapacity = 128;
        private const int InitialTemporaryBypassCapacity = 16;
        internal const int MaxSiegeTargetCount = 64;
        private const float SiegeVulnerableIntegrityThreshold01 = 0.72f;
        private const uint ParasiteRootNodeIdSalt = 0x8F3A5C7Du;
        private static readonly int CarbonFilterItemHashId = LocHash.Compute("Data_CarbonFilter");
        private const string NativeMemoryOwner = nameof(HabitatGraphManager);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private static readonly Color PipeSplineColor = new Color(0.30f, 0.82f, 0.95f, 0.88f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticSiegeTargets()
        {
            s_latestSiegeTargets = default;
            s_latestSiegeTargetOwner = null;
            s_latestSiegeTargetCount = 0;
        }

        private readonly List<ModuleSocket> _socketBuffer;
        private readonly List<ModuleRecord> _moduleBuffer;
        private readonly List<EdgeRecord> _edgeBuffer;
        private readonly List<TemporaryBypassRecord> _temporaryBypassBuffer;
        private readonly List<long> _submittedLinkIds;
        private readonly List<long> _emittedRuptureEdgeVfxKeys;
        private readonly List<uint> _ruptureCascadeAppliedNodeIds;
        private readonly Dictionary<uint, int> _moduleIndexByNodeId;
        private readonly Dictionary<SocketKey, SocketMatchEntry> _socketLookup;

        private NativeArray<LogisticsNetworkGraph.LogisticsNode> _nodes;
        private NativeArray<int> _edgeOffsets;
        private NativeArray<int> _edgeDestinations;
        private NativeArray<float> _edgeResistance;
        private NativeArray<int> _edgeWriteCursor;
        private NativeArray<byte> _anchorReachability;
        private NativeArray<byte> _traversalVisited;
        private NativeArray<int> _anchorTraversalQueue;
        private NativeArray<HabitatSiegeTargetSnapshot> _siegeTargets;
        private static NativeArray<HabitatSiegeTargetSnapshot> s_latestSiegeTargets;
        private static HabitatGraphManager s_latestSiegeTargetOwner;
        private static int s_latestSiegeTargetCount;

        private readonly LogisticsNetworkGraph _graph;
        private int _nodeCount;
        private int _edgeCount;
        private int _siegeTargetCount;

        internal HabitatGraphManager(int initialModuleCapacity)
        {
            int safeModuleCapacity = math.max(1, initialModuleCapacity);
            // COLD ALLOC: List<ModuleSocket>[32] — reusable module socket scan buffer for base graph rebuilds — owner: HabitatGraphManager
            _socketBuffer = new List<ModuleSocket>(InitialSocketCapacity);
            // COLD ALLOC: List<ModuleRecord>[64] — reusable module staging buffer for CSR rebuilds — owner: HabitatGraphManager
            _moduleBuffer = new List<ModuleRecord>(safeModuleCapacity);
            // COLD ALLOC: List<EdgeRecord>[128] — reusable undirected base-link staging buffer for CSR rebuilds — owner: HabitatGraphManager
            _edgeBuffer = new List<EdgeRecord>(InitialEdgeCapacity);
            // COLD ALLOC: List<TemporaryBypassRecord>[16] — authored runtime bypass links appended into habitat CSR rebuilds — owner: HabitatGraphManager
            _temporaryBypassBuffer = new List<TemporaryBypassRecord>(InitialTemporaryBypassCapacity);
            // COLD ALLOC: List<Int64>[128] — submitted visual spline link ids for removal during rebuild — owner: HabitatGraphManager
            _submittedLinkIds = new List<long>(InitialEdgeCapacity);
            // COLD ALLOC: List<Int64>[128] - emitted rupture edge VFX keys - owner: HabitatGraphManager
            _emittedRuptureEdgeVfxKeys = new List<long>(InitialEdgeCapacity);
            // COLD ALLOC: List<UInt32>[64] - one-shot rupture cascade source guard - owner: HabitatGraphManager
            _ruptureCascadeAppliedNodeIds = new List<uint>(safeModuleCapacity);
            // COLD ALLOC: Dictionary<UInt32,Int32>[64] — node-id to module-index lookup for temporary bypass stitching — owner: HabitatGraphManager
            _moduleIndexByNodeId = new Dictionary<uint, int>(safeModuleCapacity);
            // COLD ALLOC: Dictionary<SocketKey,SocketMatchEntry>[128] — quantized socket lookup for zero-GC adjacency assembly — owner: HabitatGraphManager
            _socketLookup = new Dictionary<SocketKey, SocketMatchEntry>(InitialEdgeCapacity);

            _graph = new LogisticsNetworkGraph(safeModuleCapacity, InitialEdgeCapacity * 2, 0);
            AllocateNativeBuffers(safeModuleCapacity, InitialEdgeCapacity * 2);
        }

        internal int NodeCount => _nodeCount;
        internal int EdgeCount => _edgeCount;
        internal NativeArray<LogisticsNetworkGraph.LogisticsNode> Nodes => _nodes;
        internal NativeArray<int> EdgeOffsets => _edgeOffsets;
        internal NativeArray<int> EdgeDestinations => _edgeDestinations;
        internal NativeArray<float> EdgeResistance => _edgeResistance;
        internal LogisticsNetworkGraph Graph => _graph;

        internal static bool TryGetLatestSiegeTargets(out NativeArray<HabitatSiegeTargetSnapshot> targets, out int count)
        {
            targets = s_latestSiegeTargets;
            count = s_latestSiegeTargetCount;
            return s_latestSiegeTargetOwner != null && targets.IsCreated && count > 0;
        }

        public void Dispose()
        {
            ClearVisualLinks();
            DisposeNativeBuffers();
            _graph.Dispose();
        }

        internal int TemporaryBypassCount => _temporaryBypassBuffer.Count;

        internal void Rebuild(IReadOnlyList<GameObject> modules)
        {
            ClearVisualLinks();
            _moduleBuffer.Clear();
            _edgeBuffer.Clear();
            _moduleIndexByNodeId.Clear();
            _socketLookup.Clear();
            _nodeCount = 0;
            _edgeCount = 0;
            BaseDegradationSystem.BeginRuptureSync();

            if (modules == null || modules.Count <= 0)
            {
                ClearSiegeTargetSnapshot();
                _graph.BeginBuild(LogisticsNetworkType.OxygenPressure, 1, 1, 0);
                BaseDegradationSystem.EndRuptureSync();
                return;
            }

            PopulateModuleBuffer(modules);
            AppendParasiteRootNodes();
            _nodeCount = _moduleBuffer.Count;
            EnsureRuptureCascadeStateCapacity(_nodeCount);
            if (_nodeCount <= 0)
            {
                ClearSiegeTargetSnapshot();
                _graph.BeginBuild(LogisticsNetworkType.OxygenPressure, 1, 1, 0);
                BaseDegradationSystem.EndRuptureSync();
                return;
            }

            EnsureNodeCapacity(_nodeCount);
            BuildSocketAdjacency();
            AppendTemporaryBypassEdges();
            BuildNodeRecords();
            PruneRuptureCascadeState();
            BuildEdgeRecords();
            EvaluateAnchorReachability();
            PublishAnchorState();
            PublishComponentPowerState();
            PublishEmergencyLockdownState();
            PublishDegradationState();
            PublishSiegeTargetSnapshot();
            PublishGraphKernel();
            PublishVisualLinks();
            BaseDegradationSystem.EndRuptureSync();
        }

        internal void ApplyHydrodynamicStress(float deltaTime)
        {
            if (deltaTime <= 0f || _moduleBuffer.Count <= 0)
                return;

            ApplyWaterPumpDrainage(deltaTime);
            ApplyOxygenScrubberFilterConsumption(deltaTime);
            ApplyThermalCondensationState();
            QueueFloodMassLoads(deltaTime);
            ApplyIslandFloodCenterOfMassShifts(deltaTime);
            bool runtimeTopologyChanged = EvaluateBulkheadFloodStress(deltaTime);
            runtimeTopologyChanged |= EvaluatePressureBucklingStress(deltaTime);
            runtimeTopologyChanged |= EvaluateDetachedDebrisState();
            if (runtimeTopologyChanged)
                PublishRuntimeRuptureTopologyState();

            HectonMapMagicVegetationBridge bridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (bridge == null)
            {
                PublishSiegeTargetSnapshot();
                return;
            }

            for (int moduleIndex = 0; moduleIndex < _moduleBuffer.Count; moduleIndex++)
            {
                ModuleRecord module = _moduleBuffer[moduleIndex];
                BaseModule baseModule = module.BaseModule;
                if (baseModule == null || !baseModule.isActiveAndEnabled || baseModule.IsBreached)
                    continue;

                if (!bridge.TrySampleAbyssalFlow(module.Position, out Vector3 flowVector))
                    continue;

                float flowSpeedSquared = flowVector.sqrMagnitude;
                if (flowSpeedSquared < (MinimumHydrodynamicFlowSpeedMetersPerSecond * MinimumHydrodynamicFlowSpeedMetersPerSecond))
                    continue;

                float projectedAreaSquareMeters = ResolveProjectedDragAreaSquareMeters(baseModule);
                float moduleYieldStrengthNewtons = ResolveYieldStrengthNewtons(baseModule);
                if (projectedAreaSquareMeters <= 0f || moduleYieldStrengthNewtons <= 0f)
                    continue;

                // DragForce = 0.5 * rho * v^2 * A. Overload above yield converts into normalized fatigue.
                float dragForceNewtons = 0.5f * DefaultWaterDensityKilogramsPerCubicMeter * flowSpeedSquared * projectedAreaSquareMeters;
                if (dragForceNewtons <= moduleYieldStrengthNewtons)
                    continue;

                float overloadRatio = (dragForceNewtons - moduleYieldStrengthNewtons) / moduleYieldStrengthNewtons;
                float damageAmount = overloadRatio * deltaTime * DefaultHydrodynamicDamagePerSecondAtFullOverload * math.max(1f, baseModule.MaxIntegrity);
                if (damageAmount <= 0f || !math.isfinite(damageAmount))
                    continue;

                baseModule.ApplyDamage(damageAmount);
            }

            PublishSiegeTargetSnapshot();
        }

        private void ApplyWaterPumpDrainage(float deltaTime)
        {
            if (deltaTime <= 0f ||
                _nodeCount <= 0 ||
                !_traversalVisited.IsCreated ||
                !_anchorTraversalQueue.IsCreated)
            {
                return;
            }

            int pumpCount = WaterPumpModule.ActivePumpCount;
            for (int pumpIndex = 0; pumpIndex < pumpCount; pumpIndex++)
            {
                WaterPumpModule pump = WaterPumpModule.GetActivePump(pumpIndex);
                if (pump == null || !pump.CanPump || !TryResolveModuleNodeIndex(pump.HostModule, out int startNodeIndex))
                    continue;

                float remainingDrainM3 = pump.ResolveDrainBudgetM3(deltaTime);
                if (remainingDrainM3 <= 0f)
                    continue;

                DrainConnectedFloodComponent(startNodeIndex, ref remainingDrainM3);
            }
        }

        private void DrainConnectedFloodComponent(int startNodeIndex, ref float remainingDrainM3)
        {
            if (remainingDrainM3 <= 0f || startNodeIndex < 0 || startNodeIndex >= _nodeCount)
                return;

            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
                _traversalVisited[nodeIndex] = 0;

            int queueHead = 0;
            int queueTail = 0;
            _traversalVisited[startNodeIndex] = 1;
            _anchorTraversalQueue[queueTail++] = startNodeIndex;

            while (queueHead < queueTail && remainingDrainM3 > 0f)
            {
                int currentNodeIndex = _anchorTraversalQueue[queueHead++];
                BaseModule baseModule = _moduleBuffer[currentNodeIndex].BaseModule;
                if (baseModule != null && baseModule.isActiveAndEnabled)
                    remainingDrainM3 -= baseModule.DrainWaterVolumeM3(remainingDrainM3);

                int edgeStart = _edgeOffsets[currentNodeIndex];
                int edgeEnd = _edgeOffsets[currentNodeIndex + 1];
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int neighborNodeIndex = _edgeDestinations[edgeIndex];
                    if (neighborNodeIndex < 0 ||
                        neighborNodeIndex >= _nodeCount ||
                        _traversalVisited[neighborNodeIndex] != 0)
                    {
                        continue;
                    }

                    _traversalVisited[neighborNodeIndex] = 1;
                    _anchorTraversalQueue[queueTail++] = neighborNodeIndex;
                }
            }
        }

        private void ApplyOxygenScrubberFilterConsumption(float deltaTime)
        {
            int moduleCount = math.min(_nodeCount, _moduleBuffer.Count);
            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                BaseModule baseModule = _moduleBuffer[moduleIndex].BaseModule;
                if (baseModule != null && baseModule.isActiveAndEnabled)
                    baseModule.UpdateCarbonFilterLogistics(deltaTime, CarbonFilterItemHashId);
            }
        }

        private void ApplyThermalCondensationState()
        {
            int moduleCount = math.min(_nodeCount, _moduleBuffer.Count);
            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                BaseModule baseModule = _moduleBuffer[moduleIndex].BaseModule;
                if (baseModule == null || !baseModule.isActiveAndEnabled)
                    continue;

                float internalTemperatureCelsius = baseModule.ResolveHostRoomTemperatureCelsius();
                float externalTemperatureCelsius = baseModule.PressureCompressionDepthMeters > 100f
                    ? 2f
                    : 12f;
                baseModule.SetCondensationState(
                    internalTemperatureCelsius > CondensationInteriorTemperatureCelsius &&
                    externalTemperatureCelsius < CondensationExternalTemperatureCelsius);
            }
        }

        private bool EvaluateDetachedDebrisState()
        {
            bool topologyChanged = false;
            int moduleCount = math.min(_nodeCount, _moduleBuffer.Count);
            for (int nodeIndex = 0; nodeIndex < moduleCount; nodeIndex++)
            {
                BaseModule baseModule = _moduleBuffer[nodeIndex].BaseModule;
                if (baseModule == null ||
                    !baseModule.isActiveAndEnabled ||
                    baseModule.IsDetachedDebris ||
                    baseModule.CurrentIntegrity > 0f)
                {
                    continue;
                }

                if (!AreConnectingEdgesSevered(nodeIndex))
                    continue;

                topologyChanged |= baseModule.TryDetachAsSinkingDebris();
            }

            return topologyChanged;
        }

        private bool AreConnectingEdgesSevered(int nodeIndex)
        {
            bool hasConnection = false;
            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                if (edge.SourceIndex != nodeIndex && edge.DestinationIndex != nodeIndex)
                    continue;

                hasConnection = true;
                if (!edge.Severed)
                    return false;
            }

            return hasConnection;
        }

        private bool TryResolveModuleNodeIndex(BaseModule module, out int nodeIndex)
        {
            nodeIndex = -1;
            if (module == null)
                return false;

            uint nodeId = unchecked((uint)EntityId.ToULong(module.GetEntityId()));
            return nodeId != 0u &&
                   _moduleIndexByNodeId.TryGetValue(nodeId, out nodeIndex) &&
                   nodeIndex >= 0 &&
                   nodeIndex < _nodeCount;
        }

        private void QueueFloodMassLoads(float deltaTime)
        {
            for (int moduleIndex = 0; moduleIndex < _moduleBuffer.Count; moduleIndex++)
            {
                ModuleRecord module = _moduleBuffer[moduleIndex];
                BaseModule baseModule = module.BaseModule;
                if (baseModule == null || !baseModule.isActiveAndEnabled)
                    continue;

                float floodWaterMassKilograms = baseModule.ResolveFloodWaterMassKilograms();
                float parasiteMassKilograms = baseModule.ResolveParasiteAddedMassKilograms();
                float structuralMassKilograms = floodWaterMassKilograms + parasiteMassKilograms;
                if (structuralMassKilograms <= 0f || !math.isfinite(structuralMassKilograms))
                    continue;

                baseModule.QueueHydroStructuralLoad(structuralMassKilograms, module.Position, deltaTime);
            }
        }

        private void ApplyIslandFloodCenterOfMassShifts(float deltaTime)
        {
            if (deltaTime <= 0f ||
                _nodeCount <= 0 ||
                !_anchorReachability.IsCreated ||
                !_traversalVisited.IsCreated ||
                !_anchorTraversalQueue.IsCreated ||
                !_edgeOffsets.IsCreated ||
                !_edgeDestinations.IsCreated)
            {
                return;
            }

            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
                _traversalVisited[nodeIndex] = 0;

            for (int seedIndex = 0; seedIndex < _nodeCount; seedIndex++)
            {
                if (_traversalVisited[seedIndex] != 0)
                    continue;

                int islandStart = 0;
                int queueHead = 0;
                int queueTail = 0;
                _traversalVisited[seedIndex] = 1;
                _anchorTraversalQueue[queueTail++] = seedIndex;

                float totalFloodMassKilograms = 0f;
                Vector3 weightedFloodCentroid = Vector3.zero;

                while (queueHead < queueTail)
                {
                    int currentNodeIndex = _anchorTraversalQueue[queueHead++];
                    ModuleRecord currentRecord = _moduleBuffer[currentNodeIndex];
                    BaseModule currentModule = currentRecord.BaseModule;
                    if (currentModule != null && currentModule.isActiveAndEnabled)
                    {
                        float floodMassKilograms = currentModule.ResolveFloodWaterMassKilograms();
                        if (floodMassKilograms > 0f && math.isfinite(floodMassKilograms))
                        {
                            totalFloodMassKilograms += floodMassKilograms;
                            weightedFloodCentroid += (Vector3)currentRecord.Position * floodMassKilograms;
                        }
                    }

                    int edgeStart = _edgeOffsets[currentNodeIndex];
                    int edgeEnd = _edgeOffsets[currentNodeIndex + 1];
                    for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                    {
                        int neighborNodeIndex = _edgeDestinations[edgeIndex];
                        if (neighborNodeIndex < 0 ||
                            neighborNodeIndex >= _nodeCount ||
                            _traversalVisited[neighborNodeIndex] != 0)
                        {
                            continue;
                        }

                        _traversalVisited[neighborNodeIndex] = 1;
                        _anchorTraversalQueue[queueTail++] = neighborNodeIndex;
                    }
                }

                Vector3 centroid = totalFloodMassKilograms > 0f
                    ? weightedFloodCentroid / totalFloodMassKilograms
                    : Vector3.zero;
                for (int islandNodeOffset = islandStart; islandNodeOffset < queueTail; islandNodeOffset++)
                {
                    BaseModule islandModule = _moduleBuffer[_anchorTraversalQueue[islandNodeOffset]].BaseModule;
                    if (islandModule != null && islandModule.isActiveAndEnabled)
                        islandModule.ApplyIslandFloodCenterOfMassShift(centroid, totalFloodMassKilograms, deltaTime);
                }
            }
        }

        private bool EvaluateBulkheadFloodStress(float deltaTime)
        {
            bool topologyChanged = false;
            for (int moduleIndex = 0; moduleIndex < _moduleBuffer.Count; moduleIndex++)
            {
                BaseModule baseModule = _moduleBuffer[moduleIndex].BaseModule;
                if (baseModule != null && baseModule.isActiveAndEnabled)
                    baseModule.DecayBulkheadFloodStress(deltaTime);
            }

            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                if (edge.Severed)
                    continue;

                BaseModule sourceModule = _moduleBuffer[edge.SourceIndex].BaseModule;
                BaseModule destinationModule = _moduleBuffer[edge.DestinationIndex].BaseModule;
                bool ruptured = ApplyBulkheadFloodStress(sourceModule, destinationModule, deltaTime);
                ruptured |= ApplyBulkheadFloodStress(destinationModule, sourceModule, deltaTime);
                if (ruptured)
                {
                    MarkEdgeRuptured(ref edge);
                    _edgeBuffer[edgeIndex] = edge;
                    topologyChanged = true;
                }
            }

            return topologyChanged;
        }

        private static bool ApplyBulkheadFloodStress(BaseModule floodedModule, BaseModule candidateAirlock, float deltaTime)
        {
            if (!IsFloodedForHydroStress(floodedModule) || !IsPristineForHydroStress(candidateAirlock))
                return false;

            float floodWaterMassKilograms = floodedModule.ResolveFloodWaterMassKilograms();
            if (floodWaterMassKilograms <= 0f || !math.isfinite(floodWaterMassKilograms))
                return false;

            return candidateAirlock.AccumulateBulkheadFloodStress(floodWaterMassKilograms, deltaTime);
        }

        private bool EvaluatePressureBucklingStress(float deltaTime)
        {
            bool topologyChanged = ApplyQueuedRuptureCascadeFailures();

            for (int moduleIndex = 0; moduleIndex < _moduleBuffer.Count; moduleIndex++)
            {
                BaseModule baseModule = _moduleBuffer[moduleIndex].BaseModule;
                if (baseModule != null && baseModule.isActiveAndEnabled)
                    baseModule.DecayJointShearStress(deltaTime);
            }

            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                if (edge.Severed || edge.IsSyntheticParasiteRoot)
                    continue;

                BaseModule sourceModule = _moduleBuffer[edge.SourceIndex].BaseModule;
                BaseModule destinationModule = _moduleBuffer[edge.DestinationIndex].BaseModule;
                if (sourceModule == null || destinationModule == null)
                    continue;

                float compressionDelta = math.abs(sourceModule.PressureCompressionAlpha01 - destinationModule.PressureCompressionAlpha01);
                if (compressionDelta <= PressureBucklingCompressionDeltaThreshold || !math.isfinite(compressionDelta))
                    continue;

                bool sourceDamaged = sourceModule.ApplyJointShearStress(compressionDelta, deltaTime);
                bool destinationDamaged = destinationModule.ApplyJointShearStress(compressionDelta, deltaTime);
                if (!sourceDamaged && !destinationDamaged)
                    continue;

                float stress01 = math.max(sourceModule.JointShearStress01, destinationModule.JointShearStress01);
                if (stress01 < StructuralGroanStressThreshold01)
                    continue;

                bool sourceGroanAllowed = sourceModule.TryConsumeJointShearGroanCooldown();
                bool destinationGroanAllowed = destinationModule.TryConsumeJointShearGroanCooldown();
                if (!sourceGroanAllowed && !destinationGroanAllowed)
                    continue;

                Vector3 startAup = HectonFloatingOrigin.ToAbsoluteUniversePosition((Vector3)edge.StartSocketPosition);
                Vector3 endAup = HectonFloatingOrigin.ToAbsoluteUniversePosition((Vector3)edge.EndSocketPosition);
                Vector3 midpoint = HectonFloatingOrigin.ToRuntimePosition((startAup + endAup) * 0.5f);
                ProceduralAudioEvents.RaiseStructuralStressTriggered(
                    midpoint,
                    stress01,
                    1f + (math.saturate(stress01) * StructuralGroanPitchRange));
            }

            ApplyRuptureCascadeStressFromRupturedNodes();
            return topologyChanged;
        }

        private bool ApplyQueuedRuptureCascadeFailures()
        {
            bool topologyChanged = false;
            int maxNodeCount = math.min(_nodeCount, _moduleBuffer.Count);
            for (int nodeIndex = 0; nodeIndex < maxNodeCount; nodeIndex++)
            {
                BaseModule baseModule = _moduleBuffer[nodeIndex].BaseModule;
                if (baseModule == null || !baseModule.isActiveAndEnabled)
                    continue;

                if (!baseModule.TryConsumePendingRuptureCascadeFailure())
                    continue;

                MarkNodeRuptured(nodeIndex);
                RuptureConnectedEdges(nodeIndex);
                topologyChanged = true;
            }

            return topologyChanged;
        }

        private void ApplyRuptureCascadeStressFromRupturedNodes()
        {
            if (_nodeCount <= 0 || !_edgeOffsets.IsCreated || !_edgeDestinations.IsCreated)
                return;

            int maxNodeCount = math.min(_nodeCount, _moduleBuffer.Count);
            for (int nodeIndex = 0; nodeIndex < maxNodeCount; nodeIndex++)
            {
                LogisticsNodeFlags sourceFlags = _nodes[nodeIndex].Flags;
                BaseModule sourceModule = _moduleBuffer[nodeIndex].BaseModule;
                bool sourceRuptured = (sourceFlags & LogisticsNodeFlags.Ruptured) != 0 ||
                                      (sourceModule != null && sourceModule.IntegrityState == BaseModuleIntegrityState.Ruptured);
                if (!sourceRuptured)
                    continue;

                uint sourceNodeId = _moduleBuffer[nodeIndex].NodeId;
                if (sourceNodeId != 0u && HasRuptureCascadeBeenApplied(sourceNodeId))
                    continue;

                if (sourceNodeId != 0u)
                    MarkRuptureCascadeApplied(sourceNodeId);

                int edgeStart = _edgeOffsets[nodeIndex];
                int edgeEnd = _edgeOffsets[nodeIndex + 1];
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int neighborNodeIndex = _edgeDestinations[edgeIndex];
                    if (neighborNodeIndex < 0 || neighborNodeIndex >= maxNodeCount)
                        continue;

                    LogisticsNodeFlags neighborFlags = _nodes[neighborNodeIndex].Flags;
                    if ((neighborFlags & LogisticsNodeFlags.Ruptured) != 0)
                        continue;

                    if (!HasUnseveredRuntimeEdge(nodeIndex, neighborNodeIndex))
                        continue;

                    BaseModule neighborModule = _moduleBuffer[neighborNodeIndex].BaseModule;
                    if (neighborModule == null ||
                        !neighborModule.isActiveAndEnabled ||
                        neighborModule.IntegrityState == BaseModuleIntegrityState.Ruptured)
                    {
                        continue;
                    }

                    neighborModule.ApplyRuptureCascadeStress(RuptureCascadeNeighborStressMultiplier);
                }
            }
        }

        private bool HasUnseveredRuntimeEdge(int sourceIndex, int destinationIndex)
        {
            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                if (edge.Severed)
                    continue;

                if (edge.SourceIndex == sourceIndex && edge.DestinationIndex == destinationIndex)
                    return true;

                if (!edge.DirectedOnly &&
                    edge.SourceIndex == destinationIndex &&
                    edge.DestinationIndex == sourceIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureRuptureCascadeStateCapacity(int requiredCapacity)
        {
            int safeCapacity = NextPowerOfTwo(math.max(1, requiredCapacity));
            if (_ruptureCascadeAppliedNodeIds.Capacity >= safeCapacity)
                return;

            _ruptureCascadeAppliedNodeIds.Capacity = safeCapacity;
        }

        private bool HasRuptureCascadeBeenApplied(uint nodeId)
        {
            for (int i = 0; i < _ruptureCascadeAppliedNodeIds.Count; i++)
            {
                if (_ruptureCascadeAppliedNodeIds[i] == nodeId)
                    return true;
            }

            return false;
        }

        private void MarkRuptureCascadeApplied(uint nodeId)
        {
            if (nodeId == 0u || HasRuptureCascadeBeenApplied(nodeId))
                return;

            if (_ruptureCascadeAppliedNodeIds.Count < _ruptureCascadeAppliedNodeIds.Capacity)
                _ruptureCascadeAppliedNodeIds.Add(nodeId);
        }

        private void PruneRuptureCascadeState()
        {
            for (int i = _ruptureCascadeAppliedNodeIds.Count - 1; i >= 0; i--)
            {
                uint nodeId = _ruptureCascadeAppliedNodeIds[i];
                if (nodeId != 0u && IsRuptureCascadeSourceStillRuptured(nodeId))
                    continue;

                int lastIndex = _ruptureCascadeAppliedNodeIds.Count - 1;
                _ruptureCascadeAppliedNodeIds[i] = _ruptureCascadeAppliedNodeIds[lastIndex];
                _ruptureCascadeAppliedNodeIds.RemoveAt(lastIndex);
            }
        }

        private bool IsRuptureCascadeSourceStillRuptured(uint nodeId)
        {
            int moduleCount = math.min(_nodeCount, _moduleBuffer.Count);
            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                ModuleRecord module = _moduleBuffer[moduleIndex];
                if (module.NodeId != nodeId)
                    continue;

                LogisticsNodeFlags nodeFlags = moduleIndex < _nodes.Length ? _nodes[moduleIndex].Flags : LogisticsNodeFlags.None;
                BaseModule baseModule = module.BaseModule;
                return (nodeFlags & LogisticsNodeFlags.Ruptured) != 0 ||
                       (baseModule != null && baseModule.IntegrityState == BaseModuleIntegrityState.Ruptured);
            }

            return false;
        }

        private void RuptureConnectedEdges(int nodeIndex)
        {
            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                if (edge.Severed || edge.IsSyntheticParasiteRoot)
                    continue;

                if (edge.SourceIndex != nodeIndex && edge.DestinationIndex != nodeIndex)
                    continue;

                MarkEdgeRuptured(ref edge);
                _edgeBuffer[edgeIndex] = edge;
            }
        }

        internal void NotifyModuleEmergencyStateChanged(BaseModule module)
        {
            if (module == null || _nodeCount <= 0)
                return;

            PublishEmergencyLockdownState();
            PublishSiegeTargetSnapshot();
        }

        internal bool TryResolveFungalMindTarget(BaseModule sourceModule, out BaseModule targetModule, out float targetPotential)
        {
            targetModule = null;
            targetPotential = 0f;
            if (sourceModule == null ||
                _nodeCount <= 0 ||
                !_edgeOffsets.IsCreated ||
                !_edgeDestinations.IsCreated ||
                !_traversalVisited.IsCreated ||
                !_anchorTraversalQueue.IsCreated)
            {
                return false;
            }

            uint sourceNodeId = unchecked((uint)EntityId.ToULong(sourceModule.GetEntityId()));
            if (sourceNodeId == 0u ||
                !_moduleIndexByNodeId.TryGetValue(sourceNodeId, out int startNodeIndex) ||
                startNodeIndex < 0 ||
                startNodeIndex >= _nodeCount)
            {
                return false;
            }

            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
                _traversalVisited[nodeIndex] = 0;

            int queueHead = 0;
            int queueTail = 0;
            _traversalVisited[startNodeIndex] = 1;
            _anchorTraversalQueue[queueTail++] = startNodeIndex;

            float bestScore = 0f;
            float bestPotential = 0f;
            BaseModule bestModule = null;
            while (queueHead < queueTail)
            {
                int currentNodeIndex = _anchorTraversalQueue[queueHead++];
                byte currentDepth = _traversalVisited[currentNodeIndex];
                ModuleRecord currentRecord = _moduleBuffer[currentNodeIndex];
                if (currentNodeIndex != startNodeIndex && !currentRecord.IsSyntheticParasiteRoot)
                {
                    BaseModule currentModule = currentRecord.BaseModule;
                    if (currentModule != null && currentModule.isActiveAndEnabled)
                    {
                        float rawPotential = ResolveFungalMindPotentialScore(currentRecord, _nodes[currentNodeIndex]);
                        float depthPenalty = 1f + (math.max(0, currentDepth - 1) * 0.08f);
                        float score = rawPotential / depthPenalty;
                        if (score > bestScore && math.isfinite(score))
                        {
                            bestScore = score;
                            bestPotential = rawPotential;
                            bestModule = currentModule;
                        }
                    }
                }

                int edgeStart = _edgeOffsets[currentNodeIndex];
                int edgeEnd = _edgeOffsets[currentNodeIndex + 1];
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int neighborNodeIndex = _edgeDestinations[edgeIndex];
                    if (neighborNodeIndex < 0 ||
                        neighborNodeIndex >= _nodeCount ||
                        _traversalVisited[neighborNodeIndex] != 0 ||
                        _moduleBuffer[neighborNodeIndex].IsSyntheticParasiteRoot)
                    {
                        continue;
                    }

                    _traversalVisited[neighborNodeIndex] = (byte)math.min(255, currentDepth + 1);
                    _anchorTraversalQueue[queueTail++] = neighborNodeIndex;
                }
            }

            if (bestModule == null || bestScore <= 0f)
                return false;

            targetModule = bestModule;
            targetPotential = bestPotential;
            return true;
        }

        private void PopulateModuleBuffer(IReadOnlyList<GameObject> modules)
        {
            int count = modules.Count;
            if (_moduleBuffer.Capacity < count)
                _moduleBuffer.Capacity = count;

            for (int i = 0; i < count; i++)
            {
                GameObject moduleObject = modules[i];
                if (moduleObject == null)
                    continue;

                ModuleMarker marker = moduleObject.TryGetComponent(out ModuleMarker resolvedMarker) ? resolvedMarker : null;
                BaseModule baseModule = moduleObject.TryGetComponent(out BaseModule resolvedBaseModule) ? resolvedBaseModule : null;
                if (baseModule != null && baseModule.IsDetachedDebris)
                    continue;

                EntityId entityId = moduleObject.GetEntityId();
                uint nodeId = unchecked((uint)EntityId.ToULong(entityId));
                Vector3 modulePosition = moduleObject.transform.position;

                _moduleBuffer.Add(new ModuleRecord
                {
                    ModuleObject = moduleObject,
                    Marker = marker,
                    BaseModule = baseModule,
                    Position = modulePosition,
                    NodeId = nodeId,
                    IsAnchorNode = ResolveStructuralAnchorState(baseModule, marker),
                    IsEmergencyAirlock = ResolveEmergencyAirlockState(baseModule, marker)
                });

                _moduleIndexByNodeId[nodeId] = _moduleBuffer.Count - 1;
            }
        }

        private void AppendParasiteRootNodes()
        {
            int hostCount = _moduleBuffer.Count;
            for (int hostIndex = 0; hostIndex < hostCount; hostIndex++)
            {
                ModuleRecord host = _moduleBuffer[hostIndex];
                BaseModule hostModule = host.BaseModule;
                if (hostModule == null ||
                    !hostModule.TryGetMatureParasiteRootLoad(out float rootDrainWatts, out float infectionLevel))
                {
                    continue;
                }

                uint rootNodeId = ResolveParasiteRootNodeId(host.NodeId);
                float3 rootPosition = host.Position + new float3(0f, 0.25f + infectionLevel, 0f);
                int rootIndex = _moduleBuffer.Count;
                _moduleBuffer.Add(new ModuleRecord
                {
                    ModuleObject = null,
                    Marker = null,
                    BaseModule = null,
                    Position = rootPosition,
                    NodeId = rootNodeId,
                    IsAnchorNode = false,
                    IsEmergencyAirlock = false,
                    SyntheticPowerDrainWatts = rootDrainWatts,
                    IsSyntheticParasiteRoot = true
                });
                _moduleIndexByNodeId[rootNodeId] = rootIndex;

                _edgeBuffer.Add(new EdgeRecord
                {
                    SourceIndex = hostIndex,
                    DestinationIndex = rootIndex,
                    StartSocketPosition = host.Position,
                    EndSocketPosition = rootPosition,
                    StartForward = new float3(0f, 1f, 0f),
                    EndForward = new float3(0f, -1f, 0f),
                    Resistance = MinimumEdgeResistance,
                    Flags = PipeRenderFlags.None,
                    Severed = false,
                    IsSyntheticParasiteRoot = true
                });
            }
        }

        private uint ResolveParasiteRootNodeId(uint hostNodeId)
        {
            uint candidate = hostNodeId ^ ParasiteRootNodeIdSalt;
            if (candidate == 0u)
                candidate = ParasiteRootNodeIdSalt;

            while (_moduleIndexByNodeId.ContainsKey(candidate))
                candidate++;

            return candidate;
        }

        internal bool TryAddTemporaryBypass(GameObject sourceModule, GameObject destinationModule)
        {
            bool injectedDirectly;
            return TryAddTemporaryBypass(sourceModule, destinationModule, out injectedDirectly);
        }

        internal bool TryAddTemporaryBypass(GameObject sourceModule, GameObject destinationModule, out bool injectedDirectly)
        {
            return TryAddTemporaryBypass(sourceModule, destinationModule, 0, 0, out injectedDirectly);
        }

        internal bool TryAddTemporaryBypass(
            GameObject sourceModule,
            GameObject destinationModule,
            int sourceModuleHashId,
            int destinationModuleHashId,
            out bool injectedDirectly)
        {
            injectedDirectly = false;
            if (sourceModule == null || destinationModule == null || ReferenceEquals(sourceModule, destinationModule))
                return false;

            uint sourceNodeId = unchecked((uint)EntityId.ToULong(sourceModule.GetEntityId()));
            uint destinationNodeId = unchecked((uint)EntityId.ToULong(destinationModule.GetEntityId()));
            if (sourceNodeId == 0u || destinationNodeId == 0u || sourceNodeId == destinationNodeId)
                return false;

            sourceModuleHashId = ResolveTemporaryBypassModuleHashId(sourceModule, sourceModuleHashId);
            destinationModuleHashId = ResolveTemporaryBypassModuleHashId(destinationModule, destinationModuleHashId);
            if (sourceModuleHashId == 0 || destinationModuleHashId == 0)
                return false;

            for (int i = 0; i < _temporaryBypassBuffer.Count; i++)
            {
                TemporaryBypassRecord existing = _temporaryBypassBuffer[i];
                if (existing.SourceNodeId == sourceNodeId && existing.DestinationNodeId == destinationNodeId)
                    return false;
            }

            if (_temporaryBypassBuffer.Count >= _temporaryBypassBuffer.Capacity)
                return false;

            if (!TryResolveModuleGraphPosition(sourceNodeId, sourceModule, out Vector3 sourcePosition) ||
                !TryResolveModuleGraphPosition(destinationNodeId, destinationModule, out Vector3 destinationPosition))
            {
                return false;
            }

            int recordIndex = _temporaryBypassBuffer.Count;
            _temporaryBypassBuffer.Add(new TemporaryBypassRecord
            {
                SourceNodeId = sourceNodeId,
                DestinationNodeId = destinationNodeId,
                SourceModuleHashId = sourceModuleHashId,
                DestinationModuleHashId = destinationModuleHashId,
                SourcePosition = sourcePosition,
                DestinationPosition = destinationPosition
            });

            injectedDirectly = TryInjectTemporaryBypassIntoLiveCsr(sourceNodeId, destinationNodeId, sourcePosition, destinationPosition);
            if (injectedDirectly)
                return true;

            _temporaryBypassBuffer.RemoveAt(recordIndex);
            return false;
        }

        private static int ResolveTemporaryBypassModuleHashId(GameObject module, int capturedModuleHashId)
        {
            if (capturedModuleHashId != 0)
                return capturedModuleHashId;

            if (module != null &&
                module.TryGetComponent(out ModuleMarker marker) &&
                marker != null &&
                marker.Data != null)
            {
                return marker.Data.ModuleHashId;
            }

            return 0;
        }

        private bool TryResolveModuleGraphPosition(uint nodeId, GameObject fallbackModule, out Vector3 position)
        {
            if (_moduleIndexByNodeId.TryGetValue(nodeId, out int moduleIndex) &&
                moduleIndex >= 0 &&
                moduleIndex < _moduleBuffer.Count)
            {
                position = _moduleBuffer[moduleIndex].Position;
                return true;
            }

            if (fallbackModule == null)
            {
                position = default;
                return false;
            }

            position = fallbackModule.transform.position;
            return true;
        }

        private bool TryInjectTemporaryBypassIntoLiveCsr(uint sourceNodeId, uint destinationNodeId, Vector3 sourcePosition, Vector3 destinationPosition)
        {
            if (_nodeCount <= 0 ||
                !_edgeOffsets.IsCreated ||
                !_edgeDestinations.IsCreated ||
                !_edgeResistance.IsCreated ||
                !_moduleIndexByNodeId.TryGetValue(sourceNodeId, out int sourceIndex) ||
                !_moduleIndexByNodeId.TryGetValue(destinationNodeId, out int destinationIndex) ||
                sourceIndex == destinationIndex ||
                _edgeCount + 1 > _edgeDestinations.Length ||
                _edgeBuffer.Count >= _edgeBuffer.Capacity)
            {
                return false;
            }

            Vector3 direction = destinationPosition - sourcePosition;
            float sqrMagnitude = direction.sqrMagnitude;
            Vector3 forward = sqrMagnitude > 0.0001f ? direction / math.sqrt(sqrMagnitude) : Vector3.up;
            float resistance = math.max(MinimumEdgeResistance, math.sqrt(math.max(0f, sqrMagnitude)) * EdgeResistancePerMeter);

            _edgeBuffer.Add(new EdgeRecord
            {
                SourceIndex = sourceIndex,
                DestinationIndex = destinationIndex,
                StartSocketPosition = sourcePosition,
                EndSocketPosition = destinationPosition,
                StartForward = forward,
                EndForward = -forward,
                Resistance = resistance,
                Flags = PipeRenderFlags.None,
                Severed = false,
                DirectedOnly = true
            });

            InsertDirectedCsrEdge(sourceIndex, destinationIndex, resistance);
            EvaluateAnchorReachability();
            PublishAnchorState();
            PublishComponentPowerState();
            PublishEmergencyLockdownState();
            PublishDegradationState();
            PublishSiegeTargetSnapshot();
            PublishGraphKernel();
            return true;
        }

        private void InsertDirectedCsrEdge(int sourceIndex, int destinationIndex, float resistance)
        {
            int insertIndex = _edgeOffsets[sourceIndex + 1];
            for (int edgeIndex = _edgeCount; edgeIndex > insertIndex; edgeIndex--)
            {
                _edgeDestinations[edgeIndex] = _edgeDestinations[edgeIndex - 1];
                _edgeResistance[edgeIndex] = _edgeResistance[edgeIndex - 1];
            }

            _edgeDestinations[insertIndex] = destinationIndex;
            _edgeResistance[insertIndex] = resistance;
            for (int nodeIndex = sourceIndex + 1; nodeIndex <= _nodeCount; nodeIndex++)
                _edgeOffsets[nodeIndex] = _edgeOffsets[nodeIndex] + 1;

            _edgeCount++;
        }

        private void AppendTemporaryBypassEdges()
        {
            for (int bypassIndex = 0; bypassIndex < _temporaryBypassBuffer.Count; bypassIndex++)
            {
                TemporaryBypassRecord bypass = _temporaryBypassBuffer[bypassIndex];
                if (_edgeBuffer.Count >= _edgeBuffer.Capacity ||
                    !_moduleIndexByNodeId.TryGetValue(bypass.SourceNodeId, out int sourceIndex) ||
                    !_moduleIndexByNodeId.TryGetValue(bypass.DestinationNodeId, out int destinationIndex) ||
                    sourceIndex == destinationIndex)
                {
                    continue;
                }

                Vector3 sourcePosition = _moduleBuffer[sourceIndex].Position;
                Vector3 destinationPosition = _moduleBuffer[destinationIndex].Position;
                Vector3 direction = destinationPosition - sourcePosition;
                float sqrMagnitude = direction.sqrMagnitude;
                Vector3 forward = sqrMagnitude > 0.0001f ? direction / math.sqrt(sqrMagnitude) : Vector3.up;

                _edgeBuffer.Add(new EdgeRecord
                {
                    SourceIndex = sourceIndex,
                    DestinationIndex = destinationIndex,
                    StartSocketPosition = sourcePosition,
                    EndSocketPosition = destinationPosition,
                    StartForward = forward,
                    EndForward = -forward,
                    Flags = PipeRenderFlags.None,
                    Severed = false,
                    DirectedOnly = true
                });
            }
        }

        private void BuildSocketAdjacency()
        {
            int quantizationScale = math.max(1, (int)math.round(1f / DefaultSocketQuantization));
            for (int moduleIndex = 0; moduleIndex < _moduleBuffer.Count; moduleIndex++)
                IndexSockets(moduleIndex, _moduleBuffer[moduleIndex].ModuleObject, quantizationScale);
        }

        private void IndexSockets(int moduleIndex, GameObject root, int quantizationScale)
        {
            if (root == null)
                return;

            _socketBuffer.Clear();
            root.GetComponentsInChildren(true, _socketBuffer);

            for (int i = 0; i < _socketBuffer.Count; i++)
            {
                ModuleSocket socket = _socketBuffer[i];
                if (socket == null)
                    continue;

                Transform socketTransform = socket.transform;
                int axis = QuantizeAxis(socketTransform.forward);
                SocketKey oppositeKey = SocketKey.Create(socketTransform.position, OppositeAxis(axis), quantizationScale);

                if (_socketLookup.TryGetValue(oppositeKey, out SocketMatchEntry existing))
                {
                    if (existing.ModuleIndex != moduleIndex &&
                        ModuleSocketTopology.AreCompatible(existing.CompatibleType, existing.Direction, socket.CompatibleType, socket.Direction) &&
                        Vector3.Dot(existing.Forward, socketTransform.forward) <= OppositeDirectionDotThreshold)
                    {
                        _edgeBuffer.Add(new EdgeRecord
                        {
                            SourceIndex = existing.ModuleIndex,
                            DestinationIndex = moduleIndex,
                            StartSocketPosition = existing.Position,
                            EndSocketPosition = socketTransform.position,
                            StartForward = existing.Forward,
                            EndForward = socketTransform.forward,
                            Flags = PipeRenderFlags.None
                        });
                    }

                    continue;
                }

                SocketKey ownKey = SocketKey.Create(socketTransform.position, axis, quantizationScale);
                _socketLookup[ownKey] = new SocketMatchEntry(moduleIndex, socket.CompatibleType, socket.Direction, socketTransform.position, socketTransform.forward);
            }
        }

        private void BuildNodeRecords()
        {
            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                ModuleRecord module = _moduleBuffer[nodeIndex];
                if (module.IsSyntheticParasiteRoot)
                {
                    float rootDrainWatts = math.max(0f, module.SyntheticPowerDrainWatts);
                    _nodes[nodeIndex] = new LogisticsNetworkGraph.LogisticsNode
                    {
                        Id = module.NodeId,
                        Capacity = math.max(1f, rootDrainWatts * 0.01f),
                        Resistance = MinimumEdgeResistance,
                        CurrentLoad = -rootDrainWatts,
                        Potential = 0f,
                        Priority = 0,
                        Flags = LogisticsNodeFlags.Active | LogisticsNodeFlags.Dirty,
                        NetworkId = 0,
                        Reserved = (byte)LogisticsModuleStatusBits.None
                    };
                    continue;
                }

                _nodes[nodeIndex] = new LogisticsNetworkGraph.LogisticsNode
                {
                    Id = module.NodeId,
                    Capacity = ResolveNodeCapacity(module.Marker, module.BaseModule),
                    Resistance = ResolveNodeResistance(module.BaseModule),
                    CurrentLoad = ResolveHydroStructuralLoadNewtons(module.BaseModule),
                    Potential = 0f,
                    Priority = ResolveNodePriority(module.Marker),
                    Flags = ResolveNodeFlags(module.BaseModule),
                    NetworkId = 0,
                    Reserved = (byte)ResolveReservedState(module.BaseModule, module.IsAnchorNode, false, false)
                };
            }
        }

        private void BuildEdgeRecords()
        {
            int reservedDirectedEdgeCapacity = math.max(1, _edgeBuffer.Count * 2);
            EnsureEdgeCapacity(reservedDirectedEdgeCapacity);
            int logicalDirectedEdgeCount = 0;

            for (int nodeIndex = 0; nodeIndex <= _nodeCount; nodeIndex++)
                _edgeOffsets[nodeIndex] = 0;

            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                float distance = math.distance(edge.StartSocketPosition, edge.EndSocketPosition);
                bool unsupported = !edge.IsSyntheticParasiteRoot &&
                                   distance > LogisticsPipeBuilder.UnsupportedSpanMeters &&
                                   !HasIntermediateSupport(edge.SourceIndex, edge.DestinationIndex, edge.StartSocketPosition, edge.EndSocketPosition);

                if (unsupported || HasImplodedEndpoint(edge))
                    MarkEdgeRuptured(ref edge);

                if (!edge.IsSyntheticParasiteRoot && !edge.Severed && TryApplyHydroShearRupture(ref edge))
                    MarkEdgeRuptured(ref edge);

                edge.Resistance = edge.IsSyntheticParasiteRoot
                    ? MinimumEdgeResistance
                    : math.max(MinimumEdgeResistance, distance * EdgeResistancePerMeter);
                _edgeBuffer[edgeIndex] = edge;

                if (edge.Severed)
                    continue;

                _edgeOffsets[edge.SourceIndex + 1] = _edgeOffsets[edge.SourceIndex + 1] + 1;
                if (edge.DirectedOnly)
                {
                    logicalDirectedEdgeCount++;
                }
                else
                {
                    _edgeOffsets[edge.DestinationIndex + 1] = _edgeOffsets[edge.DestinationIndex + 1] + 1;
                    logicalDirectedEdgeCount += 2;
                }
            }

            for (int nodeIndex = 1; nodeIndex <= _nodeCount; nodeIndex++)
                _edgeOffsets[nodeIndex] = _edgeOffsets[nodeIndex] + _edgeOffsets[nodeIndex - 1];

            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
                _edgeWriteCursor[nodeIndex] = _edgeOffsets[nodeIndex];

            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                if (edge.Severed)
                    continue;

                int forwardWriteIndex = _edgeWriteCursor[edge.SourceIndex];
                _edgeWriteCursor[edge.SourceIndex] = forwardWriteIndex + 1;
                _edgeDestinations[forwardWriteIndex] = edge.DestinationIndex;
                _edgeResistance[forwardWriteIndex] = edge.Resistance;

                if (edge.DirectedOnly)
                    continue;

                int reverseWriteIndex = _edgeWriteCursor[edge.DestinationIndex];
                _edgeWriteCursor[edge.DestinationIndex] = reverseWriteIndex + 1;
                _edgeDestinations[reverseWriteIndex] = edge.SourceIndex;
                _edgeResistance[reverseWriteIndex] = edge.Resistance;
            }

            _edgeCount = logicalDirectedEdgeCount;
        }

        private bool TryApplyHydroShearRupture(ref EdgeRecord edge)
        {
            ModuleRecord sourceRecord = _moduleBuffer[edge.SourceIndex];
            ModuleRecord destinationRecord = _moduleBuffer[edge.DestinationIndex];
            if (sourceRecord.IsEmergencyAirlock || destinationRecord.IsEmergencyAirlock)
                return false;

            BaseModule sourceModule = sourceRecord.BaseModule;
            BaseModule destinationModule = destinationRecord.BaseModule;
            if (sourceModule == null || destinationModule == null)
                return false;

            bool sourceFlooded = IsFloodedForHydroStress(sourceModule);
            bool destinationFlooded = IsFloodedForHydroStress(destinationModule);
            bool sourcePristine = IsPristineForHydroStress(sourceModule);
            bool destinationPristine = IsPristineForHydroStress(destinationModule);
            if (!((sourceFlooded && destinationPristine) || (destinationFlooded && sourcePristine)))
                return false;

            float sourceFloodMassKilograms = sourceModule.ResolveFloodWaterMassKilograms();
            float destinationFloodMassKilograms = destinationModule.ResolveFloodWaterMassKilograms();
            float massDeltaKilograms = math.abs(sourceFloodMassKilograms - destinationFloodMassKilograms);
            if (massDeltaKilograms <= 0f || !math.isfinite(massDeltaKilograms))
                return false;

            float shearThresholdKilograms = ResolveHydroShearThresholdKilograms(sourceModule, destinationModule);
            return massDeltaKilograms > shearThresholdKilograms;
        }

        private bool HasImplodedEndpoint(EdgeRecord edge)
        {
            BaseModule sourceModule = _moduleBuffer[edge.SourceIndex].BaseModule;
            BaseModule destinationModule = _moduleBuffer[edge.DestinationIndex].BaseModule;
            return (sourceModule != null && sourceModule.HasImploded) ||
                   (destinationModule != null && destinationModule.HasImploded);
        }

        private void MarkEdgeRuptured(ref EdgeRecord edge)
        {
            edge.Flags |= PipeRenderFlags.MaskRuptured;
            edge.Severed = true;
            RegisterSeveredEdgeRuptureVfx(in edge);
        }

        private void MarkNodeRuptured(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= _nodeCount)
                return;

            LogisticsNetworkGraph.LogisticsNode node = _nodes[nodeIndex];
            node.Flags |= LogisticsNodeFlags.Ruptured;
            _nodes[nodeIndex] = node;

            if (nodeIndex < _moduleBuffer.Count)
                ConnectionSplineBatchRenderer.SetPipeNodeRuptured(_moduleBuffer[nodeIndex].NodeId, true);
        }

        private void RegisterSeveredEdgeRuptureVfx(in EdgeRecord edge)
        {
            if (edge.IsSyntheticParasiteRoot ||
                edge.SourceIndex < 0 ||
                edge.SourceIndex >= _moduleBuffer.Count ||
                edge.DestinationIndex < 0 ||
                edge.DestinationIndex >= _moduleBuffer.Count)
            {
                return;
            }

            long linkId = ComposeLinkId(_moduleBuffer[edge.SourceIndex].NodeId, _moduleBuffer[edge.DestinationIndex].NodeId);
            for (int i = 0; i < _emittedRuptureEdgeVfxKeys.Count; i++)
            {
                if (_emittedRuptureEdgeVfxKeys[i] == linkId)
                    return;
            }

            AbyssalFluidDecalManager fluidDecals = Hecton8.Core.GlobalRegistry.AbyssalFluidDecals;
            if (fluidDecals == null || _emittedRuptureEdgeVfxKeys.Count >= _emittedRuptureEdgeVfxKeys.Capacity)
                return;

            Vector3 startAup = HectonFloatingOrigin.ToAbsoluteUniversePosition((Vector3)edge.StartSocketPosition);
            Vector3 endAup = HectonFloatingOrigin.ToAbsoluteUniversePosition((Vector3)edge.EndSocketPosition);
            Vector3 midpointRuntime = HectonFloatingOrigin.ToRuntimePosition((startAup + endAup) * 0.5f);
            float spanMeters = math.distance(edge.StartSocketPosition, edge.EndSocketPosition);
            float radiusScale = math.lerp(0.65f, 1.2f, math.saturate(spanMeters / LogisticsPipeBuilder.UnsupportedSpanMeters));
            fluidDecals.RegisterRuptureFluid(midpointRuntime, radiusScale);
            _emittedRuptureEdgeVfxKeys.Add(linkId);
        }

        private void EvaluateAnchorReachability()
        {
            if (_nodeCount <= 0 || !_anchorReachability.IsCreated || !_anchorTraversalQueue.IsCreated)
                return;

            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                _anchorReachability[nodeIndex] = 0;
                LogisticsNetworkGraph.LogisticsNode node = _nodes[nodeIndex];
                node.Flags &= ~LogisticsNodeFlags.Isolated;
                _nodes[nodeIndex] = node;
            }

            int queueHead = 0;
            int queueTail = 0;
            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                if (!_moduleBuffer[nodeIndex].IsAnchorNode)
                    continue;

                _anchorReachability[nodeIndex] = 1;
                _anchorTraversalQueue[queueTail++] = nodeIndex;
            }

            while (queueHead < queueTail)
            {
                int currentNodeIndex = _anchorTraversalQueue[queueHead++];
                int edgeStart = _edgeOffsets[currentNodeIndex];
                int edgeEnd = _edgeOffsets[currentNodeIndex + 1];
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int neighborNodeIndex = _edgeDestinations[edgeIndex];
                    if (_anchorReachability[neighborNodeIndex] != 0)
                        continue;

                    _anchorReachability[neighborNodeIndex] = 1;
                    _anchorTraversalQueue[queueTail++] = neighborNodeIndex;
                }
            }

            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                bool anchored = _anchorReachability[nodeIndex] != 0;
                LogisticsNetworkGraph.LogisticsNode node = _nodes[nodeIndex];
                if (!anchored)
                    node.Flags |= LogisticsNodeFlags.Isolated;

                node.Reserved = (byte)ResolveReservedState(
                    _moduleBuffer[nodeIndex].BaseModule,
                    _moduleBuffer[nodeIndex].IsAnchorNode,
                    anchored,
                    false);
                _nodes[nodeIndex] = node;
            }
        }

        private void PublishAnchorState()
        {
            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                BaseModule baseModule = _moduleBuffer[nodeIndex].BaseModule;
                if (baseModule != null)
                    baseModule.SetAnchoredState(_anchorReachability[nodeIndex] != 0);
            }
        }

        private void PublishComponentPowerState()
        {
            if (_nodeCount <= 0 || !_traversalVisited.IsCreated || !_anchorTraversalQueue.IsCreated)
                return;

            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
                _traversalVisited[nodeIndex] = 0;

            for (int startNodeIndex = 0; startNodeIndex < _nodeCount; startNodeIndex++)
            {
                if (_traversalVisited[startNodeIndex] != 0)
                    continue;

                int queueHead = 0;
                int queueTail = 0;
                _traversalVisited[startNodeIndex] = 1;
                _anchorTraversalQueue[queueTail++] = startNodeIndex;

                float componentSupply = 0f;
                float componentDraw = 0f;

                while (queueHead < queueTail)
                {
                    int currentNodeIndex = _anchorTraversalQueue[queueHead++];
                    float powerRating = ResolveModulePowerRating(_moduleBuffer[currentNodeIndex]);
                    if (powerRating >= 0f)
                        componentSupply += powerRating;
                    else
                        componentDraw -= powerRating;

                    int edgeStart = _edgeOffsets[currentNodeIndex];
                    int edgeEnd = _edgeOffsets[currentNodeIndex + 1];
                    for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                    {
                        int neighborNodeIndex = _edgeDestinations[edgeIndex];
                        if (_traversalVisited[neighborNodeIndex] != 0)
                            continue;

                        _traversalVisited[neighborNodeIndex] = 1;
                        _anchorTraversalQueue[queueTail++] = neighborNodeIndex;
                    }
                }

                bool componentLowPower = componentDraw > componentSupply + 0.001f &&
                                         PowerGridManager.ResolveProjectedBrownoutTier(componentSupply, componentDraw) != LogisticsBrownoutTier.None;
                for (int queueIndex = 0; queueIndex < queueTail; queueIndex++)
                {
                    int componentNodeIndex = _anchorTraversalQueue[queueIndex];
                    LogisticsNetworkGraph.LogisticsNode node = _nodes[componentNodeIndex];
                    if (componentLowPower)
                        node.Flags |= LogisticsNodeFlags.Brownout;
                    else
                        node.Flags &= ~LogisticsNodeFlags.Brownout;

                    _nodes[componentNodeIndex] = node;

                    BaseModule baseModule = _moduleBuffer[componentNodeIndex].BaseModule;
                    if (baseModule != null)
                        baseModule.SetAmbientLightsBrownout(componentLowPower);
                }
            }
        }

        private void PublishEmergencyLockdownState()
        {
            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                ModuleRecord module = _moduleBuffer[nodeIndex];
                BaseModule baseModule = module.BaseModule;
                if (baseModule == null)
                    continue;

                bool shouldLock = false;
                bool blockManualOverride = false;
                if (module.IsEmergencyAirlock)
                {
                    int edgeStart = _edgeOffsets[nodeIndex];
                    int edgeEnd = _edgeOffsets[nodeIndex + 1];
                    for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                    {
                        int adjacentNodeIndex = _edgeDestinations[edgeIndex];
                        if (adjacentNodeIndex < 0 || adjacentNodeIndex >= _nodeCount)
                            continue;

                        LogisticsNodeFlags adjacentFlags = _nodes[adjacentNodeIndex].Flags;
                        BaseModule adjacentModule = _moduleBuffer[adjacentNodeIndex].BaseModule;
                        bool adjacentRuptured = (adjacentFlags & LogisticsNodeFlags.Ruptured) != 0 ||
                                                (adjacentModule != null && adjacentModule.IntegrityState == BaseModuleIntegrityState.Ruptured);
                        bool adjacentFlooded = adjacentModule != null && adjacentModule.IsFlooded;
                        if (adjacentRuptured || adjacentFlooded)
                        {
                            shouldLock = true;
                            if (adjacentModule != null && adjacentModule.FloodLevel01 >= 0.2f)
                            {
                                blockManualOverride = true;
                                break;
                            }
                        }
                    }
                }

                baseModule.SetEmergencyBulkheadLockdown(shouldLock, blockManualOverride);
                LogisticsNetworkGraph.LogisticsNode node = _nodes[nodeIndex];
                node.Reserved = (byte)ResolveReservedState(
                    baseModule,
                    module.IsAnchorNode,
                    _anchorReachability[nodeIndex] != 0,
                    shouldLock);
                _nodes[nodeIndex] = node;
            }
        }

        private void PublishDegradationState()
        {
            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                ModuleRecord module = _moduleBuffer[nodeIndex];
                if (module.ModuleObject == null)
                    continue;

                BaseDegradationSystem.SynchronizeNode(
                    module.ModuleObject,
                    module.NodeId,
                    _nodes[nodeIndex].Flags,
                    ResolveNodeRuptureWorldPoint(nodeIndex));
            }
        }

        private void PublishSiegeTargetSnapshot()
        {
            if (!_siegeTargets.IsCreated)
                return;

            int writeCount = 0;
            int maxNodeCount = math.min(_nodeCount, _moduleBuffer.Count);
            for (int nodeIndex = 0; nodeIndex < maxNodeCount && writeCount < MaxSiegeTargetCount; nodeIndex++)
            {
                ModuleRecord module = _moduleBuffer[nodeIndex];
                BaseModule baseModule = module.BaseModule;
                if (module.IsSyntheticParasiteRoot || baseModule == null || !baseModule.isActiveAndEnabled)
                    continue;

                LogisticsNodeFlags nodeFlags = _nodes.IsCreated && nodeIndex < _nodes.Length
                    ? _nodes[nodeIndex].Flags
                    : LogisticsNodeFlags.None;
                float integrity01 = math.saturate(baseModule.IntegrityStateNormalized);
                HabitatSiegeTargetFlags siegeFlags = ResolveSiegeTargetFlags(module, baseModule, nodeFlags, integrity01);
                if ((siegeFlags & HabitatSiegeTargetFlags.Vulnerable) == 0)
                    continue;

                _siegeTargets[writeCount++] = new HabitatSiegeTargetSnapshot
                {
                    ModuleCenter = module.Position,
                    WeakPoint = ResolveNodeRuptureWorldPoint(nodeIndex),
                    Integrity01 = integrity01,
                    Vulnerability01 = ResolveSiegeVulnerability01(baseModule, nodeFlags, integrity01),
                    NodeId = module.NodeId,
                    Flags = (byte)siegeFlags
                };
            }

            for (int i = writeCount; i < _siegeTargetCount; i++)
                _siegeTargets[i] = default;

            _siegeTargetCount = writeCount;
            s_latestSiegeTargets = _siegeTargets;
            s_latestSiegeTargetOwner = this;
            s_latestSiegeTargetCount = writeCount;
        }

        private void ClearSiegeTargetSnapshot()
        {
            if (_siegeTargets.IsCreated)
            {
                for (int i = 0; i < _siegeTargetCount; i++)
                    _siegeTargets[i] = default;
            }

            _siegeTargetCount = 0;
            if (ReferenceEquals(s_latestSiegeTargetOwner, this))
            {
                s_latestSiegeTargets = default;
                s_latestSiegeTargetOwner = null;
                s_latestSiegeTargetCount = 0;
            }
        }

        private void PublishGraphKernel()
        {
            _graph.BeginBuild(LogisticsNetworkType.OxygenPressure, _nodeCount, math.max(1, _edgeCount), 0);

            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                LogisticsNetworkGraph.LogisticsNode node = _nodes[nodeIndex];
                _graph.AddNode(node.Id, node.Capacity, node.Resistance, node.Priority, node.Flags, node.Reserved);
            }

            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                if (edge.Severed)
                    continue;

                _graph.AddEdge(edge.SourceIndex, edge.DestinationIndex, edge.Resistance);
                if (!edge.DirectedOnly)
                    _graph.AddEdge(edge.DestinationIndex, edge.SourceIndex, edge.Resistance);
            }

            _graph.FinalizeBuild();
        }

        private void PublishRuntimeRuptureTopologyState()
        {
            BuildEdgeRecords();
            EvaluateAnchorReachability();
            PublishAnchorState();
            PublishComponentPowerState();
            PublishEmergencyLockdownState();
            PublishDegradationState();
            PublishSiegeTargetSnapshot();
            PublishGraphKernel();
            ClearVisualLinks();
            PublishVisualLinks();
        }

        private void PublishVisualLinks()
        {
            int edgeCount = _edgeBuffer.Count;
            if (_submittedLinkIds.Capacity < edgeCount)
                _submittedLinkIds.Capacity = edgeCount;

            for (int edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                if (edge.IsSyntheticParasiteRoot)
                    continue;

                long linkId = ComposeLinkId(_moduleBuffer[edge.SourceIndex].NodeId, _moduleBuffer[edge.DestinationIndex].NodeId);
                SplineDescriptor descriptor = LogisticsPipeBuilder.CreateSocketDescriptor(
                    edge.StartSocketPosition,
                    edge.EndSocketPosition,
                    edge.StartForward,
                    edge.EndForward,
                    LogisticsPipeBuilder.DefaultPipeRadiusMeters,
                    edge.Flags);

                ConnectionSplineBatchRenderer.SubmitPipeLink(linkId, descriptor, PipeSplineColor);
                _submittedLinkIds.Add(linkId);
            }
        }

        private void ClearVisualLinks()
        {
            for (int i = 0; i < _submittedLinkIds.Count; i++)
                ConnectionSplineBatchRenderer.RemovePipeLink(_submittedLinkIds[i]);

            _submittedLinkIds.Clear();
        }

        private Vector3 ResolveNodeRuptureWorldPoint(int nodeIndex)
        {
            for (int edgeIndex = 0; edgeIndex < _edgeBuffer.Count; edgeIndex++)
            {
                EdgeRecord edge = _edgeBuffer[edgeIndex];
                if (!LogisticsPipeBuilder.HasRupturedMask(edge.Flags))
                    continue;

                if (edge.SourceIndex == nodeIndex)
                    return edge.StartSocketPosition;

                if (edge.DestinationIndex == nodeIndex)
                    return edge.EndSocketPosition;
            }

            return _moduleBuffer[nodeIndex].Position;
        }

        private bool HasIntermediateSupport(int sourceIndex, int destinationIndex, float3 start, float3 end)
        {
            for (int moduleIndex = 0; moduleIndex < _moduleBuffer.Count; moduleIndex++)
            {
                if (moduleIndex == sourceIndex || moduleIndex == destinationIndex)
                    continue;

                if (_moduleBuffer[moduleIndex].IsSyntheticParasiteRoot)
                    continue;

                if (!IsPipeSpanSupportModule(_moduleBuffer[moduleIndex]))
                    continue;

                float projection;
                float distanceSq = DistancePointToSegmentSq(_moduleBuffer[moduleIndex].Position, start, end, out projection);
                if (projection > 0.1f &&
                    projection < 0.9f &&
                    distanceSq <= SupportCaptureRadiusSq)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPipeSpanSupportModule(ModuleRecord module)
        {
            if (module.IsAnchorNode)
                return true;

            ModuleMarker marker = module.Marker;
            string persistentId = marker != null ? marker.PrefabId : string.Empty;
            return string.Equals(persistentId, "Build_Utility_Pylon", StringComparison.Ordinal);
        }

        private static float DistancePointToSegmentSq(float3 point, float3 start, float3 end, out float projection)
        {
            float3 segment = end - start;
            float segmentLengthSq = math.lengthsq(segment);
            if (segmentLengthSq <= 0.000001f)
            {
                projection = 0f;
                return math.lengthsq(point - start);
            }

            projection = math.saturate(math.dot(point - start, segment) / segmentLengthSq);
            float3 closestPoint = start + segment * projection;
            return math.lengthsq(point - closestPoint);
        }

        private static float ResolveNodeCapacity(ModuleMarker marker, BaseModule baseModule)
        {
            float capacity = 8f;
            if (marker != null && marker.Data != null)
                capacity += math.abs(marker.Data.powerRating) * 0.01f;

            if (baseModule != null)
                capacity += math.max(0f, baseModule.MaxIntegrity * 0.05f);

            return math.max(1f, capacity);
        }

        private static float ResolveHydroStructuralLoadNewtons(BaseModule baseModule)
        {
            if (baseModule == null)
                return 0f;

            float floodWaterMassKilograms = baseModule.ResolveFloodWaterMassKilograms();
            float parasiteMassKilograms = baseModule.ResolveParasiteAddedMassKilograms();
            float structuralMassKilograms = floodWaterMassKilograms + parasiteMassKilograms;
            if (structuralMassKilograms <= 0f || !math.isfinite(structuralMassKilograms))
                return 0f;

            float loadNewtons = structuralMassKilograms * GravityAccelerationMetersPerSecondSquared;
            return math.isfinite(loadNewtons) ? math.max(0f, loadNewtons) : 0f;
        }

        private static float ResolveNodeResistance(BaseModule baseModule)
        {
            if (baseModule == null)
                return 0.25f;

            float resistance = 0.15f;
            if (baseModule.IsFlooded)
                resistance += 0.15f;

            if (!baseModule.HasPower)
                resistance += 0.1f;

            return resistance;
        }

        private static float ResolveProjectedDragAreaSquareMeters(BaseModule baseModule)
        {
            if (baseModule == null)
                return 0f;

            Hecton8.Building.BaseModuleTemplate template = baseModule.ModuleTemplate;
            if (template != null)
                return math.max(0.1f, template.ProjectedDragAreaSquareMeters);

            return 12f;
        }

        private static float ResolveYieldStrengthNewtons(BaseModule baseModule)
        {
            if (baseModule == null)
                return 0f;

            Hecton8.Building.BaseModuleTemplate template = baseModule.ModuleTemplate;
            if (template != null)
                return math.max(1f, template.ModuleYieldStrengthNewtons);

            return 180000f;
        }

        private static float ResolveHydroShearThresholdKilograms(BaseModule sourceModule, BaseModule destinationModule)
        {
            float sourceYieldMassKilograms = ResolveYieldStrengthNewtons(sourceModule) / GravityAccelerationMetersPerSecondSquared;
            float destinationYieldMassKilograms = ResolveYieldStrengthNewtons(destinationModule) / GravityAccelerationMetersPerSecondSquared;
            float weakestYieldMassKilograms = math.min(sourceYieldMassKilograms, destinationYieldMassKilograms);
            if (weakestYieldMassKilograms <= 0f || !math.isfinite(weakestYieldMassKilograms))
                weakestYieldMassKilograms = DefaultHydroShearThresholdKilograms;

            return math.max(1f, math.min(DefaultHydroShearThresholdKilograms, weakestYieldMassKilograms));
        }

        private static bool IsFloodedForHydroStress(BaseModule baseModule)
        {
            if (baseModule == null)
                return false;

            BaseModuleIntegrityState state = baseModule.IntegrityState;
            return baseModule.IsFlooded ||
                   state == BaseModuleIntegrityState.Flooded ||
                   state == BaseModuleIntegrityState.Ruptured;
        }

        private static bool IsPristineForHydroStress(BaseModule baseModule)
        {
            return baseModule != null &&
                   !baseModule.IsFlooded &&
                   !baseModule.IsBreached &&
                   baseModule.IntegrityState == BaseModuleIntegrityState.Pristine;
        }

        private static byte ResolveNodePriority(ModuleMarker marker)
        {
            if (marker == null || marker.Data == null)
                return 48;

            switch (marker.Data.family)
            {
                case BuildableFamily.Habitat:
                    return 12;

                case BuildableFamily.Utility:
                    return 24;

                case BuildableFamily.Logistics:
                    return 36;

                default:
                    return 48;
            }
        }

        private static LogisticsNodeFlags ResolveNodeFlags(BaseModule baseModule)
        {
            LogisticsNodeFlags flags = LogisticsNodeFlags.Active;
            if (baseModule != null && baseModule.HasCascadeFailure)
                flags |= LogisticsNodeFlags.Dirty;
            if (baseModule != null && (baseModule.HasImploded || BaseDegradationSystem.IsModuleRuptured(baseModule)))
                flags |= LogisticsNodeFlags.Ruptured;

            return flags;
        }

        private static float ResolveFungalMindPotentialScore(ModuleRecord module, LogisticsNetworkGraph.LogisticsNode node)
        {
            float nodePotential = math.abs(node.Potential);
            float nodeLoad = math.abs(node.CurrentLoad);
            float modulePower = math.abs(ResolveModulePowerRating(module));
            float score = math.max(nodePotential, math.max(nodeLoad, modulePower));
            return math.isfinite(score) ? score : 0f;
        }

        private static float ResolveModulePowerRating(ModuleRecord module)
        {
            if (module.IsSyntheticParasiteRoot)
                return -math.max(0f, module.SyntheticPowerDrainWatts);

            if (module.BaseModule != null)
                return module.BaseModule.PowerRatingForHabitatGraph;

            if (module.Marker != null && module.Marker.Data != null)
                return module.Marker.Data.powerRating;

            return 0f;
        }

        private static LogisticsModuleStatusBits ResolveReservedState(BaseModule baseModule, bool isAnchorNode, bool isAnchored, bool emergencyLockdown)
        {
            LogisticsModuleStatusBits bits = LogisticsModuleStatusBits.None;
            if (baseModule != null && baseModule.HasPower)
                bits |= LogisticsModuleStatusBits.Powered;
            if (baseModule != null && baseModule.IsFlooded)
                bits |= LogisticsModuleStatusBits.Flooded;
            if (baseModule != null && baseModule.CurrentIntegrity < baseModule.MaxIntegrity)
                bits |= LogisticsModuleStatusBits.Damaged;
            if (isAnchorNode)
                bits |= LogisticsModuleStatusBits.AnchorNode;
            if (isAnchored)
                bits |= LogisticsModuleStatusBits.Anchored;
            else
                bits |= LogisticsModuleStatusBits.Unmoored;
            if (emergencyLockdown)
                bits |= LogisticsModuleStatusBits.EmergencyLockdown;

            return bits;
        }

        private static HabitatSiegeTargetFlags ResolveSiegeTargetFlags(
            ModuleRecord module,
            BaseModule baseModule,
            LogisticsNodeFlags nodeFlags,
            float integrity01)
        {
            HabitatSiegeTargetFlags flags = HabitatSiegeTargetFlags.None;
            if (module.IsEmergencyAirlock)
                flags |= HabitatSiegeTargetFlags.EmergencyAirlock;

            if (baseModule.IsFlooded || baseModule.IntegrityState == BaseModuleIntegrityState.Flooded)
                flags |= HabitatSiegeTargetFlags.Flooded;

            if (baseModule.IsBreached || baseModule.IntegrityState == BaseModuleIntegrityState.Ruptured)
                flags |= HabitatSiegeTargetFlags.Ruptured;

            if (baseModule.HasCascadeFailure || baseModule.CurrentFailureMode != BaseModuleFailureMode.None)
                flags |= HabitatSiegeTargetFlags.CascadeFailure;

            if ((nodeFlags & LogisticsNodeFlags.Brownout) != 0 || !baseModule.HasPower)
                flags |= HabitatSiegeTargetFlags.Brownout;

            if ((nodeFlags & LogisticsNodeFlags.Isolated) != 0)
                flags |= HabitatSiegeTargetFlags.Isolated;

            bool vulnerable =
                integrity01 <= SiegeVulnerableIntegrityThreshold01 ||
                (flags & (HabitatSiegeTargetFlags.Flooded |
                          HabitatSiegeTargetFlags.Ruptured |
                          HabitatSiegeTargetFlags.Brownout |
                          HabitatSiegeTargetFlags.Isolated |
                          HabitatSiegeTargetFlags.CascadeFailure)) != 0;
            if (vulnerable)
                flags |= HabitatSiegeTargetFlags.Vulnerable;

            return flags;
        }

        private static float ResolveSiegeVulnerability01(BaseModule baseModule, LogisticsNodeFlags nodeFlags, float integrity01)
        {
            float vulnerability = 1f - integrity01;
            if (baseModule.IsFlooded || baseModule.IntegrityState == BaseModuleIntegrityState.Flooded)
                vulnerability += 0.35f;

            if (baseModule.IsBreached || baseModule.IntegrityState == BaseModuleIntegrityState.Ruptured)
                vulnerability += 0.55f;

            if (baseModule.HasCascadeFailure || baseModule.CurrentFailureMode != BaseModuleFailureMode.None)
                vulnerability += 0.2f;

            if ((nodeFlags & LogisticsNodeFlags.Brownout) != 0 || !baseModule.HasPower)
                vulnerability += 0.15f;

            if ((nodeFlags & LogisticsNodeFlags.Isolated) != 0)
                vulnerability += 0.15f;

            return math.saturate(vulnerability);
        }

        private static bool ResolveStructuralAnchorState(BaseModule baseModule, ModuleMarker marker)
        {
            if (baseModule != null && baseModule.ResolveStructuralAnchorRole(marker))
                return true;

            string persistentId = marker != null ? marker.PrefabId : string.Empty;
            return string.Equals(persistentId, "Build_Foundation_Platform", StringComparison.Ordinal) ||
                   string.Equals(persistentId, "Build_Utility_Pylon", StringComparison.Ordinal);
        }

        private static bool ResolveEmergencyAirlockState(BaseModule baseModule, ModuleMarker marker)
        {
            if (baseModule != null && baseModule.ResolveEmergencyAirlockRole(marker))
                return true;

            string persistentId = marker != null ? marker.PrefabId : string.Empty;
            return string.Equals(persistentId, "Build_Airlock_Hatch", StringComparison.Ordinal) ||
                   string.Equals(persistentId, "base.module.airlock", StringComparison.Ordinal);
        }

        private static int QuantizeAxis(Vector3 direction)
        {
            float3 normalized = math.normalizesafe((float3)direction, new float3(0f, 0f, 1f));
            float absX = math.abs(normalized.x);
            float absY = math.abs(normalized.y);
            float absZ = math.abs(normalized.z);

            if (absX >= absY && absX >= absZ)
                return normalized.x >= 0f ? 0 : 1;

            if (absY >= absX && absY >= absZ)
                return normalized.y >= 0f ? 2 : 3;

            return normalized.z >= 0f ? 4 : 5;
        }

        private static int OppositeAxis(int axis)
        {
            switch (axis)
            {
                case 0: return 1;
                case 1: return 0;
                case 2: return 3;
                case 3: return 2;
                case 4: return 5;
                default: return 4;
            }
        }

        private static long ComposeLinkId(uint left, uint right)
        {
            uint min = math.min(left, right);
            uint max = math.max(left, right);
            return ((long)min << 32) | max;
        }

        private void AllocateNativeBuffers(int nodeCapacity, int edgeCapacity)
        {
            // COLD ALLOC: NativeArray<LogisticsNode>[64] — habitat node snapshot buffer — owner: HabitatGraphManager
            _nodes = new NativeArray<LogisticsNetworkGraph.LogisticsNode>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Int32>[65] — habitat CSR edge-offset buffer — owner: HabitatGraphManager
            _edgeOffsets = new NativeArray<int>(nodeCapacity + 1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Int32>[128] — habitat CSR destination buffer — owner: HabitatGraphManager
            _edgeDestinations = new NativeArray<int>(edgeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Single>[128] — habitat CSR edge-resistance buffer — owner: HabitatGraphManager
            _edgeResistance = new NativeArray<float>(edgeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Int32>[64] — CSR write-cursor scratch buffer — owner: HabitatGraphManager
            _edgeWriteCursor = new NativeArray<int>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Byte>[64] — authoritative anchor reachability state for habitat graph consumers — owner: HabitatGraphManager
            _anchorReachability = new NativeArray<byte>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Byte>[64] — graph traversal visited scratch, separate from anchor-state truth — owner: HabitatGraphManager
            _traversalVisited = new NativeArray<byte>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Int32>[64] — reusable BFS traversal queue for graph component walks — owner: HabitatGraphManager
            _anchorTraversalQueue = new NativeArray<int>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<HabitatSiegeTargetSnapshot>[64] — capped habitat weak-point snapshot for headless predator siege jobs — owner: HabitatGraphManager
            _siegeTargets = new NativeArray<HabitatSiegeTargetSnapshot>(MaxSiegeTargetCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeMemorySentinel();
        }

        private void EnsureNodeCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (_nodes.IsCreated &&
                _nodes.Length >= safeLength &&
                _edgeOffsets.Length >= safeLength + 1 &&
                _edgeWriteCursor.Length >= safeLength &&
                _anchorReachability.Length >= safeLength &&
                _traversalVisited.IsCreated &&
                _traversalVisited.Length >= safeLength &&
                _anchorTraversalQueue.Length >= safeLength &&
                _siegeTargets.IsCreated &&
                _siegeTargets.Length >= MaxSiegeTargetCount)
                return;

            DisposeNativeBuffers();
            int nodeCapacity = NextPowerOfTwo(math.max(safeLength, InitialNodeCapacity));
            int edgeCapacity = NextPowerOfTwo(math.max(nodeCapacity * 4, InitialEdgeCapacity));
            AllocateNativeBuffers(nodeCapacity, edgeCapacity);
        }

        private void EnsureEdgeCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (_edgeDestinations.IsCreated && _edgeDestinations.Length >= safeLength && _edgeResistance.Length >= safeLength)
                return;

            DisposeNativeArray(ref _edgeDestinations);
            DisposeNativeArray(ref _edgeResistance);

            int edgeCapacity = NextPowerOfTwo(math.max(safeLength, InitialEdgeCapacity));
            // COLD ALLOC: NativeArray<Int32>[edgeCapacity] - expanded habitat CSR destination buffer - owner: HabitatGraphManager
            _edgeDestinations = new NativeArray<int>(edgeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Single>[edgeCapacity] - expanded habitat CSR edge-resistance buffer - owner: HabitatGraphManager
            _edgeResistance = new NativeArray<float>(edgeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(_edgeDestinations, NativeMemoryOwner, nameof(_edgeDestinations), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_edgeResistance, NativeMemoryOwner, nameof(_edgeResistance), NativeMemoryLifetime);
        }

        private void DisposeNativeBuffers()
        {
            ClearSiegeTargetSnapshot();

            DisposeNativeArray(ref _nodes);
            DisposeNativeArray(ref _edgeOffsets);
            DisposeNativeArray(ref _edgeDestinations);
            DisposeNativeArray(ref _edgeResistance);
            DisposeNativeArray(ref _edgeWriteCursor);
            DisposeNativeArray(ref _anchorReachability);
            DisposeNativeArray(ref _traversalVisited);
            DisposeNativeArray(ref _anchorTraversalQueue);
            DisposeNativeArray(ref _siegeTargets);
        }

        private void RegisterNativeMemorySentinel()
        {
            NativeMemorySentinel.RegisterNativeArray(_nodes, NativeMemoryOwner, nameof(_nodes), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_edgeOffsets, NativeMemoryOwner, nameof(_edgeOffsets), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_edgeDestinations, NativeMemoryOwner, nameof(_edgeDestinations), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_edgeResistance, NativeMemoryOwner, nameof(_edgeResistance), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_edgeWriteCursor, NativeMemoryOwner, nameof(_edgeWriteCursor), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_anchorReachability, NativeMemoryOwner, nameof(_anchorReachability), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_traversalVisited, NativeMemoryOwner, nameof(_traversalVisited), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_anchorTraversalQueue, NativeMemoryOwner, nameof(_anchorTraversalQueue), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_siegeTargets, NativeMemoryOwner, nameof(_siegeTargets), NativeMemoryLifetime);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private static int NextPowerOfTwo(int value)
        {
            if (value <= 1)
                return 1;

            int power = 1;
            while (power < value && power > 0)
                power <<= 1;

            return power > 0 ? power : int.MaxValue;
        }

        private struct ModuleRecord
        {
            public GameObject ModuleObject;
            public ModuleMarker Marker;
            public BaseModule BaseModule;
            public float3 Position;
            public uint NodeId;
            public bool IsAnchorNode;
            public bool IsEmergencyAirlock;
            public float SyntheticPowerDrainWatts;
            public bool IsSyntheticParasiteRoot;
        }

        private struct EdgeRecord
        {
            public int SourceIndex;
            public int DestinationIndex;
            public float3 StartSocketPosition;
            public float3 EndSocketPosition;
            public float3 StartForward;
            public float3 EndForward;
            public float Resistance;
            public PipeRenderFlags Flags;
            public bool Severed;
            public bool IsSyntheticParasiteRoot;
            public bool DirectedOnly;
        }

        private struct TemporaryBypassRecord
        {
            public uint SourceNodeId;
            public uint DestinationNodeId;
            public int SourceModuleHashId;
            public int DestinationModuleHashId;
            public float3 SourcePosition;
            public float3 DestinationPosition;
        }

        private readonly struct SocketKey : IEquatable<SocketKey>
        {
            private readonly int _x;
            private readonly int _y;
            private readonly int _z;
            private readonly int _axis;

            private SocketKey(int x, int y, int z, int axis)
            {
                _x = x;
                _y = y;
                _z = z;
                _axis = axis;
            }

            public static SocketKey Create(Vector3 position, int axis, int quantizationScale)
            {
                float scale = quantizationScale > 0 ? quantizationScale : 1f;
                float3 scaledPosition = (float3)position * scale;
                int3 quantizedPosition = (int3)math.round(scaledPosition);
                return new SocketKey(quantizedPosition.x, quantizedPosition.y, quantizedPosition.z, axis);
            }

            public bool Equals(SocketKey other)
            {
                return _x == other._x &&
                       _y == other._y &&
                       _z == other._z &&
                       _axis == other._axis;
            }

            public override bool Equals(object obj)
            {
                return obj is SocketKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _x;
                    hash = (hash * 397) ^ _y;
                    hash = (hash * 397) ^ _z;
                    hash = (hash * 397) ^ _axis;
                    return hash;
                }
            }
        }

        private readonly struct SocketMatchEntry
        {
            public readonly int ModuleIndex;
            public readonly string CompatibleType;
            public readonly ModuleSocketDirection Direction;
            public readonly float3 Position;
            public readonly float3 Forward;

            public SocketMatchEntry(int moduleIndex, string compatibleType, ModuleSocketDirection direction, float3 position, float3 forward)
            {
                ModuleIndex = moduleIndex;
                CompatibleType = compatibleType;
                Direction = direction;
                Position = position;
                Forward = forward;
            }
        }
    }
}
