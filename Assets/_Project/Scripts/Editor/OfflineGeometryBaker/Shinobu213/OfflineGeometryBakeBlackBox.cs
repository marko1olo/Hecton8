#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using Unity.Collections;
using UnityEditor;

namespace Hecton8.Editor.OfflineGeometry
{
    [InitializeOnLoad]
    internal static class OfflineGeometryBakeBlackBox
    {
        private const int RingCapacity = 300;
        private const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_213.bin";
        private const uint WarningNonFiniteAny = 0x80000000u;
        private const uint WarningNonFiniteExtractionMs = 0x40000000u;
        private const uint WarningNonFiniteSerializationMs = 0x20000000u;
        private const uint WarningNonFiniteLod1Threshold = 0x10000000u;
        private const uint WarningNonFiniteLod2Threshold = 0x08000000u;
        private const uint WarningNonFiniteQuality = 0x04000000u;
        private const uint WarningNonFiniteDepth = 0x02000000u;

        private static NativeArray<OfflineGeometryBakeTelemetryEntry> _ring;
        private static int _cursor;
        private static bool _sentinelRegistered;

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
            uint nonFiniteFlags = BuildNonFiniteFlags(in metric);
            uint nonFiniteFaultHash = nonFiniteFlags != 0u ? FoldNonFiniteFaultHash(in metric, nonFiniteFlags) : 0u;
            OfflineGeometryBakeTelemetryEntry entry = new OfflineGeometryBakeTelemetryEntry
            {
                SourceHash = StableHash(in metric.SourcePath),
                OutputHash = StableHash(in metric.OutputPath),
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
            entry.WarningFlags |= nonFiniteFlags;

            entry.StateHash = FoldStateHash(entry, nonFiniteFaultHash);
            _ring[_cursor] = entry;
            _cursor = (_cursor + 1) % RingCapacity;

            if (nonFiniteFlags != 0u)
                Dump();
        }

        internal static void Dump()
        {
            EnsureAllocated();
            OfflineGeometryBaker.EnsureFileFolder(DumpPath);
            string tempPath = DumpPath + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    Span<byte> rowBytes = stackalloc byte[64];
                    for (int i = 0; i < _ring.Length; i++)
                    {
                        int index = (_cursor + i) % RingCapacity;
                        OfflineGeometryBakeTelemetryEntry entry = _ring[index];
                        WriteTelemetryEntryLittleEndian(rowBytes, in entry);
                        stream.Write(rowBytes);
                    }

                    stream.Flush(true);
                }

                long expectedBytes = (long)RingCapacity * 64L;
                long actualBytes = new FileInfo(tempPath).Length;
                if (actualBytes != expectedBytes)
                    throw new IOException("[SHINOBU_213] Torn black-box dump write.");

                OfflineGeometryBaker.ReplaceTempFile(tempPath, DumpPath);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        internal static void Dispose()
        {
            try
            {
                UnregisterNativeMemorySentinel();
            }
            finally
            {
                if (_ring.IsCreated)
                    _ring.Dispose();
                _ring = default;
                _cursor = 0;
            }
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
            try
            {
                RegisterNativeMemorySentinel();
            }
            catch
            {
                _ring.Dispose();
                _ring = default;
                _cursor = 0;
                throw;
            }
        }

        private static void RegisterNativeMemorySentinel()
        {
            if (!_ring.IsCreated || _sentinelRegistered)
                return;

            Type sentinelType = FindType("Hecton8.Core.NativeMemorySentinel");
            Type lifetimeType = FindType("Hecton8.Core.NativeAllocationLifetime");
            if (sentinelType == null || lifetimeType == null)
                throw new InvalidOperationException("[SHINOBU_213] NativeMemorySentinel bridge unavailable for black-box ring.");

            MethodInfo method = sentinelType.GetMethod("RegisterNativeArray", BindingFlags.Public | BindingFlags.Static);
            if (method == null)
                throw new InvalidOperationException("[SHINOBU_213] NativeMemorySentinel.RegisterNativeArray unavailable.");

            object lifetime = Enum.Parse(lifetimeType, "Session");
            object id = method.MakeGenericMethod(typeof(OfflineGeometryBakeTelemetryEntry)).Invoke(
                null,
                new object[] { _ring, "SHINOBU_213", "OfflineGeometryBakeBlackBox.Ring", lifetime });
            _sentinelRegistered = id is int value && value != 0;
            if (!_sentinelRegistered)
                throw new InvalidOperationException("[SHINOBU_213] NativeMemorySentinel rejected black-box ring registration.");
        }

