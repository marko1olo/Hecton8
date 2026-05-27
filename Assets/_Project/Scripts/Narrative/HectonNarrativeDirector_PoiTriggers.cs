using System;
using System.Diagnostics;
using System.IO;
#if UNITY_STANDALONE || UNITY_EDITOR
using System.IO.MemoryMappedFiles;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Generated;
using Hecton8.Core.Memory;
using Hecton8.Interaction;
using Hecton8.Narrative;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct NarrativePoiDTO
    {
        [FieldOffset(0)] public double3 PoiAUP;
        [FieldOffset(24)] public uint EventHashID;
        [FieldOffset(28)] public float TriggerRadiusMeters;
        [FieldOffset(32)] public ulong PrerequisiteBitmask;
        [FieldOffset(40)] public uint StateFlags;
        [FieldOffset(44)] private uint _pad0;
        [FieldOffset(48)] private uint _pad1;
        [FieldOffset(52)] private uint _pad2;
        [FieldOffset(56)] private uint _pad3;
        [FieldOffset(60)] private uint _pad4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct NarrativePoiPresentationDTO
    {
        [FieldOffset(0)] public uint PoiHash;
        [FieldOffset(4)] public uint QuestHash;
        [FieldOffset(8)] public uint BiomeHash;
        [FieldOffset(12)] public uint SoundscapeHash;
        [FieldOffset(16)] public uint LoreHash;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public int BitIndex;
        [FieldOffset(28)] public uint Reserved0;
        [FieldOffset(32)] private ulong _pad0;
        [FieldOffset(40)] private ulong _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct NarrativePoiBucketRangeDTO
    {
        [FieldOffset(0)] public int CellHash;
        [FieldOffset(4)] public int StartIndex;
        [FieldOffset(8)] public int Count;
        [FieldOffset(12)] public int Capacity;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public uint CollisionCount;
        [FieldOffset(24)] private uint _pad0;
        [FieldOffset(28)] private uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AupNarrativeTriggerTelemetryEntry
    {
        [FieldOffset(0)] public double ExecutionTimeMicroseconds;
        [FieldOffset(8)] public double3 PlayerAUP;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public int EvaluatedPoiCount;
        [FieldOffset(40)] public int SignalsEmitted;
        [FieldOffset(44)] public uint PlayerCellHash;
        [FieldOffset(48)] public ulong GlobalProgressionMask;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint StateHash;
    }

    public static class NarrativePoiStateFlags
    {
        public const uint Active = 1u << 0;
        public const uint Triggered = 1u << 1;
        public const uint Exhausted = 1u << 2;
        public const uint Inside = 1u << 3;
        public const uint InvalidAup = 1u << 4;
        public const uint PrerequisiteBlocked = 1u << 5;
        public const uint BucketOverflow = 1u << 6;
        public const uint DispatchPending = 1u << 7;
        public const uint Dispatched = 1u << 8;
        public const int BitIndexShift = 16;
        public const uint BitIndexMask = 0x3Fu << BitIndexShift;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint EncodeBitIndex(int bitIndex)
        {
            return ((uint)math.clamp(bitIndex, 0, 63) << BitIndexShift) & BitIndexMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DecodeBitIndex(uint stateFlags)
        {
            return (int)((stateFlags & BitIndexMask) >> BitIndexShift);
        }
    }

    public static class NarrativePoiSpatialHash
    {
        public const int CellSizeMeters = 100;
        private const int PrimeX = 73856093;
        private const int PrimeY = 19349663;
        private const int PrimeZ = 83492791;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 AupToCell(double3 aup)
        {
            double inv = 1.0d / CellSizeMeters;
            return new int3(
                (int)math.floor(aup.x * inv),
                (int)math.floor(aup.y * inv),
                (int)math.floor(aup.z * inv));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HashAupToCell(double3 aup)
        {
            return HashCell(AupToCell(aup));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HashCell(int3 cell)
        {
            return (cell.x * PrimeX) ^ (cell.y * PrimeY) ^ (cell.z * PrimeZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PositiveModulo(int value, int length)
        {
            int result = value % length;
            return result < 0 ? result + length : result;
        }
    }

    public static class NarrativePoiBucketRangeFlags
    {
        public const uint Occupied = 1u << 0;
        public const uint Overflow = 1u << 1;
    }

    public static class AupNarrativePoiRuntimeConstants
    {
        public const int DefaultPoiCapacity = 10000;
        public const int DefaultBucketCount = 4096;
        public const int BucketStride = 8;
        public const int DefaultStateMaskCount = (DefaultPoiCapacity + 63) / 64;
        public const int TelemetryCapacity = 300;
        public const int CounterCount = 16;
        public const uint SignalLaneHash = 0x53483439u;
        public const byte ProgressionSourceNarrativePoi = 1;
        public const double FaultDumpMicroseconds = 100.0d;
        public const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_349.bin";

        public enum CounterSlot : int
        {
            PoiCount = 0,
            BucketCount = 1,
            BucketStride = 2,
            LastEvaluatedPoiCount = 3,
            LastSignalsEmitted = 4,
            FaultCount = 5,
            DumpCount = 6,
            SpatialVersion = 7,
            LastPlayerCellHash = 8,
            LastTelemetryFlags = 9,
            MockPoiCount = 10,
            PendingScheduleDropCount = 11
        }

        public enum TelemetryFlags : uint
        {
            None = 0u,
            InvalidPlayerAup = 1u << 0,
            InvalidPoiAup = 1u << 1,
            BucketMiss = 1u << 2,
            BucketOverflow = 1u << 3,
            ExceededTimeBudget = 1u << 4,
            MockData = 1u << 5,
            NoLocalBucket = 1u << 6
        }
    }

    public static class NarrativePoiStateMaskWords
    {
        public const int BitsPerWord = 64;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WordCountForCapacity(int capacity)
        {
            return math.max(1, (math.max(0, capacity) + BitsPerWord - 1) >> 6);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryResolve(int poiIndex, int wordCount, out int wordIndex, out ulong bitMask)
        {
            wordIndex = poiIndex >> 6;
            bitMask = 1UL << (poiIndex & 63);
            return poiIndex >= 0 && (uint)wordIndex < (uint)wordCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClearAll(NativeArray<ulong> masks)
        {
            for (int i = 0; i < masks.Length; i++)
                masks[i] = 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSet(NativeArray<ulong> masks, int poiIndex)
        {
            return masks.IsCreated &&
                   TryResolve(poiIndex, masks.Length, out int wordIndex, out ulong bitMask) &&
                   (masks[wordIndex] & bitMask) != 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Set(NativeArray<ulong> masks, int poiIndex)
        {
            if (!masks.IsCreated ||
                !TryResolve(poiIndex, masks.Length, out int wordIndex, out ulong bitMask))
            {
                return false;
            }

            masks[wordIndex] = masks[wordIndex] | bitMask;
            return true;
        }
    }

    public struct AupNarrativePoiBufferHandles
    {
        public VaultGenerationHandle<NarrativePoiDTO> Pois;
        public VaultGenerationHandle<NarrativePoiPresentationDTO> Presentation;
        public VaultGenerationHandle<NarrativePoiBucketRangeDTO> BucketRanges;
        public VaultGenerationHandle<int> BucketIndices;
        public VaultGenerationHandle<ulong> StateMasks;
        public VaultGenerationHandle<AupNarrativeTriggerTelemetryEntry> TelemetryRing;
        public VaultGenerationHandle<int> TelemetryCursor;
        public VaultGenerationHandle<int> Counters;
        public VaultGenerationHandle<long> CsvScratch;
        public int PoiCapacity;
        public int BucketCount;
        public int BucketStride;
        public int StateMaskCount;
    }

    public ref struct AupNarrativePoiBuffers
    {
        public NativeArray<NarrativePoiDTO> Pois;
        public NativeArray<NarrativePoiPresentationDTO> Presentation;
        public NativeArray<NarrativePoiBucketRangeDTO> BucketRanges;
        public NativeArray<int> BucketIndices;
        public NativeArray<ulong> StateMasks;
        public NativeArray<AupNarrativeTriggerTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
        public NativeArray<int> Counters;
        public NativeArray<long> CsvScratch;
    }

    public static class AupNarrativePoiVault
    {
        private const SystemID Owner = SystemID.NarrativePoiTriggers;

        public static AupNarrativePoiBufferHandles EnsureBuffers(
            IDataVault vault,
            int poiCapacity = AupNarrativePoiRuntimeConstants.DefaultPoiCapacity,
            int bucketCount = AupNarrativePoiRuntimeConstants.DefaultBucketCount,
            int bucketStride = AupNarrativePoiRuntimeConstants.BucketStride)
        {
            AupNarrativePoiBufferHandles handles = default;
            if (vault == null)
                return handles;

            poiCapacity = math.max(1, poiCapacity);
            bucketCount = math.max(1, bucketCount);
            bucketStride = math.max(1, bucketStride);
            handles.PoiCapacity = poiCapacity;
            handles.BucketCount = bucketCount;
            handles.BucketStride = bucketStride;
            handles.StateMaskCount = NarrativePoiStateMaskWords.WordCountForCapacity(poiCapacity);
            handles.Pois = vault.EnsureGenerationHandle<NarrativePoiDTO>(
                BufferID.NarrativePoiTriggers,
                poiCapacity,
                Owner,
                NativeArrayOptions.UninitializedMemory);
            handles.Presentation = vault.EnsureGenerationHandle<NarrativePoiPresentationDTO>(
                BufferID.NarrativePoiPresentation,
                poiCapacity,
                Owner,
                NativeArrayOptions.UninitializedMemory);
            handles.BucketRanges = vault.EnsureGenerationHandle<NarrativePoiBucketRangeDTO>(
                BufferID.NarrativePoiBucketRanges,
                bucketCount,
                Owner,
                NativeArrayOptions.UninitializedMemory);
            handles.BucketIndices = vault.EnsureGenerationHandle<int>(
                BufferID.NarrativePoiBucketIndices,
                bucketCount * bucketStride,
                Owner,
                NativeArrayOptions.UninitializedMemory);
            handles.StateMasks = vault.EnsureGenerationHandle<ulong>(
                BufferID.NarrativePoiStateMasks,
                handles.StateMaskCount,
                Owner,
                NativeArrayOptions.UninitializedMemory);
            handles.TelemetryRing = vault.EnsureGenerationHandle<AupNarrativeTriggerTelemetryEntry>(
                BufferID.NarrativePoiTelemetryRing,
                AupNarrativePoiRuntimeConstants.TelemetryCapacity,
                Owner,
                NativeArrayOptions.UninitializedMemory);
            handles.TelemetryCursor = vault.EnsureGenerationHandle<int>(
                BufferID.NarrativePoiTelemetryCursor,
                1,
                Owner,
                NativeArrayOptions.UninitializedMemory);
            handles.Counters = vault.EnsureGenerationHandle<int>(
                BufferID.NarrativePoiCounters,
                AupNarrativePoiRuntimeConstants.CounterCount,
                Owner,
                NativeArrayOptions.UninitializedMemory);
            handles.CsvScratch = vault.EnsureGenerationHandle<long>(
                BufferID.NarrativePoiCsvScratch,
                4,
                Owner,
                NativeArrayOptions.UninitializedMemory);
            return handles;
        }

        public static bool TryResolveBuffers(
            IDataVault vault,
            ref AupNarrativePoiBufferHandles handles,
            out AupNarrativePoiBuffers buffers)
        {
            buffers = default;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!TryResolveBuffer(vault, ref handles.Pois, BufferID.NarrativePoiTriggers, handles.PoiCapacity, out buffers.Pois) ||
                !TryResolveBuffer(vault, ref handles.Presentation, BufferID.NarrativePoiPresentation, handles.PoiCapacity, out buffers.Presentation) ||
                !TryResolveBuffer(vault, ref handles.BucketRanges, BufferID.NarrativePoiBucketRanges, handles.BucketCount, out buffers.BucketRanges) ||
                !TryResolveBuffer(vault, ref handles.BucketIndices, BufferID.NarrativePoiBucketIndices, handles.BucketCount * handles.BucketStride, out buffers.BucketIndices) ||
                !TryResolveBuffer(vault, ref handles.StateMasks, BufferID.NarrativePoiStateMasks, math.max(1, handles.StateMaskCount), out buffers.StateMasks) ||
                !TryResolveBuffer(vault, ref handles.TelemetryRing, BufferID.NarrativePoiTelemetryRing, AupNarrativePoiRuntimeConstants.TelemetryCapacity, out buffers.TelemetryRing) ||
                !TryResolveBuffer(vault, ref handles.TelemetryCursor, BufferID.NarrativePoiTelemetryCursor, 1, out buffers.TelemetryCursor) ||
                !TryResolveBuffer(vault, ref handles.Counters, BufferID.NarrativePoiCounters, AupNarrativePoiRuntimeConstants.CounterCount, out buffers.Counters) ||
                !TryResolveBuffer(vault, ref handles.CsvScratch, BufferID.NarrativePoiCsvScratch, 4, out buffers.CsvScratch))
            {
                buffers = default;
                return false;
            }

            return true;
        }

        public static void ReleaseBuffers(IDataVault vault, ref AupNarrativePoiBufferHandles handles)
        {
            ReleaseBuffer(vault, ref handles.Pois, BufferID.NarrativePoiTriggers);
            ReleaseBuffer(vault, ref handles.Presentation, BufferID.NarrativePoiPresentation);
            ReleaseBuffer(vault, ref handles.BucketRanges, BufferID.NarrativePoiBucketRanges);
            ReleaseBuffer(vault, ref handles.BucketIndices, BufferID.NarrativePoiBucketIndices);
            ReleaseBuffer(vault, ref handles.StateMasks, BufferID.NarrativePoiStateMasks);
            ReleaseBuffer(vault, ref handles.TelemetryRing, BufferID.NarrativePoiTelemetryRing);
            ReleaseBuffer(vault, ref handles.TelemetryCursor, BufferID.NarrativePoiTelemetryCursor);
            ReleaseBuffer(vault, ref handles.Counters, BufferID.NarrativePoiCounters);
            ReleaseBuffer(vault, ref handles.CsvScratch, BufferID.NarrativePoiCsvScratch);
            handles = default;
        }

        private static bool TryResolveBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            return IsHandle(in handle, bufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)Owner &&
                   handle.Generation != 0u;
        }

        private static void ReleaseBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault != null && IsHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BuildNarrativePoiBucketsJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<NarrativePoiDTO> Pois;
        [NoAlias] public NativeArray<NarrativePoiBucketRangeDTO> BucketRanges;
        [NoAlias] public NativeArray<int> BucketIndices;
        [NoAlias] public NativeArray<int> Counters;
        public int PoiCount;
        public int BucketStride;

        public void Execute()
        {
            int bucketCount = BucketRanges.Length;
            int stride = math.max(1, BucketStride);
            int indexCapacity = BucketIndices.Length;
            for (int i = 0; i < bucketCount; i++)
            {
                BucketRanges[i] = new NarrativePoiBucketRangeDTO
                {
                    CellHash = 0,
                    StartIndex = i * stride,
                    Count = 0,
                    Capacity = math.min(stride, math.max(0, indexCapacity - (i * stride))),
                    Flags = 0u,
                    CollisionCount = 0u
                };
            }

            int count = math.min(PoiCount, Pois.Length);
            int faults = 0;
            for (int i = 0; i < count; i++)
            {
                NarrativePoiDTO poi = Pois[i];
                if ((poi.StateFlags & NarrativePoiStateFlags.Active) == 0u ||
                    poi.EventHashID == 0u ||
                    !math.all(math.isfinite(poi.PoiAUP)) ||
                    !math.isfinite(poi.TriggerRadiusMeters) ||
                    poi.TriggerRadiusMeters <= 0f)
                {
                    continue;
                }

                int hash = NarrativePoiSpatialHash.HashAupToCell(poi.PoiAUP);
                if (!TryInsert(hash, i, bucketCount, stride))
                    faults++;
            }

            WriteCounter(AupNarrativePoiRuntimeConstants.CounterSlot.BucketCount, bucketCount);
            WriteCounter(AupNarrativePoiRuntimeConstants.CounterSlot.BucketStride, stride);
            WriteCounter(AupNarrativePoiRuntimeConstants.CounterSlot.FaultCount, faults);
        }

        private bool TryInsert(int hash, int poiIndex, int bucketCount, int stride)
        {
            int slot = NarrativePoiSpatialHash.PositiveModulo(hash, bucketCount);
            for (int probe = 0; probe < bucketCount; probe++)
            {
                NarrativePoiBucketRangeDTO range = BucketRanges[slot];
                bool occupied = (range.Flags & 1u) != 0u;
                if (!occupied)
                {
                    range.CellHash = hash;
                    range.Count = 0;
                    range.Flags = NarrativePoiBucketRangeFlags.Occupied;
                    range.CollisionCount = (uint)probe;
                    BucketRanges[slot] = range;
                    occupied = true;
                }

                if (occupied && range.CellHash == hash)
                {
                    if (range.Count >= range.Capacity)
                    {
                        range.Flags |= NarrativePoiBucketRangeFlags.Overflow;
                        BucketRanges[slot] = range;
                        return false;
                    }

                    int writeIndex = range.StartIndex + range.Count;
                    if ((uint)writeIndex >= (uint)BucketIndices.Length)
                        return false;

                    BucketIndices[writeIndex] = poiIndex;
                    range.Count++;
                    BucketRanges[slot] = range;
                    return true;
                }

                slot++;
                if (slot >= bucketCount)
                    slot = 0;
            }

            return false;
        }

        private void WriteCounter(AupNarrativePoiRuntimeConstants.CounterSlot slot, int value)
        {
            int index = (int)slot;
            if (Counters.IsCreated && (uint)index < (uint)Counters.Length)
                Counters[index] = value;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockPoiTriggersJob : IJob
    {
        [NoAlias] public NativeArray<NarrativePoiDTO> Pois;
        [NoAlias] public NativeArray<NarrativePoiBucketRangeDTO> BucketRanges;
        [NoAlias] public NativeArray<int> BucketIndices;
        [NoAlias] public NativeArray<ulong> StateMasks;
        [NoAlias] public NativeArray<int> Counters;
        public double3 OriginAUP;
        public int RequestedCount;
        public int BucketStride;
        public uint Seed;

        public void Execute()
        {
            int count = math.min(math.max(0, RequestedCount), Pois.Length);
            uint seed = Seed != 0u ? Seed : 2166136261u;
            for (int i = 0; i < count; i++)
            {
                int x = i % 100;
                int z = (i / 100) % 100;
                int y = i / 10000;
                double3 offset = new double3((x - 50) * 60.0d, y * 25.0d, (z - 50) * 60.0d);
                uint eventHash = HashIndex(seed, i);
                int bitIndex = i & 63;
                ulong prereq = (i & 7) == 0 ? 0UL : (1UL << ((i - 1) & 63));
                Pois[i] = new NarrativePoiDTO
                {
                    PoiAUP = OriginAUP + offset,
                    EventHashID = eventHash,
                    TriggerRadiusMeters = 18f + (float)(i & 3),
                    PrerequisiteBitmask = prereq,
                    StateFlags = NarrativePoiStateFlags.Active | NarrativePoiStateFlags.EncodeBitIndex(bitIndex)
                };
            }

            for (int i = count; i < Pois.Length; i++)
                Pois[i] = default;

            if (StateMasks.IsCreated)
                NarrativePoiStateMaskWords.ClearAll(StateMasks);

            if (Counters.IsCreated)
            {
                Counters[(int)AupNarrativePoiRuntimeConstants.CounterSlot.PoiCount] = count;
                Counters[(int)AupNarrativePoiRuntimeConstants.CounterSlot.MockPoiCount] = count;
            }

            BuildNarrativePoiBucketsJob buildJob = new BuildNarrativePoiBucketsJob
            {
                Pois = Pois,
                BucketRanges = BucketRanges,
                BucketIndices = BucketIndices,
                Counters = Counters,
                PoiCount = count,
                BucketStride = BucketStride
            };
            buildJob.Execute();
        }

        private static uint HashIndex(uint seed, int index)
        {
            uint value = seed ^ unchecked((uint)index);
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return value != 0u ? value : 1u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluatePoiTriggersJob : IJob
    {
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<NarrativePoiDTO> Pois;
        [ReadOnly, NoAlias] public NativeArray<NarrativePoiPresentationDTO> Presentation;
        [ReadOnly, NoAlias] public NativeArray<NarrativePoiBucketRangeDTO> BucketRanges;
        [ReadOnly, NoAlias] public NativeArray<int> BucketIndices;
        [NoAlias] public NativeArray<ulong> PoiStateMasks;
        [NoAlias] public NativeArray<AupNarrativeTriggerTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        [NoAlias] public NativeArray<int> Counters;
        [NoAlias] public global::Hecton8.Core.MpscSignalRingBuffer<ProgressionEventSignal>.ParallelWriter ProgressionWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> ProgressionWriterBudget;
        public double3 PlayerAUP;
        public ulong GlobalNarrativeStateMask;
        public uint Frame;
        public int PoiCount;

        public void Execute()
        {
            int evaluated = 0;
            int signals = 0;
            uint flags = 0u;
            int playerCellHash = 0;
            ulong stateMask = PoiStateMasks.IsCreated && PoiStateMasks.Length > 0 ? PoiStateMasks[0] : 0UL;

            if (!math.all(math.isfinite(PlayerAUP)))
            {
                flags |= (uint)AupNarrativePoiRuntimeConstants.TelemetryFlags.InvalidPlayerAup;
                WriteTelemetry(0, 0, 0u, flags, stateMask);
                WriteCounter(AupNarrativePoiRuntimeConstants.CounterSlot.FaultCount, 1);
                return;
            }

            playerCellHash = NarrativePoiSpatialHash.HashAupToCell(PlayerAUP);
            if (!TryFindBucket(playerCellHash, out NarrativePoiBucketRangeDTO range))
            {
                flags |= (uint)AupNarrativePoiRuntimeConstants.TelemetryFlags.NoLocalBucket;
                WriteTelemetry(0, 0, unchecked((uint)playerCellHash), flags, stateMask);
                WriteCounter(AupNarrativePoiRuntimeConstants.CounterSlot.LastEvaluatedPoiCount, 0);
                WriteCounter(AupNarrativePoiRuntimeConstants.CounterSlot.LastSignalsEmitted, 0);
                return;
            }

            if ((range.Flags & NarrativePoiBucketRangeFlags.Overflow) != 0u)
                flags |= (uint)AupNarrativePoiRuntimeConstants.TelemetryFlags.BucketOverflow;

            void* poiPtr = Pois.GetUnsafePtr();
            int count = math.min(range.Count, range.Capacity);
            for (int i = 0; i < count; i++)
            {
                int index = range.StartIndex + i;
                if ((uint)index >= (uint)BucketIndices.Length)
                    continue;

                int poiIndex = BucketIndices[index];
                if ((uint)poiIndex >= (uint)PoiCount || (uint)poiIndex >= (uint)Pois.Length)
                    continue;

                evaluated++;
                ref NarrativePoiDTO poi = ref UnsafeUtility.ArrayElementAsRef<NarrativePoiDTO>(poiPtr, poiIndex);
                uint poiFlags = poi.StateFlags;
                if ((poiFlags & NarrativePoiStateFlags.Active) == 0u)
                    continue;

                if (!math.all(math.isfinite(poi.PoiAUP)) ||
                    !math.isfinite(poi.TriggerRadiusMeters) ||
                    poi.TriggerRadiusMeters <= 0f)
                {
                    poi.StateFlags = poiFlags | NarrativePoiStateFlags.InvalidAup;
                    flags |= (uint)AupNarrativePoiRuntimeConstants.TelemetryFlags.InvalidPoiAup;
                    continue;
                }

                if ((GlobalNarrativeStateMask & poi.PrerequisiteBitmask) != poi.PrerequisiteBitmask)
                {
                    poi.StateFlags = (poiFlags | NarrativePoiStateFlags.PrerequisiteBlocked) &
                                     ~(NarrativePoiStateFlags.Triggered |
                                       NarrativePoiStateFlags.Inside |
                                       NarrativePoiStateFlags.DispatchPending);
                    continue;
                }

                poiFlags &= ~NarrativePoiStateFlags.PrerequisiteBlocked;
                double3 deltaDouble = PlayerAUP - poi.PoiAUP;
                float3 delta = (float3)deltaDouble;
                float distanceSq = math.lengthsq(delta);
                float radius = math.max(0.0001f, poi.TriggerRadiusMeters);
                float radiusSq = radius * radius;
                if (!math.isfinite(distanceSq))
                {
                    poi.StateFlags = poiFlags | NarrativePoiStateFlags.InvalidAup;
                    flags |= (uint)AupNarrativePoiRuntimeConstants.TelemetryFlags.InvalidPoiAup;
                    continue;
                }

                if (distanceSq <= radiusSq)
                {
                    poiFlags |= NarrativePoiStateFlags.Triggered | NarrativePoiStateFlags.Inside;
                    if ((poiFlags & NarrativePoiStateFlags.Exhausted) == 0u)
                    {
                        poiFlags |= NarrativePoiStateFlags.Exhausted | NarrativePoiStateFlags.DispatchPending;
                        NarrativePoiStateMaskWords.Set(PoiStateMasks, poiIndex);
                        stateMask = PoiStateMasks.IsCreated && PoiStateMasks.Length > 0 ? PoiStateMasks[0] : 0UL;
                        NarrativePoiPresentationDTO presentation = (Presentation.IsCreated && (uint)poiIndex < (uint)Presentation.Length)
                            ? Presentation[poiIndex]
                            : default;
                        uint questHash = presentation.QuestHash != 0u ? presentation.QuestHash : poi.EventHashID;
                        byte signalFlags = (byte)((presentation.Flags & 0xFFu) | 1u);
                        SignalBus<ProgressionEventSignal>.TryEnqueueBounded(ProgressionWriter, ProgressionWriterBudget, new ProgressionEventSignal
                        {
                            PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(poi.PoiAUP),
                            PoiHash = poi.EventHashID,
                            QuestHash = questHash,
                            Frame = Frame,
                            Source = AupNarrativePoiRuntimeConstants.ProgressionSourceNarrativePoi,
                            Flags = signalFlags
                        });
                        signals++;
                    }

                    poi.StateFlags = poiFlags;
                    continue;
                }

                if ((poiFlags & (NarrativePoiStateFlags.Triggered | NarrativePoiStateFlags.Inside)) != 0u)
                {
                    float exitRadius = radius * 1.2f;
                    if (distanceSq > exitRadius * exitRadius)
                        poiFlags &= ~(NarrativePoiStateFlags.Triggered | NarrativePoiStateFlags.Inside);
                }

                poi.StateFlags = poiFlags;
            }

            if (PoiStateMasks.IsCreated && PoiStateMasks.Length > 0)
                PoiStateMasks[0] = stateMask;

            WriteTelemetry(evaluated, signals, unchecked((uint)playerCellHash), flags, stateMask);
            WriteCounter(AupNarrativePoiRuntimeConstants.CounterSlot.LastEvaluatedPoiCount, evaluated);
            WriteCounter(AupNarrativePoiRuntimeConstants.CounterSlot.LastSignalsEmitted, signals);
            WriteCounter(AupNarrativePoiRuntimeConstants.CounterSlot.LastPlayerCellHash, playerCellHash);
            WriteCounter(AupNarrativePoiRuntimeConstants.CounterSlot.LastTelemetryFlags, unchecked((int)flags));
        }

        private bool TryFindBucket(int hash, out NarrativePoiBucketRangeDTO range)
        {
            range = default;
            int bucketCount = BucketRanges.Length;
            if (bucketCount <= 0)
                return false;

            int slot = NarrativePoiSpatialHash.PositiveModulo(hash, bucketCount);
            for (int probe = 0; probe < bucketCount; probe++)
            {
                range = BucketRanges[slot];
                if ((range.Flags & NarrativePoiBucketRangeFlags.Occupied) == 0u)
                    return false;

                if (range.CellHash == hash)
                    return true;

                slot++;
                if (slot >= bucketCount)
                    slot = 0;
            }

            return false;
        }

        private void WriteTelemetry(int evaluated, int signals, uint playerCellHash, uint flags, ulong stateMask)
        {
            if (!TelemetryRing.IsCreated || !TelemetryCursor.IsCreated || TelemetryRing.Length <= 0)
                return;

            int cursor = TelemetryCursor[0];
            if ((uint)cursor >= (uint)TelemetryRing.Length)
                cursor = 0;

            TelemetryRing[cursor] = new AupNarrativeTriggerTelemetryEntry
            {
                ExecutionTimeMicroseconds = 0d,
                PlayerAUP = PlayerAUP,
                Frame = Frame,
                EvaluatedPoiCount = evaluated,
                SignalsEmitted = signals,
                PlayerCellHash = playerCellHash,
                GlobalProgressionMask = stateMask,
                Flags = flags,
                StateHash = ComputeStateHash(playerCellHash)
            };

            cursor++;
            if (cursor >= TelemetryRing.Length)
                cursor = 0;
            TelemetryCursor[0] = cursor;
        }

        private uint ComputeStateHash(uint playerCellHash)
        {
            uint hash = 2166136261u;
            if (PoiStateMasks.IsCreated)
            {
                int wordCount = PoiStateMasks.Length;
                for (int i = 0; i < wordCount; i++)
                {
                    ulong word = PoiStateMasks[i];
                    hash = unchecked((hash ^ (uint)word) * 16777619u);
                    hash = unchecked((hash ^ (uint)(word >> 32)) * 16777619u);
                }
            }

            hash = unchecked((hash ^ playerCellHash) * 16777619u);
            return hash;
        }

        private void WriteCounter(AupNarrativePoiRuntimeConstants.CounterSlot slot, int value)
        {
            int index = (int)slot;
            if (Counters.IsCreated && (uint)index < (uint)Counters.Length)
                Counters[index] = value;
        }
    }

    public static unsafe class AupNarrativePoiTelemetryDump
    {
        public const long DumpMagic = 0x5348494E4F425501L;
        private const int DumpHeaderBytes = 32;

        public static void Write(string relativePath, NativeArray<AupNarrativeTriggerTelemetryEntry> telemetry, int cursor)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            string fullPath = Path.GetFullPath(relativePath);
            int entrySize = UnsafeUtility.SizeOf<AupNarrativeTriggerTelemetryEntry>();
            int bytes = DumpHeaderBytes + telemetry.Length * entrySize;
            try
            {
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough))
                {
#if UNITY_STANDALONE || UNITY_EDITOR
                    stream.SetLength(bytes);
                    using (MemoryMappedFile mappedFile = MemoryMappedFile.CreateFromFile(stream, null, bytes, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false))
                    using (MemoryMappedViewAccessor accessor = mappedFile.CreateViewAccessor(0L, bytes, MemoryMappedFileAccess.Write))
                    {
                        byte* destination = null;
                        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref destination);
                        try
                        {
                            WritePayload(destination, telemetry, entrySize, cursor);
                        }
                        finally
                        {
                            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                        }
                    }
#else
                    Span<byte> header = stackalloc byte[DumpHeaderBytes];
                    fixed (byte* headerPtr = header)
                        WriteHeader(headerPtr, telemetry.Length, entrySize, cursor);
                    stream.Write(header);
                    WriteTelemetryStream(stream, telemetry, entrySize, cursor);
#endif
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void WritePayload(byte* destination, NativeArray<AupNarrativeTriggerTelemetryEntry> telemetry, int entrySize, int cursor)
        {
            WriteHeader(destination, telemetry.Length, entrySize, cursor);
            byte* payload = destination + DumpHeaderBytes;
            byte* source = (byte*)telemetry.GetUnsafeReadOnlyPtr();
            int normalizedCursor = NormalizeCursor(cursor, telemetry.Length);
            for (int i = 0; i < telemetry.Length; i++)
            {
                int sourceIndex = normalizedCursor + i;
                if (sourceIndex >= telemetry.Length)
                    sourceIndex -= telemetry.Length;

                UnsafeUtility.MemCpy(payload + i * entrySize, source + sourceIndex * entrySize, entrySize);
            }
        }

        private static void WriteTelemetryStream(FileStream stream, NativeArray<AupNarrativeTriggerTelemetryEntry> telemetry, int entrySize, int cursor)
        {
            byte* source = (byte*)telemetry.GetUnsafeReadOnlyPtr();
            int normalizedCursor = NormalizeCursor(cursor, telemetry.Length);
            for (int i = 0; i < telemetry.Length; i++)
            {
                int sourceIndex = normalizedCursor + i;
                if (sourceIndex >= telemetry.Length)
                    sourceIndex -= telemetry.Length;

                stream.Write(new ReadOnlySpan<byte>(source + sourceIndex * entrySize, entrySize));
            }
        }

        private static void WriteHeader(byte* destination, int entryCount, int entrySize, int cursor)
        {
            long* header64 = (long*)destination;
            header64[0] = DumpMagic;
            int* header32 = (int*)(destination + sizeof(long));
            header32[0] = entryCount;
            header32[1] = entrySize;
            header32[2] = cursor;
            header32[3] = DumpHeaderBytes;
            header32[4] = NarrativePoiSpatialHash.CellSizeMeters;
            header32[5] = AupNarrativePoiRuntimeConstants.BucketStride;
        }

        private static int NormalizeCursor(int cursor, int length)
        {
            int normalized = cursor % length;
            return normalized < 0 ? normalized + length : normalized;
        }
    }

#if UNITY_EDITOR
    public static class NarrativePoiCsvIngestor
    {
        public static bool TryParsePoiLine(ReadOnlySpan<byte> line, out NarrativePoiDTO dto)
        {
            dto = default;
            if (line.IsEmpty)
                return false;

            int cursor = 0;
            if (!TryReadField(line, ref cursor, out ReadOnlySpan<byte> eventName) ||
                !TryReadDouble(line, ref cursor, out double x) ||
                !TryReadDouble(line, ref cursor, out double y) ||
                !TryReadDouble(line, ref cursor, out double z) ||
                !TryReadFloat(line, ref cursor, out float radius) ||
                !TryReadHexUlong(line, ref cursor, out ulong prereq))
            {
                return false;
            }

            uint hash = HashFnv1a(eventName);
            if (hash == 0u || radius <= 0f || !math.isfinite(radius))
                return false;

            dto = new NarrativePoiDTO
            {
                PoiAUP = new double3(x, y, z),
                EventHashID = hash,
                TriggerRadiusMeters = radius,
                PrerequisiteBitmask = prereq,
                StateFlags = NarrativePoiStateFlags.Active
            };
            return math.all(math.isfinite(dto.PoiAUP));
        }

        private static bool TryReadField(ReadOnlySpan<byte> line, ref int cursor, out ReadOnlySpan<byte> field)
        {
            field = default;
            if (cursor >= line.Length)
                return false;

            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;

            field = TrimAscii(line.Slice(start, cursor - start));
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;

            return !field.IsEmpty;
        }

        private static bool TryReadDouble(ReadOnlySpan<byte> line, ref int cursor, out double value)
        {
            value = 0d;
            if (!TryReadField(line, ref cursor, out ReadOnlySpan<byte> field))
                return false;

            return TryParseDouble(field, out value);
        }

        private static bool TryReadFloat(ReadOnlySpan<byte> line, ref int cursor, out float value)
        {
            value = 0f;
            if (!TryReadDouble(line, ref cursor, out double parsed) || math.abs(parsed) > float.MaxValue)
                return false;

            value = (float)parsed;
            return float.IsFinite(value);
        }

        private static bool TryReadHexUlong(ReadOnlySpan<byte> line, ref int cursor, out ulong value)
        {
            value = 0UL;
            if (!TryReadField(line, ref cursor, out ReadOnlySpan<byte> field))
                return false;

            int start = field.Length > 2 && field[0] == (byte)'0' && (field[1] == (byte)'x' || field[1] == (byte)'X') ? 2 : 0;
            for (int i = start; i < field.Length; i++)
            {
                int digit = HexDigit(field[i]);
                if (digit < 0)
                    return false;

                value = (value << 4) | (uint)digit;
            }

            return true;
        }

        private static bool TryParseDouble(ReadOnlySpan<byte> field, out double value)
        {
            value = 0d;
            if (field.IsEmpty)
                return false;

            int index = 0;
            double sign = 1d;
            if (field[index] == (byte)'-')
            {
                sign = -1d;
                index++;
            }
            else if (field[index] == (byte)'+')
            {
                index++;
            }

            double integer = 0d;
            bool hasDigit = false;
            while (index < field.Length && field[index] >= (byte)'0' && field[index] <= (byte)'9')
            {
                hasDigit = true;
                integer = integer * 10d + (field[index] - (byte)'0');
                index++;
            }

            double fraction = 0d;
            double scale = 1d;
            if (index < field.Length && field[index] == (byte)'.')
            {
                index++;
                while (index < field.Length && field[index] >= (byte)'0' && field[index] <= (byte)'9')
                {
                    hasDigit = true;
                    fraction = fraction * 10d + (field[index] - (byte)'0');
                    scale *= 10d;
                    index++;
                }
            }

            if (!hasDigit || index != field.Length)
                return false;

            value = sign * (integer + fraction / scale);
            return double.IsFinite(value);
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && IsAsciiWhitespace(value[start]))
                start++;
            while (end >= start && IsAsciiWhitespace(value[end]))
                end--;
            return start > end ? ReadOnlySpan<byte>.Empty : value.Slice(start, end - start + 1);
        }

        private static bool IsAsciiWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n';
        }

        private static int HexDigit(byte value)
        {
            if (value >= (byte)'0' && value <= (byte)'9')
                return value - (byte)'0';
            if (value >= (byte)'a' && value <= (byte)'f')
                return value - (byte)'a' + 10;
            if (value >= (byte)'A' && value <= (byte)'F')
                return value - (byte)'A' + 10;
            return -1;
        }

        private static uint HashFnv1a(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
                hash = unchecked((hash ^ bytes[i]) * 16777619u);
            return hash;
        }
    }
#endif

    public sealed partial class HectonNarrativeDirector
    {
        private AupNarrativePoiBufferHandles _aupNarrativePoiHandles;
        private IDataVault _aupNarrativePoiVault;
        private JobHandle _aupNarrativePoiJobHandle;
        private long _aupNarrativePoiJobTimestamp;
        private uint _aupNarrativePoiScheduledFrame;
        private VaultGenerationHandle<ulong> _questDagGlobalStateMaskHandle;
        private bool _aupNarrativePoiJobScheduled;
        private bool _aupNarrativePoiLateRegistered;
        private bool _aupNarrativePoiUpdateRegistered;
        private bool _aupNarrativePoiBuffersInitialized;
        private bool _questDagGlobalStateMaskHandleCached;

        private void InitializeAupNarrativePoiVaultStorage()
        {
            _aupNarrativePoiVault = GlobalRegistry.DataVault;
            if (_aupNarrativePoiVault == null)
                return;

            _aupNarrativePoiHandles = AupNarrativePoiVault.EnsureBuffers(_aupNarrativePoiVault);
            _aupNarrativePoiBuffersInitialized = _aupNarrativePoiHandles.PoiCapacity > 0;
            SignalBus<ProgressionEventSignal>.ConfigureCacheLineCritical(
                expectedCapacity: 256,
                maxFrameSignals: 512,
                lowTierFrameSignals: 64,
                laneHash: AupNarrativePoiRuntimeConstants.SignalLaneHash);
            SignalBus<ProgressionEventSignal>.EnsureInitialized();
            TryCacheQuestDagGlobalStateMaskHandle();
            if (AupNarrativePoiVault.TryResolveBuffers(_aupNarrativePoiVault, ref _aupNarrativePoiHandles, out AupNarrativePoiBuffers buffers))
                InitializeAupNarrativePoiCounters(buffers);
        }

        private void TryRegisterAupNarrativePoiUpdate()
        {
            if (!_aupNarrativePoiBuffersInitialized)
                InitializeAupNarrativePoiVaultStorage();

            if (_aupNarrativePoiUpdateRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _aupNarrativePoiUpdateRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
        }

        private void TryUnregisterAupNarrativePoiUpdate()
        {
            if (!_aupNarrativePoiUpdateRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _aupNarrativePoiUpdateRegistered = false;
        }

        private void TryRegisterAupNarrativePoiLateFrame()
        {
            if (_aupNarrativePoiLateRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _aupNarrativePoiLateRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
        }

        private void TryUnregisterAupNarrativePoiLateFrame()
        {
            CompleteAupNarrativePoiJob(forceComplete: true);
            if (!_aupNarrativePoiLateRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
            _aupNarrativePoiLateRegistered = false;
        }

        public void LateFrameTick()
        {
            CompleteAupNarrativePoiJobIfReady();
        }

        public void Tick(float deltaTime)
        {
            if (!ShouldScanAupNarrativeTriggers(deltaTime))
                return;

            if (_playerTransform == null && !ResolvePlayerTransform())
                return;

            Vector3 playerPosition = _playerTransform.position;
            float3 playerRuntime = new float3(playerPosition.x, playerPosition.y, playerPosition.z);
            if (!math.all(math.isfinite(playerRuntime)) ||
                !TryResolveRuntimeAup(playerPosition, out AbsoluteUniversePosition playerAup))
            {
                if (EnsureAupNarrativePoiBuffers(out AupNarrativePoiBuffers buffers))
                    DumpAupNarrativePoiTelemetry(buffers);
                GlobalTelemetryBus.PublishPerformanceWarning(_blackBoxFaultWarningHash, _blackBoxFaultContextHash, 1f);
                return;
            }

            TryScheduleAupNarrativePoiVaultCheck(in playerAup, playerRuntime);
        }

        private void DisposeAupNarrativePoiVaultStorage()
        {
            CompleteAupNarrativePoiJob(forceComplete: true);
            AupNarrativePoiVault.ReleaseBuffers(_aupNarrativePoiVault, ref _aupNarrativePoiHandles);
            _aupNarrativePoiVault = null;
            _aupNarrativePoiBuffersInitialized = false;
            _questDagGlobalStateMaskHandle = default;
            _questDagGlobalStateMaskHandleCached = false;
        }

        private void RebuildAupNarrativePoiVaultRegistry()
        {
            CompleteAupNarrativePoiJob(forceComplete: true);
            if (!EnsureAupNarrativePoiBuffers(out AupNarrativePoiBuffers buffers))
                return;

            int count = 0;
            int capacity = math.min(_aupNarrativePoiHandles.PoiCapacity, math.min(buffers.Pois.Length, buffers.Presentation.Length));
            if (buffers.StateMasks.IsCreated)
                NarrativePoiStateMaskWords.ClearAll(buffers.StateMasks);

            for (int i = 0; i < _activePOIs.Count && count < capacity; i++)
            {
                NarrativeDiscovery poi = _activePOIs[i];
                if (poi == null || !poi.TryGetSpatialTrigger(out NarrativeSpatialTriggerAuthoring trigger))
                    continue;

                int bitIndex = math.clamp(trigger.BitIndex, 0, 63);
                ulong legacyBit = 1UL << bitIndex;
                uint flags = NarrativePoiStateFlags.Active | NarrativePoiStateFlags.EncodeBitIndex(bitIndex);
                bool alreadyTriggered = (narrativeAupTriggeredMask & legacyBit) != 0UL ||
                                        (trigger.PoiHash != 0u && _discoveredHashLookup.Contains(trigger.PoiHash));
                if (alreadyTriggered)
                {
                    flags |= NarrativePoiStateFlags.Exhausted | NarrativePoiStateFlags.Dispatched;
                    narrativeAupTriggeredMask |= legacyBit;
                    NarrativePoiStateMaskWords.Set(buffers.StateMasks, count);
                }

                buffers.Pois[count] = new NarrativePoiDTO
                {
                    PoiAUP = trigger.PositionAup.ToAbsoluteDouble3(),
                    EventHashID = trigger.PoiHash,
                    TriggerRadiusMeters = math.max(0.0001f, trigger.RadiusMeters),
                    PrerequisiteBitmask = ResolveNarrativePoiPrerequisiteBitmask(in trigger),
                    StateFlags = flags
                };
                buffers.Presentation[count] = new NarrativePoiPresentationDTO
                {
                    PoiHash = trigger.PoiHash,
                    QuestHash = trigger.QuestHash,
                    BiomeHash = trigger.BiomeHash,
                    SoundscapeHash = trigger.SoundscapeHash,
                    LoreHash = trigger.LoreHash,
                    Flags = (uint)(byte)trigger.Flags,
                    BitIndex = bitIndex,
                    Reserved0 = 0u
                };
                count++;
            }

            for (int i = count; i < capacity; i++)
            {
                buffers.Pois[i] = default;
                buffers.Presentation[i] = default;
            }

            buffers.Counters[(int)AupNarrativePoiRuntimeConstants.CounterSlot.PoiCount] = count;
            BuildNarrativePoiBucketsJob buildJob = new BuildNarrativePoiBucketsJob
            {
                Pois = buffers.Pois,
                BucketRanges = buffers.BucketRanges,
                BucketIndices = buffers.BucketIndices,
                Counters = buffers.Counters,
                PoiCount = count,
                BucketStride = _aupNarrativePoiHandles.BucketStride
            };
            JobHandle buildHandle = buildJob.Schedule();
            // Cold registry mutation fence: scene POI set changed, so bucket data must be coherent before gameplay ticks resume.
            DispatcherJobFence.TryComplete(ref buildHandle, forceComplete: true);
            int version = buffers.Counters[(int)AupNarrativePoiRuntimeConstants.CounterSlot.SpatialVersion];
            buffers.Counters[(int)AupNarrativePoiRuntimeConstants.CounterSlot.SpatialVersion] = version + 1;
        }

        private static ulong ResolveNarrativePoiPrerequisiteBitmask(in NarrativeSpatialTriggerAuthoring trigger)
        {
            uint questHash = trigger.QuestHash;
            uint poiHash = trigger.PoiHash;
            if (questHash == H8QuestMasks.QuestFirstHourWakeUp.NodeHash32 ||
                poiHash == H8QuestMasks.QuestFirstHourWakeUp.Trigger0Hash32)
            {
                return H8QuestMasks.QuestFirstHourWakeUp.PrerequisiteDoneMask;
            }

            if (questHash == H8QuestMasks.QuestFirstHourFindScanner.NodeHash32 ||
                poiHash == H8QuestMasks.QuestFirstHourFindScanner.Trigger0Hash32)
            {
                return H8QuestMasks.QuestFirstHourFindScanner.PrerequisiteDoneMask;
            }

            if (questHash == H8QuestMasks.QuestFirstHourScanLeviathanTrace.NodeHash32 ||
                poiHash == H8QuestMasks.QuestFirstHourScanLeviathanTrace.Trigger0Hash32)
            {
                return H8QuestMasks.QuestFirstHourScanLeviathanTrace.PrerequisiteDoneMask;
            }

            if (questHash == H8QuestMasks.QuestFirstHourFixRadio.NodeHash32 ||
                poiHash == H8QuestMasks.QuestFirstHourFixRadio.Trigger0Hash32)
            {
                return H8QuestMasks.QuestFirstHourFixRadio.PrerequisiteDoneMask;
            }

            return 0UL;
        }

        private void SyncAupNarrativePoiVaultStateFromMask()
        {
            if (!EnsureAupNarrativePoiBuffers(out AupNarrativePoiBuffers buffers))
                return;

            if (buffers.StateMasks.IsCreated)
                NarrativePoiStateMaskWords.ClearAll(buffers.StateMasks);

            int count = math.min(buffers.Counters[(int)AupNarrativePoiRuntimeConstants.CounterSlot.PoiCount], buffers.Pois.Length);
            for (int i = 0; i < count; i++)
            {
                NarrativePoiDTO dto = buffers.Pois[i];
                NarrativePoiPresentationDTO presentation = (buffers.Presentation.IsCreated && (uint)i < (uint)buffers.Presentation.Length)
                    ? buffers.Presentation[i]
                    : default;
                bool hasPresentation = presentation.PoiHash != 0u ||
                                       presentation.QuestHash != 0u ||
                                       presentation.BiomeHash != 0u ||
                                       presentation.SoundscapeHash != 0u ||
                                       presentation.LoreHash != 0u ||
                                       presentation.Flags != 0u;
                int bitIndex = hasPresentation
                    ? presentation.BitIndex
                    : NarrativePoiStateFlags.DecodeBitIndex(dto.StateFlags);
                ulong legacyBit = 1UL << math.clamp(bitIndex, 0, 63);
                bool triggered = (narrativeAupTriggeredMask & legacyBit) != 0UL ||
                                 (dto.EventHashID != 0u && _discoveredHashLookup.Contains(dto.EventHashID));
                if (triggered)
                {
                    narrativeAupTriggeredMask |= legacyBit;
                    dto.StateFlags |= NarrativePoiStateFlags.Exhausted | NarrativePoiStateFlags.Dispatched;
                    NarrativePoiStateMaskWords.Set(buffers.StateMasks, i);
                }
                else
                {
                    dto.StateFlags &= ~(NarrativePoiStateFlags.Exhausted |
                                        NarrativePoiStateFlags.Triggered |
                                        NarrativePoiStateFlags.Inside |
                                        NarrativePoiStateFlags.DispatchPending |
                                        NarrativePoiStateFlags.Dispatched);
                }

                buffers.Pois[i] = dto;
            }
        }

        private bool TryScheduleAupNarrativePoiVaultCheck(in AbsoluteUniversePosition playerAup, float3 playerRuntime)
        {
            if (_aupNarrativePoiJobScheduled)
            {
                if (EnsureAupNarrativePoiBuffers(out AupNarrativePoiBuffers pendingBuffers))
                {
                    int slot = (int)AupNarrativePoiRuntimeConstants.CounterSlot.PendingScheduleDropCount;
                    pendingBuffers.Counters[slot] = pendingBuffers.Counters[slot] == int.MaxValue
                        ? int.MaxValue
                        : pendingBuffers.Counters[slot] + 1;
                }

                return false;
            }

            if (!EnsureAupNarrativePoiBuffers(out AupNarrativePoiBuffers buffers))
                return false;

            int count = math.min(buffers.Counters[(int)AupNarrativePoiRuntimeConstants.CounterSlot.PoiCount], buffers.Pois.Length);
            if (count <= 0)
                return false;

            double3 playerAbsolute = playerAup.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(playerAbsolute)))
                return false;

            _lastScheduledPlayerAup = playerAup;
            _lastScheduledPlayerRuntime = playerRuntime;
            _hasLastScheduledPlayerPose = true;
            _aupNarrativePoiScheduledFrame = SystemDispatcher.ReadPublishedDispatcherFrameId();
            _aupNarrativePoiJobTimestamp = Stopwatch.GetTimestamp();
            _aupNarrativePoiJobHandle = new EvaluatePoiTriggersJob
            {
                Pois = buffers.Pois,
                Presentation = buffers.Presentation,
                BucketRanges = buffers.BucketRanges,
                BucketIndices = buffers.BucketIndices,
                PoiStateMasks = buffers.StateMasks,
                TelemetryRing = buffers.TelemetryRing,
                TelemetryCursor = buffers.TelemetryCursor,
                Counters = buffers.Counters,
                ProgressionWriter = SignalBus<ProgressionEventSignal>.ParallelWriter,
                ProgressionWriterBudget = SignalBus<ProgressionEventSignal>.ParallelWriterBudget,
                PlayerAUP = playerAbsolute,
                GlobalNarrativeStateMask = CaptureQuestDagGlobalNarrativeMaskForSchedule(),
                Frame = _aupNarrativePoiScheduledFrame,
                PoiCount = count
            }.Schedule();
            _aupNarrativePoiJobScheduled = true;
            return true;
        }

        private void CompleteAupNarrativePoiJobIfReady()
        {
            CompleteAupNarrativePoiJob(forceComplete: false);
        }

        private void CompleteAupNarrativePoiJob(bool forceComplete)
        {
            if (!_aupNarrativePoiJobScheduled)
                return;

            bool completed = forceComplete
                ? DispatcherJobFence.TryComplete(ref _aupNarrativePoiJobHandle, forceComplete: true)
                : DispatcherJobFence.TryFinalizeCompleted(ref _aupNarrativePoiJobHandle);
            if (!completed)
                return;

            _aupNarrativePoiJobScheduled = false;
            if (!EnsureAupNarrativePoiBuffers(out AupNarrativePoiBuffers buffers))
                return;

            double elapsedMicros = (Stopwatch.GetTimestamp() - _aupNarrativePoiJobTimestamp) * 1000000.0d / Stopwatch.Frequency;
            PatchAupNarrativePoiTelemetry(buffers, elapsedMicros);
            DispatchAupNarrativePoiSolvedResults(buffers);

            AupNarrativeTriggerTelemetryEntry last = ReadLastAupNarrativePoiTelemetry(buffers);
            if (elapsedMicros > AupNarrativePoiRuntimeConstants.FaultDumpMicroseconds ||
                (last.Flags & ((uint)AupNarrativePoiRuntimeConstants.TelemetryFlags.InvalidPlayerAup |
                               (uint)AupNarrativePoiRuntimeConstants.TelemetryFlags.InvalidPoiAup |
                               (uint)AupNarrativePoiRuntimeConstants.TelemetryFlags.BucketOverflow)) != 0u)
            {
                DumpAupNarrativePoiTelemetry(buffers);
            }
        }

        private bool EnsureAupNarrativePoiBuffers(out AupNarrativePoiBuffers buffers)
        {
            if (_aupNarrativePoiVault == null || !_aupNarrativePoiBuffersInitialized)
            {
                buffers = default;
                return false;
            }

            return AupNarrativePoiVault.TryResolveBuffers(_aupNarrativePoiVault, ref _aupNarrativePoiHandles, out buffers);
        }

        private static void InitializeAupNarrativePoiCounters(AupNarrativePoiBuffers buffers)
        {
            if (buffers.StateMasks.IsCreated)
                NarrativePoiStateMaskWords.ClearAll(buffers.StateMasks);

            if (buffers.Counters.IsCreated)
            {
                for (int i = 0; i < buffers.Counters.Length; i++)
                    buffers.Counters[i] = 0;

                buffers.Counters[(int)AupNarrativePoiRuntimeConstants.CounterSlot.BucketCount] = AupNarrativePoiRuntimeConstants.DefaultBucketCount;
                buffers.Counters[(int)AupNarrativePoiRuntimeConstants.CounterSlot.BucketStride] = AupNarrativePoiRuntimeConstants.BucketStride;
            }

            if (buffers.TelemetryCursor.IsCreated && buffers.TelemetryCursor.Length > 0)
                buffers.TelemetryCursor[0] = 0;
        }

        private void TryCacheQuestDagGlobalStateMaskHandle()
        {
            _questDagGlobalStateMaskHandleCached = _aupNarrativePoiVault != null &&
                                                   _aupNarrativePoiVault.TryGetGenerationHandle(
                                                       BufferID.QuestDagGlobalStateMasks,
                                                       out _questDagGlobalStateMaskHandle);
        }

        private ulong CaptureQuestDagGlobalNarrativeMaskForSchedule()
        {
            IDataVault vault = _aupNarrativePoiVault;
            if (!_questDagGlobalStateMaskHandleCached)
                TryCacheQuestDagGlobalStateMaskHandle();

            if (vault == null ||
                !_questDagGlobalStateMaskHandleCached ||
                !vault.TryReadHandle(in _questDagGlobalStateMaskHandle, out NativeArray<ulong> masks) ||
                !masks.IsCreated ||
                masks.Length <= 0)
            {
                return 0UL;
            }

            return masks[0];
        }

        private void PatchAupNarrativePoiTelemetry(AupNarrativePoiBuffers buffers, double elapsedMicros)
        {
            if (!buffers.TelemetryCursor.IsCreated || !buffers.TelemetryRing.IsCreated || buffers.TelemetryRing.Length <= 0)
                return;

            int cursor = buffers.TelemetryCursor[0] - 1;
            if (cursor < 0)
                cursor += buffers.TelemetryRing.Length;

            int index = cursor % buffers.TelemetryRing.Length;
            AupNarrativeTriggerTelemetryEntry entry = buffers.TelemetryRing[index];
            if (entry.Frame != _aupNarrativePoiScheduledFrame)
                return;

            entry.ExecutionTimeMicroseconds = elapsedMicros;
            if (elapsedMicros > AupNarrativePoiRuntimeConstants.FaultDumpMicroseconds)
                entry.Flags |= (uint)AupNarrativePoiRuntimeConstants.TelemetryFlags.ExceededTimeBudget;
            buffers.TelemetryRing[index] = entry;
        }

        private static AupNarrativeTriggerTelemetryEntry ReadLastAupNarrativePoiTelemetry(in AupNarrativePoiBuffers buffers)
        {
            if (!buffers.TelemetryCursor.IsCreated || !buffers.TelemetryRing.IsCreated || buffers.TelemetryRing.Length <= 0)
                return default;

            int cursor = buffers.TelemetryCursor[0] - 1;
            if (cursor < 0)
                cursor += buffers.TelemetryRing.Length;
            return buffers.TelemetryRing[cursor % buffers.TelemetryRing.Length];
        }

        private void DispatchAupNarrativePoiSolvedResults(AupNarrativePoiBuffers buffers)
        {
            int count = math.min(buffers.Counters[(int)AupNarrativePoiRuntimeConstants.CounterSlot.PoiCount], buffers.Pois.Length);
            for (int i = 0; i < count; i++)
            {
                NarrativePoiDTO dto = buffers.Pois[i];
                if ((dto.StateFlags & NarrativePoiStateFlags.DispatchPending) == 0u ||
                    (dto.StateFlags & NarrativePoiStateFlags.Dispatched) != 0u)
                {
                    continue;
                }

                dto.StateFlags = (dto.StateFlags & ~NarrativePoiStateFlags.DispatchPending) |
                                 NarrativePoiStateFlags.Exhausted |
                                 NarrativePoiStateFlags.Dispatched;
                buffers.Pois[i] = dto;
                NarrativePoiPresentationDTO presentation = (buffers.Presentation.IsCreated && (uint)i < (uint)buffers.Presentation.Length)
                    ? buffers.Presentation[i]
                    : default;
                DispatchAupNarrativePoiSolvedResult(i, in dto, in presentation);
            }
        }

        private void DispatchAupNarrativePoiSolvedResult(
            int poiIndex,
            in NarrativePoiDTO dto,
            in NarrativePoiPresentationDTO presentation)
        {
            uint poiHash = dto.EventHashID != 0u ? dto.EventHashID : presentation.PoiHash;
            if (poiHash == 0u)
                return;

            bool hasPresentation = presentation.PoiHash != 0u ||
                                   presentation.QuestHash != 0u ||
                                   presentation.BiomeHash != 0u ||
                                   presentation.SoundscapeHash != 0u ||
                                   presentation.LoreHash != 0u ||
                                   presentation.Flags != 0u;
            int bitIndex = hasPresentation
                ? presentation.BitIndex
                : NarrativePoiStateFlags.DecodeBitIndex(dto.StateFlags);
            narrativeAupTriggeredMask |= 1UL << math.clamp(bitIndex, 0, 63);

            string discoveryId = (uint)poiIndex < (uint)_poiDiscoveryIds.Length
                ? _poiDiscoveryIds[poiIndex]
                : null;
            uint discoveryHash = string.IsNullOrWhiteSpace(discoveryId)
                ? poiHash
                : NarrativeEvents.ComputeDiscoveryHash(discoveryId);
            if (discoveryHash == 0u)
                discoveryHash = poiHash;

            NarrativeEvents.TryRaiseDiscoveryMade(discoveryHash);

            RecordDiscoveryIdentity(discoveryHash, discoveryId);
            LoreDatabaseManager loreDatabase = GlobalRegistry.LoreDatabase;
            uint loreHash = presentation.LoreHash;
            if (loreDatabase != null && loreHash != 0u)
                loreDatabase.TryUnlockByHash(loreHash);

            PublishBiomeSignal(in dto, in presentation, poiHash);
            PublishSoundscapeSignal(in dto, in presentation, poiHash);
            PublishNarrativeFocusSignal(in dto, in presentation, poiHash);
            PublishHudWaypointSignal(in dto, in presentation, poiHash);
            PublishPoiStateSignal(poiHash, poiIndex, SaveStateOperationTriggered, (byte)(presentation.Flags & 0xFFu));
        }

        private void DumpAupNarrativePoiTelemetry(AupNarrativePoiBuffers buffers)
        {
            int cursor = buffers.TelemetryCursor.IsCreated ? buffers.TelemetryCursor[0] : 0;
            AupNarrativePoiTelemetryDump.Write(AupNarrativePoiRuntimeConstants.DumpPath, buffers.TelemetryRing, cursor);
            if (buffers.Counters.IsCreated)
            {
                int slot = (int)AupNarrativePoiRuntimeConstants.CounterSlot.DumpCount;
                buffers.Counters[slot] = buffers.Counters[slot] == int.MaxValue ? int.MaxValue : buffers.Counters[slot] + 1;
            }
        }

        public void GenerateMockNarrativePoiTriggersForDiagnostics(double3 originAup, int count)
        {
            CompleteAupNarrativePoiJob(forceComplete: true);
            if (!EnsureAupNarrativePoiBuffers(out AupNarrativePoiBuffers buffers))
                return;

            GenerateMockPoiTriggersJob job = new GenerateMockPoiTriggersJob
            {
                Pois = buffers.Pois,
                BucketRanges = buffers.BucketRanges,
                BucketIndices = buffers.BucketIndices,
                StateMasks = buffers.StateMasks,
                Counters = buffers.Counters,
                OriginAUP = originAup,
                RequestedCount = count,
                BucketStride = _aupNarrativePoiHandles.BucketStride,
                Seed = AupNarrativePoiRuntimeConstants.SignalLaneHash
            };
            JobHandle handle = job.Schedule();
            // Editor/diagnostic cold fence only; this mock bake is never used as a frame-path readback loop.
            DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            int written = math.min(math.max(0, count), math.min(buffers.Pois.Length, buffers.Presentation.Length));
            for (int i = 0; i < written; i++)
            {
                buffers.Presentation[i] = new NarrativePoiPresentationDTO
                {
                    PoiHash = buffers.Pois[i].EventHashID,
                    QuestHash = buffers.Pois[i].EventHashID,
                    BiomeHash = 0u,
                    SoundscapeHash = 0u,
                    LoreHash = 0u,
                    Flags = 0u,
                    BitIndex = i & 63,
                    Reserved0 = 0u
                };
            }
        }

        private bool HasPendingAupNarrativePoi()
        {
            if (!EnsureAupNarrativePoiBuffers(out AupNarrativePoiBuffers buffers))
                return false;

            int count = math.min(buffers.Counters[(int)AupNarrativePoiRuntimeConstants.CounterSlot.PoiCount], buffers.Pois.Length);
            for (int i = 0; i < count; i++)
            {
                uint flags = buffers.Pois[i].StateFlags;
                if ((flags & NarrativePoiStateFlags.Active) != 0u &&
                    (flags & NarrativePoiStateFlags.Exhausted) == 0u)
                {
                    return true;
                }
            }

            return false;
        }

        private void MarkAupNarrativePoiTriggeredByHash(uint discoveryHash)
        {
            if (discoveryHash == 0u)
                return;

            CompleteAupNarrativePoiJob(forceComplete: true);
            if (!EnsureAupNarrativePoiBuffers(out AupNarrativePoiBuffers buffers))
                return;

            int count = math.min(buffers.Counters[(int)AupNarrativePoiRuntimeConstants.CounterSlot.PoiCount], buffers.Pois.Length);
            for (int i = 0; i < count; i++)
            {
                NarrativePoiDTO dto = buffers.Pois[i];
                if (dto.EventHashID != discoveryHash)
                    continue;

                NarrativePoiPresentationDTO presentation = (buffers.Presentation.IsCreated && (uint)i < (uint)buffers.Presentation.Length)
                    ? buffers.Presentation[i]
                    : default;
                bool hasPresentation = presentation.PoiHash != 0u ||
                                       presentation.QuestHash != 0u ||
                                       presentation.BiomeHash != 0u ||
                                       presentation.SoundscapeHash != 0u ||
                                       presentation.LoreHash != 0u ||
                                       presentation.Flags != 0u;
                int bitIndex = hasPresentation
                    ? presentation.BitIndex
                    : NarrativePoiStateFlags.DecodeBitIndex(dto.StateFlags);
                narrativeAupTriggeredMask |= 1UL << math.clamp(bitIndex, 0, 63);
                NarrativePoiStateMaskWords.Set(buffers.StateMasks, i);

                uint previousFlags = dto.StateFlags;
                dto.StateFlags = (dto.StateFlags & ~NarrativePoiStateFlags.DispatchPending) |
                                 NarrativePoiStateFlags.Exhausted |
                                 NarrativePoiStateFlags.Dispatched;
                buffers.Pois[i] = dto;

                if ((previousFlags & NarrativePoiStateFlags.Exhausted) == 0u)
                    PublishPoiStateSignal(discoveryHash, i, SaveStateOperationTriggered, (byte)(presentation.Flags & 0xFFu));
                break;
            }
        }
    }
}
