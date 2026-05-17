using System.Runtime.InteropServices;
using Hecton8.World;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts.Signals
{
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]
    public struct PlayerFootstepSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float Intensity01;
        [FieldOffset(12)] public byte Flags;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]
    public struct PlayerWaterSplashSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float Intensity01;
        [FieldOffset(12)] public float SurfaceY;
        [FieldOffset(16)] public float VerticalSpeed;
        [FieldOffset(20)] public byte IsSubmerged;
        [FieldOffset(21)] public byte Flags;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 96)]
    public struct WaterTransitionSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float Intensity01;
        [FieldOffset(12)] public float SurfaceY;
        [FieldOffset(16)] public float VerticalSpeed;
        [FieldOffset(20)] public byte Kind;
        [FieldOffset(21)] public byte IsSubmerged;
        [FieldOffset(22)] public ushort Flags;
        [FieldOffset(24)] public float3 RuntimePosition;
        [FieldOffset(40)] public AbsoluteUniversePosition AbsolutePosition;
        [FieldOffset(88)] public ulong Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]
    public struct PlayerExhaleSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public byte Flags;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]
    public struct PlayerSprintStateSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public byte IsSprinting;
        [FieldOffset(9)] public byte Flags;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 16)]
    public struct PlayerFatalPressureSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float Intensity01;
        [FieldOffset(12)] public byte Flags;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]
    public struct PlayerTransportBailoutSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float Severity01;
        [FieldOffset(12)] public float3 WorldImpulse;
        [FieldOffset(24)] public byte Flags;
    }
}
