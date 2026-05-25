using System;
using Hecton8.Core.Contracts.Physics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

#if UNITY_EDITOR
namespace Hecton8.Physics
{
    public static unsafe class HabitatFluidIncursionCsv
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static int ParseCompartmentVolumesCsv(
            NativeArray<byte> csvBytes,
            int byteCount,
            NativeArray<FluidCompartmentDTO> compartments,
            int compartmentCount)
        {
            if (!csvBytes.IsCreated || !compartments.IsCreated)
                return 0;

            int safeBytes = math.min(math.max(0, byteCount), csvBytes.Length);
            int safeCount = math.min(math.max(0, compartmentCount), compartments.Length);
            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(csvBytes);
            int offset = 0;
            int applied = 0;

            while (offset < safeBytes)
            {
                SkipLineSpace(ptr, safeBytes, ref offset);
                if (offset >= safeBytes)
                    break;

                uint nodeHash = ParseNodeHash(ptr, safeBytes, ref offset);
                SkipUntilValue(ptr, safeBytes, ref offset);
                if (!TryParseFloat(ptr, safeBytes, ref offset, out float maxVolume))
                {
                    SkipLine(ptr, safeBytes, ref offset);
                    continue;
                }

                if (nodeHash != 0u && math.isfinite(maxVolume) && maxVolume > 0f)
                {
                    for (int i = 0; i < safeCount; i++)
                    {
                        FluidCompartmentDTO dto = compartments[i];
                        if (dto.NodeHashID != nodeHash)
                            continue;

                        dto.MaxWaterVolume = maxVolume;
                        dto.CurrentWaterVolume = math.min(dto.CurrentWaterVolume, maxVolume);
                        dto.WaterLevelHeight01 = dto.MaxWaterVolume > HabitatFluidIncursionConstants.WaterEpsilonM3
                            ? math.saturate(dto.CurrentWaterVolume * math.rcp(dto.MaxWaterVolume))
                            : 0f;
                        compartments[i] = dto;
                        applied++;
                        break;
                    }
                }

                SkipLine(ptr, safeBytes, ref offset);
            }

            return applied;
        }

        public static int ParseCompartmentVolumeTableCsv(
            NativeArray<byte> csvBytes,
            int byteCount,
            NativeParallelHashMap<uint, float> volumeByModuleHash)
        {
            if (!csvBytes.IsCreated || !volumeByModuleHash.IsCreated)
                return 0;

            int safeBytes = math.min(math.max(0, byteCount), csvBytes.Length);
            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(csvBytes);
            int offset = 0;
            int applied = 0;

            while (offset < safeBytes)
            {
                SkipLineSpace(ptr, safeBytes, ref offset);
                if (offset >= safeBytes)
                    break;

                uint nodeHash = ParseNodeHash(ptr, safeBytes, ref offset);
                SkipUntilValue(ptr, safeBytes, ref offset);
                if (TryParseFloat(ptr, safeBytes, ref offset, out float maxVolume) &&
                    nodeHash != 0u &&
                    math.isfinite(maxVolume) &&
                    maxVolume > 0f)
                {
                    if (!volumeByModuleHash.TryAdd(nodeHash, maxVolume))
                        volumeByModuleHash[nodeHash] = maxVolume;
                    applied++;
                }

                SkipLine(ptr, safeBytes, ref offset);
            }

            return applied;
        }

        public static int ParseCompartmentVolumeTableCsv(
            ReadOnlySpan<byte> csvBytes,
            NativeParallelHashMap<uint, float> volumeByModuleHash)
        {
            if (!volumeByModuleHash.IsCreated)
                return 0;

            int offset = 0;
            int applied = 0;
            int safeBytes = csvBytes.Length;
            while (offset < safeBytes)
            {
                SkipLineSpace(csvBytes, ref offset);
                if (offset >= safeBytes)
                    break;

                uint nodeHash = ParseNodeHash(csvBytes, ref offset);
                SkipUntilValue(csvBytes, ref offset);
                if (TryParseFloat(csvBytes, ref offset, out float maxVolume) &&
                    nodeHash != 0u &&
                    math.isfinite(maxVolume) &&
                    maxVolume > 0f)
                {
                    if (!volumeByModuleHash.TryAdd(nodeHash, maxVolume))
                        volumeByModuleHash[nodeHash] = maxVolume;
                    applied++;
                }

                SkipLine(csvBytes, ref offset);
            }

            return applied;
        }

