using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.VFX
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ParticleDataDTO
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float Lifetime;
        [FieldOffset(16)] public float3 Velocity;
        [FieldOffset(28)] public float Size;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ParticleRenderMetaDTO
    {
        [FieldOffset(0)] public float3 PreviousPosition;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float2 Uv;
        [FieldOffset(24)] public float2 Pad;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DynamicWakeDTO
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float Radius;
        [FieldOffset(16)] public float3 Force;
        [FieldOffset(28)] public float Falloff;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct MockWakeSignal
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float Radius;
        [FieldOffset(16)] public float3 Force;
        [FieldOffset(28)] public float Lifetime;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct MockAcousticSignal
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float Radius;
        [FieldOffset(16)] public float Magnitude;
        [FieldOffset(20)] public float StartTime;
        [FieldOffset(24)] public float Duration;
        [FieldOffset(28)] public float WaveSpeed;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MockFlowField
    {
        [FieldOffset(0)] public float3 GlobalFlow;
        [FieldOffset(12)] public float CurlStrength;
        [FieldOffset(16)] public float3 NoiseAnchor;
        [FieldOffset(28)] public float DensityScale;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VfxConfigurationDTO
    {
        [FieldOffset(0)] public int ParticleCount;
        [FieldOffset(4)] public float CurlNoiseStrength;
        [FieldOffset(8)] public float WakeInfluence;
        [FieldOffset(12)] public float GravitySinkingSpeed;
        [FieldOffset(16)] public float AmbientSize;
        [FieldOffset(20)] public float DensityScale;
        [FieldOffset(24)] public uint CsvProfileHash;
        [FieldOffset(28)] public uint Version;
    }

    public static class VolumetricSiltConfigurationAccess
    {
        public static unsafe ref VfxConfigurationDTO ElementAt(NativeArray<VfxConfigurationDTO> values, int index)
        {
            return ref ((VfxConfigurationDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(values))[index];
        }

        public static VfxConfigurationDTO CreateDefault(int particleCount)
        {
            return new VfxConfigurationDTO
            {
                ParticleCount = math.max(64, particleCount),
                CurlNoiseStrength = 0.15f,
                WakeInfluence = 1f,
                GravitySinkingSpeed = 1f,
                AmbientSize = 0.006f,
                DensityScale = 1f,
                CsvProfileHash = 0u,
                Version = 1u
            };
        }
    }

    public static class VolumetricSiltCsvParser
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const uint ParticleCountHash = 0x0E155D2Fu;
        private const uint CurlNoiseStrengthHash = 0x8B013DD6u;
        private const uint WakeInfluenceHash = 0x16CBAA19u;
        private const uint GravitySinkingSpeedHash = 0x7B9F9A07u;
        private const uint AmbientSiltSizeHash = 0x97AA3658u;
        private const uint DensityScaleHash = 0x68EE7C66u;

        public static bool TryParse(byte[] bytes, int length, ref VfxConfigurationDTO tuning, out uint fileHash)
        {
            fileHash = FnvOffset;
            if (bytes == null || length <= 0)
                return false;

            int clampedLength = math.min(length, bytes.Length);
            int lineStart = 0;
            bool changed = false;
            for (int i = 0; i <= clampedLength; i++)
            {
                if (i < clampedLength && bytes[i] != (byte)'\n')
                {
                    fileHash = (fileHash ^ bytes[i]) * FnvPrime;
                    continue;
                }

                changed |= TryParseLine(bytes, lineStart, i - lineStart, ref tuning);
                lineStart = i + 1;
            }

            if (changed)
            {
                tuning.CsvProfileHash = fileHash;
                tuning.Version = tuning.Version == uint.MaxValue ? 1u : tuning.Version + 1u;
            }

            return changed;
        }

        private static bool TryParseLine(byte[] bytes, int start, int length, ref VfxConfigurationDTO tuning)
        {
            int end = TrimLineEnd(bytes, start, length);
            start = TrimLineStart(bytes, start, end);
            if (end <= start || bytes[start] == (byte)'#')
                return false;

            int comma = -1;
            for (int i = start; i < end; i++)
            {
                if (bytes[i] == (byte)',' || bytes[i] == (byte)'=')
                {
                    comma = i;
                    break;
                }
            }

            if (comma <= start)
                return false;

            uint keyHash = HashKey(bytes, start, comma - start);
            if (!TryParseFloat(bytes, comma + 1, end - comma - 1, out float value))
                return false;

            switch (keyHash)
            {
                case ParticleCountHash:
                    tuning.ParticleCount = math.clamp((int)(value + 0.5f), 64, 100000);
                    return true;
                case CurlNoiseStrengthHash:
                    tuning.CurlNoiseStrength = math.clamp(value, 0f, 4f);
                    return true;
                case WakeInfluenceHash:
                    tuning.WakeInfluence = math.clamp(value, 0f, 4f);
                    return true;
                case GravitySinkingSpeedHash:
                    tuning.GravitySinkingSpeed = math.clamp(value, 0.05f, 6f);
                    return true;
                case AmbientSiltSizeHash:
                    tuning.AmbientSize = math.clamp(value, 0.0005f, 0.03f);
                    return true;
                case DensityScaleHash:
                    tuning.DensityScale = math.clamp(value, 0f, 3f);
                    return true;
                default:
                    return false;
            }
        }

        private static int TrimLineStart(byte[] bytes, int start, int end)
        {
            while (start < end && IsAsciiWhitespace(bytes[start]))
                start++;
            return start;
        }

        private static int TrimLineEnd(byte[] bytes, int start, int length)
        {
            int end = start + math.max(0, length);
            while (end > start && IsAsciiWhitespace(bytes[end - 1]))
                end--;
            return end;
        }

        private static uint HashKey(byte[] bytes, int start, int length)
        {
            uint hash = FnvOffset;
            int end = start + math.max(0, length);
            while (start < end && IsAsciiWhitespace(bytes[start]))
                start++;
            while (end > start && IsAsciiWhitespace(bytes[end - 1]))
                end--;

            for (int i = start; i < end; i++)
            {
                byte b = bytes[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                hash = (hash ^ b) * FnvPrime;
            }

            return hash;
        }

        private static bool TryParseFloat(byte[] bytes, int start, int length, out float value)
        {
            value = 0f;
            int end = TrimLineEnd(bytes, start, length);
            start = TrimLineStart(bytes, start, end);
            if (end <= start)
                return false;

            bool negative = false;
            if (bytes[start] == (byte)'-')
            {
                negative = true;
                start++;
            }
            else if (bytes[start] == (byte)'+')
            {
                start++;
            }

            float integer = 0f;
            bool hasDigit = false;
            while (start < end)
            {
                byte b = bytes[start];
                if (b < (byte)'0' || b > (byte)'9')
                    break;

                integer = integer * 10f + (b - (byte)'0');
                start++;
                hasDigit = true;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (start < end && bytes[start] == (byte)'.')
            {
                start++;
                while (start < end)
                {
                    byte b = bytes[start];
                    if (b < (byte)'0' || b > (byte)'9')
                        break;

                    fraction = fraction * 10f + (b - (byte)'0');
                    divisor *= 10f;
                    start++;
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

        private static bool IsAsciiWhitespace(byte value)
        {
            return value == (byte)' ' ||
                value == (byte)'\t' ||
                value == (byte)'\r' ||
                value == (byte)'\n';
        }
    }
}
