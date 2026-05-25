using System.Runtime.InteropServices;

namespace Hecton8.Core.Contracts.Signals
{
    internal static class DynamicMusicScalarSignalLayout
    {
        internal const int SignalStrideBytes = 64;
    }

    /// <summary>
    /// Presentation-only dynamic music scalar packet. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DynamicMusicScalarSignalLayout.SignalStrideBytes)]
    public struct DynamicMusicScalarSignal : ISignal
    {
        public const uint LaneHash = 0x44594D55u; // DYMU
        public const uint SourceMusicDirectorHash = 0x4D444952u; // MDIR
        public const uint SourceAdaptiveStemHash = 0x41535445u; // ASTE
        public const uint FlagExternalScalars = 1u << 0;
        public const uint FlagStingerImpulse = 1u << 1;
        public const uint FlagOverrideImpulse = 1u << 2;

        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public float Tension01;
        [FieldOffset(12)] public float DepthMeters;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public float DamageImpulse01;
        [FieldOffset(24)] public float StingerImpulse01;
        [FieldOffset(28)] public float PitchKick01;
        [FieldOffset(32)] public uint SourceHash;
        [FieldOffset(36)] public uint Sequence;
        [FieldOffset(40)] public ulong _pad0;
        [FieldOffset(48)] public ulong _pad1;
        [FieldOffset(56)] public ulong _pad2;
    }
}
