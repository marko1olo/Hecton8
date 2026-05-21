#if UNITY_EDITOR
using System;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Editor.GeographySanity
{
    internal static class GeographySanityProfileCsv
    {
        private const int MaxCsvLineBytes = 1024;
        private const int MaxProfileRows = 2048;

        public static NativeList<SanityProfileDTO> LoadProfiles(Allocator allocator, out int rows, out int errors)
        {
            rows = 0;
            errors = 0;
            NativeList<SanityProfileDTO> profiles = new NativeList<SanityProfileDTO>(MaxProfileRows, allocator);
            string path = Path.Combine(ResolveProjectRoot(), GeographySanityConstants.ProfilesCsvPath);
            if (!File.Exists(path))
                return profiles;

            Span<byte> line = stackalloc byte[MaxCsvLineBytes];
            int lineLength = 0;
            int row = 0;
            bool overflow = false;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                while (true)
                {
                    int value = stream.ReadByte();
                    bool eof = value < 0;
                    if (!eof && value != (byte)'\n')
                    {
                        if (lineLength < line.Length)
                            line[lineLength++] = (byte)value;
                        else
                            overflow = true;
                        continue;
                    }

                    int end = lineLength;
                    if (end > 0 && line[end - 1] == (byte)'\r')
                        end--;

                    if (end > 0 || overflow)
                    {
                        if (row > 0)
                        {
                            if (!overflow && TryParseRow(line.Slice(0, end), row, out SanityProfileDTO profile))
                            {
                                if (profiles.Length < MaxProfileRows)
                                {
                                    profiles.Add(profile);
                                    rows++;
                                }
                                else
                                {
                                    errors++;
                                }
                            }
                            else
                            {
                                errors++;
                            }
                        }

                        row++;
                    }

                    lineLength = 0;
                    overflow = false;
                    if (eof)
                        break;
                }
            }

            return profiles;
        }

        private static bool TryParseRow(ReadOnlySpan<byte> line, int row, out SanityProfileDTO profile)
        {
            profile = default;
            if (!ReadField(ref line, out ReadOnlySpan<byte> type))
                return false;

            uint typeHash = HashField(type);
            if (!ReadField(ref line, out ReadOnlySpan<byte> maxFloatingToken) ||
                !TryParseFloat(maxFloatingToken, out float maxFloating))
                return false;

            if (!ReadField(ref line, out ReadOnlySpan<byte> clearanceToken) ||
                !TryParseFloat(clearanceToken, out float clearance))
                return false;

            if (!ReadField(ref line, out ReadOnlySpan<byte> recoverToken, out bool hasOptionalTail) ||
                !TryParseFloat(recoverToken, out float recover))
                return false;

            uint flags = GeographySanityConstants.RuleCheckFloating |
                         GeographySanityConstants.RuleCheckBuried |
                         GeographySanityConstants.RuleCheckCrushDepth;
            if (hasOptionalTail)
            {
                if (!ReadField(ref line, out ReadOnlySpan<byte> flagsToken, out bool hasTrailingColumn))
                    return false;

                if (!TryParseUInt(flagsToken, out uint parsedFlags) ||
                    !IsSupportedProfileRuleMask(parsedFlags))
                    return false;

                flags = parsedFlags;

                if (hasTrailingColumn || line.Length != 0)
                    return false;
            }

            profile.ObjectTypeHash = typeHash;
            profile.MaxFloatingDistance = math.max(0.01f, maxFloating);
            profile.RequiredClearance = math.max(0f, clearance);
            profile.RecoverableEpsilon = math.max(0f, recover);
            profile.RuleFlags = flags;
            profile.RowIndex = (uint)row;
            return typeHash != 0u;
        }

        private static bool ReadField(ref ReadOnlySpan<byte> line, out ReadOnlySpan<byte> field)
        {
            return ReadField(ref line, out field, out _);
        }

        private static bool ReadField(ref ReadOnlySpan<byte> line, out ReadOnlySpan<byte> field, out bool hadSeparator)
        {
            int comma = line.IndexOf((byte)',');
            if (comma < 0)
            {
                field = Trim(line);
                line = ReadOnlySpan<byte>.Empty;
                hadSeparator = false;
                return field.Length > 0;
            }

            field = Trim(line.Slice(0, comma));
            line = line.Slice(comma + 1);
            hadSeparator = true;
            return field.Length > 0;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> bytes, out float value)
        {
            value = 0f;
            if (bytes.Length == 0)
                return false;

            int i = 0;
            float sign = 1f;
            if (bytes[i] == (byte)'-')
            {
                sign = -1f;
                i++;
            }

            float integer = 0f;
            bool any = false;
            while (i < bytes.Length && bytes[i] >= (byte)'0' && bytes[i] <= (byte)'9')
            {
                integer = integer * 10f + (bytes[i] - (byte)'0');
                i++;
                any = true;
            }

            float fraction = 0f;
            float scale = 1f;
            if (i < bytes.Length && bytes[i] == (byte)'.')
            {
                i++;
                while (i < bytes.Length && bytes[i] >= (byte)'0' && bytes[i] <= (byte)'9')
                {
                    fraction = fraction * 10f + (bytes[i] - (byte)'0');
                    scale *= 10f;
                    i++;
                    any = true;
                }
            }

            value = sign * (integer + fraction * math.rcp(scale));
            return any &&
                   i == bytes.Length &&
                   value == value &&
                   value > -3.402823e38f &&
                   value < 3.402823e38f;
        }

        private static bool TryParseUInt(ReadOnlySpan<byte> bytes, out uint value)
        {
            value = 0u;
            if (bytes.Length == 0)
                return false;

            for (int i = 0; i < bytes.Length; i++)
            {
                byte c = bytes[i];
                if (c < (byte)'0' || c > (byte)'9')
                    return false;

                uint digit = (uint)(c - (byte)'0');
                if (value > (uint.MaxValue - digit) / 10u)
                    return false;

                value = value * 10u + digit;
            }

            return true;
        }

        private static bool IsSupportedProfileRuleMask(uint flags)
        {
            const uint supported = GeographySanityConstants.RuleCheckFloating |
                                   GeographySanityConstants.RuleCheckBuried |
                                   GeographySanityConstants.RuleCheckCrushDepth;
            return flags != 0u && (flags & ~supported) == 0u;
        }

        private static uint HashField(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte c = bytes[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash ^= c;
                hash *= 16777619u;
            }

            return hash == 0u ? 1u : hash;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && (value[start] == (byte)' ' || value[start] == (byte)'\t'))
                start++;
            while (end >= start && (value[end] == (byte)' ' || value[end] == (byte)'\t'))
                end--;
            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static string ResolveProjectRoot()
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            return parent != null ? parent.FullName : Directory.GetCurrentDirectory();
        }
    }
}
#endif
