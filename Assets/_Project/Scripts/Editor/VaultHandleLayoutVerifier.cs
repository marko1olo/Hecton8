#if UNITY_EDITOR
using System;
using System.Reflection;
using Hecton8.Core.Memory;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;

namespace Hecton8.Core.Memory.Editor
{
    internal static class VaultHandleLayoutVerifier
    {
        [InitializeOnLoadMethod]
        private static void Verify()
        {
            bool valid =
                VerifyGenerationHandle() &&
                VerifyBufferHandle() &&
                VerifySliceHandle() &&
                VerifyRelocationRecord() &&
                VerifyTelemetrySnapshot() &&
                VerifyMemoryBudgetEntry() &&
                VerifyMemoryBlockSnapshot();

            if (!valid)
                throw new InvalidOperationException("Vault ABI violation. Required: handles 16/16/32 bytes, relocation 32 bytes, telemetry 64 bytes, memory budget 32 bytes, block snapshot 48 bytes with source-declared field offsets.");
        }

        private static bool VerifyGenerationHandle()
        {
            return
                UnsafeUtility.SizeOf<VaultGenerationHandle<byte>>() == 16 &&
                OffsetOf<VaultGenerationHandle<byte>>(nameof(VaultGenerationHandle<byte>.BufferID)) == 0 &&
                OffsetOf<VaultGenerationHandle<byte>>(nameof(VaultGenerationHandle<byte>.SystemID)) == 4 &&
                OffsetOf<VaultGenerationHandle<byte>>(nameof(VaultGenerationHandle<byte>.Generation)) == 8 &&
                OffsetOf<VaultGenerationHandle<byte>>(nameof(VaultGenerationHandle<byte>.Flags)) == 12;
        }

#pragma warning disable 0618
        private static bool VerifyBufferHandle()
        {
            return
                UnsafeUtility.SizeOf<VaultBufferHandle<byte>>() == 16 &&
                OffsetOf<VaultBufferHandle<byte>>(nameof(VaultBufferHandle<byte>.BufferID)) == 0 &&
                OffsetOf<VaultBufferHandle<byte>>(nameof(VaultBufferHandle<byte>.SystemID)) == 4 &&
                OffsetOf<VaultBufferHandle<byte>>(nameof(VaultBufferHandle<byte>.Generation)) == 8 &&
                OffsetOf<VaultBufferHandle<byte>>(nameof(VaultBufferHandle<byte>.Flags)) == 12;
        }
#pragma warning restore 0618

        private static bool VerifySliceHandle()
        {
            return
                UnsafeUtility.SizeOf<VaultSliceHandle<byte>>() == 32 &&
                OffsetOf<VaultSliceHandle<byte>>(nameof(VaultSliceHandle<byte>.BufferID)) == 0 &&
                OffsetOf<VaultSliceHandle<byte>>(nameof(VaultSliceHandle<byte>.SystemID)) == 4 &&
                OffsetOf<VaultSliceHandle<byte>>(nameof(VaultSliceHandle<byte>.Generation)) == 8 &&
                OffsetOf<VaultSliceHandle<byte>>(nameof(VaultSliceHandle<byte>.HandleFlags)) == 12 &&
                OffsetOf<VaultSliceHandle<byte>>(nameof(VaultSliceHandle<byte>.StartIndex)) == 16 &&
                OffsetOf<VaultSliceHandle<byte>>(nameof(VaultSliceHandle<byte>.Length)) == 20 &&
                OffsetOf<VaultSliceHandle<byte>>(nameof(VaultSliceHandle<byte>.Flags)) == 24 &&
                OffsetOf<VaultSliceHandle<byte>>(nameof(VaultSliceHandle<byte>.Reserved0)) == 28;
        }

        private static bool VerifyRelocationRecord()
        {
            return
                UnsafeUtility.SizeOf<VaultRelocationRecord>() == 32 &&
                OffsetOf<VaultRelocationRecord>(nameof(VaultRelocationRecord.OldOffsetBytes)) == 0 &&
                OffsetOf<VaultRelocationRecord>(nameof(VaultRelocationRecord.NewOffsetBytes)) == 8 &&
                OffsetOf<VaultRelocationRecord>(nameof(VaultRelocationRecord.BufferId)) == 16 &&
                OffsetOf<VaultRelocationRecord>(nameof(VaultRelocationRecord.ByteLength)) == 20 &&
                OffsetOf<VaultRelocationRecord>(nameof(VaultRelocationRecord.Generation)) == 24 &&
                OffsetOf<VaultRelocationRecord>(nameof(VaultRelocationRecord.Flags)) == 28 &&
                OffsetOf<VaultRelocationRecord>(nameof(VaultRelocationRecord.SystemId)) == 29 &&
                OffsetOf<VaultRelocationRecord>(nameof(VaultRelocationRecord.Reserved)) == 30;
        }

