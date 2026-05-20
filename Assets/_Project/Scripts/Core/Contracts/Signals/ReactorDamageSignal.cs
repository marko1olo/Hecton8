// ============================================================================
// HECTON-8 - ReactorDamageSignal.cs
// Core contract payload for reactor-driven atmosphere gas leaks.
// ============================================================================

using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>Reactor gas leak payload consumed by the base atmosphere logistics lane. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ReactorDamageSignal : ISignal
    {
        [FieldOffset(0)] public double3 DamageAup;
        [FieldOffset(24)] public uint ReactorHash;
        [FieldOffset(28)] public float Damage01;
        [FieldOffset(32)] public float ToxinLeak01;
        [FieldOffset(36)] public byte Flags;
        [FieldOffset(37)] public byte _pad0;
        [FieldOffset(38)] public ushort _pad1;
        [FieldOffset(40)] public ulong _pad2;
        [FieldOffset(48)] public ulong _pad3;
        [FieldOffset(56)] public ulong _pad4;
    }
}
