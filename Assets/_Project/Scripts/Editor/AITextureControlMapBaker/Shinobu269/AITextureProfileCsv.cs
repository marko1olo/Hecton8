#if UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;

namespace Hecton8.Editor.AITextureControlMaps
{
    [StructLayout(LayoutKind.Explicit, Size = 96)]
    internal struct AITextureIngestionProfile
    {
        [FieldOffset(0)] public FixedString64Bytes ProfileName;
        [FieldOffset(64)] public AITexturePassMask PassMask;
        [FieldOffset(68)] public int Resolution;
        [FieldOffset(72)] public float GlobalQualityWeight;
        [FieldOffset(76)] public uint StandaloneFormatHash;
        [FieldOffset(80)] public uint AndroidFormatHash;
        [FieldOffset(84)] public uint _pad0;
        [FieldOffset(88)] public ulong _pad1;
    }

    internal static unsafe class AITextureProfileCsv
    {
        private const int MaximumCsvBytes = 64 * 1024;
        private const uint HashBc7 = 0x37434248u;
        private const uint HashBc5 = 0x35434248u;
        private const uint HashAstc6 = 0x36415354u;
        private const string NativeMemoryOwner = nameof(AITextureProfileCsv);

        internal static AITextureBakeSettings LoadFirstSettingsOrDefault()
        {
            if (!TryParseFirstProfileFromCsv(out AITextureIngestionProfile profile))
                return AITextureControlMapBaker.DefaultSettings();

            AITextureBakeSettings settings = AITextureControlMapBaker.DefaultSettings();
            settings.ProfileName = profile.ProfileName.Length > 0 ? profile.ProfileName : new FixedString64Bytes("Hero_Prop");
            settings.PassMask = profile.PassMask != (AITexturePassMask)0 ? profile.PassMask : AITexturePassMask.All;
            settings.Resolution = math.clamp(profile.Resolution, 64, AITextureControlMapConstants.HeroBakeResolution);
            settings.GlobalQualityWeight = math.saturate(profile.GlobalQualityWeight);
            return settings;
        }

        internal static bool TryParseFirstProfileFromCsv(out AITextureIngestionProfile profile)
        {
            profile = default;
            string absolutePath = BuildAbsoluteProjectPath(AITextureControlMapConstants.ProfileCsvPath);
            if (!File.Exists(absolutePath))
                return false;

            NativeArray<byte> bytes = default;
            try
            {
                using (FileStream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    long length64 = stream.Length;
                    if (length64 <= 0L || length64 > MaximumCsvBytes)
                        return false;

                    int length = (int)length64;
                    bytes = AITextureNativeMemory.AllocateArray<byte>(
                        length,
                        Allocator.TempJob,
                        NativeArrayOptions.UninitializedMemory,
                        NativeMemoryOwner,
                        nameof(bytes));
                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(bytes);
                    Span<byte> target = new Span<byte>(ptr, length);
                    int read = 0;
                    while (read < length)
                    {
                        int chunk = stream.Read(target.Slice(read));
                        if (chunk <= 0)
                            break;
                        read += chunk;
                    }

                    if (read != length)
                        return false;

                    int offset = Utf8BomOffset(ptr, read);
                    byte* csv = ptr + offset;
                    int csvLength = read - offset;
                    int cursor = 0;
                    while (cursor < csvLength)
                    {
                        int rowStart = cursor;
                        int rowEnd = FindLineEnd(csv, csvLength, rowStart);
                        cursor = ConsumeLineEnd(csv, csvLength, rowEnd);
                        if (TryParseRow(csv, rowStart, rowEnd, out profile))
                            return true;
                    }
                }
            }
            finally
            {
                AITextureNativeMemory.DisposeArray(ref bytes);
            }

            return false;
        }

