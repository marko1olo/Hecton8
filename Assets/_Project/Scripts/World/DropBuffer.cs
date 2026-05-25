using System;
using Hecton8.Core;
using Hecton8.Scavenging;
using Unity.Collections;
using Unity.Jobs;

namespace Hecton8.World
{
    /// <summary>
    /// Persistent native drop queue owner for organic entropy yield.
    /// Single producer job chain, single consumer main-thread drain.
    /// </summary>
    internal struct DropBuffer : IDisposable
    {
        private const int DropBudgetRemainingIndex = 0;
        private const int DropBudgetDroppedIndex = 1;
        private const int DropBudgetLength = 2;

        private NativeQueue<ItemDropData> _queue;
        private NativeArray<int> _dropBudget;
        private int _capacity;

        public DropBuffer(int capacity, Allocator allocator)
        {
            _capacity = Math.Max(1, capacity);
            _queue = new NativeQueue<ItemDropData>(allocator);
            if (allocator == Allocator.Persistent)
            {
                NativeMemorySentinel.RegisterNativeQueue(
                    _queue,
                    _capacity,
                    nameof(DropBuffer),
                    nameof(_queue),
                    NativeAllocationLifetime.Session);
            }

            _dropBudget = new NativeArray<int>(DropBudgetLength, allocator, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[2] - organic drop writer budget/drop counter - owner: DropBuffer
            if (allocator == Allocator.Persistent)
                NativeMemorySentinel.RegisterNativeArray(_dropBudget, nameof(DropBuffer), nameof(_dropBudget), NativeAllocationLifetime.Session);
            ResetDropBudget();
            PrewarmQueue(ref _queue, _capacity);
        }

        /// <summary>True when the persistent native queue is available.</summary>
        public bool IsCreated => _queue.IsCreated;

        /// <summary>Configured maximum event count budget for one schedule window.</summary>
        public int Capacity => _capacity;

        /// <summary>True when no pending drop records remain in the native queue.</summary>
        public bool IsEmpty => !_queue.IsCreated || _queue.IsEmpty();

        /// <summary>Schedules the owner yield job without exporting the queue writer.</summary>
        public JobHandle ScheduleEntropyYieldJob(
            NativeArray<DestroyedOrganicEvent> events,
            NativeArray<HarvestableTemplate.RuntimeDescriptor> templateDescriptors,
            NativeArray<HarvestableTemplate.LootRuntimeEntry> lootEntries,
            NativeArray<EntropyYieldMaterialLutEntry> materialLut,
            int eventCount,
            int innerloopBatchCount)
        {
            if (!_queue.IsCreated || !_dropBudget.IsCreated || eventCount <= 0)
                return default;

            ResetDropBudget();
            return new EntropyYieldJob
            {
                Events = events,
                TemplateDescriptors = templateDescriptors,
                LootEntries = lootEntries,
                MaterialLut = materialLut,
                DropWriter = _queue.AsParallelWriter(),
                DropBudget = _dropBudget,
                EventCount = eventCount
            }.Schedule(eventCount, innerloopBatchCount);
        }

        /// <summary>Attempts to read one queued drop on the main thread.</summary>
        public bool TryDequeue(out ItemDropData drop)
        {
            if (!_queue.IsCreated)
            {
                drop = default;
                return false;
            }

            return _queue.TryDequeue(out drop);
        }

        /// <summary>Removes all queued entries. Main-thread only.</summary>
        public void Clear()
        {
            if (!_queue.IsCreated)
                return;

            while (_queue.TryDequeue(out _))
            {
            }

            ResetDropBudget();
        }

        /// <summary>Disposes the persistent native queue.</summary>
        public void Dispose()
        {
            if (_queue.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(DropBuffer), nameof(_queue));
                _queue.Dispose();
            }

            if (_dropBudget.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_dropBudget);
                _dropBudget.Dispose();
            }

            _capacity = 0;
        }

        private void ResetDropBudget()
        {
            if (!_dropBudget.IsCreated || _dropBudget.Length < DropBudgetLength)
                return;

            _dropBudget[DropBudgetRemainingIndex] = _capacity;
            _dropBudget[DropBudgetDroppedIndex] = 0;
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }
    }
}