        private static void UnregisterNativeMemorySentinel()
        {
            if (!_ring.IsCreated || !_sentinelRegistered)
                return;

            Type sentinelType = FindType("Hecton8.Core.NativeMemorySentinel");
            MethodInfo method = sentinelType != null ? sentinelType.GetMethod("UnregisterNativeArray", BindingFlags.Public | BindingFlags.Static) : null;
            if (method == null)
                throw new InvalidOperationException("[SHINOBU_213] NativeMemorySentinel.UnregisterNativeArray unavailable.");

            try
            {
                method.MakeGenericMethod(typeof(OfflineGeometryBakeTelemetryEntry)).Invoke(null, new object[] { _ring });
            }
            finally
            {
                _sentinelRegistered = false;
            }
        }

        private static Type FindType(string fullName)
        {
            global::System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                    return type;
            }

            return null;
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

        private static uint BuildNonFiniteFlags(in OfflineBakeMetrics metric)
        {
            uint flags = 0u;
            if (!IsFinite(metric.ExtractionMilliseconds))
                flags |= WarningNonFiniteAny | WarningNonFiniteExtractionMs;
            if (!IsFinite(metric.SerializationMilliseconds))
                flags |= WarningNonFiniteAny | WarningNonFiniteSerializationMs;
            if (!IsFinite(metric.Lod1Threshold))
                flags |= WarningNonFiniteAny | WarningNonFiniteLod1Threshold;
            if (!IsFinite(metric.Lod2Threshold))
                flags |= WarningNonFiniteAny | WarningNonFiniteLod2Threshold;
            if (!IsFinite(metric.GlobalQualityWeight))
                flags |= WarningNonFiniteAny | WarningNonFiniteQuality;
            if (!IsFinite(metric.DepthMeters))
                flags |= WarningNonFiniteAny | WarningNonFiniteDepth;
            return flags;
        }

        private static uint FoldNonFiniteFaultHash(in OfflineBakeMetrics metric, uint nonFiniteFlags)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = Fold(hash, nonFiniteFlags);
                if ((nonFiniteFlags & WarningNonFiniteExtractionMs) != 0u)
                    hash = FoldDoubleBits(hash, metric.ExtractionMilliseconds);
                if ((nonFiniteFlags & WarningNonFiniteSerializationMs) != 0u)
                    hash = FoldDoubleBits(hash, metric.SerializationMilliseconds);
                if ((nonFiniteFlags & WarningNonFiniteLod1Threshold) != 0u)
                    hash = Fold(hash, Unity.Mathematics.math.asuint(metric.Lod1Threshold));
                if ((nonFiniteFlags & WarningNonFiniteLod2Threshold) != 0u)
                    hash = Fold(hash, Unity.Mathematics.math.asuint(metric.Lod2Threshold));
                if ((nonFiniteFlags & WarningNonFiniteQuality) != 0u)
                    hash = Fold(hash, Unity.Mathematics.math.asuint(metric.GlobalQualityWeight));
                if ((nonFiniteFlags & WarningNonFiniteDepth) != 0u)
                    hash = Fold(hash, Unity.Mathematics.math.asuint(metric.DepthMeters));
                return hash;
            }
        }

        private static uint FoldDoubleBits(uint hash, double value)
        {
            ulong bits = AsUInt64(value);
            hash = Fold(hash, (uint)bits);
            return Fold(hash, (uint)(bits >> 32));
        }

        private static unsafe ulong AsUInt64(double value)
        {
            return *(ulong*)&value;
        }

        private static uint Fold(uint hash, uint value)
        {
            unchecked
            {
                return (hash ^ value) * 16777619u;
            }
        }

        private static uint FoldStateHash(OfflineGeometryBakeTelemetryEntry entry, uint nonFiniteFaultHash)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = Fold(hash, entry.SourceHash);
                hash = Fold(hash, entry.OutputHash);
                hash = Fold(hash, (uint)entry.OriginalTriangles);
                hash = Fold(hash, (uint)entry.Lod0Triangles);
                hash = Fold(hash, (uint)entry.Lod1Triangles);
                hash = Fold(hash, (uint)entry.Lod2Triangles);
                hash = Fold(hash, entry.WarningFlags);
                hash = Fold(hash, nonFiniteFaultHash);
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

        private static uint StableHash(in FixedString128Bytes value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return hash;
            }
        }
    }
}
#endif
