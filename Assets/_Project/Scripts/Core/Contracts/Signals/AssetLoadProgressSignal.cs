using System.Runtime.InteropServices;

namespace Hecton8.Core.Contracts.Signals
{
    internal static class AssetLoadProgressSignalLayout
    {
        internal const int SignalStrideBytes = 64;
    }

    /// <summary>
    /// Allocation-free asset load progress packet for streaming diagnostics. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = AssetLoadProgressSignalLayout.SignalStrideBytes)]
    public struct AssetLoadProgressSignal : ISignal
    {
        public const int ExpectedCapacity = 128;
        public const int MaxFrameSignals = 128;
        public const int LowTierFrameSignals = 32;
        public const uint LaneHash = 0x4155504Cu; // AUPL

        public const byte StageQueued = 1;
        public const byte StageGranted = 2;
        public const byte StageCompleted = 3;
        public const byte StageFailed = 4;
        public const byte StageCancelled = 5;

        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint AssetKey;
        [FieldOffset(8)] public long EstimatedBytes;
        [FieldOffset(16)] public int RequestId;
        [FieldOffset(20)] public uint UploadBudgetMb;
        [FieldOffset(24)] public uint GrantedFrameMb;
        [FieldOffset(28)] public byte Stage;
        [FieldOffset(29)] public byte Priority;
        [FieldOffset(30)] public byte Flags;
        [FieldOffset(31)] public byte _pad0;
        [FieldOffset(32)] public ulong _pad1;
        [FieldOffset(40)] public ulong _pad2;
        [FieldOffset(48)] public ulong _pad3;
        [FieldOffset(56)] public ulong _pad4;
    }
}
