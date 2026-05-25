#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.AITextureControlMaps
{
    internal static class AITexturePipelineArchaeology
    {
        private static readonly string[] ScanRoots =
        {
            "Assets/Editor"
        };

        private static readonly string[] BannedTokens =
        {
            "Substance",
            "SubstancePainter",
            "PainterBridge",
            "ReadPixels",
            "GetPixels",
            "GetPixels32",
            "GetPixel(",
            "Camera.Render",
            "Texture2D.EncodeToPNG"
        };

        [MenuItem("HECTON-8/AI Texture Control Maps/Run Legacy Capture Archaeology", false, 2660)]
        internal static void RunFromMenu()
        {
            List<LegacyCaptureFinding> findings = Scan();
            WriteReport(findings);
            Debug.Log("[AITexturePipelineArchaeology] Findings=" + findings.Count + " report=" + AITextureControlMapConstants.ArchaeologyReportPath);
        }

        internal static List<LegacyCaptureFinding> Scan()
        {
            List<LegacyCaptureFinding> findings = new List<LegacyCaptureFinding>(32); // COLD ALLOC: List<LegacyCaptureFinding>[32] - editor archaeology result - owner: AITexturePipelineArchaeology
            string rootPath = Directory.GetCurrentDirectory();
            for (int rootIndex = 0; rootIndex < ScanRoots.Length; rootIndex++)
            {
                string root = ScanRoots[rootIndex];
                if (!Directory.Exists(root))
                    continue;

                foreach (string discoveredFile in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string path = discoveredFile.Replace('\\', '/');
                    if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        continue;

                    ScanFile(rootPath, path, findings);
                }
            }

            return findings;
        }

        internal static void WriteReport(List<LegacyCaptureFinding> findings)
        {
            EnsureFileFolder(AITextureControlMapConstants.ArchaeologyReportPath);
            StringBuilder builder = new StringBuilder(2048); // COLD ALLOC: StringBuilder[2048] - editor archaeology JSON - owner: AITexturePipelineArchaeology
            builder.Append("{\n");
            builder.Append("  \"schema\": \"hecton8.ai_texture_pipeline_archaeology.v1\",\n");
            builder.Append("  \"scope\": \"Assets/Editor only; vendor and unrelated project tools are reported by separate static scans, not mutated here\",\n");
            builder.Append("  \"findingCount\": ").Append(findings.Count).Append(",\n");
            builder.Append("  \"action\": \"No file deletion performed unless a first-party texture-control legacy script is found under Assets/Editor.\",\n");
            builder.Append("  \"findings\": [\n");
            for (int i = 0; i < findings.Count; i++)
            {
                LegacyCaptureFinding finding = findings[i];
                builder.Append("    { \"path\": \"").Append(Escape(finding.Path)).Append("\", \"line\": ").Append(finding.Line).Append(", \"token\": \"").Append(Escape(finding.Token)).Append("\" }");
                builder.Append(i + 1 < findings.Count ? ",\n" : "\n");
            }

            builder.Append("  ],\n");
            builder.Append("  \"status\": \"PENDING_VERIFICATION\"\n");
            builder.Append("}\n");
            File.WriteAllText(AITextureControlMapConstants.ArchaeologyReportPath, builder.ToString());
        }

        private static void ScanFile(string rootPath, string path, List<LegacyCaptureFinding> findings)
        {
            string absolute = Path.IsPathRooted(path) ? path : Path.Combine(rootPath, path);
            if (!File.Exists(absolute))
                return;

            string[] lines = File.ReadAllLines(absolute);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                for (int tokenIndex = 0; tokenIndex < BannedTokens.Length; tokenIndex++)
                {
                    string token = BannedTokens[tokenIndex];
                    if (line.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    LegacyCaptureFinding finding;
                    finding.Path = path;
                    finding.Line = lineIndex + 1;
                    finding.Token = token;
                    findings.Add(finding);
                }
            }
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void EnsureFileFolder(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        internal struct LegacyCaptureFinding
        {
            public string Path;
            public int Line;
            public string Token;
        }
    }
}
#endif
