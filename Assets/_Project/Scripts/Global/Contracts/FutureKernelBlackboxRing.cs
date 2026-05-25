using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Hecton8.Global.Contracts
{
    internal static class FutureKernelBlackboxLayout
    {
        internal const int RingStateStrideBytes = 64;
    }

    /// <summary>
    /// Fixed state header for a caller-owned 300-entry future-kernel blackbox ring. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = FutureKernelBlackboxLayout.RingStateStrideBytes)]
    public struct FutureKernelBlackboxRingState64
    {
        [FieldOffset(0)] public ulong WriteCount;
        [FieldOffset(8)] public ulong LastPayloadHash;
        [FieldOffset(16)] public uint Cursor;
        [FieldOffset(20)] public uint Capacity;
        [FieldOffset(24)] public uint LastFrameIndex;
        [FieldOffset(28)] public uint LastSurfaceHash;
        [FieldOffset(32)] public uint LastRejectReason;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public uint DroppedWrites;
        [FieldOffset(44)] public uint Reserved0;
        [FieldOffset(48)] public ulong Reserved1;
        [FieldOffset(56)] public ulong Reserved2;
    }

    /// <summary>
    /// Stateless helpers for writing future-kernel telemetry into owner-provided memory.
    /// </summary>
    public static class FutureKernelBlackboxRing
    {
        public const int StateSizeBytes = FutureKernelBlackboxLayout.RingStateStrideBytes;
        public const uint StateInitializedFlag = 1u << 0;
        public const uint CapacityFaultFlag = 1u << 1;
        public const uint CursorSanitizedFlag = 1u << 2;

        /// <summary>Creates the fixed 300-frame ring state without allocating a ring.</summary>
        public static FutureKernelBlackboxRingState64 CreateState()
        {
            return new FutureKernelBlackboxRingState64
            {
                Capacity = FutureSystemSeamContracts.RequiredBlackboxFrames,
                Flags = StateInitializedFlag
            };
        }

        /// <summary>Validates that the ring state matches the mandated 300-frame capacity.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FutureSeamValidationError ValidateState(in FutureKernelBlackboxRingState64 state)
        {
            return state.Capacity == FutureSystemSeamContracts.RequiredBlackboxFrames
                ? FutureSeamValidationError.None
                : FutureSeamValidationError.InvalidBlackboxCapacity;
        }

        /// <summary>
        /// Appends one entry into a caller-owned ring. The owner decides whether that ring lives in DataVault.
        /// </summary>
        public static bool TryAppend(
            Span<FutureKernelBlackboxEntry64> ring,
            ref FutureKernelBlackboxRingState64 state,
            in FutureKernelBlackboxEntry64 entry)
        {
            if (!PrepareForWrite(ring.Length, ref state))
            {
                state.DroppedWrites++;
                state.Flags |= CapacityFaultFlag;
                return false;
            }

            int cursor = unchecked((int)state.Cursor);
            ring[cursor] = entry;
            state.LastPayloadHash = entry.PayloadHash;
            state.LastFrameIndex = entry.FrameIndex;
            state.LastSurfaceHash = entry.SurfaceHash;
            state.LastRejectReason = entry.RejectReason;
            state.WriteCount++;
            state.Cursor = unchecked((uint)FutureSystemSeamContracts.AdvanceBlackboxCursor(cursor));
            return true;
        }

        /// <summary>Reads the latest written entry from a caller-owned ring.</summary>
        public static bool TryReadLatest(
            ReadOnlySpan<FutureKernelBlackboxEntry64> ring,
            in FutureKernelBlackboxRingState64 state,
            out FutureKernelBlackboxEntry64 entry)
        {
            entry = default;
            if (state.WriteCount == 0UL ||
                state.Capacity != FutureSystemSeamContracts.RequiredBlackboxFrames ||
                ring.Length < FutureSystemSeamContracts.RequiredBlackboxFrames)
            {
                return false;
            }

            uint cursor = state.Cursor;
            if (cursor >= FutureSystemSeamContracts.RequiredBlackboxFrames)
                cursor = 0u;

            int latestIndex = cursor == 0u
                ? FutureSystemSeamContracts.RequiredBlackboxFrames - 1
                : unchecked((int)cursor) - 1;

            entry = ring[latestIndex];
            return true;
        }

        private static bool PrepareForWrite(int ringLength, ref FutureKernelBlackboxRingState64 state)
        {
            if (ringLength < FutureSystemSeamContracts.RequiredBlackboxFrames)
                return false;

            if (state.Capacity == 0u)
            {
                state.Capacity = FutureSystemSeamContracts.RequiredBlackboxFrames;
                state.Flags |= StateInitializedFlag;
            }

            if (state.Capacity != FutureSystemSeamContracts.RequiredBlackboxFrames)
                return false;

            if (state.Cursor >= FutureSystemSeamContracts.RequiredBlackboxFrames)
            {
                state.Cursor = 0u;
                state.Flags |= CursorSanitizedFlag;
            }

            state.Flags |= StateInitializedFlag;
            return true;
        }
    }
}
