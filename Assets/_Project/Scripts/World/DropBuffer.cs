using System;
using Unity.Collections;

namespace Hecton8.World
{
    /// <summary>
    /// Persistent native drop queue owner for organic entropy yield.
    /// Single producer job chain, single consumer main-thread drain.
    /// </summary>
    internal struct DropBuffer : IDisposable
    {
        private NativeQueue<ItemDropData> _queue;
        private int _capacity;

        public DropBuffer(int capacity, Allocator allocator)
        {
            _queue = new NativeQueue<ItemDropData>(allocator);
            _capacity = Math.Max(1, capacity);
        }

        /// <summary>True when the persistent native queue is available.</summary>
        public bool IsCreated => _queue.IsCreated;

        /// <summary>Configured maximum event count budget for one schedule window.</summary>
        public int Capacity => _capacity;

        /// <summary>Returns a Burst-safe writer for the current queue.</summary>
        public NativeQueue<ItemDropData>.ParallelWriter AsParallelWriter()
        {
            return _queue.AsParallelWriter();
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
        }

        /// <summary>Disposes the persistent native queue.</summary>
        public void Dispose()
        {
            if (_queue.IsCreated)
                _queue.Dispose();

            _capacity = 0;
        }
    }
}
