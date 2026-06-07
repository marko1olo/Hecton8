#if UNITY_EDITOR
using System;
using Hecton8.Core;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Editor.GeologyForge
{
    internal static class GeologyForgeNativeMemory
    {
        internal static NativeArray<T> AllocateArray<T>(int length, Allocator allocator, NativeArrayOptions options, string owner, string label) where T : struct
        {
            if (length <= 0)
                return default;

            NativeArray<T> array = new NativeArray<T>(length, allocator, options);
            if (!array.IsCreated)
                throw new InvalidOperationException("[GeologyForgeNativeMemory] NativeArray allocation failed for " + owner + "." + label + ".");

            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, owner, label, ResolveLifetime(allocator));
                if (sentinelId <= 0)
                    throw new InvalidOperationException("[GeologyForgeNativeMemory] NativeMemorySentinel rejected NativeArray registration for " + owner + "." + label + ".");
            }
            catch
            {
                array.Dispose();
                throw;
            }

            return array;
        }

        internal static void DisposeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            try
            {
                NativeMemorySentinel.UnregisterNativeArray(array);
            }
            finally
            {
                array.Dispose();
                array = default;
            }
        }

        internal static NativeList<T> AllocateList<T>(int capacity, Allocator allocator, string owner, string label) where T : unmanaged
        {
            NativeList<T> list = new NativeList<T>(math.max(1, capacity), allocator);
            if (!list.IsCreated)
                throw new InvalidOperationException("[GeologyForgeNativeMemory] NativeList allocation failed for " + owner + "." + label + ".");

            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeList(list, owner, label, ResolveLifetime(allocator));
                if (sentinelId <= 0)
                    throw new InvalidOperationException("[GeologyForgeNativeMemory] NativeMemorySentinel rejected NativeList registration for " + owner + "." + label + ".");
            }
            catch
            {
                list.Dispose();
                throw;
            }

            return list;
        }

        internal static void DisposeList<T>(ref NativeList<T> list, string owner, string label) where T : unmanaged
        {
            if (!list.IsCreated)
                return;

            try
            {
                NativeMemorySentinel.UnregisterNativeList(owner, label);
            }
            finally
            {
                list.Dispose();
                list = default;
            }
        }

        private static NativeAllocationLifetime ResolveLifetime(Allocator allocator)
        {
            switch (allocator)
            {
                case Allocator.Temp:
                    return NativeAllocationLifetime.Temp;
                case Allocator.TempJob:
                    return NativeAllocationLifetime.TempJob;
                case Allocator.Persistent:
                    return NativeAllocationLifetime.Session;
                default:
                    return NativeAllocationLifetime.Session;
            }
        }
    }
}
#endif
