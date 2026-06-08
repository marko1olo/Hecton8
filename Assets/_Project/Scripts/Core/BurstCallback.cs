using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Core
{
    public delegate void BurstCallbackDelegate(int eventId);

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public readonly struct BurstCallback
    {
        [FieldOffset(0)]
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
        private const int PendingCountIndex = 0;
        private const int DroppedCountIndex = 1;
        private const int CounterLength = 2;

        private NativeQueue<int> _events;
        private NativeArray<int> _counters;
        private int _capacity;
        private int _queueSentinelId;
        private int _counterSentinelId;

        public bool IsCreated => _events.IsCreated && _counters.IsCreated;
        public int Capacity => _capacity;
        public int PendingCount
        {
            get
            {
                if (!IsCreated)
                    return 0;

                int pending = _counters[PendingCountIndex];
                if (pending <= 0)
                    return 0;

                return pending < _capacity ? pending : _capacity;
            }
        }
        public int DroppedCount => IsCreated ? math.max(0, _counters[DroppedCountIndex]) : 0;
        /// <summary>Compatibility property for a low-frequency Burst callback writer. Prefer <see cref="OpenParallelWriter"/>.</summary>
        public ParallelEventWriter ParallelWriter => OpenParallelWriter();

        /// <summary>
        /// Opens the retained low-frequency MPSC callback writer.
        /// High-frequency event storms require owner-local batching before this queue.
        /// </summary>
        public ParallelEventWriter OpenParallelWriter()
        {
            NativeQueue<int>.ParallelWriter writer = _events.IsCreated ? _events.AsParallelWriter() : default;
            return new ParallelEventWriter(writer, _counters, _capacity);
        }

        public BurstCallbackQueue(int expectedCapacity)
        {
            _capacity = expectedCapacity <= 0 ? 1 : expectedCapacity;
            _events = new NativeQueue<int>(Allocator.Persistent);
            _counters = default;
            _queueSentinelId = 0;
            _counterSentinelId = 0;
            bool budgetRegistered = false;
            try
            {
                _queueSentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(
                    _events,
                    _capacity,
                    nameof(BurstCallbackQueue),
                    nameof(_events),
                    NativeAllocationLifetime.Session);
                if (_queueSentinelId <= 0)
                    throw new InvalidOperationException("NativeMemorySentinel rejected BurstCallbackQueue event queue registration.");

                _counters = Hecton8.Core.Memory.H8Memory.Allocate<int>(
                    CounterLength,
                    Hecton8.Core.Memory.SystemID.CoreDiagnostics,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
                if (!_counters.IsCreated)
                    throw new InvalidOperationException("BurstCallbackQueue counter allocation failed.");

                _counterSentinelId = NativeMemorySentinel.RegisterNativeArray(
                    _counters,
                    nameof(BurstCallbackQueue),
                    nameof(_counters),
                    NativeAllocationLifetime.Session);
                if (_counterSentinelId <= 0)
                    throw new InvalidOperationException("NativeMemorySentinel rejected BurstCallbackQueue counter registration.");

                int bytes = sizeof(int) * (_capacity + CounterLength);
                MemoryBudgetTracker.Register(BudgetOwner, bytes, bytes);
                budgetRegistered = true;
                Prewarm();
            }
            catch (Exception initializationException)
            {
                Exception cleanupException = null;

                if (_counterSentinelId > 0)
                {
                    try
                    {
                        NativeMemorySentinel.Unregister(_counterSentinelId);
                    }
                    catch (Exception exception)
                    {
                        cleanupException = exception;
                    }
                    finally
                    {
                        _counterSentinelId = 0;
                    }
                }

                try
                {
                    Hecton8.Core.Memory.H8Memory.Release(
                        ref _counters,
                        Hecton8.Core.Memory.SystemID.CoreDiagnostics);
                }
                catch (Exception exception)
                {
                    if (cleanupException == null)
                        cleanupException = exception;
                }

                if (_queueSentinelId > 0)
                {
                    try
                    {
                        NativeMemorySentinel.Unregister(_queueSentinelId);
                    }
                    catch (Exception exception)
                    {
                        if (cleanupException == null)
                            cleanupException = exception;
                    }
                    finally
                    {
                        _queueSentinelId = 0;
                    }
                }

                if (_events.IsCreated)
                {
                    try
                    {
                        _events.Dispose();
                    }
                    catch (Exception exception)
                    {
                        if (cleanupException == null)
                            cleanupException = exception;
                    }
                    finally
                    {
                        _events = default;
                    }
                }

                if (budgetRegistered && !_counters.IsCreated && !_events.IsCreated)
                    MemoryBudgetTracker.Unregister(BudgetOwner);
                _capacity = 0;

                if (cleanupException != null)
                    throw new AggregateException(
                        "Burst callback initialization failed and native cleanup also failed.",
                        initializationException,
                        cleanupException);

                throw;
            }
        }

        public void Enqueue(int eventId)
        {
            TryEnqueue(eventId);
        }

        public bool TryEnqueue(int eventId)
        {
            if (!IsCreated)
                return false;

            int index = _counters[PendingCountIndex];
            if ((uint)index >= (uint)_capacity)
            {
                int dropped = _counters[DroppedCountIndex];
                if (dropped < int.MaxValue)
                    _counters[DroppedCountIndex] = dropped + 1;
                return false;
            }

            _events.Enqueue(eventId);
            _counters[PendingCountIndex] = index + 1;
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
                _counters[PendingCountIndex] = 0;
                return false;
            }

            int pending = _counters[PendingCountIndex] - 1;
            _counters[PendingCountIndex] = pending > 0 ? pending : 0;
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
                int remaining = _counters[PendingCountIndex] - 1;
                _counters[PendingCountIndex] = remaining > 0 ? remaining : 0;
                callback.Invoke(eventId);
                drained++;
            }
            return drained;
        }

        public void Clear()
        {
            if (!IsCreated)
                return;

            while (_events.TryDequeue(out _))
            {
            }

            _counters[PendingCountIndex] = 0;
        }

        public void Dispose()
        {
            if (!_events.IsCreated &&
                !_counters.IsCreated &&
                _queueSentinelId <= 0 &&
                _counterSentinelId <= 0)
                return;

            if (IsCreated)
                Clear();

            Exception cleanupException = null;

            if (_counters.IsCreated)
            {
                Hecton8.Core.Memory.H8Memory.Release(
                    ref _counters,
                    Hecton8.Core.Memory.SystemID.CoreDiagnostics);
                if (_counters.IsCreated)
                    return;

                if (_counterSentinelId > 0)
                {
                    try
                    {
                        NativeMemorySentinel.Unregister(_counterSentinelId);
                    }
                    catch (Exception exception)
                    {
                        cleanupException = exception;
                    }
                    finally
                    {
                        _counterSentinelId = 0;
                    }
                }
            }
            else if (_counterSentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(_counterSentinelId);
                }
                catch (Exception exception)
                {
                    cleanupException = exception;
                }
                finally
                {
                    _counterSentinelId = 0;
                }
            }

            if (_queueSentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(_queueSentinelId);
                }
                catch (Exception exception)
                {
                    cleanupException = exception;
                }
                finally
                {
                    _queueSentinelId = 0;
                }
            }

            if (_events.IsCreated)
            {
                try
                {
                    _events.Dispose();
                }
                catch (Exception exception)
                {
                    if (cleanupException == null)
                        cleanupException = exception;
                }
                finally
                {
                    _events = default;
                }
            }
            else
            {
                _events = default;
            }

            try
            {
                MemoryBudgetTracker.Unregister(BudgetOwner);
            }
            catch (Exception exception)
            {
                if (cleanupException == null)
                    cleanupException = exception;
            }

            _capacity = 0;

            if (cleanupException != null)
                throw cleanupException;
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            if (!_events.IsCreated &&
                !_counters.IsCreated &&
                _queueSentinelId <= 0 &&
                _counterSentinelId <= 0)
                return inputDeps;

            JobHandle counterDisposeHandle = inputDeps;
            if (_counters.IsCreated)
            {
                counterDisposeHandle = Hecton8.Core.Memory.H8Memory.Release(
                    ref _counters,
                    inputDeps,
                    Hecton8.Core.Memory.SystemID.CoreDiagnostics);
                if (_counters.IsCreated)
                    return counterDisposeHandle;

                if (_counterSentinelId > 0)
                    CompleteCounterDisposeBeforeSentinelUnregister(ref counterDisposeHandle);
            }
            else if (_counterSentinelId > 0)
            {
                NativeMemorySentinel.Unregister(_counterSentinelId);
                _counterSentinelId = 0;
            }

            JobHandle eventsDisposeHandle = inputDeps;
            if (_events.IsCreated)
            {
                eventsDisposeHandle = _events.Dispose(inputDeps);
                if (_queueSentinelId > 0)
                    CompleteEventQueueDisposeBeforeSentinelUnregister(ref eventsDisposeHandle);
                else
                    _events = default;
            }
            else if (_queueSentinelId > 0)
            {
                NativeMemorySentinel.Unregister(_queueSentinelId);
                _queueSentinelId = 0;
            }

            MemoryBudgetTracker.Unregister(BudgetOwner);
            _capacity = 0;
            return JobHandle.CombineDependencies(eventsDisposeHandle, counterDisposeHandle);
        }

        private void CompleteCounterDisposeBeforeSentinelUnregister(ref JobHandle disposeHandle)
        {
            disposeHandle.Complete();
            if (_counterSentinelId > 0)
            {
                NativeMemorySentinel.Unregister(_counterSentinelId);
                _counterSentinelId = 0;
            }
        }

        private void CompleteEventQueueDisposeBeforeSentinelUnregister(ref JobHandle disposeHandle)
        {
            disposeHandle.Complete();
            _events = default;
            if (_queueSentinelId > 0)
            {
                NativeMemorySentinel.Unregister(_queueSentinelId);
                _queueSentinelId = 0;
            }
        }

        private void Prewarm()
        {
            for (int i = 0; i < _capacity; i++)
                _events.Enqueue(0);

            for (int i = 0; i < _capacity; i++)
                _events.TryDequeue(out _);
        }

        [StructLayout(LayoutKind.Sequential)]
        public ref struct ParallelEventWriter
        {
            private NativeQueue<int>.ParallelWriter _events;
            [NativeDisableUnsafePtrRestriction] private readonly int* _counters;
            private readonly int _capacity;

            internal ParallelEventWriter(NativeQueue<int>.ParallelWriter events, NativeArray<int> counters, int capacity)
            {
                _events = events;
                _counters = counters.IsCreated ? (int*)NativeArrayUnsafeUtility.GetUnsafePtr(counters) : null;
                _capacity = capacity;
            }

            public bool TryEnqueue(int eventId)
            {
                if (_counters == null || _capacity <= 0)
                    return false;

                ref int pendingCount = ref UnsafeUtility.AsRef<int>(_counters + PendingCountIndex);
                int index = Interlocked.Increment(ref pendingCount) - 1;
                if ((uint)index >= (uint)_capacity)
                {
                    Interlocked.Decrement(ref pendingCount);
                    ref int droppedCount = ref UnsafeUtility.AsRef<int>(_counters + DroppedCountIndex);
                    Interlocked.Increment(ref droppedCount);
                    return false;
                }

                _events.Enqueue(eventId);
                return true;
            }
        }
    }
}