        internal static bool TrySelectProfileForAsset(string assetPath, out AITextureIngestionProfile profile)
        {
            profile = default;
            if (string.IsNullOrEmpty(assetPath))
                return false;

            string lowerPath = assetPath.Replace('\\', '/').ToLowerInvariant();
            string absolutePath = BuildAbsoluteProjectPath(AITextureControlMapConstants.ProfileCsvPath);
            if (!File.Exists(absolutePath))
                return false;

            NativeArray<byte> bytes = default;
            try
            {
                using (FileStream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    long length64 = stream.Length;
                    if (length64 <= 0L || length64 > MaximumCsvBytes)
                        return false;

                    int length = (int)length64;
                    bytes = AITextureNativeMemory.AllocateArray<byte>(
                        length,
                        Allocator.TempJob,
                        NativeArrayOptions.UninitializedMemory,
                        NativeMemoryOwner,
                        nameof(bytes));
                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(bytes);
                    Span<byte> target = new Span<byte>(ptr, length);
                    int read = 0;
                    while (read < length)
                    {
                        int chunk = stream.Read(target.Slice(read));
                        if (chunk <= 0)
                            break;
                        read += chunk;
                    }

                    if (read != length)
                        return false;

                    int offset = Utf8BomOffset(ptr, read);
                    byte* csv = ptr + offset;
                    int csvLength = read - offset;
                    int cursor = 0;
                    while (cursor < csvLength)
                    {
                        int rowStart = cursor;
                        int rowEnd = FindLineEnd(csv, csvLength, rowStart);
                        cursor = ConsumeLineEnd(csv, csvLength, rowEnd);
                        if (!TryParseRow(csv, rowStart, rowEnd, out AITextureIngestionProfile candidate))
                            continue;

                        if (PathContainsProfileName(lowerPath, candidate.ProfileName) ||
                            PathContainsLeadingProfileToken(lowerPath, candidate.ProfileName))
                        {
                            profile = candidate;
                            return true;
                        }
                    }
                }
            }
            finally
            {
                AITextureNativeMemory.DisposeArray(ref bytes);
            }

            return false;
        }

        private static bool TryParseRow(byte* bytes, int rowStart, int rowEnd, out AITextureIngestionProfile profile)
        {
            profile = default;
            int first = rowStart;
            while (first < rowEnd && IsWhitespace(bytes[first]))
                first++;
            if (first >= rowEnd || bytes[first] == '#')
                return false;
            if (StartsWithIgnoreCase(bytes, first, rowEnd, "profile"))
                return false;

            int cursor = rowStart;
            ConsumeCell(bytes, rowEnd, ref cursor, out int nameStart, out int nameLength);
            ConsumeCell(bytes, rowEnd, ref cursor, out int resolutionStart, out int resolutionLength);
            ConsumeCell(bytes, rowEnd, ref cursor, out int passStart, out int passLength);
            ConsumeCell(bytes, rowEnd, ref cursor, out int qualityStart, out int qualityLength);
            ConsumeCell(bytes, rowEnd, ref cursor, out int standaloneStart, out int standaloneLength);
            ConsumeCell(bytes, rowEnd, ref cursor, out int androidStart, out int androidLength);

            profile.ProfileName = BuildFixedString(bytes, nameStart, nameLength);
            profile.Resolution = ParseInt(bytes, resolutionStart, resolutionLength, AITextureControlMapConstants.DefaultBakeResolution);
            profile.PassMask = ParsePassMask(bytes, passStart, passLength);
            profile.GlobalQualityWeight = ParseFloat(bytes, qualityStart, qualityLength, 1.0f);
            profile.StandaloneFormatHash = ParseFormatHash(bytes, standaloneStart, standaloneLength, HashBc7);
            profile.AndroidFormatHash = ParseFormatHash(bytes, androidStart, androidLength, HashAstc6);
            profile._pad0 = 0u;
            profile._pad1 = 0UL;
            if (profile.ProfileName.Length == 0)
                profile.ProfileName = new FixedString64Bytes("Unnamed_AI_Texture_Profile");
            return true;
        }

