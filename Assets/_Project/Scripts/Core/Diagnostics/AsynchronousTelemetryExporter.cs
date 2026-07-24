using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.Core.Diagnostics
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AnalyticEventDTO
    {
        [FieldOffset(0)] public uint EventHashID;
        [FieldOffset(4)] public uint TimestampSeconds;
        [FieldOffset(8)] public double3 EventAUP;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AnalyticsCountersDTO
    {
        [FieldOffset(0)] public uint EnqueuedEvents;
        [FieldOffset(4)] public uint DrainedEvents;
        [FieldOffset(8)] public uint StagedEventsThisFrame;
        [FieldOffset(12)] public uint DroppedEvents;
        [FieldOffset(16)] public uint CriticalFlushRequests;
        [FieldOffset(20)] public uint EventRingWriteCursor;
        [FieldOffset(24)] public uint NonFiniteEvents;
        [FieldOffset(28)] public uint HandoffMisses;
        [FieldOffset(32)] public uint WorkerBacklogEvents;
        [FieldOffset(36)] public uint LastCullThreshold;
        [FieldOffset(40)] public uint LastFrameIndex;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint EndpointHash;
        [FieldOffset(52)] public uint ApiKeyHash;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AnalyticsTuningDTO
    {
        [FieldOffset(0)] public float GlobalQualityWeight;
        [FieldOffset(4)] public float HeatmapSampleSeconds;
        [FieldOffset(8)] public int BatchFlushThresholdBytes;
        [FieldOffset(12)] public int NetworkTimeoutMs;
        [FieldOffset(16)] public int LowCullThreshold;
        [FieldOffset(20)] public int UltraCullThreshold;
        [FieldOffset(24)] public int StagingCapacity;
        [FieldOffset(28)] public int Flags;
        [FieldOffset(32)] public uint EndpointHash;
        [FieldOffset(36)] public uint ApiKeyHash;
        [FieldOffset(40)] public uint RouteSampleHash;
        [FieldOffset(44)] public uint DeathHash;
        [FieldOffset(48)] public uint ResourceHash;
        [FieldOffset(52)] public uint PerfSpikeHash;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AnalyticsExporterTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint TimestampSeconds;
        [FieldOffset(8)] public uint SentEvents;
        [FieldOffset(12)] public uint DiskFallbackEvents;
        [FieldOffset(16)] public uint DroppedEvents;
        [FieldOffset(20)] public uint BacklogEvents;
        [FieldOffset(24)] public uint RawBytes;
        [FieldOffset(28)] public uint CompressedBytes;
        [FieldOffset(32)] public int LastResponseCode;
        [FieldOffset(36)] public uint CompressionRatioMilli;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public uint WorkerHeartbeat;
        [FieldOffset(48)] public uint FaultCount;
        [FieldOffset(52)] public uint QueueDepthEstimate;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] public uint VaultBytes;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AnalyticsIngressCursorDTO
    {
        [FieldOffset(0)] public uint RoutineWriteCursor;
        [FieldOffset(4)] public uint RoutineReadCursor;
        [FieldOffset(8)] public uint CriticalWriteCursor;
        [FieldOffset(12)] public uint CriticalReadCursor;
        [FieldOffset(16)] public int RoutineCapacity;
        [FieldOffset(20)] public int CriticalCapacity;
        [FieldOffset(24)] public uint RoutineOverflowDrops;
        [FieldOffset(28)] public uint CriticalOverflowDrops;
        [FieldOffset(32)] public uint LastFrameIndex;
        [FieldOffset(36)] public uint StateHash;
        [FieldOffset(40)] public uint Reserved0;
        [FieldOffset(44)] public uint Reserved1;
        [FieldOffset(48)] public uint Reserved2;
        [FieldOffset(52)] public uint Reserved3;
        [FieldOffset(56)] public uint Reserved4;
        [FieldOffset(60)] public uint Reserved5;
    }

    public static class AnalyticsEventHashes
    {
        public const uint CriticalMask = 0x80000000u;
        public const uint Death = CriticalMask | 0x0044EADu;
        public const uint PerfSpike = CriticalMask | 0x00F5F1Eu;
        public const uint ResourceDelta = 0x0050E50Cu;
        public const uint RouteSample = 0x005A11E1u;
        public const uint MockRoute = 0x00160A11u;
    }

    public static class AnalyticsVaultBufferIds
    {
        public const BufferID EventRing = BufferID.AsynchronousTelemetryExporter_EventRing;
        public const BufferID Staging = BufferID.AsynchronousTelemetryExporter_Staging;
        public const BufferID Counters = BufferID.AsynchronousTelemetryExporter_Counters;
        public const BufferID TelemetryRing = BufferID.AsynchronousTelemetryExporter_TelemetryRing;
        public const BufferID TelemetryCursor = BufferID.AsynchronousTelemetryExporter_TelemetryCursor;
        public const BufferID Tuning = BufferID.AsynchronousTelemetryExporter_Tuning;
        public const BufferID CsvScratch = BufferID.AsynchronousTelemetryExporter_CsvScratch;
        public const BufferID CompressedScratch = BufferID.AsynchronousTelemetryExporter_CompressedScratch;
        public const BufferID HeatmapDebug = BufferID.AsynchronousTelemetryExporter_HeatmapDebug;
        public const BufferID HandoffA = BufferID.AsynchronousTelemetryExporter_HandoffA;
        public const BufferID HandoffB = BufferID.AsynchronousTelemetryExporter_HandoffB;
        public const BufferID WorkerAccum = BufferID.AsynchronousTelemetryExporter_WorkerAccum;
        public const BufferID RawBatchScratch = BufferID.AsynchronousTelemetryExporter_RawBatchScratch;
        public const BufferID DumpSnapshot = BufferID.AsynchronousTelemetryExporter_DumpSnapshot;
        public const BufferID RoutineIngress = BufferID.AsynchronousTelemetryExporter_RoutineIngress;
        public const BufferID CriticalIngress = BufferID.AsynchronousTelemetryExporter_CriticalIngress;
        public const BufferID IngressCursor = BufferID.AsynchronousTelemetryExporter_IngressCursor;
    }

    public static class AnalyticsExporterFlags
    {
        public const int MockEvents = 1 << 0;
        public const int HeatmapKcc = 1 << 1;
        public const int NetworkEnabled = 1 << 2;
    }

    public static class AnalyticsLayout
    {
        public static bool ValidateAnalyticLayouts(out uint failureMask)
        {
            failureMask = 0u;
            if (UnsafeUtility.SizeOf<AnalyticEventDTO>() != 32)
                failureMask |= 1u << 0;
            if (OffsetOfAnalyticEventHash() != 0)
                failureMask |= 1u << 1;
            if (OffsetOfAnalyticTimestamp() != 4)
                failureMask |= 1u << 2;
            if (OffsetOfAnalyticAup() != 8)
                failureMask |= 1u << 3;
            if (UnsafeUtility.SizeOf<AnalyticsCountersDTO>() != 64)
                failureMask |= 1u << 4;
            if (UnsafeUtility.SizeOf<AnalyticsTuningDTO>() != 64)
                failureMask |= 1u << 5;
            if (UnsafeUtility.SizeOf<AnalyticsExporterTelemetryEntry>() != 64)
                failureMask |= 1u << 6;
            if (UnsafeUtility.SizeOf<AnalyticsIngressCursorDTO>() != 64)
                failureMask |= 1u << 7;
            return failureMask == 0u;
        }

        public static void ValidateOrThrow()
        {
            if (!ValidateAnalyticLayouts(out uint failureMask))
                throw new global::Hecton8.Core.FatalArchitectureException("SHINOBU_160 analytics layout failure mask=" + failureMask);
        }

        private static unsafe int OffsetOfAnalyticEventHash()
        {
            AnalyticEventDTO value = default;
            return ByteOffset(ref value, ref value.EventHashID);
        }

        private static unsafe int OffsetOfAnalyticTimestamp()
        {
            AnalyticEventDTO value = default;
            return ByteOffset(ref value, ref value.TimestampSeconds);
        }

        private static unsafe int OffsetOfAnalyticAup()
        {
            AnalyticEventDTO value = default;
            return ByteOffset(ref value, ref value.EventAUP);
        }

        private static unsafe int ByteOffset<TStruct, TField>(ref TStruct owner, ref TField field)
            where TStruct : struct
            where TField : struct
        {
            return (int)((byte*)UnsafeUtility.AddressOf(ref field) - (byte*)UnsafeUtility.AddressOf(ref owner));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockAnalyticsEventsJob : IJob
    {
        [NoAlias] public NativeArray<AnalyticEventDTO> RoutineIngress;
        [NoAlias] public NativeArray<AnalyticsIngressCursorDTO> IngressCursor;
        public double3 OriginAUP;
        public uint TimestampSeconds;
        public uint SimulationFrame;
        public uint SectorHash;
        public uint Seed;
        public int EventCount;

        public void Execute()
        {
            if (!RoutineIngress.IsCreated || RoutineIngress.Length == 0 || !IngressCursor.IsCreated || IngressCursor.Length == 0)
                return;

            AnalyticsIngressCursorDTO cursor = IngressCursor[0];
            int capacity = math.min(RoutineIngress.Length, math.max(0, cursor.RoutineCapacity));
            if (capacity <= 0)
                return;

            uint seed = Seed ^ SectorHash ^ (SimulationFrame * 0x9E3779B9u);
            if (seed == 0u)
                seed = 0xA1601601u;
            Unity.Mathematics.Random random = new Unity.Mathematics.Random(seed);
            int count = math.max(0, EventCount);
            for (int i = 0; i < count; i++)
            {
                double x = (double)((int)(random.NextUInt() & 0x3FFu) - 512) * 0.25d;
                double z = (double)((int)(random.NextUInt() & 0x3FFu) - 512) * 0.25d;
                uint hash = (i & 31) == 0 ? AnalyticsEventHashes.ResourceDelta : AnalyticsEventHashes.MockRoute;
                double3 offset = default;
                offset.x = x;
                offset.z = z;
                AnalyticEventDTO dto = default;
                dto.EventHashID = hash;
                dto.TimestampSeconds = TimestampSeconds;
                dto.EventAUP = OriginAUP + offset;
                if (!TryWriteRoutineIngress(RoutineIngress, ref cursor, capacity, in dto))
                {
                    cursor.RoutineOverflowDrops += unchecked((uint)(count - i));
                    break;
                }
            }

            cursor.StateHash = AnalyticsMath.HashIngressCursor(in cursor);
            IngressCursor[0] = cursor;
        }

        private static bool TryWriteRoutineIngress(
            NativeArray<AnalyticEventDTO> routineIngress,
            ref AnalyticsIngressCursorDTO cursor,
            int capacity,
            in AnalyticEventDTO dto)
        {
            uint safeCapacity = (uint)capacity;
            if (cursor.RoutineWriteCursor - cursor.RoutineReadCursor >= safeCapacity)
                return false;

            int slot = (int)(cursor.RoutineWriteCursor % safeCapacity);
            routineIngress[slot] = dto;
            cursor.RoutineWriteCursor++;
            return true;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct ProcessAnalyticsQueueJob : IJob
    {
        [NoAlias] public NativeArray<AnalyticEventDTO> RoutineIngress;
        [NoAlias] public NativeArray<AnalyticEventDTO> CriticalIngress;
        [NoAlias] public NativeArray<AnalyticsIngressCursorDTO> IngressCursor;
        [NoAlias] public NativeArray<AnalyticEventDTO> EventRing;
        [NoAlias] public NativeArray<AnalyticEventDTO> StagingBuffer;
        [NoAlias] public NativeArray<AnalyticEventDTO> HeatmapDebug;
        [NoAlias] public NativeArray<AnalyticsCountersDTO> Counters;
        public float GlobalQualityWeight;
        public int BackgroundBacklogEvents;
        public uint FrameIndex;
        public uint TimestampSeconds;

        public void Execute()
        {
            if (!EventRing.IsCreated || !StagingBuffer.IsCreated || !Counters.IsCreated || Counters.Length == 0 ||
                !IngressCursor.IsCreated || IngressCursor.Length == 0)
                return;

            AnalyticsCountersDTO counters = Counters[0];
            AnalyticsIngressCursorDTO ingress = IngressCursor[0];
            int routineCapacity = RoutineIngress.IsCreated ? math.min(RoutineIngress.Length, math.max(0, ingress.RoutineCapacity)) : 0;
            int criticalCapacity = CriticalIngress.IsCreated ? math.min(CriticalIngress.Length, math.max(0, ingress.CriticalCapacity)) : 0;
            counters.StagedEventsThisFrame = 0u;
            counters.LastFrameIndex = FrameIndex;
            if (ingress.RoutineOverflowDrops != 0u || ingress.CriticalOverflowDrops != 0u)
            {
                counters.DroppedEvents += ingress.RoutineOverflowDrops + ingress.CriticalOverflowDrops;
                ingress.RoutineOverflowDrops = 0u;
                ingress.CriticalOverflowDrops = 0u;
            }

            float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0f);
            int cullThreshold = (int)math.round(math.lerp(10f, 1000f, quality));
            counters.LastCullThreshold = (uint)math.max(1, cullThreshold);
            int stagingLimit = math.min(StagingBuffer.Length, math.max(1, cullThreshold));
            int drainBudget = math.min(StagingBuffer.Length, math.max(1, cullThreshold));
            float threshold = math.max(1f, (float)cullThreshold);
            float pressure = BackgroundBacklogEvents / threshold;
            float overload01 = math.saturate((pressure - 1f) / math.max(pressure, 0.0001f));
            uint dropMilli = (uint)math.min(1000, (int)math.round(math.saturate(overload01 * math.step(1f, pressure)) * 1000f));

            int staged = 0;
            int drained = 0;
            while (staged < stagingLimit &&
                   drained < drainBudget &&
                   TryReadCriticalIngress(CriticalIngress, ref ingress, criticalCapacity, out AnalyticEventDTO critical))
            {
                drained++;
                if (!math.all(math.isfinite(critical.EventAUP)))
                {
                    counters.NonFiniteEvents++;
                    counters.DroppedEvents++;
                    continue;
                }

                if (critical.TimestampSeconds == 0u)
                    critical.TimestampSeconds = TimestampSeconds;

                StagingBuffer[staged++] = critical;
                if (EventRing.Length > 0)
                {
                    int ringIndex = (int)(counters.EventRingWriteCursor % (uint)EventRing.Length);
                    EventRing[ringIndex] = critical;
                    WriteHeatmapDebug(HeatmapDebug, counters.EventRingWriteCursor, critical);
                    counters.EventRingWriteCursor++;
                }

                counters.CriticalFlushRequests++;
            }

            while (staged < stagingLimit &&
                   drained < drainBudget &&
                   TryReadRoutineIngress(RoutineIngress, ref ingress, routineCapacity, out AnalyticEventDTO dto))
            {
                drained++;
                bool finite = math.all(math.isfinite(dto.EventAUP));
                if (!finite)
                {
                    counters.NonFiniteEvents++;
                    counters.DroppedEvents++;
                    continue;
                }

                if (dropMilli > 0u && ShouldDropRoutineDuringDrain(in dto, TimestampSeconds, unchecked((uint)math.max(0, BackgroundBacklogEvents)), dropMilli))
                {
                    counters.DroppedEvents++;
                    continue;
                }

                if (dto.TimestampSeconds == 0u)
                    dto.TimestampSeconds = TimestampSeconds;

                StagingBuffer[staged++] = dto;
                if (EventRing.Length > 0)
                {
                    int ringIndex = (int)(counters.EventRingWriteCursor % (uint)EventRing.Length);
                    EventRing[ringIndex] = dto;
                    WriteHeatmapDebug(HeatmapDebug, counters.EventRingWriteCursor, dto);
                    counters.EventRingWriteCursor++;
                }
            }

            while (drained < drainBudget &&
                   TryReadRoutineIngress(RoutineIngress, ref ingress, routineCapacity, out AnalyticEventDTO overflow))
            {
                drained++;
                if (!math.all(math.isfinite(overflow.EventAUP)))
                    counters.NonFiniteEvents++;
                counters.DroppedEvents++;
            }

            ingress.LastFrameIndex = FrameIndex;
            ingress.StateHash = AnalyticsMath.HashIngressCursor(in ingress);
            IngressCursor[0] = ingress;
            counters.DrainedEvents += (uint)drained;
            counters.StagedEventsThisFrame = (uint)staged;
            counters.WorkerBacklogEvents = (uint)math.max(0, BackgroundBacklogEvents);
            counters.StateHash = AnalyticsMath.HashCounters(in counters);
            Counters[0] = counters;
        }

        private static bool TryReadCriticalIngress(
            NativeArray<AnalyticEventDTO> criticalIngress,
            ref AnalyticsIngressCursorDTO cursor,
            int capacity,
            out AnalyticEventDTO dto)
        {
            return TryReadIngress(
                criticalIngress,
                ref cursor.CriticalReadCursor,
                cursor.CriticalWriteCursor,
                capacity,
                out dto);
        }

        private static bool TryReadRoutineIngress(
            NativeArray<AnalyticEventDTO> routineIngress,
            ref AnalyticsIngressCursorDTO cursor,
            int capacity,
            out AnalyticEventDTO dto)
        {
            return TryReadIngress(
                routineIngress,
                ref cursor.RoutineReadCursor,
                cursor.RoutineWriteCursor,
                capacity,
                out dto);
        }

        private static bool TryReadIngress(
            NativeArray<AnalyticEventDTO> ingress,
            ref uint readCursor,
            uint writeCursor,
            int capacity,
            out AnalyticEventDTO dto)
        {
            dto = default;
            if (!ingress.IsCreated || capacity <= 0 || readCursor == writeCursor)
                return false;

            uint safeCapacity = (uint)capacity;
            int slot = (int)(readCursor % safeCapacity);
            dto = ingress[slot];
            readCursor++;
            return true;
        }

        private static void WriteHeatmapDebug(NativeArray<AnalyticEventDTO> heatmapDebug, uint cursor, in AnalyticEventDTO dto)
        {
            if (!heatmapDebug.IsCreated || heatmapDebug.Length == 0)
                return;

            heatmapDebug[(int)(cursor % (uint)heatmapDebug.Length)] = dto;
        }

        private static bool ShouldDropRoutineDuringDrain(
            in AnalyticEventDTO dto,
            uint fallbackTimestampSeconds,
            uint backlog,
            uint dropMilli)
        {
            uint timestamp = dto.TimestampSeconds != 0u ? dto.TimestampSeconds : fallbackTimestampSeconds;
            uint gate = HashDrainGate(dto.EventHashID, timestamp, backlog, dto.EventAUP);
            return gate % 1000u < dropMilli;
        }

        private static uint HashDrainGate(uint eventHashId, uint timestampSeconds, uint backlog, double3 eventAup)
        {
            uint hash = 2166136261u;
            hash = (hash ^ eventHashId) * 16777619u;
            hash = (hash ^ timestampSeconds) * 16777619u;
            hash = (hash ^ backlog) * 16777619u;
            hash = (hash ^ FoldDoubleBitsBurst(eventAup.x)) * 16777619u;
            hash = (hash ^ FoldDoubleBitsBurst(eventAup.y)) * 16777619u;
            hash = (hash ^ FoldDoubleBitsBurst(eventAup.z)) * 16777619u;
            hash ^= hash >> 16;
            return hash;
        }

        private static uint FoldDoubleBitsBurst(double value)
        {
            ulong bits = unchecked((ulong)math.aslong(value));
            return unchecked((uint)bits ^ (uint)(bits >> 32));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct CompressAnalyticsBufferJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<byte> Source;
        [NoAlias] public NativeArray<byte> Destination;
        [NoAlias] public NativeArray<int> ResultBytes;
        public int SourceBytes;

        public void Execute()
        {
            if (!Source.IsCreated || !Destination.IsCreated || !ResultBytes.IsCreated || ResultBytes.Length == 0)
                return;

            ResultBytes[0] = AnalyticsCompression.CompressRleBlock(Source, SourceBytes, Destination);
        }
    }

    public static class AnalyticsCompression
    {
        public const uint RleEnvelopeMagic = 0x414E524Cu;
        public const uint RawPayloadMagic = 0x414E4152u;
        public const int RawHeaderBytes = 24;
        public const int RleEnvelopeHeaderBytes = 16;

        public static int CompressRleBlock(NativeArray<byte> source, int sourceBytes, NativeArray<byte> destination)
        {
            if (!source.IsCreated || !destination.IsCreated || sourceBytes <= 0)
                return 0;

            int limit = math.min(sourceBytes, source.Length);
            int src = 0;
            int dst = 0;
            while (src < limit && dst < destination.Length)
            {
                byte value = source[src];
                int run = 1;
                while (src + run < limit && run < 255 && source[src + run] == value)
                    run++;

                if (run >= 4 || value == 0xFF)
                {
                    if (dst + 3 > destination.Length)
                        return 0;

                    destination[dst++] = 0xFF;
                    destination[dst++] = (byte)run;
                    destination[dst++] = value;
                    src += run;
                }
                else
                {
                    destination[dst++] = value;
                    src++;
                }
            }

            return src == limit ? dst : 0;
        }

        public static int CompressRleEnvelope(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            if (source.Length <= 0 || destination.Length < RleEnvelopeHeaderBytes)
                return 0;

            int compressedBytes = CompressRleSpan(source, destination.Slice(RleEnvelopeHeaderBytes));
            if (compressedBytes <= 0)
                return 0;

            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), RleEnvelopeMagic);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), (uint)source.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(8, 4), (uint)compressedBytes);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(12, 4), 1u);
            return compressedBytes + RleEnvelopeHeaderBytes;
        }

        public static int CompressRleSpan(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            int src = 0;
            int dst = 0;
            while (src < source.Length && dst < destination.Length)
            {
                byte value = source[src];
                int run = 1;
                while (src + run < source.Length && run < 255 && source[src + run] == value)
                    run++;

                if (run >= 4 || value == 0xFF)
                {
                    if (dst + 3 > destination.Length)
                        return 0;

                    destination[dst++] = 0xFF;
                    destination[dst++] = (byte)run;
                    destination[dst++] = value;
                    src += run;
                }
                else
                {
                    destination[dst++] = value;
                    src++;
                }
            }

            return src == source.Length ? dst : 0;
        }
    }

    public static class AnalyticsMath
    {
        public static uint HashCounters(in AnalyticsCountersDTO counters)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, counters.EnqueuedEvents);
            hash = Mix(hash, counters.DrainedEvents);
            hash = Mix(hash, counters.DroppedEvents);
            hash = Mix(hash, counters.CriticalFlushRequests);
            hash = Mix(hash, counters.NonFiniteEvents);
            hash = Mix(hash, counters.WorkerBacklogEvents);
            return hash;
        }

        public static uint HashTelemetry(in AnalyticsExporterTelemetryEntry entry)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, entry.Frame);
            hash = Mix(hash, entry.SentEvents);
            hash = Mix(hash, entry.DiskFallbackEvents);
            hash = Mix(hash, entry.DroppedEvents);
            hash = Mix(hash, entry.BacklogEvents);
            hash = Mix(hash, (uint)entry.LastResponseCode);
            hash = Mix(hash, entry.FaultCount);
            hash = Mix(hash, entry.VaultBytes);
            return hash;
        }

        public static uint HashTuning(in AnalyticsTuningDTO tuning)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, math.asuint(tuning.GlobalQualityWeight));
            hash = Mix(hash, math.asuint(tuning.HeatmapSampleSeconds));
            hash = Mix(hash, unchecked((uint)tuning.BatchFlushThresholdBytes));
            hash = Mix(hash, unchecked((uint)tuning.NetworkTimeoutMs));
            hash = Mix(hash, unchecked((uint)tuning.Flags));
            hash = Mix(hash, tuning.EndpointHash);
            hash = Mix(hash, tuning.ApiKeyHash);
            return hash;
        }

        public static uint HashIngressCursor(in AnalyticsIngressCursorDTO cursor)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, cursor.RoutineWriteCursor);
            hash = Mix(hash, cursor.RoutineReadCursor);
            hash = Mix(hash, cursor.CriticalWriteCursor);
            hash = Mix(hash, cursor.CriticalReadCursor);
            hash = Mix(hash, unchecked((uint)cursor.RoutineCapacity));
            hash = Mix(hash, unchecked((uint)cursor.CriticalCapacity));
            hash = Mix(hash, cursor.RoutineOverflowDrops);
            hash = Mix(hash, cursor.CriticalOverflowDrops);
            hash = Mix(hash, cursor.LastFrameIndex);
            return hash;
        }

        public static uint HashBytes(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
                hash = (hash ^ bytes[i]) * 16777619u;
            return hash;
        }

        private static uint Mix(uint hash, uint value)
        {
            hash = (hash ^ value) * 16777619u;
            hash = (hash ^ (value >> 16)) * 16777619u;
            return hash;
        }
    }

    [DisallowMultipleComponent]
    public sealed class AsynchronousTelemetryExporter : MonoBehaviour, IDispatcherSystem, IGlobalRegistryHotSwapListener
    {
        private const uint SystemHash = 0xA160160u;
        private const int DefaultEventRingCapacity = 16384;
        private const int DefaultStagingCapacity = 4096;
        private const int DefaultTelemetryCapacity = 300;
        private const int DefaultCsvScratchBytes = 16384;
        private const int RawEventBytes = 32;
        private const int MaxHandoffEvents = 4096;
        private const int MaxRawBatchBytes = AnalyticsCompression.RawHeaderBytes + MaxHandoffEvents * RawEventBytes;
        private const int MaxCompressedBatchBytes = AnalyticsCompression.RleEnvelopeHeaderBytes + MaxRawBatchBytes * 3;
        private const int DefaultCompressedScratchBytes = MaxCompressedBatchBytes;
        private const int DumpHeaderBytes = 32;
        private const int DumpSnapshotBytes = DumpHeaderBytes + DefaultTelemetryCapacity * 64;
        private const int MaxBacklogReplayFilesPerFlush = 8;
        private const int WorkerWaitMilliseconds = 100;
        private const int WorkerJoinMilliseconds = 750;
        private const int BatchStateIdle = 0;
        private const int BatchStatePending = 1;
        private const int BatchStateWriting = 2;
        private const int DumpStateIdle = 0;
        private const int DumpStatePending = 1;
        private const int DumpStateWriting = 2;
        private const int IngressWriteRejected = 0;
        private const int IngressWriteAccepted = 1;
        private const int IngressWriteOverflow = 2;
        private const int WorkerFlagRunning = 1 << 0;
        private const int WorkerFlagDiskFallback = 1 << 1;
        private const int WorkerFlagEndpointConfigured = 1 << 2;
        private const int WorkerFlagFaulted = 1 << 3;
        private const ulong WorkerVaultMutationGuardMask = 1UL << 61;
        private static readonly ProfilerMarker ProcessQueueMarker = new ProfilerMarker("H8.Analytics.ProcessQueue");
        private static readonly ProfilerMarker HandoffMarker = new ProfilerMarker("H8.Analytics.ExportSignal");
        private static AsynchronousTelemetryExporter s_active;

        [Header("Capacity")]
        [SerializeField] private int _eventRingCapacity = DefaultEventRingCapacity;
        [SerializeField] private int _stagingCapacity = DefaultStagingCapacity;
        [SerializeField] private int _ingressExpectedCapacity = DefaultStagingCapacity;

        [Header("Cold Config")]
        [SerializeField] private string _endpointCsvRelativePath = "Assets/_Project/Data/Analytics/telemetry_config.csv";
        [SerializeField] private bool _networkEnabled;
        [SerializeField] private bool _generateMockAnalytics;
        [SerializeField] private bool _sampleKccHeatmap = true;
        [SerializeField] private bool _drawHeatmapGizmos = true;

        private IDataVault _dataVault;
        private VaultGenerationHandle<AnalyticEventDTO> _eventRingHandle;
        private VaultGenerationHandle<AnalyticEventDTO> _stagingHandle;
        private VaultGenerationHandle<AnalyticEventDTO> _routineIngressHandle;
        private VaultGenerationHandle<AnalyticEventDTO> _criticalIngressHandle;
        private VaultGenerationHandle<AnalyticsIngressCursorDTO> _ingressCursorHandle;
        private VaultGenerationHandle<AnalyticsCountersDTO> _countersHandle;
        private VaultGenerationHandle<AnalyticsExporterTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<AnalyticsTuningDTO> _tuningHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<byte> _compressedScratchHandle;
        private VaultGenerationHandle<AnalyticEventDTO> _heatmapDebugHandle;
        private VaultGenerationHandle<AnalyticEventDTO> _handoffAHandle;
        private VaultGenerationHandle<AnalyticEventDTO> _handoffBHandle;
        private VaultGenerationHandle<AnalyticEventDTO> _workerAccumHandle;
        private VaultGenerationHandle<byte> _rawBatchScratchHandle;
        private VaultGenerationHandle<byte> _dumpSnapshotHandle;
        private AnalyticsTuningDTO _cachedTuning;
        private uint _lastNonFiniteEvents;
        private uint _lastWorkerFaultCount;
        private uint _lastDumpFrame;
        private uint _lastKccHeatmapFrame;
        private uint _lastFrameTimeSignalFrame;
        private uint _lastSurvivalDeathFrame;
        private uint _survivalDeathSignalSourceId;
        private int _lastSurvivalDeathSignalSequence;
        private uint _fallbackFrameCounter;
        private uint _sessionTimestampSeconds;
        private double3 _lastKnownPlayerAup;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private HectonSurvivalSystem _survivalSystem;
        private float _heatmapTimerSeconds;
        private float _mockTimerSeconds;
        private float _sessionTimestampAccumulator;
        private bool _hasLastKnownPlayerAup;
        private bool _dispatcherRegistered;
        private bool _hotSwapListenerRegistered;
        private bool _storageReady;
        private IDataVault _workerStorageVault;

        private Thread _workerThread;
        private AutoResetEvent _flushSignal;
        private int _shutdownRequested;
        private int _pendingBatchState;
        private int _pendingBatchIndex = -1;
        private int _pendingBatchCount;
        private int _pendingDumpState;
        private int _pendingDumpBytes;
        private int _acceptingIngress;
        private int _mainThreadId;
        private int _handoffWriteIndex;
        private int _workerAccumCount;
        private long _lastWorkerFlushTicks;
        private int _workerLastResponseCode;
        private int _workerFlags;
        private int _workerHeartbeat;
        private int _workerFaultCount;
        private bool _workerBuffersLocked;
        private int _workerBatchFlushThresholdBytes;
        private int _workerNetworkTimeoutMs;
        private int _workerTuningFlags;
        private int _ingressPendingEstimate;
        private int _hotEnqueuedDelta;
        private int _hotDroppedDelta;
        private int _hotNonFiniteDelta;
        private HttpWebRequest _activeRequest;
        private long _workerSentEvents;
        private long _workerDiskFallbackEvents;
        private long _workerRawBytesSent;
        private long _workerCompressedBytesSent;
        private long _fallbackFileSequence;
        private string _endpointUrl;
        private string _apiKey;
        private string _fallbackDirectory;
        private IDataVault _workerBufferGuardVault;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticExporterState()
        {
            s_active = null;
        }

        public static bool TryRecordEvent(uint eventHashId, uint timestampSeconds, double3 eventAup)
        {
            AsynchronousTelemetryExporter active = s_active;
            if (active == null)
                return false;

            if (Volatile.Read(ref active._acceptingIngress) == 0 || !active.IsOwnerThread())
            {
                active.NoteHotPathDropped();
                return false;
            }

            if (!math.all(math.isfinite(eventAup)))
            {
                active.NoteHotPathNonFinite();
                return false;
            }

            if (!active.ShouldAcceptHotPathEvent(eventHashId, timestampSeconds, eventAup))
            {
                active.NoteHotPathDropped();
                return false;
            }

            int writeResult = active.TryWriteIngressEvent(eventHashId, timestampSeconds, eventAup);
            if (writeResult == IngressWriteAccepted)
            {
                active.NoteHotPathEnqueued();
                return true;
            }

            if (writeResult != IngressWriteOverflow)
                active.NoteHotPathDropped();
            return false;
        }

        public static bool TryRecordRouteSample(uint timestampSeconds, double3 eventAup)
        {
            return TryRecordEvent(AnalyticsEventHashes.RouteSample, timestampSeconds, eventAup);
        }

        public static bool TryRecordCriticalDeath(uint timestampSeconds, double3 eventAup)
        {
            return TryRecordEvent(AnalyticsEventHashes.Death, timestampSeconds, eventAup);
        }

        public static bool TryReadCounters(out AnalyticsCountersDTO counters)
        {
            counters = default;
            AsynchronousTelemetryExporter active = s_active;
            if (active == null || !active.TryReadCountersBuffer(out NativeArray<AnalyticsCountersDTO>.ReadOnly buffer))
                return false;

            counters = buffer[0];
            return true;
        }

        public static bool TryReadTuning(out AnalyticsTuningDTO tuning)
        {
            tuning = default;
            AsynchronousTelemetryExporter active = s_active;
            if (active == null || !active.TryReadTuningBuffer(out NativeArray<AnalyticsTuningDTO>.ReadOnly buffer))
                return false;

            tuning = buffer[0];
            return true;
        }

        public static bool TryWriteTuning(in AnalyticsTuningDTO tuning)
        {
            AsynchronousTelemetryExporter active = s_active;
            if (active == null ||
                !active.IsOwnerThread() ||
                !active.TryOpenTuningForOwner(out NativeArray<AnalyticsTuningDTO> buffer))
                return false;

            AnalyticsTuningDTO sanitized = active.SanitizeTuning(tuning);
            buffer[0] = sanitized;
            active._cachedTuning = sanitized;
            active.ApplyWorkerTuningSnapshot(in sanitized);
            return true;
        }

        private bool IsOwnerThread()
        {
            return _mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId == _mainThreadId;
        }

        public static bool TryReadLatestTelemetry(out AnalyticsExporterTelemetryEntry entry)
        {
            entry = default;
            AsynchronousTelemetryExporter active = s_active;
            if (active == null || !active.TryReadTelemetryBuffers(out NativeArray<AnalyticsExporterTelemetryEntry>.ReadOnly ring, out NativeArray<int>.ReadOnly cursor))
                return false;

            int index = (cursor[0] + ring.Length - 1) % ring.Length;
            entry = ring[index];
            return true;
        }

        public uint GetSystemIdHash() => SystemHash;

        public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.PostSimulation;

        public byte GetBucketId() => 0;

        public int GetDependencyCount() => 0;

        public uint GetDependencyHash(int dependencyIndex) => 0u;

        public void PreSimulationTick(in DispatcherTimingDTO timing) { }

        public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) => dependsOn;

        public void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            if (!_storageReady || !HasIngressStorage())
                return;

            RefreshTuningFromVault();
            uint frameId = ResolveFrameId(in timing);
            uint timestampSeconds = AdvanceSessionTimestamp(timing.FrameDelta);
            TrySampleKccHeatmap(
                timing.FrameDelta,
                timestampSeconds,
                (_cachedTuning.Flags & AnalyticsExporterFlags.HeatmapKcc) != 0);

            IngestGameplaySignals(timestampSeconds, frameId);

            if ((_cachedTuning.Flags & AnalyticsExporterFlags.MockEvents) != 0)
                GenerateMockAnalytics(timing.FrameDelta, timestampSeconds, frameId);

            ProcessQueue(timestampSeconds, frameId);
            WriteExporterTelemetry(timestampSeconds, frameId);
        }

        public void VisualSyncTick(in DispatcherTimingDTO timing) { }

        private void OnEnable()
        {
            AnalyticsLayout.ValidateOrThrow();
            if (s_active != null && s_active != this)
                return;

            s_active = this;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            Volatile.Write(ref _acceptingIngress, 0);
            _dataVault = GlobalRegistry.DataVault;
            TryRegisterHotSwapListener();
            _fallbackDirectory = ResolveFallbackDirectory();

            ResetHotPathCounters();
            CachePlayerRuntimeContext(GlobalRegistry.Player);
            RefreshSurvivalSignalBinding();
            AllocateColdManagedObjects();
            _storageReady = TryAcquireVaultStorage();
            LoadEndpointConfigurationCold();
            if (_storageReady && StartWorker())
                Volatile.Write(ref _acceptingIngress, 1);
            else if (_storageReady)
                ReleaseWorkerStorageAfterStartFailure();

            if (!_dispatcherRegistered && GlobalRegistry.TryRegisterDispatcherSystem(this))
                _dispatcherRegistered = true;
        }

        private void OnDisable()
        {
            if (_dispatcherRegistered)
            {
                GlobalRegistry.UnregisterDispatcherSystem(this);
                _dispatcherRegistered = false;
            }

            Volatile.Write(ref _acceptingIngress, 0);
            TryUnregisterHotSwapListener();
            ClearPlayerRuntimeContext();
            if (!StopWorker())
            {
                _storageReady = false;
                return;
            }

            TeardownStoppedWorkerState();
        }

        private void OnDestroy()
        {
            Volatile.Write(ref _acceptingIngress, 0);
            TryUnregisterHotSwapListener();
            ClearPlayerRuntimeContext();
            if (StopWorker())
                TeardownStoppedWorkerState();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);
                RefreshSurvivalSignalBinding();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            RebindDataVault(currentService as IDataVault, previousService as IDataVault);
        }

        private void TeardownStoppedWorkerState()
        {
            ResetHotPathCounters();
            _storageReady = false;
            ReleaseVaultHandles(_workerStorageVault ?? _dataVault);
            _dataVault = null;
            if (s_active == this)
                s_active = null;
        }

        private void AllocateColdManagedObjects()
        {
            _eventRingCapacity = math.max(300, _eventRingCapacity);
            _stagingCapacity = math.clamp(_stagingCapacity, 64, MaxHandoffEvents);
            _ingressExpectedCapacity = math.max(_ingressExpectedCapacity, _stagingCapacity);
            if (_flushSignal == null)
            {
                // COLD ALLOC: AutoResetEvent[1] - background telemetry wake signal - owner: AsynchronousTelemetryExporter
                _flushSignal = new AutoResetEvent(false);
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private bool TryReadWorkerBuffer<T>(
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (!_storageReady ||
                vault == null ||
                requiredLength <= 0 ||
                !IsVaultHandleCreated(in handle) ||
                !vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly resolved) ||
                resolved.Length < requiredLength)
            {
                return false;
            }

            buffer = resolved;
            return true;
        }

        private bool TryOpenWorkerBufferForOwner<T>(
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (!_workerBuffersLocked ||
                vault == null ||
                requiredLength <= 0 ||
                !IsVaultHandleCreated(in handle) ||
                !vault.TryResolveHandle(in handle, out NativeArray<T> resolved) ||
                !resolved.IsCreated ||
                resolved.Length < requiredLength)
            {
                return false;
            }

            buffer = resolved;
            return true;
        }

        private void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            try
            {
                if (vault != null && IsVaultHandleCreated(in handle))
                    vault.ReleaseBuffer(in handle);
                else if (IsVaultHandleCreated(in handle))
                {
                    Interlocked.Increment(ref _workerFaultCount);
                    SetWorkerFlag(WorkerFlagFaulted);
                }
            }
            catch (Exception)
            {
                Interlocked.Increment(ref _workerFaultCount);
                SetWorkerFlag(WorkerFlagFaulted);
            }
            finally
            {
                handle = default;
            }
        }

        private void ReleaseVaultHandles(IDataVault releaseVaultFallback = null)
        {
            UnlockWorkerVaultBuffers();

            IDataVault vault = _workerStorageVault ?? releaseVaultFallback ?? _dataVault;
            ReleaseVaultHandle(vault, ref _eventRingHandle);
            ReleaseVaultHandle(vault, ref _stagingHandle);
            ReleaseVaultHandle(vault, ref _routineIngressHandle);
            ReleaseVaultHandle(vault, ref _criticalIngressHandle);
            ReleaseVaultHandle(vault, ref _ingressCursorHandle);
            ReleaseVaultHandle(vault, ref _countersHandle);
            ReleaseVaultHandle(vault, ref _telemetryHandle);
            ReleaseVaultHandle(vault, ref _telemetryCursorHandle);
            ReleaseVaultHandle(vault, ref _tuningHandle);
            ReleaseVaultHandle(vault, ref _csvScratchHandle);
            ReleaseVaultHandle(vault, ref _compressedScratchHandle);
            ReleaseVaultHandle(vault, ref _heatmapDebugHandle);
            ReleaseVaultHandle(vault, ref _handoffAHandle);
            ReleaseVaultHandle(vault, ref _handoffBHandle);
            ReleaseVaultHandle(vault, ref _workerAccumHandle);
            ReleaseVaultHandle(vault, ref _rawBatchScratchHandle);
            ReleaseVaultHandle(vault, ref _dumpSnapshotHandle);
            _workerStorageVault = null;
        }

        private bool TryAcquireVaultStorage()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            _dataVault = vault;
            _workerStorageVault = vault;
            _eventRingHandle = vault.EnsureGenerationHandle<AnalyticEventDTO>(
                AnalyticsVaultBufferIds.EventRing,
                _eventRingCapacity,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);
            _stagingHandle = vault.EnsureGenerationHandle<AnalyticEventDTO>(
                AnalyticsVaultBufferIds.Staging,
                _stagingCapacity,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);
            _routineIngressHandle = vault.EnsureGenerationHandle<AnalyticEventDTO>(
                AnalyticsVaultBufferIds.RoutineIngress,
                _ingressExpectedCapacity,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);
            _criticalIngressHandle = vault.EnsureGenerationHandle<AnalyticEventDTO>(
                AnalyticsVaultBufferIds.CriticalIngress,
                math.max(64, _ingressExpectedCapacity >> 4),
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);
            _ingressCursorHandle = vault.EnsureGenerationHandle<AnalyticsIngressCursorDTO>(
                AnalyticsVaultBufferIds.IngressCursor,
                1,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            _countersHandle = vault.EnsureGenerationHandle<AnalyticsCountersDTO>(
                AnalyticsVaultBufferIds.Counters,
                1,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            _telemetryHandle = vault.EnsureGenerationHandle<AnalyticsExporterTelemetryEntry>(
                AnalyticsVaultBufferIds.TelemetryRing,
                DefaultTelemetryCapacity,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            _telemetryCursorHandle = vault.EnsureGenerationHandle<int>(
                AnalyticsVaultBufferIds.TelemetryCursor,
                1,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            _tuningHandle = vault.EnsureGenerationHandle<AnalyticsTuningDTO>(
                AnalyticsVaultBufferIds.Tuning,
                1,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.ClearMemory);
            _csvScratchHandle = vault.EnsureGenerationHandle<byte>(
                AnalyticsVaultBufferIds.CsvScratch,
                DefaultCsvScratchBytes,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);
            _compressedScratchHandle = vault.EnsureGenerationHandle<byte>(
                AnalyticsVaultBufferIds.CompressedScratch,
                DefaultCompressedScratchBytes,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);
            _heatmapDebugHandle = vault.EnsureGenerationHandle<AnalyticEventDTO>(
                AnalyticsVaultBufferIds.HeatmapDebug,
                512,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);
            _handoffAHandle = vault.EnsureGenerationHandle<AnalyticEventDTO>(
                AnalyticsVaultBufferIds.HandoffA,
                MaxHandoffEvents,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);
            _handoffBHandle = vault.EnsureGenerationHandle<AnalyticEventDTO>(
                AnalyticsVaultBufferIds.HandoffB,
                MaxHandoffEvents,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);
            _workerAccumHandle = vault.EnsureGenerationHandle<AnalyticEventDTO>(
                AnalyticsVaultBufferIds.WorkerAccum,
                MaxHandoffEvents,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);
            _rawBatchScratchHandle = vault.EnsureGenerationHandle<byte>(
                AnalyticsVaultBufferIds.RawBatchScratch,
                MaxRawBatchBytes,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);
            _dumpSnapshotHandle = vault.EnsureGenerationHandle<byte>(
                AnalyticsVaultBufferIds.DumpSnapshot,
                DumpSnapshotBytes,
                SystemID.CoreDiagnostics,
                NativeArrayOptions.UninitializedMemory);

            _cachedTuning = CreateDefaultTuning();
            bool ready =
                IsVaultHandleCreated(in _eventRingHandle) &&
                IsVaultHandleCreated(in _stagingHandle) &&
                IsVaultHandleCreated(in _routineIngressHandle) &&
                IsVaultHandleCreated(in _criticalIngressHandle) &&
                IsVaultHandleCreated(in _ingressCursorHandle) &&
                IsVaultHandleCreated(in _countersHandle) &&
                IsVaultHandleCreated(in _telemetryHandle) &&
                IsVaultHandleCreated(in _telemetryCursorHandle) &&
                IsVaultHandleCreated(in _tuningHandle) &&
                IsVaultHandleCreated(in _csvScratchHandle) &&
                IsVaultHandleCreated(in _heatmapDebugHandle) &&
                IsVaultHandleCreated(in _handoffAHandle) &&
                IsVaultHandleCreated(in _handoffBHandle) &&
                IsVaultHandleCreated(in _workerAccumHandle) &&
                IsVaultHandleCreated(in _rawBatchScratchHandle) &&
                IsVaultHandleCreated(in _dumpSnapshotHandle) &&
                IsVaultHandleCreated(in _compressedScratchHandle);
            if (ready)
                ready = LockWorkerVaultBuffers();

            if (ready)
            {
                if (TryOpenTuningForOwner(out NativeArray<AnalyticsTuningDTO> tuning))
                {
                    tuning[0] = _cachedTuning;
                    ApplyWorkerTuningSnapshot(in _cachedTuning);
                }
                else
                {
                    ready = false;
                }
            }

            if (ready)
                ready = InitializeIngressCursor();

            if (!ready)
            {
                UnlockWorkerVaultBuffers();
                ReleaseVaultHandles(vault);
            }

            return ready;
        }

        private void RebindDataVault(IDataVault nextVault, IDataVault previousVault = null)
        {
            if (ReferenceEquals(_dataVault, nextVault))
                return;

            Volatile.Write(ref _acceptingIngress, 0);
            if (!StopWorker())
            {
                _storageReady = false;
                return;
            }

            ResetHotPathCounters();
            _storageReady = false;
            ReleaseVaultHandles(_workerStorageVault ?? _dataVault ?? previousVault);
            _dataVault = nextVault;
            if (_dataVault == null || !isActiveAndEnabled)
                return;

            _storageReady = TryAcquireVaultStorage();
            if (!_storageReady)
                return;

            if (StartWorker())
                Volatile.Write(ref _acceptingIngress, 1);
            else
                ReleaseWorkerStorageAfterStartFailure();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private bool InitializeIngressCursor()
        {
            if (!TryOpenWorkerBufferForOwner(
                    in _ingressCursorHandle,
                    1,
                    out NativeArray<AnalyticsIngressCursorDTO> cursorBuffer) ||
                !TryOpenWorkerBufferForOwner(
                    in _routineIngressHandle,
                    1,
                    out NativeArray<AnalyticEventDTO> routineIngress) ||
                !TryOpenWorkerBufferForOwner(
                    in _criticalIngressHandle,
                    1,
                    out NativeArray<AnalyticEventDTO> criticalIngress))
            {
                return false;
            }

            AnalyticsIngressCursorDTO cursor = default;
            cursor.RoutineCapacity = routineIngress.Length;
            cursor.CriticalCapacity = criticalIngress.Length;
            cursor.StateHash = AnalyticsMath.HashIngressCursor(in cursor);
            cursorBuffer[0] = cursor;
            return cursor.RoutineCapacity > 0 && cursor.CriticalCapacity > 0;
        }

        private bool LockWorkerVaultBuffers()
        {
            IDataVault vault = _dataVault;
            if (_workerBuffersLocked || vault == null)
                return _workerBuffersLocked;

            _workerBuffersLocked = vault.TryAcquireMutationGuard(WorkerVaultMutationGuardMask);
            if (_workerBuffersLocked)
                _workerBufferGuardVault = vault;
            return _workerBuffersLocked;
        }

        private void UnlockWorkerVaultBuffers()
        {
            if (!_workerBuffersLocked)
                return;

            try
            {
                IDataVault vault = _workerBufferGuardVault ?? _workerStorageVault ?? _dataVault;
                if (vault != null)
                    vault.ReleaseMutationGuard(WorkerVaultMutationGuardMask);
                else
                {
                    Interlocked.Increment(ref _workerFaultCount);
                    SetWorkerFlag(WorkerFlagFaulted);
                }
            }
            catch (Exception)
            {
                Interlocked.Increment(ref _workerFaultCount);
                SetWorkerFlag(WorkerFlagFaulted);
            }
            finally
            {
                _workerBuffersLocked = false;
                _workerBufferGuardVault = null;
            }
        }

        private AnalyticsTuningDTO CreateDefaultTuning()
        {
            AnalyticsTuningDTO tuning = default;
            tuning.GlobalQualityWeight = math.saturate(HomeostasisBrain.GlobalQualityWeight);
            tuning.HeatmapSampleSeconds = 5f;
            tuning.BatchFlushThresholdBytes = 32 * 1024;
            tuning.NetworkTimeoutMs = 3000;
            tuning.LowCullThreshold = 10;
            tuning.UltraCullThreshold = 1000;
            tuning.StagingCapacity = _stagingCapacity;
            tuning.Flags = (_generateMockAnalytics ? AnalyticsExporterFlags.MockEvents : 0) |
                           (_sampleKccHeatmap ? AnalyticsExporterFlags.HeatmapKcc : 0) |
                           (_networkEnabled ? AnalyticsExporterFlags.NetworkEnabled : 0);
            tuning.EndpointHash = 0u;
            tuning.ApiKeyHash = 0u;
            tuning.RouteSampleHash = AnalyticsEventHashes.RouteSample;
            tuning.DeathHash = AnalyticsEventHashes.Death;
            tuning.ResourceHash = AnalyticsEventHashes.ResourceDelta;
            tuning.PerfSpikeHash = AnalyticsEventHashes.PerfSpike;
            tuning.StateHash = AnalyticsMath.HashTuning(in tuning);
            return tuning;
        }

        private void ApplyWorkerTuningSnapshot(in AnalyticsTuningDTO tuning)
        {
            Volatile.Write(ref _workerBatchFlushThresholdBytes, tuning.BatchFlushThresholdBytes);
            Volatile.Write(ref _workerNetworkTimeoutMs, tuning.NetworkTimeoutMs);
            Volatile.Write(ref _workerTuningFlags, tuning.Flags);
        }

        private AnalyticsTuningDTO SanitizeTuning(in AnalyticsTuningDTO input)
        {
            AnalyticsTuningDTO tuning = input;
            tuning.GlobalQualityWeight = math.saturate(math.isfinite(tuning.GlobalQualityWeight) ? tuning.GlobalQualityWeight : HomeostasisBrain.GlobalQualityWeight);
            tuning.HeatmapSampleSeconds = math.clamp(math.isfinite(tuning.HeatmapSampleSeconds) ? tuning.HeatmapSampleSeconds : 5f, 0.5f, 60f);
            tuning.BatchFlushThresholdBytes = math.clamp(tuning.BatchFlushThresholdBytes, 1024, MaxRawBatchBytes);
            tuning.NetworkTimeoutMs = math.clamp(tuning.NetworkTimeoutMs, 250, 30000);
            tuning.LowCullThreshold = math.clamp(tuning.LowCullThreshold, 1, 256);
            tuning.UltraCullThreshold = math.clamp(tuning.UltraCullThreshold, 64, MaxHandoffEvents);
            tuning.StagingCapacity = math.clamp(tuning.StagingCapacity, 64, _stagingCapacity);
            tuning.RouteSampleHash = tuning.RouteSampleHash != 0u ? tuning.RouteSampleHash : AnalyticsEventHashes.RouteSample;
            tuning.DeathHash = tuning.DeathHash != 0u ? tuning.DeathHash : AnalyticsEventHashes.Death;
            tuning.ResourceHash = tuning.ResourceHash != 0u ? tuning.ResourceHash : AnalyticsEventHashes.ResourceDelta;
            tuning.PerfSpikeHash = tuning.PerfSpikeHash != 0u ? tuning.PerfSpikeHash : AnalyticsEventHashes.PerfSpike;
            tuning.StateHash = AnalyticsMath.HashTuning(in tuning);
            return tuning;
        }

        private void RefreshTuningFromVault()
        {
            if (!TryOpenTuningForOwner(out NativeArray<AnalyticsTuningDTO> tuning))
                return;

            AnalyticsTuningDTO next = tuning[0];
            next.GlobalQualityWeight = math.saturate(HomeostasisBrain.GlobalQualityWeight);
            next.EndpointHash = _cachedTuning.EndpointHash;
            next.ApiKeyHash = _cachedTuning.ApiKeyHash;
            next = SanitizeTuning(next);
            tuning[0] = next;
            _cachedTuning = next;
            ApplyWorkerTuningSnapshot(in next);
        }

        private bool ShouldAcceptHotPathEvent(uint eventHashId, uint timestampSeconds, double3 eventAup)
        {
            if ((eventHashId & AnalyticsEventHashes.CriticalMask) != 0u)
                return true;

            int backlog = ResolveBacklogPressureEvents();
            AnalyticsTuningDTO tuning = _cachedTuning;
            float quality = math.saturate(math.isfinite(tuning.GlobalQualityWeight) ? tuning.GlobalQualityWeight : HomeostasisBrain.GlobalQualityWeight);
            float smoothQuality = quality * quality * (3f - 2f * quality);
            int low = math.max(1, tuning.LowCullThreshold);
            int ultra = math.max(low, tuning.UltraCullThreshold);
            float threshold = math.max(1f, math.lerp((float)low, (float)ultra, smoothQuality));
            float pressure = backlog / threshold;
            float overload01 = math.saturate((pressure - 1f) / math.max(pressure, 0.0001f));
            float pressureGate = math.step(1f, pressure);
            float drop01 = math.saturate(overload01 * pressureGate);
            if (drop01 <= 0f)
                return true;

            uint gate = HashHotPathGate(eventHashId, timestampSeconds, unchecked((uint)backlog), eventAup);
            uint dropMilli = (uint)math.min(1000, (int)math.round(drop01 * 1000f));
            return gate % 1000u >= dropMilli;
        }

        private bool HasIngressStorage()
        {
            return IsVaultHandleCreated(in _routineIngressHandle) &&
                   IsVaultHandleCreated(in _criticalIngressHandle) &&
                   IsVaultHandleCreated(in _ingressCursorHandle);
        }

        private int TryWriteIngressEvent(uint eventHashId, uint timestampSeconds, double3 eventAup)
        {
            if (!TryOpenIngressBuffersForOwner(
                    out NativeArray<AnalyticEventDTO> routineIngress,
                    out NativeArray<AnalyticEventDTO> criticalIngress,
                    out NativeArray<AnalyticsIngressCursorDTO> ingressCursor))
            {
                return IngressWriteRejected;
            }

            AnalyticsIngressCursorDTO cursor = ingressCursor[0];
            bool critical = (eventHashId & AnalyticsEventHashes.CriticalMask) != 0u;
            NativeArray<AnalyticEventDTO> events = critical ? criticalIngress : routineIngress;
            int declaredCapacity = critical ? cursor.CriticalCapacity : cursor.RoutineCapacity;
            int capacity = math.min(events.Length, math.max(0, declaredCapacity));
            if (!events.IsCreated || capacity <= 0)
                return IngressWriteRejected;

            uint safeCapacity = (uint)capacity;
            uint writeCursor = critical ? cursor.CriticalWriteCursor : cursor.RoutineWriteCursor;
            uint readCursor = critical ? cursor.CriticalReadCursor : cursor.RoutineReadCursor;
            if (writeCursor - readCursor >= safeCapacity)
            {
                if (critical)
                    cursor.CriticalOverflowDrops += 1u;
                else
                    cursor.RoutineOverflowDrops += 1u;
                cursor.StateHash = AnalyticsMath.HashIngressCursor(in cursor);
                ingressCursor[0] = cursor;
                return IngressWriteOverflow;
            }

            AnalyticEventDTO dto = default;
            dto.EventHashID = eventHashId;
            dto.TimestampSeconds = timestampSeconds;
            dto.EventAUP = eventAup;

            events[(int)(writeCursor % safeCapacity)] = dto;
            if (critical)
                cursor.CriticalWriteCursor = writeCursor + 1u;
            else
                cursor.RoutineWriteCursor = writeCursor + 1u;
            cursor.StateHash = AnalyticsMath.HashIngressCursor(in cursor);
            ingressCursor[0] = cursor;
            return IngressWriteAccepted;
        }

        private int ResolveBacklogPressureEvents()
        {
            int pending = math.max(0, Volatile.Read(ref _ingressPendingEstimate));
            int handoff = Volatile.Read(ref _pendingBatchState) == BatchStatePending
                ? math.max(0, Volatile.Read(ref _pendingBatchCount))
                : 0;
            int workerAccum = math.max(0, Volatile.Read(ref _workerAccumCount));
            long total = (long)pending + handoff + workerAccum;
            return total > int.MaxValue ? int.MaxValue : (int)total;
        }

        private static uint HashHotPathGate(uint eventHashId, uint timestampSeconds, uint backlog, double3 eventAup)
        {
            uint hash = 2166136261u;
            hash = (hash ^ eventHashId) * 16777619u;
            hash = (hash ^ timestampSeconds) * 16777619u;
            hash = (hash ^ backlog) * 16777619u;
            hash = (hash ^ FoldDoubleBits(eventAup.x)) * 16777619u;
            hash = (hash ^ FoldDoubleBits(eventAup.y)) * 16777619u;
            hash = (hash ^ FoldDoubleBits(eventAup.z)) * 16777619u;
            hash ^= hash >> 16;
            return hash;
        }

        private static uint FoldDoubleBits(double value)
        {
            ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
            return unchecked((uint)bits ^ (uint)(bits >> 32));
        }

        private void NoteHotPathEnqueued()
        {
            Interlocked.Increment(ref _hotEnqueuedDelta);
            Interlocked.Increment(ref _ingressPendingEstimate);
        }

        private void NoteHotPathDropped()
        {
            Interlocked.Increment(ref _hotDroppedDelta);
        }

        private void NoteHotPathNonFinite()
        {
            Interlocked.Increment(ref _hotDroppedDelta);
            Interlocked.Increment(ref _hotNonFiniteDelta);
        }

        private void ResetHotPathCounters()
        {
            Interlocked.Exchange(ref _ingressPendingEstimate, 0);
            Interlocked.Exchange(ref _hotEnqueuedDelta, 0);
            Interlocked.Exchange(ref _hotDroppedDelta, 0);
            Interlocked.Exchange(ref _hotNonFiniteDelta, 0);
            Interlocked.Exchange(ref _pendingDumpState, DumpStateIdle);
            Volatile.Write(ref _pendingDumpBytes, 0);
            _fallbackFrameCounter = 0u;
            _sessionTimestampSeconds = 1u;
            _sessionTimestampAccumulator = 0f;
            _lastFrameTimeSignalFrame = 0u;
            _lastSurvivalDeathFrame = 0u;
            _lastSurvivalDeathSignalSequence = 0;
            _lastKnownPlayerAup = double3.zero;
            _hasLastKnownPlayerAup = false;
        }

        private uint ResolveFrameId(in DispatcherTimingDTO timing)
        {
            if (timing.FrameId != 0u)
            {
                _fallbackFrameCounter = timing.FrameId;
                return timing.FrameId;
            }

            uint next = _fallbackFrameCounter + 1u;
            if (next == 0u)
                next = 1u;
            _fallbackFrameCounter = next;
            return next;
        }

        private uint ResolveLastFrameId()
        {
            return _fallbackFrameCounter != 0u ? _fallbackFrameCounter : 1u;
        }

        private uint AdvanceSessionTimestamp(float deltaTime)
        {
            float safeDelta = math.isfinite(deltaTime) ? math.clamp(deltaTime, 0f, 10f) : 0f;
            _sessionTimestampAccumulator += safeDelta;
            int wholeSeconds = (int)math.floor(_sessionTimestampAccumulator);
            if (wholeSeconds > 0)
            {
                _sessionTimestampSeconds += unchecked((uint)wholeSeconds);
                _sessionTimestampAccumulator -= wholeSeconds;
            }

            return _sessionTimestampSeconds != 0u ? _sessionTimestampSeconds : 1u;
        }

        private void ApplyHotPathCounterDeltas(NativeArray<AnalyticsCountersDTO> counters)
        {
            if (!counters.IsCreated || counters.Length == 0)
                return;

            int enqueued = Interlocked.Exchange(ref _hotEnqueuedDelta, 0);
            int dropped = Interlocked.Exchange(ref _hotDroppedDelta, 0);
            int nonFinite = Interlocked.Exchange(ref _hotNonFiniteDelta, 0);
            if (enqueued == 0 && dropped == 0 && nonFinite == 0)
                return;

            AnalyticsCountersDTO value = counters[0];
            if (enqueued > 0)
                value.EnqueuedEvents += unchecked((uint)enqueued);
            if (dropped > 0)
                value.DroppedEvents += unchecked((uint)dropped);
            if (nonFinite > 0)
                value.NonFiniteEvents += unchecked((uint)nonFinite);
            value.StateHash = AnalyticsMath.HashCounters(in value);
            counters[0] = value;
        }

        private void SubtractIngressPendingEstimate(int drained)
        {
            if (drained <= 0)
                return;

            int observed;
            int next;
            do
            {
                observed = Volatile.Read(ref _ingressPendingEstimate);
                next = math.max(0, observed - drained);
            }
            while (Interlocked.CompareExchange(ref _ingressPendingEstimate, next, observed) != observed);
        }

        private static int SaturateCursorDelta(uint delta)
        {
            return delta > int.MaxValue ? int.MaxValue : (int)delta;
        }

        private void TrySampleKccHeatmap(float deltaTime, uint timestampSeconds, bool recordRouteSample)
        {
            _heatmapTimerSeconds += math.max(0f, deltaTime);
            ReadOnlySpan<KccVelocitySignal> signals = SignalBus<KccVelocitySignal>.GetSignals();
            if (signals.Length == 0)
                return;

            KccVelocitySignal latest = default;
            bool found = false;
            for (int i = 0; i < signals.Length; i++)
            {
                KccVelocitySignal candidate = signals[i];
                if (candidate.Sequence == 0u)
                    continue;

                if (!found ||
                    candidate.Frame > latest.Frame ||
                    (candidate.Frame == latest.Frame && candidate.Sequence > latest.Sequence))
                {
                    latest = candidate;
                    found = true;
                }
            }

            if (!found || latest.Frame == _lastKccHeatmapFrame)
                return;

            _lastKccHeatmapFrame = latest.Frame;
            double3 aup = latest.BodyAup.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(aup)))
                return;

            _lastKnownPlayerAup = aup;
            _hasLastKnownPlayerAup = true;

            float sampleSeconds = math.max(0.5f, _cachedTuning.HeatmapSampleSeconds);
            if (!recordRouteSample || _heatmapTimerSeconds < sampleSeconds)
                return;

            _heatmapTimerSeconds = 0f;
            TryRecordEvent(AnalyticsEventHashes.RouteSample, timestampSeconds, aup);
        }

        private void IngestGameplaySignals(uint timestampSeconds, uint frameId)
        {
            IngestEntityDeathSignals(timestampSeconds);
            IngestItemAcquiredSignals(timestampSeconds);
            IngestSurvivalDeathSignals(timestampSeconds, frameId);
            IngestFrameTimeSignals(timestampSeconds, frameId);
        }

        private void IngestEntityDeathSignals(uint timestampSeconds)
        {
            ReadOnlySpan<EntityDeathSignal> signals = SignalBus<EntityDeathSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                double3 aup = signals[i].PositionAup.ToAbsoluteDouble3();
                if (math.all(math.isfinite(aup)))
                    TryRecordEvent(AnalyticsEventHashes.Death, timestampSeconds, aup);
            }
        }

        private void IngestItemAcquiredSignals(uint timestampSeconds)
        {
            ReadOnlySpan<ItemAcquiredSignal> signals = SignalBus<ItemAcquiredSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ItemAcquiredSignal signal = signals[i];
                if (!IsResourceDeltaSource(signal.SourceKind))
                    continue;

                double3 aup = signal.PositionAup.ToAbsoluteDouble3();
                if (math.all(math.isfinite(aup)))
                    TryRecordEvent(AnalyticsEventHashes.ResourceDelta, timestampSeconds, aup);
            }
        }

        private static bool IsResourceDeltaSource(byte sourceKind)
        {
            return sourceKind == ItemAcquiredSignalSourceKinds.Unknown ||
                   sourceKind == ItemAcquiredSignalSourceKinds.ResourceNode ||
                   sourceKind == ItemAcquiredSignalSourceKinds.ProceduralOreSpawner ||
                   sourceKind == ItemAcquiredSignalSourceKinds.DeployableSdfDrill ||
                   sourceKind == ItemAcquiredSignalSourceKinds.VoxelCarve ||
                   sourceKind == ItemAcquiredSignalSourceKinds.ScavengingLootOracle ||
                   sourceKind == ItemAcquiredSignalSourceKinds.HarvestableOutcrop ||
                   sourceKind == ItemAcquiredSignalSourceKinds.DroneMining;
        }

        private void IngestSurvivalDeathSignals(uint timestampSeconds, uint frameId)
        {
            if (!_hasLastKnownPlayerAup)
                return;

            uint sourceId = _survivalDeathSignalSourceId;
            if (sourceId == 0u)
                return;

            IngestLatestSurvivalDeathSignal(timestampSeconds, frameId, sourceId);

            ReadOnlySpan<SurvivalVitalsChangedSignal> signals = SignalBus<SurvivalVitalsChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                SurvivalVitalsChangedSignal signal = signals[i];
                uint signalFrame = ResolveSurvivalDeathSignalFrame(in signal, frameId);
                TryRecordSurvivalDeathTelemetry(in signal, sourceId, timestampSeconds, signalFrame);
            }
        }

        private void IngestLatestSurvivalDeathSignal(uint timestampSeconds, uint frameId, uint sourceId)
        {
            if (!SurvivalSignalRoute.TryGetLatestDeathForSource(sourceId, out SurvivalVitalsChangedSignal signal, out int sequence))
                return;

            if (sequence == _lastSurvivalDeathSignalSequence)
                return;

            uint signalFrame = ResolveSurvivalDeathSignalFrame(in signal, frameId);
            if (TryRecordSurvivalDeathTelemetry(in signal, sourceId, timestampSeconds, signalFrame))
                _lastSurvivalDeathSignalSequence = sequence;
        }

        private bool TryRecordSurvivalDeathTelemetry(
            in SurvivalVitalsChangedSignal signal,
            uint sourceId,
            uint timestampSeconds,
            uint signalFrame)
        {
            if (sourceId == 0u || signal.SourceId != sourceId)
                return false;

            if ((signal.Flags & SurvivalVitalsChangedSignalFlags.Death) == 0u)
                return false;

            if (signalFrame != 0u && signalFrame == _lastSurvivalDeathFrame)
                return true;

            if (!TryRecordEvent(AnalyticsEventHashes.Death, timestampSeconds, _lastKnownPlayerAup))
                return false;

            _lastSurvivalDeathFrame = signalFrame;
            return true;
        }

        private static uint ResolveSurvivalDeathSignalFrame(in SurvivalVitalsChangedSignal signal, uint fallbackFrameId)
        {
            return signal.Frame != 0u ? signal.Frame : fallbackFrameId;
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerContext)
        {
            _playerRuntimeContext = playerContext != null && playerContext.IsInitialized ? playerContext : null;
            _survivalSystem = _playerRuntimeContext != null ? _playerRuntimeContext.SurvivalSystem : null;
        }

        private void ClearPlayerRuntimeContext()
        {
            _playerRuntimeContext = null;
            _survivalSystem = null;
            _survivalDeathSignalSourceId = 0u;
            _lastSurvivalDeathSignalSequence = 0;
        }

        private void RefreshSurvivalSignalBinding()
        {
            uint sourceId = ResolveSurvivalSignalSourceId(_survivalSystem);
            if (_survivalDeathSignalSourceId == sourceId)
                return;

            _survivalDeathSignalSourceId = sourceId;
            _lastSurvivalDeathSignalSequence = sourceId != 0u &&
                                               SurvivalSignalRoute.TryGetLatestDeathForSource(sourceId, out _, out int sequence)
                ? sequence
                : 0;
        }

        private static uint ResolveSurvivalSignalSourceId(HectonSurvivalSystem system)
        {
            return system != null
                ? RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(system.GetEntityId()))
                : 0u;
        }

        private void IngestFrameTimeSignals(uint timestampSeconds, uint frameId)
        {
            if (!_hasLastKnownPlayerAup)
                return;

            ReadOnlySpan<FrameTimeSignal> signals = SignalBus<FrameTimeSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                FrameTimeSignal signal = signals[i];
                uint signalFrame = signal.Frame != 0u ? signal.Frame : frameId;
                if (signalFrame == _lastFrameTimeSignalFrame ||
                    !math.isfinite(signal.CurrentFrameTimeMs) ||
                    !math.isfinite(signal.TargetFrameTimeMs) ||
                    !math.isfinite(signal.JitterSigmaMs))
                {
                    continue;
                }

                float target = math.max(1f, signal.TargetFrameTimeMs);
                float spikeThreshold = math.max(target * 1.5f, target + math.max(0f, signal.JitterSigmaMs) * 2f);
                if (signal.CurrentFrameTimeMs < spikeThreshold)
                    continue;

                _lastFrameTimeSignalFrame = signalFrame;
                TryRecordEvent(AnalyticsEventHashes.PerfSpike, timestampSeconds, _lastKnownPlayerAup);
            }
        }

        private void GenerateMockAnalytics(float deltaTime, uint timestampSeconds, uint frameId)
        {
            _mockTimerSeconds += math.max(0f, deltaTime);
            if (_mockTimerSeconds < 1f)
                return;

            _mockTimerSeconds = 0f;
            if (!TryOpenIngressBuffersForOwner(
                    out NativeArray<AnalyticEventDTO> routineIngress,
                    out NativeArray<AnalyticEventDTO> _,
                    out NativeArray<AnalyticsIngressCursorDTO> ingressCursor))
                return;

            AnalyticsIngressCursorDTO beforeCursor = ingressCursor[0];
            AnalyticsTuningDTO tuning = _cachedTuning;
            float quality = math.saturate(math.isfinite(tuning.GlobalQualityWeight) ? tuning.GlobalQualityWeight : HomeostasisBrain.GlobalQualityWeight);
            float smoothQuality = quality * quality * (3f - 2f * quality);
            int low = math.max(1, tuning.LowCullThreshold);
            int ultra = math.max(low, tuning.UltraCullThreshold);
            int pressureLimit = math.max(1, (int)math.round(math.lerp((float)low, (float)ultra, smoothQuality)));
            int baselineCount = math.max(1, (int)math.round(math.lerp(20f, 500f, smoothQuality)));
            float pressure = ResolveBacklogPressureEvents() / math.max(1f, (float)pressureLimit);
            float pressureGate = math.step(1f, pressure);
            float overload01 = math.saturate((pressure - 1f) / math.max(pressure, 0.0001f));
            int eventCount = math.max(1, (int)math.round(math.lerp((float)baselineCount, math.max(1f, baselineCount * 0.25f), overload01 * pressureGate)));
            double3 mockOrigin = default;
            mockOrigin.y = -80d;

            GenerateMockAnalyticsEventsJob job = default;
            job.RoutineIngress = routineIngress;
            job.IngressCursor = ingressCursor;
            job.OriginAUP = mockOrigin;
            job.TimestampSeconds = timestampSeconds;
            job.SimulationFrame = frameId;
            job.SectorHash = HashAupSector(mockOrigin);
            job.Seed = SystemHash;
            job.EventCount = eventCount;
            job.Execute();

            AnalyticsIngressCursorDTO afterCursor = ingressCursor[0];
            int written = SaturateCursorDelta(afterCursor.RoutineWriteCursor - beforeCursor.RoutineWriteCursor);
            if (written <= 0)
                return;

            Interlocked.Add(ref _hotEnqueuedDelta, written);
            Interlocked.Add(ref _ingressPendingEstimate, written);
        }

        private void ProcessQueue(uint timestampSeconds, uint frameId)
        {
            if (!TryOpenProcessingBuffersForOwner(
                    out NativeArray<AnalyticEventDTO> eventRing,
                    out NativeArray<AnalyticEventDTO> staging,
                    out NativeArray<AnalyticsCountersDTO> counters,
                    out NativeArray<AnalyticEventDTO> routineIngress,
                    out NativeArray<AnalyticEventDTO> criticalIngress,
                    out NativeArray<AnalyticsIngressCursorDTO> ingressCursor))
            {
                return;
            }

            ApplyHotPathCounterDeltas(counters);
            uint drainedBefore = counters[0].DrainedEvents;
            using (ProcessQueueMarker.Auto())
            {
                TryOpenWorkerBufferForOwner(
                    in _heatmapDebugHandle,
                    1,
                    out NativeArray<AnalyticEventDTO> heatmapDebug);
                ProcessAnalyticsQueueJob drainJob = new ProcessAnalyticsQueueJob
                {
                    RoutineIngress = routineIngress,
                    CriticalIngress = criticalIngress,
                    IngressCursor = ingressCursor,
                    EventRing = eventRing,
                    StagingBuffer = staging,
                    HeatmapDebug = heatmapDebug,
                    Counters = counters,
                    GlobalQualityWeight = _cachedTuning.GlobalQualityWeight,
                    BackgroundBacklogEvents = ResolveBacklogPressureEvents(),
                    FrameIndex = frameId,
                    TimestampSeconds = timestampSeconds
                };
                drainJob.Execute();
            }

            AnalyticsCountersDTO counter = counters[0];
            SubtractIngressPendingEstimate(unchecked((int)(counter.DrainedEvents - drainedBefore)));
            if (counter.NonFiniteEvents != _lastNonFiniteEvents)
            {
                _lastNonFiniteEvents = counter.NonFiniteEvents;
                TryDumpBlackBox(counter, 0x4E414E00u, frameId);
            }

            int count = math.min((int)counter.StagedEventsThisFrame, staging.Length);
            if (count > 0)
                PublishBatchToWorker(staging, count, ref counter, counters);
        }

        private void PublishBatchToWorker(
            NativeArray<AnalyticEventDTO> staging,
            int count,
            ref AnalyticsCountersDTO counter,
            NativeArray<AnalyticsCountersDTO> counters)
        {
            using (HandoffMarker.Auto())
            {
                if (Interlocked.CompareExchange(ref _pendingBatchState, BatchStateWriting, BatchStateIdle) != BatchStateIdle)
                {
                    counter.HandoffMisses++;
                    counter.DroppedEvents += (uint)count;
                    counter.StateHash = AnalyticsMath.HashCounters(in counter);
                    counters[0] = counter;
                    return;
                }

                int batchIndex = _handoffWriteIndex;
                NativeArray<AnalyticEventDTO> destination = OpenHandoffBufferForOwner(batchIndex);
                if (!destination.IsCreated)
                {
                    counter.HandoffMisses++;
                    counter.DroppedEvents += (uint)count;
                    counter.StateHash = AnalyticsMath.HashCounters(in counter);
                    counters[0] = counter;
                    Interlocked.Exchange(ref _pendingBatchState, BatchStateIdle);
                    return;
                }

                int safeCount = math.min(count, destination.Length);
                for (int i = 0; i < safeCount; i++)
                    destination[i] = staging[i];

                _handoffWriteIndex = 1 - batchIndex;
                Volatile.Write(ref _pendingBatchIndex, batchIndex);
                Volatile.Write(ref _pendingBatchCount, safeCount);
                Interlocked.Exchange(ref _pendingBatchState, BatchStatePending);
                SignalWorkerNoThrow();
            }
        }

        private NativeArray<AnalyticEventDTO> OpenHandoffBufferForOwner(int batchIndex)
        {
            return batchIndex == 0
                ? CreateLockedWorkerView(in _handoffAHandle, MaxHandoffEvents)
                : CreateLockedWorkerView(in _handoffBHandle, MaxHandoffEvents);
        }

        private bool StartWorker()
        {
            if (_workerThread != null)
            {
                if (!_workerThread.IsAlive)
                {
                    _workerThread = null;
                    DisposeWorkerSignalNoThrow();
                }
                else
                {
                    return true;
                }
            }

            if (!_workerBuffersLocked)
                return false;

            try
            {
                Volatile.Write(ref _shutdownRequested, 0);
                if (_flushSignal == null)
                    AllocateColdManagedObjects();
                // COLD ALLOC: Thread[1] - isolated analytics compression and I/O worker - owner: AsynchronousTelemetryExporter
                Thread workerThread = new Thread(WorkerLoop)
                {
                    IsBackground = true,
                    Name = "H8_Analytics_IO",
                    Priority = System.Threading.ThreadPriority.BelowNormal
                };
                _workerThread = workerThread;
                workerThread.Start();
                return true;
            }
            catch (Exception)
            {
                Volatile.Write(ref _shutdownRequested, 1);
                _workerThread = null;
                Interlocked.Increment(ref _workerFaultCount);
                SetWorkerFlag(WorkerFlagFaulted);
                return false;
            }
        }

        private void ReleaseWorkerStorageAfterStartFailure()
        {
            Volatile.Write(ref _acceptingIngress, 0);
            _storageReady = false;
            ReleaseVaultHandles(_workerStorageVault ?? _dataVault);
            DisposeWorkerSignalNoThrow();
        }

        private void DisposeWorkerSignalNoThrow()
        {
            if (_flushSignal == null)
                return;

            try
            {
                _flushSignal.Dispose();
            }
            catch (Exception)
            {
            }
            finally
            {
                _flushSignal = null;
            }
        }

        private bool SignalWorkerNoThrow()
        {
            AutoResetEvent signal = _flushSignal;
            if (signal == null)
                return false;

            try
            {
                signal.Set();
                return true;
            }
            catch (Exception)
            {
                Interlocked.Increment(ref _workerFaultCount);
                SetWorkerFlag(WorkerFlagFaulted);
                return false;
            }
        }

        private bool StopWorker()
        {
            Volatile.Write(ref _shutdownRequested, 1);
            SignalWorkerNoThrow();

            Thread thread = _workerThread;
            TryJoinWorkerNoThrow(thread);

            if (thread != null && thread.IsAlive)
            {
                HttpWebRequest request = Volatile.Read(ref _activeRequest);
                if (request != null)
                    request.Abort();
                SignalWorkerNoThrow();
                TryJoinWorkerNoThrow(thread);
            }

            if (thread != null && thread.IsAlive)
            {
                Interlocked.Increment(ref _workerFaultCount);
                SetWorkerFlag(WorkerFlagFaulted);
                if (TryOpenCountersForOwner(out NativeArray<AnalyticsCountersDTO> counters))
                    TryDumpBlackBox(counters[0], 0x444C4F43u, ResolveLastFrameId());
                return false;
            }

            _workerThread = null;
            DisposeWorkerSignalNoThrow();
            UnlockWorkerVaultBuffers();

            return true;
        }

        private bool TryJoinWorkerNoThrow(Thread thread)
        {
            if (thread == null || !thread.IsAlive)
                return true;

            if (ReferenceEquals(Thread.CurrentThread, thread))
                return false;

            try
            {
                thread.Join(WorkerJoinMilliseconds);
                return !thread.IsAlive;
            }
            catch (Exception)
            {
                Interlocked.Increment(ref _workerFaultCount);
                SetWorkerFlag(WorkerFlagFaulted);
                return false;
            }
        }

        private int ReadWorkerAccumCount()
        {
            return math.max(0, Volatile.Read(ref _workerAccumCount));
        }

        private void PublishWorkerAccumCount(int count)
        {
            Volatile.Write(ref _workerAccumCount, math.clamp(count, 0, MaxHandoffEvents));
        }

        private void SetWorkerFlag(int flag)
        {
            int observed;
            int next;
            do
            {
                observed = Volatile.Read(ref _workerFlags);
                next = observed | flag;
                if (next == observed)
                    return;
            }
            while (Interlocked.CompareExchange(ref _workerFlags, next, observed) != observed);
        }

        private void ClearWorkerFlag(int flag)
        {
            int observed;
            int next;
            do
            {
                observed = Volatile.Read(ref _workerFlags);
                next = observed & ~flag;
                if (next == observed)
                    return;
            }
            while (Interlocked.CompareExchange(ref _workerFlags, next, observed) != observed);
        }

        private void WorkerLoop()
        {
            SetWorkerFlag(WorkerFlagRunning);
            if (!string.IsNullOrEmpty(_endpointUrl))
                SetWorkerFlag(WorkerFlagEndpointConfigured);
            else
                ClearWorkerFlag(WorkerFlagEndpointConfigured);
            try
            {
                while (Volatile.Read(ref _shutdownRequested) == 0 || Volatile.Read(ref _pendingBatchState) == BatchStatePending)
                {
                    AutoResetEvent signal = _flushSignal;
                    if (signal == null)
                        break;

                    signal.WaitOne(WorkerWaitMilliseconds);
                    TryWritePendingBlackBoxDump();
                    if (Volatile.Read(ref _pendingBatchState) != BatchStatePending)
                    {
                        if (ReadWorkerAccumCount() > 0 && DateTime.UtcNow.Ticks - _lastWorkerFlushTicks > TimeSpan.TicksPerSecond * 60L)
                            FlushWorkerAccumulatedBatch();
                        continue;
                    }

                    int batchIndex = Volatile.Read(ref _pendingBatchIndex);
                    int count = math.clamp(Volatile.Read(ref _pendingBatchCount), 0, MaxHandoffEvents);
                    NativeArray<AnalyticEventDTO> batch = OpenWorkerHandoffBufferForOwner(batchIndex);

                    try
                    {
                        if (batch.IsCreated)
                            AccumulateWorkerBatch(batch, count);
                        else
                            Interlocked.Increment(ref _workerFaultCount);
                    }
                    catch
                    {
                        Interlocked.Increment(ref _workerFaultCount);
                        SetWorkerFlag(WorkerFlagFaulted);
                    }
                    finally
                    {
                        Volatile.Write(ref _pendingBatchCount, 0);
                        Volatile.Write(ref _pendingBatchIndex, -1);
                        Interlocked.Exchange(ref _pendingBatchState, BatchStateIdle);
                    }
                }

                if (ReadWorkerAccumCount() > 0)
                    FlushWorkerAccumulatedBatch();
                TryWritePendingBlackBoxDump();
            }
            catch
            {
                Interlocked.Increment(ref _workerFaultCount);
                SetWorkerFlag(WorkerFlagFaulted);
            }
            finally
            {
                ClearWorkerFlag(WorkerFlagRunning);
            }
        }

        private void AccumulateWorkerBatch(NativeArray<AnalyticEventDTO> events, int count)
        {
            if (count <= 0)
                return;

            NativeArray<AnalyticEventDTO> accumulation = CreateLockedWorkerView(in _workerAccumHandle, MaxHandoffEvents);
            if (!accumulation.IsCreated)
                return;

            bool forceFlush = false;
            int accumCount = math.clamp(Volatile.Read(ref _workerAccumCount), 0, accumulation.Length);
            if (accumCount == 0)
                _lastWorkerFlushTicks = DateTime.UtcNow.Ticks;

            for (int i = 0; i < count; i++)
            {
                if (accumCount >= accumulation.Length)
                {
                    PublishWorkerAccumCount(accumCount);
                    FlushWorkerAccumulatedBatch();
                    accumulation = CreateLockedWorkerView(in _workerAccumHandle, MaxHandoffEvents);
                    if (!accumulation.IsCreated)
                        return;
                    accumCount = math.clamp(Volatile.Read(ref _workerAccumCount), 0, accumulation.Length);
                }

                accumulation[accumCount] = events[i];
                accumCount++;
                forceFlush |= (events[i].EventHashID & AnalyticsEventHashes.CriticalMask) != 0u;
            }

            PublishWorkerAccumCount(accumCount);
            int thresholdBytes = math.clamp(Volatile.Read(ref _workerBatchFlushThresholdBytes), 1024, MaxRawBatchBytes);
            if (forceFlush || accumCount * RawEventBytes >= thresholdBytes)
                FlushWorkerAccumulatedBatch();
        }

        private void FlushWorkerAccumulatedBatch()
        {
            int count = math.clamp(Volatile.Read(ref _workerAccumCount), 0, MaxHandoffEvents);
            if (count <= 0)
                return;

            NativeArray<AnalyticEventDTO> accumulation = CreateLockedWorkerView(in _workerAccumHandle, MaxHandoffEvents);
            NativeArray<byte> rawBytesBuffer = CreateLockedWorkerView(in _rawBatchScratchHandle, MaxRawBatchBytes);
            NativeArray<byte> compressedBytesBuffer = CreateLockedWorkerView(in _compressedScratchHandle, DefaultCompressedScratchBytes);
            if (!accumulation.IsCreated || !rawBytesBuffer.IsCreated || !compressedBytesBuffer.IsCreated)
            {
                Interlocked.Increment(ref _workerFaultCount);
                return;
            }

            PublishWorkerAccumCount(0);
            _lastWorkerFlushTicks = DateTime.UtcNow.Ticks;
            int rawBytes = SerializeEvents(accumulation, count, rawBytesBuffer);
            int compressedBytes = AnalyticsCompression.CompressRleEnvelope(
                AsReadOnlySpan(rawBytesBuffer, rawBytes),
                AsSpan(compressedBytesBuffer));
            if (compressedBytes <= 0)
            {
                compressedBytes = rawBytes;
                CopyBytes(rawBytesBuffer, compressedBytesBuffer, rawBytes);
            }

            bool sent = false;
            if ((Volatile.Read(ref _workerTuningFlags) & AnalyticsExporterFlags.NetworkEnabled) != 0 && !string.IsNullOrEmpty(_endpointUrl))
                sent = TrySendCompressedBatch(compressedBytesBuffer, compressedBytes);

            if (sent)
            {
                Interlocked.Add(ref _workerSentEvents, count);
                TryFlushDiskBacklog();
            }
            else
            {
                try
                {
                    WriteDiskFallback(compressedBytesBuffer, compressedBytes);
                    Interlocked.Add(ref _workerDiskFallbackEvents, count);
                    SetWorkerFlag(WorkerFlagDiskFallback);
                }
                catch
                {
                    Interlocked.Increment(ref _workerFaultCount);
                    SetWorkerFlag(WorkerFlagFaulted);
                }
            }

            Interlocked.Add(ref _workerRawBytesSent, rawBytes);
            Interlocked.Add(ref _workerCompressedBytesSent, compressedBytes);
            Interlocked.Increment(ref _workerHeartbeat);
        }

        private int SerializeEvents(NativeArray<AnalyticEventDTO> events, int count, NativeArray<byte> destination)
        {
            Span<byte> span = AsSpan(destination);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(0, 4), AnalyticsCompression.RawPayloadMagic);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), 1u);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(8, 4), (uint)count);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(12, 4), RawEventBytes);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(16, 4), unchecked((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(20, 4), 0u);

            int offset = AnalyticsCompression.RawHeaderBytes;
            for (int i = 0; i < count; i++)
            {
                AnalyticEventDTO dto = events[i];
                BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset, 4), dto.EventHashID);
                BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset + 4, 4), dto.TimestampSeconds);
                BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(offset + 8, 8), unchecked((ulong)BitConverter.DoubleToInt64Bits(dto.EventAUP.x)));
                BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(offset + 16, 8), unchecked((ulong)BitConverter.DoubleToInt64Bits(dto.EventAUP.y)));
                BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(offset + 24, 8), unchecked((ulong)BitConverter.DoubleToInt64Bits(dto.EventAUP.z)));
                offset += RawEventBytes;
            }

            return offset;
        }

        private bool TrySendCompressedBatch(NativeArray<byte> payload, int byteCount)
        {
            HttpWebRequest request = null;
            try
            {
                string endpoint = _endpointUrl;
                if (!IsHttpEndpoint(endpoint))
                {
                    Volatile.Write(ref _workerLastResponseCode, -3);
                    Interlocked.Increment(ref _workerFaultCount);
                    return false;
                }

                request = (HttpWebRequest)WebRequest.Create(endpoint);
                request.Method = "POST";
                request.ContentType = "application/octet-stream";
                int timeoutMs = math.clamp(Volatile.Read(ref _workerNetworkTimeoutMs), 250, 30000);
                request.Timeout = timeoutMs;
                request.ReadWriteTimeout = timeoutMs;
                request.ContentLength = byteCount;
                if (!string.IsNullOrEmpty(_apiKey))
                    request.Headers["X-H8-Analytics-Key"] = _apiKey;

                Volatile.Write(ref _activeRequest, request);
                using (Stream requestStream = request.GetRequestStream())
                    requestStream.Write(AsReadOnlySpan(payload, byteCount));

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    int code = (int)response.StatusCode;
                    Volatile.Write(ref _workerLastResponseCode, code);
                    return code >= 200 && code <= 299;
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse response = ex.Response as HttpWebResponse;
                if (response != null)
                {
                    try
                    {
                        Volatile.Write(ref _workerLastResponseCode, (int)response.StatusCode);
                    }
                    finally
                    {
                        response.Dispose();
                    }
                }
                else
                {
                    Volatile.Write(ref _workerLastResponseCode, -1);
                }

                Interlocked.Increment(ref _workerFaultCount);
                return false;
            }
            catch
            {
                Volatile.Write(ref _workerLastResponseCode, -2);
                Interlocked.Increment(ref _workerFaultCount);
                return false;
            }
            finally
            {
                if (ReferenceEquals(Volatile.Read(ref _activeRequest), request))
                    Volatile.Write(ref _activeRequest, null);
            }
        }

        private static bool IsHttpEndpoint(string endpoint)
        {
            return !string.IsNullOrEmpty(endpoint) &&
                   (endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase));
        }

        private void WriteDiskFallback(NativeArray<byte> payload, int byteCount)
        {
            Directory.CreateDirectory(_fallbackDirectory);
            long sequence = Interlocked.Increment(ref _fallbackFileSequence);
            string finalPath = Path.Combine(
                _fallbackDirectory,
                "analytics_" + DateTime.UtcNow.Ticks.ToString("X16") + "_" + sequence.ToString("X8") + ".h8log");
            string tmpPath = finalPath + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(tmpPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(AsReadOnlySpan(payload, byteCount));
                    stream.Flush(true);
                    if (stream.Length != byteCount)
                        throw new IOException("Analytics disk fallback temp length mismatch.");
                }

                File.Move(tmpPath, finalPath);
                if (!TryGetFallbackFileLength(finalPath, out long finalBytes) || finalBytes != byteCount)
                {
                    TryDeleteReplayFile(finalPath);
                    throw new IOException("Analytics disk fallback final length mismatch.");
                }
            }
            catch
            {
                TryDeleteTempFallbackFile(tmpPath);
                throw;
            }
        }

        private static bool TryGetFallbackFileLength(string path, out long bytes)
        {
            bytes = 0L;
            try
            {
                if (string.IsNullOrEmpty(path))
                    return false;

                bytes = new FileInfo(path).Length;
                return bytes >= 0L;
            }
            catch
            {
                bytes = 0L;
                return false;
            }
        }

        private void TryFlushDiskBacklog()
        {
            try
            {
                TryFlushDiskBacklogUnchecked();
            }
            catch
            {
                Interlocked.Increment(ref _workerFaultCount);
                SetWorkerFlag(WorkerFlagFaulted);
            }
        }

        private void TryFlushDiskBacklogUnchecked()
        {
            if (string.IsNullOrEmpty(_endpointUrl) || !Directory.Exists(_fallbackDirectory))
                return;

            NativeArray<byte> compressedBytesBuffer = CreateLockedWorkerView(in _compressedScratchHandle, DefaultCompressedScratchBytes);
            if (!compressedBytesBuffer.IsCreated)
                return;

            using (System.Collections.Generic.IEnumerator<string> files =
                   Directory.EnumerateFiles(_fallbackDirectory, "analytics_*.h8log", SearchOption.TopDirectoryOnly).GetEnumerator())
            {
                int processed = 0;
                while (processed < MaxBacklogReplayFilesPerFlush && files.MoveNext())
                {
                    processed++;
                    string path = files.Current;
                    bool deleteAfterRead = false;
                    using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096))
                    {
                        int length = (int)math.min((long)compressedBytesBuffer.Length, stream.Length);
                        if (length <= 0)
                        {
                            deleteAfterRead = true;
                        }
                        else
                        {
                            int read = stream.Read(AsSpan(compressedBytesBuffer, length));
                            if (read != length)
                            {
                                Interlocked.Increment(ref _workerFaultCount);
                                SetWorkerFlag(WorkerFlagFaulted);
                                deleteAfterRead = true;
                            }

                            if (!deleteAfterRead && !IsValidTelemetryPayload(compressedBytesBuffer, read))
                            {
                                deleteAfterRead = true;
                            }
                            else if (!deleteAfterRead)
                            {
                                if (!TrySendCompressedBatch(compressedBytesBuffer, read))
                                    return;
                                deleteAfterRead = true;
                            }
                        }
                    }

                    if (deleteAfterRead && !TryDeleteReplayFile(path))
                        return;
                }
            }
        }

        private bool TryDeleteReplayFile(string path)
        {
            try
            {
                File.Delete(path);
                return true;
            }
            catch
            {
                Interlocked.Increment(ref _workerFaultCount);
                SetWorkerFlag(WorkerFlagFaulted);
                return false;
            }
        }

        private void TryDeleteTempFallbackFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                Interlocked.Increment(ref _workerFaultCount);
                SetWorkerFlag(WorkerFlagFaulted);
            }
        }

        private static bool IsValidTelemetryPayload(NativeArray<byte> payload, int byteCount)
        {
            if (!payload.IsCreated || byteCount < AnalyticsCompression.RleEnvelopeHeaderBytes)
                return false;

            ReadOnlySpan<byte> bytes = AsReadOnlySpan(payload, byteCount);
            uint magic = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(0, 4));
            if (magic == AnalyticsCompression.RleEnvelopeMagic)
            {
                if (byteCount < AnalyticsCompression.RleEnvelopeHeaderBytes)
                    return false;

                uint rawBytes = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
                uint compressedBytes = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4));
                return rawBytes > 0u &&
                       compressedBytes > 0u &&
                       compressedBytes <= int.MaxValue &&
                       AnalyticsCompression.RleEnvelopeHeaderBytes + (int)compressedBytes == byteCount;
            }

            if (magic != AnalyticsCompression.RawPayloadMagic)
                return false;
            if (byteCount < AnalyticsCompression.RawHeaderBytes)
                return false;

            uint count = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4));
            uint stride = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(12, 4));
            if (stride != RawEventBytes || count > MaxHandoffEvents)
                return false;

            return AnalyticsCompression.RawHeaderBytes + (int)count * RawEventBytes == byteCount;
        }

        private static void CopyBytes(NativeArray<byte> source, NativeArray<byte> destination, int byteCount)
        {
            int count = math.min(math.min(source.Length, destination.Length), math.max(0, byteCount));
            for (int i = 0; i < count; i++)
                destination[i] = source[i];
        }

        private NativeArray<AnalyticEventDTO> OpenWorkerHandoffBufferForOwner(int batchIndex)
        {
            return batchIndex == 0
                ? CreateLockedWorkerView(in _handoffAHandle, MaxHandoffEvents)
                : CreateLockedWorkerView(in _handoffBHandle, MaxHandoffEvents);
        }

        private NativeArray<T> CreateLockedWorkerView<T>(in VaultGenerationHandle<T> handle, int requiredLength) where T : struct
        {
            TryOpenWorkerBufferForOwner(in handle, requiredLength, out NativeArray<T> view);
            return view;
        }

        private static Span<byte> AsSpan(NativeArray<byte> buffer)
        {
            unsafe
            {
                return new Span<byte>(NativeArrayUnsafeUtility.GetUnsafePtr(buffer), buffer.Length);
            }
        }

        private static Span<byte> AsSpan(NativeArray<byte> buffer, int byteCount)
        {
            int safeCount = math.clamp(byteCount, 0, buffer.Length);
            unsafe
            {
                return new Span<byte>(NativeArrayUnsafeUtility.GetUnsafePtr(buffer), safeCount);
            }
        }

        private static ReadOnlySpan<byte> AsReadOnlySpan(NativeArray<byte> buffer, int byteCount)
        {
            int safeCount = math.clamp(byteCount, 0, buffer.Length);
            unsafe
            {
                return new ReadOnlySpan<byte>(NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(buffer), safeCount);
            }
        }

        private static ReadOnlySpan<byte> AsReadOnlySpan(NativeArray<byte>.ReadOnly buffer, int byteCount)
        {
            int safeCount = math.clamp(byteCount, 0, buffer.Length);
            unsafe
            {
                return new ReadOnlySpan<byte>(buffer.GetUnsafeReadOnlyPtr(), safeCount);
            }
        }

        private void WriteExporterTelemetry(uint timestampSeconds, uint frameId)
        {
            if (!TryOpenTelemetryForOwner(out NativeArray<AnalyticsExporterTelemetryEntry> telemetry, out NativeArray<int> cursor) ||
                !TryOpenCountersForOwner(out NativeArray<AnalyticsCountersDTO> counters))
            {
                return;
            }

            AnalyticsCountersDTO counter = counters[0];
            uint rawBytes = unchecked((uint)math.min(int.MaxValue, Interlocked.Read(ref _workerRawBytesSent)));
            uint compressedBytes = unchecked((uint)math.min(int.MaxValue, Interlocked.Read(ref _workerCompressedBytesSent)));
            uint backlogEstimate = unchecked((uint)math.max(0, ResolveBacklogPressureEvents()));
            AnalyticsExporterTelemetryEntry entry = default;
            entry.Frame = frameId;
            entry.TimestampSeconds = timestampSeconds;
            entry.SentEvents = unchecked((uint)math.min(int.MaxValue, Interlocked.Read(ref _workerSentEvents)));
            entry.DiskFallbackEvents = unchecked((uint)math.min(int.MaxValue, Interlocked.Read(ref _workerDiskFallbackEvents)));
            entry.DroppedEvents = counter.DroppedEvents;
            entry.BacklogEvents = backlogEstimate;
            entry.RawBytes = rawBytes;
            entry.CompressedBytes = compressedBytes;
            entry.LastResponseCode = Volatile.Read(ref _workerLastResponseCode);
            entry.CompressionRatioMilli = rawBytes > 0u ? (uint)math.min(100000u, (compressedBytes * 1000u) / rawBytes) : 0u;
            entry.Flags = unchecked((uint)Volatile.Read(ref _workerFlags));
            entry.WorkerHeartbeat = unchecked((uint)Volatile.Read(ref _workerHeartbeat));
            entry.FaultCount = unchecked((uint)Volatile.Read(ref _workerFaultCount));
            entry.QueueDepthEstimate = backlogEstimate;
            entry.VaultBytes = ResolveVaultTelemetryBytes();
            entry.StateHash = AnalyticsMath.HashTelemetry(in entry);

            int write = cursor[0];
            telemetry[write % telemetry.Length] = entry;
            cursor[0] = (write + 1) % telemetry.Length;

            counter.WorkerBacklogEvents = entry.BacklogEvents;
            counter.Flags = entry.Flags;
            counter.EndpointHash = _cachedTuning.EndpointHash;
            counter.ApiKeyHash = _cachedTuning.ApiKeyHash;
            counter.StateHash = AnalyticsMath.HashCounters(in counter);
            counters[0] = counter;

            if (entry.FaultCount != _lastWorkerFaultCount)
            {
                _lastWorkerFaultCount = entry.FaultCount;
                TryDumpBlackBox(counter, 0x4641554Cu, frameId);
            }
        }

        private void TryDumpBlackBox(AnalyticsCountersDTO counter, uint reason, uint frameId)
        {
            uint frame = frameId != 0u ? frameId : ResolveLastFrameId();
            if (_lastDumpFrame != 0u && frame - _lastDumpFrame < 60u)
                return;

            _lastDumpFrame = frame;
            DumpBlackBox(counter, reason);
        }

        private void DumpBlackBox(AnalyticsCountersDTO counter, uint reason)
        {
            if (!TryReadTelemetryBuffers(out NativeArray<AnalyticsExporterTelemetryEntry>.ReadOnly telemetry, out NativeArray<int>.ReadOnly cursor) ||
                Interlocked.CompareExchange(ref _pendingDumpState, DumpStateWriting, DumpStateIdle) != DumpStateIdle)
            {
                return;
            }

            try
            {
                if (!TryOpenWorkerBufferForOwner(in _dumpSnapshotHandle, DumpSnapshotBytes, out NativeArray<byte> snapshot))
                {
                    Interlocked.Exchange(ref _pendingDumpState, DumpStateIdle);
                    return;
                }

                Span<byte> bytes = AsSpan(snapshot, DumpSnapshotBytes);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(0, 4), 0x41313630u);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(4, 4), reason);
                int count = math.min(telemetry.Length, DefaultTelemetryCapacity);
                int normalizedCursor = NormalizeTelemetryRingIndex(cursor[0], telemetry.Length);
                AnalyticsExporterTelemetryEntry normalizedEntry = telemetry[normalizedCursor];
                bool ringHasWrapped = count > 0 && IsTelemetryEntryWritten(in normalizedEntry);
                int startIndex = ResolveTelemetryDumpStartIndex(cursor[0], telemetry.Length, ringHasWrapped);

                BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(8, 4), (uint)count);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(12, 4), (uint)UnsafeUtility.SizeOf<AnalyticsExporterTelemetryEntry>());
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(16, 4), (uint)cursor[0]);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(20, 4), counter.StateHash);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(24, 4), counter.NonFiniteEvents);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(28, 4), counter.DroppedEvents);

                int offset = DumpHeaderBytes;
                for (int i = 0; i < count; i++)
                {
                    int sourceIndex = ResolveTelemetryDumpSourceIndex(startIndex, telemetry.Length, i);
                    AnalyticsExporterTelemetryEntry entry = telemetry[sourceIndex];
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offset, 4), entry.Frame);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offset + 4, 4), entry.TimestampSeconds);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offset + 8, 4), entry.SentEvents);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offset + 12, 4), entry.DiskFallbackEvents);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offset + 16, 4), entry.DroppedEvents);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offset + 20, 4), entry.BacklogEvents);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offset + 24, 4), entry.RawBytes);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offset + 28, 4), entry.CompressedBytes);
                    BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(offset + 32, 4), entry.LastResponseCode);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offset + 36, 4), entry.CompressionRatioMilli);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offset + 40, 4), entry.Flags);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offset + 44, 4), entry.WorkerHeartbeat);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offset + 48, 4), entry.FaultCount);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offset + 52, 4), entry.QueueDepthEstimate);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offset + 56, 4), entry.StateHash);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(offset + 60, 4), entry.VaultBytes);
                    offset += 64;
                }

                Volatile.Write(ref _pendingDumpBytes, offset);
                Interlocked.Exchange(ref _pendingDumpState, DumpStatePending);
                SignalWorkerNoThrow();
            }
            catch
            {
                Interlocked.Increment(ref _workerFaultCount);
                SetWorkerFlag(WorkerFlagFaulted);
                Interlocked.Exchange(ref _pendingDumpState, DumpStateIdle);
            }
        }

        private static int ResolveTelemetryDumpStartIndex(int cursorValue, int ringLength, bool ringHasWrapped)
        {
            if (ringLength <= 0)
                return 0;

            return ringHasWrapped ? NormalizeTelemetryRingIndex(cursorValue, ringLength) : 0;
        }

        private static bool IsTelemetryEntryWritten(in AnalyticsExporterTelemetryEntry entry)
        {
            return entry.StateHash != 0u ||
                   entry.Frame != 0u ||
                   entry.TimestampSeconds != 0u ||
                   entry.Flags != 0u ||
                   entry.VaultBytes != 0u;
        }

        private static int ResolveTelemetryDumpSourceIndex(int startIndex, int ringLength, int offset)
        {
            if (ringLength <= 0)
                return 0;

            int index = NormalizeTelemetryRingIndex(startIndex, ringLength) + math.max(0, offset);
            return index % ringLength;
        }

        private static int NormalizeTelemetryRingIndex(int cursorValue, int ringLength)
        {
            if (ringLength <= 0)
                return 0;

            int index = cursorValue % ringLength;
            return index < 0 ? index + ringLength : index;
        }

        private void TryWritePendingBlackBoxDump()
        {
            if (Interlocked.CompareExchange(ref _pendingDumpState, DumpStateWriting, DumpStatePending) != DumpStatePending)
                return;

            try
            {
                int byteCount = math.clamp(Volatile.Read(ref _pendingDumpBytes), 0, DumpSnapshotBytes);
                if (byteCount <= 0)
                {
                    Interlocked.Increment(ref _workerFaultCount);
                    SetWorkerFlag(WorkerFlagFaulted);
                    return;
                }

                if (!TryReadWorkerBuffer(in _dumpSnapshotHandle, DumpSnapshotBytes, out NativeArray<byte>.ReadOnly snapshot))
                {
                    Interlocked.Increment(ref _workerFaultCount);
                    SetWorkerFlag(WorkerFlagFaulted);
                    return;
                }

                ReadOnlySpan<byte> payload = AsReadOnlySpan(snapshot, byteCount);
                bool wroteTimestampedDump = Hecton8.Core.NativeFaultDumpWriter.TryWriteAll(BuildAnalyticsCrashDumpPath(DateTime.UtcNow.Ticks), payload, byteCount);
                bool wroteLatestDump = Hecton8.Core.NativeFaultDumpWriter.TryWriteAll("Docs/AgentLogs/Dump_ANALYTICS_CRASH.bin", payload, byteCount);
                if (!wroteTimestampedDump && !wroteLatestDump)
                {
                    Interlocked.Increment(ref _workerFaultCount);
                    SetWorkerFlag(WorkerFlagFaulted);
                }
            }
            catch
            {
                Interlocked.Increment(ref _workerFaultCount);
                SetWorkerFlag(WorkerFlagFaulted);
            }
            finally
            {
                Volatile.Write(ref _pendingDumpBytes, 0);
                Interlocked.Exchange(ref _pendingDumpState, DumpStateIdle);
            }
        }

        private static string BuildAnalyticsCrashDumpPath(long utcTicks)
        {
            return "Docs/AgentLogs/Dump_ANALYTICS_CRASH_" + utcTicks.ToString("X16") + ".bin";
        }

        private string ResolveFallbackDirectory()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, "Docs", "AgentLogs");
        }

        private void LoadEndpointConfigurationCold()
        {
#if !UNITY_EDITOR
            return;
#else
            if (!TryOpenCsvScratchForOwner(out NativeArray<byte> scratch))
                return;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string path = Path.Combine(projectRoot, _endpointCsvRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
                path = Path.Combine(projectRoot, "Assets", "_Project", "Data", "Analytics", "analytics_endpoint.csv");
            if (!File.Exists(path))
                return;

            int bytesRead;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096))
            {
                int readLimit = (int)math.min((long)scratch.Length, stream.Length);
                bytesRead = stream.Read(AsSpan(scratch, readLimit));
            }

            ParseEndpointCsv(AsReadOnlySpan(scratch, math.min(bytesRead, scratch.Length)));
