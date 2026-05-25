using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    /// <summary>
    /// Vault handles for logistics CSR routing scratch.
    /// Physical arrays are resolved only as caller-local write views.
    /// </summary>
    internal static class LogisticsRouteScratchMemory
    {
        private const int InitialNodeCapacity = 4096;
        private const int InitialEdgeCapacity = 8192;
        private const SystemID OwnerSystem = SystemID.Construction;
        private const BufferID EdgeOffsetsBufferId = (BufferID)72032;
        private const BufferID EdgeDestinationsBufferId = (BufferID)72033;
        private const BufferID EdgeWriteCursorBufferId = (BufferID)72034;
        private const BufferID StorageCapacityByNodeBufferId = (BufferID)72035;
        private const BufferID VisitedBufferId = (BufferID)72036;
        private const BufferID QueueBufferId = (BufferID)72037;
        private const BufferID ResultNodeIndexBufferId = (BufferID)72038;

        private static VaultGenerationHandle<int> s_EdgeOffsetsHandle;
        private static VaultGenerationHandle<int> s_EdgeDestinationsHandle;
        private static VaultGenerationHandle<int> s_EdgeWriteCursorHandle;
        private static VaultGenerationHandle<byte> s_StorageCapacityByNodeHandle;
        private static VaultGenerationHandle<byte> s_VisitedHandle;
        private static VaultGenerationHandle<int> s_QueueHandle;
        private static VaultGenerationHandle<int> s_ResultNodeIndexHandle;

        internal static bool TryAcquireWriteBuffers(
            IDataVault vault,
            int nodeCount,
            int edgeCount,
            out NativeArray<int> edgeOffsets,
            out NativeArray<int> edgeDestinations,
            out NativeArray<int> edgeWriteCursor,
            out NativeArray<byte> storageCapacityByNode,
            out NativeArray<byte> visited,
            out NativeArray<int> queue,
            out NativeArray<int> resultNodeIndex)
        {
            edgeOffsets = default;
            edgeDestinations = default;
            edgeWriteCursor = default;
            storageCapacityByNode = default;
            visited = default;
            queue = default;
            resultNodeIndex = default;

            if (vault == null || nodeCount <= 0 || edgeCount < 0)
                return false;

            int nodeCapacity = math.max(InitialNodeCapacity, nodeCount);
            int edgeCapacity = math.max(InitialEdgeCapacity, edgeCount);
            int edgeOffsetCapacity = math.max(InitialNodeCapacity + 1, nodeCount + 1);
            if (!TryEnsureHandle(vault, ref s_EdgeOffsetsHandle, EdgeOffsetsBufferId, edgeOffsetCapacity) ||
                !TryEnsureHandle(vault, ref s_EdgeDestinationsHandle, EdgeDestinationsBufferId, edgeCapacity) ||
                !TryEnsureHandle(vault, ref s_EdgeWriteCursorHandle, EdgeWriteCursorBufferId, nodeCapacity) ||
                !TryEnsureHandle(vault, ref s_StorageCapacityByNodeHandle, StorageCapacityByNodeBufferId, nodeCapacity) ||
                !TryEnsureHandle(vault, ref s_VisitedHandle, VisitedBufferId, nodeCapacity) ||
                !TryEnsureHandle(vault, ref s_QueueHandle, QueueBufferId, nodeCapacity) ||
                !TryEnsureHandle(vault, ref s_ResultNodeIndexHandle, ResultNodeIndexBufferId, 1))
            {
                return false;
            }

            bool acquired = false;
            bool edgeOffsetsLocked = false;
            bool edgeDestinationsLocked = false;
            bool edgeWriteCursorLocked = false;
            bool storageCapacityLocked = false;
            bool visitedLocked = false;
            bool queueLocked = false;
            bool resultLocked = false;
            try
            {
                if (!vault.TryAcquireWriteLock(in s_EdgeOffsetsHandle, OwnerSystem, out edgeOffsets))
                    return false;
                edgeOffsetsLocked = true;
                if (!vault.TryAcquireWriteLock(in s_EdgeDestinationsHandle, OwnerSystem, out edgeDestinations))
                    return false;
                edgeDestinationsLocked = true;
                if (!vault.TryAcquireWriteLock(in s_EdgeWriteCursorHandle, OwnerSystem, out edgeWriteCursor))
                    return false;
                edgeWriteCursorLocked = true;
                if (!vault.TryAcquireWriteLock(in s_StorageCapacityByNodeHandle, OwnerSystem, out storageCapacityByNode))
                    return false;
                storageCapacityLocked = true;
                if (!vault.TryAcquireWriteLock(in s_VisitedHandle, OwnerSystem, out visited))
                    return false;
                visitedLocked = true;
                if (!vault.TryAcquireWriteLock(in s_QueueHandle, OwnerSystem, out queue))
                    return false;
                queueLocked = true;
                if (!vault.TryAcquireWriteLock(in s_ResultNodeIndexHandle, OwnerSystem, out resultNodeIndex))
                    return false;
                resultLocked = true;

                acquired =
                    edgeOffsets.IsCreated &&
                    edgeDestinations.IsCreated &&
                    edgeWriteCursor.IsCreated &&
                    storageCapacityByNode.IsCreated &&
                    visited.IsCreated &&
                    queue.IsCreated &&
                    resultNodeIndex.IsCreated &&
                    edgeOffsets.Length >= edgeOffsetCapacity &&
                    edgeDestinations.Length >= edgeCapacity &&
                    edgeWriteCursor.Length >= nodeCapacity &&
                    storageCapacityByNode.Length >= nodeCapacity &&
                    visited.Length >= nodeCapacity &&
                    queue.Length >= nodeCapacity &&
                    resultNodeIndex.Length >= 1;
                return acquired;
            }
            finally
            {
                if (!acquired)
                    ReleaseWriteLocks(
                        vault,
                        edgeOffsetsLocked,
                        edgeDestinationsLocked,
                        edgeWriteCursorLocked,
                        storageCapacityLocked,
                        visitedLocked,
                        queueLocked,
                        resultLocked);
            }
        }

        internal static void Dispose()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault != null)
            {
                ReleaseBuffer(vault, ref s_EdgeOffsetsHandle);
                ReleaseBuffer(vault, ref s_EdgeDestinationsHandle);
                ReleaseBuffer(vault, ref s_EdgeWriteCursorHandle);
                ReleaseBuffer(vault, ref s_StorageCapacityByNodeHandle);
                ReleaseBuffer(vault, ref s_VisitedHandle);
                ReleaseBuffer(vault, ref s_QueueHandle);
                ReleaseBuffer(vault, ref s_ResultNodeIndexHandle);
                return;
            }

            s_EdgeOffsetsHandle = default;
            s_EdgeDestinationsHandle = default;
            s_EdgeWriteCursorHandle = default;
            s_StorageCapacityByNodeHandle = default;
            s_VisitedHandle = default;
            s_QueueHandle = default;
            s_ResultNodeIndexHandle = default;
        }

        internal static void ReleaseWriteLocks(IDataVault vault)
        {
            if (vault == null)
                return;

            ReleaseWriteLock(vault, in s_ResultNodeIndexHandle);
            ReleaseWriteLock(vault, in s_QueueHandle);
            ReleaseWriteLock(vault, in s_VisitedHandle);
            ReleaseWriteLock(vault, in s_StorageCapacityByNodeHandle);
            ReleaseWriteLock(vault, in s_EdgeWriteCursorHandle);
            ReleaseWriteLock(vault, in s_EdgeDestinationsHandle);
            ReleaseWriteLock(vault, in s_EdgeOffsetsHandle);
        }

        private static void ReleaseWriteLocks(
            IDataVault vault,
            bool edgeOffsetsLocked,
            bool edgeDestinationsLocked,
            bool edgeWriteCursorLocked,
            bool storageCapacityLocked,
            bool visitedLocked,
            bool queueLocked,
            bool resultLocked)
        {
            if (vault == null)
                return;

            if (resultLocked)
                ReleaseWriteLock(vault, in s_ResultNodeIndexHandle);
            if (queueLocked)
                ReleaseWriteLock(vault, in s_QueueHandle);
            if (visitedLocked)
                ReleaseWriteLock(vault, in s_VisitedHandle);
            if (storageCapacityLocked)
                ReleaseWriteLock(vault, in s_StorageCapacityByNodeHandle);
            if (edgeWriteCursorLocked)
                ReleaseWriteLock(vault, in s_EdgeWriteCursorHandle);
            if (edgeDestinationsLocked)
                ReleaseWriteLock(vault, in s_EdgeDestinationsHandle);
            if (edgeOffsetsLocked)
                ReleaseWriteLock(vault, in s_EdgeOffsetsHandle);
        }

        private static bool TryEnsureHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            int safeLength = math.max(1, requiredLength);
            if (IsHandleCreated(in handle) &&
                vault.TryResolveHandle(in handle, out NativeArray<T> existing) &&
                existing.IsCreated &&
                existing.Length >= safeLength)
            {
                return true;
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                safeLength,
                OwnerSystem,
                NativeArrayOptions.ClearMemory);
            return IsHandleCreated(in handle) &&
                   vault.TryResolveHandle(in handle, out existing) &&
                   existing.IsCreated &&
                   existing.Length >= safeLength;
        }

        private static void ReleaseWriteLock<T>(IDataVault vault, in VaultGenerationHandle<T> handle) where T : struct
        {
            if (!IsHandleCreated(in handle))
                return;

            vault.ReleaseWriteLock(in handle, OwnerSystem);
        }

        private static void ReleaseBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (IsHandleCreated(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }
    }
}
