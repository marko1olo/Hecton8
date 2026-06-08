#if UNITY_EDITOR
using System;
using Hecton8.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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

        internal static NativeList<T> AllocateList<T>(int capacity, Allocator allocator, string owner, string label, out int sentinelId) where T : unmanaged
        {
            sentinelId = 0;
            NativeList<T> list = new NativeList<T>(math.max(1, capacity), allocator);
            if (!list.IsCreated)
                throw new InvalidOperationException("[GeologyForgeNativeMemory] NativeList allocation failed for " + owner + "." + label + ".");

            try
            {
                sentinelId = NativeMemorySentinel.RegisterNativeListInstance(list, owner, label, ResolveLifetime(allocator));
                if (sentinelId <= 0)
                    throw new InvalidOperationException("[GeologyForgeNativeMemory] NativeMemorySentinel rejected NativeList registration for " + owner + "." + label + ".");
            }
            catch
            {
                System.Exception nativeSentinelCleanupException1 = null;

                if (sentinelId > 0)
                {
                    try
                    {
                        NativeMemorySentinel.Unregister(sentinelId);
                    }
                    catch (System.Exception nativeSentinelException1)
                    {
                        nativeSentinelCleanupException1 = nativeSentinelException1;
                    }
                    finally
                    {
                        sentinelId = 0;
                    }
                }

                try
                {
                    list.Dispose();
                }
                catch (System.Exception nativeSentinelException1)
                {
                    if (nativeSentinelCleanupException1 == null)
                        nativeSentinelCleanupException1 = nativeSentinelException1;
                }

                if (nativeSentinelCleanupException1 != null)
                    throw nativeSentinelCleanupException1;

                throw;
            }

            return list;
        }

        internal static void DisposeList<T>(ref NativeList<T> list, ref int sentinelId) where T : unmanaged
        {
            Exception firstException = null;

            if (sentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(sentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    sentinelId = 0;
                }
            }

            if (list.IsCreated)
            {
                try
                {
                    list.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    list = default;
                }
            }
            else
            {
                list = default;
            }

            if (firstException != null)
                throw firstException;
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
