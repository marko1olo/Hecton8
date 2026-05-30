using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Building;
using Hecton8.Crafting;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Economy;
using Hecton8.Gameplay;
using Hecton8.Items;
using Hecton8.Power;
using Hecton8.SaveSystem;
using Unity.Collections;
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

        // COLD ALLOC: StorageEndpoint[64] - fixed logistics storage registry - owner: BaseLogisticsNetwork
        private const int StorageEndpointCapacity = 64;
        private const int FabricatorEndpointCapacity = 32;
        private const int RecyclerEndpointCapacity = 32;
        private static readonly StorageEndpoint[] s_StorageEndpoints = new StorageEndpoint[StorageEndpointCapacity];
        // COLD ALLOC: FabricatorEndpoint[32] - fixed fabrication endpoint registry - owner: BaseLogisticsNetwork
        private static readonly FabricatorEndpoint[] s_FabricatorEndpoints = new FabricatorEndpoint[FabricatorEndpointCapacity];
        // COLD ALLOC: RecyclerEndpoint[32] - fixed recycler endpoint registry - owner: BaseLogisticsNetwork
        private static readonly RecyclerEndpoint[] s_RecyclerEndpoints = new RecyclerEndpoint[RecyclerEndpointCapacity];
        private static int s_StorageEndpointCount;
        private static int s_FabricatorEndpointCount;
        private static int s_RecyclerEndpointCount;
        private const int ReservationPoolCapacity = 64;
        // COLD ALLOC: LogisticsReservation[64] â€” fixed logistics reservation token pool â€” owner: BaseLogisticsNetwork
        private static readonly LogisticsReservation[] s_ReservationPool = CreateReservationPool();
        private static int s_ReservationPoolCount = ReservationPoolCapacity;
        private static int s_NextReservationId = 1;
        private static IDataVault s_DataVault;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < s_StorageEndpointCount; i++)
                s_StorageEndpoints[i] = default;
            for (int i = 0; i < s_FabricatorEndpointCount; i++)
                s_FabricatorEndpoints[i] = default;
            for (int i = 0; i < s_RecyclerEndpointCount; i++)
                s_RecyclerEndpoints[i] = default;

            s_StorageEndpointCount = 0;
            s_FabricatorEndpointCount = 0;
            s_RecyclerEndpointCount = 0;
            ResetReservationPool();
            s_NextReservationId = 1;
            LogisticsRouteScratchMemory.Dispose(s_DataVault);
            s_DataVault = null;
        }

        internal static void BindDataVault(IDataVault vault)
        {
            if (ReferenceEquals(s_DataVault, vault))
                return;

            LogisticsRouteScratchMemory.Dispose(s_DataVault);
            s_DataVault = vault;
        }

        public static void RegisterStorage(StorageCrate crate, PowerNode node)
        {
            if (crate == null || node == null)
                return;

            for (int i = 0; i < s_StorageEndpointCount; i++)
            {
                if (ReferenceEquals(s_StorageEndpoints[i].Crate, crate))
                    return;
            }

            if (s_StorageEndpointCount >= StorageEndpointCapacity)
                return;

            s_StorageEndpoints[s_StorageEndpointCount++] = new StorageEndpoint
            {
                Crate = crate,
                Node = node
            };
        }

        public static void UnregisterStorage(StorageCrate crate)
        {
            for (int i = s_StorageEndpointCount - 1; i >= 0; i--)
            {
                if (ReferenceEquals(s_StorageEndpoints[i].Crate, crate))
                    RemoveStorageEndpointAt(i);
            }
        }

        public static int CollectStorageCratesNonAlloc(Vector3 origin, float radius, StorageCrate[] results)
        {
            if (results == null ||
                results.Length == 0 ||
                !math.isfinite(origin.x) ||
                !math.isfinite(origin.y) ||
                !math.isfinite(origin.z) ||
                !math.isfinite(radius) ||
                radius <= 0f)
            {
                return 0;
            }

            float radiusSq = radius * radius;
            int count = 0;
            for (int i = 0; i < s_StorageEndpointCount; i++)
            {
                StorageEndpoint endpoint = s_StorageEndpoints[i];
                StorageCrate crate = endpoint.Crate;
                if (crate == null || !crate.gameObject.activeInHierarchy)
                    continue;

                float distanceSq = (crate.transform.position - origin).sqrMagnitude;
                if (distanceSq > radiusSq)
                    continue;

                bool duplicate = false;
                for (int resultIndex = 0; resultIndex < count; resultIndex++)
                {
                    if (!ReferenceEquals(results[resultIndex], crate))
                        continue;

                    duplicate = true;
                    break;
                }

                if (duplicate)
                    continue;

                results[count++] = crate;
                if (count >= results.Length)
                    break;
            }

            return count;
        }

        public static void RegisterFabricator(Fabricator fabricator, PowerNode node)
        {
            if (fabricator == null || node == null)
                return;

            for (int i = 0; i < s_FabricatorEndpointCount; i++)
            {
                if (ReferenceEquals(s_FabricatorEndpoints[i].Fabricator, fabricator))
                    return;
            }

            if (s_FabricatorEndpointCount >= FabricatorEndpointCapacity)
                return;

            s_FabricatorEndpoints[s_FabricatorEndpointCount++] = new FabricatorEndpoint
            {
                Fabricator = fabricator,
                Node = node
            };
        }

        public static void UnregisterFabricator(Fabricator fabricator)
        {
            for (int i = s_FabricatorEndpointCount - 1; i >= 0; i--)
            {
                if (ReferenceEquals(s_FabricatorEndpoints[i].Fabricator, fabricator))
                    RemoveFabricatorEndpointAt(i);
            }
        }

        public static void RegisterRecycler(ResourceRecyclerModule recycler, PowerNode node)
        {
            if (recycler == null || node == null)
                return;

            for (int i = 0; i < s_RecyclerEndpointCount; i++)
            {
                if (ReferenceEquals(s_RecyclerEndpoints[i].Recycler, recycler))
                    return;
            }

            if (s_RecyclerEndpointCount >= RecyclerEndpointCapacity)
                return;

            s_RecyclerEndpoints[s_RecyclerEndpointCount++] = new RecyclerEndpoint
            {
                Recycler = recycler,
                Node = node
            };
        }

        public static void UnregisterRecycler(ResourceRecyclerModule recycler)
        {
            for (int i = s_RecyclerEndpointCount - 1; i >= 0; i--)
            {
                if (ReferenceEquals(s_RecyclerEndpoints[i].Recycler, recycler))
                    RemoveRecyclerEndpointAt(i);
            }
        }

        private static void RemoveStorageEndpointAt(int index)
        {
            int lastIndex = s_StorageEndpointCount - 1;
            if ((uint)index > (uint)lastIndex)
                return;

            for (int i = index; i < lastIndex; i++)
                s_StorageEndpoints[i] = s_StorageEndpoints[i + 1];

            s_StorageEndpoints[lastIndex] = default;
            s_StorageEndpointCount = lastIndex;
        }

        private static void RemoveFabricatorEndpointAt(int index)
        {
            int lastIndex = s_FabricatorEndpointCount - 1;
            if ((uint)index > (uint)lastIndex)
                return;

            for (int i = index; i < lastIndex; i++)
                s_FabricatorEndpoints[i] = s_FabricatorEndpoints[i + 1];

            s_FabricatorEndpoints[lastIndex] = default;
            s_FabricatorEndpointCount = lastIndex;
        }

        private static void RemoveRecyclerEndpointAt(int index)
        {
            int lastIndex = s_RecyclerEndpointCount - 1;
            if ((uint)index > (uint)lastIndex)
                return;

            for (int i = index; i < lastIndex; i++)
                s_RecyclerEndpoints[i] = s_RecyclerEndpoints[i + 1];

            s_RecyclerEndpoints[lastIndex] = default;
            s_RecyclerEndpointCount = lastIndex;
        }

        public static int CountAccessibleItem(PowerGrid grid, ItemData item)
        {
            if (grid == null || item == null)
                return 0;

            int count = 0;
            for (int i = 0; i < s_StorageEndpointCount; i++)
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
            for (int i = 0; i < s_StorageEndpointCount; i++)
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

            for (int i = 0; i < s_StorageEndpointCount && deposited < amount; i++)
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
            for (int i = 0; i < s_StorageEndpointCount && remaining > 0; i++)
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
                if (!TryRouteNearestStorageEndpoint(sourceNode, grid, out int endpointIndex))
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

        private static bool TryRouteNearestStorageEndpoint(PowerNode sourceNode, PowerGrid grid, out int endpointIndex)
        {
            endpointIndex = -1;
            if (sourceNode == null || grid == null)
                return false;

            int nodeCount = grid.LogisticsNodeCount;
            if (nodeCount <= 0)
                return TryResolveFirstStorageEndpoint(grid, out endpointIndex);

            if (!grid.TryResolveLogisticsNodeIndex(sourceNode, out int startNodeIndex))
                return TryResolveFirstStorageEndpoint(grid, out endpointIndex);

            NativeArray<int>.ReadOnly graphEdgeOffsets = grid.GetLogisticsEdgeOffsetsReadOnly();
            NativeArray<int>.ReadOnly graphEdgeDestinations = grid.GetLogisticsEdgeDestinationsReadOnly();
            if (!graphEdgeOffsets.IsCreated ||
                !graphEdgeDestinations.IsCreated ||
                graphEdgeOffsets.Length <= nodeCount)
            {
                return TryResolveFirstStorageEndpoint(grid, out endpointIndex);
            }

            int edgeCount = math.min(grid.LogisticsEdgeCount, graphEdgeDestinations.Length);
            int terminalEdgeOffset = graphEdgeOffsets[nodeCount];
            if (terminalEdgeOffset < 0 || terminalEdgeOffset > edgeCount)
                return TryResolveFirstStorageEndpoint(grid, out endpointIndex);

            edgeCount = terminalEdgeOffset;
            IDataVault vault = s_DataVault;
            if (!LogisticsRouteScratchMemory.TryAcquireWriteBuffers(
                    vault,
                    nodeCount,
                    edgeCount,
                    out NativeArray<int> edgeOffsets,
                    out NativeArray<int> edgeDestinations,
                    out NativeArray<int> edgeWriteCursor,
                    out NativeArray<byte> storageCapacityByNode,
                    out NativeArray<byte> visited,
                    out NativeArray<int> queue,
                    out NativeArray<int> resultNodeIndex))
            {
                return TryResolveFirstStorageEndpoint(grid, out endpointIndex);
            }

            int targetNodeIndex;
            try
            {
                BuildRouteStorageCapacityFlags(grid, nodeCount, storageCapacityByNode);
                CopyRouteCsr(graphEdgeOffsets, graphEdgeDestinations, nodeCount, edgeCount, edgeOffsets, edgeDestinations, edgeWriteCursor);

                resultNodeIndex[0] = -1;
                LogisticsPipeRoutingKernel.ExecuteRouteBfs(
                    nodeCount,
                    startNodeIndex,
                    edgeOffsets,
                    edgeDestinations,
                    storageCapacityByNode,
                    visited,
                    queue,
                    resultNodeIndex);

                targetNodeIndex = resultNodeIndex[0];
            }
            finally
            {
                LogisticsRouteScratchMemory.ReleaseWriteLocks(vault);
            }

            if (targetNodeIndex < 0 || targetNodeIndex >= nodeCount)
                return false;

            return TryResolveStorageEndpointByNodeIndex(grid, targetNodeIndex, out endpointIndex);
        }

        private static bool TryResolveFirstStorageEndpoint(PowerGrid grid, out int endpointIndex)
        {
            endpointIndex = -1;
            if (grid == null)
                return false;

            for (int i = 0; i < s_StorageEndpointCount; i++)
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

        private static void BuildRouteStorageCapacityFlags(
            PowerGrid grid,
            int nodeCount,
            NativeArray<byte> storageCapacityByNode)
        {
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                storageCapacityByNode[nodeIndex] = 0;

            for (int endpointIndex = 0; endpointIndex < s_StorageEndpointCount; endpointIndex++)
            {
                StorageEndpoint endpoint = s_StorageEndpoints[endpointIndex];
                if (endpoint.Crate == null ||
                    endpoint.Node == null ||
                    endpoint.Node.Grid != grid ||
                    !endpoint.Crate.HasAutomatedCapacity())
                {
                    continue;
                }

                if (grid.TryResolveLogisticsNodeIndex(endpoint.Node, out int nodeIndex) &&
                    nodeIndex >= 0 &&
                    nodeIndex < nodeCount)
                {
                    storageCapacityByNode[nodeIndex] = 1;
                }
            }
        }

        private static void CopyRouteCsr(
            NativeArray<int>.ReadOnly sourceEdgeOffsets,
            NativeArray<int>.ReadOnly sourceEdgeDestinations,
            int nodeCount,
            int edgeCount,
            NativeArray<int> edgeOffsets,
            NativeArray<int> edgeDestinations,
            NativeArray<int> edgeWriteCursor)
        {
            for (int nodeIndex = 0; nodeIndex <= nodeCount; nodeIndex++)
                edgeOffsets[nodeIndex] = sourceEdgeOffsets[nodeIndex];

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                edgeWriteCursor[nodeIndex] = edgeOffsets[nodeIndex];

            for (int edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
                edgeDestinations[edgeIndex] = sourceEdgeDestinations[edgeIndex];
        }

        private static bool TryResolveStorageEndpointByNodeIndex(PowerGrid grid, int targetNodeIndex, out int endpointIndex)
        {
            endpointIndex = -1;
            if (grid == null || targetNodeIndex < 0)
                return false;

            for (int i = 0; i < s_StorageEndpointCount; i++)
            {
                StorageEndpoint endpoint = s_StorageEndpoints[i];
                if (endpoint.Crate == null ||
                    endpoint.Node == null ||
                    endpoint.Node.Grid != grid ||
                    !endpoint.Crate.HasAutomatedCapacity())
                {
                    continue;
                }

                if (grid.TryResolveLogisticsNodeIndex(endpoint.Node, out int endpointNodeIndex) &&
                    endpointNodeIndex == targetNodeIndex)
                {
                    endpointIndex = i;
                    return true;
                }
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
            for (int i = 0; i < s_StorageEndpointCount; i++)
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

            for (int i = 0; i < s_FabricatorEndpointCount; i++)
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
                if (token <= 0 ||
                    !ThreadSafeCommandQueue.TryEnqueue(EntityCommand.CreateCommitStorageReservation(token, reservationId, requesterId)))
                {
                    crate.CommitReservation(reservationId);
                }
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

            for (int i = 0; i < s_StorageEndpointCount && remaining > 0; i++)
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

            for (int i = 0; i < s_StorageEndpointCount && remaining > 0; i++)
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

        public static void ExecuteLogisticsRouteBfs(
            int nodeCount,
            int startNodeIndex,
            NativeArray<int> edgeOffsets,
            NativeArray<int> edgeDestinations,
            NativeArray<byte> storageCapacityByNode,
            NativeArray<byte> visited,
            NativeArray<int> queue,
            NativeArray<int> resultNodeIndex)
        {
            LogisticsPipeRoutingKernel.ExecuteRouteBfs(
                nodeCount,
                startNodeIndex,
                edgeOffsets,
                edgeDestinations,
                storageCapacityByNode,
                visited,
                queue,
                resultNodeIndex);
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