#endif
        }

#if UNITY_EDITOR
        private void ParseEndpointCsv(ReadOnlySpan<byte> csv)
        {
            int start = 0;
            for (int i = 0; i <= csv.Length; i++)
            {
                if (i < csv.Length && csv[i] != (byte)'\n')
                    continue;

                ReadOnlySpan<byte> line = TrimAscii(csv.Slice(start, i - start));
                start = i + 1;
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                int comma = IndexOf(line, (byte)',');
                if (comma <= 0)
                    continue;

                ReadOnlySpan<byte> key = TrimAscii(line.Slice(0, comma));
                ReadOnlySpan<byte> value = TrimAscii(line.Slice(comma + 1));
                ApplyEndpointConfig(key, value);
            }

            if (TryOpenTuningForOwner(out NativeArray<AnalyticsTuningDTO> tuning))
                tuning[0] = _cachedTuning;
            ApplyWorkerTuningSnapshot(in _cachedTuning);
        }
#endif

#if UNITY_EDITOR
        private void ApplyEndpointConfig(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
        {
            if (EqualsAscii(key, "endpoint"))
            {
                _cachedTuning.EndpointHash = AnalyticsMath.HashBytes(value);
                _endpointUrl = Encoding.UTF8.GetString(value);
                return;
            }

            if (EqualsAscii(key, "api_key"))
            {
                _cachedTuning.ApiKeyHash = AnalyticsMath.HashBytes(value);
                _apiKey = Encoding.UTF8.GetString(value);
                return;
            }

            if (EqualsAscii(key, "timeout_ms") && TryParsePositiveInt(value, out int timeout))
                _cachedTuning.NetworkTimeoutMs = timeout;
            else if (EqualsAscii(key, "batch_bytes") && TryParsePositiveInt(value, out int bytes))
                _cachedTuning.BatchFlushThresholdBytes = bytes;
            else if (EqualsAscii(key, "heatmap_seconds") && TryParsePositiveFloat(value, out float seconds))
                _cachedTuning.HeatmapSampleSeconds = seconds;
        }
#endif

        private bool TryOpenProcessingBuffersForOwner(
            out NativeArray<AnalyticEventDTO> eventRing,
            out NativeArray<AnalyticEventDTO> staging,
            out NativeArray<AnalyticsCountersDTO> counters,
            out NativeArray<AnalyticEventDTO> routineIngress,
            out NativeArray<AnalyticEventDTO> criticalIngress,
            out NativeArray<AnalyticsIngressCursorDTO> ingressCursor)
        {
            eventRing = default;
            staging = default;
            counters = default;
            routineIngress = default;
            criticalIngress = default;
            ingressCursor = default;
            TryOpenWorkerBufferForOwner(in _eventRingHandle, 1, out eventRing);
            TryOpenWorkerBufferForOwner(in _stagingHandle, 1, out staging);
            TryOpenWorkerBufferForOwner(in _countersHandle, 1, out counters);
            TryOpenWorkerBufferForOwner(in _routineIngressHandle, 1, out routineIngress);
            TryOpenWorkerBufferForOwner(in _criticalIngressHandle, 1, out criticalIngress);
            TryOpenWorkerBufferForOwner(in _ingressCursorHandle, 1, out ingressCursor);
            return eventRing.IsCreated &&
                   staging.IsCreated &&
                   counters.IsCreated &&
                   routineIngress.IsCreated &&
                   criticalIngress.IsCreated &&
                   ingressCursor.IsCreated &&
                   ingressCursor.Length > 0;
        }

        private bool TryOpenIngressBuffersForOwner(
            out NativeArray<AnalyticEventDTO> routineIngress,
            out NativeArray<AnalyticEventDTO> criticalIngress,
            out NativeArray<AnalyticsIngressCursorDTO> ingressCursor)
        {
            routineIngress = default;
            criticalIngress = default;
            ingressCursor = default;
            TryOpenWorkerBufferForOwner(in _routineIngressHandle, 1, out routineIngress);
            TryOpenWorkerBufferForOwner(in _criticalIngressHandle, 1, out criticalIngress);
            TryOpenWorkerBufferForOwner(in _ingressCursorHandle, 1, out ingressCursor);
            return routineIngress.IsCreated &&
                   criticalIngress.IsCreated &&
                   ingressCursor.IsCreated &&
                   ingressCursor.Length > 0;
        }

        private bool TryOpenCountersForOwner(out NativeArray<AnalyticsCountersDTO> counters)
        {
            return TryOpenWorkerBufferForOwner(in _countersHandle, 1, out counters);
        }

        private bool TryReadCountersBuffer(out NativeArray<AnalyticsCountersDTO>.ReadOnly counters)
        {
            return TryReadWorkerBuffer(in _countersHandle, 1, out counters);
        }

        private bool TryOpenTuningForOwner(out NativeArray<AnalyticsTuningDTO> tuning)
        {
            return TryOpenWorkerBufferForOwner(in _tuningHandle, 1, out tuning);
        }

        private bool TryReadTuningBuffer(out NativeArray<AnalyticsTuningDTO>.ReadOnly tuning)
        {
            return TryReadWorkerBuffer(in _tuningHandle, 1, out tuning);
        }

        private bool TryOpenTelemetryForOwner(out NativeArray<AnalyticsExporterTelemetryEntry> telemetry, out NativeArray<int> cursor)
        {
            telemetry = default;
            cursor = default;
            return TryOpenWorkerBufferForOwner(in _telemetryHandle, DefaultTelemetryCapacity, out telemetry) &&
                   TryOpenWorkerBufferForOwner(in _telemetryCursorHandle, 1, out cursor);
        }

        private bool TryReadTelemetryBuffers(out NativeArray<AnalyticsExporterTelemetryEntry>.ReadOnly telemetry, out NativeArray<int>.ReadOnly cursor)
        {
            telemetry = default;
            cursor = default;
            return TryReadWorkerBuffer(in _telemetryHandle, DefaultTelemetryCapacity, out telemetry) &&
                   TryReadWorkerBuffer(in _telemetryCursorHandle, 1, out cursor);
        }

        private bool TryOpenCsvScratchForOwner(out NativeArray<byte> scratch)
        {
            return TryOpenWorkerBufferForOwner(in _csvScratchHandle, DefaultCsvScratchBytes, out scratch);
        }

        private uint ResolveVaultTelemetryBytes()
        {
            long bytes = 0L;
            bytes += ResolveVaultBytes(in _eventRingHandle, 1);
            bytes += ResolveVaultBytes(in _stagingHandle, 1);
            bytes += ResolveVaultBytes(in _routineIngressHandle, 1);
            bytes += ResolveVaultBytes(in _criticalIngressHandle, 1);
            bytes += ResolveVaultBytes(in _ingressCursorHandle, 1);
            bytes += ResolveVaultBytes(in _countersHandle, 1);
            bytes += ResolveVaultBytes(in _telemetryHandle, DefaultTelemetryCapacity);
            bytes += ResolveVaultBytes(in _telemetryCursorHandle, 1);
            bytes += ResolveVaultBytes(in _tuningHandle, 1);
            bytes += ResolveVaultBytes(in _csvScratchHandle, DefaultCsvScratchBytes);
            bytes += ResolveVaultBytes(in _compressedScratchHandle, DefaultCompressedScratchBytes);
            bytes += ResolveVaultBytes(in _heatmapDebugHandle, 1);
            bytes += ResolveVaultBytes(in _handoffAHandle, MaxHandoffEvents);
            bytes += ResolveVaultBytes(in _handoffBHandle, MaxHandoffEvents);
            bytes += ResolveVaultBytes(in _workerAccumHandle, MaxHandoffEvents);
            bytes += ResolveVaultBytes(in _rawBatchScratchHandle, MaxRawBatchBytes);
            bytes += ResolveVaultBytes(in _dumpSnapshotHandle, DumpSnapshotBytes);
            if (bytes <= 0L)
                return 0u;
            return bytes >= uint.MaxValue ? uint.MaxValue : (uint)bytes;
        }

        private long ResolveVaultBytes<T>(in VaultGenerationHandle<T> handle, int requiredLength) where T : struct
        {
            return TryReadWorkerBuffer(in handle, requiredLength, out NativeArray<T>.ReadOnly buffer)
                ? (long)buffer.Length * UnsafeUtility.SizeOf<T>()
                : 0L;
        }

#if UNITY_EDITOR
        private static int IndexOf(ReadOnlySpan<byte> bytes, byte target)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == target)
                    return i;
            }
            return -1;
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && value[start] <= 32)
                start++;
            while (end >= start && value[end] <= 32)
                end--;
            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool EqualsAscii(ReadOnlySpan<byte> bytes, string text)
        {
            if (bytes.Length != text.Length)
                return false;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte c = bytes[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                if (c != (byte)text[i])
                    return false;
            }
            return true;
        }

        private static bool TryParsePositiveInt(ReadOnlySpan<byte> bytes, out int value)
        {
            value = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte c = bytes[i];
                if (c < (byte)'0' || c > (byte)'9')
                    return false;
                value = value * 10 + c - (byte)'0';
            }
            return bytes.Length > 0;
        }

        private static bool TryParsePositiveFloat(ReadOnlySpan<byte> bytes, out float value)
        {
            value = 0f;
            float divisor = 1f;
            bool fraction = false;
            bool consumed = false;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte c = bytes[i];
                if (c == (byte)'.' && !fraction)
                {
                    fraction = true;
                    continue;
                }
                if (c < (byte)'0' || c > (byte)'9')
                    return false;
                consumed = true;
                int digit = c - (byte)'0';
                if (fraction)
                {
                    divisor *= 10f;
                    value += digit / divisor;
                }
                else
                {
                    value = value * 10f + digit;
                }
            }
            return consumed;
        }