        private static bool VerifyTelemetrySnapshot()
        {
            return
                UnsafeUtility.SizeOf<VaultTelemetrySnapshot>() == 64 &&
                OffsetOf<VaultTelemetrySnapshot>(nameof(VaultTelemetrySnapshot.AllocatedBytes)) == 0 &&
                OffsetOf<VaultTelemetrySnapshot>(nameof(VaultTelemetrySnapshot.ArenaBytes)) == 8 &&
                OffsetOf<VaultTelemetrySnapshot>(nameof(VaultTelemetrySnapshot.LastMovedBytes)) == 16 &&
                OffsetOf<VaultTelemetrySnapshot>(nameof(VaultTelemetrySnapshot.ResolutionTicks)) == 24 &&
                OffsetOf<VaultTelemetrySnapshot>(nameof(VaultTelemetrySnapshot.VaultGenerationID)) == 32 &&
                OffsetOf<VaultTelemetrySnapshot>(nameof(VaultTelemetrySnapshot.GenerationMismatchCount)) == 36 &&
                OffsetOf<VaultTelemetrySnapshot>(nameof(VaultTelemetrySnapshot.LastFaultBufferID)) == 40 &&
                OffsetOf<VaultTelemetrySnapshot>(nameof(VaultTelemetrySnapshot.LastFaultHandleGeneration)) == 44 &&
                OffsetOf<VaultTelemetrySnapshot>(nameof(VaultTelemetrySnapshot.LastFaultMetaGeneration)) == 48 &&
                OffsetOf<VaultTelemetrySnapshot>(nameof(VaultTelemetrySnapshot.LastDefragFlags)) == 52 &&
                OffsetOf<VaultTelemetrySnapshot>(nameof(VaultTelemetrySnapshot.Reserved0)) == 53 &&
                OffsetOf<VaultTelemetrySnapshot>(nameof(VaultTelemetrySnapshot.Reserved1)) == 54 &&
                OffsetOf<VaultTelemetrySnapshot>(nameof(VaultTelemetrySnapshot.ResolvedHandleCount)) == 56;
        }

        private static bool VerifyMemoryBudgetEntry()
        {
            return
                UnsafeUtility.SizeOf<VaultMemoryBudgetEntry>() == 32 &&
                OffsetOf<VaultMemoryBudgetEntry>(nameof(VaultMemoryBudgetEntry.SystemHash)) == 0 &&
                OffsetOf<VaultMemoryBudgetEntry>(nameof(VaultMemoryBudgetEntry.BufferID)) == 4 &&
                OffsetOf<VaultMemoryBudgetEntry>(nameof(VaultMemoryBudgetEntry.BudgetBytes)) == 8 &&
                OffsetOf<VaultMemoryBudgetEntry>(nameof(VaultMemoryBudgetEntry.DefragThresholdBytes)) == 16 &&
                OffsetOf<VaultMemoryBudgetEntry>(nameof(VaultMemoryBudgetEntry.Flags)) == 24 &&
                OffsetOf<VaultMemoryBudgetEntry>(nameof(VaultMemoryBudgetEntry.Reserved0)) == 28;
        }

        private static bool VerifyMemoryBlockSnapshot()
        {
            return
                UnsafeUtility.SizeOf<VaultMemoryBlockSnapshot>() == 48 &&
                OffsetOf<VaultMemoryBlockSnapshot>(nameof(VaultMemoryBlockSnapshot.OffsetBytes)) == 0 &&
                OffsetOf<VaultMemoryBlockSnapshot>(nameof(VaultMemoryBlockSnapshot.Bytes)) == 8 &&
                OffsetOf<VaultMemoryBlockSnapshot>(nameof(VaultMemoryBlockSnapshot.BufferKey)) == 16 &&
                OffsetOf<VaultMemoryBlockSnapshot>(nameof(VaultMemoryBlockSnapshot.H8BlockIndex)) == 20 &&
                OffsetOf<VaultMemoryBlockSnapshot>(nameof(VaultMemoryBlockSnapshot.Version)) == 24 &&
                OffsetOf<VaultMemoryBlockSnapshot>(nameof(VaultMemoryBlockSnapshot.Owner)) == 28 &&
                OffsetOf<VaultMemoryBlockSnapshot>(nameof(VaultMemoryBlockSnapshot.LockCount)) == 30 &&
                OffsetOf<VaultMemoryBlockSnapshot>(nameof(VaultMemoryBlockSnapshot.State)) == 32 &&
                OffsetOf<VaultMemoryBlockSnapshot>(nameof(VaultMemoryBlockSnapshot.Flags)) == 33 &&
                OffsetOf<VaultMemoryBlockSnapshot>(nameof(VaultMemoryBlockSnapshot.Reserved0)) == 34 &&
                OffsetOf<VaultMemoryBlockSnapshot>(nameof(VaultMemoryBlockSnapshot.Reserved1)) == 36 &&
                OffsetOf<VaultMemoryBlockSnapshot>(nameof(VaultMemoryBlockSnapshot.Reserved2)) == 40;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
    }
}
#endif
