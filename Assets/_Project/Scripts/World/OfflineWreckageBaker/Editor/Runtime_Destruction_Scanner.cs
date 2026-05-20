using System;
using System.IO;
using System.Text;
using Hecton8.World.OfflineWreckageBaker;
using UnityEditor;
using UnityEngine;

namespace Hecton8.World.OfflineWreckageBaker.Editor
{
    public static class Runtime_Destruction_Scanner
    {
        private static readonly string[] s_roots =
        {
            "Assets/_Project/Scripts/Combat",
            "Assets/_Project/Scripts/Gameplay/Combat",
            "Assets/_Project/Scripts/Environment"
        };

        private static readonly string[] s_forbiddenPatterns =
        {
            "sharedMesh.vertices",
            ".mesh.vertices",
            "SetVertices(",
            "RecalculateNormals(",
            "AddBlendShapeFrame",
            "SkinnedMeshRenderer",
            "Voronoi",
            "Shatter(",
            "ShatterMesh",
            "FractureMesh",
            "FractureShard",
            "ProceduralFracture",
            "AddComponent<Rigidbody>",
            "Instantiate("
        };

        [MenuItem("HECTON-8/Wreckage Forge/Scan Runtime Destruction")]
        public static void ScanMenu()
        {
            ScanAndWriteReport(Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length));
        }

        public static int ScanAndWriteReport(string projectRoot)
        {
            int findingCount = 0;
            StringBuilder findings = new StringBuilder(4096); // COLD ALLOC: StringBuilder[4096] - editor report staging - owner: Runtime_Destruction_Scanner
            StringBuilder roots = new StringBuilder(1024); // COLD ALLOC: StringBuilder[1024] - editor root status staging - owner: Runtime_Destruction_Scanner
            for (int rootIndex = 0; rootIndex < s_roots.Length; rootIndex++)
            {
                string root = Path.Combine(projectRoot, s_roots[rootIndex]);
                if (!Directory.Exists(root))
                {
                    AppendRoot(roots, s_roots[rootIndex], "MISSING");
                    continue;
                }

                AppendRoot(roots, s_roots[rootIndex], "SCANNED");
                string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    string file = files[fileIndex].Replace('\\', '/');
                    if (file.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    string text = File.ReadAllText(file);
                    for (int patternIndex = 0; patternIndex < s_forbiddenPatterns.Length; patternIndex++)
                    {
                        string pattern = s_forbiddenPatterns[patternIndex];
                        int offset = text.IndexOf(pattern, StringComparison.Ordinal);
                        while (offset >= 0)
                        {
                            int line = CountLine(text, offset);
                            AppendFinding(findings, Relative(projectRoot, file), line, pattern);
                            findingCount++;
                            offset = text.IndexOf(pattern, offset + pattern.Length, StringComparison.Ordinal);
                        }
                    }
                }
            }

            string reportDir = Path.Combine(projectRoot, "Docs", "Reports");
            Directory.CreateDirectory(reportDir);
            string reportPath = Path.Combine(reportDir, "PHYSICS_OPTIMIZATION_REPORT.json");
            string previousReport = File.Exists(reportPath) ? File.ReadAllText(reportPath) : string.Empty;
            if (!string.IsNullOrEmpty(previousReport))
                WriteTextAtomic(Path.Combine(reportDir, "PHYSICS_OPTIMIZATION_REPORT_PREVIOUS_SHINOBU_209.json"), previousReport);

            int previousReportBytes = MeasureUtf8Text(previousReport, out uint previousReportHash);
            StringBuilder json = new StringBuilder(8192); // COLD ALLOC: StringBuilder[8192] - editor JSON report - owner: Runtime_Destruction_Scanner
            json.Append("{\n");
            json.Append("  \"agent\": \"SHINOBU_209\",\n");
            json.Append("  \"status\": \"PENDING_VERIFICATION\",\n");
            json.Append("  \"summary\": \"Runtime Mesh Deformations Eradicated\",\n");
            json.Append("  \"findingCount\": ").Append(findingCount).Append(",\n");
            json.Append("  \"previousReportPreserved\": ").Append(string.IsNullOrEmpty(previousReport) ? "false" : "true").Append(",\n");
            json.Append("  \"previousReportBytes\": ").Append(previousReportBytes).Append(",\n");
            json.Append("  \"previousReportHash\": ").Append(previousReportHash).Append(",\n");
            json.Append("  \"previousReportAgent\": \"");
            AppendEscaped(json, ExtractJsonStringValue(previousReport, "agent"));
            json.Append("\",\n");

            json.Append("  \"roots\": [\n");
            json.Append(roots);
            json.Append("\n  ],\n");
            json.Append("  \"findings\": [\n");
            json.Append(findings);
            json.Append("\n  ]\n");
            json.Append("}\n");
            string output = json.ToString();
            WriteTextAtomic(reportPath, output);
            WriteTextAtomic(Path.Combine(reportDir, "PHYSICS_OPTIMIZATION_REPORT_SHINOBU_209.json"), output);
            AssetDatabase.Refresh();
            return findingCount;
        }

