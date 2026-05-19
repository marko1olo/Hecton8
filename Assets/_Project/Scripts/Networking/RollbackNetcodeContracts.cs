using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Determinism;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Networking
{
    public static class RollbackNetcodeConstants
    {
        public const int StateRingFrameCapacity = 256;
        public const int InputRingCapacity = 512;
        public const int TelemetryFrameCapacity = 300;
        public const int VisualStateCapacity = 16;
        public const int VisualHistoryCapacity = VisualStateCapacity;
        public const int CommandCapacity = 8;
        public const int MaxRollbackFrames = 120;
        public const int MaxRigidbodyAups = 256;
        public const int MaxPlayerStates = 4;
        public const int MaxEntityAups = 512;
        public const int MaxEntityVelocities = 512;
        public const int MaxRoomWaterLevels = 256;
        public const int MaxEntityFlags = 512;
        public const int MaxEntityItems = 512;
        public const int MaxInventoryItems = 512;
        public const int MaxQuestMasks = 128;
        public const int MaxPredatorChosenStates = 256;
        public const int MerkleLeafCapacity = 16;
        public const int MerkleBranchCapacity = 8;
        public const int MerkleBranchNodeStart = MerkleLeafCapacity;
        public const int MerkleRootNodeIndex = 31;
        public const int MerkleNodeCapacity = 32;
        public const int LeafDeltaCapacity = 16;
        public const int MockJitterPacketCapacity = 128;
        public const int CsvScratchBytes = 4096;
        public const int SnapshotHeaderBytes = 128;
        public const int DesyncHashCadenceFrames = 60;
        public const float DefaultVisualInterpolationSeconds = 0.05f;
        public const float DefaultPredictionAggressiveness = 0.6f;
        public const float DefaultLookRollbackMinQuality = 0.55f;
        public const float ResimDumpThresholdMs = 5f;
        public const byte PhaseSimulation = 1 << 0;
        public const byte PhasePostSimulation = 1 << 1;

        public static int ResolveSnapshotPayloadBytes()
        {
            int bytes = 0;
            bytes += UnsafeUtility.SizeOf<AbsoluteUniversePosition>() * MaxRigidbodyAups;
            bytes += UnsafeUtility.SizeOf<LockstepPlayerKinematicState>() * MaxPlayerStates;
            bytes += UnsafeUtility.SizeOf<AbsoluteUniversePosition>() * MaxEntityAups;
            bytes += UnsafeUtility.SizeOf<float3>() * MaxEntityVelocities;
            bytes += UnsafeUtility.SizeOf<float>() * MaxRoomWaterLevels;
            bytes += UnsafeUtility.SizeOf<uint>() * MaxEntityFlags;
            bytes += UnsafeUtility.SizeOf<uint>() * MaxEntityItems;
            bytes += UnsafeUtility.SizeOf<ushort>() * MaxEntityItems;
            bytes += UnsafeUtility.SizeOf<uint>() * MaxInventoryItems;
            bytes += UnsafeUtility.SizeOf<int>() * MaxInventoryItems;
            bytes += UnsafeUtility.SizeOf<float>() * MaxInventoryItems;
            bytes += UnsafeUtility.SizeOf<ulong>() * MaxQuestMasks;
            bytes += UnsafeUtility.SizeOf<byte>() * MaxPredatorChosenStates;
            return bytes;
        }

        public static int ResolveSnapshotStrideBytes()
        {
            return Align8(SnapshotHeaderBytes + ResolveSnapshotPayloadBytes());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Align8(int value)
        {
            return (value + 7) & ~7;
        }
    }

    public static class RollbackNetcodeVault
    {
        public const SystemID OwnerSystem = SystemID.CoreDeterminism;
        public const BufferID StateRingBuffer = (BufferID)70750;
        public const BufferID FrameSnapshots = (BufferID)70751;
        public const BufferID RuntimeState = (BufferID)70752;
        public const BufferID RemoteInputRing = (BufferID)70753;
        public const BufferID TickCommands = (BufferID)70754;
        public const BufferID VisualStates = (BufferID)70755;
        public const BufferID TelemetryRing = (BufferID)70756;
        public const BufferID Tuning = (BufferID)70757;
        public const BufferID AudioSuppression = (BufferID)70758;
        public const BufferID CsvScratch = (BufferID)70759;
        public const BufferID LatencyProfile = (BufferID)70769;
        public const BufferID MerkleNodes = (BufferID)70770;
        public const BufferID MerkleLeafDescriptors = (BufferID)70771;
        public const BufferID LeafDeltaRecords = (BufferID)70772;
        public const BufferID InputJournalRing = (BufferID)70773;
        public const BufferID MockJitterPackets = (BufferID)70774;
        public const BufferID MockJitterState = (BufferID)70775;
        public const BufferID VisualHistory = (BufferID)70776;
    }

    public static class RollbackNetcodeFlags
    {
        public const uint None = 0u;
        public const uint Active = 1u << 0;
        public const uint ServerMode = 1u << 1;
        public const uint ClientMode = 1u << 2;
        public const uint RollbackRequired = 1u << 3;
        public const uint Resimulating = 1u << 4;
        public const uint HashMismatch = 1u << 5;
        public const uint DesyncPaused = 1u << 6;
        public const uint EmergencyMock = 1u << 7;
        public const uint MissingInputJournal = 1u << 8;
        public const uint ModQuarantine = 1u << 9;
        public const uint SnapshotMissing = 1u << 10;
        public const uint ResimBudgetExceeded = 1u << 11;
        public const uint FullStateOverwriteRequested = 1u << 12;
        public const uint BranchProbeRequested = 1u << 13;
        public const uint HardResyncRequired = 1u << 14;
        public const uint MockJitterActive = 1u << 15;
        public const int MismatchShift = 16;
        public const uint MismatchMask = 0x00070000u;
    }

    public static class RemoteInputFlags
    {
        public const uint None = 0u;
        public const uint Received = 1u << 0;
        public const uint Predicted = 1u << 1;
        public const uint ModQuarantined = 1u << 2;
    }

    public static class InputMismatchFlags
    {
        public const uint None = 0u;
        public const uint Button = 1u << 0;
        public const uint Move = 1u << 1;
        public const uint Look = 1u << 2;
    }

    public static class RollbackMerkleFlags
    {
        public const uint None = 0u;
        public const uint Authoritative = 1u << 0;
        public const uint AupExactDouble3 = 1u << 1;
        public const uint OptionalQualityLeaf = 1u << 2;
        public const uint Missing = 1u << 3;
        public const uint SkippedByQuality = 1u << 4;
        public const uint PresentationExcluded = 1u << 5;
        public const uint BranchNode = 1u << 6;
        public const uint RootNode = 1u << 7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FrameSnapshotDTO
    {
        [FieldOffset(0)] public ulong FrameHash64;
        [FieldOffset(8)] public uint Tick;
        [FieldOffset(12)] public uint InputMaskP1;
        [FieldOffset(16)] public uint InputMaskP2;
        [FieldOffset(20)] public uint MemoryOffset;
        [FieldOffset(24)] public uint MerkleRootIndex;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct StatePageHeaderDTO
    {
        [FieldOffset(0)] public ulong FrameHash64;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint PayloadBytes;
        [FieldOffset(16)] public uint RigidbodyAupCount;
        [FieldOffset(20)] public uint PlayerStateCount;
        [FieldOffset(24)] public uint EntityAupCount;
        [FieldOffset(28)] public uint EntityVelocityCount;
        [FieldOffset(32)] public uint RoomWaterCount;
        [FieldOffset(36)] public uint EntityFlagCount;
        [FieldOffset(40)] public uint EntityItemHashCount;
        [FieldOffset(44)] public uint EntityQuantityCount;
        [FieldOffset(48)] public uint InventoryHashCount;
        [FieldOffset(52)] public uint InventoryQuantityCount;
        [FieldOffset(56)] public uint InventoryDurabilityCount;
        [FieldOffset(60)] public uint QuestMaskCount;
        [FieldOffset(64)] public uint PredatorChosenStateCount;
        [FieldOffset(68)] public uint Flags;
        [FieldOffset(72)] public uint MemoryOffset;
        [FieldOffset(76)] public uint ModQuarantineMask;
        [FieldOffset(80)] public uint MerkleRootIndex;
        [FieldOffset(84)] public uint MerkleBranchCount;
        [FieldOffset(88)] public uint FirstMismatchBufferId;
        [FieldOffset(92)] public uint FirstMismatchByteOffset;
        [FieldOffset(96)] public ulong Reserved0;
        [FieldOffset(104)] public ulong Reserved1;
        [FieldOffset(112)] public ulong Reserved2;
        [FieldOffset(120)] public ulong Reserved3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct RemoteInputFrameDTO
    {
        [FieldOffset(0)] public InputStateDTO Input;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public partial struct MockTickCommand
    {
        [FieldOffset(0)] public uint CurrentFrame;
        [FieldOffset(4)] public uint RollbackFrame;
        [FieldOffset(8)] public uint InputMaskP1;
        [FieldOffset(12)] public ushort FramesToSimulate;
        [FieldOffset(14)] public byte PhaseMask;
        [FieldOffset(15)] public byte Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct RollbackTuningDTO
    {
        [FieldOffset(0)] public int MaxRollbackFrames;
        [FieldOffset(4)] public int VisualInterpolationFrames;
        [FieldOffset(8)] public float VisualInterpolationSeconds;
        [FieldOffset(12)] public float InputPredictionAggressiveness;
        [FieldOffset(16)] public float MinQualityForLookRollback;
        [FieldOffset(20)] public float GlobalQualityWeight;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint PingSimulatedFrames;
        [FieldOffset(32)] public uint PacketLossPermille;
        [FieldOffset(36)] public uint DuplicatePermille;
        [FieldOffset(40)] public uint RedundancyCount;
        [FieldOffset(44)] public uint HashCadenceFrames;
        [FieldOffset(48)] public uint MaxMerkleLeaves;
        [FieldOffset(52)] public uint InputDelayFrames;
        [FieldOffset(56)] public uint _pad0;
        [FieldOffset(60)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct RollbackRuntimeStateDTO
    {
        [FieldOffset(0)] public ulong LastFrameHash64;
        [FieldOffset(8)] public ulong LastRemoteHash64;
        [FieldOffset(16)] public uint CurrentFrame;
        [FieldOffset(20)] public uint LastRollbackFrame;
        [FieldOffset(24)] public uint LastRemoteFrame;
        [FieldOffset(28)] public uint LastMismatchFrame;
        [FieldOffset(32)] public uint FramesResimulated;
        [FieldOffset(36)] public uint RollbacksTriggered;
        [FieldOffset(40)] public float ResimComputeTimeMs;
        [FieldOffset(44)] public float GlobalQualityWeight;
        [FieldOffset(48)] public float MismatchSeverity01;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint StateSnapshotBytes;
        [FieldOffset(60)] public uint StateMemoryOffset;
        [FieldOffset(64)] public uint DesyncCount;
        [FieldOffset(68)] public uint DesyncRepairAttempts;
        [FieldOffset(72)] public uint FirstMismatchBufferId;
        [FieldOffset(76)] public uint FirstMismatchByteOffset;
        [FieldOffset(80)] public ulong LastBranchHash64;
        [FieldOffset(88)] public ulong Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VisualStateDTO
    {
        [FieldOffset(0)] public double3 AnchorAupAbsolute;
        [FieldOffset(24)] public float3 TrueLocalMeters;
        [FieldOffset(36)] public float3 InterpolatedLocalMeters;
        [FieldOffset(48)] public float Blend01;
        [FieldOffset(52)] public float BlendStep01;
        [FieldOffset(56)] public uint EntityId;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VisualStateHistoryDTO
    {
        [FieldOffset(0)] public float3 Offset0;
        [FieldOffset(12)] public float3 Offset1;
        [FieldOffset(24)] public float3 Offset2;
        [FieldOffset(36)] public float3 LastOutput;
        [FieldOffset(48)] public uint EntityId;
        [FieldOffset(52)] public uint Cursor;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint CorrectionFrame;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct NetTelemetryEntry64
    {
        [FieldOffset(0)] public ulong FrameHash64;
        [FieldOffset(8)] public ulong RemoteHash64;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint LastRollbackFrame;
        [FieldOffset(24)] public uint DroppedPackets;
        [FieldOffset(28)] public uint DuplicatedPackets;
        [FieldOffset(32)] public uint ResimulatedFrames;
        [FieldOffset(36)] public float ResimComputeTimeMs;
        [FieldOffset(40)] public float GlobalQualityWeight;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint InputMaskP1;
        [FieldOffset(52)] public uint InputMaskP2;
        [FieldOffset(56)] public uint MismatchBufferId;
        [FieldOffset(60)] public uint MismatchByteOffset;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct RollbackAudioSuppressionDTO
    {
        [FieldOffset(0)] public uint IsResimulating;
        [FieldOffset(4)] public uint UntilFrame;
        [FieldOffset(8)] public uint SuppressionFrame;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct RollbackLegacyProfileDTO
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint SimulatedPingMs;
        [FieldOffset(12)] public uint JitterMs;
        [FieldOffset(16)] public float PacketLoss01;
        [FieldOffset(20)] public float PredictionAggressiveness;
        [FieldOffset(24)] public uint MaxRollbackFrames;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct RollbackVaultBufferDescriptor32
    {
        [FieldOffset(0)] public uint BufferId;
        [FieldOffset(4)] public uint ByteOffset;
        [FieldOffset(8)] public uint ByteLength;
        [FieldOffset(12)] public uint ElementStride;
        [FieldOffset(16)] public uint ElementCount;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint LeafIndex;
        [FieldOffset(28)] public uint Generation;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct H8NetMerkleNodeRecord32
    {
        [FieldOffset(0)] public ulong HashLo;
        [FieldOffset(8)] public ulong HashHi;
        [FieldOffset(16)] public uint BufferId;
        [FieldOffset(20)] public uint ByteOffset;
        [FieldOffset(24)] public uint ByteLength;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct H8NetLeafDeltaRecord64
    {
        [FieldOffset(0)] public ulong LocalHashLo;
        [FieldOffset(8)] public ulong RemoteHashLo;
        [FieldOffset(16)] public ulong LocalHashHi;
        [FieldOffset(24)] public ulong RemoteHashHi;
        [FieldOffset(32)] public uint BufferId;
        [FieldOffset(36)] public uint ByteOffset;
        [FieldOffset(40)] public uint ByteLength;
        [FieldOffset(44)] public uint FirstDifferentByte;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct RollbackInputJournalSlot64
    {
        [FieldOffset(0)] public InputStateDTO Predicted;
        [FieldOffset(24)] public InputStateDTO Remote;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint ReceivedMask;
        [FieldOffset(56)] public uint ExpectedMask;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MockNetworkJitterPacket64
    {
        [FieldOffset(0)] public InputStateDTO Input;
        [FieldOffset(24)] public uint SourceFrame;
        [FieldOffset(28)] public uint ReleaseFrame;
        [FieldOffset(32)] public uint Sequence;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public ulong HashSalt;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MockNetworkJitterState64
    {
        [FieldOffset(0)] public uint Head;
        [FieldOffset(4)] public uint Tail;
        [FieldOffset(8)] public uint Sequence;
        [FieldOffset(12)] public uint DroppedPackets;
        [FieldOffset(16)] public uint DuplicatedPackets;
        [FieldOffset(20)] public uint PacketLossPermille;
        [FieldOffset(24)] public uint DuplicatePermille;
        [FieldOffset(28)] public uint DelayFrames;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint LastFrame;
        [FieldOffset(40)] public ulong RngState;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    public static class RollbackNetcodeLayoutGuard
    {
        public const uint FrameSnapshot = 1u << 0;
        public const uint StatePageHeader = 1u << 1;
        public const uint MerkleNode = 1u << 2;
        public const uint LeafDelta = 1u << 3;
        public const uint InputJournal = 1u << 4;
        public const uint MockJitterPacket = 1u << 5;
        public const uint MockJitterState = 1u << 6;
        public const uint VisualHistory = 1u << 7;
        public const uint Telemetry = 1u << 8;
        public const uint RuntimeState = 1u << 9;
        public const uint Tuning = 1u << 10;
        public const uint AudioSuppression = 1u << 11;

        public static uint Validate()
        {
            uint mask = 0u;
            mask |= SizeMask<FrameSnapshotDTO>(32, FrameSnapshot);
            mask |= SizeMask<StatePageHeaderDTO>(128, StatePageHeader);
            mask |= SizeMask<H8NetMerkleNodeRecord32>(32, MerkleNode);
            mask |= SizeMask<H8NetLeafDeltaRecord64>(64, LeafDelta);
            mask |= SizeMask<RollbackInputJournalSlot64>(64, InputJournal);
            mask |= SizeMask<MockNetworkJitterPacket64>(64, MockJitterPacket);
            mask |= SizeMask<MockNetworkJitterState64>(64, MockJitterState);
            mask |= SizeMask<VisualStateHistoryDTO>(64, VisualHistory);
            mask |= SizeMask<NetTelemetryEntry64>(64, Telemetry);
            mask |= SizeMask<RollbackRuntimeStateDTO>(96, RuntimeState);
            mask |= SizeMask<RollbackTuningDTO>(64, Tuning);
            mask |= SizeMask<RollbackAudioSuppressionDTO>(16, AudioSuppression);
            return mask;
        }

        private static uint SizeMask<T>(int expected, uint flag) where T : struct
        {
            return UnsafeUtility.SizeOf<T>() == expected ? 0u : flag;
        }
    }

    public static unsafe class RollbackNetcodeMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ResolveInputDifferenceFlags(in InputStateDTO predicted, in InputStateDTO remote, float moveEpsilon, float lookEpsilon)
        {
            uint flags = InputMismatchFlags.None;
            if (predicted.ButtonMask != remote.ButtonMask)
                flags |= InputMismatchFlags.Button;

            float2 moveDelta = predicted.MoveAxis - remote.MoveAxis;
            if (math.lengthsq(moveDelta) > moveEpsilon * moveEpsilon)
                flags |= InputMismatchFlags.Move;

            float2 lookDelta = predicted.LookDelta - remote.LookDelta;
            if (math.lengthsq(lookDelta) > lookEpsilon * lookEpsilon)
                flags |= InputMismatchFlags.Look;

            return flags;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldRollback(uint mismatchFlags, float globalQualityWeight, float minQualityForLookRollback)
        {
            if ((mismatchFlags & (InputMismatchFlags.Button | InputMismatchFlags.Move)) != 0u)
                return true;

            if ((mismatchFlags & InputMismatchFlags.Look) == 0u)
                return false;

            float lookGate = math.step(math.saturate(minQualityForLookRollback), math.saturate(globalQualityWeight));
            return lookGate >= 0.5f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveMismatchSeverity(uint mismatchFlags, float globalQualityWeight)
        {
            if ((mismatchFlags & InputMismatchFlags.Button) != 0u)
                return 1f;
            if ((mismatchFlags & InputMismatchFlags.Move) != 0u)
                return math.lerp(0.6f, 0.85f, Smooth01(math.saturate(globalQualityWeight)));
            if ((mismatchFlags & InputMismatchFlags.Look) != 0u)
                return 0.25f * Smooth01(math.saturate(globalQualityWeight));
            return 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - (2f * x));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveBudgetedRollbackFrames(in RollbackTuningDTO tuning, float quality)
        {
            int maxFrames = math.clamp(tuning.MaxRollbackFrames, 1, RollbackNetcodeConstants.MaxRollbackFrames);
            float qualityWeight = math.saturate(math.isfinite(quality) ? quality : 1f);
            float normalized = math.saturate((qualityWeight - 0.1f) * 1.1111112f);
            float budget = math.lerp(0.25f, 1f, Smooth01(normalized));
            return math.clamp((int)math.round(maxFrames * budget), 1, maxFrames);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveMerkleLeafBudget(in RollbackTuningDTO tuning, float quality)
        {
            int maxLeaves = math.clamp((int)(tuning.MaxMerkleLeaves == 0u ? RollbackNetcodeConstants.MerkleLeafCapacity : tuning.MaxMerkleLeaves), 1, RollbackNetcodeConstants.MerkleLeafCapacity);
            int minLeaves = math.min(4, maxLeaves);
            float qualityWeight = math.saturate(math.isfinite(quality) ? quality : 1f);
            float normalized = math.saturate((qualityWeight - 0.1f) * 1.1111112f);
            return math.clamp((int)math.round(math.lerp(minLeaves, maxLeaves, Smooth01(normalized))), 1, maxLeaves);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ResolveHashCadenceFrames(in RollbackTuningDTO tuning, float quality)
        {
            uint baseCadence = tuning.HashCadenceFrames == 0u ? RollbackNetcodeConstants.DesyncHashCadenceFrames : tuning.HashCadenceFrames;
            float qualityWeight = math.saturate(math.isfinite(quality) ? quality : 1f);
            float stretched = math.lerp(baseCadence * 2f, baseCadence, Smooth01(qualityWeight));
            return (uint)math.clamp((int)math.round(stretched), 15, 180);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EstimateResimulationCostMs(int frames, float quality, float severity)
        {
            float qualityWeight = math.saturate(math.isfinite(quality) ? quality : 1f);
            float qualityCurve = Smooth01(qualityWeight);
            float hydratedFrameCostUs = math.lerp(8f, 42f, qualityCurve);
            float severityScale = math.lerp(0.7f, 1.15f, math.saturate(severity));
            return (math.max(0, frames) * hydratedFrameCostUs * severityScale) * 0.001f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Unity.Mathematics.Random CreateDeterministicRandom(uint sectorHash, uint simulationFrameCounter)
        {
            uint seed = sectorHash ^ (simulationFrameCounter * 747796405u) ^ 0x9E3779B9u;
            seed ^= seed >> 16;
            seed *= 2246822519u;
            seed ^= seed >> 13;
            seed = seed == 0u ? 1u : seed;
            return new Unity.Mathematics.Random(seed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong HashExactBytes(void* ptr, int byteLength)
        {
            return MemorySentinelMath.ComputeXXHash3Full64(ptr, byteLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong HashExactAupDouble3(in double3 aup)
        {
            double3 copy = aup;
            return HashExactBytes(&copy, UnsafeUtility.SizeOf<double3>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong HashExactAupDouble3(in AbsoluteUniversePosition aup)
        {
            double3 absolute = new double3(
                (aup.GridX * (double)AbsoluteUniversePosition.CellSizeMeters) + aup.LocalX,
                (aup.GridY * (double)AbsoluteUniversePosition.CellSizeMeters) + aup.LocalY,
                (aup.GridZ * (double)AbsoluteUniversePosition.CellSizeMeters) + aup.LocalZ);
            return HashExactAupDouble3(in absolute);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong MixHash64(ulong state, ulong value)
        {
            state ^= value + 0x9E3779B97F4A7C15UL + (state << 6) + (state >> 2);
            state ^= state >> 33;
            state *= 0xff51afd7ed558ccdUL;
            state ^= state >> 33;
            state *= 0xc4ceb9fe1a85ec53UL;
            state ^= state >> 33;
            return state == 0UL ? 0xA24BAED4963EE407UL : state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveRollbackFrameCount(uint rollbackFrame, uint currentFrame)
        {
            if (rollbackFrame > currentFrame)
                return 0;
            uint frames = currentFrame - rollbackFrame;
            return frames > ushort.MaxValue ? ushort.MaxValue : (int)frames;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 AbsoluteFromPlayerState(in LockstepPlayerKinematicState state)
        {
            return new double3(
                (state.SectorX * (double)AbsoluteUniversePosition.CellSizeMeters) + state.LocalPosition.x,
                (state.SectorY * (double)AbsoluteUniversePosition.CellSizeMeters) + state.LocalPosition.y,
                (state.SectorZ * (double)AbsoluteUniversePosition.CellSizeMeters) + state.LocalPosition.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 LocalMetersFromAnchor(double3 absolute, double3 anchor)
        {
            double3 delta = absolute - anchor;
            delta = math.select(double3.zero, delta, math.isfinite(delta));
            return new float3(
                (float)math.clamp(delta.x, -1000000d, 1000000d),
                (float)math.clamp(delta.y, -1000000d, 1000000d),
                (float)math.clamp(delta.z, -1000000d, 1000000d));
        }
    }

    public static unsafe class RollbackNetcodeBufferAccess
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref FrameSnapshotDTO FrameSnapshotAt(NativeArray<FrameSnapshotDTO> snapshots, int index)
        {
            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(snapshots);
            return ref UnsafeUtility.AsRef<FrameSnapshotDTO>((byte*)ptr + index * UnsafeUtility.SizeOf<FrameSnapshotDTO>());
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ComputeMerkleRootJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<RollbackVaultBufferDescriptor32> LeafDescriptors;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<H8NetMerkleNodeRecord32> MerkleNodes;
        [ReadOnly, NoAlias] public NativeArray<AbsoluteUniversePosition> RigidbodyAups;
        [ReadOnly, NoAlias] public NativeArray<LockstepPlayerKinematicState> PlayerStates;
        [ReadOnly, NoAlias] public NativeArray<AbsoluteUniversePosition> EntityAups;
        [ReadOnly, NoAlias] public NativeArray<float3> EntityVelocities;
        [ReadOnly, NoAlias] public NativeArray<float> RoomWaterLevels;
        [ReadOnly, NoAlias] public NativeArray<uint> EntityFlags;
        [ReadOnly, NoAlias] public NativeArray<uint> EntityItemHashes;
        [ReadOnly, NoAlias] public NativeArray<ushort> EntityQuantities;
        [ReadOnly, NoAlias] public NativeArray<uint> InventoryHashes;
        [ReadOnly, NoAlias] public NativeArray<int> InventoryQuantities;
        [ReadOnly, NoAlias] public NativeArray<float> InventoryDurabilities;
        [ReadOnly, NoAlias] public NativeArray<ulong> QuestMasks;
        [ReadOnly, NoAlias] public NativeArray<byte> PredatorChosenStates;
        public int QualityLeafBudget;
        public uint Frame;

        public void Execute(int index)
        {
            if (!MerkleNodes.IsCreated || !LeafDescriptors.IsCreated || index < 0 || index >= MerkleNodes.Length)
                return;

            H8NetMerkleNodeRecord32 node = default;
            node.Flags = RollbackMerkleFlags.Missing;

            if (index >= LeafDescriptors.Length || index >= QualityLeafBudget)
            {
                if (index < MerkleNodes.Length)
                {
                    node.Flags = RollbackMerkleFlags.SkippedByQuality;
                    MerkleNodes[index] = node;
                }
                return;
            }

            RollbackVaultBufferDescriptor32 descriptor = LeafDescriptors[index];
            if ((descriptor.Flags & RollbackMerkleFlags.PresentationExcluded) != 0u)
            {
                node.BufferId = descriptor.BufferId;
                node.ByteOffset = descriptor.ByteOffset;
                node.ByteLength = descriptor.ByteLength;
                node.Flags = RollbackMerkleFlags.PresentationExcluded | RollbackMerkleFlags.SkippedByQuality;
                MerkleNodes[index] = node;
                return;
            }

            ulong hash = HashDescriptor(in descriptor, out uint byteLength);
            node.HashLo = hash;
            node.HashHi = RollbackNetcodeMath.MixHash64(hash, ((ulong)Frame << 32) ^ descriptor.BufferId ^ descriptor.ElementCount);
            node.BufferId = descriptor.BufferId;
            node.ByteOffset = descriptor.ByteOffset;
            node.ByteLength = byteLength;
            node.Flags = descriptor.Flags & ~(RollbackMerkleFlags.Missing | RollbackMerkleFlags.SkippedByQuality);
            if (hash == 0UL || byteLength == 0u)
                node.Flags |= RollbackMerkleFlags.Missing;
            MerkleNodes[index] = node;
        }

        private ulong HashDescriptor(in RollbackVaultBufferDescriptor32 descriptor, out uint byteLength)
        {
            byteLength = 0u;
            switch (descriptor.BufferId)
            {
                case (uint)BufferID.RigidbodyAUPs:
                    return HashAupArray(RigidbodyAups, descriptor, out byteLength);
                case (uint)BufferID.PlayerKinematicState:
                    return HashNativeArray(PlayerStates, descriptor, out byteLength);
                case (uint)BufferID.EntityAUPs:
                    return HashAupArray(EntityAups, descriptor, out byteLength);
                case (uint)BufferID.EntityVelocities:
                    return HashNativeArray(EntityVelocities, descriptor, out byteLength);
                case (uint)BufferID.RoomWaterLevels:
                    return HashNativeArray(RoomWaterLevels, descriptor, out byteLength);
                case (uint)BufferID.EntityFlags:
                    return HashNativeArray(EntityFlags, descriptor, out byteLength);
                case (uint)BufferID.EntityItemHashes:
                    return HashNativeArray(EntityItemHashes, descriptor, out byteLength);
                case (uint)BufferID.EntityQuantities:
                    return HashNativeArray(EntityQuantities, descriptor, out byteLength);
                case (uint)BufferID.ShinobuInventoryHashes:
                    return HashNativeArray(InventoryHashes, descriptor, out byteLength);
                case (uint)BufferID.ShinobuInventoryQuantities:
                    return HashNativeArray(InventoryQuantities, descriptor, out byteLength);
                case (uint)BufferID.ShinobuInventoryDurabilities:
                    return HashNativeArray(InventoryDurabilities, descriptor, out byteLength);
                case (uint)BufferID.QuestDagGlobalStateMasks:
                    return HashNativeArray(QuestMasks, descriptor, out byteLength);
                case (uint)BufferID.PredatorCognitionChosenStates:
                    return HashNativeArray(PredatorChosenStates, descriptor, out byteLength);
                default:
                    return 0UL;
            }
        }

        private static ulong HashNativeArray<T>(NativeArray<T> source, in RollbackVaultBufferDescriptor32 descriptor, out uint byteLength)
            where T : struct
        {
            byteLength = 0u;
            if (!source.IsCreated || source.Length <= 0)
                return 0UL;

            int stride = UnsafeUtility.SizeOf<T>();
            int start = stride > 0 ? (int)(descriptor.ByteOffset / (uint)stride) : 0;
            int desired = descriptor.ElementCount == 0u ? source.Length : (int)descriptor.ElementCount;
            int count = math.clamp(desired, 0, source.Length - math.min(start, source.Length));
            if (start < 0 || start >= source.Length || count <= 0)
                return 0UL;

            byteLength = (uint)(count * stride);
            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source) + (start * stride);
            return RollbackNetcodeMath.HashExactBytes(ptr, (int)byteLength);
        }

        private static ulong HashAupArray(NativeArray<AbsoluteUniversePosition> source, in RollbackVaultBufferDescriptor32 descriptor, out uint byteLength)
        {
            byteLength = 0u;
            if (!source.IsCreated || source.Length <= 0)
                return 0UL;

            int desired = descriptor.ElementCount == 0u ? source.Length : (int)descriptor.ElementCount;
            int count = math.clamp(desired, 0, source.Length);
            if (count <= 0)
                return 0UL;

            ulong hash = 0xCBF29CE484222325UL ^ descriptor.BufferId;
            for (int i = 0; i < count; i++)
                hash = RollbackNetcodeMath.MixHash64(hash, RollbackNetcodeMath.HashExactAupDouble3(in source[i]));

            byteLength = (uint)(count * UnsafeUtility.SizeOf<double3>());
            return hash;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct FinalizeMerkleRootJob : IJob
    {
        [NoAlias] public NativeArray<H8NetMerkleNodeRecord32> MerkleNodes;
        [NoAlias] public NativeArray<RollbackRuntimeStateDTO> RuntimeState;
        public uint Frame;
        public int QualityLeafBudget;

        public void Execute()
        {
            if (!MerkleNodes.IsCreated || MerkleNodes.Length <= RollbackNetcodeConstants.MerkleRootNodeIndex)
                return;

            int leafBudget = math.clamp(QualityLeafBudget, 1, RollbackNetcodeConstants.MerkleLeafCapacity);
            for (int branch = 0; branch < RollbackNetcodeConstants.MerkleBranchCapacity; branch++)
            {
                int left = branch * 2;
                int right = left + 1;
                int nodeIndex = RollbackNetcodeConstants.MerkleBranchNodeStart + branch;
                H8NetMerkleNodeRecord32 leftNode = left < leafBudget ? MerkleNodes[left] : default;
                H8NetMerkleNodeRecord32 rightNode = right < leafBudget ? MerkleNodes[right] : default;
                H8NetMerkleNodeRecord32 branchNode = default;
                branchNode.HashLo = RollbackNetcodeMath.MixHash64(leftNode.HashLo, rightNode.HashLo);
                branchNode.HashHi = RollbackNetcodeMath.MixHash64(leftNode.HashHi, rightNode.HashHi ^ (uint)nodeIndex);
                branchNode.BufferId = 0u;
                branchNode.ByteOffset = (uint)left;
                branchNode.ByteLength = (uint)(math.min(2, math.max(0, leafBudget - left)));
                branchNode.Flags = RollbackMerkleFlags.BranchNode;
                MerkleNodes[nodeIndex] = branchNode;
            }

            ulong rootLo = 0x9E3779B97F4A7C15UL ^ Frame;
            ulong rootHi = 0xC2B2AE3D27D4EB4FUL ^ (uint)leafBudget;
            for (int i = 0; i < RollbackNetcodeConstants.MerkleBranchCapacity; i++)
            {
                H8NetMerkleNodeRecord32 branchNode = MerkleNodes[RollbackNetcodeConstants.MerkleBranchNodeStart + i];
                rootLo = RollbackNetcodeMath.MixHash64(rootLo, branchNode.HashLo);
                rootHi = RollbackNetcodeMath.MixHash64(rootHi, branchNode.HashHi);
            }

            H8NetMerkleNodeRecord32 root = default;
            root.HashLo = rootLo;
            root.HashHi = rootHi;
            root.ByteLength = (uint)leafBudget;
            root.Flags = RollbackMerkleFlags.RootNode;
            MerkleNodes[RollbackNetcodeConstants.MerkleRootNodeIndex] = root;

            if (RuntimeState.IsCreated && RuntimeState.Length > 0)
            {
                RollbackRuntimeStateDTO state = RuntimeState[0];
                state.CurrentFrame = Frame;
                state.LastFrameHash64 = rootLo;
                state.LastBranchHash64 = rootHi;
                RuntimeState[0] = state;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct StateSnapshotJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<AbsoluteUniversePosition> RigidbodyAups;
        [ReadOnly, NoAlias] public NativeArray<LockstepPlayerKinematicState> PlayerStates;
        [ReadOnly, NoAlias] public NativeArray<AbsoluteUniversePosition> EntityAups;
        [ReadOnly, NoAlias] public NativeArray<float3> EntityVelocities;
        [ReadOnly, NoAlias] public NativeArray<float> RoomWaterLevels;
        [ReadOnly, NoAlias] public NativeArray<uint> EntityFlags;
        [ReadOnly, NoAlias] public NativeArray<uint> EntityItemHashes;
        [ReadOnly, NoAlias] public NativeArray<ushort> EntityQuantities;
        [ReadOnly, NoAlias] public NativeArray<uint> InventoryHashes;
        [ReadOnly, NoAlias] public NativeArray<int> InventoryQuantities;
        [ReadOnly, NoAlias] public NativeArray<float> InventoryDurabilities;
        [ReadOnly, NoAlias] public NativeArray<ulong> QuestMasks;
        [ReadOnly, NoAlias] public NativeArray<byte> PredatorChosenStates;
        [ReadOnly, NoAlias] public NativeArray<H8NetMerkleNodeRecord32> MerkleNodes;
        [NoAlias] public NativeArray<byte> StateRingBuffer;
        [NoAlias] public NativeArray<FrameSnapshotDTO> FrameSnapshots;
        [NoAlias] public NativeArray<RollbackRuntimeStateDTO> RuntimeState;
        public uint Frame;
        public int RingFrameCapacity;
        public int SnapshotStrideBytes;
        public int MaxRigidbodyAups;
        public int MaxPlayerStates;
        public int MaxEntityAups;
        public int MaxEntityVelocities;
        public int MaxRoomWaterLevels;
        public int MaxEntityFlags;
        public int MaxEntityItems;
        public int MaxInventoryItems;
        public int MaxQuestMasks;
        public int MaxPredatorChosenStates;
        public uint InputMaskP1;
        public uint InputMaskP2;
        public uint ModQuarantineMask;
        public uint MerkleRootIndex;

        public void Execute()
        {
            if (!StateRingBuffer.IsCreated || !FrameSnapshots.IsCreated || SnapshotStrideBytes <= RollbackNetcodeConstants.SnapshotHeaderBytes)
                return;

            int capacity = math.max(1, RingFrameCapacity);
            int pageIndex = (int)(Frame % (uint)capacity);
            int pageOffset = pageIndex * SnapshotStrideBytes;
            if (pageOffset < 0 || pageOffset + RollbackNetcodeConstants.SnapshotHeaderBytes > StateRingBuffer.Length)
                return;

            byte* page = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(StateRingBuffer) + pageOffset;
            StatePageHeaderDTO* header = (StatePageHeaderDTO*)page;
            UnsafeUtility.MemClear(header, RollbackNetcodeConstants.SnapshotHeaderBytes);
            header->Frame = Frame;
            header->MemoryOffset = (uint)pageOffset;
            header->ModQuarantineMask = ModQuarantineMask;

            byte* payload = page + RollbackNetcodeConstants.SnapshotHeaderBytes;
            byte* cursor = payload;
            int availablePayloadBytes = SnapshotStrideBytes - RollbackNetcodeConstants.SnapshotHeaderBytes;
            int writtenBytes = 0;

            CopySource(RigidbodyAups, MaxRigidbodyAups, ref cursor, ref writtenBytes, availablePayloadBytes, out uint rigidbodyAupCount);
            CopySource(PlayerStates, MaxPlayerStates, ref cursor, ref writtenBytes, availablePayloadBytes, out uint playerStateCount);
            CopySource(EntityAups, MaxEntityAups, ref cursor, ref writtenBytes, availablePayloadBytes, out uint entityAupCount);
            CopySource(EntityVelocities, MaxEntityVelocities, ref cursor, ref writtenBytes, availablePayloadBytes, out uint entityVelocityCount);
            CopySource(RoomWaterLevels, MaxRoomWaterLevels, ref cursor, ref writtenBytes, availablePayloadBytes, out uint roomWaterCount);
            CopySource(EntityFlags, MaxEntityFlags, ref cursor, ref writtenBytes, availablePayloadBytes, out uint entityFlagCount);
            CopySource(EntityItemHashes, MaxEntityItems, ref cursor, ref writtenBytes, availablePayloadBytes, out uint entityItemHashCount);
            CopySource(EntityQuantities, MaxEntityItems, ref cursor, ref writtenBytes, availablePayloadBytes, out uint entityQuantityCount);
            CopySource(InventoryHashes, MaxInventoryItems, ref cursor, ref writtenBytes, availablePayloadBytes, out uint inventoryHashCount);
            CopySource(InventoryQuantities, MaxInventoryItems, ref cursor, ref writtenBytes, availablePayloadBytes, out uint inventoryQuantityCount);
            CopySource(InventoryDurabilities, MaxInventoryItems, ref cursor, ref writtenBytes, availablePayloadBytes, out uint inventoryDurabilityCount);
            CopySource(QuestMasks, MaxQuestMasks, ref cursor, ref writtenBytes, availablePayloadBytes, out uint questMaskCount);
            CopySource(PredatorChosenStates, MaxPredatorChosenStates, ref cursor, ref writtenBytes, availablePayloadBytes, out uint predatorChosenStateCount);
            header->RigidbodyAupCount = rigidbodyAupCount;
            header->PlayerStateCount = playerStateCount;
            header->EntityAupCount = entityAupCount;
            header->EntityVelocityCount = entityVelocityCount;
            header->RoomWaterCount = roomWaterCount;
            header->EntityFlagCount = entityFlagCount;
            header->EntityItemHashCount = entityItemHashCount;
            header->EntityQuantityCount = entityQuantityCount;
            header->InventoryHashCount = inventoryHashCount;
            header->InventoryQuantityCount = inventoryQuantityCount;
            header->InventoryDurabilityCount = inventoryDurabilityCount;
            header->QuestMaskCount = questMaskCount;
            header->PredatorChosenStateCount = predatorChosenStateCount;
            header->MerkleRootIndex = MerkleRootIndex;

            header->PayloadBytes = (uint)writtenBytes;
            int hashBytes = RollbackNetcodeConstants.SnapshotHeaderBytes + writtenBytes;
            ulong merkleHash = 0UL;
            if (MerkleNodes.IsCreated && MerkleNodes.Length > RollbackNetcodeConstants.MerkleRootNodeIndex)
                merkleHash = MerkleNodes[RollbackNetcodeConstants.MerkleRootNodeIndex].HashLo;
            header->FrameHash64 = merkleHash == 0UL ? RollbackNetcodeMath.HashExactBytes(page, hashBytes) : merkleHash;

            int snapshotIndex = pageIndex;
            ref FrameSnapshotDTO snapshot = ref RollbackNetcodeBufferAccess.FrameSnapshotAt(FrameSnapshots, snapshotIndex);
            snapshot.FrameHash64 = header->FrameHash64;
            snapshot.Tick = Frame;
            snapshot.InputMaskP1 = InputMaskP1;
            snapshot.InputMaskP2 = InputMaskP2;
            snapshot.MemoryOffset = (uint)pageOffset;
            snapshot.MerkleRootIndex = MerkleRootIndex;
            snapshot.Flags = header->Flags;

            if (RuntimeState.IsCreated && RuntimeState.Length > 0)
            {
                RollbackRuntimeStateDTO state = RuntimeState[0];
                state.CurrentFrame = Frame;
                state.LastFrameHash64 = header->FrameHash64;
                state.StateSnapshotBytes = (uint)hashBytes;
                state.StateMemoryOffset = (uint)pageOffset;
                if (MerkleNodes.IsCreated && MerkleNodes.Length > RollbackNetcodeConstants.MerkleRootNodeIndex)
                    state.LastBranchHash64 = MerkleNodes[RollbackNetcodeConstants.MerkleRootNodeIndex].HashHi;
                RuntimeState[0] = state;
            }
        }

        private static void CopySource<T>(
            NativeArray<T> source,
            int maxCount,
            ref byte* cursor,
            ref int writtenBytes,
            int availablePayloadBytes,
            out uint copiedCount)
            where T : struct
        {
            copiedCount = 0u;
            if (!source.IsCreated || source.Length <= 0 || maxCount <= 0)
                return;

            int count = math.min(source.Length, maxCount);
            int byteCount = count * UnsafeUtility.SizeOf<T>();
            if (byteCount <= 0 || writtenBytes + byteCount > availablePayloadBytes)
                return;

            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
            UnsafeUtility.MemCpy(cursor, sourcePtr, byteCount);
            cursor += byteCount;
            writtenBytes += byteCount;
            copiedCount = (uint)count;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct RestoreSnapshotJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<byte> StateRingBuffer;
        [NoAlias] public NativeArray<AbsoluteUniversePosition> RigidbodyAups;
        [NoAlias] public NativeArray<LockstepPlayerKinematicState> PlayerStates;
        [NoAlias] public NativeArray<AbsoluteUniversePosition> EntityAups;
        [NoAlias] public NativeArray<float3> EntityVelocities;
        [NoAlias] public NativeArray<float> RoomWaterLevels;
        [NoAlias] public NativeArray<uint> EntityFlags;
        [NoAlias] public NativeArray<uint> EntityItemHashes;
        [NoAlias] public NativeArray<ushort> EntityQuantities;
        [NoAlias] public NativeArray<uint> InventoryHashes;
        [NoAlias] public NativeArray<int> InventoryQuantities;
        [NoAlias] public NativeArray<float> InventoryDurabilities;
        [NoAlias] public NativeArray<ulong> QuestMasks;
        [NoAlias] public NativeArray<byte> PredatorChosenStates;
        [NoAlias] public NativeArray<RollbackRuntimeStateDTO> RuntimeState;
        public uint RollbackFrame;
        public int RingFrameCapacity;
        public int SnapshotStrideBytes;

        public void Execute()
        {
            if (!StateRingBuffer.IsCreated || SnapshotStrideBytes <= RollbackNetcodeConstants.SnapshotHeaderBytes)
                return;

            int capacity = math.max(1, RingFrameCapacity);
            int pageIndex = (int)(RollbackFrame % (uint)capacity);
            int pageOffset = pageIndex * SnapshotStrideBytes;
            if (pageOffset < 0 || pageOffset + RollbackNetcodeConstants.SnapshotHeaderBytes > StateRingBuffer.Length)
                return;

            byte* page = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(StateRingBuffer) + pageOffset;
            StatePageHeaderDTO* header = (StatePageHeaderDTO*)page;
            if (header->Frame != RollbackFrame || header->PayloadBytes == 0u)
            {
                MarkSnapshotMissing();
                return;
            }

            byte* cursor = page + RollbackNetcodeConstants.SnapshotHeaderBytes;
            CopyDestination(cursor, header->RigidbodyAupCount, RigidbodyAups, out int rigidBytes);
            cursor += rigidBytes;
            CopyDestination(cursor, header->PlayerStateCount, PlayerStates, out int playerBytes);
            cursor += playerBytes;
            CopyDestination(cursor, header->EntityAupCount, EntityAups, out int entityBytes);
            cursor += entityBytes;
            CopyDestination(cursor, header->EntityVelocityCount, EntityVelocities, out int velocityBytes);
            cursor += velocityBytes;
            CopyDestination(cursor, header->RoomWaterCount, RoomWaterLevels, out int roomBytes);
            cursor += roomBytes;
            CopyDestination(cursor, header->EntityFlagCount, EntityFlags, out int entityFlagBytes);
            cursor += entityFlagBytes;
            CopyDestination(cursor, header->EntityItemHashCount, EntityItemHashes, out int entityItemHashBytes);
            cursor += entityItemHashBytes;
            CopyDestination(cursor, header->EntityQuantityCount, EntityQuantities, out int entityQuantityBytes);
            cursor += entityQuantityBytes;
            CopyDestination(cursor, header->InventoryHashCount, InventoryHashes, out int inventoryHashBytes);
            cursor += inventoryHashBytes;
            CopyDestination(cursor, header->InventoryQuantityCount, InventoryQuantities, out int inventoryQuantityBytes);
            cursor += inventoryQuantityBytes;
            CopyDestination(cursor, header->InventoryDurabilityCount, InventoryDurabilities, out int inventoryDurabilityBytes);
            cursor += inventoryDurabilityBytes;
            CopyDestination(cursor, header->QuestMaskCount, QuestMasks, out int questMaskBytes);
            cursor += questMaskBytes;
            CopyDestination(cursor, header->PredatorChosenStateCount, PredatorChosenStates, out _);

            if (RuntimeState.IsCreated && RuntimeState.Length > 0)
            {
                RollbackRuntimeStateDTO state = RuntimeState[0];
                state.LastRollbackFrame = RollbackFrame;
                state.StateMemoryOffset = (uint)pageOffset;
                state.StateSnapshotBytes = RollbackNetcodeConstants.SnapshotHeaderBytes + header->PayloadBytes;
                state.Flags &= ~RollbackNetcodeFlags.SnapshotMissing;
                RuntimeState[0] = state;
            }
        }

        private void MarkSnapshotMissing()
        {
            if (!RuntimeState.IsCreated || RuntimeState.Length <= 0)
                return;

            RollbackRuntimeStateDTO state = RuntimeState[0];
            state.Flags |= RollbackNetcodeFlags.SnapshotMissing;
            RuntimeState[0] = state;
        }

        private static void CopyDestination<T>(byte* source, uint serializedCount, NativeArray<T> destination, out int byteCount)
            where T : struct
        {
            int count = destination.IsCreated
                ? math.min((int)serializedCount, destination.Length)
                : 0;
            byteCount = (int)serializedCount * UnsafeUtility.SizeOf<T>();
            if (count <= 0)
                return;

            int copyBytes = count * UnsafeUtility.SizeOf<T>();
            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafePtr(destination);
            UnsafeUtility.MemCpy(destinationPtr, source, copyBytes);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct DetectInputMismatchJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<InputStateDTO> PredictedJournal;
        [ReadOnly, NoAlias] public NativeArray<RemoteInputFrameDTO> RemoteInputRing;
        [NoAlias] public NativeArray<RollbackInputJournalSlot64> InputJournalRing;
        [NoAlias] public NativeArray<RollbackRuntimeStateDTO> RuntimeState;
        public uint CurrentFrame;
        public int MaxRollbackFrames;
        public float GlobalQualityWeight;
        public float MinQualityForLookRollback;
        public float MoveEpsilon;
        public float LookEpsilon;

        public void Execute()
        {
            if (!RuntimeState.IsCreated || RuntimeState.Length <= 0)
                return;

            RollbackRuntimeStateDTO state = RuntimeState[0];
            state.CurrentFrame = CurrentFrame;
            state.GlobalQualityWeight = math.saturate(GlobalQualityWeight);
            state.MismatchSeverity01 = 0f;
            state.Flags &= ~(RollbackNetcodeFlags.RollbackRequired | RollbackNetcodeFlags.MismatchMask | RollbackNetcodeFlags.MissingInputJournal);

            if (!PredictedJournal.IsCreated || !RemoteInputRing.IsCreated || !InputJournalRing.IsCreated ||
                PredictedJournal.Length <= 0 || RemoteInputRing.Length <= 0 || InputJournalRing.Length <= 0)
            {
                state.Flags |= RollbackNetcodeFlags.MissingInputJournal;
                RuntimeState[0] = state;
                return;
            }

            int lookback = math.min(MaxRollbackFrames, math.min(PredictedJournal.Length, math.min(RemoteInputRing.Length, InputJournalRing.Length)));
            if (lookback <= 0)
            {
                RuntimeState[0] = state;
                return;
            }

            uint bestFrame = 0u;
            uint bestMask = 0u;
            float bestSeverity = 0f;
            bool found = false;

            for (int age = lookback - 1; age >= 0; age--)
            {
                if (CurrentFrame < (uint)age)
                    continue;

                uint frame = CurrentFrame - (uint)age;
                int ringIndex = (int)(frame % (uint)RemoteInputRing.Length);
                RemoteInputFrameDTO remote = RemoteInputRing[ringIndex];
                InputStateDTO predicted = PredictedJournal[(int)(frame % (uint)PredictedJournal.Length)];
                RollbackInputJournalSlot64 slot = InputJournalRing[(int)(frame % (uint)InputJournalRing.Length)];
                slot.Predicted = predicted;
                slot.Frame = frame;
                slot.ExpectedMask = 1u;
                slot.Flags = RemoteInputFlags.Predicted;
                if (remote.Frame == frame && (remote.Flags & RemoteInputFlags.Received) != 0u)
                {
                    slot.Remote = remote.Input;
                    slot.ReceivedMask = 1u;
                    slot.Flags |= remote.Flags;
                }
                else
                {
                    slot.ReceivedMask = 0u;
                }

                InputJournalRing[(int)(frame % (uint)InputJournalRing.Length)] = slot;
                if (slot.ReceivedMask != slot.ExpectedMask)
                {
                    bestFrame = frame;
                    bestMask = InputMismatchFlags.Button;
                    bestSeverity = 1f;
                    state.Flags |= RollbackNetcodeFlags.MissingInputJournal;
                    found = true;
                    break;
                }

                state.LastRemoteFrame = frame;
                if ((remote.Flags & RemoteInputFlags.ModQuarantined) != 0u)
                {
                    state.Flags |= RollbackNetcodeFlags.ModQuarantine;
                    continue;
                }

                uint mismatch = RollbackNetcodeMath.ResolveInputDifferenceFlags(predicted, remote.Input, MoveEpsilon, LookEpsilon);
                if (!RollbackNetcodeMath.ShouldRollback(mismatch, GlobalQualityWeight, MinQualityForLookRollback))
                    continue;

                bestFrame = frame;
                bestMask = mismatch;
                bestSeverity = RollbackNetcodeMath.ResolveMismatchSeverity(mismatch, GlobalQualityWeight);
                found = true;
                break;
            }

            if (found)
            {
                state.LastMismatchFrame = bestFrame;
                state.MismatchSeverity01 = bestSeverity;
                state.Flags |= RollbackNetcodeFlags.RollbackRequired | (bestMask << RollbackNetcodeFlags.MismatchShift);
            }

            RuntimeState[0] = state;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockNetworkJitterJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<InputStateDTO> PredictedJournal;
        [NoAlias] public NativeArray<RemoteInputFrameDTO> RemoteInputRing;
        [NoAlias] public NativeArray<MockNetworkJitterPacket64> Packets;
        [NoAlias] public NativeArray<MockNetworkJitterState64> JitterState;
        public uint CurrentFrame;
        public uint DelayFrames;
        public uint PacketLossPermille;
        public uint DuplicatePermille;
        public uint Seed;

        public void Execute()
        {
            if (!PredictedJournal.IsCreated || !RemoteInputRing.IsCreated || !Packets.IsCreated || !JitterState.IsCreated ||
                PredictedJournal.Length <= 0 || RemoteInputRing.Length <= 0 || Packets.Length <= 0 || JitterState.Length <= 0)
            {
                return;
            }

            MockNetworkJitterState64 state = JitterState[0];
            state.PacketLossPermille = math.min(PacketLossPermille, 1000u);
            state.DuplicatePermille = math.min(DuplicatePermille, 1000u);
            state.DelayFrames = DelayFrames;
            state.Flags |= RollbackNetcodeFlags.MockJitterActive;
            state.LastFrame = CurrentFrame;
            uint seed = Seed ^ CurrentFrame ^ (uint)PredictedJournal.Length;
            Unity.Mathematics.Random rng = RollbackNetcodeMath.CreateDeterministicRandom(seed, CurrentFrame);

            InputStateDTO input = PredictedJournal[(int)(CurrentFrame % (uint)PredictedJournal.Length)];
            uint lossRoll = rng.NextUInt(1000u);
            if (lossRoll < state.PacketLossPermille)
            {
                state.DroppedPackets++;
            }
            else
            {
                Enqueue(ref state, input, CurrentFrame, CurrentFrame + DelayFrames, 0u);
                uint duplicateRoll = rng.NextUInt(1000u);
                if (duplicateRoll < state.DuplicatePermille)
                {
                    Enqueue(ref state, input, CurrentFrame, CurrentFrame + DelayFrames + 1u, RemoteInputFlags.Predicted);
                    state.DuplicatedPackets++;
                }
            }

            DrainReady(ref state);
            JitterState[0] = state;
        }

        private void Enqueue(ref MockNetworkJitterState64 state, in InputStateDTO input, uint sourceFrame, uint releaseFrame, uint flags)
        {
            uint nextHead = (state.Head + 1u) % (uint)Packets.Length;
            if (nextHead == state.Tail)
            {
                state.DroppedPackets++;
                return;
            }

            MockNetworkJitterPacket64 packet = default;
            packet.Input = input;
            packet.SourceFrame = sourceFrame;
            packet.ReleaseFrame = releaseFrame;
            packet.Sequence = state.Sequence++;
            packet.Flags = flags;
            packet.HashSalt = RollbackNetcodeMath.MixHash64(sourceFrame, releaseFrame ^ packet.Sequence);
            Packets[(int)state.Head] = packet;
            state.Head = nextHead;
        }

        private void DrainReady(ref MockNetworkJitterState64 state)
        {
            int guard = 0;
            while (state.Tail != state.Head && guard++ < Packets.Length)
            {
                MockNetworkJitterPacket64 packet = Packets[(int)state.Tail];
                if (packet.ReleaseFrame > CurrentFrame)
                    break;

                int remoteIndex = (int)(packet.SourceFrame % (uint)RemoteInputRing.Length);
                RemoteInputRing[remoteIndex] = new RemoteInputFrameDTO
                {
                    Input = packet.Input,
                    Frame = packet.SourceFrame,
                    Flags = RemoteInputFlags.Received | packet.Flags
                };
                state.Tail = (state.Tail + 1u) % (uint)Packets.Length;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ApplyRemoteInputCorrectionJob : IJob
    {
        [NoAlias] public NativeArray<InputStateDTO> PredictedJournal;
        [ReadOnly, NoAlias] public NativeArray<RemoteInputFrameDTO> RemoteInputRing;
        public uint RollbackFrame;
        public uint CurrentFrame;

        public void Execute()
        {
            if (!PredictedJournal.IsCreated || !RemoteInputRing.IsCreated || PredictedJournal.Length <= 0 || RemoteInputRing.Length <= 0)
                return;

            for (uint frame = RollbackFrame; frame <= CurrentFrame; frame++)
            {
                int remoteIndex = (int)(frame % (uint)RemoteInputRing.Length);
                RemoteInputFrameDTO remote = RemoteInputRing[remoteIndex];
                if (remote.Frame != frame || (remote.Flags & RemoteInputFlags.Received) == 0u)
                    continue;
                if ((remote.Flags & RemoteInputFlags.ModQuarantined) != 0u)
                    continue;

                PredictedJournal[(int)(frame % (uint)PredictedJournal.Length)] = remote.Input;
                if (frame == uint.MaxValue)
                    break;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct HeadlessResimulationCommandJob : IJob
    {
        [NoAlias] public NativeArray<MockTickCommand> Commands;
        [NoAlias] public NativeArray<RollbackAudioSuppressionDTO> AudioSuppression;
        [NoAlias] public NativeArray<RollbackRuntimeStateDTO> RuntimeState;
        public uint RollbackFrame;
        public uint CurrentFrame;
        public uint InputMaskP1;

        public void Execute()
        {
            int frames = RollbackNetcodeMath.ResolveRollbackFrameCount(RollbackFrame, CurrentFrame);
            if (Commands.IsCreated && Commands.Length > 0)
            {
                MockTickCommand command = default;
                command.CurrentFrame = CurrentFrame;
                command.RollbackFrame = RollbackFrame;
                command.FramesToSimulate = (ushort)math.min(frames, ushort.MaxValue);
                command.PhaseMask = RollbackNetcodeConstants.PhaseSimulation | RollbackNetcodeConstants.PhasePostSimulation;
                command.Flags = 1;
                command.InputMaskP1 = InputMaskP1;
                Commands[0] = command;
            }

            if (AudioSuppression.IsCreated && AudioSuppression.Length > 0)
            {
                RollbackAudioSuppressionDTO suppression = default;
                suppression.IsResimulating = 1u;
                suppression.UntilFrame = CurrentFrame + 1u;
                suppression.SuppressionFrame = CurrentFrame;
                suppression.Flags = 1u;
                AudioSuppression[0] = suppression;
            }

            if (RuntimeState.IsCreated && RuntimeState.Length > 0)
            {
                RollbackRuntimeStateDTO state = RuntimeState[0];
                state.RollbacksTriggered++;
                state.FramesResimulated += (uint)frames;
                state.LastRollbackFrame = RollbackFrame;
                state.Flags |= RollbackNetcodeFlags.Resimulating;
                RuntimeState[0] = state;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct RollbackFixedPipelineJob : IJob
    {
        [NoAlias] public NativeArray<RollbackTuningDTO> Tuning;
        [NoAlias] public NativeArray<RollbackRuntimeStateDTO> RuntimeState;
        [NoAlias] public NativeArray<InputStateDTO> PredictedJournal;
        [ReadOnly, NoAlias] public NativeArray<RemoteInputFrameDTO> RemoteInputRing;
        [NoAlias] public NativeArray<RollbackInputJournalSlot64> InputJournalRing;
        [NoAlias] public NativeArray<byte> StateRingBuffer;
        [NoAlias] public NativeArray<FrameSnapshotDTO> FrameSnapshots;
        [NoAlias] public NativeArray<MockTickCommand> Commands;
        [NoAlias] public NativeArray<RollbackAudioSuppressionDTO> AudioSuppression;
        [NoAlias] public NativeArray<VisualStateDTO> VisualStates;
        [NoAlias] public NativeArray<VisualStateHistoryDTO> VisualHistory;
        [NoAlias] public NativeArray<H8NetMerkleNodeRecord32> MerkleNodes;
        [ReadOnly, NoAlias] public NativeArray<MockNetworkJitterState64> MockJitterState;
        [NoAlias] public NativeArray<NetTelemetryEntry64> Telemetry;
        [NoAlias] public NativeArray<AbsoluteUniversePosition> RigidbodyAups;
        [NoAlias] public NativeArray<LockstepPlayerKinematicState> PlayerStates;
        [NoAlias] public NativeArray<AbsoluteUniversePosition> EntityAups;
        [NoAlias] public NativeArray<float3> EntityVelocities;
        [NoAlias] public NativeArray<float> RoomWaterLevels;
        [NoAlias] public NativeArray<uint> EntityFlags;
        [NoAlias] public NativeArray<uint> EntityItemHashes;
        [NoAlias] public NativeArray<ushort> EntityQuantities;
        [NoAlias] public NativeArray<uint> InventoryHashes;
        [NoAlias] public NativeArray<int> InventoryQuantities;
        [NoAlias] public NativeArray<float> InventoryDurabilities;
        [NoAlias] public NativeArray<ulong> QuestMasks;
        [NoAlias] public NativeArray<byte> PredatorChosenStates;
        public uint CurrentFrame;
        public uint ModeFlags;
        public int RingFrameCapacity;
        public int SnapshotStrideBytes;
        public int MaxRollbackFrames;
        public int MaxRigidbodyAups;
        public int MaxPlayerStates;
        public int MaxEntityAups;
        public int MaxEntityVelocities;
        public int MaxRoomWaterLevels;
        public int MaxEntityFlags;
        public int MaxEntityItems;
        public int MaxInventoryItems;
        public int MaxQuestMasks;
        public int MaxPredatorChosenStates;
        public float GlobalQualityWeight;
        public float MoveEpsilon;
        public float LookEpsilon;
        public int TelemetryWriteIndex;
        public uint ModQuarantineMask;

        public void Execute()
        {
            if (!RuntimeState.IsCreated || RuntimeState.Length <= 0)
                return;

            RollbackTuningDTO tuning = ResolveTuning();
            if (Tuning.IsCreated && Tuning.Length > 0)
                Tuning[0] = tuning;

            RollbackRuntimeStateDTO state = RuntimeState[0];
            state.Flags = (state.Flags | ModeFlags) &
                ~(RollbackNetcodeFlags.RollbackRequired |
                  RollbackNetcodeFlags.Resimulating |
                  RollbackNetcodeFlags.ResimBudgetExceeded |
                  RollbackNetcodeFlags.BranchProbeRequested |
                  RollbackNetcodeFlags.MismatchMask);
            RuntimeState[0] = state;

            DetectInputMismatchJob detect = new DetectInputMismatchJob
            {
                PredictedJournal = PredictedJournal,
                RemoteInputRing = RemoteInputRing,
                InputJournalRing = InputJournalRing,
                RuntimeState = RuntimeState,
                CurrentFrame = CurrentFrame,
                MaxRollbackFrames = MaxRollbackFrames,
                GlobalQualityWeight = GlobalQualityWeight,
                MinQualityForLookRollback = tuning.MinQualityForLookRollback,
                MoveEpsilon = MoveEpsilon,
                LookEpsilon = LookEpsilon
            };
            detect.Execute();

            state = RuntimeState[0];
            if ((state.Flags & RollbackNetcodeFlags.RollbackRequired) != 0u)
                ExecuteRollback(in tuning, ref state);

            SnapshotCurrentState();
            CheckRemoteHashFence();
            VisualStateInterpolatorJob visual = new VisualStateInterpolatorJob
            {
                VisualStates = VisualStates,
                VisualHistory = VisualHistory,
                CurrentFrame = CurrentFrame,
                GlobalQualityWeight = GlobalQualityWeight
            };
            visual.Execute();
            WriteTelemetry();
        }

        private RollbackTuningDTO ResolveTuning()
        {
            RollbackTuningDTO tuning = default;
            tuning.MaxRollbackFrames = RollbackNetcodeConstants.MaxRollbackFrames;
            tuning.VisualInterpolationFrames = 3;
            tuning.VisualInterpolationSeconds = RollbackNetcodeConstants.DefaultVisualInterpolationSeconds;
            tuning.InputPredictionAggressiveness = RollbackNetcodeConstants.DefaultPredictionAggressiveness;
            tuning.MinQualityForLookRollback = RollbackNetcodeConstants.DefaultLookRollbackMinQuality;
            tuning.GlobalQualityWeight = GlobalQualityWeight;
            tuning.HashCadenceFrames = RollbackNetcodeConstants.DesyncHashCadenceFrames;
            tuning.MaxMerkleLeaves = RollbackNetcodeConstants.MerkleLeafCapacity;
            tuning.InputDelayFrames = 0u;

            if (Tuning.IsCreated && Tuning.Length > 0)
                tuning = Tuning[0];

            tuning.MaxRollbackFrames = math.clamp(tuning.MaxRollbackFrames, 1, RollbackNetcodeConstants.MaxRollbackFrames);
            tuning.VisualInterpolationFrames = math.clamp(tuning.VisualInterpolationFrames, 1, 12);
            tuning.VisualInterpolationSeconds = math.clamp(tuning.VisualInterpolationSeconds, 0.016f, 0.25f);
            tuning.InputPredictionAggressiveness = math.saturate(tuning.InputPredictionAggressiveness);
            tuning.MinQualityForLookRollback = math.saturate(tuning.MinQualityForLookRollback);
            tuning.GlobalQualityWeight = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f);
            tuning.PacketLossPermille = math.min(tuning.PacketLossPermille, 1000u);
            tuning.DuplicatePermille = math.min(tuning.DuplicatePermille, 1000u);
            tuning.RedundancyCount = math.min(tuning.RedundancyCount, 4u);
            tuning.HashCadenceFrames = tuning.HashCadenceFrames == 0u ? RollbackNetcodeConstants.DesyncHashCadenceFrames : math.clamp(tuning.HashCadenceFrames, 15u, 180u);
            tuning.MaxMerkleLeaves = tuning.MaxMerkleLeaves == 0u ? RollbackNetcodeConstants.MerkleLeafCapacity : math.clamp(tuning.MaxMerkleLeaves, 1u, (uint)RollbackNetcodeConstants.MerkleLeafCapacity);
            tuning.InputDelayFrames = math.min(tuning.InputDelayFrames, 30u);
            return tuning;
        }

        private void ExecuteRollback(in RollbackTuningDTO tuning, ref RollbackRuntimeStateDTO state)
        {
            uint rollbackFrame = state.LastMismatchFrame;
            double3 preRollbackVisual = ResolvePrimaryAbsolutePosition();

            RestoreSnapshotJob restore = new RestoreSnapshotJob
            {
                StateRingBuffer = StateRingBuffer,
                RigidbodyAups = RigidbodyAups,
                PlayerStates = PlayerStates,
                EntityAups = EntityAups,
                EntityVelocities = EntityVelocities,
                RoomWaterLevels = RoomWaterLevels,
                EntityFlags = EntityFlags,
                EntityItemHashes = EntityItemHashes,
                EntityQuantities = EntityQuantities,
                InventoryHashes = InventoryHashes,
                InventoryQuantities = InventoryQuantities,
                InventoryDurabilities = InventoryDurabilities,
                QuestMasks = QuestMasks,
                PredatorChosenStates = PredatorChosenStates,
                RuntimeState = RuntimeState,
                RollbackFrame = rollbackFrame,
                RingFrameCapacity = RingFrameCapacity,
                SnapshotStrideBytes = SnapshotStrideBytes
            };
            restore.Execute();

            ApplyRemoteInputCorrectionJob correction = new ApplyRemoteInputCorrectionJob
            {
                PredictedJournal = PredictedJournal,
                RemoteInputRing = RemoteInputRing,
                RollbackFrame = rollbackFrame,
                CurrentFrame = CurrentFrame
            };
            correction.Execute();

            HeadlessResimulationCommandJob command = new HeadlessResimulationCommandJob
            {
                Commands = Commands,
                AudioSuppression = AudioSuppression,
                RuntimeState = RuntimeState,
                RollbackFrame = rollbackFrame,
                CurrentFrame = CurrentFrame,
                InputMaskP1 = ResolveInputMask()
            };
            command.Execute();

            state = RuntimeState[0];
            int resimFrames = RollbackNetcodeMath.ResolveRollbackFrameCount(rollbackFrame, CurrentFrame);
            state.ResimComputeTimeMs = RollbackNetcodeMath.EstimateResimulationCostMs(resimFrames, GlobalQualityWeight, state.MismatchSeverity01);
            state.Flags &= ~RollbackNetcodeFlags.RollbackRequired;
            if (state.ResimComputeTimeMs > RollbackNetcodeConstants.ResimDumpThresholdMs)
                state.Flags |= RollbackNetcodeFlags.ResimBudgetExceeded;
            RuntimeState[0] = state;

            WriteVisualCorrection(preRollbackVisual, ResolvePrimaryAbsolutePosition(), in tuning);
        }

        private void SnapshotCurrentState()
        {
            StateSnapshotJob snapshot = new StateSnapshotJob
            {
                RigidbodyAups = RigidbodyAups,
                PlayerStates = PlayerStates,
                EntityAups = EntityAups,
                EntityVelocities = EntityVelocities,
                RoomWaterLevels = RoomWaterLevels,
                EntityFlags = EntityFlags,
                EntityItemHashes = EntityItemHashes,
                EntityQuantities = EntityQuantities,
                InventoryHashes = InventoryHashes,
                InventoryQuantities = InventoryQuantities,
                InventoryDurabilities = InventoryDurabilities,
                QuestMasks = QuestMasks,
                PredatorChosenStates = PredatorChosenStates,
                MerkleNodes = MerkleNodes,
                StateRingBuffer = StateRingBuffer,
                FrameSnapshots = FrameSnapshots,
                RuntimeState = RuntimeState,
                Frame = CurrentFrame,
                RingFrameCapacity = RingFrameCapacity,
                SnapshotStrideBytes = SnapshotStrideBytes,
                MaxRigidbodyAups = MaxRigidbodyAups,
                MaxPlayerStates = MaxPlayerStates,
                MaxEntityAups = MaxEntityAups,
                MaxEntityVelocities = MaxEntityVelocities,
                MaxRoomWaterLevels = MaxRoomWaterLevels,
                MaxEntityFlags = MaxEntityFlags,
                MaxEntityItems = MaxEntityItems,
                MaxInventoryItems = MaxInventoryItems,
                MaxQuestMasks = MaxQuestMasks,
                MaxPredatorChosenStates = MaxPredatorChosenStates,
                InputMaskP1 = ResolveInputMask(),
                InputMaskP2 = 0u,
                ModQuarantineMask = ModQuarantineMask,
                MerkleRootIndex = RollbackNetcodeConstants.MerkleRootNodeIndex
            };
            snapshot.Execute();
        }

        private void CheckRemoteHashFence()
        {
            RollbackTuningDTO tuning = ResolveTuning();
            uint cadence = RollbackNetcodeMath.ResolveHashCadenceFrames(in tuning, GlobalQualityWeight);
            if (cadence == 0u ||
                (CurrentFrame % cadence) != 0u ||
                !RuntimeState.IsCreated ||
                RuntimeState.Length <= 0)
            {
                return;
            }

            RollbackRuntimeStateDTO state = RuntimeState[0];
            if (state.LastRemoteHash64 == 0UL ||
                state.LastFrameHash64 == 0UL ||
                state.LastRemoteHash64 == state.LastFrameHash64)
            {
                return;
            }

            state.Flags |= RollbackNetcodeFlags.HashMismatch | RollbackNetcodeFlags.BranchProbeRequested;
            state.DesyncCount++;
            state.DesyncRepairAttempts++;
            ResolveFirstMerkleMismatch(ref state);
            if (state.DesyncRepairAttempts >= 3u)
            {
                state.Flags |= RollbackNetcodeFlags.HardResyncRequired |
                    RollbackNetcodeFlags.DesyncPaused |
                    RollbackNetcodeFlags.FullStateOverwriteRequested;
            }
            RuntimeState[0] = state;
        }

        private void ResolveFirstMerkleMismatch(ref RollbackRuntimeStateDTO state)
        {
            if (!MerkleNodes.IsCreated || MerkleNodes.Length <= RollbackNetcodeConstants.MerkleBranchNodeStart)
                return;

            state.FirstMismatchBufferId = 0u;
            state.FirstMismatchByteOffset = 0u;
            for (int i = 0; i < RollbackNetcodeConstants.MerkleLeafCapacity && i < MerkleNodes.Length; i++)
            {
                H8NetMerkleNodeRecord32 node = MerkleNodes[i];
                if ((node.Flags & (RollbackMerkleFlags.Missing | RollbackMerkleFlags.SkippedByQuality)) != 0u)
                    continue;
                if (node.HashLo == 0UL)
                    continue;

                state.FirstMismatchBufferId = node.BufferId;
                state.FirstMismatchByteOffset = node.ByteOffset;
                return;
            }
        }

        private void WriteTelemetry()
        {
            if (!RuntimeState.IsCreated ||
                RuntimeState.Length <= 0 ||
                !FrameSnapshots.IsCreated ||
                FrameSnapshots.Length <= 0)
            {
                return;
            }

            RollbackRuntimeStateDTO state = RuntimeState[0];
            FrameSnapshotDTO snapshot = FrameSnapshots[(int)(CurrentFrame % (uint)FrameSnapshots.Length)];
            int index = TelemetryWriteIndex;
            if (!TelemetryTargetIsReady(ref index))
                return;

            NetTelemetryEntry64 entry = default;
            entry.Frame = CurrentFrame;
            entry.LastRollbackFrame = state.LastRollbackFrame;
            if (MockJitterState.IsCreated && MockJitterState.Length > 0)
            {
                MockNetworkJitterState64 jitter = MockJitterState[0];
                entry.DroppedPackets = jitter.DroppedPackets;
                entry.DuplicatedPackets = jitter.DuplicatedPackets;
            }
            entry.ResimulatedFrames = state.FramesResimulated;
            entry.ResimComputeTimeMs = state.ResimComputeTimeMs;
            entry.GlobalQualityWeight = GlobalQualityWeight;
            entry.FrameHash64 = snapshot.FrameHash64;
            entry.RemoteHash64 = state.LastRemoteHash64;
            entry.Flags = state.Flags;
            entry.InputMaskP1 = ResolveInputMask();
            entry.InputMaskP2 = 0u;
            entry.MismatchBufferId = state.FirstMismatchBufferId;
            entry.MismatchByteOffset = state.FirstMismatchByteOffset;
            Telemetry[index] = entry;
        }

        private bool TelemetryTargetIsReady(ref int index)
        {
            if (!Telemetry.IsCreated || Telemetry.Length <= 0)
                return false;

            if ((uint)index >= (uint)Telemetry.Length)
                index = 0;
            return true;
        }

        private void WriteVisualCorrection(double3 preRollbackVisual, double3 correctedMath, in RollbackTuningDTO tuning)
        {
            if (!VisualStates.IsCreated || VisualStates.Length <= 0)
                return;

            int frames = math.max(1, tuning.VisualInterpolationFrames);
            VisualStateDTO visual = default;
            double3 anchor = preRollbackVisual;
            visual.AnchorAupAbsolute = anchor;
            visual.TrueLocalMeters = RollbackNetcodeMath.LocalMetersFromAnchor(correctedMath, anchor);
            visual.InterpolatedLocalMeters = float3.zero;
            visual.Blend01 = 0f;
            visual.BlendStep01 = 1f / math.max(1f, (float)frames);
            visual.EntityId = 0u;
            visual.Flags = 1u;
            VisualStates[0] = visual;

            if (VisualHistory.IsCreated && VisualHistory.Length > 0)
            {
                VisualStateHistoryDTO history = VisualHistory[0];
                history.EntityId = visual.EntityId;
                history.Offset0 = visual.TrueLocalMeters;
                history.Offset1 = history.LastOutput;
                history.Offset2 = float3.zero;
                history.CorrectionFrame = CurrentFrame;
                history.Flags = 1u;
                VisualHistory[0] = history;
            }
        }

        private double3 ResolvePrimaryAbsolutePosition()
        {
            if (PlayerStates.IsCreated && PlayerStates.Length > 0)
                return RollbackNetcodeMath.AbsoluteFromPlayerState(PlayerStates[0]);

            if (RigidbodyAups.IsCreated && RigidbodyAups.Length > 0)
                return AbsoluteFromAup(RigidbodyAups[0]);

            if (EntityAups.IsCreated && EntityAups.Length > 0)
                return AbsoluteFromAup(EntityAups[0]);

            return double3.zero;
        }

        private static double3 AbsoluteFromAup(in AbsoluteUniversePosition aup)
        {
            return new double3(
                (aup.GridX * (double)AbsoluteUniversePosition.CellSizeMeters) + aup.LocalX,
                (aup.GridY * (double)AbsoluteUniversePosition.CellSizeMeters) + aup.LocalY,
                (aup.GridZ * (double)AbsoluteUniversePosition.CellSizeMeters) + aup.LocalZ);
        }

        private uint ResolveInputMask()
        {
            if (!PredictedJournal.IsCreated || PredictedJournal.Length <= 0)
                return 0u;

            return PredictedJournal[(int)(CurrentFrame % (uint)PredictedJournal.Length)].ButtonMask;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct VisualStateBlendJob : IJob
    {
        [NoAlias] public NativeArray<VisualStateDTO> VisualStates;

        public void Execute()
        {
            if (!VisualStates.IsCreated)
                return;

            for (int i = 0; i < VisualStates.Length; i++)
            {
                VisualStateDTO state = VisualStates[i];
                if ((state.Flags & 1u) == 0u)
                    continue;

                state.Blend01 = math.saturate(state.Blend01 + state.BlendStep01);
                state.InterpolatedLocalMeters = math.lerp(state.InterpolatedLocalMeters, state.TrueLocalMeters, state.Blend01);
                if (state.Blend01 >= 0.999f)
                    state.Flags &= ~1u;
                VisualStates[i] = state;
            }
        }
    }
}
