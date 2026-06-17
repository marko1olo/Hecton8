#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    internal static class Particle_System_Scanner
    {
        private const string ReportRelativePath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";

        [MenuItem("Hecton8/Rendering/Particle System Scanner")]
        public static void Run()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            ScanReport report = Scan(projectRoot);
            WriteReport(projectRoot, report);
            AssetDatabase.Refresh();
        }

        private static ScanReport Scan(string projectRoot)
        {
            ScanReport report = new ScanReport
            {
                Agent = "SHINOBU_237",
                Scanner = "Particle_System_Scanner",
                TimestampUtc = DateTime.UtcNow.ToString("O"),
                Status = "PASS"
            };

            ScanPath(projectRoot, "Assets/_Project/Prefabs/Vehicles", "*.prefab", report);
            ScanPath(projectRoot, "Assets/_Project/Scripts/VFX", "*.cs", report);
            report.ForbiddenCpuParticlesEradicated = report.ForbiddenHits == 0;
            if (!report.ForbiddenCpuParticlesEradicated)
                report.Status = "FAIL_FORBIDDEN_CPU_PARTICLE_PATH";
            return report;
        }

        private static void ScanPath(string projectRoot, string relativePath, string searchPattern, ScanReport report)
        {
            string absolutePath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(absolutePath))
            {
                report.MissingPaths.Add(relativePath);
                return;
            }

            foreach (string file in Directory.EnumerateFiles(absolutePath, searchPattern, SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                string assetPath = ToProjectRelativePath(projectRoot, file);
                bool forbiddenContext = IsForbiddenWakeContext(assetPath, text);
                if (text.IndexOf("ParticleSystem", StringComparison.Ordinal) >= 0)
                {
                    report.ParticleSystemHits++;
                    RecordHit(report, assetPath + " :: ParticleSystem", forbiddenContext);
                }

                if (text.IndexOf(".Emit(", StringComparison.Ordinal) >= 0 ||
                    text.IndexOf("ParticleSystem.Emit", StringComparison.Ordinal) >= 0)
                {
                    report.EmitCallHits++;
                    RecordHit(report, assetPath + " :: Emit", forbiddenContext);
                }

                if (text.IndexOf("CollisionModule", StringComparison.Ordinal) >= 0 ||
                    text.IndexOf("m_CollisionModule", StringComparison.Ordinal) >= 0)
                {
                    report.CollisionModuleHits++;
                    RecordHit(report, assetPath + " :: CollisionModule", forbiddenContext);
                }
            }
        }

        private static bool IsForbiddenWakeContext(string assetPath, string text)
        {
            return assetPath.IndexOf("MarineSnow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                assetPath.IndexOf("Silt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                assetPath.IndexOf("Propwash", StringComparison.OrdinalIgnoreCase) >= 0 ||
                assetPath.IndexOf("PropWash", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("propwash", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("marine snow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("silt", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void RecordHit(ScanReport report, string hit, bool forbiddenContext)
        {
            report.Hits.Add(hit);
            if (forbiddenContext)
                report.ForbiddenHits++;
        }

        private static string ToProjectRelativePath(string projectRoot, string file)
        {
            string fullRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullFile = Path.GetFullPath(file);
            if (!fullFile.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                return fullFile;

            return fullFile.Substring(fullRoot.Length).Replace(Path.DirectorySeparatorChar, '/');
        }

        private static void WriteReport(string projectRoot, ScanReport report)
        {
            string path = Path.Combine(projectRoot, ReportRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine("{");
            AppendJson(builder, "agent", report.Agent, comma: true, indent: 2);
            AppendJson(builder, "scanner", report.Scanner, comma: true, indent: 2);
            AppendJson(builder, "timestampUtc", report.TimestampUtc, comma: true, indent: 2);
            AppendJson(builder, "status", report.Status, comma: true, indent: 2);
            AppendJson(builder, "forbiddenCpuParticlesEradicated", report.ForbiddenCpuParticlesEradicated ? "true" : "false", comma: true, indent: 2, rawValue: true);
            AppendJson(builder, "particleSystemHits", report.ParticleSystemHits.ToString(), comma: true, indent: 2, rawValue: true);
            AppendJson(builder, "emitCallHits", report.EmitCallHits.ToString(), comma: true, indent: 2, rawValue: true);
            AppendJson(builder, "collisionModuleHits", report.CollisionModuleHits.ToString(), comma: true, indent: 2, rawValue: true);
            AppendJson(builder, "forbiddenHits", report.ForbiddenHits.ToString(), comma: true, indent: 2, rawValue: true);
            AppendArray(builder, "missingPaths", report.MissingPaths, comma: true);
            AppendArray(builder, "hits", report.Hits, comma: false);
            builder.AppendLine("}");
            File.WriteAllText(path, builder.ToString());
        }

        private static void AppendJson(StringBuilder builder, string name, string value, bool comma, int indent, bool rawValue = false)
        {
            builder.Append(' ', indent);
            builder.Append('"').Append(name).Append("\": ");
            if (rawValue)
                builder.Append(value);
            else
                builder.Append('"').Append(Escape(value)).Append('"');
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendArray(StringBuilder builder, string name, List<string> values, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": [");
            if (values.Count > 0)
                builder.AppendLine();

            for (int i = 0; i < values.Count; i++)
            {
                builder.Append("    \"").Append(Escape(values[i])).Append('"');
                if (i + 1 < values.Count)
                    builder.Append(',');
                builder.AppendLine();
            }

            if (values.Count > 0)
                builder.Append("  ");
            builder.Append(']');
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private sealed class ScanReport
        {
            public string Agent;
            public string Scanner;
            public string TimestampUtc;
            public string Status;
            public bool ForbiddenCpuParticlesEradicated;
            public int ParticleSystemHits;
            public int EmitCallHits;
            public int CollisionModuleHits;
            public int ForbiddenHits;
            public readonly List<string> MissingPaths = new List<string>(16);
            public readonly List<string> Hits = new List<string>(64);
        }
    }
}
#endif
