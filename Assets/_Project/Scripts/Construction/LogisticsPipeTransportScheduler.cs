using System.Collections.Generic;
using Hecton8.Gameplay;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
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
        // COLD ALLOC: List<LogisticsPipeNode>[32] — active pipe-node registry for shared DAG transport scheduling — owner: LogisticsPipeTransportScheduler
        private static readonly List<LogisticsPipeNode> _activeNodes = new List<LogisticsPipeNode>(InitialNodeCapacity);
        // COLD ALLOC: int[32] — visited-mark scratch for ordered fallback replay without heap churn — owner: LogisticsPipeTransportScheduler
        private static int[] _visitMarks = new int[InitialNodeCapacity];

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
        private static int _lastProcessedFrame = -1;
        private static int _visitStamp = 1;

        [BurstCompile]
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
            CompletePendingSort();
            DisposeNativeArray(ref _edgeOffsets);
            DisposeNativeArray(ref _edgeDestinations);
            DisposeNativeArray(ref _inputIndegrees);
            DisposeNativeArray(ref _workIndegrees);
            DisposeNativeArray(ref _queue);
            DisposeNativeArray(ref _sortedOrder);
            DisposeNativeArray(ref _sortedCount);
            _activeNodes.Clear();
            _scheduledSortedCount = 0;
            _lastProcessedFrame = -1;
            _visitStamp = 1;
            EnsureVisitCapacity(InitialNodeCapacity);
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

            CompletePendingSort();
            ReplayCurrentOrder(activeCount);
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
                System.Array.Clear(_visitMarks, 0, _visitMarks.Length);
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
                StorageCrate destinationCrate = _activeNodes[sourceIndex].DestinationCrate;
                if (destinationCrate == null)
                    continue;

                int outDegree = 0;
                for (int destinationIndex = 0; destinationIndex < activeCount; destinationIndex++)
                {
                    if (sourceIndex == destinationIndex)
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
                StorageCrate destinationCrate = _activeNodes[sourceIndex].DestinationCrate;
                if (destinationCrate == null)
                    continue;

                for (int destinationIndex = 0; destinationIndex < activeCount; destinationIndex++)
                {
                    if (sourceIndex == destinationIndex)
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
        }

        private static int CountEdges(int activeCount)
        {
            int edgeCount = 0;
            for (int sourceIndex = 0; sourceIndex < activeCount; sourceIndex++)
            {
                StorageCrate destinationCrate = _activeNodes[sourceIndex].DestinationCrate;
                if (destinationCrate == null)
                    continue;

                for (int destinationIndex = 0; destinationIndex < activeCount; destinationIndex++)
                {
                    if (sourceIndex == destinationIndex)
                        continue;

                    if (ReferenceEquals(destinationCrate, _activeNodes[destinationIndex].SourceCrate))
                        edgeCount++;
                }
            }

            return edgeCount;
        }

        private static void CompletePendingSort()
        {
            if (!_pendingSort)
                return;

            _pendingSortHandle.Complete();
            _pendingSortHandle = default;
            _pendingSort = false;
            _scheduledSortedCount = _sortedCount.IsCreated ? _sortedCount[0] : 0;
        }

        private static void EnsureVisitCapacity(int requiredCount)
        {
            if (_visitMarks != null && _visitMarks.Length >= requiredCount)
                return;

            int nextCapacity = _visitMarks != null ? _visitMarks.Length : InitialNodeCapacity;
            while (nextCapacity < requiredCount)
                nextCapacity <<= 1;

            _visitMarks = new int[nextCapacity];
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
            int safeLength = Mathf.Max(1, requiredLength);
            if (array.IsCreated && array.Length >= safeLength)
                return;

            DisposeNativeArray(ref array);
            array = new NativeArray<int>(safeLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static void DisposeNativeArray(ref NativeArray<int> array)
        {
            if (!array.IsCreated)
                return;

            array.Dispose();
            array = default;
        }
    }
}
