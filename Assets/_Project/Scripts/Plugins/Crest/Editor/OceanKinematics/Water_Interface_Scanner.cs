#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physics.Editor
{
    /// <summary>
    /// Cold editor scanner for direct/OOP ocean interface usage outside the ocean kinematics boundary.
    /// </summary>
    public static class Water_Interface_Scanner
    {
        private const string RootReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";
        private const string SidecarReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_261.json";
        private const string RootPropertyName = "shinobu261OceanKinematicsScanner";

        private static readonly string[] Patterns =
        {
            "FindObjectOfType<OceanRenderer",
            "FindAnyObjectByType<OceanRenderer",
            ".GetWaterHeight(",
            ".GetSurfaceFlow(",
            ".GetWaveNormal(",
            "IWaterSurface"
        };

        [MenuItem("Hecton8/Physics/Water Interface Scanner")]
        public static void Run()
        {
            ScanAndWriteReport();
        }

        public static void ScanAndWriteReport()
        {
            string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/_Project/Scripts" });
            StringBuilder findings = new StringBuilder(4096);
            StringBuilder legacyManagedCallers = new StringBuilder(512);
            int directOceanRendererLookups = 0;
            int managedWaterQueries = 0;
            int scanned = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (ShouldSkip(assetPath))
                    continue;

                scanned++;
                string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
                if (!File.Exists(fullPath))
                    continue;

                int lineNumber = 0;
                foreach (string lineText in File.ReadLines(fullPath))
                {
                    lineNumber++;
                    for (int p = 0; p < Patterns.Length; p++)
                    {
                        int index = lineText.IndexOf(Patterns[p], StringComparison.Ordinal);
                        while (index >= 0)
                        {
                            if (Patterns[p].Contains("OceanRenderer"))
                            {
                                directOceanRendererLookups++;
                            }
                            else
                            {
                                managedWaterQueries++;
                                AppendUniqueJsonString(legacyManagedCallers, assetPath);
                            }

                            AppendFinding(findings, assetPath, lineNumber, Patterns[p]);
                            index = lineText.IndexOf(Patterns[p], index + Patterns[p].Length, StringComparison.Ordinal);
                        }
                    }
                }
            }

            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "Docs/Reports"));
            string status = directOceanRendererLookups == 0 && managedWaterQueries == 0
                ? "OOP Water Queries Eradicated"
                : "OOP Water Queries Not Eradicated - legacy callers remain";
            string entryJson = "{\n" +
                               "  \"agent\": \"SHINOBU_261\",\n" +
                               "  \"scanner\": \"Water_Interface_Scanner\",\n" +
                               "  \"status\": \"" + status + "\",\n" +
                               "  \"generatedWithoutUnity\": false,\n" +
                               "  \"unityMenuRoute\": \"Hecton/Physics/Water Interface Scanner\",\n" +
                               "  \"scanScope\": \"Assets/_Project/Scripts excluding Plugins/Crest\",\n" +
                               "  \"dedicatedReport\": \"" + SidecarReportPath + "\",\n" +
                               "  \"runtimeRouteProof\": {\n" +
                               "    \"adapter\": \"Crest4KinematicsAdapter\",\n" +
                               "    \"requestDto\": \"OceanKinematicsSampleRequestDTO[50000] @ Vault 72940\",\n" +
                               "    \"resultDto\": \"FluidSampleResultDTO[50000] @ Vault 72941\",\n" +
                               "    \"cachedDearLie\": \"OceanCachedFluidSampleDTO[50000] @ Vault 72947\",\n" +
                               "    \"ownedPathScanPerformed\": false,\n" +
                               "    \"ownedPathScanReason\": \"Water_Interface_Scanner excludes Plugins/Crest so Task 19 measures external callers only; owned runtime forbidden patterns are verified by SHINOBU_261 scoped static gates.\",\n" +
                               "    \"ownedAdapterLegacyFacadeAllowed\": true,\n" +
                               "    \"ownedAdapterLegacyFacadeRoute\": \"Crest4KinematicsAdapter retains managed single-sample compatibility facades until Player/Flora migration; hot-path authority is the Vault-backed batch route.\",\n" +
                               "    \"compileGuardCaveat\": \"Hecton8.Crest.Bridge.asmdef still has a shared Hecton8.Core reference for legacy Crest bridge files and explicit cold registration/origin/quality seams; SHINOBU_261 scoped runtime files do not import Hecton8.Core directly and no sibling gameplay domain reference was added.\"\n" +
                               "  },\n" +
                               "  \"scannedScripts\": " + scanned.ToString() + ",\n" +
                               "  \"directOceanRendererLookups\": " + directOceanRendererLookups.ToString() + ",\n" +
                               "  \"managedWaterQueries\": " + managedWaterQueries.ToString() + ",\n" +
                               "  \"oopWaterQueriesEradicated\": " + (directOceanRendererLookups == 0 && managedWaterQueries == 0 ? "true" : "false") + ",\n" +
                               "  \"ownerBoundary\": \"If managedWaterQueries is nonzero, the remaining callers are outside the SHINOBU_261 Crest adapter write scope and require Player/Flora owner migration or integrator authorization.\",\n" +
                               "  \"requiredMigration\": \"Move remaining callers to Vault-backed OceanKinematicsSampleRequestDTO queues, OceanMacroStateDTO reads, or an owner-approved nonblocking batch bridge; do not add per-frame Crest/OceanRenderer calls.\",\n" +
                               "  \"legacyManagedCallers\": [\n" + legacyManagedCallers.ToString() + "\n  ],\n" +
                               "  \"findings\": [\n" + findings.ToString() + "\n  ]\n" +
                               "}\n";

            string rootPath = Path.Combine(Directory.GetCurrentDirectory(), RootReportPath);
            string sidecarPath = Path.Combine(Directory.GetCurrentDirectory(), SidecarReportPath);
            AtomicWriteText(sidecarPath, entryJson);
            WriteSharedRootReport(rootPath, entryJson);
            Debug.Log("[Water_Interface_Scanner] " + status + " -> " + RootReportPath + " and " + SidecarReportPath);
        }

        private static void WriteSharedRootReport(string rootPath, string entryJson)
        {
            string lockPath = rootPath + ".lock";
            Directory.CreateDirectory(Path.GetDirectoryName(rootPath));
            for (int attempt = 0; attempt < 8; attempt++)
            {
                try
                {
                    using (FileStream lockStream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                    {
                        lockStream.SetLength(0);
                        byte[] ownerBytes = Encoding.ASCII.GetBytes("SHINOBU_261_WATER_INTERFACE_SCANNER");
                        lockStream.Write(ownerBytes, 0, ownerBytes.Length);
                        string rootJson = File.Exists(rootPath)
                            ? File.ReadAllText(rootPath)
                            : string.Empty;
                        AtomicWriteText(rootPath, UpsertRootReport(rootJson, entryJson));
                    }

                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(25);
                }
            }

            throw new IOException("Water_Interface_Scanner could not acquire the shared report lock.");
        }

        private static void AtomicWriteText(string path, string text)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string tempPath = path + ".tmp";
            string backupPath = path + ".bak";
            File.WriteAllText(tempPath, text);
            if (File.Exists(path))
            {
                if (File.Exists(backupPath))
                    File.Delete(backupPath);

                File.Replace(tempPath, path, backupPath, true);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }

        private static bool ShouldSkip(string assetPath)
        {
            return assetPath.Contains("/Physics/OceanKinematics/") ||
                   assetPath.Contains("/Plugins/Crest/") ||
                   assetPath.EndsWith("/IHectonOceanKinematics.cs", StringComparison.Ordinal) ||
                   assetPath.EndsWith("/IOceanKinematics.cs", StringComparison.Ordinal) ||
                   assetPath.EndsWith("/HectonOceanKinematicsBridgeBase.cs", StringComparison.Ordinal) ||
                   assetPath.EndsWith("/OceanKinematicsRuntimeService.cs", StringComparison.Ordinal) ||
                   assetPath.EndsWith("/HectonOceanRegistry.cs", StringComparison.Ordinal) ||
                   assetPath.EndsWith("/OceanAdapterContracts.cs", StringComparison.Ordinal);
        }

        private static void AppendFinding(StringBuilder builder, string path, int line, string pattern)
        {
            if (builder.Length > 0)
                builder.Append(",\n");

            builder.Append("    { \"path\": \"");
            builder.Append(Escape(path));
            builder.Append("\", \"line\": ");
            builder.Append(line);
            builder.Append(", \"pattern\": \"");
            builder.Append(Escape(pattern));
            builder.Append("\" }");
        }

        private static void AppendUniqueJsonString(StringBuilder builder, string path)
        {
            string escaped = Escape(path);
            string needle = "\"" + escaped + "\"";
            if (builder.ToString().IndexOf(needle, StringComparison.Ordinal) >= 0)
                return;

            if (builder.Length > 0)
                builder.Append(",\n");

            builder.Append("    \"");
            builder.Append(escaped);
            builder.Append("\"");
        }

        private static string UpsertRootReport(string rootJson, string entryJson)
        {
            string propertyJson = BuildRootProperty(entryJson);
            if (string.IsNullOrWhiteSpace(rootJson))
                return "{\n" + propertyJson + "\n}\n";

            int objectStart = rootJson.IndexOf('{');
            int objectEnd = rootJson.LastIndexOf('}');
            if (objectStart < 0 || objectEnd <= objectStart)
                return "{\n" + propertyJson + "\n}\n";

            int propertyStart;
            int propertyEnd;
            if (TryFindTopLevelProperty(rootJson, RootPropertyName, objectStart, objectEnd, out propertyStart, out propertyEnd))
                return rootJson.Substring(0, propertyStart) + propertyJson.TrimStart() + rootJson.Substring(propertyEnd);

            bool hasExistingContent = HasObjectContent(rootJson, objectStart + 1, objectEnd);
            string separator = hasExistingContent ? ",\n" : "\n";
            return rootJson.Substring(0, objectEnd).TrimEnd() + separator + propertyJson + "\n}\n";
        }

        private static string BuildRootProperty(string entryJson)
        {
            string normalized = entryJson.TrimEnd('\r', '\n');
            normalized = normalized.Replace("\r\n", "\n").Replace("\n", "\n  ");
            return "  \"" + RootPropertyName + "\": " + normalized;
        }

        private static bool HasObjectContent(string text, int start, int end)
        {
            for (int i = start; i < end && i < text.Length; i++)
            {
                char c = text[i];
                if (!char.IsWhiteSpace(c))
                    return true;
            }

            return false;
        }

        private static bool TryFindTopLevelProperty(
            string json,
            string propertyName,
            int objectStart,
            int objectEnd,
            out int propertyStart,
            out int propertyEnd)
        {
            propertyStart = -1;
            propertyEnd = -1;
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = objectStart; i < objectEnd && i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    int keyStart = i;
                    int keyEnd = FindStringEnd(json, keyStart + 1, objectEnd);
                    if (depth == 1 &&
                        keyEnd > keyStart &&
                        keyEnd == keyStart + 1 + propertyName.Length &&
                        string.CompareOrdinal(json, keyStart + 1, propertyName, 0, propertyName.Length) == 0)
                    {
                        int colon = SkipWhitespace(json, keyEnd + 1, objectEnd);
                        if (colon < objectEnd && json[colon] == ':')
                        {
                            int valueStart = SkipWhitespace(json, colon + 1, objectEnd);
                            int valueEnd = SkipJsonValue(json, valueStart, objectEnd);
                            propertyStart = keyStart;
                            propertyEnd = valueEnd;
                            return true;
                        }
                    }

                    i = keyEnd;
                    continue;
                }

                if (c == '{' || c == '[')
                    depth++;
                else if (c == '}' || c == ']')
                    depth--;
            }

            return false;
        }

        private static int FindStringEnd(string text, int start, int end)
        {
            bool escaped = false;
            for (int i = start; i < end && i < text.Length; i++)
            {
                char c = text[i];
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    return i;
                }
            }

            return end;
        }

        private static int SkipWhitespace(string text, int start, int end)
        {
            int i = start;
            while (i < end && i < text.Length && char.IsWhiteSpace(text[i]))
                i++;
            return i;
        }

        private static int SkipJsonValue(string text, int start, int end)
        {
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = start; i < end && i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
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
                    depth++;
                    continue;
                }

                if (c == '}' || c == ']')
                {
                    if (depth == 0)
                        return i;

                    depth--;
                    continue;
                }

                if (depth == 0 && c == ',')
                    return i;
            }

            return end;
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif
