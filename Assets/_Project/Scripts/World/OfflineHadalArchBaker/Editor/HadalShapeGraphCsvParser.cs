using System;
using System.Collections.Generic;
using System.IO;
using Hecton8.World.OfflineHadalArchBaker;
using Unity.Mathematics;

namespace Hecton8.World.OfflineHadalArchBaker.Editor
{
    public static class HadalShapeGraphCsvParser
    {
        public static bool TryLoad(string path, string recipeName, List<SdfShapeDTO> output, out uint schemaHash)
        {
            schemaHash = 2166136261u;
            output.Clear();
            if (!File.Exists(path))
                return false;

            string text = File.ReadAllText(path);
            ReadOnlySpan<char> data = text.AsSpan();
            ReadOnlySpan<char> recipeFilter = recipeName == null ? ReadOnlySpan<char>.Empty : recipeName.AsSpan();
            int lineStart = 0;
            int row = 0;
            while (lineStart < data.Length)
            {
                int lineEnd = lineStart;
                while (lineEnd < data.Length && data[lineEnd] != '\n')
                    lineEnd++;

                ReadOnlySpan<char> line = data.Slice(lineStart, lineEnd - lineStart).Trim();
                lineStart = lineEnd + 1;
                row++;
                if (line.Length == 0 || line[0] == '#')
                    continue;

                if (row == 1 && StartsWithAscii(line, "recipe"))
                {
                    schemaHash = HashLine(line, schemaHash);
                    continue;
                }

                if (TryParseLine(line, recipeFilter, out SdfShapeDTO shape))
                    output.Add(shape);
            }

            return output.Count > 0;
        }

        private static bool TryParseLine(ReadOnlySpan<char> line, ReadOnlySpan<char> recipeFilter, out SdfShapeDTO shape)
        {
            shape = default;
            ReadOnlySpan<char> recipe = GetCell(line, 0);
            if (!recipeFilter.IsEmpty && !recipe.Equals(recipeFilter, StringComparison.OrdinalIgnoreCase))
                return false;

            ReadOnlySpan<char> type = GetCell(line, 1);
            ReadOnlySpan<char> op = GetCell(line, 2);
            if (!TryParseFloat(GetCell(line, 3), out float px) ||
                !TryParseFloat(GetCell(line, 4), out float py) ||
                !TryParseFloat(GetCell(line, 5), out float pz) ||
                !TryParseFloat(GetCell(line, 6), out float ex) ||
                !TryParseFloat(GetCell(line, 7), out float ey) ||
                !TryParseFloat(GetCell(line, 8), out float ez))
            {
                return false;
            }

            TryParseFloat(GetCell(line, 9), out float blend);
            TryParseFloat(GetCell(line, 10), out float noiseWeight);
            shape = new SdfShapeDTO
            {
                ShapeType = ResolveShapeType(type),
                Operation = ResolveOperation(op),
                Position = new float3(px, py, pz),
                Extents = new float3(math.max(ex, 0.001f), math.max(ey, 0.001f), math.max(ez, 0.001f)),
                BlendRadius = math.max(blend, 0f),
                NoiseWeight = math.max(noiseWeight, 0f),
                Flags = 0u,
                MaterialHash = HashCell(recipe),
                _pad0 = 0UL,
                _pad1 = 0UL
            };
            return true;
        }

        private static ReadOnlySpan<char> GetCell(ReadOnlySpan<char> line, int targetColumn)
        {
            int column = 0;
            int start = 0;
            for (int i = 0; i <= line.Length; i++)
            {
                if (i != line.Length && line[i] != ',')
                    continue;

                if (column == targetColumn)
                    return line.Slice(start, i - start).Trim();

                start = i + 1;
                column++;
            }

            return ReadOnlySpan<char>.Empty;
        }