        private static void ConsumeCell(byte* bytes, int rowEnd, ref int cursor, out int start, out int length)
        {
            while (cursor < rowEnd && IsWhitespace(bytes[cursor]))
                cursor++;

            start = cursor;
            while (cursor < rowEnd && bytes[cursor] != ',')
                cursor++;

            int end = cursor;
            while (end > start && IsWhitespace(bytes[end - 1]))
                end--;

            length = math.max(0, end - start);
            if (cursor < rowEnd && bytes[cursor] == ',')
                cursor++;
        }

        private static FixedString64Bytes BuildFixedString(byte* bytes, int start, int length)
        {
            FixedString64Bytes value = default;
            int end = start + length;
            for (int i = start; i < end && value.Length < FixedString64Bytes.UTF8MaxLengthInBytes; i++)
            {
                byte b = bytes[i];
                if (b >= 32 && b < 127)
                    value.Add(b);
            }

            return value;
        }

        private static int ParseInt(byte* bytes, int start, int length, int fallback)
        {
            int end = start + length;
            int cursor = start;
            bool negative = false;
            if (cursor < end && bytes[cursor] == '-')
            {
                negative = true;
                cursor++;
            }

            int value = 0;
            bool any = false;
            while (cursor < end)
            {
                byte b = bytes[cursor++];
                if (b < '0' || b > '9')
                    return any ? (negative ? -value : value) : fallback;
                any = true;
                value = value * 10 + (b - '0');
            }

            return any ? (negative ? -value : value) : fallback;
        }

        private static float ParseFloat(byte* bytes, int start, int length, float fallback)
        {
            int end = start + length;
            int cursor = start;
            bool negative = false;
            if (cursor < end && bytes[cursor] == '-')
            {
                negative = true;
                cursor++;
            }

            float value = 0.0f;
            bool any = false;
            while (cursor < end && bytes[cursor] >= '0' && bytes[cursor] <= '9')
            {
                any = true;
                value = value * 10.0f + (bytes[cursor++] - '0');
            }

            if (cursor < end && bytes[cursor] == '.')
            {
                cursor++;
                float place = 0.1f;
                while (cursor < end && bytes[cursor] >= '0' && bytes[cursor] <= '9')
                {
                    any = true;
                    value += (bytes[cursor++] - '0') * place;
                    place *= 0.1f;
                }
            }

            if (!any || !math.isfinite(value))
                return fallback;
            return negative ? -value : value;
        }

        private static AITexturePassMask ParsePassMask(byte* bytes, int start, int length)
        {
            AITexturePassMask mask = (AITexturePassMask)0;
            if (SegmentContains(bytes, start, length, "normal"))
                mask |= AITexturePassMask.Normal;
            if (SegmentContains(bytes, start, length, "depth"))
                mask |= AITexturePassMask.Depth;
            if (SegmentContains(bytes, start, length, "colorid") || SegmentContains(bytes, start, length, "color_id"))
                mask |= AITexturePassMask.ColorId;
            if (SegmentContains(bytes, start, length, "curvature"))
                mask |= AITexturePassMask.Curvature;
            if (SegmentContains(bytes, start, length, "all"))
                mask = AITexturePassMask.All;
            return mask == (AITexturePassMask)0 ? AITexturePassMask.All : mask;
        }

        private static uint ParseFormatHash(byte* bytes, int start, int length, uint fallback)
        {
            if (SegmentContains(bytes, start, length, "bc7"))
                return HashBc7;
            if (SegmentContains(bytes, start, length, "bc5"))
                return HashBc5;
            if (SegmentContains(bytes, start, length, "astc_6x6") || SegmentContains(bytes, start, length, "astc6"))
                return HashAstc6;
            return fallback;
        }

        private static bool PathContainsProfileName(string lowerPath, FixedString64Bytes profileName)
        {
            int nameLength = profileName.Length;
            if (string.IsNullOrEmpty(lowerPath) || nameLength == 0 || lowerPath.Length < nameLength)
                return false;

            for (int start = 0; start <= lowerPath.Length - nameLength; start++)
            {
                bool equal = true;
                for (int i = 0; i < nameLength; i++)
                {
                    byte expected = ToLowerAscii(profileName[i]);
                    char actual = lowerPath[start + i];
                    if (actual == '-' || actual == ' ')
                        actual = '_';
                    if (actual != (char)expected)
                    {
                        equal = false;
                        break;
                    }
                }

                if (equal)
                    return true;
            }

            return false;
        }

