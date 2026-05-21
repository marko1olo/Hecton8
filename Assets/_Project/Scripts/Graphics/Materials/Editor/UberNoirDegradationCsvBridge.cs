using System;
using System.IO;

namespace Hecton8.Graphics.Materials.Editor
{
    internal static class UberNoirDegradationCsvBridge
    {
        private const string RelativePath = "Data/Visuals/environmental_degradation_rules.csv";
        private const int ScratchBytes = 4096;
        private static readonly byte[] s_scratch = new byte[ScratchBytes];

        public static bool TryReload()
        {
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            string path = Path.Combine(root, RelativePath);
            if (!File.Exists(path))
                return false;

            int byteCount = ReadFileIntoScratch(path);
            if (byteCount <= 0)
                return false;

            VisualPressureAgingRuntime.TryReadEditorTuning(
                out VisualAgingTuningDTO tuning,
                out _,
                out _,
                out _);

            if (!ParseCsv(new ReadOnlySpan<byte>(s_scratch, 0, byteCount), ref tuning))
                return false;

            return VisualPressureAgingRuntime.TryWriteEditorTuning(
                tuning.RustStressMultiplier,
                tuning.CorrosionPressureMultiplier,
                tuning.SaltDepthMultiplier,
                tuning.BiomassTemperatureMultiplier,
                tuning.GlassFractureThreshold,
                tuning.TemperatureBoostMultiplier,
                tuning.QualityNoiseOctaveScale,
                tuning.ScorchIntensityMultiplier);
        }

        private static int ReadFileIntoScratch(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long length = stream.Length;
                if (length <= 0L || length > ScratchBytes)
                    return 0;

                int total = 0;
                while (total < length)
                {
                    int read = stream.Read(s_scratch, total, (int)length - total);
                    if (read <= 0)
                        break;

                    total += read;
                }

                return total == length ? total : 0;
            }
        }

        private static bool ParseCsv(ReadOnlySpan<byte> bytes, ref VisualAgingTuningDTO tuning)
        {
            bool parsed = false;
            int lineStart = 0;
            for (int i = 0; i <= bytes.Length; i++)
            {
                if (i < bytes.Length && bytes[i] != (byte)'\n')
                    continue;

                ReadOnlySpan<byte> line = Trim(bytes.Slice(lineStart, i - lineStart));
                lineStart = i + 1;
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                int comma = IndexOf(line, (byte)',');
                if (comma <= 0)
                    continue;

                ReadOnlySpan<byte> key = Trim(line.Slice(0, comma));
                ReadOnlySpan<byte> value = Trim(line.Slice(comma + 1));
                if (!TryParseFloat(value, out float parsedValue))
                    continue;

                parsed |= Apply(HashLowerFnv1A(key), parsedValue, ref tuning);
            }

            return parsed;
        }

        private static bool Apply(uint key, float value, ref VisualAgingTuningDTO tuning)
        {
            switch (key)
            {
                case 0x436ED2B4u: tuning.RustStressMultiplier = Max0(value); return true;
                case 0x67C02865u: tuning.CorrosionPressureMultiplier = Max0(value); return true;
                case 0xA173BDF3u: tuning.SaltDepthMultiplier = Max0(value); return true;
                case 0xBFF5E684u: tuning.BiomassTemperatureMultiplier = Max0(value); return true;
                case 0xEDD0017Fu: tuning.GlassFractureThreshold = Saturate(value); return true;
                case 0x1BC3EDBDu: tuning.TemperatureBoostMultiplier = Max0(value); return true;
                case 0x1ACB3DD7u: tuning.QualityNoiseOctaveScale = Saturate(value); return true;
                case 0xBEE9A39Bu: tuning.ScorchIntensityMultiplier = Max0(value); return true;
                case 0xFA06E7F3u: tuning.ScorchIntensityMultiplier = Max0(value); return true;
                default: return false;
            }
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> span)
        {
            int start = 0;
            int end = span.Length - 1;
            while (start <= end && IsWhitespace(span[start]))
                start++;
            while (end >= start && IsWhitespace(span[end]))
                end--;
            return start <= end ? span.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool IsWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r';
        }

        private static int IndexOf(ReadOnlySpan<byte> span, byte value)
        {
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] == value)
                    return i;
            }

            return -1;
        }

        private static uint HashLowerFnv1A(ReadOnlySpan<byte> span)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < span.Length; i++)
            {
                byte b = span[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                hash = (hash ^ b) * 16777619u;
            }

            return hash;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> span, out float value)
        {
            value = 0.0f;
            if (span.Length == 0)
                return false;

            int index = 0;
            float sign = 1.0f;
            if (span[index] == (byte)'-')
            {
                sign = -1.0f;
                index++;
            }
            else if (span[index] == (byte)'+')
            {
                index++;
            }

            float integer = 0.0f;
            bool hasDigit = false;
            while (index < span.Length && span[index] >= (byte)'0' && span[index] <= (byte)'9')
            {
                integer = integer * 10.0f + (span[index] - (byte)'0');
                index++;
                hasDigit = true;
            }

            float fraction = 0.0f;
            float scale = 1.0f;
            if (index < span.Length && span[index] == (byte)'.')
            {
                index++;
                while (index < span.Length && span[index] >= (byte)'0' && span[index] <= (byte)'9')
                {
                    fraction = fraction * 10.0f + (span[index] - (byte)'0');
                    scale *= 10.0f;
                    index++;
                    hasDigit = true;
                }
            }

            if (!hasDigit || index != span.Length)
                return false;

            value = sign * (integer + fraction / scale);
            return true;
        }

        private static float Max0(float value)
        {
            return value > 0.0f ? value : 0.0f;
        }

        private static float Saturate(float value)
        {
            if (value <= 0.0f)
                return 0.0f;
            return value >= 1.0f ? 1.0f : value;
        }
    }
}
