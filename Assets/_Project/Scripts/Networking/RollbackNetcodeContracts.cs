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
        public const int CommandCapacity = 8;
        public const int MaxRollbackFrames = 120;
        public const int MaxRigidbodyAups = 256;
        public const int MaxPlayerStates = 4;
        public const int MaxEntityAups = 512;
        public const int MaxEntityVelocities = 512;
        public const int MaxRoomWaterLevels = 256;
        public const int CsvScratchBytes = 4096;
        public const int SnapshotHeaderBytes = 64;
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

    [StructLayout(LayoutKind.Sequential, Size = 24)]
    public struct FrameSnapshotDTO
    {
        public ulong FrameHash64;
        public uint InputMaskP1;
        public uint InputMaskP2;
        public uint MemoryOffset;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct StatePageHeaderDTO
    {
        public ulong FrameHash64;
        public uint Frame;
        public uint PayloadBytes;
        public uint RigidbodyAupCount;
        public uint PlayerStateCount;
        public uint EntityAupCount;
        public uint EntityVelocityCount;
        public uint RoomWaterCount;
        public uint Flags;
        public uint MemoryOffset;
        public uint ModQuarantineMask;
        public uint Reserved0;
        public uint Reserved1;
        public uint Reserved2;
        public uint Reserved3;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct RemoteInputFrameDTO
    {
        public InputStateDTO Input;
        public uint Frame;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public partial struct MockTickCommand
    {
        public uint CurrentFrame;
        public uint RollbackFrame;
        public uint InputMaskP1;
        public ushort FramesToSimulate;
        public byte PhaseMask;
        public byte Flags;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct RollbackTuningDTO
    {
        public int MaxRollbackFrames;
        public int VisualInterpolationFrames;
        public float VisualInterpolationSeconds;
        public float InputPredictionAggressiveness;
        public float MinQualityForLookRollback;
        public float GlobalQualityWeight;
        public uint Flags;
        public uint PingSimulatedFrames;
    }

    [StructLayout(LayoutKind.Sequential, Size = 80)]
    public struct RollbackRuntimeStateDTO
    {
        public ulong LastFrameHash64;
        public ulong LastRemoteHash64;
        public uint CurrentFrame;
        public uint LastRollbackFrame;
        public uint LastRemoteFrame;
        public uint LastMismatchFrame;
        public uint FramesResimulated;
        public uint RollbacksTriggered;
        public float ResimComputeTimeMs;
        public float GlobalQualityWeight;
        public float MismatchSeverity01;
        public uint Flags;
        public uint StateSnapshotBytes;
        public uint StateMemoryOffset;
        public uint DesyncCount;
        public uint Reserved0;
        public ulong Reserved1;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct VisualStateDTO
    {
        public double3 AnchorAupAbsolute;
        public float3 TrueLocalMeters;
        public float3 InterpolatedLocalMeters;
        public float Blend01;
        public float BlendStep01;
        public uint EntityId;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, Size = 80)]
    public struct NetcodeTelemetryEntry
    {
        public ulong FrameHash64;
        public ulong RemoteHash64;
        public uint Frame;
        public uint LastRollbackFrame;
        public uint RollbacksTriggered;
        public uint FramesResimulated;
        public float ResimComputeTimeMs;
        public float GlobalQualityWeight;
        public uint Flags;
        public uint InputMaskP1;
        public uint InputMaskP2;
        public uint StateMemoryOffset;
        public uint SnapshotBytes;
        public uint MismatchFrame;
        public uint Reserved0;
        public uint Reserved1;
        public ulong Reserved2;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct RollbackAudioSuppressionDTO
    {
        public uint IsResimulating;
        public uint UntilFrame;
        public uint SuppressionFrame;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct RollbackLegacyProfileDTO
    {
        public uint Magic;
        public uint Version;
        public uint SimulatedPingMs;
        public uint JitterMs;
        public float PacketLoss01;
        public float PredictionAggressiveness;
        public uint MaxRollbackFrames;
        public uint Flags;
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
            float qualityCurve = Smooth01(qualityWeight);
            float emergencyFloor = math.lerp(0.22f, 0.35f, math.step(0.3f, qualityWeight));
            float budget = math.lerp(emergencyFloor, 1f, qualityCurve);
            return math.max(1, (int)math.round(maxFrames * budget));
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
    public unsafe struct StateSnapshotJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<AbsoluteUniversePosition> RigidbodyAups;
        [ReadOnly, NoAlias] public NativeArray<LockstepPlayerKinematicState> PlayerStates;
        [ReadOnly, NoAlias] public NativeArray<AbsoluteUniversePosition> EntityAups;
        [ReadOnly, NoAlias] public NativeArray<float3> EntityVelocities;
        [ReadOnly, NoAlias] public NativeArray<float> RoomWaterLevels;
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
        public uint InputMaskP1;
        public uint InputMaskP2;
        public uint ModQuarantineMask;

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
            header->RigidbodyAupCount = rigidbodyAupCount;
            header->PlayerStateCount = playerStateCount;
            header->EntityAupCount = entityAupCount;
            header->EntityVelocityCount = entityVelocityCount;
            header->RoomWaterCount = roomWaterCount;

            header->PayloadBytes = (uint)writtenBytes;
            int hashBytes = RollbackNetcodeConstants.SnapshotHeaderBytes + writtenBytes;
            header->FrameHash64 = RollbackNetcodeMath.HashExactBytes(page, hashBytes);

            int snapshotIndex = pageIndex;
            ref FrameSnapshotDTO snapshot = ref RollbackNetcodeBufferAccess.FrameSnapshotAt(FrameSnapshots, snapshotIndex);
            snapshot.FrameHash64 = header->FrameHash64;
            snapshot.InputMaskP1 = InputMaskP1;
            snapshot.InputMaskP2 = InputMaskP2;
            snapshot.MemoryOffset = (uint)pageOffset;
            snapshot.Reserved0 = 0u;

            if (RuntimeState.IsCreated && RuntimeState.Length > 0)
            {
                RollbackRuntimeStateDTO state = RuntimeState[0];
                state.CurrentFrame = Frame;
                state.LastFrameHash64 = header->FrameHash64;
                state.StateSnapshotBytes = (uint)hashBytes;
                state.StateMemoryOffset = (uint)pageOffset;
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

            if (!PredictedJournal.IsCreated || !RemoteInputRing.IsCreated || PredictedJournal.Length <= 0 || RemoteInputRing.Length <= 0)
            {
                state.Flags |= RollbackNetcodeFlags.MissingInputJournal;
                RuntimeState[0] = state;
                return;
            }

            int lookback = math.min(MaxRollbackFrames, math.min(PredictedJournal.Length, RemoteInputRing.Length));
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
                if (remote.Frame != frame || (remote.Flags & RemoteInputFlags.Received) == 0u)
                    continue;

                state.LastRemoteFrame = frame;
                if ((remote.Flags & RemoteInputFlags.ModQuarantined) != 0u)
                {
                    state.Flags |= RollbackNetcodeFlags.ModQuarantine;
                    continue;
                }

                InputStateDTO predicted = PredictedJournal[(int)(frame % (uint)PredictedJournal.Length)];
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
        [NoAlias] public NativeArray<byte> StateRingBuffer;
        [NoAlias] public NativeArray<FrameSnapshotDTO> FrameSnapshots;
        [NoAlias] public NativeArray<MockTickCommand> Commands;
        [NoAlias] public NativeArray<RollbackAudioSuppressionDTO> AudioSuppression;
        [NoAlias] public NativeArray<VisualStateDTO> VisualStates;
        [NoAlias] public NativeArray<NetcodeTelemetryEntry> Telemetry;
        [NoAlias] public NativeArray<AbsoluteUniversePosition> RigidbodyAups;
        [NoAlias] public NativeArray<LockstepPlayerKinematicState> PlayerStates;
        [NoAlias] public NativeArray<AbsoluteUniversePosition> EntityAups;
        [NoAlias] public NativeArray<float3> EntityVelocities;
        [NoAlias] public NativeArray<float> RoomWaterLevels;
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
                  RollbackNetcodeFlags.MismatchMask);
            RuntimeState[0] = state;

            DetectInputMismatchJob detect = new DetectInputMismatchJob
            {
                PredictedJournal = PredictedJournal,
                RemoteInputRing = RemoteInputRing,
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

            if (Tuning.IsCreated && Tuning.Length > 0)
                tuning = Tuning[0];

            tuning.MaxRollbackFrames = math.clamp(tuning.MaxRollbackFrames, 1, RollbackNetcodeConstants.MaxRollbackFrames);
            tuning.VisualInterpolationFrames = math.clamp(tuning.VisualInterpolationFrames, 1, 12);
            tuning.VisualInterpolationSeconds = math.clamp(tuning.VisualInterpolationSeconds, 0.016f, 0.25f);
            tuning.InputPredictionAggressiveness = math.saturate(tuning.InputPredictionAggressiveness);
            tuning.MinQualityForLookRollback = math.saturate(tuning.MinQualityForLookRollback);
            tuning.GlobalQualityWeight = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f);
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
                InputMaskP1 = ResolveInputMask(),
                InputMaskP2 = 0u,
                ModQuarantineMask = ModQuarantineMask
            };
            snapshot.Execute();
        }

        private void CheckRemoteHashFence()
        {
            if ((CurrentFrame % RollbackNetcodeConstants.DesyncHashCadenceFrames) != 0u ||
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

            state.Flags |= RollbackNetcodeFlags.HashMismatch |
                RollbackNetcodeFlags.DesyncPaused |
                RollbackNetcodeFlags.FullStateOverwriteRequested;
            state.DesyncCount++;
            RuntimeState[0] = state;
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

            NetcodeTelemetryEntry entry = default;
            entry.Frame = CurrentFrame;
            entry.LastRollbackFrame = state.LastRollbackFrame;
            entry.RollbacksTriggered = state.RollbacksTriggered;
            entry.FramesResimulated = state.FramesResimulated;
            entry.ResimComputeTimeMs = state.ResimComputeTimeMs;
            entry.GlobalQualityWeight = GlobalQualityWeight;
            entry.FrameHash64 = snapshot.FrameHash64;
            entry.RemoteHash64 = state.LastRemoteHash64;
            entry.Flags = state.Flags;
            entry.InputMaskP1 = ResolveInputMask();
            entry.InputMaskP2 = 0u;
            entry.StateMemoryOffset = snapshot.MemoryOffset;
            entry.SnapshotBytes = state.StateSnapshotBytes;
            entry.MismatchFrame = state.LastMismatchFrame;
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
