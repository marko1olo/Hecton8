using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Graphics.Culling
{
    public static class AbyssalShadowCullingConstants
    {
        public const int DefaultInstanceCapacity = 50000;
        public const int TelemetryCapacity = 300;
        public const int FrustumPlaneCount = 6;
        public const int ProfileRuleCapacity = 64;
        public const int CsvScratchCapacity = 32768;
        public const int HzbGridResolution = 16;
        public const int HzbTileCapacity = HzbGridResolution * HzbGridResolution;
        public const int ShadowCullStateStrideBytes = 32;
        public const float MinimumShadowDistanceMeters = 20f;
        public const float DefaultMaximumShadowDistanceMeters = 150f;
        public const float DefaultDitherFadeBand01 = 0.1f;
        public const float DefaultDarknessThreshold = 0.045f;
        public const float DefaultPointLightUltraThreshold = 0.85f;
        public const float DefaultShadowCasterRadiusLow = 1.25f;
        public const float DefaultShadowCasterRadiusUltra = 0.15f;
        public const float DefaultDirectionalShadowReachMeters = 35f;
        public const float MinimumDistanceHysteresisMeters = 3f;
        public const float MaximumDistanceHysteresisMeters = 5f;
        public const float MinimumFrustumHysteresisMeters = 3f;
        public const float MaximumFrustumHysteresisMeters = 5f;
        public const float MaximumDarknessHysteresisScalar = 0.02f;
        public const float MinimumDarknessHysteresisScalar = 0.006f;
        public const float MaximumSdfHysteresisScalar = 0.025f;
        public const float MinimumSdfHysteresisScalar = 0.006f;
        public const float MaximumRadiusHysteresisMeters = 0.35f;
        public const float MinimumRadiusHysteresisMeters = 0.08f;
        public const float MaximumPointBudgetHysteresis01 = 0.1f;
        public const float MinimumPointBudgetHysteresis01 = 0.03f;
    }

    public static class AbyssalShadowBufferIds
    {
        public const BufferID Instances = (BufferID)71340;
        public const BufferID States = (BufferID)71341;
        public const BufferID IlluminationScalars = (BufferID)71342;
        public const BufferID FrustumPlanes = (BufferID)71343;
        public const BufferID Counters = (BufferID)71344;
        public const BufferID TelemetryRing = (BufferID)71345;
        public const BufferID RuntimeState = (BufferID)71346;
        public const BufferID ProfileRules = (BufferID)71347;
        public const BufferID CsvScratch = (BufferID)71348;
        public const BufferID HzbDepthTiles = (BufferID)71349;
        public const BufferID IndirectArgs = (BufferID)71350;
    }

    public static class AbyssalShadowCullFlags
    {
        public const uint MainVisible = 1u << 0;
        public const uint CastShadows = 1u << 1;
        public const uint ShadowOnly = 1u << 2;
        public const uint DitherFadeActive = 1u << 3;
        public const uint DarknessCulled = 1u << 4;
        public const uint DistanceShadowCulled = 1u << 5;
        public const uint MainFrustumCulled = 1u << 6;
        public const uint PointLightCulled = 1u << 7;
        public const uint TooSmallCaster = 1u << 8;
        public const uint RollbackExcluded = 1u << 9;
        public const uint HzbOcclusionCulled = 1u << 10;
        public const uint SdfOcclusionCulled = 1u << 11;
        public const uint NonFinite = 1u << 31;
    }

    public static class AbyssalShadowSourceFlags
    {
        public const uint DirectionalLightShadow = 1u << 0;
        public const uint PointLightShadow = 1u << 1;
        public const uint DynamicCaster = 1u << 2;
        public const uint RollbackAuthoritative = 1u << 30;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ShadowCullStateDTO
    {
        [FieldOffset(0)] public uint InstanceHash;
        [FieldOffset(4)] public float DistanceSq;
        [FieldOffset(8)] public uint CullFlags;
        [FieldOffset(12)] public float IlluminationScalar;
        [FieldOffset(16)] public byte _pad0;
        [FieldOffset(17)] public byte _pad1;
        [FieldOffset(18)] public byte _pad2;
        [FieldOffset(19)] public byte _pad3;
        [FieldOffset(20)] public byte _pad4;
        [FieldOffset(21)] public byte _pad5;
        [FieldOffset(22)] public byte _pad6;
        [FieldOffset(23)] public byte _pad7;
        [FieldOffset(24)] public byte _pad8;
        [FieldOffset(25)] public byte _pad9;
        [FieldOffset(26)] public byte _pad10;
        [FieldOffset(27)] public byte _pad11;
        [FieldOffset(28)] public byte _pad12;
        [FieldOffset(29)] public byte _pad13;
        [FieldOffset(30)] public byte _pad14;
        [FieldOffset(31)] public byte _pad15;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ShadowCullInstanceDTO
    {
        [FieldOffset(0)] public double3 CenterAUP;
        [FieldOffset(24)] public float3 Extents;
        [FieldOffset(36)] public float BoundsRadius;
        [FieldOffset(40)] public uint InstanceHash;
        [FieldOffset(44)] public uint SourceFlags;
        [FieldOffset(48)] public float MaterialShadowScalar;
        [FieldOffset(52)] public float OcclusionScalar;
        [FieldOffset(56)] public uint ProfileHash;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ShadowCullCountersDTO
    {
        [FieldOffset(0)] public uint EvaluatedCount;
        [FieldOffset(4)] public uint MainCulledCount;
        [FieldOffset(8)] public uint ShadowCulledCount;
        [FieldOffset(12)] public uint DarknessCulledCount;
        [FieldOffset(16)] public uint PointLightCulledCount;
        [FieldOffset(20)] public uint ShadowOnlyCount;
        [FieldOffset(24)] public uint DitheredCount;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public uint HzbCulledCount;
        [FieldOffset(36)] public uint SdfCulledCount;
        [FieldOffset(40)] public uint VisibleShadowCount;
        [FieldOffset(44)] public uint ProfileRuleCount;
        [FieldOffset(48)] public uint StateHash;
        [FieldOffset(52)] public uint _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CullingTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint EvaluatedCount;
        [FieldOffset(8)] public uint MainCulledCount;
        [FieldOffset(12)] public uint ShadowCulledCount;
        [FieldOffset(16)] public uint DarknessCulledCount;
        [FieldOffset(20)] public uint PointLightCulledCount;
        [FieldOffset(24)] public uint UploadedCount;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public float BurstWallTimeMs;
        [FieldOffset(36)] public float UploadMicroseconds;
        [FieldOffset(40)] public float GlobalQualityWeight;
        [FieldOffset(44)] public float MaxShadowDistanceMeters;
        [FieldOffset(48)] public uint StateHash;
        [FieldOffset(52)] public uint NonFiniteHash;
        [FieldOffset(56)] public uint ShadowOnlyCount;
        [FieldOffset(60)] public uint DitheredCount;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AbyssalShadowRuntimeStateDTO
    {
        [FieldOffset(0)] public float BaseShadowDistanceMeters;
        [FieldOffset(4)] public float DitherFadeBand01;
        [FieldOffset(8)] public float DarknessThreshold;
        [FieldOffset(12)] public int ActiveInstanceCount;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public float GlobalQualityWeightOverride;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public float3 DirectionalLightDirection;
        [FieldOffset(44)] public float PointLightUltraThreshold;
        [FieldOffset(48)] public float MaxShadowDistanceMeters;
        [FieldOffset(52)] public float MinCasterRadiusMeters;
        [FieldOffset(56)] public uint LastUploadCount;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct ShadowCullHzbTileDTO
    {
        [FieldOffset(0)] public float DepthMeters;
        [FieldOffset(4)] public float OcclusionBiasMeters;
        [FieldOffset(8)] public uint TileHash;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ShadowCullIndirectArgsDTO
    {
        [FieldOffset(0)] public uint VertexCountPerInstance;
        [FieldOffset(4)] public uint InstanceCount;
        [FieldOffset(8)] public uint StartVertex;
        [FieldOffset(12)] public uint StartInstance;
        [FieldOffset(16)] public uint StartIndex;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint _pad0;
        [FieldOffset(28)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AbyssalShadowTunerSnapshot
    {
        [FieldOffset(0)] public uint EvaluatedCount;
        [FieldOffset(4)] public uint MainCulledCount;
        [FieldOffset(8)] public uint ShadowCulledCount;
        [FieldOffset(12)] public uint DarknessCulledCount;
        [FieldOffset(16)] public uint PointLightCulledCount;
        [FieldOffset(20)] public uint ShadowOnlyCount;
        [FieldOffset(24)] public uint DitheredCount;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public float GlobalQualityWeight;
        [FieldOffset(36)] public float BaseShadowDistanceMeters;
        [FieldOffset(40)] public float DitherFadeBand01;
        [FieldOffset(44)] public float DarknessThreshold;
        [FieldOffset(48)] public float LastBurstWallTimeMs;
        [FieldOffset(52)] public float LastUploadMicroseconds;
        [FieldOffset(56)] public float MaxShadowDistanceMeters;
        [FieldOffset(60)] public uint LastUploadCount;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ShadowCullProfileRuleDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float MinCasterRadiusMeters;
        [FieldOffset(8)] public float ShadowDistanceScale;
        [FieldOffset(12)] public float DarknessThresholdScale;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public float PointLightBudget01;
        [FieldOffset(24)] public float FadeBandScale;
        [FieldOffset(28)] public uint _pad0;
    }

#if UNITY_EDITOR
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ShadowCullCsvParseResultDTO
    {
        [FieldOffset(0)] public uint ParsedRuleCount;
        [FieldOffset(4)] public uint RejectedLineCount;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint LastProfileHash;
        [FieldOffset(16)] public float LastMinCasterRadiusMeters;
        [FieldOffset(20)] public float LastDistanceScale;
        [FieldOffset(24)] public float LastDarknessScale;
        [FieldOffset(28)] public float LastFadeBandScale;
    }

    public static class AbyssalShadowFrustumMath
    {
        public static float4 LocalizeWorldPlane(float4 worldPlane, double3 cameraAUP)
        {
            double3 normal = new double3(worldPlane.x, worldPlane.y, worldPlane.z);
            double shiftedDistance = (double)worldPlane.w + math.dot(normal, cameraAUP);
            return new float4(worldPlane.xyz, (float)shiftedDistance);
        }

        public static void WriteDefaultCameraRelativePlanes(NativeArray<float4> planes)
        {
            if (!planes.IsCreated || planes.Length < AbyssalShadowCullingConstants.FrustumPlaneCount)
                return;

            planes[0] = new float4(1f, 0f, 0f, 48f);
            planes[1] = new float4(-1f, 0f, 0f, 48f);
            planes[2] = new float4(0f, 1f, 0f, 32f);
            planes[3] = new float4(0f, -1f, 0f, 32f);
            planes[4] = new float4(0f, 0f, 1f, 2f);
            planes[5] = new float4(0f, 0f, -1f, 220f);
        }
    }

    public static unsafe class AbyssalShadowProfileCsv
    {
        public static bool Parse(
            NativeArray<byte> bytes,
            int byteCount,
            NativeArray<ShadowCullProfileRuleDTO> rules,
            out ShadowCullCsvParseResultDTO result)
        {
            if (!bytes.IsCreated || !rules.IsCreated || byteCount <= 0)
            {
                result = default;
                return false;
            }

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes);
            return ParseInternal(ptr, byteCount, rules, rules.Length, true, out result);
        }

        public static bool Validate(
            NativeArray<byte> bytes,
            int byteCount,
            int ruleCapacity,
            out ShadowCullCsvParseResultDTO result)
        {
            if (!bytes.IsCreated || byteCount <= 0 || ruleCapacity <= 0)
            {
                result = default;
                return false;
            }

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes);
            return ParseInternal(ptr, byteCount, default, ruleCapacity, false, out result);
        }

        private static bool ParseInternal(
            byte* ptr,
            int byteCount,
            NativeArray<ShadowCullProfileRuleDTO> rules,
            int ruleCapacity,
            bool commit,
            out ShadowCullCsvParseResultDTO result)
        {
            result = default;
            int lineStart = 0;
            int writeIndex = 0;
            for (int i = 0; i <= byteCount; i++)
            {
                if (i < byteCount && ptr[i] != '\n')
                    continue;

                int lineEnd = i;
                if (lineEnd > lineStart && ptr[lineEnd - 1] == '\r')
                    lineEnd--;

                if (TryParseRule(ptr, lineStart, lineEnd, out ShadowCullProfileRuleDTO rule))
                {
                    if (writeIndex < ruleCapacity)
                    {
                        if (commit)
                            rules[writeIndex] = rule;
                        writeIndex++;
                        result.LastProfileHash = rule.ProfileHash;
                        result.LastMinCasterRadiusMeters = rule.MinCasterRadiusMeters;
                        result.LastDistanceScale = rule.ShadowDistanceScale;
                        result.LastDarknessScale = rule.DarknessThresholdScale;
                        result.LastFadeBandScale = rule.FadeBandScale;
                    }
                    else
                    {
                        result.RejectedLineCount++;
                    }
                }
                else if (HasNonCommentPayload(ptr, lineStart, lineEnd))
                {
                    result.RejectedLineCount++;
                }

                lineStart = i + 1;
            }

            result.ParsedRuleCount = (uint)writeIndex;
            result.Flags = writeIndex > 0 ? 1u : 0u;
            return writeIndex > 0;
        }

        private static bool TryParseRule(byte* bytes, int lineStart, int lineEnd, out ShadowCullProfileRuleDTO rule)
        {
            rule = default;
            int cursor = SkipWhitespace(bytes, lineStart, lineEnd);
            if (cursor >= lineEnd || bytes[cursor] == '#')
                return false;

            int nameStart = cursor;
            cursor = SeekSeparator(bytes, cursor, lineEnd);
            int nameEnd = TrimEnd(bytes, nameStart, cursor);
            if (cursor >= lineEnd)
                return false;
            cursor++;

            if (!TryReadFloatToken(bytes, ref cursor, lineEnd, out float minRadius))
                return false;
            if (!TryReadFloatToken(bytes, ref cursor, lineEnd, out float distanceScale))
                return false;
            if (!TryReadFloatToken(bytes, ref cursor, lineEnd, out float darknessScale))
                return false;
            if (!TryReadFloatToken(bytes, ref cursor, lineEnd, out float fadeScale))
                fadeScale = 1f;

            rule.ProfileHash = HashAsciiLower(bytes, nameStart, nameEnd);
            rule.MinCasterRadiusMeters = math.max(0f, minRadius);
            rule.ShadowDistanceScale = math.clamp(distanceScale, 0.1f, 4f);
            rule.DarknessThresholdScale = math.clamp(darknessScale, 0.1f, 4f);
            rule.Flags = 1u;
            rule.PointLightBudget01 = math.saturate(distanceScale - 0.5f);
            rule.FadeBandScale = math.clamp(fadeScale, 0.25f, 4f);
            rule._pad0 = 0u;
            return rule.ProfileHash != 0u;
        }

        private static bool TryReadFloatToken(byte* bytes, ref int cursor, int lineEnd, out float value)
        {
            int start = SkipWhitespace(bytes, cursor, lineEnd);
            int end = SeekSeparator(bytes, start, lineEnd);
            int trimmedEnd = TrimEnd(bytes, start, end);
            cursor = end < lineEnd ? end + 1 : end;
            return TryParseFloat(bytes, start, trimmedEnd, out value);
        }

        private static int SeekSeparator(byte* bytes, int start, int end)
        {
            int cursor = start;
            while (cursor < end && bytes[cursor] != ',' && bytes[cursor] != '=' && bytes[cursor] != ';')
                cursor++;
            return cursor;
        }

        private static int SkipWhitespace(byte* bytes, int start, int end)
        {
            int cursor = start;
            while (cursor < end && (bytes[cursor] == ' ' || bytes[cursor] == '\t'))
                cursor++;
            return cursor;
        }

        private static int TrimEnd(byte* bytes, int start, int end)
        {
            int cursor = end;
            while (cursor > start && (bytes[cursor - 1] == ' ' || bytes[cursor - 1] == '\t'))
                cursor--;
            return cursor;
        }

        private static bool HasNonCommentPayload(byte* bytes, int start, int end)
        {
            int cursor = SkipWhitespace(bytes, start, end);
            return cursor < end && bytes[cursor] != '#';
        }

        private static bool TryParseFloat(byte* bytes, int start, int end, out float value)
        {
            value = 0f;
            if (start >= end)
                return false;

            int cursor = start;
            float sign = 1f;
            if (bytes[cursor] == '-')
            {
                sign = -1f;
                cursor++;
            }
            else if (bytes[cursor] == '+')
            {
                cursor++;
            }

            float integer = 0f;
            bool hasDigit = false;
            while (cursor < end && bytes[cursor] >= '0' && bytes[cursor] <= '9')
            {
                hasDigit = true;
                integer = integer * 10f + (bytes[cursor] - '0');
                cursor++;
            }

            float fraction = 0f;
            float scale = 0.1f;
            if (cursor < end && bytes[cursor] == '.')
            {
                cursor++;
                while (cursor < end && bytes[cursor] >= '0' && bytes[cursor] <= '9')
                {
                    hasDigit = true;
                    fraction += (bytes[cursor] - '0') * scale;
                    scale *= 0.1f;
                    cursor++;
                }
            }

            if (!hasDigit)
                return false;
            if (cursor != end)
                return false;

            value = sign * (integer + fraction);
            return math.isfinite(value);
        }

        public static uint HashAsciiLower(byte* bytes, int start, int end)
        {
            uint hash = 2166136261u;
            for (int i = start; i < end; i++)
            {
                byte c = bytes[i];
                if (c >= 'A' && c <= 'Z')
                    c = (byte)(c + 32);
                hash ^= c;
                hash *= 16777619u;
            }

            return hash == 0u ? 1u : hash;
        }
    }
#endif

    public static class AbyssalShadowDumpWriter
    {
        public static unsafe void DumpTelemetry(string path, NativeArray<CullingTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            int byteCount = telemetry.Length * UnsafeUtility.SizeOf<CullingTelemetryEntry>();
            void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
            NativeFaultDumpWriter.TryWriteAll(path, new ReadOnlySpan<byte>(source, byteCount), byteCount);
        }
    }
}
