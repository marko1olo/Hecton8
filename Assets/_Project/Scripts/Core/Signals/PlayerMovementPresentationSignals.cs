using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts.Signals
{
    internal static class PlayerMovementPresentationSignalLayout
    {
        public const int FootstepStrideBytes = 32;
        public const int WaterSplashStrideBytes = 32;
        public const int PresentationAupStrideBytes = 48;
        public const int WaterTransitionStrideBytes = 128;
        public const int ExhaleStrideBytes = 16;
        public const int SprintStateStrideBytes = 16;
        public const int FatalPressureStrideBytes = 16;
        public const int TransportBailoutStrideBytes = 32;
    }

    [StructLayout(LayoutKind.Explicit, Size = PlayerMovementPresentationSignalLayout.FootstepStrideBytes)]
    public struct PlayerFootstepSignal : ISignal
    {
        public const int ExpectedCapacity = 16;
        public const int MaxFrameSignals = 32;
        public const int LowTierFrameSignals = 8;
        public const uint LaneHash = 0x5CFCFEBEu; // FNV32("PlayerFootstepSignal")

        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float Intensity01;
        [FieldOffset(12)] public byte Flags;
        [FieldOffset(13)] private byte _pad0;
        [FieldOffset(14)] private byte _pad1;
        [FieldOffset(15)] private byte _pad2;
        [FieldOffset(16)] private byte _pad3;
        [FieldOffset(17)] private byte _pad4;
        [FieldOffset(18)] private byte _pad5;
        [FieldOffset(19)] private byte _pad6;
        [FieldOffset(20)] private byte _pad7;
        [FieldOffset(21)] private byte _pad8;
        [FieldOffset(22)] private byte _pad9;
        [FieldOffset(23)] private byte _pad10;
        [FieldOffset(24)] private byte _pad11;
        [FieldOffset(25)] private byte _pad12;
        [FieldOffset(26)] private byte _pad13;
        [FieldOffset(27)] private byte _pad14;
        [FieldOffset(28)] private byte _pad15;
        [FieldOffset(29)] private byte _pad16;
        [FieldOffset(30)] private byte _pad17;
        [FieldOffset(31)] private byte _pad18;
    }

    [StructLayout(LayoutKind.Explicit, Size = PlayerMovementPresentationSignalLayout.WaterSplashStrideBytes)]
    public struct PlayerWaterSplashSignal : ISignal
    {
        public const int ExpectedCapacity = 8;
        public const int MaxFrameSignals = 16;
        public const int LowTierFrameSignals = 4;
        public const uint LaneHash = 0x6411655Cu; // FNV32("PlayerWaterSplashSignal")

        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float Intensity01;
        [FieldOffset(12)] public float SurfaceY;
        [FieldOffset(16)] public float VerticalSpeed;
        [FieldOffset(20)] public byte IsSubmerged;
        [FieldOffset(21)] public byte Flags;
        [FieldOffset(22)] private byte _pad0;
        [FieldOffset(23)] private byte _pad1;
        [FieldOffset(24)] private byte _pad2;
        [FieldOffset(25)] private byte _pad3;
        [FieldOffset(26)] private byte _pad4;
        [FieldOffset(27)] private byte _pad5;
        [FieldOffset(28)] private byte _pad6;
        [FieldOffset(29)] private byte _pad7;
        [FieldOffset(30)] private byte _pad8;
        [FieldOffset(31)] private byte _pad9;
    }

    [StructLayout(LayoutKind.Explicit, Size = PlayerMovementPresentationSignalLayout.PresentationAupStrideBytes)]
    public struct PlayerPresentationAup48
    {
        [FieldOffset(0)] public long GridX;
        [FieldOffset(8)] public long GridY;
        [FieldOffset(16)] public long GridZ;
        [FieldOffset(24)] public float LocalX;
        [FieldOffset(28)] public float LocalY;
        [FieldOffset(32)] public float LocalZ;
        [FieldOffset(36)] private uint _pad0;
        [FieldOffset(40)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = PlayerMovementPresentationSignalLayout.WaterTransitionStrideBytes)]
    public struct WaterTransitionSignal : ISignal
    {
        public const int ExpectedCapacity = 8;
        public const int MaxFrameSignals = 16;
        public const int LowTierFrameSignals = 8;
        public const uint LaneHash = 0x3EF2CD93u; // FNV32("WaterTransitionSignal")

        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float Intensity01;
        [FieldOffset(12)] public float SurfaceY;
        [FieldOffset(16)] public float VerticalSpeed;
        [FieldOffset(20)] public byte Kind;
        [FieldOffset(21)] public byte IsSubmerged;
        [FieldOffset(22)] public ushort Flags;
        [FieldOffset(24)] public float3 RuntimePosition;
        [FieldOffset(36)] private byte _pad0;
        [FieldOffset(37)] private byte _pad1;
        [FieldOffset(38)] private byte _pad2;
        [FieldOffset(39)] private byte _pad3;
        [FieldOffset(40)] public PlayerPresentationAup48 AbsolutePosition;
        [FieldOffset(88)] public ulong Reserved;
        [FieldOffset(96)] public ulong Reserved1;
        [FieldOffset(104)] public ulong Reserved2;
        [FieldOffset(112)] public ulong Reserved3;
        [FieldOffset(120)] public ulong Reserved4;
    }

    [StructLayout(LayoutKind.Explicit, Size = PlayerMovementPresentationSignalLayout.ExhaleStrideBytes)]
    public struct PlayerExhaleSignal : ISignal
    {
        public const int ExpectedCapacity = 16;
        public const int MaxFrameSignals = 32;
        public const int LowTierFrameSignals = 8;
        public const uint LaneHash = 0x0C6B5471u; // FNV32("PlayerExhaleSignal")

        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public byte Flags;
        [FieldOffset(9)] private byte _pad0;
        [FieldOffset(10)] private byte _pad1;
        [FieldOffset(11)] private byte _pad2;
        [FieldOffset(12)] private byte _pad3;
        [FieldOffset(13)] private byte _pad4;
        [FieldOffset(14)] private byte _pad5;
        [FieldOffset(15)] private byte _pad6;
    }

    [StructLayout(LayoutKind.Explicit, Size = PlayerMovementPresentationSignalLayout.SprintStateStrideBytes)]
    public struct PlayerSprintStateSignal : ISignal
    {
        public const int ExpectedCapacity = 8;
        public const int MaxFrameSignals = 16;
        public const int LowTierFrameSignals = 4;
        public const uint LaneHash = 0x0141C365u; // FNV32("PlayerSprintStateSignal")

        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public byte IsSprinting;
        [FieldOffset(9)] public byte Flags;
        [FieldOffset(10)] private byte _pad0;
        [FieldOffset(11)] private byte _pad1;
        [FieldOffset(12)] private byte _pad2;
        [FieldOffset(13)] private byte _pad3;
        [FieldOffset(14)] private byte _pad4;
        [FieldOffset(15)] private byte _pad5;
    }

    [StructLayout(LayoutKind.Explicit, Size = PlayerMovementPresentationSignalLayout.FatalPressureStrideBytes)]
    public struct PlayerFatalPressureSignal : ISignal
    {
        public const int ExpectedCapacity = 8;
        public const int MaxFrameSignals = 16;
        public const int LowTierFrameSignals = 4;
        public const uint LaneHash = 0x7F048B59u; // FNV32("PlayerFatalPressureSignal")

        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float Intensity01;
        [FieldOffset(12)] public byte Flags;
        [FieldOffset(13)] private byte _pad0;
        [FieldOffset(14)] private byte _pad1;
        [FieldOffset(15)] private byte _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = PlayerMovementPresentationSignalLayout.TransportBailoutStrideBytes)]
    public struct PlayerTransportBailoutSignal : ISignal
    {
        public const int ExpectedCapacity = 4;
        public const int MaxFrameSignals = 8;
        public const int LowTierFrameSignals = 2;
        public const uint LaneHash = 0xDD8B5153u; // FNV32("PlayerTransportBailoutSignal")

        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float Severity01;
        [FieldOffset(12)] public float3 WorldImpulse;
        [FieldOffset(24)] public byte Flags;
        [FieldOffset(25)] private byte _pad0;
        [FieldOffset(26)] private byte _pad1;
        [FieldOffset(27)] private byte _pad2;
        [FieldOffset(28)] private byte _pad3;
        [FieldOffset(29)] private byte _pad4;
        [FieldOffset(30)] private byte _pad5;
        [FieldOffset(31)] private byte _pad6;
    }
}
