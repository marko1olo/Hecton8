using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Hecton8.Core
{
    public delegate void BurstCallbackDelegate(int eventId);

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct BurstCallback
    {
        private readonly FunctionPointer<BurstCallbackDelegate> _function;

        public BurstCallback(FunctionPointer<BurstCallbackDelegate> function)
        {
            _function = function;
        }

        public bool IsCreated => _function.IsCreated;

        public void Invoke(int eventId)
        {
            if (_function.IsCreated)
                _function.Invoke(eventId);
        }
    }

    /// <summary>
    /// Persistent integer event queue for Burst-to-main-thread callback routing.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct BurstCallbackQueue : IDisposable
    {
        private const string BudgetOwner = nameof(BurstCallbackQueue);

        private NativeQueue<int> _events;
        private NativeArray<int> _pendingCount;
        private int _capacity;
        private int _counterSentinelId;

        public bool IsCreated => _events.IsCreated && _pendingCount.IsCreated;
        public int Capacity => _capacity;
        public int PendingCount
        {
            get
            {
                if (!IsCreated)
                    return 0;

                int pending = _pendingCount[0];
                if (pending <= 0)
                    return 0;

                return pending < _capacity ? pending : _capacity;
            }
        }
        public ParallelEventWriter ParallelWriter => new ParallelEventWriter(_events, _pendingCount, _capacity);

        public BurstCallbackQueue(int expectedCapacity)
        {
            _capacity = expectedCapacity <= 0 ? 1 : expectedCapacity;
            _events = new NativeQueue<int>(Allocator.Persistent);
            _pendingCount = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeQueue(
                _events,
                _capacity,
                nameof(BurstCallbackQueue),
                nameof(_events),
                NativeAllocationLifetime.Session);
            _counterSentinelId = NativeMemorySentinel.RegisterNativeArray(
                _pendingCount,
                nameof(BurstCallbackQueue),
                nameof(_pendingCount),
                NativeAllocationLifetime.Session);

            int bytes = sizeof(int) * (_capacity + 1);
            MemoryBudgetTracker.Register(BudgetOwner, bytes, bytes);
            Prewarm();
        }

        public void Enqueue(int eventId)
        {
            TryEnqueue(eventId);
        }

        public bool TryEnqueue(int eventId)
        {
            if (!IsCreated)
                return false;

            int index = _pendingCount[0];
            if ((uint)index >= (uint)_capacity)
                return false;

            _events.Enqueue(eventId);
            _pendingCount[0] = index + 1;
            return true;
        }

        public bool TryDequeue(out int eventId)
        {
            if (!IsCreated)
            {
                eventId = 0;
                return false;
            }

            if (!_events.TryDequeue(out eventId))
            {
                _pendingCount[0] = 0;
                return false;
            }

            int pending = _pendingCount[0] - 1;
            _pendingCount[0] = pending > 0 ? pending : 0;
            return true;
        }

        public int Drain(BurstCallback callback, int maxEvents)
        {
            if (!IsCreated || !callback.IsCreated || maxEvents <= 0)
                return 0;

            int pending = PendingCount;
            if (pending <= 0)
                return 0;

            int limit = pending < maxEvents ? pending : maxEvents;
            int drained = 0;
            while (drained < limit && _events.TryDequeue(out int eventId))
            {
                callback.Invoke(eventId);
                drained++;
            }

            _pendingCount[0] = 0;
            return drained;
        }

        public void Clear()
        {
            if (!IsCreated)
                return;

            while (_events.TryDequeue(out _))
            {
            }

            _pendingCount[0] = 0;
        }

        public void Dispose()
        {
            if (!IsCreated)
                return;

            Clear();
            NativeMemorySentinel.UnregisterNativeQueue(nameof(BurstCallbackQueue), nameof(_events));

            if (_counterSentinelId != 0)
            {
                NativeMemorySentinel.Unregister(_counterSentinelId);
                _counterSentinelId = 0;
            }

            MemoryBudgetTracker.Unregister(BudgetOwner);
            _events.Dispose();
            _pendingCount.Dispose();
            _capacity = 0;
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            if (!IsCreated)
                return inputDeps;

            NativeMemorySentinel.UnregisterNativeQueue(nameof(BurstCallbackQueue), nameof(_events));

            if (_counterSentinelId != 0)
            {
                NativeMemorySentinel.Unregister(_counterSentinelId);
                _counterSentinelId = 0;
            }

            MemoryBudgetTracker.Unregister(BudgetOwner);
            JobHandle eventsDisposeHandle = _events.Dispose(inputDeps);
            JobHandle counterDisposeHandle = _pendingCount.Dispose(inputDeps);
            _events = default;
            _pendingCount = default;
            _capacity = 0;
            return JobHandle.CombineDependencies(eventsDisposeHandle, counterDisposeHandle);
        }

        private void Prewarm()
        {
            for (int i = 0; i < _capacity; i++)
                _events.Enqueue(0);

            for (int i = 0; i < _capacity; i++)
                _events.TryDequeue(out _);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ParallelEventWriter
        {
            private NativeQueue<int>.ParallelWriter _events;
            [NativeDisableUnsafePtrRestriction] private readonly int* _pendingCount;
            private readonly int _capacity;

            internal ParallelEventWriter(NativeQueue<int> events, NativeArray<int> pendingCount, int capacity)
            {
                _events = events.IsCreated ? events.AsParallelWriter() : default;
                _pendingCount = pendingCount.IsCreated ? (int*)NativeArrayUnsafeUtility.GetUnsafePtr(pendingCount) : null;
                _capacity = capacity;
            }

            public bool TryEnqueue(int eventId)
            {
                if (_pendingCount == null || _capacity <= 0)
                    return false;

                ref int pendingCount = ref UnsafeUtility.AsRef<int>(_pendingCount);
                int index = Interlocked.Increment(ref pendingCount) - 1;
                if ((uint)index >= (uint)_capacity)
                    return false;

                _events.Enqueue(eventId);
                return true;
            }
        }
    }
}
