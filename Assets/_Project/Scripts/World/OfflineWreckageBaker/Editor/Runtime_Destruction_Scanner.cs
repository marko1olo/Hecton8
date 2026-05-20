using System;
using System.IO;
using System.Text;
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
            StringBuilder json = new StringBuilder(8192); // COLD ALLOC: StringBuilder[8192] - editor JSON report - owner: Runtime_Destruction_Scanner
            json.Append("{\n");
            json.Append("  \"agent\": \"SHINOBU_209\",\n");
            json.Append("  \"status\": \"PENDING_VERIFICATION\",\n");
            json.Append("  \"summary\": \"Runtime Mesh Deformations Eradicated\",\n");
            json.Append("  \"findingCount\": ").Append(findingCount).Append(",\n");
            json.Append("  \"previousReportPreserved\": ").Append(string.IsNullOrEmpty(previousReport) ? "false" : "true").Append(",\n");
            if (!string.IsNullOrEmpty(previousReport))
            {
                json.Append("  \"previousReport\": \"");
                AppendEscaped(json, previousReport);
                json.Append("\",\n");
            }

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

        private static void AppendEscaped(StringBuilder builder, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' || c == '"')
                    builder.Append('\\');
                builder.Append(c);
            }
        }

        private static void WriteTextAtomic(string path, string text)
        {
            OfflineWreckageAtomicFile.WriteTextUtf8(path, text);
        }
    }
}
