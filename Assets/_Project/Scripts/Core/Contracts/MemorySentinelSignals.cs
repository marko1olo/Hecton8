using System.Runtime.InteropServices;

namespace Hecton8.Core.Contracts.Signals
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct MemoryDesyncSignal : ISignal
    {
        public const uint FlagRollbackApplied = 1u << 0;
        public const uint FlagFatal = 1u << 1;
        public const uint FlagTeleport = 1u << 2;
        public const uint FlagCritical = 1u << 3;
        public const uint FlagPointerMismatch = 1u << 4;

        [FieldOffset(0)] public uint TargetHash;
        [FieldOffset(4)] public uint ExpectedHash;
        [FieldOffset(8)] public uint CalculatedHash;
        [FieldOffset(12)] public uint StoredHash;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public int BufferId;
        [FieldOffset(28)] public int ByteLength;
        [FieldOffset(32)] public ulong TargetMemoryFingerprint;
        [FieldOffset(40)] public ulong FullHash64;
        [FieldOffset(48)] public float Severity01;
        [FieldOffset(52)] public float GlobalQualityWeight;
        [FieldOffset(56)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct HashDeltaUpdateSignal : ISignal
    {
        public const uint FlagLegalInventoryChange = 1u << 0;
        public const uint FlagLegalAupChange = 1u << 1;
        public const uint FlagRefreshRollbackMirror = 1u << 2;

        [FieldOffset(0)] public uint TargetHash;
        [FieldOffset(4)] public uint ExpectedHash;
        [FieldOffset(8)] public uint StoredHash;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public int BufferId;
        [FieldOffset(20)] public int ByteOffset;
        [FieldOffset(24)] public int ByteLength;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public ulong TargetMemoryFingerprint;
        [FieldOffset(40)] public ulong SourceLogicHash;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct MemorySentinelRollbackSignal : ISignal
    {
        [FieldOffset(0)] public uint TargetHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint ExpectedHash;
        [FieldOffset(12)] public uint CorrectedHash;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public int BufferId;
        [FieldOffset(24)] public int ByteLength;
        [FieldOffset(28)] public int RollbackByteOffset;
        [FieldOffset(32)] public ulong TargetMemoryFingerprint;
        [FieldOffset(40)] public ulong _pad0;
        [FieldOffset(48)] public ulong _pad1;
        [FieldOffset(56)] public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct ModdedGameMaskSignal : ISignal
    {
        public const uint FlagLifecycleShutdown = 1u << 0;
        public const uint FlagEditorOverride = 1u << 1;

        [FieldOffset(0)] public uint ModdedGameMask;
        [FieldOffset(4)] public uint ActiveModCount;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint SourceHash;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public uint _pad0;
        [FieldOffset(24)] public ulong _pad1;
        [FieldOffset(32)] public ulong _pad2;
        [FieldOffset(40)] public ulong _pad3;
        [FieldOffset(48)] public ulong _pad4;
        [FieldOffset(56)] public ulong _pad5;
    }
}
