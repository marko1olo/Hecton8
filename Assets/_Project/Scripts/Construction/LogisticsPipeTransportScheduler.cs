using System.Collections.Generic;
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
        private const float CycleWarningCadenceSeconds = 5f;
        // COLD ALLOC: List<LogisticsPipeNode>[32] — active pipe-node registry for shared DAG transport scheduling — owner: LogisticsPipeTransportScheduler
        private static readonly List<LogisticsPipeNode> _activeNodes = new List<LogisticsPipeNode>(InitialNodeCapacity);
        // COLD ALLOC: NativeArray<int>[capacity] — visited-mark scratch for ordered fallback replay without managed heap churn — owner: LogisticsPipeTransportScheduler
        private static NativeArray<int> _visitMarks;
        // COLD ALLOC: NativeArray<int>[capacity] — cycle-repair suppressed edge source scratch for deterministic Kahn recovery — owner: LogisticsPipeTransportScheduler
        private static NativeArray<int> _suppressedEdgeSources;
        // COLD ALLOC: NativeArray<int>[capacity] — cycle-repair suppressed edge destination scratch for deterministic Kahn recovery — owner: LogisticsPipeTransportScheduler
        private static NativeArray<int> _suppressedEdgeDestinations;

        private static NativeArray<int> _edgeOffsets;
        private static NativeArray<int> _edgeDestinations;
        private static NativeArray<int> _inputIndegrees;
        private static NativeArray<int> _workIndegrees;
        private static NativeArray<int> _queue;
        private static NativeArray<int> _sortedOrder;
        private static NativeArray<int> _sortedCount;

        private static JobHandle _pendingSortHandle;
        private static bool _pendingSort;
        private static int _scheduledSortedCount;
        private static int _scheduledNodeCount;
        private static int _lastProcessedFrame = -1;
        private static int _visitStamp = 1;
        private static float _nextCycleWarningTime;

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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
            EnsureVisitCapacity(InitialNodeCapacity);
            EnsureSuppressionCapacity(InitialNodeCapacity);
        }

        internal static void Shutdown()
        {
            JobHandle teardownDependency = CancelPendingSortForTeardown();
            teardownDependency = DisposeNativeArray(ref _edgeOffsets, teardownDependency);
            teardownDependency = DisposeNativeArray(ref _edgeDestinations, teardownDependency);
            teardownDependency = DisposeNativeArray(ref _inputIndegrees, teardownDependency);
            teardownDependency = DisposeNativeArray(ref _workIndegrees, teardownDependency);
            teardownDependency = DisposeNativeArray(ref _queue, teardownDependency);
            teardownDependency = DisposeNativeArray(ref _sortedOrder, teardownDependency);
            teardownDependency = DisposeNativeArray(ref _sortedCount, teardownDependency);
            teardownDependency = DisposeNativeArray(ref _visitMarks, teardownDependency);
            teardownDependency = DisposeNativeArray(ref _suppressedEdgeSources, teardownDependency);
            teardownDependency = DisposeNativeArray(ref _suppressedEdgeDestinations, teardownDependency);
            JobHandle.ScheduleBatchedJobs();
            DispatcherJobSwap.TryComplete(ref teardownDependency, forceComplete: true);
            _activeNodes.Clear();
            _scheduledSortedCount = 0;
            _scheduledNodeCount = 0;
            _lastProcessedFrame = -1;
            _visitStamp = 1;
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

            int currentFrame = Time.frameCount;
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
            EnsureVisitCapacity(activeCount);
            _visitStamp++;
            if (_visitStamp == int.MaxValue)
            {
                ClearVisitMarks();
                _visitStamp = 1;
            }

            for (int nodeIndex = 0; nodeIndex < activeCount; nodeIndex++)
                _activeNodes[nodeIndex].SchedulerRefresh();

            if (_scheduledSortedCount == activeCount && _sortedOrder.IsCreated)
            {
                for (int sortedIndex = 0; sortedIndex < _scheduledSortedCount; sortedIndex++)
                {
                    int nodeIndex = _sortedOrder[sortedIndex];
                    if (nodeIndex < 0 || nodeIndex >= activeCount)
                        continue;

                    _visitMarks[nodeIndex] = _visitStamp;
                    _activeNodes[nodeIndex].ExecuteCoordinatedSlowTick();
                }
            }

            for (int nodeIndex = 0; nodeIndex < activeCount; nodeIndex++)
            {
                if (_visitMarks[nodeIndex] == _visitStamp)
                    continue;

                _activeNodes[nodeIndex].ExecuteCoordinatedSlowTick();
            }
        }

        private static void ScheduleNextOrder(int activeCount)
        {
            int edgeCount = CountEdges(activeCount);
            EnsureNativeCapacity(activeCount, edgeCount);

            for (int nodeIndex = 0; nodeIndex <= activeCount; nodeIndex++)
                _edgeOffsets[nodeIndex] = 0;

            for (int nodeIndex = 0; nodeIndex < activeCount; nodeIndex++)
                _inputIndegrees[nodeIndex] = 0;

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
                    _inputIndegrees[destinationIndex] = _inputIndegrees[destinationIndex] + 1;
                }

                _edgeOffsets[sourceIndex + 1] = outDegree;
            }

            for (int nodeIndex = 1; nodeIndex <= activeCount; nodeIndex++)
                _edgeOffsets[nodeIndex] = _edgeOffsets[nodeIndex] + _edgeOffsets[nodeIndex - 1];

            for (int nodeIndex = 0; nodeIndex < activeCount; nodeIndex++)
                _workIndegrees[nodeIndex] = _edgeOffsets[nodeIndex];

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

                    int writeIndex = _workIndegrees[sourceIndex];
                    _workIndegrees[sourceIndex] = writeIndex + 1;
                    _edgeDestinations[writeIndex] = destinationIndex;
                }
            }

            BuildPipeTopologicalOrderJob job = new BuildPipeTopologicalOrderJob
            {
                NodeCount = activeCount,
                EdgeOffsets = _edgeOffsets,
                EdgeDestinations = _edgeDestinations,
                InputIndegrees = _inputIndegrees,
                WorkIndegrees = _workIndegrees,
                Queue = _queue,
                SortedOrder = _sortedOrder,
                SortedCount = _sortedCount
            };

            _pendingSortHandle = job.Schedule();
            _pendingSort = true;
            _scheduledSortedCount = 0;
            _scheduledNodeCount = activeCount;
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
            _scheduledSortedCount = _sortedCount.IsCreated ? _sortedCount[0] : 0;
            if (_scheduledSortedCount < _scheduledNodeCount)
                RepairCycleOrder(_scheduledNodeCount);
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

        private static void ClearVisitMarks()
        {
            if (!_visitMarks.IsCreated)
                return;

            for (int i = 0; i < _visitMarks.Length; i++)
                _visitMarks[i] = 0;
        }

        private static void EnsureVisitCapacity(int requiredCount)
        {
            if (_visitMarks.IsCreated && _visitMarks.Length >= requiredCount)
                return;

            int currentCapacity = _visitMarks.IsCreated ? _visitMarks.Length : 0;
            int nextCapacity = math.max(currentCapacity, InitialNodeCapacity);
            while (nextCapacity < requiredCount)
                nextCapacity <<= 1;

            NativeArray<int> nextVisitMarks = new NativeArray<int>(nextCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[capacity] - visited-mark scratch for ordered pipe replay - owner: LogisticsPipeTransportScheduler
            for (int i = 0; i < currentCapacity; i++)
                nextVisitMarks[i] = _visitMarks[i];

            DisposeNativeArray(ref _visitMarks);
            _visitMarks = nextVisitMarks;
        }

        private static void EnsureSuppressionCapacity(int requiredCount)
        {
            if (_suppressedEdgeSources.IsCreated &&
                _suppressedEdgeDestinations.IsCreated &&
                _suppressedEdgeSources.Length >= requiredCount &&
                _suppressedEdgeDestinations.Length >= requiredCount)
                return;

            int currentCapacity = _suppressedEdgeSources.IsCreated ? _suppressedEdgeSources.Length : 0;
            int nextCapacity = math.max(currentCapacity, InitialNodeCapacity);
            while (nextCapacity < requiredCount)
                nextCapacity <<= 1;

            NativeArray<int> nextSources = new NativeArray<int>(nextCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[capacity] - cycle-repair suppressed edge sources - owner: LogisticsPipeTransportScheduler
            NativeArray<int> nextDestinations = new NativeArray<int>(nextCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[capacity] - cycle-repair suppressed edge destinations - owner: LogisticsPipeTransportScheduler
            int destinationCapacity = _suppressedEdgeDestinations.IsCreated ? _suppressedEdgeDestinations.Length : 0;
            int copyCount = math.min(currentCapacity, destinationCapacity);
            for (int i = 0; i < copyCount; i++)
            {
                nextSources[i] = _suppressedEdgeSources[i];
                nextDestinations[i] = _suppressedEdgeDestinations[i];
            }

            DisposeNativeArray(ref _suppressedEdgeSources);
            DisposeNativeArray(ref _suppressedEdgeDestinations);
            _suppressedEdgeSources = nextSources;
            _suppressedEdgeDestinations = nextDestinations;
        }

        private static void EnsureNativeCapacity(int nodeCount, int edgeCount)
        {
            EnsureNativeArray(ref _edgeOffsets, nodeCount + 1);
            EnsureNativeArray(ref _edgeDestinations, edgeCount);
            EnsureNativeArray(ref _inputIndegrees, nodeCount);
            EnsureNativeArray(ref _workIndegrees, nodeCount);
            EnsureNativeArray(ref _queue, nodeCount);
            EnsureNativeArray(ref _sortedOrder, nodeCount);
            EnsureNativeArray(ref _sortedCount, 1);
        }

        private static void EnsureNativeArray(ref NativeArray<int> array, int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (array.IsCreated && array.Length >= safeLength)
                return;

            DisposeNativeArray(ref array);
            array = new NativeArray<int>(safeLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static void RepairCycleOrder(int activeCount)
        {
            if (activeCount <= 0 || !_sortedOrder.IsCreated || !_inputIndegrees.IsCreated || !_workIndegrees.IsCreated || !_queue.IsCreated)
                return;

            EnsureVisitCapacity(activeCount);
            EnsureSuppressionCapacity(activeCount);

            int suppressedEdgeCount = 0;
            int repairedCount = _scheduledSortedCount;
            while (repairedCount < activeCount && suppressedEdgeCount < activeCount)
            {
                StampVisitedNodes(repairedCount);
                if (!TrySelectCycleEdge(activeCount, suppressedEdgeCount, out int suppressedSourceIndex, out int suppressedDestinationIndex))
                    break;

                _suppressedEdgeSources[suppressedEdgeCount] = suppressedSourceIndex;
                _suppressedEdgeDestinations[suppressedEdgeCount] = suppressedDestinationIndex;
                suppressedEdgeCount++;
                LogCycleRepairWarning(suppressedSourceIndex, suppressedDestinationIndex);

                // COLD SYNC REPAIR: cyclic player-authored pipe loops are exceptional invalid topology.
                repairedCount = BuildSynchronousOrder(activeCount, suppressedEdgeCount);
            }

            _scheduledSortedCount = repairedCount;
            if (_sortedCount.IsCreated)
                _sortedCount[0] = repairedCount;
        }

        private static void StampVisitedNodes(int sortedCount)
        {
            _visitStamp++;
            if (_visitStamp == int.MaxValue)
            {
                ClearVisitMarks();
                _visitStamp = 1;
            }

            int safeSortedCount = math.min(sortedCount, _scheduledNodeCount);
            for (int sortedIndex = 0; sortedIndex < safeSortedCount; sortedIndex++)
            {
                int nodeIndex = _sortedOrder[sortedIndex];
                if (nodeIndex < 0 || nodeIndex >= _scheduledNodeCount)
                    continue;

                _visitMarks[nodeIndex] = _visitStamp;
            }
        }

        private static bool TrySelectCycleEdge(int activeCount, int suppressedEdgeCount, out int suppressedSourceIndex, out int suppressedDestinationIndex)
        {
            suppressedSourceIndex = -1;
            suppressedDestinationIndex = -1;

            for (int sourceIndex = activeCount - 1; sourceIndex >= 0; sourceIndex--)
            {
                if (_visitMarks[sourceIndex] == _visitStamp)
                    continue;

                int edgeStart = _edgeOffsets[sourceIndex];
                int edgeEnd = _edgeOffsets[sourceIndex + 1];
                for (int edgeIndex = edgeEnd - 1; edgeIndex >= edgeStart; edgeIndex--)
                {
                    int destinationIndex = _edgeDestinations[edgeIndex];
                    if (destinationIndex < 0 || destinationIndex >= activeCount)
                        continue;

                    if (_visitMarks[destinationIndex] == _visitStamp)
                        continue;

                    if (IsSuppressedEdge(sourceIndex, destinationIndex, suppressedEdgeCount))
                        continue;

                    suppressedSourceIndex = sourceIndex;
                    suppressedDestinationIndex = destinationIndex;
                    return true;
                }
            }

            return false;
        }

        private static int BuildSynchronousOrder(int activeCount, int suppressedEdgeCount)
        {
            int queueCount = 0;
            for (int nodeIndex = 0; nodeIndex < activeCount; nodeIndex++)
            {
                int indegree = _inputIndegrees[nodeIndex];
                for (int suppressedIndex = 0; suppressedIndex < suppressedEdgeCount; suppressedIndex++)
                {
                    if (_suppressedEdgeDestinations[suppressedIndex] == nodeIndex && indegree > 0)
                        indegree--;
                }

                _workIndegrees[nodeIndex] = indegree;
                if (indegree == 0)
                    _queue[queueCount++] = nodeIndex;
            }

            int queueReadIndex = 0;
            int sortedCount = 0;
            while (queueReadIndex < queueCount)
            {
                int nodeIndex = _queue[queueReadIndex++];
                _sortedOrder[sortedCount++] = nodeIndex;

                int edgeStart = _edgeOffsets[nodeIndex];
                int edgeEnd = _edgeOffsets[nodeIndex + 1];
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int destinationIndex = _edgeDestinations[edgeIndex];
                    if (IsSuppressedEdge(nodeIndex, destinationIndex, suppressedEdgeCount))
                        continue;

                    int nextIndegree = _workIndegrees[destinationIndex] - 1;
                    _workIndegrees[destinationIndex] = nextIndegree;
                    if (nextIndegree == 0)
                        _queue[queueCount++] = destinationIndex;
                }
            }

            return sortedCount;
        }

        private static bool IsSuppressedEdge(int sourceIndex, int destinationIndex, int suppressedEdgeCount)
        {
            for (int suppressedIndex = 0; suppressedIndex < suppressedEdgeCount; suppressedIndex++)
            {
                if (_suppressedEdgeSources[suppressedIndex] != sourceIndex)
                    continue;

                if (_suppressedEdgeDestinations[suppressedIndex] == destinationIndex)
                    return true;
            }

            return false;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogCycleRepairWarning(int sourceIndex, int destinationIndex)
        {
            float currentTime = Time.unscaledTime;
            if (currentTime < _nextCycleWarningTime)
                return;

            _nextCycleWarningTime = currentTime + CycleWarningCadenceSeconds;
            Debug.LogWarning($"LogisticsPipeTransportScheduler dropped cyclic edge {sourceIndex}->{destinationIndex} to keep pipe DAG valid.");
        }

        private static void DisposeNativeArray(ref NativeArray<int> array)
        {
            if (!array.IsCreated)
                return;

            array.Dispose();
            array = default;
        }

        private static JobHandle DisposeNativeArray(ref NativeArray<int> array, JobHandle dependency)
        {
            if (!array.IsCreated)
                return dependency;

            JobHandle disposeHandle = array.Dispose(dependency);
            array = default;
            return disposeHandle;
        }
    }
}
