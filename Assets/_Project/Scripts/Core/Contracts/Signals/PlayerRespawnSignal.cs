using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts.Signals
{
    public static class PlayerRespawnSignalPhase
    {
        public const byte Request = 1;
        public const byte Committed = 2;
    }

    public static class PlayerRespawnSignalFlags
    {
        public const uint Requested = 1u << 0;
        public const uint Committed = 1u << 1;
        public const uint SuspendCollision = 1u << 2;
        public const uint MockMedicalBay = 1u << 3;
        public const uint FallbackLifepod = 1u << 4;
        public const uint InvalidTargetAup = 1u << 5;
        public const uint PenaltyApplied = 1u << 6;
        public const uint InvalidDeathAup = 1u << 7;
    }

    /// <summary>
    /// Cold-lane death reconciliation packet. Size: 128 bytes; two double3 AUPs require explicit tail padding.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct PlayerRespawnSignal : ISignal
    {
        public const uint LaneHash = 0x5253504Eu; // RSPN
        public const int ExpectedCapacity = 8;
        public const int MaxFrameSignals = 16;
        public const int LowTierFrameSignals = 4;
        public const byte MaxSuspendCollisionFrames = 4;

        [FieldOffset(0)] public double3 DeathAUP;
        [FieldOffset(24)] public double3 RespawnAUP;
        [FieldOffset(48)] public uint PlayerHash;
        [FieldOffset(52)] public uint MedicalBayHashID;
        [FieldOffset(56)] public uint DamageHash;
        [FieldOffset(60)] public uint Frame;
        [FieldOffset(64)] public uint Sequence;
        [FieldOffset(68)] public uint Flags;
        [FieldOffset(72)] public byte Phase;
        [FieldOffset(73)] public byte SuspendCollisionFrames;
        [FieldOffset(74)] public ushort Reserved0;
        [FieldOffset(76)] public uint Reserved1;
        [FieldOffset(80)] public ulong Reserved2;
        [FieldOffset(88)] public ulong Reserved3;
        [FieldOffset(96)] public ulong Reserved4;
        [FieldOffset(104)] public ulong Reserved5;
        [FieldOffset(112)] public ulong Reserved6;
        [FieldOffset(120)] public ulong Reserved7;
    }
}
