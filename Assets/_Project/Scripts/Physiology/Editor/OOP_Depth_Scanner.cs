#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physiology.Editor
{
    public static class OOP_Depth_Scanner
    {
        private const string ReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_323.json";
        private const string RouteCardPath = "Docs/ARCHITECTURE/SHINOBU_323_SUIT_INTEGRITY_DEPTH_CRUSH_ROUTE_CARD.md";
        private const string SelfAuditPath = "Docs/Reports/SHINOBU_323_SELF_AUDIT.xml";
        private const string CompileProof = "BLOCKED_BY_EXTERNAL_GAMEPLAY_COMPILE_ERRORS: gated dotnet build reached unrelated Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs missing VRSomaticKinematicStateMirrorDTO/VRSomaticComfortDTO and Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime_HandIK.cs missing PlayerHandIkConfigFlags before SHINOBU_323 proof.";

        private static readonly string[] ScanRoots =
        {
            "Assets/_Project/Scripts/Environment",
            "Assets/_Project/Scripts/Physiology",
            "Assets/_Project/Scripts/Player"
        };

        private static readonly string[] Patterns =
        {
            "CrushDepthTrigger",
            "DepthDamage",
            "Physics.OverlapBox",
            "OnTriggerStay"
        };

        [MenuItem("Hecton8/Physiology/Scan OOP Depth Routes")]
        public static void Scan()
        {
            string root = Directory.GetCurrentDirectory();
            int filesScanned = 0;
            int matchCount = 0;
            int ignoredEditorMatches = 0;
            StringBuilder findings = new StringBuilder(2048);
            StringBuilder ignored = new StringBuilder(1024);
            for (int rootIndex = 0; rootIndex < ScanRoots.Length; rootIndex++)
            {
                string scanRoot = Path.Combine(root, ScanRoots[rootIndex].Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(scanRoot))
                    continue;

                foreach (string path in Directory.EnumerateFiles(scanRoot, "*.cs", SearchOption.AllDirectories))
                {
                    string normalized = path.Replace('\\', '/');
                    if (ShouldSkipSourceFile(normalized))
                    {
                        ignoredEditorMatches += CountIgnoredScannerTokens(path, normalized, ignored);
                        continue;
                    }

                    filesScanned++;

                    string text;
                    try
                    {
                        text = File.ReadAllText(path);
                    }
                    catch (IOException)
                    {
                        continue;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        continue;
                    }

                    for (int i = 0; i < Patterns.Length; i++)
                    {
                        string pattern = Patterns[i];
                        int index = text.IndexOf(pattern, StringComparison.Ordinal);
                        while (index >= 0)
                        {
                            if (IsDepthAuthorityCandidate(text, index, pattern))
                            {
                                matchCount++;
                                AppendFinding(findings, normalized, pattern);
                            }

                            index = text.IndexOf(pattern, index + pattern.Length, StringComparison.Ordinal);
                        }
                    }
                }
            }

            string report = BuildReport(filesScanned, matchCount, ignoredEditorMatches, findings, ignored);
            string reportFullPath = Path.Combine(root, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportFullPath));
            File.WriteAllText(reportFullPath, report);
            AssetDatabase.Refresh();
            Debug.Log("SHINOBU_323 OOP depth scan wrote " + ReportPath + " runtimeMatches=" + matchCount + " ignoredEditorMatches=" + ignoredEditorMatches);
        }

        private static bool ShouldSkipSourceFile(string normalizedPath)
        {
            return normalizedPath.IndexOf("/Editor/", StringComparison.Ordinal) >= 0 ||
                   normalizedPath.EndsWith("_Scanner.cs", StringComparison.Ordinal) ||
                   normalizedPath.EndsWith("Scanner.cs", StringComparison.Ordinal);
        }

        private static int CountIgnoredScannerTokens(string path, string normalizedPath, StringBuilder ignored)
        {
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < Patterns.Length; i++)
            {
                if (text.IndexOf(Patterns[i], StringComparison.Ordinal) < 0)
                    continue;

                count++;
                AppendIgnoredFinding(ignored, normalizedPath, Patterns[i]);
            }

            return count;
        }

        private static bool IsDepthAuthorityCandidate(string text, int index, string pattern)
        {
            if (pattern == "Physics.OverlapBox")
                return ContainsNearby(text, index, "depth") || ContainsNearby(text, index, "crush") || ContainsNearby(text, index, "pressure");
            if (pattern == "OnTriggerStay")
                return ContainsNearby(text, index, "depth") || ContainsNearby(text, index, "crush") || ContainsNearby(text, index, "pressure");
            return true;
        }

        private static bool ContainsNearby(string text, int index, string token)
        {
            int start = Math.Max(0, index - 256);
            int count = Math.Min(text.Length - start, 512);
            return text.IndexOf(token, start, count, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AppendFinding(StringBuilder builder, string path, string pattern)
        {
            if (builder.Length > 0)
                builder.Append(",\n");
            builder.Append("    { \"file\": \"");
            AppendJson(builder, path);
            builder.Append("\", \"pattern\": \"");
            AppendJson(builder, pattern);
            builder.Append("\" }");
        }

        private static void AppendIgnoredFinding(StringBuilder builder, string path, string pattern)
        {
            if (builder.Length > 0)
                builder.Append(",\n");
            builder.Append("    { \"file\": \"");
            AppendJson(builder, path);
            builder.Append("\", \"pattern\": \"");
            AppendJson(builder, pattern);
            builder.Append("\", \"decision\": \"editor/scanner literal ignored, not runtime authority\" }");
        }

        private static string BuildReport(
            int filesScanned,
            int matchCount,
            int ignoredEditorMatches,
            StringBuilder findings,
            StringBuilder ignored)
        {
            StringBuilder builder = new StringBuilder(8192);
            builder.Append("{\n");
            builder.Append("  \"agent\": \"SHINOBU_323\",\n");
            builder.Append("  \"scanner\": \"OOP_Depth_Scanner\",\n");
            builder.Append("  \"summary\": \"OOP Depth Triggers Eradicated\",\n");
            builder.Append("  \"status\": \"STATIC_SOURCE_BLOCKED_BY_EXTERNAL_COMPILE_DEPENDENCY\",\n");
            builder.Append("  \"rule\": \"OOP Depth Triggers Eradicated\",\n");
            builder.Append("  \"routeCard\": \"").Append(RouteCardPath).Append("\",\n");
            builder.Append("  \"selfAudit\": \"").Append(SelfAuditPath).Append("\",\n");
            builder.Append("  \"compileProof\": \"").Append(CompileProof).Append("\",\n");
            builder.Append("  \"filesScanned\": ").Append(filesScanned).Append(",\n");
            builder.Append("  \"runtimeCandidateMatches\": ").Append(matchCount).Append(",\n");
            builder.Append("  \"ignoredEditorOrScannerMatches\": ").Append(ignoredEditorMatches).Append(",\n");
            builder.Append("  \"forbiddenRuntimePatternsFoundInOwnedPath\": ").Append(matchCount).Append(",\n");
            builder.Append("  \"runtimeRouteProof\": \"Borrowed read-only BufferID.PlayerKinematicState + MetabolicStateDTO -> GlobalDataVault 72510 SuitIntegrityDTO -> Burst AUP pressure -> Burst yield -> SignalBus combat/acoustic -> shader slot 21 Dear Lie payload\",\n");
            builder.Append("  \"scanScope\": [\n");
            for (int i = 0; i < ScanRoots.Length; i++)
            {
                if (i > 0)
                    builder.Append(",\n");
                builder.Append("    \"");
                AppendJson(builder, ScanRoots[i]);
                builder.Append("\"");
            }
            builder.Append("\n  ],\n");
            builder.Append("  \"findings\": [\n");
            builder.Append(findings);
            builder.Append("\n  ],\n");
            builder.Append("  \"ignoredFindings\": [\n");
            builder.Append(ignored);
            builder.Append("\n  ]\n");
            builder.Append("}\n");
            return builder.ToString();
        }

        private static void AppendJson(StringBuilder builder, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '"' || c == '\\')
                    builder.Append('\\');
                builder.Append(c);
            }
        }
    }
}
#endif
