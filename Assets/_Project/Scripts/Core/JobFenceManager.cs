using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Hecton8.Core
{
    /// <summary>
    /// Persistent non-allocating JobHandle fan-in buffer.
    /// </summary>
    public struct JobFenceManager : IDisposable
    {
        private const string BudgetOwner = nameof(JobFenceManager);

        public NativeArray<JobHandle> Handles;
        public int Capacity;
        public int Count;
        public int WriteIndex;
        public int SentinelId;

        public JobFenceManager(int capacity)
        {
            Capacity = capacity <= 0 ? 1 : capacity;
            Count = 0;
            WriteIndex = 0;
            Handles = new NativeArray<JobHandle>(Capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            SentinelId = NativeMemorySentinel.RegisterNativeArray(
                Handles,
                nameof(JobFenceManager),
                nameof(Handles),
                NativeAllocationLifetime.Session);

            long bytes = (long)UnsafeUtility.SizeOf<JobHandle>() * Capacity;
            MemoryBudgetTracker.Register(BudgetOwner, bytes, bytes);
        }

        public bool TryRegister(JobHandle handle)
        {
            if (!Handles.IsCreated || Count >= Capacity)
                return false;

            Handles[WriteIndex] = handle;
            WriteIndex++;
            if (WriteIndex >= Capacity)
                WriteIndex = 0;

            Count++;
            return true;
        }

        public JobHandle CombineAndClear()
        {
            if (!Handles.IsCreated || Count <= 0)
                return default;

            JobHandle combined = CombineRegisteredHandles();
            ClearRegisteredSlots();
            return combined;
        }

        public void Clear()
        {
            if (!Handles.IsCreated)
            {
                Count = 0;
                WriteIndex = 0;
                return;
            }

            ClearRegisteredSlots();
        }

        public void Dispose()
        {
            if (!Handles.IsCreated)
                return;

            ClearRegisteredSlots();
            if (SentinelId != 0)
            {
                NativeMemorySentinel.Unregister(SentinelId);
                SentinelId = 0;
            }

            MemoryBudgetTracker.Unregister(BudgetOwner);
            Handles.Dispose();
            Capacity = 0;
            Count = 0;
            WriteIndex = 0;
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            if (!Handles.IsCreated)
                return inputDeps;

            if (SentinelId != 0)
            {
                NativeMemorySentinel.Unregister(SentinelId);
                SentinelId = 0;
            }

            MemoryBudgetTracker.Unregister(BudgetOwner);
            JobHandle disposeHandle = Handles.Dispose(inputDeps);
            Handles = default;
            Capacity = 0;
            Count = 0;
            WriteIndex = 0;
            return disposeHandle;
        }

        private void ClearRegisteredSlots()
        {
            if (Count <= 0)
                return;

            int readIndex = WriteIndex - Count;
            while (readIndex < 0)
                readIndex += Capacity;

            for (int i = 0; i < Count; i++)
            {
                Handles[readIndex] = default;
                readIndex++;
                if (readIndex >= Capacity)
                    readIndex = 0;
            }

            Count = 0;
            WriteIndex = 0;
        }

        private JobHandle CombineRegisteredHandles()
        {
            int readIndex = WriteIndex - Count;
            if (readIndex < 0)
                readIndex += Capacity;

            JobHandle combined = Handles[readIndex];
            for (int i = 1; i < Count; i++)
            {
                readIndex++;
                if (readIndex >= Capacity)
                    readIndex = 0;

                combined = JobHandle.CombineDependencies(combined, Handles[readIndex]);
            }

            return combined;
        }
    }
}
