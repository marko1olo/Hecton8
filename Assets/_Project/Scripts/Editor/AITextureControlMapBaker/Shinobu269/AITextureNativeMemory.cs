#if UNITY_EDITOR
using System;
using Hecton8.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Editor.AITextureControlMaps
{
    internal static class AITextureNativeMemory
    {
        internal static NativeArray<T> AllocateArray<T>(int length, Allocator allocator, NativeArrayOptions options, string owner, string label) where T : struct
        {
            if (length <= 0)
                return default;

            NativeArray<T> array = new NativeArray<T>(length, allocator, options);
            if (!array.IsCreated)
                throw new InvalidOperationException("[AITextureNativeMemory] NativeArray allocation failed for " + label + ".");

            RegisterArray(ref array, owner, label, ResolveLifetime(allocator));
            return array;
        }

        internal static void RegisterArray<T>(ref NativeArray<T> array, string owner, string label, NativeAllocationLifetime lifetime) where T : struct
        {
            if (!array.IsCreated)
                return;

            string safeOwner = string.IsNullOrEmpty(owner) ? nameof(AITextureNativeMemory) : owner;
            string safeLabel = string.IsNullOrEmpty(label) ? typeof(T).Name : label;
            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, safeOwner, safeLabel, lifetime);
                if (sentinelId <= 0)
                    throw new InvalidOperationException("[AITextureNativeMemory] NativeMemorySentinel rejected NativeArray registration for " + safeOwner + "." + safeLabel + ".");
            }
            catch
            {
                array.Dispose();
                array = default;
                throw;
            }
        }

        internal static unsafe void DisposeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            System.Exception nativeSentinelCleanupException0 = null;

            try
            {
                NativeMemorySentinel.UnregisterPointer(trackedPointer);
            }
            catch (System.Exception nativeSentinelException0)
            {
                nativeSentinelCleanupException0 = nativeSentinelException0;
            }

            try
            {
                array.Dispose();
            }
            catch (System.Exception nativeSentinelException0)
            {
                if (nativeSentinelCleanupException0 == null)
                    nativeSentinelCleanupException0 = nativeSentinelException0;
            }
            finally
            {
                array = default;
            }

            if (nativeSentinelCleanupException0 != null)
                throw nativeSentinelCleanupException0;
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
