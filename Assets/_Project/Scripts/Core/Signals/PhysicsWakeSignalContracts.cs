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
        [FieldOffset(48)] private byte _pad11;
        [FieldOffset(49)] private byte _pad12;
        [FieldOffset(50)] private byte _pad13;
        [FieldOffset(51)] private byte _pad14;
        [FieldOffset(52)] private byte _pad15;
        [FieldOffset(53)] private byte _pad16;
        [FieldOffset(54)] private byte _pad17;
        [FieldOffset(55)] private byte _pad18;
        [FieldOffset(56)] private byte _pad19;
        [FieldOffset(57)] private byte _pad20;
        [FieldOffset(58)] private byte _pad21;
        [FieldOffset(59)] private byte _pad22;
        [FieldOffset(60)] private byte _pad23;
        [FieldOffset(61)] private byte _pad24;
        [FieldOffset(62)] private byte _pad25;
        [FieldOffset(63)] private byte _pad26;
    }
}
