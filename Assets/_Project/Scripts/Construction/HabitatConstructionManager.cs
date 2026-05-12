using System;
using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Inventory;
using Hecton8.Items;
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
        private const int InitialInventoryPlacementCapacity = 64;
        private const int InitialCostCapacity = 8;
        private const string NativeMemoryOwner = nameof(HabitatConstructionManager);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;

        private readonly List<ModuleSocket> _moduleSocketBuffer;
        private readonly List<int2> _connectionBuffer;
        private readonly Dictionary<SocketKey, SocketMatchEntry> _socketLookup;

        private PlayerInventory.ItemPlacement[] _inventoryPlacementBuffer;
        private int[] _costHashBuffer;
        private int[] _costRemainingBuffer;
        private int[] _costRemovedBuffer;
        private ItemData[] _costItemBuffer;
        private int[] _adjacencyCountBuffer;
        private int[] _adjacencyWriteBuffer;

        private NativeArray<IntegrityNodeRecord> _nodeBuffer;
        private NativeArray<int2> _adjacencyRanges;
        private NativeArray<int> _adjacency;
        private NativeArray<int> _queueBuffer;
        private NativeArray<int> _depthBuffer;
        private NativeArray<IntegrityValidationResult> _resultBuffer;

        private JobHandle _validationHandle;
        private bool _validationPending;
        private float _lastIntegrityScore;
        private bool _lastPlacementAllowed = true;
        private string _lastBlockReason = string.Empty;

        public HabitatConstructionManager()
        {
            // COLD ALLOC: List<ModuleSocket>[16] — reusable module socket scan buffer for preview and graph assembly — owner: HabitatConstructionManager
            _moduleSocketBuffer = new List<ModuleSocket>(16);
            // COLD ALLOC: List<int2>[128] — reusable undirected connection list for adjacency assembly — owner: HabitatConstructionManager
            _connectionBuffer = new List<int2>(128);
            // COLD ALLOC: Dictionary<SocketKey,SocketMatchEntry>[256] — reusable quantized socket lookup for O(N) connection assembly — owner: HabitatConstructionManager
            _socketLookup = new Dictionary<SocketKey, SocketMatchEntry>(256);
            // COLD ALLOC: PlayerInventory.ItemPlacement[64] — reusable inventory placement snapshot for zero-GC build-cost checks — owner: HabitatConstructionManager
            _inventoryPlacementBuffer = new PlayerInventory.ItemPlacement[InitialInventoryPlacementCapacity];
            // COLD ALLOC: Int32[8] — reusable hashed build-cost keys — owner: HabitatConstructionManager
            _costHashBuffer = new int[InitialCostCapacity];
            // COLD ALLOC: Int32[8] — reusable remaining-cost counters — owner: HabitatConstructionManager
            _costRemainingBuffer = new int[InitialCostCapacity];
            // COLD ALLOC: Int32[8] — reusable removed-cost counters for rollback — owner: HabitatConstructionManager
            _costRemovedBuffer = new int[InitialCostCapacity];
            // COLD ALLOC: ItemData[8] — reusable rollback item references — owner: HabitatConstructionManager
            _costItemBuffer = new ItemData[InitialCostCapacity];
            // COLD ALLOC: Int32[96] — reusable adjacency-degree staging buffer — owner: HabitatConstructionManager
            _adjacencyCountBuffer = new int[InitialNodeCapacity];
            // COLD ALLOC: Int32[96] — reusable adjacency write-offset staging buffer — owner: HabitatConstructionManager
            _adjacencyWriteBuffer = new int[InitialNodeCapacity];

            AllocateNativeBuffers(InitialNodeCapacity, InitialAdjacencyCapacity);
        }

        public bool IsValidationPending => _validationPending;
        public float LastIntegrityScore => _lastIntegrityScore;
        public bool LastPlacementAllowed => _lastPlacementAllowed;
        public string LastBlockReason => _lastBlockReason;

        public void Dispose()
        {
            JobHandle teardownDependency = CancelPendingValidationForTeardown();
            DisposeNativeBuffers(teardownDependency);
            JobHandle.ScheduleBatchedJobs();
        }

        public void ResetValidation()
        {
            CompletePendingValidation();
            _validationPending = false;
            _lastIntegrityScore = 0f;
            _lastPlacementAllowed = true;
            _lastBlockReason = string.Empty;
        }

        public float3 SnapWorldPosition(float3 worldPosition, float gridSize)
        {
            float snappedGrid = gridSize > 0f ? gridSize : DefaultGridSize;
            int gridMillimeters = ResolveGridMillimeters(snappedGrid);
            Vector3 absolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(
                new Vector3(worldPosition.x, worldPosition.y, worldPosition.z));
            Vector3 snappedAbsolutePosition = new Vector3(
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

            double millimetersDouble = meters * 1000.0;
            long millimeters = millimetersDouble >= 0.0
                ? (long)(millimetersDouble + 0.5)
                : (long)(millimetersDouble - 0.5);
            long snappedMillimeters = SnapIntegerToGrid(millimeters, gridMillimeters);
            return (float)(snappedMillimeters * 0.001);
        }

        private static long SnapIntegerToGrid(long value, int grid)
        {
            long safeGrid = grid > 0 ? grid : 1;
            long halfGrid = safeGrid >> 1;
            if (value >= 0)
                return ((value + halfGrid) / safeGrid) * safeGrid;

            return ((value - halfGrid) / safeGrid) * safeGrid;
        }

        public bool TryResolveSocketAlignment(
            Transform ghostRoot,
            List<ModuleSocket> ghostSockets,
            ModuleSocket targetSocket,
            float yawOffsetDegrees,
            out Vector3 alignedPosition,
            out Quaternion alignedRotation,
            out ModuleSocket alignedGhostSocket)
        {
            int yawStep = (int)math.floor(yawOffsetDegrees * 0.011111111f + 0.5f);
            return TryResolveSocketAlignment(
                ghostRoot,
                ghostSockets,
                targetSocket,
                yawStep,
                out alignedPosition,
                out alignedRotation,
                out alignedGhostSocket);
        }

        public bool TryResolveSocketAlignment(
            Transform ghostRoot,
            List<ModuleSocket> ghostSockets,
            ModuleSocket targetSocket,
            int yawStep,
            out Vector3 alignedPosition,
            out Quaternion alignedRotation,
            out ModuleSocket alignedGhostSocket)
        {
            alignedPosition = default;
            alignedRotation = default;
            alignedGhostSocket = null;

            if (ghostRoot == null || targetSocket == null || ghostSockets == null || ghostSockets.Count == 0)
                return false;

            Transform targetTransform = targetSocket.transform;
            Quaternion socketYawRotation = ResolveSocketYawRotation(yawStep);
            Quaternion desiredSocketRotation = targetTransform.rotation * socketYawRotation;
            Vector3 targetPosition = targetTransform.position;
            Quaternion inverseGhostRotation = Quaternion.Inverse(ghostRoot.rotation);
            Vector3 ghostPosition = ghostRoot.position;
            float bestScore = float.MaxValue;

            for (int i = 0; i < ghostSockets.Count; i++)
            {
                ModuleSocket candidate = ghostSockets[i];
                if (candidate == null)
                    continue;

                if (!candidate.isActiveAndEnabled)
                    continue;

                if (!candidate.CanConnectTo(targetSocket))
                    continue;

                Transform candidateTransform = candidate.transform;
                Quaternion localSocketRotation = inverseGhostRotation * candidateTransform.rotation;
                Vector3 localSocketPosition = ghostRoot.InverseTransformPoint(candidateTransform.position);
                Quaternion candidateRotation = desiredSocketRotation * Quaternion.Inverse(localSocketRotation);
                Vector3 rotatedLocalOffset = candidateRotation * localSocketPosition;
                Vector3 candidatePosition = targetPosition - rotatedLocalOffset;
                float score = Vector3.SqrMagnitude(candidatePosition - ghostPosition);

                if (score >= bestScore)
                    continue;

                bestScore = score;
                alignedPosition = candidatePosition;
                alignedRotation = candidateRotation;
                alignedGhostSocket = candidate;
            }

            return alignedGhostSocket != null;
        }

        public bool HasBuildResources(PlayerInventory inventory, BuildableData data)
        {
            if (data == null || data.buildCost == null || data.buildCost.Count == 0)
                return true;

            if (inventory == null || inventory.Grid == null)
                return false;

            int costCount = PrepareCostBuffers(data);
            if (costCount == 0)
                return true;

            EnsureInventoryPlacementCapacity(inventory.Grid.TotalCells);
            int placementCount = inventory.GetPlacements(_inventoryPlacementBuffer);

            for (int placementIndex = 0; placementIndex < placementCount; placementIndex++)
            {
                PlayerInventory.ItemPlacement placement = _inventoryPlacementBuffer[placementIndex];
                if (placement.itemHashId == 0 || placement.stackCount <= 0)
                    continue;

                for (int costIndex = 0; costIndex < costCount; costIndex++)
                {
                    if (_costRemainingBuffer[costIndex] <= 0 || _costHashBuffer[costIndex] != placement.itemHashId)
                        continue;

                    _costRemainingBuffer[costIndex] -= placement.stackCount;
                }
            }

            for (int costIndex = 0; costIndex < costCount; costIndex++)
            {
                if (_costRemainingBuffer[costIndex] > 0)
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

            int costCount = PrepareCostBuffers(data);
            if (costCount == 0)
                return true;

            Array.Clear(_costRemovedBuffer, 0, costCount);

            for (int costIndex = 0; costIndex < costCount; costIndex++)
            {
                int remaining = _costRemainingBuffer[costIndex];
                while (remaining > 0)
                {
                    if (!inventory.TryRemoveFirstMatchingItemByHash(_costHashBuffer[costIndex]))
                    {
                        RollbackRemovedResources(inventory, costCount);
                        return false;
                    }

                    _costRemovedBuffer[costIndex]++;
                    remaining--;
                }
            }

            return true;
        }

        public bool ScheduleIntegrityValidation(
            ConstructionManager constructionManager,
            GameObject candidateGhost,
            BuildableData candidateData,
            float gridSize,
            float integrityBudget,
            float depthPenalty)
        {
            if (_validationPending || candidateGhost == null || candidateData == null)
                return false;

            int nodeCount = BuildValidationGraph(constructionManager, candidateGhost, candidateData, gridSize);
            if (nodeCount <= 0)
            {
                _lastPlacementAllowed = false;
                _lastIntegrityScore = -1f;
                _lastBlockReason = UnsupportedReason;
                return false;
            }

            var job = new IntegrityValidationJob
            {
                Nodes = _nodeBuffer,
                NodeCount = nodeCount,
                AdjacencyRanges = _adjacencyRanges,
                Adjacency = _adjacency,
                Queue = _queueBuffer,
                Depths = _depthBuffer,
                Result = _resultBuffer,
                IntegrityBudget = integrityBudget > 0f ? integrityBudget : DefaultIntegrityBudget,
                DepthPenalty = depthPenalty > 0f ? depthPenalty : DefaultDepthPenalty,
                DisconnectedPenalty = DefaultDisconnectedPenalty
            };

            _validationHandle = job.Schedule();
            _validationPending = true;
            _lastPlacementAllowed = false;
            _lastBlockReason = PendingReason;
            return true;
        }

        public bool TryConsumeCompletedValidation()
        {
            if (!_validationPending)
                return false;

            if (!DispatcherJobSwap.TryComplete(ref _validationHandle, false))
                return false;

            _validationPending = false;

            IntegrityValidationResult result = _resultBuffer[0];
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

        private int PrepareCostBuffers(BuildableData data)
        {
            int costCount = data != null && data.buildCost != null ? data.buildCost.Count : 0;
            if (costCount <= 0)
                return 0;

            EnsureCostCapacity(costCount);

            int preparedCount = 0;
            for (int i = 0; i < costCount; i++)
            {
                InventoryCost cost = data.buildCost[i];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                _costHashBuffer[preparedCount] = LocHash.Compute(cost.item.PersistentId);
                _costRemainingBuffer[preparedCount] = cost.amount;
                _costItemBuffer[preparedCount] = cost.item;
                preparedCount++;
            }

            return preparedCount;
        }

        private void RollbackRemovedResources(PlayerInventory inventory, int costCount)
        {
            if (inventory == null)
                return;

            for (int i = 0; i < costCount; i++)
            {
                int removed = _costRemovedBuffer[i];
                if (removed <= 0 || _costItemBuffer[i] == null)
                    continue;

                if (_costItemBuffer[i] != null)
                    inventory.TryAddItem(Hecton.Localization.LocHash.Compute(_costItemBuffer[i].PersistentId), removed);
                _costRemovedBuffer[i] = 0;
            }
        }

        private int BuildValidationGraph(
            ConstructionManager constructionManager,
            GameObject candidateGhost,
            BuildableData candidateData,
            float gridSize)
        {
            int existingCount = constructionManager != null ? constructionManager.ModuleCount : 0;
            int nodeCount = existingCount + 1;
            EnsureNodeCapacity(nodeCount);

            int candidateIndex = 0;
            if (constructionManager != null && constructionManager.SpawnedModules != null)
            {
                IReadOnlyList<GameObject> modules = constructionManager.SpawnedModules;
                for (int i = 0; i < modules.Count; i++)
                {
                    GameObject moduleObject = modules[i];
                    if (moduleObject == null)
                        continue;

                    BuildableData moduleData = ResolveBuildableData(moduleObject);
                    WriteNodeRecord(candidateIndex++, moduleObject.transform.position, moduleData, false);
                }
            }

            WriteNodeRecord(candidateIndex, candidateGhost.transform.position, candidateData, true);

            int activeNodeCount = candidateIndex + 1;
            _connectionBuffer.Clear();
            _socketLookup.Clear();

            int validationGridSize = math.max(1, (int)math.floor(math.max(DefaultGridSize, gridSize) * math.rcp(DefaultSocketQuantization) + 0.5f));
            if (constructionManager != null && constructionManager.SpawnedModules != null)
            {
                IReadOnlyList<GameObject> modules = constructionManager.SpawnedModules;
                int nodeIndex = 0;
                for (int i = 0; i < modules.Count; i++)
                {
                    GameObject moduleObject = modules[i];
                    if (moduleObject == null)
                        continue;

                    IndexSockets(moduleIndex: nodeIndex, root: moduleObject, validationGridSize);
                    nodeIndex++;
                }
            }

            IndexSockets(activeNodeCount - 1, candidateGhost, validationGridSize);
            BuildAdjacency(activeNodeCount);
            return activeNodeCount;
        }

        private void IndexSockets(int moduleIndex, GameObject root, int validationGridSize)
        {
            if (root == null)
                return;

            _moduleSocketBuffer.Clear();
            root.GetComponentsInChildren(true, _moduleSocketBuffer);

            for (int i = 0; i < _moduleSocketBuffer.Count; i++)
            {
                ModuleSocket socket = _moduleSocketBuffer[i];
                if (socket == null)
                    continue;

                if (!socket.isActiveAndEnabled)
                    continue;

                Transform socketTransform = socket.transform;
                int axis = QuantizeAxis(socketTransform.forward);
                SocketKey oppositeKey = SocketKey.Create(socketTransform.position, OppositeAxis(axis), validationGridSize);

                if (_socketLookup.TryGetValue(oppositeKey, out SocketMatchEntry existing))
                {
                    if (existing.ModuleIndex != moduleIndex &&
                        ModuleSocketTopology.AreCompatible(existing.CompatibleType, existing.Direction, socket.CompatibleType, socket.Direction) &&
                        Vector3.Dot(existing.Forward, socketTransform.forward) <= OppositeDirectionDotThreshold)
                    {
                        _connectionBuffer.Add(new int2(existing.ModuleIndex, moduleIndex));
                        continue;
                    }
                }

                SocketKey ownKey = SocketKey.Create(socketTransform.position, axis, validationGridSize);
                _socketLookup[ownKey] = new SocketMatchEntry(moduleIndex, socket.CompatibleType, socket.Direction, socketTransform.forward);
            }
        }

        private void BuildAdjacency(int nodeCount)
        {
            Array.Clear(_adjacencyCountBuffer, 0, nodeCount);

            int connectionCount = _connectionBuffer.Count;
            for (int i = 0; i < connectionCount; i++)
            {
                int2 connection = _connectionBuffer[i];
                _adjacencyCountBuffer[connection.x]++;
                _adjacencyCountBuffer[connection.y]++;
            }

            int adjacencyCount = 0;
            for (int i = 0; i < nodeCount; i++)
            {
                _adjacencyRanges[i] = new int2(adjacencyCount, _adjacencyCountBuffer[i]);
                _adjacencyWriteBuffer[i] = adjacencyCount;
                adjacencyCount += _adjacencyCountBuffer[i];
            }

            EnsureAdjacencyCapacity(adjacencyCount);

            for (int i = 0; i < connectionCount; i++)
            {
                int2 connection = _connectionBuffer[i];
                int abWrite = _adjacencyWriteBuffer[connection.x]++;
                int baWrite = _adjacencyWriteBuffer[connection.y]++;
                _adjacency[abWrite] = connection.y;
                _adjacency[baWrite] = connection.x;
            }
        }

        private void WriteNodeRecord(int index, Vector3 position, BuildableData data, bool isCandidate)
        {
            _nodeBuffer[index] = new IntegrityNodeRecord
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

        private static Quaternion ResolveSocketYawRotation(int yawStep)
        {
            const float halfSqrt = 0.7071067811865476f;
            switch (yawStep & 3)
            {
                case 0: return new Quaternion(0f, 1f, 0f, 0f);
                case 1: return new Quaternion(0f, -halfSqrt, 0f, halfSqrt);
                case 2: return Quaternion.identity;
                default: return new Quaternion(0f, halfSqrt, 0f, halfSqrt);
            }
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

        private void EnsureInventoryPlacementCapacity(int required)
        {
            if (_inventoryPlacementBuffer != null && _inventoryPlacementBuffer.Length >= required)
                return;

            int newCapacity = NextPowerOfTwo(math.max(required, InitialInventoryPlacementCapacity));
            _inventoryPlacementBuffer = new PlayerInventory.ItemPlacement[newCapacity];
        }

        private void EnsureCostCapacity(int required)
        {
            if (_costHashBuffer.Length >= required)
                return;

            int newCapacity = NextPowerOfTwo(math.max(required, InitialCostCapacity));
            _costHashBuffer = new int[newCapacity];
            _costRemainingBuffer = new int[newCapacity];
            _costRemovedBuffer = new int[newCapacity];
            _costItemBuffer = new ItemData[newCapacity];
        }

        private void AllocateNativeBuffers(int nodeCapacity, int adjacencyCapacity)
        {
            // COLD ALLOC: NativeArray<IntegrityNodeRecord>[96] — placement validation node snapshot buffer — owner: HabitatConstructionManager
            _nodeBuffer = new NativeArray<IntegrityNodeRecord>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            // COLD ALLOC: NativeArray<int2>[96] — placement validation adjacency ranges — owner: HabitatConstructionManager
            _adjacencyRanges = new NativeArray<int2>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            // COLD ALLOC: NativeArray<int>[384] — placement validation flattened adjacency list — owner: HabitatConstructionManager
            _adjacency = new NativeArray<int>(adjacencyCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            // COLD ALLOC: NativeArray<int>[96] — placement validation BFS queue staging — owner: HabitatConstructionManager
            _queueBuffer = new NativeArray<int>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            // COLD ALLOC: NativeArray<int>[96] — placement validation BFS depth staging — owner: HabitatConstructionManager
            _depthBuffer = new NativeArray<int>(nodeCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            // COLD ALLOC: NativeArray<IntegrityValidationResult>[1] — placement validation result slot — owner: HabitatConstructionManager
            _resultBuffer = new NativeArray<IntegrityValidationResult>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            RegisterNativeBuffers();
        }

        private void EnsureNodeCapacity(int required)
        {
            if (_nodeBuffer.IsCreated && _nodeBuffer.Length >= required)
                return;

            if (_validationPending)
                CompletePendingValidation();

            DisposeNativeBuffers();

            int newNodeCapacity = NextPowerOfTwo(math.max(required, InitialNodeCapacity));
            int newAdjacencyCapacity = NextPowerOfTwo(math.max(newNodeCapacity * 4, InitialAdjacencyCapacity));
            _adjacencyCountBuffer = new int[newNodeCapacity];
            _adjacencyWriteBuffer = new int[newNodeCapacity];
            AllocateNativeBuffers(newNodeCapacity, newAdjacencyCapacity);
        }

        private void EnsureAdjacencyCapacity(int required)
        {
            if (_adjacency.IsCreated && _adjacency.Length >= required)
                return;

            if (_validationPending)
                CompletePendingValidation();

            NativeArray<int> previousAdjacency = _adjacency;
            int newCapacity = NextPowerOfTwo(math.max(required, InitialAdjacencyCapacity));
            _adjacency = new NativeArray<int>(newCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            RegisterTrackedNativeArray(_adjacency, nameof(_adjacency));

            if (previousAdjacency.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(previousAdjacency);
                previousAdjacency.Dispose();
            }
        }

        private void DisposeNativeBuffers()
        {
            DisposeNativeBuffers(default);
        }

        private JobHandle DisposeNativeBuffers(JobHandle dependency)
        {
            JobHandle disposeHandle = dependency;

            if (_nodeBuffer.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_nodeBuffer);
                disposeHandle = _nodeBuffer.Dispose(disposeHandle);
                _nodeBuffer = default;
            }

            if (_adjacencyRanges.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_adjacencyRanges);
                disposeHandle = _adjacencyRanges.Dispose(disposeHandle);
                _adjacencyRanges = default;
            }

            if (_adjacency.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_adjacency);
                disposeHandle = _adjacency.Dispose(disposeHandle);
                _adjacency = default;
            }

            if (_queueBuffer.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_queueBuffer);
                disposeHandle = _queueBuffer.Dispose(disposeHandle);
                _queueBuffer = default;
            }

            if (_depthBuffer.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_depthBuffer);
                disposeHandle = _depthBuffer.Dispose(disposeHandle);
                _depthBuffer = default;
            }

            if (_resultBuffer.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_resultBuffer);
                disposeHandle = _resultBuffer.Dispose(disposeHandle);
                _resultBuffer = default;
            }

            return disposeHandle;
        }

        private void RegisterNativeBuffers()
        {
            RegisterTrackedNativeArray(_nodeBuffer, nameof(_nodeBuffer));
            RegisterTrackedNativeArray(_adjacencyRanges, nameof(_adjacencyRanges));
            RegisterTrackedNativeArray(_adjacency, nameof(_adjacency));
            RegisterTrackedNativeArray(_queueBuffer, nameof(_queueBuffer));
            RegisterTrackedNativeArray(_depthBuffer, nameof(_depthBuffer));
            RegisterTrackedNativeArray(_resultBuffer, nameof(_resultBuffer));
        }

        private static void RegisterTrackedNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.RegisterNativeArray(
                array,
                NativeMemoryOwner,
                label,
                NativeMemoryLifetime);
        }

        private void CompletePendingValidation()
        {
            if (!_validationPending)
                return;

            DispatcherJobSwap.TryComplete(ref _validationHandle, true);
            _validationPending = false;
        }

        private JobHandle CancelPendingValidationForTeardown()
        {
            if (!_validationPending)
                return _validationHandle;

            JobHandle dependency = _validationHandle;
            _validationHandle = default;
            _validationPending = false;
            return dependency;
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

            public static SocketKey Create(Vector3 position, int axis, int validationGridSize)
            {
                float scale = validationGridSize > 0 ? validationGridSize : 1;
                float3 scaledPosition = (float3)position * scale;
                int3 quantizedPosition = (int3)math.floor(scaledPosition + new float3(0.5f));
                return new SocketKey(
                    quantizedPosition.x,
                    quantizedPosition.y,
                    quantizedPosition.z,
                    axis);
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
            public readonly Vector3 Forward;

            public SocketMatchEntry(int moduleIndex, string compatibleType, ModuleSocketDirection direction, Vector3 forward)
            {
                ModuleIndex = moduleIndex;
                CompatibleType = compatibleType;
                Direction = direction;
                Forward = forward;
            }
        }

        private struct IntegrityNodeRecord
        {
            public float Mass;
            public byte IsSupportRoot;
            public byte IsCandidate;
        }

        private struct IntegrityValidationResult
        {
            public float Integrity;
            public int CandidateDepth;
            public byte Allowed;
            public byte FailureReason;
        }

        private enum IntegrityFailureReasonCode : byte
        {
            None = 0,
            Unsupported = 1,
            IntegrityExceeded = 2
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct IntegrityValidationJob : IJob
        {
            [ReadOnly] public NativeArray<IntegrityNodeRecord> Nodes;
            [ReadOnly] public NativeArray<int2> AdjacencyRanges;
            [ReadOnly] public NativeArray<int> Adjacency;
            public NativeArray<int> Queue;
            public NativeArray<int> Depths;
            public NativeArray<IntegrityValidationResult> Result;
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
