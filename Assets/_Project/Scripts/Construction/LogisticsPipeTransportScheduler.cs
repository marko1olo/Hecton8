using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.World;
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
        private const int MaxEdgeCapacity = MaxNodeCapacity * (MaxNodeCapacity - 1);
        private const int CycleWarningCadenceFrames = 300;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const string CycleRepairWarningMessage = "LogisticsPipeTransportScheduler found a cyclic pipe DAG; cyclic remainder will replay in stable registration order.";
#endif
        // COLD ALLOC: LogisticsPipeNode[MaxNodeCapacity] - fixed pipe-node registry - owner: LogisticsPipeTransportScheduler
        private static readonly LogisticsPipeNode[] _activeNodes = new LogisticsPipeNode[MaxNodeCapacity];
        // COLD ALLOC: int[MaxNodeCapacity + 1] - DAG edge offsets scratch - owner: LogisticsPipeTransportScheduler
        private static readonly int[] _edgeOffsets = new int[MaxNodeCapacity + 1];
        // COLD ALLOC: int[MaxEdgeCapacity] - DAG edge destinations scratch - owner: LogisticsPipeTransportScheduler
        private static readonly int[] _edgeDestinations = new int[MaxEdgeCapacity];
        // COLD ALLOC: int[MaxNodeCapacity] - DAG input indegree scratch - owner: LogisticsPipeTransportScheduler
        private static readonly int[] _inputIndegrees = new int[MaxNodeCapacity];
        // COLD ALLOC: int[MaxNodeCapacity] - DAG mutable indegree/write-cursor scratch - owner: LogisticsPipeTransportScheduler
        private static readonly int[] _workIndegrees = new int[MaxNodeCapacity];
        // COLD ALLOC: int[MaxNodeCapacity] - DAG queue scratch - owner: LogisticsPipeTransportScheduler
        private static readonly int[] _queue = new int[MaxNodeCapacity];
        // COLD ALLOC: int[MaxNodeCapacity] - latest deterministic sorted order - owner: LogisticsPipeTransportScheduler
        private static readonly int[] _sortedOrder = new int[MaxNodeCapacity];
        private static int _activeNodeCount;
        private static int _scheduledSortedCount;
        private static int _scheduledNodeCount;
        private static int _activeTopologyVersion;
        private static int _scheduledTopologyVersion = -1;
        private static int _scheduledTopologySignature = -1;
        private static int _lastProcessedFrame = -1;
        private static int _nextCycleWarningFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Shutdown();
        }

        internal static void Shutdown()
        {
            System.Array.Clear(_activeNodes, 0, _activeNodeCount);
            _activeNodeCount = 0;
            _scheduledSortedCount = 0;
            _scheduledNodeCount = 0;
            _activeTopologyVersion = 0;
            _scheduledTopologyVersion = -1;
            _scheduledTopologySignature = -1;
            _lastProcessedFrame = -1;
            _nextCycleWarningFrame = 0;
        }

        internal static void BindDataVault(IDataVault vault)
        {
            _ = vault;
            _scheduledSortedCount = 0;
            _scheduledNodeCount = 0;
            _scheduledTopologyVersion = -1;
            _scheduledTopologySignature = -1;
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

        internal static int ActiveNodeCount => _activeNodeCount;

        internal static LogisticsPipeNode GetActiveNodeAt(int index)
        {
            return index >= 0 && index < _activeNodeCount ? _activeNodes[index] : null;
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
            if (_scheduledSortedCount != activeCount ||
                _scheduledNodeCount != activeCount ||
                _scheduledTopologyVersion != _activeTopologyVersion ||
                _scheduledTopologySignature != topologySignature)
            {
                BuildCurrentOrder(activeCount, topologySignature);
            }

            ReplayCurrentOrder(activeCount, topologySignature);
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
                _scheduledTopologySignature == topologySignature)
            {
                for (int sortedIndex = 0; sortedIndex < _scheduledSortedCount; sortedIndex++)
                {
                    int nodeIndex = _sortedOrder[sortedIndex];
                    if (nodeIndex < 0 || nodeIndex >= activeCount)
                        continue;

                    _activeNodes[nodeIndex].ExecuteCoordinatedSlowTick();
                }

                return;
            }

            for (int nodeIndex = 0; nodeIndex < activeCount; nodeIndex++)
                _activeNodes[nodeIndex].ExecuteCoordinatedSlowTick();
        }

        private static void BuildCurrentOrder(int activeCount, int topologySignature)
        {
            if (activeCount <= 0 || activeCount > MaxNodeCapacity)
            {
                _scheduledSortedCount = 0;
                _scheduledNodeCount = 0;
                _scheduledTopologyVersion = -1;
                _scheduledTopologySignature = -1;
                return;
            }

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
                    if ((uint)writeIndex < (uint)_edgeDestinations.Length)
                        _edgeDestinations[writeIndex] = destinationIndex;
                }
            }

            int queueCount = 0;
            for (int nodeIndex = 0; nodeIndex < activeCount; nodeIndex++)
            {
                int indegree = _inputIndegrees[nodeIndex];
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
                    int nextIndegree = _workIndegrees[destinationIndex] - 1;
                    _workIndegrees[destinationIndex] = nextIndegree;
                    if (nextIndegree == 0)
                        _queue[queueCount++] = destinationIndex;
                }
            }

            _scheduledSortedCount = sortedCount;
            if (_scheduledSortedCount < activeCount)
            {
                LogCycleRepairWarning();
                for (int nodeIndex = 0; nodeIndex < activeCount; nodeIndex++)
                {
                    if (_workIndegrees[nodeIndex] <= 0)
                        continue;

                    _sortedOrder[_scheduledSortedCount++] = nodeIndex;
                }
            }

            _scheduledNodeCount = activeCount;
            _scheduledTopologyVersion = _activeTopologyVersion;
            _scheduledTopologySignature = topologySignature;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogCycleRepairWarning()
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
