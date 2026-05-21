using System;
using System.Runtime.InteropServices;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Inventory;
using Hecton8.World;
using Hecton.Localization;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Owns rigid-grid snapping, socket alignment, structural validation, and zero-GC build-cost transactions
    /// for the live habitat placement preview.
    /// </summary>
    internal sealed class HabitatConstructionManager : IDisposable
    {
        internal const string PendingReason = "STRUCTURAL ANALYSIS PENDING";
        internal const string UnsupportedReason = "NO STRUCTURAL SUPPORT PATH";
        internal const string IntegrityFailureReason = "STRUCTURAL INTEGRITY EXCEEDED";

        private const float DefaultGridSize = 4f;
        private const float DefaultSocketQuantization = 0.05f;
        private const float DefaultDepthPenalty = 0.75f;
        private const float DefaultIntegrityBudget = 240f;
        private const float DefaultDisconnectedPenalty = 1000f;
        private const float OppositeDirectionDotThreshold = -0.85f;
        private const int InitialNodeCapacity = 96;
        private const int InitialAdjacencyCapacity = 384;
        private const int InitialConnectionCapacity = 384;
        private const int InitialSocketLookupCapacity = 512;
        private const int MaxCostCapacity = 32;
        private const BufferID IntegrityNodeBufferId = (BufferID)70949;
        private const BufferID IntegrityRangeBufferId = (BufferID)70950;
        private const BufferID IntegrityAdjacencyBufferId = (BufferID)70951;
        private const BufferID IntegrityQueueBufferId = (BufferID)70952;
        private const BufferID IntegrityDepthBufferId = (BufferID)70953;
        private const BufferID IntegrityResultBufferId = (BufferID)70954;
        private const BufferID IntegrityDegreeScratchBufferId = (BufferID)70955;
        private const BufferID IntegrityWriteScratchBufferId = (BufferID)70956;
        private const BufferID IntegrityConnectionBufferId = (BufferID)70957;
        private const BufferID IntegritySocketLookupBufferId = (BufferID)70958;
        private const uint IntegrityNodeLockBit = 1u << 0;
        private const uint IntegrityRangeLockBit = 1u << 1;
        private const uint IntegrityAdjacencyLockBit = 1u << 2;
        private const uint IntegrityQueueLockBit = 1u << 3;
        private const uint IntegrityDepthLockBit = 1u << 4;
        private const uint IntegrityResultLockBit = 1u << 5;

        private VaultGenerationHandle<IntegrityNodeRecord> _nodeBufferHandle;
        private VaultGenerationHandle<int2> _adjacencyRangesHandle;
        private VaultGenerationHandle<int> _adjacencyHandle;
        private VaultGenerationHandle<int> _queueBufferHandle;
        private VaultGenerationHandle<int> _depthBufferHandle;
        private VaultGenerationHandle<IntegrityValidationResult> _resultBufferHandle;
        private VaultGenerationHandle<int> _adjacencyDegreeScratchHandle;
        private VaultGenerationHandle<int> _adjacencyWriteScratchHandle;
        private VaultGenerationHandle<int2> _connectionBufferHandle;
        private VaultGenerationHandle<SocketLookupSlot> _socketLookupHandle;

        private JobHandle _validationHandle;
        private IDataVault _catalogVault;
        private int _nodeCapacity;
        private int _adjacencyCapacity;
        private int _connectionCapacity;
        private int _socketLookupCapacity;
        private int _connectionCount;
        private int _socketLookupCount;
        private uint _lockedValidationBufferMask;
        private int _cachedExistingGraphSignature;
        private int _cachedExistingGraphGridSize;
        private int _cachedExistingGraphModuleListCount;
        private int _cachedExistingGraphNodeCount;
        private int _cachedExistingGraphConnectionCount;
        private bool _hasExistingGraphCache;
        private bool _validationPending;
        private bool _discardValidationResult;
        private float _lastIntegrityScore;
        private bool _lastPlacementAllowed = true;
        private string _lastBlockReason = string.Empty;

        public bool IsValidationPending => _validationPending;
        public float LastIntegrityScore => _lastIntegrityScore;
        public bool LastPlacementAllowed => _lastPlacementAllowed;
        public string LastBlockReason => _lastBlockReason;

        public void BindCatalogVault(IDataVault catalogVault)
        {
            if (ReferenceEquals(_catalogVault, catalogVault))
            {
                if (_catalogVault != null)
                    EnsureNodeCapacity(InitialNodeCapacity);
                return;
            }

            if (_validationPending)
                return;

            ReleaseValidationVaultHandles();
            InvalidateExistingGraphCache();
            _catalogVault = catalogVault;
            if (_catalogVault != null)
                EnsureNodeCapacity(InitialNodeCapacity);
        }

        public void Dispose()
        {
            CompletePendingValidationForTeardown();
            ReleaseValidationVaultHandles();
            JobHandle.ScheduleBatchedJobs();
        }

        public void ResetValidation()
        {
            if (_validationPending)
            {
                _discardValidationResult = true;
            }
            else
            {
                _discardValidationResult = false;
                _validationHandle = default;
                UnlockValidationBuffers();
            }

            _lastIntegrityScore = 0f;
            _lastPlacementAllowed = true;
            _lastBlockReason = string.Empty;
        }

        public float3 SnapWorldPosition(float3 worldPosition, float gridSize)
        {
            float snappedGrid = gridSize > 0f ? gridSize : DefaultGridSize;
            int gridMillimeters = ResolveGridMillimeters(snappedGrid);
            if (!TryResolveAupFromRuntimeOrigin(
                    new Vector3(worldPosition.x, worldPosition.y, worldPosition.z),
                    out double3 absolutePosition))
            {
                return worldPosition;
            }

            double3 snappedAbsolutePosition = new double3(
                SnapMeterToGridMillimeters(absolutePosition.x, gridMillimeters),
                SnapMeterToGridMillimeters(absolutePosition.y, gridMillimeters),
                SnapMeterToGridMillimeters(absolutePosition.z, gridMillimeters));
            Vector3 runtimePosition = HectonFloatingOrigin.ToRuntimePosition(snappedAbsolutePosition);
            return new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
        }

        private static int ResolveGridMillimeters(float gridSize)
        {
            float safeGrid = gridSize > 0.001f && float.IsFinite(gridSize) ? gridSize : DefaultGridSize;
            int millimeters = Mathf.RoundToInt(safeGrid * 1000f);
            return Mathf.Max(1, millimeters);
        }

        private static float SnapMeterToGridMillimeters(float meters, int gridMillimeters)
        {
            if (!float.IsFinite(meters))
                return 0f;

            return (float)SnapMeterToGridMillimeters((double)meters, gridMillimeters);
        }

        private static double SnapMeterToGridMillimeters(double meters, int gridMillimeters)
        {
            if (!math.isfinite(meters))
                return 0d;

            double millimetersDouble = meters * 1000.0;
            long millimeters = millimetersDouble >= 0.0
                ? (long)(millimetersDouble + 0.5)
                : (long)(millimetersDouble - 0.5);
            long snappedMillimeters = SnapIntegerToGrid(millimeters, gridMillimeters);
            return snappedMillimeters * 0.001;
        }

        private static long SnapIntegerToGrid(long value, int grid)
        {
            long safeGrid = grid > 0 ? grid : 1;
            long halfGrid = safeGrid >> 1;
            if (value >= 0)
                return ((value + halfGrid) / safeGrid) * safeGrid;

            return ((value - halfGrid) / safeGrid) * safeGrid;
        }

        public bool HasBuildResources(PlayerInventory inventory, BuildableData data)
        {
            if (data == null || data.buildCost == null || data.buildCost.Count == 0)
                return true;

            if (inventory == null || inventory.Grid == null)
                return false;

            Span<int> costHashes = stackalloc int[MaxCostCapacity];
            Span<int> costRemaining = stackalloc int[MaxCostCapacity];
            int costCount = PrepareCostBuffers(data, costHashes, costRemaining);
            if (costCount == 0)
                return true;
            if (costCount < 0)
                return false;

            NativeArray<int>.ReadOnly itemIds = inventory.GetItemIDsReadOnly();
            NativeArray<ushort>.ReadOnly stackCounts = inventory.GetStackCountsReadOnly();
            NativeArray<ushort>.ReadOnly craftLockedCounts = inventory.GetCraftLockedCountsReadOnly();
            int itemCount = math.min(itemIds.Length, math.min(stackCounts.Length, craftLockedCounts.Length));
            if (itemCount <= 0)
                return false;

            for (int itemIndex = 0; itemIndex < itemCount; itemIndex++)
            {
                int itemHashId = itemIds[itemIndex];
                if (itemHashId == 0)
                    continue;

                int availableCount = math.max(0, math.max(1, (int)stackCounts[itemIndex]) - craftLockedCounts[itemIndex]);
                if (availableCount <= 0)
                    continue;

                for (int costIndex = 0; costIndex < costCount; costIndex++)
                {
                    if (costRemaining[costIndex] <= 0 || costHashes[costIndex] != itemHashId)
                        continue;

                    costRemaining[costIndex] -= availableCount;
                }
            }

            for (int costIndex = 0; costIndex < costCount; costIndex++)
            {
                if (costRemaining[costIndex] > 0)
                    return false;
            }

            return true;
        }

        public bool ConsumeBuildResources(PlayerInventory inventory, BuildableData data)
        {
            if (data == null || data.buildCost == null || data.buildCost.Count == 0)
                return true;

            if (inventory == null || inventory.Grid == null)
                return false;

            Span<int> costHashes = stackalloc int[MaxCostCapacity];
            Span<int> costRemaining = stackalloc int[MaxCostCapacity];
            Span<int> costRemoved = stackalloc int[MaxCostCapacity];
            int costCount = PrepareCostBuffers(data, costHashes, costRemaining);
            if (costCount == 0)
                return true;
            if (costCount < 0)
                return false;

            for (int i = 0; i < costCount; i++)
                costRemoved[i] = 0;

            for (int costIndex = 0; costIndex < costCount; costIndex++)
            {
                int remaining = costRemaining[costIndex];
                while (remaining > 0)
                {
                    if (!inventory.TryRemoveFirstMatchingItemByHash(costHashes[costIndex]))
                    {
                        RollbackRemovedResources(inventory, costCount, costHashes, costRemoved);
                        return false;
                    }

                    costRemoved[costIndex]++;
                    remaining--;
                }
            }

            return true;
        }

        public bool ScheduleIntegrityValidation(
            ConstructionManager constructionManager,
            BuildableData candidateData,
            Vector3 candidatePosition,
            Quaternion candidateRotation,
            float gridSize,
            float integrityBudget,
            float depthPenalty)
        {
            if (_validationPending)
                return false;

            if (candidateData == null || !IsFinitePose(candidatePosition, candidateRotation))
            {
                _lastPlacementAllowed = false;
                _lastIntegrityScore = -1f;
                _lastBlockReason = UnsupportedReason;
                return false;
            }

            int nodeCount = BuildValidationGraph(
                constructionManager,
                candidatePosition,
                candidateRotation,
                candidateData,
                gridSize,
                out IntegrityGraphBuffers graphBuffers);
            if (nodeCount <= 0)
            {
                _lastPlacementAllowed = false;
                _lastIntegrityScore = -1f;
                _lastBlockReason = UnsupportedReason;
                return false;
            }

            if (!TryLockValidationBuffers())
            {
                _lastPlacementAllowed = false;
                _lastIntegrityScore = -1f;
                _lastBlockReason = UnsupportedReason;
                return false;
            }

            var job = new IntegrityValidationJob
            {
                Nodes = graphBuffers.Nodes,
                NodeCount = nodeCount,
                AdjacencyRanges = graphBuffers.AdjacencyRanges,
                Adjacency = graphBuffers.Adjacency,
                Queue = graphBuffers.Queue,
                Depths = graphBuffers.Depths,
                Result = graphBuffers.Result,
                IntegrityBudget = integrityBudget > 0f ? integrityBudget : DefaultIntegrityBudget,
                DepthPenalty = depthPenalty > 0f ? depthPenalty : DefaultDepthPenalty,
                DisconnectedPenalty = DefaultDisconnectedPenalty
            };

            _validationHandle = job.Schedule();
            _validationPending = true;
            _discardValidationResult = false;
            _lastPlacementAllowed = false;
            _lastBlockReason = PendingReason;
            return true;
        }

        public bool TryConsumeCompletedValidation()
        {
            if (!_validationPending)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _validationHandle))
                return false;

            bool discardResult = _discardValidationResult;
            _discardValidationResult = false;
            _validationPending = false;

            if (!TryResolveResultBuffer(out NativeArray<IntegrityValidationResult> resultBuffer))
            {
                UnlockValidationBuffers();
                if (discardResult)
                {
                    _lastIntegrityScore = 0f;
                    _lastPlacementAllowed = true;
                    _lastBlockReason = string.Empty;
                }
                else
                {
                    _lastIntegrityScore = -1f;
                    _lastPlacementAllowed = false;
                    _lastBlockReason = UnsupportedReason;
                }

                return true;
            }

            IntegrityValidationResult result = resultBuffer[0];
            UnlockValidationBuffers();
            if (discardResult)
            {
                _lastIntegrityScore = 0f;
                _lastPlacementAllowed = true;
                _lastBlockReason = string.Empty;
                return true;
            }

            _lastIntegrityScore = result.Integrity;
            _lastPlacementAllowed = result.Allowed != 0;

            switch ((IntegrityFailureReasonCode)result.FailureReason)
            {
                case IntegrityFailureReasonCode.None:
                    _lastBlockReason = string.Empty;
                    break;
                case IntegrityFailureReasonCode.Unsupported:
                    _lastBlockReason = UnsupportedReason;
                    break;
                default:
                    _lastBlockReason = IntegrityFailureReason;
                    break;
            }

            return true;
        }

        private static int PrepareCostBuffers(
            BuildableData data,
            Span<int> costHashes,
            Span<int> costRemaining)
        {
            int costCount = data != null && data.buildCost != null ? data.buildCost.Count : 0;
            if (costCount <= 0)
                return 0;

            int preparedCount = 0;
            for (int i = 0; i < costCount; i++)
            {
                InventoryCost cost = data.buildCost[i];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                int itemHashId = LocHash.Compute(cost.item.PersistentId);
                if (itemHashId == 0)
                    continue;

                int groupIndex = FindCostGroupIndex(costHashes, itemHashId, preparedCount);
                if (groupIndex < 0)
                {
                    if (preparedCount >= costHashes.Length || preparedCount >= costRemaining.Length)
                        return -1;

                    groupIndex = preparedCount;
                    costHashes[groupIndex] = itemHashId;
                    costRemaining[groupIndex] = 0;
                    preparedCount++;
                }

                if (!TryAccumulateCostAmount(costRemaining, groupIndex, cost.amount))
                    return -1;
            }

            return preparedCount;
        }

        private static int FindCostGroupIndex(Span<int> costHashes, int itemHashId, int preparedCount)
        {
            for (int i = 0; i < preparedCount; i++)
            {
                if (costHashes[i] == itemHashId)
                    return i;
            }

            return -1;
        }

        private static bool TryAccumulateCostAmount(Span<int> costRemaining, int groupIndex, int amount)
        {
            if (groupIndex < 0 || groupIndex >= costRemaining.Length || amount <= 0)
                return false;

            int current = costRemaining[groupIndex];
            if (current > int.MaxValue - amount)
                return false;

            costRemaining[groupIndex] = current + amount;
            return true;
        }

        private static void RollbackRemovedResources(
            PlayerInventory inventory,
            int costCount,
            Span<int> costHashes,
            Span<int> costRemoved)
        {
            if (inventory == null)
                return;

            for (int i = 0; i < costCount; i++)
            {
                int removed = costRemoved[i];
                int itemHashId = costHashes[i];
                if (removed <= 0 || itemHashId == 0)
                    continue;

                inventory.TryAddItem(itemHashId, removed);
                costRemoved[i] = 0;
            }
        }

        private int BuildValidationGraph(
            ConstructionManager constructionManager,
            Vector3 candidatePosition,
            Quaternion candidateRotation,
            BuildableData candidateData,
            float gridSize,
            out IntegrityGraphBuffers graphBuffers)
        {
            graphBuffers = default;
            int moduleListCount = constructionManager != null ? constructionManager.ModuleCount : 0;
            int validationGridSize = math.max(1, (int)math.floor(math.max(DefaultGridSize, gridSize) * math.rcp(DefaultSocketQuantization) + 0.5f));
            int graphSignature = ComputeExistingGraphSignature(constructionManager, _catalogVault);

            if (!HasValidationGraphCapacity(moduleListCount + 1) || !TryResolveValidationGraphBuffers(out graphBuffers))
                return 0;

            int existingNodeCount = EnsureExistingGraphCache(constructionManager, validationGridSize, _catalogVault, graphSignature, ref graphBuffers);
            if (existingNodeCount < 0)
                return 0;

            int activeNodeCount = existingNodeCount + 1;

            WriteNodeRecord(graphBuffers.Nodes, existingNodeCount, candidatePosition, candidateData, true);
            if (!IndexCandidateSockets(existingNodeCount, candidatePosition, candidateRotation, candidateData, validationGridSize, _catalogVault, ref graphBuffers))
                return 0;

            if (!BuildAdjacency(activeNodeCount, ref graphBuffers))
                return 0;

            return activeNodeCount;
        }

        private int EnsureExistingGraphCache(
            ConstructionManager constructionManager,
            int validationGridSize,
            IDataVault catalogVault,
            int graphSignature,
            ref IntegrityGraphBuffers graphBuffers)
        {
            int moduleListCount = constructionManager != null ? constructionManager.ModuleCount : 0;
            if (_hasExistingGraphCache &&
                _cachedExistingGraphSignature == graphSignature &&
                _cachedExistingGraphGridSize == validationGridSize &&
                _cachedExistingGraphModuleListCount == moduleListCount)
            {
                if (TrimConnectionBufferToExistingCache())
                    return _cachedExistingGraphNodeCount;
            }

            ResetGraphScratch(ref graphBuffers);

            int nodeIndex = 0;
            if (constructionManager != null)
            {
                for (int i = 0; i < moduleListCount; i++)
                {
                    GameObject moduleObject = constructionManager.GetSpawnedModuleAt(i);
                    if (moduleObject == null)
                        continue;

                    Transform moduleTransform = moduleObject.transform;
                    BuildableData moduleData = ResolveBuildableData(moduleObject);
                    WriteNodeRecord(graphBuffers.Nodes, nodeIndex, moduleTransform.position, moduleData, false);
                    if (!IndexSockets(nodeIndex, moduleTransform.position, moduleTransform.rotation, moduleData, validationGridSize, catalogVault, ref graphBuffers))
                        return -1;

                    nodeIndex++;
                }
            }

            _cachedExistingGraphSignature = graphSignature;
            _cachedExistingGraphGridSize = validationGridSize;
            _cachedExistingGraphModuleListCount = moduleListCount;
            _cachedExistingGraphNodeCount = nodeIndex;
            _cachedExistingGraphConnectionCount = _connectionCount;
            _hasExistingGraphCache = true;
            return nodeIndex;
        }

        private bool TrimConnectionBufferToExistingCache()
        {
            int staleConnectionCount = _connectionCount - _cachedExistingGraphConnectionCount;
            if (staleConnectionCount >= 0)
            {
                _connectionCount = _cachedExistingGraphConnectionCount;
                return true;
            }

            InvalidateExistingGraphCache();
            return false;
        }

        private void InvalidateExistingGraphCache()
        {
            _hasExistingGraphCache = false;
            _cachedExistingGraphSignature = 0;
            _cachedExistingGraphGridSize = 0;
            _cachedExistingGraphModuleListCount = 0;
            _cachedExistingGraphNodeCount = 0;
            _cachedExistingGraphConnectionCount = 0;
            _connectionCount = 0;
            _socketLookupCount = 0;
            if (TryResolveValidationGraphBuffers(out IntegrityGraphBuffers graphBuffers))
                ClearSocketLookup(graphBuffers.SocketLookup);
        }

        private void ResetGraphScratch(ref IntegrityGraphBuffers graphBuffers)
        {
            _connectionCount = 0;
            _socketLookupCount = 0;
            ClearSocketLookup(graphBuffers.SocketLookup);
        }

        private static void ClearSocketLookup(NativeArray<SocketLookupSlot> lookup)
        {
            if (!lookup.IsCreated)
                return;

            for (int i = 0; i < lookup.Length; i++)
                lookup[i] = default;
        }

        private static int ComputeExistingGraphSignature(ConstructionManager constructionManager, IDataVault socketVault)
        {
            int sceneModuleCount = constructionManager != null ? constructionManager.ModuleCount : 0;
            if (TryComputeSocketVaultGraphSignature(socketVault, sceneModuleCount, out int socketVaultSignature))
                return socketVaultSignature;

            if (constructionManager == null)
                return 0;

            unchecked
            {
                int count = sceneModuleCount;
                int hash = 17;
                hash = (hash * 397) ^ count;
                for (int i = 0; i < count; i++)
                {
                    GameObject module = constructionManager.GetSpawnedModuleAt(i);
                    hash = (hash * 397) ^ ComputeSceneModuleSignature(module);
                }

                return hash;
            }
        }

        private static int ComputeSceneModuleSignature(GameObject module)
        {
            if (module == null)
                return 0;

            unchecked
            {
                int hash = 23;
                BuildableData data = ResolveBuildableData(module);
                hash = FoldSignature(hash, data != null ? data.ModuleHashId : 0);
                hash = FoldSignature(hash, data != null ? (int)data.family : 0);

                Transform moduleTransform = module.transform;
                if (moduleTransform == null || !IsFinitePose(moduleTransform.position, moduleTransform.rotation))
                    return FoldSignature(hash, int.MinValue);

                if (TryResolveAupFromRuntimeOrigin(moduleTransform.position, out double3 rootAup))
                {
                    hash = FoldSignature(hash, QuantizeSignatureAup(rootAup.x));
                    hash = FoldSignature(hash, QuantizeSignatureAup(rootAup.y));
                    hash = FoldSignature(hash, QuantizeSignatureAup(rootAup.z));
                }
                else
                {
                    Vector3 runtimePosition = moduleTransform.position;
                    hash = FoldSignature(hash, QuantizeSignatureAup(runtimePosition.x));
                    hash = FoldSignature(hash, QuantizeSignatureAup(runtimePosition.y));
                    hash = FoldSignature(hash, QuantizeSignatureAup(runtimePosition.z));
                }

                Quaternion rotation = moduleTransform.rotation;
                hash = FoldSignature(hash, math.asuint(rotation.x));
                hash = FoldSignature(hash, math.asuint(rotation.y));
                hash = FoldSignature(hash, math.asuint(rotation.z));
                hash = FoldSignature(hash, math.asuint(rotation.w));
                return hash != 0 ? hash : 1;
            }
        }

        private static bool TryComputeSocketVaultGraphSignature(IDataVault socketVault, int expectedModuleCount, out int signature)
        {
            signature = 0;
            if (socketVault == null ||
                expectedModuleCount <= 0 ||
                !ShinobuSocketConstructionRuntime.TryResolveVaultViews(socketVault, out ConstructionSocketVaultViews views) ||
                !views.Counters.IsCreated ||
                views.Counters.Length <= 4 ||
                !views.Modules.IsCreated)
            {
                return false;
            }

            int moduleCount = math.clamp(views.Counters[0], 0, views.Modules.Length);
            if (moduleCount != expectedModuleCount)
                return false;

            int socketCount = 0;
            if (views.SocketStates.IsCreated)
                socketCount = math.clamp(views.Counters[1], 0, views.SocketStates.Length);

            int connectionCount = 0;
            if (views.Connections.IsCreated)
                connectionCount = math.clamp(views.Counters[4], 0, views.Connections.Length);

            unchecked
            {
                int hash = 0x53484753; // "SHGS" - SHINOBU graph signature.
                hash = FoldSignature(hash, moduleCount);
                hash = FoldSignature(hash, socketCount);
                hash = FoldSignature(hash, connectionCount);
                hash = FoldSignature(hash, views.Counters[2]);
                hash = FoldSignature(hash, views.Counters[3]);

                for (int i = 0; i < moduleCount; i++)
                {
                    ConstructionSocketModuleDTO module = views.Modules[i];
                    hash = FoldSignature(hash, module.ModuleHash);
                    hash = FoldSignature(hash, module.SocketStart);
                    hash = FoldSignature(hash, module.SocketCount);
                    hash = FoldSignature(hash, module.Flags);
                    hash = FoldSignature(hash, module.TopologyVersion);
                    hash = FoldSignature(hash, module.ConnectedMask);
                    hash = FoldSignature(hash, QuantizeSignatureAup(module.RootAup.x));
                    hash = FoldSignature(hash, QuantizeSignatureAup(module.RootAup.y));
                    hash = FoldSignature(hash, QuantizeSignatureAup(module.RootAup.z));
                    hash = FoldSignature(hash, math.asuint(module.Rotation.value.x));
                    hash = FoldSignature(hash, math.asuint(module.Rotation.value.y));
                    hash = FoldSignature(hash, math.asuint(module.Rotation.value.z));
                    hash = FoldSignature(hash, math.asuint(module.Rotation.value.w));
                }

                for (int i = 0; i < connectionCount; i++)
                {
                    SocketConnectionPairDTO pair = views.Connections[i];
                    hash = FoldSignature(hash, pair.TargetSocketIndex);
                    hash = FoldSignature(hash, pair.GhostSocketIndex);
                    hash = FoldSignature(hash, pair.TargetModuleHash);
                    hash = FoldSignature(hash, pair.GhostModuleHash);
                    hash = FoldSignature(hash, pair.ConnectionKind);
                    hash = FoldSignature(hash, pair.Flags);
                    hash = FoldSignature(hash, pair.ResultHash);
                }

                signature = hash != 0 ? hash : 1;
                return true;
            }
        }

        private static int FoldSignature(int hash, int value)
        {
            unchecked
            {
                return (hash * 397) ^ value;
            }
        }

        private static int FoldSignature(int hash, uint value)
        {
            return FoldSignature(hash, unchecked((int)value));
        }

        private static int QuantizeSignatureAup(double value)
        {
            if (!math.isfinite(value))
                return int.MinValue;

            double millimeters = value * 1000d;
            double rounded = millimeters >= 0d ? math.floor(millimeters + 0.5d) : math.ceil(millimeters - 0.5d);
            if (rounded > int.MaxValue)
                return int.MaxValue;
            if (rounded < int.MinValue)
                return int.MinValue;

            return (int)rounded;
        }

        private bool IndexSockets(int moduleIndex, Vector3 rootPosition, Quaternion rootRotation, BuildableData data, int validationGridSize, IDataVault catalogVault, ref IntegrityGraphBuffers graphBuffers)
        {
            if (data == null || data.ModuleTemplate == null || !IsFinitePose(rootPosition, rootRotation))
                return true;

            BaseModuleTemplate template = data.ModuleTemplate;
            uint prefabHash = unchecked((uint)template.TemplateHashId);
            if (BaseModuleCatalogRuntime.TryGetModuleSocketRangeFromVault(
                    catalogVault,
                    prefabHash,
                    out NativeArray<SocketDefinitionDTO> catalogSockets,
                    out int socketStart,
                    out int socketCount,
                    out _))
            {
                return IndexSocketRange(moduleIndex, rootPosition, rootRotation, catalogSockets, socketStart, socketCount, validationGridSize, ref graphBuffers);
            }

            if (Application.isPlaying)
                return true;

            BaseModuleTemplate.SocketDefinition[] definitions = template.SocketDefinitions;
            if (definitions == null || definitions.Length == 0)
                return true;

            for (int i = 0; i < definitions.Length; i++)
            {
                if (!BaseModuleCatalogRuntime.TryBuildSocketFromTemplate(template, i, out SocketDefinitionDTO socket))
                    continue;

                if (!IndexSocket(moduleIndex, rootPosition, rootRotation, socket, validationGridSize, ref graphBuffers))
                    return false;
            }

            return true;
        }

        private bool IndexSocketRange(int moduleIndex, Vector3 rootPosition, Quaternion rootRotation, NativeArray<SocketDefinitionDTO> sockets, int socketStart, int socketCount, int validationGridSize, ref IntegrityGraphBuffers graphBuffers)
        {
            int end = math.min(socketStart + socketCount, sockets.Length);
            for (int i = socketStart; i < end; i++)
            {
                if (!IndexSocket(moduleIndex, rootPosition, rootRotation, sockets[i], validationGridSize, ref graphBuffers))
                    return false;
            }

            return true;
        }

        private bool IndexCandidateSockets(int moduleIndex, Vector3 rootPosition, Quaternion rootRotation, BuildableData data, int validationGridSize, IDataVault catalogVault, ref IntegrityGraphBuffers graphBuffers)
        {
            if (data == null || data.ModuleTemplate == null || !IsFinitePose(rootPosition, rootRotation))
                return true;

            BaseModuleTemplate template = data.ModuleTemplate;
            uint prefabHash = unchecked((uint)template.TemplateHashId);
            if (BaseModuleCatalogRuntime.TryGetModuleSocketRangeFromVault(
                    catalogVault,
                    prefabHash,
                    out NativeArray<SocketDefinitionDTO> catalogSockets,
                    out int socketStart,
                    out int socketCount,
                    out _))
            {
                return IndexCandidateSocketRange(moduleIndex, rootPosition, rootRotation, catalogSockets, socketStart, socketCount, validationGridSize, ref graphBuffers);
            }

            if (Application.isPlaying)
                return true;

            BaseModuleTemplate.SocketDefinition[] definitions = template.SocketDefinitions;
            if (definitions == null || definitions.Length == 0)
                return true;

            for (int i = 0; i < definitions.Length; i++)
            {
                if (!BaseModuleCatalogRuntime.TryBuildSocketFromTemplate(template, i, out SocketDefinitionDTO socket))
                    continue;

                if (!IndexCandidateSocket(moduleIndex, rootPosition, rootRotation, socket, validationGridSize, ref graphBuffers))
                    return false;
            }

            return true;
        }

        private bool IndexCandidateSocketRange(int moduleIndex, Vector3 rootPosition, Quaternion rootRotation, NativeArray<SocketDefinitionDTO> sockets, int socketStart, int socketCount, int validationGridSize, ref IntegrityGraphBuffers graphBuffers)
        {
            int end = math.min(socketStart + socketCount, sockets.Length);
            for (int i = socketStart; i < end; i++)
            {
                if (!IndexCandidateSocket(moduleIndex, rootPosition, rootRotation, sockets[i], validationGridSize, ref graphBuffers))
                    return false;
            }

            return true;
        }

        private bool IndexCandidateSocket(int moduleIndex, Vector3 rootPosition, Quaternion rootRotation, in SocketDefinitionDTO socket, int validationGridSize, ref IntegrityGraphBuffers graphBuffers)
        {
            if (!TryResolveSocketPose(rootPosition, rootRotation, in socket, out double3 socketAup, out Vector3 socketForward))
                return true;

            int axis = QuantizeAxis(socketForward);
            SocketKey oppositeKey = SocketKey.Create(socketAup, OppositeAxis(axis), validationGridSize);
            if (!TryFindSocket(graphBuffers.SocketLookup, in oppositeKey, out SocketLookupSlot existing))
                return true;

            if (existing.ModuleIndex == moduleIndex ||
                !BaseModuleCatalogRuntime.AreSocketMasksCompatible(existing.CompatibilityMask, socket.AllowedConnectionsMask) ||
                math.dot(existing.Forward, new float3(socketForward.x, socketForward.y, socketForward.z)) > OppositeDirectionDotThreshold)
            {
                return true;
            }

            return AddConnection(ref graphBuffers, existing.ModuleIndex, moduleIndex);
        }

        private bool IndexSocket(int moduleIndex, Vector3 rootPosition, Quaternion rootRotation, in SocketDefinitionDTO socket, int validationGridSize, ref IntegrityGraphBuffers graphBuffers)
        {
            if (!TryResolveSocketPose(rootPosition, rootRotation, in socket, out double3 socketAup, out Vector3 socketForward))
                return true;

            int axis = QuantizeAxis(socketForward);
            SocketKey oppositeKey = SocketKey.Create(socketAup, OppositeAxis(axis), validationGridSize);

            if (TryFindSocket(graphBuffers.SocketLookup, in oppositeKey, out SocketLookupSlot existing))
            {
                if (existing.ModuleIndex != moduleIndex &&
                    BaseModuleCatalogRuntime.AreSocketMasksCompatible(existing.CompatibilityMask, socket.AllowedConnectionsMask) &&
                    math.dot(existing.Forward, new float3(socketForward.x, socketForward.y, socketForward.z)) <= OppositeDirectionDotThreshold)
                {
                    return AddConnection(ref graphBuffers, existing.ModuleIndex, moduleIndex);
                }
            }

            SocketKey ownKey = SocketKey.Create(socketAup, axis, validationGridSize);
            return TryInsertSocket(graphBuffers.SocketLookup, in ownKey, moduleIndex, socket.AllowedConnectionsMask, socketForward);
        }

        private bool AddConnection(ref IntegrityGraphBuffers graphBuffers, int a, int b)
        {
            if (!graphBuffers.Connections.IsCreated ||
                a < 0 ||
                b < 0 ||
                a == b ||
                _connectionCount < 0 ||
                _connectionCount >= graphBuffers.Connections.Length ||
                _connectionCount >= _connectionCapacity)
            {
                InvalidateExistingGraphCache();
                return false;
            }

            graphBuffers.Connections[_connectionCount++] = new int2(a, b);
            return true;
        }

        private bool TryInsertSocket(NativeArray<SocketLookupSlot> lookup, in SocketKey key, int moduleIndex, uint compatibilityMask, Vector3 forward)
        {
            if (!lookup.IsCreated || lookup.Length == 0 || !IsPowerOfTwo(lookup.Length))
                return false;

            uint hash = key.Hash();
            int mask = lookup.Length - 1;
            int start = unchecked((int)(hash & (uint)mask));
            for (int probe = 0; probe < lookup.Length; probe++)
            {
                int index = (start + probe) & mask;
                SocketLookupSlot slot = lookup[index];
                if (slot.Occupied == 0)
                {
                    lookup[index] = SocketLookupSlot.Create(in key, hash, moduleIndex, compatibilityMask, forward);
                    _socketLookupCount++;
                    return true;
                }

                if (slot.Hash == hash && slot.Matches(in key))
                {
                    lookup[index] = SocketLookupSlot.Create(in key, hash, moduleIndex, compatibilityMask, forward);
                    return true;
                }
            }

            InvalidateExistingGraphCache();
            return false;
        }

        private static bool TryFindSocket(NativeArray<SocketLookupSlot> lookup, in SocketKey key, out SocketLookupSlot found)
        {
            found = default;
            if (!lookup.IsCreated || lookup.Length == 0 || !IsPowerOfTwo(lookup.Length))
                return false;

            uint hash = key.Hash();
            int mask = lookup.Length - 1;
            int start = unchecked((int)(hash & (uint)mask));
            for (int probe = 0; probe < lookup.Length; probe++)
            {
                int index = (start + probe) & mask;
                SocketLookupSlot slot = lookup[index];
                if (slot.Occupied == 0)
                    return false;

                if (slot.Hash == hash && slot.Matches(in key))
                {
                    found = slot;
                    return true;
                }
            }

            return false;
        }

        private static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

        private static bool TryResolveSocketPose(Vector3 rootPosition, Quaternion rootRotation, in SocketDefinitionDTO socket, out double3 socketAup, out Vector3 socketForward)
        {
            socketAup = default;
            socketForward = Vector3.forward;
            if (!IsFinitePose(rootPosition, rootRotation))
                return false;

            quaternion rotation = new quaternion(rootRotation.x, rootRotation.y, rootRotation.z, rootRotation.w);
            float3 worldNormal = math.rotate(rotation, socket.Normal);
            if (!math.all(math.isfinite(socket.LocalOffset)) || !math.all(math.isfinite(worldNormal)))
                return false;

            if (!TryResolveAupFromRuntimeOrigin(rootPosition, out double3 rootAup))
                return false;

            socketAup = BaseModuleCatalogRuntime.ResolveSocketAup(rootAup, rotation, in socket);
            if (!math.all(math.isfinite(socketAup)))
                return false;

            socketForward = new Vector3(worldNormal.x, worldNormal.y, worldNormal.z);
            return true;
        }

        private static bool IsFinitePose(Vector3 position, Quaternion rotation)
        {
            return math.isfinite(position.x) &&
                   math.isfinite(position.y) &&
                   math.isfinite(position.z) &&
                   math.isfinite(rotation.x) &&
                   math.isfinite(rotation.y) &&
                   math.isfinite(rotation.z) &&
                   math.isfinite(rotation.w);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out double3 aup)
        {
            aup = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
            {
                return false;
            }

            double3 originAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!math.all(math.isfinite(originAup)))
                return false;

            aup = originAup + new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            return math.all(math.isfinite(aup));
        }

        private bool BuildAdjacency(int nodeCount, ref IntegrityGraphBuffers graphBuffers)
        {
            NativeArray<int> adjacencyCounts = graphBuffers.AdjacencyCounts;
            NativeArray<int> adjacencyWrites = graphBuffers.AdjacencyWrites;
            if (!adjacencyCounts.IsCreated ||
                !adjacencyWrites.IsCreated ||
                !graphBuffers.AdjacencyRanges.IsCreated ||
                !graphBuffers.Connections.IsCreated ||
                adjacencyCounts.Length < nodeCount ||
                adjacencyWrites.Length < nodeCount ||
                graphBuffers.AdjacencyRanges.Length < nodeCount ||
                _connectionCount < 0 ||
                _connectionCount > graphBuffers.Connections.Length ||
                _connectionCount > _connectionCapacity)
            {
                return false;
            }

            for (int i = 0; i < nodeCount; i++)
                adjacencyCounts[i] = 0;

            int connectionCount = _connectionCount;
            for (int i = 0; i < connectionCount; i++)
            {
                int2 connection = graphBuffers.Connections[i];
                if (!IsValidConnectionIndex(in connection, nodeCount))
                {
                    InvalidateExistingGraphCache();
                    return false;
                }

                adjacencyCounts[connection.x]++;
                adjacencyCounts[connection.y]++;
            }

            int adjacencyCount = 0;
            for (int i = 0; i < nodeCount; i++)
            {
                int count = adjacencyCounts[i];
                if (adjacencyCount > int.MaxValue - count)
                {
                    InvalidateExistingGraphCache();
                    return false;
                }

                graphBuffers.AdjacencyRanges[i] = new int2(adjacencyCount, count);
                adjacencyWrites[i] = adjacencyCount;
                adjacencyCount += count;
            }

            if (adjacencyCount > _adjacencyCapacity ||
                !graphBuffers.Adjacency.IsCreated ||
                graphBuffers.Adjacency.Length < adjacencyCount)
            {
                InvalidateExistingGraphCache();
                return false;
            }

            for (int i = 0; i < connectionCount; i++)
            {
                int2 connection = graphBuffers.Connections[i];
                if (!IsValidConnectionIndex(in connection, nodeCount))
                {
                    InvalidateExistingGraphCache();
                    return false;
                }

                int abWrite = adjacencyWrites[connection.x]++;
                int baWrite = adjacencyWrites[connection.y]++;
                graphBuffers.Adjacency[abWrite] = connection.y;
                graphBuffers.Adjacency[baWrite] = connection.x;
            }

            return true;
        }

        private static bool IsValidConnectionIndex(in int2 connection, int nodeCount)
        {
            return nodeCount > 0 &&
                   connection.x != connection.y &&
                   (uint)connection.x < (uint)nodeCount &&
                   (uint)connection.y < (uint)nodeCount;
        }

        private static void WriteNodeRecord(NativeArray<IntegrityNodeRecord> nodes, int index, Vector3 position, BuildableData data, bool isCandidate)
        {
            nodes[index] = new IntegrityNodeRecord
            {
                Mass = math.max(1f, data != null ? data.TotalResourceCount : 1f),
                IsSupportRoot = (byte)((data == null || data.family == BuildableFamily.Structure) ? 1 : 0),
                IsCandidate = (byte)(isCandidate ? 1 : 0)
            };
        }

        private static BuildableData ResolveBuildableData(GameObject moduleObject)
        {
            if (moduleObject == null)
                return null;

            return moduleObject.TryGetComponent(out ModuleMarker marker) ? marker.Data : null;
        }

        private static int QuantizeAxis(Vector3 direction)
        {
            float3 raw = (float3)direction;
            if (!math.all(math.isfinite(raw)))
                raw = new float3(0f, 0f, 1f);

            float absX = math.abs(raw.x);
            float absY = math.abs(raw.y);
            float absZ = math.abs(raw.z);
            if ((absX + absY + absZ) <= 0.0001f)
            {
                raw = new float3(0f, 0f, 1f);
                absX = 0f;
                absY = 0f;
                absZ = 1f;
            }

            if (absX >= absY && absX >= absZ)
                return raw.x >= 0f ? 0 : 1;

            if (absY >= absX && absY >= absZ)
                return raw.y >= 0f ? 2 : 3;

            return raw.z >= 0f ? 4 : 5;
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

        private bool EnsureNodeCapacity(int required)
        {
            if (_catalogVault == null)
                return false;

            if (_nodeCapacity >= required && TryResolveValidationGraphBuffers(out _))
                return true;

            if (_validationPending)
                return false;

            InvalidateExistingGraphCache();

            int newNodeCapacity = NextPowerOfTwo(math.max(required, InitialNodeCapacity));
            int newAdjacencyCapacity = NextPowerOfTwo(math.max(newNodeCapacity * 4, InitialAdjacencyCapacity));
            int newConnectionCapacity = NextPowerOfTwo(math.max(newNodeCapacity * 8, InitialConnectionCapacity));
            int newSocketLookupCapacity = NextPowerOfTwo(math.max(newNodeCapacity * 16, InitialSocketLookupCapacity));
            _nodeBufferHandle = EnsureVaultHandle(IntegrityNodeBufferId, newNodeCapacity, ref _nodeBufferHandle);
            _adjacencyRangesHandle = EnsureVaultHandle(IntegrityRangeBufferId, newNodeCapacity, ref _adjacencyRangesHandle);
            _queueBufferHandle = EnsureVaultHandle(IntegrityQueueBufferId, newNodeCapacity, ref _queueBufferHandle);
            _depthBufferHandle = EnsureVaultHandle(IntegrityDepthBufferId, newNodeCapacity, ref _depthBufferHandle);
            _resultBufferHandle = EnsureVaultHandle(IntegrityResultBufferId, 1, ref _resultBufferHandle);
            _adjacencyHandle = EnsureVaultHandle(IntegrityAdjacencyBufferId, newAdjacencyCapacity, ref _adjacencyHandle);
            _adjacencyDegreeScratchHandle = EnsureVaultHandle(IntegrityDegreeScratchBufferId, newNodeCapacity, ref _adjacencyDegreeScratchHandle);
            _adjacencyWriteScratchHandle = EnsureVaultHandle(IntegrityWriteScratchBufferId, newNodeCapacity, ref _adjacencyWriteScratchHandle);
            _connectionBufferHandle = EnsureVaultHandle(IntegrityConnectionBufferId, newConnectionCapacity, ref _connectionBufferHandle);
            _socketLookupHandle = EnsureVaultHandle(IntegritySocketLookupBufferId, newSocketLookupCapacity, ref _socketLookupHandle);
            _nodeCapacity = newNodeCapacity;
            _adjacencyCapacity = newAdjacencyCapacity;
            _connectionCapacity = newConnectionCapacity;
            _socketLookupCapacity = newSocketLookupCapacity;
            return TryResolveValidationGraphBuffers(out _);
        }

        private bool HasValidationGraphCapacity(int requiredNodes)
        {
            if (_catalogVault == null)
                return false;

            if (requiredNodes <= 0 ||
                _nodeCapacity < requiredNodes ||
                _adjacencyCapacity <= 0 ||
                _connectionCapacity <= 0 ||
                _socketLookupCapacity <= 0)
            {
                return false;
            }

            return true;
        }

        private bool TryResolveValidationGraphBuffers(out IntegrityGraphBuffers graphBuffers)
        {
            graphBuffers = default;
            if (_catalogVault == null)
                return false;

            return _catalogVault.TryResolveHandle(in _nodeBufferHandle, out graphBuffers.Nodes) &&
                   _catalogVault.TryResolveHandle(in _adjacencyRangesHandle, out graphBuffers.AdjacencyRanges) &&
                   _catalogVault.TryResolveHandle(in _adjacencyHandle, out graphBuffers.Adjacency) &&
                   _catalogVault.TryResolveHandle(in _queueBufferHandle, out graphBuffers.Queue) &&
                   _catalogVault.TryResolveHandle(in _depthBufferHandle, out graphBuffers.Depths) &&
                   _catalogVault.TryResolveHandle(in _resultBufferHandle, out graphBuffers.Result) &&
                   _catalogVault.TryResolveHandle(in _adjacencyDegreeScratchHandle, out graphBuffers.AdjacencyCounts) &&
                   _catalogVault.TryResolveHandle(in _adjacencyWriteScratchHandle, out graphBuffers.AdjacencyWrites) &&
                   _catalogVault.TryResolveHandle(in _connectionBufferHandle, out graphBuffers.Connections) &&
                   _catalogVault.TryResolveHandle(in _socketLookupHandle, out graphBuffers.SocketLookup) &&
                   graphBuffers.Nodes.IsCreated &&
                   graphBuffers.AdjacencyRanges.IsCreated &&
                   graphBuffers.Adjacency.IsCreated &&
                   graphBuffers.Queue.IsCreated &&
                   graphBuffers.Depths.IsCreated &&
                   graphBuffers.Result.IsCreated &&
                   graphBuffers.AdjacencyCounts.IsCreated &&
                   graphBuffers.AdjacencyWrites.IsCreated &&
                   graphBuffers.Connections.IsCreated &&
                   graphBuffers.SocketLookup.IsCreated;
        }

        private bool TryResolveResultBuffer(out NativeArray<IntegrityValidationResult> resultBuffer)
        {
            resultBuffer = default;
            return _catalogVault != null &&
                   _catalogVault.TryResolveHandle(in _resultBufferHandle, out resultBuffer) &&
                   resultBuffer.IsCreated &&
                   resultBuffer.Length > 0;
        }

        private VaultGenerationHandle<T> EnsureVaultHandle<T>(BufferID bufferId, int requiredLength, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (_catalogVault == null)
                return default;

            if (handle.BufferID != 0u &&
                _catalogVault.TryResolveHandle(in handle, out NativeArray<T> existing) &&
                existing.IsCreated &&
                existing.Length >= requiredLength)
            {
                return handle;
            }

            if (handle.BufferID != 0u)
                _catalogVault.ReleaseBuffer(in handle);

            return _catalogVault.GetGenerationHandle<T>(
                bufferId,
                math.max(1, requiredLength),
                SystemID.Construction,
                NativeArrayOptions.UninitializedMemory);
        }

        private void ReleaseValidationVaultHandles()
        {
            UnlockValidationBuffers();
            if (_catalogVault == null)
            {
                _nodeBufferHandle = default;
                _adjacencyRangesHandle = default;
                _adjacencyHandle = default;
                _queueBufferHandle = default;
                _depthBufferHandle = default;
                _resultBufferHandle = default;
                _adjacencyDegreeScratchHandle = default;
                _adjacencyWriteScratchHandle = default;
                _connectionBufferHandle = default;
                _socketLookupHandle = default;
                _nodeCapacity = 0;
                _adjacencyCapacity = 0;
                _connectionCapacity = 0;
                _socketLookupCapacity = 0;
                _connectionCount = 0;
                _socketLookupCount = 0;
                return;
            }

            ReleaseVaultHandle(ref _nodeBufferHandle);
            ReleaseVaultHandle(ref _adjacencyRangesHandle);
            ReleaseVaultHandle(ref _adjacencyHandle);
            ReleaseVaultHandle(ref _queueBufferHandle);
            ReleaseVaultHandle(ref _depthBufferHandle);
            ReleaseVaultHandle(ref _resultBufferHandle);
            ReleaseVaultHandle(ref _adjacencyDegreeScratchHandle);
            ReleaseVaultHandle(ref _adjacencyWriteScratchHandle);
            ReleaseVaultHandle(ref _connectionBufferHandle);
            ReleaseVaultHandle(ref _socketLookupHandle);
            _nodeCapacity = 0;
            _adjacencyCapacity = 0;
            _connectionCapacity = 0;
            _socketLookupCapacity = 0;
            _connectionCount = 0;
            _socketLookupCount = 0;
        }

        private void ReleaseVaultHandle<T>(ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (handle.BufferID != 0u)
                _catalogVault.ReleaseBuffer(in handle);

            handle = default;
        }

        private bool TryLockValidationBuffers()
        {
            UnlockValidationBuffers();
            return TryLockValidationBuffer(IntegrityNodeBufferId, IntegrityNodeLockBit) &&
                   TryLockValidationBuffer(IntegrityRangeBufferId, IntegrityRangeLockBit) &&
                   TryLockValidationBuffer(IntegrityAdjacencyBufferId, IntegrityAdjacencyLockBit) &&
                   TryLockValidationBuffer(IntegrityQueueBufferId, IntegrityQueueLockBit) &&
                   TryLockValidationBuffer(IntegrityDepthBufferId, IntegrityDepthLockBit) &&
                   TryLockValidationBuffer(IntegrityResultBufferId, IntegrityResultLockBit);
        }

        private bool TryLockValidationBuffer(BufferID bufferId, uint bit)
        {
            if (_catalogVault == null || !_catalogVault.TryLockBuffer(bufferId, SystemID.Construction))
            {
                UnlockValidationBuffers();
                return false;
            }

            _lockedValidationBufferMask |= bit;
            return true;
        }

        private void UnlockValidationBuffers()
        {
            UnlockValidationBuffer(IntegrityResultBufferId, IntegrityResultLockBit);
            UnlockValidationBuffer(IntegrityDepthBufferId, IntegrityDepthLockBit);
            UnlockValidationBuffer(IntegrityQueueBufferId, IntegrityQueueLockBit);
            UnlockValidationBuffer(IntegrityAdjacencyBufferId, IntegrityAdjacencyLockBit);
            UnlockValidationBuffer(IntegrityRangeBufferId, IntegrityRangeLockBit);
            UnlockValidationBuffer(IntegrityNodeBufferId, IntegrityNodeLockBit);
        }

        private void UnlockValidationBuffer(BufferID bufferId, uint bit)
        {
            if ((_lockedValidationBufferMask & bit) == 0u)
                return;

            _catalogVault?.TryUnlockBuffer(bufferId, SystemID.Construction);
            _lockedValidationBufferMask &= ~bit;
        }

        private void CompletePendingValidationForTeardown()
        {
            if (!_validationPending)
                return;

            DispatcherJobSwap.TryComplete(ref _validationHandle, true);
            _validationPending = false;
            _discardValidationResult = false;
            UnlockValidationBuffers();
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

        private readonly struct SocketKey : IEquatable<SocketKey>
        {
            public readonly int X;
            public readonly int Y;
            public readonly int Z;
            public readonly int Axis;

            private SocketKey(int x, int y, int z, int axis)
            {
                X = x;
                Y = y;
                Z = z;
                Axis = axis;
            }

            public static SocketKey Create(double3 socketAup, int axis, int validationGridSize)
            {
                double scale = validationGridSize > 0 ? validationGridSize : 1d;
                double3 scaledPosition = socketAup * scale;
                int3 quantizedPosition = new int3(
                    QuantizeScaledAup(scaledPosition.x),
                    QuantizeScaledAup(scaledPosition.y),
                    QuantizeScaledAup(scaledPosition.z));
                return new SocketKey(
                    quantizedPosition.x,
                    quantizedPosition.y,
                    quantizedPosition.z,
                    axis);
            }

            private static int QuantizeScaledAup(double value)
            {
                if (!math.isfinite(value))
                    return 0;

                double rounded = value >= 0d ? math.floor(value + 0.5d) : math.ceil(value - 0.5d);
                if (rounded > int.MaxValue)
                    return int.MaxValue;
                if (rounded < int.MinValue)
                    return int.MinValue;

                return (int)rounded;
            }

            public bool Equals(SocketKey other)
            {
                return X == other.X &&
                       Y == other.Y &&
                       Z == other.Z &&
                       Axis == other.Axis;
            }

            public override bool Equals(object obj)
            {
                return obj is SocketKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = X;
                    hash = (hash * 397) ^ Y;
                    hash = (hash * 397) ^ Z;
                    hash = (hash * 397) ^ Axis;
                    return hash;
                }
            }

            public uint Hash()
            {
                return unchecked((uint)GetHashCode());
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 48)]
        private struct SocketLookupSlot
        {
            [FieldOffset(0)] public int X;
            [FieldOffset(4)] public int Y;
            [FieldOffset(8)] public int Z;
            [FieldOffset(12)] public int Axis;
            [FieldOffset(16)] public int ModuleIndex;
            [FieldOffset(20)] public uint CompatibilityMask;
            [FieldOffset(24)] public float3 Forward;
            [FieldOffset(36)] public uint Hash;
            [FieldOffset(40)] public byte Occupied;
            [FieldOffset(41)] public byte _pad0;
            [FieldOffset(42)] public ushort _pad1;
            [FieldOffset(44)] public uint _pad2;

            public static SocketLookupSlot Create(in SocketKey key, uint hash, int moduleIndex, uint compatibilityMask, Vector3 forward)
            {
                return new SocketLookupSlot
                {
                    X = key.X,
                    Y = key.Y,
                    Z = key.Z,
                    Axis = key.Axis,
                    ModuleIndex = moduleIndex,
                    CompatibilityMask = compatibilityMask,
                    Forward = new float3(forward.x, forward.y, forward.z),
                    Hash = hash,
                    Occupied = 1
                };
            }

            public bool Matches(in SocketKey key)
            {
                return X == key.X &&
                       Y == key.Y &&
                       Z == key.Z &&
                       Axis == key.Axis;
            }
        }

        private struct IntegrityGraphBuffers
        {
            public NativeArray<IntegrityNodeRecord> Nodes;
            public NativeArray<int2> AdjacencyRanges;
            public NativeArray<int> Adjacency;
            public NativeArray<int> Queue;
            public NativeArray<int> Depths;
            public NativeArray<IntegrityValidationResult> Result;
            public NativeArray<int> AdjacencyCounts;
            public NativeArray<int> AdjacencyWrites;
            public NativeArray<int2> Connections;
            public NativeArray<SocketLookupSlot> SocketLookup;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct IntegrityNodeRecord
        {
            [FieldOffset(0)] public float Mass;
            [FieldOffset(4)] public byte IsSupportRoot;
            [FieldOffset(5)] public byte IsCandidate;
            [FieldOffset(6)] public ushort _pad0;
            [FieldOffset(8)] public ulong _pad1;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct IntegrityValidationResult
        {
            [FieldOffset(0)] public float Integrity;
            [FieldOffset(4)] public int CandidateDepth;
            [FieldOffset(8)] public byte Allowed;
            [FieldOffset(9)] public byte FailureReason;
            [FieldOffset(10)] public ushort _pad0;
            [FieldOffset(12)] public uint _pad1;
        }

        private enum IntegrityFailureReasonCode : byte
        {
            None = 0,
            Unsupported = 1,
            IntegrityExceeded = 2
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct IntegrityValidationJob : IJob
        {
            [ReadOnly] [NoAlias] public NativeArray<IntegrityNodeRecord> Nodes;
            [ReadOnly] [NoAlias] public NativeArray<int2> AdjacencyRanges;
            [ReadOnly] [NoAlias] public NativeArray<int> Adjacency;
            [NoAlias] public NativeArray<int> Queue;
            [NoAlias] public NativeArray<int> Depths;
            [NoAlias] public NativeArray<IntegrityValidationResult> Result;
            public int NodeCount;
            public float IntegrityBudget;
            public float DepthPenalty;
            public float DisconnectedPenalty;

            public void Execute()
            {
                for (int i = 0; i < NodeCount; i++)
                    Depths[i] = -1;

                int head = 0;
                int tail = 0;
                int visited = 0;
                int candidateDepth = -1;
                float integrity = IntegrityBudget;

                for (int i = 0; i < NodeCount; i++)
                {
                    if (Nodes[i].IsSupportRoot == 0)
                        continue;

                    Depths[i] = 0;
                    Queue[tail++] = i;
                }

                if (tail == 0 && NodeCount > 0)
                {
                    Depths[0] = 0;
                    Queue[tail++] = 0;
                }

                while (head < tail)
                {
                    int nodeIndex = Queue[head++];
                    int depth = Depths[nodeIndex];
                    IntegrityNodeRecord node = Nodes[nodeIndex];
                    visited++;

                    integrity -= node.Mass * (1f + depth * DepthPenalty);

                    if (node.IsCandidate != 0)
                        candidateDepth = depth;

                    int2 range = AdjacencyRanges[nodeIndex];
                    int start = range.x;
                    int count = range.y;
                    for (int i = 0; i < count; i++)
                    {
                        int neighborIndex = Adjacency[start + i];
                        if (Depths[neighborIndex] >= 0)
                            continue;

                        Depths[neighborIndex] = depth + 1;
                        Queue[tail++] = neighborIndex;
                    }
                }

                IntegrityFailureReasonCode reason = IntegrityFailureReasonCode.None;
                bool disconnected = visited != NodeCount || candidateDepth < 0;
                if (disconnected)
                {
                    integrity -= DisconnectedPenalty;
                    reason = IntegrityFailureReasonCode.Unsupported;
                }
                else if (integrity < 0f)
                {
                    reason = IntegrityFailureReasonCode.IntegrityExceeded;
                }

                Result[0] = new IntegrityValidationResult
                {
                    Integrity = integrity,
                    CandidateDepth = candidateDepth,
                    Allowed = (byte)(reason == IntegrityFailureReasonCode.None ? 1 : 0),
                    FailureReason = (byte)reason
                };
            }
        }
    }
}