        private static uint ParseNodeHash(byte* ptr, int length, ref int offset)
        {
            uint numeric = 0u;
            uint hash = FnvOffset;
            bool sawDigit = false;
            bool sawAlpha = false;

            while (offset < length)
            {
                byte c = ptr[offset];
                if (c == (byte)',' || c == (byte)'\n' || c == (byte)'\r')
                    break;

                if (c >= (byte)'0' && c <= (byte)'9')
                {
                    sawDigit = true;
                    numeric = (numeric * 10u) + (uint)(c - (byte)'0');
                }
                else if (c > (byte)' ')
                {
                    sawAlpha = true;
                }

                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash = (hash ^ c) * FnvPrime;
                offset++;
            }

            return sawDigit && !sawAlpha ? numeric : hash;
        }

        private static bool TryParseFloat(byte* ptr, int length, ref int offset, out float value)
        {
            value = 0f;
            float sign = 1f;
            float scale = 1f;
            bool fractional = false;
            bool sawDigit = false;

            SkipLineSpace(ptr, length, ref offset);
            if (offset < length && ptr[offset] == (byte)'-')
            {
                sign = -1f;
                offset++;
            }

            while (offset < length)
            {
                byte c = ptr[offset];
                if (c == (byte)',' || c == (byte)'\n' || c == (byte)'\r')
                    break;
                if (c == (byte)'.')
                {
                    fractional = true;
                    offset++;
                    continue;
                }
                if (c < (byte)'0' || c > (byte)'9')
                {
                    offset++;
                    continue;
                }

                sawDigit = true;
                int digit = c - (byte)'0';
                if (fractional)
                {
                    scale *= 0.1f;
                    value += digit * scale;
                }
                else
                {
                    value = (value * 10f) + digit;
                }

                offset++;
            }

            value *= sign;
            return sawDigit;
        }

        private static void SkipUntilValue(byte* ptr, int length, ref int offset)
        {
            while (offset < length && ptr[offset] != (byte)',')
                offset++;
            if (offset < length && ptr[offset] == (byte)',')
                offset++;
        }

        private static void SkipLineSpace(byte* ptr, int length, ref int offset)
        {
            while (offset < length)
            {
                byte c = ptr[offset];
                if (c != (byte)' ' && c != (byte)'\t')
                    return;
                offset++;
            }
        }

        private static void SkipLine(byte* ptr, int length, ref int offset)
        {
            while (offset < length && ptr[offset] != (byte)'\n')
                offset++;
            if (offset < length)
                offset++;
        }

        private static uint ParseNodeHash(ReadOnlySpan<byte> bytes, ref int offset)
        {
            uint numeric = 0u;
            uint hash = FnvOffset;
            bool sawDigit = false;
            bool sawAlpha = false;

            while (offset < bytes.Length)
            {
                byte c = bytes[offset];
                if (c == (byte)',' || c == (byte)'\n' || c == (byte)'\r')
                    break;

                if (c >= (byte)'0' && c <= (byte)'9')
                {
                    sawDigit = true;
                    numeric = (numeric * 10u) + (uint)(c - (byte)'0');
                }
                else if (c > (byte)' ')
                {
                    sawAlpha = true;
                }

                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash = (hash ^ c) * FnvPrime;
                offset++;
            }

            return sawDigit && !sawAlpha ? numeric : hash;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> bytes, ref int offset, out float value)
        {
            value = 0f;
            float sign = 1f;
            float scale = 1f;
            bool fractional = false;
            bool sawDigit = false;

            SkipLineSpace(bytes, ref offset);
            if (offset < bytes.Length && bytes[offset] == (byte)'-')
            {
                sign = -1f;
                offset++;
            }

            while (offset < bytes.Length)
            {
                byte c = bytes[offset];
                if (c == (byte)',' || c == (byte)'\n' || c == (byte)'\r')
                    break;
                if (c == (byte)'.')
                {
                    fractional = true;
                    offset++;
                    continue;
                }
                if (c < (byte)'0' || c > (byte)'9')
                {
                    offset++;
                    continue;
                }

                sawDigit = true;
                int digit = c - (byte)'0';
                if (fractional)
                {
                    scale *= 0.1f;
                    value += digit * scale;
                }
                else
                {
                    value = (value * 10f) + digit;
                }

                offset++;
            }

            value *= sign;
            return sawDigit;
        }

        private static void SkipUntilValue(ReadOnlySpan<byte> bytes, ref int offset)
        {
            while (offset < bytes.Length && bytes[offset] != (byte)',')
                offset++;
            if (offset < bytes.Length && bytes[offset] == (byte)',')
                offset++;
        }

        private static void SkipLineSpace(ReadOnlySpan<byte> bytes, ref int offset)
        {
            while (offset < bytes.Length)
            {
                byte c = bytes[offset];
                if (c != (byte)' ' && c != (byte)'\t')
                    return;
                offset++;
            }
        }

        private static void SkipLine(ReadOnlySpan<byte> bytes, ref int offset)
        {
            while (offset < bytes.Length && bytes[offset] != (byte)'\n')
                offset++;
            if (offset < bytes.Length)
                offset++;
        }
    }
}
#endif
