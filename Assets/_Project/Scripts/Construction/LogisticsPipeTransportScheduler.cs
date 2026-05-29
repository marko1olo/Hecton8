using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Shared SlowTick owner for logistics pipes.
    /// Computes a directed crate-to-crate DAG and replays pipe steps in topological order.
    /// </summary>
    internal static class LogisticsPipeTransportScheduler
    {
        private const int MaxNodeCapacity = 128;
        private const int CycleWarningCadenceFrames = 300;
        private const SystemID OwnerSystemId = SystemID.Construction;
        private const BufferID EdgeOffsetsBufferId = (BufferID)72054;
        private const BufferID EdgeDestinationsBufferId = (BufferID)72055;
        private const BufferID InputIndegreesBufferId = (BufferID)72056;
        private const BufferID WorkIndegreesBufferId = (BufferID)72057;
        private const BufferID QueueBufferId = (BufferID)72058;
        private const BufferID SortedOrderBufferId = (BufferID)72059;
        private const BufferID SortedCountBufferId = (BufferID)72060;
        private const ulong SortBuffersMutationGuardMask = 0x000000001FC00000UL;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const string CycleRepairWarningMessage = "LogisticsPipeTransportScheduler dropped cyclic edge to keep pipe DAG valid.";
#endif
        // COLD ALLOC: fixed pipe-node registry; hot scheduler never grows or shifts a managed collection.
        private static readonly LogisticsPipeNode[] _activeNodes = new LogisticsPipeNode[MaxNodeCapacity];
        private static int _activeNodeCount;
        private static IDataVault _dataVault;
        private static VaultGenerationHandle<int> _edgeOffsetsHandle;
        private static VaultGenerationHandle<int> _edgeDestinationsHandle;

        private static VaultGenerationHandle<int> _inputIndegreesHandle;
        private static VaultGenerationHandle<int> _workIndegreesHandle;
        private static VaultGenerationHandle<int> _queueHandle;
        private static VaultGenerationHandle<int> _sortedOrderHandle;
        private static VaultGenerationHandle<int> _sortedCountHandle;
        private static bool _sortGuardHeld;

        private static JobHandle _pendingSortHandle;
        private static bool _pendingSort;
        private static int _scheduledSortedCount;
        private static int _scheduledNodeCount;
        private static int _activeTopologyVersion;
        private static int _sortTopologyVersion = -1;
        private static int _scheduledTopologyVersion = -1;
        private static int _sortTopologySignature = -1;
        private static int _scheduledTopologySignature = -1;
        private static int _lastProcessedFrame = -1;
        private static int _nextCycleWarningFrame;

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BuildPipeTopologicalOrderJob : IJob
        {
            public int NodeCount;

            [ReadOnly] public NativeArray<int> EdgeOffsets;
            [ReadOnly] public NativeArray<int> EdgeDestinations;
            [ReadOnly] public NativeArray<int> InputIndegrees;

            public NativeArray<int> WorkIndegrees;
            public NativeArray<int> Queue;
            public NativeArray<int> SortedOrder;
            public NativeArray<int> SortedCount;

            public void Execute()
            {
                int nodeCount = NodeCount;
                int queueCount = 0;

                for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                {
                    int indegree = InputIndegrees[nodeIndex];
                    WorkIndegrees[nodeIndex] = indegree;
                    if (indegree == 0)
                        Queue[queueCount++] = nodeIndex;
                }

                int queueReadIndex = 0;
                int sortedCount = 0;
                while (queueReadIndex < queueCount)
                {
                    int nodeIndex = Queue[queueReadIndex++];
                    SortedOrder[sortedCount++] = nodeIndex;

                    int edgeStart = EdgeOffsets[nodeIndex];
                    int edgeEnd = EdgeOffsets[nodeIndex + 1];
                    for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                    {
                        int destinationIndex = EdgeDestinations[edgeIndex];
                        int nextIndegree = WorkIndegrees[destinationIndex] - 1;
                        WorkIndegrees[destinationIndex] = nextIndegree;
                        if (nextIndegree == 0)
                            Queue[queueCount++] = destinationIndex;
                    }
                }

                SortedCount[0] = sortedCount;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Shutdown();
        }

        internal static void Shutdown()
        {
            JobHandle teardownDependency = CancelPendingSortForTeardown();
            JobHandle.ScheduleBatchedJobs();
            DispatcherJobSwap.TryComplete(ref teardownDependency, forceComplete: true);
            IDataVault vault = _dataVault;
            ReleaseSortWriteLocks(vault);
            ReleaseVaultHandles(vault);
            System.Array.Clear(_activeNodes, 0, _activeNodeCount);
            _activeNodeCount = 0;
            _scheduledSortedCount = 0;
            _scheduledNodeCount = 0;
            _activeTopologyVersion = 0;
            _sortTopologyVersion = -1;
            _scheduledTopologyVersion = -1;
            _sortTopologySignature = -1;
            _scheduledTopologySignature = -1;
            _lastProcessedFrame = -1;
            _nextCycleWarningFrame = 0;
        }

        internal static void BindDataVault(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            JobHandle teardownDependency = CancelPendingSortForTeardown();
            JobHandle.ScheduleBatchedJobs();
            DispatcherJobSwap.TryComplete(ref teardownDependency, forceComplete: true);
            ReleaseSortWriteLocks(_dataVault);
            ReleaseVaultHandles(_dataVault);
            _dataVault = vault;
        }

        internal static void Register(LogisticsPipeNode node)
        {
            if (node == null)
                return;

            int activeCount = _activeNodeCount;
            for (int i = 0; i < activeCount; i++)
            {
                if (ReferenceEquals(_activeNodes[i], node))
                    return;
            }

            if (_activeNodeCount >= MaxNodeCapacity)
                return;

            _activeNodes[_activeNodeCount++] = node;
            _activeTopologyVersion++;
        }

        internal static void Unregister(LogisticsPipeNode node)
        {
            if (node == null)
                return;

            int activeCount = _activeNodeCount;
            for (int i = 0; i < activeCount; i++)
            {
                if (!ReferenceEquals(_activeNodes[i], node))
                    continue;

                RemoveActiveNodeAt(i);
                break;
            }
        }

        internal static bool TryRunSlowTick(LogisticsPipeNode requester)
        {
            if (requester == null)
                return false;

            if (_activeNodeCount <= 0)
                return false;

            int currentFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastProcessedFrame == currentFrame)
                return false;

            _lastProcessedFrame = currentFrame;

            int activeCount = CompactActiveNodes();
            if (activeCount <= 0)
                return true;

            int topologySignature = RefreshActiveNodesForScheduler(activeCount);
            CompletePendingSort(forceComplete: false);
            ReplayCurrentOrder(activeCount, topologySignature);
            if (!_pendingSort)
                ScheduleNextOrder(activeCount, topologySignature);
            return true;
        }

        private static int CompactActiveNodes()
        {
            for (int i = _activeNodeCount - 1; i >= 0; i--)
            {
                if (_activeNodes[i] != null)
                    continue;

                RemoveActiveNodeAt(i);
            }

            return _activeNodeCount;
        }

        private static void RemoveActiveNodeAt(int index)
        {
            int lastIndex = _activeNodeCount - 1;
            if ((uint)index > (uint)lastIndex)
                return;

            _activeNodes[index] = _activeNodes[lastIndex];
            _activeNodes[lastIndex] = null;
            _activeNodeCount = lastIndex;
            _activeTopologyVersion++;
        }

        private static int RefreshActiveNodesForScheduler(int activeCount)
        {
            unchecked
            {
                int signature = 17;
                for (int nodeIndex = 0; nodeIndex < activeCount; nodeIndex++)
                {
                    LogisticsPipeNode node = _activeNodes[nodeIndex];
                    node.SchedulerRefresh();
                    signature = (signature * 397) ^ node.SchedulerTopologyKey;
                }

                return signature;
            }
        }

        private static void ReplayCurrentOrder(int activeCount, int topologySignature)
        {
            if (_scheduledSortedCount == activeCount &&
                _scheduledNodeCount == activeCount &&
                _scheduledTopologyVersion == _activeTopologyVersion &&
                _scheduledTopologySignature == topologySignature &&
                TryReadBuffer(CacheDataVault(), in _sortedOrderHandle, SortedOrderBufferId, activeCount, out NativeArray<int>.ReadOnly sortedOrder))
            {
                for (int sortedIndex = 0; sortedIndex < _scheduledSortedCount; sortedIndex++)
                {
                    int nodeIndex = sortedOrder[sortedIndex];
                    if (nodeIndex < 0 || nodeIndex >= activeCount)
                        continue;

                    _activeNodes[nodeIndex].ExecuteCoordinatedSlowTick();
                }

                return;
            }

            for (int nodeIndex = 0; nodeIndex < activeCount; nodeIndex++)
                _activeNodes[nodeIndex].ExecuteCoordinatedSlowTick();
        }

        private static void ScheduleNextOrder(int activeCount, int topologySignature)
        {
            int edgeCount = CountEdges(activeCount);
            IDataVault vault = CacheDataVault();
            if (!TryAcquireSortWriteBuffers(
                    vault,
                    activeCount,
                    edgeCount,
                    out NativeArray<int> edgeOffsets,
                    out NativeArray<int> edgeDestinations,
                    out NativeArray<int> inputIndegrees,
                    out NativeArray<int> workIndegrees,
                    out NativeArray<int> queue,
                    out NativeArray<int> sortedOrder,
                    out NativeArray<int> sortedCount))
            {
                _scheduledSortedCount = 0;
                _scheduledNodeCount = 0;
                _scheduledTopologyVersion = -1;
                _scheduledTopologySignature = -1;
                return;
            }

            bool scheduled = false;
            try
            {
                for (int nodeIndex = 0; nodeIndex <= activeCount; nodeIndex++)
                    edgeOffsets[nodeIndex] = 0;

                for (int nodeIndex = 0; nodeIndex < activeCount; nodeIndex++)
                    inputIndegrees[nodeIndex] = 0;

                sortedCount[0] = 0;

                for (int sourceIndex = 0; sourceIndex < activeCount; sourceIndex++)
                {
                    if (!_activeNodes[sourceIndex].ParticipatesInSchedulerDag)
                        continue;

                    StorageCrate destinationCrate = _activeNodes[sourceIndex].DestinationCrate;
                    if (destinationCrate == null)
                        continue;

                    int outDegree = 0;
                    for (int destinationIndex = 0; destinationIndex < activeCount; destinationIndex++)
                    {
                        if (sourceIndex == destinationIndex)
                            continue;

                        if (!_activeNodes[destinationIndex].ParticipatesInSchedulerDag)
                            continue;

                        if (!ReferenceEquals(destinationCrate, _activeNodes[destinationIndex].SourceCrate))
                            continue;

                        outDegree++;
                        inputIndegrees[destinationIndex] = inputIndegrees[destinationIndex] + 1;
                    }

                    edgeOffsets[sourceIndex + 1] = outDegree;
                }

                for (int nodeIndex = 1; nodeIndex <= activeCount; nodeIndex++)
                    edgeOffsets[nodeIndex] = edgeOffsets[nodeIndex] + edgeOffsets[nodeIndex - 1];

                for (int nodeIndex = 0; nodeIndex < activeCount; nodeIndex++)
                    workIndegrees[nodeIndex] = edgeOffsets[nodeIndex];

                for (int sourceIndex = 0; sourceIndex < activeCount; sourceIndex++)
                {
                    if (!_activeNodes[sourceIndex].ParticipatesInSchedulerDag)
                        continue;

                    StorageCrate destinationCrate = _activeNodes[sourceIndex].DestinationCrate;
                    if (destinationCrate == null)
                        continue;

                    for (int destinationIndex = 0; destinationIndex < activeCount; destinationIndex++)
                    {
                        if (sourceIndex == destinationIndex)
                            continue;

                        if (!_activeNodes[destinationIndex].ParticipatesInSchedulerDag)
                            continue;

                        if (!ReferenceEquals(destinationCrate, _activeNodes[destinationIndex].SourceCrate))
                            continue;

                        int writeIndex = workIndegrees[sourceIndex];
                        workIndegrees[sourceIndex] = writeIndex + 1;
                        edgeDestinations[writeIndex] = destinationIndex;
                    }
                }

                BuildPipeTopologicalOrderJob job;
                job.NodeCount = activeCount;
                job.EdgeOffsets = edgeOffsets;
                job.EdgeDestinations = edgeDestinations;
                job.InputIndegrees = inputIndegrees;
                job.WorkIndegrees = workIndegrees;
                job.Queue = queue;
                job.SortedOrder = sortedOrder;
                job.SortedCount = sortedCount;

                _pendingSortHandle = job.Schedule();
                _pendingSort = true;
                _scheduledSortedCount = 0;
                _scheduledNodeCount = activeCount;
                _sortTopologyVersion = _activeTopologyVersion;
                _sortTopologySignature = topologySignature;
                scheduled = true;
            }
            finally
            {
                if (!scheduled)
                    ReleaseSortWriteLocks(vault);
            }
        }

        private static int CountEdges(int activeCount)
        {
            int edgeCount = 0;
            for (int sourceIndex = 0; sourceIndex < activeCount; sourceIndex++)
            {
                if (!_activeNodes[sourceIndex].ParticipatesInSchedulerDag)
                    continue;

                StorageCrate destinationCrate = _activeNodes[sourceIndex].DestinationCrate;
                if (destinationCrate == null)
                    continue;

                for (int destinationIndex = 0; destinationIndex < activeCount; destinationIndex++)
                {
                    if (sourceIndex == destinationIndex)
                        continue;

                    if (!_activeNodes[destinationIndex].ParticipatesInSchedulerDag)
                        continue;

                    if (ReferenceEquals(destinationCrate, _activeNodes[destinationIndex].SourceCrate))
                        edgeCount++;
                }
            }

            return edgeCount;
        }

        private static void CompletePendingSort(bool forceComplete)
        {
            if (!_pendingSort)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _pendingSortHandle, forceComplete))
                return;

            _pendingSort = false;
            IDataVault vault = CacheDataVault();
            if (!TryReadLockedSortBuffers(
                    vault,
                    _scheduledNodeCount,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    out NativeArray<int> sortedCount))
            {
                _scheduledSortedCount = 0;
                _scheduledNodeCount = 0;
                _scheduledTopologyVersion = -1;
                _sortTopologyVersion = -1;
                _scheduledTopologySignature = -1;
                _sortTopologySignature = -1;
                ReleaseSortWriteLocks(vault);
                return;
            }

            _scheduledSortedCount = sortedCount[0];
            _scheduledTopologyVersion = _sortTopologyVersion;
            _scheduledTopologySignature = _sortTopologySignature;
            _sortTopologyVersion = -1;
            _sortTopologySignature = -1;
            if (_scheduledSortedCount < _scheduledNodeCount)
            {
                LogCycleRepairWarning(-1, -1);
                sortedCount[0] = 0;
                _scheduledSortedCount = 0;
                _scheduledTopologyVersion = -1;
                _scheduledTopologySignature = -1;
            }

            ReleaseSortWriteLocks(vault);
        }

        private static JobHandle CancelPendingSortForTeardown()
        {
            if (!_pendingSort)
                return _pendingSortHandle;

            JobHandle dependency = _pendingSortHandle;
            _pendingSortHandle = default;
            _pendingSort = false;
            _scheduledSortedCount = 0;
            _scheduledNodeCount = 0;
            _sortTopologyVersion = -1;
            _scheduledTopologyVersion = -1;
            _sortTopologySignature = -1;
            _scheduledTopologySignature = -1;
            return dependency;
        }

        private static IDataVault CacheDataVault()
        {
            return _dataVault;
        }

        private static bool TryReadBuffer(
            IDataVault vault,
            in VaultGenerationHandle<int> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<int>.ReadOnly buffer)
        {
            buffer = default;
            return vault != null &&
                   IsLogisticsVaultHandle(in handle, expectedBufferId) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.Length >= math.max(1, requiredLength);
        }

        private static bool TryAcquireSortWriteBuffers(
            IDataVault vault,
            int nodeCount,
            int edgeCount,
            out NativeArray<int> edgeOffsets,
            out NativeArray<int> edgeDestinations,
            out NativeArray<int> inputIndegrees,
            out NativeArray<int> workIndegrees,
            out NativeArray<int> queue,
            out NativeArray<int> sortedOrder,
            out NativeArray<int> sortedCount)
        {
            edgeOffsets = default;
            edgeDestinations = default;
            inputIndegrees = default;
            workIndegrees = default;
            queue = default;
            sortedOrder = default;
            sortedCount = default;

            if (vault == null || !vault.TryAcquireMutationGuard(SortBuffersMutationGuardMask))
                return false;

            bool guardHeld = true;
            try
            {
                if (!TryBorrowSortBuffer(vault, EdgeOffsetsBufferId, nodeCount + 1, ref _edgeOffsetsHandle, out edgeOffsets) ||
                    !TryBorrowSortBuffer(vault, EdgeDestinationsBufferId, edgeCount, ref _edgeDestinationsHandle, out edgeDestinations) ||
                    !TryBorrowSortBuffer(vault, InputIndegreesBufferId, nodeCount, ref _inputIndegreesHandle, out inputIndegrees) ||
                    !TryBorrowSortBuffer(vault, WorkIndegreesBufferId, nodeCount, ref _workIndegreesHandle, out workIndegrees) ||
                    !TryBorrowSortBuffer(vault, QueueBufferId, nodeCount, ref _queueHandle, out queue) ||
                    !TryBorrowSortBuffer(vault, SortedOrderBufferId, nodeCount, ref _sortedOrderHandle, out sortedOrder) ||
                    !TryBorrowSortBuffer(vault, SortedCountBufferId, 1, ref _sortedCountHandle, out sortedCount))
                {
                    return false;
                }

                _sortGuardHeld = true;
                guardHeld = false;
                return true;
            }
            finally
            {
                if (guardHeld)
                    vault.ReleaseMutationGuard(SortBuffersMutationGuardMask);
            }
        }

        private static bool TryReadLockedSortBuffers(
            IDataVault vault,
            int nodeCount,
            out NativeArray<int> edgeOffsets,
            out NativeArray<int> edgeDestinations,
            out NativeArray<int> inputIndegrees,
            out NativeArray<int> workIndegrees,
            out NativeArray<int> queue,
            out NativeArray<int> sortedOrder,
            out NativeArray<int> sortedCount)
        {
            edgeOffsets = default;
            edgeDestinations = default;
            inputIndegrees = default;
            workIndegrees = default;
            queue = default;
            sortedOrder = default;
            sortedCount = default;

            return vault != null &&
                   TryReadLockedBuffer(vault, in _edgeOffsetsHandle, EdgeOffsetsBufferId, nodeCount + 1, out edgeOffsets) &&
                   TryReadLockedBuffer(vault, in _edgeDestinationsHandle, EdgeDestinationsBufferId, 0, out edgeDestinations) &&
                   TryReadLockedBuffer(vault, in _inputIndegreesHandle, InputIndegreesBufferId, nodeCount, out inputIndegrees) &&
                   TryReadLockedBuffer(vault, in _workIndegreesHandle, WorkIndegreesBufferId, nodeCount, out workIndegrees) &&
                   TryReadLockedBuffer(vault, in _queueHandle, QueueBufferId, nodeCount, out queue) &&
                   TryReadLockedBuffer(vault, in _sortedOrderHandle, SortedOrderBufferId, nodeCount, out sortedOrder) &&
                   TryReadLockedBuffer(vault, in _sortedCountHandle, SortedCountBufferId, 1, out sortedCount);
        }

        private static bool TryBorrowSortBuffer(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            ref VaultGenerationHandle<int> handle,
            out NativeArray<int> buffer)
        {
            buffer = default;
            int safeLength = math.max(1, requiredLength);
            if (vault == null)
                return false;

            if (!IsLogisticsVaultHandle(in handle, bufferId) ||
                !vault.TryReadHandle(in handle, out NativeArray<int> existing) ||
                !existing.IsCreated ||
                existing.Length < safeLength)
            {
                handle = vault.EnsureGenerationHandle<int>(bufferId, safeLength, OwnerSystemId, NativeArrayOptions.ClearMemory);
            }

            if (!IsLogisticsVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer))
            {
                return false;
            }

            if (buffer.IsCreated && buffer.Length >= safeLength)
                return true;

            buffer = default;
            return false;
        }

        private static bool TryReadLockedBuffer(
            IDataVault vault,
            in VaultGenerationHandle<int> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<int> buffer)
        {
            buffer = default;
            return IsLogisticsVaultHandle(in handle, expectedBufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= math.max(1, requiredLength);
        }

        private static void ReleaseSortWriteLocks(IDataVault vault)
        {
            if (!_sortGuardHeld)
                return;

            if (vault != null)
                vault.ReleaseMutationGuard(SortBuffersMutationGuardMask);
            _sortGuardHeld = false;
        }

        private static void ReleaseVaultHandles(IDataVault vault)
        {
            if (vault != null)
            {
                ReleaseVaultHandle(vault, ref _sortedCountHandle, SortedCountBufferId);
                ReleaseVaultHandle(vault, ref _sortedOrderHandle, SortedOrderBufferId);
                ReleaseVaultHandle(vault, ref _queueHandle, QueueBufferId);
                ReleaseVaultHandle(vault, ref _workIndegreesHandle, WorkIndegreesBufferId);
                ReleaseVaultHandle(vault, ref _inputIndegreesHandle, InputIndegreesBufferId);
                ReleaseVaultHandle(vault, ref _edgeDestinationsHandle, EdgeDestinationsBufferId);
                ReleaseVaultHandle(vault, ref _edgeOffsetsHandle, EdgeOffsetsBufferId);
            }
            else
            {
                ResetVaultHandles();
            }

            _dataVault = null;
        }

        private static void ReleaseVaultHandle(IDataVault vault, ref VaultGenerationHandle<int> handle, BufferID expectedBufferId)
        {
            if (IsLogisticsVaultHandle(in handle, expectedBufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsLogisticsVaultHandle(in VaultGenerationHandle<int> handle, BufferID expectedBufferId)
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == (uint)OwnerSystemId &&
                   handle.Generation != 0u;
        }

        private static void ResetVaultHandles()
        {
            _sortedCountHandle = default;
            _sortedOrderHandle = default;
            _queueHandle = default;
            _workIndegreesHandle = default;
            _inputIndegreesHandle = default;
            _edgeDestinationsHandle = default;
            _edgeOffsetsHandle = default;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogCycleRepairWarning(int sourceIndex, int destinationIndex)
        {
            int currentFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (currentFrame < _nextCycleWarningFrame)
                return;

            _nextCycleWarningFrame = currentFrame + CycleWarningCadenceFrames;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning(CycleRepairWarningMessage);
#endif
        }

    }
}
