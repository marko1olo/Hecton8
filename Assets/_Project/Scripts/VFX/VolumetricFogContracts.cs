using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.VFX
{
    public static class VolumetricFogConstants
    {
        public const int ParamsStrideBytes = 64;
        public const int PointLightStrideBytes = 32;
        public const int TelemetryEntryStrideBytes = 64;
        public const int ExtinctionProfileStrideBytes = 64;
        public const int TelemetryCapacity = 300;
        public const int MaxPointLights = 8;
        public const int ExtinctionProfileCapacity = 16;
        public const int ExtinctionCsvScratchBytes = 65536;
        public const int MinRaySteps = 4;
        public const int MaxRaySteps = 64;
    }

    [StructLayout(LayoutKind.Explicit, Size = VolumetricFogConstants.ParamsStrideBytes)]
    public struct VolumetricFogParamsDTO
    {
        [FieldOffset(0)] public float4 FogColorAndDensity;
        [FieldOffset(16)] public float4 ScatteringParams;
        [FieldOffset(32)] public float4 FlowAdvection;
        [FieldOffset(48)] public float4 QualityAndLimits;
    }

    [StructLayout(LayoutKind.Explicit, Size = VolumetricFogConstants.PointLightStrideBytes)]
    public struct PointLightDTO
    {
        [FieldOffset(0)] public float4 PositionRadius;
        [FieldOffset(16)] public float4 ColorIntensity;
    }

    [StructLayout(LayoutKind.Explicit, Size = VolumetricFogConstants.TelemetryEntryStrideBytes)]
    public struct VolumetricFogTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public int RaySteps;
        [FieldOffset(8)] public float RenderScale;
        [FieldOffset(12)] public float EstimatedGpuMicroseconds;
        [FieldOffset(16)] public float4 CameraPositionLocalAndQuality;
        [FieldOffset(32)] public uint StateHash;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public float AccumulatedDensity;
        [FieldOffset(44)] public float MaxRayDistance;
        [FieldOffset(48)] public float4 DebugValues;
    }

    [StructLayout(LayoutKind.Explicit, Size = VolumetricFogConstants.ExtinctionProfileStrideBytes)]
    public struct WaterExtinctionProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float MinDepthMeters;
        [FieldOffset(8)] public float MaxDepthMeters;
        [FieldOffset(12)] public float DensityMultiplier;
        [FieldOffset(16)] public float4 AbsorptionAndScatter;
        [FieldOffset(32)] public float4 BiomeWeights;
        [FieldOffset(48)] public float4 Reserved;
    }

    public static class VolumetricFogNativeLayout
    {
        public static bool Validate()
        {
            return UnsafeUtility.SizeOf<VolumetricFogParamsDTO>() == VolumetricFogConstants.ParamsStrideBytes &&
                   OffsetOf<VolumetricFogParamsDTO>(nameof(VolumetricFogParamsDTO.FogColorAndDensity)) == 0 &&
                   OffsetOf<VolumetricFogParamsDTO>(nameof(VolumetricFogParamsDTO.ScatteringParams)) == 16 &&
                   OffsetOf<VolumetricFogParamsDTO>(nameof(VolumetricFogParamsDTO.FlowAdvection)) == 32 &&
                   OffsetOf<VolumetricFogParamsDTO>(nameof(VolumetricFogParamsDTO.QualityAndLimits)) == 48 &&
                   UnsafeUtility.SizeOf<PointLightDTO>() == VolumetricFogConstants.PointLightStrideBytes &&
                   UnsafeUtility.SizeOf<VolumetricFogTelemetryEntry>() == VolumetricFogConstants.TelemetryEntryStrideBytes &&
                   UnsafeUtility.SizeOf<WaterExtinctionProfileDTO>() == VolumetricFogConstants.ExtinctionProfileStrideBytes;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }
    }

    public static class VolumetricFogParamsAccess
    {
        public static ref VolumetricFogParamsDTO ElementAt(NativeArray<VolumetricFogParamsDTO> values, int index)
        {
            unsafe
            {
                return ref ((VolumetricFogParamsDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(values))[index];
            }
        }

        public static ref PointLightDTO LightAt(NativeArray<PointLightDTO> values, int index)
        {
            unsafe
            {
                return ref ((PointLightDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(values))[index];
            }
        }

        public static WaterExtinctionProfileDTO CreateDefaultExtinctionProfile()
        {
            return new WaterExtinctionProfileDTO
            {
                ProfileHash = VolumetricFogExtinctionCsvParser.HashAsciiLower("default_abyss"),
                MinDepthMeters = 0f,
                MaxDepthMeters = 20000f,
                DensityMultiplier = 1f,
                AbsorptionAndScatter = new float4(0.035f, 0.075f, 0.11f, 0.65f),
                BiomeWeights = new float4(1f, 0f, 0f, 0f),
                Reserved = default
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct BuildMockVolumetricLightsJob : IJob
    {
        [NoAlias] public NativeArray<PointLightDTO> PointLights;
        public float3 CameraPositionWS;
        public float3 CameraForwardWS;
        public float FramePhaseSeconds;
        public float QualityWeight;

        public void Execute()
        {
            if (!PointLights.IsCreated)
                return;

            int capacity = math.min(PointLights.Length, VolumetricFogConstants.MaxPointLights);
            for (int i = 0; i < capacity; i++)
                PointLights[i] = default;

            if (capacity <= 0)
                return;

            float quality = math.saturate(SanitizeFloat(QualityWeight, 0f));
            int activeCount = math.clamp(1 + (int)math.floor(quality * (capacity - 1) + 0.0001f), 1, capacity);
            float3 cameraPosition = SanitizeFloat3(CameraPositionWS);
            float3 forward = SafeNormalize(SanitizeFloat3(CameraForwardWS), new float3(0f, 0f, 1f));
            float3 side = SafeNormalize(math.cross(forward, new float3(0f, 1f, 0f)), new float3(1f, 0f, 0f));
            float3 up = SafeNormalize(math.cross(side, forward), new float3(0f, 1f, 0f));
            float phaseSeconds = SanitizeFloat(FramePhaseSeconds, 0f);

            for (int i = 0; i < activeCount; i++)
            {
                float index01 = (i + 0.5f) / math.max(1, activeCount);
                float phase = phaseSeconds * (0.11f + index01 * 0.07f) + index01 * 6.2831853f;
                float radialMeters = math.lerp(7f, 22f, index01);
                float heightMeters = math.lerp(-2.5f, 3.5f, math.frac(index01 * 1.6180339f));
                float3 offset = forward * (10f + radialMeters * 0.8f) +
                                side * (math.sin(phase) * radialMeters) +
                                up * (heightMeters + math.cos(phase * 0.7f) * 1.5f);
                float pulse = 0.65f + 0.35f * math.sin(phase * 1.7f);
                float radius = math.lerp(7f, 18f, quality) * (0.75f + index01 * 0.5f);
                float intensity = math.lerp(0.15f, 1.15f, quality) * pulse;

                PointLights[i] = new PointLightDTO
                {
                    PositionRadius = new float4(cameraPosition + offset, SanitizeFloat(radius, 1f)),
                    ColorIntensity = new float4(
                        SanitizeFloat(0.07f + index01 * 0.05f, 0.07f),
                        SanitizeFloat(0.18f + index01 * 0.04f, 0.18f),
                        SanitizeFloat(0.24f + index01 * 0.08f, 0.24f),
                        SanitizeFloat(intensity, 0f))
                };
            }
        }

        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return math.isfinite(lengthSq) && lengthSq > 1e-6f ? value * math.rsqrt(lengthSq) : fallback;
        }

        private static float3 SanitizeFloat3(float3 value)
        {
            return math.select(float3.zero, value, math.isfinite(value));
        }

        private static float SanitizeFloat(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }
    }

    public static class VolumetricFogExtinctionCsvParser
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const uint HeaderNameHash = 0x8D39BDE6u;
        private const uint HeaderProfileHash = 0xF80C33B0u;

        public static bool TryParseInto(
            ReadOnlySpan<byte> bytes,
            NativeArray<WaterExtinctionProfileDTO> profiles,
            out int profileCount,
            out uint fileHash)
        {
            profileCount = 0;
            fileHash = FnvOffset;
            if (bytes.IsEmpty || !profiles.IsCreated)
                return false;

            for (int i = 0; i < profiles.Length; i++)
                profiles[i] = default;

            int lineStart = 0;
            for (int i = 0; i <= bytes.Length; i++)
            {
                if (i < bytes.Length && bytes[i] != (byte)'\n')
                {
                    fileHash = (fileHash ^ bytes[i]) * FnvPrime;
                    continue;
                }

                if (TryParseLine(bytes.Slice(lineStart, i - lineStart), out WaterExtinctionProfileDTO profile))
                    UpsertProfile(profiles, ref profileCount, in profile);

                lineStart = i + 1;
            }

            return profileCount > 0;
        }

        public static uint HashAsciiLower(string text)
        {
            if (string.IsNullOrEmpty(text))
                return FnvOffset;

            uint hash = FnvOffset;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                byte b = c >= 'A' && c <= 'Z' ? (byte)(c + 32) : (byte)c;
                hash = (hash ^ b) * FnvPrime;
            }

            return hash;
        }

        private static bool TryParseLine(ReadOnlySpan<byte> line, out WaterExtinctionProfileDTO profile)
        {
            profile = default;
            line = Trim(line);
            if (line.IsEmpty || line[0] == (byte)'#')
                return false;

            ReadOnlySpan<byte> name = ReadColumn(line, 0);
            uint profileHash = HashKey(name);
            if (profileHash == HeaderNameHash || profileHash == HeaderProfileHash)
                return false;

            if (!TryParseFloat(ReadColumn(line, 1), out float minDepth) ||
                !TryParseFloat(ReadColumn(line, 2), out float maxDepth) ||
                !TryParseFloat(ReadColumn(line, 3), out float absorptionR) ||
                !TryParseFloat(ReadColumn(line, 4), out float absorptionG) ||
                !TryParseFloat(ReadColumn(line, 5), out float absorptionB))
            {
                return false;
            }

            float scatter = TryParseFloat(ReadColumn(line, 6), out float parsedScatter) ? parsedScatter : 0.65f;
            float density = TryParseFloat(ReadColumn(line, 7), out float parsedDensity) ? parsedDensity : 1f;
            profile = new WaterExtinctionProfileDTO
            {
                ProfileHash = profileHash,
                MinDepthMeters = math.max(0f, minDepth),
                MaxDepthMeters = math.max(math.max(0f, minDepth) + 0.001f, maxDepth),
                DensityMultiplier = math.clamp(density, 0f, 8f),
                AbsorptionAndScatter = new float4(
                    math.max(0f, absorptionR),
                    math.max(0f, absorptionG),
                    math.max(0f, absorptionB),
                    math.max(0f, scatter)),
                BiomeWeights = new float4(1f, 0f, 0f, 0f),
                Reserved = default
            };

            return true;
        }

        private static void UpsertProfile(NativeArray<WaterExtinctionProfileDTO> profiles, ref int profileCount, in WaterExtinctionProfileDTO profile)
        {
            int count = math.min(profileCount, profiles.Length);
            for (int i = 0; i < count; i++)
            {
                if (profiles[i].ProfileHash != profile.ProfileHash)
                    continue;

                profiles[i] = profile;
                return;
            }

            if (profileCount >= profiles.Length)
                return;

            profiles[profileCount] = profile;
            profileCount++;
        }

        private static ReadOnlySpan<byte> ReadColumn(ReadOnlySpan<byte> line, int columnIndex)
        {
            int start = 0;
            int column = 0;
            for (int i = 0; i <= line.Length; i++)
            {
                bool atEnd = i == line.Length;
                if (!atEnd && line[i] != (byte)',')
                    continue;

                if (column == columnIndex)
                    return Trim(line.Slice(start, i - start));

                start = i + 1;
                column++;
            }

            return ReadOnlySpan<byte>.Empty;
        }

        private static uint HashKey(ReadOnlySpan<byte> bytes)
        {
            bytes = Trim(bytes);
            uint hash = FnvOffset;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                hash = (hash ^ b) * FnvPrime;
            }

            return hash;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> bytes, out float value)
        {
            value = 0f;
            bytes = Trim(bytes);
            if (bytes.IsEmpty)
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
                index++;
                hasDigit = true;
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
                    index++;
                    hasDigit = true;
                }
            }

            if (!hasDigit)
                return false;

            value = integer + fraction / divisor;
            if (negative)
                value = -value;

            return math.isfinite(value);
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> bytes)
        {
            int start = 0;
            int end = bytes.Length;
            while (start < end && IsAsciiWhitespace(bytes[start]))
                start++;
            while (end > start && IsAsciiWhitespace(bytes[end - 1]))
                end--;
            return bytes.Slice(start, end - start);
        }

        private static bool IsAsciiWhitespace(byte value)
        {
            return value == (byte)' ' ||
                   value == (byte)'\t' ||
                   value == (byte)'\r' ||
                   value == (byte)'\n';
        }
    }
}
