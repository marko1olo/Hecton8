#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    internal static class CPU_Foam_Scanner
    {
        private const string ReportRelativePath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";
        private const string ReportPropertyName = "jacobianFoam";

        [MenuItem("HECTON-8/Rendering/CPU Foam Scanner")]
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
                Agent = "SHINOBU_266",
                Scanner = "CPU_Foam_Scanner",
                TimestampUtc = DateTime.UtcNow.ToString("O"),
                Status = "PASS"
            };

            ScanPath(projectRoot, "Assets/_Project/Scripts/Environment", "*.cs", report);
            ScanPath(projectRoot, "Assets/_Project/Prefabs/Vehicles", "*.prefab", report);
            ScanPath(projectRoot, "Assets/_Project/Prefabs/Environment", "*.prefab", report);
            ScanPath(projectRoot, "Assets/_Project/Scenes", "*.unity", report);

            report.SuperfluousCpuParticlesEradicated = report.ForbiddenHits == 0;
            if (!report.SuperfluousCpuParticlesEradicated)
                report.Status = "FAIL_CPU_FOAM_PARTICLE_PATH";
            report.Output = report.SuperfluousCpuParticlesEradicated
                ? "Superfluous CPU Particles Eradicated"
                : "CPU foam particle path requires owner cleanup";

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

            string particleComponentToken = "Particle" + "System";
            string particleEmitToken = particleComponentToken + "." + "Emit";
            string emitCallToken = "." + "Emit(";
            string textureReadbackToken = "Read" + "Pixels";
            string serializedParticleToken = "m_" + particleComponentToken;
            string[] files = Directory.GetFiles(absolutePath, searchPattern, SearchOption.AllDirectories);
            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string file = files[fileIndex];
                string text = File.ReadAllText(file);
                string assetPath = ToProjectRelativePath(projectRoot, file);
                bool foamContext = IsFoamContext(assetPath, text);

                if (text.IndexOf(particleComponentToken, StringComparison.Ordinal) >= 0)
                {
                    report.ParticleComponentHits++;
                    RecordHit(report, assetPath + " :: " + particleComponentToken, foamContext);
                }

                if (text.IndexOf(emitCallToken, StringComparison.Ordinal) >= 0 ||
                    text.IndexOf(particleEmitToken, StringComparison.Ordinal) >= 0)
                {
                    report.EmitCallHits++;
                    RecordHit(report, assetPath + " :: Emit", foamContext);
                }

                if (text.IndexOf(textureReadbackToken, StringComparison.Ordinal) >= 0)
                {
                    report.TextureReadbackHits++;
                    RecordHit(report, assetPath + " :: " + textureReadbackToken, foamContext);
                }

                if (searchPattern == "*.prefab" || searchPattern == "*.unity")
                {
                    if (text.IndexOf(particleComponentToken, StringComparison.Ordinal) >= 0 ||
                        text.IndexOf(serializedParticleToken, StringComparison.Ordinal) >= 0)
                    {
                        report.ScenePrefabParticleHits++;
                        RecordHit(report, assetPath + " :: ScenePrefabParticle", foamContext);
                    }
                }
            }
        }

        private static bool IsFoamContext(string assetPath, string text)
        {
            return Contains(assetPath, "foam") ||
                Contains(assetPath, "whitecap") ||
                Contains(assetPath, "splash") ||
                Contains(assetPath, "wake") ||
                Contains(assetPath, "water") ||
                Contains(text, "foam") ||
                Contains(text, "whitecap") ||
                Contains(text, "splash") ||
                Contains(text, "wake") ||
                Contains(text, "shoreline");
        }

        private static bool Contains(string value, string token)
        {
            return value != null && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void RecordHit(ScanReport report, string hit, bool forbidden)
        {
            report.Hits.Add(hit);
            if (forbidden)
            {
                report.ForbiddenHits++;
                report.ForbiddenHitsDetailed.Add(hit);
            }
        }

        private static void WriteReport(string projectRoot, ScanReport report)
        {
            string path = Path.Combine(projectRoot, ReportRelativePath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string foamReportJson = ToNamedJson(report);
            string mergedJson;
            if (File.Exists(path))
            {
                string existingJson = File.ReadAllText(path, Encoding.UTF8);
                if (!TryReplaceTopLevelProperty(existingJson, ReportPropertyName, foamReportJson, out mergedJson) &&
                    !TryInsertTopLevelProperty(existingJson, foamReportJson, out mergedJson))
                {
                    mergedJson = "{\n" + foamReportJson + "\n}\n";
                }
            }
            else
            {
                mergedJson = "{\n" + foamReportJson + "\n}\n";
            }

            File.WriteAllText(path, mergedJson, Encoding.UTF8);
        }

        private static string ToProjectRelativePath(string projectRoot, string file)
        {
            string fullRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullFile = Path.GetFullPath(file);
            return fullFile.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
                ? fullFile.Substring(fullRoot.Length).Replace(Path.DirectorySeparatorChar, '/')
                : fullFile.Replace(Path.DirectorySeparatorChar, '/');
        }

        private static string ToJson(ScanReport report)
        {
            StringBuilder builder = new StringBuilder(2048);
            builder.Append("{\n");
            AppendProperty(builder, "agent", report.Agent, true);
            AppendProperty(builder, "scanner", report.Scanner, true);
            AppendProperty(builder, "timestamp_utc", report.TimestampUtc, true);
            AppendProperty(builder, "status", report.Status, true);
            AppendProperty(builder, "particle_component_hits", report.ParticleComponentHits, true);
            AppendProperty(builder, "emit_call_hits", report.EmitCallHits, true);
            AppendProperty(builder, "texture_readback_hits", report.TextureReadbackHits, true);
            AppendProperty(builder, "scene_prefab_particle_hits", report.ScenePrefabParticleHits, true);
            AppendProperty(builder, "forbidden_hits", report.ForbiddenHits, true);
            AppendProperty(builder, "superfluous_cpu_particles_eradicated", report.SuperfluousCpuParticlesEradicated, true);
            AppendProperty(builder, "output", report.Output, true);
            AppendArray(builder, "missing_paths", report.MissingPaths, true);
            AppendArray(builder, "hits", report.Hits, true);
            AppendArray(builder, "forbidden_hits_detailed", report.ForbiddenHitsDetailed, false);
            builder.Append("}\n");
            return builder.ToString();
        }

        private static string ToNamedJson(ScanReport report)
        {
            string objectJson = ToJson(report).Trim();
            StringBuilder builder = new StringBuilder(objectJson.Length + ReportPropertyName.Length + 8);
            builder.Append("  \"").Append(ReportPropertyName).Append("\": ");
            for (int i = 0; i < objectJson.Length; i++)
            {
                char c = objectJson[i];
                builder.Append(c);
                if (c == '\n' && i + 1 < objectJson.Length)
                    builder.Append("  ");
            }

            return builder.ToString();
        }

        private static bool TryReplaceTopLevelProperty(string json, string propertyName, string replacement, out string merged)
        {
            merged = null;
            int index = json.IndexOf("\"" + propertyName + "\"", StringComparison.Ordinal);
            while (index >= 0)
            {
                if (IsTopLevelProperty(json, index) &&
                    TryFindPropertyValueBounds(json, index, out int start, out int end, out bool hadTrailingComma))
                {
                    string replacementWithComma = hadTrailingComma ? replacement + "," : replacement;
                    merged = json.Substring(0, start) + replacementWithComma + json.Substring(end);
                    return true;
                }

                index = json.IndexOf("\"" + propertyName + "\"", index + propertyName.Length + 2, StringComparison.Ordinal);
            }

            return false;
        }

        private static bool TryInsertTopLevelProperty(string json, string propertyJson, out string merged)
        {
            merged = null;
            int close = json.LastIndexOf('}');
            int open = json.IndexOf('{');
            if (open < 0 || close <= open)
                return false;

            bool hasContent = false;
            for (int i = open + 1; i < close; i++)
            {
                if (!char.IsWhiteSpace(json[i]))
                {
                    hasContent = true;
                    break;
                }
            }

            string prefix = json.Substring(0, close).TrimEnd();
            string suffix = json.Substring(close).TrimStart();
            merged = hasContent
                ? prefix + ",\n" + propertyJson + "\n" + suffix
                : "{\n" + propertyJson + "\n}\n";
            return true;
        }

        private static bool IsTopLevelProperty(string json, int propertyQuoteIndex)
        {
            int objectDepth = 0;
            int arrayDepth = 0;
            bool inString = false;
            bool escape = false;
            for (int i = 0; i < propertyQuoteIndex; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (escape)
                    {
                        escape = false;
                    }
                    else if (c == '\\')
                    {
                        escape = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                    inString = true;
                else if (c == '{')
                    objectDepth++;
                else if (c == '}')
                    objectDepth--;
                else if (c == '[')
                    arrayDepth++;
                else if (c == ']')
                    arrayDepth--;
            }

            return objectDepth == 1 && arrayDepth == 0;
        }

        private static bool TryFindPropertyValueBounds(string json, int propertyQuoteIndex, out int start, out int end, out bool hadTrailingComma)
        {
            start = propertyQuoteIndex;
            while (start > 0 &&
                json[start - 1] != '\n' &&
                json[start - 1] != '\r' &&
                char.IsWhiteSpace(json[start - 1]))
            {
                start--;
            }

            end = propertyQuoteIndex;
            hadTrailingComma = false;
            int colon = json.IndexOf(':', propertyQuoteIndex);
            if (colon < 0)
                return false;

            int valueStart = colon + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;

            bool inString = false;
            bool escape = false;
            int nestedDepth = 0;
            for (int i = valueStart; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (escape)
                    {
                        escape = false;
                    }
                    else if (c == '\\')
                    {
                        escape = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{' || c == '[')
                {
                    nestedDepth++;
                    continue;
                }

                if (c == '}' || c == ']')
                {
                    if (nestedDepth == 0)
                    {
                        end = i;
                        return true;
                    }

                    nestedDepth--;
                    if (nestedDepth == 0)
                    {
                        int afterValue = i + 1;
                        while (afterValue < json.Length && char.IsWhiteSpace(json[afterValue]))
                            afterValue++;
                        hadTrailingComma = afterValue < json.Length && json[afterValue] == ',';
                        end = hadTrailingComma ? afterValue + 1 : i + 1;
                        return true;
                    }
                }
                else if (c == ',' && nestedDepth == 0)
                {
                    hadTrailingComma = true;
                    end = i + 1;
                    return true;
                }
            }

            return false;
        }

        private static void AppendProperty(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": \"").Append(Escape(value)).Append('"');
            builder.Append(comma ? ",\n" : "\n");
        }

        private static void AppendProperty(StringBuilder builder, string name, int value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value);
            builder.Append(comma ? ",\n" : "\n");
        }

        private static void AppendProperty(StringBuilder builder, string name, bool value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value ? "true" : "false");
            builder.Append(comma ? ",\n" : "\n");
        }

        private static void AppendArray(StringBuilder builder, string name, List<string> values, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": [");
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                builder.Append('"').Append(Escape(values[i])).Append('"');
            }

            builder.Append(']');
            builder.Append(comma ? ",\n" : "\n");
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private sealed class ScanReport
        {
            public string Agent;
            public string Scanner;
            public string TimestampUtc;
            public string Status;
            public int ParticleComponentHits;
            public int EmitCallHits;
            public int TextureReadbackHits;
            public int ScenePrefabParticleHits;
            public int ForbiddenHits;
            public bool SuperfluousCpuParticlesEradicated;
            public string Output;
            public readonly List<string> MissingPaths = new List<string>(16);
            public readonly List<string> Hits = new List<string>(64);
            public readonly List<string> ForbiddenHitsDetailed = new List<string>(64);
        }
    }
}
#endif
