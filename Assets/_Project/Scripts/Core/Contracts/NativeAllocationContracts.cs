using System.Runtime.InteropServices;
using Unity.Collections;

namespace Hecton8.Core
{
    /// <summary>
    /// Expected lifetime for persistent native allocations registered with the native memory sentinel.
    /// </summary>
    public enum NativeAllocationLifetime : byte
    {
        Scene = 0,
        Session = 1,
        Permanent = 2,
        TransientArena = 3,
        Temp = 4,
        TempJob = 5
    }

    /// <summary>
    /// Blittable source descriptor for deterministic replay snapshot capture.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct NativeAllocationSnapshotSource
    {
        /// <summary>Internal native address value copied only into the replay recorder.</summary>
        [FieldOffset(0)]
        public ulong SourcePointerValue;

        /// <summary>Allocation byte count.</summary>
        [FieldOffset(8)]
        public long Bytes;

        /// <summary>Stable owner hash.</summary>
        [FieldOffset(16)]
        public uint OwnerHash;

        /// <summary>Stable label hash.</summary>
        [FieldOffset(20)]
        public uint LabelHash;

        /// <summary>Frame where the allocation was registered.</summary>
        [FieldOffset(24)]
        public int AllocationFrame;

        /// <summary>Stored <see cref="NativeAllocationLifetime"/> value.</summary>
        [FieldOffset(28)]
        public byte Lifetime;

        /// <summary>Stored <see cref="Allocator"/> value.</summary>
        [FieldOffset(29)]
        public byte Allocator;

        /// <summary>Reserved padding for fixed 32-byte layout.</summary>
        [FieldOffset(30)]
        public ushort Reserved;
    }
}
