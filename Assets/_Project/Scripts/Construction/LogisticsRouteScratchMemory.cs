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
        private const BufferID EdgeOffsetsBufferId = BufferID.LogisticsRouteScratchMemory_EdgeOffsetsBufferId;
        private const BufferID EdgeDestinationsBufferId = BufferID.LogisticsRouteScratchMemory_EdgeDestinationsBufferId;
        private const BufferID EdgeWriteCursorBufferId = BufferID.LogisticsRouteScratchMemory_EdgeWriteCursorBufferId;
        private const BufferID StorageCapacityByNodeBufferId = BufferID.LogisticsRouteScratchMemory_StorageCapacityByNodeBufferId;
        private const BufferID VisitedBufferId = BufferID.LogisticsRouteScratchMemory_VisitedBufferId;
        private const BufferID QueueBufferId = BufferID.LogisticsRouteScratchMemory_QueueBufferId;
        private const BufferID ResultNodeIndexBufferId = BufferID.LogisticsRouteScratchMemory_ResultNodeIndexBufferId;
        private const ulong RouteScratchMutationGuardMask = 0x000000000000007FUL;

        private static VaultGenerationHandle<int> s_EdgeOffsetsHandle;
        private static VaultGenerationHandle<int> s_EdgeDestinationsHandle;
        private static VaultGenerationHandle<int> s_EdgeWriteCursorHandle;
        private static VaultGenerationHandle<byte> s_StorageCapacityByNodeHandle;
        private static VaultGenerationHandle<byte> s_VisitedHandle;
        private static VaultGenerationHandle<int> s_QueueHandle;
        private static VaultGenerationHandle<int> s_ResultNodeIndexHandle;
        private static bool s_WriteGuardHeld;
        private static IDataVault s_WriteGuardVault;

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

            if (!vault.TryAcquireMutationGuard(RouteScratchMutationGuardMask))
                return false;

            bool guardHeld = true;
            int nodeCapacity = math.max(InitialNodeCapacity, nodeCount);
            int edgeCapacity = math.max(InitialEdgeCapacity, edgeCount);
            int edgeOffsetCapacity = math.max(InitialNodeCapacity + 1, nodeCount + 1);
            bool acquired = false;
            try
            {
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

                if (!TryResolveWriteBuffer(vault, in s_EdgeOffsetsHandle, EdgeOffsetsBufferId, out edgeOffsets) ||
                    !TryResolveWriteBuffer(vault, in s_EdgeDestinationsHandle, EdgeDestinationsBufferId, out edgeDestinations) ||
                    !TryResolveWriteBuffer(vault, in s_EdgeWriteCursorHandle, EdgeWriteCursorBufferId, out edgeWriteCursor) ||
                    !TryResolveWriteBuffer(vault, in s_StorageCapacityByNodeHandle, StorageCapacityByNodeBufferId, out storageCapacityByNode) ||
                    !TryResolveWriteBuffer(vault, in s_VisitedHandle, VisitedBufferId, out visited) ||
                    !TryResolveWriteBuffer(vault, in s_QueueHandle, QueueBufferId, out queue) ||
                    !TryResolveWriteBuffer(vault, in s_ResultNodeIndexHandle, ResultNodeIndexBufferId, out resultNodeIndex))
                {
                    return false;
                }

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
                if (!acquired)
                    return false;

                s_WriteGuardHeld = true;
                s_WriteGuardVault = vault;
                guardHeld = false;
                return acquired;
            }
            finally
            {
                if (guardHeld)
                    vault.ReleaseMutationGuard(RouteScratchMutationGuardMask);
            }
        }

        internal static void Dispose(IDataVault vault)
        {
            if (vault != null)
            {
                ReleaseWriteLocks(vault);
                ReleaseBuffer(vault, ref s_EdgeOffsetsHandle, EdgeOffsetsBufferId);
                ReleaseBuffer(vault, ref s_EdgeDestinationsHandle, EdgeDestinationsBufferId);
                ReleaseBuffer(vault, ref s_EdgeWriteCursorHandle, EdgeWriteCursorBufferId);
                ReleaseBuffer(vault, ref s_StorageCapacityByNodeHandle, StorageCapacityByNodeBufferId);
                ReleaseBuffer(vault, ref s_VisitedHandle, VisitedBufferId);
                ReleaseBuffer(vault, ref s_QueueHandle, QueueBufferId);
                ReleaseBuffer(vault, ref s_ResultNodeIndexHandle, ResultNodeIndexBufferId);
                return;
            }

            s_EdgeOffsetsHandle = default;
            s_EdgeDestinationsHandle = default;
            s_EdgeWriteCursorHandle = default;
            s_StorageCapacityByNodeHandle = default;
            s_VisitedHandle = default;
            s_QueueHandle = default;
            s_ResultNodeIndexHandle = default;
            s_WriteGuardHeld = false;
            s_WriteGuardVault = null;
        }

        internal static void ReleaseWriteLocks(IDataVault vault)
        {
            if (!s_WriteGuardHeld)
                return;

            IDataVault guardVault = s_WriteGuardVault ?? vault;
            s_WriteGuardHeld = false;
            s_WriteGuardVault = null;
            guardVault?.ReleaseMutationGuard(RouteScratchMutationGuardMask);
        }

        private static bool TryEnsureHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            int safeLength = math.max(1, requiredLength);
            if (IsRouteScratchHandle(in handle, bufferId) &&
                vault.TryReadHandle(in handle, out NativeArray<T> existing) &&
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
            return IsRouteScratchHandle(in handle, bufferId) &&
                   vault.TryReadHandle(in handle, out existing) &&
                   existing.IsCreated &&
                   existing.Length >= safeLength;
        }

        private static bool TryResolveWriteBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return IsRouteScratchHandle(in handle, expectedBufferId) &&
                   vault.TryResolveHandle(in handle, out buffer);
        }

        private static void ReleaseBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID expectedBufferId) where T : struct
        {
            if (IsRouteScratchHandle(in handle, expectedBufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsRouteScratchHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == (uint)OwnerSystem &&
                   handle.Generation != 0u;
        }
    }
}
