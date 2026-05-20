#if UNITY_EDITOR
using System;
using System.IO;
using Unity.Collections;
using UnityEditor;

namespace Hecton8.Editor.OfflineGeometry
{
    [InitializeOnLoad]
    internal static class OfflineGeometryBakeBlackBox
    {
        private const int RingCapacity = 300;
        private const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_213.bin";

        private static NativeArray<OfflineGeometryBakeTelemetryEntry> _ring;
        private static int _cursor;

        static OfflineGeometryBakeBlackBox()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting -= Dispose;
            EditorApplication.quitting += Dispose;
        }

        internal static void Record(in OfflineBakeMetrics metric)
        {
            EnsureAllocated();
            OfflineGeometryBakeTelemetryEntry entry = new OfflineGeometryBakeTelemetryEntry
            {
                SourceHash = StableHash(metric.SourcePath.ToString()),
                OutputHash = StableHash(metric.OutputPath.ToString()),
                OriginalTriangles = metric.OriginalTriangles,
                Lod0Triangles = metric.Lod0Triangles,
                Lod1Triangles = metric.Lod1Triangles,
                Lod2Triangles = metric.Lod2Triangles,
                PrimitiveColliderCount = metric.PrimitiveColliderCount,
                ConvexColliderCount = metric.ConvexColliderCount,
                ExtractionMicroseconds = ToMicroseconds(metric.ExtractionMilliseconds),
                SerializationMicroseconds = ToMicroseconds(metric.SerializationMilliseconds),
                Lod1Threshold = Sanitize(metric.Lod1Threshold),
                Lod2Threshold = Sanitize(metric.Lod2Threshold),
                GlobalQualityWeight = Sanitize(metric.GlobalQualityWeight),
                DepthMeters = Sanitize(metric.DepthMeters),
                WarningFlags = metric.WarningFlags
            };
            entry.StateHash = FoldStateHash(entry);
            _ring[_cursor] = entry;
            _cursor = (_cursor + 1) % RingCapacity;

            if (!IsFinite(metric.ExtractionMilliseconds) ||
                !IsFinite(metric.SerializationMilliseconds) ||
                !IsFinite(metric.Lod1Threshold) ||
                !IsFinite(metric.Lod2Threshold) ||
                !IsFinite(metric.GlobalQualityWeight) ||
                !IsFinite(metric.DepthMeters))
            {
                Dump();
            }
        }

        internal static void Dump()
        {
            EnsureAllocated();
            OfflineGeometryBaker.EnsureFileFolder(DumpPath);
            using (FileStream stream = new FileStream(DumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                Span<byte> rowBytes = stackalloc byte[64];
                for (int i = 0; i < _ring.Length; i++)
                {
                    int index = (_cursor + i) % RingCapacity;
                    OfflineGeometryBakeTelemetryEntry entry = _ring[index];
                    WriteTelemetryEntryLittleEndian(rowBytes, in entry);
                    stream.Write(rowBytes);
                }
            }
        }

        internal static void Dispose()
        {
            if (_ring.IsCreated)
                _ring.Dispose();
            _ring = default;
            _cursor = 0;
        }

        private static void EnsureAllocated()
        {
            if (_ring.IsCreated)
                return;

            // COLD ALLOC: NativeArray<OfflineGeometryBakeTelemetryEntry>[300] - editor bake black-box ring - owner: OfflineGeometryBakeBlackBox
            _ring = new NativeArray<OfflineGeometryBakeTelemetryEntry>(RingCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            OfflineGeometryBakeTelemetryEntry sentinel = default;
            sentinel.StateHash = 0x53483231u;
            for (int i = 0; i < RingCapacity; i++)
                _ring[i] = sentinel;
            _cursor = 0;
        }

        private static int ToMicroseconds(double milliseconds)
        {
            if (!IsFinite(milliseconds))
                return 0;

            double microseconds = milliseconds * 1000d;
            if (microseconds <= 0d)
                return 0;
            if (microseconds >= int.MaxValue)
                return int.MaxValue;
            return (int)microseconds;
        }

        private static float Sanitize(float value)
        {
            return IsFinite(value) ? value : 0f;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static uint FoldStateHash(OfflineGeometryBakeTelemetryEntry entry)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ entry.SourceHash) * 16777619u;
                hash = (hash ^ entry.OutputHash) * 16777619u;
                hash = (hash ^ (uint)entry.OriginalTriangles) * 16777619u;
                hash = (hash ^ (uint)entry.Lod0Triangles) * 16777619u;
                hash = (hash ^ (uint)entry.Lod1Triangles) * 16777619u;
                hash = (hash ^ (uint)entry.Lod2Triangles) * 16777619u;
                hash = (hash ^ entry.WarningFlags) * 16777619u;
                return hash;
            }
        }

        private static void WriteTelemetryEntryLittleEndian(Span<byte> bytes, in OfflineGeometryBakeTelemetryEntry entry)
        {
            WriteUInt32Little(bytes, 0, entry.SourceHash);
            WriteUInt32Little(bytes, 4, entry.OutputHash);
            WriteInt32Little(bytes, 8, entry.OriginalTriangles);
            WriteInt32Little(bytes, 12, entry.Lod0Triangles);
            WriteInt32Little(bytes, 16, entry.Lod1Triangles);
            WriteInt32Little(bytes, 20, entry.Lod2Triangles);
            WriteInt32Little(bytes, 24, entry.PrimitiveColliderCount);
            WriteInt32Little(bytes, 28, entry.ConvexColliderCount);
            WriteInt32Little(bytes, 32, entry.ExtractionMicroseconds);
            WriteInt32Little(bytes, 36, entry.SerializationMicroseconds);
            WriteFloatLittle(bytes, 40, entry.Lod1Threshold);
            WriteFloatLittle(bytes, 44, entry.Lod2Threshold);
            WriteFloatLittle(bytes, 48, entry.GlobalQualityWeight);
            WriteFloatLittle(bytes, 52, entry.DepthMeters);
            WriteUInt32Little(bytes, 56, entry.WarningFlags);
            WriteUInt32Little(bytes, 60, entry.StateHash);
        }

        private static void WriteFloatLittle(Span<byte> bytes, int offset, float value)
        {
            WriteUInt32Little(bytes, offset, Unity.Mathematics.math.asuint(value));
        }

        private static void WriteInt32Little(Span<byte> bytes, int offset, int value)
        {
            WriteUInt32Little(bytes, offset, unchecked((uint)value));
        }

        private static void WriteUInt32Little(Span<byte> bytes, int offset, uint value)
        {
            uint endianSafe = BitConverter.IsLittleEndian ? value : ReverseBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                bytes[offset] = (byte)endianSafe;
                bytes[offset + 1] = (byte)(endianSafe >> 8);
                bytes[offset + 2] = (byte)(endianSafe >> 16);
                bytes[offset + 3] = (byte)(endianSafe >> 24);
            }
            else
            {
                bytes[offset] = (byte)(endianSafe >> 24);
                bytes[offset + 1] = (byte)(endianSafe >> 16);
                bytes[offset + 2] = (byte)(endianSafe >> 8);
                bytes[offset + 3] = (byte)endianSafe;
            }
        }

        private static uint ReverseBytes(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                   ((value & 0x0000FF00u) << 8) |
                   ((value & 0x00FF0000u) >> 8) |
                   ((value & 0xFF000000u) >> 24);
        }

        private static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (!string.IsNullOrEmpty(value))
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        hash ^= value[i];
                        hash *= 16777619u;
                    }
                }

                return hash;
            }
        }
    }
}
#endif