#endif

        private static uint HashAupSector(in double3 aup)
        {
            const double InvSectorSize = 0.001d;
            long sx = (long)math.floor(aup.x * InvSectorSize);
            long sy = (long)math.floor(aup.y * InvSectorSize);
            long sz = (long)math.floor(aup.z * InvSectorSize);
            uint hash = 2166136261u;
            hash = MixSignedLong(hash, sx);
            hash = MixSignedLong(hash, sy);
            hash = MixSignedLong(hash, sz);
            return hash != 0u ? hash : 0x160160u;
        }

        private static uint MixSignedLong(uint hash, long value)
        {
            ulong bits = unchecked((ulong)value);
            hash = (hash ^ unchecked((uint)bits)) * 16777619u;
            hash = (hash ^ unchecked((uint)(bits >> 32))) * 16777619u;
            return hash;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_drawHeatmapGizmos ||
                !TryReadWorkerBuffer(in _heatmapDebugHandle, 1, out NativeArray<AnalyticEventDTO>.ReadOnly heatmap))
            {
                return;
            }

            if (heatmap.Length == 0 || !TryReadCountersBuffer(out NativeArray<AnalyticsCountersDTO>.ReadOnly counters))
                return;

            uint cursor = counters[0].EventRingWriteCursor;
            int available = math.min(100, math.min(heatmap.Length, (int)math.min(cursor, (uint)heatmap.Length)));
            double3 origin = double3.zero;
            bool hasOrigin = false;
            for (int i = 0; i < available; i++)
            {
                int index = (int)((cursor + (uint)heatmap.Length - 1u - (uint)i) % (uint)heatmap.Length);
                AnalyticEventDTO dto = heatmap[index];
                if (dto.EventHashID == 0u || !math.all(math.isfinite(dto.EventAUP)))
                    continue;

                if (!hasOrigin)
                {
                    origin = dto.EventAUP;
                    hasOrigin = true;
                }

                double3 local = dto.EventAUP - origin;
                if (math.lengthsq((float3)local) > 250000f)
                    continue;

                Gizmos.color = ResolveHeatmapColor(dto.EventHashID);
                Gizmos.DrawSphere(new Vector3((float)local.x, (float)local.y, (float)local.z), 0.35f);
            }
        }

        private static Color ResolveHeatmapColor(uint eventHashId)
        {
            if (eventHashId == AnalyticsEventHashes.Death)
                return new Color(1f, 0.05f, 0.02f, 0.85f);
            if (eventHashId == AnalyticsEventHashes.ResourceDelta)
                return new Color(0.15f, 0.95f, 0.25f, 0.75f);
            if (eventHashId == AnalyticsEventHashes.PerfSpike)
                return new Color(1f, 0.2f, 0.95f, 0.8f);
            if (eventHashId == AnalyticsEventHashes.RouteSample || eventHashId == AnalyticsEventHashes.MockRoute)
                return new Color(0.1f, 0.72f, 1f, 0.55f);

            return (eventHashId & AnalyticsEventHashes.CriticalMask) != 0u
                ? new Color(1f, 0.5f, 0.05f, 0.8f)
                : new Color(0.65f, 0.75f, 0.95f, 0.45f);
        }
#endif
    }
}
