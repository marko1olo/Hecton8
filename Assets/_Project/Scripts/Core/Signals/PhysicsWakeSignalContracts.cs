using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts.Signals
{
    internal static class PhysicsWakeSignalLayout
    {
        internal const int WakeRequestSignalStrideBytes = 64;
    }

    /// <summary>
    /// Deferred physics-culling wake pulse. Absolute AUP is stored as double3 to avoid
    /// truncating a 100 km world-space event origin to float before culling math.
    /// Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = PhysicsWakeSignalLayout.WakeRequestSignalStrideBytes)]
    public struct WakeRequestSignal : ISignal
    {
        [FieldOffset(0)] public double3 OriginAup;
        [FieldOffset(24)] public float RadiusMeters;
        [FieldOffset(28)] public uint SourceHash;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public byte Flags;
        [FieldOffset(37)] private byte _pad0;
        [FieldOffset(38)] private byte _pad1;
        [FieldOffset(39)] private byte _pad2;
        [FieldOffset(40)] private byte _pad3;
        [FieldOffset(41)] private byte _pad4;
        [FieldOffset(42)] private byte _pad5;
        [FieldOffset(43)] private byte _pad6;
        [FieldOffset(44)] private byte _pad7;
        [FieldOffset(45)] private byte _pad8;
        [FieldOffset(46)] private byte _pad9;
        [FieldOffset(47)] private byte _pad10;
        [FieldOffset(48)] private ulong _pad11;
        [FieldOffset(56)] private ulong _pad12;
    }
}
