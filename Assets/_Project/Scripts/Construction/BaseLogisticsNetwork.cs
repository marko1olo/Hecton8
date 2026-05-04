using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Building;
using Hecton8.Crafting;
using Hecton8.Core;
using Hecton8.Economy;
using Hecton8.Gameplay;
using Hecton8.Items;
using Hecton8.Power;
using Hecton8.SaveSystem;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Power-grid-scoped logistics registry. It links passive storage endpoints and production modules without
    /// introducing a scene-wide ticking manager. All queries and reservations are cold-path and zero-LINQ.
    /// </summary>
    internal static class BaseLogisticsNetwork
    {
        internal sealed class LogisticsReservation
        {
            private const int TouchedCrateCapacity = 32;
            // COLD ALLOC: StorageCrate[32] — fixed touched-crate reservation buffer — owner: LogisticsReservation
            private readonly StorageCrate[] _touchedCrates = new StorageCrate[TouchedCrateCapacity];
            private int _touchedCrateCount;

            public int ReservationId { get; private set; }
            public PowerGrid Grid { get; private set; }
            public bool IsPrepared { get; private set; }

            public int TouchedCrateCount => _touchedCrateCount;

            public void Initialize(int reservationId, PowerGrid grid)
            {
                ReservationId = reservationId;
                Grid = grid;
                IsPrepared = true;
                ClearTouchedCrates();
            }

            public bool TryAddTouchedCrate(StorageCrate crate)
            {
                if (crate == null)
                    return true;

                for (int i = 0; i < _touchedCrateCount; i++)
                {
                    if (ReferenceEquals(_touchedCrates[i], crate))
                        return true;
                }

                if (_touchedCrateCount >= TouchedCrateCapacity)
                    return false;

                _touchedCrates[_touchedCrateCount++] = crate;
                return true;
            }

            public StorageCrate GetTouchedCrate(int index)
            {
                return index >= 0 && index < _touchedCrateCount ? _touchedCrates[index] : null;
            }

            public void Release()
            {
                ReservationId = 0;
                Grid = null;
                IsPrepared = false;
                ClearTouchedCrates();
            }

            private void ClearTouchedCrates()
            {
                for (int i = 0; i < _touchedCrateCount; i++)
                    _touchedCrates[i] = null;

                _touchedCrateCount = 0;
            }
        }

        private struct StorageEndpoint
        {
            public StorageCrate Crate;
            public PowerNode Node;
        }

        private struct FabricatorEndpoint
        {
            public Fabricator Fabricator;
            public PowerNode Node;
        }

        private struct RecyclerEndpoint
        {
            public ResourceRecyclerModule Recycler;
            public PowerNode Node;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct LogisticsPipeRouteBfsJob : IJob
        {
            public int NodeCount;
            public int StartNodeIndex;

            [ReadOnly] public NativeArray<int> EdgeOffsets;
            [ReadOnly] public NativeArray<int> EdgeDestinations;
            [ReadOnly] public NativeArray<byte> StorageCapacityByNode;

            public NativeArray<byte> Visited;
            public NativeArray<int> Queue;
            public NativeArray<int> ResultNodeIndex;

            public void Execute()
            {
                if (!ResultNodeIndex.IsCreated || ResultNodeIndex.Length <= 0)
                    return;

                ResultNodeIndex[0] = -1;

                int safeNodeCount = math.min(NodeCount, math.min(StorageCapacityByNode.Length, math.min(Visited.Length, Queue.Length)));
                if (safeNodeCount <= 0 ||
                    StartNodeIndex < 0 ||
                    StartNodeIndex >= safeNodeCount ||
                    !EdgeOffsets.IsCreated ||
                    EdgeOffsets.Length <= safeNodeCount ||
                    !EdgeDestinations.IsCreated)
                {
                    return;
                }

                for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
                    Visited[nodeIndex] = 0;

                int head = 0;
                int tail = 0;
                Queue[tail++] = StartNodeIndex;
                Visited[StartNodeIndex] = 1;

                while (head < tail)
                {
                    int nodeIndex = Queue[head++];
                    if (StorageCapacityByNode[nodeIndex] != 0)
                    {
                        ResultNodeIndex[0] = nodeIndex;
                        return;
                    }

                    int edgeStart = EdgeOffsets[nodeIndex];
                    int edgeEnd = EdgeOffsets[nodeIndex + 1];
                    if (edgeStart < 0 || edgeEnd < edgeStart || edgeEnd > EdgeDestinations.Length)
                        continue;

                    for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                    {
                        int destinationNodeIndex = EdgeDestinations[edgeIndex];
                        if (destinationNodeIndex < 0 ||
                            destinationNodeIndex >= safeNodeCount ||
                            Visited[destinationNodeIndex] != 0)
                        {
                            continue;
                        }

                        Visited[destinationNodeIndex] = 1;
                        if (tail >= safeNodeCount)
                            return;

                        Queue[tail++] = destinationNodeIndex;
                    }
                }
            }
        }

        // COLD ALLOC: List<StorageEndpoint>[16] — logistics storage registry — owner: BaseLogisticsNetwork
        private static readonly List<StorageEndpoint> s_StorageEndpoints = new List<StorageEndpoint>(16);
        // COLD ALLOC: List<FabricatorEndpoint>[8] — fabrication endpoint registry — owner: BaseLogisticsNetwork
        private static readonly List<FabricatorEndpoint> s_FabricatorEndpoints = new List<FabricatorEndpoint>(8);
        // COLD ALLOC: List<RecyclerEndpoint>[8] — recycler endpoint registry — owner: BaseLogisticsNetwork
        private static readonly List<RecyclerEndpoint> s_RecyclerEndpoints = new List<RecyclerEndpoint>(8);
        private const int ReservationPoolCapacity = 64;
        // COLD ALLOC: LogisticsReservation[64] â€” fixed logistics reservation token pool â€” owner: BaseLogisticsNetwork
        private static readonly LogisticsReservation[] s_ReservationPool = CreateReservationPool();
        private static int s_ReservationPoolCount = ReservationPoolCapacity;
        private static int s_NextReservationId = 1;
        private const int RouteScratchInitialNodeCapacity = 32;
        private const int RouteScratchInitialEdgeCapacity = 64;
        private static NativeArray<int> s_RouteEdgeOffsets;
        private static NativeArray<int> s_RouteEdgeDestinations;
        private static NativeArray<int> s_RouteEdgeWriteCursor;
        private static NativeArray<byte> s_RouteStorageCapacityByNode;
        private static NativeArray<byte> s_RouteVisited;
        private static NativeArray<int> s_RouteQueue;
        private static NativeArray<int> s_RouteResultNodeIndex;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_StorageEndpoints.Clear();
            s_FabricatorEndpoints.Clear();
            s_RecyclerEndpoints.Clear();
            ResetReservationPool();
            s_NextReservationId = 1;
            DisposeRouteScratch();
        }

        public static void RegisterStorage(StorageCrate crate, PowerNode node)
        {
            if (crate == null || node == null)
                return;

            for (int i = 0; i < s_StorageEndpoints.Count; i++)
            {
                if (ReferenceEquals(s_StorageEndpoints[i].Crate, crate))
                    return;
            }

            s_StorageEndpoints.Add(new StorageEndpoint
            {
                Crate = crate,
                Node = node
            });
        }

        public static void UnregisterStorage(StorageCrate crate)
        {
            for (int i = s_StorageEndpoints.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(s_StorageEndpoints[i].Crate, crate))
                    s_StorageEndpoints.RemoveAt(i);
            }
        }

        public static void RegisterFabricator(Fabricator fabricator, PowerNode node)
        {
            if (fabricator == null || node == null)
                return;

            for (int i = 0; i < s_FabricatorEndpoints.Count; i++)
            {
                if (ReferenceEquals(s_FabricatorEndpoints[i].Fabricator, fabricator))
                    return;
            }

            s_FabricatorEndpoints.Add(new FabricatorEndpoint
            {
                Fabricator = fabricator,
                Node = node
            });
        }

        public static void UnregisterFabricator(Fabricator fabricator)
        {
            for (int i = s_FabricatorEndpoints.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(s_FabricatorEndpoints[i].Fabricator, fabricator))
                    s_FabricatorEndpoints.RemoveAt(i);
            }
        }

        public static void RegisterRecycler(ResourceRecyclerModule recycler, PowerNode node)
        {
            if (recycler == null || node == null)
                return;

            for (int i = 0; i < s_RecyclerEndpoints.Count; i++)
            {
                if (ReferenceEquals(s_RecyclerEndpoints[i].Recycler, recycler))
                    return;
            }

            s_RecyclerEndpoints.Add(new RecyclerEndpoint
            {
                Recycler = recycler,
                Node = node
            });
        }

        public static void UnregisterRecycler(ResourceRecyclerModule recycler)
        {
            for (int i = s_RecyclerEndpoints.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(s_RecyclerEndpoints[i].Recycler, recycler))
                    s_RecyclerEndpoints.RemoveAt(i);
            }
        }

        public static int CountAccessibleItem(PowerGrid grid, ItemData item)
        {
            if (grid == null || item == null)
                return 0;

            int count = 0;
            for (int i = 0; i < s_StorageEndpoints.Count; i++)
            {
                StorageEndpoint endpoint = s_StorageEndpoints[i];
                if (endpoint.Crate == null || endpoint.Node == null || endpoint.Node.Grid != grid)
                    continue;

                count += endpoint.Crate.CountItem(item);
            }

            return count;
        }

        public static int CountAccessibleItem(PowerGrid grid, int itemHashId)
        {
            if (grid == null || itemHashId == 0)
                return 0;

            int count = 0;
            for (int i = 0; i < s_StorageEndpoints.Count; i++)
            {
                StorageEndpoint endpoint = s_StorageEndpoints[i];
                if (endpoint.Crate == null || endpoint.Node == null || endpoint.Node.Grid != grid)
                    continue;

                count += endpoint.Crate.CountItemByHash(itemHashId);
            }

            return count;
        }

        public static bool TryDepositItem(PowerGrid grid, ItemData item, int amount, out int deposited)
        {
            deposited = 0;
            if (grid == null || item == null || amount <= 0)
                return false;

            for (int i = 0; i < s_StorageEndpoints.Count && deposited < amount; i++)
            {
                StorageEndpoint endpoint = s_StorageEndpoints[i];
                if (endpoint.Crate == null || endpoint.Node == null || endpoint.Node.Grid != grid)
                    continue;

                while (deposited < amount && endpoint.Crate.TryAddAutomatedItem(item))
                    deposited++;
            }

            return deposited > 0;
        }

        public static bool TryConsumeAccessibleItem(PowerGrid grid, int itemHashId, int amount)
        {
            if (grid == null || itemHashId == 0 || amount <= 0)
                return false;

            int remaining = amount;
            for (int i = 0; i < s_StorageEndpoints.Count && remaining > 0; i++)
            {
                StorageEndpoint endpoint = s_StorageEndpoints[i];
                if (endpoint.Crate == null || endpoint.Node == null || endpoint.Node.Grid != grid)
                    continue;

                while (remaining > 0 && endpoint.Crate.TryConsumeItemByHash(itemHashId))
                    remaining--;
            }

            return remaining <= 0;
        }

        public static bool TryDepositItem(PowerNode sourceNode, ItemData item, int amount, out int deposited)
        {
            deposited = 0;
            if (sourceNode == null)
                return false;

            PowerGrid grid = sourceNode.Grid;
            if (grid == null || item == null || amount <= 0)
                return false;

            int routeWatchdog = math.max(1, amount);
            while (deposited < amount && routeWatchdog-- > 0)
            {
                if (!TryResolveNearestStorageEndpoint(sourceNode, grid, out int endpointIndex))
                    break;

                StorageCrate crate = s_StorageEndpoints[endpointIndex].Crate;
                if (crate == null)
                    break;

                int depositedBeforeCrate = deposited;
                while (deposited < amount && crate.TryAddAutomatedItem(item))
                    deposited++;

                if (deposited == depositedBeforeCrate)
                    break;
            }

            return deposited > 0;
        }

        private static bool TryResolveNearestStorageEndpoint(PowerNode sourceNode, PowerGrid grid, out int endpointIndex)
        {
            endpointIndex = -1;
            if (sourceNode == null || grid == null)
                return false;

            List<PowerNode> topologyNodes = grid.TopologyNodes;
            int nodeCount = topologyNodes != null ? topologyNodes.Count : 0;
            if (nodeCount <= 0)
                return TryResolveFirstStorageEndpoint(grid, out endpointIndex);

            if (!TryResolveTopologyNodeIndex(topologyNodes, sourceNode, out int startNodeIndex))
                return TryResolveFirstStorageEndpoint(grid, out endpointIndex);

            int edgeCount = CountTopologyEdges(grid, topologyNodes, nodeCount);
            EnsureRouteScratchCapacity(nodeCount, edgeCount);
            BuildRouteStorageCapacityFlags(grid, topologyNodes, nodeCount);
            BuildRouteCsr(grid, topologyNodes, nodeCount, edgeCount);

            s_RouteResultNodeIndex[0] = -1;
            new LogisticsPipeRouteBfsJob
            {
                NodeCount = nodeCount,
                StartNodeIndex = startNodeIndex,
                EdgeOffsets = s_RouteEdgeOffsets,
                EdgeDestinations = s_RouteEdgeDestinations,
                StorageCapacityByNode = s_RouteStorageCapacityByNode,
                Visited = s_RouteVisited,
                Queue = s_RouteQueue,
                ResultNodeIndex = s_RouteResultNodeIndex
            }.Run();

            int targetNodeIndex = s_RouteResultNodeIndex[0];
            if (targetNodeIndex < 0 || targetNodeIndex >= nodeCount)
                return false;

            PowerNode targetNode = topologyNodes[targetNodeIndex];
            for (int i = 0; i < s_StorageEndpoints.Count; i++)
            {
                StorageEndpoint endpoint = s_StorageEndpoints[i];
                if (endpoint.Crate == null ||
                    endpoint.Node == null ||
                    endpoint.Node.Grid != grid ||
                    !ReferenceEquals(endpoint.Node, targetNode) ||
                    !endpoint.Crate.HasAutomatedCapacity())
                {
                    continue;
                }

                endpointIndex = i;
                return true;
            }

            return false;
        }

        private static bool TryResolveFirstStorageEndpoint(PowerGrid grid, out int endpointIndex)
        {
            endpointIndex = -1;
            if (grid == null)
                return false;

            for (int i = 0; i < s_StorageEndpoints.Count; i++)
            {
                StorageEndpoint endpoint = s_StorageEndpoints[i];
                if (endpoint.Crate == null ||
                    endpoint.Node == null ||
                    endpoint.Node.Grid != grid ||
                    !endpoint.Crate.HasAutomatedCapacity())
                {
                    continue;
                }

                endpointIndex = i;
                return true;
            }

            return false;
        }

        private static int CountTopologyEdges(PowerGrid grid, List<PowerNode> topologyNodes, int nodeCount)
        {
            int edgeCount = 0;
            for (int sourceIndex = 0; sourceIndex < nodeCount; sourceIndex++)
            {
                PowerNode sourceNode = topologyNodes[sourceIndex];
                if (sourceNode == null)
                    continue;

                List<PowerNode> neighbors = sourceNode.Neighbors;
                int neighborCount = neighbors != null ? neighbors.Count : 0;
                for (int neighborIndex = 0; neighborIndex < neighborCount; neighborIndex++)
                {
                    PowerNode neighbor = neighbors[neighborIndex];
                    if (neighbor == null || neighbor.Grid != grid)
                        continue;

                    if (TryResolveTopologyNodeIndex(topologyNodes, neighbor, out _))
                        edgeCount++;
                }
            }

            return edgeCount;
        }

        private static void BuildRouteStorageCapacityFlags(PowerGrid grid, List<PowerNode> topologyNodes, int nodeCount)
        {
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                s_RouteStorageCapacityByNode[nodeIndex] = 0;

            for (int endpointIndex = 0; endpointIndex < s_StorageEndpoints.Count; endpointIndex++)
            {
                StorageEndpoint endpoint = s_StorageEndpoints[endpointIndex];
                if (endpoint.Crate == null ||
                    endpoint.Node == null ||
                    endpoint.Node.Grid != grid ||
                    !endpoint.Crate.HasAutomatedCapacity())
                {
                    continue;
                }

                if (TryResolveTopologyNodeIndex(topologyNodes, endpoint.Node, out int nodeIndex) &&
                    nodeIndex >= 0 &&
                    nodeIndex < nodeCount)
                {
                    s_RouteStorageCapacityByNode[nodeIndex] = 1;
                }
            }
        }

        private static void BuildRouteCsr(PowerGrid grid, List<PowerNode> topologyNodes, int nodeCount, int edgeCount)
        {
            for (int nodeIndex = 0; nodeIndex <= nodeCount; nodeIndex++)
                s_RouteEdgeOffsets[nodeIndex] = 0;

            for (int sourceIndex = 0; sourceIndex < nodeCount; sourceIndex++)
            {
                PowerNode sourceNode = topologyNodes[sourceIndex];
                if (sourceNode == null)
                    continue;

                List<PowerNode> neighbors = sourceNode.Neighbors;
                int neighborCount = neighbors != null ? neighbors.Count : 0;
                int outDegree = 0;
                for (int neighborIndex = 0; neighborIndex < neighborCount; neighborIndex++)
                {
                    PowerNode neighbor = neighbors[neighborIndex];
                    if (neighbor == null || neighbor.Grid != grid)
                        continue;

                    if (TryResolveTopologyNodeIndex(topologyNodes, neighbor, out _))
                        outDegree++;
                }

                s_RouteEdgeOffsets[sourceIndex + 1] = outDegree;
            }

            for (int nodeIndex = 1; nodeIndex <= nodeCount; nodeIndex++)
                s_RouteEdgeOffsets[nodeIndex] = s_RouteEdgeOffsets[nodeIndex] + s_RouteEdgeOffsets[nodeIndex - 1];

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                s_RouteEdgeWriteCursor[nodeIndex] = s_RouteEdgeOffsets[nodeIndex];

            for (int sourceIndex = 0; sourceIndex < nodeCount; sourceIndex++)
            {
                PowerNode sourceNode = topologyNodes[sourceIndex];
                if (sourceNode == null)
                    continue;

                List<PowerNode> neighbors = sourceNode.Neighbors;
                int neighborCount = neighbors != null ? neighbors.Count : 0;
                for (int neighborIndex = 0; neighborIndex < neighborCount; neighborIndex++)
                {
                    PowerNode neighbor = neighbors[neighborIndex];
                    if (neighbor == null || neighbor.Grid != grid)
                        continue;

                    if (!TryResolveTopologyNodeIndex(topologyNodes, neighbor, out int destinationIndex))
                        continue;

                    int writeIndex = s_RouteEdgeWriteCursor[sourceIndex];
                    if (writeIndex < 0 || writeIndex >= edgeCount)
                        continue;

                    s_RouteEdgeWriteCursor[sourceIndex] = writeIndex + 1;
                    s_RouteEdgeDestinations[writeIndex] = destinationIndex;
                }
            }
        }

        private static bool TryResolveTopologyNodeIndex(List<PowerNode> topologyNodes, PowerNode node, out int nodeIndex)
        {
            nodeIndex = -1;
            if (topologyNodes == null || node == null)
                return false;

            int scratchIndex = node.GraphScratchIndex;
            if (scratchIndex >= 0 &&
                scratchIndex < topologyNodes.Count &&
                ReferenceEquals(topologyNodes[scratchIndex], node))
            {
                nodeIndex = scratchIndex;
                return true;
            }

            for (int i = 0; i < topologyNodes.Count; i++)
            {
                if (!ReferenceEquals(topologyNodes[i], node))
                    continue;

                nodeIndex = i;
                return true;
            }

            return false;
        }

        public static bool TryResolveNearestSupplyEndpoint(PowerGrid grid, int itemHashId, Vector3 origin, out Vector3 position)
        {
            position = Vector3.zero;
            if (grid == null || itemHashId == 0)
                return false;

            bool found = false;
            float bestDistanceSq = float.MaxValue;
            for (int i = 0; i < s_StorageEndpoints.Count; i++)
            {
                StorageEndpoint endpoint = s_StorageEndpoints[i];
                if (endpoint.Crate == null ||
                    endpoint.Node == null ||
                    endpoint.Node.Grid != grid ||
                    endpoint.Crate.CountItemByHash(itemHashId) <= 0)
                {
                    continue;
                }

                Vector3 candidatePosition = endpoint.Crate.transform.position;
                float distanceSq = (candidatePosition - origin).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                position = candidatePosition;
                found = true;
            }

            bool hasNetworkStock = found || CountAccessibleItem(grid, itemHashId) > 0;
            if (!hasNetworkStock)
                return found;

            for (int i = 0; i < s_FabricatorEndpoints.Count; i++)
            {
                FabricatorEndpoint endpoint = s_FabricatorEndpoints[i];
                if (endpoint.Fabricator == null || endpoint.Node == null || endpoint.Node.Grid != grid)
                    continue;

                Vector3 candidatePosition = endpoint.Fabricator.transform.position;
                float distanceSq = (candidatePosition - origin).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                position = candidatePosition;
                found = true;
            }

            return found;
        }

        /// <summary>
        /// Prepare phase for legacy string-id callers. It resolves items through the item catalog and reserves slots
        /// without physically moving them.
        /// </summary>
        public static bool TryReserveResources(
            PowerGrid grid,
            Dictionary<string, int> costs,
            ItemCatalog itemCatalog,
            out LogisticsReservation reservation)
        {
            reservation = null;

            if (grid == null || costs == null || costs.Count == 0)
                return false;

            if (!TryRentReservation(grid, out LogisticsReservation preparedReservation))
                return false;

            var enumerator = costs.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<string, int> pair = enumerator.Current;
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0)
                    continue;

                int itemHashId = LocHash.Compute(pair.Key);
                if (itemHashId == 0 || !TryReserveSingleItemByHash(grid, itemHashId, pair.Value, preparedReservation))
                {
                    RollbackReserved(preparedReservation);
                    return false;
                }
            }

            reservation = preparedReservation;
            return true;
        }

        /// <summary>
        /// Prepare phase for authored recipe costs. Reserves slots across all connected storage crates in the grid.
        /// </summary>
        public static bool TryReserveResources(
            PowerGrid grid,
            List<InventoryCost> costs,
            out LogisticsReservation reservation)
        {
            reservation = null;

            if (grid == null || costs == null || costs.Count == 0)
                return false;

            if (!TryRentReservation(grid, out LogisticsReservation preparedReservation))
                return false;

            for (int i = 0; i < costs.Count; i++)
            {
                InventoryCost cost = costs[i];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                if (!TryReserveSingleItem(grid, cost.item, cost.amount, preparedReservation))
                {
                    RollbackReserved(preparedReservation);
                    return false;
                }
            }

            reservation = preparedReservation;
            return true;
        }

        public static bool TryReserveResources(
            PowerGrid grid,
            int[] itemHashIds,
            int[] amounts,
            int costCount,
            out LogisticsReservation reservation)
        {
            reservation = null;

            if (grid == null ||
                itemHashIds == null ||
                amounts == null ||
                costCount <= 0 ||
                costCount > itemHashIds.Length ||
                costCount > amounts.Length)
            {
                return false;
            }

            if (!TryRentReservation(grid, out LogisticsReservation preparedReservation))
                return false;

            for (int i = 0; i < costCount; i++)
            {
                int itemHashId = itemHashIds[i];
                int amount = amounts[i];
                if (itemHashId == 0 || amount <= 0)
                    continue;

                if (!TryReserveSingleItemByHash(grid, itemHashId, amount, preparedReservation))
                {
                    RollbackReserved(preparedReservation);
                    return false;
                }
            }

            reservation = preparedReservation;
            return true;
        }

        /// <summary>
        /// Commit phase. Reserved slots are physically removed from the underlying storage crates.
        /// </summary>
        public static void CommitReserved(LogisticsReservation reservation)
        {
            if (reservation == null || !reservation.IsPrepared)
                return;

            int reservationId = reservation.ReservationId;
            int touchedCrateCount = reservation.TouchedCrateCount;
            for (int i = 0; i < touchedCrateCount; i++)
            {
                StorageCrate crate = reservation.GetTouchedCrate(i);
                crate?.CommitReservation(reservationId);
            }

            ReturnReservation(reservation);
        }

        public static void CommitReservedViaCommandQueue(LogisticsReservation reservation)
        {
            CommitReservedViaCommandQueue(reservation, 0);
        }

        /// <summary>
        /// Enqueues storage reservation commit commands and tags them with an optional requester id for commit acknowledgement.
        /// </summary>
        /// <param name="reservation">Prepared logistics reservation.</param>
        /// <param name="requesterId">Optional requester id notified by the command queue after commit.</param>
        public static void CommitReservedViaCommandQueue(LogisticsReservation reservation, int requesterId)
        {
            if (reservation == null || !reservation.IsPrepared)
                return;

            int reservationId = reservation.ReservationId;
            int touchedCrateCount = reservation.TouchedCrateCount;
            for (int i = 0; i < touchedCrateCount; i++)
            {
                StorageCrate crate = reservation.GetTouchedCrate(i);
                if (crate == null)
                    continue;

                int token = ThreadSafeCommandQueue.RegisterGameObjectTarget(crate.gameObject);
                if (token > 0)
                    ThreadSafeCommandQueue.Enqueue(EntityCommand.CreateCommitStorageReservation(token, reservationId, requesterId));
                else
                    crate.CommitReservation(reservationId);
            }

            ReturnReservation(reservation);
        }

        /// <summary>
        /// Rollback phase. Reserved slots become available again without changing physical storage contents.
        /// </summary>
        public static void RollbackReserved(LogisticsReservation reservation)
        {
            if (reservation == null || !reservation.IsPrepared)
                return;

            int reservationId = reservation.ReservationId;
            int touchedCrateCount = reservation.TouchedCrateCount;
            for (int i = 0; i < touchedCrateCount; i++)
            {
                StorageCrate crate = reservation.GetTouchedCrate(i);
                crate?.ReleaseReservation(reservationId);
            }

            ReturnReservation(reservation);
        }

        private static LogisticsReservation[] CreateReservationPool()
        {
            LogisticsReservation[] pool = new LogisticsReservation[ReservationPoolCapacity]; // COLD ALLOC: LogisticsReservation[64] â€” fixed reservation token storage â€” owner: BaseLogisticsNetwork
            for (int i = 0; i < ReservationPoolCapacity; i++)
                pool[i] = new LogisticsReservation(); // COLD ALLOC: LogisticsReservation[1] â€” prewarmed logistics reservation token â€” owner: BaseLogisticsNetwork

            return pool;
        }

        private static void ResetReservationPool()
        {
            for (int i = 0; i < ReservationPoolCapacity; i++)
            {
                LogisticsReservation reservation = s_ReservationPool[i];
                if (reservation == null)
                    continue;

                reservation.Release();
            }

            s_ReservationPoolCount = ReservationPoolCapacity;
        }

        private static bool TryRentReservation(PowerGrid grid, out LogisticsReservation reservation)
        {
            reservation = null;
            if (grid == null || s_ReservationPoolCount <= 0)
                return false;

            int poolIndex = --s_ReservationPoolCount;
            reservation = s_ReservationPool[poolIndex];
            if (reservation == null)
            {
                s_ReservationPoolCount++;
                return false;
            }

            reservation.Initialize(GetNextReservationId(), grid);
            return true;
        }

        private static void ReturnReservation(LogisticsReservation reservation)
        {
            if (reservation == null)
                return;

            reservation.Release();
            if (s_ReservationPoolCount >= ReservationPoolCapacity)
                return;

            s_ReservationPool[s_ReservationPoolCount++] = reservation;
        }

        private static bool TryReserveSingleItem(
            PowerGrid grid,
            ItemData item,
            int amount,
            LogisticsReservation reservation)
        {
            if (grid == null || item == null || amount <= 0 || reservation == null || !reservation.IsPrepared)
                return false;

            if (CountAccessibleItem(grid, item) < amount)
                return false;

            int remaining = amount;
            int reservationId = reservation.ReservationId;

            for (int i = 0; i < s_StorageEndpoints.Count && remaining > 0; i++)
            {
                StorageEndpoint endpoint = s_StorageEndpoints[i];
                if (endpoint.Crate == null || endpoint.Node == null || endpoint.Node.Grid != grid)
                    continue;

                while (remaining > 0 && endpoint.Crate.TryReserveItem(item, reservationId))
                {
                    if (!reservation.TryAddTouchedCrate(endpoint.Crate))
                    {
                        endpoint.Crate.ReleaseReservation(reservationId);
                        return false;
                    }

                    remaining--;
                }
            }

            return remaining <= 0;
        }

        private static bool TryReserveSingleItemByHash(
            PowerGrid grid,
            int itemHashId,
            int amount,
            LogisticsReservation reservation)
        {
            if (grid == null || itemHashId == 0 || amount <= 0 || reservation == null || !reservation.IsPrepared)
                return false;

            if (CountAccessibleItem(grid, itemHashId) < amount)
                return false;

            int remaining = amount;
            int reservationId = reservation.ReservationId;

            for (int i = 0; i < s_StorageEndpoints.Count && remaining > 0; i++)
            {
                StorageEndpoint endpoint = s_StorageEndpoints[i];
                if (endpoint.Crate == null || endpoint.Node == null || endpoint.Node.Grid != grid)
                    continue;

                while (remaining > 0 && endpoint.Crate.TryReserveItemByHash(itemHashId, reservationId))
                {
                    if (!reservation.TryAddTouchedCrate(endpoint.Crate))
                    {
                        endpoint.Crate.ReleaseReservation(reservationId);
                        return false;
                    }

                    remaining--;
                }
            }

            return remaining <= 0;
        }

        private static void EnsureRouteScratchCapacity(int nodeCount, int edgeCount)
        {
            EnsureNativeIntArray(ref s_RouteEdgeOffsets, math.max(RouteScratchInitialNodeCapacity + 1, nodeCount + 1));
            EnsureNativeIntArray(ref s_RouteEdgeDestinations, math.max(RouteScratchInitialEdgeCapacity, edgeCount));
            EnsureNativeIntArray(ref s_RouteEdgeWriteCursor, math.max(RouteScratchInitialNodeCapacity, nodeCount));
            EnsureNativeByteArray(ref s_RouteStorageCapacityByNode, math.max(RouteScratchInitialNodeCapacity, nodeCount));
            EnsureNativeByteArray(ref s_RouteVisited, math.max(RouteScratchInitialNodeCapacity, nodeCount));
            EnsureNativeIntArray(ref s_RouteQueue, math.max(RouteScratchInitialNodeCapacity, nodeCount));
            EnsureNativeIntArray(ref s_RouteResultNodeIndex, 1);
        }

        private static void EnsureNativeIntArray(ref NativeArray<int> array, int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (array.IsCreated && array.Length >= safeLength)
                return;

            DisposeNativeArray(ref array);
            array = new NativeArray<int>(safeLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static void EnsureNativeByteArray(ref NativeArray<byte> array, int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (array.IsCreated && array.Length >= safeLength)
                return;

            DisposeNativeArray(ref array);
            array = new NativeArray<byte>(safeLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static void DisposeRouteScratch()
        {
            DisposeNativeArray(ref s_RouteEdgeOffsets);
            DisposeNativeArray(ref s_RouteEdgeDestinations);
            DisposeNativeArray(ref s_RouteEdgeWriteCursor);
            DisposeNativeArray(ref s_RouteStorageCapacityByNode);
            DisposeNativeArray(ref s_RouteVisited);
            DisposeNativeArray(ref s_RouteQueue);
            DisposeNativeArray(ref s_RouteResultNodeIndex);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            array.Dispose();
            array = default;
        }

        private static int GetNextReservationId()
        {
            int nextId = s_NextReservationId++;
            if (nextId > 0)
                return nextId;

            s_NextReservationId = 1;
            return s_NextReservationId++;
        }
    }
}
