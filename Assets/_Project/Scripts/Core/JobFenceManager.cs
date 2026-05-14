using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Hecton8.Core
{
    /// <summary>
    /// Persistent non-allocating JobHandle fan-in buffer.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct JobFenceManager : IDisposable
    {
        private const string BudgetOwner = nameof(JobFenceManager);

        private NativeArray<JobHandle> _handles;
        private int _capacity;
        private int _count;
        private int _writeIndex;
        private int _sentinelId;

        public bool IsCreated => _handles.IsCreated;
        public int Count => _count;
        public int Capacity => _capacity;

        public JobFenceManager(int capacity)
        {
            _capacity = capacity <= 0 ? 1 : capacity;
            _count = 0;
            _writeIndex = 0;
            _handles = new NativeArray<JobHandle>(_capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _sentinelId = NativeMemorySentinel.RegisterNativeArray(
                _handles,
                nameof(JobFenceManager),
                nameof(_handles),
                NativeAllocationLifetime.Session);

            long bytes = (long)UnsafeUtility.SizeOf<JobHandle>() * _capacity;
            MemoryBudgetTracker.Register(BudgetOwner, bytes, bytes);
        }

        public bool TryRegister(JobHandle handle)
        {
            if (!_handles.IsCreated || _count >= _capacity)
                return false;

            _handles[_writeIndex] = handle;
            _writeIndex++;
            if (_writeIndex >= _capacity)
                _writeIndex = 0;

            _count++;
            return true;
        }

        public JobHandle CombineAndClear()
        {
            if (!_handles.IsCreated || _count <= 0)
                return default;

            JobHandle combined = CombineRegisteredHandles();
            ClearRegisteredSlots();
            return combined;
        }

        public void Clear()
        {
            if (!_handles.IsCreated)
            {
                _count = 0;
                _writeIndex = 0;
                return;
            }

            ClearRegisteredSlots();
        }

        public void Dispose()
        {
            if (!_handles.IsCreated)
                return;

            ClearRegisteredSlots();
            if (_sentinelId != 0)
            {
                NativeMemorySentinel.Unregister(_sentinelId);
                _sentinelId = 0;
            }

            MemoryBudgetTracker.Unregister(BudgetOwner);
            _handles.Dispose();
            _capacity = 0;
            _count = 0;
            _writeIndex = 0;
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            if (!_handles.IsCreated)
                return inputDeps;

            if (_sentinelId != 0)
            {
                NativeMemorySentinel.Unregister(_sentinelId);
                _sentinelId = 0;
            }

            MemoryBudgetTracker.Unregister(BudgetOwner);
            JobHandle disposeHandle = _handles.Dispose(inputDeps);
            _handles = default;
            _capacity = 0;
            _count = 0;
            _writeIndex = 0;
            return disposeHandle;
        }

        private void ClearRegisteredSlots()
        {
            if (_count <= 0)
                return;

            int readIndex = _writeIndex - _count;
            while (readIndex < 0)
                readIndex += _capacity;

            for (int i = 0; i < _count; i++)
            {
                _handles[readIndex] = default;
                readIndex++;
                if (readIndex >= _capacity)
                    readIndex = 0;
            }

            _count = 0;
            _writeIndex = 0;
        }

        private JobHandle CombineRegisteredHandles()
        {
            int readIndex = _writeIndex - _count;
            if (readIndex < 0)
                readIndex += _capacity;

            JobHandle combined = _handles[readIndex];
            for (int i = 1; i < _count; i++)
            {
                readIndex++;
                if (readIndex >= _capacity)
                    readIndex = 0;

                combined = JobHandle.CombineDependencies(combined, _handles[readIndex]);
            }

            return combined;
        }
    }
}
