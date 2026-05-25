using System.Collections.Generic;
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
        private const int InitialNodeCapacity = 32;
        private const int CycleWarningCadenceFrames = 300;
        private const SystemID OwnerSystemId = SystemID.Construction;
        private const BufferID EdgeOffsetsBufferId = (BufferID)72054;
        private const BufferID EdgeDestinationsBufferId = (BufferID)72055;
        private const BufferID InputIndegreesBufferId = (BufferID)72056;
        private const BufferID WorkIndegreesBufferId = (BufferID)72057;
        private const BufferID QueueBufferId = (BufferID)72058;
        private const BufferID SortedOrderBufferId = (BufferID)72059;
        private const BufferID SortedCountBufferId = (BufferID)72060;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const string CycleRepairWarningMessage = "LogisticsPipeTransportScheduler dropped cyclic edge to keep pipe DAG valid.";
#endif
        // COLD ALLOC: List<LogisticsPipeNode>[32] - managed pipe-node registry for shared DAG transport scheduling.
        private static readonly List<LogisticsPipeNode> _activeNodes = new List<LogisticsPipeNode>(InitialNodeCapacity);
        private static IDataVault _dataVault;
        private static VaultGenerationHandle<int> _edgeOffsetsHandle;
        private static VaultGenerationHandle<int> _edgeDestinationsHandle;

        private static VaultGenerationHandle<int> _inputIndegreesHandle;
        private static VaultGenerationHandle<int> _workIndegreesHandle;
        private static VaultGenerationHandle<int> _queueHandle;
        private static VaultGenerationHandle<int> _sortedOrderHandle;
        private static VaultGenerationHandle<int> _sortedCountHandle;
        private static bool _sortLocksHeld;

        private static JobHandle _pendingSortHandle;
        private static bool _pendingSort;
        private static int _scheduledSortedCount;
        private static int _scheduledNodeCount;
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
            IDataVault vault = CacheDataVault();
            ReleaseSortWriteLocks(vault);
            ReleaseVaultHandles(vault);
            _activeNodes.Clear();
            _scheduledSortedCount = 0;
            _scheduledNodeCount = 0;
            _lastProcessedFrame = -1;
            _nextCycleWarningFrame = 0;
        }

        internal static void Register(LogisticsPipeNode node)
        {
            if (node == null)
                return;

            int activeCount = _activeNodes.Count;
            for (int i = 0; i < activeCount; i++)
            {
                if (ReferenceEquals(_activeNodes[i], node))
                    return;
            }

            _activeNodes.Add(node);
        }

        internal static void Unregister(LogisticsPipeNode node)
        {
            if (node == null)
                return;

            int activeCount = _activeNodes.Count;
            for (int i = 0; i < activeCount; i++)
            {
                if (!ReferenceEquals(_activeNodes[i], node))
                    continue;

                _activeNodes.RemoveAt(i);
                break;
            }
        }

        internal static bool TryRunSlowTick(LogisticsPipeNode requester)
        {
            if (requester == null)
                return false;

            if (_activeNodes.Count <= 0)
                return false;

            int currentFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastProcessedFrame == currentFrame)
                return false;

            _lastProcessedFrame = currentFrame;

            int activeCount = CompactActiveNodes();
            if (activeCount <= 0)
                return true;

            CompletePendingSort(forceComplete: false);
            ReplayCurrentOrder(activeCount);
            if (!_pendingSort)
                ScheduleNextOrder(activeCount);
            return true;
        }

        private static int CompactActiveNodes()
        {
            for (int i = _activeNodes.Count - 1; i >= 0; i--)
            {
                if (_activeNodes[i] != null)
                    continue;

                _activeNodes.RemoveAt(i);
            }

            return _activeNodes.Count;
        }

        private static void ReplayCurrentOrder(int activeCount)
        {
            for (int nodeIndex = 0; nodeIndex < activeCount; nodeIndex++)
                _activeNodes[nodeIndex].SchedulerRefresh();

            if (_scheduledSortedCount == activeCount &&
                TryReadBuffer(CacheDataVault(), in _sortedOrderHandle, activeCount, out NativeArray<int>.ReadOnly sortedOrder))
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

        private static void ScheduleNextOrder(int activeCount)
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

            BuildPipeTopologicalOrderJob job = new BuildPipeTopologicalOrderJob
            {
                NodeCount = activeCount,
                EdgeOffsets = edgeOffsets,
                EdgeDestinations = edgeDestinations,
                InputIndegrees = inputIndegrees,
                WorkIndegrees = workIndegrees,
                Queue = queue,
                SortedOrder = sortedOrder,
                SortedCount = sortedCount
            };

                _pendingSortHandle = job.Schedule();
                _pendingSort = true;
                _sortLocksHeld = true;
                _scheduledSortedCount = 0;
                _scheduledNodeCount = activeCount;
                scheduled = true;
            }
            finally
            {
                if (!scheduled)
                    ReleaseSortWriteLocks(vault, true, true, true, true, true, true, true);
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
            if (!TryResolveSortBuffers(
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
                ReleaseSortWriteLocks(vault);
                return;
            }

            _scheduledSortedCount = sortedCount[0];
            if (_scheduledSortedCount < _scheduledNodeCount)
            {
                LogCycleRepairWarning(-1, -1);
                sortedCount[0] = 0;
                _scheduledSortedCount = 0;
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
            return dependency;
        }

        private static IDataVault CacheDataVault()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            return _dataVault;
        }

        private static bool TryReadBuffer(
            IDataVault vault,
            in VaultGenerationHandle<int> handle,
            int requiredLength,
            out NativeArray<int>.ReadOnly buffer)
        {
            buffer = default;
            return vault != null &&
                   handle.BufferID != 0u &&
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

            bool edgeOffsetsLocked = false;
            bool edgeDestinationsLocked = false;
            bool inputIndegreesLocked = false;
            bool workIndegreesLocked = false;
            bool queueLocked = false;
            bool sortedOrderLocked = false;
            bool sortedCountLocked = false;

            if (!TryAcquireWriteBuffer(vault, EdgeOffsetsBufferId, nodeCount + 1, ref _edgeOffsetsHandle, out edgeOffsets))
                return false;
            edgeOffsetsLocked = true;

            if (!TryAcquireWriteBuffer(vault, EdgeDestinationsBufferId, edgeCount, ref _edgeDestinationsHandle, out edgeDestinations))
            {
                ReleaseSortWriteLocks(vault, edgeOffsetsLocked, edgeDestinationsLocked, inputIndegreesLocked, workIndegreesLocked, queueLocked, sortedOrderLocked, sortedCountLocked);
                return false;
            }
            edgeDestinationsLocked = true;

            if (!TryAcquireWriteBuffer(vault, InputIndegreesBufferId, nodeCount, ref _inputIndegreesHandle, out inputIndegrees))
            {
                ReleaseSortWriteLocks(vault, edgeOffsetsLocked, edgeDestinationsLocked, inputIndegreesLocked, workIndegreesLocked, queueLocked, sortedOrderLocked, sortedCountLocked);
                return false;
            }
            inputIndegreesLocked = true;

            if (!TryAcquireWriteBuffer(vault, WorkIndegreesBufferId, nodeCount, ref _workIndegreesHandle, out workIndegrees))
            {
                ReleaseSortWriteLocks(vault, edgeOffsetsLocked, edgeDestinationsLocked, inputIndegreesLocked, workIndegreesLocked, queueLocked, sortedOrderLocked, sortedCountLocked);
                return false;
            }
            workIndegreesLocked = true;

            if (!TryAcquireWriteBuffer(vault, QueueBufferId, nodeCount, ref _queueHandle, out queue))
            {
                ReleaseSortWriteLocks(vault, edgeOffsetsLocked, edgeDestinationsLocked, inputIndegreesLocked, workIndegreesLocked, queueLocked, sortedOrderLocked, sortedCountLocked);
                return false;
            }
            queueLocked = true;

            if (!TryAcquireWriteBuffer(vault, SortedOrderBufferId, nodeCount, ref _sortedOrderHandle, out sortedOrder))
            {
                ReleaseSortWriteLocks(vault, edgeOffsetsLocked, edgeDestinationsLocked, inputIndegreesLocked, workIndegreesLocked, queueLocked, sortedOrderLocked, sortedCountLocked);
                return false;
            }
            sortedOrderLocked = true;

            if (!TryAcquireWriteBuffer(vault, SortedCountBufferId, 1, ref _sortedCountHandle, out sortedCount))
            {
                ReleaseSortWriteLocks(vault, edgeOffsetsLocked, edgeDestinationsLocked, inputIndegreesLocked, workIndegreesLocked, queueLocked, sortedOrderLocked, sortedCountLocked);
                return false;
            }

            return true;
        }

        private static bool TryResolveSortBuffers(
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
                   TryResolveBuffer(vault, in _edgeOffsetsHandle, nodeCount + 1, out edgeOffsets) &&
                   TryResolveBuffer(vault, in _edgeDestinationsHandle, 0, out edgeDestinations) &&
                   TryResolveBuffer(vault, in _inputIndegreesHandle, nodeCount, out inputIndegrees) &&
                   TryResolveBuffer(vault, in _workIndegreesHandle, nodeCount, out workIndegrees) &&
                   TryResolveBuffer(vault, in _queueHandle, nodeCount, out queue) &&
                   TryResolveBuffer(vault, in _sortedOrderHandle, nodeCount, out sortedOrder) &&
                   TryResolveBuffer(vault, in _sortedCountHandle, 1, out sortedCount);
        }

        private static bool TryAcquireWriteBuffer(
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

            if (handle.BufferID == 0u ||
                !vault.TryResolveHandle(in handle, out NativeArray<int> existing) ||
                !existing.IsCreated ||
                existing.Length < safeLength)
            {
                handle = vault.EnsureGenerationHandle<int>(bufferId, safeLength, OwnerSystemId, NativeArrayOptions.ClearMemory);
            }

            if (handle.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in handle, OwnerSystemId, out buffer))
            {
                return false;
            }

            if (buffer.IsCreated && buffer.Length >= safeLength)
                return true;

            vault.ReleaseWriteLock(in handle, OwnerSystemId);
            buffer = default;
            return false;
        }

        private static bool TryResolveBuffer(
            IDataVault vault,
            in VaultGenerationHandle<int> handle,
            int requiredLength,
            out NativeArray<int> buffer)
        {
            buffer = default;
            return handle.BufferID != 0u &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= math.max(1, requiredLength);
        }

        private static void ReleaseSortWriteLocks(IDataVault vault)
        {
            if (!_sortLocksHeld)
                return;

            ReleaseSortWriteLocks(vault, true, true, true, true, true, true, true);
            _sortLocksHeld = false;
        }

        private static void ReleaseSortWriteLocks(
            IDataVault vault,
            bool edgeOffsetsLocked,
            bool edgeDestinationsLocked,
            bool inputIndegreesLocked,
            bool workIndegreesLocked,
            bool queueLocked,
            bool sortedOrderLocked,
            bool sortedCountLocked)
        {
            if (vault == null)
                return;

            if (sortedCountLocked)
                vault.ReleaseWriteLock(in _sortedCountHandle, OwnerSystemId);
            if (sortedOrderLocked)
                vault.ReleaseWriteLock(in _sortedOrderHandle, OwnerSystemId);
            if (queueLocked)
                vault.ReleaseWriteLock(in _queueHandle, OwnerSystemId);
            if (workIndegreesLocked)
                vault.ReleaseWriteLock(in _workIndegreesHandle, OwnerSystemId);
            if (inputIndegreesLocked)
                vault.ReleaseWriteLock(in _inputIndegreesHandle, OwnerSystemId);
            if (edgeDestinationsLocked)
                vault.ReleaseWriteLock(in _edgeDestinationsHandle, OwnerSystemId);
            if (edgeOffsetsLocked)
                vault.ReleaseWriteLock(in _edgeOffsetsHandle, OwnerSystemId);
        }

        private static void ReleaseVaultHandles(IDataVault vault)
        {
            if (vault != null)
            {
                ReleaseVaultHandle(vault, ref _sortedCountHandle);
                ReleaseVaultHandle(vault, ref _sortedOrderHandle);
                ReleaseVaultHandle(vault, ref _queueHandle);
                ReleaseVaultHandle(vault, ref _workIndegreesHandle);
                ReleaseVaultHandle(vault, ref _inputIndegreesHandle);
                ReleaseVaultHandle(vault, ref _edgeDestinationsHandle);
                ReleaseVaultHandle(vault, ref _edgeOffsetsHandle);
            }

            _dataVault = null;
        }

        private static void ReleaseVaultHandle(IDataVault vault, ref VaultGenerationHandle<int> handle)
        {
            if (handle.BufferID == 0u)
                return;

            vault.ReleaseBuffer(in handle);
            handle = default;
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
