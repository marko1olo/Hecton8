using System;
using System.IO;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Cold-tick CSV override path for human-authored botany rules. Parsing is byte-based and allocation-free after caller-owned scratch exists.
    /// </summary>
    public static unsafe class FloraGenomeCsvHotloader
    {
        private const byte Comma = (byte)',';
        private const byte CarriageReturn = (byte)'\r';
        private const byte LineFeed = (byte)'\n';
        private const byte Comment = (byte)'#';

        public static bool TryApplyOverrides(
            string csvPath,
            NativeArray<byte> scratchBytes,
            NativeArray<FloraGenomeDTO> genomes,
            ref long lastWriteUtcTicks,
            out int updatedCount)
        {
            updatedCount = 0;
            if (string.IsNullOrEmpty(csvPath) || !scratchBytes.IsCreated || !genomes.IsCreated || scratchBytes.Length <= 0)
                return false;

            FileInfo fileInfo = new FileInfo(csvPath);
            if (!fileInfo.Exists)
                return false;

            long writeTicks = fileInfo.LastWriteTimeUtc.Ticks;
            if (writeTicks == lastWriteUtcTicks)
                return false;

            int byteCount = TryReadFile(csvPath, scratchBytes);
            if (byteCount <= 0)
                return false;

            byte* bytes = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratchBytes);
            int cursor = 0;
            while (cursor < byteCount)
            {
                int lineStart = cursor;
                while (cursor < byteCount && bytes[cursor] != LineFeed)
                    cursor++;

                int lineEnd = cursor;
                if (cursor < byteCount && bytes[cursor] == LineFeed)
                    cursor++;

                while (lineEnd > lineStart && bytes[lineEnd - 1] == CarriageReturn)
                    lineEnd--;

                if (TryApplyLine(bytes, lineStart, lineEnd, genomes))
                    updatedCount++;
            }

            lastWriteUtcTicks = writeTicks;
            return updatedCount > 0;
        }

        private static int TryReadFile(string csvPath, NativeArray<byte> scratchBytes)
        {
            try
            {
                using FileStream stream = new FileStream(
                    csvPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    bufferSize: 64 * 1024,
                    options: FileOptions.SequentialScan);

                if (stream.Length <= 0L || stream.Length > scratchBytes.Length)
                    return 0;

                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratchBytes);
                Span<byte> span = new Span<byte>(ptr, (int)stream.Length);
                int totalRead = 0;
                while (totalRead < span.Length)
                {
                    int read = stream.Read(span.Slice(totalRead));
                    if (read <= 0)
                        break;

                    totalRead += read;
                }

                return totalRead == span.Length ? totalRead : 0;
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
        }

        private static bool TryApplyLine(byte* bytes, int start, int end, NativeArray<FloraGenomeDTO> genomes)
        {
            if (end <= start)
                return false;

            byte first = bytes[start];
            if (first == Comment || first == (byte)'S' || first == (byte)'s')
                return false;

            int tokenStart = start;
            int tokenIndex = 0;
            uint speciesHash = 0u;
            FixedString32Bytes axiom = default;
            float branchAngleRadians = 0f;
            float baseScale = 0f;
            float segmentLength = 0f;
            float biolumThreshold = 0f;
            uint color = 0u;
            uint traits = 0u;
            byte maxIterations = 0;
            byte ruleProfile = 0;
            byte hazardFlags = 0;

            for (int i = start; i <= end; i++)
            {
                bool atEnd = i == end;
                if (!atEnd && bytes[i] != Comma)
                    continue;

                int tokenEnd = TrimRight(bytes, tokenStart, i);
                int trimmedStart = TrimLeft(bytes, tokenStart, tokenEnd);
                switch (tokenIndex)
                {
                    case 0:
                        if (!TryParseUInt(bytes, trimmedStart, tokenEnd, out speciesHash))
                            return false;
                        break;
                    case 1:
                        CopyAxiom(bytes, trimmedStart, tokenEnd, ref axiom);
                        break;
                    case 2:
                        if (!TryParseFloat(bytes, trimmedStart, tokenEnd, out float angleDeg))
                            return false;
                        branchAngleRadians = math.radians(angleDeg);
                        break;
                    case 3:
                        TryParseFloat(bytes, trimmedStart, tokenEnd, out baseScale);
                        break;
                    case 4:
                        TryParseFloat(bytes, trimmedStart, tokenEnd, out segmentLength);
                        break;
                    case 5:
                        TryParseFloat(bytes, trimmedStart, tokenEnd, out biolumThreshold);
                        break;
                    case 6:
                        TryParseUInt(bytes, trimmedStart, tokenEnd, out color);
                        break;
                    case 7:
                        TryParseUInt(bytes, trimmedStart, tokenEnd, out traits);
                        break;
                    case 8:
                        TryParseUInt(bytes, trimmedStart, tokenEnd, out uint iterations);
                        maxIterations = (byte)math.clamp((int)iterations, 1, FloraGenomeLSystemConstants.MaxRuntimeIterations);
                        break;
                    case 9:
                        TryParseUInt(bytes, trimmedStart, tokenEnd, out uint profile);
                        ruleProfile = (byte)math.clamp((int)profile, 0, 2);
                        break;
                    case 10:
                        TryParseUInt(bytes, trimmedStart, tokenEnd, out uint hazard);
                        hazardFlags = (byte)(hazard & 0xFFu);
                        break;
                }

                tokenIndex++;
                tokenStart = i + 1;
            }

            if (speciesHash == 0u || axiom.Length <= 0)
                return false;

            int genomeIndex = FindGenomeIndex(genomes, speciesHash);
            if (genomeIndex < 0)
                genomeIndex = FindGenomeIndex(genomes, 0u);
            if (genomeIndex < 0)
                return false;

            FloraGenomeDTO genome = genomes[genomeIndex];
            genome.SpeciesHash = speciesHash;
            genome.Axiom = axiom;
            if (math.isfinite(branchAngleRadians) && math.abs(branchAngleRadians) > 0.0001f)
                genome.BranchAngleRadians = branchAngleRadians;
            if (math.isfinite(baseScale) && baseScale > 0f)
                genome.BaseScale = baseScale;
            if (math.isfinite(segmentLength) && segmentLength > 0f)
                genome.SegmentLengthMeters = segmentLength;
            if (math.isfinite(biolumThreshold))
                genome.BiolumThreshold = biolumThreshold;
            if (color != 0u)
                genome.PackedColorHDR = color;
            if (traits != 0u)
                genome.TraitFlags = traits;
            if (maxIterations != 0)
                genome.MaxIterations = maxIterations;
            genome.RuleProfile = ruleProfile;
            genome.HazardFlags = hazardFlags;
            genome._pad0 = 0;
            genomes[genomeIndex] = genome;
            return true;
        }

        private static int FindGenomeIndex(NativeArray<FloraGenomeDTO> genomes, uint speciesHash)
        {
            for (int i = 0; i < genomes.Length; i++)
            {
                if (genomes[i].SpeciesHash == speciesHash)
                    return i;
            }

            return -1;
        }

        private static void CopyAxiom(byte* bytes, int start, int end, ref FixedString32Bytes axiom)
        {
            axiom.Clear();
            int limit = math.min(end, start + FixedString32Bytes.UTF8MaxLengthInBytes);
            for (int i = start; i < limit; i++)
            {
                byte value = bytes[i];
                if (value <= 32)
                    continue;

                axiom.Add(value);
            }
        }

        private static bool TryParseUInt(byte* bytes, int start, int end, out uint value)
        {
            value = 0u;
            if (end <= start)
                return false;

            int i = start;
            bool hex = false;
            if (i + 1 < end && bytes[i] == (byte)'0' && (bytes[i + 1] == (byte)'x' || bytes[i + 1] == (byte)'X'))
            {
                hex = true;
                i += 2;
            }

            for (; i < end; i++)
            {
                byte c = bytes[i];
                uint digit;
                if (c >= (byte)'0' && c <= (byte)'9')
                    digit = (uint)(c - (byte)'0');
                else if (hex && c >= (byte)'A' && c <= (byte)'F')
                    digit = 10u + (uint)(c - (byte)'A');
                else if (hex && c >= (byte)'a' && c <= (byte)'f')
                    digit = 10u + (uint)(c - (byte)'a');
                else
                    return false;

                value = hex ? ((value << 4) | digit) : ((value * 10u) + digit);
            }

            return true;
        }

        private static bool TryParseFloat(byte* bytes, int start, int end, out float value)
        {
            value = 0f;
            if (end <= start)
                return false;

            int i = start;
            float sign = 1f;
            if (bytes[i] == (byte)'-')
            {
                sign = -1f;
                i++;
            }

            float whole = 0f;
            while (i < end && bytes[i] >= (byte)'0' && bytes[i] <= (byte)'9')
            {
                whole = (whole * 10f) + (bytes[i] - (byte)'0');
                i++;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (i < end && bytes[i] == (byte)'.')
            {
                i++;
                while (i < end && bytes[i] >= (byte)'0' && bytes[i] <= (byte)'9')
                {
                    fraction = (fraction * 10f) + (bytes[i] - (byte)'0');
                    divisor *= 10f;
                    i++;
                }
            }

            if (i != end)
                return false;

            value = sign * (whole + (fraction / math.max(divisor, 1f)));
            return math.isfinite(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int TrimLeft(byte* bytes, int start, int end)
        {
            while (start < end && bytes[start] <= 32)
                start++;
            return start;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int TrimRight(byte* bytes, int start, int end)
        {
            while (end > start && bytes[end - 1] <= 32)
                end--;
            return end;
        }
    }
}