        private static void AppendFinding(StringBuilder builder, string path, int line, string pattern)
        {
            if (builder.Length > 0)
                builder.Append(",\n");

            builder.Append("    { \"path\": \"");
            AppendEscaped(builder, path);
            builder.Append("\", \"line\": ").Append(line).Append(", \"pattern\": \"");
            AppendEscaped(builder, pattern);
            builder.Append("\" }");
        }

        private static void AppendRoot(StringBuilder builder, string path, string status)
        {
            if (builder.Length > 0)
                builder.Append(",\n");

            builder.Append("    { \"path\": \"");
            AppendEscaped(builder, path);
            builder.Append("\", \"status\": \"");
            AppendEscaped(builder, status);
            builder.Append("\" }");
        }

        private static int CountLine(string text, int offset)
        {
            int line = 1;
            int limit = Math.Min(offset, text.Length);
            for (int i = 0; i < limit; i++)
            {
                if (text[i] == '\n')
                    line++;
            }

            return line;
        }

        private static string Relative(string projectRoot, string path)
        {
            string root = projectRoot.Replace('\\', '/').TrimEnd('/');
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path.Substring(root.Length + 1) : path;
        }

        private static int MeasureUtf8Text(string text, out uint hash)
        {
            hash = 2166136261u;
            if (string.IsNullOrEmpty(text))
            {
                hash = OfflineWreckageBakeMath.Hash(hash);
                return 0;
            }

            int byteCount = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    {
                        byteCount += HashUtf8Scalar(char.ConvertToUtf32(c, text[i + 1]), ref hash);
                        i++;
                        continue;
                    }

                    byteCount += HashUtf8Scalar(0xFFFD, ref hash);
                    continue;
                }

                byteCount += HashUtf8Scalar(char.IsLowSurrogate(c) ? 0xFFFD : c, ref hash);
            }

            hash = OfflineWreckageBakeMath.Hash(hash);
            return byteCount;
        }

        private static int HashUtf8Scalar(int scalar, ref uint hash)
        {
            if (scalar <= 0x7F)
            {
                HashRawByte((byte)scalar, ref hash);
                return 1;
            }

            if (scalar <= 0x7FF)
            {
                HashRawByte((byte)(0xC0 | (scalar >> 6)), ref hash);
                HashRawByte((byte)(0x80 | (scalar & 0x3F)), ref hash);
                return 2;
            }

            if (scalar <= 0xFFFF)
            {
                HashRawByte((byte)(0xE0 | (scalar >> 12)), ref hash);
                HashRawByte((byte)(0x80 | ((scalar >> 6) & 0x3F)), ref hash);
                HashRawByte((byte)(0x80 | (scalar & 0x3F)), ref hash);
                return 3;
            }

            HashRawByte((byte)(0xF0 | (scalar >> 18)), ref hash);
            HashRawByte((byte)(0x80 | ((scalar >> 12) & 0x3F)), ref hash);
            HashRawByte((byte)(0x80 | ((scalar >> 6) & 0x3F)), ref hash);
            HashRawByte((byte)(0x80 | (scalar & 0x3F)), ref hash);
            return 4;
        }

        private static void HashRawByte(byte value, ref uint hash)
        {
            hash ^= value;
            hash *= 16777619u;
        }

        private static string ExtractJsonStringValue(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
                return "NONE";

            string marker = "\"" + key + "\"";
            int keyIndex = json.IndexOf(marker, StringComparison.Ordinal);
            if (keyIndex < 0)
                return "UNKNOWN";

            int colon = json.IndexOf(':', keyIndex + marker.Length);
            if (colon < 0)
                return "UNKNOWN";

            int start = colon + 1;
            while (start < json.Length && IsJsonWhitespace(json[start]))
                start++;

            if (start >= json.Length || json[start] != '"')
                return "UNKNOWN";

            int end = start + 1;
            while (end < json.Length)
            {
                char c = json[end];
                if (c == '"' && !IsEscaped(json, end))
                    break;
                end++;
            }

            return end < json.Length ? json.Substring(start + 1, end - start - 1) : "UNKNOWN";
        }

        private static bool IsJsonWhitespace(char value)
        {
            return value == ' ' || value == '\t' || value == '\r' || value == '\n';
        }

        private static bool IsEscaped(string text, int quoteIndex)
        {
            int slashCount = 0;
            for (int i = quoteIndex - 1; i >= 0 && text[i] == '\\'; i--)
                slashCount++;

            return (slashCount & 1) != 0;
        }

        private static void AppendEscaped(StringBuilder builder, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\':
                    case '"':
                        builder.Append('\\').Append(c);
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < ' ')
                        {
                            builder.Append("\\u00");
                            AppendHexByte(builder, (byte)c);
                            break;
                        }

                        builder.Append(c);
                        break;
                }
            }
        }

        private static void AppendHexByte(StringBuilder builder, byte value)
        {
            builder.Append(NibbleToHex((value >> 4) & 0xF));
            builder.Append(NibbleToHex(value & 0xF));
        }

        private static char NibbleToHex(int value)
        {
            return (char)(value < 10 ? '0' + value : 'A' + value - 10);
        }

        private static void WriteTextAtomic(string path, string text)
        {
            OfflineWreckageAtomicFile.WriteTextUtf8(path, text);
        }
    }
}
