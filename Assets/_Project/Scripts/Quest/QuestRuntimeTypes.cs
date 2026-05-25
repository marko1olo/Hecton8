using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Quest
{
    internal static class QuestRuntimeLayout
    {
        public const int SignalPayloadStrideBytes = 64;
        public const int BitAddressStrideBytes = 16;
        public const int PrerequisiteDescriptorStrideBytes = 16;
        public const int NodeDescriptorStrideBytes = 128;
        public const int RevertDescriptorStrideBytes = 32;
        public const int SaveHeaderStrideBytes = 64;
    }

    internal enum QuestSignalKind : byte
    {
        None = 0,
        ItemCollected = 1,
        DepthReached = 2,
        BiomeEntered = 3,
        DiscoveryMade = 4,
        AudioLogFound = 5,
        EclipseStarted = 6,
        SignalDecoded = 7,
        ItemLost = 8,
        CraftCompleted = 9
    }

    internal enum QuestStateBand : byte
    {
        Quest = 0,
        Item = 1,
        Location = 2,
        Narrative = 3,
        Phase = 4,
        EntityDestroy = 5,
        Deadlock = 6
    }

    internal enum QuestTransitionType : byte
    {
        Activate = 0,
        Complete = 1,
        Revert = 2
    }

    public enum QuestPhaseGateType : byte
    {
        None = 0,
        Abyssal = 1,
        Thermal = 2
    }

    [Flags]
    internal enum QuestSignalContextFlags : uint
    {
        None = 0u,
        ThermalPhase = 1u << 0,
        AbyssalPhase = 1u << 1
    }

    [StructLayout(LayoutKind.Explicit, Size = QuestRuntimeLayout.SignalPayloadStrideBytes)]
    internal struct QuestSignalPayload
    {
        [FieldOffset(0)]
        public double Timestamp;

        [FieldOffset(8)]
        public float3 Position;

        [FieldOffset(20)]
        public uint EntityHash;

        [FieldOffset(24)]
        public uint ItemId;

        [FieldOffset(28)]
        public uint Flags;

        [FieldOffset(32)]
        public float NumericValue;

        [FieldOffset(36)]
        public ushort EventType;

        [FieldOffset(38)]
        public ushort SubType;

        [FieldOffset(40)]
        private ulong _pad0;

        [FieldOffset(48)]
        private ulong _pad1;

        [FieldOffset(56)]
        private ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = QuestRuntimeLayout.BitAddressStrideBytes)]
    internal struct QuestBitAddress
    {
        [FieldOffset(0)]
        public int WordIndex;

        [FieldOffset(4)]
        public uint BitMask;

        [FieldOffset(8)]
        public uint FlagId;

        [FieldOffset(12)]
        private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = QuestRuntimeLayout.PrerequisiteDescriptorStrideBytes)]
    internal struct QuestPrerequisiteDescriptor
    {
        [FieldOffset(0)]
        public int StateWordIndex;

        [FieldOffset(4)]
        public uint RequiredMask;

        [FieldOffset(8)]
        private ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = QuestRuntimeLayout.NodeDescriptorStrideBytes)]
    internal struct QuestNodeDescriptor
    {
        [FieldOffset(0)]
        public uint QuestHash;

        [FieldOffset(4)]
        public uint PayloadHash;

        [FieldOffset(8)]
        public uint PrereqMask;

        [FieldOffset(12)]
        public uint CompletionFlagID;

        [FieldOffset(16)]
        public uint FailureFlagID;

        [FieldOffset(20)]
        public uint RevertFlagID;

        [FieldOffset(24)]
        public uint PhaseGate;

        [FieldOffset(28)]
        public uint ActiveFlagID;

        [FieldOffset(32)]
        public uint CriticalItemHash;

        [FieldOffset(36)]
        public int PrereqStartIndex;

        [FieldOffset(40)]
        public float RequiredValue;

        [FieldOffset(44)]
        public uint ActiveMask;

        [FieldOffset(48)]
        public uint CompletedMask;

        [FieldOffset(52)]
        public uint SetMask;

        [FieldOffset(56)]
        public uint ClearMask;

        [FieldOffset(60)]
        public int QuestIndex;

        [FieldOffset(64)]
        public int ActiveWordIndex;

        [FieldOffset(68)]
        public int CompletedWordIndex;

        [FieldOffset(72)]
        public int SetWordIndex;

        [FieldOffset(76)]
        public int ClearWordIndex;

        [FieldOffset(80)]
        public ushort PrereqWordIndex;

        [FieldOffset(82)]
        public ushort ReservedWordIndex;

        [FieldOffset(84)]
        public byte PrereqCount;

        [FieldOffset(85)]
        public byte SignalKind;

        [FieldOffset(86)]
        public byte TransitionType;

        [FieldOffset(87)]
        public byte Reserved;

        [FieldOffset(88)]
        private ulong _pad0;

        [FieldOffset(96)]
        private ulong _pad1;

        [FieldOffset(104)]
        private ulong _pad2;

        [FieldOffset(112)]
        private ulong _pad3;

        [FieldOffset(120)]
        private ulong _pad4;
    }

    internal readonly struct QuestRuntimeResult
    {
        public QuestRuntimeResult(int questIndex, bool completed, QuestTransitionType transitionType)
        {
            QuestIndex = questIndex;
            Completed = completed ? (byte)1 : (byte)0;
            TransitionType = transitionType;
        }

        public readonly int QuestIndex;
        public readonly byte Completed;
        public readonly QuestTransitionType TransitionType;
    }

    [StructLayout(LayoutKind.Explicit, Size = QuestRuntimeLayout.RevertDescriptorStrideBytes)]
    internal struct QuestRevertDescriptor
    {
        [FieldOffset(0)]
        public uint CriticalItemHash;

        [FieldOffset(4)]
        public uint EntityDestroyFlagId;

        [FieldOffset(8)]
        public uint DeadlockFlagId;

        [FieldOffset(12)]
        public uint ActiveFlagId;

        [FieldOffset(16)]
        public uint CompletedFlagId;

        [FieldOffset(20)]
        public uint RespawnEventHash;

        [FieldOffset(24)]
        public int QuestIndex;

        [FieldOffset(28)]
        private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = QuestRuntimeLayout.SaveHeaderStrideBytes)]
    public unsafe struct QuestSaveHeader
    {
        public const uint HeaderMagic = 0x48514753u;
        public const uint CurrentSchemaVersion = 1u;

        [FieldOffset(0)]
        public uint Magic;

        [FieldOffset(4)]
        public uint Version;

        [FieldOffset(8)]
        public uint FlagCount;

        [FieldOffset(12)]
        public uint Checksum;

        [FieldOffset(16)]
        public double Timestamp;

        [FieldOffset(24)]
        public fixed uint Reserved[10];

        public void WriteSchemaVersion()
        {
            fixed (uint* reserved = Reserved)
                reserved[0] = CurrentSchemaVersion;
        }

        public readonly uint ReadSchemaVersion()
        {
            fixed (uint* reserved = Reserved)
                return reserved[0];
        }
    }

    internal static unsafe class QuestFlagHashKernel
    {
        public static uint ComputeStableHash(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? 0u
                : ComputeStableHash(value.AsSpan());
        }

        public static uint ComputeStableHash(ReadOnlySpan<char> value)
        {
            if (value.Length <= 0)
                return 0u;

            fixed (char* valuePtr = value)
                return ComputeStableHash((ushort*)valuePtr, value.Length);
        }

        public static uint ComputeStableHash(ushort* value, int length)
        {
            if (value == null || length <= 0)
                return 0u;

            unchecked
            {
                uint hash = Hecton.Localization.LocHash.FnvOffsetBasis;
                for (int i = 0; i < length; i++)
                {
                    ushort current = value[i];
                    hash ^= (byte)current;
                    hash *= Hecton.Localization.LocHash.FnvPrime;
                    hash ^= (byte)(current >> 8);
                    hash *= Hecton.Localization.LocHash.FnvPrime;
                }

                return hash;
            }
        }
    }
}