        private static bool TryParseFloat(ReadOnlySpan<char> span, out float value)
        {
            value = 0f;
            span = span.Trim();
            if (span.Length == 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (span[index] == '-' || span[index] == '+')
            {
                sign = span[index] == '-' ? -1f : 1f;
                index++;
            }

            double integer = 0d;
            bool anyDigit = false;
            while (index < span.Length && span[index] >= '0' && span[index] <= '9')
            {
                anyDigit = true;
                integer = (integer * 10d) + (span[index] - '0');
                index++;
            }

            double fraction = 0d;
            double divisor = 1d;
            if (index < span.Length && span[index] == '.')
            {
                index++;
                while (index < span.Length && span[index] >= '0' && span[index] <= '9')
                {
                    anyDigit = true;
                    fraction = (fraction * 10d) + (span[index] - '0');
                    divisor *= 10d;
                    index++;
                }
            }

            if (!anyDigit)
                return false;

            int exponent = 0;
            if (index < span.Length && (span[index] == 'e' || span[index] == 'E'))
            {
                index++;
                int exponentSign = 1;
                if (index < span.Length && (span[index] == '-' || span[index] == '+'))
                {
                    exponentSign = span[index] == '-' ? -1 : 1;
                    index++;
                }

                bool exponentDigit = false;
                while (index < span.Length && span[index] >= '0' && span[index] <= '9')
                {
                    exponentDigit = true;
                    exponent = (exponent * 10) + (span[index] - '0');
                    index++;
                }

                if (!exponentDigit)
                    return false;
                exponent *= exponentSign;
            }

            if (index != span.Length)
                return false;

            double parsed = (integer + (fraction / divisor)) * sign;
            if (exponent != 0)
                parsed = ScaleByFloatPow10(parsed, exponent);

            if (parsed > float.MaxValue || parsed < -float.MaxValue)
                return false;

            value = (float)parsed;
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static double ScaleByFloatPow10(double value, int exponent)
        {
            if (value == 0d || exponent == 0)
                return value;
            if (exponent > 38)
                return value > 0d ? double.PositiveInfinity : double.NegativeInfinity;
            if (exponent < -46)
                return 0d;

            int count = exponent < 0 ? -exponent : exponent;
            double scale = 1d;
            for (int i = 0; i < count; i++)
                scale *= 10d;

            return exponent < 0 ? value / scale : value * scale;
        }

        private static uint ResolveShapeType(ReadOnlySpan<char> value)
        {
            if (value.Equals("box".AsSpan(), StringComparison.OrdinalIgnoreCase))
                return (uint)SdfShapeType.Box;
            if (value.Equals("torus".AsSpan(), StringComparison.OrdinalIgnoreCase))
                return (uint)SdfShapeType.Torus;
            if (value.Equals("cylinder".AsSpan(), StringComparison.OrdinalIgnoreCase))
                return (uint)SdfShapeType.Cylinder;
            return (uint)SdfShapeType.Sphere;
        }

        private static uint ResolveOperation(ReadOnlySpan<char> value)
        {
            if (value.Equals("subtract".AsSpan(), StringComparison.OrdinalIgnoreCase))
                return (uint)SdfBooleanOperation.Subtract;
            if (value.Equals("intersect".AsSpan(), StringComparison.OrdinalIgnoreCase))
                return (uint)SdfBooleanOperation.Intersect;
            if (value.Equals("smooth".AsSpan(), StringComparison.OrdinalIgnoreCase) || value.Equals("smooth_union".AsSpan(), StringComparison.OrdinalIgnoreCase))
                return (uint)SdfBooleanOperation.SmoothUnion;
            return (uint)SdfBooleanOperation.Add;
        }

        private static bool StartsWithAscii(ReadOnlySpan<char> value, string prefix)
        {
            return value.StartsWith(prefix.AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

        private static uint HashLine(ReadOnlySpan<char> value, uint hash)
        {
            for (int i = 0; i < value.Length; i++)
                hash = HadalArchBakeMath.HashBytes((byte)value[i], hash);
            return hash;
        }

        private static uint HashCell(ReadOnlySpan<char> value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
                hash = HadalArchBakeMath.HashBytes((byte)value[i], hash);
            return hash == 0u ? 1u : hash;
        }
    }
}