        private static bool PathContainsLeadingProfileToken(string lowerPath, FixedString64Bytes profileName)
        {
            int nameLength = profileName.Length;
            if (string.IsNullOrEmpty(lowerPath) || nameLength < 4)
                return false;

            int tokenLength = 0;
            while (tokenLength < nameLength)
            {
                byte b = profileName[tokenLength];
                if (b == '_' || b == '-' || b == ' ')
                    break;
                tokenLength++;
            }

            if (tokenLength < 4 || tokenLength == nameLength || lowerPath.Length < tokenLength)
                return false;

            for (int start = 0; start <= lowerPath.Length - tokenLength; start++)
            {
                bool equal = true;
                for (int i = 0; i < tokenLength; i++)
                {
                    char actual = lowerPath[start + i];
                    if (actual == '-' || actual == ' ')
                        actual = '_';
                    if (actual != (char)ToLowerAscii(profileName[i]))
                    {
                        equal = false;
                        break;
                    }
                }

                if (equal)
                    return true;
            }

            return false;
        }

        private static bool SegmentContains(byte* bytes, int start, int length, string token)
        {
            int end = start + length;
            int tokenLength = token.Length;
            for (int i = start; i <= end - tokenLength; i++)
            {
                bool equal = true;
                for (int t = 0; t < tokenLength; t++)
                {
                    if (ToLowerAscii(bytes[i + t]) != token[t])
                    {
                        equal = false;
                        break;
                    }
                }

                if (equal)
                    return true;
            }

            return false;
        }

        private static bool StartsWithIgnoreCase(byte* bytes, int start, int end, string token)
        {
            if (end - start < token.Length)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                if (ToLowerAscii(bytes[start + i]) != token[i])
                    return false;
            }

            return true;
        }

        private static byte ToLowerAscii(byte value)
        {
            return value >= 'A' && value <= 'Z' ? (byte)(value + 32) : value;
        }

        private static int FindLineEnd(byte* bytes, int length, int cursor)
        {
            while (cursor < length && bytes[cursor] != '\n' && bytes[cursor] != '\r')
                cursor++;
            return cursor;
        }

        private static int ConsumeLineEnd(byte* bytes, int length, int cursor)
        {
            if (cursor < length && bytes[cursor] == '\r')
                cursor++;
            if (cursor < length && bytes[cursor] == '\n')
                cursor++;
            return cursor;
        }

        private static int Utf8BomOffset(byte* bytes, int length)
        {
            return length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        }

        private static bool IsWhitespace(byte value)
        {
            return value == ' ' || value == '\t';
        }

        [MenuItem("Hecton8/AI Texture Control Maps/Create Default AI Texture Profile CSV", false, 2693)]
        internal static void CreateDefaultCsvFromMenu()
        {
            WriteDefaultCsvIfMissing(true);
        }

        private static void WriteDefaultCsvIfMissing(bool refreshAssetDatabase)
        {
            string absolutePath = BuildAbsoluteProjectPath(AITextureControlMapConstants.ProfileCsvPath);
            if (File.Exists(absolutePath))
                return;

            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(absolutePath,
                "profile,resolution,pass_mask,global_quality_weight,standalone,android\n" +
                "Hero_Prop,4096,All,1.0,BC7,ASTC_6x6\n" +
                "Module,4096,Normal|Depth|ColorID|Curvature,0.85,BC7,ASTC_6x6\n" +
                "Debris,512,Normal|ColorID|Curvature,0.25,BC7,ASTC_6x6\n", new UTF8Encoding(false));
            if (refreshAssetDatabase)
                AssetDatabase.Refresh();
        }

        private static string BuildAbsoluteProjectPath(string projectPath)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), projectPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
#endif
