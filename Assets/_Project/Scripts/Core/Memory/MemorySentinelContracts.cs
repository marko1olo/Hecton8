using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Core.Memory
{
    public static class MemorySentinelConstants
    {
        public const int ValidationStateSizeBytes = 32;
        public const int TargetSizeBytes = 64;
        public const int ResultSizeBytes = 64;
        public const int RuntimeStateSizeBytes = 64;
        public const int TelemetryEntrySizeBytes = 64;
        public const int AupSnapshotSizeBytes = 64;
        public const int MockInventorySpanSizeBytes = 64;
        public const int ModQuarantineSpanSizeBytes = 64;
        public const int TelemetryCapacity = 300;
        public const uint ModPrefix16 = 0x4D50u;
        public const uint ModPrefix16LE = 0x504Du;
        public const uint ModPrefix32LE = 0x50444F4Du;
        public const uint ModPrefix32BE = 0x4D4F4450u;

        public const uint TargetFlagActive = 1u << 0;
        public const uint TargetFlagCritical = 1u << 1;
        public const uint TargetFlagRollback = 1u << 2;
        public const uint TargetFlagAup = 1u << 3;
        public const uint TargetFlagInventory = 1u << 4;
        public const uint TargetFlagAllowModPrefix = 1u << 5;
        public const uint TargetFlagMock = 1u << 6;
        public const uint TargetFlagPointerRegistry = 1u << 7;
        public const uint TargetFlagModQuarantine = 1u << 8;

        public const uint ResultFlagHashed = 1u << 0;
        public const uint ResultFlagMismatch = 1u << 1;
        public const uint ResultFlagPointerMismatch = 1u << 2;
        public const uint ResultFlagSkippedQuality = 1u << 3;
        public const uint ResultFlagSkippedCadence = 1u << 4;
        public const uint ResultFlagSkippedModQuarantine = 1u << 5;
        public const uint ResultFlagInvalidPointer = 1u << 6;
        public const uint ResultFlagEmergencySeed = 1u << 7;
        public const uint ResultFlagPointerFingerprintMismatch = 1u << 8;
    }

    [StructLayout(LayoutKind.Explicit, Size = MemorySentinelConstants.ValidationStateSizeBytes)]
    public unsafe struct ValidationStateDTO
    {
        [FieldOffset(0)] public ulong TargetMemoryPointer;
        [FieldOffset(8)] public uint ExpectedHash;
        [FieldOffset(12)] public uint StoredHash;
        [FieldOffset(16)] public uint CheckInterval;
        [FieldOffset(20)] public uint _pad0;
        [FieldOffset(24)] public ulong _pad1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref ValidationStateDTO ElementAt(void* basePointer, int index)
        {
            return ref UnsafeUtility.AsRef<ValidationStateDTO>(
                (byte*)basePointer + (index * UnsafeUtility.SizeOf<ValidationStateDTO>()));
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = MemorySentinelConstants.TargetSizeBytes)]
    public struct MemorySentinelTargetDTO
    {
        [FieldOffset(0)] public ulong TargetMemoryPointer;
        [FieldOffset(8)] public int ByteLength;
        [FieldOffset(12)] public int RollbackByteOffset;
        [FieldOffset(16)] public uint TargetHash;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint CheckInterval;
        [FieldOffset(28)] public uint LastLegalFrame;
        [FieldOffset(32)] public float MinQualityWeight;
        [FieldOffset(36)] public float Criticality01;
        [FieldOffset(40)] public uint ModdedGameMask;
        [FieldOffset(44)] public int BufferId;
        [FieldOffset(48)] public ulong TargetMemoryFingerprint;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = MemorySentinelConstants.ResultSizeBytes)]
    public struct MemorySentinelResultDTO
    {
        [FieldOffset(0)] public uint TargetHash;
        [FieldOffset(4)] public uint CalculatedHash;
        [FieldOffset(8)] public uint ExpectedHash;
        [FieldOffset(12)] public uint StoredHash;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public int ByteLength;
        [FieldOffset(28)] public int RollbackByteOffset;
        [FieldOffset(32)] public ulong FullHash64;
        [FieldOffset(40)] public float GlobalQualityWeight;
        [FieldOffset(44)] public float ValidationCostMicrosecondsEstimate;
        [FieldOffset(48)] public uint CheckInterval;
        [FieldOffset(52)] public uint LastLegalFrame;
        [FieldOffset(56)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = MemorySentinelConstants.RuntimeStateSizeBytes)]
    public struct MemorySentinelRuntimeStateDTO
    {
        [FieldOffset(0)] public float ValidationFrequencyHz;
        [FieldOffset(4)] public float AupTeleportToleranceMeters;
        [FieldOffset(8)] public float Strictness01;
        [FieldOffset(12)] public float GlobalQualityWeightOverride;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public float LastValidationMs;
        [FieldOffset(24)] public uint LastValidationFrame;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public int TargetCount;
        [FieldOffset(36)] public int LastCorrectedCount;
        [FieldOffset(40)] public int LastFatalCount;
        [FieldOffset(44)] public int ValidationCadenceFrames;
        [FieldOffset(48)] public uint ModdedGameMask;
        [FieldOffset(52)] public uint _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = MemorySentinelConstants.TelemetryEntrySizeBytes)]
    public struct MemorySentinelTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint BytesHashedPerFrame;
        [FieldOffset(8)] public uint DesyncsCorrected;
        [FieldOffset(12)] public uint DesyncsDetected;
        [FieldOffset(16)] public float ValidationComputeTimeMs;
        [FieldOffset(20)] public float GlobalQualityWeight;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint TargetCount;
        [FieldOffset(32)] public uint FatalCount;
        [FieldOffset(36)] public uint RollbackBytes;
        [FieldOffset(40)] public uint LastTargetHash;
        [FieldOffset(44)] public uint LastExpectedHash;
        [FieldOffset(48)] public uint LastCalculatedHash;
        [FieldOffset(52)] public uint ValidationCadenceFrames;
        [FieldOffset(56)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = MemorySentinelConstants.AupSnapshotSizeBytes)]
    public struct MemorySentinelAupSnapshotDTO
    {
        [FieldOffset(0)] public double3 GlobalPosition;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public float MaxMetersPerSecond;
        [FieldOffset(36)] public float LastDeltaMeters;
        [FieldOffset(40)] public float LastRequiredSpeedMetersPerSecond;
        [FieldOffset(44)] public uint _pad0;
        [FieldOffset(48)] public ulong _pad1;
        [FieldOffset(56)] public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = MemorySentinelConstants.MockInventorySpanSizeBytes)]
    public partial struct MockInventorySpan
    {
        [FieldOffset(0)] public ulong Word0;
        [FieldOffset(8)] public ulong Word1;
        [FieldOffset(16)] public ulong Word2;
        [FieldOffset(24)] public ulong Word3;
        [FieldOffset(32)] public ulong Word4;
        [FieldOffset(40)] public ulong Word5;
        [FieldOffset(48)] public ulong Word6;
        [FieldOffset(56)] public ulong Word7;
    }

    [StructLayout(LayoutKind.Explicit, Size = MemorySentinelConstants.ModQuarantineSpanSizeBytes)]
    public partial struct MemorySentinelModQuarantineSpan
    {
        [FieldOffset(0)] public uint Prefix;
        [FieldOffset(4)] public uint ModHash;
        [FieldOffset(8)] public uint MutationCounter;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public ulong Payload0;
        [FieldOffset(24)] public ulong Payload1;
        [FieldOffset(32)] public ulong Payload2;
        [FieldOffset(40)] public ulong Payload3;
        [FieldOffset(48)] public ulong Payload4;
        [FieldOffset(56)] public ulong Payload5;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MemorySentinelTunerSnapshotDTO
    {
        [FieldOffset(0)] public float ValidationFrequencyHz;
        [FieldOffset(4)] public float AupTeleportToleranceMeters;
        [FieldOffset(8)] public float Strictness01;
        [FieldOffset(12)] public float GlobalQualityWeight;
        [FieldOffset(16)] public float LastValidationMs;
        [FieldOffset(20)] public uint LastValidationFrame;
        [FieldOffset(24)] public uint TargetCount;
        [FieldOffset(28)] public uint LastCorrectedCount;
        [FieldOffset(32)] public uint LastFatalCount;
        [FieldOffset(36)] public uint LastBytesHashed;
        [FieldOffset(40)] public uint ModdedGameMask;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    public static unsafe class MemorySentinelMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint FoldHash64(uint2 hash)
        {
            return hash.x ^ math.rol(hash.y, 13);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ComputeDeterministicHash32(void* ptr, int byteLength, ulong seed = 0UL)
        {
            return FoldDeterministicHash64(ComputeDeterministicHash64(ptr, byteLength, seed));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ComputeDeterministicHash64(void* ptr, int byteLength, ulong seed = 0UL)
        {
            if (ptr == null || byteLength <= 0)
                return 0UL;

            byte* bytes = (byte*)ptr;
            ulong hash = 14695981039346656037UL ^ seed ^ ((ulong)(uint)byteLength << 32);
            for (int i = 0; i < byteLength; i++)
            {
                hash ^= bytes[i];
                hash *= 1099511628211UL;
            }

            return Avalanche64(hash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint FoldDeterministicHash64(ulong hash)
        {
            return (uint)hash ^ (uint)(hash >> 32);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Avalanche64(ulong hash)
        {
            hash ^= hash >> 33;
            hash *= 0xff51afd7ed558ccdUL;
            hash ^= hash >> 33;
            hash *= 0xc4ceb9fe1a85ec53UL;
            hash ^= hash >> 33;
            return hash == 0UL ? 1UL : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ComputeXXHash3Folded(ReadOnlySpan<byte> bytes)
        {
            fixed (byte* ptr = bytes)
                return ComputeXXHash3Folded(ptr, bytes.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ComputeXXHash3Folded(NativeArray<byte> bytes)
        {
            if (!bytes.IsCreated)
                return 0u;

            return ComputeXXHash3Folded(bytes.GetUnsafeReadOnlyPtr(), bytes.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ComputeXXHash3Folded(void* ptr, int byteLength)
        {
            if (ptr == null || byteLength <= 0)
                return 0u;

            return ComputeDeterministicHash32(ptr, byteLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ComputeXXHash3Full64(ReadOnlySpan<byte> bytes)
        {
            fixed (byte* ptr = bytes)
                return ComputeXXHash3Full64(ptr, bytes.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ComputeXXHash3Full64(NativeArray<byte> bytes)
        {
            if (!bytes.IsCreated)
                return 0UL;

            return ComputeXXHash3Full64(bytes.GetUnsafeReadOnlyPtr(), bytes.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ComputeXXHash3Full64(void* ptr, int byteLength)
        {
            if (ptr == null || byteLength <= 0)
                return 0UL;

            return ComputeDeterministicHash64(ptr, byteLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ComputeTargetHash(int bufferId, int byteOffset)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)bufferId) * 16777619u;
                hash = (hash ^ (uint)byteOffset) * 16777619u;
                return hash == 0u ? 1u : hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ComputePointerFingerprint(ulong pointer, int byteLength, int bufferId)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                hash = (hash ^ pointer) * 1099511628211UL;
                hash = (hash ^ (uint)byteLength) * 1099511628211UL;
                hash = (hash ^ (uint)bufferId) * 1099511628211UL;
                return hash == 0UL ? 1UL : hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasModPrefix(ReadOnlySpan<byte> bytes)
        {
            fixed (byte* ptr = bytes)
                return HasModPrefix(ptr, bytes.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasModPrefix(NativeArray<byte> bytes)
        {
            if (!bytes.IsCreated)
                return false;

            return HasModPrefix((byte*)bytes.GetUnsafeReadOnlyPtr(), bytes.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasModPrefix(byte* ptr, int byteLength)
        {
            if (ptr == null || byteLength < 2)
                return false;

            ushort prefix16 = UnsafeUtility.ReadArrayElement<ushort>(ptr, 0);
            if (prefix16 == MemorySentinelConstants.ModPrefix16 ||
                prefix16 == MemorySentinelConstants.ModPrefix16LE)
                return true;

            if (byteLength < 4)
                return false;

            uint prefix32 = UnsafeUtility.ReadArrayElement<uint>(ptr, 0);
            return prefix32 == MemorySentinelConstants.ModPrefix32LE ||
                   prefix32 == MemorySentinelConstants.ModPrefix32BE;
        }

    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct MemorySentinelValidationJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<ValidationStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<MemorySentinelTargetDTO> Targets;
        [NoAlias] public NativeArray<MemorySentinelResultDTO> Results;
        public uint Frame;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            ValidationStateDTO state = States[index];
            MemorySentinelTargetDTO target = Targets[index];
            MemorySentinelResultDTO result = default;
            result.TargetHash = target.TargetHash;
            result.ExpectedHash = state.ExpectedHash;
            result.StoredHash = state.StoredHash;
            result.Frame = Frame;
            result.ByteLength = target.ByteLength;
            result.RollbackByteOffset = target.RollbackByteOffset;
            result.GlobalQualityWeight = GlobalQualityWeight;
            result.CheckInterval = state.CheckInterval;
            result.LastLegalFrame = target.LastLegalFrame;

            if ((target.Flags & MemorySentinelConstants.TargetFlagActive) == 0u)
            {
                Results[index] = result;
                return;
            }

            float minQuality = math.saturate(target.MinQualityWeight);
            float quality = math.saturate(math.select(0f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
            float qualityDeficit = math.saturate((minQuality - quality) * math.rcp(math.max(0.0001f, minQuality + 0.0001f)));
            qualityDeficit = qualityDeficit * qualityDeficit * (3f - 2f * qualityDeficit);

            uint interval = math.max(1u, math.max(state.CheckInterval, target.CheckInterval));
            uint qualityInterval = (uint)math.clamp((int)math.round(math.lerp(1f, 64f, qualityDeficit)), 1, 64);
            interval = math.max(interval, qualityInterval);
            if (state.ExpectedHash != 0u && interval > 1u)
            {
                uint cadencePhase = target.TargetHash == 0u ? 0u : target.TargetHash % interval;
                if ((Frame % interval) != cadencePhase)
                {
                    result.Flags |= MemorySentinelConstants.ResultFlagSkippedCadence;
                    Results[index] = result;
                    return;
                }
            }

            ulong pointer = target.TargetMemoryPointer;
            if (state.TargetMemoryPointer != pointer)
                result.Flags |= MemorySentinelConstants.ResultFlagPointerMismatch;

            if (pointer == 0UL || target.ByteLength <= 0)
            {
                result.Flags |= MemorySentinelConstants.ResultFlagInvalidPointer;
                Results[index] = result;
                return;
            }

            ulong fingerprint = MemorySentinelMath.ComputePointerFingerprint(
                pointer,
                target.ByteLength,
                target.BufferId);
            if (target.TargetMemoryFingerprint != 0UL && target.TargetMemoryFingerprint != fingerprint)
                result.Flags |= MemorySentinelConstants.ResultFlagPointerFingerprintMismatch;

            byte* ptr = (byte*)pointer;
            if ((target.Flags & MemorySentinelConstants.TargetFlagAllowModPrefix) != 0u &&
                target.ModdedGameMask != 0u &&
                MemorySentinelMath.HasModPrefix(ptr, target.ByteLength))
            {
                result.Flags |= MemorySentinelConstants.ResultFlagSkippedModQuarantine;
                Results[index] = result;
                return;
            }

            ulong fullHash = MemorySentinelMath.ComputeDeterministicHash64(ptr, target.ByteLength);
            uint calculated = MemorySentinelMath.FoldDeterministicHash64(fullHash);
            result.FullHash64 = fullHash;
            result.CalculatedHash = calculated;
            result.Flags |= MemorySentinelConstants.ResultFlagHashed;

            state.StoredHash = calculated;
            if (state.ExpectedHash == 0u)
            {
                state.ExpectedHash = calculated;
                result.ExpectedHash = calculated;
                result.Flags |= MemorySentinelConstants.ResultFlagEmergencySeed;
            }
            else if (calculated != state.ExpectedHash)
            {
                result.Flags |= MemorySentinelConstants.ResultFlagMismatch;
            }

            result.ExpectedHash = state.ExpectedHash;
            result.StoredHash = state.StoredHash;
            States[index] = state;
            Results[index] = result;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct MockInventoryByteMutationJob : IJob
    {
        [NoAlias] public NativeArray<MockInventorySpan> MockInventory;
        public uint Frame;
        public int MutationByteCount;

        public void Execute()
        {
            if (!MockInventory.IsCreated || MockInventory.Length <= 0)
                return;

            int count = math.clamp(MutationByteCount, 1, 4);
            byte* bytes = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(MockInventory);
            for (int i = 0; i < count; i++)
            {
                int byteIndex = (int)((Frame * 17u + 13u + (uint)(i * 7)) & 63u);
                bytes[byteIndex] ^= (byte)(0x5Au + ((Frame + (uint)i) & 31u));
            }
        }
    }
}
