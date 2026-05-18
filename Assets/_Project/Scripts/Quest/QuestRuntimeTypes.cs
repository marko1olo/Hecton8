using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Quest
{
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

    [StructLayout(LayoutKind.Sequential)]
    internal struct QuestSignalPayload
    {
        public uint EntityHash;
        public ushort EventType;
        public ushort SubType;
        public float3 Position;
        public uint ItemId;
        public double Timestamp;
        public uint Flags;
        public float NumericValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct QuestBitAddress
    {
        public int WordIndex;
        public uint BitMask;
        public uint FlagId;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct QuestPrerequisiteDescriptor
    {
        public int StateWordIndex;
        public uint RequiredMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct QuestNodeDescriptor
    {
        public uint QuestHash;
        public uint PayloadHash;
        public uint PrereqMask;
        public uint CompletionFlagID;
        public uint FailureFlagID;
        public uint RevertFlagID;
        public uint PhaseGate;
        public uint ActiveFlagID;
        public uint CriticalItemHash;
        public int PrereqStartIndex;
        public ushort PrereqWordIndex;
        public ushort ReservedWordIndex;
        public float RequiredValue;
        public uint ActiveMask;
        public uint CompletedMask;
        public uint SetMask;
        public uint ClearMask;
        public byte PrereqCount;
        public byte SignalKind;
        public byte TransitionType;
        public byte Reserved;
        public int QuestIndex;
        public int ActiveWordIndex;
        public int CompletedWordIndex;
        public int SetWordIndex;
        public int ClearWordIndex;
    }

    internal readonly struct QuestRuntimeResult
    {
        public QuestRuntimeResult(int questIndex, bool completed, QuestTransitionType transitionType)
        {
            QuestIndex = questIndex;
            Completed = completed;
            TransitionType = transitionType;
        }

        public int QuestIndex { get; }
        public bool Completed { get; }
        public QuestTransitionType TransitionType { get; }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct QuestRevertDescriptor
    {
        public uint CriticalItemHash;
        public uint EntityDestroyFlagId;
        public uint DeadlockFlagId;
        public uint ActiveFlagId;
        public uint CompletedFlagId;
        public uint RespawnEventHash;
        public int QuestIndex;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public unsafe struct QuestSaveHeader
    {
        public const uint HeaderMagic = 0x48514753u;
        public const uint CurrentSchemaVersion = 1u;

        public uint Magic;
        public uint Version;
        public uint FlagCount;
        public uint Checksum;
        public double Timestamp;
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
