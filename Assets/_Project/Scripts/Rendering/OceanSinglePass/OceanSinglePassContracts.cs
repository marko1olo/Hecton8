using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Rendering.OceanSinglePass
{
    public static class OceanSinglePassConstants
    {
        public const int VisualOverridesStrideBytes = 32;
        public const int TuningStrideBytes = 64;
        public const int AestheticProfileStrideBytes = 64;
        public const int TelemetryEntryStrideBytes = 64;
        public const int TelemetryCapacity = 300;
        public const int AestheticProfileCapacity = 64;
        public const int CsvScratchBytes = 32768;
        public const int WakeEventGpuCapacity = 512;
        public const int WakeMinResolution = 256;
        public const int WakeMaxResolution = 1024;
        public const int WakeResolutionQuantum = 16;
        public const int CBufferBytes = VisualOverridesStrideBytes;
        public const float WakeTextureWorldSizeMeters = 512f;
        public const float DefaultSeaLevelMeters = 14.02f;
        public const float RenderGraphSpikeDumpThresholdMicroseconds = 2000f;
        public const uint LayoutHash = 0x53323632u;
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_262.bin";

        public const BufferID VisualOverridesBuffer = (BufferID)71895;
        public const BufferID TuningBuffer = (BufferID)71896;
        public const BufferID TelemetryRingBuffer = (BufferID)71897;
        public const BufferID TelemetryCursorBuffer = (BufferID)71898;
        public const BufferID AestheticProfilesBuffer = (BufferID)71899;
        public const BufferID CsvScratchBuffer = (BufferID)71900;
        public const BufferID MockRenderStateBuffer = (BufferID)71901;
        public const BufferID SelfAuditBuffer = (BufferID)71902;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct OceanVisualOverridesDTO
    {
        [FieldOffset(0)] public float4 FoamAndShadowParams;
        [FieldOffset(16)] public float4 ShorelineDepthParams;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct OceanGuillotineTuningDTO
    {
        [FieldOffset(0)] public float4 FoamParams;
        [FieldOffset(16)] public float4 WakeParams;
        [FieldOffset(32)] public float4 ShorelineParams;
        [FieldOffset(48)] public uint Version;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public float GlobalQualityWeightOverride;
        [FieldOffset(60)] public float Pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct OceanAestheticProfileDTO
    {
        [FieldOffset(0)] public uint BiomeHash;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public float FoamThreshold;
        [FieldOffset(12)] public float FoamIntensity;
        [FieldOffset(16)] public float WakeStrength;
        [FieldOffset(20)] public float WakeLifespanSeconds;
        [FieldOffset(24)] public float ShorelineDepthFadeMeters;
        [FieldOffset(28)] public float ReflectionCubemapMix;
        [FieldOffset(32)] public float SeaLevelMeters;
        [FieldOffset(36)] public float GlobalQualityWeightOverride;
        [FieldOffset(40)] public float4 Reserved0;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint Pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct OceanRenderTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public float DepthPassMicroseconds;
        [FieldOffset(12)] public float WakeComputeMicroseconds;
        [FieldOffset(16)] public int WakeResolution;
        [FieldOffset(20)] public float WakeResolutionScale;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] public float WakeEventCount;
        [FieldOffset(32)] public float4 WakeScrollOffset;
        [FieldOffset(48)] public uint StateHash;
        [FieldOffset(52)] public uint ProfileHash;
        [FieldOffset(56)] public float CpuSubmitMicroseconds;
        [FieldOffset(60)] public uint Pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct OceanMockRenderStateDTO
    {
        [FieldOffset(0)] public float4 PlaneCenterSize;
        [FieldOffset(16)] public float4 CameraLocalAup;
        [FieldOffset(32)] public float4 QualityFoamWakeSea;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] public uint Pad0;
    }

    public static class OceanSinglePassMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeQualityWeight(float qualityWeight)
        {
            return math.isfinite(qualityWeight) ? math.saturate(qualityWeight) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SmoothQualityCurve(float qualityWeight)
        {
            float q = SanitizeQualityWeight(qualityWeight);
            return q * q * (3f - 2f * q);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveWakeResolution(float qualityWeight)
        {
            float curved = SmoothQualityCurve(qualityWeight);
            float raw = math.lerp(
                OceanSinglePassConstants.WakeMinResolution,
                OceanSinglePassConstants.WakeMaxResolution,
                curved);
            int quantized = (int)math.round(raw / OceanSinglePassConstants.WakeResolutionQuantum) *
                            OceanSinglePassConstants.WakeResolutionQuantum;
            return math.clamp(
                quantized,
                OceanSinglePassConstants.WakeMinResolution,
                OceanSinglePassConstants.WakeMaxResolution);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveWakeResolutionScale(float qualityWeight)
        {
            return ResolveWakeResolution(qualityWeight) * (1f / OceanSinglePassConstants.WakeMaxResolution);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveWakeStrength(float qualityWeight)
        {
            float q = SanitizeQualityWeight(qualityWeight);
            return math.lerp(0.18f, 1.35f, q * q * (3f - 2f * q));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 ResolveWakeScrollOffset(double3 cameraAup, float textureWorldSizeMeters)
        {
            double safeSize = math.max(1.0, math.abs(textureWorldSizeMeters));
            double wrappedX = WrapMeters(cameraAup.x, safeSize);
            double wrappedZ = WrapMeters(cameraAup.z, safeSize);
            float localX = (float)wrappedX;
            float localZ = (float)wrappedZ;
            return new float4(localX / (float)safeSize, localZ / (float)safeSize, localX, localZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double WrapMeters(double value, double period)
        {
            double safePeriod = math.max(0.0001, math.abs(period));
            double wrapped = value - math.floor(value / safePeriod) * safePeriod;
            return wrapped < 0.0 ? wrapped + safePeriod : wrapped;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OceanGuillotineTuningDTO CreateDefaultTuning()
        {
            OceanGuillotineTuningDTO tuning = default;
            tuning.FoamParams = new float4(0.68f, 1.05f, 0.18f, 0f);
            tuning.WakeParams = new float4(1f, 3.6f, 0.82f, OceanSinglePassConstants.WakeTextureWorldSizeMeters);
            tuning.ShorelineParams = new float4(8f, OceanSinglePassConstants.DefaultSeaLevelMeters, 0.42f, 0.62f);
            tuning.Version = 1u;
            tuning.Flags = 1u;
            tuning.GlobalQualityWeightOverride = -1f;
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OceanVisualOverridesDTO ResolveVisualOverrides(in OceanGuillotineTuningDTO tuning, float globalQualityWeight)
        {
            float q = tuning.GlobalQualityWeightOverride >= 0f
                ? SanitizeQualityWeight(tuning.GlobalQualityWeightOverride)
                : SanitizeQualityWeight(globalQualityWeight);
            float qualityCurve = SmoothQualityCurve(q);

            OceanVisualOverridesDTO dto = default;
            dto.FoamAndShadowParams = new float4(
                math.saturate(tuning.FoamParams.x),
                math.max(0f, tuning.FoamParams.y) * math.lerp(0.22f, 1f, qualityCurve),
                math.saturate(tuning.FoamParams.z),
                q);
            dto.ShorelineDepthParams = new float4(
                math.max(0.1f, tuning.ShorelineParams.x),
                tuning.ShorelineParams.y,
                math.max(1f, tuning.WakeParams.w),
                math.max(0f, tuning.WakeParams.x) * ResolveWakeStrength(q));
            return dto;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashTelemetry(
            uint frame,
            int wakeResolution,
            float qualityWeight,
            float depthMicroseconds,
            float wakeMicroseconds)
        {
            uint hash = 2166136261u;
            hash = (hash ^ frame) * 16777619u;
            hash = (hash ^ unchecked((uint)wakeResolution)) * 16777619u;
            hash = (hash ^ math.asuint(qualityWeight)) * 16777619u;
            hash = (hash ^ math.asuint(depthMicroseconds)) * 16777619u;
            hash = (hash ^ math.asuint(wakeMicroseconds)) * 16777619u;
            return hash == 0u ? OceanSinglePassConstants.LayoutHash : hash;
        }
    }

    #if UNITY_EDITOR
    public static class OceanAestheticProfileCsvParser
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static bool TryParseProfiles(
            ReadOnlySpan<byte> bytes,
            NativeArray<OceanAestheticProfileDTO> profiles,
            out int profileCount,
            out uint fileHash)
        {
            fileHash = HashBytes(bytes);
            profileCount = 0;
            if (!profiles.IsCreated || profiles.Length <= 0)
                return false;

            int cursor = 0;
            while (cursor < bytes.Length && profileCount < profiles.Length)
            {
                ReadOnlySpan<byte> line = Trim(ReadLine(bytes, ref cursor));
                if (line.Length <= 0 || line[0] == (byte)'#')
                    continue;

                int tokenCursor = 0;
                ReadOnlySpan<byte> biome = Trim(ReadCsvToken(line, ref tokenCursor));
                if (biome.Length <= 0 || IsHeaderToken(biome))
                    continue;

                OceanAestheticProfileDTO profile = default;
                profile.BiomeHash = HashLowerAscii(biome);
                profile.Version = 1u;
                profile.FoamThreshold = 0.68f;
                profile.FoamIntensity = 1f;
                profile.WakeStrength = 1f;
                profile.WakeLifespanSeconds = 3.6f;
                profile.ShorelineDepthFadeMeters = 8f;
                profile.ReflectionCubemapMix = 0.42f;
                profile.SeaLevelMeters = OceanSinglePassConstants.DefaultSeaLevelMeters;
                profile.GlobalQualityWeightOverride = -1f;
                profile.Flags = 1u;

                if (!TryParseOptionalFloat(line, ref tokenCursor, ref profile.FoamThreshold) ||
                    !TryParseOptionalFloat(line, ref tokenCursor, ref profile.FoamIntensity) ||
                    !TryParseOptionalFloat(line, ref tokenCursor, ref profile.WakeStrength) ||
                    !TryParseOptionalFloat(line, ref tokenCursor, ref profile.WakeLifespanSeconds) ||
                    !TryParseOptionalFloat(line, ref tokenCursor, ref profile.ShorelineDepthFadeMeters) ||
                    !TryParseOptionalFloat(line, ref tokenCursor, ref profile.ReflectionCubemapMix) ||
                    !TryParseOptionalFloat(line, ref tokenCursor, ref profile.SeaLevelMeters) ||
                    !TryParseOptionalFloat(line, ref tokenCursor, ref profile.GlobalQualityWeightOverride))
                {
                    continue;
                }

                profile.FoamThreshold = math.saturate(profile.FoamThreshold);
                profile.FoamIntensity = math.clamp(profile.FoamIntensity, 0f, 8f);
                profile.WakeStrength = math.clamp(profile.WakeStrength, 0f, 8f);
                profile.WakeLifespanSeconds = math.clamp(profile.WakeLifespanSeconds, 0.05f, 24f);
                profile.ShorelineDepthFadeMeters = math.clamp(profile.ShorelineDepthFadeMeters, 0.1f, 128f);
                profile.ReflectionCubemapMix = math.saturate(profile.ReflectionCubemapMix);
                profile.SeaLevelMeters = ResolveProfileSeaLevelMeters(profile.SeaLevelMeters);
                profile.GlobalQualityWeightOverride = math.clamp(profile.GlobalQualityWeightOverride, -1f, 1f);
                profiles[profileCount] = profile;
                profileCount++;
            }

            return profileCount > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveProfileSeaLevelMeters(float value)
        {
            return math.isfinite(value) &&
                math.abs(value) > 0.0001f &&
                math.abs(value) <= 1000f
                ? value
                : OceanSinglePassConstants.DefaultSeaLevelMeters;
        }

        public static uint HashLowerAscii(ReadOnlySpan<byte> bytes)
        {
            uint hash = FnvOffset;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                hash = (hash ^ b) * FnvPrime;
            }

            return hash == 0u ? FnvOffset : hash;
        }

        private static bool IsHeaderToken(ReadOnlySpan<byte> token)
        {
            return token.Length == 5 &&
                   (token[0] == (byte)'b' || token[0] == (byte)'B') &&
                   (token[1] == (byte)'i' || token[1] == (byte)'I') &&
                   (token[2] == (byte)'o' || token[2] == (byte)'O') &&
                   (token[3] == (byte)'m' || token[3] == (byte)'M') &&
                   (token[4] == (byte)'e' || token[4] == (byte)'E');
        }

        private static ReadOnlySpan<byte> ReadLine(ReadOnlySpan<byte> bytes, ref int cursor)
        {
            int start = cursor;
            while (cursor < bytes.Length && bytes[cursor] != (byte)'\n')
                cursor++;

            int end = cursor;
            if (cursor < bytes.Length && bytes[cursor] == (byte)'\n')
                cursor++;

            return bytes.Slice(start, end - start);
        }

        private static ReadOnlySpan<byte> ReadCsvToken(ReadOnlySpan<byte> line, ref int cursor)
        {
            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;

            int end = cursor;
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;

            return line.Slice(start, end - start);
        }

        private static bool TryParseOptionalFloat(ReadOnlySpan<byte> line, ref int cursor, ref float value)
        {
            if (cursor >= line.Length)
                return true;

            ReadOnlySpan<byte> token = Trim(ReadCsvToken(line, ref cursor));
            if (token.Length <= 0)
                return true;

            if (!TryParseFloat(token, out float parsed))
                return false;

            value = parsed;
            return true;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> span)
        {
            int start = 0;
            int end = span.Length - 1;
            while (start <= end && IsAsciiWhitespace(span[start]))
                start++;
            while (end >= start && IsAsciiWhitespace(span[end]))
                end--;
            return start <= end ? span.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> bytes, out float value)
        {
            value = 0f;
            bytes = Trim(bytes);
            if (bytes.Length <= 0)
                return false;

            int index = 0;
            bool negative = false;
            if (bytes[index] == (byte)'-')
            {
                negative = true;
                index++;
            }
            else if (bytes[index] == (byte)'+')
            {
                index++;
            }

            float integer = 0f;
            bool hasDigit = false;
            while (index < bytes.Length)
            {
                byte b = bytes[index];
                if (b < (byte)'0' || b > (byte)'9')
                    break;

                integer = integer * 10f + (b - (byte)'0');
                hasDigit = true;
                index++;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (index < bytes.Length && bytes[index] == (byte)'.')
            {
                index++;
                while (index < bytes.Length)
                {
                    byte b = bytes[index];
                    if (b < (byte)'0' || b > (byte)'9')
                        break;

                    fraction = fraction * 10f + (b - (byte)'0');
                    divisor *= 10f;
                    hasDigit = true;
                    index++;
                }
            }

            if (!hasDigit || index != bytes.Length)
                return false;

            value = integer + fraction / divisor;
            if (negative)
                value = -value;
            return math.isfinite(value);
        }

        private static uint HashBytes(ReadOnlySpan<byte> bytes)
        {
            uint hash = FnvOffset;
            for (int i = 0; i < bytes.Length; i++)
                hash = (hash ^ bytes[i]) * FnvPrime;
            return hash == 0u ? FnvOffset : hash;
        }

        private static bool IsAsciiWhitespace(byte value)
        {
            return value == (byte)' ' ||
                   value == (byte)'\t' ||
                   value == (byte)'\r' ||
                   value == (byte)'\n';
        }
    }
    #endif

    public static unsafe class OceanSinglePassTelemetryDump
    {
        public static bool TryWrite(string projectRoot, NativeArray<OceanRenderTelemetryEntry> telemetryRing, int writeIndex, int writtenCount)
        {
            _ = projectRoot;
            _ = writeIndex;
            int count = telemetryRing.IsCreated
                ? math.clamp(writtenCount, 0, math.min(telemetryRing.Length, OceanSinglePassConstants.TelemetryCapacity))
                : 0;
            return count > 0;
        }

        private static int WrapIndex(int value, int capacity)
        {
            int safeCapacity = math.max(1, capacity);
            int wrapped = value % safeCapacity;
            return wrapped < 0 ? wrapped + safeCapacity : wrapped;
        }
    }
}
