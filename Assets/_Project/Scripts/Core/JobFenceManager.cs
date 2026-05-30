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

        private NativeArray<JobHandle> Handles;
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
            if (!Handles.IsCreated || Capacity <= 0)
                return false;

            int safeCount = ResolveSafeCount();
            if (safeCount >= Capacity)
                return false;

            int writeIndex = NormalizeIndex(WriteIndex);
            Handles[writeIndex] = handle;
            WriteIndex = AdvanceIndex(writeIndex);
            Count = safeCount + 1;
            return true;
        }

        public JobHandle CombineAndClear()
        {
            int safeCount = ResolveSafeCount();
            if (!Handles.IsCreated || safeCount <= 0)
                return default;

            int safeWriteIndex = NormalizeIndex(WriteIndex);
            JobHandle combined = CombineRegisteredHandles(safeCount, safeWriteIndex);
            ClearRegisteredSlots(safeCount, safeWriteIndex);
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

            ClearRegisteredSlots(ResolveSafeCount(), NormalizeIndex(WriteIndex));
        }

        public void Dispose()
        {
            if (!Handles.IsCreated)
                return;

            ClearRegisteredSlots(ResolveSafeCount(), NormalizeIndex(WriteIndex));
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

        private void ClearRegisteredSlots(int safeCount, int safeWriteIndex)
        {
            if (!Handles.IsCreated || Capacity <= 0 || safeCount <= 0)
                return;

            int readIndex = NormalizeIndex(safeWriteIndex - safeCount);

            for (int i = 0; i < safeCount; i++)
            {
                Handles[readIndex] = default;
                readIndex = AdvanceIndex(readIndex);
            }

            Count = 0;
            WriteIndex = 0;
        }

        private JobHandle CombineRegisteredHandles(int safeCount, int safeWriteIndex)
        {
            int readIndex = NormalizeIndex(safeWriteIndex - safeCount);

            JobHandle combined = Handles[readIndex];
            for (int i = 1; i < safeCount; i++)
            {
                readIndex = AdvanceIndex(readIndex);

                combined = JobHandle.CombineDependencies(combined, Handles[readIndex]);
            }

            return combined;
        }

        private int ResolveSafeCount()
        {
            if (!Handles.IsCreated || Capacity <= 0)
                return 0;

            if (Count <= 0)
                return 0;

            return Count > Capacity ? Capacity : Count;
        }

        private int NormalizeIndex(int index)
        {
            if (Capacity <= 0)
                return 0;

            int normalized = index % Capacity;
            return normalized < 0 ? normalized + Capacity : normalized;
        }

        private int AdvanceIndex(int index)
        {
            int next = index + 1;
            return next >= Capacity || next < 0 ? 0 : next;
        }
    }
}
