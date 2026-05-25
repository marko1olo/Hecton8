// ============================================================================
// HECTON-8 - AirlockPressurizationCsv.cs
// SHINOBU_338 cold-boot allocation-free CSV parser for airlock hardware profiles.
// ============================================================================

#if UNITY_EDITOR
using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Gameplay.AirlockPressurization
{
    public static class AirlockPressurizationCsv
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static int ParseHardwareProfiles(
            ReadOnlySpan<byte> csvBytes,
            NativeArray<AirlockHardwareProfileDTO> profiles,
            out uint parseFaultMask)
        {
            parseFaultMask = 0u;
            if (!profiles.IsCreated || profiles.Length <= 0)
                return 0;

            int offset = 0;
            int written = 0;
            int length = csvBytes.Length;
            while (offset < length && written < profiles.Length)
            {
                SkipLineSpace(csvBytes, ref offset);
                if (offset >= length)
                    break;

                if (IsHeaderLine(csvBytes, offset))
                {
                    SkipLine(csvBytes, ref offset);
                    continue;
                }

                uint profileHash = ParseNameHash(csvBytes, ref offset);
                SkipComma(csvBytes, ref offset);
                bool volumeOk = TryParseFloat(csvBytes, ref offset, out float chamberVolumeLiters);
                SkipComma(csvBytes, ref offset);
                bool maxWaterOk = TryParseFloat(csvBytes, ref offset, out float maxWaterVolumeLiters);
                SkipComma(csvBytes, ref offset);
                bool pumpOk = TryParseFloat(csvBytes, ref offset, out float pumpSpeedLps);
                SkipComma(csvBytes, ref offset);
                bool exponentOk = TryParseFloat(csvBytes, ref offset, out float exponent);
                SkipComma(csvBytes, ref offset);
                bool powerOk = TryParseFloat(csvBytes, ref offset, out float powerWatts);
                SkipComma(csvBytes, ref offset);
                bool breachOk = TryParseFloat(csvBytes, ref offset, out float breachAreaM2);

                if (profileHash == 0u || !volumeOk || !maxWaterOk || !pumpOk || !exponentOk || !powerOk || !breachOk)
                {
                    parseFaultMask |= 1u;
                    SkipLine(csvBytes, ref offset);
                    continue;
                }

                profiles[written++] = new AirlockHardwareProfileDTO
                {
                    ProfileHash = profileHash,
                    ChamberVolumeLiters = math.max(1f, chamberVolumeLiters),
                    MaxWaterVolumeLiters = math.max(0f, maxWaterVolumeLiters),
                    PumpEvacuationSpeedLps = math.max(0f, pumpSpeedLps),
                    EqualizationCurveExponent = math.max(0.25f, exponent),
                    PowerDrawWatts = math.max(0f, powerWatts),
                    BreachAreaM2 = math.max(0f, breachAreaM2),
                    Flags = 0u
                };

                SkipLine(csvBytes, ref offset);
            }

            return written;
        }

        private static bool IsHeaderLine(ReadOnlySpan<byte> bytes, int offset)
        {
            if (offset >= bytes.Length)
                return false;

            byte c = bytes[offset];
            return c == (byte)'#' ||
                   c == (byte)'/' ||
                   c == (byte)'n' ||
                   c == (byte)'N';
        }

        private static uint ParseNameHash(ReadOnlySpan<byte> bytes, ref int offset)
        {
            uint hash = FnvOffset;
            bool sawAny = false;
            while (offset < bytes.Length)
            {
                byte c = bytes[offset];
                if (c == (byte)',' || c == (byte)'\r' || c == (byte)'\n')
                    break;

                if (c > (byte)' ')
                {
                    sawAny = true;
                    if (c >= (byte)'A' && c <= (byte)'Z')
                        c = (byte)(c + 32);
                    hash = (hash ^ c) * FnvPrime;
                }

                offset++;
            }

            return sawAny ? hash : 0u;
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
                if (c == (byte)',' || c == (byte)'\r' || c == (byte)'\n')
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
            return sawDigit && math.isfinite(value);
        }

        private static void SkipComma(ReadOnlySpan<byte> bytes, ref int offset)
        {
            while (offset < bytes.Length && bytes[offset] != (byte)',' && bytes[offset] != (byte)'\r' && bytes[offset] != (byte)'\n')
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
            if (offset < bytes.Length && bytes[offset] == (byte)'\n')
                offset++;
        }
    }
}
#endif
